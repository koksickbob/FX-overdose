using FXOverdose.P2P.Core;
using NUnit.Framework;

namespace FXOverdose.P2P.Core.Tests
{
    public sealed class P2PNetworkTradingCodecTests
    {
        [Test]
        public void Request_RoundTripsWithoutClientOwnedAssetValues()
        {
            byte[] bytes = P2PNetworkTradingCodec.EncodeRequest(77, new P2PTradeRequest(3, P2PTradeAction.OpenLong, 20, 0.5));
            Assert.That(P2PNetworkTradingCodec.TryDecodeRequest(bytes, out ulong steamId, out var request), Is.True);
            Assert.That(steamId, Is.EqualTo(77)); Assert.That(request.RequestId, Is.EqualTo(3));
            Assert.That(request.Leverage, Is.EqualTo(20)); Assert.That(request.MarginRatio, Is.EqualTo(0.5));
        }

        [Test]
        public void State_RoundTripsAuthoritativeRanking()
        {
            var match = new P2PLocalMatch(new P2PMatchRules());
            match.AddPlayer(1, "A"); match.AddPlayer(2, "B"); match.Start(100);
            var result = match.SubmitTrade(1, new P2PTradeRequest(1, P2PTradeAction.OpenLong, 10, 0.5));
            byte[] bytes = P2PNetworkTradingCodec.EncodeState(result, match.GetLeaderboard());
            Assert.That(P2PNetworkTradingCodec.TryDecodeState(bytes, out var decoded, out var players), Is.True);
            Assert.That(decoded.IsAccepted, Is.True); Assert.That(players.Count, Is.EqualTo(2));
            Assert.That(players[0].Rank, Is.EqualTo(1));
        }
    }
}
