using System;
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
    [SerializeField] private float startingBalance = 4000f; // 시작 자산 (초기 4,000)
    [SerializeField] private float targetBalance = 100000f;  // 목표 자산 (엔딩 철폐되어 단순 표기용)
    [SerializeField] private float currentBalance;           // 현재 자산

    [Header("시간 설정")]
    [SerializeField] private int currentDay = 1;    // 현재 일차
    [SerializeField] private int currentHour = 9;   // 현재 시간
    [SerializeField] private int currentMinute = 0; // 현재 분

    // 당일 오전 9시작 시점의 총 자산 (일일 정산용)
    public float StartOfDayEquity { get; private set; }
    // 구버전 세이브처럼 09:00 기준 자산이 없는 경우, 로드 시점부터 계산 중인지 표시합니다.
    public bool IsDailyPnlPartial { get; private set; }

    // 현실에서 몇 초마다 게임 속 1분이 흐를지 설정 (기본 5.0초 대비 5배/기존 2.0초 대비 2배 빠른 속도 -> 1분 = 1.0초)
    [Tooltip("현실에서 몇 초마다 게임 속 1분이 흐르는지 설정합니다. (1.0초 = 5배속)")]
    [SerializeField] private float secondsPerGameMinute = 1.0f;

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

    // 게임 시작 시 한 번 실행
    private void Start()
    {
        var saveManager = FXOverdose.Core.SaveLoadManager.Instance;
        if (saveManager != null && saveManager.IsPendingLoad)
        {
            // 불러오기 모드 진입
            currentState = GameState.Loading;
            currentEnding = EndingType.None;
            timeAccumulator = 0f;

            InitializeChoiceEventController();
            InitializeTraderLevelSystem();
            EnsureActiveItemEffectManager();
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

        // 게임 상태 초기화 -> 초기에는 LLM 로딩 및 개장 준비 상태(Loading)로 대기
        currentState = GameState.Loading;
        currentEnding = EndingType.None;

        StartOfDayEquity = startingBalance;
        IsDailyPnlPartial = false;

        // 시간 누적값 초기화
        timeAccumulator = 0f;

        // 돌발 선택 이벤트 컨트롤러(ChoiceEventController) 자동 부착 및 초기화
        InitializeChoiceEventController();
        InitializeTraderLevelSystem();

        // 동적 시간 완급 조절기(DynamicTimeRegulator) 부착
        EnsureDynamicTimeRegulator();

        // 액티브 아이템 효과 및 업그레이드 초기화
        EnsureActiveItemEffectManager();
        ActiveItemEffectManager.Instance?.ResetAll();

        // AI 장기/단기 기억 시스템 초기화
        FXOverdose.AI.TraderMemoryManager.Instance?.ResetAll();

        // 주인공 및 스킬 레벨 시스템 초기화
        FXOverdose.Trading.TraderLevelSystem.Instance?.ResetLevels();

        Debug.Log("새 게임 시작 (LLM 예열 및 차트 개장 로딩 단계 진입 - 초기 자본: $2,500)");
    }

    private void EnsureActiveItemEffectManager()
    {
        if (ActiveItemEffectManager.Instance != null) return;
        ActiveItemEffectManager manager = GetComponent<ActiveItemEffectManager>();
        if (manager == null) gameObject.AddComponent<ActiveItemEffectManager>();
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

    public void FinishLoadingAndStartPlaying()
    {
        if (currentState == GameState.Loading)
        {
            currentState = GameState.Playing;
            Debug.Log("[GameManager] LLM 및 차트 엔진 예열 완료 -> 게임 정식 개장 (Playing)");
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

            currentState = GameState.Settlement;
            Debug.Log($"[GameManager] {currentDay}일차 24:00 종료. 일일 정산 대기 상태 진입.");
            OnDayEnded?.Invoke();
        }
    }

    // 일일 정산 화면에서 '다음날 진행하기' 호출 시 실행
    public void ProceedToNextDay()
    {
        if (currentState != GameState.Settlement) return;

        currentHour = 9;
        currentMinute = 0;
        currentDay++;

        var status = TraderStatus.CanonicalInstance;
        StartOfDayEquity = status != null ? status.GetTotalEquity() : currentBalance;
        IsDailyPnlPartial = false;

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

        currentState = GameState.Playing;

        if (remainingFastForwardMinutes > 0)
        {
            int resumeMinutes = remainingFastForwardMinutes;
            remainingFastForwardMinutes = 0;
            AdvanceGameMinutes(resumeMinutes);
        }
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
        
        // 게임 오버 이벤트 발생 (UI 연동)
        OnGameOverEvent?.Invoke(ending);
        
        if (FXOverdose.AI.LLM.LocalLLMService.Instance != null)
        {
            FXOverdose.AI.LLM.LocalLLMService.Instance.TriggerGameOverSpiralLoop(ending.ToString());
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

