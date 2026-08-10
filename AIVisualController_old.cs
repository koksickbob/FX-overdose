using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FXOverdose.Trading;

namespace FXOverdose.AI
{
    public enum DialoguePriority
    {
        Low = 0,     // ?⑥닚 李⑦듃 愿留??〓낫/?쇰컲 以묎퀎 -> 諛붿걯硫??ㅽ궢(Drop)
        Normal = 1,  // ?ъ???吏꾩엯/醫낅즺, ?꾩씠??蹂듭슜, 硫섑깉 蹂??-> ???湲????쒖감 異쒕젰
        High = 2,    // ?ㅽ궗 ?낃렇?덉씠?? 以묒슂 ?곹깭 ??-> ?쇰컲 ???媛濡쒖콈湲?Preempt)
        Critical = 3 // 媛뺤젣 泥?궛, ?ㅻ쾭?꾩쫰 ??＜, ?뚮컻 湲곕?, 洹밸떒??李⑦듃 蹂??-> High ?ы븿 紐⑤뱺 吏꾪뻾 以????利됱떆 媛濡쒖콈湲?    }

    public enum EventCategory
    {
        General = 0,
        ChartMovement = 1,
        MentalChange = 2,
        HealthChange = 3,
        GimmickTriggered = 4,
        ItemUsed = 5,
        PositionOpened = 6,
        PositionClosed = 7,
        SkillUpgraded = 8,
        DailySettlement = 9,
        Tutorial = 10
    }

    public class AIVisualController : MonoBehaviour
    {
        private const float DialogueContentPadding = 52f;
        private const float DialogueTailCenterOffset = 6f;
        private const float CharacterDialogueVerticalOffset = 36f;
        private const float DialogueBalloonAdditionalVerticalOffset = 48f;
        private const float DialogueUIScale = 1.15f;
        private const float CostumeOpticalScale = 0.95f;
        private const float CostumeOpticalFootCompensation = -0.020f;

        public enum ExpressionState
        {
            Delighted = 0, // ?諛??섏씡 (ROE > +20%)
            Confident = 1, // ?쇰컲 ?섏씡 (ROE 0% ~ +20%)
            Anxious   = 2, // ?먯떎 吏꾪뻾 (ROE -20% ~ 0%)
            Desperate = 3, // 洹뱀떖??臾쇰┝ (ROE < -20% ?먮뒗 Danger)
            Overdose  = 4  // ??＜ ?곹깭 (Overdose)
        }

        private struct DialogueRequest
        {
            public string Text;
            public DialoguePriority Priority;
            public EventCategory Category;
            public float RequestTime;
        }

        [Header("?쒖뒪???곌껐")]
        [SerializeField] private TraderStatus traderStatus;
        [SerializeField] private TradingController tradingController;
        [SerializeField] private AITradingBrain aiBrain;
        [SerializeField] private Inventory inventory;

        [Header("鍮꾩＜??諛??좊땲硫붿씠??)]
        [SerializeField] private Animator characterAnimator;
        [SerializeField] private Image characterImage;
        [SerializeField] private GameObject dangerAuraEffect; // Danger/Overdose ??寃쎄퀬 ?ㅻ씪
        [SerializeField, Min(0f)] private float contextualEmotionDuration = 4f;

        [Header("留먰뭾??UI (Typewriter Effect)")]
        [SerializeField] private GameObject dialogueBalloonPanel;
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private TMP_FontAsset dialogueFont;
        [SerializeField, Min(8f)] private float dialogueFontSizeMin = 19f;
        [SerializeField, Min(8f)] private float dialogueFontSizeMax = 27f;
        [SerializeField] private float typewriterCharDelay = 0.02f;
#pragma warning disable 0414
        [SerializeField] private float balloonDisplayDuration = 4.0f; // 湲곗〈 6.0珥덉뿉??鍮좊Ⅸ 8遺??멸쾶???띾룄??留욎떠 4.0珥덈줈 ?⑥텞
#pragma warning restore 0414
        public float BalloonDisplayDuration => balloonDisplayDuration;
        
        public bool SuppressNormalDialogues { get; set; } = false;

        [Header("?꾩옱 ?곹깭 (?쎄린 ?꾩슜)")]
        [SerializeField] private TraderEmotion currentEmotion = TraderEmotion.Focused;
        private readonly Dictionary<TraderEmotion, Sprite> emotionSprites = new Dictionary<TraderEmotion, Sprite>();
        private readonly Dictionary<string, Sprite> itemUseSprites = new Dictionary<string, Sprite>();
        private readonly Dictionary<SkillType, Sprite> skillUpgradeSprites = new Dictionary<SkillType, Sprite>();
        private readonly Dictionary<TradingController.PositionType, Sprite> positionSprites = new Dictionary<TradingController.PositionType, Sprite>();
        private readonly Dictionary<Sprite, float> costumeSpriteScales = new Dictionary<Sprite, float>();
        private readonly Dictionary<Sprite, Vector2> costumeSpriteOffsets = new Dictionary<Sprite, Vector2>();
        private Sprite overdoseStateSprite;
        private float emotionOverrideUntil;
        private bool isItemUseVisualActive;
        private float itemUseVisualUntil;
        private bool isSkillUpgradeVisualActive;
        private bool isPositionVisualActive;
        private float positionVisualUntil;
        private bool isOverdoseStateVisualActive;
        private string currentItemUseId;
        private SkillType currentSkillUpgradeType;
        private TradingController.PositionType currentPositionVisualType;
        private Coroutine typewriterCoroutine;
        private Coroutine hideBalloonCoroutine;
        private Coroutine tutorialAdvanceIndicatorCoroutine;
        private RectTransform tutorialAdvanceIndicator;
        private CanvasGroup tutorialAdvanceIndicatorGroup;
        private DialoguePriority currentDisplayPriority = DialoguePriority.Normal;
        private EventCategory currentDisplayCategory = EventCategory.General;
        private bool visualLayoutOffsetApplied;
        private bool dialogueVisualScaleApplied;
        private bool characterBaseScaleCaptured;
        private Vector3 characterBaseScale = Vector3.one;
        private Vector2 characterBaseAnchoredPosition;

        // ?곗꽑?쒖쐞 ??諛?荑⑦???Lock 愿由??쒖뼱遺
        private Queue<DialogueRequest> dialogueQueue = new Queue<DialogueRequest>();
        private bool isBalloonLocked = false;
        public bool IsBalloonActive => isBalloonLocked;
        private float balloonUnlockTime = 0f;
        private Dictionary<EventCategory, float> lastCategoryOutputTimes = new Dictionary<EventCategory, float>();

        private void Start()
        {
            if (traderStatus == null) traderStatus = FindAnyObjectByType<TraderStatus>();
            if (tradingController == null) tradingController = FindAnyObjectByType<TradingController>();
            if (aiBrain == null) aiBrain = FindAnyObjectByType<AITradingBrain>();
            if (inventory == null) inventory = FindAnyObjectByType<Inventory>(FindObjectsInactive.Include);

            ResolveCharacterImage();
            ApplyDialogueVisualScale();
            ApplyCharacterDialogueVerticalOffset();
            LoadEmotionSprites();
            LoadItemUseSprites();
            LoadSkillUpgradeSprites();
            LoadPositionSprites();
            overdoseStateSprite = LoadCostumeSprite("States", "Overdose");
            ApplyEmotion(currentEmotion, true);
            ApplyCostumeVisualScale();

            if (CostumeManager.Instance != null)
            {
                CostumeManager.Instance.OnCostumesChanged -= HandleCostumeChanged;
                CostumeManager.Instance.OnCostumesChanged += HandleCostumeChanged;
            }

            ApplyDialogueTextStyle();
            EnsureTutorialAdvanceIndicator();
            SetTutorialAdvanceIndicator(false);

            if (aiBrain != null)
            {
                aiBrain.OnAIDecisionMade += HandleAIDecisionMade;
            }

            if (inventory != null)
            {
                inventory.ItemConsumed -= HandleItemConsumed;
                inventory.ItemConsumed += HandleItemConsumed;
            }

            if (dialogueBalloonPanel != null) dialogueBalloonPanel.SetActive(false);
            if (dangerAuraEffect != null) dangerAuraEffect.SetActive(false);
        }

        // AI媛 ????щ? 異쒕젰???뚮룄 留먰뭾???덉뿉???숈씪???고듃? ?ш린瑜??좎??쒕떎.
        private void ApplyDialogueVisualScale()
        {
            if (dialogueVisualScaleApplied) return;

            RectTransform balloonRect = dialogueBalloonPanel != null
                ? dialogueBalloonPanel.GetComponent<RectTransform>()
                : null;
            if (balloonRect == null || dialogueText == null) return;

            // ?ъ뿉 ?⑥븘 ?덈뒗 援щ쾭???고듃 媛믪쓣 癒쇱? ?꾩옱 湲곗??쇰줈 蹂댁젙????15% ?뺣??⑸땲??
            if (dialogueFontSizeMin <= 14f) dialogueFontSizeMin = 19f;
            if (dialogueFontSizeMax <= 22f) dialogueFontSizeMax = 27f;
            dialogueFontSizeMin *= DialogueUIScale;
            dialogueFontSizeMax *= DialogueUIScale;

            Canvas.ForceUpdateCanvases();
            balloonRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, balloonRect.rect.width * DialogueUIScale);
            balloonRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, balloonRect.rect.height * DialogueUIScale);
            dialogueVisualScaleApplied = true;
        }

