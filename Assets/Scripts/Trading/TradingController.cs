using System;
using UnityEngine;
using FXOverdose.Core;

namespace FXOverdose.Trading
{
    public class TradingController : MonoBehaviour
    {
        public enum PositionType
        {
            None,
            Long,
            Short
        }

        public enum EventPositionHandlingMode
        {
            StandardAuto,          // 0: 유연한 AI 자동 판단 (목표가/손절가 또는 +300% 이상 고수익/극심한 손실 시 판단)
            InstantTakeProfit,     // 1: 목표 달성 즉시 칼익절 (+100% 등 지정 수익률 달성 시 빔 효과 중에도 즉시 익절)
            InstantStopLoss,       // 2: 위기 감지 즉시 손절 (-30% 등 지정 손실선 도달 시 빔 효과 중에도 즉시 손절)
            HoldToMitigateLoss,    // 3: 손실 약화 버티기 (일부러 버텨서 반등을 노리며, 청산 직전(-85%)에만 비상 청산)
            GreedyHold             // 4: 탐욕적 홀딩 (3000% 등 극한의 수익을 노리며 이벤트 쉴드 끝까지 버틴 후 최고점/트레이링 익절)
        }

        public static TradingController Instance { get; private set; }

        public enum OwnerType
        {
            AI,
            Player
        }

        public enum TradingMode
        {
            AI_Auto,
            Player_Manual
        }

        [Header("매매 조작 모드 설정")]
        [SerializeField] private TradingMode activeTradingMode = TradingMode.AI_Auto;
        public TradingMode ActiveTradingMode => activeTradingMode;
        public event Action<TradingMode> OnTradingModeChanged;

        /// <summary>챌린지 모드는 AI 자동매매를 허용하지 않습니다.</summary>
        public bool IsAITradingLockedByGameMode =>
            SaveLoadManager.Instance != null && !SaveLoadManager.Instance.AllowsAITrading;
        
        public bool IsManualModeLockedByYomi { get; private set; } = false;
        public void LockManualMode()
        {
            // 챌린지에서는 어떤 기믹도 USER 수동매매 주도권을 빼앗을 수 없습니다.
            if (IsAITradingLockedByGameMode) return;
            IsManualModeLockedByYomi = true;
        }
        public void UnlockManualMode() { IsManualModeLockedByYomi = false; }

        public void LockManualModeTemporarily(float seconds)
        {
            if (IsAITradingLockedByGameMode) return;
            StartCoroutine(TemporaryLockRoutine(seconds));
        }

        private global::System.Collections.IEnumerator TemporaryLockRoutine(float seconds)
        {
            IsManualModeLockedByYomi = true;
            yield return new WaitForSecondsRealtime(seconds);
            IsManualModeLockedByYomi = false;
        }

        private void Awake()
        {
            Instance = this;

            if (IsAITradingLockedByGameMode)
            {
                activeTradingMode = TradingMode.Player_Manual;
                IsManualModeLockedByYomi = false;
                Debug.Log("[TradingController] CHALLENGE 모드: USER 수동매매로 고정합니다.");
            }
        }

        public void SetTradingMode(TradingMode mode, bool forceRestore = false)
        {
            if (IsAITradingLockedByGameMode && mode == TradingMode.AI_Auto)
            {
                if (activeTradingMode != TradingMode.Player_Manual)
                {
                    activeTradingMode = TradingMode.Player_Manual;
                    OnTradingModeChanged?.Invoke(activeTradingMode);
                }
                IsManualModeLockedByYomi = false;
                Debug.LogWarning("[TradingController] CHALLENGE 모드에서는 AI 자동매매를 사용할 수 없습니다.");
                return;
            }

            if (activeTradingMode == mode) return;

            if (!forceRestore)
            {
                if (mode == TradingMode.Player_Manual && IsManualModeLockedByYomi)
                {
                    Debug.LogWarning("[TradingController] ⚠️ 요미가 매매 주도권을 강제로 뺏어 잠근 상태라 수동 모드로 전환할 수 없습니다.");
                    OutputSpecificEventDialogue("ToggleManualBlocked");
                    return;
                }

                if (mode == TradingMode.Player_Manual && traderStatus != null && traderStatus.CurrentMentalState == TraderStatus.MentalState.Overdose)
                {
                    Debug.LogWarning("[TradingController] ⚠️ 오버도즈 상태에서는 수동 매매로 전환할 수 없습니다.");
                    OutputSpecificEventDialogue("ToggleManualBlocked");
                    return;
                }

                // 2. 게임 플레이(장이 개시된 상태) 검증
                var gm = gameManager != null ? gameManager : UnityEngine.Object.FindAnyObjectByType<GameManager>();
                if (gm != null && gm.CurrentState != GameManager.GameState.Playing)
                {
                    Debug.LogWarning("[TradingController] ⚠️ 장이 개시(Playing)되기 전에는 매매 모드를 전환할 수 없습니다.");
                    OutputSpecificEventDialogue("ToggleManualBlocked");
                    return;
                }

                // 3. 시장 오픈 여부 검증
                var market = marketEngine != null ? marketEngine : UnityEngine.Object.FindAnyObjectByType<MarketSimulationEngine>();
                if (market != null && !market.IsMarketOpen)
                {
                    Debug.LogWarning("[TradingController] ⚠️ 시장(Market)이 아직 개장하지 않았습니다. 개장 후에 전환 가능합니다.");
                    OutputSpecificEventDialogue("ToggleManualBlocked");
                    return;
                }
            }

            activeTradingMode = mode;
            Debug.Log($"[TradingController ⚙️] 매매 조작 모드 전환: {mode} (forceRestore: {forceRestore})");
            OnTradingModeChanged?.Invoke(mode);

            if (!forceRestore)
            {
                if (mode == TradingMode.Player_Manual)
                {
                    OutputSpecificEventDialogue("ToggleManualStart");
                }
                else
                {
                    OutputSpecificEventDialogue("ToggleManualAuto");
                }
            }
        }

        public void ToggleTradingMode()
        {
            if (IsAITradingLockedByGameMode)
            {
                SetTradingMode(TradingMode.AI_Auto);
                return;
            }

            SetTradingMode(activeTradingMode == TradingMode.AI_Auto ? TradingMode.Player_Manual : TradingMode.AI_Auto);
        }

