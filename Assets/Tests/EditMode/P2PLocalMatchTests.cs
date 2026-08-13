using NUnit.Framework;

namespace FXOverdose.P2P.Core.Tests
{
    public sealed class P2PLocalMatchTests
    {
        [Test]
        public void FourPlayers_TradeIndependentlyOnOneMarket()
        {
            var match = new P2PLocalMatch(new P2PMatchRules(maximumPlayers: 4, maximumLeverage: 20));
            P2PPlayerRuntimeState player1 = match.AddPlayer(1, "P1");
            P2PPlayerRuntimeState player2 = match.AddPlayer(2, "P2");
            P2PPlayerRuntimeState player3 = match.AddPlayer(3, "P3");
            P2PPlayerRuntimeState player4 = match.AddPlayer(4, "P4");
            match.Start(100d);

            Assert.That(match.SubmitTrade(1, new P2PTradeRequest(1, P2PTradeAction.OpenLong, 10, 0.25d)).IsAccepted, Is.True);
            Assert.That(match.SubmitTrade(2, new P2PTradeRequest(1, P2PTradeAction.OpenShort, 10, 0.25d)).IsAccepted, Is.True);
            Assert.That(match.SubmitTrade(3, new P2PTradeRequest(1, P2PTradeAction.OpenLong, 5, 0.25d)).IsAccepted, Is.True);
            Assert.That(match.SubmitTrade(4, new P2PTradeRequest(1, P2PTradeAction.OpenShort, 5, 0.25d)).IsAccepted, Is.True);

            match.UpdateMarketPrice(105d, 10);

            Assert.That(player1.Position.UnrealizedPnL, Is.GreaterThan(0d));
            Assert.That(player2.Position.UnrealizedPnL, Is.LessThan(0d));
            Assert.That(player3.Position.UnrealizedPnL, Is.GreaterThan(0d));
            Assert.That(player4.Position.UnrealizedPnL, Is.LessThan(0d));
            Assert.That(match.Players.Count, Is.EqualTo(4));
        }

        [Test]
        public void RequestId_IsDeduplicatedPerPlayer()
        {
            var match = new P2PLocalMatch(new P2PMatchRules(maximumPlayers: 2));
            match.AddPlayer(1, "P1");
            match.AddPlayer(2, "P2");
            match.Start(100d);
            var request = new P2PTradeRequest(9, P2PTradeAction.OpenLong, 10, 0.25d);

            Assert.That(match.SubmitTrade(1, request).IsAccepted, Is.True);
            Assert.That(match.SubmitTrade(1, request).RejectReason, Is.EqualTo(P2PTradeRejectReason.DuplicateRequest));
            Assert.That(match.SubmitTrade(2, request).IsAccepted, Is.True);
        }

        [Test]
        public void RequestId_WrapAround_AcceptsNewSequenceAndRejectsOldPacket()
        {
            var match=new P2PLocalMatch(new P2PMatchRules());
            match.AddPlayer(1,"P1");match.Start(100d);
            Assert.That(match.SubmitTrade(1,new P2PTradeRequest(uint.MaxValue,P2PTradeAction.OpenLong,2,.25d)).IsAccepted,Is.True);
            Assert.That(match.SubmitTrade(1,new P2PTradeRequest(1,P2PTradeAction.ClosePosition)).IsAccepted,Is.True);
            Assert.That(match.SubmitTrade(1,new P2PTradeRequest(uint.MaxValue,P2PTradeAction.OpenLong,2,.25d)).RejectReason,
                Is.EqualTo(P2PTradeRejectReason.DuplicateRequest));
        }

        [Test]
        public void ReconnectGrace_BlocksOrdersAndRestoresExistingState()
        {
            var match=new P2PLocalMatch(new P2PMatchRules());
            var player=match.AddPlayer(1,"P1");match.Start(100d);
            Assert.That(match.SetPlayerConnected(1,false),Is.True);
            Assert.That(match.SubmitTrade(1,new P2PTradeRequest(1,P2PTradeAction.OpenLong,2,.25d)).RejectReason,
                Is.EqualTo(P2PTradeRejectReason.PlayerDisconnected));
            Assert.That(match.SetPlayerConnected(1,true),Is.True);
            Assert.That(player.IsEliminated,Is.False);
            Assert.That(match.SubmitTrade(1,new P2PTradeRequest(1,P2PTradeAction.OpenLong,2,.25d)).IsAccepted,Is.True);
        }

        [Test]
        public void Liquidation_WithCatastrophicLoss_DepletesMentalAndEliminatesPlayer()
        {
            var rules = new P2PMatchRules(maximumMarginRatio: 0.99d, tradingFeeRate: 0d);
            var match = new P2PLocalMatch(rules);
            P2PPlayerRuntimeState player = match.AddPlayer(1, "P1");
            match.Start(100d);
            match.SubmitTrade(1, new P2PTradeRequest(1, P2PTradeAction.OpenLong, 10, 0.99d));

            // 현금이 조금 남아도 대규모 실현 손실로 멘탈이 0이면 멘탈 탈락입니다.
            match.UpdateMarketPrice(90.5d, 10);

            Assert.That(player.Position.IsOpen, Is.False);
            Assert.That(player.IsEliminated, Is.True);
            Assert.That(player.EliminationReason, Is.EqualTo(P2PEliminationReason.MentalDepleted));
            Assert.That(player.CashBalance, Is.GreaterThan(0d));
        }

        [Test]
        public void ConsecutiveRealizedLosses_ImmediatelyReduceAuthoritativeMental()
        {
            var match=new P2PLocalMatch(new P2PMatchRules(tradingFeeRate:0d));
            var player=match.AddPlayer(1,"P1");match.Start(100d);
            match.SubmitTrade(1,new P2PTradeRequest(1,P2PTradeAction.OpenLong,1,.5d));
            match.UpdateMarketPrice(90d,1);match.SubmitTrade(1,new P2PTradeRequest(2,P2PTradeAction.ClosePosition));
            double afterFirst=player.Mental;
            match.SubmitTrade(1,new P2PTradeRequest(3,P2PTradeAction.OpenLong,1,.5d));
            match.UpdateMarketPrice(81d,2);match.SubmitTrade(1,new P2PTradeRequest(4,P2PTradeAction.ClosePosition));
            Assert.That(afterFirst,Is.LessThan(100d));
            Assert.That(player.Mental,Is.LessThan(afterFirst));
        }

        [Test]
        public void ManualMentalRules_MatchOriginalEntryAndLossStreakPenalties()
        {
            var player=new P2PPlayerRuntimeState(1,"P1",7000d);
            player.RecordPositionOpened();
            Assert.That(player.Mental,Is.EqualTo(90d));

            player.RecordTradeResult(-1d);
            Assert.That(player.LosingStreak,Is.EqualTo(1));
            Assert.That(player.Mental,Is.EqualTo(82.5d));

            player.RecordTradeResult(-1d);
            Assert.That(player.LosingStreak,Is.EqualTo(2));
            Assert.That(player.Mental,Is.EqualTo(64.5d));

            player.RecordTradeResult(1d);
            Assert.That(player.LosingStreak,Is.Zero);
        }
    }
}
