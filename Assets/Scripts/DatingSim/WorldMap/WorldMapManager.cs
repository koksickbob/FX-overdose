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

        private const string StoreSceneName = "ConvenienceStoreScene";

        [Header("Job & Date Configuration")]
        public List<PartTimeJobData> availableJobs = new List<PartTimeJobData>();
        public List<DateCourseData> availableDateCourses = new List<DateCourseData>();

        public event Action OnActionFailed; // 자원 부족 시 발생
        public event Action<string> OnDateStarted; // 데이트 진입 콜백
        // OnJobFinished 제거: 알바 결과는 편의점 씬의 결과 패널이 보여주며, 씬을 넘어온 뒤에는
        // 발행할 주체도 시점도 없습니다. 발화되지 않는 이벤트를 남겨 두지 않습니다.

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

            // 1.5 목적지 씬이 빌드에 등록돼 있는지 먼저 확인합니다.
            //     차감이 끝난 뒤에 로드가 실패하면 슬롯 2개와 체력만 날아가고 로딩 화면에 갇힙니다.
            //     자원을 쓰기 전에 갈 수 있는지부터 봐야 합니다.
            if (!Application.CanStreamedLevelBeLoaded(StoreSceneName))
            {
                Debug.LogError($"[WorldMap] {StoreSceneName}이 빌드 세팅에 없습니다. " +
                               "메뉴 'FX Overdose/Build Convenience Store Scene'을 먼저 실행하십시오.");
                OnActionFailed?.Invoke();
                return;
            }

            // 2. 자원 차감
            DatingTimeManager.Instance.TryConsumeTimeSlot(requiredSlots);
            DatingTimeManager.Instance.TryConsumeStamina(job.staminaCost);

            // 3. 편의점 타이쿤 미니게임으로 진입합니다. 보상은 근무 결과에 따라 그쪽에서 정산합니다.
            //    차감을 먼저 확정해야 씬 전환 중 종료해도 슬롯이 되살아나지 않습니다.
            SaveLoadManager.Instance?.SaveCurrentGame();

            // WorldMapManager는 씬 스코프라 편의점 씬에서는 조회할 수 없습니다. 기본급을 값으로 넘깁니다.
            FXOverdose.DatingSim.Store.StoreShiftManager.PendingBasePay = job.rewardAmount;

            LoadingScreenController.TargetSceneToLoad = StoreSceneName;
            SceneManager.LoadScene("LoadingScene");
        }

        /// <summary>
        /// 일급을 트레이딩 코어 자산에 반영합니다. 편의점 씬에는 이 매니저의 인스턴스가 없으므로 static입니다.
        ///
        /// ⚠️ 음수를 넣지 마십시오. 감점은 지급액을 줄일 뿐이며, 알바가 잔고를 깎는 행동이 되면 안 됩니다. (R12)
        /// </summary>
        public static void PayWage(float amount)
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
