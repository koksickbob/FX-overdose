# D2. 이벤트 런타임 확장 구현 계획

작성일: 2026-09-16
상태: 설계 (미착수)
상위 문서: [P2_08_00_Story_Implementation_Breakdown.md](P2_08_00_Story_Implementation_Breakdown.md) · 선행: [D1](P2_08_10_D1_Stat_Progression_Core.md)
기준 문서: [scenario_통합.md](../P2_Story/scenario_통합.md) 2부 전체 · §4.1-4
관련 코드: [`Assets/Scripts/Events/Story/`](../../Assets/Scripts/Events/Story/) · [`EventSystem_Usage_Guide.md`](../P2_04_System/EventSystem_Usage_Guide.md)

---

## 1. 범위

기존 이벤트 런타임을 **스토리 이벤트 74건을 실을 수 있는 그릇**으로 확장한다. 뼈대는 이미 돌고 있으므로 새로 만드는 것이 아니라 **네 군데를 넓히는 작업**이다.

콘텐츠 변환(C1–C7)은 이 문서가 끝나야 시작할 수 있다. 데이터 스키마가 바뀌면 이미 변환한 챕터를 다시 손봐야 하기 때문이다.

---

## 2. 현행 런타임 (이미 되는 것)

| 기능 | 위치 |
| --- | --- |
| 노드 그래프 + `NextId` 분기, 종료(-1) | `EventDefinition.EventNode` |
| 선택지 → 호감도 델타 · 즉답 · 분기 · 세이브 플래그 | `EventChoice` |
| 배경 교체 (`BackgroundId`, null이면 유지) | `EventNode` |
| 요미 스탠딩 감정 24종 · `HideStanding`(CG 전용 이벤트) | `EventEmotion` / `EventDefinition` |
| 지문 표기(`※` 접두 → 타자기 없이 즉시 표시) | `EventView` |
| 오버레이 / 전용 씬 두 호스트 | `EventOverlayHost` · `EventSceneHost` |
| 중복 진입 가드 · 완료 이벤트 게이트 | `EventLauncher` |
| 지연 커밋(선택 결과를 버퍼에 쌓았다가 한 번에 반영) | `EventRunner.Commit` |
| 진행 기록 — `EventCompletedIds` · `EventChoiceHistory` · `StoryFlags` | `SaveData` |

---

## 3. 확장 항목

### 3.1 의존도 델타 — **최우선**

`EventChoice`에 호감도만 있고 **의존도가 없다.** 통합 문서의 모든 선택지는 `(호감도 ±N, 의존도 ±M)` 2축이므로, 이것 없이는 어떤 이벤트도 정확히 옮길 수 없다.

- `EventChoice`에 `Obsession` 필드 추가. 생성자 인자 순서는 `(text, affection, obsession, reply, nextId, setFlag)` — 기존 호출부는 샘플 1건뿐이라 지금 바꾸는 것이 가장 싸다.
- `EventRunner`에 `pendingObsession` 버퍼 추가, `SelectChoice`에서 누적, `Commit`에서 `DatingTimeManager.ModifyObsession` 호출.
- ⚠️ **저장 횟수**: `ModifyAffection`·`ModifyObsession` 모두 호출 즉시 디스크에 쓴다. 지금도 델타가 있으면 커밋 한 번에 쓰기 2회인데, 의존도까지 더하면 3회가 된다. **두 수치를 한 번에 반영하고 마지막에 한 번만 저장하는 경로**를 D1에 요청하거나, 커밋 구간에서 자동 저장을 잠시 끄는 방식 중 하나를 택한다.

### 3.2 선택지 5개 대응 — **조용한 데이터 손실**

`EventView.MaxChoiceButtons = 4` 이고 `ShowChoices`가 `i < MaxChoiceButtons` 로만 돈다. **선택지가 5개면 다섯째가 에러 없이 사라진다.**

챕터 4·5의 메인 이벤트는 대부분 5지선다다([옷가게-1], [놀이공원-1], [사무실], [집-혼자있는낮], [야시장-1], [와인바-1], [반지공방-1], [집-잠2] 등 — [추적표](P2_08_01_Event_Tracker.md) 참고).

- `MaxChoiceButtons`를 **5**로 올리고 버튼 프리팹/레이아웃 높이를 재검토한다.
- 동시에 **정의 개수가 버튼 수를 넘으면 에디터에서 즉시 실패**하도록 가드를 넣는다. 조용히 잘리는 것이 이 버그의 본질이다.

### 3.3 트리거 조건 (선행조건 메타)

현재 `EventLauncher.Play(eventId)`는 호출자가 "지금 이 이벤트를 틀어야 한다"를 이미 알고 있다고 가정한다. 74건을 월드맵에 얹으려면 **이벤트 쪽이 자기 조건을 들고 있어야** 한다.

`EventDefinition`에 조건 블록을 추가한다.

