using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 액티브 아이템(패시브 버프 및 2단계 업그레이드)의 보유 레벨을 추적하고,
/// 실시간 보정치(수익 증폭, 손실 감소, 체력/멘탈 감소 완화)를 연산하여 다른 시스템에 제공합니다.
/// UI 제작자가 쉽게 연결할 수 있는 상태 조회 API와 이벤트를 포함합니다.
/// </summary>
public class ActiveItemEffectManager : MonoBehaviour
{
    public static ActiveItemEffectManager Instance { get; private set; }

    // 아이템별 현재 레벨 (0 = 미보유, 1 = LV.1, 2 = LV.2 MAX)
    private readonly Dictionary<ItemData, int> itemLevels = new();

    // 외부 시스템에서 즉시 조회할 수 있는 합산 보정 비율 (0.1 = 10%)
    public float ProfitBoostRate { get; private set; }       // 수익률 추가 비율
    public float LossReductionRate { get; private set; }     // 손실 감소 비율
    public float MentalDrainReduction { get; private set; }  // 멘탈 감소 완화 비율
    public float HealthDrainReduction { get; private set; }  // 체력 감소 완화 비율

    // 액티브 아이템 상태 변경 시 UI 갱신을 위해 통지되는 이벤트
    public event Action OnActiveItemsChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 아이템의 현재 보유 레벨을 반환합니다. (0이면 미보유)
    /// </summary>
    public int GetItemLevel(ItemData item)
    {
        if (item == null) return 0;
        itemLevels.TryGetValue(item, out int level);
        return level;
    }

    /// <summary>HUD 등 읽기 전용 UI가 현재 활성 아이템과 레벨을 할당 없이 복사합니다.</summary>
    public void CopyActiveItemLevels(List<KeyValuePair<ItemData, int>> destination)
    {
        if (destination == null) return;
        destination.Clear();
        foreach (KeyValuePair<ItemData, int> entry in itemLevels)
        {
            if (entry.Key != null && entry.Value > 0) destination.Add(entry);
        }
    }

    /// <summary>
    /// 아이템이 최대 레벨(구매 가능 횟수 한계)에 도달했는지 여부를 반환합니다.
    /// </summary>
    public bool IsMaxLevel(ItemData item)
    {
        if (item == null || !item.IsActiveItem) return false;
        return GetItemLevel(item) >= item.MaxLevel;
    }

    /// <summary>
    /// 다음 구매/업그레이드에 필요한 가격을 계산합니다.
    /// </summary>
    public int GetNextUpgradePrice(ItemData item)
    {
        if (item == null) return 0;
        
        float basePrice = item.Price;
        
        GameManager gm = FindAnyObjectByType<GameManager>();
        if (gm != null)
        {
            // 액티브/패시브 아이템도 2일마다 20% 상승
            basePrice *= Mathf.Pow(1.2f, (gm.CurrentDay - 1) / 2f);
        }

        int currentLevel = GetItemLevel(item);
        if (currentLevel <= 0) return Mathf.RoundToInt(basePrice);

        // 초기 구매(LV.1 -> LV.2) 시 priceMultiplierPerLevel 반영
        float multiplier = Mathf.Pow(item.PriceMultiplierPerLevel, currentLevel);
        return Mathf.RoundToInt(basePrice * multiplier);
    }

