# 세이브 커버리지 정비 + 요미 선택형 대화 ↔ 차트 힌트 통합 계획

> **작성일**: 2026-08-13
> **대상 브랜치**: `Dev_koksickbob3`
> **상태**: 계획 확정 / 착수 전
> **통합 이력**: `SaveData_Coverage_Audit.md` + `YomiRoom_ChoiceTalk_ChartHint_Plan.md` → 본 문서로 병합 (원본 2건 폐기, 내용 전량 이관)
> **재검증 이력 (2026-08-13)**: 씬 파일·빌드 세팅·씬 전환 그래프·매니저 배치까지 대조하여 **신규 결함 9건 추가** (`SV-A6`~`A8`, `SV-B11`~`B14`, `SV-C6`~`C7`). 이 중 3건은 **계획 자체의 구멍**이었다 (`SV-A6` `SV-A7` `SV-B11`)
> **관련 문서**: [ChoiceEvent_System_Refactoring_Plan.md](ChoiceEvent_System_Refactoring_Plan.md) · [Refactored_Architecture_Master.md](Refactored_Architecture_Master.md)

---

## 0. 문서 사용법

- **1~2절**: 왜 두 작업이 하나의 계획이어야 하는가 + 우선순위 전체 지도
- **3절**: 세이브 결함 전수 목록 (`SV-*` ID)
- **4절**: 요미 힌트 기능 사양
- **5절**: 단계별 실행 계획 (P0 → P4)
- **6절**: 안전장치 (`S*` ID) · 7절: 검증 · 8절: 미결정 사항 (`Q*` ID)

ID 체계는 문서 전체에서 유일하다. `SV-` = 세이브 결함, `S` = 안전장치, `Q` = 승인 필요 결정.

---

## 1. 요약

두 가지 작업을 하나로 묶는다.

1. **세이브 커버리지 정비** — 전 시스템 상태를 세이브 경로와 대조한 결과 **결함 31건**(치명 8 / 중대 14 / 경미 7 / 데드 필드 2).
2. **요미 선택형 대화 ↔ 차트 방향성 힌트** — 자유 채팅 폐지, 선택형 일상 대화 신설, 호감도로 당일 차트 거시 방향성 힌트 획득.

**왜 하나의 계획인가.** 2번 기능은 "요미의 방에서 얻은 상태"를 "GameScene의 차트"까지 실어 나른다. 그런데 조사 결과 **그 배관이 뚫려 있지 않다.**

- 요미의 방에서 올린 호감도는 **디스크에 저장되지 않는다** (`SV-B1`)
- 요미의 방에서 GameScene으로 들어가면 **새 게임이 시작되어 1일차로 리셋된다** (`SV-B10`)
- 그날의 거시 방향성은 세이브에서 복원해도 **즉시 재추첨되어 덮어써진다** (`SV-B3`)

기능 코드를 먼저 짜면 "저장 안 되는 호감도로, 재추첨되는 방향성을, 1일차로 리셋되는 차트에" 붙이게 된다. 게다가 양쪽 모두 `SaveData` 스키마를 건드리므로, **따로 진행하면 세이브 포맷을 두 번 깬다.**

**재검증에서 드러난 더 근본적인 3가지** (2026-08-13, 씬·전환 그래프 대조):

- **요미의 방으로 들어가는 문이 애초에 없다** (`SV-B11`). 씬 전환 전수 조사 결과 `YomiRoomScene`을 로드하는 코드는 `WorldMapManager.ReturnToRoom()` 하나뿐이고, 월드맵으로 가는 문 역시 요미의 방에서만 열린다. **정상 플레이에서 데이팅 파트에 도달할 수 없다.**
- **시간 슬롯이 영원히 리필되지 않는다** (`SV-A8`). `AdvanceDay()` 호출자가 0건이라 초기 5슬롯을 쓰면 데이팅 파트는 **런 전체에서 영구 종료**된다. "하루 1~2회 대화"를 전제한 힌트 설계가 성립하지 않는다.
- **매니저가 없는 씬에서 저장하면 그 매니저의 필드가 기본값으로 디스크에 박힌다** (`SV-A6`). `GameScene`에는 `DatingTimeManager`가 없으므로, 세이브를 불러와 GameScene에서 저장하는 것만으로 **호감도가 0으로 파괴된다.** 병합 전 계획의 `SV-B1`은 `gm == null` 한 방향만 봐서 이 반대 방향을 놓쳤다.

그리고 세이브 누락의 성질이 하나로 수렴한다 — **"하루 단위로 플레이어를 제약하는 카운터"가 저장되지 않아, 세이브 스컴도 아니고 그냥 불러오기만 해도 제약이 리셋된다.**

**전체 설계 원칙 3줄.**

1. 하루의 거시 방향성은 **단 한 번** 결정되고, 그 뒤엔 누구도 다시 굴리지 않는다.
2. `SaveData` 스키마는 이번 작업에서 **한 번만** 바꾼다 (P1에서 최종 형태를 확정, 배선만 단계적으로).
3. 저장 실패는 조용해서는 안 된다. 저장되지 않는 상태는 **저장하거나, 저장하지 않는 근거를 문서에 남기거나** 둘 중 하나다.

---

## 2. 우선순위 지도

| 단계 | 이름 | 포함 항목 | 상태 |
| --- | --- | --- | --- |
| **P0** | **배관 수리** — 저장이 도달하지 않는 경로 | `SV-B1` `SV-B2` `SV-B10` `SV-A6` `SV-A7` `SV-B14` | ✅ **완료 (2026-08-13)** |
| **P1** | **스키마 확정** — `SaveData` 최종 형태를 한 번에 | `SV-D1` `SV-D2` 제거 + P1~P4 신규 필드 전량 추가 | ✅ **완료** |
| **P2** | **밸런스 붕괴 차단** — 치명 누락 배선 | `SV-A1`~`SV-A5` `SV-B12` | ✅ **완료** |
| **P3** | **정합성 복구** — 중대 누락 배선 | `SV-B3`~`SV-B6` `SV-B9` | ✅ **완료** |
| **P3.5** | **데이팅 루프 성립** — P4의 하드 전제 | `SV-A8`(슬롯 리필) `SV-B11`(진입 경로) `SV-B13`(포지션 중 이탈) + `Q1` 취침 | ✅ **완료 (2026-08-13)** |
| **P4** | **신규 기능** — 선택형 대화 + 차트 힌트 | `SV-B8` + 4절 전체 → **[YomiRoom_ChoiceTalk_System_Plan.md](YomiRoom_ChoiceTalk_System_Plan.md)로 분해** | 착수 가능 |
| **P5** | **후속** — 승인 대기 / 여유 시 | `SV-B7`(`Q1`) `SV-C1`~`SV-C7` | 별도 승인 |

### 2.1. P0~P3 구현 기록 (2026-08-13)

| 항목 | 반영 위치 |
| --- | --- |
| `SV-B1` `SV-A6` | `SaveLoadManager.SaveGame()`을 **`CurrentData` 베이스 + 매니저별 개별 가드** 구조로 재작성. 일괄 `return false` 가드 해체. 인벤토리 목록은 누적 방지를 위해 `Clear()` 후 채움 |
| `SV-A7` | 저장 성공 시 `CurrentData = data` 갱신. `ApplyLoadedDataToGame()`의 `CurrentData = null` 제거 |
| `SV-B14` | `WriteSaveFile()` 신설 — 임시 파일 → `File.Replace`(백업 동반) 원자적 교체 + `try/catch`. 베이스 부재 시 디스크를 읽는 `ReadSaveFile()` 추가 |
| `SV-B2` | `WorldMapManager`의 알바 보상·데이트 비용 폴백 직후 `SaveCurrentGame()` 호출 |
| `SV-B10` | `YomiRoomManager.StartTrading()`에서 `SaveCurrentGame()` → `PrepareLoadGame()` 순서로 호출. `MoveToWorldMap()`도 저장 후 이동 |
| `SV-D1` `SV-D2` | `SaveData.CurrentEmotion` / `CurrentSignalPhase` 제거 + 제거 사유 주석 |
| P1 신규 필드 | `SaveData`에 22개 추가 (중독·이벤트 스케줄·이벤트 계약·엔진 시간축·정산 문맥·Outlook·요미 대화) |
| 마이그레이션 | `SaveDataMigrator`에 1.6.0 게이트 추가. 백필은 불필요하나 **0으로 역직렬화되면 "1일차에 결정됨"으로 오해되는 일차 필드만** -1로 보정 |
| `SV-A1`~`A3` | `TraderStatus.CaptureSaveData()` / `RestoreFromSaveData()` 신설. **기존 리플렉션 6줄을 걷어내고** 여기로 흡수. 부수 효과로 `wasLoaded` 플래그가 처음으로 실제 동작하게 되어, `Start()`의 `ResetStatus()`가 복원값을 덮어쓸 위험도 함께 닫힘 |
| `SV-A4` | `ChoiceEventController.CaptureSaveData()` / `RestoreFromSaveData()` 신설 |
| `SV-A5` | `TradingController.CaptureSaveData()` 신설 + `RestorePosition()`이 **포지션 유무와 무관하게** 이벤트 계약을 복원하도록 변경(포지션이 없으면 꺼진 상태로 확정). 복원 불가능한 `eventProtectionEndTime`은 명시적으로 해제 |
| `SV-B3`~`B5` | 엔진의 `CaptureSaveData`/`RestoreFromSaveData`에 `lastUpdatedDay`·`minutesUntilNextRegimeChange`·`currentTotalMinutes` 추가 |
| `SV-B6` `SV-B9` | `GameManager.CaptureSettlementContext()` / `RestoreSettlementContext()` 신설 |
| `SV-B12` | `DatingTimeManager.ModifyAffection()`에 `Mathf.Clamp(0, 100)` 적용 |
| 검증 도구 | `Assets/Editor/SaveRoundTripTester.cs` — 메뉴 `FXOverdose/Debug/세이브 왕복(Round-trip) 검사` |

**미구현으로 남긴 것**: `S17`의 저장 디바운스. 원자적 쓰기를 넣었고 데이팅 대화 1회당 저장은 4회 수준이라 현재는 불필요하다. 저장 빈도가 체감될 때 추가한다.

