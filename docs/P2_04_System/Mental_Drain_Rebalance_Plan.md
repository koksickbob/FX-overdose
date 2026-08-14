# 멘탈 감소 시스템 리밸런싱 계획

작성일: 2026-08-14 / 대상: 트레이딩 파트(`GameScene`) 멘탈 시스템
상태: **코드 적용 완료 (2026-08-14)** — 0단계 에셋 마이그레이션만 미실행

## 진행 상태

| 항목 | 상태 |
|---|---|
| 0. 이벤트 에셋 마이그레이션 | ⏳ **미실행** — Unity 에디터에서 메뉴 실행 필요 (아래 참조) |
| 1. 체력 연동 감소에 아이템 경감 | ✅ 완료 (안 A: 체력 연동으로 일원화) |
| 2. FOMO 임계치 2% → 5% | ✅ 완료 |
| 3. 손실 청산 비율 전환 | ✅ 완료 (**B안 제곱근 채택**, 익절 대칭 포함) |
| 4. 이벤트 페널티 범위 | ✅ 코드/생성기/마이그레이터 완료, 에셋 반영은 0단계 대기 |
| 5. 트라우마 천장 제거 | ✅ 완료 (`trauma_cured` 업적 제거 포함) |
| 6. 지뢰계 배율 오류 | ✅ 완료 |
| 7. `canRegenMental` 제거 | ✅ 완료 |
| 8. AITradingBrain | ✅ 사장 메서드 2개 제거 (클래스는 존치 — 아래 참조) |
| 합산 예산 수치 하향 | ✅ 완료 (진입 -5, 연속 손절 -4/-9/-16) |

**남은 작업 — Unity 에디터에서 1회 실행:**
`Tools/FX OVERDOSE/Migrate Event Templates (C3+C4)`
242개 템플릿의 음수 `MentalChangeAmount`를 `MentalPenaltyOnFail`로 이관하고 -30~-5 범위로 압축합니다.
되돌리려면 `git checkout Assets/Resources/Events/` (실행 시점에 해당 경로는 clean이었습니다).
실행 전까지는 코드 상한(-35)이 방어하므로 게임이 깨지지는 않지만, **베팅 성공 시 멘탈이 깎이는 반전 현상은 그대로 남습니다.**

검증 스크립트: `python verify_mental_balance.py` (리포지토리 루트)

---


