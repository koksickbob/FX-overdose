using FXOverdose.P2P.Steam;
using Steamworks;

namespace FXOverdose.P2P.Infrastructure
{
    /// <summary>SteamRuntimeService의 실제 Steamworks.NET 어댑터입니다.</summary>
    public sealed class SteamworksRuntimeBackend : ISteamRuntimeBackend
    {
        public bool RestartAppIfNecessary(uint appId) =>
            SteamAPI.RestartAppIfNecessary(new AppId_t(appId));

        public bool IsPacksizeValid() => Packsize.Test();
        public bool IsNativeLibraryValid() => DllCheck.Test();
        public bool Initialize() => SteamAPI.Init();
        public uint GetAppId() => SteamUtils.GetAppID().m_AppId;
        public ulong GetSteamId() => SteamUser.GetSteamID().m_SteamID;
        public string GetPersonaName() => SteamFriends.GetPersonaName();
        public void InitializeRelayNetworkAccess() => SteamNetworkingUtils.InitRelayNetworkAccess();
        public void RunCallbacks() => SteamAPI.RunCallbacks();
        public void Shutdown() => SteamAPI.Shutdown();
    }
}