> **P3.5는 재검증에서 신설됐다.** 슬롯이 리필되지 않고 방으로 들어가는 문도 없는 상태에서는 P4를 만들어도 **플레이어가 기능에 접근할 수 없다.** 이전 판의 우선순위는 이 층을 통째로 빠뜨리고 있었다.

**P0~P3가 닫히기 전에는 P4를 시작하지 않는다.** P4의 모든 검증이 P0~P3의 정상 동작을 전제하기 때문이다.

우선순위 판단 기준: ① 다른 작업의 검증을 막는가 → P0 ② 스키마를 깨는가 → P1로 모음 ③ 플레이어가 이득을 보는 방향으로 무너지는가 → P2 ④ 값이 거짓이 되는가 → P3.

---

## 3. 세이브 결함 전수 목록

### 3.1. 검사 방법

- `SaveData`의 전 필드를 `SaveGame()` 수집부([SaveLoadManager.cs:92-177](../../Assets/Scripts/System/SaveLoadManager.cs#L92-L177))와 `ApplyLoadedDataToGame()` 주입부([:314-451](../../Assets/Scripts/System/SaveLoadManager.cs#L314-L451))에서 역추적 → **선언만 되고 쓰이지 않는 데드 필드** 색출
- 싱글턴/매니저 16개 + `TraderStatus` / `TradingController` / `MarketSimulationEngine` / `ChoiceEventController` / `Inventory`의 인스턴스 필드를 전수 열거 → **런타임에 변하는데 저장 대상이 아닌 값** 색출
- 각 누락에 대해 "로드 직후 어떤 일이 벌어지는가"를 판정하여 등급 부여

### 3.2. 등급 A — 치명 (밸런스/진행 붕괴) → **P2**

| ID | 시스템 | 누락 상태 | 위치 | 로드 후 증상 |
| --- | --- | --- | --- | --- |
| **SV-A1** | `TraderStatus` | `isLeverageAddicted`, `consecutiveHighLevWins`, `consecutiveLowLevTrades` | [TraderStatus.cs:37-39](../../Assets/Scripts/TraderStatus.cs#L37-L39) | **고배율 중독 상태가 해제된다.** 중독 페널티를 불러오기 한 번으로 무효화 |
| **SV-A2** | `TraderStatus` | `currentLosingStreak` | [:36](../../Assets/Scripts/TraderStatus.cs#L36) | 연속 손절 카운터가 0으로. 연패 기반 멘탈 페널티/트라우마 회피 |
| **SV-A3** | `TraderStatus` | `canRegenMental` | [:41](../../Assets/Scripts/TraderStatus.cs#L41) | 체력 30% 이하로 잠긴 멘탈 자연회복이 로드 즉시 풀림 |
| **SV-A4** | `ChoiceEventController` | `eventsTriggeredToday`, `lowMentalEventsTriggeredToday`, `lastTriggerDay`, `nextRandomTriggerMinuteOfDay`, `lastEventTriggerGameMinutes` | [ChoiceEventController.cs:77-83](../../Assets/Scripts/Events/ChoiceEventController.cs#L77-L83) | **하루 2회 이벤트 상한이 리셋된다.** 저장/로드 반복으로 무한 이벤트 파밍 |
| **SV-A5** | `TradingController` | `isEventTradeActive`, `currentEventHandlingMode`, `eventTargetROELimit`, `eventStopLossROELimit`, `isEventPlayerChoice`, `isEventTrueSignal` | [TradingController.cs:317-323](../../Assets/Scripts/Trading/TradingController.cs#L317-L323) | 이벤트 강제 포지션이 **일반 포지션으로 둔갑**. `RestorePosition`([:268](../../Assets/Scripts/Trading/TradingController.cs#L268))은 방향·증거금·레버리지만 복구하고 이벤트 계약을 복구하지 않음 |

| **SV-A6** | `SaveLoadManager` | **부재 매니저의 필드가 기본값으로 덮어써짐** | [SaveLoadManager.cs:92](../../Assets/Scripts/System/SaveLoadManager.cs#L92), [:171](../../Assets/Scripts/System/SaveLoadManager.cs#L171) | `SaveGame()`이 매번 `new SaveData`를 만들고 `DatingTimeManager.Instance?.SaveToData(data)`처럼 **null 조건 호출**을 한다. 매니저가 없으면 그 필드는 **`SaveData` 기본값 그대로 디스크에 기록된다.** `DatingTimeManager`는 `YomiRoomScene`/`WorldMapScene`에만 존재하고 **`GameScene`에는 없다**(씬 GUID 대조 확인). ⇒ **세이브를 불러와 GameScene에서 저장하는 것만으로 호감도·체력·슬롯이 0/기본값으로 파괴된다** |
| **SV-A7** | `SaveLoadManager` | `SaveGame()`이 `CurrentData`를 갱신하지 않음 | [SaveLoadManager.cs:179-183](../../Assets/Scripts/System/SaveLoadManager.cs#L179-L183) | JSON을 쓰고 바로 `return`한다. **P0에서 `CurrentData`를 부분 저장의 베이스로 삼는 순간, 베이스가 "마지막 로드 시점"에 고정되어** 이후 트레이딩 진행이 통째로 롤백된다. `CurrentData = data;` 한 줄이 반드시 필요 |
| **SV-A8** | `DatingTimeManager` | **시간 슬롯이 영구히 리필되지 않음** | [DatingTimeManager.cs:163-172](../../Assets/Scripts/DatingSim/Core/DatingTimeManager.cs#L163-L172) | `AdvanceDay()` 호출자 0건 → `currentTimeSlot`을 되돌리는 코드가 **어디에도 없다.** 초기 5슬롯 소진 후 데이팅 파트는 런 전체에서 영구 종료. **"하루 1~2회 대화"를 전제한 힌트 설계가 성립하지 않는다** |

> `liquidationPrice`는 `RestorePosition`에서 재계산하므로 누락이 아니다 ([:280-288](../../Assets/Scripts/Trading/TradingController.cs#L280-L288)).
>
> `SV-A6`과 `SV-B1`은 같은 뿌리의 양방향이다. **원칙을 "`gm`이 없으면 트레이딩 블록을 건너뛴다"가 아니라 "부재한 매니저의 필드는 어떤 경우에도 덮어쓰지 않는다"로 일반화해야 한다** (`S16`).

### 3.3. 등급 B — 중대 → **P0 / P3 / P5**

| ID | 단계 | 시스템 | 누락 상태 | 위치 | 증상 |
| --- | --- | --- | --- | --- | --- |
| **SV-B1** | **P0** | `SaveLoadManager` | **저장 경로 자체** — `GameManager` 부재 시 저장 전면 실패 | [SaveLoadManager.cs:73-77](../../Assets/Scripts/System/SaveLoadManager.cs#L73-L77) | **요미의 방/월드맵에서 일어난 모든 변화가 디스크에 닿지 못한다.** `DatingTimeManager`의 저장 호출 7개 전부 무효 |
| **SV-B2** | **P0** | `WorldMapManager` | 알바 보상 폴백이 `CurrentData`에만 기록됨 | [WorldMapManager.cs:82-85](../../Assets/Scripts/DatingSim/WorldMap/WorldMapManager.cs#L82-L85) | `SaveGame()`이 `SaveData`를 씬에서 새로 조립하므로 **보상이 증발한다** |
| **SV-B10** | **P0** | `YomiRoomManager` | GameScene 재진입 시 로드 플래그 미설정 | [YomiRoomManager.cs:134-140](../../Assets/Scripts/DatingSim/YomiRoom/YomiRoomManager.cs#L134-L140) | `IsPendingLoad`가 false → `GameManager.Start()`가 **`StartNewGame()` 호출** ([GameManager.cs:146](../../Assets/Scripts/GameManager.cs#L146)) → 잔고 초기화, `currentDay = 1` |
| **SV-B3** | **P3** | `MarketSimulationEngine` | `lastUpdatedDay` | [MarketSimulationEngine.cs:37](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L37) | 복원된 `currentDailyRegime`이 첫 프레임에 **재추첨되어 덮어써진다** (힌트 기능의 직접 전제) |
| **SV-B4** | **P3** | `MarketSimulationEngine` | `minutesUntilNextRegimeChange` | [:40](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L40) | 로드 시 60으로 리셋. 이벤트 빔이 잡아 둔 국면 유지 시간이 풀림 |
| **SV-B5** | **P3** | `MarketSimulationEngine` | `currentTotalMinutes` | [:119](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L119) | 캔들 타임스탬프 기준점이 0으로 리셋 → 복원된 캔들과 신규 캔들의 시간축 불연속 |
| **SV-B6** | **P3** | `GameManager` | `TodayRegularDeduction`, `TodayRegularDeductionReason` | [GameManager.cs:59-60](../../Assets/Scripts/GameManager.cs#L59-L60) | 정산 창의 지출 사유가 사라짐 ([DailySettlementUIController.cs:272](../../Assets/Scripts/UI/DailySettlementUIController.cs#L272)) |
| **SV-B9** | **P3** | `GameManager` | `isSettlementProcessing` | [GameManager.cs:61](../../Assets/Scripts/GameManager.cs#L61) | 정산 진행 중 저장 → 로드 시 정산 창이 뜨지 않고 24:00에 멈춤 (재현 조건 좁음) |
| **SV-B8** | **P4** | 차트 방향성 | `DailyMarketOutlook` (미존재) | — | 요미 힌트 기능의 전제. 4.4절 |
| **SV-B7** | **P5** | `DatingTimeManager` | `StoryProgressStage`, `DatingDay` — **필드는 있으나 갱신 주체가 없음** | [DatingTimeManager.cs:163](../../Assets/Scripts/DatingSim/Core/DatingTimeManager.cs#L163) | `AdvanceDay()` 호출자 0건. 항상 1일차. 일차 카운터 이원화 (`Q1` 승인 필요). **같은 원인의 슬롯 미리필은 심각도가 달라 `SV-A8`로 분리** |
| **SV-B11** | **P3.5** | 씬 전환 | **`GameScene` → 요미의 방 진입 경로 부재** | [YomiRoomManager.cs:126-140](../../Assets/Scripts/DatingSim/YomiRoom/YomiRoomManager.cs#L126-L140), [WorldMapManager.cs:137-141](../../Assets/Scripts/DatingSim/WorldMap/WorldMapManager.cs#L137-L141) | 전환 그래프 전수 조사: Title→tutorial/GameScene, tutorial→GameScene, YomiRoom→WorldMap/GameScene, WorldMap→YomiRoom. **`YomiRoomScene`을 로드하는 코드는 `ReturnToRoom()` 하나뿐** → 정상 플레이에서 데이팅 파트에 **도달 불가**. 씬 파일과 빌드 세팅은 정상(둘 다 존재·enabled) (`Q5`) |
| **SV-B12** | **P2** | `DatingTimeManager` | 호감도에 **상한이 없음** | [DatingTimeManager.cs:138-143](../../Assets/Scripts/DatingSim/Core/DatingTimeManager.cs#L138-L143) | `ModifyObsession`은 0~100 클램프가 있는데 `ModifyAffection`은 없다. UI는 `clamped / 100f`로 게이지를 그린다([YomiRoomTopDownPrototype.cs:342-343](../../Assets/Scripts/DatingSim/YomiRoom/YomiRoomTopDownPrototype.cs#L342-L343)). 힌트 티어는 **당일 획득량** 기준이라 즉시 악용은 아니지만, 누적 호감도 기반 콘텐츠가 붙는 순간 터진다 |
| **SV-B13** | **P3.5** | `TradingController` / 씬 전환 | **포지션 보유 중 씬 이탈 = 시간 정지** | — | 엔진과 컨트롤러가 GameScene에 있으므로 요미의 방으로 나가면 시세가 멈춘다. 포지션은 세이브로 보존되고 복귀 시 그대로 복원된다 → `SV-B11` 진입 경로를 만드는 순간 **손실 포지션을 들고 도망쳐 무기한 청산 회피**가 가능해진다 (`S18`) |
| **SV-B14** | **P0** | `SaveLoadManager` | 저장에 **예외 처리도 원자성도 없음** | [SaveLoadManager.cs:180](../../Assets/Scripts/System/SaveLoadManager.cs#L180) | `File.WriteAllText` 직접 호출, `try/catch` 없음(`PrepareLoadGame`에는 있다). P0 이후 `DatingTimeManager`가 **호감도·슬롯 변경마다 저장을 호출**하므로 쓰기 빈도가 급증 → 쓰기 도중 종료 시 세이브 파일 손상 (`S17`) |

### 3.4. 등급 C — 경미 (연출/표시 계열) → **P5**

| ID | 시스템 | 누락 상태 | 증상 |
| --- | --- | --- | --- |
| **SV-C1** | `TraderStatus` | `healthDropMentalDrainAccumulator`, `healthDropMentalDrainTimer` | 체력 저하 멘탈 누수 진행분이 리셋. 플레이어에게 유리한 방향이라 시급하지 않음 |
| **SV-C2** | `TradingController` | `lastMarginAmount`, `lastClosedPosition`, `lastClosedPrice` | 직전 매매 참조 대사/UI가 초기 상태로 |
| **SV-C3** | `TraderMemoryManager` | 저장은 되나 **리플렉션 3회**로 private 필드를 긁음 ([SaveLoadManager.cs:191-229](../../Assets/Scripts/System/SaveLoadManager.cs#L191-L229)) | 필드명 변경 시 **컴파일 에러 없이 조용히 저장 실패**. 코드 주석에도 이미 "추후 GetData() 추가 권장"이라 적혀 있음 |
| **SV-C4** | `BossManager` | 보스 격파 이력 | 현재 보스는 일차로 결정되므로 실피해 없음. 향후 다회차 요소 추가 시 필요 |
| **SV-C5** | `AchievementManager` | `PlayerPrefs` 전역 저장 (슬롯 무관) | **의도된 설계**로 판단 (업적은 계정 단위). 단 "슬롯 삭제해도 업적 카운터 잔존"은 문서화 필요 |
| **SV-C6** | `SaveLoadManager` | **`TitleScene`에만 배치됨** (씬 GUID 대조 확인). `DontDestroyOnLoad`로 전파 | 에디터에서 `YomiRoomScene` / `YomiRoom_Test`를 직접 재생하면 `Instance == null` → 저장 전무, `CurrentData` 없음. **4.10절의 힌트 일차 조회(`CurrentData.CurrentDay`)가 NRE로 터진다.** 테스트 씬 직접 재생이 실제 개발 워크플로우이므로 null 가드 필수 (`S19`) |
| **SV-C7** | `DatingTimeManager` | `YomiRoomScene`과 `WorldMapScene` **양쪽에 배치** | 중복 인스턴스는 `Destroy`로 처리되어 동작은 하지만, **인스펙터 초기값(스태미나·슬롯)이 어느 쪽이 이기는지 씬 진입 순서에 의존**한다. 초기값 진실 원천을 한쪽으로 정리 |

### 3.5. 등급 D — 데드 필드 → **P1**

| ID | 필드 | 실태 | 처리 |
| --- | --- | --- | --- |
| **SV-D1** | `SaveData.CurrentEmotion` [:84](../../Assets/Scripts/System/SaveData.cs#L84) | 수집부에도 주입부에도 **등장하지 않음**. 순수 데드 | **삭제**. 감정은 매 순간 재계산되므로 복원 의미 없음 |
| **SV-D2** | `SaveData.CurrentSignalPhase` [:67](../../Assets/Scripts/System/SaveData.cs#L67) | 저장은 되지만([MarketSimulationEngine.cs:176](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L176)) 복원 시 **무조건 `None`으로 덮어씀**([:206](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L206)) | **삭제**. `activeSignal` 객체가 직렬화 불가라 복원이 애초에 불가능 |

### 3.6. 저장하지 않기로 명시하는 것 (검토 완료, 조치 없음)

나중에 "이건 왜 빠졌지?"를 반복하지 않기 위해 근거를 남긴다.

| 대상 | 제외 근거 |
| --- | --- |
| `Time.time` 기반 쿨다운 — `playerTradeCooldownEndTime`, `eventProtectionEndTime`, `lastOverdoseTradeTime`, `lastMentalTriggerTime` | 세션 시작 시 `Time.time`이 0으로 리셋되므로 절대값 보존이 무의미. 로드 시 쿨다운 해제가 정상 동작 |
| 오버도즈 상태 — `isOverdoseTradeActive`, `overdoseProtectionEndTime` | **오버도즈 중에는 저장 자체가 차단됨** ([SaveLoadManager.cs:86-90](../../Assets/Scripts/System/SaveLoadManager.cs#L86-L90)) |
| 고속 시간 패스 — `remainingFastForwardMinutes`, `preservedFastForwardMinutes` | 스킬 업그레이드 연출 중의 일시 상태. 중간 저장 경로 없음 |
| `AITradingBrain` 전체 (`isProcessingSignal`, `currentActiveSignal`, `lastDecisionLog`) | 신호는 로드 시 `None`으로 리셋되는 것이 설계. 개장 후 3분 내 재발행 |
| `MarketSimulationEngine`의 난이도 스칼라 (`dayVolatilityMultiplier`, `tickInstability`, `fakeoutProbability`, `slippageRange`) | **일차로부터 결정되는 파생값** ([:371-409](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L371-L409)). 저장하면 이중 진실 원천이 됨 |
| `ouCenterPrice` | `RestoreFromSaveData`에서 현재가로 재설정([:198](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L198)). 의도된 동작 |
| `ShopManager.pausedByShop`, UI 패널 개폐 상태 | 표시 상태 |
| `AudioManager` / `SystemSettingsManager` | `PlayerPrefs` 전역 설정이 맞다. 슬롯별 저장 대상 아님 |
| `TutorialManager` 튜토리얼 중간 진행도 | 별도 씬이며 완료 플래그(`IsTutorialCompleted`)만으로 충분 |
| `ActiveItemEffectManager`, `ShopManager`, `Inventory`, `CostumeManager`, `DeliveryFoodManager`, `WorldMapManager` | 전수 검사 결과 **누락 없음**. 보유 상태가 전부 세이브에 반영됨 |

### 3.7. 재검증했으나 문제없음 (재조사 방지용 기록)

| 대상 | 확인 결과 |
| --- | --- |
| `YomiRoomScene` / `WorldMapScene` 존재 및 빌드 세팅 | **정상.** 두 씬 모두 존재하고 `EditorBuildSettings`에 `enabled: 1`로 등록됨 |
| 각 씬의 매니저 배치 | `YomiRoomScene`: `YomiRoomManager` + `DatingTimeManager` / `WorldMapScene`: `WorldMapManager` + `DatingTimeManager` — 정상 (단 `SV-C7` 참고) |
| `LoadingScreenController`의 비트레이딩 씬 처리 | **정상.** `GameScene`/`tutorial`만 차트 준비를 대기하고 그 외는 UI 준비로 분기함 ([LoadingScreenController.cs:99-112](../../Assets/Scripts/UI/LoadingScreenController.cs#L99-L112)) |
| `ChoiceEventController`의 LLM 프리페치 중 씬 이탈 | **정상.** `OnDestroy`에서 `preFetchCts.Cancel()` + `Dispose` 처리됨 ([ChoiceEventController.cs:114-140](../../Assets/Scripts/Events/ChoiceEventController.cs#L114-L140)) |
| 요미의 방 UI 자산 충돌 위험 | **없음.** 방 UI는 `YomiRoomTopDownPrototype.Build(scene)`가 **런타임에 조립**한다(`if (!Application.isPlaying) return;`). 씬/프리팹에 직렬화되지 않으므로 P4-d의 UI 작업은 **코드 수정만으로 완결**되고 빌더 재실행에 덮어써질 자산이 없다 |
| `YomiRoom_Test.unity` vs `YomiRoomScene.unity` 이중화 | 양쪽 모두 같은 런타임 빌더를 호출하므로 코드 수정이 자동 반영됨. 빌드 대상은 `YomiRoomScene` 하나 |

---

## 4. 요미 선택형 대화 ↔ 차트 힌트 사양 (P4)

### 4.1. 차트 거시 방향성 현황 — 요청사항 1번 답변

| 항목 | 심볼 | 위치 | 접근성 |
| --- | --- | --- | --- |
| 일일 거시 국면 (**이것이 "거시적 방향성"**) | `currentDailyRegime` | [MarketSimulationEngine.cs:36](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L36) | **private, 접근자 없음** |
| 단기 국면 (30분~2시간 단위) | `currentRegime` / `CurrentRegime` | [:33](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L33), [:133](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L133) | public |
| 일일 국면 추첨 | `DetermineDailyRegime()` | [:411](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L411) | private |
| 추첨 트리거 | `UpdateDailyDifficulty(int currentDay)` | [:371](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L371) | public, [:325](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L325)에서 매 프레임 호출 |
| 일일 국면 → 가격 반영 | `macroDrift` ±0.00015 | [:472-474](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L472-L474) | — |
| 일일 국면 → 단기 국면 편향 | `SwitchToRandomRegime()` | [:875-909](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L875-L909) | private |
| 방향성 무시 조건 | `IsOverridingTrend` | [:67](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L67) | public |

**결론: 새로 만들 필요 없다.** `currentDailyRegime`이 그대로 "오늘의 거시 방향성"이다. 필요한 건 ① public 접근자 ② 씬 밖에서도 읽히는 저장소 ③ 재추첨 금지 장치.

`MarketRegime` 4종의 의미 ([:10-16](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L10-L16)):

| Regime | 드리프트 | 힌트 표현 |
| --- | --- | --- |
| `Bull` | +0.00015 | "위로 갈 것 같아" |
| `Bear` | -0.00015 | "아래로 갈 것 같아" |
| `Sideways` | 0 | "오늘은 지루할 거야" |
| `Squeeze` | **랜덤 ±0.0003** | **방향 힌트 불가** — 변동성 경고만 (`S4`) |

일일 국면이 단기 국면을 편향시키는 강도는 **60% / 20% / 10% / 10%** ([:879-905](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L879-L905)). 즉 `Bull` 날에도 10%는 `Bear` 구간이 나온다. **힌트는 "확정"이 아니라 "경향"이며, 대사도 그렇게 써야 한다.**

### 4.2. 자유 채팅 현황 — 요청사항 2번 대상

| 구성 요소 | 위치 | 처리 |
| --- | --- | --- |
| 입력 필드 + 전송 버튼 + 말풍선 로그 | `YomiRoomDialogueUI` [YomiRoomTopDownPrototype.cs:403-484](../../Assets/Scripts/DatingSim/YomiRoom/YomiRoomTopDownPrototype.cs#L403-L484) | **말풍선 로그는 유지, 입력 행만 교체** |
| 텍스트 응답 공급자 | `IYomiDialogueProvider` / `PlaceholderDialogueProvider` | **삭제** (구현체 1개짜리 인터페이스, 선택형에선 시그니처가 안 맞음) |
| 매니저 측 채팅 경로 | `ProcessUserChatInput` [YomiRoomManager.cs:80-99](../../Assets/Scripts/DatingSim/YomiRoom/YomiRoomManager.cs#L80-L99) | **삭제 후 선택형 API로 대체** |
| 상태 | `YomiRoomState.Chatting` / `Responding` | **유지** (선택형에도 그대로 쓴다) |

> ⚠️ `CLAUDE.md`와 [DatingSim_FreeChat_Removal_Plan.md](../P2_03_LLM_Architecture/DatingSim_FreeChat_Removal_Plan.md)에는 "채팅 UI는 절대 삭제하지 말 것"이라고 적혀 있다. 이 계획이 그 지시를 **의도적으로 뒤집는다.** 착수와 동시에 두 문서를 갱신해야 한다 (`Q4`, 5.6절).

### 4.3. 호감도 및 일차 현황

**호감도**: `DatingTimeManager`([DatingTimeManager.cs](../../Assets/Scripts/DatingSim/Core/DatingTimeManager.cs))가 이미 전부 갖고 있다. `DontDestroyOnLoad`, `CurrentAffection`, `ModifyAffection(int)`([:138](../../Assets/Scripts/DatingSim/Core/DatingTimeManager.cs#L138)), `OnAffectionChanged` 이벤트, `SaveData.DatingAffection` 연동([:75](../../Assets/Scripts/DatingSim/Core/DatingTimeManager.cs#L75), [:94](../../Assets/Scripts/DatingSim/Core/DatingTimeManager.cs#L94)). **새 매니저를 만들지 않는다.**

현재 채팅은 호감도도 시간 슬롯도 소모/지급하지 않는다 ([YomiRoomManager.cs:75](../../Assets/Scripts/DatingSim/YomiRoom/YomiRoomManager.cs#L75), [:94](../../Assets/Scripts/DatingSim/YomiRoom/YomiRoomManager.cs#L94)의 `TODO(P2)`). 자리 표시자 응답을 파밍하지 못하게 막아 둔 것이며, **이번 작업에서 되돌린다.**

**일차 소유권 — 카운터가 두 개다:**

| 카운터 | 소유자 | 증가 지점 | 세이브 필드 |
| --- | --- | --- | --- |
| 트레이딩 일차 | `GameManager.currentDay` | [GameManager.cs:653](../../Assets/Scripts/GameManager.cs#L653) (24:00 정산) | `SaveData.CurrentDay` |
| 데이팅 일차 | `DatingTimeManager.currentDay` | `AdvanceDay()` [:163](../../Assets/Scripts/DatingSim/Core/DatingTimeManager.cs#L163) — **호출자 0건** | `SaveData.DatingDay` |

`DatingDay`는 증가한 적이 없으므로 항상 1이다. **힌트의 "해당 일차"는 `GameManager.CurrentDay` 하나로 통일한다.** `DatingTimeManager.currentDay`는 이 통일된 값의 읽기 전용 미러로 격하시킨다 (`SV-B7` / `Q1`).

> ⚠️ `AdvanceDay()`가 불리지 않는다는 사실의 **더 큰 결과는 일차가 아니라 시간 슬롯**이다. `currentTimeSlot`을 되돌리는 코드가 이 메서드 안에만 있어서, 초기 5슬롯을 쓰면 데이팅 파트가 **런 전체에서 영구 종료**된다 (`SV-A8`). "대화 1회 = 슬롯 1"을 전제한 4.6절 수치는 슬롯 리필이 배선된 뒤에야 의미를 갖는다.

### 4.4. `DailyMarketOutlook` — 하루 방향성의 단일 진실 원천 (`SV-B8`)

**핵심 규칙: 하루의 거시 방향성은 딱 한 번 결정되고, 그 뒤엔 누구도 다시 굴리지 않는다.**

```csharp
// Assets/Scripts/Trading/DailyMarketOutlook.cs  (신규, 약 60줄)
namespace FXOverdose.Trading
{
    /// <summary>그날의 거시 방향성. 씬과 무관하게 SaveData를 통해 유지된다.</summary>
    public static class DailyMarketOutlook
    {
        public static int Day { get; private set; } = -1;
        public static MarketSimulationEngine.MarketRegime Regime { get; private set; }
        public static bool Revealed { get; private set; }   // 요미가 힌트로 알려줬는가 (= 차트 편향 강화 조건)

        /// <summary>해당 일차의 방향성을 얻는다. 미결정이면 이 자리에서 결정하고 세이브에 기록한다.</summary>
        public static MarketSimulationEngine.MarketRegime GetOrRoll(int day);

        /// <summary>요미가 힌트를 발화한 뒤 잠근다. 잠긴 뒤에는 재추첨되지 않는다.</summary>
        public static void MarkRevealed(int day);

        /// <summary>일차가 바뀌면 무효화한다. GetOrRoll 내부에서 자동 처리.</summary>
        public static void InvalidateIfStale(int day);

        internal static void Load(SaveData data);
        internal static void Capture(SaveData data);
    }
}
```

`static`을 쓰는 이유: 씬 전환에 살아남아야 하는데 `MonoBehaviour` 싱글턴을 새로 만들면 각 씬에 프리팹을 배치해야 한다. 상태는 `int` 2개 + `bool` 1개이고 진실 원천은 세이브다. **오브젝트를 만들 이유가 없다.**

**`MarketSimulationEngine` 개조 — 추첨자에서 소비자로:**

| 대상 | 변경 |
| --- | --- |
| [:133](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L133) 부근 | `public MarketRegime CurrentDailyRegime => currentDailyRegime;` 추가 (요청사항 1번의 실제 산출물) |
| [:411](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L411) `DetermineDailyRegime()` | 본문을 `currentDailyRegime = DailyMarketOutlook.GetOrRoll(day);`로 교체. **자체 `Random.value` 추첨 제거.** 추첨 확률표(35/25/25/15)는 `DailyMarketOutlook`으로 이관 |
| [:371](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L371) `UpdateDailyDifficulty` | `DetermineDailyRegime(currentDay)`로 일차 전달 |
| [:875](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L875) `SwitchToRandomRegime()` | `DailyMarketOutlook.Revealed`면 역방향 분기 제거 (`Q2`) |
| [:194](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L194) `RestoreFromSaveData` | `lastUpdatedDay = data.CurrentDay;` 추가 (`SV-B3` 이중 방어) |

이 개조 후 **엔진은 방향성을 만들지 않는다.** 어느 씬에서 먼저 물어보든 같은 답이 나온다.

### 4.5. 대화 흐름

```
[요미의 방] 자유대화 버튼
        │
        ▼
[TalkTopic 1개 선택]  ── 오늘 미사용 토픽 풀에서 무작위
        │
        ▼
[노드 1] 요미 대사 + 선택지 2~4개 ──► 선택 ──► 호감도 +0~3
        │
        ▼
[노드 2] ...                       ──► 선택 ──► 호감도 +0~3
        │
        ▼ (노드 2~4개, 토픽마다 다름)
[토픽 종료]  gainToday 누계
        │
        ├─ gainToday < 4  ──► 평범한 마무리 대사, 힌트 없음
        └─ gainToday ≥ 4  ──► 힌트 티어 판정 ──► DailyMarketOutlook 조회/생성
                                    │
                                    ▼
                            [힌트 대사 1줄 출력] + Outlook.Revealed = true (잠금)
                                    │
                                    ▼
[GameScene 진입 / 당일 차트 개장] ──► 엔진이 Outlook을 "소비" (재추첨 금지)
                                    │
                                    ▼
[다음 날 진입] ──► Outlook.Day != CurrentDay ──► 무효 ──► 새 날 기준으로 재생성
```

### 4.6. 호감도 · 힌트 티어 수치

| 항목 | 값 | 근거 |
| --- | --- | --- |
| 대화 1회 비용 | **시간 슬롯 1** | 무한 파밍 차단 (`S1`). 하루 5슬롯 중 최대 2회 대화 상정 |
| 토픽당 선택 노드 수 | **2~4개** | 요청사항 4번 |
| 선택지당 호감도 | **+0 / +1 / +2 / +3** | 오답은 0, 마이너스는 주지 않음 (`S9`) |
| 토픽 1회 최대 획득 | +8 (4노드 × +2 평균) | — |
| 힌트 없음 | `gainToday < 4` | — |
| **티어 1** (모호) | `gainToday 4~6` | 변동성/분위기만 |
| **티어 2** (명시) | `gainToday ≥ 7` | 방향 명시 |
| 힌트 발급 횟수 | **하루 1회** | 티어는 최초 발급 시점에 고정, 이후 대화로 승급 불가 (`S3`) |

`gainToday`는 **당일 대화에서 획득한 호감도 합계**다. 누적 호감도가 아니다. "이미 호감도 200이니까 매일 자동으로 티어 2"를 막는다.

### 4.7. 힌트 대사 — 요청사항 7번

`DailyMarketOutlook.Regime × 힌트 티어` 조합별로 대사 풀을 둔다.

| Regime | 티어 1 (모호 · 자칭 `나`) | 티어 2 (명시 · 자칭 `요미`) |
| --- | --- | --- |
| `Bull` | "나 오늘 왠지 기분 좋은데? 이유는 몰라~" | "오빠, 오늘은 위야! 요미 감각 믿어!" |
| `Bear` | "음... 나 오늘은 조심하는 게 좋을 것 같아" | "오늘 떨어져! 오빠 욕심부리면 요미가 화낼 거야!" |
| `Sideways` | "오늘은 아무 일도 없을 것 같은데... 나만 그런가?" | "오늘 완전 지루할 거야. 요미 말 믿고 쉬어!" |
| `Squeeze` | "나 심장이 두근거려... 왜 이러지?" | **방향 대신 변동성 경고** — "오빠, 요미 무서워... 오늘 엄청 흔들릴 거야!" |

위 8줄은 **각 칸의 견본**이다. 티어 1은 자기도 이유를 모르는 예감이라 `나`로 흘리고, 티어 2는 자기 감각을 내세우는 단언이라 `요미`로 못 박는다. 나머지 32줄도 이 대비를 지킨다.

각 칸에 **최소 5줄**을 채워 8 × 5 = **40줄 이상**. 무작위 선택하되 직전 사용분 1개는 제외.

**대사 규격** — [P2_02_Yomi_Character_Bible.md](../P2_02_Worldbuilding/P2_02_Yomi_Character_Bible.md)를 따른다.

| 항목 | 값 |
| --- | --- |
| 호칭 / 말투 | **"오빠" / 반말** (바이블 4.1~4.3절) |
| 자칭 | 티어 1(모호·혼잣말)은 `나`, 티어 2(단언·자기 감각 어필)는 `요미` (바이블 4.2절 감정 온도 규칙) |
| 길이 | **60자 이내** — 데이팅 일상 말풍선 표면 |
| 톤 | **차트 인격이 아니다.** 힌트는 요미가 방에서 흘리는 예감이지 매매 지시가 아니므로, "가즈아" 계열 도박꾼 어휘를 쓰지 않는다 |
| 확신도 | 티어 1은 자기도 이유를 모르는 감각, 티어 2는 단언. **어느 쪽도 "확정"이라고 말하지 않는다** (`S5`) |

> 초판에 쓰여 있던 "마스터 + 존댓말"은 게임에 이미 들어간 요미 대사 880줄과 정면으로 충돌해 폐기했다. 이 충돌은 코드 전반에서 **2026-08-13 일괄 정리 완료**됐다 (바이블 7.1절).

**작성 후 반드시** `python yomi_dialogue_lint.py` 를 돌린다. `YomiTalkTopics.cs`를 만들면 린터의 `CS_SOURCES` 목록에 경로를 추가해야 검사 범위에 들어간다.

### 4.8. 대화 데이터 구조 — 요청사항 4번

```csharp
// Assets/Scripts/DatingSim/Dialogue/YomiTalkTopics.cs  (신규)
namespace FXOverdose.DatingSim.Dialogue
{
    public readonly struct TalkChoice   { public readonly string Text; public readonly int Affection; }
    public readonly struct TalkNode     { public readonly string YomiLine; public readonly TalkChoice[] Choices; }
    public readonly struct TalkTopic    { public readonly string Id; public readonly string Title; public readonly TalkNode[] Nodes; }

    public static class YomiTalkTopics
    {
        public static readonly TalkTopic[] All = { /* 토픽 정의 */ };
        public static readonly string[][] HintLines = { /* [Regime, Tier] 별 대사 풀 */ };
    }
}
```

**ScriptableObject 대신 C# 정적 테이블을 쓴다** (`Q3`). 이유 3가지:

1. **폰트 프리베이크.** [PrebakeTMPFont.cs:23](../../Assets/Scripts/Editor/PrebakeTMPFont.cs#L23)은 `Assets` 하위의 **`.cs` 파일만** 스캔한다. 대사를 `.asset`에 넣으면 새 한글 글자가 아틀라스에 없어 **□로 렌더된다.** `.cs`에 두면 기존 프리베이크 메뉴가 그대로 동작한다.
2. 텍스트와 정수 하나뿐이라 인스펙터 편집의 이득이 거의 없다. 반면 `.asset`은 머지 충돌이 지저분하다.
3. 신규 임포터·에디터 메뉴·씬 와이어링이 전부 불필요해진다.

> **승급 경로**: 비개발자 작가가 직접 편집해야 할 시점이 오면 `ScenarioCSVImporter`와 같은 CSV+TXT 파이프라인으로 옮긴다. 그때는 **프리베이크 도구가 `.asset`/CSV도 스캔하도록 확장하는 작업이 동반 필수다** (`S11`).

토픽 5~6개로 시작한다. 20일 플레이 × 하루 2회 = 40회 소비되므로 소진 시 재사용을 허용하되, **같은 날 같은 토픽 재출현은 금지**한다 (`S2`).

#### 4.8.1. 요미 대사(`YomiLine`) 작성 규칙

바이블을 그대로 따르되, 이 대화는 **데이팅 파트**이므로 축이 고정된다.

- **차트 인격을 쓰지 않는다** (바이블 3.5절). 일상 대화에서 요미는 덤벙대고 응석부리고 질투한다
- 감정 축은 `DailyDialogueCategory` 7종 중 하나로 잡는다 (바이블 5.2절)
- 길이 **60자 이내**, 반말, 호칭 "오빠"
- 집착 표현 수위는 현재 집착도에 맞춘다 (바이블 5.3절)

#### 4.8.2. 플레이어 선택지(`TalkChoice.Text`) 작성 규칙 — **바이블이 다루지 않는 영역**

바이블은 요미의 목소리만 규정한다. **선택지는 오빠(플레이어)의 대사이므로 별도 규칙이 필요하다.**

| 항목 | 규칙 |
| --- | --- |
| 화자 | 오빠 |
| 요미 호칭 | **"요미" 또는 "너" 둘 다 자연스럽다.** 친밀한 관계의 2인칭이므로 매번 이름을 부르면 오히려 어색하다 |
| 말투 | 반말. 요미보다 **차분하고 짧게** — 느낌표를 남발하지 않는다 |
| 길이 | **25자 이내.** 버튼에 들어가야 한다 |
| 자칭 | **"나" 고정.** 오빠는 3인칭 자칭을 쓰지 않는다 — `요미가~` 식 3인칭 자칭은 **요미만의 특징**이므로 오빠가 따라 하면 캐릭터 구분이 무너진다 |

**선택지 설계 원칙 — 정답 하나 + 오답 하나가 아니다.**

+3은 요미가 **가장 듣고 싶어 하는 말**, +0은 **틀린 말이 아니라 무심한 말**이다. 플레이어가 "요미가 뭘 원하는지"를 읽어내는 게 게임이지, 상식 퀴즈가 아니다. 그래서 +0 선택지도 상황상 합리적으로 들려야 한다.

**예시 토픽** (`TOPIC_MEAL`, 노드 2개):

```
[노드 1] 요미: "오빠 밥은 먹고 다니는 거야? 요미가 굶지 말라고 했지!"
  ├─ "너나 잘 챙겨 먹어."          +0   ← 틀린 말은 아니지만 무심하다
  ├─ "너 주려고 참았지."            +2
  └─ "요미가 챙겨주면 먹을게."      +3   ← 의존을 인정해주는 말

[노드 2] 요미: "그럼 지금 만들어 줄게! 뭐 먹고 싶어?"
  ├─ "아무거나."                    +0
  ├─ "네가 좋아하는 걸로."          +2
  └─ "요미가 만든 거면 다 좋아."    +3
```

노드 2개 × 최대 +3 = **+6**. 티어 1(4~6) 진입은 되지만 티어 2(≥7)는 안 된다. **4노드짜리 토픽을 뽑아야 티어 2가 나온다** — 4.6절 수치와 맞물리는 의도된 설계다.

**대사 총량 견적**: 토픽 6개 × 노드 평균 3개 = 요미 대사 18줄 + 선택지 54줄. 여기에 힌트 대사 40줄, 마무리 대사 6줄. **합계 약 120줄.**

> ⚠️ 본 문서의 예시 대사는 **`.md`라서 `yomi_dialogue_lint.py`의 검사 범위 밖**이다. `YomiTalkTopics.cs`로 옮긴 뒤 린터를 돌려 다시 확인한다.

### 4.9. `YomiRoomManager` API 교체 — 요청사항 2·3번

```csharp
// 삭제
public async void ProcessUserChatInput(string userMessage);
private IYomiDialogueProvider dialogueProvider;
public void SetDialogueProvider(IYomiDialogueProvider provider);
// → IYomiDialogueProvider.cs, PlaceholderDialogueProvider.cs 파일째 삭제

// 신설
public bool TryStartTalk();                 // 슬롯 1 소모, 토픽 추첨, 실패 시 OnActionFailed
public void SelectChoice(int choiceIndex);  // 호감도 가산 → 다음 노드 or 종료
public event Action<TalkNode> OnTalkNodeAdvanced;
public event Action<string, string> OnTalkFinished;  // 마무리 대사, 힌트 대사(없으면 null)
```

UI(`YomiRoomDialogueUI`)는 **말풍선 로그·스크롤·초상화를 그대로 재사용**하고, 하단 입력 행(`TMP_InputField` + 전송 버튼)만 선택지 버튼 2~4개로 교체한다. `AppendLine()`은 손대지 않는다.

### 4.10. 힌트 발화 판정

`YomiRoomManager`가 토픽 종료 시 판정한다.

```
gainToday < 4                      → 힌트 없음
이미 오늘 힌트 발급됨              → 힌트 없음 (S3)
Outlook.Regime == Squeeze          → 방향 대신 변동성 경고 (S4)
그 외                              → 티어별 대사 1줄, MarkRevealed() 호출
```

**일차 조회는 `SaveLoadManager.Instance.CurrentData.CurrentDay`**를 쓴다. `GameManager`가 없는 씬이므로 `FindAnyObjectByType`은 쓸 수 없다. (`WorldMapManager`가 이미 쓰는 폴백 패턴과 동일)

> ⚠️ `SaveLoadManager`는 `TitleScene`에만 배치되어 있다(`SV-C6`). 요미의 방 씬을 에디터에서 **직접 재생하면 `Instance`가 null**이므로, 위 조회는 반드시 null 가드를 거쳐야 한다. `Instance`나 `CurrentData`가 없으면 **힌트 기능 전체를 조용히 비활성화**하고 대화만 진행한다 (`S19`).

---

## 5. 실행 계획

### 5.1. P0 — 배관 수리

| 항목 | 대상 파일 | 작업 |
| --- | --- | --- |
| `SV-B1` `SV-A6` | `SaveLoadManager.cs` | `SaveGame()`의 `data`를 `new SaveData { ... }`가 아니라 **`CurrentData`(없으면 새 인스턴스)를 베이스로** 조립한다. 그리고 **매니저별로 개별 가드**를 건다 — `gm == null`이면 트레이딩 블록 전체를, `DatingTimeManager.Instance == null`이면 데이팅 블록을 **건드리지 않는다**(베이스 값 유지). 현재의 일괄 `if (gm == null \|\| status == null \|\| levelSys == null \|\| memory == null) return false;`([:73](../../Assets/Scripts/System/SaveLoadManager.cs#L73))를 블록별 가드로 해체하는 것이 작업의 실체다 |
| `SV-A7` | `SaveLoadManager.cs` | `File.WriteAllText` 직후 **`CurrentData = data;`** 추가. 이것이 없으면 부분 저장의 베이스가 "마지막 로드 시점"에 고정된다 |
| `SV-B14` | `SaveLoadManager.cs` | 임시 파일에 쓰고 `File.Replace`로 교체 + `try/catch`. 더불어 `DatingTimeManager`의 변경별 즉시 저장을 **프레임 말 1회로 디바운스**(연속 호출 병합) |
| `SV-B2` | `WorldMapManager.cs` | `SV-B1`+`SV-A7`이 닫히면 기존 `CurrentData.Balance` 폴백([:82-85](../../Assets/Scripts/DatingSim/WorldMap/WorldMapManager.cs#L82-L85))이 **비로소 실제로 저장된다.** 폴백 직후 `SaveCurrentGame()` 호출만 추가 |
| `SV-B10` | `YomiRoomManager.cs`, `WorldMapManager.cs` | `StartTrading()` / 씬 이동 직전에 `SaveCurrentGame()` → `PrepareLoadGame(ActiveStorySlotIndex)` 순으로 호출. **`SV-B1`이 선행되어야 저장이 성공한다** (순서 고정) |
| — | `SaveLoadManager.cs` | `ApplyLoadedDataToGame()` 말미의 **`CurrentData = null;`([:449](../../Assets/Scripts/System/SaveLoadManager.cs#L449)) 제거.** 부분 저장의 베이스를 계속 들고 있어야 한다 |

> ⚠️ `SV-B1`/`SV-A6`은 "저장 실패"를 "부분 저장"으로 바꾸는 것이므로 **본 계획에서 가장 위험한 항목**이다. 7절 왕복 검사를 반드시 통과시킨다.
>
> **작업 내 순서 고정**: `SV-A7`(CurrentData 갱신) → `SV-B1`/`SV-A6`(부분 저장) → `SV-B10`/`SV-B2`. 역순으로 하면 부분 저장이 스테일 베이스 위에 쌓여 진행 상황이 롤백된다.

### 5.2. P1 — `SaveData` 스키마 확정 (한 번만 깬다)

**삭제 (`SV-D1` `SV-D2`)**

```csharp
// public TraderEmotion CurrentEmotion;          ← 제거
// public SignalPhase CurrentSignalPhase;        ← 제거
```

**추가 — P2/P3/P4에서 쓸 필드를 이 시점에 전부 넣는다**

```csharp
// --- TraderStatus 중독/연패 (SV-A1~A3) ---
public bool IsLeverageAddicted = false;
public int ConsecutiveHighLevWins = 0;
public int ConsecutiveLowLevTrades = 0;
public int CurrentLosingStreak = 0;
public bool CanRegenMental = true;

// --- 돌발 선택 이벤트 일일 스케줄 (SV-A4) ---
public int EventLastTriggerDay = -1;
public int EventsTriggeredToday = 0;
public int LowMentalEventsTriggeredToday = 0;
public int EventNextRandomTriggerMinuteOfDay = -1;
public long EventLastTriggerGameMinutes = -999999L;

// --- 이벤트 강제 포지션 계약 (SV-A5) ---
public bool IsEventTradeActive = false;
public TradingController.EventPositionHandlingMode EventHandlingMode = TradingController.EventPositionHandlingMode.StandardAuto;
public float EventTargetROELimit = 0f;
public float EventStopLossROELimit = 0f;
public bool IsEventPlayerChoice = false;
public bool IsEventTrueSignal = true;

// --- 차트 엔진 시간축/국면 (SV-B3~B5) ---
public int MarketLastUpdatedDay = -1;
public int MinutesUntilNextRegimeChange = 60;
public long MarketTotalMinutes = 0;

// --- 일일 정산 문맥 (SV-B6, SV-B9) ---
public float TodayRegularDeduction = 0f;
public string TodayRegularDeductionReason = "";
public bool IsSettlementProcessing = false;

// --- 차트 거시 방향성 (SV-B8 / P4) ---
public int OutlookDay = -1;
public MarketSimulationEngine.MarketRegime OutlookRegime;
public bool OutlookRevealed = false;

// --- 요미 선택형 대화 (P4) ---
public int TalkAffectionGainToday = 0;                            // 당일 대화 획득 호감도 합
public int TalkHintIssuedDay = -1;                                // 힌트를 발급한 일차
public List<string> TalkTopicsUsedToday = new List<string>();     // 당일 소비 토픽 ID
```

기존 `CurrentDailyRegime`([SaveData.cs:66](../../Assets/Scripts/System/SaveData.cs#L66))은 **`OutlookRegime`의 파생값이 되므로 유지하되 엔진이 쓰기만 한다.** 진실 원천은 `OutlookRegime`이다.

**마이그레이션**

신규 필드는 전부 **C# 필드 초기화자에 안전한 기본값**을 둔다. `JsonUtility.FromJson`은 인스턴스를 먼저 생성한 뒤(= 초기화자 실행) JSON에 존재하는 필드만 덮어쓰므로, **구버전 JSON에 없는 필드는 초기화자 값을 그대로 유지한다.** 따라서 `SaveDataMigrator`([SaveDataMigrator.cs:11](../../Assets/Scripts/System/SaveDataMigrator.cs#L11))에 추가할 백필은 없다. 필드 **삭제**도 구버전 JSON의 잔존 키를 `JsonUtility`가 조용히 무시하므로 안전하다. 버전 게이트만 한 칸 올린다.

```csharp
if (CompareVersions(dataVersion, "1.6.0") < 0)
{
    // 신규 필드는 초기화자 기본값으로 충분. 데드 필드 제거는 무시되므로 조치 없음.
    hasMigrated = true;
}
```

**배선 방식 — 리플렉션을 늘리지 않는다**

현재 주입부는 private 필드를 **리플렉션으로 이름 문자열 매칭**해 밀어넣는다 ([SaveLoadManager.cs:342-381](../../Assets/Scripts/System/SaveLoadManager.cs#L342-L381)). 여기에 신규 필드 20여 개를 더 얹으면 **필드명 오타 한 글자가 컴파일 에러 없이 조용한 저장 실패**로 변한다.

`MarketSimulationEngine`은 이미 올바른 패턴을 갖고 있다 — `CaptureSaveData(data)` / `RestoreFromSaveData(data)`. **이번에 건드리는 클래스에만** 같은 메서드 쌍을 추가하고, 손대지 않는 클래스의 기존 리플렉션은 그대로 둔다.

| 클래스 | 추가할 것 |
| --- | --- |
| `TraderStatus` | `CaptureSaveData(SaveData)` / `RestoreFromSaveData(SaveData)` — 기존 리플렉션 6줄도 여기로 흡수 |
| `ChoiceEventController` | 동일 |
| `TradingController` | `CaptureSaveData` 추가 + 기존 `RestorePosition`에 이벤트 계약 복원 병합 |
| `MarketSimulationEngine` | 기존 메서드에 `SV-B3`~`SV-B5` 필드 3개 추가 |
| `GameManager` | `SV-B6`/`SV-B9`는 리플렉션 대신 기존 `RestoreStartOfDayEquity` 옆에 `internal` 복원 메서드 1개 |

### 5.3. P2 — 치명 누락 배선

| 순서 | 항목 | 대상 파일 | 검증 |
| --- | --- | --- | --- |
| 1 | `SV-A1`~`SV-A3` | `TraderStatus.cs`, `SaveLoadManager.cs` | 중독 진입 → 저장 → 로드 → **중독 유지** |
| 2 | `SV-A4` | `ChoiceEventController.cs` | 이벤트 2회 소진 → 저장 → 로드 → **추가 이벤트 없음** |
| 3 | `SV-A5` | `TradingController.cs` | 이벤트 강제 포지션 보유 중 저장 → 로드 → **특수 익절 규칙 유지** |
| 4 | `SV-B12` | `DatingTimeManager.cs` | `ModifyAffection`에 `Mathf.Clamp(0, 100)` 추가 (`ModifyObsession`과 동일 형태). 한 줄 |

### 5.4. P3 — 정합성 복구

`SV-B3`~`SV-B6`, `SV-B9`. `MarketSimulationEngine.cs`의 `CaptureSaveData`/`RestoreFromSaveData`에 3필드 추가, `GameManager`에 정산 문맥 복원 메서드 추가. P2와 병행 가능.

### 5.4.5. P3.5 — 데이팅 루프 성립 (재검증 신설, P4의 하드 전제)

**하루의 루프 — `Q1`·`Q5` 승인 확정 (2026-08-13)**

```
[요미의 방] 아침 시작
     │  슬롯 소모 행동: 월드맵(알바·데이트) / 요미와 대화 / 휴식
     │
     ├─ [PC] 거래 개시 ──► [GameScene] Day N ──► 24:00 일일 정산
     │                                              │
     │                                              ▼
     │                                     Day N+1 진입 + 요미의 방 아침으로 복귀
     │
     └─ [침대] 취침 ─────────────────────────► Day N+1 + 요미의 방 아침
        (거래하지 않고 하루를 마감)
```

- **`Q5` 확정**: 진입 지점은 버튼이 아니라 **일일 정산 직후 자동 복귀**다. 정산이 끝나면 다음 날 아침으로 넘어가면서 곧바로 요미의 방에서 시작한다.
- **`Q1` 확정**: 거래 없이 하루를 넘기는 경로는 **요미의 방 침대의 취침**이다.

| 항목 | 작업 |
| --- | --- |
| `SV-A8` **슬롯 리필** | 일차 증가는 `GameManager.FinalizeProceedToNextDay()` 한 곳에만 두고([GameManager.cs:653](../../Assets/Scripts/GameManager.cs#L653)) **거기서 `DatingTimeManager.AdvanceDay()`를 호출해 슬롯을 리필**한다. `DatingTimeManager.currentDay`는 `GameManager.CurrentDay`의 미러가 된다 (`S12`) |
| `SV-B11` **진입 경로** | `FinalizeProceedToNextDay()` 말미에서 `YomiRoomScene`으로 전환. 취침 경로는 **같은 정산 루틴을 태워** 일차 증가 지점을 하나로 유지한다 |
| `SV-B13` **포지션 중 이탈** | 포지션 보유 중에는 데이팅 씬 이동을 **차단**한다 (`S18`) |

> ⚠️ **이중 일차 증가 / 정산 우회 위험.** 거래 도중 요미의 방으로 돌아갔다가 취침하면 24:00 정산을 건너뛰어 **페널티·보스·엔딩 판정이 통째로 누락**된다. 두 경로 모두 `FinalizeProceedToNextDay()`를 지나가게 만들거나, 거래를 시작한 날에는 조기 복귀를 막아야 한다. P3.5 구현 시 최우선 검증 항목이다.

세 항목 모두 P4의 기능 코드가 아니라 **P4가 도달 가능해지기 위한 조건**이다. 이 층이 비면 힌트를 만들어도 플레이어가 볼 수 없다.

**P3.5 구현 기록 (2026-08-13)**

| 항목 | 반영 |
| --- | --- |
| `Q1` 취침 | 침대 모달을 **버튼 3개(주/보조/취소)** 구조로 확장. 보조 버튼 "오늘은 여기까지" → `YomiRoomManager.TrySleep()`. **일차를 직접 올리지 않고** `GameManager.PendingSleepThroughToday` 플래그를 세운 뒤 GameScene에서 `AdvanceGameMinutes(24:00까지 남은 분)`로 **기존 정산 루틴을 그대로 태운다** — 정산 우회 위험을 구조적으로 제거 |
| `SV-A8` 슬롯 리필 | `FinalizeProceedToNextDay()`에서 `DatingTimeManager.SyncToNewDay(currentDay)` 호출. `AdvanceDay()`를 **폐기하고** 스스로 일차를 올리지 않는 미러 전용 메서드로 대체 (`S12`) |
| `SV-B11` 진입 경로 | 정산 완료 후 `ReturnToYomiRoomForNewMorning()`으로 `YomiRoomScene` 전환. **아침 컷씬이 있는 날(스토리 6·16일차, 보스전)은 제외** — 그 연출이 트레이딩 파트의 도입이라 GameScene에 머무른다 (`ponytail:` 주석으로 후속 검토 표시) |
| `SV-B13` 이탈 차단 | PC 상호작용에 `EnsureNoOpenPosition()` — 포지션이 열려 있으면 거부하고 사유 표시 (`S18`) |
| UI 규격 | [P2_05_YomiRoom_Modal_UI_Spec.md](../P2_05_UI_and_Art/P2_05_YomiRoom_Modal_UI_Spec.md) 신설. 이미지를 `Resources/DatingSim/YomiRoom/UI/Modal/`에 넣기만 하면 반영되며, **없으면 단색 임시 UI로 그대로 동작**한다 |

**착수 전 확인된 사실 — 요미의 방에 "취침" 액션은 아직 없었다 (현재는 추가됨).**
현재 침대(`YomiRoomInteractionType.RestBed`)에 걸린 것은 **휴식**이다 — 슬롯 1을 쓰고 체력 +10을 회복하며 하루를 넘기지 않는다([YomiRoomTopDownPrototype.cs:179-183](../../Assets/Scripts/DatingSim/YomiRoom/YomiRoomTopDownPrototype.cs#L179-L183), [Builder:110](../../Assets/Scripts/DatingSim/UI/YomiRoomTopDownPrototypeBuilder.cs#L110)). 방의 상호작용은 침대와 PC **둘뿐**이다. 취침은 신설해야 하며, 확인 모달이 단일 확인 버튼 구조라 **휴식과 취침을 어떻게 갈라 보여줄지**가 남은 설계 결정이다 (`Q6`).

### 5.5. P4 — 신규 기능

| Phase | 내용 | 대상 파일 | 검증 |
| --- | --- | --- | --- |
| **4-a** | `DailyMarketOutlook` 신설 + 저장 배선 | `DailyMarketOutlook.cs`(신규), `SaveLoadManager.cs` | 7절 정합성 검사 |
| **4-b** | 엔진 개조: `CurrentDailyRegime` 공개, 추첨 이관 | `MarketSimulationEngine.cs` | GameScene 재입장 시 방향성 불변 |
| **4-c** | 자유 채팅 폐지 + 선택형 대화 매니저 API | `YomiRoomManager.cs`, `IYomiDialogueProvider.cs`(삭제), `PlaceholderDialogueProvider.cs`(삭제) | `dotnet build` 양쪽 |
| **4-d** | 대화 DB + 힌트 대사 풀 + UI 선택지 버튼 | `YomiTalkTopics.cs`(신규), `YomiRoomTopDownPrototype.cs`, `YomiRoomTopDownPrototypeBuilder.cs` | `python yomi_dialogue_lint.py` 통과 + 인게임 1회 완주 + **프리베이크 재실행** |

### 5.6. P5 — 후속 / 문서

| 항목 | 내용 |
| --- | --- |
| `SV-B7` (`Q1`) | 일차 카운터 일원화 + 취침(하루 넘김) 경로. **승인 필요** |
| `SV-C1`~`SV-C5` | 여유 시 처리. `SV-C5`는 코드 수정 없이 문서화만 |
| 문서 갱신 | `CLAUDE.md`(채팅 UI 삭제 금지 조항 수정, `Q4`), [DatingSim_FreeChat_Removal_Plan.md](../P2_03_LLM_Architecture/DatingSim_FreeChat_Removal_Plan.md), [Refactored_Architecture_Master.md](Refactored_Architecture_Master.md) 추가 기록 |

---

## 6. 안전장치

| ID | 위험 | 장치 |
| --- | --- | --- |
| **S1** | 대화 무한 반복으로 호감도·힌트 파밍 | 대화 1회 = 시간 슬롯 1 소모. 슬롯 0이면 `TryStartTalk()` false |
| **S2** | 같은 토픽 반복으로 정답 선택지 암기 파밍 | `TalkTopicsUsedToday`로 당일 재출현 차단 |
| **S3** | 하루에 여러 번 힌트 받아 티어 승급 | `TalkHintIssuedDay`로 하루 1회 고정. 티어는 최초 발급 시점 확정 |
| **S4** | `Squeeze` 날에 방향 힌트를 주면 필연적으로 거짓말 | `Squeeze`는 방향 대사 자체를 발화하지 않고 변동성 경고 전용 풀 사용 |
| **S5** | 요미가 알려준 방향과 실제 차트가 반대로 감 | ① 대사를 "확정"이 아닌 "경향"으로 작성 ② `Revealed` 날은 역방향 단기 국면 분기 제거(`Q2`) ③ 선택 이벤트·오버도즈 트랩의 일시적 역주행은 **허용된 노이즈**로 명시 (`IsOverridingTrend`, [:67](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L67)) |
| **S6** | 세이브 스컴으로 방향성 재추첨 | Outlook을 세이브에 기록하고 **결정 즉시 저장**. 재로드해도 같은 값 |
| **S7** | 씬 재진입마다 방향성이 바뀜 | 엔진의 자체 추첨 제거(4.4) + `lastUpdatedDay` 복원(`SV-B3`) |
| **S8** | 새 게임인데 이전 판의 Outlook이 살아 있음 (`static` 잔존) | `StartNewGame()` / `PrepareNewGame()`에서 명시적 리셋 |
| **S9** | 오답 선택 시 호감도 감소로 진행이 막힘 | 최소 획득량 0. **마이너스를 주지 않는다.** 집착도(`ModifyObsession`)는 이번 범위 밖 |
| **S10** | 씬 전환 중 비동기 응답이 파괴된 오브젝트를 건드림 | 기존 `if (this == null) return` 패턴 유지 ([YomiRoomTopDownPrototype.cs:475](../../Assets/Scripts/DatingSim/YomiRoom/YomiRoomTopDownPrototype.cs#L475)). 선택형은 동기 처리라 위험 자체가 줄어듦 |
| **S11** | 새 한글 대사가 □로 렌더 | 대사를 `.cs` 정적 테이블에 배치(4.8) + P4-d 완료 시 `Tools/Prebake All Scripts Text into Font` 재실행 **필수** |
| **S12** | 일차 카운터 이원화로 힌트가 엉뚱한 날을 가리킴 | 일차는 `SaveData.CurrentDay` 단일 기준. `DatingTimeManager.currentDay`는 미러로 격하 (`SV-B7`) |
| **S13** | 힌트만 받고 거래하지 않은 채 날이 바뀜 | Outlook이 일차 키를 가지므로 `Day != CurrentDay` 시 자동 무효 → 재추첨 (요청사항 6번 후반부) |
| **S14** | 오버도즈/기믹 중 저장 차단 규칙과 충돌 | 기존 차단 규칙([SaveLoadManager.cs:86-90](../../Assets/Scripts/System/SaveLoadManager.cs#L86-L90)) 유지. Outlook 결정은 요미의 방에서 일어나므로 해당 없음 |
| **S15** | P0의 부분 저장이 트레이딩 상태를 빈 값으로 덮어씀 | `gm == null`일 때 트레이딩 블록을 **건드리지 않는다**(베이스 `CurrentData` 값 유지). 7절 왕복 검사에서 GameManager 부재 시나리오를 명시적으로 검증 |
| **S16** | **부재 매니저의 필드가 기본값으로 디스크에 박힘** (`SV-A6`) | 저장 규칙을 일반화한다 — **"필드를 쓰는 주체가 씬에 없으면 그 필드는 손대지 않는다."** `gm` 한 방향이 아니라 `DatingTimeManager` / `TraderStatus` / `TraderLevelSystem` / `TraderMemoryManager` 전부에 블록별 가드를 건다 |
| **S17** | 저장 빈도 급증 + 원자성 부재로 세이브 파일 손상 (`SV-B14`) | 임시 파일 → `File.Replace` 원자적 교체 + `try/catch`. `DatingTimeManager`의 변경별 저장은 프레임 말 1회로 디바운스 |
| **S18** | 손실 포지션을 들고 데이팅 씬으로 도망쳐 무기한 청산 회피 (`SV-B13`) | `TradingController.IsActive`가 true면 데이팅 씬 전환 차단. 사유를 UI에 표시 |
| **S19** | 요미의 방 단독 재생 시 `SaveLoadManager` 부재로 NRE (`SV-C6`) | `Instance`/`CurrentData` null이면 **힌트 기능만 조용히 비활성화**하고 대화는 정상 진행. 예외를 던지지 않는다 |

---

## 7. 검증

`.asmdef`도 테스트 어셈블리도 없으므로 **에디터 메뉴 자동 검사 2개**를 남긴다. 프레임워크는 쓰지 않는다.

### 7.1. `FXOverdose/Debug/세이브 왕복(Round-trip) 검사` — P0~P3용

- 현재 씬 상태 → `SaveGame` → `PrepareLoadGame` → `ApplyLoadedDataToGame` 후 **A·B 등급 필드 전부가 왕복 전후 동일**한지 `Assert`
- `SaveData`의 모든 public 필드를 리플렉션으로 열거해, **수집부에서 한 번도 대입되지 않은 필드**를 경고로 출력 (`SV-D1` 같은 데드 필드 재발 방지)
- `GameManager`가 없는 상태를 흉내 내어 **부분 저장이 성공하고, 트레이딩 필드가 보존되는지** (`SV-B1` / `S15`)
- **`DatingTimeManager`가 없는 상태**(= GameScene 단독)에서 저장 → **데이팅 필드가 기본값으로 덮어써지지 않는지** (`SV-A6` / `S16`) — 이전 판이 놓친 반대 방향
- 저장 → `CurrentData`가 **방금 저장한 값과 일치**하는지 (`SV-A7`)
- 저장 중 예외를 강제로 발생시켜 **기존 세이브 파일이 온전히 남는지** (`SV-B14` / `S17`)

### 7.2. `FXOverdose/Debug/요미 힌트 ↔ 차트 방향성 정합성 검사` — P4용

- `GetOrRoll(day)` 100회 반복 → 같은 `day`면 항상 같은 값 (`S6`/`S7`)
- `day` 증가 → 값 재추첨, `Revealed` 해제 (`S13`)
- 4개 Regime × 2 티어 = 8칸 힌트 대사 풀이 **전부 비어 있지 않은지** (`S4`·`S11` 회귀 방지)
- `Squeeze` + 티어 2 → 방향 단어(`위`/`아래`)를 포함하지 않는지 (`S4`)
- 모든 `TalkTopic`의 노드 수가 2~4이고 각 노드 선택지가 2~4개인지 (요청사항 4번 계약)
- 각 노드에 **+3 선택지가 정확히 하나** 있는지 (4.8.2절 설계 원칙)
- 선택지 텍스트가 **25자 이내**인지 (버튼 잘림 방지)

말투·호칭 검사는 별도 도구가 맡는다:

```
python yomi_dialogue_lint.py
```

### 7.3. 수동

```
dotnet build "Assembly-CSharp.csproj" -v:m
dotnet build "Assembly-CSharp-Editor.csproj" -v:m
```

인게임 시나리오:

| 단계 | 시나리오 |
| --- | --- |
| P0 | 요미의 방에서 호감도 변경 → 세이브 JSON 반영 확인 / 요미의 방 → GameScene 왕복 후 일차·잔고 유지 확인 / **호감도 30인 세이브를 타이틀에서 로드 → GameScene에서 저장 → 호감도가 30으로 유지되는지** (`SV-A6`) |
| P3.5 | 슬롯 소진 → 다음 날 진입 → **슬롯 5로 리필** (`SV-A8`) / GameScene에서 요미의 방으로 이동 가능 (`SV-B11`) / 포지션 보유 중 이동 시도 → **차단** (`SV-B13`) |
| P2 | 중독 상태 진입 → 저장 → 로드 → 중독 유지 (`SV-A1`) / 이벤트 2회 소진 후 로드 → 추가 이벤트 없음 (`SV-A4`) / 이벤트 포지션 보유 중 로드 → 특수 익절 규칙 유지 (`SV-A5`) |
| P4 | 요미의 방 대화 → 티어 2 힌트 획득 → GameScene 진입 → 차트 방향 일치 → 요미의 방 재진입 → 힌트 재발급 차단 → 다음 날 → 방향성 재추첨 |

---

## 8. 미결정 사항 (승인 필요)

| ID | 내용 | 권장안 | 관련 |
| --- | --- | --- | --- |
| **Q1** | **거래 없이 하루를 넘기는 경로가 현재 존재하지 않는다.** 일차를 올리는 코드는 `GameManager.FinalizeProceedToNextDay()` 하나뿐이며, GameScene에서 게임 내 24:00에 도달해야만 실행된다. 요청사항 6번의 "거래하지 않고 다음날로" 전제가 미성립 | **(A) 요미의 방에 "취침" 액션 추가 → GameScene 복귀 후 기존 일일 정산 루틴 재사용.** 일차 증가 로직이 여전히 한 곳에만 존재하므로 정산·페널티·보스·엔딩 판정이 전부 정상 동작<br>**(B) 일차 소유권을 `SaveData.CurrentDay`로 옮겨 양쪽 씬에서 증가 → 파산 판정 우회 구멍. 비권장** | `SV-B7` `S12` / P5 |
| **Q2** | 힌트 발급일의 차트 편향 강화 여부. 현재 일일 국면의 실제 영향력은 `macroDrift` ±0.00015와 단기 국면 60% 편향뿐이라 티어 2 힌트를 받고도 체감상 반대로 가는 날이 나온다 | `Outlook.Revealed == true`인 날에 한해 `SwitchToRandomRegime()`에서 **역방향 국면 분기(10%)를 제거**(Bull 날의 Bear 분기 → Sideways로 흡수). 튜닝 노브는 `hintedBiasStrength` 하나만 노출, 드리프트는 건드리지 않음 | `S5` / P4-b |
| **Q3** | 대화 DB 형식 | C# 정적 테이블 (폰트 프리베이크 때문, 4.8절). 이의 없으면 진행 | `S11` / P4-d |
| **Q4** | `CLAUDE.md`의 "채팅 UI 삭제 금지" 지시 뒤집기 | 입력 행만 교체, 말풍선 로그는 유지. **본 계획 승인 = 승인** | P5 |
| **Q5** | **`GameScene` → 요미의 방 진입 지점을 어디에 둘 것인가.** 현재 데이팅 파트는 정상 플레이에서 도달 불가 (`SV-B11`) | **(A) 일일 정산 창을 닫은 뒤 "저녁 일정" 선택지로 분기** — 하루의 리듬(거래 → 정산 → 저녁)에 자연스럽게 얹히고, `Q1`의 취침 경로와 한 쌍으로 맞물린다. **권장**<br>**(B) 상단 바에 상시 버튼** — 구현은 가장 싸지만 장중 아무 때나 이탈 가능해져 `SV-B13` 위험이 커지고 시간 압박이 무너진다 | `SV-B11` `SV-B13` / P3.5 |

---

## 9. 범위 밖 (이번에 하지 않는 것)

- 집착도(`ModifyObsession`) 연동 — 호감도만 다룬다
- LLM 기반 대사 생성 — 선택형은 정적 텍스트로 충분하다
- `FXOverdose.DatingSim.Scenario` 재배선 — 여전히 호출자 0건이며 이번 기능과 무관
- 대화 연출(표정 변화, 보이스) — `YomiSpriteController` 연동은 후속
- 힌트 정확도의 아이템/스킬 강화 — 밸런스 확정 후 검토
- `SV-C3`의 `TraderMemoryManager` 리플렉션 제거 — 이번에 건드리지 않는 클래스이므로 유지 (5.2절 원칙)