| 필드 | 의미 |
| --- | --- |
| `Chapter` | 발생 챕터 (D1의 `DatingChapter`) |
| `LocationId` | 발생 장소 (D3의 장소 ID) |
| `Kind` | `메인` / `사이드` / `체크포인트` |
| `RequiredFlags` / `ForbiddenFlags` | `StoryFlags` 전제 |
| `MinAffection` / `MinObsession` | 수치 전제 (CP4 저호감 분기 등) |
| `Repeatable` | 사이드 이벤트 반복 가능 여부 |

§3의 원칙 두 가지를 이 메타로 표현한다.

- **"처음 가는 장소의 이벤트는 고정, 이후 반복 진입은 랜덤 1–2개"** → 메인은 `Repeatable = false`, 사이드는 `true` + 장소별 풀에서 추첨.
- **"모든 장소를 한 번은 방문해야 주요 이벤트로 진입"** → 체크포인트는 해당 단계 메인 이벤트 전수 완료를 `RequiredFlags`로 요구한다.

선택 로직 자체(어느 이벤트를 고를지)는 D3의 장소 진입 처리에 둔다. D2는 **조건을 선언하고 판정하는 것**까지만 한다.

### 3.4 장면 전환과 저호감 변형

- 통합 문서의 `***`(장면 전환)는 **배경 교체 + 페이드**로 옮긴다. `BackgroundId`가 이미 있으므로 전용 필드는 필요 없고, `EventView`에 짧은 페이드 한 번만 추가한다.
- `▶ 선택` / `【①】` 표기는 순수 문서 표기이며 런타임 개념이 아니다. 각각 `Choices` 배열과 분기 노드로 흡수된다.
- **CP4 저호감 변형**(호감도 관심 이하면 대사·연출이 통째로 다름)은 분기 노드로 처리하지 말고 **별도 `EventDefinition` 2벌**로 만들고 `MinAffection`으로 고른다. 한 그래프 안에 두면 노드 번호가 두 배가 되고 퇴고 시 어느 쪽 대사인지 추적이 안 된다.

---

## 4. 데이터 변환 규약 (C1–C7이 따를 규칙)

콘텐츠 문서는 전부 이 규약대로 `EventNode` 배열을 만든다.

- **이벤트 ID**: `EVT_{챕터}_{장소}_{일련}` (예: `EVT_CH2_CVS_01`). ⚠️ 세이브에 남으므로 **재사용·개명 금지**.
- **노드 분할**: 문단 하나 = 노드 하나가 기본. 대사는 노드당 1–3줄.
- **화자**: 요미 대사는 `"요미"`, 서술·독백은 `null`, 지문은 `※` 접두.
- **감정**: 노드마다 24종 중 하나. 미지정 시 `Calm`이 아니라 **직전 노드 감정 유지**가 자연스러우므로, 장면이 바뀔 때만 명시한다.
- **선택지**: 25자 이내·반말·즉답 1줄 (`ChoiceTalk` 문법 그대로).
- **수치**: 통합 문서 `[선택: …]` 의 괄호 값을 그대로 옮긴다. **임의로 조정하지 않는다** — 밸런싱은 §4.1-5 분류표의 격차 값으로 검증한다.
- **호스트**: 기본 `Overlay`. 전용 씬은 CP 4건과 엔딩처럼 화면 전체가 바뀌는 것만.

> **텍스트는 `.cs` 하드코딩을 유지한다.** `EventCatalog`를 ScriptableObject로 옮기면 폰트 프리베이크(`Tools/Prebake All Scripts Text into Font`)가 `.cs`만 스캔하므로 새 한글이 □로 렌더된다. 챕터별로 `EventCatalog_Ch2.cs` 식 partial/정적 테이블로 나누고, **대사를 추가할 때마다 프리베이크를 다시 돌린다.**

---

## 5. 완료 판정

1. 의존도 델타가 있는 선택지를 고르면 커밋 후 `DatingObsession`이 정확히 그만큼 움직이고, **디스크 쓰기는 1회**다.
2. 선택지 5개짜리 이벤트가 **다섯 개 모두** 표시된다. 6개를 넣으면 에디터에서 즉시 실패한다.
3. 챕터·장소·플래그 조건이 맞지 않는 이벤트는 `Play`가 false를 돌려준다.
4. 배경이 지정된 노드에서 페이드와 함께 배경이 바뀐다.
5. 샘플 이벤트가 아니라 **실제 이벤트 1건**([편의점-1])이 처음부터 끝까지 재생된다.

---

## 6. 하지 않는 것

- 이벤트 데이터의 ScriptableObject 이전. 4장 경고 참고.
- 자동 재생·스킵·백로그 확장. 기존 `EventView` 기능으로 충분하며, 요구가 실제로 나온 뒤에 붙인다.
- 보이스·립싱크 훅. 계획에 없다.
- 이벤트 편집 에디터 툴. 74건은 손으로 옮기는 편이 툴을 만드는 것보다 싸다.
