# 요미 대본 연기형(Scenario-Driven Actor) LLM 시스템 종합 매뉴얼

이 문서는 기존의 '자유 채팅(Free Chat)' 챗봇 시스템을 폐기하고, 스토리 라이터의 의도를 100% 반영하는 **'대본 연기형(Actor) 시스템'**으로 아키텍처를 전면 개편하면서 구현된 모든 시스템 단계와 구조를 상세히 기록한 종합 매뉴얼입니다.

---

## 🏗️ Phase 1. 패러다임 전환 (From Chatbot to Actor)
과거 시스템은 유저가 말을 걸면 단순히 기분(Mood)과 과거 기억 조각(RAG)을 기반으로 자유롭게 대답하는 구조였습니다. 이는 탈선(할루시네이션)이 잦고 작가의 기획 의도를 담아낼 수 없었습니다.
현재의 시스템은 LLM을 **'대본을 쥐여주고 상황극을 시키는 배우(Actor)'**로 취급합니다. 작가가 기획한 완벽한 씬(Scene)의 목표와 예시 대사를 주입받아 유저를 압박하거나 유혹합니다.

---

## 💾 Phase 2. 데이터베이스 뼈대 구축 (Data Schema)
수만 개의 시나리오를 관리하기 위해 규격화된 데이터 모델을 구축했습니다.
- **`ScenarioEntry.cs`**: 개별 씬(Scene)의 정보를 담는 최소 단위입니다.
  - **[필터링/스코어링]**: `Category`, `RequiredTime`, `RequiredMood`, `TargetAffection`, `RequiredFlags` 등
  - **[LLM 지시문]**: `ContextDescription`(상황), `ActorGoal`(목표), `DialogueExamples`(작가 예시 대사)
  - **[제어]**: `MaxTurns` (씬 강제 종료를 위한 턴 제한)
- **`ScenarioDatabase.cs`**: 위 Entry들을 리스트로 관리하는 `ScriptableObject` 에셋입니다.

---

## 📝 Phase 2.5. 이원화 저작 파이프라인 (Dual-Track Authoring)
스토리 작가가 기계적인 엑셀 칸에 갇혀 장문의 글을 쓰는 고통을 없애기 위해, **기획/수치(CSV)**와 **스토리/대본(TXT)**을 완벽히 분리하여 작업하고 에디터가 이를 합치는 이원화 파이프라인을 도입했습니다.

### [1] 마스터 수치 기획 (기획자용 CSV)
기획자는 `ScenarioMaster.csv` 파일에 텍스트를 배제한 순수 수치와 플래그만 입력합니다.
- **CSV 헤더:** `ScenarioID, Category, RequiredTime, RequiredMood, TargetAffection, TargetObsession, MinIdleHours, MaxTurns, RequiredFlags, BlockingFlags, RewardAffection, RewardObsession, SetFlagsOnComplete`
- **입력 예시:** `DAILY_NIGHT_AFF2_001`, `Daily`, `Night`, `Anxious`, `50`, `80`, `0`, `5`, ``, `FLAG_BROKE`, `5`, `-5`, `FLAG_NIGHT_DONE`

### [2] 대본 작성 (스토리 작가용 TXT)
스토리 작가는 메모장 등 텍스트 에디터를 열고 파일명을 `[ScenarioID].txt` (예: `DAILY_NIGHT_AFF2_001.txt`)로 저장한 뒤 자유롭게 글을 씁니다.

**[텍스트 작성 양식 예시]**
```text
# Context
오빠가 밤 늦게 야근을 마치고 돌아왔다. 요미는 불안해하며 기다렸다.

# Goal
원망하듯 쳐다보다가, 오빠가 안아주면 금세 풀려서 울먹거려라.

# Dialogues
오빠... 왜 이제 와...
다른 여자 만나고 온 거 아니지...? 거짓말하면 죽여버릴 거야.
흐으... 미안해 내가 너무 예민했어... 일단 안아줘.
```

### [3] 에디터 베이킹 (Baking)
유니티 상단 메뉴 `FX Overdose/AI/Import Scenario (CSV+TXT)` 버튼을 누르면, 시스템이 CSV의 수치 뼈대와 TXT의 살을 완벽하게 맞물려 하나의 `ScenarioEntry` 에셋으로 구워냅니다.

---

