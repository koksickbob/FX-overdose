using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FXOverdose.Trading;

#pragma warning disable CS0649

namespace FXOverdose.UI.Chart
{
    public class TradingPanelUIController : MonoBehaviour
    {
        public enum ControlMode
        {
            Leverage,
            MarginRatio,
            AIStyle
        }

        [Header("시스템 연결")]
        [SerializeField] private TradingController tradingController;
        [SerializeField] private GameManager gameManager;

        [Header("포지션 진입/종료 버튼")]
        [SerializeField] private Button longButton;
        [SerializeField] private Button shortButton;
        [SerializeField] private Button closePositionButton;
        [SerializeField] private TMP_Text longSubtitleText; // "Tap to Open Long" 또는 "LONG 보유중"
        [SerializeField] private TMP_Text shortSubtitleText; // "Tap to Open Short" 또는 "SHORT 보유중"

        [Header("조작부 모드 전환 탭 (우측 상단)")]
        [SerializeField] private Button btnTabLeverageMode;   // "LEVERAGE 배율" 탭
        [SerializeField] private Button btnTabMarginRatioMode; // "MARGIN 비율" 탭
        [SerializeField] private Button btnTabAIStyleMode;     // "AUTO STYLE 성향" 탭
        [SerializeField] private GameObject leverageControlContainer;    // 레버리지 조작부 컨테이너
        [SerializeField] private GameObject marginRatioControlContainer; // 투자비율 조작부 컨테이너
        [SerializeField] private GameObject aiStyleControlContainer;     // AI 성향 조작부 컨테이너 (신설)
        [SerializeField] private GameObject tabsBarContainer;            // 모드 전환 탭 바 컨테이너

        [Header("AI 성향(AI Style) 설정 (신설)")]
        [SerializeField] private Button btnPresetSafe;
        [SerializeField] private Button btnPresetBalanced;
        [SerializeField] private Button btnPresetAggressive;
        [SerializeField] private TMP_Text aiStyleDescText; // 선택 시 설명 텍스트

        [Header("증거금(Margin Ratio) 설정 [10% 단위 스태퍼 + 프리셋 + 슬라이더 호환]")]
        [SerializeField] private Slider marginPercentageSlider; // 기존 슬라이더 (호환 유지)
        [SerializeField] private TMP_Text marginAmountText;     // "투입: $3,737 (30%)"
        [SerializeField] private Button btnMarginRatioMinus;    // -10% 버튼
        [SerializeField] private Button btnMarginRatioPlus;     // +10% 버튼
        [SerializeField] private TMP_Text marginRatioDisplayText; // "30% ($3,737)"
        [SerializeField] private Button btnPresetRatio10;       // 10% 프리셋
        [SerializeField] private Button btnPresetRatio25;       // 25% 프리셋
        [SerializeField] private Button btnPresetRatio50;       // 50% 프리셋
        [SerializeField] private Button btnPresetRatio75;       // 75% 프리셋
        [SerializeField] private Button btnPresetRatio100;      // 100% 프리셋

        [Header("레버리지(Leverage) 선택")]
        [SerializeField] private Button btnLeverageMinus;
        [SerializeField] private Button btnLeveragePlus;
        [SerializeField] private TMP_Text leverageDisplayText; // "10x"
        [SerializeField] private Button btnPreset1x;
        [SerializeField] private Button btnPreset5x;
        [SerializeField] private Button btnPreset10x;
        [SerializeField] private Button btnPreset25x;
        [SerializeField] private Button btnPreset50x;
        [SerializeField] private Button btnPreset100x;
        [SerializeField] private Button btnPreset125x;

        public Button LongButton => longButton;
        public Button ShortButton => shortButton;
        public Button ClosePositionButton => closePositionButton;
        public Button BtnLeverageMinus => btnLeverageMinus;
        public Button BtnLeveragePlus => btnLeveragePlus;
        public RectTransform TutorialMarginHighlightTarget =>
            marginRatioControlContainer != null ? marginRatioControlContainer.GetComponent<RectTransform>() : null;
        public RectTransform TutorialLeverageHighlightTarget =>
            leverageControlContainer != null ? leverageControlContainer.GetComponent<RectTransform>() : null;

        public void ShowMarginControlsForTutorial() => SwitchControlMode(ControlMode.MarginRatio);
        public void ShowLeverageControlsForTutorial() => SwitchControlMode(ControlMode.Leverage);

        [Header("실시간 포지션 상태 오버레이 (ROE & PnL)")]
        [SerializeField] private GameObject positionStatusPanel;
        [SerializeField] private TMP_Text positionTypeText;
        [SerializeField] private TMP_Text roeText;
        [SerializeField] private TMP_Text pnlText;
        [SerializeField] private TMP_Text entryPriceText;
        [SerializeField] private TMP_Text liquidationPriceText;
        [SerializeField] private TMP_Text targetPriceText; // AI 결정 목표 주가 (TARGET PRICE) 표시기

        // 색상 토큰
        private readonly Color cyanHighlight = new Color(0.024f, 0.714f, 0.831f, 1f); // #06B6D4
        private readonly Color inactivePresetColor = new Color(0.122f, 0.161f, 0.235f, 1f);
        private readonly Color bullishColor = new Color(0.133f, 0.773f, 0.369f, 1f);
        private readonly Color bearishColor = new Color(0.937f, 0.267f, 0.267f, 1f);
        private readonly Color balancedColor = new Color(0.024f, 0.714f, 0.831f, 1f);
        private readonly Color aiStylePanelColor = new Color(0.043f, 0.070f, 0.125f, 0.98f);

        private ControlMode currentControlMode = ControlMode.Leverage;
        private int currentSelectedLeverage = 10;
        private int currentSelectedMarginPercent = 30; // 기본 30%
        private float selectedMarginPercentage = 0.30f;
        private TradingController.PositionType lastVisualPosition = TradingController.PositionType.None;
        private Canvas positionFxCanvas;
        private CanvasGroup positionFxGroup;
        private Image positionFxDim;
        private RectTransform positionBannerRect;
        private Image positionBannerImage;

        // Day Volatility Gimmicks
        private GameObject lagUIPanel;
        private TMP_Text lagText;
        private bool isLagUIVisible = false;
        private Dictionary<Button, Vector2> originalButtonPositions = new Dictionary<Button, Vector2>();
        private Coroutine hallucinationCoroutine;
        private TraderStatus traderStatus;
        private TMP_Text positionBannerText;
        private Coroutine positionFxCoroutine;
        private GameObject tradeCooldownOverlay;
        private TMP_Text tradeCooldownText;
        private RectTransform effectStatusPanel;
        private ActiveItemEffectManager activeItemManager;
        private readonly List<KeyValuePair<ItemData, int>> activeItemStates = new();
        private readonly List<EffectIconView> effectIconViews = new();
        private ItemData pastaItem;
        private RectTransform inventoryPanelRect;
        private string effectIconSignature = string.Empty;
        private float nextEffectStatusRefreshTime;

        private sealed class EffectIconView
        {
            public bool IsFood;
            public TMP_Text Badge;
            public Image TimerFill;
        }

