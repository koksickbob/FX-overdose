# 트레이딩 파트 기믹·로직 마스터 문서

- **작성일**: 2026-10-06
- **기준**: `Dev_koksickbob_Laptop` (74220c4)
- **성격**: 기획서가 아니라 **현재 코드에 실제로 살아 있는 것만** 적은 역공학 레퍼런스입니다. 모든 수치는 소스에서 직접 확인했고 파일·행 번호를 붙였습니다. 코드와 다르면 코드가 맞습니다.
- **범위**: `GameScene` 트레이딩 루프 전체 (시장 시뮬레이션 / 포지션 / 바이탈 / 기믹 / AI / 성장 / 돌발 이벤트 / 정산 / 엔딩). 미연시 파트와 P2P는 접점만 다룹니다.
- **§1~18**은 현재 동작의 기술(記述), **§19는 고쳐야 할 결함·불일치**, **§20은 실제 BTC 선물 시장과의 격차**, **§21은 신호 시스템(§3)의 다양성 개선 후보**, **§22~23은 삭제 계획**입니다. §20과 §21은 성격이 다릅니다 — 전자는 *현실성*, 후자는 *플레이 체감*이며 겹치는 항목은 서로 참조만 합니다.
- **삭제 결정 진행 상황**
  - ✅ **기믹 4 (고배율 중독 금단현상)** 삭제 — 2026-10-06 완료 (§22)
  - ✅ **정기 지출 + 요미 지출 예고** 삭제 — 2026-10-06 완료 (§23)

> **▶ 할 일을 찾는다면 [Trading_System_Refactor_Backlog.md](Trading_System_Refactor_Backlog.md)로 가십시오.** 이 문서의 §17.2·§19~§23에 흩어진 개선·삭제 항목을 ID·의존성·실행 순서로 정리한 백로그입니다. **근거와 수치는 이 문서에, 순서와 상태는 그쪽에** 둡니다 — 중복 기재하지 않습니다.
>
> 관련 문서: 버그 감사 이력은 [Trading_System_Audit_Fix_Plan.md](Trading_System_Audit_Fix_Plan.md), 멘탈 곡선 설계 근거는 [Mental_Drain_Rebalance_Plan.md](Mental_Drain_Rebalance_Plan.md), 난이도는 [Difficulty_System_Plan.md](Difficulty_System_Plan.md), 완료된 변경 기록은 [Refactored_Architecture_Master.md](Refactored_Architecture_Master.md).

---

## 0. 한눈에 보는 시스템 지도

```
GameManager (시계·잔고·상태머신·정산·엔딩)
   │ OnGameMinuteAdvanced (인게임 1분)
   ├──► MarketSimulationEngine ─ OnPriceUpdated / OnCandleClosed / OnMarketSignalGenerated
   │        │                                      │
   │        │                                      ├──► AITradingBrain (자동매매 판단)
   │        │                                      └──► ChartUIController (렌더)
   │        └──► TradingController (포지션 생애주기·청산·수수료·멘탈 반영)
   │                 │ OnPositionOpened / OnPositionClosed / OnPositionLiquidated
   │                 ├──► MentalDrainGimmickController (멘탈 소모 기믹 4종)
   │                 ├──► TraderLevelSystem (경험치·레벨)
   │                 └──► TraderStatus (HP·멘탈·오버도즈 판정)
   ├──► ChoiceEventController (돌발 선택 이벤트 → 차트 빔 + 강제 포지션)
   └──► DynamicTimeRegulator (슬로우모션·음식 배속)
```

**레이어 규칙**: 매니저가 로직·데이터를 들고 `event Action<...>`을 쏘고, UI 컨트롤러가 구독합니다. 매니저는 UI 컨트롤러를 참조하지 않습니다.

---

## 1. 시간과 게임 상태

### 1.1 시계

