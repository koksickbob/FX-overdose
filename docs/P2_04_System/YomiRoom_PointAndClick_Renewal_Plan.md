# 요미의 방 포인트 앤 클릭 리뉴얼 계획서

작성일: 2026-09-15
상태: 계획 확정 (미결 사항 답변 반영, 착수 전)
관련 문서: [P2_03_YomiRoom_TopDown_Interaction_Draft.md](../P2_02_Worldbuilding/P2_03_YomiRoom_TopDown_Interaction_Draft.md) (리뉴얼 완료 시 폐기) · [P2_05_YomiRoom_Modal_UI_Spec.md](../P2_05_UI_and_Art/P2_05_YomiRoom_Modal_UI_Spec.md) · [Settlement_In_YomiRoom_Plan.md](Settlement_In_YomiRoom_Plan.md) · [Affection_Tier_Table.md](Affection_Tier_Table.md) · [YomiRoom_ChoiceTalk_System_Plan.md](YomiRoom_ChoiceTalk_System_Plan.md)

---

## 1. 목표

요미의 방 씬의 UI와 조작을 전면 교체한다.

| | 현행 (탑다운) | 변경 후 (포인트 앤 클릭) |
| --- | --- | --- |
| 조작 | WASD로 요미를 걸려 오브젝트 반경(1.45) 안에서 좌클릭 | 방 배경 위 오브젝트 UI를 **직접 클릭** |
| 화면 | 방(좌 절반 카메라) + 대화 패널(우) 분할 | **방 전체 화면** + 대화 패널 **오버레이** |
| 오브젝트 | 배경 그림(`RoomLeft.png`)에 박힌 가구 + 투명 콜라이더 | 방 UI 위에 **침대 / 컴퓨터 / 핸드폰 / 문** UI를 레이어로 덧댐 |
| 대화 | 우측 패널이 항상 떠 있음 | **핸드폰 클릭 시 대화 패널 등장** |
| 퇴장 | 방 하단 트리거에 닿는 즉시 월드맵 로딩 | 문 클릭 → **확인 모달** → 월드맵 |
| 배경 | 아침 배경 1장 고정 | **시간대(아침/점심/저녁/밤)별 배경 분기** |
| 요미 | 플레이어가 조작하는 걷기 캐릭터 | 방 중앙에 **T1 기본 에셋(`T1/Calm`)**. 클릭 상호작용은 추후 |

UI 에셋은 아직 없다. 그래서 **시스템을 먼저 만들고, 에셋이 들어오면 임시 씬에서 테스트한 뒤 본 씬에 교체**하는 순서로 진행한다.

---

## 2. 현행 구조 요약 (교체 대상 파악)

현재 방은 씬 파일이 아니라 **런타임 빌더가 만든다.** `YomiRoomScene.unity`에 저장된 옛 버튼 메뉴 캔버스는 Play 시 파괴되고, 그 자리에 탑다운 방이 생성된다.

- 진입점: `DatingSimSceneBuilder` (`[RuntimeInitializeOnLoadMethod]`, 씬 이름이 `YomiRoomScene`일 때) → `YomiRoomTopDownPrototype.Build(scene)` (`Assets/Scripts/DatingSim/UI/YomiRoomTopDownPrototypeBuilder.cs`)
- 규칙·상태: `YomiRoomManager` — **UI를 전혀 참조하지 않으며 모든 행동을 API로 노출**한다. 리뉴얼에서 건드리지 않는다.

| 행동 | 현행 트리거 | 호출 API |
| --- | --- | --- |
| 거래 시작 (PC) | 근접 + 좌클릭 → 모달 확인. 포지션 보유 시 차단 | `YomiRoomManager.StartTrading()` |
| 잠깐 휴식 (침대) | 근접 + 좌클릭 → 모달 1번 버튼 (슬롯 1 소모, 체력 +10) | `TryRest()` |
| 오늘은 여기까지 (침대) | 모달 2번 버튼 → 방에서 정산 | `TrySleep()` |
| 월드맵 이동 | 하단 출구 트리거 접촉 즉시 | `MoveToWorldMap()` |
| 대화 | 우측 대화 패널 토글 버튼 (오브젝트 무관) | `TryStartTalk()` / `CloseTalk()` |

