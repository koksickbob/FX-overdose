using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using FXOverdose.Trading;

#pragma warning disable CS0649

namespace FXOverdose.UI.TopBar
{
    public class TopStatusBarUIController : MonoBehaviour
    {
        private const float DayTimeCardWidth = 330f;
        private const int DayTimeHorizontalPadding = 24;
        private const int BarVerticalPadding = 8;
        private const float NormalizedCardHeight = 92f;
        private static readonly Color HudBackground = new Color32(5, 12, 24, 0);
        private static readonly Color CardBackground = new Color32(10, 22, 39, 248);
        private static readonly Color CardBorder = new Color32(31, 61, 86, 255);
        private static readonly Color PrimaryText = new Color32(221, 247, 250, 255);
        private static readonly Color SecondaryText = new Color32(111, 143, 165, 255);
        private static readonly Color CyanAccent = new Color32(6, 182, 212, 255);
        private static readonly Color GoldAccent = new Color32(234, 179, 8, 255);

        [Header("Tutorial Targets")]
        [SerializeField] private RectTransform balanceHighlightTarget;

        public RectTransform TutorialBalanceHighlightTarget => balanceHighlightTarget;

        [Header("시스템 및 렌더러 연결")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private TradingController tradingController;
        [SerializeField] private SparklineRenderer sparklineRenderer;

        [Header("[카드 1] 날짜 및 시간 UI")]
        [SerializeField] private TMP_Text dayLabel;  // "6월 28일"
        [SerializeField] private TMP_Text timeLabel; // "23:47"

        [Header("[카드 2] BALANCE (총 자산/Equity) UI")]
        [SerializeField] private TMP_Text balanceValueLabel; // "$12,458.36"

        [Header("[카드 3] P&L (누적 수익률) UI")]
        [SerializeField] private TMP_Text pnlPercentageLabel; // "+18.47%"
        [SerializeField] private TMP_Text pnlAmountLabel;     // "+$1,458.36" (선택적 부가 정보)

        [Header("테마 색상 (TradingView)")]
        [SerializeField] private Color bullishColor = new Color(0.133f, 0.773f, 0.369f, 1f); // #22C55E
        [SerializeField] private Color bearishColor = new Color(0.937f, 0.267f, 0.267f, 1f); // #EF4444

        // 자산 궤적은 GameManager가 소유합니다(세이브 수집 대상). 이 컨트롤러는 읽어서 그리기만 합니다.
        private Image pnlAccent;
        private float lastLayoutWidth = -1f;
        private readonly Dictionary<Transform, float> baseCardWidths = new();

        private void Start()
        {
            if (gameManager == null) gameManager = GameManager.Instance;
            if (tradingController == null) tradingController = FindAnyObjectByType<TradingController>();
            if (sparklineRenderer == null) sparklineRenderer = GetComponentInChildren<SparklineRenderer>();
            ConfigureDayTimeCardLayout();
            ApplyVisualRedesign();
            baseCardWidths.Clear();
            lastLayoutWidth = -1f;
            RefreshDistributedCardLayout();

            if (gameManager != null)
            {
                gameManager.OnGameMinuteAdvanced += HandleGameMinuteAdvanced;
                gameManager.OnFastForwardEnded += UpdateDayTimeUI;
            }

            // 초기 자산 기록은 하지 않습니다. GameManager가 새 날·새 게임·로드 시점에 궤적을 시딩하므로,
            // 여기서 점을 만들면 복원 전 잔고가 첫 점으로 박혀 그래프가 엉뚱한 높이에서 꺾입니다
            // (스크립트 실행 순서가 지정돼 있지 않아 이 Start가 데이터 복원보다 먼저 돌 수 있습니다).

            UpdateDayTimeUI();
        }

        private void ConfigureDayTimeCardLayout()
        {
            Transform dayTimeCard = dayLabel != null ? dayLabel.transform.parent : transform.Find("DayTimeCard");
            if (dayTimeCard == null) return;

            LayoutElement layout = dayTimeCard.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.minWidth = DayTimeCardWidth;
                layout.preferredWidth = DayTimeCardWidth;
            }

            HorizontalLayoutGroup horizontal = dayTimeCard.GetComponent<HorizontalLayoutGroup>();
            if (horizontal != null)
            {
                RectOffset padding = horizontal.padding;
                padding.left = DayTimeHorizontalPadding;
                padding.right = DayTimeHorizontalPadding;
                horizontal.padding = padding;
            }
        }

