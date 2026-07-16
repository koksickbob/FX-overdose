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

    private FXOverdose.AI.LLM.LocalLLMService llmService;

    private void TriggerItemDialogue(ItemData item)
    {
        if (llmService == null) llmService = FXOverdose.AI.LLM.LocalLLMService.Instance;
        llmService?.RequestDialogue(FXOverdose.AI.LLM.EventCategory.ItemUsed, $"{item.ItemName} 복용 (효과: {item.Type} +{item.EffectAmount})");
    }

    // 체력이 가득 차 있지 않을 때 체력 회복 효과를 적용합니다.
    private bool RestoreHealth(ItemData item)
    {
        if (traderStatus.CurrentHealth >= traderStatus.MaxHealth)
        {
            Debug.Log("[ItemUser] 체력이 이미 가득 찼습니다.");
            return false;
        }

        traderStatus.ChangeHealth(item.EffectAmount);
        TriggerItemDialogue(item);
        Debug.Log($"[ItemUser] {item.ItemName} 사용: 체력 +{item.EffectAmount}");
        return true;
    }

    // 멘탈이 가득 차 있지 않거나, 트라우마로 인해 한계치(천장)가 제한되었을 때 멘탈 및 한계치 회복 효과를 적용합니다.
    private bool RestoreMental(ItemData item)
    {
        if (traderStatus.CurrentMental >= traderStatus.MaxMental && traderStatus.MaxMentalLimit >= traderStatus.MaxMental)
        {
            Debug.Log("[ItemUser] 멘탈과 최대 한계치가 이미 최대치(100)까지 가득 찼습니다.");
            return false;
        }

        // 💡 [트라우마 천장 극복 기믹] 만약 드로다운 트라우마 등으로 인해 멘탈 한계치(MaxMentalLimit)가 100 미만으로 제한된 상태라면,
        // 디저트 및 멘탈 회복 아이템 사용 시 제한된 천장(한계치) 자체를 함께 상승시켜 트라우마 극복을 돕습니다!
        if (traderStatus.MaxMentalLimit < traderStatus.MaxMental || traderStatus.HasDrawdownTrauma)
        {
            float newLimit = Mathf.Min(traderStatus.MaxMental, traderStatus.MaxMentalLimit + item.EffectAmount);
            traderStatus.SetMaxMentalCeiling(newLimit);

            if (newLimit >= traderStatus.MaxMental)
            {
                traderStatus.HasDrawdownTrauma = false;
                Debug.Log("[ItemUser] ✨ 당분 및 진정제 효과로 드로다운 트라우마 천장 제한이 완전히 극복되었습니다!");
            }
        }

        Object.FindAnyObjectByType<FXOverdose.AI.MentalDrainGimmickController>()?.CureMentalGimmicks();
        traderStatus.ChangeMental(item.EffectAmount, true);

        // [기획서 4.4장 부합] 진정제나 멘탈 회복제 투여 시 고배율 중독 상태 치료
        if (traderStatus.IsLeverageAddicted && (item.ItemName.Contains("진정") || item.ItemName.Contains("수면") || item.EffectAmount >= 20f))
        {
            traderStatus.CureLeverageAddiction();
        }

        TriggerItemDialogue(item);
        Debug.Log($"[ItemUser] {item.ItemName} 사용: 멘탈 및 한계치 +{item.EffectAmount} 회복");
        return true;
    }
}
