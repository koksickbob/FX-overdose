using System;
using UnityEngine;
using FXOverdose.Trading;
using FXOverdose.Core;

namespace FXOverdose.AI
{
    /// <summary>
    /// 요미의 자동 매매 판단 엔진입니다. 시장 신호를 규칙 기반으로 평가해 진입/청산을 실행합니다.
    /// 이름과 달리 LLM을 사용하지 않습니다(사용한 적도 없습니다). 삭제하면 자동 매매,
    /// FOMO 후회 기믹(OnSignalEvaluationCompleted 구독), 고배율 중독 폭주(ForceNextTradeHighLeverage),
    /// 차트 힌트가 함께 죽으므로 "LLM 잔재"로 오인해 제거하지 마십시오.
    /// </summary>
    public class AITradingBrain : MonoBehaviour
    {
        [Header("시스템 연결")]
        [SerializeField] private MarketSimulationEngine marketEngine;
        [SerializeField] private TradingController tradingController;
        [SerializeField] private TraderStatus traderStatus;
        [SerializeField] private GameManager gameManager;

        [Header("AI 판단 매개변수")]
        [SerializeField] [Range(1, 125)] private int defaultLeverage = 10;
        [SerializeField] [Range(0.1f, 0.9f)] private float tradeMarginRatio = 0.20f; // 1회 진입 시 시드 투자 비율

        [Header("현재 AI 판단 상태 (읽기 전용)")]
        [SerializeField] private bool isProcessingSignal = false;
        [SerializeField] private MarketSignal currentActiveSignal;
        [SerializeField] private string lastDecisionLog = "";

        public event Action<MarketSignal, bool> OnSignalEvaluationCompleted; // (신호, 진입여부)

        public string LastDecisionLog => lastDecisionLog;
        
        public bool ForceNextTradeHighLeverage = false;

        private ITraderLevelProvider levelProvider;
        public bool IsBossAI { get; set; } = false;
        
        public void SetLevelProvider(ITraderLevelProvider provider)
        {
            levelProvider = provider;
        }

        public Func<TradingController.PositionType, float, int, float, float, bool, float, bool> TradeExecutor;
        public Func<float> GetAvailableBalance;

