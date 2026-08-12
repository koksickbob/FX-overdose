# P2P Phase 5 공통 시장 및 시간 동기화 구현 기록

## 구현 결과

호스트에서만 가격과 시간을 계산하고 NGO 연결 클라이언트에 같은 스냅샷을 배포하는 `NetworkMarketAuthority`를 추가했습니다. 클라이언트는 시장을 계산하지 않고 호스트 스냅샷만 적용합니다.

## 구현 기능

- Match Nonce 기반 결정적 시장 Seed 생성
- 09:00 동시 시작 및 24:00 진행 종료
- 호스트 전용 가격·Bid·Ask·분봉 OHLCV 계산
- 0.2초 간격 최신 스냅샷 전송
- Sequence를 이용한 늦게 도착한 패킷 폐기
- 체크섬을 이용한 손상 스냅샷 거절
- 신규 클라이언트의 전체 스냅샷 요청 및 즉시 복구
- 호스트 일시정지 상태의 전원 동기화
- 타이틀 로비 UI에서 시간·가격·Sequence 실시간 표시
- Phase 6 주문 체결에서 사용할 `AuthoritativePrice` 제공

## 전송 구조

```text
Host P2PMarketSimulationEngine
  → P2PMarketSnapshotCodec
  → NGO Custom Named Message
  → Steam Networking Sockets Transport
  → Client P2PMarketReplica
```

최신 상태가 중요한 시장 틱은 `UnreliableSequenced`로 보냅니다. 뒤늦게 참가하거나 재동기화를 요청한 클라이언트에는 `ReliableSequenced`로 전체 최신 스냅샷을 보냅니다.

## 주요 파일

- `Assets/Scripts/P2P/Core/P2PMarketSynchronization.cs`
- `Assets/Scripts/P2P/Infrastructure/NetworkMarketAuthority.cs`
- `Assets/Tests/EditMode/P2PMarketSynchronizationTests.cs`

## 사용 기술 스택

| 기술 | 역할 | 설명 |
|---|---|---|
| NGO Custom Messaging | 스냅샷 배포와 재동기화 요청 | Network Prefab 없이 연결된 Client ID에 이름 있는 바이너리 메시지를 보냅니다. |
| Steam Networking Sockets | 실제 패킷 전송 | NGO 메시지를 Steam P2P 또는 Valve Relay 경로로 운반합니다. |
| 결정적 PRNG | 호스트 시장 가격 생성 | 같은 Seed에서 재현 가능한 가격 순서를 생성하지만 실제 권위 계산은 호스트만 실행합니다. |
| Sequence·Checksum | 순서 및 데이터 무결성 | 오래된 패킷과 손상된 스냅샷을 클라이언트 상태에 적용하지 않습니다. |
| NUnit | 시장 시간과 복구 규칙 테스트 | 같은 Seed, 정지, 24:00 종료, 늦은 패킷과 손상 데이터를 검증합니다. |

## 자동 테스트

- 같은 Seed와 경과 시간에서 같은 시장 생성
- 09:00 시작 및 24:00 종료
- 일시정지 중 시간·가격 정지
- 늦게 도착한 Sequence 폐기
- 스냅샷 직렬화 왕복 및 손상 검출

Core 전체 소스는 Unity 내장 Roslyn 컴파일에 성공했습니다. Unity Editor가 최신 Infrastructure 변경을 임포트한 후 전체 Test Runner와 두 클라이언트 장시간 일치 검증이 필요합니다.

## 실행 검증

두 클라이언트가 경기 시작 후 특정 로비 화면 하단에서 다음 값이 같은지 비교합니다.

```text
NGO 동기화 | 09:xx | 가격 | #Sequence
```

15분 동안 Host와 Client의 시간, 가격과 Sequence가 계속 일치하고 24:00에 함께 멈추면 Phase 5 완료 조건을 충족합니다.
