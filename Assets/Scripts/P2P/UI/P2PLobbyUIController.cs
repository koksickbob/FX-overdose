using System;
using System.Collections.Generic;
using FXOverdose.P2P.Core;
using FXOverdose.P2P.Infrastructure;
using FXOverdose.P2P.Lobby;
using FXOverdose.P2P.Steam;
using TMPro;
using Steamworks;
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
        private TMP_Text roomTitle;
        private TMP_Text settingsText;
        private TMP_Text marginSettingsText;
        private TMP_Text membersText;
        private TMP_Text networkStatus;
        private Button readyButton;
        private Button startButton;
        private readonly List<Button> ruleButtons = new();
        private readonly List<Button> searchButtons = new();
        private readonly List<RawImage> memberAvatars = new();
        private readonly Dictionary<ulong, Texture2D> avatarTextures = new();
        private Callback<AvatarImageLoaded_t> avatarLoadedCallback;
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
            if (SteamRuntimeBootstrap.CanUseP2P && avatarLoadedCallback == null)
                avatarLoadedCallback = Callback<AvatarImageLoaded_t>.Create(OnAvatarLoaded);
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
            avatarLoadedCallback?.Dispose(); avatarLoadedCallback = null;
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
            ArrangeTitleMenu();
            // 메뉴 버튼은 타이틀 기본 캔버스에 두어 NEW GAME의 모드 선택 모달보다 뒤에 렌더링합니다.
            Transform titleCanvas = transform.parent != null ? transform.parent : transform;
            Button open = CreateTitleMenuButton(titleCanvas, "Btn_P2P", "MULTI PLAY", "Steam 대전", 2);
            Transform gameModePanel = titleCanvas.Find("GameModePanel");
            if (gameModePanel != null) open.transform.SetSiblingIndex(gameModePanel.GetSiblingIndex());
            open.onClick.AddListener(() =>
            {
                transform.SetAsLastSibling();
                window.SetActive(true);
                window.transform.SetAsLastSibling();
                RefreshAll();
                if (SteamLobbyManager.Instance?.CurrentLobby == null && SteamRuntimeBootstrap.CanUseP2P)
                    Search();
            });

            // 타이틀 씬의 픽셀 방 배경은 그대로 보이고 UI 패널만 얹히도록 약하게 어둡힙니다.
            window = CreateImage(transform, "P2PLobbyPanel", new Color32(2, 8, 18, 255), Vector2.zero, Vector2.one).gameObject;
            Transform modal = window.transform;
            TMP_Text logo = CreateText(modal, "Title", "FX\nOVERDOSE", 60, TextColor, new Vector2(0.052f, 0.79f), new Vector2(0.34f, 0.96f), TextAlignmentOptions.BottomLeft);
            logo.fontStyle = FontStyles.Bold; logo.characterSpacing = 2f; logo.lineSpacing = -18f;
            steamStatus = CreateText(modal, "SteamStatus", "STEAM 확인 중", 14, Muted, new Vector2(0.055f, 0.705f), new Vector2(0.72f, 0.75f), TextAlignmentOptions.Left);
            lobbyStatus = CreateText(modal, "LobbyStatus", "로비 없음", 15, Cyan, new Vector2(0.055f, 0.665f), new Vector2(0.72f, 0.705f), TextAlignmentOptions.Left);

            browserView = CreateScreenView(modal, "LobbyBrowserView");
            Image listPanel = CreatePanel(browserView.transform, "PublicLobbyPanel", new Vector2(0.052f, 0.10f), new Vector2(0.61f, 0.63f), Cyan);
            CreateText(listPanel.transform, "BrowserTitle", "전체 로비", 27, TextColor, new Vector2(0.04f, 0.84f), new Vector2(0.50f, 0.96f), TextAlignmentOptions.Left).fontStyle = FontStyles.Bold;
            CreateText(listPanel.transform, "SearchTitle", "공개 로비 목록", 15, Muted, new Vector2(0.04f, 0.76f), new Vector2(0.50f, 0.84f), TextAlignmentOptions.Left);
            CreateButton(listPanel.transform, "Refresh", "↻  목록 새로고침", new Vector2(0.68f, 0.82f), new Vector2(0.95f, 0.94f), Cyan, 15).onClick.AddListener(Search);
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                Button result = CreateButton(listPanel.transform, $"SearchResult{i}", "-", new Vector2(0.04f, 0.61f - i * 0.145f), new Vector2(0.96f, 0.72f - i * 0.145f), new Color32(50,78,104,255), 16);
                result.gameObject.SetActive(false);
                result.onClick.AddListener(() => JoinSearchResult(index));
                searchButtons.Add(result);
            }
            Image createPanel = CreatePanel(browserView.transform, "CreateLobbyPanel", new Vector2(0.64f, 0.20f), new Vector2(0.95f, 0.63f), Pink);
            CreateText(createPanel.transform, "CreateTitle", "로비 생성", 27, TextColor, new Vector2(0.07f, 0.81f), new Vector2(0.93f, 0.94f), TextAlignmentOptions.Left).fontStyle = FontStyles.Bold;
            CreateButton(createPanel.transform, "Public", "PUBLIC LOBBY\n공개 로비 생성", new Vector2(0.07f, 0.32f), new Vector2(0.93f, 0.53f), Cyan, 17).onClick.AddListener(() => CreateLobby(SteamLobbyVisibility.Public));
            CreateButton(createPanel.transform, "Friends", "FRIENDS LOBBY\n친구 로비 생성", new Vector2(0.07f, 0.07f), new Vector2(0.93f, 0.28f), Pink, 17).onClick.AddListener(() => CreateLobby(SteamLobbyVisibility.InviteOnly));

            roomView = CreateScreenView(modal, "LobbyRoomView");
            Image roomHeader = CreatePanel(roomView.transform, "RoomHeader", new Vector2(0.052f, 0.56f), new Vector2(0.36f, 0.65f), Cyan);
            roomTitle = CreateText(roomHeader.transform, "RoomTitle", "Steam 로비", 24, TextColor, new Vector2(0.05f, 0.43f), new Vector2(0.95f, 0.92f), TextAlignmentOptions.Left);
            roomTitle.fontStyle = FontStyles.Bold;
            CreateText(roomHeader.transform, "RoomType", "● STEAM CONNECTED", 13, new Color32(74,201,112,255), new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.42f), TextAlignmentOptions.Left);
            Image membersPanel = CreatePanel(roomView.transform, "MembersPanel", new Vector2(0.052f, 0.20f), new Vector2(0.42f, 0.54f), Cyan);
            CreateText(membersPanel.transform, "MembersTitle", "참가자 목록", 24, TextColor, new Vector2(0.05f, 0.84f), new Vector2(0.95f, 0.96f), TextAlignmentOptions.Left).fontStyle = FontStyles.Bold;
            membersText = CreateText(membersPanel.transform, "Members", "참가자 없음", 17, TextColor, new Vector2(0.17f, 0.06f), new Vector2(0.95f, 0.81f), TextAlignmentOptions.TopLeft);
            membersText.richText = true; membersText.lineSpacing = 42f;
            for (int i = 0; i < 4; i++)
            {
                RawImage avatar = CreateRawImage(membersPanel.transform, $"SteamAvatar{i}", new Color32(18,48,72,255),
                    new Vector2(0.055f, 0.61f - i * 0.185f), new Vector2(0.145f, 0.76f - i * 0.185f));
                avatar.gameObject.AddComponent<Outline>().effectColor = new Color32(50,78,104,255);
                avatar.gameObject.SetActive(false); memberAvatars.Add(avatar);
            }
            Image settingsPanel = CreatePanel(roomView.transform, "SettingsPanel", new Vector2(0.45f, 0.20f), new Vector2(0.77f, 0.54f), Cyan);
            CreateText(settingsPanel.transform, "SettingsTitle", "경기 설정", 24, TextColor, new Vector2(0.06f, 0.82f), new Vector2(0.94f, 0.95f), TextAlignmentOptions.Left).fontStyle = FontStyles.Bold;
            settingsText = CreateText(settingsPanel.transform, "LeverageSetting", "", 22, TextColor, new Vector2(0.23f, 0.45f), new Vector2(0.77f, 0.59f), TextAlignmentOptions.Center);
            settingsText.fontStyle = FontStyles.Bold;
            marginSettingsText = CreateText(settingsPanel.transform, "MarginSetting", "", 22, TextColor, new Vector2(0.23f, 0.25f), new Vector2(0.77f, 0.39f), TextAlignmentOptions.Center);
            marginSettingsText.fontStyle = FontStyles.Bold;
            ruleButtons.Add(CreateButton(settingsPanel.transform, "LevDown", "<", new Vector2(0.06f, 0.45f), new Vector2(0.22f, 0.59f), Cyan, 20));
            ruleButtons[0].onClick.AddListener(() => ChangeLeverage(-1));
            ruleButtons.Add(CreateButton(settingsPanel.transform, "LevUp", ">", new Vector2(0.78f, 0.45f), new Vector2(0.94f, 0.59f), Cyan, 20));
            ruleButtons[1].onClick.AddListener(() => ChangeLeverage(1));
            ruleButtons.Add(CreateButton(settingsPanel.transform, "MarginDown", "<", new Vector2(0.06f, 0.25f), new Vector2(0.22f, 0.39f), Cyan, 20));
            ruleButtons[2].onClick.AddListener(() => ChangeMargin(-1));
            ruleButtons.Add(CreateButton(settingsPanel.transform, "MarginUp", ">", new Vector2(0.78f, 0.25f), new Vector2(0.94f, 0.39f), Cyan, 20));
            ruleButtons[3].onClick.AddListener(() => ChangeMargin(1));
            CreateText(settingsPanel.transform, "Rules", "1 DAY · MANUAL TRADING\n파산 또는 멘탈 0 탈락", 14, Muted, new Vector2(0.06f, 0.02f), new Vector2(0.94f, 0.21f), TextAlignmentOptions.Left);
            CreateButton(roomView.transform, "Invite", "친구 초대", new Vector2(0.80f, 0.43f), new Vector2(0.95f, 0.51f), Pink, 15).onClick.AddListener(() => SteamLobbyManager.Instance?.OpenInviteOverlay());
            readyButton = CreateButton(roomView.transform, "Ready", "준비", new Vector2(0.34f, 0.045f), new Vector2(0.54f, 0.135f), Cyan, 22);
            readyButton.onClick.AddListener(ToggleReady);
            startButton = CreateButton(roomView.transform, "Start", "경기 시작", new Vector2(0.56f, 0.045f), new Vector2(0.77f, 0.135f), Pink, 20);
            startButton.onClick.AddListener(() => SteamLobbyManager.Instance?.TryStartMatch());
            CreateButton(roomView.transform, "Leave", "나가기", new Vector2(0.052f, 0.045f), new Vector2(0.22f, 0.135f), Muted, 20).onClick.AddListener(() => SteamLobbyManager.Instance?.LeaveLobby());
            networkStatus = CreateText(roomView.transform, "NetworkStatus", "NGO: 대기", 14, Muted, new Vector2(0.80f, 0.30f), new Vector2(0.95f, 0.40f), TextAlignmentOptions.TopLeft);
            roomView.SetActive(false);
            window.SetActive(false);
        }

        private static void ArrangeTitleMenu()
        {
            string[] names = { "Btn_NewGame", "Btn_LoadGame", "Btn_Settings", "Btn_QuitGame" };
            int[] slots = { 0, 1, 3, 4 };
            for (int i = 0; i < names.Length; i++)
            {
                GameObject button = GameObject.Find(names[i]);
                if (button != null && button.TryGetComponent(out RectTransform rect)) SetTitleMenuRect(rect, slots[i]);
            }
        }

        private static Button CreateTitleMenuButton(Transform parent, string name, string title, string subtitle, int slot)
        {
            Image image = CreateImage(parent, name, Panel, Vector2.zero, Vector2.one);
            SetTitleMenuRect(image.rectTransform, slot);
            Button button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white; colors.highlightedColor = new Color(0.72f,0.95f,1f,1f);
            colors.pressedColor = new Color(0.35f,0.75f,0.85f,1f); colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.36f,0.42f,0.48f,0.55f); colors.fadeDuration = 0.08f; button.colors = colors;
            var outline = image.gameObject.AddComponent<Outline>(); outline.effectColor = new Color32(50,78,104,255); outline.effectDistance = new Vector2(2,-2);
            TMP_Text titleText = CreateText(image.transform, "Title", title, 30f, TextColor, new Vector2(0.06f,0.36f), new Vector2(0.94f,0.92f), TextAlignmentOptions.Left);
            titleText.fontStyle = FontStyles.Bold;
            CreateText(image.transform, "Subtitle", subtitle, 14f, Muted, new Vector2(0.06f,0.06f), new Vector2(0.94f,0.38f), TextAlignmentOptions.Left);
            CreateText(image.transform, "Arrow", ">", 30f, Muted, new Vector2(0.86f,0.15f), new Vector2(0.96f,0.85f), TextAlignmentOptions.Center);
            return button;
        }

        private static void SetTitleMenuRect(RectTransform rect, int slot)
        {
            float top = 0.56f - slot * 0.09f;
            SetRect(rect, new Vector2(0.075f, top - 0.07f), new Vector2(0.36f, top));
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

        private void OnSteamStatus(SteamRuntimeStatus status)
        {
            if (status.CanUseP2P && avatarLoadedCallback == null)
                avatarLoadedCallback = Callback<AvatarImageLoaded_t>.Create(OnAvatarLoaded);
            RefreshAll();
        }
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
                settingsText.text = $"최대 레버리지  <color=#06B6D4>{leveragePresets[leverageIndex]}x</color>";
                marginSettingsText.text = $"최대 마진  <color=#06B6D4>{marginPresets[marginIndex]:P0}</color>";
                membersText.text = "참가자 없음";
                RefreshMemberAvatars(null);
                localReady = false;
            }
            else
            {
                leverageIndex = Math.Max(0, Array.IndexOf(leveragePresets, lobby.Settings.MaximumLeverage));
                marginIndex = Math.Max(0, Array.IndexOf(marginPresets, lobby.Settings.MaximumMarginRatio));
                string ownerName = "Steam";
                for (int i = 0; i < lobby.Members.Count; i++)
                    if (lobby.Members[i].SteamId == lobby.OwnerSteamId) { ownerName = lobby.Members[i].PersonaName; break; }
                if (roomTitle != null) roomTitle.text = $"'{ownerName}'의 방";
                settingsText.text = $"최대 레버리지  <color=#06B6D4>{lobby.Settings.MaximumLeverage}x</color>";
                marginSettingsText.text = $"최대 마진  <color=#06B6D4>{lobby.Settings.MaximumMarginRatio:P0}</color>";
                var lines = new List<string> { $"<color=#7EA0B8>참가자 {lobby.Members.Count}/{lobby.Settings.MaximumPlayers}</color>" };
                localReady = false;
                for (int i = 0; i < lobby.Members.Count; i++)
                {
                    SteamLobbyMember member = lobby.Members[i];
                    bool ready = SteamLobbyRules.IsEffectivelyReady(member, lobby.RulesRevision);
                    string badge = member.IsHost ? "<color=#EAB308>♛ HOST</color>" : ready ? "<color=#4AC970>● 준비</color>" : "<color=#FF4872>○ 미준비</color>";
                    lines.Add($"<color=#7EA0B8>▣</color>  {member.PersonaName}     {badge}");
                    if (member.SteamId == SteamRuntimeBootstrap.LocalSteamId) localReady = ready;
                }
                membersText.text = string.Join("\n", lines);
                RefreshMemberAvatars(lobby);
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

        private static RawImage CreateRawImage(Transform parent, string name, Color color, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(parent, false); RawImage image = go.GetComponent<RawImage>(); image.color = color;
            SetRect(go.GetComponent<RectTransform>(), min, max); return image;
        }

        private void RefreshMemberAvatars(SteamLobbySnapshot lobby)
        {
            for (int i = 0; i < memberAvatars.Count; i++)
            {
                bool visible = lobby != null && i < lobby.Members.Count;
                memberAvatars[i].gameObject.SetActive(visible);
                if (!visible) continue;
                ulong steamId = lobby.Members[i].SteamId;
                memberAvatars[i].texture = avatarTextures.TryGetValue(steamId, out Texture2D cached) ? cached : null;
                memberAvatars[i].color = cached != null ? Color.white : new Color32(18,48,72,255);
                if (cached == null && SteamRuntimeBootstrap.CanUseP2P)
                {
                    int image = SteamFriends.GetMediumFriendAvatar(new CSteamID(steamId));
                    if (image > 0) LoadAvatar(steamId, image);
                }
            }
        }

        private void OnAvatarLoaded(AvatarImageLoaded_t result)
        {
            LoadAvatar(result.m_steamID.m_SteamID, result.m_iImage);
            RefreshMemberAvatars(SteamLobbyManager.Instance?.CurrentLobby);
        }

        private void LoadAvatar(ulong steamId, int imageId)
        {
            if (imageId <= 0 || avatarTextures.ContainsKey(steamId) || !SteamUtils.GetImageSize(imageId, out uint width, out uint height) || width == 0 || height == 0) return;
            int byteCount = checked((int)(width * height * 4u));
            byte[] rgba = new byte[byteCount];
            if (!SteamUtils.GetImageRGBA(imageId, rgba, rgba.Length)) return;
            FlipImageRows(rgba, (int)width, (int)height);
            var texture = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false, false) { name = $"SteamAvatar_{steamId}" };
            texture.LoadRawTextureData(rgba); texture.Apply(false, false); avatarTextures[steamId] = texture;
        }

        private static void FlipImageRows(byte[] rgba, int width, int height)
        {
            int stride = width * 4;
            byte[] row = new byte[stride];
            for (int y = 0; y < height / 2; y++)
            {
                int top = y * stride;
                int bottom = (height - 1 - y) * stride;
                Buffer.BlockCopy(rgba, top, row, 0, stride);
                Buffer.BlockCopy(rgba, bottom, rgba, top, stride);
                Buffer.BlockCopy(row, 0, rgba, bottom, stride);
            }
        }

        private static GameObject CreateView(Transform parent, string name)
        {
            var view = new GameObject(name, typeof(RectTransform));
            view.transform.SetParent(parent, false);
            SetRect(view.GetComponent<RectTransform>(), new Vector2(0.02f, 0.04f), new Vector2(0.98f, 0.80f));
            return view;
        }

        private static GameObject CreateScreenView(Transform parent, string name)
        {
            var view = new GameObject(name, typeof(RectTransform));
            view.transform.SetParent(parent, false);
            SetRect(view.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            return view;
        }

        private static Image CreatePanel(Transform parent, string name, Vector2 min, Vector2 max, Color accent)
        {
            Image panel = CreateImage(parent, name, Panel, min, max);
            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = accent;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return panel;
        }

        private static TMP_Text CreateText(Transform parent, string name, string value, float size, Color color, Vector2 min, Vector2 max, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false); var text = go.GetComponent<TMP_Text>(); text.text = value; text.fontSize = size; text.color = color; text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal; SetRect(go.GetComponent<RectTransform>(), min, max); return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 min, Vector2 max, Color accent, float fontSize = 15f)
        {
            Image image = CreateImage(parent, name, Navy, min, max); var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var outline = image.gameObject.AddComponent<Outline>(); outline.effectColor = accent; outline.effectDistance = new Vector2(2, -2);
            ColorBlock colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(0.72f, 0.95f, 1f); colors.pressedColor = new Color(0.55f, 0.78f, 0.85f); colors.disabledColor = new Color(0.35f, 0.42f, 0.48f, 0.55f); button.colors = colors;
            TMP_Text text = CreateText(image.transform, "Label", label, fontSize, TextColor, new Vector2(0.03f, 0.08f), new Vector2(0.97f, 0.92f), TextAlignmentOptions.Center);
            text.fontStyle = FontStyles.Bold;
            return button;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        }

        private void OnDestroy()
        {
            foreach (Texture2D texture in avatarTextures.Values) if (texture != null) Destroy(texture);
            avatarTextures.Clear();
        }
    }
}
