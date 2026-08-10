# FX Overdose Steam P2P 기능명세서

## 1. 문서 목적

본 문서는 `FX Overdose`의 CHALLENGE 모드를 기반으로 한 Steam PC판 1~4인 경쟁 멀티플레이의 기술 스택, 런타임 구조, 데이터 모델, 네트워크 메시지, 기능별 동작과 예외 처리 기준을 정의합니다.

상위 게임 규칙은 [P2P 멀티플레이 기획서 초안](P2P_멀티플레이_기획서_초안.md)을 따릅니다.

---

## 2. 확정 범위

| 항목 | 명세 |
|---|---|
| 플랫폼 | Steam PC판 |
| 참가 인원 | 1~4인 |
| 경기 방식 | 호스트 권위형 Listen Server |
| 기반 모드 | CHALLENGE |
| 조작 | USER 수동매매 고정 |
| 경기 시간 | 인게임 09:00~24:00 |
| 승리 조건 | 24:00 최종 수익률 1위 |
| 탈락 조건 | 총자산 0 이하 또는 멘탈 0 이하 |
| 로비 설정 | 최대 레버리지, 최대 마진 비율 |
| 아이템 | 체력·멘탈 케어 아이템 허용 |
| 금지 기능 | AI 자동매매, 시간 가속 아이템, 스킬 업그레이드 |
| 돌발 이벤트 | 전원 동시 발생, 현실 시간 15초 선택 |

---

## 3. 기술 스택

### 3.1. 기본 기술

| 영역 | 기술 | 역할 |
|---|---|---|
| 게임 엔진 | Unity 6 / C# | 클라이언트 및 게임 로직 |
| 플랫폼 API | Steamworks SDK | Steam 사용자, 로비, 친구 초대 및 네트워킹 기반 |
| Unity 바인딩 | Steamworks.NET | C#에서 Steamworks API 접근 |
| 매치 로비 | Steam Matchmaking & Lobbies | 공개·친구·비공개 로비 생성 및 참가 |
| 게임 동기화 | Netcode for GameObjects | 접속자 관리, RPC와 권위 상태 복제 |
| 전송 계층 | Steam Networking 기반 NGO Transport | Steam ID 기반 P2P 패킷 송수신 |
| 패킷 경로 | Steam Datagram Relay | NAT 우회 및 사용자 IP 보호 |
| 직렬화 | NGO 직렬화 + `INetworkSerializable` | 경기 상태와 요청 메시지 전송 |
| 비동기 처리 | C# `async/await`, Steam Callback/CallResult | 로비 생성, 검색, 참가 처리 |
| 로컬 테스트 | Unity Multiplayer Play Mode 또는 복수 빌드 | 다중 클라이언트 검증 |
| 버전 관리 | Git + Unity `.meta` | 패키지, 프리팹과 코드 변경 관리 |

### 3.2. 패키지 후보

```text
com.unity.netcode.gameobjects
Steamworks.NET
Steam Networking용 NGO Transport 어댑터
```

Steam Transport는 프로젝트 도입 전에 아래 항목을 검증해야 합니다.

- 현재 Unity 및 NGO 버전 호환성
- 최신 Steam Networking Sockets 또는 Messages 사용 여부
- Windows x64 네이티브 라이브러리 포함 여부
- Steam Datagram Relay 지원 여부
- 호스트와 클라이언트의 Steam ID 매핑 방식
- 유지보수 상태와 라이선스

호환되는 Transport를 채택하기 어렵다면 `NetworkTransport` 추상화를 구현한 프로젝트 전용 `SteamP2PTransport`를 작성합니다.

### 3.3. 사용하지 않는 서비스

Steam PC판 전용 MVP에서는 다음 서비스를 중복 사용하지 않습니다.

- Unity Lobby
- Unity Relay
- Unity Authentication
- Unity Matchmaker

Steam Lobby가 매치 구성과 인증된 Steam 사용자 식별을 담당하고, Steam Networking이 실제 경기 패킷을 전달합니다.

---

## 4. 전체 아키텍처

