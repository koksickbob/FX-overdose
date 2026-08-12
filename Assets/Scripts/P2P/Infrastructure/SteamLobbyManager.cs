using System;
using System.Collections.Generic;
using System.Globalization;
using FXOverdose.P2P.Lobby;
using Steamworks;
using UnityEngine;

namespace FXOverdose.P2P.Infrastructure
{
    /// <summary>Steam Lobby 콜백을 게임에서 사용하는 불변 스냅샷으로 변환합니다.</summary>
    [DefaultExecutionOrder(-9000)]
    public sealed class SteamLobbyManager : MonoBehaviour
    {
        private const int DefaultPlayers = 4;
        private const int DefaultLeverage = 20;
        private const double DefaultMarginRatio = 0.5;

        private Callback<LobbyCreated_t> lobbyCreated;
        private Callback<LobbyEnter_t> lobbyEntered;
        private Callback<LobbyChatUpdate_t> lobbyChatUpdated;
        private Callback<LobbyDataUpdate_t> lobbyDataUpdated;
        private Callback<GameLobbyJoinRequested_t> joinRequested;
        private Callback<LobbyKicked_t> lobbyKicked;
        private Callback<LobbyMatchList_t> lobbyMatchList;
        private SteamLobbySettings pendingSettings;
        private ulong currentLobbyId;
        private bool callbacksRegistered;

        public static SteamLobbyManager Instance { get; private set; }
        public SteamLobbyState State { get; private set; } = SteamLobbyState.Idle;
        public SteamLobbySnapshot CurrentLobby { get; private set; }
        public string LastError { get; private set; } = string.Empty;
        public event Action<SteamLobbySnapshot> LobbyChanged;
        public event Action<SteamLobbyState, string> StateChanged;
        public event Action<IReadOnlyList<SteamLobbySummary>> LobbySearchCompleted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateInstance()
        {
            if (Instance != null) return;
            var target = new GameObject(nameof(SteamLobbyManager));
            DontDestroyOnLoad(target);
            target.AddComponent<SteamLobbyManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (SteamRuntimeBootstrap.CanUseP2P) RegisterCallbacks();
            else SteamRuntimeBootstrap.StatusChanged += OnSteamStatusChanged;
        }

        public bool CreateLobby(SteamLobbyVisibility visibility, SteamLobbySettings settings)
        {
            if (!SteamRuntimeBootstrap.CanUseP2P) return Fail("Steam P2P를 사용할 수 없습니다.");
            if ((State != SteamLobbyState.Idle && State != SteamLobbyState.Failed) || !SteamLobbyRules.IsValid(settings)) return Fail("로비 설정 또는 현재 상태가 올바르지 않습니다.");
            pendingSettings = settings;
            SetState(SteamLobbyState.Creating);
            SteamMatchmaking.CreateLobby(ToSteamVisibility(visibility), settings.MaximumPlayers);
            return true;
        }

        public bool CreateDefaultLobby(SteamLobbyVisibility visibility) =>
            CreateLobby(visibility, new SteamLobbySettings(DefaultPlayers, DefaultLeverage, DefaultMarginRatio));

        public bool JoinLobby(ulong lobbyId)
        {
            if (!SteamRuntimeBootstrap.CanUseP2P || lobbyId == 0 || (State != SteamLobbyState.Idle && State != SteamLobbyState.Failed)) return Fail("로비에 참가할 수 없는 상태입니다.");
            SetState(SteamLobbyState.Joining);
            SteamMatchmaking.JoinLobby(new CSteamID(lobbyId));
            return true;
        }

