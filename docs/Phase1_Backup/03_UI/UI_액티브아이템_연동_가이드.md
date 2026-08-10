# [UI 제작자를 위한] 액티브 아이템 & 업그레이드 시스템 연동 가이드

본 문서는 **FX Overdose** 프로젝트에 새롭게 도입된 **"액티브 아이템 (패시브 버프 & 2단계 업그레이드)"** 시스템의 UI 커스텀 및 연동 방법을 UI/GUI 제작자 분들께 안내하기 위해 작성되었습니다.

개발팀에서 이미 핵심 로직과 상태 계산, 상점 구매 분기 및 UI 동적 갱신 뼈대 코드를 모두 완성해 두었으므로, UI 제작자 분들은 **이미지 에셋 교체, 레이아웃 커스텀, 그리고 아래 안내된 간단한 Helper API 연결**만으로 완벽한 상점 및 인벤토리 UI를 완성하실 수 있습니다!

---

## 1. 아이템 시스템 이원화 개요

이제 게임 내 아이템은 크게 **2가지 범주**로 구분됩니다.

| 범주 | 설명 | 상점 카드 UI 특징 | 인벤토리 동작 |
|:---|:---|:---|:---|
| **1. 소모형 아이템**<br>(Consumables) | 구매 즉시 인벤토리 슬롯으로 들어가며, 클릭하여 사용 시 체력(`Health`) 또는 멘탈(`Mental`)을 즉시 회복하고 사라집니다. | 기존 Blue / Pink 계열 카드 디자인 | 인벤토리 슬롯(`OWNED x3`)에 누적되며, 클릭 시 사용 |
| **2. 액티브 아이템**<br>(Active Buffs / Upgrades) | 구매 즉시 효과가 영구 활성화되며, 인벤토리 슬롯에 들어가지 않고 상시 적용되는 버프 아이템입니다. | **Gold / Orange / Cyan 계열 프레임 권장**<br>레벨 및 업그레이드 가격 표시 | 인벤토리 슬롯 대신 **[ACTIVE BUFFS 요약 바]**에 적용 수치 표시 |

### 액티브 아이템의 2가지 업그레이드 타입
1. **중첩 구매 (2단계 업그레이드 - 수익률 증가 / 손해 감소)**
   - 초기 1회 구매(`LV.1`) + 재구매 1회(`LV.2 MAX`) = **총 2회 구매 가능 (`MAX LV.2`)**
   - 상점 UI에서 `LV.1 ➔ MAX LV.2` 및 다음 업그레이드 비용이 실시간 갱신되어야 하며, 만렙 달성 시 버튼이 `MAX LV ✓`로 바뀌고 비활성화됩니다.
2. **단회 영구 적용 (Permanent Buff - 체력 / 멘탈 감소 완화)**
   - **단 1회만 구매 가능 (`MAX LV.1`)**
   - 한 번 구매하면 영구 유지되며 상점 버튼이 `ACTIVE ✓`로 바뀌고 비활성화됩니다.

---

## 2. ItemData Inspector 설정 방법 (Asset 세팅)

`Project` 창에서 `ItemData` 에셋을 선택하면 Inspector 창에 다음과 같은 항목들이 표시됩니다.

```yaml
[기본 정보]
  ItemId: "dual_monitor"
  ItemName: "듀얼 모니터"
  Description: "수익률이 증가합니다."
  Icon: [Sprite]

[사용 효과]
  Effect Type: ProfitBoost  # (Health, Mental, ProfitBoost, LossReduction, MentalDrainGuard, HealthDrainGuard)
  Effect Amount: 10         # 10이면 +10% (레벨당 증가 수치)

[상점 정보]
  Price: 2000               # 초기 구매 가격 ($2,000)

[액티브 아이템 설정 (패시브 버프/업그레이드)]  <-- ⭐ 새로 추가된 영역!
  Is Active Item: check [✔] # true로 체크해야 액티브 아이템으로 동작합니다.
  Max Level: 2              # 2이면 2단계 업그레이드, 1이면 단회 영구 적용
  Price Multiplier Per Level: 1.5 # 2단계 업그레이드 시 다음 가격 증가율 (2000 * 1.5 = $3,000)
```