```text
[Steam Client]
      │
      ▼
[SteamBootstrap]
Steam API 초기화, 콜백, 사용자 정보
      │
      ▼
[SteamLobbyManager] ───── 친구 초대 / 공개방 검색
      │
      ▼
[NetworkManager + SteamP2PTransport]
      │
      ▼
[P2PMatchManager / Host Authority]
      ├── NetworkMarketAuthority
      ├── NetworkTradingAuthority
      ├── P2PInventoryAuthority
      ├── P2PChoiceEventController
      ├── MultiplayerLeaderboard
      └── MultiplayerPlayerState[1..4]
```

### 4.1. 권한 원칙

- 로비 소유자가 경기 호스트가 됩니다.
- 호스트만 공통 시간과 시장 가격을 확정합니다.
- 클라이언트는 결과값이 아니라 입력 요청만 보냅니다.
- 잔고, 손익, 체력, 멘탈, 인벤토리와 탈락 여부는 호스트가 계산합니다.
- 클라이언트가 보낸 자산이나 회복 결과는 신뢰하지 않습니다.
- 화면 보간을 제외한 승패 판정은 모두 호스트 상태를 기준으로 합니다.

---

## 5. 씬 및 오브젝트 구성

### 5.1. 권장 씬 흐름

```text
TitleScene
  → P2PLobbyScene
  → LoadingScene
  → GameScene(P2P Match)
  → P2PResultScene 또는 결과 오버레이
  → P2PLobbyScene
```

### 5.2. 영구 오브젝트

| 오브젝트 | 생명주기 | 역할 |
|---|---|---|
| `SteamBootstrap` | 앱 종료까지 | Steam API 및 콜백 유지 |
| `NetworkManager` | 로비~경기 종료 | NGO 연결 유지 |
| `SteamLobbyManager` | 로비 종료까지 | Steam Lobby 상태 유지 |
| `P2PMatchManager` | 경기 단위 | 경기 상태와 종료 관리 |

`DontDestroyOnLoad` 중복 인스턴스가 생기지 않도록 각 시스템은 단일 진입점에서 생성합니다.

---

## 6. 핵심 상태 모델

### 6.1. 경기 상태

```csharp
public enum P2PMatchPhase : byte
{
    None,
    Lobby,
    Loading,
    Countdown,
    Playing,
    ChoiceEvent,
    Settlement,
    Finished,
    Aborted
}
```

```csharp
public struct P2PMatchRules : INetworkSerializable
{
    public int MaxPlayers;
    public float StartingBalance;
    public int MaxLeverage;
    public float MaxMarginRatio;
    public int StartHour;
    public int EndHour;
    public float SecondsPerGameMinute;
    public bool EnableChoiceEvents;
    public int ChoiceTimeoutSeconds;
}
```

### 6.2. 플레이어 상태

```csharp
public enum P2PEliminationReason : byte
{
    None,
    Bankruptcy,
    MentalOverdose,
    DisconnectForfeit
}
```

```csharp
public struct MultiplayerPlayerState : INetworkSerializable
{
    public ulong ClientId;
    public ulong SteamId;
    public FixedString64Bytes DisplayName;

    public float CashBalance;
    public float TotalEquity;
    public float Health;
    public float Mental;

    public byte PositionType;
    public float EntryPrice;
    public float MarginAmount;
    public int Leverage;
    public float UnrealizedPnL;

    public bool IsReady;
    public bool IsConnected;
    public bool IsEliminated;
    public P2PEliminationReason EliminationReason;
    public double EliminationServerTime;
}
```

### 6.3. 시장 상태

```csharp
public struct NetworkMarketSnapshot : INetworkSerializable
{
    public uint Sequence;
    public int MatchSeed;
    public int Day;
    public int Hour;
    public int Minute;
    public float Price;
    public byte Regime;
    public uint Checksum;
    public double ServerTime;
}
```

---

## 7. Steam 초기화 명세

### 7.1. `SteamBootstrap`

책임:

- `SteamAPI_RestartAppIfNecessary` 처리
- `SteamAPI_Init` 성공 여부 확인
- 매 프레임 Steam 콜백 실행
- 로컬 Steam ID와 닉네임 제공
- 앱 종료 시 `SteamAPI_Shutdown` 호출
- Steam 미실행 또는 초기화 실패 시 P2P 메뉴 비활성화

