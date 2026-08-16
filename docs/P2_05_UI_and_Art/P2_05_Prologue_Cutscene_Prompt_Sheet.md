# P2_05 프롤로그(EVT_MAIN_001) — 스토리 분해 및 컷씬 CG 프롬프트 시트 (코덱스 전달용)

> **작성일**: 2026-08-15
> **대상**: **코덱스(Codex) — 구현 담당 AI 에이전트.** 이 문서 단독으로 작업이 성립하도록 작성됨. 대화 맥락 없이 이 문서와 저장소만으로 진행할 것.
> **용도**: 프롤로그 원고를 [EventScene 시스템 계획](../P2_04_System/EventScene_System_Plan.md)의 노드 규격으로 분해하고, 필요한 컷씬 CG를 발주 가능한 완성 프롬프트로 정리한다.
> **규약**: 프롬프트 블록은 [편의점 시트](P2_05_ConvenienceStore_Gemini_Prompt_Sheet.md)와 같은 방식 — **조립·치환 없이 블록 통째로 복사해서 붙여넣는다.** 캐릭터 디자인은 [P2_07 감정 스프라이트 계획](../P2_07_emotion/P2_07_Yomi_Emotion_Sprite_Prompt_Plan.md) 1절 실측 표를 계승하되, **비에 젖은 상태·다른 의상**이라는 프롤로그 전용 변형이 있다 (2절).
> **원고**: 오너 제공 프롤로그 원문 (2026-08-15). 본 문서의 대사·지문은 원문을 노드 단위로 분할한 것이며 내용 수정 없음(줄 나눔만).

---

## 0. 코덱스 작업 지시

### 0.1. 범위

| 구분 | 내용 |
| --- | --- |
| **할 것** | [이벤트 시스템 계획 v2](../P2_04_System/EventScene_System_Plan.md) **9절 구현 순서 0~8단계 전체** + 본 문서 3절의 노드 40개를 샘플 이벤트 `EVT_MAIN_001`의 정적 C# 테이블로 구현 |
| **하지 않을 것** | CG 이미지 생성 — 5절 프롬프트는 **이미지 생성 AI(제미나이) 발주용**이며 코덱스 몫이 아니다. 에셋이 없는 동안 단색 플레이스홀더(짙은 남색 등)로 배경 슬롯을 채우고 진행할 것 |

### 0.2. 반드시 지킬 프로젝트 규약 (CLAUDE.md 요약)

1. **코드·주석·인게임 텍스트 전부 한국어.**
2. **이벤트 진행 중 자동 저장 절대 금지** — 계획 문서 7.1절의 지연 커밋 규칙이 제1 원칙. `ModifyAffection` 등 기존 매니저의 상태 변경 API를 이벤트 중 호출하지 말 것.
3. 대사 데이터는 **정적 C# 테이블** (`YomiTalkTopics.cs` 패턴). ScriptableObject 금지 — 폰트 프리베이크가 .cs만 스캔한다.
4. 한글 대사 추가 후 `Tools/Prebake All Scripts Text into Font` 재실행 필요 (미실행 시 □ 렌더). 에디터 메뉴 실행은 오너 몫이므로 완료 보고에 명시할 것.
5. 씬 전환은 반드시 `LoadingScene` 경유 (`LoadingScreenController.TargetSceneToLoad`).
6. 씬은 손으로 만들지 말고 **에디터 빌더 메뉴**로 생성 (`FX Overdose/Build Event Scene` 신설).
7. `SaveData` 변경 시 3곳 세트: `SaveGame()` gather / load scatter / `SaveDataMigrator` backfill.
8. 컴파일 검증: `dotnet build "Assembly-CSharp.csproj" -v:m` + `dotnet build "Assembly-CSharp-Editor.csproj" -v:m` (P2P asmdef는 이번 작업과 무관하나 건드렸다면 함께 빌드).
9. 구조 변경 시 `docs/P2_04_System/Refactored_Architecture_Master.md`에 로그 추가.

### 0.3. 완료 기준

