using System;

namespace FXOverdose.P2P.Steam
{
    /// <summary>Unity와 Steamworks.NET에 직접 의존하지 않는 Steam 런타임 상태 관리자입니다.</summary>
    public sealed class SteamRuntimeService : ISteamRuntimeService
    {
        private readonly ISteamRuntimeBackend backend;
        private bool apiInitialized;

        public SteamRuntimeService(ISteamRuntimeBackend backend)
        {
            this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
            Status = new SteamRuntimeStatus(SteamRuntimeState.NotStarted);
        }

        public SteamRuntimeStatus Status { get; private set; }
        public bool CanUseP2P => Status.CanUseP2P;
        public event Action<SteamRuntimeStatus> StatusChanged;

        public bool Initialize(uint developmentAppId, bool checkRestart)
        {
            if (Status.State == SteamRuntimeState.Ready)
            {
                return true;
            }

            SetStatus(new SteamRuntimeStatus(SteamRuntimeState.Initializing));

            try
            {
                if (checkRestart && backend.RestartAppIfNecessary(developmentAppId))
                {
                    return Fail(SteamInitializationError.RestartRequired,
                        "Steam을 통해 게임을 다시 실행해야 합니다.");
                }

                if (!backend.IsPacksizeValid())
                {
                    return Fail(SteamInitializationError.PacksizeMismatch,
                        "Steamworks 구조체 크기가 현재 플랫폼과 맞지 않습니다.");
                }

                if (!backend.IsNativeLibraryValid())
                {
                    return Fail(SteamInitializationError.NativeLibraryMismatch,
                        "Steamworks 네이티브 라이브러리 버전이 맞지 않습니다.");
                }

                if (!backend.Initialize())
                {
                    return Fail(SteamInitializationError.SteamClientUnavailable,
                        "Steam 클라이언트 실행, 로그인 및 앱 라이선스를 확인해 주세요.");
                }

                apiInitialized = true;
                ulong steamId = backend.GetSteamId();
                if (steamId == 0)
                {
                    ShutdownBackendAfterFailure();
                    return Fail(SteamInitializationError.InvalidSteamUser,
                        "로그인된 Steam 사용자를 확인할 수 없습니다.");
                }

                uint appId = backend.GetAppId();
                string personaName = backend.GetPersonaName();
                backend.InitializeRelayNetworkAccess();
                SetStatus(new SteamRuntimeStatus(
                    SteamRuntimeState.Ready,
                    appId: appId,
                    steamId: steamId,
                    personaName: personaName));
                return true;
            }
            catch (Exception exception)
            {
                ShutdownBackendAfterFailure();
                return Fail(SteamInitializationError.UnexpectedException, exception.Message);
            }
        }

        public void PumpCallbacks()
        {
            if (Status.State != SteamRuntimeState.Ready || !apiInitialized)
            {
                return;
            }

            try
            {
                backend.RunCallbacks();
            }
            catch (Exception exception)
            {
                ShutdownBackendAfterFailure();
                Fail(SteamInitializationError.UnexpectedException, exception.Message);
            }
        }

        public void Shutdown()
        {
            if (!apiInitialized)
            {
                if (Status.State == SteamRuntimeState.NotStarted)
                {
                    SetStatus(new SteamRuntimeStatus(SteamRuntimeState.Shutdown));
                }
                return;
            }

            SetStatus(new SteamRuntimeStatus(SteamRuntimeState.ShuttingDown));
            backend.Shutdown();
            apiInitialized = false;
            SetStatus(new SteamRuntimeStatus(SteamRuntimeState.Shutdown));
        }

        private bool Fail(SteamInitializationError error, string message)
        {
            SetStatus(new SteamRuntimeStatus(SteamRuntimeState.Failed, error, message));
            return false;
        }

        private void ShutdownBackendAfterFailure()
        {
            if (!apiInitialized)
            {
                return;
            }

            backend.Shutdown();
            apiInitialized = false;
        }

        private void SetStatus(SteamRuntimeStatus status)
        {
            Status = status;
            StatusChanged?.Invoke(status);
        }
    }
}