공개 API 후보:

```csharp
public bool IsInitialized { get; }
public ulong LocalSteamId { get; }
public string PersonaName { get; }
public event Action OnSteamInitialized;
public event Action<string> OnSteamInitializationFailed;
```

개발 빌드에서는 `steam_appid.txt`를 사용할 수 있지만 배포 Depot에는 포함하지 않습니다.

---

## 8. Steam 로비 기능 명세

### 8.1. 로비 생성

```csharp
public Task<SteamLobbyInfo> CreateLobbyAsync(P2PMatchRules rules, LobbyVisibility visibility)
```

호스트가 설정할 Lobby Data:

| 키 | 예시 |
|---|---|
| `build_version` | `0.8.0` |
| `mode` | `p2p_challenge` |
| `state` | `lobby` |
| `max_players` | `4` |
| `max_leverage` | `50` |
| `max_margin` | `0.50` |
| `choice_timeout` | `15` |
| `joinable` | `1` |

### 8.2. 로비 검색

- 빌드 버전이 같은 로비만 표시합니다.
- `p2p_challenge` 모드만 검색합니다.
- 빈 슬롯이 있는 로비만 표시합니다.
- 공개방, 친구방과 초대 참가를 지원합니다.
- 경기 시작 후 `joinable=0`으로 변경합니다.

### 8.3. 로비 참가

참가 순서:

1. Steam Lobby 참가
2. 로비 데이터 및 버전 검증
3. 로비 소유자의 Steam ID 확인
4. NGO 클라이언트 연결 시작
5. Steam ID와 NGO Client ID 연결 승인
6. 로비 UI에 참가자 카드 생성

### 8.4. 준비 및 규칙 변경

- 방장만 최대 레버리지와 최대 마진 비율을 변경할 수 있습니다.
- 규칙 변경 시 모든 비호스트 플레이어의 준비 상태를 해제합니다.
- 모든 연결된 플레이어가 준비해야 시작할 수 있습니다.
- 방장은 준비 여부와 무관하게 시작 버튼을 누르기 전에 최종 검증을 통과해야 합니다.

---

## 9. 연결 승인 및 버전 검증

NGO Connection Approval Payload 후보:

```csharp
public struct P2PConnectionPayload
{
    public ulong SteamId;
    public string BuildVersion;
    public ulong LobbyId;
    public string Nonce;
}
```

거절 조건:

- Steam 초기화 실패
- Steam Lobby에 없는 사용자
- 로비 정원 초과
- 빌드 버전 불일치
- 경기가 이미 시작됨
- 중복 Steam ID
- 잘못된 Lobby ID 또는 Nonce

---

## 10. 공통 시장 동기화

### 10.1. 시장 생성

- 호스트가 경기 시작 시 `MatchSeed`를 생성합니다.
- 호스트의 `MarketSimulationEngine`만 권위 시장을 실행합니다.
- 클라이언트는 호스트의 가격과 캔들 데이터를 표시합니다.
- 시장 시드만 공유해 각자 완전 독립 계산하는 방식은 부동소수점 및 프레임 차이 위험 때문에 최종 판정에 사용하지 않습니다.

### 10.2. 전송 주기

| 데이터 | 권장 주기 | 채널 |
|---|---|---|
| 가격 틱 | 시장 틱마다 | Unreliable Sequenced |
| 캔들 확정 | 캔들 종료 시 | Reliable |
| 인게임 시간 | 1분 경과 시 | Reliable |
| 시장 전체 스냅샷 | 2~5초마다 | Reliable |
| 체크섬 | 스냅샷과 함께 | Reliable |

### 10.3. 불일치 교정

- 오래된 `Sequence` 패킷은 폐기합니다.
- 체크섬이 다르면 최신 전체 스냅샷을 요청합니다.
- 클라이언트의 차트는 교정값으로 부드럽게 보간하되 주문 체결에는 호스트 가격만 사용합니다.

---

## 11. 매매 기능 명세

### 11.1. 주문 요청