- [ ] 새 게임 시작 → LoadingScene → EventScene에서 N01~N39가 순서대로 재생되고 클릭으로 진행된다
- [ ] 전 노드 선택지 없음 — ClickCatcher만으로 진행
- [ ] 대사 로그 패널에 재생 순서대로 누적된다
- [ ] 진행 중 강제 종료 시 디스크에 아무 변화 없음 (자동 저장 0회 검증)
- [ ] N40 도달 시 `EventCompletedIds`에 `"EVT_MAIN_001"` 커밋 후 YomiRoom으로 복귀
- [ ] 이미 완료된 슬롯에서는 프롤로그가 다시 재생되지 않는다
- [ ] CG 미보유 상태에서 플레이스홀더로 전 구간 진행 가능

미결정 사항(계획 문서 9절 Q-E1~E3)은 **권장안대로 진행**하고 완료 보고에 채택 내역을 명시할 것.

---

## 1. 이벤트 개요

| 항목 | 값 |
| --- | --- |
| 이벤트 ID | `EVT_MAIN_001` (영구 식별자 — 세이브 기록용, 재사용 금지) |
| 제목 | 프롤로그 — 비 오는 밤의 만남 |
| 구조 | **완전 선형** — 선택지 없음. 전 노드 `Choices` 비움, `NextId` 체인 |
| **호스트** | **전용 씬** (`EventHostKind.Scene`) — 40노드 + CG 8장의 긴 이벤트이고, 타이틀 직후라 오버레이할 게임 화면이 없다 (계획 3.3절) |
| 진입 | 새 게임 시작 직후 (TitleScene → LoadingScene → EventScene) |
| 복귀 | `ReturnScene = "YomiRoomScene"` (프롤로그 종료 = 동거 시작). 계획 7.4절의 허용 목록 안에 있다 |
| 종료 커밋 | 2단계 커밋(계획 6.2절). `EventCompletedIds += "EVT_MAIN_001"` + **`LastSceneName = "YomiRoomScene"` 명시 기입** — 이게 없으면 프롤로그 직후 종료한 플레이어가 재접속에서 거래 화면으로 떨어진다 (계획 7.5절). 호감도 델타 0, 분기 플래그 없음 |
| 연출 방식 | **풀 CG 모드** — `HideStanding = true`. 요미는 CG 안에 그려져 있으므로 스탠딩을 겹치지 않는다. CG는 `EventNode.BackgroundId`로 교체 |

`EventNode.Emotion`은 전 노드 기본값 — 풀 CG라 스탠딩 감정 매핑을 쓰지 않는다.

### 시스템 계획 대비 추가 요구 1건

**배경(CG) 전환 연출** — 컷 전환 시 크로스페이드(0.5s 내외) 권장. 계획에 배경 전환 연출 규정이 없으므로 `EventView`에 훅을 마련한다. v1은 즉시 교체로 시작해도 무방. **페이드는 `Time.unscaledDeltaTime` 기준**이어야 한다 (계획 4.3절).

---

## 2. 프롤로그의 요미 — 기본 스킨과 다른 점

P2_07 실측 표의 캐릭터 디자인(흑발 초장발 웨이브·아호게·호박색 눈·창백한 피부·마른 체형)은 유지하되, 아래가 다르다. **CG 전 컷에서 일관 유지.**

| 부위 | 프롤로그 상태 | 근거(원문) |
| --- | --- | --- |
| 머리카락 | 비에 흠뻑 젖어 뺨·어깨에 들러붙음. 아호게도 축 처짐 | "물에 젖어 뺨에 빽빽하게 붙어버린 머리카락" |
| 눈 | 호박색이되 **생기 없음·짓무름**. 마지막 컷 전까지 광채 없음 | "눈동자는 짓물러 있었고", "생기 없는 눈" |
| 의상 | **몸에 맞지 않는 얇은 옷** — 기본 스킨(흰 티+돌핀 쇼츠)이 아님. 얇고 헐렁한 옷 1벌, 흠뻑 젖음 | "몸에 맞지 않는 얇은 옷가지" |
| 발 | 맨발, **고인 물로 더럽혀짐** | "고인 물로 더럽혀진 작은 맨발" |
| 입술 | 핏기 없이 떨림 | "핏기 없는 입술은 가련하게 떨리고" |

**플레이어(오빠) 묘사 규칙** — 데이팅 파트 관례 및 캐릭터 바이블 2.2절: **얼굴을 절대 그리지 않는다.** 뒷모습·팔·손·우산까지만 허용. 전 프롬프트의 네거티브에 강제되어 있다.

