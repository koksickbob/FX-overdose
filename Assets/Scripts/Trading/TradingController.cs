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

        [Header("시스템 연결")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private MarketSimulationEngine marketEngine;
        [SerializeField] private TraderStatus traderStatus;

        [Header("현재 포지션 상태 (읽기 전용)")]
        [SerializeField] private PositionType currentPosition = PositionType.None;
        [SerializeField] private float entryPrice;
        [SerializeField] private float marginAmount; // 투입한 증거금
        [SerializeField] private int currentLeverage = 10;
        [SerializeField] private float liquidationPrice;

        public PositionType CurrentPosition => currentPosition;
        public float EntryPrice => entryPrice;
        public float MarginAmount => marginAmount;
        public int CurrentLeverage => currentLeverage;
        public float LiquidationPrice => liquidationPrice;

        // 이벤트 통지
        public event Action OnPositionChanged;
        public event Action OnPositionLiquidated;
        public event Action<float, float> OnPositionClosed; // (최종 회수금, PnL)

        private void Start()
        {
            if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
            if (marketEngine == null) marketEngine = FindAnyObjectByType<MarketSimulationEngine>();
            if (traderStatus == null) traderStatus = FindAnyObjectByType<TraderStatus>();

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

        private void HandlePriceUpdated(float price)
        {
            if (currentPosition == PositionType.None) return;

            // 강제 청산(Liquidation) 판정
            CheckLiquidation(price);
        }

        // 포지션 진입 (Long 또는 Short)
        public bool OpenPosition(PositionType type, float margin, int leverage)
        {
            if (gameManager == null || marketEngine == null || type == PositionType.None)
            {
                return false;
            }

            if (currentPosition != PositionType.None)
            {
                Debug.LogWarning("[TradingController] 이미 포지션을 보유 중입니다. 먼저 포지션을 종료하세요.");
                return false;
            }

            if (gameManager.CurrentBalance < margin || margin <= 0f)
            {
                Debug.LogWarning("[TradingController] 증거금이 부족합니다.");
                return false;
            }

            leverage = Mathf.Clamp(leverage, 1, 125);

            // 증거금 차감
            gameManager.ChangeBalance(-margin);

            currentPosition = type;
            entryPrice = marketEngine.CurrentPrice;
            marginAmount = margin;
            currentLeverage = leverage;

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

            Debug.Log($"[TradingController] {type} 포지션 진입! 진입가: {entryPrice:N1}, 증거금: {margin:N0}, 레버리지: {leverage}x, 청산가: {liquidationPrice:N1}");
            OnPositionChanged?.Invoke();
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
                    // 수익 시 흥분 및 기분 회복
                    traderStatus.ChangeMental(pnl * 0.02f);
                    traderStatus.ChangeHealth(5f);
                }
            }

            Debug.Log($"[TradingController] 포지션 종료. 실현 손익: {pnl:N1} ({CalculateROEPercentage():F2}%), 최종 회수금: {totalReturn:N0}");

            PositionType closedType = currentPosition;
            currentPosition = PositionType.None;

            OnPositionClosed?.Invoke(totalReturn, pnl);
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

            // 증거금 전액 몰수 (정산금 0원)
            marginAmount = 0f;
            currentPosition = PositionType.None;

            // AI 트레이더 멘탈 대붕괴 (Overdose 직전 또는 즉시 도달)
            if (traderStatus != null)
            {
                traderStatus.ChangeMental(-60f);
            }

            OnPositionLiquidated?.Invoke();
            OnPositionChanged?.Invoke();
        }

        // 멘헤라 AI 트레이더 폭주(Overdose 상태) 시 호출되는 고레버리지 뇌동매매 실행 함수
        public void TriggerOverdoseTrade()
        {
            if (gameManager == null || marketEngine == null) return;

            Debug.LogWarning("[TradingController] 🩸 [Overdose 폭주] AI 트레이더가 통제를 벗어나 고레버리지 뇌동매매를 강행합니다!");

            // 기존 포지션이 없다면 남은 자산의 50% 이상을 고레버리지(100배~125배)로 진입
            if (currentPosition == PositionType.None)
            {
                float forcedMargin = gameManager.CurrentBalance * UnityEngine.Random.Range(0.5f, 0.8f);
                int forcedLeverage = UnityEngine.Random.Range(100, 126);
                PositionType forcedDirection = UnityEngine.Random.value > 0.5f ? PositionType.Long : PositionType.Short;

                if (forcedMargin > 10f)
                {
                    OpenPosition(forcedDirection, forcedMargin, forcedLeverage);
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
                    Debug.LogWarning($"[TradingController] 🩸 [Overdose 폭주] 남은 자금 {addMargin:N0}원 전부 물타기 및 레버리지 125배 상향!");
                    OnPositionChanged?.Invoke();
                }
            }
        }
    }
}
