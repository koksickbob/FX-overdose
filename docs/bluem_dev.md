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

### 주인공 AI 19종 감정 스프라이트 연동

- 기존 단일 캐릭터의 외형, 착석 비율, 의상과 픽셀 스타일을 기준으로 `TraderEmotion` 19종 전용 스프라이트를 제작했습니다.
- 모든 이미지는 동일한 `1254×1254` 캔버스와 중심 좌표를 사용하며 초록 크로마키와 외곽 잔색을 제거했습니다.
- `AIVisualController`가 ROE, 멘탈, 체력, 이벤트 문맥으로 판정된 감정을 동명의 스프라이트에 직접 연결합니다.
- 이벤트 연출에서 `ShowEmotion(TraderEmotion emotion, float duration)`으로 특정 감정을 잠시 강제 표시할 수 있습니다.

### 감정 캐릭터 표시 크기·레벨 HUD·Jealous 보정

- 19종 이미지의 공통 투명 여백을 제거하고 기존 캐릭터와 같은 세로형 비율로 맞춰 게임 화면 표시 크기를 키웠습니다.
- 캐릭터 자식이던 레벨/EXP HUD를 Canvas로 옮기고 `AI/USER` 전환 버튼 오른쪽에 10px 간격, 동일한 46px 높이로 배치했습니다.
- 11번째 `Jealous` 스프라이트의 비정상적인 추가 손을 제거하고 정확히 두 팔이 자연스럽게 교차하도록 교체했습니다.

### 인벤토리 슬롯 아이콘 중앙 정렬

- 동적 인벤토리의 `ItemIcon` 영역을 슬롯 기준 상하좌우 `14%`의 동일한 여백으로 변경했습니다.
- 아이콘 피벗과 Anchored Position을 슬롯 정중앙에 고정해 에너지 드링크를 포함한 모든 아이템이 프레임 중앙에 표시됩니다.

### 타이틀 화면 1차 구현

- `TitleScene`을 새로 추가하고 Build Settings의 첫 번째 진입 씬으로 등록했습니다.
- 기존 트레이딩 룸 배경에 네이비 딤, 시안/핑크 포인트를 적용한 `FX OVERDOSE` 픽셀 타이틀 화면을 구성했습니다.
- `NEW GAME`, `CONTINUE`, `SETTINGS`, `QUIT` 버튼을 `MainMenuController`에 연결했습니다.
- 3개 저장 슬롯을 표시하는 불러오기 팝업과 BGM/SFX 슬라이더가 있는 설정 팝업을 구현했습니다.
- `LoadingScene`이 아직 없는 현재 단계에서는 새 게임 및 불러오기 선택 시 `GameScene`으로 안전하게 바로 이동합니다.
- `TitleScene`에 UI 전용 카메라를 자동 생성해 Game 뷰의 `Display 1 No cameras rendering` 상태를 방지했습니다.

### LLM·차트 연동 로딩 화면

- 검정 배경 중앙에 잠자는 SD 캐릭터, 하단에 `0%~100%` 진행 바를 배치한 `LoadingScene`을 추가했습니다.
- `GameScene`을 Additive 방식으로 백그라운드 준비하고, LLM 예열과 초기 대사 시퀀스 및 차트/시장 객체 준비가 모두 끝나야 100%가 됩니다.
- 로딩 화면 페이드 인/아웃을 구현하고, 페이드 아웃이 완전히 끝난 뒤에만 게임 시간과 시장이 시작되도록 연결했습니다.
- 잠자는 SD 캐릭터는 기존 캐릭터 디자인을 기준으로 새로 제작하고 투명 배경 스프라이트로 적용했습니다.
- 최초 실행에만 동작하던 런타임 초기화 방식 대신 `SceneManager.sceneLoaded`에 로딩 UI 생성을 연결해, 타이틀에서 `NEW GAME`으로 진입할 때 빈 화면이 나오던 문제를 수정했습니다.
- 로딩 페이드 아웃 후 게임 화면을 먼저 노출하고 `Time.timeScale = 0` 상태로 0.5초 정지한 뒤, 게임 개장과 첫 LLM 대사가 시작되도록 전환 순서를 조정했습니다.

### 멘탈 감소 플로팅 텍스트 방향 변경

- 멘탈 감소량과 원인 텍스트가 위로 올라가던 효과를 아래로 내려가며 사라지도록 반전했습니다.
- 이동 거리는 `VitalsValueUI`의 `Mental Float Travel Distance` 값으로 조절할 수 있습니다.

### 타이틀 불러오기 팝업 닫기 수정

- 씬에 정상적으로 직렬화되지 않던 보조 `TitleScreenRuntimeBinder`를 제거했습니다.
- 불러오기/설정 팝업의 닫기 버튼, 저장 슬롯, 볼륨 슬라이더 연결을 `MainMenuController`가 직접 담당하도록 변경했습니다.

