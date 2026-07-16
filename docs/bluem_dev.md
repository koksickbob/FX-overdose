# FX OVERDOSE 개발 기록

bluem이 구현한 기능과 검증 결과를 기록하는 문서입니다.
기능 하나를 완성할 때마다 아래 형식으로 계속 추가합니다.

---

## 2026-07-13 — GameManager 기본 게임 진행 시스템

### 구현 완료

- 게임 상태 구분
  - `Playing`: 게임 진행 중
  - `Paused`: 일시정지
  - `GameOver`: 게임 종료
- 엔딩 종류 구분
  - `Success`: 목표 자산 달성
  - `Bankruptcy`: 보유 자산 소진
  - `Overdose`: 멘탈 관리 실패
- 시작 자산, 현재 자산, 목표 자산 관리
- 인게임 날짜와 시간 진행
- 현실 시간과 인게임 시간의 진행 속도 설정
- 자산 증가 및 감소 처리
- 목표 자산 달성 시 성공 엔딩 판정
- 자산이 0 이하가 되면 파산 엔딩 판정
- 게임 일시정지 및 재개 처리
- 추후 멘탈 시스템에서 호출할 Overdose 엔딩 진입 함수 준비

### 관련 파일

- `Assets/Scripts/GameManager.cs`

### 검증 완료

- Unity Play 모드에서 게임 시간이 정상적으로 흐르는 것을 확인함
- 날짜, 시간, 분 값이 정상적으로 변경되는 것을 확인함
- GameManager 기본 기능이 정상 작동함

### 다음 작업 후보

- AI 트레이더의 체력 및 멘탈 상태 시스템
- 임시 버튼을 이용한 자산 증감 테스트
- 날짜, 시간, 자산을 표시하는 HUD

---

## 2026-07-13 — AI 트레이더 체력 및 멘탈 시스템

### 구현 완료

- 트레이더 최대 체력 및 현재 체력 관리
- 트레이더 최대 멘탈 및 현재 멘탈 관리
- 게임 진행 중 시간에 따른 체력 감소
- 체력이 0일 때 시간에 따른 멘탈 감소
- 체력과 멘탈 수치를 `0 ~ 최대 수치` 범위로 제한
- 현재 멘탈 수치에 따른 감정 상태 구분
  - `Stable`: 안정
  - `Anxious`: 불안
  - `Danger`: 위험
  - `Overdose`: 통제 불능
- 멘탈이 0이 되면 GameManager의 Overdose 엔딩 호출
- UI 게이지 연결에 사용할 체력 및 멘탈 비율 제공

### 관련 파일

- `Assets/Scripts/TraderStatus.cs`
- `Assets/Scripts/GameManager.cs`

### Unity 연결

- TraderStatus 컴포넌트의 `Game Manager` 참조 연결
- 게임 진행 상태가 `Playing`일 때만 수치가 감소하도록 연결

### 검증 완료

- Unity Play 모드에서 체력이 정상적으로 감소하는 것을 확인함
- 체력이 0이 된 후 멘탈이 정상적으로 감소하는 것을 확인함
- TraderStatus와 GameManager 연결이 정상 작동함

### 다음 작업 후보

- 테스트 버튼을 이용한 체력, 멘탈 및 자산 증감
- 날짜, 시간, 자산, 체력, 멘탈을 표시하는 HUD
- 수익률에 따른 캐릭터 감정 및 스프라이트 변경

---

## 2026-07-13 — 회복 아이템 데이터 및 사용 처리

### 구현 완료

- ScriptableObject 기반 아이템 데이터 구조 구현
- 아이템 고유 ID, 이름, 설명, 아이콘 및 가격 정의
- 체력 및 멘탈 아이템 효과 유형 구분
- 아이템별 회복량 설정
- 에너지 드링크 데이터 생성
  - 체력 30 회복
  - 가격 500
- 디저트 데이터 생성
  - 멘탈 20 회복
  - 가격 700
- ItemUser를 통한 체력 및 멘탈 회복 효과 적용
- 체력이나 멘탈이 가득 찬 경우 아이템 사용 방지
- 아이템 사용 성공 여부 반환
- 잘못된 데이터 또는 참조 누락 시 Console 경고 처리

### 관련 파일

- `Assets/Scripts/Items/ItemData.cs`
- `Assets/Scripts/Items/ItemUser.cs`
- `Assets/Data/Items/EnergyDrink.asset`
- `Assets/Data/Items/Dessert.asset`
- `Assets/Scripts/TraderStatus.cs`

### Unity 연결

- GameScene의 TraderStatus 오브젝트에 ItemUser 컴포넌트 추가
- ItemUser의 TraderStatus 참조 연결

### 검증 완료

- 두 아이템 데이터 에셋의 효과 유형과 회복량 설정 확인
- EnergyDrink의 이름과 ID 설정 확인
- GameScene에서 ItemUser와 TraderStatus 참조 연결 확인
- Unity에서 스크립트 컴파일 및 컴포넌트 추가 확인

### 다음 작업 후보

- 아이템 보유 개수 관리용 Inventory 구현
- 실제 아이템 사용 버튼 및 아이콘 배치
- 아이템 사용 시 보유 개수 차감 및 HUD 갱신

---

## 2026-07-13 — 인벤토리 및 아이템 사용 버튼

