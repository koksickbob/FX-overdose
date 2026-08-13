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
        public const double SecondsPerGameMinute = 1d;

        private enum MarketRegime { Bull, Bear, Sideways, Squeeze }

        private uint randomState;
        private double minuteAccumulator;
        private double tickAccumulator;
        private double forcedTrendPerTick;
        private int forcedTrendTicks;
        private double simulatedSeconds;
        private double currentVolatility = 0.002;
        private double ouCenterPrice;
        private MarketRegime currentRegime = MarketRegime.Sideways;
        private MarketRegime dailyRegime;
        private int minutesUntilNextRegimeChange = 60;

        public P2PMarketSimulationEngine(int seed, double initialPrice = 67842.1)
        {
            if (initialPrice <= 0 || double.IsNaN(initialPrice) || double.IsInfinity(initialPrice))
                throw new ArgumentOutOfRangeException(nameof(initialPrice));
            Seed = seed; randomState = unchecked((uint)seed) | 1u;
            ouCenterPrice = initialPrice;
            dailyRegime = PickDailyRegime();
            Snapshot = new P2PMarketSnapshot(0, seed, initialPrice, initialPrice, initialPrice,
                9 * 60, true, initialPrice, initialPrice, initialPrice, 0);
        }

        public int Seed { get; }
        public P2PMarketSnapshot Snapshot { get; private set; }

        public void SetPaused(bool paused) => Apply(Snapshot.Price, Snapshot.TotalMinutes, paused, 0);

        /// <summary>원본 돌발 이벤트의 OverrideMarketTrend와 같이 지정한 등락률을 여러 틱에 나눠 반영합니다.</summary>
        public void OverrideMarketTrend(double percent, int durationTicks)
        {
            forcedTrendTicks = Math.Max(1, durationTicks);
            forcedTrendPerTick = percent / 100d / forcedTrendTicks;
        }

        public void Advance(double realSeconds, double secondsPerGameMinute = SecondsPerGameMinute)
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
                // 원본 MarketSimulationEngine과 동일하게 한 게임 분을 다섯 틱으로 나누고,
                // 국면/세션 변동성, 단기 파동, GARCH 완화, OU 평균회귀를 함께 적용합니다.
                simulatedSeconds += tickInterval;
                double dtFraction = tickInterval / secondsPerGameMinute;
                GetRegimeParameters(out double drift,out double targetVol,out double ouTheta);

                int hour=Snapshot.TotalMinutes/60;
                double sessionVolMultiplier=hour<8?0.5:hour<16?1.2:2.0;
                targetVol*=sessionVolMultiplier;

                double marketClock=Snapshot.TotalMinutes*60d+simulatedSeconds;
                double waveDrift=Math.Sin(marketClock%350d/350d*Math.PI*2d)*0.0004
                    +Math.Cos(marketClock%130d/130d*Math.PI*2d)*0.0002
                    +Math.Sin(simulatedSeconds%15d/15d*Math.PI*2d)*0.00015;
                double macroDrift=dailyRegime==MarketRegime.Bull?0.00015
                    :dailyRegime==MarketRegime.Bear?-0.00015
                    :dailyRegime==MarketRegime.Squeeze?Range(-0.0003,0.0003):0;
                drift+=waveDrift+macroDrift;

                currentVolatility=Lerp(currentVolatility,targetVol,Math.Min(1d,dtFraction*5d));
                double ouTerm=ouTheta*(ouCenterPrice-Snapshot.Price)/Snapshot.Price;
                double u1=Math.Max(1e-6,NextUnit()),u2=NextUnit();
                double normal=Math.Sqrt(-2d*Math.Log(u1))*Math.Sin(2d*Math.PI*u2);
                double stochasticNoise=currentVolatility*Math.Sqrt(dtFraction)*normal;
                double totalReturn=drift*dtFraction+ouTerm*dtFraction+stochasticNoise;

                if (forcedTrendTicks > 0)
                {
                    totalReturn += forcedTrendPerTick;
                    forcedTrendTicks--;
                }
                double next = Math.Max(10, Snapshot.Price * (1 + totalReturn));
                double volume=Math.Abs(next-Snapshot.Price)*Range(2d,10d);
                Apply(next, Snapshot.TotalMinutes + (advanceMinute ? 1 : 0), false, volume);
                if(advanceMinute)AdvanceRegimeClock();
            }
        }

        private void GetRegimeParameters(out double drift,out double targetVol,out double ouTheta)
        {
            switch(currentRegime)
            {
                case MarketRegime.Bull:drift=0.0004;targetVol=0.0035;ouTheta=0.02;break;
                case MarketRegime.Bear:drift=-0.0004;targetVol=0.0045;ouTheta=0.02;break;
                case MarketRegime.Squeeze:drift=Range(-0.0008,0.0008);targetVol=0.012;ouTheta=0.01;break;
                default:drift=0;targetVol=0.0025;ouTheta=0.15;break;
            }
        }

        private void AdvanceRegimeClock()
        {
            ouCenterPrice=Lerp(ouCenterPrice,Snapshot.Price,0.05);
            if(--minutesUntilNextRegimeChange>0)return;
            double value=NextUnit();
            if(dailyRegime==MarketRegime.Bull)currentRegime=value<.60?MarketRegime.Bull:value<.80?MarketRegime.Sideways:value<.90?MarketRegime.Bear:MarketRegime.Squeeze;
            else if(dailyRegime==MarketRegime.Bear)currentRegime=value<.60?MarketRegime.Bear:value<.80?MarketRegime.Sideways:value<.90?MarketRegime.Bull:MarketRegime.Squeeze;
            else if(dailyRegime==MarketRegime.Squeeze)currentRegime=value<.50?MarketRegime.Squeeze:value<.70?MarketRegime.Bull:value<.90?MarketRegime.Bear:MarketRegime.Sideways;
            else currentRegime=value<.40?MarketRegime.Sideways:value<.65?MarketRegime.Bull:value<.90?MarketRegime.Bear:MarketRegime.Squeeze;
            minutesUntilNextRegimeChange=RangeInt(30,120);
        }

        private MarketRegime PickDailyRegime()
        {
            double value=NextUnit();
            return value<.35?MarketRegime.Sideways:value<.60?MarketRegime.Bull:value<.85?MarketRegime.Bear:MarketRegime.Squeeze;
        }

        private void Apply(double price, int totalMinutes, bool paused, double volume)
        {
            bool newMinute = totalMinutes != Snapshot.TotalMinutes;
            double open = newMinute ? Snapshot.Price : Snapshot.Open;
            double high = newMinute ? Math.Max(open, price) : Math.Max(Snapshot.High, price);
            double low = newMinute ? Math.Min(open, price) : Math.Min(Snapshot.Low, price);
            // 원본의 변동성 기반 스프레드와 0.5% 소프트 캡을 그대로 사용합니다.
            double spread = Math.Min(price*0.005,Math.Max(0.01,price*currentVolatility*0.5));
            Snapshot = new P2PMarketSnapshot(Snapshot.Sequence + 1, Seed, price,
                price - spread / 2, price + spread / 2, Math.Min(totalMinutes, 24 * 60),
                paused, open, high, low, newMinute ? volume : Snapshot.Volume + volume);
        }

        private double NextUnit()
        {
            randomState ^= randomState << 13; randomState ^= randomState >> 17; randomState ^= randomState << 5;
            return randomState / (double)uint.MaxValue;
        }

        private double Range(double min,double max)=>min+(max-min)*NextUnit();
        private int RangeInt(int min,int max)=>min+(int)Math.Floor(NextUnit()*(max-min));
        private static double Lerp(double from,double to,double amount)=>from+(to-from)*amount;
    }

    public sealed class P2PMarketReplica
    {
        public P2PMarketSnapshot Snapshot { get; private set; }
        public bool TryApply(P2PMarketSnapshot snapshot)
        {
            if (!IsSequenceNewer(snapshot.Sequence,Snapshot.Sequence) || snapshot.Checksum != P2PMarketChecksum.Calculate(snapshot)) return false;
            Snapshot = snapshot; return true;
        }
        private static bool IsSequenceNewer(ulong candidate,ulong previous)=>candidate!=previous&&unchecked((long)(candidate-previous))>0;
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
