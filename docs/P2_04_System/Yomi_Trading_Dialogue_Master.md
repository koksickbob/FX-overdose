# 트레이딩 파트 요미 대사 마스터 문서

- **작성일**: 2026-10-06 / **갱신**: 2026-10-08 — 요미 대사 전면 삭제 반영
- **기준**: `Dev_koksickbob3` 작업 트리 (2026-10-08)
- **성격**: 기획서가 아니라 **현재 코드에 실제로 살아 있는 것만** 적은 역공학 레퍼런스입니다. 파일·행 번호는 소스에서 직접 확인했습니다. 코드와 다르면 코드가 맞습니다. 모든 분석은 **정적 분석**이며 플레이 검증은 하지 않았습니다.
- **범위**: `GameScene` 트레이딩 루프에서 요미가 말하는 **모든 경로** — HUD 말풍선, 돌발 선택 이벤트 팝업, 일일 정산, 게임오버, 스토리·보스 독백, 표정 연동, 데이터 자산, 저작·검증 도구. 미연시 파트(요미의 방 대화)는 트레이딩 접점(일일 방향 힌트)과 2026-10-08 삭제 기록(§13)만 다룹니다.
- **구성**: §0~§9는 현재 동작, **§10은 확인된 결함·불일치**, §11은 대사를 채울 때의 작업 순서, §12는 제거된 기능의 이력, §13은 2026-10-08 전면 삭제 기록입니다.

> ⚠️ **2026-10-08 — 요미 대사는 현재 0줄입니다.** 트레이딩·미연시를 가리지 않고 요미의 대사 텍스트를 모두 지웠습니다.
> 말풍선 엔진·매처·DB 컨테이너·이벤트 팝업 카드·정산/게임오버 카드·요미의 방 대화 UI와 **각 트리거는 그대로**이고, 대사만 비어 있습니다.
> 코드에 박혀 있던 하드코딩 대사는 전부 **DB 카테고리 조회**로 바뀌었습니다. 이 문서의 "대사" 자리는 이제 **DB 카테고리 키**를 가리킵니다(§3). 새 대사를 채우는 방법은 §11.

> 관련 문서: 트레이딩 시스템 전체는 [Trading_System_Master.md](Trading_System_Master.md), 요미 화법 규칙은 [P2_02_Yomi_Character_Bible.md](../P2_02_Worldbuilding/P2_02_Yomi_Character_Bible.md) §4, LLM 제거 기록은 [Trading_LLM_Removal_Plan.md](../P2_03_LLM_Architecture/Trading_LLM_Removal_Plan.md).

---

## 0. 한눈에 보기

**이 프로젝트에 LLM은 없습니다.** 트레이딩 파트의 요미 대사는 ① 대사 DB(카테고리 키 조회) ② 이벤트 에셋 텍스트 ③ `GameManager`의 순서형 독백 목록, 셋 중 하나에서만 나옵니다. **셋 다 비어 있고, 코드에 하드코딩된 대사는 없습니다.**

```
                         ┌──────────────── 데이터 소스 (전부 0줄) ────────────────┐
                         │ YomiDialogueDatabase.asset — 카테고리 키 62종의 유일한 정본 │
                         │ 이벤트 에셋 (템플릿 242개 / EVENT_* 30개)의 요미 대사 칸    │
                         │ GameManager 독백 목록 (6·16일차, 보스 등장·승패)            │
                         └─────────────────────────────────────────────────────────┘
                                          │
   ┌──────────────────────────────────────┼─────────────────────────────────────┐
   ▼                                      ▼                                     ▼
[채널 A] HUD 말풍선                 [채널 B] 돌발 이벤트 팝업          [채널 C] 모달 화면
AIVisualController                   ChoiceEventPopupUIController        DailySettlementUIController
 · 우선순위 4단 + 큐 + 타이프라이터   · "YOMI // AI MARKET ANALYST" 카드 · 정산 반응 (DB 3키)
 · 대사마다 표정 자동 평가            · 대사가 없으면 머리글만 표시       GameOverUIController
 · DB에 대사가 없으면 침묵            · 표정 없음                         · 최종 메시지 (DB 4키)
                                                                         · 표정은 손익/엔딩으로 직접 지정
```

| 채널 | 경로 수 | 대사 출처 | 표정 |
|---|---|---|---|
| A. HUD 말풍선 | 약 40개 트리거 (§3·§4) | DB 카테고리 (경로 A 3종 + 경로 B 51종) + 독백 목록 | `TraderEmotionEvaluator`가 대사마다 자동 평가 (§8) |
| B. 돌발 이벤트 팝업 | 1 (§5) | 템플릿 `FallbackMonologues` / `EVENT_*.AIMonologue` → DB `ChoiceEventFallback` | 없음 |
| C. 정산·게임오버 모달 | 2 (§6, §7) | DB `Settlement*` 3종 / `GameOverFinal_*` 4종 | 화면이 직접 지정 + HUD 캐릭터에도 `ShowEmotion` |

### 0.1 대사 총량

| 출처 | 삭제 전 | 현재 | 비고 |
|---|---|---|---|
| `Assets/YomiDialogueDatabase.asset` | 810줄 (21 카테고리) | **0** | 모든 트리거의 정본 (§3) |
| `.cs` 하드코딩 말풍선 리터럴 | 약 50줄 | **0** | 자리는 DB 카테고리 조회로 전환 |
| 돌발 이벤트 템플릿 `FallbackMonologues` | 726줄 (242개 × 3) | **0** | 빈 리스트 |
| `EVENT_*.asset`의 `AIMonologue` | 30줄 | **0** | 빈 문자열 |
| `ChoiceEventRuntimeData` 기본 이벤트 | 15줄 | **0** | 빈 문자열 |
| 정산 반응 / 게임오버 최종 메시지 | 3 / 4줄 | **0** | DB 조회로 전환 |
| 정산 위약금 대사 (`GameScene.unity` storyEvents) | 3줄 | **0** | 빈 문자열 |
| 6·16일차 독백 / 보스 등장·승패 독백 | 3 + 3 / 3 + 3 + 3줄 | **0** | 빈 목록 |
| 보스 등장 반응 (`BossBattleUIController`) | 8줄 | **0** | DB 조회로 전환 |
| `Assets/YomiDailyDialogueDatabase.asset` | 100줄 | **0** | 원래 런타임 미사용 (§9.2) |
| `DialogueDB_Import.json` (DB 미러) | 810줄 | **0** | `{"entries": []}` |

미연시 파트의 삭제분(요미의 방 토픽 9편·인사 4줄·방향 힌트 40줄, 스토리 이벤트 샘플)은 §13.

---

## 1. 말풍선 엔진 — `AIVisualController`

트레이딩 파트 요미 대사의 대부분이 이 컴포넌트 하나로 나갑니다. **이벤트를 구독하지 않습니다** — 모든 호출자가 직접 부릅니다. 구독은 의상 변경(`CostumeManager.OnCostumesChanged`)과 아이템 소비(`Inventory.ItemConsumed` → 포즈) 두 개뿐입니다.

### 1.1 진입점과 열거형

