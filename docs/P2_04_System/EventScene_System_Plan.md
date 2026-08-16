# 이벤트 진행 시스템(EventSystem) — 계획 및 구조 검토 (v2)

> **작성일**: 2026-08-14 / **개정**: 2026-08-15 (v2 — 오버레이·전용 씬 2호스트 구조로 재구성)
> **대상 브랜치**: `Dev_koksickbob3`
> **상태**: 계획 확정 / 구조 검토 2회 완료 / 구현 착수 가능
> **관련 문서**: [YomiRoom_ChoiceTalk_System_Plan.md](YomiRoom_ChoiceTalk_System_Plan.md) (선택형 대화 규격의 원본),
> [P2_02_Yomi_Character_Bible.md](../P2_02_Worldbuilding/P2_02_Yomi_Character_Bible.md) (대사 규격),
> [P2_05_Prologue_Cutscene_Prompt_Sheet.md](../P2_05_UI_and_Art/P2_05_Prologue_Cutscene_Prompt_Sheet.md) (첫 적용 사례 `EVT_MAIN_001`)

> **v1 대비 변경 요지**
> 1. 전용 씬 단일 구조 → **코어 1개 + 표현 호스트 2개**(오버레이 / 전용 씬)로 재구성. 파일명은 유지하되 시스템 이름은 `EventScene`이 아니라 **`EventSystem`**이다.
> 2. 실측으로 무너진 계획 3건 정정 (8절). 재사용한다던 자산 중 둘은 지금 형태로 못 쓴다.
> 3. **2단계 커밋** 도입 — 저장이 조용히 실패하는 3경로를 발견해서, 실패해도 결과가 남도록 구조를 바꿨다 (6.2절).

---

## 1. 요약

데이트·메인 스토리·프롤로그·엔딩에 공용으로 쓸 **이벤트 진행 시스템**을 만든다. 비주얼노벨형 연출(배경 + 요미 스탠딩 + 대사창 + 선택지)이고, 기존 요미 방의 선택형 대화(ChoiceTalk)와 UI 문법은 같되 **분기·플래그·다회차 저장**을 갖는 상위 시스템이다.

이 시스템의 형태를 지배하는 제약이 셋이다.

1. **이벤트 진행 중 자동 저장 절대 금지** — 그런데 기존 시스템은 곳곳에서 자동 저장을 일으킨다(7.1절). 결과는 메모리에 누적했다가 종료 시점에 한 번에 커밋한다.
2. **커밋 저장은 실패할 수 있다** — `SaveGame()`은 예외 없이 `false`만 돌려주는 실패 경로가 3개다(7.2절). 그래서 커밋을 **2단계**로 나눈다.
3. **호출자가 여럿이고 상황이 제각각이다** — 거래 중 스토리 이벤트, 방/월드맵의 데이트, 타이틀 직후 프롤로그. 하나의 표현 방식으로는 전부 감당되지 않는다 → **호스트 2종**(3절).

---

## 2. 요구사항

| # | 요구 | 비고 |
| --- | --- | --- |
| R1 | 배경 이미지 + 요미 에셋 + 대사창 + 대사 로그 + 설정 UI + 선택지 UI | 4.2절 |
| R2 | 호감도 단계별 전용 감정 에셋 | ✅ 완료 (2026-08-16) — 24종 × T1~T4, 7.9절 |
| R3 | 대사 로그: 재생 순서대로, 아래로 스크롤 | 4.4절 |
| R4 | 설정 UI에서 저장·업적 제거, 나가기·사운드만 | **선행 개조 필요** — 8.1절 |
| R5 | 대사는 타자 출력 → 클릭 시 전체 표시 | 4.3절 |
| R6 | 출력 완료 후 아무 영역 클릭 → 다음 대사 | 4.3절 |
| R7 | 플레이어 선택지 시스템 | 4.3절 |
| R8 | 선택 → 호감도 변화 / 스토리 분기 반영 + 저장 테이블 신설 | 6장 |
| R9 | 시작 플래그·종료 플래그 존재, **자동 저장 금지** | 6.2절, 7.1절 |
| R10 | 멀티 슬롯 · 다회차 플레이 지원 | 6.3절, Q-E2 |
| **R11** | **오버레이 / 전용 씬 두 방식을 상황에 따라 선택** | 3장 — v2 신규 |
| **R12** | **어느 호스트로 재생해도 이벤트 데이터와 진행 로직은 동일** | 3.1절 — 두 벌 구현 금지 |
| **R13** | **에디터에서 씬 단독 재생으로 이벤트를 테스트할 수 있어야 한다** | 7.6절 — 작가 워크플로 |

---

## 3. 구조 — 코어 1개, 호스트 2개

`EventScene`이라는 씬 하나로는 부족하고, 오버레이 하나로도 부족하다. 그런데 **둘을 따로 만들면 대사 진행·분기·커밋이 두 벌이 되어 반드시 갈라진다.** 그래서 갈라지는 부분만 호스트로 뽑는다.

