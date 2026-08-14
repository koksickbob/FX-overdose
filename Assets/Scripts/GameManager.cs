using System;
using System.Collections.Generic;
using FXOverdose.Core;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    // --- 당일 자산 궤적 (상단 HUD P&L 스파크라인) ---
    // UI가 아니라 여기서 들고 있어야 세이브 수집 범위에 들어옵니다.
    // 종전에는 TopStatusBarUIController의 private 필드라 저장이 구조적으로 불가능했고,
    // 불러오면 그래프가 1점으로 재시작해 직선으로 보였습니다.
    private readonly List<float> dailyEquityHistory = new List<float>();
    private int lastEquitySampleMinute = -1;
    private const int EquitySampleIntervalMinutes = 15;
    private const int MaxEquitySamples = 96; // 인게임 24시간 / 15분

    /// <summary>당일 자산 궤적(읽기 전용). UI는 이 값을 읽기만 하고 기록하지 않습니다.</summary>
    public IReadOnlyList<float> DailyEquityHistory => dailyEquityHistory;

    /// <summary>현재 총자산(현금 + 증거금 + 미실현손익). TraderStatus의 기존 계산을 재사용합니다.</summary>
    private float CurrentTotalEquity()
    {
        var status = TraderStatus.CanonicalInstance;
        return status != null ? status.GetTotalEquity() : currentBalance;
    }

    /// <summary>새 날(또는 새 게임)의 기준선으로 궤적을 다시 시작합니다.</summary>
    private void ResetDailyEquityHistory(float seedEquity)
    {
        dailyEquityHistory.Clear();
        dailyEquityHistory.Add(seedEquity);
        lastEquitySampleMinute = -1;
    }

    /// <summary>
    /// 표본 주기(15분)에 걸리면 현재 자산을 궤적에 남깁니다.
    /// 싱글은 AdvanceOneMinute, P2P는 ApplyP2PState가 호출합니다 —
    /// P2P는 분 진행이 AdvanceOneMinute를 거치지 않고 이벤트만 발행하기 때문입니다.
    /// </summary>
    private void SampleDailyEquityIfDue()
    {
        if (currentMinute % EquitySampleIntervalMinutes != 0 || currentMinute == lastEquitySampleMinute) return;

        lastEquitySampleMinute = currentMinute;
        dailyEquityHistory.Add(CurrentTotalEquity());
        if (dailyEquityHistory.Count > MaxEquitySamples)
        {
            dailyEquityHistory.RemoveAt(0);
        }
    }

    private bool p2pExternalMode;
    public void EnableP2PExternalMode()
    {
        p2pExternalMode=true;

        // P2P 경기용 인스턴스는 상주시키지 않습니다. 경기가 끝난 뒤에도 살아남으면
        // p2pExternalMode 플래그가 스토리 세션으로 새어 들어가 시계가 멈춘 채로 시작합니다.
        // 현재 씬으로 되돌려 씬과 함께 폐기되게 합니다.
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (gameObject.scene != activeScene && activeScene.IsValid())
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(gameObject, activeScene);
            if (instance == this) instance = null;
        }

        currentDate=startDate;currentHour=9;currentMinute=0;
        // TopStatusBar가 Start에서 수익선 최초 점을 기록하기 전에 P2P 시작 자산을 확정합니다.
        currentBalance=startingBalance;
        StartOfDayEquity=startingBalance;
        ResetDailyEquityHistory(startingBalance);
        currentState=GameState.Playing;
    }
    public void ApplyP2PState(int totalMinutes,float balance)
    {
        int previousTotalMinutes=currentHour*60+currentMinute;
        int nextHour=totalMinutes/60,nextMinute=totalMinutes%60;
        int advancedMinutes=Mathf.Max(0,totalMinutes-previousTotalMinutes);
        currentDate=startDate;currentHour=nextHour;currentMinute=nextMinute;currentBalance=balance;currentState=GameState.Playing;
        // 패킷 지연으로 두 분 이상 건너뛰어도 원본 캔들 엔진이 분봉을 빠뜨리지 않게 합니다.
        for(int i=0;i<advancedMinutes;i++)OnGameMinuteAdvanced?.Invoke();
        // P2P는 AdvanceOneMinute를 거치지 않으므로 자산 표본을 여기서 직접 남깁니다.
        SampleDailyEquityIfDue();
    }
    // 1분 경과 시 발행하는 이벤트
    public event Action OnGameMinuteAdvanced;
    // 💡 고속 시간 경과(AdvanceGameMinutes) 완료 또는 중단 직후 UI 단 1회 갱신을 트리거하는 이벤트
    public event Action OnFastForwardEnded;
    // 하루 종료(24:00) 시 발행하는 이벤트 (일일 정산 UI 표시용)
    public event Action OnDayEnded;
    // 게임 오버 발생 시 발행하는 이벤트 (게임 오버 UI 표시용)
    public static event Action<EndingType> OnGameOverEvent;
    [System.Serializable]
    public class StoryEvent 
    { 
        public int triggerDay; 
        public bool isPenalty; 
        public float penaltyAmount; 
        public string dialogueMessage; 
        public System.Collections.Generic.List<Sprite> comicPanels; 
    }

    //게임 진행 상태
    public enum GameState
    {
        Loading,
        Playing,
        Paused,
        Settlement,
        GameOver
    }

    //게임 엔딩 종료
    public enum EndingType
    {
        None,
        Success,
        Bankruptcy,
        Overdose
    }

    [Header("게임 진행 상태")]
    [SerializeField] private GameState currentState = GameState.Loading;
    [SerializeField] private EndingType currentEnding = EndingType.None;

    [Header("자산 설정")]
    [SerializeField] private float startingBalance = 7000f; // 초기 자본금
    [SerializeField] private float currentBalance;           // 현재 자산

    // 최종 보스(20일차 사채업자)의 자산 배율. BossManager의 등록값과 같아야 합니다.
    // 보스 데이터가 없을 때의 엔딩 백업 판정에만 쓰입니다.
    private const float FinalBossAssetScale = 0.9f;

    [Header("스토리 모드 이벤트 및 엔딩 연출")]
    [SerializeField] private System.Collections.Generic.List<StoryEvent> storyEvents = new System.Collections.Generic.List<StoryEvent>();
    [SerializeField] private System.Collections.Generic.List<Sprite> successEndingComic;
    [SerializeField] private System.Collections.Generic.List<Sprite> bankruptcyEndingComic;
    [SerializeField] private System.Collections.Generic.List<Sprite> overdoseEndingComic;
    public StoryEvent TodayEvent { get; private set; } // DailySettlementUIController 접근용
    public float TodayRegularDeduction { get; private set; } // 일일 정산 UI 접근용 (정기 지출)
    public string TodayRegularDeductionReason { get; private set; } // 일일 정산 UI 접근용 (정기 지출 사유)
    public bool isSettlementProcessing = false;

    [Header("시간 설정")]
    // 달력 날짜가 권위값이고 일차(CurrentDay)는 여기서 파생됩니다.
    // 시/분을 날짜와 합쳐 하나의 DateTime으로 두지 않는 이유: 하루는 24:00에 끝나는데
    // 그 시점에 일차는 아직 '어제'여야 정산·보스 승패 판정이 맞습니다. 합치면 24:00이
    // 자동으로 익일 00:00이 되어 하루가 어긋납니다.
    private System.DateTime startDate = FXOverdose.Core.GameCalendar.DefaultStartDate;
    private System.DateTime currentDate = FXOverdose.Core.GameCalendar.DefaultStartDate;
    [SerializeField] private int currentHour = 9;   // 현재 시간
    [SerializeField] private int currentMinute = 0; // 현재 분

    // 스토리 최종일. 실제 엔딩의 주인은 BossData.IsFinalBoss이고 이 상수는 보스 스폰이
    // 실패했을 때의 백업 판정에만 쓰입니다. 스토리 개편으로 게임 길이가 바뀌면 여기 한 줄만 고칩니다.
    public const int StoryLastDay = 20;

    /// <summary>
    /// 20일차 강제 종료 스위치. <b>스토리 개편 기간 동안 꺼 둡니다 (2026-08-15).</b>
    ///
    /// 끄면 20일차를 넘겨 계속 진행합니다. 정기 지출은 21일차부터 3일마다 1.3배씩 불어나므로
    /// (<see cref="CalculateExpectedDeduction"/>) 압박 자체는 유지됩니다.
    ///
    /// ⚠️ <b>보스와 함께 꺼져 있으면 성공 엔딩에 도달할 수 없습니다.</b> 성공 판정이
    ///    「최종 보스 격파」와 「20일차 백업 판정」 둘뿐인데 양쪽이 다 닫히기 때문입니다.
    ///    남는 결말은 파산과 오버도즈뿐이며, 이는 개편 기간의 의도된 상태입니다.
    /// </summary>
    public static readonly bool StoryDayLimitEnabled = false;

    /// <summary>
    /// 지금 20일차 강제 종료가 살아 있는지. <b>비활성화는 스토리 모드에만 적용됩니다.</b>
    ///
    /// 엔드리스·챌린지는 종료 조건이 모드의 뼈대이므로 스위치와 무관하게 종전 동작을 유지합니다.
    /// </summary>
    private static bool DayLimitActive
    {
        get
        {
            if (StoryDayLimitEnabled) return true;
            var save = FXOverdose.Core.SaveLoadManager.Instance;
            // 세이브 매니저가 없는 테스트 씬 등은 스토리로 간주해 종전대로 꺼 둡니다.
            return save != null && save.CurrentGameMode != FXOverdose.Core.GameMode.Story;
        }
    }

    // 당일 오전 9시작 시점의 총 자산 (일일 정산용)
    public float StartOfDayEquity { get; private set; }
    // 구버전 세이브처럼 09:00 기준 자산이 없는 경우, 로드 시점부터 계산 중인지 표시합니다.
    public bool IsDailyPnlPartial { get; private set; }

    // 현실에서 몇 초마다 게임 속 1분이 흐를지 설정 (기존 1.0초에서 1.5배 가속하여 약 0.666초 -> 하루 현실 시간 10분)
    [Tooltip("현실에서 몇 초마다 게임 속 1분이 흐르는지 설정합니다. (0.666초 = 하루 10분)")]
    [SerializeField] private float secondsPerGameMinute = 0.666f;

    // 실제로 흐른 시간을 누적하는 변수
    private float timeAccumulator;

    // 💡 고속 시간 진행 상태 및 남은 분 보존 변수 (스킬 업그레이드 중 이벤트 발생 시 스킵 방지)
    public bool IsFastForwardingTime { get; private set; } = false;
    private int remainingFastForwardMinutes = 0;
    
    private int preservedFastForwardMinutes = 0;
    private bool interruptFastForwardForOverdose = false;

    // 다른 스크립트에서 현재 값을 읽을 수 있도록 공개
    // 값 변경은 할 수 없음
    public GameState CurrentState => currentState;
    public EndingType CurrentEnding => currentEnding;
    public float StartingBalance => startingBalance;
    public float CurrentBalance => currentBalance;
    /// <summary>게임 시작 날짜(에폭). 세이브가 자기 값을 들고 있으므로 상수가 아닙니다.</summary>
    public System.DateTime StartDate => startDate;
    public System.DateTime CurrentDate => currentDate;
    public int CurrentYear => currentDate.Year;
    public int CurrentMonth => currentDate.Month;
    /// <summary>그 달의 몇 일인지. 일차(<see cref="CurrentDay"/>)와 다른 값이므로 주의하십시오.</summary>
    public int CurrentDayOfMonth => currentDate.Day;

    /// <summary>
    /// 게임 시작으로부터 며칠째인지(1부터). 보스 일정·스토리 이벤트·난이도 구간 등
    /// 서수를 키로 쓰는 기존 시스템 전부가 이 값을 읽습니다.
    /// </summary>
    public int CurrentDay => (currentDate - startDate).Days + 1;
    public int CurrentHour => currentHour;
    public int CurrentMinute => currentMinute;
    public float SecondsPerGameMinute => secondsPerGameMinute;

    /// <summary>일차 서수를 달력 날짜로 환산합니다. 보스 배지처럼 예정 일차를 표기할 때 씁니다.</summary>
    public System.DateTime DateForDay(int dayOrdinal) => startDate.AddDays(Mathf.Max(1, dayOrdinal) - 1);

    // 저장 데이터를 불러올 때 당일 손익 기준 자산을 복구합니다.
    // 구버전 세이브에는 값이 없으므로 현재 잔고를 안전한 기준값으로 사용합니다.
    internal void RestoreStartOfDayEquity(float equity, bool isPartial)
    {
        StartOfDayEquity = float.IsFinite(equity) && equity > 0f ? equity : currentBalance;
        IsDailyPnlPartial = isPartial;
    }

    /// <summary>일일 정산 문맥을 세이브에 담습니다. (SV-B6 / SV-B9)</summary>
    internal void CaptureSettlementContext(FXOverdose.Core.SaveData data)
    {
        if (data == null) return;
        data.TodayRegularDeduction = TodayRegularDeduction;
        data.TodayRegularDeductionReason = TodayRegularDeductionReason ?? "";
        data.IsSettlementProcessing = isSettlementProcessing;

        // 당일 P&L 스파크라인 궤적. 베이스 데이터를 재사용하므로 비우고 다시 채웁니다.
        if (data.DailyEquityHistory == null) data.DailyEquityHistory = new List<float>();
        data.DailyEquityHistory.Clear();
        data.DailyEquityHistory.AddRange(dailyEquityHistory);
    }

    /// <summary>세이브에서 일일 정산 문맥을 되돌립니다. 정산 창의 지출 사유가 유지됩니다.</summary>
    internal void RestoreSettlementContext(FXOverdose.Core.SaveData data)
    {
        if (data == null) return;
        TodayRegularDeduction = data.TodayRegularDeduction;
        TodayRegularDeductionReason = data.TodayRegularDeductionReason ?? "";
        isSettlementProcessing = data.IsSettlementProcessing;

        // 저장된 궤적을 되돌립니다. 구버전 세이브나 방에서 저장된 세이브는 비어 있으므로
        // 그날 기준선(StartOfDayEquity)으로 1점 시딩해 그래프가 엉뚱한 높이에서 시작하지 않게 합니다.
        dailyEquityHistory.Clear();
        if (data.DailyEquityHistory != null && data.DailyEquityHistory.Count > 0)
        {
            dailyEquityHistory.AddRange(data.DailyEquityHistory);
            if (dailyEquityHistory.Count > MaxEquitySamples)
            {
                dailyEquityHistory.RemoveRange(0, dailyEquityHistory.Count - MaxEquitySamples);
            }
        }
        else
        {
            dailyEquityHistory.Add(StartOfDayEquity > 0f ? StartOfDayEquity : currentBalance);
        }
        lastEquitySampleMinute = -1;
    }

    public void SetSecondsPerGameMinute(float newValue)
    {
        secondsPerGameMinute = Mathf.Max(0.001f, newValue);
    }

    /// <summary>
    /// 세이브에서 달력과 시각을 되돌립니다.
    ///
    /// 마이그레이터에만 기대지 않고 여기서도 폴백을 겁니다. 마이그레이터는 세이브 버전이
    /// 현재 버전과 같으면 아예 돌지 않는데, 요미의 방(= GameManager가 없는 씬)에서 만들어진
    /// 새 게임의 첫 세이브가 바로 그 경우라 날짜가 빈 채로 최신 버전 태그를 달고 들어옵니다.
    /// </summary>
    internal void RestoreClock(string startDateText, string currentDateText, int hour, int minute, int legacyDay)
    {
        startDate = FXOverdose.Core.GameCalendar.TryParse(startDateText, out var parsedStart)
            ? parsedStart
            : FXOverdose.Core.GameCalendar.DefaultStartDate;

        // 날짜가 비었거나 손상됐으면 일차 서수로 역산합니다. 구버전 세이브의 정상 경로이기도 합니다.
        if (!FXOverdose.Core.GameCalendar.TryParse(currentDateText, out var parsedCurrent))
        {
            parsedCurrent = startDate.AddDays(Mathf.Max(1, legacyDay) - 1);
        }

        // 시작일보다 이전 날짜는 일차가 0 이하가 되어 모든 서수 판정을 망가뜨립니다.
        currentDate = parsedCurrent.Date < startDate ? startDate : parsedCurrent.Date;
        currentHour = hour;
        currentMinute = minute;
    }

    /// <summary>
    /// 스토리 연출용 날짜 점프. <b>날짜만 옮깁니다</b> — 체력/멘탈 회복, 시간 슬롯 리필, 차트 리셋 같은
    /// 하루 전환 처리는 하지 않습니다. 건너뛴 날의 정기 지출을 어떻게 할지도 정해진 바 없습니다.
    /// 그 정책은 실제로 점프를 쓰는 스토리 작업에서 정하십시오.
    ///
    /// 보스·스토리 이벤트·최종일이 예약된 일차는 넘지 못하고 그 날에서 멈춥니다.
    /// 특히 최종일 판정이 등호 비교라, 뛰어넘으면 엔딩이 영영 발생하지 않습니다.
    /// </summary>
    public void AdvanceDate(int days)
    {
        if (days < 1) return;

        int fromDay = CurrentDay;
        int targetDay = fromDay + days;
        var bossManager = FXOverdose.Core.BossManager.Instance;

        for (int day = fromDay + 1; day < targetDay; day++)
        {
            // 보스는 HasBossToday가 아니라 IsBossScheduledDay로 봅니다.
            // 보스 기능이 꺼져 있어도 '보스가 편성된 날'은 계속 피해야 합니다 —
            // 지금 건너뛴 날은 보스를 되살려도 돌아오지 않습니다.
            bool reserved = (DayLimitActive && day == StoryLastDay)
                || (bossManager != null && bossManager.IsBossScheduledDay(day))
                || storyEvents.Exists(e => e.triggerDay == day);
            if (!reserved) continue;

            Debug.LogWarning($"[GameManager] 날짜 점프가 예약 일차({day}일차)를 건너뛰려 해 그 날에서 멈춥니다. " +
                             $"요청 {days}일 → 실제 {day - fromDay}일");
            targetDay = day;
            break;
        }

        currentDate = startDate.AddDays(targetDay - 1);
    }

    /// <summary>
    /// 스토리 위약금 스위치. <b>스토리 개편 기간 동안 꺼 둡니다 (2026-08-15).</b>
    ///
    /// 위약금은 <c>StoryEvent.isPenalty</c>/<c>penaltyAmount</c>를 읽는 곳이 5군데로 흩어져 있어
    /// 관문이 될 함수가 없습니다. 그래서 <b>데이터 쪽을 0으로 만들어</b> 모든 읽는 쪽이
    /// 자연스럽게 "위약금 없음"을 보게 합니다 — 정산 UI의 위약금 표시와 요미의 위약금 대사까지
    /// 함께 사라집니다.
    ///
    /// 씬 에셋은 건드리지 않습니다. 런타임 사본만 0이 되므로 true로 되돌리면 원값이 그대로 돌아옵니다.
    ///
    /// ⚠️ <b>정기 지출(CalculateExpectedDeduction)은 위약금이 아닙니다.</b> 3·7·11·15·18일차의
    ///    구독료·로비 자금 등은 이 스위치와 무관하게 계속 빠져나갑니다.
    /// </summary>
    public static readonly bool StoryPenaltiesEnabled = false;

    /// <summary>
    /// 요미의 방에서 일일 정산까지 끝낼지 여부. 끄면 종전처럼 취침 시 GameScene으로 넘어가
    /// 남은 시간을 가속한 뒤 거기서 정산합니다.
    ///
    /// 이 스위치를 끄면 <see cref="Instance"/>의 상주 동작도 함께 무의미해지지만,
    /// 상주 자체는 유지됩니다 — 시간 슬롯이 시계를 미는 경로가 그것에 기대고 있지 않으므로
    /// 되돌려도 안전합니다.
    /// </summary>
    public static readonly bool RoomSettlementEnabled = true;

    private static GameManager instance;

    /// <summary>
    /// 상주 GameManager. 씬을 넘나들어도 같은 인스턴스를 돌려줍니다.
    ///
    /// 인스펙터에 배선된 <c>[SerializeField] GameManager</c> 참조는 씬이 다시 로드되면
    /// <b>파괴된 사본</b>을 가리키게 됩니다. Unity에서 파괴된 오브젝트는 <c>== null</c>이 참이므로,
    /// 각 소비자는 <c>if (gameManager == null) gameManager = GameManager.Instance;</c> 한 줄로
    /// 살아 있는 인스턴스를 다시 잡을 수 있습니다.
    /// </summary>
    public static GameManager Instance
    {
        get
        {
            if (instance != null) return instance;
            instance = FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            return instance;
        }
    }

    private void Awake()
    {
        // 중복이 생기면 <b>새로 들어온 쪽이 이깁니다.</b>
        //
        // 이 오브젝트에는 GameManager뿐 아니라 TraderStatus·TradingController·
        // MarketSimulationEngine·AIVisualController·AITradingBrain이 함께 붙어 있습니다.
        // 즉 "GameManager를 상주시킨다"는 것은 <b>트레이딩 코어 전체를 씬 밖으로 끌어낸다</b>는 뜻입니다.
        //
        // 그래서 옛 사본을 남기고 새 사본을 버리면, GameScene의 차트·거래 UI가 인스펙터로
        // 물고 있던 엔진이 파괴되어 배선이 끊깁니다. 반대로 새 쪽을 살리면 씬 배선이 온전하고,
        // 잃는 것은 옛 인스턴스의 런타임 상태뿐인데 그것은 어차피 세이브에서 복원됩니다.
        //
        // 상주가 필요한 구간은 「GameScene → 요미의 방」 한 방향뿐입니다. 방에서 정산할 때
        // 살아 있기만 하면 되고, 거래 씬으로 돌아오면 그 씬의 것이 주인이 되는 편이 옳습니다.
        if (instance != null && instance != this)
        {
            Debug.Log("[GameManager] 거래 씬의 새 인스턴스가 이전 상주 인스턴스를 대체합니다.");
            Destroy(instance.gameObject);
        }

        instance = this;
        initializedForCurrentScene = false;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;

        // 모든 컴포넌트의 Start보다 먼저 코스튬 관리자를 준비하여
        // AIVisualController가 첫 프레임부터 장착 변경 이벤트를 구독할 수 있게 합니다.
        EnsureBossManager();
        EnsureCostumeManager();
        DisableStoryPenaltiesIfNeeded();
    }

    private void OnDestroy()
    {
        // 구독 해제는 조건 없이 합니다. 대체당해 파괴되는 인스턴스가 구독을 남기면
        // 이미 죽은 오브젝트에서 씬 로드 콜백이 돌아갑니다.
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (instance == this) instance = null;
    }

    /// <summary>
    /// 위약금을 끈 상태라면 인스펙터에서 들어온 금액을 런타임에서 0으로 만듭니다.
    /// 만화 컷(comicPanels)과 아침 독백은 그대로 둡니다 — 그쪽은 위약금이 아니라 스토리 연출입니다.
    /// </summary>
    private void DisableStoryPenaltiesIfNeeded()
    {
        if (StoryPenaltiesEnabled || storyEvents == null) return;

        int cleared = 0;
        int comicsCleared = 0;
        foreach (var evt in storyEvents)
        {
            if (evt == null || !evt.isPenalty) continue;

            // 정산 시점에 재생되는 위약금 만화(5·11일차)는 돈을 빼앗기는 장면 자체라
            // 차감이 없는 지금 틀면 앞뒤가 맞지 않습니다. 함께 죽입니다.
            // 6·16일차 만화는 아침 스토리 컷씬이라 위약금과 별개이므로 남깁니다.
            if (evt.triggerDay != 6 && evt.triggerDay != 16 && evt.comicPanels != null && evt.comicPanels.Count > 0)
            {
                evt.comicPanels.Clear();
                comicsCleared++;
            }

            evt.isPenalty = false;
            evt.penaltyAmount = 0f;
            cleared++;
        }

        // 16일차 독백의 첫 줄만 금액을 명시합니다. 나머지 두 줄(100배 레버리지·남은 5일)은
        // 위약금과 무관하게 성립하므로 첫 줄만 갈아끼워 스토리 기능을 유지합니다.
        if (day16Monologue != null && day16Monologue.Count > 0)
        {
            day16Monologue[0] = "슬슬 한계야... 이 속도로는 사채업자 마감일까지 절대 못 맞춰!! 멘탈이 부서질 것 같지만, 여기서 포기할 순 없어.";
        }

        if (cleared > 0)
            Debug.Log($"[GameManager] ⚠️ 스토리 위약금 비활성화 상태입니다. 위약금 {cleared}건을 0으로, 위약금 만화 {comicsCleared}건을 비웠습니다. (정기 지출은 그대로 유지)");
    }

    public bool IsGameLoaded { get; private set; }

    /// <summary>거래가 실제로 일어나는 씬. 이 밖에서는 시계가 멈추고 상태가 Paused가 됩니다.</summary>
    private static bool IsTradingScene(string sceneName)
        => sceneName == "GameScene" || sceneName == "tutorial" || sceneName == "SampleScene";

    private void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (this != Instance) return;

        if (IsTradingScene(scene.name))
        {
            // 거래 씬에 (다시) 들어왔습니다. 최초 진입 때 Start가 하던 준비를 그대로 반복합니다 —
            // 상주 오브젝트라 두 번째 진입부터는 Start가 호출되지 않기 때문입니다.
            // 이 경로가 없으면 방에서 거래를 개시할 때 세이브 복원이 통째로 누락됩니다.
            InitializeForTradingScene();
            return;
        }

        // 로딩 씬은 거쳐 가는 씬입니다. 여기서 상태를 건드리면 곧이어 Additive로 올라오는
        // 거래 씬의 준비 상태(Loading)를 덮어써 개장이 막힙니다.
        if (scene.name == "LoadingScene") return;

        // 거래 씬을 떠났으므로 다음 진입에서 다시 준비해야 합니다.
        initializedForCurrentScene = false;

        // 거래 씬 밖(요미의 방·월드맵·편의점): 시계와 체력·멘탈 감소를 멈춥니다.
        // Paused를 고른 이유는 저장과 잔고 변동은 계속 허용되기 때문입니다 —
        // 그 둘이 막히면 방에서의 저장이 조용히 실패합니다.
        if (currentState != GameState.Settlement && currentState != GameState.GameOver)
        {
            currentState = GameState.Paused;
        }

        // 정산 도중에 종료했다가 방으로 복귀한 경우 이어서 진행합니다.
        // GameScene 전용이던 재개 트리거(Start의 CurrentHour >= 24)가 여기서는 돌지 않습니다.
        if (RoomSettlementEnabled && (isSettlementProcessing || currentHour >= 24))
        {
            Debug.Log("[GameManager] 중단된 일일 정산을 방에서 재개합니다.");
            ProcessDailySettlementWithStory();
        }
    }

    // 게임 시작 시 한 번 실행
    private void Start()
    {
        if (p2pExternalMode) { currentDate = startDate; currentHour = 9; currentMinute = 0; currentBalance = startingBalance; StartOfDayEquity = startingBalance; currentState = GameState.Playing; EnsureDayTimeBackgroundController(); return; }

        // ⚠️ 여기서 활성 씬 이름으로 "거래 씬인가"를 판정하면 안 됩니다.
        //    GameScene은 Additive로 로드되고 SetActiveScene은 그 뒤에 호출되므로,
        //    이 시점의 활성 씬은 아직 LoadingScene입니다. 거래 씬을 거래 씬이 아니라고 판정해
        //    상태를 Paused로 덮으면 FinishLoadingAndStartPlaying이 (Loading일 때만 동작하므로)
        //    개장을 건너뛰고 차트가 멈춥니다.
        InitializeForTradingScene();
    }

    // 이 씬 진입에 대한 준비가 끝났는지. sceneLoaded와 Start가 모두 호출하므로 중복을 막습니다.
    private bool initializedForCurrentScene;

    /// <summary>
    /// 거래 씬 진입 시의 준비. <see cref="HandleSceneLoaded"/>와 <see cref="Start"/> 양쪽에서
    /// 호출되며, 먼저 도착한 쪽이 수행하고 나머지는 건너뜁니다.
    ///
    /// 둘 다 필요한 이유: 상주 오브젝트는 재진입에서 <c>Start</c>가 돌지 않고,
    /// 반대로 씬 로드를 거치지 않고 만들어지는 경우에는 <c>sceneLoaded</c>가 오지 않습니다.
    /// </summary>
    private void InitializeForTradingScene()
    {
        if (initializedForCurrentScene) return;
        initializedForCurrentScene = true;

        var saveManager = FXOverdose.Core.SaveLoadManager.Instance;
        // 스토리 모드는 요미의 방에서 시작하고, 방이 거래 개시 전에 저장 → 복원 예약을 겁니다.
        // 새 게임 초기화는 이미 PrepareNewGame()이 데이터에 심어두므로 이 경로로 들어와도 안전합니다.
        // 아래 StartNewGame()은 세이브를 쓰지 않는 무한·챌린지 모드 전용 경로로 남습니다.
        if (saveManager != null && saveManager.IsPendingLoad)
        {
            // 불러오기 모드 진입
            IsGameLoaded = true;
            currentState = GameState.Loading;
            currentEnding = EndingType.None;
            timeAccumulator = 0f;

            InitializeChoiceEventController();
            InitializeTraderLevelSystem();
            EnsureActiveItemEffectManager();
            EnsureCostumeManager();
            EnsureDynamicTimeRegulator();

            saveManager.ApplyLoadedDataToGame();
            Debug.Log("[GameManager] 불러오기 데이터 적용 완료. 게임 로딩 단계 진입.");
        }
        else
        {
            // 새 게임 시작
            StartNewGame();
        }

        EnsureDayTimeBackgroundController();
    }

    // 게임 실행 중 매 프레임 호출
    private void Update()
    {
        if(p2pExternalMode)return;
        // 게임 진행 상태가 아니면 시간을 흐르게 하지 않음
        if (currentState != GameState.Playing)
        {
            return;
        }

        UpdateGameTime();
    }

    // 새 게임의 초기 상태 설정
    public void StartNewGame()
    {
        var saveManager = FXOverdose.Core.SaveLoadManager.Instance;
        if (saveManager != null && saveManager.CurrentGameMode == FXOverdose.Core.GameMode.Story)
            startingBalance = FXOverdose.Core.StoryDifficultyTables.Get(saveManager.CurrentStoryDifficulty).StartingBalance;

        // 자산 초기화
        currentBalance = startingBalance;

        // 날짜와 시간 초기화
        startDate = FXOverdose.Core.GameCalendar.DefaultStartDate;
        currentDate = startDate;
        currentHour = 9;
        currentMinute = 0;

        // 게임 상태 초기화 -> 초기에는 차트 개장 준비 상태(Loading)로 대기
        currentState = GameState.Loading;
        currentEnding = EndingType.None;

        StartOfDayEquity = startingBalance;
        IsDailyPnlPartial = false;
        ResetDailyEquityHistory(startingBalance);

        TodayRegularDeduction = 0f;
        TodayRegularDeductionReason = "";

        // 시간 누적값 초기화
        timeAccumulator = 0f;

        // 돌발 선택 이벤트 컨트롤러(ChoiceEventController) 자동 부착 및 초기화
        InitializeChoiceEventController();
        InitializeTraderLevelSystem();
        // 초기 아이템 수량 세팅 (에너지드링크/파르페 5개, 약품들 2개)
        var inventory = UnityEngine.Object.FindAnyObjectByType<Inventory>(UnityEngine.FindObjectsInactive.Include);
        if (inventory != null)
        {
            inventory.ResetForNewGame();
        }

        // 동적 시간 완급 조절기(DynamicTimeRegulator) 부착
        EnsureDynamicTimeRegulator();

        // 액티브 아이템 효과 및 업그레이드 초기화
        EnsureActiveItemEffectManager();
        ActiveItemEffectManager.Instance?.ResetAll();
        EnsureCostumeManager();
        CostumeManager.Instance?.ResetAll();

        // AI 장기/단기 기억 시스템 초기화
        FXOverdose.AI.TraderMemoryManager.Instance?.ResetAll();

        // 주인공 및 스킬 레벨 시스템 초기화
        FXOverdose.Trading.TraderLevelSystem.Instance?.ResetLevels();

        Debug.Log("새 게임 시작 (차트 개장 로딩 단계 진입 - 초기 자본: $2,500)");
        
        // 새 게임 진입 시 초기 설정된 데이터를 현재 선택된 슬롯에 즉시 저장
        // SaveLoadManager는 Loading 상태일 때 저장을 막으므로 임시로 Playing 상태로 변경 후 저장합니다.
        GameState previousState = currentState;
        currentState = GameState.Playing;
        FXOverdose.Core.SaveLoadManager.Instance?.SaveCurrentGame();
        currentState = previousState;
    }

    private void EnsureActiveItemEffectManager()
    {
        if (ActiveItemEffectManager.Instance != null) return;
        ActiveItemEffectManager manager = GetComponent<ActiveItemEffectManager>();
        if (manager == null) gameObject.AddComponent<ActiveItemEffectManager>();
    }

    private void EnsureCostumeManager()
    {
        if (CostumeManager.Instance != null) return;
        CostumeManager manager = GetComponent<CostumeManager>();
        if (manager == null) gameObject.AddComponent<CostumeManager>();
    }

    private void EnsureBossManager()
    {
        var manager = GetComponent<FXOverdose.Core.BossManager>();
        if (manager == null)
            gameObject.AddComponent<FXOverdose.Core.BossManager>();
    }

    private void EnsureDynamicTimeRegulator()
    {
        if (FXOverdose.Core.DynamicTimeRegulator.Instance != null) return;
        var regulator = GetComponent<FXOverdose.Core.DynamicTimeRegulator>();
        if (regulator == null) gameObject.AddComponent<FXOverdose.Core.DynamicTimeRegulator>();
    }

    private void EnsureDayTimeBackgroundController()
    {
        if (GetComponent<FXOverdose.UI.DayTimeBackgroundController>() == null)
            gameObject.AddComponent<FXOverdose.UI.DayTimeBackgroundController>();
    }

    /// <summary>
    /// 새 게임 첫날과 불러온 현재 일차 모두 보스 데이터와 실제 AI 인스턴스를 동기화합니다.
    /// 기존에는 다음 날로 넘어갈 때만 보스를 생성해 1일차 보스가 표시되지 않았습니다.
    /// </summary>
    private void SyncBossForCurrentDay()
    {
        var bossManager = FXOverdose.Core.BossManager.Instance;
        if (bossManager == null) return;

        if (!bossManager.HasBossToday(CurrentDay))
        {
            bossManager.ClearBoss();
            return;
        }

        var status = TraderStatus.CanonicalInstance;
        float currentEquity = status != null ? status.GetTotalEquity() : currentBalance;
        if (!float.IsFinite(currentEquity) || currentEquity <= 0f)
            currentEquity = StartOfDayEquity > 0f ? StartOfDayEquity : startingBalance;

        // 구버전 세이브 등에서 데이터가 복구되지 않았을 때를 대비한 안전망으로, 
        // 무조건 아침 9시(조우 시점)의 자산을 우선 기준값으로 사용하도록 보정합니다.
        float encounterEquity = StartOfDayEquity > 0f ? StartOfDayEquity : currentEquity;

        bossManager.SpawnBossForDay(CurrentDay, encounterEquity);
    }

    private System.Collections.Generic.List<string> day6Monologue = new System.Collections.Generic.List<string> {
        "하아... 이런 푼돈 단타로는 사채업자 근처도 못 가! 더 큰 돈을 벌려면 레버리지 배율을 높여야 해.",
        "부지런히 거래해서 경험치를 쌓고 레벨을 올려야만 중고배율 레버리지가 해금된다고!",
        "지금부터는 어떻게든 레벨을 올려서 자산을 공격적으로 뻥튀기해야만 살아남을 수 있어. 가자!"
    };

    private System.Collections.Generic.List<string> day16Monologue = new System.Collections.Generic.List<string> {
        "말도 안 돼... 10만 달러 배상 청구 폭탄이라니!! 잔고가 박살나고 멘탈이 부서질 것 같지만, 여기서 포기할 순 없어.",
        "단숨에 복구하려면 '100배 풀레버리지'가 반드시 필요해. 아직 해금을 못했다면 어떻게든 최고 레벨(LV.9)까지 올려야 해!",
        "남은 시간은 단 5일. 모 아니면 도다. 가즈아아아!!!"
    };

    private System.Collections.IEnumerator PlayStoryMonologueAndWait(System.Collections.Generic.List<string> lines, System.Action onComplete)
    {
        var aiVisual = FindAnyObjectByType<FXOverdose.AI.AIVisualController>();
        if (aiVisual != null)
        {
            foreach (var line in lines)
            {
                aiVisual.DisplayDialogueBalloon(line, FXOverdose.AI.DialoguePriority.Critical, FXOverdose.AI.EventCategory.Tutorial);
                
                yield return new WaitForSeconds(0.5f);
                
                while (true)
                {
                    bool clicked = false;
#if ENABLE_INPUT_SYSTEM
                    if (UnityEngine.InputSystem.Pointer.current != null && UnityEngine.InputSystem.Pointer.current.press.wasPressedThisFrame)
                        clicked = true;
#else
                    if (Input.GetMouseButtonDown(0)) clicked = true;
#endif
                    if (clicked) break;
                    yield return null;
                }
                
                aiVisual.SetTutorialAdvanceIndicator(false);
                yield return new WaitForSeconds(0.1f);
            }
            
            aiVisual.HideDialogueBalloon();
        }
        else
        {
            yield return new WaitForSeconds(1.0f);
        }
        
        onComplete?.Invoke();
    }

    // 1일차 오프닝 컷씬과 독백은 제거되었습니다 (2026-08-14).
    // 엔딩 조건이 바뀌고 스토리라인이 개편될 예정이라, 옛 목표("20일 안에 100만 달러")를
    // 선언하던 인트로를 남겨두면 잘못된 목표를 안내하게 됩니다.
    // 새 인트로를 붙일 때는 튜토리얼 중 재생을 막을 수단(옛 skipCutscenes 인자)도 함께 되살리십시오.
    public void FinishLoadingAndStartPlaying()
    {
        if (currentState == GameState.Loading)
        {
            SyncBossForCurrentDay();
            var bossManager = FXOverdose.Core.BossManager.Instance;
            if (bossManager != null && bossManager.HasBossToday(CurrentDay) && bossManager.CurrentBoss != null)
            {
                StartCoroutine(WaitBossEntranceAndStartPlaying());
            }
            else
            {
                currentState = GameState.Playing;
                Debug.Log("[GameManager] 차트 엔진 예열 완료 -> 게임 정식 개장 (Playing)");
            }
            
            // 💡 [자동저장 개선] 24:00 마감 직후에 저장된 데이터를 로드한 경우, 즉시 일일 정산 프로세스로 진입합니다.
            if (IsGameLoaded && currentHour >= 24)
            {
                ProcessDailySettlementWithStory();
            }
            else
            {
                // 요미의 방에서 취침을 선택했다면 남은 하루를 건너뛰어 정산까지 보냅니다. (Q1)
                ConsumePendingSleepRequest();
            }
        }
    }

    // 돌발 선택 이벤트 컨트롤러 자동 연결 및 초기화
    private void InitializeChoiceEventController()
    {
        var choiceEventCtrl = GetComponent<FXOverdose.Events.ChoiceEventController>();
        if (choiceEventCtrl == null)
        {
            choiceEventCtrl = gameObject.AddComponent<FXOverdose.Events.ChoiceEventController>();
            Debug.Log("[GameManager] 💡 ChoiceEventController 컴포넌트 자동 부착 완료");
        }

        var marketEngine = GetComponent<FXOverdose.Trading.MarketSimulationEngine>();
        if (marketEngine == null) marketEngine = FindAnyObjectByType<FXOverdose.Trading.MarketSimulationEngine>();

        var tradingCtrl = GetComponent<FXOverdose.Trading.TradingController>();
        if (tradingCtrl == null) tradingCtrl = FindAnyObjectByType<FXOverdose.Trading.TradingController>();

        var status = TraderStatus.CanonicalInstance;

        choiceEventCtrl.Initialize(this, marketEngine, tradingCtrl, status);
    }

    private void InitializeTraderLevelSystem()
    {
        var levelSystem = GetComponent<FXOverdose.Trading.TraderLevelSystem>();
        if (levelSystem == null)
        {
            levelSystem = gameObject.AddComponent<FXOverdose.Trading.TraderLevelSystem>();
            Debug.Log("[GameManager] 💡 TraderLevelSystem 컴포넌트 자동 부착 완료");
        }
    }

    // 실제 시간을 게임 시간으로 변환
    private void UpdateGameTime()
    {
        // 프레임 사이에 흐른 실제 시간을 누적
        timeAccumulator += Time.deltaTime;

        // 설정한 시간이 지나면 게임 시간 1분 증가
        while (timeAccumulator >= secondsPerGameMinute && currentState == GameState.Playing)
        {
            timeAccumulator -= secondsPerGameMinute;
            AdvanceOneMinute();
        }
    }

    // 게임 시간을 1분 증가
    private void AdvanceOneMinute()
    {
        currentMinute++;

        // 60분이 되면 다음 시간으로 이동
        if (currentMinute >= 60)
        {
            currentMinute = 0;
            currentHour++;
        }

        // 해당 분의 차트/상태 계산을 먼저 완료한 뒤 정산 스냅샷을 만들 수 있도록 알립니다.
        OnGameMinuteAdvanced?.Invoke();

        // 구독자들이 이 분의 손익을 반영한 뒤에 자산을 표본으로 남깁니다. 순서를 앞당기면 한 틱 밀린 값이 찍힙니다.
        SampleDailyEquityIfDue();

        // 오늘 나갈 돈을 요미가 미리 알려줍니다.
        // Playing 전이 지점이 6곳(보스 연출·스토리 컷씬·로드·취침 복귀 등)에 흩어져 있어 그 전부에 걸면 취약합니다.
        // 여기는 아침 연출이 모두 끝나고 시간이 실제로 흐르기 시작한 뒤에만 도달하므로
        // 말풍선이 등장 연출과 겹치지 않고, 하루 1회가 보장됩니다.
        if (expenseAnnouncedForDay != CurrentDay)
        {
            expenseAnnouncedForDay = CurrentDay;
            AnnounceTodayExpenses();
        }

        // 24시가 되면 마지막 1분 데이터 반영 이후 일일 정산 모드 진입
        if (currentHour >= 24 && currentState == GameState.Playing)
        {
            // 💡 [24시 정산] 당일 정산 전에 보유 중인 포지션이 있다면 전량 강제 청산(정산)하여 손익을 확정합니다.
            var tradingCtrl = FindAnyObjectByType<FXOverdose.Trading.TradingController>();
            if (tradingCtrl != null && tradingCtrl.CurrentPosition != FXOverdose.Trading.TradingController.PositionType.None)
            {
                tradingCtrl.ClosePosition();
                Debug.Log("[GameManager] 24:00 마감 시간 도달. 당일 정산을 위해 열려 있는 포지션을 강제로 종료 및 수익/손실 확정.");
            }

            if (currentState == GameState.GameOver)
            {
                Debug.Log("[GameManager] 마감 강제 청산으로 인해 게임오버 발생. 일일 정산 및 저장을 중단합니다.");
                return;
            }

            // 곧 적용될 페널티와 정기 지출을 계산하여 파산 예정인지 미리 확인 (게임 오버 루프 방지)
            float expectedPenalty = 0f;
            if (FXOverdose.Core.SaveLoadManager.Instance != null && FXOverdose.Core.SaveLoadManager.Instance.CurrentGameMode == FXOverdose.Core.GameMode.Story)
            {
                var evt = storyEvents.Find(e => e.triggerDay == CurrentDay);
                if (CurrentDay != 16 && evt != null && evt.isPenalty && evt.penaltyAmount > 0)
                {
                    expectedPenalty = evt.penaltyAmount;
                }
            }

            float expectedDeduction = CalculateExpectedDeduction(CurrentDay, out string _);
            
            var status = TraderStatus.CanonicalInstance;
            float totalEquity = status != null ? status.GetTotalEquity() : currentBalance;
            float projectedEquity = totalEquity - expectedDeduction - expectedPenalty;
            float projectedBalance = currentBalance - expectedDeduction - expectedPenalty;

            bool willGameOver = projectedEquity <= 0f && projectedBalance <= 0f;

            // 💡 [자동저장 개선] 일일 정산 모드 진입 직전(24:00 마감)에 당일의 최종 상태를 자동 저장합니다.
            // 플레이어가 정산 화면을 보고 게임을 끄더라도 당일 진행 상황을 잃지 않게 됩니다.
            // 단, 정산 후 파산이 확정된 상태라면 저장하지 않아 아침 9시 시점부터 다시 시작할 수 있게 합니다.
            if (!willGameOver && FXOverdose.Core.SaveLoadManager.Instance != null)
            {
                bool saved = FXOverdose.Core.SaveLoadManager.Instance.SaveCurrentGame();
                if (saved) Debug.Log("[GameManager] 24:00 마감 직전(일일 정산 진입 전) 자동 저장 완료.");
            }
            else if (willGameOver)
            {
                Debug.Log("[GameManager] 정기 지출/페널티 적용 후 파산이 확정되어 자동 저장을 생략합니다.");
            }

            ProcessDailySettlementWithStory();
        }
    }

    private float CalculateExpectedDeduction(int day, out string deductionReason)
    {
        float deduction = 0f;
        deductionReason = "";

        if (day == 3) { deduction = 5000f; deductionReason = "트레이딩 플랫폼 프리미엄 구독료"; }
        else if (day == 7) { deduction = 12000f; deductionReason = "불법 거래소 단속 회피를 위한 로비 자금"; }
        else if (day == 11) { deduction = 20000f; deductionReason = "의문의 해킹 공격 복구 비용"; }
        else if (day == 15) { deduction = 60000f; deductionReason = "최종 결전을 앞둔 장비 오버클럭 세팅비"; }
        else if (day == 18) { deduction = 150000f; deductionReason = "작전 세력에게 지불할 정보 수수료"; }
        else if (day >= 21 && (day - 21) % 3 == 0)
        {
            // Endless 모드: 21일부터 3일마다 1.3배씩 증가 (18일차 금액인 150,000 기준)
            int cycles = (day - 21) / 3 + 1;
            deduction = 150000f * Mathf.Pow(1.3f, cycles);
            deductionReason = "시스템 유지보수 비용 지속 청구";
        }
        
        return deduction;
    }

    // 오늘 지출을 예고한 일차. 하루 1회만 알리기 위한 표식입니다.
    // 저장하지 않으므로 중간에 다시 접속하면 한 번 더 알려줍니다 — 리마인더로 유용합니다.
    private int expenseAnnouncedForDay = -1;

    /// <summary>
    /// 오늘 빠져나갈 돈을 트레이딩 시작 시점에 요미가 미리 알려줍니다.
    ///
    /// 16일차는 대상이 아닙니다. 그날 위약금은 아침 컷씬 직후에 이미 차감되므로
    /// "오늘 밤 나갈 거야"라고 하면 거짓말이 됩니다. (day16Monologue가 사후 반응을 담당합니다)
    /// </summary>
    private void AnnounceTodayExpenses()
    {
        if (FXOverdose.Core.SaveLoadManager.Instance == null
            || FXOverdose.Core.SaveLoadManager.Instance.CurrentGameMode != FXOverdose.Core.GameMode.Story)
        {
            return;
        }

        float deduction = CalculateExpectedDeduction(CurrentDay, out string reason);

        // 아침에 이미 차감되는 16일차 위약금은 예고에서 제외합니다.
        float penalty = 0f;
        if (CurrentDay != 16)
        {
            var evt = storyEvents.Find(e => e.triggerDay == CurrentDay);
            if (evt != null && evt.isPenalty && evt.penaltyAmount > 0f) penalty = evt.penaltyAmount;
        }

        if (deduction <= 0f && penalty <= 0f) return;

        string line;
        if (deduction > 0f && penalty > 0f)
        {
            line = $"오빠, 오늘 최악이야... {reason} ${deduction:N0}에 위약금 ${penalty:N0}까지, "
                 + $"합쳐서 ${deduction + penalty:N0}이 밤에 빠져나가!";
        }
        else if (deduction > 0f)
        {
            line = $"오빠, 오늘 밤에 {reason}로 ${deduction:N0} 빠져나가. 그 전에 벌어놔야 해!";
        }
        else
        {
            line = $"오늘 밤 위약금 ${penalty:N0} 나가는 날이야... 각오하고 시작하자.";
        }

        var visual = FindAnyObjectByType<FXOverdose.AI.AIVisualController>();
        visual?.DisplayDialogueBalloon(line, FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.EventCategory.General);
        Debug.Log($"[GameManager] 💸 {CurrentDay}일차 지출 예고: 정기 {deduction:N0} / 위약금 {penalty:N0}");
    }

    private void ProcessDailySettlementWithStory()
    {
        isSettlementProcessing = true; // 파산 판정 유예
        currentState = GameState.Settlement;

        TodayEvent = storyEvents.Find(e => e.triggerDay == CurrentDay);

        // Day 1은 게임 시작 시 이미 재생했으므로 24:00에는 패스
        if (CurrentDay == 1)
        {
            Debug.Log($"[GameManager] 1일차 24:00 종료. 일일 정산 대기 상태 진입.");
            OnDayEnded?.Invoke();
            return;
        }

        // 페널티 적용 (Day 16 페널티는 아침으로 이동했으므로 예외)
        if (FXOverdose.Core.SaveLoadManager.Instance != null && FXOverdose.Core.SaveLoadManager.Instance.CurrentGameMode == FXOverdose.Core.GameMode.Story)
        {
            if (CurrentDay != 16 && TodayEvent != null && TodayEvent.isPenalty && TodayEvent.penaltyAmount > 0)
            {
                currentBalance -= TodayEvent.penaltyAmount; 
                if (TraderStatus.CanonicalInstance != null)
                    TraderStatus.CanonicalInstance.AdjustPeakBalanceForExpenditure(TodayEvent.penaltyAmount);
                Debug.Log($"[GameManager] 스토리 이벤트 위약금 강제 차감: -{TodayEvent.penaltyAmount:N0} (잔고: {currentBalance:N0})");
            }
        }

        // 💡 [새 기능] 특정 일차 정기 지출 시스템 (인플레이션형)
        float deduction = CalculateExpectedDeduction(CurrentDay, out string deductionReason);

        TodayRegularDeduction = deduction;
        TodayRegularDeductionReason = deductionReason;

        if (deduction > 0f)
        {
            currentBalance -= deduction;
            if (TraderStatus.CanonicalInstance != null)
                TraderStatus.CanonicalInstance.AdjustPeakBalanceForExpenditure(deduction);
            Debug.Log($"[GameManager] 정기 지출 발생: {deductionReason} (-${deduction:N0}) -> 남은 잔고: ${currentBalance:N0}");
        }

        // 컷툰 재생 또는 바로 정산
        if (FXOverdose.Core.SaveLoadManager.Instance != null && FXOverdose.Core.SaveLoadManager.Instance.CurrentGameMode == FXOverdose.Core.GameMode.Story)
        {
            // Day 6, Day 16 컷씬은 아침으로 이동했으므로 예외
            //
            // 요미의 방에서 정산할 때는 그 씬에 컷씬 Canvas가 없습니다.
            // Instance만 보면 null이라 컷씬이 통째로 건너뛰어지므로, 없으면 만들어 씁니다.
            var comic = FXOverdose.UI.ComicCutsceneController.Instance
                        ?? FXOverdose.UI.ComicCutsceneController.GetOrCreateRuntime(
                               UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            if (CurrentDay != 6 && CurrentDay != 16 && TodayEvent != null && TodayEvent.comicPanels != null && TodayEvent.comicPanels.Count > 0 && comic != null)
            {
                comic.PlayCutscene(TodayEvent.comicPanels, () => {
                    Debug.Log($"[GameManager] {CurrentDay}일차 24:00 종료. 일일 정산 대기 상태 진입.");
                    OnDayEnded?.Invoke();
                });
                return;
            }
        }

        Debug.Log($"[GameManager] {CurrentDay}일차 24:00 종료. 일일 정산 대기 상태 진입.");
        OnDayEnded?.Invoke();
    }

    // 일일 정산 화면에서 '다음날 진행하기' 호출 시 실행
    public void ProceedToNextDay()
    {
        if (currentState != GameState.Settlement) return;

        isSettlementProcessing = false;

        var status = TraderStatus.CanonicalInstance;
        float totalEquity = status != null ? status.GetTotalEquity() : currentBalance;

        var bossManager = FXOverdose.Core.BossManager.Instance;
        if (bossManager != null && bossManager.HasBossToday(CurrentDay) && bossManager.CurrentBoss != null)
        {
            var boss = bossManager.CurrentBoss;
            bool isWin = bossManager.IsBossBankrupt;
            
            if (!isWin)
            {
                isWin = totalEquity > bossManager.BossCurrentAsset;
            }

            if (isWin)
            {
                if (boss.IsFinalBoss)
                {
                    EndGame(EndingType.Success);
                    return;
                }
                else
                {
                    System.Collections.Generic.List<string> winReaction = new System.Collections.Generic.List<string> {
                        $"[{boss.Name} 격파 결과]", 
                        $"흐응~ 고작 그 정도 자본으로 나한테 덤빈 거야?", 
                        $"역시 넌 평생 알바생 피나 빨아먹는 하수일 뿐이야!"
                    };
                    StartCoroutine(PlayStoryMonologueAndWait(winReaction, () => {
                        FinalizeProceedToNextDay();
                    }));
                    return;
                }
            }
            else
            {
                System.Collections.Generic.List<string> loseReaction = new System.Collections.Generic.List<string> {
                    $"[{boss.Name} 패배 결과]",
                    $"말도 안 돼... 내가, 내가 졌다고...?",
                    $"이건 다 저 차트가 조작된 탓이야!!! 다시 매매해야 해!!!"
                };
                StartCoroutine(PlayStoryMonologueAndWait(loseReaction, () => {
                    EndGame(EndingType.Overdose);
                }));
                return;
            }
        }

        // Day 20 엔딩 백업. BossManager가 없거나 최종 보스 스폰에 실패했을 때만 도달합니다.
        // 이 분기가 없으면 그런 예외 상황에서 21일차로 넘어가 엔딩이 영영 나지 않습니다.
        //
        // 종전에는 $1,000,000 고정값을 썼는데, 목표 자산 성공 조건이 철폐된 뒤로는
        // 근거 없는 숫자였습니다(당시 targetBalance는 $100,000으로 10배 어긋나 있었습니다).
        // 실제 승리 조건인 "최종 보스 자산 초과"를 그대로 흉내 냅니다.
        //
        // StoryDayLimitEnabled를 끄면 이 판정이 통째로 사라져 21일차 이후로 계속 진행합니다.
        // 그 상태에서는 성공 엔딩에 도달할 수 없습니다 — 스위치 주석 참고.
        if (DayLimitActive && CurrentDay == StoryLastDay)
        {
            float finalBossAsset = StartOfDayEquity * FinalBossAssetScale;
            if (totalEquity > finalBossAsset) EndGame(EndingType.Success);
            else EndGame(EndingType.Bankruptcy);
            return;
        }

        FinalizeProceedToNextDay();
    }

    private void FinalizeProceedToNextDay()
    {
        // 정산 창 닫힐 때 유예된 파산 판정 일괄 검사
        CheckEnding();
        if (currentState == GameState.GameOver) return; // 파산 당했으면 진행 불가

        currentHour = 9;
        currentMinute = 0;
        // 월말·연말 경계는 AddDays가 처리합니다.
        currentDate = currentDate.AddDays(1);

        var status = TraderStatus.CanonicalInstance;
        StartOfDayEquity = status != null ? status.GetTotalEquity() : currentBalance;
        IsDailyPnlPartial = false;

        // 스파크라인도 같은 기준선에서 다시 시작합니다.
        // OnDayEnded(24:00)가 아니라 여기여야 합니다 — 그쪽은 정산 화면이 뜨기 전이라
        // 거기서 비우면 정산 화면이 그날 그래프를 보여주는 도중에 그래프가 사라집니다.
        ResetDailyEquityHistory(StartOfDayEquity);

        TodayRegularDeduction = 0f;
        TodayRegularDeductionReason = "";

        // 다음 날로 넘어갈 때 체력과 멘탈을 모두 최대로 회복
        if (status != null)
        {
            status.ChangeHealth(9999f);
            status.ChangeMental(9999f, "NewDayReset");
            Debug.Log("[GameManager] 일일 정산 후 다음 날 진입: 체력과 멘탈이 최대로 회복되었습니다.");
        }

        Debug.Log($"{CurrentDay}일차 시작 (09:00)");

        // 💡 [데이팅 파트 하루 리셋] 일차를 올리는 곳은 여기 하나뿐이므로 시간 슬롯 리필도 여기서 합니다. (SV-A8)
        //    DatingTimeManager의 일차는 이 값의 미러가 됩니다. (S12)
        FXOverdose.DatingSim.Core.DatingTimeManager.Instance?.SyncToNewDay(CurrentDay);

        // ⭐ 전날 기억 압축 및 저중요도 Pruning 실행
        FXOverdose.AI.TraderMemoryManager.Instance?.OnDayAdvanced(CurrentDay);

        // 💡 [차트 리셋] 다음 날로 넘어갈 때 새로운 하루가 시작되도록 차트를 새로 고침(프리웜)합니다.
        var marketEngine = FindAnyObjectByType<FXOverdose.Trading.MarketSimulationEngine>();
        if (marketEngine != null)
        {
            marketEngine.ResetEngine(marketEngine.CurrentPrice);
            Debug.Log("[GameManager] 다음 날로 넘어감에 따라 차트 엔진(과거 기록)을 리셋 및 새로운 차트 프리웜 완료.");
            
            // 💡 리셋된 엔진의 마켓을 다시 개장합니다.
            marketEngine.OpenMarketAfterLoading();
        }
        else
        {
            InvalidateSavedChartForNewDay();
        }

        var bossManager = FXOverdose.Core.BossManager.Instance;
        bool hasBossMorningEvent = bossManager != null && bossManager.HasBossToday(CurrentDay);
        FXOverdose.Core.BossData pendingBossData = hasBossMorningEvent ? bossManager.GetBossData(CurrentDay) : null;
        
        if (!hasBossMorningEvent && bossManager != null)
        {
            bossManager.ClearBoss();
        }

        bool hasStoryMorningEvent = false;
        System.Action storyMorningAction = null;

        if (FXOverdose.Core.SaveLoadManager.Instance != null && FXOverdose.Core.SaveLoadManager.Instance.CurrentGameMode == FXOverdose.Core.GameMode.Story)
        {
            if (CurrentDay == 6 || CurrentDay == 16)
            {
                var evt = storyEvents.Find(e => e.triggerDay == CurrentDay);
                if (evt != null)
                {
                    hasStoryMorningEvent = true;
                    storyMorningAction = () => {
                        System.Action applyPenaltyAndMonologue = () => {
                            if (CurrentDay == 16 && evt.isPenalty && evt.penaltyAmount > 0)
                            {
                                currentBalance -= evt.penaltyAmount;
                                if (TraderStatus.CanonicalInstance != null) TraderStatus.CanonicalInstance.AdjustPeakBalanceForExpenditure(evt.penaltyAmount);
                                Debug.Log($"[GameManager] {CurrentDay}일차 아침 페널티 차감: -{evt.penaltyAmount}");
                            }
                            
                            var monologue = CurrentDay == 6 ? day6Monologue : day16Monologue;
                            StartCoroutine(PlayStoryMonologueAndWait(monologue, () => {
                                Debug.Log($"[GameManager] {CurrentDay}일차 스토리 시작 컷씬 및 독백 종료.");
                                
                                if (hasBossMorningEvent)
                                {
                                    PlayBossMorningSequence(pendingBossData, bossManager);
                                }
                                else
                                {
                                    currentState = GameState.Playing;
                                }
                            }));
                        };

                        if (evt.comicPanels != null && evt.comicPanels.Count > 0 && FXOverdose.UI.ComicCutsceneController.Instance != null)
                        {
                            FXOverdose.UI.ComicCutsceneController.Instance.PlayCutscene(evt.comicPanels, applyPenaltyAndMonologue);
                        }
                        else
                        {
                            applyPenaltyAndMonologue();
                        }
                    };
                }
            }
        }

        if (hasStoryMorningEvent)
        {
            currentState = GameState.Paused;
            storyMorningAction?.Invoke();
        }
        else if (hasBossMorningEvent)
        {
            currentState = GameState.Paused;
            PlayBossMorningSequence(pendingBossData, bossManager);
        }
        else
        {
            currentState = GameState.Playing;
        }

        // 일일 정산(다음날 진입) 시점에 현재 게임 상태 자동 저장
        if (FXOverdose.Core.SaveLoadManager.Instance != null)

        {
            bool saved = FXOverdose.Core.SaveLoadManager.Instance.SaveCurrentGame();
            if (saved) Debug.Log("[GameManager] 일일 정산 시점 자동 저장 완료.");
        }

        if (remainingFastForwardMinutes > 0)
        {
            int resumeMinutes = remainingFastForwardMinutes;
            remainingFastForwardMinutes = 0;
            AdvanceGameMinutes(resumeMinutes);
        }

        // 정산이 끝나면 다음 날 아침을 요미의 방에서 시작합니다. (SV-B11 / Q5)
        // 아침 컷씬(스토리 6·16일차, 보스전)이 있는 날은 그 연출이 트레이딩 파트의 도입이므로
        // GameScene에 머무릅니다.
        // ponytail: 컷씬 종료 콜백에 방 복귀를 얹으면 모든 날이 방에서 시작하게 됩니다.
        //           연출 순서 검토가 끝난 뒤에 옮기십시오.
        if (!hasStoryMorningEvent && !hasBossMorningEvent)
        {
            ReturnToYomiRoomForNewMorning();
        }
        // 엔드리스·챌린지는 방으로 가지 않고 GameScene에서 그대로 다음 날을 이어갑니다.
        // (ReturnToYomiRoomForNewMorning이 모드를 보고 스스로 물러납니다)
    }

    /// <summary>
    /// 차트 엔진이 없는 씬(요미의 방·월드맵)에서 하루가 넘어갈 때, <b>세이브의 차트 상태를 무효화</b>합니다.
    ///
    /// 일차 전환은 원래 엔진을 리셋하고 프리웜하는데, 방에는 엔진이 없어 그 처리가 조용히 건너뛰어집니다.
    /// 그런데 전환 직후 자동 저장이 돌기 때문에 <b>전날 캔들이 그대로 디스크에 남고</b>, 다음 날
    /// GameScene에 들어가면 어제 차트가 복원된 채 하루가 시작됩니다.
    ///
    /// 세이브 쪽을 비워 두면 다음 진입에서 새 하루의 차트를 프리웜합니다.
    /// </summary>
    private void InvalidateSavedChartForNewDay()
    {
        var data = FXOverdose.Core.SaveLoadManager.Instance?.CurrentData;
        if (data == null) return;

        data.ChartHistories?.Clear();
        data.MarketLastUpdatedDay = -1;   // 복원 측이 CurrentDay 기준으로 다시 판정합니다.
        data.MarketTotalMinutes = 0;
        Debug.Log("[GameManager] 차트 엔진이 없는 씬에서 하루가 넘어가 세이브의 차트 기록을 비웠습니다. (다음 거래 진입 시 새로 프리웜)");
    }

    /// <summary>
    /// 다음 날 아침을 요미의 방에서 시작하도록 씬을 전환합니다. (SV-B11)
    ///
    /// <b>스토리 모드 전용입니다.</b> 엔드리스·챌린지는 데이팅 파트도 세이브도 쓰지 않으므로
    /// 방으로 보내면 갈 곳이 없습니다. 그 모드들은 GameScene에서 그대로 다음 날을 이어갑니다.
    ///
    /// 판정을 호출부가 아니라 여기서 하는 이유는, 아침 복귀 경로가 나중에 늘어나도
    /// 모드 조건이 한 곳에 남아 있게 하기 위해서입니다.
    /// </summary>
    private void ReturnToYomiRoomForNewMorning()
    {
        var saveManager = FXOverdose.Core.SaveLoadManager.Instance;
        if (saveManager == null || saveManager.CurrentGameMode != FXOverdose.Core.GameMode.Story)
        {
            currentState = GameState.Playing;
            Debug.Log($"[GameManager] {CurrentDay}일차 아침 — {saveManager?.CurrentGameMode.ToString() ?? "모드 미상"} 모드이므로 거래 화면에서 이어갑니다.");
            return;
        }

        // 방에서 정산했다면 이미 방에 있습니다. 같은 씬을 다시 로드하면 씬 전환 0회라는 이점이 사라지고,
        // 방금 마친 정산 직후에 로딩 화면이 한 번 더 끼어듭니다.
        string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (activeScene == "YomiRoomScene")
        {
            currentState = GameState.Paused;
            Debug.Log($"[GameManager] 🌅 {CurrentDay}일차 아침 — 방에 그대로 머무릅니다. (씬 전환 없음)");
            return;
        }

        Debug.Log($"[GameManager] 🌅 {CurrentDay}일차 아침 — 요미의 방에서 하루를 시작합니다.");
        FXOverdose.UI.LoadingScreenController.TargetSceneToLoad = "YomiRoomScene";
        UnityEngine.SceneManagement.SceneManager.LoadScene("LoadingScene");
    }

    private void PlayBossMorningSequence(FXOverdose.Core.BossData boss, FXOverdose.Core.BossManager bossManager)
    {
        System.Collections.Generic.List<string> bossIntro = new System.Collections.Generic.List<string> {
            $"[{boss.Name} 등장!]",
            boss.Description,
            $"감히 내 앞길을 막아? 내 트레이딩으로 네 놈의 영혼까지 털어주겠어!"
        };
        StartCoroutine(PlayStoryMonologueAndWait(bossIntro, () => {
            // 보스 스폰을 이 시점으로 지연시킴 (보스 등장 연출 UI 트리거)
            bossManager.SpawnBossForDay(CurrentDay, StartOfDayEquity);
            
            // 보스 등장 연출 애니메이션(약 2초) 동안 시장이 멈춰있도록 대기 후 게임 재개
            StartCoroutine(WaitBossEntranceAndPlay());
        }));
    }

    private System.Collections.IEnumerator WaitBossEntranceAndStartPlaying()
    {
        yield return new UnityEngine.WaitForSecondsRealtime(2.1f);
        currentState = GameState.Playing;
        Debug.Log("[GameManager] 보스 등장 연출 종료. 차트 엔진 예열 완료 -> 게임 정식 개장 (Playing)");
    }

    private System.Collections.IEnumerator WaitBossEntranceAndPlay()
    {
        yield return new UnityEngine.WaitForSecondsRealtime(2.1f);
        ResumeGame(); // ResumeGame을 호출하여 혹시 남아있는 FastForward(고속 진행)도 처리
    }

    /// <summary>
    /// 요미의 방에서 취침을 선택하고 GameScene에 진입했음을 알리는 요청 플래그입니다. (Q1 / SV-A8)
    /// GameScene이 개장하면 소비되어 남은 하루를 건너뛰고 기존 일일 정산 루틴을 태웁니다.
    /// </summary>
    public static bool PendingSleepThroughToday = false;

    /// <summary>
    /// 요미의 방에서 취침했을 때 <b>그 자리에서</b> 하루를 마감하고 일일 정산에 진입합니다. (씬 전환 없음)
    ///
    /// 남은 시간을 1분씩 시뮬레이션하지 않고 시각만 24:00으로 옮깁니다. 취침 시점에는
    /// 열린 포지션이 있을 수 없고(방 → GameScene은 단방향), 체력·멘탈은 다음 날 어차피 최대로
    /// 회복되며, 캔들은 하루가 바뀌면 리셋되기 때문입니다.
    /// </summary>
    /// <returns>정산에 진입했으면 true. false면 호출부가 종전처럼 GameScene으로 넘겨야 합니다.</returns>
    public bool TrySettleFromRoom()
    {
        if (!RoomSettlementEnabled) return false;
        if (currentState == GameState.GameOver) return false;

        // 정산 경로는 Playing 상태를 전제로 만들어져 있습니다(24:00 판정, 강제 청산 등).
        // 방에서는 Paused로 대기 중이므로 잠깐 Playing으로 되돌린 뒤 시각을 마감으로 옮깁니다.
        currentState = GameState.Playing;
        AdvanceClockWithoutSimulation(24 * 60);

        Debug.Log($"[GameManager] 🛏️ 요미의 방에서 하루를 마감합니다. ({CurrentDay}일차 → 일일 정산)");
        ProcessDailySettlementWithStory();
        return true;
    }

    /// <summary>취침 요청이 있으면 남은 하루를 고속으로 흘려보내 24:00 정산에 도달시킵니다.</summary>
    private void ConsumePendingSleepRequest()
    {
        if (!PendingSleepThroughToday) return;
        PendingSleepThroughToday = false;

        int remainingMinutes = (24 * 60) - (currentHour * 60 + currentMinute);
        if (remainingMinutes <= 0) return;

        Debug.Log($"[GameManager] 🛏️ 취침 요청 소비: 남은 {remainingMinutes}분을 건너뛰고 일일 정산으로 진행합니다.");
        AdvanceGameMinutes(remainingMinutes);
    }

    /// <summary>
    /// 시장을 돌리지 않고 <b>시각만</b> 앞으로 옮깁니다. 24:00을 넘지 않습니다.
    ///
    /// <see cref="AdvanceGameMinutes"/>와의 차이가 중요합니다 — 그쪽은 1분씩 시뮬레이션하며
    /// 캔들·이벤트·체력 감소를 발생시키고, <b>Playing이 아니면 즉시 멈춰 남은 분을 쌓아둡니다.</b>
    /// 요미의 방·월드맵은 Playing이 아니므로 거기서 그 메서드를 쓰면 시계가 전진하지 않고
    /// 쌓인 분이 나중에 거래 개시 시점에 한꺼번에 터집니다.
    ///
    /// 방에서는 시장이 돌지 않으니 분당 이벤트를 발행할 이유도 없습니다. 이쪽이 맞습니다.
    /// </summary>
    /// <returns>실제로 전진한 분. 24:00에 걸려 잘렸으면 요청보다 작습니다.</returns>
    public int AdvanceClockWithoutSimulation(int minutes)
    {
        if (minutes <= 0) return 0;

        int before = currentHour * 60 + currentMinute;
        int after = Mathf.Min(before + minutes, 24 * 60);
        currentHour = after / 60;
        currentMinute = after % 60;
        return after - before;
    }

    // 스킬 공부 기믹 등으로 여러 분(시간)이 한 번에 경과할 때 호출
    public void AdvanceGameMinutes(int minutes)
    {
        if (minutes <= 0) return;
        IsFastForwardingTime = true;
        remainingFastForwardMinutes += minutes;

        while (remainingFastForwardMinutes > 0)
        {
            if (currentState != GameState.Playing || interruptFastForwardForOverdose)
            {
                // 돌발 이벤트 팝업 등으로 게임이 일시정지(Paused)되었으면 즉시 루프를 중단하여 남은 시간을 보존 (이벤트 스킵 방지)
                Debug.Log($"[GameManager] ⏸️ 고속 시간 진행 중 일시정지 감지! (남은 고속 진행 시간: {remainingFastForwardMinutes}분 보존 및 대기)");
                break;
            }
            remainingFastForwardMinutes--;
            AdvanceOneMinute();
        }

        if (remainingFastForwardMinutes <= 0 && !interruptFastForwardForOverdose)
        {
            IsFastForwardingTime = false;
            OnFastForwardEnded?.Invoke();
        }
        else if (interruptFastForwardForOverdose)
        {
            interruptFastForwardForOverdose = false;
        }
    }

    public void PauseFastForwardForOverdose()
    {
        if (IsFastForwardingTime && remainingFastForwardMinutes > 0)
        {
            interruptFastForwardForOverdose = true;
            preservedFastForwardMinutes = remainingFastForwardMinutes;
            remainingFastForwardMinutes = 0; 
            IsFastForwardingTime = false;
            Debug.Log($"[GameManager] ⏸️ Overdose 발생으로 고속 시간 진행 중단. (남은 {preservedFastForwardMinutes}분 보존)");
        }
    }

    public void ResumePreservedFastForward()
    {
        if (preservedFastForwardMinutes > 0)
        {
            Debug.Log($"[GameManager] ▶️ Overdose 해제. 보존된 남은 고속 진행 시간({preservedFastForwardMinutes}분) 이어서 진행");
            int resumeMinutes = preservedFastForwardMinutes;
            preservedFastForwardMinutes = 0;
            AdvanceGameMinutes(resumeMinutes);
        }
    }

    // 자산을 증가하거나 감소시키는 함수
    public void ChangeBalance(float amount)
    {
        // 게임 진행 중이거나 일시정지(이벤트/상점 팝업 등) 상태일 때 자산 변경 가능
        if (currentState != GameState.Playing && currentState != GameState.Paused)
        {
            return;
        }

        // 양수면 증가, 음수면 감소
        currentBalance += amount;

        Debug.Log(
            $"자산 변동: {amount:N0}, 현재 자산: {currentBalance:N0}"
        );
        
        FXOverdose.Core.AchievementManager.Instance?.RecordPeakBalance(currentBalance);

        // 자산 변경 후 엔딩 조건 검사
        CheckEnding();
    }

    // 상점 등에서 자산 지출을 요청할 때 사용합니다.
    // 상점으로 게임이 일시정지된 상태에서도 구매할 수 있습니다.
    public bool TrySpendBalance(float amount)
    {
        if (currentState == GameState.GameOver)
        {
            return false;
        }

        if (amount <= 0f)
        {
            Debug.LogWarning("지출 금액은 0보다 커야 합니다.");
            return false;
        }

        if (currentBalance < amount)
        {
            AudioManager.Play(AudioCue.InsufficientBalance);
            Debug.Log("자산이 부족합니다.");
            return false;
        }

        currentBalance -= amount;
        if (TraderStatus.CanonicalInstance != null)
        {
            TraderStatus.CanonicalInstance.AdjustPeakBalanceForExpenditure(amount);
        }
        Debug.Log($"자산 지출: -{amount:N0}, 현재 자산: {currentBalance:N0}");
        CheckEnding();
        return true;
    }

    // 성공 또는 파산 조건 확인
    private void CheckEnding()
    {
        if (isSettlementProcessing) return; // 정산 중에는 파산 판정 유예

        var status = TraderStatus.CanonicalInstance;
        bool isOverdose = status != null && (status.CurrentMentalState == TraderStatus.MentalState.Overdose || status.CurrentMental <= 0f);

        // [목표 자산 성공 조건 철폐] 엔딩 방향 개편에 따라 targetBalance 도달 시 자동 클리어 조건을 철폐합니다.
        // 올인(100% 증거금) 진입 시 현금 잔고(currentBalance)가 0원이 되어도 증거금에 자산이 살아있으므로,
        // 총 자산(Total Equity)과 현금 잔고가 모두 0 이하인 진짜 청산/파산 시점에만 게임오버를 트리거합니다.
        float totalEquity = status != null ? status.GetTotalEquity() : currentBalance;
        if (totalEquity <= 0f && currentBalance <= 0f)
        {


            currentBalance = 0f;
            if (isOverdose)
            {
                EndGame(EndingType.Overdose);
            }
            else
            {
                EndGame(EndingType.Bankruptcy);
            }
        }
    }

    /// <summary>청산처럼 잔고 변경 이벤트 없이 포지션 자산이 소멸한 직후 엔딩 조건을 다시 검사합니다.</summary>
    public void EvaluateEndingConditions()
    {
        if (currentState != GameState.Playing && currentState != GameState.Paused)
        {
            return;
        }

        CheckEnding();
    }

    // 멘탈 시스템에서 호출할 Overdose 엔딩 함수
    public void TriggerOverdoseEnding()
    {
        if (currentState != GameState.Playing)
        {
            return;
        }

        EndGame(EndingType.Overdose);
    }

    // 게임 종료 처리
    private void EndGame(EndingType ending)
    {
        currentEnding = ending;
        currentState = GameState.GameOver;

        Debug.Log($"게임 종료: {ending}");
        
        FXOverdose.Core.AchievementManager.Instance?.RecordEnding(ending.ToString());

        System.Collections.Generic.List<Sprite> panels = null;
        switch (ending)
        {
            case EndingType.Success: panels = successEndingComic; break;
            case EndingType.Bankruptcy: panels = bankruptcyEndingComic; break;
            case EndingType.Overdose: panels = overdoseEndingComic; break;
        }

        if (panels != null && panels.Count > 0 && FXOverdose.UI.ComicCutsceneController.Instance != null)
        {
            FXOverdose.UI.ComicCutsceneController.Instance.PlayCutscene(panels, () => {
                OnGameOverEvent?.Invoke(ending);
            });
        }
        else
        {
            // 게임 오버 이벤트 발생 (UI 연동)
            OnGameOverEvent?.Invoke(ending);
        }
    }

    // 게임 일시정지
    public void PauseGame()
    {
        if (currentState == GameState.Playing)
        {
            currentState = GameState.Paused;
        }
    }

    // 게임 다시 시작
    public void ResumeGame()
    {
        if (currentState == GameState.Paused)
        {
            currentState = GameState.Playing;

            if (remainingFastForwardMinutes > 0)
            {
                Debug.Log($"[GameManager] ▶️ 게임 재개 -> 보존된 남은 고속 경과 시간({remainingFastForwardMinutes}분) 이어서 진행");
                int resumeMinutes = remainingFastForwardMinutes;
                remainingFastForwardMinutes = 0;
                AdvanceGameMinutes(resumeMinutes);
            }
        }
    }
}
