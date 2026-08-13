using System;
using System.Collections.Generic;
using FXOverdose.P2P.Connection;
using FXOverdose.P2P.Lobby;
using Netcode.Transports;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FXOverdose.P2P.Infrastructure
{
    /// <summary>Steam 로비를 NGO 호스트·클라이언트 세션으로 전환합니다.</summary>
    [DefaultExecutionOrder(-8000)]
    public sealed class P2PNetworkSessionManager : MonoBehaviour
    {
        private readonly Dictionary<ulong, ulong> networkToSteam = new Dictionary<ulong, ulong>();
        private readonly HashSet<ulong> connectedSteamIds = new HashSet<ulong>();
        private NetworkManager networkManager;
        private SteamNetworkingSocketsTransport transport;
        private NetworkMarketAuthority marketAuthority;
        private NetworkTradingAuthority tradingAuthority;
        private NetworkCompetitionAuthority competitionAuthority;
        private string attemptedNonce = string.Empty;
        private bool gameplaySceneRequested;

        public static P2PNetworkSessionManager Instance { get; private set; }
        public bool IsRunning => networkManager != null && networkManager.IsListening;
        public event Action<ulong, ulong> ClientMapped;
        public event Action<ulong> ClientDisconnected;
        public event Action<ulong> SteamClientDisconnected;
        public event Action<string> ConnectionFailed;
        public NetworkMarketAuthority MarketAuthority => marketAuthority;
        public NetworkTradingAuthority TradingAuthority => tradingAuthority;
        public NetworkCompetitionAuthority CompetitionAuthority => competitionAuthority;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateInstance()
        {
            if (Instance != null) return;
            var target = new GameObject(nameof(P2PNetworkSessionManager));
            DontDestroyOnLoad(target);
            target.AddComponent<P2PNetworkSessionManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (SteamLobbyManager.Instance != null)
            {
                SteamLobbyManager.Instance.LobbyChanged += OnLobbyChanged;
                OnLobbyChanged(SteamLobbyManager.Instance.CurrentLobby);
            }
        }

        private void OnLobbyChanged(SteamLobbySnapshot lobby)
        {
            if (lobby == null || !lobby.MatchStarted || string.IsNullOrEmpty(lobby.ConnectionNonce) ||
                lobby.ConnectionNonce == attemptedNonce || IsRunning) return;
            attemptedNonce = lobby.ConnectionNonce;
            StartFromCurrentLobby();
        }

        public bool StartFromCurrentLobby()
        {
            SteamLobbySnapshot lobby = SteamLobbyManager.Instance?.CurrentLobby;
            if (!SteamRuntimeBootstrap.CanUseP2P || lobby == null || !lobby.MatchStarted || string.IsNullOrEmpty(lobby.ConnectionNonce))
                return Fail("시작 가능한 Steam 로비가 없습니다.");

            EnsureNetworkManager();
            if (networkManager.IsListening) return false;

            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.NetworkConfig.ConnectionData = P2PConnectionPayloadCodec.Encode(new P2PConnectionPayload(
                lobby.LobbyId, SteamRuntimeBootstrap.LocalSteamId, Application.version, lobby.ConnectionNonce));
            networkManager.ConnectionApprovalCallback = ApproveConnection;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnTransportFailure += OnTransportFailure;

            bool isHost = lobby.OwnerSteamId == SteamRuntimeBootstrap.LocalSteamId;
            if (!isHost) transport.ConnectToSteamID = lobby.OwnerSteamId;
            bool started = isHost ? networkManager.StartHost() : networkManager.StartClient();
            if (!started) CleanupCallbacks();
            Debug.Log(started ? $"[P2P Network] {(isHost ? "HOST" : "CLIENT")} 시작" : "[P2P Network] 시작 실패");
            if (started && isHost) Invoke(nameof(TryLoadGameplayScene), 0.5f);
            return started;
        }

        private void OnClientConnected(ulong _) => TryLoadGameplayScene();

        private void TryLoadGameplayScene()
        {
            if (gameplaySceneRequested || networkManager == null || !networkManager.IsServer || !networkManager.IsListening) return;
            SteamLobbySnapshot lobby = SteamLobbyManager.Instance?.CurrentLobby;
            if (lobby == null || networkManager.ConnectedClientsIds.Count < lobby.Members.Count) return;
            gameplaySceneRequested = true;
            networkManager.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
        }

        public bool TryGetSteamId(ulong networkClientId, out ulong steamId) => networkToSteam.TryGetValue(networkClientId, out steamId);

        public void ShutdownSession()
        {
            if (networkManager != null && networkManager.IsListening) networkManager.Shutdown();
            CleanupCallbacks();
            networkToSteam.Clear();
            connectedSteamIds.Clear();
            gameplaySceneRequested = false;
            marketAuthority?.ResetForSession();
            tradingAuthority?.ResetForSession();
            competitionAuthority?.ResetForSession();
        }

        private void EnsureNetworkManager()
        {
            if (networkManager != null) return;
            transport = gameObject.AddComponent<SteamNetworkingSocketsTransport>();
            networkManager = gameObject.AddComponent<NetworkManager>();
            marketAuthority = gameObject.AddComponent<NetworkMarketAuthority>();
            tradingAuthority = gameObject.AddComponent<NetworkTradingAuthority>();
            competitionAuthority = gameObject.AddComponent<NetworkCompetitionAuthority>();
            networkManager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                ConnectionApproval = true,
                EnableSceneManagement = true,
                ForceSamePrefabs = true
            };
        }

        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            SteamLobbySnapshot lobby = SteamLobbyManager.Instance?.CurrentLobby;
            if (lobby == null)
            {
                Reject(response, P2PConnectionRejectReason.WrongLobby);
                return;
            }

            var memberIds = new HashSet<ulong>();
            for (int i = 0; i < lobby.Members.Count; i++) memberIds.Add(lobby.Members[i].SteamId);
            P2PConnectionApprovalResult result = P2PConnectionApprovalValidator.Validate(request.Payload,
                lobby.LobbyId, Application.version, lobby.ConnectionNonce, lobby.MatchStarted,
                memberIds, connectedSteamIds, lobby.Settings.MaximumPlayers);
            if (!result.Approved)
            {
                Reject(response, result.Reason);
                return;
            }

            P2PConnectionPayloadCodec.TryDecode(request.Payload, out var payload);
            response.Approved = true;
            response.CreatePlayerObject = false;
            response.Pending = false;
            networkToSteam[request.ClientNetworkId] = payload.SteamId;
            connectedSteamIds.Add(payload.SteamId);
            ClientMapped?.Invoke(request.ClientNetworkId, payload.SteamId);
        }

        private static void Reject(NetworkManager.ConnectionApprovalResponse response, P2PConnectionRejectReason reason)
        {
            response.Approved = false; response.CreatePlayerObject = false; response.Pending = false; response.Reason = reason.ToString();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            ulong disconnectedSteamId=0;
            if (networkToSteam.TryGetValue(clientId, out ulong steamId))
            {
                disconnectedSteamId=steamId;
                networkToSteam.Remove(clientId); connectedSteamIds.Remove(steamId);
            }
            Debug.LogWarning($"[P2P Network] 연결 종료 · NGO client={clientId} · Steam={disconnectedSteamId} · reason={networkManager?.DisconnectReason}");
            if (disconnectedSteamId != 0) SteamClientDisconnected?.Invoke(disconnectedSteamId);
            if (networkManager != null && networkManager.IsClient && !networkManager.IsServer && clientId == networkManager.LocalClientId)
                ConnectionFailed?.Invoke(networkManager.DisconnectReason);
            ClientDisconnected?.Invoke(clientId);
        }

        private void OnTransportFailure() => ConnectionFailed?.Invoke("Steam Transport failure");
        private bool Fail(string reason) { ConnectionFailed?.Invoke(reason); Debug.LogWarning($"[P2P Network] {reason}"); return false; }

        private void CleanupCallbacks()
        {
            if (networkManager == null) return;
            networkManager.ConnectionApprovalCallback = null;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnTransportFailure -= OnTransportFailure;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            if (SteamLobbyManager.Instance != null) SteamLobbyManager.Instance.LobbyChanged -= OnLobbyChanged;
            ShutdownSession();
            Instance = null;
        }
    }
}
