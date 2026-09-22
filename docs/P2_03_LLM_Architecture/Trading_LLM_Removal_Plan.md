# 투자 파트 LLM 제거 계획 (초안)

> 상태: **완료 (코드·자산·문서). 인게임 플레이 확인만 남음.** 작성·실행 2026-09-22.
> 목표: 투자 파트(`GameScene`)의 돌발 선택 이벤트에서 로컬 LLM 생성을 완전히 걷어내고,
> 제목·본문·요미 대사 전부를 사전 작성 자산에서만 가져오도록 단순화한다.
> 2026-08-12 [DatingSim 자유 채팅 제거](DatingSim_FreeChat_Removal_Plan.md)에 이어, 프로젝트에서 LLM 의존을 완전히 제거한다.

---

## 1. 현황 조사 결과

### 1.1 LLM 진입점은 단 하나뿐이다

| 항목 | 실측 |
| --- | --- |
| LLM 호출 API | `LLMSafeGenerator.GenerateChoiceEventAsync()` **1개** |
| 호출하는 곳 | `ChoiceEventController.StartPreFetchingLLMEvent()`, `ChoiceEventDebugMenu`(에디터) |
| 생성 산출물 | `ScenarioTitle` / `ScenarioDescription` / `AIMonologue` 3필드 |
| LLMUnity 참조 파일 | `LLMSafeGenerator.cs` **1개** |

**CLAUDE.md가 말하는 "일기(diary) 생성"은 코드에 존재하지 않는다.** `GenerateDiary`류 심볼도, 호출부도 없다.
문서만 낡았으므로 이번에 같이 고친다.

### 1.2 하드코딩 경로는 이미 100% 완비되어 있다

- `Assets/Resources/Events/Templates/*.asset` — **242개 전부** `FallbackTitle` / `FallbackDescription` / `FallbackMonologues`가 채워져 있다.
  (`Assets/Scripts/Editor/GenerateTemplateFallbackText.cs`가 `{카테고리}_{흐름}_{위험도}_{시간대}` 조합표로 결정적 생성)
- `Assets/Resources/Events/EVENT_*.asset` — 완전 수기 작성 이벤트 **30개**가 별도로 존재.

→ **LLM은 이미 잉여 경로다.** 제거 작업의 본질은 "새 시스템 구축"이 아니라 "분기 삭제"다.

### 1.3 함정: 요미 대사 DB 1순위 경로는 지금 항상 실패한다

`ChoiceEventController.ResolveYomiLine()`의 우선순위는 다음과 같다.

```
1순위 YomiDialogueDatabase ("ChoiceEvent_{카테고리}_{흐름}" → "ChoiceEvent_Generic")
2순위 LLM 생성 AIMonologue
3순위 템플릿 FallbackMonologues
4순위 고정 문구
```

그런데 `Assets/YomiDialogueDatabase.asset`의 **엔트리 810개 중 `eventCategory`가 `ChoiceEvent_*`인 것은 0개**다.
즉 1순위는 항상 빈손으로 떨어지고, 현재 팝업의 요미 대사는 **LLM → 템플릿 대사** 순으로 나오고 있다.
LLM을 빼면 자동으로 템플릿 `FallbackMonologues` 단독이 된다.

---

## 1.4 진행 현황

| 단계 | 상태 | 비고 |
| --- | :---: | --- |
| 1단계 코드에서 LLM 분기 제거 | ☑ | 어셈블리 4종 전부 오류 0 · 경고 0 |
| 2단계 씬 / 패키지 / 용량 정리 | ☑ | `LLM_Manager` 삭제 · 패키지 제거 · `StreamingAssets/` 폴더 소멸 |
| 3단계 문서 | ☑ | `CLAUDE.md`·`AGENTS.md` 동기화 · 리팩토링 로그 §14 추가 |
| 4단계 검증 | ◐ | 린트·빌드 통과. **인게임 플레이 확인은 미실시** |

### 실제로 한 일

**1단계 — 코드**
- `Assets/Scripts/AI/LLM/` 삭제 (`LLMSafeGenerator` 371줄 + `LLMOutputSanitizer` 383줄)
- `ChoiceEventController`: 프리페치 필드·메서드·10분 연기 루프 제거. `activeTemplate`도 읽는 곳이 없어져 삭제
- `ShowRandomTemplateEvent()` 신설 — 예정 시각에 템플릿을 즉석에서 뽑는다
- `ShowTemplateEvent` / `TryResolveEventText`에서 생성 텍스트 인자 제거, `ResolveYomiLine`은 `FallbackMonologues` → 고정 문구 2단으로 축소
- `ChoiceEventPopupUIController`: `WarnIfPromptResidue` 삭제, `TruncateForDisplay`를 private 헬퍼로 이관
- `ChoiceEventDebugMenu`: 생성 검증 메뉴 2종 삭제

