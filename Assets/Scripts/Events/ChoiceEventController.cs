using System;
using System.Collections.Generic;
using UnityEngine;
using FXOverdose.Core;
using FXOverdose.Trading;

namespace FXOverdose.Events
{
    public class ChoiceEventController : MonoBehaviour
    {
        // ── 이벤트 지속 시간 기본값 ────────────────────────────────────────
        // 템플릿 옵션의 OverrideDurationSeconds가 0 이하일 때 쓰이는 값입니다.
        // 과거에는 호출부에 150이 리터럴로 박혀 있어 템플릿 값이 통째로 무시됐습니다. (C3)
        //
        //  · 이벤트 쉴드(실시간 초) : 강제 진입한 포지션이 일반 청산 로직에 먹히지 않는 보호 시간.
        //                            TradingController.eventProtectionEndTime 이 소비합니다.
        //  · 차트 드리프트(인게임 분): 강제 빔이 목표 변동률까지 도달하는 데 쓰는 시간.
        //                            MarketSimulationEngine 이 소비합니다.
        private const int DefaultEventShieldSeconds = 150;
        private const int DefaultChartDriftMinutes = 30;

        /// <summary>
        /// 이보다 짧은 OverrideDurationSeconds는 legacy 단위 오해로 기입된 값으로 보고 무시합니다.
        /// TradingController가 쉴드에 적용하는 하한(30초)과 같은 값입니다.
        /// </summary>
        private const int MinPlausibleShieldSeconds = 30;

        /// <summary>실패(트랩) 시 빔 위력 배율. 성공 빔의 몇 %로 반대 방향 빔을 낼지. (R6)</summary>
        private const float TrapBeamRatio = 0.7f;

        [Header("연결 시스템")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private MarketSimulationEngine marketEngine;
        [SerializeField] private TradingController tradingController;
        [SerializeField] private TraderStatus traderStatus;
        [SerializeField] private ChoiceEventPopupUIController uiController;

        [Header("이벤트 데이터 풀")]
        [SerializeField] private List<ChoiceEventSO> allEvents = new();
        [Header("이벤트 논리 템플릿 풀")]
        [SerializeField] private List<EventLogicTemplateSO> logicTemplates = new();

        private ChoiceEventSO currentActiveEvent; // 하드코딩 Fallback용 및 동적 생성용 공용
        private ChoiceEventSO dynamicEventInstance; // 메모리 릭 방지용 추적

        public bool IsEventActive => (uiController != null && uiController.IsShowing) || currentActiveEvent != null;
        public ChoiceEventSO CurrentActiveEvent => currentActiveEvent;

        private bool pausedByChoiceEvent;
        private int lastTriggerDay = -1;

        // 이 세션(씬 진입)에서 저장된 스케줄이 현재 시각과 맞는지 한 번 검사했는지.
        // 저장하지 않습니다 — 씬에 들어올 때마다 다시 검사해야 하는 값입니다.
        private bool scheduleValidatedThisSession = false;
        private int nextRandomTriggerMinuteOfDay = -1;
        private long lastEventTriggerGameMinutes = -999999L;
        private int eventsTriggeredToday = 0;
        private int lowMentalEventsTriggeredToday = 0;
        public int SpecialItemOptionsUsedToday { get; private set; } = 0;
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

            if (gameManager == null) gameManager = GameManager.Instance;
            if (marketEngine == null) marketEngine = FindAnyObjectByType<MarketSimulationEngine>(FindObjectsInactive.Include);
            if (tradingController == null) tradingController = FindAnyObjectByType<TradingController>(FindObjectsInactive.Include);
            traderStatus = TraderStatus.CanonicalInstance;

            Initialize(gameManager, marketEngine, tradingController, traderStatus);
        }

