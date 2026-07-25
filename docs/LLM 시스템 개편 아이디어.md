# [FX OVERDOSE] 하이브리드 AI 시스템 개편 및 파이프라인 마이그레이션 계획서

본 계획서는 게임의 코어 매매 판단과 일반 대사는 **C# 하드코딩(Rule-based)**으로 완전히 전환하여 퍼포먼스를 극대화하고, LLM은 오직 **'돌발 이벤트(Choice Event)'**와 **'일일 정산 일기(Daily Settlement)'**에만 개입하도록 아키텍처를 개편하는 로드맵입니다. 할루시네이션 원천 차단 기법(문법 제약, 로직/텍스트 분리) 및 **사전 호출(Pre-fetching) 최적화**를 적용합니다.

## 1. 확정 및 검토 완료 사항 (Resolved)

> [!TIP]
> **사용 모델 및 플러그인 제약 기능 확인 완료**
> - **적용 모델**: `Qwen 2.5 1.5B INT4` (온디바이스 최적화 및 한국어/역할극 성능 밸런스가 가장 뛰어남).
> - **유니티 LLM 플러그인 Grammar 지원**: 확인 결과, 유니티에서 주로 사용하는 `LlamaSharp` 및 `LLM for Unity` 플러그인은 내부적으로 `llama.cpp` 엔진을 사용하므로 **BNF Grammar 및 JSON Schema 강제 제약 기능을 네이티브로 완벽하게 지원**합니다. 이를 통해 포맷 붕괴 없는 100% 안전한 데이터 파싱이 가능합니다.

> [!IMPORTANT]
> **LLM 사전 호출(Pre-fetching) 최적화 적용**
> 온디바이스 1.5B 모델 추론 시 불가피하게 발생하는 지연 시간(Latency)을 유저가 전혀 느끼지 못하도록 **'백그라운드 사전 작동'** 방식을 도입합니다.
> - **일일 정산**: 게임 내 시간 기준 23:50 에 백그라운드에서 LLM에 미리 프롬프트를 전송하여 텍스트를 생성해 둡니다. 24:00 에 정산 UI가 열릴 때는 이미 완성된 텍스트가 0초 만에 즉시 출력됩니다.
> - **돌발 이벤트**: 이벤트 발생 예정 시간 10~20분 전(인게임)에 차트 상황과 이벤트를 확정 짓고 미리 독백을 생성해 둡니다.

- **[해결됨]** ~~돌발 이벤트 뉴스 텍스트 자체(예: "SEC 규제 발표")도 LLM이 차트 상황을 보고 완전히 지어내게 할까요? 아니면 뉴스는 기존 하드코딩(`ChoiceEventSO`)을 유지하고, 해당 뉴스를 본 요미의 반응(독백)만 LLM이 생성하게 할까요?~~
  👉 **결론**: 독백 생성 기획을 폐지하고, **돌발 뉴스 이벤트 자체(뉴스 제목, 내용, 선택지 텍스트)**를 LLM이 모두 생성하도록 개편합니다.
- **[해결됨]** 돌발 이벤트 선택지(Options) 생성 시 LLM 환각(Hallucination)으로 인한 게임 밸런스 붕괴 우려
  👉 **결론**: **'텍스트와 게임 로직의 분리(Separation of Text and Logic)'** 적용. C# 코드가 선택지의 기계적 수치(예: 롱 진입 강제, 차트 빔 +10% 등) 틀을 먼저 고정해두고, LLM에게는 그 수치에 어울리는 **'선택지 제목과 설명 텍스트'만 지어내도록 JSON 스키마를 강제**합니다.
- **[해결됨]** ~~C#으로 개편될 `AITradingBrain`의 상태별 진입 확률 세부 기획 수치는 기획서에 있는 값을 그대로 사용하면 될까요?~~
  👉 **결론**: 이미 하드코딩으로 로직 적용 및 메타데이터 연동 완료됨.

---

## 3. 제안하는 변경 사항 (Proposed Changes)

시스템 개편은 **(1) 레거시 코어 삭제 및 하드코딩 교체**, **(2) LLM 파이프라인 신규 구축** 두 가지 갈래로 진행됩니다.

### 🗑️ 레거시 시스템 제거 및 교체 (Core Refactoring)

기존에 모든 틱(Tick)마다 LLM을 괴롭히던 무거운 파이프라인을 걷어냅니다.

#### [MODIFY] `Assets/Scripts/AI/AITradingBrain.cs`
- **변경 사항**: LLM 의존성 완전 제거. C# 내부의 확률 기반 `State Machine`으로 전면 개편.
- **로직**:
  - `MarketSignal` 발생 시 `TraderStatus`의 멘탈/체력 수치를 읽어옵니다.
  - 수학적 확률(Random)에 따라 `True Signal`에 정확히 진입할지, `False Signal`(휩소)에 속아 넘어갈지 즉각 판단합니다.
  - 판단 결과에 따라 `TradingController`에 즉각 포지션 및 레버리지 값 전송 (Latency 0ms).

#### [MODIFY] `Assets/Scripts/AI/AIPartnerController.cs` (Hub)
- **변경 사항**: 매초/매프레임 LLM에 상태 데이터를 쏘고 프롬프트를 조립하던 기존 폴링(Polling) 루프를 완전 삭제합니다.
- **대체**: 상태 데이터는 오직 `AITradingBrain`과 `AIVisualController`, 그리고 `YomiDialogueMatcher`(하드코딩 대사) 로만 전달됩니다.

