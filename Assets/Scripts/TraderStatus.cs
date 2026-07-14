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

    [Header("상시 멘탈 기믹 상태 (6대 기믹 확장)")]
    [SerializeField] private float maxMentalLimit = 100f; // 트라우마 발생 시 제한되는 멘탈 천장
    [SerializeField] private int currentLosingStreak = 0; // 연속 손절 카운터
    [SerializeField] private bool isLeverageAddicted = false; // 고배율 중독 상태
    [SerializeField] private int consecutiveHighLevWins = 0; // 50배 이상 연속 익절 카운터
    [SerializeField] private bool hasDrawdownTrauma = false; // 드로다운 트라우마 여부
    [SerializeField] private float peakBalance = 0f; // 역대 최고 자산(High Water Mark)
    [SerializeField] private float currentDrawdownPercent = 0f; // 실시간 드로다운 비율
    [SerializeField] private bool canRegenMental = true; // 자연 회복 허용 여부 (체력 30% 이하 시 false)

    [Header("현재 상태")]
    [SerializeField] private MentalState currentMentalState;

    // 다른 스크립트에서 현재 상태를 읽을 때 사용
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float CurrentMental => currentMental;
    public float MaxMental => maxMental;
    public float MaxMentalLimit => maxMentalLimit;
    public float EffectiveMaxMental => Mathf.Min(maxMental, maxMentalLimit);
    public MentalState CurrentMentalState => currentMentalState;

    public int CurrentLosingStreak
    {
        get => currentLosingStreak;
        set => currentLosingStreak = Mathf.Max(0, value);
    }
    public bool IsLeverageAddicted
    {
        get => isLeverageAddicted;
        set => isLeverageAddicted = value;
    }
    public int ConsecutiveHighLevWins
    {
        get => consecutiveHighLevWins;
        set => consecutiveHighLevWins = Mathf.Max(0, value);
    }
    public bool HasDrawdownTrauma
    {
        get => hasDrawdownTrauma;
        set => hasDrawdownTrauma = value;
    }
    public float PeakBalance
    {
        get => peakBalance;
        set => peakBalance = Mathf.Max(peakBalance, value);
    }
    public float CurrentDrawdownPercent
    {
        get => currentDrawdownPercent;
        set => currentDrawdownPercent = value;
    }
    public bool CanRegenMental
    {
        get => canRegenMental;
        set => canRegenMental = value;
    }

    // 실시간 총 자산 (보유 현금 + 포지션 증거금 + 미실현 손익) 반환
    public float GetTotalEquity()
    {
        float equity = gameManager != null ? gameManager.CurrentBalance : 0f;
        var tradingController = Object.FindAnyObjectByType<FXOverdose.Trading.TradingController>(FindObjectsInactive.Include);
        if (tradingController != null && tradingController.CurrentPosition != FXOverdose.Trading.TradingController.PositionType.None)
        {
            equity += tradingController.MarginAmount + tradingController.CalculateUnrealizedPnL();
        }
        return equity;
    }

    // 체력 비율을 0~1 값으로 반환
    // 나중에 체력 게이지 UI의 fillAmount에 사용
    public float HealthRatio => currentHealth / maxHealth;

    // 멘탈 비율을 0~1 값으로 반환
    // 나중에 멘탈 게이지 UI의 fillAmount에 사용
    public float MentalRatio => currentMental / maxMental;

    public static TraderStatus CanonicalInstance
    {
        get
        {
            GameManager gm = Object.FindAnyObjectByType<GameManager>();
            if (gm != null && gm.GetComponent<TraderStatus>() != null)
            {
                return gm.GetComponent<TraderStatus>();
            }
            return Object.FindAnyObjectByType<TraderStatus>();
        }
    }

    private void Start()
    {
        if (this == CanonicalInstance)
        {
            ResetStatus();
        }
        else
        {
            SyncFromCanonical();
        }
    }

    private void SyncFromCanonical()
    {
        TraderStatus canonical = CanonicalInstance;
        if (canonical != null && canonical != this)
        {
            this.currentHealth = canonical.currentHealth;
            this.maxHealth = canonical.maxHealth;
            this.currentMental = canonical.currentMental;
            this.maxMental = canonical.maxMental;
            this.maxMentalLimit = canonical.maxMentalLimit;
            this.currentLosingStreak = canonical.currentLosingStreak;
            this.isLeverageAddicted = canonical.isLeverageAddicted;
            this.consecutiveHighLevWins = canonical.consecutiveHighLevWins;
            this.hasDrawdownTrauma = canonical.hasDrawdownTrauma;
            this.peakBalance = canonical.peakBalance;
            this.currentDrawdownPercent = canonical.currentDrawdownPercent;
            this.canRegenMental = canonical.canRegenMental;
            this.currentMentalState = canonical.currentMentalState;
        }
    }

    public static void SyncAllInstances()
    {
        TraderStatus canonical = CanonicalInstance;
        if (canonical == null) return;

#pragma warning disable 0618
        TraderStatus[] all = Object.FindObjectsByType<TraderStatus>(FindObjectsInactive.Include);
#pragma warning restore 0618
        foreach (var st in all)
        {
            if (st != null && st != canonical)
            {
                st.currentHealth = canonical.currentHealth;
                st.maxHealth = canonical.maxHealth;
                st.currentMental = canonical.currentMental;
                st.maxMental = canonical.maxMental;
                st.maxMentalLimit = canonical.maxMentalLimit;
                st.currentLosingStreak = canonical.currentLosingStreak;
                st.isLeverageAddicted = canonical.isLeverageAddicted;
                st.consecutiveHighLevWins = canonical.consecutiveHighLevWins;
                st.hasDrawdownTrauma = canonical.hasDrawdownTrauma;
                st.peakBalance = canonical.peakBalance;
                st.currentDrawdownPercent = canonical.currentDrawdownPercent;
                st.canRegenMental = canonical.canRegenMental;
                st.currentMentalState = canonical.currentMentalState;
            }
        }
    }

    private void Update()
    {
        // 씬 내 중복된 TraderStatus가 존재할 경우 메인(CanonicalInstance)만 연산하고, 서브 인스턴스는 매 프레임 동기화합니다.
        if (this != CanonicalInstance)
        {
            SyncFromCanonical();
            return;
        }

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
        if (this != CanonicalInstance && CanonicalInstance != null)
        {
            CanonicalInstance.ResetStatus();
            return;
        }

        currentHealth = maxHealth;
        currentMental = maxMental;
        maxMentalLimit = maxMental;
        currentLosingStreak = 0;
        isLeverageAddicted = false;
        consecutiveHighLevWins = 0;
        hasDrawdownTrauma = false;
        if (gameManager == null) gameManager = Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
        peakBalance = gameManager != null ? gameManager.StartingBalance : 10000f;
        currentDrawdownPercent = 0f;
        canRegenMental = true;

        UpdateMentalState();
        SyncAllInstances();
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

    private FXOverdose.AI.LLM.LocalLLMService llmService;
    private MentalState lastTrackedMentalState = MentalState.Stable;

    private void TriggerLLMDialogue(FXOverdose.AI.LLM.EventCategory cat, string ctx)
    {
        if (llmService == null) llmService = FXOverdose.AI.LLM.LocalLLMService.Instance;
        llmService?.RequestDialogue(cat, ctx);
    }

    // 체력을 증가하거나 감소시키는 함수
    public void ChangeHealth(float amount)
    {
        if (this != CanonicalInstance && CanonicalInstance != null)
        {
            CanonicalInstance.ChangeHealth(amount);
            return;
        }

        float prevHealth = currentHealth;
        currentHealth += amount;

        // 체력이 0보다 작거나 최대 체력보다 커지지 않도록 제한
        currentHealth = Mathf.Clamp(
            currentHealth,
            0f,
            maxHealth
        );

        // 체력 임계치 돌파 시 유동적 대사 호출
        if (amount < 0f && maxHealth > 0f)
        {
            float prevRatio = prevHealth / maxHealth;
            float currRatio = currentHealth / maxHealth;

            if (prevRatio > 0.5f && currRatio <= 0.5f)
            {
                TriggerLLMDialogue(FXOverdose.AI.LLM.EventCategory.HealthChange, "체력 50% 이하 과로 상태 진입, 눈 앞이 흐려지고 예민해짐");
            }
            else if (prevRatio > 0.2f && currRatio <= 0.2f)
            {
                TriggerLLMDialogue(FXOverdose.AI.LLM.EventCategory.HealthChange, "체력 20% 이하 탈진 임박 상태, 머리가 깨질 듯한 극심한 수면 부족 및 두통 호소");
            }
        }

        // [핵심 기능 규격] 체력이 절반 이하(<= 50%)로 떨어진 이후부터는 
        // 체력이 감소할 때마다 멘탈 수치도 동일한 비율로 함께 감소하도록 연동
        if (amount < 0f && prevHealth <= maxHealth * 0.5f)
        {
            ChangeMental(amount);
        }
        else if (amount < 0f && prevHealth > maxHealth * 0.5f && currentHealth < maxHealth * 0.5f)
        {
            float excessDrop = currentHealth - (maxHealth * 0.5f);
            if (excessDrop < 0f)
            {
                ChangeMental(excessDrop);
            }
        }

        SyncAllInstances();
    }

    // 멘탈을 증가하거나 감소시키는 함수
    public void ChangeMental(float amount, bool ignoreRegenBlock = false)
    {
        if (this != CanonicalInstance && CanonicalInstance != null)
        {
            CanonicalInstance.ChangeMental(amount, ignoreRegenBlock);
            return;
        }

        // 자연 회복 차단(canRegenMental == false) 상태에서 양수(회복) 시도 시, ignoreRegenBlock이 false이면 차단
        if (amount > 0f && !canRegenMental && !ignoreRegenBlock)
        {
            return;
        }

        currentMental += amount;

        // 멘탈이 0보다 작거나 최대 멘탈(및 트라우마 제한 maxMentalLimit)보다 커지지 않도록 제한
        float effectiveMax = Mathf.Min(maxMental, maxMentalLimit);
        currentMental = Mathf.Clamp(
            currentMental,
            0f,
            effectiveMax
        );

        // 멘탈이 바뀔 때마다 감정 상태 갱신
        UpdateMentalState();
        SyncAllInstances();
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
            if (currentMentalState != MentalState.Overdose)
            {
                currentMentalState = MentalState.Overdose;
                TriggerLLMDialogue(FXOverdose.AI.LLM.EventCategory.MentalChange, "멘탈 0 도달, 통제 불능 및 Overdose 폭주 상태");
            }

            // 통제 불능 시 즉각 고레버리지 뇌동매매/물타기 강행
            if (tradingController != null)
            {
                tradingController.TriggerOverdoseTrade();
            }

            // [기획서 7장 엔딩 조건 부합] 멘탈이 0일 때 자금/증거금까지 소진(0 이하)된 경우에만 Overdose 배드엔딩 발동
            if (gameManager != null && GetTotalEquity() <= 0f)
            {
                gameManager.TriggerOverdoseEnding();
            }
        }
        else if (currentMental <= 25f)
        {
            if (currentMentalState != MentalState.Danger)
            {
                currentMentalState = MentalState.Danger;
                TriggerLLMDialogue(FXOverdose.AI.LLM.EventCategory.MentalChange, "멘탈 25점 이하 경고 (Danger 상태 진입, 극심한 공포와 자책)");

                if (tradingController != null && UnityEngine.Random.value < 0.4f)
                {
                    Debug.LogWarning("[TraderStatus] ⚠️ [Danger 상태 진입] AI 트레이더의 불안감이 극에 달해 뇌동매매를 시도합니다!");
                    tradingController.TriggerOverdoseTrade();
                }
            }
        }
        else if (currentMental <= 50f)
        {
            if (currentMentalState != MentalState.Anxious)
            {
                currentMentalState = MentalState.Anxious;
                if (lastTrackedMentalState == MentalState.Stable)
                {
                    TriggerLLMDialogue(FXOverdose.AI.LLM.EventCategory.MentalChange, "멘탈 50점 이하 (Anxious 불안 상태 진입)");
                }
            }
        }
        else
        {
            if (currentMentalState != MentalState.Stable)
            {
                currentMentalState = MentalState.Stable;
                if (lastTrackedMentalState != MentalState.Stable)
                {
                    TriggerLLMDialogue(FXOverdose.AI.LLM.EventCategory.MentalChange, "멘탈 50점 이상 회복 (Stable 평온 상태 회복)");
                }
            }
        }

        lastTrackedMentalState = currentMentalState;
    }

    // --- 돌발 선택 이벤트 연동 메서드 ---
    public void ModifyMentalState(float amount)
    {
        // 이벤트 및 아이템 등에 의한 회복은 자연 회복 차단(canRegenMental == false) 중이라도 적용
        ChangeMental(amount, true);
    }

    // --- 상시 멘탈 소모 6대 기믹 제어 메서드 ---
    public void SetMaxMentalCeiling(float ceiling)
    {
        if (this != CanonicalInstance && CanonicalInstance != null)
        {
            CanonicalInstance.SetMaxMentalCeiling(ceiling);
            return;
        }

        this.maxMentalLimit = Mathf.Clamp(ceiling, 0f, maxMental);
        if (this.currentMental > this.maxMentalLimit)
        {
            this.currentMental = this.maxMentalLimit;
            UpdateMentalState();
        }
        SyncAllInstances();
    }

    public void CureLeverageAddiction()
    {
        if (this != CanonicalInstance && CanonicalInstance != null)
        {
            CanonicalInstance.CureLeverageAddiction();
            return;
        }

        this.isLeverageAddicted = false;
        this.consecutiveHighLevWins = 0;
        Debug.Log("[TraderStatus] 💊 고배율 레버리지 중독 상태가 치료/초기화되었습니다.");
        SyncAllInstances();
    }

    public void TriggerImpulsiveTrade(int leverage)
    {
        if (tradingController == null)
        {
            tradingController = FindAnyObjectByType<FXOverdose.Trading.TradingController>();
        }

        if (tradingController != null)
        {
            FXOverdose.Trading.TradingController.PositionType randomPos = 
                (UnityEngine.Random.value > 0.5f) 
                ? FXOverdose.Trading.TradingController.PositionType.Long 
                : FXOverdose.Trading.TradingController.PositionType.Short;

            tradingController.ExecuteEmergencyTrade(randomPos, leverage);

            var visual = FindAnyObjectByType<FXOverdose.AI.AIVisualController>();
            visual?.DisplayDialogueBalloon("더는 못 참아!! 100배로 싹 다 복구한다!!");
            Debug.LogWarning($"[TraderStatus] ⚠️ TriggerImpulsiveTrade 발동: {randomPos} {leverage}배 강제 진입");
        }
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