```csharp
public struct TradeRequest : INetworkSerializable
{
    public uint RequestId;
    public byte Action;
    public byte PositionType;
    public int Leverage;
    public float MarginRatio;
    public uint LastKnownMarketSequence;
}
```

액션:

- `OpenLong`
- `OpenShort`
- `ClosePosition`

### 11.2. 호스트 검증

호스트는 다음 순서로 주문을 검증합니다.

1. 경기 단계가 `Playing`인지 확인
2. 플레이어가 연결 및 생존 상태인지 확인
3. 돌발 이벤트 선택 화면이 아닌지 확인
4. 중복 `RequestId`인지 확인
5. 현재 포지션과 액션이 호환되는지 확인
6. 레버리지가 `1~MaxLeverage`인지 확인
7. 마진 비율이 허용 범위 이하인지 확인
8. 사용 가능한 현금과 최소 주문 금액 확인
9. 호스트 현재가로 체결
10. 변경 상태와 결과 코드 전송

### 11.3. 주문 응답

```csharp
public enum TradeRejectReason : byte
{
    None,
    MatchNotPlaying,
    PlayerEliminated,
    ChoiceEventActive,
    DuplicateRequest,
    PositionConflict,
    InvalidLeverage,
    InvalidMargin,
    InsufficientBalance
}
```

클라이언트는 요청 중 버튼을 잠시 잠그고, 승인 또는 거절을 받으면 다시 갱신합니다.

---

## 12. 체력·멘탈 및 탈락 명세

### 12.1. 상태 계산

- 체력과 멘탈 변화는 호스트에서 계산합니다.
- 자연 감소는 공통 인게임 시간에 맞춰 동일한 기준을 사용합니다.
- 포지션 진입, 손절, 청산과 돌발 이벤트의 멘탈 변화도 호스트가 적용합니다.
- AI의 일반 대사와 연출은 유지할 수 있지만 AI 매매 호출은 차단합니다.

### 12.2. 탈락 조건

```text
TotalEquity <= 0  → Bankruptcy
Mental <= 0       → MentalOverdose
```

- 두 조건 중 하나가 먼저 충족되는 즉시 탈락합니다.
- 싱글플레이의 오버도즈 강제매매와 35초 보호는 적용하지 않습니다.
- 호스트가 탈락을 확정한 프레임부터 모든 신규 요청을 거절합니다.
- 열린 포지션은 호스트 현재가로 종료하고 결과 기록을 확정합니다.
- 탈락 플레이어는 관전 상태로 남습니다.

### 12.3. 동시 탈락

- 호스트의 동일 시장 `Sequence`에서 발생하면 동시 탈락으로 기록합니다.
- 생존 시간이 같다면 탈락 판정 직전 총자산이 높은 플레이어를 상위에 둡니다.

---

## 13. 아이템 및 상점 명세

### 13.1. 허용 기능

- 에너지 드링크
- 디저트
- 영양제
- 진정제
- 시간 변경 효과가 없는 즉시 회복형 배달 음식
- 경기 자산을 사용하는 아이템 구매
- 보유 수량 내 아이템 사용

### 13.2. 금지 기능

- 파스타 및 모든 시간 가속·감속 아이템
- `DynamicTimeRegulator` 변경 효과
- 액티브 장비와 영구 패시브
- 싱글 세이브 인벤토리 반입
- 스킬 업그레이드
- 스킬 업그레이드의 시간 경과

### 13.3. 아이템 요청

```csharp
public struct ItemActionRequest : INetworkSerializable
{
    public uint RequestId;
    public FixedString64Bytes ItemId;
    public byte Action; // Buy or Use
}
```

호스트 검증 항목:

- 허용 아이템 ID인지 확인
- 플레이어가 생존 상태인지 확인
- 구매 자산이 충분한지 확인
- 사용 수량이 있는지 확인
- 회복 가능한 상태인지 확인
- 시간 변경 또는 금지 효과가 포함되지 않았는지 확인

아이템 데이터는 클라이언트 ScriptableObject를 참고하되, 가격과 효과의 최종 권위값은 호스트의 P2P 허용 목록에서 가져옵니다.

---

## 14. 돌발 선택 이벤트 명세

### 14.1. 발생

