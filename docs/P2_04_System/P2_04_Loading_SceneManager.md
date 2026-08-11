# P2_04: 로딩 씬 매니저 아키텍처

## 개요
각 씬 전환과 무거운 데이터(로컬 LLM) 비동기 로딩을 통합 관리하는 시스템입니다.
기존 트레이딩 파트용으로 구축된 `LoadingScreenController`를 미연시 씬까지 확장하여 전역 호환성을 확보합니다.

## 핵심 기능
- `LoadingScreenController.cs` (Global Manager)
- **로딩 연출**: 기존 `LoadingScene`의 UI 프로그레스 바 하나로 모든 로딩 내역 표기.
- **LLM 동기화 플래그**: `RequireLLM` 플래그가 true일 때, 트레이딩 차트 준비를 기다리는 대신 미연시 씬의 `DatingSimLLMController`를 찾습니다.
- **Qwen2.5-7B 로드 시뮬레이션**: 씬 비동기 로딩 진행률 50% + LLM 초기화(`IsLLMReady`) 진행률 50%를 덧셈하여, 화면 멈춤 없이 100%까지 부드럽게 UI를 갱신합니다.