        [Header("시스템 연결")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private MarketSimulationEngine marketEngine;
        [SerializeField] private TraderStatus traderStatus;

        [Header("현재 포지션 상태 (읽기 전용)")]
        [SerializeField] private PositionType currentPosition = PositionType.None;
        [SerializeField] private OwnerType currentOwner = OwnerType.AI;
        [SerializeField] private float entryPrice;
        [SerializeField] private float marginAmount; // 투입한 증거금
        [SerializeField] private int currentLeverage = 10;
        [SerializeField] private float liquidationPrice;

        [SerializeField] private float targetPrice;   // AI가 결정한 목표 주가 (익절가)
        [SerializeField] private float stopLossPrice; // AI가 결정한 손절가

        private float lastMarginAmount = 0f;
        private PositionType lastClosedPosition = PositionType.None;
        private float lastClosedPrice = 0f;
        private float lastClosedTime = -1f;

        private bool isMarginCallSlowMotionTriggered = false;
        private bool isTargetBreakthroughSlowMotionTriggered = false;

        public PositionType CurrentPosition => currentPosition;
        public OwnerType CurrentOwner => currentOwner;
        public bool IsActive => currentPosition != PositionType.None;
        public float EntryPrice => entryPrice;
        public float MarginAmount => marginAmount;
        public float LastMarginAmount => lastMarginAmount;
        public int CurrentLeverage => currentLeverage;
        public float LiquidationPrice => liquidationPrice;
        public float TargetPrice => targetPrice;
        public float StopLossPrice => stopLossPrice;

        // 이벤트 통지
        public event Action OnPositionChanged;
        public event Action OnPositionLiquidated;
        public event Action<float, float> OnPositionClosed; // (최종 회수금, PnL)
        public event Action<PositionType, float, int> OnPositionOpened; // (포지션방향, 증거금, 레버리지)

        // 테스트 및 디버그용 포지션 청산 시뮬레이션 Helper
        public void SimulateCloseForTest(bool isProfit, float pnl)
        {
            if (currentPosition == PositionType.None) return;

            if (ActiveItemEffectManager.Instance != null)
            {
                if (pnl > 0f) pnl *= (1f + ActiveItemEffectManager.Instance.ProfitBoostRate);
                else if (pnl < 0f) pnl *= (1f - ActiveItemEffectManager.Instance.LossReductionRate);
            }


            float returned = marginAmount + pnl;
            if (returned < 0f) returned = 0f;
            lastMarginAmount = marginAmount;

            // 💡 [이벤트 순서 수정] 이벤트 수신자(AI, UI)가 활성 증거금(MarginAmount) 및 포지션 정보를 정확히 읽을 수 있도록 청산 직전에 이벤트 발송!
            OnPositionClosed?.Invoke(returned, pnl);

            currentPosition = PositionType.None;
            targetPrice = 0f;
            stopLossPrice = 0f;
            isEventTradeActive = false;
            currentEventHandlingMode = EventPositionHandlingMode.StandardAuto;
            eventTargetROELimit = 0f;
            eventStopLossROELimit = 0f;
            isEventPlayerChoice = false;
            isEventTrueSignal = true;
            maxObservedEventROE = 0f;
            lastReportedROEBasket = 0;
            OnPositionChanged?.Invoke();
        }

        private void Start()
        {
            if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
            if (marketEngine == null) marketEngine = FindAnyObjectByType<MarketSimulationEngine>();
            traderStatus = TraderStatus.CanonicalInstance;

            if (marketEngine != null)
            {
                marketEngine.OnPriceUpdated += HandlePriceUpdated;
            }
        }

        public void RestorePosition(FXOverdose.Core.SaveData data)
        {
            if (data == null || !data.HasActivePosition) return;
            
            currentPosition = data.PositionType;
            entryPrice = data.EntryPrice;
            marginAmount = data.MarginAmount;
            currentLeverage = data.CurrentLeverage;
            targetPrice = data.TargetPrice;
            stopLossPrice = data.StopLossPrice;
            currentOwner = data.CurrentOwner;

            const float maintenanceMarginRate = 0.005f;
            if (currentPosition == PositionType.Long)
            {
                liquidationPrice = entryPrice * (1f - (1f / currentLeverage) + maintenanceMarginRate);
            }
            else if (currentPosition == PositionType.Short)
            {
                liquidationPrice = entryPrice * (1f + (1f / currentLeverage) - maintenanceMarginRate);
            }

            OnPositionOpened?.Invoke(currentPosition, marginAmount, currentLeverage);
            OnPositionChanged?.Invoke();
        }

        private void OnDestroy()
        {
            if (marketEngine != null)
            {
                marketEngine.OnPriceUpdated -= HandlePriceUpdated;
            }
        }

        
        private float lastReportedROE = 0f;
        private float lastROEDialogueTime = 0f;
        private float eventPositionOpenedTime = -1f;
        private float eventProtectionEndTime = -1f;
        public bool IsEventProtected => Time.time < eventProtectionEndTime;
        public bool IsEventPlayerChoice => isEventPlayerChoice;
        public bool IsEventTrueSignal => isEventTrueSignal;

        [Header("Overdose 매매 제어 상태")]
        [SerializeField] private bool isOverdoseTradeActive = false;
        [SerializeField] private float overdoseProtectionEndTime = -1f;
        public bool IsOverdoseTradeActive => isOverdoseTradeActive;

        [Header("이벤트 포지션 관리 상태 (읽기 전용)")]
        [SerializeField] private bool isEventTradeActive = false;
        public bool IsEventTradeActive => isEventTradeActive;
        [SerializeField] private EventPositionHandlingMode currentEventHandlingMode = EventPositionHandlingMode.StandardAuto;
        [SerializeField] private float eventTargetROELimit = 0f;
        [SerializeField] private float eventStopLossROELimit = 0f;
        [SerializeField] private bool isEventPlayerChoice = false;
        [SerializeField] private bool isEventTrueSignal = true;
        private float maxObservedEventROE = 0f;
        private int lastReportedROEBasket = 0;
        private float playerTradeCooldownEndTime = -1f;
        public const float PlayerTradeCooldownSeconds = 3f;
        public float RemainingPlayerTradeCooldown => Mathf.Max(0f, playerTradeCooldownEndTime - Time.time);
        public bool IsPlayerTradeOnCooldown => RemainingPlayerTradeCooldown > 0f;

        private void Update()
        {
            // 일시정지/정산 중에는 자동 보호시간 만료가 포지션을 청산하지 않게 합니다.
            // 특히 Settlement에서는 GameManager가 잔고 변경을 거부하므로, 여기서 청산하면
            // 회수금 반영 없이 포지션만 초기화될 수 있습니다.
            if (gameManager == null || gameManager.CurrentState != GameManager.GameState.Playing)
            {
                return;
            }

            if (isEventTradeActive && currentPosition != PositionType.None)
            {
                bool isMarketEventOver = marketEngine != null && !marketEngine.IsExternalEventOverride;
                if (Time.time >= eventProtectionEndTime || isMarketEventOver)
                {
                    HandleEventProtectionExpired();
                }
            }
        }

        private void ProcessEventPositionReaction(float price)
        {
            if (currentPosition == PositionType.None || marginAmount <= 0f) return;

            float roe = CalculateROEPercentage();
            if (roe > maxObservedEventROE) maxObservedEventROE = roe;

            var visual = UnityEngine.Object.FindAnyObjectByType<FXOverdose.AI.AIVisualController>();

            // 💡 1. [능동적 실시간 실황 중계 및 악결과 원망/절규 리액션]
            if (visual != null)
            {
                if (isEventPlayerChoice && !isEventTrueSignal)
                {
                    if (roe <= -40f && lastReportedROEBasket > -40)
                    {
                        lastReportedROEBasket = -40;
                        OutputSpecificEventDialogue("RoeNegative40", roe: roe);
                        if (traderStatus != null) traderStatus.ModifyMentalState(-15f);
                    }
                    else if (roe <= -65f && lastReportedROEBasket > -65)
                    {
                        lastReportedROEBasket = -65;
                        OutputSpecificEventDialogue("RoeNegative65", roe: roe);
                    }
                }
                else if (!isEventTrueSignal)
                {
                    if (roe <= -50f && lastReportedROEBasket > -50)
                    {
                        lastReportedROEBasket = -50;
                        OutputSpecificEventDialogue("RoeNegative50", roe: roe);
                    }
                }
                else
                {
                    if (roe >= 50f && roe < 100f && lastReportedROEBasket < 50)
                    {
                        lastReportedROEBasket = 50;
                        OutputSpecificEventDialogue("RoePositive50", roe: roe, priority: FXOverdose.AI.DialoguePriority.Normal, cat: FXOverdose.AI.EventCategory.ChartMovement);
                    }
                    else if (roe >= 100f && roe < 200f && lastReportedROEBasket < 100)
                    {
                        lastReportedROEBasket = 100;
                        OutputSpecificEventDialogue("RoePositive100", roe: roe, priority: FXOverdose.AI.DialoguePriority.High, cat: FXOverdose.AI.EventCategory.ChartMovement);
                    }
                    else if (roe >= 200f && lastReportedROEBasket < 200)
                    {
                        lastReportedROEBasket = 200;
                        OutputSpecificEventDialogue("RoePositive200", roe: roe, priority: FXOverdose.AI.DialoguePriority.High, cat: FXOverdose.AI.EventCategory.ChartMovement);
                    }
                }
            }

            // 💡 [수동 조작 모드 보호] 플레이어가 수동 조작 중이고 오버도즈 상태가 아니면 AI가 비상 물타기 및 자동 청산 로직을 수행하지 않음
            if (activeTradingMode == TradingMode.Player_Manual && !isOverdoseTradeActive)
            {
                return;
            }

            // ⭐ [Overdose 강제 35초 쉴드 보장] 오버도즈 중에는 AI의 익절/손절/물타기 등 모든 자동 판단(StandardAuto 포함)을 중지하고 청산(CheckLiquidation) 또는 플레이어의 아이템 사용만 허용!
            if (isOverdoseTradeActive)
            {
                return;
            }

            // 💡 2. [악결과(Trap/실패) 바닥 부근 비상 물타기 발동 (HoldToMitigateLoss 혹은 StandardAuto)]
            if (!isEventTrueSignal && (currentEventHandlingMode == EventPositionHandlingMode.HoldToMitigateLoss || currentEventHandlingMode == EventPositionHandlingMode.StandardAuto))
            {
                if (roe <= -75f && roe > -92f && lastReportedROEBasket != -999 && gameManager != null && gameManager.CurrentBalance > 10f)
                {
                    lastReportedROEBasket = -999;
                    float addMargin = Mathf.Min(gameManager.CurrentBalance * 0.45f, marginAmount * 1.5f);
                    if (addMargin > 10f)
                    {
                        gameManager.ChangeBalance(-addMargin);
                        marginAmount += addMargin;
                        Debug.Log($"[TradingController 💉] 악결과 바닥 부근(-75%) 비상 물타기 발동! 증거금 ${addMargin:N0} 투입으로 평단가 낮춤 및 반등 꼬리 탈출 준비!");
                        OutputSpecificEventDialogue("EmergencyWaterRiding", roe: roe);
                        OnPositionChanged?.Invoke();
                        return;
                    }
                }
            }

            var levelSystem = TraderLevelSystem.Instance;
            int cubeLvl = levelSystem != null ? levelSystem.CubePatienceLevel : 1;
            int bookLvl = levelSystem != null ? levelSystem.BookJudgmentLevel : 1;
            int chartLvl = levelSystem != null ? levelSystem.ChartStudyLevel : 1;

            switch (currentEventHandlingMode)
            {
                case EventPositionHandlingMode.InstantTakeProfit:
                    float expectedMaxROE = 100f;
                    if (marketEngine != null && marketEngine.CurrentSignalPhase != SignalPhase.None)
                    {
                        float beamPct = Mathf.Abs(marketEngine.ActiveSignal.TargetPercentageDelta);
                        expectedMaxROE = beamPct * currentLeverage;
                        if (expectedMaxROE <= 0f) expectedMaxROE = 100f;
                    }
                    float takeProfitMultiplier = levelSystem != null ? levelSystem.GetTakeProfitMultiplier() : 0.8f;
                    float dynamicTakeProfit = expectedMaxROE * takeProfitMultiplier;
                    float targetTakeProfitROE = eventTargetROELimit > 0f ? eventTargetROELimit : dynamicTakeProfit;
                    if (roe >= targetTakeProfitROE)
                    {
                        Debug.Log($"[TradingController ⚡] 이벤트 칼익절(InstantTakeProfit) 발동! 예상 수익 구간의 {takeProfitMultiplier*100:0}% 달성 (ROE +{roe:F1}%).");
                        OutputYomiDialogue(FXOverdose.AI.EventCategory.PositionClosed, FXOverdose.AI.DialoguePriority.High);
                        ClosePosition();
                    }
                    break;

                case EventPositionHandlingMode.InstantStopLoss:
                    float stopLossTightness = levelSystem != null ? levelSystem.GetStopLossTightness() : 0.09f;
                    float dynamicStopLoss = -stopLossTightness * 3.33f * 100f; // -30% ~ -5%
                    float targetStopLossROE = eventStopLossROELimit < 0f ? eventStopLossROELimit : dynamicStopLoss;
                    if (roe <= targetStopLossROE)
                    {
                        Debug.Log($"[TradingController ⚡] 이벤트 긴급 손절(InstantStopLoss) 발동! ROE {roe:F1}% 위기 감지로 피해를 최소화하기 위해 즉시 정리합니다.");
                        OutputYomiDialogue(FXOverdose.AI.EventCategory.PositionClosed, FXOverdose.AI.DialoguePriority.High);
                        ClosePosition();
                    }
                    break;

                case EventPositionHandlingMode.HoldToMitigateLoss:
                    float mitigationStopLimit = eventStopLossROELimit < 0f ? eventStopLossROELimit : -88f;
                    if (roe <= mitigationStopLimit)
                    {
                        Debug.Log($"[TradingController 🛡️] 손실 약화 버티기(HoldToMitigateLoss) 비상 방어: 청산 직전까지 인내했으나 잔여 증거금을 지키기 위해 긴급 정리합니다. (ROE {roe:F1}%)");
                        OutputYomiDialogue(FXOverdose.AI.EventCategory.PositionClosed, FXOverdose.AI.DialoguePriority.High);
                        ClosePosition();
                    }
                    else 
                    {
                        float escapeLimit = -12f + (bookLvl - 1) * 1.5f;
                        if ((maxObservedEventROE <= -40f || lastReportedROEBasket == -999) && roe >= escapeLimit)
                        {
                            Debug.Log($"[TradingController 🛡️] 손실 약화 버티기(HoldToMitigateLoss) 성공! 반등 꼬리(Whipsaw Recovery) 시점에 극적으로 탈출합니다. (ROE {roe:F1}%)");
                            OutputYomiDialogue(FXOverdose.AI.EventCategory.PositionClosed, FXOverdose.AI.DialoguePriority.High);
                            ClosePosition();
                        }
                    }
                    break;

                case EventPositionHandlingMode.GreedyHold:
                    if (eventTargetROELimit > 0f && roe >= eventTargetROELimit)
                    {
                        Debug.Log($"[TradingController 👑] 탐욕적 홀딩(GreedyHold) 지정 목표치 달성! ROE +{roe:F1}% 극한 수익 확정 청산!");
                        OutputYomiDialogue(FXOverdose.AI.EventCategory.PositionClosed, FXOverdose.AI.DialoguePriority.High);
                        ClosePosition();
                    }
                    else 
                    {
                        float trailingSens1 = 0.75f + (chartLvl - 1) * 0.015f;
                        float trailingSens2 = 0.80f + (chartLvl - 1) * 0.015f;
                        float trailingSens3 = 0.82f + (chartLvl - 1) * 0.015f;

                        if ((maxObservedEventROE >= 150f && roe <= maxObservedEventROE * trailingSens1) ||
                            (maxObservedEventROE >= 80f && roe <= maxObservedEventROE * trailingSens2) ||
                            (maxObservedEventROE >= 45f && roe <= maxObservedEventROE * trailingSens3))
                        {
                            Debug.Log($"[TradingController 👑] 탐욕적 홀딩(GreedyHold) 트레이링 익절 작동! 고점(+{maxObservedEventROE:F1}%) 대비 조정 감지로 ROE +{roe:F1}% 확정 청산!");
                            OutputYomiDialogue(FXOverdose.AI.EventCategory.PositionClosed, FXOverdose.AI.DialoguePriority.High);
                            ClosePosition();
                        }
                    }
                    break;

                case EventPositionHandlingMode.StandardAuto:
                default:
                    if (eventTargetROELimit > 0f && roe >= eventTargetROELimit)
                    {
                        Debug.Log($"[TradingController 🤖] 이벤트 유연 판단(StandardAuto) 지정 익절: ROE +{roe:F1}% 달성으로 수익을 챙깁니다.");
                        OutputYomiDialogue(FXOverdose.AI.EventCategory.PositionClosed, FXOverdose.AI.DialoguePriority.High);
                        ClosePosition();
                    }
                    else
                    {
                        float trailingSens = 0.75f + (chartLvl - 1) * 0.015f; // LV1 = 75%, LV10 = 88.5%
                        if (maxObservedEventROE >= 40f && roe <= maxObservedEventROE * trailingSens)
                        {
                            Debug.Log($"[TradingController 🤖] 이벤트 유연 판단(StandardAuto) 트레이링 익절: 고점(+{maxObservedEventROE:F1}%) 대비 조정 감지로 ROE +{roe:F1}% 확정 청산.");
                            OutputYomiDialogue(FXOverdose.AI.EventCategory.PositionClosed, FXOverdose.AI.DialoguePriority.High);
                            ClosePosition();
                        }
                        else if (roe <= -65f && Time.time >= eventPositionOpenedTime + 5.0f)
                        {
                            Debug.Log($"[TradingController 🤖] 이벤트 유연 판단(StandardAuto) 손절: 위험 수준의 손실(ROE {roe:F1}%) 감지로 포지션을 정리합니다.");
                            OutputYomiDialogue(FXOverdose.AI.EventCategory.PositionClosed, FXOverdose.AI.DialoguePriority.High);
                            ClosePosition();
                        }
                    }
                    break;
            }
        }

        private void HandleEventProtectionExpired()
        {
            isEventTradeActive = false;
            isEventPlayerChoice = false;
            isEventTrueSignal = true;
            lastReportedROEBasket = 0;
            float roe = CalculateROEPercentage();
            Debug.Log($"[TradingController 🏁] 이벤트 보호 쉴드 및 유지 시간 종료. 최종 ROE: {roe:F1}%, 모드: {currentEventHandlingMode}");

            var visual = UnityEngine.Object.FindAnyObjectByType<FXOverdose.AI.AIVisualController>();

            // 💡 [수동 조작 모드 보호] 플레이어 수동 조작 중이면 AI가 포지션을 자동 청산하지 않고 제어권을 넘김
            if (activeTradingMode == TradingMode.Player_Manual && !isOverdoseTradeActive)
            {
                Debug.Log($"[TradingController 🔄] 이벤트 보호 쉴드 종료. 플레이어 수동 모드이므로 AI가 포지션을 자동 정리하지 않고 제어권을 유지합니다.");
                currentEventHandlingMode = EventPositionHandlingMode.StandardAuto;
                eventTargetROELimit = 0f;
                eventStopLossROELimit = 0f;
                maxObservedEventROE = 0f;
                return;
            }

            switch (currentEventHandlingMode)
            {
                case EventPositionHandlingMode.HoldToMitigateLoss:
                    if (roe < 0f)
                    {
                        Debug.Log($"[TradingController 🛡️] 손실 약화 버티기(HoldToMitigateLoss) 시간 종료: 인내 끝에 손실({roe:F1}%)을 완화하여 정리합니다.");
                        OutputSpecificEventDialogue("EventHoldMitigateLoss", roe: roe, priority: FXOverdose.AI.DialoguePriority.High, cat: FXOverdose.AI.EventCategory.PositionClosed);
                        ClosePosition();
                    }
                    else
                    {
                        Debug.Log($"[TradingController 🛡️] 손실 약화 버티기(HoldToMitigateLoss) 역전 성공: 오히려 수익(+{roe:F1}%)으로 전환되어 익절합니다!");
                        OutputSpecificEventDialogue("EventHoldMitigateLoss", roe: roe, priority: FXOverdose.AI.DialoguePriority.High, cat: FXOverdose.AI.EventCategory.PositionClosed);
                        ClosePosition();
                    }
                    break;

                case EventPositionHandlingMode.GreedyHold:
                    if (roe > 0f)
                    {
                        Debug.Log($"[TradingController 👑] 탐욕적 홀딩(GreedyHold) 시간 종료: 누적된 ROE +{roe:F1}% 대박 수익을 전액 챙깁니다!");
                        OutputSpecificEventDialogue("EventGreedyHoldWin", roe: roe, priority: FXOverdose.AI.DialoguePriority.High, cat: FXOverdose.AI.EventCategory.PositionClosed);
                        ClosePosition();
                    }
                    else
                    {
                        Debug.Log($"[TradingController 👑] 탐욕적 홀딩(GreedyHold) 시간 종료: 기대와 달리 손실({roe:F1}%)이 발생하여 정리합니다.");
                        ClosePosition();
                    }
                    break;

                case EventPositionHandlingMode.InstantTakeProfit:
                case EventPositionHandlingMode.InstantStopLoss:
                case EventPositionHandlingMode.StandardAuto:
                default:
                    if (roe >= 15f)
                    {
                        Debug.Log($"[TradingController 🎯] 이벤트 시간 종료 및 수익권(ROE +{roe:F1}%) 달성 -> 안전하게 수익 확정 청산.");
                        ClosePosition();
                    }
                    else if (roe <= -30f)
                    {
                        Debug.Log($"[TradingController 🛑] 이벤트 시간 종료 및 손실권(ROE {roe:F1}%) -> 위험 관리 위해 손절 청산.");
                        ClosePosition();
                    }
                    else
                    {
                        Debug.Log($"[TradingController 🔄] 이벤트 포지션 보호 종료 -> 이후 AI 자동매매(익절가/손절가 판단)로 원활히 이관합니다.");
                    }
                    break;
            }

            currentEventHandlingMode = EventPositionHandlingMode.StandardAuto;
            eventTargetROELimit = 0f;
            eventStopLossROELimit = 0f;
            maxObservedEventROE = 0f;
        }

        private void HandlePriceUpdated(float price)
        {
            if (currentPosition == PositionType.None)
            {
                // ⭐ [포지션 조기 종료 이후 차트 추가 폭등/폭락 실황 후회/안도 중계]
                if (Time.time < eventProtectionEndTime || (marketEngine != null && marketEngine.IsOverridingTrend))
                {
                    if (Time.time - lastROEDialogueTime >= 6.0f)
                    {
                        lastROEDialogueTime = Time.time;
                        
                        OutputYomiDialogue(FXOverdose.AI.EventCategory.ChartMovement, FXOverdose.AI.DialoguePriority.Low);
                    }
                }
                return;
            }

            // 1. 강제 청산(Liquidation) 판정
            CheckLiquidation(price);
            if (currentPosition == PositionType.None) return;

            // ⭐ [Overdose 폭주 중 1방향 확실한 청산 유도 및 아이템 회복 탈출 처리]
            if (isOverdoseTradeActive)
            {
                if (traderStatus != null && traderStatus.CurrentMentalState != TraderStatus.MentalState.Overdose && traderStatus.CurrentMental > 0f)
                {
                    // 멘탈이 회복되어도 오버도즈 때 벌려둔 포지션과 차트 함정은 수동으로 청산하기 전까지 유지됩니다.
                    if (Time.time - lastROEDialogueTime >= 5f)
                    {
                        lastROEDialogueTime = Time.time;
                        Debug.Log("[TradingController] 💊 멘탈 회복! 하지만 오버도즈 함정은 포지션 종료 전까지 유지됩니다.");
                        OutputSpecificEventDialogue("MentalOverdoseRecover", priority: FXOverdose.AI.DialoguePriority.High, cat: FXOverdose.AI.EventCategory.MentalChange);
                    }
                    return; // 수동으로 청산할 때까지 AI 개입 및 함정 유지를 위해 대기
                }
                else if (Time.time < overdoseProtectionEndTime)
                {
                    // Overdose 중에는 일반 AI의 자의적 손절/익절이나 휩소에 의한 방해를 완전히 차단하고, 연출적 독백만 출력
                    float overdoseRoe = CalculateROEPercentage();
                    if (overdoseRoe <= -70f && Time.time - lastROEDialogueTime >= 5f)
                    {
                        lastROEDialogueTime = Time.time;
                        OutputSpecificEventDialogue("MentalOverdoseStart", priority: FXOverdose.AI.DialoguePriority.High, cat: FXOverdose.AI.EventCategory.MentalChange);
                    }
                    return; // 💡 다른 매매 로직(익절/손절/이벤트 반응/스위칭 등) 개입을 원천 차단! 오직 청산(CheckLiquidation)이나 아이템 회복 탈출만 가능하게 함!
                }
                else
                {
                    isOverdoseTradeActive = false;
                    if (marketEngine != null) marketEngine.CancelOverdoseTrapSignal();
                }
            }

            // 💡 [고속 스킵 중 포지션 강제 청산] 고속 스킵(스킬 학습 등) 중일 때는 수동 모드라도 요미의 스킬 기반 동적 익절/손절선 도달 시 강제 청산합니다.
            // 이벤트 쉴드보다 우선적으로 평가되어, 고속 스킵 중 방치로 인한 막대한 손실을 방지합니다.
            if (gameManager != null && gameManager.IsFastForwardingTime)
            {
                float skipRoe = CalculateROEPercentage();
                var levelSystem = TraderLevelSystem.Instance;
                
                float takeProfitMultiplier = levelSystem != null ? levelSystem.GetTakeProfitMultiplier() : 0.8f;
                float stopLossTightness = levelSystem != null ? levelSystem.GetStopLossTightness() : 0.09f;

                float dynamicTakeProfitRoe = 50f * takeProfitMultiplier;
                float dynamicStopLossRoe = -stopLossTightness * 3.33f * 100f;

                if (skipRoe >= dynamicTakeProfitRoe)
                {
                    Debug.Log($"[TradingController ⏩] 고속 스킵 중 요미 스킬 기반 목표 수익률(ROE +{dynamicTakeProfitRoe:F1}%) 도달로 강제 익절 청산!");
                    ClosePosition();
                    return;
                }
                else if (skipRoe <= dynamicStopLossRoe)
                {
                    Debug.Log($"[TradingController ⏩] 고속 스킵 중 요미 스킬 기반 위험 손절률(ROE {dynamicStopLossRoe:F1}%) 도달로 강제 손절 청산!");
                    ClosePosition();
                    return;
                }
            }

            // ⭐ 이벤트 보호 쉴드 작동 중: 이벤트 결과에 따른 포지션 보유, 정리, 버티기 등 맞춤형 반응 로직 수행
            if (isEventTradeActive)
            {
                bool isMarketEventOver = marketEngine != null && !marketEngine.IsExternalEventOverride;
                if (Time.time < eventProtectionEndTime && !isMarketEventOver)
                {
                    ProcessEventPositionReaction(price);
                    return;
                }
                else
                {
                    HandleEventProtectionExpired();
                    if (currentPosition == PositionType.None) return;
                }
            }

            // 2. AI 목표 주가(Target Price) 도달 익절 자동 청산 (플레이어 수동 조작 중에는 비활성화, 단 Overdose 시 강제 실행)
            if (targetPrice > 0f && (activeTradingMode == TradingMode.AI_Auto || isOverdoseTradeActive))
            {
                if ((currentPosition == PositionType.Long && price >= targetPrice) ||
                    (currentPosition == PositionType.Short && price <= targetPrice))
                {
                    Debug.Log($"[TradingController 🎯] AI 목표 주가(Target Price: ${targetPrice:N1}) 달성! AI가 자동 익절 청산을 실행합니다.");
                    ClosePosition();
                    return;
                }
            }

            // 3. AI 손절가(Stop Loss) 도달 자동 청산 (플레이어 수동 조작 중에는 비활성화, 단 Overdose 시 강제 실행)
            if (stopLossPrice > 0f && (activeTradingMode == TradingMode.AI_Auto || isOverdoseTradeActive))
            {
                if ((currentPosition == PositionType.Long && price <= stopLossPrice) ||
                    (currentPosition == PositionType.Short && price >= stopLossPrice))
                {
                    Debug.Log($"[TradingController 🛑] AI 손절선(Stop Loss: ${stopLossPrice:N1}) 도달! 자동 손절 청산을 실행합니다.");
                    ClosePosition();
                    return;
                }
            }

            // 4. 실시간 ROE 변동 구간 돌파 독백 트리거 (+15%, -15%, +30%, -30% 등) 및 슬로우 모션 기믹
            float roe = CalculateROEPercentage();

            // [슬로우 모션 기믹] 대박 수익(ROE 50% 이상) 도달 시 슬로우 모션 (4초간 2배 감속)
            if (roe >= 50f && !isTargetBreakthroughSlowMotionTriggered)
            {
                isTargetBreakthroughSlowMotionTriggered = true;
                if (FXOverdose.Core.DynamicTimeRegulator.Instance != null)
                {
                    FXOverdose.Core.DynamicTimeRegulator.Instance.TriggerDramaticSlowMotion(2.0f, 4.0f);
                    Debug.Log("[TradingController] 🚀 대박 수익 돌파(ROE +50%)! 극적 연출을 위해 4초간 슬로우 모션(2배 감속) 가동.");
                }
            }

            if (Time.time - lastROEDialogueTime >= 12f)
            {
                if ((roe >= 15f && lastReportedROE < 15f) || (roe >= 30f && lastReportedROE < 30f))
                {
                    lastReportedROE = roe;
                    lastROEDialogueTime = Time.time;
                    string directionHint = currentPosition == PositionType.Short
                        ? "Short(공매도 하락 배팅) 중이므로 주가가 폭락해서 바닥으로 내려가고 있어 수익이 나고 있는 기분 좋은 상황입니다! (더 내려가라고 소리치세요)"
                        : "Long(상승 배팅) 중이므로 주가가 치솟아 고점을 뚫고 올라가서 수익이 나고 있는 상황입니다! (더 올라가라고 소리치세요)";
                    OutputYomiDialogue(FXOverdose.AI.EventCategory.ChartMovement, FXOverdose.AI.DialoguePriority.Normal);
                }
                else if ((roe <= -15f && lastReportedROE > -15f) || (roe <= -30f && lastReportedROE > -30f))
                {
                    lastReportedROE = roe;
                    lastROEDialogueTime = Time.time;
                    string directionHint = currentPosition == PositionType.Short
                        ? "Short(공매도 하락 배팅) 중인데 주가가 반대로 솟구쳐올라 고점을 부수며 손실이 커지고 있는 위기 상황입니다! (제발 폭락하라고 비세요)"
                        : "Long(상승 배팅) 중인데 주가가 바닥으로 떨어지며 손실이 커지고 있는 위기 상황입니다! (제발 반등하라고 비세요)";
                    OutputYomiDialogue(FXOverdose.AI.EventCategory.ChartMovement, FXOverdose.AI.DialoguePriority.Normal);
                }
            }
        }

        // 포지션 진입 (AI가 방향, 레버리지, 목표가 TargetPrice를 독자적으로 결정하여 호출)
        public bool OpenPosition(PositionType type, float margin, int leverage, float aiTargetPrice = 0f, float aiStopLossPrice = 0f, bool isEmergencyTrade = false, float customEntryPrice = 0f, bool isPlayerDirectedTrade = false)
        {
            if (gameManager == null) gameManager = UnityEngine.Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            if (marketEngine == null) marketEngine = UnityEngine.Object.FindAnyObjectByType<MarketSimulationEngine>(FindObjectsInactive.Include);
            if (traderStatus == null) traderStatus = TraderStatus.CanonicalInstance;

            if (gameManager == null || marketEngine == null || type == PositionType.None)
            {
                return false;
            }

            if (gameManager.CurrentState == GameManager.GameState.Settlement || gameManager.CurrentState == GameManager.GameState.GameOver)
            {
                Debug.LogWarning("[TradingController] 정산 중이거나 게임오버 상태에서는 포지션을 개설할 수 없습니다.");
                return false;
            }

            // 챌린지에서는 AI 일반/폭주/기믹 매매를 모두 막습니다.
            // 단, 플레이어가 돌발 이벤트에서 직접 방향을 선택한 결과만 플레이어 입력으로 인정합니다.
            bool isPlayerDirectedEvent = isEmergencyTrade && isPlayerDirectedTrade;
            if (IsAITradingLockedByGameMode && !isPlayerDirectedEvent)
            {
                Debug.LogWarning("[TradingController] CHALLENGE 모드 규칙으로 AI 포지션 진입을 차단했습니다.");
                return false;
            }

            // ⭐ 이벤트 선택 우선 보호: 이벤트 쉴드 중일 때는 AI의 자의적 신규 진입/스위칭을 확실하게 차단합니다!
            if (!isEmergencyTrade && Time.time < eventProtectionEndTime)
            {
                Debug.Log("[TradingController] 🛡️ 이벤트 보호 쉴드 작동 중: AI의 자동매매(신규 진입 및 스위칭)를 차단하여 이벤트 선택지를 우선시합니다.");
                return false;
            }

            if (currentPosition != PositionType.None)
            {
                Debug.LogWarning($"[TradingController] 🔄 기존 {currentPosition} 포지션 보유 중 새로운 {type} 포지션 진입 요청 감지 -> 기존 포지션을 정리하고 스위칭합니다.");
                ClosePosition();
            }

            if (margin <= 0f || (!isEmergencyTrade && gameManager.CurrentBalance <= 1f))
            {
                Debug.LogWarning("[TradingController] 증거금이 부족합니다.");
                return false;
            }

            // 주인공 레벨에 따른 레버리지 및 증거금 클램핑 적용 (단, 돌발 이벤트 강제 진입 및 오버도스(isEmergencyTrade)는 이벤트 효과 보장을 위해 제외)
            var levelSystem = TraderLevelSystem.Instance;
            if (levelSystem != null && !isEmergencyTrade)
            {
                int maxAllowedLev = levelSystem.GetMaxAllowedLeverage();
                if (leverage > maxAllowedLev) leverage = maxAllowedLev;

                float maxMarginRatio = levelSystem.GetMaxAllowedMarginRatio();
                float maxMarginAmount = gameManager.CurrentBalance * maxMarginRatio;
                if (margin > maxMarginAmount) margin = maxMarginAmount;
            }

            // 💡 [진입 성공률 보장 안전망] 계산된 증거금이 현재 잔고보다 크더라도 잔고의 95%로 자동 조정하여 진입 실패(포지션 미개설 현상) 방지
            if (margin > gameManager.CurrentBalance)
            {
                margin = gameManager.CurrentBalance * 0.95f;
            }

            leverage = Mathf.Clamp(leverage, 1, 125);

            currentPosition = type;
            currentOwner = isPlayerDirectedTrade ? OwnerType.Player : OwnerType.AI;
            
            float expectedEntry = type == PositionType.Long ? marketEngine.CurrentAskPrice : marketEngine.CurrentBidPrice;
            entryPrice = customEntryPrice > 0f ? customEntryPrice : expectedEntry;
            marginAmount = margin;
            currentLeverage = leverage;
            targetPrice = aiTargetPrice;
            stopLossPrice = aiStopLossPrice;
            lastReportedROE = 0f;

            // 💡 [조기 게임오버 오진 방지] 포지션 및 증거금을 먼저 설정한 후 잔고를 차감해야 CheckEnding() 시 TotalEquity에 증거금이 정상 합산됩니다.
            float entryFee = margin * leverage * 0.0006f;
            gameManager.ChangeBalance(-(margin + entryFee));

            // 유지 증거금률 0.5% 반영한 청산가 연산
            float maintenanceMarginRate = 0.005f;
            if (type == PositionType.Long)
            {
                liquidationPrice = entryPrice * (1f - (1f / currentLeverage) + maintenanceMarginRate);
            }
            else
            {
                liquidationPrice = entryPrice * (1f + (1f / currentLeverage) - maintenanceMarginRate);
            }

            Debug.Log($"[TradingController 🤖] AI {type} 포지션 개시! 진입가: ${entryPrice:N1}, 증거금: ${margin:N0}, 레버리지: {leverage}x, 목표가(Target Price): ${(targetPrice > 0 ? targetPrice.ToString("N1") : "무제한")}, 청산가: ${liquidationPrice:N1}");
            eventPositionOpenedTime = Time.time;
            OnPositionChanged?.Invoke();
            OnPositionOpened?.Invoke(currentPosition, marginAmount, currentLeverage);
            return true;
        }

        // 플레이어 직접(수동) 매매 진입
        public bool OpenPlayerPosition(PositionType type, float marginPercentage, int leverage)
        {
            // 💡 [단타 어뷰징 방지] 매매 쿨타임 체크 (종료 후 3초간 신규 진입/스위칭 제한)
            if (Time.time < playerTradeCooldownEndTime)
            {
                Debug.LogWarning($"[TradingController] ⏳ 매매 쿨타임 적용 중: 포지션 종료 후 {PlayerTradeCooldownSeconds:0}초간은 새로운 포지션을 개설할 수 없습니다.");
                return false;
            }

            if (gameManager == null) gameManager = UnityEngine.Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            if (marketEngine == null) marketEngine = UnityEngine.Object.FindAnyObjectByType<MarketSimulationEngine>(FindObjectsInactive.Include);
            if (traderStatus == null) traderStatus = TraderStatus.CanonicalInstance;

            if (gameManager == null || marketEngine == null || type == PositionType.None)
            {
                return false;
            }

            if (gameManager.CurrentState == GameManager.GameState.Settlement || gameManager.CurrentState == GameManager.GameState.GameOver)
            {
                Debug.LogWarning("[TradingController] 정산 중이거나 게임오버 상태에서는 플레이어 포지션을 개설할 수 없습니다.");
                return false;
            }

            if (marketEngine.IsServerLagging)
            {
                Debug.LogWarning("[TradingController] ⚠️ 서버 렉(Lag) 발생 중! 매수/매도 주문이 먹통입니다.");
                return false;
            }

            if (currentPosition != PositionType.None)
            {
                Debug.LogWarning($"[TradingController] 🔄 기존 {currentPosition} 포지션 보유 중 플레이어 {type} 진입 요청 -> 기존 포지션을 종료하고 스위칭합니다.");
                ClosePosition();
            }

            var levelSystem = TraderLevelSystem.Instance;
            if (levelSystem != null)
            {
                int maxAllowedLev = levelSystem.GetMaxAllowedLeverage();
                if (leverage > maxAllowedLev) leverage = maxAllowedLev;

                float maxAllowedRatio = levelSystem.GetMaxAllowedMarginRatio();
                if (marginPercentage > maxAllowedRatio) marginPercentage = maxAllowedRatio;
            }

            float margin = gameManager.CurrentBalance * marginPercentage;
            if (margin <= 0f || gameManager.CurrentBalance <= 1f)
            {
                Debug.LogWarning("[TradingController] 플레이어 매매: 증거금이 부족합니다.");
                return false;
            }

            if (margin > gameManager.CurrentBalance)
            {
                margin = gameManager.CurrentBalance * 0.95f;
            }

            leverage = Mathf.Clamp(leverage, 1, 125);

            currentPosition = type;
            currentOwner = OwnerType.Player;

            // 매수(Long)는 매도호가(Ask)로 체결, 매도(Short)는 매수호가(Bid)로 체결
            float finalEntryPrice = type == PositionType.Long ? marketEngine.CurrentAskPrice : marketEngine.CurrentBidPrice;
            if (marketEngine.SlippageRange > 0)
            {
                // 불리한 방향으로 슬리피지 적용 (SlippageRange 단위 * 틱당 최소 변동폭)
                float slippageAmount = marketEngine.CurrentPrice * 0.0005f * marketEngine.SlippageRange;
                finalEntryPrice += (type == PositionType.Long ? slippageAmount : -slippageAmount);
                Debug.Log($"[TradingController] ⚠️ 슬리피지 발동! 체결가: {finalEntryPrice:N1}");
            }
            entryPrice = finalEntryPrice;

            marginAmount = margin;
            currentLeverage = leverage;
            targetPrice = 0f; // 플레이어 직접 판단 익절
            stopLossPrice = 0f; // 플레이어 직접 판단 손절
            lastReportedROE = 0f;

            // 💡 [조기 게임오버 오진 방지] 포지션 및 증거금을 먼저 설정한 후 잔고를 차감합니다.
            gameManager.ChangeBalance(-margin);

            float maintenanceMarginRate = 0.005f;
            if (type == PositionType.Long)
            {
                liquidationPrice = entryPrice * (1f - (1f / currentLeverage) + maintenanceMarginRate);
            }
            else
            {
                liquidationPrice = entryPrice * (1f + (1f / currentLeverage) - maintenanceMarginRate);
            }

            Debug.Log($"[TradingController 🎮] 플레이어 직접 {type} 포지션 진입! 진입가: ${entryPrice:N1}, 증거금: ${margin:N0} ({marginPercentage * 100f:N0}%), 레버리지: {leverage}x, 청산가: ${liquidationPrice:N1}");
            eventPositionOpenedTime = Time.time;
            OnPositionChanged?.Invoke();
            OnPositionOpened?.Invoke(currentPosition, marginAmount, currentLeverage);

            // [통합] 수동/자동 상관없이 진입 리액션 대사 출력
            OutputYomiDialogue(FXOverdose.AI.EventCategory.PositionOpened, FXOverdose.AI.DialoguePriority.High);

            // 플레이어 매매 진행 시 AI 차트 힌트 대사 연동
            var aiBrain = UnityEngine.Object.FindAnyObjectByType<FXOverdose.AI.AITradingBrain>();
            if (aiBrain != null)
            {
                aiBrain.ProvideChartHintToPlayer(type);
            }

            return true;
        }

        // 플레이어 직접 포지션 종료 (수동 청산)
        public void ClosePlayerPosition()
        {
            if (marketEngine != null && marketEngine.IsServerLagging)
            {
                Debug.LogWarning("[TradingController] ⚠️ 서버 렉(Lag) 발생 중! 익절/손절 버튼이 먹통입니다.");
                return;
            }
            ClosePosition();
        }

        // 포지션 종료 (익절/손절)
        public void ClosePosition()
        {
            if (currentPosition == PositionType.None || gameManager == null)
            {
                return;
            }

            float pnl = CalculateUnrealizedPnL();

            // 청산 수수료 적용 (총 포지션 규모의 0.06%)
            float exitFee = marginAmount * currentLeverage * 0.0006f;
            pnl -= exitFee;

            // 액티브 업그레이드 보정 적용
            if (ActiveItemEffectManager.Instance != null)
            {
                if (pnl > 0f)
                {
                    pnl *= (1f + ActiveItemEffectManager.Instance.ProfitBoostRate);
                }
                else if (pnl < 0f)
                {
                    pnl *= (1f - ActiveItemEffectManager.Instance.LossReductionRate);
                }
            }

            // 의상(Costume) 버프 적용
            string equippedCostume = CostumeManager.Instance != null ? CostumeManager.Instance.EquippedCostumeId : null;
            if (pnl > 0f && !string.IsNullOrEmpty(equippedCostume))
            {
                if (equippedCostume == CostumeManager.BunnyGirlId && activeTradingMode == TradingMode.AI_Auto)
                {
                    pnl *= 1.15f; // 바니걸: 자동매매 수익률 15% 증가
                }
                else if (equippedCostume == CostumeManager.BikiniId && activeTradingMode == TradingMode.Player_Manual)
                {
                    pnl *= 1.15f; // 비키니: 수동매매 수익률 15% 증가
                }
                else if (equippedCostume == CostumeManager.JiraiKeiId)
                {
                    pnl *= 1.20f; // 지뢰계: 매매 수익률 20% 증가
                    if (traderStatus != null)
                    {
                        traderStatus.ChangeHealth(10f);
                        traderStatus.ChangeMental(10f);
                    }
                }
            }


            float totalReturn = marginAmount + pnl;

            // 자산 정산
            gameManager.ChangeBalance(totalReturn);

            // 멘탈 및 체력 상태 반영 (손실 크기에 비례한 AI 트레이더 멘탈 변화)
            if (traderStatus != null)
            {
                if (pnl < 0f)
                {
                    // 손실 시 극심한 스트레스 및 기분 저하
                    traderStatus.ChangeMental(pnl * 0.05f); // 예: -1000원 손실 시 멘탈 -50 감소
                }
                else if (pnl > 0f)
                {
                    // 수익 시 경험치 지급 및 주인공 레벨 비례 멘탈 회복 계수 차등 적용
                    if (TraderLevelSystem.Instance != null)
                    {
                        TraderLevelSystem.Instance.AddProtagonistEXP(pnl, currentLeverage);
                    }

                    float winMultiplier = TraderLevelSystem.Instance != null ? TraderLevelSystem.Instance.GetMentalRecoveryMultiplierOnWin() : 1.0f;
                    traderStatus.ChangeMental(pnl * 0.02f * winMultiplier);
                    traderStatus.ChangeHealth(5f);
                }
                else
                {
                    // pnl == 0f (본전 종료 등 실제 수익이 발생하지 않은 거래): 거래 성공으로 인정하지 않아 경험치 및 보너스 미지급
                    Debug.Log("[TradingController] 실제 수익이 발생하지 않은 거래(PnL = 0)이므로 거래 성공 보너스 및 경험치가 지급되지 않습니다.");
                }
            }

            Debug.Log($"[TradingController] 포지션 종료. 실현 손익: {pnl:N1} ({CalculateROEPercentage():F2}%), 최종 회수금: {totalReturn:N0}");

            PositionType closedType = currentPosition;
            lastMarginAmount = marginAmount;
            lastClosedPosition = currentPosition;
            lastClosedPrice = marketEngine != null ? marketEngine.CurrentPrice : entryPrice;
            lastClosedTime = Time.time;

            // [통합] 수동/자동 상관없이 청산 리액션 대사 출력 (currentPosition 정보가 초기화되기 직전에 호출)
            OutputYomiDialogue(FXOverdose.AI.EventCategory.PositionClosed, FXOverdose.AI.DialoguePriority.High);

            // 💡 [단타 어뷰징 방지] 플레이어 수동 조작 모드이거나 플레이어가 직접 연 포지션이 종료되었을 때 매매 쿨타임 적용
            if (activeTradingMode == TradingMode.Player_Manual || currentOwner == OwnerType.Player)
            {
                playerTradeCooldownEndTime = Time.time + PlayerTradeCooldownSeconds;
            }

            // 💡 [이벤트 순서 수정] 이벤트 수신자가 활성 증거금 및 PnL ROE를 정확히 읽을 수 있도록 청산 상태 초기화 직전에 발송!
            OnPositionClosed?.Invoke(totalReturn, pnl);

            currentPosition = PositionType.None;
            currentOwner = OwnerType.AI;
            targetPrice = 0f;
            stopLossPrice = 0f;
            lastReportedROE = 0f;
            if (isOverdoseTradeActive)
            {
                isOverdoseTradeActive = false;
                overdoseProtectionEndTime = -1f;
                if (marketEngine != null) marketEngine.CancelOverdoseTrapSignal();
            }
            // 포지션 정리 뒤 총자산을 다시 계산해 반환금이 없는 종료도 엔딩 판정에서 누락되지 않게 합니다.
            gameManager?.EvaluateEndingConditions();
            isEventTradeActive = false;
            currentEventHandlingMode = EventPositionHandlingMode.StandardAuto;
            eventTargetROELimit = 0f;
            eventStopLossROELimit = 0f;
            isEventPlayerChoice = false;
            isEventTrueSignal = true;
            IsManualModeLockedByYomi = false;
            maxObservedEventROE = 0f;
            lastReportedROEBasket = 0;
            OnPositionChanged?.Invoke();
        }

        // 실시간 미실현 손익 (Unrealized PnL) 계산
        public float CalculateUnrealizedPnL()
        {
            if (currentPosition == PositionType.None || marketEngine == null || entryPrice <= 0f)
            {
                return 0f;
            }

            // 롱(Long) 청산 시 매수호가(Bid)로 팔고, 숏(Short) 청산 시 매도호가(Ask)로 삼
            float currentPriceToUse = currentPosition == PositionType.Long ? marketEngine.CurrentBidPrice : marketEngine.CurrentAskPrice;
            float priceDiff = currentPosition == PositionType.Long 
                ? (currentPriceToUse - entryPrice) 
                : (entryPrice - currentPriceToUse);

            float roeDecimal = (priceDiff / entryPrice) * currentLeverage;
            return marginAmount * roeDecimal;
        }

        // 실시간 ROE (%) 수익률 계산
        public float CalculateROEPercentage()
        {
            if (marginAmount <= 0f) return 0f;
            return (CalculateUnrealizedPnL() / marginAmount) * 100f;
        }

        // 강제 청산(Liquidation) 판정
        private void CheckLiquidation(float currentPrice)
        {
            if (currentPosition == PositionType.None) return;
            if (liquidationPrice <= 0f) return;

            // 청산 기준가는 스프레드가 적용된 호가
            float evalPrice = currentPosition == PositionType.Long ? marketEngine.CurrentBidPrice : marketEngine.CurrentAskPrice;

            // [슬로우 모션 기믹] 청산 마진콜 임박 (청산가까지 주가 여유 0.5% 미만)
            if (!isMarginCallSlowMotionTriggered)
            {
                float priceDiffPct = Mathf.Abs(evalPrice - liquidationPrice) / evalPrice;
                if (priceDiffPct < 0.005f)
                {
                    isMarginCallSlowMotionTriggered = true;
                    if (FXOverdose.Core.DynamicTimeRegulator.Instance != null)
                    {
                        FXOverdose.Core.DynamicTimeRegulator.Instance.TriggerDramaticSlowMotion(3.0f, 3.0f);
                        Debug.Log("[TradingController] ⚠️ 마진콜 청산 임박! (여유 0.5% 미만) 극적 연출을 위해 3초간 슬로우 모션(3배 감속) 가동.");
                    }
                }
            }

            // ⭐ [이벤트 포지션 초반 휩소 보호] 이벤트 등으로 포지션을 개설한 직후 3초 이내에는 순간적인 꼬리 스파이크 노이즈 1틱에 의한 억울한 즉사 청산을 방어!
            if (Time.time < eventPositionOpenedTime + 3.0f)
            {
                return;
            }



            bool isLiquidated = false;
            if (currentPosition == PositionType.Long && evalPrice <= liquidationPrice)
            {
                isLiquidated = true;
            }
            else if (currentPosition == PositionType.Short && evalPrice >= liquidationPrice)
            {
                isLiquidated = true;
            }

            if (isLiquidated)
            {
                TriggerLiquidation();
            }
        }

        // 강제 청산 실행
        private void TriggerLiquidation()
        {
            Debug.LogWarning($"[TradingController] ⚡ 강제 청산(Liquidation) 발생! 포지션 소멸. (청산가: {liquidationPrice:N1}, 현재가: {marketEngine.CurrentPrice:N1})");

            lastMarginAmount = marginAmount;

            // AI 트레이더 멘탈 붕괴 (Overdose 가속)
            if (traderStatus != null)
            {
                // 기획 요청: 돌발 이벤트 실패로 인한 강제 청산일 경우 멘탈 감소 기믹 제거
                if (isEventTradeActive && !isEventTrueSignal)
                {
                    Debug.Log("[TradingController] 이벤트 실패(트랩)로 인한 청산이므로 멘탈 감소(-40)를 면제합니다.");
                }
                else
                {
                    traderStatus.ChangeMental(-40f);
                }
            }

            // 💡 [이벤트 순서 수정] 강제청산 이벤트 발송을 포지션/증거금 초기화 직전에 진행하여 수신자가 손실 정보를 인지할 수 있게 보완
            OnPositionLiquidated?.Invoke();

            // 증거금 전액 몰수 (정산금 0원)
            marginAmount = 0f;
            currentPosition = PositionType.None;
            targetPrice = 0f;
            stopLossPrice = 0f;
            bool wasOverdoseTrade = isOverdoseTradeActive;
            if (isOverdoseTradeActive || (gameManager != null && gameManager.CurrentBalance <= 0.01f && traderStatus != null && traderStatus.CurrentMentalState == TraderStatus.MentalState.Overdose))
            {
                isOverdoseTradeActive = false;
                overdoseProtectionEndTime = -1f;
                if (marketEngine != null) marketEngine.CancelOverdoseTrapSignal();

                if (gameManager != null && gameManager.CurrentBalance <= 0.01f && (wasOverdoseTrade || (traderStatus != null && traderStatus.CurrentMentalState == TraderStatus.MentalState.Overdose)))
                {
                    Debug.LogWarning("[TradingController] 🩸 [Overdose 확정 게임오버] 올인 풀레버리지 포지션 청산 및 잔고 0원 도달 -> Overdose 엔딩 발동!");
                    gameManager.TriggerOverdoseEnding();
                }
            }
            // 증거금이 완전히 소멸한 뒤 총자산을 재평가해야 일반 올인 청산도 Bankruptcy로 연결됩니다.
            gameManager?.EvaluateEndingConditions();
            isEventTradeActive = false;
            currentEventHandlingMode = EventPositionHandlingMode.StandardAuto;
            eventTargetROELimit = 0f;
            eventStopLossROELimit = 0f;
            isEventPlayerChoice = false;
            isEventTrueSignal = true;
            maxObservedEventROE = 0f;
            lastReportedROEBasket = 0;
            OnPositionChanged?.Invoke();
        }

        private float lastOverdoseTradeTime = -100f;

        // 멘헤라 AI 트레이더 폭주(Overdose 상태) 시 호출되는 고레버리지 뇌동매매 실행 함수
        public bool IsOverdoseProtected()
        {
            return isOverdoseTradeActive && Time.time < overdoseProtectionEndTime - 1.0f;
        }

        public void TriggerOverdoseTrade()
        {
            if (IsAITradingLockedByGameMode)
            {
                Debug.Log("[TradingController] CHALLENGE 모드: Overdose AI 강제매매를 건너뜁니다.");
                return;
            }

            if (gameManager == null || marketEngine == null) return;
            
            // ⭐ [AI 통제권 강제 탈환] Overdose 상태 진입 시 플레이어 수동 조작 상태여도 AI가 통제권을 강제로 빼앗음
            if (activeTradingMode == TradingMode.Player_Manual)
            {
                Debug.LogWarning("[TradingController] 🩸 [Overdose 폭주] 플레이어 수동 조작 모드 강제 해제! AI가 제어권을 탈환합니다!");
                activeTradingMode = TradingMode.AI_Auto;
                OnTradingModeChanged?.Invoke(activeTradingMode);
            }

            // ⭐ [고속 스킵 일시정지] 오버도즈 골든타임 보장을 위해 진행 중인 고속 스킵 중단
            gameManager.PauseFastForwardForOverdose();

            // ⭐ [이벤트 포지션 쉴드 무시 및 강제 반대매매 탕진] 돌발 이벤트가 진행 중이어도 Overdose가 발생하면 이벤트를 파괴하고 쉴드를 해제합니다.
            if (Time.time < eventProtectionEndTime && !isOverdoseTradeActive)
            {
                Debug.LogWarning("[TradingController] 🩸 [Overdose 폭주] 진행 중인 돌발 이벤트 보호 쉴드를 파괴하고 탕진 매매를 시작합니다!");
                eventProtectionEndTime = -1f;
                isEventTradeActive = false; // 💡 핵심 원인: 이벤트 플래그를 해제하지 않으면 Overdose 35초 보호 쉴드가 무시되어 즉사 청산이 발생합니다.
            }

            // 과도한 중복 호출 방지 (최근 3초 이내에 Overdose 매매가 실행되었고 현재 보유 포지션이 있다면 추가 호출 스킵)
            if (Time.time - lastOverdoseTradeTime < 3f && currentPosition != PositionType.None && isOverdoseTradeActive)
            {
                return;
            }

            lastOverdoseTradeTime = Time.time;
            Debug.LogWarning("[TradingController] 🩸 [Overdose 폭주] AI 트레이더가 통제를 벗어나 100% 올인 풀레버리지 뇌동매매를 강행합니다!");

            PositionType forcedDirection = UnityEngine.Random.value > 0.5f ? PositionType.Long : PositionType.Short;

            // 만약 기존 포지션이 존재한다면 (수익/손실 여부와 관계없이) 이를 강제 청산하고 반대 방향 풀레버리지 매매로 전환
            if (currentPosition != PositionType.None)
            {
                forcedDirection = currentPosition == PositionType.Long ? PositionType.Short : PositionType.Long;
                Debug.LogWarning($"[TradingController] 🩸 [Overdose 폭주] 기존 포지션({currentPosition})을 뒤엎고 반대 방향({forcedDirection}) 풀레버리지 반대매매로 스위칭합니다!");
                ClosePosition();
            }
            else if (marketEngine.CurrentSignalPhase != SignalPhase.None && Mathf.Abs(marketEngine.ActiveSignal.TargetPercentageDelta) > 0.01f)
            {
                // 💡 이벤트 차트 빔 방향의 정확히 반대 방향으로 배팅하여 확정 청산을 유도합니다.
                forcedDirection = marketEngine.ActiveSignal.TargetPercentageDelta > 0f ? PositionType.Short : PositionType.Long;
            }

            // ⭐ [최후의 발악 보증금] 기존 포지션 청산으로 잔고가 완전히 0이 되었더라도, 극적 연출과 생존 기회를 위해 최소 100달러를 강제 대출/부여하여 올인하게 만듭니다.
            if (gameManager.CurrentBalance <= 1f)
            {
                gameManager.ChangeBalance(100f - gameManager.CurrentBalance);
                Debug.LogWarning("[TradingController] 🩸 잔고가 완전히 소멸했지만, Overdose 최후의 발악을 위해 100달러를 강제 부여합니다.");
            }

            // ⭐ [확실한 0원 청산 보장 All-In] 잔고의 일부가 아닌 전액 100% 올인하여 청산 시 잔고 0원(Overdose 게임오버)을 확실히 보장합니다!
            float forcedMargin = gameManager.CurrentBalance;
            int forcedLeverage = 125;

            float currentP = marketEngine.CurrentPrice;
            float aiTarget = forcedDirection == PositionType.Long ? currentP * 2.5f : currentP * 0.2f;

            if (forcedMargin > 1f)
            {
                StartCoroutine(DelayedOverdoseRoutine(forcedDirection, forcedMargin, forcedLeverage, aiTarget));
            }
        }

        private global::System.Collections.IEnumerator DelayedOverdoseRoutine(PositionType forcedDirection, float forcedMargin, int forcedLeverage, float aiTarget)
        {
            LockManualModeTemporarily(2.0f);
            OutputSpecificEventDialogue("OverdoseExecute", priority: FXOverdose.AI.DialoguePriority.High, cat: FXOverdose.AI.EventCategory.MentalChange, overrideLeverage: forcedLeverage);
            
            yield return new WaitForSecondsRealtime(2.0f);

            if (gameManager != null && gameManager.CurrentState == GameManager.GameState.GameOver) yield break;

            if (gameManager != null && gameManager.CurrentState == GameManager.GameState.Settlement)
            {
                Debug.Log("[TradingController] 일일 정산(Settlement) 중이므로 Overdose 강제 진입을 취소합니다.");
                yield break;
            }

            if (traderStatus != null && traderStatus.CurrentMentalState != TraderStatus.MentalState.Overdose)
            {
                Debug.Log("[TradingController] 2초 대기 중 멘탈 회복 감지! Overdose 강제 진입을 취소합니다.");
                yield break;
            }

            isOverdoseTradeActive = true;
            overdoseProtectionEndTime = Time.time + 35f;

            if (marketEngine != null) marketEngine.TriggerOverdoseTrapSignal(forcedDirection, 35);

            OpenPosition(forcedDirection, forcedMargin, forcedLeverage, aiTarget, 0f, true);
        }

        public void ExecuteEmergencyTrade(PositionType posType, int leverage, int durationSeconds = 30, EventPositionHandlingMode handlingMode = EventPositionHandlingMode.StandardAuto, float customTargetROE = 0f, float customStopLossROE = 0f, bool isPlayerChoice = false, bool isTrueSignal = true, float customMarginRatio = -1f, float delayBeforeOpen = 0f)
        {
            if (delayBeforeOpen > 0f)
            {
                StartCoroutine(DelayedEmergencyTradeRoutine(posType, leverage, durationSeconds, handlingMode, customTargetROE, customStopLossROE, isPlayerChoice, isTrueSignal, customMarginRatio, delayBeforeOpen));
                return;
            }

            if (gameManager == null) gameManager = UnityEngine.Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            if (marketEngine == null) marketEngine = UnityEngine.Object.FindAnyObjectByType<MarketSimulationEngine>(FindObjectsInactive.Include);
            if (traderStatus == null) traderStatus = TraderStatus.CanonicalInstance;

            if (posType == PositionType.None && leverage <= 0)
            {
                CloseAllPositions();
                return;
            }

            if (IsAITradingLockedByGameMode && !isPlayerChoice)
            {
                Debug.Log("[TradingController] CHALLENGE 모드: AI/기믹의 강제 포지션 진입을 건너뜁니다.");
                return;
            }

            // 💡 [핵심 수정] 뇌동매매 등 기믹 폭주 발동 시, 강제로 AI 모드로 탈환합니다. (수동 조작 잠금은 해제)
            if (!isPlayerChoice)
            {
                Debug.LogWarning("[TradingController] ⚡ [기믹 폭주] AI가 뇌동매매를 위해 플레이어 통제권을 강탈합니다!");
                if (activeTradingMode != TradingMode.AI_Auto)
                {
                    activeTradingMode = TradingMode.AI_Auto;
                    OnTradingModeChanged?.Invoke(activeTradingMode);
                }
            }

            // ⭐ [Overdose 통수 로직] 이미 Overdose 중일 때 이벤트 선택지가 들어오면, 무조건 이벤트 신호의 반대 방향으로 풀레버리지 탕진 스위칭
            if (isOverdoseTradeActive)
            {
                PositionType eventDirection = posType;
                if (eventDirection == PositionType.None && marketEngine != null && marketEngine.CurrentSignalPhase != SignalPhase.None)
                {
                    eventDirection = marketEngine.ActiveSignal.TargetPercentageDelta >= 0 ? PositionType.Long : PositionType.Short;
                }
                if (eventDirection == PositionType.None) eventDirection = PositionType.Long;
                PositionType reverseDirection = eventDirection == PositionType.Long ? PositionType.Short : PositionType.Long;

                Debug.LogWarning($"[TradingController] 🩸 [Overdose 폭주 중] 돌발 이벤트 진입 감지! 이벤트 의도({eventDirection})를 무시하고 확정 청산을 위해 반대 방향({reverseDirection}) 125배 풀레버리지로 통수 스위칭합니다!");
                
                if (currentPosition != PositionType.None) ClosePosition();
                
                float forcedMargin = gameManager.CurrentBalance;
                int forcedLeverage = 125;
                float currentP = marketEngine.CurrentPrice;
                float aiTarget = reverseDirection == PositionType.Long ? currentP * 2.5f : currentP * 0.2f;

                if (forcedMargin > 1f)
                {
                    isOverdoseTradeActive = true;
                    overdoseProtectionEndTime = Time.time + 35f;
                    if (marketEngine != null) marketEngine.TriggerOverdoseTrapSignal(reverseDirection, 35);

                    OpenPosition(reverseDirection, forcedMargin, forcedLeverage, aiTarget, 0f, true);
                }
                return;
            }

            // 💡 [핵심 수정] 만약 옵션에서 방향(posType)을 지정하지 않고 배율(leverage > 0)만 지정했다면 (예: "초단타 50배 진입을 허용한다!")
            if (posType == PositionType.None && leverage > 0)
            {
                if (currentPosition != PositionType.None)
                {
                    posType = currentPosition;
                }
                else if (marketEngine != null && marketEngine.CurrentSignalPhase != SignalPhase.None)
                {
                    posType = marketEngine.ActiveSignal.TargetPercentageDelta >= 0 ? PositionType.Long : PositionType.Short;
                }
                else
                {
                    posType = PositionType.Long;
                }
                Debug.Log($"[TradingController] ⚡ 방향 미지정 고배율 이벤트 선택(레버리지 {leverage}배) -> 자동 방향 설정({posType})으로 강제 진입 실행");
            }

            // ⭐ 이벤트 선택 우선: 기존 포지션이 존재하면(방향이 같든 다르든) 무조건 먼저 정산/청산하여 이벤트 포지션을 개설합니다!
            if (currentPosition != PositionType.None)
            {
                Debug.Log($"[TradingController] ⚡ 돌발 이벤트 선택지({posType} {leverage}배) 우선 적용을 위해 기존 {currentPosition} 포지션을 정산합니다.");
                ClosePosition();
                if (gameManager != null && gameManager.CurrentState == GameManager.GameState.GameOver)
                {
                    return;
                }
            }

            // 이벤트가 요구하는 새로운 포지션 및 레버리지로 즉시 강제 진입 (이벤트 팝업 중 Paused 상태에서도 개설 보장)
            if (gameManager != null && (gameManager.CurrentState == GameManager.GameState.Playing || gameManager.CurrentState == GameManager.GameState.Paused) && marketEngine != null)
            {
                float effectiveBalance = gameManager.CurrentBalance > 1f ? gameManager.CurrentBalance : 100f; // 잔고가 1원 이하이거나 0원일 때도 이벤트 효과로 포지션이 개설되도록 최소 보증금 적용
                float marginRatio = customMarginRatio > 0f ? customMarginRatio : 0.4f;
                float forcedMargin = Mathf.Max(10f, effectiveBalance * marginRatio);
                if (forcedMargin > gameManager.CurrentBalance && gameManager.CurrentBalance > 1f)
                {
                    forcedMargin = gameManager.CurrentBalance * (customMarginRatio > 0f ? customMarginRatio : 0.95f);
                }
                if (forcedMargin > 0f)
                {
                    float currentP = marketEngine.CurrentPrice;
                    float aiTarget = posType == PositionType.Long ? currentP * 1.15f : currentP * 0.85f;
                    float aiStop = posType == PositionType.Long ? currentP * 0.95f : currentP * 1.05f;

                    float protectDuration = Mathf.Max(30.0f, durationSeconds > 0 ? durationSeconds : 30.0f);
                    eventProtectionEndTime = Time.time + protectDuration; // 실시간 이벤트 쉴드 가동

                    isEventTradeActive = true;
                    currentEventHandlingMode = handlingMode;
                    eventTargetROELimit = customTargetROE;
                    eventStopLossROELimit = customStopLossROE;
                    isEventPlayerChoice = isPlayerChoice;
                    isEventTrueSignal = isTrueSignal;
                    maxObservedEventROE = 0f;
                    lastReportedROEBasket = 0;

                    OpenPosition(posType, forcedMargin, Mathf.Clamp(leverage, 1, 125), aiTarget, aiStop, true,
                        isPlayerDirectedTrade: isPlayerChoice);
                    Debug.Log($"[TradingController] ⚡ ExecuteEmergencyTrade (돌발 이벤트 강제 진입) 완료: {posType} / 증거금 ${forcedMargin:N0} / 레버리지 {leverage}배 ({protectDuration}초 이벤트 쉴드 가동, 모드: {handlingMode}, 플레이어선택: {isPlayerChoice}, 진위: {isTrueSignal})");
                }
            }
        }

        private global::System.Collections.IEnumerator DelayedEmergencyTradeRoutine(PositionType posType, int leverage, int durationSeconds, EventPositionHandlingMode handlingMode, float customTargetROE, float customStopLossROE, bool isPlayerChoice, bool isTrueSignal, float customMarginRatio, float delayBeforeOpen)
        {
            if (!isPlayerChoice)
            {
                LockManualModeTemporarily(delayBeforeOpen);
            }
            yield return new WaitForSecondsRealtime(delayBeforeOpen);
            
            if (gameManager != null && gameManager.CurrentState == GameManager.GameState.GameOver) yield break;
            
            ExecuteEmergencyTrade(posType, leverage, durationSeconds, handlingMode, customTargetROE, customStopLossROE, isPlayerChoice, isTrueSignal, customMarginRatio, 0f);
        }

        public void CloseAllPositions(bool isZeroFee = false)
        {
            if (currentPosition != PositionType.None)
            {
                ClosePosition();
                if (isZeroFee)
                {
                    Debug.Log("[TradingController] ⚡ 수수료 면제(Zero-Fee) 혜택으로 포지션 청산 완료.");
                }
            }
        }

        private void OutputSpecificEventDialogue(string eventCategory, float roe = 0f, float maxObserved = 0f, FXOverdose.AI.DialoguePriority priority = FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.EventCategory cat = FXOverdose.AI.EventCategory.General, int overrideLeverage = -1)
        {
            var visual = UnityEngine.Object.FindAnyObjectByType<FXOverdose.AI.AIVisualController>(UnityEngine.FindObjectsInactive.Include);
            if (visual == null) return;

            var matcher = FXOverdose.AI.Dialogue.YomiDialogueMatcher.Instance;
            if (matcher != null)
            {
                int lev = overrideLeverage >= 0 ? overrideLeverage : currentLeverage;
                float marginRatio = 0f;
                if (gameManager != null && this.marginAmount > 0f)
                {
                    marginRatio = this.marginAmount / (this.marginAmount + gameManager.CurrentBalance);
                }
                
                string text = matcher.GetEventDialogue(eventCategory, lev, marginRatio, roe, maxObserved);
                if (!string.IsNullOrEmpty(text))
                {
                    visual.DisplayDialogueBalloon(text, priority, cat);
                    return;
                }
            }
            
            visual.DisplayDialogueBalloon($"[{eventCategory}] 이벤트 발생!", priority, cat);
        }

        private void OutputYomiDialogue(FXOverdose.AI.EventCategory cat, FXOverdose.AI.DialoguePriority priority = FXOverdose.AI.DialoguePriority.Normal)
        {
            var visual = UnityEngine.Object.FindAnyObjectByType<FXOverdose.AI.AIVisualController>(UnityEngine.FindObjectsInactive.Include);
            if (visual == null) return;
            
            float roe = CalculateROEPercentage();
            string posStr = currentPosition.ToString();
            
            string matchedDialogue = "";
            var matcher = FXOverdose.AI.Dialogue.YomiDialogueMatcher.Instance;
            if (matcher == null) matcher = UnityEngine.Object.FindAnyObjectByType<FXOverdose.AI.Dialogue.YomiDialogueMatcher>(UnityEngine.FindObjectsInactive.Include);

            if (matcher != null)
            {
                var mentalState = TraderStatus.CanonicalInstance != null
                    ? TraderStatus.CanonicalInstance.CurrentMentalState.ToString()
                    : TraderStatus.MentalState.Stable.ToString();

                FXOverdose.AI.Dialogue.DirectionTag direction = FXOverdose.AI.Dialogue.DirectionTag.None;
                if (currentPosition == PositionType.Long)
                    direction = roe >= 0f ? FXOverdose.AI.Dialogue.DirectionTag.Up : FXOverdose.AI.Dialogue.DirectionTag.Down;
                else if (currentPosition == PositionType.Short)
                    direction = roe >= 0f ? FXOverdose.AI.Dialogue.DirectionTag.Down : FXOverdose.AI.Dialogue.DirectionTag.Up;

                string marketTrend = direction == FXOverdose.AI.Dialogue.DirectionTag.Up
                    ? "Bull"
                    : direction == FXOverdose.AI.Dialogue.DirectionTag.Down ? "Bear" : "Sideways";

                int currentLeverage = this.currentLeverage;
                float marginRatio = 0f;
                if (gameManager != null && gameManager.CurrentBalance + this.marginAmount > 0f)
                {
                    marginRatio = this.marginAmount / (this.marginAmount + gameManager.CurrentBalance);
                }

                var levelSystem = FXOverdose.Trading.TraderLevelSystem.Instance;
                int heroLevel = levelSystem != null ? levelSystem.ProtagonistLevel : 1;
                int skillLevel = levelSystem != null ? Mathf.Max(levelSystem.ChartStudyLevel, levelSystem.CubePatienceLevel, levelSystem.BookJudgmentLevel) : 1;

                matchedDialogue = matcher.GetDialogue(
                    posStr, marketTrend, mentalState, direction, roe >= 0f, currentLeverage, marginRatio, heroLevel, skillLevel);
            }
            
            if (!string.IsNullOrEmpty(matchedDialogue))
            {
                visual.DisplayDialogueBalloon(matchedDialogue, priority, cat);
            }
        }
    }
}