        private void ApplyDialogueTextStyle()
        {
            if (dialogueText == null) return;

            // ???먮뒗 湲곗〈 ?꾨━?뱀뿉 14 / 22 援щ쾭??媛믪씠 ?⑥븘?덈뒗 寃쎌슦 5?ъ씤???щ┛ ?ш린(19 / 27)濡??먮룞 蹂댁젙
            if (dialogueFontSizeMin <= 14f) dialogueFontSizeMin = 19f;
            if (dialogueFontSizeMax <= 22f) dialogueFontSizeMax = 27f;

            TMP_FontAsset targetFont = dialogueFont != null
                ? dialogueFont
                : TMP_Settings.defaultFontAsset;

            if (targetFont != null && targetFont.atlasTextures != null && targetFont.atlasTextures.Length > 0 && targetFont.atlasTextures[0] != null)
            {
                dialogueText.font = targetFont;
                if (targetFont.material != null)
                {
                    dialogueText.fontSharedMaterial = targetFont.material;
                }
            }

            // 留먰뭾?좎쓽 ?ш린??留욊쾶 ?좊룞?곸쑝濡?湲???ш린媛 諛붾뚮룄濡?AutoSizing ?쒖꽦??諛?理쒖냼/理쒕? ?ㅼ젙
            dialogueText.enableAutoSizing = true;
            dialogueText.fontSizeMin = dialogueFontSizeMin;
            dialogueText.fontSizeMax = Mathf.Max(dialogueFontSizeMin, dialogueFontSizeMax);
            dialogueText.fontStyle = FontStyles.Normal;
            dialogueText.color = new Color32(207, 250, 254, 255); // UI 媛?대뱶 AI ??ъ깋 #CFFAFE
            dialogueText.alignment = TextAlignmentOptions.Center;
            dialogueText.textWrappingMode = TextWrappingModes.Normal;
            dialogueText.overflowMode = TextOverflowModes.Ellipsis;

            // ?ㅽ봽?쇱씠?몄쓽 ?щ챸 諛붽묑 ?곸뿭怨??쇱そ 瑗щ━瑜??쇳빐 ?ㅼ젣 ?꾨젅???덉そ???띿뒪?몃? 諛곗튂?⑸땲??
            RectTransform textRect = dialogueText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(
                DialogueContentPadding + DialogueTailCenterOffset,
                DialogueContentPadding);
            textRect.offsetMax = new Vector2(
                -(DialogueContentPadding - DialogueTailCenterOffset),
                -DialogueContentPadding);

            // 蹂댁씠???꾨젅??湲곗? ?щ갚??RectTransform?먯꽌 泥섎━?섎?濡?TMP ?대? ?щ갚? 以묐났 ?곸슜?섏? ?딆뒿?덈떎.
            dialogueText.margin = Vector4.zero;
        }

        private void OnDestroy()
        {
            if (aiBrain != null) aiBrain.OnAIDecisionMade -= HandleAIDecisionMade;
            if (CostumeManager.Instance != null)
                CostumeManager.Instance.OnCostumesChanged -= HandleCostumeChanged;
            if (inventory != null)
            {
                inventory.ItemConsumed -= HandleItemConsumed;
            }
        }

        private void Update()
        {
            UpdateExpressionState();
        }

        // ?ㅼ떆媛??섏씡瑜?諛?硫섑깉 ?곹깭瑜?湲곕컲?쇰줈 媛먯젙???꾩텧?섍퀬 ?쒖젙 ?곹깭濡?留ㅽ븨
        private void UpdateExpressionState()
        {
            if (isSkillUpgradeVisualActive) return;

            if (isPositionVisualActive)
            {
                if (Time.unscaledTime < positionVisualUntil) return;
                isPositionVisualActive = false;
                ApplyEmotion(currentEmotion, true);
            }

            // ?꾩씠???ъ슜 ?ъ쫰??寃뚯엫 ?쇱떆?뺤? ?щ?? 愿怨꾩뾾???ㅼ젣 ?쒓컙 湲곗? ??1珥덇컙 理쒖슦???쒖떆?⑸땲??
            if (isItemUseVisualActive)
            {
                if (Time.unscaledTime < itemUseVisualUntil) return;

                isItemUseVisualActive = false;
                ApplyEmotion(currentEmotion, true);
            }

            if (traderStatus == null) return;

            if (traderStatus.CurrentMentalState == TraderStatus.MentalState.Overdose)
            {
                ApplyOverdoseStateVisual();
                return;
            }

            if (isOverdoseStateVisualActive)
            {
                isOverdoseStateVisualActive = false;
                // ?꾩슜 ?ㅻ쾭?꾩쫰 ?ㅽ봽?쇱씠?몄뿉???뚮났??利됱떆 ?꾩옱 媛먯젙 ?ㅽ봽?쇱씠?몃? ?ㅼ떆 ?곸슜?⑸땲??
                ApplyEmotion(currentEmotion, true);
            }

            if (Time.unscaledTime < emotionOverrideUntil) return;

            float roe = CalculateCurrentRoe();

            TraderEmotion evaluatedEmotion = TraderEmotionEvaluator.Evaluate(
                roe, 
                traderStatus.CurrentMentalState, 
                traderStatus.HealthRatio, 
                EventCategory.General, 
                ""
            );

            ApplyEmotion(evaluatedEmotion);
        }

        private float CalculateCurrentRoe()
        {
            if (tradingController == null ||
                tradingController.CurrentPosition == TradingController.PositionType.None ||
                tradingController.MarginAmount <= 0f)
            {
                return 0f;
            }

            float currentPrice = FindAnyObjectByType<MarketSimulationEngine>()?.CurrentPrice ?? tradingController.EntryPrice;
            float priceDiff = tradingController.CurrentPosition == TradingController.PositionType.Long
                ? currentPrice - tradingController.EntryPrice
                : tradingController.EntryPrice - currentPrice;
            float pnl = priceDiff * (tradingController.MarginAmount * tradingController.CurrentLeverage / tradingController.EntryPrice);
            return pnl / tradingController.MarginAmount * 100f;
        }

        private void ResolveCharacterImage()
        {
            if (characterImage == null)
            {
                GameObject characterObject = GameObject.Find("ProtagonistCharacterImage");
                if (characterObject != null)
                    characterImage = characterObject.GetComponent<Image>();
            }
            if (characterImage != null && !characterBaseScaleCaptured)
            {
                characterBaseScale = characterImage.rectTransform.localScale;
                characterBaseAnchoredPosition = characterImage.rectTransform.anchoredPosition;
                characterBaseScaleCaptured = true;
            }
        }