    /// <summary>
    /// 상점에서 액티브 아이템 구매 시 호출하여 레벨을 1 증가시키고 보정치를 재계산합니다.
    /// </summary>
    public bool UpgradeOrActivateItem(ItemData item)
    {
        if (item == null || !item.IsActiveItem) return false;
        if (IsMaxLevel(item))
        {
            Debug.LogWarning($"[ActiveItemEffectManager] {item.ItemName}은(는) 이미 최대 레벨({item.MaxLevel})입니다.");
            return false;
        }

        int currentLevel = GetItemLevel(item);
        itemLevels[item] = currentLevel + 1;

        Debug.Log($"[ActiveItemEffectManager] ✨ {item.ItemName} 업그레이드 완료: LV.{currentLevel} ➔ LV.{itemLevels[item]} (최대 LV.{item.MaxLevel})");

        RecalculateModifiers();
        OnActiveItemsChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 저장 가능한 형태로 현재 액티브 아이템 ID와 레벨을 내보냅니다.
    /// </summary>
    public void CaptureSaveData(List<string> itemIds, List<int> levels)
    {
        if (itemIds == null || levels == null) return;
        itemIds.Clear();
        levels.Clear();

        foreach (KeyValuePair<ItemData, int> entry in itemLevels)
        {
            ItemData item = entry.Key;
            int level = entry.Value;
            if (item == null || !item.IsActiveItem || string.IsNullOrWhiteSpace(item.ItemId) || level <= 0)
                continue;

            itemIds.Add(item.ItemId);
            levels.Add(Mathf.Clamp(level, 1, item.MaxLevel));
        }
    }

    /// <summary>
    /// 저장된 아이템 ID를 현재 상점 카탈로그의 ItemData와 연결해 보유 레벨과 버프를 복원합니다.
    /// </summary>
    public void RestoreFromSaveData(
        IReadOnlyList<ItemData> catalog,
        IReadOnlyList<string> itemIds,
        IReadOnlyList<int> levels)
    {
        itemLevels.Clear();

        if (catalog != null && itemIds != null && levels != null)
        {
            Dictionary<string, ItemData> itemsById = new(StringComparer.Ordinal);
            foreach (ItemData item in catalog)
            {
                if (item == null || !item.IsActiveItem || string.IsNullOrWhiteSpace(item.ItemId)) continue;
                itemsById[item.ItemId] = item;
            }

            int count = Mathf.Min(itemIds.Count, levels.Count);
            for (int i = 0; i < count; i++)
            {
                string itemId = itemIds[i];
                if (string.IsNullOrWhiteSpace(itemId) ||
                    !itemsById.TryGetValue(itemId, out ItemData item))
                {
                    Debug.LogWarning($"[ActiveItemEffectManager] 저장된 액티브 아이템을 현재 카탈로그에서 찾지 못했습니다: {itemId}");
                    continue;
                }

                int restoredLevel = Mathf.Clamp(levels[i], 0, item.MaxLevel);
                if (restoredLevel > 0) itemLevels[item] = restoredLevel;
            }
        }

        RecalculateModifiers();
        OnActiveItemsChanged?.Invoke();
        Debug.Log($"[ActiveItemEffectManager] 저장된 액티브 아이템 {itemLevels.Count}종 복원 완료");
    }

    /// <summary>
    /// 현재 보유 중인 모든 액티브 아이템들의 보정치를 집계합니다.
    /// </summary>
    private void RecalculateModifiers()
    {
        float profitBoost = 0f;
        float lossReduction = 0f;
        float mentalGuard = 0f;
        float healthGuard = 0f;

        foreach (var kvp in itemLevels)
        {
            ItemData item = kvp.Key;
            int level = kvp.Value;
            if (item == null || level <= 0) continue;

            // EffectAmount가 10이면 10% (0.1), 0.1이면 0.1 그대로 처리할 수 있도록 환산
            float baseRate = item.EffectAmount >= 1f ? item.EffectAmount * 0.01f : item.EffectAmount;

            switch (item.Type)
            {
                case ItemData.EffectType.ProfitBoost:
                    // 레벨당 보정치 누적 (예: LV.1 +10%, LV.2 +20%)
                    profitBoost += baseRate * level;
                    break;

                case ItemData.EffectType.LossReduction:
                    // 레벨당 손실 완화 누적 (예: LV.1 -15%, LV.2 -30%)
                    lossReduction += baseRate * level;
                    break;

                case ItemData.EffectType.MentalDrainGuard:
                    // 단회/중첩 감소 완화 (최대 80% 완화 제한)
                    mentalGuard += baseRate * level;
                    break;

                case ItemData.EffectType.HealthDrainGuard:
                    // 단회/중첩 감소 완화 (최대 80% 완화 제한)
                    healthGuard += baseRate * level;
                    break;
            }
        }

        ProfitBoostRate = Mathf.Max(0f, profitBoost);
        LossReductionRate = Mathf.Clamp(lossReduction, 0f, 0.8f);
        MentalDrainReduction = Mathf.Clamp(mentalGuard, 0f, 0.8f);
        HealthDrainReduction = Mathf.Clamp(healthGuard, 0f, 0.8f);

        Debug.Log($"[ActiveItemEffectManager] 보정치 갱신 ➔ 수익 증폭: +{ProfitBoostRate * 100:0.0}%, 손실 감소: -{LossReductionRate * 100:0.0}%, 멘탈 보호: -{MentalDrainReduction * 100:0.0}%, 체력 보호: -{HealthDrainReduction * 100:0.0}%");
    }

    /// <summary>
    /// 새 게임 시작 또는 초기화 시 모든 액티브 아이템 레벨과 보정치를 리셋합니다.
    /// </summary>
    public void ResetAll()
    {
        itemLevels.Clear();
        ProfitBoostRate = 0f;
        LossReductionRate = 0f;
        MentalDrainReduction = 0f;
        HealthDrainReduction = 0f;

        Debug.Log("[ActiveItemEffectManager] 🔄 모든 액티브 아이템 및 보정치가 초기화되었습니다.");
        OnActiveItemsChanged?.Invoke();
    }

    // --- UI 제작자가 바로 연결해서 쓸 수 있는 Helper API ---

    /// <summary>
    /// 상점 버튼/카드에 표시할 액티브 아이템 상태 문자열을 반환합니다.
    /// 예: "레벨 1 ➔ 최대 레벨 2", "영구 효과", "최대 레벨 2 ✓", "활성화 ✓"
    /// </summary>
    public string GetItemStatusLabel(ItemData item)
    {
        if (item == null || !item.IsActiveItem) return string.Empty;

        int currentLevel = GetItemLevel(item);
        bool isMax = IsMaxLevel(item);

        if (item.MaxLevel <= 1)
        {
            return isMax ? "활성화 ✓" : "영구 효과";
        }
        else
        {
            if (isMax) return $"최대 레벨 {item.MaxLevel} ✓";
            return $"레벨 {currentLevel} ➔ {currentLevel + 1} (최대 {item.MaxLevel})";
        }
    }

    /// <summary>
    /// 인벤토리/HUD 상단 등에 표시할 액티브 버프 요약 문자열을 반환합니다.
    /// </summary>
    public string GetSummaryText()
    {
        if (itemLevels.Count == 0) return "활성 효과: 없음";

        StringBuilder sb = new StringBuilder("활성 효과: ");
        List<string> buffs = new List<string>();

        if (ProfitBoostRate > 0f) buffs.Add($"수익 +{ProfitBoostRate * 100:0}%");
        if (LossReductionRate > 0f) buffs.Add($"손실 -{LossReductionRate * 100:0}%");
        if (MentalDrainReduction > 0f) buffs.Add($"멘탈 감소 -{MentalDrainReduction * 100:0}%");
        if (HealthDrainReduction > 0f) buffs.Add($"체력 감소 -{HealthDrainReduction * 100:0}%");

        if (buffs.Count == 0) return "활성 효과: 없음";
        sb.Append(string.Join("  |  ", buffs));
        return sb.ToString();
    }
}
