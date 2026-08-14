# 편의점 알바 타이쿤 미니게임 계획서

> 위치: `docs/P2_04_System/ConvenienceStore_Tycoon_MiniGame_Plan.md`
> 대상: DatingSim 파트(Phase 2) — 월드맵 `아르바이트` 액션의 실제 내용물
> 작성일: 2026-08-14
> 상태: 설계 확정 대기 (구현 착수 전)

---

## 0. 요약

월드맵의 아르바이트는 지금 **즉시 보상 지급**으로 잘려 있습니다 (`WorldMapManager.TryStartPartTimeJob` → 주석 `3. 미니게임 컷(스킵) 및 즉시 보상 지급`). 이 자리에 실제 플레이가 들어갑니다.

한 번의 근무(shift)는 **실시간 약 3분의 단일 세션**입니다. 세션 안에서 요미는 계산 / 매대 보충 / 청소를 처리하고, 그 성과가 일급과 랜덤 선물(트레이딩 파트 아이템)로 환산됩니다.

핵심 설계 원칙 세 가지:

1. **근무는 원자적(atomic)입니다.** 근무 중간 상태는 저장하지 않습니다. 세이브에 새로 들어가는 것은 필드 **1개**뿐입니다.
2. **기존 것을 그대로 씁니다.** 이동·애니메이션·시간/체력 자원·씬 전환·아이템 지급 경로 전부 이미 있는 코드를 재사용합니다.
3. **신규 매니저는 1개**입니다. 손님/매대/청소를 각각의 매니저로 쪼개지 않습니다.

---

## 1. 재사용 vs 신규 판단표

| 필요한 것 | 판단 | 근거 |
| --- | --- | --- |
| 탑다운 이동 | **재사용** — `YomiRoomTopDownController`의 이동 로직을 복제한 축소판 | 원본은 모달·`YomiRoomInteractable`·`RoomBusy`(YomiRoomManager 의존)에 묶여 있어 그대로는 못 씁니다. 이동부(WASD + `MovePosition`)만 40줄 수준으로 옮깁니다 |
| 걷기 애니메이션 | **재사용** — `YomiTopDownWalkAnimator` 그대로 | 입력 소스만 주입 가능하게 1줄 확장 (§7.2) |
| 스프라이트 | **재사용** — 요미 워크 시트, 손님도 동일 시트 색조만 변경 | 초안 명시. 교체는 아트 확정 후 |
| 시간 슬롯 / 체력 | **재사용** — `DatingTimeManager` | 이미 `DontDestroyOnLoad`라 편의점 씬에 따라옵니다 |
| 일급 지급 | **재사용** — `WorldMapManager.FinishPartTimeJob` | `GameManager` 없는 씬용 SaveData 폴백이 이미 구현돼 있습니다 |
| 선물 아이템 지급 | **신규 헬퍼 1개** — `SaveLoadManager.GrantItemToSave()` | `Inventory`/`ShopManager`는 GameScene에만 존재합니다 (§6.3) |
| 씬 전환 | **재사용** — `LoadingScreenController.TargetSceneToLoad` | 추가 작업 없음 |
| 손님 경로 탐색 | **신규 — 단, NavMesh 없음** | 웨이포인트 3~4개를 직선 보간으로 순회. 편의점은 장애물이 거의 없습니다 |
| 데이터 테이블 | **신규 ScriptableObject 1개** | §5 |
| UI | **신규 — 진행 바 + 결과 패널만** | §8 |

> 판단: NavMesh, 손님 오브젝트 풀링, 별도 asmdef, 상태 저장 계층은 **전부 제외**했습니다. 사유는 §12.

---

## 2. 씬 구조

**씬 파일**: `Assets/Scenes/DatingSim/ConvenienceStoreScene.unity`
**생성 방식**: 에디터 빌더로 전체 생성 (§7.1). 손으로 편집하지 마십시오 — 다음 빌드에서 덮어써집니다.

### 2.1 레이아웃 (월드 좌표, 카메라 orthographicSize 5.4 기준)

```
        ┌──────────────────────────────────────────────┐
        │  창고 STORAGE       (-7.5, +3.0)             │  ← 재고 상자 / 청소도구함
        ├──────────────────────────────────────────────┤
        │                                              │
        │   [매대 A]      [매대 B]      [매대 C]        │
        │  (-4.0,+0.5)   (0.0,+0.5)   (+4.0,+0.5)      │
        │                                              │
        │            (요미·손님 이동 영역)               │
        │                                              │
        │   [매대 D]      [매대 E]                      │
        │  (-4.0,-2.0)   (0.0,-2.0)                    │
        │                                              │
        ├───────────────┬──────────────────────────────┤
        │  계산대 (+5.5,-3.2)  │   출입문 (0,-4.6)      │
        └───────────────┴──────────────────────────────┘
```

### 2.2 오브젝트 계층

```
ConvenienceStoreRoot
├─ Camera (Orthographic, size 5.4)
├─ Store
│  ├─ Floor / Walls (BoxCollider2D, 요미의 방과 동일 방식)
│  ├─ Shelf_A ~ Shelf_E   : StoreStation(Shelf)  + SpriteRenderer + 재고 게이지
│  ├─ Counter             : StoreStation(Checkout)
│  ├─ Storage_Stock       : StoreStation(PickupStock)
│  ├─ Storage_Tool        : StoreStation(PickupTool)
│  └─ DirtSpots (런타임 생성) : StoreStation(Clean)
├─ Yomi (Rigidbody2D + StorePlayerController + YomiTopDownWalkAnimator)
├─ Customers (런타임 생성 부모)
├─ StoreShiftManager
└─ Canvas_Store (StoreShiftUI)
```