```
FXOverdose.Events.Story
├── EventRunner        ← 코어. 상태기계 + 분기 + 지연 커밋 버퍼. UnityEngine.UI 참조 금지
├── EventView          ← UI 위젯 트리 하나. 대사창·선택지·로그·타자기·ClickCatcher
├── EventOverlayHost   ← 현재 씬 위에 런타임 캔버스 생성 → 콜백으로 종료
├── EventSceneHost     ← EventScene에 배치 → LoadingScene 경유 복귀
└── EventLauncher      ← 진입점. 게이트 검사 후 호스트를 골라 띄운다
```

**`EventView`는 하나다.** 오버레이와 전용 씬이 다른 것은 *어디에 붙느냐*와 *밑을 어떻게 처리하느냐*뿐이고, 위젯 트리·타자기·입력 규칙은 완전히 같다. 뷰를 두 벌 만들면 R12가 첫 주에 깨진다.

### 3.1. 호스트가 책임지는 것 (이것만 다르다)

| 항목 | `EventOverlayHost` | `EventSceneHost` |
| --- | --- | --- |
| 캔버스 | 현재 씬에 런타임 생성, `sortingOrder 1200+` | `EventScene`에 빌더가 배치 |
| 밑 화면 | 불투명 백드롭으로 덮음. 씬은 살아 있음 | 씬 자체가 교체됨 |
| 게임 정지 | `GameManager.PauseGame()` 호출 | 씬 로드가 자동으로 `Paused`로 만든다 (7.3절) |
| 종료 | **완료 콜백** `Action onComplete` | **`ReturnScene`으로 복귀** (`LoadingScene` 경유) |
| 진입 비용 | 없음 (씬 전환 0회) | 로딩 2회 (진입·복귀) |
| 적합 | 짧은 이벤트, 진행 중 문맥 유지 | 긴 이벤트, 화면 전체 전환, 밑 씬을 내리고 싶을 때 |

> ⚠️ **종료 계약이 비대칭인 것은 의도다.** 전용 씬은 씬이 파괴되므로 콜백이 살아남지 못한다(델리게이트가 붙잡은 오브젝트가 먼저 죽는다). 콜백을 static으로 살려 두는 편법은 쓰지 않는다 — 호출자가 이미 파괴된 상태에서 실행되는 버그를 만든다. **오버레이 = 콜백, 전용 씬 = 복귀 씬.** 호출부는 이 차이를 알고 골라야 한다.

### 3.2. 진입점

```csharp
// 이벤트 데이터가 정한 기본 호스트로 재생
EventLauncher.Play("EVT_DATE_003");

// 호출자가 호스트를 강제 (오버레이만 콜백을 받는다)
EventLauncher.Play("EVT_MAIN_007", EventHostKind.Overlay, onComplete: () => ResumeSettlement());
EventLauncher.Play("EVT_MAIN_001", EventHostKind.Scene, returnScene: "YomiRoomScene");
```

- 반환은 `bool` — 게이트(6.4절)에 걸려 재생하지 못하면 `false`. **호출부는 반환값을 봐야 한다.** 무시하면 "이벤트가 재생됐다고 믿고 진행 상태를 올리는" 경로가 생긴다 (F-2와 같은 부류).
- 기본 호스트는 이벤트 데이터에 적는다. 호출자가 명시하면 그쪽이 이긴다.

### 3.3. 호스트 선택 기준

| 상황 | 호스트 | 이유 |
| --- | --- | --- |
| 거래 중/정산 중 스토리 이벤트 | **오버레이** | 거래 씬을 내렸다 올리면 `PrepareLoadGame` 프로토콜(7.4절)을 타야 한다. 안 건드리는 게 안전하다 |
| 요미 방·월드맵의 짧은 데이트 대화 | **오버레이** | 씬 전환 2회가 대사 열 줄보다 길다 |
| 프롤로그·엔딩·대형 데이트 (수십 노드 + CG 다수) | **전용 씬** | 밑 씬을 메모리에서 내리는 값어치가 있다. 화면이 통째로 바뀌는 연출과도 맞다 |
| 타이틀 직후 (밑에 깔 게임 화면이 없음) | **전용 씬** | 오버레이할 대상이 타이틀 UI뿐이라 의미가 없다 |

---

## 4. 씬·화면 구성과 연출

### 4.1. EventScene (전용 씬 호스트용)

```
EventScene
├── EventSceneHost    (EventRunner 소유 + 복귀 처리)
└── EventCanvas       (= EventView. 오버레이가 런타임으로 만드는 것과 같은 트리)
```

