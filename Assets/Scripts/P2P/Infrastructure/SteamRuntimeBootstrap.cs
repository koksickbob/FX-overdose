using System;
using Steamworks;
using UnityEngine;

namespace FXOverdose.P2P.Infrastructure
{
    /// <summary>
    /// Steam PC 빌드에서 Steamworks API의 생명주기를 관리합니다.
    /// 실제 App ID가 발급되기 전에는 steam_appid.txt의 개발용 App ID 480을 사용합니다.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class SteamRuntimeBootstrap : MonoBehaviour
    {
        private const uint DevelopmentAppId = 480;

        public static SteamRuntimeBootstrap Instance { get; private set; }
        public static bool IsInitialized { get; private set; }
        public static ulong LocalSteamId { get; private set; }
        public static string LocalPersonaName { get; private set; } = string.Empty;

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
            InitializeSteam();
        }

        private void Update()
        {
            if (IsInitialized)
            {
                SteamAPI.RunCallbacks();
            }
        }

        private void OnApplicationQuit()
        {
            ShutdownSteam();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                ShutdownSteam();
                Instance = null;
            }
        }

        private static void InitializeSteam()
        {
            if (IsInitialized)
            {
                return;
            }

            try
            {
#if !UNITY_EDITOR
                if (SteamAPI.RestartAppIfNecessary(new AppId_t(DevelopmentAppId)))
                {
                    Debug.Log("[Steam Runtime] Steam을 통해 재실행이 요청되어 현재 프로세스를 종료합니다.");
                    Application.Quit();
                    return;
                }
#endif

                if (!Packsize.Test())
                {
                    Debug.LogError("[Steam Runtime] Steamworks 구조체 Packsize가 현재 플랫폼과 맞지 않습니다.");
                    return;
                }

                if (!DllCheck.Test())
                {
                    Debug.LogError("[Steam Runtime] Steamworks 네이티브 라이브러리 버전이 맞지 않습니다.");
                    return;
                }

                if (!SteamAPI.Init())
                {
                    Debug.LogError("[Steam Runtime] SteamAPI.Init 실패. Steam 클라이언트 실행 및 로그인을 확인해 주세요.");
                    return;
                }

                IsInitialized = true;
                LocalSteamId = SteamUser.GetSteamID().m_SteamID;
                LocalPersonaName = SteamFriends.GetPersonaName();
                SteamNetworkingUtils.InitRelayNetworkAccess();

                Debug.Log(
                    "[Steam Runtime] READY\n" +
                    $"App ID: {SteamUtils.GetAppID().m_AppId}\n" +
                    $"Steam ID: {LocalSteamId}\n" +
                    $"Persona: {LocalPersonaName}\n" +
                    "Steam Networking Sockets: READY");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Steam Runtime] 초기화 예외: {exception}");
                ShutdownSteam();
            }
        }

        private static void ShutdownSteam()
        {
            if (!IsInitialized)
            {
                return;
            }

            SteamAPI.Shutdown();
            IsInitialized = false;
            LocalSteamId = 0;
            LocalPersonaName = string.Empty;
            Debug.Log("[Steam Runtime] SHUTDOWN");
        }
    }
}
