# 돌발 선택 이벤트 시스템 전수 검사 및 리팩토링 계획

> **작성일**: 2026-08-12
> **대상 브랜치**: `Dev_koksickbob3`
> **상태**: **Phase 1~5 코드 작업 완료 / Phase 0(실측 로그) 미수행 / 자산 마이그레이션 실행 대기**
> **보고된 증상**: 돌발 이벤트 본문과 요미의 반응에 **LLM 프롬프트가 그대로 출력됨**
>
> 진행 현황과 착수 전 검사에서 드러난 계획 수정 사항은 [12. 진행 현황](#12-진행-현황-2026-08-12) 참고.

---

## 1. 요약

돌발 선택 이벤트는 **텍스트(LLM 생성)** 와 **게임 로직(템플릿 SO)** 을 런타임에 결합해 만드는 구조입니다.
전수 검사 결과, 보고된 증상은 단일 버그가 아니라 **프롬프트 설계 → 모델 파라미터 → 출력 파싱 → 검증**으로 이어지는 파이프라인 **전 구간에 방어가 없어서** 발생하는 복합 결함입니다.

핵심은 이것입니다. **출력이 프롬프트인지 아닌지를 판별하는 장치가 어디에도 없습니다.** 검증기는 오직 "한글이 5글자 이상인가"만 봅니다. 그런데 프롬프트에 박혀 있는 안내 문구 `"[한국어로 이벤트 제목 작성]"` 자체가 한글 11자입니다. 즉 **모델이 프롬프트를 그대로 뱉으면 검증을 통과해서 화면에 표시됩니다.**

여기에 더해, 텍스트와 게임 로직이 서로를 모르는 채 생성되는 **설계 수준의 단절**이 있습니다. 선택지의 실제 효과를 LLM에게 알려주는 코드는 작성되어 있으나 **프롬프트에 주입되지 않고 버려집니다**. 그 결과 텍스트가 맞게 나오더라도 선택지 내용과 무관한 이야기가 될 수 있습니다.

총 **21건**의 결함을 발견했습니다 (치명 5 / 중대 9 / 경미 7).

---

## 2. 시스템 현황

### 2.1. 데이터 흐름

```
[GameManager.OnGameMinuteAdvanced]
        │
        ▼
[ChoiceEventController.OnGameMinuteAdvanced]
        │  발생 60분 전 ──► StartPreFetchingLLMEvent()  (async void)
        │                        │
        │                        ├─► logicTemplates 242개 중 1개 랜덤 선택 ──► activeTemplate
        │                        └─► LLMSafeGenerator.GenerateChoiceEventAsync(template, marketContext)
        │                                    │
        │                                    ├─ 프롬프트 조립 (JSON 스켈레톤 포함)
        │                                    ├─ llmAgent.Chat()          ← Bllossom 3B, CPU
        │                                    ├─ IndexOf('{') ~ LastIndexOf('}') 로 JSON 추출
        │                                    ├─ JsonUtility.FromJson
        │                                    └─ IsValidKoreanText (한글 5자 이상?)
        │                                              │
        │                                              ▼  cachedLLMData
        ▼
[예정 시각 도달] ──► ShowLLMChoiceDialog(activeTemplate, cachedLLMData)
        │                 │
        │                 ├─ 텍스트: LLM 생성분 (제목/본문/요미 대사)
        │                 └─ 로직  : activeTemplate.LogicOptions[0..2]
        ▼
[ChoiceEventPopupUIController.Show] ──► 텍스트 그대로 렌더 (필터 없음)
        ▼
[OnOptionSelected] ──► ApplyOptionEffects / ExecutePlayerDirectionalChoice
```

### 2.2. 자산 규모

| 자산 | 수량 | 위치 |
| --- | --- | --- |
| 하드코딩 이벤트 (`ChoiceEventSO`) | 30개 | `Assets/Resources/Events/` |
| 로직 템플릿 (`EventLogicTemplateSO`) | **242개** | `Assets/Resources/Events/Templates/` |
| 요미 하드코딩 대사 (`YomiDialogueDatabase`) | **810개** | `Assets/YomiDialogueDatabase.asset` |
| LLM 호스트 | 1개 | `TitleScene`의 `LLM_Manager` (`DontDestroyOnLoad`) |

---

## 3. 근본 원인 분석 — 프롬프트가 화면에 나오는 경로

### 3.1. 인과 사슬

**1단계 — 프롬프트에 JSON 스켈레톤이 통째로 들어 있다**

[LLMSafeGenerator.cs:63-68](../../Assets/Scripts/AI/LLM/LLMSafeGenerator.cs#L63-L68)

```csharp
{{
  ""ScenarioTitle"": ""[한국어로 이벤트 제목 작성]"",
  ""ScenarioDescription"": ""[한국어로 이벤트 상황 묘사 작성]"",
  ""AIMonologue"": ""[이 상황에 대한 요미의 다급한 한마디]""
}}
```

`$@"..."` 보간 문자열이므로 `{{` → `{`, `""` → `"`로 전개되어, **프롬프트 안에 문법적으로 완전한 JSON 객체가 존재**합니다.

**2단계 — 모델 설정이 "지시 따르기"에 불리하다**

`TitleScene`의 `LLMAgent` 직렬화 값:

| 설정 | 현재 값 | 문제 |
| --- | --- | --- |
| `_systemPrompt` | `A chat between a curious human and an artificial intelligence assistant.` | **LLMUnity 기본 영어 문구 그대로.** 한국어 JSON 생성기라는 역할 부여가 전혀 없음 |
| `numPredict` | `-1` | 생성 길이 무제한. 프롬프트를 통째로 복창해도 멈추지 않음 |
| `_grammar` | (비어 있음) | LLMUnity가 지원하는 **GBNF 문법 강제를 사용하지 않음** |
| `temperature` | `0.2` | 낮은 온도 + 프롬프트 내 JSON 예시 → **가장 확률 높은 다음 토큰이 "예시 복사"** 가 되기 쉬움 |
| `numGPULayers` (LLM) | `0` | CPU 추론. 생성이 느려 프리페치 지연 유발 |

`LLMSafeGenerator`는 `Awake()`에서 `llmAgent`를 캐싱만 하고 **`systemPrompt`·`temperature`·`numPredict`를 일절 설정하지 않습니다.** (참고: 삭제된 `DatingSimLLMController`는 이 값들을 코드에서 덮어썼습니다. 즉 트레이딩 쪽만 무방비입니다.)

**3단계 — JSON 추출이 "첫 `{` ~ 마지막 `}`" 통짜 슬라이스다**

[LLMSafeGenerator.cs:86-89](../../Assets/Scripts/AI/LLM/LLMSafeGenerator.cs#L86-L89)

```csharp
int start = jsonText.IndexOf('{');
int end = jsonText.LastIndexOf('}');
```

모델이 프롬프트를 복창하면 **첫 `{`는 복창된 스켈레톤의 것**입니다. 뒤이어 진짜 응답이 나오더라도 슬라이스는 두 객체를 걸쳐 잘리거나, 스켈레톤 객체만 잡힙니다.

**4단계 — 검증기가 이를 걸러내지 못한다 (핵심)**

[LLMSafeGenerator.cs:123-142](../../Assets/Scripts/AI/LLM/LLMSafeGenerator.cs#L123-L142)

```csharp
// 한글 음절이 최소 5자 이상 포함되어야 정상적인 한국어 생성으로 간주
return koreanCount >= 5;
```

안내 문구 `[한국어로 이벤트 제목 작성]` = 한글 **11자**. `[이 상황에 대한 요미의 다급한 한마디]` = 한글 **13자**.
→ **검증 통과.** 그대로 `ScenarioTitle` / `AIMonologue`에 실려 UI로 나갑니다.

**5단계 — UI에 필터가 없다**

[ChoiceEventPopupUIController.cs:122-130](../../Assets/Scripts/Events/ChoiceEventPopupUIController.cs#L122-L130) 은 받은 문자열을 길이 제한·이상 패턴 검사 없이 그대로 렌더합니다. 요미 대사는 큰따옴표로 감싸 출력하므로, 안내 문구가 **요미가 말한 것처럼** 보입니다. → 보고된 두 번째 증상과 일치.

### 3.2. 검증 방법 (착수 전 반드시 수행)

위 사슬은 코드 정황상 가장 유력하지만, **실제 모델 출력으로 확정해야 합니다.** 현재 `Logs/Editor.log`에는 `[LLMSafeGenerator]` 로그가 남아 있지 않습니다 (해당 세션에 이벤트가 발생하지 않았거나 로그가 회전됨).

1. `TitleScene`의 `LLMAgent` 인스펙터에서 `debugPrompt`를 켠다
2. GameScene 진입 후 돌발 이벤트를 1회 발생시킨다
3. 콘솔에서 **`[LLMSafeGenerator] 원본 LLM 응답:`** 로그를 확인한다
   - 프롬프트 문구가 그대로 보이면 → 3.1 사슬 확정
   - JSON은 정상인데 내용이 이상하면 → 프롬프트 품질 문제 (5.2 참고)

> [!NOTE]
> 이 확인이 끝나기 전에는 Phase 2 이후를 착수하지 마십시오. 원인이 다르면 처방도 달라집니다.

---

## 4. 발견된 결함 전수 목록

### 4.1. 치명 (게임 표시가 깨지거나 데이터가 무시됨)

| # | 결함 | 위치 | 상세 |
| --- | --- | --- | --- |
| **C1** | 프롬프트 복창을 걸러내는 검증이 없음 | [LLMSafeGenerator.cs:123](../../Assets/Scripts/AI/LLM/LLMSafeGenerator.cs#L123) | 3장 참고. 보고된 증상의 직접 원인 |
| **C2** | 프롬프트에 JSON 스켈레톤이 포함되어 복창 대상 제공 | [LLMSafeGenerator.cs:63](../../Assets/Scripts/AI/LLM/LLMSafeGenerator.cs#L63) | 채워진 예시(few-shot)가 아니라 빈 껍데기를 줌 |
| **C3** | `OverrideDurationSeconds`가 **어디서도 읽히지 않음** | [ChoiceEventController.cs:569, 600](../../Assets/Scripts/Events/ChoiceEventController.cs#L569) | 242개 템플릿·30개 이벤트에 기입된 지속시간(10~15초)이 전부 무시되고 **`150`이 하드코딩**됨. 기획 데이터가 통째로 사문화 |
| **C4** | 방향성 선택지에서 멘탈/체력이 **성공 시에만** 적용되는데 데이터는 **페널티** | [ChoiceEventController.cs:615-625](../../Assets/Scripts/Events/ChoiceEventController.cs#L615-L625) | 템플릿 다수가 `MentalChangeAmount: -117` 같은 음수. 즉 **베팅에 성공하면 멘탈이 폭락하고, 실패하면 아무 일도 없음.** 코드 주석("성공 시에만 보상 지급")과 데이터 의도가 정면 충돌 |
| **C5** | 프리페치 경쟁 상태로 텍스트/로직 불일치 | [ChoiceEventController.cs:337-364](../../Assets/Scripts/Events/ChoiceEventController.cs#L337-L364) | 4.2 R1 참고. **폭락 기사 + 폭등 선택지** 같은 조합이 발생 가능 |

### 4.2. 중대 (오동작·모순·예외 위험)

| # | 결함 | 위치 | 상세 |
| --- | --- | --- | --- |
| **R1** | `async void` 프리페치 + 진행 중 상태 초기화 | `ChoiceEventController.cs:112-114, 127-129, 337` | `ResetDailySchedule`/`ScheduleNextRandomTrigger`가 await 진행 중에 `isFetchingLLM = false`로 되돌림 → **2차 프리페치가 동시 진입**. `activeTemplate`은 await 전에 덮어써지고 `cachedLLMData`는 나중에 끝난 쪽이 씀 → 짝이 어긋남 |
| **R2** | 선택지 힌트를 계산해 놓고 **프롬프트에 넣지 않음** | [LLMSafeGenerator.cs:46-48](../../Assets/Scripts/AI/LLM/LLMSafeGenerator.cs#L46-L48) | `optionAHint`/`B`/`C` 지역변수가 생성만 되고 미사용. **LLM은 선택지가 뭔지 모른 채 기사를 씀** → 텍스트와 메커니즘의 구조적 단절 |
| **R3** | `LogicOptions[0..2]` 무방비 인덱싱 | `LLMSafeGenerator.cs:46-48`, `ChoiceEventController.cs:395-407` | 선택지가 3개 미만인 템플릿이 하나라도 있으면 `IndexOutOfRangeException`. 242개 전수 검사 필요 |
| **R4** | 취소 토큰 없음 | `ChoiceEventController.cs:355` | 씬 언로드/게임 종료 중 await 복귀 시 파괴된 오브젝트에 기록. 예외는 안 나도 유령 상태 유발 |
| **R5** | `Safe` 선택지가 트랩 판정을 받음 | `ChoiceEventController.cs:549-551, 600` | 템플릿의 Safe 옵션은 `OverrideSignalProbTrue: 0` → `isOptionSuccess = false` → `OverrideMarketTrend(beam, 150, **isTrap: true**)`. 포지션을 닫고 관망하는 선택지에 "트랩" 플래그가 붙는 것은 의미 모순 |
| **R6** | 실패 시 빔 부호 자동 반전 규칙이 데이터와 충돌 | `ChoiceEventController.cs:585-599` | 데이터에 이미 음수 빔이 기입된 템플릿이 많아, "이미 반대면 그대로"와 "아니면 70% 반전" 규칙이 템플릿마다 다르게 걸림. 기획자가 결과를 예측할 수 없음 |
| **R7** | `ThemeTag`의 용도가 코드 주석과 다름 | `EventLogicTemplateSO.cs:36` | 주석은 `"BullMarket_Pump"` 같은 태그를 기대하나, 실제 데이터는 **한 문장짜리 상황 서술**. 그대로 프롬프트에 `테마('{themeTag}')`로 들어가 어색한 문장 생성 |
| **R8** | LLM이 없는 경로에서의 동작이 환경마다 다름 | `ChoiceEventController.cs:350` | `LLMSafeGenerator`는 `TitleScene`에만 존재. 에디터에서 GameScene을 직접 재생하면 `Instance == null` → 항상 하드코딩 폴백. **개발 중 테스트와 실제 빌드의 동작이 갈림** |
| **R9** | 요미 대사 자산이 이벤트 팝업에서 미사용 | `YomiDialogueMatcher.GetEventDialogue` | 810개 대사 DB와 전용 API가 이미 있고 게임 내 **12곳에서 사용 중**인데, 가장 눈에 띄는 이벤트 팝업만 LLM에 의존 |

### 4.3. 경미 (정리 대상)

| # | 결함 | 위치 |
| --- | --- | --- |
| **M1** | `GenerateDailySettlementAsync()` 호출자 없음 (죽은 기능) | `LLMSafeGenerator.cs:144` |
| **M2** | `TriggerPrefetchedEvent()` 호출자 없음 (죽은 공개 API) | `ChoiceEventController.cs:297` |
| **M3** | 튜토리얼 주석이 사실과 다름 ("미리 예열된 LLM 데이터 사용"이라 적혀 있으나 실제로는 고정 이벤트 호출) | `TutorialManager.cs:965` |
| **M4** | `ShowLLMChoiceDialog` 실패 시에도 `eventsTriggeredToday++` 증가 | `ChoiceEventController.cs:213-222` |
| **M5** | `Resources.LoadAll` 로 242개 템플릿 전량 상주 | `ChoiceEventController.cs:341` |
| **M6** | `LowMental` 비상 트리거에 `Any` 이벤트가 섞여 뽑힘 | `ChoiceEventController.cs:264-278` |
| **M7** | UI에 텍스트 길이 상한/이상 패턴 필터 없음 | `ChoiceEventPopupUIController.cs:110-130` |

---

## 5. 설계 수준의 문제

### 5.1. 텍스트와 로직이 서로를 모른다

현재 구조는 **로직(템플릿)을 먼저 뽑고 → 그 로직을 모르는 채 텍스트를 생성**합니다. R2 때문에 선택지 정보가 프롬프트에 들어가지 않으므로, LLM이 완벽하게 동작하더라도 다음이 보장되지 않습니다.

- 기사 내용이 "규제 발표"인데 선택지는 "고래 지갑 추적"
- 기사가 폭등을 예고하는데 Safe 옵션의 빔이 `-4.3%`

**텍스트는 로직의 종속 산출물이어야 합니다.** 지금은 둘이 병렬로 만들어져 우연히 맞기를 기대하는 구조입니다.

### 5.2. "생성 실패"의 정의가 없다

현재 폴백 조건은 ① 응답이 빈 문자열 ② JSON 파싱 예외 ③ 한글 5자 미만 — 셋뿐입니다.
**"형식은 맞지만 내용이 쓰레기인 경우"** 를 판정하는 기준이 없습니다. 필요한 판정은 최소한 다음입니다.

- 출력이 프롬프트의 부분 문자열인가 (복창 탐지)
- 안내 문구 마커(`[`, `]`, `작성`, `한마디`)가 남아 있는가
- 길이가 상식 범위인가 (제목 ≤ 40자, 본문 ≤ 200자, 대사 ≤ 60자)
- 요미의 1인칭 말투인가 (`마스터`/`오빠` 호칭 포함 여부 등)

### 5.3. 두 개의 대사 시스템이 통합되어 있지 않다

`YomiDialogueDatabase`(810개, 상황별 태그·조건 매칭 완비)와 LLM 생성이 **서로를 모른 채 공존**합니다. 미연시 자유 채팅을 걷어낸 지금, 트레이딩 쪽만 검증 불가능한 생성 텍스트를 쓰는 것은 일관성이 없습니다.

### 5.4. 기획 데이터의 신뢰성이 무너져 있다

C3(지속시간 무시)과 C4(멘탈 부호 역전)는 **기획자가 인스펙터에 입력한 값이 게임에 반영되지 않거나 반대로 반영된다**는 뜻입니다. 242개 템플릿이 자동 생성된 것으로 보이는데, 그 생성 규칙 자체가 코드 동작과 어긋나 있습니다. 이건 텍스트 문제보다 심각할 수 있습니다.

---

## 6. 리팩토링 방향 (선택 필요)

| 선택지 | 내용 | 장점 | 단점 |
| --- | --- | --- | --- |
| **A. LLM 완전 제거** | 242개 템플릿에 사전 작성 텍스트를 부여하고, 요미 대사는 `YomiDialogueDatabase`로 대체 | 증상 계열 전체 소멸. 3B 모델·LlamaLib(3.8GB) 제거 가능 → 빌드 5.8GB 감소. 미연시 파트와 일관 | 242개 분량의 텍스트 집필 필요 (자동 생성 툴로 완화 가능) |
| **B. LLM 유지 + 파이프라인 경화** | GBNF 문법 강제, 시스템 프롬프트 부여, 복창 탐지 검증기, 선택지 힌트 주입 | 기존 구조 유지, 작업량 최소 | 로컬 3B 모델의 품질 한계는 남음. 검증 통과율이 낮으면 결국 폴백만 보게 됨 |
| **C. 하이브리드 (✅ 권장)** | **사전 작성 텍스트를 기본값으로 삼고**, LLM은 성공했을 때만 덧씌우는 선택적 장식으로 강등 | 최악의 경우에도 정상 텍스트 보장. LLM 품질 개선은 점진적으로 가능. A로 가는 중간 단계이자 안전망 | 두 경로를 모두 유지해야 함 |

**권장은 C입니다.** 근거는 세 가지입니다.

1. 지금 구조는 LLM이 실패하면 **하드코딩 30개 이벤트로 폴백**되는데, 이건 242개 템플릿의 로직을 버리고 완전히 다른 이벤트를 띄우는 것입니다. 즉 **폴백이 로직 일관성을 깨뜨립니다.** 템플릿 단위 사전 텍스트가 있으면 폴백해도 로직이 유지됩니다.
2. C를 구현하면 A로 가는 길이 자동으로 열립니다 (LLM 호출만 끄면 A).
3. B만 하면 C3/C4 같은 데이터-로직 모순이 그대로 남습니다. 이것들은 LLM과 무관하게 반드시 고쳐야 합니다.

---

## 7. 실행 계획

### Phase 0 — 원인 확정 (필수 선행)

1. `debugPrompt` 활성화 후 이벤트 1회 발생, `원본 LLM 응답` 로그 확보
2. 확보한 원문을 이 문서 11절에 첨부
3. 3.1 사슬이 맞는지 판정 → 아니면 계획 재수립

### Phase 1 — 지혈 (증상 즉시 차단, LLM 구조 손대지 않음)

목표: **잘못된 텍스트가 절대 화면에 나가지 않게 한다.**

4. `LLMSafeGenerator`에 **출력 위생 검사기** 신설
   - 프롬프트 복창 탐지: 응답이 프롬프트의 연속 부분과 N자 이상 일치하면 기각
   - 안내 문구 마커 탐지: `[`...`]` 패턴, `작성`, `한마디`, `ScenarioTitle` 등 금칙어
   - 필드별 길이 상한 검사
   - 한 항목이라도 실패하면 **전체 기각 → 폴백**
5. `IsValidKoreanText`의 `koreanCount >= 5` 를 필드별 최소 길이 + 비율 기준으로 교체
6. UI 최종 방어선: `ChoiceEventPopupUIController.Show`에서 길이 상한 초과 시 말줄임 + 경고 로그
7. 컴파일 및 이벤트 10회 강제 발생 테스트

### Phase 2 — 생성 파이프라인 정상화

8. `LLMSafeGenerator.Awake()`에서 모델 파라미터를 **코드로 명시 설정**
   - `systemPrompt`: 한국어 JSON 생성기 역할 부여
   - `numPredict`: 256 등 상한 (무제한 복창 차단)
   - `temperature`: 0.6~0.7 (0.2는 복창을 조장)
9. 프롬프트에서 **빈 JSON 스켈레톤 제거**, 대신 **완전히 채워진 예시 1개**(few-shot)로 교체
10. `LLMAgent.grammar`에 **GBNF JSON 스키마 지정** — 3필드 JSON 외의 출력을 모델 레벨에서 물리적으로 차단 (가장 확실한 해법)
11. **R2 해소**: `GetOptionHint` 결과를 실제로 프롬프트에 주입 → 선택지를 아는 상태로 기사 작성
12. JSON 추출을 첫 `{`~마지막 `}` 슬라이스에서 **균형 잡힌 첫 번째 객체 파싱**으로 교체

### Phase 3 — 구조 결함 수정 (LLM 무관, 단독으로도 가치 있음)

13. **C3**: `option.OverrideDurationSeconds`를 `ExecuteEmergencyTrade` / `OverrideMarketTrend`에 실제 전달. 0 이하면 기본값 150 사용
14. **C4**: 방향성 선택지의 멘탈/체력 적용 규칙 확정 후 코드·데이터 중 한쪽을 맞춤
    - 안 1: 성공 = `MentalChangeAmount` 양수 보상, 실패 = 별도 필드 `MentalPenaltyOnFail`
    - 안 2: 현행 필드를 실패 시 페널티로 재해석하고 성공 보상 필드를 신설
    - **242개 템플릿 재생성이 수반되므로 기획 결정 필요**
15. **R1**: 프리페치를 `async void` → `CancellationTokenSource` 기반으로 전환. 일정 초기화 시 진행 중 작업을 취소하고, 완료 시 **자신이 최신 요청인지 세대 카운터로 확인** 후에만 캐시에 기록
16. **R3**: 템플릿 로드 시 `LogicOptions.Length >= 3` 검증, 미달 템플릿은 풀에서 제외하고 경고
17. **R5/R6**: Safe 선택지의 트랩 판정 제외, 빔 부호 규칙을 데이터 기준 단일 규칙으로 정리
18. **R8**: `LLMSafeGenerator` 부재를 정상 경로로 취급하고 로그 레벨 조정. 에디터 직접 재생 시에도 동일 동작하도록 보장

### Phase 4 — 하이브리드 텍스트 소스 도입 (선택지 C)

19. `EventLogicTemplateSO`에 사전 작성 텍스트 필드 추가
    ```csharp
    public string FallbackTitle;
    [TextArea] public string FallbackDescription;
    public string[] FallbackMonologues;   // 요미 대사 후보
    ```
20. `ShowLLMChoiceDialog`를 **`ShowTemplateEvent`** 로 개편: 텍스트 출처를 `LLM 성공 → LLM 결과 / 실패 → 템플릿 사전 텍스트`로 분기. **템플릿 로직은 어느 쪽이든 유지**
21. 요미 대사는 `YomiDialogueMatcher.GetEventDialogue("ChoiceEvent_{테마}")` 우선 조회 → 없으면 템플릿 후보 → 없으면 LLM
    - `YomiDialogueDatabase`에 `ChoiceEvent_*` 카테고리 신규 집필 필요
22. 242개 템플릿의 사전 텍스트를 채우는 에디터 툴 작성 (`ThemeTag`가 이미 상황 서술이므로 초안 자동 생성 가능)

### Phase 5 — 정리 및 문서화

23. M1~M7 정리 (죽은 API 제거, 주석 수정, 로드 전략 개선)
24. `docs/P2_04_System/Refactored_Architecture_Master.md`에 변경 기록 append
25. 새 한국어 문구 추가 시 `Tools/Prebake All Scripts Text into Font` 재실행

---

## 8. 검증 계획

### 8.1. 재현 및 회귀 절차

돌발 이벤트는 인게임 10:00 이후 하루 2회 랜덤 발생이라 수동 테스트가 매우 느립니다. **테스트용 강제 발생 메뉴를 먼저 만드십시오.**

```
[MenuItem("FX Overdose/Debug/Force Choice Event (LLM)")]
[MenuItem("FX Overdose/Debug/Force Choice Event x20 (텍스트 검증)")]
```

### 8.2. 통과 기준

- [ ] LLM 20회 연속 생성 중 **프롬프트 문구가 화면에 나오는 사례 0건**
- [ ] 생성 실패(폴백) 시에도 **선택지 로직이 기사 내용과 일치**
- [ ] `OverrideDurationSeconds` 값 변경이 실제 빔 지속시간에 반영됨
- [ ] 방향성 선택지 성공/실패 시 멘탈 증감이 기획 의도와 일치
- [ ] 일자 전환 직후 프리페치가 진행 중이어도 텍스트/템플릿 짝이 어긋나지 않음
- [ ] `LogicOptions`가 3개 미만인 템플릿에서 예외가 발생하지 않음
- [ ] 에디터에서 GameScene 직접 재생 시에도 이벤트가 정상 표시됨

---

## 9. 리스크

| # | 리스크 | 대응 |
| --- | --- | --- |
| 1 | **C4 수정은 242개 템플릿 재생성을 동반** | 기획 결정 없이 착수 금지. Phase 3에서 별도 승인 |
| 2 | 템플릿 자산 242개를 스크립트로 일괄 수정하면 되돌리기 어려움 | 수정 전 `Assets/Resources/Events/Templates/` 전체 백업. git에 추적되므로 커밋 분리 |
| 3 | GBNF 문법은 LLMUnity 버전 의존성이 있음 | 적용 전 현재 패키지(`ai.undream.llm`)의 grammar 지원 여부 확인. 미지원이면 Phase 1 검증기로 대체 |
| 4 | 3B 모델은 GBNF를 걸어도 **내용 품질**은 보장 못 함 | 그래서 Phase 4(사전 텍스트)가 본질적 해법. Phase 2만으로 끝내지 말 것 |
| 5 | 텍스트 검증을 강화하면 폴백 비율이 급증할 수 있음 | 폴백 발생률을 로그로 계측. 50% 초과 시 선택지 A(LLM 제거)로 전환 검토 |

---

## 10. 예상 작업량

| Phase | 내용 | 예상 |
| --- | --- | --- |
| 0 | 원인 확정 (로그 확보) | 30분 |
| 1 | 출력 위생 검사기 + UI 방어선 | 3시간 |
| 2 | 프롬프트/모델 파라미터/GBNF/힌트 주입 | 4시간 |
| 3 | 구조 결함 6종 수정 | 6시간 |
| 4 | 하이브리드 텍스트 소스 + 에디터 툴 | 8시간 (+ 텍스트 집필 별도) |
| 5 | 정리 및 문서화 | 2시간 |
| | **합계 (집필 제외)** | **약 24시간** |

---

## 11. 부록 — Phase 0 로그 첨부란

> Phase 0 수행 후 `[LLMSafeGenerator] 원본 LLM 응답:` 로그 원문을 아래에 붙여넣으십시오.

```text
(미수집)
```

---

## 12. 진행 현황 (2026-08-12)

### 12.1 완료

| Phase | 내용 | 상태 |
| --- | --- | --- |
| 0 | 원인 확정 (실측 로그) | ⏳ **미수행** — 플레이모드 필요. 12.4 절차 참고 |
| 1 | 출력 위생 검사기 + UI 방어선 | ✅ 완료 |
| 2 | 모델 파라미터 / GBNF / 프롬프트 / 힌트 주입 / 균형 파싱 | ✅ 완료 |
| 3 | 구조 결함 (C3·C4·R1·R3·R4·R5·R6·R7·R8) | ✅ 코드 완료 / **자산 마이그레이션 실행 대기** |
| 4 | 하이브리드 텍스트 소스 + 사전 텍스트 생성 툴 | ✅ 완료 (초안 생성 툴 포함, 작가 검수 필요) |
| 5 | M1~M7 정리 + 문서화 | ✅ 완료 (M5는 12.3 참고) |

변경 기록은 [Refactored_Architecture_Master.md](Refactored_Architecture_Master.md) 4절에 append 했습니다.

### 12.2 착수 전 전수 검사에서 드러난 계획 수정 사항

계획서 4장의 진단 중 실제 코드/자산과 달랐던 항목입니다.

**① C3은 계획서보다 심각합니다.**
계획서는 "호출부에 150이 하드코딩되어 템플릿 값이 무시된다"고 봤으나, 실제로는
[MarketSimulationEngine.OverrideMarketTrend](../../Assets/Scripts/Trading/MarketSimulationEngine.cs#L964)가
`durationSeconds` 인자를 받자마자 `int durationMins = 30;`으로 덮어써서 **인자 자체가 아예 무시**되고 있었습니다.
호출부가 넘기던 150은 처음부터 아무 효과가 없었습니다. 인자를 되살리고 단위를 `durationInGameMinutes`로 정정했습니다.

**② `OverrideDurationSeconds`는 단위 자체가 잘못 기입돼 있었습니다.**
이 필드의 유일한 실사용처는 `TradingController`의 **이벤트 쉴드(실시간 초)** 인데,
자산에는 템플릿 10/15, 하드코딩 이벤트 2~25가 들어 있습니다. 그대로 살리면 쉴드가
**150초 → 30초(하한)로 줄어드는 미검증 밸런스 변경**이 됩니다.
→ 코드에는 30초 미만을 legacy로 보고 기본값을 쓰는 방어 규칙을 넣었고, 마이그레이션 툴이 자산을 150으로 정규화합니다.
   (현행 동작 보존이 목적입니다. 이제 값이 실제로 먹으므로 기획이 원하는 대로 다시 튜닝할 수 있습니다.)

**③ R3(선택지 3개 미만)은 실제 발생 사례가 없습니다.**
242개 전수 검사 결과 **전부 정확히 3개**를 보유합니다. 방어 코드(`HasValidOptions`)는 넣었지만 우선순위는 낮습니다.

**④ C4의 처방 근거가 데이터로 확인됩니다.**
옵션 타입별 `MentalChangeAmount` 분포:

| 타입 | 개수 | 멘탈 범위 | 기존 적용 시점 |
| --- | --- | --- | --- |
| Safe | 242 | -10 ~ +19 (241개 양수) | 무조건 |
| Aggressive | 62 | **-120 ~ -13 (전부 음수)** | **무조건** |
| SpecialItem | 240 | +15 ~ +39 | 무조건 |
| DirLong/DirShort | 182 | -120 ~ +30 (181개 음수) | **성공 시에만** |

Aggressive와 Directional은 데이터 모양이 동일한데 적용 규칙만 달랐습니다.
채택안: **성공 = `MentalChangeAmount`(보상) / 실패 = `MentalPenaltyOnFail`(페널티)** 로 분리.

**⑤ 생성기 코드와 자산이 이미 어긋나 있습니다. — 재생성 금지**
[GenerateEventTemplates.cs](../../Assets/Scripts/Editor/GenerateEventTemplates.cs)는
베팅 선택지의 `OverrideSignalProbTrue`를 `Random.Range(0.4f, 0.7f)`로 만들지만,
**현재 자산의 실측값은 0.002~0.399**입니다. 자산은 지금 생성기와 다른 버전으로 만들어졌습니다.
→ 생성기를 재실행하면 성공 확률이 평균 0.2에서 0.55로 뛰어 난이도가 통째로 바뀝니다.
   그래서 재생성이 아니라 **기존 값을 보존하는 마이그레이션 툴**을 만들었고, 생성기 메뉴에는 경고 다이얼로그를 달았습니다.

**⑥ GBNF는 지원됩니다.** (계획서 리스크 #3 해소)
`LLMClient.SetGrammar(string)`이 GBNF/JSON schema를 런타임에 받습니다.
단 grammar는 **에이전트 전역 설정**이므로, 다른 용도가 JSON에 묶이지 않도록 호출 직전에 걸고 직후 해제합니다.

### 12.3 M5(242개 템플릿 상주)에 대한 판단

`Resources.LoadAll`로 242개를 상주시키는 것은 유지합니다. 랜덤 선택에 전량이 필요하고,
템플릿은 문자열 몇 개와 숫자 필드로 이뤄진 경량 SO라 실측 부담이 미미합니다.
Addressables 전환은 이번 리팩토링 범위를 넘어서므로 착수하지 않았습니다.

### 12.4 남은 실행 절차 (사용자 수행)

**1단계 — Phase 0: 원인 확정**
1. `TitleScene`의 `LLM_Manager` 오브젝트 → `LLMAgent` 인스펙터에서 `Debug Prompt` 체크
2. `TitleScene`부터 재생 → `GameScene` 진입
   (⚠️ `GameScene`을 직접 재생하면 `LLM_Manager`가 없어 LLM 경로를 검증할 수 없습니다)
3. 메뉴 `FX Overdose/Debug/Force Choice Event x20 (텍스트 검증)` 실행
4. 콘솔의 `[LLMSafeGenerator] 원본 LLM 응답:` 원문을 11절에 첨부
5. 마지막에 출력되는 채택/폴백률 요약으로 통과 기준(8.2 첫 항목) 판정

**2단계 — 자산 마이그레이션** (git 작업 트리가 깨끗한 상태에서)
1. `Tools/FX OVERDOSE/Migrate Event Templates (C3+C4)` — 멘탈 보상/페널티 분리 + 지속시간 정규화
2. `Tools/FX OVERDOSE/Generate Template Fallback Text` — 사전 작성 텍스트 초안 (빈 항목만 채우기 권장)
3. `FX Overdose/Debug/Validate Event Templates (자산 점검)` 으로 잔여 미이행 건수 확인
4. **두 단계를 각각 별도 커밋으로 분리** (리스크 #2)

**3단계 — 폰트 프리베이크**
사전 작성 텍스트로 새 한국어 문구가 들어갔습니다. 글자가 □로 보이면
`Tools/Prebake All Scripts Text into Font` 재실행.

### 12.5 기획 판단이 필요한 잔여 항목

| # | 항목 | 현재 처리 | 필요한 결정 |
| --- | --- | --- | --- |
| 1 | 이벤트 쉴드 150초 vs 차트 드리프트 30인게임분(≈실시간 30초) | 현행 150초 유지 | 쉴드를 드리프트 길이에 맞출지, 지금처럼 넉넉히 둘지 |
| 2 | 베팅 성공 시 멘탈 보상 크기 | `\|페널티\| × 0.25`, 5~30 범위로 자동 유도 | 보상 곡선이 적절한지 |
| 3 | 242개 사전 텍스트 품질 | 조합 표 기반 자동 초안 | 작가 검수 및 개별 수정 |
| 4 | `YomiDialogueDatabase`의 `ChoiceEvent_*` 카테고리 | 미집필 (조회 실패 시 다음 순위로 폴백) | 카테고리별 대사 집필 여부 |
