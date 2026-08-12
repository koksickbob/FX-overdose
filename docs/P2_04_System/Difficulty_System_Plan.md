# 스토리 모드 난이도(EASY / NORMAL / HARD) 도입 계획

> **작성일**: 2026-08-12
> **대상 브랜치**: `Dev_koksickbob3`
> **상태**: **계획 수립 완료 / 구현 미착수**
> **적용 범위**: 트레이딩 파트의 **스토리 모드 전용**. 미연시(DatingSim) 및 무한/챌린지 모드는 영향 없음.
>
> 착수 전 확정이 필요한 항목은 [8. 미결정 사항](#8-미결정-사항) 참고.

---

## 1. 요약

스토리 모드에 `EASY` / `NORMAL` / `HARD` 난이도를 신설합니다. 난이도에 따라 달라지는 것은 **상점 가격의 기본 배율**과 **일차 경과에 따른 물가 인플레이션 상승률**입니다.

조사 결과 이 프로젝트에서 가격을 계산하는 지점은 단 3곳이며, 그중 인플레이션 공식은 **두 파일에 동일하게 복붙되어 있습니다**. 따라서 난이도 도입은 새 기능 추가인 동시에 **가격 공식 일원화 리팩토링**을 겸하게 됩니다.

`GameMode.Story = 0` 으로 구버전 세이브 호환을 잡아둔 기존 패턴을 그대로 따라 **`GameDifficulty.Normal = 0`** 으로 정의하면, 난이도 필드가 없는 기존 세이브가 자동으로 보통 난이도로 복원됩니다. 별도의 마이그레이션 코드가 필요 없습니다.

---

## 2. 시스템 현황

### 2.1. 가격 계산 지점 (전수)

| 위치 | 대상 | 현재 공식 |
|---|---|---|
| `ShopManager.GetInflatedPrice` ([ShopManager.cs:109-120](../../Assets/Scripts/Items/ShopManager.cs#L109-L120)) | 소모품 / 패시브 아이템 | `Price × 1.2^((day-1)/2)` |
| `ActiveItemEffectManager.GetNextUpgradePrice` ([ActiveItemEffectManager.cs:80-99](../../Assets/Scripts/Items/ActiveItemEffectManager.cs#L80-L99)) | 액티브 아이템 | `Price × 1.2^((day-1)/2) × PriceMultiplierPerLevel^lv` |
| `CostumeManager.Purchase` ([CostumeManager.cs:219](../../Assets/Scripts/Items/CostumeManager.cs#L219)) | 코스튬 | 정가 (인플레이션 미적용) |

> 앞의 두 곳에 `1.2^((day-1)/2)` 가 **중복 구현**되어 있습니다. 한쪽만 수정하면 액티브 아이템과 소모품의 물가가 어긋납니다.

### 2.2. 또 다른 인플레이션 축 — 정기 지출

`GameManager.CalculateExpectedDeduction` ([GameManager.cs:499-518](../../Assets/Scripts/GameManager.cs#L499-L518))이 3 / 7 / 11 / 15 / 18일차에 고정 금액을 강제 차감하고, 21일차부터는 3일마다 1.3배씩 증가합니다. 상점 가격은 아니지만 **체감 난이도의 절반 이상을 차지**하므로 난이도 배율 대상에 포함할 것을 권장합니다.

이 함수는 파산 예측([GameManager.cs:473](../../Assets/Scripts/GameManager.cs#L473))에서도 호출되므로, 함수 반환값에 배율을 걸면 예측과 실제 차감이 자동으로 일치합니다.

### 2.3. UI 표시 경로

상점 UI는 이미 `GetInflatedPrice` / `GetNextUpgradePrice` 를 경유합니다 ([ShopItemButton.cs:149-158](../../Assets/Scripts/Items/ShopItemButton.cs#L149-L158)). 따라서 **UI 코드 수정은 불필요**하며 가격 함수만 고치면 자동 반영됩니다.

### 2.4. 난이도가 미연시에 새지 않는 근거

미연시 파트의 `WorldMapManager` 는 `GameManager.ChangeBalance` / `TrySpendBalance` 를 직접 호출합니다. 가격 계산 함수를 경유하지 않으므로 구조적으로 영향을 받지 않습니다. 여기에 더해 아래 3.2의 `DifficultyManager.Current` 가드가 이중 안전장치가 됩니다.

---

## 3. 신규 타입

### 3.1. `Assets/Scripts/System/GameDifficulty.cs`

```csharp
namespace FXOverdose.Core
{
    /// <summary>
    /// 스토리 모드 전용 난이도입니다.
    /// Normal을 0으로 유지해야 Difficulty 필드가 없던 구버전 세이브도 보통으로 복원됩니다.
    /// (GameMode.Story = 0 과 동일한 호환 전략)
    /// </summary>
    public enum GameDifficulty
    {
        Normal = 0,
        Easy = 1,
        Hard = 2
    }
}
```

### 3.2. `Assets/Scripts/System/DifficultyManager.cs`

수치 테이블 · 현재 난이도 조회 · 공통 가격 계산을 한 곳에 모읍니다.

```csharp
public readonly struct DifficultySettings
{
    public readonly float ShopPriceMultiplier;        // 기본가 배율
    public readonly float InflationRatePerStep;       // 스텝당 상승률 (기존 1.2)
    public readonly int   InflationDaysPerStep;       // 스텝 간격 (기존 2)
    public readonly float RegularDeductionMultiplier; // 정기 지출 배율
}

public static class DifficultyManager
{
    /// 스토리 모드가 아니면 항상 Normal을 반환합니다.
    /// → 무한/챌린지 모드는 기존 밸런스가 수치까지 완전히 동일하게 유지됩니다.
    public static GameDifficulty Current { get; }
    public static DifficultySettings Settings { get; }

    /// 상점 공통 가격 공식.
    /// ShopManager와 ActiveItemEffectManager가 이 함수 하나를 공유합니다.
    public static int ApplyShopPricing(float basePrice, int currentDay);
}
```

**ScriptableObject 대신 static 테이블을 쓰는 이유**: 이 프로젝트는 인스펙터 참조 누락으로 이미 여러 번 문제를 겪었고(`ScenarioMatcher` 의 데이터베이스 참조 등), 정기 지출 금액도 이미 하드코딩되어 있습니다. static 상수 테이블은 씬 와이어링이 전혀 필요 없고 밸런스 조정도 한 파일에서 끝납니다.

---

## 4. 제안 수치

```
최종가 = 기본가 × ShopPriceMultiplier × InflationRate ^ ((day - 1) / DaysPerStep)
```

| | ShopPriceMul | InflationRate | DaysPerStep | 정기지출 배율 |
|---|---|---|---|---|
| **EASY** | 0.85 | 1.13 | 2 | 0.75 |
| **NORMAL** (현행과 동일) | 1.00 | 1.20 | 2 | 1.00 |
| **HARD** | 1.20 | 1.26 | 2 | 1.35 |

### 4.1. 일차별 실제 체감 배율 (기본가 대비)

| 일차 | EASY | NORMAL | HARD |
|---|---|---|---|
| 1일 | ×0.85 | ×1.00 | ×1.20 |
| 5일 | ×1.09 | ×1.44 | ×1.91 |
| 11일 | ×1.57 | ×2.49 | ×3.81 |
| 15일 | ×2.00 | ×3.58 | ×6.05 |
| 19일 | ×2.55 | ×5.16 | ×9.61 |

후반부 기준 EASY는 NORMAL의 약 0.5배, HARD는 약 1.9배입니다. 난이도 간 격차가 뚜렷하되 스토리 구간(약 20일)을 벗어나 폭주하지 않는 선입니다. NORMAL은 현행 수치와 완전히 동일하므로 기존 밸런스가 그대로 보존됩니다.

---

## 5. 저장 및 전달

`SaveLoadManager` 는 `DontDestroyOnLoad` 이므로 TitleScene → LoadingScene → tutorial → GameScene 전 구간에서 난이도가 유지됩니다.

| 파일 | 작업 |
|---|---|
| [SaveData.cs](../../Assets/Scripts/System/SaveData.cs) | `public GameDifficulty Difficulty = GameDifficulty.Normal;` 필드 추가 (`GameMode` 필드 바로 아래) |
| [SaveLoadManager.cs](../../Assets/Scripts/System/SaveLoadManager.cs) | `CurrentDifficulty` 프로퍼티 추가 / `SaveGame()` gather에 `Difficulty = CurrentDifficulty` / `PrepareLoadGame()` 의 `CurrentGameMode = Story` 강제 지점(L284-287)에서 `CurrentDifficulty = CurrentData.Difficulty` 복원 / `PrepareNewGame(GameMode, int, GameDifficulty)` 오버로드 추가 |
| [SaveDataMigrator.cs](../../Assets/Scripts/System/SaveDataMigrator.cs) | **코드 변경 없음.** `Normal = 0` 이라 enum 기본값으로 자동 처리됨을 주석으로 남겨 다음 작업자가 backfill을 중복 작성하지 않게 함 |

`PrepareNewGame` 의 기존 2인자 버전은 `Normal` 을 넘기며 신규 오버로드에 위임합니다. 따라서 **기존 호출부는 전부 무수정**입니다.

---

## 6. 게임플레이 적용

1. **`ShopManager.GetInflatedPrice`** — 본문을 `DifficultyManager.ApplyShopPricing(item.Price, gameManager.CurrentDay)` 호출로 교체.
2. **`ActiveItemEffectManager.GetNextUpgradePrice`** — `basePrice` 계산부를 같은 함수로 교체. `PriceMultiplierPerLevel^lv` 는 기존대로 곱함. → **중복 공식 제거**.
3. **`GameManager.CalculateExpectedDeduction`** — 반환 직전에 `deduction *= DifficultyManager.Settings.RegularDeductionMultiplier;` (미결정 사항 8-2 확정 후 적용).

---

## 7. 타이틀 UI

### 7.1. 흐름

```
NEW GAME → 모드 선택 → [STORY] → 슬롯 선택 → (덮어쓰기 확인)
                                      │
                                      ▼
                              ★ 난이도 선택 ★   ← 신규 단계
                                      │
                                      ▼
                              튜토리얼 확인 → PrepareNewGame(Story, slot, difficulty)
```

**이어하기 경로(`allowCreatingStorySlot == false`)는 손대지 않습니다.** 세이브에 기록된 난이도가 그대로 복원됩니다.

난이도 선택을 슬롯 선택 **뒤**에 두는 이유: `pendingSlotIndex` 가 확정된 시점이라 상태 관리가 단순하고, 사용자가 슬롯 단계에서 되돌아가도 난이도 선택이 낭비되지 않습니다.

### 7.2. 수정 대상

| 파일 | 작업 |
|---|---|
| [TitleScreenBuilder.cs](../../Assets/Scripts/UI/TitleScreenBuilder.cs) | `EnsureGameModePanel`(L267-314)을 본떠 `EnsureDifficultyPanel()` 추가. `CreateModeCard` 3장(`Btn_Easy` / `Btn_Normal` / `Btn_Hard`) + `Btn_Close` |
| [MainMenuController.cs](../../Assets/Scripts/UI/MainMenuController.cs) | `ProceedToTutorialPrompt()`(L486) 앞에 `ProceedToDifficultyPrompt()` 삽입 / 선택값을 `pendingDifficulty` 필드에 보관 / `OnClickTutorialYes`(L499)·`OnClickTutorialNo`(L520)의 `PrepareNewGame` 호출에 인자 추가 |

### 7.3. 선택 사항

세이브 슬롯 목록에 난이도를 표기(`SLOT 1 / DAY 7 / HARD`)하면 이어하기 시 혼동이 없습니다.

---

## 8. 미결정 사항

착수 전 확정이 필요합니다.

1. **코스튬 가격에도 난이도 배율을 적용할 것인가?**
   현재 코스튬은 인플레이션조차 받지 않는 순수 정가이며 업적으로 잠금이 걸려 있습니다. → **제외 권장** (치장 요소이고 게이팅이 이미 존재).

2. **정기 지출(3/7/11/15/18일차 강제 차감)을 난이도 대상에 포함할 것인가?**
   최초 요청 범위는 "상점 가격 + 인플레이션"이었으나, 체감 난이도 기여도가 큽니다. → **포함 권장**.

3. **4장의 수치 테이블이 의도한 강도인가?**
   특히 HARD 후반 ×9.61 이 과한지 검토 필요.

---

## 9. 작업 시 주의점

- **한글 폰트 프리베이크**: 한글 TMP 아틀라스는 `.cs` 소스의 문자열 리터럴에서 구워집니다. "쉬움 / 보통 / 어려움" 등 신규 한글 문자열 추가 후 `Tools/Prebake All Scripts Text into Font` 를 재실행하지 않으면 □ 로 렌더됩니다.
- **csproj 갱신**: `Assembly-CSharp.csproj` 는 gitignore 대상이며 Unity가 자동 생성합니다. 신규 `.cs` 2개가 잡히지 않으면 Unity를 다시 열어 재생성해야 `dotnet build` 검증이 가능합니다.
- **에디터 생성 UI**: 타이틀 화면은 `TitleScreenBuilder` 가 코드로 구축합니다. 씬이나 프리팹을 직접 손대면 빌더 재실행 시 덮어써집니다.

---

## 10. 검증 절차

1. `dotnet build "Assembly-CSharp.csproj" -v:m` / `dotnet build "Assembly-CSharp-Editor.csproj" -v:m`
2. 난이도별 새 게임 → 1일차 상점 가격이 각각 ×0.85 / ×1.00 / ×1.20 로 표시되는지 확인
3. 며칠 진행 후 상점 가격 및 정기 지출 금액을 4.1 표와 대조
4. 저장 → 타이틀 복귀 → 이어하기 → 난이도 유지 확인
5. **난이도 필드가 없는 기존 세이브 로드 → NORMAL 로 복원되는지 확인**
6. **무한 / 챌린지 모드 진입 → 가격이 도입 이전과 완전히 동일한지 회귀 확인**
7. 미연시(WorldMap 알바 보상 / 데이트 비용)가 난이도와 무관하게 동작하는지 확인

---

## 11. 변경 파일 요약

**신규 (2)**
- `Assets/Scripts/System/GameDifficulty.cs`
- `Assets/Scripts/System/DifficultyManager.cs`

**수정 (8)**
- `Assets/Scripts/System/SaveData.cs`
- `Assets/Scripts/System/SaveLoadManager.cs`
- `Assets/Scripts/System/SaveDataMigrator.cs` (주석만)
- `Assets/Scripts/Items/ShopManager.cs`
- `Assets/Scripts/Items/ActiveItemEffectManager.cs`
- `Assets/Scripts/GameManager.cs`
- `Assets/Scripts/UI/TitleScreenBuilder.cs`
- `Assets/Scripts/UI/MainMenuController.cs`

**문서**
- 구현 착수 시 `docs/P2_04_System/Refactored_Architecture_Master.md` 에 "상점 가격 공식 일원화" 항목 추가 기록