### 타이틀 경유 시 스킬 HUD 복구

- 앱 최초 씬에서만 실행되던 액티브 스킬 HUD 설치 방식을 `SceneManager.sceneLoaded` 기반으로 변경했습니다.
- `LoadingScene`이 `GameScene`을 Additive로 준비하는 경우에도 게임 씬의 메인 Canvas를 직접 찾아 스킬 버튼 3개를 생성합니다.

### 타이틀 경유 시 레벨 HUD 복구

- 레벨/EXP UI도 `SceneManager.sceneLoaded` 방식으로 변경했습니다.
- Additive로 로드된 `GameScene` 내부의 `ProtagonistCharacterImage`를 직접 찾아 기존 `AI/USER` 전환 버튼 옆 레벨 HUD를 생성합니다.

### 레벨 HUD 크기 확대

- 레벨 UI 가로 폭을 기존 280px의 1.3배인 364px로 확대했습니다.
- AI/USER 전환 버튼 높이를 기준으로 위·아래를 각각 0.7px 확장해 레벨 HUD 높이를 47.4px로 미세 조정했습니다.
- 축소될 때 선까지 얇아지던 레벨 배경 스프라이트 대신 AI/USER 버튼과 동일한 `UIStrokeStyle` 3px Outline을 적용하고, 내부 구분선도 3px로 통일했습니다.

### AI 대화 말풍선 고해상도 교체

- 기존 810×308 저해상도 말풍선을 화면 비율에 맞는 1915×821 고해상도 픽셀 프레임으로 교체했습니다.
- UI 가이드 색상 토큰인 배경 `#0F172A`, 내부 음영 `#0B0F19`, 테두리 `#06B6D4`, 대사 `#CFFAFE`를 적용했습니다.
- 말풍선 스프라이트를 `Point (No Filter)`, 무압축, 9-Slice로 설정해 화면 비율이 달라져도 테두리와 꼬리가 찌그러지지 않도록 했습니다.
- 말풍선 자체의 투명 바깥 영역과 왼쪽 꼬리를 고려해 대사 영역을 네 방향 52px 안쪽으로 보정했습니다.
- 대사 블록을 가로·세로 중앙 정렬하고 런타임에도 RectTransform 값을 재적용해, 씬의 이전 직렬화 값이 남아 있어도 테두리 위로 글자가 밀리지 않도록 했습니다.
- 왼쪽 말풍선 꼬리로 인해 본체 중심이 어긋나는 만큼 텍스트 영역을 오른쪽으로 6px 보정해, 실제 프레임 기준 좌우 여백을 동일하게 맞췄습니다.

### 돌발 이벤트 인터넷 기사형 UI 개편

- 기존 단일 세로 팝업을 게임 팔레트에 맞춘 `FX WIRE` 인터넷 뉴스 페이지 형태로 전면 교체했습니다.
- 브라우저 주소 표시줄, 사이트 헤더, 실시간 속보 티커, 기사 메타 정보와 출처 영역을 추가했습니다.
- 왼쪽 기사 본문은 독립 스크롤로 구성하고, 오른쪽 선택지는 고정된 `RESPONSE DESK`에 배치해 긴 기사에서도 선택 버튼이 항상 보이도록 했습니다.
- 안전·롱 선택은 초록, 고위험·숏 선택은 빨강, 액티브 아이템 선택은 노랑으로 타입별 색상을 적용했습니다.
- 아이템 필요 수량과 보유 부족 상태를 선택 카드 안에 표시하고, 부족한 선택지는 기존 로직대로 비활성화됩니다.
- 1920×1080 기준의 모바일 가로 화면과 PF Stardust 기본 폰트, 공통 3px 외곽선 규칙을 유지했습니다.
- 이벤트 선택 효과, 게임 일시정지, 아이템 차감 로직은 변경하지 않고 기존 `ChoiceEventController`와 그대로 연동했습니다.
- 기사 본문 글자 크기를 25px에서 27px로 높여 가독성을 소폭 개선했습니다.
- `RESPONSE DESK` 선택지의 기본 글자 크기를 18px에서 20px로, 자동 축소 최소값을 11.5px에서 13px로 높였습니다.

### 일일 정산 UI 구현

