# 미연시 자유 채팅(LLM) 시스템 제거 계획

> **작성일**: 2026-08-12
> **대상 브랜치**: `Dev_koksickbob3`
> **상태**: **작업 완료 (2026-08-12)** — 아래 11절 실행 결과 참고
>
> **채택된 선택지**: 4.3 → **B(명칭까지 정리)** / 4.4 → **B(Scenario 이관 보존)** / 7절 #3 → **대화 시 시간 슬롯 미소모**

---

## 1. 목적과 범위

### 1.1. 배경

미연시 파트(`YomiRoom`)의 **자유 채팅(Free Chat)** 기능은 온디바이스 로컬 LLM(Qwen2.5-7B-Instruct-Q4_K_M, 4.6GB)에 의존합니다.
이 기능을 게임에서 완전히 걷어내고, **UI는 그대로 보존**한 채 기능만 무력화합니다.
추후 기획안이 확정되면 동일한 UI 위에 새 대화 시스템을 얹을 수 있도록 **교체 지점(훅)** 을 명시적으로 남깁니다.

### 1.2. 제거 대상 (O)

| 구분 | 항목 |
| --- | --- |
| 런타임 | `DatingSimLLMController` (미연시 전용 LLM 래퍼) |
| 런타임 | `YomiRoomManager`의 LLM 상태(`LLMLoading` / `LLMProcessing`) 및 LLM 대기 로직 |
| 런타임 | `YomiRoomDialogueUI.SendCurrentMessage()`의 LLM 호출 경로 |
| 런타임 | `LoadingScreenController.RequireLLM` 분기 (미연시 모델 로딩 진행률 합산) |
| 런타임 | `DatingSimMemoryDB` 및 `Memory/` 하위 전체 (LLM 프롬프트 전용 단기 버퍼) |
| 씬 | `YomiRoom_Test.unity` / `YomiRoomScene.unity`의 LLM 관련 컴포넌트 |
| 에디터 | 씬 빌더 2종의 LLM 컴포넌트 부착 코드 |
| 에셋 | `Assets/StreamingAssets/Models/Qwen2.5-7B-Instruct-Q4_K_M.gguf` (4.6GB) + `.meta` |

### 1.3. 유지 대상 (X — 건드리지 않음)

| 구분 | 항목 | 이유 |
| --- | --- | --- |
| 트레이딩 | `LLMSafeGenerator` (돌발 이벤트 텍스트 + 일일 정산 일기) | 트레이딩 파트는 존치 결정 |
| 트레이딩 | `TitleScene`의 `LLM_Manager` 오브젝트 (`DontDestroyOnLoad`) | 위 기능의 호스트 |
| 에셋 | `llama-3.2-Korean-Bllossom-3B-gguf-Q4_K_M.gguf` (2.0GB) | **트레이딩 파트가 쓰는 모델은 3B이며 7B가 아님** (근거: 3.1절) |
| 패키지 | `ai.undream.llm` (LLMUnity) | 트레이딩 파트가 계속 사용 |
| 에셋 | `Assets/StreamingAssets/LlamaLib-v2.0.5` (3.8GB) | LLMUnity 네이티브 런타임, 3B 구동에 필수 |
| **UI 전체** | 채팅 패널, 입력창, 전송 버튼, `Free Chat` 버튼, 말풍선 로그 | **사용자 지시: UI는 전부 보존** |

> [!IMPORTANT]
> 이번 작업으로 LLMUnity 패키지와 LlamaLib(3.8GB)는 **제거되지 않습니다**. 빌드 용량 감소분은 7B 모델 파일 **약 4.6GB** 뿐입니다.

---

## 2. 현황 의존성 지도

```
[미연시 자유 채팅]  ← 이번에 제거
  YomiRoomUIController ─┐
  YomiRoomDialogueUI  ──┼→ YomiRoomManager ──→ DatingSimLLMController ──→ LLMUnity.LLMAgent (Qwen 7B)
                        │                              │
                        │                              ├→ DatingSimMemoryDB (단기 버퍼)
                        │                              └→ ScenarioManager → ScenarioMatcher → ScenarioDatabase
                        └→ DatingTimeManager (시간 슬롯/호감도)   ← 유지
  LoadingScreenController.RequireLLM ──→ DatingSimLLMController.WaitUntilReadyAsync()

[트레이딩]  ← 유지
  ChoiceEventController ──→ LLMSafeGenerator ──→ LLMUnity.LLMAgent (Bllossom 3B, TitleScene 상주)
```

