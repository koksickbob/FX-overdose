# Phase 2: 미연시 파트 통합 마스터 플랜

## 개요
트레이딩 파트와 완벽히 분리된 [요미의 방 - 월드맵 - 데이트/알바]로 이어지는 신규 코어 루프의 통합 문서입니다.

## 진행률 (Checklist)
- [ ] P2_01: 요미의 방 아키텍처 및 행동 선택 로직 구현
- [ ] P2_02: 월드맵 및 알바 미니게임 경제 밸런싱 구현
- [ ] P2_03: LLM 2-Layer 감정-호감도 분리 시스템 구현
- [ ] P2_04: 비동기 데이터/LLM 씬 전환 및 로딩 연출 매니저 구현
- [ ] P2_05: UI/에셋 프리팹 조립 및 스크립트 바인딩 완료

## 전역 매니저 구조 (Data Flow)
* **Data Layer**: `DatingSimStatus` (체력, 호감도, 시간 보관)
* **Logic Layer**: `DatingTimeManager`, `DatingLLMManager` (UI 참조 없음)
* **View Layer**: 각 씬 전용 UI Controller (매니저 이벤트 구독 방식)
