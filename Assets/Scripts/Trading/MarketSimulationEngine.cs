using System;
using System.Collections.Generic;
using UnityEngine;

namespace FXOverdose.Trading
{
    public class MarketSimulationEngine : MonoBehaviour
    {
        private bool p2pExternalMode;
        public void EnableP2PExternalMode()=>p2pExternalMode=true;
        public void PrepareP2PChartHistory(int seed,float authoritativePrice,int minutesCount=150)
        {
            EnsureCandleHistoriesInitialized();
            foreach(List<CandleData> history in candleHistories.Values)history.Clear();
            liveAggregatedCandles.Clear();

            // 모든 참가자가 동일한 시드로 같은 과거 차트를 만들되 마지막 종가는 호스트 시작가와 맞닿게 합니다.
            uint state=unchecked((uint)seed)|1u;
            float Next(float min,float max)
            {
                state^=state<<13;state^=state>>17;state^=state<<5;
                return min+(max-min)*(state/(float)uint.MaxValue);
            }

            var reversed=new List<CandleData>(minutesCount);
            float close=authoritativePrice;
            for(int i=0;i<minutesCount;i++)
            {
                float change=Next(-0.004f,0.004f);
                float open=close/Mathf.Max(0.01f,1f+change);
                float high=Mathf.Max(open,close)*(1f+Next(0f,0.0032f));
                float low=Mathf.Min(open,close)*(1f-Next(0f,0.0032f));
                float body=Mathf.Abs(close-open),range=Mathf.Max(0.01f,high-low);
                float volume=Mathf.Max(40f,range*Next(4.5f,7.5f)+body*Next(6f,11f));
                reversed.Add(new CandleData(-1-i,open,high,low,close,volume));
                close=open;
            }
            reversed.Reverse();
            candleHistories[Timeframe.M1].AddRange(reversed);

            foreach(Timeframe tf in Enum.GetValues(typeof(Timeframe)))
            {
                if(tf==Timeframe.M1)continue;
                int size=(int)tf;
                var bucket=new List<CandleData>();
                long bucketStart=long.MinValue;
                foreach(CandleData candle in reversed)
                {
                    long start=Mathf.FloorToInt(candle.timestampMinutes/(float)size)*size;
                    if(bucket.Count>0&&start!=bucketStart)
                    {
                        candleHistories[tf].Add(CandleData.Aggregate(bucket,bucketStart));
                        bucket.Clear();
                    }
                    bucketStart=start;bucket.Add(candle);
                }
                if(bucket.Count>0)candleHistories[tf].Add(CandleData.Aggregate(bucket,bucketStart));
            }

            currentTotalMinutes=0;
            currentPrice=authoritativePrice;
            ouCenterPrice=authoritativePrice;
            current24hHigh=authoritativePrice;
            current24hLow=authoritativePrice;
            current24hVolume=0f;
            StartNewLiveCandle(authoritativePrice);
            IsDataPrepared=true;
            OnEngineReset?.Invoke();
        }
        public void ApplyP2PExternalTick(float price,float bid,float ask,float volume,float snapshotHigh,float snapshotLow)
        {
            if(!IsDataPrepared)return;currentPrice=price;currentBidPrice=bid;currentAskPrice=ask;currentSpread=Mathf.Max(0,ask-bid);
            UpdateLiveCandlesWithTick(price,Mathf.Max(0,volume));

            // 패킷 사이에 발생한 틱을 모두 받지 못해도 호스트가 집계한 1분봉 고가/저가는 보존합니다.
            // 마지막 가격만 넣으면 캔들 꼬리가 사라지고 클라이언트마다 차트 모양이 달라집니다.
            if(liveM1Candle!=null)
            {
                liveM1Candle.high=Mathf.Max(liveM1Candle.high,snapshotHigh);
                liveM1Candle.low=Mathf.Min(liveM1Candle.low,snapshotLow);
            }
            foreach(CandleData candle in liveAggregatedCandles.Values)
            {
                candle.high=Mathf.Max(candle.high,snapshotHigh);
                candle.low=Mathf.Min(candle.low,snapshotLow);
            }
            OnPriceUpdated?.Invoke(price);
        }
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
        [SerializeField] private float currentBidPrice;
        [SerializeField] private float currentAskPrice;
        [SerializeField] private float currentSpread;
        [SerializeField] private float current24hHigh;
        [SerializeField] private float current24hLow;
        [SerializeField] private float current24hVolume;
        [SerializeField] private MarketRegime currentRegime = MarketRegime.Sideways;
        [SerializeField] private float currentVolatility = 0.002f;

        [SerializeField] private MarketRegime currentDailyRegime = MarketRegime.Sideways;
        private int lastUpdatedDay = -1;

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

        // 오버드라이브 연출 상태 변수
        private int currentOverdriveWaveStyle = 0; // 0: 자잘한 요동, 1: 큰 눌림목

        [Header("확정 신호 궤적 (SIG-B1) — 비워 두면 내장 궤적(TrajectoryLibrary)을 씁니다")]
        [Tooltip("가짜 이벤트 빔(트랩)용 궤적")]
        [SerializeField] private List<TrajectoryProfile> trapTrajectories = new List<TrajectoryProfile>();
        [Tooltip("정상 확정 경로용 궤적 (진짜 신호·AI 신호 전반)")]
        [SerializeField] private List<TrajectoryProfile> pathTrajectories = new List<TrajectoryProfile>();

        // GuaranteedOverride 진입 시 1회 추첨합니다. 시간 비틀림이 예전 트랩 분할 지점 지터(SIG-B2)를 일반화합니다.
        private TrajectoryProfile activeTrajectory;
        private float activeTrajectoryWarp = 1f;

        // 확정 신호 경로를 따라가게 하는 평균 회귀 강도 (SIG-B3). 국면별 ouTheta(0.01~0.15)를 쓰지 않는 이유는
        // 국면에 따라 경로 추종력이 15배까지 달라지기 때문입니다. 0.1이면 편차의 표준편차가 노이즈의 약 2배 수준에서 안정됩니다.
        private const float SignalPathOuTheta = 0.1f;

        // 확정 신호 구간의 거래량 배수 (평시 틱 변동폭 기준). 거래량 막대로 신호 진위를 읽을 수 있게 합니다. (SIG-B5)
        private const float TrueSignalVolumeMultiplier = 2.5f;
        private const float FalseSignalVolumeMultiplier = 0.7f;

        public bool IsOverdoseTrapOverride => isOverdoseTrapOverride;
        public bool IsMarketOpen { get; private set; } = false;
        public bool IsDataPrepared { get; private set; } = false;
        public bool IsFastForwarding => gameManager != null && gameManager.IsFastForwardingTime;
        public bool IsOverridingTrend => isExternalEventOverride || isOverdoseTrapOverride || (currentSignalPhase != SignalPhase.None && currentSignalPhase != SignalPhase.Cooldown);

        public event Action<MarketSignal> OnMarketSignalGenerated;
        public event Action<SignalPhase, MarketSignal> OnSignalPhaseChanged;

        [Header("Day-Based Difficulty Scaling (Phase 4)")]
        // 유동성 사냥 꼬리의 길이·발생 확률 배수입니다. 예전 이름(dayVolatilityMultiplier)과 달리
        // 틱 변동성(targetVol)에는 곱해지지 않습니다 — 일차가 올라도 실제 변동성은 그대로입니다.
        [SerializeField] private float sweepIntensityMultiplier = 1.0f;
        // 틱 갱신 주기만 촘촘하게 만드는 연출 노브입니다. 틱당 분산도 같은 비율로 줄어
        // 분당 실현 변동성은 그대로입니다(σ√(0.2/I) × √(5I) = σ). 난이도가 아니라 체감용입니다.
        [SerializeField] private float tickInstability = 1.0f; // 1.0 = normal, 10.0 = extremely shaky
        [SerializeField] private float fakeoutProbability = 0.0f;
        [SerializeField] private int slippageRange = 0; // Number of ticks offset
        [SerializeField] private bool isServerLagging = false;
        private float serverLagTimer = 0f;
        private float accumulatedLagPriceDelta = 0f;
        private float accumulatedLagVolume = 0f;

        public bool IsServerLagging => isServerLagging;
        public int SlippageRange => slippageRange;

        public void OpenMarketAfterLoading()
        {
            if (IsMarketOpen)
            {
                return;
            }

            IsMarketOpen = true;
            minutesUntilNextSignal = 3; // 개장 후 3분(3초) 뒤 첫 거래 신호 발생
            Debug.Log("[MarketSimulationEngine] 📈 로딩 및 AI 개장 대사 출력 완료 -> 시장 개장! 주가 차트 시뮬레이션 및 AI 실시간 매매가 시작됩니다.");
        }