---

## 3. 스토리 노드 분해 (스크립트)

표기: `[화자]` — `나`(독백/대사), `요미`, `※`(지문). 각 노드는 `EventNode` 1개 = 클릭 1회 분량. 대사 1~3줄 규칙 준수. **한글 대사가 정적 C# 테이블에 들어가면 `Tools/Prebake All Scripts Text into Font` 재실행 필수.**

### 제1장 — 퇴근길 (CUT-01)

| 노드 | 화자 | 내용 |
| --- | --- | --- |
| N01 | 나 | 검은 아스팔트를 때리며 쏟아지는 장대비 소리가 귓가를 멍하게 울렸다. |
| N02 | 나 | 정신없는 퇴근길의 차가운 야경은 빗물에 번져 형체를 잃고 요란한 불빛만을 산란시키고 있었다. |
| N03 | 나 | 젖은 바짓단에서 올라오는 눅눅한 한기와 축축하게 몸을 짓누르는 피로감. 나는 우산을 고쳐 쥐며 평소보다 발걸음을 서둘렀다. |
| N04 | 나 | 그저 어서 빨리 자취방으로 돌아가 이 눅눅함에서 벗어나고 싶다는 생각뿐이었다. |
| N05 | 나 | 그런 평범한 퇴근길을 지나던 내가 '그 사람'을 발견한 건 정말 뜻밖의 일이었다. |

### 제2장 — 발견 (CUT-02)

| 노드 | 화자 | 내용 |
| --- | --- | --- |
| N06 | 나 | 가로등 불빛도 제대로 미치지 못하는 골목 어귀. 젖어버린 박스더미 옆, 장대비를 그대로 맞으며 작게 웅크려 있는 형태 하나. |
| N07 | 나 | 처음엔 누군가 버리고 간 옷가지인가 싶었다. 하지만 가까워질수록 그 작은 덩어리가 미세하게 떨리고 있다는 것이 눈에 들어왔다. |
| N08 | 나 | 물에 젖어 뺨에 빽빽하게 붙어버린 머리카락, 몸에 맞지 않는 얇은 옷가지, 그리고 고인 물로 더럽혀진 작은 맨발. |
| N09 | 나 | 아무도 신경 쓰지 않는 비 오는 밤의 길가 한구석에, 누군가가 세상으로부터 완전히 버려진 것처럼 주저앉아 있었다. |

### 제3장 — 우산 (CUT-03)

| 노드 | 화자 | 내용 |
| --- | --- | --- |
| N10 | 나 | "...저기요." |
| N11 | 나 | ※나는 홀린 듯이 발걸음을 멈추고 웅크린 사람의 머리 위로 우산을 기울였다. 쏟아지는 거센 빗소리가 둔탁한 천 소리로 바뀌며 순간 작은 침묵이 생겨났다. |
| N12 | 나 | "...괜찮으세요?" |

### 제4장 — 거부 (CUT-04)

| 노드 | 화자 | 내용 |
| --- | --- | --- |
| N13 | ※ | 내 목소리가 들리자, 웅크리고 있던 몸이 발작하듯 커다랗게 움찔거렸다. |
| N14 | 나 | 그 사람은 잘게 떨리는 손으로 얼굴을 감싸 쥐며 몸을 더 작게 말아쥐었다. 타인의 존재 그 자체에 극심한 공포를 느끼는 듯한 경계심. |
| N15 | 나 | 얼굴을 덮은 젖은 머리카락 사이로 드러난 눈동자는 짓물러 있었고, 핏기 없는 입술은 가련하게 떨리고 있었다. |
| N16 | 요미 | "저, 저리 가...!" |
| N17 | 요미 | "사람들... 다 똑같아... 요미를... 요미를 괴롭히려고... 또 비웃으려고 온 거잖아...! 다 필요 없어... 건드리지 마...!" |
| N18 | 나 | 그 사람은 자신을 '요미'라 부르며 방어기제처럼 작은 손을 내저었다. 그 작은 손끝은 가느다랗게 떨리고 있었고, 눈가에는 빗물인지 눈물인지 모를 액체가 계속해서 흘러내리고 있었다. |
| N19 | 나 | 세상 전체를 경멸하고 두려워하는 눈빛. 얼마나 많은 폭언과 냉대를 받았으면, 가만히 우산을 씌워주는 손길조차 자신을 베어낼 칼날로 받아들이는 걸까. |

