# P&L 스파크라인 — 소유권 이관 및 일차 기준 전환 계획

작성일: 2026-08-14 / 대상: 상단 HUD P&L 카드(`TopStatusBarUIController` + `SparklineRenderer`)
상태: **적용 완료 (2026-08-14)** — 인게임 검증만 남음

## 적용 결과

| 항목 | 상태 |
| --- | --- |
| `SaveData.DailyEquityHistory` 추가 | ✅ |
| `GameManager` 이력 소유 · 표본 추출 · 일차 리셋 | ✅ |
| 기존 `CaptureSettlementContext`/`RestoreSettlementContext`에 편승 | ✅ 새 배선 없음 |
| `TopStatusBarUIController` 상태 제거 | ✅ UI의 궤적 상태 **0개** |
| `SparklineRenderer` 파라미터 `IReadOnlyList<float>`로 확장 | ✅ |
| P&L 라벨(누적 수익률) | ✅ **변경하지 않음** |
| `SaveRoundTripTester` 검증 항목 추가 | ✅ 직렬화 보존 + 구버전 빈 리스트 |

4개 어셈블리 전부 에러 0 / 경고 0.

### 적용 중 발견해 함께 처리한 P2P 회귀

표본 추출을 `AdvanceOneMinute()`에만 두면 **P2P에서 그래프가 직선이 됩니다.**
[`ApplyP2PState`](../../Assets/Scripts/GameManager.cs#L44)는 분 진행을 `AdvanceOneMinute`로 하지 않고
`OnGameMinuteAdvanced`만 루프로 발행하기 때문입니다. 종전 UI 구현은 그 이벤트를 구독했기에 우연히 동작했습니다.

`SampleDailyEquityIfDue()`로 추출해 **싱글(`AdvanceOneMinute`)과 P2P(`ApplyP2PState`) 양쪽에서 호출**합니다.
분 중복 표본은 `lastEquitySampleMinute` 가드가 막습니다.

---


## 증상

세이브를 불러오면 P&L 스파크라인이 **그날 수익률과 무관한 직선**으로 표시됩니다.

## 원인

`equityHistory`가 **UI 컨트롤러의 private 필드**([TopStatusBarUIController.cs:54](../../Assets/Scripts/UI/TopBar/TopStatusBarUIController.cs#L54))이고
`SaveData`에는 자산 이력 필드가 **아예 없습니다**(`StartOfDayEquity` 스칼라 1개와 시장 캔들용 `ChartHistories`뿐).

로드 시 `Start()`가 이력을 `[현재자산]` 1점으로 재시작 → [SparklineRenderer](../../Assets/Scripts/UI/TopBar/SparklineRenderer.cs#L59)가
실시간 끝점 하나를 붙여 총 2점 → **선분 1개 = 직선**. 인게임 15분 뒤 다음 고정점이 쌓일 때까지 유지됩니다.

> **구조적 원인:** CLAUDE.md의 레이어링 규약("매니저가 로직·데이터를 갖고 UI 컨트롤러는 구독만 하며 상태를 소유하지 않는다")을
> 이 컨트롤러가 위반하고 있었습니다. 이력이 UI에 있으니 매니저들을 훑는 `SaveGame()`의 수집 범위에
> **구조적으로 들어올 수 없었습니다.** 다른 저장 항목이 전부 매니저에 있는 것과 대조됩니다.

## 함께 확인된 사실

- 스파크라인은 `OnDayEnded`를 구독하지 않아 **날짜가 바뀌어도 리셋되지 않는** 롤링 96점(인게임 24시간) 창이었습니다.
- P&L 라벨(%)은 [L238-242](../../Assets/Scripts/UI/TopBar/TopStatusBarUIController.cs#L238)에서 `startingBalance`를 분모로 쓰는 **누적** 수익률이었습니다.
- 즉 그래프도 라벨도 "해당 일차" 기준이 아니었습니다.

---

## 채택한 방향

| 항목 | 결정 |
| --- | --- |
| 소유권 | **B안 — `GameManager`로 이관.** UI는 읽기만 하고 상태를 갖지 않음 |
| 그래프 기준 | **일차 리셋.** 새 날이 시작되면 이력을 비우고 그날 시작 자산으로 재시작 |
| 라벨 분모 | **`startingBalance` 유지 (누적 수익률).** 변경하지 않음 |

> 그래프는 당일 궤적, 라벨은 누적 수익률로 **의도적으로 기준이 다릅니다.**
> 목표 자본(`targetBalance`)이 누적 기준이라 플레이어의 전체 진척도 창구를 유지해야 하기 때문입니다.
> 스파크라인은 "오늘 어떻게 흘러왔는가", 퍼센트는 "시작 대비 지금 어디인가"를 각각 답합니다.
> 이 분리는 실수가 아니므로 한쪽에 맞추려 하지 마십시오.

### 왜 `GameManager`인가

새 매니저를 만들지 않습니다. 필요한 것이 전부 이미 `GameManager`에 있습니다.

- 표본 추출 시점: `AdvanceOneMinute()`이 `OnGameMinuteAdvanced`를 쏘는 바로 그 지점([L494](../../Assets/Scripts/GameManager.cs#L494))
- 일차 리셋 시점: `FinalizeProceedToNextDay()`에서 `StartOfDayEquity`를 재설정하는 그 줄 옆([L707](../../Assets/Scripts/GameManager.cs#L707))
- 자산 계산: `TraderStatus.GetTotalEquity()`가 이미 존재하고 L707이 이미 그것을 씁니다 — **새로 만들지 않고 재사용**
- 저장 훅: `CaptureSettlementContext` / `RestoreSettlementContext` 쌍이 이미 `SaveGame`·로드 경로에 배선돼 있습니다
  ([SaveLoadManager.cs:159](../../Assets/Scripts/System/SaveLoadManager.cs#L159) / [L580](../../Assets/Scripts/System/SaveLoadManager.cs#L580)) — **새 배선 불필요**

### 리셋 지점이 `OnDayEnded`가 아닌 이유 ⚠️

`OnDayEnded`는 **24:00에, 정산 화면이 뜨기 전에** 울립니다([L582/620/627](../../Assets/Scripts/GameManager.cs#L582)).
여기서 이력을 비우면 **정산 화면이 그날 그래프를 보여주는 도중에 그래프가 사라집니다.**

실제 일차 증가와 `StartOfDayEquity` 재설정은 플레이어가 "다음날 진행하기"를 누른 뒤
`FinalizeProceedToNextDay()`에서 일어납니다. 두 값은 같은 의미("새 날의 기준")이므로 **같은 곳에서 함께** 리셋합니다.

---

## 변경 내역

### 1. `GameManager` — 이력 소유

```csharp
// 당일 자산 궤적. 상단 HUD P&L 스파크라인이 읽습니다.
// UI가 아닌 여기서 들고 있어야 세이브 수집 범위에 들어옵니다.
private readonly List<float> dailyEquityHistory = new List<float>();
private int lastEquitySampleMinute = -1;
private const int EquitySampleIntervalMinutes = 15;
private const int MaxEquitySamples = 96;   // 인게임 24시간 / 15분

public IReadOnlyList<float> DailyEquityHistory => dailyEquityHistory;
```

- **표본 추출**: `AdvanceOneMinute()`에서 `OnGameMinuteAdvanced` 발행 직후, `currentMinute % 15 == 0`일 때 1점 추가
- **일차 리셋**: `FinalizeProceedToNextDay()`에서 `StartOfDayEquity` 설정 직후 `dailyEquityHistory.Clear()` 후 그 값으로 1점 시딩
- **새 게임**: `StartNewGame()`에서도 동일하게 시딩

### 2. `SaveData` — 필드 추가

```csharp
// 당일 P&L 스파크라인 궤적. 비어 있으면 로드 후 StartOfDayEquity로 재시딩합니다.
public List<float> DailyEquityHistory = new List<float>();
```

구버전 세이브는 이 키가 없어 초기화자의 빈 리스트가 유지됩니다 → **마이그레이터 불필요.**

### 3. `GameManager.CaptureSettlementContext` / `RestoreSettlementContext`

기존 쌍에 이력 수집·복원을 얹습니다. 복원 시 이력이 비어 있으면 `StartOfDayEquity`로 1점 시딩해
로드 직후에도 그래프가 그날 기준선에서 시작하게 합니다.

### 4. `TopStatusBarUIController` — 상태 제거

- `equityHistory` 필드, `Start()`의 초기 기록, `HandleGameMinuteAdvanced()`의 기록 블록 **삭제**
- `Update()`의 렌더 호출을 `gameManager.DailyEquityHistory`를 읽도록 변경
- `UpdatePnLUI()`는 **손대지 않음** — 누적 수익률 유지

`SparklineRenderer`는 `List<float>`를 받으므로 시그니처를 `IReadOnlyList<float>`로 넓힙니다(내부 로직 변경 없음).

> 부수 효과로 **로드 직후 첫 점이 틀리는 문제도 해소됩니다.** 종전에는 스크립트 실행 순서가 미지정이라
> UI의 `Start()`가 `ApplyLoadedDataToGame()`보다 먼저 돌면 복원 전 잔고를 첫 점으로 박아
> 그래프가 엉뚱한 높이에서 꺾였습니다. 이제 UI가 점을 만들지 않으므로 순서에 영향받지 않습니다.

---

---

## 검증

1. 거래를 몇 번 해 그래프에 굴곡을 만든 뒤 저장 → 불러오기 → **굴곡이 그대로 복원되는지** (직선이 아닌지)
2. 24:00 정산 화면에서 **그날 그래프가 그대로 보이는지** (리셋이 이르지 않은지)
3. "다음날 진행하기" 후 그래프가 **평평한 기준선으로 리셋**되는지
4. 구버전 세이브(slot_2) 로드 시 예외 없이 기준선에서 시작하는지
5. P&L %가 **종전과 동일하게 누적 기준**으로 나오는지 (회귀 확인 — 이번에 바꾸지 않은 값)

## 영향 범위

| 파일 | 변경 |
| --- | --- |
| [GameManager.cs](../../Assets/Scripts/GameManager.cs) | 이력 소유·표본 추출·일차 리셋·저장 수집/복원 |
| [SaveData.cs](../../Assets/Scripts/System/SaveData.cs) | 필드 1개 추가 |
| [TopStatusBarUIController.cs](../../Assets/Scripts/UI/TopBar/TopStatusBarUIController.cs) | 상태 제거, 매니저 구독, 분모 교체 |
| [SparklineRenderer.cs](../../Assets/Scripts/UI/TopBar/SparklineRenderer.cs) | 파라미터 타입만 확장 |

세이브 포맷은 **추가만** 하므로 기존 세이브 호환. `SaveLoadManager`는 배선 변경 없음.