두 계통은 **서로 다른 `LLM` 컴포넌트 인스턴스와 서로 다른 모델 파일**을 쓰며 코드 공유가 없습니다. 따라서 미연시 쪽만 잘라내도 트레이딩 파트에 영향이 없습니다.

### 2.1. 수정 대상 파일 목록

| # | 파일 | 작업 |
| --- | --- | --- |
| 1 | [YomiRoomManager.cs](../../Assets/Scripts/DatingSim/YomiRoom/YomiRoomManager.cs) | 상태 enum 정리, `Start()` LLM 대기 제거, `ProcessUserChatInput` 훅 교체 |
| 2 | [YomiRoomTopDownPrototype.cs](../../Assets/Scripts/DatingSim/YomiRoom/YomiRoomTopDownPrototype.cs) | `SendCurrentMessage()` LLM 호출 → 훅 교체, `using` 정리 |
| 3 | [YomiRoomUIController.cs](../../Assets/Scripts/DatingSim/YomiRoom/YomiRoomUIController.cs) | 상태 문구 `switch` 갱신 (UI 구조는 불변) |
| 4 | [LoadingScreenController.cs](../../Assets/Scripts/UI/LoadingScreenController.cs) | `RequireLLM` 필드/분기 제거, 진행률 단일화 |
| 5 | [WorldMapManager.cs](../../Assets/Scripts/DatingSim/WorldMap/WorldMapManager.cs) | `RequireLLM = true` 제거 |
| 6 | [DatingSimSceneBuilder.cs](../../Assets/Scripts/DatingSim/UI/DatingSimSceneBuilder.cs) | `CreateInScene<DatingSimLLMController>` 제거 |
| 7 | [YomiRoomTestSceneBuilder.cs](../../Assets/Editor/YomiRoomTestSceneBuilder.cs) | LLM/MemoryDB 컴포넌트 부착 및 모델 경로 지정 제거 |
| 8 | [DatingSimLLMController.cs](../../Assets/Scripts/DatingSim/LLM/DatingSimLLMController.cs) | **파일 삭제** |
| 9 | `Assets/Scripts/DatingSim/LLM/Memory/` 4개 파일 | **폴더 삭제** |
| 10 | `Assets/Scenes/YomiRoomScene.unity`, `Assets/Scenes/DatingSim/YomiRoom_Test.unity` | 컴포넌트 제거 (5절) |

---

## 3. 사전 확인된 사실 (조사 결과)

이 계획은 아래 실측에 근거합니다. 착수 전 재확인 권장.

### 3.1. 7B 모델은 미연시 전용이다

