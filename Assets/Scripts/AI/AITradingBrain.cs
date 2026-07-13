using System;
using UnityEngine;
using FXOverdose.Trading;

namespace FXOverdose.AI
{
    public class AITradingBrain : MonoBehaviour
    {
        [Header("시스템 연결")]
        [SerializeField] private MarketSimulationEngine marketEngine;
        [SerializeField] private TradingController tradingController;
        [SerializeField] private TraderStatus traderStatus;
        [SerializeField] private GameManager gameManager;

        [Header("AI 판단 매개변수")]
        [SerializeField] [Range(1, 125)] private int defaultLeverage = 10;
        [SerializeField] [Range(0.1f, 0.9f)] private float tradeMarginRatio = 0.3f; // 1회 진입 시 시드 투자 비율

        [Header("현재 AI 판단 상태 (읽기 전용)")]
        [SerializeField] private bool isProcessingSignal = false;
        [SerializeField] private MarketSignal currentActiveSignal;
        [SerializeField] private string lastDecisionLog = "";

        // UI 및 대화 엔진 통지 이벤트: (대사 텍스트, 감정 변화량)
        public event Action<string, float> OnAIDecisionMade;
        public event Action<MarketSignal, bool> OnSignalEvaluationCompleted; // (신호, 진입여부)

        public string LastDecisionLog => lastDecisionLog;

        private void Start()
        {
            if (marketEngine == null) marketEngine = FindAnyObjectByType<MarketSimulationEngine>();
            if (tradingController == null) tradingController = FindAnyObjectByType<TradingController>();
            if (traderStatus == null) traderStatus = FindAnyObjectByType<TraderStatus>();
            if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();

            if (marketEngine != null)
            {
                marketEngine.OnMarketSignalGenerated += HandleMarketSignalGenerated;
                marketEngine.OnSignalPhaseChanged += HandleSignalPhaseChanged;
            }

            if (tradingController != null)
            {
                tradingController.OnPositionClosed += HandlePositionClosed;
                tradingController.OnPositionLiquidated += HandlePositionLiquidated;
            }
        }

        private void OnDestroy()
        {
            if (marketEngine != null)
            {
                marketEngine.OnMarketSignalGenerated -= HandleMarketSignalGenerated;
                marketEngine.OnSignalPhaseChanged -= HandleSignalPhaseChanged;
            }

            if (tradingController != null)
            {
                tradingController.OnPositionClosed -= HandlePositionClosed;
                tradingController.OnPositionLiquidated -= HandlePositionLiquidated;
            }
        }

        // 1단계: 신호 방송 수신 (Grace Window 돌입 시점)
        private void HandleMarketSignalGenerated(MarketSignal signal)
        {
            currentActiveSignal = signal;
            isProcessingSignal = true;

            // AI 체력 및 멘탈 상태에 따른 판단 분기 (Deception Tier)
            EvaluateSignalAndReact(signal);
        }

        private void HandleSignalPhaseChanged(SignalPhase phase, MarketSignal signal)
        {
            if (phase == SignalPhase.GuaranteedOverride && isProcessingSignal)
            {
                Debug.Log($"[AITradingBrain] ⚡ 확정적 주가 제어 2단계 작동: 진입 포지션 관리 중");
            }
            else if (phase == SignalPhase.Cooldown)
            {
                isProcessingSignal = false;
                // [게임적 허용 완벽 보장 장치 (2중 보장)]
                // 만약 True Signal(정상 확정 신호)을 따라 진입한 포지션이 캔들 노이즈 등으로 인해 
                // TargetPrice에 미세하게 닿지 못한 채 보장 구간(GuaranteedOverride)이 종료되더라도,
                // 기획상 설계된 상승/하락 수익률을 확실하게 보장받기 위해 Cooldown 돌입 즉시 자동 익절 청산!
                if (tradingController != null && tradingController.CurrentPosition != TradingController.PositionType.None)
                {
                    if (currentActiveSignal.IsTrueSignal && traderStatus != null && traderStatus.HealthRatio > 0.40f)
                    {
                        Debug.Log($"[AITradingBrain] 🎯 확정 주가 보장 구간(GuaranteedOverride) 종료 -> 게임 기획 수익률 100% 획득을 위한 즉시 익절 청산 실행");
                        tradingController.ClosePosition();
                    }
                }
            }
        }