#### [DELETE] 기존 LLM 기반 일반 대사 프롬프트 빌더 
- `Assets/Scripts/AI/LLM/AIPromptBuilder.cs` 내의 일반 매매 상황(General Trading) 관련 컨텍스트 조립 로직 전체 삭제.

---

### ✨ 신규 LLM 파이프라인 구축 (New LLM Pipeline & Pre-fetching)

#### [NEW] `Assets/Scripts/AI/LLM/LLMSafeGenerator.cs`
- **역할**: 기존 `LocalLLMService`를 감싸는 상위 안전망 래퍼(Wrapper) 스크립트.
- **기능**:
  - **문법 제약(Grammar/JSON Schema)**: LlamaSharp의 `LLama.Common.Grammar` 기능을 활용하여 JSON 포맷 출력을 강제합니다.
  - **비동기 사전 호출(Async Pre-fetch) 지원**: 메인 스레드를 멈추지 않고 백그라운드 코루틴이나 Task로 추론을 돌려 결괏값만 메모리에 캐싱(Caching)해 두는 기능을 포함합니다.

#### [NEW] `Assets/Scripts/Events/EventLogicTemplateSO.cs` (이벤트 논리 템플릿)
- **추가 사항**: LLM이 연기할 **"기계적 수치와 선택지 결과의 뼈대(Template)"**를 다수 정의하는 시스템 추가.
- 상승장 펌핑 템플릿(롱 강제/익절), 하락장 패닉셀 템플릿(숏 강제/손절), 횡보장 휩소 템플릿 등 수치만 고정된 수십 가지의 `ScriptableObject` 에셋을 미리 제작합니다.
- C#은 현재 차트 상황에 맞는 템플릿을 무작위로 하나 골라서 LLM에게 전달하며, LLM은 해당 템플릿의 분위기에 맞는 뉴스 텍스트로 살을 붙입니다.

#### [MODIFY] `Assets/Scripts/Events/ChoiceEventController.cs` (돌발 이벤트)
- **변경 사항**: 요미 독백 생성 기능을 폐지하고, **돌발 뉴스(이벤트) 자체를 LLM이 생성**하도록 로직 전면 개편.
- **작동 흐름 (텍스트와 로직 분리)**:
  1. (예) 인게임 14:00 경과 시점: 오후 14:30에 발생할 이벤트를 확정하고 C# 코드가 `EventLogicTemplateSO` 풀에서 상황에 맞는 템플릿 하나를 무작위로 뽑아옵니다.
  2. C# 코드가 현재 차트 상황(상승/하락)과 뽑힌 템플릿 정보(선택지의 기계적 결과)를 담아 `LLMSafeGenerator`에 비동기 사전 생성 요청.
  3. LLM은 게임 내부 밸런스 수치는 건드리지 못한 채, 오직 템플릿 상황에 맞는 **뉴스 제목, 내용, 3가지 선택지의 텍스트(제목/설명)**만을 JSON 포맷으로 생성하여 메모리에 저장 (`eventCachedData`).
  4. 인게임 14:30 시점: 게임 시간을 일시 정지하고 `ChoiceEventPopupUIController`를 띄움. 딜레이 0초로 LLM이 쓴 텍스트와 C# 템플릿의 로직을 결합하여 출력.

#### [MODIFY] `Assets/Scripts/UI/DailySettlementUIController.cs` (일일 정산)
- **변경 사항**: 24:00 정산 호출 시점이 아닌, 23:50 경에 텍스트 사전 생성 시작.
- **작동 흐름**:
  1. 인게임 시간 `23:50` 도달 시 `GameManager`가 정산 사전 작업(`PreCalculateDailySettlement`) 이벤트를 호출.
  2. C#이 미리 수치를 요약하여 `LLMSafeGenerator`에 일기 생성 요청.
  3. 인게임 시간 `24:00` 도달 시 정산 UI 오픈. LLM 로딩창 없이 곧바로 일기 텍스트 출력 완료.

---

## 4. 검증 계획 (Verification Plan)

### 1) 퍼포먼스 및 병목 검증 (Performance)
- **비동기 사전 작동(Pre-fetching) 부하 테스트**: 23:50에 LLM이 백그라운드에서 추론을 시작할 때, 인게임 23:50~24:00 사이의 메인 스레드 프레임 드랍(FPS 하락 폭)이 게임플레이에 지장을 주지 않는지 유니티 프로파일러로 확인합니다.
- **매매 지연성(Latency) 테스트**: `AITradingBrain` 개편 후 차트 신호 발생부터 매매 체결 딜레이가 0.05초 미만으로 단축되었는지 확인합니다.

### 2) 안전성 및 포맷 붕괴 테스트 (Anti-Hallucination)
- **Extreme Case 테스트**: AI 멘탈을 0으로, 잔고를 -99%로 극단적으로 조작한 뒤 일일 정산을 호출합니다. Qwen 1.5B 모델이 Grammar 제약의 통제하에 완벽한 폼의 일기만 출력하는지 반복 검증합니다.
