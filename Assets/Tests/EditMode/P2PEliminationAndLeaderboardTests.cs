using System.Collections.Generic;
using NUnit.Framework;

namespace FXOverdose.P2P.Core.Tests
{
    public sealed class P2PEliminationAndLeaderboardTests
    {
        [Test]
        public void ZeroCashWithMargin_RemainsAlive()
        {
            var player = new P2PPlayerRuntimeState(1, "Player", 1000d);
            player.Position.Open(P2PPositionSide.Long, 100d, 1000d, 1, 0d);
            player.ChangeCash(-1000d);

            Assert.That(player.TotalEquity, Is.EqualTo(1000d));
            Assert.That(P2PEliminationEvaluator.Evaluate(player), Is.EqualTo(P2PEliminationReason.None));
        }

        [Test]
        public void ZeroTotalEquity_EliminatesForBankruptcyOnce()
        {
            var player = new P2PPlayerRuntimeState(1, "Player", 1000d);
            player.ChangeCash(-1000d);

            Assert.That(P2PEliminationEvaluator.EvaluateAndApply(player, 50), Is.True);
            Assert.That(P2PEliminationEvaluator.EvaluateAndApply(player, 51), Is.False);
            Assert.That(player.EliminationReason, Is.EqualTo(P2PEliminationReason.Bankruptcy));
            Assert.That(player.EliminationTick, Is.EqualTo(50));
        }

        [Test]
        public void ZeroMental_EliminatesImmediately()
        {
            var player = new P2PPlayerRuntimeState(1, "Player", 1000d, mental: 1d);
            player.ChangeMental(-1d);

            Assert.That(P2PEliminationEvaluator.EvaluateAndApply(player, 10), Is.True);
            Assert.That(player.EliminationReason, Is.EqualTo(P2PEliminationReason.MentalDepleted));
        }

        [Test]
        public void HealthAndMental_AreClampedBetweenZeroAndOneHundred()
        {
            var player = new P2PPlayerRuntimeState(1, "Player", 1000d, health: 90d, mental: 95d);

            player.ChangeHealth(50d);
            player.ChangeMental(50d);
            Assert.That(player.Health, Is.EqualTo(100d));
            Assert.That(player.Mental, Is.EqualTo(100d));

            player.ChangeHealth(-200d);
            player.ChangeMental(-200d);
            Assert.That(player.Health, Is.EqualTo(0d));
            Assert.That(player.Mental, Is.EqualTo(0d));
        }

        [Test]
        public void FourPlayers_AreRankedDeterministicallyIncludingSameTickEliminations()
        {
            var leader = new P2PPlayerRuntimeState(4, "Leader", 1500d);
            var survivor = new P2PPlayerRuntimeState(3, "Survivor", 1200d);
            var eliminatedHighEquity = new P2PPlayerRuntimeState(2, "Eliminated B", 900d);
            var eliminatedLowEquity = new P2PPlayerRuntimeState(1, "Eliminated A", 800d);
            eliminatedHighEquity.TryEliminate(P2PEliminationReason.MentalDepleted, 100);
            eliminatedLowEquity.TryEliminate(P2PEliminationReason.Bankruptcy, 100);

            IReadOnlyList<P2PPlayerRuntimeState> ranking = MultiplayerLeaderboard.Rank(new[]
            {
                eliminatedLowEquity, survivor, eliminatedHighEquity, leader
            });

            Assert.That(ranking[0], Is.SameAs(leader));
            Assert.That(ranking[1], Is.SameAs(survivor));
            Assert.That(ranking[2], Is.SameAs(eliminatedHighEquity));
            Assert.That(ranking[3], Is.SameAs(eliminatedLowEquity));
        }
    }
}
