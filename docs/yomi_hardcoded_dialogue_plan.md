# 요미 대사 하드코딩 전환 및 매칭 파이프라인 로드맵

## 1. 개요 (Goal Description)
요미의 주 목적이 대사 창작에서 **거래 방향성 선정**으로 변경됨에 따라, 무거운 LLM 기반 대사 생성을 제거하고 기존 생성된 `yomi_dataset.jsonl` 데이터셋을 활용하여 하드코딩된 대사를 출력하는 파이프라인으로 전환합니다. 
게임 내 현재 상황(시장 트렌드, 멘탈, 포지션, ROE 등)을 기반으로 데이터셋 내 가장 유사한(오차범위 내) 대사를 즉각적으로 찾아 출력하는 가벼운 알고리즘을 구현하는 것이 목표입니다.

## 2. 유저 리뷰 필요 (User Review Required)
> [!IMPORTANT]
> **데이터셋 최적화 방식**: 5,000개의 JSONL 데이터를 게임 런타임에 매번 검색하면 오버헤드가 발생할 수 있습니다. 
> 유니티 에디터 상에서 JSONL 파일을 읽어 **ScriptableObject(SO)** 형태의 자체 DB로 변환(Pre-bake)하여 런타임 성능을 극대화하는 방식을 제안합니다. 이 방식에 동의하시나요?

> [!WARNING]
> **조건부 규칙 기반 매칭 (Rule-based Matching)**: 수치(ROE)의 정확한 일치보다는 상황과 방향성에 중점을 둡니다.
> 1순위 (필수 조건): 포지션 (Position) 및 현재 주가의 방향성 (이전 캔들 대비 상승/하락) 일치.
> 2순위 (가중치 점수): 멘탈 상태 (Mental_State) 및 시장 흐름 (Market_Trend)의 유사도.
> 이 방식으로 변경하면, 상황에 맞는 찰진 대사가 훨씬 자연스럽게 출력됩니다.

## 3. 제안하는 변경 사항 (Proposed Changes)

### 3.1. 데이터 가공 및 파이프라인 구축 (Editor / Data)
- **`YomiDatasetConverter.cs` (Editor Script)**
  - `yomi_dataset.jsonl` 파일을 파싱하여 유니티 에디터 상에서 `YomiDialogueDatabase` (ScriptableObject)로 변환.
  - JSONL 메타데이터(`Market_Trend`, `Mental_State`, `Position`, `ROE`) 추출.
  - **[추가] 단기 방향성 오토 태깅(Auto-Tagging)**: 대사 텍스트 내 특정 키워드를 분석하여 런타임 매칭에 쓰일 `방향성 태그`를 자동 부여.
    - 예: "양봉", "떡상", "불기둥" 포함 시 -> `Required_Direction: Up`
    - 예: "음봉", "나락", "폭포수" 포함 시 -> `Required_Direction: Down`

### 3.2. 상태 매칭 알고리즘 구현 (Core Logic, 모바일 최적화)
- **`YomiDialogueMatcher.cs` (Runtime Script)**
  - 현재 게임의 상태 데이터를 입력받음 (`System_Status`, `Yomi_Status`).
  - **O(1) 1차 필터링 (딕셔너리 그룹핑)**: 5,000개의 전체 데이터를 매번 순회(O(N))하면 모바일 배터리 및 CPU 소모가 발생할 수 있습니다. 이를 방지하기 위해 ScriptableObject 초기화 시 `Market_Trend`와 `Position`을 조합한 키(Key)를 가진 딕셔너리(HashMap)로 데이터를 미리 그룹화합니다.
  - **조건부 스코어링 (Response Rules System)**:
    - `Current_ROE`의 수익/손실 여부(+/-) 및 **이전 캔들 대비 주가 방향성(Up/Down)**이 일치하는지 우선 확인.
    - **[추가] 무포지션(None) 예외 처리**: 포지션이 없을 경우 주가 방향성보다 '캔들 변동성(Volatility)' 및 거시적 'Market_Trend'에 가중치를 두어 관망용 대사가 적절히 출력되도록 스코어링 보정.
    - `Mental_State` 일치 여부에 가산점(Weight) 부여.
  - **랜덤성 및 쿨다운(Cooldown)**: 가장 높은 점수를 받은 대사 풀(Pool) 중 랜덤 출력. 직전에 사용된 멘탈/카테고리에 쿨다운(예: 5초~10초)을 적용하여 대사 도배 및 반복(앵무새 현상) 방지.

### 3.3. 시스템 연동 및 이벤트 트리거 (Integration & Trigger)
- **`YomiController.cs` (기존 LLM 연동부 수정)**
  - 무거운 LLM API 호출 로직을 비활성화하고 `YomiDialogueMatcher`로 즉각 대사 반환 파이프라인 교체.
- **[추가] 명확한 출력 트리거(Trigger) 규칙 확립**: 
  - 매 프레임이나 매 초 텍스트를 출력하지 않도록 게임 시스템 내 명확한 이벤트 시점을 정의.
  - *트리거 조건 예시*: 
    1. 캔들 마감 시(Candle Close) 단기 방향성에 큰 변화가 있을 때.
    2. 수익률(ROE)이 이전 대비 특정 퍼센트(예: 10%) 이상 급등/급락했을 때.
    3. 플레이어가 새로운 포지션에 진입하거나 종료했을 때.

### 3.4. 레거시 시스템 제거 및 대규모 구조 개편 (Legacy Refactoring)
- **기존 AI 대사 시스템 완전 삭제**: 
  - 과거에 사용하던 무거운 LLM API 연동 스크립트, 프롬프트 빌더, 통신 대기(Loading) 로직 등을 코드베이스에서 완전히 제거하여 충돌을 방지하고 용량을 최적화합니다.
- **바이패스(Bypass) 시스템 제거**: 
  - 기존에 LLM 응답 지연이나 필터링 오류 시 임시방편으로 작동하던 우회(Bypass) 시스템은 모든 대사가 즉각 확정적으로 출력되는 새로운 구조에서는 불필요하므로 완전히 폐기합니다.
- **단일화된 파이프라인 (Single Source of Truth)**: 
  - 대사 출력의 모든 진입점을 `YomiDialogueMatcher` 하나로 통일합니다. 게임 UI 컴포넌트는 오직 매처(Matcher)가 반환하는 결과값(대사 문자열)만 바라보도록 의존성을 대폭 줄이고 깔끔한 아키텍처로 개편합니다.

## 4. 검증 계획 (Verification Plan)
### Automated Tests
- `YomiDialogueMatcher`에 임의의 극단적인 상태(예: ROE -90%, Bull, 100x Long)를 주입했을 때, 가장 적절한 절망적 대사나 기도매매 대사가 1ms 이내로 매칭되어 반환되는지 단위 테스트(Unit Test) 진행.

### Manual Verification
- 에디터 플레이 모드에서 횡보장, 상승장, 하락장 등을 강제로 전환해가며 요미의 텍스트가 즉각적으로 상황에 맞게 변하는지 UI 창을 통해 육안으로 확인.
