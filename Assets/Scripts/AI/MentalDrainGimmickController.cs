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
        private int sidewaysStreakMinutes = 0;
        private float lastCheckPrice = -1f;

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

            float dt = Time.deltaTime;
            EvaluateUnrealizedPnLErosion(dt);
            EvaluateLosingStreakCountdown(dt);
            EvaluateMissedOpportunityRegret(dt);
        }

        // --- [기믹 1: 미실현 손실(Unrealized P&L) 실시간 침식] ---
        private void EvaluateUnrealizedPnLErosion(float deltaTime)
        {
            if (!tradingController.IsActive || tradingController.MarginAmount <= 0f)
            {
                panicDialogueTimer = 0f;
                return;
            }

            float roe = tradingController.CalculateROEPercentage();
            if (roe > -5.0f)
            {
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

            traderStatus.ChangeMental(-drainRate * deltaTime);

            // ROE <= -20% 지속 시 7초 주기로 불안/패닉 독백 출력
            if (roe <= -20.0f)
            {
                panicDialogueTimer += deltaTime;
                if (panicDialogueTimer >= 7.0f)
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
                Debug.LogWarning("[MentalDrainGimmickController] ⚡ 4연속 손절 후 15초 내 개입 없음 -> AI 100배 강제 뇌동매매 강행!");
                traderStatus.TriggerImpulsiveTrade(100);
            }
        }

        // --- [기믹 2: 연속 손절 콤보 (Losing Streak Multiplier) 및 기믹 4 중독 감지] ---
        private void OnPositionClosed(float returnedAmount, float realizedPnL)
        {
            if (traderStatus == null) traderStatus = FindAnyObjectByType<TraderStatus>();
            if (tradingController == null) tradingController = FindAnyObjectByType<TradingController>();
            if (traderStatus == null || tradingController == null) return;

            int closedLeverage = tradingController.CurrentLeverage;

            if (realizedPnL >= 0f)
            {
                // 수익 청산
                traderStatus.CurrentLosingStreak = 0;
                isImpulsiveCountdownActive = false;

                // [기믹 4: 고배율 중독 조건 검사 - 50배 이상 연속 3회 익절 시 중독 발동]
                if (closedLeverage >= 50)
                {
                    traderStatus.ConsecutiveHighLevWins++;
                    if (traderStatus.ConsecutiveHighLevWins >= 3 && !traderStatus.IsLeverageAddicted)
                    {
                        traderStatus.IsLeverageAddicted = true;
                        TriggerGimmickDialogue("50배 이상 고배율 3연승 과몰입 중독 기믹 발동 (도파민 폭주 및 희열)", "그래!! 바로 이 느낌이야!! 호가창의 진동이 온몸에 짜릿하게 감돈다!!");
                        Debug.LogWarning("[MentalDrainGimmickController] 🎰 [고배율 중독 금단현상 발동] 50배 이상 3연승으로 AI가 고배율에 중독되었습니다!");
                    }
                }
            }
            else
            {
                // 플레이어 수동 매매 모드일 때는 손절 시에도 연속 손절 콤보 멘탈 감소 및 휩소 자책 기믹을 발생시키지 않음!
                if (tradingController.ActiveTradingMode == TradingController.TradingMode.Player_Manual)
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
                        penalty = 40.0f;
                        isImpulsiveCountdownActive = true;
                        impulsiveTradeCountdownTimer = 15.0f;
                        TriggerGimmickDialogue("4연속 손절 복수심 100배 뇌동매매 카운트다운 기믹 발동 (극도의 분노와 자제력 상실)", "4연속 손절... 더는 못 참아! 15초 내에 100배로 올인해서 싹 다 복구한다!!");
                        Debug.LogWarning("[MentalDrainGimmickController] 🔴 [LOSE x4+] 4연속 손절! 15초 카운트다운 돌입");
                        break;
                }

                traderStatus.ChangeMental(-penalty);
            }
        }

        // --- [신규 기믹: 매 거래 실행(포지션 진입/물타기) 시 고정 10 멘탈 소모] ---
        private void OnPositionOpened(TradingController.PositionType type, float margin, int leverage)
        {
            if (traderStatus == null) traderStatus = FindAnyObjectByType<TraderStatus>();
            if (traderStatus == null) return;
            traderStatus.ChangeMental(-10.0f);
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

                    traderStatus.ChangeMental(-15.0f);
                    TriggerGimmickDialogue($"FOMO 놓친 기회 후회 기믹 발동 (놓친 상승 {priceDeltaPct:0.0}%, 멘탈 -15 감소)", "아씨!! 휩소인 줄 알고 쫄아서 안 들어갔는데 진짜 수익 자리였잖아!! 저거 다 내 돈이었는데...!!");

                    Debug.LogWarning($"[MentalDrainGimmickController] 😭 [FOMO/후회 기믹 발동] 휩소에 속아 수익 타점을 놓친 것에 대한 후회로 멘탈 -15 감소 (놓친 주가 변동: {priceDeltaPct:F2}%)");
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

            EvaluateSleepDeprivationCascade();
            EvaluateLeverageAddiction();
            EvaluateBoredomDrain();
            EvaluateDrawdownTrauma();
        }

        // --- [기믹 3: 수면 부족 연쇄 (Sleep Deprivation Cascade)] ---
        private void EvaluateSleepDeprivationCascade()
        {
            float healthRatio = traderStatus.HealthRatio;

            if (healthRatio <= 0.30f)
            {
                traderStatus.CanRegenMental = false;
            }
            else
            {
                traderStatus.CanRegenMental = true;
            }

            if (healthRatio <= 0.05f)
            {
                traderStatus.ChangeMental(-3.0f);
                if (UnityEngine.Random.value < 0.1f)
                {
                    TriggerGimmickDialogue("수면 부족 탈진 상태 (체력 5% 이하, 시야 깜빡임 및 극심한 피로감 호소)", "눈 앞이 정전된 것처럼 깜빡거려... 머리가 깨질 것 같아...");
                }
            }
            else if (healthRatio <= 0.15f) // Exhausted
            {
                traderStatus.ChangeMental(-1.0f);
            }
        }

        // --- [기믹 4: 고배율 중독 금단현상 (Leverage Addiction)] ---
        private void EvaluateLeverageAddiction()
        {
            if (!traderStatus.IsLeverageAddicted) return;

            if (tradingController.IsActive)
            {
                if (tradingController.CurrentLeverage < 50)
                {
                    traderStatus.ChangeMental(-3.0f);
                    if (UnityEngine.Random.value < 0.12f)
                    {
                        TriggerGimmickDialogue($"저배율 매매 도파민 결핍 금단현상 기믹 ({tradingController.CurrentLeverage}배에서 100배 스위칭 충동)", "10배? 10배로 뭘 먹으라고...? 이건 매매가 아니야, 소꿉장난이지. 찌릿한 그 감각이 필요해... 100배로 올리자!! 당장!!");
                    }
                }
                else
                {
                    // 50배 이상 고배율 유지 중이면 일시적 안도
                    traderStatus.ChangeMental(0.1f, true);
                }
            }
            else
            {
                // 무포지션 대기 시 금단현상
                traderStatus.ChangeMental(-2.0f);
                if (UnityEngine.Random.value < 0.12f)
                {
                    TriggerGimmickDialogue("무포지션 대기 고배율 금단현상 기믹 (손가락 떨림 및 진입 충동)", "엔터키 누르고 싶어 미치겠네... 호가창이 날 부르고 있다고... 한 번만 당기게 해줘 마스터...!");
                }
            }
        }

        // --- [기믹 5: 횡보장 지루함 스트레스 (Boredom Drain)] ---
        private void EvaluateBoredomDrain()
        {
            bool isSideways = marketEngine.CurrentRegime == MarketSimulationEngine.MarketRegime.Sideways;
            
            if (isSideways)
            {
                sidewaysStreakMinutes++;
            }
            else
            {
                sidewaysStreakMinutes = 0;
            }

            lastCheckPrice = marketEngine.CurrentPrice;

            if (sidewaysStreakMinutes >= 240) // 4시간 이상 연속 횡보
            {
                traderStatus.ChangeMental(-3.0f);

                // 60분 주기로 자극 섭취 또는 20배 단타 시도
                if (sidewaysStreakMinutes % 60 == 0)
                {
                    TriggerBoredomImpulse();
                }
            }
            else if (sidewaysStreakMinutes >= 180) // 3시간 이상 연속 횡보
            {
                traderStatus.ChangeMental(-2.0f);
                if (sidewaysStreakMinutes == 180 || UnityEngine.Random.value < 0.1f)
                {
                    TriggerGimmickDialogue("장시간 횡보장 극도의 지루함 스트레스 및 신경질 기믹 발동", "아 왜 안 움직여?! 위든 아래든 좋으니까 제발 움직이란 말이야!!");
                }
            }
        }

        private void TriggerBoredomImpulse()
        {
            // 1순위: 에너지 드링크 등 소비 시도 (인벤토리/상점에서 자동 사용)
            bool consumedDrink = traderStatus.ConsumeItem(0, 1);
            if (consumedDrink)
            {
                TriggerGimmickDialogue("횡보장 지루함 스트레스 탈피를 위한 에너지 드링크 자가 섭취 기믹 발동", "너무 지루해서 몰래 에너지 드링크를 하나 땄어...");
                Debug.Log("[MentalDrainGimmickController] 🥤 지루함 해소를 위해 AI가 스스로 에너지 드링크를 소모했습니다.");
            }
            else if (!tradingController.IsActive)
            {
                // 2순위: 무포지션 상태라면 20배 단타 랜덤 진입
                TradingController.PositionType randomPos = 
                    (UnityEngine.Random.value > 0.5f) 
                    ? TradingController.PositionType.Long 
                    : TradingController.PositionType.Short;

                tradingController.ExecuteEmergencyTrade(randomPos, 20);
                TriggerGimmickDialogue($"횡보장 지루함을 견디지 못한 묻지마 {randomPos} 20배 진입 기믹 발동", "아 심심해!! 그냥 20배로 아무데나 찔러보자!!");
                Debug.LogWarning($"[MentalDrainGimmickController] 🎰 지루함을 이기지 못한 AI가 {randomPos} 20배 진입을 시도했습니다.");
            }
        }

        // --- [기믹 6: 드로다운(Drawdown) 트라우마 천장 제한] ---
        private void EvaluateDrawdownTrauma()
        {
            // 💡 [드로다운 오판 버그 수정] 포지션 개설 시 증거금이 현금 잔고(CurrentBalance)에서 차감되므로,
            // 단순 현금 잔고가 아닌 포지션 증거금과 미실현 손익이 합산된 총 자산(Total Equity)을 기준으로 드로다운을 판정해야 함!
            float currentBal = traderStatus != null ? traderStatus.GetTotalEquity() : gameManager.CurrentBalance;
            float peak = traderStatus.PeakBalance;

            // 최고 자산 갱신 시 트라우마 완치
            if (currentBal > peak)
            {
                traderStatus.PeakBalance = currentBal;
                if (traderStatus.HasDrawdownTrauma)
                {
                    traderStatus.HasDrawdownTrauma = false;
                    traderStatus.SetMaxMentalCeiling(traderStatus.MaxMental);
                    traderStatus.ChangeMental(30.0f, true);
                    TriggerGimmickDialogue("역대 최고 자산(High Watermark) 돌파 및 트라우마 해방 기믹 발동 (희열과 안도감)", "드디어 신고점 돌파!! 지긋지긋한 트라우마에서 벗어났다!!");
                    Debug.Log("[MentalDrainGimmickController] ✨ 신고점 갱신으로 드로다운 트라우마가 완치되었습니다!");
                }
                return;
            }

            float ddPercent = (peak - currentBal) / Mathf.Max(1f, peak) * 100f;
            traderStatus.CurrentDrawdownPercent = ddPercent;

            if (ddPercent >= 20.0f)
            {
                traderStatus.HasDrawdownTrauma = true;
                float targetCeiling = ddPercent >= 50.0f ? 45.0f : (ddPercent >= 30.0f ? 60.0f : 80.0f);

                // 이미 디저트 등의 아이템 사용으로 한계치를 극복/상승시켰다면, 드로다운이 더 심각해지지 않는 한 다시 깎지 않음
                if (traderStatus.MaxMentalLimit > targetCeiling)
                {
                    traderStatus.SetMaxMentalCeiling(targetCeiling);
                }

                if (ddPercent >= 50.0f && UnityEngine.Random.value < 0.05f)
                {
                    TriggerGimmickDialogue($"최고점 대비 -{ddPercent:0.0}% 반토막 트라우마 기믹 발동 (멘탈 천장 45 제한, 극심한 절망)", "난 실패자야... 다시는 저 고점으로 돌아갈 수 없어...");
                }
            }
            else
            {
                if (ddPercent < 15.0f && traderStatus.HasDrawdownTrauma)
                {
                    traderStatus.HasDrawdownTrauma = false;
                    traderStatus.SetMaxMentalCeiling(traderStatus.MaxMental);
                    Debug.Log("[MentalDrainGimmickController] ✨ 드로다운이 15% 미만으로 회복되어 트라우마 천장이 해제되었습니다.");
                }
                else if (!traderStatus.HasDrawdownTrauma)
                {
                    traderStatus.SetMaxMentalCeiling(traderStatus.MaxMental);
                }
            }
        }
    }
}
