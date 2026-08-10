# P2_02: 월드맵 및 미니게임(알바) 시스템

## 개요
요미의 방에서 진입할 수 있는 월드맵 씬과 알바, 데이트 코스 진입을 관리하는 시스템 로직을 정의합니다.

## 1. 알바(Part-time Job) 시스템
- **소모 자원**: 타임 슬롯(기본 2), 미연시 체력(`staminaCost`)
- **보상 방식**: 기획 방향(미니게임 구현 보류)에 따라 미니게임을 씬 전환 없이 스킵하고, 즉시 `GameManager.Instance.ChangeBalance`를 통해 트레이딩 코어 자산에 보상금을 합산합니다.
- **주의**: 알바 수익은 트레이딩 수익보다 낮도록 인스펙터 리스트(`PartTimeJobData`)에서 밸런싱합니다.

## 2. 데이트 코스 진입
- **소모 자원**: 
  1. 데이트 비용(`moneyCost`): `GameManager.Instance.TrySpendBalance`를 통해 트레이딩 자금 소모. 잔여금 부족 시 진입 불가.
  2. 타임 슬롯(2~3) 및 미연시 체력(`staminaCost`)
- 자원 소모 성공 시, `OnDateStarted` 콜백을 쏘고 데이트 씬 진입(추후 `LoadingSceneManager` 연동 대기).

## 3. WorldMapUIController (UI 뷰어)
- `WorldMapManager`와 `DatingTimeManager`의 Action 이벤트를 구독하여 화면에 현재 체력, 슬롯, 자산(Balance)을 실시간으로 표기합니다.
- 액션(버튼 클릭) 실패 시 자원 부족 피드백을 발생시켜 사용자 편의성을 높입니다.
