using System;
using UnityEngine;
using FXOverdose.Trading;

namespace FXOverdose.AI
{
    /// <summary>
    /// 상시 멘탈 소모 6대 기믹(미실현 손익 침식, 연속 손절 콤보, 수면 부족 연쇄, 고배율 중독, 횡보 지루함, 드로다운 트라우마)을 
    /// 실시간(Update) 및 인게임 시간(OnGameMinuteAdvanced) 주기로 병행 제어하는 중앙 컨트롤러입니다.
    /// </summary>
    public class MentalDrainGimmickController : MonoBehaviour
    {
        [Header("연결된 핵심 시스템")]
        [SerializeField] private TraderStatus traderStatus;
        [SerializeField] private TradingController tradingController;
        [SerializeField] private MarketSimulationEngine marketEngine;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private AIVisualController visualController;
        [SerializeField] private AITradingBrain aiBrain;

        [Header("기믹 내부 타이머 및 누적기")]
        private float panicDialogueTimer = 0f;
        public bool isUnrealizedPnLCured = false;

        public void CureMentalGimmicks()
        {
            isUnrealizedPnLCured = true;
            isTrackingMissedSignal = false;
            traderStatus.CurrentLosingStreak = 0;
            isImpulsiveCountdownActive = false;
            Debug.Log("[MentalDrainGimmickController] 💊 멘탈 감소 기믹들이 1회성으로 치료(초기화)되었습니다.");
        }

        // 4연속 손절 시 15초 카운트다운 관련
        private bool isImpulsiveCountdownActive = false;
        private float impulsiveTradeCountdownTimer = 0f;

        // 휩소 오인 및 FOMO 후회 기믹 관련
        private bool isTrackingMissedSignal = false;
        private MarketSignal trackedMissedSignal;
        private float missedSignalStartPrice = 0f;
        private float missedSignalTimer = 0f;

        private bool isInitialized = false;
        private FXOverdose.AI.LLM.LocalLLMService llmService;

        private void TriggerGimmickDialogue(string gimmickContext, string fallbackDialogue = "")
        {
            if (llmService == null) llmService = FXOverdose.AI.LLM.LocalLLMService.Instance;
            if (llmService != null)
            {
                llmService.RequestDialogue(FXOverdose.AI.LLM.EventCategory.GimmickTriggered, gimmickContext);
            }
            else if (!string.IsNullOrEmpty(fallbackDialogue) && visualController != null)
            {
                visualController.DisplayDialogueBalloon(fallbackDialogue, DialoguePriority.Normal, FXOverdose.AI.LLM.EventCategory.GimmickTriggered);
            }
        }

        private void Awake()
        {
            traderStatus = TraderStatus.CanonicalInstance;
            if (tradingController == null) tradingController = FindAnyObjectByType<TradingController>();
            if (marketEngine == null) marketEngine = FindAnyObjectByType<MarketSimulationEngine>();
            if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
            if (visualController == null) visualController = FindAnyObjectByType<AIVisualController>();
            if (aiBrain == null) aiBrain = FindAnyObjectByType<AITradingBrain>();
            if (llmService == null) llmService = FXOverdose.AI.LLM.LocalLLMService.Instance;
        }

        private void Start()
        {
            Initialize(traderStatus, tradingController, marketEngine);
        }

        private void OnDestroy()
        {
            if (tradingController != null)
            {
                tradingController.OnPositionClosed -= OnPositionClosed;
                tradingController.OnPositionOpened -= OnPositionOpened;
            }
            if (gameManager != null)
            {
                gameManager.OnGameMinuteAdvanced -= OnGameMinuteAdvanced;
            }
            if (aiBrain != null)
            {
                aiBrain.OnSignalEvaluationCompleted -= OnSignalEvaluationCompleted;
            }
        }