        private void ApplyCostumeVisualScale()
        {
            ResolveCharacterImage();
            if (characterImage == null || !characterBaseScaleCaptured) return;
            float scale = characterImage.sprite != null && costumeSpriteScales.TryGetValue(characterImage.sprite, out float measuredScale)
                ? measuredScale
                : 1f;
            RectTransform rect = characterImage.rectTransform;
            Vector2 normalizedOffset = characterImage.sprite != null && costumeSpriteOffsets.TryGetValue(characterImage.sprite, out Vector2 measuredOffset)
                ? measuredOffset
                : Vector2.zero;
            bool usesMeasuredCostumeAdjustment = characterImage.sprite != null && costumeSpriteScales.ContainsKey(characterImage.sprite) &&
                !Mathf.Approximately(scale, 1f);
            if (usesMeasuredCostumeAdjustment)
            {
                scale *= CostumeOpticalScale;
                // 以묒븰 ?쇰쿁??湲곗??쇰줈 異뺤냼????諛쒕걹???꾨줈 ?ㅻ━吏 ?딅룄濡?600x1180 ?ㅻ（?ｌ쓽 ?됯퇏 諛쒕걹 嫄곕━瑜?蹂댁긽?⑸땲??
                normalizedOffset.y += CostumeOpticalFootCompensation;
            }
            rect.localScale = characterBaseScale * scale;
            rect.anchoredPosition = characterBaseAnchoredPosition + new Vector2(
                normalizedOffset.x * rect.rect.width,
                normalizedOffset.y * rect.rect.height);
        }

        private void SetCharacterSprite(Sprite sprite)
        {
            ResolveCharacterImage();
            if (characterImage == null || sprite == null) return;
            characterImage.sprite = sprite;
            characterImage.preserveAspect = true;
            ApplyCostumeVisualScale();
        }

        private void ApplyCharacterDialogueVerticalOffset()
        {
            if (visualLayoutOffsetApplied) return;

            RectTransform characterRect = characterImage != null ? characterImage.rectTransform : null;
            RectTransform balloonRect = dialogueBalloonPanel != null
                ? dialogueBalloonPanel.GetComponent<RectTransform>()
                : null;

            if (characterRect == null || balloonRect == null) return;

            characterRect.anchoredPosition += Vector2.up * CharacterDialogueVerticalOffset;
            characterBaseAnchoredPosition = characterRect.anchoredPosition;
            // ?곗륫 ?섎떒???몃줈 諛곗튂???ㅽ궗 踰꾪듉 ?곷떒怨?留먰뭾???섎떒??寃뱀튂吏 ?딅룄濡?
            // 罹먮┃???대룞?됱? ?좎??섍퀬 留먰뭾?좊쭔 議곌툑 ???꾨줈 ?щ┰?덈떎.
            balloonRect.anchoredPosition += Vector2.up *
                (CharacterDialogueVerticalOffset + DialogueBalloonAdditionalVerticalOffset);
            visualLayoutOffsetApplied = true;
        }

        private void LoadEmotionSprites()
        {
            emotionSprites.Clear();
            foreach (TraderEmotion emotion in Enum.GetValues(typeof(TraderEmotion)))
            {
                Sprite sprite = LoadCostumeSprite("Emotions", emotion.ToString());
                if (sprite != null)
                    emotionSprites[emotion] = sprite;
                else
                    Debug.LogWarning($"[AIVisualController] 媛먯젙 ?ㅽ봽?쇱씠?몃? 李얠? 紐삵뻽?듬땲?? {emotion}", this);
            }
        }

        private void LoadItemUseSprites()
        {
            itemUseSprites.Clear();
            LoadItemUseSprite("energy_drink", "EnergyDrink");
            LoadItemUseSprite("dessert", "Dessert");
            LoadItemUseSprite("supplement", "Supplement");
            LoadItemUseSprite("sedative", "Sedative");
            LoadItemUseSprite("malatang", "Malatang");
            LoadItemUseSprite("pasta", "Pasta");
            LoadItemUseSprite("steak", "Steak");
            LoadItemUseSprite("sushi", "Sushi");
            LoadItemUseSprite("tteokbokki", "Tteokbokki");
        }

        private void LoadSkillUpgradeSprites()
        {
            skillUpgradeSprites.Clear();
            LoadSkillUpgradeSprite(SkillType.ChartStudy, "ChartStudy");
            LoadSkillUpgradeSprite(SkillType.CubePatience, "CubePatience");
            LoadSkillUpgradeSprite(SkillType.BookJudgment, "BookJudgment");
        }

        private void LoadSkillUpgradeSprite(SkillType type, string resourceName)
        {
            Sprite sprite = LoadCostumeSprite("SkillUpgrade", resourceName);
            if (sprite != null)
                skillUpgradeSprites[type] = sprite;
            else
                Debug.LogWarning($"[AIVisualController] ?ㅽ궗 ?곗텧 ?ㅽ봽?쇱씠?몃? 李얠? 紐삵뻽?듬땲?? {type}", this);
        }

        private void LoadPositionSprites()
        {
            positionSprites.Clear();
            Sprite longSprite = LoadCostumeSprite("Position", "Long");
            Sprite shortSprite = LoadCostumeSprite("Position", "Short");
            if (longSprite != null) positionSprites[TradingController.PositionType.Long] = longSprite;
            if (shortSprite != null) positionSprites[TradingController.PositionType.Short] = shortSprite;
        }

        private void ApplyOverdoseStateVisual()
        {
            if (isOverdoseStateVisualActive) return;
            if (overdoseStateSprite == null)
                overdoseStateSprite = LoadCostumeSprite("States", "Overdose");
            if (overdoseStateSprite == null) return;

            ResolveCharacterImage();
            if (characterImage == null) return;
            isOverdoseStateVisualActive = true;
            currentEmotion = TraderEmotion.Manic;
            SetCharacterSprite(overdoseStateSprite);

            // ?꾩슜 ?대?吏 ?먯껜???ㅼ삤?쇨? ?ы븿?섏뼱 ?덉쑝誘濡?湲곗〈 蹂댁“ ?ㅼ삤?쇱? 以묐났?섏? ?딄쾶 ?⑸땲??
            if (dangerAuraEffect != null && dangerAuraEffect.activeSelf)
                dangerAuraEffect.SetActive(false);

            if (characterAnimator != null)
            {
                characterAnimator.SetInteger("ExpressionState", (int)TraderEmotion.Manic);
                characterAnimator.SetTrigger("OnExpressionChanged");
            }
        }

        private void LoadItemUseSprite(string itemId, string resourceName)
        {
            Sprite sprite = LoadCostumeSprite("ItemUse", resourceName);
            if (sprite != null)
            {
                itemUseSprites[itemId] = sprite;
            }
            else
            {
                Debug.LogWarning($"[AIVisualController] ?꾩씠???ъ슜 ?ㅽ봽?쇱씠?몃? 李얠? 紐삵뻽?듬땲?? {itemId}", this);
            }
        }

        /// <summary>?꾩씠???ъ슜 ?ъ쫰瑜??ㅼ젣 ?쒓컙 湲곗??쇰줈 ?좎떆 ?쒖떆?⑸땲??</summary>
        public void ShowItemUse(string itemId, float duration = 1f)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return;
            if (itemUseSprites.Count == 0) LoadItemUseSprites();

            if (!itemUseSprites.TryGetValue(itemId, out Sprite sprite) || sprite == null)
            {
                Debug.LogWarning($"[AIVisualController] 吏?먰븯吏 ?딅뒗 ?꾩씠???ъ슜 ?곗텧?낅땲?? {itemId}", this);
                return;
            }

            ResolveCharacterImage();
            if (characterImage == null) return;

            isItemUseVisualActive = true;
            currentItemUseId = itemId;
            itemUseVisualUntil = Time.unscaledTime + Mathf.Max(0.1f, duration);
            SetCharacterSprite(sprite);
        }

