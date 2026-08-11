using System;
using UnityEngine;
using FXOverdose.Core;

namespace FXOverdose.DatingSim.Core
{
    public class DatingTimeManager : MonoBehaviour
    {
        public static DatingTimeManager Instance { get; private set; }

        // --- 데이터 상태 (SaveData 연동용) ---
        private int currentStamina;
        private int maxStamina;
        private int currentAffection;
        private int currentObsession;
        private int storyProgressStage;
        private int currentTimeSlot;
        private int currentDay;

        // --- 프로퍼티 (읽기 전용) ---
        public int CurrentStamina => currentStamina;
        public int MaxStamina => maxStamina;
        public int CurrentAffection => currentAffection;
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
                // DontDestroyOnLoad(gameObject); // 필요한 경우 주석 해제 (전역 유지)
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
            data.DatingObsession = currentObsession;
            data.StoryProgressStage = storyProgressStage;
            data.DatingTimeSlot = currentTimeSlot;
            data.DatingDay = currentDay;
        }

        // --- 핵심 로직 메서드 ---

        /// <summary>시간 슬롯을 소모합니다. 부족하면 false 반환</summary>
        public bool TryConsumeTimeSlot(int cost)
        {
            if (currentTimeSlot < cost) return false;
            
            currentTimeSlot -= cost;
            OnTimeSlotChanged?.Invoke(currentTimeSlot);
            
            // 데이터 변경 시 자동 저장 플래그 혹은 직접 저장
            SaveLoadManager.Instance?.SaveCurrentGame();
            return true;
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

        /// <summary>호감도를 증감시킵니다.</summary>
        public void ModifyAffection(int amount)
        {
            currentAffection += amount;
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

        /// <summary>하루를 넘깁니다. 타임 슬롯을 리셋하고 날짜를 증가시킵니다.</summary>
        public void AdvanceDay(int defaultTimeSlots = 5)
        {
            currentDay++;
            currentTimeSlot = defaultTimeSlots;
            
            OnDayChanged?.Invoke(currentDay);
            OnTimeSlotChanged?.Invoke(currentTimeSlot);
            
            SaveLoadManager.Instance?.SaveCurrentGame();
        }
    }
}
