# 이벤트 진행 시스템 — 사용 설명서

> **작성일**: 2026-08-15
> **대상**: 이 시스템으로 이벤트를 만들거나 호출하는 사람
> **구현 위치**: `Assets/Scripts/Events/Story/`
> **설계 근거**: [EventScene_System_Plan.md](EventScene_System_Plan.md) v2 — *왜* 이렇게 생겼는지는 그쪽에 있다. 이 문서는 *어떻게 쓰는지*만 다룬다.

---

## 0. 30초 요약

```csharp
// 1) EventCatalog.cs에 이벤트를 쓴다
// 2) 어디서든 한 줄로 재생한다
EventLauncher.Play("EVT_DATE_001");
```

끝이다. 저장·복귀·중복 방지·호스트 선택은 시스템이 한다. **당신이 지켜야 할 것은 세 가지뿐이다.**

1. 이벤트 안에서 `ModifyAffection` 같은 매니저 API를 **직접 부르지 않는다** (호감도는 선택지 데이터에 적는다)
2. `Play()`의 **반환값을 확인한다**
3. 한글 대사를 추가하면 **폰트 프리베이크를 다시 돌린다**

---

## 1. 이벤트 만들기

### 1.1. 데이터를 쓴다

`Assets/Scripts/Events/Story/EventCatalog.cs`에 정적 테이블로 쓴다.

> ⚠️ **ScriptableObject로 만들지 마라.** 폰트 프리베이크가 `.cs` 소스만 스캔한다. 에셋으로 빼면 새 한글이 게임에서 □로 나온다.

```csharp
private static readonly EventDefinition MyEvent = new EventDefinition(
    "EVT_DATE_001",              // 영구 ID. 세이브에 남으므로 재사용 금지
    EventHostKind.Overlay,       // 기본 호스트 (호출자가 덮어쓸 수 있다)
    hideStanding: false,         // CG 안에 요미가 그려져 있으면 true
    nodes: new[]
    {
        // (id, speaker, lines, nextId, backgroundId, emotion, choices)
        new EventNode(0, null, new[] { "※카페 문을 열자 종이 딸랑 울렸다." }, nextId: 1),

        new EventNode(1, "요미", new[]
        {
            "오빠, 여기야 여기!",
            "요미가 자리 맡아 뒀어."
        }, nextId: 2, backgroundId: "EVT_DATE_001/Cafe", emotion: EventEmotion.Joy),

        new EventNode(2, "요미", new[] { "뭐 마실래?" }, nextId: -1,
            choices: new[]
            {
                // (text, affection, reply, nextId, setFlag)
                new EventChoice("네가 고르는 걸로.", 3, "헤헤... 그럼 요미가 정할게!", nextId: 3),
                new EventChoice("아메리카노.", 0, "...재미없어.", nextId: 4, setFlag: "DATE_001_PLAIN")
            }),

        new EventNode(3, "요미", new[] { "오빠는 요미 취향 다 아니까." }, nextId: -1),
        new EventNode(4, "요미", new[] { "쳇. 다음엔 요미가 골라 줄 거야." }, nextId: -1)
    });
```

그리고 **`All` 배열에 추가한다.** 이걸 빼먹으면 `Play()`가 "테이블에 없다"고 거절한다.

```csharp
private static readonly EventDefinition[] All = { Sample, MyEvent };
```

### 1.2. 규칙

| 항목 | 규칙 |
| --- | --- |
| **첫 노드** | 반드시 `Id = 0`. 없으면 재생이 시작되지 않는다 |
| **종료** | `nextId: -1` |
| **지문** | 줄 앞에 `※`. 타자기 없이 즉시 표시되고 화자명이 숨는다 |
| **화자** | `"요미"`면 이름이 분홍색. `null`이면 독백/지문 |
| **대사 줄 수** | 노드당 1~3줄. 줄마다 클릭 한 번 |
| **선택지** | 2~4개. 25자 이내, 반말 ([캐릭터 바이블](../P2_02_Worldbuilding/P2_02_Yomi_Character_Bible.md) 4.6절) |
| **호감도** | `EventChoice.Affection`에만 적는다. 코드로 직접 올리지 않는다 |
| **배경** | `Resources/DatingSim/Events/{BackgroundId}` 에서 스프라이트를 찾는다. 없으면 경고만 남기고 진행한다 |

`Emotion`은 지금 렌더에 쓰이지 않지만 **처음부터 적어라.** 스프라이트 96종이 들어올 때 이벤트 데이터를 전부 다시 손대지 않으려면 지금 넣어야 한다.

### 1.3. 검증한다

**Unity 메뉴 → `FXOverdose/Debug/Validate Event Data`**

플레이 모드 없이 돈다. 잡아 주는 것:

