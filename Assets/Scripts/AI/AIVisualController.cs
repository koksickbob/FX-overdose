using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FXOverdose.Trading;
using FXOverdose.AI.LLM;

namespace FXOverdose.AI
{
    public enum DialoguePriority
    {
        Low = 0,     // 단순 차트 관망/횡보/일반 중계 -> 바쁘면 스킵(Drop)
        Normal = 1,  // 포지션 진입/종료, 아이템 복용, 멘탈 변화 -> 큐 대기 후 순차 출력
        High = 2,    // 스킬 업그레이드, 중요 상태 등 -> 일반 대사 가로채기(Preempt)
        Critical = 3 // 강제 청산, 오버도즈 폭주, 돌발 기믹, 극단적 차트 변동 -> High 포함 모든 진행 중 대사 즉시 가로채기
    }

    public class AIVisualController : MonoBehaviour
    {
        private const float DialogueContentPadding = 52f;
        private const float DialogueTailCenterOffset = 6f;

        public enum ExpressionState
        {
            Delighted = 0, // 대박 수익 (ROE > +20%)
            Confident = 1, // 일반 수익 (ROE 0% ~ +20%)
            Anxious   = 2, // 손실 진행 (ROE -20% ~ 0%)
            Desperate = 3, // 극심한 물림 (ROE < -20% 또는 Danger)
            Overdose  = 4  // 폭주 상태 (Overdose)
        }

        private struct DialogueRequest
        {
            public string Text;
            public DialoguePriority Priority;
            public EventCategory Category;
            public float RequestTime;
        }

        [Header("시스템 연결")]
        [SerializeField] private TraderStatus traderStatus;
        [SerializeField] private TradingController tradingController;
        [SerializeField] private AITradingBrain aiBrain;
        [SerializeField] private LocalLLMService llmService;
        [SerializeField] private Inventory inventory;

        [Header("비주얼 및 애니메이터")]
        [SerializeField] private Animator characterAnimator;
        [SerializeField] private Image characterImage;
        [SerializeField] private GameObject dangerAuraEffect; // Danger/Overdose 시 경고 오라
        [SerializeField, Min(0f)] private float contextualEmotionDuration = 4f;

        [Header("말풍선 UI (Typewriter Effect)")]
        [SerializeField] private GameObject dialogueBalloonPanel;
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private TMP_FontAsset dialogueFont;
        [SerializeField, Min(8f)] private float dialogueFontSizeMin = 19f;
        [SerializeField, Min(8f)] private float dialogueFontSizeMax = 27f;
        [SerializeField] private float typewriterCharDelay = 0.02f;
#pragma warning disable 0414
        [SerializeField] private float balloonDisplayDuration = 4.0f; // 기존 6.0초에서 빠른 8분 인게임 속도에 맞춰 4.0초로 단축
#pragma warning restore 0414
        public float BalloonDisplayDuration => balloonDisplayDuration;

        [Header("현재 상태 (읽기 전용)")]
        [SerializeField] private TraderEmotion currentEmotion = TraderEmotion.Focused;
        private readonly Dictionary<TraderEmotion, Sprite> emotionSprites = new Dictionary<TraderEmotion, Sprite>();
        private readonly Dictionary<string, Sprite> itemUseSprites = new Dictionary<string, Sprite>();
        private float emotionOverrideUntil;
        private bool isItemUseVisualActive;
        private float itemUseVisualUntil;
        private Coroutine typewriterCoroutine;
        private Coroutine hideBalloonCoroutine;
        private DialoguePriority currentDisplayPriority = DialoguePriority.Normal;
        private EventCategory currentDisplayCategory = EventCategory.General;

        // 우선순위 큐 및 쿨타임/Lock 관리 제어부
        private Queue<DialogueRequest> dialogueQueue = new Queue<DialogueRequest>();
        private bool isBalloonLocked = false;
        private float balloonUnlockTime = 0f;
        private Dictionary<EventCategory, float> lastCategoryOutputTimes = new Dictionary<EventCategory, float>();

