# 편의점 알바 타이쿤 폐기 — 즉시 정산 알바 전환 계획

- **작성일**: 2026-10-08
- **상태**: 결정 확정(2026-10-08) — D2만 35%로 조정, 나머지는 권장안.
- **진행**: 1단계 ✅ · 2단계 ✅ · 3단계 ✅ · 4단계 — 컴파일 ✅(런타임·에디터 경고·오류 0) / **프리베이크·에디터 검사·플레이 체크리스트는 Unity에서 확인 필요**
- **결정 배경**: 본편 트레이딩이 이미 실시간 조작과 판단을 요구합니다. 그 위에 알바까지 3분짜리 실시간 타이쿤을 요구하면 피로가 겹칩니다. 그래서 타이쿤을 폐기하고, 알바는 **누르면 바로 결과가 뜨는 행동**으로 바꿉니다.
- **폐기 대상 기획서**: [ConvenienceStore_Tycoon_MiniGame_Plan.md](ConvenienceStore_Tycoon_MiniGame_Plan.md)

---

## 0. 요약

| | 지금 (타이쿤) | 바꾼 뒤 |
|---|---|---|
| 흐름 | 월드맵 → 로딩 → 편의점 씬(인트로 3초 + 근무 180초 실시간 조작) → 결과 패널 → 로딩 → 월드맵 | 월드맵에서 **[알바 시작] → 결과 모달 → [확인]** |
| 씬 전환 | 2회 | 0회 |
| 소요 | 약 3분 + 로딩 2회 | 클릭 2번 |
| 일급 | 기본급 ₩1,200에서 감점만큼 깎임 (하한 30%) | 기본급 그대로 (§3 D1) |
| 선물 | 계산한 손님마다 5% (하루 3개 상한) | 근무 1회당 1번 추첨, 35% (§3 D2) |

- **삭제 규모**: 런타임 스크립트 7개 2,308줄, 에디터 도구 3개 212줄, 씬 1개, 스프라이트 36장(6.1MB), 빌드 세팅 1항목.
- **신설 규모(추정)**: `WorldMapManager` 즉시 정산 약 40줄, 결과 모달 UI 약 60줄, 검증 메뉴 1개(기존 테스트에서 2개만 옮김).
- **세이브 형식 변경 없음.**

---

## 1. 현재 구조 (조사 결과)

### 1.1 흐름

