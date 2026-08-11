using System;
using FXOverdose.P2P.Steam;
using UnityEngine;

namespace FXOverdose.P2P.Infrastructure
{
    /// <summary>Steam 서비스의 Unity 생명주기와 콜백 펌프를 담당합니다.</summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class SteamRuntimeBootstrap : MonoBehaviour
    {
        // Valve의 Spacewar 테스트 App ID입니다. 출시 전 실제 게임 App ID로 교체해야 합니다.
        private const uint DevelopmentAppId = 480;
        private SteamRuntimeService runtimeService;

        public static SteamRuntimeBootstrap Instance { get; private set; }
        public static SteamRuntimeStatus CurrentStatus => Instance != null
            ? Instance.runtimeService.Status
            : new SteamRuntimeStatus(SteamRuntimeState.NotStarted);
        public static bool IsInitialized => CurrentStatus.State == SteamRuntimeState.Ready;
        public static bool CanUseP2P => CurrentStatus.CanUseP2P;
        public static ulong LocalSteamId => CurrentStatus.SteamId;
        public static string LocalPersonaName => CurrentStatus.PersonaName;

        /// <summary>타이틀 UI와 로비 계층이 초기화 결과를 구독하는 이벤트입니다.</summary>
        public static event Action<SteamRuntimeStatus> StatusChanged;

        public ISteamRuntimeService Service => runtimeService;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            StatusChanged = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateRuntimeInstance()
        {
            if (Instance != null)
            {
                return;
            }

            var bootstrapObject = new GameObject(nameof(SteamRuntimeBootstrap));
            DontDestroyOnLoad(bootstrapObject);
            bootstrapObject.AddComponent<SteamRuntimeBootstrap>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            runtimeService = new SteamRuntimeService(new SteamworksRuntimeBackend());
            runtimeService.StatusChanged += HandleStatusChanged;

            // Editor에서 RestartAppIfNecessary를 호출하면 Unity 자체가 종료될 수 있습니다.
            runtimeService.Initialize(DevelopmentAppId, !Application.isEditor);
            if (CurrentStatus.Error == SteamInitializationError.RestartRequired)
            {
                Application.Quit();
            }
        }

        private void Update() => runtimeService?.PumpCallbacks();

        private void OnApplicationQuit() => ShutdownSteam();

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            ShutdownSteam();
            Instance = null;
        }

        private void HandleStatusChanged(SteamRuntimeStatus status)
        {
            StatusChanged?.Invoke(status);

            if (status.State == SteamRuntimeState.Ready)
            {
                Debug.Log(
                    "[Steam Runtime] READY\n" +
                    $"App ID: {status.AppId}\n" +
                    $"Steam ID: {status.SteamId}\n" +
                    $"Persona: {status.PersonaName}\n" +
                    "Steam Networking Sockets: READY");
            }
            else if (status.State == SteamRuntimeState.Failed)
            {
                // 실패해도 게임을 종료하지 않으며 STORY 등 오프라인 모드는 계속 사용할 수 있습니다.
                Debug.LogWarning($"[Steam Runtime] P2P 비활성화 ({status.Error}): {status.Message}");
            }
            else if (status.State == SteamRuntimeState.Shutdown)
            {
                Debug.Log("[Steam Runtime] SHUTDOWN");
            }
        }

        private void ShutdownSteam()
        {
            if (runtimeService == null)
            {
                return;
            }

            runtimeService.Shutdown();
            runtimeService.StatusChanged -= HandleStatusChanged;
        }
    }
}
