using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FXOverdose.P2P.Core
{
    public readonly struct P2PPlayerTradeSnapshot
    {
        public P2PPlayerTradeSnapshot(ulong playerId, string name, double cash, double equity, double returnRate,
            P2PPositionSide side, double entryPrice, double margin, int leverage, double unrealizedPnl, int rank)
        {
            PlayerId = playerId; Name = name ?? string.Empty; Cash = cash; Equity = equity; ReturnRate = returnRate;
            Side = side; EntryPrice = entryPrice; Margin = margin; Leverage = leverage; UnrealizedPnL = unrealizedPnl; Rank = rank;
        }
        public ulong PlayerId { get; }
        public string Name { get; }
        public double Cash { get; }
        public double Equity { get; }
        public double ReturnRate { get; }
        public P2PPositionSide Side { get; }
        public double EntryPrice { get; }
        public double Margin { get; }
        public int Leverage { get; }
        public double UnrealizedPnL { get; }
        public int Rank { get; }
    }

    public static class P2PNetworkTradingCodec
    {
        public static byte[] EncodeRequest(ulong steamId, P2PTradeRequest request)
        {
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
            writer.Write(steamId); writer.Write(request.RequestId); writer.Write((byte)request.Action);
            writer.Write(request.Leverage); writer.Write(request.MarginRatio); return stream.ToArray();
        }
        public static bool TryDecodeRequest(byte[] bytes, out ulong steamId, out P2PTradeRequest request)
        {
            steamId = 0; request = default;
            try { using var stream = new MemoryStream(bytes, false); using var reader = new BinaryReader(stream);
                steamId = reader.ReadUInt64(); uint id = reader.ReadUInt32(); var action = (P2PTradeAction)reader.ReadByte();
                int leverage = reader.ReadInt32(); double margin = reader.ReadDouble();
                if (stream.Position != stream.Length || !Enum.IsDefined(typeof(P2PTradeAction), action)) return false;
                request = new P2PTradeRequest(id, action, leverage, margin); return true; }
            catch { return false; }
        }
        public static byte[] EncodeState(ulong sequence,P2PTradeResult result, IReadOnlyList<P2PPlayerRuntimeState> ranked)
        {
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.UTF8);
            writer.Write(sequence);writer.Write(result.RequestId); writer.Write((byte)result.RejectReason); writer.Write(result.FillPrice); writer.Write(ranked.Count);
            for (int i = 0; i < ranked.Count; i++) { var p = ranked[i]; writer.Write(p.PlayerId); writer.Write(p.DisplayName);
                writer.Write(p.CashBalance); writer.Write(p.TotalEquity); writer.Write(p.ReturnRate); writer.Write((byte)p.Position.Side);
                writer.Write(p.Position.EntryPrice); writer.Write(p.Position.MarginAmount); writer.Write(p.Position.Leverage); writer.Write(p.Position.UnrealizedPnL); }
            return stream.ToArray();
        }
        public static bool TryDecodeState(byte[] bytes,out ulong sequence, out P2PTradeResult result, out IReadOnlyList<P2PPlayerTradeSnapshot> players)
        {
            sequence=0;result = default; players = Array.Empty<P2PPlayerTradeSnapshot>();
            try { using var stream = new MemoryStream(bytes, false); using var reader = new BinaryReader(stream, Encoding.UTF8);
                sequence=reader.ReadUInt64();result = new P2PTradeResult(reader.ReadUInt32(), (P2PTradeRejectReason)reader.ReadByte(), reader.ReadDouble());
                int count = reader.ReadInt32(); if (count < 0 || count > 4) return false; var list = new List<P2PPlayerTradeSnapshot>(count);
                for (int i = 0; i < count; i++) list.Add(new P2PPlayerTradeSnapshot(reader.ReadUInt64(), reader.ReadString(), reader.ReadDouble(),
                    reader.ReadDouble(), reader.ReadDouble(), (P2PPositionSide)reader.ReadByte(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadInt32(), reader.ReadDouble(), i + 1));
                if (stream.Position != stream.Length) return false; players = list; return true; }
            catch { return false; }
        }
    }
}
