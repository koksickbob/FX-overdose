using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 인벤토리에 저장되는 아이템 한 종류와 보유 개수입니다.
/// </summary>
[Serializable]
public class InventorySlot
{
    [SerializeField] private ItemData item;
    [SerializeField, Min(0)] private int quantity;

    public ItemData Item => item;
    public int Quantity => quantity;

    public InventorySlot(ItemData item, int quantity)
    {
        this.item = item;
        this.quantity = Mathf.Max(0, quantity);
    }

    public void Add(int amount)
    {
        quantity += amount;
    }

    public bool RemoveOne()
    {
        if (quantity <= 0)
        {
            return false;
        }

        quantity--;
        return true;
    }

    public bool RemoveAmount(int amount)
    {
        if (quantity < amount || amount <= 0)
        {
            return false;
        }

        quantity -= amount;
        return true;
    }
}

/// <summary>
/// 아이템별 보유 개수를 저장하고 아이템 추가 및 사용을 처리합니다.
/// </summary>
public class Inventory : MonoBehaviour
{
    [Header("아이템 사용 처리")]
    [SerializeField] private ItemUser itemUser;

    [Header("보유 아이템")]
    [SerializeField] private List<InventorySlot> slots = new();

    // UI가 아이템 개수 변경을 감지할 때 구독할 이벤트입니다.
    public event Action<ItemData, int> QuantityChanged;

    public IReadOnlyList<InventorySlot> Slots => slots;

    private void Awake()
    {
        RemoveInvalidAndDuplicateSlots();
    }

    /// <summary>
    /// 아이템을 지정한 개수만큼 인벤토리에 추가합니다.
    /// </summary>
    public bool AddItem(ItemData item, int amount = 1)
    {
        if (item == null)
        {
            Debug.LogWarning("[Inventory] 추가할 ItemData가 없습니다.", this);
            return false;
        }

        if (amount <= 0)
        {
            Debug.LogWarning("[Inventory] 추가 개수는 1 이상이어야 합니다.", this);
            return false;
        }

        InventorySlot slot = FindSlot(item);

        if (slot == null)
        {
            slot = new InventorySlot(item, amount);
            slots.Add(slot);
        }
        else
        {
            slot.Add(amount);
        }

        QuantityChanged?.Invoke(item, slot.Quantity);
        Debug.Log($"[Inventory] {item.ItemName} +{amount}, 현재 {slot.Quantity}개");
        return true;
    }

    /// <summary>
    /// 보유 중인 아이템을 사용합니다.
    /// 효과 적용에 성공한 경우에만 보유 개수를 1개 차감합니다.
    /// </summary>
    public bool UseItem(ItemData item)
    {
        if (itemUser == null)
        {
            Debug.LogError("[Inventory] ItemUser가 연결되지 않았습니다.", this);
            return false;
        }

        if (item == null)
        {
            Debug.LogWarning("[Inventory] 사용할 ItemData가 없습니다.", this);
            return false;
        }

        InventorySlot slot = FindSlot(item);

        if (slot == null || slot.Quantity <= 0)
        {
            Debug.Log($"[Inventory] {item.ItemName}을(를) 보유하고 있지 않습니다.");
            return false;
        }

        // 체력 또는 멘탈이 가득 차 아이템 효과가 적용되지 않으면 차감하지 않습니다.
        if (!itemUser.TryUseItem(item))
        {
            return false;
        }

        slot.RemoveOne();
        QuantityChanged?.Invoke(item, slot.Quantity);
        Debug.Log($"[Inventory] {item.ItemName} 사용 완료, 남은 수량 {slot.Quantity}개");
        return true;
    }

    /// <summary>
    /// 지정된 수량의 아이템을 차감합니다. (돌발 선택 이벤트 등 이벤트 소비용)
    /// </summary>
    public bool RemoveItem(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return false;
        InventorySlot slot = FindSlot(item);
        if (slot == null || slot.Quantity < amount) return false;
        slot.RemoveAmount(amount);
        QuantityChanged?.Invoke(item, slot.Quantity);
        Debug.Log($"[Inventory] {item.ItemName} {amount}개 소비 완료, 남은 수량 {slot.Quantity}개");
        return true;
    }

    /// <summary>
    /// 해당 아이템의 현재 보유 개수를 반환합니다.
    /// </summary>
    public int GetQuantity(ItemData item)
    {
        if (item == null)
        {
            return 0;
        }

        InventorySlot slot = FindSlot(item);
        return slot?.Quantity ?? 0;
    }

    private InventorySlot FindSlot(ItemData item)
    {
        return slots.Find(slot => slot != null && slot.Item == item);
    }

    // Inspector에서 비어 있는 슬롯과 중복 아이템을 정리합니다.
    private void RemoveInvalidAndDuplicateSlots()
    {
        HashSet<ItemData> foundItems = new();

        for (int i = slots.Count - 1; i >= 0; i--)
        {
            InventorySlot slot = slots[i];

            if (slot == null || slot.Item == null || !foundItems.Add(slot.Item))
            {
                slots.RemoveAt(i);
            }
        }
    }
}