        /// <summary>
        /// 팝업이 떠 있는 동안 이 컴포넌트가 꺼지면(P2P 진입 시 <c>DisableAll&lt;ChoiceEventController&gt;()</c> 등)
        /// <see cref="OnOptionSelected"/>가 영영 불리지 않아 <c>pausedByChoiceEvent</c>로 건 일시정지가
        /// <c>GameState.Paused</c>로 굳습니다. 되돌릴 곳이 여기밖에 없습니다.
        /// </summary>
        private void OnDisable()
        {
            if (!pausedByChoiceEvent) return;

            uiController?.Hide();
            if (gameManager != null && gameManager.CurrentState == GameManager.GameState.Paused)
            {
                gameManager.ResumeGame();
                Debug.LogWarning("[ChoiceEventController] 선택 이벤트 도중 컴포넌트가 비활성화되어 인게임 시간을 강제 재개합니다.");
            }
            pausedByChoiceEvent = false;
            currentActiveEvent = null;
        }

        private void OnDestroy()
        {
            if (gameManager != null)
            {
                gameManager.OnGameMinuteAdvanced -= OnGameMinuteAdvanced;
            }
        }

        /// <summary>
        /// 일일 이벤트 스케줄을 세이브에 담습니다. 이 값들이 빠져 있어
        /// 불러오기만 하면 하루 2회 상한이 리셋되던 문제를 막습니다. (SV-A4)
        /// </summary>
        public void CaptureSaveData(FXOverdose.Core.SaveData data)
        {
            if (data == null) return;

            data.EventLastTriggerDay = lastTriggerDay;
            data.EventsTriggeredToday = eventsTriggeredToday;
            data.LowMentalEventsTriggeredToday = lowMentalEventsTriggeredToday;
            data.EventNextRandomTriggerMinuteOfDay = nextRandomTriggerMinuteOfDay;
            data.EventLastTriggerGameMinutes = lastEventTriggerGameMinutes;
        }

        /// <summary>세이브에서 일일 이벤트 스케줄을 되돌립니다.</summary>
        public void RestoreFromSaveData(FXOverdose.Core.SaveData data)
        {
            if (data == null) return;

            lastTriggerDay = data.EventLastTriggerDay;
            eventsTriggeredToday = data.EventsTriggeredToday;
            lowMentalEventsTriggeredToday = data.LowMentalEventsTriggeredToday;
            nextRandomTriggerMinuteOfDay = data.EventNextRandomTriggerMinuteOfDay;
            lastEventTriggerGameMinutes = data.EventLastTriggerGameMinutes;

            // 되돌린 스케줄이 지금 시각과 맞는지는 첫 분 진행 때 검사합니다.
            scheduleValidatedThisSession = false;
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
            scheduleValidatedThisSession = false;
        }

        /// <summary>
        /// 그날 발생시킬 돌발 이벤트 상한. 거래 시간이 짧은 날에는 줄입니다.
        ///
        /// 데이팅 파트에서 시간 슬롯을 쓰면 거래 개시 시각이 뒤로 밀립니다(슬롯 1개 = 3시간).
        /// 21:00에 시작하면 남은 3시간에 이벤트 2번은 과밀하고, 애초에 다음 이벤트 예약 상한이
        /// 23:20이라 두 번째는 잡히지도 않은 채 "한도만 차지"하는 상태가 됩니다.
        ///
        /// 기준을 <b>현재 시각이 아니라 그날의 개시 시각</b>으로 잡는 것이 중요합니다.
        /// 현재 시각으로 재면 정상적인 09:00 시작에서도 저녁이 되면 한도가 줄어 평소 동작이 바뀝니다.
        /// 개시 시각은 남은 슬롯 수에서 되짚을 수 있고, 슬롯은 거래 중에 변하지 않으므로
        /// 하루 내내 같은 값이 나옵니다 — 별도 저장이 필요 없습니다.
        /// </summary>
        private int DailyEventCap()
        {
            // 시간 슬롯은 스토리 모드의 데이팅 파트에만 있습니다. 엔드리스·챌린지는 항상 09:00에 시작하므로
            // 종전대로 2회입니다.
            // ⚠️ DatingTimeManager는 DontDestroyOnLoad라 스토리 세션의 잔여 슬롯이 남아 있을 수 있습니다.
            //    모드를 먼저 보지 않으면 그 값이 엔드리스의 이벤트 한도를 조용히 깎습니다.
            var save = FXOverdose.Core.SaveLoadManager.Instance;
            if (save != null && save.CurrentGameMode != FXOverdose.Core.GameMode.Story) return 2;

            var dating = FXOverdose.DatingSim.Core.DatingTimeManager.Instance;
            int startMinute = dating != null
                ? FXOverdose.DatingSim.Core.DatingTimeManager.MinuteOfDayForSlots(dating.CurrentTimeSlot)
                : 9 * 60;

            int tradableMinutes = (24 * 60) - startMinute;
            if (tradableMinutes >= 360) return 2;   // 6시간 이상 — 평소대로 2회
            if (tradableMinutes >= 180) return 1;   // 3시간 이상 — 1회
            return 0;                               // 3시간 미만 — 발생시키지 않음
        }