### 2.3 손님 웨이포인트

손님은 4점 경로만 씁니다: `입구 → 목표 매대 → 계산대 대기열 슬롯 → 입구(퇴장)`. 각 구간은 직선 `Vector2.MoveTowards`. 서로 겹치는 것은 **허용**합니다 (충돌 처리 안 함).

계산대 대기열은 슬롯 4개 고정 배열 (`Vector2[] queueSlots`). 5번째부터는 입장 자체를 막습니다 (스폰 스킵).

---

## 3. 상태 기계

### 3.1 근무 상태 (`StoreShiftState`)

```
Intro ──▶ Working ──▶ Settling ──▶ Result ──▶ (WorldMapScene 복귀)
                │
                └──▶ Aborted (플레이어 중도 포기) ──▶ Result
```

| 상태 | 내용 | 입력 |
| --- | --- | --- |
| `Intro` | "근무 시작" 3초 카운트다운, 손님 스폰 정지 | 잠금 |
| `Working` | 타이머 진행, 손님 스폰, 모든 액션 가능 | 허용 |
| `Settling` | 타이머 종료. 남은 손님 즉시 퇴장 처리, 오염/빈 매대 감점 확정 | 잠금 |
| `Result` | 결과 패널 표시, 일급·선물 확정 및 세이브 커밋 | 확인 버튼만 |
| `Aborted` | ESC → 확인 모달 → 즉시 `Settling` | — |

**중도 포기 정책**: 시간 슬롯·체력은 **환불하지 않습니다.** 일급은 그 시점까지의 성과로 정산합니다. (포기가 리셋 노가다 수단이 되면 안 됩니다.)

### 3.2 손님 상태 (`StoreCustomerState`)

```
Entering ──▶ Browsing ──▶ Queueing ──▶ WaitingAtCounter ──▶ Leaving
                 │                            │
                 │(15% 확률로 오염 스폰)        └──(인내 초과)──▶ Walkout
                 │                                                  │
                 └──────────────────────────────────────────────────┘
                                                    (물건을 계산대에 버림 → 정리 대상 생성)
```

| 상태 | 지속 | 비고 |
| --- | --- | --- |
| `Entering` | 입구 → 목표 매대 이동 | 목표 매대의 재고가 0이면 아무것도 못 집고 바로 `Walkout`. **이탈로 집계합니다** — 아래 경고 참조 |
| `Browsing` | 1.5초 정지 | 종료 시 해당 매대 재고 −1 ~ −3, **15% 확률로 그 매대 앞에 오염 스폰** |
| `Queueing` | 대기열 슬롯으로 이동 | 슬롯이 비면 앞으로 당김 |
| `WaitingAtCounter` | 인내 타이머 시작 (§5.4) | 머리 위 인내 게이지 표시 |
| `Walkout` | 인내 초과 | 일급 감점 + **계산대에 `잔여물` 오브젝트 생성** (요미가 청소 액션으로 치워야 함) |
| `Leaving` | 출구로 이동 후 Destroy | 계산 성공 시 **5% 확률로 선물 드롭** (§5.5) |

> ⚠️ **재고 없음 이탈을 감점 없이 두면 매대를 비워 두는 것이 최적 전략이 됩니다.** 매출이 없어진 지금(§9), 손님을 받지 않는 것에 아무 대가가 없으면 플레이어는 창고에 가지 않고 계산대만 지키면 됩니다. 그래서 이 이탈은 `walkoutPenalty`를 그대로 적용하고 등급 분모에도 포함시킵니다. 다만 잔여물은 생성하지 않습니다 (집은 물건이 없으므로).

### 3.3 요미 액션 = 홀드 진행도

**모든 액션은 `E` 키를 누르고 있는 동안만 진행됩니다.** 창고의 「무엇을 챙길지」 선택만 예외로, 근처에서 `E`를 **탭**하면 2지선다 모달이 뜹니다.

```csharp
// StoreStation 내부. 진행도의 주인은 스테이션 자신입니다 —
// 전역 레지스트리를 두지 않으므로 "떠났다가 돌아와서 이어하기"가 공짜로 됩니다.
public float Progress;      // 0..1, E를 떼면 그 값에서 정지 (감쇠 없음)
public float RequiredTime;  // 테이블에서 주입
public bool IsBlocked;      // 손이 비었는데 매대 보충을 시도하는 등
```

진행 규칙:

- `E` 누름 + 스테이션 반경 1.4 이내 + 전제조건 충족 → `Progress += dt / RequiredTime`
- `E` 뗌 / 반경 이탈 → **정지, 값 유지** (초안 요구사항)
- `Progress >= 1` → `Complete()` 실행 후 `Progress = 0`
- 계산대는 예외: 손님이 바뀌면 진행도 리셋 (이전 손님의 진행도를 승계하면 무료 계산이 됩니다)

**전제조건 (`IsBlocked` 판정)**

