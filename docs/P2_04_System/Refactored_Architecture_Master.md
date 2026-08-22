# FX Overdose 리팩토링 종합 마스터 (Refactored Architecture Master)

이 문서는 FX Overdose 전체 시스템 리팩토링 과정에서 변경된 아키텍처, 개선된 로직, 최적화 내역을 Phase별로 종합하여 기록하는 중앙 문서입니다.

## Phase 1: Core & Data Foundation (진행 중)

### 1. 개요
* **목표:** 게임의 핵심 사이클(시간, 턴, 정산)과 세이브 데이터를 관리하는 근본적인 뼈대의 결합도 감소 및 성능 안정화.

### 2. 주요 변경 사항 및 아키텍처 (작성 예정)

#### GameManager.cs
* **하드코딩 제거 (데이터화):** 
  * 기존 코드에 하드코딩되었던 스토리 컷씬/독백용 텍스트(`day1Monologue` 등)를 `[SerializeField]` 리스트로 추출하여 외부 노출.
  * 하드코딩된 일차별 페널티/지출 조건(`CalculateExpectedDeduction()`)을 `[System.Serializable] struct RegularDeductionEvent` 구조로 분리하고 인스펙터 리스트(`regularDeductions`)로 데이터 연동.
  * 보스 등장 및 패배 시 나오는 하드코딩 스트링 대사들을 배열(Format string)로 추출.
* **이벤트 기반 최적화:** 
  * 인게임 타이머와 정산 구조에서 기존 이벤트(`Action`) 구조가 온전히 작동하도록 코드 흐름 정리.

#### TraderStatus.cs
* **하드코딩 수치 캡슐화:** 
  * 체력/멘탈 감소 속도 계산 시 쓰이던 고정 소수점 연산 계수(예: `5.0f`, `0.15f`)를 Inspector용 속성(`baseSpeedScale`, `maxHealthDecreaseLimit`)으로 추출하여 유연성 부여.
* **퍼포먼스 개선 (캐싱 최적화):**
  * `Update()`나 게임 플레이 도중 빈번히 호출되는 `HasItem()`, `ConsumeItem()` 로직에서 매번 사용되던 `FindAnyObjectByType<Inventory>()`를 삭제하고, 내부적으로 `cachedInventory`를 참조하도록 캐싱 로직 도입.
* **읽기 전용 프로퍼티 패턴 유지:**
  * 외부 참조에 대해서는 화살표 함수(`=>`)를 통한 읽기 전용 접근을 강제.

#### 데이터 저장소 및 기타 코어
* **SaveLoadManager.cs:**
* **DynamicTimeRegulator.cs:**

---

## Phase 2: DatingSim Core Architecture

### 1. 개요
* **목표:** 기존 트레이딩 파트의 로직(실시간 시간/체력, 단일 텍스트 박스 기반 대화)과 완전히 독립된 미연시(DatingSim) 파트의 구조를 구축하고, 무한 대화 로깅에 대비한 최적화를 수행합니다.

### 2. 주요 변경 사항 및 아키텍처

#### DatingTimeManager.cs & SaveData.cs (Data/Logic Layer)
* **슬롯 기반 시스템 (Action Point):** 트레이딩의 실시간 타이머와 완벽히 분리된 `DatingTimeSlot`, `DatingStamina` 시스템 구축.
* **영구 보존 원칙 (SaveData):** 호감도(`DatingAffection`), 집착도(`DatingObsession`), 스토리 진행도(`StoryProgressStage`)를 모두 `SaveData`에 편입하여 세이브 무결성 확보.
* **뷰-로직 분리 (Observer Pattern):** 모든 데이터 변경 시 `Action` 이벤트를 방출하여 UI 컨트롤러가 매니저 종속 없이 화면을 갱신하도록 구성.

