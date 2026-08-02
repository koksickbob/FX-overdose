# [FX OVERDOSE] 돌발 선택 이벤트 시스템 필요 아트 에셋 및 UI 정리

본 문서는 `ChoiceEventController` 및 `ChoiceEventPopupUIController`를 통해 구현된 **일일 돌발 선택 이벤트 15종 시스템**의 시각적·청각적 완성도를 극대화하기 위해 필요한 UI 레이아웃, 스프라이트(아트 에셋), 폰트 및 사운드 효과(SFX) 요구사항을 정리한 아트/UI 제작 명세서입니다.

---

## 1. UI 팝업 창 레이아웃 구조 (Hierarchy & Layout)

현재 코드는 씬 내에 UI 프리팹이 연결되지 않은 경우 런타임에 동적으로 고품질 모달 UI를 생성하도록 설계되어 있습니다. 유니티 에디터에서 정식 UI 프리팹(`ChoiceEventPopupPanel.prefab`)을 제작할 경우 다음 하이라키와 명세를 따릅니다.

```
Canvas
 └─ ChoiceEventPopupPanel (Panel / Screen Dim Layer)
     ├─ BackgroundDim (Image - #000000 75% Alpha, 화면 클릭 차단)
     └─ ModalBox (Image - Cyber Subculture Theme Card Box)
         ├─ HeaderBar (Image - Top Accent Border / News Ticker Style)
         │   ├─ AlertIcon (Image - Breaking News / Warning Symbol)
         │   └─ ScenarioTitleText (TextMeshProUGUI - 뉴스 헤드라인 타이틀)
         ├─ ScenarioDescText (TextMeshProUGUI - 경제 상황 및 배경 설명)
         ├─ AIMonologueContainer (Panel - AI 독백 강조 박스)
         │   ├─ AITraderPortrait (Image - AI 트레이더 표정 일러스트 슬롯)
         │   └─ AIMonologueText (TextMeshProUGUI - AI 트레이더의 날것의 혼잣말)
         ├─ OptionButtonContainer (VerticalLayoutGroup)
         │   ├─ OptionButton_A (Button - 🟢 안전 지향 선택지)
         │   ├─ OptionButton_B (Button - 🔴 공격 베팅 선택지)
         │   └─ OptionButton_C (Button - 🟡 특수 아이템 / 직접 지시 선택지)
         └─ ToastWarningText (TextMeshProUGUI - 아이템 부족 등 경고 토스트)
```

---

## 2. 디자인 테마 및 컬러 팔레트 (Color Design Tokens)

상점 UI 및 트레이딩뷰 차트 컬러 가이드(`TradingView Color Design System`)와 일관성을 유지하며, 사이버 서브컬처 및 멘헤라 도트 감성을 극대화합니다.

| UI 요소 / 상태 | HEX 코드 | RGBA 값 (0~1 기준) | 용도 및 시각적 특징 |
| :--- | :---: | :---: | :--- |
| **모달 배경 (Card BG)** | `#0F172A` | `(0.059, 0.090, 0.165, 0.95)` | 짙은 네이비/슬레이트 톤의 반투명 팝업 배경 |
| **헤더 바 / 포인트** | `#06B6D4` | `(0.024, 0.714, 0.831, 1.0)` | 네온 사이버 시안 (AI 트레이더 아이덴티티) |
| **안전 버튼 (Option A)** | `#22C55E` | `(0.133, 0.773, 0.369, 1.0)` | TradingView Bullish Mint (안전 관망/리스크 회피) |
| **공격 버튼 (Option B)** | `#EF4444` | `(0.937, 0.267, 0.267, 1.0)` | TradingView Bearish Coral (고배율 올인/위험 감수) |
| **특수 버튼 (Option C)** | `#EAB308` | `(0.918, 0.702, 0.031, 1.0)` | Cyber Amber / Gold (아이템 소비 및 플레이어 직접 개입) |
| **AI 독백 텍스트** | `#CFFAFE` | `(0.812, 0.980, 0.996, 1.0)` | 밝은 시안 톤으로 독백 대사 시인성 확보 |
| **토스트 경고 (Error)** | `#FF4D4D` | `(1.000, 0.302, 0.302, 1.0)` | 특수 아이템 부족 또는 실행 불가 시 깜빡임 경고 |

---

## 3. 필요 2D 아트 에셋 리스트 (Sprites & Icons)

모두 도트 픽셀 아트(`Point (No Filter)`, `Sprite (2D and UI)`) 기준으로 제작하며, 9-Slicing을 위해 테두리 여백을 명확히 설정합니다.

