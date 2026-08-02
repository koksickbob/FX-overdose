# 게임 오버 UI 조립 가이드

이 문서는 UI 제작자분께서 게임 오버 화면 시스템을 유니티 씬에 원활하게 조립하고 연동할 수 있도록 안내하는 가이드입니다.

---

## 1. UI 구조 준비

먼저 `GameScene` (또는 게임이 진행되는 씬)의 메인 캔버스(`Canvas`) 아래에 게임 오버 화면으로 사용할 **Panel**을 하나 생성합니다. 이 패널은 게임 오버 연출이 나오기 전까지는 숨겨져 있어야 하므로 **비활성화(Inactive)** 상태로 둡니다.

```text
Canvas (또는 적절한 UI 루트)
 └─ GameOverUI_Panel (이름 자유, 상태: 비활성화)
     ├─ Background (검은색 반투명 등)
     ├─ TitleText ("GAME OVER")
     ├─ ReasonText (게임 오버 사유가 출력될 TextMeshPro)
     └─ ReturnTitle_Button (타이틀로 돌아가기 버튼)
```

## 2. 스크립트 부착 및 설정

1. 씬에 있는 적절한 매니저 오브젝트(예: `UIManager` 또는 `GameManager`)에 **`GameOverUIController.cs`** 스크립트를 드래그 앤 드롭하여 추가합니다. (또는 `GameOverUI_Panel` 자체에 붙여도 무방합니다.)
2. 인스펙터(Inspector) 창에서 아래 항목들을 연결합니다.

- **Game Over Panel**: 방금 만든 `GameOverUI_Panel` 오브젝트를 드래그해서 넣습니다.
- **Reason Text**: 사유가 출력될 `ReasonText` (TextMeshProUGUI 컴포넌트) 오브젝트를 드래그해서 넣습니다.
- **Display Delay**: 기본값은 `13`입니다. (게임 오버 시 캐릭터가 미쳐가는 독백 대사가 대략 26초 정도 진행되는데, 그 **절반 시점**에 화면이 전환되도록 맞춘 값입니다.)

## 3. 버튼 이벤트 연결

1. 타이틀로 돌아가는 `ReturnTitle_Button`을 선택합니다.
2. 인스펙터의 `Button (Script)` 컴포넌트에서 **On Click ()** 리스트의 `+` 버튼을 누릅니다.
3. `GameOverUIController` 스크립트가 붙어있는 오브젝트를 타겟 빈칸에 드래그 앤 드롭합니다.
4. 우측의 Function 드롭다운을 열어 **`GameOverUIController -> ReturnToTitle()`** 메서드를 선택합니다.

## 4. 작동 방식 참고

- 게임 내에서 파산(Bankruptcy) 또는 도파민 과다(Overdose) 등으로 `GameManager.EndGame()`이 호출되면, `OnGameOverEvent`가 발생합니다.
- `GameOverUIController`는 이를 감지하고 보이지 않게 대기하다가, 설정된 지연 시간(`13초`)이 지나면 자동으로 화면을 띄우고 `ReasonText`의 내용을 상황에 맞게 덮어씌웁니다.

> **💡 참고:** 사유 텍스트 문구는 `GameOverUIController.cs` 스크립트 내부의 `GetEndingReason()` 함수에서 수정할 수 있습니다. 폰트, 색상, 디자인 등은 텍스트 컴포넌트에서 자유롭게 디자인하시면 됩니다.