        private void Start()
        {
            if (traderStatus == null) traderStatus = FindAnyObjectByType<TraderStatus>();
            if (tradingController == null) tradingController = FindAnyObjectByType<TradingController>();
            if (aiBrain == null) aiBrain = FindAnyObjectByType<AITradingBrain>();
            if (llmService == null) llmService = LocalLLMService.Instance;
            if (inventory == null) inventory = FindAnyObjectByType<Inventory>(FindObjectsInactive.Include);

            ResolveCharacterImage();
            LoadEmotionSprites();
            LoadItemUseSprites();
            ApplyEmotion(currentEmotion, true);

            ApplyDialogueTextStyle();

            if (aiBrain != null)
            {
                aiBrain.OnAIDecisionMade += HandleAIDecisionMade;
            }

            if (llmService != null)
            {
                // 중복 호출 방지를 위해 카테고리 정보가 포함된 이벤트만 단일 구독
                llmService.OnDialogueGeneratedWithCategory -= HandleLLMDialogueGeneratedWithCategory;
                llmService.OnDialogueGeneratedWithCategory += HandleLLMDialogueGeneratedWithCategory;
            }

            if (inventory != null)
            {
                inventory.ItemConsumed -= HandleItemConsumed;
                inventory.ItemConsumed += HandleItemConsumed;
            }

            if (dialogueBalloonPanel != null) dialogueBalloonPanel.SetActive(false);
            if (dangerAuraEffect != null) dangerAuraEffect.SetActive(false);
        }

        // AI가 새 대사를 출력할 때도 말풍선 안에서 동일한 폰트와 크기를 유지한다.
        private void ApplyDialogueTextStyle()
        {
            if (dialogueText == null) return;

            // 씬 또는 기존 프리팹에 14 / 22 구버전 값이 남아있는 경우 5포인트 올린 크기(19 / 27)로 자동 보정
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

            // 말풍선의 크기에 맞게 유동적으로 글자 크기가 바뀌도록 AutoSizing 활성화 및 최소/최대 설정
            dialogueText.enableAutoSizing = true;
            dialogueText.fontSizeMin = dialogueFontSizeMin;
            dialogueText.fontSizeMax = Mathf.Max(dialogueFontSizeMin, dialogueFontSizeMax);
            dialogueText.fontStyle = FontStyles.Normal;
            dialogueText.color = new Color32(207, 250, 254, 255); // UI 가이드 AI 대사색 #CFFAFE
            dialogueText.alignment = TextAlignmentOptions.Center;
            dialogueText.textWrappingMode = TextWrappingModes.Normal;
            dialogueText.overflowMode = TextOverflowModes.Ellipsis;

            // 스프라이트의 투명 바깥 영역과 왼쪽 꼬리를 피해 실제 프레임 안쪽에 텍스트를 배치합니다.
            RectTransform textRect = dialogueText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(
                DialogueContentPadding + DialogueTailCenterOffset,
                DialogueContentPadding);
            textRect.offsetMax = new Vector2(
                -(DialogueContentPadding - DialogueTailCenterOffset),
                -DialogueContentPadding);

            // 보이는 프레임 기준 여백을 RectTransform에서 처리하므로 TMP 내부 여백은 중복 적용하지 않습니다.
            dialogueText.margin = Vector4.zero;
        }

        private void OnDestroy()
        {
            if (aiBrain != null) aiBrain.OnAIDecisionMade -= HandleAIDecisionMade;
            if (llmService != null)
            {
                llmService.OnDialogueGeneratedWithCategory -= HandleLLMDialogueGeneratedWithCategory;
            }
            if (inventory != null)
            {
                inventory.ItemConsumed -= HandleItemConsumed;
            }
        }

        private void Update()
        {
            UpdateExpressionState();
        }

        // 실시간 수익률 및 멘탈 상태를 기반으로 감정을 도출하고 표정 상태로 매핑
        private void UpdateExpressionState()
        {
            // 아이템 사용 포즈는 게임 일시정지 여부와 관계없이 실제 시간 기준 약 1초간 최우선 표시합니다.
            if (isItemUseVisualActive)
            {
                if (Time.unscaledTime < itemUseVisualUntil) return;

                isItemUseVisualActive = false;
                ApplyEmotion(currentEmotion, true);
            }

            if (traderStatus == null) return;

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
            if (characterImage != null) return;
            GameObject characterObject = GameObject.Find("ProtagonistCharacterImage");
            if (characterObject != null)
                characterImage = characterObject.GetComponent<Image>();
        }

        private void LoadEmotionSprites()
        {
            emotionSprites.Clear();
            foreach (TraderEmotion emotion in Enum.GetValues(typeof(TraderEmotion)))
            {
                Sprite sprite = Resources.Load<Sprite>($"Characters/Emotions/{emotion}");
                if (sprite != null)
                    emotionSprites[emotion] = sprite;
                else
                    Debug.LogWarning($"[AIVisualController] 감정 스프라이트를 찾지 못했습니다: {emotion}", this);
            }
        }

        private void LoadItemUseSprites()
        {
            itemUseSprites.Clear();
            LoadItemUseSprite("energy_drink", "EnergyDrink");
            LoadItemUseSprite("dessert", "Dessert");
            LoadItemUseSprite("supplement", "Supplement");
            LoadItemUseSprite("sedative", "Sedative");
        }

