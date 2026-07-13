using System;
using System.Collections.Generic;
using UnityEngine;

namespace FXOverdose.Trading
{
    public class MarketSimulationEngine : MonoBehaviour
    {
        // 시장 거시 국면 (Regime)
        public enum MarketRegime
        {
            Bull,       // 강세장 (상승 드리프트)
            Bear,       // 약세장 (하락 드리프트)
            Sideways,   // 횡보장 (평균 회귀 강함)
            Squeeze     // 광기/스퀴즈 (극고변동성 및 청산 빔)
        }

        [Header("시스템 연결")]
        [SerializeField] private GameManager gameManager;

        [Header("초기 가격 및 설정")]
        [SerializeField] private float initialPrice = 67842.1f; // 참고 이미지 기준 BTC 시작 가격
        [SerializeField] private int maxCandleBuffer = 200;     // 타임프레임별 최대 캔들 유지 개수

        [Header("현재 시장 상태 (읽기 전용)")]
        [SerializeField] private float currentPrice;
        [SerializeField] private float current24hHigh;
        [SerializeField] private float current24hLow;
        [SerializeField] private float current24hVolume;
        [SerializeField] private MarketRegime currentRegime = MarketRegime.Sideways;
        [SerializeField] private float currentVolatility = 0.002f;

        // 국면 전환 제어 변수
        private int minutesUntilNextRegimeChange = 60;
        private float ouCenterPrice; // OU 평균 회귀 중심 가격

        // 실시간 1분봉 진행 캔들
        private CandleData liveM1Candle;
        private long currentTotalMinutes = 0;

        // 타임프레임별 캔들 저장소
        private Dictionary<Timeframe, List<CandleData>> candleHistories = new Dictionary<Timeframe, List<CandleData>>();
        private Dictionary<Timeframe, CandleData> liveAggregatedCandles = new Dictionary<Timeframe, CandleData>();

        // 외부에서 캔들 및 가격 정보에 접근하기 위한 프로퍼티 및 이벤트
        public float CurrentPrice => currentPrice;
        public float Current24hHigh => current24hHigh;
        public float Current24hLow => current24hLow;
        public float Current24hVolume => current24hVolume;
        public MarketRegime CurrentRegime => currentRegime;

        // 가격이나 캔들이 갱신될 때 UI 및 트레이딩 컨트롤러에 알리는 이벤트
        public event Action<float> OnPriceUpdated;
        public event Action<Timeframe, CandleData> OnCandleClosed;

        private void Awake()
        {
            // 모든 타임프레임 리스트 초기화
            foreach (Timeframe tf in Enum.GetValues(typeof(Timeframe)))
            {
                candleHistories[tf] = new List<CandleData>();
            }
        }

        private void Start()
        {
            if (gameManager == null)
            {
                gameManager = FindFirstObjectByType<GameManager>();
            }

            if (gameManager != null)
            {
                gameManager.OnGameMinuteAdvanced += OnGameMinuteAdvanced;
            }

            ResetEngine(initialPrice);
        }

        private void OnDestroy()
        {
            if (gameManager != null)
            {
                gameManager.OnGameMinuteAdvanced -= OnGameMinuteAdvanced;
            }
        }

        public void ResetEngine(float startPrice)
        {
            currentPrice = startPrice;
            ouCenterPrice = startPrice;
            current24hHigh = startPrice;
            current24hLow = startPrice;
            current24hVolume = 0f;
            currentTotalMinutes = 0;

            foreach (var list in candleHistories.Values)
            {
                list.Clear();
            }
            liveAggregatedCandles.Clear();

            // 게임 시작 전 과거 150분(2시간 반) 데이터 Pre-warm 생성
            PrewarmHistoricalCandles(150);

            // 첫 실시간 1분봉 열기
            StartNewLiveCandle(currentPrice);
        }

        private void Update()
        {
            if (gameManager == null || gameManager.CurrentState != GameManager.GameState.Playing)
            {
                return;
            }

            // 프레임 단위 실시간 틱 주가 이동 (보간 및 미시 변동성)
            SimulateTickMovement(Time.deltaTime);
        }