        private void ResetDailySchedule(int day, int currentDayMinutes = 0)
        {
            lastTriggerDay = day;
            eventsTriggeredToday = 0;
            lowMentalEventsTriggeredToday = 0;
            SpecialItemOptionsUsedToday = 0;

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
                if (eventsTriggeredToday < DailyEventCap() && currentDayMinutes >= nextRandomTriggerMinuteOfDay)
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
            else if (!scheduleValidatedThisSession && nextRandomTriggerMinuteOfDay <= currentDayMinutes)
            {
                // 같은 일차인데 예정 시각이 이미 지나 있다 = 스케줄을 잡은 뒤 시계가 앞으로 점프했다는 뜻입니다.
                // 데이팅 파트에서 시간 슬롯을 쓰면 시계가 분 단위 진행 없이 통째로 밀리므로
                // (AdvanceClockWithoutSimulation은 OnGameMinuteAdvanced를 발행하지 않습니다)
                // 이 컨트롤러는 점프를 목격하지 못한 채 "예정 시각이 지났다"만 보게 됩니다.
                // 그대로 두면 거래 개시 첫 분에 팝업이 터집니다. 현재 시각 기준으로 다시 잡습니다.
                Debug.Log($"[ChoiceEventController] ⏭️ 시계 점프 감지 — 지난 예정 시각({nextRandomTriggerMinuteOfDay / 60:D2}:{nextRandomTriggerMinuteOfDay % 60:D2})을 현재({hour:D2}:{minute:D2}) 기준으로 재조정합니다.");
                ResetDailySchedule(day, currentDayMinutes);
            }
            scheduleValidatedThisSession = true;

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

            // 3. 일일 랜덤 발생 (하루 2회 한도 & 예정된 랜덤 시간 도달 시)
            if (eventsTriggeredToday < DailyEventCap() && currentDayMinutes >= nextRandomTriggerMinuteOfDay)
            {
                // 텍스트가 전부 사전 작성 자산이므로 예정 시각에 그대로 띄웁니다.
                // (생성 대기용 10분 연기 루프는 LLM 제거와 함께 사라졌습니다.)
                bool shown = ShowRandomTemplateEvent();

                if (!shown)
                {
                    // M4: 표시에 실패했으면 하루 발생 횟수를 소모하지 않고 잠시 뒤 다시 시도합니다.
                    nextRandomTriggerMinuteOfDay = currentDayMinutes + 10;
                    Debug.LogWarning($"[ChoiceEventController] ⚠️ 이벤트 표시에 실패해 발생 횟수를 소모하지 않고 {nextRandomTriggerMinuteOfDay / 60:D2}:{nextRandomTriggerMinuteOfDay % 60:D2}으로 재시도를 예약합니다.");
                    return;
                }

                eventsTriggeredToday++;
                lastEventTriggerGameMinutes = totalGameMinutes;
                if (eventsTriggeredToday < DailyEventCap())
                {
                    ScheduleNextRandomTrigger(currentDayMinutes);
                }
                return;
            }

            // 4. AI 트레이더 멘탈 위기(LowMental) 비상 트리거 판정 (하루 최대 1회 제한 적용)
            if (lowMentalEventsTriggeredToday < 1 && traderStatus != null && traderStatus.MentalRatio <= 0.15f)
            {
                if (gameManager != null && gameManager.IsFastForwardingTime)
                {
                    // 고속 스킵(스킬 학습 등) 중에는 이벤트 팝업이 난입하여 스킵을 방해하지 못하도록 발생을 지연(무시)합니다.
                    return;
                }

                if (Time.time - lastMentalTriggerTime > 180f) // 실시간 3분 쿨다운
                {
                    // M4: 표시에 성공했을 때만 하루 1회 한도와 쿨다운을 소모합니다.
                    if (!TriggerRandomEvent(EventTriggerCondition.LowMental)) return;

                    lastMentalTriggerTime = Time.time;
                    lastEventTriggerGameMinutes = totalGameMinutes;
                    lowMentalEventsTriggeredToday++;

                    if (eventsTriggeredToday < DailyEventCap() && nextRandomTriggerMinuteOfDay < currentDayMinutes + 60)
                    {
                        ScheduleNextRandomTrigger(currentDayMinutes);
                    }
                }
            }
        }

