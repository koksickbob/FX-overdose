using System.Collections.Generic;
using FXOverdose.P2P.Connection;
using NUnit.Framework;

namespace FXOverdose.P2P.Core.Tests
{
    public sealed class P2PConnectionApprovalTests
    {
        private const ulong LobbyId = 100;
        private const ulong SteamId = 76561198000000001;
        private static readonly HashSet<ulong> Members = new HashSet<ulong> { SteamId };

        [Test]
        public void Payload_RoundTripsAllIdentityFields()
        {
            var source = new P2PConnectionPayload(LobbyId, SteamId, "1.2.3", "nonce");
            Assert.That(P2PConnectionPayloadCodec.TryDecode(P2PConnectionPayloadCodec.Encode(source), out var decoded), Is.True);
            Assert.That(decoded.LobbyId, Is.EqualTo(LobbyId));
            Assert.That(decoded.SteamId, Is.EqualTo(SteamId));
            Assert.That(decoded.BuildVersion, Is.EqualTo("1.2.3"));
            Assert.That(decoded.Nonce, Is.EqualTo("nonce"));
        }

        [Test]
        public void Payload_RejectsMalformedBytes()
        {
            Assert.That(P2PConnectionPayloadCodec.TryDecode(null, out _), Is.False);
            Assert.That(P2PConnectionPayloadCodec.TryDecode(new byte[] { 1, 2, 3 }, out _), Is.False);
        }

        [Test]
        public void Validator_ApprovesCurrentLobbyMember()
        {
            Assert.That(Validate(Payload()).Approved, Is.True);
        }

        [Test]
        public void Validator_RejectsWrongLobbyBuildAndNonce()
        {
            Assert.That(Validate(Payload(lobbyId: 101)).Reason, Is.EqualTo(P2PConnectionRejectReason.WrongLobby));
            Assert.That(Validate(Payload(build: "old")).Reason, Is.EqualTo(P2PConnectionRejectReason.WrongBuild));
            Assert.That(Validate(Payload(nonce: "wrong")).Reason, Is.EqualTo(P2PConnectionRejectReason.WrongNonce));
        }

        [Test]
        public void Validator_RejectsNonMemberAndDuplicate()
        {
            Assert.That(Validate(Payload(steamId: 999)).Reason, Is.EqualTo(P2PConnectionRejectReason.NotLobbyMember));
            Assert.That(Validate(Payload(), new HashSet<ulong> { SteamId }).Reason, Is.EqualTo(P2PConnectionRejectReason.DuplicateSteamId));
        }

        [Test]
        public void Validator_RejectsBeforeMatchStart()
        {
            var result = P2PConnectionApprovalValidator.Validate(Payload(), LobbyId, "1.2.3", "nonce", false,
                Members, new HashSet<ulong>(), 4);
            Assert.That(result.Reason, Is.EqualTo(P2PConnectionRejectReason.MatchNotStarted));
        }

        private static byte[] Payload(ulong lobbyId = LobbyId, ulong steamId = SteamId, string build = "1.2.3", string nonce = "nonce") =>
            P2PConnectionPayloadCodec.Encode(new P2PConnectionPayload(lobbyId, steamId, build, nonce));

        private static P2PConnectionApprovalResult Validate(byte[] payload, IReadOnlyCollection<ulong> connected = null) =>
            P2PConnectionApprovalValidator.Validate(payload, LobbyId, "1.2.3", "nonce", true,
                Members, connected ?? new HashSet<ulong>(), 4);
    }
}
