using System;
using System.Collections.Generic;
using UnityEngine;

namespace FXOverdose.Trading
{
    // 차트 타임프레임 (1분, 5분, 15분, 1시간, 4시간, 1일)
    public enum Timeframe
    {
        M1 = 1,
        M5 = 5,
        M15 = 15,
        H1 = 60,
        H4 = 240,
        D1 = 1440
    }

    // 단일 캔들스틱(OHLCV) 데이터를 표현하는 직렬화 가능한 클래스
    [System.Serializable]
    public class CandleData
    {
        [Tooltip("캔들 시작 시점 (게임 누적 분 단위)")]
        public long timestampMinutes;

        [Tooltip("시가 (Open)")]
        public float open;

        [Tooltip("고가 (High)")]
        public float high;

        [Tooltip("저가 (Low)")]
        public float low;

        [Tooltip("종가 (Close)")]
        public float close;

        [Tooltip("거래량 (Volume)")]
        public float volume;

        public CandleData() { }

        public CandleData(long timestampMinutes, float open, float high, float low, float close, float volume)
        {
            this.timestampMinutes = timestampMinutes;
            this.open = open;
            this.high = high;
            this.low = low;
            this.close = close;
            this.volume = volume;
        }

        // 양봉(상승) 여부 (종가가 시가 이상)
        public bool IsBullish => close >= open;

        // 캔들 몸통(Body)의 상단 가격
        public float BodyTop => Mathf.Max(open, close);

        // 캔들 몸통(Body)의 하단 가격
        public float BodyBottom => Mathf.Min(open, close);

        // 캔들 몸통의 크기 (절대값)
        public float BodySize => Mathf.Abs(close - open);

        // 윗꼬리(Upper Wick) 크기
        public float UpperWickSize => high - BodyTop;

        // 아랫꼬리(Lower Wick) 크기
        public float LowerWickSize => BodyBottom - low;

        // 고점과 저점 전체 변동 범위
        public float TotalRange => high - low;

        // 변동률 (%)
        public float PercentageChange => open > 0f ? ((close - open) / open) * 100f : 0f;

        // 복사본 생성 유틸리티
        public CandleData Clone()
        {
            return new CandleData(timestampMinutes, open, high, low, close, volume);
        }

        // 1분봉 여러 개를 합쳐서 상위 타임프레임(5m, 15m, 1h 등) 캔들 1개로 집계(Aggregate)하는 유틸리티
        public static CandleData Aggregate(List<CandleData> sourceCandles, long targetTimestampMinutes)
        {
            if (sourceCandles == null || sourceCandles.Count == 0)
            {
                return null;
            }

            float aggOpen = sourceCandles[0].open;
            float aggClose = sourceCandles[sourceCandles.Count - 1].close;
            float aggHigh = float.MinValue;
            float aggLow = float.MaxValue;
            float aggVolume = 0f;

            for (int i = 0; i < sourceCandles.Count; i++)
            {
                CandleData c = sourceCandles[i];
                if (c.high > aggHigh) aggHigh = c.high;
                if (c.low < aggLow) aggLow = c.low;
                aggVolume += c.volume;
            }

            return new CandleData(targetTimestampMinutes, aggOpen, aggHigh, aggLow, aggClose, aggVolume);
        }
    }
}
