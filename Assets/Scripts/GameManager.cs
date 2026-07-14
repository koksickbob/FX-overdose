using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    // 1분 경과 시 발행하는 이벤트
    public event Action OnGameMinuteAdvanced;
    //게임 진행 상태
    public enum GameState
    {
        Playing,
        Paused,
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
    [SerializeField] private GameState currentState = GameState.Playing;
    [SerializeField] private EndingType currentEnding = EndingType.None;

    [Header("자산 설정")]
    [SerializeField] private float startingBalance = 10000f; // 시작 자산
    [SerializeField] private float targetBalance = 100000f;  // 목표 자산
    [SerializeField] private float currentBalance;           // 현재 자산

    [Header("시간 설정")]
    [SerializeField] private int currentDay = 1;    // 현재 일차
    [SerializeField] private int currentHour = 9;   // 현재 시간
    [SerializeField] private int currentMinute = 0; // 현재 분

    // 현실에서 몇 초마다 게임 속 1분이 흐를지 설정 (기본 5.0초 대비 5배/기존 2.0초 대비 2배 빠른 속도 -> 1분 = 1.0초)
    [Tooltip("현실에서 몇 초마다 게임 속 1분이 흐르는지 설정합니다. (1.0초 = 5배속)")]
    [SerializeField] private float secondsPerGameMinute = 1.0f;

    // 실제로 흐른 시간을 누적하는 변수
    private float timeAccumulator;

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

    // 게임 시작 시 한 번 실행
    private void Start()
    {
        StartNewGame();
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

        // 게임 상태 초기화
        currentState = GameState.Playing;
        currentEnding = EndingType.None;

        // 시간 누적값 초기화
        timeAccumulator = 0f;

        // 돌발 선택 이벤트 컨트롤러(ChoiceEventController) 자동 부착 및 초기화
        InitializeChoiceEventController();

        Debug.Log("새 게임 시작");
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

        var status = GetComponent<TraderStatus>();
        if (status == null) status = FindAnyObjectByType<TraderStatus>();

        choiceEventCtrl.Initialize(this, marketEngine, tradingCtrl, status);
    }

    // 실제 시간을 게임 시간으로 변환
    private void UpdateGameTime()
    {
        // 프레임 사이에 흐른 실제 시간을 누적
        timeAccumulator += Time.deltaTime;

        // 설정한 시간이 지나면 게임 시간 1분 증가
        while (timeAccumulator >= secondsPerGameMinute)
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

        // 24시가 되면 다음 날로 이동
        if (currentHour >= 24)
        {
            currentHour = 0;
            currentDay++;

            Debug.Log($"{currentDay}일차 시작");
        }

        // 1분 경과 이벤트 발행
        OnGameMinuteAdvanced?.Invoke();
    }

    // 자산을 증가하거나 감소시키는 함수
    public void ChangeBalance(float amount)
    {
        // 게임 진행 중에만 자산 변경 가능
        if (currentState != GameState.Playing)
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
        Debug.Log($"자산 지출: -{amount:N0}, 현재 자산: {currentBalance:N0}");
        CheckEnding();
        return true;
    }

    // 성공 또는 파산 조건 확인
    private void CheckEnding()
    {
        var status = GetComponent<TraderStatus>();
        if (status == null) status = FindAnyObjectByType<TraderStatus>();
        bool isOverdose = status != null && (status.CurrentMentalState == TraderStatus.MentalState.Overdose || status.CurrentMental <= 0f);

        // [Overdose 폭주 상태가 아닐 때만] 현재 자산이 목표 자산 이상이면 성공 엔딩 발동
        if (!isOverdose && currentBalance >= targetBalance)
        {
            EndGame(EndingType.Success);
        }
        // 현재 자산이 0 이하이면 파산 또는 Overdose 엔딩
        else if (currentBalance <= 0f)
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
        }
    }
}