        private void Start()
        {
            ApplyMainHudHorizontalMargins();
            EnsureAIStyleUI();
            BuildEffectStatusHUD();
            BindActiveItemManager();
            if (tradingController == null) tradingController = FindAnyObjectByType<TradingController>();
            if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();

            traderStatus = TraderStatus.CanonicalInstance;

            if (longButton != null) originalButtonPositions[longButton] = longButton.GetComponent<RectTransform>().anchoredPosition;
            if (shortButton != null) originalButtonPositions[shortButton] = shortButton.GetComponent<RectTransform>().anchoredPosition;
            if (closePositionButton != null) originalButtonPositions[closePositionButton] = closePositionButton.GetComponent<RectTransform>().anchoredPosition;

            // 서버 렉 전용 오버레이 동적 생성
            CreateServerLagOverlay();

            if (tradingController != null)
            {
                tradingController.OnPositionChanged += RefreshPanelUI;
                tradingController.OnPositionLiquidated += HandlePositionLiquidated;
                tradingController.OnPositionOpened += HandlePositionOpened;
                tradingController.OnPositionClosed += HandlePositionClosed;
                tradingController.OnTradingModeChanged += (mode) => RefreshPanelUI();
                tradingController.OnAITradingStyleChanged += OnAITradingStyleChangedCallback;
            }

            BuildPositionFx();
            BuildTradeCooldownUI();
            ConfigureDynamicValueText(marginRatioDisplayText, 22f, 12f);
            ConfigureDynamicValueText(marginAmountText, 18f, 10f);
            SetupButtons();
            
            if (tradingController != null)
            {
                OnAITradingStyleChangedCallback(tradingController.CurrentAITradingStyle);
            }

            SelectLeverage(currentSelectedLeverage);
            SelectMarginRatio(currentSelectedMarginPercent);
            SwitchControlMode(ControlMode.Leverage); // 기본 레버리지 탭 활성화
            RefreshPanelUI();
            RefreshEffectStatusHUD();
        }

        private void ApplyMainHudHorizontalMargins()
        {
            RectTransform rect = transform as RectTransform;
            if (rect == null) return;

            rect.offsetMin = new Vector2(UIStrokeStyle.ScreenEdgeMargin, rect.offsetMin.y);
            rect.offsetMax = new Vector2(-UIStrokeStyle.CenterGutterHalf, rect.offsetMax.y);
        }

        private void CreateServerLagOverlay()
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null) return;