        private void OnDestroy()
        {
            if (gameManager != null)
            {
                gameManager.OnGameMinuteAdvanced -= HandleGameMinuteAdvanced;
                gameManager.OnFastForwardEnded -= UpdateDayTimeUI;
            }
        }

        private void Update()
        {
            // 매 프레임 실시간 총 자산(Total Equity) 및 P&L % 연산
            if (gameManager == null) return;

            float currentEquity = CalculateTotalEquity();
            UpdateBalanceUI(currentEquity);
            UpdatePnLUI(currentEquity);

            // 스파크라인 하이브리드 실시간 렌더링 (GameManager가 가진 당일 궤적 + 매 프레임 실시간 끝점)
            if (sparklineRenderer != null)
            {
                sparklineRenderer.RefreshSparkline(gameManager.DailyEquityHistory, currentEquity);
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            if (!isActiveAndEnabled) return;
            RefreshDistributedCardLayout();
        }

        private void RefreshDistributedCardLayout()
        {
            RectTransform rootRect = transform as RectTransform;
            HorizontalLayoutGroup rootLayout = GetComponent<HorizontalLayoutGroup>();
            if (rootRect == null || rootLayout == null) return;

            float availableWidth = rootRect.rect.width;
            if (availableWidth <= 0f || Mathf.Approximately(availableWidth, lastLayoutWidth)) return;
            lastLayoutWidth = availableWidth;

            List<LayoutElement> cardLayouts = new();
            float baseCardsWidth = 0f;
            int activeCardCount = 0;
            for (int i = 0; i < transform.childCount; i++)
            {
                RectTransform child = transform.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf) continue;

                LayoutElement layout = child.GetComponent<LayoutElement>();
                if (layout != null && layout.ignoreLayout) continue;

                float currentWidth = layout != null && layout.preferredWidth > 0f
                    ? layout.preferredWidth
                    : child.rect.width;
                if (!baseCardWidths.TryGetValue(child, out float baseWidth))
                {
                    baseWidth = Mathf.Max(0f, currentWidth);
                    baseCardWidths[child] = baseWidth;
                }

                baseCardsWidth += baseWidth;
                if (layout != null) cardLayouts.Add(layout);
                activeCardCount++;
            }

            int gaps = Mathf.Max(0, activeCardCount - 1);
            float contentWidth = availableWidth - rootLayout.padding.horizontal;
            float targetCardsWidth = Mathf.Max(baseCardsWidth, contentWidth - gaps * 7f);
            float widthScale = baseCardsWidth > 0f ? targetCardsWidth / baseCardsWidth : 1f;

            foreach (LayoutElement layout in cardLayouts)
            {
                if (!baseCardWidths.TryGetValue(layout.transform, out float baseWidth)) continue;
                layout.preferredWidth = baseWidth * widthScale;
            }

            float cardsWidth = baseCardsWidth * widthScale;
            rootLayout.spacing = gaps > 0
                ? Mathf.Max(7f, (contentWidth - cardsWidth) / gaps)
                : 0f;
            rootLayout.childAlignment = TextAnchor.MiddleCenter;
            LayoutRebuilder.MarkLayoutForRebuild(rootRect);
        }

        private void HandleGameMinuteAdvanced()
        {
            // 궤적 고정점 기록은 GameManager.AdvanceOneMinute이 담당합니다. 여기서는 시계 표시만 갱신합니다.
            if (gameManager != null && gameManager.IsFastForwardingTime) return;
            UpdateDayTimeUI();
        }

