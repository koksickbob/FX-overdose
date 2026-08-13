# 이벤트 진행 씬(EventScene) 시스템 — 계획 및 구조 검토

> **작성일**: 2026-08-14
> **대상 브랜치**: `Dev_koksickbob3`
> **상태**: 계획 초안 / 구조 검토 완료 / 구현 착수 전
> **관련 문서**: [YomiRoom_ChoiceTalk_System_Plan.md](YomiRoom_ChoiceTalk_System_Plan.md) (선택형 대화 규격의 원본),
> [P2_02_Yomi_Character_Bible.md](../P2_02_Worldbuilding/P2_02_Yomi_Character_Bible.md) (대사 규격)

---

## 1. 요약

데이트 및 메인 스토리 진행 시 사용할 **전용 이벤트 진행 씬**을 신설한다. 비주얼노벨형 연출(배경 + 요미 스탠딩 + 대사창 + 선택지)로, 기존 요미 방의 선택형 대화(ChoiceTalk)와 UI 문법은 같되 **분기·플래그·다회차 저장**을 갖는 상위 시스템이다.

핵심 제약 두 가지가 설계를 지배한다:

1. **이벤트 진행 중 자동 저장 절대 금지** — 그런데 기존 시스템은 곳곳에서 자동 저장을 일으킨다(7.1절). 따라서 이벤트 결과는 **메모리에 누적했다가 종료 시점에 한 번에 커밋**하는 구조가 필수다.
2. **다회차 + 멀티 슬롯 지원** — 이벤트 완료/분기 기록은 전부 `SaveData`(슬롯별 파일) 안에 있어야 하며, 회차 정의(Q-E2)에 따라 테이블 모양이 달라진다.

---

## 2. 요구사항 정리

| # | 요구 | 비고 |
| --- | --- | --- |
| R1 | 배경 이미지 + 요미 에셋 + 대사창 + 대사 로그 + 설정 UI + 선택지 UI로 씬 구성 | 3장 |
| R2 | 호감도 단계별 전용 감정 에셋 (상세 테이블 추후 기입) | 데이터 훅은 지금부터 (7.8절) |
| R3 | 대사 로그: 재생 순서대로, 아래로 스크롤하며 읽기 | 4.3절 |
| R4 | 설정 UI에서 저장·업적 제거, 나가기·사운드만 | `SettingsMenuController` P2P 변형 재사용 (5장) |
| R5 | 대사는 타자 출력 → 클릭 시 전체 표시 | ChoiceTalk와 동일 문법 (4.1절) |
| R6 | 출력 완료 후 아무 영역 클릭 → 다음 대사 | 4.1절 |
| R7 | 플레이어 선택지 시스템 | 4.2절 |
| R8 | 선택 → 호감도 변화 / 메인 스토리 분기 반영용 시스템 + 저장 테이블 신설 | 6장 |
| R9 | 시작 플래그·종료 플래그 존재, **자동 저장 금지** | 6.2절, 7.1절 |
| R10 | 멀티 슬롯 · 다회차 플레이 지원 | 6.3절, Q-E2 |

---

## 3. 씬 구성

새 씬 `EventScene` 1개. 데이트 이벤트든 메인 스토리 이벤트든 같은 씬을 쓰고 **이벤트 ID로 내용만 갈아끼운다.** 진입 지점(요미 방 / 월드맵)이 어디든 목적지는 하나여야 복귀 로직이 단순해진다.

```
EventScene
├── Background        (전체 화면 이미지, 이벤트 데이터가 지정)
├── YomiStanding      (요미 스탠딩. 호감도 단계 × 감정 → 스프라이트)
├── DialoguePanel     (화자명 + 본문. 타자 출력)
├── ChoicePanel       (선택지 2~4개. 표시 중엔 전체 클릭 무시)
├── LogPanel          (대사 백로그. 열면 진행 일시정지)
├── SettingsButton    (SettingsMenuController — 오디오 + 나가기만)
└── ClickCatcher      (전체 화면 투명 버튼 — 대사 넘김 전용)
```

- **씬 전환은 반드시 `LoadingScene` 경유** (프로젝트 규약). 진입 전에 정적 컨텍스트를 채운다:
  `EventSceneContext { EventId, ReturnScene }` — 이벤트 ID와 함께 **복귀 목적지**(YomiRoom/WorldMap)를 반드시 실어야 한다. 씬이 하나이므로 어디로 돌아갈지는 호출자가 알려줘야 한다.
