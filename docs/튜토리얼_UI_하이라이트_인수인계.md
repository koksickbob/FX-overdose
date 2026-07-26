# 튜토리얼 UI 하이라이트 시스템 인수인계 문서

## 1. 개요 및 현상
현재 `TutorialManager.cs`에서는 튜토리얼 진행 시 유저가 주목해야 할 특정 UI(잔고, 멘탈, 차트, 버튼 등)에 시선을 유도하기 위해 런타임에 동적으로 노란색 테두리 하이라이트 상자를 생성하여 씌우는 로직이 적용되어 있습니다.

그러나 **잔고(Balance) 및 일부 카드 형태의 UI**에서 하이라이트 테두리가 카드 전체를 감싸지 못하고 텍스트(예: `$4,000.00`) 바로 아래에 아주 작은 5x5 사이즈의 점이나 엉뚱한 크기로 생성되는 이슈가 발생하고 있습니다.

### 원인 분석
- `TutorialManager`는 `FindAnyObjectByType`과 리플렉션(Reflection)을 통해 내부 텍스트 컴포넌트(`balanceValueLabel` 등)를 찾은 뒤, `parent`를 타고 올라가며 카드의 배경 껍데기를 동적으로 추적합니다.
- 하지만 현재 UI 프리팹(`TopStatusBar` 등)의 계층(Hierarchy) 구조가 매우 깊거나, Layout Group의 자동 정렬 크기 갱신 지연, 혹은 텍스트와 배경 Image가 형제(Sibling) 객체로 분리되어 있는 등의 구조적 요인으로 인해 동적 추적 로직이 정확한 카드 테두리를 찾는 데 실패하고 있습니다.

---

## 2. UI 담당자 요청 사항 (해결 방안)

이 문제를 코드단에서 억지로 추적(부모 탐색, 이름 검색 등)하는 방식은 UI 프리팹이 수정될 때마다 고장날 위험이 큽니다. 
따라서 UI를 담당하시는 분께서 **에디터 상에서 하이라이트 대상 영역을 명시적으로 지정**해주시는 방향으로 구조를 개선해야 합니다.

### 방법 A: 프리팹 내부에 하이라이트 전용 투명 박스 삽입 (권장)
가장 확실한 방법은, 하이라이트가 쳐져야 하는 UI 요소(잔고 카드, 멘탈 카드 등)의 내부에 하이라이트 기준이 될 **투명한 RectTransform(또는 Image)을 미리 만들어두고 이를 컨트롤러에서 참조**하게 하는 것입니다.

1. `TopStatusBar` 프리팹을 엽니다.
2. 잔고 카드의 배경 역할을 하는 오브젝트 아래에 빈 오브젝트를 하나 만들고 이름은 `BalanceHighlightTarget` 등으로 짓습니다.
3. 이 오브젝트의 앵커(Anchor)를 `Stretch (0,0 ~ 1,1)`로 설정하여 카드 전체를 덮게 한 뒤 투명하게 둡니다.
4. `TopStatusBarUIController.cs`에 다음 필드를 추가하고 인스펙터에서 2번의 오브젝트를 끌어다 연결(Bind)합니다.
   ```csharp
   [Header("Tutorial Targets")]
   public Transform balanceHighlightTarget;
   ```
5. `TutorialManager.cs`에서는 복잡한 부모 탐색 코드(`FindCardByName` 등)를 전부 지우고, 그냥 저 `balanceHighlightTarget`을 가져다 하이라이트를 생성하면 완벽하게 해결됩니다.

### 방법 B: 직접 하이라이트 UI를 만들어두고 On/Off 제어
코드로 하이라이트 이펙트(노란색 테두리 + 깜빡임)를 생성하는 대신, 아예 UI 프리팹 디자인 단계에서 노란색 테두리를 예쁘게 만들어두고 꺼둔(Active = false) 상태로 저장합니다.
- 튜토리얼 매니저는 단순히 그 테두리 오브젝트를 `SetActive(true)` 해주기만 하면 됩니다.
- 애니메이션(깜빡임)도 유니티 Animator를 이용해 UI 자체에 달아두면 가시성과 퀄리티가 훨씬 좋아집니다.

---

## 3. 현재 튜토리얼에서 하이라이트가 필요한 UI 목록
UI 개편 및 인수인계 진행 시 다음 4가지 영역에 대한 타겟팅(또는 명시적 참조) 작업이 필요합니다.

1. **메인 차트 영역**
   - 대상: `ChartUIController`의 메인 패널
   - 현재 방식: `ChartUIController`의 최상단 transform을 가져와 씌움
2. **잔고(Balance) 카드**
   - 대상: `TopStatusBarUIController` 내부의 잔고 표시 카드 전체
   - 현재 방식: `balanceValueLabel` 텍스트를 찾은 뒤 위로 올라가며 카드 껍데기를 찾으려다 실패하여 작은 점으로 나타나는 문제 발생
3. **멘탈(Mental) 상태 카드**
   - 대상: `VitalsValueUI` 내부의 멘탈 게이지가 포함된 카드 전체
   - 현재 방식: `mentalSlider`를 찾아 부모를 역추적 중
4. **트레이딩 버튼 (Long, Short, Close)**
   - 대상: `TradingPanelUIController` 내부의 각 버튼들
   - 현재 방식: 버튼 오브젝트 자체에 하이라이트를 씌우므로 현재는 정상 작동 중

---

## 4. 인수인계 후속 작업 가이드 (프로그래머 대상)

UI 담당자가 위의 '방법 A' 또는 '방법 B'로 구조를 잡아주시면, 프로그래머는 `TutorialManager.cs`의 200~260번 라인 근처에 있는 `AutoBindHighlights()` 함수를 다음과 같이 아주 심플하게 수정해주시면 됩니다.

```csharp
// 수정 후 예상 코드 예시
var topBar = FindAnyObjectByType<TopStatusBarUIController>();
if (topBar != null && topBar.balanceHighlightTarget != null)
{
    balanceHighlight = CreateHighlightOverlay(topBar.balanceHighlightTarget);
}
```

이렇게 하면 UI 구조가 아무리 바뀌어도(Layout Group 등) 타겟 영역만 잘 지정되어 있다면 완벽하게 하이라이트가 씌워집니다.