        // 1. 날짜 및 시간 카드 업데이트
        private void UpdateDayTimeUI()
        {
            if (gameManager == null) return;

            if (dayLabel != null)
            {
                dayLabel.text = FXOverdose.Core.GameCalendar.ToKoreanShort(gameManager.CurrentDate);
            }

            if (timeLabel != null)
            {
                timeLabel.text = $"{gameManager.CurrentHour:00}:{gameManager.CurrentMinute:00}";
            }
        }

        // 2. 총 자산 (BALANCE) 카드 업데이트
        private void UpdateBalanceUI(float currentEquity)
        {
            if (balanceValueLabel != null)
            {
                balanceValueLabel.text = $"${currentEquity:N2}";
            }
        }

        // 3. 누적 수익률 (P&L) 카드 업데이트
        private void UpdatePnLUI(float currentEquity)
        {
            float startingBalance = gameManager != null && gameManager.StartingBalance > 0 
                ? gameManager.StartingBalance : 7000f;

            float pnlDiff = currentEquity - startingBalance;
            float pnlPercentage = (pnlDiff / startingBalance) * 100f;

            Color targetColor = pnlDiff >= 0 ? bullishColor : bearishColor;
            string sign = pnlDiff >= 0 ? "+" : "";

            if (pnlPercentageLabel != null)
            {
                pnlPercentageLabel.text = $"{sign}{pnlPercentage:F2}%";
                pnlPercentageLabel.color = targetColor;
            }

            // SeparatePnLCard가 이 라벨을 숨긴 뒤 다시 켜는 곳이 없습니다. 숨겨진 동안은 갱신도 하지 않습니다.
            // (다시 노출할 계획이면 SeparatePnLCard의 SetActive(false)부터 걷어내십시오.)
            if (pnlAmountLabel != null && pnlAmountLabel.gameObject.activeSelf)
            {
                pnlAmountLabel.text = $"{sign}${pnlDiff:N2}";
                pnlAmountLabel.color = targetColor;
            }

            if (pnlAccent != null)
            {
                pnlAccent.color = targetColor;
            }
        }

