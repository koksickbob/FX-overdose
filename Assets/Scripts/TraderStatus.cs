using UnityEngine;

#pragma warning disable CS0649

public class TraderStatus : MonoBehaviour
{
    private bool p2pExternalMode;
    public void EnableP2PExternalMode()=>p2pExternalMode=true;
    public void ApplyP2PVitals(float health,float mental)
    {
        float hd=health-currentHealth,md=mental-currentMental;currentHealth=Mathf.Clamp(health,0,MaxHealth);currentMental=Mathf.Clamp(mental,0,MaxMental);
        // 네트워크 복제는 값 표시만 갱신합니다. 매 패킷을 멘탈 감소 사유 팝업으로 출력하지 않습니다.
        if(!Mathf.Approximately(hd,0))OnHealthChanged?.Invoke(hd);if(!Mathf.Approximately(md,0))OnMentalValueChanged?.Invoke(md);UpdateMentalState();SyncAllInstances();
    }
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
    [SerializeField] private float healthDecreasePerSecond = 0.05f;

    [Header("상시 멘탈 기믹 상태")]
    [SerializeField] private int currentLosingStreak = 0; // 연속 손절 카운터
    [SerializeField] private bool isLeverageAddicted = false; // 고배율 중독 상태
    [SerializeField] private int consecutiveHighLevWins = 0; // 50배 이상 연속 익절 카운터
    [SerializeField] private int consecutiveLowLevTrades = 0; // 중독 상태에서 50배 이하 매매 카운터
    [SerializeField] private float peakBalance = 0f; // 역대 최고 자산(High Water Mark)

    [Header("지속 멘탈 감소 누적기")]
    [SerializeField] private float healthDropMentalDrainAccumulator = 0f;
    [SerializeField] private float healthDropMentalDrainTimer = 0f;

    [Header("현재 상태")]
    [SerializeField] private MentalState currentMentalState;
    private MentalState lastTrackedMentalState = MentalState.Stable;

    // 체력 저하로 인한 자연 감소의 사유 문자열입니다.
    // 지뢰계 의상의 감소 증폭에서 제외되는 유일한 경로라, 리터럴 대신 이 상수로 비교합니다.
    // ponytail: 문자열 reason 비교. 증폭 제외 대상이 2개 이상 되면 MentalChangeSource enum으로 승격.
    public const string NaturalDrainReason = "체력 저하";

    // 멘탈 감소/증가 시 원인과 함께 알리는 이벤트
    public event System.Action<float, string> OnMentalChangedWithReason;
    public event System.Action<float> OnHealthChanged;
    public event System.Action<float> OnMentalValueChanged;
    public event System.Action<MentalState> OnMentalStateChanged;

    // 다른 스크립트에서 현재 상태를 읽을 때 사용
    public float CurrentHealth => currentHealth;
    public float MaxHealth 
    {
        get
        {
            float bonus = 0f;
            if (CostumeManager.Instance != null && CostumeManager.Instance.EquippedCostumeId == CostumeManager.StreetCapId)
            {
                bonus = 30f;
            }
            return maxHealth + bonus;
        }
    }
    public float CurrentMental => currentMental;
    public float MaxMental 
    {
        get
        {
            float bonus = 0f;
            if (CostumeManager.Instance != null && CostumeManager.Instance.EquippedCostumeId == CostumeManager.PajamaId)
            {
                bonus = 15f;
            }
            return maxMental + bonus;
        }
    }
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
    public int ConsecutiveLowLevTrades
    {
        get => consecutiveLowLevTrades;
        set => consecutiveLowLevTrades = Mathf.Max(0, value);
    }
    public float PeakBalance
    {
        get => peakBalance;
        set => peakBalance = value;
    }

    // 지출 전후의 드로다운 비율(%)이 정확히 유지되게 역대 최고 자산(PeakBalance)을 비례 하향 조정합니다.
    public void AdjustPeakBalanceForExpenditure(float expenditureAmount)
    {
        if (expenditureAmount <= 0f || peakBalance <= 0f) return;

        float currentEquity = GetTotalEquity();
        float preExpenditureEquity = currentEquity + expenditureAmount;
        if (preExpenditureEquity > 0f)
        {
            float ratio = Mathf.Clamp01(currentEquity / preExpenditureEquity);
            peakBalance *= ratio;
            Debug.Log($"[TraderStatus] 🛍️ 아이템/스킬 지출(-{expenditureAmount:N0})로 역대 최고 자산(PeakBalance)이 비례 보정되었습니다: {peakBalance:N0} (드로다운 % 동일 유지)");
        }
        else
        {
            peakBalance = Mathf.Max(0f, peakBalance - expenditureAmount);
        }
    }

