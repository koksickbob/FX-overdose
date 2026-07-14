using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FXOverdose.Trading;
using FXOverdose.AI.LLM;

namespace FXOverdose.AI
{
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
        [SerializeField] private float typewriterCharDelay = 0.03f;
        [SerializeField] private float balloonDisplayDuration = 8.0f;

        [Header("현재 상태 (읽기 전용)")]
        [SerializeField] private ExpressionState currentExpression = ExpressionState.Confident;
        private Coroutine typewriterCoroutine;
        private Coroutine hideBalloonCoroutine;

        private void Start()
        {
            if (traderStatus == null) traderStatus = FindAnyObjectByType<TraderStatus>();
            if (tradingController == null) tradingController = FindAnyObjectByType<TradingController>();
            if (aiBrain == null) aiBrain = FindAnyObjectByType<AITradingBrain>();
            if (llmService == null) llmService = FindAnyObjectByType<LocalLLMService>();

            if (aiBrain != null)
            {
                aiBrain.OnAIDecisionMade += HandleAIDecisionMade;
            }

            if (llmService != null)
            {
                llmService.OnDialogueGenerated += HandleLLMDialogueGenerated;
            }

            if (dialogueBalloonPanel != null) dialogueBalloonPanel.SetActive(false);
            if (dangerAuraEffect != null) dangerAuraEffect.SetActive(false);
        }

        private void OnDestroy()
        {
            if (aiBrain != null) aiBrain.OnAIDecisionMade -= HandleAIDecisionMade;
            if (llmService != null) llmService.OnDialogueGenerated -= HandleLLMDialogueGenerated;
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
            // AI 판단 대사가 출력되면 말풍선 표시
            DisplayDialogueBalloon(dialogue);
        }

        private void HandleLLMDialogueGenerated(string dialogue)
        {
            // 온디바이스 LLM이 생성한 대사 말풍선 표시
            DisplayDialogueBalloon(dialogue);
        }

        public void DisplayDialogueBalloon(string text)
        {
            if (dialogueBalloonPanel == null || dialogueText == null) return;

            if (typewriterCoroutine != null) StopCoroutine(typewriterCoroutine);
            if (hideBalloonCoroutine != null) StopCoroutine(hideBalloonCoroutine);

            dialogueBalloonPanel.SetActive(true);
            typewriterCoroutine = StartCoroutine(TypewriterCoroutine(text));
        }

        private IEnumerator TypewriterCoroutine(string text)
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

            hideBalloonCoroutine = StartCoroutine(HideBalloonAfterDelay(balloonDisplayDuration));
        }

        private IEnumerator HideBalloonAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (dialogueBalloonPanel != null) dialogueBalloonPanel.SetActive(false);
        }
    }
}