        private void LoadItemUseSprite(string itemId, string resourceName)
        {
            Sprite sprite = Resources.Load<Sprite>($"Characters/ItemUse/{resourceName}");
            if (sprite != null)
            {
                itemUseSprites[itemId] = sprite;
            }
            else
            {
                Debug.LogWarning($"[AIVisualController] 아이템 사용 스프라이트를 찾지 못했습니다: {itemId}", this);
            }
        }

        /// <summary>아이템 사용 포즈를 실제 시간 기준으로 잠시 표시합니다.</summary>
        public void ShowItemUse(string itemId, float duration = 1f)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return;
            if (itemUseSprites.Count == 0) LoadItemUseSprites();

            if (!itemUseSprites.TryGetValue(itemId, out Sprite sprite) || sprite == null)
            {
                Debug.LogWarning($"[AIVisualController] 지원하지 않는 아이템 사용 연출입니다: {itemId}", this);
                return;
            }

            ResolveCharacterImage();
            if (characterImage == null) return;

            isItemUseVisualActive = true;
            itemUseVisualUntil = Time.unscaledTime + Mathf.Max(0.1f, duration);
            characterImage.sprite = sprite;
            characterImage.preserveAspect = true;
        }

        private void HandleItemConsumed(ItemData item)
        {
            if (item != null) ShowItemUse(item.ItemId, 1f);
        }

        /// <summary>이벤트나 연출 코드에서 19종 감정을 직접 표시할 때 사용합니다.</summary>
        public void ShowEmotion(TraderEmotion emotion, float duration = 4f)
        {
            emotionOverrideUntil = Time.unscaledTime + Mathf.Max(0f, duration);

            // 대사 감정은 최신 상태로 기록하되, 진행 중인 1초 아이템 포즈를 덮어쓰지는 않습니다.
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
                characterImage.sprite = sprite;
                characterImage.preserveAspect = true;
            }

            // 기존 Animator를 사용하는 씬도 0~18 ExpressionState 파라미터로 호환합니다.
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

        private void HandleAIDecisionMade(string dialogue, float emotionDelta)
        {
            DisplayDialogueBalloon(dialogue, DialoguePriority.Normal, EventCategory.General);
        }

        private void HandleLLMDialogueGenerated(string dialogue)
        {
            // 하위 호환
            DisplayDialogueBalloon(dialogue, DialoguePriority.Normal, EventCategory.General);
        }

