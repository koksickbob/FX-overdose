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

            default:
                Debug.LogWarning($"[ItemUser] 지원하지 않는 아이템 효과입니다: {item.Type}", item);
                return false;
        }
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
        Debug.Log($"[ItemUser] {item.ItemName} 사용: 체력 +{item.EffectAmount}");
        return true;
    }

    // 멘탈이 가득 차 있지 않을 때 멘탈 회복 효과를 적용합니다.
    private bool RestoreMental(ItemData item)
    {
        if (traderStatus.CurrentMental >= traderStatus.MaxMental)
        {
            Debug.Log("[ItemUser] 멘탈이 이미 가득 찼습니다.");
            return false;
        }

        traderStatus.ChangeMental(item.EffectAmount);
        Debug.Log($"[ItemUser] {item.ItemName} 사용: 멘탈 +{item.EffectAmount}");
        return true;
    }
}
