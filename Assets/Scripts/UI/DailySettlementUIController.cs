using System.Collections;
using FXOverdose.AI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FXOverdose.UI
{
    /// <summary>
    /// 매일 24:00에 순자산 기준 일일 손익과 요미의 정산 반응을 표시합니다.
    /// GameManager의 Settlement 상태는 유지하고 Time.timeScale은 변경하지 않습니다.
    /// </summary>
    public sealed class DailySettlementUIController : MonoBehaviour
    {
        // 스킬 상세(280), 설정 메뉴(200), 돌발 이벤트(150)보다 항상 위에 표시합니다.
        private const int SettlementSortingOrder = 1000;
        private const float ModalWidth = 1280f;
        private const float ModalHeight = 820f;
        private const float DialogueWaitTimeout = 8f;

        private static readonly Color DeepBackground = new Color32(11, 15, 25, 255);   // #0B0F19
        private static readonly Color PanelBackground = new Color32(15, 23, 42, 255);  // #0F172A
        private static readonly Color HeaderBackground = new Color32(20, 29, 51, 255); // #141D33
        private static readonly Color BorderColor = new Color32(59, 75, 102, 255);      // #3B4B66
        private static readonly Color Cyan = new Color32(6, 182, 212, 255);             // #06B6D4
        private static readonly Color BodyText = new Color32(203, 213, 225, 255);       // #CBD5E1
        private static readonly Color MutedText = new Color32(102, 117, 143, 255);      // #66758F
        private static readonly Color AiText = new Color32(207, 250, 254, 255);         // #CFFAFE
        private static readonly Color ProfitGreen = new Color32(34, 197, 94, 255);      // #22C55E
        private static readonly Color LossRed = new Color32(239, 68, 68, 255);          // #EF4444
        private static readonly Color NeutralGold = new Color32(234, 179, 8, 255);      // #EAB308

        private GameManager gameManager;
        private GameObject overlayRoot;
        private CanvasGroup overlayCanvasGroup;
        private RectTransform modalRect;
        private Image modalAccent;
        private Image pnlCardBackground;
        private Outline pnlCardOutline;
        private Image characterImage;
        private TMP_Text ledgerLabel;
        private TMP_Text titleText;
        private TMP_Text nextDayButtonText;
        private TMP_Text dailyPnlLabelText;
        private TMP_Text pnlValueText;
        private TMP_Text pnlPercentText;
        private TMP_Text resultBadgeText;
        private TMP_Text startingEquityText;
        private TMP_Text totalEquityText;
        private TMP_Text reactionText;
        private TMP_Text reactionStatusText;
        private TMP_Text emotionLabelText;
        private TMP_Text healthValueText;
        private TMP_Text mentalValueText;
        private RectTransform healthFillRect;
        private RectTransform mentalFillRect;
        private Button proceedButton;
        private Image proceedButtonBackground;

        private Coroutine transitionCoroutine;
        private Coroutine dialogueTimeoutCoroutine;
        private bool settlementRequestActive;
        private bool isClosing;
        private int lastPresentedDay = -1;
        private float currentDailyPnl;
        private float currentDailyReturn;
        private float currentTotalEquity;

        private void Awake()
        {
            ResolveSystems();
            EnsureUIBuilt();
            HideImmediate();
        }

        private void OnEnable()
        {
            BindEvents();
        }

        private void Start()
        {
            BindEvents();
        }

        private void OnDisable()
        {
            UnbindEvents();
        }

        private void OnDestroy()
        {
            UnbindEvents();
        }

        private void ResolveSystems()
        {
            if (gameManager == null)
            {
                gameManager = FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            }

        }

        private void BindEvents()
        {
            ResolveSystems();

            if (gameManager != null)
            {
                gameManager.OnDayEnded -= HandleDayEnded;
                gameManager.OnDayEnded += HandleDayEnded;
            }

            }

        private void UnbindEvents()
        {
            if (gameManager != null)
            {
                gameManager.OnDayEnded -= HandleDayEnded;
            }

            }

        private void HandleDayEnded()
        {
            ResolveSystems();
            if (gameManager == null || gameManager.CurrentState != GameManager.GameState.Settlement)
            {
                return;
            }

            if (lastPresentedDay == gameManager.CurrentDay && overlayRoot != null && overlayRoot.activeSelf)
            {
                return;
            }

            lastPresentedDay = gameManager.CurrentDay;
            ShowSettlement();
        }

        public void ShowSettlement()
        {
            ResolveSystems();
            EnsureUIBuilt();

            if (gameManager == null || overlayRoot == null)
            {
                Debug.LogWarning("[DailySettlementUI] GameManager 또는 정산 UI를 준비하지 못했습니다.");
                return;
            }

            TraderStatus status = TraderStatus.CanonicalInstance;
            currentTotalEquity = status != null ? status.GetTotalEquity() : gameManager.CurrentBalance;

            float startingEquity = gameManager.StartOfDayEquity;
            if (!float.IsFinite(startingEquity) || startingEquity <= 0.001f)
            {
                startingEquity = currentTotalEquity;
                Debug.LogWarning("[DailySettlementUI] 시작 자산 기록이 없어 현재 순자산을 기준값으로 사용했습니다.");
            }

            currentDailyPnl = currentTotalEquity - startingEquity;
            currentDailyReturn = Mathf.Abs(startingEquity) > 0.001f
                ? currentDailyPnl / startingEquity * 100f
                : 0f;

            UpdateSettlementValues(startingEquity, status);
            UpdateResultTheme();
            UpdateCharacterSprite();

            settlementRequestActive = true;
            isClosing = false;
            SetProceedInteractable(false);
            reactionText.text = GetImmediateReaction(currentDailyPnl);
            reactionStatusText.text = "GENERATING YOMI COMMENT...";

            overlayRoot.SetActive(true);
            overlayRoot.transform.SetAsLastSibling();
            overlayCanvasGroup.alpha = 0f;
            overlayCanvasGroup.blocksRaycasts = true;
            overlayCanvasGroup.interactable = true;
            modalRect.anchoredPosition = new Vector2(0f, 72f);
            modalRect.localScale = new Vector3(0.985f, 0.985f, 1f);

            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }
            transitionCoroutine = StartCoroutine(AnimateIn());

            RequestSettlementDialogue();
        }

        private void UpdateSettlementValues(float startingEquity, TraderStatus status)
        {
            int day = Mathf.Max(1, gameManager.CurrentDay);
            ledgerLabel.text = $"FX OVERDOSE  /  DAILY LEDGER  /  #{day:000}";
            titleText.text = $"DAY {day:00} SETTLEMENT";
            nextDayButtonText.text = $"PROCEED TO DAY {day + 1:00}";
            dailyPnlLabelText.text = gameManager.IsDailyPnlPartial ? "P&L SINCE LOAD" : "DAILY P&L";

            pnlValueText.text = FormatSignedCurrency(currentDailyPnl);
            pnlPercentText.text = $"{FormatSignedPercent(currentDailyReturn)}  /  NET EQUITY CHANGE";
            startingEquityText.text = $"${startingEquity:N2}";
            totalEquityText.text = $"${currentTotalEquity:N2}";

            float healthRatio = status != null ? Mathf.Clamp01(status.HealthRatio) : 1f;
            float mentalRatio = status != null && status.EffectiveMaxMental > 0.001f
                ? Mathf.Clamp01(status.CurrentMental / status.EffectiveMaxMental)
                : 1f;
            healthFillRect.anchorMax = new Vector2(healthRatio, 1f);
            mentalFillRect.anchorMax = new Vector2(mentalRatio, 1f);
            healthValueText.text = status != null
                ? $"{status.CurrentHealth:0}/{status.MaxHealth:0}"
                : "--/--";
            mentalValueText.text = status != null
                ? $"{status.CurrentMental:0}/{status.EffectiveMaxMental:0}"
                : "--/--";
        }

        private void UpdateResultTheme()
        {
            Color resultColor;
            string resultLabel;

            if (currentDailyPnl > 0.005f)
            {
                resultColor = ProfitGreen;
                resultLabel = "▲  PROFIT SESSION";
            }
            else if (currentDailyPnl < -0.005f)
            {
                resultColor = LossRed;
                resultLabel = "▼  LOSS SESSION";
            }
            else
            {
                resultColor = NeutralGold;
                resultLabel = "■  FLAT SESSION";
            }

            modalAccent.color = resultColor;
            pnlValueText.color = resultColor;
            pnlPercentText.color = resultColor;
            resultBadgeText.color = resultColor;
            resultBadgeText.text = resultLabel;

            Color cardTint = Color.Lerp(HeaderBackground, resultColor, 0.09f);
            cardTint.a = 1f;
            pnlCardBackground.color = cardTint;
            pnlCardOutline.effectColor = Color.Lerp(BorderColor, resultColor, 0.65f);
        }

        private void UpdateCharacterSprite()
        {
            TraderEmotion emotion = GetSettlementEmotion(currentDailyReturn, currentDailyPnl);
            Sprite sprite = Resources.Load<Sprite>($"Characters/Emotions/{emotion}");

            if (sprite == null)
            {
                GameObject existingCharacter = GameObject.Find("ProtagonistCharacterImage");
                Image existingImage = existingCharacter != null ? existingCharacter.GetComponent<Image>() : null;
                sprite = existingImage != null ? existingImage.sprite : null;
            }

            characterImage.sprite = sprite;
            characterImage.enabled = sprite != null;
            characterImage.preserveAspect = true;
            emotionLabelText.text = $"YOMI  /  {GetEmotionLabel(emotion)}";

            AIVisualController visualController = FindAnyObjectByType<AIVisualController>(FindObjectsInactive.Include);
            visualController?.ShowEmotion(emotion, 5f);
        }

        private void RequestSettlementDialogue()
        {
            if (dialogueTimeoutCoroutine != null)
            {
                StopCoroutine(dialogueTimeoutCoroutine);
            }
            dialogueTimeoutCoroutine = StartCoroutine(UnlockProceedAfterTimeout());

            settlementRequestActive = false;
            reactionStatusText.text = "LOCAL SUMMARY READY";
            SetProceedInteractable(true);
        }



        private IEnumerator UnlockProceedAfterTimeout()
        {
            float elapsed = 0f;
            while (elapsed < DialogueWaitTimeout && settlementRequestActive)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (settlementRequestActive && overlayRoot != null && overlayRoot.activeSelf)
            {
                reactionStatusText.text = "LOCAL SUMMARY READY";
                SetProceedInteractable(true);
            }

            dialogueTimeoutCoroutine = null;
        }

        private void HandleProceedClicked()
        {
            if (isClosing || gameManager == null || gameManager.CurrentState != GameManager.GameState.Settlement)
            {
                return;
            }

            isClosing = true;
            settlementRequestActive = false;
            SetProceedInteractable(false);

            if (dialogueTimeoutCoroutine != null)
            {
                StopCoroutine(dialogueTimeoutCoroutine);
                dialogueTimeoutCoroutine = null;
            }

            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }
            transitionCoroutine = StartCoroutine(AnimateOutAndProceed());
        }

        private IEnumerator AnimateIn()
        {
            const float duration = 0.28f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                overlayCanvasGroup.alpha = eased;
                modalRect.anchoredPosition = Vector2.LerpUnclamped(new Vector2(0f, 72f), Vector2.zero, eased);
                modalRect.localScale = Vector3.LerpUnclamped(new Vector3(0.985f, 0.985f, 1f), Vector3.one, eased);
                yield return null;
            }

            overlayCanvasGroup.alpha = 1f;
            modalRect.anchoredPosition = Vector2.zero;
            modalRect.localScale = Vector3.one;
            transitionCoroutine = null;
        }

        private IEnumerator AnimateOutAndProceed()
        {
            BuildDayTransitionOverlay(
                out GameObject dayTransitionRoot,
                out CanvasGroup dayTransitionGroup,
                out Image sleepingYomiImage);

            const float duration = 0.45f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                overlayCanvasGroup.alpha = 1f - t;
                modalRect.anchoredPosition = Vector2.Lerp(Vector2.zero, new Vector2(0f, -34f), t);
                dayTransitionGroup.alpha = t;
                sleepingYomiImage.rectTransform.localScale = Vector3.Lerp(
                    new Vector3(0.96f, 0.96f, 1f),
                    Vector3.one,
                    t);
                yield return null;
            }

            HideImmediate();

            dayTransitionGroup.alpha = 1f;
            sleepingYomiImage.rectTransform.localScale = Vector3.one;
            yield return new WaitForSecondsRealtime(2.1f);

            // 화면이 완전히 가려진 상태에서 실제 날짜·차트·시장 상태를 다음 날로 전환합니다.
            gameManager.ProceedToNextDay();

            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                dayTransitionGroup.alpha = 1f - t;
                yield return null;
            }

            Destroy(dayTransitionRoot);
            transitionCoroutine = null;
            isClosing = false;
        }

        private void BuildDayTransitionOverlay(
            out GameObject root,
            out CanvasGroup group,
            out Image sleepingImage)
        {
            Transform parent = overlayRoot != null && overlayRoot.transform.parent != null
                ? overlayRoot.transform.parent
                : transform;

            root = new GameObject(
                "NextDaySleepTransition",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Canvas),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            root.transform.SetParent(parent, false);

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = Color.black;

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = SettlementSortingOrder + 100;

            group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = true;
            group.interactable = true;

            GameObject character = new GameObject(
                "SleepingYomi",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            character.transform.SetParent(root.transform, false);
            sleepingImage = character.GetComponent<Image>();
            sleepingImage.sprite = Resources.Load<Sprite>("UI/DayTransition/SleepingYomi");
            sleepingImage.preserveAspect = true;
            sleepingImage.raycastTarget = false;
            RectTransform characterRect = sleepingImage.rectTransform;
            characterRect.anchorMin = characterRect.anchorMax = new Vector2(0.5f, 0.5f);
            characterRect.pivot = new Vector2(0.5f, 0.5f);
            characterRect.sizeDelta = new Vector2(680f, 680f);
            characterRect.anchoredPosition = new Vector2(0f, 18f);

            GameObject captionObject = new GameObject(
                "NightCaption",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            captionObject.transform.SetParent(root.transform, false);
            TMP_Text caption = captionObject.GetComponent<TextMeshProUGUI>();
            caption.font = TMP_Settings.defaultFontAsset;
            caption.text = "YOMI IS RESTING...";
            caption.fontSize = 24f;
            caption.fontStyle = FontStyles.Bold;
            caption.alignment = TextAlignmentOptions.Center;
            caption.color = new Color32(102, 117, 143, 255);
            caption.raycastTarget = false;
            RectTransform captionRect = caption.rectTransform;
            captionRect.anchorMin = captionRect.anchorMax = new Vector2(0.5f, 0.5f);
            captionRect.sizeDelta = new Vector2(720f, 60f);
            captionRect.anchoredPosition = new Vector2(0f, -300f);
        }

        private void SetProceedInteractable(bool interactable)
        {
            if (proceedButton == null || proceedButtonBackground == null)
            {
                return;
            }

            proceedButton.interactable = interactable;
            proceedButtonBackground.color = interactable
                ? new Color32(10, 126, 151, 255)
                : new Color32(43, 58, 75, 255);
            nextDayButtonText.color = interactable ? Color.white : MutedText;
        }

        private void HideImmediate()
        {
            if (overlayRoot == null)
            {
                return;
            }

            overlayCanvasGroup.alpha = 0f;
            overlayCanvasGroup.blocksRaycasts = false;
            overlayCanvasGroup.interactable = false;
            overlayRoot.SetActive(false);
        }

        private void EnsureUIBuilt()
        {
            if (overlayRoot != null)
            {
                return;
            }

            Transform canvasTransform = transform;
            Canvas parentCanvas = GetComponent<Canvas>();
            if (parentCanvas == null)
            {
                parentCanvas = GetComponentInParent<Canvas>();
                canvasTransform = parentCanvas != null ? parentCanvas.transform : transform;
            }

            overlayRoot = CreatePanel(canvasTransform, "DailySettlementOverlay", new Color(0f, 0f, 0f, 0.82f), true);
            RectTransform overlayRect = overlayRoot.GetComponent<RectTransform>();
            Stretch(overlayRect, Vector2.zero, Vector2.zero);

            Canvas overlayCanvas = overlayRoot.AddComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = SettlementSortingOrder;
            overlayRoot.AddComponent<GraphicRaycaster>();
            overlayCanvasGroup = overlayRoot.AddComponent<CanvasGroup>();

            GameObject modal = CreatePanel(overlayRoot.transform, "DailyLedger", PanelBackground, false);
            modalRect = modal.GetComponent<RectTransform>();
            Fixed(modalRect, new Vector2(0.5f, 0.5f), new Vector2(ModalWidth, ModalHeight), Vector2.zero);

            Shadow shadow = modal.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.82f);
            shadow.effectDistance = new Vector2(14f, -14f);
            shadow.useGraphicAlpha = true;
            AddOutline(modal, BorderColor);

            GameObject accent = CreatePanel(modal.transform, "ResultAccent", Cyan, false);
            modalAccent = accent.GetComponent<Image>();
            SetTopRect(accent.GetComponent<RectTransform>(), 0f, 0f, 0f, 5f);

            BuildHeader(modal.transform);
            BuildCharacterColumn(modal.transform);
            BuildSummaryColumn(modal.transform);
            BuildFooter(modal.transform);
        }

        private void BuildHeader(Transform parent)
        {
            GameObject header = CreatePanel(parent, "Header", HeaderBackground, false);
            SetTopRect(header.GetComponent<RectTransform>(), 0f, 0f, 5f, 103f);

            ledgerLabel = CreateText(header.transform, "LedgerLabel", 15f, Cyan, TextAlignmentOptions.TopLeft);
            SetTopRect(ledgerLabel.rectTransform, 28f, 360f, 15f, 22f);
            ledgerLabel.text = "FX OVERDOSE  /  DAILY LEDGER  /  #001";
            ledgerLabel.characterSpacing = 1.2f;

            titleText = CreateText(header.transform, "Title", 39f, Color.white, TextAlignmentOptions.TopLeft);
            SetTopRect(titleText.rectTransform, 28f, 410f, 38f, 50f);
            titleText.text = "DAY 01 SETTLEMENT";
            titleText.fontStyle = FontStyles.Bold;

            GameObject closedBadge = CreatePanel(header.transform, "ClosedBadge", DeepBackground, false);
            RectTransform badgeRect = closedBadge.GetComponent<RectTransform>();
            Fixed(badgeRect, new Vector2(1f, 0.5f), new Vector2(280f, 54f), new Vector2(-164f, 0f));
            AddOutline(closedBadge, BorderColor, new Vector2(2f, -2f));

            TMP_Text badgeText = CreateText(closedBadge.transform, "ClosedText", 17f, BodyText, TextAlignmentOptions.Center);
            Stretch(badgeText.rectTransform, new Vector2(10f, 4f), new Vector2(-10f, -4f));
            badgeText.text = "SESSION CLOSED  /  24:00";
            badgeText.characterSpacing = 0.7f;

            CreateHorizontalRule(header.transform, "HeaderRule", BorderColor, 0f, 0f, 101f, 2f);
        }

        private void BuildCharacterColumn(Transform parent)
        {
            GameObject column = CreatePanel(parent, "YomiColumn", DeepBackground, false);
            RectTransform columnRect = column.GetComponent<RectTransform>();
            SetRect(columnRect, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(28f, 92f), new Vector2(420f, -128f));
            AddOutline(column, BorderColor);

            emotionLabelText = CreateText(column.transform, "EmotionLabel", 17f, Cyan, TextAlignmentOptions.TopLeft);
            SetTopRect(emotionLabelText.rectTransform, 20f, 20f, 15f, 26f);
            emotionLabelText.text = "YOMI  /  FOCUSED";
            emotionLabelText.fontStyle = FontStyles.Bold;
            emotionLabelText.characterSpacing = 0.8f;

            GameObject stageGlow = CreatePanel(column.transform, "CharacterStage", new Color32(13, 30, 48, 255), false);
            RectTransform stageRect = stageGlow.GetComponent<RectTransform>();
            Stretch(stageRect, new Vector2(18f, 226f), new Vector2(-18f, -48f));
            AddOutline(stageGlow, new Color32(32, 66, 88, 255), new Vector2(2f, -2f));

            GameObject characterObject = new GameObject("SettlementYomi", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            characterObject.transform.SetParent(stageGlow.transform, false);
            characterImage = characterObject.GetComponent<Image>();
            characterImage.color = Color.white;
            characterImage.raycastTarget = false;
            characterImage.preserveAspect = true;
            Stretch(characterImage.rectTransform, new Vector2(8f, 6f), new Vector2(-8f, -6f));

            GameObject quotePanel = CreatePanel(column.transform, "ReactionCard", HeaderBackground, false);
            RectTransform quoteRect = quotePanel.GetComponent<RectTransform>();
            SetRect(quoteRect, Vector2.zero, new Vector2(1f, 0f), new Vector2(18f, 18f), new Vector2(-18f, 208f));
            AddOutline(quotePanel, Cyan, new Vector2(2f, -2f));

            GameObject quoteAccent = CreatePanel(quotePanel.transform, "QuoteAccent", Cyan, false);
            SetRect(quoteAccent.GetComponent<RectTransform>(), Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(6f, 0f));

            reactionStatusText = CreateText(quotePanel.transform, "ReactionStatus", 13f, MutedText, TextAlignmentOptions.TopLeft);
            SetTopRect(reactionStatusText.rectTransform, 22f, 16f, 13f, 22f);
            reactionStatusText.text = "GENERATING YOMI COMMENT...";
            reactionStatusText.characterSpacing = 0.7f;

            reactionText = CreateText(quotePanel.transform, "ReactionText", 20f, AiText, TextAlignmentOptions.Center);
            Stretch(reactionText.rectTransform, new Vector2(22f, 18f), new Vector2(-16f, -42f));
            reactionText.textWrappingMode = TextWrappingModes.Normal;
            reactionText.overflowMode = TextOverflowModes.Ellipsis;
            reactionText.enableAutoSizing = true;
            reactionText.fontSizeMin = 13f;
            reactionText.fontSizeMax = 20f;
            reactionText.lineSpacing = 3f;
        }

        private void BuildSummaryColumn(Transform parent)
        {
            GameObject column = CreatePanel(parent, "SummaryColumn", DeepBackground, false);
            RectTransform columnRect = column.GetComponent<RectTransform>();
            SetRect(columnRect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(446f, 92f), new Vector2(-28f, -128f));
            AddOutline(column, BorderColor);

            TMP_Text sectionTitle = CreateText(column.transform, "SectionTitle", 19f, BodyText, TextAlignmentOptions.TopLeft);
            SetTopRect(sectionTitle.rectTransform, 20f, 20f, 16f, 28f);
            sectionTitle.text = "PERFORMANCE SUMMARY";
            sectionTitle.fontStyle = FontStyles.Bold;
            sectionTitle.characterSpacing = 0.8f;

            GameObject pnlCard = CreatePanel(column.transform, "DailyPnlCard", HeaderBackground, false);
            pnlCardBackground = pnlCard.GetComponent<Image>();
            SetTopRect(pnlCard.GetComponent<RectTransform>(), 20f, 20f, 54f, 170f);
            pnlCardOutline = AddOutline(pnlCard, Cyan);

            dailyPnlLabelText = CreateText(pnlCard.transform, "PnlLabel", 17f, MutedText, TextAlignmentOptions.TopLeft);
            SetTopRect(dailyPnlLabelText.rectTransform, 22f, 220f, 17f, 24f);
            dailyPnlLabelText.text = "DAILY P&L";
            dailyPnlLabelText.characterSpacing = 1f;

            resultBadgeText = CreateText(pnlCard.transform, "ResultBadge", 17f, ProfitGreen, TextAlignmentOptions.TopRight);
            SetTopRect(resultBadgeText.rectTransform, 240f, 22f, 17f, 24f);
            resultBadgeText.text = "▲  PROFIT SESSION";
            resultBadgeText.fontStyle = FontStyles.Bold;

            pnlValueText = CreateText(pnlCard.transform, "PnlValue", 53f, ProfitGreen, TextAlignmentOptions.Left);
            SetTopRect(pnlValueText.rectTransform, 22f, 22f, 49f, 67f);
            pnlValueText.text = "+$0.00";
            pnlValueText.fontStyle = FontStyles.Bold;
            pnlValueText.enableAutoSizing = true;
            pnlValueText.fontSizeMin = 34f;
            pnlValueText.fontSizeMax = 53f;

            pnlPercentText = CreateText(pnlCard.transform, "PnlPercent", 18f, ProfitGreen, TextAlignmentOptions.BottomLeft);
            SetRect(pnlPercentText.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(22f, 17f), new Vector2(-22f, 45f));
            pnlPercentText.text = "+0.00%  /  NET EQUITY CHANGE";
            pnlPercentText.characterSpacing = 0.5f;

            GameObject startCard = CreateMetricCard(column.transform, "StartingEquityCard", "OPENING EQUITY", out startingEquityText);
            SetTopRect(startCard.GetComponent<RectTransform>(), 20f, 411f, 242f, 104f);

            GameObject totalCard = CreateMetricCard(column.transform, "TotalEquityCard", "TOTAL EQUITY", out totalEquityText);
            SetTopRect(totalCard.GetComponent<RectTransform>(), 411f, 20f, 242f, 104f);
            totalEquityText.color = Cyan;

            GameObject condition = CreatePanel(column.transform, "ClosingCondition", HeaderBackground, false);
            RectTransform conditionRect = condition.GetComponent<RectTransform>();
            SetRect(conditionRect, Vector2.zero, Vector2.one, new Vector2(20f, 22f), new Vector2(-20f, -365f));
            AddOutline(condition, BorderColor, new Vector2(2f, -2f));

            TMP_Text conditionTitle = CreateText(condition.transform, "ConditionTitle", 15f, MutedText, TextAlignmentOptions.TopLeft);
            SetTopRect(conditionTitle.rectTransform, 18f, 18f, 12f, 22f);
            conditionTitle.text = "CLOSING CONDITION  /  MARKED AT MARKET";
            conditionTitle.characterSpacing = 0.7f;

            healthFillRect = CreateVitalRow(condition.transform, "Health", "HP", ProfitGreen, 43f, out healthValueText);
            mentalFillRect = CreateVitalRow(condition.transform, "Mental", "MENTAL", new Color32(168, 85, 247, 255), 94f, out mentalValueText);
        }

        private void BuildFooter(Transform parent)
        {
            TMP_Text note = CreateText(parent, "LedgerNote", 14f, MutedText, TextAlignmentOptions.BottomLeft);
            SetRect(note.rectTransform, Vector2.zero, new Vector2(0f, 0f), new Vector2(30f, 24f), new Vector2(690f, 72f));
            note.text = "EQUITY INCLUDES CASH, MARGIN AND UNREALIZED P&L";
            note.characterSpacing = 0.6f;

            GameObject buttonObject = CreatePanel(parent, "ProceedButton", new Color32(10, 126, 151, 255), true);
            proceedButtonBackground = buttonObject.GetComponent<Image>();
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            SetRect(buttonRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-430f, 22f), new Vector2(-30f, 74f));
            AddOutline(buttonObject, Cyan);

            proceedButton = buttonObject.AddComponent<Button>();
            proceedButton.targetGraphic = proceedButtonBackground;
            proceedButton.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = proceedButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color32(210, 250, 255, 255);
            colors.pressedColor = new Color32(158, 215, 224, 255);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color32(118, 126, 140, 210);
            colors.fadeDuration = 0.08f;
            proceedButton.colors = colors;
            proceedButton.onClick.AddListener(HandleProceedClicked);

            nextDayButtonText = CreateText(buttonObject.transform, "Text", 20f, Color.white, TextAlignmentOptions.Center);
            Stretch(nextDayButtonText.rectTransform, new Vector2(12f, 4f), new Vector2(-12f, -4f));
            nextDayButtonText.text = "PROCEED TO DAY 02";
            nextDayButtonText.fontStyle = FontStyles.Bold;
            nextDayButtonText.characterSpacing = 0.7f;
        }

        private static GameObject CreateMetricCard(Transform parent, string name, string label, out TMP_Text valueText)
        {
            GameObject card = CreatePanel(parent, name, HeaderBackground, false);
            AddOutline(card, BorderColor, new Vector2(2f, -2f));

            TMP_Text labelText = CreateText(card.transform, "Label", 14f, MutedText, TextAlignmentOptions.TopLeft);
            SetTopRect(labelText.rectTransform, 18f, 18f, 14f, 22f);
            labelText.text = label;
            labelText.characterSpacing = 0.8f;

            valueText = CreateText(card.transform, "Value", 29f, Color.white, TextAlignmentOptions.BottomLeft);
            SetRect(valueText.rectTransform, Vector2.zero, Vector2.one, new Vector2(18f, 12f), new Vector2(-18f, -38f));
            valueText.text = "$0.00";
            valueText.fontStyle = FontStyles.Bold;
            valueText.enableAutoSizing = true;
            valueText.fontSizeMin = 20f;
            valueText.fontSizeMax = 29f;
            return card;
        }

        private static RectTransform CreateVitalRow(Transform parent, string name, string label, Color fillColor, float top, out TMP_Text valueText)
        {
            TMP_Text labelText = CreateText(parent, $"{name}Label", 16f, BodyText, TextAlignmentOptions.MidlineLeft);
            SetTopRect(labelText.rectTransform, 18f, 665f, top, 32f);
            labelText.text = label;
            labelText.fontStyle = FontStyles.Bold;

            GameObject track = CreatePanel(parent, $"{name}Track", DeepBackground, false);
            SetTopRect(track.GetComponent<RectTransform>(), 105f, 118f, top + 5f, 22f);
            AddOutline(track, new Color32(38, 52, 72, 255), new Vector2(2f, -2f));

            GameObject fillArea = new GameObject($"{name}FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(track.transform, false);
            Stretch(fillArea.GetComponent<RectTransform>(), new Vector2(3f, 3f), new Vector2(-3f, -3f));

            GameObject fill = CreatePanel(fillArea.transform, $"{name}Fill", fillColor, false);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            valueText = CreateText(parent, $"{name}Value", 16f, BodyText, TextAlignmentOptions.MidlineRight);
            SetTopRect(valueText.rectTransform, 650f, 18f, top, 32f);
            valueText.text = "100/100";
            return fillRect;
        }

        private static TraderEmotion GetSettlementEmotion(float dailyReturn, float dailyPnl)
        {
            if (dailyReturn >= 20f) return TraderEmotion.Euphoria;
            if (dailyReturn >= 5f) return TraderEmotion.Confident;
            if (dailyPnl > 0.005f) return TraderEmotion.Pleased;
            if (dailyReturn <= -20f) return TraderEmotion.Tearful;
            if (dailyReturn <= -5f) return TraderEmotion.Despairing;
            if (dailyPnl < -0.005f) return TraderEmotion.Anxious;
            return TraderEmotion.Relieved;
        }

        private static string GetEmotionLabel(TraderEmotion emotion)
        {
            return emotion switch
            {
                TraderEmotion.Euphoria => "EUPHORIA",
                TraderEmotion.Confident => "CONFIDENT",
                TraderEmotion.Pleased => "PLEASED",
                TraderEmotion.Tearful => "TEARFUL",
                TraderEmotion.Despairing => "DESPAIRING",
                TraderEmotion.Anxious => "ANXIOUS",
                TraderEmotion.Relieved => "RELIEVED",
                _ => emotion.ToString().ToUpperInvariant()
            };
        }

        private static string GetImmediateReaction(float dailyPnl)
        {
            if (dailyPnl > 0.005f)
                return "“오빠, 오늘 기록 정리 중이야... 우리 목표에 조금 더 가까워졌지? ♥”";
            if (dailyPnl < -0.005f)
                return "“오빠... 오늘 손실 기록을 봐도 요미 버리면 안 돼. 내일은 꼭 되찾을게...”";
            return "“오늘은 간신히 본전이네... 내일은 요미가 확실한 수익을 보여줄게.”";
        }

        private static string FormatSignedCurrency(float value)
        {
            return value >= 0f ? $"+${value:N2}" : $"-${Mathf.Abs(value):N2}";
        }

        private static string FormatSignedPercent(float value)
        {
            return value >= 0f ? $"+{value:N2}%" : $"-{Mathf.Abs(value):N2}%";
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

            return text;
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

    /// <summary>GameScene의 메인 Canvas에 일일 정산 UI를 자동 설치합니다.</summary>
    public static class DailySettlementUIBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneCallback()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "GameScene")
            {
                return;
            }

            Install(scene);
        }

        private static void Install(Scene gameScene)
        {
            Canvas fallback = null;
            Canvas target = null;

            foreach (GameObject root in gameScene.GetRootGameObjects())
            {
                Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true);
                foreach (Canvas canvas in canvases)
                {
                    if (!canvas.isRootCanvas)
                    {
                        continue;
                    }

                    if (canvas.name == "TradingViewCanvas")
                    {
                        target = canvas;
                        break;
                    }

                    if (fallback == null || canvas.sortingOrder > fallback.sortingOrder)
                    {
                        fallback = canvas;
                    }
                }

                if (target != null)
                {
                    break;
                }
            }

            target ??= fallback;
            if (target == null || target.GetComponent<DailySettlementUIController>() != null)
            {
                return;
            }

            target.gameObject.AddComponent<DailySettlementUIController>();
            Debug.Log("[DailySettlementUI] GameScene에 일일 정산 UI 설치 완료");
        }
    }
}