        private void HandleItemConsumed(ItemData item)
        {
            if (item != null) ShowItemUse(item.ItemId, 1f);
        }

        /// <summary>?쒓컙 ?뚮え???ㅽ궗 ?낃렇?덉씠?쒓? 吏꾪뻾?섎뒗 ?숈븞 ?꾩슜 ?붾? ?ъ쫰瑜??좎??⑸땲??</summary>
        public void BeginSkillUpgradeVisual(SkillType type)
        {
            if (skillUpgradeSprites.Count == 0) LoadSkillUpgradeSprites();
            if (!skillUpgradeSprites.TryGetValue(type, out Sprite sprite) || sprite == null) return;

            ResolveCharacterImage();
            if (characterImage == null) return;

            isSkillUpgradeVisualActive = true;
            currentSkillUpgradeType = type;
            isItemUseVisualActive = false;
            SetCharacterSprite(sprite);
        }

        public void EndSkillUpgradeVisual()
        {
            if (!isSkillUpgradeVisualActive) return;
            isSkillUpgradeVisualActive = false;
            ApplyEmotion(currentEmotion, true);
        }

        /// <summary>LONG/SHORT 吏꾩엯 諛⑺뼢??紐몄쭞?쇰줈 利됱떆 ?몄??쒗궎??吏㏃? ?꾩슜 ?ъ쫰?낅땲??</summary>
        public void ShowPositionOpen(TradingController.PositionType type, float duration = 1f)
        {
            if (type == TradingController.PositionType.None || isSkillUpgradeVisualActive) return;
            if (positionSprites.Count == 0) LoadPositionSprites();
            if (!positionSprites.TryGetValue(type, out Sprite sprite) || sprite == null) return;

            ResolveCharacterImage();
            if (characterImage == null) return;

            isPositionVisualActive = true;
            currentPositionVisualType = type;
            isItemUseVisualActive = false;
            positionVisualUntil = Time.unscaledTime + Mathf.Max(0.2f, duration);
            SetCharacterSprite(sprite);
        }

        /// <summary>?대깽?몃굹 ?곗텧 肄붾뱶?먯꽌 19醫?媛먯젙??吏곸젒 ?쒖떆?????ъ슜?⑸땲??</summary>
        public void ShowEmotion(TraderEmotion emotion, float duration = 4f)
        {
            emotionOverrideUntil = Time.unscaledTime + Mathf.Max(0f, duration);

            // ???媛먯젙? 理쒖떊 ?곹깭濡?湲곕줉?섎릺, 吏꾪뻾 以묒씤 1珥??꾩씠???ъ쫰瑜???뼱?곗????딆뒿?덈떎.
            if (isItemUseVisualActive)
            {
                currentEmotion = emotion;
                return;
            }

            ApplyEmotion(emotion, true);
        }

        private void ApplyEmotion(TraderEmotion emotion, bool force = false)
        {
            if (!force && emotion == currentEmotion) return;
            currentEmotion = emotion;

            ResolveCharacterImage();
            if (characterImage != null && emotionSprites.TryGetValue(emotion, out Sprite sprite))
            {
                SetCharacterSprite(sprite);
            }

            // 湲곗〈 Animator瑜??ъ슜?섎뒗 ?щ룄 0~18 ExpressionState ?뚮씪誘명꽣濡??명솚?⑸땲??
            if (characterAnimator != null)
            {
                characterAnimator.SetInteger("ExpressionState", (int)emotion);
                characterAnimator.SetTrigger("OnExpressionChanged");
            }

            bool showAura = emotion is TraderEmotion.Panicked
                or TraderEmotion.Despairing
                or TraderEmotion.Furious
                or TraderEmotion.Tearful
                or TraderEmotion.Manic
                or TraderEmotion.Obsessive
                or TraderEmotion.Vengeful;
            if (dangerAuraEffect != null && dangerAuraEffect.activeSelf != showAura)
                dangerAuraEffect.SetActive(showAura);
        }

        private Sprite LoadCostumeSprite(string category, string spriteName)
        {
            string costumeId = CostumeManager.Instance != null
                ? CostumeManager.Instance.EquippedCostumeId
                : CostumeManager.StandardId;
            string path = CostumeManager.Instance != null
                ? CostumeManager.Instance.GetResourcePath(category, spriteName)
                : $"Characters/{category}/{spriteName}";
            Sprite sprite = Resources.Load<Sprite>(path);
            bool usedCostumeSprite = sprite != null;
            if (sprite == null && !path.StartsWith("Characters/" + category + "/", StringComparison.Ordinal))
            {
                sprite = Resources.Load<Sprite>($"Characters/{category}/{spriteName}");
                Debug.LogWarning($"[AIVisualController] 肄붿뒪???ㅽ봽?쇱씠?멸? ?놁뼱 湲곕낯?뺤쑝濡??泥댄빀?덈떎: {path}", this);
            }
            if (sprite != null)
            {
                costumeSpriteScales[sprite] = usedCostumeSprite
                    ? GetMeasuredCostumeScale(costumeId, category, spriteName)
                    : 1f;
                costumeSpriteOffsets[sprite] = usedCostumeSprite
                    ? GetMeasuredCostumeOffset(costumeId, category, spriteName)
                    : Vector2.zero;
            }
            return sprite;
        }