- 핸드폰·문 스프라이트(`Resources/DatingSim/YomiRoom/Morning/Objects/Phone.png`, `Door.png`)는 있으나 **어떤 코드도 쓰지 않는다.**
- 요미 T1 스프라이트: `Resources/DatingSim/Emotions/Sprites/T1/*.png` (감정 24종). 현재 이벤트 씬(`EventView`)에서만 로드한다.
- 시간대 판정은 이미 있다: `YomiTalkTopics.TimeOfSlot(remainingSlots)` → `TalkTime.Morning`(슬롯 ≥5) / `Noon`(4) / `Evening`(3) / `Night`(≤2). 슬롯 변화는 `DatingTimeManager.OnTimeSlotChanged`로 통지된다.
- `YomiRoomDialogueUI.Start()`가 매니저 이벤트 구독과 입장 인사(`TryGreetOnEnter`)를 함께 수행한다 → 3.5 주의 사항.
- 방 위치를 저장하는 세이브 필드는 없다 → **세이브 형식 변경 없음.**

---

## 3. 설계

### 3.1 원칙

1. **`YomiRoomManager`는 그대로 둔다.** 새 UI는 기존 API를 호출하는 입력층만 교체한다 (매니저 → UI 참조 금지 규칙 유지).
2. **유지하는 UI**: `YomiRoomDialogueUI`(대화 패널 — 표시 방식만 오버레이로 변경), `YomiRoomVerticalStatusUI`(체력·시간·호감도·집착 카드), 기존 확인 모달. 이미 매니저 이벤트 기반이라 조작 방식과 무관하다.
3. **에셋 없이도 돌아간다.** 스프라이트가 없으면 반투명 색상 사각형 + 이름 라벨로 대체해, 지금 바로 클릭 흐름을 검증할 수 있게 한다.
4. 새 컴포넌트는 최소로. 클릭·호버는 Unity 기본 `Button`으로 처리하고 별도 입력 시스템을 만들지 않는다. 시간대 판정도 기존 `YomiTalkTopics.TimeOfSlot`을 재사용한다.

### 3.2 화면 구성 (UGUI 단일 캔버스, 전체 화면)

```
Canvas_YomiRoomPointClick        (Screen Space Overlay, 전체 화면)
├─ RoomArea                      (화면 전체로 늘림)
│  ├─ RoomBackground   Image     방 배경 (시간대별 교체 — 3.4)
│  ├─ Hotspot_Bed      Button    침대
│  ├─ Hotspot_Computer Button    컴퓨터
│  ├─ Hotspot_Phone    Button    핸드폰
│  ├─ Hotspot_Door     Button    문
│  └─ Yomi             Button    요미 (T1/Calm, 중앙)
├─ StatusCards                   YomiRoomVerticalStatusUI (방 위 좌측 오버레이)
├─ DialoguePanel                 YomiRoomDialogueUI (방 위 오버레이, 기본 숨김 — 3.5)
│  └─ CloseButton                패널 닫기 (신규)
├─ FeedbackText                  실패 사유 안내
└─ InteractionModal              기존 모달 (침대·컴퓨터·문 공용)
```

- 탑다운용 카메라 뷰포트 분할(`rect 0,0,0.5,1`), 벽 블록, 콜라이더, 출구 트리거, 플레이어 오브젝트는 전부 생성하지 않는다.
- 그리기 순서(뒤 → 앞): 배경 → 오브젝트·요미 → 상태 카드 → 대화 패널 → 피드백 → 모달.

### 3.3 핫스팟

**컴포넌트** — `YomiRoomHotspot` (enum 필드 하나를 가진 마커)