### 제5장 — 손수건 (CUT-05)

| 노드 | 화자 | 내용 |
| --- | --- | --- |
| N20 | ※ | 나는 한숨을 나지막하게 내쉬며 젖은 어깨 위로 우산을 조금 더 밀어 넣어 주었다. 이대로 두면 오늘 밤을 넘기지 못하고 쓰러질 것처럼 위태로웠다. |
| N21 | 나 | "나쁜 사람 아니에요. 괴롭히려고 온 것도 아니고." |
| N22 | ※ | 조심스럽게 말을 건네며, 나는 호주머니에서 주섬주섬 손수건을 꺼내 앞으로 내밀었다. |
| N23 | 나 | "무슨 일이 있었는지는 모르겠지만... 도와드리고 싶어서 그래요. 이대로 있으면 위험하니까 일단 비는 피하고 생각해요." |

### 제6장 — 눈맞춤 (CUT-06) ★ 감정 절정

| 노드 | 화자 | 내용 |
| --- | --- | --- |
| N24 | 요미 | "......?" |
| N25 | 나 | 날 밀쳐내려 뻗은 손길이 허공에서 멈춰 섰다. 가시를 세운 것처럼 무작정 나를 밀어내려고 했던 것과는 다르게, 젖은 머리칼 사이의 눈동자가 나를 바라보고 있었다. |
| N26 | 나 | 그 순간, 시간의 유속이 잠시 나와 그 사람 사이에 머무르기 시작했다. 나는 씌워주던 우산이 기울어지는 것도 모른 채 그 사람의 생기 없는 눈을 물끄러미 마주했다. |
| N27 | 나 | 빗방울이 작게 고인 물에 떨어지며 그 사람의 눈에 생긴 것과 같은 파동을 일으켰다. 이윽고 떨어진 물방울이 다시 솟아 올랐다 잠잠해 질 때쯤, 눈을 마주치고 있다는 것을 인식했는지 곧바로 얼굴을 피했다. |
| N28 | 요미 | "정말로... 요미를... 안 괴롭혀...?" |
| N29 | 나 | 여전히 경계심이 서려 있었지만, 조금은 누그러진 가느다란 목소리. 그 뒤편에는 오갈 데 없는 절박함과 지푸라기라도 잡고 싶어 하는 아이 같은 순진함이 섞여 있었다. |
| N30 | ※ | 나는 쥐고 있던 손수건을 작은 손에 쥐여주며 담담하게 답했다. |
| N31 | 나 | "남을 괴롭힐 만큼 한가하지 않아요. 날도 추운데 우선 근처 어디 비를 피할 곳이라도...." |
| N32 | 요미 | "...집" |
| N33 | 나 | "네?" |
| N34 | 요미 | "집으로... 다른 곳은 싫어." |
| N35 | 나 | "............." |

### 제7장 — 옷자락 (CUT-07)

| 노드 | 화자 | 내용 |
| --- | --- | --- |
| N36 | 나 | 어느새 가느다란 손가락이 내 옷자락 끝부분을 살그머니, 아주 살그머니 쥐어 왔다. |
| N37 | 나 | 금방이라도 부서질 것처럼 가엾은 움직임이었지만, 나는 이 움직임이 나를 따라가겠다는, 지금 이 사람이 할 수 있는 최대한의 의사 표현임을 알 수 있었다. |

### 제8장 — 귀갓길 (CUT-08)

| 노드 | 화자 | 내용 |
| --- | --- | --- |
| N38 | 나 | 나는 내 몸에 빗방울이 흘러내리는 것도 개의치 않은 채 우산을 받쳐 주었다. 작은 몸집이 우산 아래로 비틀거리며 천천히 몸을 일으키고는, 금방이라도 쓰러질 것 같은 발걸음으로 걷기 시작했다. |
| N39 | 나 | 비바람이 몰아치던 차가운 밤거리 한복판에서 나는 다른 말을 하지 않고 그 사람을 나의 작은 보금자리로 이끌었다. |
| N40 | — | `NextId = -1` (종료 → 커밋 → YomiRoom) |

---

## 4. CG 컷 리스트