        /// <summary>
        /// 기존 데이터 바인딩과 튜토리얼 하이라이트 대상을 유지한 채 상단 HUD 외형만 재구성합니다.
        /// </summary>
        private void ApplyVisualRedesign()
        {
            Image rootBackground = GetComponent<Image>();
            if (rootBackground != null)
            {
                rootBackground.sprite = null;
                rootBackground.color = HudBackground;
                rootBackground.raycastTarget = false;
            }

            HorizontalLayoutGroup rootLayout = GetComponent<HorizontalLayoutGroup>();
            if (rootLayout != null)
            {
                int horizontalMargin = Mathf.RoundToInt(UIStrokeStyle.ScreenEdgeMargin);
                rootLayout.padding = new RectOffset(
                    horizontalMargin,
                    horizontalMargin,
                    BarVerticalPadding,
                    BarVerticalPadding);
                rootLayout.spacing = 7f;
                rootLayout.childAlignment = TextAnchor.MiddleCenter;
            }

            RemoveRule(transform, "TopHudUpperRule");
            RemoveRule(transform, "TopHudLowerRule");

            Transform dayCard = FindDescendant(transform, "DayTimeCard");
            Transform balanceCard = FindDescendant(transform, "BalanceCard");
            Transform pnlCard = FindDescendant(transform, "PnLCard");
            Transform pnlGraphCard = SeparatePnLCard(pnlCard);
            Transform vitalsPanel = FindDescendant(transform, "VitalsPanel");

            NormalizeCardHeight(dayCard);
            NormalizeCardHeight(balanceCard);
            NormalizeCardHeight(pnlCard);
            NormalizeCardHeight(pnlGraphCard);
            NormalizeCardHeight(vitalsPanel);

            StyleCard(dayCard, CyanAccent);
            StyleCard(balanceCard, GoldAccent);
            StyleCard(pnlCard, bearishColor);
            StyleCard(pnlGraphCard, bearishColor);
            StyleCard(vitalsPanel, new Color32(168, 85, 247, 255));

            // TMP 텍스트 스타일 처리와 무관하게 손익 그래프는 먼저 가시성을 확보합니다.
            if (sparklineRenderer != null)
            {
                sparklineRenderer.SetVisualWeight(3.5f);
                LayoutElement sparklineLayout = sparklineRenderer.GetComponent<LayoutElement>();
                if (sparklineLayout != null) sparklineLayout.ignoreLayout = true;

                RectTransform sparklineRect = sparklineRenderer.GetComponent<RectTransform>();
                if (sparklineRect != null)
                {
                    sparklineRect.anchorMin = Vector2.zero;
                    sparklineRect.anchorMax = Vector2.one;
                    sparklineRect.pivot = new Vector2(0.5f, 0.5f);
                    sparklineRect.offsetMin = new Vector2(14f, 14f);
                    sparklineRect.offsetMax = new Vector2(-14f, -14f);
                }

                sparklineRenderer.transform.SetAsLastSibling();
            }

            StyleValue(dayLabel, 23f, PrimaryText);
            if (dayLabel != null)
            {
                dayLabel.characterSpacing = 1.5f;
                dayLabel.fontStyle = FontStyles.Bold;
            }
            StyleValue(timeLabel, 25f, CyanAccent);
            if (timeLabel != null)
            {
                timeLabel.characterSpacing = 2f;
                timeLabel.fontStyle = FontStyles.Bold;
            }

            StyleNamedLabel("BalanceTitle", "BALANCE");
            StyleNamedLabel("PnLTitle", "P&L");
            StyleValue(balanceValueLabel, 26f, PrimaryText);
            StyleValue(pnlPercentageLabel, 25f, bearishColor);
            StyleValue(pnlAmountLabel, 13f, SecondaryText);

            if (balanceCard != null) AddCornerTag(balanceCard, "AVAILABLE EQUITY", GoldAccent);
            if (pnlCard != null)
            {
                pnlAccent = FindDescendant(pnlCard, "CardAccent")?.GetComponent<Image>();
                if (pnlAccent != null)
                {
                    RectTransform accentRect = pnlAccent.rectTransform;
                    accentRect.anchoredPosition = new Vector2(3f, 0f);
                    accentRect.sizeDelta = new Vector2(6f, 0f);
                    pnlAccent.transform.SetAsLastSibling();
                }
            }
            if (dayCard != null) AddMarketStatus(dayCard);

            StyleVitals(vitalsPanel);
            StyleSettingsButton(vitalsPanel);
        }