```csharp
public enum YomiRoomHotspotType { Bed, Computer, Phone, Door, Yomi }
```

- 각 핫스팟은 `Image` + `Button`. 호버 강조는 `Button`의 Color Tint(에셋이 오면 Sprite Swap)로 처리한다.
- **불규칙한 외곽선 클릭 판정**: `Image.alphaHitTestMinimumThreshold = 0.1f`. 투명 영역 클릭은 무시된다. (텍스처 Import 설정에서 **Read/Write Enabled** 필요 — 에셋 수령 체크리스트에 포함)
- 다음 상태에서는 핫스팟 클릭을 무시한다 — 현행 `RoomBusy` 가드를 옮기고 조건을 추가한다.
  - `YomiRoomState != Idle` (대화 중·응답 중·휴식 중·전환 중)
  - 모달이 열려 있음
  - **대화 패널이 열려 있음** (오버레이 뒤 오브젝트 오클릭 방지. 패널의 `blocksRaycasts`로 자연히 막히는 영역 외 부분까지 코드로 가드)

**행동 매핑** — `YomiRoomHotspotController` (현행 `YomiRoomTopDownController` 대체)

| 핫스팟 | 클릭 시 | 비고 |
| --- | --- | --- |
| 침대 | 모달: `잠깐 눈 붙이기`(`TryRest`) / `오늘은 여기까지`(`TrySleep`) | 슬롯 부족 시 1번 버튼 비활성 — 현행 로직 이관 |
| 컴퓨터 | 모달 확인 → `StartTrading()` | 포지션 보유 차단 — 현행 로직 이관 |
| 문 | **모달 확인** → `MoveToWorldMap()` | 오클릭 방지. 버튼 1개(`나가기`) + 취소 |
| 핸드폰 | **대화 패널 열기** | 대화 시작은 패널 안의 기존 `자유대화` 토글 버튼이 담당 (3.5) |
| 요미 | 아무 동작 없음 | **추후 추가.** 클릭 수신만 연결해 두고 핸들러는 비워 둔다 |

`ConfirmInteraction()` / `SecondaryInteraction()` / 포지션 확인 / Escape로 모달 닫기는 `YomiRoomTopDownController`에서 **옮겨오되 근접 판정·이동 코드는 버린다.** Escape는 모달 → 대화 패널 순서로 닫는다.

### 3.4 에셋 슬롯 · 시간대 배경 분기

빌더가 아래 경로를 `Resources.Load<Sprite>`로 읽고, **없으면 플레이스홀더**로 그린다. 에셋 교체 = 파일을 해당 경로에 넣기.

| 슬롯 | 경로 (`Assets/Resources/` 기준) | 플레이스홀더 |
| --- | --- | --- |
| 방 배경 | `DatingSim/YomiRoom/PointClick/{Time}/Room` | 시간대별 단색 (아침 밝은 톤 → 밤 어두운 톤) + 시간대 라벨 |
| 침대 | `DatingSim/YomiRoom/PointClick/Bed` | 반투명 사각형 + "침대" |
| 컴퓨터 | `DatingSim/YomiRoom/PointClick/Computer` | 반투명 사각형 + "컴퓨터" |
| 핸드폰 | `DatingSim/YomiRoom/PointClick/Phone` | 반투명 사각형 + "핸드폰" |
| 문 | `DatingSim/YomiRoom/PointClick/Door` | 반투명 사각형 + "문" |
| 요미 | `DatingSim/Emotions/Sprites/T1/Calm` (기존 에셋) | — |

`{Time}` = `Morning` / `Noon` / `Evening` / `Night` (`YomiTalkTopics.TalkTime` 이름 그대로).

