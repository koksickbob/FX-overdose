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
        High = 2     // 강제 청산, 파산, 탈진, 오버도즈 폭주 -> 즉시 진행 중인 대사를 가로채기(Preempt)
    }

    public class AIVisualController : MonoBehaviour
    {
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

        [Header("비주얼 및 애니메이터")]
        [SerializeField] private Animator characterAnimator;
        [SerializeField] private GameObject dangerAuraEffect; // Danger/Overdose 시 경고 오라

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
        [SerializeField] private ExpressionState currentExpression = ExpressionState.Confident;
        private Coroutine typewriterCoroutine;
        private Coroutine hideBalloonCoroutine;

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
            dialogueText.alignment = TextAlignmentOptions.TopLeft;
            dialogueText.textWrappingMode = TextWrappingModes.Normal;
            dialogueText.overflowMode = TextOverflowModes.Ellipsis;
            dialogueText.margin = new Vector4(6f, 5f, 6f, 5f);
        }

        private void OnDestroy()
        {
            if (aiBrain != null) aiBrain.OnAIDecisionMade -= HandleAIDecisionMade;
            if (llmService != null)
            {
                llmService.OnDialogueGeneratedWithCategory -= HandleLLMDialogueGeneratedWithCategory;
            }
        }

        private void Update()
        {
            UpdateExpressionState();
        }

        // 실시간 수익률 및 멘탈 상태를 기반으로 4단계 표정 전환
        private void UpdateExpressionState()
        {
            if (traderStatus == null) return;

            ExpressionState targetExp = currentExpression;

            if (traderStatus.CurrentMentalState == TraderStatus.MentalState.Overdose)
            {
                targetExp = ExpressionState.Overdose;
            }
            else if (traderStatus.CurrentMentalState == TraderStatus.MentalState.Danger)
            {
                targetExp = ExpressionState.Desperate;
            }
            else if (tradingController != null && tradingController.CurrentPosition != TradingController.PositionType.None && tradingController.MarginAmount > 0f)
            {
                float currentPrice = FindAnyObjectByType<MarketSimulationEngine>()?.CurrentPrice ?? tradingController.EntryPrice;
                float priceDiff = tradingController.CurrentPosition == TradingController.PositionType.Long 
                    ? (currentPrice - tradingController.EntryPrice) : (tradingController.EntryPrice - currentPrice);
                float pnl = priceDiff * (tradingController.MarginAmount * tradingController.CurrentLeverage / tradingController.EntryPrice);
                float roe = (pnl / tradingController.MarginAmount) * 100f;

                if (roe > 20f) targetExp = ExpressionState.Delighted;
                else if (roe >= 0f) targetExp = ExpressionState.Confident;
                else if (roe > -20f) targetExp = ExpressionState.Anxious;
                else targetExp = ExpressionState.Desperate;
            }
            else
            {
                // 포지션 없을 때 체력/멘탈 기준
                targetExp = traderStatus.HealthRatio > 0.5f ? ExpressionState.Confident : ExpressionState.Anxious;
            }

            if (targetExp != currentExpression)
            {
                currentExpression = targetExp;
                ApplyExpressionToAnimator(currentExpression);
            }

            // 오라 제어
            bool showAura = currentExpression == ExpressionState.Desperate || currentExpression == ExpressionState.Overdose;
            if (dangerAuraEffect != null && dangerAuraEffect.activeSelf != showAura)
            {
                dangerAuraEffect.SetActive(showAura);
            }
        }

        private void ApplyExpressionToAnimator(ExpressionState state)
        {
            if (characterAnimator != null)
            {
                characterAnimator.SetInteger("ExpressionState", (int)state);
                characterAnimator.SetTrigger("OnExpressionChanged");
            }
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
            DialoguePriority priority = DialoguePriority.Normal;
            if (category == EventCategory.SkillUpgraded)
            {
                priority = DialoguePriority.High;
            }
            else if (traderStatus != null && (traderStatus.CurrentMentalState == TraderStatus.MentalState.Overdose || traderStatus.HealthRatio <= 0.05f))
            {
                priority = DialoguePriority.High;
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

            // 1. 카테고리별 글로벌 쿨타임 검사 (High 우선순위는 쿨타임 무시)
            if (priority != DialoguePriority.High && category != EventCategory.General)
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
            if (priority == DialoguePriority.High)
            {
                // 💡 [게임오버 연쇄 대사 보호] 게임오버 상태에서는 말풍선이 출력/Lock 중일 때 이전 연쇄 대사를 중간에 끊지 않고 큐에 적재하여 순차적으로 완벽히 읽을 수 있게 보장!
                var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
                if (gm != null && gm.CurrentState == GameManager.GameState.GameOver && (isBalloonLocked || (dialogueBalloonPanel != null && dialogueBalloonPanel.activeSelf)))
                {
                    dialogueQueue.Enqueue(new DialogueRequest { Text = text, Priority = priority, Category = category, RequestTime = Time.time });
                    return;
                }

                // 일반 High는 즉시 현재 진행 중인 대사를 멈추고 가로채기
                StartOrPreemptDialogue(text, priority, category);
                return;
            }

            // 현재 말풍선이 출력 중이거나 최소 읽기 Lock 상태인 경우
            if (isBalloonLocked || (dialogueBalloonPanel != null && dialogueBalloonPanel.activeSelf))
            {
                if (priority == DialoguePriority.Low)
                {
                    // Low는 현재 말풍선이 떠 있거나 큐가 밀려 있으면 과감히 스킵(Drop)
                    return;
                }
                else
                {
                    // Normal은 큐가 너무 꽉 차있지 않다면(최대 3개) 큐에 대기
                    if (dialogueQueue.Count < 3)
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

            // 카테고리 출력 타임스탬프 기록
            lastCategoryOutputTimes[category] = Time.time;

            // 글자 수 비례 최소 읽기 보장 시간: 빠른 인게임 속도(8분 = 24시간)에 맞춰 max(2.2초, 글자수 * 0.05초)로 단축
            float minReadDuration = Mathf.Max(2.2f, text.Length * 0.05f);
            float displayDuration = Mathf.Clamp(text.Length * 0.07f, 2.5f, Mathf.Max(balloonDisplayDuration, 5.0f));

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
            if (dialogueQueue.Count > 0)
            {
                DialogueRequest nextReq = dialogueQueue.Dequeue();
                // 큐에서 대기한 지 8초가 넘은 일반 요청은 만료 처리하여 낡은 상황 대사를 스킵 (단, 게임오버 상태의 멘헤라 연쇄 대사는 만료되지 않고 100% 출력!)
                var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
                bool isGameOver = gm != null && gm.CurrentState == GameManager.GameState.GameOver;
                if (isGameOver || Time.time - nextReq.RequestTime < 8f)
                {
                    StartOrPreemptDialogue(nextReq.Text, nextReq.Priority, nextReq.Category);
                    yield break;
                }
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