        private Transform SeparatePnLCard(Transform pnlCard)
        {
            if (pnlCard == null || pnlCard.parent == null) return null;
            Transform graphCard = pnlCard.parent.Find("PnLGraphCard");
            if (graphCard == null)
            {
                GameObject graphObject = new("PnLGraphCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
                graphObject.transform.SetParent(pnlCard.parent, false);
                graphCard = graphObject.transform;
            }
            graphCard.SetSiblingIndex(pnlCard.GetSiblingIndex() + 1);

            LayoutElement numberSize = pnlCard.GetComponent<LayoutElement>();
            if (numberSize == null) numberSize = pnlCard.gameObject.AddComponent<LayoutElement>();
            numberSize.minWidth = 208f;
            numberSize.preferredWidth = 208f;
            LayoutElement graphSize = graphCard.GetComponent<LayoutElement>();
            if (graphSize == null) graphSize = graphCard.gameObject.AddComponent<LayoutElement>();
            graphSize.minWidth = 176f;
            graphSize.preferredWidth = 176f;

            Transform sparkline = FindDescendant(pnlCard, "SparklineContainer");
            if (sparkline != null) sparkline.SetParent(graphCard, false);

            HorizontalLayoutGroup pnlLayout = pnlCard.GetComponent<HorizontalLayoutGroup>();
            if (pnlLayout != null)
            {
                pnlLayout.padding = new RectOffset(16, 16, 12, 12);
                pnlLayout.childAlignment = TextAnchor.MiddleCenter;
                pnlLayout.childControlWidth = true;
                pnlLayout.childControlHeight = true;
                pnlLayout.childForceExpandWidth = true;
                pnlLayout.childForceExpandHeight = true;
            }

            Transform textGroup = FindDescendant(pnlCard, "PnLTextGroup");
            if (textGroup != null)
            {
                VerticalLayoutGroup textLayout = textGroup.GetComponent<VerticalLayoutGroup>();
                if (textLayout != null)
                {
                    textLayout.childAlignment = TextAnchor.MiddleCenter;
                    textLayout.childControlWidth = true;
                    textLayout.childForceExpandWidth = true;
                }
                LayoutElement textSize = textGroup.GetComponent<LayoutElement>();
                if (textSize == null) textSize = textGroup.gameObject.AddComponent<LayoutElement>();
                textSize.flexibleWidth = 1f;
            }

            Transform title = FindDescendant(pnlCard, "PnLTitle");
            if (title != null)
            {
                title.gameObject.SetActive(true);
                TMP_Text titleText = title.GetComponent<TMP_Text>();
                if (titleText != null)
                {
                    titleText.text = "P&L";
                    titleText.alignment = TextAlignmentOptions.Left;
                }
            }
            if (pnlAmountLabel != null) pnlAmountLabel.gameObject.SetActive(false);
            Transform tag = pnlCard.Find("TelemetryTag");
            if (tag != null) tag.gameObject.SetActive(false);
            if (pnlPercentageLabel != null)
            {
                pnlPercentageLabel.alignment = TextAlignmentOptions.Center;
                pnlPercentageLabel.fontSize = 30f;
                pnlPercentageLabel.enableAutoSizing = true;
                pnlPercentageLabel.fontSizeMin = 13f;
                pnlPercentageLabel.fontSizeMax = 30f;
                pnlPercentageLabel.textWrappingMode = TextWrappingModes.NoWrap;
                pnlPercentageLabel.overflowMode = TextOverflowModes.Ellipsis;
                pnlPercentageLabel.margin = new Vector4(4f, 0f, 4f, 0f);
            }
            if (pnlCard.GetComponent<RectMask2D>() == null) pnlCard.gameObject.AddComponent<RectMask2D>();
            return graphCard;
        }

        private void StyleNamedLabel(string objectName, string value)
        {
            TMP_Text label = FindDescendant(transform, objectName)?.GetComponent<TMP_Text>();
            if (label == null) return;
            label.text = value;
            label.color = SecondaryText;
            label.fontSize = 12f;
            label.enableAutoSizing = false;
            label.fontStyle = FontStyles.Normal;
            label.characterSpacing = 2.5f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
        }

        private static void StyleValue(TMP_Text text, float size, Color color)
        {
            if (text == null) return;
            text.color = color;
            text.fontSize = size;
            text.fontSizeMin = Mathf.Min(12f, size);
            text.fontSizeMax = size;
            text.fontStyle = FontStyles.Bold;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }

        private static void StyleCard(Transform card, Color accentColor)
        {
            if (card == null) return;
            Image background = card.GetComponent<Image>();
            if (background != null)
            {
                background.sprite = null;
                background.color = CardBackground;
                background.raycastTarget = false;
            }

            Outline outline = card.GetComponent<Outline>();
            if (outline == null) outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = CardBorder;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = true;

            Shadow shadow = null;
            foreach (Shadow candidate in card.GetComponents<Shadow>())
            {
                // Outline도 Shadow를 상속하므로 정확히 기본 Shadow 컴포넌트만 찾습니다.
                if (candidate.GetType() == typeof(Shadow))
                {
                    shadow = candidate;
                    break;
                }
            }
            if (shadow == null) shadow = card.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color32(0, 0, 0, 145);
            shadow.effectDistance = new Vector2(3f, -3f);
            shadow.useGraphicAlpha = true;

            GameObject accent = GetOrCreateUi(card, "CardAccent");
            RectTransform accentRect = accent.GetComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0f, 0.16f);
            accentRect.anchorMax = new Vector2(0f, 0.84f);
            accentRect.pivot = new Vector2(0f, 0.5f);
            accentRect.anchoredPosition = new Vector2(0f, 0f);
            accentRect.sizeDelta = new Vector2(4f, 0f);
            accent.GetComponent<Image>().color = accentColor;
            IgnoreLayout(accent);

            GameObject topGlow = GetOrCreateUi(card, "CardTopGlow");
            RectTransform glowRect = topGlow.GetComponent<RectTransform>();
            glowRect.anchorMin = new Vector2(0f, 1f);
            glowRect.anchorMax = new Vector2(1f, 1f);
            glowRect.pivot = new Vector2(0.5f, 1f);
            glowRect.anchoredPosition = Vector2.zero;
            glowRect.sizeDelta = new Vector2(0f, 1f);
            Color glowColor = accentColor;
            glowColor.a = 0.35f;
            topGlow.GetComponent<Image>().color = glowColor;
            IgnoreLayout(topGlow);
        }