**2단계 — 씬·패키지·용량**
- `TitleScene`에서 `LLM_Manager` 오브젝트(GameObject + `LLMSafeGenerator` + `LLMAgent` + `LLM` + Transform) 및 루트 참조 제거 — 씬 125줄 감소
- CHALLENGE 모드 설명 문구를 「CHALLENGE에서는 AI 자동매매만 잠깁니다.」로 교체
- `Packages/manifest.json` · `packages-lock.json`에서 `ai.undream.llm` 제거 (`newtonsoft-json`은 다른 패키지가 계속 요구하므로 유지)
- `Assets/StreamingAssets/` **폴더 자체가 사라졌다** — LlamaLib(추적 113파일 3.8GB) + Bllossom 3B `.gguf`(1.9GB) + `.meta`. 코드에 `Application.streamingAssetsPath` 사용처가 0곳이라 안전했다
  - ⚠️ 히스토리 용량(pack 4.48GB)은 그대로다. 계획대로 `filter-repo`는 하지 않았다

**3단계 — 문서**: `CLAUDE.md`·`AGENTS.md` 4곳 동시 수정, `Refactored_Architecture_Master.md` §14 추가

**4단계 — 검증 결과**
- `python yomi_dialogue_lint.py` → 2271줄 검사 / 위반 0 (exit 0)
- `dotnet build` × 4 (`Assembly-CSharp`, `-Editor`, `P2P.Core`, `P2P.Core.Tests`) → 전부 오류 0 · 경고 0
- ❗ **남은 확인**: Unity에서 TitleScene → GameScene 진입 후 이벤트 2회 발생을 눈으로 볼 것. 특히 **예정 시각에 바로 뜨는지**(연기 루프 제거의 결과)

### 후속 처리가 필요한 잔여물
- `Assets/_Recovery/0.unity` — 크래시 복구 스냅샷. 삭제된 `LLMSafeGenerator`/`LLMUnity`를 참조하므로 Unity 콘솔에 missing script 경고가 뜬다. 빌드 설정에 없고 게임 콘텐츠도 아니라 손대지 않았다. 쓸모없으면 지우면 된다.
- Unity를 다시 열면 `.csproj`가 재생성되며 LLMUnity 참조가 자동으로 빠진다.

---

## 2. 결정 — A안 채택 (2026-09-22)

**요미 이벤트 대사를 어디서 가져올 것인가 → A안으로 확정, 반영 완료.**

| 안 | 내용 | 비용 | 평가 |
| --- | --- | --- | --- |
| **A (권장)** | 템플릿 `FallbackMonologues` 단독. DB 1순위 분기와 `preferYomiDialogueDatabase` 필드를 삭제 | 코드 삭제뿐 | 대사 726줄이 이미 존재하고, `yomi_dialogue_lint.py`의 검사 범위(`Assets/Resources/Events/**/*.asset`)에 이미 들어있다 |
| B | `YomiDialogueDatabase`에 `ChoiceEvent_*` 엔트리를 신설 | 대사 신규 집필 + 매칭키 설계 | 810개 DB와 형식은 통일되지만, 템플릿 대사와 중복 자산 두 벌을 유지해야 한다 |

→ **A 권장.** 단, 726줄이 조합표 자동 생성 초안이라 실질 문구 다양성은 약 40종이다.
이건 **제거 작업의 블로커가 아니라 별도 집필 부채**로 분리해 추적한다.

---

## 3. 단계별 작업

### 1단계 — 코드에서 LLM 분기 제거 (약 반나절)

**`Assets/Scripts/Events/ChoiceEventController.cs`**
- 삭제할 필드/메서드: `cachedLLMData`, `isFetchingLLM`, `preFetchCts`, `preFetchGeneration`,
  `preFetchAttempted`, `preFetchDeferrals`, `MaxPreFetchDeferrals`, `preFetchMinuteOfDay`,
  `StartPreFetchingLLMEvent()`, `CancelPendingPreFetch()`
- `OnGameMinuteAdvanced`의 **2.5 프리페치 블록**과 **생성 대기 10분 연기 루프** 전체 삭제
- `ShowTemplateEvent(template, cachedLLMData)` → `ShowTemplateEvent(template)` 로 고정
- `TryResolveEventText()`에서 `GeneratedChoiceEventData` 인자와 `usedLLM` 분기 제거
- `ResolveYomiLine()` → A안 기준 **FallbackMonologues → 고정 문구** 2단으로 축소
- ⚠️ **부수효과**: 이벤트가 예정 시각에 정확히 뜬다(최대 인게임 60분 지연이 사라짐). 체감 페이싱이 바뀌므로 4단계에서 확인한다.

