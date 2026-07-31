using UnityEngine;
using FXOverdose.Core;
using FXOverdose.Trading;

namespace FXOverdose.AI
{
    [RequireComponent(typeof(AITradingBrain))]
    public class BossAIController : MonoBehaviour
    {
        private AITradingBrain brain;
        private BossLevelProvider levelProvider;
        private MarketSimulationEngine marketEngine;

        public bool IsHoldingPosition { get; private set; }
        public TradingController.PositionType CurrentPositionType { get; private set; }
        public float EntryPrice { get; private set; }
        public float MarginAmount { get; private set; }
        public int Leverage { get; private set; }
        
        private float virtualTargetPrice;
        private float virtualStopLossPrice;
        private float holdTimer;
        private const float MaxHoldTime = 120f;

        public float CurrentRealTimePnL { get; private set; }
        public float CurrentRealTimeReturnRate { get; private set; }
        
        private GameManager gameManager;
        private int skippedSignalCount = 0;

        private void Awake()
        {
            brain = GetComponent<AITradingBrain>();
            brain.IsBossAI = true; // 체력/멘탈 소모 끄기 (무적 기믹)
            marketEngine = FindAnyObjectByType<MarketSimulationEngine>(FindObjectsInactive.Include);

            brain.GetAvailableBalance = () => BossManager.Instance != null ? BossManager.Instance.BossCurrentAsset : 0f;
            
            brain.TradeExecutor = (posType, margin, lev, tgt, sl, crazy, price) => 
            {
                if (BossManager.Instance == null || BossManager.Instance.IsBossBankrupt) return false;
                if (IsHoldingPosition) return false;
                
                IsHoldingPosition = true;
                CurrentPositionType = posType;
                EntryPrice = price > 0f ? price : (marketEngine != null ? marketEngine.CurrentPrice : 65000f);
                MarginAmount = margin;
                Leverage = lev;
                
                // 보스 모의 매매 결과: 스킬 레벨에 따른 승률로 목표 도달 여부를 미리 판단할 수도 있지만,
                // 플레이어와 동일하게 진짜 차트 움직임(tgt, sl)을 따라가게 만듭니다. (진짜 빔을 타게 됨)
                virtualTargetPrice = tgt;
                virtualStopLossPrice = sl;
                holdTimer = 0f;
                
                CurrentRealTimePnL = 0f;
                CurrentRealTimeReturnRate = 0f;
                
                return true; 
            };
            
            gameManager = FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            if (marketEngine != null)
            {
                marketEngine.OnMarketSignalGenerated += HandleMarketSignalGenerated;
            }
            if (gameManager != null)
            {
                gameManager.OnFastForwardEnded += HandleFastForwardEnded;
            }
        }

        private void OnDestroy()
        {
            if (marketEngine != null)
            {
                marketEngine.OnMarketSignalGenerated -= HandleMarketSignalGenerated;
            }
            if (gameManager != null)
            {
                gameManager.OnFastForwardEnded -= HandleFastForwardEnded;
            }
        }

        private void HandleMarketSignalGenerated(MarketSignal signal)
        {
            if (gameManager != null && gameManager.IsFastForwardingTime)
            {
                skippedSignalCount++;
            }
        }

        private void HandleFastForwardEnded()
        {
            if (skippedSignalCount > 0 && BossManager.Instance != null && !BossManager.Instance.IsBossBankrupt)
            {
                SimulateBulkTrades(skippedSignalCount);
                skippedSignalCount = 0;
            }
        }

        private void SimulateBulkTrades(int tradeCount)
        {
            if (levelProvider == null || BossManager.Instance == null) return;

            float currentAsset = BossManager.Instance.BossCurrentAsset;
            if (currentAsset <= 0f) return;

            int leverage = levelProvider.GetMaxAllowedLeverage();
            float marginRatio = levelProvider.GetMaxAllowedMarginRatio() * 0.5f; 
            float accuracy = levelProvider.GetSignalAccuracy();
            
            float baseDelta = 0.045f;
            float takeProfit = baseDelta * levelProvider.GetTakeProfitMultiplier();
            float stopLoss = levelProvider.GetStopLossTightness();

            float totalProfitAndLoss = 0f;

            for (int i = 0; i < tradeCount; i++)
            {
                float tradeMargin = currentAsset * marginRatio;
                
                if (UnityEngine.Random.value <= accuracy)
                {
                    float roe = takeProfit * leverage;
                    float profit = tradeMargin * roe;
                    totalProfitAndLoss += profit;
                    currentAsset += profit;
                }
                else
                {
                    float roe = stopLoss * leverage;
                    float loss = tradeMargin * Mathf.Abs(roe);
                    totalProfitAndLoss -= loss;
                    currentAsset -= loss;
                }

                if (currentAsset <= 0f)
                {
                    currentAsset = 0f;
                    break;
                }
            }

            BossManager.Instance.UpdateBossAsset(currentAsset);
            Debug.Log($"[BossAIController] 고속 시간 가속 종료. 가상 거래 {tradeCount}회 일괄 정산 완료 -> 누적 손익: {totalProfitAndLoss:N0}, 현재 자산: {currentAsset:N0}");
        }

        private void Update()
        {
            if (IsHoldingPosition && marketEngine != null && BossManager.Instance != null && !BossManager.Instance.IsBossBankrupt)
            {
                float currentPrice = marketEngine.CurrentPrice;
                
                // PnL 계산
                float priceDiff = CurrentPositionType == TradingController.PositionType.Long 
                    ? currentPrice - EntryPrice 
                    : EntryPrice - currentPrice;
                    
                CurrentRealTimeReturnRate = (priceDiff / EntryPrice) * Leverage * 100f;
                CurrentRealTimePnL = MarginAmount * (CurrentRealTimeReturnRate / 100f);
                
                bool shouldClose = false;
                holdTimer += Time.deltaTime;
                
                // 목표가/손절가 도달 체크
                if (CurrentPositionType == TradingController.PositionType.Long)
                {
                    if (virtualTargetPrice > 0f && currentPrice >= virtualTargetPrice) shouldClose = true;
                    if (virtualStopLossPrice > 0f && currentPrice <= virtualStopLossPrice) shouldClose = true;
                }
                else
                {
                    if (virtualTargetPrice > 0f && currentPrice <= virtualTargetPrice) shouldClose = true;
                    if (virtualStopLossPrice > 0f && currentPrice >= virtualStopLossPrice) shouldClose = true;
                }
                
                // 강제 청산 (ROE <= -100%)
                if (CurrentRealTimeReturnRate <= -100f)
                {
                    CurrentRealTimeReturnRate = -100f;
                    CurrentRealTimePnL = -MarginAmount;
                    shouldClose = true;
                }
                
                // 타임아웃
                if (holdTimer > MaxHoldTime)
                {
                    shouldClose = true;
                }
                
                if (shouldClose)
                {
                    BossManager.Instance.UpdateBossAsset(BossManager.Instance.BossCurrentAsset + CurrentRealTimePnL);
                    IsHoldingPosition = false;
                    CurrentRealTimePnL = 0f;
                    CurrentRealTimeReturnRate = 0f;
                }
            }
        }

        public void InitializeForBoss(BossData bossData, float startingAsset)
        {
            levelProvider = new BossLevelProvider(bossData.SkillLevel);
            brain.SetLevelProvider(levelProvider);
        }
    }
}
