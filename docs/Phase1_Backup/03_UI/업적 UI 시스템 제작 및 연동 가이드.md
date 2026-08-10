# 업적 UI 시스템 제작 및 연동 가이드

이 문서는 UI 디자이너가 업적 UI를 Unity 에디터에서 조립하고 시스템과 연동하기 위한 가이드입니다.

## 1. 필요한 프리팹 및 UI 요소

업적 UI는 크게 **① 팝업 패널 전체(Controller)** 와 **② 리스트 내부의 개별 항목(Item)** 두 가지로 구성됩니다.

### 1-1. AchievementItem 프리팹 제작
업적 리스트에 들어갈 개별 항목(1개의 업적) UI입니다.

1. `Canvas` 상에 UI 요소를 디자인하여 빈 게임 오브젝트 혹은 Image로 묶습니다.
2. 해당 최상위 오브젝트에 `AchievementItemUI` 스크립트를 컴포넌트로 추가합니다.
3. 인스펙터 창에서 다음 참조들을 연결합니다:
   - `Title Text`: 업적 이름이 표시될 TextMeshPro 컴포넌트
   - `Desc Text`: 업적 달성 방법(설명)이 표시될 TextMeshPro 컴포넌트
   - `Reward Text`: "보상: OOO 스킨" 형태로 표시될 TextMeshPro 컴포넌트
   - `Locked Overlay`: 미달성 상태일 때 위에 덮어씌울 UI(자물쇠 아이콘, 반투명 검은 배경 등) 오브젝트
4. 디자인이 완료되면 이 오브젝트를 Project 탭으로 드래그하여 프리팹(`AchievementItem.prefab`)으로 만듭니다.

### 1-2. Achievement UI 팝업 창 (전체 패널)
리스트 항목들을 스크롤해서 볼 수 있는 전체 팝업 창입니다. 타이틀 씬과 인게임 씬 양쪽에서 접근 가능해야 하므로 프리팹으로 만들어 양쪽 캔버스(또는 글로벌 캔버스)에 배치하는 것을 권장합니다.

1. 업적 창 UI(배경, 제목, X 닫기 버튼 등)를 디자인합니다.
2. 내부에 Unity UI의 `Scroll View`를 생성합니다.
3. `Scroll View` 내부의 `Content` 오브젝트에 `Vertical Layout Group`과 `Content Size Fitter` (Vertical Fit: Preferred Size) 컴포넌트를 추가합니다.
4. 최상위 팝업 창 오브젝트(또는 컨트롤러 역할을 할 오브젝트)에 `AchievementUIController` 스크립트를 추가합니다.
5. 인스펙터 창에서 다음 참조들을 연결합니다:
   - `Overlay`: 팝업 창이 열릴 때 활성화될 최상위 패널 (본인 자신이어도 무방함)
   - `Content Panel`: 방금 설정한 Scroll View 내부의 `Content` 트랜스폼
   - `Item Prefab`: 1-1에서 제작한 `AchievementItem` 프리팹
   - `Close Button`: 창 닫기 버튼(`X` 버튼 또는 `닫기` 텍스트 버튼)
6. 팝업 창을 닫힌 상태(비활성화)로 둡니다.
7. 프리팹화하여 `TitleScene` 캔버스와 `GameScene` 캔버스(또는 런타임 캔버스 로딩 로직)에 배치합니다.

---

## 2. 진입점 (Entry Points) 확인 및 유의사항

코드 단에서 다음 2곳에서 업적 팝업을 열 수 있도록 기능이 연결되어 있습니다.

### 타이틀 씬
- 화면 우측 하단에 생성되는 `Btn_Achievements` (코드명) 버튼을 클릭 시 팝업이 띄워집니다.
- 버튼 자체는 `TitleScreenBuilder.cs`가 자동 생성하지만, 팝업(`AchievementUIController`)은 씬에 하나 존재해야(프리팹 배치) 이 버튼이 눌렸을 때 자동으로 찾아서 열어줍니다.

### 게임 내 환경설정 창
- 게임을 일시정지하고 여는 환경설정(`SETTINGS`) 팝업 내부에 `ACHIEVEMENTS` 버튼이 추가되었습니다.
- 해당 버튼 클릭 시, 마찬가지로 현재 활성화된 씬 내부의 `AchievementUIController`를 찾아 팝업을 엽니다.

## 3. 테스트 방법
- 인게임 혹은 타이틀 화면에서 버튼을 눌러 리스트가 잘 스크롤 되는지 확인합니다.
- 달성되지 않은 업적은 이름과 보상이 `???` 로 표기되고 `Locked Overlay`가 켜지는지 확인합니다.
- `AchievementManager` 콤포넌트의 컨텍스트 메뉴(기어 아이콘 클릭)나 치트 버튼을 통해 임의로 업적을 달성한 후 다시 창을 열어 정상적으로 정보가 표출되는지 테스트합니다.