        // 테스트 및 디버그용 수동 신호 발행 Helper
        public void TriggerSignalForTest(MarketSignal signal)
        {
            if (signal.Direction == TradingController.PositionType.None) signal.Direction = MarketSignal.AdvertisedDirectionOf(signal.Type);
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
        public float CurrentBidPrice => currentBidPrice;
        public float CurrentAskPrice => currentAskPrice;
        public float CurrentSpread => currentSpread;
        public float Current24hHigh => current24hHigh;
        public float Current24hLow => current24hLow;
        public float Current24hVolume => current24hVolume;
        public MarketRegime CurrentRegime => currentRegime;

        /// <summary>오늘의 거시 방향성. 요미의 힌트가 참조하는 값입니다.</summary>
        public MarketRegime CurrentDailyRegime => currentDailyRegime;

        // 가격이나 캔들이 갱신될 때 UI 및 트레이딩 컨트롤러에 알리는 이벤트
        public event Action<float> OnPriceUpdated;
        public event Action<Timeframe, CandleData> OnCandleClosed;
        public event Action OnEngineReset;

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

        private bool wasLoaded = false;

        public void CaptureSaveData(FXOverdose.Core.SaveData data)
        {
            data.CurrentChartPrice = currentPrice;
            data.Current24hHigh = current24hHigh;
            data.Current24hLow = current24hLow;
            data.Current24hVolume = current24hVolume;
            data.CurrentRegime = currentRegime;
            data.CurrentDailyRegime = currentDailyRegime;

            // 로드 후 일일 국면이 재추첨되어 덮어써지는 것을 막습니다. (SV-B3)
            data.MarketLastUpdatedDay = lastUpdatedDay;
            // 이벤트 빔이 잡아 둔 국면 유지 시간과 캔들 시간축 기준점. (SV-B4 / SV-B5)
            data.MinutesUntilNextRegimeChange = minutesUntilNextRegimeChange;
            data.MarketTotalMinutes = currentTotalMinutes;

            data.ChartHistories.Clear();
            data.FlatChartHistories.Clear();
            data.FlatLiveCandles.Clear();
            foreach (var kvp in candleHistories)
            {
                var th = new FXOverdose.Core.TimeframeHistory { timeframe = kvp.Key, candles = new List<FXOverdose.Core.SavedCandle>() };
                foreach (var c in kvp.Value)
                {
                    th.candles.Add(new FXOverdose.Core.SavedCandle { timestampMinutes = c.timestampMinutes, open = c.open, high = c.high, low = c.low, close = c.close, volume = c.volume });
                    data.FlatChartHistories.Add(new FXOverdose.Core.FlatSavedCandle { timeframe = kvp.Key, timestampMinutes = c.timestampMinutes, open = c.open, high = c.high, low = c.low, close = c.close, volume = c.volume });
                }
                if (liveAggregatedCandles.TryGetValue(kvp.Key, out CandleData liveC))
                {
                    th.liveCandle = new FXOverdose.Core.SavedCandle { timestampMinutes = liveC.timestampMinutes, open = liveC.open, high = liveC.high, low = liveC.low, close = liveC.close, volume = liveC.volume };
                    data.FlatLiveCandles.Add(new FXOverdose.Core.FlatSavedCandle { timeframe = kvp.Key, timestampMinutes = liveC.timestampMinutes, open = liveC.open, high = liveC.high, low = liveC.low, close = liveC.close, volume = liveC.volume });
                }
                data.ChartHistories.Add(th);
            }
        }

        public void RestoreFromSaveData(FXOverdose.Core.SaveData data)
        {
            // 거래를 한 번도 시작하지 않은 세이브는 차트가 비어 있습니다.
            // 스토리 새 게임은 요미의 방에서 시작하는데 그 씬엔 이 엔진이 없어
            // CaptureSaveData가 건너뛰어지고 CurrentChartPrice = 0이 그대로 디스크에 남습니다.
            //
            // 그 0을 복원하면 OU 평균 회귀 항이 (0-0)/0 = NaN이 되고, 최저가 방어는
            // NaN 비교가 항상 false라 이를 잡지 못해 가격이 NaN으로 고착됩니다.
            // 결과는 캔들 0개 + 손익/잔고 전부 NaN입니다.
            //
            // wasLoaded를 세우지 않고 빠져나가면 Start()가 정상 ResetEngine을 수행합니다.
            // 이미 Start()가 지나갔다면 초기화된 정상 가격이 그대로 유지되므로 호출 순서와 무관하게 안전합니다.
            if (!(data.CurrentChartPrice > 0f))
            {
                Debug.LogWarning("[MarketSimulationEngine] 세이브에 차트 데이터가 없어(가격 0) 복원을 건너뛰고 정상 초기화로 진행합니다.");
                return;
            }

            wasLoaded = true;
            currentPrice = data.CurrentChartPrice;
            ouCenterPrice = data.CurrentChartPrice;
            current24hHigh = data.Current24hHigh;
            current24hLow = data.Current24hLow;
            current24hVolume = data.Current24hVolume;
            currentRegime = data.CurrentRegime;
            currentDailyRegime = data.CurrentDailyRegime;

            // 복원한 일일 국면이 첫 Update에서 재추첨되지 않도록 갱신 일차를 함께 되돌립니다. (SV-B3)
            lastUpdatedDay = data.MarketLastUpdatedDay > 0 ? data.MarketLastUpdatedDay : data.CurrentDay;
            minutesUntilNextRegimeChange = data.MinutesUntilNextRegimeChange > 0 ? data.MinutesUntilNextRegimeChange : 60;
            currentTotalMinutes = data.MarketTotalMinutes;

            // 로드 시 진행 중이던 신호(이벤트)는 activeSignal 객체가 없으므로 None으로 안전하게 초기화
            ResetTransientMarketState();

            candleHistories.Clear();
            liveAggregatedCandles.Clear();
            
            // 신버전: FlatChartHistories에서 복구
            if (data.FlatChartHistories != null && data.FlatChartHistories.Count > 0)
            {
                foreach (var fc in data.FlatChartHistories)
                {
                    if (!candleHistories.ContainsKey(fc.timeframe))
                    {
                        candleHistories[fc.timeframe] = new List<CandleData>();
                    }
                    candleHistories[fc.timeframe].Add(new CandleData(fc.timestampMinutes, fc.open, fc.high, fc.low, fc.close, fc.volume));
                }
                
                if (data.FlatLiveCandles != null)
                {
                    foreach (var flc in data.FlatLiveCandles)
                    {
                        liveAggregatedCandles[flc.timeframe] = new CandleData(flc.timestampMinutes, flc.open, flc.high, flc.low, flc.close, flc.volume);
                    }
                }
            }
            else // 구버전 호환 (작동 안할 가능성 높음)
            {
                foreach (var th in data.ChartHistories)
                {
                    var list = new List<CandleData>();
                    if (th.candles != null)
                    {
                        foreach (var c in th.candles)
                        {
                            list.Add(new CandleData(c.timestampMinutes, c.open, c.high, c.low, c.close, c.volume));
                        }
                    }
                    candleHistories[th.timeframe] = list;
                    liveAggregatedCandles[th.timeframe] = new CandleData(th.liveCandle.timestampMinutes, th.liveCandle.open, th.liveCandle.high, th.liveCandle.low, th.liveCandle.close, th.liveCandle.volume);
                }
            }
            
            liveAggregatedCandles.TryGetValue(Timeframe.M1, out liveM1Candle);

            // 요미의 방에서 하루를 넘기면 이 엔진이 없는 채로 일차만 바뀝니다. 그 세이브는 어제 차트를
            // 그대로 들고 있으므로 여기서 다음 날로 넘깁니다. GameScene에서 넘긴 경우는 RollOverToNewDay가
            // 이미 MarketLastUpdatedDay를 새 일차로 맞춰 저장했으므로 이 조건에 걸리지 않습니다.
            if (data.MarketLastUpdatedDay > 0 && data.MarketLastUpdatedDay < data.CurrentDay)
            {
                RollOverToNewDay(data.CurrentDay);
            }
            else if (liveM1Candle == null)
            {
                StartNewLiveCandle(currentPrice);
            }

            Debug.Log("[MarketSimulationEngine] 차트 히스토리 및 현재 가격 복구 완료.");
        }

        private void Start()
        {
            EnsureCandleHistoriesInitialized();
            if (gameManager == null)
            {
                gameManager = GameManager.Instance;
            }

            if (gameManager != null)
            {
                gameManager.OnGameMinuteAdvanced += OnGameMinuteAdvanced;
                gameManager.OnFastForwardEnded += HandleFastForwardEnded;
            }

            if (!wasLoaded)
            {
                ResetEngine(initialPrice);
            }
            else
            {
                Debug.Log("[MarketSimulationEngine] 세이브 로드로 인해 ResetEngine(초기화)을 건너뜁니다.");
                IsDataPrepared = true;
            }
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


            var tradingCtrl = UnityEngine.Object.FindAnyObjectByType<TradingController>(FindObjectsInactive.Include);
            if (tradingCtrl != null && tradingCtrl.CurrentPosition == TradingController.PositionType.None)
            {
                currentSignalPhase = SignalPhase.None;
                // 페이즈를 None으로 되돌리면 UpdateSignalSystem의 해제 지점(GuaranteedOverride/Cooldown 전이)에
                // 영영 도달하지 못합니다. 여기서 같이 내리지 않으면 스프레드 ×5와 세션 변동성·오더블록 정지가
                // 다음 날 ResetEngine까지 그대로 남습니다.
                isExternalEventOverride = false;
                minutesUntilNextSignal = UnityEngine.Random.Range(3, 6);
                Debug.Log($"[MarketEngine] 🚀 스킬 업그레이드(고속 시간 패스) 완료 -> 업그레이드된 새 스킬 능력치 반영을 위해 {minutesUntilNextSignal}분(초) 후 신규 거래 신호가 발행됩니다.");
            }
        }

        private const long MinutesPerDay = 1440;

        /// <summary>
        /// 진행 중이던 신호·오버라이드·오버도즈 함정·서버 렉 같은 일시 상태를 비웁니다.
        /// 새 게임(ResetEngine)·불러오기(RestoreFromSaveData)·하루 넘김(RollOverToNewDay) 세 경로가 공유합니다.
        /// 예전에는 경로마다 목록을 따로 들고 있어 한쪽에만 빠진 항목이 버그가 됐습니다
        /// (서버 렉을 남기면 불러오기 직후 차트가 몇 초간 멈춘 채 시작합니다).
        /// </summary>
        private void ResetTransientMarketState()
        {
            currentSignalPhase = SignalPhase.None;
            signalPhaseTimerMinutes = 0;
            isExternalEventOverride = false;
            isOverdoseTrapOverride = false;
            overdoseTrapEndTime = -1f;
            isServerLagging = false;
            serverLagTimer = 0f;
            accumulatedLagPriceDelta = 0f;
            accumulatedLagVolume = 0f;
        }

        /// <summary>
        /// 하루가 넘어갈 때 차트를 <b>이어 붙입니다</b>. 가격과 캔들 히스토리는 그대로 두고,
        /// 거래가 있었던 진행 중 캔들을 마감한 뒤 시간축을 다음 1440분 경계로 옮깁니다.
        /// 그래서 어제 종가가 오늘 시가가 되고, D1 캔들이 하루에 하나씩 쌓입니다.
        ///
        /// 밤사이(24:00~09:00)는 시뮬레이션하지 않으므로 가격 공백 없이 그대로 이어집니다.
        /// GameScene에서 정산하면 GameManager가, 요미의 방에서 정산하면 다음 GameScene 진입 시
        /// RestoreFromSaveData가 호출합니다.
        /// </summary>
        public void RollOverToNewDay(int newDay)
        {
            if (p2pExternalMode) return;
            EnsureCandleHistoriesInitialized();

            // 24:00 마지막 분에 새로 열린 빈 캔들(거래량 0)은 버리고, 거래가 있었던 캔들만 마감합니다.
            if (liveM1Candle != null && liveM1Candle.volume > 0f) FinalizeCandle(Timeframe.M1, liveM1Candle);
            foreach (Timeframe tf in Enum.GetValues(typeof(Timeframe)))
            {
                if (tf == Timeframe.M1) continue;
                if (liveAggregatedCandles.TryGetValue(tf, out CandleData live) && live != null && live.volume > 0f)
                {
                    FinalizeCandle(tf, live);
                }
            }
            liveAggregatedCandles.Clear();

            // 이미 경계 위에 있어도 반드시 한 칸 넘깁니다. 그대로 두면 같은 타임스탬프의 D1이 두 번 생깁니다.
            currentTotalMinutes = (currentTotalMinutes / MinutesPerDay + 1) * MinutesPerDay;

            IsMarketOpen = false;   // GameManager가 OpenMarketAfterLoading으로 다시 엽니다
            tickTimer = 0f;
            ResetTransientMarketState();
            minutesUntilNextSignal = 3;
            ouCenterPrice = currentPrice;
            current24hHigh = currentPrice;
            current24hLow = currentPrice;
            current24hVolume = 0f;

            // 새 날의 거시 기조를 지금 확정합니다. 이래야 직후 저장되는 MarketLastUpdatedDay가 새 일차를 가리켜
            // 다음 불러오기에서 하루를 한 번 더 넘기지 않습니다.
            UpdateDailyDifficulty(newDay);

            StartNewLiveCandle(currentPrice);
            IsDataPrepared = true;
            Debug.Log($"[MarketEngine] 📅 {newDay}일차로 차트를 이어 붙였습니다. 시가 ${currentPrice:N1} (시간축 {currentTotalMinutes}분)");
            OnEngineReset?.Invoke();
        }

        public void ResetEngine(float startPrice)
        {
            EnsureCandleHistoriesInitialized();
            IsDataPrepared = false;
            IsMarketOpen = false;
            currentPrice = startPrice;
            ouCenterPrice = startPrice;
            current24hHigh = startPrice;
            current24hLow = startPrice;
            current24hVolume = 0f;
            currentTotalMinutes = 0;
            tickTimer = 0f;
            ResetTransientMarketState();
            // 💡 [AI 매매 실시간 검증 최적화] 게임 시작 후 단 3초(3분봉) 만에 첫 매매 신호가 발생하여 주인공 AI가 즉시 판단 및 매매를 개시하도록 설정
            minutesUntilNextSignal = 3;

            foreach (var list in candleHistories.Values)
            {
                list.Clear();
            }
            liveAggregatedCandles.Clear();

            // 게임 시작 전 과거 150분(2시간 반) 데이터 Pre-warm 생성 및 최종 시뮬레이션 마감 종가를 현재 주가로 동기화
            float prewarmedEndPrice = PrewarmHistoricalCandles(150, startPrice);
            currentPrice = prewarmedEndPrice;
            ouCenterPrice = prewarmedEndPrice;

            // 첫 실시간 1분봉 열기 (과거 캔들의 마지막 종가와 정확히 맞닿아 갭 없이 연결)
            StartNewLiveCandle(currentPrice);
            IsDataPrepared = true;
            OnEngineReset?.Invoke();
        }

        private void Update()
        {
            if(p2pExternalMode)return;
            if (gameManager == null || gameManager.CurrentState != GameManager.GameState.Playing || !IsMarketOpen)
            {
                return;
            }

            UpdateDailyDifficulty(gameManager.CurrentDay);

            // Server Lag Gimmick (Priority 4: Only if no Overdose, FastForward, or Event Override)
            if (!isOverdoseTrapOverride && !IsFastForwarding && !isExternalEventOverride && gameManager.CurrentDay >= 16)
            {
                // 매우 낮은 확률로 발동 (대략 1시간에 1번꼴)
                if (!isServerLagging && UnityEngine.Random.value < 0.05f * Time.deltaTime) 
                {
                    isServerLagging = true;
                    serverLagTimer = UnityEngine.Random.Range(3.0f, 5.0f);
                    accumulatedLagPriceDelta = 0f;
                    accumulatedLagVolume = 0f;
                    Debug.Log("[MarketEngine] ⚠️ 서버 렉 발생! 3~5초간 차트 업데이트가 정지됩니다.");
                }
            }

            if (isServerLagging)
            {
                serverLagTimer -= Time.deltaTime;
                if (serverLagTimer <= 0f)
                {
                    isServerLagging = false;
                    Debug.Log("[MarketEngine] ⚡ 서버 렉 복구 완료! 밀린 차트가 한 번에 갱신됩니다.");
                    currentPrice += accumulatedLagPriceDelta;
                    // 긍정 조건을 부정하는 형태여야 NaN도 걸립니다. (currentPrice < 10f 는 NaN에서 false)
                    if (!(currentPrice >= 10f)) currentPrice = 10f;
                    UpdateLiveCandlesWithTick(currentPrice, accumulatedLagVolume);
                    OnPriceUpdated?.Invoke(currentPrice);
                    accumulatedLagPriceDelta = 0f;
                    accumulatedLagVolume = 0f;
                }
            }

            // 1초마다 주가 틱이 변동될 때 발생 (명세서 L101 기준. 1분 = 5초 속도 기준 1분당 5틱 유지)
            // 틱 불안정성(TickInstability)에 따라 갱신 주기를 단축시켜 차트를 요동치게 만듭니다.
            float secondsPerMinute = gameManager != null ? gameManager.SecondsPerGameMinute : 5.0f;
            float baseTickInterval = Mathf.Clamp(secondsPerMinute / 5.0f, 0.05f, 1.0f);
            float tickInterval = baseTickInterval / Mathf.Max(1.0f, tickInstability);

            tickTimer += Time.deltaTime;
            while (tickTimer >= tickInterval)
            {
                tickTimer -= tickInterval;
                SimulateTickMovement(tickInterval);
            }
        }

        public void UpdateDailyDifficulty(int currentDay)
        {
            if (lastUpdatedDay != currentDay)
            {
                lastUpdatedDay = currentDay;
                DetermineDailyRegime(currentDay);
            }

            float effectiveDay = Mathf.Min(currentDay, 25f); // 25일차에서 캡

            if (currentDay <= 5)
            {
                sweepIntensityMultiplier = 1.0f;
                tickInstability = 1.0f;
                fakeoutProbability = 0.0f;
                slippageRange = 0;
            }
            else if (currentDay <= 10)
            {
                sweepIntensityMultiplier = 1.2f;
                tickInstability = 1.5f;
                fakeoutProbability = 0.1f;
                slippageRange = 0;
            }
            else if (currentDay <= 15)
            {
                sweepIntensityMultiplier = 1.5f;
                tickInstability = 3.0f;
                fakeoutProbability = 0.3f;
                slippageRange = 3;
            }
            else
            {
                sweepIntensityMultiplier = 2.0f + (effectiveDay - 16) * 0.15f;
                tickInstability = 5.0f + (effectiveDay - 16) * 1.0f;
                fakeoutProbability = 0.5f;
                slippageRange = 5;
            }
        }

        /// <summary>
        /// 엔진은 더 이상 일일 국면을 추첨하지 않습니다. DailyMarketOutlook이 결정한 값을 소비할 뿐입니다.
        /// 요미의 방에서 힌트로 먼저 결정됐다면 그 값이 그대로 내려옵니다. (SV-B8)
        /// </summary>
        private void DetermineDailyRegime(int currentDay)
        {
            currentDailyRegime = DailyMarketOutlook.GetOrRoll(currentDay);
            Debug.Log($"[MarketEngine] 📅 일일 마켓 분위기(Daily Regime) 적용: {currentDailyRegime}" +
                      (DailyMarketOutlook.Revealed ? " (요미가 예고한 방향)" : string.Empty));
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
                    targetVol = 0.0035f; // 변동성 상향
                    ouTheta = 0.02f;
                    break;
                case MarketRegime.Bear:
                    drift = -0.0004f;
                    targetVol = 0.0045f; // 변동성 상향
                    ouTheta = 0.02f;
                    break;
                case MarketRegime.Sideways:
                    drift = 0f;
                    targetVol = 0.0025f; // 변동성 상향
                    ouTheta = 0.15f; // 박스권 강한 회귀
                    break;
                case MarketRegime.Squeeze:
                    drift = UnityEngine.Random.Range(-0.0008f, 0.0008f);
                    targetVol = 0.012f; // 광기 변동성 대폭 상향
                    ouTheta = 0.01f;
                    break;
            }

            // 💡 [자연스러운 차트 파동 생성] 고정된 Drift로 인해 차트가 일직선으로 그려지는 것을 방지하기 위해 실시간 단기 파동(Sine Wave)을 결합합니다.
            float timeSec = Time.time;
            float waveCycle1 = ((currentTotalMinutes * 60f + timeSec) % 350f) / 350f * Mathf.PI * 2f;
            float waveCycle2 = ((currentTotalMinutes * 60f + timeSec) % 130f) / 130f * Mathf.PI * 2f;
            float waveCycle3 = (timeSec % 15f) / 15f * Mathf.PI * 2f; // 초단기 미세 파동 추가 (현실감 부여)
            
            float waveDrift = (Mathf.Sin(waveCycle1) * 0.0004f) + (Mathf.Cos(waveCycle2) * 0.0002f) + (Mathf.Sin(waveCycle3) * 0.00015f);
            
            // 고속 스킵 중에는 랜덤성을 더 부여하여 일직선 패턴 완전 타파
            if (IsFastForwarding)
            {
                waveDrift += UnityEngine.Random.Range(-0.0003f, 0.0003f);
            }

            float macroDrift = 0f;
            if (currentDailyRegime == MarketRegime.Bull) macroDrift = 0.00015f;
            else if (currentDailyRegime == MarketRegime.Bear) macroDrift = -0.00015f;
            else if (currentDailyRegime == MarketRegime.Squeeze) macroDrift = UnityEngine.Random.Range(-0.0003f, 0.0003f);

            // 🌟 [Realistic Feature 3] 세션(Session) 기반 시장 성격 변화
            float sessionVolMultiplier = 1.0f;
            float activeFakeoutProb = fakeoutProbability;
            if (gameManager != null && !isOverdoseTrapOverride && !IsOverridingTrend)
            {
                int h = gameManager.CurrentHour;
                if (h >= 0 && h < 8) // 아시아장: 거래량/변동성 감소, 횡보 강함
                {
                    sessionVolMultiplier = 0.5f;
                    activeFakeoutProb = Mathf.Max(0.05f, fakeoutProbability * 0.5f);
                }
                else if (h >= 8 && h < 16) // 런던장: 변동성 증가 시작
                {
                    sessionVolMultiplier = 1.2f;
                }
                else // 뉴욕장 (16~24): 최고 변동성, 휩쏘 및 돌파 빈도 증가
                {
                    sessionVolMultiplier = 2.0f;
                    activeFakeoutProb = Mathf.Min(0.85f, fakeoutProbability * 1.5f);
                }
            }
            targetVol *= sessionVolMultiplier;

            drift += waveDrift + macroDrift;

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
                // 1단계 판단 여유 시간: 너무 굳어있지 않게 노이즈를 40% 수준으로 살리고 횡보 유지 (골든타임 예고 방송 및 대기)
                stochasticNoise *= 0.40f;
                drift = 0f;
            }
            else if (currentSignalPhase == SignalPhase.GuaranteedOverride)
            {
                // 2단계 확정적 주가 제어 구간: 너무 정직한 일직선 이동을 방지하고 현실적인 흔들림을 주입
                // Wave Style 0: 자잘하게 요동치며 꾸준히 이동 (stochasticNoise 크게 증폭)
                // Wave Style 1: 큰 눌림목(파동)을 형성하며 이동 (stochasticNoise 약간 증폭, 주기 긴 강한 사인파 결합)
                
                if (!isExternalEventOverride && activeSignal.IsTrueSignal)
                {
                    // 일반 스킬(AI) 확정 수익 구간: 노이즈를 대폭 억제하여 좁은 스탑로스가 터지지 않게 보호
                    stochasticNoise *= 0.15f; 
                }
                else
                {
                    if (currentOverdriveWaveStyle == 0)
                    {
                        stochasticNoise *= 1.5f; // 기존 0.35f에서 대폭 상향하여 음봉/양봉 섞임 유도
                        drift += Mathf.Sin(Time.time * 2.5f) * 0.00015f + Mathf.Cos(Time.time * 5.0f) * 0.0001f;
                    }
                    else
                    {
                        stochasticNoise *= 0.8f;
                        // 주기 20~30초 가량의 꽤 큰 역추세 파동 형성
                        drift += Mathf.Sin(Time.time * 0.5f) * 0.0006f + Mathf.Cos(Time.time * 0.2f) * 0.0003f;
                    }
                }

                // 궤적 (SIG-B1): 이번 1분 동안의 진행률 변화량이 드리프트, 노이즈 커브가 흔들림 배수입니다.
                // 분마다 P(t+1/D) − P(t)를 쓰면 합이 망원급수라 총 이동량이 "목표 변동률 × progress(1)"로 정확히 보존됩니다.
                TrajectoryProfile trajectory = activeTrajectory != null ? activeTrajectory : TrajectoryLibrary.Paths[0];
                float totalDuration = Mathf.Max(1f, activeSignal.DurationMinutes);
                float minuteStep = 1f / totalDuration;
                float elapsedRatio = Mathf.Clamp01(1f - (float)signalPhaseTimerMinutes / totalDuration);
                float midMinuteRatio = Mathf.Min(1f, elapsedRatio + minuteStep * 0.5f); // 이번 1분의 중간 지점
                float pathDriftPerMinute = (activeSignal.TargetPercentageDelta / 100f)
                    * (trajectory.ProgressAt(Mathf.Min(1f, elapsedRatio + minuteStep), activeTrajectoryWarp)
                       - trajectory.ProgressAt(elapsedRatio, activeTrajectoryWarp));
                stochasticNoise *= trajectory.NoiseAt(midMinuteRatio, activeTrajectoryWarp);

                // 트랩은 파동 드리프트를 덮어쓰고(패턴이 그대로 보이도록), 정상 경로는 파동 위에 얹습니다. 예전 동작과 같습니다.
                bool isTrapPath = isExternalEventOverride && !activeSignal.IsTrueSignal;
                if (isTrapPath) drift = pathDriftPerMinute;
                else drift += pathDriftPerMinute;

                // OU 처리 (SIG-B3)
                //  · 꺾임이 핵심인 궤적(내장 트랩 3종 등, trackPathWithOu = false): OU 무력화 — 당기면 패턴이 뭉개집니다
                //  · 그 외: OU를 끄지 않고 중심선을 궤적 위의 현재 지점으로 옮깁니다. 가격이 경로보다 앞서면 당기고
                //    뒤처지면 밀어 눌림목·되돌림이 저절로 생기고, 노이즈 편차가 쌓이지 않아 목표 도달이 안정됩니다.
                //    전역 ouCenterPrice는 건드리지 않습니다 — 신호가 끝난 뒤 평시 회귀의 기준이 어긋나기 때문입니다.
                if (!trajectory.trackPathWithOu || activeSignal.SignalStartPrice <= 0f)
                {
                    ouTerm = 0f;
                }
                else
                {
                    // 가격은 이 1분 동안 P(t) → P(t+1/D)로 움직이므로 중심은 분 중간 지점에 둡니다(평균 지연 0).
                    float pathCenter = activeSignal.SignalStartPrice
                                       * (1f + activeSignal.TargetPercentageDelta / 100f * trajectory.ProgressAt(midMinuteRatio, activeTrajectoryWarp));
                    ouTerm = SignalPathOuTheta * (pathCenter - currentPrice) / currentPrice;
                }
            }