    // 실시간 총 자산 (보유 현금 + 포지션 증거금 + 미실현 손익) 반환
    public float GetTotalEquity()
    {
        float equity = gameManager != null ? gameManager.CurrentBalance : 0f;
        if (this.tradingController == null)
        {
            this.tradingController = Object.FindAnyObjectByType<FXOverdose.Trading.TradingController>(FindObjectsInactive.Include);
        }
        if (this.tradingController != null && this.tradingController.CurrentPosition != FXOverdose.Trading.TradingController.PositionType.None)
        {
            equity += this.tradingController.MarginAmount + this.tradingController.CalculateUnrealizedPnL();
        }
        return equity;
    }

    // 체력 비율을 0~1 값으로 반환
    // 나중에 체력 게이지 UI의 fillAmount에 사용
    public float HealthRatio => currentHealth / MaxHealth;

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

    private bool wasLoaded = false;

    /// <summary>
    /// 세이브에 상태를 담습니다. 중독·연패 카운터가 빠져 있어
    /// 불러오기 한 번으로 페널티가 해제되던 문제를 막습니다. (SV-A1~A3)
    /// </summary>
    public void CaptureSaveData(FXOverdose.Core.SaveData data)
    {
        if (data == null) return;

        data.PeakBalance = peakBalance;
        data.CurrentMental = currentMental;
        data.CurrentMentalState = currentMentalState;
        data.CurrentHealth = currentHealth;
        data.MaxMental = maxMental;

        data.IsLeverageAddicted = isLeverageAddicted;
        data.ConsecutiveHighLevWins = consecutiveHighLevWins;
        data.ConsecutiveLowLevTrades = consecutiveLowLevTrades;
        data.CurrentLosingStreak = currentLosingStreak;
    }

    /// <summary>세이브에서 상태를 되돌립니다.</summary>
    public void RestoreFromSaveData(FXOverdose.Core.SaveData data)
    {
        if (data == null) return;

        wasLoaded = true;

        peakBalance = data.PeakBalance;
        currentMental = data.CurrentMental;
        currentMentalState = data.CurrentMentalState;
        currentHealth = data.CurrentHealth;
        if (data.MaxMental > 0f)
        {
            maxMental = data.MaxMental;
        }

        isLeverageAddicted = data.IsLeverageAddicted;
        consecutiveHighLevWins = data.ConsecutiveHighLevWins;
        consecutiveLowLevTrades = data.ConsecutiveLowLevTrades;
        currentLosingStreak = data.CurrentLosingStreak;
    }

