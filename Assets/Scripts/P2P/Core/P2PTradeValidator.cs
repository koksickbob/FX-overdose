namespace FXOverdose.P2P.Core
{
    /// <summary>호스트가 권위 상태를 변경하기 전에 주문을 검증합니다.</summary>
    public static class P2PTradeValidator
    {
        public static P2PTradeRejectReason Validate(
            P2PTradeRequest request,
            P2PPlayerRuntimeState player,
            P2PMatchRules rules,
            P2PMatchPhase matchPhase,
            bool isChoiceEventActive,
            bool isDuplicateRequest,
            double marketPrice)
        {
            if (matchPhase != P2PMatchPhase.Playing) return P2PTradeRejectReason.MatchNotPlaying;
            if (!player.IsConnected) return P2PTradeRejectReason.PlayerDisconnected;
            if (player.IsEliminated) return P2PTradeRejectReason.PlayerEliminated;
            if (isChoiceEventActive) return P2PTradeRejectReason.ChoiceEventActive;
            if (isDuplicateRequest) return P2PTradeRejectReason.DuplicateRequest;
            if (!P2PMatchRules.IsFinitePositive(marketPrice)) return P2PTradeRejectReason.InvalidMarketPrice;

            bool isOpenRequest = request.Action == P2PTradeAction.OpenLong || request.Action == P2PTradeAction.OpenShort;
            if (isOpenRequest && player.Position.IsOpen) return P2PTradeRejectReason.PositionConflict;
            if (request.Action == P2PTradeAction.ClosePosition)
                return player.Position.IsOpen ? P2PTradeRejectReason.None : P2PTradeRejectReason.PositionConflict;

            if (request.Leverage < 1 || request.Leverage > rules.MaximumLeverage)
                return P2PTradeRejectReason.InvalidLeverage;
            if (!P2PMatchRules.IsFinitePositive(request.MarginRatio) || request.MarginRatio > rules.MaximumMarginRatio)
                return P2PTradeRejectReason.InvalidMargin;

            double margin = player.CashBalance * request.MarginRatio;
            double entryFee = P2PTradeCalculator.CalculateFee(margin, request.Leverage, rules.TradingFeeRate);
            if (margin < rules.MinimumOrderAmount || margin + entryFee > player.CashBalance)
                return P2PTradeRejectReason.InsufficientBalance;

            return P2PTradeRejectReason.None;
        }
    }
}