- 인게임 시간이 24:00에 도달해 `GameState.Settlement`로 전환되면 자동으로 열리는 일일 정산 화면을 추가했습니다.
- 영수증형 `DAILY LEDGER` 패널에 당일 순자산 손익, 수익률, 시작 자산, 현재 총자산, HP와 멘탈 마감 상태를 표시합니다.
- 수익은 초록색과 위 화살표, 손실은 빨간색과 아래 화살표, 보합은 노란색으로 구분합니다.
- 정산 결과에 따라 기존 19종 요미 감정 스프라이트 중 환희·자신감·만족·불안·절망·오열·안도 이미지를 자동 표시합니다.
- `EventCategory.DailySettlement`로 LLM 반응을 요청하고, 모바일·오프라인 모드에서도 손익별 전용 대사가 출력되도록 폴백 대사를 추가했습니다.
- LLM 응답이 지연되어도 8초 뒤 로컬 요약으로 다음 날 진행 버튼을 사용할 수 있도록 안전장치를 넣었습니다.
- `PROCEED TO DAY` 버튼은 정산 UI를 닫은 후 `GameManager.ProceedToNextDay()`를 호출해 다음 날 09:00부터 게임을 재개합니다.
- 스킬 시간 넘김이 자정을 지나는 경우에도 정산 LLM 요청이 차단되지 않도록 예외 처리했습니다.
- 저장 데이터에 `StartOfDayEquity`를 포함해 이어하기 후에도 당일 손익이 정확하게 계산되도록 보완했습니다.
- 스킬 상세창·설정창보다 정산 화면의 표시 우선순위를 높여 자정을 넘긴 스킬 업그레이드 뒤에도 정산창이 가려지지 않습니다.
- 마지막 1분의 차트 변동을 먼저 반영한 뒤 정산 손익을 계산하고, 정산 중 자동 포지션 만료가 증거금을 유실시키지 않도록 거래 갱신을 멈춥니다.
- 현 저장 형식에 포함되지 않는 포지션 증거금이 유실되지 않도록 열린 포지션 보유 중 저장은 차단하고 버튼에 `CLOSE POSITION`을 표시합니다.
- 각 LLM 정산 요청에 일차 번호를 함께 전달해, 고속 시간 진행 중 늦게 도착한 전날 반응이 다음 날 정산창에 섞이지 않도록 했습니다.
- 구버전 저장 파일처럼 09:00 기준 자산이 없는 경우에는 `P&L SINCE LOAD`로 명시해 불러오기 이후 변동을 하루 전체 손익으로 오해하지 않게 했습니다.
- 정산·로딩·게임오버 상태에서는 저장을 거부해 상태가 누락된 저장 파일이 만들어지지 않도록 했습니다.
- 정산 전용 요미 반응은 `DAILY LEDGER` 안에서만 보여주고 기존 메인 말풍선에는 중복 출력하지 않습니다.

### 숍 인터넷 쇼핑몰형 UI 개편

- 기존 팝업형 상점을 돌발 이벤트의 인터넷 기사 UI와 같은 브라우저 계열 디자인으로 교체했습니다.
- 전용 픽셀 스프라이트 `MarketplaceBrowserShell`과 `MarketplaceProductCard`를 제작해 네이비·시안·골드 팔레트와 공통 외곽선 규칙을 적용했습니다.
- 주소 표시줄, `FX MARKET` 헤더, 검색창, 지갑 잔액, 카테고리 탭, 상품 개수, 온라인 상태 푸터를 추가했습니다.
- `ALL ITEMS`, `CARE`, `ACTIVE GEAR` 필터와 이름·ID·설명 검색을 실제 상품 목록에 연결했습니다.
- 상품 카드는 카탈로그 아이템 개수에 맞춰 3열로 동적 생성되며, 아이템 추가·삭제와 검색 결과가 즉시 레이아웃에 반영됩니다.
- 기존 `ShopManager`, `ShopItemButton`, 인벤토리 수량 갱신 및 액티브 아이템 구매·업그레이드 로직은 그대로 유지했습니다.

### 게임오버 최종 세션 리포트 UI

- `GameManager.OnGameOverEvent` 발생 후 가이드 기준 13초 동안 요미의 게임오버 독백 연출을 유지한 뒤 최종 세션 리포트를 표시합니다.
- 게임오버 UI는 `GameScene`의 `TradingViewCanvas`에 런타임으로 자동 설치되며, 비활성 자식 패널만 숨겨 이벤트 구독이 누락되지 않습니다.
- 파산·오버도즈·성공 엔딩별 종료 사유, 엔딩 코드, 최종 순자산, 종료 일시, 멘탈 상태를 표시합니다.
- 파산은 `Tearful`, 오버도즈는 `Obsessive`, 성공은 `Euphoria` 감정 스프라이트와 연결했습니다.
- 정산·스킬·설정·돌발 이벤트보다 높은 Sorting Order와 전체 화면 입력막을 사용해 게임오버 연출 중 다른 UI가 조작되지 않습니다.
- `RETURN TO TITLE` 버튼은 실시간 페이드 아웃 후 `Time.timeScale`을 복원하고 `TitleScene`으로 이동합니다.
- 타이틀 복귀 시 남은 게임오버 LLM 연쇄 대사와 대기열을 정리해 타이틀 화면에 늦은 대사가 출력되지 않도록 했습니다.
- 일반 올인 강제 청산 후에도 총자산을 다시 검사해 `Bankruptcy` 게임오버가 누락되지 않도록 보완했습니다.
- 타이틀에서 게임으로 재진입할 때 기존 `DontDestroyOnLoad` LLM 서비스 때문에 `Loading` 상태에 고착되지 않도록 현재 게임 매니저와 시장 개장을 재확인합니다.