### 구현 완료

- 아이템별 보유 개수 관리
- 같은 아이템 획득 시 수량 합산
- 보유하지 않은 아이템 사용 차단
- 아이템 효과 적용 성공 시에만 수량 차감
- 능력치가 가득 찬 경우 아이템 수량 유지
- 아이템 수량 변경 이벤트 제공
- 에너지 드링크 및 디저트 사용 버튼 생성
- 버튼에 아이템 이름과 현재 보유 수량 표시
- 보유 수량이 0이면 버튼 비활성화
- 에디터 메뉴를 통한 버튼 및 참조 자동 생성 도구 추가

### 관련 파일

- `Assets/Scripts/Items/Inventory.cs`
- `Assets/Scripts/Items/InventoryItemButton.cs`
- `Assets/Editor/ItemButtonsUIBuilder.cs`
- `Assets/Scripts/Items/ItemUser.cs`

### 검증 완료

- Unity Play 모드에서 아이템 버튼 표시 확인
- 에너지 드링크 사용 시 체력 회복 확인
- 디저트 사용 시 멘탈 회복 확인
- 아이템 사용 후 보유 수량 차감 확인
- 아이템 버튼과 Inventory 참조가 정상적으로 작동함

### 다음 작업 후보

- 아이템 구매를 위한 상점 시스템
- 상점 구매 성공 시 자산 차감 및 인벤토리 추가
- 상점 UI 열기 및 닫기

---

## 2026-07-14 — 레퍼런스형 트레이딩 차트 UI

### 구현 완료

- 기존 실시간 캔들 및 시간봉 전환 기능을 유지한 채 차트 외형 개선
- 차트 전체를 채우도록 캔들 간격 자동 계산
- 상승 캔들은 민트색, 하락 캔들은 코랄색으로 변경
- 차트 패널, 종목명, 현재가, 등락률 및 시간봉 버튼을 레퍼런스형으로 배치
- 가로·세로 그리드와 현재가 점선 및 가격 태그 추가
- 우측 가격축과 하단 시간축의 글자 크기 및 색상 통일
- 현재가 태그를 차트 우측 경계 안쪽으로 이동해 화면 이탈 방지
- 하단 주문부를 LONG / SHORT / LEVERAGE 3카드 비율로 재배치
- 롱·숏 대형 버튼과 레버리지 증감·프리셋 버튼을 모바일 터치 크기로 확대
- LEVERAGE / MARGIN 탭 전환 UI 복구
- MARGIN 비율 증감 버튼과 10%·25%·50%·75%·100% 프리셋 재배치

### 관련 파일

- `Assets/Editor/ChartReferenceStyler.cs`
- `Assets/Editor/BottomTradingReferenceStyler.cs`

---

## 2026-07-16 — 차트 좌측·상단 안전 여백 정렬

- 차트 좌측 여백을 DAY 카드와 동일한 12px로 통일
- 차트 상단 앵커를 0.90으로 내려 108px TopBar 영역과 분리
- TopBar 하단과 차트 상단 사이에 추가 8px 여백 적용
- 하단 매매 UI 스타일러가 차트를 다시 0.92까지 올리던 덮어쓰기 제거
- UI 최초 생성, 차트 스타일 재적용, TopBar 재적용 경로에 동일한 위치값 반영

관련 파일:

- `Assets/Editor/TradingViewUIBuilder.cs`
- `Assets/Editor/ChartReferenceStyler.cs`
- `Assets/Editor/BottomTradingReferenceStyler.cs`
- `Assets/Editor/BalancePnLCardStyler.cs`

### 하단 매매 카드 여백 통일

- LONG 카드 좌측 외곽 여백을 차트·DAY 카드와 동일한 12px로 적용
- LONG↔SHORT, SHORT↔레버리지 카드 사이 간격을 각각 8px로 통일
- 레버리지 카드 우측 외곽 여백도 12px로 적용
- LONG·SHORT 합산 크기의 포지션 매도 버튼도 동일한 외곽선에 맞춤

### 레버리지·마진 컨트롤 세로 중앙 정렬

- 카드 위·아래 내부 여백을 각각 6%로 통일
- LEVERAGE/MARGIN 탭과 `+/-` 조작 영역 사이 간격을 약 4.5%로 확보
- `+/-` 조작 영역과 하단 수치 프리셋 버튼 사이 간격도 약 4.5%로 일치
- 레버리지와 마진 모드에 동일한 앵커 범위 적용

### 하단 매매 UI 우측선 정렬

- 차트 우측선의 `-6px` 오프셋을 레버리지·마진 카드 우측에도 동일하게 적용
- LONG·SHORT 사이 8px 간격과 좌측 12px 여백은 유지
- 차트와 하단 매매 UI의 우측 수직선이 한 줄로 이어지도록 정렬

### SHOP 버튼 세로 크기 확대

- SHOP 버튼을 420×140px에서 절반인 210×70px로 축소
- 가로와 세로를 같은 비율로 줄여 원본 3:1 비율 유지
- 기존 인벤토리 추적 위치와 클릭 기능 유지

### 캐릭터·말풍선·레벨 HUD 상향 정렬

