using System;
using System.Collections.Generic;
using UnityEngine;
using FXOverdose.DatingSim.Core;
using FXOverdose.UI;
using UnityEngine.SceneManagement;
using FXOverdose.Core;

namespace FXOverdose.DatingSim.WorldMap
{
    [Serializable]
    public struct PartTimeJobData
    {
        public string jobName;
        public int staminaCost;
        public float rewardAmount;
    }

    /// <summary>알바 1회의 즉시 정산 결과. 월드맵 결과 모달이 그대로 보여줍니다.</summary>
    public struct PartTimeJobResult
    {
        public string JobName;
        public float Pay;
        public string GiftItemId;   // 선물이 없으면 null
        public string GiftName;
        public int SlotsBefore, SlotsAfter;
        public int StaminaBefore, StaminaAfter;
        public int TotalShifts;     // 이번 근무를 포함한 누적 근무 횟수 (세이브가 없으면 0)
        public bool SaveFailed;
    }

    [Serializable]
    public struct DateCourseData
    {
        public string courseName;
        public int timeSlotCost;
        public int staminaCost;
        public float moneyCost;
    }

    public class WorldMapManager : MonoBehaviour
    {
        public static WorldMapManager Instance { get; private set; }

        /// <summary>알바 1회가 쓰는 시간 슬롯. 기획서 고정값이며 월드맵 카드 표기도 이 값을 읽습니다.</summary>
        public const int PartTimeJobSlotCost = 2;

        /// <summary>근무 1회당 선물 확률. 타이쿤의 "계산 손님마다 5%"를 대신합니다 (2026-10-08 확정).</summary>
        public const float JobGiftChance = 0.35f;

        /// <summary>
        /// 알바 선물 후보. 당첨되면 이 중 하나를 1개 줍니다.
        /// ItemId는 상점 카탈로그의 ItemData.ItemId와 같아야 합니다 — 오타는 조용한 아이템 증발입니다.
        /// 검사: FXOverdose/Debug/Part-Time Job Test.
        /// </summary>
        public static readonly (string ItemId, string Name)[] JobGifts =
        {
            ("energy_drink", "에너지 드링크"),
            ("dessert", "파르페"),
        };

        [Header("Job & Date Configuration")]
        public List<PartTimeJobData> availableJobs = new List<PartTimeJobData>();
        public List<DateCourseData> availableDateCourses = new List<DateCourseData>();

        public event Action OnActionFailed; // 자원 부족 시 발생
        public event Action<string> OnDateStarted; // 데이트 진입 콜백
        public event Action<PartTimeJobResult> OnJobFinished; // 알바 즉시 정산 완료 — 결과 모달이 구독합니다

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            // 요미의 방과 같은 이유로 도착 즉시 한 번 저장합니다. SaveGame이 현재 씬을 찍으므로
            // 이것이 곧 복귀 지점 갱신입니다. (F-11)
            SaveLoadManager.Instance?.SaveCurrentGame();
        }

