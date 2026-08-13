# P2P Phase 0 기술 검증 기록

## 1. Phase 목표

Steam PC판 1~4인 P2P 구현을 시작하기 전에 프로젝트 환경을 확인하고, 네트워크 기술 조합과 설치 버전을 고정하며, 패키지 준비 상태를 반복 확인할 수 있는 진단 기반을 마련합니다.

검증 기준일: `2026-08-10`

---

## 2. 확인한 프로젝트 환경

| 항목 | 확인 결과 |
|---|---|
| Unity Editor | `6000.5.3f1` |
| 프로젝트 구조 | GameObject / MonoBehaviour 기반 |
| 주요 씬 | Title, Loading, Game, Tutorial |
| 대상 멀티 플랫폼 | Steam PC판 |
| 기존 네트워크 패키지 | Multiplayer Center만 설치됨 |
| NGO | 기존 미설치 |
| Steamworks.NET | 기존 미설치 |
| Steam Transport | 기존 미설치 |
| Unity Test Framework | `1.7.0` 설치됨 |
| 독립 테스트 어셈블리 | 기존 없음 |

기존 코드에는 CHALLENGE 모드의 AI 자동매매 잠금과 USER 수동매매 고정 로직이 이미 존재합니다. 다만 `GameManager`, `TradingController`와 `TraderStatus`가 한 명의 로컬 플레이어를 전제로 하므로 Phase 1에서 플레이어별 순수 상태 모델을 먼저 분리해야 합니다.

---

## 3. 확정 기술 스택

### Netcode for GameObjects 2.13.1

Unity가 제공하는 고수준 멀티플레이 프레임워크입니다. GameObject와 MonoBehaviour 기반 프로젝트에서 네트워크 객체, RPC, 접속자와 동기화 상태를 관리합니다.

이 프로젝트에서 담당할 역할:

- 호스트와 클라이언트 연결 상태
- 플레이어별 네트워크 식별자
- 주문 요청 RPC
- 시장, 경기 단계와 플레이어 상태 복제
- 연결 승인과 종료 콜백

선정 이유:

- 현재 프로젝트 구조와 동일한 GameObject·MonoBehaviour 방식
- Unity 6에서 공식 지원
- 전송 계층을 교체할 수 있어 Steam Networking과 조합 가능

### Steamworks.NET 2025.164.0

Valve의 C++ Steamworks SDK를 Unity C#에서 호출하게 해주는 오픈소스 래퍼입니다. Steam API와 거의 1:1로 대응하며 MIT 라이선스를 사용합니다.

이 프로젝트에서 담당할 역할:

- Steam 초기화
- Steam ID와 닉네임
- Steam Lobby 생성·검색·참가
- Steam 친구 초대와 Overlay
- Steam Networking API 접근

### Steam Networking Transport

Transport는 NGO가 만든 네트워크 메시지를 실제 네트워크로 운반하는 저수준 전송 계층입니다.

이 프로젝트에서 담당할 역할:

- Steam ID 기반 호스트·클라이언트 연결
- Reliable 및 Unreliable 패킷 전송
- Steam Datagram Relay 사용
- NAT 우회와 IP 보호

SteamNetworkingSockets Transport 1.0.1을 채택했습니다. Unity Technologies의 Multiplayer Community Contributions 저장소에서 제공되는 커뮤니티 패키지이며, Steamworks.NET의 Steam Networking Sockets API를 NGO `NetworkTransport`에 연결합니다. 공식 Unity 지원 제품은 아니므로 패키지 커밋을 잠그고 프로젝트에서 호환성 및 연결 종료 동작을 별도로 검증합니다.

---

## 4. 적용한 프로젝트 변경

### 패키지 선언

`Packages/manifest.json`에 다음 버전을 고정했습니다.

```text
com.unity.netcode.gameobjects: 2.13.1
com.rlabrecque.steamworks.net: 2025.164.0
```

버전을 고정한 이유는 개발 PC와 CI가 서로 다른 최신 버전을 자동으로 받아 네트워크 직렬화나 API가 달라지는 문제를 방지하기 위해서입니다.