        private static void NormalizeCardHeight(Transform card)
        {
            if (card == null) return;
            LayoutElement layout = card.GetComponent<LayoutElement>();
            if (layout == null) layout = card.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = NormalizedCardHeight;
            layout.preferredHeight = NormalizedCardHeight;
            layout.flexibleHeight = 0f;
        }

        private static void StyleVitals(Transform vitalsPanel)
        {
            if (vitalsPanel == null) return;

            TMP_Text hpTitle = FindDescendant(vitalsPanel, "HPTitle")?.GetComponent<TMP_Text>();
            TMP_Text mentalTitle = FindDescendant(vitalsPanel, "MentalTitle")?.GetComponent<TMP_Text>();
            StyleVitalTitle(hpTitle, new Color32(255, 82, 125, 255));
            StyleVitalTitle(mentalTitle, new Color32(183, 112, 255, 255));

            TMP_Text hpValue = FindDescendant(vitalsPanel, "HPValue")?.GetComponent<TMP_Text>();
            TMP_Text mentalValue = FindDescendant(vitalsPanel, "MentalValue")?.GetComponent<TMP_Text>();
            StyleVitalValue(hpValue);
            StyleVitalValue(mentalValue);

            foreach (Slider slider in vitalsPanel.GetComponentsInChildren<Slider>(true))
            {
                bool mental = slider.name.IndexOf("mental", StringComparison.OrdinalIgnoreCase) >= 0;
                Image fill = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
                if (fill != null)
                {
                    fill.sprite = null;
                    fill.color = mental
                        ? new Color32(168, 85, 247, 255)
                        : new Color32(244, 63, 94, 255);
                }
            }

            StyleBarBackground(vitalsPanel, "HPBarBackground");
            StyleBarBackground(vitalsPanel, "MentalBarBackground");
        }

        private static void StyleVitalTitle(TMP_Text text, Color color)
        {
            if (text == null) return;
            text.color = color;
            text.fontSize = 14f;
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 1.5f;
        }

        private static void StyleVitalValue(TMP_Text text)
        {
            if (text == null) return;
            text.color = PrimaryText;
            text.fontSize = 12f;
            text.fontStyle = FontStyles.Normal;
        }