- 실제 AI 대사에 연결된 말풍선 중심 Y를 0.455에서 화면 중앙인 0.5로 이동
- 말풍선 높이와 가로 위치는 유지하고 Y 앵커만 `0.395~0.605`로 조정
- 캐릭터도 동일하게 화면 높이의 4.5%만큼 위로 이동
- 레벨·EXP HUD는 캐릭터 자식 구조를 유지해 캐릭터와 함께 자동 이동
- 현재 GameScene과 UI 생성 빌더에 동일한 위치값 적용

### TopBar 좌우 마진 균등 정렬

- TopBar 카드 묶음의 정렬을 `MiddleLeft`에서 `MiddleCenter`로 변경
- 화면에 남던 32px 공간을 좌우 16px씩 균등 분배
- 카드 크기, 카드 사이 8px 간격, 기본 좌우 패딩 12px은 유지

### AI 포지션 상태 창 내부 정렬

- AI 진입 시 표시되는 `PositionStatusPanel`의 기본 100×100px 크기 문제 수정
- 레버리지·마진 카드 내부 8% 안전영역으로 상태 패널 확장
- 좌우·상하 패딩을 모두 14px로 통일하고 텍스트 묶음을 중앙 정렬
- 긴 TARGET/LIQ 문장은 카드 폭 안에서 자동 축소하고 넘칠 경우 말줄임 처리
- 현재 스타일러와 최초 UI 생성 빌더에 동일한 레이아웃 적용
- `Assets/Scripts/UI/Chart/ChartUIController.cs`
- `Assets/Scripts/UI/Chart/CandleItemUI.cs`

### 검증 완료

- Unity 스크립트 컴파일 성공
- 기존 차트 데이터 갱신 및 시간봉 버튼 연결 유지

---

## 2026-07-14 — 게임 전체 PF Stardust 폰트 통일

### 구현 완료

- TextMesh Pro 프로젝트 기본 폰트를 PF Stardust Bold Dynamic SDF로 변경
- 현재 GameScene의 모든 TMP 텍스트에 PF Stardust 일괄 적용
- 런타임 씬 로드 시 활성·비활성 UI 전체에 동일 폰트 자동 적용
- 새로 생성되는 차트, 상점, 아이템 및 상태 UI의 기본 폰트 통일
- PF Stardust에 없는 한글 글리프는 Korean Dynamic Font로 fallback 처리

### 관련 파일

- `Assets/Editor/PFStardustGlobalFontApplicator.cs`
- `Assets/Scripts/UI/GlobalPFStardustFont.cs`
- `Assets/Fonts/PFStardustBold Dynamic SDF.asset`
- `Assets/TextMesh Pro/Resources/TMP Settings.asset`

---

## 2026-07-14 — 동적 픽셀 인벤토리 및 아이템 스프라이트

### 구현 완료

- CARE ITEMS 스타일의 픽셀 인벤토리 슬롯 프레임 제작
- 에너지 드링크 및 딸기 디저트 픽셀 스프라이트 제작
- 초록 크로마 배경 제거 후 투명 PNG로 프로젝트에 저장
- `Inventory.Slots`의 유효 아이템 수만큼 슬롯 자동 생성
- 최대 5열 배치 후 슬롯이 늘어나면 다음 행으로 자동 확장
- 슬롯 개수에 따라 패널 너비·높이·슬롯 크기·간격 자동 계산
- 슬롯에 아이콘과 우측 하단 보유 수량 표시
- 아이템 추가로 슬롯 수가 변하면 인벤토리 UI 자동 재생성
- 상점 구매 및 아이템 사용 직후 인벤토리 수량 텍스트 즉시 갱신

### 관련 파일

- `Assets/Img/Items/EnergyDrinkPixel.png`
- `Assets/Img/Items/DessertPixel.png`
- `Assets/Img/UI/InventorySlotFramePixel.png`
- `Assets/Scripts/Items/DynamicInventoryUI.cs`
- `Assets/Scripts/Items/InventoryItemButton.cs`
- `Assets/Editor/InventoryPixelUIInstaller.cs`

### 추가 — 픽셀 SHOP 버튼

- 인벤토리 슬롯 프레임과 같은 네이비·라벤더 픽셀 SHOP 버튼 제작
- 기존 ShopManager 클릭 기능을 유지한 채 버튼 Image 교체
- PF Stardust `SHOP` 텍스트를 이미지 위에 별도로 배치
- 인벤토리 슬롯 행이 늘어나면 버튼이 패널 위로 자동 이동

관련 파일:

- `Assets/Img/UI/ShopButtonPixel.png`
- `Assets/Editor/ShopPixelButtonStyler.cs`
- `Assets/Scripts/UI/ShopButtonInventoryFollower.cs`

### 추가 — 동적 CARE SHOP 팝업

- 승인된 프리뷰를 기준으로 네이비·라벤더 픽셀 상점 팝업 구현
- `ShopManager.catalogItems` 목록을 기준으로 상품 카드 자동 생성·삭제
- 상품 1~3개는 한 행에 자동 맞춤, 더 많아지면 다음 행 및 세로 스크롤 사용
- 상품 아이콘, 효과, 가격, 보유 수량 및 BUY 버튼 자동 구성
- 구매 성공 즉시 보유 수량과 상단 잔액 갱신
- 기존 상점 열기·닫기·게임 일시정지·잔액 차감 기능 유지
- 전용 Overlay Canvas(sorting order 100)로 트레이딩 UI보다 항상 위에 표시
- 상품 카드 좌우 안전 여백을 적용해 오른쪽 디저트 카드 테두리 잘림 방지

