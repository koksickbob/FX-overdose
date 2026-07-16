# FX Overdose - 메인화면 및 로딩화면 UI 제작 및 조립 가이드

본 문서는 **FX Overdose** 프로젝트의 게임 진입점인 **메인 화면(Main Menu)**과 **로딩 화면(Loading Screen)**의 UI 제작 및 조립을 위한 가이드입니다. 
각 화면의 권장 계층 구조(Hierarchy), 에셋 제작 요구사항, 그리고 C# 컨트롤러와 인스펙터 상에서 연결해야 할 필드 및 이벤트 명세표를 제공합니다.

---

## 🎮 [파트 1] 메인 메뉴 화면 (`MainMenuController.cs`)

메인 메뉴는 게임을 처음 실행했을 때 플레이어가 마주하는 타이틀 화면입니다. 새 게임 시작, 불러오기, 환경설정, 게임 종료 기능을 제공합니다.

### 1. 권장 Hierarchy 뼈대 구조
```text
Canvas_MainMenu
 ├── BackgroundImage (타이틀 배경 이미지 또는 애니메이션)
 ├── TitleLogo (게임 타이틀 로고 이미지 "FX OVERDOSE")
 ├── MainMenuButtonsArea (버튼 그룹 컨테이너 - Vertical Layout Group)
 │    ├── Btn_NewGame ("새 게임 시작" 버튼)
 │    ├── Btn_LoadGame ("불러오기" 버튼)
 │    ├── Btn_Settings ("설정" 버튼)
 │    └── Btn_QuitGame ("게임 종료" 버튼)
 ├── PopupPanels (팝업 패널 묶음)
 │    ├── LoadGamePanel (불러오기 팝업 - 기본 비활성화)
 │    │    └── 슬롯 및 닫기 버튼 등
 │    └── SettingsPanel (환경설정 팝업 - 기본 비활성화)
 │         └── 볼륨 슬라이더 및 닫기 버튼 등
```

### 2. 인스펙터 바인딩 명세 (`[SerializeField]`)
스크립트 경로: `Assets/Scripts/UI/MainMenuController.cs`

| 인스펙터 필드명 | UI 타입 | 바인딩 대상 및 설명 |
| :--- | :--- | :--- |
| `btnNewGame` | `Button` | **새 게임 시작** 버튼. 클릭 시 기존 데이터를 초기화하고 로딩 화면으로 진입합니다. |
| `btnLoadGame` | `Button` | **불러오기** 팝업 오픈 버튼. 클릭 시 `loadGamePanel`을 활성화합니다. |
| `btnSettings` | `Button` | **설정** 팝업 오픈 버튼. 클릭 시 `settingsPanel`을 활성화합니다. |
| `btnQuitGame` | `Button` | **게임 종료** 버튼. 데스크톱 환경에서 게임을 종료합니다. |
| `loadGamePanel` | `GameObject` | **불러오기 팝업 UI 패널**. 게임 시작 시 자동으로 비활성화 처리됩니다. |
| `settingsPanel` | `GameObject` | **환경설정 팝업 UI 패널**. 게임 시작 시 자동으로 비활성화 처리됩니다. |

### 3. 작업자 체크포인트
- `LoadGamePanel` 및 `SettingsPanel` 내부에 닫기(Close) 버튼과 관련된 UI 스크립트는 이 가이드 범위를 벗어나며, 개별 팝업 스크립트 또는 Unity 이벤트 리스너를 통해 자신의 `gameObject.SetActive(false)`를 호출하도록 연결해야 합니다.
- `btnLoadGame`을 통한 세이브 데이터 호출 UI(슬롯 목록 등)는 추후 `SaveLoadManager`와 바인딩되어야 하므로 디자인 영역(슬롯 프리팹)을 넉넉히 잡아주세요.

---

## 💾 [파트 1-1] 불러오기 팝업 (`LoadGamePanel`) 내부 구조

세이브 데이터를 선택하여 게임을 이어서 시작할 수 있는 팝업입니다. `SaveLoadManager`와 연동될 수 있도록 슬롯 버튼들을 구성해야 합니다.

