using FXOverdose.P2P.Core;
using NUnit.Framework;

namespace FXOverdose.P2P.Tests
{
    public sealed class P2PCompetitionTests
    {
        [Test]
        public void CompetitionState_RoundTripsVitalsInventoryAndResult()
        {
            var source = new P2PCompetitionSnapshot
            {
                EventActive=true, EventId=2, EventTitle="stress", EventSecondsLeft=12.5f,
                Finished=true, LastMessage="done",
                Players=new[]{new P2PCompetitionPlayerSnapshot(77,45,0,true,P2PEliminationReason.MentalDepleted,0,1,2,3)}
            };
            byte[] bytes=P2PCompetitionCodec.EncodeState(source);
            Assert.That(P2PCompetitionCodec.TryDecodeState(bytes,77,out var decoded),Is.True);
            Assert.That(decoded.Finished,Is.True); Assert.That(decoded.Players[0].Mental,Is.Zero);
            Assert.That(decoded.Players[0].Supplement,Is.EqualTo(3));
        }

        [Test]
        public void Finish_ClosesOpenPositionAndRejectsFurtherTrading()
        {
            var match=new P2PLocalMatch(new P2PMatchRules());var p=match.AddPlayer(1,"host");match.Start(100);
            Assert.That(match.SubmitTrade(1,new P2PTradeRequest(1,P2PTradeAction.OpenLong,2,.5)).IsAccepted,Is.True);
            match.Finish();
            Assert.That(p.Position.IsOpen,Is.False);Assert.That(match.Phase,Is.EqualTo(P2PMatchPhase.Finished));
            Assert.That(match.SubmitTrade(1,new P2PTradeRequest(2,P2PTradeAction.OpenLong,2,.5)).RejectReason,Is.EqualTo(P2PTradeRejectReason.MatchNotPlaying));
        }
    }
}
