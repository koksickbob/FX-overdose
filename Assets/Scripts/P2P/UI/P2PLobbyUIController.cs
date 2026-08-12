using System;
using System.Collections.Generic;
using FXOverdose.P2P.Core;
using FXOverdose.P2P.Infrastructure;
using FXOverdose.P2P.Lobby;
using FXOverdose.P2P.Steam;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FXOverdose.P2P.UI
{
    /// <summary>타이틀에서 Steam 로비와 NGO 연결을 직접 시험하는 런타임 UI입니다.</summary>
    public sealed class P2PLobbyUIController : MonoBehaviour
    {
        private static readonly Color32 Navy = new(7, 17, 31, 255);
        private static readonly Color32 Panel = new(12, 27, 47, 250);
        private static readonly Color32 Cyan = new(6, 182, 212, 255);
        private static readonly Color32 Pink = new(255, 72, 114, 255);
        private static readonly Color32 TextColor = new(225, 242, 255, 255);
        private static readonly Color32 Muted = new(126, 160, 184, 255);

        private GameObject window;
        private GameObject browserView;
        private GameObject roomView;
        private TMP_Text steamStatus;
        private TMP_Text lobbyStatus;
        private TMP_Text settingsText;
        private TMP_Text membersText;
        private TMP_Text networkStatus;
        private TMP_Text tradingStatus;
        private Button readyButton;
        private Button startButton;
        private readonly List<Button> ruleButtons = new();
        private readonly List<Button> searchButtons = new();
        private int leverageIndex = 4;
        private int marginIndex = 2;
        private readonly int[] leveragePresets = { 1, 2, 5, 10, 20, 50, 100 };
        private readonly double[] marginPresets = { 0.1, 0.25, 0.5, 0.75, 1.0 };
        private bool localReady;
        private float marketUiTimer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= BuildForScene;
            SceneManager.sceneLoaded += BuildForScene;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BuildCurrentScene() => BuildForScene(SceneManager.GetActiveScene(), LoadSceneMode.Single);

        private static void BuildForScene(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "TitleScene" || FindAnyObjectByType<P2PLobbyUIController>() != null) return;
            Canvas canvas = FindTitleCanvas();
            if (canvas == null) return;
            var target = new GameObject("P2PLobbyUIController", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            target.transform.SetParent(canvas.transform, false);
            RectTransform rect = target.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Canvas overlayCanvas = target.GetComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = 5000;
            target.AddComponent<P2PLobbyUIController>();
        }

        private static Canvas FindTitleCanvas()
        {
            GameObject named = GameObject.Find("Canvas_MainMenu");
            if (named != null && named.TryGetComponent(out Canvas canvas)) return canvas;
            return FindAnyObjectByType<Canvas>();
        }

        private void Awake() => BuildUI();

        private void OnEnable()
        {
            SteamRuntimeBootstrap.StatusChanged += OnSteamStatus;
            BindManagers();
            RefreshAll();
        }

        private void Start()
        {
            BindManagers();
            RefreshAll();
        }

        private void Update()
        {
            if (window == null || !window.activeSelf) return;
            marketUiTimer += Time.unscaledDeltaTime;
            if (marketUiTimer < 0.25f) return;
            marketUiTimer = 0f;
            var authority = P2PNetworkSessionManager.Instance?.MarketAuthority;
            if (authority == null || authority.CurrentSnapshot.Sequence == 0) return;
            var market = authority.CurrentSnapshot;
            networkStatus.text = $"NGO 동기화 | {market.Hour:00}:{market.Minute:00} | {market.Price:N1} | #{market.Sequence}";
            networkStatus.color = Cyan;
            RefreshTradingStatus();
        }

        private void OnDisable()
        {
            SteamRuntimeBootstrap.StatusChanged -= OnSteamStatus;
            if (SteamLobbyManager.Instance != null)
            {
                SteamLobbyManager.Instance.LobbyChanged -= OnLobbyChanged;
                SteamLobbyManager.Instance.StateChanged -= OnLobbyStateChanged;
                SteamLobbyManager.Instance.LobbySearchCompleted -= OnSearchCompleted;
            }
            if (P2PNetworkSessionManager.Instance != null)
            {
                P2PNetworkSessionManager.Instance.ClientMapped -= OnClientMapped;
                P2PNetworkSessionManager.Instance.ConnectionFailed -= OnConnectionFailed;
            }
        }

        private void BindManagers()
        {
            var lobby = SteamLobbyManager.Instance;
            if (lobby != null)
            {
                lobby.LobbyChanged -= OnLobbyChanged; lobby.LobbyChanged += OnLobbyChanged;
                lobby.StateChanged -= OnLobbyStateChanged; lobby.StateChanged += OnLobbyStateChanged;
                lobby.LobbySearchCompleted -= OnSearchCompleted; lobby.LobbySearchCompleted += OnSearchCompleted;
            }
            var network = P2PNetworkSessionManager.Instance;
            if (network != null)
            {
                network.ClientMapped -= OnClientMapped; network.ClientMapped += OnClientMapped;
                network.ConnectionFailed -= OnConnectionFailed; network.ConnectionFailed += OnConnectionFailed;
            }
        }

        private void BuildUI()
        {
            Button open = CreateButton(transform, "Btn_P2P", "P2P MULTI", new Vector2(0.755f, 0.78f), new Vector2(0.955f, 0.86f), Cyan);
            open.onClick.AddListener(() =>
            {
                transform.SetAsLastSibling();
                window.SetActive(true);
                window.transform.SetAsLastSibling();
                RefreshAll();
                if (SteamLobbyManager.Instance?.CurrentLobby == null && SteamRuntimeBootstrap.CanUseP2P)
                    Search();
            });

            window = CreateImage(transform, "P2PLobbyPanel", new Color32(2, 8, 18, 235), Vector2.zero, Vector2.one).gameObject;
            Image modal = CreateImage(window.transform, "Window", Panel, new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.94f));
            modal.gameObject.AddComponent<Outline>().effectColor = Cyan;
            CreateText(modal.transform, "Title", "STEAM P2P", 32, TextColor, new Vector2(0.04f, 0.91f), new Vector2(0.96f, 0.98f), TextAlignmentOptions.Left).fontStyle = FontStyles.Bold;
            steamStatus = CreateText(modal.transform, "SteamStatus", "STEAM 확인 중", 15, Muted, new Vector2(0.04f, 0.855f), new Vector2(0.96f, 0.91f), TextAlignmentOptions.Left);
            lobbyStatus = CreateText(modal.transform, "LobbyStatus", "로비 없음", 16, Cyan, new Vector2(0.04f, 0.80f), new Vector2(0.96f, 0.855f), TextAlignmentOptions.Left);
            CreateButton(modal.transform, "Close", "X", new Vector2(0.91f, 0.91f), new Vector2(0.97f, 0.98f), Pink).onClick.AddListener(() => window.SetActive(false));

            browserView = CreateView(modal.transform, "LobbyBrowserView");
            CreateText(browserView.transform, "BrowserTitle", "전체 로비", 25, TextColor, new Vector2(0.04f, 0.86f), new Vector2(0.96f, 0.96f), TextAlignmentOptions.Left).fontStyle = FontStyles.Bold;
            CreateButton(browserView.transform, "Refresh", "목록 새로고침", new Vector2(0.04f, 0.75f), new Vector2(0.20f, 0.84f), Cyan).onClick.AddListener(Search);
            CreateButton(browserView.transform, "Public", "공개 로비 생성", new Vector2(0.57f, 0.75f), new Vector2(0.75f, 0.84f), Cyan).onClick.AddListener(() => CreateLobby(SteamLobbyVisibility.Public));
            // 친구 로비는 목록에 노출되지 않으며 Steam 초대로만 참가합니다.
            CreateButton(browserView.transform, "Friends", "친구 로비 생성", new Vector2(0.77f, 0.75f), new Vector2(0.96f, 0.84f), Pink).onClick.AddListener(() => CreateLobby(SteamLobbyVisibility.InviteOnly));
            CreateText(browserView.transform, "SearchTitle", "공개 로비 목록", 18, Cyan, new Vector2(0.04f, 0.65f), new Vector2(0.96f, 0.72f), TextAlignmentOptions.Left);
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                Button result = CreateButton(browserView.transform, $"SearchResult{i}", "-", new Vector2(0.04f, 0.54f - i * 0.11f), new Vector2(0.96f, 0.63f - i * 0.11f), Cyan);
                result.gameObject.SetActive(false);
                result.onClick.AddListener(() => JoinSearchResult(index));
                searchButtons.Add(result);
            }

            roomView = CreateView(modal.transform, "LobbyRoomView");
            CreateText(roomView.transform, "RoomTitle", "특정 로비", 25, TextColor, new Vector2(0.04f, 0.86f), new Vector2(0.96f, 0.96f), TextAlignmentOptions.Left).fontStyle = FontStyles.Bold;
            membersText = CreateText(roomView.transform, "Members", "참가자 없음", 18, TextColor, new Vector2(0.04f, 0.32f), new Vector2(0.48f, 0.82f), TextAlignmentOptions.TopLeft);
            settingsText = CreateText(roomView.transform, "Settings", "", 18, TextColor, new Vector2(0.52f, 0.70f), new Vector2(0.96f, 0.82f), TextAlignmentOptions.Left);
            ruleButtons.Add(CreateButton(roomView.transform, "LevDown", "레버리지 -", new Vector2(0.52f, 0.59f), new Vector2(0.72f, 0.68f), Cyan));
            ruleButtons[0].onClick.AddListener(() => ChangeLeverage(-1));
            ruleButtons.Add(CreateButton(roomView.transform, "LevUp", "레버리지 +", new Vector2(0.75f, 0.59f), new Vector2(0.96f, 0.68f), Cyan));
            ruleButtons[1].onClick.AddListener(() => ChangeLeverage(1));
            ruleButtons.Add(CreateButton(roomView.transform, "MarginDown", "마진 -", new Vector2(0.52f, 0.48f), new Vector2(0.72f, 0.57f), Cyan));
            ruleButtons[2].onClick.AddListener(() => ChangeMargin(-1));
            ruleButtons.Add(CreateButton(roomView.transform, "MarginUp", "마진 +", new Vector2(0.75f, 0.48f), new Vector2(0.96f, 0.57f), Cyan));
            ruleButtons[3].onClick.AddListener(() => ChangeMargin(1));
            CreateButton(roomView.transform, "Invite", "친구 초대", new Vector2(0.52f, 0.36f), new Vector2(0.72f, 0.45f), Pink).onClick.AddListener(() => SteamLobbyManager.Instance?.OpenInviteOverlay());
            readyButton = CreateButton(roomView.transform, "Ready", "준비", new Vector2(0.04f, 0.12f), new Vector2(0.22f, 0.23f), Cyan);
            readyButton.onClick.AddListener(ToggleReady);
            startButton = CreateButton(roomView.transform, "Start", "경기 시작", new Vector2(0.25f, 0.12f), new Vector2(0.43f, 0.23f), Pink);
            startButton.onClick.AddListener(() => SteamLobbyManager.Instance?.TryStartMatch());
            CreateButton(roomView.transform, "Leave", "나가기", new Vector2(0.46f, 0.12f), new Vector2(0.62f, 0.23f), Muted).onClick.AddListener(() => SteamLobbyManager.Instance?.LeaveLobby());
            networkStatus = CreateText(roomView.transform, "NetworkStatus", "NGO: 대기", 16, Muted, new Vector2(0.66f, 0.12f), new Vector2(0.96f, 0.23f), TextAlignmentOptions.Left);
            tradingStatus = CreateText(roomView.transform, "TradingStatus", "매매 연결 대기", 14, TextColor, new Vector2(0.52f, 0.24f), new Vector2(0.96f, 0.34f), TextAlignmentOptions.TopLeft);
            CreateButton(roomView.transform, "Long", "LONG", new Vector2(0.04f, 0.01f), new Vector2(0.18f, 0.09f), Cyan).onClick.AddListener(() => SubmitTrade(P2PTradeAction.OpenLong));
            CreateButton(roomView.transform, "Short", "SHORT", new Vector2(0.20f, 0.01f), new Vector2(0.34f, 0.09f), Pink).onClick.AddListener(() => SubmitTrade(P2PTradeAction.OpenShort));
            CreateButton(roomView.transform, "ClosePosition", "포지션 종료", new Vector2(0.36f, 0.01f), new Vector2(0.51f, 0.09f), Muted).onClick.AddListener(() => SubmitTrade(P2PTradeAction.ClosePosition));
            roomView.SetActive(false);
            window.SetActive(false);
        }

        private readonly List<SteamLobbySummary> searchResults = new();
        private void CreateLobby(SteamLobbyVisibility visibility) => SteamLobbyManager.Instance?.CreateLobby(visibility, CurrentSettings());
        private void Search() { searchResults.Clear(); HideSearchResults(); SteamLobbyManager.Instance?.SearchPublicLobbies(20); }
        private void JoinSearchResult(int index) { if (index < searchResults.Count) SteamLobbyManager.Instance?.JoinLobby(searchResults[index].LobbyId); }
        private SteamLobbySettings CurrentSettings() => new(4, leveragePresets[leverageIndex], marginPresets[marginIndex]);

        private void ChangeLeverage(int delta)
        {
            leverageIndex = Mathf.Clamp(leverageIndex + delta, 0, leveragePresets.Length - 1);
            ApplyRules();
        }

        private void ChangeMargin(int delta)
        {
            marginIndex = Mathf.Clamp(marginIndex + delta, 0, marginPresets.Length - 1);
            ApplyRules();
        }

        private void ApplyRules()
        {
            SteamLobbySnapshot lobby = SteamLobbyManager.Instance?.CurrentLobby;
            if (lobby != null && lobby.OwnerSteamId == SteamRuntimeBootstrap.LocalSteamId)
                SteamLobbyManager.Instance.UpdateRules(CurrentSettings());
            RefreshAll();
        }

        private void ToggleReady()
        {
            SteamLobbySnapshot lobby = SteamLobbyManager.Instance?.CurrentLobby;
            if (lobby == null) return;
            localReady = false;
            for (int i = 0; i < lobby.Members.Count; i++)
                if (lobby.Members[i].SteamId == SteamRuntimeBootstrap.LocalSteamId)
                    localReady = SteamLobbyRules.IsEffectivelyReady(lobby.Members[i], lobby.RulesRevision);
            SteamLobbyManager.Instance.SetReady(!localReady);
        }

        private void SubmitTrade(P2PTradeAction action)
        {
            var trading = P2PNetworkSessionManager.Instance?.TradingAuthority;
            if (trading == null) return;
            trading.Submit(action, leveragePresets[leverageIndex],
                action == P2PTradeAction.ClosePosition ? 0 : marginPresets[marginIndex]);
        }

        private void RefreshTradingStatus()
        {
            var trading = P2PNetworkSessionManager.Instance?.TradingAuthority;
            if (trading == null || trading.Players.Count == 0 || tradingStatus == null) return;
            var lines = new List<string>();
            foreach (var player in trading.Players)
                lines.Add($"#{player.Rank} {player.Name} {player.Equity:N0} {player.Side} PnL {player.UnrealizedPnL:+0;-0;0}");
            tradingStatus.text = string.Join("\n", lines) + $"\n결과: {trading.LastResult.RejectReason} @ {trading.LastResult.FillPrice:N1}";
        }

        private void OnSteamStatus(SteamRuntimeStatus _) => RefreshAll();
        private void OnLobbyChanged(SteamLobbySnapshot _) => RefreshAll();
        private void OnLobbyStateChanged(SteamLobbyState _, string __) => RefreshAll();
        private void OnClientMapped(ulong clientId, ulong steamId) { networkStatus.text = $"NGO 연결: Client {clientId} / Steam {steamId}"; }
        private void OnConnectionFailed(string reason) { networkStatus.text = $"NGO 실패: {reason}"; networkStatus.color = Pink; }

        private void OnSearchCompleted(IReadOnlyList<SteamLobbySummary> results)
        {
            searchResults.Clear();
            for (int i = 0; i < results.Count && i < searchButtons.Count; i++)
            {
                searchResults.Add(results[i]);
                searchButtons[i].gameObject.SetActive(true);
                searchButtons[i].GetComponentInChildren<TMP_Text>().text = $"방 {results[i].LobbyId}  {results[i].MemberCount}/{results[i].MaximumPlayers}  x{results[i].MaximumLeverage}";
            }
            lobbyStatus.text = results.Count == 0 ? "검색된 공개 로비가 없습니다." : $"공개 로비 {results.Count}개 발견";
        }

        private void HideSearchResults() { foreach (Button button in searchButtons) button.gameObject.SetActive(false); }

        private void RefreshAll()
        {
            if (steamStatus == null) return;
            SteamRuntimeStatus steam = SteamRuntimeBootstrap.CurrentStatus;
            steamStatus.text = steam.CanUseP2P ? $"STEAM READY  |  {steam.PersonaName}  |  {steam.SteamId}" : $"STEAM OFFLINE  |  {steam.Error}: {steam.Message}";
            steamStatus.color = steam.CanUseP2P ? Cyan : Pink;
            SteamLobbyManager manager = SteamLobbyManager.Instance;
            SteamLobbySnapshot lobby = manager?.CurrentLobby;
            bool isInRoom = lobby != null;
            if (browserView != null) browserView.SetActive(!isInRoom);
            if (roomView != null) roomView.SetActive(isInRoom);
            lobbyStatus.text = lobby == null ? $"상태: {manager?.State ?? SteamLobbyState.Idle}  {manager?.LastError}" : $"LOBBY {lobby.LobbyId}  |  {(lobby.MatchStarted ? "게임 시작됨" : "대기 중")}";
            if (lobby == null)
            {
                settingsText.text = $"최대 레버리지 x{leveragePresets[leverageIndex]}  |  최대 마진 {marginPresets[marginIndex]:P0}";
                membersText.text = "참가자 없음";
                localReady = false;
            }
            else
            {
                leverageIndex = Math.Max(0, Array.IndexOf(leveragePresets, lobby.Settings.MaximumLeverage));
                marginIndex = Math.Max(0, Array.IndexOf(marginPresets, lobby.Settings.MaximumMarginRatio));
                settingsText.text = $"최대 레버리지 x{lobby.Settings.MaximumLeverage}  |  최대 마진 {lobby.Settings.MaximumMarginRatio:P0}  |  규칙 #{lobby.RulesRevision}";
                var lines = new List<string> { $"참가자 {lobby.Members.Count}/{lobby.Settings.MaximumPlayers}" };
                localReady = false;
                for (int i = 0; i < lobby.Members.Count; i++)
                {
                    SteamLobbyMember member = lobby.Members[i];
                    bool ready = SteamLobbyRules.IsEffectivelyReady(member, lobby.RulesRevision);
                    lines.Add($"{(member.IsHost ? "[HOST]" : ready ? "[READY]" : "[WAIT]")} {member.PersonaName} ({member.SteamId})");
                    if (member.SteamId == SteamRuntimeBootstrap.LocalSteamId) localReady = ready;
                }
                membersText.text = string.Join("\n", lines);
                bool isHost = lobby.OwnerSteamId == SteamRuntimeBootstrap.LocalSteamId;
                foreach (Button button in ruleButtons) button.interactable = isHost && !lobby.MatchStarted;
                startButton.interactable = isHost && SteamLobbyRules.CanStart(lobby, SteamRuntimeBootstrap.LocalSteamId);
                readyButton.interactable = !isHost && !lobby.MatchStarted;
                readyButton.GetComponentInChildren<TMP_Text>().text = localReady ? "준비 취소" : "준비";
            }
            networkStatus.text = P2PNetworkSessionManager.Instance?.IsRunning == true ? "NGO: 연결 실행 중" : "NGO: 로비 시작 대기";
        }

        private static Image CreateImage(Transform parent, string name, Color color, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false); var image = go.GetComponent<Image>(); image.color = color;
            SetRect(go.GetComponent<RectTransform>(), min, max); return image;
        }

        private static GameObject CreateView(Transform parent, string name)
        {
            var view = new GameObject(name, typeof(RectTransform));
            view.transform.SetParent(parent, false);
            SetRect(view.GetComponent<RectTransform>(), new Vector2(0.02f, 0.04f), new Vector2(0.98f, 0.80f));
            return view;
        }

        private static TMP_Text CreateText(Transform parent, string name, string value, float size, Color color, Vector2 min, Vector2 max, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false); var text = go.GetComponent<TMP_Text>(); text.text = value; text.fontSize = size; text.color = color; text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal; SetRect(go.GetComponent<RectTransform>(), min, max); return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 min, Vector2 max, Color accent)
        {
            Image image = CreateImage(parent, name, Navy, min, max); var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var outline = image.gameObject.AddComponent<Outline>(); outline.effectColor = accent; outline.effectDistance = new Vector2(2, -2);
            CreateText(image.transform, "Label", label, 15, TextColor, new Vector2(0.03f, 0.08f), new Vector2(0.97f, 0.92f), TextAlignmentOptions.Center);
            return button;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        }
    }
}