        public void Initialize(TraderStatus status, TradingController trading, MarketSimulationEngine market)
        {
            traderStatus = TraderStatus.CanonicalInstance;
            if (trading != null) tradingController = trading;
            if (market != null) marketEngine = market;
            if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
            if (visualController == null) visualController = FindAnyObjectByType<AIVisualController>();
            if (aiBrain == null) aiBrain = FindAnyObjectByType<AITradingBrain>();

            if (tradingController != null)
            {
                tradingController.OnPositionClosed -= OnPositionClosed;
                tradingController.OnPositionClosed += OnPositionClosed;
                tradingController.OnPositionOpened -= OnPositionOpened;
                tradingController.OnPositionOpened += OnPositionOpened;
            }

            if (gameManager != null)
            {
                gameManager.OnGameMinuteAdvanced -= OnGameMinuteAdvanced;
                gameManager.OnGameMinuteAdvanced += OnGameMinuteAdvanced;
            }

            if (aiBrain != null)
            {
                aiBrain.OnSignalEvaluationCompleted -= OnSignalEvaluationCompleted;
                aiBrain.OnSignalEvaluationCompleted += OnSignalEvaluationCompleted;
            }

            if (!isInitialized)
            {
                isInitialized = true;
                Debug.Log("[MentalDrainGimmickController] 🧠 상시 멘탈 소모 6대 기믹 코어 엔진이 초기화되었습니다.");
            }
        }

        private void Update()
        {
            if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
            if (gameManager == null || gameManager.CurrentState != GameManager.GameState.Playing || gameManager.IsFastForwardingTime)
            {
                return;
            }

            if (traderStatus == null) traderStatus = FindAnyObjectByType<TraderStatus>();
            if (tradingController == null) tradingController = FindAnyObjectByType<TradingController>();
            if (marketEngine == null) marketEngine = FindAnyObjectByType<MarketSimulationEngine>();
            if (aiBrain == null)
            {
                aiBrain = FindAnyObjectByType<AITradingBrain>();
                if (aiBrain != null)
                {
                    aiBrain.OnSignalEvaluationCompleted -= OnSignalEvaluationCompleted;
                    aiBrain.OnSignalEvaluationCompleted += OnSignalEvaluationCompleted;
                }
            }
            if (traderStatus == null || tradingController == null)
            {
                return;
            }

            float dt = Time.unscaledDeltaTime;
            EvaluateUnrealizedPnLErosion(dt);
            EvaluateLosingStreakCountdown(dt);
            EvaluateMissedOpportunityRegret(dt);
        }

        // --- [기믹 1: 미실현 손실(Unrealized P&L) 실시간 침식] ---
        private float roeDrainAccumulator = 0f;

        private void EvaluateUnrealizedPnLErosion(float deltaTime)
        {
            if (isUnrealizedPnLCured) return;

            // 💡 [LV.10 스킬 보너스] 차트 공부 LV.10 달성 시 미실현 손실 압박 기믹 완전 면역
            if (FXOverdose.Trading.TraderLevelSystem.Instance != null && FXOverdose.Trading.TraderLevelSystem.Instance.ChartStudyLevel >= 10)
            {
                roeDrainAccumulator = 0f;
                return;
            }

            if (!tradingController.IsActive || tradingController.MarginAmount <= 0f)
            {
                panicDialogueTimer = 0f;
                roeDrainAccumulator = 0f;
                return;
            }

            float roe = tradingController.CalculateROEPercentage();
            if (roe > -5.0f)
            {
                roeDrainAccumulator = 0f;
                return;
            }

            float drainRate = 0f;
            if (roe <= -20.0f)
            {
                drainRate = 0.15f;
            }
            else if (roe <= -10.0f)
            {
                drainRate = 0.05f;
            }
            else // -10.0f < roe <= -5.0f
            {
                drainRate = 0.01f;
            }

            roeDrainAccumulator += deltaTime;

            // 1초 단위로 누적하여 한 번에 차감 (UI 스팸 방지 및 가독성 향상)
            if (roeDrainAccumulator >= 1.0f)
            {
                traderStatus.ChangeMental(-drainRate, false, "미실현 손실 압박");
                roeDrainAccumulator -= 1.0f;
            }

            // ROE <= -20% 지속 시 25초 주기로 불안/패닉 독백 출력
            if (roe <= -20.0f)
            {
                panicDialogueTimer += deltaTime;
                if (panicDialogueTimer >= 25.0f)
                {
                    panicDialogueTimer = 0f;
                    TriggerGimmickDialogue($"미실현 손실 공포 기믹 (ROE {roe:0.0}% 손실 진행 중, 극도의 공포와 패닉)", "안돼 안돼 안돼!! 내 시드가... 갈려 나간다!! 물타기 해야 해, 아니 손절해야 해?!");
                }
            }
            else
            {
                panicDialogueTimer = 0f;
            }
        }

