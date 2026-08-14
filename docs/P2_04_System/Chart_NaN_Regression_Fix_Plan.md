# 차트 미렌더링 / 자금·손익 NaN 회귀 수정 계획

작성일: 2026-08-14 / 대상: `GameScene` 시장 시뮬레이션 · 세이브 복원
상태: **적용 완료 (2026-08-14)** — 인게임 검증만 남음

## 적용 결과

| 항목 | 상태 |
| --- | --- |
| 3.1 복원 지점 가드 (근본) | ✅ [MarketSimulationEngine.cs:282](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L282) |
| 3.2 최저가 방어 NaN 대응 ×2 | ✅ 동 파일 459행 / 814행 |
| 3.2 PnL `entryPrice` 가드 | ✅ [TradingController.cs:1233](../../Assets/Scripts/Trading/TradingController.cs#L1233) |
| 3.3 시간 배속 센티널 + 복원 조건 | ✅ [SaveData.cs](../../Assets/Scripts/System/SaveData.cs#L46) · [SaveLoadManager.cs](../../Assets/Scripts/System/SaveLoadManager.cs#L484) |
| C7 `GameManager`의 `0.666f` | ⛔ **보류** — 죽은 값이 아니었음 (아래 참조) |

4개 어셈블리 전부 에러 0 / 경고 0. 실제 세이브 3개에 새 가드를 대입한 예측 검증 통과.

### 실제 세이브 대입 결과

| 슬롯 | 차트 | 시간 배속 |
| --- | --- | --- |
| slot_0 (가격 0, 캔들 0) | 가드 발동 → `ResetEngine` 정상 초기화 | 저장값 3.0 **무시** → 씬 기준 1.0 |
| slot_1 (가격 0, 캔들 0) | 가드 발동 → 정상 초기화 | 1.0 유지 |
| **slot_2 (정상 세이브)** | **가격 67,915 · 캔들 205개 그대로 복원** | 1.0 주입 — **회귀 없음** |

### C7 보류 사유 (계획 수립 시 오판)

`0.666f`를 "모든 씬이 덮어쓰는 죽은 값"으로 판단했으나, [GameManager.cs:92](../../Assets/Scripts/GameManager.cs#L92)의 주석이

```
// 현실에서 몇 초마다 게임 속 1분이 흐를지 설정 (기존 1.0초에서 1.5배 가속하여 약 0.666초 -> 하루 현실 시간 10분)
```

즉 **0.666은 "하루를 현실 10분으로 줄이자"는 의도된 설계 변경**이고, 씬들이 아직 `1`(하루 15분)에 머물러 있는 것입니다.
코드 기본값을 1로 맞추면 그 의도를 지우게 되므로 손대지 않았습니다.

> **미결(기획 판단): 기준 배속을 0.666(하루 10분)으로 올릴 것인가, 1.0(하루 15분)을 확정할 것인가.**
> 이번 수정과는 독립입니다 — 복원 가드는 어느 쪽이든 씬 값을 그대로 존중합니다.
> 0.666으로 갈 경우 세 씬(`GameScene`/`tutorial`/`SampleScene`)의 직렬화 값을 함께 바꿔야 합니다.

---


## 증상

- 트레이딩 파트에서 **캔들이 하나도 렌더링되지 않음**
- **balance / PnL 등 수치가 전부 `NaN`**
- 자동매매 로직 자체는 계속 도는 것처럼 보임

## 결론 먼저

멘탈 리밸런싱 작업과 **무관합니다.** `TradingController` 변경분은 balance·price에 쓰기가 없고(멘탈만 기록),
`MarketSimulationEngine`과 차트 UI는 이번 세션에 수정하지 않았습니다.

원인은 **차트 데이터가 비어 있는 세이브를 아무 검사 없이 복원**하는 것이며, 아래 사슬 전체를 코드로 확인했습니다.

---

## 1. 원인 사슬 (전 단계 코드 확인 완료)

| # | 위치 | 내용 |
| --- | --- | --- |
| 1 | `SaveLoadManager.PrepareNewGame()` | `CurrentData = new SaveData()` → `CurrentChartPrice = 0`, `ChartHistories` 비어 있음 |
| 2 | [YomiRoomManager.cs:78](../../Assets/Scripts/DatingSim/YomiRoom/YomiRoomManager.cs#L78) | 스토리 새 게임은 요미의 방에서 시작. 도착 저장(F-11)이 슬롯 파일을 씀 |
| 3 | [SaveLoadManager.cs:162](../../Assets/Scripts/System/SaveLoadManager.cs#L162) | 그 씬엔 엔진이 없어 `marketEngine?.CaptureSaveData(data)`가 건너뛰어짐 → **0이 디스크에 확정** |
| 4 | [MarketSimulationEngine.cs:282](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L282) | `RestoreFromSaveData`에 **가드 없음.** `currentPrice = 0`, `ouCenterPrice = 0`, 캔들 목록은 빈 배열로 재구성 |
| 5 | [MarketSimulationEngine.cs:343](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L343) | 같은 함수가 `wasLoaded = true`를 세워 `if (!wasLoaded) ResetEngine(...)`이 **건너뛰어지고** `IsDataPrepared = true`로 선언됨 → **캔들 0개** |
| 6 | [MarketSimulationEngine.cs:599](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L599) | `ouTerm = ouTheta * (ouCenterPrice - currentPrice) / currentPrice` → `(0-0)/0` = **0/0 = NaN** |
| 7 | [MarketSimulationEngine.cs:796](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L796) | `priceDelta = currentPrice * totalReturn` = `0 * NaN` = NaN → `currentPrice = NaN`. 최저가 방어 `if (currentPrice < 10f)`가 **NaN을 못 잡음**(`NaN < 10f`는 false) → 가격이 NaN으로 영구 고착 |
| 8 | [TradingController.cs:1233](../../Assets/Scripts/Trading/TradingController.cs#L1233) | 진입 시 `entryPrice = NaN`. 가드 `entryPrice <= 0f`도 **NaN을 통과시킴**(`NaN <= 0f`는 false) → PnL NaN |
| 9 | `TradingController.ClosePosition` | `totalReturn = marginAmount + NaN` → `gameManager.ChangeBalance(NaN)` → **balance NaN**, 이후 전 UI로 전파 |

`AITradingBrain`은 신호·타이머 구동이라 가격이 NaN이어도 루프가 계속 돌아 "자동매매는 살아 있는" 것처럼 보입니다.

> **핵심 교훈:** `< 0`, `<= 0`, `< 10f` 형태의 비교는 **NaN에 대해 항상 false**라 방어가 되지 않습니다.
> NaN까지 막으려면 `!(x > 0f)`, `!(x >= 10f)`처럼 **긍정 조건을 부정**해야 합니다.
> 7·8번 두 곳의 기존 가드가 정확히 이 함정에 빠져 있습니다.

---

## 2. 실제 세이브 파일 실측 (`%USERPROFILE%\AppData\LocalLow\DefaultCompany\fx overdose\`)

| 슬롯 | 수정 시각 | `CurrentChartPrice` | 캔들 수 | `LastSceneName` | `Balance` | `SecondsPerGameMinute` |
| --- | --- | --- | --- | --- | --- | --- |
| **slot_0** | 2026-08-14 17:36 | **0.0** | **0** | `YomiRoomScene` | 40000 (정상) | **3.0** |
| slot_1 | 2026-08-10 20:11 | **0.0** | **0** | (없음/구버전) | 7000 (정상) | 1.0 |
| slot_2 | 2026-08-14 01:34 | 67915.04 | 205 | (없음/구버전) | 6017.06 (정상) | 1.0 |

### 여기서 확정되는 사실 3가지

1. **디스크 세이브는 오염되지 않았습니다.** 세 슬롯 모두 최상위 필드에 `NaN`/`Inf`가 **하나도 없고**, `Balance`도 정상입니다.
   NaN은 런타임에서만 발생했고 저장으로 굳지 않았습니다. → **가드만 넣으면 slot_0은 잔고 40,000 / 1일차를 유지한 채 정상 복구됩니다. 세이브 마이그레이터가 필요 없습니다.**
2. **이 결함은 F-11(도착 저장)보다 오래됐습니다.** slot_1(8/10)도 `CurrentChartPrice = 0`, 캔들 0개입니다.
   F-11은 원인을 **새로 만든 게 아니라, 스토리 새 게임이 반드시 이 경로를 밟게 만들어 상시 재현되게 했습니다.**
3. **파생 버그 발견 — 게임 시간 배속.** slot_0만 `SecondsPerGameMinute = 3.0`인데, 이는
   [SaveData.cs:46](../../Assets/Scripts/System/SaveData.cs#L46)의 초기화 기본값입니다. 방에는 `GameManager`가 없어
   [SaveLoadManager.cs:135-137](../../Assets/Scripts/System/SaveLoadManager.cs#L135)의 기록 블록이 통째로 건너뛰어졌고,
   복원 때는 그 3.0이 아무 검사 없이 `GameManager.secondsPerGameMinute`에 주입됩니다.
   **기준값은 1.0이므로 게임 시간이 3배 느려집니다.**

   하필 `3.0f`는 [강제 청산 슬로우 모션의 목표 배속](../../Assets/Scripts/Trading/TradingController.cs#L1273)과 같은 숫자입니다.
   즉 slot_0을 이어하면 게임이 **"강제 청산 연출 속도"에 영구히 고정된 채** 진행됩니다.

### 기준 배속이 1.0인 근거 (씬 직렬화 값)

```
GameScene.unity   secondsPerGameMinute: 1
tutorial.unity    secondsPerGameMinute: 1
SampleScene.unity secondsPerGameMinute: 1
```

코드에 흩어진 세 기본값 중 **권위를 갖는 것은 씬의 1.0뿐**입니다.

| 위치 | 리터럴 | 실제 역할 |
| --- | --- | --- |
| [GameManager.cs:94](../../Assets/Scripts/GameManager.cs#L94) | `0.666f` | **죽은 값.** 모든 씬이 1로 덮어씀 |
| [DynamicTimeRegulator.cs:11](../../Assets/Scripts/Core/DynamicTimeRegulator.cs#L11) | `1.0f` | 자리표시자. [Start()에서 `gameManager.SecondsPerGameMinute`로 덮어씀](../../Assets/Scripts/Core/DynamicTimeRegulator.cs#L37) |
| [SaveData.cs:46](../../Assets/Scripts/System/SaveData.cs#L46) | `3.0f` | **버그의 원인.** 기준값도 아니고 하필 슬로우 모션 수치와 동일 |

> `DynamicTimeRegulator.baseSecondsPerMinute`은 **슬로우 모션 값이 아니라 연출 후 복귀할 평상시 기준값**입니다
> ([L83](../../Assets/Scripts/Core/DynamicTimeRegulator.cs#L83)에서 복귀에 사용). 슬로우 모션 목표치는 전부 호출자가 인자로 넘깁니다 —
> 오버도즈 폭주 `2.5f`/5초([TraderStatus.cs:513](../../Assets/Scripts/TraderStatus.cs#L513)),
> 강제 청산 `3.0f`/3초, 기타 `2.0f`/4초.
> 저장 시 현재 배속이 아니라 `BaseSecondsPerMinute`(기준값)을 찍는 것은 **의도된 올바른 설계**입니다 —
> 슬로우 모션 도중 저장해도 연출 배속이 굳지 않습니다.

---

## 3. 수정안

### 3.1 근본 수정 — 복원 지점 가드 (필수)

```csharp
// MarketSimulationEngine.RestoreFromSaveData
public void RestoreFromSaveData(FXOverdose.Core.SaveData data)
{
    // 거래를 한 번도 시작하지 않은 세이브(요미의 방에서 먼저 저장된 새 게임)는 차트가 비어 있습니다.
    // 그대로 복원하면 가격 0 → OU 항 0/0 = NaN → 가격이 NaN으로 고착됩니다.
    // wasLoaded를 세우지 않고 빠져나가 Start()가 정상 ResetEngine을 수행하게 둡니다.
    if (!(data.CurrentChartPrice > 0f))
    {
        Debug.LogWarning("[MarketSimulationEngine] 세이브에 차트 데이터가 없어 정상 초기화로 진행합니다.");
        return;
    }

    wasLoaded = true;
    ...
}
```

`RestoreFromSaveData`가 `Start()`보다 먼저 불리든 나중에 불리든 안전합니다.
먼저 불리면 `wasLoaded`가 false로 남아 `Start()`가 `ResetEngine`을 수행하고, 나중에 불리면 이미 초기화된 정상 가격이 그대로 유지됩니다.

**호출자가 하나(`ApplyLoadedDataToGame`)뿐이라 이 한 곳이 모든 경로를 덮습니다.**

### 3.2 방어 가드 — NaN이 자금까지 번지지 않게 (권장)

```csharp
// MarketSimulationEngine.cs:796 — 최저가 방어가 NaN도 잡도록
if (!(currentPrice >= 10f)) currentPrice = 10f;

// TradingController.cs:1233 — NaN entryPrice 차단
if (currentPosition == PositionType.None || marketEngine == null || !(entryPrice > 0f))
```

3.1만으로 이번 증상은 사라지지만, 다른 경로로 NaN이 생기면 **또 조용히 자금까지 번집니다.**
두 줄은 그 전파를 끊는 최소 방어입니다.

### 3.3 게임 시간 배속 (2절 3번) — 센티널 채택

기준값을 상수로 또 박아 넣으면 세 번째 사본이 생겨 같은 사고가 반복됩니다.
**"저장된 적 없음"을 표현하는 센티널**을 써서 값의 출처를 씬 하나로 유지합니다.
`StartOfDayEquity = -1f`가 이미 쓰는 방식이라 코드베이스 관례와도 일치합니다.

```csharp
// SaveData.cs
// -1 = 저장된 적 없음(방·월드맵처럼 GameManager가 없는 씬에서 저장된 경우).
// 복원 측이 이 값을 보고 주입을 건너뛰어 씬에 설정된 기준 배속을 유지합니다.
public float SecondsPerGameMinute = -1f;

// SaveLoadManager.ApplyLoadedDataToGame — 유효한 값일 때만 주입
if (CurrentData.SecondsPerGameMinute > 0f)
{
    gmType.GetField("secondsPerGameMinute", ...)?.SetValue(gm, CurrentData.SecondsPerGameMinute);
}
```

곁가지로 [GameManager.cs:94](../../Assets/Scripts/GameManager.cs#L94)의 `0.666f`는 모든 씬이 덮어쓰는 죽은 값이라
혼동을 줍니다. 씬과 같은 `1f`로 맞추기를 권합니다(동작 변화 없음).

---

## 4. 확인이 필요한 사항

| # | 사안 | 선택지 | 비고 |
| --- | --- | --- | --- |
| **C1** | 근본 수정 방식 | **(A) 복원 가드 (권장)** / (B) 방에서 저장 금지 / (C) `PrepareNewGame`에서 차트 초기값 시딩 | B는 F-11 복귀 지점 기록을 되돌리게 되고, C는 데이터 층이 엔진 상수를 알아야 합니다 |
| **C2** | 방어 가드(3.2) 적용 | **(A) 두 줄 함께 적용 (권장)** / (B) 근본 수정만 | B를 고르면 다른 NaN 경로가 생겼을 때 다시 자금까지 번집니다 |
| ~~C3~~ | ~~게임 시간 기준값~~ | **해결됨 — 씬 직렬화 값이 `1`로 확정.** 별도 확인 불필요 | 2절 참조 |
| **C4** | 시간 배속 수정 방식 | **센티널 `-1f` 채택 (3.3절)** | 기준값 사본을 늘리지 않는 쪽 |
| **C5** | slot_0 / slot_1 처리 | **(A) 그대로 두기 (권장)** / (B) 삭제 후 새로 시작 | 가드 적용 시 두 슬롯 모두 잔고·날짜를 유지한 채 차트만 새로 시작합니다. 세이브 손실 없음 |
| **C6** | 커밋 분리 | **(A) 멘탈 리밸런싱과 별도 커밋 (권장)** / (B) 함께 | 원인 계통이 완전히 달라 분리를 권합니다 |
| **C7** | `GameManager`의 죽은 `0.666f` | (A) 씬과 같은 `1f`로 정리 / (B) 그대로 | 동작 변화 없음. 순수 혼동 제거 |

C3이 코드로 해결되어 **막고 있는 확인 사항은 없습니다.** 나머지는 전부 권장안대로 진행 가능합니다.

---

## 5. 검증 절차

1. **slot_0 이어하기** → 방 도착 → GameScene 진입.
   - 캔들이 렌더링되는지
   - balance가 40,000으로 표시되는지 (NaN 아님)
   - 콘솔에 `[MarketSimulationEngine] 세이브에 차트 데이터가 없어 정상 초기화로 진행합니다.` 경고가 1회 찍히는지
2. **slot_2 이어하기** (정상 세이브) → 저장된 가격 67,915와 캔들 205개가 그대로 복원되는지.
   **3.1 가드가 정상 세이브의 복원을 막지 않는지 확인하는 회귀 테스트입니다.**
3. 포지션 1회 진입/청산 → PnL·잔고가 유한한 값인지.
4. C3/C4 적용 시 게임 내 1분이 실제 몇 초인지 slot_0과 slot_2에서 동일한지.

## 6. 영향 범위

| 파일 | 변경 |
| --- | --- |
| [MarketSimulationEngine.cs](../../Assets/Scripts/Trading/MarketSimulationEngine.cs) | 복원 가드 1개 + 최저가 방어 1줄 |
| [TradingController.cs](../../Assets/Scripts/Trading/TradingController.cs) | PnL 가드 1줄 |
| [SaveData.cs](../../Assets/Scripts/System/SaveData.cs) · [SaveLoadManager.cs](../../Assets/Scripts/System/SaveLoadManager.cs) | C3/C4 확정 시 시간 배속 센티널 |

세이브 포맷 변경 없음 → **마이그레이터 불필요, 기존 세이브 전부 그대로 사용 가능.**
되돌리려면 해당 커밋만 revert하면 됩니다.
