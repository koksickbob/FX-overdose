using System;
using UnityEngine;
using FXOverdose.Core;

namespace FXOverdose.DatingSim.Core
{
    public class DatingTimeManager : MonoBehaviour
    {
        public static DatingTimeManager Instance { get; private set; }

        // --- 데이터 상태 (SaveData 연동용) ---
        [Header("Initial Dating State")]
        [SerializeField] private int currentStamina = 100;
        [SerializeField] private int maxStamina = 100;
        private int currentAffection;
        // 역대 최고 호감도. 토픽 해금 판정의 기준입니다. 호감도가 깎여도 내려가지 않습니다. (TS8)
        private int peakAffection;
        private int currentObsession;
        private int storyProgressStage;
        [SerializeField] private int currentTimeSlot = 5;
        [SerializeField] private int currentDay = 1;

        // --- 프로퍼티 (읽기 전용) ---
        public int CurrentStamina => currentStamina;
        public int MaxStamina => maxStamina;
        public int CurrentAffection => currentAffection;

        /// <summary>역대 최고 호감도. 대화 토픽 해금은 이 값으로 판정합니다.</summary>
        public int PeakAffection => peakAffection;
        public int CurrentObsession => currentObsession;
        public int StoryProgressStage => storyProgressStage;
        public int CurrentTimeSlot => currentTimeSlot;
        public int CurrentDay => currentDay;

        // --- 이벤트 (UI 및 기타 로직 갱신용) ---
        public event Action<int, int> OnStaminaChanged; // current, max
        public event Action<int> OnTimeSlotChanged;
        public event Action<int> OnAffectionChanged;
        public event Action<int> OnObsessionChanged;
        public event Action<int> OnStoryProgressChanged;
        public event Action<int> OnDayChanged;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // 테스트 씬처럼 SaveLoadManager가 개입하여 LoadFromSaveData를 호출하지 않는 환경을 위한 방어 코드
            if (maxStamina == 0)
            {
                maxStamina = 100;
                currentStamina = 100;
                currentTimeSlot = 5;
                currentDay = 1;

                // UI 갱신을 위해 이벤트 강제 호출
                OnStaminaChanged?.Invoke(currentStamina, maxStamina);
                OnTimeSlotChanged?.Invoke(currentTimeSlot);
                OnAffectionChanged?.Invoke(currentAffection);
                OnObsessionChanged?.Invoke(currentObsession);
            }
        }

        // --- SaveLoadManager에서 호출될 Load/Save 헬퍼 ---
        public void LoadFromSaveData(SaveData data)
        {
            currentStamina = data.DatingStamina;
            maxStamina = data.DatingMaxStamina;
            currentAffection = data.DatingAffection;
            // 구버전 세이브 대비: 마이그레이터가 채우지 못한 경우에도 현재 호감도 아래로 내려가지 않게 합니다.
            peakAffection = Mathf.Max(data.TalkPeakAffection, data.DatingAffection);
            currentObsession = data.DatingObsession;
            storyProgressStage = data.StoryProgressStage;
            currentTimeSlot = data.DatingTimeSlot;
            currentDay = data.DatingDay;

            // 로드 후 이벤트 강제 방출하여 UI 동기화
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            OnTimeSlotChanged?.Invoke(currentTimeSlot);
            OnAffectionChanged?.Invoke(currentAffection);
            OnObsessionChanged?.Invoke(currentObsession);
            OnStoryProgressChanged?.Invoke(storyProgressStage);
            OnDayChanged?.Invoke(currentDay);
        }

        public void SaveToData(SaveData data)
        {
            data.DatingStamina = currentStamina;
            data.DatingMaxStamina = maxStamina;
            data.DatingAffection = currentAffection;
            data.TalkPeakAffection = peakAffection;
            data.DatingObsession = currentObsession;
            data.StoryProgressStage = storyProgressStage;
            data.DatingTimeSlot = currentTimeSlot;
            data.DatingDay = currentDay;
        }

        // --- 핵심 로직 메서드 ---