- 씬은 프로젝트 규약대로 **에디터 빌더 메뉴로 생성**한다 (`FX Overdose/Build Event Scene`). 손으로 만든 씬은 다음 빌더 실행에 덮인다. Build Settings 등록 필요.

---

## 4. 연출 흐름

### 4.1. 대사 출력 상태기계

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

[End]  종료 처리 → 결과 커밋(6.2절) → ReturnScene으로 복귀
```

### 4.2. 선택지

ChoiceTalk의 `TalkChoice` 문법을 따른다(25자 이내, 반말, 선택 직후 요미 반응 1줄). 추가되는 것은 **분기**다: 선택지마다 다음 노드를 지정할 수 있다 (5장). 호감도 배분 규격(토픽당 총획득/총감소 고정)은 이벤트에는 그대로 적용하지 않는다 — 이벤트는 하루 1회 반복형이 아니라 1회성이므로 이벤트별로 계획한다.

### 4.3. 대사 로그(백로그)

- 이벤트 시작부터의 (화자, 대사) 목록을 **메모리에만** 누적. 저장하지 않는다.
- 로그 버튼 클릭 → 패널이 열리고 진행 입력 차단. 재생 순서대로 위→아래, 최신이 맨 아래, 스크롤 가능.
- 선택지에서 고른 플레이어 대사도 로그에 남긴다 (뭘 골랐는지 다시 볼 수 있어야 분기 이벤트에서 의미가 있다).

---

## 5. 재사용 자산

| 필요 | 재사용 대상 | 근거 |
| --- | --- | --- |
| 타자 출력·클릭 스킵 | ChoiceTalk 대화 UI의 출력 연출 | 동일 문법 요구(R5) |
| 설정 UI (저장·업적 제거) | [SettingsMenuController.cs](../../Assets/Scripts/UI/SettingsMenuController.cs) — **P2P 변형이 정확히 이 구성** ("업적·세이브만 숨기고 오디오·FPS 설정과 종료 동작 제공") | 이미 있는 모드를 그대로 사용, 신규 UI 불필요 |
| 선택지 데이터 규격 | `TalkChoice`/`TalkNode` (YomiTalkTopics.cs) | 검증된 구조, 폰트 프리베이크 호환 |
| 이벤트 조건·플래그 구조 | `ScenarioEntry`의 `requiredFlags`/`blockingFlags`/`setFlagsOnComplete` | 휴면 자산이지만 구조가 이 용도로 설계돼 있음 |
| 저장 패턴 | `SaveData` + `SaveGame()` gather + load scatter + `SaveDataMigrator` | 기존 Talk* 필드와 동일 절차 |

---

## 6. 데이터 및 저장 설계

### 6.1. 이벤트 스크립트 데이터

ID 체계는 ChoiceTalk 3.1절을 따른다: `EVT_DATE_001`, `EVT_MAIN_001` — **영구 식별자, 재사용 금지** (세이브에 기록되므로).

```csharp
// 정적 C# 테이블 (YomiTalkTopics 패턴 — 폰트 프리베이크가 .cs만 스캔하므로)
public readonly struct EventNode
{
    public readonly int Id;               // 노드 번호 (이벤트 내 로컬)
    public readonly string[] Lines;       // 대사 1~3줄. "※" 지문 규칙 동일
    public readonly EventEmotion Emotion; // 감정 (테이블 추후 확장, 지금은 enum 자리만)
    public readonly EventChoice[] Choices;// 비어 있으면 자동으로 NextId 진행
    public readonly int NextId;           // 선택지 없을 때 다음 노드 (-1 = 종료)
}

