using System;
using System.Collections.Generic;
using UnityEngine;
using FXOverdose.Core;
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
        [Header("LLM 이벤트 논리 템플릿 풀")]
        [SerializeField] private List<EventLogicTemplateSO> logicTemplates = new();

        private ChoiceEventSO currentActiveEvent; // 하드코딩 Fallback용 및 동적 생성용 공용
        private ChoiceEventSO dynamicEventInstance; // 메모리 릭 방지용 추적
        private EventLogicTemplateSO activeTemplate; // LLM 템플릿용
        private FXOverdose.AI.LLM.GeneratedChoiceEventData cachedLLMData;
        
        public bool IsTutorialMode { get; set; } = false;
        
        public bool IsEventActive => (uiController != null && uiController.IsShowing) || currentActiveEvent != null;
        public ChoiceEventSO CurrentActiveEvent => currentActiveEvent;

        private bool pausedByChoiceEvent;
        private int lastTriggerDay = -1;
        private int nextRandomTriggerMinuteOfDay = -1;
        private int preFetchMinuteOfDay = -1;
        private bool isFetchingLLM = false;
        private long lastEventTriggerGameMinutes = -999999L;
        private int eventsTriggeredToday = 0;
        private float lastMentalTriggerTime = -999f;
        private float lastEventTriggerRealTime = -999f;

        private void Awake()
        {
            if (uiController == null) uiController = GetComponentInChildren<ChoiceEventPopupUIController>(true);
            if (uiController == null) uiController = FindAnyObjectByType<ChoiceEventPopupUIController>(FindObjectsInactive.Include);
            if (uiController == null)
            {
                GameObject uiGo = new GameObject("ChoiceEventPopupUIController", typeof(ChoiceEventPopupUIController));
                uiGo.transform.SetParent(this.transform);
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
            if (IsTutorialMode) return;
            
            lastTriggerDay = day;
            eventsTriggeredToday = 0;

            // 오전 10시(600분) 이후 첫 이벤트 랜덤 발생 스케줄링
            int minStartMinute = Mathf.Max(10 * 60, currentDayMinutes);
            if (minStartMinute < 10 * 60) minStartMinute = 10 * 60;
            int maxStartMinute = Mathf.Min(minStartMinute + 360, 16 * 60); // 10:00 ~ 16:00 사이 첫 이벤트
            if (maxStartMinute <= minStartMinute) maxStartMinute = minStartMinute + 60;

            nextRandomTriggerMinuteOfDay = UnityEngine.Random.Range(minStartMinute, maxStartMinute);
            preFetchMinuteOfDay = nextRandomTriggerMinuteOfDay - 60; // 60분 전 미리 캐싱 (시간적 여유 확보)
            cachedLLMData = null;
            activeTemplate = null;
            isFetchingLLM = false;
            Debug.Log($"[ChoiceEventController] 📅 {day}일차 첫 돌발 선택 이벤트 예정 시간: {nextRandomTriggerMinuteOfDay / 60:D2}:{nextRandomTriggerMinuteOfDay % 60:D2} (사전 생성: {preFetchMinuteOfDay / 60:D2}:{preFetchMinuteOfDay % 60:D2})");
        }

        private void ScheduleNextRandomTrigger(int currentDayMinutes)
        {
            // 하루 총 2번 제한에 따라, 이전 이벤트 발생 후 최소 1시간(인게임 시간 60분)의 여유를 두고 그 이후부터 다시 랜덤으로 발생
            int minNextMinute = Mathf.Max(currentDayMinutes + 60, 10 * 60);
            int maxNextMinute = Mathf.Min(minNextMinute + 300, 23 * 60 + 20); // 최소 1시간 ~ 최대 6시간 이내 랜덤 (23:20 한도)
            if (maxNextMinute <= minNextMinute) maxNextMinute = minNextMinute + 60;

            nextRandomTriggerMinuteOfDay = UnityEngine.Random.Range(minNextMinute, maxNextMinute);
            preFetchMinuteOfDay = nextRandomTriggerMinuteOfDay - 60; // 60분 전 미리 캐싱
            cachedLLMData = null;
            activeTemplate = null;
            isFetchingLLM = false;
            Debug.Log($"[ChoiceEventController] 📅 다음 랜덤 이벤트 예정 시간: {nextRandomTriggerMinuteOfDay / 60:D2}:{nextRandomTriggerMinuteOfDay % 60:D2} (최소 1시간 쿨다운 적용)");
        }

        private void OnGameMinuteAdvanced()
        {
            if (IsTutorialMode) return;

            if (gameManager == null || gameManager.CurrentState != GameManager.GameState.Playing)
            {
                return;
            }

            // ⭐ [Overdose 보호] Overdose 상태일 때는 돌발 이벤트를 발생시키지 않고 무시합니다.
            if (traderStatus != null && traderStatus.CurrentMentalState == TraderStatus.MentalState.Overdose)
            {
                return;
            }

            int day = gameManager.CurrentDay;
            int hour = gameManager.CurrentHour;
            int minute = gameManager.CurrentMinute;
            int currentDayMinutes = hour * 60 + minute;

            if (gameManager.IsFastForwardingTime)
            {
                // 💡 [UI 겹침 방지 및 자연스러운 연출] 
                // 고속 스킵 중 이벤트 예정 시간을 돌파했다면, 스킵 도중이나 직후에 팝업이 바로 떠서 지저분해지는 것을 방지하기 위해
                // 이벤트 발생 시각을 '현재 시간 + 15~45분 뒤'로 지속적으로 밀어냅니다.
                // 결과적으로 고속 스킵이 완전히 종료된 이후 15~45분 사이에 자연스럽게 이벤트가 발생하게 됩니다.
                if (eventsTriggeredToday < 2 && currentDayMinutes >= nextRandomTriggerMinuteOfDay)
                {
                    nextRandomTriggerMinuteOfDay = currentDayMinutes + UnityEngine.Random.Range(15, 46);
                    Debug.Log($"[ChoiceEventController] 📅 고속 스킵 중 이벤트 예정 시간 돌파 감지! 이벤트 발생을 고속 스킵 이후 자연스러운 시점({nextRandomTriggerMinuteOfDay / 60:D2}:{nextRandomTriggerMinuteOfDay % 60:D2})으로 재조정합니다.");
                }
                return;
            }

            long totalGameMinutes = (long)day * 1440L + currentDayMinutes;

            // 24:00 마지막 분 계산 중에는 신규 돌발 이벤트를 열지 않고 일일 정산을 우선합니다.
            if (hour >= 24)
            {
                return;
            }

            if (day != lastTriggerDay)
            {
                ResetDailySchedule(day, currentDayMinutes);
            }

            // 1. 게임 시작 이후 오전 10시 이전에는 발생 차단
            if (hour < 10)
            {
                return;
            }

            // 2. 이벤트 발생 후 쿨다운 체크 (인게임 60분 및 현실 시간 10초)
            if (totalGameMinutes - lastEventTriggerGameMinutes < 60 || Time.unscaledTime - lastEventTriggerRealTime < 10f)
            {
                return;
            }

            // 2.5. 이벤트 발생 20분 전 사전 텍스트 생성 시작
            if (eventsTriggeredToday < 2 && currentDayMinutes >= preFetchMinuteOfDay && currentDayMinutes < nextRandomTriggerMinuteOfDay)
            {
                if (!isFetchingLLM && cachedLLMData == null)
                {
                    StartPreFetchingLLMEvent();
                }
            }

            // 3. 일일 랜덤 발생 (하루 2회 한도 & 예정된 랜덤 시간 도달 시)
            if (eventsTriggeredToday < 2 && currentDayMinutes >= nextRandomTriggerMinuteOfDay)
            {
                // 💡 LLM 텍스트 생성이 아직 안 끝났다면, 이벤트 발생을 10분씩 계속 뒤로 미뤄서 게임을 멈추지 않고 기다려 줍니다!
                if (isFetchingLLM)
                {
                    nextRandomTriggerMinuteOfDay += 10;
                    Debug.Log($"[ChoiceEventController] ⏳ LLM 텍스트 생성이 아직 진행 중입니다. 이벤트 발생 시간을 {nextRandomTriggerMinuteOfDay / 60:D2}:{nextRandomTriggerMinuteOfDay % 60:D2}으로 미룹니다.");
                    return;
                }

                // 캐싱된 LLM 데이터가 있으면 그걸 띄우고, 없으면 하드코딩 Fallback 띄움
                if (cachedLLMData != null && activeTemplate != null)
                {
                    ShowLLMChoiceDialog(activeTemplate, cachedLLMData);
                }
                else
                {
                    TriggerRandomEvent(EventTriggerCondition.TimeOfDay);
                }

                eventsTriggeredToday++;
                lastEventTriggerGameMinutes = totalGameMinutes;
                if (eventsTriggeredToday < 2)
                {
                    ScheduleNextRandomTrigger(currentDayMinutes);
                }
                return;
            }

            // 4. AI 트레이더 멘탈 위기(LowMental) 비상 트리거 판정
            if (traderStatus != null && traderStatus.MentalRatio <= 0.15f)
            {
                if (gameManager != null && gameManager.IsFastForwardingTime)
                {
                    // 고속 스킵(스킬 학습 등) 중에는 이벤트 팝업이 난입하여 스킵을 방해하지 못하도록 발생을 지연(무시)합니다.
                    return;
                }

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
            if (traderStatus != null && traderStatus.CurrentMentalState == TraderStatus.MentalState.Overdose)
            {
                return;
            }

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

        public void TriggerPrefetchedEvent()
        {
            if (cachedLLMData != null && activeTemplate != null)
            {
                ShowLLMChoiceDialog(activeTemplate, cachedLLMData);
            }
            else
            {
                TriggerRandomEvent(EventTriggerCondition.Any);
            }
        }

        public void ForceGuaranteedProfitEvent()
        {
            if (dynamicEventInstance != null) Destroy(dynamicEventInstance);
            
            dynamicEventInstance = ScriptableObject.CreateInstance<ChoiceEventSO>();
            dynamicEventInstance.EventID = "tutorial_guaranteed_profit";
            dynamicEventInstance.ScenarioTitle = "튜토리얼 확정 수익 이벤트";
            dynamicEventInstance.ScenarioDescription = "어느 선택지를 골라도 확정적인 수익이 발생합니다. 테스트해 보세요.";
            dynamicEventInstance.AIMonologue = "오빠! 이 이벤트는 무조건 수익이 나도록 설정되어 있어! 마음 놓고 선택해!";
            dynamicEventInstance.Options = new ChoiceOptionData[3];
            
            for (int i = 0; i < 3; i++)
            {
                var opt = new ChoiceOptionData();
                opt.OptionTitle = $"선택지 {i + 1}";
                opt.Description = "무조건 수익이 보장됩니다.";
                opt.OptionType = i == 0 ? ChoiceOptionType.Safe : (i == 1 ? ChoiceOptionType.Aggressive : ChoiceOptionType.SpecialItem);
                opt.OverrideSignalProbTrue = 1.0f; // 확정 성공
                opt.OverrideBeamPercent = 10f;     // 10% 상승 빔
                opt.ForcePosition = TradingController.PositionType.Long;
                opt.ForceLeverage = 10;
                opt.PositionHandlingMode = TradingController.EventPositionHandlingMode.StandardAuto;
                dynamicEventInstance.Options[i] = opt;
            }

            ShowChoiceDialog(dynamicEventInstance);
        }

        public async void StartPreFetchingLLMEvent()
        {
            if (logicTemplates.Count == 0)
            {
                EventLogicTemplateSO[] loaded = Resources.LoadAll<EventLogicTemplateSO>("Events/Templates");
                if (loaded != null && loaded.Length > 0) logicTemplates.AddRange(loaded);
                else return; // 템플릿이 없으면 프리페치 포기 (하드코딩 Fallback으로 넘어감)
            }

            isFetchingLLM = true;
            activeTemplate = logicTemplates[UnityEngine.Random.Range(0, logicTemplates.Count)];
            string marketContext = marketEngine != null ? $"현재 시장 상황: 주가 ${marketEngine.CurrentPrice:F1} (최고 ${marketEngine.Current24hHigh:F1} / 최저 ${marketEngine.Current24hLow:F1}), 상태: {marketEngine.CurrentRegime}" : "Unknown";

            var generator = FXOverdose.AI.LLM.LLMSafeGenerator.Instance;
            if (generator != null)
            {
                try
                {
                    cachedLLMData = await generator.GenerateChoiceEventAsync(activeTemplate, marketContext);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ChoiceEventController] 사전 텍스트 생성 실패: {e.Message}");
                    cachedLLMData = null;
                }
            }
            isFetchingLLM = false;
        }

        private void ShowChoiceDialog(ChoiceEventSO eventData)
        {
            if (eventData == null || uiController == null) return;
            currentActiveEvent = eventData;
            activeTemplate = null; // LLM 템플릿 초기화

            PrepareGamePause();
            AudioManager.Play(AudioCue.EventAppear, true);
            uiController.Show(eventData, OnOptionSelected);
        }

        private void ShowLLMChoiceDialog(EventLogicTemplateSO template, FXOverdose.AI.LLM.GeneratedChoiceEventData llmData)
        {
            if (template == null || llmData == null || uiController == null) return;
            activeTemplate = template;

            PrepareGamePause();
            
            // 기존에 만들었던 동적 인스턴스가 있다면 삭제
            if (dynamicEventInstance != null) Destroy(dynamicEventInstance);
            
            dynamicEventInstance = ScriptableObject.CreateInstance<ChoiceEventSO>();
            dynamicEventInstance.EventID = template.TemplateID;
            dynamicEventInstance.ScenarioTitle = llmData.ScenarioTitle;
            dynamicEventInstance.ScenarioDescription = llmData.ScenarioDescription;
            dynamicEventInstance.AIMonologue = llmData.AIMonologue;
            dynamicEventInstance.Options = new ChoiceOptionData[3];
            
            // 옵션 A
            dynamicEventInstance.Options[0] = ConvertTemplateOption(template.LogicOptions[0]);
            dynamicEventInstance.Options[0].OptionTitle = template.LogicOptions[0].OptionTitle;
            dynamicEventInstance.Options[0].Description = template.LogicOptions[0].OptionDescription;

            // 옵션 B
            dynamicEventInstance.Options[1] = ConvertTemplateOption(template.LogicOptions[1]);
            dynamicEventInstance.Options[1].OptionTitle = template.LogicOptions[1].OptionTitle;
            dynamicEventInstance.Options[1].Description = template.LogicOptions[1].OptionDescription;

            // 옵션 C
            dynamicEventInstance.Options[2] = ConvertTemplateOption(template.LogicOptions[2]);
            dynamicEventInstance.Options[2].OptionTitle = template.LogicOptions[2].OptionTitle;
            dynamicEventInstance.Options[2].Description = template.LogicOptions[2].OptionDescription;

            currentActiveEvent = dynamicEventInstance;
            AudioManager.Play(AudioCue.EventAppear, true);
            uiController.Show(dynamicEventInstance, OnOptionSelected);
            
            // 캐시 비우기
            cachedLLMData = null;
        }

        private ChoiceOptionData ConvertTemplateOption(EventLogicOptionData logicOption)
        {
            ChoiceOptionData opt = new ChoiceOptionData();
            if (logicOption == null) return opt;
            opt.OptionType = logicOption.OptionType;
            opt.RequiredItemId = logicOption.RequiredItemId;
            opt.RequiredItemCount = logicOption.RequiredItemCount;
            opt.OverrideSignalProbTrue = logicOption.OverrideSignalProbTrue;
            opt.OverrideBeamPercent = logicOption.OverrideBeamPercent;
            opt.OverrideDurationSeconds = logicOption.OverrideDurationSeconds;
            opt.MentalChangeAmount = logicOption.MentalChangeAmount;
            opt.HealthChangeAmount = logicOption.HealthChangeAmount;
            opt.ForceLeverage = logicOption.ForceLeverage;
            opt.ForcePosition = logicOption.ForcePosition;
            opt.PositionHandlingMode = logicOption.PositionHandlingMode;
            opt.CustomTargetROELimit = logicOption.CustomTargetROELimit;
            opt.CustomStopLossROELimit = logicOption.CustomStopLossROELimit;
            return opt;
        }

        private void PrepareGamePause()
        {
            if (gameManager != null)
            {
                int currentMinuteOfDay = gameManager.CurrentHour * 60 + gameManager.CurrentMinute;
                lastEventTriggerGameMinutes = (long)gameManager.CurrentDay * 1440L + currentMinuteOfDay;
            }
            lastEventTriggerRealTime = Time.unscaledTime;

            if (gameManager != null)
            {
                pausedByChoiceEvent = gameManager.CurrentState == GameManager.GameState.Playing;
                if (pausedByChoiceEvent)
                {
                    gameManager.PauseGame();
                    Debug.Log($"[ChoiceEventController] ⏸️ 돌발 선택 이벤트 발생으로 인게임 시간 정지");
                }
            }
        }

        public void OnOptionSelected(int optionIndex)
        {
            if (currentActiveEvent == null || optionIndex < 0 || optionIndex >= currentActiveEvent.Options.Length) return;
            ChoiceOptionData option = currentActiveEvent.Options[optionIndex];

            if (option == null) return;

            // 1. 특수 아이템 개입 요구 검증 및 차감
            if (option.OptionType == ChoiceOptionType.SpecialItem && !string.IsNullOrEmpty(option.RequiredItemId))
            {
                if (traderStatus == null || !traderStatus.ConsumeItem(option.RequiredItemId, option.RequiredItemCount))
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
            currentActiveEvent = null;
        }

        public int GetDynamicEventLeverage(int baseLeverage)
        {
            if (baseLeverage <= 0) return 0;
            
            int playerMax = TraderLevelSystem.Instance != null ? TraderLevelSystem.Instance.GetMaxAllowedLeverage() : 10;
            int currentDay = gameManager != null ? gameManager.CurrentDay : 1;
            
            // 일차별 해금 캡: 플레이어 최대치 + (일차 * 7)
            int dayCap = playerMax + (currentDay * 7);
            
            // 최종 이벤트 레버리지 계산
            int dynamicLeverage = Mathf.Min(baseLeverage, dayCap);
            
            // 하드캡 125배 적용
            return Mathf.Min(dynamicLeverage, 125);
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
                    int dynamicLeverage = GetDynamicEventLeverage(option.ForceLeverage > 0 ? option.ForceLeverage : 10);
                    tradingController.ExecuteEmergencyTrade(option.ForcePosition, dynamicLeverage, 150, option.PositionHandlingMode, option.CustomTargetROELimit, option.CustomStopLossROELimit, isPlayerChoice: false, isTrueSignal: isOptionSuccess);
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
                int dynamicLeverage = GetDynamicEventLeverage(option.ForceLeverage > 0 ? option.ForceLeverage : 100);
                tradingController.ExecuteEmergencyTrade(playerChosenPos, dynamicLeverage, 150, handlingMode, option.CustomTargetROELimit, option.CustomStopLossROELimit, isPlayerChoice: true, isTrueSignal: isSuccess);
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
                    float trapPenalty = -20f;
                    if (TraderLevelSystem.Instance != null && TraderLevelSystem.Instance.ChartStudyLevel >= 9)
                    {
                        trapPenalty *= 0.5f;
                    }
                    if (traderStatus != null) traderStatus.ModifyMentalState(trapPenalty);
                    Debug.LogWarning($"[ChoiceEventController] ⚠️ 플레이어 직접 선택({playerChosenPos}) 트랩 발동! ({trapBeam:F2}%, 모드: {handlingMode}) 및 멘탈 페널티({trapPenalty})");
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