            // 🌟 [Realistic Feature 2] 눈에 보이지 않는 오더블록(저항/지지선) 로직
            // 라운드 피겨(1000단위) 근처에서 저항/지지가 발생 (오버도즈 및 강제 빔 중에는 절대 무시)
            if (!isOverdoseTrapOverride && currentSignalPhase != SignalPhase.GuaranteedOverride && !IsOverridingTrend)
            {
                float roundNumber = Mathf.Round(currentPrice / 1000f) * 1000f;
                if (roundNumber > 10f)
                {
                    float distanceToRound = Mathf.Abs(currentPrice - roundNumber) / currentPrice;
                    if (distanceToRound < 0.002f) // 라운드 피겨 ±0.2% 이내 접근 시
                    {
                        // 튕겨내는 힘 (저항선 역할)
                        float pushBackForce = Mathf.Sign(currentPrice - roundNumber) * 0.1f;
                        // 돌파 모멘텀 (fakeout 확률이 낮을수록 돌파를 잘함)
                        if (UnityEngine.Random.value > (1f - activeFakeoutProb))
                        {
                            ouTerm += pushBackForce;
                        }
                    }
                }
            }

            // 5. 최종 수익률 
            float totalReturn = (drift * dtFraction) + (ouTerm * dtFraction) + stochasticNoise;

