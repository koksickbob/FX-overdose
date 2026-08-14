# 세이브 씬 위치 보존 — 계획 및 구조 검토

> **작성일**: 2026-08-14
> **대상 브랜치**: `Dev_koksickbob3`
> **상태**: **구현 완료 / 플레이 검증 대기** (컴파일 통과, 10장 수동 검증 미실시)
> **관련 문서**: [P2_04_Loading_SceneManager.md](P2_04_Loading_SceneManager.md) (씬 전환 규약),
> [SaveData_And_YomiHint_Integrated_Plan.md](SaveData_And_YomiHint_Integrated_Plan.md) (세이브 테이블 원본),
> [Difficulty_System_Plan.md](Difficulty_System_Plan.md) (난이도별 초기 자금),
> [Refactored_Architecture_Master.md](Refactored_Architecture_Master.md) (구조 변경 로그)

---

## 1. 요약

**플레이어가 어느 씬에 있었는지가 세이브에 남지 않는다.** 요미의 방이나 월드맵에서 저장하고 종료해도, 재접속하면 항상 `GameScene`(트레이딩 화면) 한복판에서 시작한다.

원래도 있던 구멍이지만, 이번에 **하루의 시작 지점을 요미의 방으로 옮기면서** 마주칠 확률이 크게 올라갔다. 이제 새 게임도, 매일 아침도 방에서 시작하므로 "방에 있다가 껐다"가 가장 흔한 종료 상황이 된다.

씬 위치 자체를 남기는 일은 작다. 방과 월드맵은 이미 `GameManager`가 없는 씬을 전제로 설계되어 있고(6장), 세이브 스냅샷을 직접 읽는 폴백 경로가 이미 구현되어 있다. **신규 복원 로직은 필요 없다.**

다만 Q1의 "새 게임 1일차 어색함"을 파고든 결과, **새 게임 초기화가 `GameScene`에 갇혀 있다는 구조적 문제**가 드러났고 여기서 기존 버그 두 개가 나왔다(7장). 이번 작업은 그 초기화를 데이터 레벨로 끌어내는 것까지 포함한다.

---

## 2. 확인된 현재 상태

이 절의 내용은 전부 코드에서 직접 확인한 사실이다.

### 2.1. 씬 위치는 저장되지 않는다

[SaveData.cs](../../Assets/Scripts/System/SaveData.cs)에 씬·위치·좌표를 담는 필드가 **하나도 없다** (`scene` 대소문자 무시 검색 결과 0건).

### 2.2. 불러오기는 `GameScene` 고정이다

