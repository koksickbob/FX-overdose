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

## Phase 2: DatingSim Core & LLM Hybrid Architecture

### 1. 개요
* **목표:** 기존 트레이딩 파트의 로직(실시간 시간/체력, 단일 텍스트 박스 기반 대화)과 완전히 독립된 미연시(DatingSim) 파트의 구조를 구축하고, 무한 대화 로깅에 대비한 최적화를 수행합니다.

### 2. 주요 변경 사항 및 아키텍처

#### DatingTimeManager.cs & SaveData.cs (Data/Logic Layer)
* **슬롯 기반 시스템 (Action Point):** 트레이딩의 실시간 타이머와 완벽히 분리된 `DatingTimeSlot`, `DatingStamina` 시스템 구축.
* **영구 보존 원칙 (SaveData):** 호감도(`DatingAffection`), 집착도(`DatingObsession`), 스토리 진행도(`StoryProgressStage`)를 모두 `SaveData`에 편입하여 세이브 무결성 확보.
* **뷰-로직 분리 (Observer Pattern):** 모든 데이터 변경 시 `Action` 이벤트를 방출하여 UI 컨트롤러가 매니저 종속 없이 화면을 갱신하도록 구성.

#### DatingSimMemoryDB.cs & IMemoryRetriever (Memory Layer)
* **주제별 파편화 (Topic Sharding):** 대화 기록을 `MemoryTopic`에 따라 개별 JSON 파일(`SlotX_Memories_Romance.json` 등)로 분리 저장하여 디스크/메모리 부하를 방지.
* **비동기 접근 강제:** 유니티 메인 스레드 블로킹 방지를 위해 모든 I/O를 `async/await` 기반의 비동기 Task로 강제.
* **전략 패턴 확장성:** `IMemoryRetriever` 인터페이스를 도입하여, 현재의 `SimpleTopicRetriever`(단순 텍스트 추출) 구조를 향후 Vector RAG 시스템 기반의 `SemanticRetriever`로 무중단 교체 가능하도록 뼈대 설계 완료.

#### WorldMapManager.cs & Economic Linking (World Layer)
* **트레이딩-미연시 경제 통합:** 알바 보상 획득 및 데이트 자금 소모 시 트레이딩 코어인 `GameManager`의 `ChangeBalance`, `TrySpendBalance` API를 직접 호출하여 자금을 일원화.
* **데이터 분리 (Scriptable Structs):** `PartTimeJobData`, `DateCourseData` 구조체를 통해 알바/데이트 코스의 코스트(체력/슬롯/비용)를 인스펙터에서 리스트로 직관적으로 관리.

#### DatingSimLLMController.cs (LLM Control Layer)
* **2-Layer 감정-호감도 분리 프롬프트:** 장기 상태(호감도/집착도)와 단기 상태(`DatingMood`)를 이원화하여 주입함으로써 멘헤라 성격의 입체성 확보.
* **기억 왜곡 전처리 (Memory Distortion):** `ObsessionLevel`이 임계치를 넘으면 C# 백엔드 단계에서 LLM에 주입될 과거 기억 주변에 왜곡된 텍스트를 몰래 삽입하여 할루시네이션 리스크 없이 멘헤라성을 극대화.
* **JSON 내적 독백 파싱:** `Thought`와 `Dialogue`를 분리하는 JSON 포맷을 강제하고, 한국어 검증기(Validator)를 결합하여 7B 로컬 모델의 불안정성을 완벽 방어.

#### LoadingScreenController.cs (Transition Layer)
* **LLM 로드 시뮬레이션 및 전역 씬 관리:** 트레이딩 씬에서는 차트 시스템을 대기하고, 미연시 씬 전환 시 `RequireLLM` 플래그를 통해 `DatingSimLLMController`의 Qwen2.5-7B 로컬 모델 로딩 완료를 100% 로딩 바 하나로 스무스하게 연결.

---
*이하 Phase 5 내용은 리팩토링 진행 시 순차적으로 업데이트됩니다.*