관련 파일:

- `Assets/Scripts/Items/DynamicShopUI.cs`

---

## 2026-07-15 — 돌발 선택 이벤트 UI 표시 순서 수정

- 런타임 생성 돌발 이벤트 팝업에 전용 Canvas 추가
- 팝업 `sortingOrder`를 150으로 설정해 차트·HUD·상점보다 위에 표시
- 설정 메뉴는 기존 200을 유지해 최상단 계층 보존
- GraphicRaycaster를 보장해 이벤트 선택 버튼 입력이 다른 UI에 막히지 않도록 수정
- 수동 연결된 이벤트 패널에도 같은 정렬 설정을 자동 적용

관련 파일:

- `Assets/Scripts/Events/ChoiceEventPopupUIController.cs`

---

## 2026-07-15 — 말풍선 화살표 제거 및 대사 가독성 개선

- 말풍선 오른쪽 아래의 삼각형 진행 화살표 제거
- PF Stardust 대사 자동 크기를 14~22로 확대
- 대사를 좌측 상단 정렬로 변경해 아래 방향으로 자연스럽게 줄바꿈
- 긴 문장은 자동 축소하고 최소 크기에서도 말풍선 영역 밖으로 나오지 않도록 Truncate 유지
- 현재 GameScene과 런타임 스타일, UI 재생성 도구에 동일 설정 적용

관련 파일:

- `Assets/Img/Generated_image_1-removebg-preview.png`
- `Assets/Scripts/AI/AIVisualController.cs`
- `Assets/Editor/TradingViewUIBuilder.cs`

---

## 2026-07-15 — HP·Mental 전용 프레임 및 설정 버튼 복구

- 공용 카드 이미지를 840×92 영역에 강제 확장하던 방식을 제거
- 공용 카드와 동일한 남색·청회색 픽셀 테두리를 사용하는 HP·Mental 전용 가로 프레임 제작
- 프레임 중앙에 HP/Mental 구분선, 우측에 독립 설정 버튼 칸 구성
- 실제 표시 비율 840:92에 맞춰 모서리와 테두리가 찌그러지지 않도록 스프라이트 비율 보정
- 설정 톱니를 폰트 글리프에서 독립 투명 픽셀 스프라이트로 교체
- 설정 버튼의 기존 일시정지·게임 종료 연결은 그대로 유지

관련 파일:

- `Assets/Img/VitalsPanelFrameUnified.png`
- `Assets/Img/UI/SettingsGearUnified.png`
- `Assets/Editor/VitalsPanelStyler.cs`
- `Assets/Editor/SettingsMenuInstaller.cs`

### HP·Mental 내부 여백 보정

- 제목, 하트·두뇌 아이콘, 게이지를 프레임 테두리에서 안쪽으로 이동
- 게이지 배경과 실제 Fill 영역 높이를 줄여 상하 여백 확보
- HP와 Mental에 동일한 내부 마진과 크기 규칙 적용
- 전용 프레임과 설정 버튼 영역의 크기는 유지
- 중앙 구분선을 기준으로 Mental 내부 요소를 우측으로 3% 이동
- 외곽선→HP 시작 여백과 중앙선→Mental 시작 여백을 동일하게 맞춤
- 양쪽 아이콘·게이지·수치 영역에 동일한 폭과 간격 적용

### AI·USER 전환 버튼 차트 정렬

- P&L 아래 8px 여백 조건 유지
- 버튼 상단을 차트 상단보다 10px 아래로 이동
- 차트 오른쪽 8px 간격을 유지해 차트 영역과 겹치지 않도록 정렬
- 버튼의 2px 외곽선을 고려해 Rect 간격을 10px로 설정하고 실제 보이는 차트 여백은 8px로 통일

---

## 2026-07-15 — 비-TopBar UI 외곽선 3px 통일

- TopBar를 제외한 게임 UI의 공통 외곽선 두께를 3px로 정의
- 차트, LONG/SHORT/매도, 레버리지·마진 버튼에 동일한 두께 적용
- AI/USER 전환 버튼과 설정 메뉴 내부 버튼에 동일한 두께 적용
- 인벤토리 패널과 동적 아이템 슬롯에 동일한 두께 적용
- 상점 모달, 상품 카드, 구매·닫기 버튼에 동일한 두께 적용
- 액티브 스킬 버튼·상세 창·업그레이드 버튼에 동일한 두께 적용
- 캐릭터 레벨·EXP HUD 외곽선에 동일한 두께 적용
- 외곽선 색상은 각 기능의 기존 강조색을 유지하고 두께만 공통화

관련 파일:

- `Assets/Scripts/UI/UIStrokeStyle.cs`
- `Assets/Scripts/UI/SettingsMenuController.cs`
- `Assets/Scripts/UI/ActiveSkillHUDController.cs`
- `Assets/Scripts/UI/TraderLevelUIController.cs`
- `Assets/Scripts/Items/DynamicInventoryUI.cs`
- `Assets/Scripts/Items/DynamicShopUI.cs`
- `Assets/Editor/ChartReferenceStyler.cs`
- `Assets/Editor/InventoryPixelUIInstaller.cs`
- `Assets/Editor/BottomTradingReferenceStyler.cs`
- `Assets/Scenes/GameScene.unity`

