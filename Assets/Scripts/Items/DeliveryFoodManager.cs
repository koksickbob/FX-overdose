using UnityEngine;
using FXOverdose.Core;

public class DeliveryFoodManager : MonoBehaviour
{
    public const string SteakUnlockAchievementId = "purchase_consumable_50";
    public const float PastaDurationSeconds = 180f;
    public const float PastaTimeMultiplier = 1.5f;

    public static DeliveryFoodManager Instance { get; private set; }

    [SerializeField] private float pastaRemainingSeconds;
    [SerializeField] private int lastSteakPurchaseDay = -999;

    public float PastaRemainingSeconds => pastaRemainingSeconds;
    public int LastSteakPurchaseDay => lastSteakPurchaseDay;

    public static DeliveryFoodManager EnsureInstance()
    {
        if (Instance != null) return Instance;
        DeliveryFoodManager found = FindAnyObjectByType<DeliveryFoodManager>();
        if (found != null) return found;
        return new GameObject("DeliveryFoodManager").AddComponent<DeliveryFoodManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        if (pastaRemainingSeconds <= 0f) return;
        pastaRemainingSeconds = Mathf.Max(0f, pastaRemainingSeconds - Time.unscaledDeltaTime);
        ApplyPastaSpeed();
    }

    public void ActivateOrRefreshPasta()
    {
        pastaRemainingSeconds = PastaDurationSeconds;
        ApplyPastaSpeed();
    }

    public bool CanPurchaseSteak(int currentDay, out string reason)
    {
        if (AchievementManager.Instance == null ||
            !AchievementManager.Instance.IsAchievementUnlocked(SteakUnlockAchievementId))
        {
            reason = "업적 '큰손 고객' 달성 후 구매 가능";
            return false;
        }

        int daysRemaining = 7 - (currentDay - lastSteakPurchaseDay);
        if (daysRemaining > 0)
        {
            reason = $"{daysRemaining}일 후 재구매 가능";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public void RecordSteakPurchase(int currentDay) => lastSteakPurchaseDay = currentDay;

    public void Restore(int savedLastSteakDay, float savedPastaSeconds)
    {
        lastSteakPurchaseDay = savedLastSteakDay;
        pastaRemainingSeconds = Mathf.Max(0f, savedPastaSeconds);
        ApplyPastaSpeed();
    }

    private void ApplyPastaSpeed()
    {
        if (DynamicTimeRegulator.Instance != null)
            DynamicTimeRegulator.Instance.SetFoodTimeMultiplier(pastaRemainingSeconds > 0f ? PastaTimeMultiplier : 1f);
    }
}
