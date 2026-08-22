using System;
using UnityEngine;
using FXOverdose.Trading;

namespace FXOverdose.AI
{
    /// <summary>
    /// 상시 멘탈 소모 기믹 <b>5종</b>(미실현 손실 압박, 연속 손절 콤보, 고배율 중독, 포지션 진입, FOMO 후회)을
    /// 실시간(Update)과 포지션 이벤트로 제어하는 중앙 컨트롤러입니다.
    /// (수면 부족 연쇄·횡보 지루함·드로다운 트라우마는 기획에서 빠졌습니다.)
    /// </summary>
    public class MentalDrainGimmickController : MonoBehaviour
    {
        // 진입 포기한 신호를 "놓친 기회"로 인정하는 주가 변동률 임계치입니다.
        private const float MissedOpportunityThresholdPct = 5.0f;

        // 포지션 진입/물타기 1회당 고정 멘탈 소모.
        private const float PositionOpenMentalCost = 5.0f;

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
        private bool pendingImpulsiveTrade = false;

        /// <summary>
        /// 보스 전용 브레인이 아닌 <b>플레이어(요미) 브레인</b>을 찾습니다.
        /// <c>BossManager</c>가 보스를 스폰하면 <c>[RequireComponent]</c>로 두 번째 <see cref="AITradingBrain"/>이
        /// 생기는데, <c>FindAnyObjectByType</c>은 어느 쪽을 돌려줄지 보장하지 않습니다. 보스 브레인을 잡으면
        /// 고배율 강제 매매 지시(<c>ForceNextTradeHighLeverage</c>)와 FOMO 후회 추적이 엉뚱한 대상에게 걸립니다.
        /// </summary>
        private static AITradingBrain FindPlayerBrain()
        {
            AITradingBrain[] brains = FindObjectsByType<AITradingBrain>(FindObjectsInactive.Include);
            for (int i = 0; i < brains.Length; i++)
            {
                if (brains[i] != null && !brains[i].IsBossAI) return brains[i];
            }
            return null;
        }

        public void CureMentalGimmicks()
        {
            isUnrealizedPnLCured = true;
            isTrackingMissedSignal = false;

            if (traderStatus == null) traderStatus = TraderStatus.CanonicalInstance;
            if (traderStatus != null) traderStatus.CurrentLosingStreak = 0;

            Debug.Log("[MentalDrainGimmickController] 💊 멘탈 감소 기믹들이 1회성으로 치료(초기화)되었습니다.");
        }

        // 휩소 오인 및 FOMO 후회 기믹 관련
        private bool isTrackingMissedSignal = false;
        private MarketSignal trackedMissedSignal;
        private float missedSignalStartPrice = 0f;
        private float missedSignalTimer = 0f;

        private bool isInitialized = false;
        

        private void TriggerGimmickDialogue(string gimmickContext, string fallbackDialogue = "")
        {
            if (visualController != null && !string.IsNullOrEmpty(fallbackDialogue))
            {
                visualController.DisplayDialogueBalloon(fallbackDialogue, DialoguePriority.Normal, FXOverdose.AI.EventCategory.GimmickTriggered);
            }
        }

        private static MentalDrainGimmickController instance;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;

            traderStatus = TraderStatus.CanonicalInstance;
            if (tradingController == null) tradingController = FindAnyObjectByType<TradingController>();
            if (marketEngine == null) marketEngine = FindAnyObjectByType<MarketSimulationEngine>();
            if (gameManager == null) gameManager = GameManager.Instance;
            if (visualController == null) visualController = FindAnyObjectByType<AIVisualController>();
            if (aiBrain == null) aiBrain = FindPlayerBrain();
            
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
            if (gameManager == null) gameManager = GameManager.Instance;
            if (visualController == null) visualController = FindAnyObjectByType<AIVisualController>();
            if (aiBrain == null) aiBrain = FindPlayerBrain();

            if (tradingController != null)
            {
                tradingController.OnPositionClosed -= OnPositionClosed;
                tradingController.OnPositionClosed += OnPositionClosed;
                tradingController.OnPositionOpened -= OnPositionOpened;
                tradingController.OnPositionOpened += OnPositionOpened;
            }

            if (aiBrain != null)
            {
                aiBrain.OnSignalEvaluationCompleted -= OnSignalEvaluationCompleted;
                aiBrain.OnSignalEvaluationCompleted += OnSignalEvaluationCompleted;
            }