| 스테이션 | 전제조건 | 실패 시 안내 문구 |
| --- | --- | --- |
| `Checkout` | 대기열 맨 앞 손님 존재 | "계산할 손님이 없어요." |
| `Shelf` | 요미가 `Carry.Stock` 보유 & 해당 매대 재고 < 최대 | "채울 물건이 없어요." / "이미 가득 찼어요." |
| `Clean` | 요미가 `Carry.Tool` 보유 | "청소도구가 필요해요." |
| `PickupStock` / `PickupTool` | 항상 가능 (교체됨) | — |

### 3.4 요미 소지 상태 (`Carry`)

```csharp
enum Carry { None, Stock, Tool }   // 상자 또는 대걸레, 동시 소지 불가
int carriedStockUnits;             // Carry.Stock일 때만 유효. 창고 1회 = 6유닛
```

창고에서 다른 것을 집으면 들고 있던 것은 **버려집니다** (남은 재고 유닛 소멸). 인벤토리 UI를 만들지 않기 위한 의도적 단순화입니다. 머리 위 아이콘 하나로 표시합니다.

---

## 4. 진행 루프 요약

```
[월드맵] 아르바이트 선택
   └─ 슬롯 2 + 체력 검사 → 차감 → SaveCurrentGame()
   └─ LoadingScreenController.TargetSceneToLoad = "ConvenienceStoreScene"

[편의점] Intro(3s) → Working(180s)
   ├─ 손님 스폰 루프 (§5.4 tier 테이블)
   ├─ 요미: 창고 왕복 / 매대 보충 / 계산 / 청소
   └─ 이탈·오염·빈 매대가 점수를 깎음

[정산] Settling → 임금 계산 (§9) → 선물 확정
   ├─ WorldMapManager.FinishPartTimeJob(true, jobIndex, 계산된 일급)
   ├─ SaveLoadManager.GrantItemToSave(itemId, n)  ×선물 수
   └─ SaveCurrentGame()   ← 디스크 반영 필수 (§6.4)

[결과] 패널 확인 → LoadingScene → WorldMapScene
```

---

## 5. 신설 테이블

**형식**: ScriptableObject 1개, 에셋 1개.
**경로**: `Assets/Resources/Store/ConvenienceStoreConfig.asset`
**클래스**: `FXOverdose.DatingSim.Store.StoreConfig`
**로드**: `Resources.Load<StoreConfig>("Store/ConvenienceStoreConfig")`

> `Resources` 아래에 두는 이유: 씬을 에디터 빌더가 전부 재생성하므로 인스펙터에 손으로 꽂은 참조는 매번 날아갑니다. 배달 음식 아이템(`Assets/Resources/Items/Food/`)이 이미 같은 이유로 `Resources`에 있습니다. `Assets/Data/Items/`의 에너지드링크·파르페는 `Resources` 밖이지만, 이 미니게임은 **ItemData 참조가 아니라 ID 문자열만** 쓰므로 문제되지 않습니다 (§6.3).

### 5.1 근무 기본값 (`ShiftSettings`)

| 필드 | 타입 | 기본값 | 설명 |
| --- | --- | --- | --- |
| `shiftDurationSeconds` | float | `180` | 근무 1회 실시간 길이 |
| `introSeconds` | float | `3` | 카운트다운 |
| `timeSlotCost` | int | `2` | 기존 `WorldMapManager` 고정값과 일치시킬 것 |
| `minPayRatio` | float | `0.3` | 감점이 아무리 커도 `기본급 × 이 값` 아래로는 안 내려감 |
| `interactRadius` | float | `1.4` | 요미의 방(`1.45`)과 맞춤 |
| `moveSpeed` | float | `4.2` | 요미의 방과 동일 |

### 5.2 매대 테이블 (`ShelfEntry[]`)

| 필드 | 타입 | 예시 | 설명 |
| --- | --- | --- | --- |
| `shelfId` | string | `shelf_drink` | 스테이션 식별자 |
| `displayName` | string | `음료 코너` | UI 표기 |
| `maxStock` | int | `8` | 최대 재고 |
| `initialStock` | int | `5` | 근무 시작 재고 |
| `refillHoldSeconds` | float | `3.0` | 1유닛 보충 홀드 시간 |
| `unitsPerRefill` | int | `2` | 홀드 1회 완료당 채워지는 수 |

> 판매가(`unitPrice`) 필드는 두지 않습니다. 요미의 일급은 매출과 무관합니다 (§9).

기본 5개: `shelf_drink`(음료) / `shelf_snack`(과자) / `shelf_lunch`(도시락) / `shelf_daily`(생활용품) / `shelf_ice`(아이스크림).

### 5.3 액션 테이블 (`TaskEntry[]`)

| `taskType` | `holdSeconds` | 완료 효과 |
| --- | --- | --- |
| `Checkout` | `1.2 + 0.5 × 품목수` | 손님 계산 완료(감점 회피), 선물 판정 |
| `ShelfRefill` | `3.0` | 재고 `+unitsPerRefill`, 소지 유닛 −`unitsPerRefill` |
| `Clean` | `4.0` | 오염 제거 |
| `CleanupLeftover` | `2.5` | 이탈 손님 잔여물 제거 (제거해야 계산대 재사용 가능) |
| `PickupStock` | `0`(탭) | `Carry.Stock`, `carriedStockUnits = 6` |
| `PickupTool` | `0`(탭) | `Carry.Tool` |

