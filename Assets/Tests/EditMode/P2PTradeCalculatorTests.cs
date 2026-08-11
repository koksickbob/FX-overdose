using NUnit.Framework;

namespace FXOverdose.P2P.Core.Tests
{
    public sealed class P2PTradeCalculatorTests
    {
        [Test]
        public void SamePriceMove_ProducesOppositeLongAndShortPnL()
        {
            var longPosition = OpenPosition(P2PPositionSide.Long, 100d, 1000d, 10);
            var shortPosition = OpenPosition(P2PPositionSide.Short, 100d, 1000d, 10);

            double longPnl = P2PTradeCalculator.CalculateUnrealizedPnL(longPosition, 110d);
            double shortPnl = P2PTradeCalculator.CalculateUnrealizedPnL(shortPosition, 110d);

            Assert.That(longPnl, Is.EqualTo(1000d).Within(0.0001d));
            Assert.That(shortPnl, Is.EqualTo(-1000d).Within(0.0001d));
        }

        [Test]
        public void OpenAndClosePosition_AppliesEntryAndExitFees()
        {
            var rules = new P2PMatchRules(maximumMarginRatio: 1d, tradingFeeRate: 0.0006d);
            var player = new P2PPlayerRuntimeState(1, "Player", 10000d);
            var request = new P2PTradeRequest(1, P2PTradeAction.OpenLong, leverage: 10, marginRatio: 0.5d);

            P2PTradeCalculator.OpenPosition(player, request, rules, 100d);
            double returned = P2PTradeCalculator.ClosePosition(player, rules, 100d);

            Assert.That(returned, Is.EqualTo(4970d).Within(0.0001d));
            Assert.That(player.CashBalance, Is.EqualTo(9940d).Within(0.0001d));
            Assert.That(player.Position.IsOpen, Is.False);
        }

        [Test]
        public void LiquidationPrice_MatchesExistingTradingFormula()
        {
            double longPrice = P2PTradeCalculator.CalculateLiquidationPrice(P2PPositionSide.Long, 100d, 10, 0.005d);
            double shortPrice = P2PTradeCalculator.CalculateLiquidationPrice(P2PPositionSide.Short, 100d, 10, 0.005d);

            Assert.That(longPrice, Is.EqualTo(90.5d).Within(0.0001d));
            Assert.That(shortPrice, Is.EqualTo(109.5d).Within(0.0001d));
        }

        private static P2PPositionState OpenPosition(P2PPositionSide side, double entry, double margin, int leverage)
        {
            var position = new P2PPositionState();
            position.Open(side, entry, margin, leverage, 0d);
            return position;
        }
    }
}
