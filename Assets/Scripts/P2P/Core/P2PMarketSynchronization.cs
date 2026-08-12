using System;
using System.IO;
using System.Text;

namespace FXOverdose.P2P.Market
{
    public readonly struct P2PMarketSnapshot
    {
        public P2PMarketSnapshot(ulong sequence, int seed, double price, double bid, double ask,
            int totalMinutes, bool paused, double open, double high, double low, double volume)
        {
            Sequence = sequence; Seed = seed; Price = price; Bid = bid; Ask = ask;
            TotalMinutes = totalMinutes; Paused = paused; Open = open; High = high;
            Low = low; Volume = volume;
        }
        public ulong Sequence { get; }
        public int Seed { get; }
        public double Price { get; }
        public double Bid { get; }
        public double Ask { get; }
        public int TotalMinutes { get; }
        public bool Paused { get; }
        public double Open { get; }
        public double High { get; }
        public double Low { get; }
        public double Volume { get; }
        public int Hour => TotalMinutes / 60;
        public int Minute => TotalMinutes % 60;
        public bool IsFinished => TotalMinutes >= 24 * 60;
        public uint Checksum => P2PMarketChecksum.Calculate(this);
    }

    /// <summary>호스트에서만 실행하는 결정적 가격 및 09:00~24:00 시간 엔진입니다.</summary>
    public sealed class P2PMarketSimulationEngine
    {
        private uint randomState;
        private double minuteAccumulator;
        private double tickAccumulator;

        public P2PMarketSimulationEngine(int seed, double initialPrice = 67842.1)
        {
            if (initialPrice <= 0 || double.IsNaN(initialPrice) || double.IsInfinity(initialPrice))
                throw new ArgumentOutOfRangeException(nameof(initialPrice));
            Seed = seed; randomState = unchecked((uint)seed) | 1u;
            Snapshot = new P2PMarketSnapshot(0, seed, initialPrice, initialPrice, initialPrice,
                9 * 60, true, initialPrice, initialPrice, initialPrice, 0);
        }

        public int Seed { get; }
        public P2PMarketSnapshot Snapshot { get; private set; }

        public void SetPaused(bool paused) => Apply(Snapshot.Price, Snapshot.TotalMinutes, paused, 0);

        public void Advance(double realSeconds, double secondsPerGameMinute = 0.666)
        {
            if (realSeconds <= 0 || secondsPerGameMinute <= 0 || Snapshot.Paused || Snapshot.IsFinished) return;
            minuteAccumulator += realSeconds;
            tickAccumulator += realSeconds;
            double tickInterval = secondsPerGameMinute / 5d;
            while (tickAccumulator >= tickInterval && !Snapshot.IsFinished)
            {
                tickAccumulator -= tickInterval;
                bool advanceMinute = minuteAccumulator >= secondsPerGameMinute;
                if (advanceMinute) minuteAccumulator -= secondsPerGameMinute;
                // 기존 시장처럼 한 게임 분을 다섯 개의 작은 틱으로 구성합니다.
                double noise = (NextUnit() - 0.5) * 0.00108;
                double meanReversion = (67842.1 - Snapshot.Price) / 67842.1 * 0.00012;
                double next = Math.Max(10, Snapshot.Price * (1 + noise + meanReversion));
                Apply(next, Snapshot.TotalMinutes + (advanceMinute ? 1 : 0), false, Math.Abs(noise) * 200);
            }
        }

        private void Apply(double price, int totalMinutes, bool paused, double volume)
        {
            bool newMinute = totalMinutes != Snapshot.TotalMinutes;
            double open = newMinute ? Snapshot.Price : Snapshot.Open;
            double high = newMinute ? Math.Max(open, price) : Math.Max(Snapshot.High, price);
            double low = newMinute ? Math.Min(open, price) : Math.Min(Snapshot.Low, price);
            double spread = Math.Max(0.01, price * 0.0002);
            Snapshot = new P2PMarketSnapshot(Snapshot.Sequence + 1, Seed, price,
                price - spread / 2, price + spread / 2, Math.Min(totalMinutes, 24 * 60),
                paused, open, high, low, newMinute ? volume : Snapshot.Volume + volume);
        }

        private double NextUnit()
        {
            randomState ^= randomState << 13; randomState ^= randomState >> 17; randomState ^= randomState << 5;
            return randomState / (double)uint.MaxValue;
        }
    }

    public sealed class P2PMarketReplica
    {
        public P2PMarketSnapshot Snapshot { get; private set; }
        public bool TryApply(P2PMarketSnapshot snapshot)
        {
            if (snapshot.Sequence <= Snapshot.Sequence || snapshot.Checksum != P2PMarketChecksum.Calculate(snapshot)) return false;
            Snapshot = snapshot; return true;
        }
    }

    public static class P2PMarketSnapshotCodec
    {
        private const uint Magic = 0x4D4B5435;
        public static byte[] Encode(P2PMarketSnapshot value)
        {
            using var stream = new MemoryStream(128); using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(Magic); writer.Write(value.Sequence); writer.Write(value.Seed); writer.Write(value.Price);
            writer.Write(value.Bid); writer.Write(value.Ask); writer.Write(value.TotalMinutes); writer.Write(value.Paused);
            writer.Write(value.Open); writer.Write(value.High); writer.Write(value.Low); writer.Write(value.Volume); writer.Write(value.Checksum);
            return stream.ToArray();
        }
        public static bool TryDecode(byte[] bytes, out P2PMarketSnapshot value)
        {
            value = default;
            if (bytes == null || bytes.Length > 256) return false;
            try
            {
                using var stream = new MemoryStream(bytes, false); using var reader = new BinaryReader(stream, Encoding.UTF8, true);
                if (reader.ReadUInt32() != Magic) return false;
                var result = new P2PMarketSnapshot(reader.ReadUInt64(), reader.ReadInt32(), reader.ReadDouble(), reader.ReadDouble(),
                    reader.ReadDouble(), reader.ReadInt32(), reader.ReadBoolean(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble());
                uint checksum = reader.ReadUInt32();
                if (stream.Position != stream.Length || checksum != result.Checksum || result.Price <= 0) return false;
                value = result; return true;
            }
            catch (Exception) { return false; }
        }
    }

    public static class P2PMarketChecksum
    {
        public static uint Calculate(P2PMarketSnapshot value)
        {
            unchecked
            {
                uint hash = 2166136261;
                void Add(long data) { hash = (hash ^ (uint)data) * 16777619; hash = (hash ^ (uint)(data >> 32)) * 16777619; }
                Add((long)value.Sequence); Add(value.Seed); Add(BitConverter.DoubleToInt64Bits(value.Price));
                Add(BitConverter.DoubleToInt64Bits(value.Bid)); Add(BitConverter.DoubleToInt64Bits(value.Ask)); Add(value.TotalMinutes);
                Add(value.Paused ? 1 : 0); Add(BitConverter.DoubleToInt64Bits(value.Open)); Add(BitConverter.DoubleToInt64Bits(value.High));
                Add(BitConverter.DoubleToInt64Bits(value.Low)); Add(BitConverter.DoubleToInt64Bits(value.Volume)); return hash;
            }
        }
    }
}
