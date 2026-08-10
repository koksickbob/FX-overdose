using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FXOverdose.DatingSim.Core;
using FXOverdose.DatingSim.LLM;
using FXOverdose.DatingSim.WorldMap;
using FXOverdose.DatingSim.YomiRoom;

namespace FXOverdose.DatingSim.UI
{
    /// <summary>P2_05 조립 문서를 코드로 재현해 두 미연시 씬의 필수 UI와 매니저를 보장합니다.</summary>
    public static class DatingSimSceneBuilder
    {
        private static readonly Color32 Backdrop = new(5, 10, 22, 255);
        private static readonly Color32 Panel = new(10, 22, 39, 245);
        private static readonly Color32 PanelSoft = new(17, 31, 51, 238);
        private static readonly Color32 Border = new(35, 73, 99, 255);
        private static readonly Color32 Cyan = new(34, 211, 238, 255);
        private static readonly Color32 Pink = new(244, 114, 182, 255);
        private static readonly Color32 Purple = new(168, 85, 247, 255);
        private static readonly Color32 Gold = new(250, 204, 21, 255);
        private static readonly Color32 Text = new(226, 245, 250, 255);
        private static readonly Color32 Muted = new(125, 151, 168, 255);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => BuildForScene(scene);

        public static void BuildForScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            if (scene.name != "YomiRoomScene" && scene.name != "WorldMapScene") return;

            EnsureCamera(scene);
            EnsureEventSystem(scene);
            EnsureTimeManager(scene);

            if (scene.name == "YomiRoomScene") BuildYomiRoom(scene);
            else BuildWorldMap(scene);
        }

        private static void BuildYomiRoom(Scene scene)
        {
            if (FindInScene<YomiRoomManager>(scene) == null)
                CreateInScene<YomiRoomManager>(scene, "YomiRoomManager");
            if (FindInScene<DatingSimLLMController>(scene) == null)
                CreateInScene<DatingSimLLMController>(scene, "DatingSimLLMController");

            // P2_03: 버튼 메뉴 대신 실제로 걸어 다니는 탑다운 방 프로토타입을 우선 사용합니다.
            YomiRoomTopDownPrototype.Build(scene);
            return;

#pragma warning disable CS0162
            if (FindObject(scene, "Canvas_YomiRoom") != null) return;

            Canvas canvas = CreateCanvas(scene, "Canvas_YomiRoom");
            CreateImage(canvas.transform, "Backdrop", Backdrop, Vector2.zero, Vector2.one);
            CreateImage(canvas.transform, "WindowGlow", new Color32(17, 62, 82, 120),
                new Vector2(0.03f, 0.08f), new Vector2(0.70f, 0.91f));

            RectTransform header = CreatePanel(canvas.transform, "StatusHeader",
                new Vector2(0.025f, 0.865f), new Vector2(0.975f, 0.975f));
            TextMeshProUGUI stamina = CreateStatus(header, "StaminaText", "STAMINA  100 / 100", 0f, 0.25f, Cyan);
            TextMeshProUGUI slots = CreateStatus(header, "TimeSlotText", "TIME SLOT  5", 0.25f, 0.48f, Gold);
            TextMeshProUGUI affection = CreateStatus(header, "AffectionText", "AFFECTION  0", 0.48f, 0.72f, Pink);
            TextMeshProUGUI obsession = CreateStatus(header, "ObsessionText", "OBSESSION  0", 0.72f, 1f, Purple);

            RectTransform sceneCard = CreatePanel(canvas.transform, "RoomScene",
                new Vector2(0.035f, 0.18f), new Vector2(0.70f, 0.84f));
            TMP_Text roomTitle = CreateText(sceneCard, "RoomTitle", "YOMI'S ROOM", 22f,
                new Vector2(0.05f, 0.88f), new Vector2(0.50f, 0.97f), TextAlignmentOptions.Left);
            roomTitle.color = Cyan;
            roomTitle.characterSpacing = 3f;
            TMP_Text yomiPlaceholder = CreateText(sceneCard, "YomiArtSlot", "YOMI", 70f,
                new Vector2(0.12f, 0.18f), new Vector2(0.88f, 0.82f), TextAlignmentOptions.Center);
            yomiPlaceholder.color = new Color32(61, 88, 111, 150);
            TMP_Text artGuide = CreateText(sceneCard, "ArtGuide", "CHARACTER ART SLOT", 14f,
                new Vector2(0.30f, 0.08f), new Vector2(0.70f, 0.15f), TextAlignmentOptions.Center);
            artGuide.color = Muted;

            RectTransform actions = CreatePanel(canvas.transform, "ActionPanel",
                new Vector2(0.72f, 0.18f), new Vector2(0.965f, 0.84f));
            CreateText(actions, "ActionTitle", "TODAY'S ACTION", 20f,
                new Vector2(0.08f, 0.87f), new Vector2(0.92f, 0.97f), TextAlignmentOptions.Center).color = Cyan;
            Button freeChat = CreateButton(actions, "FreeChatButton", "자유대화", 0.68f, Pink);
            Button rest = CreateButton(actions, "RestButton", "휴식", 0.51f, Cyan);
            Button worldMap = CreateButton(actions, "WorldMapButton", "월드맵 외출", 0.34f, Purple);
            Button trading = CreateButton(actions, "TradingButton", "트레이딩 시작", 0.17f, Gold);

            RectTransform feedbackPanel = CreatePanel(canvas.transform, "FeedbackPanel",
                new Vector2(0.035f, 0.045f), new Vector2(0.965f, 0.15f));
            TextMeshProUGUI state = CreateText(feedbackPanel, "RoomStateText", "요미가 당신을 기다리고 있어요.", 22f,
                new Vector2(0.035f, 0.1f), new Vector2(0.965f, 0.9f), TextAlignmentOptions.Center);

            YomiRoomUIController ui = canvas.gameObject.AddComponent<YomiRoomUIController>();
            ui.Configure(freeChat, rest, worldMap, trading, stamina, slots, affection, obsession, state);
#pragma warning restore CS0162
        }

