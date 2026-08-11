# P2P Phase 2 Steam 초기화 계층 구현 기록

## 1. 구현 결과

Steamworks.NET 정적 API 호출과 게임에서 사용하는 상태 로직을 분리했습니다. 게임은 Steam 초기화 실패 시 종료되지 않으며 `CanUseP2P`만 `false`가 됩니다. 따라서 STORY 등 오프라인 기능은 계속 실행할 수 있습니다.

타이틀 씬에는 아직 P2P 메뉴 오브젝트가 없으므로 기존 씬 파일을 직접 변경하지 않았습니다. 이후 메뉴 UI는 `SteamRuntimeBootstrap.StatusChanged` 이벤트와 `CurrentStatus`를 사용해 버튼 활성 상태, Steam 닉네임, Steam ID와 실패 메시지를 표시할 수 있습니다.

## 2. 구현한 작업

### Steam 상태 계약

- `SteamRuntimeState`: 시작 전, 초기화 중, 준비, 실패, 종료 중과 종료 상태
- `SteamInitializationError`: 재실행 필요, 구조체 불일치, 네이티브 DLL 불일치, Steam 클라이언트·라이선스 오류, 잘못된 사용자와 예상하지 못한 예외
- `SteamRuntimeStatus`: 상태, 오류, 사용자 정보와 `CanUseP2P`를 묶은 불변 스냅샷
- `ISteamRuntimeService`: UI와 로비가 의존할 초기화 서비스 인터페이스
- `ISteamRuntimeBackend`: Steamworks.NET 호출을 교체 가능한 경계로 분리한 인터페이스

### Steam 런타임 서비스

- 중복 초기화 방지
- 초기화 단계별 오류 분류
- Steam ID가 0인 잘못된 사용자 방어
- 부분 초기화 뒤 실패할 경우 안전한 API 종료
- 준비 상태에서만 콜백 실행
- 중복 종료 방지와 종료 시 사용자 정보 제거
- 상태 변경 이벤트 발행

### Unity 부트스트랩과 Steam 어댑터

- `BeforeSceneLoad` 자동 생성과 `DontDestroyOnLoad` 유지
- 중복 인스턴스 제거
- Domain Reload 비활성화 시 정적 상태 초기화
- Editor에서는 `RestartAppIfNecessary`를 호출하지 않음
- 빌드에서는 Steam 재실행 요청 처리
- 매 프레임 Steam 콜백 펌프 실행
- 종료 및 오브젝트 파괴 경로의 안전한 Shutdown
- Steam 실패 시 경고만 남기고 게임 실행 유지

### 개발 App ID 관리

- 현재 개발 App ID는 Valve Spacewar `480`입니다.
- 저장소 루트의 `steam_appid.txt`는 Editor와 로컬 개발 실행용입니다.
- Unity 기본 빌드는 저장소 루트 파일을 Player에 자동 포함하지 않습니다.
- SteamPipe 배포 스크립트를 추가할 때 `steam_appid.txt`를 depot 파일 목록에서 명시적으로 제외해야 합니다.
- 정식 Steam App ID 발급 후 `DevelopmentAppId`와 로컬 파일 값을 교체해야 합니다.

## 3. 사용한 기술 스택

| 기술 | 이 작업에서 맡은 역할 | 기술 설명 |
|---|---|---|
| Unity 6.0 | 실행 생명주기, 자동 생성, 씬 유지 | 게임 오브젝트와 씬을 실행하는 게임 엔진입니다. `RuntimeInitializeOnLoadMethod`와 `DontDestroyOnLoad`로 씬 전환과 무관한 런타임 서비스를 유지합니다. |
| C# | 상태 모델, 인터페이스, 서비스 구현 | Unity의 주 개발 언어입니다. 인터페이스로 외부 SDK와 게임 규칙 사이의 결합을 낮췄습니다. |
| Steamworks.NET 2025.164.0 | Steam API, 사용자 ID·닉네임, 콜백, Relay 준비 | Valve Steamworks C++ SDK를 C#에서 호출할 수 있게 만든 래퍼입니다. |
| NUnit / Unity Test Framework | 초기화와 종료 상태 자동 테스트 | 실제 Steam 클라이언트 없이 가짜 백엔드를 주입해 성공과 실패 경로를 반복 검증합니다. |
| Assembly Definition | 순수 로직 격리 | `FXOverdose.P2P.Core`를 Unity 엔진과 Steam SDK에 의존하지 않는 별도 컴파일 단위로 유지합니다. |

NGO(Netcode for GameObjects)와 Steam Networking Sockets Transport는 설치되어 있지만, Phase 2에서는 연결을 열지 않습니다. 두 기술은 Phase 4에서 Steam 로비 참가자를 실제 호스트·클라이언트 세션으로 연결할 때 사용합니다.

## 4. 자동 테스트

`SteamRuntimeServiceTests`에 다음 8개 테스트를 추가했습니다.

1. 유효한 사용자 초기화 성공과 정보 저장
2. Steam 미실행·라이선스 실패 시 P2P만 비활성화
3. 잘못된 사용자에서 부분 초기화 API 정리
4. 준비 상태의 중복 초기화 방지
5. 준비 상태에서만 콜백 실행
6. 콜백 예외 발생 시 API 정리와 P2P 비활성화
7. 중복 Shutdown 방지와 사용자 정보 제거
8. Steam을 통한 재실행 필요 오류 분류

2026-08-11 배치 테스트 실행은 코드 컴파일 전에 Unity Licensing Client 프로토콜 불일치로 중단되었습니다.

```text
HandshakeResponse: Unsupported protocol version '1.18.1'
The connection with the Unity Licensing Client has been lost
```

따라서 Phase 1에서 통과한 16개와 Phase 2에서 추가한 8개, 총 24개 테스트는 Unity 라이선스 연결 복구 후 재실행해야 합니다. 이 기록은 테스트 성공으로 간주하지 않습니다.

별도로 Unity에 포함된 Roslyn C# 컴파일러로 `FXOverdose.P2P.Core` 전체 소스의 라이브러리 컴파일은 성공했습니다. 이는 순수 서비스 계층의 문법과 타입 연결을 확인한 결과이며, Unity 프로젝트 임포트와 Steamworks.NET 어댑터 검증을 대신하지는 않습니다.

## 5. 다음 작업

1. Unity Hub와 Editor를 완전히 종료한 뒤 Licensing Client 연결 복구
2. EditMode 총 24개 테스트 재실행
3. Play Mode에서 App ID 480, Steam ID와 닉네임 로그 확인
4. Phase 3에서 타이틀 P2P 메뉴와 Steam 상태 표시 UI 생성
5. Steam 로비 생성, 참가, 초대와 준비 상태 구현