### 소모 아이템 사용 캐릭터 연출

- 에너지 드링크, 파르페(Dessert), 영양제, 진정제 전용 캐릭터 사용 포즈 스프라이트 4종을 제작했습니다.
- 모든 스프라이트를 기존 감정 이미지와 동일한 `600x1180` 투명 캔버스와 머리·발 기준선으로 정렬했습니다.
- 크로마키 제거 후 투명 픽셀과 녹색 테두리 잔상을 검사하고 제거했습니다.
- 인벤토리에서 실제 아이템 차감이 성공했을 때만 약 1초간 사용 포즈가 표시됩니다.
- 아이템 포즈 중 LLM 대사 감정이 도착해도 포즈를 덮어쓰지 않고, 1초 후 가장 최신 감정 스프라이트로 복귀합니다.
- 돌발 선택 이벤트에서 특수 아이템을 소비하는 경우에도 동일한 연출이 재사용됩니다.
- 게임이 일시정지된 상태에서도 `Time.unscaledTime` 기준으로 표시 시간이 정상 종료됩니다.

### 3종 게임 모드 및 스토리 세이브 슬롯 연동

- 타이틀의 `NEW GAME`을 누르면 `STORY`, `ENDLESS`, `CHALLENGE` 3가지 모드를 선택하는 전용 팝업이 열립니다.
- `STORY`는 기존 3개 세이브 슬롯 화면으로 연결되며, 저장 데이터가 있는 슬롯은 이어하기, 빈 슬롯은 새 스토리 시작으로 동작합니다.
- 스토리에서 선택한 슬롯 번호를 세션 동안 유지해 설정창의 저장 버튼이 해당 슬롯에 저장하도록 수정했습니다.
- 기존 1.1.x 이하 세이브에는 게임 모드 필드가 없어도 `Story = 0` 기본값으로 읽히도록 저장 형식을 1.2.0으로 확장했습니다.
- `ENDLESS`는 새 게임으로 시작하며 기존 AI/USER 자동매매 전환을 사용할 수 있습니다.
- `CHALLENGE`는 USER 수동매매로 시작하고 AI/USER 전환 버튼을 잠급니다.
- 챌린지에서는 일반 AI 판단뿐 아니라 오버도즈, 고배율 중독, 비선택형 돌발 이벤트 등 우회 자동매매도 거래 진입부에서 차단합니다.
- 플레이어가 돌발 이벤트에서 직접 롱/숏 방향을 고른 결과는 사용자 입력으로 인정해 그대로 적용됩니다.
- 챌린지의 금지 범위는 현재 `AI 자동매매`이며, 캐릭터 대사 생성용 로컬 LLM과 로딩 준비 과정은 유지합니다.
- 저장 기능은 스토리 전용으로 제한하고, 무한/챌린지 설정창에는 `STORY MODE ONLY`를 표시합니다.

### 게임 시간 연동 3단계 배경

- 기존 밤 트레이딩 룸의 구도와 픽셀 스타일을 유지한 아침·해질녘 배경 2종을 제작했습니다.
- 게임 시각 `06:00~16:59`에는 아침, `17:00~19:59`에는 해질녘, `20:00~05:59`에는 기존 밤 배경을 표시합니다.
- 시간대가 바뀔 때 0.8초 크로스페이드로 전환하며, 게임 일시정지 중에도 전환이 끊기지 않도록 실시간 기준으로 처리합니다.
- 새 게임, 세이브 불러오기, 다음 날 09:00 이동처럼 시간이 즉시 바뀌는 경우에도 현재 시각을 다시 확인해 올바른 배경을 적용합니다.
- 기존 `BackGround`의 위치·비율·UI 정렬 순서는 건드리지 않고 동일 크기(`1672×941`) 스프라이트만 교체합니다.
- 아침과 해질녘 텍스처는 Point 필터, 밉맵 해제, 무압축 Sprite 설정으로 임포트해 픽셀 선명도를 유지합니다.

### 숍 상품 보유 수량 여백 보정

- 상품 카드의 `OWNED x N` 표시를 오른쪽 끝에서 14px 왼쪽으로 이동해 글자가 잘려 보이지 않도록 수정했습니다.
- 동적으로 생성되는 일반 소모품과 액티브 기어 상품 카드 모두 동일한 여백을 적용합니다.

### 소형 HUD 폰트 깨짐 방지