- `DisplayDialogueBalloon(text, priority, category)` — [AIVisualController.cs:811](../../Assets/Scripts/AI/AIVisualController.cs#L811). 오버로드 [:792](../../Assets/Scripts/AI/AIVisualController.cs#L792)(Normal/General), [:797](../../Assets/Scripts/AI/AIVisualController.cs#L797)(General). **`text`가 null이거나 비어 있으면 아무것도 하지 않습니다** — DB에 대사가 없을 때 침묵하는 근거입니다.
- `DisplayDatabaseDialogue(dbCategory, priority, category)` — [:806](../../Assets/Scripts/AI/AIVisualController.cs#L806) (2026-10-08 신설). 대사 DB의 카테고리에서 한 줄을 뽑아 위로 넘깁니다. 하드코딩 대사를 대체한 자리 대부분이 이걸 씁니다.
- `DialoguePriority` = `Low` / `Normal` / `High` / `Critical` ([:11-17](../../Assets/Scripts/AI/AIVisualController.cs#L11))
- `EventCategory` = `General` / `ChartMovement` / `MentalChange` / `HealthChange` / `GimmickTriggered` / `ItemUsed` / `PositionOpened` / `PositionClosed` / `SkillUpgraded` / `DailySettlement` / `Tutorial` ([:19-32](../../Assets/Scripts/AI/AIVisualController.cs#L19)). `DailySettlement`와 `HealthChange`는 **호출자가 없습니다.** 이 열거형은 **말풍선 분류**(쿨다운·표정 판정용)이고, 대사 DB의 카테고리 키(§3)와는 별개입니다.

### 1.2 표시 규칙 (판정 순서대로)

1. 고속 시간 경과(`IsFastForwardingTime`) 중에는 **High 미만 폐기**.
2. 새 `SkillUpgraded` 요청이 오면 큐에 쌓인 `SkillUpgraded`를 지움.
3. **카테고리 쿨다운** — Low/Normal에만, `General`은 제외. 표시 *시작* 시각 기준.

   | 카테고리 | 쿨다운 | | 카테고리 | 쿨다운 |
   |---|---|---|---|---|
   | ChartMovement | 10초 | | ItemUsed | 3초 |
   | HealthChange | 8초 | | PositionOpened | 3초 |
   | MentalChange | 7초 | | PositionClosed | 3초 |
   | GimmickTriggered | 6초 | | SkillUpgraded | 2초 |
   | 그 외 | 4초 | | | |

   [GetCategoryCooldown, :1167](../../Assets/Scripts/AI/AIVisualController.cs#L1167)
4. **Critical**은 항상 선점.
5. **High** — Critical 표시 중이면 큐. High 표시 중이면 둘 다 `SkillUpgraded`일 때만 선점, 아니면 큐. GameOver 상태면 큐. 그 외엔 Low/Normal을 선점하거나 바로 시작.
6. **Low** (표시 중) — 큐의 첫 Low를 교체, 없으면 큐(10개 미만일 때).
7. **Normal** (표시 중) — 큐(10개 미만일 때).
8. 큐는 **우선순위 정렬 없는 FIFO**입니다. 꺼낼 때 TTL 검사: High/Critical 8초, Low/Normal 5초(요청 시각 기준). GameOver 상태에서는 TTL 무시.

### 1.3 표시 시간과 타이프라이터

[StartOrPreemptDialogue, :946](../../Assets/Scripts/AI/AIVisualController.cs#L946) / [TypewriterCoroutine, :983](../../Assets/Scripts/AI/AIVisualController.cs#L983)

| 값 | 공식 / 값 | 비고 |
|---|---|---|
| 타이프라이터 간격 | **0.03초/글자** | 코드 기본값 0.02, `GameScene.unity:1081` 직렬화 값이 우선 |
| 타이핑 후 유지 | `Clamp(글자수 × 0.07, 2.5, max(balloonDisplayDuration, 5))` = **2.5~8초** | `balloonDisplayDuration` 코드 4 / 씬 **8** |
| 선점 잠금 | `max(2.2, 글자수 × 0.05)`초 | `IsBalloonActive` 판정에 쓰임 |
| 시간 축 | `WaitForSeconds` (스케일 시간) | 설정 메뉴가 `timeScale = 0`이면 타이핑도 멈춤 |
| `Tutorial` 카테고리 | 자동으로 닫히지 않고 진행 화살표 표시 | 스토리 독백 전용 (§4.6) |

- 글자 수로 자르지 않습니다. TMP `Overflow = Ellipsis`, 자동 크기 19~27(×1.15), 글자색 `#CFFAFE`. 바이블의 "말풍선 80자" 제한은 넘치면 말줄임표로 잘리는 것을 피하기 위한 **저작 규칙**입니다.

---

## 2. 대사 DB와 매처 — `YomiDialogueDatabase` / `YomiDialogueMatcher`

### 2.1 구성

| 요소 | 위치 | 비고 |
|---|---|---|
| 엔트리 스키마 `YomiDialogueEntry` | [YomiDialogueData.cs](../../Assets/Scripts/AI/Dialogue/YomiDialogueData.cs) | 필드 목록은 §11.2 |
| DB 컨테이너 | [YomiDialogueDatabase.cs](../../Assets/Scripts/AI/Dialogue/YomiDialogueDatabase.cs) | 조회표 2개: `"{position}_{marketTrend}"` 키 / `eventCategory` 키 |
| 에셋 | `Assets/YomiDialogueDatabase.asset` | **엔트리 0개.** `Resources` 밖 → 씬 직렬화 참조로만 연결 |
| 매처 | [YomiDialogueMatcher.cs](../../Assets/Scripts/AI/Dialogue/YomiDialogueMatcher.cs) | `GameScene.unity`의 루트 오브젝트 `YomiDialogueMatcher`. `DontDestroyOnLoad` 싱글턴, 쿨다운 5초 |

> ⚠️ 매처는 **GameScene에만** 있습니다. GameScene을 한 번도 열지 않은 세션(예: 요미의 방에서 시작해 방에서 취침·정산)에서는 `Instance == null`이고, DB 조회는 전부 조용히 실패합니다. 2026-10-08 이전에는 하드코딩 폴백이 있는 경로(정산 반응·게임오버 메시지 등)가 그래도 말했지만, **지금은 모든 경로가 DB를 거치므로 그 세션에선 전부 침묵합니다** (§10 YD-11).

### 2.2 두 개의 조회 경로

| | **경로 A — `GetDialogue()`** (점수제) | **경로 B — `GetEventDialogue()`** (카테고리 랜덤) |
|---|---|---|
| 위치 | [YomiDialogueMatcher.cs:42-140](../../Assets/Scripts/AI/Dialogue/YomiDialogueMatcher.cs#L42) | [:145-163](../../Assets/Scripts/AI/Dialogue/YomiDialogueMatcher.cs#L145) |
| 호출 | `TradingController.OutputYomiDialogue()` [:1684](../../Assets/Scripts/Trading/TradingController.cs#L1684) 하나 | `TradingController.OutputSpecificEventDialogue()` [:1660](../../Assets/Scripts/Trading/TradingController.cs#L1660), `AIVisualController.DisplayDatabaseDialogue()`, 그 밖의 직접 호출 (기믹·차트 힌트·스킬·펀딩비·보스·정산·게임오버·이벤트 팝업) |
| 쓰는 카테고리 | `PositionOpened` / `PositionClosed` / `ChartMovement` (3종) | 나머지 59종 (§3.2~§3.5) |
| 선택 | 점수 최고점 −5 이내 풀을 셔플 → 최근 15개 해시에 없는 첫 줄 | 카테고리 안에서 균등 랜덤 (반복 방지 없음) |
| 쿨다운 5초 | **검사함** (걸리면 `null`) | 검사 안 함. 단 성공 시 쿨다운 시각을 갱신 |
| 매처 치환 | `{leverage}` `{margin}` | `{leverage}` `{margin}` `{roe:F1}` `{maxObserved:F1}` |
| 호출부 치환 | — | `{position}` (차트 힌트) · `{level}` (스킬) · `{amount}` (펀딩비) · `{boss}` (보스 등장) |
| 결과 없음 | 아무 말 안 함 | 아무 말 안 함 — **하드코딩 폴백은 2026-10-08에 전부 삭제** |

**쿨다운이 두 경로에 공유됩니다.** 경로 B 대사가 하나 나가면 이후 5초 동안 경로 A(진입·청산·차트 대사)가 `null`이 됩니다. 2026-10-08에 체력 경고·기믹·펀딩비 같은 옛 하드코딩 자리까지 경로 B로 옮겨졌으므로, **대사를 채우면 이 간섭이 예전보다 넓게 일어납니다** (§10 YD-4).

### 2.3 경로 A 점수 공식 — `CalculateScore` [:165-277](../../Assets/Scripts/AI/Dialogue/YomiDialogueMatcher.cs#L165)

| 조건 | 점수 |
|---|---|
| `eventCategory == currentAction` | **+500** (다르면 **−9999 = 탈락**). **`eventCategory`가 빈 엔트리는 이 판정을 건너뜁니다** — 탈락도 +500도 없이 세 동작 모두의 후보가 되고, 같은 카테고리 엔트리가 있으면 집니다 |
| 포지션 보유 중: 방향 일치 / `isProfit` 일치 | +50 / +50 |
| 무포지션: 방향 일치 | +30 |
| `mentalState` 일치 | +40 |
| 레버리지 근접 | 최대 +30 |
| 고위험 대사 & 마진비율 ≥ 0.7 / 저위험 대사 & 마진비율 < 0.5 | +35 / +15 |
| 주인공·스킬 레벨 근접 | 각 최대 +20 |
| `requiredOwner` 일치 | +30 |
| `requiredChartTrend` 일치 | +50 (Any가 아닌데 불일치면 **−9999**) |
| PnL 구간 / 보유 시간 구간 일치 | +20 / +20 |
| 체력 ≤ `maxHealthLimit` | +35 |
| 의상 일치 | +40 |

**입력값** ([TradingController.cs:1684-1756](../../Assets/Scripts/Trading/TradingController.cs#L1684)):
- `position` = 현재 포지션 문자열 (`None`/`Long`/`Short`)
- `mentalState` = `TraderStatus.MentalState` (**`Stable`/`Anxious`/`Danger`/`Overdose`**) — 대사를 쓸 때 이 넷으로 태깅해야 +40을 받습니다 (§10 YD-3)
- `owner` = 포지션 소유자, 무포지션이면 현재 매매 모드
- `actualChartTrend` = 신호 페이즈가 있으면 목표 변동률 부호로 `Pump`/`Dump`, 없으면 `Sideways`
- `marketTrend`는 ROE 부호로 Bull/Bear/Sideways를 만듭니다. 조회표 1차 키이므로 `Any`로 두면 항상 후보에 듭니다

---

## 3. DB 카테고리 전체표 — 코드가 요청하는 키 62종

**전부 0줄입니다.** 아래 키로 엔트리를 넣으면 해당 트리거가 말하기 시작합니다(§11). 키는 대소문자를 구분합니다. 경로 B 엔트리는 `text`와 `eventCategory`만 읽습니다.

### 3.1 경로 A — 점수제 (3종)

| 키 | 트리거 | 위치 (TradingController.cs) | 우선순위 |
|---|---|---|---|
| `PositionOpened` | AI·이벤트·오버도즈·뇌동 진입 / 수동 LONG·SHORT / P2P | [:967](../../Assets/Scripts/Trading/TradingController.cs#L967), [:1075](../../Assets/Scripts/Trading/TradingController.cs#L1075), [:44](../../Assets/Scripts/Trading/TradingController.cs#L44) | High |
| `PositionClosed` | 일반 청산 / 강제 청산 / P2P / 24:00 강제 정리 | [:1208](../../Assets/Scripts/Trading/TradingController.cs#L1208) (High), [:1356](../../Assets/Scripts/Trading/TradingController.cs#L1356) (Critical), [:45](../../Assets/Scripts/Trading/TradingController.cs#L45) | High / Critical |
| `ChartMovement` | 무포지션 중계 / 보유 중 ROE ±15·±30 돌파 | [:728](../../Assets/Scripts/Trading/TradingController.cs#L728) (Low), [:857](../../Assets/Scripts/Trading/TradingController.cs#L857)·[:863](../../Assets/Scripts/Trading/TradingController.cs#L863) (Normal) | Low / Normal |

### 3.2 경로 B — 매매 (`TradingController`, 20종)

`OutputSpecificEventDialogue` 경유는 매처 치환자 `{leverage}` `{margin}` `{roe:F1}` `{maxObserved:F1}`를 모두 씁니다.

| 키 | 트리거 | 위치 | 우선순위 / 말풍선 분류 |
|---|---|---|---|
| `ToggleManualBlocked` | 요미 임시 락 중 수동 전환 / 오버도즈 중 / Playing 아님 / 장 마감 | [:161, 168, 177, 186](../../Assets/Scripts/Trading/TradingController.cs#L161) | High / General |
| `ToggleManualStart` · `ToggleManualAuto` | 전환 성공 (세이브 복원의 `forceRestore`는 무음) | [:199](../../Assets/Scripts/Trading/TradingController.cs#L199) · [:203](../../Assets/Scripts/Trading/TradingController.cs#L203) | High / General |
| `RoeNegative40` · `RoeNegative65` | 플레이어가 고른 베팅 + 가짜 신호, ROE −40 / −65 돌파 1회 (−40은 **멘탈 −15 동반**) | [:467](../../Assets/Scripts/Trading/TradingController.cs#L467) · [:473](../../Assets/Scripts/Trading/TradingController.cs#L473) | High / General |
| `RoeNegative50` | 플레이어 선택이 아닌 가짜 신호, −50 돌파 | [:481](../../Assets/Scripts/Trading/TradingController.cs#L481) | High / General |
| `RoePositive50` · `100` · `200` | 진짜 신호(뇌동매매 포함), ROE 50~100 / 100~200 / ≥ 200 | [:489](../../Assets/Scripts/Trading/TradingController.cs#L489) · [:494](../../Assets/Scripts/Trading/TradingController.cs#L494) · [:499](../../Assets/Scripts/Trading/TradingController.cs#L499) | Normal / ChartMovement (10초 쿨다운) · High / ChartMovement |
| `EmergencyWaterRiding` | 가짜 신호 + HoldToMitigateLoss·StandardAuto, ROE −92~−75 → 자동 물타기 | [:528](../../Assets/Scripts/Trading/TradingController.cs#L528) | High / General |
| `EventHoldMitigateLoss` | 쉴드 만료 + HoldToMitigateLoss → **대사 후 즉시 청산(청산 대사 생략)** | [:664](../../Assets/Scripts/Trading/TradingController.cs#L664), [:670](../../Assets/Scripts/Trading/TradingController.cs#L670) | High / PositionClosed |
| `EventGreedyHoldWin` | 쉴드 만료 + GreedyHold & ROE > 0 → 즉시 청산 | [:679](../../Assets/Scripts/Trading/TradingController.cs#L679) | High / PositionClosed |
| `MentalOverdoseRecover` | 오버도즈 매매 중 멘탈이 오버도즈를 벗어남, 5초마다 반복 | [:748](../../Assets/Scripts/Trading/TradingController.cs#L748) | High / MentalChange |
| `MentalOverdoseStart` | 오버도즈 35초 창 안에서 **ROE ≤ −70**, 5초마다 반복 | [:759](../../Assets/Scripts/Trading/TradingController.cs#L759) | High / MentalChange |
| `OverdoseExecute` | 멘탈 0 → `DelayedOverdoseRoutine`, `{leverage}`는 125 | [:1479](../../Assets/Scripts/Trading/TradingController.cs#L1479) | High / MentalChange |
| `TradeFailedInsufficientMargin` | 증거금 ≤ 0 또는 잔고 ≤ 1 (AI / 수동) | [:911](../../Assets/Scripts/Trading/TradingController.cs#L911), [:1023](../../Assets/Scripts/Trading/TradingController.cs#L1023) | High / General |
| `CostumeBuffActivated` | 수익 청산 + (바니걸 & AI 모드 / 비키니 & 수동 모드 / 지뢰계 항상) | [:1132-1142](../../Assets/Scripts/Trading/TradingController.cs#L1132) | High / General |
| `GameOver` | 청산 후 잔고 ≤ 10 | [:1235](../../Assets/Scripts/Trading/TradingController.cs#L1235) | Critical / PositionClosed |
| `FundingPaid` · `FundingReceived` | 매시 정각 펀딩비 지불 / 수령, 증거금의 1% 이상일 때만. **`{amount}`** = 금액 | [:391](../../Assets/Scripts/Trading/TradingController.cs#L391) | Low / ChartMovement |

### 3.3 경로 B — 바이탈·멘탈 기믹 (12종)

| 키 | 트리거 | 위치 | 우선순위 / 말풍선 분류 |
|---|---|---|---|
| `HealthWarning50` · `40` · `15` | 체력 50 / 40 / 15% 하향 돌파. 여러 경계를 한 번에 넘으면 가장 심각한 것 하나 | [TraderStatus.cs:367-385](../../Assets/Scripts/TraderStatus.cs#L367) | High / MentalChange |
| `MentalAnxious` | 멘탈 Stable → Anxious | [:532](../../Assets/Scripts/TraderStatus.cs#L532) | Normal / MentalChange |
| `MentalRecovered` | 멘탈 → Stable 회복 | [:543](../../Assets/Scripts/TraderStatus.cs#L543) | Normal / MentalChange |
| `ImpulsiveTrade` | 뇌동매매 발동 (4연패, 또는 Danger 진입 시 40%) | [:632](../../Assets/Scripts/TraderStatus.cs#L632) | Normal / General |
| `UnrealizedLossPanic` | ROE ≤ −20을 25초 유지 (반복) | [MentalDrainGimmickController.cs:244](../../Assets/Scripts/AI/MentalDrainGimmickController.cs#L244) | Normal / GimmickTriggered |
| `LosingStreak2` · `3` · `4` | 2 / 3 / 4연속 이상 손절 (4는 뇌동매매 예약) | [:286](../../Assets/Scripts/AI/MentalDrainGimmickController.cs#L286) · [:290](../../Assets/Scripts/AI/MentalDrainGimmickController.cs#L290) · [:295](../../Assets/Scripts/AI/MentalDrainGimmickController.cs#L295) | Normal / GimmickTriggered |
| `수동매매책임전가` | 수동 매매 손실 (연패 1~3). `{leverage}` `{margin}` | [:308](../../Assets/Scripts/AI/MentalDrainGimmickController.cs#L308) | Normal / GimmickTriggered |
| `MissedSignalFomo` | AI 모드에서 진입 포기한 신호가 8~60초 뒤 수익 자리로 판명 | [:396](../../Assets/Scripts/AI/MentalDrainGimmickController.cs#L396) | Normal / GimmickTriggered |

### 3.4 경로 B — AI 두뇌 (`AITradingBrain`, 9종)

| 키 | 트리거 | 위치 | 우선순위 / 말풍선 분류 |
|---|---|---|---|
| `EventSignal_PlayerTrue` · `PlayerFalse` · `AITrue` · `AIFalse` | 돌발 이벤트 신호 주입 시 골든타임 예고 (플레이어 선택 × 진위). **현재 도달 불가** (§10 YD-2) | [AITradingBrain.cs:149](../../Assets/Scripts/AI/AITradingBrain.cs#L149) · [:154](../../Assets/Scripts/AI/AITradingBrain.cs#L154) · [:162](../../Assets/Scripts/AI/AITradingBrain.cs#L162) · [:167](../../Assets/Scripts/AI/AITradingBrain.cs#L167) | High / ChartMovement |
| `ChartHint_TrapDetected_High` · `GoodEntry_High` · `Normal_High` | 수동 진입 직후, 차트 공부 LV ≥ 7 또는 LV ≥ 4 & `Random ≤ GetSignalAccuracy()` — 처리 중 신호가 가짜 / 진짜 / 없음. **`{position}`** = 진입 방향 | [:682-684](../../Assets/Scripts/AI/AITradingBrain.cs#L682) | High / ChartMovement |
| `ChartHint_Confused_Low` · `BlindTrust_Low` | 그 외, 50:50. `{position}` | [:689](../../Assets/Scripts/AI/AITradingBrain.cs#L689) | High / ChartMovement |

### 3.5 경로 B — 성장·보스·모달·이벤트 (18종)

| 키 | 트리거 | 위치 | 표시 |
|---|---|---|---|
| `SkillUpgraded_ChartStudy` · `_CubePatience` · `_BookJudgment` | 스킬 강화 완료. **`{level}`** = 오른 뒤 레벨. 예전 공용 키 `SkillUpgraded`는 더 이상 조회하지 않습니다 | [TraderLevelSystem.cs:470](../../Assets/Scripts/Trading/TraderLevelSystem.cs#L470) | 말풍선 High / SkillUpgraded |
| `BossAppear_Day3` · `6` · `9` · `12` · `15` · `18` · `20` | 보스 등장 (`CurrentBoss` 변경 감지). **`{boss}`** = 보스 이름. 스토리 모드는 보스가 꺼져 있음 | [BossBattleUIController.cs:261](../../Assets/Scripts/UI/BossBattleUIController.cs#L261) | 말풍선 Critical / GimmickTriggered |
| `SettlementProfit` · `SettlementLoss` · `SettlementEven` | 일일 정산 반응 — 당일 손익 부호 (±$0.005 불감대) | [DailySettlementUIController.cs:844](../../Assets/Scripts/UI/DailySettlementUIController.cs#L844) | 정산 카드, 코드가 “ ”로 감쌈 |
| `GameOverFinal_Bankruptcy` · `_Overdose` · `_Success` · `_None` | 게임오버 최종 메시지 (엔딩 종류) | [GameOverUIController.cs:297](../../Assets/Scripts/UI/GameOverUIController.cs#L297) | 게임오버 카드, 코드가 “ ”로 감쌈 |
| `ChoiceEventFallback` | 돌발 이벤트 템플릿에 요미 대사가 없을 때 (지금은 전부) | [ChoiceEventController.cs:557](../../Assets/Scripts/Events/ChoiceEventController.cs#L557) | 이벤트 팝업 카드 |

---

## 4. 트리거 동작 메모

키·조건·위치는 §3에 있습니다. 여기에는 대사를 쓸 때 알아야 할 **동작상의 특징**만 남깁니다.

### 4.1 포지션 진입·청산·차트 (경로 A)

- 포지션 스위칭은 먼저 `ClosePosition`을 거치므로 청산 대사가 매처 쿨다운(5초)을 걸어 **진입 대사가 자주 누락**됩니다. 수동 진입 직후에는 차트 힌트(§3.4)가 이어집니다.
- 청산 대사는 포지션 정보가 초기화되기 **직전**에 뽑습니다. `suppressDialogue`는 이벤트 쉴드 만료(`EventHoldMitigateLoss`·`EventGreedyHoldWin`)에서만 `true`입니다.
- 무포지션 `ChartMovement`는 이벤트 쉴드 중이거나 `IsOverridingTrend`일 때 6초 간격으로 요청하고, **신호 페이즈가 있을 때만**(`requiredChartTrend` Pump/Dump) 말이 됩니다.
- 보유 중 `ChartMovement`(ROE ±15/±30, 12초 간격)는 **`position`이 `Long`/`Short`/`Any`인 엔트리만 후보**입니다 (§10 YD-1).
- 수익 청산에 의상 버프가 걸리면 직전의 `CostumeBuffActivated`가 매처 쿨다운을 걸어 청산 대사가 나오지 않습니다.

### 4.2 돌발 이벤트 포지션 실시간 중계

[ProcessEventPositionReaction, TradingController.cs:450](../../Assets/Scripts/Trading/TradingController.cs#L450). 돌발 선택 이벤트로 열린 포지션이 **이벤트 쉴드** 안에 있는 동안만 동작합니다. `EventHoldMitigateLoss`·`EventGreedyHoldWin`은 카테고리 이름과 달리 **대사 직후 청산**합니다 (§10 YD-5).

### 4.3 바이탈·멘탈 전이 (`TraderStatus`)

- 체력 경계값은 AI 기만 티어(40% Tier 3, 15% Tier 4)와 맞춘 것입니다. 50%부터는 체력 감소가 같은 양만큼 멘탈로 번집니다.
- 멘탈 전이(`MentalAnxious`·`MentalRecovered`)는 **Playing 상태에서만**, **실시간 20초 쿨다운** ([SayMentalTransition, :572](../../Assets/Scripts/TraderStatus.cs#L572)). 쿨다운은 DB에 대사가 없어도 소모됩니다.
- `ImpulsiveTrade`는 챌린지 모드에서도 요청되지만 매매 자체는 중단됩니다 (§10 YD-9).

### 4.4 멘탈 소모 기믹 (`MentalDrainGimmickController`)

모두 `TriggerGimmickDialogue(키)` → **Normal / GimmickTriggered** (6초 쿨다운) ([:70](../../Assets/Scripts/AI/MentalDrainGimmickController.cs#L70)). Playing이 아니거나 고속 경과 중이면 기믹 자체가 돌지 않습니다.

- 미실현 손실 기믹은 차트 공부 LV.10 또는 치료 아이템 사용 후 꺼집니다.
- 강제 청산은 `OnPositionLiquidated`라 연패 카운트에 들어가지 않습니다.
- 이 대사들은 High 청산 대사 직후에 Normal로 들어와 큐에 쌓이고, 청산 대사가 길면 TTL 5초가 지나 버려질 수 있습니다.

### 4.5 AI 두뇌 (`AITradingBrain`)

- 차트 힌트([ProvideChartHintToPlayer, :670](../../Assets/Scripts/AI/AITradingBrain.cs#L670))는 수동 진입 직후 1회 ([TradingController.cs:1079-1082](../../Assets/Scripts/Trading/TradingController.cs#L1079)). DB에 그 키의 대사가 없으면 힌트 없이 끝납니다.
- 힌트는 **플레이어 진입 방향과 신호 방향을 비교하지 않습니다** (§10 YD-8). 보통 직전의 High `PositionOpened` 대사 뒤에 큐로 대기합니다 (TTL 8초).
- 골든타임 예고([HandleEventSignalReaction, :139](../../Assets/Scripts/AI/AITradingBrain.cs#L139))는 현재 도달 불가입니다 (§10 YD-2).

### 4.6 스토리·보스 독백 (`GameManager`) — 순서형 목록

순서가 있는 독백은 DB가 아니라 `GameManager`의 문자열 목록으로 재생합니다. **다섯 목록 모두 비어 있습니다.**

| 목록 | 위치 | 재생 시점 |
|---|---|---|
| `day6Monologue` | [GameManager.cs:707](../../Assets/Scripts/GameManager.cs#L707) | 6일차 아침 (스토리 모드, 만화 후) |
| `day16Monologue` | [:709](../../Assets/Scripts/GameManager.cs#L709) | 16일차 아침 |
| `winReaction` / `loseReaction` | [:975](../../Assets/Scripts/GameManager.cs#L975) / [:984](../../Assets/Scripts/GameManager.cs#L984) | 보스전 승리 / 패배 (정산 직후) |
| `bossIntro` | [PlayBossMorningSequence, :1197](../../Assets/Scripts/GameManager.cs#L1197) | 보스 날 아침 |

- 재생기: [PlayStoryMonologueAndWait, :711](../../Assets/Scripts/GameManager.cs#L711) — 줄마다 Critical / **Tutorial** 말풍선, 0.5초 대기 후 클릭으로 넘기고, 끝나면 말풍선을 닫고 다음 단계(보스 스폰·개장·엔딩)로 넘어갑니다. **목록이 비면 즉시** 말풍선을 닫고 다음 단계로 갑니다.
- 16일차 첫 줄을 위약금 꺼짐에 맞춰 런타임 교체하던 코드는 2026-10-08에 대사와 함께 삭제했습니다.
- 보스는 스토리 모드에서 꺼져 있어(`BossesEnabled = false`) 보스 독백은 엔드리스·챌린지에서만 재생됩니다.

### 4.7 아이템 사용 — 시스템 문구 (유지)

[ItemUser.cs:137](../../Assets/Scripts/Items/ItemUser.cs#L137)의 `{아이템명} 복용 (효과: {타입} +{수치})`는 요미 화법이 아닌 **시스템 안내**라 지우지 않았습니다. 지금 요미 말풍선에 나오는 유일한 문구입니다 (§10 YD-18).

---

## 5. 돌발 선택 이벤트의 요미 대사 — 채널 B

### 5.1 언제 뜨는가

[ChoiceEventController.OnGameMinuteAdvanced, :223-339](../../Assets/Scripts/Events/ChoiceEventController.cs#L223)

- **차단 조건**: Playing 아님 / 멘탈 Overdose / 고속 경과(예약을 15~45분 뒤로 미룸) / 10시 이전·24시 / 직전 이벤트로부터 인게임 60분 또는 실시간 10초 미만.
- **정기 이벤트**: 하루 상한 [DailyEventCap, :175-193](../../Assets/Scripts/Events/ChoiceEventController.cs#L175) — 스토리 외 2회, 스토리는 데이트 후 남은 매매 시간이 6시간 이상이면 2 / 3시간 이상 1 / 미만 0. 첫 이벤트는 10:00~16:00, 다음은 +60분 이후 최대 +300분(23:20 상한). 예약 시각이 되면 **확률 판정 없이** 뜹니다.
- **멘탈 위기 이벤트**: `MentalRatio ≤ 0.15`, 하루 1회, 실시간 180초 쿨다운 ([:316](../../Assets/Scripts/Events/ChoiceEventController.cs#L316)) → `EVENT_*` 풀의 `LowMental` 이벤트.

### 5.2 대사 출처 (전부 비어 있음)

| 이벤트 풀 | 쓰이는 경우 | 요미 대사 칸 | 현재 |
|---|---|---|---|
| **템플릿** 242개 (`Resources/Events/Templates/`) | 정기 이벤트 기본 | `EventLogicTemplateSO.FallbackMonologues` | 빈 리스트 |
| **EVENT_\*** 30개 (`Resources/Events/`) | 템플릿 풀이 비었을 때 / 템플릿 옵션·텍스트가 깨졌을 때 / 멘탈 위기 이벤트 / 디버그 지정 / P2P | `ChoiceEventSO.AIMonologue` | 빈 문자열 |
| `ChoiceEventRuntimeData` 기본 15개 | `Resources/Events`에 `ChoiceEventSO`가 하나도 없을 때 | 코드 리터럴 | 빈 문자열 |

- 템플릿 대사 해석: [ResolveYomiLine, :545](../../Assets/Scripts/Events/ChoiceEventController.cs#L545) — 후보 중 랜덤 → 비면 DB `ChoiceEventFallback` → 그것도 없으면 빈 문자열.
- **요미 대사는 템플릿 성공 판정에 들어가지 않습니다** ([TryResolveEventText, :521](../../Assets/Scripts/Events/ChoiceEventController.cs#L521)). 2026-10-08 이전에는 들어 있었는데, 그대로 두면 대사가 빈 템플릿 242개가 전부 "깨진 템플릿"이 되어 이벤트가 하드코딩 `EVENT_*` 풀로만 뜹니다. 같은 이유로 `EventLogicTemplateSO.HasFallbackText`([:84](../../Assets/Scripts/Events/EventLogicTemplateSO.cs#L84))도 제목·본문만 봅니다.
- **대사는 팝업이 열리기 전에 한 번 정해집니다.** 선택지·결과·멘탈·카테고리와 무관합니다.
- 템플릿 이름은 `Template_{카테고리}_{흐름}_{리스크}_{세션}` (10 × 4 × 3 × 2 = 240 + 수작업 2개). 제목·본문·선택지 문구는 그대로 남아 있습니다(요미 대사가 아닌 기사·버튼 텍스트).
- EVENT_12(`AI_DEPENDENCY`)는 제목·본문·선택지 설명에 "오빠"가 들어 있습니다 — 요미 대사 칸이 아니라 이벤트 서술이라 남겼습니다.

### 5.3 어떻게 보이는가

[ChoiceEventPopupUIController.cs](../../Assets/Scripts/Events/ChoiceEventPopupUIController.cs)

1. 전면 "BREAKING NEWS!" 오버레이: 페이드 인 0.22초 → 펄스 유지 1.05초 → 페이드 아웃 0.28초 (비스케일 시간).
2. 기사 패널(제목·본문·요미 인용 카드)이 **한 번에** 나타남. 타이프라이터 없음.
3. 인용 카드 텍스트 ([:133-139](../../Assets/Scripts/Events/ChoiceEventPopupUIController.cs#L133)):
   ```
   <color=#06B6D4><b>YOMI // AI MARKET ANALYST</b></color>
   <color=#CFFAFE>“{대사}”</color>      ← 대사가 있을 때만
   ```
   - 200자에서 자르고 "…" (`DisplayMaxMonologueLength`, [:24](../../Assets/Scripts/Events/ChoiceEventPopupUIController.cs#L24))
   - **대사가 없으면 머리글 한 줄만** 표시합니다. 예전의 존댓말 폴백("시장 데이터가 불안정해요…")은 삭제했습니다.
4. 초상·표정 없음. 카드 높이는 텍스트에 맞춰 늘어남(최소 118).

### 5.4 선택한 뒤

팝업 닫기 → 효과 적용([:632](../../Assets/Scripts/Events/ChoiceEventController.cs#L632)) → 시간 재개([:639](../../Assets/Scripts/Events/ChoiceEventController.cs#L639)). **결과 팝업도, 선택별 반응 대사도 없습니다.** 이후 요미는 일반 말풍선으로만 반응합니다.

1. 포지션 진입 → `PositionOpened`, Safe 선택지는 청산 → `PositionClosed` (§3.1)
2. 쉴드 동안 ROE 중계 → `RoeNegative*` / `RoePositive*` / `EmergencyWaterRiding` (§3.2)
3. 쉴드 만료 → `EventHoldMitigateLoss` / `EventGreedyHoldWin` (§3.2)
4. 골든타임 예고 → 도달 불가 (§10 YD-2)

> `Assets/Scripts/Events/Story/`(EventCatalog·EventView 등)는 선택지별 요미 답변(`EventChoice.Reply`)과 표정을 가진 **별개의 미연시식 스토리 이벤트 시스템**입니다. 트레이딩 팝업과 무관하고, 샘플 1개(`EVT_SAMPLE_001`)뿐이며 게임 내 호출자가 없습니다. 샘플의 요미 줄·답변도 비웠습니다(§13).

---

## 6. 일일 정산의 요미 반응 — 채널 C

### 6.1 흐름

1. 24:00 — 열린 포지션 강제 청산 → HUD에 `PositionClosed` 요청 (곧 덮임).
2. `GameManager.ProcessDailySettlementWithStory()` → `OnDayEnded`.
3. [DailySettlementUIController.HandleDayEnded, :130](../../Assets/Scripts/UI/DailySettlementUIController.cs#L130) — Settlement 상태일 때만, 하루 1회.
4. "`<날짜>` COMPLETE / 오늘 거래 종료 · 24:00 / DAILY SETTLEMENT READY" 카드 (0.22 + 0.95 + 0.28초).
5. `ShowSettlement` — 정산 모달이 0.28초 슬라이드 인. **반응 대사는 즉시 표시**(타이프라이터 없음), 진행 버튼도 즉시 활성.
6. 진행 → 검은 화면 + `SleepingYomi` 스프라이트 + "YOMI IS RESTING..." 2.55초 → 다음 날.

### 6.2 반응 대사 선택 — [:266-277](../../Assets/Scripts/UI/DailySettlementUIController.cs#L266)

| 순위 | 조건 | 대사 | 상태 라벨 |
|---|---|---|---|
| 1 | 오늘 스토리 이벤트가 위약금 & `dialogueMessage` 있음 | `TodayEvent.dialogueMessage` | PENALTY IMPOSED (빨강) |
| 2 | 그 외 → [GetImmediateReaction, :844](../../Assets/Scripts/UI/DailySettlementUIController.cs#L844) | DB `SettlementProfit` / `SettlementLoss` / `SettlementEven` (당일 손익 부호, ±$0.005) | LOCAL SUMMARY READY (회색) |

- 손익 = `총자산 − 그날 시작 자산`. DB에 대사가 없으면 반응 칸이 비어 있습니다.
- **위약금 대사**: `GameScene.unity`의 `GameManager.storyEvents` 5개 모두 `dialogueMessage`가 빈 문자열입니다(5·11·16일차 3줄을 2026-10-08에 삭제). `StoryPenaltiesEnabled = false` ([GameManager.cs:352](../../Assets/Scripts/GameManager.cs#L352))라 1순위 분기는 원래도 도달 불가였습니다.

### 6.3 표정 — [GetSettlementEmotion, :817](../../Assets/Scripts/UI/DailySettlementUIController.cs#L817)

| 당일 수익률 | 표정 | | 당일 수익률 | 표정 |
|---|---|---|---|---|
| ≥ +20% | Euphoria | | ≤ −20% | Tearful |
| ≥ +5% | Confident | | ≤ −5% | Despairing |
| > 0 | Pleased | | < 0 | Anxious |
| 본전 | Relieved | | | |

정산 카드의 초상(`Characters/Emotions/{표정}`)과 라벨 "YOMI / {EMOTION}"을 바꾸고, HUD 캐릭터에도 `ShowEmotion(표정, 5초)`를 겁니다 (말풍선은 없음). **표정은 7단계인데 대사 키는 3종**입니다.

### 6.4 요미의 방 정산

요미의 방에서 취침하면 [RoomSettlementUIBootstrap.cs](../../Assets/Scripts/UI/RoomSettlementUIBootstrap.cs)가 같은 `DailySettlementUIController`·`GameOverUIController`를 방 캔버스에 붙입니다. 로직은 GameScene과 100% 동일합니다. 단 이번 세션에 GameScene을 한 번도 거치지 않았으면 매처가 없어 반응이 비어 있습니다 (§2.1).

---

## 7. 게임오버 화면 — 채널 C

[GameOverUIController.cs](../../Assets/Scripts/UI/GameOverUIController.cs) — `GameManager.OnGameOverEvent` 수신. 오버도즈 엔딩이면 HUD 말풍선이 닫힐 때(`IsBalloonActive == false`)까지 기다렸다가 뜹니다.

"FINAL YOMI MESSAGE" 카드 — [GetFinalMessage, :297](../../Assets/Scripts/UI/GameOverUIController.cs#L297): DB `GameOverFinal_{엔딩}`을 조회해 “ ”로 감쌉니다. 엔딩은 `Bankruptcy`(표정 Tearful) / `Overdose`(Obsessive) / `Success`(Euphoria) / `None`(Despairing). UI를 만들 때 넣던 기본 텍스트도 빈 문자열로 바꿨습니다.

엔딩 사유·상세 문구("자본금 전액 손실…" 등)는 요미 대사가 아닌 시스템 문구라 남아 있습니다. 0.4초 페이드, 타이프라이터 없음. 이와 별도로 HUD에서는 청산 후 잔고 ≤ 10일 때 DB `GameOver`가 먼저 요청됩니다 (§3.2).

---

## 8. 표정(감정) 연동

### 8.1 감정 19종과 스프라이트

`TraderEmotion` ([TraderEmotion.cs](../../Assets/Scripts/AI/TraderEmotion.cs)): Euphoria, Confident, Pleased, Relieved, Affectionate, Focused, Suspicious, Anxious, Frustrated, Regretful, Jealous, Panicked, Despairing, Furious, Tearful, Manic, Obsessive, Exhausted, Vengeful.

- 로드: [LoadEmotionSprites, AIVisualController.cs:383-394](../../Assets/Scripts/AI/AIVisualController.cs#L383) → `Resources/{의상 경로}/Emotions/{이름}`, 없으면 `Characters/Emotions/{이름}`. 기본 + 의상 13벌 모두 19장씩 갖춰져 있습니다.
- 오버도즈 상태는 전용 `States/Overdose` 스프라이트.
- **DB 엔트리에는 표정 필드가 없습니다.** `mentalState`는 매칭 필터일 뿐입니다.

### 8.2 대사마다 표정 자동 평가 — [TraderEmotionEvaluator.cs:8-99](../../Assets/Scripts/AI/TraderEmotionEvaluator.cs#L8)

말풍선이 시작될 때마다 `Evaluate(ROE, 멘탈, 체력비, 카테고리, 대사 텍스트)` → `ShowEmotion(결과, max(4초, 표시 시간))`. 첫 일치가 이깁니다.

1. 게임오버이거나 대사에 `강제청산/게임오버/파산/청산 소진/Overdose 확정/연쇄 붕괴` → Tearful (`2단계` → Vengeful, `3단계` → Obsessive)
2. 멘탈 Overdose → Manic
3. 체력 ≤ 10% → Exhausted
4. 카테고리 ItemUsed → Relieved
5. GimmickTriggered + 대사에 `FOMO/놓친/휩소` → Regretful
6. 카테고리 SkillUpgraded → Confident
7. ChartMovement + 대사에 `조기 종료/미리/이벤트/포지션 종료` → Regretful
8. 무포지션: Danger → Despairing / Anxious → Anxious / 그 외 Focused
9. 보유 중 ROE 구간: > +30 Euphoria · > +5 Confident · > 0 Focused · > −5 Suspicious · > −20 Frustrated · 그 외 Panicked (각 구간에서 멘탈 Danger/Anxious면 한 단계 어둡게)

→ **대사 텍스트의 키워드가 표정을 바꿉니다.** 새 대사를 쓸 때 위 키워드를 넣거나 빼면 표정이 달라집니다.

말풍선이 없을 때는 [:283-293](../../Assets/Scripts/AI/AIVisualController.cs#L283)의 유휴 갱신이 `General` 카테고리로 재평가합니다. 정산·게임오버 화면은 이 평가기를 거치지 않고 표정을 직접 지정합니다.

---

## 9. 데이터 자산·저작 도구·검증

### 9.1 매매 대사 DB — JSON으로 넣는다

| 도구 | 메뉴 | 동작 |
|---|---|---|
| [YomiDialogueTools.cs](../../Assets/Editor/YomiDialogueTools.cs) | `FX Overdose/Dialogue/Import Database from JSON` | 루트 `DialogueDB_Import.json` → 에셋 `entries` **전체 교체** (Undo 지원) |
| 〃 | `FX Overdose/Dialogue/Export Database to JSON` | 에셋 → 루트 `DialogueDB_Export.json` |

- `DialogueDB_Import.json`은 현재 `{"entries": []}`입니다. Import는 **덮어쓰기**라, 인스펙터에서 고친 엔트리는 Import 전에 Export로 먼저 JSON에 반영해야 합니다.
- 810줄을 "접두 3 × 본문 3 × 접미 3" 조합으로 찍어내던 `YomiDialogueGenerator.cs`(`Generate Menhera Dialogue DB`)는 **2026-10-08에 삭제**했습니다. 실행하면 에셋을 통째로 옛 대사로 덮어쓰기 때문입니다.

### 9.2 일상 대사 DB — 런타임 미사용

`Assets/YomiDailyDialogueDatabase.asset` — 엔트리 0개. 생성기 `YomiDailyDialogueGenerator.cs`도 2026-10-08에 삭제했습니다. **이 에셋을 참조하는 코드·씬·프리팹이 없습니다.**

### 9.3 돌발 이벤트 텍스트 생성기

| 도구 | 메뉴 | 동작 |
|---|---|---|
| [GenerateTemplateFallbackText.cs](../../Assets/Scripts/Editor/GenerateTemplateFallbackText.cs) | `Tools/FX OVERDOSE/Generate Template Fallback Text` | "빈 항목만 채우기" / "전체 덮어쓰기". **제목·본문·선택지 문구만** 만듭니다 — 요미 대사 풀은 2026-10-08에 걷어냈습니다 |
| [ChoiceEventAssetGenerator.cs](../../Assets/Editor/ChoiceEventAssetGenerator.cs) | `Tools/FX OVERDOSE/Generate 15 Choice Event Assets` (이름과 달리 **30개** 생성) | `EVENT_*.asset` 30개를 리터럴로 생성·덮어쓰기. 요미 대사 인자(4번째)는 전부 `""` |
| `ChoiceEventDebugMenu.cs` | `FX Overdose/Debug/Force Choice Event (템플릿)` / `(하드코딩 이벤트)` / `Validate Event Templates (자산 점검)` | 디버그 강제 발동·점검. 점검은 `HasFallbackText`(제목·본문)를 봅니다 |

### 9.4 화법 린트 — `python yomi_dialogue_lint.py`

바이블 §4 규칙 검사, 위반 시 exit 1.

| 대상 | 길이 상한 | 화법 검사 | 현재 |
|---|---|---|---|
| `YomiDialogueDatabase.asset` | 80 | ✅ | 0줄 |
| `YomiDailyDialogueDatabase.asset` | 60 | ✅ | 0줄 |
| `EVENT_*.AIMonologue` | 150 | ✅ | 빈 값 |
| 템플릿 `FallbackMonologues` | 60 | ✅ (정규식 버그, §10 YD-20) | 0줄 |
| `CS_SOURCES` 목록의 .cs | — | "마스터"·AI 자기지칭만 | 471줄 통과 |

- 규칙: ① "마스터" 호칭 금지 ② AI 자기지칭(재부팅·학습 데이터·나는 AI 등) 금지 ③ 존댓말 금지(상대를 비꼬는 한 문장은 예외) ④ 표면별 길이.
- ⚠️ **현재 exit 1입니다.** 화법 위반은 0건이지만 미연시 **[대화 테이블] 최소 분량 검사 12건**(시간대별 호감도 0 토픽 ≥ 1·밤 ≥ 2, 힌트 풀 8개 각 ≥ 5줄)이 실패합니다. 대사를 지운 데 따른 예상된 결과이고, 요미의 방 대화를 다시 채우면 풀립니다.
- 예전 하드코딩 폴백은 린트 범위 밖이었지만 이제 그 자리가 전부 DB라, **DB에 넣는 대사는 모두 길이·화법 검사를 받습니다.** `GameManager` 독백 목록(§4.6)만 여전히 범위 밖입니다.

### 9.5 폰트

- `Tools/Prebake All Scripts Text into Font` ([PrebakeTMPFont.cs](../../Assets/Scripts/Editor/PrebakeTMPFont.cs))는 `*.cs`에 더해 **`YomiDialogueDatabase.asset`과 `Resources/Events/**/*.asset`도** 스캔합니다(`\uXXXX`를 풀어서 셈). 2026-10-08 생성기 삭제로 DB 대사가 `.cs`에 없게 되어 추가했습니다. **대사를 넣은 뒤에는 반드시 다시 실행하십시오.**
- `Tools/FX OVERDOSE/Prepopulate Font Asset`([ChoiceEventFontPrepopulator.cs](../../Assets/Editor/ChoiceEventFontPrepopulator.cs))는 Static 폰트에 글자를 추가하려 해 **실효가 없습니다** (§10 YD-22).
- 아틀라스에 없는 글자는 폴백 동적 폰트로 렌더되어 **문장 중간에 글꼴이 바뀝니다**.

---

## 10. 확인된 결함·불일치

전부 정적 분석이며 플레이 검증은 하지 않았습니다. **동작** = 플레이어가 체감하는 오동작, **데이터** = 대사 자산 문제, **도구** = 검증·문서 불일치. 2026-10-08 전면 삭제 이후의 상태를 "현재" 칸에 적었습니다 — 데이터 결함은 대사와 함께 대부분 사라졌고, **동작 결함은 대사를 다시 채우는 순간 그대로 돌아옵니다.**

### 10.1 동작

| ID | 문제 | 근거 | 수정 방향 | 현재 |
|---|---|---|---|---|
| **YD-1** | 포지션 보유 중 ROE ±15/±30 차트 대사가 나오지 않음 | 옛 DB의 ChartMovement 108줄이 전부 `position: None`이었음. 매처는 `Long`/`Short`일 때 `Long_*`·`Any_*` 키만 조회 | 보유 중 대사는 `position` Any(또는 Long/Short)로 태깅 | 저작 시 주의. (`directionHint` LLM 잔재 문자열은 2026-10-08 삭제) |
| **YD-2** | 돌발 이벤트 골든타임 예고 4종 도달 불가 | `ApplyOptionEffects`([:632](../../Assets/Scripts/Events/ChoiceEventController.cs#L632))가 신호를 동기 발행한 뒤 `ResumeGame`([:639](../../Assets/Scripts/Events/ChoiceEventController.cs#L639)). 수신 측 [AITradingBrain.cs:105](../../Assets/Scripts/AI/AITradingBrain.cs#L105)가 Playing이 아니면 반환 | 재개 후 주입하거나, 골든타임 대사만 상태 검사 앞으로 빼기 | 그대로 |
| **YD-3** | 멘탈 태그 체계 불일치로 청산 대사 소유자가 뒤바뀜 | 옛 DB `mentalState`는 Panicked/Euphoria/Focused/Furious/Stable/Overdose, 런타임은 Stable/Anxious/Danger/Overdose | 런타임 4단계로 태깅하거나 소유자 점수를 멘탈보다 높이기 | 저작 시 주의 |
| **YD-4** | 매처 쿨다운 5초가 경로 A·B에 공유되어 대사가 삼켜짐 | 경로 B 성공이 `lastDialogueTime`을 갱신 | 쿨다운을 경로 A 안에서만 갱신 | **영향 확대** — 경로 B 호출부가 하드코딩 자리까지 늘어남 (§2.2) |
| **YD-5** | `EventHoldMitigateLoss`·`EventGreedyHoldWin`은 "버티자" 이름인데 코드는 대사 직후 청산 | [TradingController.cs:664-680](../../Assets/Scripts/Trading/TradingController.cs#L664) | "여기서 정리할게" 류로 쓰거나 이름대로 홀딩 구현 | 저작 시 주의 |
| **YD-6** | `MentalOverdoseStart`는 진입 시가 아니라 ROE ≤ −70에서 5초마다 반복 | [:756-760](../../Assets/Scripts/Trading/TradingController.cs#L756) | 이름 정정 또는 트리거 이동 | 그대로 |
| **YD-7** | `OverdoseExecute`가 방향과 무관하게 "롱"을 말함 | 옛 DB 9줄 | 방향 중립 문구 | 대사 삭제로 소멸 (저작 시 주의) |
| YD-8 | 차트 힌트가 플레이어 진입 방향을 신호 방향과 비교하지 않음 | [AITradingBrain.cs:670-700](../../Assets/Scripts/AI/AITradingBrain.cs#L670) | `TargetPercentageDelta` 부호와 `playerPos` 비교 | 그대로 |
| YD-9 | 뇌동매매 대사는 챌린지 모드에서도 요청되지만 매매는 중단됨 | [TraderStatus.cs:632](../../Assets/Scripts/TraderStatus.cs#L632) | 요청을 실행 성공 뒤로 | 그대로 |
| YD-10 | 오버도즈 중 말풍선이 뜨면 `States/Overdose`가 `Emotions/Manic`으로 덮이고 복구되지 않음 | [AIVisualController.cs:436-458](../../Assets/Scripts/AI/AIVisualController.cs#L436) | `ShowEmotion`에서 오버도즈 비주얼 존중 | 그대로 |
| YD-11 | 매처가 GameScene에만 있어, GameScene 미방문 세션에서 DB 대사가 전부 무음 | §2.1 | 매처를 프리팹화해 방·정산 경로에서도 띄우기 | **영향 확대** — 정산 반응·게임오버 메시지도 이제 DB라 방에서 시작한 세션의 방 정산은 반응이 빔 |

### 10.2 데이터

| ID | 문제 | 현재 |
|---|---|---|
| YD-12 | 코드가 요청하는 11개 카테고리가 DB에 없어 하드코딩 폴백만 나감 | **해소** — 폴백 삭제, 모든 자리가 DB 키 조회 (§3) |
| YD-13 | "주인님" 9줄이 `requiredCostumeId: Any` (바이블은 메이드 의상 한정) | 대사 삭제로 소멸. 저작 시 `Maid` 지정 |
| YD-14 | 돌발 이벤트 템플릿 대사가 고유 23줄뿐, 카테고리·리스크 미반영 | 대사 삭제로 소멸. 생성기 풀도 제거 |
| YD-15 | 정산 반응은 손익 부호 3종 — 규모·체력·일차 미반영 (표정은 7단계) | 구조는 그대로 (DB 키 3종) |
| YD-16 | 돌발 이벤트에 선택 후 결과 반응 대사가 없음 | 그대로 |
| YD-17 | 기본 15개 이벤트의 구 어투, 팝업 빈 대사 폴백의 존댓말 | **해소** — 둘 다 삭제 |
| YD-18 | 아이템 사용 말풍선이 요미 화법이 아닌 시스템 문구 | 그대로 (의도적으로 남김, §4.7) |
| YD-19 | 감정 스프라이트 Furious·Affectionate·Jealous는 트레이딩 흐름에서 도달 불가 | 그대로 |

### 10.3 도구·문서

| ID | 문제 | 현재 |
|---|---|---|
| YD-20 | 린트가 템플릿 대사를 4칸 들여쓴 접힘 줄에서 놓침 ([yomi_dialogue_lint.py:120](../../yomi_dialogue_lint.py)) | 템플릿 대사 0줄이라 지금은 무관. 다시 쓰면 해당 |
| YD-21 | 린트 `CS_SOURCES`에 삭제된 파일 2개(`LLMSafeGenerator.cs`, `PlaceholderDialogueProvider.cs`), `LEN_LLM` 이름 잔재 | 그대로 |
| YD-22 | `ChoiceEventFontPrepopulator`가 Static 폰트에 글자를 추가하려 해 실효 없음 | 그대로 (프리베이크가 이벤트 에셋을 스캔하게 되어 필요성은 줄어듦) |
| YD-23 | `CLAUDE.md`의 EVENT_* 풀 사용 조건 서술이 실제보다 좁음 | 그대로 |
| YD-24 | 바이블 §5.1 제목 "감정 20종" ↔ 코드 19종 | 그대로 |
| YD-25 | 잔재: `EventCategory.DailySettlement`·`HealthChange` 미사용, `DialogueWaitTimeout` 미사용, "GENERATING YOMI COMMENT...", 템플릿 헤더 "(LLM 실패 시 사용…)", 메뉴명 "Generate 15 …"(30개) | 그대로 |
| YD-26 | 일기 기능이 살아 있는 것처럼 적힌 문서 | 그대로 |

---

## 11. 대사를 채울 때

### 11.1 넣을 곳

| 채우려는 것 | 넣을 곳 | 이어서 할 일 |
|---|---|---|
| HUD 말풍선·정산 반응·게임오버 메시지·이벤트 폴백 (§3의 키 62종) | `DialogueDB_Import.json`에 엔트리 추가 → `FX Overdose/Dialogue/Import Database from JSON` | 린트 → 프리베이크 → 플레이 확인 |
| 돌발 이벤트 템플릿 대사 | 템플릿 에셋의 `FallbackMonologues` 직접 편집 (생성기는 더 이상 만들지 않음) | 린트 → 프리베이크 (이벤트 에셋도 스캔함) |
| EVENT_* 대사 | `ChoiceEventAssetGenerator.cs`의 4번째 인자 → 재생성 (에셋을 덮어씀) | 린트 → 프리베이크 |
| 6·16일차·보스 독백 | `GameManager.cs`의 목록 5개 (§4.6) | 프리베이크 |
| 요미의 방 대화·인사·방향 힌트 | `YomiTalkTopics.cs` (§13) | 린트(대화 테이블 최소 분량) → 프리베이크 |
| 스토리 이벤트 | `EventCatalog.cs` | `FXOverdose/Debug/Validate Event Data` → 프리베이크 |

### 11.2 DB 엔트리 작성법

```json
{
    "entries": [
        { "text": "…", "eventCategory": "HealthWarning50" },
        { "text": "…", "eventCategory": "PositionClosed", "mentalState": "Anxious",
          "position": "Any", "marketTrend": "Any", "isProfit": false, "requiredOwner": "AI",
          "requiredChartTrend": "Any", "minAbsolutePnL": -99999, "maxAbsolutePnL": -1,
          "minTradeDuration": -1, "maxTradeDuration": 9999, "maxHealthLimit": 999, "requiredCostumeId": "Any" }
    ]
}
```

- **경로 B 키**(§3.2~§3.5)는 `text`와 `eventCategory`만 읽습니다.
- **경로 A 키**(§3.1)는 점수 필드까지 읽습니다. 필드: `text`, `marketTrend`, `mentalState`, `position`, `requiredDirection`, `isProfit`, `requiredLeverage`, `requiredHeroLevel`, `requiredSkillLevel`, `isHighMarginRisk`, `eventCategory`, `requiredOwner`, `requiredChartTrend`, `minAbsolutePnL`, `maxAbsolutePnL`, `minTradeDuration`, `maxTradeDuration`, `maxHealthLimit`, `requiredCostumeId`. **위 예시처럼 모든 필드를 적으십시오.** `YomiDialogueEntry`에는 매개변수 없는 생성자가 없어, JSON에서 빠진 필드는 클래스 초기값(`"Any"` 등)이 아니라 빈 값(null/0)으로 들어올 수 있습니다. 특히 `requiredChartTrend`가 비면 `"Any"`가 아니라서 경로 A에서 **항상 탈락(−9999)**합니다.
  - `mentalState`는 런타임 4단계(Stable/Anxious/Danger/Overdose)로 (YD-3).
  - 보유 중 `ChartMovement`는 `position`을 Any·Long·Short로 (YD-1).
  - `eventCategory`를 비우면 세 동작 모두의 후보가 됩니다 (§2.3).
- 길이: 말풍선 **80자**, 돌발 이벤트 템플릿 **60자**, EVENT_* **150자** (린트 기준).
- 호칭은 "오빠", 자칭은 "요미"/"나", 반말 (바이블 §4). 표정 키워드(§8.2) 확인.

---

## 12. 제거된 기능 (이력)

| 기능 | 상태 | 비고 |
|---|---|---|
| **요미 대사 전체 (트레이딩·미연시)** | **2026-10-08 삭제** | 출력 장치와 트리거는 유지, 하드코딩 자리는 DB 키 조회로 전환. §13 |
| 대사 DB 생성기 (`YomiDialogueGenerator`, `YomiDailyDialogueGenerator`) | 2026-10-08 삭제 | 실행 시 옛 대사로 에셋을 덮어씀 |
| 돌발 이벤트 요미 대사 LLM 생성 | 2026-09-22 제거 | 템플릿 단독(A안)으로 정리. [Trading_LLM_Removal_Plan.md](../P2_03_LLM_Architecture/Trading_LLM_Removal_Plan.md) |
| 정산 LLM 일기 | 2026-07-28 UI 경로 제거, 08-13 생성기 삭제 | 23:50 선요청 + 정산 시 최대 8초 대기 구조였음 |
| `HandleAIDecisionMade` 말풍선 | 제거 | `currentAction`을 넘기지 않아 후보 전량이 −9999 ([AIVisualController.cs:788-790](../../Assets/Scripts/AI/AIVisualController.cs#L788) 주석) |
| `AITradingBrain.HandlePositionClosed` 반성 대사 | 2026-10-06 삭제 (DEAD-3) | 분기만 있고 본문이 비어 있었음 |
| 요미 지출 예고 + 정산 지출 대사 | 2026-10-06 삭제 (f9da7ed) | 정기 지출과 함께 제거. [Trading_System_Master.md](Trading_System_Master.md) §23 |
| 체력 임계·멘탈 전이 대사 | 2026-10-06 추가 (DEAD-1, DEAD-2) → 2026-10-08 대사 삭제 | 트리거는 `HealthWarning*`·`MentalAnxious`·`MentalRecovered` 키로 남음 |
| 펀딩비 알림 대사 | 2026-10-06 추가 (REAL-1) → 2026-10-08 대사 삭제 | `FundingPaid`·`FundingReceived` 키로 남음 |

---

## 13. 2026-10-08 요미 대사 전면 삭제 기록

### 13.1 지운 것

- **데이터**: §0.1 표 전부. 이벤트 에셋 272개(템플릿 242 + EVENT_* 30)는 요미 대사 칸만 비웠고 제목·본문·선택지는 그대로입니다.
- **하드코딩 → DB 키**: `TraderStatus`(체력 경고 3·멘탈 전이 2·뇌동매매 1), `MentalDrainGimmickController`(미실현 손실·연패 3·FOMO·책임 전가 폴백), `AITradingBrain`(골든타임 폴백 4·차트 힌트 폴백 5), `TraderLevelSystem`(스킬 3), `TradingController`(펀딩비 2), `BossBattleUIController`(보스 등장 8), `DailySettlementUIController`(정산 3), `GameOverUIController`(최종 메시지 4 + UI 기본 텍스트), `ChoiceEventController`(템플릿 폴백 1), `YomiDialogueMatcher`("데이터베이스가 연결되지 않았어!" → null), `ChoiceEventPopupUIController`(존댓말 폴백 삭제).
- **순서형 독백 → 빈 목록**: `GameManager`의 `day6Monologue`·`day16Monologue`·`winReaction`·`loseReaction`·`bossIntro`, 16일차 첫 줄 런타임 교체 코드.
- **미연시**: `YomiTalkTopics.cs`의 토픽 9편(`All`), 시간대 인사 4줄(`GreetingFor`), 방향 힌트 표 8개(40줄). 타입·API·작성 지침 주석은 남겼습니다. 스토리 이벤트 샘플 `EVT_SAMPLE_001`은 요미의 줄·답변만 비우고 노드 구조(지문·선택지)는 검증 도구용으로 남겼습니다.
- **도구**: 대사 DB 생성기 2개 삭제, 템플릿 텍스트 생성기의 요미 대사 풀 제거.

### 13.2 지우면서 바꾼 동작

| 변경 | 이유 |
|---|---|
| 템플릿 성공 판정·`HasFallbackText`에서 요미 대사 조건 제거 | 그대로 두면 242개 템플릿이 전부 "깨진 템플릿"이 되어 이벤트가 `EVENT_*` 풀로만 뜸 |
| 이벤트 팝업은 대사가 없으면 머리글만 | 존댓말 폴백 문구 제거 |
| 요미의 방 인사는 빈 문자열이면 발화하지 않음 ([YomiRoomManager.cs:271](../../Assets/Scripts/DatingSim/YomiRoom/YomiRoomManager.cs#L271)) | 빈 말풍선 방지. 토픽이 없으면 '대화하기'는 "지금 시간대에 나눌 이야기가 없어요.", 힌트 표가 비면 힌트 미발급 — 둘 다 기존 가드 |
| 차트 힌트는 DB에 대사가 없으면 반환 | 폴백 삭제 후 `null.Replace` 예외 방지 |
| 프리베이크가 DB·이벤트 에셋도 스캔 | 생성기 삭제로 DB 대사가 `.cs`에 없어짐 (§9.5) |

### 13.3 재사용 금지 ID

`YomiTalkTopics`의 토픽 ID는 세이브의 대화 이력(`TalkTopicsSeenTotal`·`TalkCompletedFlags`·`TalkChoiceHistory`)에 남아 있으므로 새 토픽에 쓰지 마십시오 (TS5): `TALK_GREET_001` `TALK_PLAY_001` `TALK_MEAL_001` `TALK_ANGER_001` `TALK_SLEEP_001` `TALK_LOVE_001` `TALK_ANGER_002` `TALK_LOVE_002` `TALK_LOVE_003`. 같은 목록이 `YomiTalkTopics.All`의 주석에도 있습니다.
