# P2_04: 로딩 씬 매니저 아키텍처

## 개요
각 씬 전환과 무거운 데이터 비동기 로딩을 통합 관리하는 시스템입니다.
기존 트레이딩 파트용으로 구축된 `LoadingScreenController`를 미연시 씬까지 확장하여 전역 호환성을 확보합니다.

> [!IMPORTANT]
> **[2026-08-12 변경]** 미연시 자유 채팅(로컬 LLM) 제거에 따라 `RequireLLM` 플래그와 모델 로딩 대기 분기가 삭제되었습니다.
> 이제 씬 전환 시 설정할 값은 `TargetSceneToLoad` 하나뿐입니다. → [제거 계획](../P2_03_LLM_Architecture/DatingSim_FreeChat_Removal_Plan.md)

## 핵심 기능
- `LoadingScreenController.cs` (Global Manager)
- **로딩 연출**: 기존 `LoadingScene`의 UI 프로그레스 바 하나로 모든 로딩 내역 표기.
- **진행률 산정**: 씬 비동기 로딩 진행률을 전체의 65%로 환산하고, 이후 대상 씬별 준비 대기(`GameScene`/`tutorial`은 차트 시스템 준비)를 거쳐 100%로 마무리합니다.
