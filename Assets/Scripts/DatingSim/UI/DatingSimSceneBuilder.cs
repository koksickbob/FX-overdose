using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FXOverdose.DatingSim.Core;
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
            if (scene.name != "YomiRoomScene" && scene.name != "WorldMapScene" && scene.name != "ConvenienceStoreScene") return;

            EnsureCamera(scene);
            EnsureEventSystem(scene);
            EnsureTimeManager(scene);

            if (scene.name == "ConvenienceStoreScene") ConvenienceStorePrototype.Build(scene);
            else if (scene.name == "YomiRoomScene") BuildYomiRoom(scene);
            else BuildWorldMap(scene);
        }

        private static void BuildYomiRoom(Scene scene)
        {
            if (FindInScene<YomiRoomManager>(scene) == null)
                CreateInScene<YomiRoomManager>(scene, "YomiRoomManager");

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
            if (FindObject(scene, "Canvas_WorldMap_Runtime") != null) return;
            GameObject oldCanvas = FindObject(scene, "Canvas_WorldMap");
            if (oldCanvas != null)
            {
                oldCanvas.name = "Canvas_WorldMap_Legacy";
                oldCanvas.SetActive(false);
                Object.Destroy(oldCanvas);
            }

            Canvas canvas = CreateCanvas(scene, "Canvas_WorldMap_Runtime");
            Image map = CreateImage(canvas.transform, "RegionMapBackground", Color.white, Vector2.zero, Vector2.one);
            map.sprite = LoadResourceSprite("DatingSim/WorldMap/UI/SeoulMapBackground");
            map.preserveAspect = false;
            CreateImage(canvas.transform, "MapTint", new Color32(2, 7, 18, 42), Vector2.zero, Vector2.one).raycastTarget = false;

            Button previousRegion = CreateStandaloneButton(canvas.transform, "PreviousRegionButton", "<",
                new Vector2(0.012f, 0.46f), new Vector2(0.068f, 0.60f), Cyan);
            Button nextRegion = CreateStandaloneButton(canvas.transform, "NextRegionButton", ">",
                new Vector2(0.932f, 0.46f), new Vector2(0.988f, 0.60f), Cyan);
            TMP_Text previousLabel = previousRegion.transform.Find("Label").GetComponent<TMP_Text>();
            TMP_Text nextLabel = nextRegion.transform.Find("Label").GetComponent<TMP_Text>();
            previousLabel.fontSize = nextLabel.fontSize = 42f;
            previousLabel.fontStyle = nextLabel.fontStyle = FontStyles.Bold;

            RectTransform top = CreatePanel(canvas.transform, "WorldMapHeader", new Vector2(0f, 0.87f), Vector2.one);
            float[] x = { 0.01f, 0.112f, 0.214f, 0.316f };
            string[] initial = { "체력  100/100", "남은 시간  5/5", "호감도  0%", "보유 자산  ₩0" };
            Color[] statusColors = { Cyan, Gold, Pink, new Color32(134, 239, 172, 255) };
            string[] statusIcons = { "♥", "◷", "♡", "₩" };
            TextMeshProUGUI[] statusTexts = new TextMeshProUGUI[4];
            Image staminaFill = null;
            Image affectionFill = null;
            Image[] timePips = new Image[5];
            for (int i = 0; i < 4; i++)
            {
                RectTransform card = CreatePanel(top, $"StatusCard_{i}", new Vector2(x[i], 0.14f), new Vector2(x[i] + 0.094f, 0.88f));
                TextMeshProUGUI icon = CreateText(card, "Icon", statusIcons[i], 21f, new Vector2(0.04f, 0.42f), new Vector2(0.25f, 0.9f), TextAlignmentOptions.Center);
                icon.color = statusColors[i];
                statusTexts[i] = CreateText(card, "Value", initial[i], 17f, new Vector2(0.23f, 0.38f), new Vector2(0.96f, 0.92f), TextAlignmentOptions.Center);
                statusTexts[i].color = statusColors[i];
                statusTexts[i].fontStyle = FontStyles.Bold;
                if (i == 0 || i == 2)
                {
                    Image track = CreateImage(card, "Track", new Color32(27, 45, 61, 255), new Vector2(0.08f, 0.13f), new Vector2(0.92f, 0.25f));
                    Image fill = CreateImage(track.transform, "Fill", statusColors[i], Vector2.zero, Vector2.one);
                    fill.type = Image.Type.Filled;
                    fill.fillMethod = Image.FillMethod.Horizontal;
                    fill.fillOrigin = 0;
                    if (i == 0) staminaFill = fill;
                    else affectionFill = fill;
                }
                else if (i == 1)
                {
                    for (int pip = 0; pip < 5; pip++)
                    {
                        float pipX = 0.08f + pip * 0.175f;
                        timePips[pip] = CreateImage(card, $"Pip_{pip}", statusColors[i], new Vector2(pipX, 0.12f), new Vector2(pipX + 0.12f, 0.25f));
                    }
                }
            }

            TMP_Text title = CreateText(top, "MapTitle", "SEOUL CITY MAP", 38f, new Vector2(0.43f, 0.42f), new Vector2(0.88f, 0.94f), TextAlignmentOptions.Center);
            title.color = Cyan;
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 4f;
            TMP_Text subtitle = CreateText(top, "MapSubtitle", "오늘 어디로 갈까요?", 17f, new Vector2(0.48f, 0.08f), new Vector2(0.83f, 0.42f), TextAlignmentOptions.Center);
            subtitle.color = new Color32(104, 173, 224, 255);
            BuildWorldMapSettingsButton(top);

            RectTransform pinLayer = new GameObject("LocationPins", typeof(RectTransform)).GetComponent<RectTransform>();
            pinLayer.SetParent(canvas.transform, false);
            SetRect(pinLayer, new Vector2(0f, 0.25f), new Vector2(1f, 0.87f));
            Button room = CreateMapPin(pinLayer, "RoomPin", "요미의 방", "DatingSim/WorldMap/UI/PinHome", new Vector2(0.35f, 0.56f), Cyan);
            Button job = CreateMapPin(pinLayer, "JobPin", "편의점 알바", "DatingSim/WorldMap/UI/PinWork", new Vector2(0.62f, 0.58f), Gold);
            Button date = CreateMapPin(pinLayer, "DatePin", "한강공원 데이트", "DatingSim/WorldMap/UI/PinDate", new Vector2(0.49f, 0.25f), Pink);
            Button arcade = CreateMapPin(pinLayer, "ArcadePin", "홍대 오락실", "DatingSim/WorldMap/UI/PinLeisure", new Vector2(0.17f, 0.64f), Purple);
            Button cafe = CreateMapPin(pinLayer, "CafePin", "성수 카페", "DatingSim/WorldMap/UI/PinDate", new Vector2(0.82f, 0.52f), Pink);
            RectTransform[] routeDots = CreateDottedRoute(pinLayer);

            RectTransform filters = CreatePanel(canvas.transform, "MapFilters", new Vector2(0.015f, 0.235f), new Vector2(0.34f, 0.3f));
            string[] filterLabels = { "전체", "알바", "데이트", "휴식" };
            Color[] filterColors = { Cyan, Gold, Pink, Purple };
            Button[] filterButtons = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                float min = 0.015f + i * 0.246f;
                filterButtons[i] = CreateStandaloneButton(filters, $"Filter_{i}", filterLabels[i], new Vector2(min, 0.12f), new Vector2(min + 0.23f, 0.88f), filterColors[i]);
            }
            filters.gameObject.SetActive(false);

            RectTransform detail = CreatePanel(canvas.transform, "LocationDetail", new Vector2(0.008f, 0.015f), new Vector2(0.992f, 0.225f));
            Image thumbnail = CreateImage(detail, "Thumbnail", Color.white, new Vector2(0.025f, 0.12f), new Vector2(0.245f, 0.88f));
            thumbnail.sprite = LoadResourceSprite("DatingSim/YomiRoom/Morning/RoomLeft");
            thumbnail.preserveAspect = true;
            TextMeshProUGUI detailTitle = CreateText(detail, "LocationTitle", "요미의 방", 30f, new Vector2(0.28f, 0.57f), new Vector2(0.72f, 0.88f), TextAlignmentOptions.Left);
            detailTitle.color = Cyan;
            detailTitle.fontStyle = FontStyles.Bold;
            TextMeshProUGUI detailMeta = CreateText(detail, "LocationMeta", "현재 위치  ·  비용 없음", 18f, new Vector2(0.28f, 0.35f), new Vector2(0.72f, 0.58f), TextAlignmentOptions.Left);
            detailMeta.color = Text;
            TextMeshProUGUI detailDescription = CreateText(detail, "LocationDescription", "휴식을 취하고 다음 일정을 계획할 수 있습니다.", 16f, new Vector2(0.28f, 0.12f), new Vector2(0.72f, 0.35f), TextAlignmentOptions.Left);
            detailDescription.color = Muted;
            Button action = CreateStandaloneButton(detail, "PrimaryAction", "돌아가기", new Vector2(0.77f, 0.24f), new Vector2(0.965f, 0.76f), Cyan);
            TextMeshProUGUI actionLabel = action.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI feedback = CreateText(detail, "FeedbackText", string.Empty, 14f, new Vector2(0.73f, 0.02f), new Vector2(0.98f, 0.2f), TextAlignmentOptions.Center);
            feedback.color = Pink;

            WorldMapUIController ui = canvas.gameObject.AddComponent<WorldMapUIController>();
            ui.ConfigureMapUI(statusTexts[0], statusTexts[1], statusTexts[3], statusTexts[2], room, job, date, arcade, cafe,
                action, filterButtons, new[] { room.gameObject, job.gameObject, date.gameObject, arcade.gameObject, cafe.gameObject },
                detailTitle, detailMeta, detailDescription, actionLabel, feedback, staminaFill, affectionFill, timePips,
                routeDots, new[] { room.GetComponent<RectTransform>(), job.GetComponent<RectTransform>(), date.GetComponent<RectTransform>(),
                    arcade.GetComponent<RectTransform>(), cafe.GetComponent<RectTransform>() });
            ui.ConfigureRegionNavigation(map, title, previousRegion, nextRegion, pinLayer.gameObject, detail.gameObject);
        }

        private static RectTransform[] CreateDottedRoute(Transform parent)
        {
            const int dotCount = 11;
            RectTransform[] dots = new RectTransform[dotCount];
            for (int i = 0; i < dotCount; i++)
            {
                GameObject dot = new($"RouteDot_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                dot.transform.SetParent(parent, false);
                RectTransform rect = dot.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.35f, 0.56f);
                rect.sizeDelta = new Vector2(10f, 10f);
                Image image = dot.GetComponent<Image>();
                image.color = new Color32(34, 211, 238, (byte)(130 + i * 10));
                image.raycastTarget = false;
                dot.transform.SetSiblingIndex(0);
                dot.SetActive(false);
                dots[i] = rect;
            }
            return dots;
        }

        private static Button CreateMapPin(Transform parent, string name, string label, string spritePath, Vector2 center, Color accent)
        {
            GameObject root = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = center;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(118f, 146f);
            Image image = root.GetComponent<Image>();
            image.sprite = LoadResourceSprite(spritePath);
            image.color = Color.white;
            image.preserveAspect = true;
            Button button = root.GetComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color32(210, 248, 255, 255);
            colors.pressedColor = new Color32(145, 185, 205, 255);
            button.colors = colors;
            RectTransform labelPanel = CreatePanel(root.transform, "LabelPanel", new Vector2(-0.28f, -0.18f), new Vector2(1.28f, 0.12f));
            labelPanel.GetComponent<Image>().raycastTarget = false;
            labelPanel.GetComponent<Outline>().effectColor = accent;
            TextMeshProUGUI text = CreateText(labelPanel, "Label", label, 16f, new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.95f), TextAlignmentOptions.Center);
            text.color = accent;
            text.fontStyle = FontStyles.Bold;
            return button;
        }

        private static void BuildWorldMapSettingsButton(Transform parent)
        {
            Button button = CreateStandaloneButton(parent, "SettingsButton", string.Empty, new Vector2(0.945f, 0.18f), new Vector2(0.988f, 0.86f), Cyan);
            Image gear = CreateImage(button.transform, "SettingsButtonVisual", Color.white, new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f));
            gear.sprite = LoadResourceSprite("DatingSim/YomiRoom/UI/SettingsGear");
            gear.preserveAspect = true;
            button.gameObject.SetActive(false);
            SettingsMenuController settings = button.gameObject.AddComponent<SettingsMenuController>();
            settings.ConfigureRoomButton(button);
            button.gameObject.SetActive(true);
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

        private static Sprite LoadResourceSprite(string resourcePath)
        {
            // Sprite 타입으로 임포트된 UI 에셋은 Unity가 생성한 원본 Sprite를 우선 사용합니다.
            // Texture2D에서 런타임 Sprite를 재생성하면 플랫폼별 압축 포맷에서 색상 채널이 손실될 수 있습니다.
            Sprite importedSprite = Resources.Load<Sprite>(resourcePath);
            if (importedSprite != null) return importedSprite;

            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                Debug.LogWarning($"[DatingSimUI] UI 텍스처를 찾지 못했습니다: {resourcePath}");
                return null;
            }
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
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