---

## 3. 핵심 Helper API & 이벤트 (단 1개의 매니저만 연결하면 끝!)

액티브 아이템과 관련된 모든 상태 계산, 다음 가격 조회, 문자열 라벨 생성은 싱글톤 매니저인 **`ActiveItemEffectManager.Instance`**에서 100% 제공합니다. 복잡한 계산을 UI 스크립트에서 직접 하실 필요가 없습니다!

### 📌 API 조회 메서드 목록 (`ActiveItemEffectManager.Instance`)

```csharp
// 1. 특정 아이템의 현재 보유 레벨 반환 (0 = 미보유, 1 = LV.1, 2 = LV.2 MAX)
int level = ActiveItemEffectManager.Instance.GetItemLevel(itemData);

// 2. 특정 아이템이 최대 레벨(만렙/구매완료)인지 확인
bool isMax = ActiveItemEffectManager.Instance.IsMaxLevel(itemData);

// 3. 현재 레벨에 따른 다음 업그레이드 구매 비용 계산 ($2,000 -> $3,000)
int nextPrice = ActiveItemEffectManager.Instance.GetNextUpgradePrice(itemData);

// 4. ⭐ [추천] 상점 버튼/카드에 바로 넣을 수 있는 상태 문자열 자동 반환
// 반환 예시: "LV.1 ➔ LV.2 (MAX 2)" | "MAX LV.2 ✓" | "PERMANENT BUFF" | "ACTIVE ✓"
string statusText = ActiveItemEffectManager.Instance.GetItemStatusLabel(itemData);

// 5. ⭐ [추천] 인벤토리/HUD 상단에 바로 넣을 수 있는 전체 적용 버프 요약 문자열 자동 반환
// 반환 예시: "ACTIVE BUFFS: PROFIT +20%  |  LOSS -15%  |  MENTAL DRAIN -30%"
string summaryText = ActiveItemEffectManager.Instance.GetSummaryText();
```

---

## 4. UI 스크립트 작성 예시 (이벤트 자동 구독)

상점에서 아이템을 구매하거나 게임이 초기화될 때 UI가 **자동으로 새로고침**되게 하려면, UI 스크립트의 `OnEnable`에서 `OnActiveItemsChanged` 이벤트를 구독해주시면 됩니다. (현재 뼈대 스크립트인 `ShopItemButton.cs`와 `DynamicInventoryUI.cs`에 이미 적용되어 있으니 코드를 참고하세요!)

```csharp
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CustomShopCardUI : MonoBehaviour
{
    [SerializeField] private ItemData item;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text statusLabelText;
    [SerializeField] private Button buyButton;

    private void OnEnable()
    {
        // ⭐ 액티브 아이템 상태 변경 이벤트 구독 (자동 갱신)
        if (ActiveItemEffectManager.Instance != null)
        {
            ActiveItemEffectManager.Instance.OnActiveItemsChanged -= RefreshUI;
            ActiveItemEffectManager.Instance.OnActiveItemsChanged += RefreshUI;
        }
        RefreshUI();
    }

    private void OnDisable()
    {
        if (ActiveItemEffectManager.Instance != null)
        {
            ActiveItemEffectManager.Instance.OnActiveItemsChanged -= RefreshUI;
        }
    }

    public void RefreshUI()
    {
        if (item == null || ActiveItemEffectManager.Instance == null) return;

        if (item.IsActiveItem)
        {
            bool isMax = ActiveItemEffectManager.Instance.IsMaxLevel(item);

            // 1. 구매 버튼 비활성화 처리 (만렙 달성 시 클릭 차단)
            if (buyButton != null) buyButton.interactable = !isMax;

            // 2. 가격 표시 (만렙이면 "MAXED", 아니면 다음 단계 비용)
            if (priceText != null)
            {
                priceText.text = isMax ? "MAXED" : $"${ActiveItemEffectManager.Instance.GetNextUpgradePrice(item):N0}";
            }

            // 3. 상태 라벨 표시 ("LV.1 ➔ MAX LV.2" 등)
            if (statusLabelText != null)
            {
                statusLabelText.text = ActiveItemEffectManager.Instance.GetItemStatusLabel(item);
            }
        }
        else
        {
            // 기존 소모형 아이템 로직
            if (buyButton != null) buyButton.interactable = true;
            if (priceText != null) priceText.text = $"${item.Price:N0}";
        }
    }
}
```