        public bool SearchPublicLobbies(int maximumResults = 50)
        {
            if (!SteamRuntimeBootstrap.CanUseP2P || (State != SteamLobbyState.Idle && State != SteamLobbyState.Failed))
                return Fail("로비를 검색할 수 없는 상태입니다.");
            SteamMatchmaking.AddRequestLobbyListStringFilter(SteamLobbyDataKeys.Game, SteamLobbyDataKeys.GameValue, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(SteamLobbyDataKeys.Mode, SteamLobbyDataKeys.ModeValue, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(SteamLobbyDataKeys.Build, Application.version, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(1);
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(Math.Max(1, Math.Min(100, maximumResults)));
            SetState(SteamLobbyState.Searching);
            SteamMatchmaking.RequestLobbyList();
            return true;
        }

        public void LeaveLobby()
        {
            if (currentLobbyId != 0 && SteamRuntimeBootstrap.CanUseP2P) SteamMatchmaking.LeaveLobby(new CSteamID(currentLobbyId));
            currentLobbyId = 0;
            CurrentLobby = null;
            SetState(SteamLobbyState.Idle);
            LobbyChanged?.Invoke(null);
        }

        public bool SetReady(bool ready)
        {
            if (CurrentLobby == null || CurrentLobby.MatchStarted) return false;
            var lobby = new CSteamID(currentLobbyId);
            SteamMatchmaking.SetLobbyMemberData(lobby, SteamLobbyDataKeys.Ready, ready ? "1" : "0");
            SteamMatchmaking.SetLobbyMemberData(lobby, SteamLobbyDataKeys.ReadyRevision, CurrentLobby.RulesRevision.ToString(CultureInfo.InvariantCulture));
            RefreshSnapshot();
            return true;
        }

        public bool UpdateRules(SteamLobbySettings settings)
        {
            if (CurrentLobby == null || CurrentLobby.OwnerSteamId != SteamRuntimeBootstrap.LocalSteamId ||
                CurrentLobby.MatchStarted || !SteamLobbyRules.IsValid(settings)) return false;
            var lobby = new CSteamID(currentLobbyId);
            int revision = CurrentLobby.RulesRevision + 1;
            bool success = SteamMatchmaking.SetLobbyMemberLimit(lobby, settings.MaximumPlayers) &&
                SetLobbyData(lobby, SteamLobbyDataKeys.MaximumLeverage, settings.MaximumLeverage.ToString(CultureInfo.InvariantCulture)) &&
                SetLobbyData(lobby, SteamLobbyDataKeys.MaximumMarginRatio, settings.MaximumMarginRatio.ToString(CultureInfo.InvariantCulture)) &&
                SetLobbyData(lobby, SteamLobbyDataKeys.RulesRevision, revision.ToString(CultureInfo.InvariantCulture));
            if (success) RefreshSnapshot();
            return success;
        }

        public bool TryStartMatch()
        {
            if (!SteamLobbyRules.CanStart(CurrentLobby, SteamRuntimeBootstrap.LocalSteamId)) return false;
            var lobby = new CSteamID(currentLobbyId);
            string nonce = Guid.NewGuid().ToString("N");
            bool success = SetLobbyData(lobby, SteamLobbyDataKeys.ConnectionNonce, nonce) &&
                SetLobbyData(lobby, SteamLobbyDataKeys.MatchStarted, "1") && SteamMatchmaking.SetLobbyJoinable(lobby, false);
            if (success) RefreshSnapshot();
            return success;
        }

        /// <summary>결과 화면에서 같은 Steam 로비를 재대결 준비 상태로 되돌립니다.</summary>
        public bool ResetMatchForRematch()
        {
            if (CurrentLobby == null || CurrentLobby.OwnerSteamId != SteamRuntimeBootstrap.LocalSteamId) return false;
            var lobby = new CSteamID(currentLobbyId);
            bool success = SetLobbyData(lobby, SteamLobbyDataKeys.MatchStarted, "0") &&
                SetLobbyData(lobby, SteamLobbyDataKeys.ConnectionNonce, string.Empty) &&
                SteamMatchmaking.SetLobbyJoinable(lobby, true);
            if (success)
            {
                SteamMatchmaking.SetLobbyMemberData(lobby, SteamLobbyDataKeys.Ready, "0");
                RefreshSnapshot();
            }
            return success;
        }

        public void OpenInviteOverlay()
        {
            if (currentLobbyId != 0) SteamFriends.ActivateGameOverlayInviteDialog(new CSteamID(currentLobbyId));
        }

        private void RegisterCallbacks()
        {
            if (callbacksRegistered) return;
            callbacksRegistered = true;
            lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
            lobbyChatUpdated = Callback<LobbyChatUpdate_t>.Create(_ => RefreshSnapshot());
            lobbyDataUpdated = Callback<LobbyDataUpdate_t>.Create(data => { if (data.m_ulSteamIDLobby == currentLobbyId) RefreshSnapshot(); });
            joinRequested = Callback<GameLobbyJoinRequested_t>.Create(data => { if (State == SteamLobbyState.Idle) JoinLobby(data.m_steamIDLobby.m_SteamID); });
            lobbyKicked = Callback<LobbyKicked_t>.Create(_ => LeaveLobby());
            lobbyMatchList = Callback<LobbyMatchList_t>.Create(OnLobbyMatchList);
        }

        private void OnLobbyMatchList(LobbyMatchList_t data)
        {
            var results = new List<SteamLobbySummary>((int)data.m_nLobbiesMatching);
            for (int i = 0; i < data.m_nLobbiesMatching; i++)
            {
                CSteamID lobby = SteamMatchmaking.GetLobbyByIndex(i);
                if (!lobby.IsValid()) continue;
                results.Add(new SteamLobbySummary(lobby.m_SteamID,
                    ParseInt(SteamMatchmaking.GetLobbyData(lobby, SteamLobbyDataKeys.MemberCount), 0), SteamMatchmaking.GetLobbyMemberLimit(lobby),
                    ParseInt(SteamMatchmaking.GetLobbyData(lobby, SteamLobbyDataKeys.MaximumLeverage), DefaultLeverage),
                    ParseDouble(SteamMatchmaking.GetLobbyData(lobby, SteamLobbyDataKeys.MaximumMarginRatio), DefaultMarginRatio)));
            }
            SetState(SteamLobbyState.Idle);
            LobbySearchCompleted?.Invoke(results);
        }

        private void OnSteamStatusChanged(FXOverdose.P2P.Steam.SteamRuntimeStatus status)
        {
            if (!status.CanUseP2P) return;
            SteamRuntimeBootstrap.StatusChanged -= OnSteamStatusChanged;
            RegisterCallbacks();
        }

        private void OnLobbyCreated(LobbyCreated_t data)
        {
            if (data.m_eResult != EResult.k_EResultOK || data.m_ulSteamIDLobby == 0) { Fail($"Steam 로비 생성 실패: {data.m_eResult}"); return; }
            var lobby = new CSteamID(data.m_ulSteamIDLobby);
            SetLobbyData(lobby, SteamLobbyDataKeys.Game, SteamLobbyDataKeys.GameValue);
            SetLobbyData(lobby, SteamLobbyDataKeys.Mode, SteamLobbyDataKeys.ModeValue);
            SetLobbyData(lobby, SteamLobbyDataKeys.Build, Application.version);
            SetLobbyData(lobby, SteamLobbyDataKeys.MaximumLeverage, pendingSettings.MaximumLeverage.ToString(CultureInfo.InvariantCulture));
            SetLobbyData(lobby, SteamLobbyDataKeys.MaximumMarginRatio, pendingSettings.MaximumMarginRatio.ToString(CultureInfo.InvariantCulture));
            SetLobbyData(lobby, SteamLobbyDataKeys.RulesRevision, "1");
            SetLobbyData(lobby, SteamLobbyDataKeys.MatchStarted, "0");
        }

        private void OnLobbyEntered(LobbyEnter_t data)
        {
            if ((EChatRoomEnterResponse)data.m_EChatRoomEnterResponse != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            { Fail($"Steam 로비 참가 실패: {(EChatRoomEnterResponse)data.m_EChatRoomEnterResponse}"); return; }
            var lobby = new CSteamID(data.m_ulSteamIDLobby);
            if (SteamMatchmaking.GetLobbyData(lobby, SteamLobbyDataKeys.Game) != SteamLobbyDataKeys.GameValue ||
                SteamMatchmaking.GetLobbyData(lobby, SteamLobbyDataKeys.Mode) != SteamLobbyDataKeys.ModeValue ||
                SteamMatchmaking.GetLobbyData(lobby, SteamLobbyDataKeys.Build) != Application.version ||
                SteamMatchmaking.GetLobbyData(lobby, SteamLobbyDataKeys.MatchStarted) == "1")
            { SteamMatchmaking.LeaveLobby(lobby); Fail("게임 버전이 다르거나 이미 시작된 로비입니다."); return; }
            currentLobbyId = data.m_ulSteamIDLobby;
            SetState(SteamLobbyState.InLobby);
            SetReady(false);
            RefreshSnapshot();
        }

        private void RefreshSnapshot()
        {
            if (currentLobbyId == 0) return;
            var lobby = new CSteamID(currentLobbyId);
            ulong owner = SteamMatchmaking.GetLobbyOwner(lobby).m_SteamID;
            int revision = ParseInt(SteamMatchmaking.GetLobbyData(lobby, SteamLobbyDataKeys.RulesRevision), 1);
            var settings = new SteamLobbySettings(SteamMatchmaking.GetLobbyMemberLimit(lobby),
                ParseInt(SteamMatchmaking.GetLobbyData(lobby, SteamLobbyDataKeys.MaximumLeverage), DefaultLeverage),
                ParseDouble(SteamMatchmaking.GetLobbyData(lobby, SteamLobbyDataKeys.MaximumMarginRatio), DefaultMarginRatio));
            int count = SteamMatchmaking.GetNumLobbyMembers(lobby);
            if (owner == SteamRuntimeBootstrap.LocalSteamId &&
                SteamMatchmaking.GetLobbyData(lobby, SteamLobbyDataKeys.MemberCount) != count.ToString(CultureInfo.InvariantCulture))
                SetLobbyData(lobby, SteamLobbyDataKeys.MemberCount, count.ToString(CultureInfo.InvariantCulture));
            var members = new List<SteamLobbyMember>(count);
            for (int i = 0; i < count; i++)
            {
                CSteamID id = SteamMatchmaking.GetLobbyMemberByIndex(lobby, i);
                bool ready = SteamMatchmaking.GetLobbyMemberData(lobby, id, SteamLobbyDataKeys.Ready) == "1";
                int readyRevision = ParseInt(SteamMatchmaking.GetLobbyMemberData(lobby, id, SteamLobbyDataKeys.ReadyRevision), 0);
                members.Add(new SteamLobbyMember(id.m_SteamID, SteamFriends.GetFriendPersonaName(id), id.m_SteamID == owner, ready, readyRevision));
            }
            CurrentLobby = new SteamLobbySnapshot(currentLobbyId, owner, settings, revision,
                SteamMatchmaking.GetLobbyData(lobby, SteamLobbyDataKeys.MatchStarted) == "1", members,
                SteamMatchmaking.GetLobbyData(lobby, SteamLobbyDataKeys.ConnectionNonce));
            LobbyChanged?.Invoke(CurrentLobby);
        }

        private bool Fail(string message) { LastError = message; SetState(SteamLobbyState.Failed, message); Debug.LogWarning($"[Steam Lobby] {message}"); return false; }
        private void SetState(SteamLobbyState state, string message = "") { State = state; LastError = message; StateChanged?.Invoke(state, message); }
        private static bool SetLobbyData(CSteamID lobby, string key, string value) => SteamMatchmaking.SetLobbyData(lobby, key, value);
        private static int ParseInt(string value, int fallback) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) ? result : fallback;
        private static double ParseDouble(string value, double fallback) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) ? result : fallback;
        private static ELobbyType ToSteamVisibility(SteamLobbyVisibility visibility) =>
            visibility == SteamLobbyVisibility.Public
                ? ELobbyType.k_ELobbyTypePublic
                : ELobbyType.k_ELobbyTypePrivate;

        private void OnDestroy()
        {
            if (Instance != this) return;
            LeaveLobby();
            SteamRuntimeBootstrap.StatusChanged -= OnSteamStatusChanged;
            lobbyCreated?.Dispose(); lobbyEntered?.Dispose(); lobbyChatUpdated?.Dispose(); lobbyDataUpdated?.Dispose(); joinRequested?.Dispose(); lobbyKicked?.Dispose(); lobbyMatchList?.Dispose();
            Instance = null;
        }
    }
}
