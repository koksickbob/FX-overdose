# 요미의 방 일일 정산 계획 (초안 · 컨펌 대기)

**작성일**: 2026-08-15
**상태**: **구현 완료 (2026-08-15)** — 다만 **5절의 설계에서 방향을 바꿨다.** 씬 수술(GameScene에서 GameManager 제거·`StoryDatabase` 추출·인스펙터 23곳 재배선) 없이 같은 결과를 얻는 경로를 택했다. 14절 참고.
**관련**: [Calendar_DateTime_System_Plan.md](Calendar_DateTime_System_Plan.md) (완료) · [TimeSlot_Clock_Integration_Plan.md](TimeSlot_Clock_Integration_Plan.md) (컨펌 대기 — **9절의 상호작용 주의**)
**요구**: 취침 시 `GameScene`으로 넘어가 시간을 가속하지 않고, **요미의 방에서 일일 정산까지 끝낸다.**

---

## 1. 목표와 비목표

**목표**
- 취침 → 씬 전환 없이 방에서 정산 → 다음 날 아침도 방에서 시작. **씬 로드 2회 → 0회.**
- 거래를 하지 않은 날에도 위약금·정기 지출·만화 컷씬·파산 판정이 **누락 없이** 처리된다.
- 정산 로직이 **한 벌로 유지**된다. 방용 사본을 만들지 않는다.

**비목표 (13절)**
- 트레이딩 중 방으로 복귀하는 양방향 이동
- 보스전·스토리 컷씬 날의 아침 연출을 방으로 이전
- 정산 UI 디자인 변경

---

## 2. 왜 "정산 UI만 방에 붙이면 된다"가 아닌가

