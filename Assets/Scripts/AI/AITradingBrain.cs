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
            traderStatus = TraderStatus.CanonicalInstance;
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

            // 상시 멘탈 소모 6대 기믹 코어 컨트롤러 자동 바인딩
            MentalDrainGimmickController drainController = FindAnyObjectByType<MentalDrainGimmickController>();
            if (drainController == null)
            {
                drainController = gameObject.AddComponent<MentalDrainGimmickController>();
            }
            drainController.Initialize(traderStatus, tradingController, marketEngine);
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
            if (gameManager == null) gameManager = UnityEngine.Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            if (marketEngine == null) marketEngine = UnityEngine.Object.FindAnyObjectByType<MarketSimulationEngine>(FindObjectsInactive.Include);

            if (gameManager != null && gameManager.CurrentState != GameManager.GameState.Playing) return;
            if (marketEngine != null && !marketEngine.IsMarketOpen) return;

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
                    else if (!currentActiveSignal.IsTrueSignal)
                    {
                        Debug.Log($"[AITradingBrain] ⚠️ 가짜 신호/트랩(Trap) 구간 종료 -> 휩소 갇힘 방지를 위해 포지션을 손절/정리합니다.");
                        tradingController.ClosePosition();
                    }
                }
            }
        }

        // 4단계 기만(Deception Tier) 기반 신호 분석 및 매매 결정
        private void EvaluateSignalAndReact(MarketSignal signal)
        {
            if (traderStatus == null) traderStatus = TraderStatus.CanonicalInstance;
            if (tradingController == null) tradingController = UnityEngine.Object.FindAnyObjectByType<TradingController>(FindObjectsInactive.Include);
            if (gameManager == null) gameManager = UnityEngine.Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);

            if (traderStatus == null || tradingController == null || gameManager == null) return;

            float healthRatio = traderStatus.HealthRatio;
            TraderStatus.MentalState mentalState = traderStatus.CurrentMentalState;
            float availableBalance = gameManager.CurrentBalance;

            if (availableBalance < 10f)
            {
                TriggerDialogue("증거금이 바닥났어... 남은 시드가 이것밖에 안 남다니 말도 안 돼...", -0.05f);
                OnSignalEvaluationCompleted?.Invoke(signal, false);
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
                        TriggerDialogueWithCategory(FXOverdose.AI.LLM.EventCategory.PositionOpened, $"[오인 진입] {trapPos} 시드 80% ({leverage}배, 목표가 ${aiTarget:N0})", -0.15f);
                        OnSignalEvaluationCompleted?.Invoke(signal, true);
                    }
                    else
                    {
                        OnSignalEvaluationCompleted?.Invoke(signal, false);
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
                    float aiTarget = weakPos == TradingController.PositionType.Long ? startPrice * 1.03f : startPrice * 0.97f;
                    float aiStopLoss = weakPos == TradingController.PositionType.Long ? startPrice * 0.98f : startPrice * 1.02f;

                    bool opened = tradingController.OpenPosition(weakPos, margin, leverage, aiTarget, aiStopLoss);
                    if (opened)
                    {
                        TriggerDialogueWithCategory(FXOverdose.AI.LLM.EventCategory.PositionOpened, $"[단타 진입] {weakPos} 가볍게 {leverage}배 진입 (목표가 ${aiTarget:N0})", -0.02f);
                        OnSignalEvaluationCompleted?.Invoke(signal, true);
                    }
                    else
                    {
                        OnSignalEvaluationCompleted?.Invoke(signal, false);
                    }
                }
                else if (signal.Strength == SignalStrength.Strong && signal.IsTrueSignal)
                {
                    OpenNormalPosition(signal, availableBalance, tradeMarginRatio, defaultLeverage);
                }
                else
                {
                    // 60% 확률로 함정에 빠져 고레버리지 진입, 40% 관망
                    if (UnityEngine.Random.value < 0.6f)
                    {
                        OpenNormalPosition(signal, availableBalance, tradeMarginRatio * 0.8f, defaultLeverage);
                    }
                    else
                    {
                        TriggerDialogue("뭔가 휩소 냄새가 나는데... 이번엔 그냥 관망해야겠어.", 0f);
                        OnSignalEvaluationCompleted?.Invoke(signal, false);
                    }
                }
                return;
            }

            // 🟢 Tier 1: 최상/정상 상태 (Stable, 체력 > 75%)
            if (healthRatio > 0.75f)
            {
                if (signal.Strength == SignalStrength.Weak)
                {
                    if (signal.IsTrueSignal)
                    {
                        // 💡 진짜 약한 신호는 가볍게 단타 스캘핑 진입
                        OpenNormalPosition(signal, availableBalance, tradeMarginRatio * 0.6f, defaultLeverage);
                    }
                    else
                    {
                        TriggerDialogue("[신호 필터링] 거래량이 텅 비었잖아. 뻔한 가짜 반등 미끼... 절대 안 속아.", 0.05f);
                        OnSignalEvaluationCompleted?.Invoke(signal, false);
                    }
                }
                else if (signal.Strength == SignalStrength.Strong && signal.IsTrueSignal)
                {
                    // 확실한 수익 신호 포착
                    OpenNormalPosition(signal, availableBalance, tradeMarginRatio * 1.5f, defaultLeverage * 2);
                }
                else if (signal.Strength == SignalStrength.Strong && !signal.IsTrueSignal)
                {
                    // 💡 대형 속임수(Trap/False Breakout)를 날카롭게 간파하고 75% 확률로 함정을 역이용하는 역매매(Counter-Trap) 진입!
                    if (UnityEngine.Random.value < 0.75f)
                    {
                        TradingController.PositionType counterPos = signal.Type switch
                        {
                            MarketSignalType.BullTrap => TradingController.PositionType.Short, // 롱 유도 함정이므로 숏 진입
                            MarketSignalType.BearTrap => TradingController.PositionType.Long,  // 숏 유도 함정이므로 롱 진입
                            MarketSignalType.BullishBreakout => TradingController.PositionType.Short,
                            MarketSignalType.BearishBreakout => TradingController.PositionType.Long,
                            _ => TradingController.PositionType.Short
                        };

                        float margin = availableBalance * tradeMarginRatio;
                        int leverage = defaultLeverage * 2;
                        float startPrice = signal.SignalStartPrice > 0f ? signal.SignalStartPrice : (marketEngine != null ? marketEngine.CurrentPrice : 65000f);
                        float aiTarget = counterPos == TradingController.PositionType.Long ? startPrice * 1.05f : startPrice * 0.95f;
                        float aiStopLoss = counterPos == TradingController.PositionType.Long ? startPrice * 0.98f : startPrice * 1.02f;

                        bool opened = tradingController.OpenPosition(counterPos, margin, leverage, aiTarget, aiStopLoss);
                        if (opened)
                        {
                            TriggerDialogueWithCategory(FXOverdose.AI.LLM.EventCategory.PositionOpened, $"[역매매 진입] 세력 함정 간파 후 역매매 {counterPos} {leverage}배 진입 (목표가 ${aiTarget:N0})", 0.15f);
                            OnSignalEvaluationCompleted?.Invoke(signal, true);
                        }
                        else
                        {
                            OnSignalEvaluationCompleted?.Invoke(signal, false);
                        }
                    }
                    else
                    {
                        TriggerDialogue("[속임수 경고] 세력들이 거대한 함정(Trap) 빔을 파놓았어. 지금 들어가면 청산이다... 패스.", 0.1f);
                        OnSignalEvaluationCompleted?.Invoke(signal, false);
                    }
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
                TriggerDialogueWithCategory(FXOverdose.AI.LLM.EventCategory.PositionOpened, $"[정상 진입] {posType} 시드 {ratio*100:0}% ({leverage}배, 목표가 ${aiTarget:N0})", 0.1f);
                OnSignalEvaluationCompleted?.Invoke(signal, true);
            }
            else
            {
                OnSignalEvaluationCompleted?.Invoke(signal, false);
            }
        }

        // 폭주 뇌동매매 (Overdose)
        private void ExecuteOverdoseTrade(MarketSignal signal, float balance)
        {
            // [기획서 4.5장 부합] Overdose 시 "손실이 큰 방향으로 고레버리지 진입 강제 실행"
            TradingController.PositionType crazyPos = signal.TargetPercentageDelta > 0f 
                ? TradingController.PositionType.Short : TradingController.PositionType.Long;
            float margin = balance * 0.95f;
            int leverage = 125;
            float startPrice = signal.SignalStartPrice > 0f ? signal.SignalStartPrice : (marketEngine != null ? marketEngine.CurrentPrice : 65000f);
            // Overdose 시에는 목표가는 터무니없이 높게(+50%), 손절선은 없음(0f)
            float aiTarget = crazyPos == TradingController.PositionType.Long ? startPrice * 1.5f : startPrice * 0.5f;

            bool opened = tradingController.OpenPosition(crazyPos, margin, leverage, aiTarget, 0f);
            if (opened)
            {
                TriggerDialogueWithCategory(FXOverdose.AI.LLM.EventCategory.PositionOpened, $"[OVERDOSE 뇌동매매] {crazyPos} 125배 풀레버리지 올인 (목표가 ${aiTarget:N0})", -0.3f);
                OnSignalEvaluationCompleted?.Invoke(signal, true);
            }
            else
            {
                OnSignalEvaluationCompleted?.Invoke(signal, false);
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
                    TriggerDialogueWithCategory(FXOverdose.AI.LLM.EventCategory.PositionClosed, $"[단타 손절] 휩소 손절 (ROE {roe:0.0}%, PnL ${pnl:N0})", -0.05f);
                }
                else
                {
                    TriggerDialogueWithCategory(FXOverdose.AI.LLM.EventCategory.PositionClosed, $"[대형 손실] 손절 충격 (ROE {roe:0.0}%, PnL ${pnl:N0})", -0.2f);
                }
            }
            else
            {
                TriggerDialogueWithCategory(FXOverdose.AI.LLM.EventCategory.PositionClosed, $"[익절 성공] 수익 달성 (ROE {roe:+0.0}%, PnL +${pnl:N0})", 0.15f);
            }
        }

        // 강제 청산(Liquidation) 시 극도의 멘탈 붕괴 리액션
        private void HandlePositionLiquidated()
        {
            isProcessingSignal = false;
            TriggerDialogueWithCategory(FXOverdose.AI.LLM.EventCategory.PositionClosed, "[강제청산 대참사] 증거금 100% 강제 청산 소진", -0.5f);
        }

        private FXOverdose.AI.LLM.LocalLLMService llmService;

        private void TriggerDialogue(string dialogue, float emotionDelta)
        {
            TriggerDialogueWithCategory(FXOverdose.AI.LLM.EventCategory.General, dialogue, emotionDelta);
        }

        private void TriggerDialogueWithCategory(FXOverdose.AI.LLM.EventCategory category, string dialogue, float emotionDelta)
        {
            lastDecisionLog = dialogue;
            Debug.Log($"[AITradingBrain 💬] ({category}) {dialogue}");
            if (traderStatus != null && Mathf.Abs(emotionDelta) > 0.001f)
            {
                traderStatus.ModifyMentalState(emotionDelta * 10f);
            }

            if (llmService == null) llmService = FXOverdose.AI.LLM.LocalLLMService.Instance;
            if (llmService != null)
            {
                llmService.RequestDialogue(category, dialogue);
            }
            else
            {
                OnAIDecisionMade?.Invoke(dialogue, emotionDelta);
            }
        }
    }
}