- AI/USER 전환 라벨, 스킬 `LV.n` 배지, 캐릭터 레벨/EXP 텍스트를 고정 크기 렌더링으로 변경했습니다.
- 스킬 배지의 잘못된 Auto Size 범위(`최소 12px / 최대 11px`)를 제거하고 `LV.10`까지 들어가도록 표시 영역을 넓혔습니다.
- PF Stardust Bold 원본 위에 중복 적용되던 합성 Bold를 제거하고 외곽선을 최대 0.05로 낮춰 작은 글자의 뭉개짐을 방지했습니다.
- 빌드에서 동적 폰트 아틀라스가 초기화되더라도 `AI`, `USER`, `LV`, `EXP`, 숫자 글리프를 첫 사용 전에 준비합니다.
- 씬 로딩용 전역 폰트 적용기가 개별 TMP Outline 머티리얼을 덮어쓰지 않도록 수정했습니다.

### 스킬 업그레이드 시간 경과 연출

- 차트 공부, 큐브 풀기, 파산 회고록 읽기에 맞춘 요미 전용 행동 스프라이트 3종을 추가했습니다.
- 기존 감정·아이템 포즈와 같은 `600×1180` 투명 캔버스, 착석 비율, 캐릭터 기준선을 사용합니다.
- 업그레이드 시작 시 요미가 해당 행동 포즈로 전환되고 반투명 시간 경과 화면이 페이드 인됩니다.
- `09:00 → +3시간`으로 소모 시간을 먼저 안내하고, 실제 비용 적용 뒤 `09:00 → 12:00`처럼 변경된 시간을 표시합니다.
- 페이드가 가장 어두운 시점에 기존 자금·HP·게임 시간 소모와 레벨 증가를 실행합니다.
- 연출은 `Time.unscaledDeltaTime`과 `WaitForSecondsRealtime`을 사용해 게임 시간 처리 중에도 끊기지 않습니다.
- 연출 종료 후 최신 감정 스프라이트와 스킬 정보 팝업으로 자연스럽게 복귀합니다.

관련 파일:

- `Assets/Scripts/AI/AIVisualController.cs`
- `Assets/Scripts/UI/ActiveSkillHUDController.cs`
- `Assets/Resources/Characters/SkillUpgrade/ChartStudy.png`
- `Assets/Resources/Characters/SkillUpgrade/CubePatience.png`
- `Assets/Resources/Characters/SkillUpgrade/BookJudgment.png`

### 돌발 이벤트 BREAKING NEWS 사전 등장 연출

- 돌발 이벤트 발생 직후 기사 선택창을 같은 프레임에 갑자기 표시하던 흐름을 개선했습니다.
- 게임 시간을 먼저 정지하고 약 1.6초 동안 `BREAKING NEWS!`, `돌발 이벤트 등장`, 이벤트 카테고리를 전체 화면에 표시합니다.
- 빨간 속보 강조선, 시안 이벤트 안내, 짧은 헤드라인 펄스와 페이드 인·아웃을 적용했습니다.
- 사전 연출이 끝난 뒤 기존 `FX WIRE` 기사와 선택지를 표시합니다.
- `Time.unscaledDeltaTime`을 사용해 이벤트로 게임이 일시정지된 상태에서도 연출이 정상 재생됩니다.
- 이벤트 UI가 중간에 닫히거나 교체될 경우 진행 중인 코루틴과 임시 오버레이를 함께 정리합니다.

관련 파일:

- `Assets/Scripts/Events/ChoiceEventPopupUIController.cs`

### LONG·SHORT 포지션 방향 가시성 통합 연출

- 중앙에 `▲ LONG POSITION OPENED` 또는 `▼ SHORT POSITION OPENED`와 실제 레버리지를 표시합니다.
- 진입 순간 차트에 2px 이하의 짧은 미세 충격을 적용하고 약 1초간 전용 요미 방향 포즈를 표시합니다.
- LONG은 상승 차트를 들고 위를 가리키고, SHORT는 하락 차트를 들고 아래를 가리키는 전용 `600×1180` 투명 스프라이트를 사용합니다.
- 포지션 보유 중 차트 외곽선, 현재가 태그, 방향 화살표와 `LONG/SHORT ×레버리지`를 항상 함께 표시합니다.
- 진입가 위치에는 방향색 점선과 `▲ LONG ENTRY` 또는 `▼ SHORT ENTRY` 태그를 표시합니다.
- 익절 시 포지션 방향과 관계없이 초록색 결과 배너를 표시하고, LONG 픽셀은 위로, SHORT 픽셀은 아래로 흩어집니다.
- 손절 시 포지션 방향과 관계없이 빨간색 결과 배너·화면 플래시와 손실 픽셀 파편을 표시합니다.
- 강제청산 시 빨간 경고 점멸 3회, `LIQUIDATED` 배너와 붉은 픽셀 파편을 표시합니다.
- 모든 방향 구분은 색상뿐 아니라 `▲/▼` 아이콘과 `LONG/SHORT` 텍스트를 함께 사용합니다.
- 모든 전환은 `Time.unscaledDeltaTime`과 실시간 대기를 사용해 일시정지 상태에서도 마무리됩니다.