- 호스트만 이벤트 발생 시점을 결정합니다.
- 이벤트는 모든 생존 플레이어에게 동시에 표시합니다.
- 이벤트 진입과 동시에 공통 시장 시간과 가격 갱신을 정지합니다.
- 선택 제한은 현실 시간 `15초`입니다.

### 14.2. 이벤트 상태

```csharp
public struct NetworkChoiceEventState : INetworkSerializable
{
    public uint EventSequence;
    public FixedString64Bytes EventId;
    public double DeadlineServerTime;
    public int CommonOutcomeSeed;
    public bool IsResolving;
}
```

플레이어별 선택 상태:

```csharp
public struct PlayerChoiceState : INetworkSerializable
{
    public ulong ClientId;
    public int SelectedOptionIndex;
    public bool HasSubmitted;
    public bool WasRandomlyAssigned;
}
```

### 14.3. 선택 처리

1. 클라이언트가 옵션 인덱스를 전송합니다.
2. 호스트가 옵션 범위와 요구 아이템을 검증합니다.
3. 유효한 선택이면 잠그고 변경을 금지합니다.
4. 모든 생존자가 선택하면 즉시 결과를 처리합니다.
5. 15초가 지나면 미선택자에게 유효 옵션을 무작위 배정합니다.

결정적 무작위 키:

```text
Hash(MatchSeed, EventSequence, PlayerSteamId)
```

요구 아이템이 없거나 실제 보유한 선택지만 무작위 후보에 포함합니다.

### 14.4. 결과 적용

- 시장 방향과 가격 빔은 전원에게 하나의 공통 결과를 적용합니다.
- 포지션, 아이템 소모, 체력과 멘탈 효과는 플레이어별 선택에 따라 적용합니다.
- 결과로 멘탈 또는 총자산이 0이 되면 즉시 탈락 판정을 수행합니다.
- 모든 결과 스냅샷 전송이 끝난 뒤 시장 시간을 재개합니다.

기존 싱글 이벤트는 선택지마다 시장을 서로 다르게 변경할 수 있으므로 P2P 전용 이벤트 데이터 또는 변환 계층이 필요합니다.

---

## 15. 순위 및 경기 종료

### 15.1. 실시간 순위

생존 플레이어는 현재 수익률 내림차순으로 정렬합니다.

```text
CurrentReturn = (TotalEquity - StartingBalance) / StartingBalance × 100
```

탈락 플레이어는 생존 플레이어 아래에 두고 생존 시간으로 정렬합니다.

### 15.2. 24:00 처리

1. 신규 주문 차단
2. 모든 생존 플레이어의 열린 포지션 강제 종료
3. 최종 자산 및 통계 확정
4. 최종 수익률 계산
5. 동률 규칙 적용
6. 결과 스냅샷 전송
7. `P2PMatchPhase.Finished` 전환

### 15.3. 조기 종료

- 생존자가 한 명만 남으면 즉시 승리 처리할지 24:00까지 계속할지는 로비 규칙으로 두지 않고 MVP 정책으로 고정합니다.
- MVP 권장안은 마지막 생존자가 나오면 즉시 경기를 종료하는 것입니다.
- 1인 연습방은 24:00까지 계속합니다.

---

## 16. UI 기능 명세

### 16.1. 로비

- Steam 닉네임과 호스트 표시
- 최대 4인 참가자 카드
- 준비 상태
- 최대 레버리지 선택
- 최대 마진 비율 선택
- 친구 초대
- 공개·친구·비공개 상태
- 연결 품질
- 버전 불일치 및 참가 실패 메시지

### 16.2. 경기 HUD

- 내 자산, 수익률, 체력과 멘탈
- 참가자별 순위, 자산, 수익률과 생존 상태
- 상대 포지션 방향 공개 여부는 추후 확정
- 공통 시간
- 최대 레버리지와 최대 마진 규칙
- 접속 끊김 및 재접속 표시
- 전체 이벤트 피드

### 16.3. 돌발 이벤트 UI

- 이벤트 제목과 본문
- 선택지 버튼
- 요구 아이템 및 보유 수량
- `15.0`초 카운트다운
- 다른 플레이어의 선택 완료 여부
- 결과 공개 전 선택 내용 비공개
- 시간 초과 무작위 선택 표시

