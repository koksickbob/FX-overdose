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

        [Header("Job & Date Configuration")]
        public List<PartTimeJobData> availableJobs = new List<PartTimeJobData>();
        public List<DateCourseData> availableDateCourses = new List<DateCourseData>();

        public event Action OnActionFailed; // 자원 부족 시 발생
        public event Action<string, float> OnJobFinished; // 알바 성공 콜백 (UI 연동용)
        public event Action<string> OnDateStarted; // 데이트 진입 콜백

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
            int requiredSlots = 2; // 기획서 고정 (알바는 2슬롯 소모)

            if (DatingTimeManager.Instance == null) return;
            
            // 1. 체력 및 슬롯 사전 검사
            if (DatingTimeManager.Instance.CurrentStamina < job.staminaCost || DatingTimeManager.Instance.CurrentTimeSlot < requiredSlots)
            {
                OnActionFailed?.Invoke();
                return;
            }

            // 2. 자원 차감
            DatingTimeManager.Instance.TryConsumeTimeSlot(requiredSlots);
            DatingTimeManager.Instance.TryConsumeStamina(job.staminaCost);
            
            // 3. 미니게임 컷(스킵) 및 즉시 보상 지급
            FinishPartTimeJob(true, jobIndex);
        }

        public void FinishPartTimeJob(bool success, int jobIndex)
        {
            if (!success) return;
            
            var job = availableJobs[jobIndex];
            
            // 트레이딩 코어 자산(Balance)에 합산
            var gameManager = FindAnyObjectByType<GameManager>();
            if (gameManager != null)
            {
                gameManager.ChangeBalance(job.rewardAmount);
            }
            else if (SaveLoadManager.Instance?.CurrentData != null)
            {
                // GameManager가 없는 씬이므로 세이브 스냅샷에 직접 반영한 뒤 즉시 기록합니다.
                SaveLoadManager.Instance.CurrentData.Balance += job.rewardAmount;
                SaveLoadManager.Instance.SaveCurrentGame();
            }

            OnJobFinished?.Invoke(job.jobName, job.rewardAmount);
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
            var gameManager = FindAnyObjectByType<GameManager>();
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
