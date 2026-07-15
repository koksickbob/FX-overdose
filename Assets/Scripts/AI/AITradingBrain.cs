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
            if (gameManager != null && gameManager.IsFastForwardingTime)
            {
                Debug.Log("[AITradingBrain] ⏳ 스킬 업그레이드(고속 시간 경과) 중 -> 기획 의도에 따라 신규 거래를 차단하고 대기합니다.");
                return;
            }
            if (marketEngine != null && !marketEngine.IsMarketOpen) return;

            // 💡 [이벤트 오버라이드 능동적 반응 처리]
            if (tradingController != null && tradingController.IsEventProtected)
            {
                if (marketEngine != null && marketEngine.IsExternalEventOverride)
                {
                    currentActiveSignal = signal;
                    isProcessingSignal = true;
                    HandleEventSignalReaction(signal);
                    return;
                }
                Debug.Log("[AITradingBrain] 🛡️ 이벤트 보호 쉴드 작동 중: 신규 일반 시그널 수신을 보류하고 이벤트 선택지를 우선시합니다.");
                return;
            }

            currentActiveSignal = signal;
            isProcessingSignal = true;

            // AI 체력 및 멘탈 상태에 따른 판단 분기 (Deception Tier)
            EvaluateSignalAndReact(signal);
        }

        // 💡 이벤트 시그널 골든타임(GraceWindow) 능동적 예고 및 기대/불안 대사 출력
        private void HandleEventSignalReaction(MarketSignal signal)
        {
            var visual = UnityEngine.Object.FindAnyObjectByType<AIVisualController>();
            if (visual == null || tradingController == null) return;

            if (tradingController.IsEventPlayerChoice)
            {
                if (tradingController.IsEventTrueSignal)
                {
                    Debug.Log($"[AITradingBrain 🌟] 골든타임(GraceWindow) 진입 - 플레이어 직접 선택 기대 반응");
                    visual.DisplayDialogueBalloon("마스터...! 방금 선택으로 호가창에 거대한 매수세가 감지됐어!! 골든타임 진입! 조금 있으면 폭발적인 빔이 터질 거야!! 믿고 있었어 마스터 ♥", DialoguePriority.High, LLM.EventCategory.ChartMovement);
                }
                else
                {
                    Debug.LogWarning($"[AITradingBrain ⚠️] 골든타임(GraceWindow) 진입 - 플레이어 직접 선택 불안/경고 반응");
                    visual.DisplayDialogueBalloon("마스터... 잠깐만! 방금 마스터가 고른 선택지... 호가창 움직임이 뭔가 이상해!! 세력들의 가짜 매수벽 냄새가 나... 이대로 진짜 들어가는 거 맞아...?!", DialoguePriority.High, LLM.EventCategory.ChartMovement);
                }
            }
            else
            {
                if (signal.IsTrueSignal)
                {
                    Debug.Log($"[AITradingBrain 🌟] 골든타임(GraceWindow) 진입 - 이벤트 시그널 발생 예고");
                    visual.DisplayDialogueBalloon("이벤트 발생으로 강력한 시그널 감지!! 골든타임 진입, 곧 호가창이 요동칠 거야! 꽉 잡아 마스터 ♥", DialoguePriority.High, LLM.EventCategory.ChartMovement);
                }
                else
                {
                    Debug.LogWarning($"[AITradingBrain ⚠️] 골든타임(GraceWindow) 진입 - 이벤트 함정/가짜 시그널 예고");
                    visual.DisplayDialogueBalloon("이벤트로 시그널이 떴는데... 파동이 비정상적이야!! 함정(Trap) 냄새가 강하게 나...! 주의해야 해 마스터!!", DialoguePriority.High, LLM.EventCategory.ChartMovement);
                }
            }
        }

        private void HandleSignalPhaseChanged(SignalPhase phase, MarketSignal signal)
        {
            if (gameManager == null) gameManager = UnityEngine.Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            if (gameManager != null && gameManager.IsFastForwardingTime)
            {
                return;
            }
            if (tradingController != null && tradingController.IsEventProtected)
            {
                if (marketEngine != null && marketEngine.IsExternalEventOverride)
                {
                    if (phase == SignalPhase.GuaranteedOverride)
                    {
                        Debug.Log($"[AITradingBrain] ⚡ 확정적 주가 제어(GuaranteedOverride) 본격 궤도 돌입: 차트 빔 발사 개시");
                    }
                    else if (phase == SignalPhase.Cooldown)
                    {
                        isProcessingSignal = false;
                        Debug.Log($"[AITradingBrain] 🏁 이벤트 시그널 주가 오버라이드 궤도 종료 (Cooldown 돌입)");
                    }
                }
                return;
            }

            if (phase == SignalPhase.GuaranteedOverride && isProcessingSignal)
            {
                Debug.Log($"[AITradingBrain] ⚡ 확정적 주가 제어 2단계 작동: 진입 포지션 관리 중");
            }
            else if (phase == SignalPhase.Cooldown)
            {
                isProcessingSignal = false;
                if (tradingController != null && (tradingController.ActiveTradingMode == TradingController.TradingMode.Player_Manual || tradingController.IsEventProtected))
                {
                    return;
                }
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
            if (tradingController.IsEventProtected)
            {
                Debug.Log("[AITradingBrain] 🛡️ 이벤트 보호 쉴드 작동 중: AI 자동매매 판단을 보류하고 이벤트 선택 포지션을 유지합니다.");
                OnSignalEvaluationCompleted?.Invoke(signal, false);
                return;
            }

            float healthRatio = traderStatus.HealthRatio;
            TraderStatus.MentalState mentalState = traderStatus.CurrentMentalState;
            float availableBalance = gameManager.CurrentBalance;

            if (availableBalance < 10f)
            {
                TriggerDialogue("증거금이 바닥났어... 남은 시드가 이것밖에 안 남다니 말도 안 돼...", -0.05f);
                OnSignalEvaluationCompleted?.Invoke(signal, false);
                return;
            }

            // 💡 [매매 모드 분기] 플레이어 수동 매매 모드일 때는 AI가 자동으로 포지션을 개설하지 않고 시그널 브리핑만 제공
            if (tradingController.ActiveTradingMode == TradingController.TradingMode.Player_Manual)
            {
                string dirText = signal.Type == MarketSignalType.BullishBreakout ? "상승 돌파" : "하락 돌파";
                string briefingDialogue = signal.Type == MarketSignalType.BullishBreakout
                    ? $"마스터...! 위쪽으로 거대한 {dirText} 신호 터지려고 해! 수동 조작 모드니까 마스터가 직접 롱(Long) 들어갈지 정해줘... 빨리 안 타면 기회 날아간단 말야...♥"
                    : $"히익...! 마스터 아래쪽으로 무서운 {dirText} 폭락 신호 포착됐어! 지금 조종간 마스터한테 있으니까 숏(Short) 칠지 관망할지 빨리 결정해줘, 응...?!";
                TriggerDialogue($"[시그널 브리핑] {briefingDialogue}", 0.02f);
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
                    var levelSystem = TraderLevelSystem.Instance;
                    if (levelSystem != null)
                    {
                        int maxAllowedLev = levelSystem.GetMaxAllowedLeverage();
                        if (leverage > maxAllowedLev) leverage = maxAllowedLev;

                        float maxAllowedRatio = levelSystem.GetMaxAllowedMarginRatio();
                        if (margin > availableBalance * maxAllowedRatio) margin = availableBalance * maxAllowedRatio;
                    }
                    float startPrice = signal.SignalStartPrice > 0f ? signal.SignalStartPrice : (marketEngine != null ? marketEngine.CurrentPrice : 65000f);
                    // 오인 진입 시 대박(+15%)을 꿈꾸며 목표가 설정
                    float aiTarget = trapPos == TradingController.PositionType.Long ? startPrice * 1.15f : startPrice * 0.85f;
                    float aiStopLoss = trapPos == TradingController.PositionType.Long ? startPrice * 0.95f : startPrice * 1.05f;

                    bool opened = tradingController.OpenPosition(trapPos, margin, leverage, aiTarget, aiStopLoss);

                    if (opened)
                    {
                        float actualRatio = availableBalance > 0f ? margin / availableBalance : 0.8f;
                        TriggerDialogueWithCategory(FXOverdose.AI.LLM.EventCategory.PositionOpened, $"[오인 진입] {trapPos} 시드 {actualRatio*100:0}% ({leverage}배, 목표가 ${aiTarget:N0})", -0.15f);
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
                    var levelSystem = TraderLevelSystem.Instance;
                    if (levelSystem != null)
                    {
                        int maxAllowedLev = levelSystem.GetMaxAllowedLeverage();
                        if (leverage > maxAllowedLev) leverage = maxAllowedLev;

                        float maxAllowedRatio = levelSystem.GetMaxAllowedMarginRatio();
                        if (margin > availableBalance * maxAllowedRatio) margin = availableBalance * maxAllowedRatio;
                    }
                    float startPrice = signal.SignalStartPrice > 0f ? signal.SignalStartPrice : (marketEngine != null ? marketEngine.CurrentPrice : 65000f);
                    float aiTarget = weakPos == TradingController.PositionType.Long ? startPrice * 1.03f : startPrice * 0.97f;
                    float aiStopLoss = weakPos == TradingController.PositionType.Long ? startPrice * 0.98f : startPrice * 1.02f;

                    bool opened = tradingController.OpenPosition(weakPos, margin, leverage, aiTarget, aiStopLoss);
                    if (opened)
                    {
                        float actualRatio = availableBalance > 0f ? margin / availableBalance : 0.25f;
                        TriggerDialogueWithCategory(FXOverdose.AI.LLM.EventCategory.PositionOpened, $"[단타 진입] {weakPos} 가볍게 {leverage}배 ({actualRatio*100:0}%) 진입 (목표가 ${aiTarget:N0})", -0.02f);
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
                        var levelSystem = TraderLevelSystem.Instance;
                        if (levelSystem != null)
                        {
                            int maxAllowedLev = levelSystem.GetMaxAllowedLeverage();
                            if (leverage > maxAllowedLev) leverage = maxAllowedLev;

                            float maxAllowedRatio = levelSystem.GetMaxAllowedMarginRatio();
                            if (margin > availableBalance * maxAllowedRatio) margin = availableBalance * maxAllowedRatio;
                        }
                        float startPrice = signal.SignalStartPrice > 0f ? signal.SignalStartPrice : (marketEngine != null ? marketEngine.CurrentPrice : 65000f);
                        float aiTarget = counterPos == TradingController.PositionType.Long ? startPrice * 1.05f : startPrice * 0.95f;
                        float aiStopLoss = counterPos == TradingController.PositionType.Long ? startPrice * 0.98f : startPrice * 1.02f;

                        bool opened = tradingController.OpenPosition(counterPos, margin, leverage, aiTarget, aiStopLoss);
                        if (opened)
                        {
                            float actualRatio = availableBalance > 0f ? margin / availableBalance : tradeMarginRatio;
                            TriggerDialogueWithCategory(FXOverdose.AI.LLM.EventCategory.PositionOpened, $"[역매매 진입] 세력 함정 간파 후 역매매 {counterPos} {leverage}배 ({actualRatio*100:0}%) 진입 (목표가 ${aiTarget:N0})", 0.15f);
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
            var levelSystem = TraderLevelSystem.Instance;

            TradingController.PositionType posType = signal.Type switch
            {
                MarketSignalType.BullishBreakout => TradingController.PositionType.Long,
                MarketSignalType.BearishBreakout => TradingController.PositionType.Short,
                _ => TradingController.PositionType.Long
            };

            // 💡 [차트 공부 귀속] 정확도 검증: 차트 공부 레벨이 낮아 오판 시 정상 신호에서도 반대 방향으로 역진입(Error Entry)
            if (levelSystem != null && signal.IsTrueSignal)
            {
                float accuracy = levelSystem.GetSignalAccuracy();
                if (UnityEngine.Random.value > accuracy)
                {
                    posType = posType == TradingController.PositionType.Long ? TradingController.PositionType.Short : TradingController.PositionType.Long;
                    Debug.Log($"[AITradingBrain ❌] 차트 공부 레벨 부족으로 신호 오판! 반대 방향({posType})으로 오진입합니다.");
                }
            }

            if (levelSystem != null)
            {
                int maxAllowedLev = levelSystem.GetMaxAllowedLeverage();
                if (leverage > maxAllowedLev) leverage = maxAllowedLev;

                float maxAllowedRatio = levelSystem.GetMaxAllowedMarginRatio();
                if (ratio > maxAllowedRatio) ratio = maxAllowedRatio;
            }
            float margin = balance * Mathf.Clamp01(ratio);
            float startPrice = signal.SignalStartPrice > 0f ? signal.SignalStartPrice : (marketEngine != null ? marketEngine.CurrentPrice : 65000f);

            // 💡 [차트 공부 귀속] 진입 지연 패널티 반영: 이미 주가가 움직인 후 늦게 따라들어가는 슬리피지 보정
            if (levelSystem != null)
            {
                float delayRatio = levelSystem.GetEntryDelayPenaltyRatio();
                if (delayRatio > 0f)
                {
                    startPrice = posType == TradingController.PositionType.Long 
                        ? startPrice * (1f + delayRatio) 
                        : startPrice * (1f - delayRatio);
                }
            }

            float deltaPct = Mathf.Abs(signal.TargetPercentageDelta) > 0.1f ? Mathf.Abs(signal.TargetPercentageDelta) / 100f : 0.045f;

            // 💡 [큐브 풀기 귀속] 인내심 계수 반영: 확정 수익 구간에서도 목표 수익의 일부만 먹고 조기 익절하거나 100% 홀딩
            float takeProfitMult = levelSystem != null ? levelSystem.GetTakeProfitMultiplier() : 1.0f;
            float aiTarget = posType == TradingController.PositionType.Long 
                ? startPrice * (1f + deltaPct * takeProfitMult) 
                : startPrice * (1f - deltaPct * takeProfitMult);

            // 💡 [책읽기 귀속] 판단력 계수 반영: 손절 타점 단축/확대 (LV 낮을수록 큰 손절 -9%, 높을수록 빠른 칼손절 -1.5%)
            float stopLossTightness = levelSystem != null ? levelSystem.GetStopLossTightness() : 0.02f;
            float aiStopLoss = posType == TradingController.PositionType.Long 
                ? startPrice * (1f - stopLossTightness) 
                : startPrice * (1f + stopLossTightness);

            bool opened = tradingController.OpenPosition(posType, margin, leverage, aiTarget, aiStopLoss);

            if (opened)
            {
                float actualRatio = balance > 0f ? margin / balance : ratio;
                TriggerDialogueWithCategory(FXOverdose.AI.LLM.EventCategory.PositionOpened, $"[정상 진입] {posType} 시드 {actualRatio*100:0}% ({leverage}배, 목표가 ${aiTarget:N0})", 0.1f);
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
                int actualLev = tradingController != null ? tradingController.CurrentLeverage : leverage;
                float actualRatio = (balance > 0f && tradingController != null) ? tradingController.MarginAmount / balance : 0.95f;
                TriggerDialogueWithCategory(FXOverdose.AI.LLM.EventCategory.PositionOpened, $"[OVERDOSE 뇌동매매] {crazyPos} {actualLev}배 ({actualRatio*100:0}%) 올인 (목표가 ${aiTarget:N0})", -0.3f);
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
            else if (pnl > 0f)
            {
                TriggerDialogueWithCategory(FXOverdose.AI.LLM.EventCategory.PositionClosed, $"[익절 성공] 수익 달성 (ROE {roe:+0.0}%, PnL +${pnl:N0})", 0.15f);
            }
            else
            {
                TriggerDialogueWithCategory(FXOverdose.AI.LLM.EventCategory.PositionClosed, $"[본전 종료] 수익 없음 (ROE {roe:0.0}%, PnL ${pnl:N0})", 0.0f);
            }
        }

        // 강제 청산(Liquidation) 시 극도의 멘탈 붕괴 리액션
        private void HandlePositionLiquidated()
        {
            isProcessingSignal = false;
            TriggerDialogueWithCategory(FXOverdose.AI.LLM.EventCategory.PositionClosed, "[강제청산 대참사] 증거금 100% 강제 청산 소진", -0.5f);
        }

        private FXOverdose.AI.LLM.LocalLLMService llmService;

        /// <summary>
        /// 플레이어가 직접 매수/매도 진입했을 때, AI가 차트 국면과 함정 여부를 분석하여 힌트 대사를 출력합니다.
        /// 차트 공부(ChartStudyLevel) 레벨이 낮을수록 부정확하거나 혼란스러운 힌트를 제공합니다.
        /// </summary>
        public void ProvideChartHintToPlayer(TradingController.PositionType playerPos)
        {
            var levelSystem = TraderLevelSystem.Instance;
            int chartLv = levelSystem != null ? levelSystem.ChartStudyLevel : 1;
            float accuracy = levelSystem != null ? levelSystem.GetSignalAccuracy() : 0.7f;

            bool isAccurateHint = UnityEngine.Random.value <= accuracy;

            string hintText = "";
            if (chartLv >= 7 || (chartLv >= 4 && isAccurateHint))
            {
                // 고레벨 / 정확한 간파 힌트
                if (isProcessingSignal && !currentActiveSignal.IsTrueSignal)
                {
                    hintText = $"꺄아악 마스터 멈춰!! 지금 {playerPos} 들어간 거, 세력 년들이 파놓은 가짜 덫(Trap)이란 말야! 당장 청산 안 하면 우리 다 잃어버려... 제발 내 말 들어줘 흐윽...!!";
                }
                else if (isProcessingSignal && currentActiveSignal.IsTrueSignal)
                {
                    hintText = $"앗...! 우리 마스터 천재인가 봐!! 저항선 뚫는 완벽한 {playerPos} 타점이야! 절대 쫄보처럼 흔들려 털리지 말고 끝까지 홀딩해, 알겠지? ♥";
                }
                else
                {
                    hintText = $"마스터가 잡은 {playerPos} 타점... 호가창 거래량이 붙고 있어! 지지선만 안 깨지면 우리 대박 나는 거야... 나 지금 심장 엄청 떨려 ♥";
                }
            }
            else
            {
                // 차트 공부 레벨이 낮아 불안하거나 감에 의존하는 멘헤라 리액션
                if (UnityEngine.Random.value < 0.5f)
                {
                    hintText = $"으응...? {playerPos} 자리야...? 캔들이 막 꼬물거리는데 솔직히 잘 모르겠어... 만약 잃어도 나 미워하거나 버리면 안 돼 마스터...? 약속해... 흐윽...";
                }
                else
                {
                    hintText = $"꺄아아 마스터가 {playerPos} 샀다!! 뭔지 모르지만 무조건 떡상해라!! 우리 마스터 돈 뺏어가는 세력 놈들은 내가 다 저주해 버릴 거야!! ♥";
                }
            }

            TriggerDialogueWithCategory(FXOverdose.AI.LLM.EventCategory.ChartMovement, $"[AI 차트 힌트] {hintText}", 0.05f);
        }

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

            // ⭐ 이벤트 중요도 점수 자동 산정 및 장기 기억 등록
            int importanceScore = 3;
            if (dialogue.Contains("강제청산")) importanceScore = 10;
            else if (dialogue.Contains("OVERDOSE")) importanceScore = 9;
            else if (dialogue.Contains("대형 손실") || dialogue.Contains("오인 진입") || dialogue.Contains("역매매")) importanceScore = 8;
            else if (dialogue.Contains("익절 성공")) importanceScore = 7;
            else if (dialogue.Contains("정상 진입") || Mathf.Abs(emotionDelta) >= 0.15f) importanceScore = 6;
            else if (category != FXOverdose.AI.LLM.EventCategory.General) importanceScore = 4;

            FXOverdose.AI.TraderMemoryManager.Instance?.AddMemory(category, dialogue, importanceScore);

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