- 노드 ID 중복 (뒤쪽 노드가 영영 재생되지 않는다)
- `Id = 0` 없음
- **끊어진 링크** — 없는 노드를 가리키는 `NextId`
- 대사가 빈 노드
- 어디서도 도달할 수 없는 노드 (경고)

> 이벤트가 길어지면 오타는 **"그 선택지를 고르기 전까지 발견되지 않는"** 형태로 숨는다. 데이터를 고칠 때마다 이 메뉴를 돌려라.

### 1.4. 폰트를 굽는다

**Unity 메뉴 → `Tools/Prebake All Scripts Text into Font`**

한글 대사를 추가/수정했으면 반드시 실행한다. 안 하면 게임에서 □로 나온다.

---

## 2. 이벤트 부르기

### 2.1. 기본

```csharp
using FXOverdose.Events.Story;

if (!EventLauncher.Play("EVT_DATE_001"))
{
    // 재생되지 않았다 — 슬롯을 깎거나 진행도를 올리면 안 된다
    return;
}
```

> ⚠️ **반환값을 무시하지 마라.** `false`인데 진행 상태를 올리면 "이벤트를 안 봤는데 본 것으로 처리되는" 상태가 만들어진다.

### 2.2. 호스트를 고를 때

```csharp
// 오버레이 — 씬 전환 0회. 끝나면 콜백
EventLauncher.Play("EVT_MAIN_007", EventHostKind.Overlay,
    onComplete: () => ResumeSettlement());

// 전용 씬 — 다녀와서 returnScene으로 복귀
EventLauncher.Play("EVT_MAIN_001", EventHostKind.Scene,
    returnScene: "YomiRoomScene");
```

**어느 쪽을 쓸지:**

| 상황 | 호스트 |
| --- | --- |
| 거래 중 / 정산 중 스토리 이벤트 | **오버레이** |
| 방·월드맵의 짧은 데이트 대화 | **오버레이** |
| 프롤로그·엔딩·대형 데이트 (수십 노드 + CG) | **전용 씬** |
| 타이틀 직후 (밑에 깔 화면이 없음) | **전용 씬** |

애매하면 **오버레이**를 써라. 씬을 떠나지 않으므로 잘못될 여지가 적다.

### 2.3. 두 호스트의 차이 — 이것만 알면 된다

|  | 오버레이 | 전용 씬 |
| --- | --- | --- |
| `onComplete` | **호출된다** | **무시된다** (씬이 파괴되어 콜백이 살아남지 못한다) |
| `returnScene` | 무시된다 | **필수** — 생략하면 현재 씬 |
| 복귀 가능한 씬 | (해당 없음) | `YomiRoomScene`, `WorldMapScene` **만** |

> **전용 씬에서 GameScene으로는 못 돌아간다.** 거래 씬 복귀는 `SaveCurrentGame()` → `PrepareLoadGame()` 순서를 지켜야 진행 중인 판이 새 게임으로 초기화되지 않는데, 그 프로토콜을 이벤트 시스템이 한 벌 더 갖는 대신 **거래 중 이벤트는 오버레이로 보내기로** 정했다. 오버레이는 씬을 아예 떠나지 않아 이 문제가 없다.

### 2.4. 조건 판정

```csharp
// 이미 끝까지 본 이벤트인가 (중도 이탈은 기록이 남지 않아 false)
if (EventLauncher.IsCompleted("EVT_MAIN_001")) { ... }

// 이전 이벤트에서 세운 분기 플래그
if (EventLauncher.HasFlag("MAIN_ROUTE_A")) { ... }

// 지금 이벤트가 돌고 있는가
if (EventLauncher.IsRunning) { ... }
```

### 2.5. 진입 비용은 호출자 몫

시간 슬롯 소모·데이트 비용 지불·날짜 진행은 **이벤트 밖에서** 처리한다. 이벤트 러너는 대화만 안다.

```csharp
// 순서: 먼저 재생 가능한지 확인 → 비용 지불 → 재생
if (EventLauncher.IsCompleted(id)) return;
if (!DatingTimeManager.Instance.TryConsumeTimeSlot(1)) return;
EventLauncher.Play(id);
```

---

## 3. 시스템이 알아서 하는 것 (건드리지 마라)

### 3.1. 자동 저장 금지 — 지연 커밋

이벤트가 도는 동안 **디스크 쓰기는 0회**다. 호감도·플래그·선택 이력은 `EventRunner`의 버퍼에 쌓였다가 종료 시점에 한 번에 나간다.

> ⚠️ 그래서 **이벤트 UI나 콜백 안에서 `ModifyAffection`·`TryConsumeStamina` 같은 걸 부르면 안 된다.** 그 메서드들은 그 자리에서 디스크에 쓴다. 부르는 순간 "호감도는 올랐는데 이벤트는 미완료"라는 반쪽 상태가 저장된다.

중도 이탈하면 아무것도 남지 않는다. 이벤트는 처음부터 다시다.