        // --- [기믹 2 연동 카운트다운: 4연속 손절 시 15초 내 미개입 시 강제 100배 진입] ---
        private void EvaluateLosingStreakCountdown(float deltaTime)
        {
            if (!isImpulsiveCountdownActive) return;
            if (tradingController != null && tradingController.ActiveTradingMode == TradingController.TradingMode.Player_Manual)
            {
                isImpulsiveCountdownActive = false;
                impulsiveTradeCountdownTimer = 0f;
                return;
            }

            // 플레이어가 개입하여 연속 손절이 리셋되었거나 이미 새 포지션에 들어갔다면 카운트다운 종료
            if (traderStatus.CurrentLosingStreak < 4 || tradingController.IsActive)
            {
                isImpulsiveCountdownActive = false;
                impulsiveTradeCountdownTimer = 0f;
                return;
            }

            impulsiveTradeCountdownTimer -= deltaTime;
            if (impulsiveTradeCountdownTimer <= 0f)
            {
                isImpulsiveCountdownActive = false;
                Debug.LogWarning("[MentalDrainGimmickController] ⚡ 4연속 손절 후 5초 내 개입 없음 -> AI 100배 기믹 뇌동매매 강행!");
                traderStatus.TriggerImpulsiveTrade(100);
            }
        }

