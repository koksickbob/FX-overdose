# 요미(Yomi) 대본 연기형(Scenario-Driven Actor) LLM 아키텍처 구현 계획

## 1. 개요 (패러다임의 전환)
**[PC 플랫폼 독점 아키텍처]** 본 시스템은 모바일 환경을 배제하고 오직 PC(Windows) 환경만을 타겟으로 설계되었습니다.

기존의 "랜덤 자유 채팅(Free Chat) + 과거 기억 RAG" 방식은 챗봇의 한계를 벗어나지 못해 스토리 라이터의 기획 의도를 담아낼 수 없었습니다. 
이를 해결하기 위해, LLM을 단순한 챗봇이 아닌 **'대본을 읽고 상황극을 연기하는 배우(Actor)'**로 취급하는 **대본 연기형(Scenario-Driven) 아키텍처**로 전면 개편합니다.

> [!WARNING]
> **[작업 진행 보류 엄수]**
> 본 문서에 기재된 아키텍처 구현 작업은 전체 설계 및 계획이 완전히 확정(Complete)되기 전까지는 실제 C# 스크립트 작성이나 시스템 수정을 절대 진행하지 마십시오.

---

## 2. 시나리오 주도형(Scenario-Driven) 프롬프트 시스템

스토리 작가가 작성한 수많은 '상황(Scenario)' 데이터를 LLM에게 주입하여 방향성을 100% 통제합니다.

### 2.1. 프롬프트 구조 전면 교체 (Actor Mode)
과거의 기억보다 **현재 부여된 대본(Scene)**을 최우선으로 주입합니다.

```text
[System Rules]
당신은 '요미' 역할을 맡은 배우입니다. 아래의 대본(상황)에 완벽하게 몰입하여 연기하세요.
1. Output language: KOREAN ONLY. 
2. Comprehension: Perfectly understand Korean internet slang and cute variations.

[현재 씬(Scene) 대본]
- 상황(Context): {스토리 작가가 작성한 구체적 상황. 예: 오빠가 새벽에 향수 냄새를 묻히고 옴}
- 목표(Goal): {작가가 LLM에게 지시하는 행동 목표. 예: 의심하고 추궁하되, 마지막엔 믿어줄 것}

[참고용 대사 예시 (작가의 톤앤매너 100% 모방할 것)]
- "오빠... 이 냄새 뭐야? 나 말고 누구 만나고 왔어?"
- "...거짓말. 오빠 눈동자가 흔들리잖아."
```

### 2.2. 시나리오 데이터베이스 (Scenario DB) 구축
스토리 작가가 엑셀이나 에디터 툴을 통해 수천 개의 상황을 손쉽게 작성할 수 있도록 `ScriptableObject` 또는 `JSON` 기반의 대규모 시나리오 DB를 구축합니다.
- `ScenarioID`: 씬 식별자 (예: `SCENE_JEALOUS_01`)
- `ContextDescription`: 상황 설명
- `ActorGoal`: LLM의 행동 지침
- `AllowedEmotions`: 해당 씬에서 허용되는 감정 상태
- `ScriptExamples`: 톤앤매너 가이드용 대사 리스트

---

## 3. 구조화된 메모리 및 연속성(Continuity) 관리

모든 대화를 무식하게 검색(RAG)하는 대신, 시나리오 진행에 필수적인 '핵심 정보'만 압축하여 관리합니다.

### 3.1. 플래그(Flag) 기반 분기 시스템
- 유저의 대화나 선택에 따라 특정 `ScenarioID`가 해금되거나, 분기(Flag)가 켜집니다.
- 예: 유저가 늦게 귀가한 날 변명을 잘못하면 `Flag_LiedToYomi = true`가 저장되고, 다음 날 LLM은 이 플래그가 켜진 씬(Scene) 대본을 전달받아 유저를 압박합니다.

### 3.2. 핵심 요약 메모리 (Core Summary)
- 유저가 남긴 텍스트 전체를 RAG로 가져오는 대신, 씬(Scene)이 종료될 때마다 LLM을 한 번 더 호출하여 **"해당 씬에서 유저가 취한 핵심 스탠스와 거짓말 유무"**를 1~2줄로 요약(Summary)하여 저장합니다.
- 이 요약본은 다음 시나리오의 프롬프트에 `[과거 요약]`으로 주입되어 연속성을 보장합니다.

---

## 4. C# 시스템 아키텍처 개편안

### 4.1. `ScenarioMatcher.cs` (상황 스코어링 엔진 - 핵심)
기존 트레이딩 파트의 `YomiDialogueMatcher` 스코어링 기법을 미연시 파트에 이식합니다. 수만 개의 대본 중 현재 상태와 가장 수학적으로 일치하는 1개의 대본을 추출(Retrieval)합니다.
- **필수 조건(Filtering):** 활성화된 스토리 플래그, 필수 시간대(낮/밤) 불일치 시 -9999점으로 컷오프.
- **가중치(Scoring):**
  - 감정 상태(`DatingMood`) 일치 시 +50점
  - 요구 호감도(`Affection`) / 집착도(`Obsession`)와 현재 유저 스탯의 델타(오차)가 적을수록 높은 가산점 (+30점)
  - 방치 시간(접속 텀) 조건 부합 시 특수 가산점 (+40점)
- **쿨다운(Cooldown):** 최근 출력된 `ScenarioID`를 `HashSet`에 기록하여 앵무새처럼 같은 상황이 반복되지 않도록 방어.

### 4.2. `ScenarioManager.cs` (대본 관리자)
- `ScenarioMatcher`를 호출하여 이번 턴에 LLM이 연기할 최적의 `CurrentSceneContext`를 결정합니다.

### 4.3. `DatingSimLLMController.cs` (수정)
- 더 이상 무작위 Daily RAG를 사용하지 않습니다.
- `ScenarioManager`로부터 전달받은 대본(`CurrentSceneContext`)을 바탕으로 프롬프트를 조립(Actor Mode)합니다.
- 출력은 여전히 JSON(`{"Thought": "...", "Dialogue": "..."}`)을 유지하여 멘헤라 특유의 속마음을 파싱할 수 있게 합니다.

### 4.4. `System/SaveData.cs` (코어 상태 데이터)
- 호감도(`Affection`), 집착도(`Obsession`)
- **[NEW]** 활성화된 씬 플래그 목록 (`List<string> ActiveFlags`)
- **[NEW]** 씬 요약 기록 (`List<string> SceneSummaries`)

---

## 5. 7B 모델 맞춤형 안전장치 유지
시나리오 모드에서도 소형 모델의 한계를 극복하기 위한 기법은 그대로 유지됩니다.
1. **내적 독백 (Inner Monologue):** 겉과 속이 다른 멘헤라 연기를 위해 JSON 강제 출력 유지.
2. **단기 감정 락(Lock):** 작가가 지정한 `AllowedEmotions` 내에서만 감정이 변하도록 C#에서 통제.
3. **네거티브 룰 금지:** 금지어 대신, 긍정적인 '작가의 대사 예시' 모방을 강력히 지시하여 토큰 오염(외계어 출력) 차단.
