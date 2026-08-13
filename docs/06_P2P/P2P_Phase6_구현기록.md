# P2P Phase 6 네트워크 수동매매 및 순위 구현 기록

## 구현 결과

클라이언트가 LONG·SHORT·종료 요청만 보내고 호스트가 Phase 1 검증기와 Phase 5 권위 가격으로 체결하는 `NetworkTradingAuthority`를 추가했습니다.

## 구현 기능

- 요청 ID, 방향, 레버리지와 마진 비율만 전송
- NGO 연결 매핑과 요청 Steam ID 대조
- 호스트의 `P2PLocalMatch.SubmitTrade`로 중복·잔액·설정·포지션 검증
- 호스트 시장 가격으로만 체결
- 가격 갱신 때 전 플레이어 미실현 손익과 청산가 판정
- 현금·총자산·포지션·PnL·실시간 순위 전체 배포
- 특정 로비 화면에 LONG, SHORT와 포지션 종료 테스트 버튼
- 주문 승인·거절 사유와 체결 가격 표시

클라이언트 Payload에는 현금이나 총자산이 존재하지 않습니다. 따라서 클라이언트가 자산값을 임의로 만들어 보낼 수 없고 호스트 상태만 전원에게 복제됩니다.

## 주요 파일

- `Assets/Scripts/P2P/Core/P2PNetworkTradingModels.cs`
- `Assets/Scripts/P2P/Infrastructure/NetworkTradingAuthority.cs`
- `Assets/Tests/EditMode/P2PNetworkTradingCodecTests.cs`

## 기술 스택

| 기술 | 역할 | 설명 |
|---|---|---|
| NGO Custom Messaging | 주문 요청 및 권위 상태 배포 | 요청은 ReliableSequenced로 호스트에 보내며 결과도 신뢰성 있게 전원에게 전달합니다. |
| Phase 1 도메인 모델 | 주문 검증·체결·손익·순위 | Unity와 네트워크에 의존하지 않는 기존 권위 계산을 그대로 재사용합니다. |
| Phase 5 Market Authority | 체결 및 평가 가격 | 클라이언트 화면 가격이 아니라 호스트의 최신 시장 가격을 사용합니다. |
| Steam ID 매핑 | 요청자 신원 검증 | NGO Client ID와 승인된 Steam ID를 대조해 다른 플레이어를 사칭한 주문을 버립니다. |

## 실연결 검증

1. 두 클라이언트로 경기 시작
2. Host가 LONG, Client가 SHORT 요청
3. 양쪽에 같은 체결 가격과 참가자 순위가 표시되는지 확인
4. 같은 플레이어가 포지션 보유 중 다시 진입하면 `PositionConflict` 확인
5. 포지션 종료 후 현금과 총자산이 양쪽에서 같은지 확인
6. 설정 상한을 넘긴 요청이 `InvalidLeverage` 또는 `InvalidMargin`으로 거절되는지 확인