    private void Start()
    {
        if (this == CanonicalInstance)
        {
            if (wasLoaded)
            {
                Debug.Log("[TraderStatus] 로드 중이므로 HP/멘탈 초기화를 건너롼뜁니다.");
            }
            else
            {
                ResetStatus();
            }
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
            this.currentLosingStreak = canonical.currentLosingStreak;
            this.isLeverageAddicted = canonical.isLeverageAddicted;
            this.consecutiveHighLevWins = canonical.consecutiveHighLevWins;
            this.consecutiveLowLevTrades = canonical.consecutiveLowLevTrades;
            this.peakBalance = canonical.peakBalance;
            this.currentMentalState = canonical.currentMentalState;

            this.healthDropMentalDrainAccumulator = canonical.healthDropMentalDrainAccumulator;
            this.healthDropMentalDrainTimer = canonical.healthDropMentalDrainTimer;
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
                st.currentLosingStreak = canonical.currentLosingStreak;
                st.isLeverageAddicted = canonical.isLeverageAddicted;
                st.consecutiveHighLevWins = canonical.consecutiveHighLevWins;
                st.consecutiveLowLevTrades = canonical.consecutiveLowLevTrades;
                st.peakBalance = canonical.peakBalance;
                st.currentMentalState = canonical.currentMentalState;
            }
        }
    }

    private void Update()
    {
        if(p2pExternalMode)return;
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

        healthDropMentalDrainTimer += Time.deltaTime;
        if (healthDropMentalDrainTimer >= 2.0f)
        {
            if (healthDropMentalDrainAccumulator <= -0.01f)
            {
                ChangeMental(healthDropMentalDrainAccumulator, NaturalDrainReason);
                healthDropMentalDrainAccumulator = 0f;
            }
            healthDropMentalDrainTimer = 0f;
        }
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
        currentLosingStreak = 0;
        isLeverageAddicted = false;
        consecutiveHighLevWins = 0;
        consecutiveLowLevTrades = 0;
        if (gameManager == null) gameManager = Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
        peakBalance = 0f;

        healthDropMentalDrainAccumulator = 0f;
        healthDropMentalDrainTimer = 0f;

        UpdateMentalState();
        SyncAllInstances();
    }

    // 시간에 따라 상태를 감소시키는 함수
    private void DecreaseStatusOverTime()
    {
        // 인게임 1분 속도(SecondsPerGameMinute)에 동기화하여 체력/멘탈 감소 속도 자동 조절
        float speedScale = 5.0f / Mathf.Max(0.001f, gameManager.SecondsPerGameMinute);

        float healthGuard = ActiveItemEffectManager.Instance != null ? ActiveItemEffectManager.Instance.HealthDrainReduction : 0f;
        float mentalGuard = ActiveItemEffectManager.Instance != null ? ActiveItemEffectManager.Instance.MentalDrainReduction : 0f;

        float currentDay = gameManager != null ? gameManager.CurrentDay : 1f;
        float currentHealthDecrease = Mathf.Min(0.15f, healthDecreasePerSecond + (currentDay * 0.005f));
        float healthDrainThisFrame = currentHealthDecrease * 1.5f * (1f - healthGuard) * speedScale * Time.deltaTime;

        ChangeHealth(-healthDrainThisFrame);

        // 체력 1~50% 구간의 멘탈 감소는 ChangeHealth의 체력 연동이 전담합니다.
        // 여기서 같은 감소를 한 번 더 누적하면 하나의 사건(시간 경과에 따른 체력 감소)이
        // 이중으로 계상되므로, 체력이 0이라 ChangeHealth가 아무 변화도 만들지 못하는
        // 경우에만 직접 누적합니다. 연동 대비 2배 속도로 급감시킵니다.
        if (currentHealth <= 0f)
        {
            healthDropMentalDrainAccumulator += -healthDrainThisFrame * 2.0f * (1f - mentalGuard);
        }
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
            MaxHealth
        );
        float appliedHealthDelta = currentHealth - prevHealth;
        if (!Mathf.Approximately(appliedHealthDelta, 0f))
        {
            OnHealthChanged?.Invoke(appliedHealthDelta);
        }

        // 체력 임계치 돌파 시 유동적 대사 호출
        if (amount < 0f && MaxHealth > 0f)
        {
            float prevRatio = prevHealth / MaxHealth;
            float currRatio = currentHealth / MaxHealth;

            if (prevRatio > 0.5f && currRatio <= 0.5f)
            {
            }
            else if (prevRatio > 0.2f && currRatio <= 0.2f)
            {
            }
        }

        // [핵심 기능 규격] 체력이 절반 이하(<= 50%)로 떨어진 이후부터는
        // 체력이 감소할 때마다 멘탈 수치도 동일한 비율로 함께 감소하도록 연동.
        // 요청량(amount)이 아니라 클램프 후 실제 변화량(appliedHealthDelta)을 기준으로 삼습니다.
        // 요청량을 쓰면 체력이 0으로 클램프된 뒤에도 "계속 떨어지는 것처럼" 멘탈이 청구됩니다.
        if (appliedHealthDelta < 0f)
        {
            float mentalGuard = ActiveItemEffectManager.Instance != null ? ActiveItemEffectManager.Instance.MentalDrainReduction : 0f;
            float linkedDrop = 0f;

            if (prevHealth <= MaxHealth * 0.5f)
            {
                linkedDrop = appliedHealthDelta;
            }
            else if (currentHealth < MaxHealth * 0.5f)
            {
                // 50% 경계를 넘어 내려간 경우, 경계 아래로 내려간 초과분만 연동합니다.
                linkedDrop = Mathf.Min(0f, currentHealth - (MaxHealth * 0.5f));
            }

            if (linkedDrop < 0f)
            {
                healthDropMentalDrainAccumulator += linkedDrop * (1f - mentalGuard);
            }
        }

        SyncAllInstances();
    }

    // 멘탈을 증가하거나 감소시키는 함수
    public void ChangeMental(float amount, string reason = "")
    {
        if (this != CanonicalInstance && CanonicalInstance != null)
        {
            CanonicalInstance.ChangeMental(amount, reason);
            return;
        }

        // 지뢰계 의상(Costume) 디버프 적용: 체력 저하로 인한 자연 감소를 제외한 모든 멘탈 감소 1.25배 가속
        if (amount < 0f && reason != NaturalDrainReason && CostumeManager.Instance != null && CostumeManager.Instance.EquippedCostumeId == CostumeManager.JiraiKeiId)
        {
            amount *= 1.25f;
        }

        float prevMental = currentMental;
        currentMental += amount;

        // 멘탈이 0보다 작거나 최대 멘탈보다 커지지 않도록 제한
        currentMental = Mathf.Clamp(
            currentMental,
            0f,
            MaxMental
        );
        float appliedMentalDelta = currentMental - prevMental;
        if (!Mathf.Approximately(appliedMentalDelta, 0f))
        {
            OnMentalValueChanged?.Invoke(appliedMentalDelta);
        }

        if (!string.IsNullOrEmpty(reason))
        {
            OnMentalChangedWithReason?.Invoke(amount, reason);
        }

        // 멘탈이 바뀔 때마다 감정 상태 갱신
        UpdateMentalState();
        SyncAllInstances();
    }

    [Header("트레이딩 컨트롤러 연동")]
    [SerializeField] private FXOverdose.Trading.TradingController tradingController;

    // 현재 멘탈 수치에 따라 감정 상태 결정
    private void UpdateMentalState()
    {
        MentalState previousState = currentMentalState;

        // P2P의 탈락과 매매 권위는 호스트가 판정합니다. 여기서 싱글용 오버도즈
        // 강제매매/슬로모션/엔딩을 실행하면 클라이언트 상태가 서버와 달라집니다.
        if (p2pExternalMode)
        {
            // 멘탈 0은 서버가 탈락으로 처리하므로 싱글 오버도즈 사운드/BGM 상태에는 진입하지 않습니다.
            currentMentalState = currentMental <= 25f ? MentalState.Danger
                : currentMental <= 50f ? MentalState.Anxious
                : MentalState.Stable;
            if (previousState != currentMentalState) OnMentalStateChanged?.Invoke(currentMentalState);
            lastTrackedMentalState = currentMentalState;
            return;
        }

        if (tradingController == null)
        {
            tradingController = FindAnyObjectByType<FXOverdose.Trading.TradingController>();
        }

        if (currentMental <= 0f)
        {
            if (currentMentalState != MentalState.Overdose)
            {
                currentMentalState = MentalState.Overdose;

                // [슬로우 모션 기믹] 오버도즈 폭주 발동 순간 슬로우 모션 (5초간 2.5배 감속)
                if (FXOverdose.Core.DynamicTimeRegulator.Instance != null)
                {
                    FXOverdose.Core.DynamicTimeRegulator.Instance.TriggerDramaticSlowMotion(2.5f, 5.0f);
                    Debug.Log("[TraderStatus] 💥 오버도즈 폭주 발동! 극적 연출을 위해 5초간 슬로우 모션(2.5배 감속) 가동.");
                }

                // 통제 불능 최초 진입 시에만 즉각 고레버리지 뇌동매매/물타기 강행 (매 프레임 호출 및 랙 유발 방지)
                if (tradingController != null)
                {
                    tradingController.TriggerOverdoseTrade();
                }
            }

            // [기획서 7장 엔딩 조건 부합] 멘탈이 0일 때 자금/증거금까지 소진(0 이하)된 경우에만 Overdose 배드엔딩 발동
            if (gameManager != null && GetTotalEquity() <= 0f)
            {
                // ⭐ [Overdose 보호] 35초의 연출/쉴드 시간 동안에는 총자산이 0 이하가 되어도 게임오버를 유예합니다.
                bool isProtected = tradingController != null && tradingController.IsOverdoseProtected();
                if (!isProtected)
                {
                    gameManager.TriggerOverdoseEnding();
                }
            }
        }
        else if (currentMental <= 25f)
        {
            if (currentMentalState != MentalState.Danger)
            {
                currentMentalState = MentalState.Danger;

                if (tradingController != null && UnityEngine.Random.value < 0.4f)
                {
                    Debug.LogWarning("[TraderStatus] ⚠️ [Danger 상태 진입] AI 트레이더의 불안감이 극에 달해 뇌동매매를 시도합니다!");
                    TriggerImpulsiveTrade(100);
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
                }
            }
        }

        if (lastTrackedMentalState == MentalState.Overdose && currentMentalState != MentalState.Overdose)
        {
            if (gameManager != null)
            {
                gameManager.ResumePreservedFastForward();
            }
        }

        if (previousState != currentMentalState)
        {
            OnMentalStateChanged?.Invoke(currentMentalState);
        }
        lastTrackedMentalState = currentMentalState;
    }

    // --- 돌발 선택 이벤트 연동 메서드 ---
    public void ModifyMentalState(float amount)
    {
        ChangeMental(amount);
    }

    public void IncreaseMaxMental(float amount)
    {
        if (this != CanonicalInstance && CanonicalInstance != null)
        {
            CanonicalInstance.IncreaseMaxMental(amount);
            return;
        }

        if (amount <= 0f) return;
        maxMental += amount;
        currentMental = Mathf.Min(currentMental + amount, MaxMental);
        UpdateMentalState();
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
        this.consecutiveLowLevTrades = 0;
        Debug.Log("[TraderStatus] 💊 고배율 레버리지 중독 상태가 치료/초기화되었습니다.");
        SyncAllInstances();
    }

    public void TriggerImpulsiveTrade(int leverage)
    {
        if (tradingController == null)
        {
            tradingController = FindAnyObjectByType<FXOverdose.Trading.TradingController>();
        }

        if (currentMentalState == MentalState.Overdose || (tradingController != null && tradingController.IsOverdoseTradeActive))
        {
            Debug.Log("[TraderStatus] 오버도즈 중이므로 뇌동매매(TriggerImpulsiveTrade) 발동을 무시합니다.");
            return;
        }

        if (tradingController != null)
        {
            FXOverdose.Trading.TradingController.PositionType randomPos = 
                (UnityEngine.Random.value > 0.5f) 
                ? FXOverdose.Trading.TradingController.PositionType.Long 
                : FXOverdose.Trading.TradingController.PositionType.Short;

            float allowedMarginRatio = 1.0f;
            if (FXOverdose.Trading.TraderLevelSystem.Instance != null)
            {
                allowedMarginRatio = FXOverdose.Trading.TraderLevelSystem.Instance.GetMaxAllowedMarginRatio();
            }

            tradingController.ExecuteEmergencyTrade(randomPos, leverage, 30, FXOverdose.Trading.TradingController.EventPositionHandlingMode.StandardAuto, 0f, 0f, false, true, allowedMarginRatio, 2.0f);

            var visual = FindAnyObjectByType<FXOverdose.AI.AIVisualController>();
            visual?.DisplayDialogueBalloon("더는 못 참아!! 100배로 싹 다 복구한다!!");
            Debug.LogWarning($"[TraderStatus] ⚠️ TriggerImpulsiveTrade 발동: {randomPos} {leverage}배 강제 진입 대기 시작 (마진 비율: {allowedMarginRatio:F2})");
        }
    }

    public void ModifyHealthState(float amount)
    {
        ChangeHealth(amount);
    }

    public bool HasItem(string itemId, int count = 1)
    {
        if (string.IsNullOrEmpty(itemId) || count <= 0) return true;

        Inventory inv = FindAnyObjectByType<Inventory>();
        if (inv != null)
        {
            return inv.GetQuantity(itemId) >= count;
        }

        return false;
    }

    public bool ConsumeItem(string itemId, int count = 1)
    {
        if (string.IsNullOrEmpty(itemId) || count <= 0) return true;

        Inventory inv = FindAnyObjectByType<Inventory>();

        if (inv != null)
        {
            // InventorySlot 순회하여 itemId가 일치하는 아이템 찾기
            foreach (var slot in inv.Slots)
            {
                if (slot != null && slot.Item != null && slot.Item.ItemId == itemId)
                {
                    if (inv.GetQuantity(slot.Item) >= count)
                    {
                        return inv.RemoveItem(slot.Item, count);
                    }
                    break;
                }
            }
        }

        return false;
    }
}