        // 프레임 단위 실시간 주가 움직임 시뮬레이션
        private void SimulateTickMovement(float deltaTime)
        {
            float secondsPerMinute = gameManager != null ? gameManager.SecondsPerGameMinute : 5.0f; // 1분 = 실제 시간 5초
            float dtFraction = deltaTime / Mathf.Max(0.001f, secondsPerMinute); // 1분 대비 흐른 시간 비율

            // 1. 국면(Regime)별 드리프트(Drift) 및 목표 변동성
            float drift = 0f;
            float targetVol = 0.002f;
            float ouTheta = 0.05f; // 평균 회귀 강도

            switch (currentRegime)
            {
                case MarketRegime.Bull:
                    drift = 0.0004f;
                    targetVol = 0.0025f;
                    ouTheta = 0.02f;
                    break;
                case MarketRegime.Bear:
                    drift = -0.0004f;
                    targetVol = 0.0035f;
                    ouTheta = 0.02f;
                    break;
                case MarketRegime.Sideways:
                    drift = 0f;
                    targetVol = 0.0015f;
                    ouTheta = 0.15f; // 박스권 강한 회귀
                    break;
                case MarketRegime.Squeeze:
                    drift = UnityEngine.Random.Range(-0.0008f, 0.0008f);
                    targetVol = 0.008f; // 광기 변동성
                    ouTheta = 0.01f;
                    break;
            }

            // 2. GARCH 스타일 변동성 군집 (TargetVol로 서서히 수렴하거나 스파이크 후 유지)
            currentVolatility = Mathf.Lerp(currentVolatility, targetVol, dtFraction * 5f);

            // 3. OU (Ornstein-Uhlenbeck) 평균 회귀 항
            float ouTerm = ouTheta * (ouCenterPrice - currentPrice) / currentPrice;

            // 4. 확률적 위너 과정 (Brownian Motion Noise)
            // Box-Muller 변환으로 정규 분포 난수 생성
            float u1 = UnityEngine.Random.value;
            float u2 = UnityEngine.Random.value;
            float randNormal = Mathf.Sqrt(-2f * Mathf.Log(Mathf.Max(1e-6f, u1))) * Mathf.Sin(2f * Mathf.PI * u2);

            float stochasticNoise = currentVolatility * Mathf.Sqrt(dtFraction) * randNormal;

            // 5. 최종 수익률 및 가격 변동
            float totalReturn = (drift * dtFraction) + (ouTerm * dtFraction) + stochasticNoise;
            float priceDelta = currentPrice * totalReturn;
            currentPrice += priceDelta;
            if (currentPrice < 10f) currentPrice = 10f; // 최저가 방어

            // 6. 실시간 1분봉 및 상위 타임프레임 Live 캔들 갱신
            float tickVolume = Mathf.Abs(priceDelta) * UnityEngine.Random.Range(2f, 10f);
            UpdateLiveCandlesWithTick(currentPrice, tickVolume);

            // 이벤트 알림
            OnPriceUpdated?.Invoke(currentPrice);
        }

        // 매 프레임 실시간 틱 가격을 Live 캔들에 반영
        private void UpdateLiveCandlesWithTick(float price, float volume)
        {
            if (liveM1Candle != null)
            {
                if (price > liveM1Candle.high) liveM1Candle.high = price;
                if (price < liveM1Candle.low) liveM1Candle.low = price;
                liveM1Candle.close = price;
                liveM1Candle.volume += volume;
            }

            // 상위 타임프레임 진행 중인 캔들들도 함께 갱신
            foreach (Timeframe tf in Enum.GetValues(typeof(Timeframe)))
            {
                if (liveAggregatedCandles.TryGetValue(tf, out CandleData aggCandle))
                {
                    if (price > aggCandle.high) aggCandle.high = price;
                    if (price < aggCandle.low) aggCandle.low = price;
                    aggCandle.close = price;
                    aggCandle.volume += volume;
                }
            }

            // 24시간 고가/저가/거래량 갱신
            if (price > current24hHigh) current24hHigh = price;
            if (price < current24hLow) current24hLow = price;
            current24hVolume += volume;
        }