        public void TryStartPartTimeJob(int jobIndex)
        {
            if (jobIndex < 0 || jobIndex >= availableJobs.Count) return;
            
            var job = availableJobs[jobIndex];

            var time = DatingTimeManager.Instance;
            if (time == null) return;

            // 1. 체력 및 슬롯 사전 검사
            if (time.CurrentStamina < job.staminaCost || time.CurrentTimeSlot < PartTimeJobSlotCost)
            {
                OnActionFailed?.Invoke();
                return;
            }

            var result = new PartTimeJobResult
            {
                JobName = job.jobName,
                Pay = job.rewardAmount,
                SlotsBefore = time.CurrentTimeSlot,
                StaminaBefore = time.CurrentStamina,
            };

            // 2. 자원 차감 — 지급보다 먼저 합니다. 그 사이에 종료돼도 공짜 일급이 생기지 않습니다.
            time.TryConsumeTimeSlot(PartTimeJobSlotCost);
            time.TryConsumeStamina(job.staminaCost);

            // 3. 즉시 정산. 실시간 타이쿤 미니게임은 트레이딩과 피로가 겹쳐 폐기했습니다.
            //    (docs/P2_04_System/ConvenienceStore_Tycoon_Removal_Plan.md)
            PayWage(job.rewardAmount);

            var save = SaveLoadManager.Instance;
            if (save != null && UnityEngine.Random.value < JobGiftChance)
            {
                var gift = JobGifts[UnityEngine.Random.Range(0, JobGifts.Length)];
                if (save.GrantItemToSave(gift.ItemId))
                {
                    result.GiftItemId = gift.ItemId;
                    result.GiftName = gift.Name;
                }
            }
            if (save?.CurrentData != null) result.TotalShifts = ++save.CurrentData.StoreTotalShifts;

            // 일급·선물·누적 근무를 한 번에 디스크에 확정합니다.
            result.SaveFailed = save == null || !save.SaveCurrentGame();

            result.SlotsAfter = time.CurrentTimeSlot;
            result.StaminaAfter = time.CurrentStamina;
            OnJobFinished?.Invoke(result);
        }

        /// <summary>
        /// 일급을 트레이딩 코어 자산에 반영합니다. 상주 GameManager가 없으면 세이브 스냅샷에 직접 씁니다.
        ///
        /// ⚠️ 음수를 넣지 마십시오. 알바가 잔고를 깎는 행동이 되면 안 됩니다. (R12)
        /// </summary>
        private static void PayWage(float amount)
        {
            if (amount <= 0f) return;

            var gameManager = GameManager.Instance;
            if (gameManager != null)
            {
                gameManager.ChangeBalance(amount);
                return;
            }

            // GameManager가 없는 씬이므로 세이브 스냅샷에 직접 반영합니다. 확정은 호출부의 SaveCurrentGame이 합니다.
            if (SaveLoadManager.Instance?.CurrentData != null)
                SaveLoadManager.Instance.CurrentData.Balance += amount;
        }

        public void TryStartDateCourse(int courseIndex)
        {
            if (courseIndex < 0 || courseIndex >= availableDateCourses.Count) return;

            var course = availableDateCourses[courseIndex];

            if (DatingTimeManager.Instance == null) return;

            // 1. 체력 및 슬롯 검사 (사전)
            if (DatingTimeManager.Instance.CurrentStamina < course.staminaCost || DatingTimeManager.Instance.CurrentTimeSlot < course.timeSlotCost)
            {
                OnActionFailed?.Invoke();
                return;
            }

            // 2. 자금 검사 및 차감 (GameManager 연동)
            var gameManager = GameManager.Instance;
            bool paid;
            if (gameManager != null)
            {
                paid = gameManager.TrySpendBalance(course.moneyCost);
            }
            else if (SaveLoadManager.Instance?.CurrentData != null &&
                     SaveLoadManager.Instance.CurrentData.Balance >= course.moneyCost)
            {
                SaveLoadManager.Instance.CurrentData.Balance -= course.moneyCost;
                SaveLoadManager.Instance.SaveCurrentGame();
                paid = true;
            }
            else
            {
                paid = false;
            }

            if (!paid)
            {
                OnActionFailed?.Invoke(); // 잔고 부족
                return;
            }

            // 3. 실제 시간 및 체력 차감
            DatingTimeManager.Instance.TryConsumeTimeSlot(course.timeSlotCost);
            DatingTimeManager.Instance.TryConsumeStamina(course.staminaCost);
            
            // 4. 연출 트리거 (P2_04의 LoadingSceneManager를 통해 씬 전환 예정)
            OnDateStarted?.Invoke(course.courseName);
        }

        public void ReturnToRoom()
        {
            LoadingScreenController.TargetSceneToLoad = "YomiRoomScene";
            SceneManager.LoadScene("LoadingScene");
        }
    }
}
