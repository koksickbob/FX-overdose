using FXOverdose.P2P.Market;
using NUnit.Framework;

namespace FXOverdose.P2P.Core.Tests
{
    public sealed class P2PMarketSynchronizationTests
    {
        [Test]
        public void SameSeedAndElapsedTime_ProduceIdenticalMarket()
        {
            var first = new P2PMarketSimulationEngine(1234);
            var second = new P2PMarketSimulationEngine(1234);
            first.SetPaused(false); second.SetPaused(false);
            first.Advance(60); second.Advance(60);
            Assert.That(first.Snapshot.Sequence, Is.EqualTo(second.Snapshot.Sequence));
            Assert.That(first.Snapshot.Price, Is.EqualTo(second.Snapshot.Price));
            Assert.That(first.Snapshot.Checksum, Is.EqualTo(second.Snapshot.Checksum));
        }

        [Test]
        public void Market_StartsAtNineAndStopsAtTwentyFour()
        {
            var engine = new P2PMarketSimulationEngine(55);
            Assert.That(engine.Snapshot.Hour, Is.EqualTo(9));
            engine.SetPaused(false);
            engine.Advance(2000, 0.666);
            Assert.That(engine.Snapshot.Hour, Is.EqualTo(24));
            Assert.That(engine.Snapshot.Minute, Is.Zero);
            Assert.That(engine.Snapshot.IsFinished, Is.True);
        }

        [Test]
        public void PausedMarket_DoesNotAdvanceTimeOrPrice()
        {
            var engine = new P2PMarketSimulationEngine(9);
            var before = engine.Snapshot;
            engine.Advance(30);
            Assert.That(engine.Snapshot.Sequence, Is.EqualTo(before.Sequence));
            Assert.That(engine.Snapshot.Price, Is.EqualTo(before.Price));
        }

        [Test]
        public void Replica_DiscardsLateSnapshot()
        {
            var engine = new P2PMarketSimulationEngine(9);
            engine.SetPaused(false); var old = engine.Snapshot;
            engine.Advance(1); var latest = engine.Snapshot;
            var replica = new P2PMarketReplica();
            Assert.That(replica.TryApply(latest), Is.True);
            Assert.That(replica.TryApply(old), Is.False);
            Assert.That(replica.Snapshot.Sequence, Is.EqualTo(latest.Sequence));
        }

        [Test]
        public void SnapshotCodec_RoundTripsAndRejectsCorruption()
        {
            var engine = new P2PMarketSimulationEngine(99);
            engine.SetPaused(false); engine.Advance(3);
            byte[] bytes = P2PMarketSnapshotCodec.Encode(engine.Snapshot);
            Assert.That(P2PMarketSnapshotCodec.TryDecode(bytes, out var decoded), Is.True);
            Assert.That(decoded.Checksum, Is.EqualTo(engine.Snapshot.Checksum));
            bytes[12] ^= 0x7F;
            Assert.That(P2PMarketSnapshotCodec.TryDecode(bytes, out _), Is.False);
        }
    }
}
