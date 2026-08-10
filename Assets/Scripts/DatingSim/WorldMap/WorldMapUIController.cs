using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FXOverdose.DatingSim.Core;

namespace FXOverdose.DatingSim.WorldMap
{
    public class WorldMapUIController : MonoBehaviour
    {
        [Header("Top Bar UI")]
        [SerializeField] private TextMeshProUGUI staminaText;
        [SerializeField] private TextMeshProUGUI timeSlotText;
        [SerializeField] private TextMeshProUGUI balanceText;

        [Header("Actions")]
        [SerializeField] private Button firstJobButton;
        [SerializeField] private Button firstDateButton;
        
        [Header("Feedback")]
        [SerializeField] private TextMeshProUGUI feedbackText;

        private void Start()
        {
            BindButtons();
            SubscribeEvents();
            UpdateAllUI();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        private void BindButtons()
        {
            if (firstJobButton != null)
                firstJobButton.onClick.AddListener(() => WorldMapManager.Instance?.TryStartPartTimeJob(0));

            if (firstDateButton != null)
                firstDateButton.onClick.AddListener(() => WorldMapManager.Instance?.TryStartDateCourse(0));
        }

        private void SubscribeEvents()
        {
            if (DatingTimeManager.Instance != null)
            {
                DatingTimeManager.Instance.OnStaminaChanged += UpdateStaminaUI;
                DatingTimeManager.Instance.OnTimeSlotChanged += UpdateTimeSlotUI;
            }

            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.OnActionFailed += HandleActionFailed;
                WorldMapManager.Instance.OnJobFinished += HandleJobFinished;
                WorldMapManager.Instance.OnDateStarted += HandleDateStarted;
            }
        }

        private void UnsubscribeEvents()
        {
            if (DatingTimeManager.Instance != null)
            {
                DatingTimeManager.Instance.OnStaminaChanged -= UpdateStaminaUI;
                DatingTimeManager.Instance.OnTimeSlotChanged -= UpdateTimeSlotUI;
            }

            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.OnActionFailed -= HandleActionFailed;
                WorldMapManager.Instance.OnJobFinished -= HandleJobFinished;
                WorldMapManager.Instance.OnDateStarted -= HandleDateStarted;
            }
        }

        private void UpdateAllUI()
        {
            if (DatingTimeManager.Instance != null)
            {
                UpdateStaminaUI(DatingTimeManager.Instance.CurrentStamina, DatingTimeManager.Instance.MaxStamina);
                UpdateTimeSlotUI(DatingTimeManager.Instance.CurrentTimeSlot);
            }
            UpdateBalanceUI();
            
            if (feedbackText != null) feedbackText.text = "행동을 선택해 주세요.";
        }

        private void UpdateStaminaUI(int current, int max)
        {
            if (staminaText != null)
                staminaText.text = $"Stamina: {current} / {max}";
        }

        private void UpdateTimeSlotUI(int currentSlots)
        {
            if (timeSlotText != null)
                timeSlotText.text = $"Time Slots: {currentSlots}";
        }

        private void UpdateBalanceUI()
        {
            var gm = FindObjectOfType<GameManager>();
            if (gm != null && balanceText != null)
            {
                balanceText.text = $"Balance: ${gm.CurrentBalance:N0}";
            }
        }

        private void HandleActionFailed()
        {
            if (feedbackText != null) feedbackText.text = "행동 불가: 자원(체력/시간/자금)이 부족합니다.";
        }

        private void HandleJobFinished(string jobName, float reward)
        {
            UpdateBalanceUI();
            if (feedbackText != null) feedbackText.text = $"{jobName} 완료! 보상금: ${reward:N0}이 자산에 합산되었습니다.";
        }

        private void HandleDateStarted(string courseName)
        {
            UpdateBalanceUI();
            if (feedbackText != null) feedbackText.text = $"{courseName} 코스로 진입합니다...";
        }
    }
}
