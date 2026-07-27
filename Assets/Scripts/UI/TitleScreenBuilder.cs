using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FXOverdose.Core;

namespace FXOverdose.UI
{
    /// <summary>TitleScene의 픽셀 타이틀 UI와 팝업을 조립합니다.</summary>
    public static class TitleScreenBuilder
    {
        private static readonly Color32 Navy = new(7, 17, 31, 255);
        private static readonly Color32 Panel = new(12, 27, 47, 245);
        private static readonly Color32 Cyan = new(6, 182, 212, 255);
        private static readonly Color32 Pink = new(255, 72, 114, 255);
        private static readonly Color32 Text = new(225, 242, 255, 255);
        private static readonly Color32 Muted = new(126, 160, 184, 255);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BuildOnTitleScene()
        {
            BuildForCurrentScene();
        }

        public static void BuildForCurrentScene()
        {
            if (SceneManager.GetActiveScene().name != "TitleScene")
                return;

            EnsureTitleCamera();
            EnsureSystems();

            GameObject existingCanvas = GameObject.Find("Canvas_MainMenu");
            if (existingCanvas != null)
            {
                EnsureGameModePanel(existingCanvas.transform);
                return;
            }

            Canvas canvas = CreateCanvas();
            BuildBackground(canvas.transform);
            BuildHeader(canvas.transform);

            MainMenuController controller = new GameObject("MainMenuController").AddComponent<MainMenuController>();
            controller.transform.SetParent(canvas.transform, false);

            Button newGame = CreateMenuButton(canvas.transform, "Btn_NewGame", "NEW GAME", "새 게임 시작", 0);
            Button loadGame = CreateMenuButton(canvas.transform, "Btn_LoadGame", "CONTINUE", "게임 불러오기", 1);
            Button settings = CreateMenuButton(canvas.transform, "Btn_Settings", "SETTINGS", "환경 설정", 2);
            Button quit = CreateMenuButton(canvas.transform, "Btn_QuitGame", "QUIT", "게임 종료", 3);

            GameObject loadPanel = BuildLoadPanel(canvas.transform, controller);
            GameObject settingsPanel = BuildSettingsPanel(canvas.transform);
            GameObject modePanel = EnsureGameModePanel(canvas.transform);
            controller.Configure(newGame, loadGame, settings, quit, loadPanel, settingsPanel, modePanel);

            CreateText(canvas.transform, "VersionText", "EARLY ACCESS  /  BUILD 0.1", 16f, Muted,
                new Vector2(0.055f, 0.025f), new Vector2(0.42f, 0.075f), TextAlignmentOptions.Left);
            CreateText(canvas.transform, "WarningText", "TRADE THE MARKET.  PROTECT HER MIND.", 16f, Muted,
                new Vector2(0.58f, 0.025f), new Vector2(0.945f, 0.075f), TextAlignmentOptions.Right);
        }

        private static void EnsureTitleCamera()
        {
            if (Object.FindAnyObjectByType<Camera>() != null)
                return;

            GameObject cameraObject = new("Title Camera", typeof(Camera), typeof(AudioListener));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Navy;
            camera.cullingMask = 0;
            camera.orthographic = true;
            camera.depth = -100f;
            cameraObject.tag = "MainCamera";
        }