### 5.4 손님 스폰 tier 테이블 (`CustomerTier[]`)

tier는 `SaveData.StoreTotalShifts`(§6.2)로 선택합니다. 근무를 반복할수록 손님이 많아집니다.

| `minTotalShifts` | `spawnIntervalSeconds` | `maxConcurrent` | `patienceSeconds` | `itemsPerCustomer` |
| --- | --- | --- | --- | --- |
| `0` | `12.0` | `3` | `40` | `1~2` |
| `5` | `9.5` | `4` | `35` | `1~3` |
| `12` | `7.5` | `5` | `30` | `2~3` |
| `25` | `6.0` | `5` | `26` | `2~4` |

`spawnIntervalSeconds`에는 ±20% 난수를 곱합니다 (규칙적인 리듬은 곧 암기가 됩니다).

### 5.5 랜덤 인카운트 / 선물 테이블 (`GiftEntry[]`)

| 필드 | 타입 | 값 |
| --- | --- | --- |
| `giftChance` | float | `0.05` — 계산 **성공** 손님당 판정 |
| `itemId` | string | `energy_drink` / `dessert` |
| `weight` | int | `50` / `50` |
| `amount` | int | `1` |
| `dailyGiftCap` | int | `3` — 근무 1회 최대 선물 수 |

> `itemId` 값은 실제 에셋과 반드시 일치해야 합니다: `Assets/Data/Items/EnergyDrink.asset` → `energy_drink`, `Assets/Data/Items/Dessert.asset` → `dessert`. 오타는 조용히 아이템 증발로 이어집니다 (§6.3의 검증 항목).

### 5.6 감점 테이블 (`PenaltySettings`)

| 필드 | 기본값 | 설명 |
| --- | --- | --- |
| `walkoutPenalty` | `60` | 이탈 손님 1명당 일급 차감 |
| `dirtPenaltyPerSpot` | `25` | 근무 종료 시 남아 있는 오염 1개당 |
| `emptyShelfPenalty` | `20` | 근무 종료 시 재고 0인 매대 1개당 |
| `leftoverPenalty` | `15` | 근무 종료 시 남은 잔여물 1개당 |
| `dirtSpawnChance` | `0.15` | 손님 Browsing 종료 시 판정 |

---

## 6. 저장 기능

### 6.1 저장하지 않는 것 (명시)

- **근무 중간 상태 일체** — 남은 시간, 매대 재고, 오염 위치, 손님 배치, 홀드 진행도.
- **편의점 씬을 `SaveLoadManager.ResumableScenes`에 추가하지 않습니다.** 추가하면 재접속 시 "근무가 없는 편의점"에 떨어집니다. 근무 중 종료 = 근무 소실이며, 소모한 슬롯·체력은 돌아오지 않습니다.

> `ponytail:` 근무는 원자적. 중단 복구가 실제로 필요해지면 그때 `StoreShiftSnapshot` 직렬화 계층을 추가하십시오. 그 전까지 필드 20개를 미리 만들 이유가 없습니다.

### 6.2 `SaveData` 신설 필드 — 1개

```csharp
// SaveData.cs, DatingSim 상태 블록 하단에 추가
// --- 편의점 알바 (Phase 2) ---
// 누적 근무 횟수. 손님 스폰 tier 선택에만 쓰입니다.
// 기본값 0이라 구버전 세이브는 마이그레이션 없이 tier 0으로 안전하게 시작합니다.
public int StoreTotalShifts = 0;
```

수집·주입 지점:

- **수집**: `SaveLoadManager.SaveGame()`에는 손대지 않습니다. `StoreShiftManager`가 정산 시점에 `CurrentData.StoreTotalShifts++` 한 뒤 `SaveCurrentGame()`을 호출합니다 (부분 저장이 베이스를 재사용하므로 그대로 살아남습니다 — `SaveLoadManager.cs:107` 주석 참고).
- **주입**: 없음. 읽는 쪽이 `SaveLoadManager.Instance.CurrentData.StoreTotalShifts`를 직접 봅니다. 미러링할 매니저를 만들지 않습니다.
- **마이그레이션**: 불필요. `SaveDataMigrator`에 추가할 항목 없음.

### 6.3 선물 아이템 지급 경로 — 신설 헬퍼 1개

**문제**: `Inventory`와 `ShopManager`는 GameScene에만 있습니다. 편의점 씬에서 `Inventory.AddItem()`을 부를 대상이 없습니다.
**해법**: `SaveData`의 병렬 리스트(`InventoryItemIds` / `InventoryItemQuantities`)에 직접 씁니다. `WorldMapManager`가 잔고에 대해 이미 쓰고 있는 폴백과 같은 방식입니다.