### 권장 Hierarchy 구조
```text
LoadGamePanel (Image - 팝업 배경 반투명 패널)
 ├── ModalWindow (실제 팝업 창 영역)
 │    ├── Txt_Title ("게임 불러오기")
 │    ├── ScrollView_Slots (세이브 슬롯 목록 스크롤 뷰)
 │    │    └── Viewport
 │    │         └── Content (Vertical Layout Group)
 │    │              ├── SaveSlotPrefab_1 (버튼)
 │    │              │    ├── Txt_SlotName ("Save 1")
 │    │              │    ├── Txt_PlayTime ("플레이 시간: 02:45")
 │    │              │    └── Txt_Date ("2023.10.27 15:30")
 │    │              ├── SaveSlotPrefab_2
 │    │              └── SaveSlotPrefab_3
 │    └── Btn_Close ("닫기" 버튼)
```

### UI 조립 가이드
1. **슬롯 프리팹 (SaveSlotPrefab)**: 세이브 데이터 슬롯 하나를 프리팹으로 만들어 둡니다. 각 슬롯 버튼의 `onClick` 이벤트에는 추후 프로그래머가 `SaveLoadManager.Instance.PrepareLoadGame(slotIndex)`를 연동할 예정이므로, 버튼의 형태만 우선 조립해둡니다.
2. **닫기 버튼**: `Btn_Close`의 `onClick` 이벤트에 `LoadGamePanel` 게임오브젝트를 드래그 앤 드롭한 뒤, `GameObject.SetActive(false)`를 호출하도록 설정합니다.

---

## ⚙️ [파트 1-2] 환경설정 팝업 (`SettingsPanel`) 내부 구조

게임의 오디오 볼륨을 조절하거나 그래픽 설정을 변경하는 팝업입니다. 메인 메뉴용과 게임 플레이 중의 설정 팝업 모양이 유사하게 구성됩니다.

### 권장 Hierarchy 구조
```text
SettingsPanel (Image - 팝업 배경 반투명 패널)
 ├── ModalWindow (실제 팝업 창 영역)
 │    ├── Txt_Title ("환경 설정")
 │    ├── AudioSettingsArea (오디오 설정 영역 - Vertical Layout Group)
 │    │    ├── BGMVolumeGroup
 │    │    │    ├── Txt_Label ("배경음악 (BGM)")
 │    │    │    └── Slider_BGM (UI Slider)
 │    │    └── SFXVolumeGroup
 │    │         ├── Txt_Label ("효과음 (SFX)")
 │    │         └── Slider_SFX (UI Slider)
 │    └── Btn_Close ("닫기" 버튼)
```

### UI 조립 가이드
1. **슬라이더 (Slider)**: BGM 및 SFX 슬라이더의 `MinValue`는 `0`, `MaxValue`는 `1`로 설정합니다.
2. **볼륨 조절 이벤트 연결**: 슬라이더의 `OnValueChanged(Single)` (동적 Float) 이벤트 슬롯을 추가하여 추후 오디오 컨트롤러 스크립트의 `SetBGMVolume`과 `SetSFXVolume` 메서드가 연결될 수 있도록 비워둡니다.
3. **닫기 버튼**: `Btn_Close`의 `onClick` 이벤트에 `SettingsPanel` 게임오브젝트를 드래그 앤 드롭한 뒤, `GameObject.SetActive(false)`를 호출하도록 설정합니다.

---

## ⏸️ [파트 1-3] 인게임 일시정지 및 설정 메뉴 (`SettingsOverlay`)

게임 플레이 중 우측 상단의 설정 버튼을 누르면 나타나는 동적 생성 UI입니다. 이 메뉴에는 게임 저장 기능이 포함되어 있습니다.

### UI 조립 및 구현 방식 안내
인게임 `SettingsMenuController`는 메인 메뉴와 달리 캔버스에 미리 그려두는 방식 대신 **스크립트에서 런타임에 UI를 동적으로 생성(`CreateUIObject`)**하는 방식을 사용합니다. 
따라서 별도의 프리팹 조립은 필요하지 않으나, 런타임 시 렌더링되는 계층 구조는 다음과 같습니다.

```text
SettingsOverlay (Canvas - 런타임 자동 생성)
 ├── SettingsPanel (Image - 어두운 팝업 배경)
 │    └── InnerFrame (Image - 테두리 프레임)
 │         ├── SettingsTitle ("SETTINGS")
 │         ├── PausedLabel ("GAME PAUSED")
 │         ├── ModeSwitchButton (AI/수동 매매 모드 토글 버튼)
 │         ├── SaveButton ("SAVE GAME" - 클릭 시 현재 상태를 저장)
 │         ├── ResumeButton ("CONTINUE" - 일시정지 해제 및 게임 재개)
 │         └── QuitButton ("QUIT GAME" - 게임 종료)
```