        public bool TriggerRandomEvent(EventTriggerCondition preferredCondition = EventTriggerCondition.Any)
        {
            if (traderStatus != null && traderStatus.CurrentMentalState == TraderStatus.MentalState.Overdose)
            {
                return false;
            }

            if (allEvents.Count == 0) LoadAllEventAssets();
            if (allEvents.Count == 0) return false;

            // M6: 특정 조건(LowMental 등)을 요청했을 때는 그 조건에 맞는 이벤트만 후보에 넣습니다.
            //     과거에는 조건이 맞는 이벤트와 Any 이벤트를 한 통에 섞어 뽑아, 멘탈 위기 전용
            //     연출이 요청됐는데도 아무 관련 없는 이벤트가 나올 수 있었습니다.
            List<ChoiceEventSO> candidates = new();
            foreach (var ev in allEvents)
            {
                if (ev == null) continue;
                if (preferredCondition == EventTriggerCondition.Any || ev.TriggerCondition == preferredCondition)
                {
                    candidates.Add(ev);
                }
            }

            // 조건에 맞는 전용 이벤트가 하나도 없을 때만 Any 이벤트로 넓혀서 재탐색합니다.
            if (candidates.Count == 0 && preferredCondition != EventTriggerCondition.Any)
            {
                foreach (var ev in allEvents)
                {
                    if (ev != null && ev.TriggerCondition == EventTriggerCondition.Any) candidates.Add(ev);
                }
                if (candidates.Count > 0)
                {
                    Debug.Log($"[ChoiceEventController] 조건 '{preferredCondition}' 전용 이벤트가 없어 Any 이벤트로 대체합니다.");
                }
            }

            if (candidates.Count == 0) candidates = allEvents;

            ChoiceEventSO selected = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            return ShowChoiceDialog(selected);
        }

