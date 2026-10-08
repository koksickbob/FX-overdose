# 트레이딩 파트 요미 대사 마스터 문서

- **작성일**: 2026-10-06
- **기준**: `Dev_koksickbob_Laptop` (e3589f3)
- **성격**: 기획서가 아니라 **현재 코드에 실제로 살아 있는 것만** 적은 역공학 레퍼런스입니다. 파일·행 번호는 소스에서 직접 확인했습니다. 코드와 다르면 코드가 맞습니다. 모든 분석은 **정적 분석**이며 플레이 검증은 하지 않았습니다. §4.3의 청산 대사 풀 판정표만 매처 점수 공식을 Python으로 재현해 DB 810줄에 직접 돌려 얻었습니다.
- **범위**: `GameScene` 트레이딩 루프에서 요미가 말하는 **모든 경로** — HUD 말풍선, 돌발 선택 이벤트 팝업, 일일 정산, 게임오버, 스토리·보스 독백, 표정 연동, 데이터 자산, 저작·검증 도구. 미연시 파트(요미의 방 대화)는 트레이딩에 영향을 주는 접점(일일 방향 힌트)만 다룹니다.
- **구성**: §0~§9는 현재 동작의 기술, **§10은 확인된 결함·불일치**, §11은 대사를 추가·수정할 때의 작업 순서, §12는 제거된 기능의 이력입니다.

> 관련 문서: 트레이딩 시스템 전체는 [Trading_System_Master.md](Trading_System_Master.md), 요미 화법 규칙은 [P2_02_Yomi_Character_Bible.md](../P2_02_Worldbuilding/P2_02_Yomi_Character_Bible.md) §4, LLM 제거 기록은 [Trading_LLM_Removal_Plan.md](../P2_03_LLM_Architecture/Trading_LLM_Removal_Plan.md).

---

## 0. 한눈에 보기

**이 프로젝트에 LLM은 없습니다.** 트레이딩 파트의 요미 대사는 전부 ① 대사 DB(ScriptableObject) ② C# 하드코딩 리터럴 ③ 이벤트 에셋 텍스트 셋 중 하나에서 나옵니다.

```
                         ┌──────────────── 데이터 소스 ────────────────┐
                         │ YomiDialogueDatabase.asset (810줄, 21 카테고리) │
                         │ .cs 하드코딩 리터럴 (폴백 + 전용 대사)          │
                         │ 이벤트 에셋 (템플릿 242개 / EVENT_* 30개)       │
                         └───────────────────────────────────────────────┘
                                          │
   ┌──────────────────────────────────────┼─────────────────────────────────────┐
   ▼                                      ▼                                     ▼
[채널 A] HUD 말풍선                 [채널 B] 돌발 이벤트 팝업          [채널 C] 모달 화면
AIVisualController                   ChoiceEventPopupUIController        DailySettlementUIController
 · 우선순위 4단 + 큐 + 타이프라이터   · "YOMI // AI MARKET ANALYST" 카드 · 정산 반응 3줄
 · 대사마다 표정 자동 평가            · 선택 전 1줄, 즉시 표시            GameOverUIController
 · 매매·기믹·체력·멘탈·스토리 전부    · 표정 없음                         · 엔딩별 최종 메시지 4줄
                                                                         · 표정은 손익/엔딩으로 직접 지정
```

| 채널 | 경로 수 | 대사 출처 | 표정 |
|---|---|---|---|
| A. HUD 말풍선 | 약 30개 트리거 (§4) | DB 21 카테고리 + 하드코딩 | `TraderEmotionEvaluator`가 대사마다 자동 평가 (§8) |
| B. 돌발 이벤트 팝업 | 1 (§5) | 템플릿 `FallbackMonologues` 또는 `EVENT_*.AIMonologue` | 없음 |
| C. 정산·게임오버 모달 | 2 (§6, §7) | 하드코딩 | 화면이 직접 지정 + HUD 캐릭터에도 `ShowEmotion` |

### 0.1 대사 총량

| 출처 | 줄 수 | 런타임 사용 |
|---|---|---|
| `Assets/YomiDialogueDatabase.asset` | 810 (21 카테고리, 중복 0) | ✅ |
| 하드코딩 말풍선 리터럴 (.cs) | 약 50 (§4.6~§4.10) | ✅ |
| 돌발 이벤트 템플릿 `FallbackMonologues` | 726 (242개 × 3), **고유 문장 23개** | ✅ |
| `EVENT_*.asset`의 `AIMonologue` | 30 | ✅ (조건부, §5.1) |
| `ChoiceEventRuntimeData` 기본 이벤트 | 15 | 에셋이 하나도 없을 때만 |
| 정산 반응 / 게임오버 최종 메시지 | 3 / 4 | ✅ |
| 정산 위약금 대사 (`GameScene.unity`) | 3 | ❌ 위약금 꺼짐 (§6.2) |
| `Assets/YomiDailyDialogueDatabase.asset` | 100 (고유 70) | ❌ **아무도 읽지 않음** (§9.2) |

---

## 1. 말풍선 엔진 — `AIVisualController`

트레이딩 파트 요미 대사의 대부분이 이 컴포넌트 하나로 나갑니다. **이벤트를 구독하지 않습니다** — 모든 호출자가 `DisplayDialogueBalloon()`을 직접 부릅니다. 구독은 의상 변경(`CostumeManager.OnCostumesChanged`)과 아이템 소비(`Inventory.ItemConsumed` → 포즈) 두 개뿐입니다.

### 1.1 진입점과 열거형

