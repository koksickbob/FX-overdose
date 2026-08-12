# P2P Phase 4 NGO와 Steam Transport 구현 기록

## 구현 결과

Steam 로비 참가자를 NGO 호스트·클라이언트 세션으로 전환하는 연결 계층을 추가했습니다. 방장은 NGO Host와 Steam P2P Listen Socket을 열고, 참가자는 로비 방장의 Steam ID를 `ConnectToSteamID`로 설정해 접속합니다.

## 연결 순서

```text
Steam Lobby 전원 준비
→ 방장이 match_started와 connection_nonce 설정
→ 방장 StartHost / 참가자 StartClient
→ Steam Networking Sockets P2P 연결
→ NGO Connection Approval Payload 전송
→ Lobby·Build·Nonce·Steam ID 검증
→ NGO Client ID와 Steam ID 매핑
```

## 구현 기능

- 런타임 `NetworkManager`와 `SteamNetworkingSocketsTransport` 구성
- 로비 방장은 `StartHost()`, 참가자는 `StartClient()` 실행
- 참가자의 Transport 목적지를 방장 Steam ID로 설정
- Lobby ID, Steam ID, Build Version과 임의 Session Nonce 직렬화
- 잘못된 크기·형식 Payload 방어
- 다른 Lobby, Build와 Nonce 거절
- 로비 비참가 Steam ID 거절
- 중복 Steam ID 및 정원 초과 거절
- 경기 시작 전 연결 거절
- NGO Client ID와 Steam ID 매핑 및 조회
- 연결 해제 시 매핑 제거
- Transport 실패와 NGO 거절 사유 이벤트 제공
- Network Player Prefab 없이 연결하고 경기 플레이어 객체는 후속 Phase에서 권위 상태와 함께 생성

## 주요 파일

- `Assets/Scripts/P2P/Core/P2PConnectionApproval.cs`
- `Assets/Scripts/P2P/Infrastructure/P2PNetworkSessionManager.cs`
- `Assets/Tests/EditMode/P2PConnectionApprovalTests.cs`

## 사용 기술 스택

| 기술 | 역할 | 설명 |
|---|---|---|
| NGO 2.13.1 | 연결 승인, Client ID, 세션 생명주기 | Unity의 고수준 네트워크 프레임워크입니다. 이후 RPC와 NetworkVariable도 이 계층을 사용합니다. |
| Steam Networking Sockets Transport | NGO 패킷의 실제 전송 | IP 주소 대신 Steam ID로 P2P 연결하고 필요한 경우 Valve Relay를 사용합니다. |
| Steamworks.NET | Steam ID와 Relay API | Steam 클라이언트 및 네이티브 Networking Sockets API를 C#에 제공합니다. |
| Binary Payload | 연결 신원과 세션 정보 전달 | 고정 Magic과 버전을 포함해 손상되거나 다른 프로토콜인 데이터를 빠르게 거절합니다. |
| NUnit / Unity Test Framework | 승인 규칙 검증 | Steam 연결 없이 정상, 변조, 중복과 비회원 요청을 검사합니다. |

## 승인 거절 사유

`MalformedPayload`, `WrongLobby`, `WrongBuild`, `WrongNonce`, `InvalidSteamId`, `NotLobbyMember`, `DuplicateSteamId`, `LobbyFull`, `MatchNotStarted`를 구분합니다. NGO는 이 값을 클라이언트의 `DisconnectReason`으로 전달합니다.

## 검증 상태

- Unity 내장 Roslyn으로 Core 전체 컴파일 성공
- Payload 및 승인 규칙 테스트 6개 메서드, 10개 검증 시나리오 추가
- 사용자가 2026-08-11 두 Steam 클라이언트의 실제 연결 성공을 확인함
- Host와 Client의 로비 참가, 준비 및 NGO 연결 경로 검증 완료

## 다음 검증

1. Unity 포커스 복귀 후 최신 스크립트 임포트와 Console 확인
2. 전체 EditMode 테스트 실행
3. 두 Steam 계정으로 같은 Lobby 참가
4. 방장이 시작한 뒤 Host와 Client 시작 로그 확인
5. Host에서 NGO Client ID와 상대 Steam ID 매핑 확인
6. Build 또는 Nonce를 바꾼 개발 클라이언트가 거절되는지 확인

LoadingScene UI와 실제 Network Player Prefab은 아직 만들지 않았습니다. 타이틀 테스트 UI에는 현재 세션 이벤트를 연결했으며, Phase 5의 시장 동기화 객체를 첫 Network Prefab으로 등록하는 순서가 안전합니다.