- 씬은 **에디터 빌더 메뉴로 생성**한다 (`FX Overdose/Build Event Scene`). 손으로 만든 씬은 다음 빌더 실행에 덮인다(프로젝트 규약). Build Settings 등록 필요.
- 진입 전 정적 컨텍스트를 채운다: `EventLaunchContext { EventId, ReturnScene }`. 씬이 하나이므로 어디로 돌아갈지는 호출자만 안다.

### 4.2. EventView 위젯 트리 (양쪽 공통)

```
EventCanvas
├── Backdrop          (오버레이일 때만 불투명. 전용 씬에서는 비활성)
├── Background        (배경/CG. 이벤트 데이터가 지정)
├── YomiStanding      (스탠딩. 호감도 단계 × 감정 → 스프라이트. CG 전용 이벤트는 숨김)
├── DialoguePanel     (화자명 + 본문. 타자 출력)
├── ChoicePanel       (선택지 2~4개. 표시 중엔 전체 클릭 무시)
├── LogPanel          (대사 백로그. 열면 진행 일시정지)
├── SettingsButton    (SettingsMenuController 제한 레이아웃 — 8.1절)
└── ClickCatcher      (전체 화면 투명 버튼 — 대사 넘김 전용)
```

`YomiStanding` 숨김은 이벤트 헤더의 플래그로 정한다. 프롤로그처럼 CG 안에 요미가 그려진 이벤트는 스탠딩을 겹치면 안 된다.

### 4.3. 대사 출력 상태기계 (코어)

```
[Typing]  글자 단위 출력 중
   │ 클릭 → 전문 즉시 표시
   ▼
[Revealed]  출력 완료, 대기
   │ 아무 영역 클릭
   ▼
다음 노드 → 대사면 [Typing], 선택지면 [Choice], 없으면 [End]

[Choice]  선택지 표시 중
   │ ClickCatcher 비활성 — 선택지 버튼만 입력을 받는다 (7.7절)
   ▼ 선택 → 결과 누적(메모리) → 다음 노드

[End]  종료 처리 → 2단계 커밋(6.2절) → 호스트에 종료 통보
```

