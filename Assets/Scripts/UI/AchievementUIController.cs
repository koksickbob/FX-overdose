using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FXOverdose.Core;

namespace FXOverdose.UI
{
    public class AchievementUIController : MonoBehaviour
    {
        [Header("UI References (비어 있으면 런타임에 자동 생성)")]
        [SerializeField] private GameObject overlay;
        [SerializeField] private Transform contentPanel;
        [SerializeField] private GameObject itemPrefab;
        [SerializeField] private Button closeButton;

        private TMP_Text progressText;

        public static AchievementUIController Instance { get; private set; }

        private static readonly Color32 Dim = new(1, 6, 14, 220);
        private static readonly Color32 Panel = new(11, 20, 36, 255);
        private static readonly Color32 Header = new(18, 31, 52, 255);
        private static readonly Color32 Card = new(16, 29, 48, 255);
        private static readonly Color32 Border = new(51, 76, 105, 255);
        private static readonly Color32 Cyan = new(6, 182, 212, 255);
        private static readonly Color32 Gold = new(234, 179, 8, 255);
        private static readonly Color32 Text = new(226, 236, 248, 255);
        private static readonly Color32 Muted = new(137, 157, 181, 255);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureAchievementSystem()
        {
            if (AchievementManager.Instance == null)
                new GameObject("AchievementManager").AddComponent<AchievementManager>();
            if (Instance == null)
                new GameObject("AchievementUI").AddComponent<AchievementUIController>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            BuildRuntimeUIIfNeeded();
        }

        private void OnEnable()
        {
            if (AchievementManager.Instance != null)
                AchievementManager.Instance.OnAchievementsChanged += HandleAchievementsChanged;
        }