        private static void BuildWorldMap(Scene scene)
        {
            WorldMapManager manager = FindInScene<WorldMapManager>(scene);
            if (manager == null) manager = CreateInScene<WorldMapManager>(scene, "WorldMapManager");
            EnsureDefaultMapData(manager);
            if (FindObject(scene, "Canvas_WorldMap") != null) return;

            Canvas canvas = CreateCanvas(scene, "Canvas_WorldMap");
            CreateImage(canvas.transform, "Backdrop", new Color32(5, 13, 25, 255), Vector2.zero, Vector2.one);

            RectTransform header = CreatePanel(canvas.transform, "ResourceHeader",
                new Vector2(0.025f, 0.865f), new Vector2(0.975f, 0.975f));
            TextMeshProUGUI stamina = CreateStatus(header, "StaminaText", "STAMINA  100 / 100", 0f, 0.32f, Cyan);
            TextMeshProUGUI slots = CreateStatus(header, "TimeSlotText", "TIME SLOT  5", 0.32f, 0.64f, Gold);
            TextMeshProUGUI balance = CreateStatus(header, "BalanceText", "BALANCE  $0", 0.64f, 1f, Pink);

            TMP_Text title = CreateText(canvas.transform, "MapTitle", "CITY MAP", 34f,
                new Vector2(0.04f, 0.76f), new Vector2(0.42f, 0.84f), TextAlignmentOptions.Left);
            title.color = Cyan;
            title.characterSpacing = 5f;
            TMP_Text subtitle = CreateText(canvas.transform, "MapSubtitle", "남은 시간과 체력을 확인하고 목적지를 선택하세요.", 17f,
                new Vector2(0.04f, 0.71f), new Vector2(0.62f, 0.76f), TextAlignmentOptions.Left);
            subtitle.color = Muted;

            RectTransform jobCard = CreatePanel(canvas.transform, "JobLocationCard",
                new Vector2(0.07f, 0.27f), new Vector2(0.46f, 0.68f));
            CreateLocationCopy(jobCard, "PART-TIME JOB", "편의점 야간 알바", "체력 -20  ·  시간 슬롯 -2\n성공 보상  $1,200", Cyan);
            Button job = CreateWideButton(jobCard, "JobButton", "알바 시작", Cyan);

            RectTransform dateCard = CreatePanel(canvas.transform, "DateLocationCard",
                new Vector2(0.54f, 0.27f), new Vector2(0.93f, 0.68f));
            CreateLocationCopy(dateCard, "DATE COURSE", "야경 카페 데이트", "체력 -10  ·  시간 슬롯 -1\n비용  $500", Pink);
            Button date = CreateWideButton(dateCard, "DateButton", "데이트 시작", Pink);

            RectTransform feedbackPanel = CreatePanel(canvas.transform, "FeedbackPanel",
                new Vector2(0.07f, 0.075f), new Vector2(0.78f, 0.20f));
            TextMeshProUGUI feedback = CreateText(feedbackPanel, "FeedbackText", "행동을 선택해 주세요.", 20f,
                new Vector2(0.04f, 0.1f), new Vector2(0.96f, 0.9f), TextAlignmentOptions.Center);
            Button room = CreateStandaloneButton(canvas.transform, "RoomButton", "요미의 방으로", new Vector2(0.80f, 0.075f), new Vector2(0.93f, 0.20f), Purple);

            WorldMapUIController ui = canvas.gameObject.AddComponent<WorldMapUIController>();
            ui.Configure(stamina, slots, balance, job, date, room, feedback);
        }

