using System;
using System.Collections.Generic;
using FXOverdose.P2P.Core;
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
        private int eventId; private readonly Dictionary<ulong,int> choices=new();
        public P2PCompetitionSnapshot Current { get; private set; } = new();
        public event Action StateChanged;

        public void ResetForSession()
        {
            initialized=false;eventActive=false;finished=false;tick=broadcastTick=eventDeadline=0;eventId=0;
            choices.Clear();Current=new P2PCompetitionSnapshot();
        }

        private void Update()
        {
            net ??= NetworkManager.Singleton; if(net==null||!net.IsListening)return; EnsureRegistered();
            if(!net.IsServer)return; var match=trading.HostMatch;if(match==null)return;
            if(!initialized){initialized=true;foreach(var p in match.Players){p.SetInventoryAmount("energy_drink",5);p.SetInventoryAmount("dessert",5);p.SetInventoryAmount("sedative",2);p.SetInventoryAmount("supplement",2);}Broadcast("경기가 시작됐어용");}
            if(finished)return;
            tick+=Time.unscaledDeltaTime;broadcastTick+=Time.unscaledDeltaTime;
            if(tick>=1f)
            {
                float seconds=tick;tick=0;
                // TraderStatus.DecreaseStatusOverTime의 1일차·기본 아이템 상태 공식을 호스트에서 동일하게 계산합니다.
                const double secondsPerGameMinute=.666d;
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
                    Evaluate(p,match);
                }
            }
            var snapshot=market.CurrentSnapshot;
            if(!eventActive && snapshot.TotalMinutes>=720 && eventId==0) StartEvent();
            if(!eventActive && snapshot.TotalMinutes>=1080 && eventId==1) StartEvent();
            if(eventActive && Time.unscaledTime>=eventDeadline) ResolveEvent(match);
            int alive=0;foreach(var p in match.Players)if(!p.IsEliminated)alive++;
            if(snapshot.IsFinished || (match.Players.Count>1&&alive<=1)) Finish(match);
            if(broadcastTick>=.25f){broadcastTick=0;Broadcast(string.Empty);}
        }

        public void Submit(P2PCompetitionAction action,string value)
        { if(net==null||!net.IsListening)return;byte[] b=P2PCompetitionCodec.EncodeAction(SteamRuntimeBootstrap.LocalSteamId,action,value);if(net.IsServer)Process(net.LocalClientId,b);else{using var w=Writer(b);net.CustomMessagingManager.SendNamedMessage(ActionMessage,NetworkManager.ServerClientId,w,NetworkDelivery.ReliableSequenced);} }

        private void Process(ulong sender,byte[] bytes)
        {
            if(finished||!P2PCompetitionCodec.TryDecodeAction(bytes,out ulong claimed,out var action,out string value))return;
            ulong actual=SteamRuntimeBootstrap.LocalSteamId;if(sender!=net.LocalClientId&&(!P2PNetworkSessionManager.Instance.TryGetSteamId(sender,out actual)||actual!=claimed))return;
            var match=trading.HostMatch;if(match==null||!match.TryGetPlayer(actual,out var p)||p.IsEliminated)return;
            string message=string.Empty;
            if(action==P2PCompetitionAction.ChooseEvent){if(eventActive&&int.TryParse(value,out int c)&&P2PChoiceRules.IsValid(c)&&!choices.ContainsKey(actual)){choices[actual]=c;message=$"{p.DisplayName} 선택 완료";}}
            else if(action==P2PCompetitionAction.BuyItem) message=Buy(p,value);
            else if(action==P2PCompetitionAction.UseItem) message=Use(p,value);
            Broadcast(message);trading.BroadcastCurrentState();
        }

        private static string Buy(P2PPlayerRuntimeState p,string id)
        { if(!TryItem(id,out int cost,out _,out _))return "배달음식과 액티브 기어는 사용할 수 없어용";if(p.CashBalance<cost)return "잔액이 부족해용";p.ChangeCash(-cost);p.SetInventoryAmount(id,Count(p,id)+1);return $"{id} 구매 완료"; }
        private static string Use(P2PPlayerRuntimeState p,string id)
        { if(!TryItem(id,out _,out double health,out double mental)||Count(p,id)<=0)return "사용할 수 없는 아이템이에용";p.SetInventoryAmount(id,Count(p,id)-1);p.ChangeHealth(health);p.ChangeMental(mental);return $"{id} 사용 완료"; }
        private static bool TryItem(string id,out int cost,out double health,out double mental)
        { cost=0;health=mental=0;switch(id){case "energy_drink":cost=500;health=30;return true;case "dessert":cost=600;mental=20;return true;case "sedative":cost=1000;mental=40;return true;case "supplement":cost=900;health=50;return true;default:return false;} }
        private static int Count(P2PPlayerRuntimeState p,string id)=>p.Inventory.TryGetValue(id,out int n)?n:0;

        private void StartEvent(){eventActive=true;eventId++;choices.Clear();eventDeadline=Time.unscaledTime+15f;trading.HostMatch.IsChoiceEventActive=true;market.SetPausedByHost(true);Broadcast("돌발 이벤트: 15초 안에 선택해용");}
        private void ResolveEvent(P2PLocalMatch match)
        { foreach(var p in match.Players){if(p.IsEliminated)continue;if(!choices.TryGetValue(p.PlayerId,out int c))c=P2PChoiceRules.GetTimeoutChoice(p.PlayerId,eventId);if(c==0)p.ChangeHealth(15);else if(c==1){p.ChangeMental(20);p.ChangeHealth(-5);}Evaluate(p,match);}eventActive=false;match.IsChoiceEventActive=false;market.SetPausedByHost(false);Broadcast("이벤트 결과가 적용됐어용");trading.BroadcastCurrentState(); }
        private static void Evaluate(P2PPlayerRuntimeState p,P2PLocalMatch match)
        { P2PEliminationReason reason=p.Mental<=0?P2PEliminationReason.MentalDepleted:p.TotalEquity<=0?P2PEliminationReason.Bankruptcy:P2PEliminationReason.None;if(reason==P2PEliminationReason.None)return;if(p.Position.IsOpen)P2PTradeCalculator.ClosePosition(p,match.Rules,match.MarketPrice);p.TryEliminate(reason,(long)Time.unscaledTime); }
        private void Finish(P2PLocalMatch match){finished=true;eventActive=false;market.SetPausedByHost(true);match.IsChoiceEventActive=false;match.Finish();trading.BroadcastCurrentState();Broadcast("경기 종료 · 최종 순위가 확정됐어용");}

        private void Broadcast(string message)
        { var match=trading.HostMatch;if(match==null)return;var list=new List<P2PCompetitionPlayerSnapshot>();foreach(var p in match.Players)list.Add(new P2PCompetitionPlayerSnapshot(p.PlayerId,p.Health,p.Mental,p.IsEliminated,p.EliminationReason,Count(p,"energy_drink"),Count(p,"dessert"),Count(p,"sedative"),Count(p,"supplement")));var x=new P2PCompetitionSnapshot{Players=list,EventActive=eventActive,EventId=eventId,EventTitle=eventActive?"긴급 시장 스트레스":"",EventSecondsLeft=eventActive?Math.Max(0,eventDeadline-Time.unscaledTime):0,Finished=finished,LastMessage=string.IsNullOrEmpty(message)?Current.LastMessage:message};byte[] b=P2PCompetitionCodec.EncodeState(x);Apply(b);using var w=Writer(b);net.CustomMessagingManager.SendNamedMessage(StateMessage,net.ConnectedClientsIds,w,NetworkDelivery.ReliableSequenced); }
        private void ReceiveAction(ulong sender,FastBufferReader reader){if(!net.IsServer)return;reader.ReadValueSafe(out byte[] b);Process(sender,b);}
        private void ReceiveState(ulong sender,FastBufferReader reader){if(net.IsServer||sender!=NetworkManager.ServerClientId)return;reader.ReadValueSafe(out byte[] b);Apply(b);}
        private void Apply(byte[] b){if(!P2PCompetitionCodec.TryDecodeState(b,SteamRuntimeBootstrap.LocalSteamId,out var x))return;Current=x;StateChanged?.Invoke();}
        private void EnsureRegistered(){if(registered)return;registered=true;trading=GetComponent<NetworkTradingAuthority>();market=GetComponent<NetworkMarketAuthority>();net.CustomMessagingManager.RegisterNamedMessageHandler(ActionMessage,ReceiveAction);net.CustomMessagingManager.RegisterNamedMessageHandler(StateMessage,ReceiveState);}
        private static FastBufferWriter Writer(byte[] b){var w=new FastBufferWriter(sizeof(int)+b.Length,Allocator.Temp);w.WriteValueSafe(b);return w;}
        private void OnDestroy(){if(!registered||net?.CustomMessagingManager==null)return;net.CustomMessagingManager.UnregisterNamedMessageHandler(ActionMessage);net.CustomMessagingManager.UnregisterNamedMessageHandler(StateMessage);}
    }
}