**`Assets/Scripts/Events/ChoiceEventPopupUIController.cs`**
- `LLMOutputSanitizer.TruncateForDisplay` 3곳 → 10줄짜리 private 로컬 헬퍼로 인라인
- `LooksLikePromptResidue` 검사 삭제 (생성 텍스트가 없으면 프롬프트 잔재도 없다)

**파일 통째로 삭제**
- `Assets/Scripts/AI/LLM/LLMSafeGenerator.cs` (371줄)
- `Assets/Scripts/AI/LLM/LLMOutputSanitizer.cs` (383줄) — `LLMGenerationStats` 포함
- `.meta` 동반 삭제

**`Assets/Scripts/Editor/ChoiceEventDebugMenu.cs`**
- 생성 통계 리포트 / LLM 생성 테스트 메뉴 삭제, 템플릿 강제 표시 메뉴만 유지

**컴파일 확인**
```
dotnet build "Assembly-CSharp.csproj" -v:m
dotnet build "Assembly-CSharp-Editor.csproj" -v:m
```

### 2단계 — 씬 / 패키지 / 용량 정리 (약 반나절)

- `Assets/Scenes/TitleScene.unity`의 **`LLM_Manager` 오브젝트 삭제** (`LLMSafeGenerator` + `LLMUnity.LLMAgent` + `LLMUnity.LLM` 3컴포넌트)
- TitleScene 내 UI 문구 수정 — "CHALLENGE에서는 캐릭터 대사용 LLM은…" 텍스트가 씬에 박혀 있다
- `Packages/manifest.json`에서 `"ai.undream.llm"` 제거
- `Assets/StreamingAssets/LlamaLib-v2.0.5/` 삭제
  - ⚠️ **git 추적 중이다 — 113파일 / 3.8GB.** 작업트리와 빌드 용량은 즉시 줄지만 **히스토리(pack 4.48GB)는 그대로다.**
  - 히스토리까지 줄이려면 `git filter-repo`가 필요하고, 이건 브랜치 공유자 전원에게 영향을 준다. **이번 범위 밖으로 둘 것을 권장**한다.
- `Assets/StreamingAssets/Models/` (1.9GB, gitignore됨) — 로컬 삭제만 하면 된다

### 3단계 — 문서 (약 1시간)

- `CLAUDE.md` 및 `AGENTS.md`의 "trading half still uses a local LLM" 문단 교체 (두 파일 동기화 필수)
- 같은 문서의 `.gguf` / Bllossom 3B 관련 서술 정리
- `docs/P2_04_System/Refactored_Architecture_Master.md`에 구조 변경 기록 추가
- 본 문서를 제거 기록으로 승격, `docs/P2_03_LLM_Architecture/`의 남은 설계 문서를 `_Deprecated/`로 이동

### 4단계 — 검증

- `python yomi_dialogue_lint.py` → exit 0
- TitleScene → GameScene 진입, **이벤트 2회 발생**까지 플레이하여 팝업 3요소(제목·본문·요미 대사) 정상 표시 확인
- 이벤트 발생 시각이 예정대로인지 확인 (1단계 부수효과)
- 저장/불러오기 1회 — `SaveData`는 **변경 없음**. 프리페치 상태는 애초에 저장되지 않으므로 마이그레이션 불필요
- 폰트 프리베이크는 불필요 (새 한국어 문자열이 늘지 않는다)

---

## 4. 범위 밖 — 손대지 말 것

- **DatingSim 대사 프로바이더** (`IYomiDialogueProvider` / `PlaceholderDialogueProvider`) — 이미 LLM이 없다
- **하드코딩 이벤트 30개** (`Assets/Resources/Events/EVENT_*.asset`) 및 `TriggerRandomEvent` 경로 — 그대로 유지
- **git 히스토리 재작성** — 별도 결정 사안
- **요미 대사 문구 다양화** — 별도 집필 작업으로 분리

---

## 5. 기대 효과

| 항목 | 효과 |
| --- | --- |
| 삭제 코드 | 약 900줄 (LLM 2파일 754줄 + 컨트롤러 프리페치·동시성 약 150줄) |
| 사라지는 개념 | 프리페치 세대 관리, `CancellationTokenSource`, `SemaphoreSlim` 게이트, GBNF 문법 제약, JSON 파싱, 출력 위생 검사, 10분 연기 루프 |
| 용량 | 작업트리·빌드 3.8GB 감소 |
| 런타임 | 모델 로드 대기 제거, 이벤트 발생 시각 결정적, 생성 실패라는 실패 모드 자체가 소멸 |
| 품질 | 표시 텍스트 100%가 사람이 검수 가능한 자산 — 린트로 전수 검사 가능 |