        // 4단계 기만(Deception Tier) 기반 신호 분석 및 매매 결정
        private void EvaluateSignalAndReact(MarketSignal signal)
        {
            if (traderStatus == null || tradingController == null || gameManager == null) return;

            float healthRatio = traderStatus.HealthRatio;
            TraderStatus.MentalState mentalState = traderStatus.CurrentMentalState;
            float availableBalance = gameManager.CurrentBalance;

            if (availableBalance < 50f)
            {
                TriggerDialogue("증거금이 바닥났어... 남은 시드가 이것밖에 안 남다니 말도 안 돼...", -0.05f);
                return;
            }

            // 🔴 Tier 4: Overdose / 통제 불능 상태 (체력 <= 15% 또는 Danger/Overdose)
            if (healthRatio <= 0.15f || mentalState == TraderStatus.MentalState.Danger || mentalState == TraderStatus.MentalState.Overdose)
            {
                ExecuteOverdoseTrade(signal, availableBalance);
                return;
            }

            // 🟠 Tier 3: 심각한 피로/오인 상태 (Heavily Deceived, 체력 15% ~ 40%)
            if (healthRatio > 0.15f && healthRatio <= 0.40f)
            {
                // 강한 가짜 신호(불트랩/베어트랩)를 진짜 대박 자리로 오인하여 풀시드 고레버리지 진입!
                if (signal.Strength == SignalStrength.Strong && !signal.IsTrueSignal)
                {
                    TradingController.PositionType trapPos = signal.Type switch
                    {
                        MarketSignalType.BullTrap => TradingController.PositionType.Long,
                        MarketSignalType.BearTrap => TradingController.PositionType.Short,
                        _ => TradingController.PositionType.Long
                    };

                    float margin = availableBalance * 0.8f; // 풀시드 80% 물림
                    int leverage = Mathf.Min(50, defaultLeverage * 3);
                    float startPrice = signal.SignalStartPrice > 0f ? signal.SignalStartPrice : (marketEngine != null ? marketEngine.CurrentPrice : 65000f);
                    // 오인 진입 시 대박(+15%)을 꿈꾸며 목표가 설정
                    float aiTarget = trapPos == TradingController.PositionType.Long ? startPrice * 1.15f : startPrice * 0.85f;
                    float aiStopLoss = trapPos == TradingController.PositionType.Long ? startPrice * 0.95f : startPrice * 1.05f;

                    bool opened = tradingController.OpenPosition(trapPos, margin, leverage, aiTarget, aiStopLoss);

                    if (opened)
                    {
                        TriggerDialogue($"[오인 진입] 그래, 바로 지금이야!! 세력들의 거대한 매수벽이 들어왔어! 시드 80%를 {leverage}배로 다 박는다!! (목표가: ${aiTarget:N0})", -0.15f);
                        OnSignalEvaluationCompleted?.Invoke(signal, true);
                    }
                }
                else
                {
                    TriggerDialogue("머리가 너무 아파서 차트가 눈에 안 들어와... 왠지 불안해...", -0.08f);
                    OnSignalEvaluationCompleted?.Invoke(signal, false);
                }
                return;
            }

            // 🟡 Tier 2: 적당히 속는 상태 (Mildly Deceived, 체력 40% ~ 75%)
            if (healthRatio > 0.40f && healthRatio <= 0.75f)
            {
                // 약한 신호(Weak Signal)나 가짜 단타 미끼에 적당히 속아서 진입
                if (signal.Strength == SignalStrength.Weak)
                {
                    TradingController.PositionType weakPos = signal.Type switch
                    {
                        MarketSignalType.BullishBreakout => TradingController.PositionType.Long,
                        MarketSignalType.BearishBreakout => TradingController.PositionType.Short,
                        MarketSignalType.BullTrap => TradingController.PositionType.Long,
                        MarketSignalType.BearTrap => TradingController.PositionType.Short,
                        _ => TradingController.PositionType.Long
                    };

                    float margin = availableBalance * 0.25f; // 가볍게 25% 진입
                    int leverage = defaultLeverage;
                    float startPrice = signal.SignalStartPrice > 0f ? signal.SignalStartPrice : (marketEngine != null ? marketEngine.CurrentPrice : 65000f);
                    // 단타 목표가 (+3%), 손절가 (-2%)
                    float aiTarget = weakPos == TradingController.PositionType.Long ? startPrice * 1.03f : startPrice * 0.97f;
                    float aiStopLoss = weakPos == TradingController.PositionType.Long ? startPrice * 0.98f : startPrice * 1.02f;

                    bool opened = tradingController.OpenPosition(weakPos, margin, leverage, aiTarget, aiStopLoss);

                    if (opened)
                    {
                        TriggerDialogue($"[가벼운 단타 진입] 어? 돌파 각이 보인다! 가볍게 {leverage}배로 단타만 치고 나오자! (목표가: ${aiTarget:N0})", -0.02f);
                        OnSignalEvaluationCompleted?.Invoke(signal, true);
                    }
                }
                else if (signal.Strength == SignalStrength.Strong && signal.IsTrueSignal)
                {
                    // 강한 진짜 신호도 진입
                    OpenNormalPosition(signal, availableBalance, tradeMarginRatio, defaultLeverage);
                }
                else
                {
                    TriggerDialogue("뭔가 휩소 냄새가 나는데... 이번엔 그냥 관망해야겠어.", 0f);
                    OnSignalEvaluationCompleted?.Invoke(signal, false);
                }
                return;
            }

            // 🟢 Tier 1: 최상/정상 상태 (Stable, 체력 > 75%)
            if (healthRatio > 0.75f)
            {
                if (signal.Strength == SignalStrength.Weak)
                {
                    // 약한 미끼/단타 신호는 완벽히 필터링
                    TriggerDialogue("[신호 필터링] 거래량이 텅 비었잖아. 뻔한 가짜 반등 미끼... 절대 안 속아.", 0.05f);
                    OnSignalEvaluationCompleted?.Invoke(signal, false);
                }
                else if (signal.Strength == SignalStrength.Strong && signal.IsTrueSignal)
                {
                    // 확실한 수익 신호 포착
                    OpenNormalPosition(signal, availableBalance, tradeMarginRatio * 1.5f, defaultLeverage * 2);
                }
                else if (signal.Strength == SignalStrength.Strong && !signal.IsTrueSignal)
                {
                    // 대형 속임수 휩소 경고
                    TriggerDialogue("[속임수 경고] 세력들이 거대한 함정(Trap) 빔을 파놓았어. 지금 들어가면 청산이다... 패스.", 0.1f);
                    OnSignalEvaluationCompleted?.Invoke(signal, false);
                }
                return;
            }
        }

