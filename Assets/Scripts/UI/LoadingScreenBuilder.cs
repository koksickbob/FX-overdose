using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FXOverdose.UI
{
    /// <summary>LoadingScene의 검정 배경, SD 캐릭터와 진행 표시를 조립합니다.</summary>
    public static class LoadingScreenBuilder
    {
        private static readonly Color32 Cyan = new(6, 182, 212, 255);
        private static readonly Color32 Track = new(16, 29, 42, 255);
        private static readonly Color32 TextColor = new(220, 238, 246, 255);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneCallback()
        {
            // RuntimeInitializeOnLoadMethod는 앱 시작 때만 호출되므로 이후 씬 전환은
            // sceneLoaded 이벤트로 직접 받아 로딩 UI를 생성해야 합니다.
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "LoadingScene") return;
            BuildForCurrentScene();
            Debug.Log("[LoadingScreenBuilder] LoadingScene UI와 카메라 조립 완료");
        }

        public static void BuildForCurrentScene()
        {
            if (SceneManager.GetActiveScene().name != "LoadingScene") return;
            EnsureCamera();
            if (GameObject.Find("Canvas_LoadingScreen") != null) return;

            GameObject canvasObject = new("Canvas_LoadingScreen", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10000;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Image background = CreateImage(canvas.transform, "BlackBackground", Color.black, Vector2.zero, Vector2.one);
            background.raycastTarget = true;

            Image character = CreateImage(canvas.transform, "SleepingChibi", Color.white,
                new Vector2(0.29f, 0.25f), new Vector2(0.71f, 0.78f));
            Texture2D texture = Resources.Load<Texture2D>("UI/Loading/SleepingChibi");
            if (texture != null)
            {
                character.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                character.preserveAspect = true;
            }

            TMP_Text status = CreateText(canvas.transform, "StatusText", "INITIALIZING", 22f,
                new Vector2(0.18f, 0.145f), new Vector2(0.82f, 0.205f), TextAlignmentOptions.Center);
            status.color = new Color32(125, 151, 168, 255);
            status.characterSpacing = 4f;

            Slider slider = CreateProgressBar(canvas.transform);
            TMP_Text percent = CreateText(canvas.transform, "ProgressText", "0%", 29f,
                new Vector2(0.82f, 0.075f), new Vector2(0.91f, 0.13f), TextAlignmentOptions.Right);
            percent.color = Cyan;
            percent.fontStyle = FontStyles.Bold;

            TMP_Text hint = CreateText(canvas.transform, "HintText", "SHE IS DREAMING OF A CLEAN ENTRY...", 16f,
                new Vector2(0.18f, 0.025f), new Vector2(0.82f, 0.065f), TextAlignmentOptions.Center);
            hint.color = new Color32(70, 91, 106, 255);
            hint.characterSpacing = 2f;

            LoadingScreenController controller = new GameObject("LoadingScreenController").AddComponent<LoadingScreenController>();
            controller.transform.SetParent(canvas.transform, false);
            controller.Configure(slider, percent, status, canvasObject.GetComponent<CanvasGroup>());
        }

        private static Slider CreateProgressBar(Transform parent)
        {
            GameObject root = new("ProgressBar", typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);
            SetRect(root.GetComponent<RectTransform>(), new Vector2(0.09f, 0.085f), new Vector2(0.80f, 0.125f));

            Image outline = CreateImage(root.transform, "Outline", new Color32(45, 65, 80, 255), Vector2.zero, Vector2.one);
            Image track = CreateImage(root.transform, "Background", Track, new Vector2(0.006f, 0.14f), new Vector2(0.994f, 0.86f));
            GameObject fillArea = new("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            SetRect(fillArea.GetComponent<RectTransform>(), new Vector2(0.012f, 0.22f), new Vector2(0.988f, 0.78f));
            Image fill = CreateImage(fillArea.transform, "Fill", Cyan, Vector2.zero, Vector2.one);

            Slider slider = root.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.fillRect = fill.rectTransform;
            slider.targetGraphic = track;
            slider.interactable = false;
            outline.raycastTarget = false;
            return slider;
        }

        private static void EnsureCamera()
        {
            if (Object.FindAnyObjectByType<Camera>() != null) return;
            GameObject go = new("Loading Camera", typeof(Camera), typeof(AudioListener));
            Camera camera = go.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;
            camera.depth = -100f;
            go.tag = "MainCamera";
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

        private static TMP_Text CreateText(Transform parent, string name, string value, float size, Vector2 min, Vector2 max, TextAlignmentOptions alignment)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TMP_Text text = go.GetComponent<TMP_Text>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = value;
            text.fontSize = size;
            text.color = TextColor;
            text.alignment = alignment;
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
