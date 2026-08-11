# P2_05: UI 담당자 전용 가이드 및 조립 문서

## 개요
이 문서는 프로그래머 파트에서 작성된 Phase 2의 미연시 코어 로직(YomiRoom, WorldMap, Dialogue, Loading)을 실제 유니티 에디터 씬 상에서 어떻게 배치하고 UI와 연결해야 하는지 명시하는 **최종 조립 매뉴얼**입니다.
UI 담당자 및 레벨 디자이너는 아래 가이드에 따라 빈 게임 오브젝트를 생성하고 캔버스의 UI 요소들을 인스펙터에 할당해 주십시오.

---

## 1. 요미의 방 (YomiRoomScene) 조립 가이드

### 1) 필요 UI 요소 명세 (Canvas 하위에 생성)
- **상태 표시 텍스트 (`TextMeshProUGUI`)**
  - 체력(Stamina) 텍스트
  - 타임 슬롯(Time Slot) 텍스트
  - 호감도(Affection) 텍스트
  - 집착도(Obsession) 텍스트
  - 현재 상태 알림 텍스트 (피드백 출력용)
- **액션 버튼 (`Button`)**
  - `대화` 버튼 (구 `자유대화`. 버튼 자체는 유지, 응답 생성 기능만 제거됨)
  - `휴식` 버튼
  - `월드맵 외출` 버튼
  - `트레이딩 시작` 버튼

### 2) 필수 매니저 프리팹 (씬 최상단에 생성)
씬 계층 구조(Hierarchy) 최상단에 빈 게임 오브젝트를 3개 생성하고 다음 스크립트를 부착하십시오:
- `YomiRoomManager`
- `DatingTimeManager` (이미 씬 간 전환 시 넘어왔다면 생략 가능)
- ~~`DatingSimLLMController`~~ — **2026-08-12 삭제됨. 더 이상 부착하지 마십시오.**

### 3) UI 컨트롤러 바인딩
1. Canvas 게임 오브젝트에 `YomiRoomUIController` 스크립트를 부착합니다.
2. Inspector 창에서 방금 생성한 **텍스트**와 **버튼**들을 해당 필드(`staminaText`, `restButton` 등)에 모두 드래그 앤 드롭으로 할당합니다.
3. 실행 시 `YomiRoomManager`와 `DatingTimeManager`가 뿜어내는 이벤트에 자동으로 연동되어 UI가 갱신됩니다.

---

## 2. 월드맵 (WorldMapScene) 조립 가이드

### 1) 필요 UI 요소 명세 (Canvas 하위에 생성)
- **상단 재화 바 (`TextMeshProUGUI`)**
  - 체력(Stamina) 텍스트
  - 타임 슬롯(Time Slot) 텍스트
  - 자산 잔고(Balance) 텍스트 (트레이딩 파트 재화)
- **상호작용 UI**
  - 상태 피드백 텍스트 (`TextMeshProUGUI`)
  - `알바 진입` 버튼 (`Button`)
  - `데이트 진입` 버튼 (`Button`)

### 2) 필수 매니저 프리팹 (씬 최상단에 생성)
- 씬 최상단에 빈 게임 오브젝트를 생성하고 `WorldMapManager` 스크립트를 부착합니다.
- *참고: `GameManager`(자금 관리)와 `DatingTimeManager`는 씬 간 유지 객체로 이미 활성화되어 있어야 합니다.*

### 3) UI 컨트롤러 바인딩 및 기획자 밸런싱 세팅
1. Canvas 게임 오브젝트에 `WorldMapUIController` 스크립트를 부착하고, 생성한 버튼과 텍스트들을 Inspector에 할당합니다.
2. **[기획자 세팅]** `WorldMapManager` 게임 오브젝트를 클릭한 뒤, Inspector에서 `Available Jobs`(알바 리스트)와 `Available Dates`(데이트 리스트)를 열어 보상금, 소모 체력, 비용 등을 직접 세팅하십시오.

---

## 3. 공통 로딩 씬 (LoadingScene) 조립 가이드

### 1) 필요 UI 요소 명세 (Canvas 하위에 생성)
- 배경 전체 화면 일러스트 (`Image`)
- 프로그레스 바 (`Slider`)
- 진행률 퍼센트 표시 (`TextMeshProUGUI`)
- 로딩 상태 텍스트 (`TextMeshProUGUI` - 예: "LOADING SCENE DATA...")

### 2) 씬 조립 및 바인딩
- 기존에 존재하던 `LoadingScreenController` 스크립트가 부착된 캔버스/매니저 객체에 방금 생성한 UI 요소들을 할당합니다.

### 3) 씬 진입 트리거 가이드

> [!IMPORTANT]
> **[2026-08-12 변경]** 자유 채팅(로컬 LLM) 제거로 `RequireLLM` 플래그가 삭제되었습니다. 미연시 씬도 트레이딩 씬과 동일하게 처리하십시오.

```csharp
// 모든 씬 이동에 공통으로 적용되는 로직
FXOverdose.UI.LoadingScreenController.TargetSceneToLoad = "YomiRoomScene"; // 전환할 대상 씬 입력
UnityEngine.SceneManagement.SceneManager.LoadScene("LoadingScene");
```

---
*이 가이드대로 UI 바인딩을 완료하면, 미연시 씬과 트레이딩 파트를 아우르는 거대한 Phase 2 경제/시간 루프 시스템이 완벽하게 가동됩니다.*
