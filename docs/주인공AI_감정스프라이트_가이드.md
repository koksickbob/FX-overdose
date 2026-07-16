# 주인공 AI 감정 스프라이트 및 연출 가이드

본 문서는 주인공 '요미(Yomi)' 트레이더의 정형화된 **19종 감정 시스템(TraderEmotion)** 에 대한 개요와, 추후 스프라이트 및 애니메이션 리소스를 추가할 때 담당자가 참고할 수 있는 가이드라인입니다.

**요미(Yomi) 캐릭터 설정 요약:**
- 특유의 덤벙대고 얼빵한 성격 탓에 모든 아르바이트에서 잘림.
- 전 재산 2500$로 비트코인을 시작했으나 트레이딩에 천재적 재능을 지님.
- 플레이어(오빠/자기)와 결혼하여 부자가 되는 것이 유일한 목표인 애정결핍 멘헤라.
- 자신을 3인칭('요미는~')으로 부름.

---

## 1. 정형화된 19종 감정 (TraderEmotion)

새롭게 확립된 19종 감정은 크게 5가지 카테고리로 분류되며, 게임 내 수익률(ROE), 멘탈 상태(MentalState), 체력, 각종 돌발 이벤트에 따라 실시간으로 변동됩니다.

### 🟢 긍정 감정 (Positive)
- **`Euphoria` (환희/흥분)**: 대박 수익, ROE > +30% 이상일 때 극도의 쾌락 상태
- **`Confident` (자신만만)**: 안정적인 수익권 (ROE +5% ~ +30%), 자신의 실력 과시
- **`Pleased` (만족/기분 좋음)**: 소폭 수익권 (ROE 0% ~ +5%), 애교 부림
- **`Relieved` (안도감)**: 아이템 사용이나 위기 탈출 직후 십년감수
- **`Affectionate` (애정/집착)**: 수익과 무관하게 마스터에게 애정을 강하게 표현할 때

### ⚪ 중립/관망 감정 (Neutral)
- **`Focused` (냉정/집중)**: 포지션 없이 차트 흐름을 날카롭게 주시할 때
- **`Suspicious` (의심/경계)**: 방향성이 모호하여 세력을 의심할 때 (약간의 하락세 시작 시)

### 🟡 부정 감정 - 경미 (Negative - Light)
- **`Anxious` (초조/불안)**: 소폭 손실 (ROE -5% ~ 0%), 호가창 하나하나에 집착
- **`Frustrated` (짜증/좌절)**: 손실폭 확대 (ROE -20% ~ -5%), 타점 실패로 신경질
- **`Regretful` (후회/아쉬움)**: 포지션을 조기 종료했는데 차트가 우리 쪽에 유리하게 날아갈 때(FOMO)
- **`Jealous` (질투)**: 시스템 확장용 예약 (현재 미사용)

### 🔴 부정 감정 - 심각 (Negative - Heavy)
- **`Panicked` (패닉/공포)**: 급격한 폭락/급등 반대 방향, 멘탈 Danger 진입 시 두려움
- **`Despairing` (절망/체념)**: 도저히 손쓸 수 없는 손실, 마스터에게 버려질까봐 절망
- **`Furious` (분노/격앙)**: 시장과 세력을 향해 쌍욕하며 분노 표출
- **`Tearful` (오열/눈물)**: 파산 직전, 강제 청산 시 눈물을 쏟으며 애원

### 🟣 극단/특수 감정 (Extreme/Special)
- **`Manic` (광기/폭주)**: 멘탈 0(Overdose 상태)에서 나오는 통제 불능의 웃음과 광기
- **`Obsessive` (병적 집착)**: 게임오버(특정 루트) 직후의 얀데레풍 집착
- **`Exhausted` (기력 소진)**: 체력 10% 미만 진입, 눈이 감기고 목소리도 안 나오는 상태
- **`Vengeful` (복수심/저주)**: 특정 배드엔딩 시 세력을 향한 피맺힌 증오

---

## 2. 시스템 연동 구조 개요

현재 AI의 감정은 **`TraderEmotionEvaluator.Evaluate()`** 정적 유틸리티에 의해 매 프레임/상황마다 단 1개의 감정으로 도출됩니다.