---

## 5. UI 제작 권장 가이드 및 디자인 팁

### ① 상점 카드 UI 시각적 구분 (`DynamicShopUI.cs`)
현재 `DynamicShopUI.cs` 뼈대 코드에는 아이템 종류에 따라 카드 프레임 색상과 텍스트 색상을 자동으로 변경하는 로직이 들어 있습니다.
- **체력 소모형 (`Health`)**: 하늘색 / 청록색 테두리 (`#9EEDFF`)
- **멘탈 소모형 (`Mental`)**: 분홍색 / 보라색 테두리 (`#FFB8FA`)
- **액티브 아이템 (`IsActiveItem == true`)**: **골드 / 오렌지 계열 프리미엄 테두리 (`#FFE080`)** + 녹색 효과 텍스트

*👉 추후 커스텀 프리팹을 만드실 때도, 액티브 아이템 카드는 배경에 미세한 골드 그라데이션이나 번개/업그레이드 아이콘을 넣어 "영구 적용 프리미엄 효과"임을 플레이어에게 시각적으로 강조해주시면 훨씬 완성도가 높아집니다.*

### ② 인벤토리 패널 상단 버프 요약 바 (`DynamicInventoryUI.cs`)
액티브 아이템은 구매해도 인벤토리 16칸 슬롯에 들어가지 않습니다. (소모형 회복 아이템만 슬롯을 차지합니다.)
대신 플레이어가 자신이 어떤 영구 보정을 받고 있는지 확인할 수 있도록, 현재 인벤토리 창 상단 제목(`CARE ITEMS`) 옆에 **[ACTIVE BUFFS: PROFIT +20% | LOSS -15%]** 텍스트가 자동으로 렌더링되도록 뼈대(`activeBuffsText`)가 부착되어 있습니다.

*👉 추후 UI 에셋 작업 시 이 요약 바 텍스트를 인벤토리 창 상단이나 전용 HUD 상단 바(Top Bar)에 예쁘게 배치해주시면 됩니다. `ActiveItemEffectManager.Instance.GetSummaryText()` 메서드 하나만 호출하면 완성된 텍스트가 반환됩니다!*

---

## 6. 문의 및 협업 포인트
- **버튼 클릭 시 구매 요청**: `shopManager.BuyItem(item)`만 호출하면 가격 차감, 업그레이드 연산, 이벤트 발송, UI 갱신까지 모두 한 번에 자동으로 처리됩니다.
- 뼈대 코드 위치:
  - `Assets/Scripts/Items/ActiveItemEffectManager.cs` (핵심 매니저 및 Helper API)
  - `Assets/Scripts/Items/ShopItemButton.cs` (버튼 뼈대 로직)
  - `Assets/Scripts/Items/DynamicShopUI.cs` (상점 동적 카드 생성 로직)
  - `Assets/Scripts/Items/DynamicInventoryUI.cs` (인벤토리 상단 버프 텍스트 로직)

스크립트 연결 중 의문점이 있거나 추가적인 조회 API(예: 특정 버프의 아이콘만 따로 렌더링하기 위한 데이터 구조 등)가 필요하시면 언제든 개발팀에 말씀해 주세요!