### ① UI 프레임 및 버튼 픽셀 스프라이트
1. `UI_PopupModal_Frame.png` (9-Slice 가능 모달 외곽선 프레임, 글래스모피즘 또는 픽셀 보더 적용)
2. `UI_HeaderBar_News.png` (상단 `[BREAKING NEWS]` 전용 강조 배너 바)
3. `UI_OptionBtn_Safe.png` (녹색 계열 픽셀 버튼 프레임 - Normal / Pressed / Disabled 3상태)
4. `UI_OptionBtn_Aggressive.png` (적색 계열 픽셀 버튼 프레임 - Normal / Pressed / Disabled 3상태)
5. `UI_OptionBtn_Special.png` (황금색/네온 시안 계열 특수 버튼 프레임 - 빛나는 픽셀 애니메이션 효과 추천)

### ② AI 트레이더 멘탈 상태별 초상화/일러스트 (Portrait Sprites)
이벤트 발생 시 AI 트레이더의 현재 감정 상태 및 시나리오 분위기를 보여주기 위해 독백 박스 옆에 표시되는 일러스트입니다.
1. `AI_Portrait_Stable.png` (냉철하고 차분하게 분석하는 모습)
2. `AI_Portrait_Anxious.png` (눈동자가 흔들리고 식은땀을 흘리며 초조해하는 모습)
3. `AI_Portrait_Danger.png` (충혈된 눈으로 광기에 차서 모니터를 노려보는 모습)
4. `AI_Portrait_Overdose.png` (오버도즈 각성 - 눈에서 사이버 오로라가 흐르거나 완전히 넋이 나간 상태)
5. `AI_Portrait_Dependency.png` (시나리오 11/12 전용 - 화면 밖 플레이어를 향해 간절히 애원하는 표정)

### ③ 이벤트 카테고리 아이콘 (Category Icons - 32x32 px)
* `Icon_BreakingNews.png` (신문 기사 또는 텔레그램 메신저 알림 아이콘)
* `Icon_FSC_Warning.png` (금융감시국 규제 및 법원 마크)
* `Icon_WhaleAlert.png` (고래 꼬리 또는 대형 지갑 이동 마크)
* `Icon_WCB_Rate.png` (중앙은행 금리 인상 의사봉 아이콘)
* `Icon_Singularity.png` (양자 컴퓨터 / 신경망 오버도즈 각성 아이콘)

---

## 4. 필요 사운드 효과 (SFX Audio Assets)

돌발 선택 이벤트 팝업이 뜰 때는 플레이어의 몰입을 극대화하기 위해 인게임 시간 정지와 함께 강렬한 사운드가 연출되어야 합니다.

| 사운드 파일명 (`.wav` / `.mp3`) | 재생 시점 및 연출 설명 |
| :--- | :--- |
| `SFX_Event_Trigger_News.wav` | 일반 뉴스/시간대 이벤트 트리거 시 (긴급 뉴스 속보 타지기 소리 + 알람 딩동) |
| `SFX_Event_Trigger_Crisis.wav` | AI 멘탈 위기(`LowMental`) 및 오버도즈 이벤트 발생 시 (글리치 효과음 + 심장 박동 + 삐- 경고음) |
| `SFX_Option_Click_Safe.wav` | A(안전) 선택지 클릭 및 시간 재개 시 (부드럽고 안정적인 클릭음 / 확인음) |
| `SFX_Option_Click_Aggressive.wav` | B(공격) 선택지 클릭 시 (강렬한 베팅 철칵 소리 + 차트 폭등/폭락 예고음) |
| `SFX_Option_Click_Special.wav` | C(특수/아이템) 선택지 클릭 시 (아이템 사용 효과음 + 마법/시스템 가동 신비로운 사운드) |
| `SFX_Option_Error_NoItem.wav` | 아이템 부족으로 선택 불가 토스트 출력 시 (낮은 톤의 둔탁한 버저음) |

---

## 5. UI 적용 및 연동 안내 (Developer Guide for Art Team)

1. **폰트 적용**: 프로젝트 전역 픽셀 폰트(`PFStardust.ttf`)를 `TextMeshProUGUI` 에셋(`PFStardust SDF`)으로 변환하여 모든 이벤트 팝업 텍스트에 지정합니다.
2. **동적 UI vs 프리팹 UI 선택**:
   - 코드는 씬 내에 `ChoiceEventController` 스크립트만 붙어있으면 런타임에 **완전 자동(Auto-Build)**으로 위 레이아웃을 그려냅니다.
   - 씬에 `ChoiceEventPopupPanel` 프리팹을 배치하고 `ChoiceEventPopupUIController`의 Inspector 슬롯에 각 텍스트/버튼을 수동 연결해두면, 코드는 동적 생성을 생략하고 **해당 프리팹 UI를 우선적으로 사용하여 애니메이션과 커스텀 스프라이트를 100% 반영**합니다.