관련 파일:

- `Assets/Scripts/AI/AIVisualController.cs`
- `Assets/Scripts/UI/Chart/ChartUIController.cs`
- `Assets/Scripts/UI/Chart/TradingPanelUIController.cs`
- `Assets/Resources/Characters/Position/Long.png`
- `Assets/Resources/Characters/Position/Short.png`

### 다음 일차 수면 페이드 전환

- 일일 정산의 `PROCEED TO DAY` 버튼을 누른 직후 다음 날로 즉시 전환되던 흐름을 개선했습니다.
- 정산창에서 검은 수면 화면으로 즉시 전환하고 중앙에 잠든 요미를 표시합니다.
- 베개와 네이비 담요를 사용해 쉬고 있는 전환 전용 요미 스프라이트를 새로 제작했습니다.
- 검은 화면을 약 2.55초 유지한 뒤 완전히 가려진 상태에서 실제 날짜, 차트와 시장을 다음 날 09:00으로 전환합니다.
- 새 아침 화면으로 약 0.45초 페이드 아웃하여 전체 전환 시간이 약 3초가 되도록 구성했습니다.
- `Time.unscaledDeltaTime`과 `WaitForSecondsRealtime`을 사용해 정산 상태에서도 연출이 정상 재생됩니다.
- 전환 중 전체 화면 입력을 차단하여 중복 클릭이나 다른 UI 조작을 방지합니다.

관련 파일:

- `Assets/Scripts/UI/DailySettlementUIController.cs`
- `Assets/Resources/UI/DayTransition/SleepingYomi.png`

### 마진·숍 동적 상태 폰트 영역 이탈 방지

- 마진 비율과 금액 문자열이 자산 증가로 길어질 때 고정 150px 영역 밖으로 나가던 문제를 수정했습니다.
- 마진 표시 폭을 190px 선호 폭과 Flexible Width 구조로 확장하고 `12~22px` Auto Size를 적용했습니다.
- 현재 씬에 이미 생성된 마진 UI에도 런타임에서 Auto Size, 좌우 여백, NoWrap과 Ellipsis를 강제 적용합니다.
- 숍의 `BUY`, `ACTIVE ✓`, `MAX LV ✓` 버튼 라벨에 `11~21px` Auto Size와 내부 안전 여백을 적용했습니다.
- 액티브 장비의 `MAX LV.2 ✓` 같은 보유 상태 라벨에도 별도 Auto Size와 영역 제한을 적용했습니다.
- 동적으로 새로 생성되는 상품 카드와 기존에 생성된 카드 갱신 경로 모두 같은 규칙을 사용합니다.

관련 파일:

- `Assets/Scripts/UI/Chart/TradingPanelUIController.cs`
- `Assets/Editor/TradingViewUIBuilder.cs`
- `Assets/Scripts/Items/DynamicShopUI.cs`
- `Assets/Scripts/Items/ShopItemButton.cs`

### 일차 종료 DAY COMPLETE 사전 연출

- 24:00 도달 직후 일일 정산 결과창이 갑자기 표시되던 흐름을 개선했습니다.
- 결과창 전에 약 1.45초 동안 `DAY 01 COMPLETE`, `오늘 거래 종료 · 24:00`, `DAILY SETTLEMENT READY`를 전체 화면에 표시합니다.
- 돌발 이벤트 `BREAKING NEWS`와 같은 인지용 전환 구조를 사용하되, 일일 마감에 맞춘 시안·골드 색상과 짧은 헤드라인 펄스를 적용했습니다.
- 0.22초 페이드 인, 0.95초 유지, 0.28초 페이드 아웃 후 기존 `DAILY LEDGER` 결과창을 표시합니다.
- Settlement 상태와 입력 차단을 유지하며 `Time.unscaledDeltaTime`으로 일시정지 상태에서도 정상 재생됩니다.
- 컨트롤러 비활성화나 파괴 시 진행 중인 코루틴과 임시 오버레이를 정리합니다.

관련 파일:

- `Assets/Scripts/UI/DailySettlementUIController.cs`

### 매매 모드 AUTO·USER 표기 정리

- 우측 상단 매매 모드 버튼의 `AI` 표기를 `AUTO`로 변경하고 수동 모드는 기존 `USER`를 유지합니다.
- 설정 팝업의 `모드: AI 자동`, `모드: USER 수동`을 각각 `모드: AUTO`, `모드: USER`로 간결하게 변경했습니다.
- 하단 거래 카드의 `AI 자동 매수 대기`, `AI 자동 매도 대기` 문구를 `자동 매수 대기`, `자동 매도 대기`로 변경했습니다.
- 자동 포지션 보유 표기도 `(AI)`에서 `(AUTO)`로 통일했습니다.
- 빌드에서 PF Stardust 동적 아틀라스가 초기화되어도 `AUTO`의 `T`, `O` 글리프가 미리 준비되도록 소형 HUD 글자 집합을 확장했습니다.