**모든 시간 기반 연출은 `Time.unscaledDeltaTime`을 쓴다.** 설정 메뉴가 열리면 [SettingsMenuController.cs:288-289](../../Assets/Scripts/UI/SettingsMenuController.cs#L288-L289)가 `Time.timeScale = 0`을 걸고, `DynamicTimeRegulator`는 거래 중 슬로모션을 건다. 스케일드 시간을 쓰면 타자기가 멈추거나 느려진다.

선택지 문법은 ChoiceTalk의 `TalkChoice`를 따른다(25자 이내, 반말, 선택 직후 요미 반응 1줄). 추가되는 것은 **분기** — 선택지마다 다음 노드를 지정할 수 있다(6.1절). 호감도 배분 규격(토픽당 총획득 고정)은 적용하지 않는다. 이벤트는 하루 1회 반복형이 아니라 1회성이므로 이벤트별로 계획한다.

### 4.4. 대사 로그(백로그)

- 이벤트 시작부터의 (화자, 대사)를 **메모리에만** 누적. 저장하지 않는다.
- 로그 버튼 → 패널이 열리고 진행 입력 차단. 재생 순서대로 위→아래, 최신이 맨 아래, 스크롤 가능.
- 선택지에서 고른 플레이어 대사도 남긴다. 뭘 골랐는지 다시 볼 수 없으면 분기 이벤트에서 로그가 무의미하다.

---

## 5. 데이터

### 5.1. 이벤트 스크립트

ID 체계는 ChoiceTalk 3.1절을 따른다: `EVT_DATE_001`, `EVT_MAIN_001` — **영구 식별자, 재사용 금지**(세이브에 기록된다).

```csharp
// 정적 C# 테이블 (YomiTalkTopics 패턴 — 폰트 프리베이크가 .cs만 스캔하므로)
public readonly struct EventDefinition
{
    public readonly string Id;
    public readonly EventHostKind DefaultHost;  // 기본 표현 방식
    public readonly bool HideStanding;          // CG 전용 이벤트
    public readonly EventNode[] Nodes;
}

public readonly struct EventNode
{
    public readonly int Id;               // 노드 번호 (이벤트 내 로컬)
    public readonly string[] Lines;       // 대사 1~3줄. "※" 지문 규칙 동일
    public readonly string BackgroundId;  // 배경/CG 교체 (null이면 유지)
    public readonly EventEmotion Emotion; // 감정 24종 → 호감도 티어 × 감정으로 스탠딩 교체 (7.9절)
    public readonly EventChoice[] Choices;// 비어 있으면 NextId로 자동 진행
    public readonly int NextId;           // -1 = 종료
}

public readonly struct EventChoice
{
    public readonly string Text;
    public readonly int Affection;        // 지연 커밋 누적 대상 (7.1절)
    public readonly string Reply;
    public readonly int NextId;           // ★ 분기
    public readonly string SetFlag;       // 분기 플래그 (null이면 없음)
}
```

ChoiceTalk의 `TalkNode`는 **선형 배열**이라 그대로 못 쓴다 — 실측: [YomiRoomManager.cs:229-236](../../Assets/Scripts/DatingSim/YomiRoom/YomiRoomManager.cs#L229-L236)이 `activeNodeIndex++`로만 진행한다. **기존 ChoiceTalk를 이 구조로 옮기지 않는다** — 일상 대화는 선형으로 충분하고, 옮기면 세이브 호환과 대사 전량 재작성이 따라온다. 두 시스템은 `TalkChoice` 문법(말투 규격)만 공유하고 데이터 타입은 분리한다.

### 5.2. 대사 데이터는 정적 C# 테이블로

`PrebakeTMPFont`는 `.cs`만 스캔한다. ScriptableObject로 가면 새 한글이 □가 된다. YomiTalkTopics 패턴이 검증돼 있으므로 그대로 따른다. 휴면 CSV 파이프라인(`ScenarioCSVImporter`) 부활은 작가 협업이 실제로 시작될 때 재검토(Q-E3).

---

## 6. 저장 설계

### 6.1. SaveData 신설 필드

```csharp
public List<string> EventCompletedIds = new();   // 종료 플래그: "EVT_MAIN_001"
public List<string> EventChoiceHistory = new();  // "EVT_MAIN_001:3:1" — TalkChoiceHistory와 동일 포맷
public List<string> StoryFlags = new();          // 분기 플래그: "MAIN_ROUTE_A" 등
```

- `SaveGame()` gather / load scatter 2곳에 추가.
- **`SaveDataMigrator` 백필은 필요 없다.** [SaveDataMigrator.cs:38-44](../../Assets/Scripts/System/SaveDataMigrator.cs#L38-L44)에 근거가 적혀 있다 — JsonUtility는 인스턴스를 먼저 만든 뒤 JSON에 있는 키만 덮으므로, 구버전 JSON에 없는 신규 필드는 초기화자의 빈 리스트를 유지한다. (v1 계획의 "3곳 세트"는 과잉이었다.)
- `EventChoiceHistory`는 `TalkChoiceHistory`처럼 상한 트림.

### 6.2. ★ 2단계 커밋 — 이 시스템의 핵심

`SaveCurrentGame()`은 **실패해도 예외를 던지지 않고 `false`만 돌려준다.** 실패 경로가 셋이고(7.2절), 그중 하나는 기존 스토리 이벤트가 발화하는 바로 그 상태다. 따라서 커밋을 나눈다.

```
[End] 도달
 │
 ├─ 1단계: SaveLoadManager.CurrentData 에 반영          ← 항상 성공 (메모리)
 │    · EventCompletedIds += 이벤트 ID
 │    · StoryFlags += 분기 플래그
 │    · EventChoiceHistory += 선택 이력
 │    · DatingTimeManager.ModifyAffection(누적 델타)     ← 이 호출이 2단계를 겸한다
 │    · LastSceneName = ReturnScene  (전용 씬 호스트만 — 7.5절)
 │
 └─ 2단계: SaveCurrentGame() 1회 시도                    ← 실패 가능
      · 성공 → 끝
      · 실패 → 경고 로그만 남기고 진행. 1단계가 CurrentData에 남아 있으므로
              다음 저장이 언제 일어나든 부분 저장 베이스([SaveLoadManager.cs:137](../../Assets/Scripts/System/SaveLoadManager.cs#L137))를
              통해 자동으로 함께 실린다.
```

이 구조 덕분에 **최악의 경우에도 잃는 것은 "디스크에 늦게 쓰였다"뿐이고, 결과 자체는 사라지지 않는다.** 기존 부분 저장 메커니즘(SV-A6)을 그대로 얻어 쓰는 것이라 새로 만드는 것이 없다.

> 호감도 델타를 `ModifyAffection`으로 한 번에 흘리는 것이 2단계를 겸한다 — 저 메서드가 [자체적으로 `SaveCurrentGame()`을 부르기](../../Assets/Scripts/DatingSim/Core/DatingTimeManager.cs#L222) 때문이다. 델타가 0인 이벤트에서만 별도로 2단계를 호출한다.

### 6.3. 시작/종료 플래그 (R9)

- **시작 플래그**: 런타임 전용이되 **static**이어야 한다. 전용 씬 호스트는 진입 시 이전 인스턴스가 이미 파괴되므로 인스턴스 필드에 두면 중복 진입 가드가 무효다.
- **종료 플래그**: 끝까지 진행됐을 때만 `SaveData.EventCompletedIds`에 기록.
- **중도 이탈**: 아무것도 남기지 않는다 → 이벤트는 처음부터. 자동 저장 금지의 자연스러운 귀결이다.
- 다회차: **회차의 정의(Q-E2)가 먼저다.** "슬롯별 독립"이면 위로 충분하고, "회차 인계(NG+)"라면 `PlaythroughCount`와 인계 규칙 필드가 추가된다.

### 6.4. 진입 게이트 (`EventLauncher.Play`가 재생 전에 검사)

| 검사 | 실패 시 |
| --- | --- |
| 이미 `EventCompletedIds`에 있는가 | `false` 반환 (1회성 이벤트 재생 방지) |
| static 진행 플래그가 켜져 있는가 | `false` 반환 (중복 진입·이벤트 중 이벤트) |
| 이벤트 ID가 테이블에 있는가 | `false` 반환 + 에러 로그 |
| 저장이 가능한 상태인가 | **재생은 허용, 경고 로그.** 2단계 커밋이 있으므로 막을 이유가 없다 |

---

## 7. 안전장치 — 실측으로 확인한 위험과 대응

### 7.1. 자동 저장은 정말로 곳곳에서 일어난다

[DatingTimeManager](../../Assets/Scripts/DatingSim/Core/DatingTimeManager.cs)의 상태 변경 메서드 **전부**가 그 자리에서 디스크에 쓴다 — `TryConsumeTimeSlot`(:156) · `TryConsumeStamina`(:199) · `RecoverStamina`(:209) · `ModifyAffection`(:222) · `ModifyObsession`(:231) · `SetStoryProgressStage`(:239) · `SyncToNewDay`(:270).

> **이벤트 진행 중에는 기존 매니저의 상태 변경 API를 일절 호출하지 않는다.**
> 호감도 델타·플래그·완료 기록을 `EventRunner`가 메모리에 누적하고, [End]에서 한 번에 반영한다.

위반하면 중도 이탈 시 "호감도는 올랐는데 이벤트는 미완료"라는 반쪽 상태가 디스크에 남는다.

### 7.2. ★ 커밋 저장이 조용히 실패하는 경로 3개

[`SaveGame()`](../../Assets/Scripts/System/SaveLoadManager.cs#L92)은 아래 셋에서 `false`를 돌려주고 끝난다.

| # | 조건 | 위치 |
| --- | --- | --- |
| 1 | `CurrentGameMode != Story` | [:94-98](../../Assets/Scripts/System/SaveLoadManager.cs#L94-L98) |
| 2 | `GameManager.CurrentState`가 `Playing`/`Paused`가 아님 → **Settlement·GameOver·Loading에서 저장 불가** | [:117-123](../../Assets/Scripts/System/SaveLoadManager.cs#L117-L123) |
| 3 | 오버도즈 상태 또는 기믹 진행 중 | [:125-130](../../Assets/Scripts/System/SaveLoadManager.cs#L125-L130) |

**2번이 실제로 밟히는 경로가 이미 있다.** 기존 스토리 이벤트는 [GameManager.cs:994](../../Assets/Scripts/GameManager.cs#L994)에서 `currentState = GameState.Settlement`로 바꾼 직후 [:1042](../../Assets/Scripts/GameManager.cs#L1042)에서 컷씬을 재생한다. 이 자리에 이벤트 시스템을 붙이면 커밋이 실패한다. → **6.2절 2단계 커밋으로 해결.** 추가로 `PauseGame()`은 Settlement에서 아무 일도 하지 않으므로([GameManager.cs:499](../../Assets/Scripts/GameManager.cs#L499)) 오버레이 호스트도 이 경로를 우회하지 못한다. 2단계 커밋이 유일한 해법이다.

### 7.3. 씬 로드가 GameManager 상태를 바꾼다 — 이득 1, 함정 1

[`HandleSceneLoaded`](../../Assets/Scripts/GameManager.cs#L476-L503)는 거래 씬·로딩 씬이 아닌 모든 씬에서 상태를 `Paused`로 만든다.

- **이득**: EventScene 로드만으로 저장 허용 상태(`Paused`)가 확보된다. 전용 씬 호스트는 별도 정지 처리가 필요 없다.
- **함정**: 같은 메서드가 이어서 `RoomSettlementEnabled && (isSettlementProcessing || currentHour >= 24)`면 **그 씬에서 일일 정산을 시작한다.** 24:00 직후나 정산 중단 상태에서 EventScene에 진입하면 **이벤트 위로 정산이 겹쳐 올라온다.**
  → 대응: `EventLauncher`가 전용 씬 호스트를 고를 때 `isSettlementProcessing || CurrentHour >= 24`면 진입을 미루거나 오버레이로 강등한다. 이 판정은 진입 게이트(6.4절)에 넣는다.

### 7.4. ★ GameScene 복귀에는 별도 프로토콜이 필요하다

거래 씬 복귀는 `SaveCurrentGame()` → `PrepareLoadGame()` 순서를 지켜야 GameManager가 `StartNewGame()`으로 새 게임을 시작하지 않는다 (SV-B10, [YomiRoomManager.cs:489-495](../../Assets/Scripts/DatingSim/YomiRoom/YomiRoomManager.cs#L489-L495)). 이 프로토콜을 안 밟으면 **진행 중이던 판이 초기화된다.**

→ **전용 씬 호스트의 `ReturnScene`은 `YomiRoomScene`·`WorldMapScene`만 허용한다.** GameScene 복귀가 필요한 이벤트는 오버레이 호스트를 쓴다(3.3절). 이 제한이 프로토콜 중복 구현을 통째로 없앤다.

### 7.5. ★ `LastSceneName` — 프롤로그에서 바로 터진다

[`ResumableScenes`](../../Assets/Scripts/System/SaveLoadManager.cs#L40)는 `{GameScene, YomiRoomScene, WorldMapScene}`이고 EventScene은 없다. **이건 의도상 옳다** — 재접속 시 이벤트 씬으로 복귀하면 안 된다. `EventScene`을 이 배열에 추가하는 것은 오답이다.

문제는 새 게임 프롤로그다. TitleScene → EventScene 경로라 `LastSceneName`이 여태 `""`이고, 커밋 직후 종료하면 [LoadingScreenController.cs:64-70](../../Assets/Scripts/UI/LoadingScreenController.cs#L64-L70) / [MainMenuController.cs:628](../../Assets/Scripts/UI/MainMenuController.cs#L628)의 폴백에 따라 **요미 방이 아니라 GameScene으로 떨어진다.**

→ 전용 씬 호스트는 커밋 1단계에서 `data.LastSceneName = ReturnScene`을 **명시적으로 기입한다.** (오버레이는 밑 씬이 그대로라 이 문제가 없다.)

### 7.6. 씬 단독 재생 방어 (R13)

`SaveLoadManager`는 `TitleScene`에만 있으므로, 에디터에서 EventScene만 열면 `Instance`가 null이다. [YomiRoomManager의 `scratchData` 패턴](../../Assets/Scripts/DatingSim/YomiRoom/YomiRoomManager.cs#L89-L122)(SV-C6)을 그대로 따른다 — 임시 `SaveData`로 떨어지고, 디스크에 쓰지 않으며, 경고를 한 번 남긴다. 이게 없으면 이벤트 작가가 자기 이벤트를 열어볼 수조차 없다.

### 7.7. 입력 레이어 충돌

"아무 영역 클릭 → 다음 대사"(R6)와 "선택지 클릭"(R7)은 같은 화면을 두고 경쟁한다. `ClickCatcher`는 `[Choice]` 상태와 로그 패널이 열린 동안 **반드시 비활성**. **상태기계의 상태가 입력 레이어를 단독으로 결정하고, UI 쪽에서 개별 판단하지 않는다.**

오버레이 호스트는 여기에 하나 더 있다 — 백드롭이 밑 씬의 레이캐스트를 막아야 한다. 안 막으면 이벤트 대사를 넘기는 클릭이 밑의 거래 버튼까지 누른다.

### 7.8. 스컴 가능성 — 의도 확인 필요

진행 중 저장이 없으므로 중요 분기에서 게임을 끄고 다시 하면 선택을 무한 재시도할 수 있다. ChoiceTalk는 "여는 즉시 소비 마커 저장"(TS1, [YomiRoomManager.cs:178-190](../../Assets/Scripts/DatingSim/YomiRoom/YomiRoomManager.cs#L178-L190))으로 막았지만 이벤트는 자동 저장 금지라 그 수단이 없다. 1회성 스토리에서 재시도 허용은 보통 의도된 관용이지만, **메인 스토리 분기점에서도 허용할지**는 기획 결정이다 (Q-E1).

### 7.9. 감정 에셋 — 배선 완료 (2026-08-16)

> v1 계획은 "필드만 먼저 파고 기본 표정 하나로 렌더"였다. 스프라이트 96종이 실제로 들어와서 **렌더까지 붙였다.**

- `EventEmotion`은 [P2_07](../P2_07_emotion/P2_07_DatingSim_Emotion_Table_Implementation_Plan.md) 24종이고, **enum 이름이 곧 파일명**이다 — `Resources/DatingSim/Emotions/Sprites/{T1~T4}/{감정}.png`. 개명하면 그 감정이 화면에서 사라진다.
- 티어 판정은 `DatingTimeManager.CurrentAffection` **현재치** 기준 91/61/31 경계 ([Affection_Tier_Table.md](Affection_Tier_Table.md)가 단일 진실 원천). 피크가 아닌 이유는 그 문서에 있다 — 호감도가 깎였는데 T4 표정이 나오면 거짓말이 된다.
- ⚠️ **로드는 `Resources.LoadAll<Sprite>(...)[0]`이다.** 감정 PNG가 Multiple 스프라이트 모드로 임포트돼 있어(시트당 서브 스프라이트 1장) 메인 에셋이 Texture2D고, `Resources.Load<Sprite>`는 여기서 **null을 돌려준다.**
- 스프라이트가 없으면 Calm 폴백 → 그것도 없으면 스탠딩을 숨기고 경고만. 배경과 같은 방침이라 아트가 빠져도 이벤트는 완주한다.
- `DatingEmotionTableSO`(P2_07 4절)는 **만들지 않았다.** 파일명이 enum 이름과 같아 테이블이 문자열 연결 한 줄과 같은 일을 한다. 감정별 UI 색상·표시명이 실제로 필요해지면 그때 만든다.
- 검증: `FXOverdose/Debug/Validate Event Data`가 24종 × 4티어 = 96장 로드를 확인한다. enum만 늘리고 스프라이트를 안 넣으면 원래는 *그 감정이 쓰인 대사에 도달했을 때만* 조용히 Calm으로 떨어진다.
- 크로마키 원본·리테이크 잔재 105장은 `ArtSource/DatingSim/Emotions/`로 뺐다. **Resources 폴더는 참조 여부와 무관하게 전부 빌드에 실리기 때문이다.**

### 7.10. 진입 비용은 호출자 몫

슬롯 소모·날짜 진행·데이트 비용 지불은 **이벤트 밖**에서 호출자가 처리한다(이벤트 러너는 대화만 안다). 그 시점의 자동 저장은 허용된다. 경계선은 **"`EventLauncher.Play` 성공 반환부터 [End] 커밋까지"**로 명문화한다.

---

## 8. 재사용 자산 — 실측 결과 (v1에서 정정)

| 필요 | v1 계획 | **실측** |
| --- | --- | --- |
| 설정 UI (저장·업적 제거) | "P2P 변형이 정확히 이 구성, 신규 UI 불필요" | ✗ **선행 개조 필요** — 8.1절 |
| 타자 출력·클릭 스킵 | "ChoiceTalk 대화 UI에서 재사용" | ✗ **독립 컴포넌트가 없다** — 8.2절 |
| 오버레이 호스트 기반 | (v1에 없음) | ✓ **선례 2건** — 8.3절 |
| 선택지 데이터 규격 | `TalkChoice`/`TalkNode` | ✓ 문법만 계승, 타입은 분리 (5.1절) |
| 이벤트 조건·플래그 구조 | `ScenarioEntry`의 flags | △ 휴면 자산. 구조는 맞지만 호출자가 없다 |
| 저장 패턴 | `SaveData` + gather/scatter | ✓ 단 마이그레이터는 불필요 (6.1절) |

### 8.1. SettingsMenuController — 판정 소스 일반화가 선행 작업

[`ApplyP2PMenuLayout()`](../../Assets/Scripts/UI/SettingsMenuController.cs#L515-L517)은 `P2PNetworkSessionManager.Instance?.IsRunning`에 하드와이어돼 있다. EventScene은 P2P 세션이 아니므로 이 레이아웃을 켤 수 없다.

→ 판정을 **"제한 레이아웃이 필요한가"**라는 플래그로 뽑고, P2P와 이벤트가 각각 그 플래그를 세우게 한다. 메서드 이름도 `ApplyRestrictedMenuLayout()`으로 바꾼다. 작업량은 작지만 **이걸 먼저 하지 않으면 이벤트 중 설정 창에서 저장 버튼이 노출된다** — 자동 저장 금지 요구의 정면 위반이다.

### 8.2. 타자기는 발췌·컴포넌트화가 선행 작업

타자기 연출은 독립 컴포넌트가 아니라 [YomiRoomTopDownPrototype.cs](../../Assets/Scripts/DatingSim/YomiRoom/YomiRoomTopDownPrototype.cs)(989줄) 안에 말풍선 로그와 함께 박혀 있다 — [:846](../../Assets/Scripts/DatingSim/YomiRoom/YomiRoomTopDownPrototype.cs#L846) 부근. 재사용하려면 뽑아내야 한다. 뽑을 때 **지문은 타자기를 쓰지 않는다**는 기존 규칙([:738](../../Assets/Scripts/DatingSim/YomiRoom/YomiRoomTopDownPrototype.cs#L738), D-4)과 **행 높이를 전체 문자열로 미리 확정해 레이아웃 흔들림을 막는 처리**([:889](../../Assets/Scripts/DatingSim/YomiRoom/YomiRoomTopDownPrototype.cs#L889), TS25)를 같이 가져온다. 둘 다 이미 한 번 겪은 문제다.

### 8.3. 오버레이 호스트에는 검증된 선례가 둘 있다

- [`ComicCutsceneController.GetOrCreateRuntime()`](../../Assets/Scripts/UI/ComicCutsceneController.cs#L32) — 캔버스가 없는 씬에서 스스로 전체화면 캔버스를 만들고(`sortingOrder 1200`), 클릭으로 진행하고, **완료 콜백**으로 끝난다. GameManager가 정산 중에, TutorialManager가 튜토리얼 중에 같은 방식으로 부른다.
- [`ChoiceEventController.PrepareGamePause()`](../../Assets/Scripts/Events/ChoiceEventController.cs#L807) — 선택지 팝업 + `GameManager.PauseGame()`. **거래 중 선택지 이벤트가 이미 오버레이로 돌고 있다.**

`EventOverlayHost`는 이 둘의 조합이다. 새로 발명하는 구조가 아니다.

---

## 9. 구현 순서

선행 작업(0단계)을 먼저 끝낸다. 안 하면 3·5단계에서 막힌다.

| 단계 | 내용 | 산출 |
| --- | --- | --- |
| **0-a** | `SettingsMenuController` 제한 레이아웃 플래그 일반화 (8.1절) | 기존 P2P 동작 무회귀 |
| **0-b** | 타자기·말풍선을 재사용 가능한 컴포넌트로 발췌 (8.2절) | 요미 방 동작 무회귀 |
| 1 | `EventDefinition`/`EventNode`/`EventChoice`/`EventEmotion` + 샘플 이벤트 1편 | 정적 테이블 |
| 2 | `EventRunner` — 상태기계 + 지연 커밋 버퍼 + 2단계 커밋 (6.2절). **UI 참조 금지** | 코어 |
| 3 | `EventView` — 위젯 트리 + 타자기 + 로그 + 입력 레이어(7.7절) | UI 1벌 |
| 4 | `SaveData` 3필드 + gather/scatter | 저장 |
| 5 | `EventOverlayHost` + `EventLauncher` + 진입 게이트(6.4절) | **오버레이 경로 완성 — 여기서 한 번 돌려본다** |
| 6 | `EventSceneHost` + 씬 빌더(`FX Overdose/Build Event Scene`) + Build Settings 등록 | 전용 씬 경로 |
| 7 | 복귀 배선 — `ReturnScene` 제한(7.4절), `LastSceneName` 기입(7.5절), 정산 충돌 게이트(7.3절) | 전용 씬 안전장치 |
| 8 | 폰트 프리베이크 재실행 + 시나리오 테스트 | 10절 |

**5단계에서 한 번 끊고 검증한다.** 오버레이만으로 이벤트가 완주되면 코어·뷰·저장이 전부 검증된 것이고, 6~7단계는 껍데기 하나를 더 씌우는 일이 된다. 반대로 전용 씬부터 만들면 복귀 계약 디버깅과 코어 디버깅이 뒤섞인다.

---

## 10. 검증 항목

- [ ] 오버레이로 이벤트 완주 → 결과가 디스크에 반영
- [ ] 전용 씬으로 **같은 이벤트** 완주 → 동일 결과 (R12 검증)
- [ ] 진행 중 강제 종료 → 디스크 무변화, 재진입 시 이벤트가 처음부터
- [ ] `GameState.Settlement`에서 이벤트 완주 → 2단계 커밋 경고 로그 후, 다음 저장에 결과가 실려감 (7.2절)
- [ ] 프롤로그(전용 씬) 완주 직후 종료 → 재접속 시 **요미 방**으로 복귀 (7.5절)
- [ ] 이벤트 중 설정 창 → 저장·업적 버튼 없음 (8.1절)
- [ ] 이벤트 중 설정 창 열고 닫아도 타자기가 정상 진행 (`unscaledDeltaTime`, 4.3절)
- [ ] 선택지 표시 중 화면 아무 곳이나 클릭해도 대사가 넘어가지 않음 (7.7절)
- [ ] 오버레이 중 밑 씬의 버튼이 눌리지 않음 (7.7절)
- [ ] 완료된 이벤트 재호출 → `Play`가 `false` 반환, 재생 안 됨 (6.4절)
- [ ] EventScene 단독 재생 → NRE 없이 진행, 경고 1회 (7.6절)

---

## 11. 결정 필요 사항

| ID | 질문 | 권장 |
| --- | --- | --- |
| Q-E1 | 메인 스토리 분기점에서도 스컴(껐다 켜서 재선택)을 허용하는가? | **허용** — 막으려면 진행 중 저장이 필요한데 그게 R9 위반이다. 되돌릴 수 없게 하려면 분기 직후 이벤트를 끊고 완료 커밋을 넣는 편이 설계상 깔끔하다 |
| Q-E2 | 다회차 = 슬롯별 독립인가, 회차 인계(NG+)인가? | 테이블 설계 선행 조건. NG+면 인계 항목 목록이 먼저 필요하다 |
| Q-E3 | 이벤트 대사 소스 — 정적 C# 테이블 유지 vs CSV 파이프라인 부활 | v1은 정적 테이블. 작가 투입 시 재검토 |
