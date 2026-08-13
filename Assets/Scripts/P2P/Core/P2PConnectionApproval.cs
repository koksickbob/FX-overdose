using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FXOverdose.P2P.Connection
{
    public readonly struct P2PConnectionPayload
    {
        public P2PConnectionPayload(ulong lobbyId, ulong steamId, string buildVersion, string nonce)
        {
            LobbyId = lobbyId; SteamId = steamId;
            BuildVersion = buildVersion ?? string.Empty; Nonce = nonce ?? string.Empty;
        }
        public ulong LobbyId { get; }
        public ulong SteamId { get; }
        public string BuildVersion { get; }
        public string Nonce { get; }
    }

    public enum P2PConnectionRejectReason
    {
        None, MalformedPayload, WrongLobby, WrongBuild, WrongNonce,
        InvalidSteamId, NotLobbyMember, DuplicateSteamId, LobbyFull, MatchNotStarted
    }

    public readonly struct P2PConnectionApprovalResult
    {
        public P2PConnectionApprovalResult(bool approved, P2PConnectionRejectReason reason)
        { Approved = approved; Reason = reason; }
        public bool Approved { get; }
        public P2PConnectionRejectReason Reason { get; }
    }

    public static class P2PConnectionPayloadCodec
    {
        private const uint Magic = 0x46585032; // FXP2
        private const byte Version = 1;
        public static byte[] Encode(P2PConnectionPayload payload)
        {
            using var stream = new MemoryStream(128);
            using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(Magic); writer.Write(Version); writer.Write(payload.LobbyId); writer.Write(payload.SteamId);
            writer.Write(payload.BuildVersion); writer.Write(payload.Nonce);
            return stream.ToArray();
        }

        public static bool TryDecode(byte[] bytes, out P2PConnectionPayload payload)
        {
            payload = default;
            if (bytes == null || bytes.Length == 0 || bytes.Length > 512) return false;
            try
            {
                using var stream = new MemoryStream(bytes, false);
                using var reader = new BinaryReader(stream, Encoding.UTF8, true);
                if (reader.ReadUInt32() != Magic || reader.ReadByte() != Version) return false;
                var decoded = new P2PConnectionPayload(reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadString(), reader.ReadString());
                if (stream.Position != stream.Length || decoded.BuildVersion.Length > 64 || decoded.Nonce.Length > 64) return false;
                payload = decoded; return true;
            }
            catch (Exception) { return false; }
        }
    }

    public static class P2PConnectionApprovalValidator
    {
        public static P2PConnectionApprovalResult Validate(byte[] bytes, ulong lobbyId, string buildVersion,
            string nonce, bool matchStarted, IReadOnlyCollection<ulong> lobbyMembers,
            IReadOnlyCollection<ulong> connectedSteamIds, int maximumPlayers)
        {
            if (!P2PConnectionPayloadCodec.TryDecode(bytes, out var payload)) return Reject(P2PConnectionRejectReason.MalformedPayload);
            if (!matchStarted) return Reject(P2PConnectionRejectReason.MatchNotStarted);
            if (payload.LobbyId != lobbyId) return Reject(P2PConnectionRejectReason.WrongLobby);
            if (!string.Equals(payload.BuildVersion, buildVersion, StringComparison.Ordinal)) return Reject(P2PConnectionRejectReason.WrongBuild);
            if (!string.Equals(payload.Nonce, nonce, StringComparison.Ordinal) || string.IsNullOrEmpty(nonce)) return Reject(P2PConnectionRejectReason.WrongNonce);
            if (payload.SteamId == 0) return Reject(P2PConnectionRejectReason.InvalidSteamId);
            if (lobbyMembers == null || !lobbyMembers.Contains(payload.SteamId)) return Reject(P2PConnectionRejectReason.NotLobbyMember);
            if (connectedSteamIds != null && connectedSteamIds.Contains(payload.SteamId)) return Reject(P2PConnectionRejectReason.DuplicateSteamId);
            if (connectedSteamIds != null && connectedSteamIds.Count >= maximumPlayers) return Reject(P2PConnectionRejectReason.LobbyFull);
            return new P2PConnectionApprovalResult(true, P2PConnectionRejectReason.None);
        }
        private static P2PConnectionApprovalResult Reject(P2PConnectionRejectReason reason) => new P2PConnectionApprovalResult(false, reason);
    }
}