관련 파일:

- `Assets/Scripts/UI/SettingsMenuController.cs`
- `Assets/Scripts/UI/Chart/TradingPanelUIController.cs`
- `Assets/Scripts/UI/GlobalPFStardustFont.cs`

### 오버도즈 전용 멘헤라 캐릭터 스프라이트

- 기존 `Manic` 감정 스프라이트를 재사용하던 오버도즈 상태에 별도 전용 요미 스프라이트를 추가했습니다.
- 불안정한 집착 미소, 핑크·보라색 발광 동공, 붉어진 얼굴과 가슴 앞에서 긴장한 양손으로 멘헤라 분위기를 강조했습니다.
- 캐릭터 주변에 네온 마젠타·보라 픽셀 오오라, 깨진 하트와 글리치 파편을 포함했습니다.
- 기존 감정 스프라이트와 같은 `600×1180` 투명 캔버스와 캐릭터 기준선을 사용합니다.
- `TraderStatus.MentalState.Overdose` 상태에서는 일반 감정 판정보다 전용 스프라이트를 우선 표시합니다.
- 오버도즈에서 회복하면 현재 감정 스프라이트로 즉시 복귀합니다.
- 전용 이미지 자체의 오오라와 기존 보조 오오라가 중복되지 않도록 오버도즈 표시 중 보조 오오라를 비활성화합니다.

관련 파일:

- `Assets/Scripts/AI/AIVisualController.cs`
- `Assets/Resources/Characters/States/Overdose.png`
### 요미 바니걸 코스튬 스프라이트

- 기존 `Pleased` 스프라이트를 기준으로 요미의 얼굴, 장발, 아호게, 앉아 있는 기본 자세와 픽셀 아트 스타일을 유지한 바니걸 전신 스프라이트를 제작했다.
- 검정 바니 슈트, 흰 칼라와 커프스, 마젠타 리본, 검정 스타킹 구성으로 디자인했다.
- `Assets/Resources/Characters/Costumes/BunnyGirl.png`에 600x1180 투명 PNG로 추가했으며 Point 필터와 무압축 Sprite 임포트 설정을 적용했다.
### 요미 Standard 비율 리뉴얼 스프라이트

- 바니걸 스프라이트의 길어진 성인 등신 비율을 기준으로 요미의 기본 상태 스프라이트 한 장을 새로 제작했다.
- 기존 요미의 흰 반팔 티셔츠, 흰 파이핑이 들어간 검정 반바지, 맨발과 앉은 자세를 유지했다.
- `Assets/Resources/Characters/States/Standard.png`에 600x1180 투명 PNG로 추가했으며 Point 필터와 무압축 Sprite 임포트 설정을 적용했다.

### 요미 전신 스프라이트 자연 비율 리뉴얼

- 기존 프로젝트 요미의 남청색 머리, 청회색 하이라이트, 금빛 눈, 복숭아색 피부와 픽셀 명암을 기준으로 캐릭터 디자인을 유지했습니다.
- 흰 반팔 티셔츠와 흰 파이핑 검정 돌핀팬츠를 공통 의상으로 사용하고, 자연스럽게 연결되는 상체·골반·하체 비율로 기본형을 교체했습니다.
- 감정 19종, 아이템 사용 4종, 스킬 행동 3종, LONG·SHORT 포지션 2종, 오버도즈 1종을 같은 체형 기준으로 리뉴얼했습니다.
- 팔을 접거나 소품을 드는 변형에서도 상완·전완 길이, 팔꿈치 위치와 손 크기가 급격히 변하지 않도록 보정했습니다.
- 전체 30장을 `600×1180` RGBA 투명 PNG로 정규화했으며 기존 `.meta`를 유지해 Unity GUID와 런타임 리소스 경로를 보존했습니다.
- 게임 화면에서 캐릭터가 지나치게 작게 보이지 않도록 투명 여백을 재조정하고, 각 스프라이트의 실제 표시 높이를 약 `1120px`로 통일했습니다.
- 추가로 적용했던 캐릭터 UI 표시 영역 `1.25배` 확대는 화면 점유율이 과해 롤백하고, 스프라이트 내부 실제 표시 높이 약 `1120px` 확대만 유지했습니다.
- 이후 현재 캐릭터 중심 위치를 유지한 상태에서 표시 영역만 가로·세로 `1.25배` 확대하도록 다시 조정했습니다.
- 하단 고정 방식 대신 기존 앵커 중심을 기준으로 좌우·상하에 대칭 확장해 확대 전후 캐릭터 중심 위치가 움직이지 않습니다.
- 교체 전 원본 PNG와 `.meta`는 `Backups/YomiSprites_PreRenewal_20260725/`에 별도로 보존했습니다.

관련 파일:

- `Assets/Resources/Characters/States/`
- `Assets/Resources/Characters/Emotions/`
- `Assets/Resources/Characters/ItemUse/`
- `Assets/Resources/Characters/SkillUpgrade/`
- `Assets/Resources/Characters/Position/`
- `Backups/YomiSprites_PreRenewal_20260725/`

### 상점 APPAREL 카테고리 및 코스튬 구매·장착

- `FX MARKET`에 `APPAREL` 카테고리를 추가하고 일반 상품과 함께 검색·필터링할 수 있도록 확장했습니다.
- 기본 흰 티셔츠·돌핀팬츠 코스튬은 처음부터 보유한 `STANDARD` 상품으로 표시되며 언제든 다시 장착할 수 있습니다.
- `BUNNY GIRL` 코스튬을 `$200`에 1회 구매할 수 있도록 추가했습니다.
- 의류 카드 버튼은 상태에 따라 `BUY`, `EQUIP`, `EQUIPPED`로 전환되며 구매와 장착을 분리했습니다.
- 장착 시 현재 감정뿐 아니라 감정 19종, 아이템 사용 4종, 스킬 행동 3종, LONG·SHORT와 오버도즈까지 전체 스프라이트 세트가 즉시 전환됩니다.
- 바니걸 스프라이트는 토끼 귀, 검정 바니 슈트, 리본, 양쪽 커프스, 스타킹과 구두를 공통으로 유지하며 `600×1180` RGBA 투명 PNG로 정규화했습니다.
- 구두 외곽선이 캐릭터 표시 영역 하단에 걸려 보이지 않도록 바니걸 변형 29종을 캔버스 안에서 `24px` 상향하고 하단 투명 여백을 `54px`로 확보했습니다.
- 보유 코스튬 ID와 현재 장착 코스튬 ID를 저장 데이터 버전 `1.3.0`에 포함했습니다.
- 구버전 저장 데이터에는 코스튬 필드가 없어도 `STANDARD` 보유·장착 상태로 안전하게 복원됩니다.

관련 파일:

- `Assets/Scripts/Items/CostumeManager.cs`
- `Assets/Scripts/Items/ShopManager.cs`
- `Assets/Scripts/Items/DynamicShopUI.cs`
- `Assets/Scripts/AI/AIVisualController.cs`
- `Assets/Scripts/System/SaveData.cs`
- `Assets/Scripts/System/SaveLoadManager.cs`
- `Assets/Scripts/GameManager.cs`
- `Assets/Resources/Characters/Costumes/BunnyGirl/`

### macOS Apple Silicon LLM 네이티브 런타임 복구

- Play Mode에서 `Library libllamalib_osx-arm64_runtime.dylib not found`로 LLM 서비스가 시작되지 않던 원인을 확인했습니다.
- 기존 `LlamaLib-v2.0.5` 설치에는 Windows x64 네이티브 파일만 있고 macOS ARM64 런타임이 누락되어 있었습니다.
- 공식 undreamai `LlamaLib v2.0.5` 릴리스 ZIP의 SHA-256을 공개 체크섬과 대조한 뒤 `osx-arm64/native` 파일 4개만 프로젝트에 추가했습니다.
- 런타임, CPU 가속, CPU 비가속 동적 라이브러리가 모두 Apple Silicon `arm64` Mach-O 파일임을 확인했습니다.
- macOS에서 `runtime`과 `no-acc` 라이브러리의 `dlopen` 성공 및 CPU 아키텍처 목록 반환을 검증했습니다.

관련 파일:

- `Assets/StreamingAssets/LlamaLib-v2.0.5/osx-arm64/native/libllamalib_osx-arm64_runtime.dylib`
- `Assets/StreamingAssets/LlamaLib-v2.0.5/osx-arm64/native/libllamalib_osx-arm64_acc.dylib`
- `Assets/StreamingAssets/LlamaLib-v2.0.5/osx-arm64/native/libllamalib_osx-arm64_no-acc.dylib`
- `Assets/StreamingAssets/LlamaLib-v2.0.5/osx-arm64/native/libllamalib_osx-arm64_runtime_static.a`

### 요미 블랙 비키니 코스튬

- 요미의 기존 얼굴, 장발, 색상, 체형과 감정·행동별 포즈를 유지한 블랙 비키니 코스튬을 추가했습니다.
- 삼각 홀터 상의와 중앙 금색 링, 양옆 크로스 스트랩 및 금색 링이 들어간 하의를 공통 디자인으로 사용합니다.
- 의류 상점의 `BLACK BIKINI` 상품으로 등록했으며 가격은 `$400`입니다.
- 구매 후 `EQUIP`, 장착 후 `EQUIPPED`로 전환되는 기존 코스튬 구매·장착·저장 흐름을 그대로 사용합니다.

관련 파일:

- `Assets/Scripts/Items/CostumeManager.cs`
- `Assets/Resources/Characters/Costumes/Bikini/`
