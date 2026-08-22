using UnityEngine;

/// <summary>
/// ItemData에 정의된 효과를 AI 트레이더에게 적용합니다.
/// 아이템 보유 개수와 차감 처리는 이후 Inventory 시스템이 담당합니다.
/// </summary>
public class ItemUser : MonoBehaviour
{
    [Header("아이템 효과 대상")]
    [SerializeField] private TraderStatus traderStatus;

    /// <summary>
    /// Unity UI Button 등에서 아이템 사용을 요청할 때 호출합니다.
    /// </summary>
    public void UseItem(ItemData item)
    {
        TryUseItem(item);
    }

    /// <summary>
    /// 아이템 효과를 적용하고, 실제로 사용했는지를 반환합니다.
    /// 이후 Inventory는 true일 때만 보유 개수를 차감하면 됩니다.
    /// </summary>
    public bool TryUseItem(ItemData item)
    {
        TraderStatus canonical = TraderStatus.CanonicalInstance;
        if (canonical != null) traderStatus = canonical;

        if (traderStatus == null)
        {
            Debug.LogError("[ItemUser] TraderStatus가 연결되지 않았습니다.", this);
            return false;
        }

        bool isOverdoseGimmick = false;
        if (FXOverdose.Trading.TradingController.Instance != null)
        {
            isOverdoseGimmick = FXOverdose.Trading.TradingController.Instance.IsOverdoseTradeActive;
        }

        if (traderStatus.CurrentMentalState == TraderStatus.MentalState.Overdose || isOverdoseGimmick)
        {
            Debug.LogWarning("[ItemUser] 오버도즈 기믹 진행 중이거나 오버도즈 상태에서는 아이템을 사용할 수 없습니다.", this);
            return false;
        }

        if (item == null)
        {
            Debug.LogWarning("[ItemUser] 사용할 ItemData가 없습니다.", this);
            return false;
        }

        if (item.EffectAmount <= 0f)
        {
            Debug.LogWarning($"[ItemUser] {item.ItemName}의 효과량이 0 이하입니다.", item);
            return false;
        }

        switch (item.Type)
        {
            case ItemData.EffectType.Health:
                return RestoreHealth(item);

            case ItemData.EffectType.Mental:
                return RestoreMental(item);

            case ItemData.EffectType.DeliveryFood:
                return UseDeliveryFood(item);

            case ItemData.EffectType.ProfitBoost:
            case ItemData.EffectType.LossReduction:
            case ItemData.EffectType.MentalDrainGuard:
            case ItemData.EffectType.HealthDrainGuard:
                Debug.Log($"[ItemUser] {item.ItemName}은(는) 액티브 패시브 버프/업그레이드 아이템이므로 인벤토리에서 소모 사용되지 않고 상시 적용됩니다.");
                return false;

            default:
                Debug.LogWarning($"[ItemUser] 지원하지 않는 아이템 효과입니다: {item.Type}", item);
                return false;
        }
    }

    private bool UseDeliveryFood(ItemData item)
    {
        float health = 0f;
        float mental = 0f;
        bool specialEffect = false;

        switch (item.ItemId)
        {
            case "malatang": health = 25f; mental = 50f; break;
            case "sushi": health = 30f; mental = 55f; break;
            case "tteokbokki": health = 30f; mental = 60f; break;
            case "pasta":
                health = 15f;
                mental = 15f;
                DeliveryFoodManager.EnsureInstance().ActivateOrRefreshPasta();
                specialEffect = true;
                break;
            case "steak":
                traderStatus.IncreaseMaxMental(10f);
                traderStatus.ChangeHealth(traderStatus.MaxHealth);
                traderStatus.ChangeMental(traderStatus.MaxMental);
                specialEffect = true;
                break;
            default:
                Debug.LogWarning($"[ItemUser] 알 수 없는 배달 음식입니다: {item.ItemId}", item);
                return false;
        }

        // 치파오·한복·유카타 스킨 효과: 배달음식의 기본 체력/멘탈 회복량 15% 증가 (특수 효과 수치는 제외)
        // 세 코스튬 모두 설명에 같은 효과가 적혀 있는데 치파오만 구현돼 있었습니다.
        if (CostumeManager.IsAnyEquipped(CostumeManager.QipaoId, CostumeManager.HanbokId, CostumeManager.YukataId))
        {
            health *= 1.15f;
            mental *= 1.15f;
        }

        bool canHealHealth = health > 0f && traderStatus.CurrentHealth < traderStatus.MaxHealth;
        bool canHealMental = mental > 0f && traderStatus.CurrentMental < traderStatus.MaxMental;
        if (!canHealHealth && !canHealMental && !specialEffect) return false;

        if (canHealHealth) traderStatus.ChangeHealth(health);
        if (canHealMental) traderStatus.ChangeMental(mental);
        Object.FindAnyObjectByType<FXOverdose.AI.MentalDrainGimmickController>()?.CureMentalGimmicks();
        TriggerItemDialogue(item);
        FXOverdose.Core.AchievementManager.Instance?.RecordItemUsage(item.ItemId);
        Debug.Log($"[ItemUser] 배달 음식 {item.ItemName} 사용: HP +{health}, Mental +{mental}");
        return true;
    }

    