        private static void StyleBarBackground(Transform root, string objectName)
        {
            Image background = FindDescendant(root, objectName)?.GetComponent<Image>();
            if (background == null) return;
            background.sprite = null;
            background.color = new Color32(2, 8, 18, 245);
            Outline outline = background.GetComponent<Outline>();
            if (outline == null) outline = background.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(35, 55, 74, 255);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        private static void StyleSettingsButton(Transform root)
        {
            Transform button = FindDescendant(root, "SettingsButton");
            if (button == null) button = FindDescendant(root, "SettingsButtonVisual")?.parent;
            if (button == null) return;

            Image background = button.GetComponent<Image>();
            if (background != null)
            {
                background.sprite = null;
                background.color = new Color32(12, 27, 47, 255);
            }

            Outline outline = button.GetComponent<Outline>();
            if (outline == null) outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = CyanAccent;
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            Button unityButton = button.GetComponent<Button>();
            if (unityButton != null && background != null)
            {
                unityButton.targetGraphic = background;
                ColorBlock colors = unityButton.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color32(176, 244, 255, 255);
                colors.pressedColor = new Color32(94, 182, 200, 255);
                unityButton.colors = colors;
            }
        }

        private static void AddCornerTag(Transform card, string value, Color color)
        {
            Transform existing = card.Find("TelemetryTag");
            TMP_Text tag;
            if (existing != null)
            {
                tag = existing.GetComponent<TMP_Text>();
            }
            else
            {
                GameObject go = new("TelemetryTag", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
                go.transform.SetParent(card, false);
                go.GetComponent<LayoutElement>().ignoreLayout = true;
                tag = go.GetComponent<TMP_Text>();
            }

            RectTransform rect = tag.rectTransform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-12f, -8f);
            rect.sizeDelta = new Vector2(145f, 18f);
            tag.text = value;
            tag.alignment = TextAlignmentOptions.TopRight;
            tag.font = TMP_Settings.defaultFontAsset;
            tag.fontSize = 8.5f;
            tag.characterSpacing = 1.4f;
            tag.color = new Color(color.r, color.g, color.b, 0.62f);
            tag.raycastTarget = false;
            tag.textWrappingMode = TextWrappingModes.NoWrap;
        }

        private static void AddMarketStatus(Transform dayCard)
        {
            GameObject status = GetOrCreateUi(dayCard, "MarketLiveDot");
            RectTransform rect = status.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(13f, 9f);
            rect.sizeDelta = new Vector2(5f, 5f);
            status.GetComponent<Image>().color = new Color32(34, 197, 94, 255);
            IgnoreLayout(status);

            Transform existing = dayCard.Find("MarketLiveLabel");
            TMP_Text label;
            if (existing != null) label = existing.GetComponent<TMP_Text>();
            else
            {
                GameObject go = new("MarketLiveLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
                go.transform.SetParent(dayCard, false);
                go.GetComponent<LayoutElement>().ignoreLayout = true;
                label = go.GetComponent<TMP_Text>();
            }
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0f, 0f);
            labelRect.pivot = new Vector2(0f, 0f);
            labelRect.anchoredPosition = new Vector2(23f, 5f);
            labelRect.sizeDelta = new Vector2(90f, 14f);
            label.text = "MARKET LIVE";
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = 8.5f;
            label.characterSpacing = 1.3f;
            label.color = new Color32(85, 191, 132, 230);
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
        }

        private static void RemoveRule(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                existing.gameObject.SetActive(false);
            }
        }

        private static GameObject GetOrCreateUi(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.gameObject;

            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return go;
        }

        private static void IgnoreLayout(GameObject go)
        {
            LayoutElement layout = go.GetComponent<LayoutElement>();
            if (layout == null) layout = go.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null) return null;
            if (root.name == objectName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDescendant(root.GetChild(i), objectName);
                if (found != null) return found;
            }
            return null;
        }

        // 실시간 총 자산(Total Equity) 공식: 보유 현금 + 투입 증거금 + 실시간 미실현 손익
        public float CalculateTotalEquity()
        {
            if (gameManager == null) return 0f;

            float equity = gameManager.CurrentBalance;

            if (tradingController != null && tradingController.CurrentPosition != TradingController.PositionType.None)
            {
                equity += tradingController.MarginAmount + tradingController.CalculateUnrealizedPnL();
            }

            return equity;
        }
    }
}
