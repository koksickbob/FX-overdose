using UnityEngine;

public class TraderStatus : MonoBehaviour
{
    // 트레이더의 현재 감정 상태
    public enum MentalState
    {
        Stable,   // 안정
        Anxious,  // 불안
        Danger,   // 위험
        Overdose  // 통제 불능
    }

    [Header("게임 매니저")]
    [SerializeField] private GameManager gameManager;

    [Header("체력 설정")]
    [SerializeField] private float maxHealth = 100f;     // 최대 체력
    [SerializeField] private float currentHealth = 100f; // 현재 체력

    [Header("멘탈 설정")]
    [SerializeField] private float maxMental = 100f;     // 최대 멘탈
    [SerializeField] private float currentMental = 100f; // 현재 멘탈

    [Header("시간에 따른 감소량")]
    [Tooltip("현실 시간 1초마다 감소하는 체력입니다.")]
    [SerializeField] private float healthDecreasePerSecond = 0.5f;

    [Tooltip("체력이 0일 때 현실 시간 1초마다 감소하는 멘탈입니다.")]
    [SerializeField] private float mentalDecreasePerSecond = 1f;

    [Header("현재 상태")]
    [SerializeField] private MentalState currentMentalState;

    // 다른 스크립트에서 현재 상태를 읽을 때 사용
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float CurrentMental => currentMental;
    public float MaxMental => maxMental;
    public MentalState CurrentMentalState => currentMentalState;

    // 체력 비율을 0~1 값으로 반환
    // 나중에 체력 게이지 UI의 fillAmount에 사용
    public float HealthRatio => currentHealth / maxHealth;

    // 멘탈 비율을 0~1 값으로 반환
    // 나중에 멘탈 게이지 UI의 fillAmount에 사용
    public float MentalRatio => currentMental / maxMental;

    private void Start()
    {
        ResetStatus();
    }

    private void Update()
    {
        // GameManager가 연결되지 않았다면 실행 중단
        if (gameManager == null)
        {
            return;
        }

        // 게임이 진행 중일 때만 체력과 멘탈을 감소시킴
        if (gameManager.CurrentState != GameManager.GameState.Playing)
        {
            return;
        }

        DecreaseStatusOverTime();
    }

    // 새 게임 시작 시 체력과 멘탈 초기화
    public void ResetStatus()
    {
        currentHealth = maxHealth;
        currentMental = maxMental;

        UpdateMentalState();
    }

    // 시간에 따라 상태를 감소시키는 함수
    private void DecreaseStatusOverTime()
    {
        // Time.deltaTime을 곱하면 초당 일정한 속도로 감소
        ChangeHealth(-healthDecreasePerSecond * Time.deltaTime);

        // 체력이 모두 떨어지면 멘탈도 계속 감소
        if (currentHealth <= 0f)
        {
            ChangeMental(-mentalDecreasePerSecond * Time.deltaTime);
        }
    }

    // 체력을 증가하거나 감소시키는 함수
    public void ChangeHealth(float amount)
    {
        currentHealth += amount;

        // 체력이 0보다 작거나 최대 체력보다 커지지 않도록 제한
        currentHealth = Mathf.Clamp(
            currentHealth,
            0f,
            maxHealth
        );
    }

    // 멘탈을 증가하거나 감소시키는 함수
    public void ChangeMental(float amount)
    {
        currentMental += amount;

        // 멘탈이 0보다 작거나 최대 멘탈보다 커지지 않도록 제한
        currentMental = Mathf.Clamp(
            currentMental,
            0f,
            maxMental
        );

        // 멘탈이 바뀔 때마다 감정 상태 갱신
        UpdateMentalState();
    }

    // 현재 멘탈 수치에 따라 감정 상태 결정
    private void UpdateMentalState()
    {
        if (currentMental <= 0f)
        {
            currentMentalState = MentalState.Overdose;

            // 멘탈이 0이면 GameManager에 Overdose 엔딩 요청
            if (gameManager != null)
            {
                gameManager.TriggerOverdoseEnding();
            }
        }
        else if (currentMental <= 25f)
        {
            currentMentalState = MentalState.Danger;
        }
        else if (currentMental <= 50f)
        {
            currentMentalState = MentalState.Anxious;
        }
        else
        {
            currentMentalState = MentalState.Stable;
        }
    }
}