총 8컷. **1차 제작 6컷**만 있으면 연출이 성립하고, 2차 2컷은 보강용이다 (해당 장은 1차분 CG를 유지한 채 진행 가능).

| 컷 | 파일명 | 장면 | 표시 구간 | 우선 |
| --- | --- | --- | --- | --- |
| CUT-01 | `EVT_MAIN_001_Cut01.png` | 비 내리는 밤거리 정경 (요미 없음) | N01~N05 | 2차 (없으면 CUT-02로 대체) |
| CUT-02 | `EVT_MAIN_001_Cut02.png` | 골목 어귀, 웅크린 실루엣 (원경) | N06~N09 | **1차** |
| CUT-03 | `EVT_MAIN_001_Cut03.png` | 우산을 씌워주는 순간 (근경) | N10~N12 | **1차** |
| CUT-04 | `EVT_MAIN_001_Cut04.png` | 얼굴을 감싸며 거부하는 요미 | N13~N19 | **1차** |
| CUT-05 | `EVT_MAIN_001_Cut05.png` | 손수건을 내미는 손 | N20~N23 | 2차 (없으면 CUT-04 유지) |
| CUT-06 | `EVT_MAIN_001_Cut06.png` | 올려다보는 눈맞춤 ★ | N24~N35 | **1차** |
| CUT-07 | `EVT_MAIN_001_Cut07.png` | 옷자락을 쥔 작은 손 | N36~N37 | **1차** |
| CUT-08 | `EVT_MAIN_001_Cut08.png` | 우산 아래 두 사람의 귀갓길 (후경) | N38~N39 | **1차** |

**공통 사양**

- 캔버스 **16:9, 1920×1080 이상** 생성. 풀프레임 배경이므로 **크로마키 없음** (편의점 시트와 다름).
- 스타일: 캐주얼 애니메 일러스트 (P2_07 96종과 동일 계열) + 시네마틱 조명. 픽셀아트 아님.
- 팔레트: 한색 야경 — 짙은 남색/슬레이트 블루/차가운 회색 기조, 원경 네온·가로등의 번진 난색 포인트만 허용.
- 각 컷 2~3장 후보 생성 → 1장 선별. 컷 간 요미 디자인 일관성 검수는 2절 표 기준.
- 저장 위치(제안): `Assets/Resources/DatingSim/Events/EVT_MAIN_001/`. 임포트: Sprite(Single), 압축 무손실 권장 — 풀스크린 CG라 압축 아티팩트가 눈에 띈다.

---

## 5. 완성 프롬프트

### [01] CUT-01 — 비 내리는 밤거리 정경 [생성]

```
Create a full-frame cinematic event CG illustration for a visual novel,
16:9 widescreen, casual anime illustration style, clean lineart, soft cel shading.
Muted cold night palette: deep navy, slate blue, cold gray; distant neon signs and
streetlights as blurred warm accents diffused by rain.

Subject: a city street at night in heavy rain, seen from the sidewalk.
- Torrential rain falling in visible streaks, black wet asphalt reflecting
  scattered city lights, light shapes dissolved and smeared by the rainwater.
- Commuters in the far distance hurrying with umbrellas, all as dark anonymous
  silhouettes only, out of focus.
- Mood: exhausting end of a workday, cold, damp, lonely.
- No main character in frame.

Do not include: readable text, signs with letters, watermark, logo, sunny sky,
daylight, bright cheerful colors, photorealistic style, 3d render, pixel art.
```

### [02] CUT-02 — 골목 어귀의 웅크린 실루엣 [생성]

```
Create a full-frame cinematic event CG illustration for a visual novel,
16:9 widescreen, casual anime illustration style, clean lineart, soft cel shading.
Muted cold night palette: deep navy, slate blue, cold gray. Heavy rain in visible streaks.

Subject: the mouth of a dark narrow alley at night, barely reached by streetlight.
- Beside a pile of rain-soaked collapsed cardboard boxes, one small figure sits
  huddled on the ground, knees hugged to chest, fully exposed to the pouring rain.
- The figure is a frail girl seen from a distance: very long messy black wavy hair
  soaked and plastered over her face and shoulders, a thin oversized off-white shirt
  that does not fit her, bare feet dirtied by the puddle water she sits in.
- Her face is hidden by wet hair; she reads almost like a bundle of discarded clothes
  at first glance, trembling slightly.
- Composition: viewer stands on the sidewalk looking into the alley; the girl is
  small in frame, lower third, surrounded by darkness and falling rain.

Do not include: readable text, watermark, logo, her face in detail, eye contact,
bright colors, daylight, other people, horror elements, blood, photorealistic, 3d, pixel art.
```