---

## 2026-07-15 — 플레이어 포지션 전용 매도 버튼

- LONG/SHORT 버튼 재클릭으로 포지션이 종료되던 토글 동작 제거
- 플레이어 수동 포지션 보유 시에만 `포지션 매도` 버튼 표시
- 매도 버튼을 LONG과 SHORT 두 카드의 합친 영역 크기로 배치
- 매도 버튼이 두 진입 버튼 위를 덮고 클릭 입력을 우선하도록 계층 조정
- 매도 버튼 클릭 시 현재 플레이어 포지션 전량 정리
- AI 보유 포지션에는 플레이어 매도 버튼이 표시되지 않도록 소유자 검사 추가

관련 파일:

- `Assets/Scripts/UI/Chart/TradingPanelUIController.cs`
- `Assets/Editor/BottomTradingReferenceStyler.cs`
- `Assets/Editor/TradingViewUIBuilder.cs`
- `Assets/Scenes/GameScene.unity`
- `Assets/Scripts/Items/ShopManager.cs`
- `Assets/Scripts/Items/ShopItemButton.cs`
- `Assets/Editor/DynamicShopUIInstaller.cs`

---

## 2026-07-14 — AI 대사 텍스트 가독성 조정

- 캐릭터 AI 대사에 PF Stardust 기본 폰트 적용
- 말풍선 크기에 맞춰 자동 글자 크기를 11~17 범위로 축소
- 긴 대사가 자연스럽게 줄바꿈되도록 설정
- 좌측 중앙 정렬과 내부 여백을 적용해 말풍선 테두리와 겹치지 않도록 개선
- 대사 갱신 시에도 스타일이 유지되도록 `AIVisualController`에서 재적용

관련 파일:

- `Assets/Scripts/AI/AIVisualController.cs`
- `Assets/Editor/TradingViewUIBuilder.cs`

---

## 2026-07-15 — 영양제·진정제 아이템 확장

- 기획서의 체력 관리 아이템 `Supplement` 추가: HP +50, 가격 $1,100
- 기획서의 멘탈 관리 아이템 `Sedative` 추가: Mental +40, 가격 $1,300
- 기존 아이템 스타일에 맞춘 256×256 투명 픽셀 스프라이트 제작
- 시작 인벤토리에 영양제와 진정제 각각 1개 추가
- 상점 Catalog Items에 두 아이템을 중복 없이 자동 추가
- 상품 4개가 2열×2행으로 표시되도록 상점 카드 레이아웃 조정
- 인벤토리는 기존 동적 슬롯 시스템을 사용해 4개 아이템에 맞춰 자동 확장

관련 파일:

- `Assets/Img/Items/SupplementPixel.png`
- `Assets/Img/Items/SedativePixel.png`
- `Assets/Data/Items/Supplement.asset`
- `Assets/Data/Items/Sedative.asset`
- `Assets/Editor/CareItemExpansionInstaller.cs`
- `Assets/Scripts/Items/DynamicShopUI.cs`
- `Assets/Scenes/GameScene.unity`

---

## 2026-07-14 — 픽셀 설정 버튼 및 일시정지 메뉴

- HP/Mental UI 우측 상단 슬롯에 픽셀 톱니바퀴 버튼 이미지 적용
- 설정 버튼 클릭 시 `GameManager`와 Unity 전체 시간을 함께 일시정지
- PF Stardust 폰트를 적용한 설정 팝업 생성
- `CONTINUE` 버튼으로 이전 플레이 상태 복원
- `QUIT GAME` 버튼으로 빌드에서는 게임 종료, Unity Editor에서는 Play Mode 종료
- 설정 팝업 전용 Overlay Canvas를 사용해 다른 게임 UI보다 위에 표시
- TMP 텍스트와 Image를 같은 오브젝트에 추가하던 설치 오류를 수정하고 이미지 전용 자식 오브젝트로 분리

관련 파일:

- `Assets/Img/UI/SettingsGearPixel.png`
- `Assets/Scripts/UI/SettingsMenuController.cs`
- `Assets/Editor/SettingsMenuInstaller.cs`

---

## 2026-07-14 — 캐릭터 및 말풍선 누끼 보정

- 캐릭터 스프라이트 외곽의 초록색 크로마키 잔여 픽셀 제거
- 말풍선 테두리 주변의 초록색 번짐 제거
- 기존 픽셀 외곽선과 투명 배경을 유지하도록 디스필 및 1px 가장자리 정리 적용
- 기존 씬의 Sprite 참조를 유지하기 위해 원본 경로에 보정 PNG 반영

관련 파일:

- `Assets/Img/Generated_image_2-removebg-preview.png`
- `Assets/Img/Generated_image_1-removebg-preview.png`

---

## 2026-07-14 — 캐릭터·말풍선 좌우 배치

- 캐릭터를 우측 화면의 왼쪽 아래 영역으로 이동
- 말풍선을 캐릭터 위가 아닌 오른쪽 옆으로 이동
- 말풍선 원본의 왼쪽 꼬리가 캐릭터를 향하도록 위치 조정
- 현재 GameScene과 UI 재생성 도구에 동일한 앵커 비율 적용