[DailySettlementUIController.cs:133](../../Assets/Scripts/UI/DailySettlementUIController.cs#L133)
```csharp
ResolveSystems();   // FindAnyObjectByType<GameManager>()
if (gameManager == null || gameManager.CurrentState != GameManager.GameState.Settlement) return;
```
정산 UI는 **`GameManager`를 찾아 상태를 읽고**, 표시값(총자산·일일 손익·멘탈·체력)은 `TraderStatus`에서 가져온다. 방에는 둘 다 없으므로 UI만 붙이면 아무 일도 일어나지 않는다.

## 3. 왜 "거래 안 하는 날 전용으로 새로 구현"이 안 되는가

### 3.1 취침 가능한 날에도 처리할 것이 있다

방에서 아침을 시작하는 날은 **보스도 스토리 컷씬도 없는 날**뿐이다 ([GameManager.cs:1040](../../Assets/Scripts/GameManager.cs#L1040)). 보스는 3·6·9·12·15·18·20일차, 스토리 아침은 6·16일차이므로 취침 가능일은 **1, 2, 4, 5, 7, 8, 10, 11, 13, 14, 17, 19일차**다.

> **갱신 (2026-08-15)**: 보스가 임시 비활성화되어([Boss_Penalty_Temporary_Disable.md](Boss_Penalty_Temporary_Disable.md)) 지금은 **6·16일차를 뺀 모든 날**이 방에서 아침을 시작한다. 이 계획의 적용 범위가 넓어졌다. 아래 표의 위약금은 같은 조치로 현재 0이지만, **되살릴 때를 대비해 원값 그대로 남겨 둔다** — 정기 지출(7·11일차)은 지금도 그대로 빠져나간다.

`GameScene.unity`의 실제 `storyEvents` 데이터와 `CalculateExpectedDeduction`을 대조하면:

| 일차 | 취침해도 반드시 처리돼야 하는 것 |
| --- | --- |
| 1 | 만화 4컷 |
| **5** | 위약금 **$10,000** + 만화 2컷 + 요미 대사 |
| **7** | 정기 지출 **$12,000** |
| **11** | 위약금 **$30,000** + 정기 지출 **$20,000** + 만화 2컷 + 요미 대사 |

**거래를 안 해도 돈은 나가고 컷씬은 재생된다.** 그 차감으로 파산 → 엔딩이 뜰 수도 있다.

### 3.2 일차 전환의 부작용이 8가지다

[GameManager.cs:838~](../../Assets/Scripts/GameManager.cs#L838) `FinalizeProceedToNextDay()`가 하는 일: 파산 판정 → 시각 09:00 → 날짜+1 → `StartOfDayEquity` 갱신 → 스파크라인 리셋 → 체력·멘탈 최대 회복 → 슬롯 리필(`SyncToNewDay`) → 기억 pruning → 차트 리셋 → 자동 저장.

방에 두 번째 구현을 두면 이 중 하나가 빠져도 조용히 깨지고, 나중에 지출표나 회복 규칙을 고칠 때 **한쪽만 고쳐진다.**

> **결론: 새로 구현이 아니라 호스팅을 옮긴다.** 로직은 그대로 두고, 그 로직이 방에서도 살아 있게 만든다.

---

## 4. 현행 구조 (조사 결과)

### 4.1 상주 매니저 vs 씬 스코프

| 상주 (`DontDestroyOnLoad`) | 씬 스코프 (GameScene 전용) |
| --- | --- |
| `SaveLoadManager`, `BossManager`, `TraderMemoryManager`, `AchievementManager`, `AudioManager`, `DatingTimeManager`, 업적 UI | **`GameManager`**, `TraderStatus`(GameManager 오브젝트에 부착), `TradingController`, `MarketSimulationEngine`, `AIVisualController`, `ChoiceEventController`, `TraderLevelSystem`, `DynamicTimeRegulator`, UI 전부 |

정산에 필요한 것 중 **상주 쪽에 이미 있는 것**: 보스 판정(`BossManager`), 기억 pruning(`TraderMemoryManager`), 슬롯 리필(`DatingTimeManager`), 저장(`SaveLoadManager`). 이들은 손댈 필요가 없다.

### 4.2 `GameManager`의 씬 결속

인스펙터에 직렬화된 데이터 ([GameScene.unity](../../Assets/Scenes/GameScene.unity) 881행~):
- `storyEvents` — triggerDay·위약금·요미 대사·만화 컷 (5건)
- `successEndingComic` / `bankruptcyEndingComic` / `overdoseEndingComic` — 각 3컷
- `startingBalance`, `targetBalance`, `secondsPerGameMinute`

`day6Monologue` / `day16Monologue`는 코드 내 하드코딩 리스트라 이전 대상이 아니다.

### 4.3 인스펙터 참조 23개 ★

`[SerializeField] private GameManager` / `TraderStatus`가 **20개 파일 23곳**에 있고 전부 GameScene에서 배선돼 있다 (`TradingController`, `MarketSimulationEngine`, `AITradingBrain`, `ChoiceEventController`, `HUDController`, `TopStatusBarUIController`, `TraderLevelSystem`, `ShopManager`, `MentalDrainGimmickController`, `SettingsMenuController`, `YomiSpriteController`, `AIVisualController`, `ItemUser`, `DynamicTimeRegulator`, `TraderMemoryManager` 등).

**GameScene에서 GameManager 오브젝트를 없애면 이 링크가 전부 끊긴다.** 상당수는 `FindAnyObjectByType` 폴백을 갖고 있으나(예: [TraderStatus.cs:296](../../Assets/Scripts/TraderStatus.cs#L296)) 전부는 아니다. 이번 작업 최대의 리스크 지점이다.

### 4.4 시계·드레인·저장이 모두 `GameState`에 걸려 있다

| 대상 | 게이트 |
| --- | --- |
| 시계 진행 | [GameManager.cs:270](../../Assets/Scripts/GameManager.cs#L270) `Update()`: `currentState != Playing` → 정지 |
| 체력·멘탈 감소 | [TraderStatus.cs:303](../../Assets/Scripts/TraderStatus.cs#L303) `!= Playing` → 정지 |
| **저장** | [SaveLoadManager.cs:117](../../Assets/Scripts/System/SaveLoadManager.cs#L117) `gm != null && != Playing && != Paused` → **저장 거부** |
| 잔고 변동 | [GameManager.cs:1159](../../Assets/Scripts/GameManager.cs#L1159) `Playing` 또는 `Paused`만 허용 |
| 돌발 이벤트 | [ChoiceEventController.cs:235](../../Assets/Scripts/Events/ChoiceEventController.cs#L235) `!= Playing` → 정지 |

**`Paused`가 정확히 우리가 원하는 조합이다** — 시계 정지, 드레인 정지, 이벤트 정지, 그러나 저장과 잔고 변동은 허용. 방·월드맵에 있는 동안 `GameManager`를 `Paused`로 두면 된다.

> 지금 방에서 저장이 되는 이유는 `gm == null`이라 게이트 자체를 건너뛰기 때문이다. GameManager가 상주하면 **이 게이트가 처음으로 방에서도 작동한다.** 상태를 잘못 두면 방에서 저장이 조용히 실패한다.

---

## 5. 설계

### 5.1 세 갈래

| # | 작업 | 목적 |
| --- | --- | --- |
| **A** | `GameManager` + 부착 컴포넌트를 **상주화** | 방에서도 정산 로직이 살아 있게 |
| **B** | 씬 직렬화 데이터를 **`StoryDatabase` ScriptableObject로 추출** | A의 전제 — GameScene 사본을 없애야 중복이 안 생긴다 |
| **C** | 정산·컷씬 UI를 방에도 **호스팅** | 둘 다 런타임 자가 빌드라 Canvas만 있으면 된다 |

### 5.2 A — 상주화

`GameManager`는 **`TitleScene`에서 생성**해 `DontDestroyOnLoad`로 올린다. `LLM_Manager`가 이미 같은 자리에 있다(CLAUDE.md). GameScene의 GameManager 오브젝트는 **제거**하고, 씬의 참조들은 `Awake`에서 `FindAnyObjectByType`으로 해결하도록 폴백을 보강한다(4.3).

같은 오브젝트에 붙어 함께 상주하게 되는 것: `TraderStatus`(= `CanonicalInstance`), `ChoiceEventController`, `DynamicTimeRegulator`, `TraderLevelSystem`, `CostumeManager`. 전부 `Playing` 게이트를 갖고 있어 `Paused` 상태에서는 무해하다.

**상주하지 않는 것**: `MarketSimulationEngine`, `TradingController`, `AIVisualController`, UI. 이들은 GameScene에 남는다 → 6절과 7.2의 처리가 필요하다.

### 5.3 새 상태 규약

```
GameScene 밖(방·월드맵·편의점) : GameState.Paused
GameScene 거래 중             : GameState.Playing
정산 중 (어느 씬이든)          : GameState.Settlement
```
씬 전환 시 상태를 누가 세우는가가 핵심이다 — 12절 #1.

### 5.4 취침 흐름 (변경 후)

```
방에서 "취침"
  → GameManager.currentHour = 24 (남은 시간 시뮬레이션 없음)
  → ProcessDailySettlementWithStory()      ← 기존 코드 그대로
      · 위약금 차감 / 정기 지출 / 만화 컷씬 / 요미 대사
      · OnDayEnded → 방 Canvas의 정산 UI
  → ProceedToNextDay() → FinalizeProceedToNextDay()
      · 날짜 +1, 09:00, 체력·멘탈 회복, 슬롯 리필, 기억 pruning, 자동 저장
  → currentState = Paused,  씬 전환 없이 방에 그대로
```
**씬 로드 0회.** 지금의 방→GameScene→방(2회)이 사라진다.

> 남은 시간을 시뮬레이션하지 않는 근거는 [TimeSlot_Clock_Integration_Plan.md](TimeSlot_Clock_Integration_Plan.md) 검토와 동일하다 — 방→GameScene이 단방향이라 취침 시점에 열린 포지션이 없고, 체력·멘탈은 다음 날 어차피 최대 회복되며, 캔들은 리셋된다.

---

## 6. 코드 변경 목록

| 파일 | 변경 | 규모 |
| --- | --- | --- |
| **신규** `Assets/Data/StoryDatabase.asset` + `StoryDatabase.cs` | 4.2의 씬 직렬화 데이터 이전 | 중 |
| `GameManager` | 상주화, `StoryDatabase` 참조로 전환, `RunSettlementFromRoom()` 진입점, 씬 밖 `Paused` 규약 | 중 |
| `GameScene.unity` | GameManager 오브젝트 제거, 참조 재배선 | **중~대 (리스크 최상)** |
| `TitleScene` | GameManager 프리팹 배치 | 소 |
| `YomiRoomManager.TrySleep()` | 씬 전환 대신 정산 호출 | 소 |
| 요미의 방 씬/빌더 | 정산 UI + 컷씬 UI 호스팅 | 소 |
| `SaveLoadManager` | 정산 재개 트리거 확장 (7.3) | 소 |
| `MarketSimulationEngine` 관련 | 방 정산 시 차트 무효화 (7.2) | 소 |
| 인스펙터 참조 23곳 | `FindAnyObjectByType` 폴백 보강 | 중 |
| **`SaveData`** | **신규 필드 없음** (7절) | — |

---

## 7. 저장 테이블 검토 ★

### 7.1 결론: 신규 필드는 필요 없다

검토한 후보와 기각 사유:

| 후보 | 판정 |
| --- | --- |
| `PendingSettlementDay` (정산 대기 일차) | **불필요** — `CurrentHour >= 24` + `IsSettlementProcessing`으로 판정된다 |
| `SettlementScene` (어디서 정산 중인지) | **불필요** — `LastSceneName`이 이미 있다 |
| `SettlementStage` (컷씬 재생 단계) | **불필요** — 컷씬 도중 종료 시 재시작되는 문제는 현행에도 동일하며 이번 변경으로 악화되지 않는다 |
| 상주화 표식 | **불필요** — 런타임 구조이지 세이브 상태가 아니다 |

`IsSettlementProcessing`, `TodayRegularDeduction`, `TodayRegularDeductionReason`, `LastSceneName`, `DailyEquityHistory`가 이미 있고 그대로 쓰인다.

### 7.2 그러나 — 차트 데이터가 리셋되지 않는다 ★ (신규 조치 필요)

`FinalizeProceedToNextDay()`는 차트를 이렇게 리셋한다 ([GameManager.cs:878~](../../Assets/Scripts/GameManager.cs#L878)):
```csharp
var marketEngine = FindAnyObjectByType<MarketSimulationEngine>();
if (marketEngine != null) { marketEngine.ResetEngine(...); marketEngine.OpenMarketAfterLoading(); }
```
**방에는 `MarketSimulationEngine`이 없다 → `null` → 리셋이 조용히 건너뛰어진다.** 그런데 정산 직후 자동 저장이 일어나므로, 세이브에는 **전날 캔들(`ChartHistories`)이 그대로** 남는다. 다음 날 GameScene에 들어가면 어제 차트가 복원된 채 하루가 시작된다.

→ **엔진이 없을 때는 세이브의 차트 상태를 무효화**해 다음 진입에서 프리웜되게 한다.
```csharp
// 방에서 정산하면 차트 엔진이 없어 리셋을 못 합니다.
// 다음 GameScene 진입이 새 하루의 차트를 프리웜하도록 세이브 쪽을 비웁니다.
data.ChartHistories.Clear();
data.MarketLastUpdatedDay = -1;      // RestoreSaveData가 CurrentDay로 재판정
data.MarketTotalMinutes = 0;
```
**저장 테이블 관련 실질 작업은 이것 하나다.** 필드는 그대로 두고 값을 비운다.

> `OutlookDay` / `OutlookRegime`는 `DailyMarketOutlook`이 static이라 일차가 바뀌면 `GetOrRoll`이 새로 뽑는다. 별도 조치 불필요.

### 7.3 정산 중 종료 → 재개 경로

현행 재개 트리거는 GameScene 전용이다 ([GameManager.cs:556](../../Assets/Scripts/GameManager.cs#L556)):
```csharp
if (IsGameLoaded && currentHour >= 24) ProcessDailySettlementWithStory();
```
방에서 정산 중 종료하면 `LastSceneName = "YomiRoomScene"`, `CurrentHour = 24`, `IsSettlementProcessing = true`인 세이브가 남는다. **방으로 복귀하는 경로에는 이 트리거가 없다** → 정산이 영영 재개되지 않고 24:00에 멈춘 방에 갇힌다.

→ 재개 판정을 씬 무관하게 만든다. 상주 `GameManager`가 씬 로드 완료 시 `CurrentHour >= 24 || IsSettlementProcessing`이면 정산을 재개한다.

### 7.4 `SecondsPerGameMinute = -1` 센티널의 의미 소멸

[SaveData.cs:51](../../Assets/Scripts/System/SaveData.cs#L51) 주석: *"-1 = GameManager가 없는 씬(요미의 방·월드맵)에서 저장돼 기록되지 않음"*. GameManager가 상주하면 **-1이 다시는 기록되지 않는다.** 진단 전용 필드라 동작에 영향은 없으나 주석이 거짓이 되므로 갱신한다. 구버전 세이브의 -1은 그대로 유효한 기록이다.

### 7.5 마이그레이션

**불필요.** 필드 추가·삭제·의미 변경이 없다. 7.2는 저장 시점의 처리이지 기존 세이브의 보정이 아니다.

단, 구버전 세이브 중 `LastSceneName = "GameScene"` + `CurrentHour >= 24`인 것들은 기존 GameScene 재개 경로를 그대로 타야 한다 — 7.3의 조건을 넓힐 때 이 경로를 깨뜨리지 말 것.

---

## 8. 안전장치

### I-1. GameScene의 GameManager 중복 ★
상주본이 있는데 GameScene에도 남아 있으면 `FindAnyObjectByType<GameManager>()`가 **어느 쪽을 돌려줄지 보장되지 않는다.** 잔고·시계·상태가 두 벌이 되어 증상이 무작위로 나타난다.
→ GameScene에서 오브젝트를 **제거**하는 것이 정답이며, 그러려면 5.1-B(데이터 추출)가 선행돼야 한다. 방어적으로 `Awake`에서 중복 인스턴스를 파괴하는 코드도 함께 넣는다.

### I-2. `TraderStatus.CanonicalInstance` 규약 ★
[TraderStatus.cs:162](../../Assets/Scripts/TraderStatus.cs#L162) — canonical은 **"GameManager 오브젝트에 붙은 TraderStatus"**로 정의된다. GameManager가 상주하면 canonical도 자동으로 상주하므로 규약 자체는 유지된다. 다만 GameScene에 별도 `TraderStatus`가 남아 있으면 그것이 비-canonical이 되어 매 프레임 `SyncFromCanonical()`한다 — 동작은 하지만 의도치 않은 사본이므로 정리한다.

### I-3. 방에서 저장이 조용히 실패 ★
4.4의 저장 게이트가 방에서 처음으로 작동한다. 상태가 `Playing`/`Paused`가 아니면 **`SaveCurrentGame()`이 false를 반환하고 경고만 남긴다** — 호출부 대부분이 반환값을 보지 않으므로 무증상으로 진행이 날아간다.
→ 방·월드맵 진입 시 `Paused`를 세우는 지점을 단일화하고, 정산 중(`Settlement`) 저장이 필요한 시점이 있는지 별도 확인한다.

### I-4. 방에서 시간이 흐른다
`Paused`를 세우지 못하면 `Update()`가 매 프레임 시계를 돌린다. 방에서 가만히 있어도 24:00이 되어 정산이 터진다. I-3과 같은 원인, 반대 증상.

### I-5. 인스펙터 참조 23개 단절 (4.3)
`FindAnyObjectByType` 폴백이 없는 컴포넌트는 `NullReferenceException` 또는 무동작이 된다. 폴백 유무를 **파일별로 전수 확인**해야 한다. 컴파일은 통과하므로 빌드로는 못 잡는다.

### I-6. 차트가 전날 것으로 시작 (7.2)
방 정산 시 엔진 부재로 리셋이 건너뛰어진다.

### I-7. 정산 재개 불가 (7.3)
24:00에 멈춘 방에서 빠져나오지 못한다.

### I-8. 취침 가능일의 컷씬·위약금 누락 (3.1)
5·7·11일차가 회귀 검증의 핵심 케이스다. "거래를 안 했으니 정산에 아무것도 없다"는 **거짓이다.**

### I-9. 파산 엔딩이 방에서 발생
11일차 위약금 $30,000 + 지출 $20,000으로 파산하면 `EndGame(Bankruptcy)` → `GameOverUIController`가 필요하다. **방에 게임오버 UI가 없으면 엔딩이 표시되지 않는다.** 12절 #3.

### I-10. 20일차는 방에서 오지 않는다
최종 보스날이라 아침부터 GameScene에 머문다. 성공 엔딩 경로는 이번 변경의 영향을 받지 않는다 — 다만 파산 엔딩(I-9)은 아무 날에나 올 수 있다.

### I-11. `LoadingScreenController`의 차트 대기
GameScene 로딩은 차트 시스템 준비를 기다린다(CLAUDE.md). 방으로 가는 경로는 그 대기가 없으므로, 정산 후 GameScene에 진입할 때만 해당된다 — **변경 없음.** 단 GameManager가 상주하면 로딩 화면이 기다리는 대상이 바뀌는지 확인한다.

### I-12. P2P
`EnableP2PExternalMode`가 GameManager를 P2P 모드로 세운다. **상주 GameManager가 P2P 경기 후에도 살아남아 스토리 세션을 오염시키지 않는지** 확인해야 한다. 현재는 씬 스코프라 자동으로 폐기됐다. 12절 #4.

---

## 9. 슬롯 연동 계획서와의 상호작용 ★

[TimeSlot_Clock_Integration_Plan.md](TimeSlot_Clock_Integration_Plan.md) 3.3은 슬롯 소비 시 시계를 이렇게 민다:
```csharp
var gm = FindAnyObjectByType<GameManager>();
if (gm != null) { gm.AdvanceGameMinutes(minutes); return; }   // ← 상주화되면 항상 이 경로
```
**GameManager가 상주하면 `gm != null`이 방에서도 참이 되어 `AdvanceGameMinutes`를 탄다.** 그런데 그 메서드는 [GameManager.cs:1111](../../Assets/Scripts/GameManager.cs#L1111)에서
```csharp
if (currentState != GameState.Playing || interruptFastForwardForOverdose) break;
```
로 **`Paused`면 즉시 중단**하고 `remainingFastForwardMinutes`에 분을 쌓아둔다. 결과:
1. 방에서 슬롯을 써도 **시계가 전진하지 않고**
2. 쌓인 분이 나중에 **트레이딩 시작 시점에 한꺼번에 터진다**

→ 슬롯 전진은 `AdvanceGameMinutes`가 아니라 **시뮬레이션 없이 시각만 옮기는 별도 메서드**를 써야 한다. 시장이 돌지 않는 방에서는 분당 이벤트를 발행할 이유도 없으므로 이쪽이 원래 옳다.

**두 계획서의 착수 순서에 따라 이 함정의 형태가 달라진다** — 12절 #5.

---

## 10. 검증 체크리스트

- [ ] **5일차 취침** → 위약금 $10,000 차감 + 만화 2컷 + 요미 대사 (I-8)
- [ ] **7일차 취침** → 정기 지출 $12,000 차감
- [ ] **11일차 취침** → 위약금 $30,000 + 지출 $20,000 + 컷씬, 파산 시 엔딩 표시 (I-9)
- [ ] 취침 후 씬 로드가 **0회**인지 (로그로 확인)
- [ ] 취침 후 다음 날 아침 방 상태: 슬롯 5, 09:00, 체력·멘탈 최대
- [ ] **방에서 대기해도 시계가 흐르지 않는지** (I-4)
- [ ] **방에서 저장이 되는지** — 슬롯 소비·알바·데이트 각각 (I-3)
- [ ] GameScene 진입 시 GameManager가 **하나뿐인지** (I-1)
- [ ] GameScene의 23개 인스펙터 참조가 전부 해결되는지 (I-5) ★
- [ ] **방 정산 다음 날 GameScene 차트가 새로 프리웜되는지** (I-6) ★
- [ ] 정산 중 종료 → 재시작 → 방에서 정산 재개 (I-7)
- [ ] 구버전 세이브(GameScene 24:00) 재개가 여전히 동작 (7.5)
- [ ] 보스날(3·9·12·15·18)·스토리날(6·16)은 **여전히 GameScene에서 아침 시작**
- [ ] 20일차 최종 보스 → 성공 엔딩 정상 (I-10)
- [ ] P2P 경기 후 스토리 새 게임이 오염되지 않는지 (I-12)
- [ ] `dotnet build` 4종

---

## 11. 작업 순서

1. **B — `StoryDatabase` 추출** (씬 데이터를 에셋으로. 여기서 값이 하나라도 새면 5·11일차가 조용히 죽는다)
2. **A — 상주화** + I-1 중복 방어 + 5.3 상태 규약
3. **I-5 — 인스펙터 참조 23곳 전수 점검** (2와 같은 단계. 분리하면 씬이 깨진 채로 남는다)
4. **C — 방 UI 호스팅** + `TrySleep()` 전환
5. **7.2 차트 무효화 + 7.3 정산 재개** (여기까지가 안전장치의 본체)
6. 10절 검증
7. [Refactored_Architecture_Master.md](Refactored_Architecture_Master.md)에 기록

1~3번은 **함께 끝내야 한다.** 중간에서 멈추면 GameScene이 동작하지 않는 상태로 남는다. 4~5번은 그 위에 얹는 단계라 분리 가능하다.

---

## 12. 미결 사항 (컨펌 필요)

| # | 항목 | 선택지 | 제안 |
| --- | --- | --- | --- |
| 1 | **씬 밖 `Paused` 상태를 누가 세우는가** | (a) `GameManager`가 `sceneLoaded` 이벤트로 자기 씬을 보고 스스로 (b) 각 씬 매니저(`YomiRoomManager`·`WorldMapManager`)가 명시 호출 | **(a)** — (b)는 씬이 하나 늘 때마다 누락된다. 편의점 씬이 이미 그 예다 |
| 2 | **`GameManager`를 어디서 생성** | (a) `TitleScene`에 배치 (`LLM_Manager`와 같은 자리) (b) 최초 필요 시 코드로 생성 | **(a)** — 인스펙터 배선이 남아 있는 동안은 실체가 있는 편이 안전하다 |
| 3 | **파산 엔딩 UI를 방에도 둘지** (I-9) | (a) 방에도 `GameOverUIController` 호스팅 (b) 파산 시에만 GameScene으로 전환 | **(a) 권장** — 아래 12.1 참고 |
| 4 | **P2P 후 상주 GameManager 처리** (I-12) | (a) P2P 종료 시 파괴 후 재생성 (b) 상태 초기화 | **(a)** — P2P 모드 플래그가 남으면 증상이 기괴해진다 |
| 5 | **두 계획서의 착수 순서** (9절) | (a) 슬롯 연동 먼저 → 이 작업 (b) 이 작업 먼저 → 슬롯 연동 (c) 함께 | **(b)** — 상주화가 슬롯 계획의 `gm == null` 분기를 통째로 없앤다. 슬롯을 먼저 하면 그 분기를 만들었다가 곧 지우게 된다 |
| 6 | **취침 외의 정산 경로** | 거래 중 24:00 정산은 GameScene 유지 / 그것도 방으로 | **GameScene 유지** — 거래 중이면 이미 GameScene에 있다. 옮길 이유가 없다 |

### 12.1 #3 해결안 — (a)가 생각보다 싸다

처음에는 "(b)가 구현이 싸다"고 봤으나, 이 계획의 **C 단계가 이미 방에 Canvas를 세우고 정산 UI와 컷씬 UI를 얹는다.** `GameOverUIController`도 같은 부류다 — [GameOverUIController.cs:558](../../Assets/Scripts/UI/GameOverUIController.cs#L558)에서 보듯 런타임에 자기 UI를 만들어내므로 **같은 Canvas에 컴포넌트 하나를 더 붙이는 일**이다. (b)처럼 파산 전용 씬 전환 경로를 따로 만들면 오히려 분기가 하나 더 늘고, 그 경로만 테스트에서 빠지기 쉽다.

**보스·위약금 비활성화로 파산 압력이 줄었지만 사라지지는 않았다** ([Boss_Penalty_Temporary_Disable.md](Boss_Penalty_Temporary_Disable.md) 4.3) — 정기 지출(7일차 $12,000, 11일차 $20,000)은 그대로다. "요즘 파산이 잘 안 나더라"를 근거로 (b)를 고르면, 위약금을 되살리는 순간 방에서 엔딩이 표시되지 않는 버그가 된다.

→ **(a)로 확정 제안.** C 단계에 컴포넌트 하나 추가.

### 12.2 I-5 해결안 — 23곳을 전수 점검하지 않는 방법

`[SerializeField] GameManager/TraderStatus` 23곳을 파일마다 손으로 확인하는 것은 느리고 빠뜨리기 쉽다(그리고 컴파일로는 안 잡힌다). **`GameManager`에 지연 탐색 정적 접근자를 하나 두면** 각 소비자의 폴백이 전부 같은 한 줄이 된다.

```csharp
// GameManager
private static GameManager cached;
public static GameManager Instance =>
    cached != null ? cached : (cached = FindAnyObjectByType<GameManager>(FindObjectsInactive.Include));
```
```csharp
// 각 소비자의 Awake/Start — 23곳 모두 같은 형태
if (gameManager == null) gameManager = GameManager.Instance;
```

이점:
- 폴백이 **한 가지 모양**이라 누락을 grep으로 찾을 수 있다 (`[SerializeField]`가 있는데 이 줄이 없는 파일)
- 상주화 이후에는 `FindAnyObjectByType`이 사실상 1회만 돌고 캐시된다 — 현재 곳곳의 매 프레임 탐색보다 오히려 싸다
- `TraderStatus`는 이미 `CanonicalInstance`라는 같은 성격의 접근자를 갖고 있다. 규약을 맞추는 것이지 새 패턴을 만드는 게 아니다

⚠️ 캐시는 **씬 전환·P2P 종료 시 무효화**해야 한다(I-1/I-12). `cached != null` 검사가 Unity의 파괴된 오브젝트 비교를 타므로 파괴된 인스턴스는 자동으로 걸러지지만, P2P 모드 플래그가 남은 살아 있는 인스턴스는 걸러지지 않는다.

---

## 13. 범위 밖 (YAGNI)

- **보스전·스토리 컷씬 날의 아침 연출 이전** — 그 날들은 아침부터 GameScene에 머무는 것이 설계다.
- **트레이딩 → 방 복귀** — 단방향 전제가 깨지면 "취침 시 열린 포지션 없음"(5.4)이 무너진다.
- **`MarketSimulationEngine` 상주화** — 방에서 시장을 돌릴 이유가 없다. 7.2의 세이브 무효화로 충분하다.
- **정산 UI 리디자인** — 이번은 호스팅 이전이지 UI 작업이 아니다.

---

## 14. 실제 구현 — 씬 수술을 피한 경로 (2026-08-15)

5절의 A·B·C 중 **B(StoryDatabase 추출)와 GameScene 오브젝트 제거를 하지 않았다.** 같은 결과를 훨씬 작은 위험으로 얻는 길이 있었기 때문이다.

### 14.1 상주화 방식 — 씬의 GameManager가 스스로 상주한다

TitleScene에 새 프리팹을 두고 GameScene에서 원본을 제거하는 대신, **GameScene의 GameManager가 `Awake`에서 자기 자신을 `DontDestroyOnLoad`로 올린다.**

```csharp
if (instance != null && instance != this) { Destroy(gameObject); return; }   // 재진입 시 딸려온 사본
instance = this;
DontDestroyOnLoad(gameObject);
SceneManager.sceneLoaded += HandleSceneLoaded;
```

이 방식의 이점:
- **인스펙터 데이터가 그대로 산다.** 상주본은 최초 GameScene의 그 오브젝트이므로 `storyEvents`·엔딩 만화가 이미 들어 있다 → **`StoryDatabase` 추출이 필요 없다.**
- **씬 파일을 건드리지 않는다.** GameScene의 23개 인스펙터 배선은 최초 진입에서 그대로 유효하다.
- 재진입 시 딸려오는 사본은 스스로 폐기된다. 그때 끊기는 참조는 14.2가 받아낸다.

**대가**: 상주본은 최초 GameScene 진입에서 만들어진다. 그 전(새 게임 첫날 방)에는 GameManager가 없으므로 **방 정산이 불가능하다** → `TrySleep`이 종전 경로(GameScene으로 넘어가 가속)로 자연스럽게 떨어진다. 기능이 없는 게 아니라 하루 늦게 켜지는 것이라 허용했다.

### 14.2 인스펙터 참조 — 12.2의 접근자 + 일괄 치환

`GameManager.Instance`(지연 탐색 + 캐시)를 추가하고, 코드 전역의 `FindAnyObjectByType<GameManager>(...)` **61곳을 전부 이 접근자로 치환**했다(34개 파일). 대부분의 소비자는 이미 `if (gameManager == null) gameManager = FindAnyObjectByType<...>()` 폴백을 갖고 있었으므로, 치환만으로 재진입 복구가 완성된다 — 파괴된 사본은 Unity에서 `== null`이 참이기 때문이다.

> **`Find`를 그대로 두면 안 되는 이유**: `Destroy`는 프레임 끝에 반영되므로, 사본이 `Awake`에서 자폭을 예약한 뒤에도 같은 프레임의 `Find`는 **그 사본을 돌려줄 수 있다.** 캐시된 `Instance`는 상주본을 가리키므로 이 경합이 없다.

### 14.3 Start가 두 번 돌지 않는 문제

상주 오브젝트는 재진입해도 `Start`가 다시 호출되지 않는다. 그런데 세이브 복원(`ApplyLoadedDataToGame`)이 `Start`에 들어 있었다 — **그대로 뒀다면 방에서 거래를 개시할 때 복원이 통째로 누락된다.**

`Start`의 본문을 `InitializeForTradingScene()`으로 뽑아, 최초에는 `Start`가, 재진입에서는 `HandleSceneLoaded`가 부르게 했다.

### 14.4 씬 밖 상태 — 5.3 규약 적용

`HandleSceneLoaded`가 거래 씬(`GameScene`/`tutorial`/`SampleScene`) 밖에서는 상태를 **`Paused`** 로 세운다. 시계·체력 감소·돌발 이벤트가 멎고, 저장과 잔고 변동은 계속 허용된다(I-3/I-4).

### 14.5 P2P (I-12)

`EnableP2PExternalMode`가 호출되면 오브젝트를 **현재 씬으로 되돌려** 상주를 해제한다. 경기가 끝나면 씬과 함께 폐기되므로 `p2pExternalMode` 플래그가 스토리 세션으로 새지 않는다.

### 14.6 방의 UI 호스팅 — `RoomSettlementUIBootstrap`

`BossBattleUIBootstrap`과 같은 패턴으로 [RoomSettlementUIBootstrap.cs](../../Assets/Scripts/UI/RoomSettlementUIBootstrap.cs)를 만들어, `YomiRoomScene`이 로드되면 루트 Canvas에 `DailySettlementUIController`와 `GameOverUIController`를 붙인다. 둘 다 런타임 자가 빌드라 **씬 편집이 필요 없다.**

만화 컷씬은 `ComicCutsceneController.GetOrCreateRuntime(scene)`으로 없으면 만들어 쓴다.

> 등록 순서가 중요하다. 부트스트랩은 `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)`에서 `sceneLoaded`를 구독하고, GameManager는 자기 `Awake`에서 구독한다. 따라서 **부트스트랩 핸들러가 먼저 돈다** — 정산 재개(7.3)가 UI 설치보다 앞서 실행돼 `OnDayEnded`를 놓치는 일이 없다.

### 14.7 12절 결정 반영

| # | 확정 |
| --- | --- |
| 1 | (a) `GameManager`가 `sceneLoaded`로 스스로 판단 — 14.4 |
| 2 | **변경** — TitleScene 배치가 아니라 GameScene 원본의 자기 상주 (14.1) |
| 3 | (a) 방에 `GameOverUIController` 호스팅 — 14.6 |
| 4 | (a) P2P 시 상주 해제 — 14.5 |
| 5 | **무의미해짐** — 상주가 지연되므로 슬롯 계획의 `gm == null` 분기가 계속 유효하다. 두 계획의 순서 제약이 사라져 슬롯을 먼저 구현했다 |
| 6 | 거래 중 24:00 정산은 GameScene 유지 |

### 14.8 이 방식으로 미뤄진 것

- **`StoryDatabase` 추출** — 필요가 사라졌다. 다만 [Boss_Penalty_Temporary_Disable.md](Boss_Penalty_Temporary_Disable.md) 5.1의 16일차 독백 데이터화는 여전히 그 작업을 기다린다.
- **GameScene에서 GameManager 오브젝트 제거** — 하지 않았다. 재진입 시 사본이 생겼다가 자폭하는 비용(프레임당 1회)이 남는다.
- **`RoomSettlementEnabled = false`로 되돌릴 수 있다.** 방 정산이 문제를 일으키면 이 한 줄로 종전 동작(GameScene 가속)으로 복귀한다. 상주화 자체는 유지되지만 슬롯 시계 경로가 그것에 기대지 않으므로 안전하다.