        // --- [기믹 2: 연속 손절 콤보 (Losing Streak Multiplier) 및 기믹 4 중독 감지] ---
        private void OnPositionClosed(float returnedAmount, float realizedPnL)
        {
            isUnrealizedPnLCured = false;

            if (traderStatus == null) traderStatus = FindAnyObjectByType<TraderStatus>();
            if (tradingController == null) tradingController = FindAnyObjectByType<TradingController>();
            if (traderStatus == null || tradingController == null) return;

            int closedLeverage = tradingController.CurrentLeverage;
            bool isManualMode = tradingController.ActiveTradingMode == TradingController.TradingMode.Player_Manual;

            // 💡 [기믹 4: 고배율 중독 금단현상 리워크]
            if (isManualMode)
            {
                var levelSystem = FXOverdose.Trading.TraderLevelSystem.Instance;
                int maxAllowedLev = levelSystem != null ? levelSystem.GetMaxAllowedLeverage() : 10;
                
                // 중독 발동: 레벨 해금 50배 이상, 플레이어 직접 조작, 50배 이상으로 연속 3회 익절
                if (maxAllowedLev >= 50 && closedLeverage >= 50 && realizedPnL > 0f)
                {
                    traderStatus.ConsecutiveHighLevWins++;
                    traderStatus.ConsecutiveLowLevTrades = 0; // 고배율 익절 시 저배율 카운트 초기화

                    if (traderStatus.ConsecutiveHighLevWins >= 3 && !traderStatus.IsLeverageAddicted)
                    {
                        traderStatus.IsLeverageAddicted = true;
                        traderStatus.ConsecutiveHighLevWins = 0;
                        TriggerGimmickDialogue("50배 이상 고배율 3연승 과몰입 중독 기믹 발동 (도파민 폭주 및 희열)", "그래!! 바로 이 느낌이야!! 호가창의 진동이 온몸에 짜릿하게 감돈다!!");
                        Debug.LogWarning("[MentalDrainGimmickController] 🎰 [고배율 중독 발동] 50배 이상 3연승으로 고배율에 중독되었습니다!");
                    }
                }
                else if (traderStatus.IsLeverageAddicted && closedLeverage <= 50)
                {
                    // 고배율 중독 상태에서 50배 이하의 저배율 매매 진행 시 (수익/손실 무관)
                    traderStatus.ConsecutiveLowLevTrades++;
                    
                    if (traderStatus.ConsecutiveLowLevTrades >= 2)
                    {
                        // 2회 누적 시 요미가 강제로 매매 주도권을 뺏음
                        Debug.LogWarning("[MentalDrainGimmickController] 😡 [고배율 중독 폭주] 요미가 답답함을 못 참고 매매 주도권을 강탈합니다!");
                        TriggerGimmickDialogue("[고배율 중독 폭주 기믹 발동] 쫄보 같은 저배율 매매에 극도의 답답함과 짜증을 느끼며 강제로 마우스를 뺏어버리는 상황. 트레이더는 도파민 부족으로 손을 떨며 '비켜봐, 내가 시원하게 긁어줄게'라는 식의 미친듯한 탐욕과 참을 수 없는 짜증을 강렬하게 표출해 줘.", "아 진짜 답답해 미치겠네!! 장난쳐?! 그딴 푼돈으로 언제 부자 될 건데?! 이리 내, 내가 직접 할 거야!!");
                        
                        tradingController.LockManualMode();
                        tradingController.SetTradingMode(TradingController.TradingMode.AI_Auto);
                        traderStatus.CureLeverageAddiction(); // 중독 상태 해제
                        
                        // AITradingBrain에 다음번 판단에서 강제로 고배율 매매를 진행하도록 지시
                        if (aiBrain != null)
                        {
                            aiBrain.ForceNextTradeHighLeverage = true;
                        }
                    }
                    else
                    {
                        TriggerGimmickDialogue("고배율 중독 금단현상 1회 경고 (저배율 답답함)", "야... 배율 너무 낮지 않아...? 아까처럼 고배율로 팍팍 좀 들어가자 응...?");
                    }
                }
                else if (closedLeverage >= 50 && realizedPnL <= 0f)
                {
                     traderStatus.ConsecutiveHighLevWins = 0;
                }
            }

            if (realizedPnL >= 0f)
            {
                // 수익 청산
                traderStatus.CurrentLosingStreak = 0;
                isImpulsiveCountdownActive = false;
            }
            else
            {
                // 플레이어 수동 매매 모드일 때는 손절 시에도 연속 손절 콤보 멘탈 감소 및 휩소 자책 기믹을 발생시키지 않음!
                if (isManualMode)
                {
                    Debug.Log("[MentalDrainGimmickController] 🛡️ 플레이어 수동 매매 중 손절 발생: 휩소 기믹 및 연속 손절 페널티를 면제합니다.");
                    return;
                }

                // 손실 청산
                traderStatus.CurrentLosingStreak++;
                traderStatus.ConsecutiveHighLevWins = 0;

                int streak = traderStatus.CurrentLosingStreak;
                float penalty = 0f;

                switch (streak)
                {
                    case 1:
                        penalty = 5.0f;
                        break;
                    case 2:
                        penalty = 12.0f;
                        TriggerGimmickDialogue("2연속 손절 휩소 자책 기믹 발동 (손실 스트레스)", "아씨, 꼬리만 털고 왜 반대로 가는데?!");
                        break;
                    case 3:
                        penalty = 25.0f;
                        TriggerGimmickDialogue("3연속 손절 피해망상 기믹 발동 (세력 조롱 피해의식 심화)", "차트가 날 감시하고 조롱하는 게 분명해...!");
                        Debug.LogWarning("[MentalDrainGimmickController] 🔴 [LOSE x3] 3연속 손절! 극도의 자격지심 발생");
                        break;
                    default: // 4연속 이상
                        penalty = 35.0f;
                        isImpulsiveCountdownActive = true;
                        impulsiveTradeCountdownTimer = 5.0f;
                        TriggerGimmickDialogue("4연속 손절 복수심 100배 뇌동매매 카운트다운 기믹 발동 (극도의 분노와 자제력 상실)", "4연속 손절... 더는 못 참아! 5초 내에 100배로 싹 다 복구한다!!");
                        Debug.LogWarning("[MentalDrainGimmickController] 🔴 [LOSE x4+] 4연속 손절! 5초 카운트다운 돌입");
                        break;
                }

                traderStatus.ChangeMental(-penalty, false, "연속 손절 스트레스");
            }
        }