            // ⭐ [안전망: 확정 주가 오버드라이브 구간 -25% ROE 청산 방어 (자연스러운 스프링 꼬리 효과)]
            // 주의: 오버도즈 폭주(isOverdoseTrapOverride) 발동 중에는 어떠한 가드도 무시하고 청산(-100%)을 우선시합니다.
            if (currentSignalPhase == SignalPhase.GuaranteedOverride && !isOverdoseTrapOverride && activeSignal.IsTrueSignal)
            {
                var tradingCtrl = UnityEngine.Object.FindAnyObjectByType<TradingController>(FindObjectsInactive.Include);
                if (tradingCtrl != null && tradingCtrl.CurrentPosition != TradingController.PositionType.None)
                {
                    bool isCorrectDirection = (tradingCtrl.CurrentPosition == TradingController.PositionType.Long && activeSignal.TargetPercentageDelta > 0) ||
                                              (tradingCtrl.CurrentPosition == TradingController.PositionType.Short && activeSignal.TargetPercentageDelta < 0);

                    if (isCorrectDirection)
                    {
                        float expectedPrice = currentPrice * (1f + totalReturn);
                        float expectedRoe = 0f;
                    
                    if (tradingCtrl.CurrentPosition == TradingController.PositionType.Long)
                    {
                        expectedRoe = (expectedPrice - tradingCtrl.EntryPrice) / tradingCtrl.EntryPrice * tradingCtrl.CurrentLeverage * 100f;
                        if (expectedRoe < -24f) // -24% 부근부터 강력한 지지/반발 매수세 연출
                        {
                            if (totalReturn < 0) 
                            {
                                // 하락폭을 대폭 줄이고 양수 노이즈(반발 매수)를 더해 꼬리를 형성
                                float dampFactor = Mathf.Clamp01(25f + expectedRoe);
                                totalReturn = totalReturn * dampFactor + Mathf.Abs(stochasticNoise) * 1.5f;
                                expectedPrice = currentPrice * (1f + totalReturn);
                                expectedRoe = (expectedPrice - tradingCtrl.EntryPrice) / tradingCtrl.EntryPrice * tradingCtrl.CurrentLeverage * 100f;
                            }
                            
                            // 절대 하한선 방어: -25% 도달 시 즉시 무작위 꼬리 반등 형성
                            if (expectedRoe < -25f)
                            {
                                float hardLimit = tradingCtrl.EntryPrice * (1f - (25f / (tradingCtrl.CurrentLeverage * 100f)));
                                expectedPrice = hardLimit + (currentPrice * UnityEngine.Random.Range(0.0002f, 0.0008f));
                                totalReturn = (expectedPrice - currentPrice) / currentPrice;
                            }
                        }
                    }
                    else // Short
                    {
                        expectedRoe = (tradingCtrl.EntryPrice - expectedPrice) / tradingCtrl.EntryPrice * tradingCtrl.CurrentLeverage * 100f;
                        if (expectedRoe < -24f) 
                        {
                            if (totalReturn > 0) // Short인데 가격 상승(손실 방향)
                            {
                                // 상승폭을 대폭 줄이고 음수 노이즈(반발 매도)를 더해 꼬리를 형성
                                float dampFactor = Mathf.Clamp01(25f + expectedRoe); 
                                totalReturn = totalReturn * dampFactor - Mathf.Abs(stochasticNoise) * 1.5f;
                                expectedPrice = currentPrice * (1f + totalReturn);
                                expectedRoe = (tradingCtrl.EntryPrice - expectedPrice) / tradingCtrl.EntryPrice * tradingCtrl.CurrentLeverage * 100f;
                            }
                            
                            if (expectedRoe < -25f)
                            {
                                float hardLimit = tradingCtrl.EntryPrice * (1f + (25f / (tradingCtrl.CurrentLeverage * 100f)));
                                expectedPrice = hardLimit - (currentPrice * UnityEngine.Random.Range(0.0002f, 0.0008f));
                                totalReturn = (expectedPrice - currentPrice) / currentPrice;
                            }
                        }
                    }
                    }
                }
            }