**배경 분기 동작**
- 시간대 판정: `YomiTalkTopics.TimeOfSlot(DatingTimeManager.CurrentTimeSlot)` — 대화 토픽과 같은 판정을 써서 "밤 대사인데 아침 배경" 같은 불일치를 막는다.
- 갱신 시점: 씬 진입 시 1회 + `DatingTimeManager.OnTimeSlotChanged` 구독 (방 안에서 `TryRest`로 슬롯이 줄면 즉시 배경이 바뀐다). 구독은 배경을 가진 컴포넌트가 `OnDestroy`에서 해제한다.
- 폴백: 해당 시간대 배경이 없으면 `Morning` → 그것도 없으면 플레이스홀더. 에셋이 시간대별로 순차 납품돼도 깨지지 않는다.
- **오브젝트 레이어는 1차에서 시간대 분기하지 않는다.** 밤 조명이 오브젝트까지 달라져야 하면 같은 규칙(`PointClick/{Time}/Bed` 우선, 없으면 공용 경로)으로 확장한다 — 에셋이 그렇게 들어올 때 추가.

**납품 형식 (확정)**: 발주 명세서 [P2_05_YomiRoom_PointClick_Asset_Order_Spec.md](../P2_05_UI_and_Art/P2_05_YomiRoom_PointClick_Asset_Order_Spec.md)에서 **배경·오브젝트 전부 1920×1080 동일 캔버스, 트리밍 금지**로 정했다. 모든 핫스팟을 배경 크기로 겹쳐 늘리기만 하면 되므로 좌표 표가 필요 없다. 클릭 판정은 알파 히트 테스트가 처리한다. 원근 구조(소실점·요미 발 위치·UI 금지 영역)도 명세서 §4를 따른다.
전체 화면이므로 배경은 **16:9 기준**으로 받고, 다른 비율에서는 `Image.preserveAspect` + 화면을 덮도록 확대(가장자리 잘림 허용)한다. 오브젝트 레이어도 배경과 같은 RectTransform 안에 두어 함께 스케일되게 한다.

### 3.5 대화 패널 오버레이

- **숨김은 `SetActive(false)`가 아니라 `CanvasGroup`**(`alpha 0`, `interactable false`, `blocksRaycasts false`)으로 한다.
  `YomiRoomDialogueUI.Start()`가 이벤트 구독과 입장 인사를 수행하므로, 비활성으로 시작하면 `Start`가 핸드폰을 누를 때까지 실행되지 않아 **입장 인사·`OnActionFailed` 사유가 유실**된다. CanvasGroup 방식이면 인사는 로그에 쌓여 있다가 핸드폰을 열 때 보인다.
- 열기: 핸드폰 핫스팟. 닫기: 패널의 `CloseButton` 또는 Escape.
- **대화 진행 중(`Chatting`/`Responding`)에는 닫기 버튼 비활성.** 대화를 끝내려면 기존 `대화 종료` 토글을 쓴다 — 선택지 도중 패널만 닫혀 `YomiRoomState`가 `Chatting`에 묶인 채 핫스팟이 전부 잠기는 상황을 막는다.
- 대화 종료(`OnTalkFinished`) 후에도 패널은 자동으로 닫지 않는다 — 요미의 마무리 대사를 읽을 시간을 준다. 사용자가 직접 닫는다.
- `OnActionFailed` 사유(체력 부족, 오늘 대화 완료 등)는 현행대로 대화 패널 지문 채널에 찍힌다. 패널이 닫힌 상태에서 발생하는 실패(침대·컴퓨터)는 `FeedbackText`로 보인다.
- 요미 클릭 상호작용이 추후 설계되면, 대화 진입 경로를 핸드폰과 요미 중 어디로 둘지 그때 다시 정한다 (현재는 핸드폰 단일 경로).

### 3.6 빌더 구조