### 3.2. 커밋은 실패할 수 있고, 그래도 결과는 안 사라진다

저장이 막히는 상황이 실제로 있다 — 정산 중(`GameState.Settlement`), 오버도즈 중, 스토리 모드가 아닐 때.

커밋은 2단계다. 1단계는 메모리(`CurrentData`)에 반영하고 **항상 성공**한다. 2단계에서 디스크 쓰기를 1회 시도한다. 실패하면 경고 로그가 뜨지만, 1단계가 남아 있어 **다음 저장에 함께 실려 나간다.** 결과는 사라지지 않는다.

이런 로그가 보이면 정상 동작이다:

```
[EventLauncher] 현재 상태(Settlement)에서는 저장이 막혀 있습니다.
                'EVT_MAIN_007'의 결과는 메모리에 남았다가 다음 저장에 함께 기록됩니다.
```

### 3.3. 그 밖에

- **중복 진입 차단** — 이벤트 중에 다른 이벤트를 부르면 거절된다
- **완료 이벤트 재생 차단** — 이미 본 이벤트는 `Play()`가 `false`
- **정산 충돌 회피** — 24:00 직후나 정산 중에 전용 씬을 요청하면 자동으로 오버레이로 강등한다 (전용 씬으로 가면 그 씬에서 일일 정산이 겹쳐 시작된다)
- **설정 창 제한** — 이벤트 중에는 저장·업적 버튼이 숨는다
- **복귀 지점 기록** — 전용 씬 이벤트 직후 종료해도 `returnScene`으로 재접속한다

---

## 4. 테스트

| 메뉴 | 언제 |
| --- | --- |
| `FXOverdose/Debug/Validate Event Data` | 데이터를 고칠 때마다. 플레이 모드 불필요 |
| `FXOverdose/Debug/Play Sample Event (Overlay)` | 플레이 모드에서 오버레이 확인 |
| `FXOverdose/Debug/Play Sample Event (Scene)` | 플레이 모드에서 전용 씬 확인 |
| `FX Overdose/Build Event Scene` | 최초 1회. 씬 생성 + 빌드 세팅 등록 |
| `Tools/Prebake All Scripts Text into Font` | 한글 대사를 추가한 뒤 |

**같은 이벤트를 두 호스트로 재생해서 결과가 같은지** 확인하는 것이 이 시스템의 핵심 검증이다. 다르면 표현이 갈라진 것이다.

---

## 5. 문제 해결

| 증상 | 원인 |
| --- | --- |
| `Play()`가 `false`를 반환 | 로그를 봐라 — 테이블에 없음 / 이미 완료 / 이벤트 진행 중 / 복귀 씬 불허 중 하나다 |
| 대사가 □로 나온다 | 폰트 프리베이크 미실행 (`Tools/Prebake All Scripts Text into Font`) |
| 이벤트가 시작되자마자 끝난다 | `Id = 0` 노드가 없다. `Validate Event Data`로 확인 |
| 중간에 갑자기 끝난다 | `NextId`가 없는 노드를 가리킨다. 콘솔에 `노드 N을(를) 찾을 수 없습니다` |
| 선택지를 누르려는데 대사가 넘어간다 | 있으면 버그다 — 선택지 표시 중에는 ClickCatcher가 꺼진다 |
| 설정 창을 열었다 닫으니 타자기가 멈춤 | 있으면 버그다 — 이벤트 연출은 전부 unscaled 시간을 쓴다 |
| 이후 모든 이벤트가 "진행 중"으로 막힘 | `EventLauncher.IsRunning`이 안 내려갔다. 호스트가 `NotifyFinished()`를 못 부르고 죽은 경우 |
| 배경이 안 나온다 | `Resources/DatingSim/Events/{BackgroundId}` 경로 확인. 콘솔에 경고가 있다 |
| 새 `.cs`가 컴파일에 안 잡힌다 | csproj가 stale하다. Unity를 다시 열면 재생성된다 |

---

## 6. 파일 지도

| 파일 | 고칠 일 |
| --- | --- |
| `EventCatalog.cs` | **이벤트 대사를 쓸 때. 대부분 여기만 건드린다** |
| `EventDefinition.cs` | 데이터 구조에 필드를 추가할 때 |
| `EventRunner.cs` | 진행 규칙·커밋 규칙을 바꿀 때 |
| `EventView.cs` | 화면 배치·연출을 바꿀 때. **양쪽 호스트가 공유하므로 한 곳만 고치면 된다** |
| `EventOverlayHost.cs` / `EventSceneHost.cs` | 진입·복귀 방식을 바꿀 때 |
| `EventLauncher.cs` | 게이트 조건을 추가할 때 |
| `SaveData.cs` | `EventCompletedIds` / `EventChoiceHistory` / `StoryFlags` |