### [03] CUT-03 — 우산을 씌워주는 순간 [생성]

```
Create a full-frame cinematic event CG illustration for a visual novel,
16:9 widescreen, casual anime illustration style, clean lineart, soft cel shading.
Muted cold night palette: deep navy, slate blue, cold gray. Heavy rain at night.

Subject: first-person view of a young man tilting his umbrella over a huddled girl.
- Camera is the man's point of view, looking slightly down: only his arm and hand
  holding a dark umbrella are visible at the edge of the frame. His face is never shown.
- Under the umbrella's edge: a frail girl crouched on the wet ground next to soaked
  cardboard boxes, very long messy black wavy hair drenched and stuck to her cheeks,
  a single drooping strand of ahoge, thin oversized off-white shirt soaked through,
  small bare feet dirtied by puddle water, pale skin, bloodless trembling lips.
- Her head stays low, face mostly veiled by wet hair, not yet reacting.
- Rain drums on the umbrella canopy; directly beneath it a small dry pocket of
  stillness, rain continuing to pour all around.

Do not include: the man's face or body beyond his arm, readable text, watermark,
logo, smiling, bright colors, daylight, extra people, blood, horror, photorealistic,
3d, pixel art.
```

### [04] CUT-04 — 거부하는 요미 [생성]

```
Create a full-frame cinematic event CG illustration for a visual novel,
16:9 widescreen, casual anime illustration style, clean lineart, soft cel shading.
Muted cold night palette: deep navy, slate blue, cold gray. Heavy rain at night,
dark alley, faint streetlight from one side.

Subject: close view of a terrified frail girl shielding herself.
- A girl crouched on the wet ground, body curled as small as possible, one trembling
  hand half-covering her face, the other small hand weakly pushed out toward the
  viewer to shove them away.
- Very long messy black wavy hair completely drenched, plastered across her cheeks
  and over her face; between the wet strands one dull amber (golden-orange) eye is
  visible, red-rimmed and sore from crying, glaring with a mixture of fear and hostility.
- Pale skin, bloodless trembling lips, thin oversized off-white soaked shirt,
  rain or tears streaming down from her eyes, small dirty bare feet tucked under her.
- Emotion: extreme wariness of other human beings, a cornered small animal;
  pitiful rather than scary.
- Camera: slightly above her, the viewer's position, medium close-up.

Do not include: any part of the man, readable text, watermark, logo, knife, blood,
horror lighting, grin, bright colors, daylight, extra people, photorealistic, 3d, pixel art.
```

### [05] CUT-05 — 손수건을 내미는 손 [생성]

```
Create a full-frame cinematic event CG illustration for a visual novel,
16:9 widescreen, casual anime illustration style, clean lineart, soft cel shading.
Muted cold night palette: deep navy, slate blue, cold gray. Heavy rain at night,
under the small sheltered space of an umbrella.

Subject: first-person view of offering a folded handkerchief.
- Camera is the man's point of view: in the foreground his one hand extends a clean
  folded pale handkerchief toward a crouched girl; his other arm holds a dark
  umbrella at the frame's edge. His face is never shown.
- The girl beyond the handkerchief, slightly out of focus: drenched very long messy
  black wavy hair stuck to her cheeks, pale skin, thin oversized off-white soaked
  shirt, huddled small, peeking warily through wet strands of hair.
- The handkerchief is the focal point, dry and warm-toned against the cold wet scene.

Do not include: the man's face, readable text, watermark, logo, bright colors,
daylight, extra people, blood, horror, photorealistic, 3d, pixel art.
```

### [06] CUT-06 — 올려다보는 눈맞춤 ★ [생성]