```csharp
// SaveLoadManager.cs 에 추가
/// <summary>
/// GameScene 밖(요미의 방·월드맵·편의점)에서 아이템을 지급합니다.
/// Inventory 인스턴스가 없는 씬이라 세이브 스냅샷에 직접 누적합니다.
/// 다음 GameScene 진입 때 ApplyLoadedDataToGame이 이 목록으로 인벤토리를 재구성합니다.
///
/// ⚠️ 이 메서드는 디스크에 쓰지 않습니다. 호출부가 SaveCurrentGame()으로 확정해야 합니다.
/// </summary>
public bool GrantItemToSave(string itemId, int amount = 1)
{
    if (CurrentData == null || string.IsNullOrEmpty(itemId) || amount <= 0) return false;

    int index = CurrentData.InventoryItemIds.IndexOf(itemId);
    if (index >= 0 && index < CurrentData.InventoryItemQuantities.Count)
    {
        CurrentData.InventoryItemQuantities[index] += amount;   // 같은 ID는 합산 — 중복 행을 만들면 복원 때 덮어써집니다
        return true;
    }

    CurrentData.InventoryItemIds.Add(itemId);
    CurrentData.InventoryItemQuantities.Add(amount);
    return true;
}
```

**이 경로가 성립하는 근거 (확인 완료)**

1. `SaveGame()`의 인벤토리 수집 블록은 `if (inventory != null)`로 감싸져 있습니다 (`SaveLoadManager.cs:165`). 편의점 씬에는 `Inventory`가 없으므로 우리가 쓴 값이 지워지지 않습니다.
2. GameScene 복귀는 `YomiRoomManager.StartTrading()` → `SaveCurrentGame()` → `PrepareLoadGame()` 순서입니다. `PrepareLoadGame`이 **디스크에서 다시 읽으므로**, 편의점에서의 변경은 반드시 디스크에 반영돼 있어야 합니다.
3. `ApplyLoadedDataToGame()`이 `ShopManager.CatalogItems`에서 ID로 `ItemData`를 찾아 `Inventory.AddItem`을 호출합니다 (`SaveLoadManager.cs:552-569`). **카탈로그에 없는 ID는 조용히 버려집니다** → `energy_drink`/`dessert`가 GameScene의 `ShopManager` 카탈로그에 실제로 들어 있는지 구현 전에 인스펙터로 확인할 것.

### 6.4 정산 커밋 순서 (엄수)

```csharp
// StoreShiftManager.CommitResult()
var save = SaveLoadManager.Instance;

// 1) 일급 — 기존 경로 재사용. 편의점 씬에는 GameManager가 없으므로 내부 폴백을 탑니다.
WorldMapManager.Instance?.FinishPartTimeJob(true, jobIndex, finalPay);
// ↑ WorldMapManager는 씬 스코프라 편의점에서는 null입니다. §7.3의 static 헬퍼로 대체하거나
//   해당 폴백 로직을 그대로 옮겨 씁니다. 결정 필요 — 권장: static 헬퍼.

// 2) 선물
foreach (var g in earnedGifts) save?.GrantItemToSave(g.itemId, g.amount);

// 3) 누적 근무 횟수
if (save?.CurrentData != null) save.CurrentData.StoreTotalShifts++;

// 4) 확정 — 이 한 번만 디스크에 씁니다
save?.SaveCurrentGame();
```

**실패 케이스**: `SaveCurrentGame()`은 오버도즈 상태에서 `false`를 반환하고 저장을 거부합니다 (`SaveLoadManager.cs:100-105`). 이때 `CurrentData`의 변경은 메모리에 남아 다음 성공 저장에 딸려 갑니다 — 데이터는 잃지 않지만 **그 사이 강제 종료하면 일급과 선물이 사라집니다.** 정산 패널에 저장 실패 시 경고 한 줄을 노출합니다.

---

## 7. 파이프라인 / 신설·수정 파일

### 7.1 에디터 빌더 (필수)

이 프로젝트는 씬을 손으로 만들지 않습니다. `Assets/Editor/YomiRoomTestSceneBuilder.cs`(`FX Overdose/Build YomiRoom Test Scene`)와 동일한 골격으로:

- **`Assets/Editor/ConvenienceStoreSceneBuilder.cs`** — `[MenuItem("FX Overdose/Build Convenience Store Scene")]`
  씬 생성 → `ConvenienceStorePrototype.Build(scene)` 호출 → `Assets/Scenes/DatingSim/ConvenienceStoreScene.unity`로 저장 → **Build Settings에 등록**.
- **`Assets/Scripts/DatingSim/UI/ConvenienceStorePrototypeBuilder.cs`** — 실제 지오메트리/UI 구축. `YomiRoomTopDownPrototype`의 헬퍼(`CreateBlock`, `CreateCanvas`, `CreateText`, `GetPixelSprite`, `LoadWalkFrames`)를 그대로 본떠 씁니다. 색상 상수(Navy/Floor/Wall/Cyan/Pink)도 동일하게 유지해 톤을 맞춥니다.

### 7.2 신설 파일 (5개)

| 파일 | 역할 | 대략 규모 |
| --- | --- | --- |
| `Assets/Scripts/DatingSim/Store/StoreConfig.cs` | ScriptableObject + §5 테이블 struct 전부 | ~150줄 |
| `Assets/Scripts/DatingSim/Store/StoreShiftManager.cs` | 근무 FSM, 타이머, 손님 스폰, 점수, 정산, 커밋. **임금 계산식은 이 안의 `public static` 순수 메서드**로 둡니다 (테스트 대상, §11) | ~350줄 |
| `Assets/Scripts/DatingSim/Store/StoreStation.cs` | 계산대/매대/창고/청소 공용 홀드 타깃. 진행도 소유 | ~120줄 |
| `Assets/Scripts/DatingSim/Store/StoreCustomer.cs` | 손님 FSM + 직선 이동 | ~150줄 |
| `Assets/Scripts/DatingSim/Store/StorePlayerController.cs` | WASD 이동 + `E` 홀드 판정 + 근접 스테이션 탐색 | ~110줄 |
| `Assets/Scripts/DatingSim/Store/StoreShiftUI.cs` | 진행 바 / 남은 시간 / 손님 인내 게이지 / 결과 패널 | ~250줄 |