### 기능 연동 체크포인트
1. **SaveGame 기능 연동**: 런타임 생성된 SaveButton이 클릭되면 `SettingsMenuController`의 `SaveGame()` 메서드가 실행되며, 내부적으로 `SaveLoadManager.Instance.SaveGame(0)`을 호출하여 현재 진행도를 기본 슬롯(0번)에 저장합니다. 저장 성공 시 버튼 텍스트가 2초간 "SAVED!"로 바뀌며 시각적 피드백을 제공합니다.
2. **동적 버튼의 디자인 수정**: 런타임 팝업 내부의 버튼 색상(Color), 텍스트 폰트 크기, 레이아웃 등은 Unity 인스펙터가 아닌 `SettingsMenuController.cs` 내부의 `BuildMenu()` 메서드 코드에서 위치 및 컬러 변수 값을 직접 수정하여 디자인을 제어해야 합니다.

---

## ⏳ [파트 2] 로딩 화면 (`LoadingScreenController.cs`)

씬과 씬 사이(메인 메뉴 ➔ 게임 씬)를 전환할 때 데이터 로딩을 대기하고 시각적 피드백을 제공하는 화면입니다.

### 1. 권장 Hierarchy 뼈대 구조
```text
Canvas_LoadingScreen
 ├── BackgroundImage (로딩 일러스트 및 배경)
 ├── LoadingIndicatorArea (하단 로딩 상태 표시 영역)
 │    ├── Txt_Loading ("Loading..." 텍스트)
 │    ├── ProgressBar (로딩 진행도를 나타내는 슬라이더 - UI Slider)
 │    │    ├── Background
 │    │    └── Fill Area 
 │    │         └── Fill
 │    └── LoadingCharacter (뛰어가는 주인공 SD 캐릭터 등 애니메이션 요소)
```

### 2. 인스펙터 바인딩 명세 (`[SerializeField]`)
스크립트 경로: `Assets/Scripts/UI/LoadingScreenController.cs`

| 인스펙터 필드명 | UI 타입 | 바인딩 대상 및 설명 |
| :--- | :--- | :--- |
| `progressBar` | `Slider` | 비동기 씬 로드 진행도(`0.0` ~ `1.0`)를 시각적으로 보여줄 UI 슬라이더입니다. |
| `loadingAnimator` | `Animator` | 화면 전체 페이드 효과, 혹은 캐릭터 로딩 애니메이션을 제어할 애니메이터 컴포넌트입니다. (옵션) |
| `minimumLoadingTime` | `float` | **최소 로딩 대기 시간 (초)**. 로딩이 너무 순식간에 끝나는 것을 방지하기 위한 페이크 로딩 대기 시간입니다. (기본값: `2.0f`) |

### 3. 애니메이터 (Animator) 트리거 명세
`loadingAnimator`에 연결된 애니메이터 컨트롤러(`Animator Controller`)는 다음 두 가지 트리거 파라미터(`Trigger`)를 설정해야 합니다.
1. **`StartLoad`**: 로딩 화면 시작 직후 호출됩니다. (예: 페이드 인 애니메이션)
2. **`FinishLoad`**: 로딩이 `90%` 이상 완료되고 `minimumLoadingTime`을 채웠을 때 호출됩니다. (예: 페이드 아웃 애니메이션)
> ⚠️ **주의**: `FinishLoad` 호출 이후 약 0.5초 뒤에 씬이 즉시 전환되므로 페이드 아웃 애니메이션 클립의 길이는 가급적 0.5초 이내로 맞추거나 스크립트의 대기 시간을 조정해야 합니다.

---

## 📝 UI 조립 담당자 종합 체크리스트
- [ ] `MainMenuController`의 메인 버튼 4종(`NewGame`, `LoadGame`, `Settings`, `QuitGame`) Inspector 연결 확인
- [ ] 메인 메뉴의 팝업 패널 2종(`loadGamePanel`, `settingsPanel`) Inspector 연결 확인
- [ ] `LoadingScreenController`의 `progressBar`(Slider 컴포넌트) 연결 여부 확인
- [ ] 로딩 화면에 Animator를 사용할 경우 `StartLoad`, `FinishLoad` 파라미터(Trigger 타입)가 제대로 설정되었는지 확인