| 항목 | 값 | 위치 |
|---|---|---|
| 인게임 1분당 실제 초 | **0.666초** (`GameScene.unity`에 직렬화된 값) | [GameManager.cs:198](../../Assets/Scripts/GameManager.cs#L198) |
| 하루 거래 시간 | 09:00 → 24:00 (900 인게임 분 ≈ **실시간 10분**) | [GameManager.cs:846](../../Assets/Scripts/GameManager.cs#L846) |
| 날짜 | `startDate` = 2026-06-26, `CurrentDay = (currentDate - startDate).Days + 1` | [GameCalendar.cs](../../Assets/Scripts/System/GameCalendar.cs), [GameManager.cs:228](../../Assets/Scripts/GameManager.cs#L228) |
| 1분 진행 | `timeAccumulator`가 `secondsPerGameMinute`를 넘을 때마다 `AdvanceOneMinute()` | [GameManager.cs:832](../../Assets/Scripts/GameManager.cs#L832) |

`AdvanceOneMinute()`의 실행 순서가 중요합니다 — ① 분 증가 → ② `OnGameMinuteAdvanced` 발행 → ③ 자산 표본 기록 → ④ 24시 도달 시 정산 진입. 구독자가 그 분의 손익을 반영한 뒤에 표본을 찍기 위해 순서를 바꾸면 한 틱 밀린 값이 기록됩니다.

### 1.2 GameState

`Loading → Playing ⇄ Paused → Settlement → (GameOver)`

- **Paused**: 돌발 이벤트 팝업, 상점 팝업. `ChangeBalance`는 Paused에서도 허용되지만 `TradingController.Update()`는 Playing에서만 돕니다.
- **Settlement**: `isSettlementProcessing = true`로 파산 판정을 유예합니다. 이 상태에서 포지션을 열 수 없습니다.
- **GameOver**: `EndGame()`이 엔딩 컷씬 후 `OnGameOverEvent`를 발행.

### 1.3 고속 시간 경과 (Fast Forward)

스킬 학습(3~4시간) 등으로 수백 분을 한 번에 흘릴 때 쓰입니다. [GameManager.cs:1436](../../Assets/Scripts/GameManager.cs#L1436)

- `AdvanceGameMinutes(n)` — 1분씩 **동기 루프**로 시뮬레이션. Playing이 아니게 되면 즉시 중단하고 남은 분을 보존 (이벤트 팝업이 스킵에 먹히지 않게).
- `AdvanceClockWithoutSimulation(n)` — 시각만 옮기고 분당 이벤트를 발행하지 않음. 요미의 방·월드맵에서 사용 (24:00 클램프).
- 고속 스킵 중 변화:
  - 신규 시장 신호 발생 중단 ([MarketSimulationEngine.cs:1261](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L1261))
  - 돌발 이벤트 예정 시각을 `현재 + 15~45분`으로 계속 밀어냄 ([ChoiceEventController.cs:247](../../Assets/Scripts/Events/ChoiceEventController.cs#L247))
  - 멘탈 소모 기믹 전면 정지 ([MentalDrainGimmickController.cs:148](../../Assets/Scripts/AI/MentalDrainGimmickController.cs#L148))
  - 유동성 사냥(꼬리 스파이크) 정지, 15% 확률로 국면 강제 전환(일직선 방지), 1분당 5서브틱 가상 시뮬레이션
  - **보유 포지션 강제 청산선 작동**: ROE ≥ `50 × 익절배율` 또는 ≤ `-손절강도 × 333%` 도달 시 수동 모드라도 강제 청산 ([TradingController.cs:475](../../Assets/Scripts/Trading/TradingController.cs#L475))
- 오버도즈 발생 시 `PauseFastForwardForOverdose()`로 남은 분을 보존했다가, 멘탈 회복 시 `ResumePreservedFastForward()`로 이어서 진행.

### 1.4 슬로우모션 연출 (DynamicTimeRegulator)

`TriggerDramaticSlowMotion(targetSpeed, durationSeconds)` — `targetSpeed`는 **인게임 1분에 쓸 실제 초**입니다. 값이 클수록 느려집니다. 평시 0.666초 기준:

| 트리거 | 배속 | 지속 | 위치 |
|---|---|---|---|
| ROE +50% 돌파 (대박) | 2.0초/분 (약 3배 감속) | 4초 | [TradingController.cs:544](../../Assets/Scripts/Trading/TradingController.cs#L544) |
| 마진콜 임박 (청산가까지 여유 0.5% 미만) | 3.0초/분 | 3초 | [TradingController.cs:1261](../../Assets/Scripts/Trading/TradingController.cs#L1261) |
| 오버도즈 폭주 진입 | 2.5초/분 | 5초 | [TraderStatus.cs:503](../../Assets/Scripts/TraderStatus.cs#L503) |

- 두 포지션 연출 플래그(`isTargetBreakthroughSlowMotionTriggered`, `isMarginCallSlowMotionTriggered`)는 포지션 종료·청산 시 리셋되어 포지션 단위로 1회씩 재생됩니다.
- 전환은 `Lerp(current, target, unscaledDeltaTime * 3)`으로 부드럽게 ([DynamicTimeRegulator.cs:57](../../Assets/Scripts/Core/DynamicTimeRegulator.cs#L57)).
- 파스타(배달음식)는 `SetFoodTimeMultiplier(1.5)` → 180초간 시간 1.5배 가속. 슬로우모션 중에는 덮어쓰지 않습니다.

---

## 2. 시장 시뮬레이션 (MarketSimulationEngine)

### 2.1 틱 생성 공식

틱 주기: `clamp(secondsPerGameMinute / 5, 0.05, 1.0) / max(1, tickInstability)` → 평시 약 0.133초, 후반 `tickInstability`가 오르면 더 촘촘해집니다. [MarketSimulationEngine.cs:514](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L514)

한 틱의 수익률:

```
totalReturn = (drift × dtFraction) + (ouTerm × dtFraction) + stochasticNoise

drift        = 국면 드리프트 + 파동(사인 3중첩) + 일일 거시 드리프트 (+ 신호 구간 보정)
ouTerm       = ouTheta × (ouCenterPrice - currentPrice) / currentPrice     ← 평균 회귀
stochasticNoise = currentVolatility × √dtFraction × N(0,1)                ← Box-Muller
currentVolatility = Lerp(현재, targetVol, dtFraction × 5)                  ← GARCH 풍 군집
```

최저가 방어는 `if (!(currentPrice >= 10f)) currentPrice = 10f;` — **긍정 조건을 부정하는 형태여야 NaN도 걸립니다.** 이 패턴은 코드 전역의 관례입니다.

### 2.2 국면(Regime) — 틱 단위 성격

| Regime | drift | targetVol | ouTheta |
|---|---|---|---|
| Bull | +0.0004 | 0.0035 | 0.02 |
| Bear | −0.0004 | 0.0045 | 0.02 |
| Sideways | 0 | 0.0025 | 0.15 (강한 박스권 회귀) |
| Squeeze | ±0.0008 랜덤 | 0.012 (광기) | 0.01 |

유지 시간 30~120분, 만료 시 `SwitchToRandomRegime()`. 전환 확률은 **그날의 거시 기조**에 종속됩니다:

| 일일 기조 | 전이 확률 |
|---|---|
| Bull | Bull 60% / Sideways 20% / Bear 10% / Squeeze 10% |
| Bear | Bear 60% / Sideways 20% / Bull 10% / Squeeze 10% |
| Squeeze | Squeeze 50% / Bull 20% / Bear 20% / Sideways 10% |
| Sideways | Sideways 40% / Bull 25% / Bear 25% / Squeeze 10% |

**요미가 그날 방향을 예고(`DailyMarketOutlook.Revealed`)했다면 역방향 구간이 Sideways로 치환됩니다** — 힌트를 듣고도 반대로 가면 힌트가 거짓말이 되므로. [MarketSimulationEngine.cs:1042](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L1042)

### 2.3 일일 거시 방향성 (DailyMarketOutlook)

MonoBehaviour가 아닌 **static 클래스**. 상태는 int 2개 + bool 1개이고 진실 원천은 세이브입니다. [DailyMarketOutlook.cs](../../Assets/Scripts/Trading/DailyMarketOutlook.cs)

- 추첨표: Sideways 35% / Bull 25% / Bear 25% / Squeeze 15%
- **하루의 방향성은 딱 한 번 결정되고 누구도 다시 굴리지 않습니다.** 요미의 방에서 힌트로 먼저 물어보든 GameScene 차트가 먼저 물어보든 같은 답이 나옵니다.
- 결정 즉시 `Persist()`로 디스크에 기록 — **세이브 스컴으로 방향성 재추첨을 막는 장치**입니다.
- 거시 드리프트 주입량: Bull +0.00015 / Bear −0.00015 / Squeeze ±0.0003

### 2.4 일차별 난이도 스케일링

[MarketSimulationEngine.cs:528](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L528) — 25일차에서 캡.

| 일차 | 꼬리 강도 (`sweepIntensityMultiplier`) | 틱 불안정성 (체감용) | 가짜돌파 확률 | 슬리피지 | 서버 렉 |
|---|---|---|---|---|---|
| 1~5 | 1.0 | 1.0 | 0% | 0 | 없음 |
| 6~10 | 1.2 | 1.5 | 10% | 0 | 없음 |
| 11~15 | 1.5 | 3.0 | 30% | 3틱 | 없음 |
| 16+ | `2.0 + (일차−16)×0.15` | `5.0 + (일차−16)×1.0` | 50% | 5틱 | **활성** |

> **꼬리 강도**는 유동성 사냥의 꼬리 길이·발생 확률에만 쓰이고 틱 변동성(`targetVol`)에는 곱해지지 않습니다. **틱 불안정성**은 분당 실현 변동성에 중립인 순수 연출 노브입니다. 일차에 따라 실제 난이도를 올리는 것은 **가짜돌파 확률·슬리피지·서버 렉** 세 축입니다. (2026-10-06 FIX-3: 오해를 부르던 이름 `dayVolatilityMultiplier`를 실제 동작에 맞게 바꿨습니다 — §19.1-3)

### 2.5 현실감 기믹 4종

> 이 4종은 차트의 **모양**을 만드는 장치입니다. 실제 BTC 선물 시장의 **경제적 메커니즘**(펀딩비·청산 캐스케이드·호가 깊이 등)과의 격차는 §20에 따로 정리했습니다.

**① 스프레드 (Spread)** — [MarketSimulationEngine.cs:885](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L885)
```
spread = currentPrice × currentVolatility × 0.5
  × 3.0 (서버 렉 중)
  × 5.0 (이벤트 빔 오버라이드 중)
  소프트캡: 최대 가격의 0.5%
  오버도즈 중: 가격의 0.0001로 최소화 (연출 방해 방지)
Bid = price − spread/2,  Ask = price + spread/2
```
롱은 Ask로 체결하고 Bid로 청산, 숏은 그 반대. 청산 판정도 호가 기준입니다.

**② 오더블록 (보이지 않는 지지/저항)** — 1000단위 라운드 피겨 ±0.2% 접근 시 `Mathf.Sign(price - round) × 0.1`의 반발력을 OU 항에 더합니다. 단 `fakeoutProbability`가 낮을수록 돌파를 잘 합니다. 오버도즈·확정 빔 구간에서는 완전히 무시. [MarketSimulationEngine.cs:768](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L768)

**③ 세션 (시간대별 시장 성격)** — `CurrentHour` 기준. 오버도즈·빔 구간 제외. [MarketSimulationEngine.cs:633](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L633)

| 시각 | 세션 | 변동성 배수 | 가짜돌파 보정 |
|---|---|---|---|
| 00~08 | 아시아 | ×0.5 | ×0.5 (최소 5%) |
| 08~16 | 런던 | ×1.2 | — |
| 16~24 | 뉴욕 | ×2.0 | ×1.5 (최대 85%) |

**④ 유동성 사냥 / 스탑 헌팅** — 매 인게임 분마다 판정. Squeeze 국면 30%, 그 외 5% (+일차 변동성이 1.0 초과면 +10%p). 발동 시 `0.5%~2% × 꼬리 강도` 떨어진 가격을 **실제 시세로 1틱 찍고 곧바로 되돌립니다** — 호가(Bid/Ask)까지 옮기므로 청산·AI 익절/손절·이벤트 포지션 판정이 이 꼬리를 봅니다. 꼬리는 모든 타임프레임 진행 캔들에 반영되고 거래량 +50~200, 순간 변동성 ×2. **오버도즈·고속스킵·서버 렉·확정 빔 구간에서는 차단.** 진입 직후 3초 휩소 보호(§4.2)는 그대로 적용됩니다. [MarketSimulationEngine.cs:1079](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L1079)

> 2026-10-06 이전에는 1분봉의 high/low만 늘리는 순수 시각 효과라 스탑을 헌팅하지 못했습니다 (§19.1-②, FIX-2).

### 2.6 서버 렉 (16일차 이후)

[MarketSimulationEngine.cs:484](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L484)

- 발동: 16일차 이상 + 오버도즈·고속스킵·이벤트 빔이 아닐 때, 매 프레임 `0.05 × deltaTime` 확률 (대략 1시간에 1회꼴)
- 지속 3.0~5.0초. 그 동안 **가격 델타와 거래량을 누적만 하고 화면을 멈춥니다.**
- 복구 순간 누적분을 한 번에 반영 → 차트가 점프.
- 렉 중에는 **매수/매도 주문과 익절/손절 버튼이 전부 먹통**입니다 ([TradingController.cs:965](../../Assets/Scripts/Trading/TradingController.cs#L965), [:1056](../../Assets/Scripts/Trading/TradingController.cs#L1056)). 손절도 못 하는 구간입니다.
- `TradingPanelUIController`가 전용 오버레이를 띄웁니다.
- `ponytail:` 렉 구간 동안 복리를 무시하고 델타를 단순 합산합니다(3~5초, 약 15~25틱).

### 2.7 캔들

- 타임프레임: M1(1) / M5(5) / M15(15) / H1(60) / H4(240) / D1(1440) — enum 값이 곧 분 수.
- 타임프레임별 버퍼 최대 200개, 초과 시 가장 오래된 것 제거.
- 게임 시작 시 과거 **150분**을 Pre-warm 생성하고 마지막 종가를 현재가와 맞닿게 연결 (갭 없음).
- Pre-warm 거래량은 캔들 형태에 따라 가중: 장대양봉 ×1.4~2.1, 장대음봉(패닉셀) ×1.6~2.6, 긴 꼬리 ×1.2~1.7, 횡보 도지 ×0.3~0.65.
- 고속 스킵·동기 시간 경과로 1분봉이 **거래량 0 + 일직선**으로 비는 것을 감지하면 `SimulateFastForwardTicks()`로 1분당 5서브틱을 선제 주입합니다.
- 차트 UI 기본값: 5분봉, 최대 표시 50개 ([ChartUIController.cs:32](../../Assets/Scripts/UI/Chart/ChartUIController.cs#L32)).
- 프리웜이 150분이라 1일차의 H4·D1은 **프리웜 스텁 1개 + 진행 캔들**뿐입니다. 차트가 날짜를 넘어 이어지므로(§11.3) 이후 **D1은 하루 1개, H4는 하루 4개**씩 경계에 맞춰 쌓입니다. (FIX-1/FIX-4, 2026-10-06)

---

## 3. 차트 신호 시스템 (4단계 페이즈)

게임의 핵심 기믹입니다. "차트가 미리 예고하고, 그 예고가 진짜일 수도 함정일 수도 있다."

### 3.1 페이즈 타임라인

```
None ──(카운트다운)──► GraceWindow ──► GuaranteedOverride ──► Cooldown ──► None
     신호 간격             판단 여유            확정적 주가 제어        중복 방지
```

| 페이즈 | 성격 | 차트 거동 |
|---|---|---|
| `None` | 평시 평균 회귀 | 국면 기본값 |
| `GraceWindow` | AI·플레이어 판단 골든타임 | **노이즈 40%로 억제 + drift 0 (횡보 대기)** |
| `GuaranteedOverride` | 목표 변동률까지 강제 이동 | OU 항 무력화, 아래 연출 패턴 적용 |
| `Cooldown` | 15~25분 (무포지션이면 3분으로 단축) | 평시 복귀 |

- 개장 직후 첫 신호: **3분 뒤** (`minutesUntilNextSignal = 3`)
- `None` 상태 신호 간격: 쿨다운 종료 후 8~15분 / 무포지션 조기 종료 시 5~10분
- **무포지션 장기 대기 방지**: `GuaranteedOverride` 중 외부 이벤트 빔이 아니고 AI가 포지션을 잡지 않았다면, 10분 경과 후 확정 구간과 쿨다운을 모두 생략하고 즉시 다음 신호 주기로 복귀 ([MarketSimulationEngine.cs:1300](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L1300)).

### 3.2 신호 생성 확률 (GenerateMarketSignal)

> 다양성·현실성 개선 후보는 §21.1. 2026-10-06부터 **일일 기조를 읽습니다**(SIG-A1). 현재 차트 모양은 아직 읽지 않습니다 — §21.1-A3.

[MarketSimulationEngine.cs:1341](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L1341)

신호 종류는 **그날의 거시 기조**(§2.3)를 따릅니다 (SIG-A1, 2026-10-06 — 이전엔 기조와 무관하게 35/35/15/15).

| 일일 기조 | `BullishBreakout` | `BearishBreakout` | `BullTrap` | `BearTrap` | 성격 |
|---|---|---|---|---|---|
| Bull | **45%** | 15% | 15% | **25%** | 추세장 — 숏을 꼬신 뒤 급등(베어트랩)이 전형 |
| Bear | 15% | **45%** | **25%** | 15% | 대칭 |
| Sideways | 20% | 20% | **30%** | **30%** | 박스권 — 양 끝단 가짜 돌파가 지배적 |
| Squeeze | 30% | 30% | 20% | 20% | Strong 80%, 변동 크기 ×1.25 |

- `BullTrap` = 롱 꼬시기 휩소, `BearTrap` = 숏 꼬시기 휩소 (둘 다 항상 가짜)
- 강도: Strong 65% / Weak 35% (Squeeze 기조는 Strong 80%)
- **진짜 신호 확률**: `0.60 − (fakeoutProbability × 0.5) ± 0.20` — Breakout만 대상, 기조와 **같은 방향 돌파는 +0.20, 반대 방향은 −0.20** (횡보·광기는 보정 없음, 0.05~0.95로 클램프). 16일차 이후(fakeout 0.5)면 기준이 35%로 떨어집니다. Trap은 100% 가짜.
- **결과적으로 신호가 기조 방향으로 가격을 움직일 확률**: Bull/Bear 기조에서 초반 **70%**, 16일차 이후 **62.5%** (이전엔 기조와 무관하게 50%). 요미의 일일 방향 힌트가 매매 판단에 실질적인 우위가 됩니다.
- 목표 변동률 / 지속:
  - Strong: ±3.0~6.0%, 15~30분 (10배 레버리지 기준 ROE ±30~60%)
  - Weak: ±0.6~1.5%, 5~10분 (ROE ±6~15%)
- 판단 여유(Grace): 3~5분

### 3.3 GuaranteedOverride 연출 패턴

> 패턴 확장·자연스러움 개선 후보는 §21.2. 현재 조합은 **Wave 2종 × Trap 3종**이 전부이며 Trap 3종은 외부 이벤트 전용이라, 일반 AI 신호가 쓰는 패턴은 실질적으로 2개입니다.

진입 시 `currentOverdriveWaveStyle`(0~1), `currentOverdriveTrapType`(0~2)을 무작위로 뽑습니다.

**일반 스킬(AI) 확정 수익 구간** (`!isExternalEventOverride && IsTrueSignal`): 노이즈를 **15%로 대폭 억제** — 좁은 손절선이 노이즈에 터지지 않도록 보호.

**그 외 파동 스타일**
- Wave 0: 노이즈 ×1.5 + 짧은 주기 사인 → 자잘하게 요동치며 이동 (음봉/양봉 섞임)
- Wave 1: 노이즈 ×0.8 + 20~30초 주기 큰 역추세 파동 → 눌림목 형성

**외부 이벤트 트랩 3종** (`isExternalEventOverride && !IsTrueSignal`) — 경과 비율에 따라 drift를 바꿉니다:
| 타입 | 패턴 |
|---|---|
| 0 Classic V-Shape | 약 70% 구간(0.6~0.8 추첨) ×1.35 급행 → 나머지 역방향 ×0.45 반등 |
| 1 W-Shape Double Trap | 1차 급락 ×1.5 → 페이크 반등 ×0.8 → 2차 급락 ×1.2 (개미털기) → 최종 탈출 빔 ×0.6. 경계 약 0.4/0.6/0.85를 순서 보장하며 추첨, 탈출 구간 ≥ 0.1 |
| 2 Slow Bleed + Flash Spike | 약 85% 구간(0.8~0.9 추첨) 노이즈 ×0.3으로 말려죽이다 나머지에 노이즈 ×2.0 극적 빔 |

분할 지점은 `GuaranteedOverride` 진입 시 `RollTrapSplits()`가 1회 추첨하고(SIG-B2, 2026-10-06), 각 구간 드리프트의 분모도 같은 값을 쓰므로 **구간별 총 이동량은 지터와 무관하게 보존**됩니다.

**평균 회귀(OU) 처리** (SIG-B3, 2026-10-06) — 트랩 3종은 OU를 끕니다(비선형 패턴 보존). **정상 확정 경로**는 OU를 끄지 않고, 중심선을 `시작가 → 목표가` 경로 위로 옮겨(`SignalPathOuTheta = 0.1`) 가격이 경로 주변을 오가게 합니다. 노이즈 편차가 쌓이지 않아 목표 도달이 안정되고(종료 편차 표준편차가 절반으로), 경로 주변의 눌림목·되돌림은 남습니다. 전역 `ouCenterPrice`는 건드리지 않습니다.

### 3.4 −25% ROE 스프링 꼬리 안전망

`GuaranteedOverride` + **진짜 신호** + 플레이어 포지션 방향이 일치할 때만 작동. [MarketSimulationEngine.cs:794](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L794)

- ROE −24% 부근부터 역행 하락폭을 `clamp01(25 + expectedRoe)` 배로 줄이고 반발 노이즈(`|noise| × 1.5`)를 더해 꼬리를 만듭니다.
- ROE −25%를 넘기려 하면 **하드 리밋 가격으로 되돌리고 무작위 꼬리 반등**을 형성합니다.
- **오버도즈 폭주 중에는 이 가드가 무시됩니다** — 그때는 −100% 청산이 목적입니다.

### 3.5 외부 강제 주입

- `OverrideMarketTrend(목표%, 인게임분, isWhipsaw)` → `InitiateEventSignalOverride` → `ForceInjectSignal`
- 골든타임 2분 고정, 실제 드리프트 = `max(5, 지속분 − 2)`, 변동성 ×3.0(휩소) 또는 ×1.8
- `isExternalEventOverride = true`가 서므로 스프레드 ×5, 세션 보정·오더블록 정지
- ⚠️ **단위 불일치 주의**: 차트 빔 지속은 **인게임 분**, 포지션 이벤트 쉴드는 **실시간 초**입니다. 과거 이 혼동으로 쉴드가 항상 2/3 지점에서 끊겼습니다 ([TradingController.cs:142](../../Assets/Scripts/Trading/TradingController.cs#L142) 주석).

---

## 4. 포지션 로직 (TradingController)

### 4.1 두 개의 진입 경로

| | `OpenPosition` (AI/기믹/이벤트) | `OpenPlayerPosition` (수동) |
|---|---|---|
| 진입 수수료 | **포지션 규모 × 0.06%** 차감 | **동일** (2026-10-06 FIX-7 이전엔 없었음) |
| 슬리피지 | 없음 | `price × 0.0005 × SlippageRange`를 불리한 방향으로 |
| 레버리지 클램프 | 레벨 상한 (단 `isEmergencyTrade`면 면제) | 레벨 상한 적용 |
| 증거금 클램프 | 레벨 상한 비율 + 잔고 95% 안전망 | 레벨 상한 비율 + **증거금 + 진입 수수료 ≤ 잔고** 상한 |
| 목표가/손절가 | AI가 지정 | 0 (플레이어 직접 판단) |
| 쿨다운 체크 | 없음 | **3초 쿨다운** |
| 서버 렉 차단 | 없음 | 차단 |

공통: 레버리지 `clamp(1, 125)`. 기존 포지션이 있으면 먼저 청산하고 스위칭. 증거금 설정 → **그 다음** 잔고 차감 (조기 게임오버 오진 방지).

### 4.2 청산가 / PnL / 강제 청산

```
유지 증거금률 = 0.5%
Long  청산가 = entryPrice × (1 − 1/leverage + 0.005)
Short 청산가 = entryPrice × (1 + 1/leverage − 0.005)

미실현 손익 = margin × (가격차 / entryPrice) × leverage     ← 청산 호가 기준
ROE(%)      = 미실현손익 / margin × 100
```

- **진입 후 3초 휩소 보호**: `eventPositionOpenedTime + 3초` 이내에는 청산 판정을 건너뜁니다. 꼬리 스파이크 1틱에 의한 억울한 즉사 방지. 이벤트 포지션뿐 아니라 **모든 포지션**에 적용됩니다 ([TradingController.cs:1275](../../Assets/Scripts/Trading/TradingController.cs#L1275)).
- 강제 청산 시 증거금 **전액 몰수**(정산금 0원), 멘탈 **−40**. 단 **이벤트 실패(트랩)로 인한 청산은 −40을 면제**합니다 ([TradingController.cs:1310](../../Assets/Scripts/Trading/TradingController.cs#L1310)).

### 4.3 청산(ClosePosition) 정산 순서

순서를 바꾸면 분모가 오염되거나 기믹이 무음 실패합니다.

1. `pnl = 미실현손익 − 청산수수료(규모 × 0.06%)`
2. 액티브 아이템: 이익이면 `× (1 + ProfitBoostRate)`, 손실이면 `× (1 − LossReductionRate)`
3. 의상 버프 (아래 §9.3)
4. **`capitalBeforeSettle` 캡처** (현금 + 증거금) ← 회수금 반영 **전**
5. `ChangeBalance(증거금 + pnl)`
6. 멘탈·체력·경험치 반영
7. `OnPositionClosed` 발행 ← 수신자가 증거금/ROE를 읽을 수 있도록 **상태 초기화 직전**
8. 포지션 상태 초기화 → `EvaluateEndingConditions()`
9. ⚠️ **`IsManualModeLockedByYomi`는 여기서 지우지 않습니다.** 임시 락이 걸린 대기 구간(오버도즈 2초, 뇌동매매 지연 진입) 중에도 `ClosePosition()`은 수동 청산·돌발 이벤트·24시 강제 청산으로 호출됩니다. 여기서 풀면 그 사이 플레이어가 수동 모드로 빠져나가 강제 진입을 피합니다. 해제는 `TemporaryLockRoutine`이 자기 세대를 확인한 뒤 직접 합니다.

### 4.4 청산 시 멘탈 변화 곡선

[TradingController.cs:9](../../Assets/Scripts/Trading/TradingController.cs#L9) — **절대 금액이 아니라 총자본 대비 손익률** 기준이라 초기 자본 7,000이든 후반 700,000이든 같은 비율이면 같은 변화가 나옵니다.

```
손실: mental −= min(25, 40 × √(손실률))
수익: mental += min(15, 25 × √(수익률)) × 레벨별회복배율,  체력 +5
```

| 손익률 | 멘탈 손실 | 멘탈 회복(배율 1.0) |
|---|---|---|
| 1% | −4.0 | +2.5 |
| 5% | −8.9 | +5.6 |
| 10% | −12.6 | +7.9 |
| 25% | −20.0 | +12.5 |
| 36%+ | −22.0 ~ | **+15.0 (상한)** |
| 39%+ | **−25.0 (상한)** | |

제곱근 곡선이라 소액 손실도 체감이 남고 대형 손실은 완만해집니다 → **단발 오버도즈 불가, 누적으로만 도달**. PnL == 0인 거래는 성공으로 인정하지 않아 경험치·보너스를 주지 않습니다.

> `python verify_mental_balance.py`가 이 곡선의 설계 제약(스케일 불변성·단발 상한·3연속 손절 생존)을 검증합니다. **수치를 바꿀 때 이 스크립트의 상수도 함께 고쳐야 합니다.**

### 4.5 수동/자동 모드와 주도권 락

- `TradingMode`: `AI_Auto` / `Player_Manual`. 토글은 **장 개시(Playing) + 시장 개장 + 비(非)오버도즈 + 락 없음**일 때만 허용.
- `IsManualModeLockedByYomi` — 요미가 주도권을 강탈한 상태. 수동 전환 불가. **락은 `LockManualModeTemporarily(초)` 임시 락뿐**이며 오버도즈 폭주(2초)와 뇌동매매 지연 진입(`delayBeforeOpen`)이 겁니다. (영구 락 `LockManualMode`/`UnlockManualMode`는 유일한 사용처였던 고배율 중독 기믹과 함께 2026-10-06 삭제)
- **락 소유권 세대(`manualLockGeneration`)**: 임시 락이 대기 중에 새 임시 락이 걸리면 세대가 바뀌고, 먼저 건 쪽은 자기 세대가 아닐 때 해제를 포기합니다. 없으면 구간이 겹칠 때 먼저 끝난 쪽이 아직 살아 있어야 할 다른 쪽의 락까지 풉니다. [TradingController.cs:88](../../Assets/Scripts/Trading/TradingController.cs#L88)
- **챌린지 모드에서는 어떤 기믹도 수동 주도권을 빼앗을 수 없습니다** (`IsAITradingLockedByGameMode`).

### 4.6 수동 모드 보호

플레이어가 수동 조작 중이고 오버도즈가 아니면 AI는 다음을 하지 않습니다:
- 목표가/손절가 자동 청산 ([TradingController.cs:517](../../Assets/Scripts/Trading/TradingController.cs#L517))
- 이벤트 포지션 자동 판단·물타기 ([TradingController.cs:208](../../Assets/Scripts/Trading/TradingController.cs#L208))
- 이벤트 쉴드 종료 시 자동 정리 (제어권을 그대로 넘김)
- 신규 시그널 자동 진입 (브리핑만)
- FOMO 후회 기믹 추적

---

## 5. 바이탈 (TraderStatus)

### 5.1 정본 인스턴스 규칙

씬을 넘나들며 **여러 `TraderStatus`가 동시에 존재**할 수 있습니다. `TraderStatus.CanonicalInstance`(GameManager 오브젝트에 붙은 것)만 권위를 가지며, 나머지는 매 프레임 `SyncFromCanonical()`로 미러링합니다.

- 모든 mutator(`ChangeHealth`/`ChangeMental`/`ResetStatus`/`IncreaseMaxMental`)는 비정본에서 호출되면 정본에 위임합니다.
- 기믹 상태 `CurrentLosingStreak`는 **프로퍼티 세터도 정본을 거칩니다** (`Owner` 프로퍼티) — 그러지 않으면 비정본에 쓴 값이 다음 동기화에서 조용히 되돌려져 기믹이 무음 실패합니다.
- **`GetComponent<TraderStatus>()`가 정본을 줬다고 가정하지 마십시오.**

### 5.2 멘탈 상태 4단

| 상태 | 멘탈 | 진입 시 동작 |
|---|---|---|
| `Stable` | > 50 | — |
| `Anxious` | ≤ 50 | — |
| `Danger` | ≤ 25 | **40% 확률로 100배 뇌동매매** (`TriggerImpulsiveTrade(100)`) |
| `Overdose` | ≤ 0 | 슬로우모션 + **`TriggerOverdoseTrade()`** (최초 진입 시 1회만) |

멘탈 0 + 총자산 ≤ 0이면 Overdose 엔딩 — 단 35초 보호 중에는 유예.

### 5.3 자연 감소

[TraderStatus.cs:340](../../Assets/Scripts/TraderStatus.cs#L340)

```
speedScale   = 5.0 / secondsPerGameMinute                 (0.666 기준 ≈ 7.5)
일차별 감소량 = min(0.15, 0.05 + 일차 × 0.005)
프레임 체력감소 = 일차별감소량 × 1.5 × (1 − 체력보호율) × speedScale × deltaTime
```

**체력-멘탈 연동**: 체력이 최대의 50% 이하로 떨어진 뒤부터, 체력이 줄 때마다 **같은 양만큼 멘탈도 감소**합니다. 50% 경계를 넘어 내려간 경우에는 경계 아래 초과분만 연동. 요청량이 아니라 **클램프 후 실제 변화량**을 기준으로 삼으므로, 체력이 0으로 클램프된 뒤에는 더 청구되지 않습니다. 체력이 0이라 `ChangeHealth`가 아무 변화도 못 만드는 경우에만 `DecreaseStatusOverTime`이 **연동 대비 2배 속도로** 직접 누적합니다.

**체력 임계 경고 대사** (DEAD-1, 2026-10-06) — 체력이 아래 경계를 처음 넘어 떨어질 때 요미가 한 번 말합니다. 한 번에 여러 경계를 넘으면 가장 심각한 것 하나만.

| 경계 | 바뀌는 동작 | 대사 요지 |
|---|---|---|
| 50% | 이후 체력 감소가 멘탈로 번지기 시작 (위 연동) | "이제부터 지치면 멘탈도 같이 깎여" |
| 40% | AI가 강한 함정 신호를 대박으로 오인 (§8.1 Tier 3) | "지금 요미 판단 믿으면 큰일 날지도 몰라" |
| 15% | 다음 신호에서 반대 방향 125배 폭주 매매 (Tier 4) | "요미 지금 제정신 아니야… 뭐라도 먹여줘" |

누적분은 2초마다 한 번씩 `"체력 저하"` 사유로 묶어서 차감합니다 (UI 스팸 방지). 이 사유는 **지뢰계 의상의 멘탈 감소 증폭에서 제외되는 유일한 경로**입니다 (`NaturalDrainReason` 상수로 비교).

### 5.4 최대치 확장

| 소스 | 효과 |
|---|---|
| 스트리트 볼캡 | 최대 체력 +30 |
| 미드나잇 잠옷 | 최대 멘탈 +15 |
| 스테이크 (배달음식) | 최대 멘탈 +10 **영구** + HP/멘탈 풀 회복 |

`MaxHealth`/`MaxMental` 프로퍼티가 의상 보너스를 포함하므로 비율 계산은 반드시 이쪽을 써야 합니다.

---

## 6. 상시 멘탈 소모 기믹 4종 (MentalDrainGimmickController)

클래스 주석에 명시: 기획에서 **수면 부족 연쇄·횡보 지루함·드로다운 트라우마·고배율 중독은 빠졌습니다.** 현재 4종(기믹 1·2·3·5)이 살아 있습니다.

### 기믹 1 — 미실현 손실 실시간 침식

ROE에 따라 **초당** 멘탈을 깎습니다. 1초 단위로 묶어서 차감 (UI 스팸 방지).

| ROE | 초당 감소 |
|---|---|
| > −5% | 0 (누적기 리셋) |
| −5% ~ −10% | 0.01 |
| −10% ~ −20% | 0.05 |
| ≤ −20% | 0.15 |

- 오버도즈 보호 중 또는 이벤트 포지션이면 **×0.5**
- **차트 공부 LV.10이면 완전 면역**
- ROE ≤ −20% 지속 시 25초 주기로 패닉 독백
- 치료 아이템(`CureMentalGimmicks()`)으로 1회성 면역 → 해제는 **`OnPositionClosed`에서만**. 무포지션 상태에서 치료하고 바로 진입했을 때 아이템 값어치가 사라지지 않게 `OnPositionOpened`에서는 지우지 않습니다.

### 기믹 2 — 연속 손절 콤보

| 연패 | 멘탈 페널티 | 추가 연출 |
|---|---|---|
| 1 | −4.0 | — |
| 2 | −9.0 | 휩소 자책 |
| 3 | −16.0 | 피해망상 (세력 조롱) |
| **4+** | **−0.0** | **즉시 100배 뇌동매매 발동** |

4연속에 추가 페널티를 주지 않는 이유: 뇌동매매가 발동하는데 페널티까지 겹치면 오버도즈가 함께 터집니다. 수익 청산 시 연패 카운터 리셋.

**수동 매매 책임 전가**: 플레이어가 직접 연 포지션(`CurrentOwner == Player`)의 손실이면 페널티 **×1.5** 증폭 + 요미의 원망 대사.

### 기믹 3 — 포지션 진입 비용

진입/물타기 **1회당 고정 멘탈 −5** (`PositionOpenMentalCost`). 방향·배율 무관.

### 기믹 4 — (결번)

고배율 중독 금단현상은 **2026-10-06 삭제되었습니다** (백로그 DEL-1). 다른 문서·백로그가 "기믹 5(FOMO)"로 참조하므로 번호는 당기지 않습니다.

### 기믹 5 — FOMO 놓친 기회 후회

1. AI가 신호를 보고 **진입을 포기**하면(`OnSignalEvaluationCompleted(signal, false)`) 주가 추적 시작. 단 이미 포지션이 있어서 실패한 경우는 "쫄아서 안 들어간 게 아니므로" 제외.
2. 8초 경과 후 판정: 신호가 실제로 진짜였거나, 주가가 **5% 이상** 움직였거나, 목표 변동률이 5% 이상이었으면 → **멘탈 −15** (차트 공부 LV.9+면 −7.5)
3. 60초간 큰 변동 없이 지나가면 "관망 성공"으로 보고 추적 종료
4. 수동 모드에서는 추적하지 않습니다

> `FindPlayerBrain()`이 `IsBossAI == false`인 브레인을 찾는 이유: 보스가 스폰되면 `[RequireComponent]`로 두 번째 `AITradingBrain`이 생기고, `FindAnyObjectByType`은 어느 쪽을 줄지 보장하지 않습니다. 보스 브레인을 잡으면 FOMO 추적이 엉뚱한 대상에 걸립니다.

---

## 7. 오버도즈 폭주 시퀀스

게임의 최종 페널티 기믹입니다. **전체 타임라인**:

```
멘탈 0 도달
  └─ TraderStatus.UpdateMentalState()
       ├─ 슬로우모션 2.5초/분 × 5초
       └─ TradingController.TriggerOverdoseTrade()
            ├─ 플레이어 수동 모드 강제 해제 (AI 제어권 탈환)
            ├─ 고속 스킵 일시정지 (골든타임 보장)
            ├─ 진행 중 돌발 이벤트 쉴드 파괴 (isEventTradeActive = false)
            ├─ 방향 결정:
            │    · 기존 포지션 있음 → 그 반대 방향으로 스위칭
            │    · 이벤트 빔 진행 중 → 빔의 정확히 반대 방향
            │    · 그 외 → 50% 랜덤
            ├─ 잔고 ≤ 1 이면 "최후의 발악"으로 $100 강제 부여
            └─ DelayedOverdoseRoutine (2초 대기)
                 ├─ 2초 임시 락 + 폭주 대사
                 ├─ 대기 중 멘탈 회복 감지 시 취소
                 ├─ 정산/게임오버 중이면 취소
                 ├─ isOverdoseTradeActive = true, 보호 35초
                 ├─ MarketEngine.TriggerOverdoseTrapSignal(방향, 35초)
                 │    · 가짜 신호 방송: TargetPercentageDelta = ±180% (대박 착각 유도)
                 │    · isOverdoseTrapOverride = true
                 └─ OpenPosition(방향, 잔고 100% 올인, 125배, isEmergencyTrade: true)
```

### 7.1 죽음의 차트 빔

[MarketSimulationEngine.cs:673](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L673)

```
노이즈 ×0.03   (휩소가 청산을 방해하거나 엉뚱한 반등이 나오는 것을 원천 차단)
OU 항 = 0
drift = (포지션 반대방향 ±0.015) / max(15, 남은초) × secondsPerGameMinute
```
125배 기준 0.8% 역행 시 −100%. 약 20~25초에 걸쳐 확실하게 청산선에 도달합니다. 스프레드도 최소화됩니다.

### 7.2 35초 보호 쉴드 동안 금지되는 것

`isOverdoseTradeActive && Time.time < overdoseProtectionEndTime`:
- AI의 익절/손절/물타기 등 **모든 자동 판단 중지** (StandardAuto 포함)
- 이벤트 포지션 반응 로직 전면 차단
- 신규 시그널 수신 차단
- 돌발 이벤트 발생 차단
- 아이템 사용 금지 ([ItemUser.cs:43](../../Assets/Scripts/Items/ItemUser.cs#L43))
- 총자산 0 이하라도 **게임오버 유예**

**허용되는 것은 오직 강제 청산(`CheckLiquidation`)뿐입니다.**

### 7.3 이탈 조건

- **멘탈 회복**: 함정 신호는 `CancelOverdoseTrapSignal()`로 해제되지만 **포지션과 차트 함정은 수동으로 청산하기 전까지 유지**됩니다. 5초 주기로 안내 대사. `ResumePreservedFastForward()`로 보존된 고속 스킵 재개.
- **청산 도달 + 잔고 0**: `TriggerOverdoseEnding()` → Overdose 배드엔딩 확정
- **35초 경과**: `isOverdoseTradeActive = false`, 함정 해제

### 7.4 오버도즈 중 이벤트 "통수"

이미 오버도즈 중일 때 돌발 이벤트 선택지가 들어오면, **이벤트 의도를 무시하고 그 반대 방향으로 125배 풀레버리지 전환**합니다 ([TradingController.cs:1506](../../Assets/Scripts/Trading/TradingController.cs#L1506)).

---

## 8. AI 자동매매 (AITradingBrain)

> ⚠️ 이름과 달리 **LLM을 사용하지 않습니다(사용한 적도 없습니다)**. 규칙 기반입니다. 삭제하면 자동매매·FOMO 기믹·차트 힌트가 함께 죽으므로 "LLM 잔재"로 오인해 제거하지 마십시오.

### 8.1 4단계 기만 티어 (Deception Tier)

**체력 비율**이 판단력을 결정합니다. 체력이 낮을수록 가짜 신호를 진짜로 오인합니다.

| Tier | 조건 | 거동 |
|---|---|---|
| 🟢 1 Stable | 체력 > 75% | Weak+진짜 → 소액 진입 / Strong+진짜 → 적극 진입 / **Strong+가짜 → `신호정확도` 확률로 간파해 역방향(카운터) 진입** |
| 🟡 2 Mildly Deceived | 체력 40~75% | Weak 신호(진위 무관)에 적당히 속아 15% 진입 / Strong+진짜 정상 진입 / 그 외 `(1−정확도)×2` 확률로 낚임 |
| 🟠 3 Heavily Deceived | 체력 15~40% | **Strong+가짜(트랩)를 대박 자리로 오인**해 개방된 최대 레버리지 쏟아부음, 목표 +15% |
| 🔴 4 Overdose | 체력 ≤ 15% 또는 Danger/Overdose | `ExecuteOverdoseTrade` — 신호 **반대 방향**으로 잔고 95%, 125배, 목표 ±50%, **손절선 없음** |

### 8.2 AI 성향 3종 (플레이어가 UI에서 선택)

| 성향 | 레버리지 | 증거금 비율 | 특징 |
|---|---|---|---|
| `Safe` | 5배 고정 (또는 ×0.5) | 0.15~0.20 | 약한 신호 관망, 함정 의심 시 회피, 손절 강도 ×0.5, 익절 배율 ×0.5 |
| `Balanced` | `max(기본, 레벨상한 × 0.6~0.7)` | 0.20 기본 | 기본 동작 |
| `Aggressive` | 레벨 상한 전부 (최대 125) | 0.8~1.0 | **손절선 0 (노손절)**, 함정에 무조건 낚임(`trapProb = 1.0`), 익절 배율 ×2.0 |

### 8.3 진입 계산 (OpenNormalPosition)

```
방향 = 신호 종류에서 유도 (Breakout은 순방향, Trap은 꼬시는 방향)
  └─ 차트 공부 오판: 진짜 신호인데 random > 신호정확도 면 반대 방향 역진입
     (단 이벤트 확정 빔 중에는 오판 로직 무시 — 요미가 완벽하게 맞춤)

진입가 = 신호 시작가 × (1 ± deltaPct × 진입지연패널티)      ← 차트 공부 귀속
목표가 = 진입가 × (1 ± deltaPct × 익절배율)                 ← 큐브 풀기 귀속
손절가 = 진입가 × (1 ∓ 손절강도)                            ← 책읽기 귀속
```

### 8.4 확정 구간 종료 시 2중 보장

`GuaranteedOverride → Cooldown` 전이 시 ([AITradingBrain.cs:217](../../Assets/Scripts/AI/AITradingBrain.cs#L217)):
- **진짜 신호 + 체력 > 40%**: 노이즈로 목표가에 미세하게 닿지 못했어도 **즉시 익절 청산**해 기획된 수익률을 보장
- **가짜 신호/트랩**: 휩소 갇힘 방지를 위해 손절/정리

### 8.5 차트 힌트 (수동 매매 시)

플레이어가 직접 진입하면 `ProvideChartHintToPlayer()`가 말풍선을 띄웁니다. 차트 공부 레벨 7 이상(또는 4 이상 + 정확도 성공) → 함정 간파/좋은 타점 힌트. 그 미만 → 혼란·맹신 리액션. `TraderMemoryManager`에 가중치 6으로 기억 저장.

---

## 9. 성장·강화 시스템

### 9.1 주인공 레벨 (TraderLevelSystem)

- 경험치: **익절 시에만** `10 + √pnl × 0.6 + leverage`. PnL ≤ 0이면 지급 없음.
- 레벨업 필요량: `400 × 1.4^(레벨−1)`

| 레벨 | 최대 레버리지 | 최대 증거금 비율 | 익절 멘탈 회복 배율 |
|---|---|---|---|
| 1 | 5배 | 15% | 0.50 |
| 3 | 10배 | 25% | 0.60 |
| 5 | 20배 | 35% | 0.80 |
| 7 | 30배 | 45% | 1.00 |
| 10 | 50배 | 60% | 1.30 |
| 15 | 100배 | 85% | 1.80 |
| 20+ | **125배** | **100%** | **2.00** |

(전 구간 표는 [TraderLevelSystem.cs:139](../../Assets/Scripts/Trading/TraderLevelSystem.cs#L139))

### 9.2 스킬 3종 (최대 LV.10)

| 스킬 | 담당 능력치 | 비용 공식 | 체력 | 시간 |
|---|---|---|---|---|
| 차트 공부 `ChartStudy` | 신호 정확도, 진입 지연 | `3500 × 1.65^(lv−1)` | −20 | 3시간 |
| 큐브 풀기 `CubePatience` | 익절 목표 배율 | `2000 × 1.60^(lv−1)` | −12 | 1시간 |
| 책읽기 `BookJudgment` | 손절 강도 | `2500 × 1.62^(lv−1)` | −25 | 4시간 |

모든 비용에 `× 1.15^(일차−1)` 인플레이션이 곱해집니다.

**레벨별 효과**

| LV | 신호 정확도 | 진입 지연 패널티 | 익절 배율 | 손절 강도 |
|---|---|---|---|---|
| 1 | 0.60 (40% 오진입) | 7% | 0.25 | 9.0% |
| 5 | 0.80 | 3% | 0.65 | 7.0% |
| 8+ | **0.90** (오진입 10% 잔존) | **0%** | 0.84 | 5.5% |
| 10 | 0.90 | 0% | **0.90** | **5.0%** |

- 오피스룩 의상: 신호 정확도 **+0.05**
- LV.10 보너스: 차트 공부 → 미실현 손실 기믹 **완전 면역** / LV.9 → FOMO 페널티 절반

**업그레이드 절차 주의**: `AdvanceGameMinutes`는 동기 루프라 그 안에서 고속 진행이 전부 끝납니다. **반드시 레벨 증가·이벤트 발행 뒤에** 호출해야 합니다 — 앞에 두면 그 구간 전체가 옛 스킬 레벨로 시뮬레이션됩니다 ([TraderLevelSystem.cs:457](../../Assets/Scripts/Trading/TraderLevelSystem.cs#L457)).

### 9.3 액티브 아이템 (패시브 버프)

`ActiveItemEffectManager` — 구매 즉시 상시 적용, 레벨당 누적.

| 효과 타입 | 적용 지점 | 상한 |
|---|---|---|
| `ProfitBoost` | 청산 시 이익 `× (1 + rate)` | 없음 |
| `LossReduction` | 청산 시 손실 `× (1 − rate)` | 80% |
| `MentalDrainGuard` | 체력-멘탈 연동 감소 완화 | 80% |
| `HealthDrainGuard` | 자연 체력 감소 완화 | 80% |

업그레이드 가격: `기본가 × 1.2^((일차−1)/2) × 배율^현재레벨`

### 9.4 의상 (CostumeManager) — 14종

| 의상 | 효과 | 가격 |
|---|---|---|
| 기본 복장 | 없음 | 0 |
| 스트리트 볼캡 | 최대 체력 +30 | 5,000 |
| 핑크 간호사 | 영양제·진정제 효율 +15% | 10,000 |
| 미드나잇 잠옷 | 최대 멘탈 +15 | 12,000 |
| 블랙 치파오 / 전통 한복 / 나팔꽃 유카타 | 배달음식 회복량 +15% | 15,000 |
| 바텐더 | 에너지 드링크 체력 회복 +15% | 30,000 |
| 클래식 메이드 | 파르페(`dessert`) 멘탈 회복 +15% | 30,000 |
| 지뢰계 패션 | **익절 수익 +20% + 체력/멘탈 +10**, 페널티: **모든 멘탈 감소 ×1.25** (체력 저하 자연 감소 제외) | 80,000 |
| 동탄룩 | 아이템 구매 비용 −15% | 150,000 |
| 오피스룩 | 거래 정확도 +0.05 | 250,000 |
| 바니걸 | **자동매매** 익절 수익 +15% | 1,000,000 |
| 화이트 비키니 | **수동매매** 익절 수익 +15% | 1,000,000 |

의상 버프는 서로 배타적으로 적용됩니다(if/else if 체인) — 지뢰계가 가장 마지막 분기. 대부분 업적으로 해금됩니다(§13).

### 9.5 소모 아이템

| 종류 | 효과 |
|---|---|
| 체력 회복형 | 체력 +효과량. 만땅이면 사용 실패 |
| 멘탈 회복형 | 멘탈 +효과량 + **멘탈 기믹 1회성 치료**. 멘탈이 가득 차 있으면 사용 실패 |
| 마라탕 | HP +25 / 멘탈 +50 |
| 초밥 | HP +30 / 멘탈 +55 |
| 떡볶이 | HP +30 / 멘탈 +60 |
| 파스타 | HP +15 / 멘탈 +15 + **180초간 시간 1.5배 가속** |
| 스테이크 | **최대 멘탈 +10 영구** + HP/멘탈 풀 회복. 업적 `purchase_delivery_200` 해금 + **7일 쿨다운** |

상점 가격: `기본가 × 1.2^((일차−1)/2)`에 난이도 보정, 동탄룩이면 −15%.

---

## 10. 돌발 선택 이벤트 (ChoiceEventController)

### 10.1 트리거 규칙

| 항목 | 값 |
|---|---|
| 하루 상한 | 거래 가능 시간 ≥ 6시간이면 **2회**, ≥ 3시간이면 1회, 3시간 미만이면 **0회** |
| 첫 이벤트 예정 | 10:00 ~ 16:00 랜덤 |
| 다음 이벤트 | 이전 발생 +1시간 ~ +6시간 랜덤 (23:20 한도) |
| 발생 금지 | 10시 이전, 24시 이후, 오버도즈 중, 고속 스킵 중 |
| 쿨다운 | 인게임 60분 **AND** 실시간 10초 |
| 멘탈 위기 트리거 | 멘탈 비율 ≤ 15%, **하루 1회**, 실시간 180초 쿨다운 |
| 확정 수익 아이템 선택지 | **하루 1회** |

- 하루 상한은 **현재 시각이 아니라 그날의 거래 개시 시각**으로 계산합니다(미연시 슬롯 1개 = 3시간). 현재 시각으로 재면 정상적인 09:00 시작에서도 저녁에 한도가 줄어 평소 동작이 바뀝니다.
- **시계 점프 감지**: 같은 일차인데 예정 시각이 이미 지나 있으면(미연시 슬롯 소모 등) 현재 시각 기준으로 재조정합니다. 없으면 거래 개시 첫 분에 팝업이 터집니다.
- 표시 실패 시 하루 발생 횟수를 소모하지 않고 10분 뒤 재시도.
- 팝업 중 `GameManager.PauseGame()`. 컴포넌트가 비활성화되면(`OnDisable`) Paused 고착을 막기 위해 강제 재개합니다.

### 10.2 데이터 소스

1. **주 풀**: `Assets/Resources/Events/Templates/`의 `EventLogicTemplateSO` **242개**. 선택지가 3개 미만이면 풀에서 제외.
2. **예비 풀**: `Assets/Resources/Events/EVENT_*.asset` 수작업 이벤트 **30개** — 템플릿 풀이 비었을 때만.
3. 제목·본문·요미 대사는 모두 `FallbackTitle` / `FallbackDescription` / `FallbackMonologues`(랜덤 1개)에서 가져옵니다. **LLM 생성은 2026-09-22에 전면 제거되었습니다.**

### 10.3 선택지 타입과 성패 판정

| `ChoiceOptionType` | 베팅 여부 | 판정 |
|---|---|---|
| `Safe` | 아님 | 판정 없음, 보상 무조건 적용, 포지션 전량 청산 |
| `SpecialItem` | 아님 | **항상 성공**, 아이템 차감 필요 |
| `Aggressive` / `DirectionalLong` / `DirectionalShort` | **베팅** | `random ≤ OverrideSignalProbTrue` |

### 10.4 효과 적용 순서 (ApplyOptionEffects)

```
1. 바이탈
   성공/비베팅 → MentalChangeAmount, HealthChangeAmount
   실패         → MentalPenaltyOnFail, HealthPenaltyOnFail
   ※ 단일 이벤트 멘탈 페널티 절대 상한 −35 (데이터와 무관하게 강제)
2. 게임오버 확인 → 종료 시 중단
3. 매매 제어
   핸들링 모드가 StandardAuto인 베팅이면
     성공 → GreedyHold / 실패 → HoldToMitigateLoss 로 승격
   ExecuteEmergencyTrade(방향, 동적레버리지, 쉴드초, 모드, 목표ROE, 손절ROE,
                         isPlayerChoice: true, isTrueSignal: 성패)
4. 게임오버 재확인
5. 차트 강제 빔
   OverrideMarketTrend(최종빔%, 30 인게임분, isTrap = 베팅 && 실패)
```

**빔 부호 규칙** (`ResolveBeam`) — 데이터의 **크기**만 기획값으로 쓰고 **부호는 포지션과 성패에서 유도**합니다:
- 관망(포지션 없음): 데이터 부호 그대로. 트랩 아님.
- 성공: 포지션 방향으로 `+|빔|`
- 실패: 포지션 **반대** 방향으로 `|빔| × 0.7` (`TrapBeamRatio`)

**동적 레버리지 캡**: `min(기본값, 플레이어최대 + 일차 × 7, 125)`. 기본값 미지정 시 베팅 100배 / 비베팅 10배.

**증거금**: `customMarginRatio` 또는 **잔고의 40%**, 최소 $10. 잔고 ≤ 1이면 최소 보증금 $100으로 가정해 포지션을 보장합니다.

**쉴드**: `OverrideDurationSeconds` ≥ 30이면 그 값, 미만이면 **기본 150초** (legacy 데이터 단위 오해 방어). `TradingController`는 다시 `max(30, 값)`을 적용합니다.

### 10.5 이벤트 포지션 핸들링 모드 5종

쉴드가 살아 있는 동안 `ProcessEventPositionReaction()`이 매 틱 판정합니다. [TradingController.cs:243](../../Assets/Scripts/Trading/TradingController.cs#L243)

| 모드 | 진행 중 판정 | 쉴드 종료 시 |
|---|---|---|
| `StandardAuto` (0) | 지정 익절 ROE 도달 시 청산 / 고점 ≥ +40%면 `고점 × (0.75 + (차트LV−1)×0.015)` 트레일링 / 5초 경과 후 ROE ≤ −65% 손절 | ROE ≥ +15% 익절, ≤ −30% 손절, 그 사이는 일반 AI로 이관 |
| `InstantTakeProfit` (1) | `빔% × 레버리지 × 익절배율` 또는 지정값 도달 시 즉시 칼익절 | 위와 동일 |
| `InstantStopLoss` (2) | `−손절강도 × 333%` 또는 지정값 도달 시 즉시 손절 | 위와 동일 |
| `HoldToMitigateLoss` (3) | ROE ≤ **−88%**에서만 비상 청산. 최저가 −40% 이하를 찍었거나 물타기를 했다면 `−12 + (책LV−1)×1.5`% 반등 시 탈출 | 손익 무관 즉시 정리 (전용 대사) |
| `GreedyHold` (4) | 지정 목표 도달 시 청산. 아니면 3단 트레일링: 고점 150%↑ → 0.75배, 80%↑ → 0.80배, 45%↑ → 0.82배 (각 `+(차트LV−1)×0.015`) | ROE > 0이면 전액 익절, 아니면 정리 |

### 10.6 악결과 비상 물타기

`HoldToMitigateLoss` 또는 `StandardAuto` + **가짜 신호**일 때, ROE가 −75% ~ −92% 구간에 들어오고 잔고 > 10이면 **1회** 발동:

```
추가 증거금 = min(잔고 × 0.45, 기존증거금 × 1.5)   (10 초과일 때만)
```
평단가를 낮춰 반등 꼬리 탈출을 준비합니다. `lastReportedROEBasket = -999`로 표식.

### 10.7 실시간 ROE 중계

| 조건 | 임계 | 동작 |
|---|---|---|
| 플레이어 선택 + 가짜 신호 | ROE ≤ −40% | 원망 대사 + **멘탈 −15** |
| 플레이어 선택 + 가짜 신호 | ROE ≤ −65% | 절규 대사 |
| AI 판단 + 가짜 신호 | ROE ≤ −50% | 대사 |
| 진짜 신호 | +50% / +100% / +200% | 단계별 환호 대사 |

일반 포지션의 ROE 대사는 ±15%, ±30% 돌파 시 **12초 쿨다운**으로 출력됩니다.

---

## 11. 하루 마감과 정산

### 11.1 24:00 시퀀스

[GameManager.cs:874](../../Assets/Scripts/GameManager.cs#L874)

1. 열려 있는 포지션 **전량 강제 청산** (당일 손익 확정)
2. 강제 청산으로 게임오버 발생 시 정산·저장 중단
3. 예정 위약금을 미리 계산해 **파산 예정 여부** 판정 — 위약금이 `StoryPenaltiesEnabled = false`로 0인 동안은 사실상 항상 "파산 예정 아님"
4. 파산 예정이 아니면 **자동 저장** (정산 화면을 보고 꺼도 진행 상황 유지). 파산 확정이면 저장 생략 → 아침 09:00부터 재시작 가능
5. `ProcessDailySettlementWithStory()` → `Settlement` 상태, 위약금 차감(현재 0), 컷씬 재생, `OnDayEnded` 발행

### 11.2 정기 지출 — 삭제됨

일차별 강제 차감(3·7·11·15·18일차 + 엔드리스 21일차 이후 3일 주기)과 요미의 지출 예고 대사는 **2026-10-06 삭제되었습니다** (§23, 백로그 DEL-2). 현재 하루 마감 시 잔고를 깎는 경로는 스토리 위약금(꺼져 있음)뿐입니다. 자산 압박 설계는 메인스토리 편입과 함께 재설계 예정입니다.

### 11.3 다음 날 진입 (FinalizeProceedToNextDay)

1. 유예된 파산 판정 일괄 검사
2. 09:00으로 시각 리셋, 날짜 +1일
3. `StartOfDayEquity` 갱신, 자산 스파크라인 리셋
4. **체력·멘탈 모두 최대로 회복**
5. 차트를 **이어 붙임** — `MarketSimulationEngine.RollOverToNewDay()`가 진행 중 캔들을 마감하고 시간축을 다음 1440분 경계로 옮깁니다. 어제 종가가 오늘 시가가 되고 D1 캔들이 하루에 하나씩 쌓입니다. 요미의 방에서 넘긴 경우는 다음 GameScene 진입 시 엔진이 세이브의 `MarketLastUpdatedDay < CurrentDay`를 보고 스스로 넘깁니다. (FIX-1, 2026-10-06)

### 11.4 자산 스파크라인

`GameManager`가 보유합니다(과거 UI의 private 필드라 저장이 구조적으로 불가능했습니다). 인게임 **15분마다** 표본, 최대 **96개**(= 24시간). `IsDailyPnlPartial`은 세이브 로드 후의 부분 집계를 표시해 정산 라벨이 `P&L SINCE LOAD`로 바뀝니다.

### 11.5 정산 계산

```
당일 손익 = 현재 총자산 − StartOfDayEquity
당일 수익률(%) = 당일손익 / StartOfDayEquity × 100
총자산 = 현금 잔고 + 증거금 + 미실현 손익       (TraderStatus.GetTotalEquity)
```

### 11.6 요미의 방 정산

`RoomSettlementEnabled = true`. 요미의 방에서 취침하면 **씬 전환 없이** 그 자리에서 `AdvanceClockWithoutSimulation(24h)` → `ProcessDailySettlementWithStory()`. 남은 시간을 1분씩 시뮬레이션하지 않는 이유: 취침 시점에 열린 포지션이 있을 수 없고(방 → GameScene은 단방향), 체력·멘탈은 다음 날 어차피 최대로 회복되며, 차트는 다음 GameScene 진입 시 엔진이 이어 붙입니다. [GameManager.cs:1385](../../Assets/Scripts/GameManager.cs#L1385)

---

## 12. 엔딩 조건

| 엔딩 | 조건 |
|---|---|
| `Bankruptcy` | **총자산 ≤ 0 AND 현금 잔고 ≤ 0** (멘탈 정상) |
| `Overdose` | 위 조건 + 멘탈 Overdose 상태, 또는 오버도즈 올인 청산으로 잔고 0, 또는 보스전 패배 |
| `Success` | 최종 보스(20일차 사채업자) 자산 초과 — **현재 보스 시스템 OFF라 스토리 모드에서는 도달 불가** |

- **목표 자산 도달 자동 클리어는 철폐되었습니다.** 올인 진입 시 현금이 0이어도 증거금에 자산이 살아 있으므로, 총자산과 현금이 **모두** 0 이하인 진짜 파산 시점만 트리거합니다.
- 정산 중(`isSettlementProcessing`)에는 파산 판정을 유예합니다.
- 청산처럼 잔고 변경 이벤트 없이 증거금이 소멸하는 경로는 `EvaluateEndingConditions()`를 명시적으로 호출해야 엔딩 판정이 누락되지 않습니다.
- `StoryDayLimitEnabled = false` — **20일차 강제 엔딩 판정이 꺼져 있어 21일차 이후로 계속 진행됩니다** ([GameManager.cs:173](../../Assets/Scripts/GameManager.cs#L173)).

---

## 13. 업적과 해금

`AchievementManager` — PlayerPrefs 기반(세이브 슬롯과 무관한 계정 단위). 대부분 **의상 해금** 보상입니다.

| 업적 | 조건 | 보상 |
|---|---|---|
| 자본주의의 기적 | 최초 클리어(진엔딩) | 동탄룩 |
| 과부하 | 오버도즈 엔딩 | 미드나잇 잠옷 |
| 트레이딩 마스터 | 모든 스킬 LV.10 | 오피스룩 |
| 카페인 중독 I / II | 에너지 드링크 50 / 100개 | 스트리트 볼캡 / 바텐더 |
| 당분 중독 | 파르페 100개 | 클래식 메이드 |
| 마라탕/초밥/떡볶이 중독자 | 각 50개 | 치파오 / 유카타 / 한복 |
| 큰손 고객 | 배달음식 누적 200개 구매 | **스테이크 구매 해금** |
| 하이 리스크 하이 리턴 | 돌발 이벤트 위험 선택지 20회 성공 | 지뢰계 패션 |
| 억만장자의 길 I / II | 누적 최고 자산 2,500만 / 5,000만 | 간호사 / 바니걸+비키니 |
| 첫 쓴맛 / 빈털터리 / 백만장자 | 각 조건 | 없음 |

---

## 14. 보스 시스템 (현재 비활성)

`BossManager.BossesEnabled = **false**` (2026-08-15 스토리 개편 기간 동안 OFF)

- **스토리 모드에서만 꺼집니다.** 엔드리스·챌린지는 보스전이 모드의 존재 이유이므로 스위치와 무관하게 항상 켜집니다 (`BossesActive`).
- `const`가 아니라 `static readonly`인 이유: const면 하위 코드가 도달 불가로 판정되어 경고가 쏟아지고 되살릴 때까지 진짜 문제를 가립니다.
- 편성: 3/6/9/12/15/18일차 + **20일차 사채업자(최종 보스)**. 보스 자산 = 플레이어 자산 × 30~90%, 스킬 레벨 3~8.
- `BossLevelProvider`가 `ITraderLevelProvider`를 구현해 **같은 AITradingBrain 로직을 다른 능력치로** 돌립니다. 보스는 체력/멘탈 소모가 없습니다(`IsBossAI = true`).
- 보스 포지션은 가상 계산(`BossAIController`)이며 실제 `TradingController`를 쓰지 않습니다. 최대 홀딩 120초.
- 고속 스킵 중 놓친 신호는 `SimulateBulkTrades()`로 일괄 정산 (정확도 확률로 승패 판정).
- ⚠️ **`AITradingBrain`은 보스 시스템이 아닙니다.** `IsBossAI` 분기를 갖고 있을 뿐 평상시 자동매매의 실행 주체입니다. 함께 끄지 마십시오.
- `IsBossScheduledDay()`는 스위치와 무관하게 편성 여부를 답합니다 — 날짜 점프 클램프처럼 **일정을 보호하는 쪽**은 기능이 꺼져 있어도 그 날을 피해야 합니다.

---

## 15. 모드별 규칙 차이

| | Story | Endless | Challenge | P2P |
|---|---|---|---|---|
| 저장 | ✅ (슬롯 3개) | ❌ | ❌ | ❌ |
| AI 자동매매 | ✅ | ✅ | **❌ 전면 금지** | 플레이어 수동 고정 |
| 보스 | **OFF** (스위치) | ✅ | ✅ | 미사용 |
| 시작 자본 | 난이도별 | 7,000 | 7,000 | 호스트 규칙 |
| 돌발 이벤트 하루 상한 | 거래 시간에 따라 0~2 | 2 | 2 | 미사용 |

**난이도 (Story 전용)** — [StoryDifficulty.cs](../../Assets/Scripts/System/StoryDifficulty.cs)

| 난이도 | 시작 자본 | 상점 가격 배수 | 인플레이션 적용률 |
|---|---|---|---|
| Easy | $40,000 | 0.40 | **0** (인플레이션 없음) |
| Normal | $20,000 | 0.70 | 0.5 |
| Hard | $7,000 | 1.00 | 1.0 |

**챌린지 모드의 차단 지점** (`IsAITradingLockedByGameMode`):
- `Awake`에서 `Player_Manual` 고정
- `SetTradingMode(AI_Auto)` 거부
- `OpenPosition`에서 AI 진입 차단 — 단 **플레이어가 돌발 이벤트에서 직접 방향을 선택한 결과(`isEmergencyTrade && isPlayerDirectedTrade`)만 플레이어 입력으로 인정**
- `TriggerOverdoseTrade` 스킵, 모든 기믹의 주도권 강탈 무효 (`LockManualModeTemporarily`가 챌린지에서 즉시 반환)

**P2P 모드**: `TradingController` / `MarketSimulationEngine` / `TraderStatus` / `GameManager`가 각각 `EnableP2PExternalMode()`로 **표시 전용 슬레이브**가 됩니다. 로컬 시뮬레이션·기믹·엔딩 판정을 전부 멈추고 호스트 스냅샷만 반영합니다. 캔들 꼬리는 호스트가 집계한 고가/저가를 보존해야 클라이언트마다 차트가 달라지지 않습니다.

---

## 16. 저장되는 트레이딩 상태

`SaveLoadManager`가 `persistentDataPath/save_slot_{0..2}.json`에 `JsonUtility`로 기록. **Story 모드에서만** 저장됩니다.

| 소유자 | 저장 항목 |
|---|---|
`GameManager` | 잔고, 날짜/시각, `StartOfDayEquity`, 정산 컨텍스트, 자산 스파크라인 |
`TradingController` | 거래 모드, AI 성향, **이벤트 포지션 계약**(모드/목표ROE/손절ROE/플레이어선택/진위), 포지션 전체 |
`MarketSimulationEngine` | 현재가, 24h 고저/거래량, 국면, 일일 국면, **국면 갱신 일차**, 국면 유지 시간, 누적 분, 전체 캔들 히스토리(평탄화) |
`TraderStatus` | HP, 멘탈, 멘탈 상태, 최대 멘탈, **연패 카운터** |
`ChoiceEventController` | 일일 이벤트 스케줄·발생 횟수·쿨다운 |
`DailyMarketOutlook` | 일차, 방향성, 공개 여부 |
기타 | 액티브 아이템 레벨, 의상 보유/착용, 스킬·주인공 레벨, 인벤토리, 보스 자산 |

**복원 시 주의**
- 이벤트 **보호 쉴드 시간은 `Time.time` 기준이라 복원할 수 없습니다.** 보호를 잃은 채로 특수 익절/손절 계약만 유지하는 쪽이 안전합니다.
- 포지션이 없으면 베이스에 남은 옛 포지션을 **반드시 지워야** 합니다.
- 차트 가격 0인 세이브(요미의 방에서 시작한 새 게임)를 복원하면 OU 항이 NaN이 되므로 복원을 건너뛰고 정상 초기화합니다.
- `UpdateMentalState()`를 통째로 부르면 멘탈 0 세이브를 불러오는 순간 오버도즈 강제매매가 터집니다 — 추적기만 맞춰 둡니다.
- `SaveDataMigrator.Migrate()`가 버전 비교 파이프라인을 돕니다. `SaveData` 형태를 바꾸면 **`SaveData` + `SaveGame()` 수집부 + 로드 산포부 + 마이그레이터** 네 곳을 함께 고쳐야 합니다.

---

## 17. 현재 비활성·죽은 기믹

성격이 다른 두 묶음입니다. **17.1은 의도적으로 꺼 둔 것이라 그대로 두고, 17.2는 "동작해야 하는데 동작하지 않는" 것이라 처리 결정이 필요합니다.**

### 17.1 의도적 비활성 — 유지

전부 명시적 결정의 결과입니다. 되살릴 때 바꿀 지점이 한 곳으로 모여 있으므로 **손대지 않습니다.**

| 대상 | 상태 | 되살리는 법 |
|---|---|---|
| 보스 시스템 | 스토리 모드에서 OFF (`BossesEnabled = false`) | `true` 한 글자. 엔드리스·챌린지는 이미 켜져 있음 |
| 20일차 강제 엔딩 판정 | OFF (`StoryDayLimitEnabled = false`) → 성공 엔딩 도달 불가 | `true` 한 글자 |
| 스토리 위약금 | OFF (`StoryPenaltiesEnabled = false`) | `true` 한 글자. 런타임 사본만 0이라 원값이 그대로 돌아옴 |
| 수면 부족 연쇄 / 횡보 지루함 / 드로다운 트라우마 | 기획에서 제외, 분 단위 핸들러째 제거 | 핸들러 + `OnGameMinuteAdvanced` 구독을 함께 복구 |
| LLM 전반 | **2026-09-22 전면 제거.** 프로젝트에 LLM이 존재하지 않음 | 되살릴 계획 없음 |
| 고배율 중독 금단현상 (구 기믹 4) | **2026-10-06 완전 제거.** 상태·세이브 필드·치료 아이템 경로·영구 수동 락 API까지 함께 삭제 (§22) | 되살릴 계획 없음 |
| 정기 지출 + 요미 지출 예고 | **2026-10-06 완전 제거.** 금액표·예고 대사·정산 UI 분기·세이브 2필드까지 함께 삭제 (§23). 엔드리스 장기 압박은 공백 — 재설계 대상 | 되살릴 계획 없음 |

### 17.2 죽은 코드 — 의도 판정과 개선안

"원래 동작해야 했는가"를 기준으로 분류했습니다. **A = 의도는 살아 있으나 구현이 비어 있음(고쳐야 함) / B = 의도 자체가 소멸(지워야 함).**

#### 🅰 체력 임계치 돌파 대사 — 구현만 비어 있음 → ✅ 채움 (2026-10-06, DEAD-1)

> 처리 결과: 아래 개선안은 경계를 50%/20%로, 20%를 "Tier 3 진입"으로 적었지만 **실제 Tier 경계는 40%(Tier 3)와 15%(Tier 4)**입니다. 그래서 **동작이 실제로 바뀌는 50%/40%/15%** 세 경계로 맞췄고(§5.3 표), 체력은 요미 자신의 상태이므로 요미가 자기 상태를 말하는 대사로 썼습니다. 새 대사의 글자는 전부 폰트 아틀라스에 이미 있어 프리베이크가 필요 없었습니다. 아래는 처리 전 기록입니다.

[TraderStatus.cs:388-400](../../Assets/Scripts/TraderStatus.cs#L388) — 주석은 "체력 임계치 돌파 시 유동적 대사 호출"이라고 선언하는데 **두 분기의 본문이 비어 있습니다.**

```csharp
if (prevRatio > 0.5f && currRatio <= 0.5f) { }       // 50% 돌파
else if (prevRatio > 0.2f && currRatio <= 0.2f) { }  // 20% 돌파
```

**의도는 명백히 살아 있습니다** — 체력 50%는 멘탈 연동이 시작되는 경계(§5.3)이고 20%는 AI가 Tier 3(심각한 오인)로 떨어지는 구간(§8.1)입니다. **플레이어에게 가장 알려줘야 할 두 지점인데 아무 피드백이 없습니다.**

**개선안** — 기존 대사 경로를 그대로 재사용하면 2줄입니다.
```csharp
// 이미 TradingController가 쓰는 것과 같은 방식
var visual = FindAnyObjectByType<FXOverdose.AI.AIVisualController>();
visual?.DisplayDialogueBalloon(line, DialoguePriority.High, EventCategory.MentalChange);
```
- 50% 돌파 → "오빠 눈이 충혈됐어… 이제부터 지치면 멘탈도 같이 깎여" (연동 시작 고지)
- 20% 돌파 → "오빠 손이 떨려… 지금 판단력 믿으면 안 돼" (Tier 3 진입 경고)

`EventCategory.MentalChange`를 쓰면 `YomiDialogueMatcher`에 자산을 넣어 교체할 수 있습니다. **비용** 2~3줄 + 대사 2줄 / **효과** 중 — 체력이라는 스탯이 처음으로 플레이어에게 말을 겁니다.

> ⚠️ 새 한국어 리터럴을 넣으므로 `Tools/Prebake All Scripts Text into Font` 재실행이 필요합니다(§18).

#### 🅰 멘탈 상태 전이 대사 (Anxious / Stable) — 구현만 비어 있음

[TraderStatus.cs:545-558](../../Assets/Scripts/TraderStatus.cs#L545) — 위와 같은 형태의 빈 블록 2개입니다.

```csharp
currentMentalState = MentalState.Anxious;
if (lastTrackedMentalState == MentalState.Stable) { }   // 안정 → 불안
...
currentMentalState = MentalState.Stable;
if (lastTrackedMentalState != MentalState.Stable) { }   // 회복 → 안정
```

`Danger`(40% 확률 뇌동매매)와 `Overdose`(폭주)는 동작하는데 **중간 두 단계만 무음**입니다. 멘탈 4단계(§5.2) 중 절반이 플레이어에게 보이지 않습니다.

**개선안** — 위와 동일한 패턴. 전이 방향이 두 가지(악화/회복)이므로 `EventCategory.MentalChange` + `DialoguePriority.Normal`로 충분합니다. 회복 쪽 대사가 있어야 아이템 사용의 보상감이 생깁니다. **비용** 2~3줄 / **효과** 중.

#### 🅰 `AITradingBrain.HandlePositionClosed` 리액션 — 분기만 남음

[AITradingBrain.cs:711-738](../../Assets/Scripts/AI/AITradingBrain.cs#L711) — `roe`·`baseMargin`을 **계산까지 해 놓고** 분기 4개가 전부 비어 있습니다.

```csharp
float roe = baseMargin > 0f ? (pnl / baseMargin) * 100f : 0f;
if (pnl < 0f) { if (Weak 신호) { } else { } }
else if (pnl > 0f) { }
else { }
```

**두 선택지가 있습니다.**
- **채운다** — "약한 신호에 속아서 잃었다 / 강한 신호였는데도 잃었다"를 구분하는 **신호 종류별 사후 반성 대사**는 §21의 셋업 확장과 맞물려 가치가 큽니다. AI가 자기 판단을 되짚는 유일한 지점입니다.
- **지운다** — `TradingController.ClosePosition()`이 이미 `OutputYomiDialogue(PositionClosed)`로 청산 대사를 내보내므로, 채우면 **대사가 겹칠 수 있습니다.** 겹침을 피하려면 우선순위 조정이 필요합니다.

**권장**: 당장은 죽은 연산을 없애고, §21-A4(셋업 카탈로그)를 진행할 때 셋업별 반성 대사로 되살리는 쪽이 중복 없이 깔끔합니다.

> ✅ **2026-10-06 처리 (DEAD-3)** — 메서드와 `OnPositionClosed` 구독을 **통째로** 제거했습니다. 위 권장안은 "구독은 `isProcessingSignal` 관리에 필요하니 유지"라고 적었지만 재확인 결과 **이 메서드는 `isProcessingSignal`을 건드리지 않습니다** (그 관리는 `HandlePositionLiquidated`와 `HandleSignalPhaseChanged`가 합니다). 남은 호출 `GetAvailableBalance()`도 부작용 없는 조회라 동작 변화는 0입니다. 반성 대사가 필요해지면 그때 구독을 새로 겁니다.

#### 🅱 `TraderMemoryManager` — 완전한 쓰기 전용 저장소

**전수 확인 결과입니다.**

| 메서드 | 호출자 |
|---|---|
| `AddMemory()` | `AITradingBrain.ProvideChartHintToPlayer` **1곳** (차트 힌트를 가중치 6으로 기록) |
| `OnDayAdvanced()` | `GameManager` (전날 기억 압축·Pruning) |
| `ResetAll()` | `GameManager` (새 게임) |
| **`GetShortTermDialoguesText()`** | **0곳** ← 유일한 읽기 메서드 |
| **`RecordDialogue()`** | **0곳** |

**기록하고, 압축하고, 저장까지 하는데 읽는 곳이 없습니다.** 게다가 `SaveLoadManager`는 이 데이터를 **Reflection으로** 추출·복원합니다([SaveLoadManager.cs:347](../../Assets/Scripts/System/SaveLoadManager.cs#L347), 주석에 "추후 `GetData()` 메서드를 추가하는 것이 좋음").

**의도 판정: 소멸.** 이 시스템의 존재 이유는 **LLM 프롬프트에 넣을 대화 맥락**이었습니다(`GetShortTermDialoguesText`라는 이름이 그대로 말해 줍니다). LLM이 2026-09-22에 제거되면서 소비자가 사라졌습니다.

**개선안: 삭제 권장.** 읽히지 않는 데이터를 매 거래마다 기록하고, 매일 압축하고, Reflection 비용까지 들여 세이브에 쓰고 있습니다.
- `Assets/Scripts/AI/TraderMemoryManager.cs`, `MemoryEntry.cs`
- `AITradingBrain.cs:806`의 `AddMemory` 호출
- `GameManager`의 `ResetAll` / `OnDayAdvanced` 호출 2곳
- `SaveLoadManager`의 `ExtractMemoryData` / `RestoreMemoryData`와 Reflection 블록
- `SaveData`의 AI 기억 필드군(143행 부근)

**비용** 중 (세이브 경로 포함) / **효과** — 죽은 I/O와 Reflection 제거. 되살릴 계획이 있다면 **최소한 Reflection만이라도 걷어내고** `GetData()`를 노출하는 쪽이 맞습니다.

#### 🅱 `ScenarioMatcher` / `ScenarioDatabase` — 호출자도 데이터도 없음

`Assets/Scripts/DatingSim/Scenario/` — `StartNewScenario`/`IncrementTurn`에 호출자가 없고, `Assets/Data/Scenarios/`와 베이크된 `ScenarioDatabase.asset`도 **존재하지 않습니다.** 파이프라인 전체가 코드만 남은 스캐폴딩입니다.

**의도 판정: 보류.** 매칭 로직 자체는 LLM에 의존한 적이 없어 살아남았지만(CLAUDE.md), **현재 설계에 이것이 들어갈 자리가 없습니다.** 미연시 대사 시스템 재설계가 끝나기 전에는 되살릴지 판단할 수 없습니다.

**개선안**: 지금 손대지 말고 **미연시 대사 시스템 설계가 확정될 때 함께 결정**합니다. 단 `ScenarioMatcher`의 제로 할당 제약(`for` 루프만, LINQ 금지)은 **되살릴 때 반드시 지켜야 하므로** 그 주석은 보존하십시오.

#### 🅱 `TriggerGimmickDialogue`의 `gimmickContext` 인자 — 미사용 → ✅ 제거 (2026-10-06, DEAD-6)

> 처리 결과: 인자를 제거하고 `fallbackDialogue` → `dialogue`로 개명했습니다. 호출부는 DEL-1에서 3곳이 이미 빠져 **6곳**을 고쳤습니다. 아래는 처리 전 기록입니다.

[MentalDrainGimmickController.cs:69](../../Assets/Scripts/AI/MentalDrainGimmickController.cs#L69) — 첫 인자가 본문에서 전혀 쓰이지 않습니다. LLM에 넘기던 상황 설명 프롬프트였습니다.

```csharp
private void TriggerGimmickDialogue(string gimmickContext, string fallbackDialogue = "")
{
    if (visualController != null && !string.IsNullOrEmpty(fallbackDialogue))
        visualController.DisplayDialogueBalloon(fallbackDialogue, ...);   // gimmickContext 미사용
}
```

**의도 판정: 소멸.** 호출부 9곳이 매번 긴 한국어 설명 문자열을 만들어 넘기는데 그대로 버려집니다 — **폰트 아틀라스에도 불필요하게 베이크되고 있습니다**(§18의 프리베이크가 .cs 리터럴을 긁으므로).

**개선안: 인자 제거.** 시그니처에서 빼고 호출부 9곳의 첫 인자를 삭제합니다. 두 번째 인자(`fallbackDialogue`)가 실제 대사이므로 기능 변화가 없습니다. 이름도 `fallbackDialogue` → `dialogue`로 바로잡는 편이 좋습니다(폴백이 아니라 유일한 경로이므로). **비용** 작음 / **효과** 죽은 문자열 9개가 소스와 폰트 아틀라스에서 사라짐.

---

## 18. 수치를 바꿀 때 함께 고쳐야 하는 곳

| 바꾸는 것 | 동반 수정 |
|---|---|
| 청산 멘탈 곡선 상수 4개 | `verify_mental_balance.py`의 `LC/LMAX/GC/GMAX` + [Mental_Drain_Rebalance_Plan.md](Mental_Drain_Rebalance_Plan.md) |
| 연패 페널티 / 진입 비용 | 같은 스크립트의 예산 제약 검증부 (3연속 손절 후 생존 → 4연속 기믹 발동 가능) |
| `SaveData` 형태 | `SaveData` + `SaveGame()` 수집 + 로드 산포 + `SaveDataMigrator.Migrate()` |
| 요미 대사 추가 | 한국어 TMP 폰트 아틀라스가 **.cs 소스의 리터럴에서 프리베이크**됩니다 → `Tools/Prebake All Scripts Text into Font` 재실행 (안 하면 □로 렌더) |
| 요미 대사 어투 | `python yomi_dialogue_lint.py` (화법 규칙 위반 시 exit 1) |
| 구조 변경 | [Refactored_Architecture_Master.md](Refactored_Architecture_Master.md)에 append |
| 이벤트 템플릿 필드 단위 | `Tools/FX OVERDOSE/Migrate Event Templates (C3+C4)` |

---

## 19. 확인이 필요한 불일치

문서화 중 발견한, **의도와 구현이 어긋나거나 코드 주석과 실제 수치가 다른** 지점입니다. 전부 **기록만 하고 수정하지 않았습니다.** 각 항목에 개선 방법을 함께 적었습니다.

### 19.1 구현 결함 — 의도는 명확한데 동작하지 않음

**1. 날짜 간 가격 연속성이 끊깁니다** — ✅ 해소 (2026-10-06, FIX-1)

> **처리 결과**: 아래 개선안(프리웜 출발점 교체)이 아니라 **하루 넘김을 엔진의 `RollOverToNewDay()` 하나로 통일**했습니다. 작업 중 하루 전환 경로가 두 갈래였고 서로 다르게 깨져 있다는 사실이 드러났기 때문입니다.
> - **GameScene에서 정산**: `ResetEngine(어제 종가)` → 프리웜이 인자를 무시해 ~$67,842로 리셋 → 직후 자동저장이 리셋된 차트를 저장 (아래 서술대로)
> - **요미의 방에서 정산**: `InvalidateSavedChartForNewDay()`가 **레거시 `ChartHistories`만** 비우고 실제 복원에 쓰이는 `FlatChartHistories`·`CurrentChartPrice`는 남긴 채 `MarketTotalMinutes`만 0으로 → 다음 날 어제 가격·캔들이 복원되면서 시간축만 0으로 되돌아가는 어긋난 상태. 주석의 "새로 프리웜"도 사실과 달랐음
>
> 지금은 두 경로 모두 가격·캔들을 그대로 이어 갑니다. `InvalidateSavedChartForNewDay()`는 삭제했고, 세 초기화 경로(새 게임/불러오기/하루 넘김)가 따로 들고 있던 일시 상태 목록은 `ResetTransientMarketState()`로 합쳤습니다. `ResetEngine(startPrice)`가 인자를 버리던 문제도 함께 고쳤습니다(현재 호출은 새 게임의 `initialPrice` 하나뿐이라 동작은 동일). 가격 수준이 날짜를 넘어 복리로 떠도는 영향은 Wave 5(REAL-7 변동성 재조정)에서 다룹니다.

[GameManager.cs:1178](../../Assets/Scripts/GameManager.cs#L1178)은 어제 종가를 명시적으로 넘깁니다:
```csharp
marketEngine.ResetEngine(marketEngine.CurrentPrice);
```
그런데 [`PrewarmHistoricalCandles`](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L1176)는 그 인자를 쓰지 않고 직렬화 필드에서 시작합니다:
```csharp
float tempPrice = initialPrice;     // ← startPrice가 아니라 67842.1 고정
...
currentPrice = prewarmedEndPrice;   // ← 그리고 이 값이 startPrice를 덮어씀
```
결과적으로 **`startPrice` 인자가 통째로 버려지고, 매일 $67,842 근처에서 새로 시작합니다.** 어제 $75,000으로 끝났어도 오늘 시가는 어제와 무관합니다. 호출부는 분명 연속성을 의도했습니다.

**개선안**

```csharp
private float PrewarmHistoricalCandles(int minutesCount, float startPrice)
{
    float tempPrice = startPrice;   // ← initialPrice 대신
    ...
}
// ResetEngine: PrewarmHistoricalCandles(150, startPrice)
```

프리웜은 `startPrice`에서 **거꾸로 과거를 그리는 게 아니라 앞으로 150분을 굴린 뒤 그 끝값을 현재가로 삼습니다.** 그래서 한 줄만 바꾸면 하루가 바뀔 때마다 150분치 랜덤워크가 추가로 누적됩니다. 어제 종가를 **정확히** 잇고 싶다면 P2P용으로 이미 구현된 `PrepareP2PChartHistory()` 방식(마지막 종가를 권위 가격에 맞닿게 역산)을 재사용하는 편이 맞습니다 — 같은 파일 안에 있고 검증된 코드입니다.

**부작용 통제** — 가격이 날짜를 넘어 복리로 떠돌면(일간 변동성 12~60%) 20일 뒤 가격 수준이 초기값과 크게 벌어집니다. 셋 중 하나로 묶어 두십시오.
1. 일간 변동성을 실제 BTC 수준으로 낮춘다 (§20.3과 함께 처리 — 근본 해결)
2. 일차 전환 시 `ouCenterPrice`를 초기가 쪽으로 약하게 당기는 장기 앵커를 둔다
3. 가격 자체는 이어 가되 신호의 **목표 변동률을 % 기준으로만** 쓴다 (이미 그렇게 되어 있으므로 사실상 추가 작업 없음 — 가장 싼 선택)

3번이면 한 줄 수정 + 검증으로 끝납니다. 체감 변화는 "D1 차트가 의미를 갖는다"와 "어제 번 돈이 오늘 가격에 보인다" 정도입니다.

**2. 유동성 사냥이 실제로 스탑을 헌팅하지 않습니다** — ✅ 해소 (2026-10-06, FIX-2)

> **처리 결과**: 아래 개선안(`OnPriceUpdated`를 꼬리 가격으로 한 번 더 발행)은 **동작하지 않습니다.** `TradingController.CheckLiquidation`이 전달된 가격이 아니라 엔진의 `CurrentBidPrice`/`CurrentAskPrice`를 직접 읽기 때문입니다. 그래서 `PrintInstantTick()`을 신설해 꼬리 끝에서 **가격·호가·진행 캔들을 함께** 1틱 찍었다가 원래 가격으로 되돌립니다. AI 목표가/손절가는 전달된 가격으로 판정하므로 꼬리에 걸려 체결됩니다(실제 스탑 헌팅). 부수 효과로, 예전엔 1분봉에만 그려지던 꼬리가 상위 타임프레임 진행 캔들에도 반영됩니다. 서버 렉 중에는 차트가 멈춰 있어야 하므로 꼬리도 찍지 않습니다.

[`CheckLiquidationSweep()`](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L1079)은 `liveM1Candle.high/low`만 수정하고 **`currentPrice`를 움직이지 않으며 `OnPriceUpdated`도 발행하지 않습니다.** 청산 판정(`CheckLiquidation`)은 `OnPriceUpdated → HandlePriceUpdated` 경로로만 도달하므로, **꼬리가 아무리 길게 뻗어도 플레이어는 청산되지 않습니다.**

주석에는 "스탑 헌팅 기믹 강화"라고 적혀 있지만 현재는 순수 시각 효과입니다. 실제 스탑헌팅은 가격이 진짜로 거기까지 갔다가 돌아오고 그 사이에 청산이 터지는 것이 핵심입니다.

**개선안 — 꼬리 끝에서 청산 판정만 1회 태웁니다**

`currentPrice`를 실제로 옮기면 `UpdateLiveCandlesWithTick`이 다시 돌면서 캔들이 왜곡되므로, **가격은 그대로 두고 판정만** 보냅니다.

```csharp
// CheckLiquidationSweep() 끝, 꼬리를 그린 뒤
OnPriceUpdated?.Invoke(spikePrice);   // 꼬리 끝 가격으로 1회 판정
OnPriceUpdated?.Invoke(currentPrice); // 즉시 실제 가격으로 복귀
```

구독자는 `TradingController.HandlePriceUpdated` 하나뿐이고 그 안에서 `CheckLiquidation`이 호가 기준으로 판정하므로, 이 2회 호출로 "꼬리에 스쳐 청산당하고 가격은 제자리로 돌아온다"가 그대로 재현됩니다.

**반드시 함께 확인할 것**
- **진입 3초 휩소 보호**(§4.2)가 그대로 적용되는지 — 적용됩니다. `CheckLiquidation` 선두의 가드를 거치므로 진입 직후 꼬리 즉사는 여전히 막힙니다
- 이미 `GuaranteedOverride`·오버도즈·고속스킵에서는 sweep 자체가 차단되므로 **확정 구간은 영향 없음**
- ROE 대사·슬로우모션이 꼬리 1틱에 오발하지 않는지 — `HandlePriceUpdated`의 ROE 구간 대사는 12초 쿨다운이라 대체로 안전하지만, 마진콜 슬로우모션(여유 0.5% 미만)은 꼬리에 반응할 수 있습니다. 거슬리면 sweep 경로에 플래그를 하나 넘겨 연출만 건너뛰십시오

**비용** 2~3줄 + 검증 / **효과** 큼 — 난이도가 실제로 오릅니다. §21-B7(궤적 중 shakeout)의 전제 조건이기도 합니다.

**3. 일차별 변동성 배수가 실효가 없습니다** — ✅ 안 (b)로 해소 (2026-10-06, FIX-3)

`dayVolatilityMultiplier`(최대 3.35)의 사용처는 두 곳뿐입니다:
```
1086: sweepProb      += 0.10        ← 꼬리 발생 확률
1091: sweepMagnitude *= multiplier  ← 꼬리 길이 (위 2번 때문에 청산도 안 됨)
```
**`targetVol`에는 곱해지지 않습니다.** 난이도 테이블(§2.4)의 간판 수치인데 실제 변동성은 1일차와 25일차가 동일합니다.

`tickInstability`도 분산 중립입니다 — 틱 주기를 `1/I`로 줄이면 틱당 분산도 같은 비율로 줄어 분당 실현 변동성이 정확히 상쇄됩니다 (`σ√(0.2/I) × √(5I) = σ`). 차트가 더 떨려 보일 뿐입니다. 후반 난이도에서 실제로 올라가는 것은 **가짜돌파 확률·슬리피지·서버 렉** 세 가지입니다.

**개선안 — 둘 중 하나를 고르면 됩니다**

**(a) 간판대로 동작시킨다** — 세션 배수를 곱하는 바로 그 자리에 한 줄 추가:
```csharp
targetVol *= sessionVolMultiplier;
targetVol *= dayVolatilityMultiplier;   // ← 추가
```
⚠️ 단 **현재 변동성이 이미 실제 BTC의 3~8배**(§20.3)입니다. 여기에 16일차 이후 2.0~3.35배를 더 곱하면 Squeeze 국면 일간 변동성이 **200%**에 달합니다. 적용하려면 §20.3의 기준 변동성을 먼저 낮추고, 배수도 1.0~1.5 정도로 재조정해야 합니다.

**(b) 이름을 현실에 맞춘다 (권장, 비용 0)** — 필드명을 `sweepIntensityMultiplier`로 바꾸고 난이도 표(§2.4)의 열 이름도 "변동성 배수" → "꼬리 강도"로 고칩니다. **실제 동작은 그대로 두고 오해만 없앱니다.** 후반 난이도 상승은 이미 가짜돌파·슬리피지·서버 렉 세 축이 담당하고 있으므로 기능 공백이 없습니다.

`tickInstability`는 **그대로 두십시오.** 분산 중립인 것이 오히려 장점입니다 — 난이도를 바꾸지 않으면서 후반 차트를 거칠어 보이게 만드는 순수 연출 노브입니다. 다만 §2.4 표에 "체감용"이라고 명시해 두는 편이 좋습니다.

**4. H4 / D1 타임프레임이 캔들 1개짜리입니다** — ✅ 안 (c)로 해소 (2026-10-06, FIX-4). FIX-1이 차트를 날짜 너머로 이어 붙이면서 코드 변경 없이 해결됐습니다. 캔들 집계·롤오버 로직을 옮긴 5일 시뮬레이션으로 D1 하루 1개, H4 하루 4개·경계 정렬, 빈 캔들·중복 타임스탬프 없음을 확인했습니다.

프리웜이 150분인데 H4는 240분, D1은 1440분이 필요합니다. 집계 루프의 `Mathf.Abs(ts) % period == 0` 조건이 성립하지 않아 `candleHistories[tf].Count == 0` 분기로 1개만 생성된 뒤 계속 갱신됩니다. 여기에 하루마다 차트가 리셋되므로 **상위 타임프레임 버튼 2개는 사실상 장식**입니다.

**개선안 — 비용 순으로 셋**

**(a) 버튼을 숨긴다 (비용 0)** — `ChartUIController.SetupTimeframeButtons()`에서 H4·D1 버튼을 비활성화합니다. **빈 차트를 보여 주는 것보다 없는 편이 낫습니다.** 1·5·15분봉만으로도 하루 900분 스케일에는 충분합니다.

**(b) 프리웜을 늘린다** — `PrewarmHistoricalCandles(150)` → `(1440)`이면 H4 6개 + D1 1개가 채워집니다. 버퍼 상한이 타임프레임당 200개이므로 M1은 자동으로 최근 200개만 남습니다. **비용**은 시작 시 1440회 루프(수 ms) / 세이브 용량은 M1이 어차피 200개 캡이라 거의 변화 없음. 다만 D1은 여전히 1개입니다.

**(c) 19.1-1(가격 연속성)과 함께 고친다 — 근본 해결** — 날짜가 이어지면 D1 캔들이 **하루에 하나씩 자연히 쌓입니다.** 20일 플레이에 D1 20개 = 제대로 된 일봉 차트가 됩니다. H4도 하루 6개씩 누적됩니다.

**권장**: 19.1-1을 고칠 계획이면 (c) 하나로 둘 다 해결됩니다. 당분간 안 고칠 거라면 (a)로 혼란만 없애 두십시오 — (b)는 중간 비용에 중간 결과라 애매합니다.

### 19.2 주석·수치 불일치

**5. `InstantStopLoss` 동적 손절선 범위** — [TradingController.cs:265](../../Assets/Scripts/Trading/TradingController.cs#L265)의 주석은 `-30% ~ -5%`라고 적혀 있지만, 현재 `GetStopLossTightness()`의 최소값이 `0.050`이므로 실제 범위는 **−30% ~ −16.7%**입니다. `GetStopLossTightness`의 doc 주석도 `-0.015 = -1.5%`를 예로 들지만 테이블 최소는 `0.050`입니다.

> **개선안** — 어느 쪽이 기획값인지부터 정해야 합니다.
> - **주석이 맞다면**: `GetStopLossTightness()` 테이블의 LV.10 값을 `0.050` → `0.015`로 낮춥니다. 그러면 책읽기 만렙이 "−1.5% 칼손절"이 되어 **스킬 체감이 극적으로 커집니다**(현재 LV1→10의 개선폭이 9%→5%로 미미합니다). 단 §3.4의 −25% 스프링 가드와 `GuaranteedOverride`의 노이즈 억제(×0.15)가 함께 걸려 있어야 손절이 노이즈에 터지지 않습니다 — 지금 구조는 이미 그렇게 되어 있으므로 안전합니다.
> - **테이블이 맞다면**: 주석 2곳(`TradingController.cs:265`, `GetStopLossTightness` doc)의 숫자만 실제값으로 고칩니다. **비용 0.**
>
> 권장: **전자.** 스킬 레벨 10단계를 두고 효과 폭이 9%→5%인 것은 성장 보상이 약합니다. `3.33` 마법 상수도 `StopLossToROEMultiplier` 같은 이름 있는 상수로 빼면 의도가 드러납니다.

**6. 자연 체력 감소 속도** — 1일차 기준 `0.055 × 1.5 × 7.5 ≈ 초당 0.62`로, 아이템 보호 없이는 **약 162 실초(인게임 약 4시간)에 체력 0**에 도달합니다. 하루 거래 시간이 실시간 10분(900 인게임 분)이므로 **아이템 없이는 하루를 버틸 수 없습니다.**

> **개선안** — 배수 세 개가 겹쳐 있는 것이 원인입니다.
> ```csharp
> speedScale = 5.0f / secondsPerGameMinute          // 0.666 기준 ≈ 7.5배
> drain = 일차별감소량 × 1.5f × (1-가드) × speedScale × dt
> ```
> `speedScale`은 "인게임 시간 기준으로 감소 속도를 정규화한다"는 의도인데, 분모가 `secondsPerGameMinute`(0.666)이라 **실시간 기준으로는 7.5배 가속**됩니다. 여기에 하드코딩 `1.5f`가 또 곱해집니다.
>
> - **빠른 교정**: 하드코딩 `1.5f`를 제거하면 초당 0.41 → 약 244초. 여전히 부족합니다.
> - **제대로 된 교정**: 목표를 "아이템 없이 하루(900 인게임 분)에 체력 약 70% 소모"로 잡고 역산하면 1일차 감소량은 **인게임 분당 약 0.078**이 됩니다. `healthDecreasePerSecond`를 "인게임 분당"으로 재정의하고 `speedScale`·`1.5f`를 제거하는 쪽이 단위가 명확합니다.
> - **의도된 설계라면**: 주석에 "아이템 사용이 필수 전제"라고 명시하고, §5.3 헤더의 `(게임 8시간 = 100 소모 속도)` 문구를 실제값으로 고칩니다 — 현재 그 문구는 사실과 다릅니다.
>
> 어느 쪽이든 §9.3의 `HealthDrainGuard`(최대 80% 완화)와 함께 검증해야 합니다.

**7. 수동 진입에는 진입 수수료가 없습니다** — `OpenPosition`은 `margin × leverage × 0.06%`를 떼지만 `OpenPlayerPosition`은 떼지 않습니다. 청산 수수료는 양쪽 모두 부과됩니다. — ✅ 해소 (2026-10-06, FIX-7). 수동 진입에도 같은 수수료를 부과하고 세 곳의 상수를 `TradeFeeRate`로 묶었습니다. 그대로 더하면 100% 진입에서 현금이 음수가 되므로(125배면 −7.5%) 수동 경로는 **증거금 + 수수료 ≤ 잔고**가 되게 증거금을 줄입니다 — 100% 진입의 실제 증거금은 레버리지에 따라 잔고의 약 93~99.9%입니다. AI 경로(`OpenPosition`)는 손대지 않았으며, 고배율 올인(오버도즈)에서는 여전히 현금이 수수료만큼 음수가 될 수 있습니다.

> **개선안** — 단순 누락으로 보이므로 한 줄 추가로 대칭을 맞춥니다.
> ```csharp
> // OpenPlayerPosition, gameManager.ChangeBalance(-margin) 자리
> float entryFee = margin * leverage * 0.0006f;
> gameManager.ChangeBalance(-(margin + entryFee));
> ```
> **단 수수료 상수가 3곳에 흩어져 있습니다**(`OpenPosition` 진입, `ClosePosition` 청산, 여기). `private const float TradeFeeRate = 0.0006f;`로 묶어서 한 곳에서 바꿀 수 있게 하십시오 — 수수료는 밸런스 조정에서 자주 건드리는 값입니다.
>
> **영향**: 수동 플레이의 왕복 비용이 0.06% → 0.12%가 됩니다. 10배 레버리지·증거금 $1,000이면 회당 $6 추가. 단타 어뷰징 억제에도 도움이 됩니다(§4.1의 3초 쿨다운과 같은 목적).

**8. `ChoiceOptionData.CustomTargetROELimit` 주석의 기본값 설명**이 실제 코드와 다릅니다. — ✅ 해소 (2026-10-06, FIX-8). 모드별로 실제로 읽는 필드만 주석에 다시 적었고, 816개 선택지(템플릿 726 + 수작업 90) 전부 두 필드가 0이라 잘못된 전제로 기입된 자산은 없었습니다. 주석은 `StandardAuto: +300%` / `HoldToMitigateLoss: -85%`라고 적지만, 코드는 트레일링 익절(고점의 75~88.5%)과 `-88%`를 씁니다.

> **개선안** — 순수 문서 오류입니다. 주석을 실제 동작으로 교체하면 끝입니다. **비용 0.**
> ```
> CustomTargetROELimit   : 0이면 모드별 기본 동작
>                          (InstantTakeProfit: 예상수익 × 익절배율 / GreedyHold·StandardAuto: 트레일링 익절)
> CustomStopLossROELimit : 0이면 모드별 기본 동작
>                          (InstantStopLoss: -손절강도 × 333% / HoldToMitigateLoss: -88% / StandardAuto: -65%)
> ```
> 242개 템플릿 자산이 이 주석을 보고 작성되었을 수 있으므로, **고친 뒤 `CustomTargetROELimit`가 0이 아닌 자산이 몇 개인지** 한 번 세어 보는 편이 좋습니다. 잘못된 전제로 기입된 값이 있다면 §10.5 표와 대조가 필요합니다.

---

## 20. 실제 BTC 선물 시장 대비 부족한 현실성 (개선 후보)

§2.5의 현실감 기믹 4종은 차트의 **모양**을 만듭니다. 실제 무기한 선물 시장의 **경제적 메커니즘**은 대부분 빠져 있습니다. 아래는 전부 **미구현 기능 후보**입니다 — 이미 의도된 동작이 깨져 있는 항목은 §19.1에 따로 있습니다.

### 20.1 구조적으로 빠진 메커니즘

**① 펀딩비(Funding Rate) 없음 → 보유 비용이 0**

무기한 선물의 정체성입니다. 8시간마다 롱/숏 중 한쪽이 상대에게 지불하며, 추세장에서는 역방향 포지션이 가만히 있어도 갉힙니다.

현재는 진입 0.06% + 청산 0.06%만 있고 **홀딩이 완전 공짜**입니다. `GreedyHold`·`HoldToMitigateLoss` 같은 버티기 전략에 아무 비용이 없어 시간이 항상 플레이어 편입니다. 게임 밸런스 지렛대로서도 가장 쓸모가 큰 누락입니다.

**② 청산 캐스케이드(Liquidation Cascade) 없음**

실제 BTC 급락의 발생 원리 그 자체입니다. 레버리지 포지션 강제청산 → 시장가 매도 → 추가 하락 → 더 많은 청산.

게임은 레버리지가 주제인데 **플레이어가 청산당해도 시장에 아무 일이 없습니다.** 가격이 먼저 정해지고 청산은 결과일 뿐, 역방향 인과가 존재하지 않습니다.

**③ 호가창 깊이(Order Book Depth) / 시장충격 없음**

스프레드는 있으나 depth가 없습니다. 그 결과:
- 플레이어는 100% 가격 수용자 — $100을 넣든 $10,000,000을 넣든 체결가가 같습니다
- 슬리피지가 주문 규모가 아니라 **일차(day)에만** 연동됩니다 (`price × 0.0005 × slippageRange`)
- 호가 벽·스푸핑·아이스버그 같은 "읽을 거리"가 없습니다

**④ 수익률 분포가 정규분포 → 꼬리가 얇음**

Box-Muller로 `N(0,1)`을 씁니다. 실제 BTC 로그수익률은 첨도가 매우 높아 5~10σ 사건이 드물지 않습니다.

게임에서는 **변동성 자체를 키워야만 큰 움직임이 나옵니다.** "평온하던 차트가 예고 없이 2% 점프"가 구조적으로 불가능하고, 큰 움직임은 전부 신호 시스템이 사전에 예고합니다. 점프-확산 항이나 Student-t 분포 하나로 해결됩니다.

**⑤ 변동성 군집이 가짜 (GARCH가 아님)**

```csharp
currentVolatility = Mathf.Lerp(currentVolatility, targetVol, dtFraction * 5f);
```
`targetVol`은 국면이 정하는 **상수**입니다. 변동성이 정해진 레벨로 수렴할 뿐 **직전 수익률의 크기에 반응하지 않습니다.**

진짜 GARCH는 `σ²ₜ = ω + α·ε²ₜ₋₁ + β·σ²ₜ₋₁` — 큰 움직임이 다음 변동성을 키웁니다. 실제 BTC의 "한 번 터지면 며칠 간다"가 재현되지 않으며, 현재는 유동성 사냥의 `currentVolatility *= 2.0f` 일회성 펄스뿐입니다. **한 줄 수준의 변경치고 체감 차이가 가장 큽니다.**

**⑥ 거래량이 가격의 함수 → 정보량 0** — 🟡 부분 해소 (2026-10-06, SIG-B5: 확정 신호 구간에 한해 거래량이 진위를 드러냄. 평시 거래량은 여전히 가격 변화에서 유도)

```csharp
tickVolume = Mathf.Abs(priceDelta) * UnityEngine.Random.Range(2f, 10f);
```
거래량이 가격 변화에서 **유도**됩니다. 실제로는 거래량이 선행·독립 신호이고, 거래량 없는 랠리·거래량 급증 후 전환·돌파의 진위 판별이 전부 거래량으로 이뤄집니다.

현재 구조에서 플레이어가 거래량 막대를 보는 것은 **가격 변화량을 다시 보는 것과 동일**해 판단 재료가 되지 않습니다. (프리웜 캔들만은 예외적으로 캔들 형태 기반 가중치를 씁니다 — §2.7)

### 20.2 단순화된 부분 (게임상 납득 가능)

| 항목 | 실제 시장 | 게임 |
|---|---|---|
| 레버리지 티어 | 포지션이 커지면 최대 배율 강제 하락 | 없음 — 규모 무관 125배 |
| 마크 가격 | 인덱스 기반 마크 가격으로 청산 판정 (단일 거래소 조작 방어) | 로컬 호가 기준 |
| 청산 결과 | 보험기금·ADL 개입, 잔여 증거금 일부 반환 또는 손실 초과 | 증거금 전액 몰수 고정 |
| 주문 종류 | 지정가, 예약 손절, OCO | **시장가뿐** — 플레이어는 TP/SL 예약 불가 (AI만 `targetPrice`/`stopLossPrice` 보유) |
| 포지션 관리 | 분할 진입·분할 익절 | 전부 아니면 전무, 물타기는 AI 기믹으로만 |
| 주말 | 24/7이나 주말은 저유동성 + 얇은 호가 | 요일 개념 미사용 (`GameCalendar`에 날짜는 존재) |
| 틱 사이즈 | 이산 호가 + bid-ask bounce | 연속 실수 |

**세션 차이 과장**: 아시아 ×0.5 → 뉴욕 ×2.0으로 **4배**입니다. 실제 BTC의 세션 간 변동성 차이는 대략 1.3~1.8배 수준입니다.

**결정론적 사인파**: `drift`에 350초 / 130초 / 15초 주기의 사인 3개가 **고정 위상**으로 더해집니다([MarketSimulationEngine.cs:614](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L614)). 실제 시장에 없는 패턴이며, 숙련 플레이어가 15초 주기를 눈으로 익히면 그대로 읽힙니다. 주기·위상을 일차 시드로 랜덤화하면 비용 없이 해소됩니다.

### 20.3 변동성 절대 수준

틱당 σ와 틱 수를 합성하면 **`currentVolatility`가 곧 분당 변동성**입니다 (`σ√0.2 × √5 = σ`).

| 국면 | 분당 σ | 일간 환산 (900분, 세션 보정 ≈1.7배 포함) |
|---|---|---|
| Sideways | 0.25% | 약 **12%** |
| Bull / Bear | 0.35% / 0.45% | 약 18% / 23% |
| Squeeze | 1.2% | 약 **60%** |

실제 BTC 일간 변동성은 대략 2~4%(분당 0.08~0.15%)이므로 **게임은 3~8배 과장**되어 있습니다. 다만 하루가 실시간 10분이고 레버리지 드라마가 주제이므로 **의도된 템포 선택으로 봅니다** — 현실성 결함으로 분류하지 않았습니다.

### 20.4 현실과 잘 맞는 부분 (유지)

- 수수료 0.06% — 바이낸스 taker 0.04~0.05%와 유사한 수준
- 유지증거금률 0.5%, 청산가 공식
- 변동성 비례 스프레드 + 소프트캡
- Bear(0.0045) > Bull(0.0035) 변동성 — 레버리지 효과의 국면 단위 근사
- 호가 기반 체결 — 롱은 Ask 진입 / Bid 청산, 숏은 그 반대
- 24/7 시장이라 갭이 없는 것 (BTC의 실제 특성과 일치)

### 20.5 우선순위 (게임성 기여 ÷ 구현 비용)

| 순위 | 항목 | 분류 | 비용 | 효과 |
|---|---|---|---|---|
| 1 | 날짜 간 가격 연속성 | §19.1-1 **결함** | 1줄 + 밸런스 검토 | 날짜가 이어지는 하나의 시장이 됨 |
| 2 | 꼬리에 청산 연동 | §19.1-2 **결함** | 작음 | 주석이 약속한 기믹이 실제로 작동 |
| 3 | 진짜 GARCH (`targetVol += α·직전수익률²`) | §20.1-⑤ | 1~2줄 | 변동성 군집 체감이 가장 큼 |
| 4 | 펀딩비 | §20.1-① | 중간 | 홀딩 비용 도입 → 버티기 전략 밸런스 지렛대 |
| 5 | 거래량 독립화 | §20.1-⑥ | 중간 | 거래량이 판단 재료가 됨 |
| 6 | 점프 항 (낮은 확률 ±1~3% 스파이크) | §20.1-④ | 작음 | 신호 없이도 사건이 발생 |
| 7 | 사인파 위상 랜덤화 | §20.2 | 작음 | 패턴 암기 방지 |
| 8 | 청산 캐스케이드 | §20.1-② | 큼 | 레버리지 주제와 가장 어울리나 비용도 큼 |
| — | 호가창 깊이 / 마크 가격 / 레버리지 티어 | §20.1-③, §20.2 | 큼 | 교육용 시뮬이 목표가 아니면 생략 권장 |

---

## 21. 신호 시스템 다양성 개선 후보 (§3.2 / §3.3)

> **§20과의 구분** — §20은 *실제 BTC 시장과의 격차*(현실성)를 다루고, 이 장은 *신호 생성·연출의 다양성과 플레이 체감*을 다룹니다. 두 장에 함께 걸리는 항목은 중복 기재하지 않고 서로 참조만 합니다.
>
> **용어** — 이 장의 "AI"는 전부 **`AITradingBrain`, 즉 요미의 규칙 기반 자동매매**를 가리킵니다. 이 프로젝트에 LLM은 존재하지 않으며(§17), `AITradingBrain`에는 `async`/`await`/네트워크 호출이 하나도 없는 완전 동기 `switch` + 난수 임계값 로직입니다.

### 21.0 선행 작업 — 신호 종류 추가를 싸게 만들기

`MarketSignalType`은 **30곳**에서 참조됩니다 (`AITradingBrain` 14행 = switch 식 4개 / `MarketSimulationEngine` 16행). 그런데 소비 패턴을 보면 전부 둘 중 하나입니다:

```csharp
// AITradingBrain의 switch 4개 — 전부 "방향"만 뽑아냄
MarketSignalType.BullishBreakout => PositionType.Long,
MarketSignalType.BullTrap        => PositionType.Long,
...
// MarketSimulationEngine 생성·주입부 — targetDelta 부호 결정
```

**AI의 의사결정에 실제로 쓰이는 필드는 `Strength`와 `IsTrueSignal` 둘뿐이고, `Type`은 방향 유도에만 쓰입니다.** 그래서 지금은 신호 종류를 하나 추가할 때마다 `AITradingBrain`의 switch 4개를 함께 고쳐야 하며, 이것이 다양성 확장을 막는 실질적 병목입니다.

```csharp
// MarketSignal에 필드 하나 추가
public TradingController.PositionType Direction;   // 이 신호가 유도하는 방향
```

switch 4개를 `signal.Direction` 한 줄로 치환하면 **이후 신호 종류를 몇 개 추가하든 AI 코드를 건드릴 필요가 없습니다.** 비용 30분이며 A4·B1의 전제 조건입니다.

> ⚠️ `ForceInjectSignal`(이벤트 빔)과 `TriggerOverdoseTrapSignal`(오버도즈 함정)도 `MarketSignal`을 직접 생성하므로, 필드를 추가하면 이 두 곳에도 기본값을 채워야 합니다.

---

### 21.1 신호 생성 개선안 (§3.2)

#### A1. 일일 기조와 신호를 연동 ⭐ 최우선 — ✅ 완료 (2026-10-06)

> 처리 결과: 아래 확률표와 ±0.20 진위 보정을 그대로 적용하고, Squeeze 기조는 Strong 80% + 변동 크기 ×1.25로 구체화했습니다. 신호가 기조 방향으로 가격을 움직일 확률이 50% → 초반 70% / 후반 62.5%가 됩니다(§3.2).

**문제** — `GenerateMarketSignal()`이 `currentRegime`도 `currentDailyRegime`도 **읽지 않습니다.** Bear 기조인 날에도 상승 돌파가 35%로 동일하게 나옵니다. 일일 기조는 drift에만 영향을 주는데 그 값이 분당 0.015%로, 노이즈 0.25% 대비 SNR이 0.06입니다 — **플레이어가 매매하는 분~10분 단위에서는 사실상 체감되지 않습니다.** 결과적으로 세이브 스컴 방지까지 걸어 둔 요미의 일일 방향 힌트(§2.3)가 "들어도 그만"인 정보가 됩니다.

**개선** — §2.2 국면 전이표와 같은 패턴으로 신호 확률표를 기조에 종속시킵니다.

| 일일 기조 | 순방향 돌파 | 역방향 돌파 | 순방향 트랩 | 역방향 트랩 | 성격 |
|---|---|---|---|---|---|
| Bull | **45%** | 15% | 15% | **25%** | 추세장 — 숏 꼬시고 급등(BearTrap)이 전형 |
| Bear | 15% | **45%** | **25%** | 15% | 대칭 |
| Sideways | 20% | 20% | **30%** | **30%** | 박스권 — 양 끝단 가짜 돌파가 지배적 |
| Squeeze | 30% | 30% | 20% | 20% | Strong 비율·변동 크기 상향 |

진위 확률에도 정합성 보정:
```csharp
trueSignalProb = 0.60f - fakeoutProbability * 0.5f
               + (기조 순방향 ? +0.20f : 기조 역방향 ? -0.20f : 0f);
```

**비용** 20줄 / **효과** 매우 큼 — 요미의 힌트에 실질 가치가 생기고, "오늘은 하락 기조니 반등 신호는 의심한다"는 **플레이어의 판단 근거**가 만들어집니다.

#### A2. 직전 신호 결과를 기억 (마르코프 전이)

**문제** — 모든 신호가 i.i.d. 독립 추첨이라 사건에 기억이 없습니다. 실제 시장은 실패한 돌파 뒤에 반대 방향 움직임이 잘 나오고, 같은 방향 연속 성공 뒤에는 과열 반전이 옵니다.

**개선**
- 직전 신호가 **가짜였으면** 다음 신호의 **반대 방향 + 진짜** 확률 ↑ (휩소 뒤 진짜 움직임)
- 같은 방향 **2연속 성공** → 3번째는 **반전 트랩** 확률 ↑
- 직전이 Weak였으면 다음 Strong 확률 ↑ (에너지 축적)

**비용** 필드 2~3개 + 분기 / **효과** 중 — 연속 플레이 시 "흐름"이 생깁니다.

#### A3. 차트 모양을 읽어서 신호를 발생 ⭐

**문제** — 신호가 **현재 차트와 완전히 무관하게** 발생합니다. 실제 돌파는 저항선·박스 상단·직전 고점에서 일어납니다. 캔들 히스토리를 타임프레임당 200개 보유하고 있는데 신호 생성이 한 번도 참조하지 않으며, 라운드 피겨 오더블록(§2.5-②)과도 연결되어 있지 않습니다.

**개선** — 인게임 분당 1회 호출이라 200개 순회는 부담이 없습니다.

| 차트 조건 | 가중 |
|---|---|
| 최근 고점/저점 ±0.3% 이내 | Breakout 계열 ↑ |
| 라운드 피겨 근접 | Trap 계열 ↑ (오더블록 반발과 시너지) |
| 최근 N봉 고−저 폭이 좁음 | Squeeze 돌파 셋업 |
| 직전 고점 돌파를 한 번 실패 | 더블탑 → 하락 셋업 |

**비용** 중 / **효과** 매우 큼 — **차트 공부 스킬에 "차트를 본다"는 의미가 처음 생깁니다.** 현재 차트 공부는 정확도 난수 보정일 뿐이고, 플레이어가 차트를 읽어서 얻을 수 있는 정보가 사실상 없습니다.

#### A4. 셋업 카탈로그 확장 (4종 → 10종)

현재는 `Breakout × 2방향 + Trap × 2방향`이 전부입니다. 각 셋업을 **(GraceWindow 사전 연출 / 본 궤적 / 성패 시 거동)** 세 쌍으로 정의하면 B1과 그대로 맞물립니다.

| 셋업 | 사전 연출 (Grace) | 본 궤적 | 비고 |
|---|---|---|---|
| `Breakout` (기존) | 변동성 수축 | 일방향 | — |
| `FalseBreakout` (기존 Trap) | 변동성 수축 | 돌파 후 급반전 | — |
| `TrendContinuation` | 얕은 되돌림 | 추세 재개 | 추세장 전용, 성공률 높음 |
| `RangeRejection` | 박스 끝단 접근 | 반대편으로 왕복 | 횡보장 전용 |
| `StopRun` | 조용함 | 급격한 스윕 후 반대 방향 | §2.5-④ 유동성 사냥을 신호로 승격 |
| `Capitulation` | 가속 하락 | 투매 후 V 반등 | 공포 연출 |
| `Distribution` | 고점 횡보 | 느린 붕괴 | 현 Trap 타입 2와 유사 |
| `NewsSpike` | **없음 (예고 없이)** | 즉시 점프 후 절반 되돌림 | §20.1-④ 점프 항의 게임화 |
| `VolatilitySqueeze` | 극단적 수축 | 방향 미정 → 폭발 | 양방향 베팅 유도 |
| `LiquidityGrab` | 한쪽으로 유인 | 양쪽 스탑 차례로 사냥 | 현 W-shape의 독립 셋업화 |

**비용** 큼 (21.0 선행 작업 필수) / **효과** 큼 — 다만 AI의 Deception Tier(§8.1)가 `(Strength, IsTrueSignal)`만 보므로, **그 두 필드를 "AI용 요약"으로 남기고 셋업은 연출·컨텍스트 레이어로만 추가**하면 AI 로직 변경 없이 끝납니다. 이것이 가장 싼 설계입니다.

#### A5. 목표 변동률을 로그정규 분포로

**문제** — `Random.Range(3.0f, 6.0f)` 균등분포라 **항상 중간쯤**입니다. 실제 시장의 움직임 크기는 작은 것이 압도적으로 많고 큰 것이 드뭅니다.

**개선** — `mag = 3.0f * Mathf.Exp(gauss * 0.35f)` 또는 간단히 `3.0f + 3.0f * u * u` (u = `Random.value`). 평범한 2~3%가 대부분이고 가끔 8~10%가 나옵니다.

**비용** 2줄 / **효과** 중 — "가끔 터지는 큰 것"이 생겨 기대감이 유지됩니다.

#### A6. 트랩에 불확실성 부여

**문제** — Trap은 `IsTrueSignal = false`가 **100% 결정적**입니다. 요미가 간파에 성공하면(Tier 1의 `counterTrapProb`) 역진입이 무조건 정답입니다.

**개선** — BullTrap의 15%는 실제로 상승하게 합니다 ("트랩인 줄 알았는데 진짜였던" 경우).

**비용** 1줄 / **효과** 중, 단 **밸런스 리스크 있음** — Tier 1 카운터 진입의 승률이 100% → 85%로 떨어집니다. 요미 간파의 가치가 줄어드는 만큼 다른 보상이 필요할 수 있습니다.

#### A7. 신호 간격을 포아송 + 세션 가중으로

**문제** — `Random.Range(8, 16)` 균등분포라 **항상 비슷한 간격**입니다. 실제 시장은 사건이 몰릴 때 몰리고 뜸할 때 뜸합니다. 세션 시스템(§2.5-③)이 이미 있는데 신호 빈도와 연결되어 있지 않습니다.

**개선** — 지수분포(`-λ·ln(1-u)`)로 바꾸고 λ를 세션별로 조정. 아시아장은 뜸하게, 뉴욕장은 몰아서.

**비용** 5줄 / **효과** 중 — 뉴욕장(16시 이후)이 "바쁜 시간"으로 체감됩니다. 변동성만 2배 올려 둔 현재보다 세션 구분이 또렷해집니다.

#### A8. 멀티 레이어 (큰 신호 속 작은 신호) — 비권장

"일봉 추세 + 5분봉 눌림목" 같은 중첩 구조입니다. `activeSignal`이 단일 struct라 배열화가 필요하고 UI·AI·세이브가 전부 영향을 받습니다. **비용 대비 효과가 나쁩니다** — A1 + B3로 비슷한 체감을 훨씬 싸게 얻을 수 있습니다.

---

### 21.2 연출 패턴 개선안 (§3.3)

#### B1. 궤적을 `AnimationCurve` 에셋으로 외부화 ⭐

**문제** — 패턴이 **하드코딩 6조합**(Wave 2종 × Trap 3종)이고, 그나마 Trap 3종은 외부 이벤트 전용입니다. **일반 AI 신호는 Wave 0/1 두 개뿐**이며, 진짜 신호면 노이즈 ×0.15라 사실상 직선 하나입니다.

**개선** — 구간 if/else를 진행률 커브로 치환합니다.
```csharp
[CreateAssetMenu] class TrajectoryProfile : ScriptableObject {
    public AnimationCurve progress;   // x: 경과비율 0~1 → y: 목표변동률의 몇 배까지 도달
    public AnimationCurve noiseScale; // 구간별 노이즈 배수
    public float jitter;              // 구간 분할 지터
}
// 매 틱: targetPrice = start × (1 + delta × progress.Evaluate(elapsedRatio))
```
- V-shape = `0 → 1.35 → 0.9`
- W-shape = `0 → 1.5 → 0.7 → 1.3 → 1.0`
- Slow bleed = ease-in / 기존 선형 = 직선

**비용** 중 / **효과** 매우 큼 — **패턴 추가가 코드 작업에서 에셋 작업으로 바뀝니다.** 기획자가 커브를 눈으로 보며 조정할 수 있고, 20개든 50개든 가중 추첨으로 돌릴 수 있습니다. "다채로움"에 가장 직접적인 답입니다.

#### B2. 구간 분할 지점에 지터 ⭐ 거의 공짜 — ✅ 완료 (2026-10-06)

**문제** — Trap 0은 **항상** 0.7에서 꺾이고 Trap 1은 **항상** 0.4 / 0.6 / 0.85에서 꺾입니다. 같은 패턴을 두 번 본 플레이어는 다음 꺾임을 압니다.

**개선** — 신호 생성 시 1회 추첨해 `MarketSignal`에 저장. `0.7f` → `Random.Range(0.6f, 0.8f)`.

**비용** 거의 0 / **효과** 중 — 같은 패턴이 매번 다르게 보입니다. **지금 당장 넣을 수 있는 가장 싼 개선입니다.**

#### B3. OU를 끄지 말고 중심선을 이동 ⭐⭐ 최고 가성비 — ✅ 완료 (2026-10-06)

> 처리 결과: 정상 경로에만 적용하고 트랩 3종은 OU를 계속 끕니다(직선 중심선이 V/W 패턴을 뭉갬). 국면별 `ouTheta` 대신 전용 상수 `SignalPathOuTheta = 0.1`을 쓰고, 전역 `ouCenterPrice` 대신 지역 경로 중심을 계산합니다. 틱 동역학 시뮬레이션(4,000회)에서 종료 시 목표 대비 편차 표준편차가 1.79% → 0.89%(이론 0.84%)로 줄었습니다.

**문제** — `ouTerm = 0f`로 평균 회귀를 **완전히 꺼버립니다**(일방향 궤적 보장 목적). 되돌림 압력이 전혀 없어 추세가 "밀려가는" 게 아니라 "끌려가는" 느낌이 나고, 눌림목은 사인파를 수동으로 더해 흉내내고 있습니다.

**개선** — 중심선 자체를 목표가로 이동시키고 OU는 살려 둡니다.
```csharp
// ouTerm = 0f;  ← 삭제
ouCenterPrice = Mathf.Lerp(activeSignal.SignalStartPrice, 목표가, elapsedRatio);
```
가격이 중심선보다 앞서가면 당겨지고 뒤처지면 밀려서 **눌림목과 되돌림이 저절로 생성됩니다.** 사인파 수동 주입(B4)도 상당 부분 불필요해집니다.

**비용** 2~3줄 / **효과** 매우 큼 — 추세의 질감이 가장 크게 달라지는 지점이면서 수정 범위는 가장 작습니다.

> ⚠️ 오버도즈 함정의 `ouTerm = 0f`는 **그대로 두어야 합니다.** 그쪽은 확정 청산 유도가 목적이라 되돌림이 방해가 됩니다.

#### B4. 사인파 주기·위상 랜덤화

**문제** — `Mathf.Sin(Time.time * 2.5f)`, `Mathf.Cos(Time.time * 5.0f)` 등 **주파수가 상수**입니다. §2.1의 기본 파동(350초/130초/15초)도 마찬가지입니다. 숙련 플레이어가 15초 주기를 눈으로 익히면 그대로 읽힙니다.

**개선** — 주기·위상을 신호 생성 시 또는 일차 시드로 추첨합니다.

**비용** 작음 / **효과** 중 — §20.2에도 같은 항목이 있습니다. B3를 넣으면 사인파 자체를 줄일 수 있어 함께 처리하는 쪽이 좋습니다.

#### B5. 거래량을 진위 단서로 ⭐⭐ 가성비 최상 — ✅ 완료 (2026-10-06)

> 처리 결과: 아래 코드 스케치(`|가격변화| × 배수`)는 **진짜 신호에서 역효과**였습니다. 진짜 신호 구간은 노이즈를 ×0.15로 억제해 틱당 가격 변화가 평시보다 작으므로, 배수를 곱해도 평시 수준에 머뭅니다. 그래서 기준을 **그 국면의 평시 틱 변동폭**(`가격 × targetVol × √dt`)으로 바꿨습니다 — 진짜는 `max(|Δ|, 평시폭) × 2.5`, 가짜는 `평시폭 × 0.7`. 평시 틱의 평균 |Δ|가 평시폭의 약 0.8배라, 신호 구간 거래량은 **진짜 ≈ 평시의 3.1배 / 가짜 ≈ 0.9배**가 됩니다(가짜는 가격이 크게 움직이는데 막대가 평평). 오버도즈 함정은 제외.

**문제** — 실제 트레이딩에서 돌파의 진위를 가리는 **1순위 단서가 거래량**입니다. 진짜 돌파는 거래량이 폭증하고 가짜는 거래량 없이 올라갑니다. 게임에서는 `volume = |priceDelta| × rand`라 거래량이 가격의 함수이고 **플레이어에게 정보량이 0**입니다(§20.1-⑥).

**개선** — 전면 재설계 없이 **한 줄로 80%를 얻습니다.**
```csharp
float signalVolMult = currentSignalPhase == SignalPhase.GuaranteedOverride
    ? (activeSignal.IsTrueSignal ? 2.5f : 0.7f) : 1.0f;
tickVolume = Mathf.Abs(priceDelta) * Random.Range(2f, 10f) * signalVolMult;
```

**비용** 1~2줄 / **효과** 매우 큼
- 거래량 막대가 처음으로 **읽을 가치가 있는 정보**가 됩니다
- A3와 함께 **차트 공부 스킬의 서사적 근거**가 생깁니다 ("거래량 없는 돌파는 가짜다")
- 요미의 간파 대사에 **실제 데이터 근거**가 생깁니다 — 현재는 "호가창 거래량이 붙고 있어" 같은 대사만 있고 뒷받침하는 데이터가 없습니다
- 적용하면 §20.1-⑥의 우선순위를 낮출 수 있습니다

#### B6. GraceWindow를 "변동성 수축"으로

**문제** — 현재 Grace는 `노이즈 ×0.40 + drift 0` = 그냥 조용합니다. 단조롭고, 실제 돌파 직전의 모습과도 다릅니다.

**개선** — 실제 돌파 직전은 **변동성 수축(squeeze)**입니다. 캔들 몸통이 점점 작아지고 거래량이 마르다가 터집니다.
```
Grace 동안 노이즈 배수를 0.6 → 0.15로 점진 감소 (조이는 느낌)
거래량도 함께 감소
마지막 10% 구간에 가벼운 페이크 방향 틱 1~2회
```

**비용** 작음 / **효과** 중 — "폭풍 전 고요"가 생기고, 플레이어가 **차트만 보고 돌파 임박을 체감**할 수 있게 됩니다. 요미의 골든타임 예고 대사와도 연출이 맞아떨어집니다.

#### B7. 궤적 중간에 shakeout(개미털기) 주입

**문제** — 유동성 사냥이 `GuaranteedOverride` 구간에서 **차단**되어 있어 확정 추세 구간이 되돌림 없이 매끈하게 흘러갑니다. 실제 추세는 중간에 반드시 한두 번 털어냅니다.

**개선** — 궤적 중 1~2회 급격한 역방향 꼬리 후 복귀를 주입합니다. W-shape의 "2차 급락(개미털기)"을 모든 패턴에 확률적으로 적용하는 셈입니다.

**비용** 작음 / **효과** 중 — 단 **§19.1-2(꼬리가 청산을 유발하지 않음)를 먼저 정리해야** 의도대로 작동합니다. 그것을 고친 뒤 넣으면 난이도가 꽤 오릅니다.

#### B8. 진짜 신호의 노이즈 억제 완화 — 밸런스 리스크 있음

**문제** — `stochasticNoise *= 0.15f` 때문에 진짜 신호 구간이 부자연스럽게 매끈합니다. 더 큰 문제는 **메타 정보 유출**입니다 — 플레이어가 차트의 매끄러움만 보고 "이건 진짜다"를 역추론할 수 있습니다. 신호의 진위를 숨기는 것이 게임의 핵심인데 렌더링이 답을 흘립니다.

**개선** — 노이즈를 `×0.6` 정도로 유지하고, 좁은 손절선 보호는 이미 존재하는 **−25% 스프링 꼬리 가드**(§3.4)에 맡깁니다. 그 가드가 정확히 이 목적으로 만들어져 있습니다.

**비용** 1줄 / **효과** 큼, 단 **검증 필요** — 손절선이 더 자주 터집니다. 책읽기 저레벨(손절 −9%)은 괜찮지만 고레벨(−5%)은 영향을 받습니다. B3를 먼저 넣으면 되돌림이 자연스러워져 이 변경의 충격이 줄어듭니다.

---

### 21.3 추천 조합

| Tier | 항목 | 총 비용 | 얻는 것 |
|---|---|---|---|
| **0 선행** | §21.0 `Direction` 필드 분리 | 30분 | 이후 신호 종류 추가 비용 O(1) |
| **1 즉시** | **B3** OU 중심선 이동 · **B5** 거래량 배수 · **B2** 구간 지터 · **A1** 기조 연동 확률 | 반나절 | 추세 질감 + 거래량 정보화 + 요미 힌트 가치 + 패턴 반복 해소 |
| **2 다음** | **A3** 차트 컨텍스트 · **B1** 궤적 커브 에셋화 · **B6** Grace 스퀴즈 · **A5** 로그정규 · **A7** 포아송 간격 | 2~3일 | 차트 공부 스킬의 의미 + 패턴 무한 확장 |
| **3 기획 결정 후** | **A4** 셋업 10종 · **A6** 트랩 불확실성 · **B8** 노이즈 완화 · **B7** shakeout | — | 전부 난이도·밸런스를 바꿈 |
| **비권장** | **A8** 멀티 레이어 | 큼 | A1 + B3로 비슷한 체감을 훨씬 싸게 |

Tier 1 네 개만으로 체감이 가장 크게 바뀝니다. 특히 **B3와 B5는 합쳐서 5줄 남짓인데 이 목록에서 효과가 가장 큽니다.**

**연쇄 효과 주의**
- **B7**은 §19.1-2(유동성 사냥이 청산 미유발)를 먼저 정리해야 의도대로 작동합니다
- **B5**를 넣으면 §20.1-⑥(거래량 정보량 0)이 부분 해소되므로 해당 항목의 우선순위를 낮출 수 있습니다
- **B8**은 §3.4의 −25% 스프링 가드에 의존하므로, 그 가드를 손대면 함께 재검증해야 합니다
- **A1**을 넣으면 §2.3 `DailyMarketOutlook`의 세이브 스컴 방지 장치가 비로소 제값을 합니다

---

## 22. 기믹 4 (고배율 중독 금단현상) 삭제 — ✅ 완료

**2026-10-06 완료** (백로그 DEL-1). 계획서 본문은 작업 완료와 함께 걷어냈습니다 — 상세 diff는 git 기록, 변경 요약은 [Refactored_Architecture_Master.md](Refactored_Architecture_Master.md)에 있습니다.

**결과 요약**
- 제거: 기믹 본체(`MentalDrainGimmickController`), `AITradingBrain.ForceNextTradeHighLeverage`와 강제 고배율 블록, `ItemUser`의 중독 치료 경로, `TraderStatus` 중독 3필드·프로퍼티·`CureLeverageAddiction()`, `SaveData` 3필드, 호출자가 0이 된 `TradingController.LockManualMode()`/`UnlockManualMode()`
- 유지: `LockManualModeTemporarily`·`IsManualModeLockedByYomi`·`manualLockGeneration`(오버도즈·지연 진입이 사용), `FindPlayerBrain()`과 `aiBrain` 구독(FOMO가 사용), `CurrentLosingStreak`(기믹 2)
- 재작성: `ClosePosition()`의 락 보존 주석 — 근거를 "임시 락 대기 구간 중 청산 호출"로 교체 (§4.3-9)
- 검증 자산: `SaveRoundTripTester`의 SV-A1 항목을 `CurrentLosingStreak`(SV-A3) 왕복 검사로 교체
- 세이브: 구버전 세이브의 중독 필드는 `JsonUtility`가 무시 — 마이그레이션 불필요
- 남은 영향(재설계 대상): 50배 이상 수동 고배율 플레이에 대한 억제 장치가 없어졌습니다. 진정제는 멘탈 만땅일 때 사용 실패로 바뀌었습니다.

---

## 23. 정기 지출 전면 삭제 — ✅ 완료

**2026-10-06 완료** (백로그 DEL-2). 계획서 본문은 작업 완료와 함께 걷어냈습니다 — 상세 diff는 git 기록, 변경 요약은 [Refactored_Architecture_Master.md](Refactored_Architecture_Master.md)에 있습니다.

**결과 요약**
- 제거: `CalculateExpectedDeduction()`, `TodayRegularDeduction`/`TodayRegularDeductionReason`과 그 세이브·리셋 경로, 정산 시 차감 블록, 요미 지출 예고 기믹(`AnnounceTodayExpenses()`·`expenseAnnouncedForDay`·호출부), 정산 UI의 `SYSTEM EXPENSE CHARGED` 분기, `SaveData` 2필드
- 정리: 24:00 파산 예정 판정에서 정기 지출 항을 빼고 위약금 경로만 유지 (`StoryPenaltiesEnabled`로 되살릴 수 있도록)
- 주석 삭제: `StoryDayLimitEnabled`·`StoryPenaltiesEnabled` doc의 정기 지출 문단, `DisableStoryPenaltiesIfNeeded` 로그 괄호, `AdvanceDate`의 "정기 지출 정책 미정" 문장, `RoomSettlementUIBootstrap`의 근거 주석 (**`GameOverUIController` 설치 코드는 유지** — 방에서도 데이트 비용 등으로 파산 가능)
- 유지: 스토리 위약금 경로, 상점·스킬 인플레이션, `isSettlementProcessing`
- 세이브: 구버전 세이브의 2필드는 `JsonUtility`가 무시 — 마이그레이션 불필요. 멘탈 예산 무관
- 남은 영향(재설계 대상): 스토리 3·7·11·15·18일차 압박 소멸, **엔드리스 21일차 이후 자산 압박 공백** (대체 후보: 펀딩비 §20.1-① / 백로그 REAL-1)