        private void HandleLLMDialogueGeneratedWithCategory(EventCategory category, string dialogue)
        {
            // 일일 정산 대사는 전용 DAILY LEDGER 안에서 표시하므로 메인 말풍선에 중복 출력하지 않습니다.
            if (category == EventCategory.DailySettlement)
            {
                return;
            }

            DialoguePriority priority = DialoguePriority.Normal;
            if (category == EventCategory.SkillUpgraded)
            {
                priority = DialoguePriority.High;
            }
            else if (traderStatus != null && (traderStatus.CurrentMentalState == TraderStatus.MentalState.Overdose || traderStatus.HealthRatio <= 0.05f))
            {
                priority = DialoguePriority.Critical;
            }
            else if (category == EventCategory.GimmickTriggered || (traderStatus != null && traderStatus.CurrentMentalState == TraderStatus.MentalState.Danger))
            {
                priority = DialoguePriority.Critical;
            }
            else if (category == EventCategory.ChartMovement || category == EventCategory.General)
            {
                priority = DialoguePriority.Low;
            }

            DisplayDialogueBalloon(dialogue, priority, category);
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
            if (string.IsNullOrEmpty(text) || dialogueBalloonPanel == null || dialogueText == null) return;

            // 🚀 [고속 스킵 중 대사 제한] 시간이 빠르게 스킵 중일 때는 중요(High 이상) 대사만 수용하고, 나머지는 무시하여 밀림 방지
            var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            if (gm != null && gm.IsFastForwardingTime && priority < DialoguePriority.High)
            {
                return;
            }

            // 🚀 [스킬업 광클 대사 큐 정리] 새로운 스킬업 대사가 들어오면 큐에 밀려있던 예전 스킬업 대사는 비움
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

            // 1. 카테고리별 글로벌 쿨타임 검사 (High 이상 우선순위는 쿨타임 무시)
            if (priority < DialoguePriority.High && category != EventCategory.General)
            {
                float cooldown = GetCategoryCooldown(category);
                if (lastCategoryOutputTimes.TryGetValue(category, out float lastTime))
                {
                    if (Time.time - lastTime < cooldown)
                    {
                        // 쿨타임 중이면 스킵 (피로도 방지)
                        return;
                    }
                }
            }

            // 2. 우선순위에 따른 큐 및 가로채기(Preempt) 처리
            bool isDisplaying = (isBalloonLocked || (dialogueBalloonPanel != null && dialogueBalloonPanel.activeSelf));
            bool isGameOver = gm != null && gm.CurrentState == GameManager.GameState.GameOver;

            if (priority == DialoguePriority.Critical)
            {
                // Critical은 기존 대사가 무엇이든 즉시 가로채기
                StartOrPreemptDialogue(text, priority, category);
                return;
            }

            if (priority == DialoguePriority.High)
            {
                if (isDisplaying)
                {
                    if (currentDisplayPriority == DialoguePriority.Critical)
                    {
                        // Critical이 출력 중이면 High는 가로채지 못하고 큐에 적재
                        dialogueQueue.Enqueue(new DialogueRequest { Text = text, Priority = priority, Category = category, RequestTime = Time.time });
                        return;
                    }

                    if (currentDisplayPriority == DialoguePriority.High && !isGameOver)
                    {
                        // 💡 [동일 이벤트 연속 발생 강제 덮어쓰기] 
                        if (currentDisplayCategory == category && category == EventCategory.SkillUpgraded)
                        {
                            // 같은 스킬 업그레이드면 즉시 덮어써서 광클 지연 방지
                            StartOrPreemptDialogue(text, priority, category);
                            return;
                        }
                        else
                        {
                            // 다른 High 대사이거나 스킬업이 아니면 보호
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

                // 현재 진행 중인 대사가 Low나 Normal이거나 비어있다면 가로채기
                StartOrPreemptDialogue(text, priority, category);
                return;
            }

            // 현재 말풍선이 출력 중이거나 최소 읽기 Lock 상태인 경우 (Normal, Low)
            if (isDisplaying)
            {
                if (priority == DialoguePriority.Low)
                {
                    // Low는 무조건 버리지 않고, 큐 내에 기존 Low가 있다면 최신 내용으로 덮어씁니다.
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
                    else if (dialogueQueue.Count < 10) // 큐 공간이 남아있다면 삽입
                    {
                        dialogueQueue.Enqueue(new DialogueRequest { Text = text, Priority = priority, Category = category, RequestTime = Time.time });
                    }
                    return;
                }
                else
                {
                    // Normal은 큐가 너무 꽉 차있지 않다면(최대 10개) 큐에 대기
                    if (dialogueQueue.Count < 10)
                    {
                        dialogueQueue.Enqueue(new DialogueRequest { Text = text, Priority = priority, Category = category, RequestTime = Time.time });
                    }
                    return;
                }
            }

            // 말풍선이 비어있으면 즉시 출력 시작
            StartOrPreemptDialogue(text, priority, category);
        }

        private void StartOrPreemptDialogue(string text, DialoguePriority priority, EventCategory category)
        {
            if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);
            if (hideBalloonCoroutine != null) StopCoroutine(hideBalloonCoroutine);

            ApplyDialogueTextStyle();
            dialogueBalloonPanel.SetActive(true);
            currentDisplayPriority = priority;
            currentDisplayCategory = category;

            // 카테고리 출력 타임스탬프 기록
            lastCategoryOutputTimes[category] = Time.time;

            // 글자 수 비례 최소 읽기 보장 시간: 빠른 인게임 속도(8분 = 24시간)에 맞춰 max(2.2초, 글자수 * 0.05초)로 단축
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
                
                // 대사가 표시되는 시간(displayDuration)만큼 감정을 유지하여 괴리를 방지
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

            // 출력 완료 후 최소 읽기 보장 시간 및 displayDuration 대기
            hideBalloonCoroutine = StartCoroutine(HideBalloonOrProcessQueueAfterDelay(displayDuration));
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

            // 대기 큐에 다음 대사가 있다면 꺼내서 순차 출력
            while (dialogueQueue.Count > 0)
            {
                DialogueRequest nextReq = dialogueQueue.Dequeue();
                var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
                bool isGameOver = gm != null && gm.CurrentState == GameManager.GameState.GameOver;
                
                float ttl = nextReq.Priority >= DialoguePriority.High ? 8f : 5f; // 상황 지난 대사 폐기 유효기간 대폭 축소
                
                if (isGameOver || Time.time - nextReq.RequestTime < ttl)
                {
                    StartOrPreemptDialogue(nextReq.Text, nextReq.Priority, nextReq.Category);
                    yield break;
                }
                // 만료된 경우 무시하고 while 루프 계속 (다음 대사 확인)
            }

            if (dialogueBalloonPanel != null) dialogueBalloonPanel.SetActive(false);
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
