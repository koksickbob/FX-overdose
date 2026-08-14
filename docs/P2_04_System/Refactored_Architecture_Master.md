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
*이하 Phase 5 내용은 리팩토링 진행 시 순차적으로 업데이트됩니다.*
