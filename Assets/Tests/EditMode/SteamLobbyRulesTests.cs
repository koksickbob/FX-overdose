using System.Collections.Generic;
using FXOverdose.P2P.Lobby;
using NUnit.Framework;

namespace FXOverdose.P2P.Core.Tests
{
    public sealed class SteamLobbyRulesTests
    {
        [TestCase(1, 1, 0.1)]
        [TestCase(4, 100, 1.0)]
        public void Settings_AcceptsSupportedBoundaries(int players, int leverage, double margin)
        {
            Assert.That(SteamLobbyRules.IsValid(new SteamLobbySettings(players, leverage, margin)), Is.True);
        }

        [TestCase(0, 20, 0.5)]
        [TestCase(5, 20, 0.5)]
        [TestCase(4, 3, 0.5)]
        [TestCase(4, 20, 0.3)]
        public void Settings_RejectsUnsupportedValues(int players, int leverage, double margin)
        {
            Assert.That(SteamLobbyRules.IsValid(new SteamLobbySettings(players, leverage, margin)), Is.False);
        }

        [Test]
        public void RuleRevision_InvalidatesPreviousReadyState()
        {
            var member = new SteamLobbyMember(2, "Client", false, true, 3);
            Assert.That(SteamLobbyRules.IsEffectivelyReady(member, 3), Is.True);
            Assert.That(SteamLobbyRules.IsEffectivelyReady(member, 4), Is.False);
        }

        [Test]
        public void Host_IsReadyWithoutMemberReadyFlag()
        {
            var host = new SteamLobbyMember(1, "Host", true, false, 0);
            Assert.That(SteamLobbyRules.IsEffectivelyReady(host, 9), Is.True);
        }

        [Test]
        public void CanStart_RequiresOwnerAndAllClientsReadyForCurrentRevision()
        {
            var members = new List<SteamLobbyMember>
            {
                new SteamLobbyMember(1, "Host", true, false, 0),
                new SteamLobbyMember(2, "Client", false, true, 2)
            };
            var lobby = new SteamLobbySnapshot(10, 1, new SteamLobbySettings(4, 20, 0.5), 2, false, members);
            Assert.That(SteamLobbyRules.CanStart(lobby, 1), Is.True);
            Assert.That(SteamLobbyRules.CanStart(lobby, 2), Is.False);
        }

        [Test]
        public void CanStart_AllowsSoloHostForLocalMultiplayerVerification()
        {
            var members = new List<SteamLobbyMember> { new SteamLobbyMember(1, "Host", true, false, 0) };
            var lobby = new SteamLobbySnapshot(10, 1, new SteamLobbySettings(4, 20, 0.5), 1, false, members);
            Assert.That(SteamLobbyRules.CanStart(lobby, 1), Is.True);
        }

        [Test]
        public void CanStart_RejectsStartedLobbyAndStaleReadyClient()
        {
            var stale = new List<SteamLobbyMember>
            {
                new SteamLobbyMember(1, "Host", true, false, 0),
                new SteamLobbyMember(2, "Client", false, true, 1)
            };
            var lobby = new SteamLobbySnapshot(10, 1, new SteamLobbySettings(4, 20, 0.5), 2, false, stale);
            Assert.That(SteamLobbyRules.CanStart(lobby, 1), Is.False);
        }
    }
}