- `YomiRoomTopDownPrototype.Build`를 대체하는 `YomiRoomPointClickBuilder.Build(scene)`를 새로 만든다. 모달·상태 카드·대화 패널 생성부는 기존 빌더의 헬퍼를 재사용하고, 대화 패널·상태 카드는 좌우 분할 위치에서 **전체 화면 기준 오버레이 앵커**로 옮긴다.
- **주의**: 편의점 빌더(`ConvenienceStorePrototypeBuilder`)가 `YomiRoomTopDownPrototype`의 헬퍼(`CreateBlock`, `LoadWalkFrames`, `CreateCanvas` 등)와 `YomiTopDownWalkAnimator`, `YomiWalkSheet`를 공유한다. 탑다운 코드 삭제 시 이 헬퍼들은 **남기거나 공용 위치로 옮긴다.**
- `DatingSimSceneBuilder`의 씬 이름 분기에 테스트 씬 이름을 추가해, 같은 빌더가 두 씬에서 돌게 한다.

### 3.7 확정 사항 (미결 사항 답변)

| # | 질문 | 결정 | 반영 위치 |
| --- | --- | --- | --- |
| 1 | T1 기본 에셋 | `T1/Calm` 사용 | 3.4 |
| 2 | 화면 레이아웃 | 방 전체 화면 + 대화 패널 오버레이 | 3.2, 3.5, 3.6 |
| 3 | 문 클릭 동작 | 확인 모달 추가 | 3.3 |
| 4 | 핸드폰 기능 | 클릭 시 대화 패널 등장 | 3.3, 3.5 |
| 5 | 대화 진입 경로 | 4번에 귀속 — 핸드폰으로 패널을 열고 패널 안에서 대화 시작 | 3.5 |
| 6 | 시간대별 배경 | 배경 분기 필요 — 아침/점심/저녁/밤 4종 | 3.4 |

---

## 4. 작업 단계

### Phase 1 — 시스템 구축 (에셋 없음)

1. `YomiRoomHotspot`, `YomiRoomHotspotController` 작성 (3.3).
2. 시간대 배경 교체 로직 작성 (3.4).
3. 대화 패널 오버레이화 — CanvasGroup 숨김, 닫기 버튼, 대화 중 닫기 잠금 (3.5).
4. `YomiRoomPointClickBuilder` 작성 — 플레이스홀더로 전체 화면 구성 (3.2, 3.6).
5. 테스트 씬 생성 메뉴 추가: `FX Overdose/Build YomiRoom PointClick Test Scene` → `Assets/Scenes/DatingSim/YomiRoom_PointClick_Test.unity` (**빌드 설정에 넣지 않음**). `DatingSimSceneBuilder`가 이 씬 이름에서도 새 빌더를 실행하게 한다.
6. 본 씬(`YomiRoomScene`)은 **아직 탑다운 빌더 그대로** 둔다.

완료 기준
- `dotnet build "Assembly-CSharp.csproj"` / `"Assembly-CSharp-Editor.csproj"` 통과
- 테스트 씬에서 플레이스홀더 핫스팟 클릭 → 3.3 표대로 동작
  - 컴퓨터: 거래 진입 (포지션 보유 시 차단)
  - 침대: 휴식 시 슬롯·체력 변화, 정산
  - 문: 확인 모달 → 월드맵 이동, 취소 시 방 유지
  - 핸드폰: 대화 패널 등장 → 입장 인사가 로그에 보임 → 대화 진행 → 대화 종료 → 패널 닫기
  - 요미: 무반응
- 휴식으로 슬롯이 줄면 배경 플레이스홀더가 즉시 다음 시간대로 바뀜 (아침 → 점심 → 저녁 → 밤)
- 대화 진행 중 닫기 버튼 비활성, 모달·패널 열림 상태에서 핫스팟 클릭 무시
- 새 한글 문자열 추가 후 `Tools/Prebake All Scripts Text into Font` 실행 (□ 깨짐 방지)

### Phase 2 — UI 에셋 수령 → 임시 씬 테스트

UI가 들어올 때마다 반복한다.