        // --- [신규 기믹: 매 거래 실행(포지션 진입/물타기) 시 고정 10 멘탈 소모] ---
        private void OnPositionOpened(TradingController.PositionType type, float margin, int leverage)
        {
            isUnrealizedPnLCured = false;

            if (traderStatus == null) traderStatus = FindAnyObjectByType<TraderStatus>();
            if (traderStatus == null) return;
            traderStatus.ChangeMental(-10.0f, false, "포지션 진입/물타기");
            Debug.Log($"[MentalDrainGimmickController] 💸 매매 실행({type}, {leverage}배)으로 고정 멘탈 -10 감소 (현재 멘탈: {traderStatus.CurrentMental:F1})");
        }

        // --- [신규 기믹: 휩소 의심 등으로 진입을 포기한 신호 감지 및 주가 추적 시작] ---
        private void OnSignalEvaluationCompleted(MarketSignal signal, bool didEnter)
        {
            if (tradingController != null && tradingController.ActiveTradingMode == TradingController.TradingMode.Player_Manual)
            {
                isTrackingMissedSignal = false;
                return;
            }

            if (!didEnter)
            {
                trackedMissedSignal = signal;
                isTrackingMissedSignal = true;
                missedSignalStartPrice = (signal.SignalStartPrice > 0f) ? signal.SignalStartPrice : (marketEngine != null ? marketEngine.CurrentPrice : 0f);
                missedSignalTimer = 0f;
                Debug.Log($"[MentalDrainGimmickController] 👀 진입 포기 신호 감지 -> 주가 추적 시작 ({signal.GetSignalDescription()})");
            }
            else
            {
                isTrackingMissedSignal = false;
            }
        }

        // --- [신규 기믹: 휩소에 속아 수익 타점을 놓친 것에 대한 FOMO/후회 멘탈 감소] ---
        private void EvaluateMissedOpportunityRegret(float deltaTime)
        {
            if (!isTrackingMissedSignal || marketEngine == null || traderStatus == null) return;
            if (tradingController != null && tradingController.ActiveTradingMode == TradingController.TradingMode.Player_Manual)
            {
                isTrackingMissedSignal = false;
                return;
            }

            missedSignalTimer += deltaTime;

            // 판단 여유 시간(GraceWindow)이 지나고 실제 주가가 움직인 국면(8초~60초)에서 실시간 변동률 검사
            if (missedSignalTimer >= 8.0f)
            {
                float currentPrice = marketEngine.CurrentPrice;
                if (missedSignalStartPrice <= 0f) missedSignalStartPrice = currentPrice;

                float priceDeltaPct = Math.Abs(currentPrice - missedSignalStartPrice) / Mathf.Max(1f, missedSignalStartPrice) * 100f;

                // 1) 신호가 사실 진짜 수익 신호(IsTrueSignal)였거나,
                // 2) 실제 주가가 2% 이상 시원하게 움직여서(수익을 낼 수 있던 타점) 휩소 판단이 틀렸음을 깨달았을 때
                if (trackedMissedSignal.IsTrueSignal || priceDeltaPct >= 2.0f || Math.Abs(trackedMissedSignal.TargetPercentageDelta) >= 2.0f)
                {
                    isTrackingMissedSignal = false; // 중복 후회 방지

                    float fomoPenalty = -15.0f;
                    if (FXOverdose.Trading.TraderLevelSystem.Instance != null && FXOverdose.Trading.TraderLevelSystem.Instance.ChartStudyLevel >= 9)
                    {
                        fomoPenalty *= 0.5f;
                    }

                    traderStatus.ChangeMental(fomoPenalty, false, "수익 타점 놓침(FOMO)");
                    TriggerGimmickDialogue($"FOMO 놓친 기회 후회 기믹 발동 (놓친 상승 {priceDeltaPct:0.0}%, 멘탈 {fomoPenalty} 감소)", "아씨!! 휩소인 줄 알고 쫄아서 안 들어갔는데 진짜 수익 자리였잖아!! 저거 다 내 돈이었는데...!!");

                    Debug.LogWarning($"[MentalDrainGimmickController] 😭 [FOMO/후회 기믹 발동] 휩소에 속아 수익 타점을 놓친 것에 대한 후회로 멘탈 {fomoPenalty} 감소 (놓친 주가 변동: {priceDeltaPct:F2}%)");
                }
                else if (missedSignalTimer >= 60.0f)
                {
                    // 60초간 큰 변동 없이 지나가면 휩소 판단 적중(관망 성공)으로 간주하고 추적 종료
                    isTrackingMissedSignal = false;
                }
            }
        }