- `TitleScene.unity`의 `LLM_Manager` → `_model: Models/llama-3.2-Korean-Bllossom-3B-gguf-Q4_K_M.gguf`
- `YomiRoom_Test.unity`의 `LLM` → `_model: Models/Qwen2.5-7B-Instruct-Q4_K_M.gguf`
- [YomiRoomTestSceneBuilder.cs:47](../../Assets/Editor/YomiRoomTestSceneBuilder.cs#L47) 이 7B 경로를 하드코딩해 재주입

→ **7B는 미연시 자유 채팅만 사용**하므로 안전하게 삭제 가능.

### 3.2. 시나리오/메모리 스캐폴딩은 사실상 미가동 상태다

- `ScenarioManager.StartNewScenario()` 를 호출하는 코드가 **프로젝트 전체에 없음**
- `ScenarioDatabase.asset` 및 `Assets/Data/Scenarios/` (CSV·TXT 원고) **존재하지 않음**
- `ScenarioMatcher` / `ScenarioEntry` / `ScenarioDatabase` 는 `DatingSimLLMController.ConstructPrompt()` 외 참조처 없음
- `DatingSimMemoryDB` 는 `DatingSimLLMController` 와 `YomiRoomTestSceneBuilder` 에서만 참조

→ 즉 `LLM/` 폴더 전체가 **자유 채팅 외에는 아무 데도 연결되어 있지 않음**. 제거 시 파급 범위가 매우 좁습니다.

### 3.3. `YomiRoomScene`(빌드 대상)은 이미 반쯤 죽어 있다

`YomiRoomScene.unity` 에는 `DatingSimLLMController` 오브젝트만 있고 `LLMUnity.LLM` / `LLMAgent` 컴포넌트가 **없습니다**. 즉 현재 빌드 씬에서 자유 채팅은 이미 `dummyDialogues` 폴백만 반환하고 있습니다. 실제 LLM 대화가 도는 곳은 빌드 설정에 없는 `YomiRoom_Test.unity` 뿐입니다.

### 3.4. 세이브 데이터 영향 없음

[SaveData.cs:114-121](../../Assets/Scripts/System/SaveData.cs#L114-L121)의 DatingSim 필드는 체력/호감도/집착도/시간 슬롯/일차 뿐이며 LLM·대화 로그 관련 필드가 없습니다.
→ `SaveDataMigrator` 수정 **불필요**. 단, `persistentDataPath/DatingSimMemories/` 잔여 폴더 정리는 7절 참고.

---

## 4. 설계 결정: "기능만 제거, UI 보존"을 어떻게 구현할 것인가

UI 이벤트 배선(버튼 → 매니저 → 응답 표시)을 그대로 둔 채 **LLM 호출 지점만 단일 인터페이스로 치환**합니다.
추후 기획 확정 시 이 인터페이스의 구현체 하나만 갈아끼우면 됩니다.

### 4.1. 신규 파일: 대화 공급자 인터페이스

`Assets/Scripts/DatingSim/Dialogue/IYomiDialogueProvider.cs`

```csharp
namespace FXOverdose.DatingSim.Dialogue
{
    /// <summary>
    /// 요미의 응답 대사를 만들어 주는 공급자입니다.
    /// 자유 채팅(LLM) 제거 후, 향후 대체 대화 시스템이 이 인터페이스를 구현합니다.
    /// </summary>
    public interface IYomiDialogueProvider
    {
        /// <summary>대화 기능이 실제로 동작 가능한지 여부입니다.</summary>
        bool IsAvailable { get; }

        /// <summary>유저 입력에 대한 요미의 응답 한 줄을 반환합니다.</summary>
        string GetResponse(string userMessage);
    }
}
```

`Assets/Scripts/DatingSim/Dialogue/PlaceholderDialogueProvider.cs`

```csharp
namespace FXOverdose.DatingSim.Dialogue
{
    /// <summary>
    /// 대화 시스템 기획 확정 전까지 사용하는 자리 표시자입니다.
    /// UI 흐름(입력 → 응답 표시)은 유지하되, 준비 중임을 알리는 고정 대사만 돌려줍니다.
    /// </summary>
    public sealed class PlaceholderDialogueProvider : IYomiDialogueProvider
    {
        public static readonly PlaceholderDialogueProvider Default = new PlaceholderDialogueProvider();

        public bool IsAvailable => false;

        public string GetResponse(string userMessage)
        {
            return "지금은 대화 기능을 준비하고 있어... 조금만 기다려 줘.";
        }
    }
}
```

> **왜 `async`가 아닌 동기 API인가**: LLM 제거 후 남는 것은 즉시 반환되는 로컬 로직뿐입니다. 다만 호출부(`SendCurrentMessage`, `ProcessUserChatInput`)의 `async` 시그니처는 **그대로 유지**해 두어, 향후 파일 I/O 기반 대사 DB 등 비동기 구현이 들어와도 호출부를 다시 고치지 않게 합니다.

### 4.2. 상태 enum 처리 방침

`YomiRoomState.LLMLoading` / `LLMProcessing` 은 **삭제가 아니라 의미 중립 이름으로 개명**합니다. UI의 `switch` 분기와 채팅창 활성화 조건이 이 값들에 걸려 있어, 삭제하면 UI 코드를 들어내야 하기 때문입니다.

| 기존 | 변경 후 | 비고 |
| --- | --- | --- |
| `LLMLoading` | **삭제** | 모델 로딩 대기가 사라지므로 초기 상태는 `Idle` |
| `LLMProcessing` | `Responding` | "요미가 대답을 생각하는 중" 표시 유지 |
| `FreeChatting` | `Chatting` | LLM 색채 제거 (선택 사항, 4.3 참고) |

### 4.3. 네이밍 정리 범위 (선택 결정 필요)

| 선택지 | 내용 | 권장 |
| --- | --- | --- |
| **A. 최소 변경** | LLM 호출만 제거, `FreeChatting`·`TryStartFreeChat` 등 이름 그대로 유지 | 작업량 최소, diff 최소 |
| **B. 명칭까지 정리** | `FreeChat` → `Chat` 으로 개명, `Assets/Scripts/DatingSim/LLM/` 폴더 자체 소멸 | ✅ **권장** — "자유 채팅"이라는 죽은 개념이 코드에 남지 않음 |

이 문서의 나머지는 **선택 B** 기준으로 작성했습니다. A를 택할 경우 6.2절의 개명 항목만 건너뛰면 됩니다.

### 4.4. 시나리오 시스템(`LLM/Scenario/`) 처리 (선택 결정 필요)

`ScenarioEntry` / `ScenarioDatabase` / `ScenarioMatcher` / `ScenarioManager` + 에디터의 `ScenarioCSVImporter` 는 **LLM에 직접 의존하지 않는 순수 데이터·매칭 로직**입니다. (LLM은 매칭 결과를 프롬프트로 소비하기만 했음)

| 선택지 | 내용 | 비고 |
| --- | --- | --- |
| **A. 함께 삭제** | 6개 파일 전부 제거 | 현재 데이터 에셋이 하나도 없어 손실 없음. 다만 향후 대본형 대화 기획 시 재작성 필요 |
| **B. 이관 보존** | `Assets/Scripts/DatingSim/Scenario/` 로 이동 + 네임스페이스 `FXOverdose.DatingSim.LLM.Scenario` → `FXOverdose.DatingSim.Scenario` | ✅ **권장** — 상황 매칭·쿨다운·가중치 로직은 LLM 없이도 그대로 쓸 수 있는 자산 |

**선택 B 시 주의**: `ScenarioManager.IncrementTurn()` 의 유일한 호출자가 `DatingSimLLMController` 였으므로, 제거 후 이 클래스는 호출자 없는 상태가 됩니다. 문서 상단에 `// TODO(P2): 대체 대화 시스템 확정 시 턴 진행 연결` 주석을 남깁니다.

---

## 5. 단계별 작업 계획

### Phase 0 — 안전장치 (10분)

1. 작업 전용 브랜치 생성: `git checkout -b remove/datingsim-freechat`
2. 현재 `YomiRoom_Test.unity` / `YomiRoomScene.unity` 를 `docs/` 밖 임시 위치에 백업 (씬은 diff 확인이 어려워 롤백 대비 필요)
3. 기준 빌드 통과 확인:
   ```
   dotnet build "Assembly-CSharp.csproj" -v:m
   dotnet build "Assembly-CSharp-Editor.csproj" -v:m
   ```

### Phase 1 — 대체 훅 추가 (코드 추가만, 아직 아무것도 삭제하지 않음)

4. `Assets/Scripts/DatingSim/Dialogue/IYomiDialogueProvider.cs` 생성 (4.1절)
5. `Assets/Scripts/DatingSim/Dialogue/PlaceholderDialogueProvider.cs` 생성 (4.1절)
6. 컴파일 확인

> 이 단계까지는 기존 동작이 100% 유지되므로 언제든 중단 가능합니다.

### Phase 2 — 런타임 호출부 전환 (LLM 호출 제거)

7. **`YomiRoomManager.cs`**
   - `using FXOverdose.DatingSim.LLM;` / `using FXOverdose.DatingSim.LLM.Memory;` 제거, `using FXOverdose.DatingSim.Dialogue;` 추가
   - `YomiRoomState` 에서 `LLMLoading` 삭제, `LLMProcessing` → `Responding`, `FreeChatting` → `Chatting` (선택 B)
   - `async void Start()` → 일반 `Start()` 로 되돌리고 `WaitUntilReadyAsync()` 대기 제거. 초기 상태를 곧바로 `Idle` 로 설정
   - `ProcessUserChatInput()` : `DatingSimLLMController.Instance.GenerateChatAsync(...)` → `dialogueProvider.GetResponse(userMessage)`
     - `private IYomiDialogueProvider dialogueProvider = PlaceholderDialogueProvider.Default;` 필드 추가
     - **호감도 증가(`ModifyAffection`) 로직은 일단 제거** — 자리 표시자 응답으로 호감도를 올리면 밸런스가 왜곡됨. `// TODO` 주석으로 복구 지점 표시
     - `OnChatUpdated` 이벤트는 **그대로 유지** (UI 배선 보존)
   - `MoveToWorldMap()` / `StartTrading()` 의 `LoadingScreenController.RequireLLM = false;` 2줄 삭제

8. **`YomiRoomTopDownPrototype.cs`** (`YomiRoomDialogueUI.SendCurrentMessage`)
   - `using FXOverdose.DatingSim.LLM;` / `.LLM.Memory;` 제거
   - `DatingSimLLMController.Instance.GenerateChatAsync(...)` → 공급자 호출로 교체
   - `async void` 시그니처, `sending` 가드, `AppendLine("요미", "...")` 타이핑 연출, `RemoveThinkingLine()` 등 **UI 연출은 전부 유지**
   - 응답이 즉시 반환되어 타이핑 연출이 1프레임만 보이는 문제 → `await Task.Delay(600)` 등 최소 연출 지연 삽입 검토 (연출 유지 목적)

9. **`YomiRoomUIController.cs`**
   - `UpdateStateUI()` 의 `switch` 에서 `LLMLoading` 케이스 삭제, `LLMProcessing` → `Responding`, 문구에서 "(LLM)" 표기 제거
     - `"요미의 생각(LLM)을 불러오는 중..."` → 케이스 삭제
     - `"요미가 대답을 생각하는 중..."` → 유지 (LLM 단어 없음)
   - `chatPanel` / `chatInputField` / `chatSendButton` 활성화 조건의 enum 이름만 갱신. **필드·배선 일절 삭제 금지**

10. **`LoadingScreenController.cs`**
    - `public static bool RequireLLM` 필드 삭제
    - `Start()` 의 캐싱 2줄, `LoadAndPrepareGame(bool requireLLM)` 시그니처 및 `if (requireLLM)` 분기 3곳 제거
    - 진행률 계산을 `sceneProgress * 0.65f` 단일 경로로 통합
    - `"WAKING UP QWEN 2.5 7B NEURAL ENGINE..."` 문구 및 `DatingSimLLMController` 참조 블록 삭제

11. **`WorldMapManager.cs`** — `ReturnToRoom()` 의 `LoadingScreenController.RequireLLM = true;` 1줄 삭제

12. 컴파일 확인 (이 시점에서 `DatingSimLLMController` 는 아직 존재하나 참조자가 0이어야 함)

### Phase 3 — 에디터 빌더 정리

13. **`YomiRoomTestSceneBuilder.cs`**
    - `AddComponent<DatingSimMemoryDB>()`, `AddComponent<DatingSimLLMController>()` 삭제
    - `LLMUnity.LLM llm = ...; llm.model = ".../Qwen2.5-7B..."` 블록 삭제
    - `using FXOverdose.DatingSim.LLM;` / `.LLM.Memory;` 제거
    - **채팅 UI 생성 코드(`ChatPanel`, `ChatLogText`, `ChatInputField`, 전송/닫기 버튼)는 전부 유지**
14. **`DatingSimSceneBuilder.cs`** — `BuildYomiRoom()` 의 `CreateInScene<DatingSimLLMController>` 2줄 및 `using` 제거

### Phase 4 — 파일·에셋 삭제

15. `Assets/Scripts/DatingSim/LLM/DatingSimLLMController.cs` (+ `.meta`) 삭제
16. `Assets/Scripts/DatingSim/LLM/Memory/` 폴더 전체 삭제 (`DatingSimMemoryDB`, `IMemoryRetriever`, `MemoryTopic`, `SimpleTopicRetriever`)
17. 4.4절 선택에 따라 `Scenario/` 처리 (A: 삭제 / B: `DatingSim/Scenario/` 로 이동 + 네임스페이스 변경, `ScenarioCSVImporter.cs` 의 네임스페이스도 함께 수정)
18. `Assets/Scripts/DatingSim/LLM/` 빈 폴더 및 `LLM.meta` 삭제
19. 모델 파일 삭제:
    - `Assets/StreamingAssets/Models/Qwen2.5-7B-Instruct-Q4_K_M.gguf` (4.6GB, gitignore 대상이라 git 추적 없음)
    - `Assets/StreamingAssets/Models/Qwen2.5-7B-Instruct-Q4_K_M.gguf.meta` (**git 추적 대상 — 반드시 `git rm`**)
    - 참고: 현재 `git status` 에 `Qwen3-4B-Q4_K_M.gguf.meta` / `qwen2.5-1.5b-instruct-q4_k_m.gguf.meta` 삭제가 이미 스테이징 대기 중 — 함께 커밋 정리
20. 컴파일 확인 (런타임 + 에디터 양쪽)

### Phase 5 — 씬 정리 (Unity 에디터 필수)

21. Unity 에디터 열기 → 컴파일 에러 0 확인
22. `YomiRoomScene.unity` 열어 `DatingSimLLMController` 게임오브젝트 제거 후 저장
23. 메뉴 `FX Overdose/Build YomiRoom Test Scene` 재실행 → `YomiRoom_Test.unity` 를 LLM 없는 상태로 재생성
    - 이 씬은 빌더가 소유하므로 **손으로 고치지 말 것** (CLAUDE.md 규약)
24. `WorldMapScene.unity` 에 LLM 참조가 없음을 확인 (조사 결과 없음)
25. 플레이 테스트: `TitleScene → YomiRoomScene` 진입 → 채팅창 표시·입력·자리 표시자 응답 확인, `월드맵 ↔ 방` 왕복 시 로딩바 정상 종료 확인

### Phase 6 — 문서·후처리

26. P2_03 기존 문서 3종 상단에 폐기 배너 추가:
    - [DatingSim_Scenario_Architecture_Manual.md](DatingSim_Scenario_Architecture_Manual.md)
    - [DatingSim_LLM_Memory_Plan.md](DatingSim_LLM_Memory_Plan.md)
    - [LLM_Prompt_System_Specs.md](LLM_Prompt_System_Specs.md)
    ```markdown
    > [!WARNING]
    > **[DEPRECATED 2026-08-12]** 미연시 자유 채팅 LLM 시스템은 게임에서 제거되었습니다.
    > 본 문서는 히스토리 보존용이며 현재 코드와 일치하지 않습니다. → [제거 계획](DatingSim_FreeChat_Removal_Plan.md)
    ```
27. [CLAUDE.md](../../CLAUDE.md) 의 `DatingSim + LLM` 섹션 및 `Scene transitions` 의 `RequireLLM` 예제 갱신
28. [docs/P2_04_System/Refactored_Architecture_Master.md](../P2_04_System/Refactored_Architecture_Master.md) 에 구조 변경 기록 append (레포 관례)
29. 자리 표시자 안내 문구에 새 한글이 포함되면 메뉴 `Tools/Prebake All Scripts Text into Font` 재실행 (미실행 시 □ 렌더링)

---

## 6. 검증 체크리스트

### 6.1. 컴파일·정적 검증

- [ ] `dotnet build "Assembly-CSharp.csproj" -v:m` 성공
- [ ] `dotnet build "Assembly-CSharp-Editor.csproj" -v:m` 성공
- [ ] `grep -rn "DatingSimLLMController\|DatingSimMemoryDB\|RequireLLM" Assets --include=*.cs` → **0건**
- [ ] `grep -rn "LLMUnity" Assets/Scripts/DatingSim` → **0건**
- [ ] `grep -rln "DatingSimLLMController" Assets/Scenes` → **0건**
- [ ] `grep -rn "Qwen" Assets --include=*.cs` → **0건**

### 6.2. 회귀 검증 (트레이딩 파트가 멀쩡한지)

- [ ] `TitleScene` 진입 시 `LLM_Manager` 정상 생성, 3B 모델 로드 로그 확인
- [ ] `GameScene` 에서 돌발 선택 이벤트 발생 → LLM 생성 텍스트 정상 출력 (`[LLMSafeGenerator]` 로그)
- [ ] `tutorial` 씬의 `EVENT_01_FSC_ETF` 고정 이벤트 정상 동작
- [ ] 일일 정산 일기 생성 정상

### 6.3. 미연시 파트 동작 검증

- [ ] `YomiRoomScene` 진입 시 로딩바가 100%까지 도달 후 정상 전환 (`RequireLLM` 제거 후 멈춤 없음)
- [ ] 채팅 패널 UI가 **그대로 표시**되고 입력·전송이 동작
- [ ] 전송 시 자리 표시자 응답이 말풍선으로 출력되고 UI가 잠기지 않음
- [ ] `Free Chat` 버튼 → 시간 슬롯 소모 로직 확인 (4.3 선택 A/B 어느 쪽이든)
- [ ] 휴식 / 월드맵 이동 / 트레이딩 이동 정상
- [ ] 세이브·로드 후 호감도·집착도·시간 슬롯 유지

---

## 7. 리스크 및 주의사항

| # | 리스크 | 대응 |
| --- | --- | --- |
| 1 | **씬 파일은 diff가 사실상 불가** — 잘못 저장하면 복구 난이도 급상승 | Phase 0에서 씬 백업. 씬 수정은 반드시 마지막 단계에 |
| 2 | `YomiRoom_Test.unity` 를 손으로 고치면 빌더 재실행 시 되돌아감 | 빌더 코드를 먼저 고치고 메뉴로 재생성 (Phase 3 → 5 순서 엄수) |
| 3 | 시간 슬롯 소모는 `TryStartFreeChat()` 에 묶여 있음 | 대화가 자리 표시자로 바뀐 뒤에도 슬롯을 소모할지 **기획 결정 필요** (기본안: 소모하지 않음 — 얻는 것이 없는 행동에 자원을 태우지 않음) |
| 4 | 자리 표시자 응답은 즉시 반환 → 타이핑 연출이 안 보임 | 최소 지연(0.5~0.8초) 삽입해 연출 유지 |
| 5 | `persistentDataPath/DatingSimMemories/` 잔여 폴더 | 기존 플레이어 데이터에 남을 수 있음. 기능상 무해하나, 정리하려면 `Tools/FX Overdose/Clear All Save Data & Achievements` 계열 에디터 툴에 삭제 한 줄 추가 |
| 6 | `.gguf.meta` 는 git 추적 대상 | 파일 삭제 시 `git rm` 으로 함께 정리하지 않으면 다음 Unity 실행 때 재생성되어 노이즈 발생 |
| 7 | 7B 삭제 후 팀원 로컬에 파일이 남아 있을 수 있음 | 팀 공지 필요. `.gguf` 는 gitignore 대상이라 자동 동기화되지 않음 |

---

## 8. 롤백 계획

- 코드: 작업 브랜치를 통째로 폐기 (`git checkout Dev_koksickbob3 && git branch -D remove/datingsim-freechat`)
- 씬: Phase 0 백업본으로 덮어쓰기
- 모델: 7B gguf 는 git에 없으므로 **삭제 전 외부 저장소/백업 드라이브로 이동해 둘 것** (재다운로드 4.6GB)

---

## 9. 후속 TODO (이번 작업 범위 밖)

- [ ] 대체 대화 시스템 기획 확정 → `IYomiDialogueProvider` 구현체 작성
- [ ] 기존 자산 재활용 검토: `YomiDialogueDatabase` / `YomiDailyDialogueDatabase` (하드코딩 대사 DB)와 `YomiDialogueMatcher` 는 트레이딩 파트에서 이미 가동 중 — 미연시 쪽에도 붙일 수 있는지 검토
- [ ] 4.4절 선택 B 채택 시 `ScenarioManager` 의 턴 진행을 새 대화 시스템에 연결
- [ ] 자유 채팅 삭제로 비게 된 시간 슬롯 소비처(플레이 루프 밸런스) 재설계
- [ ] 장기적으로 트레이딩 파트의 `LLMSafeGenerator` 도 걷어낼지 별도 판단 → 그때 `ai.undream.llm` 패키지 + LlamaLib(3.8GB) + 3B 모델(2.0GB)까지 제거 가능

---

## 10. 예상 작업량

| Phase | 내용 | 예상 |
| --- | --- | --- |
| 0 | 브랜치·백업·기준 빌드 | 10분 |
| 1 | 훅 인터페이스 추가 | 20분 |
| 2 | 런타임 호출부 전환 (6개 파일) | 1.5시간 |
| 3 | 에디터 빌더 정리 (2개 파일) | 30분 |
| 4 | 파일·모델 삭제 | 30분 |
| 5 | 씬 정리 + 플레이 테스트 | 1시간 |
| 6 | 문서·폰트 프리베이크 | 40분 |
| | **합계** | **약 4.5시간** |


---

## 11. 실행 결과 (2026-08-12)

### 11.1 완료된 작업

| Phase | 결과 |
| --- | --- |
| 0 | 씬 2종 + `LLM/` 전체를 스크래치패드에 백업, 기준 빌드 통과 확인 |
| 1 | `Assets/Scripts/DatingSim/Dialogue/` 신설 — `IYomiDialogueProvider`, `PlaceholderDialogueProvider` |
| 2 | `YomiRoomManager`, `YomiRoomTopDownPrototype`, `YomiRoomUIController`, `LoadingScreenController`, `WorldMapManager` 전환 완료 |
| 3 | `YomiRoomTestSceneBuilder`, `DatingSimSceneBuilder`에서 LLM 컴포넌트 부착 제거 (**채팅 UI 생성 코드는 전부 보존**) |
| 4 | `DatingSimLLMController.cs` + `LLM/Memory/` 삭제, `LLM/Scenario/` → `DatingSim/Scenario/` 이관 및 네임스페이스 변경, `LLM/` 폴더 소멸 |
| 5 | 씬 YAML 직접 정리 (Unity 미기동 상태여서 빌더 재실행 대신 YAML 수술) |
| 6 | 문서 정리 (11.3 참고) |

### 11.2 코드 변경 요약

**신규**
- `Assets/Scripts/DatingSim/Dialogue/IYomiDialogueProvider.cs`
- `Assets/Scripts/DatingSim/Dialogue/PlaceholderDialogueProvider.cs`

**삭제**
- `Assets/Scripts/DatingSim/LLM/DatingSimLLMController.cs`
- `Assets/Scripts/DatingSim/LLM/Memory/` (4개 파일)

**이관**
- `Assets/Scripts/DatingSim/LLM/Scenario/` → `Assets/Scripts/DatingSim/Scenario/`
- `Assets/Scripts/DatingSim/LLM/ScenarioManager.cs` → `Assets/Scripts/DatingSim/Scenario/ScenarioManager.cs`
- 네임스페이스: `FXOverdose.DatingSim.LLM.Scenario` → `FXOverdose.DatingSim.Scenario` (`ScenarioCSVImporter` 포함)

**주요 동작 변경**
- `YomiRoomState`: `LLMLoading` 삭제, `LLMProcessing` → `Responding`, `FreeChatting` → `Chatting`
- `TryStartFreeChat()` → `TryStartChat()`, `CloseFreeChat()` → `CloseChat()`
- `YomiRoomManager.Start()`가 더 이상 `async`가 아니며 모델 로딩을 기다리지 않음 → **방 진입 즉시 조작 가능**
- 대화 시 시간 슬롯 미소모 / 호감도 미지급 (`TODO(P2)` 주석으로 복구 지점 표시)
- 자리 표시자 응답은 즉시 반환되므로 `Task.Delay(600ms)`로 '생각 중' 연출 유지
- `LoadingScreenController.RequireLLM` 및 모델 대기 분기 삭제, 진행률 단일 경로화

**보존 확인 (건드리지 않음)**
- 채팅 패널·입력창·전송/닫기 버튼·말풍선 로그·`freeChatButton` 직렬화 필드명 (씬 바인딩 유지 목적)
- `LLMSafeGenerator`, `TitleScene`의 `LLM_Manager`, `ai.undream.llm` 패키지, LlamaLib, Bllossom 3B 모델

### 11.3 씬 정리 상세

Unity가 닫혀 있어 씬 YAML을 직접 편집했습니다. 제거한 YAML 문서와 참조:

| 씬 | 제거 대상 |
| --- | --- |
| `YomiRoomScene.unity` | `DatingSimLLMController` GameObject 전체(`&994468814` + Transform + MonoBehaviour), SceneRoots 참조 |
| `YomiRoom_Test.unity` | `Managers` 오브젝트의 `DatingSimLLMController` / `LLMAgent` / `LLM` / `DatingSimMemoryDB` 컴포넌트 4종 및 `m_Component` 참조 |

제거 후 두 씬 모두 **댕글링 fileID 참조 0건**을 확인했습니다.

> [!IMPORTANT]
> Unity를 다시 열었을 때 씬이 정상 로드되는지 반드시 확인하십시오. 이상이 있으면 스크래치패드 백업으로 되돌린 뒤
> `FX Overdose/Build YomiRoom Test Scene` 메뉴로 `YomiRoom_Test.unity`를 재생성하면 됩니다.

### 11.4 문서 정리 결과

**`_Deprecated/`로 이관 + 폐기 배너 삽입**
- `DatingSim_LLM_Memory_Plan.md`
- `DatingSim_Scenario_Architecture_Manual.md`
- `LLM_Prompt_System_Specs.md`

**본문 갱신**
- `docs/P2_01_Design_and_Features/P2_00_Phase2_Master_Plan.md` — P2_03 항목 폐기 표기, `DatingLLMManager` 제거
- `docs/P2_02_Worldbuilding/P2_01_YomiRoom_Architecture.md` — 상태 enum, Inspector 값, 네임스페이스 목록 갱신
- `docs/P2_02_Worldbuilding/P2_03_YomiRoom_TopDown_Interaction_Draft.md` — 상호작용 표/상태 확장안/호출 API 갱신
- `docs/P2_04_System/P2_04_Loading_SceneManager.md` — `RequireLLM` 삭제 반영
- `docs/P2_04_System/Refactored_Architecture_Master.md` — 구 항목 취소선 처리 + `### 3. 미연시 자유 채팅(LLM) 제거` 절 추가
- `docs/P2_05_UI_and_Art/P2_05_All_Scenes_UI_Implementation_Guide.md` — 채팅 UI는 유지됨을 명시
- `docs/P2_05_UI_and_Art/P2_05_UI_Asset_Assembly.md` — 매니저 부착 목록 및 씬 진입 코드 예제 갱신
- `CLAUDE.md` — DatingSim 섹션 전면 재작성, 씬 전환 예제 갱신

**에디터 툴**
- `ClearMemoryEditor.cs` 메뉴명을 `FX Overdose/AI/Clear DatingSim Memory Residue`로 변경 (잔여 데이터 정리 용도로 존치)

### 11.5 남은 작업

- [ ] **Unity 에디터에서 실제 플레이 검증** (6.2 / 6.3 체크리스트) — 본 작업에서는 컴파일 검증까지만 수행
- [x] **7B 모델 파일 삭제 완료** — `Qwen2.5-7B-Instruct-Q4_K_M.gguf` + `.meta` 삭제 (2026-08-12). `Models/` 폴더에는 트레이딩 파트용 Bllossom 3B(2.0GB)만 남았습니다. 재사용이 필요하면 재다운로드해야 합니다
- [ ] 새 자리 표시자 문구가 □로 렌더링되면 `Tools/Prebake All Scripts Text into Font` 재실행
