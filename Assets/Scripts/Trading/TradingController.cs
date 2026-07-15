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
                if (visual != null) visual.DisplayDialogueBalloon("마스터...! 아직 내 뇌(LLM)가 로딩 중이라 정신이 깜깜해... 장 열린 뒤에 모드 전환해 줘, 응...? ♥", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.General);
                return;
            }

            // 2. 게임 플레이(장이 개시된 상태) 검증
            var gm = gameManager != null ? gameManager : UnityEngine.Object.FindAnyObjectByType<GameManager>();
            if (gm != null && gm.CurrentState != GameManager.GameState.Playing)
            {
                Debug.LogWarning("[TradingController] ⚠️ 장이 개시(Playing)되기 전에는 매매 모드를 전환할 수 없습니다.");
                var visual = UnityEngine.Object.FindAnyObjectByType<FXOverdose.AI.AIVisualController>();
                if (visual != null) visual.DisplayDialogueBalloon("마스터 아직 장도 안 열렸잖아! 호가창 움직이기 시작하면 그때 전환해 줘, 나 현기증 난단 말야...!", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.General);
                return;
            }

            // 3. 시장 오픈 여부 검증
            var market = marketEngine != null ? marketEngine : UnityEngine.Object.FindAnyObjectByType<MarketSimulationEngine>();
            if (market != null && !market.IsMarketOpen)
            {
                Debug.LogWarning("[TradingController] ⚠️ 시장(Market)이 아직 개장하지 않았습니다. 개장 후에 전환 가능합니다.");
                var visual = UnityEngine.Object.FindAnyObjectByType<FXOverdose.AI.AIVisualController>();
                if (visual != null) visual.DisplayDialogueBalloon("시장이 닫혀 있는데 어딜 전환하려고 해 마스터?! 개장하자마자 바로 전환해 줄 테니까 조금만 얌전히 기다려 ♥", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.General);
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
                    visualCtrl.DisplayDialogueBalloon("히익...! 마스터가 직접 매매하겠다고?! 내 타점이 못미더운 거야...? 흐윽... 알겠어, 대신 마스터가 잡은 포지션 옆에서 두 눈 부릅뜨고 지켜볼 거니까 절대 실수해서 우리 돈 날리면 안 돼...♥", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.General);
                }
                else
                {
                    visualCtrl.DisplayDialogueBalloon("헤헤♥ 역시 우리 마스터는 나 없으면 아무것도 못 하지?! 이제 조종간은 다시 내가 잡았으니까 마스터는 얌전히 내 화려한 차트 춤이나 감상하라고~♥", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.General);
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
            maxObservedEventROE = 0f;
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

        [Header("이벤트 포지션 관리 상태 (읽기 전용)")]
        [SerializeField] private bool isEventTradeActive = false;
        [SerializeField] private EventPositionHandlingMode currentEventHandlingMode = EventPositionHandlingMode.StandardAuto;
        [SerializeField] private float eventTargetROELimit = 0f;
        [SerializeField] private float eventStopLossROELimit = 0f;
        private float maxObservedEventROE = 0f;

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

            switch (currentEventHandlingMode)
            {
                case EventPositionHandlingMode.InstantTakeProfit:
                    float targetTakeProfitROE = eventTargetROELimit > 0f ? eventTargetROELimit : 100f;
                    if (roe >= targetTakeProfitROE)
                    {
                        Debug.Log($"[TradingController ⚡] 이벤트 칼익절(InstantTakeProfit) 발동! ROE +{roe:F1}% 달성으로 수익을 즉시 확정합니다.");
                        if (visual != null) visual.DisplayDialogueBalloon($"마스터...! 이벤트 빔으로 ROE +{roe:0.0}% 찍자마자 칼익절로 챙겼어!! 기회 놓치지 않는 게 최고지 ♥", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.PositionClosed);
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
                    float mitigationStopLimit = eventStopLossROELimit < 0f ? eventStopLossROELimit : -85f;
                    if (roe <= mitigationStopLimit)
                    {
                        Debug.Log($"[TradingController 🛡️] 손실 약화 버티기(HoldToMitigateLoss) 비상 방어: 청산 직전까지 인내했으나 잔여 증거금을 지키기 위해 긴급 정리합니다. (ROE {roe:F1}%)");
                        if (visual != null) visual.DisplayDialogueBalloon($"끝까지 이 악물고 버텼는데 더 버티면 100% 청산이야...! 남은 증거금이라도 건지려고 비상 탈출했어...! (ROE {roe:0.0}%)", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.PositionClosed);
                        ClosePosition();
                    }
                    else if (maxObservedEventROE <= -40f && roe >= -5f)
                    {
                        Debug.Log($"[TradingController 🛡️] 손실 약화 버티기(HoldToMitigateLoss) 성공! 극심한 손실 구간을 인내하고 반등 시점에 손실을 최소화하여 정리합니다. (ROE {roe:F1}%)");
                        if (visual != null) visual.DisplayDialogueBalloon($"휴우... 아까 바닥까지 떨어졌을 때 끝까지 버틴 덕분에 손실 거의 다 회복하고 탈출했어! 심장 떨어지는 줄 알았네...♥", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.PositionClosed);
                        ClosePosition();
                    }
                    break;

                case EventPositionHandlingMode.GreedyHold:
                    float targetGreedyROE = eventTargetROELimit > 0f ? eventTargetROELimit : 3000f;
                    if (roe >= targetGreedyROE)
                    {
                        Debug.Log($"[TradingController 👑] 탐욕적 홀딩(GreedyHold) 목표치 대폭발! ROE +{roe:F1}% 극한 수익 확정 청산!");
                        if (visual != null) visual.DisplayDialogueBalloon($"꺄아아악!! 마스터 봤어?! ROE +{roe:0.0}% 초거대 대박이야!! 이대로 전액 수익 확정!! 우리 부자다아아♥", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.PositionClosed);
                        ClosePosition();
                    }
                    else if (maxObservedEventROE >= 300f && roe <= maxObservedEventROE * 0.75f)
                    {
                        Debug.Log($"[TradingController 👑] 탐욕적 홀딩(GreedyHold) 트레이링 익절 작동! 고점(+{maxObservedEventROE:F1}%) 대비 조정 감지로 ROE +{roe:F1}% 확정 청산!");
                        if (visual != null) visual.DisplayDialogueBalloon($"고점 찍고 꺾이는 거 감지해서 ROE +{roe:0.0}%에 트레이링 익절했어!! 최고점에서 조금 내려왔지만 그래도 초대박이야 마스터♥", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.PositionClosed);
                        ClosePosition();
                    }
                    break;

                case EventPositionHandlingMode.StandardAuto:
                default:
                    float autoLimit = eventTargetROELimit > 0f ? eventTargetROELimit : 300f;
                    if (roe >= autoLimit)
                    {
                        Debug.Log($"[TradingController 🤖] 이벤트 유연 판단(StandardAuto) 익절: ROE +{roe:F1}% 달성으로 수익을 챙깁니다.");
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
                        if (visual != null) visual.DisplayDialogueBalloon($"이벤트 빔 끝까지 다 타서 ROE +{roe:0.0}% 달성!! 우리 마스터 선택 진짜 최고야♥", FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.LLM.EventCategory.PositionClosed);
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
            if (currentPosition == PositionType.None) return;

            // 1. 강제 청산(Liquidation) 판정
            CheckLiquidation(price);
            if (currentPosition == PositionType.None) return;

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
                    TriggerLLMDialogue(FXOverdose.AI.LLM.EventCategory.ChartMovement, $"현재 포지션({currentPosition}) ROE {roe:0.0}% 손절권 급락 중! {directionHint}");
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

            if (margin <= 0f || gameManager.CurrentBalance <= 1f)
            {
                Debug.LogWarning("[TradingController] 증거금이 부족합니다.");
                return false;
            }

            // 주인공 레벨에 따른 레버리지 및 증거금 클램핑 적용
            var levelSystem = TraderLevelSystem.Instance;
            if (levelSystem != null)
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

            // 증거금 차감
            gameManager.ChangeBalance(-margin);

            currentPosition = type;
            currentOwner = OwnerType.AI;
            entryPrice = marketEngine.CurrentPrice;
            marginAmount = margin;
            currentLeverage = leverage;
            targetPrice = aiTargetPrice;
            stopLossPrice = aiStopLossPrice;
            lastReportedROE = 0f;

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
            gameManager.ChangeBalance(-margin);

            currentPosition = type;
            currentOwner = OwnerType.Player;
            entryPrice = marketEngine.CurrentPrice;
            marginAmount = margin;
            currentLeverage = leverage;
            targetPrice = 0f; // 플레이어 직접 판단 익절
            stopLossPrice = 0f; // 플레이어 직접 판단 손절
            lastReportedROE = 0f;

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
            }

            Debug.Log($"[TradingController] 포지션 종료. 실현 손익: {pnl:N1} ({CalculateROEPercentage():F2}%), 최종 회수금: {totalReturn:N0}");

            PositionType closedType = currentPosition;
            lastMarginAmount = marginAmount;

            // 💡 [이벤트 순서 수정] 이벤트 수신자가 활성 증거금 및 PnL ROE를 정확히 읽을 수 있도록 청산 상태 초기화 직전에 발송!
            OnPositionClosed?.Invoke(totalReturn, pnl);

            currentPosition = PositionType.None;
            currentOwner = OwnerType.AI;
            targetPrice = 0f;
            stopLossPrice = 0f;
            lastReportedROE = 0f;
            isEventTradeActive = false;
            currentEventHandlingMode = EventPositionHandlingMode.StandardAuto;
            eventTargetROELimit = 0f;
            eventStopLossROELimit = 0f;
            maxObservedEventROE = 0f;
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
            isEventTradeActive = false;
            currentEventHandlingMode = EventPositionHandlingMode.StandardAuto;
            eventTargetROELimit = 0f;
            eventStopLossROELimit = 0f;
            maxObservedEventROE = 0f;
            OnPositionChanged?.Invoke();
        }

        // 멘헤라 AI 트레이더 폭주(Overdose 상태) 시 호출되는 고레버리지 뇌동매매 실행 함수
        public void TriggerOverdoseTrade()
        {
            if (gameManager == null || marketEngine == null) return;

            // ⭐ [이벤트 포지션 쉴드] 돌발 이벤트 등으로 전략적 포지션을 개설한 직후(최대 30초)에는 AI가 뇌동매매로 해당 포지션을 바로 뒤엎거나 엉뚱한 물타기를 강행하지 못하도록 차단!
            if (Time.time < eventProtectionEndTime)
            {
                Debug.Log("[TradingController] 🛡️ 이벤트 선택지 포지션 쉴드 활성화 중 -> AI Overdose 뇌동매매 개입을 임시 차단하여 이벤트 의도를 존중합니다.");
                return;
            }

            Debug.LogWarning("[TradingController] 🩸 [Overdose 폭주] AI 트레이더가 통제를 벗어나 고레버리지 뇌동매매를 강행합니다!");

            // 만약 기존 포지션이 수익(익절) 중이라면 통제 불능 상태에서는 이를 바로 청산하고 손실 나는 방향으로 스위칭
            if (currentPosition != PositionType.None && CalculateUnrealizedPnL() > 0f)
            {
                Debug.LogWarning("[TradingController] 🩸 [Overdose 폭주] 수익 중인 포지션을 뒤엎고 반대 방향 고레버리지 뇌동매매로 전환합니다!");
                ClosePosition();
            }

            // 기존 포지션이 없다면 남은 자산의 50% 이상을 고레버리지(100배~125배)로 진입
            if (currentPosition == PositionType.None)
            {
                float forcedMargin = gameManager.CurrentBalance * UnityEngine.Random.Range(0.5f, 0.8f);
                int forcedLeverage = UnityEngine.Random.Range(100, 126);

                // [기획서 4.5장 부합] Overdose 시 "손실이 큰 방향으로 고레버리지 진입 강제 실행"
                PositionType forcedDirection = PositionType.Long;
                if (marketEngine.CurrentSignalPhase != SignalPhase.None && Mathf.Abs(marketEngine.ActiveSignal.TargetPercentageDelta) > 0.01f)
                {
                    // 현재 차트 신호가 상승(TargetPercentageDelta > 0)이면 반대인 Short(숏) 진입, 하락이면 Long(롱) 진입하여 강제 손실 및 청산 유도
                    forcedDirection = marketEngine.ActiveSignal.TargetPercentageDelta > 0f ? PositionType.Short : PositionType.Long;
                }
                else if (marketEngine.CurrentRegime == MarketSimulationEngine.MarketRegime.Bull)
                {
                    forcedDirection = PositionType.Short;
                }
                else if (marketEngine.CurrentRegime == MarketSimulationEngine.MarketRegime.Bear)
                {
                    forcedDirection = PositionType.Long;
                }
                else
                {
                    // 횡보장 등에서는 최근 일일 고점 부근이면 고점 매수(Long) 물림, 저점 부근이면 저점 매도(Short) 물림
                    forcedDirection = marketEngine.CurrentPrice > (marketEngine.Current24hHigh + marketEngine.Current24hLow) * 0.5f 
                        ? PositionType.Long : PositionType.Short;
                }

                float currentP = marketEngine.CurrentPrice;
                // 뇌동매매는 목표가를 +50% 등 터무니없이 높게, 손절선은 0(없음)으로 설정
                float aiTarget = forcedDirection == PositionType.Long ? currentP * 1.5f : currentP * 0.5f;

                if (forcedMargin > 10f)
                {
                    OpenPosition(forcedDirection, forcedMargin, forcedLeverage, aiTarget, 0f);
                }
            }
            else
            {
                // 이미 포지션이 있을 경우 물타기 (남은 현금 탈탈 털어 증거금 추가 및 레버리지 급등)
                float addMargin = gameManager.CurrentBalance * 0.9f;
                if (addMargin > 10f)
                {
                    gameManager.ChangeBalance(-addMargin);
                    marginAmount += addMargin;
                    currentLeverage = 125; // 최대 레버리지로 상향
                    // 목표가도 더 무리하게 연장
                    if (targetPrice > 0f) targetPrice = currentPosition == PositionType.Long ? targetPrice * 1.2f : targetPrice * 0.8f;
                    Debug.LogWarning($"[TradingController] 🩸 [Overdose 폭주] 남은 자금 {addMargin:N0}원 전부 물타기 및 레버리지 125배 상향! (목표가 연장: ${targetPrice:N1})");
                    OnPositionChanged?.Invoke();
                    OnPositionOpened?.Invoke(currentPosition, marginAmount, currentLeverage);
                }
            }
        }

        public void ExecuteEmergencyTrade(PositionType posType, int leverage, int durationSeconds = 30, EventPositionHandlingMode handlingMode = EventPositionHandlingMode.StandardAuto, float customTargetROE = 0f, float customStopLossROE = 0f)
        {
            if (posType == PositionType.None && leverage <= 0)
            {
                CloseAllPositions();
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
                float forcedMargin = Mathf.Max(10f, gameManager.CurrentBalance * 0.4f);
                if (forcedMargin > gameManager.CurrentBalance)
                {
                    forcedMargin = gameManager.CurrentBalance * 0.95f;
                }
                if (forcedMargin > 0f && gameManager.CurrentBalance > 1f)
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
                    maxObservedEventROE = 0f;

                    OpenPosition(posType, forcedMargin, Mathf.Clamp(leverage, 1, 125), aiTarget, aiStop, true);
                    Debug.Log($"[TradingController] ⚡ ExecuteEmergencyTrade (돌발 이벤트 강제 진입) 완료: {posType} / 증거금 ${forcedMargin:N0} / 레버리지 {leverage}배 ({protectDuration}초 이벤트 쉴드 가동, 모드: {handlingMode})");
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