        private void Start()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
                closeButton.onClick.AddListener(Close);
            }
            if (overlay != null) overlay.SetActive(false);
        }

        private void OnDisable()
        {
            if (AchievementManager.Instance != null)
                AchievementManager.Instance.OnAchievementsChanged -= HandleAchievementsChanged;
        }

        private void OnDestroy()
        {
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (Instance == this) Instance = null;
        }

        public void Open()
        {
            BuildRuntimeUIIfNeeded();
            if (overlay == null) return;
            overlay.SetActive(true);
            overlay.transform.SetAsLastSibling();
            PopulateList();
        }

        public void Close()
        {
            if (overlay != null) overlay.SetActive(false);
        }

        private void HandleAchievementsChanged()
        {
            if (overlay != null && overlay.activeSelf) PopulateList();
        }

        private void PopulateList()
        {
            if (contentPanel == null || AchievementManager.Instance == null) return;

            foreach (Transform child in contentPanel)
                Destroy(child.gameObject);

            var allAchievements = AchievementManager.Instance.GetAllAchievements();
            int unlockedCount = 0;
            foreach (var achievement in allAchievements)
            {
                bool isUnlocked = AchievementManager.Instance.IsUnlocked(achievement.Id);
                if (isUnlocked) unlockedCount++;

                GameObject obj = itemPrefab != null
                    ? Instantiate(itemPrefab, contentPanel)
                    : CreateRuntimeItem(contentPanel);
                AchievementItemUI itemUI = obj.GetComponent<AchievementItemUI>();
                if (itemUI != null) itemUI.Setup(achievement, isUnlocked);
            }

            if (progressText != null)
                progressText.text = $"UNLOCKED  {unlockedCount} / {allAchievements.Count}";

            Canvas.ForceUpdateCanvases();
            if (contentPanel is RectTransform contentRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        }

        private void BuildRuntimeUIIfNeeded()
        {
            if (overlay != null && contentPanel != null) return;

            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 900;
            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

            overlay = CreatePanel(transform, "AchievementOverlay", Dim, Vector2.zero, Vector2.one).gameObject;
            Button dimClose = overlay.AddComponent<Button>();
            dimClose.targetGraphic = overlay.GetComponent<Image>();
            dimClose.onClick.AddListener(Close);

            Image modal = CreatePanel(overlay.transform, "AchievementWindow", Panel,
                new Vector2(0.18f, 0.09f), new Vector2(0.82f, 0.91f));
            AddOutline(modal.gameObject, Cyan, new Vector2(3f, -3f));
            Button modalBlocker = modal.gameObject.AddComponent<Button>();
            modalBlocker.targetGraphic = modal;
            modalBlocker.transition = Selectable.Transition.None;

            Image topAccent = CreatePanel(modal.transform, "TopAccent", Cyan,
                new Vector2(0f, 0.985f), Vector2.one);
            topAccent.raycastTarget = false;

            Image header = CreatePanel(modal.transform, "Header", Header,
                new Vector2(0.025f, 0.86f), new Vector2(0.975f, 0.965f));
            AddOutline(header.gameObject, Border, new Vector2(2f, -2f));

            TMP_Text title = CreateText(header.transform, "Title", "ACHIEVEMENTS", 34f, Text,
                new Vector2(0.035f, 0.25f), new Vector2(0.58f, 0.9f), TextAlignmentOptions.Left);
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 2f;
            CreateText(header.transform, "Subtitle", "TRADING RECORDS  /  GLOBAL PROGRESS", 14f, Muted,
                new Vector2(0.037f, 0.02f), new Vector2(0.62f, 0.34f), TextAlignmentOptions.Left);

            progressText = CreateText(header.transform, "Progress", "UNLOCKED  0 / 0", 19f, Gold,
                new Vector2(0.61f, 0.12f), new Vector2(0.86f, 0.88f), TextAlignmentOptions.Center);
            progressText.fontStyle = FontStyles.Bold;

            Image closeImage = CreatePanel(header.transform, "CloseButton", new Color32(72, 30, 44, 255),
                new Vector2(0.885f, 0.18f), new Vector2(0.97f, 0.82f));
            AddOutline(closeImage.gameObject, new Color32(244, 63, 94, 255), new Vector2(2f, -2f));
            closeButton = closeImage.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = closeImage;
            TMP_Text closeLabel = CreateText(closeImage.transform, "Label", "×", 29f, Text,
                Vector2.zero, Vector2.one, TextAlignmentOptions.Center);
            closeLabel.fontStyle = FontStyles.Bold;

            GameObject scrollObject = new("AchievementScroll", typeof(RectTransform), typeof(ScrollRect));
            scrollObject.transform.SetParent(modal.transform, false);
            SetRect(scrollObject.GetComponent<RectTransform>(), new Vector2(0.025f, 0.055f), new Vector2(0.975f, 0.835f));

            Image viewport = CreatePanel(scrollObject.transform, "Viewport", new Color32(7, 14, 26, 255),
                Vector2.zero, Vector2.one);
            viewport.gameObject.AddComponent<RectMask2D>();
            AddOutline(viewport.gameObject, Border, new Vector2(2f, -2f));

            GameObject content = new("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.spacing = 12f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentPanel = content.transform;

            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.viewport = viewport.rectTransform;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 34f;

            overlay.SetActive(false);
        }

        private static GameObject CreateRuntimeItem(Transform parent)
        {
            GameObject item = new("AchievementItem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(Outline), typeof(LayoutElement), typeof(AchievementItemUI));
            item.transform.SetParent(parent, false);
            Image background = item.GetComponent<Image>();
            background.color = Card;
            AddOutline(item, Border, new Vector2(2f, -2f));
            LayoutElement element = item.GetComponent<LayoutElement>();
            element.preferredHeight = 138f;
            element.minHeight = 138f;

            TMP_Text title = CreateText(item.transform, "Title", "???", 23f, Text,
                new Vector2(0.035f, 0.57f), new Vector2(0.58f, 0.9f), TextAlignmentOptions.Left);
            title.fontStyle = FontStyles.Bold;
            TMP_Text description = CreateText(item.transform, "Description", string.Empty, 16f, Muted,
                new Vector2(0.035f, 0.25f), new Vector2(0.68f, 0.58f), TextAlignmentOptions.Left);
            description.textWrappingMode = TextWrappingModes.Normal;
            description.overflowMode = TextOverflowModes.Ellipsis;
            TMP_Text reward = CreateText(item.transform, "Reward", string.Empty, 15f, Gold,
                new Vector2(0.69f, 0.25f), new Vector2(0.95f, 0.48f), TextAlignmentOptions.Right);
            reward.fontStyle = FontStyles.Bold;

            Image lockBadge = CreatePanel(item.transform, "LockedOverlay", new Color32(45, 55, 72, 245),
                new Vector2(0.72f, 0.56f), new Vector2(0.95f, 0.88f));
            lockBadge.raycastTarget = false;
            AddOutline(lockBadge.gameObject, Border, new Vector2(2f, -2f));
            TMP_Text lockText = CreateText(lockBadge.transform, "Label", "LOCKED", 17f, Muted,
                Vector2.zero, Vector2.one, TextAlignmentOptions.Center);
            lockText.fontStyle = FontStyles.Bold;

            Image progressTrack = CreatePanel(item.transform, "ProgressTrack", new Color32(5, 13, 25, 255),
                new Vector2(0.035f, 0.08f), new Vector2(0.88f, 0.19f));
            AddOutline(progressTrack.gameObject, Border, new Vector2(1f, -1f));
            Image progressFill = CreatePanel(progressTrack.transform, "Fill", Cyan, Vector2.zero, Vector2.one);
            progressFill.raycastTarget = false;
            TMP_Text percentage = CreateText(item.transform, "ProgressText", "0%", 16f, Text,
                new Vector2(0.89f, 0.04f), new Vector2(0.96f, 0.22f), TextAlignmentOptions.Center);
            percentage.fontStyle = FontStyles.Bold;

            item.GetComponent<AchievementItemUI>().Configure(
                title, description, reward, lockBadge.gameObject, background, progressFill, percentage);
            return item;
        }

        private static Image CreatePanel(Transform parent, string name, Color color, Vector2 min, Vector2 max)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            SetRect(image.rectTransform, min, max);
            return image;
        }

        private static TMP_Text CreateText(Transform parent, string name, string value, float size, Color color,
            Vector2 min, Vector2 max, TextAlignmentOptions alignment)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TMP_Text text = go.GetComponent<TMP_Text>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = value;
            text.fontSize = size;
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

        private static void AddOutline(GameObject target, Color color, Vector2 distance)
        {
            Outline outline = target.GetComponent<Outline>();
            if (outline == null) outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
            outline.useGraphicAlpha = true;
        }
    }
}