        // GameManager의 AdvanceOneMinute과 연동되어 1분마다 호출되는 함수
        public void OnGameMinuteAdvanced()
        {
            currentTotalMinutes++;

            // 1. 유동성 사냥(Liquidity Sweep) 위꼬리/아래꼬리 스파이크 체크
            CheckLiquidationSweep();

            // 2. 1분봉 확정 및 저장
            FinalizeCandle(Timeframe.M1, liveM1Candle);

            // 3. 상위 타임프레임(5m, 15m, 1h, 4h, 1D) 주기 검사 및 확정
            foreach (Timeframe tf in Enum.GetValues(typeof(Timeframe)))
            {
                if (tf == Timeframe.M1) continue;

                int periodMinutes = (int)tf;
                if (currentTotalMinutes % periodMinutes == 0)
                {
                    if (liveAggregatedCandles.TryGetValue(tf, out CandleData finishedAgg))
                    {
                        FinalizeCandle(tf, finishedAgg);
                    }
                }
            }

            // 4. 다음 분 실시간 캔들 생성
            StartNewLiveCandle(currentPrice);

            // 5. 국면(Regime) 전환 검사
            minutesUntilNextRegimeChange--;
            if (minutesUntilNextRegimeChange <= 0)
            {
                SwitchToRandomRegime();
            }

            // 6. OU 중심 가격(Center Price)을 서서히 이동평균 쪽으로 이동
            ouCenterPrice = Mathf.Lerp(ouCenterPrice, currentPrice, 0.05f);
        }

        // 캔들을 확정하여 히스토리 버퍼에 추가
        private void FinalizeCandle(Timeframe tf, CandleData candle)
        {
            if (candle == null) return;

            List<CandleData> history = candleHistories[tf];
            history.Add(candle);

            // 최대 버퍼 초과 시 오래된 캔들 제거
            if (history.Count > maxCandleBuffer)
            {
                history.RemoveAt(0);
            }

            // 상위 캔들 확정 시 이벤트 통지
            OnCandleClosed?.Invoke(tf, candle);
        }

        // 새 캔들 시작
        private void StartNewLiveCandle(float openPrice)
        {
            liveM1Candle = new CandleData(currentTotalMinutes, openPrice, openPrice, openPrice, openPrice, 0f);

            foreach (Timeframe tf in Enum.GetValues(typeof(Timeframe)))
            {
                int periodMinutes = (int)tf;
                // 해당 타임프레임이 새로 시작되는 주기인지 확인
                if (currentTotalMinutes % periodMinutes == 0 || !liveAggregatedCandles.ContainsKey(tf))
                {
                    liveAggregatedCandles[tf] = new CandleData(currentTotalMinutes, openPrice, openPrice, openPrice, openPrice, 0f);
                }
            }
        }

        // 국면 전환 (Regime Switching)
        private void SwitchToRandomRegime()
        {
            float rand = UnityEngine.Random.value;
            if (rand < 0.35f) currentRegime = MarketRegime.Sideways;
            else if (rand < 0.65f) currentRegime = MarketRegime.Bull;
            else if (rand < 0.90f) currentRegime = MarketRegime.Bear;
            else currentRegime = MarketRegime.Squeeze;

            minutesUntilNextRegimeChange = UnityEngine.Random.Range(30, 120); // 30분~2시간 유지
            Debug.Log($"[MarketEngine] 국면 전환: {currentRegime} (유지: {minutesUntilNextRegimeChange}분)");
        }