```
Create a full-frame cinematic event CG illustration for a visual novel,
16:9 widescreen, casual anime illustration style, clean lineart, soft cel shading.
Muted cold night palette: deep navy, slate blue, cold gray. Heavy rain at night,
but directly around the girl the rain is blocked by an umbrella held from off-frame.

Subject: the emotional core cut — a drenched girl looks up at the viewer for the
first time.
- Medium close-up, camera at the viewer's (the man's) eye position looking slightly
  down; the girl's face tilted up toward the camera, making direct eye contact.
- Her eyes: large dull amber (golden-orange) eyes with almost no light in them,
  red-rimmed from crying, wavering with disbelief — as if seeing something she
  cannot understand.
- Very long messy black wavy hair soaked and parted just enough to reveal her face,
  a single drooping ahoge, pale skin, bloodless lips slightly parted,
  thin oversized off-white soaked shirt.
- One of her thin hands is frozen mid-air, half-extended, the push-away gesture
  stopped midway.
- In the foreground or lower corner: a small puddle where a falling raindrop makes
  a single clear ripple, echoing the wavering of her eyes.
- Time feels suspended: rain streaks softened, the world quiet around the two.

Do not include: the man's face or body, smiling, readable text, watermark, logo,
bright colors, daylight, extra people, blood, horror, heart-shaped pupils,
photorealistic, 3d, pixel art.
```

### [07] CUT-07 — 옷자락을 쥔 작은 손 [생성]

```
Create a full-frame cinematic event CG illustration for a visual novel,
16:9 widescreen, casual anime illustration style, clean lineart, soft cel shading.
Muted cold night palette: deep navy, slate blue, cold gray. Rainy night, under an umbrella.

Subject: extreme close-up of a fragile gesture — small fingers grasping the hem of
a man's coat.
- Frame filled mostly by the lower hem of a dark rain-damp coat or jacket worn by
  the man (his body only as fabric, no face, no skin above the hem).
- A girl's thin pale small hand reaches from the side and pinches the very edge of
  the hem, holding it timidly with just her fingertips, knuckles slightly trembling.
- Her wet black hair strands and the soaked sleeve of a thin oversized off-white
  shirt enter the frame's edge, identifying her without showing her face.
- Water droplets on the fabric, shallow depth of field, quiet and delicate mood:
  the smallest possible expression of "I will follow you."

Do not include: faces, readable text, watermark, logo, bright colors, daylight,
extra people, blood, horror, photorealistic, 3d, pixel art.
```

### [08] CUT-08 — 우산 아래 두 사람의 귀갓길 [생성]

```
Create a full-frame cinematic event CG illustration for a visual novel,
16:9 widescreen, casual anime illustration style, clean lineart, soft cel shading.
Muted cold night palette: deep navy, slate blue, cold gray; distant blurred warm
city lights. Heavy rain and wind at night.

Subject: two figures walking away down a rainy night street, seen fully from behind.
- A young man walks holding a dark umbrella tilted over the smaller figure beside
  him, his own shoulder left out in the rain getting wet. Back view only, face
  never visible.
- Beside and slightly behind him, a frail girl: very long soaked messy black wavy
  hair down her back, thin oversized off-white soaked shirt, small dirty bare feet
  on the wet asphalt, unsteady exhausted steps, one hand holding the hem of his coat.
- Wet asphalt mirrors their silhouettes and the city lights; rain streaks across
  the streetlight glow.
- Mood: quiet, forward-moving warmth inside a cold indifferent city — the story's
  first step.

Do not include: faces, front views of either character, readable text, watermark,
logo, bright colors, daylight, extra people, blood, horror, photorealistic, 3d, pixel art.
```

---

## 6. 검수 체크리스트

- [ ] 8컷(1차는 6컷) 전부에서 요미 디자인 일치: 흑발 초장발 웨이브(젖음) / 아호게 / 호박색 눈(생기 없음) / 창백한 피부 / 얇은 헐렁한 옷 / 더럽혀진 맨발
- [ ] 플레이어 얼굴이 단 한 컷도 보이지 않음 (뒷모습·팔·손·우산만)
- [ ] 컷 간 팔레트 통일 (한색 야경 + 번진 난색 포인트)
- [ ] CUT-06의 눈에 하트 하이라이트 등 T3/T4 연출이 섞이지 않음 — 프롤로그는 관계 0 시점
- [ ] 공포/호러 톤 없음 (캐릭터 바이블 4.5절 방어선)
- [ ] 텍스트·워터마크·로고 없음
- [ ] 임포트 후 EventScene에서 16:9 풀프레임 표시 확인
