using System;
using System.Collections.Generic;

namespace FXOverdose.P2P.Lobby
{
    /// <summary>공개 검색 로비 또는 Steam 초대로만 들어오는 친구 로비입니다.</summary>
    public enum SteamLobbyVisibility { Public, InviteOnly }
    public enum SteamLobbyState { Idle, Creating, Joining, InLobby, Searching, Failed }

    public readonly struct SteamLobbySettings
    {
        public SteamLobbySettings(int maximumPlayers, int maximumLeverage, double maximumMarginRatio)
        {
            MaximumPlayers = maximumPlayers;
            MaximumLeverage = maximumLeverage;
            MaximumMarginRatio = maximumMarginRatio;
        }

        public int MaximumPlayers { get; }
        public int MaximumLeverage { get; }
        public double MaximumMarginRatio { get; }
    }

    public readonly struct SteamLobbyMember
    {
        public SteamLobbyMember(ulong steamId, string personaName, bool isHost, bool isReady, int readyRevision)
        {
            SteamId = steamId;
            PersonaName = personaName ?? string.Empty;
            IsHost = isHost;
            IsReady = isReady;
            ReadyRevision = readyRevision;
        }

        public ulong SteamId { get; }
        public string PersonaName { get; }
        public bool IsHost { get; }
        public bool IsReady { get; }
        public int ReadyRevision { get; }
    }

    public sealed class SteamLobbySnapshot
    {
        public SteamLobbySnapshot(ulong lobbyId, ulong ownerSteamId, SteamLobbySettings settings,
            int rulesRevision, bool matchStarted, IReadOnlyList<SteamLobbyMember> members, string connectionNonce = "")
        {
            LobbyId = lobbyId;
            OwnerSteamId = ownerSteamId;
            Settings = settings;
            RulesRevision = rulesRevision;
            MatchStarted = matchStarted;
            Members = members ?? Array.Empty<SteamLobbyMember>();
            ConnectionNonce = connectionNonce ?? string.Empty;
        }

        public ulong LobbyId { get; }
        public ulong OwnerSteamId { get; }
        public SteamLobbySettings Settings { get; }
        public int RulesRevision { get; }
        public bool MatchStarted { get; }
        public IReadOnlyList<SteamLobbyMember> Members { get; }
        public string ConnectionNonce { get; }
    }

    public readonly struct SteamLobbySummary
    {
        public SteamLobbySummary(ulong lobbyId, int memberCount, int maximumPlayers, int maximumLeverage, double maximumMarginRatio)
        {
            LobbyId = lobbyId;
            MemberCount = memberCount;
            MaximumPlayers = maximumPlayers;
            MaximumLeverage = maximumLeverage;
            MaximumMarginRatio = maximumMarginRatio;
        }

        public ulong LobbyId { get; }
        public int MemberCount { get; }
        public int MaximumPlayers { get; }
        public int MaximumLeverage { get; }
        public double MaximumMarginRatio { get; }
    }

    public static class SteamLobbyRules
    {
        private static readonly int[] LeveragePresets = { 1, 2, 5, 10, 20, 50, 100 };
        private static readonly double[] MarginPresets = { 0.1, 0.25, 0.5, 0.75, 1.0 };

        public static bool IsValid(SteamLobbySettings settings)
        {
            return settings.MaximumPlayers >= 1 && settings.MaximumPlayers <= 4 &&
                   Contains(LeveragePresets, settings.MaximumLeverage) &&
                   Contains(MarginPresets, settings.MaximumMarginRatio);
        }

        public static bool IsEffectivelyReady(SteamLobbyMember member, int rulesRevision) =>
            member.IsHost || (member.IsReady && member.ReadyRevision == rulesRevision);

        public static bool CanStart(SteamLobbySnapshot lobby, ulong requesterSteamId)
        {
            if (lobby == null || lobby.MatchStarted || lobby.OwnerSteamId != requesterSteamId ||
                lobby.Members.Count < 1 || lobby.Members.Count > lobby.Settings.MaximumPlayers)
            {
                return false;
            }

            for (int i = 0; i < lobby.Members.Count; i++)
            {
                if (!IsEffectivelyReady(lobby.Members[i], lobby.RulesRevision)) return false;
            }
            return true;
        }

        private static bool Contains(int[] values, int target)
        {
            for (int i = 0; i < values.Length; i++) if (values[i] == target) return true;
            return false;
        }

        private static bool Contains(double[] values, double target)
        {
            for (int i = 0; i < values.Length; i++) if (Math.Abs(values[i] - target) < 0.000001) return true;
            return false;
        }
    }

    public static class SteamLobbyDataKeys
    {
        public const string Game = "game";
        public const string Mode = "mode";
        public const string Build = "build";
        public const string MaximumLeverage = "max_leverage";
        public const string MaximumMarginRatio = "max_margin";
        public const string RulesRevision = "rules_revision";
        public const string MatchStarted = "match_started";
        public const string MemberCount = "member_count";
        public const string ConnectionNonce = "connection_nonce";
        public const string Ready = "ready";
        public const string ReadyRevision = "ready_revision";
        public const string GameValue = "fx_overdose";
        public const string ModeValue = "p2p_challenge";
    }
}