            // 6. 가격 변동 적용
            float priceDelta = currentPrice * totalReturn;
            float tickVolume = Mathf.Abs(priceDelta) * UnityEngine.Random.Range(2f, 10f);

            // 확정 신호 구간의 거래량은 신호의 진위를 드러냅니다. (SIG-B5)
            // 기준을 이번 틱의 가격 변화가 아니라 "이 국면의 평시 틱 변동폭"으로 잡습니다. 진짜 신호 구간은
            // 노이즈를 ×0.15로 억제하므로 틱당 변화가 평시보다 작아, 가격 변화에 배수를 곱하면 오히려 평시만 못합니다.
            //  · 진짜: 평시의 2.5배 — 돌파에 거래량이 실림
            //  · 가짜: 평시의 0.7배 — 가격은 크게 움직여도 거래량이 마름
            // 오버도즈 함정은 AI가 확실한 기회로 착각하게 만드는 연출이라 제외합니다.
            if (currentSignalPhase == SignalPhase.GuaranteedOverride && !isOverdoseTrapOverride)
            {
                float typicalTickMove = currentPrice * targetVol * Mathf.Sqrt(dtFraction);
                float volumeBasis = activeSignal.IsTrueSignal
                    ? Mathf.Max(Mathf.Abs(priceDelta), typicalTickMove) * TrueSignalVolumeMultiplier
                    : typicalTickMove * FalseSignalVolumeMultiplier;
                tickVolume = volumeBasis * UnityEngine.Random.Range(2f, 10f);
            }

            if (isServerLagging)
            {
                // 서버 렉 중에는 화면을 멈추고 변동분만 쌓습니다. 렉이 풀리는 순간 Update()의 복구
                // 블록(isServerLagging 해제부)이 누적분을 한 번에 반영합니다.
                // 이 누적이 비어 있던 동안은 가격이 그대로 흘러 "차트 정지" 연출이 아예 없었고,
                // 주문 버튼만 막혀 손절도 못 하는 구간이 됐습니다.
                // ponytail: 렉 구간 동안 복리를 무시하고 델타를 단순 합산합니다(3~5초, 약 15~25틱).
                //           체감 차이가 없고, 필요해지면 렉 시작가 기준 누적 수익률로 바꾸면 됩니다.
                accumulatedLagPriceDelta += priceDelta;
                accumulatedLagVolume += tickVolume;
            }
            else
            {
                currentPrice += priceDelta;
                // 최저가 방어. 긍정 조건을 부정하는 형태여야 NaN도 걸립니다.
                // (currentPrice < 10f 는 NaN에서 false라 NaN 가격을 그대로 통과시켰습니다)
                if (!(currentPrice >= 10f)) currentPrice = 10f;

                // 6. 실시간 1분봉 및 상위 타임프레임 Live 캔들 갱신
                UpdateLiveCandlesWithTick(currentPrice, tickVolume);

                // 이벤트 알림
                OnPriceUpdated?.Invoke(currentPrice);
            }

            // 🌟 [Realistic Feature 1] 스프레드(Spread) 계산 및 적용
            currentSpread = currentPrice * currentVolatility * 0.5f;
            if (isServerLagging) currentSpread *= 3.0f;
            if (isExternalEventOverride) currentSpread *= 5.0f;

            // 레버리지 즉사 방지 (소프트 캡: 최대 0.5%)
            float maxSpread = currentPrice * 0.005f;
            if (currentSpread > maxSpread) currentSpread = maxSpread;

            // 오버도즈 발동 중에는 연출 방해를 막기 위해 스프레드 최소화
            if (isOverdoseTrapOverride) currentSpread = currentPrice * 0.0001f;

            currentBidPrice = currentPrice - (currentSpread * 0.5f);
            currentAskPrice = currentPrice + (currentSpread * 0.5f);
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
            // P2P에서는 호스트 스냅샷이 유일한 시장 데이터입니다. 각 클라이언트의 로컬 난수로
            // 꼬리를 추가하면 같은 가격인데 캔들만 달라지므로 로컬 sweep을 적용하지 않습니다.
            if(!p2pExternalMode)CheckLiquidationSweep();

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
            
            // 요미가 방향을 예고한 날은 역방향 구간을 없앱니다. 힌트를 듣고도 반대로 가면 힌트가 무의미해집니다. (Q2 / S5)
            bool hinted = DailyMarketOutlook.Revealed && DailyMarketOutlook.Day == (gameManager != null ? gameManager.CurrentDay : -1);

