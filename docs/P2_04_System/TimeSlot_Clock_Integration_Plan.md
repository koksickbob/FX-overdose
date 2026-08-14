# 시간 슬롯 ↔ 게임 시계 연동 계획 (초안 · 컨펌 대기)

**작성일**: 2026-08-15
**상태**: **구현 완료 (2026-08-15)** — 11절 제안대로 확정. 남은 것은 9절 수기 검증뿐이다.
**선행**: [Calendar_DateTime_System_Plan.md](Calendar_DateTime_System_Plan.md) (구현 완료). 이 문서는 그 후속이다.
**요구**: 데이팅 파트의 시간 슬롯 5개가 **각 3시간**을 소모하고, 소모된 만큼 게임 시계가 실제로 흘러야 한다. 그 결과가 트레이딩 시작 시각에 반영되어야 하며, 돌발 이벤트 발생 시각과 장 개장도 그에 맞춰 조정한다.

---

## 1. 목표와 비목표

**목표**
- 슬롯 1개 = 게임 내 **3시간**. 5슬롯 × 3시간 = 15시간 = **09:00 ~ 24:00** (하루 전체와 정확히 일치).
- 방·월드맵에서 슬롯을 쓰면 게임 시계가 그만큼 전진한다.
- 트레이딩 시작 시각이 남은 슬롯을 반영한다. (예: 2슬롯 소모 후 거래 개시 → **15:00 시작**)
- 돌발 선택 이벤트 스케줄이 늦은 시작에서도 깨지지 않는다.
- 슬롯과 시계가 **어긋날 수 없는 구조**로 만든다.

**비목표 (12절 참고)**
- 장 운영 시간(개장/폐장) 도입
- 트레이딩 중 방으로 복귀하는 양방향 이동
- 슬롯 개수·비용 밸런싱 변경
- 자유 채팅의 슬롯 소모 (현재 0, `TODO(P2)`로 의도된 상태)

---

## 2. 현행 구조 (조사 결과)

### 2.1 슬롯은 시계와 아무 관계가 없다