        /// <summary>
        /// 슬롯 1개가 소모하는 게임 내 시간. 5슬롯 × 3시간 = 15시간이고,
        /// 하루가 09:00에 시작해 24:00에 끝나므로 정확히 하루 전체와 맞아떨어집니다.
        /// </summary>
        public const int MinutesPerTimeSlot = 180;

        /// <summary>하루에 주어지는 기본 슬롯 수. 09:00 + 5×3시간 = 24:00.</summary>
        public const int DefaultTimeSlots = 5;

        /// <summary>하루가 시작하는 시각(분). 09:00.</summary>
        public const int DayStartMinuteOfDay = 9 * 60;

        /// <summary>
        /// 남은 슬롯 수에 대응하는 시각(분)을 돌려줍니다. 5→09:00, 4→12:00 ... 0→24:00.
        /// 방·월드맵에 있는 동안 성립하는 계약이며, 거래가 시작되면 시계만 자유롭게 흐릅니다.
        /// </summary>
        public static int MinuteOfDayForSlots(int remainingSlots)
        {
            int used = Mathf.Clamp(DefaultTimeSlots - remainingSlots, 0, DefaultTimeSlots);
            return DayStartMinuteOfDay + used * MinutesPerTimeSlot;
        }

        /// <summary>남은 슬롯 수에 대응하는 시각 표기. 예: 3 → "15:00", 0 → "24:00".</summary>
        public static string ClockTextForSlots(int remainingSlots)
        {
            int minutes = MinuteOfDayForSlots(remainingSlots);
            return $"{minutes / 60:00}:{minutes % 60:00}";
        }

        /// <summary>시간 슬롯을 소모합니다. 부족하면 false 반환</summary>
        public bool TryConsumeTimeSlot(int cost)
        {
            if (currentTimeSlot < cost) return false;

            currentTimeSlot -= cost;

            // 슬롯을 깎는 유일한 통로가 여기이므로 시계도 여기서 밉니다.
            // 소비 지점(휴식·알바·데이트)을 각각 고치면 네 번째 소비 지점이 생길 때 조용히 누락됩니다.
            // ⚠️ 저장보다 먼저여야 합니다. 뒤에 두면 한 박자 늦은 시각이 디스크에 남고,
            //    그 사이에 씬이 전환되면 영영 반영되지 않습니다.
            AdvanceClockBySlots(cost);

            OnTimeSlotChanged?.Invoke(currentTimeSlot);

            // 데이터 변경 시 자동 저장 플래그 혹은 직접 저장
            SaveLoadManager.Instance?.SaveCurrentGame();
            return true;
        }

        /// <summary>
        /// 소모한 슬롯만큼 게임 시계를 앞으로 밉니다.
        ///
        /// GameManager가 있는 씬(GameScene)과 없는 씬(요미의 방·월드맵·편의점)에서 경로가 갈립니다 —
        /// 잔고를 다루는 <c>WorldMapManager.PayWage</c>와 같은 이분기입니다.
        ///
        /// ⚠️ GameManager가 있어도 <c>AdvanceGameMinutes</c>를 쓰면 안 됩니다. 그쪽은 Playing이
        ///    아니면 멈춰서 분을 쌓아두므로, 거래 개시 시점에 한꺼번에 터집니다.
        /// </summary>
        private void AdvanceClockBySlots(int slots)
        {
            if (slots <= 0) return;
            int minutes = slots * MinutesPerTimeSlot;

            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.AdvanceClockWithoutSimulation(minutes);
                return;
            }

            // GameManager가 없는 씬: 세이브 스냅샷에 직접 기입합니다.
            // 확정은 호출부(TryConsumeTimeSlot)의 SaveCurrentGame이 합니다.
            var data = SaveLoadManager.Instance?.CurrentData;
            if (data == null) return;