        // 600x1180 ?먮낯???뚰뙆 諛붿슫???믪씠瑜??숈씪 ?ъ쫰 湲곕낯 ?붾?? 留욎텣 ?ㅼ륫媛믪엯?덈떎.
        private static float GetMeasuredCostumeScale(string costumeId, string category, string spriteName)
        {
            string key = $"{category}/{spriteName}";
            if (costumeId == CostumeManager.BikiniId)
            {
                return key switch
                {
                    "SkillUpgrade/ChartStudy" => 1.07383f, "SkillUpgrade/CubePatience" => 1.08422f, "SkillUpgrade/BookJudgment" => 1.06363f,
                    "ItemUse/Supplement" => 1.12676f, "ItemUse/Malatang" => 1.05169f, "ItemUse/Dessert" => 1.04478f,
                    "ItemUse/Steak" => 1.07081f, "ItemUse/Tteokbokki" => 1.06775f, "ItemUse/Sedative" => 1.05660f,
                    "ItemUse/Sushi" => 1.09171f, "ItemUse/EnergyDrink" => 1.11332f, "ItemUse/Pasta" => 1.05169f,
                    "Position/Short" => 1.06061f, "Position/Long" => 1.05066f,
                    "States/Standard" => 1.06066f, "States/Overdose" => 1.07383f,
                    "Emotions/Focused" => 1.06464f, "Emotions/Vengeful" => 1.08108f, "Emotions/Confident" => 1.05561f,
                    "Emotions/Panicked" => 1.12676f, "Emotions/Affectionate" => 1.08641f, "Emotions/Jealous" => 1.01174f,
                    "Emotions/Tearful" => 1.00539f, "Emotions/Anxious" => 1.12790f, "Emotions/Relieved" => 1.06768f,
                    "Emotions/Manic" => 1.10891f, "Emotions/Exhausted" => 1.13017f, "Emotions/Despairing" => 1.15583f,
                    "Emotions/Furious" => 1.06061f, "Emotions/Obsessive" => 1.12450f, "Emotions/Regretful" => 1.13360f,
                    "Emotions/Pleased" => 1.06061f, "Emotions/Euphoria" => 1.09162f, "Emotions/Suspicious" => 1.05561f,
                    "Emotions/Frustrated" => 1.09268f,
                    _ => 1f
                };
            }

            if (costumeId == CostumeManager.HanbokId)
            {
                return key switch
                {
                    "SkillUpgrade/ChartStudy" => 1.08350f, "SkillUpgrade/CubePatience" => 1.12513f, "SkillUpgrade/BookJudgment" => 1.09421f,
                    "ItemUse/Supplement" => 1.12286f, "ItemUse/Malatang" => 1.16079f, "ItemUse/Dessert" => 1.15083f,
                    "ItemUse/Steak" => 1.13604f, "ItemUse/Tteokbokki" => 1.16684f, "ItemUse/Sedative" => 1.15067f,
                    "ItemUse/Sushi" => 1.07700f, "ItemUse/EnergyDrink" => 1.15904f, "ItemUse/Pasta" => 1.17050f,
                    "Position/Short" => 1.16008f, "Position/Long" => 1.10934f,
                    "States/Standard" => 1.18704f, "States/Overdose" => 1.05094f,
                    "Emotions/Focused" => 1.23614f, "Emotions/Vengeful" => 1.33493f, "Emotions/Confident" => 1.12072f,
                    "Emotions/Panicked" => 1.13892f, "Emotions/Affectionate" => 1.14344f, "Emotions/Jealous" => 1.16146f,
                    "Emotions/Tearful" => 1.10626f, "Emotions/Anxious" => 1.24138f, "Emotions/Relieved" => 1.19379f,
                    "Emotions/Manic" => 1.06903f, "Emotions/Exhausted" => 1.09961f, "Emotions/Despairing" => 1.10069f,
                    "Emotions/Furious" => 1.07625f, "Emotions/Obsessive" => 1.07729f, "Emotions/Regretful" => 1.12072f,
                    "Emotions/Pleased" => 1.23725f, "Emotions/Euphoria" => 1.17884f, "Emotions/Suspicious" => 1.14008f,
                    "Emotions/Frustrated" => 1.15440f,
                    _ => 1f
                };
            }

            if (costumeId == CostumeManager.YukataId)
            {
                return key switch
                {
                    "SkillUpgrade/ChartStudy" => 1.23043f, "SkillUpgrade/CubePatience" => 1.05288f, "SkillUpgrade/BookJudgment" => 1.10287f,
                    "ItemUse/Supplement" => 1.16510f, "ItemUse/Malatang" => 1.06877f, "ItemUse/Dessert" => 1.15321f,
                    "ItemUse/Steak" => 1.10682f, "ItemUse/Tteokbokki" => 1.05268f, "ItemUse/Sedative" => 1.12399f,
                    "ItemUse/Sushi" => 1.16199f, "ItemUse/EnergyDrink" => 1.09421f, "ItemUse/Pasta" => 1.08852f,
                    "Position/Short" => 1.06084f, "Position/Long" => 1.09091f,
                    "States/Standard" => 1.07095f, "States/Overdose" => 1.01921f,
                    "Emotions/Focused" => 1.37654f, "Emotions/Vengeful" => 1.06999f, "Emotions/Confident" => 1.08577f,
                    "Emotions/Panicked" => 1.11389f, "Emotions/Affectionate" => 1.07205f, "Emotions/Jealous" => 1.12286f,
                    "Emotions/Tearful" => 1.14374f, "Emotions/Anxious" => 1.09091f, "Emotions/Relieved" => 1.09852f,
                    "Emotions/Manic" => 1.06190f, "Emotions/Exhausted" => 1.10835f, "Emotions/Despairing" => 1.06801f,
                    "Emotions/Furious" => 1.10615f, "Emotions/Obsessive" => 1.08147f, "Emotions/Regretful" => 1.09862f,
                    "Emotions/Pleased" => 1.12274f, "Emotions/Euphoria" => 1.08471f, "Emotions/Suspicious" => 1.05288f,
                    "Emotions/Frustrated" => 1.06910f,
                    _ => 1f
                };
            }

            return 1f;
        }

        // X???ㅻ（??以묒떖, Y??諛쒕걹??湲곕낯 ?붾?? 留욎텣 600x1180 ?뺢퇋??醫뚰몴?낅땲??
        private static Vector2 GetMeasuredCostumeOffset(string costumeId, string category, string spriteName)
        {
            string key = $"{category}/{spriteName}";
            if (costumeId == CostumeManager.BikiniId)
            {
                return key switch
                {
                    "SkillUpgrade/ChartStudy" => new(-0.030425f, -0.018655f), "SkillUpgrade/CubePatience" => new(-0.027106f, -0.028943f), "SkillUpgrade/BookJudgment" => new(-0.020333f, -0.015774f),
                    "ItemUse/Supplement" => new(-0.012207f, -0.049654f), "ItemUse/Malatang" => new(-0.021867f, -0.012901f), "ItemUse/Dessert" => new(-0.035697f, -0.006198f),
                    "ItemUse/Steak" => new(-0.033909f, -0.021749f), "ItemUse/Tteokbokki" => new(-0.018629f, -0.019426f), "ItemUse/Sedative" => new(-0.031651f, -0.017013f),
                    "ItemUse/Sushi" => new(-0.009021f, -0.036043f), "ItemUse/EnergyDrink" => new(-0.000928f, -0.044344f), "ItemUse/Pasta" => new(0.007888f, -0.017358f),
                    "Position/Short" => new(0.013308f, -0.022470f), "Position/Long" => new(-0.025349f, -0.008013f),
                    "States/Standard" => new(-0.031820f, -0.017053f), "States/Overdose" => new(-0.034899f, -0.040496f),
                    "Emotions/Focused" => new(-0.019518f, -0.027067f), "Emotions/Vengeful" => new(-0.045045f, -0.033898f), "Emotions/Confident" => new(-0.028103f, -0.015655f),
                    "Emotions/Panicked" => new(0.011268f, -0.047744f), "Emotions/Affectionate" => new(-0.018940f, -0.022520f), "Emotions/Jealous" => new(-0.031195f, 0.007288f),
                    "Emotions/Tearful" => new(0.000838f, 0.000000f), "Emotions/Anxious" => new(-0.012112f, -0.052093f), "Emotions/Relieved" => new(-0.030195f, -0.014025f),
                    "Emotions/Manic" => new(-0.032252f, -0.036650f), "Emotions/Exhausted" => new(-0.034847f, -0.036874f), "Emotions/Despairing" => new(-0.035508f, -0.030855f),
                    "Emotions/Furious" => new(-0.014091f, -0.023369f), "Emotions/Obsessive" => new(0.000937f, -0.049554f), "Emotions/Regretful" => new(-0.004612f, -0.054759f),
                    "Emotions/Pleased" => new(-0.032652f, -0.017078f), "Emotions/Euphoria" => new(0.001819f, -0.032378f), "Emotions/Suspicious" => new(-0.005232f, -0.021023f),
                    "Emotions/Frustrated" => new(-0.016313f, -0.008797f),
                    _ => Vector2.zero
                };
            }

            if (costumeId == CostumeManager.HanbokId)
            {
                return key switch
                {
                    "SkillUpgrade/ChartStudy" => new(-0.013474f, -0.021190f), "SkillUpgrade/CubePatience" => new(-0.020419f, -0.032472f), "SkillUpgrade/BookJudgment" => new(0.022041f, -0.001974f),
                    "ItemUse/Supplement" => new(-0.028072f, -0.014430f), "ItemUse/Malatang" => new(0.016712f, -0.018267f), "ItemUse/Dessert" => new(-0.025768f, -0.035238f),
                    "ItemUse/Steak" => new(-0.004620f, -0.029903f), "ItemUse/Tteokbokki" => new(-0.008473f, -0.027758f), "ItemUse/Sedative" => new(-0.011256f, -0.038222f),
                    "ItemUse/Sushi" => new(-0.003462f, -0.007334f), "ItemUse/EnergyDrink" => new(-0.018086f, -0.019221f), "ItemUse/Pasta" => new(0.006970f, -0.021399f),
                    "Position/Short" => new(-0.007600f, -0.033562f), "Position/Long" => new(0.012109f, -0.026416f),
                    "States/Standard" => new(-0.034622f, -0.008285f), "States/Overdose" => new(-0.021937f, -0.025871f),
                    "Emotions/Focused" => new(-0.007014f, -0.033299f), "Emotions/Vengeful" => new(0.002225f, -0.028566f), "Emotions/Confident" => new(-0.030619f, -0.029545f),
                    "Emotions/Panicked" => new(-0.001782f, -0.032875f), "Emotions/Affectionate" => new(0.020369f, -0.010902f), "Emotions/Jealous" => new(0.020460f, -0.044853f),
                    "Emotions/Tearful" => new(-0.007198f, -0.035246f), "Emotions/Anxious" => new(0.015718f, -0.002835f), "Emotions/Relieved" => new(-0.029522f, -0.035997f),
                    "Emotions/Manic" => new(-0.006954f, -0.016395f), "Emotions/Exhausted" => new(-0.019993f, -0.003388f), "Emotions/Despairing" => new(-0.012590f, -0.014035f),
                    "Emotions/Furious" => new(-0.037542f, -0.014234f), "Emotions/Obsessive" => new(-0.001795f, -0.021031f), "Emotions/Regretful" => new(-0.038090f, -0.032394f),
                    "Emotions/Pleased" => new(0.008841f, -0.047384f), "Emotions/Euphoria" => new(0.001816f, -0.019632f), "Emotions/Suspicious" => new(0.014601f, -0.019985f),
                    "Emotions/Frustrated" => new(-0.000705f, -0.029969f),
                    _ => Vector2.zero
                };
            }

            if (costumeId == CostumeManager.YukataId)
            {
                return key switch
                {
                    "SkillUpgrade/ChartStudy" => new(-0.000833f, -0.040341f), "SkillUpgrade/CubePatience" => new(0.010617f, -0.012514f), "SkillUpgrade/BookJudgment" => new(0.008443f, -0.049666f),
                    "ItemUse/Supplement" => new(-0.044662f, -0.048591f), "ItemUse/Malatang" => new(-0.032839f, -0.022673f), "ItemUse/Dessert" => new(-0.006599f, -0.030426f),
                    "ItemUse/Steak" => new(-0.031271f, -0.023495f), "ItemUse/Tteokbokki" => new(-0.011316f, -0.023217f), "ItemUse/Sedative" => new(-0.011033f, -0.042545f),
                    "ItemUse/Sushi" => new(-0.014255f, -0.024687f), "ItemUse/EnergyDrink" => new(-0.005314f, -0.022295f), "ItemUse/Pasta" => new(-0.007183f, -0.017103f),
                    "Position/Short" => new(-0.014978f, -0.018032f), "Position/Long" => new(-0.011742f, -0.034746f),
                    "States/Standard" => new(0.003570f, -0.016427f), "States/Overdose" => new(-0.014455f, -0.018587f),
                    "Emotions/Focused" => new(-0.019187f, -0.020893f), "Emotions/Vengeful" => new(-0.007133f, -0.001419f), "Emotions/Confident" => new(-0.029716f, -0.017555f),
                    "Emotions/Panicked" => new(-0.016613f, -0.026479f), "Emotions/Affectionate" => new(0.000180f, -0.020564f), "Emotions/Jealous" => new(-0.014869f, -0.041921f),
                    "Emotions/Tearful" => new(-0.014057f, -0.049555f), "Emotions/Anxious" => new(0.000076f, -0.031048f), "Emotions/Relieved" => new(-0.007159f, -0.027039f),
                    "Emotions/Manic" => new(-0.007810f, -0.024826f), "Emotions/Exhausted" => new(-0.016445f, -0.036300f), "Emotions/Despairing" => new(0.011740f, -0.020393f),
                    "Emotions/Furious" => new(0.008473f, -0.039975f), "Emotions/Obsessive" => new(-0.001802f, -0.013782f), "Emotions/Regretful" => new(-0.016315f, -0.027084f),
                    "Emotions/Pleased" => new(-0.024955f, -0.034357f), "Emotions/Euphoria" => new(-0.013630f, -0.038221f), "Emotions/Suspicious" => new(-0.010397f, -0.016128f),
                    "Emotions/Frustrated" => new(-0.023049f, -0.008213f),
                    _ => Vector2.zero
                };
            }

            return Vector2.zero;
        }