            lagUIPanel = new GameObject("ServerLagOverlay");
            lagUIPanel.transform.SetParent(parentCanvas.transform, false);
            var rect = lagUIPanel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            
            var img = lagUIPanel.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.6f); // 어두운 렉 화면
            img.raycastTarget = true; // 클릭 차단
            
            GameObject textObj = new GameObject("LagText");
            textObj.transform.SetParent(lagUIPanel.transform, false);
            lagText = textObj.AddComponent<TextMeshProUGUI>();
            lagText.text = "서버 연결 지연 중...";
            lagText.fontSize = 60;
            lagText.color = Color.red;
            lagText.alignment = TextAlignmentOptions.Center;
            
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = new Vector2(800, 200);
            textRect.anchoredPosition = Vector2.zero;

            lagUIPanel.SetActive(false);
        }

        private static void ConfigureDynamicValueText(TMP_Text text, float maxSize, float minSize)
        {
            if (text == null) return;
            text.enableAutoSizing = true;
            text.fontSizeMax = maxSize;
            text.fontSizeMin = minSize;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.margin = new Vector4(4f, 0f, 4f, 0f);
            text.alignment = TextAlignmentOptions.Center;
        }

        /// <summary>
        /// 구버전 씬처럼 AUTO STYLE 직렬화 슬롯이 비어 있어도 기존 컨트롤 카드 안에
        /// 세 번째 탭과 성향 프리셋을 멱등 생성합니다.
        /// </summary>
        private void EnsureAIStyleUI()
        {
            if (tabsBarContainer == null)
            {
                Transform tabParent = btnTabLeverageMode != null
                    ? btnTabLeverageMode.transform.parent
                    : btnTabMarginRatioMode != null ? btnTabMarginRatioMode.transform.parent : null;
                if (tabParent != null) tabsBarContainer = tabParent.gameObject;
            }

            if (tabsBarContainer == null || leverageControlContainer == null) return;

            if (btnTabAIStyleMode == null)
            {
                Transform existing = tabsBarContainer.transform.Find("BtnTabAIStyleMode");
                btnTabAIStyleMode = existing != null
                    ? existing.GetComponent<Button>()
                    : CreateAIStyleButton(tabsBarContainer.transform, "BtnTabAIStyleMode", "AUTO STYLE", cyanHighlight);
            }

            LayoutAIStyleTabs();

            if (aiStyleControlContainer == null)
            {
                Transform parent = leverageControlContainer.transform.parent;
                Transform existing = parent.Find("Container_AIStyleMode");
                aiStyleControlContainer = existing != null
                    ? existing.gameObject
                    : CreateAIStyleContainer(parent);
            }

            if (aiStyleControlContainer == null) return;

            btnPresetSafe ??= FindButton(aiStyleControlContainer.transform, "BtnPresetSafe");
            btnPresetBalanced ??= FindButton(aiStyleControlContainer.transform, "BtnPresetBalanced");
            btnPresetAggressive ??= FindButton(aiStyleControlContainer.transform, "BtnPresetAggressive");
            SetButtonLabel(btnTabAIStyleMode, "AUTO STYLE");
            SetButtonLabel(btnPresetSafe, "SAFE");
            SetButtonLabel(btnPresetBalanced, "BALANCE");
            SetButtonLabel(btnPresetAggressive, "AGGRESSIVE");
            if (aiStyleDescText == null)
            {
                Transform desc = aiStyleControlContainer.transform.Find("AIStyleDescription/Label");
                if (desc != null) aiStyleDescText = desc.GetComponent<TMP_Text>();
            }

            aiStyleControlContainer.SetActive(false);
        }

        private void LayoutAIStyleTabs()
        {
            LayoutTab(btnTabLeverageMode, 0f, 0.32f);
            LayoutTab(btnTabMarginRatioMode, 0.34f, 0.66f);
            LayoutTab(btnTabAIStyleMode, 0.68f, 1f);
        }

        private static void LayoutTab(Button button, float minX, float maxX)
        {
            if (button == null) return;
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(minX, 0f);
            rect.anchorMax = new Vector2(maxX, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private GameObject CreateAIStyleContainer(Transform parent)
        {
            GameObject container = new("Container_AIStyleMode", typeof(RectTransform));
            container.transform.SetParent(parent, false);
            RectTransform rect = container.GetComponent<RectTransform>();
            RectTransform source = leverageControlContainer.GetComponent<RectTransform>();
            rect.anchorMin = source.anchorMin;
            rect.anchorMax = source.anchorMax;
            rect.offsetMin = source.offsetMin;
            rect.offsetMax = source.offsetMax;

            GameObject presets = new("AIStylePresets", typeof(RectTransform));
            presets.transform.SetParent(container.transform, false);
            SetRuntimeRect(presets.GetComponent<RectTransform>(), new Vector2(0f, 0.49f), Vector2.one,
                new Vector2(0f, 2f), new Vector2(0f, -2f));

            btnPresetSafe = CreateAIStyleButton(presets.transform, "BtnPresetSafe", "SAFE", bullishColor);
            btnPresetBalanced = CreateAIStyleButton(presets.transform, "BtnPresetBalanced", "BALANCE", balancedColor);
            btnPresetAggressive = CreateAIStyleButton(presets.transform, "BtnPresetAggressive", "AGGRESSIVE", bearishColor);
            LayoutPreset(btnPresetSafe, 0f, 0.315f);
            LayoutPreset(btnPresetBalanced, 0.3425f, 0.6575f);
            LayoutPreset(btnPresetAggressive, 0.685f, 1f);

            GameObject descPanel = new("AIStyleDescription", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            descPanel.transform.SetParent(container.transform, false);
            SetRuntimeRect(descPanel.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1f, 0.42f),
                new Vector2(0f, 2f), Vector2.zero);
            descPanel.GetComponent<Image>().color = aiStylePanelColor;
            Outline descOutline = descPanel.GetComponent<Outline>();
            descOutline.effectColor = new Color(cyanHighlight.r, cyanHighlight.g, cyanHighlight.b, 0.45f);
            descOutline.effectDistance = UIStrokeStyle.EffectDistance;

            GameObject labelObject = new("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(descPanel.transform, false);
            SetRuntimeRect(labelObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
                new Vector2(12f, 5f), new Vector2(-12f, -5f));
            aiStyleDescText = labelObject.GetComponent<TextMeshProUGUI>();
            aiStyleDescText.font = TMP_Settings.defaultFontAsset;
            aiStyleDescText.fontSize = 15f;
            aiStyleDescText.enableAutoSizing = true;
            aiStyleDescText.fontSizeMin = 11f;
            aiStyleDescText.fontSizeMax = 15f;
            aiStyleDescText.alignment = TextAlignmentOptions.Center;
            aiStyleDescText.color = new Color32(207, 250, 254, 255);
            aiStyleDescText.textWrappingMode = TextWrappingModes.Normal;
            aiStyleDescText.overflowMode = TextOverflowModes.Ellipsis;
            aiStyleDescText.raycastTarget = false;

            return container;
        }

        private Button CreateAIStyleButton(Transform parent, string objectName, string label, Color accent)
        {
            GameObject buttonObject = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline));
            buttonObject.transform.SetParent(parent, false);
            Image image = buttonObject.GetComponent<Image>();
            image.color = inactivePresetColor;
            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.78f, 0.85f, 0.9f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            Outline outline = buttonObject.GetComponent<Outline>();
            outline.effectColor = new Color(accent.r, accent.g, accent.b, 0.8f);
            outline.effectDistance = UIStrokeStyle.EffectDistance;

            GameObject textObject = new("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(buttonObject.transform, false);
            SetRuntimeRect(textObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
                new Vector2(5f, 3f), new Vector2(-5f, -3f));
            TMP_Text text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = label;
            text.fontSize = objectName == "BtnTabAIStyleMode" ? 17f : 15f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 10f;
            text.fontSizeMax = text.fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return button;
        }

        private static void LayoutPreset(Button button, float minX, float maxX)
        {
            if (button == null) return;
            SetRuntimeRect(button.GetComponent<RectTransform>(), new Vector2(minX, 0f), new Vector2(maxX, 1f),
                Vector2.zero, Vector2.zero);
        }

        private static void SetRuntimeRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            if (rect == null) return;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
        }

        private static Button FindButton(Transform root, string objectName)
        {
            Transform found = root.Find($"AIStylePresets/{objectName}");
            return found != null ? found.GetComponent<Button>() : null;
        }

        private static void SetButtonLabel(Button button, string label)
        {
            if (button == null) return;
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null) text.text = label;
        }

        private void OnDestroy()
        {
            if (tradingController != null)
            {
                tradingController.OnPositionChanged -= RefreshPanelUI;
                tradingController.OnPositionLiquidated -= HandlePositionLiquidated;
                tradingController.OnPositionOpened -= HandlePositionOpened;
                tradingController.OnPositionClosed -= HandlePositionClosed;
                tradingController.OnAITradingStyleChanged -= OnAITradingStyleChangedCallback;
            }
            if (activeItemManager != null)
                activeItemManager.OnActiveItemsChanged -= RefreshEffectStatusHUD;
        }

        private void Update()
        {
            // 포지션 보유 중일 때 매 프레임 실시간 ROE 및 PnL 숫자 갱신
            if (tradingController != null && tradingController.CurrentPosition != TradingController.PositionType.None)
            {
                UpdatePositionStatusNumbers();
            }

            // 슬라이더를 드래그한 경우 동기화
            if (marginPercentageSlider != null && gameManager != null)
            {
                if (Mathf.Abs(marginPercentageSlider.value - selectedMarginPercentage) > 0.005f)
                {
                    int roundedPercent = Mathf.RoundToInt(marginPercentageSlider.value * 100f);
                    SelectMarginRatio(roundedPercent);
                }
            }

            // 실시간 자본금 변화에 맞춰 투자 비율 금액 텍스트 동적 갱신
            if (gameManager != null)
            {
                float marginAmount = gameManager.CurrentBalance * selectedMarginPercentage;
                if (marginRatioDisplayText != null)
                {
                    marginRatioDisplayText.text = $"{currentSelectedMarginPercent}% (${marginAmount:N0})";
                }
                if (marginAmountText != null)
                {
                    marginAmountText.text = $"투입: ${marginAmount:N0} ({currentSelectedMarginPercent}%)";
                }
            }

            UpdateTradeCooldownUI();
            if (activeItemManager == null) BindActiveItemManager();
            if (Time.unscaledTime >= nextEffectStatusRefreshTime)
            {
                nextEffectStatusRefreshTime = Time.unscaledTime + 0.2f;
                RefreshEffectStatusHUD();
            }
            UpdateVolatilityGimmicks();
        }

        private void BuildEffectStatusHUD()
        {
            DynamicInventoryUI inventoryUI = FindAnyObjectByType<DynamicInventoryUI>();
            inventoryPanelRect = inventoryUI != null ? inventoryUI.GetComponent<RectTransform>() : null;
            Canvas rootCanvas = GetComponentInParent<Canvas>();
            Transform hudParent = inventoryPanelRect != null && inventoryPanelRect.parent != null
                ? inventoryPanelRect.parent
                : rootCanvas != null ? rootCanvas.transform : transform;
            Transform existing = hudParent.Find("EffectStatusHUD");
            GameObject panelObject;
            if (existing == null)
            {
                panelObject = new GameObject("EffectStatusHUD", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(LayoutElement));
                panelObject.transform.SetParent(hudParent, false);
            }
            else panelObject = existing.gameObject;

            effectStatusPanel = panelObject.GetComponent<RectTransform>();
            effectStatusPanel.anchorMin = effectStatusPanel.anchorMax = new Vector2(1f, 0f);
            effectStatusPanel.pivot = new Vector2(1f, 0f);
            Canvas effectCanvas = panelObject.GetComponent<Canvas>();
            if (effectCanvas == null) effectCanvas = panelObject.AddComponent<Canvas>();
            effectCanvas.overrideSorting = true;
            // 캐릭터/요미 레이어보다 위, 상점(100)과 각종 모달 UI보다는 아래에 둡니다.
            effectCanvas.sortingOrder = 80;
            LayoutElement layout = panelObject.GetComponent<LayoutElement>();
            layout.ignoreLayout = true;
            Image background = panelObject.GetComponent<Image>();
            background.color = new Color(0.025f, 0.045f, 0.082f, 0.96f);
            background.raycastTarget = false;
            Outline outline = panelObject.GetComponent<Outline>();
            outline.effectColor = new Color(cyanHighlight.r, cyanHighlight.g, cyanHighlight.b, 0.72f);
            outline.effectDistance = UIStrokeStyle.EffectDistance;

            VerticalLayoutGroup oldVerticalLayout = panelObject.GetComponent<VerticalLayoutGroup>();
            if (oldVerticalLayout != null) oldVerticalLayout.enabled = false;
            HorizontalLayoutGroup iconLayout = panelObject.GetComponent<HorizontalLayoutGroup>();
            if (iconLayout == null) iconLayout = panelObject.AddComponent<HorizontalLayoutGroup>();
            iconLayout.padding = new RectOffset(8, 8, 8, 8);
            iconLayout.spacing = 6f;
            iconLayout.childAlignment = TextAnchor.MiddleRight;
            iconLayout.childControlWidth = true;
            iconLayout.childControlHeight = true;
            iconLayout.childForceExpandWidth = false;
            iconLayout.childForceExpandHeight = true;

            Transform obsoleteText = panelObject.transform.Find("StatusText");
            if (obsoleteText != null)
            {
                obsoleteText.gameObject.SetActive(false);
                Destroy(obsoleteText.gameObject);
            }
            pastaItem = Resources.Load<ItemData>("Items/Food/Pasta");
            LayoutEffectStatusHUD();
            panelObject.transform.SetAsLastSibling();
        }

        private void LayoutEffectStatusHUD()
        {
            if (effectStatusPanel == null) return;
            if (inventoryPanelRect == null)
            {
                DynamicInventoryUI inventoryUI = FindAnyObjectByType<DynamicInventoryUI>();
                inventoryPanelRect = inventoryUI != null ? inventoryUI.GetComponent<RectTransform>() : null;
            }

            const float gapFromInventory = 12f;
            float inventoryWidth = inventoryPanelRect != null ? inventoryPanelRect.rect.width : 436f;
            float inventoryRightOffset = inventoryPanelRect != null
                ? -inventoryPanelRect.anchoredPosition.x
                : UIStrokeStyle.ScreenEdgeMargin;
            float bottomOffset = inventoryPanelRect != null
                ? inventoryPanelRect.anchoredPosition.y
                : UIStrokeStyle.ScreenEdgeMargin;
            effectStatusPanel.anchoredPosition = new Vector2(
                -(inventoryRightOffset + inventoryWidth + gapFromInventory),
                bottomOffset);
        }

        private void BindActiveItemManager()
        {
            ActiveItemEffectManager manager = ActiveItemEffectManager.Instance;
            if (manager == activeItemManager) return;
            if (activeItemManager != null)
                activeItemManager.OnActiveItemsChanged -= RefreshEffectStatusHUD;
            activeItemManager = manager;
            if (activeItemManager != null)
                activeItemManager.OnActiveItemsChanged += RefreshEffectStatusHUD;
            RefreshEffectStatusHUD();
        }

        private void RefreshEffectStatusHUD()
        {
            if (effectStatusPanel == null) return;

            // 인벤토리 옆 효과 HUD는 시간제 파스타 효과만 표시합니다.
            // 영구 액티브 장비 효과는 실제 계산에는 유지하되 이 HUD에서는 노출하지 않습니다.
            activeItemStates.Clear();
            float pastaSeconds = DeliveryFoodManager.Instance != null
                ? DeliveryFoodManager.Instance.PastaRemainingSeconds
                : 0f;
            bool hasPasta = pastaSeconds > 0.05f;
            effectStatusPanel.gameObject.SetActive(hasPasta);
            if (!hasPasta) return;
            LayoutEffectStatusHUD();

            const string signature = "pasta|";
            if (signature != effectIconSignature)
            {
                effectIconSignature = signature;
                RebuildEffectIcons(hasPasta);
            }

            foreach (EffectIconView view in effectIconViews)
            {
                if (view.IsFood && view.TimerFill != null)
                {
                    float remainingRatio = Mathf.Clamp01(pastaSeconds / DeliveryFoodManager.PastaDurationSeconds);
                    view.TimerFill.fillAmount = 1f - remainingRatio;
                }
            }
        }

        private void RebuildEffectIcons(bool hasPasta)
        {
            for (int i = effectStatusPanel.childCount - 1; i >= 0; i--)
            {
                Transform child = effectStatusPanel.GetChild(i);
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            effectIconViews.Clear();

            if (hasPasta && pastaItem != null)
                effectIconViews.Add(CreateEffectIcon(pastaItem, true, string.Empty, new Color(1f, 0.61f, 0.27f, 1f)));
            effectStatusPanel.sizeDelta = new Vector2(
                16f + effectIconViews.Count * 64f + Mathf.Max(0, effectIconViews.Count - 1) * 6f,
                80f);
        }

        private EffectIconView CreateEffectIcon(ItemData item, bool isFood, string badgeLabel, Color accent)
        {
            GameObject card = new($"EffectIcon_{item.ItemId}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(LayoutElement));
            card.transform.SetParent(effectStatusPanel, false);
            LayoutElement size = card.GetComponent<LayoutElement>();
            size.preferredWidth = 64f;
            size.minWidth = 54f;
            size.preferredHeight = 64f;
            size.minHeight = 54f;
            Image cardBackground = card.GetComponent<Image>();
            cardBackground.color = new Color(0.055f, 0.09f, 0.15f, 1f);
            cardBackground.raycastTarget = false;
            Outline cardOutline = card.GetComponent<Outline>();
            cardOutline.effectColor = new Color(accent.r, accent.g, accent.b, 0.82f);
            cardOutline.effectDistance = UIStrokeStyle.EffectDistance;

            GameObject iconObject = new("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.transform.SetParent(card.transform, false);
            Image icon = iconObject.GetComponent<Image>();
            icon.sprite = item.Icon;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            SetRuntimeRect(icon.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(7f, isFood ? 5f : 18f), new Vector2(-7f, -4f));

            if (isFood)
            {
                GameObject fillObject = new("TimeProgressFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                fillObject.transform.SetParent(card.transform, false);
                Image timerFill = fillObject.GetComponent<Image>();
                timerFill.sprite = null;
                timerFill.color = new Color(1f, 0.43f, 0.12f, 0.38f);
                timerFill.type = Image.Type.Filled;
                timerFill.fillMethod = Image.FillMethod.Vertical;
                timerFill.fillOrigin = (int)Image.OriginVertical.Bottom;
                timerFill.fillAmount = 0f;
                timerFill.raycastTarget = false;
                SetRuntimeRect(timerFill.rectTransform, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
                return new EffectIconView { IsFood = true, TimerFill = timerFill };
            }

            GameObject badgeObject = new("Badge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            badgeObject.transform.SetParent(card.transform, false);
            Image badgeBackground = badgeObject.GetComponent<Image>();
            badgeBackground.color = new Color(0.015f, 0.027f, 0.05f, 0.94f);
            badgeBackground.raycastTarget = false;
            SetRuntimeRect(badgeObject.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1f, 0f),
                new Vector2(3f, 3f), new Vector2(-3f, 19f));

            GameObject labelObject = new("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(badgeObject.transform, false);
            TMP_Text badge = labelObject.GetComponent<TextMeshProUGUI>();
            badge.font = TMP_Settings.defaultFontAsset;
            badge.text = badgeLabel;
            badge.fontSize = 13f;
            badge.fontStyle = FontStyles.Bold;
            badge.alignment = TextAlignmentOptions.Center;
            badge.color = accent;
            badge.textWrappingMode = TextWrappingModes.NoWrap;
            badge.raycastTarget = false;
            SetRuntimeRect(badge.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            return new EffectIconView { IsFood = false, Badge = badge };
        }

        private void UpdateVolatilityGimmicks()
        {
            var marketEngine = FindAnyObjectByType<MarketSimulationEngine>();
            if (marketEngine == null || gameManager == null) return;

            // 1. 서버 렉 UI 처리
            if (marketEngine.IsServerLagging && !isLagUIVisible)
            {
                isLagUIVisible = true;
                if (lagUIPanel != null)
                {
                    lagUIPanel.transform.SetAsLastSibling(); // 최상단 노출
                    lagUIPanel.SetActive(true);
                }
            }
            else if (!marketEngine.IsServerLagging && isLagUIVisible)
            {
                isLagUIVisible = false;
                if (lagUIPanel != null) lagUIPanel.SetActive(false);
            }

            // 2. 환각 기믹 (16일차 이상 + 멘탈 20% 미만)
            if (gameManager.CurrentDay >= 16 && traderStatus != null && traderStatus.CurrentMental < traderStatus.MaxMental * 0.2f)
            {
                if (hallucinationCoroutine == null)
                {
                    hallucinationCoroutine = StartCoroutine(HallucinationRoutine());
                }

                // 버튼 회피 기믹 (마우스 오버 시 도망감 - 도망가는 거리는 무작위)
                EvadeButtonIfHovered(longButton);
                EvadeButtonIfHovered(shortButton);
                EvadeButtonIfHovered(closePositionButton);
            }
            else
            {
                if (hallucinationCoroutine != null)
                {
                    StopCoroutine(hallucinationCoroutine);
                    hallucinationCoroutine = null;
                }
            }
        }

        private void EvadeButtonIfHovered(Button btn)
        {
            if (btn == null) return;
            var rect = btn.GetComponent<RectTransform>();
            if (rect == null) return;

            Vector2 originalPos = Vector2.zero;
            if (originalButtonPositions.TryGetValue(btn, out Vector2 pos))
            {
                originalPos = pos;
            }
            else
            {
                return;
            }

            // 간단한 마우스 위치 기반 회피 (PointerEnter 이벤트를 쓰지 않고 단순 거리 체크)
            Vector2 mousePos = Input.mousePosition;
            Vector2 localMousePos;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)rect.parent, mousePos, null, out localMousePos))
            {
                float dist = Vector2.Distance(rect.anchoredPosition, localMousePos);
                if (dist < 100f) // 마우스가 가까이 오면
                {
                    Vector2 evadeDir = (rect.anchoredPosition - localMousePos).normalized;
                    if (evadeDir == Vector2.zero) evadeDir = new Vector2(UnityEngine.Random.Range(-1f,1f), UnityEngine.Random.Range(-1f,1f)).normalized;
                    
                    // Time.deltaTime을 곱해 너무 빠르게 튕겨나가지 않도록 조정
                    rect.anchoredPosition += evadeDir * UnityEngine.Random.Range(500f, 800f) * Time.deltaTime;
                    
                    // 화면 밖으로 나가지 않도록 원래 위치로 서서히 끌어당김
                    rect.anchoredPosition = Vector3.Lerp(rect.anchoredPosition, originalPos, Time.deltaTime * 2f);
                }
                else
                {
                    // 마우스가 멀어지면 원래 위치로 복귀
                    if (Vector2.Distance(rect.anchoredPosition, originalPos) > 1f)
                    {
                        rect.anchoredPosition = Vector3.Lerp(rect.anchoredPosition, originalPos, Time.deltaTime * 5f);
                    }
                }
            }
            else
            {
                if (Vector2.Distance(rect.anchoredPosition, originalPos) > 1f)
                {
                    rect.anchoredPosition = Vector3.Lerp(rect.anchoredPosition, originalPos, Time.deltaTime * 5f);
                }
            }
        }

        private IEnumerator HallucinationRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(UnityEngine.Random.Range(2f, 5f));
                
                // 버튼 색상 반전 (Long <-> Short)
                if (longButton != null && shortButton != null)
                {
                    var longImg = longButton.GetComponent<Image>();
                    var shortImg = shortButton.GetComponent<Image>();
                    
                    if (longImg != null && shortImg != null)
                    {
                        Color temp = longImg.color;
                        longImg.color = shortImg.color;
                        shortImg.color = temp;
                        
                        yield return new WaitForSeconds(UnityEngine.Random.Range(0.2f, 0.5f));
                        
                        temp = longImg.color;
                        longImg.color = shortImg.color;
                        shortImg.color = temp;
                    }
                }
            }
        }

        private void SetupButtons()
        {
            if (longButton != null) longButton.onClick.AddListener(OnLongButtonClicked);
            if (shortButton != null) shortButton.onClick.AddListener(OnShortButtonClicked);
            if (closePositionButton != null) closePositionButton.onClick.AddListener(OnCloseButtonClicked);

            // 모드 전환 탭 버튼 바인딩
            if (btnTabLeverageMode != null) btnTabLeverageMode.onClick.AddListener(() => SwitchControlMode(ControlMode.Leverage));
            if (btnTabMarginRatioMode != null) btnTabMarginRatioMode.onClick.AddListener(() => SwitchControlMode(ControlMode.MarginRatio));
            if (btnTabAIStyleMode != null) btnTabAIStyleMode.onClick.AddListener(() => SwitchControlMode(ControlMode.AIStyle));

            // AI 성향 바인딩
            if (btnPresetSafe != null) btnPresetSafe.onClick.AddListener(() => SelectAITradingStyle(TradingController.AITradingStyle.Safe));
            if (btnPresetBalanced != null) btnPresetBalanced.onClick.AddListener(() => SelectAITradingStyle(TradingController.AITradingStyle.Balanced));
            if (btnPresetAggressive != null) btnPresetAggressive.onClick.AddListener(() => SelectAITradingStyle(TradingController.AITradingStyle.Aggressive));

            // 레버리지 조작 버튼 바인딩
            if (btnLeverageMinus != null) btnLeverageMinus.onClick.AddListener(() => SelectLeverage(currentSelectedLeverage - 1));
            if (btnLeveragePlus != null) btnLeveragePlus.onClick.AddListener(() => SelectLeverage(currentSelectedLeverage + 1));

            if (btnPreset1x != null) btnPreset1x.onClick.AddListener(() => SelectLeverage(1));
            if (btnPreset5x != null) btnPreset5x.onClick.AddListener(() => SelectLeverage(5));
            if (btnPreset10x != null) btnPreset10x.onClick.AddListener(() => SelectLeverage(10));
            if (btnPreset25x != null) btnPreset25x.onClick.AddListener(() => SelectLeverage(25));
            if (btnPreset50x != null) btnPreset50x.onClick.AddListener(() => SelectLeverage(50));
            if (btnPreset100x != null) btnPreset100x.onClick.AddListener(() => SelectLeverage(100));
            if (btnPreset125x != null) btnPreset125x.onClick.AddListener(() => SelectLeverage(125));

            // 투자 사용 비율(Margin Ratio) 10% 단위 증감 및 프리셋 바인딩
            if (btnMarginRatioMinus != null) btnMarginRatioMinus.onClick.AddListener(() => SelectMarginRatio(currentSelectedMarginPercent - 10));
            if (btnMarginRatioPlus != null) btnMarginRatioPlus.onClick.AddListener(() => SelectMarginRatio(currentSelectedMarginPercent + 10));

            if (btnPresetRatio10 != null) btnPresetRatio10.onClick.AddListener(() => SelectMarginRatio(10));
            if (btnPresetRatio25 != null) btnPresetRatio25.onClick.AddListener(() => SelectMarginRatio(25));
            if (btnPresetRatio50 != null) btnPresetRatio50.onClick.AddListener(() => SelectMarginRatio(50));
            if (btnPresetRatio75 != null) btnPresetRatio75.onClick.AddListener(() => SelectMarginRatio(75));
            if (btnPresetRatio100 != null) btnPresetRatio100.onClick.AddListener(() => SelectMarginRatio(100));

            if (marginPercentageSlider != null)
            {
                marginPercentageSlider.minValue = 0.10f;
                marginPercentageSlider.maxValue = 1f;
                marginPercentageSlider.value = 0.30f;
            }
        }

        // 1. 우측 상단 탭 모드 전환
        public void SwitchControlMode(ControlMode mode)
        {
            currentControlMode = mode;

            bool hasPosition = tradingController != null && tradingController.CurrentPosition != TradingController.PositionType.None;

            if (leverageControlContainer != null)
            {
                leverageControlContainer.SetActive(!hasPosition && mode == ControlMode.Leverage);
            }

            if (marginRatioControlContainer != null)
            {
                marginRatioControlContainer.SetActive(!hasPosition && mode == ControlMode.MarginRatio);
            }

            if (aiStyleControlContainer != null)
            {
                aiStyleControlContainer.SetActive(!hasPosition && mode == ControlMode.AIStyle);
            }

            // 탭 버튼 하이라이트 색상 갱신
            UpdatePresetHighlight(btnTabLeverageMode, mode == ControlMode.Leverage);
            UpdatePresetHighlight(btnTabMarginRatioMode, mode == ControlMode.MarginRatio);
            UpdatePresetHighlight(btnTabAIStyleMode, mode == ControlMode.AIStyle);
        }

        private void OnAITradingStyleChangedCallback(TradingController.AITradingStyle style)
        {
            UpdateAIStyleButton(btnPresetSafe, bullishColor, style == TradingController.AITradingStyle.Safe);
            UpdateAIStyleButton(btnPresetBalanced, balancedColor, style == TradingController.AITradingStyle.Balanced);
            UpdateAIStyleButton(btnPresetAggressive, bearishColor, style == TradingController.AITradingStyle.Aggressive);

            if (aiStyleDescText != null)
            {
                aiStyleDescText.text = style switch
                {
                    TradingController.AITradingStyle.Safe => "LOW LEVERAGE · 소액 진입 · 강한 신호만 거래",
                    TradingController.AITradingStyle.Balanced => "BALANCED · 시장 신호에 맞춘 기본 자동매매",
                    TradingController.AITradingStyle.Aggressive => "HIGH LEVERAGE · 높은 증거금 · 손절 최소화",
                    _ => ""
                };
            }
        }

        private void UpdateAIStyleButton(Button button, Color accent, bool selected)
        {
            if (button == null) return;
            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = selected
                    ? new Color(accent.r * 0.68f, accent.g * 0.68f, accent.b * 0.68f, 1f)
                    : inactivePresetColor;
            }
            Outline outline = button.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = new Color(accent.r, accent.g, accent.b, selected ? 1f : 0.62f);
                outline.effectDistance = UIStrokeStyle.EffectDistance;
            }
        }

        public void SelectAITradingStyle(TradingController.AITradingStyle style)
        {
            if (tradingController != null)
            {
                tradingController.SetAITradingStyle(style);
            }
        }

        // 2. 투자 사용 비율 설정 (10% 단위 또는 프리셋)
        public void SelectMarginRatio(int percent)
        {
            float maxAllowedRatio = TraderLevelSystem.Instance != null ? TraderLevelSystem.Instance.GetMaxAllowedMarginRatio() : 1.0f;
            int maxAllowedPercent = Mathf.RoundToInt(maxAllowedRatio * 100f);
            if (percent > maxAllowedPercent)
            {
                currentSelectedMarginPercent = maxAllowedPercent;
            }
            else
            {
                currentSelectedMarginPercent = Mathf.Clamp(percent, 10, 100);
            }
            selectedMarginPercentage = currentSelectedMarginPercent / 100f;

            if (marginPercentageSlider != null)
            {
                marginPercentageSlider.value = selectedMarginPercentage;
            }

            float marginAmount = gameManager != null ? gameManager.CurrentBalance * selectedMarginPercentage : 0f;

            if (marginRatioDisplayText != null)
            {
                marginRatioDisplayText.text = $"{currentSelectedMarginPercent}% (${marginAmount:N0})";
            }

            if (marginAmountText != null)
            {
                marginAmountText.text = $"MARGIN: ${marginAmount:N0} ({currentSelectedMarginPercent}%)";
            }

            // 프리셋 비율 버튼 하이라이트
            UpdatePresetHighlight(btnPresetRatio10, currentSelectedMarginPercent == 10);
            UpdatePresetHighlight(btnPresetRatio25, currentSelectedMarginPercent == 25);
            UpdatePresetHighlight(btnPresetRatio50, currentSelectedMarginPercent == 50);
            UpdatePresetHighlight(btnPresetRatio75, currentSelectedMarginPercent == 75);
            UpdatePresetHighlight(btnPresetRatio100, currentSelectedMarginPercent == 100);
        }

        // 3. 레버리지 배율 선택
        public void SelectLeverage(int lev)
        {
            int maxAllowed = TraderLevelSystem.Instance != null ? TraderLevelSystem.Instance.GetMaxAllowedLeverage() : 125;
            if (lev > maxAllowed)
            {
                currentSelectedLeverage = maxAllowed;
            }
            else
            {
                currentSelectedLeverage = Mathf.Clamp(lev, 1, 125);
            }

            if (leverageDisplayText != null)
            {
                leverageDisplayText.text = $"{currentSelectedLeverage}x";
            }

            // 프리셋 버튼 하이라이트
            UpdatePresetHighlight(btnPreset1x, currentSelectedLeverage == 1);
            UpdatePresetHighlight(btnPreset5x, currentSelectedLeverage == 5);
            UpdatePresetHighlight(btnPreset10x, currentSelectedLeverage == 10);
            UpdatePresetHighlight(btnPreset25x, currentSelectedLeverage == 25);
            UpdatePresetHighlight(btnPreset50x, currentSelectedLeverage == 50);
            UpdatePresetHighlight(btnPreset100x, currentSelectedLeverage == 100);
            UpdatePresetHighlight(btnPreset125x, currentSelectedLeverage == 125);
        }

        private void UpdatePresetHighlight(Button btn, bool isSelected)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.color = isSelected ? cyanHighlight : inactivePresetColor;
            }
        }

        // 플레이어 직접 포지션 진입. 보유 중인 포지션은 전용 매도 버튼으로만 정리합니다.
        private void OnLongButtonClicked()
        {
            if (tradingController == null) return;
            if (tradingController.CurrentPosition != TradingController.PositionType.None)
            {
                Debug.Log("[TradingPanelUIController] 포지션 보유 중에는 LONG 재클릭을 무시합니다. 포지션 매도 버튼을 사용하세요.");
                return;
            }

            tradingController.OpenPlayerPosition(TradingController.PositionType.Long, selectedMarginPercentage, currentSelectedLeverage);
        }

        private void OnShortButtonClicked()
        {
            if (tradingController == null) return;
            if (tradingController.CurrentPosition != TradingController.PositionType.None)
            {
                Debug.Log("[TradingPanelUIController] 포지션 보유 중에는 SHORT 재클릭을 무시합니다. 포지션 매도 버튼을 사용하세요.");
                return;
            }

            tradingController.OpenPlayerPosition(TradingController.PositionType.Short, selectedMarginPercentage, currentSelectedLeverage);
        }

        private void OnCloseButtonClicked()
        {
            if (tradingController != null && tradingController.CurrentPosition != TradingController.PositionType.None)
            {
                Debug.Log("[TradingPanelUIController] 플레이어가 직접 포지션 종료(청산) 버튼을 클릭했습니다.");
                tradingController.ClosePlayerPosition();
            }
        }

        public void RefreshPanelUI()
        {
            if (tradingController == null) return;

            bool hasPosition = tradingController.CurrentPosition != TradingController.PositionType.None;
            bool isManualMode = tradingController.ActiveTradingMode == TradingController.TradingMode.Player_Manual;
            bool showPlayerSellButton = hasPosition && isManualMode;
            bool isTradeCooldown = isManualMode && !hasPosition && tradingController.IsPlayerTradeOnCooldown;

            // 진입 버튼은 포지션이 없을 때만 동작하고, 보유 중에는 전용 매도 버튼이 위를 덮습니다.
            if (longButton != null) longButton.interactable = isManualMode && !hasPosition && !isTradeCooldown;
            if (shortButton != null) shortButton.interactable = isManualMode && !hasPosition && !isTradeCooldown;
            if (closePositionButton != null)
            {
                closePositionButton.gameObject.SetActive(showPlayerSellButton);
                closePositionButton.interactable = showPlayerSellButton;
                if (showPlayerSellButton) closePositionButton.transform.SetAsLastSibling();
            }

            if (longSubtitleText != null)
            {
                longSubtitleText.text = isTradeCooldown
                    ? "재진입 대기 중"
                    : hasPosition
                    ? (isManualMode ? "포지션 매도 버튼 사용" : (tradingController.CurrentPosition == TradingController.PositionType.Long ? "LONG 보유중 (AUTO)" : "대기중"))
                    : (isManualMode ? "LONG 수동 매수" : "자동 매수 대기");
            }
            if (shortSubtitleText != null)
            {
                shortSubtitleText.text = isTradeCooldown
                    ? "재진입 대기 중"
                    : hasPosition
                    ? (isManualMode ? "포지션 매도 버튼 사용" : (tradingController.CurrentPosition == TradingController.PositionType.Short ? "SHORT 보유중 (AUTO)" : "대기중"))
                    : (isManualMode ? "SHORT 수동 매도" : "자동 매도 대기");
            }

            UpdateTradeCooldownUI();

            // 상태 오버레이 패널 및 탭 바 표시 여부
            if (positionStatusPanel != null)
            {
                positionStatusPanel.SetActive(hasPosition);
            }
            if (tabsBarContainer != null)
            {
                tabsBarContainer.SetActive(!hasPosition);
            }
            if (leverageControlContainer != null)
            {
                leverageControlContainer.SetActive(!hasPosition && currentControlMode == ControlMode.Leverage);
            }
            if (marginRatioControlContainer != null)
            {
                marginRatioControlContainer.SetActive(!hasPosition && currentControlMode == ControlMode.MarginRatio);
            }
            if (aiStyleControlContainer != null)
            {
                aiStyleControlContainer.SetActive(!hasPosition && currentControlMode == ControlMode.AIStyle);
            }

            if (hasPosition)
            {
                UpdatePositionStatusNumbers();
                if (positionTypeText != null)
                {
                    string ownerTag = tradingController.CurrentOwner == TradingController.OwnerType.Player ? "[플레이어]" : "[AUTO]";
                    positionTypeText.text = $"{ownerTag} {tradingController.CurrentPosition} {tradingController.CurrentLeverage}x";
                    positionTypeText.color = tradingController.CurrentPosition == TradingController.PositionType.Long 
                        ? bullishColor : bearishColor;
                }
                if (entryPriceText != null) entryPriceText.text = $"ENTRY: ${tradingController.EntryPrice:N1}";
                if (targetPriceText != null)
                {
                    if (tradingController.CurrentOwner == TradingController.OwnerType.Player)
                    {
                        targetPriceText.text = "TARGET: 직접 판단 익절 (수동)";
                    }
                    else
                    {
                        targetPriceText.text = tradingController.TargetPrice > 0f 
                            ? $"TARGET: ${tradingController.TargetPrice:N1} (AI 목표가)" 
                            : "TARGET: 무제한 (Overdose 뇌동매매)";
                    }
                }
                if (liquidationPriceText != null)
                {
                    if (tradingController.CurrentOwner == TradingController.OwnerType.Player)
                    {
                        liquidationPriceText.text = $"LIQ: ${tradingController.LiquidationPrice:N1} | STOP: 직접 판단 손절 (수동)";
                    }
                    else
                    {
                        string stopText = tradingController.StopLossPrice > 0f ? $"${tradingController.StopLossPrice:N1}" : "없음";
                        liquidationPriceText.text = $"LIQ: ${tradingController.LiquidationPrice:N1} | STOP: {stopText}";
                    }
                }
            }
        }

        private void UpdatePositionStatusNumbers()
        {
            if (tradingController == null) return;

            float roe = tradingController.CalculateROEPercentage();
            float pnl = tradingController.CalculateUnrealizedPnL();

            if (roeText != null)
            {
                string sign = roe >= 0 ? "+" : "";
                roeText.text = $"{sign}{roe:F2}%";
                roeText.color = roe >= 0 ? bullishColor : bearishColor;
            }

            if (pnlText != null)
            {
                string sign = pnl >= 0 ? "+" : "";
                pnlText.text = $"PnL: {sign}${pnl:N2}";
                pnlText.color = pnl >= 0 ? bullishColor : bearishColor;
            }
        }

        private void BuildTradeCooldownUI()
        {
            if (closePositionButton == null) return;

            Transform parent = closePositionButton.transform.parent;
            Transform existing = parent.Find("TradeCooldownOverlay");
            if (existing != null)
            {
                tradeCooldownOverlay = existing.gameObject;
                tradeCooldownText = existing.GetComponentInChildren<TMP_Text>(true);
                return;
            }

            GameObject overlay = new("TradeCooldownOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            overlay.transform.SetParent(parent, false);

            RectTransform sourceRect = closePositionButton.GetComponent<RectTransform>();
            RectTransform overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = sourceRect.anchorMin;
            overlayRect.anchorMax = sourceRect.anchorMax;
            overlayRect.pivot = sourceRect.pivot;
            overlayRect.anchoredPosition = sourceRect.anchoredPosition;
            overlayRect.sizeDelta = sourceRect.sizeDelta;
            overlayRect.localRotation = sourceRect.localRotation;
            overlayRect.localScale = sourceRect.localScale;

            Image background = overlay.GetComponent<Image>();
            background.color = new Color32(15, 23, 42, 224);
            background.raycastTarget = false;

            Outline outline = overlay.GetComponent<Outline>();
            outline.effectColor = new Color32(255, 255, 255, 110);
            outline.effectDistance = UIStrokeStyle.EffectDistance;

            GameObject textObject = new("CooldownText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(overlay.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(6f, 4f);
            textRect.offsetMax = new Vector2(-6f, -4f);

            tradeCooldownText = textObject.GetComponent<TextMeshProUGUI>();
            tradeCooldownText.font = TMP_Settings.defaultFontAsset;
            tradeCooldownText.fontSize = 25f;
            tradeCooldownText.fontStyle = FontStyles.Bold;
            tradeCooldownText.alignment = TextAlignmentOptions.Center;
            tradeCooldownText.color = Color.white;
            tradeCooldownText.raycastTarget = false;
            tradeCooldownText.textWrappingMode = TextWrappingModes.NoWrap;
            tradeCooldownText.text = "COOLDOWN  3.0s";

            overlay.SetActive(false);
            tradeCooldownOverlay = overlay;
        }

        private void UpdateTradeCooldownUI()
        {
            if (tradingController == null) return;

            bool hasPosition = tradingController.CurrentPosition != TradingController.PositionType.None;
            bool isManualMode = tradingController.ActiveTradingMode == TradingController.TradingMode.Player_Manual;
            float remaining = tradingController.RemainingPlayerTradeCooldown;
            bool showCooldown = isManualMode && !hasPosition && remaining > 0f;

            if (tradeCooldownOverlay != null)
            {
                tradeCooldownOverlay.SetActive(showCooldown);
                if (showCooldown) tradeCooldownOverlay.transform.SetAsLastSibling();
            }

            if (showCooldown && tradeCooldownText != null)
            {
                tradeCooldownText.text = $"COOLDOWN  {remaining:0.0}s";
            }

            bool canEnter = isManualMode && !hasPosition && !showCooldown;
            if (longButton != null && longButton.interactable != canEnter) longButton.interactable = canEnter;
            if (shortButton != null && shortButton.interactable != canEnter) shortButton.interactable = canEnter;
        }

        private void HandlePositionLiquidated()
        {
            TradingController.PositionType direction = tradingController != null
                ? tradingController.CurrentPosition
                : lastVisualPosition;
            PlayPositionFx(PositionFxKind.Liquidated, direction, 0f, 0);
            RefreshPanelUI();
        }

        private enum PositionFxKind
        {
            Open,
            ProfitClose,
            LossClose,
            Liquidated
        }

        private void HandlePositionOpened(TradingController.PositionType type, float margin, int leverage)
        {
            lastVisualPosition = type;
            FindAnyObjectByType<FXOverdose.AI.AIVisualController>(FindObjectsInactive.Include)?.ShowPositionOpen(type, 1.05f);
            PlayPositionFx(PositionFxKind.Open, type, 0f, leverage);
        }

        private void HandlePositionClosed(float returnedAmount, float pnl)
        {
            PlayPositionFx(pnl >= 0f ? PositionFxKind.ProfitClose : PositionFxKind.LossClose, lastVisualPosition, pnl, 0);
        }

        private void BuildPositionFx()
        {
            if (positionFxCanvas != null) return;
            Canvas hostCanvas = GetComponentInParent<Canvas>();
            Transform parent = hostCanvas != null ? hostCanvas.rootCanvas.transform : transform.root;

            GameObject root = new("PositionDirectionFX", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            root.transform.SetParent(parent, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
            positionFxCanvas = root.GetComponent<Canvas>();
            positionFxCanvas.overrideSorting = true;
            positionFxCanvas.sortingOrder = 145;
            positionFxGroup = root.GetComponent<CanvasGroup>();
            positionFxGroup.blocksRaycasts = false;
            positionFxGroup.interactable = false;

            GameObject dim = new("DirectionFlash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dim.transform.SetParent(root.transform, false);
            RectTransform dimRect = dim.GetComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = dimRect.offsetMax = Vector2.zero;
            positionFxDim = dim.GetComponent<Image>();
            positionFxDim.raycastTarget = false;
            positionFxDim.color = Color.clear;

            GameObject banner = new("PositionBanner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            banner.transform.SetParent(root.transform, false);
            positionBannerRect = banner.GetComponent<RectTransform>();
            positionBannerRect.anchorMin = positionBannerRect.anchorMax = new Vector2(0.5f, 0.5f);
            positionBannerRect.sizeDelta = new Vector2(660f, 104f);
            positionBannerImage = banner.GetComponent<Image>();
            positionBannerImage.color = new Color32(15, 23, 42, 246);
            Outline outline = banner.GetComponent<Outline>();
            outline.effectDistance = UIStrokeStyle.EffectDistance;

            GameObject label = new("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            label.transform.SetParent(banner.transform, false);
            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(18f, 8f);
            labelRect.offsetMax = new Vector2(-18f, -8f);
            positionBannerText = label.GetComponent<TextMeshProUGUI>();
            positionBannerText.font = TMP_Settings.defaultFontAsset;
            positionBannerText.fontSize = 34f;
            positionBannerText.alignment = TextAlignmentOptions.Center;
            positionBannerText.fontStyle = FontStyles.Bold;
            positionBannerText.raycastTarget = false;
            banner.SetActive(false);
        }

        private void PlayPositionFx(PositionFxKind kind, TradingController.PositionType direction, float pnl, int leverage)
        {
            BuildPositionFx();
            if (positionFxCoroutine != null) StopCoroutine(positionFxCoroutine);
            positionFxCoroutine = StartCoroutine(PlayPositionFxRoutine(kind, direction, pnl, leverage));
        }

        private IEnumerator PlayPositionFxRoutine(PositionFxKind kind, TradingController.PositionType direction, float pnl, int leverage)
        {
            bool isLong = direction != TradingController.PositionType.Short;
            Color directionColor = isLong ? bullishColor : bearishColor;
            string arrow = isLong ? "▲" : "▼";
            string directionName = isLong ? "LONG" : "SHORT";

            positionBannerRect.gameObject.SetActive(true);
            positionBannerRect.localScale = new Vector3(0.92f, 0.92f, 1f);
            positionBannerImage.GetComponent<Outline>().effectColor = directionColor;
            positionBannerText.color = kind == PositionFxKind.Liquidated ? Color.white : directionColor;
            positionFxGroup.alpha = 1f;

            if (kind == PositionFxKind.Open)
            {
                positionBannerText.text = $"{arrow}  {directionName} POSITION OPENED  ·  ×{leverage}";
                StartCoroutine(ShakeChart(0.18f, 2f));

                float elapsed = 0f;
                const float duration = 0.52f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    positionBannerRect.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, Mathf.SmoothStep(0f, 1f, t));
                    yield return null;
                }
                yield return new WaitForSecondsRealtime(0.38f);
            }
            else if (kind == PositionFxKind.Liquidated)
            {
                positionBannerText.text = "⚠  LIQUIDATED  ·  POSITION LOST";
                positionBannerImage.GetComponent<Outline>().effectColor = bearishColor;
                for (int i = 0; i < 3; i++)
                {
                    positionFxDim.color = new Color(bearishColor.r, bearishColor.g, bearishColor.b, 0.30f);
                    yield return new WaitForSecondsRealtime(0.11f);
                    positionFxDim.color = Color.clear;
                    yield return new WaitForSecondsRealtime(0.10f);
                }
                SpawnPixelParticles(bearishColor, 0);
                yield return new WaitForSecondsRealtime(0.45f);
            }
            else
            {
                bool profit = kind == PositionFxKind.ProfitClose;
                Color resultColor = profit ? bullishColor : bearishColor;
                positionBannerText.text = profit
                    ? $"{arrow}  {directionName} CLOSED  +${pnl:N0}"
                    : $"{arrow}  {directionName} CLOSED  -${Mathf.Abs(pnl):N0}";
                positionBannerText.color = resultColor;
                positionBannerImage.GetComponent<Outline>().effectColor = resultColor;

                if (profit)
                {
                    SpawnPixelParticles(resultColor, isLong ? 1 : -1);
                }
                else
                {
                    positionFxDim.color = new Color(resultColor.r, resultColor.g, resultColor.b, 0.20f);
                    SpawnPixelParticles(resultColor, 0);
                    yield return new WaitForSecondsRealtime(0.18f);
                    positionFxDim.color = Color.clear;
                }
                yield return new WaitForSecondsRealtime(1.25f);
            }

            float fade = 0f;
            while (fade < 0.28f)
            {
                fade += Time.unscaledDeltaTime;
                positionFxGroup.alpha = 1f - Mathf.Clamp01(fade / 0.28f);
                yield return null;
            }
            positionBannerRect.gameObject.SetActive(false);
            positionFxDim.color = Color.clear;
            positionFxGroup.alpha = 1f;
            positionFxCoroutine = null;
        }

        private void SpawnPixelParticles(Color color, int verticalDirection)
        {
            if (positionFxCanvas == null) return;
            for (int i = 0; i < 18; i++)
            {
                GameObject pixel = new($"DirectionPixel_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                pixel.transform.SetParent(positionFxCanvas.transform, false);
                RectTransform rect = pixel.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = Vector2.one * UnityEngine.Random.Range(6f, 14f);
                rect.anchoredPosition = new Vector2(UnityEngine.Random.Range(-260f, 260f), UnityEngine.Random.Range(-25f, 25f));
                Image image = pixel.GetComponent<Image>();
                image.color = color;
                image.raycastTarget = false;
                StartCoroutine(AnimatePixel(pixel, verticalDirection));
            }
        }

        private static IEnumerator AnimatePixel(GameObject pixel, int verticalDirection)
        {
            RectTransform rect = pixel.GetComponent<RectTransform>();
            Image image = pixel.GetComponent<Image>();
            Vector2 start = rect.anchoredPosition;
            float horizontal = UnityEngine.Random.Range(-150f, 150f);
            float vertical = verticalDirection == 0
                ? UnityEngine.Random.Range(-90f, 90f)
                : verticalDirection * UnityEngine.Random.Range(120f, 260f);
            float elapsed = 0f;
            const float duration = 0.7f;
            while (elapsed < duration && pixel != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                rect.anchoredPosition = start + new Vector2(horizontal, vertical) * t;
                Color c = image.color;
                c.a = 1f - t;
                image.color = c;
                yield return null;
            }
            if (pixel != null) Destroy(pixel);
        }

        private static IEnumerator ShakeChart(float duration, float magnitude)
        {
            RectTransform chart = GameObject.Find("ChartMainPanel")?.GetComponent<RectTransform>();
            if (chart == null) yield break;
            Vector2 origin = chart.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                chart.anchoredPosition = origin + UnityEngine.Random.insideUnitCircle * magnitude;
                yield return null;
            }
            chart.anchoredPosition = origin;
        }
    }
}
