using FXOverdose.P2P.Core;
using NUnit.Framework;

namespace FXOverdose.P2P.Core.Tests
{
    public sealed class P2PRequestSecurityGuardTests
    {
        [Test]
        public void BurstRequests_AreRateLimitedAndEventuallyBlocked()
        {
            var guard=new P2PRequestSecurityGuard(2,3);
            Assert.That(guard.TryConsume(1,0),Is.EqualTo(P2PRequestGuardResult.Allowed));
            Assert.That(guard.TryConsume(1,.1),Is.EqualTo(P2PRequestGuardResult.Allowed));
            Assert.That(guard.TryConsume(1,.2),Is.EqualTo(P2PRequestGuardResult.RateLimited));
            Assert.That(guard.TryConsume(1,.3),Is.EqualTo(P2PRequestGuardResult.RateLimited));
            Assert.That(guard.TryConsume(1,.4),Is.EqualTo(P2PRequestGuardResult.Blocked));
        }

        [Test]
        public void RateWindow_ResetsButSecurityViolationsRemain()
        {
            var guard=new P2PRequestSecurityGuard(1,3);
            Assert.That(guard.TryConsume(7,0),Is.EqualTo(P2PRequestGuardResult.Allowed));
            Assert.That(guard.TryConsume(7,.1),Is.EqualTo(P2PRequestGuardResult.RateLimited));
            Assert.That(guard.TryConsume(7,1.1),Is.EqualTo(P2PRequestGuardResult.Allowed));
            Assert.That(guard.RecordViolation(7,1.2),Is.EqualTo(P2PRequestGuardResult.RateLimited));
            Assert.That(guard.RecordViolation(7,1.3),Is.EqualTo(P2PRequestGuardResult.Blocked));
        }
    }
}