[MainMenuController.cs:627](../../Assets/Scripts/UI/MainMenuController.cs#L627) 이어하기 경로가 `LoadGameFlow()`를 인자 없이 호출한다. 기본값이 `"GameScene"`이다.

### 2.3. 매니저별 실제 소재 씬

씬 파일의 스크립트 GUID를 직접 대조한 결과다. 이 표가 설계 전체를 지배한다.

| 매니저 | 소재 씬 | 수명 |
| --- | --- | --- |
| `SaveLoadManager` | `TitleScene` | `DontDestroyOnLoad` — 전 구간 생존 |
| `GameManager` | **`GameScene` 단독** | 씬 스코프 — 방/월드맵엔 **없음** |
| `DatingTimeManager` | `YomiRoomScene`, `WorldMapScene` | `DontDestroyOnLoad` |
| `YomiRoomManager` | `YomiRoomScene` | 씬 스코프 |

### 2.4. 저장은 방·월드맵에서 활발히 일어난다

- [DatingTimeManager.cs](../../Assets/Scripts/DatingSim/Core/DatingTimeManager.cs) — 시간 슬롯을 소모할 때마다 (7곳)
- [YomiRoomManager.cs](../../Assets/Scripts/DatingSim/YomiRoom/YomiRoomManager.cs) — 씬 전환·휴식·대화 종료 시 (5곳)
- [WorldMapManager.cs](../../Assets/Scripts/DatingSim/WorldMap/WorldMapManager.cs) — 알바 보상·데이트 비용 처리 시 (2곳)

즉 "방에서 저장된 세이브"는 예외가 아니라 **정상 상태**다.

### 2.5. 새 게임 초기화는 `GameScene`에 갇혀 있다

난이도별 초기 자금과 시작 아이템 지급이 전부 [GameManager.StartNewGame()](../../Assets/Scripts/GameManager.cs#L204) 안에 있다. `GameManager`는 `GameScene`에만 있으므로, **방에서 시작하는 스토리 모드는 플레이어가 거래를 개시할 때까지 초기화되지 않은 상태로 굴러간다.** 7장의 버그 두 개가 전부 여기서 나온다.

---

## 3. 문제의 정확한 형태

```
[요미의 방에서 시간 슬롯 소모 → 자동 저장] → 게임 종료
                                                  │
                                       재접속 → 타이틀 → 이어하기
                                                  │
                                                  ▼
                            GameScene, 저장 당시 시각의 트레이딩 화면
                            (방에서 아직 쓰지 않은 시간 슬롯은 그대로 남아 있음)
```

데이터가 손상되지는 않는다. 시간 슬롯·호감도는 `SaveData`에 정상 보존된다. **위치만 틀린다.** 다만 그 결과 플레이어는 "아직 방에서 할 일이 남았는데 거래 화면에 떨어지는" 상태가 되고, 방으로 돌아갈 정규 경로가 없다(방 복귀는 일일 정산 이후에만 일어난다 — [GameManager.ReturnToYomiRoomForNewMorning()](../../Assets/Scripts/GameManager.cs#L837)).

---

## 4. 확정된 결정

| # | 질문 | 결정 |
| --- | --- | --- |
| Q1 | 새 게임 초기화 전에 방에서 종료한 세이브의 목적지 | **A안 채택** — 단, 어색함을 남기지 말고 해소한다 → 5.5절 |
| Q2 | 월드맵 복귀 포함 여부 | **B안 채택** — 3개 씬 전부 포함. 씬이 늘거나 바뀔 수 있으므로 **목록을 한 곳에 모아 확장 가능하게** → 5.2절 |
| Q3 | 문서 갱신 범위 | *미회신 — 기본안 적용*: `Refactored_Architecture_Master.md`에 씬 위치 보존과 새 게임 시작 지점 변경을 **함께** 기록 |

### Q1의 어색함을 어떻게 없앴는가

A안은 "미초기화 세이브는 `LastSceneName`을 무시하고 `GameScene`으로 보낸다"였다. 그러면 새 게임 1일차에 방에서 껐다 켠 플레이어만 거래 화면에서 시작하는 예외가 남는다.

이 예외의 뿌리는 **초기화가 `GameScene`에만 있다는 것**이다. 초기화를 새 게임 준비 시점으로 끌어내면(5.5절) 방에 있는 동안에도 데이터가 이미 완성되어 있으므로, **A안의 예외 분기 자체가 필요 없어진다.** `LastSceneName`을 그대로 존중해 방으로 복귀시켜도 된다.

즉 A안의 의도(잘못된 자산을 화면에 띄우지 않는다)는 지키되, 그 수단을 "예외적으로 거래 화면으로 보낸다"에서 "애초에 자산을 올바르게 심는다"로 바꾼다. 예외 분기가 사라지므로 코드도 더 줄어든다.

---

## 5. 설계

### 5.1. 저장 테이블 변경 — 필드 2개

[SaveData.cs](../../Assets/Scripts/System/SaveData.cs), `IsTutorialCompleted` 인근:

```csharp
// 재접속 시 복귀할 씬. 빈 문자열이면 GameScene(기존 동작).
public string LastSceneName = "";

// 시작 아이템(에너지드링크·파르페 5개, 약품 2개)을 아직 못 받았음.
// 아이템 구성의 출처가 GameScene에 배치된 ItemData 슬롯이라 데이터만으로는 심을 수 없어,
// 복원 경로가 이 플래그를 보고 Inventory.ResetForNewGame()을 대신 호출한다. (5.5절)
public bool NeedsStartingItems = false;
```

두 필드 모두 **기본값이 곧 구버전 호환**이다(`""` = GameScene, `false` = 이미 지급됨). `SaveDataMigrator`는 건드리지 않는다.

동시에 선행 작업에서 넣었던 `NeedsNewGameInit`은 **삭제한다** (5.6절).

### 5.2. 복귀 허용 씬 목록 — 확장 가능하게 (Q2)

씬 이름을 `if`문에 흩뿌리지 않고 한 곳에 모은다. [SaveLoadManager](../../Assets/Scripts/System/SaveLoadManager.cs)에 배치한다 — "이 세이브가 어디로 복귀하는가"는 세이브의 관심사이고, 저장하는 쪽(`SaveGame`)과 불러오는 쪽(`MainMenuController`)이 같은 판정을 공유해야 하기 때문이다.

```csharp
/// <summary>재접속 시 복귀를 허용하는 씬. 복귀 가능한 씬이 늘어나면 이 배열에만 추가한다.</summary>
public static readonly string[] ResumableScenes = { "GameScene", "YomiRoomScene", "WorldMapScene" };

public static bool IsResumableScene(string sceneName)
    => !string.IsNullOrEmpty(sceneName) && Array.IndexOf(ResumableScenes, sceneName) >= 0;
```

`ScriptableObject` 설정 에셋까지 가지 않는다. 씬 목록은 빌드 세팅과 함께 움직이는 코드 상수이고, 지금은 3개다. 인스펙터 노출이 실제로 필요해지면 그때 올린다.

목록에 없는 씬(`tutorial`, `LoadingScene`, `EventScene` 등)에서 저장하면 **이전 값을 유지**한다. 덮어쓰지 않는다. 이 규칙이 없으면 튜토리얼 중 저장 → 재접속 시 튜토리얼이 다시 재생된다.

### 5.3. 수집 — `SaveGame()` 한 곳

[SaveLoadManager.SaveGame()](../../Assets/Scripts/System/SaveLoadManager.cs#L53)의 `data.IsTutorialCompleted = ...` 직후:

```csharp
string activeScene = SceneManager.GetActiveScene().name;
if (IsResumableScene(activeScene))
    data.LastSceneName = activeScene;
```

### 5.4. 불러오기 — 목적지 분기

[MainMenuController.cs:625](../../Assets/Scripts/UI/MainMenuController.cs#L625) 이어하기 경로:

```csharp
if (!SaveLoadManager.Instance.PrepareLoadGame(slotIndex)) return;
LoadGameFlow(SaveLoadManager.Instance.CurrentData?.LastSceneName);
```

`LoadGameFlow()`는 이미 목적지 인자를 받게 고쳐져 있다. 여기에 가드만 더한다:

```csharp
if (!SaveLoadManager.IsResumableScene(targetScene) || !Application.CanStreamedLevelBeLoaded(targetScene))
    targetScene = "GameScene";
```

빈 문자열(구버전), 빌드에서 빠진 씬, 목록에서 제거된 씬이 전부 이 한 줄에 걸린다.

### 5.5. 새 게임 초기화를 데이터 레벨로 (Q1 해소)

[SaveLoadManager.PrepareNewGame()](../../Assets/Scripts/System/SaveLoadManager.cs#L371), `CurrentData = new SaveData();` 직후:

```csharp
// 스토리 모드는 요미의 방에서 시작하는데 GameManager는 GameScene에만 있다.
// 초기 자금을 GameScene 진입까지 미루면 방에서 기본값 1000달러가 보이고,
// 그 사이 벌어들인 알바 수익이 나중에 StartNewGame()에 덮여 사라진다. (7.1절)
if (CurrentGameMode == GameMode.Story)
{
    float startingBalance = StoryDifficultyTables.Get(CurrentStoryDifficulty).StartingBalance;
    CurrentData.Balance = startingBalance;
    CurrentData.StartOfDayEquity = startingBalance;
}
CurrentData.NeedsStartingItems = true;
```

`StoryDifficultyTables.Get()`은 순수 static이라 씬 의존이 없다 ([StoryDifficulty.cs:26](../../Assets/Scripts/System/StoryDifficulty.cs#L26)). 날짜·시각은 `SaveData` 기본값이 이미 1일차 09:00이고, 레벨·기억·코스튬·액티브 아이템도 기본값이 곧 초기 상태라 따로 심을 것이 없다.

**시작 아이템만 예외다.** [Inventory.ResetForNewGame()](../../Assets/Scripts/Items/Inventory.cs#L84)은 씬에 배치된 `ItemData` 에셋의 **이름을 부분 문자열로 매칭**해 수량을 정한다(`energydrink`/`dessert`/`parfait` → 5, `sedative`/`supplement` → 2). 에셋 없이는 재현할 수 없다. 그래서 아이템 ID를 문서·코드에 복제하지 않고, **복원 경로가 이 메서드를 대신 부르게** 한다. [ApplyLoadedDataToGame()](../../Assets/Scripts/System/SaveLoadManager.cs#L481)의 인벤토리 블록:

```csharp
if (inventory != null && shopManager != null)
{
    if (CurrentData.NeedsStartingItems)
    {
        // 새 게임의 첫 GameScene 진입. 저장된 목록(비어 있음) 대신 시작 지급분을 넣는다.
        inventory.ResetForNewGame();
        CurrentData.NeedsStartingItems = false;
    }
    else
    {
        // 기존 ID 기반 복원 (변경 없음)
    }
}
```

### 5.6. `NeedsNewGameInit` 삭제

선행 작업에서 넣은 이 플래그의 유일한 역할은 "초기화되지 않은 새 게임을 불러오기로 오인하지 않게 막는 것"이었다. 5.5절로 **데이터가 준비 시점에 완성되므로 오인할 상태 자체가 없어진다.** 함께 지운다:

- `SaveData.NeedsNewGameInit` 필드
- `GameManager.Start()`의 `shouldRestore` 분기 → 원래의 `if (saveManager != null && saveManager.IsPendingLoad)`로 복귀
- `GameManager.StartNewGame()`에 넣었던 플래그 해제 + `ClearPendingLoad()` 호출
- `SaveLoadManager.ClearPendingLoad()` 메서드 (호출자가 없어짐)

`StartNewGame()` 자체는 남는다. 무한·챌린지 모드는 세이브를 쓰지 않아 `IsPendingLoad`가 항상 false이고, 이 경로로 초기화된다.

### 5.7. 씬 도착 시점에 복귀 지점을 남긴다 — **구현 중 추가**

계획 단계에서 놓쳤던 구멍이다. `LastSceneName`은 "저장이 일어난 씬"을 기록하는데, **씬에 도착하는 것만으로는 저장이 일어나지 않는다.** 방의 저장은 전부 행동(대화 종료·시간 슬롯 소모·씬 전환)에 붙어 있다.

그래서 이런 경우가 그대로 남는다:

```
일일 정산 (GameScene에서 자동 저장 → LastSceneName = "GameScene")
   → ReturnToYomiRoomForNewMorning() → 방 도착
   → 아무것도 안 하고 종료
   → 재접속 시 GameScene. 아침의 방을 건너뛴다.
```

**이것이 가장 흔한 경우다 — 매일 아침이 여기 해당한다.** 튜토리얼 종료 직후(`tutorial` 씬에서 저장 → 목록에 없어 이전 값 유지)도 같은 형태다.

씬 도착 자체를 기록으로 만든다. `SaveGame()`이 현재 씬을 `LastSceneName`에 찍으므로(5.3절) **도착 시 한 번 저장하는 것만으로 복귀 지점이 갱신된다.** 별도의 스탬프 코드가 필요 없다.

- [YomiRoomManager.Start()](../../Assets/Scripts/DatingSim/YomiRoom/YomiRoomManager.cs#L75) — `SaveCurrentGame()` 1줄
- [WorldMapManager](../../Assets/Scripts/DatingSim/WorldMap/WorldMapManager.cs) — `Start()` 신설, 같은 1줄

호출자마다 고치지 않고 **각 씬이 자기 도착을 스스로 기록**하게 한 것이라, 방으로 오는 경로가 몇 개든(새 게임·튜토리얼 종료·아침 복귀·월드맵 귀환) 전부 덮인다. 앞으로 경로가 늘어도 마찬가지다.

부수 효과로, 스토리 새 게임의 슬롯 파일이 **방 도착 시점에 생성된다.** 종전에는 `StartNewGame()`이 `GameScene`에서 만들었는데 5.6절로 그 경로가 사라졌으므로, 이 저장이 그 역할을 대신한다.

### 5.8. `IsTutorialCompleted` 복원 시점 — **구현 중 추가**

`SaveLoadManager.IsTutorialCompleted`는 `SaveData`가 아니라 **매니저의 런타임 프로퍼티**이고, 복원은 `ApplyLoadedDataToGame()`에서만 일어났다. 방으로 바로 복귀하면 그게 돌지 않는다. 그런데 5.7절로 방은 도착하자마자 저장하고, `SaveGame()`은 `data.IsTutorialCompleted = this.IsTutorialCompleted`를 쓴다 → **완료된 튜토리얼이 false로 덮여 다시 재생된다.**

복원을 `PrepareLoadGame()`으로 옮겼다(중복이 아니라 이동 — `ApplyLoadedDataToGame()`은 항상 `PrepareLoadGame()` 뒤에만 실행된다). 2일차 이상 백필 로직도 함께 옮겼다.

같은 프로퍼티에서 **기존 버그도 하나 나왔다.** `DontDestroyOnLoad`라 이전 판의 값이 남는데 `PrepareNewGame()`이 내리지 않아, **한 세션에서 게임을 끝낸 뒤 새로 시작하면 튜토리얼이 통째로 스킵됐다.** `PrepareNewGame()`에 `IsTutorialCompleted = false` 한 줄을 넣었다.

### 5.9. 부수 수정 — `startingBalance` 복원 (7.2절 버그)

`ApplyLoadedDataToGame()`의 `GameManager` 리플렉션 블록에 한 줄 추가한다. 이미 같은 방식으로 private 필드를 쓰고 있다.

```csharp
if (CurrentGameMode == GameMode.Story)
    gmType.GetField("startingBalance", NonPublic | Instance)
          ?.SetValue(gm, StoryDifficultyTables.Get(CurrentStoryDifficulty).StartingBalance);
```

---

## 6. 씬별 복원 책임 — **새로 만들 것이 없다**

이것이 이번 검토의 핵심 결론이다. 처음에는 "방에는 `GameManager`가 없으니 `ApplyLoadedDataToGame()`이 안 돌아 복원 경로를 통째로 새로 짜야 한다"고 봤으나, **틀린 판단이었다.** 방과 월드맵은 애초에 `GameManager` 없는 환경을 전제로 작성되어 있다.

| 방에서 필요한 상태 | 복원 주체 | 현재 상태 |
| --- | --- | --- |
| 호감도·집착도·체력·시간 슬롯·일차 | `DatingTimeManager.LoadFromSaveData()` | ✅ `PrepareLoadGame()`에서 호출 중 (선행 작업에서 추가) |
| 대화 진행도 (`Talk*` 전 필드) | `YomiRoomManager.ProgressData` | ✅ `SaveLoadManager.CurrentData`를 **매번 직접 읽음** — 복원 개념 자체가 없음 |
| 보유 자산 표시 | `WorldMapUIController.UpdateBalanceUI()` | ✅ `GameManager`가 null이면 `CurrentData.Balance`로 폴백 ([:256](../../Assets/Scripts/DatingSim/WorldMap/WorldMapUIController.cs#L256)) |
| 알바 보상·데이트 비용 정산 | `WorldMapManager` | ✅ `GameManager`가 null이면 `CurrentData.Balance`를 직접 갱신 후 즉시 저장 ([:82](../../Assets/Scripts/DatingSim/WorldMap/WorldMapManager.cs#L82)) |
| 잔고·차트·포지션·인벤토리 등 트레이딩 일체 | `ApplyLoadedDataToGame()` | ✅ `IsPendingLoad`가 켜진 채 유지되므로, 플레이어가 거래를 개시해 `GameScene`에 들어가는 시점에 정상 실행 |

마지막 줄이 중요하다. 방으로 바로 복귀해도 `IsPendingLoad`를 끄는 코드가 없으므로 예약이 살아 있고, [YomiRoomManager.StartTrading()](../../Assets/Scripts/DatingSim/YomiRoom/YomiRoomManager.cs#L467)이 다시 `저장 → PrepareLoadGame` 순서를 밟은 뒤 `GameScene`에 진입하면 그때 전체 복원이 일어난다. **트레이딩 상태는 늦게 복원될 뿐, 유실되지 않는다.**

---

## 7. 이 설계가 함께 잡는 기존 버그

Q1을 파고들다 발견한 것으로, 씬 위치와는 별개지만 원인이 같다(2.5절 — 초기화가 `GameScene`에 갇혀 있다). 둘 다 코드로 확인했고 아직 재현 테스트는 하지 않았다.

### 7.1. 1일차 알바 수익이 거래 개시 시점에 증발한다

```
새 게임 → 방 → 월드맵 → 알바 (GameManager가 null이라 CurrentData.Balance에 직접 합산 후 저장)
   → 방 → 거래 개시 → GameScene → StartNewGame() → currentBalance = startingBalance
   → 벌어둔 알바 수익 소멸. 이어서 SaveCurrentGame()이 그 상태를 디스크에 확정.
```

`StartNewGame()`이 잔고를 무조건 초기값으로 되돌리기 때문이다. 5.5절로 초기화가 준비 시점에 끝나면 `StartNewGame()`이 스토리 모드에서 아예 실행되지 않으므로 사라진다.

### 7.2. 이어하기 후 누적 수익률(P&L)이 잘못 표시된다

[TopStatusBarUIController.UpdatePnLUI()](../../Assets/Scripts/UI/TopBar/TopStatusBarUIController.cs#L236)는 `gameManager.StartingBalance`를 수익률 계산의 분모로 쓴다. 그런데 `ApplyLoadedDataToGame()`은 이 필드를 복원하지 않으므로, 이어하기로 진입하면 **인스펙터 기본값 7000이 그대로 분모가 된다.** 난이도별 초기 자금이 7000이 아닌 세이브는 수익률이 전부 틀리게 나온다.

**이것은 이번 변경과 무관하게 지금도 발생하는 버그다.** 5.9절 한 줄로 고쳤다.

### 7.3. 한 세션에서 새 게임을 다시 시작하면 튜토리얼이 스킵된다

`SaveLoadManager.IsTutorialCompleted`가 `DontDestroyOnLoad` 프로퍼티인데 `PrepareNewGame()`이 내리지 않아, 게임을 한 번 끝낸 세션에서 새 게임을 시작하면 `TutorialManager`가 완료 상태로 판단해 튜토리얼을 건너뛴다. 이것도 기존 버그이며 5.8절에서 함께 고쳤다.

---

## 8. 엣지 케이스

| # | 상황 | 처리 |
| --- | --- | --- |
| E1 | 구버전 세이브 | `LastSceneName == ""` → `GameScene`. 기존과 완전히 동일 |
| E2 | 씬 이름이 바뀌거나 빌드 설정에서 빠짐 | `IsResumableScene` + `CanStreamedLevelBeLoaded` 가드로 `GameScene` 폴백 (5.4) |
| E3 | `tutorial`·`LoadingScene`에서 저장 | 목록에 없어 이전 값 유지 (5.2). 이후 방에 도착하면 5.7절 저장이 갱신한다 |
| E4 | 새 게임 1일차에 방에서 종료 후 재로드 | **방으로 복귀한다.** 5.5절로 잔고가 이미 올바르고, 시작 아이템은 `NeedsStartingItems`가 살아 있어 첫 `GameScene` 진입 때 지급된다 |
| E5 | 오버도즈·정산 중 | 애초에 저장이 거부되므로(`SaveGame` 초반 가드) 해당 없음 |
| E6 | 방에서 대화 진행 중 종료 | `Talk*`가 `CurrentData`에 그대로 있고 방이 직접 읽으므로 대화 중간부터 이어짐 |
| E7 | 월드맵에서 종료 | Q2 B안에 따라 **월드맵으로 복귀**. 알바·데이트 정산은 그 자리에서 즉시 저장되므로 중간 상태가 남지 않는다 |
| E8 | 무한·챌린지 모드 | 저장 자체를 지원하지 않아(`AllowsSaving`) 해당 없음. `StartNewGame()` 경로도 그대로 유지. 5.7절 저장도 `AllowsSaving` 가드에 걸려 무시된다 |
| E9 | 아침에 방으로 나오자마자 종료 | 5.7절로 도착 즉시 복귀 지점이 갱신되어 **방으로 복귀**한다 |
| E10 | 요미 방 단독 재생(`YomiRoom_Test`) | `SaveLoadManager.Instance`가 null이라 5.7절 저장이 `?.`에서 멈춘다. 기존 `scratchData` 경로 그대로 |

---

## 9. 선행 변경 (이미 완료·검증됨)

이 계획의 전제가 된 변경이다. **플레이 검증 완료** — 새 게임 → 방 → 거래 개시 시 난이도별 초기 자금과 시작 아이템이 정상 적용되는 것을 확인했다.

| 파일 | 변경 | 이번 작업에서 |
| --- | --- | --- |
| [MainMenuController.cs](../../Assets/Scripts/UI/MainMenuController.cs) | `LoadGameFlow()`에 목적지 인자 추가(기본값 `GameScene`). 스토리 새 게임은 `YomiRoomScene`으로 | 유지 + 5.4절 |
| [TutorialManager.cs](../../Assets/Scripts/System/TutorialManager.cs) | `EndTutorial()` 목적지를 `GameScene` → `YomiRoomScene` | 유지 |
| [SaveLoadManager.cs](../../Assets/Scripts/System/SaveLoadManager.cs) | `PrepareLoadGame()`이 `DatingTimeManager`를 즉시 주입 | 유지 (6장의 전제) |
| [SaveData.cs](../../Assets/Scripts/System/SaveData.cs) | `NeedsNewGameInit` 플래그 신설 | **삭제** (5.6절) |
| [GameManager.cs](../../Assets/Scripts/GameManager.cs) | 복원/새 게임 분기가 `NeedsNewGameInit`을 함께 판정 | **되돌림** (5.6절) |
| [SaveLoadManager.cs](../../Assets/Scripts/System/SaveLoadManager.cs) | `ClearPendingLoad()` 신설 | **삭제** (5.6절) |

`NeedsNewGameInit`은 증상(초기화 오인)을 막는 장치였고, 5.5절은 원인(초기화가 `GameScene`에 갇혀 있음)을 없앤다. 원인이 사라지면 장치는 불필요하므로 지운다. **선행 변경을 폐기하는 게 아니라, 임시 방편을 정식 해법으로 교체하는 것이다.**

---

## 10. 검증 절차

구현 후 아래를 수동으로 확인한다. 자동 테스트는 `FXOverdose.P2P.Core` 밖이라 붙일 수 없다.

**씬 위치**

1. **구버전 세이브** — 이번 변경 전에 만든 세이브로 이어하기 → `GameScene` 진입, 잔고·포지션·인벤토리 정상
2. **방에서 종료** — 방에서 시간 슬롯 1개 소모 → 종료 → 이어하기 → **방으로 복귀**, 남은 슬롯·호감도 일치
3. **방 복귀 후 거래** — 2번에 이어 거래 개시 → `GameScene`에서 잔고·차트·포지션이 저장 시점과 일치
4. **월드맵에서 종료** — 월드맵에서 알바 후 종료 → 이어하기 → **월드맵으로 복귀**, 잔고에 보상 반영
5. **대화 중간 종료** — 방에서 선택형 대화 진행 중 종료 → 이어하기 → 대화 진행도 유지
6. **튜토리얼 직후 종료** — 튜토리얼 완료 → 방 진입 직후 종료 → 이어하기 → **방으로 복귀** (튜토리얼 재생 안 됨)
6-1. **아침 방에서 즉시 종료 (5.7절)** — 하루를 마감해 아침 방으로 나온 직후 종료 → 이어하기 → **방으로 복귀**

**새 게임 초기화 (5.5절)**

7. **난이도별 초기 자금** — 난이도를 바꿔 새 게임 3회 → **방에 들어선 시점부터** 자산이 해당 난이도 값
8. **시작 아이템** — 새 게임 → 방 → 거래 개시 → 에너지드링크·파르페 5개, 약품 2개 정상
9. **1일차 방에서 종료 후 재로드 (E4)** — 새 게임 → 방에서 바로 종료 → 이어하기 → 방으로 복귀, 자산 정상 → 거래 개시 후 시작 아이템 정상
10. **알바 수익 보존 (7.1절)** — 새 게임 → 월드맵 알바 → 거래 개시 → **알바 수익이 잔고에 남아 있을 것**
11. **P&L 기준값 (7.2절)** — 7000이 아닌 난이도로 진행 후 이어하기 → 누적 수익률이 올바른 분모로 표시
12. **무한·챌린지 모드** — 두 모드 새 게임 → 초기 자금·아이템 정상 (`StartNewGame()` 경로 회귀 확인)
13. **튜토리얼 스킵 회귀 (7.3절)** — 한 세션 안에서 게임 종료 후 새 게임 시작 → **튜토리얼이 정상 재생될 것**
14. **세이브 왕복 검사** — 에디터 메뉴 `FXOverdose/Debug/세이브 왕복(Round-trip) 검사` 실행 → orphan 0건 유지 확인

14번은 새 필드가 필드 커버리지 감사에 걸리지 않는지 보는 것이다. 두 필드 모두 `SaveLoadManager.cs`에서 이름으로 언급되고 그 파일은 이미 감사 소스 목록에 있으므로 통과가 예상되지만, 실행해 확인한다.

---

## 11. 실제 변경 내역

| 파일 | 내용 |
| --- | --- |
| [SaveData.cs](../../Assets/Scripts/System/SaveData.cs) | `LastSceneName`·`NeedsStartingItems` 추가, `NeedsNewGameInit` 삭제 |
| [SaveLoadManager.cs](../../Assets/Scripts/System/SaveLoadManager.cs) | `ResumableScenes`+`IsResumableScene` / `SaveGame` 씬 수집 / `PrepareNewGame` 시딩·튜토리얼 플래그 리셋 / `PrepareLoadGame` 튜토리얼 플래그 복원 / 시작 아이템 분기 / `startingBalance` 복원 / `ClearPendingLoad` 삭제 |
| [GameManager.cs](../../Assets/Scripts/GameManager.cs) | `Start()` 복원 분기를 `IsPendingLoad` 단독 판정으로 되돌림, `StartNewGame()`의 플래그 처리 제거 |
| [MainMenuController.cs](../../Assets/Scripts/UI/MainMenuController.cs) | 이어하기가 `LastSceneName`으로 복귀, `LoadGameFlow` 가드 |
| [YomiRoomManager.cs](../../Assets/Scripts/DatingSim/YomiRoom/YomiRoomManager.cs) | `Start()`에서 도착 저장 (5.7) |
| [WorldMapManager.cs](../../Assets/Scripts/DatingSim/WorldMap/WorldMapManager.cs) | `Start()` 신설, 도착 저장 (5.7) |

마이그레이터와 신규 복원 로직은 예상대로 **손대지 않았다.** 계획 대비 늘어난 것은 5.7·5.8절 두 건으로, 둘 다 구현 중 발견한 구멍이다.

`Assembly-CSharp` / `Assembly-CSharp-Editor` 컴파일 통과 (오류 0, 경고 0). **10장 수동 검증 14건은 아직 실시하지 않았다.**
