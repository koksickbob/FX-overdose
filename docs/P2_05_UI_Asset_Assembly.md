# P2_05: UI 담당자 전용 가이드 및 조립 문서

## 개요
프로그래머가 작성한 매니저 로직과 UI 프리팹을 연결하고 에셋을 세팅하기 위한 가이드입니다.

## 씬별 조립 가이드
1. **요미의 방 (YomiRoomScene)**
   - 필요 UI: 행동 선택 패널(월드맵, 대화, 휴식, 트레이딩), 상태창(호감도, 시간)
   - 바인딩 규약: `YomiRoomUIController`의 Inspector에 각 버튼 할당.

2. **월드맵 (WorldMapScene)**
   - 필요 UI: 맵 네비게이터, 소지 자금 패널
   - 바인딩 규약: 각 코스 진입 버튼에 `LoadScene` 이벤트 연결.

3. **공통 로딩 (LoadingScene)**
   - 필요 UI: 배경 일러스트 이미지, 프로그레스 바(Slider), 팁 텍스트
   - 바인딩 규약: `LoadingSceneManager` 스크립트에 캔버스 구성요소 할당.
