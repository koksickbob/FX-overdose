using System;
using System.Collections.Generic;
using System.Collections;
using FXOverdose.P2P.Connection;
using FXOverdose.P2P.Lobby;
using Netcode.Transports;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Steamworks;

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
        private P2PNetworkDiagnostics diagnostics;
        private string attemptedNonce = string.Empty;
        private bool gameplaySceneRequested;
        private bool intentionalShutdown,reconnecting;
        private ulong sessionHostSteamId;

        public static P2PNetworkSessionManager Instance { get; private set; }
        public bool IsRunning => networkManager != null && networkManager.IsListening;
        /// <summary>세션 종료·앱 종료 중에는 NGO가 CustomMessagingManager를 이미 해제하므로 모든 송신을 막습니다.</summary>
        public static bool CanSend(NetworkManager manager) =>
            manager != null && manager.IsListening && !manager.ShutdownInProgress && manager.CustomMessagingManager != null;
        public event Action<ulong, ulong> ClientMapped;
        public event Action<ulong> ClientDisconnected;
        public event Action<ulong> SteamClientDisconnected;
        public event Action<string> ConnectionFailed;
        public NetworkMarketAuthority MarketAuthority => marketAuthority;
        public NetworkTradingAuthority TradingAuthority => tradingAuthority;
        public NetworkCompetitionAuthority CompetitionAuthority => competitionAuthority;
        public string MatchId { get; private set; } = string.Empty;
        public P2PNetworkDiagnostics Diagnostics => diagnostics;

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
            intentionalShutdown=false;

            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.NetworkConfig.ConnectionData = P2PConnectionPayloadCodec.Encode(new P2PConnectionPayload(
                lobby.LobbyId, SteamRuntimeBootstrap.LocalSteamId, Application.version, lobby.ConnectionNonce));
            networkManager.ConnectionApprovalCallback = ApproveConnection;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnTransportFailure += OnTransportFailure;

            if(sessionHostSteamId==0)sessionHostSteamId=lobby.OwnerSteamId;
            MatchId=$"{lobby.LobbyId:X}-{lobby.ConnectionNonce.Substring(0,Math.Min(8,lobby.ConnectionNonce.Length))}";
            bool isHost = sessionHostSteamId == SteamRuntimeBootstrap.LocalSteamId;
            if (!isHost) transport.ConnectToSteamID = sessionHostSteamId;
            bool started = isHost ? networkManager.StartHost() : networkManager.StartClient();
            if (!started) CleanupCallbacks();
            Debug.Log(started ? $"[P2P Network][{MatchId}] {(isHost ? "HOST" : "CLIENT")} 시작" : $"[P2P Network][{MatchId}] 시작 실패");
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
            intentionalShutdown=true;
            StopAllCoroutines();reconnecting=false;
            if (networkManager != null && networkManager.IsListening) networkManager.Shutdown();
            CleanupCallbacks();
            networkToSteam.Clear();
            connectedSteamIds.Clear();
            gameplaySceneRequested = false;
            sessionHostSteamId=0;MatchId=string.Empty;
            marketAuthority?.ResetForSession();
            tradingAuthority?.ResetForSession();
            competitionAuthority?.ResetForSession();
            diagnostics?.ResetCounters();
        }

        private void EnsureNetworkManager()
        {
            if (networkManager != null) return;
            transport = gameObject.AddComponent<SteamNetworkingSocketsTransport>();
            // 일시적인 Steam 릴레이 지연을 즉시 연결 종료로 판단하지 않도록 연결 유지 시간을 60초로 둡니다.
            transport.options = new[]
            {
                new SteamNetworkingConfigValue_t
                {
                    m_eValue=ESteamNetworkingConfigValue.k_ESteamNetworkingConfig_TimeoutConnected,
                    m_eDataType=ESteamNetworkingConfigDataType.k_ESteamNetworkingConfig_Int32,
                    m_val=new SteamNetworkingConfigValue_t.OptionValue{m_int32=60000}
                }
            };
            networkManager = gameObject.AddComponent<NetworkManager>();
            marketAuthority = gameObject.AddComponent<NetworkMarketAuthority>();
            tradingAuthority = gameObject.AddComponent<NetworkTradingAuthority>();
            competitionAuthority = gameObject.AddComponent<NetworkCompetitionAuthority>();
            diagnostics = gameObject.AddComponent<P2PNetworkDiagnostics>();
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
            // 의도적 종료(타이틀 복귀·앱 종료) 중에는 NGO 내부가 이미 해체된 상태라 재접속·기권 처리를 하지 않습니다.
            if (intentionalShutdown) return;
            Debug.LogWarning($"[P2P Network][{MatchId}] 연결 종료 · NGO client={clientId} · Steam={disconnectedSteamId} · reason={networkManager?.DisconnectReason}");
            if (disconnectedSteamId != 0) SteamClientDisconnected?.Invoke(disconnectedSteamId);
            if (networkManager != null && networkManager.IsClient && !networkManager.IsServer && clientId == networkManager.LocalClientId)
            {
                if(!intentionalShutdown&&!reconnecting)StartCoroutine(TryReconnectClient());
            }
            ClientDisconnected?.Invoke(clientId);
        }

        private IEnumerator TryReconnectClient()
        {
            reconnecting=true;
            string lastReason=string.IsNullOrWhiteSpace(networkManager?.DisconnectReason)?"호스트 연결 종료":networkManager.DisconnectReason;
            for(int attempt=1;attempt<=3;attempt++)
            {
                yield return new WaitForSecondsRealtime(2f);
                if(intentionalShutdown){reconnecting=false;yield break;}
                CleanupCallbacks();
                if(StartFromCurrentLobby())
                {
                    Debug.Log($"[P2P Reconnect][{MatchId}] 자동 재접속 시도 {attempt}/3");
                    float deadline=Time.unscaledTime+3f;
                    while(Time.unscaledTime<deadline)
                    {
                        if(networkManager!=null&&networkManager.IsConnectedClient){reconnecting=false;yield break;}
                        yield return null;
                    }
                    if(networkManager!=null&&networkManager.IsListening)networkManager.Shutdown();
                }
            }
            reconnecting=false;
            ConnectionFailed?.Invoke($"{lastReason} · 재접속 실패로 경기가 무효 처리됐어용");
        }

        private void OnTransportFailure()
        {
            if(networkManager!=null&&networkManager.IsClient&&!networkManager.IsServer&&!intentionalShutdown&&!reconnecting)
                StartCoroutine(TryReconnectClient());
            else if(!intentionalShutdown)ConnectionFailed?.Invoke("Steam Transport failure");
        }
        private bool Fail(string reason) { ConnectionFailed?.Invoke(reason); Debug.LogWarning($"[P2P Network] {reason}"); return false; }

        private void CleanupCallbacks()
        {
            if (networkManager == null) return;
            networkManager.ConnectionApprovalCallback = null;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnTransportFailure -= OnTransportFailure;
        }

        // NetworkManager와 같은 오브젝트에 있고 실행 순서가 -8000이라 NGO 자체 종료보다 먼저 정리됩니다.
        private void OnApplicationQuit() => ShutdownSession();

        private void OnDestroy()
        {
            if (Instance != this) return;
            if (SteamLobbyManager.Instance != null) SteamLobbyManager.Instance.LobbyChanged -= OnLobbyChanged;
            ShutdownSession();
            Instance = null;
        }
    }
}