            if (currentDailyRegime == MarketRegime.Bull)
            {
                if (rand < 0.60f) currentRegime = MarketRegime.Bull;
                else if (rand < 0.80f) currentRegime = MarketRegime.Sideways;
                else if (rand < 0.90f) currentRegime = hinted ? MarketRegime.Sideways : MarketRegime.Bear;
                else currentRegime = MarketRegime.Squeeze;
            }
            else if (currentDailyRegime == MarketRegime.Bear)
            {
                if (rand < 0.60f) currentRegime = MarketRegime.Bear;
                else if (rand < 0.80f) currentRegime = MarketRegime.Sideways;
                else if (rand < 0.90f) currentRegime = hinted ? MarketRegime.Sideways : MarketRegime.Bull;
                else currentRegime = MarketRegime.Squeeze;
            }
            else if (currentDailyRegime == MarketRegime.Squeeze)
            {
                if (rand < 0.50f) currentRegime = MarketRegime.Squeeze;
                else if (rand < 0.70f) currentRegime = MarketRegime.Bull;
                else if (rand < 0.90f) currentRegime = MarketRegime.Bear;
                else currentRegime = MarketRegime.Sideways;
            }
            else // Sideways
            {
                if (rand < 0.40f) currentRegime = MarketRegime.Sideways;
                else if (rand < 0.65f) currentRegime = MarketRegime.Bull;
                else if (rand < 0.90f) currentRegime = MarketRegime.Bear;
                else currentRegime = MarketRegime.Squeeze;
            }

            minutesUntilNextRegimeChange = UnityEngine.Random.Range(30, 120); // 30분~2시간 유지
            Debug.Log($"[MarketEngine] 국면 전환: {currentRegime} (유지: {minutesUntilNextRegimeChange}분, 일일 기조: {currentDailyRegime})");
        }

        // 유동성 사냥 (꼬리 휩소 스파이크 발생 - 스탑 헌팅 기믹 강화)
        private void CheckLiquidationSweep()
        {
            // 오버도즈 발동 중이거나 고속 스킵 중, 확정 주가 구간일 때는 스탑헌팅(무작위 휩쏘)을 방지합니다.
            // 서버 렉 중에는 차트가 멈춰 있어야 하므로 꼬리 틱도 찍지 않습니다.
            if (isOverdoseTrapOverride || IsFastForwarding || isServerLagging || currentSignalPhase == SignalPhase.GuaranteedOverride) return;

            // Squeeze 국면에서는 30% 확률, 그 외에는 5% 확률 + 일차별 휩쏘 보정치
            float baseProb = currentRegime == MarketRegime.Squeeze ? 0.30f : 0.05f;
            float sweepProb = baseProb + (sweepIntensityMultiplier > 1.0f ? 0.10f : 0.0f); 

            if (UnityEngine.Random.value < sweepProb)
            {
                // 일차별 변동성에 맞춰 꼬리(스파이크)의 크기도 증가합니다.
                float sweepMagnitude = UnityEngine.Random.Range(0.005f, 0.02f) * sweepIntensityMultiplier; 
                bool sweepUp = UnityEngine.Random.value > 0.5f;
                float restorePrice = currentPrice;
                float spikePrice = currentPrice * (sweepUp ? 1f + sweepMagnitude : 1f - sweepMagnitude);

                // 꼬리 끝을 실제 시세로 1틱 찍었다가 곧바로 되돌립니다. (FIX-2)
                // 예전에는 1분봉의 high/low만 늘려 청산·손절 판정이 꼬리를 보지 못했습니다(순수 시각 효과).
                // 청산은 전달된 가격이 아니라 엔진의 호가(Bid/Ask)로 판정하므로, 이벤트만 쏘지 않고 호가까지 함께 옮깁니다.
                PrintInstantTick(spikePrice, UnityEngine.Random.Range(50f, 200f)); // 거래량 폭증
                PrintInstantTick(restorePrice, 0f);

                currentVolatility *= 2.0f; // 순간 변동성 폭발
            }
        }

        /// <summary>
        /// 시뮬레이션 없이 가격 한 틱을 즉시 찍습니다. 호가·진행 캔들(모든 타임프레임)·24h 통계를 갱신하고
        /// <see cref="OnPriceUpdated"/>를 발행해 청산·익절·손절 판정이 이 가격을 보게 합니다.
        /// 스프레드는 직전 틱의 값을 그대로 씁니다.
        /// </summary>
        private void PrintInstantTick(float price, float volume)
        {
            currentPrice = price;
            currentBidPrice = price - (currentSpread * 0.5f);
            currentAskPrice = price + (currentSpread * 0.5f);
            UpdateLiveCandlesWithTick(price, volume);
            OnPriceUpdated?.Invoke(price);
        }