1. 월드맵 `[알바 시작]` → [WorldMapUIController.ExecuteSelectedLocation](../../Assets/Scripts/DatingSim/WorldMap/WorldMapUIController.cs#L322) → [WorldMapManager.TryStartPartTimeJob](../../Assets/Scripts/DatingSim/WorldMap/WorldMapManager.cs#L56)
2. 체력 ≥ 20, 슬롯 ≥ 2 검사 → 편의점 씬이 빌드에 있는지 검사 → **슬롯 2·체력 20 차감** → 저장 → `StoreShiftManager.PendingBasePay = 1200` → `LoadingScene` → `ConvenienceStoreScene`
3. 씬은 비어 있고 [DatingSimSceneBuilder.cs:53·59](../../Assets/Scripts/DatingSim/UI/DatingSimSceneBuilder.cs#L53)가 로드 시점에 `ConvenienceStorePrototype.Build`로 통째로 조립합니다.
4. [StoreShiftManager](../../Assets/Scripts/DatingSim/Store/StoreShiftManager.cs) — 인트로 3초 → 근무 180초 → 정산(감점·등급·선물) → `Commit`: [PayWage](../../Assets/Scripts/DatingSim/WorldMap/WorldMapManager.cs#L103) + [GrantItemToSave](../../Assets/Scripts/System/SaveLoadManager.cs#L73) + `StoreTotalShifts++` + 저장 1회
5. [StoreShiftUI](../../Assets/Scripts/DatingSim/Store/StoreShiftUI.cs) 결과 패널 → `ReturnToWorldMap` → 로딩 → 월드맵

### 1.2 정산 규칙 (폐기 대상)

- 기본급 = `PartTimeJobData.rewardAmount` (₩1,200, [DatingSimSceneBuilder.cs:313](../../Assets/Scripts/DatingSim/UI/DatingSimSceneBuilder.cs#L313)). 상한도 기본급입니다.
- 감점: 손님 이탈 60 / 남은 오염 25 / 빈 매대 20 / 두고 간 물건 15. 하한은 기본급 × 0.3. 등급 S~D. 중도 포기 시 0원.
- 선물: 계산 손님마다 5%, 하루 3개 상한, 에너지 드링크·파르페 50:50.
- `SaveData.StoreTotalShifts`: 누적 근무 횟수. 손님 밀도 티어 선택에만 쓰였습니다.

### 1.3 타이쿤을 지워도 남는 것 (공용)

| 요소 | 남는 이유 |
|---|---|
| `WorldMapManager.PayWage` | 즉시 정산이 그대로 씀. 상주 `GameManager`가 있으면 `ChangeBalance`(월드맵의 Paused 상태에서도 허용), 없으면 세이브 스냅샷에 직접 반영 |
| `SaveLoadManager.GrantItemToSave` | 선물 지급 경로 (D2 유지 시) |
| `DatingTimeManager.TryConsumeTimeSlot` / `TryConsumeStamina` | 차감 API (차감과 시계 이동, 저장까지 함) |
| `PartTimeJobData` (`jobName`·`staminaCost`·`rewardAmount`) | 알바 데이터의 유일한 출처 |
| 월드맵 "편의점 알바" 핀·카드 | 알바를 시작하는 곳 |
| `YomiRoomTopDownPrototype` 헬퍼(`CreateBlock`·`CreateCanvas` 등) | 요미의 방 빌더도 씀 |
| `YomiTopDownWalkAnimator`, `YomiWalkSheet.png` | 옛 탑다운 방 빌더가 아직 참조 — 정리는 요미의 방 리뉴얼 Phase 4 몫 (§6) |
| `DatingSim/YomiRoom/UI/Modal/PanelFrame` | 요미의 방 공용 모달 프레임 — 결과 모달도 이걸 씀 |

### 1.4 스토리와의 관계

스토리의 "편의점"은 **장소**입니다. `[편의점-1]`·`[편의점-B]`는 작성됐고 `[편의점-2]`·`[편의점]`은 미작성이며([P2_08_01_Event_Tracker.md](../P2_08_Story_Implementation/P2_08_01_Event_Tracker.md)), 모두 `EventCatalog` 기반의 별개 시스템으로 재생됩니다. **타이쿤과 연결된 데가 없어 영향이 없습니다.** 바뀌는 것은 [P2_08_12](../P2_08_Story_Implementation/P2_08_12_D3_WorldMap_Location_FlowerShop.md) :136의 "장소별 미니게임은 편의점 타이쿤 외에 계획 없음"이라는 서술뿐입니다.

---

## 2. 새 동작 사양

### 2.1 흐름

1. 월드맵에서 편의점 알바를 고르면 카드가 `체력 -20 · 시간 -2 · 일급 ₩1,200`을 보여 줍니다. **이 문구를 `availableJobs[0]`에서 만듭니다** — 지금은 [WorldMapUIController.cs:275](../../Assets/Scripts/DatingSim/WorldMap/WorldMapUIController.cs#L275)에 숫자가 따로 박혀 있어 데이터를 바꾸면 표기가 어긋납니다. 즉시 정산은 "표기 = 실지급"이 전부라 출처를 하나로 맞춥니다.
2. `[알바 시작]` → 검사 실패(체력·슬롯 부족) 시 기존 피드백 "행동 불가: 자원(체력/시간/자금)이 부족합니다."
3. 검사 통과 시 **한 프레임 안에서**: 슬롯 2·체력 20 차감 → 일급 지급 → 선물 추첨 → 누적 근무 +1 → 저장 1회 → `OnJobFinished(결과)` 발행.
4. 결과 모달을 띄우고 `AudioCue.Profit`을 재생합니다. `[확인]`을 누르면 닫히고 월드맵에 그대로 남습니다(남은 슬롯으로 다른 행동 가능).

### 2.2 결과 모달

```
┌─ 편의점 야간 알바 · 근무 완료 ──────────────┐
│  근무 시간    09:00 → 15:00   (시간 -2)       │
│  체력         80 → 60         (-20)           │
│  일급         + ₩1,200                        │
│  선물         에너지 드링크 × 1    ← 있을 때만 │
│  보유 자산    ₩41,200                         │
│  누적 근무    7회차               ← D3         │
│                                 [ 확인 ]      │
└───────────────────────────────────────────────┘
```

- 시각은 `DatingTimeManager.ClockTextForSlots(남은 슬롯)`으로 근무 전·후를 그립니다.
- 저장이 실패하면 한 줄 경고를 덧붙입니다("저장에 실패했습니다. 다음 저장 때 함께 기록됩니다." — 타이쿤 결과 패널과 같은 방침).
- 모달은 화면 전체 입력을 막습니다. 결과를 보는 중에 `[알바 시작]`이 한 번 더 눌려 근무가 연속으로 두 번 처리되는 일을 막기 위해서입니다.
- 월드맵 빌더의 기존 헬퍼(`CreatePanel`·`CreateText`·`CreateStandaloneButton`)와 `PanelFrame`으로 조립하고, 시작할 때는 숨겨 둡니다.

### 2.3 타이쿤 기획서에서 이어받는 원칙

- **기본급 출처는 `PartTimeJobData.rewardAmount` 하나.** 카드 표기, 지급액, 결과 모달이 모두 여기서 나옵니다.
- **알바는 잔고를 깎지 않습니다(R12).** 지급액은 0 이상입니다(`PayWage`가 이미 보장).
- **차감이 지급보다 먼저입니다.** 그 사이에 종료돼도 공짜 일급이 생기지 않습니다.
- **선물은 실력과 무관한 행운입니다.** 지급은 `GrantItemToSave`로 하고, 다음 GameScene 진입 때 인벤토리에 들어갑니다. 첫 거래 진입 전에 받은 선물도 시작 아이템 위에 얹힙니다([SaveLoadManager.cs:562-572](../../Assets/Scripts/System/SaveLoadManager.cs#L562)).
- 타이쿤의 "근무 중 종료 = 근무 소실"(R8)과 "중도 포기 = 0원" 규칙은 대상이 사라집니다. 근무에 중간 상태가 없습니다.

---

## 3. 결정이 필요한 항목

| # | 항목 | 선택지 | 권장 | 근거 |
|---|---|---|---|---|
| **D1** | 일급 | (a) 고정 ₩1,200 · (b) 무작위 편차 · (c) 누적 근무에 따라 인상 | **(a)** | 카드에 적힌 금액이 그대로 들어오는 것이 "피로를 덜자"는 취지에 맞습니다. (b)는 실력과 무관한 감점, (c)는 새 밸런스 설계가 필요합니다. ⚠️ 타이쿤은 감점 때문에 평균이 기본급보다 낮았으므로 (a)는 사실상 **소폭 상향**입니다. 참고로 1,200은 시작 자금 대비 Easy 3% / Normal 6% / Hard 17%입니다 |
| **D2** | 선물 | (a) 근무 1회당 1번 추첨 유지(에너지 드링크·파르페 50:50, 1개) · (b) 폐지 | **(a), 확률 35%로 확정** | 타이쿤의 기대값은 근무당 약 0.5~0.7개(계산 손님 10~14명 × 5%)였습니다. 권장안은 50%였으나 **35%로 축소 확정**(2026-10-08) — 클릭 한 번으로 받는 보상이라 실시간 근무보다 낮게 둡니다. (b)를 고르면 `GrantItemToSave`의 마지막 호출처가 사라집니다. 확률은 상수 하나(`JobGiftChance`)로 조정합니다 |
| **D3** | 누적 근무 `StoreTotalShifts` | (a) 유지 + 결과 모달에 "n회차" 표시 · (b) 필드 삭제 | **(a)** | 세이브 그대로이고, 표시하면 읽는 곳이 생깁니다. 나중에 시급 인상이나 장소 해금 조건 후보로도 쓸 수 있습니다. (b)도 마이그레이션은 필요 없습니다(옛 세이브에 남은 키는 `JsonUtility`가 무시) |
| **D4** | 편의점 스프라이트 36장 | (a) `Icon_Money`·`Icon_Gift` 2장만 `Resources/DatingSim/WorldMap/UI/`로 옮겨 결과 모달에 쓰고 나머지 34장 삭제 · (b) 전부 삭제(글자만) | **(a)** | 옮기는 비용이 2장뿐입니다. `.meta`가 함께 옮겨져 픽셀 임포트 설정이 유지되므로, 전용 임포터(`ConvenienceStoreUIAssetImporter`)를 지워도 됩니다 |
| **D5** | 결과 모달의 요미 반응 대사 | (a) 넣지 않음 · (b) DB 키 `PartTimeJobDone` 하나 연결 | **(a)** | 대사는 지금 0줄이고(2026-10-08 전면 삭제), 매처가 GameScene에만 있어 월드맵에서는 대개 침묵합니다(대사 마스터 문서 YD-11). 대사를 다시 쓸 때 함께 정합니다 |

---

## 4. 작업 단계

### 1단계 — 즉시 정산으로 교체 (타이쿤 파일은 아직 남김)

타이쿤을 남긴 채 기능부터 바꿉니다. 단독으로 검증할 수 있고 되돌리기 쉽습니다. 이 단계에서 `WorldMapManager`의 편의점 참조가 사라지므로, 2단계에서 파일을 지워도 컴파일이 깨지지 않습니다.

| 파일 | 변경 |
|---|---|
| [WorldMapManager.cs](../../Assets/Scripts/DatingSim/WorldMap/WorldMapManager.cs) | `TryStartPartTimeJob`에서 씬 존재 검사·`PendingBasePay`·씬 로드를 걷어내고 즉시 정산. `PartTimeJobResult` 구조체와 `event Action<PartTimeJobResult> OnJobFinished` 추가(타이쿤 도입 때 지운 이벤트의 부활). `StoreSceneName` 상수 삭제 |
| [WorldMapUIController.cs](../../Assets/Scripts/DatingSim/WorldMap/WorldMapUIController.cs) | `OnJobFinished` 구독 → 결과 모달 채우기·표시, `UpdateBalanceUI`, 효과음. 알바 카드 문구를 `availableJobs[0]`에서 생성 |
| [DatingSimSceneBuilder.cs](../../Assets/Scripts/DatingSim/UI/DatingSimSceneBuilder.cs) `BuildWorldMap` | 결과 모달(숨김)을 조립해 `WorldMapUIController`에 넘김 |

즉시 정산 골격:

```csharp
public struct PartTimeJobResult
{
    public string JobName;
    public float Pay;
    public string GiftItemId;          // 없으면 null
    public int SlotsBefore, SlotsAfter;
    public int StaminaBefore, StaminaAfter;
    public int TotalShifts;
    public bool SaveFailed;
}

public event Action<PartTimeJobResult> OnJobFinished;

private const float JobGiftChance = 0.35f;                                  // D2 (확정)
private static readonly string[] JobGiftItemIds = { "energy_drink", "dessert" };

// TryStartPartTimeJob — 기존 사전 검사(체력·슬롯) 뒤
var time = DatingTimeManager.Instance;
var result = new PartTimeJobResult { JobName = job.jobName, Pay = job.rewardAmount,
    SlotsBefore = time.CurrentTimeSlot, StaminaBefore = time.CurrentStamina };

// 차감이 지급보다 먼저 — 사이에 종료돼도 공짜 일급이 생기지 않습니다.
time.TryConsumeTimeSlot(requiredSlots);
time.TryConsumeStamina(job.staminaCost);

PayWage(job.rewardAmount);
var save = SaveLoadManager.Instance;
if (UnityEngine.Random.value < JobGiftChance)
{
    result.GiftItemId = JobGiftItemIds[UnityEngine.Random.Range(0, JobGiftItemIds.Length)];
    save?.GrantItemToSave(result.GiftItemId);
}
if (save?.CurrentData != null) result.TotalShifts = ++save.CurrentData.StoreTotalShifts;
result.SaveFailed = save == null || !save.SaveCurrentGame();   // 일급·선물·누적 근무를 한 번에 확정

result.SlotsAfter = time.CurrentTimeSlot;
result.StaminaAfter = time.CurrentStamina;
OnJobFinished?.Invoke(result);
```

> 매니저는 이벤트만 내고 모달을 모릅니다(레이어링 규칙). 차감 API가 호출마다 저장하므로 디스크 쓰기는 3회이며, 타이쿤 때(차감 2 + 진입 1 + 정산 1)보다 적습니다.

### 2단계 — 타이쿤 삭제

| 대상 | 규모 | 비고 |
|---|---|---|
| `Assets/Scripts/DatingSim/Store/` 6개 (`StoreShiftManager`·`StoreShiftUI`·`StoreConfig`·`StoreCustomer`·`StorePlayerController`·`StoreStation`) | 1,865줄 | 폴더째 (`.meta` 포함) |
| `Assets/Scripts/DatingSim/UI/ConvenienceStorePrototypeBuilder.cs` | 443줄 | |
| `Assets/Editor/ConvenienceStoreSceneBuilder.cs` · `ConvenienceStoreUIAssetImporter.cs` | 65줄 | 메뉴 `FX Overdose/Build Convenience Store Scene` 소멸 |
| `Assets/Editor/ConvenienceStoreTestRunner.cs` | 147줄 | **검사 2개는 옮겨 남깁니다** — 선물 ID가 상점 카탈로그에 있는지(오타 = 조용한 아이템 증발, R7), `GrantItemToSave` 합산. → `PartTimeJobTestRunner` (메뉴 `FXOverdose/Debug/Part-Time Job Test`) |
| `Assets/Scenes/ConvenienceStoreScene.unity` + 빌드 세팅 항목 | — | Build Settings 창에서 제거. 뒤 씬(EventScene)의 인덱스가 당겨지지만 인덱스로 로드하는 곳은 타이틀(0번)뿐이라 무관 |
| `Assets/Resources/DatingSim/Store/` | 36장, 6.1MB | D4에 따라 2장만 이동 |
| [DatingSimSceneBuilder.cs:53·59](../../Assets/Scripts/DatingSim/UI/DatingSimSceneBuilder.cs#L53) | 2줄 | 편의점 씬 분기 |
| `SaveData.StoreTotalShifts` 주석 | — | "손님 스폰 tier 선택에만" → 새 의미로 (D3) |
| 편의점을 언급하는 주석 | — | `DatingTimeManager.cs:158·167·180`, `GameManager.cs:493`, `SaveLoadManager.cs:66·569-570`, `YomiRoomTopDownPrototype.cs:467`, `EventSceneBuilder.cs:14`. 동작과 무관, 문구만 |

### 3단계 — 문서

- **폐기 표시**(상단 배너): [ConvenienceStore_Tycoon_MiniGame_Plan.md](ConvenienceStore_Tycoon_MiniGame_Plan.md), [P2_05_ConvenienceStore_UI_And_Sprite_Spec.md](../P2_05_UI_and_Art/P2_05_ConvenienceStore_UI_And_Sprite_Spec.md), [P2_05_ConvenienceStore_Gemini_Prompt_Sheet.md](../P2_05_UI_and_Art/P2_05_ConvenienceStore_Gemini_Prompt_Sheet.md)
- **갱신**:
  - `CLAUDE.md`·`AGENTS.md` — 빌드 씬 순서에서 `ConvenienceStoreScene` 제거
  - [Refactored_Architecture_Master.md](Refactored_Architecture_Master.md) — 새 절
  - [P2_08_12](../P2_08_Story_Implementation/P2_08_12_D3_WorldMap_Location_FlowerShop.md) :136 — 장소별 미니게임 없음
  - [YomiRoom_PointAndClick_Renewal_Plan.md](YomiRoom_PointAndClick_Renewal_Plan.md) :144·210·230 — "편의점이 사용 중이라 유지"하던 근거가 사라짐
  - [TimeSlot_Clock_Integration_Plan.md](TimeSlot_Clock_Integration_Plan.md) :289 — "알바 씬 로드 실패" 점검 항목 무효

### 4단계 — 검증

- **컴파일**: `dotnet build` 4종. 단 에디터 csproj에는 Unity가 다시 만들 때까지 지운 파일이 남아 있으니, Unity를 한 번 연 뒤 빌드합니다.
- **폰트**: 결과 모달의 새 문구 때문에 `Tools/Prebake All Scripts Text into Font`를 다시 실행합니다.
- **에디터 검사**: `FXOverdose/Debug/Part-Time Job Test`.
- **플레이**:
  - [ ] 체력 < 20 또는 슬롯 < 2 → 실패 피드백, 아무것도 차감되지 않음
  - [ ] 정상 → 결과 모달, 잔고 +1,200, 슬롯 -2, 체력 -20, 시계 +6시간
  - [ ] 결과 직후 종료 → 재접속 시 잔고·슬롯·누적 근무가 유지됨
  - [ ] **GameScene을 거친 세션**(상주 `GameManager` 있음)과 **거치지 않은 세션** 양쪽에서 잔고 반영
  - [ ] 선물 → 다음 거래 진입 시 인벤토리에 들어옴. 1일차에 알바를 먼저 하면 시작 아이템과 선물이 모두 있음
  - [ ] 모달을 띄운 채 `[알바 시작]`을 다시 눌러도 두 번째 근무가 처리되지 않음
  - [ ] 남은 슬롯으로 데이트·방 복귀가 정상 동작

---

## 5. 위험과 대응

| 위험 | 대응 |
|---|---|
| 평균 수입이 오름 (감점 소멸) | D1 메모. 문제가 되면 `rewardAmount` 한 값만 조정 — 카드·지급·모달이 같이 따라갑니다 |
| 아이템 수급 변화 | D2 확률 상수 하나로 조정 |
| 저장 실패 (드묾) | 결과 모달에 경고. 메모리 반영은 유지되어 다음 성공 저장에 딸려 갑니다 |
| 결과 모달이 다른 UI 위·아래로 꼬임 | 모달을 캔버스 최상단에 두고 전면 레이캐스트 차단 |
| 탑다운 방 잔재가 고아처럼 보임 | 이번 범위 밖 — §6 |

---

## 6. 하지 않는 것

- 알바 종류 추가, 장소·해금 시스템 — [P2_08_12](../P2_08_Story_Implementation/P2_08_12_D3_WorldMap_Location_FlowerShop.md) 범위
- 누적 근무에 따른 시급 인상 (D1 (c))
- 요미 반응 대사 (D5)
- 편의점 스토리 이벤트(`[편의점-1]` 등)를 알바와 연결
- `YomiTopDownWalkAnimator`·`YomiWalkSheet.png` 등 탑다운 방 잔재 정리 — 편의점이 사라지면 옛 탑다운 방 빌더만 남은 참조처가 되므로, 요미의 방 리뉴얼 Phase 4에서 함께 정리