        // 유동성 사냥 (꼬리 휩소 스파이크 발생)
        private void CheckLiquidationSweep()
        {
            // Squeeze 국면에서는 30% 확률, 그 외에는 5% 확률로 꼬리 생성
            float sweepProb = currentRegime == MarketRegime.Squeeze ? 0.30f : 0.05f;
            if (UnityEngine.Random.value < sweepProb)
            {
                float sweepMagnitude = UnityEngine.Random.Range(0.005f, 0.02f); // 0.5% ~ 2% 꼬리
                bool sweepUp = UnityEngine.Random.value > 0.5f;

                if (sweepUp)
                {
                    float spikePrice = currentPrice * (1f + sweepMagnitude);
                    if (liveM1Candle != null && spikePrice > liveM1Candle.high) liveM1Candle.high = spikePrice;
                }
                else
                {
                    float spikePrice = currentPrice * (1f - sweepMagnitude);
                    if (liveM1Candle != null && spikePrice < liveM1Candle.low) liveM1Candle.low = spikePrice;
                }

                if (liveM1Candle != null)
                {
                    liveM1Candle.volume += UnityEngine.Random.Range(50f, 200f); // 거래량 폭증
                }
                currentVolatility *= 2.0f; // 순간 변동성 폭발
            }
        }

        // 돌발 이벤트(Choice Event) 시 시장 충격 주기
        public void TriggerMarketShock(float percentageChange, float volatilityMultiplier, int durationMinutes)
        {
            float shockPrice = currentPrice * (1f + (percentageChange / 100f));
            currentPrice = shockPrice;
            currentVolatility *= volatilityMultiplier;
            minutesUntilNextRegimeChange = durationMinutes;

            if (percentageChange > 0f) currentRegime = MarketRegime.Bull;
            else if (percentageChange < 0f) currentRegime = MarketRegime.Bear;
            else currentRegime = MarketRegime.Squeeze;

            UpdateLiveCandlesWithTick(currentPrice, Mathf.Abs(percentageChange) * 100f);
            Debug.Log($"[MarketEngine] 외부 이벤트 충격 발생! 변화율: {percentageChange:F2}%, 현재가: {currentPrice:N1}");
        }

        // 타임프레임별 과거 캔들 리스트 조회
        public List<CandleData> GetCandleHistory(Timeframe tf)
        {
            if (candleHistories.TryGetValue(tf, out var list))
            {
                return list;
            }
            return new List<CandleData>();
        }

        // 실시간 진행 중인 캔들 조회
        public CandleData GetLiveCandle(Timeframe tf)
        {
            if (tf == Timeframe.M1) return liveM1Candle;
            if (liveAggregatedCandles.TryGetValue(tf, out var candle)) return candle;
            return liveM1Candle;
        }

        // 게임 시작 시 초기 과거 데이터(Pre-warm) 생성
        private void PrewarmHistoricalCandles(int minutesCount)
        {
            float tempPrice = initialPrice;
            long startTimestamp = -minutesCount;

            for (int i = 0; i < minutesCount; i++)
            {
                long ts = startTimestamp + i;
                float drift = UnityEngine.Random.Range(-0.0015f, 0.0015f);
                float open = tempPrice;
                float close = open * (1f + drift + UnityEngine.Random.Range(-0.002f, 0.002f));
                float high = Mathf.Max(open, close) * (1f + UnityEngine.Random.Range(0f, 0.003f));
                float low = Mathf.Min(open, close) * (1f - UnityEngine.Random.Range(0f, 0.003f));
                float vol = UnityEngine.Random.Range(10f, 100f);

                CandleData m1 = new CandleData(ts, open, high, low, close, vol);
                candleHistories[Timeframe.M1].Add(m1);
                tempPrice = close;

                // 상위 타임프레임 집계
                foreach (Timeframe tf in Enum.GetValues(typeof(Timeframe)))
                {
                    if (tf == Timeframe.M1) continue;
                    int period = (int)tf;
                    if (Mathf.Abs(ts) % period == 0 || candleHistories[tf].Count == 0)
                    {
                        candleHistories[tf].Add(new CandleData(ts, open, high, low, close, vol));
                    }
                    else
                    {
                        CandleData lastAgg = candleHistories[tf][candleHistories[tf].Count - 1];
                        if (high > lastAgg.high) lastAgg.high = high;
                        if (low < lastAgg.low) lastAgg.low = low;
                        lastAgg.close = close;
                        lastAgg.volume += vol;
                    }
                }
            }

            currentPrice = tempPrice;
            ouCenterPrice = tempPrice;
            current24hHigh = tempPrice * 1.05f;
            current24hLow = tempPrice * 0.95f;
            current24hVolume = 12543.8f;
        }
    }
}