> 매니저는 로직만, UI는 `event` 구독만 — Phase 2 레이어링 규칙(`CLAUDE.md`)을 그대로 따릅니다. `StoreShiftManager`는 `StoreShiftUI`를 참조하지 않습니다.

이벤트 목록:

```csharp
public event Action<StoreShiftState> OnStateChanged;
public event Action<float> OnTimeRemaining;          // 초
public event Action<StoreStation, float> OnProgress; // 스테이션, 0..1
public event Action<string> OnFeedback;              // "채울 물건이 없어요."
public event Action<StoreCustomer> OnCustomerWalkout;
public event Action<StoreShiftResult> OnShiftFinished;
```

### 7.3 기존 파일 수정 (4곳, 전부 소규모)

| 파일 | 수정 |
| --- | --- |
| `Assets/Scripts/System/SaveData.cs` | `StoreTotalShifts` 필드 1개 추가 |
| `Assets/Scripts/System/SaveLoadManager.cs` | `GrantItemToSave()` 추가 (§6.3) |
| `Assets/Scripts/DatingSim/WorldMap/WorldMapManager.cs` | ① `TryStartPartTimeJob`이 즉시 보상 대신 편의점 씬으로 전환 ② 일급 지급 폴백을 `public static void PayWage(float amount)`로 분리 (편의점 씬에서 `WorldMapManager.Instance`가 null이므로) |
| `Assets/Scripts/DatingSim/YomiRoom/YomiRoomTopDownPrototype.cs` | `YomiTopDownWalkAnimator.Configure`에 `Func<Vector2> moveInputSource = null` 선택 인자 추가. null이면 기존 `YomiRoomTopDownController` 조회로 폴백 → 요미의 방 동작은 그대로 |

`WorldMapManager.TryStartPartTimeJob` 변경 후:

```csharp
// 자원 차감까지는 동일. 그 다음 즉시 보상 대신:
SaveLoadManager.Instance?.SaveCurrentGame();          // 차감 확정 + 복귀 지점은 WorldMapScene 유지
// static 2개로 씬 간 전달 (LoadingScreenController와 같은 패턴).
// WorldMapManager는 씬 스코프라 편의점 씬에서 조회할 수 없으므로 기본급을 값으로 넘깁니다.
StoreShiftManager.PendingJobIndex = jobIndex;
StoreShiftManager.PendingBasePay  = job.rewardAmount;
LoadingScreenController.TargetSceneToLoad = "ConvenienceStoreScene";
SceneManager.LoadScene("LoadingScene");
```

### 7.4 빌드/에셋 파이프라인 체크리스트

1. `FX Overdose/Build Convenience Store Scene` 실행 → 씬 생성.
2. **Build Settings 등록** — `TitleScene → LoadingScene → GameScene → tutorial` 뒤에 추가. 누락 시 `LoadingScene`이 무한 대기합니다.
3. `Assets/Resources/Store/ConvenienceStoreConfig.asset` 생성 후 §5 기본값 입력.
4. **`Tools/Prebake All Scripts Text into Font` 재실행.** 신규 한국어 문자열("근무 종료", "채울 물건이 없어요.", 매대 이름 등)이 TMP 아틀라스에 없으면 □로 렌더됩니다.
5. `LoadingScreenController`는 손댈 필요 없음 — 차트 준비 대기는 `GameScene`/`tutorial`에만 걸려 있고(`LoadingScreenController.cs:99,132`), 편의점은 그 분기를 타지 않습니다.
6. 컴파일 확인: `dotnet build "Assembly-CSharp.csproj" -v:m` + `dotnet build "Assembly-CSharp-Editor.csproj" -v:m`.

---

## 8. UI 사양

| 요소 | 위치 | 내용 |
| --- | --- | --- |
| 남은 시간 | 상단 중앙 | `MM:SS` + 얇은 게이지 |
| 현재 일급 | 상단 우측 | 기본급에서 시작해 **깎여 내려가기만 함**. 감점 발생 시 즉시 반영 + 붉게 점멸 |
| 홀드 진행 바 | **요미 머리 위** | 0~1. `E`를 뗀 상태에서도 값이 남아 있으면 회색으로 계속 표시 (재개 가능함을 알리는 유일한 신호) |
| 소지품 아이콘 | 요미 머리 위 우측 | 상자 / 대걸레 / 없음 |
| 손님 인내 게이지 | 손님 머리 위 | 초록→노랑→빨강. `WaitingAtCounter`에서만 표시 |
| 매대 재고 | 매대 위 | `5/8` 형식 텍스트, 0이면 빨강 |
| 안내 문구 | 하단 중앙 | `OnFeedback` 2.5초 표시. 요미의 방 `ShowFeedback`과 동일 규격 |
| 결과 패널 | 중앙 모달 | 등급 / 계산 성공·이탈 수 / 감점 내역 / 일급 / 받은 선물 목록 / `확인` |

