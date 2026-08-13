using System;
using System.Collections.Generic;
using FXOverdose.P2P.Core;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace FXOverdose.P2P.Infrastructure
{
    /// <summary>클라이언트 주문을 호스트 가격으로 검증·체결하고 전체 순위를 배포합니다.</summary>
    public sealed class NetworkTradingAuthority : MonoBehaviour
    {
        private const string RequestMessage = "FXO.P2P.TradeRequest.v1";
        private const string StateMessage = "FXO.P2P.TradeState.v1";
        private NetworkManager networkManager;
        private P2PLocalMatch hostMatch;
        private NetworkMarketAuthority market;
        private bool registered;
        private uint nextRequestId = 1;
        private double lastMarketPrice;
        private float stateBroadcastTimer;
        private ulong stateSequence,lastAppliedSequence;
        private readonly P2PRequestSecurityGuard requestGuard=new();

        public IReadOnlyList<P2PPlayerTradeSnapshot> Players { get; private set; } = Array.Empty<P2PPlayerTradeSnapshot>();
        public P2PTradeResult LastResult { get; private set; }
        public event Action StateChanged;
        public P2PLocalMatch HostMatch => hostMatch;

        public void ResetForSession()
        {
            hostMatch=null; Players=Array.Empty<P2PPlayerTradeSnapshot>(); LastResult=default;
            nextRequestId=1; lastMarketPrice=0;stateSequence=lastAppliedSequence=0;requestGuard.Reset();
        }

        private void Update()
        {
            networkManager ??= NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening) return;
            EnsureRegistered();
            if (!networkManager.IsServer) return;
            EnsureHostMatch();
            double price = market != null ? market.AuthoritativePrice : 0;
            if (price > 0 && hostMatch != null && hostMatch.Phase == P2PMatchPhase.Playing && Math.Abs(price - lastMarketPrice) > 0.000001)
            {
                lastMarketPrice = price;
                hostMatch.UpdateMarketPrice(price, (long)(market.CurrentSnapshot.Sequence));
                stateBroadcastTimer += Time.unscaledDeltaTime;
                if(stateBroadcastTimer>=.1f){stateBroadcastTimer=0;Broadcast(new P2PTradeResult(0,P2PTradeRejectReason.None,price),false);}
            }
        }

        public void Submit(P2PTradeAction action, int leverage, double marginRatio)
        {
            if (networkManager == null || !networkManager.IsListening) return;
            uint requestId=nextRequestId++;if(nextRequestId==0)nextRequestId=1;
            var request = new P2PTradeRequest(requestId, action, leverage, marginRatio);
            byte[] bytes = P2PNetworkTradingCodec.EncodeRequest(SteamRuntimeBootstrap.LocalSteamId, request);
            P2PNetworkSessionManager.Instance?.Diagnostics?.RecordSent(bytes.Length);
            if (networkManager.IsServer) ProcessRequest(networkManager.LocalClientId, bytes);
            else { using var writer = Writer(bytes); networkManager.CustomMessagingManager.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced); }
        }

        private void EnsureRegistered()
        {
            if (registered) return; registered = true;
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(RequestMessage, ReceiveRequest);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(StateMessage, ReceiveState);
            market = GetComponent<NetworkMarketAuthority>();
        }

        private void EnsureHostMatch()
        {
            if (hostMatch != null || market == null || market.AuthoritativePrice <= 0) return;
            var lobby = SteamLobbyManager.Instance.CurrentLobby;
            hostMatch = new P2PLocalMatch(new P2PMatchRules(lobby.Settings.MaximumPlayers, lobby.Settings.MaximumLeverage, lobby.Settings.MaximumMarginRatio));
            foreach (var member in lobby.Members) hostMatch.AddPlayer(member.SteamId, member.PersonaName);
            hostMatch.Start(market.AuthoritativePrice); lastMarketPrice = market.AuthoritativePrice;
            Broadcast(new P2PTradeResult(0, P2PTradeRejectReason.None, lastMarketPrice));
        }

        private void ReceiveRequest(ulong sender, FastBufferReader reader)
        {
            if (!networkManager.IsServer) return; reader.ReadValueSafe(out byte[] bytes); ProcessRequest(sender, bytes);
            P2PNetworkSessionManager.Instance?.Diagnostics?.RecordReceived(bytes?.Length??0);
        }

        private void ProcessRequest(ulong sender, byte[] bytes)
        {
            EnsureHostMatch();
            ulong actualSteamId = SteamRuntimeBootstrap.LocalSteamId;
            if(sender!=networkManager.LocalClientId&&!P2PNetworkSessionManager.Instance.TryGetSteamId(sender,out actualSteamId)){BlockClient(sender,"Steam ID mapping missing");return;}
            if(hostMatch==null)return;
            P2PRequestGuardResult guardResult=requestGuard.TryConsume(actualSteamId,Time.unscaledTime);
            if(guardResult!=P2PRequestGuardResult.Allowed){if(guardResult==P2PRequestGuardResult.Blocked)BlockClient(sender,"trade rate limit");return;}
            if(!P2PNetworkTradingCodec.TryDecodeRequest(bytes,out ulong claimedSteamId,out var request))
            {if(requestGuard.RecordViolation(actualSteamId,Time.unscaledTime)==P2PRequestGuardResult.Blocked)BlockClient(sender,"malformed trade packets");return;}
            // 원격 요청은 NGO 연결 승인 때 매핑된 Steam ID와 Payload의 Steam ID가 반드시 같아야 합니다.
            if(actualSteamId!=claimedSteamId)
            {if(requestGuard.RecordViolation(actualSteamId,Time.unscaledTime)==P2PRequestGuardResult.Blocked)BlockClient(sender,"spoofed Steam ID");return;}
            hostMatch.UpdateMarketPrice(market.AuthoritativePrice, (long)market.CurrentSnapshot.Sequence);
            P2PTradeResult result = hostMatch.SubmitTrade(actualSteamId, request);
            if(result.RejectReason==P2PTradeRejectReason.InvalidLeverage||result.RejectReason==P2PTradeRejectReason.InvalidMargin)
                if(requestGuard.RecordViolation(actualSteamId,Time.unscaledTime)==P2PRequestGuardResult.Blocked){BlockClient(sender,"invalid trade requests");return;}
            Debug.Log($"[P2P Trade][{P2PNetworkSessionManager.Instance?.MatchId}] {actualSteamId} req={request.RequestId} {request.Action} x{request.Leverage} margin {request.MarginRatio:P0} => {result.RejectReason} @ {result.FillPrice:F1}");
            Broadcast(result,true);
        }

        private void BlockClient(ulong sender,string reason)
        {
            Debug.LogWarning($"[P2P Security][{P2PNetworkSessionManager.Instance?.MatchId}] client={sender} 차단 · {reason}");
            if(sender!=networkManager.LocalClientId)networkManager.DisconnectClient(sender);
        }

        public void BroadcastCurrentState() => Broadcast(new P2PTradeResult(0, P2PTradeRejectReason.None, lastMarketPrice),true);

        private void Broadcast(P2PTradeResult result,bool reliable=true)
        {
            if (hostMatch == null) return;
            byte[] bytes = P2PNetworkTradingCodec.EncodeState(++stateSequence,result, hostMatch.GetLeaderboard());
            P2PNetworkSessionManager.Instance?.Diagnostics?.RecordSent(bytes.Length*networkManager.ConnectedClientsIds.Count);
            ApplyState(bytes);
            using var writer = Writer(bytes);
            networkManager.CustomMessagingManager.SendNamedMessage(StateMessage,networkManager.ConnectedClientsIds,writer,reliable?NetworkDelivery.ReliableSequenced:NetworkDelivery.UnreliableSequenced);
        }

        private void ReceiveState(ulong sender, FastBufferReader reader)
        {
            if (networkManager.IsServer || sender != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out byte[] bytes); ApplyState(bytes);
            P2PNetworkSessionManager.Instance?.Diagnostics?.RecordReceived(bytes?.Length??0);
        }

        private void ApplyState(byte[] bytes)
        {
            if (!P2PNetworkTradingCodec.TryDecodeState(bytes,out ulong sequence, out var result, out var players)||!IsSequenceNewer(sequence,lastAppliedSequence)) return;
            lastAppliedSequence=sequence;
            LastResult = result; Players = players; StateChanged?.Invoke();
        }

        private static bool IsSequenceNewer(ulong candidate,ulong previous)=>candidate!=previous&&unchecked((long)(candidate-previous))>0;

        private static FastBufferWriter Writer(byte[] bytes)
        {
            var writer = new FastBufferWriter(sizeof(int) + bytes.Length, Allocator.Temp); writer.WriteValueSafe(bytes); return writer;
        }

        private void OnDestroy()
        {
            if (!registered || networkManager?.CustomMessagingManager == null) return;
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(RequestMessage);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(StateMessage);
        }
    }
}
