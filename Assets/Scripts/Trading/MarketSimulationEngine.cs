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

        [Header("차트 신호 및 확정적 주가 제어 (Phase 3)")]
        [SerializeField] private SignalPhase currentSignalPhase = SignalPhase.None;
        [SerializeField] private MarketSignal activeSignal;
        [SerializeField] private int signalPhaseTimerMinutes = 0;
        [SerializeField] private int minutesUntilNextSignal = 45; // 30~60분 주기
        [SerializeField] private bool isExternalEventOverride = false;

        public SignalPhase CurrentSignalPhase => currentSignalPhase;
        public MarketSignal ActiveSignal => activeSignal;
        public bool IsExternalEventOverride => isExternalEventOverride;

        [Header("Overdose 폭주 차트 트랩 (Phase 3 Override)")]
        [SerializeField] private bool isOverdoseTrapOverride = false;
        [SerializeField] private TradingController.PositionType overdoseTrapPositionType;
        [SerializeField] private float overdoseTrapEndTime = -1f;

        public bool IsOverdoseTrapOverride => isOverdoseTrapOverride;
        public bool IsMarketOpen { get; private set; } = false;
        public bool IsFastForwarding => gameManager != null && gameManager.IsFastForwardingTime;
        public bool IsOverridingTrend => isExternalEventOverride || isOverdoseTrapOverride || (currentSignalPhase != SignalPhase.None && currentSignalPhase != SignalPhase.Cooldown);

        public event Action<MarketSignal> OnMarketSignalGenerated;
        public event Action<SignalPhase, MarketSignal> OnSignalPhaseChanged;

        public void OpenMarketAfterLoading()
        {
            IsMarketOpen = true;
            minutesUntilNextSignal = 3; // 개장 후 3분(3초) 뒤 첫 거래 신호 발생
            Debug.Log("[MarketSimulationEngine] 📈 로딩 및 AI 개장 대사 출력 완료 -> 시장 개장! 주가 차트 시뮬레이션 및 AI 실시간 매매가 시작됩니다.");
        }

        // 테스트 및 디버그용 수동 신호 발행 Helper
        public void TriggerSignalForTest(MarketSignal signal)
        {
            activeSignal = signal;
            currentSignalPhase = SignalPhase.GraceWindow;
            // 💡 [타이머 정상화] 신호 주입 시 여유 시간(GraceWindow)을 정상 반영하여 즉시 GuaranteedOverride로 건너뛰지 않도록 보호
            signalPhaseTimerMinutes = signal.GraceMinutes > 0 ? signal.GraceMinutes : 3;
            OnMarketSignalGenerated?.Invoke(signal);
            OnSignalPhaseChanged?.Invoke(currentSignalPhase, signal);
        }

        // 테스트 및 디버그용 수동 주가 틱 이동 Helper (진입 포지션의 자동 익절/손절/청산 연동 검증용)
        public void SimulatePriceTickForTest(float newPrice)
        {
            currentPrice = newPrice;
            Debug.Log($"[MarketEngine 🧪] 테스트 주가 실시간 틱 이동 강제 실행: -> ${currentPrice:N1}");
            UpdateLiveCandlesWithTick(currentPrice, Mathf.Abs(currentPrice * 0.01f) * UnityEngine.Random.Range(5f, 20f));
            OnPriceUpdated?.Invoke(currentPrice);
        }

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

        private float tickTimer = 0f;

        private void Awake()
        {
            EnsureCandleHistoriesInitialized();
        }

        private void EnsureCandleHistoriesInitialized()
        {
            if (candleHistories == null)
            {
                candleHistories = new Dictionary<Timeframe, List<CandleData>>();
            }
            if (liveAggregatedCandles == null)
            {
                liveAggregatedCandles = new Dictionary<Timeframe, CandleData>();
            }
            foreach (Timeframe tf in Enum.GetValues(typeof(Timeframe)))
            {
                if (!candleHistories.ContainsKey(tf) || candleHistories[tf] == null)
                {
                    candleHistories[tf] = new List<CandleData>();
                }
            }
        }

        private void Start()
        {
            EnsureCandleHistoriesInitialized();
            if (gameManager == null)
            {
                gameManager = FindAnyObjectByType<GameManager>();
            }

            if (gameManager != null)
            {
                gameManager.OnGameMinuteAdvanced += OnGameMinuteAdvanced;
                gameManager.OnFastForwardEnded += HandleFastForwardEnded;
            }

            ResetEngine(initialPrice);
        }

        private void OnDestroy()
        {
            if (gameManager != null)
            {
                gameManager.OnGameMinuteAdvanced -= OnGameMinuteAdvanced;
                gameManager.OnFastForwardEnded -= HandleFastForwardEnded;
            }
        }

        private void HandleFastForwardEnded()
        {
            var llm = FXOverdose.AI.LLM.LocalLLMService.Instance;
            if (llm != null) llm.ClearQueueExceptSkillUpgraded();

            var tradingCtrl = UnityEngine.Object.FindAnyObjectByType<TradingController>(FindObjectsInactive.Include);
            if (tradingCtrl != null && tradingCtrl.CurrentPosition == TradingController.PositionType.None)
            {
                currentSignalPhase = SignalPhase.None;
                minutesUntilNextSignal = UnityEngine.Random.Range(3, 6);
                Debug.Log($"[MarketEngine] 🚀 스킬 업그레이드(고속 시간 패스) 완료 -> 업그레이드된 새 스킬 능력치 반영을 위해 {minutesUntilNextSignal}분(초) 후 신규 거래 신호가 발행됩니다.");
            }
        }

        public void ResetEngine(float startPrice)
        {
            EnsureCandleHistoriesInitialized();
            IsMarketOpen = false;
            currentPrice = startPrice;
            ouCenterPrice = startPrice;
            current24hHigh = startPrice;
            current24hLow = startPrice;
            current24hVolume = 0f;
            currentTotalMinutes = 0;
            tickTimer = 0f;
            currentSignalPhase = SignalPhase.None;
            signalPhaseTimerMinutes = 0;
            isExternalEventOverride = false;
            // 💡 [AI 매매 실시간 검증 최적화] 게임 시작 후 단 3초(3분봉) 만에 첫 매매 신호가 발생하여 주인공 AI가 즉시 판단 및 매매를 개시하도록 설정
            minutesUntilNextSignal = 3;

            foreach (var list in candleHistories.Values)
            {
                list.Clear();
            }
            liveAggregatedCandles.Clear();

            // 게임 시작 전 과거 150분(2시간 반) 데이터 Pre-warm 생성 및 최종 시뮬레이션 마감 종가를 현재 주가로 동기화
            float prewarmedEndPrice = PrewarmHistoricalCandles(150);
            currentPrice = prewarmedEndPrice;
            ouCenterPrice = prewarmedEndPrice;

            // 첫 실시간 1분봉 열기 (과거 캔들의 마지막 종가와 정확히 맞닿아 갭 없이 연결)
            StartNewLiveCandle(currentPrice);
        }

        private void Update()
        {
            if (gameManager == null || gameManager.CurrentState != GameManager.GameState.Playing || !IsMarketOpen)
            {
                return;
            }

            // 1초마다 주가 틱이 변동될 때 발생 (명세서 L101 기준. 1분 = 5초 속도 기준 1분당 5틱 유지)
            float secondsPerMinute = gameManager != null ? gameManager.SecondsPerGameMinute : 5.0f;
            float tickInterval = Mathf.Clamp(secondsPerMinute / 5.0f, 0.05f, 1.0f);

            tickTimer += Time.deltaTime;
            while (tickTimer >= tickInterval)
            {
                tickTimer -= tickInterval;
                SimulateTickMovement(tickInterval);
            }
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

            // 💡 [자연스러운 차트 파동 생성] 고정된 Drift로 인해 차트가 일직선으로 그려지는 것을 방지하기 위해 단기 파동(Sine Wave)을 결합합니다.
            float waveCycle1 = (currentTotalMinutes % 35) / 35f * Mathf.PI * 2f;
            float waveCycle2 = (currentTotalMinutes % 13) / 13f * Mathf.PI * 2f;
            float waveDrift = (Mathf.Sin(waveCycle1) * 0.0003f) + (Mathf.Cos(waveCycle2) * 0.00015f);
            
            // 고속 스킵 중에는 랜덤성을 더 부여하여 일직선 패턴 완전 타파
            if (IsFastForwarding)
            {
                waveDrift += UnityEngine.Random.Range(-0.0003f, 0.0003f);
            }

            drift += waveDrift;

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

            // ⭐ [Overdose 폭주 죽음의 차트 빔 주입] 오버도즈 상태일 때 주인공 포지션과 반대 방향으로 휩소(중간 반등) 없이 확실하고 가파르게 주가를 이동시켜 0원 청산을 유도!
            if (isOverdoseTrapOverride && Time.time < overdoseTrapEndTime)
            {
                // 노이즈(잔파도 휩소)를 3% 수준으로 극히 억제하여 휩소에 의해 청산이 방해받거나 엉뚱한 반등이 나오는 것을 원천 차단
                stochasticNoise *= 0.03f;
                ouTerm = 0f;

                // 125배 레버리지 기준 0.8% 역행 시 -100% 청산. 약 20~25초에 걸쳐 확실하게 청산선(-1.2% 등)에 도달하도록 강력한 반대 방향 드리프트 주입
                float trapRemainingSeconds = Mathf.Max(1f, overdoseTrapEndTime - Time.time);
                float requiredDriftPerSecond = (overdoseTrapPositionType == TradingController.PositionType.Long ? -0.015f : 0.015f) / Mathf.Max(15f, trapRemainingSeconds);
                drift = requiredDriftPerSecond * (gameManager != null ? gameManager.SecondsPerGameMinute : 5.0f);
            }
            else if (isOverdoseTrapOverride && Time.time >= overdoseTrapEndTime)
            {
                CancelOverdoseTrapSignal();
            }
            else if (currentSignalPhase == SignalPhase.GraceWindow)
            {
                // 1단계 판단 여유 시간: 노이즈를 15% 수준으로 억제하고 횡보 유지 (골든타임 예고 방송 및 대기)
                stochasticNoise *= 0.15f;
                drift = 0f;
            }
            else if (currentSignalPhase == SignalPhase.GuaranteedOverride)
            {
                // 2단계 확정적 주가 제어 구간: 위너 노이즈 억제 및 확정적 드리프트 주입
                stochasticNoise *= 0.18f; // 잔파도 최소화하되 캔들 자연스러움 유지

                if (isExternalEventOverride && !activeSignal.IsTrueSignal)
                {
                    // 💡 [악결과(Trap/실패) 휩소 꼬리 반등 궤적 생성]
                    // 앞 70% 구간: 목표 변동률의 135%까지 강하게 몰아쳐 극도의 공포(-60%~-85% ROE) 유도
                    // 뒤 30% 구간: 45% 강한 기술적 반등 꼬리(Whipsaw Recovery Bounce)를 발생시켜 버티기/물타기 극적 탈출 기회 제공
                    float totalDuration = Mathf.Max(1f, activeSignal.DurationMinutes);
                    float elapsedRatio = 1f - ((float)signalPhaseTimerMinutes / totalDuration);

                    if (elapsedRatio < 0.7f)
                    {
                        float targetDriftPerMinute = ((activeSignal.TargetPercentageDelta * 1.35f) / 100f) / Mathf.Max(1f, totalDuration * 0.7f);
                        drift = targetDriftPerMinute;
                    }
                    else
                    {
                        // 막바지 30% 구간: 반등 꼬리 드리프트
                        float recoveryDriftPerMinute = ((-activeSignal.TargetPercentageDelta * 0.45f) / 100f) / Mathf.Max(1f, totalDuration * 0.3f);
                        drift = recoveryDriftPerMinute;
                    }
                }
                else
                {
                    // 정상 확정 구간: 목표 변동률을 남은 보장 시간 동안 분할 반영하여 부드러운 드리프트 생성
                    float targetDriftPerMinute = (activeSignal.TargetPercentageDelta / 100f) / Mathf.Max(1, activeSignal.DurationMinutes);
                    drift = targetDriftPerMinute;
                }

                // OU 평균 회귀 항 무력화 (일방향 궤적 보장)
                ouTerm = 0f;
            }

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

        // 스킬 공부 및 시간 패스 등으로 1분 단위 고속 경과 시 차트 캔들이 비거나 0-Volume 일직선으로 굳는 현상을 방지하기 위한 실시간 틱 시뮬레이션
        public void SimulateFastForwardTicks(float dtMinutes = 1.0f)
        {
            float secondsPerMinute = gameManager != null ? gameManager.SecondsPerGameMinute : 5.0f;
            int subTicks = 5; // 1분당 5번의 가상 틱 변동을 분할 적용하여 정교한 캔들 꼬리 및 몸통 생성
            float subDeltaTime = (secondsPerMinute * dtMinutes) / subTicks;

            for (int i = 0; i < subTicks; i++)
            {
                SimulateTickMovement(subDeltaTime);
            }
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

            // 💡 [시간 고속 경과 및 스킬 공부 시 차트 캔들 비어버림 방지]
            // Update() 프레임이 돌지 않고 동기식으로 시간이 패스될 경우(또는 GameManager 고속 진행 중),
            // 해당 1분봉이 거래량 0과 일직선(open == high == low == close)으로 비어버리는 것을 감지하여 가상 틱 시뮬레이션을 선제 주입합니다!
            if ((gameManager != null && gameManager.IsFastForwardingTime) ||
                (liveM1Candle != null && liveM1Candle.volume <= 0.0001f && Mathf.Approximately(liveM1Candle.high, liveM1Candle.low)))
            {
                SimulateFastForwardTicks(1.0f);
            }

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
            if (IsFastForwarding && UnityEngine.Random.value < 0.15f)
            {
                // 스킵 중에는 차트가 한 방향으로만 일직선으로 뻗는 것을 방지하기 위해 15% 확률로 잦은 국면 전환 유도
                minutesUntilNextRegimeChange = 0;
            }

            if (minutesUntilNextRegimeChange <= 0)
            {
                SwitchToRandomRegime();
            }

            // 6. OU 중심 가격(Center Price)을 서서히 이동평균 쪽으로 이동
            ouCenterPrice = Mathf.Lerp(ouCenterPrice, currentPrice, 0.05f);

            // 7. 차트 신호 및 주가 오버라이드 타임라인 진행 (Phase 3)
            UpdateSignalSystem();
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

        // 돌발 선택 이벤트 차트 빔 점진 주입 및 골든타임 연동 (OverrideMarketTrend)
        public void OverrideMarketTrend(float targetChangePercent, int durationSeconds, bool isWhipsaw = false)
        {
            // 💡 [순간이동 제거] 1프레임 만에 주가를 순간 이동시키지 않고, 인게임시간 30분(5분봉 6캔들) 동안 점진적 드리프트로 이동하도록 설정
            int durationMins = 30; // 사용자 요청: 돌발이벤트 차트 변동을 30분으로 일괄 확대
            InitiateEventSignalOverride(targetChangePercent, durationMins, isWhipsaw);
        }

        public void InitiateEventSignalOverride(float targetChangePercent, int durationMins, bool isWhipsaw = false)
        {
            MarketSignalType sigType = isWhipsaw 
                ? (targetChangePercent >= 0f ? MarketSignalType.BullTrap : MarketSignalType.BearTrap) 
                : (targetChangePercent >= 0f ? MarketSignalType.BullishBreakout : MarketSignalType.BearishBreakout);

            // 💡 1단계 골든타임(GraceWindow) 2분(실시간 10초) 설정으로 AI 예고 대사 및 판단 여유 보장
            int graceMins = 2;
            currentVolatility *= (isWhipsaw ? 3.0f : 1.8f);
            minutesUntilNextRegimeChange = durationMins + graceMins;

            if (targetChangePercent > 0f) currentRegime = MarketRegime.Bull;
            else if (targetChangePercent < 0f) currentRegime = MarketRegime.Bear;
            else currentRegime = MarketRegime.Squeeze;

            ForceInjectSignal(sigType, SignalStrength.Strong, !isWhipsaw, targetChangePercent, durationMins, graceMins);
            Debug.Log($"[MarketEngine] ⚡ InitiateEventSignalOverride 실행! 목표 변동률: {targetChangePercent:F2}%, 지속 캔들: {durationMins}분 (골든타임 {graceMins}분, 휩소: {isWhipsaw})");
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

        // 게임 시작 시 초기 과거 데이터(Pre-warm) 생성 및 최종 종가 반환
        private float PrewarmHistoricalCandles(int minutesCount)
        {
            float tempPrice = initialPrice;
            long startTimestamp = -minutesCount;

            for (int i = 0; i < minutesCount; i++)
            {
                long ts = startTimestamp + i;
                float drift = UnityEngine.Random.Range(-0.0018f, 0.0018f);
                float open = tempPrice;
                float close = open * (1f + drift + UnityEngine.Random.Range(-0.0022f, 0.0022f));
                float high = Mathf.Max(open, close) * (1f + UnityEngine.Random.Range(0f, 0.0032f));
                float low = Mathf.Min(open, close) * (1f - UnityEngine.Random.Range(0f, 0.0032f));

                // 1. 캔들 크기(전체 고점-저점 변동폭 및 실물 몸통 크기)에 비례하는 기본 거래량 산출
                float bodySize = Mathf.Abs(close - open);
                float totalRange = Mathf.Max(0.01f, high - low);
                float baseVolume = (totalRange * UnityEngine.Random.Range(4.5f, 7.5f)) + (bodySize * UnityEngine.Random.Range(6.0f, 11.0f));

                // 2. 캔들 방향(양봉/음봉) 및 형태(장대/긴 꼬리/도지)에 따른 거래량 가중치 부여
                float directionMultiplier = 1.0f;
                bool isBullish = close >= open;

                if (totalRange / Mathf.Max(1f, open) > 0.003f) // 변동성이 큰 장대캔들 또는 큰 꼬리 캔들
                {
                    if (bodySize > totalRange * 0.6f)
                    {
                        // 장대양봉 또는 장대음봉: 거래량 폭발 실린 추세 돌파 또는 패닉셀
                        directionMultiplier = isBullish 
                            ? UnityEngine.Random.Range(1.4f, 2.1f) // 강한 매수 돌파 거래량
                            : UnityEngine.Random.Range(1.6f, 2.6f); // 공포 패닉셀 급락 거래량
                    }
                    else
                    {
                        // 긴 꼬리 망치/유성형: 위아래 치열한 매수/매도 공방 거래량
                        directionMultiplier = UnityEngine.Random.Range(1.2f, 1.7f);
                    }
                }
                else if (bodySize < totalRange * 0.3f && totalRange / Mathf.Max(1f, open) < 0.0015f)
                {
                    // 변동성이 적고 몸통이 작은 횡보 도지: 거래량 극감
                    directionMultiplier = UnityEngine.Random.Range(0.3f, 0.65f);
                }
                else
                {
                    // 일반적인 추세 캔들
                    directionMultiplier = isBullish 
                        ? UnityEngine.Random.Range(0.9f, 1.35f) 
                        : UnityEngine.Random.Range(1.0f, 1.55f);
                }

                // 자연스러운 노이즈를 결합하여 최종 1분봉 거래량 확정
                float vol = Mathf.Max(40f, baseVolume * directionMultiplier * UnityEngine.Random.Range(0.88f, 1.12f));

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

            return tempPrice;
        }

        // Phase 3: 차트 신호 및 3단계 주가 제어 타임라인 업데이트 (1분마다 호출)
        private void UpdateSignalSystem()
        {
            if (IsFastForwarding) return;

            switch (currentSignalPhase)
            {
                case SignalPhase.None:
                    minutesUntilNextSignal--;
                    if (minutesUntilNextSignal <= 0)
                    {
                        GenerateMarketSignal();
                    }
                    break;

                case SignalPhase.GraceWindow:
                    signalPhaseTimerMinutes--;
                    if (signalPhaseTimerMinutes <= 0)
                    {
                        // 여유 시간 종료 -> 2단계 확정적 주가 오버라이드 구간 돌입
                        currentSignalPhase = SignalPhase.GuaranteedOverride;
                        signalPhaseTimerMinutes = activeSignal.DurationMinutes;
                        Debug.Log($"[MarketEngine] ⚡ [2단계 확정 주가 오버라이드 돌입] {activeSignal.GetSignalDescription()}");
                        OnSignalPhaseChanged?.Invoke(currentSignalPhase, activeSignal);
                    }
                    break;

                case SignalPhase.GuaranteedOverride:
                    signalPhaseTimerMinutes--;
                    if (signalPhaseTimerMinutes <= 0)
                    {
                        // 확정적 구간 종료 -> 3단계 쿨다운 돌입
                        currentSignalPhase = SignalPhase.Cooldown;
                        isExternalEventOverride = false;
                        signalPhaseTimerMinutes = UnityEngine.Random.Range(10, 16); // 10~15분 쿨다운
                        Debug.Log($"[MarketEngine] 🛑 [확정 주가 제어 종료 -> 쿨다운 돌입] ({signalPhaseTimerMinutes}분 유지)");
                        OnSignalPhaseChanged?.Invoke(currentSignalPhase, activeSignal);
                    }
                    else
                    {
                        // 💡 [무포지션 장기 대기 방지 고속화] 만약 외부 이벤트 빔 오버라이드 상태가 아니고, 현재 AI가 포지션을 잡지 않은 상태라면
                        // 10초 동안만 FOMO 기믹 판정을 위해 주가를 이동시키고, 그 이후에는 남은 오버라이드 및 쿨다운 시간을 모두 생략하여 즉각 다음 매매 기회를 제공!
                        if (!isExternalEventOverride)
                        {
                            var tradingCtrl = UnityEngine.Object.FindAnyObjectByType<TradingController>(FindObjectsInactive.Include);
                            if (tradingCtrl != null && tradingCtrl.CurrentPosition == TradingController.PositionType.None)
                            {
                                if (activeSignal.DurationMinutes > 0 && activeSignal.DurationMinutes - signalPhaseTimerMinutes >= 10)
                                {
                                    Debug.Log("[MarketEngine] ⏩ AI 무포지션 상태 10초 경과 감지 -> 장기 관망 방지를 위해 확정 구간 및 쿨다운을 생략하고 즉각 신규 신호 주기를 시작합니다.");
                                    currentSignalPhase = SignalPhase.None;
                                    minutesUntilNextSignal = UnityEngine.Random.Range(3, 6);
                                    OnSignalPhaseChanged?.Invoke(currentSignalPhase, activeSignal);
                                }
                            }
                        }
                    }
                    break;

                case SignalPhase.Cooldown:
                    signalPhaseTimerMinutes--;
                    if (signalPhaseTimerMinutes <= 0)
                    {
                        currentSignalPhase = SignalPhase.None;
                        isExternalEventOverride = false;
                        minutesUntilNextSignal = UnityEngine.Random.Range(5, 11); // 쿨다운 종료 후 5~10초 내 신속 재진입
                    }
                    else
                    {
                        // 💡 쿨다운 중 무포지션 상태라면 3초 이내로 신속히 재진입 준비
                        var tradingCtrl = UnityEngine.Object.FindAnyObjectByType<TradingController>(FindObjectsInactive.Include);
                        if (tradingCtrl != null && tradingCtrl.CurrentPosition == TradingController.PositionType.None)
                        {
                            if (signalPhaseTimerMinutes > 3) signalPhaseTimerMinutes = 3;
                        }
                    }
                    break;
            }
        }

        // 새 차트 신호 생성 및 방송
        public void GenerateMarketSignal()
        {
            float rand = UnityEngine.Random.value;
            MarketSignalType type;
            if (rand < 0.35f) type = MarketSignalType.BullishBreakout;
            else if (rand < 0.70f) type = MarketSignalType.BearishBreakout;
            else if (rand < 0.85f) type = MarketSignalType.BullTrap;
            else type = MarketSignalType.BearTrap;

            // 강도 설정 (65% 확률로 Strong, 35% 확률로 Weak)
            SignalStrength strength = UnityEngine.Random.value < 0.65f ? SignalStrength.Strong : SignalStrength.Weak;

            // IsTrueSignal 결정: Breakout은 75% 확률로 진짜, Trap은 100% 가짜 속임수
            bool isTrue = (type == MarketSignalType.BullishBreakout || type == MarketSignalType.BearishBreakout) && UnityEngine.Random.value < 0.75f;

            int duration = strength == SignalStrength.Strong ? UnityEngine.Random.Range(15, 31) : UnityEngine.Random.Range(5, 11);
            int grace = UnityEngine.Random.Range(3, 6); // 3~5분 골든타임 여유 시간

            // 확정 변동률(TargetPercentageDelta) 연산
            float targetDelta = 0f;
            if (strength == SignalStrength.Strong)
            {
                // 강한 신호: ±3.0% ~ ±6.0% (10배 레버리지 기준 ±30%~±60% ROE)
                float mag = UnityEngine.Random.Range(3.0f, 6.0f);
                if (type == MarketSignalType.BullishBreakout) targetDelta = isTrue ? mag : -mag;
                else if (type == MarketSignalType.BearishBreakout) targetDelta = isTrue ? -mag : mag;
                else if (type == MarketSignalType.BullTrap) targetDelta = -mag; // 롱 유도 후 급락 빔
                else if (type == MarketSignalType.BearTrap) targetDelta = mag;  // 숏 유도 후 급등 빔
            }
            else
            {
                // 약한 신호(단타/미끼): ±0.6% ~ ±1.5% (10배 레버리지 기준 ±6%~±15% ROE)
                float mag = UnityEngine.Random.Range(0.6f, 1.5f);
                if (type == MarketSignalType.BullishBreakout) targetDelta = isTrue ? mag : -mag;
                else if (type == MarketSignalType.BearishBreakout) targetDelta = isTrue ? -mag : mag;
                else if (type == MarketSignalType.BullTrap) targetDelta = -mag;
                else if (type == MarketSignalType.BearTrap) targetDelta = mag;
            }

            activeSignal = new MarketSignal
            {
                Type = type,
                Strength = strength,
                IsTrueSignal = isTrue,
                TargetPercentageDelta = targetDelta,
                DurationMinutes = duration,
                GraceMinutes = grace,
                SignalStartPrice = currentPrice
            };

            currentSignalPhase = SignalPhase.GraceWindow;
            signalPhaseTimerMinutes = grace;

            Debug.Log($"[MarketEngine] 📣 [신호 방송 - 1단계 판단 여유 골든타임 돌입] {activeSignal.GetSignalDescription()}");
            OnMarketSignalGenerated?.Invoke(activeSignal);
            OnSignalPhaseChanged?.Invoke(currentSignalPhase, activeSignal);
        }

        // 강제로 특정 차트 신호를 외부(이벤트나 AI 튜닝용)에서 주입하는 함수
        public void ForceInjectSignal(MarketSignalType type, SignalStrength strength, bool isTrue, float targetDelta, int durationMinutes, int graceMinutes = 3)
        {
            activeSignal = new MarketSignal
            {
                Type = type,
                Strength = strength,
                IsTrueSignal = isTrue,
                TargetPercentageDelta = targetDelta,
                DurationMinutes = durationMinutes,
                GraceMinutes = graceMinutes,
                SignalStartPrice = currentPrice
            };

            currentSignalPhase = SignalPhase.GraceWindow;
            signalPhaseTimerMinutes = graceMinutes;
            isExternalEventOverride = true;

            Debug.Log($"[MarketEngine] ⚡ [외부 강제 신호 주입 (이벤트 빔 보장)] {activeSignal.GetSignalDescription()}");
            OnMarketSignalGenerated?.Invoke(activeSignal);
            OnSignalPhaseChanged?.Invoke(currentSignalPhase, activeSignal);
        }

        // 오버도즈 상태 돌입 시 호출되어 주인공을 함정(반대 방향 죽음의 차트 빔)으로 이끄는 신호 주입 및 차트 제어
        public void TriggerOverdoseTrapSignal(TradingController.PositionType trapPosType, int durationSeconds = 35)
        {
            isOverdoseTrapOverride = true;
            overdoseTrapPositionType = trapPosType;
            overdoseTrapEndTime = Time.time + durationSeconds;

            // 함정 신호 방송 (주인공 AI가 완벽한 기회로 착각하도록 과장된 가짜 신호 주입)
            MarketSignalType trapSigType = trapPosType == TradingController.PositionType.Long 
                ? MarketSignalType.BullishBreakout : MarketSignalType.BearishBreakout;
            float trapTargetDelta = trapPosType == TradingController.PositionType.Long ? 180f : -180f; // 엄청난 대각선 상승/하락 착각 유도

            activeSignal = new MarketSignal
            {
                Type = trapSigType,
                Strength = SignalStrength.Strong,
                IsTrueSignal = false,
                TargetPercentageDelta = trapTargetDelta,
                DurationMinutes = Mathf.Max(12, Mathf.CeilToInt(durationSeconds / 2.5f)),
                GraceMinutes = 0,
                SignalStartPrice = currentPrice
            };

            currentSignalPhase = SignalPhase.GuaranteedOverride;
            signalPhaseTimerMinutes = activeSignal.DurationMinutes;

            Debug.Log($"[MarketEngine] 🩸 [Overdose 함정 신호 가동] AI가 {trapPosType} 방향 확실한 대박 기회로 착각! ({durationSeconds}초간 반대 방향 청산 빔 발동)");
            OnMarketSignalGenerated?.Invoke(activeSignal);
            OnSignalPhaseChanged?.Invoke(currentSignalPhase, activeSignal);
        }

        public void CancelOverdoseTrapSignal()
        {
            if (isOverdoseTrapOverride)
            {
                isOverdoseTrapOverride = false;
                overdoseTrapEndTime = -1f;
                currentSignalPhase = SignalPhase.None;
                minutesUntilNextSignal = 3;
                Debug.Log("[MarketEngine] 💊 [Overdose 차트 함정 해제] 플레이어의 멘탈 회복 조치로 죽음의 차트 빔이 해제되고 정상 차트로 복귀합니다.");
            }
        }
    }
}