진행 바는 **월드 스페이스가 아니라 캔버스에 두고 `WorldToScreenPoint`로 따라붙입니다.** 요미의 방 UI가 전부 스크린 스페이스 캔버스라 톤이 맞습니다.

---

## 9. 임금 / 등급 계산식

**요미는 시급제입니다. 매출은 요미의 주머니로 들어오지 않습니다.**
일급은 **기본급에서 시작해 감점으로 깎여 내려가기만 합니다.** 판매가·매출·인센티브 개념은 시스템 전체에서 제외합니다 (2026-08-14 확정).

```csharp
// StoreShiftManager 내부 public static. 순수 함수 — 씬도 시간도 참조하지 않습니다.
// basePay는 WorldMapManager.availableJobs[jobIndex].rewardAmount 를 그대로 받습니다.
public static StoreShiftResult Settle(in StoreShiftTally t, float basePay, StoreConfig cfg)
{
    float penalty  = t.Walkouts           * cfg.walkoutPenalty
                   + t.RemainingDirt      * cfg.dirtPenaltyPerSpot
                   + t.EmptyShelves       * cfg.emptyShelfPenalty
                   + t.RemainingLeftovers * cfg.leftoverPenalty;

    float floor = basePay * cfg.minPayRatio;                // 아무리 망해도 여기까지는 받습니다
    float pay   = Mathf.Max(floor, basePay - penalty);      // 상한은 기본급 — 초과 수익 경로 없음

    // 등급은 "들어온 손님 대비 계산해 준 손님" 비율입니다.
    // 분모에는 재고가 없어 발길을 돌린 손님도 포함됩니다 (§3.2) — 매대를 비워 두는 것이
    // 등급을 올리는 편법이 되면 안 되기 때문입니다.
    float served = t.TotalCustomers > 0 ? (float)t.CheckedOut / t.TotalCustomers : 1f;
    char grade = served >= 0.95f && t.RemainingDirt == 0 ? 'S'
               : served >= 0.85f ? 'A'
               : served >= 0.65f ? 'B'
               : served >= 0.40f ? 'C' : 'D';

    return new StoreShiftResult(pay, grade, t);
}
```

> `sales` 항이 사라지면서 계산식이 **단조 감소**가 됐습니다. 이게 실은 더 낫습니다 — 플레이어는 "얼마나 더 벌까"가 아니라 "얼마나 덜 깎일까"를 보게 되고, 초안의 *일급 삭감*이라는 압박이 정면에 옵니다. HUD의 일급 숫자는 근무 내내 **기본급에서 내려가기만 합니다**.

**선물은 등급과 무관합니다.** 계산 성공 손님마다 독립적으로 5% 판정 (`dailyGiftCap`으로 상한). 초안의 "행운 요소"라는 성격을 유지하기 위함입니다 — 실력 보상으로 만들면 아이템 파밍 루트가 됩니다.

**기본급의 출처는 기존 `PartTimeJobData.rewardAmount` 하나입니다.** `StoreConfig`에 기본급을 따로 두지 않습니다 — 같은 숫자가 두 곳에 있으면 반드시 어긋납니다. `StoreShiftManager`는 `WorldMapManager.availableJobs[jobIndex].rewardAmount`를 `PendingBasePay`로 함께 넘겨받습니다.

**월드맵의 예상 보상 표기는 지금 그대로 둡니다.** 상한이 기본급이므로 표기와 실지급의 상한이 일치합니다. 문구만 `일급 400원`에서 `일급 최대 400원`으로 바꿉니다.

---

## 10. 리스크 / 엣지 케이스

| # | 상황 | 처리 |
| --- | --- | --- |
| R1 | 근무 중 `DatingTimeManager`가 `SaveCurrentGame()`을 매 변경마다 호출 | 근무 중에는 스태미나·슬롯을 **건드리지 않습니다.** 전부 시작 시점에 차감 완료. 초당 디스크 쓰기가 발생하면 안 됩니다 |
| R2 | 계산 진행 중 손님이 인내 초과로 이탈 | 진행도 즉시 리셋 + `잔여물` 생성. "거의 다 했는데 뺏김"이 명확히 보이도록 진행 바를 빨강으로 1회 점멸 |
| R3 | 대기열 5명 초과 | 스폰 스킵 (이탈로 치지 않음). 감당 못 할 벌점이 쌓이는 것을 막습니다 |
| R4 | 오염 위치가 매대·계산대와 겹침 | 스폰 시 기존 스테이션과 반경 1.0 이내면 재추첨 3회, 실패 시 스폰 포기 |
| R5 | 요미가 창고 물건을 든 채 다른 것을 집음 | 이전 소지품 소멸 (§3.4). 확인 모달 없음 — 반복 조작에 모달은 방해입니다 |
| R6 | 오버도즈 상태에서 정산 | `SaveCurrentGame()` 거부. 결과 패널에 경고 표시, `CurrentData`에는 반영 유지 (§6.4) |
| R7 | `energy_drink`/`dessert` ID가 `ShopManager` 카탈로그에 없음 | 선물이 조용히 증발. 구현 시 에디터 테스트로 검증 (§11) |
| R8 | 근무 중 강제 종료 | 근무 소실, 슬롯·체력 미환불. 의도된 동작 (§6.1) |
| R9 | `E` 홀드 중 `Alt+Tab` | Unity가 키 상태를 유지할 수 있음 → `Application.isFocused == false`면 홀드 강제 해제 |
| R10 | 열린 포지션을 들고 알바 진입 | 이미 월드맵까지 왔다면 요미의 방 PC 가드(`EnsureNoOpenPosition`)를 통과한 것. 추가 가드 불필요 |
| R11 | **매대를 일부러 비워 손님을 쫓아냄** | 매출이 없어져 "손님을 안 받는 것"에 대가가 사라졌습니다. 재고 없음 이탈도 `walkoutPenalty` + 등급 분모 포함으로 처리 (§3.2) |
| R12 | 감점이 기본급을 초과 | `minPayRatio` 하한으로 clamp. **일급이 음수가 되어 잔고를 깎는 일은 없습니다** — `ChangeBalance`에 음수가 들어가면 알바가 손해 보는 행동이 됩니다 |

