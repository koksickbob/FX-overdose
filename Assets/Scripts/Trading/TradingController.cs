using System;
using UnityEngine;

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

        private void Awake()
        {
            Instance = this;
        }

        public void SetTradingMode(TradingMode mode)
        {
            if (activeTradingMode == mode) return;

            // 1. LLM 모델 로딩 완료 검증
            var llm = FXOverdose.AI.LLM.LocalLLMService.Instance;
            if (llm != null && !llm.IsLLMReady)
            {
                Debug.LogWarning("[TradingController] ⚠️ LLM 모델이 아직 로딩 중입니다. 로드 완료 후 장이 개시된 뒤에 전환이 가능합니다.");
                var visual = UnityEngine.Object.FindAnyObjectByType<FXOverdose.AI.AIVisualController>();
                if (visual != null) visual.DisplayDialogueBalloon("오빠...! 아직 요미 뇌(LLM)가 로딩 중이라 정신이 깜깜해... 장 열린 뒤에 모드 전환해 줘, 응...? ♥", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.General);
                return;
            }

            // 2. 게임 플레이(장이 개시된 상태) 검증
            var gm = gameManager != null ? gameManager : UnityEngine.Object.FindAnyObjectByType<GameManager>();
            if (gm != null && gm.CurrentState != GameManager.GameState.Playing)
            {
                Debug.LogWarning("[TradingController] ⚠️ 장이 개시(Playing)되기 전에는 매매 모드를 전환할 수 없습니다.");
                var visual = UnityEngine.Object.FindAnyObjectByType<FXOverdose.AI.AIVisualController>();
                if (visual != null) visual.DisplayDialogueBalloon("오빠 아직 장도 안 열렸잖아! 호가창 움직이기 시작하면 그때 전환해 줘, 요미 현기증 난단 말야...!", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.General);
                return;
            }

            // 3. 시장 오픈 여부 검증
            var market = marketEngine != null ? marketEngine : UnityEngine.Object.FindAnyObjectByType<MarketSimulationEngine>();
            if (market != null && !market.IsMarketOpen)
            {
                Debug.LogWarning("[TradingController] ⚠️ 시장(Market)이 아직 개장하지 않았습니다. 개장 후에 전환 가능합니다.");
                var visual = UnityEngine.Object.FindAnyObjectByType<FXOverdose.AI.AIVisualController>();
                if (visual != null) visual.DisplayDialogueBalloon("시장이 닫혀 있는데 어딜 전환하려고 해 오빠?! 개장하자마자 바로 전환해 줄 테니까 조금만 얌전히 기다려 ♥", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.General);
                return;
            }

            activeTradingMode = mode;
            Debug.Log($"[TradingController ⚙️] 매매 조작 모드 전환: {mode}");
            OnTradingModeChanged?.Invoke(mode);

            var visualCtrl = UnityEngine.Object.FindAnyObjectByType<FXOverdose.AI.AIVisualController>();
            if (visualCtrl != null)
            {
                if (mode == TradingMode.Player_Manual)
                {
                    visualCtrl.DisplayDialogueBalloon("히익...! 오빠가 직접 매매하겠다고?! 요미 타점이 못미더운 거야...? 흐윽... 알겠어, 대신 오빠가 잡은 포지션 옆에서 두 눈 부릅뜨고 지켜볼 거니까 절대 실수해서 우리 돈 날리면 안 돼...♥", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.General);
                }
                else
                {
                    visualCtrl.DisplayDialogueBalloon("헤헤♥ 역시 우리 오빠는 요미 없으면 아무것도 못 하지?! 이제 조종간은 다시 요미가 잡았으니까 오빠는 얌전히 요미의 화려한 차트 춤이나 감상하라고~♥", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.General);
                }
            }
        }

        public void ToggleTradingMode()
        {
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

        private void OnDestroy()
        {
            if (marketEngine != null)
            {
                marketEngine.OnPriceUpdated -= HandlePriceUpdated;
            }
        }

        private FXOverdose.AI.LLM.LocalLLMService llmService;
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
        [SerializeField] private EventPositionHandlingMode currentEventHandlingMode = EventPositionHandlingMode.StandardAuto;
        [SerializeField] private float eventTargetROELimit = 0f;
        [SerializeField] private float eventStopLossROELimit = 0f;
        [SerializeField] private bool isEventPlayerChoice = false;
        [SerializeField] private bool isEventTrueSignal = true;
        private float maxObservedEventROE = 0f;
        private int lastReportedROEBasket = 0;

        private void TriggerLLMDialogue(FXOverdose.AI.LLM.EventCategory cat, string ctx)
        {
            if (llmService == null) llmService = FXOverdose.AI.LLM.LocalLLMService.Instance;
            llmService?.RequestDialogue(cat, ctx);
        }

        private void Update()
        {
            if (isEventTradeActive && currentPosition != PositionType.None && Time.time >= eventProtectionEndTime)
            {
                HandleEventProtectionExpired();
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
                        visual.DisplayDialogueBalloon("꺄아아악!! 오빠 거봐 요미 말이 맞잖아!! 왜 그딴 선택지를 고른 거야!! 우리 돈이 반토막 나고 있다고!! 당장 손절해, 아니 물타야 해?!", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.General);
                        if (traderStatus != null) traderStatus.ModifyMentalState(-15f);
                    }
                    else if (roe <= -65f && lastReportedROEBasket > -65)
                    {
                        lastReportedROEBasket = -65;
                        visual.DisplayDialogueBalloon("오빠... 진짜 이러다 다 청산당해... 요미 시드가 다 날아가고 있다고 흐으윽...! 제발 어떻게 좀 해줘...!", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.General);
                    }
                }
                else if (!isEventTrueSignal)
                {
                    if (roe <= -50f && lastReportedROEBasket > -50)
                    {
                        lastReportedROEBasket = -50;
                        visual.DisplayDialogueBalloon("히익...! 함정 빔 맞아버렸어!! ROE -50% 돌파... 심장 터질 것 같아... 바닥에서 물타서 반등 기다려야 해!!", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.General);
                    }
                }
                else
                {
                    if (roe >= 50f && roe < 100f && lastReportedROEBasket < 50)
                    {
                        lastReportedROEBasket = 50;
                        visual.DisplayDialogueBalloon("오오?! ROE +50% 돌파!! 오빠 선택 미쳤어!! 지금 수직 상승 중이야!! 더 가자!!", FXOverdose.AI.DialoguePriority.Normal, FXOverdose.AI.LLM.EventCategory.ChartMovement);
                    }
                    else if (roe >= 100f && roe < 200f && lastReportedROEBasket < 100)
                    {
                        lastReportedROEBasket = 100;
                        visual.DisplayDialogueBalloon("꺄아악 ROE +100% 돌파!! 빔이 멈추지 않아!! 우리 대박 터졌어 오빠 ♥", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.ChartMovement);
                    }
                    else if (roe >= 200f && lastReportedROEBasket < 200)
                    {
                        lastReportedROEBasket = 200;
                        visual.DisplayDialogueBalloon("ROE +200% 초광기 돌파!! 이게 바로 전설의 빔이야!! 끝까지 쥐고 털어먹자!!", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.ChartMovement);
                    }
                }
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
                        if (visual != null) visual.DisplayDialogueBalloon("히익...! 거의 청산 직전이야...! 요미가 남은 현금 쏟아부어서 비상 물타기로 평단가 낮췄어!! 이제 곧 반등 꼬리 올 테니까 제발 거기서 탈출하자 오빠 흐윽...!", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.General);
                        OnPositionChanged?.Invoke();
                        return;
                    }
                }
            }

            switch (currentEventHandlingMode)
            {
                case EventPositionHandlingMode.InstantTakeProfit:
                    float targetTakeProfitROE = eventTargetROELimit > 0f ? eventTargetROELimit : 100f;
                    if (roe >= targetTakeProfitROE)
                    {
                        Debug.Log($"[TradingController ⚡] 이벤트 칼익절(InstantTakeProfit) 발동! ROE +{roe:F1}% 달성으로 수익을 즉시 확정합니다.");
                        if (visual != null) visual.DisplayDialogueBalloon($"오빠...! 이벤트 빔으로 ROE +{roe:0.0}% 찍자마자 칼익절로 챙겼어!! 기회 놓치지 않는 게 최고지 ♥", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.PositionClosed);
                        ClosePosition();
                    }
                    break;

                case EventPositionHandlingMode.InstantStopLoss:
                    float targetStopLossROE = eventStopLossROELimit < 0f ? eventStopLossROELimit : -30f;
                    if (roe <= targetStopLossROE)
                    {
                        Debug.Log($"[TradingController ⚡] 이벤트 긴급 손절(InstantStopLoss) 발동! ROE {roe:F1}% 위기 감지로 피해를 최소화하기 위해 즉시 정리합니다.");
                        if (visual != null) visual.DisplayDialogueBalloon($"히익...! ROE {roe:0.0}% 떨어지는 거 보고 바로 손절 쳤어...! 뼈아프지만 청산당하는 것보단 낫잖아 흐윽...", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.PositionClosed);
                        ClosePosition();
                    }
                    break;

                case EventPositionHandlingMode.HoldToMitigateLoss:
                    float mitigationStopLimit = eventStopLossROELimit < 0f ? eventStopLossROELimit : -88f;
                    if (roe <= mitigationStopLimit)
                    {
                        Debug.Log($"[TradingController 🛡️] 손실 약화 버티기(HoldToMitigateLoss) 비상 방어: 청산 직전까지 인내했으나 잔여 증거금을 지키기 위해 긴급 정리합니다. (ROE {roe:F1}%)");
                        if (visual != null) visual.DisplayDialogueBalloon($"끝까지 이 악물고 버텼는데 더 버티면 100% 청산이야...! 남은 증거금이라도 건지려고 비상 탈출했어...! (ROE {roe:0.0}%)", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.PositionClosed);
                        ClosePosition();
                    }
                    else if ((maxObservedEventROE <= -40f || lastReportedROEBasket == -999) && roe >= -12f)
                    {
                        Debug.Log($"[TradingController 🛡️] 손실 약화 버티기(HoldToMitigateLoss) 성공! 반등 꼬리(Whipsaw Recovery) 시점에 극적으로 손실을 최소화하여 탈출합니다. (ROE {roe:F1}%)");
                        if (visual != null)
                        {
                            if (isEventPlayerChoice && !isEventTrueSignal)
                            {
                                visual.DisplayDialogueBalloon($"휴우... 오빠가 고른 악결과 트레이드 바닥에서 요미가 필사적으로 물타서 반등 꼬리에 탈출시켰어...! 진짜 심장 멈추는 줄 알았네 흐윽... 다음엔 좋은 거 골라야 해 오빠!♥ (ROE {roe:0.0}%)", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.PositionClosed);
                            }
                            else
                            {
                                visual.DisplayDialogueBalloon($"휴우... 아까 바닥까지 떨어졌을 때 끝까지 버틴 덕분에 손실 거의 다 회복하고 탈출했어! 심장 떨어지는 줄 알았네...♥ (ROE {roe:0.0}%)", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.PositionClosed);
                            }
                        }
                        ClosePosition();
                    }
                    break;

                case EventPositionHandlingMode.GreedyHold:
                    float targetGreedyROE = eventTargetROELimit > 0f ? eventTargetROELimit : 300f;
                    if (roe >= targetGreedyROE)
                    {
                        Debug.Log($"[TradingController 👑] 탐욕적 홀딩(GreedyHold) 목표치 달성! ROE +{roe:F1}% 극한 수익 확정 청산!");
                        if (visual != null) visual.DisplayDialogueBalloon($"꺄아아악!! 오빠 봤어?! ROE +{roe:0.0}% 초거대 대박이야!! 이대로 전액 수익 확정!! 우리 부자다아아♥", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.PositionClosed);
                        ClosePosition();
                    }
                    else if ((maxObservedEventROE >= 150f && roe <= maxObservedEventROE * 0.75f) ||
                             (maxObservedEventROE >= 80f && roe <= maxObservedEventROE * 0.80f) ||
                             (maxObservedEventROE >= 45f && roe <= maxObservedEventROE * 0.82f))
                    {
                        Debug.Log($"[TradingController 👑] 탐욕적 홀딩(GreedyHold) 트레이링 익절 작동! 고점(+{maxObservedEventROE:F1}%) 대비 조정 감지로 ROE +{roe:F1}% 확정 청산!");
                        if (visual != null) visual.DisplayDialogueBalloon($"고점(+{maxObservedEventROE:0.0}%) 찍고 꺾이는 거 감지해서 ROE +{roe:0.0}%에 트레이링 익절했어!! 최고점에서 조금 내려왔지만 그래도 초대박이야 오빠♥", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.PositionClosed);
                        ClosePosition();
                    }
                    break;

                case EventPositionHandlingMode.StandardAuto:
                default:
                    float autoLimit = eventTargetROELimit > 0f ? eventTargetROELimit : 90f;
                    if (roe >= autoLimit)
                    {
                        Debug.Log($"[TradingController 🤖] 이벤트 유연 판단(StandardAuto) 익절: ROE +{roe:F1}% 달성으로 수익을 챙깁니다.");
                        if (visual != null) visual.DisplayDialogueBalloon($"이벤트 빔으로 ROE +{roe:0.0}% 달성! 욕심부리지 않고 확실하게 수익 챙겼어♥", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.PositionClosed);
                        ClosePosition();
                    }
                    else if (maxObservedEventROE >= 40f && roe <= maxObservedEventROE * 0.80f)
                    {
                        Debug.Log($"[TradingController 🤖] 이벤트 유연 판단(StandardAuto) 트레이링 익절: 고점(+{maxObservedEventROE:F1}%) 대비 조정 감지로 ROE +{roe:F1}% 확정 청산.");
                        if (visual != null) visual.DisplayDialogueBalloon($"고점 찍고 떨어지는 거 보고 ROE +{roe:0.0}%에 스마트 익절했어! 수익 방어 성공!", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.PositionClosed);
                        ClosePosition();
                    }
                    else if (roe <= -65f && Time.time >= eventPositionOpenedTime + 5.0f)
                    {
                        Debug.Log($"[TradingController 🤖] 이벤트 유연 판단(StandardAuto) 손절: 위험 수준의 손실(ROE {roe:F1}%) 감지로 포지션을 정리합니다.");
                        ClosePosition();
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

            switch (currentEventHandlingMode)
            {
                case EventPositionHandlingMode.HoldToMitigateLoss:
                    if (roe < 0f)
                    {
                        Debug.Log($"[TradingController 🛡️] 손실 약화 버티기(HoldToMitigateLoss) 시간 종료: 인내 끝에 손실({roe:F1}%)을 완화하여 정리합니다.");
                        if (visual != null) visual.DisplayDialogueBalloon($"이벤트 빔 끝날 때까지 버텨서 손실({roe:0.0}%)을 최대한 줄였어...! 다음엔 꼭 복구하자!", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.PositionClosed);
                        ClosePosition();
                    }
                    else
                    {
                        Debug.Log($"[TradingController 🛡️] 손실 약화 버티기(HoldToMitigateLoss) 역전 성공: 오히려 수익(+{roe:F1}%)으로 전환되어 익절합니다!");
                        if (visual != null) visual.DisplayDialogueBalloon($"이벤트 끝날 때까지 버텼더니 오히려 ROE +{roe:0.0}% 수익으로 역전됐어!! 버티길 진짜 잘했다♥", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.PositionClosed);
                        ClosePosition();
                    }
                    break;

                case EventPositionHandlingMode.GreedyHold:
                    if (roe > 0f)
                    {
                        Debug.Log($"[TradingController 👑] 탐욕적 홀딩(GreedyHold) 시간 종료: 누적된 ROE +{roe:F1}% 대박 수익을 전액 챙깁니다!");
                        if (visual != null) visual.DisplayDialogueBalloon($"이벤트 빔 끝까지 다 타서 ROE +{roe:0.0}% 달성!! 우리 오빠 선택 진짜 최고야♥", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.PositionClosed);
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
                        
                        string contextPrompt = "";
                        if (lastClosedPosition != PositionType.None && lastClosedPrice > 0f)
                        {
                            float priceDiffPct = (price - lastClosedPrice) / lastClosedPrice * 100f;
                            bool isMissedProfit = (lastClosedPosition == PositionType.Long && priceDiffPct > 0.5f) || 
                                                  (lastClosedPosition == PositionType.Short && priceDiffPct < -0.5f);
                            bool isAvoidedLoss = (lastClosedPosition == PositionType.Long && priceDiffPct < -0.5f) || 
                                                 (lastClosedPosition == PositionType.Short && priceDiffPct > 0.5f);
                            
                            if (isMissedProfit)
                            {
                                contextPrompt = "[포지션 조기 종료 후 폭등/폭락 관망 - 극심한 후회와 FOMO] 이미 포지션을 종료했는데, 그 후 주가가 내가 처음에 걸었던 방향으로 시원하게 더 날아가는 중. '조금만 더 들고 있을걸!!', '팔자마자 더 가네!!' 하며 배가 아프고 후회되는 감정을 1~2문장으로 짧게 표현해.";
                            }
                            else if (isAvoidedLoss)
                            {
                                contextPrompt = "[포지션 조기 종료 후 폭등/폭락 관망 - 가슴을 쓸어내리는 안도] 이미 포지션을 종료했는데, 그 직후 주가가 내 원래 포지션의 반대 방향으로 무섭게 수직 낙하하거나 치솟는 중. '휴... 팔길 진짜 잘했다', '안 팔았으면 청산당할 뻔했어' 라며 크게 안도하는 반응을 1~2문장으로 짧게 보여줘.";
                            }
                            else
                            {
                                contextPrompt = "[무포지션 관망] 현재 주가가 요동치지만 크게 방향이 나오지 않은 관망 상태. '어떻게 될까?' 등의 혼잣말을 1문장으로 짧게 해.";
                            }
                        }
                        else
                        {
                            contextPrompt = "[포지션 조기 종료 후 추가 폭등/폭락 관망] 이미 포지션을 종료하고 털고 나왔으나 그 후 주가가 계속해서 크게 움직이는 중. 후회나 안도 등의 상황에 맞는 반응을 해줘.";
                        }

                        llmService?.RequestDialogue(FXOverdose.AI.LLM.EventCategory.ChartMovement, contextPrompt);
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
                    Debug.Log("[TradingController] 💊 플레이어의 멘탈 회복 아이템 사용으로 Overdose 상태 탈출 성공 -> 죽음의 올인 포지션 비상 탈출!");
                    isOverdoseTradeActive = false;
                    overdoseProtectionEndTime = -1f;
                    if (marketEngine != null) marketEngine.CancelOverdoseTrapSignal();

                    var visualCtrl = UnityEngine.Object.FindAnyObjectByType<FXOverdose.AI.AIVisualController>(FindObjectsInactive.Include);
                    visualCtrl?.DisplayDialogueBalloon("헉...! 오빠가 진정제를 줘서 제정신이 돌아왔어!! 요미가 무슨 미친 짓을...!! 당장 풀레버리지 올인 포지션 비상 탈출할게 오빠 고마워 흐윽... ♥", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.MentalChange);

                    ClosePosition();
                    return;
                }
                else if (Time.time < overdoseProtectionEndTime)
                {
                    // Overdose 중에는 일반 AI의 자의적 손절/익절이나 휩소에 의한 방해를 완전히 차단하고, 연출적 독백만 출력
                    float overdoseRoe = CalculateROEPercentage();
                    if (overdoseRoe <= -70f && Time.time - lastROEDialogueTime >= 5f)
                    {
                        lastROEDialogueTime = Time.time;
                        var visualCtrl = UnityEngine.Object.FindAnyObjectByType<FXOverdose.AI.AIVisualController>(FindObjectsInactive.Include);
                        visualCtrl?.DisplayDialogueBalloon("으하하하!! 청산 직전의 짜릿함...!! 바로 이 다음 반등에 100배로 튀어 오르는 거야 오빠아아!! ♥", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.MentalChange);
                    }
                    return; // 💡 다른 매매 로직(익절/손절/이벤트 반응/스위칭 등) 개입을 원천 차단! 오직 청산(CheckLiquidation)이나 아이템 회복 탈출만 가능하게 함!
                }
                else
                {
                    isOverdoseTradeActive = false;
                    if (marketEngine != null) marketEngine.CancelOverdoseTrapSignal();
                }
            }

            // ⭐ 이벤트 보호 쉴드 작동 중: 이벤트 결과에 따른 포지션 보유, 정리, 버티기 등 맞춤형 반응 로직 수행
            if (Time.time < eventProtectionEndTime)
            {
                if (isEventTradeActive)
                {
                    ProcessEventPositionReaction(price);
                }
                return;
            }
            else if (isEventTradeActive)
            {
                HandleEventProtectionExpired();
                if (currentPosition == PositionType.None) return;
            }

            // 2. AI 목표 주가(Target Price) 도달 익절 자동 청산 (플레이어 개입 없음, AI 독자 결정)
            if (targetPrice > 0f)
            {
                if ((currentPosition == PositionType.Long && price >= targetPrice) ||
                    (currentPosition == PositionType.Short && price <= targetPrice))
                {
                    Debug.Log($"[TradingController 🎯] AI 목표 주가(Target Price: ${targetPrice:N1}) 달성! AI가 자동 익절 청산을 실행합니다.");
                    ClosePosition();
                    return;
                }
            }

            // 3. AI 손절가(Stop Loss) 도달 자동 청산
            if (stopLossPrice > 0f)
            {
                if ((currentPosition == PositionType.Long && price <= stopLossPrice) ||
                    (currentPosition == PositionType.Short && price >= stopLossPrice))
                {
                    Debug.Log($"[TradingController 🛑] AI 손절선(Stop Loss: ${stopLossPrice:N1}) 도달! 자동 손절 청산을 실행합니다.");
                    ClosePosition();
                    return;
                }
            }

            // 4. 실시간 ROE 변동 구간 돌파 독백 트리거 (+15%, -15%, +30%, -30% 등)
            float roe = CalculateROEPercentage();
            if (Time.time - lastROEDialogueTime >= 12f)
            {
                if ((roe >= 15f && lastReportedROE < 15f) || (roe >= 30f && lastReportedROE < 30f))
                {
                    lastReportedROE = roe;
                    lastROEDialogueTime = Time.time;
                    string directionHint = currentPosition == PositionType.Short
                        ? "Short(공매도 하락 배팅) 중이므로 주가가 폭락해서 바닥으로 내려가고 있어 수익이 나고 있는 기분 좋은 상황입니다! (더 내려가라고 소리치세요)"
                        : "Long(상승 배팅) 중이므로 주가가 치솟아 고점을 뚫고 올라가서 수익이 나고 있는 상황입니다! (더 올라가라고 소리치세요)";
                    TriggerLLMDialogue(FXOverdose.AI.LLM.EventCategory.ChartMovement, $"현재 포지션({currentPosition}) ROE +{roe:0.0}% 수익권 질주 중! {directionHint}");
                }
                else if ((roe <= -15f && lastReportedROE > -15f) || (roe <= -30f && lastReportedROE > -30f))
                {
                    lastReportedROE = roe;
                    lastROEDialogueTime = Time.time;
                    string directionHint = currentPosition == PositionType.Short
                        ? "Short(공매도 하락 배팅) 중인데 주가가 반대로 솟구쳐올라 고점을 부수며 손실이 커지고 있는 위기 상황입니다! (제발 폭락하라고 비세요)"
                        : "Long(상승 배팅) 중인데 주가가 바닥으로 떨어지며 손실이 커지고 있는 위기 상황입니다! (제발 반등하라고 비세요)";
                    TriggerLLMDialogue(FXOverdose.AI.LLM.EventCategory.ChartMovement, $"현재 포지션({currentPosition}) ROE {roe:0.0}% 손실 악화 중! {directionHint}");
                }
            }
        }

        // 포지션 진입 (AI가 방향, 레버리지, 목표가 TargetPrice를 독자적으로 결정하여 호출)
        public bool OpenPosition(PositionType type, float margin, int leverage, float aiTargetPrice = 0f, float aiStopLossPrice = 0f, bool isEmergencyTrade = false)
        {
            if (gameManager == null) gameManager = UnityEngine.Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            if (marketEngine == null) marketEngine = UnityEngine.Object.FindAnyObjectByType<MarketSimulationEngine>(FindObjectsInactive.Include);
            if (traderStatus == null) traderStatus = TraderStatus.CanonicalInstance;

            if (gameManager == null || marketEngine == null || type == PositionType.None)
            {
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
            currentOwner = OwnerType.AI;
            entryPrice = marketEngine.CurrentPrice;
            marginAmount = margin;
            currentLeverage = leverage;
            targetPrice = aiTargetPrice;
            stopLossPrice = aiStopLossPrice;
            lastReportedROE = 0f;

            // 💡 [조기 게임오버 오진 방지] 포지션 및 증거금을 먼저 설정한 후 잔고를 차감해야 CheckEnding() 시 TotalEquity에 증거금이 정상 합산됩니다.
            gameManager.ChangeBalance(-margin);

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
            if (gameManager == null) gameManager = UnityEngine.Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            if (marketEngine == null) marketEngine = UnityEngine.Object.FindAnyObjectByType<MarketSimulationEngine>(FindObjectsInactive.Include);
            if (traderStatus == null) traderStatus = TraderStatus.CanonicalInstance;

            if (gameManager == null || marketEngine == null || type == PositionType.None)
            {
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
            entryPrice = marketEngine.CurrentPrice;
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

            // 플레이어 매매 진행 시 AI 차트 힌트 대사 연동
            var aiBrain = UnityEngine.Object.FindAnyObjectByType<FXOverdose.AI.AITradingBrain>();
            if (aiBrain != null)
            {
                aiBrain.ProvideChartHintToPlayer(type);
            }

            return true;
        }

        // 포지션 종료 (익절/손절)
        public void ClosePosition()
        {
            if (currentPosition == PositionType.None || gameManager == null)
            {
                return;
            }

            float pnl = CalculateUnrealizedPnL();

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

        // 실시간 미실현 손익 (Unrealized PnL) 계산
        public float CalculateUnrealizedPnL()
        {
            if (currentPosition == PositionType.None || marketEngine == null || entryPrice <= 0f)
            {
                return 0f;
            }

            float currentPrice = marketEngine.CurrentPrice;
            float priceDiff = currentPosition == PositionType.Long 
                ? (currentPrice - entryPrice) 
                : (entryPrice - currentPrice);

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
            // ⭐ [이벤트 포지션 초반 휩소 보호] 이벤트 등으로 포지션을 개설한 직후 3초 이내에는 순간적인 꼬리 스파이크 노이즈 1틱에 의한 억울한 즉사 청산을 방어!
            if (Time.time < eventPositionOpenedTime + 3.0f)
            {
                return;
            }

            // ⭐ [Overdose 보호] 오버도즈 폭주 상태일 때는 플레이어가 대처할 수 있도록 35초의 연출/쉴드 시간을 보장하며, 그 전에는 지하실로 처박혀도 청산을 유예함
            if (isOverdoseTradeActive && Time.time < overdoseProtectionEndTime - 1.0f)
            {
                return;
            }

            bool isLiquidated = false;
            if (currentPosition == PositionType.Long && currentPrice <= liquidationPrice)
            {
                isLiquidated = true;
            }
            else if (currentPosition == PositionType.Short && currentPrice >= liquidationPrice)
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

            // AI 트레이더 멘탈 대붕괴 (Overdose 직전 또는 즉시 도달)
            if (traderStatus != null)
            {
                traderStatus.ChangeMental(-60f);
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
        public void TriggerOverdoseTrade()
        {
            if (gameManager == null || marketEngine == null) return;
            
            // ⭐ [고속 스킵 일시정지] 오버도즈 골든타임 보장을 위해 진행 중인 고속 스킵 중단
            gameManager.PauseFastForwardForOverdose();

            // ⭐ [이벤트 포지션 쉴드 무시 및 강제 반대매매 탕진] 돌발 이벤트가 진행 중이어도 Overdose가 발생하면 이벤트를 파괴하고 쉴드를 해제합니다.
            if (Time.time < eventProtectionEndTime && !isOverdoseTradeActive)
            {
                Debug.LogWarning("[TradingController] 🩸 [Overdose 폭주] 진행 중인 돌발 이벤트 보호 쉴드를 파괴하고 탕진 매매를 시작합니다!");
                eventProtectionEndTime = -1f;
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

            // ⭐ [확실한 0원 청산 보장 All-In] 잔고의 일부가 아닌 전액 100% 올인하여 청산 시 잔고 0원(Overdose 게임오버)을 확실히 보장합니다!
            float forcedMargin = gameManager.CurrentBalance;
            int forcedLeverage = 125;

            float currentP = marketEngine.CurrentPrice;
            float aiTarget = forcedDirection == PositionType.Long ? currentP * 2.5f : currentP * 0.2f;

            if (forcedMargin > 1f)
            {
                isOverdoseTradeActive = true;
                overdoseProtectionEndTime = Time.time + 35f;

                // 💡 [Overdose 함정 차트 및 신호 가동] 주인공이 좋은 지점으로 착각할 가짜 초대박 신호와 함께 반대 방향 죽음의 차트 가동
                marketEngine.TriggerOverdoseTrapSignal(forcedDirection, 35);

                OpenPosition(forcedDirection, forcedMargin, forcedLeverage, aiTarget, 0f, true);
                var visual = UnityEngine.Object.FindAnyObjectByType<FXOverdose.AI.AIVisualController>(FindObjectsInactive.Include);
                visual?.DisplayDialogueBalloon($"크하하하!! 완벽한 진입 타점이다!! {forcedLeverage}배 풀레버리지 남은 시드 싹 다 올인!! 오늘 끝장을 보자!!", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.MentalChange);
            }
        }

        public void ExecuteEmergencyTrade(PositionType posType, int leverage, int durationSeconds = 30, EventPositionHandlingMode handlingMode = EventPositionHandlingMode.StandardAuto, float customTargetROE = 0f, float customStopLossROE = 0f, bool isPlayerChoice = false, bool isTrueSignal = true)
        {
            if (gameManager == null) gameManager = UnityEngine.Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            if (marketEngine == null) marketEngine = UnityEngine.Object.FindAnyObjectByType<MarketSimulationEngine>(FindObjectsInactive.Include);
            if (traderStatus == null) traderStatus = TraderStatus.CanonicalInstance;

            if (posType == PositionType.None && leverage <= 0)
            {
                CloseAllPositions();
                return;
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
                float forcedMargin = Mathf.Max(10f, effectiveBalance * 0.4f);
                if (forcedMargin > gameManager.CurrentBalance && gameManager.CurrentBalance > 1f)
                {
                    forcedMargin = gameManager.CurrentBalance * 0.95f;
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

                    OpenPosition(posType, forcedMargin, Mathf.Clamp(leverage, 1, 125), aiTarget, aiStop, true);
                    Debug.Log($"[TradingController] ⚡ ExecuteEmergencyTrade (돌발 이벤트 강제 진입) 완료: {posType} / 증거금 ${forcedMargin:N0} / 레버리지 {leverage}배 ({protectDuration}초 이벤트 쉴드 가동, 모드: {handlingMode}, 플레이어선택: {isPlayerChoice}, 진위: {isTrueSignal})");
                }
            }
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
    }
}
