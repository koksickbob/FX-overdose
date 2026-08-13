using System;
using FXOverdose.P2P.Market;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace FXOverdose.P2P.Infrastructure
{
    /// <summary>호스트 시장 스냅샷을 모든 NGO 클라이언트에 배포합니다.</summary>
    public sealed class NetworkMarketAuthority : MonoBehaviour
    {
        private const string SnapshotMessage = "FXO.P2P.MarketSnapshot.v1";
        private const string RequestMessage = "FXO.P2P.MarketSnapshotRequest.v1";
        // 기존 차트의 분당 5틱 움직임을 클라이언트에서도 빠뜨리지 않도록 10Hz로 복제합니다.
        private const double SnapshotInterval = 0.1;
        private NetworkManager networkManager;
        private P2PMarketSimulationEngine hostEngine;
        private P2PMarketReplica replica = new();
        private double sendAccumulator;
        private bool registered;

        public P2PMarketSnapshot CurrentSnapshot => networkManager != null && networkManager.IsServer && hostEngine != null
            ? hostEngine.Snapshot : replica.Snapshot;
        public double AuthoritativePrice => CurrentSnapshot.Price;
        public event Action<P2PMarketSnapshot> SnapshotChanged;

        public void ResetForSession()
        {
            hostEngine = null; replica = new P2PMarketReplica(); sendAccumulator = 0;
        }

        private void Update()
        {
            networkManager ??= NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening) return;
            EnsureRegistered();
            if (!networkManager.IsServer) return;
            EnsureHostEngine();
            hostEngine.Advance(Time.unscaledDeltaTime);
            sendAccumulator += Time.unscaledDeltaTime;
            if (sendAccumulator >= SnapshotInterval)
            {
                sendAccumulator = 0;
                Broadcast(hostEngine.Snapshot);
                SnapshotChanged?.Invoke(hostEngine.Snapshot);
            }
        }

        public void SetPausedByHost(bool paused)
        {
            if (networkManager == null || !networkManager.IsServer || hostEngine == null) return;
            hostEngine.SetPaused(paused);
            Broadcast(hostEngine.Snapshot);
        }

        public void OverrideMarketTrendByHost(double percent, int durationTicks)
        {
            if (networkManager == null || !networkManager.IsServer || hostEngine == null) return;
            hostEngine.OverrideMarketTrend(percent,durationTicks);
        }

        private void EnsureRegistered()
        {
            if (registered) return;
            registered = true;
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(SnapshotMessage, ReceiveSnapshot);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(RequestMessage, ReceiveSnapshotRequest);
            if (!networkManager.IsServer) SendSnapshotRequest();
        }

        private void EnsureHostEngine()
        {
            if (hostEngine != null) return;
            string nonce = SteamLobbyManager.Instance?.CurrentLobby?.ConnectionNonce ?? string.Empty;
            int seed = StableSeed(nonce);
            hostEngine = new P2PMarketSimulationEngine(seed);
            hostEngine.SetPaused(false);
            Broadcast(hostEngine.Snapshot);
            Debug.Log($"[P2P Market] HOST 권위 시장 시작 | Seed {seed} | 09:00 | {hostEngine.Snapshot.Price:F1}");
        }

        private void Broadcast(P2PMarketSnapshot snapshot)
        {
            if (networkManager == null || !networkManager.IsServer || networkManager.ConnectedClientsIds.Count == 0) return;
            byte[] bytes = P2PMarketSnapshotCodec.Encode(snapshot);
            using var writer = new FastBufferWriter(sizeof(int) + bytes.Length, Allocator.Temp);
            writer.WriteValueSafe(bytes);
            networkManager.CustomMessagingManager.SendNamedMessage(SnapshotMessage,
                networkManager.ConnectedClientsIds, writer, NetworkDelivery.UnreliableSequenced);
        }

        private void ReceiveSnapshot(ulong senderClientId, FastBufferReader reader)
        {
            if (networkManager == null || networkManager.IsServer || senderClientId != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out byte[] bytes);
            if (!P2PMarketSnapshotCodec.TryDecode(bytes, out var snapshot) || !replica.TryApply(snapshot)) return;
            SnapshotChanged?.Invoke(snapshot);
        }

        private void ReceiveSnapshotRequest(ulong senderClientId, FastBufferReader reader)
        {
            if (networkManager == null || !networkManager.IsServer || hostEngine == null) return;
            byte[] bytes = P2PMarketSnapshotCodec.Encode(hostEngine.Snapshot);
            using var writer = new FastBufferWriter(sizeof(int) + bytes.Length, Allocator.Temp);
            writer.WriteValueSafe(bytes);
            networkManager.CustomMessagingManager.SendNamedMessage(SnapshotMessage, senderClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        private void SendSnapshotRequest()
        {
            using var writer = new FastBufferWriter(1, Allocator.Temp);
            networkManager.CustomMessagingManager.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        private static int StableSeed(string value)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < value.Length; i++) hash = hash * 31 + value[i];
                return hash == 0 ? 1 : hash;
            }
        }

        private void OnDestroy()
        {
            if (!registered || networkManager == null || networkManager.CustomMessagingManager == null) return;
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(SnapshotMessage);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(RequestMessage);
        }
    }
}