멘탈 감소 로직 전수 조사 결과를 바탕으로 한 리밸런싱 계획서입니다. 감소 경로 진입점은
[TraderStatus.ChangeMental()](../../Assets/Scripts/TraderStatus.cs#L462) 하나이며, 모든 감소가 여기로 수렴합니다.

---

## 0. 선결 과제 — 라이브 이벤트 데이터가 마이그레이션되지 않았습니다 (최우선)

리밸런싱보다 먼저 처리해야 하는 사안입니다. **현재 배포 데이터는 코드가 이미 고쳤다고 주석에 적어둔 버그를 그대로 안고 있습니다.**

`Assets/Resources/Events/Templates/*.asset` 242개(선택지 726개)를 전수 조사한 결과:

| 항목 | 실측 |
|---|---|
| `MentalPenaltyOnFail`이 0이 아닌 선택지 | **0개 / 726개** |
| `MentalChangeAmount`가 음수인 선택지 | **244개** |
| 음수 `MentalChangeAmount` 최솟값 | **-120** |

[ChoiceEventController.ApplyVitals()](../../Assets/Scripts/Events/ChoiceEventController.cs#L904)는
`베팅 성공 → MentalChangeAmount / 베팅 실패 → MentalPenaltyOnFail`로 분기합니다.
따라서 지금 게임에서 실제로 벌어지는 일은 다음과 같습니다.

- **베팅에 성공하면 멘탈 -120 → 즉시 오버도즈**
- **베팅에 실패하면 멘탈 0 → 무손실**

이는 [ChoiceEventController.cs:900-902](../../Assets/Scripts/Events/ChoiceEventController.cs#L900-L902) 주석이
"과거에는 그랬으나 이제 고쳤다"고 서술한 바로 그 상태입니다. 코드는 고쳐졌지만
`Tools/FX OVERDOSE/Migrate Event Templates (C3+C4)`([MigrateEventTemplates.cs](../../Assets/Scripts/Editor/MigrateEventTemplates.cs))가
**셰어된 에셋에 한 번도 실행되지 않았습니다.**

> **선행 작업**: 아래 4번 항목(수치 조정)에 착수하기 전에 마이그레이션 메뉴를 먼저 실행하고,
> 결과를 커밋해야 합니다. 순서를 바꾸면 "리밸런싱했는데 체감이 정반대"가 됩니다.

---

## 1. 체력 연동 멘탈 감소에 아이템 경감 적용

**요청**: 상시 자연 감소 3·4번 조건(체력 감소 연동)에도 `MentalDrainReduction`이 적용되어야 함.

### 현행

[TraderStatus.ChangeHealth()](../../Assets/Scripts/TraderStatus.cs#L445-L456)의 누적기 가산에는 경감이 없습니다.

```csharp
if (amount < 0f && prevHealth <= MaxHealth * 0.5f)
    healthDropMentalDrainAccumulator += amount;          // ← 경감 미적용
```

경감이 적용되는 곳은 [DecreaseStatusOverTime()](../../Assets/Scripts/TraderStatus.cs#L396-L401)의
`mentalDecreasePerSecond` 항뿐입니다.

### 실측 영향 (기본값 `secondsPerGameMinute = 0.666` → `speedScale = 7.5`)

| 경로 | 실시간 초당 멘탈 감소 | 아이템 경감(최대 80%) |
|---|---|---|
| `mentalDecreasePerSecond` 항 (체력 ≤ 50%) | `0.04 × 7.5 = 0.30` | 적용 ✅ |
| 체력 감소 연동 항 (Day 1) | `0.055 × 1.5 × 7.5 = 0.62` | **미적용** ❌ |
| 체력 감소 연동 항 (Day 20+) | `0.15 × 1.5 × 7.5 = 1.69` | **미적용** ❌ |

즉 **경감이 안 되는 쪽이 되는 쪽보다 2~5.6배 큽니다.** 멘탈 회복 아이템의 "멘탈 보호 -80%" 버프가
실제로는 전체 감소량의 15~30%에만 걸리므로, 체감 효과가 표기의 1/4 수준입니다.

### 함께 발견된 결함 두 가지

**(a) 체력 0에서도 연동 감소가 계속 청구됩니다.**
`ChangeHealth`는 `currentHealth`를 0으로 클램프하지만, 누적기에는 **클램프된 실제 변화량이 아니라
요청된 `amount`를 그대로** 더합니다([L447](../../Assets/Scripts/TraderStatus.cs#L447)).
`prevHealth = 0`이어도 `prevHealth <= MaxHealth * 0.5f`가 참이므로 조건을 통과합니다.
결과적으로 체력이 바닥난 뒤에도 "체력이 계속 떨어지는 것처럼" 멘탈이 깎입니다.

**(b) 자연 감소가 이중 계상됩니다.**
`DecreaseStatusOverTime()`은 (1) `ChangeHealth(-체력감소량)`을 호출하고 (2) 별도로
`mentalDecreasePerSecond`를 누적기에 더합니다. 그런데 (1)의 `ChangeHealth`가 내부에서
같은 누적기에 체력 감소량을 또 더합니다. 설계 문서상 별개인 두 규칙이
"시간 경과에 따른 체력 감소"라는 한 사건에 동시에 걸립니다.

체력 0 상황의 실제 합계: `0.16 × 7.5 = 1.20` + `1.69` = **1.89~2.89/실초 → 멘탈 100이 35~55초에 소진.**

### 계획

1. `ChangeHealth`의 누적기 가산을 `amount` → `appliedHealthDelta`(클램프 후 실제 변화량)로 교체 — 결함 (a) 해소.
2. 같은 지점에 `mentalGuard`를 곱한다.
   ```csharp
   float mentalGuard = ActiveItemEffectManager.Instance != null
       ? ActiveItemEffectManager.Instance.MentalDrainReduction : 0f;
   healthDropMentalDrainAccumulator += appliedHealthDelta * (1f - mentalGuard);
   ```
3. 결함 (b)는 **설계 판단이 필요합니다.** 두 안 중 택일:
   - **안 A (권장)**: `DecreaseStatusOverTime()`의 `mentalDecreasePerSecond` 항을 제거하고
     체력 연동 하나로 일원화. 규칙이 하나로 줄고, `mentalDecreasePerSecond` 필드도 삭제 가능.
   - **안 B**: 체력 연동 항에 계수를 넣어 감쇠(예: `× 0.3`). 두 규칙을 모두 유지하되 합계를 조정.

   > 안 A를 권합니다. 안 B는 "체력 연동"과 "시간 자연 감소"라는 이름이 다른 두 값이
   > 실제로는 같은 사건을 두 번 세는 구조를 그대로 남깁니다.

**영향 파일**: [TraderStatus.cs](../../Assets/Scripts/TraderStatus.cs)

---

## 2. FOMO 후회 기믹 발동 조건 완화 (2% → 5%)

**요청**: 놓친 신호의 주가 변동 임계치를 2% → 5%로 상향.

### 현행

[MentalDrainGimmickController.cs:429](../../Assets/Scripts/AI/MentalDrainGimmickController.cs#L429)

```csharp
if (trackedMissedSignal.IsTrueSignal || priceDeltaPct >= 2.0f
    || Math.Abs(trackedMissedSignal.TargetPercentageDelta) >= 2.0f)
```

### 계획

두 개의 `2.0f`를 `5.0f`로 변경합니다. 매직 넘버가 두 곳에 중복되므로 상수로 뽑습니다.

```csharp
private const float MissedOpportunityThresholdPct = 5.0f;
```

**주의**: `IsTrueSignal`이 참이면 변동률과 무관하게 발동하는 OR 분기가 앞에 있습니다.
임계치를 올려도 "진짜 신호였던 경우"는 여전히 100% 발동하므로, 실제 발동 빈도 감소폭은
"가짜 신호였지만 주가가 2~5% 움직인" 케이스에 한정됩니다. 체감 완화가 부족하면
`IsTrueSignal` 분기에도 변동률 조건을 AND로 묶는 2차 조정이 필요합니다.

**영향 파일**: [MentalDrainGimmickController.cs](../../Assets/Scripts/AI/MentalDrainGimmickController.cs)

---

## 3. 손실 청산 멘탈 감소를 자본 대비 비율로 전환 ★

**요청**: 전체 Balance 대비 자본 감소율을 반영한 계산 방안 물색.

### 현행의 문제

[TradingController.cs:1134](../../Assets/Scripts/Trading/TradingController.cs#L1134)

```csharp
traderStatus.ChangeMental(pnl * 0.05f);   // -1000원 손실 → 멘탈 -50
```

**절대 금액 기준이라 게임 진행에 따라 완전히 무너집니다.**
난이도별 초기 자본은 Hard 7,000 / Normal 20,000 / Easy 40,000
([StoryDifficulty.cs:30-32](../../Assets/Scripts/System/StoryDifficulty.cs#L30-L32))이고,
목표 자본까지 자본은 수십~수백 배로 불어납니다.

| 시점 | 잔고 | 손실 | 자본 대비 | 현행 멘탈 감소 |
|---|---|---|---|---|
| Hard 1일차 | 7,000 | -2,000 | 28.6% | **-100 (즉시 오버도즈)** |
| 중반 | 200,000 | -2,000 | 1.0% | **-100 (즉시 오버도즈)** |
| 후반 | 2,000,000 | -20,000 | 1.0% | -100 (즉시 오버도즈) |
| 후반 | 2,000,000 | -500 | 0.025% | -25 |

후반부에는 **잔고의 0.1%도 안 되는 손실이 멘탈 바 전체를 날립니다.** 사실상 후반 트레이딩은
"거래하면 오버도즈"가 되어 멘탈 시스템이 게임 메커니즘으로서 붕괴합니다.

### 방안 비교

`lossRatio = |pnl| / capitalBefore` 로 정의할 때:

| 방안 | 식 | 1% 손실 | 5% | 10% | 25% | 50% | 평가 |
|---|---|---|---|---|---|---|---|
| A. 선형+상한 | `min(30, 120·r)` | -1.2 | -6 | -12 | -30 | -30 | 소액 손실이 무의미해짐. 상한 도달이 급격 |
| **B. 제곱근+상한** | `min(25, 40·√r)` | **-4.0** | **-8.9** | **-12.6** | **-20** | **-25** | **권장** |
| C. 구간 바스켓 | ROE 기믹처럼 -5/-10/-20/-30 | -5 | -10 | -20 | -30 | -30 | 기존 설계 언어와 일치하나 경계에서 튐 |

**B안(제곱근)을 권합니다.** 근거:

- 소액 손실도 체감이 남습니다(1% 손실에 -4). 선형 A안은 1% 손실이 -1.2라 아무 감흥이 없습니다.
- 대형 손실에서 완만해집니다. "한 방에 오버도즈"가 사라지고, **누적으로만 오버도즈에 도달**합니다.
  이는 4연속 손절 뇌동매매 기믹(아래 참조)이 살아나기 위한 전제입니다.
- 구간 없이 연속적이라 경계에서 튀지 않고, 상한 25는 다른 감소원과 합산했을 때의 예산에서 역산한 값입니다.

### 분모 정의 — 구현 시 반드시 주의

`ClosePosition`에서 [`gameManager.ChangeBalance(totalReturn)`가 L1126](../../Assets/Scripts/Trading/TradingController.cs#L1126),
멘탈 반영이 [L1129](../../Assets/Scripts/Trading/TradingController.cs#L1129)입니다.
**멘탈 계산 시점의 `CurrentBalance`에는 이미 회수금이 반영되어 있습니다.**

분모는 진입 시점의 총자본이어야 하므로, `ChangeBalance` **호출 전에** 캡처해야 합니다.

```csharp
// ChangeBalance 호출 이전에 캡처
float capitalBefore = gameManager.CurrentBalance + marginAmount;   // 현금 + 증거금
...
if (pnl < 0f)
{
    float lossRatio = Mathf.Abs(pnl) / Mathf.Max(1f, capitalBefore);
    float mentalLoss = Mathf.Min(25f, 40f * Mathf.Sqrt(lossRatio));
    traderStatus.ChangeMental(-mentalLoss);
}
```

`Mathf.Max(1f, ...)`는 잔고 0 또는 음수(파산 직전) 상황의 0 나눗셈 방어입니다.

### 동반 변경 — 익절 회복도 비율 기반이어야 합니다

감소만 비율화하면 대칭이 깨집니다. 익절 회복은 여전히
[`pnl * 0.02f * winMultiplier`](../../Assets/Scripts/Trading/TradingController.cs#L1145)라
후반부에는 **한 번의 익절로 멘탈이 무조건 만땅**이 됩니다. 손실 쪽만 고치면
"손실은 안 아프고 익절은 만능"이 되어 멘탈 시스템이 반대 방향으로 무력화됩니다.

```csharp
float gainRatio = pnl / Mathf.Max(1f, capitalBefore);
float mentalGain = Mathf.Min(15f, 25f * Mathf.Sqrt(gainRatio)) * winMultiplier;
```

| 수익률 | 회복량 (winMultiplier 1.0) |
|---|---|
| 1% | +2.5 |
| 5% | +5.6 |
| 10% | +7.9 |
| 36% 이상 | +15 (상한) |

감소 대비 회복이 약 60% 수준이라 순 하락 압력이 유지됩니다 — 게임 컨셉에 부합합니다.

**영향 파일**: [TradingController.cs](../../Assets/Scripts/Trading/TradingController.cs)

---

## 4. 돌발 선택 이벤트 실패 페널티 범위 재산정 ★

**요청**: `-40 ~ -10 × riskMultiplier`는 광범위하고 단발 오버도즈 위험이 있음. 적정 범위 예측.

### 현행 범위 (요청보다 심각합니다)

[GenerateEventTemplates.cs:116](../../Assets/Scripts/Editor/GenerateEventTemplates.cs#L116)에서
`riskMultiplier = High 3 / Medium 2 / Low 1`이므로 실제 범위는:

| 리스크 | 실패 페널티 | 성공 보상 | 성공 확률 | 지뢰계 착용 시 최악 |
|---|---|---|---|---|
| Low (×1) | -40 ~ -11 | +12 ~ +5 | 40~70% | -50 |
| Medium (×2) | -80 ~ -22 | +24 ~ +10 | 40~70% | -100 |
| **High (×3)** | **-120 ~ -33** | +36 ~ +15 | 40~70% | **-150** |

High 리스크는 **30~60% 확률로 멘탈 100을 한 방에 지웁니다.** 리스크/리워드 비율도
실패 -120 대 성공 +36으로 3.3:1이라 기대값이 크게 음수입니다.

### 설계 기준

단일 이벤트가 정할 것은 "죽었다/살았다"가 아니라 **"이제 위험해졌다"**여야 합니다.

- 만멘탈(100)에서 최악의 단발 실패를 맞아도 오버도즈에 도달하지 않을 것 → **최대 페널티 ≤ 35**
- 이미 소모된 상태(예: 멘탈 50)에서 High 실패를 맞으면 Danger(≤25)에 진입할 것 → 최대 페널티 ≥ 25
- 리스크/리워드 비율은 성공 확률 40~70%를 감안해 **2:1 이내**

### 제안 범위

`riskMultiplier`를 페널티에 그대로 곱하는 방식(선형 ×1/×2/×3)을 폐기하고 별도 계수를 씁니다.

```csharp
// riskMultiplier(1/2/3)를 페널티에 직접 곱하지 않습니다.
float penaltyScale = risk == "High" ? 2.5f : (risk == "Medium" ? 1.6f : 1.0f);
MentalPenaltyOnFail = Mathf.RoundToInt(Random.Range(-12, -5) * penaltyScale);
MentalChangeAmount  = Mathf.RoundToInt(Random.Range(5, 13) * penaltyScale);
```

| 리스크 | 실패 페널티 (제안) | 성공 보상 (제안) | 최악 R/R | 지뢰계 최악 |
|---|---|---|---|---|
| Low (×1.0) | **-12 ~ -5** | +12 ~ +5 | 1.0 : 1 | -15 |
| Medium (×1.6) | **-19 ~ -8** | +19 ~ +8 | 1.0 : 1 | -24 |
| High (×2.5) | **-30 ~ -13** | +30 ~ +13 | 1.0 : 1 | **-37.5** |

- 만멘탈에서 High 최악(-30)을 맞아도 70 잔존 → Stable 유지
- 멘탈 50에서 High 최악 → 20 → Danger 진입 (의도된 긴장)
- 지뢰계 착용 최악 -37.5도 단독 오버도즈 불가

### 코드 측 안전망 (데이터와 무관하게 강제)

생성기만 고치면 손으로 작성한 SO나 재베이크를 잊은 에셋이 다시 -120을 들고 올 수 있습니다.
적용 지점에 상한을 겁니다.

```csharp
// ChoiceEventController.ApplyVitals()
private const int MaxSingleEventMentalPenalty = 35;
...
int mental = (!isBet || isSuccess) ? option.MentalChangeAmount : option.MentalPenaltyOnFail;
mental = Mathf.Max(mental, -MaxSingleEventMentalPenalty);   // 단발 상한 강제
```

### 적용 절차 (0번 항목과 연결)

242개 에셋이 이미 베이크되어 있고 `MigrateEventTemplates`의 주석([L14-15](../../Assets/Scripts/Editor/MigrateEventTemplates.cs#L14-L15))이
경고하듯 **생성기 재실행은 성공 확률 등 다른 밸런스까지 통째로 갈아엎습니다.** 따라서:

1. `Tools/FX OVERDOSE/Migrate Event Templates (C3+C4)` 실행 → 음수 `MentalChangeAmount`를 `MentalPenaltyOnFail`로 이관 (0번 항목)
2. 마이그레이터에 **리스케일 단계 추가** — 이관된 페널티를 위 표 범위로 압축
   (예: `newPenalty = Mathf.RoundToInt(oldPenalty * 0.25f)`, 하한 -35 클램프)
3. 생성기(`GenerateEventTemplates`)는 **신규 생성분에만** 적용되도록 수치 갱신
4. 코드 안전망 상한 추가

**영향 파일**: [GenerateEventTemplates.cs](../../Assets/Scripts/Editor/GenerateEventTemplates.cs),
[MigrateEventTemplates.cs](../../Assets/Scripts/Editor/MigrateEventTemplates.cs),
[ChoiceEventController.cs](../../Assets/Scripts/Events/ChoiceEventController.cs),
`Assets/Resources/Events/Templates/*.asset` (242개)

---

## 5. 트라우마 천장(`maxMentalLimit`) 전면 제거

**요청**: 트라우마 로직을 폐기했으므로 천장 하향 기능이 존재하면 안 됨.

### 잔재 현황

로직 본체는 이미 삭제됐고([TraderStatus.cs:51-52](../../Assets/Scripts/TraderStatus.cs#L51-L52) "(트라우마 변수 삭제됨)"),
**천장을 낮추는 호출자가 하나도 없습니다.** 남아 있는 것은 배관과 사용 불가능한 도달 지점뿐입니다.

| 위치 | 내용 | 처리 |
|---|---|---|
| [TraderStatus.cs:43](../../Assets/Scripts/TraderStatus.cs#L43) | `maxMentalLimit` 필드 | 삭제 |
| [TraderStatus.cs:95-106](../../Assets/Scripts/TraderStatus.cs#L95-L106) | `MaxMentalLimit` 프로퍼티 (파자마 +15 보너스 중복) | 삭제 |
| [TraderStatus.cs:107](../../Assets/Scripts/TraderStatus.cs#L107) | `EffectiveMaxMental` | `MaxMental`로 치환 |
| [TraderStatus.cs:486](../../Assets/Scripts/TraderStatus.cs#L486) | `Mathf.Min(maxMental, maxMentalLimit)` 클램프 | `MaxMental` 단독으로 |
| [TraderStatus.cs:622-637](../../Assets/Scripts/TraderStatus.cs#L622-L637) | `SetMaxMentalCeiling()` | 메서드 삭제 |
| [TraderStatus.cs:212/235/273/303/363/649](../../Assets/Scripts/TraderStatus.cs#L212) | 저장·복원·동기화·리셋의 `maxMentalLimit` 배관 | 각 줄 삭제 |
| [SaveData.cs:54](../../Assets/Scripts/System/SaveData.cs#L54) | `MaxMentalLimit` 필드 | 삭제 (구 세이브의 잉여 키는 `JsonUtility`가 무시하므로 마이그레이션 불필요) |
| [ItemUser.cs:166-186](../../Assets/Scripts/Items/ItemUser.cs#L166-L186) | 천장 극복 분기 | 삭제, `RestoreMental` 진입 조건을 `CurrentMental >= MaxMental`로 단순화 |

### 함께 정리할 것

- **`AchievementManager.RecordTraumaCured()`([L313](../../Assets/Scripts/System/AchievementManager.cs#L313))는 호출자가 없습니다.**
  따라서 업적 `trauma_cured`("트라우마 극복")는 **현재 획득 불가능**합니다. 업적 정의
  ([L99](../../Assets/Scripts/System/AchievementManager.cs#L99)), 메서드, `Pref_TraumaCured` 상수, 판정 분기
  ([L169](../../Assets/Scripts/System/AchievementManager.cs#L169), [L364-365](../../Assets/Scripts/System/AchievementManager.cs#L364-L365)),
  리셋 처리([L412](../../Assets/Scripts/System/AchievementManager.cs#L412))를 함께 제거합니다.
  > **결정 필요**: 업적 목록에서 항목이 사라지면 기존 플레이어의 업적 번호/진행률 표시가 밀립니다.
  > 대안은 업적을 남기고 달성 조건을 다른 것으로 교체하는 것입니다. 기획 판단이 필요합니다.
- `IncreaseMaxMental()`([L639](../../Assets/Scripts/TraderStatus.cs#L639))은 **유지**합니다.
  최대 멘탈 영구 증가 아이템([ItemUser.cs:101](../../Assets/Scripts/Items/ItemUser.cs#L101))이 실사용 중이며 트라우마와 무관합니다.
  단 내부의 `maxMentalLimit += amount` 줄은 제거합니다.
- [MentalDrainGimmickController.cs:8](../../Assets/Scripts/AI/MentalDrainGimmickController.cs#L8)의 클래스 요약 주석이
  아직 "6대 기믹(… 수면 부족 연쇄, 횡보 지루함, 드로다운 트라우마)"을 서술합니다. 실제 남은 기믹은
  미실현 손실 압박 / 연속 손절 / 고배율 중독 / 포지션 진입 / FOMO 후회 5개입니다. 주석을 실제에 맞게 갱신합니다.

**영향 파일**: [TraderStatus.cs](../../Assets/Scripts/TraderStatus.cs),
[SaveData.cs](../../Assets/Scripts/System/SaveData.cs), [ItemUser.cs](../../Assets/Scripts/Items/ItemUser.cs),
[AchievementManager.cs](../../Assets/Scripts/System/AchievementManager.cs),
[MentalDrainGimmickController.cs](../../Assets/Scripts/AI/MentalDrainGimmickController.cs)

---

## 6. 지뢰계 의상 ×1.25 배율 오류 수정

**요청**: 자연 감소는 1.25배를 맞으면 안 되고, 나머지 멘탈 감소는 전부 1.25배를 맞아야 함.

### 현행 (의도와 정반대로 동작)

[TraderStatus.cs:476-480](../../Assets/Scripts/TraderStatus.cs#L476-L480)

```csharp
// 시간에 따른 자연 감소("TimeDrain")를 제외한 모든 멘탈 감소 수치 1.25배 가속
if (amount < 0f && reason != "TimeDrain" && ... EquippedCostumeId == JiraiKeiId)
    amount *= 1.25f;
```

**`"TimeDrain"`이라는 reason으로 `ChangeMental`을 호출하는 코드가 프로젝트 전체에 없습니다.**
자연 감소는 [L345](../../Assets/Scripts/TraderStatus.cs#L345)에서 `"체력 저하"`라는 reason으로 들어옵니다.
따라서 예외 조건이 절대 참이 되지 않고, **자연 감소도 1.25배를 맞고 있습니다.**

### 계획 — 최소 수정안

`reason` 문자열은 [VitalsValueUI](../../Assets/Scripts/UI/VitalsValueUI.cs#L25)가 화면에 표시하는 값이라
`"체력 저하"`를 `"TimeDrain"`으로 바꿀 수는 없습니다. 상수를 도입해 비교 대상을 실제 값에 맞춥니다.

```csharp
// 자연 감소(체력 연동)는 지뢰계 증폭에서 제외됩니다.
public const string NaturalDrainReason = "체력 저하";
...
if (amount < 0f && reason != NaturalDrainReason && CostumeManager.Instance != null
    && CostumeManager.Instance.EquippedCostumeId == CostumeManager.JiraiKeiId)
{
    amount *= 1.25f;
}
```

L345의 호출부도 리터럴 대신 상수를 쓰도록 바꿉니다. 자연 감소는 이 한 곳에서만 발생하므로
상수 하나로 전 경로가 커버됩니다.

> `reason` 문자열 비교는 취약합니다. 감소 출처가 더 늘어나면
> `MentalChangeSource` 열거형 + `ChangeMental(float, MentalChangeSource, string reason)` 시그니처로
> 승격하는 것이 정공법이나, 현 시점에는 제외 대상이 하나뿐이라 상수로 충분합니다.
> — `ponytail: 문자열 reason 비교, 제외 출처가 2개 이상 되면 enum으로 승격`

**검증**: 지뢰계 착용 후 체력을 50% 이하로 떨어뜨리고 멘탈 감소 로그의 계수가 1.0인지 확인.
동시에 포지션 진입(-10)이 -12.5로 찍히는지 확인.

**영향 파일**: [TraderStatus.cs](../../Assets/Scripts/TraderStatus.cs)

---

## 7. `canRegenMental` 제거

**요청**: 필요 없으면 삭제.

### 현황

**`canRegenMental`을 `false`로 설정하는 코드가 프로젝트 전체에 없습니다.**
필드 주석([L49](../../Assets/Scripts/TraderStatus.cs#L49))의 "체력 30% 이하 시 false"는 구현된 적이 없고,
값은 항상 `true`입니다. 따라서 `ChangeMental`의 회복 차단 분기([L471-474](../../Assets/Scripts/TraderStatus.cs#L471-L474))는
**절대 실행되지 않는 죽은 코드**이며, 이를 우회하기 위한 `ignoreRegenBlock` 파라미터도 무의미합니다.

### 계획

| 위치 | 처리 |
|---|---|
| [TraderStatus.cs:49](../../Assets/Scripts/TraderStatus.cs#L49) | `canRegenMental` 필드 삭제 |
| [TraderStatus.cs:135-139](../../Assets/Scripts/TraderStatus.cs#L135-L139) | `CanRegenMental` 프로퍼티 삭제 |
| [TraderStatus.cs:462](../../Assets/Scripts/TraderStatus.cs#L462) | `ChangeMental` 시그니처에서 `ignoreRegenBlock` 파라미터 제거 |
| [TraderStatus.cs:471-474](../../Assets/Scripts/TraderStatus.cs#L471-L474) | 회복 차단 분기 삭제 |
| [TraderStatus.cs:218/242/279/309/370](../../Assets/Scripts/TraderStatus.cs#L218) | 저장·복원·동기화·리셋 배관 삭제 |
| [SaveData.cs:155](../../Assets/Scripts/System/SaveData.cs#L155) | `CanRegenMental` 필드 삭제 (마이그레이션 불필요) |
| [TraderStatus.cs:618](../../Assets/Scripts/TraderStatus.cs#L618) | `ModifyMentalState`의 `ChangeMental(amount, true)` → `ChangeMental(amount)` |
| [ItemUser.cs:103/123/192](../../Assets/Scripts/Items/ItemUser.cs#L103) | `ChangeMental(x, true)` → `ChangeMental(x)` |
| [GameManager.cs:717](../../Assets/Scripts/GameManager.cs#L717) | `ChangeMental(9999f, true, "NewDayReset")` → `ChangeMental(9999f, "NewDayReset")` |

`ChangeMental`의 시그니처가 `(float amount, string reason = "")`로 단순해집니다.

**영향 파일**: [TraderStatus.cs](../../Assets/Scripts/TraderStatus.cs),
[SaveData.cs](../../Assets/Scripts/System/SaveData.cs), [ItemUser.cs](../../Assets/Scripts/Items/ItemUser.cs),
[GameManager.cs](../../Assets/Scripts/GameManager.cs)

---

## 8. AITradingBrain — 전제 정정 및 실제 사장 코드 제거

**요청**: LLM 자동 매매 계획이 폐지되어 이름만 남았는지 확인하고, 무의미하면 삭제.

### 확인 결과: **클래스는 살아 있고, 삭제하면 자동 매매가 동작하지 않습니다.**

[AITradingBrain.cs](../../Assets/Scripts/AI/AITradingBrain.cs)(834줄)를 전수 확인했습니다.

- **LLM 코드는 원래부터 없습니다.** 파일 전체에 `LLM`/`llm` 문자열이 0건입니다.
  규칙 기반 시그널 판정 엔진이며, LLM 자동매매의 잔재가 아닙니다.
- **현재 게임의 자동 매매 실행 주체입니다.**
  - [L41/L46](../../Assets/Scripts/AI/AITradingBrain.cs#L41) `TradeExecutor` → `tradingController.OpenPosition(...)`
  - [L222/L227](../../Assets/Scripts/AI/AITradingBrain.cs#L222) `tradingController.ClosePosition()`
- **다른 시스템이 의존합니다.**
  - [MentalDrainGimmickController](../../Assets/Scripts/AI/MentalDrainGimmickController.cs#L119)가
    `OnSignalEvaluationCompleted`를 구독 — **FOMO 후회 기믹(2번 항목)의 유일한 입력원**입니다.
  - 같은 파일 [L289](../../Assets/Scripts/AI/MentalDrainGimmickController.cs#L289)가 `ForceNextTradeHighLeverage`를 세팅 — 고배율 중독 폭주 기믹
  - [TradingController.cs:1049-1052](../../Assets/Scripts/Trading/TradingController.cs#L1049-L1052)가 `ProvideChartHintToPlayer` 호출
  - `BossAIController`, `AIVisualController`, `AITradingSystemTestRunner`(에디터 테스트)도 참조

> **결론: 클래스 삭제는 계획에서 제외합니다.** 삭제하면 자동 매매, FOMO 기믹, 고배율 중독 폭주,
> 차트 힌트가 동시에 죽습니다. 이름이 오해를 부른다면 클래스 요약 주석에
> "규칙 기반 판정 엔진이며 LLM을 사용하지 않는다"를 명시하는 편이 안전합니다.

### 다만 사장된 코드는 실제로 있습니다 — 이쪽을 삭제합니다

| 위치 | 상태 | 처리 |
|---|---|---|
| [AITradingBrain.cs:805-808](../../Assets/Scripts/AI/AITradingBrain.cs#L805-L808) `TriggerDialogue` | **본문이 비어 있고 호출자 0건** | 삭제 |
| [AITradingBrain.cs:810-817+](../../Assets/Scripts/AI/AITradingBrain.cs#L810) `TriggerDialogueWithCategory` | **호출자 0건** | 삭제 |

후자가 이 파일에서 멘탈을 건드리는 **유일한** 코드입니다
(`traderStatus.ModifyMentalState(emotionDelta * 10f)`, [L816](../../Assets/Scripts/AI/AITradingBrain.cs#L816)).
도달 불가능하므로 멘탈 감소 경로에서 완전히 배제됩니다.

메서드 본문에 포함된 `TraderMemoryManager.AddMemory` 호출도 함께 사라지므로,
**대사 기반 기억 등록이 의도된 기능이었다면 삭제 전에 재배선 여부를 판단해야 합니다.**
현재 코드상 호출되지 않으므로 기능적 손실은 0입니다.

**영향 파일**: [AITradingBrain.cs](../../Assets/Scripts/AI/AITradingBrain.cs)

---

## 합산 피해 예산 (Stacking Budget)

개별 수치보다 중요한 것은 **합산**입니다. 단일 매매 손실 한 번에 여러 기믹이 동시에 걸립니다.

### 설계 제약: 4연속 손절 뇌동매매 기믹이 발동 가능해야 합니다

[MentalDrainGimmickController.cs:333-338](../../Assets/Scripts/AI/MentalDrainGimmickController.cs#L333-L338)의
4연속 손절 → 100배 뇌동매매 기믹은 **3연속 손절 후에도 멘탈이 남아 있어야** 발동합니다.
현행 수치로는 그 전에 오버도즈가 확정되어 **이 기믹이 사실상 사장되어 있습니다.**

### 시나리오: 수동 매매 3연속 손절 (매회 자본의 5% 손실, 지뢰계 미착용)

| 감소원 | 현행 | 제안 |
|---|---|---|
| 진입 ×3 | -10 ×3 = **-30** | -5 ×3 = **-15** |
| 손실 청산 ×3 | 자본 5% = 350원 손실 시 -17.5 ×3 = **-52.5**<br>(후반 자본 100만이면 5% = 5만원 → **-100 ×3**) | `40·√0.05 = 8.9` ×3 = **-26.7** |
| 연속 손절 (×1.5 수동) | (5+12+25)×1.5 = **-63** | (4+9+16)×1.5 = **-43.5** |
| **누적** | **-145.5 (2회차에 오버도즈)** | **-85.2 (멘탈 14.8 잔존 → Danger)** |

제안 수치에서는 3연속 손절 후 Danger 상태로 살아남아 **4연속 뇌동매매 기믹이 정상 발동**합니다.
자동 매매(요미 주도, ×1.5 미적용) 시에는 -70.7로 29.3이 남아 여유가 더 큽니다.

### 제안 수치 요약표

| # | 항목 | 현행 | 제안 |
|---|---|---|---|
| 8 | 포지션 진입/물타기 | -10 | **-5** |
| 9~11 | 연속 손절 1/2/3회 | -5 / -12 / -25 | **-4 / -9 / -16** |
| 12 | 연속 손절 4회+ | 0 | 0 (유지) |
| 13 | 수동 매매 배율 | ×1.5 | ×1.5 (유지) |
| 14 | FOMO 후회 | -15 (2% 조건) | -15 (**5% 조건**) |
| 15 | 손실 청산 | `pnl × 0.05` (절대금액) | **`-min(25, 40·√손실률)`** |
| 15' | 익절 회복 | `pnl × 0.02 × mult` (절대금액) | **`+min(15, 25·√수익률) × mult`** |
| 19 | 이벤트 실패 | -120 ~ -11 | **-30 ~ -5** |
| 5~7 | 미실현 손실 압박 | -0.01 / -0.05 / -0.15 초당 | 유지 |
| 1~4 | 자연 감소 | 아이템 경감 부분 적용 | 전면 적용 + 이중 계상 해소 |

> 8·9~11번 하향은 예산 계산에서 역산한 값으로 사용자 요청에 없던 항목입니다.
> 15번(비율화)만 적용하면 후반부 손실 청산 피해가 급감하는 반면 8·9~11번은 절대값이라
> **후반에는 고정 페널티가 전체의 대부분을 차지**하게 됩니다. 두 축을 함께 조정하지 않으면
> "초반엔 청산이 무섭고 후반엔 진입이 무서운" 비일관적 체감이 남습니다.
> 별도 판단이 필요하면 15번만 먼저 적용하고 8·9~11번은 플레이테스트 후 결정해도 됩니다.

---

## 작업 순서

의존 관계가 있어 순서가 중요합니다.

| 단계 | 작업 | 근거 |
|---|---|---|
| **0** | `Migrate Event Templates (C3+C4)` 실행 및 커밋 | 4번의 전제. 현재 라이브 데이터가 성공 시 -120 |
| **1** | 7번 (`canRegenMental` 제거) | `ChangeMental` 시그니처가 바뀌므로 다른 수정보다 먼저 |
| **2** | 5번 (트라우마 천장 제거) | 같은 파일의 대규모 삭제, 6번보다 먼저 |
| **3** | 6번 (지뢰계 배율 수정) | 1~2단계로 정리된 `ChangeMental` 위에서 작업 |
| **4** | 1번 (아이템 경감 + 이중 계상) | 안 A/B 기획 판단 필요 |
| **5** | 8번 (사장 메서드 삭제) | 독립적, 아무 때나 가능 |
| **6** | 2번 (FOMO 임계치) | 독립적 |
| **7** | 3번 (손실 청산 비율화 + 익절 대칭) | 밸런스 핵심 |
| **8** | 4번 (이벤트 페널티 리스케일) | 0단계 완료 후 |
| **9** | 합산 예산표 기준 수치 일괄 적용 | 8·9~11번 하향 여부 결정 후 |

각 단계마다 컴파일 확인:
```
dotnet build "Assembly-CSharp.csproj" -v:m
dotnet build "Assembly-CSharp-Editor.csproj" -v:m
```

---

## 검증 방법

이 프로젝트는 `FXOverdose.P2P.Core` 외에 자동 테스트가 없습니다. 수동 검증 항목:

1. **로그 기반 확인** — `ChangeMental`의 `reason` 인자가 이미 전 경로에 붙어 있습니다
   ([OnMentalChangedWithReason](../../Assets/Scripts/TraderStatus.cs#L63)).
   디버그 빌드에서 `reason`과 최종 적용량을 함께 로그로 남기면 어느 기믹이 얼마를 깎았는지 추적 가능합니다.
2. **지뢰계 배율 (6번)** — 착용 상태에서 자연 감소 로그 계수 1.0, 진입 페널티 -12.5 확인.
3. **비율 기반 청산 (3번)** — 잔고 7,000과 잔고 700,000에서 각각 자본의 10%를 손실 청산했을 때
   **동일하게 -12.6**이 나오는지 확인. 이것이 이번 변경의 핵심 회귀 지점입니다.
4. **4연속 손절 기믹 (예산)** — 의도적으로 4연속 손절을 만들어 뇌동매매가 실제로 발동하는지 확인.
   현행에서는 발동 전에 오버도즈합니다.
5. **이벤트 페널티 (4번)** — `Tools/FX Overdose/Clear All Save Data & Achievements` 후
   High 리스크 베팅 실패를 만들어 -35를 초과하지 않는지 확인.

---

## 미결 사항 (기획 판단 필요)

| # | 사안 | 선택지 |
|---|---|---|
| 1 | 자연 감소 이중 계상 해소 방식 | 안 A(체력 연동으로 일원화, 권장) / 안 B(계수로 감쇠) |
| 5 | `trauma_cured` 업적 | 목록에서 제거 / 달성 조건을 다른 것으로 교체 |
| 예산 | 8·9~11번(진입·연속손절) 하향 | 3번과 함께 즉시 적용 / 플레이테스트 후 결정 |
| 2 | FOMO 완화폭 | 5% 임계치만 적용 / `IsTrueSignal` 분기에도 AND 조건 추가 |