        private static void EnsureSystems()
        {
            if (Object.FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            if (SaveLoadManager.Instance == null)
                new GameObject("SaveLoadManager").AddComponent<SaveLoadManager>();
        }

        private static Canvas CreateCanvas()
        {
            GameObject go = new("Canvas_MainMenu", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void BuildBackground(Transform parent)
        {
            Image background = CreateImage(parent, "BackgroundImage", Color.white, Vector2.zero, Vector2.one);
            Texture2D texture = Resources.Load<Texture2D>("UI/TitleBackground");
            if (texture != null)
            {
                background.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                background.preserveAspect = false;
            }
            else
            {
                background.color = Navy;
            }

            CreateImage(parent, "BackgroundDim", new Color32(2, 8, 18, 168), Vector2.zero, Vector2.one);
            CreateImage(parent, "LeftGradientPanel", new Color32(4, 14, 28, 232),
                new Vector2(0f, 0f), new Vector2(0.48f, 1f));

            Image cyanLine = CreateImage(parent, "CyanEdge", Cyan,
                new Vector2(0.047f, 0.11f), new Vector2(0.050f, 0.89f));
            cyanLine.raycastTarget = false;
            Image pinkLine = CreateImage(parent, "PinkAccent", Pink,
                new Vector2(0.052f, 0.865f), new Vector2(0.23f, 0.871f));
            pinkLine.raycastTarget = false;
        }

        private static void BuildHeader(Transform parent)
        {
            TMP_Text logo = CreateText(parent, "TitleLogo", "FX\nOVERDOSE", 92f, Text,
                new Vector2(0.075f, 0.64f), new Vector2(0.43f, 0.87f), TextAlignmentOptions.BottomLeft);
            logo.fontStyle = FontStyles.Bold;
            logo.characterSpacing = 2f;
            logo.lineSpacing = -20f;
            logo.outlineWidth = 0.18f;
            logo.outlineColor = new Color32(1, 7, 17, 255);

            TMP_Text subtitle = CreateText(parent, "Subtitle", "PIXEL TRADING PSYCHOLOGICAL SIMULATION", 19f, Cyan,
                new Vector2(0.078f, 0.595f), new Vector2(0.43f, 0.64f), TextAlignmentOptions.Left);
            subtitle.characterSpacing = 3f;
        }

        private static Button CreateMenuButton(Transform parent, string name, string title, string subtitle, int index)
        {
            float top = 0.54f - index * 0.105f;
            Image image = CreateImage(parent, name, Panel, new Vector2(0.075f, top - 0.08f), new Vector2(0.36f, top));
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = CreateButtonColors();

            Outline outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = index == 0 ? Cyan : new Color32(50, 78, 104, 255);
            outline.effectDistance = new Vector2(2f, -2f);

            CreateText(image.transform, "Title", title, 30f, index == 0 ? Cyan : Text,
                new Vector2(0.06f, 0.36f), new Vector2(0.94f, 0.92f), TextAlignmentOptions.Left).fontStyle = FontStyles.Bold;
            CreateText(image.transform, "Subtitle", subtitle, 14f, Muted,
                new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.38f), TextAlignmentOptions.Left);
            CreateText(image.transform, "Arrow", ">", 30f, index == 0 ? Cyan : Muted,
                new Vector2(0.86f, 0.15f), new Vector2(0.96f, 0.85f), TextAlignmentOptions.Center);
            return button;
        }

        /// <summary>
        /// 기존 TitleScene UI를 삭제하지 않고 누락된 게임 모드 선택 창만 안전하게 추가합니다.
        /// 직렬화된 타이틀 씬과 런타임 생성 타이틀 양쪽에서 함께 사용합니다.
        /// </summary>
        public static GameObject EnsureGameModePanel(Transform parent)
        {
            if (parent == null) return null;

            Transform existing = parent.Find("GameModePanel");
            if (existing != null) return existing.gameObject;

            GameObject overlay = CreateModalOverlay(parent, "GameModePanel");
            Image window = CreateWindow(overlay.transform, "ModalWindow", new Vector2(0.15f, 0.14f), new Vector2(0.85f, 0.86f));
            CreateModalTitle(window.transform, "SELECT GAME MODE", "플레이할 규칙을 선택하세요");

            CreateModeCard(
                window.transform,
                "Btn_StoryMode",
                "01  STORY",
                "SAVE SLOT",
                "저장된 이야기를 이어가거나\n빈 슬롯에서 새로 시작",
                new Vector2(0.055f, 0.25f),
                new Vector2(0.325f, 0.70f),
                Cyan);

            CreateModeCard(
                window.transform,
                "Btn_EndlessMode",
                "02  ENDLESS",
                "AI ENABLED",
                "끝없이 이어지는 시장\nAI 자동매매 사용 가능",
                new Vector2(0.365f, 0.25f),
                new Vector2(0.635f, 0.70f),
                new Color32(74, 201, 112, 255));

            CreateModeCard(
                window.transform,
                "Btn_ChallengeMode",
                "03  CHALLENGE",
                "USER ONLY",
                "AI 자동매매 금지\n플레이어 수동매매 고정",
                new Vector2(0.675f, 0.25f),
                new Vector2(0.945f, 0.70f),
                Pink);

            CreateText(window.transform, "RuleHint", "CHALLENGE에서는 캐릭터 대사는 유지되고, AI 자동매매만 잠깁니다.", 14f, Muted,
                new Vector2(0.08f, 0.175f), new Vector2(0.92f, 0.225f), TextAlignmentOptions.Center);

            CreateSmallButton(window.transform, "Btn_Close", "BACK", new Vector2(0.39f, 0.07f), new Vector2(0.61f, 0.16f));
            overlay.SetActive(false);
            return overlay;
        }

        public static GameObject EnsureTutorialPromptPanel(Transform parent)
        {
            if (parent == null) return null;

            Transform existing = parent.Find("TutorialPromptPanel");
            if (existing != null) return existing.gameObject;

            GameObject overlay = CreateModalOverlay(parent, "TutorialPromptPanel");
            Image window = CreateWindow(overlay.transform, "ModalWindow", new Vector2(0.25f, 0.35f), new Vector2(0.75f, 0.65f));
            CreateModalTitle(window.transform, "TUTORIAL", "튜토리얼을 진행하시겠습니까?");

            CreateText(window.transform, "Desc", "처음 오셨다면 튜토리얼을 통해\n게임의 기초 시스템을 배우는 것을 권장합니다.", 18f, Muted,
                new Vector2(0.05f, 0.40f), new Vector2(0.95f, 0.70f), TextAlignmentOptions.Center);

            CreateSmallButton(window.transform, "Btn_Yes", "예 (진행)", new Vector2(0.15f, 0.15f), new Vector2(0.45f, 0.30f));
            CreateSmallButton(window.transform, "Btn_No", "아니오 (건너뛰기)", new Vector2(0.55f, 0.15f), new Vector2(0.85f, 0.30f));

            overlay.SetActive(false);
            return overlay;
        }

        private static void CreateModeCard(
            Transform parent,
            string name,
            string title,
            string badge,
            string description,
            Vector2 min,
            Vector2 max,
            Color32 accent)
        {
            Image card = CreateImage(parent, name, new Color32(13, 31, 52, 255), min, max);
            Button button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card;
            button.colors = CreateButtonColors();

            Outline outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = accent;
            outline.effectDistance = new Vector2(2f, -2f);

            Image accentLine = CreateImage(card.transform, "AccentLine", accent,
                new Vector2(0.07f, 0.83f), new Vector2(0.93f, 0.855f));
            accentLine.raycastTarget = false;

            TMP_Text titleText = CreateText(card.transform, "Title", title, 27f, Text,
                new Vector2(0.07f, 0.67f), new Vector2(0.93f, 0.82f), TextAlignmentOptions.Left);
            titleText.fontStyle = FontStyles.Bold;

            TMP_Text badgeText = CreateText(card.transform, "Badge", badge, 18f, accent,
                new Vector2(0.07f, 0.48f), new Vector2(0.93f, 0.63f), TextAlignmentOptions.Left);
            badgeText.fontStyle = FontStyles.Bold;

            TMP_Text descriptionText = CreateText(card.transform, "Description", description, 16f, Muted,
                new Vector2(0.07f, 0.18f), new Vector2(0.93f, 0.45f), TextAlignmentOptions.TopLeft);
            descriptionText.textWrappingMode = TextWrappingModes.Normal;
            descriptionText.overflowMode = TextOverflowModes.Overflow;

            CreateText(card.transform, "Arrow", ">", 26f, accent,
                new Vector2(0.79f, 0.03f), new Vector2(0.94f, 0.17f), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
        }

        private static GameObject BuildLoadPanel(Transform parent, MainMenuController controller)
        {
            GameObject overlay = CreateModalOverlay(parent, "LoadGamePanel");
            Image window = CreateWindow(overlay.transform, "ModalWindow", new Vector2(0.30f, 0.19f), new Vector2(0.70f, 0.81f));
            CreateModalTitle(window.transform, "LOAD GAME", "저장된 거래 기록을 선택하세요");

            for (int i = 0; i < 3; i++)
            {
                int slotIndex = i;
                bool hasSave = SaveLoadManager.Instance != null && SaveLoadManager.Instance.HasSave(i);
                Image slot = CreateImage(window.transform, $"SaveSlot_{i + 1}", new Color32(15, 35, 58, 255),
                    new Vector2(0.09f, 0.60f - i * 0.18f), new Vector2(0.91f, 0.73f - i * 0.18f));
                Button slotButton = slot.gameObject.AddComponent<Button>();
                slotButton.targetGraphic = slot;
                slotButton.interactable = hasSave;
                slotButton.colors = CreateButtonColors();
                slotButton.onClick.AddListener(() => controller.OnClickLoadSlot(slotIndex));
                CreateText(slot.transform, "SlotName", $"SAVE {i + 1:00}", 23f, hasSave ? Text : Muted,
                    new Vector2(0.05f, 0.18f), new Vector2(0.34f, 0.82f), TextAlignmentOptions.Left).fontStyle = FontStyles.Bold;
                CreateText(slot.transform, "State", hasSave ? "DATA FOUND" : "EMPTY SLOT", 16f, hasSave ? Cyan : Muted,
                    new Vector2(0.37f, 0.18f), new Vector2(0.94f, 0.82f), TextAlignmentOptions.Right);
            }

            Button close = CreateSmallButton(window.transform, "Btn_Close", "CLOSE", new Vector2(0.36f, 0.08f), new Vector2(0.64f, 0.18f));
            close.onClick.AddListener(() => overlay.SetActive(false));
            overlay.SetActive(false);
            return overlay;
        }

        private static GameObject BuildSettingsPanel(Transform parent)
        {
            GameObject overlay = CreateModalOverlay(parent, "SettingsPanel");
            Image window = CreateWindow(overlay.transform, "ModalWindow", new Vector2(0.32f, 0.20f), new Vector2(0.68f, 0.80f));
            CreateModalTitle(window.transform, "SETTINGS", "오디오 및 화면 설정");

            CreateText(window.transform, "BGMLabel", "BGM VOLUME", 19f, Text,
                new Vector2(0.10f, 0.61f), new Vector2(0.90f, 0.68f), TextAlignmentOptions.Left);
            Slider bgm = CreateSlider(window.transform, "Slider_BGM", new Vector2(0.10f, 0.53f), new Vector2(0.90f, 0.60f));
            bgm.value = PlayerPrefs.GetFloat("BGMVolume", 0.8f);
            bgm.onValueChanged.AddListener(value => {
                PlayerPrefs.SetFloat("BGMVolume", value);
                if (FXOverdose.Core.AudioManager.Instance != null)
                    FXOverdose.Core.AudioManager.Instance.SetBGMVolume(value);
            });

            CreateText(window.transform, "SFXLabel", "SFX VOLUME", 19f, Text,
                new Vector2(0.10f, 0.40f), new Vector2(0.90f, 0.47f), TextAlignmentOptions.Left);
            Slider sfx = CreateSlider(window.transform, "Slider_SFX", new Vector2(0.10f, 0.32f), new Vector2(0.90f, 0.39f));
            sfx.value = PlayerPrefs.GetFloat("SFXVolume", 0.8f);
            sfx.onValueChanged.AddListener(value => {
                PlayerPrefs.SetFloat("SFXVolume", value);
                if (FXOverdose.Core.AudioManager.Instance != null)
                    FXOverdose.Core.AudioManager.Instance.SetSFXVolume(value);
            });

            Button close = CreateSmallButton(window.transform, "Btn_Close", "APPLY & CLOSE", new Vector2(0.29f, 0.09f), new Vector2(0.71f, 0.20f));
            close.onClick.AddListener(() =>
            {
                PlayerPrefs.Save();
                overlay.SetActive(false);
            });
            overlay.SetActive(false);
            return overlay;
        }

        private static GameObject CreateModalOverlay(Transform parent, string name)
        {
            return CreateImage(parent, name, new Color32(0, 4, 12, 220), Vector2.zero, Vector2.one).gameObject;
        }

        private static Image CreateWindow(Transform parent, string name, Vector2 min, Vector2 max)
        {
            Image window = CreateImage(parent, name, Panel, min, max);
            Outline outline = window.gameObject.AddComponent<Outline>();
            outline.effectColor = Cyan;
            outline.effectDistance = new Vector2(3f, -3f);
            return window;
        }

        private static void CreateModalTitle(Transform parent, string title, string subtitle)
        {
            CreateText(parent, "Txt_Title", title, 34f, Cyan,
                new Vector2(0.08f, 0.82f), new Vector2(0.92f, 0.94f), TextAlignmentOptions.Left).fontStyle = FontStyles.Bold;
            CreateText(parent, "Txt_Subtitle", subtitle, 15f, Muted,
                new Vector2(0.08f, 0.75f), new Vector2(0.92f, 0.82f), TextAlignmentOptions.Left);
        }

        private static Button CreateSmallButton(Transform parent, string name, string label, Vector2 min, Vector2 max)
        {
            Image image = CreateImage(parent, name, new Color32(18, 48, 72, 255), min, max);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = CreateButtonColors();
            Outline outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = Cyan;
            outline.effectDistance = new Vector2(2f, -2f);
            CreateText(image.transform, "Label", label, 20f, Text, Vector2.zero, Vector2.one, TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
            return button;
        }

        private static Slider CreateSlider(Transform parent, string name, Vector2 min, Vector2 max)
        {
            GameObject root = new(name, typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);
            SetRect(root.GetComponent<RectTransform>(), min, max);

            Image background = CreateImage(root.transform, "Background", new Color32(3, 10, 22, 255), Vector2.zero, Vector2.one);
            GameObject fillArea = new("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            SetRect(fillArea.GetComponent<RectTransform>(), new Vector2(0.02f, 0.18f), new Vector2(0.98f, 0.82f));
            Image fill = CreateImage(fillArea.transform, "Fill", Cyan, Vector2.zero, Vector2.one);

            Slider slider = root.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.fillRect = fill.rectTransform;
            slider.targetGraphic = background;
            return slider;
        }

        private static ColorBlock CreateButtonColors()
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.72f, 0.95f, 1f, 1f);
            colors.pressedColor = new Color(0.35f, 0.75f, 0.85f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.36f, 0.42f, 0.48f, 0.55f);
            colors.fadeDuration = 0.08f;
            return colors;
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

        private static TMP_Text CreateText(Transform parent, string name, string value, float size, Color color, Vector2 min, Vector2 max, TextAlignmentOptions alignment)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TMP_Text text = go.GetComponent<TMP_Text>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = value;
            text.fontSize = size;
            text.fontSizeMin = Mathf.Max(10f, size * 0.55f);
            text.fontSizeMax = size;
            text.enableAutoSizing = true;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            SetRect(text.rectTransform, min, max);
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

}
