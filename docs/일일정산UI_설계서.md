# 일일 정산 UI 시스템 연동용 설계 및 디자인 가이드

본 문서는 UI 담당자가 일일 정산(Daily Settlement) 화면을 조립하고 구현할 때 참고할 수 있는 시스템 스펙 및 설계 가이드라인입니다. 로직 단에서 필요한 데이터와 이벤트는 모두 `GameManager` 등에 준비되어 있으므로 UI 조립 시 아래 사항들을 연결하면 됩니다.

---

## 1. 개요 및 발생 시점
- **발생 시점**: 인게임 시간 기준 **매일 24:00 (자정)** 에 도달했을 때.
- **게임 상태**: `GameManager.CurrentState`가 `GameState.Settlement`로 전환되며 인게임 시간이 완전히 정지됩니다.
- **연동 이벤트**: `GameManager`의 `OnDayEnded` 이벤트를 구독하여 이 이벤트가 발생하면 정산 UI 팝업을 띄우면 됩니다.

## 2. 필수 표시 데이터 항목
UI 화면 상에 다음과 같은 데이터가 표시되어야 합니다.

### 1) 당일 수익금 (Daily PnL)
- **설명**: 09:00 시작 시점의 자본금 대비 24:00 마감 시점의 총 자본금 차이
- **계산식**:
  ```csharp
  // GameManager와 TraderStatus 참조 필요
  float currentTotalEquity = TraderStatus.CanonicalInstance != null ? TraderStatus.CanonicalInstance.GetTotalEquity() : gameManager.CurrentBalance;
  float dailyPnL = currentTotalEquity - gameManager.StartOfDayEquity;
  ```
- **연출 제안**: 수익이 + 이면 초록색/파란색 텍스트와 위 화살표, - 이면 붉은색 텍스트와 아래 화살표로 강조.

### 2) 현재 총 보유 자산 (Total Equity)
- **설명**: 정산 시점의 총 자산(현금 + 증거금 + 미실현 손익)
- **데이터 위치**: `TraderStatus.CanonicalInstance.GetTotalEquity()`

### 3) 요미(Yomi)의 한 줄 반응 (LLM 대사)
- **설명**: 정산 결과에 대한 요미의 즉각적인 반응(결혼을 꿈꾸며 애교 부리거나 버려질까봐 매달림).
- **데이터 요청 방법**:
  `AIPromptBuilder`를 통해 `EventCategory.DailySettlement` 카테고리로 프롬프트를 빌드하여 로컬 LLM에 전송하고 결과값을 텍스트 컴포넌트에 출력합니다.
  ```csharp
  string prompt = aiPromptBuilder.BuildPrompt(EventCategory.DailySettlement, $"오늘 당일 수익금은 {dailyPnL:N0}$ 였습니다.");
  // LocalLLMService를 호출하여 응답 대기 후 화면에 표시
  ```

## 3. 조작 인터페이스 (버튼)

### [다음날 진행하기] 버튼
- **기능**: 유저가 정산 내역과 요미의 반응을 모두 확인한 후, 다음 날로 넘어가기 위한 버튼입니다.
- **호출 로직**:
  버튼의 `onClick` 이벤트에 아래 코드를 연결합니다.
  ```csharp
  public void OnClickProceedToNextDay()
  {
      // 1. UI 닫기 애니메이션 또는 즉시 숨김
      gameObject.SetActive(false);
      
      // 2. 게임루프 다음날로 이관
      gameManager.ProceedToNextDay();
  }
  ```

## 4. UI 연출 디자인 제안 (UI팀 참고용)
1. **배경 처리**: 게임 차트 및 메인 화면이 뒤에 보일 수 있도록 반투명(딤 처리) 혹은 블러 처리를 권장합니다.
2. **등장 연출**: 하루의 끝을 알리는 무거운 타격음이나 맑은 종소리 등과 함께 영수증/결과지 포맷으로 위에서 아래로 떨어지거나 팝업되는 애니메이션.
3. **요미의 위치**: 화면 한쪽에 요미의 스프라이트가 표시되며, LLM에서 도출된 감정에 맞춰 환희/절망의 표정이 연동되면 좋습니다. (TraderEmotionEvaluator 로직 재활용 가능)