        // 정상/확실한 진입
        private void OpenNormalPosition(MarketSignal signal, float balance, float ratio, int leverage)
        {
            TradingController.PositionType posType = signal.Type switch
            {
                MarketSignalType.BullishBreakout => TradingController.PositionType.Long,
                MarketSignalType.BearishBreakout => TradingController.PositionType.Short,
                _ => TradingController.PositionType.Long
            };

            float margin = balance * Mathf.Clamp01(ratio);
            float startPrice = signal.SignalStartPrice > 0f ? signal.SignalStartPrice : (marketEngine != null ? marketEngine.CurrentPrice : 65000f);
            float deltaPct = Mathf.Abs(signal.TargetPercentageDelta) > 0.1f ? Mathf.Abs(signal.TargetPercentageDelta) / 100f : 0.045f;
            float aiTarget = posType == TradingController.PositionType.Long ? startPrice * (1f + deltaPct) : startPrice * (1f - deltaPct);
            float aiStopLoss = posType == TradingController.PositionType.Long ? startPrice * 0.98f : startPrice * 1.02f;

            bool opened = tradingController.OpenPosition(posType, margin, leverage, aiTarget, aiStopLoss);

            if (opened)
            {
                TriggerDialogue($"[골든타임 제어] 완벽한 3분 돌파 타점이다. 내 분석대로 시드 {ratio*100:0}%를 {leverage}배로 진입 완료. (목표가: ${aiTarget:N0})", 0.1f);
                OnSignalEvaluationCompleted?.Invoke(signal, true);
            }
        }