[DatingTimeManager.cs:112](../../Assets/Scripts/DatingSim/Core/DatingTimeManager.cs#L112)
```csharp
public bool TryConsumeTimeSlot(int cost)
{
    if (currentTimeSlot < cost) return false;
    currentTimeSlot -= cost;
    OnTimeSlotChanged?.Invoke(currentTimeSlot);
    SaveLoadManager.Instance?.SaveCurrentGame();
    return true;
}
```
**단순 카운터다.** 시계(`GameManager.currentHour/currentMinute`)를 건드리지 않는다. 슬롯을 5개 다 써도 트레이딩은 09:00에 시작한다.

### 2.2 슬롯 소비 지점 (전 3곳)

| 위치 | 행동 | 비용 | 현 시간 환산 |
| --- | --- | --- | --- |
| [YomiRoomManager.cs:422](../../Assets/Scripts/DatingSim/YomiRoom/YomiRoomManager.cs#L422) | 잠깐 휴식 | `restTimeSlotCost = 1` | 3시간 |
| [WorldMapManager.cs:84](../../Assets/Scripts/DatingSim/WorldMap/WorldMapManager.cs#L84) | 편의점 알바 | `2` 고정 | 6시간 |
| [WorldMapManager.cs:160](../../Assets/Scripts/DatingSim/WorldMap/WorldMapManager.cs#L160) | 데이트 코스 | `course.timeSlotCost` (현재 1) | 3시간 |

자유 채팅은 슬롯을 **소모하지 않는다** — 플레이스홀더 응답을 반복 수확하지 못하게 한 의도적 조치다.

슬롯 리필은 [DatingTimeManager.cs:188](../../Assets/Scripts/DatingSim/Core/DatingTimeManager.cs#L188) `SyncToNewDay(day, defaultTimeSlots: 5)` 한 곳뿐이고, 이는 [GameManager.cs:932](../../Assets/Scripts/GameManager.cs#L932) 일차 전환에서만 호출된다.

### 2.3 방 → 트레이딩 경로

[YomiRoomManager.cs:472](../../Assets/Scripts/DatingSim/YomiRoom/YomiRoomManager.cs#L472) `StartTrading()`은 저장 → 복원 예약 → `GameScene` 로드만 한다. 시계는 세이브에서 복원되며, 일차 전환 시 [GameManager.cs:844](../../Assets/Scripts/GameManager.cs#L844)에서 09:00으로 리셋된 값이 그대로 들어온다.

> **방·월드맵 씬에는 `GameManager`가 없다.** 따라서 슬롯 소비 시점에 `AdvanceGameMinutes()`를 부를 수 없다. 이것이 설계의 핵심 제약이다.

### 2.4 이미 있는 선례 — 취침

[YomiRoomManager.cs:442](../../Assets/Scripts/DatingSim/YomiRoom/YomiRoomManager.cs#L442) `TrySleep()`은 **static 요청 플래그**를 세우고 GameScene이 소비하는 방식으로 방↔시계를 연결한다.
```csharp
GameManager.PendingSleepThroughToday = true;   // 방에서 요청
// → GameScene 개장 시 ConsumePendingSleepRequest()가 남은 시간을 고속 소모
```
`GameManager`가 없는 씬에서 시계에 영향을 주는 **검증된 패턴**이며, 잔고에도 같은 패턴이 있다 ([WorldMapManager.PayWage](../../Assets/Scripts/DatingSim/WorldMap/WorldMapManager.cs#L103) — `gm`이 없으면 `CurrentData.Balance`에 직접 기입).

### 2.5 돌발 이벤트 스케줄

[ChoiceEventController.cs:189](../../Assets/Scripts/Events/ChoiceEventController.cs#L189)
- `ResetDailySchedule(day, currentDayMinutes)` — 첫 이벤트를 `[max(10:00, 현재), min(현재+6h, 16:00)]`에서 뽑는다. 상·하한이 뒤집히면 `현재+60분`으로 폴백.
- **호출 조건은 `day != lastTriggerDay` 뿐이다** ([:273](../../Assets/Scripts/Events/ChoiceEventController.cs#L273)). 같은 날 안에서 시계가 점프해도 재스케줄되지 않는다.
- `ScheduleNextRandomTrigger` 상한은 23:20, 하루 **2회** 한도.
- 예정 시각은 세이브에 남는다 (`EventNextRandomTriggerMinuteOfDay`).

### 2.6 장 개장

[MarketSimulationEngine.cs:165](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L165) `OpenMarketAfterLoading()`은 `IsMarketOpen = true` 한 줄이다. **시간 개념이 없다.** 씬이 로드되면 몇 시든 개장한다.

---

## 3. 설계 결정

### 3.1 대안 비교

| | A. 파생식 (슬롯에서 시각 계산) | **B. 슬롯 소비가 시계를 전진 (채택)** | C. 시계가 슬롯을 결정 |
| --- | --- | --- | --- |
| 진실의 원천 | 슬롯 | **시계** (슬롯은 잔여 표시) | 시계 |
| 방에서 저장된 시각 | 항상 09:00 (거짓) | **실제 시각** | 실제 시각 |
| 어긋남 가능성 | 두 값이 공존 → 상시 위험 | 기입이 한 곳 → 낮음 | 슬롯 UI 재작성 필요 |
| 트레이딩 시작 반영 | 진입 시점 1회 환산 필요 | **세이브 복원만으로 자동** | 자동 |
| 코드 변경량 | 중 | **소** | 대 |

**B 채택.** 슬롯을 쓰는 순간 시계를 밀어두면, 트레이딩 시작 시각은 **기존 `RestoreClock` 경로를 타고 저절로** 맞는다. 새 저장 필드도, 진입 시점 환산 코드도 필요 없다.

### 3.2 매핑

```
슬롯 1개 = 180분 (3시간)
하루 시작 09:00 = 540분,  하루 종료 24:00 = 1440분
현재 시각(분) = 540 + (5 - 남은슬롯) × 180
```

| 남은 슬롯 | 시각 | 의미 |
| --- | --- | --- |
| 5 | 09:00 | 아침, 아무것도 안 함 |
| 4 | 12:00 | 1슬롯 소모 |
| 3 | 15:00 | 휴식 + 데이트 |
| 2 | 18:00 | 알바(2슬롯) |
| 1 | 21:00 | |
| 0 | 24:00 | **거래 시간이 0** — 5절 참고 |

**이 등식은 방·월드맵에 있는 동안만 성립한다.** 트레이딩이 시작되면 시계가 자유롭게 흐르고 슬롯은 그날 더 쓰이지 않으므로, 등식은 깨지는 게 정상이다. 검증 지점을 여기에 맞춰야 한다 (8절 H-1).

### 3.3 기입 지점은 `TryConsumeTimeSlot` 하나

슬롯을 깎는 유일한 메서드가 시계도 함께 민다. 소비 지점 3곳을 각각 고치면 네 번째 소비 지점이 생길 때 조용히 누락된다.

```csharp
public bool TryConsumeTimeSlot(int cost)
{
    if (currentTimeSlot < cost) return false;
    currentTimeSlot -= cost;
    AdvanceClockBySlots(cost);       // ← 추가
    OnTimeSlotChanged?.Invoke(currentTimeSlot);
    SaveLoadManager.Instance?.SaveCurrentGame();   // 기입 후 저장 (순서 중요)
    return true;
}
```

`AdvanceClockBySlots`는 `GameManager` 유무로 갈린다 — `PayWage`와 같은 이분기다.
```csharp
private void AdvanceClockBySlots(int slots)
{
    int minutes = slots * MinutesPerTimeSlot;   // 180
    var gm = FindAnyObjectByType<GameManager>();
    if (gm != null) { gm.AdvanceGameMinutes(minutes); return; }   // GameScene: 정식 경로
    // 방·월드맵: 세이브 스냅샷에 직접 기입. 확정은 호출부의 SaveCurrentGame이 한다.
    var data = SaveLoadManager.Instance?.CurrentData;
    if (data == null) return;
    int total = Mathf.Min(data.CurrentHour * 60 + data.CurrentMinute + minutes, 24 * 60);
    data.CurrentHour = total / 60;
    data.CurrentMinute = total % 60;
}
```

---

## 4. 트레이딩 시작 시각 반영

**추가 코드가 필요 없다.** `StartTrading()` → `SaveCurrentGame()` → `PrepareLoadGame()` → GameScene의 `RestoreClock(...)`이 이미 세이브의 시각을 되돌린다. 슬롯 소비가 그 시각을 이미 밀어놨으므로 15:00에 시작한다.

**단, 아래 셋은 그냥 따라오지 않는다.**

### 4.1 돌발 이벤트 스케줄 무효화 ★

방에서 09:00에 저장될 때 `EventNextRandomTriggerMinuteOfDay`가 예컨대 11:20으로 이미 잡혀 있다. 슬롯 4개를 쓰면 시계는 21:00이 되는데, 트레이딩 시작 시 `day == lastTriggerDay`라 **`ResetDailySchedule`이 호출되지 않는다.** 그러면 `currentDayMinutes(1260) >= nextRandomTriggerMinuteOfDay(680)`이 즉시 참이 되어 **거래 개시 첫 분에 팝업이 터진다.**

→ 시계가 앞으로 점프하면 스케줄을 다시 잡아야 한다. 판정 기준을 `day != lastTriggerDay`에서 **`day != lastTriggerDay || 예정 시각이 이미 지났음`** 으로 넓힌다.

### 4.2 하루 2회 예산이 남은 시간에 안 맞는다

21:00 시작이면 남은 시간은 3시간뿐인데 한도는 여전히 2회다. 최소 쿨다운이 60분이라 물리적으로는 가능하나, 3시간에 2번은 과밀하다. 반대로 `ScheduleNextRandomTrigger`의 23:20 상한 때문에 두 번째가 영영 안 잡히는 구간도 생긴다.

→ **남은 시간에 비례해 한도를 정한다** (11절 #3에서 확정).

### 4.3 장 개장

현행은 몇 시든 개장하므로 늦은 시작에서도 동작은 한다. 다만 "3시간짜리 장"이 그날의 차트가 된다. 캔들 엔진은 `ResetEngine` + 프리웜을 일차 전환에서 이미 마쳤으므로 기술적 문제는 없다.

→ 개장 시간대 제한을 넣을지는 **밸런스 판단**이다 (11절 #4).

---

## 5. 슬롯을 전부 소진한 경우

5슬롯을 다 쓰면 시각이 24:00이 되어 **거래 시간이 0**이다.

**기존 코드가 이미 이 경우를 받아준다.** [GameManager.cs:556](../../Assets/Scripts/GameManager.cs#L556)
```csharp
if (IsGameLoaded && currentHour >= 24) ProcessDailySettlementWithStory();
```
`StartTrading()`이 `PrepareLoadGame()`을 거치므로 `IsGameLoaded == true`이고, 거래 없이 곧장 일일 정산으로 넘어간다. **의도한 것은 아니지만 올바른 동작이다.**

다만 플레이어에게는 "거래를 시작했는데 아무것도 못 하고 정산 화면"이 된다. 방에서 미리 막을지, 그대로 정산으로 보낼지는 11절 #1에서 확정한다.

---

## 6. 코드 변경 목록

| 파일 | 변경 | 규모 |
| --- | --- | --- |
| [DatingTimeManager.cs](../../Assets/Scripts/DatingSim/Core/DatingTimeManager.cs) | `MinutesPerTimeSlot = 180` 상수, `AdvanceClockBySlots()`, `TryConsumeTimeSlot`에 연결 | 소 |
| [DatingTimeManager.cs](../../Assets/Scripts/DatingSim/Core/DatingTimeManager.cs) | `SyncToNewDay`에 슬롯·시계 정합 주석 (일차 전환은 양쪽을 동시에 리셋) | 소 |
| [ChoiceEventController.cs](../../Assets/Scripts/Events/ChoiceEventController.cs) | 시계 점프 감지 후 재스케줄(4.1), 잔여 시간 기반 한도(4.2) | 중 |
| [YomiRoomManager.cs](../../Assets/Scripts/DatingSim/YomiRoom/YomiRoomManager.cs) | `StartTrading()`에 잔여 슬롯 0 가드 (11절 #1 결정에 따름) | 소 |
| [SaveDataMigrator.cs](../../Assets/Scripts/System/SaveDataMigrator.cs) | 1.8.0 블록 — 슬롯/시계 정합 백필 (7절) | 소 |
| 데이팅 UI 2종 | 슬롯 pip 옆에 현재 시각 표기 (11절 #5) | 소 |
| **`SaveData`** | **변경 없음** — 7절 참고 | — |

---

## 7. 저장 테이블

### 7.1 새 필드는 필요 없다

시계(`CurrentHour`/`CurrentMinute`)와 슬롯(`DatingTimeSlot`)이 **둘 다 이미 저장된다.** 이번 변경은 둘 사이에 관계를 부여할 뿐 새 상태를 만들지 않는다. 슬롯당 분(180)은 설계 상수이므로 세이브에 넣지 않는다 — 넣으면 밸런싱으로 값을 바꿔도 기존 세이브가 옛 값에 고착된다 (`SecondsPerGameMinute`가 같은 이유로 기록 전용이다).

### 7.2 새로 생기는 계약

> **방·월드맵에 있는 동안: `CurrentHour × 60 + CurrentMinute == 540 + (5 - DatingTimeSlot) × 180`**

기존 계약 `DatingDay == CurrentDay`([DatingTimeManager.cs:181](../../Assets/Scripts/DatingSim/Core/DatingTimeManager.cs#L181))와 같은 급의 불변식이며, 같은 자리에 문서화한다.

### 7.3 마이그레이션 (1.8.0) ★

**구버전 세이브는 이 계약을 위반한 채로 존재한다.** 슬롯을 3개 쓰고 방에서 저장한 세이브는 `DatingTimeSlot = 2`, `CurrentHour = 9`다. 그대로 로드하면 시계가 6시간 뒤로 밀린 채 트레이딩이 시작된다 — 플레이어에게 **유리한** 방향이라 게임이 깨지진 않지만 계약이 처음부터 어긋난다.

```csharp
if (CompareVersions(dataVersion, "1.8.0") < 0)
{
    // 슬롯 소비가 시계를 밀지 않던 시절의 세이브를 계약에 맞춥니다.
    // 트레이딩 중 저장된 세이브(= 시계가 이미 09:00을 넘어 자유롭게 흐른 상태)를
    // 되돌리면 안 되므로, 아직 09:00인 세이브 = 방/월드맵 세이브만 보정합니다.
    if (data.CurrentHour == 9 && data.CurrentMinute == 0 && data.DatingTimeSlot < 5)
    {
        int minutes = 540 + (5 - Mathf.Clamp(data.DatingTimeSlot, 0, 5)) * 180;
        data.CurrentHour = minutes / 60;
        data.CurrentMinute = minutes % 60;
    }
}
```
**보정 대상을 09:00 정각으로 한정하는 것이 핵심이다.** 조건 없이 슬롯으로 시계를 덮으면 트레이딩 도중(예: 14:30)에 저장한 세이브가 슬롯 기준으로 되돌아가 진행이 통째로 날아간다.

---

## 8. 안전장치

### H-1. 슬롯과 시계의 이중 진실
7.2의 계약이 **방·월드맵에서만** 성립한다는 점이 함정이다. 트레이딩 중에는 시계만 흐르므로 등식이 깨지는 게 정상이며, 이를 검증 코드에 넣으면 오탐이 쏟아진다.
→ 검증은 **`LastSceneName`이 방/월드맵인 세이브에 한해서만** 수행한다.

### H-2. 구버전 세이브 (7.3)
09:00 정각 한정 보정. 조건을 넓히면 진행 중인 세이브를 파괴한다.

### H-3. 돌발 이벤트가 거래 개시 즉시 터짐 (4.1) ★
`day != lastTriggerDay`만으로는 같은 날 안의 시계 점프를 못 잡는다. 재스케줄 조건을 넓히지 않으면 **늦게 시작할수록 첫 분에 팝업**이 뜬다.

### H-4. 이벤트 한도가 남은 시간을 초과 (4.2)
`ScheduleNextRandomTrigger`의 23:20 상한과 60분 쿨다운 때문에, 늦은 시작에서는 두 번째 이벤트가 예약만 되고 발생하지 않는 구간이 생긴다. 한도를 잔여 시간에 맞춰 줄이면 "발생 안 함"이 정상 상태로 바뀐다.

### H-5. 24:00 도달 시 클램프
`AdvanceClockBySlots`가 `min(..., 1440)`으로 자른다. 슬롯 검사(`currentTimeSlot < cost`)가 먼저 막아주지만, 슬롯과 시계가 어긋난 세이브가 들어왔을 때 25:00 같은 값이 생기면 `currentHour >= 24` 판정과 정산 경로가 동시에 이상해진다.

### H-6. 차감 후 씬 로드 실패
[WorldMapManager.cs:75](../../Assets/Scripts/DatingSim/WorldMap/WorldMapManager.cs#L75)가 이미 `CanStreamedLevelBeLoaded`로 **차감 전에** 검사한다. 시계 전진도 같은 차감 안에서 일어나므로 이 방어가 그대로 적용된다 — **`TryConsumeTimeSlot` 호출 순서를 앞당기지 말 것.**

### H-7. `DatingTimeManager`는 `DontDestroyOnLoad`
GameScene에도 살아 있다. 거기서 슬롯이 소비되면 세이브에 직접 기입하는 경로를 타면 안 된다 — `GameManager`의 시계와 어긋난다. 3.3의 `gm != null` 분기가 이를 막는다.

### H-8. 기입과 저장의 순서
`TryConsumeTimeSlot`이 이미 `SaveCurrentGame()`을 호출한다. 시계 기입은 **반드시 그 전에** 와야 한다. 뒤에 두면 한 박자 늦은 값이 저장되고, 그 사이에 씬이 전환되면 영영 반영되지 않는다.

### H-9. 취침과의 정합
`TrySleep()`은 남은 시간을 24:00까지 스킵한다. 슬롯이 남아 있어도 성립하며, 슬롯은 다음 날 `SyncToNewDay`가 5로 리필한다. 충돌 없음 — 다만 **취침 시 남은 슬롯을 0으로 만들지는 않는다**(같은 날 다시 방으로 돌아올 수 없으므로 무해).

### H-10. 튜토리얼
`tutorial` 씬은 데이팅 파트를 거치지 않고 `AdvanceGameMinutes(150)`을 직접 쓴다. 영향 없음. 단 `TutorialManager`가 `DatingTimeManager`를 건드리지 않는지 착수 시 재확인.

### H-11. P2P
`DatingTimeManager`를 쓰지 않고 `TotalMinutes`(0~1440)만 다룬다. **영향 없음.** `FXOverdose.P2P.Core` 무수정.

---

## 9. 검증 체크리스트

- [ ] 슬롯 0개 소모 후 거래 → 09:00 시작
- [ ] 휴식 1회(1슬롯) 후 거래 → 12:00 시작
- [ ] 알바(2슬롯) 후 거래 → 15:00 시작
- [ ] 알바 + 데이트 + 휴식(4슬롯) 후 거래 → 21:00 시작
- [ ] **5슬롯 전소 후 거래 진입** → 11절 #1의 확정 동작대로
- [ ] 방에서 슬롯 소비 → 저장 → 재로드 → 시각이 유지되는지 (기입/저장 순서, H-8)
- [ ] **늦은 시작에서 거래 첫 분에 돌발 이벤트가 터지지 않는지 (H-3) ★**
- [ ] 21:00 시작에서 돌발 이벤트 한도가 잔여 시간에 맞는지 (H-4)
- [ ] 24:00을 넘는 시각이 만들어지지 않는지 (H-5)
- [ ] 알바 씬 로드 실패 시 슬롯·시계 **둘 다** 보존되는지 (H-6)
- [ ] 다음 날 전환 → 슬롯 5 + 시계 09:00 동시 리셋
- [ ] 취침 → 24:00 정산 정상 (H-9)
- [ ] **구버전 세이브**(슬롯 소비 + 09:00) 로드 → 시각 보정 (H-2)
- [ ] **트레이딩 중 저장된 구버전 세이브**(예: 14:30) 로드 → **보정되지 않고 유지** (H-2 역방향) ★
- [ ] 정산 화면·상단바의 날짜/시각 표기 정상 (달력 개편분 회귀)
- [ ] `dotnet build` 4종

에디터 메뉴 `FXOverdose/Debug/Calendar System Test`에 슬롯↔시각 환산과 1.8.0 마이그레이션 케이스를 **추가**한다. 순수 로직이라 씬 없이 검증 가능하다.

---

## 10. 작업 순서

1. `DatingTimeManager` — 상수·`AdvanceClockBySlots`·`TryConsumeTimeSlot` 연결 (여기까지가 기능의 본체)
2. `SaveDataMigrator` 1.8.0 + 검증기 케이스 추가 (**1과 같은 단계에서. 나중이 없다**)
3. `ChoiceEventController` — H-3 재스케줄, H-4 한도
4. 슬롯 0 가드 + UI 시각 표기 (11절 #1/#5 결정에 따름)
5. 9절 검증
6. [Refactored_Architecture_Master.md](Refactored_Architecture_Master.md)에 기록

1~2번까지면 요구사항의 본체(슬롯 → 시계 → 트레이딩 시작 시각)가 완성된다. 3번은 그로 인해 깨지는 것을 고치는 단계라 **같은 릴리스에 반드시 포함**해야 한다.

---

## 11. 미결 사항 (컨펌 필요)

| # | 항목 | 선택지 | 제안 |
| --- | --- | --- | --- |
| 1 | **5슬롯 전소 후 거래 진입** | (a) 방에서 "거래 시작" 버튼 비활성 + 안내 (b) 그대로 진입시켜 즉시 정산 (c) 5번째 슬롯 소비 자체를 막음 | **(b)** — 기존 `IsGameLoaded && hour >= 24` 경로가 이미 정산으로 보낸다. 코드 0줄. 다만 안내 문구는 필요 |
| 2 | **마지막 슬롯 잔여 3시간 거래** | 그대로 / 최소 거래 시간 보장 | **그대로** — 슬롯을 쓸수록 거래 시간이 줄어드는 것이 이 시스템의 트레이드오프 |
| 3 | **돌발 이벤트 한도** | (a) 항상 2회 (b) 잔여 6시간 미만이면 1회, 3시간 미만이면 0회 (c) 잔여시간 ÷ 6시간 비례 | **(b)** — 규칙이 단순하고 로그로 확인하기 쉽다 |
| 4 | **장 개장 시간** | (a) 현행 유지(항상 개장) (b) 특정 시각 이후 개장 불가 | **(a)** — 개장 제한은 #1과 기능이 겹치고, 늦은 시작 자체가 이미 페널티다 |
| 5 | **방/월드맵 UI에 현재 시각 표기** | 표기 / 슬롯 pip만 유지 | **표기** — 슬롯이 시간이 된 이상 `3/5`만으로는 "몇 시인지"를 알 수 없다. `15:00` 병기 |
| 6 | **슬롯당 3시간 = 확정?** | 3시간 고정 / 행동별 가변 | **3시간 고정** — 5×3=15시간이 09:00~24:00과 정확히 맞아떨어진다 |
| 7 | 자유 채팅 슬롯 소모 | 0 유지 / 소모 도입 | **0 유지** — 대화 시스템 교체 전까지는 현행 `TODO(P2)` 그대로 |

미결 사항 결정 내용이야.


---

## 12. 범위 밖 (YAGNI)

- **장 운영 시간(개장/폐장) 시스템** — #4에서 현행 유지로 제안. 도입하면 캔들 엔진의 분 진행과 정산 시각까지 재설계해야 한다.
- **트레이딩 → 방 복귀** — 현재 단방향이다. 양방향이 되면 3.2의 등식과 슬롯 소비 시점 판정이 전부 흔들린다.
- **행동별 가변 소요 시간** (알바 4시간, 데이트 2시간 등) — 슬롯 단위를 깨면 UI(5칸 pip)와 잔여 계산이 함께 바뀐다. 필요해지면 그때 슬롯 자체를 분 단위로 대체하는 편이 낫다.
- **슬롯·비용 밸런싱 변경** — 이번 작업은 배선이지 밸런싱이 아니다.