        // 돌발 선택 이벤트 차트 빔 점진 주입 및 골든타임 연동 (OverrideMarketTrend)
        /// <summary>
        /// 돌발 이벤트의 강제 빔을 주입합니다.
        /// </summary>
        /// <param name="targetChangePercent">목표 변동률(%). 부호가 방향입니다.</param>
        /// <param name="durationInGameMinutes">
        /// 목표 변동률까지 도달하는 데 쓸 <b>인게임 분</b>. 0 이하면 기본 30분.
        /// ⚠️ 과거 이 인자는 이름이 durationSeconds였고 메서드 안에서 30으로 덮어써져 완전히 무시됐습니다.
        ///    호출부가 넘기던 150은 아무 효과가 없었습니다. (C3)
        /// </param>
        /// <param name="isWhipsaw">트랩(휩소) 여부. 신호 종류와 변동성 배수가 달라집니다.</param>
        public void OverrideMarketTrend(float targetChangePercent, int durationInGameMinutes, bool isWhipsaw = false)
        {
            // 💡 [순간이동 제거] 1프레임 만에 주가를 순간 이동시키지 않고, 인게임 시간 동안 점진적 드리프트로 이동시킵니다.
            int durationMins = durationInGameMinutes > 0 ? durationInGameMinutes : 30;
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
            
            // 골든타임을 포함해 총합이 durationMins가 되도록, 실제 드리프트 시간에서 graceMins를 뺍니다.
            // ⚠️ 이 시간은 인게임 분입니다. TradingController의 이벤트 쉴드는 실시간 초라 단위가 다릅니다.
            //    (secondsPerGameMinute = 1인 GameScene 기준 30분 ≈ 실시간 30초, 쉴드 기본값은 150초)
            int actualDriftMins = Mathf.Max(5, durationMins - graceMins);
            minutesUntilNextRegimeChange = actualDriftMins + graceMins;

            if (targetChangePercent > 0f) currentRegime = MarketRegime.Bull;
            else if (targetChangePercent < 0f) currentRegime = MarketRegime.Bear;
            else currentRegime = MarketRegime.Squeeze;

            ForceInjectSignal(sigType, SignalStrength.Strong, !isWhipsaw, targetChangePercent, actualDriftMins, graceMins);
            Debug.Log($"[MarketEngine] ⚡ InitiateEventSignalOverride 실행! 목표 변동률: {targetChangePercent:F2}%, 실 드리프트: {actualDriftMins}분 (골든타임 {graceMins}분, 휩소: {isWhipsaw})");
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
        private float PrewarmHistoricalCandles(int minutesCount, float startPrice)
        {
            // 예전에는 인자 없이 initialPrice에서 출발해, ResetEngine(startPrice)의 startPrice가 통째로 버려졌습니다.
            float tempPrice = startPrice;
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
            switch (currentSignalPhase)
            {
                case SignalPhase.None:
                    if (IsFastForwarding) return; // 💡 [수정] 고속 스킵 중에는 신규 신호를 발생시키지 않고 관망합니다.

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
                        
                        // 오버드라이브 연출 패턴 무작위 설정
                        currentOverdriveWaveStyle = UnityEngine.Random.Range(0, 2);
                        // 이벤트 빔은 트랩/정상 경로 목록에서, 시장 신호는 셋업별 궤적에서 고릅니다. (SIG-A4)
                        bool trapPath = isExternalEventOverride && !activeSignal.IsTrueSignal;
                        if (trapPath) activeTrajectory = TrajectoryLibrary.Pick(trapTrajectories, TrajectoryLibrary.Traps);
                        else if (isExternalEventOverride) activeTrajectory = TrajectoryLibrary.Pick(pathTrajectories, TrajectoryLibrary.Paths);
                        else activeTrajectory = TrajectoryLibrary.PickForSetup(pathTrajectories, activeSignal.Setup);
                        activeTrajectoryWarp = activeTrajectory.RollWarp();

                        Debug.Log($"[MarketEngine] ⚡ [2단계 확정 주가 오버라이드 돌입] {activeSignal.GetSignalDescription()} (Wave: {currentOverdriveWaveStyle}, 궤적: {activeTrajectory.name}, 시간 비틀림 {activeTrajectoryWarp:F2})");
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
                        signalPhaseTimerMinutes = UnityEngine.Random.Range(15, 26); // 15~25분 쿨다운
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
                                    minutesUntilNextSignal = UnityEngine.Random.Range(5, 11);
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
                        minutesUntilNextSignal = UnityEngine.Random.Range(8, 16); // 쿨다운 종료 후 8~15분 내 신속 재진입
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

        // 직전 신호 기억 (SIG-A2) ------------------------------------------------------------------
        // 시장이 만든 신호만 기억합니다(이벤트 빔·오버도즈 함정 제외). 저장하지 않으므로 불러오면 기억 없이 시작합니다.
        private bool hasLastSignal;
        private bool lastSignalWasTrue;
        private bool lastSignalWasWeak;
        private int lastSignalMoveDir;                       // 실제 가격이 간 방향 (+1 상승 / −1 하락)
        private TradingController.PositionType lastSignalLure;
        private bool lastSignalAtHigh, lastSignalAtLow;      // 신호가 날 때 1시간 고점/저점 근처였는가
        private int sameDirectionTrueStreak;                 // 같은 방향 진짜 신호 연속 횟수
        private int sameDirectionStreakDir;

        private void RememberSignal(MarketSignal signal, ChartContext ctx)
        {
            int moveDir = signal.TargetPercentageDelta > 0f ? 1 : signal.TargetPercentageDelta < 0f ? -1 : 0;
            if (signal.IsTrueSignal && moveDir != 0 && moveDir == sameDirectionStreakDir) sameDirectionTrueStreak++;
            else if (signal.IsTrueSignal && moveDir != 0) { sameDirectionTrueStreak = 1; sameDirectionStreakDir = moveDir; }
            else sameDirectionTrueStreak = 0;

            hasLastSignal = true;
            lastSignalWasTrue = signal.IsTrueSignal;
            lastSignalWasWeak = signal.Strength == SignalStrength.Weak;
            lastSignalMoveDir = moveDir;
            lastSignalLure = signal.LureDirection;
            lastSignalAtHigh = ctx.NearHigh;
            lastSignalAtLow = ctx.NearLow;
        }

        /// <summary>
        /// 직전 신호의 결과로 이번 신호 종류의 가중치를 조정합니다. 신호마다 독립 추첨이라 "흐름"이 없던 것을 메웁니다.
        ///  ① 휩소 뒤 진짜 움직임 — 직전 가짜 신호가 실제로 간 방향의 돌파 ×1.5 (진위 보정은 MemoryTruthBonus)
        ///  ② 과열 — 같은 방향 진짜 신호 2연속 뒤에는 그 방향으로 꼬신 뒤 꺾는 트랩 ×1.6
        ///  ③ 이중 천장/바닥 — 고점(저점)에서 롱(숏)을 꼬신 가짜 신호 뒤 다시 고점(저점)이면 같은 트랩 ×1.5
        /// </summary>
        private void ApplySignalMemory(ChartContext ctx, ref float wBullBreak, ref float wBearBreak, ref float wBullTrap, ref float wBearTrap)
        {
            if (!hasLastSignal) return;

            if (!lastSignalWasTrue)
            {
                if (lastSignalMoveDir > 0) wBullBreak *= 1.5f;
                else if (lastSignalMoveDir < 0) wBearBreak *= 1.5f;

                if (lastSignalLure == TradingController.PositionType.Long && lastSignalAtHigh && ctx.NearHigh) wBullTrap *= 1.5f;
                if (lastSignalLure == TradingController.PositionType.Short && lastSignalAtLow && ctx.NearLow) wBearTrap *= 1.5f;
            }

            if (sameDirectionTrueStreak >= 2)
            {
                if (sameDirectionStreakDir > 0) wBullTrap *= 1.6f;
                else wBearTrap *= 1.6f;
            }
        }

        /// <summary>직전 가짜 신호가 실제로 간 방향의 돌파는 진짜일 확률 +0.15 — 털린 뒤에 나오는 진짜 움직임.</summary>
        private float MemoryTruthBonus(MarketSignalType type)
        {
            if (!hasLastSignal || lastSignalWasTrue) return 0f;
            int dir = type == MarketSignalType.BullishBreakout ? 1 : type == MarketSignalType.BearishBreakout ? -1 : 0;
            return dir != 0 && dir == lastSignalMoveDir ? 0.15f : 0f;
        }

        // 차트 컨텍스트 (SIG-A3) ------------------------------------------------------------------
        private const int ChartContextLookbackMinutes = 60;       // 최근 1시간 1분봉
        private const float NearExtremeRatio = 0.003f;            // 고점/저점 0.3% 이내
        private const float NearRoundRatio = 0.002f;              // 라운드 피겨 0.2% 이내 (오더블록 반발 범위와 같음)

        private struct ChartContext
        {
            public bool NearHigh, NearLow, NearRound, NarrowRange;
        }

        /// <summary>
        /// 최근 1시간 1분봉으로 지금 가격이 어디에 있는지 읽습니다. 신호 생성 때만(수 분에 한 번) 호출되므로 순회 비용은 무시할 수준입니다.
        /// 박스 폭은 같은 시간 랜덤워크의 1σ(분당 σ × √분)보다 좁으면 "조여 있다"고 봅니다.
        /// </summary>
        private ChartContext ReadChartContext()
        {
            var ctx = new ChartContext();
            if (currentPrice <= 0f || !candleHistories.TryGetValue(Timeframe.M1, out List<CandleData> m1) || m1.Count < 20) return ctx;

            int n = Mathf.Min(ChartContextLookbackMinutes, m1.Count);
            float hi = currentPrice, lo = currentPrice;
            for (int i = m1.Count - n; i < m1.Count; i++)
            {
                if (m1[i].high > hi) hi = m1[i].high;
                if (m1[i].low < lo) lo = m1[i].low;
            }

            ctx.NearHigh = (hi - currentPrice) / currentPrice < NearExtremeRatio;
            ctx.NearLow = (currentPrice - lo) / currentPrice < NearExtremeRatio;
            float round = Mathf.Round(currentPrice / 1000f) * 1000f;
            ctx.NearRound = round > 10f && Mathf.Abs(currentPrice - round) / currentPrice < NearRoundRatio;
            ctx.NarrowRange = (hi - lo) / currentPrice < currentVolatility * Mathf.Sqrt(n);
            return ctx;
        }

        // 셋업 가중치 표 (SIG-A4). 신호 종류·진위·기조 조합마다 하나씩입니다.
        private static readonly (SignalSetup setup, float weight)[] SetupsTrueSqueeze =
            { (SignalSetup.VolatilitySqueeze, 50f), (SignalSetup.Breakout, 35f), (SignalSetup.NewsSpike, 15f) };
        private static readonly (SignalSetup setup, float weight)[] SetupsTrueWithTrend =
            { (SignalSetup.TrendContinuation, 40f), (SignalSetup.Breakout, 45f), (SignalSetup.NewsSpike, 15f) };
        private static readonly (SignalSetup setup, float weight)[] SetupsTrueOther =
            { (SignalSetup.Breakout, 80f), (SignalSetup.NewsSpike, 20f) };
        private static readonly (SignalSetup setup, float weight)[] SetupsFalseBreakout =
            { (SignalSetup.FalseBreakout, 50f), (SignalSetup.StopRun, 30f), (SignalSetup.LiquidityGrab, 20f) };
        private static readonly (SignalSetup setup, float weight)[] SetupsBullTrapRange =
            { (SignalSetup.RangeRejection, 45f), (SignalSetup.FalseBreakout, 25f), (SignalSetup.LiquidityGrab, 15f), (SignalSetup.Distribution, 15f) };
        private static readonly (SignalSetup setup, float weight)[] SetupsBearTrapRange =
            { (SignalSetup.RangeRejection, 45f), (SignalSetup.FalseBreakout, 25f), (SignalSetup.LiquidityGrab, 15f), (SignalSetup.Capitulation, 15f) };
        private static readonly (SignalSetup setup, float weight)[] SetupsBullTrap =
            { (SignalSetup.Distribution, 35f), (SignalSetup.FalseBreakout, 30f), (SignalSetup.StopRun, 15f), (SignalSetup.LiquidityGrab, 20f) };
        private static readonly (SignalSetup setup, float weight)[] SetupsBearTrap =
            { (SignalSetup.Capitulation, 35f), (SignalSetup.FalseBreakout, 30f), (SignalSetup.StopRun, 15f), (SignalSetup.LiquidityGrab, 20f) };

        /// <summary>
        /// 신호 종류·진위·그날 기조에서 연출 계열(셋업)을 고릅니다. (SIG-A4)
        /// 롱을 꼬시는 트랩은 고점 분산, 숏을 꼬시는 트랩은 투매 후 V반등이 전형이고, 횡보장 트랩은 박스권 반락이 많습니다.
        /// </summary>
        private SignalSetup ChooseSetup(MarketSignalType type, bool isTrue, bool narrowRange)
        {
            bool breakout = type == MarketSignalType.BullishBreakout || type == MarketSignalType.BearishBreakout;
            (SignalSetup setup, float weight)[] table;
            if (breakout && isTrue)
            {
                // 박스 폭이 좁게 조여 있었다면(SIG-A3) 기조와 무관하게 변동성 수축 돌파 계열입니다.
                table = currentDailyRegime == MarketRegime.Squeeze || narrowRange ? SetupsTrueSqueeze
                      : TrendAlignmentBonus(type) > 0f ? SetupsTrueWithTrend
                      : SetupsTrueOther;
            }
            else if (breakout)
            {
                table = SetupsFalseBreakout;
            }
            else
            {
                bool range = currentDailyRegime == MarketRegime.Sideways;
                bool lureLong = type == MarketSignalType.BullTrap;
                table = range ? (lureLong ? SetupsBullTrapRange : SetupsBearTrapRange)
                              : (lureLong ? SetupsBullTrap : SetupsBearTrap);
            }

            float total = 0f;
            for (int i = 0; i < table.Length; i++) total += table[i].weight;
            float r = UnityEngine.Random.value * total;
            for (int i = 0; i < table.Length; i++)
            {
                r -= table[i].weight;
                if (r <= 0f) return table[i].setup;
            }
            return table[table.Length - 1].setup;
        }

        // 일일 기조별 신호 종류 가중치 [상승 돌파, 하락 돌파, 불트랩, 베어트랩] (SIG-A1)
        // 추세장에서는 추세 방향 돌파와 "역추세 쪽을 꼬신 뒤 추세 방향으로 가는" 트랩(상승장의 베어트랩)이 많고,
        // 박스권은 양 끝단의 가짜 돌파가 지배적입니다.
        private static readonly float[] BullSignalWeights     = { 0.45f, 0.15f, 0.15f, 0.25f };
        private static readonly float[] BearSignalWeights     = { 0.15f, 0.45f, 0.25f, 0.15f };
        private static readonly float[] SidewaysSignalWeights = { 0.20f, 0.20f, 0.30f, 0.30f };
        private static readonly float[] SqueezeSignalWeights  = { 0.30f, 0.30f, 0.20f, 0.20f };

        private static float[] SignalTypeWeights(MarketRegime dailyRegime)
        {
            switch (dailyRegime)
            {
                case MarketRegime.Bull: return BullSignalWeights;
                case MarketRegime.Bear: return BearSignalWeights;
                case MarketRegime.Squeeze: return SqueezeSignalWeights;
                default: return SidewaysSignalWeights;
            }
        }

        /// <summary>돌파 신호가 그날 기조와 같은 방향이면 +0.2, 반대면 -0.2, 그 외(트랩·횡보·광기) 0.</summary>
        private float TrendAlignmentBonus(MarketSignalType type)
        {
            int trend = currentDailyRegime == MarketRegime.Bull ? 1 : currentDailyRegime == MarketRegime.Bear ? -1 : 0;
            int dir = type == MarketSignalType.BullishBreakout ? 1 : type == MarketSignalType.BearishBreakout ? -1 : 0;
            return trend * dir * 0.20f;
        }

        // 새 차트 신호 생성 및 방송
        public void GenerateMarketSignal()
        {
            // 신호 종류는 그날의 거시 기조를 따릅니다. (SIG-A1)
            // 예전엔 기조와 무관하게 35/35/15/15라, 하락 기조인 날에도 상승 돌파가 똑같이 나와
            // 요미의 일일 방향 힌트가 매매 판단에 거의 쓸모가 없었습니다.
            float[] w = SignalTypeWeights(currentDailyRegime);

            // 지금 차트 모양도 읽습니다. (SIG-A3) 돌파는 고점/저점에서, 트랩은 라운드 피겨와 고점/저점 위에서 잘 납니다.
            // 예전엔 신호가 차트와 완전히 무관하게 터져, 차트를 봐도 다음 신호를 짐작할 단서가 없었습니다.
            ChartContext ctx = ReadChartContext();
            float wBullBreak = w[0], wBearBreak = w[1], wBullTrap = w[2], wBearTrap = w[3];
            if (ctx.NearHigh) { wBullBreak *= 1.6f; wBullTrap *= 1.4f; } // 저항선 돌파 시도 — 진짜든 가짜든
            if (ctx.NearLow) { wBearBreak *= 1.6f; wBearTrap *= 1.4f; }  // 지지선 이탈 시도
            if (ctx.NearRound) { wBullTrap *= 1.5f; wBearTrap *= 1.5f; } // 라운드 피겨 = 오더블록 반발 자리
            ApplySignalMemory(ctx, ref wBullBreak, ref wBearBreak, ref wBullTrap, ref wBearTrap); // SIG-A2
            float wTotal = wBullBreak + wBearBreak + wBullTrap + wBearTrap;

            float rand = UnityEngine.Random.value * wTotal;
            MarketSignalType type;
            if (rand < wBullBreak) type = MarketSignalType.BullishBreakout;
            else if (rand < wBullBreak + wBearBreak) type = MarketSignalType.BearishBreakout;
            else if (rand < wBullBreak + wBearBreak + wBullTrap) type = MarketSignalType.BullTrap;
            else type = MarketSignalType.BearTrap;

            // 강도 설정 (기본 65% Strong, 광기 기조는 80%)
            float strongProb = currentDailyRegime == MarketRegime.Squeeze ? 0.80f : 0.65f;
            if (hasLastSignal && lastSignalWasWeak) strongProb = Mathf.Min(0.95f, strongProb + 0.15f); // 약한 신호 뒤 에너지 축적 (SIG-A2)
            SignalStrength strength = UnityEngine.Random.value < strongProb ? SignalStrength.Strong : SignalStrength.Weak;

            // IsTrueSignal 결정: Breakout은 기본 60% 확률로 진짜, Trap은 100% 가짜 속임수.
            // Phase 3 이후(fakeoutProbability 증가) 시 낚시(가짜 돌파) 확률 증가 — fakeoutProbability 0.5면 0.35.
            // 기조와 같은 방향의 돌파는 +20%p, 반대 방향은 -20%p (SIG-A1).
            float trueSignalProb = Mathf.Clamp(0.60f - (fakeoutProbability * 0.5f) + TrendAlignmentBonus(type) + MemoryTruthBonus(type), 0.05f, 0.95f);
            bool isTrue = (type == MarketSignalType.BullishBreakout || type == MarketSignalType.BearishBreakout) && UnityEngine.Random.value < trueSignalProb;

            // 광기 기조는 움직임 자체도 큽니다.
            float magnitudeScale = currentDailyRegime == MarketRegime.Squeeze ? 1.25f : 1.0f;

            int duration = strength == SignalStrength.Strong ? UnityEngine.Random.Range(15, 31) : UnityEngine.Random.Range(5, 11);
            int grace = UnityEngine.Random.Range(3, 6); // 3~5분 골든타임 여유 시간

            // 연출 계열(셋업) — 궤적 모양과 예고 시간만 바꿉니다. AI가 읽는 종류·진위·방향은 위에서 이미 정해졌습니다. (SIG-A4)
            SignalSetup setup = ChooseSetup(type, isTrue, ctx.NarrowRange);
            if (setup == SignalSetup.NewsSpike)
            {
                duration = UnityEngine.Random.Range(5, 11); // 짧고 굵게
                grace = 1;                                  // 예고가 거의 없습니다
            }

            // 확정 변동률(TargetPercentageDelta) 연산
            float targetDelta = 0f;
            if (strength == SignalStrength.Strong)
            {
                // 강한 신호: ±3.0% ~ ±6.0% (10배 레버리지 기준 ±30%~±60% ROE)
                float mag = UnityEngine.Random.Range(3.0f, 6.0f) * magnitudeScale;
                if (type == MarketSignalType.BullishBreakout) targetDelta = isTrue ? mag : -mag;
                else if (type == MarketSignalType.BearishBreakout) targetDelta = isTrue ? -mag : mag;
                else if (type == MarketSignalType.BullTrap) targetDelta = -mag; // 롱 유도 후 급락 빔
                else if (type == MarketSignalType.BearTrap) targetDelta = mag;  // 숏 유도 후 급등 빔
            }
            else
            {
                // 약한 신호(단타/미끼): ±0.6% ~ ±1.5% (10배 레버리지 기준 ±6%~±15% ROE)
                float mag = UnityEngine.Random.Range(0.6f, 1.5f) * magnitudeScale;
                if (type == MarketSignalType.BullishBreakout) targetDelta = isTrue ? mag : -mag;
                else if (type == MarketSignalType.BearishBreakout) targetDelta = isTrue ? -mag : mag;
                else if (type == MarketSignalType.BullTrap) targetDelta = -mag;
                else if (type == MarketSignalType.BearTrap) targetDelta = mag;
            }

            activeSignal = new MarketSignal
            {
                Type = type,
                Direction = MarketSignal.AdvertisedDirectionOf(type),
                Setup = setup,
                Strength = strength,
                IsTrueSignal = isTrue,
                TargetPercentageDelta = targetDelta,
                DurationMinutes = duration,
                GraceMinutes = grace,
                SignalStartPrice = currentPrice
            };

            currentSignalPhase = SignalPhase.GraceWindow;
            signalPhaseTimerMinutes = grace;
            RememberSignal(activeSignal, ctx);

            Debug.Log($"[MarketEngine] 📣 [신호 방송 - 1단계 판단 여유 골든타임 돌입] {activeSignal.GetSignalDescription()} (차트: 고점근접 {ctx.NearHigh} / 저점근접 {ctx.NearLow} / 라운드 {ctx.NearRound} / 좁은박스 {ctx.NarrowRange})");
            OnMarketSignalGenerated?.Invoke(activeSignal);
            OnSignalPhaseChanged?.Invoke(currentSignalPhase, activeSignal);
        }

        // 강제로 특정 차트 신호를 외부(이벤트나 AI 튜닝용)에서 주입하는 함수
        public void ForceInjectSignal(MarketSignalType type, SignalStrength strength, bool isTrue, float targetDelta, int durationMinutes, int graceMinutes = 3)
        {
            activeSignal = new MarketSignal
            {
                Type = type,
                Direction = MarketSignal.AdvertisedDirectionOf(type),
                Setup = isTrue ? SignalSetup.Breakout : SignalSetup.FalseBreakout,
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
                Direction = trapPosType,
                Setup = SignalSetup.FalseBreakout,
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
                // 페이즈가 None이 되면 UpdateSignalSystem의 해제 지점에 도달할 수 없으므로 함께 내립니다.
                isExternalEventOverride = false;
                minutesUntilNextSignal = 3;
                OnSignalPhaseChanged?.Invoke(currentSignalPhase, activeSignal);
                Debug.Log("[MarketEngine] 💊 [Overdose 차트 함정 해제] 플레이어의 멘탈 회복 조치로 죽음의 차트 빔이 해제되고 정상 차트로 복귀합니다.");
            }
        }
    }
}