        private void HandleCostumeChanged()
        {
            LoadEmotionSprites();
            LoadItemUseSprites();
            LoadSkillUpgradeSprites();
            LoadPositionSprites();
            overdoseStateSprite = LoadCostumeSprite("States", "Overdose");

            ResolveCharacterImage();
            if (characterImage == null) return;
            if (traderStatus != null &&
                traderStatus.CurrentMentalState == TraderStatus.MentalState.Overdose)
            {
                isOverdoseStateVisualActive = false;
                ApplyOverdoseStateVisual();
                return;
            }

            if (isSkillUpgradeVisualActive &&
                skillUpgradeSprites.TryGetValue(currentSkillUpgradeType, out Sprite skillSprite))
            {
                SetCharacterSprite(skillSprite);
            }
            else if (isPositionVisualActive &&
                     positionSprites.TryGetValue(currentPositionVisualType, out Sprite positionSprite))
            {
                SetCharacterSprite(positionSprite);
            }
            else if (isItemUseVisualActive &&
                     itemUseSprites.TryGetValue(currentItemUseId, out Sprite itemSprite))
            {
                SetCharacterSprite(itemSprite);
            }
            else if (emotionSprites.TryGetValue(currentEmotion, out Sprite emotionSprite))
            {
                SetCharacterSprite(emotionSprite);
            }
        }

        private void HandleAIDecisionMade(string dialogue, float emotionDelta)
        {
            var matcher = FXOverdose.AI.Dialogue.YomiDialogueMatcher.Instance;
            if (matcher == null) matcher = UnityEngine.Object.FindAnyObjectByType<FXOverdose.AI.Dialogue.YomiDialogueMatcher>(UnityEngine.FindObjectsInactive.Include);
            
            var tc = tradingController;
            if (tc == null) tc = UnityEngine.Object.FindAnyObjectByType<TradingController>(UnityEngine.FindObjectsInactive.Include);

            if (tc != null && matcher != null)
            {
                float roe = CalculateCurrentRoe();
                string posStr = tc.CurrentPosition.ToString();
                var mentalState = traderStatus != null ? traderStatus.CurrentMentalState.ToString() : TraderStatus.MentalState.Stable.ToString();

                FXOverdose.AI.Dialogue.DirectionTag direction = FXOverdose.AI.Dialogue.DirectionTag.None;
                if (tc.CurrentPosition == TradingController.PositionType.Long)
                    direction = roe >= 0f ? FXOverdose.AI.Dialogue.DirectionTag.Up : FXOverdose.AI.Dialogue.DirectionTag.Down;
                else if (tc.CurrentPosition == TradingController.PositionType.Short)
                    direction = roe >= 0f ? FXOverdose.AI.Dialogue.DirectionTag.Down : FXOverdose.AI.Dialogue.DirectionTag.Up;

                string marketTrend = direction == FXOverdose.AI.Dialogue.DirectionTag.Up
                    ? "Bull"
                    : direction == FXOverdose.AI.Dialogue.DirectionTag.Down ? "Bear" : "Sideways";

                int currentLeverage = tc.CurrentLeverage;
                var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>(UnityEngine.FindObjectsInactive.Include);
                float marginRatio = 0f;
                if (gm != null && gm.CurrentBalance + tc.MarginAmount > 0f)
                {
                    marginRatio = tc.MarginAmount / (tc.MarginAmount + gm.CurrentBalance);
                }

                var levelSystem = FXOverdose.Trading.TraderLevelSystem.Instance;
                int heroLevel = levelSystem != null ? levelSystem.ProtagonistLevel : 1;
                int skillLevel = levelSystem != null ? Mathf.Max(levelSystem.ChartStudyLevel, levelSystem.CubePatienceLevel, levelSystem.BookJudgmentLevel) : 1;

                string matchedDialogue = matcher.GetDialogue(
                    posStr, marketTrend, mentalState, direction, roe >= 0f, currentLeverage, marginRatio, heroLevel, skillLevel);

                if (!string.IsNullOrEmpty(matchedDialogue))
                {
                    DisplayDialogueBalloon(matchedDialogue, DialoguePriority.Normal, EventCategory.General);
                }
            }
        }



        public void DisplayDialogueBalloon(string text)
        {
            DisplayDialogueBalloon(text, DialoguePriority.Normal, EventCategory.General);
        }