        // 폭주 뇌동매매 (Overdose)
        private void ExecuteOverdoseTrade(MarketSignal signal, float balance)
        {
            // 방향과 무관하게 125배 고레버리지 풀시드 물타기
            TradingController.PositionType crazyPos = UnityEngine.Random.value < 0.5f ? TradingController.PositionType.Long : TradingController.PositionType.Short;
            float margin = balance * 0.95f;
            int leverage = 125;
            float startPrice = signal.SignalStartPrice > 0f ? signal.SignalStartPrice : (marketEngine != null ? marketEngine.CurrentPrice : 65000f);
            // Overdose 시에는 목표가는 터무니없이 높게(+50%), 손절선은 없음(0f)
            float aiTarget = crazyPos == TradingController.PositionType.Long ? startPrice * 1.5f : startPrice * 0.5f;

            bool opened = tradingController.OpenPosition(crazyPos, margin, leverage, aiTarget, 0f);
            if (opened)
            {
                TriggerDialogue($"[OVERDOSE 뇌동매매] 몰라!! 다 필요 없어!! 지금 당장 125배로 올인 박자!! (목표가: ${aiTarget:N0})", -0.3f);
                OnSignalEvaluationCompleted?.Invoke(signal, true);
            }
        }

        // 포지션 종료 시 리액션 (약한 손해 구간/적당히 속았을 때의 반응 등)
        private void HandlePositionClosed(float returnedAmount, float pnl)
        {
            float balance = gameManager != null ? gameManager.CurrentBalance : returnedAmount;
            float baseMargin = tradingController != null 
                ? (tradingController.MarginAmount > 0f ? tradingController.MarginAmount : tradingController.LastMarginAmount) 
                : 0f;
            float roe = baseMargin > 0f ? (pnl / baseMargin) * 100f : 0f;

            if (pnl < 0f)
            {
                if (currentActiveSignal.Strength == SignalStrength.Weak)
                {
                    TriggerDialogue($"[단타 손절] 아씨... 가벼운 휩소에 긁혔어 ({roe:0.0}%)... 머리가 아파. 다음 타점엔 절대 안 당해.", -0.05f);
                }
                else
                {
                    TriggerDialogue($"[대형 손실 충격] 으아악... {roe:0.0}% 손절이라니... 말도 안 돼!! 왜 내 매물만 노리고 떨어지냐고!!", -0.2f);
                }
            }
            else
            {
                TriggerDialogue($"[익절 성공] 하하하!! {roe:+0.0}% 익절 달성!! 봤지? 차트의 신은 바로 나야!!", 0.15f);
            }
        }

        // 강제 청산(Liquidation) 시 극도의 멘탈 붕괴 리액션
        private void HandlePositionLiquidated()
        {
            isProcessingSignal = false;
            TriggerDialogue("[강제청산 대참사] 뭐...? 청산당했다고?! 내 전재산이... 안 돼!! 다 거짓말이야!! 차트가 나를 죽이려 하고 있어!!", -0.5f);
        }

        private void TriggerDialogue(string dialogue, float emotionDelta)
        {
            lastDecisionLog = dialogue;
            Debug.Log($"[AITradingBrain 💬] {dialogue}");
            OnAIDecisionMade?.Invoke(dialogue, emotionDelta);
        }
    }
}
