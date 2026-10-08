# 위기 연출 재설계 계획 (초안) — 유동적 시간 연출 철폐

> 상태: **초안 / 미승인**. 작성 2026-09-22.
> 목표: 인게임 시계 배속을 건드리는 "유동적 시간 연출"을 전부 제거하고,
> **플레이어가 위기에 직면했음을 직관적으로 보여주면서 실제로 대처할 수 있는** 연출로 대체한다.
> 선행 작업: [투자 파트 LLM 제거](../P2_03_LLM_Architecture/Trading_LLM_Removal_Plan.md) (2026-09-22 완료)

---

## 1. 현황 조사 결과

### 1.1 "유동적 시간 연출"의 전체 구성

기준값은 `GameScene`에 직렬화된 `secondsPerGameMinute: 0.666`이다 (인게임 1분 = 실시간 0.666초).
이 값을 런타임에 흔드는 주체는 [DynamicTimeRegulator.cs](../../Assets/Scripts/Core/DynamicTimeRegulator.cs) (96줄) 하나뿐이고, `Lerp`로 부드럽게 목표 배속까지 밀어 준다.

| # | 트리거 | 위치 | 효과 |
| --- | --- | --- | --- |
| 1 | 오버도즈 진입 | [TraderStatus.cs:505](../../Assets/Scripts/TraderStatus.cs#L505) | 2.5배 감속 5초 |
| 2 | ROE +50% 돌파 | [TradingController.cs:808](../../Assets/Scripts/Trading/TradingController.cs#L808) | 2.0배 감속 4초 |
| 3 | 청산가 여유 0.5% 미만 | [TradingController.cs:1269](../../Assets/Scripts/Trading/TradingController.cs#L1269) | 3.0배 감속 3초 |
| 4 | 파스타 아이템 | [DeliveryFoodManager.cs:106](../../Assets/Scripts/Items/DeliveryFoodManager.cs#L106) | 180초간 **×1.5 가속** |

### 1.2 ⚠️ 핵심 발견 — 이건 연출이 아니라 기계적 보조다

`secondsPerGameMinute`를 읽는 곳은 시계만이 아니다.

| 소비자 | 위치 | 용도 |
| --- | --- | --- |
| `MarketSimulationEngine` | 516 / 582 / 682 / 904 | **가격·캔들 생성 속도** |
| `TraderStatus` | 343 (`speedScale = 5.0f / secondsPerGameMinute`) | 멘탈·체력 감소 속도 정규화 |

즉 청산 임박 슬로우모션은 화면만 느려지는 게 아니라 **주가가 청산가로 접근하는 속도까지 늦춘다.**
플레이어에게 실시간 반응 시간을 3초 더 주는 **난이도 완화 장치**였고, 이것이 지적된 "시간벌이"의 정체다.

→ **제거하면 반응 창이 같이 사라진다.** 새 설계는 이 반응 창을 어떻게 처리할지 반드시 결정해야 한다(§3).

한편 멘탈 감소는 *게임 분당* 기준으로 정규화되어 있으므로, 기준 배속을 그대로 두는 한 **`verify_mental_balance.py`의 제약은 영향받지 않는다.**

### 1.3 지금 위기는 어떻게 전달되고 있나 (= 왜 직관적이지 않은가)

| 채널 | 현재 상태 | 문제 |
| --- | --- | --- |
| 청산가 | `LIQ: $68,412.3` **텍스트 한 줄** ([TradingPanelUIController.cs:1143](../../Assets/Scripts/UI/Chart/TradingPanelUIController.cs#L1143)) | 현재가와 머릿속으로 뺄셈을 해야 위험을 안다 |
| 차트 | 현재가 라인 + 진입 방향 라인 있음 | **청산가 라인이 없다.** 위기가 차트에 그려지지 않는다 |
| 오디오 | `layer_danger` BGM 크로스페이드 — 이미 연속 `dangerFactor`(0~1) 계산 중 ([AudioManager.cs:410-434](../../Assets/Scripts/System/AudioManager.cs#L410)) | **private이라 시각 연출이 이 신호를 못 쓴다** |
| 시간 | 여유 0.5% 미만에서 3초 슬로우모션 1회 | 0.5%는 **이미 손쓸 수 없는 시점**이다. 경고가 아니라 사망 선고 |
| 상단바 | 손익·바이탈 | 청산 근접도 표시 없음 |

**결론: 위기 신호는 이미 계산되고 있는데 오디오만 쓰고 있다.** 시각 채널은 사실상 비어 있고, 유일한 시각적 강조가 "느려짐"이었다.

### 1.4 제약 조건

- **포스트프로세싱이 꺼져 있다.** `GameScene` 메인 카메라 `m_RenderPostProcessing: 0`.
  URP Volume 기반 비네트·색수차는 렌더 파이프라인 설정 변경과 2D 렌더 비용을 부른다.
  → **캔버스 오버레이 이미지 1장이 압도적으로 싸다.** 이 계획은 포스트프로세싱을 켜지 않는다.
- **범위 밖**: `IsFastForwardingTime` / `AdvanceGameMinutes`는 스킬 공부용 **시간 스킵**(동기 while 루프)이다. 연출이 아니라 게임 기능이므로 건드리지 않는다.
- **범위 밖**: `Time.timeScale`은 설정창·로딩의 일시정지에만 쓰인다. 그대로 둔다.

---

## 2. 설계 원칙

1. **위기 신호는 하나로 통일한다.** 이미 `AudioManager` 안에 있는 `dangerFactor`를 밖으로 꺼내 모든 채널이 같은 값을 읽는다. 채널마다 따로 판정하면 "BGM은 위험한데 화면은 평온"이 생긴다.
2. **시계 배속은 연출 수단에서 완전히 뺀다.** 시뮬레이션 속도와 얽혀 있어 연출 손잡이로 쓰기에 부적합하다.
3. **위기는 거리로 보여준다.** 숫자를 읽고 계산하게 하지 말고, 차트 위의 두 선이 가까워지는 것으로 보여준다.
4. **보여주기만 하면 의미 없다.** 티어가 올라갈 때마다 **지금 누를 수 있는 버튼**이 같이 제시되어야 한다.
5. **경고는 빨라야 한다.** 0.5%가 아니라 **5% 여유**에서 1단계 경계가 시작된다.

---

## 3. ❗ 결정이 필요한 항목 — 반응 창을 어떻게 할 것인가

슬로우모션이 제공했던 "실시간 3초"를 무엇으로 대체할지가 이 재설계의 유일한 갈림길이다.

| 안 | 내용 | 난이도 영향 | 구현 비용 |
| --- | --- | --- | --- |
| **A. 순수 지각적** | 반응 창을 주지 않는다. 경고를 훨씬 일찍(5% 여유) 띄우는 것으로 갈음 | **상승.** 고배율 플레이가 즉사에 가까워진다 | 가장 낮음 |
| **B. 명시적 유예 (권장)** | 임계 티어에서 **플레이어가 직접 누르는** 유예 수단을 준다. 하루 N회 제한 자원. 누르면 그 포지션에 한해 몇 초간 청산 판정을 보류 | 중립. 위기 대처가 **수동적 기다림에서 능동적 조작으로** 바뀐다 | 중간 |
| **C. 시장만 감속** | 시계는 그대로 두고 임계 구간에서 `MarketSimulationEngine`의 변동 폭만 제한 | 현행 유지 | 중간 — **결국 시뮬레이션을 흔드는 같은 문제** |

**B 권장.** 이유:
- 원칙 4를 그대로 만족한다. "위기에 직면했음을 보여주고 대처할 수 있게" 하려면 대처 행동이 있어야 한다.
- 슬로우모션은 플레이어가 아무것도 하지 않아도 알아서 걸렸다. 그래서 **연출로도 약했고 기능으로도 숨어 있었다.**
- 기존 자산 재사용 가능: 이벤트 쉴드(`TradingController.eventProtectionEndTime`)가 이미 "일정 시간 청산 판정 보류" 기구다. 새로 만들 게 아니라 진입점만 열면 된다.

C는 §1.2에서 문제 삼은 결합을 이름만 바꿔 유지하는 안이라 권하지 않는다.

---

## 4. 삭제 목록

**파일 통째로**
- `Assets/Scripts/Core/DynamicTimeRegulator.cs` (96줄) + `.meta`

**호출부**
- `TraderStatus.cs:503-508` — 오버도즈 슬로우모션 블록
- `TradingController.cs:802-811` — ROE +50% 슬로우모션 블록
- `TradingController.cs:1260-1273` — 마진콜 슬로우모션 블록
- 플래그 `isMarginCallSlowMotionTriggered` / `isTargetBreakthroughSlowMotionTriggered` (선언 246-247, 리셋 1218-1219 · 1355-1356)
- `GameManager.EnsureDynamicTimeRegulator()` (678-683) 및 호출 2곳 (561, 629)

**파스타 가속**
- `DeliveryFoodManager.ApplyPastaSpeed()` / `SetFoodTimeMultiplier` 경로
- ⚠️ **파스타 아이템의 효과가 사라진다.** 상점에 남겨 두려면 다른 효과를 붙여야 한다 — §7 참조

**세이브**
- `SaveLoadManager.cs:189-191` — `data.SecondsPerGameMinute` 기록부
- `SaveData.SecondsPerGameMinute` 필드는 **남겨 둔다.** `JsonUtility`는 미사용 키를 무시하므로 마이그레이션이 필요 없고, 지우면 구버전 세이브 호환 검증만 늘어난다.

**남기는 것**
- `GameManager.secondsPerGameMinute` / `SetSecondsPerGameMinute` — 기준 배속은 여전히 설계 상수로 필요하다. `MarketSimulationEngine`·`TraderStatus`가 정규화에 쓴다. **런타임에 이 값을 바꾸는 주체만 없어진다.**

---

## 5. 새 설계 — 위기 게이지 단일화

### 5.1 신설: `CrisisLevelMonitor`

`Assets/Scripts/Trading/CrisisLevelMonitor.cs` (신규, 150줄 내외 예상).
매니저 규약(로직·데이터 보유, `event`로 발행, UI 참조 금지)을 따른다.

```csharp
public float CrisisFactor { get; }                            // 0~1 연속값
public CrisisTier Tier { get; }                               // Calm / Caution / Danger / Critical
public event Action<CrisisTier, CrisisTier> OnTierChanged;    // (이전, 현재)
public event Action<float> OnFactorChanged;
```

**입력 3종** — 셋 중 가장 높은 값을 채택한다.

| 입력 | 계산 | 근거 |
| --- | --- | --- |
| 청산 근접도 | `1 - clamp01(abs(현재가 - 청산가) / 현재가 / 0.05)` | 여유 5%에서 0, 0%에서 1. **가장 직접적인 위기인데 지금 UI가 없다** |
| 미실현 손실 | `clamp01(-ROE / 20)` | `AudioManager`의 기존 계산식 그대로 |
| 멘탈 | `clamp01((0.4 - 멘탈비율) / 0.3)` | `AudioManager`의 기존 계산식 그대로 |

**티어 경계**: `Calm < 0.25 ≤ Caution < 0.55 ≤ Danger < 0.85 ≤ Critical`

`AudioManager`의 `UpdateTension` 내부 계산은 이 모니터를 읽도록 바꿔 **판정을 한 곳으로 모은다.**

### 5.2 채널별 연출 (전부 캔버스 레이어 — 포스트프로세싱 불필요)

| 채널 | Calm | Caution | Danger | Critical |
| --- | --- | --- | --- | --- |
| **차트 청산가 라인** (신설) | 숨김 | 빨간 점선 상시 표시 + `LIQ` 태그 | 선 굵어짐, 현재가 라인과의 간격에 음영 | 간격 음영이 붉게 차오르고 1초 주기 점멸 |
| **화면 테두리** (신설, Image 1장) | α 0 | α 0.15 은은한 붉은 테두리 | α 0.35 | α 0.5 + **심장박동 리듬 2연타 펄스** |
| **상단바 청산 거리** (신설) | 숨김 | `LIQ까지 -4.2%` | 수치 붉게 + 굵게 | 수치 점멸 |
| **오디오** | — | 기존 `layer_danger` (재사용) | 동일, 볼륨 상승 | 티어 진입 시 **단발 경보음** 1회 |
| **요미 반응** | — | 티어 상승 시 1회 대사 | 티어 상승 시 1회 대사 | 티어 진입 시 1회 대사 + `ShowEmotion` |
| **대처 수단** | — | 포지션 카드 강조(어디를 볼지 지시) | **긴급 청산 버튼 노출** | 긴급 청산 + §3-B 유예 수단 활성화 |

**핵심은 첫 행이다.** 차트에 청산가 라인을 긋는 것만으로 "숫자 뺄셈"이 "두 선이 붙는다"로 바뀐다.
이 한 줄이 슬로우모션 3종이 하던 일보다 위기를 더 잘 전달한다.

### 5.3 재사용하는 기존 자산 (새로 만들지 않는다)

| 필요 | 기존 것 |
| --- | --- |
| 위기 강도 신호 | `AudioManager`의 `dangerFactor` 계산식 (410-434) — 모니터로 이관 |
| 위기 BGM | `layer_danger` 크로스페이드 기구 — 그대로 |
| 차트 라인 그리기 | `ChartUIController`의 현재가 라인 / 진입 방향 라인 구현 패턴 (241-289, 360) |
| 긴급 청산 | `TradingController.CloseAllPositions()` |
| 유예(§3-B) | `TradingController.eventProtectionEndTime` — 이벤트 쉴드 기구 |
| 요미 대사 | `YomiDialogueMatcher.GetDialogue(...)` — `mentalState`·`isProfit`·`marginRatio` 조건이 이미 인자로 있다 |
| 요미 표정 | `AIVisualController.ShowEmotion(TraderEmotion, duration)` |
| 경보음 | `AudioCue`에 1종 추가 (`CrisisAlarm`) — `Resources/Audio/SFX/`에 wav만 넣으면 자동 연결 |

---

## 6. 단계별 작업

### 1단계 — 철폐 (반나절)
§4 삭제 목록 실행. 이 단계만 끝내도 프로젝트는 컴파일·플레이 가능하다(위기 연출이 오디오뿐인 상태로 퇴화).
`dotnet build` 런타임·에디터 확인.

### 2단계 — 위기 신호 통일 (1일)
`CrisisLevelMonitor` 신설 → `AudioManager.UpdateTension`이 이 모니터를 읽도록 전환.
**이 시점에서 오디오 동작이 기존과 동일해야 한다** — 청산 근접도 입력이 추가되었으므로 경고가 더 빨리 붙는 것만 차이다.

### 3단계 — 차트 청산가 라인 (1일)
가장 효과가 큰 단일 항목. `ChartUIController`에 `LiquidationPriceLine` 추가.
⚠️ `Assets/Editor/`의 `Build Trading Chart UI` 빌더가 차트 캔버스를 소유하므로, **빌더에도 같이 반영**해야 다음 실행에서 지워지지 않는다.

### 4단계 — 화면 테두리 + 상단바 거리 표시 (1일)
오버레이 `Image` 1장(radial gradient 스프라이트) + `TopStatusBarUIController`에 카드 1개.
스프라이트가 없으면 런타임 생성 텍스처로 폴백.

### 5단계 — 요미 반응 + 경보음 (반나절)
티어 상승 시에만 발화. **스팸 방지가 요점** — 티어가 경계를 오르내리며 진동하지 않도록 히스테리시스(하강은 0.05 낮은 값에서) 필수.

### 6단계 — 대처 수단 (§3 결정에 따라, 1~2일)
긴급 청산 버튼 + B안 채택 시 유예 수단.

### 7단계 — 검증
- `dotnet build` 4종
- `python verify_mental_balance.py` — 기준 배속을 안 건드리므로 통과해야 정상
- `python yomi_dialogue_lint.py` — 요미 대사 추가 시
- 인게임: 고배율 포지션을 청산가까지 밀어 티어 4단계 전이를 눈으로 확인

---

## 7. 부수적으로 정리해야 할 것

- **파스타 아이템의 효과가 사라진다.** 선택:
  (a) 상점에서 제외, (b) 다른 효과로 교체(예: 멘탈 회복), (c) 유예 수단(§3-B)의 충전 아이템으로 전용.
  → **(c) 권장.** 아이템 자산·스프라이트·사용 연출이 이미 다 있고, "시간을 사는 아이템"이라는 정체성이 유지된다.
- `SaveLoadManager.cs:553`의 긴 주석(배속을 복원하지 않는 이유)은 `DynamicTimeRegulator`가 사라지면 전제가 바뀌므로 갱신한다.
- `docs/P2_04_System/Refactored_Architecture_Master.md`에 §15로 기록한다.

---

## 8. 기대 효과

| 항목 | 효과 |
| --- | --- |
| 삭제 | `DynamicTimeRegulator` 96줄 + 호출부 3곳 + 플래그 2개 + 세이브 기록부 |
| 없어지는 개념 | 배속 Lerp, 슬로우모션 코루틴, 음식 배속 배율, 연출이 시뮬레이션 속도를 바꾸는 결합 |
| 위기 경고 시점 | 청산 여유 **0.5% → 5%** (10배 빨라짐) |
| 위기 전달 채널 | 오디오 1종 → 차트·화면·상단바·오디오·요미 **5종 동시** |
| 판정 위치 | `AudioManager` private → `CrisisLevelMonitor` 단일 출처 |
| 플레이어 행동 | 자동으로 걸리는 감속(수동적) → 버튼으로 대처(능동적) |
