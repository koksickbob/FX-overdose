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

        [SerializeField] private float targetPrice;   // AI가 결정한 목표 주가 (익절가)
        [SerializeField] private float stopLossPrice; // AI가 결정한 손절가

        private float lastMarginAmount = 0f;

        public PositionType CurrentPosition => currentPosition;
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

        // 테스트 및 디버그용 포지션 청산 시뮬레이션 Helper
        public void SimulateCloseForTest(bool isProfit, float pnl)
        {
            if (currentPosition == PositionType.None) return;
            float returned = marginAmount + pnl;
            if (returned < 0f) returned = 0f;
            lastMarginAmount = marginAmount;

            // 💡 [이벤트 순서 수정] 이벤트 수신자(AI, UI)가 활성 증거금(MarginAmount) 및 포지션 정보를 정확히 읽을 수 있도록 청산 직전에 이벤트 발송!
            OnPositionClosed?.Invoke(returned, pnl);

            currentPosition = PositionType.None;
            targetPrice = 0f;
            stopLossPrice = 0f;
            OnPositionChanged?.Invoke();
        }

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

            // 1. 강제 청산(Liquidation) 판정
            CheckLiquidation(price);
            if (currentPosition == PositionType.None) return;

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
        }

        // 포지션 진입 (AI가 방향, 레버리지, 목표가 TargetPrice를 독자적으로 결정하여 호출)
        public bool OpenPosition(PositionType type, float margin, int leverage, float aiTargetPrice = 0f, float aiStopLossPrice = 0f)
        {
            if (gameManager == null || marketEngine == null || type == PositionType.None)
            {
                return false;
            }

            if (currentPosition != PositionType.None)
            {
                Debug.LogWarning($"[TradingController] 🔄 기존 {currentPosition} 포지션 보유 중 새로운 {type} 포지션 진입 요청 감지 -> 기존 포지션을 정리하고 스위칭합니다.");
                ClosePosition();
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
            targetPrice = aiTargetPrice;
            stopLossPrice = aiStopLossPrice;

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
            lastMarginAmount = marginAmount;

            // 💡 [이벤트 순서 수정] 이벤트 수신자가 활성 증거금 및 PnL ROE를 정확히 읽을 수 있도록 청산 상태 초기화 직전에 발송!
            OnPositionClosed?.Invoke(totalReturn, pnl);

            currentPosition = PositionType.None;
            targetPrice = 0f;
            stopLossPrice = 0f;
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
            OnPositionChanged?.Invoke();
        }

        // 멘헤라 AI 트레이더 폭주(Overdose 상태) 시 호출되는 고레버리지 뇌동매매 실행 함수
        public void TriggerOverdoseTrade()
        {
            if (gameManager == null || marketEngine == null) return;

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
                }
            }
        }

        // --- 돌발 선택 이벤트 연동 메서드 ---
        public void ExecuteEmergencyTrade(PositionType posType, int leverage)
        {
            if (posType == PositionType.None)
            {
                CloseAllPositions();
                return;
            }

            if (currentPosition != PositionType.None && currentPosition != posType)
            {
                ClosePosition();
            }

            if (currentPosition == PositionType.None && gameManager != null && marketEngine != null)
            {
                float forcedMargin = Mathf.Max(10f, gameManager.CurrentBalance * 0.4f);
                if (forcedMargin <= gameManager.CurrentBalance)
                {
                    float currentP = marketEngine.CurrentPrice;
                    float aiTarget = posType == PositionType.Long ? currentP * 1.15f : currentP * 0.85f;
                    float aiStop = posType == PositionType.Long ? currentP * 0.95f : currentP * 1.05f;
                    OpenPosition(posType, forcedMargin, Mathf.Clamp(leverage, 1, 125), aiTarget, aiStop);
                    Debug.Log($"[TradingController] ⚡ ExecuteEmergencyTrade 실행: {posType} / 레버리지 {leverage}배");
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