도출된 `TraderEmotion`은 크게 두 곳으로 전달됩니다:
1. **`AIPromptBuilder`**: LLM에게 대사 생성 시 감정 톤(`ToneDirective`)을 1:1로 매핑하여 주입합니다.
2. **`AIVisualController`**: 현재 도출된 `TraderEmotion`을 애니메이터용 상태인 `ExpressionState`로 치환하여 유니티 Animator로 전달합니다.

> [!IMPORTANT]  
> 현재 `AIVisualController`의 `ExpressionState`는 5종류(Delighted, Confident, Anxious, Desperate, Overdose)만 존재합니다.
> 따라서 현재 시스템은 **19개의 `TraderEmotion`을 5개의 `ExpressionState`로 묶어서** 임시 매핑 중입니다.

---

## 3. 스프라이트/애니메이션 확장 가이드 (To-Do)

추후 그래픽 담당자가 19종 감정에 대응하는 스프라이트 시트 및 애니메이션을 준비한 후, 클라이언트 단에서 연동하는 방법입니다.

### Step 1. `ExpressionState` 확장
1. `Assets/Scripts/AI/AIVisualController.cs`를 엽니다.
2. 상단에 정의된 `public enum ExpressionState` 항목을 `TraderEmotion`의 19종과 동일하게 늘려줍니다. (이름을 동일하게 맞추는 것을 권장합니다.)

### Step 2. 임시 맵핑 로직 제거 및 1:1 대응
1. `AIVisualController.cs` 내부의 `UpdateExpressionState()` 메서드를 찾습니다.
2. 현재 `switch`문으로 19종 감정을 5종으로 묶어주는 아래 로직을 1:1 맵핑으로 직관화합니다.

```csharp
// [기존 묶음 로직 예시]
ExpressionState targetExp = currentEmotion switch
{
    TraderEmotion.Euphoria or TraderEmotion.Manic => ExpressionState.Delighted,
    TraderEmotion.Confident or TraderEmotion.Pleased => ExpressionState.Confident,
    ...
};

// [확장 후 1:1 로직 예시]
// ExpressionState와 TraderEmotion의 enum 순서를 동일하게 맞췄다면 캐스팅으로 1줄 처리가 가능합니다.
ExpressionState targetExp = (ExpressionState)(int)currentEmotion;
```

### Step 3. Unity Animator Controller 수정
1. 주인공 AI의 Animator Controller 창을 엽니다.
2. 파라미터 `ExpressionState` (현재 Int)를 기반으로 동작하는 Transition 조건들을 **0 ~ 18 (19종)로 세분화**합니다.
3. 각 19가지 상태 노드를 만들고, 완성된 표정 애니메이션 클립(또는 Blend Tree)을 할당합니다.
4. (선택사항) 아우라 이펙트나 카메라 흔들림 등은 `ExpressionState`가 `Panicked`, `Manic`, `Overdose` 등일 때 켜지도록 로직이나 애니메이션 이벤트를 통해 제어합니다.

---

## 4. (참고) 감정별 연출 컨셉 제안

스프라이트 작업 시 참고할 수 있는 특징적 연출 제안입니다.

- **`Euphoria` / `Manic`**: 홍조 띤 얼굴, 광기 어린 웃음, 동공이 약간 풀려있음. 화면 주변에 붉은색 또는 보라색 이펙트.
- **`Focused` / `Confident`**: 눈매가 날카롭고 이성적. 턱을 괴거나 팔짱을 낀 포즈.
- **`Anxious` / `Frustrated`**: 식은땀, 입술이나 손톱을 깨무는 애니메이션, 떨리는 동공.
- **`Despairing` / `Tearful`**: 초점 잃은 눈, 눈물이 맺힘, 웅크리거나 화면에 매달리는 듯한 구도.
- **`Exhausted`**: 반쯤 감긴 눈, 다크서클, 미세하게 흔들리는 몸.

이 가이드 문서를 바탕으로 스프라이트를 제작 및 연동해 주시면 LLM의 대사 내용과 완벽하게 일치하는 고차원적인 AI 연출이 가능해집니다.
