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
                StateSequence=42, EventActive=true, EventId=2, EventKey="EVENT_01", EventTitle="stress",
                EventDescription="original article", EventMonologue="yomi line",
                Choice1Title="safe", Choice1Description="safe result", Choice1Type=0,
                Choice2Title="risk", Choice2Description="risk result", Choice2Type=1,
                EventSecondsLeft=12.5f,
                Finished=true, LastMessage="done",
                Players=new[]{new P2PCompetitionPlayerSnapshot(77,45,0,true,P2PEliminationReason.MentalDepleted,0,1,2,3)}
            };
            byte[] bytes=P2PCompetitionCodec.EncodeState(source);
            Assert.That(P2PCompetitionCodec.TryDecodeState(bytes,77,out var decoded),Is.True);
            Assert.That(decoded.Finished,Is.True); Assert.That(decoded.Players[0].Mental,Is.Zero);
            Assert.That(decoded.Players[0].Supplement,Is.EqualTo(3));
            Assert.That(decoded.EventKey,Is.EqualTo("EVENT_01"));
            Assert.That(decoded.EventDescription,Is.EqualTo("original article"));
            Assert.That(decoded.Choice2Title,Is.EqualTo("risk"));
            Assert.That(decoded.StateSequence,Is.EqualTo(42));
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

        [Test]
        public void ChoiceRules_RejectItemOptionAndTimeoutUsesOnlyOptionsOneOrTwo()
        {
            Assert.That(P2PChoiceRules.IsValid(0),Is.True);
            Assert.That(P2PChoiceRules.IsValid(1),Is.True);
            Assert.That(P2PChoiceRules.IsValid(2),Is.False);
            for(ulong player=1;player<=8;player++)Assert.That(P2PChoiceRules.GetTimeoutChoice(player,3),Is.InRange(0,1));
        }
    }
}