관련 파일:

- `Assets/Scenes/GameScene.unity`
- `Assets/Editor/TradingViewUIBuilder.cs`

---

## 2026-07-15 — 캐릭터 머리 위 레벨·경험치 HUD

- 기존 전체 화면 레벨 팝업, 스킬 카드, `LEVEL` 버튼 제거
- 문서의 UI 색상 토큰(`#0B0F19`, `#0F172A`, `#141D33`, `#06B6D4`, `#CFFAFE`)만 사용한 픽셀 프레임 제작
- 갈색, 금속 질감, 볼트, 스크래치와 장식 요소를 제거하고 2칸 구조로 단순화
- 캐릭터 이미지의 자식으로 HUD를 배치해 캐릭터 위치 변경 시 함께 이동
- 캐릭터 머리 위에 현재 `LV`와 `EXP 현재값 / 필요값`만 표시
- 경험치 진행도를 작은 슬라이더로 표시하고 레벨 이벤트 발생 시 즉시 갱신
- 머리 위 공간을 덜 차지하도록 HUD 높이를 최종 60px로 축소
- 동적 텍스트에 프로젝트 기본 PF Stardust TMP 폰트 적용
- GameScene 실행 시 `ProtagonistCharacterImage`에 HUD를 자동 설치

관련 파일:

- `Assets/Resources/UI/CharacterLevelExpFrame.png`
- `Assets/Scripts/UI/TraderLevelUIController.cs`

---

## 2026-07-15 — 설정 버튼 하단 AI·USER 텍스트 전환

- 잘못 추가한 AI·플레이어 공용 포지션 매도 버튼 이미지와 기능 변경 제거
- 설정 버튼 바로 아래의 기존 매매 모드 전환 버튼에서 화살표 스프라이트 제거
- 문서 색상 토큰(`#0B0F19`, `#0F172A`, `#141D33`, `#06B6D4`, `#CFFAFE`) 사용
- 현재 AI 모드에서는 `AI`, 직접 매매 모드에서는 `USER` 라벨 표시
- 단색 `#141D33` 배경과 `#06B6D4` 테두리만 사용해 텍스트 중심으로 단순화
- 클릭 시 기존 `TradingController.ToggleTradingMode()`를 그대로 호출
- 모드 버튼을 `ChartMainPanel` 오른쪽 8px, `PnLCard` 아래 8px의 교차 영역에 배치
- 버튼 크기를 92×46px로 고정하고 캔버스 경계에서 클램핑해 다른 UI와 겹치지 않도록 처리

관련 파일:

- `Assets/Scripts/UI/SettingsMenuController.cs`

---

## 2026-07-15 — 우측 상단 3대 스킬 버튼 및 정보 창

- 프로젝트의 3대 성장 스킬 `차트 공부`, `큐브 풀기`, `파산 회고록 읽기`를 개별 버튼으로 분리
- 설정 버튼 아래에 66×66px 버튼 3개를 8px 간격의 세로 1열로 정렬
- 화면 우측에서 8px, 설정 버튼 아래에서 24px 여백을 유지하도록 배치
- 스킬 버튼 사이 간격을 12px로 조정해 각 버튼을 시각적으로 분리
- 차트 노트, 큐브, 회고록을 표현한 독립 투명 픽셀 스프라이트 제작
- 각 버튼에 현재 스킬 레벨 배지 표시
- 버튼 클릭 시 전용 정보 팝업을 다른 UI보다 높은 Sorting Order로 표시
- 정보 창에 현재 레벨, 실제 적용 효과, 다음 업그레이드 비용·HP·시간 표시
- 정보 창에 실제 `UPGRADE` 버튼을 추가하고 자산·HP·최대 레벨 조건 검사
- 업그레이드 성공 시 돈·체력·게임 시간을 실제 차감하고 스킬 레벨 및 효과 즉시 갱신
- 조건 부족 및 업그레이드 성공 결과를 정보 창 하단 메시지로 표시
- `TraderLevelSystem.OnSkillLevelChanged` 이벤트로 레벨 배지와 열린 정보 창을 즉시 갱신
- PF Stardust 폰트 및 문서의 남색·시안 색상 토큰 적용

관련 파일:

- `Assets/Scripts/UI/ActiveSkillHUDController.cs`
- `Assets/Scripts/Trading/TraderLevelSystem.cs`
- `Assets/Resources/UI/Skills/ChartStudyIcon.png`
- `Assets/Resources/UI/Skills/CubePatienceIcon.png`
- `Assets/Resources/UI/Skills/BookJudgmentIcon.png`

---

## 2026-07-15 — 상단 상태바 프레임·간격 통일

- 날짜·시간, 자산, P&L, HP·Mental, 설정 영역의 배경을 `StatusCardFrame`으로 통일
- 모든 상단 카드 높이를 92px로 맞추고 상단바 높이를 108px로 정리
- 카드 사이 간격 8px, 상하 패딩 8px, 화면 좌우 패딩 12px로 통일
- 좌측 날짜 카드에도 12px 화면 여백을 추가해 우측 설정 영역과 균형 조정
- 1920px 기준 한 줄 안에 들어오도록 날짜 310px, 자산 300px, P&L 390px, 상태 영역 840px로 정돈
- 설정 버튼의 별도 금속 프레임 이미지를 제거하고 공용 프레임 위에 톱니 레이어만 표시
- UI 재생성 시에도 같은 규칙이 유지되도록 `TradingViewUIBuilder` 기본 배치값 동기화