1. 에셋을 3.4 경로에 배치. Import 설정: Sprite (2D and UI), **Read/Write Enabled** (알파 히트 테스트용).
2. 테스트 씬에서 확인:
   - 배경·오브젝트 레이어 정렬이 어긋나지 않는가
   - 시간대 4종 배경 모두에서 오브젝트 레이어가 어색하지 않은가 (밤 배경 + 낮 조명 오브젝트 → 오브젝트 분기 필요 여부 판단, 3.4)
   - 투명 영역 클릭이 무시되고 오브젝트 외곽선대로 클릭되는가
   - 호버 강조가 보이는가
   - 대화 패널 오버레이가 요미·핫스팟을 가려 조작을 방해하지 않는가
   - 16:9 외 해상도(16:10, 21:9)에서 배치가 무너지지 않는가
3. 좌표 표 방식(잘린 PNG)이면 이 단계에서 좌표를 확정한다.

### Phase 3 — 본 씬 교체

1. `DatingSimSceneBuilder`의 `YomiRoomScene` 분기를 새 빌더로 전환.
2. 전 경로 회귀 확인: 새 게임 1일차 진입 / 아침 복귀(`GameManager.ReturnToYomiRoomForNewMorning`) / 월드맵 귀환(`WorldMapManager.ReturnToRoom`) / 세이브 이어하기(`ResumableScenes`) / 방 정산·게임오버 UI(`RoomSettlementUIBootstrap`).
3. 월드맵에서 슬롯을 쓰고 돌아왔을 때 배경이 해당 시간대로 뜨는지 확인.
4. 요미 상호작용이 붙기 전까지 요미 핫스팟은 무반응 상태로 출시 가능.

### Phase 4 — 정리

| 대상 | 처리 |
| --- | --- |
| `YomiRoomTopDownController`, `YomiRoomInteractable`, `YomiRoomExitTrigger`, `YomiRoomTopDownHUD`(미사용) | 삭제 |
| `YomiRoomTopDownPrototype.BuildRoom` / `BuildPlayer` / `BuildInteractables` | 삭제. 편의점이 쓰는 헬퍼는 유지 (3.6) |
| `YomiTopDownWalkAnimator`, `YomiWalkSheet.png` | **편의점이 사용 중 → 유지** |
| `DatingSimSceneBuilder.cs:216` 썸네일의 `Morning/RoomLeft` 참조 | 새 배경 경로로 교체 또는 유지 판단 |
| `YomiRoomUIController`, `Assets/Editor/YomiRoomTestSceneBuilder.cs`, `YomiRoom_Test.unity`, `DatingSimSceneBuilder` 72–116행 죽은 코드 | 옛 버튼 메뉴 잔재. 삭제 |
| `YomiRoomScene.unity`에 저장된 옛 `Canvas_YomiRoom` | 삭제 (런타임에 어차피 파괴됨) |
| `P2_03_YomiRoom_TopDown_Interaction_Draft.md` | `_Deprecated` 표기 |
| `Refactored_Architecture_Master.md` | 구조 변경 기록 추가 |
| `CLAUDE.md` / `AGENTS.md` | `YomiRoom_Test.unity` 설명을 새 테스트 씬으로 갱신 (두 파일 동기화) |

---

## 5. 영향 범위

| 영역 | 영향 |
| --- | --- |
| `YomiRoomManager` API·상태 머신 | 없음 |
| `SaveData` / `SaveDataMigrator` | 없음 (방 위치 필드가 원래 없고, 배경은 슬롯에서 매번 계산) |
| 시간 슬롯·체력·호감도 규칙 | 없음 (같은 API 호출) |
| `YomiRoomDialogueUI` | 표시 방식 변경 — CanvasGroup 숨김·닫기 버튼·대화 중 닫기 잠금 추가. 대화 로직은 불변 |
| `YomiRoomVerticalStatusUI` | 위치만 전체 화면 기준으로 재배치 |
| `YomiTalkTopics.TimeOfSlot` | 읽기 전용 재사용 (변경 없음) |
| 편의점 씬 | 공용 헬퍼·걷기 애니메이터 유지 필수 → Phase 4에서 확인 |
| 폰트 아틀라스 | 새 한글 문자열 추가 시 프리베이크 재실행 |
