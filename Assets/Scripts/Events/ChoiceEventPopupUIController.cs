using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FXOverdose.Events
{
    /// <summary>
    /// 돌발 선택 이벤트를 FX WIRE 인터넷 기사 형태로 표시합니다.
    /// 선택 효과와 아이템 검증은 ChoiceEventController가 담당하고,
    /// 이 컴포넌트는 화면 구성과 사용자 입력만 처리합니다.
    /// </summary>
    public class ChoiceEventPopupUIController : MonoBehaviour
    {
        private const int EventPopupSortingOrder = 150;
        private const int OptionCount = 3;
        private const float ModalWidth = 1500f;
        private const float ModalHeight = 920f;

        private static readonly Color DeepBackground = new Color32(11, 15, 25, 255);   // #0B0F19
        private static readonly Color PanelBackground = new Color32(15, 23, 42, 255);  // #0F172A
        private static readonly Color HeaderBackground = new Color32(20, 29, 51, 255); // #141D33
        private static readonly Color BorderColor = new Color32(59, 75, 102, 255);      // #3B4B66
        private static readonly Color Cyan = new Color32(6, 182, 212, 255);             // #06B6D4
        private static readonly Color BodyText = new Color32(203, 213, 225, 255);       // #CBD5E1
        private static readonly Color MutedText = new Color32(102, 117, 143, 255);      // #66758F
        private static readonly Color AiText = new Color32(207, 250, 254, 255);         // #CFFAFE
        private static readonly Color SafeGreen = new Color32(34, 197, 94, 255);        // #22C55E
        private static readonly Color RiskRed = new Color32(239, 68, 68, 255);          // #EF4444
        private static readonly Color SpecialGold = new Color32(234, 179, 8, 255);      // #EAB308
        private static readonly Color ErrorRed = new Color32(255, 77, 77, 255);         // #FF4D4D

        [Header("UI 연결 슬롯 (미연결 시 런타임 자동 생성)")]
        [SerializeField] private GameObject popupPanel;
        [SerializeField] private TMP_Text scenarioTitleText;
        [SerializeField] private TMP_Text scenarioDescText;
        [SerializeField] private TMP_Text aiMonologueText;
        [SerializeField] private Button[] optionButtons = new Button[OptionCount];
        [SerializeField] private TMP_Text[] optionTexts = new TMP_Text[OptionCount];
        [SerializeField] private TMP_Text toastText;

        private readonly Color disabledAccent = new Color32(86, 98, 119, 255);
        private Action<int> currentCallback;
        private ChoiceEventSO currentEvent;
        private ScrollRect scrollRect;
        private RectTransform articleContentRect;
        private RectTransform aiQuoteCardRect;
        private LayoutElement aiQuoteLayout;
        private TMP_Text browserAddressText;
        private TMP_Text breakingMetaText;
        private TMP_Text articleMetaText;
        private GameObject toastContainer;
        private Image[] optionBackgrounds = new Image[OptionCount];
        private Image[] optionAccentBars = new Image[OptionCount];
        private Outline[] optionOutlines = new Outline[OptionCount];
        private Coroutine breakingNewsCoroutine;
        private GameObject breakingNewsOverlay;

        private void Awake()
        {
            EnsureUIBuilt();
            EnsureOverlayPriority();

            if (popupPanel != null)
            {
                popupPanel.SetActive(false);
            }
        }

        public void Show(ChoiceEventSO eventData, Action<int> onOptionSelected)
        {
            if (eventData == null)
            {
                return;
            }

            EnsureUIBuilt();
            EnsureOverlayPriority();
            EnsureRuntimeArrays();

            if (popupPanel == null)
            {
                Debug.LogWarning("[ChoiceEventUI] Canvas가 준비되지 않아 돌발 이벤트 UI를 표시하지 못했습니다.");
                return;
            }

            currentEvent = eventData;
            currentCallback = onOptionSelected;

            string eventId = string.IsNullOrWhiteSpace(eventData.EventID) ? "market-alert" : eventData.EventID.Trim();
            string category = GetEventCategory(eventData.TriggerCondition);
            string title = string.IsNullOrWhiteSpace(eventData.ScenarioTitle) ? "긴급 시장 속보" : eventData.ScenarioTitle;

            if (scenarioTitleText != null)
            {
                scenarioTitleText.text = title;
            }

            if (scenarioDescText != null)
            {
                scenarioDescText.text = string.IsNullOrWhiteSpace(eventData.ScenarioDescription)
                    ? "현재 시장 상황을 분석하고 대응 방안을 선택해 주세요."
                    : eventData.ScenarioDescription;
            }

            if (aiMonologueText != null)
            {
                string monologue = string.IsNullOrWhiteSpace(eventData.AIMonologue)
                    ? "시장 데이터가 불안정해요. 대응 방향을 정해 주세요."
                    : eventData.AIMonologue;
                aiMonologueText.text =
                    $"<color=#06B6D4><b>YOMI // AI MARKET ANALYST</b></color>\n" +
                    $"<color=#CFFAFE>“{monologue}”</color>";
            }

            GameManager gameManager = FindAnyObjectByType<GameManager>();
            int day = gameManager != null ? gameManager.CurrentDay : 1;
            int hour = gameManager != null ? gameManager.CurrentHour : 0;
            int minute = gameManager != null ? gameManager.CurrentMinute : 0;

            if (browserAddressText != null)
            {
                browserAddressText.text = $"https://fxwire.local/live/{ToUrlSlug(eventId)}";
            }

            if (breakingMetaText != null)
            {
                breakingMetaText.text = $"LIVE UPDATE  /  {category}  /  DAY {day:00}  {hour:00}:{minute:00}";
            }

            if (articleMetaText != null)
            {
                articleMetaText.text = $"BREAKING  /  {category}  /  {eventId.ToUpperInvariant()}";
            }

            HideToast();

            TraderStatus traderStatus = TraderStatus.CanonicalInstance;
            ChoiceOptionData[] options = eventData.Options ?? Array.Empty<ChoiceOptionData>();

            for (int i = 0; i < OptionCount; i++)
            {
                int optionIndex = i;
                bool hasOption = i < options.Length && options[i] != null;
                ChoiceOptionData option = hasOption ? options[i] : null;

                if (optionButtons[i] != null)
                {
                    optionButtons[i].onClick.RemoveAllListeners();
                    optionButtons[i].onClick.AddListener(() => OnOptionButtonClicked(optionIndex));
                    optionButtons[i].gameObject.SetActive(hasOption);
                }

                if (!hasOption)
                {
                    continue;
                }

                bool isAvailable = IsOptionAvailable(option, traderStatus);
                if (optionButtons[i] != null)
                {
                    optionButtons[i].interactable = isAvailable;
                }
                ApplyOptionVisual(i, option.OptionType, isAvailable);

                if (optionTexts[i] != null)
                {
                    Color accent = GetOptionAccent(option.OptionType, i);
                    string accentHex = ColorUtility.ToHtmlStringRGB(accent);
                    string letter = ((char)('A' + i)).ToString();
                    string requirement = string.Empty;

                    if (option.OptionType == ChoiceOptionType.SpecialItem && !string.IsNullOrWhiteSpace(option.RequiredItemId))
                    {
                        requirement =
                            $"\n<size=68%><color=#EAB308>REQUIRED: {option.RequiredItemId.ToUpperInvariant()} ×{option.RequiredItemCount}</color></size>";
                    }

                    string unavailable = isAvailable
                        ? string.Empty
                        : "\n<size=68%><color=#FF4D4D><b>REQUIRED ITEM MISSING</b></color></size>";

                    optionTexts[i].text =
                        $"<size=76%><color=#{accentHex}><b>{letter} / {GetOptionLabel(option.OptionType)}</b></color></size>\n" +
                        $"<size=100%><b>{option.OptionTitle}</b></size>\n" +
                        $"<size=68%><color=#CBD5E1>{option.Description}</color></size>" +
                        requirement + unavailable;
                }
            }

            if (popupPanel == null)
            {
                return;
            }

            popupPanel.SetActive(false);
            if (breakingNewsCoroutine != null) StopCoroutine(breakingNewsCoroutine);
            breakingNewsCoroutine = StartCoroutine(ShowBreakingNewsThenArticle(category));
        }

        public void Hide()
        {
            CancelInvoke(nameof(HideToast));
            if (breakingNewsCoroutine != null)
            {
                StopCoroutine(breakingNewsCoroutine);
                breakingNewsCoroutine = null;
            }
            if (breakingNewsOverlay != null)
            {
                Destroy(breakingNewsOverlay);
                breakingNewsOverlay = null;
            }
            currentEvent = null;
            currentCallback = null;

            if (popupPanel != null)
            {
                popupPanel.SetActive(false);
            }
        }

        private IEnumerator ShowBreakingNewsThenArticle(string category)
        {
            Transform overlayParent = popupPanel != null ? popupPanel.transform.parent : transform;
            breakingNewsOverlay = CreatePanel(overlayParent, "BreakingNewsArrivalOverlay", new Color32(3, 8, 20, 242), true);
            Stretch(breakingNewsOverlay.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

            Canvas overlayCanvas = breakingNewsOverlay.AddComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = EventPopupSortingOrder + 1;
            breakingNewsOverlay.AddComponent<GraphicRaycaster>();
            CanvasGroup group = breakingNewsOverlay.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            GameObject alertLine = CreatePanel(breakingNewsOverlay.transform, "AlertLine", RiskRed, false);
            RectTransform lineRect = alertLine.GetComponent<RectTransform>();
            Fixed(lineRect, new Vector2(0.5f, 0.5f), new Vector2(760f, 5f), new Vector2(0f, 82f));

            TMP_Text headline = CreateText(breakingNewsOverlay.transform, "BreakingHeadline", 68f, AiText, TextAlignmentOptions.Center);
            Fixed(headline.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1100f, 120f), new Vector2(0f, 12f));
            headline.text = "<color=#EF4444>BREAKING</color> NEWS!";
            headline.fontStyle = FontStyles.Bold;

            TMP_Text arrival = CreateText(breakingNewsOverlay.transform, "EventArrival", 29f, Cyan, TextAlignmentOptions.Center);
            Fixed(arrival.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(900f, 70f), new Vector2(0f, -70f));
            arrival.text = $"돌발 이벤트 등장  ·  {category}";
            arrival.fontStyle = FontStyles.Bold;

            TMP_Text prompt = CreateText(breakingNewsOverlay.transform, "PreparePrompt", 17f, MutedText, TextAlignmentOptions.Center);
            Fixed(prompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(800f, 50f), new Vector2(0f, -122f));
            prompt.text = "새로운 시장 속보를 확인하세요";

            yield return FadeCanvasGroup(group, 0f, 1f, 0.22f);

            float pulseElapsed = 0f;
            const float holdDuration = 1.05f;
            while (pulseElapsed < holdDuration)
            {
                pulseElapsed += Time.unscaledDeltaTime;
                float pulse = 1f + Mathf.Sin(pulseElapsed * 9f) * 0.025f;
                headline.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
                yield return null;
            }

            yield return FadeCanvasGroup(group, 1f, 0f, 0.28f);
            Destroy(breakingNewsOverlay);
            breakingNewsOverlay = null;
            breakingNewsCoroutine = null;

            if (popupPanel == null || currentEvent == null) yield break;

            popupPanel.SetActive(true);
            popupPanel.transform.SetAsLastSibling();
            EnsureOverlayPriority();
            Canvas.ForceUpdateCanvases();
            RefreshArticleLayout();

            if (scrollRect != null)
            {
                scrollRect.StopMovement();
                scrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private static IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            group.alpha = to;
        }

        public void ShowToastWarning(string message)
        {
            Debug.LogWarning($"[ChoiceEventUI] {message}");

            if (toastText == null)
            {
                return;
            }

            toastText.text = message;
            if (toastContainer != null)
            {
                toastContainer.SetActive(true);
            }
            else
            {
                toastText.gameObject.SetActive(true);
            }

            CancelInvoke(nameof(HideToast));
            Invoke(nameof(HideToast), 2.5f);
        }

        private void HideToast()
        {
            if (toastContainer != null)
            {
                toastContainer.SetActive(false);
            }
            else if (toastText != null)
            {
                toastText.gameObject.SetActive(false);
            }
        }

        private void OnOptionButtonClicked(int index)
        {
            if (currentEvent == null)
            {
                return;
            }

            currentCallback?.Invoke(index);
        }

        private void EnsureUIBuilt()
        {
            EnsureRuntimeArrays();

            if (popupPanel != null)
            {
                Transform marker = popupPanel.transform.Find("ModalBox/InternetNewsLayout");
                if (marker != null && RebindGeneratedUI(marker))
                {
                    return;
                }

                popupPanel.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(popupPanel);
                }
                else
                {
                    DestroyImmediate(popupPanel);
                }

                popupPanel = null;
                ResetRuntimeReferences();
            }

            Canvas canvas = FindTargetCanvas();
            if (canvas == null)
            {
                Debug.LogWarning("[ChoiceEventUI] 돌발 이벤트 UI를 배치할 Canvas를 찾지 못했습니다.");
                return;
            }

            GameObject panelGo = CreatePanel(canvas.transform, "ChoiceEventPopupPanel", new Color(0f, 0f, 0f, 0.78f), true);
            RectTransform rootRect = panelGo.GetComponent<RectTransform>();
            Stretch(rootRect, Vector2.zero, Vector2.zero);
            popupPanel = panelGo;

            GameObject modalGo = CreatePanel(panelGo.transform, "ModalBox", PanelBackground, false);
            RectTransform modalRect = modalGo.GetComponent<RectTransform>();
            Fixed(modalRect, new Vector2(0.5f, 0.5f), new Vector2(ModalWidth, ModalHeight), Vector2.zero);

            Shadow shadow = modalGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.78f);
            shadow.effectDistance = new Vector2(12f, -12f);
            shadow.useGraphicAlpha = true;
            AddOutline(modalGo, BorderColor);

            GameObject layoutGo = new GameObject("InternetNewsLayout", typeof(RectTransform));
            layoutGo.transform.SetParent(modalGo.transform, false);
            RectTransform layoutRect = layoutGo.GetComponent<RectTransform>();
            Stretch(layoutRect, Vector2.zero, Vector2.zero);

            CreateTopAccent(layoutGo.transform);
            CreateBrowserChrome(layoutGo.transform);
            CreateSiteHeader(layoutGo.transform);
            CreateBreakingTicker(layoutGo.transform);
            CreateArticleColumn(layoutGo.transform);
            CreateResponseSidebar(layoutGo.transform);

            EnsureOverlayPriority();
        }

        private void CreateTopAccent(Transform parent)
        {
            GameObject accent = CreatePanel(parent, "TopAccent", Cyan, false);
            SetTopRect(accent.GetComponent<RectTransform>(), 0f, 0f, 0f, 4f);
        }

        private void CreateBrowserChrome(Transform parent)
        {
            GameObject browser = CreatePanel(parent, "BrowserChrome", HeaderBackground, false);
            RectTransform browserRect = browser.GetComponent<RectTransform>();
            SetTopRect(browserRect, 0f, 0f, 4f, 44f);

            CreatePixelIndicator(browser.transform, "ClosePixel", new Color32(239, 68, 68, 255), 24f);
            CreatePixelIndicator(browser.transform, "MinimizePixel", new Color32(234, 179, 8, 255), 48f);
            CreatePixelIndicator(browser.transform, "OnlinePixel", new Color32(34, 197, 94, 255), 72f);

            GameObject addressBar = CreatePanel(browser.transform, "AddressBar", DeepBackground, false);
            RectTransform addressRect = addressBar.GetComponent<RectTransform>();
            Stretch(addressRect, new Vector2(102f, 7f), new Vector2(-18f, -7f));
            AddOutline(addressBar, new Color32(44, 59, 82, 255), new Vector2(2f, -2f));

            browserAddressText = CreateText(addressBar.transform, "AddressText", 16f, MutedText, TextAlignmentOptions.MidlineLeft);
            Stretch(browserAddressText.rectTransform, new Vector2(14f, 0f), new Vector2(-12f, 0f));
            browserAddressText.text = "https://fxwire.local/live/market-alert";
        }

        private void CreateSiteHeader(Transform parent)
        {
            GameObject header = CreatePanel(parent, "SiteHeader", DeepBackground, false);
            RectTransform headerRect = header.GetComponent<RectTransform>();
            SetTopRect(headerRect, 0f, 0f, 48f, 70f);

            TMP_Text logo = CreateText(header.transform, "Logo", 34f, Color.white, TextAlignmentOptions.MidlineLeft);
            SetRect(logo.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(26f, 0f), new Vector2(260f, 0f));
            logo.text = "<color=#06B6D4>FX</color> WIRE";
            logo.fontStyle = FontStyles.Bold;

            TMP_Text nav = CreateText(header.transform, "Navigation", 17f, BodyText, TextAlignmentOptions.Center);
            SetRect(nav.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(300f, 0f), new Vector2(-250f, 0f));
            nav.text = "MARKETS     CRYPTO     MACRO     SIGNAL DESK";
            nav.characterSpacing = 1.5f;

            GameObject liveBadge = CreatePanel(header.transform, "LiveBadge", new Color32(64, 20, 30, 255), false);
            Fixed(liveBadge.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(174f, 38f), new Vector2(-108f, 0f));
            AddOutline(liveBadge, RiskRed, new Vector2(2f, -2f));

            TMP_Text liveText = CreateText(liveBadge.transform, "LiveText", 18f, Color.white, TextAlignmentOptions.Center);
            Stretch(liveText.rectTransform, Vector2.zero, Vector2.zero);
            liveText.text = "24/7  LIVE";
            liveText.fontStyle = FontStyles.Bold;

            CreateHorizontalRule(header.transform, "HeaderRule", BorderColor, 0f, 0f, 68f, 2f);
        }

        private void CreateBreakingTicker(Transform parent)
        {
            GameObject ticker = CreatePanel(parent, "BreakingTicker", HeaderBackground, false);
            RectTransform tickerRect = ticker.GetComponent<RectTransform>();
            SetTopRect(tickerRect, 0f, 0f, 118f, 44f);

            GameObject badge = CreatePanel(ticker.transform, "BreakingBadge", RiskRed, false);
            SetRect(badge.GetComponent<RectTransform>(), Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(166f, 0f));

            TMP_Text badgeText = CreateText(badge.transform, "BreakingText", 20f, Color.white, TextAlignmentOptions.Center);
            Stretch(badgeText.rectTransform, Vector2.zero, Vector2.zero);
            badgeText.text = "BREAKING";
            badgeText.fontStyle = FontStyles.Bold;

            breakingMetaText = CreateText(ticker.transform, "BreakingMeta", 18f, BodyText, TextAlignmentOptions.MidlineLeft);
            Stretch(breakingMetaText.rectTransform, new Vector2(188f, 0f), new Vector2(-22f, 0f));
            breakingMetaText.text = "LIVE UPDATE  /  MARKET ALERT  /  DAY 01  09:00";
            breakingMetaText.characterSpacing = 0.8f;
        }

        private void CreateArticleColumn(Transform parent)
        {
            GameObject article = CreatePanel(parent, "ArticleColumn", DeepBackground, false);
            RectTransform articleRect = article.GetComponent<RectTransform>();
            SetRect(articleRect, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(32f, 24f), new Vector2(950f, -182f));
            AddOutline(article, BorderColor);

            GameObject scrollArea = new GameObject("ScrollArea", typeof(RectTransform), typeof(ScrollRect));
            scrollArea.transform.SetParent(article.transform, false);
            RectTransform scrollAreaRect = scrollArea.GetComponent<RectTransform>();
            Stretch(scrollAreaRect, new Vector2(8f, 8f), new Vector2(-8f, -8f));

            scrollRect = scrollArea.GetComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 38f;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.12f;

            GameObject viewport = CreatePanel(scrollArea.transform, "Viewport", new Color(1f, 1f, 1f, 0.001f), true);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            Stretch(viewportRect, Vector2.zero, new Vector2(-22f, 0f));
            viewport.AddComponent<RectMask2D>();

            GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            articleContentRect = content.GetComponent<RectTransform>();
            articleContentRect.anchorMin = new Vector2(0f, 1f);
            articleContentRect.anchorMax = new Vector2(1f, 1f);
            articleContentRect.pivot = new Vector2(0.5f, 1f);
            articleContentRect.anchoredPosition = Vector2.zero;
            articleContentRect.sizeDelta = Vector2.zero;

            VerticalLayoutGroup articleLayout = content.GetComponent<VerticalLayoutGroup>();
            articleLayout.childAlignment = TextAnchor.UpperLeft;
            articleLayout.padding = new RectOffset(30, 30, 26, 34);
            articleLayout.spacing = 18f;
            articleLayout.childControlWidth = true;
            articleLayout.childControlHeight = true;
            articleLayout.childForceExpandWidth = true;
            articleLayout.childForceExpandHeight = false;

            ContentSizeFitter contentFitter = content.GetComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            articleMetaText = CreateFlowText(content.transform, "ArticleMeta", 18f, Cyan, TextAlignmentOptions.Left);
            articleMetaText.text = "BREAKING  /  MARKET ALERT  /  EVENT";
            articleMetaText.fontStyle = FontStyles.Bold;
            articleMetaText.characterSpacing = 1.5f;

            scenarioTitleText = CreateFlowText(content.transform, "ArticleTitle", 44f, Color.white, TextAlignmentOptions.TopLeft);
            scenarioTitleText.text = "긴급 시장 속보";
            scenarioTitleText.fontStyle = FontStyles.Bold;
            scenarioTitleText.lineSpacing = 2f;

            GameObject divider = CreatePanel(content.transform, "TitleDivider", Cyan, false);
            LayoutElement dividerLayout = divider.AddComponent<LayoutElement>();
            dividerLayout.minHeight = 3f;
            dividerLayout.preferredHeight = 3f;
            dividerLayout.flexibleHeight = 0f;

            TMP_Text byline = CreateFlowText(content.transform, "Byline", 16f, MutedText, TextAlignmentOptions.Left);
            byline.text = "FX WIRE MARKET DESK  |  LIVE MARKET COVERAGE";
            byline.characterSpacing = 1f;

            scenarioDescText = CreateFlowText(content.transform, "ArticleBody", 27f, BodyText, TextAlignmentOptions.TopLeft);
            scenarioDescText.text = "현재 시장 상황을 분석하고 대응 방안을 선택해 주세요.";
            scenarioDescText.lineSpacing = 6f;
            scenarioDescText.paragraphSpacing = 8f;

            GameObject quoteCard = CreatePanel(content.transform, "AIQuoteCard", HeaderBackground, false);
            aiQuoteCardRect = quoteCard.GetComponent<RectTransform>();
            AddOutline(quoteCard, Cyan, new Vector2(2f, -2f));
            aiQuoteLayout = quoteCard.AddComponent<LayoutElement>();
            aiQuoteLayout.minHeight = 118f;
            aiQuoteLayout.preferredHeight = 118f;

            GameObject quoteAccent = CreatePanel(quoteCard.transform, "QuoteAccent", Cyan, false);
            SetRect(quoteAccent.GetComponent<RectTransform>(), Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(6f, 0f));

            aiMonologueText = CreateText(quoteCard.transform, "AIMonologue", 23f, AiText, TextAlignmentOptions.TopLeft);
            Stretch(aiMonologueText.rectTransform, new Vector2(26f, 20f), new Vector2(-24f, -20f));
            aiMonologueText.textWrappingMode = TextWrappingModes.Normal;
            aiMonologueText.overflowMode = TextOverflowModes.Overflow;
            aiMonologueText.lineSpacing = 4f;

            TMP_Text source = CreateFlowText(content.transform, "SourceFooter", 14f, MutedText, TextAlignmentOptions.Left);
            source.text = "SOURCE: FX OVERDOSE MARKET SIMULATION NETWORK  •  UPDATED IN REAL TIME";
            source.characterSpacing = 0.7f;

            GameObject scrollbarGo = CreatePanel(scrollArea.transform, "Scrollbar", HeaderBackground, true);
            RectTransform scrollbarRect = scrollbarGo.GetComponent<RectTransform>();
            SetRect(scrollbarRect, new Vector2(1f, 0f), Vector2.one, new Vector2(-12f, 0f), Vector2.zero);

            GameObject slidingArea = new GameObject("SlidingArea", typeof(RectTransform));
            slidingArea.transform.SetParent(scrollbarGo.transform, false);
            RectTransform slidingRect = slidingArea.GetComponent<RectTransform>();
            Stretch(slidingRect, new Vector2(2f, 2f), new Vector2(-2f, -2f));

            GameObject handle = CreatePanel(slidingArea.transform, "Handle", Cyan, true);
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0f, 0f);
            handleRect.anchorMax = new Vector2(1f, 0.25f);
            handleRect.offsetMin = Vector2.zero;
            handleRect.offsetMax = Vector2.zero;

            Scrollbar scrollbar = scrollbarGo.AddComponent<Scrollbar>();
            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handle.GetComponent<Image>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            scrollRect.viewport = viewportRect;
            scrollRect.content = articleContentRect;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            scrollRect.verticalScrollbarSpacing = 8f;
        }

        private void CreateResponseSidebar(Transform parent)
        {
            GameObject sidebar = CreatePanel(parent, "ResponseSidebar", DeepBackground, false);
            RectTransform sidebarRect = sidebar.GetComponent<RectTransform>();
            SetRect(sidebarRect, new Vector2(1f, 0f), Vector2.one, new Vector2(-526f, 24f), new Vector2(-32f, -182f));
            AddOutline(sidebar, BorderColor);

            GameObject sidebarAccent = CreatePanel(sidebar.transform, "SidebarAccent", Cyan, false);
            SetTopRect(sidebarAccent.GetComponent<RectTransform>(), 0f, 0f, 0f, 4f);

            TMP_Text deskTitle = CreateText(sidebar.transform, "DeskTitle", 27f, Color.white, TextAlignmentOptions.TopLeft);
            SetTopRect(deskTitle.rectTransform, 22f, 22f, 20f, 34f);
            deskTitle.text = "RESPONSE DESK";
            deskTitle.fontStyle = FontStyles.Bold;

            TMP_Text deskSubtitle = CreateText(sidebar.transform, "DeskSubtitle", 15f, MutedText, TextAlignmentOptions.TopLeft);
            SetTopRect(deskSubtitle.rectTransform, 22f, 22f, 56f, 28f);
            deskSubtitle.text = "CHOOSE ONE ACTION TO RESOLVE THIS ALERT";
            deskSubtitle.characterSpacing = 0.6f;

            CreateHorizontalRule(sidebar.transform, "DeskRule", BorderColor, 22f, 22f, 90f, 2f);

            GameObject optionsContainer = new GameObject("OptionsContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
            optionsContainer.transform.SetParent(sidebar.transform, false);
            RectTransform optionsRect = optionsContainer.GetComponent<RectTransform>();
            Stretch(optionsRect, new Vector2(18f, 58f), new Vector2(-18f, -108f));

            VerticalLayoutGroup optionsLayout = optionsContainer.GetComponent<VerticalLayoutGroup>();
            optionsLayout.childAlignment = TextAnchor.UpperCenter;
            optionsLayout.spacing = 14f;
            optionsLayout.childControlWidth = true;
            optionsLayout.childControlHeight = true;
            optionsLayout.childForceExpandWidth = true;
            optionsLayout.childForceExpandHeight = true;

            for (int i = 0; i < OptionCount; i++)
            {
                GameObject buttonGo = CreatePanel(optionsContainer.transform, $"OptionButton_{i}", HeaderBackground, true);
                RectTransform buttonRect = buttonGo.GetComponent<RectTransform>();
                buttonRect.sizeDelta = new Vector2(0f, 174f);
                buttonGo.AddComponent<RectMask2D>();

                LayoutElement buttonLayout = buttonGo.AddComponent<LayoutElement>();
                buttonLayout.minHeight = 168f;
                buttonLayout.preferredHeight = 174f;
                buttonLayout.flexibleHeight = 1f;

                optionBackgrounds[i] = buttonGo.GetComponent<Image>();
                optionOutlines[i] = AddOutline(buttonGo, BorderColor);

                optionButtons[i] = buttonGo.AddComponent<Button>();
                optionButtons[i].targetGraphic = optionBackgrounds[i];
                optionButtons[i].transition = Selectable.Transition.ColorTint;
                ColorBlock colors = optionButtons[i].colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color32(218, 250, 255, 255);
                colors.pressedColor = new Color32(166, 217, 226, 255);
                colors.selectedColor = Color.white;
                colors.disabledColor = new Color32(112, 120, 135, 210);
                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.08f;
                optionButtons[i].colors = colors;

                GameObject accent = CreatePanel(buttonGo.transform, "AccentBar", GetOptionAccent(ChoiceOptionType.Safe, i), false);
                SetRect(accent.GetComponent<RectTransform>(), Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(7f, 0f));
                optionAccentBars[i] = accent.GetComponent<Image>();

                optionTexts[i] = CreateText(buttonGo.transform, "Text", 20f, Color.white, TextAlignmentOptions.TopLeft);
                Stretch(optionTexts[i].rectTransform, new Vector2(24f, 12f), new Vector2(-16f, -12f));
                optionTexts[i].enableAutoSizing = true;
                optionTexts[i].fontSizeMin = 13f;
                optionTexts[i].fontSizeMax = 20f;
                optionTexts[i].textWrappingMode = TextWrappingModes.Normal;
                optionTexts[i].overflowMode = TextOverflowModes.Overflow;
                optionTexts[i].lineSpacing = 1.5f;
                optionTexts[i].raycastTarget = false;
            }

            TMP_Text footer = CreateText(sidebar.transform, "DecisionFooter", 14f, MutedText, TextAlignmentOptions.Center);
            SetRect(footer.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(18f, 16f), new Vector2(-18f, 48f));
            footer.text = "THE SELECTED RESPONSE IS APPLIED IMMEDIATELY";
            footer.characterSpacing = 0.4f;

            toastContainer = CreatePanel(sidebar.transform, "Toast", new Color32(62, 19, 29, 250), false);
            RectTransform toastRect = toastContainer.GetComponent<RectTransform>();
            SetRect(toastRect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(18f, 10f), new Vector2(-18f, 62f));
            AddOutline(toastContainer, ErrorRed, new Vector2(2f, -2f));

            toastText = CreateText(toastContainer.transform, "ToastText", 17f, Color.white, TextAlignmentOptions.Center);
            Stretch(toastText.rectTransform, new Vector2(12f, 6f), new Vector2(-12f, -6f));
            toastText.fontStyle = FontStyles.Bold;
            toastContainer.SetActive(false);
        }

        private void RefreshArticleLayout()
        {
            if (articleContentRect == null)
            {
                return;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(articleContentRect);

            if (aiMonologueText != null && aiQuoteCardRect != null && aiQuoteLayout != null)
            {
                float availableWidth = Mathf.Max(320f, aiQuoteCardRect.rect.width - 50f);
                float preferredHeight = aiMonologueText.GetPreferredValues(aiMonologueText.text, availableWidth, 0f).y + 44f;
                aiQuoteLayout.preferredHeight = Mathf.Max(118f, preferredHeight);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(articleContentRect);
            Canvas.ForceUpdateCanvases();
        }

        private bool RebindGeneratedUI(Transform marker)
        {
            browserAddressText = FindText(marker, "BrowserChrome/AddressBar/AddressText");
            breakingMetaText = FindText(marker, "BreakingTicker/BreakingMeta");
            articleMetaText = FindText(marker, "ArticleColumn/ScrollArea/Viewport/Content/ArticleMeta");
            scenarioTitleText = FindText(marker, "ArticleColumn/ScrollArea/Viewport/Content/ArticleTitle");
            scenarioDescText = FindText(marker, "ArticleColumn/ScrollArea/Viewport/Content/ArticleBody");
            aiMonologueText = FindText(marker, "ArticleColumn/ScrollArea/Viewport/Content/AIQuoteCard/AIMonologue");
            toastText = FindText(marker, "ResponseSidebar/Toast/ToastText");

            Transform scrollTransform = marker.Find("ArticleColumn/ScrollArea");
            scrollRect = scrollTransform != null ? scrollTransform.GetComponent<ScrollRect>() : null;

            Transform contentTransform = marker.Find("ArticleColumn/ScrollArea/Viewport/Content");
            articleContentRect = contentTransform as RectTransform;

            Transform quoteTransform = marker.Find("ArticleColumn/ScrollArea/Viewport/Content/AIQuoteCard");
            aiQuoteCardRect = quoteTransform as RectTransform;
            aiQuoteLayout = quoteTransform != null ? quoteTransform.GetComponent<LayoutElement>() : null;

            Transform toastTransform = marker.Find("ResponseSidebar/Toast");
            toastContainer = toastTransform != null ? toastTransform.gameObject : null;

            for (int i = 0; i < OptionCount; i++)
            {
                Transform buttonTransform = marker.Find($"ResponseSidebar/OptionsContainer/OptionButton_{i}");
                optionButtons[i] = buttonTransform != null ? buttonTransform.GetComponent<Button>() : null;
                optionTexts[i] = buttonTransform != null ? FindText(buttonTransform, "Text") : null;
                optionBackgrounds[i] = buttonTransform != null ? buttonTransform.GetComponent<Image>() : null;
                optionOutlines[i] = buttonTransform != null ? buttonTransform.GetComponent<Outline>() : null;

                Transform accentTransform = buttonTransform != null ? buttonTransform.Find("AccentBar") : null;
                optionAccentBars[i] = accentTransform != null ? accentTransform.GetComponent<Image>() : null;
            }

            bool hasCoreReferences = scrollRect != null && articleContentRect != null && scenarioTitleText != null &&
                                     scenarioDescText != null && aiMonologueText != null && toastText != null;

            for (int i = 0; i < OptionCount && hasCoreReferences; i++)
            {
                hasCoreReferences = optionButtons[i] != null && optionTexts[i] != null &&
                                    optionBackgrounds[i] != null && optionAccentBars[i] != null;
            }

            return hasCoreReferences;
        }

        private void ApplyOptionVisual(int index, ChoiceOptionType type, bool isAvailable)
        {
            if (index < 0 || index >= OptionCount)
            {
                return;
            }

            Color accent = isAvailable ? GetOptionAccent(type, index) : disabledAccent;

            if (optionBackgrounds[index] != null)
            {
                Color tint = Color.Lerp(HeaderBackground, accent, isAvailable ? 0.13f : 0.04f);
                tint.a = 1f;
                optionBackgrounds[index].color = tint;
            }

            if (optionAccentBars[index] != null)
            {
                optionAccentBars[index].color = accent;
            }

            if (optionOutlines[index] != null)
            {
                optionOutlines[index].effectColor = isAvailable
                    ? Color.Lerp(BorderColor, accent, 0.62f)
                    : BorderColor;
            }
        }

        private static bool IsOptionAvailable(ChoiceOptionData option, TraderStatus traderStatus)
        {
            if (option == null)
            {
                return false;
            }

            if (option.OptionType != ChoiceOptionType.SpecialItem || string.IsNullOrWhiteSpace(option.RequiredItemId))
            {
                return true;
            }

            return traderStatus != null && traderStatus.HasItem(option.RequiredItemId, option.RequiredItemCount);
        }

        private static string GetEventCategory(EventTriggerCondition condition)
        {
            return condition switch
            {
                EventTriggerCondition.TimeOfDay => "MARKET WATCH",
                EventTriggerCondition.LowMental => "RISK ALERT",
                EventTriggerCondition.HighLeverage => "LEVERAGE ALERT",
                _ => "LIVE MARKET"
            };
        }

        private static string GetOptionLabel(ChoiceOptionType type)
        {
            return type switch
            {
                ChoiceOptionType.Safe => "SAFE RESPONSE",
                ChoiceOptionType.Aggressive => "HIGH RISK",
                ChoiceOptionType.SpecialItem => "ACTIVE GEAR",
                ChoiceOptionType.DirectionalLong => "LONG SIGNAL",
                ChoiceOptionType.DirectionalShort => "SHORT SIGNAL",
                _ => "RESPONSE"
            };
        }

        private static Color GetOptionAccent(ChoiceOptionType type, int fallbackIndex)
        {
            return type switch
            {
                ChoiceOptionType.Safe => SafeGreen,
                ChoiceOptionType.Aggressive => RiskRed,
                ChoiceOptionType.SpecialItem => SpecialGold,
                ChoiceOptionType.DirectionalLong => SafeGreen,
                ChoiceOptionType.DirectionalShort => RiskRed,
                _ => fallbackIndex == 0 ? SafeGreen : (fallbackIndex == 1 ? RiskRed : SpecialGold)
            };
        }

        private static string ToUrlSlug(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "market-alert";
            }

            return value.Trim().ToLowerInvariant().Replace(' ', '-').Replace('_', '-');
        }

        private void EnsureRuntimeArrays()
        {
            if (optionButtons == null || optionButtons.Length != OptionCount)
            {
                optionButtons = new Button[OptionCount];
            }

            if (optionTexts == null || optionTexts.Length != OptionCount)
            {
                optionTexts = new TMP_Text[OptionCount];
            }

            if (optionBackgrounds == null || optionBackgrounds.Length != OptionCount)
            {
                optionBackgrounds = new Image[OptionCount];
            }

            if (optionAccentBars == null || optionAccentBars.Length != OptionCount)
            {
                optionAccentBars = new Image[OptionCount];
            }

            if (optionOutlines == null || optionOutlines.Length != OptionCount)
            {
                optionOutlines = new Outline[OptionCount];
            }
        }

        private void ResetRuntimeReferences()
        {
            scenarioTitleText = null;
            scenarioDescText = null;
            aiMonologueText = null;
            toastText = null;
            browserAddressText = null;
            breakingMetaText = null;
            articleMetaText = null;
            scrollRect = null;
            articleContentRect = null;
            aiQuoteCardRect = null;
            aiQuoteLayout = null;
            toastContainer = null;
            optionButtons = new Button[OptionCount];
            optionTexts = new TMP_Text[OptionCount];
            optionBackgrounds = new Image[OptionCount];
            optionAccentBars = new Image[OptionCount];
            optionOutlines = new Outline[OptionCount];
        }

        private Canvas FindTargetCanvas()
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                return parentCanvas.rootCanvas;
            }

            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
            foreach (Canvas candidate in canvases)
            {
                if (candidate != null && candidate.name == "TradingViewCanvas")
                {
                    return candidate;
                }
            }

            Canvas bestCanvas = null;
            float bestScore = float.MinValue;

            foreach (Canvas candidate in canvases)
            {
                if (candidate == null || !candidate.isRootCanvas)
                {
                    continue;
                }

                float score = candidate.renderMode == RenderMode.ScreenSpaceOverlay ? 1000f : 0f;
                CanvasScaler scaler = candidate.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    Vector2 reference = scaler.referenceResolution;
                    score += 500f - Vector2.Distance(reference, new Vector2(1920f, 1080f)) * 0.1f;
                }

                RectTransform rect = candidate.transform as RectTransform;
                if (rect != null)
                {
                    score += Mathf.Min(300f, rect.rect.width * rect.rect.height / 10000f);
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCanvas = candidate;
                }
            }

            return bestCanvas;
        }

        // 차트(5), HUD(20), 상점(100)보다 위에서 렌더링하고 설정창(200)은 최상단으로 유지합니다.
        private void EnsureOverlayPriority()
        {
            if (popupPanel == null)
            {
                return;
            }

            Canvas popupCanvas = popupPanel.GetComponent<Canvas>();
            if (popupCanvas == null)
            {
                popupCanvas = popupPanel.AddComponent<Canvas>();
            }

            popupCanvas.overrideSorting = true;
            popupCanvas.sortingOrder = EventPopupSortingOrder;

            if (popupPanel.GetComponent<GraphicRaycaster>() == null)
            {
                popupPanel.AddComponent<GraphicRaycaster>();
            }
        }

        private static GameObject CreatePanel(Transform parent, string name, Color color, bool raycastTarget)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(parent, false);
            Image image = panel.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return panel;
        }

        private static TMP_Text CreateText(Transform parent, string name, float fontSize, Color color, TextAlignmentOptions alignment)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            ConfigureText(text, fontSize, color, alignment);
            return text;
        }

        private static TMP_Text CreateFlowText(Transform parent, string name, float fontSize, Color color, TextAlignmentOptions alignment)
        {
            TMP_Text text = CreateText(parent, name, fontSize, color, alignment);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void ConfigureText(TMP_Text text, float fontSize, Color color, TextAlignmentOptions alignment)
        {
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            text.richText = true;

            TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
            if (defaultFont != null)
            {
                text.font = defaultFont;
            }
        }

        private static TMP_Text FindText(Transform parent, string path)
        {
            Transform target = parent != null ? parent.Find(path) : null;
            return target != null ? target.GetComponent<TMP_Text>() : null;
        }

        private static Outline AddOutline(GameObject target, Color color, Vector2? distance = null)
        {
            Outline outline = target.GetComponent<Outline>();
            if (outline == null)
            {
                outline = target.AddComponent<Outline>();
            }

            outline.effectColor = color;
            outline.effectDistance = distance ?? global::UIStrokeStyle.EffectDistance;
            outline.useGraphicAlpha = true;
            return outline;
        }

        private static void CreatePixelIndicator(Transform parent, string name, Color color, float x)
        {
            GameObject indicator = CreatePanel(parent, name, color, false);
            Fixed(indicator.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(12f, 12f), new Vector2(x, 0f));
        }

        private static void CreateHorizontalRule(Transform parent, string name, Color color, float left, float right, float top, float height)
        {
            GameObject rule = CreatePanel(parent, name, color, false);
            SetTopRect(rule.GetComponent<RectTransform>(), left, right, top, height);
        }

        private static void Fixed(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 anchoredPosition)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void SetTopRect(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