관련 파일:

- `Assets/Editor/DayTimeCardStyler.cs`
- `Assets/Editor/BalancePnLCardStyler.cs`
- `Assets/Editor/VitalsPanelStyler.cs`
- `Assets/Editor/SettingsMenuInstaller.cs`
- `Assets/Editor/TradingViewUIBuilder.cs`
### 액티브 스킬 버튼 우측 여백 정렬

- 액티브 스킬 버튼 묶음의 화면 우측 여백을 `12px`로 조정했습니다.
- 차트 UI의 좌측 외곽 여백 `12px`와 동일하게 맞춰 화면 좌우 균형을 통일했습니다.
### 캐릭터 레벨 UI 선 두께 통일

- 레벨 프레임 스프라이트 자체 테두리 위에 Unity `Outline`이 중복 적용되던 문제를 제거했습니다.
- 프레임에 포함된 단일 테두리만 표시하여 차트, 인벤토리, 스킬 버튼과 비슷한 선 두께로 정리했습니다.
### 주인공 캐릭터 스프라이트 교체

- `ProtagonistCharacterImage`가 기존부터 참조하던 캐릭터 PNG를 새 픽셀 캐릭터 이미지로 교체했습니다.
- 초록 크로마키 배경과 가장자리 초록 번짐을 제거하여 실제 알파 투명 PNG로 적용했습니다.
- 기존 씬의 GUID와 Sprite fileID를 유지하고, 1254px 원본에 맞춰 Sprite Editor 크롭 영역을 갱신했습니다.
### FX MARKET 상점 및 액티브 장비 4종

- 상점을 쿠팡·아마존식 상품 탐색 구조를 참고한 `FX MARKET` 3열 스크롤 마켓으로 개편했습니다.
- 상단 카테고리 바, 잔액, 즉시 적용 안내, 상품 유형 배지, 설명, 가격, 보유/레벨 상태, 구매 CTA를 분리했습니다.
- 듀얼 모니터(수익 증폭), 손실 보험(손실 감소), 심리 상담권(멘탈 소모 완화), 인체공학 의자(체력 소모 완화)를 추가했습니다.
- 네 아이템 모두 전용 투명 픽셀 스프라이트와 `ItemData`가 자동 생성되며 `ShopManager.catalogItems`에 동적으로 등록됩니다.
- `GameManager`가 `ActiveItemEffectManager`를 새 게임 시작 전에 자동 생성하여 구매·업그레이드·실제 보정 효과가 정상 작동합니다.
### 상점 상품 배지 및 구매 버튼 이미지 교체

- `CARE ITEM`, `ACTIVE GEAR` 배지에 각각 청록 케어 프레임과 골드 액티브 프레임을 적용했습니다.
- 일반 소모품 구매 버튼과 액티브 장비 구매/업그레이드 버튼을 별도 픽셀 프레임 이미지로 교체했습니다.
- 텍스트는 이미지에 굽지 않고 PF Stardust TMP 텍스트로 유지하여 `BUY`, `ACTIVE`, `MAX LV` 상태가 동적으로 바뀝니다.
- 네 프레임은 투명 PNG, Point 필터, 무압축, 9-slice로 자동 임포트됩니다.
### 주인공 캐릭터 상시 Idle 애니메이션

- 현재 착석 캐릭터를 기준으로 호흡, 머리카락 흔들림, 눈 깜빡임이 포함된 8프레임 idle 시트를 추가했습니다.
- 4×2 시트를 런타임에서 384×512 셀로 정확히 분할하여 별도의 Animator Controller 없이 재생합니다.
- 프레임별 유지 시간을 다르게 지정해 눈 깜빡임은 빠르고 호흡은 느리게 보이도록 구성했습니다.
- `Time.unscaledDeltaTime`을 사용하여 상점이나 설정 창으로 게임이 일시정지된 동안에도 계속 반복됩니다.
- GameScene의 `ProtagonistCharacterImage`에 자동으로 설치되므로 씬 수동 연결이 필요하지 않습니다.

### Idle 애니메이션 프레임 흔들림 보정

- 8개 프레임의 불투명 캐릭터 영역을 기준으로 몸의 가로 중심과 발끝 위치를 정렬했습니다.
- 모든 프레임을 동일한 `244×482` 크기로 잘라 프레임 전환 중 캐릭터와 UI 크기가 좌우·상하로 튀지 않게 수정했습니다.

### 주인공 Idle/한숨 애니메이션 16프레임 확장

- 착석 캐릭터 애니메이션을 기존 8프레임에서 16프레임으로 확장했습니다.
- 눈을 감고 어깨를 내린 뒤 픽셀 숨결이 커졌다가 사라지고 천천히 원래 자세로 돌아오는 한숨 동작을 추가했습니다.
- 한숨이 화면에서 약 3초간 보이도록 프레임별 유지 시간을 늘렸습니다.
- 4×4 시트를 `1536×2048`의 세로형 구조로 정규화해 각 셀이 정확히 `384×512`가 되며, 몸 중심과 발끝이 모든 프레임에서 같은 위치를 유지합니다.

