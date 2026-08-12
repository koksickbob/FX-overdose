# P2P Phase 3 Steam 로비 구현 기록

## 구현 결과

Steamworks.NET Lobby API를 사용하는 `SteamLobbyManager`와 SDK 비종속 로비 규칙 모델을 추가했습니다. 실제 검증을 위해 타이틀 씬에 런타임 생성되는 `P2P MULTI` 로비 UI도 추가했습니다.

## 구현 기능

- 공개 검색 로비와 초대 전용 친구 로비 생성
- 게임·모드·빌드 버전 필터를 적용한 공개 로비 검색
- Lobby ID 직접 참가와 Steam 초대 수락 참가
- Steam 친구 초대 Overlay
- 참가자 Steam ID, 닉네임, 방장과 준비 상태 스냅샷
- 최대 레버리지 `1/2/5/10/20/50/100` 프리셋
- 최대 마진 비율 `10/25/50/75/100%` 프리셋
- 방장만 인원·레버리지·마진 설정 변경
- 규칙 변경 시 `rules_revision`을 증가시켜 기존 준비 상태 무효화
- 방장과 현재 규칙에 준비한 전원이 있어야 경기 시작 가능
- 경기 시작 시 로비 참가 잠금
- 다른 게임, 다른 모드, 다른 빌드와 이미 시작한 로비 참가 거절
- 로비 퇴장과 Steam 연결 해제 시 상태 정리
- 타이틀 화면의 `P2P MULTI` 버튼 및 로비 조작 패널
- Steam 상태, 로비 오류와 NGO 연결 상태 표시
- `P2P MULTI → 전체 로비 → 특정 로비`의 3단계 화면 흐름
- 전체 로비에서는 공개 목록·새로고침·공개/친구 로비 생성만 제공
- 특정 로비에서는 참가자·설정·초대·준비·시작·나가기만 제공

## 데이터 구조

### Lobby Data

| 키 | 내용 |
|---|---|
| `game` | `fx_overdose` 고정 식별자 |
| `mode` | `p2p_challenge` 고정 모드 |
| `build` | `Application.version` |
| `max_leverage` | 방장이 정한 최대 레버리지 |
| `max_margin` | 방장이 정한 최대 마진 비율 |
| `rules_revision` | 규칙 변경 횟수 |
| `match_started` | 경기 시작 및 참가 잠금 여부 |
| `member_count` | 검색 화면 표시용 현재 인원 |

### Lobby Member Data

| 키 | 내용 |
|---|---|
| `ready` | 플레이어의 준비 여부 |
| `ready_revision` | 어떤 규칙 버전에 준비했는지 표시 |

Steam에서는 다른 플레이어의 Member Data를 방장이 직접 수정할 수 없습니다. 따라서 규칙 변경 때 `rules_revision`을 올리고 이전 revision의 준비 응답을 무효로 처리합니다.

## 주요 파일

- `Assets/Scripts/P2P/Core/SteamLobbyModels.cs`
- `Assets/Scripts/P2P/Infrastructure/SteamLobbyManager.cs`
- `Assets/Tests/EditMode/SteamLobbyRulesTests.cs`
- `Assets/Scripts/P2P/UI/P2PLobbyUIController.cs`

## 사용한 기술 스택

| 기술 | 역할 | 설명 |
|---|---|---|
| Steamworks.NET Matchmaking | 로비 생성·검색·참가·초대·메타데이터 | Valve Steam Lobby API를 C#에서 호출하는 계층입니다. |
| Steam Callback | 비동기 생성 결과, 참가, 데이터 및 인원 변경 | `SteamAPI.RunCallbacks()`가 Steam 이벤트를 Unity 메인 스레드에 전달합니다. |
| 순수 C# 모델 | 설정 검증과 시작 가능 판정 | Steam SDK 없이 로비 규칙을 자동 테스트할 수 있게 합니다. |
| NUnit / Unity Test Framework | 프리셋, 권한, revision 준비 상태 검증 | 네트워크 없이 결정 규칙을 반복 검사합니다. |

NGO와 Steam Transport는 아직 연결하지 않았습니다. Phase 3의 Lobby는 플레이어를 모으고 설정을 공유하는 대기실이며 실제 게임 패킷은 Phase 4에서 시작합니다.

## 검증 상태

- Unity 내장 Roslyn으로 `FXOverdose.P2P.Core` 전체 컴파일 성공
- 로비 규칙 테스트 10개 케이스 추가
- 열려 있는 Unity Editor가 아직 새 스크립트를 임포트하지 않아 전체 Unity 테스트는 미실행
- 실제 로비 생성·초대·참가는 서로 다른 Steam 계정 2개 이상으로 검증 필요

## 다음 검증 절차

1. Unity Editor로 돌아가 Asset Refresh와 컴파일 완료 확인
2. Test Runner에서 전체 EditMode 테스트 실행
3. 첫 계정으로 친구 전용 로비 생성
4. 다른 Steam 계정을 초대해 참가자와 준비 상태 확인
5. 방장이 규칙을 변경했을 때 클라이언트 준비가 해제되는지 확인
6. 전원 준비 후 시작하면 신규 참가가 차단되는지 확인