## ⚡ Phase 3. 런타임 최적화 및 에디터 툴링 (Optimization)
대규모 데이터 순회 시 발생하는 렉(Lag)과 가비지 컬렉터(GC) 스파이크를 방지하기 위해 두 가지 최적화를 적용했습니다.
1. **에디터 사전 병합 (CSV Baking)**: `ScenarioCSVImporter.cs`를 통해 스토리 작가가 작업한 여러 개의 CSV 파일을 유니티 에디터 상에서 단일 `ScenarioDatabase` 에셋으로 구워냅니다. 런타임에는 무거운 텍스트 파싱이 발생하지 않습니다.
2. **O(1) 해시맵 사전 필터링 (Lookup Table)**: `ScenarioDatabase`가 초기화될 때, 모든 대본을 `카테고리_시간대` (예: `Daily_Night`)를 Key로 하는 `Dictionary`에 담아둡니다. 수만 개의 데이터를 순회할 필요 없이, 조건에 맞는 300개의 후보군만 즉시 뽑아냅니다.

---

## 🎯 Phase 4. 상황 스코어링 엔진 (Scoring Engine)
후보군 중에서 현재 요미의 상태(호감도, 집착도, 방치 시간 등)와 가장 완벽하게 부합하는 1개의 대본을 뽑아냅니다.
- **`ScenarioMatcher.cs`**: 기존 트레이딩 시스템(`YomiDialogueMatcher`)의 철학을 계승한 클래스입니다.
- **다회차 반복(앵무새) 방지 풀링 (Random Pooling)**: 단일 최고점 1개만 뽑으면 다회차 플레이 시 매번 똑같은 상황만 등장하게 됩니다. 이를 막기 위해 최고점 대비 5점 이하의 오차를 가진 'Top-Tier 후보군(Pool)'을 먼저 추려낸 뒤, 그 안에서 무작위(Random)로 1개의 씬을 추출합니다.
- **쿨다운 방어 및 데이터 고갈(Fallback) 우회**: 최근 재생된 `ScenarioID`를 `HashSet`에 보관하여 연속 등장을 막습니다. 단, **작성된 데이터셋이 너무 적어서 현재 매칭 가능한 모든 씬이 쿨다운에 걸려버릴 경우, 게임이 멈추는 것을 방지하기 위해 쿨다운을 강제로 무시하고 씬을 재탐색하는 안전장치**가 내장되어 있습니다.
- **Zero-Allocation**: 매 턴마다 발생하는 메모리 누수를 막기 위해, LINQ(`Where`, `OrderBy`)를 전면 배제하고 순수 `for` 루프만으로 스코어링을 계산하여 가비지(GC) 발생량을 0으로 만들었습니다.

---

## 🧠 Phase 5. 하이브리드 메모리 시스템 (MemoryDB 개편)
기존에 유저의 채팅을 무식하게 통째로 JSON에 박아넣던 방식을 완전히 들어냈습니다.
- **`DatingSimMemoryDB.cs`**:
  - **단기 버퍼 (Short-Term Buffer)**: 현재 진행 중인 씬(Scene) 내부에서 오고 간 최근 10마디의 대화를 캐싱하여, 프롬프트 최하단에 주입합니다. 이를 통해 대화의 티키타카(연속성)가 극도로 자연스러워집니다.
  - **장기 아카이브 (Chunking)**: 씬이 종료(`MaxTurns` 도달)되면, 해당 버퍼의 내용을 `ScenarioID` 단위로 묶어서(Chunk) 파일에 저장합니다. 

---

## 🎭 Phase 6. LLM 프롬프트 조립 로직 (Actor Mode)
최종적으로 LLM에게 지시를 내리는 코어 두뇌를 대수술했습니다.
- **`ScenarioManager.cs` (신설)**: 
  - `ScenarioMatcher`를 호출해 이번 씬의 대본을 가져오고, 턴 수를 카운트하여 씬의 시작과 종료(`MaxTurns`)를 관리합니다.
- **`DatingSimLLMController.cs` (리팩토링)**:
  - 기존의 Random RAG 주입(일상 대사 무작위 추출) 로직을 폐기했습니다.
  - 이제 프롬프트는 오직 **[페르소나 룰] + [현재 씬 대본(상황/목표/예시)] + [단기 버퍼(최근 대화)]** 의 3단 구조로 깔끔하게 조립되어 LLM에 전달됩니다. 모델은 헷갈림 없이 100% 작가의 의도대로 연기하게 됩니다.