- `DisplayDialogueBalloon(text, priority, category)` — [AIVisualController.cs:802](../../Assets/Scripts/AI/AIVisualController.cs#L802). 오버로드 [:792](../../Assets/Scripts/AI/AIVisualController.cs#L792)(Normal/General), [:797](../../Assets/Scripts/AI/AIVisualController.cs#L797)(General).
- `DialoguePriority` = `Low` / `Normal` / `High` / `Critical` ([:11-17](../../Assets/Scripts/AI/AIVisualController.cs#L11))
- `EventCategory` = `General` / `ChartMovement` / `MentalChange` / `HealthChange` / `GimmickTriggered` / `ItemUsed` / `PositionOpened` / `PositionClosed` / `SkillUpgraded` / `DailySettlement` / `Tutorial` ([:19-32](../../Assets/Scripts/AI/AIVisualController.cs#L19)). `DailySettlement`와 `HealthChange`는 **호출자가 없습니다.**

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

   [GetCategoryCooldown, :1158-1172](../../Assets/Scripts/AI/AIVisualController.cs#L1158)
4. **Critical**은 항상 선점.
5. **High** — Critical 표시 중이면 큐. High 표시 중이면 둘 다 `SkillUpgraded`일 때만 선점, 아니면 큐. GameOver 상태면 큐. 그 외엔 Low/Normal을 선점하거나 바로 시작.
6. **Low** (표시 중) — 큐의 첫 Low를 교체, 없으면 큐(10개 미만일 때).
7. **Normal** (표시 중) — 큐(10개 미만일 때).
8. 큐는 **우선순위 정렬 없는 FIFO**입니다. 꺼낼 때 TTL 검사: High/Critical 8초, Low/Normal 5초(요청 시각 기준). GameOver 상태에서는 TTL 무시.

### 1.3 표시 시간과 타이프라이터

[StartOrPreemptDialogue, :937-972](../../Assets/Scripts/AI/AIVisualController.cs#L937) / [TypewriterCoroutine, :974-996](../../Assets/Scripts/AI/AIVisualController.cs#L974)

| 값 | 공식 / 값 | 비고 |
|---|---|---|
| 타이프라이터 간격 | **0.03초/글자** | 코드 기본값 0.02, `GameScene.unity:1081` 직렬화 값이 우선 |
| 타이핑 후 유지 | `Clamp(글자수 × 0.07, 2.5, max(balloonDisplayDuration, 5))` = **2.5~8초** | `balloonDisplayDuration` 코드 4 / 씬 **8** |
| 선점 잠금 | `max(2.2, 글자수 × 0.05)`초 | `IsBalloonActive` 판정에 쓰임 |
| 시간 축 | `WaitForSeconds` (스케일 시간) | 설정 메뉴가 `timeScale = 0`이면 타이핑도 멈춤 |
| `Tutorial` 카테고리 | 자동으로 닫히지 않고 진행 화살표 표시 | 스토리 독백 전용 (§4.10) |

- 글자 수로 자르지 않습니다. TMP `Overflow = Ellipsis`, 자동 크기 19~27(×1.15), 글자색 `#CFFAFE`. 바이블의 "말풍선 80자" 제한은 넘치면 말줄임표로 잘리는 것을 피하기 위한 **저작 규칙**입니다.

---

## 2. 대사 DB와 매처 — `YomiDialogueDatabase` / `YomiDialogueMatcher`

### 2.1 구성

| 요소 | 위치 | 비고 |
|---|---|---|
| 엔트리 스키마 `YomiDialogueEntry` | [YomiDialogueData.cs](../../Assets/Scripts/AI/Dialogue/YomiDialogueData.cs) | text, eventCategory, position, marketTrend, mentalState, isProfit, requiredOwner, requiredChartTrend, PnL 범위, maxHealthLimit, requiredCostumeId 등 |
| DB 컨테이너 | [YomiDialogueDatabase.cs](../../Assets/Scripts/AI/Dialogue/YomiDialogueDatabase.cs) | 조회표 2개: `"{position}_{marketTrend}"` 키 / `eventCategory` 키 |
| 에셋 | `Assets/YomiDialogueDatabase.asset` | `Resources` 밖 → 씬 직렬화 참조로만 연결 |
| 매처 | [YomiDialogueMatcher.cs](../../Assets/Scripts/AI/Dialogue/YomiDialogueMatcher.cs) | `GameScene.unity`의 루트 오브젝트 `YomiDialogueMatcher`. `DontDestroyOnLoad` 싱글턴, 쿨다운 5초 |

> ⚠️ 매처는 **GameScene에만** 있습니다. GameScene을 한 번도 열지 않은 상태(예: 요미의 방에서 시작)에서는 `Instance == null`이고, DB 대사는 전부 조용히 실패합니다. 하드코딩 폴백이 있는 경로만 말합니다.

### 2.2 두 개의 조회 경로

| | **경로 A — `GetDialogue()`** (점수제) | **경로 B — `GetEventDialogue()`** (카테고리 랜덤) |
|---|---|---|
| 위치 | [YomiDialogueMatcher.cs:42-140](../../Assets/Scripts/AI/Dialogue/YomiDialogueMatcher.cs#L42) | [:145-163](../../Assets/Scripts/AI/Dialogue/YomiDialogueMatcher.cs#L145) |
| 유일한 호출 래퍼 | `TradingController.OutputYomiDialogue()` [:1690](../../Assets/Scripts/Trading/TradingController.cs#L1690) | `TradingController.OutputSpecificEventDialogue()` [:1666](../../Assets/Scripts/Trading/TradingController.cs#L1666) + 직접 호출 4곳 (§4.7~§4.9) |
| 쓰는 카테고리 | `PositionOpened` / `PositionClosed` / `ChartMovement` | 나머지 18종 + DB에 없는 11종 |
| 선택 | 점수 최고점 −5 이내 풀을 셔플 → 최근 15개 해시에 없는 첫 줄 | 카테고리 안에서 균등 랜덤 (반복 방지 없음) |
| 쿨다운 5초 | **검사함** (걸리면 `null`) | 검사 안 함. 단 성공 시 쿨다운 시각을 갱신 |
| 치환 | `{leverage}` `{margin}` | `{leverage}` `{margin}` `{roe:F1}` `{maxObserved:F1}` |
| 결과 없음 | 아무 말 안 함 | `OutputSpecificEventDialogue`는 아무 말 안 함 / 직접 호출부는 하드코딩 폴백 |

**쿨다운이 두 경로에 공유됩니다.** 경로 B 대사가 나가면 이후 5초 동안 경로 A(진입·청산·차트 대사)가 `null`이 됩니다. 실제로 겹치는 상황은 §10 YD-4.

### 2.3 경로 A 점수 공식 — `CalculateScore` [:165-277](../../Assets/Scripts/AI/Dialogue/YomiDialogueMatcher.cs#L165)

| 조건 | 점수 |
|---|---|
| `eventCategory == currentAction` | **+500** (불일치면 **−9999 = 탈락**) |
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

**DB에 있는 810줄의 eventCategory는 하나도 비어 있지 않으므로** 첫 줄의 게이트 때문에 경로 A는 사실상 "카테고리별 풀 + 세부 가중치"로 동작합니다.

**입력값** ([TradingController.cs:1690-1762](../../Assets/Scripts/Trading/TradingController.cs#L1690)):
- `position` = 현재 포지션 문자열 (`None`/`Long`/`Short`)
- `mentalState` = `TraderStatus.MentalState` (**`Stable`/`Anxious`/`Danger`/`Overdose`**) — DB 태그 체계와 다릅니다 (§10 YD-3)
- `owner` = 포지션 소유자, 무포지션이면 현재 매매 모드
- `actualChartTrend` = 신호 페이즈가 있으면 목표 변동률 부호로 `Pump`/`Dump`, 없으면 `Sideways`
- `marketTrend`는 ROE 부호로 Bull/Bear/Sideways를 만들지만 **DB 전 엔트리가 `Any`라 의미 없음**

---

## 3. DB 카테고리 전체표

810줄 = 21 카테고리. 각 27줄 풀은 `YomiDialogueGenerator`가 **접두 3 × 본문 3 × 접미 3** 조합으로 만든 것입니다 (§9.1).

| 카테고리 | 줄 수 | 경로 | 호출 위치 (TradingController.cs) | 예시 |
|---|---|---|---|---|
| `PositionOpened` | 27 | A | [:973](../../Assets/Scripts/Trading/TradingController.cs#L973), [:1081](../../Assets/Scripts/Trading/TradingController.cs#L1081) | 오빠! 이번 포지션은 진짜 느낌이 좋아! 제발제발 올라라 얍! |
| `PositionClosed` | 189 (7풀) | A | [:1214](../../Assets/Scripts/Trading/TradingController.cs#L1214), [:1362](../../Assets/Scripts/Trading/TradingController.cs#L1362) | §4.3 표 참조 |
| `ChartMovement` | 108 (4풀) | A | [:728](../../Assets/Scripts/Trading/TradingController.cs#L728), [:860](../../Assets/Scripts/Trading/TradingController.cs#L860), [:869](../../Assets/Scripts/Trading/TradingController.cs#L869) | 오빠오빠!! 지금 차트 떡상하는거 안보여?! … |
| `ToggleManualBlocked` | 27 | B | [:161-186](../../Assets/Scripts/Trading/TradingController.cs#L161) | 아 안돼! 지금은 요미 맘대로 안돼...! 무서워... |
| `ToggleManualStart` | 27 | B | [:199](../../Assets/Scripts/Trading/TradingController.cs#L199) | 오빠가 직접 할거야? 오빠의 신들린 매매... 요미 너무 기대돼! |
| `ToggleManualAuto` | 27 | B | [:203](../../Assets/Scripts/Trading/TradingController.cs#L203) | 흥... 오빠가 싼 똥은 요미가 다 치워야지... |
| `RoeNegative40` / `65` / `50` | 27 ×3 | B | [:467](../../Assets/Scripts/Trading/TradingController.cs#L467) / [:473](../../Assets/Scripts/Trading/TradingController.cs#L473) / [:481](../../Assets/Scripts/Trading/TradingController.cs#L481) | 오빠 제발!! 반토막 났잖아!! 빨리 빼라고!! |
| `RoePositive50` / `100` / `200` | 27 ×3 | B | [:489](../../Assets/Scripts/Trading/TradingController.cs#L489) / [:494](../../Assets/Scripts/Trading/TradingController.cs#L494) / [:499](../../Assets/Scripts/Trading/TradingController.cs#L499) | 오빠오빠!! 수익률 50% 돌파했어!! 조금만 더 버텨볼까?! |
| `EmergencyWaterRiding` | 27 | B | [:528](../../Assets/Scripts/Trading/TradingController.cs#L528) | 안돼... 여기서 포기할 순 없어! 물타기 들어간다!! |
| `EventHoldMitigateLoss` | 27 | B | [:664](../../Assets/Scripts/Trading/TradingController.cs#L664), [:670](../../Assets/Scripts/Trading/TradingController.cs#L670) | 제발... 지금 손절치면 너무 손해야... 무조건 반등 온다!! |
| `EventGreedyHoldWin` | 27 | B | [:679](../../Assets/Scripts/Trading/TradingController.cs#L679) | 아직이야... 여기서 익절하기엔 너무 아까워! 더 존버하자!! |
| `MentalOverdoseRecover` | 27 | B | [:748](../../Assets/Scripts/Trading/TradingController.cs#L748) | 하아... 하아... 요미 방금... 무슨 짓을 한 거지...? |
| `MentalOverdoseStart` | 27 | B | [:759](../../Assets/Scripts/Trading/TradingController.cs#L759) | 후후... 차트가 날 미치게 만드네... 다 부숴버릴거야!! |
| `OverdoseExecute` | 27 | B | [:1485](../../Assets/Scripts/Trading/TradingController.cs#L1485) | 이거야!! 상남자 특! {leverage}배 롱 드가자!! |
| `TradeFailedInsufficientMargin` | 27 | B | [:917](../../Assets/Scripts/Trading/TradingController.cs#L917), [:1029](../../Assets/Scripts/Trading/TradingController.cs#L1029) | 오빠 미쳤어? 돈도 없으면서 무슨 매매를 하겠다는 거야?! |
| `CostumeBuffActivated` | 27 | B | [:1138-1148](../../Assets/Scripts/Trading/TradingController.cs#L1138) | 짜잔~ 새 옷 입은 기념으로 수익금 뻥튀기 보너스야!! |
| `GameOver` | 27 | B | [:1241](../../Assets/Scripts/Trading/TradingController.cs#L1241) | 오빠... 우리 잔고가 0원이 됐어... 이제 진짜 끝이야... |

**코드가 요청하지만 DB에 없는 카테고리 11종** — 항상 하드코딩 폴백이 나갑니다 (§4.7~§4.9).

| 카테고리 | 호출 위치 |
|---|---|
| `SkillUpgraded` | [TraderLevelSystem.cs:475](../../Assets/Scripts/Trading/TraderLevelSystem.cs#L475) |
| `수동매매책임전가` (한글 키) | [MentalDrainGimmickController.cs:312](../../Assets/Scripts/AI/MentalDrainGimmickController.cs#L312) |
| `EventSignal_PlayerTrue` / `PlayerFalse` / `AITrue` / `AIFalse` | [AITradingBrain.cs:151-172](../../Assets/Scripts/AI/AITradingBrain.cs#L151) |
| `ChartHint_TrapDetected_High` / `GoodEntry_High` / `Normal_High` / `Confused_Low` / `BlindTrust_Low` | [AITradingBrain.cs:693-717](../../Assets/Scripts/AI/AITradingBrain.cs#L693) |

DB 필드 분포 요약: `marketTrend` 전부 Any / `position` Any 621·None 189 / `requiredCostumeId` 전부 Any / 방향·레버리지·레벨·고위험 필드 전부 0 / 치환자는 `{leverage}` 27줄(OverdoseExecute)만 사용. 최장 71자, 평균 45.5자.

---

## 4. HUD 말풍선 트리거 카탈로그

### 4.1 포지션 진입 — `PositionOpened` (경로 A)

| 트리거 | 위치 | 우선순위 |
|---|---|---|
| AI·이벤트·오버도즈·뇌동 진입 (`OpenPosition`) | [TradingController.cs:973](../../Assets/Scripts/Trading/TradingController.cs#L973) | High |
| 수동 LONG/SHORT 버튼 (`OpenPlayerPosition`) | [:1081](../../Assets/Scripts/Trading/TradingController.cs#L1081) | High |

27줄이 모두 같은 점수라 사실상 랜덤입니다. 포지션 스위칭은 먼저 `ClosePosition`을 거치므로 청산 대사가 매처 쿨다운을 걸어 **진입 대사가 자주 누락**됩니다. 수동 진입 직후에는 차트 힌트(§4.8)가 이어집니다.

### 4.2 차트 움직임 — `ChartMovement` (경로 A)

| 상황 | 위치 | 조건 | 결과 |
|---|---|---|---|
| **무포지션** 중계 | [:717-731](../../Assets/Scripts/Trading/TradingController.cs#L717) | 이벤트 쉴드 중이거나 `IsOverridingTrend`, 6초 간격, Low | 신호 페이즈가 있을 때만 출력 (`Pump`/`Dump` 필수). 풀 = 소유자 × 방향 4종 |
| **보유 중** ROE ±15 / ±30 돌파 | [:851-870](../../Assets/Scripts/Trading/TradingController.cs#L851) | 12초 간격, Normal | **현재 DB로는 한 줄도 나오지 않음** (§10 YD-1) |

무포지션 4풀: Player+Pump(Furious, "왜 안 타냐") / Player+Dump(Panicked) / AI+Pump(Euphoria) / AI+Dump(Focused).

### 4.3 청산 — `PositionClosed` (경로 A)

| 트리거 | 위치 | 우선순위 |
|---|---|---|
| 일반 청산 (`ClosePosition`, `suppressDialogue == false`) | [:1214](../../Assets/Scripts/Trading/TradingController.cs#L1214) | High |
| 강제 청산 (`TriggerLiquidation`) | [:1362](../../Assets/Scripts/Trading/TradingController.cs#L1362) | Critical |
| 24:00 강제 정리 | `GameManager.AdvanceOneMinute` → `ClosePosition` | High (곧 정산 화면이 덮음) |

대사는 포지션 정보가 초기화되기 **직전**에 뽑습니다. `suppressDialogue`는 이벤트 쉴드 만료(§4.4)에서만 `true`입니다.

**7개 하위 풀**

| 풀 | 조건 (DB 태그) | 예시 |
|---|---|---|
| ① 플레이어 익절 | Stable / Player / 익 / PnL 1~99,999 | 오빠...! 직접 익절하다니 제법인데? |
| ② 플레이어 손절 | Panicked / Player / 손 | 아 진짜... 왜 직접 손절 치고 난리야?! |
| ③ AI 익절 | Euphoria / AI / 익 | 짜잔~ 요미가 깔끔하게 익절했어! 칭찬해줘! |
| ④ AI 손절 | Stable / AI / 손 | 앗... 어쩔 수 없는 손절이었어… |
| ⑤ 대박 익절 | Euphoria / Any / 익 / PnL ≥ 1,000 | 하아앗...! 우리 방금 얼마 번거야?! |
| ⑥ 청산 | Overdose / AI / 손 | 오빠...... 방금... 청산당한거야? |
| ⑦ 저체력 익절 | Panicked / Any / 익 / 체력 ≤ 20 | 콜록... 돈은 벌었는데... 요미 너무 아파… |

**실제로 뽑히는 풀** (매처 재현, 체력 > 20, 마진비율 < 0.5 가정)

| 청산 | 멘탈 Stable | Anxious | Danger | Overdose |
|---|---|---|---|---|
| 플레이어 익절 | ① | ① | ① | ① |
| AI 익절 | **① ("직접 익절" 대사가 AI 매매에)** | ③ | ③ | ③ |
| 플레이어 손절 | **④ (AI 손절 대사)** | ② | ② | ⑥ |
| AI 손절 | ④ | ④ + ⑥ | ④ + ⑥ | ⑥ |
| 체력 ≤ 20 익절 | ①/③ + ⑦ 혼합 | | | |

- **⑤ 대박 익절 27줄은 어떤 조합에서도 뽑히지 않습니다** (PnL 5,000으로 돌려도 ①/③이 이김).
- 굵게 표시한 칸은 소유자가 어긋난 대사입니다. 원인은 §10 YD-3.
- 수익 청산에 의상 버프가 걸리면 직전의 `CostumeBuffActivated`가 매처 쿨다운을 걸어 청산 대사가 나오지 않습니다.

### 4.4 돌발 이벤트 포지션 실시간 중계 (경로 B)

돌발 선택 이벤트로 열린 포지션이 **이벤트 쉴드** 안에 있는 동안만 동작합니다. [ProcessEventPositionReaction, :450-502](../../Assets/Scripts/Trading/TradingController.cs#L450)

| 카테고리 | 조건 | 우선순위 / 카테고리 |
|---|---|---|
| `RoeNegative40` | 플레이어가 고른 베팅 + 가짜 신호, ROE −40 돌파 (1회). **멘탈 −15 동반** | High / General |
| `RoeNegative65` | 같은 조건, −65 돌파 (1회) | High / General |
| `RoeNegative50` | 플레이어 선택이 아닌 가짜 신호, −50 돌파 | High / General |
| `RoePositive50` | 진짜 신호, ROE 50~100 | Normal / ChartMovement (10초 쿨다운 적용) |
| `RoePositive100` / `200` | 100~200 / ≥ 200 | High / ChartMovement |
| `EmergencyWaterRiding` | 가짜 신호 + HoldToMitigateLoss·StandardAuto 모드, ROE −92~−75 → 자동 물타기 | High / General [:528](../../Assets/Scripts/Trading/TradingController.cs#L528) |
| `EventHoldMitigateLoss` | 쉴드 만료, HoldToMitigateLoss 모드 → **대사 후 즉시 청산(청산 대사 생략)** | High / PositionClosed |
| `EventGreedyHoldWin` | 쉴드 만료, GreedyHold 모드 & ROE > 0 → 즉시 청산 | High / PositionClosed |

`RoePositive*`는 뇌동매매(`isTrueSignal: true`로 열림)에도 나옵니다.

### 4.5 매매 모드 전환·오버도즈·증거금 부족 (경로 B)

| 카테고리 | 트리거 | 위치 |
|---|---|---|
| `ToggleManualBlocked` | 요미 임시 락 중 수동 전환 시도 / 오버도즈 중 / Playing 아님 / 장 마감 | [:161, 168, 177, 186](../../Assets/Scripts/Trading/TradingController.cs#L161) |
| `ToggleManualStart` / `ToggleManualAuto` | 전환 성공 (세이브 복원의 `forceRestore`는 무음) | [:199](../../Assets/Scripts/Trading/TradingController.cs#L199) / [:203](../../Assets/Scripts/Trading/TradingController.cs#L203) |
| `OverdoseExecute` | 멘탈 0 → `DelayedOverdoseRoutine`, 레버리지 125로 치환 | [:1485](../../Assets/Scripts/Trading/TradingController.cs#L1485) |
| `MentalOverdoseStart` | 오버도즈 35초 창 안에서 **ROE ≤ −70**, 5초마다 반복 | [:756-760](../../Assets/Scripts/Trading/TradingController.cs#L756) |
| `MentalOverdoseRecover` | 오버도즈 매매 중인데 멘탈이 오버도즈를 벗어남, 5초마다 반복 | [:744-749](../../Assets/Scripts/Trading/TradingController.cs#L744) |
| `TradeFailedInsufficientMargin` | 증거금 ≤ 0 또는 잔고 ≤ 1 (AI / 수동) | [:917](../../Assets/Scripts/Trading/TradingController.cs#L917), [:1029](../../Assets/Scripts/Trading/TradingController.cs#L1029) |
| `CostumeBuffActivated` | 수익 청산 + (바니걸 & AI 모드 / 비키니 & 수동 모드 / 지뢰계 항상) | [:1138-1148](../../Assets/Scripts/Trading/TradingController.cs#L1138) |
| `GameOver` | 청산 후 잔고 ≤ 10, Critical | [:1241](../../Assets/Scripts/Trading/TradingController.cs#L1241) |

### 4.6 바이탈 — 하드코딩 (`TraderStatus`)

| 트리거 | 대사 | 우선순위 / 카테고리 | 위치 |
|---|---|---|---|
| 체력 50% 하향 돌파 | 하아... 머리가 핑 돌아... 이제부터 지치면 멘탈도 같이 깎여, 오빠... | High / MentalChange | [TraderStatus.cs:367-385](../../Assets/Scripts/TraderStatus.cs#L367) |
| 체력 40% 하향 돌파 | 오빠... 눈이 자꾸 감겨... 지금 요미 판단 믿으면 큰일 날지도 몰라!! | 〃 | 〃 |
| 체력 15% 하향 돌파 | 더는 못 버텨... 요미 지금 제정신 아니야!! 오빠, 빨리 뭐라도 먹여줘...! | 〃 | 〃 |
| 멘탈 Stable → Anxious | 으음... 요미 슬슬 불안해지기 시작했어... 오빠, 우리 무리하지 말자 응...? | Normal / MentalChange | [:525-545](../../Assets/Scripts/TraderStatus.cs#L525) |
| 멘탈 → Stable 회복 | 후우~ 이제 좀 살 것 같아! 요미 다시 집중할 수 있어, 오빠! | 〃 | 〃 |
| 뇌동매매 발동 | 더는 못 참아!! 100배로 싹 다 복구한다!! | Normal / General | [:632](../../Assets/Scripts/TraderStatus.cs#L632) |

- 체력 경계를 한 번에 여러 개 넘으면 가장 심각한 것 하나만 말합니다. 경계값은 AI 기만 티어(40% Tier 3, 15% Tier 4)와 맞춘 것입니다.
- 멘탈 전이 대사는 **Playing 상태에서만**, **실시간 20초 쿨다운** ([SayMentalTransition, :572-580](../../Assets/Scripts/TraderStatus.cs#L572)).
- 뇌동매매는 4연패(§4.7) 또는 Danger 진입 시 40% 확률로 발동합니다.

### 4.7 멘탈 소모 기믹 — 하드코딩 (`MentalDrainGimmickController`)

모두 `TriggerGimmickDialogue()` → **Normal / GimmickTriggered** (6초 쿨다운) ([:69-75](../../Assets/Scripts/AI/MentalDrainGimmickController.cs#L69)). Playing이 아니거나 고속 경과 중이면 기믹 자체가 돌지 않습니다.

| 트리거 | 대사 | 위치 |
|---|---|---|
| ROE ≤ −20을 25초 유지 (반복) | 안돼 안돼 안돼!! 내 시드가... 갈려 나간다!! 물타기 해야 해, 아니 손절해야 해?! | [:244](../../Assets/Scripts/AI/MentalDrainGimmickController.cs#L244) |
| 2연속 손절 | 아씨, 꼬리만 털고 왜 반대로 가는데?! | [:286](../../Assets/Scripts/AI/MentalDrainGimmickController.cs#L286) |
| 3연속 손절 | 차트가 날 감시하고 조롱하는 게 분명해...! | [:290](../../Assets/Scripts/AI/MentalDrainGimmickController.cs#L290) |
| 4연속 이상 | 4연속 손절... 더는 못 참아! 지금 당장 100배로 싹 다 복구한다!! → 뇌동매매 예약 | [:295](../../Assets/Scripts/AI/MentalDrainGimmickController.cs#L295) |
| 수동 매매 손실 (연패 1~3) | DB `수동매매책임전가` 조회 → 없으므로 폴백 "거봐! 요미 말 안 듣고 오빠가 맘대로 쳐서 돈 날렸잖아!!" | [:301-318](../../Assets/Scripts/AI/MentalDrainGimmickController.cs#L301) |
| FOMO (AI 모드, 놓친 신호가 8~60초 뒤 수익 자리로 판명) | 아씨!! 휩소인 줄 알고 쫄아서 안 들어갔는데 진짜 수익 자리였잖아!! 저거 다 내 돈이었는데...!! | [:404](../../Assets/Scripts/AI/MentalDrainGimmickController.cs#L404) |

- 미실현 손실 기믹은 차트 공부 LV.10 또는 치료 아이템 사용 후 꺼집니다.
- 강제 청산은 `OnPositionLiquidated`라 연패 카운트에 들어가지 않습니다.
- 이 대사들은 High 청산 대사 직후에 Normal로 들어와 큐에 쌓이고, 청산 대사가 길면 TTL 5초가 지나 버려질 수 있습니다.

### 4.8 AI 두뇌 — 차트 힌트와 이벤트 골든타임 (`AITradingBrain`, DB 미보유 → 하드코딩 폴백)

**차트 힌트** — [ProvideChartHintToPlayer, AITradingBrain.cs:676-729](../../Assets/Scripts/AI/AITradingBrain.cs#L676). 수동 진입 직후 1회 ([TradingController.cs:1084-1088](../../Assets/Scripts/Trading/TradingController.cs#L1084)). High / ChartMovement.

| 등급 | 조건 | 분기 | 폴백 대사 (요약) |
|---|---|---|---|
| High | 차트 공부 LV ≥ 7, 또는 LV ≥ 4 & `Random ≤ GetSignalAccuracy()` | 처리 중 신호가 가짜 → `TrapDetected_High` | 꺄아악 오빠 멈춰!! 지금 {Long/Short} 들어간 거, 세력 년들이 파놓은 가짜 덫(Trap)이란 말야! … |
| | | 처리 중 신호가 진짜 → `GoodEntry_High` | 앗...! 우리 오빠 천재인가 봐!! 저항선 뚫는 완벽한 {…} 타점이야! … |
| | | 처리 중 신호 없음 → `Normal_High` | 오빠가 잡은 {…} 타점... 호가창 거래량이 붙고 있어! … |
| Low | 그 외 | 50% `Confused_Low` | 으응...? {…} 자리야...? 캔들이 막 꼬물거리는데 솔직히 잘 모르겠어... … |
| | | 50% `BlindTrust_Low` | 꺄아아 오빠가 {…} 샀다!! 뭔지 모르지만 무조건 떡상해라!! … |

- 힌트는 **플레이어 진입 방향과 신호 방향을 비교하지 않습니다.** "가짜 덫"은 신호가 가짜라는 뜻일 뿐, 플레이어가 그 덫 방향으로 들어갔는지는 보지 않습니다.
- 보통 직전의 High `PositionOpened` 대사 뒤에 큐로 대기합니다 (TTL 8초).

**이벤트 골든타임** — [HandleEventSignalReaction, AITradingBrain.cs:139-176](../../Assets/Scripts/AI/AITradingBrain.cs#L139). 돌발 이벤트가 신호를 강제 주입했을 때 "곧 빔이 터진다 / 함정 냄새가 난다"를 예고하는 4분기(플레이어 선택 × 진위). High / ChartMovement. **현재 도달 불가** (§10 YD-2).

### 4.9 성장·아이템·펀딩비 — 하드코딩

| 트리거 | 대사 | 우선순위 / 카테고리 | 위치 |
|---|---|---|---|
| 스킬 강화 완료 (DB `SkillUpgraded` 없음 → 폴백) | 차트 공부 완료! (LV.n) 눈알이 빠질 것 같지만 타점 분석 능력이 올랐어! / 큐브 풀기 완료! … / 독서 완료! … | High / SkillUpgraded | [TraderLevelSystem.cs:468-495](../../Assets/Scripts/Trading/TraderLevelSystem.cs#L468) |
| 아이템 사용 | `{아이템명} 복용 (효과: {타입} +{수치})` — 요미 화법이 아닌 시스템 문구 | Normal / ItemUsed | [ItemUser.cs:134-138](../../Assets/Scripts/Items/ItemUser.cs#L134) |
| 펀딩비 지불 (매시 정각, 증거금의 1% 이상) | 펀딩비 ${p:N0} 나갔어... 오래 들고 있으면 계속 새어 나가, 오빠! | Low / ChartMovement | [TradingController.cs:368-395](../../Assets/Scripts/Trading/TradingController.cs#L368) |
| 펀딩비 수령 | 펀딩비 ${p:N0} 들어왔어! 반대쪽 사람들이 내준 거야, 헤헤~ | 〃 | 〃 |

### 4.10 보스·스토리 독백 — 하드코딩

| 트리거 | 대사 | 우선순위 / 카테고리 | 위치 |
|---|---|---|---|
| 보스 등장 (`CurrentBoss` 변경 감지) | 3·6·9·12·15·18·20일차별 1줄 + 기본 `“{보스명} 등장!! 오빠, 우리 실력을 보여주자!!”` | Critical / GimmickTriggered | [BossBattleUIController.cs:255-274](../../Assets/Scripts/UI/BossBattleUIController.cs#L255) |
| 6일차 아침 (스토리 모드, 만화 후) | `day6Monologue` 3줄 (레버리지 해금 동기 부여) | Critical / **Tutorial** (클릭 진행) | [GameManager.cs:702-706](../../Assets/Scripts/GameManager.cs#L702) |
| 16일차 아침 | `day16Monologue` 3줄. 위약금이 꺼져 있어 첫 줄을 "슬슬 한계야... 이 속도로는 사채업자 마감일까지 절대 못 맞춰!! …"로 런타임 교체 | 〃 | [:708-712](../../Assets/Scripts/GameManager.cs#L708), [:455-458](../../Assets/Scripts/GameManager.cs#L455) |
| 보스 날 아침 | `[{보스명} 등장!]` / 보스 설명 / 보스 대사 | 〃 | [:1210-1214](../../Assets/Scripts/GameManager.cs#L1210) |
| 보스전 승리 / 패배 (정산 직후) | `[{보스명} 격파 결과]` + 보스 대사 2줄 / `[… 패배 결과]` + "말도 안 돼... 내가, 내가 졌다고...?" … | 〃 | [:978-995](../../Assets/Scripts/GameManager.cs#L978) |

- 재생기: [PlayStoryMonologueAndWait, GameManager.cs:714-750](../../Assets/Scripts/GameManager.cs#L714) — 줄마다 0.5초 대기 후 클릭으로 넘기고, 끝나면 말풍선을 닫습니다.
- 보스는 스토리 모드에서 꺼져 있어(`BossesEnabled = false`) 보스 대사는 엔드리스·챌린지에서만 나옵니다.
- 보스 승패 대사 중 일부는 요미가 아니라 **보스의 말**인데 요미 말풍선으로 출력됩니다.

---

## 5. 돌발 선택 이벤트의 요미 대사 — 채널 B

### 5.1 언제 뜨는가

[ChoiceEventController.OnGameMinuteAdvanced, :223-339](../../Assets/Scripts/Events/ChoiceEventController.cs#L223)

- **차단 조건**: Playing 아님 / 멘탈 Overdose / 고속 경과(예약을 15~45분 뒤로 미룸) / 10시 이전·24시 / 직전 이벤트로부터 인게임 60분 또는 실시간 10초 미만.
- **정기 이벤트**: 하루 상한 [DailyEventCap, :175-193](../../Assets/Scripts/Events/ChoiceEventController.cs#L175) — 스토리 외 2회, 스토리는 데이트 후 남은 매매 시간이 6시간 이상이면 2 / 3시간 이상 1 / 미만 0. 첫 이벤트는 10:00~16:00, 다음은 +60분 이후 최대 +300분(23:20 상한). 예약 시각이 되면 **확률 판정 없이** 뜹니다.
- **멘탈 위기 이벤트**: `MentalRatio ≤ 0.15`, 하루 1회, 실시간 180초 쿨다운 ([:316](../../Assets/Scripts/Events/ChoiceEventController.cs#L316)) → `EVENT_*` 풀의 `LowMental` 이벤트.

### 5.2 어떤 대사가 고르는가

| 이벤트 풀 | 쓰이는 경우 | 요미 대사 출처 | 선택 |
|---|---|---|---|
| **템플릿** 242개 (`Resources/Events/Templates/`) | 정기 이벤트 기본 | `EventLogicTemplateSO.FallbackMonologues` (3줄) | 3줄 중 균등 랜덤, 빈 줄은 건너뜀 |
| **EVENT_\*** 30개 (`Resources/Events/`) | 템플릿 풀이 비었을 때 / 템플릿 옵션·텍스트가 깨졌을 때 / 멘탈 위기 이벤트 / 디버그 지정 / P2P | `ChoiceEventSO.AIMonologue` (1줄) | 이벤트당 고정 |
| `ChoiceEventRuntimeData` 기본 15개 | `Resources/Events`에 `ChoiceEventSO`가 하나도 없을 때 | 코드 리터럴 | 이벤트당 고정 |

- 템플릿 대사 해석: [ResolveYomiLine, :544-557](../../Assets/Scripts/Events/ChoiceEventController.cs#L544). 쓸 줄이 없으면 고정 문구 **"오빠...! 이거 지금 어떻게 할지 빨리 정해줘!"**.
- **대사는 팝업이 열리기 전에 한 번 정해집니다.** 선택지·결과·멘탈·카테고리와 무관합니다.
- 템플릿 이름은 `Template_{카테고리}_{흐름}_{리스크}_{세션}` (10 × 4 × 3 × 2 = 240 + 수작업 2개). 대사는 **흐름(Crash/Pump/Sideways/Whipsaw)만으로** 결정되어 726줄 중 고유 문장은 23개입니다 (§9.3).
- 치환자 없음. "오빠"는 에셋에 그대로 적혀 있습니다.

예시 (`Template_Whale_Crash_High_USSession`):
- 안 돼... 이 속도면 청산이야. 오빠, 제발 빨리 정해줘...!
- 밑이 안 보여... 받쳐줄 매수벽이 하나도 없어! 어떻게 해야 돼?!
- 요미 계산이 다 틀렸어... 오빠가 대신 정해줘. 위야, 아래야?!

### 5.3 어떻게 보이는가

[ChoiceEventPopupUIController.cs](../../Assets/Scripts/Events/ChoiceEventPopupUIController.cs)

1. 전면 "BREAKING NEWS!" 오버레이: 페이드 인 0.22초 → 펄스 유지 1.05초 → 페이드 아웃 0.28초 (비스케일 시간).
2. 기사 패널(제목·본문·요미 인용 카드)이 **한 번에** 나타남. 타이프라이터 없음.
3. 인용 카드 텍스트 ([:133-142](../../Assets/Scripts/Events/ChoiceEventPopupUIController.cs#L133)):
   ```
   <color=#06B6D4><b>YOMI // AI MARKET ANALYST</b></color>
   <color=#CFFAFE>“{대사}”</color>
   ```
   - 200자에서 자르고 "…" (`DisplayMaxMonologueLength`, [:24](../../Assets/Scripts/Events/ChoiceEventPopupUIController.cs#L24))
   - 빈 대사면 "시장 데이터가 불안정해요. 대응 방향을 정해 주세요." — **존댓말**이고 요미 화법이 아닙니다.
4. 초상·표정 없음. 카드 높이는 텍스트에 맞춰 늘어남(최소 118).

### 5.4 선택한 뒤

[OnOptionSelected, :601-648](../../Assets/Scripts/Events/ChoiceEventController.cs#L601) — 팝업 닫기 → 효과 적용 → 시간 재개. **결과 팝업도, 선택별 반응 대사도 없습니다.** 이후 요미는 일반 말풍선으로만 반응합니다.

1. 포지션 진입 → `PositionOpened` (§4.1), Safe 선택지는 청산 → `PositionClosed` (§4.3)
2. 쉴드 동안 ROE 중계 → `RoeNegative*` / `RoePositive*` / `EmergencyWaterRiding` (§4.4)
3. 쉴드 만료 → `EventHoldMitigateLoss` / `EventGreedyHoldWin` (§4.4)
4. 골든타임 예고 → 도달 불가 (§10 YD-2)

> `Assets/Scripts/Events/Story/`(EventCatalog·EventView 등)는 선택지별 요미 답변(`EventChoice.Reply`)과 표정을 가진 **별개의 미연시식 스토리 이벤트 시스템**입니다. 트레이딩 팝업과 무관하고, 현재 샘플 1개(`EVT_SAMPLE_001`)뿐이며 게임 내 호출자가 없습니다.

---

## 6. 일일 정산의 요미 반응 — 채널 C

### 6.1 흐름

1. 24:00 — 열린 포지션 강제 청산 → HUD에 `PositionClosed` 대사 (곧 덮임).
2. `GameManager.ProcessDailySettlementWithStory()` → `OnDayEnded`.
3. [DailySettlementUIController.HandleDayEnded, :130-146](../../Assets/Scripts/UI/DailySettlementUIController.cs#L130) — Settlement 상태일 때만, 하루 1회.
4. "`<날짜>` COMPLETE / 오늘 거래 종료 · 24:00 / DAILY SETTLEMENT READY" 카드 (0.22 + 0.95 + 0.28초).
5. [ShowSettlement, :231-294](../../Assets/Scripts/UI/DailySettlementUIController.cs#L231) — 정산 모달이 0.28초 슬라이드 인. **반응 대사는 즉시 표시**(타이프라이터 없음), 진행 버튼도 즉시 활성.
6. 진행 → 검은 화면 + `SleepingYomi` 스프라이트 + "YOMI IS RESTING..." 2.55초 → 다음 날.

### 6.2 반응 대사 선택 — [:266-277](../../Assets/Scripts/UI/DailySettlementUIController.cs#L266)

| 순위 | 조건 | 대사 | 상태 라벨 |
|---|---|---|---|
| 1 | 오늘 스토리 이벤트가 위약금 & `dialogueMessage` 있음 | `TodayEvent.dialogueMessage` | PENALTY IMPOSED (빨강) |
| 2 | 그 외 → [GetImmediateReaction, :843-850](../../Assets/Scripts/UI/DailySettlementUIController.cs#L843) | 아래 3줄 | LOCAL SUMMARY READY (회색) |

`GetImmediateReaction`은 **당일 손익의 부호만** 봅니다 (±$0.005 불감대). 손익 = `총자산 − 그날 시작 자산`.

| 조건 | 대사 |
|---|---|
| 수익 | “오빠, 오늘 기록 정리 중이야... 우리 목표에 조금 더 가까워졌지? ♥” |
| 손실 | “오빠... 오늘 손실 기록을 봐도 요미 버리면 안 돼. 내일은 꼭 되찾을게...” |
| 본전 | “오늘은 간신히 본전이네... 내일은 요미가 확실한 수익을 보여줄게.” |

**위약금 대사 (현재 출력 불가)** — `GameScene.unity`의 `GameManager.storyEvents`에 직렬화된 5·11·16일차 대사 3줄 (월세 1만 / 사채 이자 3만 / 카페 배상 10만 달러). `StoryPenaltiesEnabled = false` ([GameManager.cs:352](../../Assets/Scripts/GameManager.cs#L352))라 `DisableStoryPenaltiesIfNeeded()`가 모든 이벤트의 `isPenalty`를 꺼서 1순위 분기에 들어갈 수 없습니다.

### 6.3 표정 — [GetSettlementEmotion, :817-826](../../Assets/Scripts/UI/DailySettlementUIController.cs#L817)

| 당일 수익률 | 표정 | | 당일 수익률 | 표정 |
|---|---|---|---|---|
| ≥ +20% | Euphoria | | ≤ −20% | Tearful |
| ≥ +5% | Confident | | ≤ −5% | Despairing |
| > 0 | Pleased | | < 0 | Anxious |
| 본전 | Relieved | | | |

정산 카드의 초상(`Characters/Emotions/{표정}`)과 라벨 "YOMI / {EMOTION}"을 바꾸고, HUD 캐릭터에도 `ShowEmotion(표정, 5초)`를 겁니다 (말풍선은 없음). **표정은 7단계인데 대사는 3단계**입니다.

### 6.4 요미의 방 정산

요미의 방에서 취침하면 [RoomSettlementUIBootstrap.cs](../../Assets/Scripts/UI/RoomSettlementUIBootstrap.cs)가 같은 `DailySettlementUIController`·`GameOverUIController`를 방 캔버스에 붙입니다. **대사·표정 로직은 GameScene과 100% 동일**하고, 방 전용 정산 대사는 없습니다.

---

## 7. 게임오버 화면 — 채널 C

[GameOverUIController.cs](../../Assets/Scripts/UI/GameOverUIController.cs) — `GameManager.OnGameOverEvent` 수신. 오버도즈 엔딩이면 HUD 말풍선이 닫힐 때(`IsBalloonActive == false`)까지 기다렸다가 뜹니다.

"FINAL YOMI MESSAGE" 카드 — [GetFinalMessage, :296-305](../../Assets/Scripts/UI/GameOverUIController.cs#L296)

| 엔딩 | 대사 | 표정 |
|---|---|---|
| Bankruptcy | “다 잃어버렸어... 그래도 오빠, 요미를 혼자 두고 가지 마...” | Tearful |
| Overdose | “머릿속이 멈추질 않아... 오빠만 여기 남아 있으면 돼...” | Obsessive |
| Success | “해냈어, 오빠! 우리의 기록은 여기서 끝이 아니라 시작이야.” | Euphoria |
| 그 외 | “세션이 끝났어... 타이틀 화면에서 다시 만나자.” | Despairing |

0.4초 페이드, 타이프라이터 없음. 이와 별도로 HUD에서는 청산 후 잔고 ≤ 10일 때 DB `GameOver` 27줄 중 하나가 먼저 나갑니다 (§4.5).

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

→ **대사 텍스트의 키워드가 표정을 바꿉니다.** 대사를 새로 쓸 때 위 키워드를 넣거나 빼면 표정이 달라집니다.

말풍선이 없을 때는 [:283-293](../../Assets/Scripts/AI/AIVisualController.cs#L283)의 유휴 갱신이 `General` 카테고리로 재평가합니다. 정산·게임오버 화면은 이 평가기를 거치지 않고 표정을 직접 지정합니다.

---

## 9. 데이터 자산·저작 도구·검증

### 9.1 매매 대사 DB의 정본은 `.cs`다

| 도구 | 메뉴 | 동작 |
|---|---|---|
| [YomiDialogueGenerator.cs](../../Assets/Editor/YomiDialogueGenerator.cs) | `FX Overdose/Dialogue/Generate Menhera Dialogue DB` | 30개 그룹 × (접두 3 × 본문 3 × 접미 3) = **810줄**을 `DialogueDB_Import.json`으로 쓰고 곧바로 Import. **에셋을 통째로 덮어씁니다** |
| [YomiDialogueTools.cs](../../Assets/Editor/YomiDialogueTools.cs) | `FX Overdose/Dialogue/Export Database to JSON` | 에셋 → `DialogueDB_Export.json` (루트) |
| 〃 | `FX Overdose/Dialogue/Import Database from JSON` | `DialogueDB_Import.json` → 에셋 `entries` 전체 교체 (Undo 지원) |

- 루트의 `DialogueDB_Import.json`(git 추적)은 현재 에셋과 텍스트·카테고리·멘탈 태그가 완전히 일치합니다.
- **에셋을 인스펙터에서 직접 고치면** ① 생성기를 다시 돌리는 순간 사라지고 ② 폰트 프리베이크(.cs만 스캔)에 안 잡힙니다. 그래서 **대사의 정본은 `YomiDialogueGenerator.cs`**로 보는 것이 안전합니다.

### 9.2 일상 대사 DB — 런타임 미사용

`Assets/YomiDailyDialogueDatabase.asset` — 100줄(인사·식사·수면·놀이·분노·애정 각 10, 기본 40), 그중 30줄은 생성기([YomiDailyDialogueGenerator.cs](../../Assets/Editor/YomiDailyDialogueGenerator.cs), `FX Overdose/AI/Generate 100 Daily Dialogues`)가 무작위 복사로 채운 중복이라 고유 70줄. **이 에셋을 참조하는 코드·씬·프리팹이 없습니다.** 린트 대상이고 톤 레퍼런스로만 쓰입니다.

### 9.3 돌발 이벤트 대사 생성기

| 도구 | 메뉴 | 동작 |
|---|---|---|
| [GenerateTemplateFallbackText.cs](../../Assets/Scripts/Editor/GenerateTemplateFallbackText.cs) | `Tools/FX OVERDOSE/Generate Template Fallback Text` | "빈 항목만 채우기" / "전체 덮어쓰기". 대사는 `MonologuePool[흐름]`(흐름당 5줄, [:89-123](../../Assets/Scripts/Editor/GenerateTemplateFallbackText.cs#L89))에서 `StableHash(템플릿ID) % 5`부터 연속 3줄 — 결정적이라 git diff가 안정적. 이름을 해석 못 하면 고정 3줄 |
| [ChoiceEventAssetGenerator.cs](../../Assets/Editor/ChoiceEventAssetGenerator.cs) | `Tools/FX OVERDOSE/Generate 15 Choice Event Assets` (이름과 달리 **30개** 생성) | `EVENT_*.asset` 30개를 리터럴로 생성·덮어쓰기 |
| `ChoiceEventDebugMenu.cs` | `FX Overdose/Debug/Force Choice Event (템플릿)` / `(하드코딩 이벤트)` / `Validate Event Templates (자산 점검)` | 디버그 강제 발동·점검 |

### 9.4 화법 린트 — `python yomi_dialogue_lint.py`

바이블 §4 규칙 검사, 위반 시 exit 1. 현재 2,257줄 검사, 위반 0.

| 대상 | 길이 상한 | 화법 검사 |
|---|---|---|
| `YomiDialogueDatabase.asset` | 80 | ✅ |
| `YomiDailyDialogueDatabase.asset` | 60 | ✅ |
| `EVENT_*.AIMonologue` | 150 | ✅ |
| 템플릿 `FallbackMonologues` | 60 | ✅ (단 **정규식 버그로 대부분 누락**, §10 YD-20) |
| `CS_SOURCES` 목록의 .cs | — | "마스터"·AI 자기지칭만 |

규칙: ① "마스터" 호칭 금지 ② AI 자기지칭(재부팅·학습 데이터·나는 AI 등) 금지 ③ 존댓말 금지(상대를 비꼬는 한 문장은 예외) ④ 표면별 길이.

**린트 범위 밖**: `AITradingBrain.cs` / `TradingController.cs` / `TraderLevelSystem.cs` / `TraderStatus.cs` / `MentalDrainGimmickController.cs` / `BossBattleUIController.cs` / `DailySettlementUIController.cs` / `GameOverUIController.cs` / `YomiDialogueGenerator.cs` / `ChoiceEventRuntimeData.cs`의 하드코딩 대사. 같은 규칙을 `AITradingBrain.cs`에 돌리면 80자 초과 폴백이 6줄 나옵니다(최대 105자).

### 9.5 폰트

- `Tools/Prebake All Scripts Text into Font` ([PrebakeTMPFont.cs:59](../../Assets/Scripts/Editor/PrebakeTMPFont.cs#L59))는 **`*.cs`만** 스캔합니다. `.asset`의 대사는 `\uXXXX`로 저장되어 있어 스캔하더라도 잡히지 않습니다.
- 현재 두 DB에 쓰인 461자는 모두 아틀라스에 있습니다 — 생성기 `.cs`에 같은 리터럴이 있기 때문입니다. 템플릿 대사도 `GenerateTemplateFallbackText.cs`에 원문이 있어 커버됩니다.
- `Tools/FX OVERDOSE/Prepopulate Font Asset`([ChoiceEventFontPrepopulator.cs](../../Assets/Editor/ChoiceEventFontPrepopulator.cs))는 `Assets/Data`·`Assets/Resources`의 SO를 스캔하지만 폰트를 Dynamic으로 바꾸지 않습니다. `PrebakeTMPFont.cs:106-111` 주석대로 Static 폰트에 대한 `TryAddCharacters`는 아무것도 하지 않으므로 **실효가 없습니다.** 루트의 `YomiDialogueDatabase.asset`은 어차피 범위 밖입니다.
- 아틀라스에 없는 글자는 폴백 동적 폰트로 렌더되어 **문장 중간에 글꼴이 바뀝니다** (같은 주석의 2026-08-14 기록).

---

## 10. 확인된 결함·불일치

전부 정적 분석이며 플레이 검증은 하지 않았습니다. **동작** = 플레이어가 체감하는 오동작, **데이터** = 대사 자산 문제, **도구** = 검증·문서 불일치.

### 10.1 동작

| ID | 문제 | 근거 | 수정 방향 |
|---|---|---|---|
| **YD-1** | **포지션 보유 중 ROE ±15/±30 차트 대사가 한 줄도 안 나옴.** | DB의 ChartMovement 108줄은 전부 `position: None`. 매처는 `Long`/`Short`일 때 `Long_*`·`Any_*` 키만 조회 → 후보가 전부 다른 카테고리라 −9999 → `null`. `Any_Any` 목록이 비어 있지 않아 `None` 폴백([:68-81](../../Assets/Scripts/AI/Dialogue/YomiDialogueMatcher.cs#L68))도 안 탐 | 보유 중 전용 풀을 생성기에 추가(position Any + Pump/Dump 대신 수익/손실 구분). `:857-868`의 `directionHint` 문자열은 LLM 프롬프트 잔재라 함께 삭제 |
| **YD-2** | **돌발 이벤트 골든타임 예고 대사 4종 도달 불가.** | `OnOptionSelected`가 `ApplyOptionEffects`(→ `OverrideMarketTrend` → `ForceInjectSignal` → `OnMarketSignalGenerated` 동기 발행)를 [:631](../../Assets/Scripts/Events/ChoiceEventController.cs#L631)에서, `ResumeGame`을 [:638](../../Assets/Scripts/Events/ChoiceEventController.cs#L638)에서 호출. 수신 측 [AITradingBrain.cs:105](../../Assets/Scripts/AI/AITradingBrain.cs#L105)가 Playing이 아니면 반환 | 재개 후 주입하거나, 골든타임 대사만 상태 검사 앞으로 빼기 |
| **YD-3** | **청산 대사의 소유자가 뒤바뀌고 대박 익절 풀 27줄이 죽어 있음.** (§4.3 표) | DB `mentalState`는 Panicked/Euphoria/Focused/Furious/Stable/Overdose, 런타임은 Stable/Anxious/Danger/Overdose. **621/810줄이 +40을 영원히 못 받음.** 그 결과 멘탈 Stable이면 Stable 태그(+40)가 소유자 일치(+30)를 이겨 AI 익절에 "직접 익절하다니" 대사가 나옴 | 생성기의 멘탈 태그를 런타임 4단계로 다시 매기거나, 소유자 점수를 멘탈보다 높이기 |
| **YD-4** | **매처 쿨다운 5초가 경로 A·B에 공유되어 대사가 삼켜짐.** | 모드 전환 직후 진입·청산 대사 / 의상 버프 수익 청산의 청산 대사 / 포지션 스위칭의 진입 대사 | 쿨다운을 경로 A 안에서만 갱신 |
| **YD-5** | `EventHoldMitigateLoss`·`EventGreedyHoldWin` 대사는 "버티자/존버하자"인데 **코드는 대사 직후 청산**. | [:664-680](../../Assets/Scripts/Trading/TradingController.cs#L664) | 대사를 "여기서 정리할게" 류로 교체하거나 카테고리명대로 홀딩 구현 |
| **YD-6** | `MentalOverdoseStart`는 이름과 달리 **오버도즈 진입 시가 아니라 ROE ≤ −70에서 5초마다 반복**. | [:756-760](../../Assets/Scripts/Trading/TradingController.cs#L756) | 이름 정정 또는 트리거 이동 |
| **YD-7** | `OverdoseExecute` 중 9줄이 "{leverage}배 **롱** 드가자" — 실제 방향 무관. | DB | `{position}` 치환 추가 또는 방향 중립 문구 |
| YD-8 | 차트 힌트가 플레이어 진입 방향을 신호 방향과 비교하지 않음. | [AITradingBrain.cs:689-702](../../Assets/Scripts/AI/AITradingBrain.cs#L689) | `TargetPercentageDelta` 부호와 `playerPos` 비교 |
| YD-9 | 뇌동매매 대사는 챌린지 모드에서도 나오지만 매매 자체는 중단됨. | [TraderStatus.cs:632](../../Assets/Scripts/TraderStatus.cs#L632) | 대사를 실행 성공 뒤로 |
| YD-10 | 오버도즈 중 말풍선이 뜨면 `States/Overdose` 스프라이트가 `Emotions/Manic`으로 덮이고 복구되지 않음 (`isOverdoseStateVisualActive`가 그대로 true). | [AIVisualController.cs:436-458](../../Assets/Scripts/AI/AIVisualController.cs#L436) | `ShowEmotion`에서 오버도즈 비주얼 존중 |
| YD-11 | 요미 말풍선 매처가 GameScene에만 있어, GameScene 미방문 상태에서 DB 대사가 전부 무음. | §2.1 | 현재 DB 대사 경로가 GameScene 안에서만 호출되므로 실해는 없음. 다른 씬에서 쓰게 되면 프리팹화 |

### 10.2 데이터

| ID | 문제 | 수정 방향 |
|---|---|---|
| **YD-12** | 코드가 요청하는 11개 카테고리가 DB에 없어 하드코딩 폴백만 나감(§3). 폴백은 린트 범위 밖이고 6줄이 80자 초과. | 생성기에 카테고리를 추가하거나, DB 조회를 지우고 하드코딩을 정본으로 인정 |
| YD-13 | "주인님" 9줄이 `requiredCostumeId: Any` — 바이블 §4.1은 메이드 의상 한정. | 생성기에서 해당 그룹에 `Maid` 지정 |
| YD-14 | 돌발 이벤트 템플릿 대사가 흐름 4종 × 5줄 + 3줄 = **고유 23줄**. 카테고리(고래·규제·해킹…)와 리스크가 대사에 반영되지 않음. | `MonologuePool`을 카테고리 축으로 확장 |
| YD-15 | 정산 반응은 손익 부호 3줄 — 손익 규모·체력·일차·연패를 반영하지 않음 (표정은 7단계). | 표정 7단계에 맞춰 대사 풀 확장 |
| YD-16 | 돌발 이벤트에 선택 후 결과 반응 대사가 없음. | `EventLogicOptionData`에 반응 필드 추가 |
| YD-17 | `ChoiceEventRuntimeData` 기본 15개 이벤트가 구 어투("AI 트레이더", "자기야")이고 린트 대상 아님. 팝업 빈 대사 폴백도 존댓말. | 에셋 누락 대비용이라 우선순위 낮음. 문구만 교체 |
| YD-18 | 아이템 사용 말풍선이 요미 화법이 아닌 시스템 문구. | 아이템별 요미 대사로 교체 |
| YD-19 | 감정 스프라이트 Furious·Affectionate·Jealous는 트레이딩 흐름에서 도달 불가. | 평가기 분기 추가 또는 그대로 둠 |

### 10.3 도구·문서

| ID | 문제 | 수정 방향 |
|---|---|---|
| **YD-20** | **린트가 템플릿 대사 726줄 중 242개 조각만 검사.** [yomi_dialogue_lint.py:120](../../yomi_dialogue_lint.py)의 `FallbackMonologues:\n((?:  - .*\n)+)`가 4칸 들여쓴 YAML 접힘 줄에서 멈춰, 템플릿마다 **첫 줄의 첫 물리 행**만 잡힘(예: "요미 계산이 다 틀렸어... 오빠가"). | YAML 파서(또는 `yaml_scalars`)로 교체 |
| YD-21 | 린트 `CS_SOURCES`에 삭제된 파일 2개(`LLMSafeGenerator.cs`, `PlaceholderDialogueProvider.cs`)가 남아 있고, 트레이딩 하드코딩 대사 파일은 빠져 있음. `LEN_LLM` 이름도 잔재. | 목록 갱신 |
| YD-22 | `ChoiceEventFontPrepopulator`가 Static 폰트에 글자를 추가하려 해 실효 없음 (§9.5). | 삭제하거나 `PrebakeTMPFont`처럼 Dynamic 전환 |
| YD-23 | `CLAUDE.md`는 EVENT_* 풀이 "템플릿 풀이 빌 때만" 쓰인다고 적었지만, 멘탈 위기 이벤트·템플릿 오류·디버그·P2P에서도 쓰임. | CLAUDE.md·AGENTS.md 문구 수정 |
| YD-24 | 바이블 §5.1 제목 "감정 20종" ↔ 코드 19종. | 바이블 수정 |
| YD-25 | 잔재: `EventCategory.DailySettlement`·`HealthChange` 미사용, `DailySettlementUIController.DialogueWaitTimeout` 미사용, 상태 문구 "GENERATING YOMI COMMENT..."(항상 덮어씀), `EventLogicTemplateSO` 헤더 "(LLM 실패 시 사용…)", 메뉴명 "Generate 15 Choice Event Assets"(30개 생성). | 정리 |
| YD-26 | 일기 기능이 살아 있는 것처럼 적힌 문서: `DatingSim_FreeChat_Removal_Plan.md:36, :316`, `Refactored_Architecture_Master.md:72, :332`. | 문서 갱신 |

---

## 11. 대사를 추가·수정할 때

| 바꾸려는 것 | 고칠 곳 | 이어서 할 일 |
|---|---|---|
| DB 카테고리 대사 (진입·청산·ROE·모드 전환 등) | [YomiDialogueGenerator.cs](../../Assets/Editor/YomiDialogueGenerator.cs)의 해당 그룹 → `Generate Menhera Dialogue DB` 실행 | 린트 → 프리베이크 |
| DB에 새 카테고리 | 생성기에 그룹 추가 + 호출부에서 `OutputSpecificEventDialogue("이름")` 또는 `GetEventDialogue("이름")` | 〃. 카테고리가 DB에 없으면 경로 B는 조용히 실패한다는 점에 주의 |
| 하드코딩 대사 (체력·멘탈·기믹·힌트·스킬·펀딩비·보스·스토리·정산·게임오버) | 해당 .cs 리터럴 (§4.6~§4.10, §6, §7) | 프리베이크. **린트는 이 파일들을 보지 않으므로** 화법을 직접 확인 |
| 돌발 이벤트 템플릿 대사 | `GenerateTemplateFallbackText.cs`의 `MonologuePool` → "전체 덮어쓰기", 또는 템플릿 에셋 직접 편집 | 직접 편집했다면 같은 문장이 .cs에 없으므로 프리베이크에 안 잡힘 → 글자 확인 |
| EVENT_* 대사 | `ChoiceEventAssetGenerator.cs` → 재생성 (에셋 덮어씀) | 린트 → 프리베이크 |
| 표정 | 대사 텍스트의 키워드가 표정을 바꾼다는 점 확인 (§8.2) | — |

- 매매 말풍선은 **80자**, 돌발 이벤트 템플릿 대사는 **60자**, EVENT_* 대사는 **150자** 이하 (린트 기준).
- 호칭은 "오빠", 자칭은 "요미"/"나", 반말 (바이블 §4).

---

## 12. 제거된 기능 (이력)

| 기능 | 상태 | 비고 |
|---|---|---|
| 돌발 이벤트 요미 대사 LLM 생성 | 2026-09-22 제거 | 우선순위가 "DB `ChoiceEvent_*` → LLM → 템플릿 → 고정 문구"였으나 DB에 `ChoiceEvent_*`가 0줄이라 1순위는 원래 죽어 있었음. 템플릿 단독(A안)으로 정리, `preferYomiDialogueDatabase` 삭제. [Trading_LLM_Removal_Plan.md](../P2_03_LLM_Architecture/Trading_LLM_Removal_Plan.md) |
| 정산 LLM 일기 | 2026-07-28 UI 경로 제거, 08-13 생성기 삭제 | 23:50 선요청 + 정산 시 최대 8초 대기 구조였음. 현재 `GetImmediateReaction` 3줄이 그 자리 |
| `HandleAIDecisionMade` 말풍선 | 제거 | `currentAction`을 넘기지 않아 후보 전량이 −9999 → 한 번도 말한 적 없음 ([AIVisualController.cs:788-790](../../Assets/Scripts/AI/AIVisualController.cs#L788) 주석) |
| `AITradingBrain.HandlePositionClosed` 반성 대사 | 2026-10-06 삭제 (DEAD-3) | 분기만 있고 본문이 비어 있었음 |
| 요미 지출 예고 + 정산 지출 대사 | 2026-10-06 삭제 (f9da7ed) | 정기 지출과 함께 제거. [Trading_System_Master.md](Trading_System_Master.md) §23 |
| 체력 임계·멘탈 전이 대사 | 2026-10-06 **추가** (DEAD-1, DEAD-2) | 빈 분기를 채움 — §4.6 |
| 펀딩비 알림 대사 | 2026-10-06 **추가** (REAL-1) | §4.9 |