            int total = Mathf.Min(data.CurrentHour * 60 + data.CurrentMinute + minutes, 24 * 60);
            data.CurrentHour = total / 60;
            data.CurrentMinute = total % 60;
        }

        /// <summary>체력을 소모합니다. 부족하면 false 반환</summary>
        public bool TryConsumeStamina(int cost)
        {
            if (currentStamina < cost) return false;
            
            currentStamina -= cost;
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            
            SaveLoadManager.Instance?.SaveCurrentGame();
            return true;
        }

        /// <summary>체력을 회복합니다. 최대치를 초과하지 않습니다.</summary>
        public void RecoverStamina(int amount)
        {
            currentStamina = Mathf.Min(currentStamina + amount, maxStamina);
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            
            SaveLoadManager.Instance?.SaveCurrentGame();
        }

        /// <summary>호감도를 증감시킵니다. 집착도와 동일하게 0~100으로 제한됩니다. (SV-B12)</summary>
        public void ModifyAffection(int amount)
        {
            currentAffection = Mathf.Clamp(currentAffection + amount, 0, 100);

            // 최고치 갱신은 호감도의 주인인 여기서 합니다.
            // 대화 외의 경로(이벤트·데이트 등)로 올라도 해금 판정이 누락되지 않아야 합니다. (U-3)
            if (currentAffection > peakAffection) peakAffection = currentAffection;

            OnAffectionChanged?.Invoke(currentAffection);
            SaveLoadManager.Instance?.SaveCurrentGame();
        }

        /// <summary>집착도를 증감시킵니다.</summary>
        public void ModifyObsession(int amount)
        {
            currentObsession += amount;
            currentObsession = Mathf.Clamp(currentObsession, 0, 100);
            OnObsessionChanged?.Invoke(currentObsession);
            SaveLoadManager.Instance?.SaveCurrentGame();
        }

        /// <summary>스토리 진행도를 업데이트합니다.</summary>
        public void SetStoryProgressStage(int stage)
        {
            storyProgressStage = stage;
            OnStoryProgressChanged?.Invoke(storyProgressStage);
            SaveLoadManager.Instance?.SaveCurrentGame();
        }

        /// <summary>
        /// 트레이딩 파트가 다음 날로 넘어갈 때 호출됩니다. 일차를 받아 미러링하고 시간 슬롯을 리필합니다. (SV-A8 / S12)
        ///
        /// 일차를 여기서 스스로 올리지 않는 것이 핵심입니다. 일차의 주인은 GameManager 하나뿐이며,
        /// 이 매니저의 currentDay는 그 값을 따라가는 읽기 전용 사본입니다.
        ///
        /// ⚠️ <b>계약: SaveData.DatingDay == SaveData.CurrentDay 는 항상 성립해야 합니다.</b> (F-9 / S-5)
        ///    요미 대화의 모든 하루 게이트 — 선제 인사(TalkLastGreetingDay), 힌트 발급(TalkHintIssuedDay),
        ///    하루 1회 한도(TalkLastSessionEndDay), 일일 리셋(TalkDailyStateDay) — 는 <b>CurrentDay</b>를 보는데,
        ///    데이팅 UI와 슬롯은 <b>DatingDay</b>를 봅니다. 지금은 이 메서드가 유일한 일차 진입점이라 둘이 같습니다.
        ///    데이팅 전용 일차 진행을 만들면 그 게이트들이 <b>한꺼번에 조용히</b> 깨집니다.
        ///    그런 경로가 필요해지면 CurrentDay도 함께 올리거나, 게이트의 기준을 DatingDay로 통일하십시오.
        /// </summary>
        /// <remarks>
        /// 슬롯 리필과 시계 09:00 복귀는 <b>둘 다 일차 전환에서</b> 일어납니다 —
        /// 시계는 <c>GameManager.FinalizeProceedToNextDay</c>가, 슬롯은 여기가 맡습니다.
        /// 그래서 새 하루는 항상 「슬롯 5 / 09:00」으로 계약(<see cref="MinuteOfDayForSlots"/>)을
        /// 만족한 상태로 시작합니다. 한쪽만 바꾸면 계약이 깨집니다.
        /// </remarks>
        public void SyncToNewDay(int newDay, int defaultTimeSlots = DefaultTimeSlots)
        {
            bool dayChanged = currentDay != newDay;
            currentDay = newDay;
            currentTimeSlot = defaultTimeSlots;

            if (dayChanged) OnDayChanged?.Invoke(currentDay);
            OnTimeSlotChanged?.Invoke(currentTimeSlot);

            SaveLoadManager.Instance?.SaveCurrentGame();
        }
    }
}