#### ~~DatingSimMemoryDB.cs & IMemoryRetriever (Memory Layer)~~ — **2026-08-12 제거됨**
* ~~**주제별 파편화 (Topic Sharding):** 대화 기록을 `MemoryTopic`에 따라 개별 JSON 파일(`SlotX_Memories_Romance.json` 등)로 분리 저장하여 디스크/메모리 부하를 방지.~~
* ~~**비동기 접근 강제:** 유니티 메인 스레드 블로킹 방지를 위해 모든 I/O를 `async/await` 기반의 비동기 Task로 강제.~~
* ~~**전략 패턴 확장성:** `IMemoryRetriever` 인터페이스를 도입하여, 현재의 `SimpleTopicRetriever`(단순 텍스트 추출) 구조를 향후 Vector RAG 시스템 기반의 `SemanticRetriever`로 무중단 교체 가능하도록 뼈대 설계 완료.~~
* → 자유 채팅 제거와 함께 `Memory/` 계층 전체 삭제. 상세: [3. 미연시 자유 채팅(LLM) 제거](#3-미연시-자유-채팅llm-제거-2026-08-12)

#### WorldMapManager.cs & Economic Linking (World Layer)
* **트레이딩-미연시 경제 통합:** 알바 보상 획득 및 데이트 자금 소모 시 트레이딩 코어인 `GameManager`의 `ChangeBalance`, `TrySpendBalance` API를 직접 호출하여 자금을 일원화.
* **데이터 분리 (Scriptable Structs):** `PartTimeJobData`, `DateCourseData` 구조체를 통해 알바/데이트 코스의 코스트(체력/슬롯/비용)를 인스펙터에서 리스트로 직관적으로 관리.

#### ~~DatingSimLLMController.cs (LLM Control Layer)~~ — **2026-08-12 제거됨**
* ~~**2-Layer 감정-호감도 분리 프롬프트:** 장기 상태(호감도/집착도)와 단기 상태(`DatingMood`)를 이원화하여 주입함으로써 멘헤라 성격의 입체성 확보.~~
* ~~**기억 왜곡 전처리 (Memory Distortion):** `ObsessionLevel`이 임계치를 넘으면 C# 백엔드 단계에서 LLM에 주입될 과거 기억 주변에 왜곡된 텍스트를 몰래 삽입.~~
* ~~**JSON 내적 독백 파싱:** `Thought`와 `Dialogue`를 분리하는 JSON 포맷을 강제하고, 한국어 검증기(Validator)를 결합하여 7B 로컬 모델의 불안정성을 방어.~~
* → 대체: `FXOverdose.DatingSim.Dialogue.IYomiDialogueProvider`. 상세: [3. 미연시 자유 채팅(LLM) 제거](#3-미연시-자유-채팅llm-제거-2026-08-12)

#### LoadingScreenController.cs (Transition Layer)
* **전역 씬 관리:** `TargetSceneToLoad` 하나로 대상 씬을 지정하고, 트레이딩 씬(`GameScene`/`tutorial`)에서는 차트 시스템 준비를 대기한 뒤 로딩 바를 100%로 마감.
* ~~**LLM 로드 시뮬레이션:** `RequireLLM` 플래그를 통해 Qwen2.5-7B 로컬 모델 로딩 완료를 로딩 바에 합산.~~ — **2026-08-12 제거됨**

---

### 3. 미연시 자유 채팅(LLM) 제거 (2026-08-12)

#### 3.1 배경
로컬 LLM(Qwen2.5-7B) 기반 자유 채팅은 챗봇의 한계를 벗어나지 못해 기획 의도를 담아내기 어려웠고, 수 GB 모델 상주 비용도 컸습니다.
**미연시 자유 채팅만** 제거하고 **트레이딩 파트의 `LLMSafeGenerator`(돌발 이벤트/일일 일기, Bllossom 3B)는 존치**합니다.

#### 3.2 아키텍처 변경
* **대화 공급자 훅 도입:** `FXOverdose.DatingSim.Dialogue.IYomiDialogueProvider` 신설. 호출부(`YomiRoomManager.ProcessUserChatInput`, `YomiRoomDialogueUI.SendCurrentMessage`)는 이 인터페이스만 참조하며, 현재 구현체는 자리 표시자 `PlaceholderDialogueProvider` 입니다. 대체 대화 시스템은 **구현체 교체만으로** 붙습니다.
* **UI 전면 보존:** 채팅 패널·입력창·전송 버튼·말풍선 로그·`자유대화` 버튼 및 관련 리소스는 **일절 삭제하지 않았습니다.** 기능(응답 생성)만 분리했습니다.
* **상태 머신 정리:** `YomiRoomState`에서 `LLMLoading` 삭제, `LLMProcessing` → `Responding`, `FreeChatting` → `Chatting` 으로 개명하여 LLM 색채 제거.
* **로딩 경로 단일화:** `LoadingScreenController.RequireLLM` 및 모델 로딩 대기 분기 삭제. 진행률은 씬 로딩 65% 단일 경로로 통합.
* **네임스페이스 소멸:** `FXOverdose.DatingSim.LLM` 제거. 하위의 시나리오 계층은 LLM 비의존 자산이므로 `FXOverdose.DatingSim.Scenario` (`Assets/Scripts/DatingSim/Scenario/`)로 이관 보존했습니다. 단, 현재 호출자가 없어 유휴 상태입니다.

#### 3.3 삭제 목록
| 대상 | 처리 |
| --- | --- |
| `DatingSimLLMController.cs` | 삭제 |
| `LLM/Memory/` (`DatingSimMemoryDB`, `IMemoryRetriever`, `MemoryTopic`, `SimpleTopicRetriever`) | 삭제 |
| `LLM/Scenario/` + `ScenarioManager.cs` | `DatingSim/Scenario/` 로 이관 (네임스페이스 변경) |
| `YomiRoomScene.unity` / `YomiRoom_Test.unity` | LLM·MemoryDB 컴포넌트 및 `LLMUnity.LLM` / `LLMAgent` 제거 |
| `LoadingScreenController.RequireLLM` | 필드 및 분기 삭제 |
| `Qwen2.5-7B-Instruct-Q4_K_M.gguf` (4.6GB) | 빌드에서 제외 대상 (로컬 파일 삭제는 별도 수행) |

#### 3.4 잔여 과제
* 대체 대화 시스템 기획 확정 → `IYomiDialogueProvider` 구현체 작성
* 대화 시 시간 슬롯 소모 및 호감도 지급 재설계 (현재 자리 표시자 상태에서는 둘 다 비활성)
* `ScenarioManager.StartNewScenario` / `IncrementTurn` 재연결

---

### 4. 돌발 선택 이벤트 시스템 리팩토링 (2026-08-12)

계획서: [ChoiceEvent_System_Refactoring_Plan.md](ChoiceEvent_System_Refactoring_Plan.md)
증상: 이벤트 본문과 요미 대사에 **LLM 프롬프트 안내 문구가 그대로 출력됨**

#### 4.1 근본 구조 변경 — 텍스트는 로직의 종속 산출물

기존에는 **로직(템플릿)을 뽑고 → 그 로직을 모르는 채 텍스트를 생성**했고, 텍스트 생성이 실패하면
템플릿을 통째로 버리고 **완전히 다른 하드코딩 이벤트로 폴백**했습니다. 즉 폴백이 로직 일관성을 깨뜨렸습니다.

이제 템플릿이 단일 진실 공급원입니다.

```
템플릿 선택 (로직 확정)
      │
      ├─ 텍스트 생성 시도 (LLM, 선택지 힌트를 프롬프트에 주입)
      │        ├─ 위생 검사 통과 → 생성분 사용
      │        └─ 기각/실패      → 템플릿 사전 작성 텍스트 사용
      │
      └─ 선택지 로직(LogicOptions) — 어느 쪽이든 그대로 유지
```

`ShowLLMChoiceDialog` → **`ShowTemplateEvent`** 로 개편. 텍스트 출처만 분기하고 로직은 불변입니다.
요미 대사는 `YomiDialogueDatabase`(810개, 검수 완료) → LLM 생성분 → 템플릿 사전 대사 → 기본 문구 순으로 조회합니다.

#### 4.2 신설

| 파일 | 역할 |
| --- | --- |
| `Assets/Scripts/AI/LLM/LLMOutputSanitizer.cs` | 출력 위생 검사기. 프롬프트 복창·안내문 마커·길이·한국어 비율을 검사해 한 항목이라도 실패하면 전체 기각 |
| `LLMGenerationStats` (동 파일) | 폴백률 계측. 10회 이상 시도에서 50% 초과하면 경고 (리스크 #5 판단 근거) |
| `Assets/Scripts/Editor/ChoiceEventDebugMenu.cs` | 강제 발생 / 텍스트 생성 N회 검증 / 자산 점검 메뉴 |
| `Assets/Scripts/Editor/MigrateEventTemplates.cs` | 기존 자산을 C3·C4 규격으로 이관 (생성기 재실행 아님) |
| `Assets/Scripts/Editor/GenerateTemplateFallbackText.cs` | 242개 템플릿 사전 작성 텍스트 초안 생성 |

#### 4.3 결함 수정 요약

| # | 결함 | 처리 |
| --- | --- | --- |
| C1 | 복창을 걸러내는 검증 없음 (`koreanCount >= 5`) | `LLMOutputSanitizer`로 교체. 정적 지시문 블록과 12자 이상 연속 일치 시 기각 |
| C2 | 프롬프트에 빈 JSON 스켈레톤 포함 | 완전히 채워진 few-shot 예시 1개로 교체 |
| C3 | `OverrideDurationSeconds`가 어디서도 안 읽힘 | 이벤트 쉴드에 실제 전달. **`OverrideMarketTrend`의 인자는 메서드 내부에서 30으로 덮어써져 아예 무시되고 있었음** — 인자를 되살리고 단위를 `durationInGameMinutes`로 정정 |
| C4 | 방향성 선택지 멘탈이 성공 시에만 적용되는데 데이터는 음수 | `MentalPenaltyOnFail`/`HealthPenaltyOnFail` 필드 신설. 성공=보상 / 실패=페널티로 분리 |
| C5·R1·R4 | 프리페치 경쟁 상태 | `CancellationTokenSource` + 세대 카운터. 완료 시 자신이 최신 세대일 때만 캐시에 기록 |
| R2 | 선택지 힌트를 계산만 하고 프롬프트에 미주입 | 동적 컨텍스트 블록에 실제 주입 |
| R3 | `LogicOptions[0..2]` 무방비 인덱싱 | `HasValidOptions` 검증 후 풀에 편입. **전수 검사 결과 242개 모두 3개 보유 — 실제 발생 사례는 없었음** |
| R5 | Safe 선택지가 트랩 판정을 받음 | 트랩 판정을 "베팅에 실패했을 때"로 한정. Safe/SpecialItem은 성패 판정 대상이 아님 |
| R6 | 실패 시 빔 부호 규칙이 데이터와 충돌 | 단일 규칙으로 통일 — 데이터의 **크기**만 쓰고 **부호**는 포지션+성패에서 유도 |
| R7 | `ThemeTag`가 태그가 아니라 상황 서술 | `GetThemeDescription()`으로 `[ID]` 접두사를 제거한 서술만 프롬프트에 주입 |
| R8 | LLM 부재 시 환경마다 동작이 갈림 | 부재를 정상 경로로 취급. 사전 작성 텍스트로 동일하게 표시되므로 에디터/빌드 동작 일치 |
| R9 | 요미 대사 DB가 이벤트 팝업에서 미사용 | `YomiDialogueMatcher.GetEventDialogue("ChoiceEvent_{카테고리}_{흐름}")` 우선 조회 |
| M1 | `GenerateDailySettlementAsync` 죽은 기능 | 삭제 (호출자 0) |
| M2 | `TriggerPrefetchedEvent` 죽은 API | `ForceTriggerTemplateEvent`로 대체하여 디버그 메뉴에서 실사용 |
| M3 | 튜토리얼 주석이 사실과 다름 | 정정 |
| M4 | 표시 실패에도 `eventsTriggeredToday++` | 성공 시에만 증가, 실패 시 10분 뒤 재시도 예약 |
| M6 | `LowMental` 트리거에 `Any` 이벤트 혼입 | 조건 일치 이벤트만 후보. 전용 이벤트가 없을 때만 `Any`로 확장 |
| M7 | UI에 길이 상한/이상 패턴 필터 없음 | 표시 직전 절단 + 프롬프트 잔재 감지 시 에러 로그 |

#### 4.4 모델 파라미터 (코드에서 강제)

`TitleScene`의 `LLMAgent` 직렬화 값은 LLMUnity 기본값 그대로였습니다(영어 시스템 프롬프트 / `numPredict: -1` / `temperature: 0.2` / grammar 없음).
`LLMSafeGenerator.Awake()`에서 코드로 덮어씁니다.

| 설정 | 변경 |
| --- | --- |
| `systemPrompt` | 한국어 JSON 생성기 역할 부여 |
| `numPredict` | `-1` → `320` (무제한 복창 차단) |
| `temperature` | `0.2` → `0.65` (낮은 온도가 예시 복사를 조장) |
| `grammar` | 3필드 JSON GBNF 강제. **호출 직전에 걸고 직후 해제** — grammar는 에이전트 전역이라 다른 용도까지 JSON에 묶이기 때문 |

#### 4.5 잔여 과제

* **Phase 0 미완**: 실제 모델 출력 로그로 3.1 인과 사슬 확정 (플레이모드 필요)
* 242개 템플릿 사전 텍스트는 **자동 생성 초안** 상태 — 작가 검수 필요
* `YomiDialogueDatabase`에 `ChoiceEvent_*` 카테고리 신규 집필 필요 (현재는 조회 실패 시 다음 순위로 폴백)
* 이벤트 쉴드 150초 vs 차트 드리프트 30인게임분(≈실시간 30초)의 불일치 — 기획 판단 대기

---

## 5. 자유 채팅 경제 개편 (2026-08-14)

상세는 [YomiRoom_ChoiceTalk_System_Plan.md](YomiRoom_ChoiceTalk_System_Plan.md) 14장.

| 항목 | 변경 |
| --- | --- |
| 소모 자원 | 시간 슬롯 1칸 → **미연시 체력 10** (`talkStaminaCost`) |
| 횟수 | **하루 1회.** 카운트는 대화 종료 시점(완주·중도 종료 모두)에 소모 — `SaveData.TalkLastSessionEndDay` |
| 호감도 | 토픽 총획득 **정확히 +3** (직면 +2 · 착지 +1), 총감소 **정확히 -2** (-1 선택지 12개 신설) |
| 힌트 임계 | 4/7 → **2/3** (총획득 상한과 정합) |
| 버그 수정 | **일일 리셋 미구현** — `TalkTopicsUsedToday`/`TalkAffectionGainToday`가 영영 안 비워져 토픽 영구 소진 + 힌트 임계 무력화. `YomiRoomManager.EnsureDailyTalkState()`가 일차 변화를 보고 자가 리셋 (`SaveData.TalkDailyStateDay`) |
| API | `YomiRoomManager.OnActionFailed`가 `Action<string>`으로 사유 문구 전달. 현행 대화 패널이 지문 채널로 표시 |

### 5.1 세이브 코어 결함 수정 (2026-08-14) ✅

조사 전문과 나머지 7건은 [YomiRoom_ChoiceTalk_System_Plan.md](YomiRoom_ChoiceTalk_System_Plan.md) 15장. **세이브 코어에 영향이 간 2건만 여기 남긴다.** 둘 다 자유 채팅 개편과 무관한 기존 결함이었다.

| ID | 결함 | 수정 |
| --- | --- | --- |
| **S-1** | `PrepareNewGame()`이 `CurrentData = null`로 두는 바람에, 새 게임의 첫 저장에서 `SaveGame()`의 `CurrentData ?? ReadSaveFile(slot)` 이 **이전 판의 슬롯 파일을 베이스로 삼았다.** 씬에 있는 매니저만 자기 필드를 덮어쓰므로 주인 없는 필드(`Talk*` 전체)가 통째로 상속됐다 — 새 게임 1일차에 3단계 토픽이 열려 있었다 | `CurrentData = new SaveData()`. **슬롯 파일은 지우지 않는다** — 새 게임을 시작만 하고 그만둘 수 있으므로, 첫 저장 때 정상적으로 덮어쓴다 |
| **S-2** | `DatingTimeManager`가 `DontDestroyOnLoad`인데 `PrepareNewGame()`의 리셋 목록에 없어, 같은 세션에서 새 게임 시 이전 판의 호감도·집착도·체력·일차가 첫 저장에 기록됐다 | 위에서 만든 기본값 `SaveData`를 그대로 `LoadFromSaveData()`에 먹인다 (한 줄) |

**안전망을 먼저 세운 뒤 착수했다.** `SaveRoundTripTester.RunFieldCoverageAudit()`의 소스 목록에 `YomiRoomManager.cs`(`Talk*` 15필드)와 `DailyMarketOutlook.cs`(`Outlook*` 2필드)가 **빠져 있었고**, orphan 판정이 경고로만 흘러 6건이 방치돼 있었다. 두 파일을 넣고 **orphan을 실패로 승격**했다. 현재 `SaveData` 97필드 중 orphan 0건.

> **새 저장 경로를 만들면 반드시 그 소스를 `RunFieldCoverageAudit`의 목록에 추가할 것.** 빠뜨리면 그 파일이 소유한 필드가 전부 데드로 오판된다.

---

## 6. 하루의 시작 지점 이전 + 세이브 씬 위치 보존 (2026-08-14)

상세는 [Scene_Position_Persistence_Plan.md](Scene_Position_Persistence_Plan.md).

### 6.1 시작 지점 이전

스토리 모드 진입 순서를 `타이틀 → 로딩 → GameScene`에서 **`타이틀 → 로딩 → YomiRoomScene`**으로 바꿨다. 튜토리얼 종료(`TutorialManager.EndTutorial`) 목적지도 방으로 옮겼다. 이로써 새 게임 1일차와 매일 아침(`GameManager.ReturnToYomiRoomForNewMorning`)이 모두 방에서 시작한다.

### 6.2 이 변경이 드러낸 구조 문제 — 새 게임 초기화가 `GameScene`에 갇혀 있었다

**`GameManager`는 `GameScene`에만 있다** (씬 GUID 대조로 확인. 방·월드맵엔 없다). 그런데 난이도별 초기 자금과 시작 아이템 지급이 전부 `GameManager.StartNewGame()` 안에 있었다. 시작 지점을 방으로 옮기자 다음이 깨졌다:

방은 거래 개시 전에 `저장 → PrepareLoadGame` 순서를 밟는다(SV-B10). 그러면 `IsPendingLoad`가 서고, `GameScene`에 도착한 `GameManager`가 이를 **불러오기로 오인해 `StartNewGame()`을 영영 실행하지 않는다.** 결과는 잔고 기본값 1000달러 고정 + 인벤토리 0개.

**해법은 초기화를 데이터 레벨로 끌어내는 것이다.** `SaveLoadManager.PrepareNewGame()`이 난이도별 초기 자금을 `CurrentData`에 직접 심는다(`StoryDifficultyTables`는 순수 static이라 씬 의존이 없다). 날짜·레벨·기억·코스튬은 `SaveData` 기본값이 곧 초기 상태라 심을 것이 없다.

**시작 아이템만 예외다.** `Inventory.ResetForNewGame()`이 씬에 배치된 `ItemData` 에셋의 이름을 부분 문자열로 매칭해 수량을 정하므로 데이터로 재현할 수 없다. 아이템 ID를 복제하는 대신 `SaveData.NeedsStartingItems` 플래그를 두고, `ApplyLoadedDataToGame()`이 이 플래그를 보면 저장된 목록 대신 `ResetForNewGame()`을 호출하게 했다.

이로써 스토리 모드에서 `StartNewGame()`은 실행되지 않는다. 세이브를 쓰지 않는 **무한·챌린지 모드 전용 경로**로 남는다.

### 6.3 씬 위치 보존

| 항목 | 내용 |
| --- | --- |
| 필드 | `SaveData.LastSceneName` (기본 `""` = `GameScene`. 기본값이 곧 구버전 호환이라 마이그레이터 불필요) |
| 허용 목록 | `SaveLoadManager.ResumableScenes` — `GameScene`/`YomiRoomScene`/`WorldMapScene`. 저장하는 쪽과 불러오는 쪽이 같은 판정을 공유해야 해서 한 곳에 모았다. 씬이 늘면 이 배열에만 추가 |
| 수집 | `SaveGame()`이 현재 씬을 찍는다. **목록에 없는 씬에서는 갱신하지 않고 이전 값을 유지** — 그러지 않으면 튜토리얼 중 저장한 세이브가 재접속 때 튜토리얼을 다시 재생한다 |
| 복귀 | `MainMenuController` 이어하기가 `LastSceneName`으로 전환. 빈 값·빌드 누락·목록 제거는 `LoadGameFlow` 가드가 `GameScene`으로 떨군다 |

**신규 복원 로직은 만들지 않았다.** 방·월드맵은 이미 `GameManager` 없는 환경을 전제로 작성되어 있다 — `YomiRoomManager.ProgressData`는 `CurrentData`를 매번 직접 읽고, `WorldMapManager`/`WorldMapUIController`는 `GameManager`가 null이면 `CurrentData.Balance`로 폴백한다. 트레이딩 상태는 `IsPendingLoad`가 살아 있어 플레이어가 거래를 개시할 때 정상 복원된다(늦게 복원될 뿐 유실되지 않는다).

### 6.4 씬 도착을 스스로 기록하게 했다 (F-11)

`LastSceneName`은 "저장이 일어난 씬"인데 **씬 도착만으로는 저장이 일어나지 않는다.** 방의 저장은 전부 행동에 붙어 있다. 그래서 아침에 방으로 나오자마자 종료하면 `LastSceneName`이 여전히 `GameScene`이었다 — **매일 아침이 이 경우다.**

`YomiRoomManager.Start()`와 `WorldMapManager.Start()`에서 도착 즉시 한 번 저장한다. `SaveGame()`이 현재 씬을 찍으므로 이 저장 자체가 복귀 지점 갱신이고, 별도 스탬프 코드가 필요 없다. **각 씬이 자기 도착을 스스로 기록**하므로 방으로 오는 경로가 몇 개든(새 게임·튜토리얼 종료·아침 복귀·월드맵 귀환) 전부 덮인다.

부수 효과로 스토리 새 게임의 슬롯 파일이 방 도착 시점에 생성된다. 종전엔 `StartNewGame()`이 `GameScene`에서 만들었으므로 그 역할을 대신한다.

### 6.5 함께 잡은 기존 버그 3건

| ID | 버그 | 수정 |
| --- | --- | --- |
| **F-12** | **1일차 알바 수익 증발.** 새 게임 → 월드맵 알바(`GameManager`가 null이라 `CurrentData.Balance`에 직접 합산·저장) → 거래 개시 → `StartNewGame()`이 `currentBalance = startingBalance`로 되돌리고 곧바로 `SaveCurrentGame()`이 확정 | 6.2의 데이터 시딩으로 스토리 모드에서 `StartNewGame()`이 실행되지 않아 소멸 |
| **F-13** | **이어하기 후 누적 수익률 오표시.** `TopStatusBarUIController.UpdatePnLUI()`가 `gameManager.StartingBalance`를 분모로 쓰는데 `ApplyLoadedDataToGame()`이 이 필드를 복원하지 않아, 인스펙터 기본값 7000이 분모가 됐다. 초기 자금이 7000이 아닌 난이도는 수익률이 전부 틀렸다 | 복원 리플렉션 블록에 `startingBalance` 한 줄 추가 |
| **F-14** | **한 세션에서 새 게임 시 튜토리얼 스킵.** `SaveLoadManager.IsTutorialCompleted`가 `DontDestroyOnLoad` 프로퍼티인데 `PrepareNewGame()`이 내리지 않아 이전 판의 값이 남았다 | `PrepareNewGame()`에 `IsTutorialCompleted = false` 한 줄 |

추가로 이 프로퍼티는 `ApplyLoadedDataToGame()`에서만 복원됐는데, 방으로 바로 복귀하면 그게 돌지 않는다. 6.4의 도착 저장이 `data.IsTutorialCompleted = this.IsTutorialCompleted`를 쓰므로 **완료된 튜토리얼이 false로 덮인다.** 복원을 `PrepareLoadGame()`으로 옮겼다(중복이 아니라 이동 — `ApplyLoadedDataToGame()`은 항상 그 뒤에만 실행된다).

> **`GameManager` 없는 씬에 새 로직을 넣을 때는 `SaveLoadManager.CurrentData`를 진실의 원천으로 삼을 것.** 방·월드맵의 기존 코드가 전부 그 규약을 따르고 있고, 씬 위치 보존이 성립한 이유도 그것이다.

---

## 7. 멘탈 감소 시스템 리밸런싱 (2026-08-14)

전체 계획과 수치 근거는 [Mental_Drain_Rebalance_Plan.md](Mental_Drain_Rebalance_Plan.md). 여기엔 구조가 바뀐 부분만 남긴다.

### 7.1 `ChangeMental` 시그니처가 좁아졌다

`ChangeMental(float, bool ignoreRegenBlock, string reason)` → **`ChangeMental(float, string reason = "")`**.

`canRegenMental`을 `false`로 세팅하는 코드가 프로젝트에 하나도 없어 회복 차단 분기는 실행된 적이 없었고, 그걸 우회하려던 `ignoreRegenBlock`도 따라서 무의미했다. 필드·프로퍼티·`SaveData.CanRegenMental`까지 함께 걷어냈다. **구버전 세이브의 잉여 키는 `JsonUtility`가 무시하므로 마이그레이터는 필요 없다.**

### 7.2 트라우마 천장(`maxMentalLimit`)이 사라졌다

천장을 낮추는 호출자가 없는 채로 배관만 남아 있었다. 필드·`MaxMentalLimit`·`EffectiveMaxMental`·`SetMaxMentalCeiling()`·`SaveData.MaxMentalLimit`을 제거하고 클램프를 `MaxMental` 단독으로 되돌렸다.

`EffectiveMaxMental`을 읽던 UI 3곳(`GameOverUIController`, `DailySettlementUIController`)은 `MaxMental`로 바꿨다. **`IncreaseMaxMental()`은 존치** — 최대 멘탈 영구 증가 아이템이 실사용 중이며 트라우마와 무관하다.

곁가지로 `AchievementManager.RecordTraumaCured()`가 호출된 적이 없어 `trauma_cured` 업적이 획득 불가였다. 업적 정의·`Pref_TraumaCured`·판정 분기·`AchievementType.Custom`(유일 사용처였다)을 함께 제거했다.

### 7.3 자연 감소의 이중 계상을 없앴다 (안 A)

`DecreaseStatusOverTime()`이 `ChangeHealth(-체력감소량)`을 부르고 **별도로** `mentalDecreasePerSecond`를 누적기에 더했는데, 그 `ChangeHealth`가 내부에서 같은 누적기에 또 더하고 있었다. 이름이 다른 두 규칙이 "시간 경과에 따른 체력 감소"라는 한 사건을 두 번 세고 있었다.

- 체력 1~50% 구간의 멘탈 감소는 **`ChangeHealth`의 체력 연동이 전담**한다.
- 체력 0에서는 `ChangeHealth`가 아무 변화도 만들지 못하므로(클램프) `DecreaseStatusOverTime()`이 직접 연동 대비 2배로 누적한다.
- `mentalDecreasePerSecond` 필드는 제거했다.

**`ChangeHealth`의 연동 기준을 `amount`(요청량) → `appliedHealthDelta`(클램프 후 실제 변화량)로 바꿨다.** 종전엔 체력이 0으로 클램프된 뒤에도 요청량이 그대로 청구돼 "체력이 계속 떨어지는 것처럼" 멘탈이 깎였다. 같은 지점에 `MentalDrainReduction`도 적용했다 — 종전엔 경감이 안 걸리는 쪽이 걸리는 쪽보다 2~5.6배 커서 아이템의 "멘탈 보호 -80%"가 전체의 15~30%에만 먹혔다.

### 7.4 청산 멘탈 변화가 절대 금액에서 자본 대비 비율로 바뀌었다

`pnl * 0.05f`는 절대 금액이라 후반부엔 잔고의 0.1%도 안 되는 손실이 멘탈 바 전체를 날렸다. 제곱근 곡선으로 교체했다(`TradingController`의 4개 상수).

```
손실: -min(25, 40·√(|pnl| / 진입시점총자본))
익절: +min(15, 25·√( pnl  / 진입시점총자본)) × winMultiplier
```

> **분모는 `gameManager.ChangeBalance(totalReturn)` 호출 _전에_ 캡처해야 한다.** 멘탈 계산 시점의 `CurrentBalance`에는 이미 회수금이 반영돼 있어, 순서를 바꾸면 분모에 회수금이 섞여 비율이 왜곡된다.

감소만 비율화하면 후반에 익절 한 번으로 멘탈이 만땅이 되므로 **익절도 같은 곡선으로 묶었다.**

### 7.5 이벤트 페널티는 코드에도 상한을 걸었다

`ChoiceEventController.ApplyVitals()`가 데이터와 무관하게 단발 -35를 강제한다. 생성기만 고치면 재베이크를 잊은 에셋이나 손으로 쓴 SO가 다시 -120을 들고 올 수 있다.

`MigrateEventTemplates`에 압축 단계(C5)를 추가했다 — 이관한 페널티를 -30~-5로 좁히고, 성공 보상은 **이관 전** 위험도 기준으로 뽑아 위험/보상이 1:1에 수렴한다. 이미 이관된 자산도 범위로 끌어오므로 재실행에 안전하다.

> ⚠️ **에셋 마이그레이션은 아직 실행되지 않았다.** `Assets/Resources/Events/Templates`의 726개 선택지 중 244개가 여전히 음수 `MentalChangeAmount`(최소 -120)를 들고 있고 `MentalPenaltyOnFail`은 전부 0이다. 즉 **베팅 성공 시 멘탈이 폭락하고 실패 시 무손실**인 상태다. 에디터에서 `Tools/FX OVERDOSE/Migrate Event Templates (C3+C4)` 1회 실행이 남았다.

### 7.6 지뢰계 배율은 의도와 정반대로 동작하고 있었다

`reason != "TimeDrain"`으로 자연 감소를 증폭에서 빼려 했으나 **`"TimeDrain"`으로 `ChangeMental`을 부르는 코드가 없었다.** 자연 감소는 `"체력 저하"`로 들어오므로 예외가 한 번도 성립하지 않았다. `TraderStatus.NaturalDrainReason` 상수를 도입해 호출부와 비교부가 같은 값을 쓰게 했다.

### 7.7 `AITradingBrain`은 LLM 잔재가 아니다

이름 때문에 폐기된 LLM 자동매매의 잔재로 오인되기 쉬우나, **파일 전체에 `LLM` 문자열이 0건인 규칙 기반 판정 엔진이며 현재 자동 매매의 실행 주체다.** 삭제하면 자동 매매·FOMO 후회 기믹(`OnSignalEvaluationCompleted` 구독)·고배율 중독 폭주·차트 힌트가 함께 죽는다. 클래스 요약 주석에 이 경고를 박아 뒀다.

실제로 사장돼 있던 건 `TriggerDialogue`(본문 없음)와 `TriggerDialogueWithCategory`(호출자 0건) 두 메서드이며, 후자가 이 파일에서 멘탈을 건드리는 유일한 코드였다. 둘 다 제거했다.

그에 딸린 `OnAIDecisionMade` 이벤트와 구독자 `AIVisualController.HandleAIDecisionMade`도 함께 걷어냈다. 처음엔 "의도됐으나 배선이 끊긴 기능"으로 보였으나, 점검 결과 **재배선할 대상이 아니었다.**

`HandleAIDecisionMade`는 자기 파라미터(`dialogue`, `emotionDelta`)를 쓰지 않고 게임 상태를 다시 읽어 `YomiDialogueMatcher.GetDialogue()`를 호출하는데, 뒤쪽 7개 인자(`owner`/`actualChartTrend`/`absolutePnL`/`duration`/`currentHealth`/`costumeId`/`currentAction`)를 전부 생략했다. 특히 `currentAction`이 기본값 `""`인 것이 치명적이다 — `CalculateScore`의 첫 게이트가

```csharp
if (!string.IsNullOrEmpty(entry.eventCategory)) {
    if (entry.eventCategory == currentAction) score += 500;
    else return -9999;   // 원천 차단
}
```

인데 `YomiDialogueDatabase.asset`의 **810개 엔트리 전부가 비어 있지 않은 `eventCategory`를 가진다**(빈 값 0개). 따라서 후보 전량이 `-9999`로 걸러져 `GetDialogue`는 항상 `null`을 반환했다. **발행자가 살아 있던 시절에도 이 핸들러는 말풍선을 한 번도 띄운 적이 없다.**

의도했던 기능은 [`TradingController.OutputYomiDialogue`](../../Assets/Scripts/Trading/TradingController.cs#L1712)가 `currentAction`에 `cat.ToString()`을 넘겨 +500 정확 매칭 경로로 이미 온전히 수행하고 있다(호출처 10곳). `HandleAIDecisionMade`는 그 설계로 가는 도중에 남은 축소 중복이었다.

> **LLM과 무관하다.** `YomiDialogueMatcher`는 `YomiDialogueDatabase` ScriptableObject를 점수화해 고르는 순수 룰 기반 조회기다. `Assets/Scripts/AI/`에서 LLM을 쓰는 파일은 `LLM/LLMSafeGenerator.cs`, `LLM/LLMOutputSanitizer.cs` 둘뿐이며 트레이딩 파트의 선택 이벤트/일기 전용이다.

곁가지로 `AITradingSystemTestRunner`의 Test 1이 `if (decisionFired && ...)`로 게이트돼 있었다. `decisionFired`가 항상 false라 **테스트 본문 전체가 한 번도 실행된 적이 없었다.** 죽은 게이트를 걷어내 `IsActive` 검증만 남겼고, 그 결과 Test 1이 실제로 돌게 됐다.

> 남은 잔재: `AITradingBrain.lastDecisionLog` / `LastDecisionLog`도 유일한 기입자가 `TriggerDialogueWithCategory`였다. 지금은 항상 빈 문자열이며 테스트 러너 로그 5곳이 이를 `최종 AI 독백: ""`로 출력한다. 제거하려면 그 로그 문구까지 손봐야 해 이번 범위에서 제외했다.

### 7.8 P2P는 손대지 않았다

`P2PPlayerRuntimeState`/`P2PLocalMatch`/`NetworkCompetitionAuthority`가 이번에 바뀐 싱글 규칙(진입 -10, 연속 손절 5/12/25, `pnl × 0.05`)을 **이식해 복제**하고 있다. 계획 범위가 트레이딩 파트였으므로 그대로 뒀고, 따라서 **두 파트의 멘탈 밸런스는 현재 갈라져 있다.** P2P를 맞출지는 별도 판단이 필요하다.

### 7.9 검증

`verify_mental_balance.py`(리포지토리 루트)가 청산 곡선과 합산 예산 제약을 검사한다. 핵심은 **스케일 불변성**(자본 7천~200만에서 동일 손익률 → 동일 멘탈 변화)과 **4연속 손절 뇌동매매 기믹의 발동 가능성**이다. 후자는 종전 수치로는 2회차에 오버도즈가 확정돼 기믹이 사장돼 있었고, 지금은 수동 3연속 손절 후 멘탈 14.7이 남는다. 수치를 바꾸면 이 스크립트의 상수도 함께 고칠 것.

---

## 8. 일차 누적 → 달력 날짜 전환 (2026-08-15)

계획서: [Calendar_DateTime_System_Plan.md](Calendar_DateTime_System_Plan.md)

`~일차` 누적 표기를 버리고 년/월/일/시/분 달력으로 전환했다. 에폭은 **2026년 6월 26일(금)**, 20일차 = 7월 15일.

### 8.1 달력이 권위값, 일차는 파생값

`GameManager.currentDay` 필드를 없애고 `currentDate`(DateTime)를 권위값으로 뒀다. `CurrentDay`는 `(currentDate - startDate).Days + 1`을 돌려주는 **프로퍼티**로 남았다.

시그니처가 그대로라 **일차 서수를 키로 쓰는 15개 시스템 30여 개 호출 지점이 한 줄도 바뀌지 않았다** — 보스 일정, 스토리 `triggerDay`, 시장 난이도 구간, 스테이크 7일 쿨다운, 요미 대화 하루 게이트, AI 기억 pruning. 이들이 원하는 건 달력이 아니라 "며칠째"이며 그 의미는 변하지 않았기 때문이다.

> **시/분은 날짜와 합치지 않았다.** 하루는 24:00에 끝나는데 그 시점의 일차는 아직 '어제'여야 정산과 보스 승패 판정이 맞는다. 하나의 `DateTime`으로 합치면 24:00이 익일 00:00이 되어 하루가 어긋난다.

### 8.2 저장 테이블

실질 추가는 문자열 2개(`CurrentDate`, `StartDate`)뿐이다. 나머지 일차 서수 필드 12개(`OutlookDay`·`LastSteakPurchaseDay`·`DatingDay`·`Talk*Day` 등)는 의미가 그대로라 마이그레이션 대상이 아니다. `CurrentDay`도 파생값이지만 계속 기록한다(구버전 호환·역산 근거·`DatingDay` 계약 검증).

**`SaveData`의 날짜 초기화자는 빈 문자열이어야 한다.** 이 값이 "달력 도입 이전 세이브"를 가려내는 판정 기준이다. 기본 날짜를 박으면 구버전 JSON에 키가 없어 초기화자 값이 남고 20일차 세이브가 1일차 날짜로 로드된다. 새 게임의 초기값은 `SaveLoadManager.PrepareNewGame()`이 명시적으로 심는다 — 스토리 모드는 `GameManager`가 없는 요미의 방에서 첫 저장이 일어나 수집 경로(`if (gm != null)`)가 채워주지 못하고, 버전 태그는 이미 최신이라 마이그레이터도 돌지 않는다.

그래서 `GameManager.RestoreClock()`이 **버전과 무관하게** 폴백한다: 날짜가 비었거나 손상되면 `StartDate + (CurrentDay - 1)`로 역산한다.

### 8.3 리플렉션 제거

`SaveLoadManager`가 `currentDay`/`currentHour`/`currentMinute`를 리플렉션으로 주입하던 3줄을 `GameManager.RestoreClock()` 호출로 교체했다. 리플렉션은 필드명이 바뀌어도 컴파일 에러 없이 조용히 실패하는데, 시간축이 통째로 초기값이 되는 사고는 눈에 잘 띄지 않는다. (`currentBalance` 주입은 그대로 남아 있다.)

### 8.4 직렬화는 반드시 InvariantCulture

`FXOverdose.Core.GameCalendar`가 포맷·파싱·표기를 전담한다. 태국(불기)·일본(연호) 로케일에서 `ToString("yyyy-MM-dd")`가 연도를 2569로 쓰는 것을 막기 위해서다. 파싱은 `TryParseExact` — 손상된 날짜 한 줄 때문에 세이브 전체가 예외로 죽으면 안 된다.

### 8.5 날짜 점프의 함정

`AdvanceDate(days)`를 넣었으나 **날짜만 옮긴다** — 체력/멘탈 회복, 시간 슬롯 리필, 차트 리셋은 하지 않는다. 건너뛴 날의 정기 지출 정책도 미정이라 실제로 점프를 쓰는 스토리 작업에서 정해야 한다.

예약 일차(보스·스토리 이벤트·`StoryLastDay`)는 넘지 못하고 그 날에서 멈추며 경고를 남긴다. 특히 **최종일 판정이 등호 비교(`CurrentDay == StoryLastDay`)라 뛰어넘으면 엔딩이 영영 발생하지 않는다.**

### 8.6 화면

`DAY NN` 표기를 8곳에서 제거하고 `6월 28일`(엔딩은 `2026년 6월 28일`)로 바꿨다. 요일은 표기하지 않는다. 보스 배지만은 **예정 일차**를 표시하므로 `GameManager.DateForDay(ordinal)`로 환산한다 — 현재 날짜를 쓰면 일차 복구보다 보스 스폰이 먼저 오는 순서 문제에 걸린다. 정산 원장 번호 `#003`은 DAY 라벨이 아니라 문서 일련번호이므로 서수를 유지했다.

### 8.7 게임 길이

20일 유지. 다만 하드코딩돼 있던 `20`을 `GameManager.StoryLastDay` 상수로 모아 스토리 개편이 한 줄로 길이를 바꿀 수 있게 했다. 실제 최종일의 주인은 여전히 `BossData.IsFinalBoss`이고, 이 상수는 보스 스폰 실패 시의 백업 판정에만 쓰인다.

### 8.8 검증

에디터 메뉴 `FXOverdose/Debug/Calendar System Test`가 순수 로직 17건을 검사한다(에폭·요일, 일차 환산, 월말/연말 경계, 불기 로케일 왕복, 마이그레이션 역산과 sentinel 보존). 씬이 필요한 항목 — 24:00 정산의 날짜, 새 게임 첫 저장 왕복, 날짜 점프 클램프 — 은 계획서 7절의 수기 체크리스트로 남겼다.

> ⚠️ **폰트 프리베이크 필요.** `년`/`월`/`일`이 새 UI 문자열로 들어갔다. `Tools/Prebake All Scripts Text into Font`를 돌리지 않으면 □로 렌더된다.

---

## 9. 시간 슬롯 ↔ 시계 연동 · 방 내 정산 · 시스템 임시 비활성화 (2026-08-15)

계획서 3종: [TimeSlot_Clock_Integration_Plan.md](TimeSlot_Clock_Integration_Plan.md) · [Settlement_In_YomiRoom_Plan.md](Settlement_In_YomiRoom_Plan.md) · [Boss_Penalty_Temporary_Disable.md](Boss_Penalty_Temporary_Disable.md)

### 9.1 슬롯이 시계를 민다

시간 슬롯 1개 = **게임 내 3시간**. 5슬롯 × 3시간 = 09:00~24:00과 정확히 일치한다. 슬롯을 깎는 유일한 통로인 `DatingTimeManager.TryConsumeTimeSlot`이 시계도 함께 밀므로, 거래 개시 시각은 **기존 세이브 복원 경로를 타고 저절로** 맞는다(2슬롯 소모 → 15:00 시작). 저장 테이블은 변경 없음 — 시계와 슬롯이 이미 둘 다 저장되고 있었고, 관계만 부여했다.

> ⚠️ 시계 전진에 `AdvanceGameMinutes`를 쓰면 안 된다. 그쪽은 `Playing`이 아니면 멈춰 분을 쌓아두므로 거래 개시 때 한꺼번에 터진다. 방·월드맵은 `Paused`이므로 **`AdvanceClockWithoutSimulation`** 을 쓴다.

돌발 이벤트는 두 곳을 고쳤다. **시계 점프 재스케줄** — 슬롯 전진은 `OnGameMinuteAdvanced`를 발행하지 않아 컨트롤러가 점프를 목격하지 못하고 "예정 시각이 지났다"만 보게 되어 개시 첫 분에 팝업이 터졌다. **잔여 시간 기반 한도** — 개시 시각(남은 슬롯에서 역산)으로 재서 6시간 미만 1회, 3시간 미만 0회. 현재 시각으로 재면 평소 09:00 시작에서도 저녁에 한도가 줄어 동작이 바뀐다.

### 9.2 취침 시 방에서 정산 (씬 로드 2회 → 0회)

**계획서 5절의 씬 수술을 하지 않았다.** GameScene의 GameManager가 `Awake`에서 스스로 `DontDestroyOnLoad`로 올라가고, 재진입 시 딸려온 사본이 자폭한다. 상주본이 최초 GameScene의 그 오브젝트이므로 **`storyEvents`·엔딩 만화가 그대로 살아 `StoryDatabase` 추출이 필요 없어졌다.**

딸린 조치 셋:
- `Start` 본문을 `InitializeForTradingScene()`으로 분리 — 상주 오브젝트는 재진입해도 `Start`가 안 돌아서, 그대로 뒀다면 **세이브 복원이 통째로 누락**된다
- 거래 씬 밖에서는 `Paused` — 시계·드레인·이벤트는 멎고 저장·잔고 변동은 허용된다. `Playing`으로 두면 방에서 시간이 흐르고, 다른 상태면 방에서 저장이 조용히 실패한다
- `FindAnyObjectByType<GameManager>` **61곳(34파일)을 `GameManager.Instance`로 치환** — `Destroy`는 프레임 끝에 반영되므로 `Find`는 자폭 예약된 사본을 돌려줄 수 있다

방의 UI는 `RoomSettlementUIBootstrap`이 씬 로드 시 루트 Canvas에 정산·게임오버 컨트롤러를 붙인다(`BossBattleUIBootstrap`과 같은 패턴, 씬 편집 없음).

**차트 리셋 누락 방어**: 일차 전환은 `MarketSimulationEngine`을 리셋하는데 방에는 엔진이 없어 조용히 건너뛰어진다. 직후 자동 저장이 돌아 **전날 캔들이 디스크에 남으므로**, 엔진이 없을 때는 세이브의 차트 상태(`ChartHistories`·`MarketLastUpdatedDay`·`MarketTotalMinutes`)를 비워 다음 진입에서 프리웜되게 한다.

**한계**: 상주본은 최초 GameScene 진입에서 생긴다. 그 전(새 게임 첫날 방)에는 GameManager가 없어 `TrySleep`이 종전 경로로 떨어진다. `GameManager.RoomSettlementEnabled = false`로 전체를 되돌릴 수 있다.

### 9.3 임시 비활성화 스위치 3종

`BossManager.BossesEnabled` / `GameManager.StoryPenaltiesEnabled` / `GameManager.StoryDayLimitEnabled` — 전부 `false`. 스토리 개편 기간 한정이며 되살리기는 세 줄이다.

> **이 상태에서는 성공 엔딩에 도달할 수 없다.** 성공 판정이 「최종 보스 격파」와 「20일차 백업」 둘뿐인데 양쪽이 닫혔다. 파산·오버도즈만 남고 게임은 무한히 이어진다.

주석 처리 대신 **관문 스위치**를 쓴 이유는 소비 지점이 보스 11곳·위약금 5곳으로 흩어져 있어서다. `const`가 아니라 `static readonly`인 것도 의도적이다 — `const`면 하위 코드가 도달 불가로 판정돼 경고가 쏟아진다.

**기능 실행과 일정 보호는 다른 질문이다.** `HasBossToday`(스위치 영향 받음, 조우·스폰·판정용)와 `IsBossScheduledDay`(영향 안 받음, 날짜 점프 클램프용)를 나눴다. 보스가 꺼졌다고 그 날을 건너뛰면, 되살려도 이미 지나친 세이브가 남는다.

### 9.4 검증

에디터 메뉴 `FXOverdose/Debug/Calendar System Test`가 순수 로직 28건을 검사한다(달력 환산·경계·로케일 왕복·마이그레이션 1.7.0/1.8.0·슬롯 시각 환산·스위치 상태). 씬이 필요한 항목은 각 계획서의 검증 체크리스트로 남겼다.

---

## 10. 이벤트 진행 시스템 (2026-08-15)

계획: [EventScene_System_Plan.md](EventScene_System_Plan.md) v2. 데이트·메인 스토리·프롤로그가 공용으로 쓸 이벤트 재생 시스템. `Assets/Scripts/Events/Story/`.

### 10.1 코어 1개, 호스트 2개

`EventRunner`(상태기계·분기·지연 커밋, MonoBehaviour 아님) + `EventView`(위젯 트리 **한 벌**) + 호스트 2종. 두 표현이 `EventRunner` 하나만 돌리므로 진행 결과가 같다는 것이 구조로 보장된다.

| | `EventOverlayHost` | `EventSceneHost` |
| --- | --- | --- |
| 캔버스 | 현재 씬에 런타임 생성 (`sortingOrder 1300`) | `EventScene` |
| 종료 | 완료 콜백 | `ReturnScene` 복귀 |
| 복귀 계약 | **없음** — 씬을 떠나지 않는다 | `LastSceneName` 기입 + 복귀지 제한 |

**종료 계약이 비대칭인 것은 의도다.** 전용 씬은 씬이 파괴돼 콜백이 붙잡은 오브젝트가 먼저 죽는다. static으로 살리는 편법을 쓰지 않는다.

**화면은 양쪽 다 런타임 생성**이다. 씬에 프리팹을 배치하면 오버레이가 만드는 화면과 조용히 어긋난다.

### 10.2 2단계 커밋 — 저장은 조용히 실패한다

`SaveGame()`이 `false`만 돌려주는 실패 경로가 셋이다 — 스토리 모드 아님 / `GameManager` 상태가 `Playing`·`Paused` 아님(**`Settlement` 포함**) / 오버도즈 중. **기존 스토리 이벤트가 바로 그 `Settlement`에서 발화한다**(`ProcessDailySettlementWithStory` → 컷씬). 그대로 붙이면 이벤트 결과가 통째로 증발한다.

그래서 커밋을 나눴다. 1단계는 `CurrentData`에 반영(메모리, 항상 성공), 2단계는 디스크 1회 시도. **실패해도 1단계가 남아 부분 저장 베이스(SV-A6)를 타고 다음 저장에 실려 나간다.** 새로 만든 메커니즘이 아니라 기존 것을 얻어 쓴 것이다.

`PauseGame()`은 `Settlement`에서 아무 일도 하지 않으므로 오버레이도 이 경로를 우회하지 못한다 — 2단계 커밋이 유일한 해법이다.

### 10.3 전용 씬의 복귀 계약

- **복귀지에서 GameScene을 뺐다** (`EventSceneHost.AllowedReturnScenes`). 거래 씬 복귀는 `SaveCurrentGame()` → `PrepareLoadGame()` 순서(SV-B10)를 지켜야 새 게임이 시작되지 않는데, 그 프로토콜을 한 벌 더 구현하느니 거래 중 이벤트는 오버레이로 보내는 편이 낫다. 오버레이는 씬을 아예 떠나지 않는다.
- **`LastSceneName`을 커밋에서 직접 적는다.** `EventScene`은 `ResumableScenes`에 없고(의도적 — 이벤트 씬으로 복귀하면 안 된다), 새 게임 프롤로그는 `LastSceneName`이 `""`인 채로 오므로, 그대로 두면 프롤로그 직후 종료한 플레이어가 **요미 방이 아니라 GameScene**으로 떨어진다.
- **정산 충돌 강등.** `HandleSceneLoaded`는 거래 씬이 아닌 모든 씬에서 상태를 `Paused`로 바꾼 뒤 정산이 밀려 있으면 그 씬에서 일일 정산을 시작한다 — `EventScene`도 예외가 아니다. `EventLauncher.ResolveHost`가 이 경우 오버레이로 강등한다.

### 10.4 딸린 조치 2건 (선행 작업)

- **`SettingsMenuController` 제한 레이아웃 일반화.** `ApplyP2PMenuLayout` → `ApplyRestrictedMenuLayout`. 판정이 `P2PNetworkSessionManager.IsRunning`에 하드와이어돼 있어 이벤트가 켤 수 없었다. `RestrictedLayoutRequested` 정적 플래그를 OR로 추가. **이걸 안 하면 이벤트 중 설정 창에 저장 버튼이 노출된다** — 자동 저장 금지 요구의 정면 위반. 세우는 쪽이 종료 경로와 `OnDestroy` 양쪽에서 내린다.
- **타자기 발췌.** `YomiRoomTopDownPrototype`(989줄) 안에 말풍선 로그와 함께 박혀 있던 것을 `DialogueTypewriter`로 분리. 방은 스케일드 시간을 그대로 써서 기존 동작이 바뀌지 않고, 이벤트는 **unscaled**를 쓴다 — 설정 메뉴가 `Time.timeScale = 0`을 걸기 때문에 스케일드로 두면 설정을 한 번 열었다 닫는 것만으로 타자기가 영구히 멈춘다.

### 10.5 SaveData

`EventCompletedIds` / `EventChoiceHistory` / `StoryFlags` 3개. **gather·scatter·마이그레이션 코드가 하나도 없다** — 매니저가 아니라 `SaveData` 자신이 주인이라 `EventRunner`가 `CurrentData`에 직접 쓰고 `EventLauncher`가 직접 읽으며, `SaveGame`이 `CurrentData`를 베이스로 삼으므로 자동으로 실려 나간다. 구버전 JSON에 키가 없어도 `JsonUtility`가 초기화자를 유지한다.

### 10.6 검증

에디터 메뉴 3종. `FXOverdose/Debug/Validate Event Data`는 플레이 모드 없이 노드 그래프를 정적 검사한다(ID 중복·시작 노드·끊어진 링크·빈 대사·도달 불가 노드). 이벤트가 40노드를 넘으면 오타 하나가 "그 분기를 고르기 전까지 발견되지 않는" 형태로 숨으므로 이 검사가 필요하다. `Play Sample Event (Overlay|Scene)`은 플레이 모드에서 같은 이벤트를 두 호스트로 띄운다.

### 10.7 감정 스프라이트 배선 (2026-08-16)

`EventNode.Emotion`은 데이터로만 존재하고 렌더에 쓰이지 않는 상태였다(계획 7.9절의 "필드만 먼저 판다"). 스프라이트 96종이 들어와서 배선했다.

- **`EventEmotion` 11종 → 24종** (P2_07 규격). 기존 11종이 24종의 완전한 부분집합이라 이름 충돌 없이 추가만 했고, 기존 이벤트 데이터는 손대지 않았다.
- **enum 이름 = 파일명**이 계약이다 — `Resources/DatingSim/Emotions/Sprites/{T1~T4}/{감정}.png`. `DatingEmotionTableSO`(P2_07 4절)를 만들지 않은 이유가 이것이다. 파일명이 enum 이름과 같으면 테이블이 하는 일이 문자열 연결 한 줄과 같다. 감정별 UI 색상·표시명이 실제로 필요해질 때 만든다.
- ⚠️ **`Resources.LoadAll<Sprite>(...)[0]`을 쓴다.** 감정 PNG가 전부 **Multiple 스프라이트 모드**로 임포트돼 있어(시트당 서브 스프라이트 1장, 이름은 `Calm_0` 꼴) 메인 에셋이 Texture2D다. `Resources.Load<Sprite>`는 여기서 **null을 돌려준다** — 배경 로드와 코드가 달라 보이는 것은 이 때문이다.
- **티어 판정은 `CurrentAffection` 현재치**, 경계 91/61/31 ([Affection_Tier_Table.md](Affection_Tier_Table.md)). 피크를 쓰지 않는 이유는 호감도가 깎였는데 T4 표정이 나오면 연출이 거짓말이 되기 때문. 씬 단독 재생(계획 7.6절)에서는 매니저가 없어 T1로 떨어진다.
- 폴백 사슬: 요청 감정 → Calm → 스탠딩 숨김 + 경고. 배경과 같은 방침이라 아트가 빠져도 이벤트는 완주한다.
- **검증을 `Validate Event Data`에 합쳤다** — 24종 × 4티어 = 96장 로드 확인. 이게 없으면 enum만 늘리고 스프라이트를 안 넣었을 때 *그 감정이 쓰인 대사에 도달했을 때만* 조용히 Calm으로 떨어져서, 노드 그래프 오타와 같은 종류의 "나중에 발견되는" 문제가 된다.
- **크로마키 원본·리테이크 잔재 105장을 `ArtSource/DatingSim/Emotions/`로 뺐다** (`Assets/` 밖). `Resources` 폴더는 **참조 여부와 무관하게 전부 빌드에 실리므로** 쓰지 않는 105장이 그대로 빌드 용량이었다. GUID 참조가 씬·프리팹·에셋 어디에도 없음을 확인하고 옮겼다.

## 11. 트레이딩 시스템 전수 감사 대응 — P0·P1 (2026-08-22)

전체 진단과 남은 항목은 [Trading_System_Audit_Fix_Plan.md](Trading_System_Audit_Fix_Plan.md). 여기에는 **구조가 바뀐 것만** 적는다.

### 11.1 소유권을 옮긴 것

- **요미 수동매매 락에 세대(generation) 개념 도입** (`TradingController`). `ClosePosition`이 `IsManualModeLockedByYomi = false`를 무조건 써서, 바로 위 `OnPositionClosed` 구독자가 건 락을 **같은 콜스택에서** 지우고 있었다. 해제 소유권을 `UnlockManualMode` 하나로 모으고, 임시 락 코루틴은 `manualLockGeneration`이 자기 것일 때만 해제한다. `AITradingBrain`의 언락은 **버그가 아니다** — 강제 고배율 매매를 실행하는 순간이 설계상 락의 끝이다.
- **이벤트 쉴드 만료 판정을 실시간 타이머 단독으로** (`TradingController`). 차트 빔 상태(`IsExternalEventOverride`)를 함께 보던 탓에 ① 빔 없는 선택지는 1프레임 만에, ② 빔이 있어도 빔 수명(인게임 분)과 쉴드(실시간 초)의 **단위 불일치**로 항상 조기 종료됐다. 두 시스템의 수명을 서로 묶지 않는다.
- **상점 최종가 choke point를 `GetPurchasePrice`로 통일** (`ShopManager` / `ShopItemButton`). 표시가는 `GetInflatedPrice`, 차감은 `GetPurchasePrice`로 갈라져 있어 코스튬 할인이 표시에 반영되지 않는 구조였다. `GetInflatedPrice`는 순수 인플레이션 계산으로 남기고, "플레이어가 실제로 내는 값"은 `GetPurchasePrice` 하나가 답한다.

### 11.2 새로 생긴 안전장치

- **`EventLauncher` 씬 감시자.** 정적 `IsRunning`은 씬 재로드로도 안 풀려, 호스트가 죽는 모든 경로를 개별 방어해야 했다. `SceneManager.sceneLoaded`에서 `LoadingScene`/`EventScene`이 아닌 씬에 도착했는데 플래그가 서 있으면 호스트가 없다는 뜻이므로 한곳에서 내린다. `EventOverlayHost.OnDestroy`(일시정지 복구 + `NotifyFinished`)와 짝을 이룬다. **커밋은 하지 않는다** — 중도 이탈 무기록은 설계다.
- **중단 복구를 `OnDisable`에 두는 패턴.** `ChoiceEventController`(팝업 중 컴포넌트 비활성 → `Paused` 고착)와 `ActiveSkillHUDController`(코루틴 중단 → 전체화면 오버레이 잔존)가 같은 형태의 결함이었다. `OnDestroy`만으로는 P2P 진입 시의 `DisableAll<T>()`를 못 잡는다.
- **`LLMSafeGenerator`에 정적 `SemaphoreSlim` 게이트.** `llmAgent.grammar`가 에이전트 전역 설정이라 생성이 겹치면 서로의 GBNF 제약을 지웠다. LLMUnity의 `Chat`에 취소 인자가 없어 진행 중 호출을 끊을 수 없으므로 **애초에 겹치지 않게** 직렬화한다. 게이트 획득 후 취소를 재확인해, 대기 중 취소된 요청은 시작하지 않는다.
- **`CostumeManager.IsAnyEquipped(params string[])`.** 코스튬 효과 적용부가 늘면서 `Instance != null && Instance.EquippedCostumeId == X` 반복이 5곳으로 늘어 헬퍼로 접었다. 기존 인스턴스 메서드 `IsEquipped`를 감싼다.

### 11.3 삭제 — `TraderStatus.PeakBalance`

드로다운 트라우마 천장 기믹(Phase 1 "6대 기믹" ⑥) 전용 필드였다. 그 기믹은 [Mental_Drain_Rebalance_Plan.md](Mental_Drain_Rebalance_Plan.md) 5장으로 폐지됐는데 **그 계획서가 `PeakBalance`를 언급하지 않아** 함께 정리되지 못하고 남았다. 갱신 코드가 없어 항상 0이었고, `AdjustPeakBalanceForExpenditure`는 `peakBalance <= 0f` 가드에서 즉시 return해 호출부 4곳이 전부 no-op이었다.

필드·프로퍼티·메서드·저장 왕복·인스턴스 동기화·`GameManager` 호출 4곳·`SaveData.PeakBalance`를 삭제했다. 구버전 JSON의 남은 키는 `JsonUtility`가 무시하므로 마이그레이션이 필요 없다.

⚠️ **`AchievementManager`의 "누적 최고 자산"과 혼동하지 말 것.** `RecordPeakBalance` / `Stat_GlobalPeakBalance` / `AchievementType.PeakBalance`는 PlayerPrefs 기반 전역 기록으로 백만장자·억만장자 업적을 구동하며, 이번 삭제와 무관하게 정상 동작한다.

> **교훈**: 기믹을 폐지할 때 그 기믹 **전용 데이터 필드**까지 삭제 목록에 넣어야 한다. 이번 건은 로직만 지우고 필드가 남아, 이후 호출부 4곳이 "동작하는 것처럼 보이는 no-op"으로 2년 가까이 남아 있었다.

### 11.4 P2에서 바뀐 소유권

- **`TraderStatus`의 기믹 카운터 4종에 `Owner` 프로퍼티 도입.** `CurrentLosingStreak` / `IsLeverageAddicted` / `ConsecutiveHighLevWins` / `ConsecutiveLowLevTrades`는 다른 mutator와 달리 정본 위임 가드가 없어, 미러 인스턴스에 쓰면 다음 동기화가 조용히 삼켰다. **읽기·쓰기 양쪽**을 `Owner`(정본이 있으면 정본)로 통과시킨다 — 쓰기만 위임하면 미러에서 쓴 직후 읽을 때 한 프레임 낡은 값이 나온다.
- **`MentalDrainGimmickController.FindPlayerBrain()`.** 보스가 스폰되면 `[RequireComponent]` 때문에 두 번째 `AITradingBrain`이 생기고 `FindAnyObjectByType`은 어느 쪽을 줄지 보장하지 않는다. `!IsBossAI` 필터를 한곳에 모았다. 짝으로 `AITradingBrain`의 `TradingController` 구독도 `!IsBossAI`로 막았다 — 보스 브레인이 플레이어의 청산 이벤트를 받고 있었다.
- **인벤토리 복원 루프를 `SaveLoadManager.ApplySavedInventoryItems()`로 분리.** 새 게임 첫 진입 분기가 저장 목록을 통째로 버려, 1일차에 편의점 알바를 먼저 하면 선물이 사라졌다. 두 분기가 같은 헬퍼를 쓰게 해 시작 지급분 위에 얹는다.
- **`ShopManager.GetPurchasePrice`가 최종가의 유일한 답이 되었다** (11.1 참조). 표시 경로도 이쪽으로 통일.

### 11.5 진단이 틀렸던 것 — 코드를 바꾸지 않은 2건

감사 보고를 그대로 적용했으면 **회귀를 만들었을** 항목이다. 같은 지적이 다시 올라오면 여기를 먼저 볼 것.

- **`OnFastForwardEnded`는 중단 시 발화하지 않는 것이 맞다.** 중단된 고속 진행은 남은 분이 보존됐다가 `ResumeGame` / `ResumePreservedFastForward`가 `AdvanceGameMinutes`를 다시 타므로 최종 완료 시점에 반드시 발화한다. 중단 시점에 발화시키면 `MarketSimulationEngine.HandleFastForwardEnded`가 진행 중인 신호 페이즈를 `None`으로 리셋하고 `isExternalEventOverride`까지 내린다 — 잠시 멈춘 것뿐인데 신호가 취소된다. 선언부 주석만 실제 계약으로 고쳤다.
- **`EventView`의 클릭 이중 소비에 `EventSystem.IsPointerOverGameObject`를 쓰면 안 된다.** `ClickCatcher`가 전체 화면을 덮는 투명 Button이라 이벤트 진행 중에는 그 검사가 항상 참이고, 넣는 순간 대사 진행 입력이 통째로 죽는다. 대신 상단 버튼이 눌린 프레임을 기록해 그 프레임과 다음 프레임의 진행 입력만 무시한다(버튼 처리와 `Update`의 실행 순서가 보장되지 않으므로 1프레임 여유).

또 **`GlobalPFStardustFont`의 전역 폰트 훅**은 지금 무해하다. 씬에 지정된 `font`의 GUID가 `TMP Settings`의 기본 폰트와 같은 에셋이라 덮어쓰기가 no-op이다. 실제로 **다른** 폰트를 지정하게 되는 시점에 예외 마커를 넣으면 된다. `CompactHudTextStabilizer`도 무한 루프가 아니다 — `ForceMeshUpdate`가 `havePropertiesChanged`를 다시 내려 `pendingFrames`가 정상 소진된다.

### 11.6 P3 — 삭제와 소유권 통일

**삭제**(순삭감 약 360줄): `YomiSpriteController`(604줄, 씬 미배치 + `AIVisualController`와 중복), `ChartUIController`의 P2P 차트 경로 전체, `MarketSimulationEngine.TriggerMacroEvent`/`TriggerMarketShock`, `TradingController.SimulateCloseForTest`, `AchievementManager.RecordLevelUp`, 검수용 킬스위치 4종, 효과 HUD의 뱃지 아이콘 분기.

- **P2P 차트는 이미 다른 방식으로 동작하고 있었다.** `P2PGameplayUIController.RefreshOriginalChart`가 `MarketSimulationEngine.ApplyP2PExternalTick`으로 기존 엔진에 가격을 먹인다. `ChartUIController.ApplyP2PSnapshot` 계열은 그 이전 접근법의 잔재였다 — 기획 결정 사항이 아니라 폐기된 코드다.
- **`ChartUIController.GetPriceArea`가 가격→Y 변환의 유일한 출처가 되었다.** 현재가 라인·진입선·캔들 본체(`CandleItemUI`에 인자로 전달)·Y축 눈금이 각자 상수(`0.26` / `0.74` 하드코딩)를 갖고 있어 `volumeAreaRatio`를 바꾸면 서로 어긋났고, 현재 값에서도 상단 4px이 맞지 않았다.
- **`TutorialManager.SetButtonsInteractable` → `BlockAllInput()`.** `Button.interactable`을 건드리지 않으면서 그 이름을 달고 있어 "튜토리얼이 버튼 상태를 복원해 줄 것"이라는 잘못된 기대를 만들었다. 이름은 실제 동작을 말해야 한다. `ResetToNormal`도 튜토리얼이 만든 Canvas만 기억해서 지우도록 바꿨다 — 조건만 보고 지우면 원래부터 그 설정이던 남의 Canvas를 파괴한다.
- **`RecordItemPurchase(ItemData)`.** 인자 없이 모든 소모품을 세던 카운터가 "배달음식 200개" 업적(스테이크 해금 게이트)을 구동하고 있었다. 세는 대상을 호출부가 아니라 카운터 자신이 판정하게 했다.

**삭제하지 않은 것과 그 이유.** 감사 보고가 "사문"이라 한 것 중 `EventLogicTemplateSO.HasFallbackText`는 **에디터 스크립트 2곳에서 실제로 쓰이고 있었다** — 삭제 전 호출자 재확인이 필요한 이유다. `GameManager.AdvanceDate`와 `EventLauncher.HasFlag`는 프롤로그·미연시 스토리 작업에서 쓸 물건이라 남겼다. 특히 `AdvanceDate`의 주석은 "보스·스토리 예약일을 건너뛰면 엔딩이 영영 발생하지 않는다"는 비자명한 위험을 담고 있어, 코드와 함께 그 지식이 사라진다.

### 11.7 에디터 빌더 파괴 방지

`TradingViewUIBuilder.BuildTradingChartUI()`는 `TradingViewCanvas`를 통째로 파괴하고 다시 만드는데, **LONG/SHORT 버튼(`LongButtonCard`/`ShortButtonCard`)은 이 빌더가 만들지 않는다** — 씬에 손으로 배치돼 `BottomTradingPanel` 밑에 있어 함께 사라지고, 되살릴 코드가 없다. 게다가 `[InitializeOnLoadMethod]` 자동 실행이 직후 `SaveOpenScenes()`까지 하므로, EditorPrefs 키가 없는 환경(새 클론)에서 프로젝트를 열기만 해도 씬이 파괴된 채 저장된다.

- 자동 실행 경로: 캔버스가 이미 있으면 건너뛴다.
- 수동 경로: 파괴 직전 확인 다이얼로그. 모든 호출자가 이 한곳을 지나므로 폰트 무결성 메뉴도 함께 보호된다.
- **교훈**: 씬을 파괴·재생성하는 빌더는 자기가 만들지 않는 오브젝트를 품고 있는지 먼저 확인해야 한다.

---
*이하 Phase 5 내용은 리팩토링 진행 시 순차적으로 업데이트됩니다.*