        public bool TriggerSpecificEvent(string eventID)
        {
            if (allEvents.Count == 0) LoadAllEventAssets();
            ChoiceEventSO match = allEvents.Find(e => e != null && string.Equals(e.EventID, eventID, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                return ShowChoiceDialog(match);
            }

            Debug.LogWarning($"[ChoiceEventController] 이벤트 ID '{eventID}'를 찾을 수 없습니다.");
            return false;
        }

        /// <summary>
        /// 템플릿 풀에서 하나를 뽑아 돌발 이벤트를 띄웁니다.
        /// 풀이 비어 있을 때만 하드코딩 이벤트(Resources/Events/EVENT_*)로 대체합니다.
        /// </summary>
        private bool ShowRandomTemplateEvent()
        {
            EnsureTemplatesLoaded();

            if (logicTemplates.Count == 0)
            {
                Debug.LogWarning("[ChoiceEventController] 사용할 수 있는 로직 템플릿이 없어 하드코딩 이벤트로 대체합니다.");
                return TriggerRandomEvent(EventTriggerCondition.Any);
            }

            return ShowTemplateEvent(logicTemplates[UnityEngine.Random.Range(0, logicTemplates.Count)]);
        }

        /// <summary>
        /// 템플릿 기반 돌발 이벤트를 즉시 1회 발생시킵니다. 디버그 메뉴와 검증 절차용 진입점입니다.
        /// 템플릿을 지정하지 않으면 풀에서 무작위로 하나 뽑습니다.
        /// </summary>
        public bool ForceTriggerTemplateEvent(EventLogicTemplateSO template = null)
        {
            return template != null ? ShowTemplateEvent(template) : ShowRandomTemplateEvent();
        }


        /// <summary>
        /// Resources에서 로직 템플릿을 1회 로드합니다.
        /// R3: LogicOptions가 3개 미만이거나 null 슬롯이 있는 템플릿은 풀에서 제외합니다.
        /// (ShowTemplateEvent가 LogicOptions[0..2]를 무방비로 인덱싱하므로 여기서 걸러야 안전합니다.)
        /// </summary>
        private void EnsureTemplatesLoaded()
        {
            if (logicTemplates.Count > 0) return;

            EventLogicTemplateSO[] loaded = Resources.LoadAll<EventLogicTemplateSO>("Events/Templates");
            if (loaded == null || loaded.Length == 0) return;

            int rejected = 0;
            foreach (var t in loaded)
            {
                if (t == null) continue;
                if (!t.HasValidOptions)
                {
                    rejected++;
                    Debug.LogWarning($"[ChoiceEventController] ⚠️ 템플릿 '{t.name}'은 선택지가 3개 미만이라 풀에서 제외합니다.");
                    continue;
                }
                logicTemplates.Add(t);
            }

            Debug.Log($"[ChoiceEventController] 로직 템플릿 {logicTemplates.Count}개 로드 완료" + (rejected > 0 ? $" (부적합 {rejected}개 제외)" : ""));
        }

        private bool ShowChoiceDialog(ChoiceEventSO eventData)
        {
            if (eventData == null || uiController == null) return false;
            currentActiveEvent = eventData;

            PrepareGamePause();
            AudioManager.Play(AudioCue.EventAppear, true);
            uiController.Show(eventData, OnOptionSelected);
            return true;
        }

        /// <summary>
        /// 로직 템플릿으로 돌발 이벤트를 띄웁니다. 표시 텍스트와 선택지 로직 모두
        /// 템플릿 자산에 사전 작성된 값만 씁니다.
        /// </summary>
        private bool ShowTemplateEvent(EventLogicTemplateSO template)
        {
            if (template == null || uiController == null) return false;

            // R3: 인덱싱 전에 반드시 확인합니다.
            if (!template.HasValidOptions)
            {
                Debug.LogError($"[ChoiceEventController] 템플릿 '{template.name}'의 선택지가 3개 미만입니다. 하드코딩 이벤트로 대체합니다.");
                return TriggerRandomEvent(EventTriggerCondition.Any);
            }

            if (!TryResolveEventText(template, out string title, out string description, out string monologue))
            {
                Debug.LogError($"[ChoiceEventController] 템플릿 '{template.name}'의 표시 텍스트를 확보하지 못했습니다. 하드코딩 이벤트로 대체합니다.");
                return TriggerRandomEvent(EventTriggerCondition.Any);
            }

            PrepareGamePause();

            // 기존에 만들었던 동적 인스턴스가 있다면 삭제
            if (dynamicEventInstance != null) Destroy(dynamicEventInstance);

            dynamicEventInstance = ScriptableObject.CreateInstance<ChoiceEventSO>();
            dynamicEventInstance.EventID = template.TemplateID;
            dynamicEventInstance.ScenarioTitle = title;
            dynamicEventInstance.ScenarioDescription = description;
            dynamicEventInstance.AIMonologue = monologue;
            dynamicEventInstance.Options = new ChoiceOptionData[3];

            for (int i = 0; i < 3; i++)
            {
                EventLogicOptionData logic = template.LogicOptions[i];
                ChoiceOptionData opt = ConvertTemplateOption(logic);
                opt.OptionTitle = logic.OptionTitle;
                opt.Description = logic.OptionDescription;
                dynamicEventInstance.Options[i] = opt;
            }

            currentActiveEvent = dynamicEventInstance;
            AudioManager.Play(AudioCue.EventAppear, true);
            uiController.Show(dynamicEventInstance, OnOptionSelected);
            return true;
        }

        /// <summary>
        /// 표시할 텍스트 3요소를 템플릿 자산에서 결정합니다.
        ///
        /// 제목/본문   : 템플릿 사전 텍스트 → 테마 서술 기반 자동 문구
        /// 요미 대사   : 템플릿 사전 대사 후보 → 기본 문구
        ///
        /// 자동 문구는 사전 텍스트를 아직 채우지 않은 템플릿이 들어와도 빈 팝업이 뜨지 않도록
        /// 남겨 둔 최후의 방어선입니다. 현재 자산 242개는 모두 사전 텍스트가 채워져 있습니다.
        /// </summary>
        private bool TryResolveEventText(
            EventLogicTemplateSO template,
            out string title, out string description, out string monologue)
        {
            title = template.FallbackTitle;
            description = template.FallbackDescription;

            string theme = template.GetThemeDescription();
            if (string.IsNullOrWhiteSpace(title)) title = "긴급 시장 속보";
            if (string.IsNullOrWhiteSpace(description))
            {
                description = $"{theme} 시장 참여자들이 대응 방향을 두고 극심하게 갈리고 있습니다.";
            }

            monologue = ResolveYomiLine(template);

            return !string.IsNullOrWhiteSpace(title)
                && !string.IsNullOrWhiteSpace(description)
                && !string.IsNullOrWhiteSpace(monologue);
        }

        /// <summary>
        /// 이벤트 팝업에 띄울 요미 반응을 템플릿의 사전 대사 후보에서 고릅니다.
        /// 후보가 비어 있을 때만 기본 문구로 떨어집니다.
        /// </summary>
        private string ResolveYomiLine(EventLogicTemplateSO template)
        {
            var pool = template.FallbackMonologues;
            if (pool != null && pool.Length > 0)
            {
                for (int attempt = 0; attempt < pool.Length; attempt++)
                {
                    string candidate = pool[UnityEngine.Random.Range(0, pool.Length)];
                    if (!string.IsNullOrWhiteSpace(candidate)) return candidate;
                }
            }

            return "오빠...! 이거 지금 어떻게 할지 빨리 정해줘!";
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
            opt.MentalPenaltyOnFail = logicOption.MentalPenaltyOnFail;
            opt.HealthPenaltyOnFail = logicOption.HealthPenaltyOnFail;
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
                // 하루 1회 사용 제한 검증 (UI에서도 막히지만 컨트롤러에서도 이중 검증)
                if (SpecialItemOptionsUsedToday >= 1)
                {
                    uiController?.ShowToastWarning("확정 수익 아이템은 하루 1회만 사용할 수 있습니다!");
                    return;
                }

                if (traderStatus == null || !traderStatus.ConsumeItem(option.RequiredItemId, option.RequiredItemCount))
                {
                    uiController?.ShowToastWarning("필요한 특수 아이템이 부족합니다!");
                    return;
                }
                
                SpecialItemOptionsUsedToday++;
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

        /// <summary>
        /// 이 선택지가 성패 판정을 받는 "베팅"인지. Safe(관망)와 SpecialItem(확정 성공)은 아닙니다. (R5)
        /// </summary>
        private static bool IsBetOption(ChoiceOptionData option) =>
            option.OptionType == ChoiceOptionType.Aggressive ||
            option.OptionType == ChoiceOptionType.DirectionalLong ||
            option.OptionType == ChoiceOptionType.DirectionalShort;

        private static bool RollSuccess(ChoiceOptionData option)
        {
            if (option.OverrideSignalProbTrue >= 1f) return true;
            if (option.OverrideSignalProbTrue <= 0f) return false;
            return UnityEngine.Random.value <= option.OverrideSignalProbTrue;
        }

        /// <summary>
        /// 이 선택으로 플레이어가 잡게 되는 포지션. 관망이면 None.
        /// </summary>
        private static TradingController.PositionType ResolvePosition(ChoiceOptionData option)
        {
            if (option.OptionType == ChoiceOptionType.DirectionalLong) return TradingController.PositionType.Long;
            if (option.OptionType == ChoiceOptionType.DirectionalShort) return TradingController.PositionType.Short;
            return option.ForcePosition;
        }

        /// <summary>
        /// C3: 데이터에 기입된 이벤트 쉴드 지속 시간(실시간 초)을 해석합니다.
        ///
        /// ⚠️ 기존 자산은 이 필드를 다른 단위로 이해하고 기입했습니다.
        ///    (템플릿 242개는 10/15, 하드코딩 이벤트 30개는 2~25)
        ///    호출부에 150이 리터럴로 박혀 있어 지금까지 한 번도 쓰이지 않았기 때문에
        ///    이 값들은 게임에서 검증된 적이 없습니다.
        ///
        ///    그대로 살리면 쉴드가 150초에서 30초(TradingController의 하한)로 줄어드는
        ///    미검증 밸런스 변경이 되므로, 상식적인 하한 미만이면 legacy 데이터로 보고
        ///    기본값을 씁니다. 마이그레이션 툴을 돌리면 이 경로는 더 이상 타지 않습니다.
        /// </summary>
        private static int ResolveShieldSeconds(ChoiceOptionData option)
        {
            int authored = option.OverrideDurationSeconds;
            if (authored >= MinPlausibleShieldSeconds) return authored;

            if (authored > 0 && !warnedLegacyDuration)
            {
                warnedLegacyDuration = true;
                Debug.LogWarning(
                    $"[ChoiceEventController] ⚠️ OverrideDurationSeconds={authored}초는 이벤트 쉴드로 쓰기엔 너무 짧아 " +
                    $"기본값 {DefaultEventShieldSeconds}초를 사용합니다. " +
                    "Tools/FX OVERDOSE/Migrate Event Templates (C3+C4) 로 자산을 정규화하십시오. (이 경고는 세션당 1회)");
            }
            return DefaultEventShieldSeconds;
        }

        private static bool warnedLegacyDuration;

        /// <summary>돌발 이벤트 한 건이 깎을 수 있는 멘탈의 절대 상한입니다.</summary>
        private const int MaxSingleEventMentalPenalty = 35;

        /// <summary>
        /// AI 트레이더 멘탈/체력 변화를 적용합니다. (C4)
        ///
        /// 과거에는 방향성 선택지만 "성공 시에만 적용"이었는데, 데이터에는 멘탈이 음수로 기입돼 있어
        /// <b>베팅에 성공하면 멘탈이 폭락하고 실패하면 아무 일도 없는</b> 상태였습니다.
        /// 이제 보상(성공)과 페널티(실패)를 별도 필드로 분리해 적용합니다.
        /// </summary>
        private void ApplyVitals(ChoiceOptionData option, bool isBet, bool isSuccess)
        {
            if (traderStatus == null) return;

            int mental = (!isBet || isSuccess) ? option.MentalChangeAmount : option.MentalPenaltyOnFail;
            int health = (!isBet || isSuccess) ? option.HealthChangeAmount : option.HealthPenaltyOnFail;

            // 단일 이벤트는 "죽었다/살았다"가 아니라 "이제 위험해졌다"를 정해야 합니다.
            // 재베이크를 잊은 에셋이나 손으로 쓴 SO가 만멘탈을 한 방에 지우지 못하도록
            // 데이터와 무관하게 여기서 상한을 강제합니다.
            mental = Mathf.Max(mental, -MaxSingleEventMentalPenalty);

            if (mental != 0) traderStatus.ModifyMentalState(mental);
            if (health != 0) traderStatus.ModifyHealthState(health);

            if (isBet)
            {
                Debug.Log($"[ChoiceEventController] 🧠 베팅 {(isSuccess ? "성공 보상" : "실패 페널티")} 적용: 멘탈 {mental:+#;-#;0} / 체력 {health:+#;-#;0}");
            }
        }

        /// <summary>
        /// 이벤트 빔의 부호를 단일 규칙으로 결정합니다. (R6)
        ///
        ///   · 포지션을 잡지 않는 선택(관망) : 데이터 부호를 그대로 사용. 트랩 아님.
        ///   · 성공                          : 플레이어 포지션 방향으로 +|빔|
        ///   · 실패                          : 플레이어 포지션 반대 방향으로 |빔| × TrapBeamRatio
        ///
        /// 즉 데이터의 <b>크기</b>만 기획값으로 쓰고 <b>부호</b>는 포지션과 성패에서 유도합니다.
        /// 과거에는 "이미 반대 부호면 그대로 두고 아니면 70% 반전"이라는 조건부 규칙이었는데,
        /// 데이터에 음수 빔이 섞여 있어 템플릿마다 규칙이 다르게 걸려 결과를 예측할 수 없었습니다.
        /// </summary>
        private static float ResolveBeam(ChoiceOptionData option, TradingController.PositionType position, bool isBet, bool isSuccess)
        {
            float magnitude = Mathf.Abs(option.OverrideBeamPercent);

            if (position == TradingController.PositionType.None)
            {
                // 관망: 시장은 어차피 제 갈 길을 갑니다. 데이터 부호를 존중합니다.
                return option.OverrideBeamPercent;
            }

            bool favourable = !isBet || isSuccess;
            float signed = position == TradingController.PositionType.Long ? magnitude : -magnitude;

            return favourable ? signed : -signed * TrapBeamRatio;
        }

        private void ApplyOptionEffects(ChoiceOptionData option)
        {
            if (option == null) return;

            bool isBet = IsBetOption(option);
            bool isSuccess = RollSuccess(option);
            TradingController.PositionType position = ResolvePosition(option);

            // 1. 멘탈/체력 (C4)
            ApplyVitals(option, isBet, isSuccess);

            if (gameManager != null && gameManager.CurrentState == GameManager.GameState.GameOver)
            {
                return;
            }

            if (isSuccess && isBet)
            {
                FXOverdose.Core.AchievementManager.Instance?.RecordRiskyEventSuccess();
            }

            // 2. 매매 제어
            int shieldSeconds = ResolveShieldSeconds(option); // C3
            TradingController.EventPositionHandlingMode handlingMode = option.PositionHandlingMode;
            if (isBet && handlingMode == TradingController.EventPositionHandlingMode.StandardAuto)
            {
                handlingMode = isSuccess
                    ? TradingController.EventPositionHandlingMode.GreedyHold
                    : TradingController.EventPositionHandlingMode.HoldToMitigateLoss;
            }

            if (tradingController == null) tradingController = FindAnyObjectByType<TradingController>(FindObjectsInactive.Include);
            if (tradingController != null)
            {
                if (position == TradingController.PositionType.None && option.OptionType == ChoiceOptionType.Safe)
                {
                    tradingController.CloseAllPositions();
                }
                else if (option.ForceLeverage > 0 || position != TradingController.PositionType.None)
                {
                    int fallbackLeverage = isBet ? 100 : 10;
                    int dynamicLeverage = GetDynamicEventLeverage(option.ForceLeverage > 0 ? option.ForceLeverage : fallbackLeverage);
                    tradingController.ExecuteEmergencyTrade(
                        position, dynamicLeverage, shieldSeconds, handlingMode,
                        option.CustomTargetROELimit, option.CustomStopLossROELimit,
                        isPlayerChoice: true, isTrueSignal: isSuccess);
                }
            }

            // 매매 처리 중 파산/Overdose로 게임이 종료되었으면 차트 빔 주입 중단
            if (gameManager == null) gameManager = GameManager.Instance;
            if (gameManager != null && gameManager.CurrentState == GameManager.GameState.GameOver)
            {
                return;
            }

            // 3. 차트 강제 빔 오버라이드 (R5/R6)
            if (marketEngine == null) marketEngine = FindAnyObjectByType<MarketSimulationEngine>(FindObjectsInactive.Include);
            if (marketEngine != null && Mathf.Abs(option.OverrideBeamPercent) > 0.001f)
            {
                float finalBeam = ResolveBeam(option, position, isBet, isSuccess);

                // ⭐ R5: 트랩(휩소) 판정은 "베팅에 실패했을 때"만 성립합니다.
                //    과거에는 Safe 선택지의 OverrideSignalProbTrue가 0이라는 이유만으로
                //    isOptionSuccess = false → isTrap = true가 되어, 포지션을 닫고 관망하는
                //    선택지에 트랩 플래그가 붙는 의미 모순이 있었습니다.
                bool isTrap = isBet && !isSuccess;

                marketEngine.OverrideMarketTrend(finalBeam, DefaultChartDriftMinutes, isTrap);

                if (isTrap)
                {
                    Debug.LogWarning($"[ChoiceEventController] ⚠️ 트랩 발동! ({finalBeam:F2}%, 포지션: {position}, 모드: {handlingMode}, 쉴드 {shieldSeconds}초)");
                }
                else
                {
                    Debug.Log($"[ChoiceEventController] ⚡ 이벤트 빔 주입 ({finalBeam:F2}%, 포지션: {position}, 모드: {handlingMode}, 쉴드 {shieldSeconds}초)");
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
