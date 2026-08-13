using System;
using System.Collections.Generic;
using FXOverdose.Events;
using FXOverdose.P2P.Core;
using FXOverdose.Trading;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace FXOverdose.P2P.Infrastructure
{
    /// <summary>체력·멘탈, 케어 아이템, 15초 공통 이벤트와 경기 종료를 호스트에서 판정합니다.</summary>
    public sealed class NetworkCompetitionAuthority : MonoBehaviour
    {
        private const string ActionMessage="FXO.P2P.CompetitionAction.v1", StateMessage="FXO.P2P.CompetitionState.v1";
        private NetworkManager net; private NetworkTradingAuthority trading; private NetworkMarketAuthority market;
        private bool registered, initialized, eventActive, finished; private float tick, broadcastTick, eventDeadline;
        private int eventId,randomEventsTriggered,nextRandomEventMinute; private readonly Dictionary<ulong,int> choices=new();
        private ulong stateSequence,lastAppliedSequence;
        private const float ReconnectGraceSeconds=15f;
        private readonly Dictionary<ulong,float> reconnectDeadlines=new();
        private readonly P2PRequestSecurityGuard requestGuard=new(6,6);
        private ChoiceEventSO currentEvent;
        public P2PCompetitionSnapshot Current { get; private set; } = new();
        public event Action StateChanged;

        public void ResetForSession()
        {
            initialized=false;eventActive=false;finished=false;tick=broadcastTick=eventDeadline=0;eventId=randomEventsTriggered=0;nextRandomEventMinute=0;stateSequence=lastAppliedSequence=0;
            choices.Clear();reconnectDeadlines.Clear();requestGuard.Reset();currentEvent=null;Current=new P2PCompetitionSnapshot();
        }

        private void Update()
        {
            net ??= NetworkManager.Singleton; if(!P2PNetworkSessionManager.CanSend(net))return; EnsureRegistered();
            if(!net.IsServer)return; var match=trading.HostMatch;if(match==null)return;
            if(!initialized){initialized=true;nextRandomEventMinute=UnityEngine.Random.Range(10*60,16*60);foreach(var p in match.Players){p.SetInventoryAmount("energy_drink",5);p.SetInventoryAmount("dessert",5);p.SetInventoryAmount("sedative",2);p.SetInventoryAmount("supplement",2);}Broadcast("경기가 시작됐어용");}
            if(finished)return;
            tick+=Time.unscaledDeltaTime;broadcastTick+=Time.unscaledDeltaTime;
            if(tick>=1f)
            {
                float seconds=tick;tick=0;
                // TraderStatus.DecreaseStatusOverTime의 1일차·기본 아이템 상태 공식을 호스트에서 동일하게 계산합니다.
                const double secondsPerGameMinute=FXOverdose.P2P.Market.P2PMarketSimulationEngine.SecondsPerGameMinute;
                double speedScale=5d/secondsPerGameMinute;
                double healthDrainPerSecond=(.05d+.005d)*1.5d*speedScale;
                double baseMentalDrainPerSecond=.04d*speedScale;
                foreach(var p in match.Players)
                {
                    if(p.IsEliminated)continue;
                    double previousHealth=p.Health;
                    p.ChangeHealth(-healthDrainPerSecond*seconds);
                    double appliedHealthLoss=Math.Max(0d,previousHealth-p.Health);
                    double mentalDrain=0d;
                    // 원본처럼 HP 50 이하 구간에서 실제로 감소한 HP만큼 멘탈도 함께 감소합니다.
                    if(previousHealth<=50d)mentalDrain+=appliedHealthLoss;
                    else if(p.Health<50d)mentalDrain+=50d-p.Health;
                    if(p.Health<=0d)mentalDrain+=baseMentalDrainPerSecond*4d*seconds;
                    else if(p.Health<=50d)mentalDrain+=baseMentalDrainPerSecond*seconds;
                    if(mentalDrain>0d)p.ChangeMental(-mentalDrain);
                    // 원본 MentalDrainGimmickController의 미실현 손실 압박은 현실 시간 1초마다 적용됩니다.
                    if(p.Position.IsOpen&&p.Position.MarginAmount>0d)
                    {
                        double roe=p.Position.UnrealizedPnL/p.Position.MarginAmount*100d;
                        double pressure=roe<=-20d?.15d:roe<=-10d?.05d:roe<=-5d?.01d:0d;
                        if(eventActive)pressure*=.5d;
                        if(pressure>0d)p.ChangeMental(-pressure*seconds);
                    }
                    Evaluate(p,match);
                }
            }
            var snapshot=market.CurrentSnapshot;
            if(!eventActive&&randomEventsTriggered<2&&snapshot.TotalMinutes>=nextRandomEventMinute)
            {
                StartEvent(EventTriggerCondition.TimeOfDay);randomEventsTriggered++;
                if(randomEventsTriggered<2)nextRandomEventMinute=UnityEngine.Random.Range(Math.Min(snapshot.TotalMinutes+60,23*60),Math.Min(snapshot.TotalMinutes+360,23*60+20));
            }
            if(eventActive && Time.unscaledTime>=eventDeadline) ResolveEvent(match);
            ResolveExpiredReconnects(match);
            int alive=0;foreach(var p in match.Players)if(!p.IsEliminated)alive++;
            // 마지막 생존자는 경기 종료 시각까지 플레이할 수 있고, 전원이 탈락했을 때만 즉시 결과를 확정합니다.
            if(snapshot.IsFinished || alive==0) Finish(match);
            if(broadcastTick>=.25f){broadcastTick=0;Broadcast(string.Empty);}
        }

        public bool Submit(P2PCompetitionAction action,string value)
        {
            net??=NetworkManager.Singleton;
            if(!P2PNetworkSessionManager.CanSend(net))return false;
            EnsureRegistered();
            byte[] b=P2PCompetitionCodec.EncodeAction(SteamRuntimeBootstrap.LocalSteamId,action,value);
            P2PNetworkSessionManager.Instance?.Diagnostics?.RecordSent(b.Length);
            if(net.IsServer)
            {
                if(trading?.HostMatch==null)return false;
                Process(net.LocalClientId,b);
            }
            else
            {
                using var w=Writer(b);
                net.CustomMessagingManager.SendNamedMessage(ActionMessage,NetworkManager.ServerClientId,w,NetworkDelivery.ReliableSequenced);
            }
            return true;
        }

        private void Process(ulong sender,byte[] bytes)
        {
            if(finished)return;
            ulong actual=SteamRuntimeBootstrap.LocalSteamId;
            if(sender!=net.LocalClientId&&!P2PNetworkSessionManager.Instance.TryGetSteamId(sender,out actual)){BlockClient(sender,"Steam ID mapping missing");return;}
            P2PRequestGuardResult guardResult=requestGuard.TryConsume(actual,Time.unscaledTime);
            if(guardResult!=P2PRequestGuardResult.Allowed){if(guardResult==P2PRequestGuardResult.Blocked)BlockClient(sender,"competition rate limit");return;}
            if(!P2PCompetitionCodec.TryDecodeAction(bytes,out ulong claimed,out var action,out string value))
            {if(requestGuard.RecordViolation(actual,Time.unscaledTime)==P2PRequestGuardResult.Blocked)BlockClient(sender,"malformed competition packets");return;}
            if(actual!=claimed)
            {if(requestGuard.RecordViolation(actual,Time.unscaledTime)==P2PRequestGuardResult.Blocked)BlockClient(sender,"spoofed Steam ID");return;}
            var match=trading.HostMatch;if(match==null||!match.TryGetPlayer(actual,out var p)||p.IsEliminated)return;
            string message=string.Empty;
            if(action==P2PCompetitionAction.ChooseEvent){if(eventActive&&int.TryParse(value,out int c)&&P2PChoiceRules.IsValid(c)&&!choices.ContainsKey(actual)){choices[actual]=c;message=$"{p.DisplayName} 선택 완료";}}
            else if(action==P2PCompetitionAction.BuyItem) message=Buy(p,value);
            else if(action==P2PCompetitionAction.UseItem) message=Use(p,value);
            Broadcast(message);trading.BroadcastCurrentState();
        }
        private void BlockClient(ulong sender,string reason)
        {
            Debug.LogWarning($"[P2P Security][{P2PNetworkSessionManager.Instance?.MatchId}] client={sender} 차단 · {reason}");
            if(sender!=net.LocalClientId)net.DisconnectClient(sender);
        }

        private static string Buy(P2PPlayerRuntimeState p,string id)
        { if(!TryItem(id,out int cost,out _,out _))return "배달음식과 액티브 기어는 사용할 수 없어용";if(p.CashBalance<cost)return "잔액이 부족해용";p.ChangeCash(-cost);p.SetInventoryAmount(id,Count(p,id)+1);return $"{id} 구매 완료"; }
        private static string Use(P2PPlayerRuntimeState p,string id)
        { if(!TryItem(id,out _,out double health,out double mental)||Count(p,id)<=0)return "사용할 수 없는 아이템이에용";p.SetInventoryAmount(id,Count(p,id)-1);p.ChangeHealth(health);p.ChangeMental(mental);return $"{id} 사용 완료"; }
        private static bool TryItem(string id,out int cost,out double health,out double mental)
        { cost=0;health=mental=0;switch(id){case "energy_drink":cost=500;health=30;return true;case "dessert":cost=600;mental=20;return true;case "sedative":cost=1000;mental=40;return true;case "supplement":cost=900;health=50;return true;default:return false;} }
        private static int Count(P2PPlayerRuntimeState p,string id)=>p.Inventory.TryGetValue(id,out int n)?n:0;

        private void StartEvent(EventTriggerCondition preferredCondition)
        {
            ChoiceEventSO[] assets=Resources.LoadAll<ChoiceEventSO>("Events");
            List<ChoiceEventSO> events=assets!=null&&assets.Length>0?new List<ChoiceEventSO>(assets):ChoiceEventRuntimeData.GetDefaultEvents();
            if(events.Count==0)return;
            List<ChoiceEventSO> candidates=events.FindAll(e=>e!=null&&(e.TriggerCondition==preferredCondition||e.TriggerCondition==EventTriggerCondition.Any));
            if(candidates.Count==0)candidates=events;
            currentEvent=candidates[UnityEngine.Random.Range(0,candidates.Count)];
            if(currentEvent==null||currentEvent.Options==null||currentEvent.Options.Length<2)return;
            eventActive=true;eventId++;choices.Clear();eventDeadline=Time.unscaledTime+15f;
            trading.HostMatch.IsChoiceEventActive=true;market.SetPausedByHost(true);
            Broadcast("돌발 이벤트: 15초 안에 선택해용");
        }
        private void ResolveEvent(P2PLocalMatch match)
        {
            // 차트는 전 플레이어가 공유하므로 서버가 공용 결과를 단 한 번만 결정합니다.
            // 플레이어 처리 순서에 따라 마지막 선택이 차트를 덮어쓰는 문제를 방지합니다.
            int serverMarketChoice=UnityEngine.Random.Range(0,P2PChoiceRules.OptionCount);
            ChoiceOptionData sharedMarketOption=currentEvent.Options[serverMarketChoice];
            foreach(var p in match.Players)
            {
                if(p.IsEliminated)continue;
                if(!choices.TryGetValue(p.PlayerId,out int c))c=UnityEngine.Random.Range(0,P2PChoiceRules.OptionCount);
                ApplyOriginalOption(p,currentEvent.Options[c],match);
                Evaluate(p,match);
            }
            ApplySharedMarketOption(sharedMarketOption);
            eventActive=false;match.IsChoiceEventActive=false;market.SetPausedByHost(false);
            string selectedTitle=string.IsNullOrWhiteSpace(sharedMarketOption?.OptionTitle)?$"{serverMarketChoice+1}안":sharedMarketOption.OptionTitle;
            Broadcast($"서버가 {serverMarketChoice+1}안 · {selectedTitle} 시장 결과를 적용했어용");trading.BroadcastCurrentState();currentEvent=null;
        }

        private void ApplyOriginalOption(P2PPlayerRuntimeState player,ChoiceOptionData option,P2PLocalMatch match)
        {
            if(option==null)return;
            bool directional=option.OptionType==ChoiceOptionType.DirectionalLong||option.OptionType==ChoiceOptionType.DirectionalShort;
            bool success=option.OverrideSignalProbTrue>=1f||(option.OverrideSignalProbTrue>0f&&UnityEngine.Random.value<=option.OverrideSignalProbTrue);
            if(!directional||success){player.ChangeMental(option.MentalChangeAmount);player.ChangeHealth(option.HealthChangeAmount);}

            if(option.ForcePosition==TradingController.PositionType.None&&option.OptionType==ChoiceOptionType.Safe)
            {
                if(player.Position.IsOpen)P2PTradeCalculator.ClosePosition(player,match.Rules,match.MarketPrice);
            }
            else if(option.ForceLeverage>0||option.ForcePosition!=TradingController.PositionType.None)
            {
                if(player.Position.IsOpen)P2PTradeCalculator.ClosePosition(player,match.Rules,match.MarketPrice);
                P2PTradeAction action=option.ForcePosition==TradingController.PositionType.Short?P2PTradeAction.OpenShort:P2PTradeAction.OpenLong;
                int leverage=Math.Max(1,Math.Min(option.ForceLeverage>0?option.ForceLeverage:10,match.Rules.MaximumLeverage));
                P2PTradeCalculator.OpenPosition(player,new P2PTradeRequest(0,action,leverage,match.Rules.MaximumMarginRatio),match.Rules,match.MarketPrice);
                player.RecordPositionOpened();
            }

        }

        private void ApplySharedMarketOption(ChoiceOptionData option)
        {
            if(option==null||Math.Abs(option.OverrideBeamPercent)<=.001f)return;
            bool directional=option.OptionType==ChoiceOptionType.DirectionalLong||option.OptionType==ChoiceOptionType.DirectionalShort;
            bool success=option.OverrideSignalProbTrue>=1f||(option.OverrideSignalProbTrue>0f&&UnityEngine.Random.value<=option.OverrideSignalProbTrue);
            double beam=option.OverrideBeamPercent;
            TradingController.PositionType position=directional
                ?(option.OptionType==ChoiceOptionType.DirectionalShort?TradingController.PositionType.Short:TradingController.PositionType.Long)
                :option.ForcePosition;
            if(directional)beam=success?(position==TradingController.PositionType.Long?Math.Abs(beam):-Math.Abs(beam)):(position==TradingController.PositionType.Long?-Math.Abs(beam)*.7d:Math.Abs(beam)*.7d);
            else if(!success&&position!=TradingController.PositionType.None)
            {
                bool opposite=position==TradingController.PositionType.Long&&beam<0||position==TradingController.PositionType.Short&&beam>0;
                if(!opposite)beam=position==TradingController.PositionType.Long?-Math.Abs(beam)*.7d:Math.Abs(beam)*.7d;
            }
            market.OverrideMarketTrendByHost(beam,150);
        }
        private static void Evaluate(P2PPlayerRuntimeState p,P2PLocalMatch match)
        { P2PEliminationReason reason=p.Mental<=0?P2PEliminationReason.MentalDepleted:p.TotalEquity<=0?P2PEliminationReason.Bankruptcy:P2PEliminationReason.None;if(reason==P2PEliminationReason.None)return;if(p.Position.IsOpen)P2PTradeCalculator.ClosePosition(p,match.Rules,match.MarketPrice);p.TryEliminate(reason,(long)Time.unscaledTime); }
        private void Finish(P2PLocalMatch match){finished=true;eventActive=false;market.SetPausedByHost(true);match.IsChoiceEventActive=false;match.Finish();trading.BroadcastCurrentState();Broadcast("경기 종료 · 최종 순위가 확정됐어용");}

        private void Broadcast(string message)
        { if(!P2PNetworkSessionManager.CanSend(net))return;var match=trading?.HostMatch;if(match==null)return;var list=new List<P2PCompetitionPlayerSnapshot>();foreach(var p in match.Players)list.Add(new P2PCompetitionPlayerSnapshot(p.PlayerId,p.Health,p.Mental,p.IsEliminated,p.EliminationReason,Count(p,"energy_drink"),Count(p,"dessert"),Count(p,"sedative"),Count(p,"supplement")));ChoiceOptionData a=eventActive&&currentEvent?.Options?.Length>0?currentEvent.Options[0]:null;ChoiceOptionData c=eventActive&&currentEvent?.Options?.Length>1?currentEvent.Options[1]:null;var x=new P2PCompetitionSnapshot{StateSequence=++stateSequence,Players=list,EventActive=eventActive,EventId=eventId,EventKey=currentEvent?.EventID??"",EventTitle=currentEvent?.ScenarioTitle??"",EventDescription=currentEvent?.ScenarioDescription??"",EventMonologue=currentEvent?.AIMonologue??"",Choice1Title=a?.OptionTitle??"",Choice1Description=a?.Description??"",Choice1Type=(byte)(a?.OptionType??ChoiceOptionType.Safe),Choice2Title=c?.OptionTitle??"",Choice2Description=c?.Description??"",Choice2Type=(byte)(c?.OptionType??ChoiceOptionType.Aggressive),EventSecondsLeft=eventActive?Math.Max(0,eventDeadline-Time.unscaledTime):0,Finished=finished,LastMessage=string.IsNullOrEmpty(message)?Current.LastMessage:message};byte[] b=P2PCompetitionCodec.EncodeState(x);P2PNetworkSessionManager.Instance?.Diagnostics?.RecordSent(b.Length*net.ConnectedClientsIds.Count);Apply(b);using var w=Writer(b);net.CustomMessagingManager.SendNamedMessage(StateMessage,net.ConnectedClientsIds,w,finished||!string.IsNullOrEmpty(message)?NetworkDelivery.ReliableSequenced:NetworkDelivery.UnreliableSequenced); }
        private void ReceiveAction(ulong sender,FastBufferReader reader){if(!net.IsServer)return;reader.ReadValueSafe(out byte[] b);P2PNetworkSessionManager.Instance?.Diagnostics?.RecordReceived(b?.Length??0);Process(sender,b);}
        private void ReceiveState(ulong sender,FastBufferReader reader){if(net.IsServer||sender!=NetworkManager.ServerClientId)return;reader.ReadValueSafe(out byte[] b);P2PNetworkSessionManager.Instance?.Diagnostics?.RecordReceived(b?.Length??0);Apply(b);}
        private void Apply(byte[] b){if(!P2PCompetitionCodec.TryDecodeState(b,SteamRuntimeBootstrap.LocalSteamId,out var x)||!IsSequenceNewer(x.StateSequence,lastAppliedSequence))return;lastAppliedSequence=x.StateSequence;Current=x;StateChanged?.Invoke();}
        private static bool IsSequenceNewer(ulong candidate,ulong previous)=>candidate!=previous&&unchecked((long)(candidate-previous))>0;
        private void EnsureRegistered(){if(registered)return;registered=true;trading=GetComponent<NetworkTradingAuthority>();market=GetComponent<NetworkMarketAuthority>();net.CustomMessagingManager.RegisterNamedMessageHandler(ActionMessage,ReceiveAction);net.CustomMessagingManager.RegisterNamedMessageHandler(StateMessage,ReceiveState);var session=P2PNetworkSessionManager.Instance;if(session!=null){session.SteamClientDisconnected-=OnSteamClientDisconnected;session.SteamClientDisconnected+=OnSteamClientDisconnected;session.ClientMapped-=OnClientMapped;session.ClientMapped+=OnClientMapped;}}
        private void OnSteamClientDisconnected(ulong steamId)
        {
            if(!P2PNetworkSessionManager.CanSend(net)||!net.IsServer||trading?.HostMatch==null||!trading.HostMatch.SetPlayerConnected(steamId,false))return;
            reconnectDeadlines[steamId]=Time.unscaledTime+ReconnectGraceSeconds;
            Broadcast($"플레이어 연결 끊김 · {ReconnectGraceSeconds:0}초 재접속 대기");trading.BroadcastCurrentState();
        }
        private void OnClientMapped(ulong clientId,ulong steamId)
        {
            if(!P2PNetworkSessionManager.CanSend(net)||!net.IsServer||trading?.HostMatch==null||!reconnectDeadlines.Remove(steamId))return;
            trading.HostMatch.SetPlayerConnected(steamId,true);
            Broadcast("플레이어가 재접속해 상태를 복구했어용");trading.BroadcastCurrentState();
            Debug.Log($"[P2P Reconnect][{P2PNetworkSessionManager.Instance?.MatchId}] Steam={steamId} client={clientId} 스냅샷 복구");
        }
        private void ResolveExpiredReconnects(P2PLocalMatch match)
        {
            if(reconnectDeadlines.Count==0)return;
            var expired=new List<ulong>();
            foreach(var pair in reconnectDeadlines)if(Time.unscaledTime>=pair.Value)expired.Add(pair.Key);
            foreach(ulong steamId in expired)
            {
                reconnectDeadlines.Remove(steamId);
                if(match.ForfeitDisconnectedPlayer(steamId,(long)market.CurrentSnapshot.Sequence))
                {Broadcast("재접속 유예 시간이 끝나 기권 처리됐어용");trading.BroadcastCurrentState();}
            }
        }
        private static FastBufferWriter Writer(byte[] b){var w=new FastBufferWriter(sizeof(int)+b.Length,Allocator.Temp);w.WriteValueSafe(b);return w;}
        private void OnDestroy(){var session=P2PNetworkSessionManager.Instance;if(session!=null){session.SteamClientDisconnected-=OnSteamClientDisconnected;session.ClientMapped-=OnClientMapped;}if(!registered||net?.CustomMessagingManager==null)return;net.CustomMessagingManager.UnregisterNamedMessageHandler(ActionMessage);net.CustomMessagingManager.UnregisterNamedMessageHandler(StateMessage);}
    }
}
