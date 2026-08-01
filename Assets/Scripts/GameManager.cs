using System;
using FXOverdose.Core;
using UnityEngine;

public class GameManager : MonoBehaviour
{
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
    [SerializeField] private float startingBalance = 10000000f; // UI/상점 검수용 시작 자산
    [SerializeField] private float targetBalance = 100000f;  // 목표 자산 (엔딩 철폐되어 단순 표기용)
    [SerializeField] private float currentBalance;           // 현재 자산

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
    [SerializeField] private int currentDay = 1;    // 현재 일차
    [SerializeField] private int currentHour = 9;   // 현재 시간
    [SerializeField] private int currentMinute = 0; // 현재 분

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
    public float TargetBalance => targetBalance;
    public int CurrentDay => currentDay;
    public int CurrentHour => currentHour;
    public int CurrentMinute => currentMinute;
    public float SecondsPerGameMinute => secondsPerGameMinute;

    // 저장 데이터를 불러올 때 당일 손익 기준 자산을 복구합니다.
    // 구버전 세이브에는 값이 없으므로 현재 잔고를 안전한 기준값으로 사용합니다.
    internal void RestoreStartOfDayEquity(float equity, bool isPartial)
    {
        StartOfDayEquity = float.IsFinite(equity) && equity > 0f ? equity : currentBalance;
        IsDailyPnlPartial = isPartial;
    }

    public void SetSecondsPerGameMinute(float newValue)
    {
        secondsPerGameMinute = Mathf.Max(0.001f, newValue);
    }

    private void Awake()
    {
        // 모든 컴포넌트의 Start보다 먼저 코스튬 관리자를 준비하여
        // AIVisualController가 첫 프레임부터 장착 변경 이벤트를 구독할 수 있게 합니다.
        EnsureBossManager();
        EnsureCostumeManager();
    }

    public bool IsGameLoaded { get; private set; }

    // 게임 시작 시 한 번 실행
    private void Start()
    {
        var saveManager = FXOverdose.Core.SaveLoadManager.Instance;
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
        // 자산 초기화
        currentBalance = startingBalance;

        // 날짜와 시간 초기화
        currentDay = 1;
        currentHour = 9;
        currentMinute = 0;

        // 게임 상태 초기화 -> 초기에는 차트 개장 준비 상태(Loading)로 대기
        currentState = GameState.Loading;
        currentEnding = EndingType.None;

        StartOfDayEquity = startingBalance;
        IsDailyPnlPartial = false;

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

        if (!bossManager.HasBossToday(currentDay))
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

        bossManager.SpawnBossForDay(currentDay, encounterEquity);
    }

    private System.Collections.Generic.List<string> day1Monologue = new System.Collections.Generic.List<string> {
        "알바에서도 짤리고... 내 수중엔 단돈 4,000달러뿐. 20일 안에 100만 달러를 만들지 못하면 끝장이야!",
        "일단 레버리지는 5배밖에 안 되니까, 조심스럽게 소액 익절을 반복해서 경험치를 쌓고 레벨부터 올려야 해.",
        "5일 뒤엔 밀린 월세도 내야 하니까 방심하지 말자!"
    };