### Idle 13~16프레임 하체 및 표시 크기 재보정

- 하체 비율이 달랐던 13~16프레임을 정상 착석 하체가 유지되는 회복 프레임으로 교체했습니다.
- 모든 프레임의 골반 중심과 발끝을 동일 좌표에 배치하여 프레임 전환 중 위치가 흔들리지 않게 했습니다.
- Unity `Image.preserveAspect`에서 작아지던 문제를 세로형 셀과 `1.6배` 표시 배율로 보정해 기존 캐릭터 크기를 복원했습니다.

### Idle 재생 속도 및 캐릭터 크기 조정

- 한숨이 유지되는 7~10프레임의 재생 시간을 약 `3초`에서 `1.4초`로 줄였습니다.
- 전체 16프레임 루프를 약 `10.5초`에서 `6.3초`로 단축해 idle 움직임이 조금 더 빠르게 보이게 했습니다.
- 캐릭터 표시 배율을 `1.6`에서 `1.12`로 변경하여 현재 크기에서 30% 축소하고 중앙 피벗 위치를 유지했습니다.

### Idle 루프 4.5초 속도 조정

- 16개 프레임의 유지 시간을 비례 조정하여 전체 idle/한숨 루프를 약 `4.5초`로 단축했습니다.
- 한숨의 준비, 숨결 확대, 회복 순서는 유지하면서 동작 사이의 대기 시간만 줄였습니다.

### 일반 Idle 3회 후 한숨 1회 재생

- 한숨이 없는 정상 호흡 전용 16프레임 시트 `ProtagonistIdleNoSighSheet16.png`를 추가했습니다.
- 일반 idle 시트를 3회 재생한 뒤 한숨 시트를 1회 재생하고 다시 일반 idle로 돌아가도록 전환 로직을 구현했습니다.
- 두 시트 모두 `384×512` 프레임 규격과 동일한 몸 중심·발끝 좌표를 사용해 전환 순간에도 위치가 흔들리지 않습니다.

### 캐릭터 스프라이트 HD 및 투명 경계 정리

- 일반 idle과 한숨 idle 시트를 각각 `3072×4096` HD PNG로 2배 확대했습니다.
- 완전 투명 픽셀의 숨은 RGB, 옅은 크로마키 잔여 픽셀, 녹색 외곽 번짐을 제거했습니다.
- 소프트 매트 알파 범위를 재정규화하고 Point 필터와 무압축 임포트를 유지해 픽셀 외곽을 선명하게 표시합니다.
- Unity 최대 텍스처 크기를 `4096`으로 올려 HD 원본이 임포트 과정에서 다시 축소되지 않도록 했습니다.
- 기존 `일반 idle 3회 → 한숨 idle 1회` 재생 순서와 프레임 중심 좌표는 그대로 유지됩니다.

### 멘탈 상태별 정적 캐릭터 스프라이트

- `Stable`, `Anxious`, `Danger`, `Overdose` 4종의 정적 캐릭터 스프라이트를 제작했습니다.
- `Stable`에서는 기존 일반 idle 3회/한숨 1회 애니메이션을 그대로 재생합니다.
- `Anxious`, `Danger`, `Overdose` 진입 시 애니메이션을 잠시 멈추고 해당 상태 스프라이트를 즉시 표시합니다.
- 멘탈이 `Stable`로 회복되면 중단했던 idle 애니메이션으로 자동 복귀합니다.
- 네 상태 모두 기존 HD 프레임과 같은 셀 비율, 몸 중심, 발끝 위치를 사용합니다.

### 멘탈 상태별 고품질 32프레임 애니메이션 전면 교체

- 승인된 귀엽고 불쌍한 Overdose 스프라이트 품질을 기준으로 캐릭터 외형을 전면 통일했습니다.
- `Stable 일반`, `Stable 한숨`, `Anxious`, `Danger`, `Overdose` 전용 애니메이션 시트 5종을 새로 제작했습니다.
- 각 시트는 16개 고유 키프레임을 순방향/역방향으로 왕복 재생하여 상태별 32프레임 루프를 구성합니다.
- Stable은 기존 규칙대로 일반 idle을 3회 재생한 뒤 한숨 idle을 1회 재생합니다.
- Anxious는 얕은 호흡과 긴장, Danger는 눈물과 떨림, Overdose는 귀엽고 불쌍한 완전 붕괴 동작을 반복합니다.
- 모든 시트의 셀은 `384×512`이며 골반 중심, 손 지지점, 발끝과 UI 표시 비율을 동일하게 정규화했습니다.
- 초록 크로마키, 가장자리 녹색 번짐, 완전 투명 픽셀의 숨은 RGB를 제거했습니다.

### 캐릭터 애니메이션 제거 및 단일 스프라이트 복원

- 요청에 따라 캐릭터 idle, 한숨, 멘탈 상태별 애니메이션과 자동 재생 코드를 전부 제거했습니다.
- `GameScene`의 `ProtagonistCharacterImage`에 원래 연결되어 있던 단일 캐릭터 스프라이트를 다시 그대로 사용합니다.
