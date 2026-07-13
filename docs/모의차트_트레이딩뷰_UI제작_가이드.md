# [FX OVERDOSE] 모의 차트 및 트레이딩뷰 UI 제작 & 자동 조립 가이드

본 매뉴얼은 FX OVERDOSE의 24시간 비트코인 모의 차트 및 트레이딩 뷰 화면을 유니티 에디터(`Canvas`) 상에서 제작 및 조립하기 위한 공식 가이드 문서입니다.

수많은 UI 오브젝트와 인스펙터 슬롯을 일일이 수동으로 조립하는 과정에서 발생하는 실수를 방지하기 위해, **원클릭 자동 조립 스크립트(`Assets/Editor/TradingViewUIBuilder.cs`)**를 통한 자동 조립 방법과 수동 검증용 Hierarchy 명세를 함께 제공합니다.

---

## 1. 원클릭 UI 자동 조립 (One-Click Automated UI Builder)

### 1.1. 실행 방법
1. 유니티 상단 메뉴 바에서 **`Tools > FX OVERDOSE > Build Trading Chart UI`** 항목을 클릭합니다.
2. 유니티 에디터 콘솔 창에 `[FX OVERDOSE] 🚀 신규 차트 전용 캔버스 (TradingViewCanvas) 및 3대 컨트롤러 UI 조립 완료!` 메시지가 출력되면 전체 조립이 끝납니다.

### 1.2. 자동 수행 작업 요약
* **기존 캔버스 정리:** 씬 내에 기존 `TradingViewCanvas` 오브젝트가 존재할 경우, 중복이나 충돌 방지를 위해 깨끗하게 제거한 뒤 최신 사양으로 신규 생성합니다.
* **메인 카메라 뷰 연동 (ScreenSpaceCamera) 및 1920x1080 규격 설정:** 씬의 `Main Camera`를 자동 감지하여 `ScreenSpaceCamera` 모드로 연결(`planeDistance = 10f`)함으로써 메인 카메라 뷰에 정확히 맞춰지도록 하며, Canvas Scaler(`Scale With Screen Size`, Reference: `1920x1080`, Match: `0.5`)를 설정합니다. (메인 카메라 부재 시 `Overlay` 모드로 자동 fallback)
* **좌측 절반(Left Half 50%) 전용 루트 컨테이너 생성:** 트레이딩 뷰 화면이 화면 전체를 덮거나 카메라 뷰 밖으로 벗어나지 않도록, 메인 카메라 뷰의 좌측 50%(`anchorMin = 0.0, 0.0`, `anchorMax = 0.5, 1.0`, 너비 `960px`)에 정확히 안착되는 `LeftHalfTradingContainer`를 생성합니다.
* **풀링 Prefab 자동 생성 및 리소스 저장:**
  * `Assets/Prefabs/UI/CandleItemUI.prefab` (몸통, 윗꼬리, 아랫꼬리, 거래량 바 및 `CandleItemUI` 스크립트)
  * `Assets/Prefabs/UI/LineSegmentUI.prefab` (스파크라인 선분 이미지 및 피벗/앵커 `0.5, 0.5`)
* **좌측 절반(960px) 최적화 3대 패널 생성 및 컨트롤러 인스펙터 바인딩:**
  * 상단 상태 바(`TopStatusBarPanel`) ↔ `TopStatusBarUIController`
  * 중앙 차트 영역(`ChartMainPanel`) ↔ `ChartUIController`
  * 하단 매매 조작부(`BottomTradingPanel`) ↔ `TradingPanelUIController`

---

## 2. 생성되는 UI Hierarchy 계층 구조 명세

자동 빌더 실행 시 아래와 같이 메인 카메라 뷰 좌측 절반(50%)에 안착되는 완벽한 계층 구조가 조립됩니다:

```text
TradingViewCanvas (Canvas: ScreenSpaceCamera / Camera.main 연동, CanvasScaler, GraphicRaycaster)
 └─ LeftHalfTradingContainer (Anchor: 0.0~0.5 Width / 960x1080 좌측 절반 전용 컨테이너)
     ├─ TopStatusBarPanel (Height: 100px / Top Anchor / TopStatusBarUIController)
     │   ├─ DayTimeCard (DayLabel: DAY 03, TimeLabel: 🕒 23:47)
     │   ├─ BalanceCard (Title, BalanceValue: $12,458.36)
     │   └─ PnLCard (Title, PnLPct: +18.47%, PnLAmt: +$1,458.36)
     │       └─ SparklineContainer (Width: 140px, Height: 50px / SparklineRenderer)
     │
     ├─ ChartMainPanel (Stretch Anchor / ChartUIController)
     │   ├─ TimeframeBar (Height: 44px / Btn1m, Btn5m, Btn15m, Btn1h, Btn4h, BtnD1)
     │   │   └─ HeaderPriceBox (PriceHeaderLabel: 67,842.1, PriceChangeLabel: +0.00%)
     │   ├─ ChartArea (Stretch Container)
     │   │   ├─ CurrentPriceLine (Cyan Dotted Line #06B6D4)
     │   │   │   └─ PriceTag (PriceTagText: 67,842.1)
     │   │   └─ XLabel_0 ~ 5 (하단 시간 눈금)
     │   └─ YAxisContainer (YLabel_0 ~ 5 우측 가격 눈금)
     │
     └─ BottomTradingPanel (Bottom Anchor / Height: 35% / TradingPanelUIController)
         ├─ ControlHeaderBar (BtnTabLeverageMode, BtnTabMarginRatioMode)
         ├─ Container_LeverageMode (BtnLevMinus, LevDisplay: 10x, BtnLevPlus, Btn1x~125x)
         ├─ Container_MarginRatioMode (BtnMarMinus, MarDisplay: 30%, BtnMarPlus, BtnMar10~100)
         ├─ ActionButtonsBar (LongButton #22C55E, ShortButton #EF4444, ClosePositionButton #EAB308)
         └─ PositionStatusPanel (ROE Text, PnL Text, 진입가, 청산가 실시간 오버레이)
```

---

## 3. UI 테마 및 색상 토큰 가이드 (TradingView Color Design System)

* **배경 및 패널 (Dark Deep System):**
  * 메인 캔버스 / 차트 배경: `#0B0F19` (R:11, G:15, B:25)
  * 상단 HUD / 하단 매매 패널: `#0F172A` (R:15, G:23, B:42)
  * 버튼 헤더 / 타임프레임 바: `#141D33` (R:20, G:29, B:51)
* **상승 / 양봉 / 매수 (Bullish & Long):**
  * 메인 하이라이트: `#22C55E` (R:34, G:197, B:94)
  * 거래량 바 반투명: `RGBA(34, 197, 94, 0.6)`
* **하락 / 음봉 / 매도 (Bearish & Short):**
  * 메인 하이라이트: `#EF4444` (R:239, G:68, B:68)
  * 거래량 바 반투명: `RGBA(239, 68, 68, 0.6)`
* **선택 하이라이트 및 현재가 라인 (Cyan Accent):**
  * 시그니처 시안: `#06B6D4` (R:6, G:182, B:212)

---

## 4. 인스펙터 바인딩 검증 체크리스트

자동 생성 완료 후 인스펙터에서 아래 항목들이 정상적으로 연결되어 있는지 최종 확인합니다:

1. **`TopStatusBarPanel` (TopStatusBarUIController):**
   * `dayLabel`, `timeLabel`, `balanceValueLabel`, `pnlPercentageLabel`, `pnlAmountLabel`, `sparklineRenderer` 슬롯이 모두 채워져 있는지 확인.
2. **`ChartMainPanel` (ChartUIController):**
   * `priceHeaderLabel`, `priceChangeLabel`, `btn1m` ~ `btn1D`, `chartAreaTransform`, `candlePrefab`(`CandleItemUI.prefab`) 슬롯 정상 바인딩 여부 확인.
3. **`BottomTradingPanel` (TradingPanelUIController):**
   * `btnTabLeverageMode`, `btnTabMarginRatioMode`, `leverageControlContainer`, `marginRatioControlContainer` 슬롯 정상 연결 확인.
   * `longButton`, `shortButton`, `closePositionButton` 및 실시간 ROE/PnL 오버레이 텍스트 슬롯 연결 확인.

이상이 없으면 인게임 플레이 버튼(`Play`)을 눌러 실시간 비트코인 4중 확률 시뮬레이션 차트와 레버리지/비율 매매 시스템을 즉시 경험할 수 있습니다!
