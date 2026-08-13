using NUnit.Framework;

namespace FXOverdose.P2P.Core.Tests
{
    public sealed class P2PTradeValidatorTests
    {
        private P2PMatchRules rules;
        private P2PPlayerRuntimeState player;

        [SetUp]
        public void SetUp()
        {
            rules = new P2PMatchRules(maximumLeverage: 20, maximumMarginRatio: 0.5d, minimumOrderAmount: 10d);
            player = new P2PPlayerRuntimeState(1, "Player", 7000d);
        }

        [TestCase(21, 0.5d, P2PTradeRejectReason.InvalidLeverage)]
        [TestCase(20, 0.51d, P2PTradeRejectReason.InvalidMargin)]
        [TestCase(0, 0.5d, P2PTradeRejectReason.InvalidLeverage)]
        [TestCase(10, 0d, P2PTradeRejectReason.InvalidMargin)]
        public void OpenOrder_RejectsRuleLimitViolations(int leverage, double marginRatio, P2PTradeRejectReason expected)
        {
            var request = new P2PTradeRequest(1, P2PTradeAction.OpenLong, leverage, marginRatio);

            P2PTradeRejectReason result = Validate(request);

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void OpenOrder_AcceptsMaximumConfiguredValues()
        {
            var request = new P2PTradeRequest(1, P2PTradeAction.OpenShort, 20, 0.5d);

            Assert.That(Validate(request), Is.EqualTo(P2PTradeRejectReason.None));
        }

        [Test]
        public void DuplicateRequest_IsRejectedBeforeStateMutation()
        {
            var request = new P2PTradeRequest(7, P2PTradeAction.OpenLong, 10, 0.25d);

            P2PTradeRejectReason result = P2PTradeValidator.Validate(
                request, player, rules, P2PMatchPhase.Playing, false, true, 100d);

            Assert.That(result, Is.EqualTo(P2PTradeRejectReason.DuplicateRequest));
            Assert.That(player.CashBalance, Is.EqualTo(7000d));
            Assert.That(player.Position.IsOpen, Is.False);
        }

        private P2PTradeRejectReason Validate(P2PTradeRequest request)
        {
            return P2PTradeValidator.Validate(
                request, player, rules, P2PMatchPhase.Playing, false, false, 100d);
        }
    }
}