        private static void EnsureDefaultMapData(WorldMapManager manager)
        {
            if (manager.availableJobs.Count == 0)
            {
                manager.availableJobs.Add(new PartTimeJobData
                {
                    jobName = "편의점 야간 알바", staminaCost = 20, rewardAmount = 1200f
                });
            }
            if (manager.availableDateCourses.Count == 0)
            {
                manager.availableDateCourses.Add(new DateCourseData
                {
                    courseName = "야경 카페 데이트", timeSlotCost = 1, staminaCost = 10, moneyCost = 500f
                });
            }
        }

        private static void CreateLocationCopy(RectTransform card, string tag, string title, string details, Color accent)
        {
            TMP_Text tagText = CreateText(card, "Tag", tag, 14f, new Vector2(0.07f, 0.78f), new Vector2(0.93f, 0.92f), TextAlignmentOptions.Left);
            tagText.color = accent;
            tagText.characterSpacing = 3f;
            CreateText(card, "Title", title, 28f, new Vector2(0.07f, 0.55f), new Vector2(0.93f, 0.78f), TextAlignmentOptions.Left).fontStyle = FontStyles.Bold;
            TMP_Text detail = CreateText(card, "Details", details, 18f, new Vector2(0.07f, 0.27f), new Vector2(0.93f, 0.54f), TextAlignmentOptions.TopLeft);
            detail.color = Muted;
        }

        private static void EnsureTimeManager(Scene scene)
        {
            if (DatingTimeManager.Instance != null || Object.FindAnyObjectByType<DatingTimeManager>(FindObjectsInactive.Include) != null) return;
            CreateInScene<DatingTimeManager>(scene, "DatingTimeManager");
        }

        private static Canvas CreateCanvas(Scene scene, string name)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(go, scene);
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void EnsureCamera(Scene scene)
        {
            if (FindInScene<Camera>(scene) != null) return;
            GameObject go = new("Main Camera", typeof(Camera), typeof(AudioListener));
            SceneManager.MoveGameObjectToScene(go, scene);
            go.tag = "MainCamera";
            Camera camera = go.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Backdrop;
            camera.cullingMask = 0;
        }

        private static void EnsureEventSystem(Scene scene)
        {
            if (FindInScene<EventSystem>(scene) != null) return;
            GameObject go = new("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(go, scene);
        }

        private static T CreateInScene<T>(Scene scene, string name) where T : Component
        {
            GameObject go = new(name, typeof(T));
            SceneManager.MoveGameObjectToScene(go, scene);
            return go.GetComponent<T>();
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (T component in root.GetComponentsInChildren<T>(true))
                return component;
            return null;
        }

        private static GameObject FindObject(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child.gameObject;
            return null;
        }

        private static RectTransform CreatePanel(Transform parent, string name, Vector2 min, Vector2 max)
        {
            Image image = CreateImage(parent, name, Panel, min, max);
            Outline outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = Border;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
            image.raycastTarget = false;
            return image.rectTransform;
        }

        private static TextMeshProUGUI CreateStatus(Transform parent, string name, string value, float minX, float maxX, Color color)
        {
            TextMeshProUGUI text = CreateText(parent, name, value, 20f, new Vector2(minX + 0.025f, 0.12f), new Vector2(maxX - 0.025f, 0.88f), TextAlignmentOptions.Center);
            text.color = color;
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 1.5f;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, float centerY, Color accent)
        {
            return CreateStandaloneButton(parent, name, label, new Vector2(0.08f, centerY - 0.065f), new Vector2(0.92f, centerY + 0.065f), accent);
        }

        private static Button CreateWideButton(Transform parent, string name, string label, Color accent)
        {
            return CreateStandaloneButton(parent, name, label, new Vector2(0.07f, 0.07f), new Vector2(0.93f, 0.23f), accent);
        }

        private static Button CreateStandaloneButton(Transform parent, string name, string label, Vector2 min, Vector2 max, Color accent)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline));
            go.transform.SetParent(parent, false);
            SetRect(go.GetComponent<RectTransform>(), min, max);
            Image image = go.GetComponent<Image>();
            image.color = PanelSoft;
            Outline outline = go.GetComponent<Outline>();
            outline.effectColor = accent;
            outline.effectDistance = new Vector2(2f, -2f);
            Button button = go.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.72f, 0.82f, 0.9f, 1f);
            colors.disabledColor = new Color(0.38f, 0.42f, 0.48f, 0.6f);
            button.colors = colors;
            TMP_Text text = CreateText(go.transform, "Label", label, 22f, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f), TextAlignmentOptions.Center);
            text.fontStyle = FontStyles.Bold;
            text.color = Text;
            return button;
        }

        private static Image CreateImage(Transform parent, string name, Color color, Vector2 min, Vector2 max)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            SetRect(image.rectTransform, min, max);
            return image;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string value, float size, Vector2 min, Vector2 max, TextAlignmentOptions alignment)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = value;
            text.fontSize = size;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Max(12f, size * 0.68f);
            text.fontSizeMax = size;
            text.color = Text;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            SetRect(text.rectTransform, min, max);
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
