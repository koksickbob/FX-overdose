using UnityEngine;

#pragma warning disable CS0649

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

    [Header("시간에 따른 감소량 (게임 8시간 = 100 소모 속도)")]
    [Tooltip("현실 시간 1초마다 감소하는 체력입니다.")]
    [SerializeField] private float healthDecreasePerSecond = 0.04f;

    [Tooltip("체력이 0일 때 현실 시간 1초마다 감소하는 멘탈입니다.")]
    [SerializeField] private float mentalDecreasePerSecond = 0.04f;

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
        // GameManager가 연결되지 않았다면 자동 탐색
        if (gameManager == null)
        {
            gameManager = Object.FindAnyObjectByType<GameManager>();
            if (gameManager == null) return;
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
        // 인게임 1분 속도(SecondsPerGameMinute)에 동기화하여 체력/멘탈 감소 속도 자동 조절
        float speedScale = 5.0f / Mathf.Max(0.001f, gameManager.SecondsPerGameMinute);
        ChangeHealth(-healthDecreasePerSecond * speedScale * Time.deltaTime);

        // 체력이 모두 떨어지면(0 이하) 멘탈이 2배 속도로 급감
        if (currentHealth <= 0f)
        {
            ChangeMental(-mentalDecreasePerSecond * 2.0f * speedScale * Time.deltaTime);
        }
        else if (currentHealth <= maxHealth * 0.5f)
        {
            // 체력이 절반 이하일 때는 기본 멘탈 지속 감소 속도 적용
            ChangeMental(-mentalDecreasePerSecond * speedScale * Time.deltaTime);
        }
    }

    // 체력을 증가하거나 감소시키는 함수
    public void ChangeHealth(float amount)
    {
        float prevHealth = currentHealth;
        currentHealth += amount;

        // 체력이 0보다 작거나 최대 체력보다 커지지 않도록 제한
        currentHealth = Mathf.Clamp(
            currentHealth,
            0f,
            maxHealth
        );

        // [핵심 기능 규격] 체력이 절반 이하(<= 50%)로 떨어진 이후부터는 
        // 체력이 감소할 때마다 멘탈 수치도 동일한 비율로 함께 감소하도록 연동
        if (amount < 0f && prevHealth <= maxHealth * 0.5f)
        {
            // 이미 절반 이하인 상태에서 추가 체력 감소 시 감소량만큼 멘탈도 즉각 같이 감소
            ChangeMental(amount);
        }
        else if (amount < 0f && prevHealth > maxHealth * 0.5f && currentHealth < maxHealth * 0.5f)
        {
            // 이번 체력 감소로 인해 50% 선을 아래로 돌파했다면, 50% 아래로 떨어진 분량만큼 멘탈 감소
            float excessDrop = currentHealth - (maxHealth * 0.5f);
            if (excessDrop < 0f)
            {
                ChangeMental(excessDrop);
            }
        }
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

    [Header("트레이딩 컨트롤러 연동")]
    [SerializeField] private FXOverdose.Trading.TradingController tradingController;

    // 현재 멘탈 수치에 따라 감정 상태 결정
    private void UpdateMentalState()
    {
        if (tradingController == null)
        {
            tradingController = FindAnyObjectByType<FXOverdose.Trading.TradingController>();
        }

        if (currentMental <= 0f)
        {
            currentMentalState = MentalState.Overdose;

            // 통제 불능 시 즉각 고레버리지 뇌동매매/물타기 강행
            if (tradingController != null)
            {
                tradingController.TriggerOverdoseTrade();
            }

            // 멘탈이 0이면 GameManager에 Overdose 엔딩 요청
            if (gameManager != null)
            {
                gameManager.TriggerOverdoseEnding();
            }
        }
        else if (currentMental <= 25f)
        {
            // 위험 상태 진입 시 일정 확률로 뇌동매매 트리거
            if (currentMentalState != MentalState.Danger)
            {
                currentMentalState = MentalState.Danger;
                if (tradingController != null && UnityEngine.Random.value < 0.4f)
                {
                    Debug.LogWarning("[TraderStatus] ⚠️ [Danger 상태 진입] AI 트레이더의 불안감이 극에 달해 뇌동매매를 시도합니다!");
                    tradingController.TriggerOverdoseTrade();
                }
            }
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

    // --- 돌발 선택 이벤트 연동 메서드 ---
    public void ModifyMentalState(float amount)
    {
        ChangeMental(amount);
    }

    public void ModifyHealthState(float amount)
    {
        ChangeHealth(amount);
    }

    public bool ConsumeItem(int itemIndex, int count = 1)
    {
        if (itemIndex < 0 || count <= 0) return true;

        ShopManager shop = FindAnyObjectByType<ShopManager>();
        Inventory inv = null;
        if (shop != null) inv = shop.Inventory;
        if (inv == null) inv = FindAnyObjectByType<Inventory>();

        if (inv != null)
        {
            if (shop != null && shop.CatalogItems != null && itemIndex < shop.CatalogItems.Count)
            {
                ItemData item = shop.CatalogItems[itemIndex];
                if (item != null && inv.GetQuantity(item) >= count)
                {
                    return inv.RemoveItem(item, count);
                }
            }

            if (inv.Slots != null && itemIndex < inv.Slots.Count)
            {
                ItemData slotItem = inv.Slots[itemIndex].Item;
                if (slotItem != null && inv.GetQuantity(slotItem) >= count)
                {
                    return inv.RemoveItem(slotItem, count);
                }
            }
        }

        return false;
    }
}