    private void TriggerItemDialogue(ItemData item)
    {
        
        var visual = UnityEngine.Object.FindAnyObjectByType<FXOverdose.AI.AIVisualController>(UnityEngine.FindObjectsInactive.Include); if (visual != null) visual.DisplayDialogueBalloon($"{item.ItemName} 복용 (효과: {item.Type} +{item.EffectAmount})", FXOverdose.AI.DialoguePriority.Normal, FXOverdose.AI.EventCategory.ItemUsed);
    }

    // 체력이 가득 차 있지 않을 때 체력 회복 효과를 적용합니다.
    private bool RestoreHealth(ItemData item)
    {
        if (traderStatus.CurrentHealth >= traderStatus.MaxHealth)
        {
            Debug.Log("[ItemUser] 체력이 이미 가득 찼습니다.");
            return false;
        }

        float effectAmount = item.EffectAmount;
        string itemId = item.ItemId != null ? item.ItemId.ToLowerInvariant() : string.Empty;

        // 바텐더: 에너지 드링크 체력 회복량 +15%
        if (itemId.Contains("energy") && CostumeManager.IsAnyEquipped(CostumeManager.BartenderId))
        {
            effectAmount *= 1.15f;
        }

        // 간호사: 영양제 효율 +15% (진정제 쪽은 RestoreMental에서 처리)
        if (itemId.Contains("supplement") && CostumeManager.IsAnyEquipped(CostumeManager.NurseId))
        {
            effectAmount *= 1.15f;
        }

        traderStatus.ChangeHealth(effectAmount);
        TriggerItemDialogue(item);
        Debug.Log($"[ItemUser] {item.ItemName} 사용: 체력 +{effectAmount}");
        
        FXOverdose.Core.AchievementManager.Instance?.RecordItemUsage(item.ItemId);
        return true;
    }

    // 멘탈이 가득 차 있지 않을 때 멘탈 회복 효과를 적용합니다.
    private bool RestoreMental(ItemData item)
    {
        // [기획서 4.4장 부합] 진정제나 멘탈 회복제 투여 시 고배율 중독 상태 치료.
        // 고배율 중독은 멘탈 수치와 독립된 상태라, 만땅 가드보다 먼저 판정해야 합니다.
        // 예전에는 가드가 앞에 있어 멘탈이 가득 차면 진정제를 먹어도 중독이 풀리지 않았습니다.
        bool curesAddiction = traderStatus.IsLeverageAddicted &&
            (item.ItemName.Contains("진정") || item.ItemName.Contains("수면") || item.EffectAmount >= 20f);

        bool mentalFull = traderStatus.CurrentMental >= traderStatus.MaxMental;
        if (mentalFull && !curesAddiction)
        {
            Debug.Log("[ItemUser] 멘탈이 이미 최대치까지 가득 찼습니다.");
            return false;
        }

        Object.FindAnyObjectByType<FXOverdose.AI.MentalDrainGimmickController>()?.CureMentalGimmicks();

        float finalEffectAmount = item.EffectAmount;
        string mentalItemId = item.ItemId != null ? item.ItemId.ToLowerInvariant() : string.Empty;

        // 메이드: 파르페(ItemId "dessert") 멘탈 회복량 +15%
        if (mentalItemId.Contains("dessert") && CostumeManager.IsAnyEquipped(CostumeManager.MaidId))
        {
            finalEffectAmount *= 1.15f;
        }

        // 간호사: 진정제 효율 +15% (영양제 쪽은 RestoreHealth에서 처리)
        if (mentalItemId.Contains("sedative") && CostumeManager.IsAnyEquipped(CostumeManager.NurseId))
        {
            finalEffectAmount *= 1.15f;
        }

        if (!mentalFull) traderStatus.ChangeMental(finalEffectAmount);
        if (curesAddiction) traderStatus.CureLeverageAddiction();

        TriggerItemDialogue(item);
        Debug.Log($"[ItemUser] {item.ItemName} 사용: 멘탈 +{item.EffectAmount} 회복");
        
        FXOverdose.Core.AchievementManager.Instance?.RecordItemUsage(item.ItemId);
        return true;
    }
}