        // --- 인게임 분 단위 연산 (기믹 3, 4, 5, 6) ---
        public void OnGameMinuteAdvanced()
        {
            if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
            if (gameManager == null || gameManager.CurrentState != GameManager.GameState.Playing || gameManager.IsFastForwardingTime)
            {
                return;
            }

            if (traderStatus == null) traderStatus = FindAnyObjectByType<TraderStatus>();
            if (tradingController == null) tradingController = FindAnyObjectByType<TradingController>();
            if (marketEngine == null) marketEngine = FindAnyObjectByType<MarketSimulationEngine>();
            if (traderStatus == null || tradingController == null || marketEngine == null)
            {
                return;
            }

            EvaluateDrawdownTrauma();
        }

        // --- [기믹 3: 수면 부족 연쇄 (Sleep Deprivation Cascade)] ---
        // (기획 변경으로 인해 삭제됨)



        // --- [기믹 5: 드로다운(Drawdown) 트라우마] ---
        private void EvaluateDrawdownTrauma()
        {
            float currentBal = traderStatus != null ? traderStatus.GetTotalEquity() : gameManager.CurrentBalance;
            float peak = traderStatus.PeakBalance;

            // 최고 자산 갱신 시 트라우마 완치 및 버프 적용
            if (currentBal > peak)
            {
                traderStatus.PeakBalance = currentBal;
                
                if (traderStatus.HasDrawdownTrauma || traderStatus.Trauma20Applied || traderStatus.Trauma30Applied || traderStatus.Trauma50Applied)
                {
                    traderStatus.HasDrawdownTrauma = false;
                    traderStatus.Trauma20Applied = false;
                    traderStatus.Trauma30Applied = false;
                    traderStatus.Trauma50Applied = false;
                    traderStatus.TraumaConsumedMental = 0f;
                    
                    traderStatus.HasTraumaCureItemBuff = true;
                    traderStatus.HasTraumaCureTradeBuff = true;
                    
                    traderStatus.ChangeMental(20.0f, true);
                    TriggerGimmickDialogue("역대 최고 자산(High Watermark) 돌파 및 트라우마 해방 기믹 발동", "끔찍했던 계좌 반토막의 공포를 이겨내고 마침내 역대 최고 자산을 갱신했어! 과거의 손실 트라우마에서 완전히 해방된 극도의 희열과, 이제 무슨 짓을 해도 다 돈을 벌 수 있을 것 같은 무적의 자신감을 광기 서린 느낌으로 표출해 줘.");
                    Debug.Log("[MentalDrainGimmickController] ✨ 신고점 갱신으로 드로다운 트라우마가 완치되었습니다! (멘탈 +20, 아이템 1.5배 버프, 다음 거래 수익 버프 획득)");
                }
                return;
            }

            float ddPercent = (peak - currentBal) / Mathf.Max(1f, peak) * 100f;
            traderStatus.CurrentDrawdownPercent = ddPercent;

            // 회복 조건: 15% 미만으로 회복 시 소모된 멘탈 + 5 회복
            if (ddPercent < 15.0f && (traderStatus.Trauma20Applied || traderStatus.Trauma30Applied || traderStatus.Trauma50Applied))
            {
                float recoveryAmount = traderStatus.TraumaConsumedMental + 5f;
                traderStatus.ChangeMental(recoveryAmount, true);
                
                traderStatus.Trauma20Applied = false;
                traderStatus.Trauma30Applied = false;
                traderStatus.Trauma50Applied = false;
                traderStatus.TraumaConsumedMental = 0f;
                traderStatus.HasDrawdownTrauma = false;
                
                TriggerGimmickDialogue("드로다운 트라우마 부분 회복 기믹 발동", "계좌가 고점 대비 15% 이내로 복구됐어. 그동안 깎였던 멘탈이 조금 돌아오며 안도의 한숨을 내쉬지만, 아직 신고점을 뚫지 못해 불안감이 살짝 남아있는 심정을 표현해 줘.");
                Debug.Log($"[MentalDrainGimmickController] ✨ 드로다운이 15% 미만으로 회복되어 멘탈 {recoveryAmount} 회복되었습니다.");
                return;
            }

            // 각 구간 돌파 시 즉시 멘탈 소모 누적 (총 40 소모)
            if (ddPercent >= 20.0f && !traderStatus.Trauma20Applied)
            {
                traderStatus.Trauma20Applied = true;
                traderStatus.HasDrawdownTrauma = true;
                traderStatus.TraumaConsumedMental += 10f;
                traderStatus.ChangeMental(-10.0f, false, "드로다운 20% 돌파 트라우마");
                TriggerGimmickDialogue("고점 대비 -20% 하락 트라우마 기믹 발동", "고점 대비 20% 하락했어. 조금씩 깎여나가는 자산을 보며 초조하고 불안해지기 시작하는 감정을 짧게 표현해 줘.");
                Debug.Log("[MentalDrainGimmickController] 📉 드로다운 20% 돌파: 멘탈 -10 소모");
            }
            
            if (ddPercent >= 30.0f && !traderStatus.Trauma30Applied)
            {
                traderStatus.Trauma30Applied = true;
                traderStatus.HasDrawdownTrauma = true;
                traderStatus.TraumaConsumedMental += 10f;
                traderStatus.ChangeMental(-10.0f, false, "드로다운 30% 돌파 트라우마");
                TriggerGimmickDialogue("고점 대비 -30% 하락 트라우마 기믹 발동", "고점 대비 30% 하락했어. 손실이 커지자 호흡이 가빠지고 멘탈이 무너져내리며 뇌동매매의 충동을 느끼는 패닉 상태를 표현해 줘.");
                Debug.Log("[MentalDrainGimmickController] 📉 드로다운 30% 돌파: 멘탈 -10 소모");
            }

            if (ddPercent >= 50.0f && !traderStatus.Trauma50Applied)
            {
                traderStatus.Trauma50Applied = true;
                traderStatus.HasDrawdownTrauma = true;
                traderStatus.TraumaConsumedMental += 20f;
                traderStatus.ChangeMental(-20.0f, false, "드로다운 50% 반토막 트라우마");
                TriggerGimmickDialogue("고점 대비 -50% 반토막 트라우마 기믹 발동", "고점 대비 50% 하락으로 계좌가 반토막 났어. 눈앞이 깜깜해지고, 과거의 실패 트라우마가 겹쳐오며 모든 걸 포기하고 싶은 극심한 절망과 우울감을 생생하게 표현해 줘.");
                Debug.Log("[MentalDrainGimmickController] 📉 드로다운 50% 돌파: 멘탈 -20 소모");
            }
        }
    }
}