### 기술 진단기

`P2PTechnologyProbe`를 추가했습니다.

기능:

- 현재 Unity 버전과 실행 플랫폼 출력
- 64비트 프로세스 확인
- NGO 어셈블리 로드 여부 확인
- Steamworks.NET 어셈블리 로드 여부 확인
- Steam Transport 어셈블리 로드 여부 확인
- 개발 빌드와 Editor에서 준비 상태 로그 출력

패키지 타입을 직접 참조하지 않고 어셈블리를 검사하므로 패키지 설치가 완료되기 전에도 기존 싱글플레이 스크립트를 깨지 않습니다.

---

## 5. 검증 결과

### 완료

- Unity 버전 확인
- 현재 패키지 및 씬 구성 확인
- 기존 멀티플레이 패키지 부재 확인
- NGO와 Steamworks.NET 버전 결정
- 패키지 Manifest 선언
- 패키지 독립형 기술 진단기 작성
- Steam 전용이며 Unity Lobby·Relay·Authentication을 사용하지 않는 구조 확정

### 환경 장애

첫 Unity 배치 실행에서는 Package Manager IPC 연결에 성공했지만 Unity Licensing Client가 Editor와 다음 오류로 Handshake하지 못했습니다.

```text
Unsupported protocol version '1.18.1'
```

Licensing Client 재시작 후 라이선스 연결과 패키지 다운로드는 정상화되었습니다.

이후 NGO 2.7.0이 Unity 6000.5에 내장된 Unity Transport 6.5.0의 `EntityId` API와 충돌하여 `CS0619` 컴파일 오류 12건이 발생했습니다. NGO 공식 최신 릴리스의 수정 내용을 확인한 뒤 해당 호환 처리가 포함된 2.13.1로 상향했습니다. 전체 컴파일 성공 여부는 Unity의 패키지 재해석 후 확정합니다.

NGO 2.13.1 재해석 후 이전 `CS0619` 오류가 모두 사라졌으며 Safe Mode 없이 프로젝트가 열렸습니다. `packages-lock.json`에도 NGO 2.13.1과 Steamworks.NET 2025.164.0이 고정됐고, 기술 진단 결과 두 핵심 패키지가 모두 `READY`로 확인됐습니다.

SteamNetworkingSockets Transport 설치 후 패키지 소스가 정상 임포트됐으며 Transport 어셈블리 DLL 생성과 전체 C# 컴파일이 성공했습니다. 패키지는 `packages-lock.json`의 Git 커밋 해시 `f5d80002708c530ad5b95b66f4c20751e0925123`으로 재현 가능하게 고정됐습니다.

개발용 Steam App ID 480으로 Editor Play Mode 런타임 검증을 수행했습니다. Steam 클라이언트의 네이티브 라이브러리 로드, `SteamAPI.Init`, 로컬 Steam 계정 조회, Steam Networking Sockets Relay 초기화가 모두 성공했습니다. Play Mode 종료 시 `SteamAPI.Shutdown`도 정상 호출됐으며 기술 진단기의 `Steam connection prototype ready` 결과가 `READY`로 확인됐습니다.

---

## 6. Phase 0 잔여 완료 조건

- [x] Unity Package Manager가 NGO 2.13.1 다운로드 완료
- [x] Unity Package Manager가 Steamworks.NET 2025.164.0 다운로드 완료
- [x] `packages-lock.json`에 두 패키지 버전 고정
- [x] Unity 전체 C# 컴파일 오류 없음
- [x] `P2PTechnologyProbe`에서 NGO와 Steamworks.NET이 `READY`
- [x] SteamNetworkingSockets Transport 선정 및 컴파일 확인
- [x] 개발용 Steam App ID 480 초기화 정책 적용
- [ ] 두 Steam 세션에서 최소 초기화 테스트

---

## 7. 다음 진행

라이선스 및 패키지 해석을 완료하면 Phase 0의 컴파일 검증을 마무리합니다. 이후 Phase 1에서 네트워크와 무관한 로컬 4인 상태 모델, 주문 검증기, 탈락 판정기와 자동 테스트를 구현합니다.
