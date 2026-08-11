using System;
using System.Collections.Generic;
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
        private TMP_Text steamStatus;
        private TMP_Text lobbyStatus;
        private TMP_Text settingsText;
        private TMP_Text membersText;
        private TMP_Text networkStatus;
        private readonly List<Button> searchButtons = new();
        private int leverageIndex = 4;
        private int marginIndex = 2;
        private readonly int[] leveragePresets = { 1, 2, 5, 10, 20, 50, 100 };
        private readonly double[] marginPresets = { 0.1, 0.25, 0.5, 0.75, 1.0 };
        private bool localReady;

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
            });

            window = CreateImage(transform, "P2PLobbyPanel", new Color32(2, 8, 18, 235), Vector2.zero, Vector2.one).gameObject;
            Image modal = CreateImage(window.transform, "Window", Panel, new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.94f));
            modal.gameObject.AddComponent<Outline>().effectColor = Cyan;
            CreateText(modal.transform, "Title", "STEAM P2P LOBBY", 32, TextColor, new Vector2(0.04f, 0.91f), new Vector2(0.96f, 0.98f), TextAlignmentOptions.Left).fontStyle = FontStyles.Bold;
            steamStatus = CreateText(modal.transform, "SteamStatus", "STEAM 확인 중", 15, Muted, new Vector2(0.04f, 0.855f), new Vector2(0.96f, 0.91f), TextAlignmentOptions.Left);
            lobbyStatus = CreateText(modal.transform, "LobbyStatus", "로비 없음", 16, Cyan, new Vector2(0.04f, 0.80f), new Vector2(0.96f, 0.855f), TextAlignmentOptions.Left);

            CreateButton(modal.transform, "Public", "공개 방 만들기", new Vector2(0.04f, 0.71f), new Vector2(0.21f, 0.78f), Cyan).onClick.AddListener(() => CreateLobby(SteamLobbyVisibility.Public));
            CreateButton(modal.transform, "Friends", "친구 방 만들기", new Vector2(0.225f, 0.71f), new Vector2(0.395f, 0.78f), Cyan).onClick.AddListener(() => CreateLobby(SteamLobbyVisibility.FriendsOnly));
            CreateButton(modal.transform, "Private", "비공개 방", new Vector2(0.41f, 0.71f), new Vector2(0.56f, 0.78f), Cyan).onClick.AddListener(() => CreateLobby(SteamLobbyVisibility.Private));
            CreateButton(modal.transform, "Search", "공개 방 검색", new Vector2(0.575f, 0.71f), new Vector2(0.73f, 0.78f), Cyan).onClick.AddListener(Search);
            CreateButton(modal.transform, "Invite", "친구 초대", new Vector2(0.745f, 0.71f), new Vector2(0.86f, 0.78f), Pink).onClick.AddListener(() => SteamLobbyManager.Instance?.OpenInviteOverlay());
            CreateButton(modal.transform, "Close", "X", new Vector2(0.91f, 0.91f), new Vector2(0.97f, 0.98f), Pink).onClick.AddListener(() => window.SetActive(false));

            settingsText = CreateText(modal.transform, "Settings", "", 18, TextColor, new Vector2(0.04f, 0.62f), new Vector2(0.45f, 0.69f), TextAlignmentOptions.Left);
            CreateButton(modal.transform, "LevDown", "레버리지 -", new Vector2(0.04f, 0.55f), new Vector2(0.16f, 0.615f), Cyan).onClick.AddListener(() => ChangeLeverage(-1));
            CreateButton(modal.transform, "LevUp", "레버리지 +", new Vector2(0.17f, 0.55f), new Vector2(0.29f, 0.615f), Cyan).onClick.AddListener(() => ChangeLeverage(1));
            CreateButton(modal.transform, "MarginDown", "마진 -", new Vector2(0.30f, 0.55f), new Vector2(0.40f, 0.615f), Cyan).onClick.AddListener(() => ChangeMargin(-1));
            CreateButton(modal.transform, "MarginUp", "마진 +", new Vector2(0.41f, 0.55f), new Vector2(0.51f, 0.615f), Cyan).onClick.AddListener(() => ChangeMargin(1));

            membersText = CreateText(modal.transform, "Members", "참가자 없음", 17, TextColor, new Vector2(0.04f, 0.20f), new Vector2(0.51f, 0.53f), TextAlignmentOptions.TopLeft);
            CreateText(modal.transform, "SearchTitle", "검색 결과", 18, Cyan, new Vector2(0.55f, 0.62f), new Vector2(0.90f, 0.68f), TextAlignmentOptions.Left);
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                Button result = CreateButton(modal.transform, $"SearchResult{i}", "-", new Vector2(0.55f, 0.53f - i * 0.08f), new Vector2(0.90f, 0.595f - i * 0.08f), Cyan);
                result.gameObject.SetActive(false);
                result.onClick.AddListener(() => JoinSearchResult(index));
                searchButtons.Add(result);
            }

            CreateButton(modal.transform, "Ready", "준비 / 취소", new Vector2(0.04f, 0.10f), new Vector2(0.19f, 0.175f), Cyan).onClick.AddListener(ToggleReady);
            CreateButton(modal.transform, "Start", "경기 시작", new Vector2(0.205f, 0.10f), new Vector2(0.35f, 0.175f), Pink).onClick.AddListener(() => SteamLobbyManager.Instance?.TryStartMatch());
            CreateButton(modal.transform, "Leave", "로비 나가기", new Vector2(0.365f, 0.10f), new Vector2(0.51f, 0.175f), Muted).onClick.AddListener(() => SteamLobbyManager.Instance?.LeaveLobby());
            networkStatus = CreateText(modal.transform, "NetworkStatus", "NGO: 대기", 16, Muted, new Vector2(0.55f, 0.10f), new Vector2(0.90f, 0.175f), TextAlignmentOptions.Left);
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
            }
            networkStatus.text = P2PNetworkSessionManager.Instance?.IsRunning == true ? "NGO: 연결 실행 중" : "NGO: 로비 시작 대기";
        }

        private static Image CreateImage(Transform parent, string name, Color color, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false); var image = go.GetComponent<Image>(); image.color = color;
            SetRect(go.GetComponent<RectTransform>(), min, max); return image;
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
