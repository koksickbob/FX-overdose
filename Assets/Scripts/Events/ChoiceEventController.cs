using System;
using System.Collections.Generic;
using UnityEngine;
using FXOverdose.Trading;

namespace FXOverdose.Events
{
    public class ChoiceEventController : MonoBehaviour
    {
        [Header("연결 시스템")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private MarketSimulationEngine marketEngine;
        [SerializeField] private TradingController tradingController;
        [SerializeField] private TraderStatus traderStatus;
        [SerializeField] private ChoiceEventPopupUIController uiController;

        [Header("이벤트 데이터 풀")]
        [SerializeField] private List<ChoiceEventSO> allEvents = new();

        private ChoiceEventSO currentActiveEvent;
        private bool pausedByChoiceEvent;
        private int lastTriggerDay = -1;
        private int nextRandomTriggerMinuteOfDay = -1;
        private long lastEventTriggerGameMinutes = -999999L;
        private int eventsTriggeredToday = 0;
        private float lastMentalTriggerTime = -999f;

        private void Awake()
        {
            if (uiController == null) uiController = GetComponentInChildren<ChoiceEventPopupUIController>();
            if (uiController == null) uiController = FindAnyObjectByType<ChoiceEventPopupUIController>();
            if (uiController == null)
            {
                GameObject uiGo = new GameObject("ChoiceEventPopupUIController", typeof(ChoiceEventPopupUIController));
                uiController = uiGo.GetComponent<ChoiceEventPopupUIController>();
            }

            LoadAllEventAssets();
        }

        private void Start()
        {
            if (lastTriggerDay != -1 && gameManager != null) return;

            if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            if (marketEngine == null) marketEngine = FindAnyObjectByType<MarketSimulationEngine>(FindObjectsInactive.Include);
            if (tradingController == null) tradingController = FindAnyObjectByType<TradingController>(FindObjectsInactive.Include);
            traderStatus = TraderStatus.CanonicalInstance;

            Initialize(gameManager, marketEngine, tradingController, traderStatus);
        }

        private void OnDestroy()
        {
            if (gameManager != null)
            {
                gameManager.OnGameMinuteAdvanced -= OnGameMinuteAdvanced;
            }
        }

        public void Initialize(GameManager gm, MarketSimulationEngine market, TradingController trading, TraderStatus status)
        {
            gameManager = gm;
            marketEngine = market;
            tradingController = trading;
            traderStatus = TraderStatus.CanonicalInstance;

            if (gameManager != null)
            {
                gameManager.OnGameMinuteAdvanced -= OnGameMinuteAdvanced;
                gameManager.OnGameMinuteAdvanced += OnGameMinuteAdvanced;
            }

            ResetDailySchedule(1);
        }

        private void ResetDailySchedule(int day, int currentDayMinutes = 0)
        {
            lastTriggerDay = day;
            eventsTriggeredToday = 0;

            // 오전 10시(600분) 이후 첫 이벤트 랜덤 발생 스케줄링
            int minStartMinute = Mathf.Max(10 * 60, currentDayMinutes);
            if (minStartMinute < 10 * 60) minStartMinute = 10 * 60;
            int maxStartMinute = Mathf.Min(minStartMinute + 360, 16 * 60); // 10:00 ~ 16:00 사이 첫 이벤트
            if (maxStartMinute <= minStartMinute) maxStartMinute = minStartMinute + 60;

            nextRandomTriggerMinuteOfDay = UnityEngine.Random.Range(minStartMinute, maxStartMinute);
            Debug.Log($"[ChoiceEventController] 📅 {day}일차 첫 돌발 선택 이벤트 예정 시간: {nextRandomTriggerMinuteOfDay / 60:D2}:{nextRandomTriggerMinuteOfDay % 60:D2}");
        }

        private void ScheduleNextRandomTrigger(int currentDayMinutes)
        {
            // 하루 총 2번 제한에 따라, 이전 이벤트 발생 후 최소 1시간(인게임 시간 60분)의 여유를 두고 그 이후부터 다시 랜덤으로 발생
            int minNextMinute = Mathf.Max(currentDayMinutes + 60, 10 * 60);
            int maxNextMinute = Mathf.Min(minNextMinute + 300, 23 * 60 + 20); // 최소 1시간 ~ 최대 6시간 이내 랜덤 (23:20 한도)
            if (maxNextMinute <= minNextMinute) maxNextMinute = minNextMinute + 60;

            nextRandomTriggerMinuteOfDay = UnityEngine.Random.Range(minNextMinute, maxNextMinute);
            Debug.Log($"[ChoiceEventController] 📅 다음 랜덤 이벤트 예정 시간: {nextRandomTriggerMinuteOfDay / 60:D2}:{nextRandomTriggerMinuteOfDay % 60:D2} (최소 1시간 쿨다운 적용)");
        }

        private void OnGameMinuteAdvanced()
        {
            if (gameManager == null || gameManager.CurrentState != GameManager.GameState.Playing || gameManager.IsFastForwardingTime)
            {
                return;
            }

            int day = gameManager.CurrentDay;
            int hour = gameManager.CurrentHour;
            int minute = gameManager.CurrentMinute;
            int currentDayMinutes = hour * 60 + minute;
            long totalGameMinutes = (long)day * 1440L + currentDayMinutes;

            if (day != lastTriggerDay)
            {
                ResetDailySchedule(day, currentDayMinutes);
            }

            // 1. 게임 시작 이후 오전 10시 이전에는 발생 차단
            if (hour < 10)
            {
                return;
            }

            // 2. 이벤트 발생 후 최소 1시간(인게임 60분) 여유 쿨다운 체크
            if (totalGameMinutes - lastEventTriggerGameMinutes < 60)
            {
                return;
            }

            // 3. 일일 랜덤 발생 (하루 2회 한도 & 예정된 랜덤 시간 도달 시)
            if (eventsTriggeredToday < 2 && currentDayMinutes >= nextRandomTriggerMinuteOfDay)
            {
                eventsTriggeredToday++;
                lastEventTriggerGameMinutes = totalGameMinutes;
                if (eventsTriggeredToday < 2)
                {
                    ScheduleNextRandomTrigger(currentDayMinutes);
                }
                TriggerRandomEvent(EventTriggerCondition.TimeOfDay);
                return;
            }

            // 4. AI 트레이더 멘탈 위기(LowMental) 비상 트리거 판정
            if (traderStatus != null && traderStatus.MentalRatio <= 0.15f)
            {
                if (Time.time - lastMentalTriggerTime > 180f) // 실시간 3분 쿨다운
                {
                    lastMentalTriggerTime = Time.time;
                    lastEventTriggerGameMinutes = totalGameMinutes;
                    if (eventsTriggeredToday < 2 && nextRandomTriggerMinuteOfDay < currentDayMinutes + 60)
                    {
                        ScheduleNextRandomTrigger(currentDayMinutes);
                    }
                    TriggerRandomEvent(EventTriggerCondition.LowMental);
                }
            }
        }

        public void TriggerRandomEvent(EventTriggerCondition preferredCondition = EventTriggerCondition.Any)
        {
            if (allEvents.Count == 0) LoadAllEventAssets();
            if (allEvents.Count == 0) return;

            List<ChoiceEventSO> candidates = new();
            foreach (var ev in allEvents)
            {
                if (ev == null) continue;
                if (preferredCondition != EventTriggerCondition.Any && ev.TriggerCondition == preferredCondition)
                {
                    candidates.Add(ev);
                }
                else if (ev.TriggerCondition == EventTriggerCondition.Any || preferredCondition == EventTriggerCondition.Any)
                {
                    candidates.Add(ev);
                }
            }

            if (candidates.Count == 0) candidates = allEvents;
            ChoiceEventSO selected = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            ShowChoiceDialog(selected);
        }

        public void TriggerSpecificEvent(string eventID)
        {
            if (allEvents.Count == 0) LoadAllEventAssets();
            ChoiceEventSO match = allEvents.Find(e => e != null && string.Equals(e.EventID, eventID, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                ShowChoiceDialog(match);
            }
            else
            {
                Debug.LogWarning($"[ChoiceEventController] 이벤트 ID '{eventID}'를 찾을 수 없습니다.");
            }
        }

        private void ShowChoiceDialog(ChoiceEventSO eventData)
        {
            if (eventData == null || uiController == null) return;
            currentActiveEvent = eventData;

            // 이벤트가 발생할 때마다 마지막 인게임 시간 기록
            if (gameManager != null)
            {
                int currentMinuteOfDay = gameManager.CurrentHour * 60 + gameManager.CurrentMinute;
                lastEventTriggerGameMinutes = (long)gameManager.CurrentDay * 1440L + currentMinuteOfDay;
            }

            // 💡 [핵심: 상점 시간정지 동기화] 선택지 창이 떠있는 동안 상점과 완전히 동일하게 PauseGame()
            if (gameManager != null)
            {
                pausedByChoiceEvent = gameManager.CurrentState == GameManager.GameState.Playing;
                if (pausedByChoiceEvent)
                {
                    gameManager.PauseGame();
                    Debug.Log($"[ChoiceEventController] ⏸️ 돌발 선택 이벤트({eventData.EventID}) 발생으로 인게임 시간 정지");
                }
            }

            uiController.Show(eventData, OnOptionSelected);
        }

        public void OnOptionSelected(int optionIndex)
        {
            if (currentActiveEvent == null || optionIndex < 0 || optionIndex >= currentActiveEvent.Options.Length) return;
            ChoiceOptionData option = currentActiveEvent.Options[optionIndex];
            if (option == null) return;

            // 1. 특수 아이템 개입 요구 검증 및 차감
            if (option.OptionType == ChoiceOptionType.SpecialItem && option.RequiredItemIndex >= 0)
            {
                if (traderStatus == null || !traderStatus.ConsumeItem(option.RequiredItemIndex, option.RequiredItemCount))
                {
                    uiController?.ShowToastWarning("필요한 특수 아이템이 부족합니다!");
                    return;
                }
            }

            // 2. 팝업 종료
            uiController?.Hide();

            // 3. 선택 효과 먼저 적용 (청산/진입/멘탈 소모 중 파산이나 Overdose 엔딩이 발생할 수 있음)
            ApplyOptionEffects(option);

            // 4. 효과 적용 후 인게임 시간 재개 (단, 이벤트 효과로 게임이 종료(GameOver)되었다면 재개하지 않음!)
            if (pausedByChoiceEvent && gameManager != null)
            {
                if (gameManager.CurrentState == GameManager.GameState.Paused)
                {
                    gameManager.ResumeGame();
                    Debug.Log($"[ChoiceEventController] ▶️ 선택 완료(옵션 {optionIndex}: {option.OptionTitle}). 인게임 시간 재개");
                }
                else if (gameManager.CurrentState == GameManager.GameState.GameOver)
                {
                    Debug.LogWarning("[ChoiceEventController] 🛑 선택 이벤트 효과 처리 중 게임 종료(GameOver) 조건이 달성되어 시간을 재개하지 않고 종료합니다.");
                }
            }
            pausedByChoiceEvent = false;
        }

        private void ApplyOptionEffects(ChoiceOptionData option)
        {
            if (option == null) return;

            // AI 상태 변경
            if (traderStatus != null)
            {
                traderStatus.ModifyMentalState(option.MentalChangeAmount);
                traderStatus.ModifyHealthState(option.HealthChangeAmount);
            }

            if (gameManager != null && gameManager.CurrentState == GameManager.GameState.GameOver)
            {
                return;
            }

            // 직접 방향 선택 판정 분기
            if (option.OptionType == ChoiceOptionType.DirectionalLong || option.OptionType == ChoiceOptionType.DirectionalShort)
            {
                ExecutePlayerDirectionalChoice(option);
                return;
            }

            // 확률적 성공 여부 판정 (기본값 0이면 100% 확정 신호, 0~1 사이면 확률 판정)
            bool isOptionSuccess = option.OverrideSignalProbTrue <= 0f || option.OverrideSignalProbTrue >= 1f || (UnityEngine.Random.value <= option.OverrideSignalProbTrue);

            // 매매 제어
            if (tradingController == null) tradingController = FindAnyObjectByType<TradingController>(FindObjectsInactive.Include);
            if (tradingController != null)
            {
                if (option.ForcePosition == TradingController.PositionType.None && option.OptionType == ChoiceOptionType.Safe)
                {
                    tradingController.CloseAllPositions();
                }
                else if (option.ForceLeverage > 0 || option.ForcePosition != TradingController.PositionType.None)
                {
                    tradingController.ExecuteEmergencyTrade(option.ForcePosition, option.ForceLeverage > 0 ? option.ForceLeverage : 10, 150, option.PositionHandlingMode, option.CustomTargetROELimit, option.CustomStopLossROELimit, isPlayerChoice: false, isTrueSignal: isOptionSuccess);
                }
            }

            // 매매 처리 중 파산/Overdose로 게임이 종료되었으면 차트 빔 주입 중단
            if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            if (gameManager != null && gameManager.CurrentState == GameManager.GameState.GameOver)
            {
                return;
            }

            // 차트 강제 빔 오버라이드
            if (marketEngine == null) marketEngine = FindAnyObjectByType<MarketSimulationEngine>(FindObjectsInactive.Include);
            if (marketEngine != null && Mathf.Abs(option.OverrideBeamPercent) > 0.001f)
            {
                marketEngine.OverrideMarketTrend(option.OverrideBeamPercent, 150, !isOptionSuccess);
            }
        }

        private void ExecutePlayerDirectionalChoice(ChoiceOptionData option)
        {
            TradingController.PositionType playerChosenPos = option.OptionType == ChoiceOptionType.DirectionalLong 
                ? TradingController.PositionType.Long 
                : TradingController.PositionType.Short;

            bool isSuccess = UnityEngine.Random.value <= option.OverrideSignalProbTrue;

            TradingController.EventPositionHandlingMode handlingMode = option.PositionHandlingMode;
            if (handlingMode == TradingController.EventPositionHandlingMode.StandardAuto)
            {
                handlingMode = isSuccess ? TradingController.EventPositionHandlingMode.GreedyHold : TradingController.EventPositionHandlingMode.HoldToMitigateLoss;
            }

            if (tradingController == null) tradingController = FindAnyObjectByType<TradingController>(FindObjectsInactive.Include);
            if (tradingController != null)
            {
                tradingController.ExecuteEmergencyTrade(playerChosenPos, option.ForceLeverage > 0 ? option.ForceLeverage : 100, 150, handlingMode, option.CustomTargetROELimit, option.CustomStopLossROELimit, isPlayerChoice: true, isTrueSignal: isSuccess);
            }

            // 매매 처리 중 파산/Overdose로 게임이 종료되었으면 차트 트랩/빔 처리 중단
            if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            if (gameManager != null && gameManager.CurrentState == GameManager.GameState.GameOver)
            {
                return;
            }

            if (marketEngine != null)
            {
                if (isSuccess)
                {
                    float targetBeam = playerChosenPos == TradingController.PositionType.Long ? Mathf.Abs(option.OverrideBeamPercent) : -Mathf.Abs(option.OverrideBeamPercent);
                    marketEngine.OverrideMarketTrend(targetBeam, 150, false);
                    Debug.Log($"[ChoiceEventController] ⚡ 플레이어 직접 선택({playerChosenPos}) 익절 빔 성공! ({targetBeam:F2}%, 모드: {handlingMode})");
                }
                else
                {
                    float trapBeam = playerChosenPos == TradingController.PositionType.Long ? -Mathf.Abs(option.OverrideBeamPercent) * 0.7f : Mathf.Abs(option.OverrideBeamPercent) * 0.7f;
                    marketEngine.OverrideMarketTrend(trapBeam, 150, true);
                    if (traderStatus != null) traderStatus.ModifyMentalState(-20f);
                    Debug.LogWarning($"[ChoiceEventController] ⚠️ 플레이어 직접 선택({playerChosenPos}) 트랩 발동! ({trapBeam:F2}%, 모드: {handlingMode}) 및 멘탈 페널티");
                }
            }
        }

        private void LoadAllEventAssets()
        {
            ChoiceEventSO[] loaded = Resources.LoadAll<ChoiceEventSO>("Events");
            allEvents.Clear();
            if (loaded != null && loaded.Length > 0)
            {
                allEvents.AddRange(loaded);
            }
            else
            {
                allEvents.AddRange(ChoiceEventRuntimeData.GetDefaultEvents());
                Debug.Log($"[ChoiceEventController] 💡 Resources/Events 애셋이 없어 런타임 기본 15종 시나리오 데이터를 로드했습니다.");
            }
        }
    }
}
