# P2_01: 요미의 방 아키텍처

## 개요
Phase 2 게임 루프의 메인 허브인 '요미의 방' 시스템 구조를 정의합니다.

> [!IMPORTANT]
> **[2026-08-12 변경]** 자유 대화(로컬 LLM) 기능이 제거되었습니다. 대화 UI와 진입 경로는 그대로 남아 있으나,
> 응답 생성은 `FXOverdose.DatingSim.Dialogue.IYomiDialogueProvider` 훅으로 분리되었고 현재는 자리 표시자가 응답합니다.
> → [제거 계획](../P2_03_LLM_Architecture/DatingSim_FreeChat_Removal_Plan.md)

## 핵심 로직
- **행동 선택**: 월드맵 이동 / 요미와 대화 / 휴식 / 트레이딩 시작
- **로딩 분기**: 
  - 방 내부 대화/휴식: 로딩 없음
  - 월드맵/트레이딩/데이트 이동 시: `LoadingSceneManager` 호출

## 1. YomiRoomManager.cs (상태 및 로직 컨트롤러)
- **네임스페이스**: `FXOverdose.DatingSim.YomiRoom`
- **상태 (Enum)**: `Idle`, `Chatting`, `Responding`, `Resting`, `Transitioning`
- **주요 이벤트 (Action)**: 
  - `public event Action<YomiRoomState> OnStateChanged;` : 상태 변경 시 발생. UI 컨트롤러는 이 이벤트를 구독(Subscribe)함.
  - `public event Action OnActionFailed;` : 시간/체력 코스트 부족으로 행동 실패 시 발생.
- **Inspector 노출값**: `restStaminaRecoverAmount` (휴식 체력 회복량), `restTimeSlotCost` (휴식 슬롯 소모량)
  - `freeChatTimeSlotCost`는 자유 대화 제거와 함께 삭제되었습니다. 대체 대화 시스템 확정 시 슬롯 비용을 다시 도입합니다.

## 2. YomiRoomUIController.cs (뷰 컨트롤러 구현 완료)
- 로직에 개입하지 않고 오로지 매니저의 이벤트만 듣고 화면을 갱신하는 스크립트.
- `YomiRoomManager.Instance.OnStateChanged`를 구독하여 버튼 활성화 여부 및 텍스트 갱신.
- `DatingTimeManager.Instance.OnStaminaChanged`, `OnTimeSlotChanged` 등을 구독하여 게이지 및 수치 실시간 갱신.

## 3. 기능별(Feature-based) 도메인 분리 및 네임스페이스 규칙
스파게티 코드를 방지하고 유지보수성을 극대화하기 위해 미연시(DatingSim) 에셋 구조를 다음 4개 도메인으로 분리하여 관리합니다.
- **`FXOverdose.DatingSim.Core`** (`Assets/Scripts/DatingSim/Core/`) : 공통 데이터 구조, 체력/시간 관리 (Data Flow의 Data Layer)
- **`FXOverdose.DatingSim.YomiRoom`** (`Assets/Scripts/DatingSim/YomiRoom/`) : 요미의 방 전담 (현재 `YomiRoomManager` 위치)
- **`FXOverdose.DatingSim.WorldMap`** (`Assets/Scripts/DatingSim/WorldMap/`) : 월드맵/알바 전담
- **`FXOverdose.DatingSim.Dialogue`** (`Assets/Scripts/DatingSim/Dialogue/`) : 요미 응답 대사 공급 인터페이스 전담 (구 `FXOverdose.DatingSim.LLM` 대체)
- **`FXOverdose.DatingSim.Scenario`** (`Assets/Scripts/DatingSim/Scenario/`) : 상황(Scene) 데이터 및 매칭 로직 (LLM 비의존, 현재 호출자 없음)
