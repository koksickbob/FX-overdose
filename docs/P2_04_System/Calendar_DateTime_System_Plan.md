# 달력 날짜 시스템 전환 계획 (초안)

**작성일**: 2026-08-15
**상태**: **구현 완료 (2026-08-15)** — 8절 1~4단계 전부. 남은 것은 7절의 수기 검증과 폰트 프리베이크뿐이다.
**결과 요약**: [Refactored_Architecture_Master.md 8절](Refactored_Architecture_Master.md#8-일차-누적--달력-날짜-전환-2026-08-15)
**필독**: 10절 안전장치 4건(G-1~G-4)은 모두 구현에 반영됐다. 그중 G-1·G-2는 치명이며, 특히 `SaveData`의 날짜 초기화자를 실제 날짜로 바꾸면 G-1이 되살아난다.
**대상**: 트레이딩 파트 + 데이팅 파트 공용 시간축, 저장 테이블
**배경**: 스토리 편입에 따라 "~일차 누적" 표기를 버리고 **년/월/일/시/분** 단위의 실제 달력으로 전환한다. 다음 날 진행 시 일차가 아니라 날짜가 넘어간다.

---

## 1. 목표와 비목표

**목표**
- 게임 내 시각을 `2026년 3월 2일 (월) 14:35` 형태로 년·월·일·시·분까지 표기한다.
- 하루 종료 → 다음 날 진행 시 달력 날짜가 자연스럽게 넘어간다 (월말·연말 경계 포함).
- 스토리 편입 대비: "3일 후" 같은 **날짜 점프**가 가능해야 한다.
- 저장 테이블이 새 시간 모델을 담고, 기존 세이브가 깨지지 않는다.

**비목표 (이번 범위 밖 — 11절 참고)**
- 요일별 컨텐츠 분기 (요일 값은 무료로 얻지만 쓰지 않는다)
- 스토리 이벤트를 날짜로 저작하는 파이프라인
- 실시간(현실 시각) 연동
- 시간대/윤초/로케일 대응

---

## 2. 현행 구조 (조사 결과)

### 2.1 시간의 소유자

시간은 [GameManager.cs](../../Assets/Scripts/GameManager.cs) 하나가 소유한다. 필드는 셋뿐이다.

| 위치 | 필드 | 비고 |
| --- | --- | --- |
| [GameManager.cs:134](../../Assets/Scripts/GameManager.cs#L134) | `private int currentDay = 1` | 일차. 1부터 누적 |
| [GameManager.cs:135](../../Assets/Scripts/GameManager.cs#L135) | `private int currentHour = 9` | 하루는 09:00 시작 |
| [GameManager.cs:136](../../Assets/Scripts/GameManager.cs#L136) | `private int currentMinute = 0` | |

진행 규칙:
- [GameManager.cs:524](../../Assets/Scripts/GameManager.cs#L524) — `secondsPerGameMinute`마다 1분 증가, `OnGameMinuteAdvanced` 발행
- **24:00에 하루 종료** → `OnDayEnded` 발행 후 정산 대기 ([GameManager.cs:739](../../Assets/Scripts/GameManager.cs#L739))
- 정산 창을 닫으면 `FinalizeProceedToNextDay()`에서 `currentHour=9; currentMinute=0; currentDay++` ([GameManager.cs:821-823](../../Assets/Scripts/GameManager.cs#L821-L823))

> ⚠️ **24:00은 유효 상태다.** `currentHour`가 24까지 올라간 채로 정산 화면이 뜨고, 그동안 `currentDay`는 아직 **어제**여야 한다. 정산 UI·보스 승패 판정이 그 값을 읽기 때문이다 ([GameManager.cs:754](../../Assets/Scripts/GameManager.cs#L754), [DailySettlementUIController.cs:138](../../Assets/Scripts/UI/DailySettlementUIController.cs#L138)). 시각을 단일 `DateTime`으로 합치면 24:00이 자동으로 다음 날 00:00이 되어 이 판정이 **하루 어긋난다.** 설계 결정(3절)의 핵심 제약이다.

### 2.2 일차 번호를 키로 쓰는 곳 (= 건드리면 안 되는 곳)

| 파일 | 용도 |
| --- | --- |
| [BossManager.cs:113-140](../../Assets/Scripts/System/BossManager.cs#L113-L140) | `HasBossToday(day)` / `GetBossData(day)` — 보스 등장 일차 테이블 |
| [GameManager.cs:580](../../Assets/Scripts/GameManager.cs#L580), [:689](../../Assets/Scripts/GameManager.cs#L689) | `storyEvents.Find(e => e.triggerDay == currentDay)` |
| [GameManager.cs:804](../../Assets/Scripts/GameManager.cs#L804) | `currentDay == 20` 최종 엔딩 백업 |
| [GameManager.cs:879](../../Assets/Scripts/GameManager.cs#L879) | `currentDay == 6 / 16` 스토리 컷씬 |
| [MarketSimulationEngine.cs:482-528](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L482-L528) | 일차 구간별 난이도(≤5 / ≤10 / ≤15), 25일차 캡, 일일 국면 롤 |
| [MarketSimulationEngine.cs:438](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L438), [TradingPanelUIController.cs:728](../../Assets/Scripts/UI/Chart/TradingPanelUIController.cs#L728) | `CurrentDay >= 16` 환각 기믹 |
| [ShopManager.cs:119](../../Assets/Scripts/Items/ShopManager.cs#L119), [ActiveItemEffectManager.cs:90](../../Assets/Scripts/Items/ActiveItemEffectManager.cs#L90), [TraderLevelSystem.cs:335](../../Assets/Scripts/Trading/TraderLevelSystem.cs#L335) | 일차 기반 가격 인플레이션 |
| [DeliveryFoodManager.cs:69-92](../../Assets/Scripts/Items/DeliveryFoodManager.cs#L69-L92) | 스테이크 7일 쿨다운 (`currentDay - lastSteakPurchaseDay`) |
| [TraderStatus.cs:356](../../Assets/Scripts/TraderStatus.cs#L356) | 일차 비례 체력 감소 |
| [TraderMemoryManager.cs:94-151](../../Assets/Scripts/AI/TraderMemoryManager.cs#L94-L151) | 기억 일차 태깅 및 pruning |
| [ChoiceEventController.cs:246-265](../../Assets/Scripts/Events/ChoiceEventController.cs#L246-L265) | 일일 스케줄 리셋, `day * 1440 + 분` 절대분 |
| [YomiRoomManager.cs:147-347](../../Assets/Scripts/DatingSim/YomiRoom/YomiRoomManager.cs#L147-L347) | 요미 대화 하루 게이트 4종 (인사/힌트/1회한도/일일리셋) |
| [DatingTimeManager.cs:188](../../Assets/Scripts/DatingSim/Core/DatingTimeManager.cs#L188) | `SyncToNewDay(newDay)` — 일차 미러 + 슬롯 리필 |
| [TutorialManager.cs:99](../../Assets/Scripts/System/TutorialManager.cs#L99), [SaveLoadManager.cs:416](../../Assets/Scripts/System/SaveLoadManager.cs#L416) | `CurrentDay > 1` = 튜토리얼 완료 간주 |

**총 15개 시스템, 30개 이상 호출 지점.** 이들이 원하는 것은 "달력 날짜"가 아니라 **"게임 시작으로부터 며칠째인가"** 하는 서수(ordinal)다. 달력이 들어와도 이 의미는 변하지 않는다.

### 2.3 화면 표기

| 파일 | 현재 표기 |
| --- | --- |
| [TopStatusBarUIController.cs:201](../../Assets/Scripts/UI/TopBar/TopStatusBarUIController.cs#L201) | `DAY 03` |
| [HUDController.cs:109](../../Assets/Scripts/HUDController.cs#L109) | `DAY 03` |
| [GameOverUIController.cs:231](../../Assets/Scripts/UI/GameOverUIController.cs#L231) | `DAY 03  /  14:35` |
| [DailySettlementUIController.cs:145](../../Assets/Scripts/UI/DailySettlementUIController.cs#L145), [:304](../../Assets/Scripts/UI/DailySettlementUIController.cs#L304) | 일차 기반 정산 헤더 |

### 2.4 저장 테이블 현황

[SaveData.cs:43-45](../../Assets/Scripts/System/SaveData.cs#L43-L45)
```csharp
public int CurrentDay = 1;
public int CurrentHour = 9;
public int CurrentMinute = 0;
```
- 수집: [SaveLoadManager.cs:157-159](../../Assets/Scripts/System/SaveLoadManager.cs#L157-L159)
- 복원: [SaveLoadManager.cs:505-507](../../Assets/Scripts/System/SaveLoadManager.cs#L505-L507) — **리플렉션으로 private 필드에 직접 주입**

그 외 **일차 서수를 담은 저장 필드가 12개** 더 있다:
`MarketLastUpdatedDay`, `OutlookDay`, `LastSteakPurchaseDay`, `EventLastTriggerDay`, `EventLastTriggerGameMinutes`, `DatingDay`, `TalkHintIssuedDay`, `TalkLastGreetingDay`, `TalkLastSessionEndDay`, `TalkDailyStateDay`, `DailySummaryKeys`(기억 일차 키), `MemoryEntry.Day`.

또한 [DatingTimeManager.cs:181](../../Assets/Scripts/DatingSim/Core/DatingTimeManager.cs#L181)에 명시된 계약이 있다: **`SaveData.DatingDay == SaveData.CurrentDay`는 항상 성립해야 한다.**

---

## 3. 설계 결정

### 3.1 대안 비교

| | A. 일차 권위 + 달력 파생 | **B. 달력 권위 + 일차 파생 (채택)** | C. 달력 전면 교체 |
| --- | --- | --- | --- |
| 저장 필드 | 시작 날짜만 추가 | 현재 날짜 추가 | 12개 서수 필드 전부 날짜로 |
| 2.2의 30개 호출 지점 | 무수정 | **무수정** | 전부 수정 |
| 날짜 점프(스토리) | 불가 | **가능** | 가능 |
| 마이그레이션 | 불필요 | **1줄** | 필드 12개 재계산, 위험 |
| 월말/연말 경계 | 자동 | 자동 | 자동 |

**B 채택.** 달력이 사실상의 권위값이 되면서(스토리 날짜 점프 가능) 기존 서수 기반 로직은 한 줄도 손대지 않는다. C는 얻는 것 없이 15개 시스템과 세이브 호환성을 동시에 흔든다.

### 3.2 데이터 모델

```
게임 시작 날짜 (에폭, 저장됨) : StartDate = 2026-03-02 (월)   ← 9절에서 확정. 상수 아님, G-4 참고
현재 날짜   (권위, 저장됨)   : currentDate : DateTime (날짜 부분만, 시각 없음)
현재 시각   (권위, 저장됨)   : currentHour / currentMinute    ← 현행 그대로. 24:00 유지
현재 일차   (파생, 저장됨*)  : CurrentDay = (currentDate - StartDate).Days + 1
```

\* 일차는 파생값이지만 세이브에는 **계속 기록한다.** 구버전 호환, `DatingDay` 계약 검증, 디버깅 때문이다.

**시각(시/분)을 날짜와 합치지 않는 이유는 2.1의 24:00 제약** 때문이다. 하나의 `DateTime`으로 합치면 24:00 → 익일 00:00이 되어 정산 중에 일차가 미리 넘어간다. 날짜와 시각을 분리하면 현행 동작이 그대로 보존된다.

### 3.3 GameManager 공개 API (신규/변경)

```csharp
// 신규
public DateTime CurrentDate  => currentDate;              // 년/월/일
public int      CurrentYear  => currentDate.Year;
public int      CurrentMonth => currentDate.Month;
public int      CurrentDayOfMonth => currentDate.Day;     // ⚠️ CurrentDay(일차)와 이름이 다름에 주의
public DayOfWeek CurrentDayOfWeek => currentDate.DayOfWeek;

// 변경: 필드 → 파생 프로퍼티 (시그니처 동일 → 호출부 30곳 무수정)
public int CurrentDay => (currentDate - StartDate).Days + 1;

// 신규: 스토리용 날짜 점프
// ⚠️ 보스·스토리·엔딩이 예약된 일차를 뛰어넘으면 그 컨텐츠가 조용히 사라진다. G-2의 클램프 필수.
public void AdvanceDate(int days)                          // days >= 1
```

`CurrentHour` / `CurrentMinute`는 변경 없음.

### 3.4 날짜 진행

[GameManager.cs:821-823](../../Assets/Scripts/GameManager.cs#L821-L823)
```csharp
// 변경 전
currentHour = 9; currentMinute = 0; currentDay++;
// 변경 후
currentHour = 9; currentMinute = 0; currentDate = currentDate.AddDays(1);
```
월말·연말 경계는 `DateTime.AddDays`가 처리한다. 이후 줄들(`SyncToNewDay(currentDay)`, `OnDayAdvanced(currentDay)`)은 파생 프로퍼티를 읽으므로 **무수정**.

---

## 4. 저장 테이블 변경

### 4.1 신규 필드 ([SaveData.cs](../../Assets/Scripts/System/SaveData.cs))

```csharp
// --- 달력 날짜 (2026-08 개편) ---
// JsonUtility가 DateTime을 직렬화하지 못하므로 ISO 문자열로 저장합니다.
// 빈 문자열 = 달력 도입 이전 세이브. 마이그레이터가 CurrentDay로부터 역산합니다.
public string CurrentDate = "";      // "yyyy-MM-dd"
// 세이브가 만들어진 시점의 시작 날짜(에폭). 밸런싱으로 에폭 상수를 바꿔도
// 진행 중인 세이브의 일차 계산이 어긋나지 않게 세이브가 스스로 들고 있습니다.
public string StartDate = "";        // "yyyy-MM-dd"
```

**형식 선택 근거**: `long Ticks`는 사람이 못 읽고 세이브 디버깅이 불가능하다. `int Year/Month/Day` 3필드는 부분 손상 시 유효하지 않은 날짜가 만들어진다. ISO 문자열 1필드가 가장 안전하고 읽힌다.

### 4.2 유지되는 필드

| 필드 | 처리 |
| --- | --- |
| `CurrentDay` | **유지.** 파생값이지만 계속 기록 (호환/검증/디버그) |
| `CurrentHour` / `CurrentMinute` | 변경 없음 |
| 나머지 일차 서수 12개 (2.4절) | **전부 변경 없음.** 서수 의미가 그대로라 마이그레이션도 불필요 |

이것이 B안의 핵심 이득이다. **실질적인 저장 테이블 변경은 신규 문자열 2개가 전부다.**

### 4.3 수집 / 복원

수집 — [SaveLoadManager.cs:157](../../Assets/Scripts/System/SaveLoadManager.cs#L157) 인근:
```csharp
data.CurrentDate = gm.CurrentDate.ToString("yyyy-MM-dd");
data.StartDate   = gm.StartDate.ToString("yyyy-MM-dd");
data.CurrentDay  = gm.CurrentDay;   // 기존 줄 유지 (파생값 기록)
```

복원 — [SaveLoadManager.cs:505](../../Assets/Scripts/System/SaveLoadManager.cs#L505) 인근. 현재 리플렉션으로 `currentDay`를 주입하는데, 이 필드가 사라지므로 **리플렉션 3줄을 GameManager의 internal 복원 메서드 1개로 교체**한다:
```csharp
gm.RestoreClock(CurrentData.StartDate, CurrentData.CurrentDate,
                CurrentData.CurrentHour, CurrentData.CurrentMinute);
```
> 리플렉션은 필드명이 바뀌어도 컴파일 에러 없이 조용히 실패한다. 시간 필드를 손대는 이번이 걷어낼 적기다. (`RestoreStartOfDayEquity` 등 기존 `internal` 복원 메서드와 같은 방식)

### 4.4 마이그레이션 ([SaveDataMigrator.cs](../../Assets/Scripts/System/SaveDataMigrator.cs))

`Migrate()` 파이프라인에 버전 블록 1개 추가:
```csharp
if (CompareVersions(dataVersion, "1.7.0") < 0)
{
    // 달력 도입 이전 세이브: 일차 서수로부터 날짜를 역산합니다.
    // 다른 일차 필드는 전부 서수 그대로라 손댈 것이 없습니다.
    if (string.IsNullOrEmpty(data.StartDate))   data.StartDate = GameManager.DefaultStartDateText;
    if (string.IsNullOrEmpty(data.CurrentDate))
    {
        var start = DateTime.ParseExact(data.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        data.CurrentDate = start.AddDays(Mathf.Max(1, data.CurrentDay) - 1).ToString("yyyy-MM-dd");
    }
}
```
**되돌리기**: 구버전 클라이언트가 신버전 세이브를 읽어도 `CurrentDate`/`StartDate` 키를 조용히 무시하고 `CurrentDay`로 정상 동작한다 (JsonUtility 동작). 전방 호환이 공짜로 확보된다.

**파싱 실패 방어**: `CurrentDate` 파싱이 실패하면(손상·수기 편집) 예외를 던지지 말고 `StartDate + (CurrentDay-1)`로 폴백한다. 시간축 하나 때문에 세이브 전체를 못 읽게 되면 안 된다.

---

## 5. 코드 변경 목록

| 파일 | 변경 | 규모 |
| --- | --- | --- |
| [GameManager.cs](../../Assets/Scripts/GameManager.cs) | `currentDay` 필드 → `currentDate` + 파생 프로퍼티, `AdvanceDate()`, `RestoreClock()`, 에폭 상수 | 중 |
| [GameManager.cs:234](../../Assets/Scripts/GameManager.cs#L234), [:56](../../Assets/Scripts/GameManager.cs#L56), [:68](../../Assets/Scripts/GameManager.cs#L68), [:289](../../Assets/Scripts/GameManager.cs#L289) | `currentDay=1` 대입 4곳 → `currentDate=StartDate` (P2P 모드 3곳 포함) | 소 |
| [SaveData.cs](../../Assets/Scripts/System/SaveData.cs) | 문자열 필드 2개 추가 | 소 |
| [SaveLoadManager.cs](../../Assets/Scripts/System/SaveLoadManager.cs) | 수집 2줄 추가, 리플렉션 3줄 → `RestoreClock()` 1줄 | 소 |
| [SaveDataMigrator.cs](../../Assets/Scripts/System/SaveDataMigrator.cs) | 1.7.0 블록 | 소 |
| UI 4종 (2.3절) | 날짜 표기 (6절) | 소 |
| **2.2의 15개 시스템** | **무수정** | — |

**P2P 주의**: [GameManager.cs:68](../../Assets/Scripts/GameManager.cs#L68) `ApplyP2PState`는 매 패킷마다 `currentDay=1`을 대입한다. P2P는 하루짜리 경기라 `currentDate = StartDate`로 치환하면 동작이 동일하다. `FXOverdose.P2P.Core`는 `TotalMinutes`(0~1440)만 다루므로 **달력을 전혀 모른다 — asmdef의 엔진 비참조 규약도 그대로 유지된다.**

---

## 6. 화면 표기

기본 형식 (9절 확정):
```
6월 26일                 ← 날짜 카드 (요일 없음)
14:35                    ← 시각 (현행 유지)
```
`DAY NN` 표기는 **전부 삭제**한다. 대상은 아래 8곳이다.

| 파일 | 현재 | 변경 후 |
| --- | --- | --- |
| [TopStatusBarUIController.cs:201](../../Assets/Scripts/UI/TopBar/TopStatusBarUIController.cs#L201) | `DAY 03` | `6월 28일` |
| [HUDController.cs:109](../../Assets/Scripts/HUDController.cs#L109) | `DAY 03` | `6월 28일` |
| [GameOverUIController.cs:231](../../Assets/Scripts/UI/GameOverUIController.cs#L231) | `DAY 03 / 14:35` | `2026년 6월 28일 / 14:35` |
| [DailySettlementUIController.cs:169](../../Assets/Scripts/UI/DailySettlementUIController.cs#L169) | `DAY 03 COMPLETE` | `6월 28일 COMPLETE` |
| [DailySettlementUIController.cs:306-307](../../Assets/Scripts/UI/DailySettlementUIController.cs#L306-L307) | `DAY 03 SETTLEMENT` / `PROCEED TO DAY 04` | `6월 28일 SETTLEMENT` / `PROCEED TO 6월 29일` |
| [ChoiceEventPopupUIController.cs:161](../../Assets/Scripts/Events/ChoiceEventPopupUIController.cs#L161) | `… / DAY 03 14:35` | `… / 6월 28일 14:35` |
| [BossBattleUIController.cs:213](../../Assets/Scripts/UI/BossBattleUIController.cs#L213), [:230](../../Assets/Scripts/UI/BossBattleUIController.cs#L230) | `DAY 20 / BOSS` | `7월 15일 / BOSS` |
| [P2PGameplayUIController.cs:162](../../Assets/Scripts/P2P/UI/P2PGameplayUIController.cs#L162) | `DAY 01` 고정 | `6월 26일` 고정 |

- **보스 배지는 `BossData.Day`(서수)를 표시**하므로 날짜 환산이 필요하다 → `GameManager.DateForDay(ordinal)` 한 줄짜리 헬퍼를 쓴다. 현재 날짜를 쓰면 [BossBattleUIController.cs:204](../../Assets/Scripts/UI/BossBattleUIController.cs#L204)에 적힌 "일차 복구보다 보스 스폰 이벤트가 먼저 올 수 있다"는 순서 문제에 걸린다.
- **정산 원장 일련번호** `DAILY LEDGER / #003`([:305](../../Assets/Scripts/UI/DailySettlementUIController.cs#L305))는 `DAY NN` 라벨이 아니라 문서 번호이므로 **서수 유지**.
- 에디터 빌더의 플레이스홀더 텍스트([TradingViewUIBuilder.cs:311](../../Assets/Editor/TradingViewUIBuilder.cs#L311), [DayTimeCardStyler.cs:90](../../Assets/Editor/DayTimeCardStyler.cs#L90))도 함께 교체한다. 런타임에 덮이지만 에디터 미리보기가 옛 형식으로 남는다.
- 날짜/시각 문자열 길이가 `DAY 03`과 비슷하므로 `DayTimeCardWidth = 330f` 고정폭은 **그대로 둔다.**

**포맷 헬퍼**: 날짜 문자열 조립이 7개 파일에 흩어지므로 `FXOverdose.Core.GameCalendar` 정적 클래스 하나에 모은다 (직렬화 포맷 `yyyy-MM-dd`, `TryParse`, `6월 26일`, `2026년 6월 26일`). G-3의 `InvariantCulture` 규약을 한 곳에서 지키기 위해서도 필요하다.

> ⚠️ **폰트 프리베이크**: 새 한글 문자열(`년`, `월`, `일`, 요일 `월화수목금토일`)이 소스에 추가되므로 `Tools/Prebake All Scripts Text into Font`를 **반드시 재실행**해야 한다. 안 하면 □로 렌더된다. (CLAUDE.md 규약)

---

## 7. 검증 체크리스트

- [ ] 신규 게임 시작 → 에폭 날짜로 표기
- [ ] 24:00 정산 화면에서 날짜가 **아직 어제**인지 (2.1의 핵심 회귀 지점)
- [ ] 정산 완료 → 익일 09:00, 날짜 +1
- [ ] **월말 경계**: 에폭을 조정해 3/31 → 4/1 전환 확인
- [ ] **연말 경계**: 12/31 → 1/1 전환 확인
- [ ] 보스 등장 일차(예: 20일차 최종 보스)가 여전히 정확한 날에 발생
- [ ] 스테이크 7일 쿨다운이 날짜 전환 후에도 정상
- [ ] 요미 대화 하루 게이트 4종이 날짜 전환 후 리셋
- [ ] `DatingDay == CurrentDay` 계약 유지 ([DatingTimeManager.cs:181](../../Assets/Scripts/DatingSim/Core/DatingTimeManager.cs#L181))
- [ ] **구버전 세이브 로드** → 날짜 역산 정확, 일차 불변
- [ ] **20일차 구버전 세이브**가 1일차 날짜로 로드되지 않는지 (G-1의 sentinel 함정)
- [ ] **새 게임 → 요미의 방에서 즉시 저장 → 재로드** → 날짜 정상 (G-1 치명 경로)
- [ ] 같은 버전에서 만들어진 빈 날짜 세이브도 `RestoreClock` 폴백으로 복구 (G-1)
- [ ] `AdvanceDate(5)`로 20일차를 뛰어넘으려 할 때 클램프 + 경고 로그 (G-2)
- [ ] 시스템 로케일을 `th-TH` 또는 `ja-JP`로 바꾸고 저장→로드 왕복 (G-3, 연도 2569 회귀)
- [ ] `CurrentDate` 손상 세이브 → 폴백 동작, 예외 없음 (`TryParseExact`)
- [ ] 에폭 상수를 바꾼 뒤 **기존 세이브**를 로드 → 일차가 어긋나지 않는지 (G-4)
- [ ] P2P 경기 진입 → 시계 정상, `FXOverdose.P2P.Core` 무수정 통과
- [ ] `dotnet build` 4종 (`Assembly-CSharp`, `-Editor`, `P2P.Core`, `P2P.Core.Tests`)
- [ ] 폰트 프리베이크 재실행 후 날짜 문자열 렌더 확인

EditMode 테스트는 `FXOverdose.P2P.Core` 전용이고 이번 변경이 그 어셈블리를 건드리지 않으므로 **신규 테스트 대상 없음.** 날짜 경계 검증은 위 수기 체크리스트로 대체한다.

---

## 8. 작업 순서

1. `GameManager` 시간 모델 교체 + 파생 프로퍼티 (여기서 컴파일이 통과하면 2.2의 30개 호출 지점이 안전하다는 증명이 된다)
2. `SaveData` / `SaveLoadManager` / `SaveDataMigrator` — **G-1(새 게임 시딩 + 버전 무관 폴백), G-3(InvariantCulture), G-4(에폭 인스턴스화)를 여기서 함께 처리한다. 나중이 없다.**
3. UI 표기 4종 + 폰트 프리베이크
4. `AdvanceDate()` + G-2 클램프 (스토리 점프 — 실제 호출부는 스토리 작업 시)
5. 7절 검증
6. [Refactored_Architecture_Master.md](Refactored_Architecture_Master.md)에 구조 변경 기록

1~2번까지만 끝나도 게임은 정상 동작한다 (표기만 옛 형식). 중단 가능한 지점이다.

---

## 9. 결정 사항 (2026-08-15 확정)

| # | 항목 | **확정** |
| --- | --- | --- |
| 1 | 게임 시작 날짜(에폭) | **`2026-06-26` (금)** — 20일차 = 2026-07-15 (수) |
| 2 | 연도 표기 | **실존 연도(2026)** |
| 3 | 날짜 표기 형식 | **`6월 26일`** (월·일). 연도가 필요한 화면은 `2026년 6월 26일` |
| 4 | 요일 표기 | **생략** |
| 5 | `DAY NN` 병기 | **하지 않음** — 모든 화면에서 제거 |
| 6 | 게임 길이 | 스토리 작업에서 변경 예정. **지금은 20일 유지하되 하드코딩된 `20`을 상수 하나로 모은다** |

**#3 + #4 해석**: 두 항목이 서로 스치므로 명시한다 — 형식은 `6월 26일`이고 **요일 괄호는 붙이지 않는다**(#4가 요일 질문의 전용 답이므로 우선). 원안의 `3월 2일 (월)`은 초안 예시 문구였다.

**#6 해석**: "달력에 맞춰 조정"을 지금 20일 → 다른 길이로 바꾸는 것으로 읽지 않는다. 게임 길이를 건드리면 보스 일정([BossManager.cs:73-79](../../Assets/Scripts/System/BossManager.cs#L73-L79)의 3·6·9·12·15·18·20일차), 시장 난이도 구간(≤5/≤10/≤15), 스토리 이벤트 `triggerDay`가 **동시에** 흔들리는 밸런스 작업이라 "일단은"의 범위를 넘는다.
달력 모델 자체는 이미 임의 길이를 수용하므로(월말·연말 자동 처리), 이번에는 **엔딩 일차를 `GameManager.StoryLastDay` 상수 하나로 모아** 스토리 작업이 한 줄로 길이를 바꿀 수 있게 해 둔다. 실제 최종일의 주인은 `BossData.IsFinalBoss`이고, 이 상수는 보스 스폰 실패 시의 백업 판정([GameManager.cs:804](../../Assets/Scripts/GameManager.cs#L804))에만 쓰인다.

---

## 10. 안전장치 점검 (2차 검토 보강)

초안 1차에서 누락되었던 항목이다. **G-1은 날짜가 영구 유실되는 경로이므로 착수 시 반드시 함께 구현한다.**

### G-1. 새 게임의 첫 세이브가 빈 날짜로 기록된다 ★치명

[SaveLoadManager.cs:455](../../Assets/Scripts/System/SaveLoadManager.cs#L455) 주석대로, 새 게임은 **`new SaveData()`의 필드 초기화자가 곧 초기 상태**다("날짜·시각…은 따로 심지 않습니다"). 그런데:

1. 스토리 모드는 **요미의 방에서 시작**하고 거래 개시 전에 저장한다 → 그 시점에 `GameManager`가 씬에 없다 (`gm == null`)
2. 수집 코드는 `if (gm != null)` 안에 있다 ([:154](../../Assets/Scripts/System/SaveLoadManager.cs#L154)) → `CurrentDate`가 `""` 그대로 디스크에 기록
3. `data.Version = Application.version`은 이미 최신 ([:144](../../Assets/Scripts/System/SaveLoadManager.cs#L144)) → 로드 시 **마이그레이터가 `dataVersion == currentAppVersion`에서 즉시 리턴**하여 역산이 돌지 않는다 ([SaveDataMigrator.cs:20-23](../../Assets/Scripts/System/SaveDataMigrator.cs#L20-L23))

**결과: 새 게임의 날짜가 복구 불가능하게 비어 로드된다.**

> ⚠️ **여기서 초기화자에 기본 날짜를 박는 해법은 함정이다.** `CurrentDate = "2026-03-02"`로 초기화하면 G-1은 막히지만, **구버전 세이브에는 이 키 자체가 없어 초기화자 값이 그대로 남는다.** 마이그레이터의 "빈 문자열 = 구버전" 판정이 무력화되어 **20일차 세이브가 1일차 날짜로 로드된다.** 빈 문자열은 마이그레이션 감지용 sentinel이므로 반드시 유지해야 한다.

**해결 (셋 다 필요)**
- `SaveData`의 초기화자는 **빈 문자열 유지** (sentinel)
- `PrepareNewGame()` ([:456](../../Assets/Scripts/System/SaveLoadManager.cs#L456) 블록)에서 `CurrentData.StartDate` / `CurrentData.CurrentDate`를 **명시적으로 심는다** — `Balance`/`StartOfDayEquity`를 이미 같은 이유로 심고 있으므로 같은 자리에 붙인다
- `RestoreClock()`에 **버전 무관 폴백**: 날짜가 비었거나 파싱 실패면 `StartDate + (CurrentDay - 1)`, `StartDate`마저 비었으면 에폭 상수. 마이그레이터에만 의존하지 않는다 (마이그레이터는 동일 버전에서 돌지 않으므로)

### G-2. 날짜 점프가 보스·스토리·엔딩 일차를 건너뛴다 ★치명

3.3절의 `AdvanceDate(int days)`는 스토리용으로 넣었는데, 일차 서수를 건너뛰면 **그 날에 걸려 있던 컨텐츠가 조용히 사라진다.**

| 건너뛰면 잃는 것 | 근거 |
| --- | --- |
| 보스 조우 | [BossManager.cs:113](../../Assets/Scripts/System/BossManager.cs#L113) `HasBossToday(day)` — 그 날에만 판정 |
| 6·16일차 스토리 컷씬 | [GameManager.cs:879](../../Assets/Scripts/GameManager.cs#L879) |
| **20일차 최종 엔딩** | [GameManager.cs:804](../../Assets/Scripts/GameManager.cs#L804) `currentDay == 20` — **`==` 비교라 뛰어넘으면 엔딩이 영영 나지 않는다** |
| 정기 지출 / 위약금 | [GameManager.cs:580](../../Assets/Scripts/GameManager.cs#L580) `triggerDay` 일치 검색 |

**해결**: `AdvanceDate()`는 착수 4단계(8절)에서 **예약 컨텐츠가 있는 일차를 넘지 못하도록 클램프**하고, 잘렸으면 `Debug.LogWarning`을 남긴다. 스토리에서 진짜로 여러 날을 건너뛰어야 한다면 그때 "건너뛴 날의 정기 지출 합산" 같은 정책을 별도로 정한다 — **이번 범위에서는 클램프까지만.**
> 임시로 `currentDay == 20` 같은 `==` 비교를 `>=`로 바꾸는 것은 이 계획의 범위 밖이다 (엔딩 조건 변경). 클램프로 막는 편이 안전하다.

### G-3. 문화권에 따라 연도가 2569년으로 저장된다

`DateTime.ToString("yyyy-MM-dd")`는 **시스템 문화권의 달력**을 따른다. 태국(불기)·일본(연호) 로케일에서는 `2569-03-02` 같은 값이 기록되고, 다시 `ParseExact`(InvariantCulture)로 읽으면 날짜가 어긋나거나 실패한다. Unity 빌드는 OS 문화권을 물려받을 수 있다.

**해결**: 직렬화/역직렬화 **양쪽 모두** `CultureInfo.InvariantCulture`를 명시한다. 4.3절 수집 코드도 해당한다.
```csharp
gm.CurrentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
```
`Parse`가 아니라 **`TryParse` 계열**을 쓴다 — 손상된 세이브 하나 때문에 로드 전체가 예외로 죽으면 안 된다 (4.4절 폴백과 같은 이유).

### G-4. 에폭은 상수가 아니라 인스턴스 상태다

3.2절에서 `StartDate`를 "상수"라고 적었으나, 4.1절은 세이브가 자기 `StartDate`를 들고 있다고 명시한다. 둘이 모순이다.

**해결**: `GameManager`의 `StartDate`는 **에폭 상수를 기본값으로 갖는 인스턴스 필드**이고, `RestoreClock()`이 세이브 값으로 덮어쓴다. 상수는 `public const string DefaultStartDateText = "2026-03-02"` 하나만 둔다(마이그레이터도 이 값을 참조). 이렇게 해야 밸런싱으로 에폭을 바꿔도 **진행 중인 세이브의 일차 계산이 어긋나지 않는다.**

### 이미 안전한 것 (확인 완료, 조치 불필요)

- **방·월드맵에서의 저장** — [SaveLoadManager.cs:137](../../Assets/Scripts/System/SaveLoadManager.cs#L137)의 부분 저장(SV-A6)이 직전 `CurrentData`를 베이스로 삼으므로, `gm == null`이어도 **기존 날짜가 보존된다.** (단 새 게임 첫 저장은 베이스가 빈 `SaveData`라 G-1에 해당)
- **튜토리얼 백필** `CurrentData.CurrentDay > 1` ([:416](../../Assets/Scripts/System/SaveLoadManager.cs#L416)) — `CurrentDay`를 계속 저장하므로(4.2절) 무사
- **`DatingTimeManager.LoadFromSaveData`가 GameScene보다 먼저 호출되는 경로** ([:410](../../Assets/Scripts/System/SaveLoadManager.cs#L410)) — `DatingDay`는 서수 그대로라 영향 없음
- **`ChoiceEventController`의 절대분** `day * 1440 + 분` ([:265](../../Assets/Scripts/Events/ChoiceEventController.cs#L265)) — 파생 `CurrentDay`를 읽으므로 동작 동일
- **구버전 클라이언트 호환** — 신규 키를 JsonUtility가 조용히 무시 (4.4절)

---

## 11. 범위 밖 (YAGNI)

- **요일별 컨텐츠 분기** (주말 편의점 알바 시급 등) — 요일 값은 3.3절에서 이미 노출되므로, 필요해지면 컨텐츠 코드만 추가하면 된다. 시간 시스템은 더 손댈 것이 없다.
- **스토리 이벤트의 날짜 저작** — 현행 `triggerDay` 서수를 유지한다. 날짜로 쓰고 싶어지면 `storyEvents`에 `triggerDate` 문자열을 선택 필드로 추가하고 로드 시 서수로 환산하면 된다. 지금 만들면 쓰지도 않는 파서를 유지보수하게 된다.
- **시/분 단위 시간축 통합**(`DateTime` 하나로 합치기) — 2.1의 24:00 제약 때문에 이득 없이 회귀만 부른다.
- **시간대·로케일·윤초** — 단일 지역 한국어 게임.
