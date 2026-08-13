using System;

namespace FXOverdose.P2P.Core
{
    /// <summary>Long·Short 손익, 수수료와 청산가를 계산하는 순수 함수 모음입니다.</summary>
    public static class P2PTradeCalculator
    {
        public static double CalculateUnrealizedPnL(P2PPositionState position, double marketPrice)
        {
            if (!position.IsOpen || !P2PMatchRules.IsFinitePositive(position.EntryPrice) || !P2PMatchRules.IsFinitePositive(marketPrice))
                return 0d;

            double priceChangeRatio = (marketPrice - position.EntryPrice) / position.EntryPrice;
            double direction = position.Side == P2PPositionSide.Long ? 1d : -1d;
            return position.MarginAmount * position.Leverage * priceChangeRatio * direction;
        }

        public static double CalculateLiquidationPrice(P2PPositionSide side, double entryPrice, int leverage, double maintenanceMarginRate)
        {
            if (side == P2PPositionSide.None) throw new ArgumentOutOfRangeException(nameof(side));
            if (!P2PMatchRules.IsFinitePositive(entryPrice)) throw new ArgumentOutOfRangeException(nameof(entryPrice));
            if (leverage < 1) throw new ArgumentOutOfRangeException(nameof(leverage));

            double leverageDistance = 1d / leverage;
            return side == P2PPositionSide.Long
                ? entryPrice * (1d - leverageDistance + maintenanceMarginRate)
                : entryPrice * (1d + leverageDistance - maintenanceMarginRate);
        }

        public static double CalculateFee(double margin, int leverage, double feeRate) => margin * leverage * feeRate;

        public static bool HasReachedLiquidationPrice(P2PPositionState position, double marketPrice)
        {
            if (!position.IsOpen || !P2PMatchRules.IsFinitePositive(marketPrice)) return false;
            return position.Side == P2PPositionSide.Long
                ? marketPrice <= position.LiquidationPrice
                : marketPrice >= position.LiquidationPrice;
        }

        public static void OpenPosition(P2PPlayerRuntimeState player, P2PTradeRequest request, P2PMatchRules rules, double fillPrice)
        {
            double margin = player.CashBalance * request.MarginRatio;
            double fee = CalculateFee(margin, request.Leverage, rules.TradingFeeRate);
            P2PPositionSide side = request.Action == P2PTradeAction.OpenLong ? P2PPositionSide.Long : P2PPositionSide.Short;
            double liquidationPrice = CalculateLiquidationPrice(side, fillPrice, request.Leverage, rules.MaintenanceMarginRate);

            player.ChangeCash(-(margin + fee));
            player.Position.Open(side, fillPrice, margin, request.Leverage, liquidationPrice);
        }

        public static double ClosePosition(P2PPlayerRuntimeState player, P2PMatchRules rules, double fillPrice)
        {
            player.Position.MarkToMarket(fillPrice);
            double exitFee = CalculateFee(player.Position.MarginAmount, player.Position.Leverage, rules.TradingFeeRate);
            double returnedCash = Math.Max(0d, player.Position.MarginAmount + player.Position.UnrealizedPnL - exitFee);
            player.ChangeCash(returnedCash);
            player.Position.Clear();
            return returnedCash;
        }
    }
}