### 16.4. 탈락 및 관전

- `BANKRUPTCY` 또는 `MENTAL OVERDOSE` 표시
- 매매와 아이템 입력 차단
- 남은 플레이어 순위와 공통 차트 관전
- 로비 나가기 버튼

---

## 17. 연결 종료 및 호스트 이탈

### 17.1. 클라이언트 연결 종료

- 짧은 재접속 유예 시간을 둡니다.
- 유예 중 포지션 손익과 자연 감소는 계속 계산합니다.
- 신규 주문과 아이템 사용은 차단합니다.
- 유예 종료 시 `DisconnectForfeit` 처리하고 포지션을 종료합니다.

### 17.2. 호스트 이탈

MVP에서는 안전한 호스트 이전보다 명시적인 경기 무효 처리를 우선합니다.

- 로비 단계: Steam Lobby 소유자 이전 후 계속 가능
- 경기 단계: 호스트 이탈 시 경기 중단 및 무효 처리
- 이후 호스트 마이그레이션은 별도 페이즈에서 구현합니다.

---

## 18. 보안 및 부정행위 방지

- 모든 자산·손익·체력·멘탈 계산은 호스트 수행
- 주문 빈도 제한
- 중복 Request ID 차단
- 허용 범위 밖 레버리지·마진 거절
- 금지 아이템 ID 거절
- 이벤트 마감 이후 선택 거절
- Lobby Steam ID와 연결 Payload 일치 확인
- 빌드 버전 불일치 차단
- 비정상 패킷 반복 시 연결 종료
- 경기 결과에 시장 시드, 주요 요청과 최종 체크섬 기록

호스트 자체의 조작을 완전히 방지하려면 전용 서버가 필요합니다. MVP의 친구·소규모 경쟁에서는 호스트 권위형으로 시작하고, 랭크전 도입 시 전용 서버를 재검토합니다.

---

## 19. 로그 및 진단

경기별 최소 로그:

- Match ID와 Lobby ID
- Build Version
- Match Seed
- 참가 Steam ID
- 로비 규칙
- 시장 Sequence와 체크섬 오류
- 주문 승인·거절
- 아이템 구매·사용
- 돌발 이벤트 선택과 무작위 배정
- 탈락 시각과 사유
- 최종 순위 및 결과 체크섬

개인정보나 Steam 인증 토큰은 로그에 기록하지 않습니다.

---

## 20. 성능 목표

| 항목 | 목표 |
|---|---|
| 최대 참가자 | 4명 |
| 네트워크 전송 | 시장 틱 및 상태 변화 중심 |
| 호스트 프레임 | 기존 싱글 대비 유의미한 저하 없음 |
| 주문 응답 | 정상 네트워크에서 즉각적인 체감 |
| 전체 스냅샷 | 2~5초 간격 |
| 이벤트 타이머 | 서버 시간 기준 오차 최소화 |
| GC 할당 | 반복 틱과 패킷 처리에서 최소화 |

---

## 21. 완료 기준

- Steam 초기화와 종료가 안정적으로 동작합니다.
- Steam 공개·친구·비공개 로비를 생성하고 참가할 수 있습니다.
- 1~4명의 Steam ID가 NGO Client ID와 정확히 연결됩니다.
- 모든 참가자에게 동일한 시장과 시간이 표시됩니다.
- 호스트가 주문, 자산, 체력, 멘탈과 아이템을 권위 처리합니다.
- 로비의 최대 레버리지와 최대 마진 규칙을 우회할 수 없습니다.
- 케어 아이템은 사용할 수 있고 시간 변경 아이템과 스킬 업그레이드는 차단됩니다.
- 돌발 이벤트가 동시에 표시되고 15초 미선택자에게 무작위 옵션이 적용됩니다.
- 파산 또는 멘탈 0 플레이어가 즉시 탈락해 관전 상태가 됩니다.
- 24:00 또는 마지막 생존자 발생 시 최종 순위가 확정됩니다.
- 연결 종료와 잘못된 패킷이 다른 참가자의 경기를 중단시키지 않습니다.

