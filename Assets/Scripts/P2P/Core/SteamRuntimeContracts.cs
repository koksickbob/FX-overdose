using System;

namespace FXOverdose.P2P.Steam
{
    public enum SteamRuntimeState
    {
        NotStarted,
        Initializing,
        Ready,
        Failed,
        ShuttingDown,
        Shutdown
    }

    public enum SteamInitializationError
    {
        None,
        RestartRequired,
        PacksizeMismatch,
        NativeLibraryMismatch,
        SteamClientUnavailable,
        InvalidSteamUser,
        UnexpectedException
    }

    /// <summary>UI와 로비 계층에 노출되는 Steam 초기화 상태의 불변 스냅샷입니다.</summary>
    public readonly struct SteamRuntimeStatus
    {
        public SteamRuntimeStatus(
            SteamRuntimeState state,
            SteamInitializationError error = SteamInitializationError.None,
            string message = "",
            uint appId = 0,
            ulong steamId = 0,
            string personaName = "")
        {
            State = state;
            Error = error;
            Message = message ?? string.Empty;
            AppId = appId;
            SteamId = steamId;
            PersonaName = personaName ?? string.Empty;
        }

        public SteamRuntimeState State { get; }
        public SteamInitializationError Error { get; }
        public string Message { get; }
        public uint AppId { get; }
        public ulong SteamId { get; }
        public string PersonaName { get; }
        public bool CanUseP2P => State == SteamRuntimeState.Ready && SteamId != 0;
    }

    /// <summary>Steamworks.NET 정적 API를 테스트 가능한 객체 경계로 감쌉니다.</summary>
    public interface ISteamRuntimeBackend
    {
        bool RestartAppIfNecessary(uint appId);
        bool IsPacksizeValid();
        bool IsNativeLibraryValid();
        bool Initialize();
        uint GetAppId();
        ulong GetSteamId();
        string GetPersonaName();
        void InitializeRelayNetworkAccess();
        void RunCallbacks();
        void Shutdown();
    }

    public interface ISteamRuntimeService
    {
        SteamRuntimeStatus Status { get; }
        bool CanUseP2P { get; }
        event Action<SteamRuntimeStatus> StatusChanged;
        bool Initialize(uint developmentAppId, bool checkRestart);
        void PumpCallbacks();
        void Shutdown();
    }
}