        public void DisplayDialogueBalloon(string text, DialoguePriority priority)
        {
            DisplayDialogueBalloon(text, priority, EventCategory.General);
        }

        public void DisplayDialogueBalloon(string text, DialoguePriority priority, EventCategory category)
        {
            if (SuppressNormalDialogues && category != EventCategory.Tutorial)
            {
                return;
            }

            if (string.IsNullOrEmpty(text) || dialogueBalloonPanel == null || dialogueText == null) return;

            // ?? [怨좎냽 ?ㅽ궢 以?????쒗븳] ?쒓컙??鍮좊Ⅴ寃??ㅽ궢 以묒씪 ?뚮뒗 以묒슂(High ?댁긽) ??щ쭔 ?섏슜?섍퀬, ?섎㉧吏??臾댁떆?섏뿬 諛由?諛⑹?
            var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            if (gm != null && gm.IsFastForwardingTime && priority < DialoguePriority.High)
            {
                return;
            }

            // ?? [?ㅽ궗??愿묓겢 ??????뺣━] ?덈줈???ㅽ궗????ш? ?ㅼ뼱?ㅻ㈃ ?먯뿉 諛?ㅼ엳???덉쟾 ?ㅽ궗????щ뒗 鍮꾩?
            if (category == EventCategory.SkillUpgraded)
            {
                var newQueue = new Queue<DialogueRequest>();
                while (dialogueQueue.Count > 0)
                {
                    var req = dialogueQueue.Dequeue();
                    if (req.Category != EventCategory.SkillUpgraded)
                    {
                        newQueue.Enqueue(req);
                    }
                }
                dialogueQueue = newQueue;
            }

            // 1. 移댄뀒怨좊━蹂?湲濡쒕쾶 荑⑦???寃??(High ?댁긽 ?곗꽑?쒖쐞??荑⑦???臾댁떆)
            if (priority < DialoguePriority.High && category != EventCategory.General)
            {
                float cooldown = GetCategoryCooldown(category);
                if (lastCategoryOutputTimes.TryGetValue(category, out float lastTime))
                {
                    if (Time.time - lastTime < cooldown)
                    {
                        // 荑⑦???以묒씠硫??ㅽ궢 (?쇰줈??諛⑹?)
                        return;
                    }
                }
            }

            // 2. ?곗꽑?쒖쐞???곕Ⅸ ??諛?媛濡쒖콈湲?Preempt) 泥섎━
            bool isDisplaying = (isBalloonLocked || (dialogueBalloonPanel != null && dialogueBalloonPanel.activeSelf));
            bool isGameOver = gm != null && gm.CurrentState == GameManager.GameState.GameOver;

            if (priority == DialoguePriority.Critical)
            {
                // Critical? 湲곗〈 ??ш? 臾댁뾿?대뱺 利됱떆 媛濡쒖콈湲?                StartOrPreemptDialogue(text, priority, category);
                return;
            }

            if (priority == DialoguePriority.High)
            {
                if (isDisplaying)
                {
                    if (currentDisplayPriority == DialoguePriority.Critical)
                    {
                        // Critical??異쒕젰 以묒씠硫?High??媛濡쒖콈吏 紐삵븯怨??먯뿉 ?곸옱
                        dialogueQueue.Enqueue(new DialogueRequest { Text = text, Priority = priority, Category = category, RequestTime = Time.time });
                        return;
                    }

                    if (currentDisplayPriority == DialoguePriority.High && !isGameOver)
                    {
                        // ?뮕 [?숈씪 ?대깽???곗냽 諛쒖깮 媛뺤젣 ??뼱?곌린] 
                        if (currentDisplayCategory == category && category == EventCategory.SkillUpgraded)
                        {
                            // 媛숈? ?ㅽ궗 ?낃렇?덉씠?쒕㈃ 利됱떆 ??뼱?⑥꽌 愿묓겢 吏??諛⑹?
                            StartOrPreemptDialogue(text, priority, category);
                            return;
                        }
                        else
                        {
                            // ?ㅻⅨ High ??ъ씠嫄곕굹 ?ㅽ궗?낆씠 ?꾨땲硫?蹂댄샇
                            dialogueQueue.Enqueue(new DialogueRequest { Text = text, Priority = priority, Category = category, RequestTime = Time.time });
                            return;
                        }
                    }

                    if (isGameOver)
                    {
                        dialogueQueue.Enqueue(new DialogueRequest { Text = text, Priority = priority, Category = category, RequestTime = Time.time });
                        return;
                    }
                }

                // ?꾩옱 吏꾪뻾 以묒씤 ??ш? Low??Normal?닿굅??鍮꾩뼱?덈떎硫?媛濡쒖콈湲?                StartOrPreemptDialogue(text, priority, category);
                return;
            }

            // ?꾩옱 留먰뭾?좎씠 異쒕젰 以묒씠嫄곕굹 理쒖냼 ?쎄린 Lock ?곹깭??寃쎌슦 (Normal, Low)
            if (isDisplaying)
            {
                if (priority == DialoguePriority.Low)
                {
                    // Low??臾댁“嫄?踰꾨━吏 ?딄퀬, ???댁뿉 湲곗〈 Low媛 ?덈떎硫?理쒖떊 ?댁슜?쇰줈 ??뼱?곷땲??
                    bool replaced = false;
                    var arr = dialogueQueue.ToArray();
                    for (int i = 0; i < arr.Length; i++)
                    {
                        if (arr[i].Priority == DialoguePriority.Low)
                        {
                            arr[i] = new DialogueRequest { Text = text, Priority = priority, Category = category, RequestTime = Time.time };
                            replaced = true;
                            break;
                        }
                    }

                    if (replaced)
                    {
                        dialogueQueue.Clear();
                        foreach (var req in arr) dialogueQueue.Enqueue(req);
                    }
                    else if (dialogueQueue.Count < 10) // ??怨듦컙???⑥븘?덈떎硫??쎌엯
                    {
                        dialogueQueue.Enqueue(new DialogueRequest { Text = text, Priority = priority, Category = category, RequestTime = Time.time });
                    }
                    return;
                }
                else
                {
                    // Normal? ?먭? ?덈Т 苑?李⑥엳吏 ?딅떎硫?理쒕? 10媛? ?먯뿉 ?湲?                    if (dialogueQueue.Count < 10)
                    {
                        dialogueQueue.Enqueue(new DialogueRequest { Text = text, Priority = priority, Category = category, RequestTime = Time.time });
                    }
                    return;
                }
            }

            // 留먰뭾?좎씠 鍮꾩뼱?덉쑝硫?利됱떆 異쒕젰 ?쒖옉
            StartOrPreemptDialogue(text, priority, category);
        }

        private void StartOrPreemptDialogue(string text, DialoguePriority priority, EventCategory category)
        {
            if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);
            if (hideBalloonCoroutine != null) StopCoroutine(hideBalloonCoroutine);

            ApplyDialogueTextStyle();
            SetTutorialAdvanceIndicator(false);
            dialogueBalloonPanel.SetActive(true);
            currentDisplayPriority = priority;
            currentDisplayCategory = category;

            // 移댄뀒怨좊━ 異쒕젰 ??꾩뒪?ы봽 湲곕줉
            lastCategoryOutputTimes[category] = Time.time;

            // 湲????鍮꾨? 理쒖냼 ?쎄린 蹂댁옣 ?쒓컙: 鍮좊Ⅸ ?멸쾶???띾룄(8遺?= 24?쒓컙)??留욎떠 max(2.2珥? 湲?먯닔 * 0.05珥?濡??⑥텞
            float minReadDuration = Mathf.Max(2.2f, text.Length * 0.05f);
            float displayDuration = Mathf.Clamp(text.Length * 0.07f, 2.5f, Mathf.Max(balloonDisplayDuration, 5.0f));