---

## 11. 검증

`dotnet test`는 이 프로젝트에서 동작하지 않고, `FXOverdose.P2P.Core` 외에는 asmdef가 없습니다. **미니게임 하나를 위해 asmdef를 새로 만들지 않습니다.** 대신 기존 패턴(`Assets/Editor/AITradingSystemTestRunner.cs`)을 따릅니다.

**`Assets/Editor/ConvenienceStoreTestRunner.cs`** — `[MenuItem("FXOverdose/Debug/Convenience Store Shift Test")]`

검사 항목 (assert 실패 시 `Debug.LogError` + 요약 출력):

1. `StoreConfig` 에셋 로드 성공, 매대 5개·tier 4개·선물 2개 존재.
2. **모든 `GiftEntry.itemId`가 GameScene `ShopManager` 카탈로그에 존재** (R7 방어).
3. `Settle()` 경계값 3종:
   - 무결점 근무 → 등급 `S`, `pay == basePay` (**기본급 초과 지급이 없는지** 확인 — 매출 제거의 회귀 방어)
   - 전원 이탈 → `pay == basePay * minPayRatio`, 음수 아님
   - 손님 0명(스폰 전 종료) → `served == 1f`로 `S`, `pay == basePay`
4. `GrantItemToSave()`를 같은 ID로 2회 호출 → 리스트 길이 1, 수량 합산.
5. tier 선택: `StoreTotalShifts` 0 / 4 / 5 / 30 → 각각 tier 0 / 0 / 1 / 3.

수동 확인: 근무 1회 완주 → 요미의 방 PC → GameScene에서 인벤토리에 선물이 실제로 늘었는지, 잔고에 일급이 들어왔는지.

---

## 12. 의도적으로 제외한 것

| 제외 항목 | 사유 | 추가할 시점 |
| --- | --- | --- |
| 근무 중간 상태 저장 | 필드 20개 + 마이그레이션 + 복원 UI. 3분짜리 세션에 과함 | 근무 길이가 10분을 넘어가면 |
| NavMesh / A* | 편의점 내부에 장애물이 사실상 없음 | 진열대가 미로가 되면 |
| 손님 오브젝트 풀링 | 동시 최대 5명, 3분에 25명 내외. `Instantiate` 비용이 문제되는 규모가 아님 | 프로파일러에 잡히면 |
| 아이템/납품 인벤토리 UI | `Carry` enum 하나로 충분 | 소지품 종류가 3개를 넘으면 |
| 별도 asmdef + EditMode 테스트 | P2P Core 외 전례 없음. 에디터 메뉴 테스트로 대체 | 계산식이 여러 시스템에 얽히면 |
| **매출 · 판매가 · 인센티브** | 요미는 시급제. 기본급 초과 수익 경로를 만들지 않습니다 (2026-08-14 확정) | 성과급 알바를 별도 직종으로 추가할 때 |
| 승진 / 시급 인상 / 상점 업그레이드 | 초안 범위 밖. tier 상승으로 난이도만 오름 | 알바가 메인 루프가 되면 |
| 요미 외 알바 캐릭터 / 다른 알바 종류 | `PartTimeJobData` 리스트가 이미 확장을 지원함 | 두 번째 알바를 실제로 만들 때 |

---

## 13. 구현 순서

| 단계 | 산출물 | 완료 기준 |
| --- | --- | --- |
| M1 | 씬 빌더 + `StorePlayerController` | 편의점을 걸어다닐 수 있음 |
| M2 | `StoreStation` + `E` 홀드 진행도 | 창고→매대 보충이 돌아가고, 손을 떼면 진행도가 유지됨 |
| M3 | `StoreCustomer` + 스폰 | 손님이 들어와 물건을 집고 계산대에 줄을 섬 |
| M4 | 계산 / 이탈 / 오염 / 잔여물 | 3분 근무가 끝까지 돌아감 |
| M5 | `Settle()` + 결과 패널 + 세이브 커밋 | 일급·선물이 GameScene에 실제로 반영됨 |
| M6 | `ConvenienceStoreTestRunner` + 밸런스 조정 | §11 전 항목 통과 |

M1~M2까지가 "재미있는지" 판정 지점입니다. **홀드 조작이 지루하면 M3 이후는 만들지 마십시오** — 손님을 아무리 늘려도 근본 조작감은 안 바뀝니다.