            if (!isInitialized)
            {
                isInitialized = true;
                Debug.Log("[MentalDrainGimmickController] 🧠 상시 멘탈 소모 기믹 5종 코어 엔진이 초기화되었습니다.");
            }
        }

        private void Update()
        {
            if (gameManager == null) gameManager = GameManager.Instance;
            if (gameManager == null || gameManager.CurrentState != GameManager.GameState.Playing || gameManager.IsFastForwardingTime)
            {
                return;
            }

            if (traderStatus == null) traderStatus = TraderStatus.CanonicalInstance;
            if (tradingController == null) tradingController = FindAnyObjectByType<TradingController>();
            if (marketEngine == null) marketEngine = FindAnyObjectByType<MarketSimulationEngine>();
            if (aiBrain == null)
            {
                aiBrain = FindPlayerBrain();
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
            EvaluateMissedOpportunityRegret(dt);

            if (pendingImpulsiveTrade)
            {
                pendingImpulsiveTrade = false;
                if (traderStatus != null) traderStatus.TriggerImpulsiveTrade(100);
            }
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

            if (tradingController.IsOverdoseProtected() || tradingController.IsEventTradeActive)
            {
                drainRate *= 0.5f;
            }

            roeDrainAccumulator += deltaTime;

            // 1초 단위로 누적하여 한 번에 차감 (UI 스팸 방지 및 가독성 향상)
            if (roeDrainAccumulator >= 1.0f)
            {
                traderStatus.ChangeMental(-drainRate, "미실현 손실 압박");
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



        // --- [기믹 2: 연속 손절 콤보 (Losing Streak Multiplier) 및 기믹 4 중독 감지] ---
        private void OnPositionClosed(float returnedAmount, float realizedPnL)
        {
            isUnrealizedPnLCured = false;

            if (traderStatus == null) traderStatus = TraderStatus.CanonicalInstance;
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
                        TriggerGimmickDialogue("고배율 중독 폭주 기믹 발동", "아 진짜 답답해 미치겠네!! 장난쳐?! 그딴 푼돈으로 언제 부자 될 건데?! 이리 내, 내가 직접 할 거야!!");
                        
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
            }
            else
            {


                // 손실 청산
                traderStatus.CurrentLosingStreak++;
                traderStatus.ConsecutiveHighLevWins = 0;

                int streak = traderStatus.CurrentLosingStreak;
                float penalty = 0f;

                // 4연속 손절 뇌동매매 기믹이 발동하려면 3연속 손절 후에도 멘탈이 남아 있어야 합니다.
                // 합산 피해 예산(진입 + 손실 청산 + 연속 손절)에서 역산한 값입니다.
                switch (streak)
                {
                    case 1:
                        penalty = 4.0f;
                        break;
                    case 2:
                        penalty = 9.0f;
                        TriggerGimmickDialogue("2연속 손절 휩소 자책 기믹 발동 (손실 스트레스)", "아씨, 꼬리만 털고 왜 반대로 가는데?!");
                        break;
                    case 3:
                        penalty = 16.0f;
                        TriggerGimmickDialogue("3연속 손절 피해망상 기믹 발동 (세력 조롱 피해의식 심화)", "차트가 날 감시하고 조롱하는 게 분명해...!");
                        Debug.LogWarning("[MentalDrainGimmickController] 🔴 [LOSE x3] 3연속 손절! 극도의 자격지심 발생");
                        break;
                    default: // 4연속 이상
                        penalty = 0.0f; // 4연속은 뇌동매매가 발동하므로 추가 페널티로 오버도즈가 겹치는 것을 방지
                        TriggerGimmickDialogue("4연속 손절 복수심 100배 뇌동매매 기믹 발동 (극도의 분노와 자제력 상실)", "4연속 손절... 더는 못 참아! 지금 당장 100배로 싹 다 복구한다!!");
                        Debug.LogWarning("[MentalDrainGimmickController] 🔴 [LOSE x4+] 4연속 손절 감지! 즉시 100배 뇌동매매 돌입!");
                        pendingImpulsiveTrade = true;
                        break;
                }

                // --- [신규 기믹: 수동 매매 책임 전가] ---
                bool isManualTradeLoss = tradingController != null && tradingController.CurrentOwner == TradingController.OwnerType.Player;
                if (isManualTradeLoss && penalty > 0f) 
                {
                    penalty *= 1.5f; // 페널티 1.5배 증폭
                    
                    string blameDialogue = $"거봐! 요미 말 안 듣고 오빠가 맘대로 쳐서 돈 날렸잖아!!"; // Fallback
                    var matcher = FXOverdose.AI.Dialogue.YomiDialogueMatcher.Instance;
                    if (matcher != null)
                    {
                        float marginRatio = (gameManager != null && gameManager.CurrentBalance > 0) ? (tradingController.LastMarginAmount / gameManager.CurrentBalance) : 0f;
                        string fetched = matcher.GetEventDialogue("수동매매책임전가", currentLeverage: tradingController.CurrentLeverage, currentMarginRatio: marginRatio);
                        if (!string.IsNullOrEmpty(fetched)) blameDialogue = fetched;
                    }

                    TriggerGimmickDialogue("수동 매매 책임 전가 기믹 발동", blameDialogue);
                    Debug.LogWarning($"[MentalDrainGimmickController] 😡 [책임 전가] 플레이어 수동 매매 손실로 멘탈 페널티 증폭: -{penalty:F1}");
                }

                string reason = streak == 1 ? "손실 청산 스트레스" : $"{streak}연속 손절 스트레스";
                if (isManualTradeLoss) reason += " (수동 매매 원망)";
                traderStatus.ChangeMental(-penalty, reason);
            }
        }

        // --- [신규 기믹: 매 거래 실행(포지션 진입/물타기) 시 고정 10 멘탈 소모] ---
        private void OnPositionOpened(TradingController.PositionType type, float margin, int leverage)
        {
            // ⚠️ 여기서 isUnrealizedPnLCured를 지우지 않습니다. 무포지션 상태에서 치료 아이템을 쓰고
            //    바로 진입하면 치료가 그 즉시 무효가 되어 아이템 값어치가 사라졌습니다.
            //    치료는 "포지션 하나를 덮는 1회성"이므로 해제는 OnPositionClosed에서만 합니다.

            if (traderStatus == null) traderStatus = TraderStatus.CanonicalInstance;
            if (traderStatus == null) return;
            traderStatus.ChangeMental(-PositionOpenMentalCost, "포지션 진입/물타기");
            Debug.Log($"[MentalDrainGimmickController] 💸 매매 실행({type}, {leverage}배)으로 고정 멘탈 -{PositionOpenMentalCost} 감소 (현재 멘탈: {traderStatus.CurrentMental:F1})");
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
                // 이미 포지션이 있어서 진입에 실패한 경우는 휩소에 쫄아서 안 들어간 게 아니므로 후회(FOMO) 대상에서 제외!
                if (tradingController != null && tradingController.CurrentPosition != TradingController.PositionType.None)
                {
                    isTrackingMissedSignal = false;
                    return;
                }

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
                // 2) 실제 주가가 임계치 이상 시원하게 움직여서(수익을 낼 수 있던 타점) 휩소 판단이 틀렸음을 깨달았을 때
                if (trackedMissedSignal.IsTrueSignal
                    || priceDeltaPct >= MissedOpportunityThresholdPct
                    || Math.Abs(trackedMissedSignal.TargetPercentageDelta) >= MissedOpportunityThresholdPct)
                {
                    isTrackingMissedSignal = false; // 중복 후회 방지

                    float fomoPenalty = -15.0f;
                    if (FXOverdose.Trading.TraderLevelSystem.Instance != null && FXOverdose.Trading.TraderLevelSystem.Instance.ChartStudyLevel >= 9)
                    {
                        fomoPenalty *= 0.5f;
                    }

                    traderStatus.ChangeMental(fomoPenalty, "수익 타점 놓침(FOMO)");
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

        // 인게임 분 단위 연산은 없습니다. 담당하던 기믹(수면 부족 연쇄·횡보 지루함·드로다운 트라우마)이
        // 전부 기획에서 빠지면서 본문이 빈 껍데기로 남아 매 분 헛돌던 것을 핸들러째 제거했습니다.
        // 다시 필요해지면 OnGameMinuteAdvanced 핸들러와 GameManager 구독을 함께 되살리십시오.




    }
}