        private void Start()
        {
            if (TradeExecutor == null) TradeExecutor = (pos, margin, lev, tgt, sl, crazy, price) => tradingController != null && tradingController.OpenPosition(pos, margin, lev, tgt, sl, crazy, price);
            if (GetAvailableBalance == null) GetAvailableBalance = () => gameManager != null ? gameManager.CurrentBalance : 7000f;

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
            if (!IsBossAI)
            {
                MentalDrainGimmickController drainController = FindAnyObjectByType<MentalDrainGimmickController>();
                if (drainController == null)
                {
                    drainController = gameObject.AddComponent<MentalDrainGimmickController>();
                }
                drainController.Initialize(traderStatus, tradingController, marketEngine);
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
            if (gameManager == null) gameManager = UnityEngine.Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            if (marketEngine == null) marketEngine = UnityEngine.Object.FindAnyObjectByType<MarketSimulationEngine>(FindObjectsInactive.Include);

            if (gameManager != null && gameManager.CurrentState != GameManager.GameState.Playing) return;
            if (gameManager != null && gameManager.IsFastForwardingTime)
            {
                Debug.Log("[AITradingBrain] ⏳ 스킬 업그레이드(고속 시간 경과) 중 -> 기획 의도에 따라 신규 거래를 차단하고 대기합니다.");
                return;
            }
            if (marketEngine != null && !marketEngine.IsMarketOpen) return;

            // 튜토리얼 진행 중에는 요미가 직접 거래하는 단계(Step4)를 제외하고는 거래를 진행하지 않음
            if (FXOverdose.Core.TutorialManager.Instance != null && !FXOverdose.Core.TutorialManager.Instance.AllowAITrading)
            {
                return;
            }

            // 💡 [이벤트/Overdose 오버라이드 능동적 반응 처리 및 방해 차단]
            if (!IsBossAI && tradingController != null && (tradingController.IsEventProtected || tradingController.IsOverdoseTradeActive))
            {
                if (tradingController.IsOverdoseTradeActive)
                {
                    return;
                }
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

            var matcher = FXOverdose.AI.Dialogue.YomiDialogueMatcher.Instance;

            if (tradingController.IsEventPlayerChoice)
            {
                if (tradingController.IsEventTrueSignal)
                {
                    Debug.Log($"[AITradingBrain 🌟] 골든타임(GraceWindow) 진입 - 플레이어 직접 선택 기대 반응");
                    string dbText = matcher?.GetEventDialogue("EventSignal_PlayerTrue");
                    visual.DisplayDialogueBalloon(!string.IsNullOrEmpty(dbText) ? dbText : "오빠...! 방금 선택으로 호가창에 거대한 매수세가 감지됐어!! 골든타임 진입! 조금 있으면 폭발적인 빔이 터질 거야!! 믿고 있었어 오빠 ♥", DialoguePriority.High, EventCategory.ChartMovement);
                }
                else
                {
                    Debug.LogWarning($"[AITradingBrain ⚠️] 골든타임(GraceWindow) 진입 - 플레이어 직접 선택 불안/경고 반응");
                    string dbText = matcher?.GetEventDialogue("EventSignal_PlayerFalse");
                    visual.DisplayDialogueBalloon(!string.IsNullOrEmpty(dbText) ? dbText : "오빠... 잠깐만! 방금 오빠가 고른 선택지... 호가창 움직임이 뭔가 이상해!! 세력들의 가짜 매수벽 냄새가 나... 이대로 진짜 들어가는 거 맞아...?!", DialoguePriority.High, EventCategory.ChartMovement);
                }
            }
            else
            {
                if (signal.IsTrueSignal)
                {
                    Debug.Log($"[AITradingBrain 🌟] 골든타임(GraceWindow) 진입 - 이벤트 시그널 발생 예고");
                    string dbText = matcher?.GetEventDialogue("EventSignal_AITrue");
                    visual.DisplayDialogueBalloon(!string.IsNullOrEmpty(dbText) ? dbText : "이벤트 발생으로 강력한 시그널 감지!! 골든타임 진입, 곧 호가창이 요동칠 거야! 꽉 잡아 오빠 ♥", DialoguePriority.High, EventCategory.ChartMovement);
                }
                else
                {
                    Debug.LogWarning($"[AITradingBrain ⚠️] 골든타임(GraceWindow) 진입 - 이벤트 함정/가짜 시그널 예고");
                    string dbText = matcher?.GetEventDialogue("EventSignal_AIFalse");
                    visual.DisplayDialogueBalloon(!string.IsNullOrEmpty(dbText) ? dbText : "이벤트로 시그널이 떴는데... 파동이 비정상적이야!! 함정(Trap) 냄새가 강하게 나...! 주의해야 해 오빠!!", DialoguePriority.High, EventCategory.ChartMovement);
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
            if (!IsBossAI && tradingController != null && tradingController.IsEventProtected)
            {
                if (marketEngine != null && marketEngine.IsExternalEventOverride)
                {
                    if (phase == SignalPhase.GuaranteedOverride)
                    {
                        Debug.Log($"[AITradingBrain] ⚡ 확정적 주가 제어(GuaranteedOverride) 본격 궤도 돌입: 차트 빔 발사 개시");
                    }
                    else if (phase == SignalPhase.Cooldown || phase == SignalPhase.None)
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
            else if (phase == SignalPhase.Cooldown || phase == SignalPhase.None)
            {
                isProcessingSignal = false;
                if (tradingController != null && (!IsBossAI && (tradingController.ActiveTradingMode == TradingController.TradingMode.Player_Manual || tradingController.IsEventProtected || tradingController.IsOverdoseTradeActive)))
                {
                    return;
                }
                // [게임적 허용 완벽 보장 장치 (2중 보장)]
                // 만약 True Signal(정상 확정 신호)을 따라 진입한 포지션이 캔들 노이즈 등으로 인해 
                // TargetPrice에 미세하게 닿지 못한 채 보장 구간(GuaranteedOverride)이 종료되더라도,
                // 기획상 설계된 상승/하락 수익률을 확실하게 보장받기 위해 Cooldown 돌입 즉시 자동 익절 청산!
                if (!IsBossAI && tradingController != null && tradingController.CurrentPosition != TradingController.PositionType.None)
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
            if (!IsBossAI && (tradingController.IsEventProtected || tradingController.IsOverdoseTradeActive))
            {
                Debug.Log("[AITradingBrain] 🛡️ 이벤트/Overdose 보호 쉴드 작동 중: AI 자동매매 판단을 보류합니다.");
                OnSignalEvaluationCompleted?.Invoke(signal, false);
                return;
            }

            float healthRatio = IsBossAI ? 1.0f : traderStatus.HealthRatio;
            TraderStatus.MentalState mentalState = IsBossAI ? TraderStatus.MentalState.Stable : traderStatus.CurrentMentalState;
            float availableBalance = GetAvailableBalance();

            if (availableBalance < 10f)
            {

                OnSignalEvaluationCompleted?.Invoke(signal, false);
                return;
            }

            // 챌린지에서는 이전 프레임에 예약된 강제 AI 매매까지 폐기합니다.
            if (SaveLoadManager.Instance != null && !SaveLoadManager.Instance.AllowsAITrading)
            {
                ForceNextTradeHighLeverage = false;
            }

            // 💡 [고배율 중독 강제 매매] 요미가 주도권을 뺏고 강제로 고배율 매매를 실행하는 상태
            if (ForceNextTradeHighLeverage)
            {
                ForceNextTradeHighLeverage = false;
                tradingController.UnlockManualMode(); // 포지션 진입을 시도하므로 수동 전환 잠금 해제

                var levelSys = levelProvider ?? TraderLevelSystem.Instance;
                int maxLev = levelSys != null ? levelSys.GetMaxAllowedLeverage() : 125;
                int forceLev = Mathf.Max(50, maxLev); // 최소 50배 이상 고배율
                

                
                // 정상적인 매매(요미 스킬 및 레벨 스탯 반영)처럼 진입
                OpenNormalPosition(signal, availableBalance, tradeMarginRatio, forceLev);
                return;
            }

            // 💡 [매매 모드 분기] 플레이어 수동 매매 모드일 때는 AI가 자동으로 포지션을 개설하지 않고 시그널 브리핑만 제공
            if (!IsBossAI && tradingController.ActiveTradingMode == TradingController.TradingMode.Player_Manual)
            {
                OnSignalEvaluationCompleted?.Invoke(signal, false);
                return;
            }

            TradingController.AITradingStyle aiStyle = tradingController != null ? tradingController.CurrentAITradingStyle : TradingController.AITradingStyle.Balanced;

            // 🔴 Tier 4: Overdose / 통제 불능 상태 (체력 <= 15% 또는 Danger/Overdose)
            if (healthRatio <= 0.15f || mentalState == TraderStatus.MentalState.Danger || mentalState == TraderStatus.MentalState.Overdose)
            {
                ExecuteOverdoseTrade(signal, availableBalance);
                return;
            }

            // 🟠 Tier 3: 심각한 피로/오인 상태 (Heavily Deceived, 체력 15% ~ 40%)
            if (healthRatio > 0.15f && healthRatio <= 0.40f)
            {
                // 강한 가짜 신호(불트랩/베어트랩)를 진짜 대박 자리로 오인하여 진입!
                if (signal.Strength == SignalStrength.Strong && !signal.IsTrueSignal)
                {
                    if (aiStyle == TradingController.AITradingStyle.Safe)
                    {
                         Debug.Log("[AITradingBrain] 🟢 안전(Safe) 모드: 함정을 의심하여 진입을 회피합니다.");
                         OnSignalEvaluationCompleted?.Invoke(signal, false);
                         return;
                    }

                    TradingController.PositionType trapPos = signal.Type switch
                    {
                        MarketSignalType.BullTrap => TradingController.PositionType.Long,
                        MarketSignalType.BearTrap => TradingController.PositionType.Short,
                        _ => TradingController.PositionType.Long
                    };

                    ITraderLevelProvider levelSystem = levelProvider ?? TraderLevelSystem.Instance;
                    float marginRatio = levelSystem != null ? levelSystem.GetStopLossTightness() * 10f : 0.8f;
                    if (aiStyle == TradingController.AITradingStyle.Aggressive) marginRatio = 1.0f;

                    float margin = availableBalance * marginRatio; // 기계적 손절비율 * 10배 (최대 90% ~ 최소 15%)
                    
                    int leverage = defaultLeverage * 3;
                    if (aiStyle == TradingController.AITradingStyle.Aggressive) leverage = 125;

                    if (levelSystem != null)
                    {
                        // 💡 [대박 오인] 이성을 잃고 함정에 개방된 최대 레버리지 쏟아부음
                        leverage = levelSystem.GetMaxAllowedLeverage();
                        float maxAllowedRatio = levelSystem.GetMaxAllowedMarginRatio();
                        if (margin > availableBalance * maxAllowedRatio) margin = availableBalance * maxAllowedRatio;
                    }
                    float startPrice = signal.SignalStartPrice > 0f ? signal.SignalStartPrice : (marketEngine != null ? marketEngine.CurrentPrice : 65000f);
                    
                    // 오인 진입 시 대박(+15%)을 꿈꾸며 목표가 설정
                    float aiTarget = trapPos == TradingController.PositionType.Long ? startPrice * 1.15f : startPrice * 0.85f;
                    float aiStopLoss = trapPos == TradingController.PositionType.Long ? startPrice * 0.95f : startPrice * 1.05f;

                    if (aiStyle == TradingController.AITradingStyle.Aggressive)
                    {
                        aiTarget = trapPos == TradingController.PositionType.Long ? startPrice * 1.30f : startPrice * 0.70f;
                        aiStopLoss = 0f; // 노손절
                    }

                    bool opened = TradeExecutor(trapPos, margin, leverage, aiTarget, aiStopLoss, false, startPrice);

                    if (opened)
                    {
                        OnSignalEvaluationCompleted?.Invoke(signal, true);
                    }
                    else
                    {
                        OnSignalEvaluationCompleted?.Invoke(signal, false);
                    }
                }
                else
                {
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
                    if (aiStyle == TradingController.AITradingStyle.Safe)
                    {
                         Debug.Log("[AITradingBrain] 🟢 안전(Safe) 모드: 약한 신호는 관망합니다.");
                         OnSignalEvaluationCompleted?.Invoke(signal, false);
                         return;
                    }

                    TradingController.PositionType weakPos = signal.Type switch
                    {
                        MarketSignalType.BullishBreakout => TradingController.PositionType.Long,
                        MarketSignalType.BearishBreakout => TradingController.PositionType.Short,
                        MarketSignalType.BullTrap => TradingController.PositionType.Long,
                        MarketSignalType.BearTrap => TradingController.PositionType.Short,
                        _ => TradingController.PositionType.Long
                    };

                    float marginRatio = 0.15f;
                    if (aiStyle == TradingController.AITradingStyle.Aggressive) marginRatio = 0.40f;
                    float margin = availableBalance * marginRatio;
                    
                    int leverage = defaultLeverage;
                    if (aiStyle == TradingController.AITradingStyle.Aggressive) leverage = defaultLeverage * 3;

                    ITraderLevelProvider levelSystem = levelProvider ?? TraderLevelSystem.Instance;
                    if (levelSystem != null)
                    {
                        int maxAllowedLev = levelSystem.GetMaxAllowedLeverage();
                        leverage = Mathf.Min(maxAllowedLev, Mathf.Max(leverage, 20));

                        float maxAllowedRatio = levelSystem.GetMaxAllowedMarginRatio();
                        if (margin > availableBalance * maxAllowedRatio) margin = availableBalance * maxAllowedRatio;
                    }
                    float startPrice = signal.SignalStartPrice > 0f ? signal.SignalStartPrice : (marketEngine != null ? marketEngine.CurrentPrice : 65000f);
                    float aiTarget = weakPos == TradingController.PositionType.Long ? startPrice * 1.03f : startPrice * 0.97f;
                    float aiStopLoss = weakPos == TradingController.PositionType.Long ? startPrice * 0.98f : startPrice * 1.02f;

                    if (aiStyle == TradingController.AITradingStyle.Aggressive) aiStopLoss = 0f;

                    bool opened = TradeExecutor(weakPos, margin, leverage, aiTarget, aiStopLoss, false, startPrice);
                    if (opened)
                    {
                        OnSignalEvaluationCompleted?.Invoke(signal, true);
                    }
                    else
                    {
                        OnSignalEvaluationCompleted?.Invoke(signal, false);
                    }
                }
                else if (signal.Strength == SignalStrength.Strong && signal.IsTrueSignal)
                {
                    int leverage = defaultLeverage;
                    if (aiStyle == TradingController.AITradingStyle.Safe) leverage = 5;
                    else if (aiStyle == TradingController.AITradingStyle.Aggressive) leverage = 50;

                    ITraderLevelProvider levelSystem = levelProvider ?? TraderLevelSystem.Instance;
                    if (levelSystem != null) 
                    {
                         if (aiStyle == TradingController.AITradingStyle.Balanced) leverage = (int)Mathf.Max(leverage, levelSystem.GetMaxAllowedLeverage() * 0.6f);
                         else if (aiStyle == TradingController.AITradingStyle.Aggressive) leverage = levelSystem.GetMaxAllowedLeverage();
                    }
                    float ratio = tradeMarginRatio;
                    if (aiStyle == TradingController.AITradingStyle.Safe) ratio = 0.15f;
                    else if (aiStyle == TradingController.AITradingStyle.Aggressive) ratio = 0.8f;

                    OpenNormalPosition(signal, availableBalance, ratio, leverage);
                }
                else
                {
                    if (aiStyle == TradingController.AITradingStyle.Safe)
                    {
                         OnSignalEvaluationCompleted?.Invoke(signal, false);
                         return;
                    }

                    ITraderLevelProvider levelSystem = levelProvider ?? TraderLevelSystem.Instance;
                    float trapProb = levelSystem != null ? Mathf.Clamp01((1.0f - levelSystem.GetSignalAccuracy()) * 2f) : 0.6f;
                    if (aiStyle == TradingController.AITradingStyle.Aggressive) trapProb = 1.0f; // 무조건 낚임

                    if (UnityEngine.Random.value < trapProb)
                    {
                        int leverage = defaultLeverage;
                        if (aiStyle == TradingController.AITradingStyle.Aggressive) leverage = 50;

                        if (levelSystem != null) 
                        {
                             if (aiStyle == TradingController.AITradingStyle.Balanced) leverage = (int)Mathf.Max(leverage, levelSystem.GetMaxAllowedLeverage() * 0.6f);
                             else if (aiStyle == TradingController.AITradingStyle.Aggressive) leverage = levelSystem.GetMaxAllowedLeverage();
                        }
                        float ratio = tradeMarginRatio * 0.8f;
                        if (aiStyle == TradingController.AITradingStyle.Aggressive) ratio = 1.0f;

                        OpenNormalPosition(signal, availableBalance, ratio, leverage);
                    }
                    else
                    {
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
                        if (aiStyle == TradingController.AITradingStyle.Safe)
                        {
                             OnSignalEvaluationCompleted?.Invoke(signal, false);
                             return;
                        }

                        int leverage = defaultLeverage;
                        if (aiStyle == TradingController.AITradingStyle.Aggressive) leverage = defaultLeverage * 3;
                        ITraderLevelProvider levelSystem = levelProvider ?? TraderLevelSystem.Instance;
                        if (levelSystem != null) leverage = Mathf.Min(levelSystem.GetMaxAllowedLeverage(), Mathf.Max(leverage, 20));
                        
                        float ratio = tradeMarginRatio * 0.4f;
                        if (aiStyle == TradingController.AITradingStyle.Aggressive) ratio = tradeMarginRatio * 1.5f;

                        OpenNormalPosition(signal, availableBalance, ratio, leverage);
                    }
                    else
                    {
                        OnSignalEvaluationCompleted?.Invoke(signal, false);
                    }
                }
                else if (signal.Strength == SignalStrength.Strong && signal.IsTrueSignal)
                {
                    int leverage = defaultLeverage * 2;
                    if (aiStyle == TradingController.AITradingStyle.Safe) leverage = 5;
                    else if (aiStyle == TradingController.AITradingStyle.Aggressive) leverage = 50;

                    ITraderLevelProvider levelSystem = levelProvider ?? TraderLevelSystem.Instance;
                    if (levelSystem != null) 
                    {
                         if (aiStyle == TradingController.AITradingStyle.Balanced) leverage = (int)Mathf.Max(leverage, levelSystem.GetMaxAllowedLeverage() * 0.7f);
                         else if (aiStyle == TradingController.AITradingStyle.Aggressive) leverage = levelSystem.GetMaxAllowedLeverage();
                    }
                    float ratio = tradeMarginRatio * 1.2f;
                    if (aiStyle == TradingController.AITradingStyle.Safe) ratio = 0.20f;
                    else if (aiStyle == TradingController.AITradingStyle.Aggressive) ratio = 1.0f;

                    OpenNormalPosition(signal, availableBalance, ratio, leverage);
                }
                else if (signal.Strength == SignalStrength.Strong && !signal.IsTrueSignal)
                {
                    if (aiStyle == TradingController.AITradingStyle.Safe)
                    {
                         OnSignalEvaluationCompleted?.Invoke(signal, false);
                         return;
                    }

                    ITraderLevelProvider levelSystem = levelProvider ?? TraderLevelSystem.Instance;
                    
                    if (aiStyle == TradingController.AITradingStyle.Aggressive)
                    {
                         // 공격 모드: 간파 안하고 함정 방향으로 무지성 진입
                         float ratio = 1.0f;
                         int lev = levelSystem != null ? levelSystem.GetMaxAllowedLeverage() : 125;
                         OpenNormalPosition(signal, availableBalance, ratio, lev);
                         return;
                    }

                    float counterTrapProb = levelSystem != null ? levelSystem.GetSignalAccuracy() : 0.75f;
                    if (UnityEngine.Random.value < counterTrapProb)
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
                        if (levelSystem != null)
                        {
                            leverage = levelSystem.GetMaxAllowedLeverage();
                            float maxAllowedRatio = levelSystem.GetMaxAllowedMarginRatio();
                            if (margin > availableBalance * maxAllowedRatio) margin = availableBalance * maxAllowedRatio;
                        }
                        float startPrice = signal.SignalStartPrice > 0f ? signal.SignalStartPrice : (marketEngine != null ? marketEngine.CurrentPrice : 65000f);
                        
                        float deltaPct = Mathf.Abs(signal.TargetPercentageDelta) > 0.1f ? Mathf.Abs(signal.TargetPercentageDelta) / 100f : 0.045f;
                        float takeProfitMult = levelSystem != null ? levelSystem.GetTakeProfitMultiplier() : 1.0f;
                        float delayRatio = levelSystem != null ? levelSystem.GetEntryDelayPenaltyRatio() : 0.0f;
                        
                        float waveSlippage = deltaPct * delayRatio;
                        float targetMove = deltaPct * takeProfitMult;
                        
                        float aiTarget = counterPos == TradingController.PositionType.Long 
                            ? startPrice * (1f + targetMove) 
                            : startPrice * (1f - targetMove);
                        float aiStopLoss = counterPos == TradingController.PositionType.Long ? startPrice * 0.98f : startPrice * 1.02f;

                        float injectedEntryPrice = counterPos == TradingController.PositionType.Long
                            ? startPrice * (1f + waveSlippage)
                            : startPrice * (1f - waveSlippage);

                        bool opened = TradeExecutor(counterPos, margin, leverage, aiTarget, aiStopLoss, false, injectedEntryPrice);
                        if (opened)
                        {
                            OnSignalEvaluationCompleted?.Invoke(signal, true);
                        }
                        else
                        {
                            OnSignalEvaluationCompleted?.Invoke(signal, false);
                        }
                    }
                    else
                    {
                        OnSignalEvaluationCompleted?.Invoke(signal, false);
                    }
                }
                return;
            }
        }

        // 정상/확실한 진입
        private void OpenNormalPosition(MarketSignal signal, float balance, float ratio, int leverage)
        {
            TradingController.AITradingStyle aiStyle = tradingController != null ? tradingController.CurrentAITradingStyle : TradingController.AITradingStyle.Balanced;
            ITraderLevelProvider levelSystem = levelProvider ?? TraderLevelSystem.Instance;

            TradingController.PositionType posType = signal.Type switch
            {
                MarketSignalType.BullishBreakout => TradingController.PositionType.Long,
                MarketSignalType.BearishBreakout => TradingController.PositionType.Short,
                MarketSignalType.BullTrap => TradingController.PositionType.Long,
                MarketSignalType.BearTrap => TradingController.PositionType.Short,
                _ => TradingController.PositionType.Long
            };

            // 💡 [차트 공부 귀속] 정확도 검증: 차트 공부 레벨이 낮아 오판 시 정상 신호에서도 반대 방향으로 역진입(Error Entry)
            // 단, 튜토리얼 등 확정적 이벤트(IsExternalEventOverride) 진행 중에는 요미가 완벽하게 맞추도록 오판 로직을 무시합니다.
            if (levelSystem != null && signal.IsTrueSignal && !(marketEngine != null && marketEngine.IsExternalEventOverride))
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

            float deltaPct = Mathf.Abs(signal.TargetPercentageDelta) > 0.1f ? Mathf.Abs(signal.TargetPercentageDelta) / 100f : 0.045f;

            // 💡 [차트 공부 귀속] 진입 지연 패널티 반영: 예상 변동 파동(deltaPct) 중 n%만큼 주가가 진행된 뒤 늦게 진입하는 슬리피지 보정
            if (levelSystem != null)
            {
                float delayRatio = levelSystem.GetEntryDelayPenaltyRatio();
                if (delayRatio > 0f)
                {
                    float waveSlippage = deltaPct * delayRatio;
                    startPrice = posType == TradingController.PositionType.Long 
                        ? startPrice * (1f + waveSlippage) 
                        : startPrice * (1f - waveSlippage);
                }
            }

            // 💡 [큐브 풀기 귀속] 인내심 계수 반영: 확정 수익 구간에서도 목표 수익의 일부만 먹고 조기 익절하거나 100% 홀딩
            float takeProfitMult = levelSystem != null ? levelSystem.GetTakeProfitMultiplier() : 1.0f;
            if (aiStyle == TradingController.AITradingStyle.Safe) takeProfitMult *= 0.5f;
            else if (aiStyle == TradingController.AITradingStyle.Aggressive) takeProfitMult *= 2.0f;

            float aiTarget = posType == TradingController.PositionType.Long 
                ? startPrice * (1f + deltaPct * takeProfitMult) 
                : startPrice * (1f - deltaPct * takeProfitMult);

            Debug.Log($"[AITradingBrain 진입 계산] 방향:{posType}, 성향:{aiStyle}, Delta:{deltaPct}, 지연패널티적용타점:{startPrice}, 익절배율:{takeProfitMult}, 최종목표가:{aiTarget}");

            // 💡 [책읽기 귀속] 판단력 계수 반영: 손절 타점 단축/확대 (LV 낮을수록 큰 손절 -9%, 높을수록 빠른 칼손절 -1.5%)
            float stopLossTightness = levelSystem != null ? levelSystem.GetStopLossTightness() : 0.02f;
            if (aiStyle == TradingController.AITradingStyle.Safe) stopLossTightness *= 0.5f;

            float aiStopLoss = posType == TradingController.PositionType.Long 
                ? startPrice * (1f - stopLossTightness) 
                : startPrice * (1f + stopLossTightness);

            if (aiStyle == TradingController.AITradingStyle.Aggressive) aiStopLoss = 0f;

            bool opened = TradeExecutor(posType, margin, leverage, aiTarget, aiStopLoss, false, startPrice);

            if (opened)
            {
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

            // 💡 isEmergencyTrade: true를 전달하여 레벨에 따른 레버리지 클램핑을 무시하고 125배 뇌동/반대매매 보장
            bool opened = TradeExecutor(crazyPos, margin, leverage, aiTarget, 0f, true, startPrice);
            if (opened)
            {
                int actualLev = tradingController != null ? tradingController.CurrentLeverage : leverage;
                float actualRatio = (balance > 0f && tradingController != null) ? tradingController.MarginAmount / balance : 0.95f;

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
            float balance = GetAvailableBalance();
            float baseMargin = tradingController != null 
                ? (tradingController.MarginAmount > 0f ? tradingController.MarginAmount : tradingController.LastMarginAmount) 
                : 0f;
            float roe = baseMargin > 0f ? (pnl / baseMargin) * 100f : 0f;

            if (pnl < 0f)
            {
                if (currentActiveSignal.Strength == SignalStrength.Weak)
                {

                }
                else
                {

                }
            }
            else if (pnl > 0f)
            {

            }
            else
            {

            }
        }

        private void HandlePositionLiquidated()
        {
            isProcessingSignal = false;
        }

        

        /// <summary>
        /// 플레이어가 직접 매수/매도 진입했을 때, AI가 차트 국면과 함정 여부를 분석하여 힌트 대사를 출력합니다.
        /// 차트 공부(ChartStudyLevel) 레벨이 낮을수록 부정확하거나 혼란스러운 힌트를 제공합니다.
        /// </summary>
        public void ProvideChartHintToPlayer(TradingController.PositionType playerPos)
        {
            ITraderLevelProvider levelSystem = levelProvider ?? TraderLevelSystem.Instance;
            int chartLv = levelSystem != null ? levelSystem.ChartStudyLevel : 1;
            float accuracy = levelSystem != null ? levelSystem.GetSignalAccuracy() : 0.7f;

            bool isAccurateHint = UnityEngine.Random.value <= accuracy;

            string hintText = "";
            var matcher = FXOverdose.AI.Dialogue.YomiDialogueMatcher.Instance;
            string posStr = playerPos.ToString();

            if (chartLv >= 7 || (chartLv >= 4 && isAccurateHint))
            {
                // 고레벨 / 정확한 간파 힌트
                if (isProcessingSignal && !currentActiveSignal.IsTrueSignal)
                {
                    hintText = matcher?.GetEventDialogue("ChartHint_TrapDetected_High");
                    if (string.IsNullOrEmpty(hintText)) hintText = $"꺄아악 오빠 멈춰!! 지금 {playerPos} 들어간 거, 세력 년들이 파놓은 가짜 덫(Trap)이란 말야! 당장 청산 안 하면 우리 다 잃어버려... 제발 요미 말 들어줘 흐윽...!!";
                }
                else if (isProcessingSignal && currentActiveSignal.IsTrueSignal)
                {
                    hintText = matcher?.GetEventDialogue("ChartHint_GoodEntry_High");
                    if (string.IsNullOrEmpty(hintText)) hintText = $"앗...! 우리 오빠 천재인가 봐!! 저항선 뚫는 완벽한 {playerPos} 타점이야! 절대 쫄보처럼 흔들려 털리지 말고 끝까지 홀딩해, 알겠지? ♥";
                }
                else
                {
                    hintText = matcher?.GetEventDialogue("ChartHint_Normal_High");
                    if (string.IsNullOrEmpty(hintText)) hintText = $"오빠가 잡은 {playerPos} 타점... 호가창 거래량이 붙고 있어! 지지선만 안 깨지면 우리 대박 나는 거야... 요미 지금 심장 엄청 떨려 ♥";
                }
            }
            else
            {
                // 차트 공부 레벨이 낮아 불안하거나 감에 의존하는 멘헤라 리액션
                if (UnityEngine.Random.value < 0.5f)
                {
                    hintText = matcher?.GetEventDialogue("ChartHint_Confused_Low");
                    if (string.IsNullOrEmpty(hintText)) hintText = $"으응...? {playerPos} 자리야...? 캔들이 막 꼬물거리는데 솔직히 잘 모르겠어... 만약 잃어도 요미 미워하거나 버리면 안 돼 오빠...? 약속해... 흐윽...";
                }
                else
                {
                    hintText = matcher?.GetEventDialogue("ChartHint_BlindTrust_Low");
                    if (string.IsNullOrEmpty(hintText)) hintText = $"꺄아아 오빠가 {playerPos} 샀다!! 뭔지 모르지만 무조건 떡상해라!! 우리 오빠 돈 뺏어가는 세력 놈들은 요미가 다 저주해 버릴 거야!! ♥";
                }
            }
            
            hintText = hintText.Replace("{position}", posStr);
            // 힌트를 텍스트로만 만들고 버려지던 버그 수정 -> 요미 말풍선으로 직접 띄움
            var visual = UnityEngine.Object.FindAnyObjectByType<FXOverdose.AI.AIVisualController>(UnityEngine.FindObjectsInactive.Include);
            if (visual != null)
            {
                visual.DisplayDialogueBalloon(hintText, FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.EventCategory.ChartMovement);
            }

            // 기억 시스템에 저장
            FXOverdose.AI.TraderMemoryManager.Instance?.AddMemory(FXOverdose.AI.EventCategory.ChartMovement, hintText, 6);
        }

    }
}

