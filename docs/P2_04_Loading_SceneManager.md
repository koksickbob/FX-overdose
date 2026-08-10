# P2_04: 로딩 씬 매니저 아키텍처

## 개요
각 씬 전환과 무거운 데이터(LLM 등) 비동기 로딩을 통합 관리하는 시스템입니다.

## 핵심 기능
- `LoadingSceneManager.cs` (Singleton)
- **로딩 연출**: UI 프로그레스 바(`Slider.value`) 갱신
- **LLM 동기화 플래그**: `isLLMLoadingRequired`가 true일 때 LLM 준비 퍼센트와 화면의 로딩 바를 일치시키는 로직 포함.