            if (traderStatus != null)
            {
                TraderEmotion contextualEmotion = TraderEmotionEvaluator.Evaluate(
                    CalculateCurrentRoe(),
                    traderStatus.CurrentMentalState,
                    traderStatus.HealthRatio,
                    category,
                    text);
                
                // ??ш? ?쒖떆?섎뒗 ?쒓컙(displayDuration)留뚰겮 媛먯젙???좎??섏뿬 愿대━瑜?諛⑹?
                ShowEmotion(contextualEmotion, Mathf.Max(contextualEmotionDuration, displayDuration));
            }

            isBalloonLocked = true;
            balloonUnlockTime = Time.time + minReadDuration;

            typewriterCoroutine = StartCoroutine(TypewriterCoroutine(text, displayDuration));
        }

        private IEnumerator TypewriterCoroutine(string text, float displayDuration)
        {
            dialogueText.text = text;
            dialogueText.maxVisibleCharacters = 0;
            dialogueText.ForceMeshUpdate();

            int totalChars = text.Length;
            for (int i = 1; i <= totalChars; i++)
            {
                dialogueText.maxVisibleCharacters = i;
                yield return new WaitForSeconds(typewriterCharDelay);
            }

            if (currentDisplayCategory == EventCategory.Tutorial)
            {
                SetTutorialAdvanceIndicator(true);
            }
            else
            {
                // 異쒕젰 ?꾨즺 ??理쒖냼 ?쎄린 蹂댁옣 ?쒓컙 諛?displayDuration ?湲?                hideBalloonCoroutine = StartCoroutine(HideBalloonOrProcessQueueAfterDelay(displayDuration));
            }
        }

        public void HideDialogueBalloon()
        {
            if (typewriterCoroutine != null)
            {
                StopCoroutine(typewriterCoroutine);
                typewriterCoroutine = null;
            }
            if (hideBalloonCoroutine != null)
            {
                StopCoroutine(hideBalloonCoroutine);
                hideBalloonCoroutine = null;
            }
            isBalloonLocked = false;
            SetTutorialAdvanceIndicator(false);
            if (dialogueBalloonPanel != null)
            {
                dialogueBalloonPanel.SetActive(false);
            }
        }

        private IEnumerator HideBalloonOrProcessQueueAfterDelay(float delay)
        {
            float elapsed = 0f;
            while (elapsed < delay)
            {
                elapsed += Time.deltaTime;
                if (Time.time >= balloonUnlockTime)
                {
                    isBalloonLocked = false;
                }
                yield return null;
            }

            isBalloonLocked = false;

            // ?湲??먯뿉 ?ㅼ쓬 ??ш? ?덈떎硫?爰쇰궡???쒖감 異쒕젰
            while (dialogueQueue.Count > 0)
            {
                DialogueRequest nextReq = dialogueQueue.Dequeue();
                var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
                bool isGameOver = gm != null && gm.CurrentState == GameManager.GameState.GameOver;
                
                float ttl = nextReq.Priority >= DialoguePriority.High ? 8f : 5f; // ?곹솴 吏??????먭린 ?좏슚湲곌컙 ???異뺤냼
                
                if (isGameOver || Time.time - nextReq.RequestTime < ttl)
                {
                    StartOrPreemptDialogue(nextReq.Text, nextReq.Priority, nextReq.Category);
                    yield break;
                }
                // 留뚮즺??寃쎌슦 臾댁떆?섍퀬 while 猷⑦봽 怨꾩냽 (?ㅼ쓬 ????뺤씤)
            }

            SetTutorialAdvanceIndicator(false);
            if (dialogueBalloonPanel != null) dialogueBalloonPanel.SetActive(false);
        }

        private void EnsureTutorialAdvanceIndicator()
        {
            if (tutorialAdvanceIndicator != null || dialogueBalloonPanel == null) return;

            GameObject indicator = new(
                "TutorialAdvanceIndicator",
                typeof(RectTransform),
                typeof(CanvasGroup));
            indicator.transform.SetParent(dialogueBalloonPanel.transform, false);

            tutorialAdvanceIndicator = indicator.GetComponent<RectTransform>();
            tutorialAdvanceIndicator.anchorMin = tutorialAdvanceIndicator.anchorMax = new Vector2(1f, 0f);
            tutorialAdvanceIndicator.pivot = new Vector2(0.5f, 0.5f);
            tutorialAdvanceIndicator.anchoredPosition = new Vector2(
                -DialogueContentPadding - 13f,
                DialogueContentPadding + 10f);
            tutorialAdvanceIndicator.sizeDelta = new Vector2(28f, 28f);

            tutorialAdvanceIndicatorGroup = indicator.GetComponent<CanvasGroup>();
            tutorialAdvanceIndicatorGroup.blocksRaycasts = false;
            tutorialAdvanceIndicatorGroup.interactable = false;

            Color arrowColor = new Color32(103, 232, 249, 255);
            CreateTutorialArrowStroke(indicator.transform, "UpperStroke", new Vector2(-2f, 5f), -45f, arrowColor);
            CreateTutorialArrowStroke(indicator.transform, "LowerStroke", new Vector2(-2f, -5f), 45f, arrowColor);
            indicator.SetActive(false);
        }

        private static void CreateTutorialArrowStroke(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            float rotation,
            Color color)
        {
            GameObject stroke = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            stroke.transform.SetParent(parent, false);
            RectTransform rect = stroke.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(17f, 4f);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);

            Image image = stroke.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        public void SetTutorialAdvanceIndicator(bool visible)
        {
            EnsureTutorialAdvanceIndicator();
            if (tutorialAdvanceIndicator == null) return;

            if (tutorialAdvanceIndicatorCoroutine != null)
            {
                StopCoroutine(tutorialAdvanceIndicatorCoroutine);
                tutorialAdvanceIndicatorCoroutine = null;
            }

            tutorialAdvanceIndicator.gameObject.SetActive(visible);
            if (!visible)
            {
                tutorialAdvanceIndicator.anchoredPosition = new Vector2(
                    -DialogueContentPadding - 13f,
                    DialogueContentPadding + 10f);
                if (tutorialAdvanceIndicatorGroup != null) tutorialAdvanceIndicatorGroup.alpha = 1f;
                return;
            }

            tutorialAdvanceIndicator.transform.SetAsLastSibling();
            tutorialAdvanceIndicatorCoroutine = StartCoroutine(PulseTutorialAdvanceIndicator());
        }

        private IEnumerator PulseTutorialAdvanceIndicator()
        {
            Vector2 basePosition = new(
                -DialogueContentPadding - 13f,
                DialogueContentPadding + 10f);

            while (tutorialAdvanceIndicator != null && tutorialAdvanceIndicator.gameObject.activeSelf)
            {
                float pulse = (Mathf.Sin(Time.unscaledTime * 6f) + 1f) * 0.5f;
                tutorialAdvanceIndicator.anchoredPosition =
                    basePosition + new Vector2(pulse * 6f, 0f);
                if (tutorialAdvanceIndicatorGroup != null)
                    tutorialAdvanceIndicatorGroup.alpha = Mathf.Lerp(0.35f, 1f, pulse);
                yield return null;
            }

            tutorialAdvanceIndicatorCoroutine = null;
        }

        public void ClearQueueExceptSkillUpgraded()
        {
            var filteredQueue = new Queue<DialogueRequest>();
            while (dialogueQueue.Count > 0)
            {
                var req = dialogueQueue.Dequeue();
                if (req.Category == EventCategory.SkillUpgraded) filteredQueue.Enqueue(req);
            }
            while (filteredQueue.Count > 0) dialogueQueue.Enqueue(filteredQueue.Dequeue());
        }

        private float GetCategoryCooldown(EventCategory category)
        {
            return category switch
            {
                EventCategory.ChartMovement => 10.0f,
                EventCategory.MentalChange => 7.0f,
                EventCategory.HealthChange => 8.0f,
                EventCategory.GimmickTriggered => 6.0f,
                EventCategory.ItemUsed => 3.0f,
                EventCategory.PositionOpened => 3.0f,
                EventCategory.PositionClosed => 3.0f,
                EventCategory.SkillUpgraded => 2.0f,
                _ => 4.0f
            };
        }
    }
}