    private System.Collections.Generic.List<string> day6Monologue = new System.Collections.Generic.List<string> {
        "하아... 이런 푼돈 단타로는 20일 안에 절대 100만 달러를 못 만들어! 더 큰 돈을 벌려면 레버리지 배율을 높여야 해.",
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

    public void FinishLoadingAndStartPlaying(bool skipCutscenes = false)
    {
        if (currentState == GameState.Loading)
        {
            if (!skipCutscenes && !IsGameLoaded && FXOverdose.Core.SaveLoadManager.Instance != null && FXOverdose.Core.SaveLoadManager.Instance.CurrentGameMode == FXOverdose.Core.GameMode.Story)
            {
                if (currentDay == 1)
                {
                    var introEvent = storyEvents.Find(e => e.triggerDay == 1);
                    if (introEvent != null && introEvent.comicPanels != null && introEvent.comicPanels.Count > 0 && FXOverdose.UI.ComicCutsceneController.Instance != null)
                    {
                        currentState = GameState.Paused; // 시간 흐름 정지
                        Debug.Log("[GameManager] 1일차 오프닝 컷씬 시작 (시간 정지)");
                        FXOverdose.UI.ComicCutsceneController.Instance.PlayCutscene(introEvent.comicPanels, () => {
                            StartCoroutine(PlayStoryMonologueAndWait(day1Monologue, () => {
                                SyncBossForCurrentDay();
                                currentState = GameState.Playing;
                                Debug.Log("[GameManager] 오프닝 컷씬 및 독백 종료. 차트 엔진 예열 완료 -> 게임 정식 개장 (Playing)");
                            }));
                        });
                        return;
                    }
                }
            }

            SyncBossForCurrentDay();
            var bossManager = FXOverdose.Core.BossManager.Instance;
            if (bossManager != null && bossManager.HasBossToday(currentDay) && bossManager.CurrentBoss != null)
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
                var evt = storyEvents.Find(e => e.triggerDay == currentDay);
                if (currentDay != 16 && evt != null && evt.isPenalty && evt.penaltyAmount > 0)
                {
                    expectedPenalty = evt.penaltyAmount;
                }
            }

            float expectedDeduction = CalculateExpectedDeduction(currentDay, out string _);
            
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

    private void ProcessDailySettlementWithStory()
    {
        isSettlementProcessing = true; // 파산 판정 유예
        currentState = GameState.Settlement;

        TodayEvent = storyEvents.Find(e => e.triggerDay == currentDay);

        // Day 1은 게임 시작 시 이미 재생했으므로 24:00에는 패스
        if (currentDay == 1)
        {
            Debug.Log($"[GameManager] 1일차 24:00 종료. 일일 정산 대기 상태 진입.");
            OnDayEnded?.Invoke();
            return;
        }

        // 페널티 적용 (Day 16 페널티는 아침으로 이동했으므로 예외)
        if (FXOverdose.Core.SaveLoadManager.Instance != null && FXOverdose.Core.SaveLoadManager.Instance.CurrentGameMode == FXOverdose.Core.GameMode.Story)
        {
            if (currentDay != 16 && TodayEvent != null && TodayEvent.isPenalty && TodayEvent.penaltyAmount > 0)
            {
                currentBalance -= TodayEvent.penaltyAmount; 
                if (TraderStatus.CanonicalInstance != null)
                    TraderStatus.CanonicalInstance.AdjustPeakBalanceForExpenditure(TodayEvent.penaltyAmount);
                Debug.Log($"[GameManager] 스토리 이벤트 위약금 강제 차감: -{TodayEvent.penaltyAmount:N0} (잔고: {currentBalance:N0})");
            }
        }

        // 💡 [새 기능] 특정 일차 정기 지출 시스템 (인플레이션형)
        float deduction = CalculateExpectedDeduction(currentDay, out string deductionReason);

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
            if (currentDay != 6 && currentDay != 16 && TodayEvent != null && TodayEvent.comicPanels != null && TodayEvent.comicPanels.Count > 0 && FXOverdose.UI.ComicCutsceneController.Instance != null)
            {
                FXOverdose.UI.ComicCutsceneController.Instance.PlayCutscene(TodayEvent.comicPanels, () => {
                    Debug.Log($"[GameManager] {currentDay}일차 24:00 종료. 일일 정산 대기 상태 진입.");
                    OnDayEnded?.Invoke();
                });
                return;
            }
        }

        Debug.Log($"[GameManager] {currentDay}일차 24:00 종료. 일일 정산 대기 상태 진입.");
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
        if (bossManager != null && bossManager.HasBossToday(currentDay) && bossManager.CurrentBoss != null)
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

        // Day 20 진 엔딩 조건 검사 (보스전 백업)
        if (currentDay == 20)
        {
            if (totalEquity >= 1000000f) EndGame(EndingType.Success);
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
        currentDay++;

        var status = TraderStatus.CanonicalInstance;
        StartOfDayEquity = status != null ? status.GetTotalEquity() : currentBalance;
        IsDailyPnlPartial = false;

        TodayRegularDeduction = 0f;
        TodayRegularDeductionReason = "";

        // 다음 날로 넘어갈 때 체력과 멘탈을 모두 최대로 회복
        if (status != null)
        {
            status.ChangeHealth(9999f);
            status.ChangeMental(9999f, true, "NewDayReset");
            Debug.Log("[GameManager] 일일 정산 후 다음 날 진입: 체력과 멘탈이 최대로 회복되었습니다.");
        }

        Debug.Log($"{currentDay}일차 시작 (09:00)");

        // ⭐ 전날 기억 압축 및 저중요도 Pruning 실행
        FXOverdose.AI.TraderMemoryManager.Instance?.OnDayAdvanced(currentDay);

        // 💡 [차트 리셋] 다음 날로 넘어갈 때 새로운 하루가 시작되도록 차트를 새로 고침(프리웜)합니다.
        var marketEngine = FindAnyObjectByType<FXOverdose.Trading.MarketSimulationEngine>();
        if (marketEngine != null)
        {
            marketEngine.ResetEngine(marketEngine.CurrentPrice);
            Debug.Log("[GameManager] 다음 날로 넘어감에 따라 차트 엔진(과거 기록)을 리셋 및 새로운 차트 프리웜 완료.");
            
            // 💡 리셋된 엔진의 마켓을 다시 개장합니다.
            marketEngine.OpenMarketAfterLoading();
        }

        var bossManager = FXOverdose.Core.BossManager.Instance;
        bool hasBossMorningEvent = bossManager != null && bossManager.HasBossToday(currentDay);
        FXOverdose.Core.BossData pendingBossData = hasBossMorningEvent ? bossManager.GetBossData(currentDay) : null;
        
        if (!hasBossMorningEvent && bossManager != null)
        {
            bossManager.ClearBoss();
        }

        bool hasStoryMorningEvent = false;
        System.Action storyMorningAction = null;

        if (FXOverdose.Core.SaveLoadManager.Instance != null && FXOverdose.Core.SaveLoadManager.Instance.CurrentGameMode == FXOverdose.Core.GameMode.Story)
        {
            if (currentDay == 6 || currentDay == 16)
            {
                var evt = storyEvents.Find(e => e.triggerDay == currentDay);
                if (evt != null)
                {
                    hasStoryMorningEvent = true;
                    storyMorningAction = () => {
                        System.Action applyPenaltyAndMonologue = () => {
                            if (currentDay == 16 && evt.isPenalty && evt.penaltyAmount > 0)
                            {
                                currentBalance -= evt.penaltyAmount;
                                if (TraderStatus.CanonicalInstance != null) TraderStatus.CanonicalInstance.AdjustPeakBalanceForExpenditure(evt.penaltyAmount);
                                Debug.Log($"[GameManager] {currentDay}일차 아침 페널티 차감: -{evt.penaltyAmount}");
                            }
                            
                            var monologue = currentDay == 6 ? day6Monologue : day16Monologue;
                            StartCoroutine(PlayStoryMonologueAndWait(monologue, () => {
                                Debug.Log($"[GameManager] {currentDay}일차 스토리 시작 컷씬 및 독백 종료.");
                                
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
            bossManager.SpawnBossForDay(currentDay, StartOfDayEquity);
            
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