public readonly struct EventChoice
{
    public readonly string Text;
    public readonly int Affection;        // 지연 커밋 누적 대상 (7.1절)
    public readonly string Reply;
    public readonly int NextId;           // ★ 분기: 선택마다 다음 노드 지정
    public readonly string SetFlag;       // 분기 플래그 (null이면 없음)
}
```

ChoiceTalk의 `TalkNode`는 **선형 배열**이라 그대로 못 쓴다 — 메인 스토리 분기(R8)에는 노드 그래프가 필요하다(7.5절). 위처럼 `NextId` 기반으로 확장한다.

### 6.2. 시작/종료 플래그와 저장 시점 (R9)

- **시작 플래그**: 런타임 전용(메모리). 이벤트 진입 시 세워 중복 진입만 막는다. **디스크에 쓰지 않는다** — 자동 저장 금지 요구와, "중도 이탈 시 이벤트는 처음부터"라는 자연스러운 귀결.
- **종료 플래그**: 이벤트가 끝까지 진행됐을 때만 `SaveData`에 기록.
- **저장 시점**: 이벤트 진행 중에는 어떤 경로로도 저장이 일어나지 않게 하고(7.1절), 종료 시점에 결과(호감도 델타, 분기 플래그, 완료 기록)를 한 번에 `SaveData`에 반영한 뒤 **1회 저장**한다. ※ 이 "종료 시 1회 저장"을 허용할지는 결정 필요(Q-E1). 허용하지 않으면 분기 결과가 다음 수동 저장까지 디스크에 없어, 그 사이 강제 종료 시 이벤트 자체가 없던 일이 된다.

### 6.3. SaveData 신설 필드

```csharp
// SaveData (슬롯별 파일이므로 멀티 슬롯은 자동 충족)
public List<string> EventCompletedIds = new();   // 종료 플래그: "EVT_MAIN_001"
public List<string> EventChoiceHistory = new();  // "EVT_MAIN_001:3:1" (이벤트:노드:선택) — TalkChoiceHistory와 동일 포맷
public List<string> StoryFlags = new();          // 분기 플래그: "MAIN_ROUTE_A" 등
```

- `SaveGame()` gather / load scatter / `SaveDataMigrator` backfill 3곳 모두 추가 (기존 규약).
- `EventChoiceHistory`는 `TalkChoiceHistory`처럼 상한 트림.
- 다회차: **회차의 정의(Q-E2)가 먼저다.** "슬롯별 독립 플레이"라면 위로 충분하고, "회차 인계(NG+)"라면 `PlaythroughCount`와 인계 규칙 필드가 추가로 필요하다.

---

## 7. 구조 검토 — 발견된 문제와 결정 사항

### 7.1. ★ 최대 리스크: "자동 저장 금지"는 기존 시스템과 충돌한다

이 코드베이스는 **곳곳에서 자동 저장을 일으킨다.** 확인된 것만:

- `DatingTimeManager.ModifyAffection()` — 호출 즉시 자동 저장 (YomiRoomManager 주석에 명시)
- `TryConsumeStamina()` — 그 자리에서 디스크 저장
- ChoiceTalk 종료 경로 — `SaveCurrentGame()` 직접 호출

따라서 이벤트 씬에서 **선택 직후 `ModifyAffection`을 부르면 그 순간 자동 저장이 터져 R9를 위반한다.** 구조적 해법은 하나뿐이다:

> **이벤트 진행 중에는 기존 매니저의 상태 변경 API를 일절 호출하지 않는다.**
> 호감도 델타·플래그·완료 기록을 이벤트 러너가 메모리에 누적하고, [End]에서 한 번에 반영한다.

이 "지연 커밋" 규칙이 이 시스템의 제1 원칙이다. 위반하면 중도 이탈 시 "호감도는 올랐는데 이벤트는 미완료"라는 반쪽 상태가 디스크에 남는다.

### 7.2. 저장 안 하면 스컴이 가능하다 — 의도인지 확인 필요

진행 중 저장이 없으므로 **중요 분기에서 게임을 끄고 다시 하면 선택을 무한 재시도할 수 있다.** ChoiceTalk는 같은 문제를 "여는 즉시 소비 마커 저장"(TS1)으로 막았지만, 이벤트는 자동 저장 금지라 그 수단이 없다. 1회성 스토리 이벤트에서 재시도 허용은 보통 의도된 관용이지만, **메인 스토리 분기점에서도 허용할 것인지**는 기획 결정이다 (Q-E1과 연동 — 종료 시 1회 저장을 허용하면 "끝난 분기는 되돌릴 수 없다"가 성립한다).

### 7.3. 씬 신설 자체는 타당하나, 복귀 계약이 필요하다

배경·스탠딩·전용 UI로 화면 전체가 바뀌므로 오버레이보다 전용 씬이 맞다. 단 진입 지점이 둘(요미 방/월드맵)이므로 `EventSceneContext`에 **복귀 씬을 명시**해야 하고, 슬롯 소모·날짜 진행 같은 **진입 비용 처리는 호출자 몫**으로 못 박아야 한다(이벤트 러너는 대화만 안다). `LoadingScreenController.TargetSceneToLoad` 규약 준수.

### 7.4. 다회차의 정의가 저장 테이블을 결정한다 (Q-E2)

"여러 슬롯 + 다회차"가 (a) 슬롯마다 독립된 새 게임이라면 현행 슬롯 구조로 충분하다. (b) 클리어 후 인계(NG+)라면 `SaveData`에 회차 번호·인계 항목이 필요하고 `SaveDataMigrator` 백필도 커진다. **(b)인지 여부를 먼저 정하지 않으면 테이블을 두 번 만들게 된다.**

### 7.5. ChoiceTalk 데이터 구조는 분기를 못 한다

`TalkTopic.Nodes`는 선형 배열 + 무조건 다음 인덱스 진행이다. 메인 스토리 분기(R8)는 선택→다음 노드 지정이 필요하므로 6.1의 `NextId` 그래프 구조로 확장해야 한다. **기존 ChoiceTalk를 이 구조로 옮기지는 않는다** — 일상 대화는 선형으로 충분하고, 옮기면 세이브 호환·대사 전량 재작성이 따라온다. 두 시스템이 `TalkChoice` 문법(말투 규격)만 공유하고 데이터 타입은 분리한다.

### 7.6. 대사 데이터는 정적 C# 테이블로 (폰트 프리베이크)

`PrebakeTMPFont`는 .cs만 스캔한다. ScriptableObject로 가면 새 한글이 □가 된다 (`ChoiceEventFontPrepopulator`가 SO도 스캔하지만 Assets/Data·Resources 한정 + 별도 실행 필요). YomiTalkTopics 패턴(정적 테이블 + 프리베이크)이 검증돼 있으므로 그대로 따른다. 휴면 CSV 파이프라인(ScenarioCSVImporter) 부활은 작가 협업이 실제로 시작될 때 재검토(Q-E3).

### 7.7. 입력 레이어 충돌 — 고전적 버그 지점

"아무 영역 클릭 → 다음 대사"(R6)와 "선택지 클릭"(R7)은 같은 화면을 두고 경쟁한다. `ClickCatcher`는 [Choice] 상태에서 **반드시 비활성**해야 한다. 로그 패널이 열려 있을 때도 마찬가지. 상태기계(4.1)의 상태가 입력 레이어를 단독 결정하게 하고, UI 쪽에서 개별 판단하지 않는다.

### 7.8. 감정 에셋은 필드만 먼저 판다

감정 테이블은 추후라지만 **`EventNode.Emotion` 필드는 v1 데이터에 넣는다.** 나중에 넣으면 전체 이벤트 데이터를 한 번 더 손대야 한다. v1에서는 기본 표정 하나로 렌더하고, 스프라이트 매핑(호감도 단계 × 감정)만 테이블 확장으로 남긴다.

### 7.9. 요미 방 자동 저장 습성과의 공존

이벤트 씬 자체는 7.1로 해결되지만, **이벤트 진입 직전** 호출자(월드맵 데이트 버튼 등)가 슬롯 소모/비용 지불로 자동 저장을 일으키는 것은 허용된다(이벤트 밖이므로). 경계선은 "EventScene 로드 시작부터 [End] 커밋까지"로 명문화한다.

---

## 8. 구현 순서 제안

1. `EventNode`/`EventChoice`/`EventEmotion` + 샘플 이벤트 1편 (정적 테이블)
2. `EventRunner` (상태기계 + 지연 커밋 버퍼) — 매니저/UI 분리 규약 준수 (매니저가 UI 참조 금지)
3. `SaveData` 3필드 + gather/scatter + `SaveDataMigrator` 백필
4. 씬 빌더 (`FX Overdose/Build Event Scene`) + Build Settings 등록
5. UI (대사창·선택지·로그·ClickCatcher·SettingsMenuController P2P 변형 연결)
6. 진입/복귀 배선 (월드맵 데이트 → EventSceneContext → LoadingScene 경유)
7. 폰트 프리베이크 재실행 + 수동 시나리오 테스트 (중도 이탈 시 디스크 무변화 확인 포함)

---

## 9. 결정 필요 사항

| ID | 질문 | 권장 |
| --- | --- | --- |
| Q-E1 | 이벤트 **종료 시점의 1회 커밋 저장**을 허용하는가? (진행 중 금지는 확정) | **허용 권장** — 불허 시 분기 결과가 다음 수동 저장까지 휘발되고, 분기 스컴도 열린다 |
| Q-E2 | 다회차 = 슬롯별 독립인가, 회차 인계(NG+)인가? | 테이블 설계 선행 조건. NG+면 인계 항목 목록 필요 |
| Q-E3 | 이벤트 대사 데이터 소스 — 정적 C# 테이블 유지 vs CSV 파이프라인 부활 | v1은 정적 테이블. 작가 투입 시 재검토 |
