using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FXOverdose.DatingSim.Core;

namespace FXOverdose.DatingSim.YomiRoom
{
    public class YomiRoomUIController : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button freeChatButton;
        [SerializeField] private Button restButton;
        [SerializeField] private Button worldMapButton;
        [SerializeField] private Button tradingButton;

        [Header("Status Texts")]
        [SerializeField] private TextMeshProUGUI staminaText;
        [SerializeField] private TextMeshProUGUI timeSlotText;
        [SerializeField] private TextMeshProUGUI affectionText;
        [SerializeField] private TextMeshProUGUI obsessionText;
        [SerializeField] private TextMeshProUGUI roomStateText;

        private void Start()
        {
            BindButtons();
            SubscribeEvents();
            InitUI();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        private void BindButtons()
        {
            if (freeChatButton != null)
                freeChatButton.onClick.AddListener(() => YomiRoomManager.Instance?.TryStartFreeChat());
            
            if (restButton != null)
                restButton.onClick.AddListener(() => YomiRoomManager.Instance?.TryRest());
            
            if (worldMapButton != null)
                worldMapButton.onClick.AddListener(() => YomiRoomManager.Instance?.MoveToWorldMap());
            
            if (tradingButton != null)
                tradingButton.onClick.AddListener(() => YomiRoomManager.Instance?.StartTrading());
        }

        private void SubscribeEvents()
        {
            if (YomiRoomManager.Instance != null)
            {
                YomiRoomManager.Instance.OnStateChanged += UpdateStateUI;
                YomiRoomManager.Instance.OnActionFailed += HandleActionFailed;
            }

            if (DatingTimeManager.Instance != null)
            {
                DatingTimeManager.Instance.OnStaminaChanged += UpdateStaminaUI;
                DatingTimeManager.Instance.OnTimeSlotChanged += UpdateTimeSlotUI;
                DatingTimeManager.Instance.OnAffectionChanged += UpdateAffectionUI;
                DatingTimeManager.Instance.OnObsessionChanged += UpdateObsessionUI;
            }
        }

        private void UnsubscribeEvents()
        {
            if (YomiRoomManager.Instance != null)
            {
                YomiRoomManager.Instance.OnStateChanged -= UpdateStateUI;
                YomiRoomManager.Instance.OnActionFailed -= HandleActionFailed;
            }

            if (DatingTimeManager.Instance != null)
            {
                DatingTimeManager.Instance.OnStaminaChanged -= UpdateStaminaUI;
                DatingTimeManager.Instance.OnTimeSlotChanged -= UpdateTimeSlotUI;
                DatingTimeManager.Instance.OnAffectionChanged -= UpdateAffectionUI;
                DatingTimeManager.Instance.OnObsessionChanged -= UpdateObsessionUI;
            }
        }

        private void InitUI()
        {
            if (YomiRoomManager.Instance != null)
                UpdateStateUI(YomiRoomManager.Instance.CurrentState);

            if (DatingTimeManager.Instance != null)
            {
                UpdateStaminaUI(DatingTimeManager.Instance.CurrentStamina, DatingTimeManager.Instance.MaxStamina);
                UpdateTimeSlotUI(DatingTimeManager.Instance.CurrentTimeSlot);
                UpdateAffectionUI(DatingTimeManager.Instance.CurrentAffection);
                UpdateObsessionUI(DatingTimeManager.Instance.CurrentObsession);
            }
        }

        private void UpdateStateUI(YomiRoomState state)
        {
            if (roomStateText != null)
                roomStateText.text = $"State: {state}";
            
            bool isIdle = (state == YomiRoomState.Idle);
            
            if (freeChatButton != null) freeChatButton.interactable = isIdle;
            if (restButton != null) restButton.interactable = isIdle;
            if (worldMapButton != null) worldMapButton.interactable = isIdle;
            if (tradingButton != null) tradingButton.interactable = isIdle;
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

        private void UpdateAffectionUI(int affection)
        {
            if (affectionText != null)
                affectionText.text = $"Affection: {affection}";
        }

        private void UpdateObsessionUI(int obsession)
        {
            if (obsessionText != null)
                obsessionText.text = $"Obsession: {obsession}";
        }

        private void HandleActionFailed()
        {
            Debug.LogWarning("[YomiRoomUI] Not enough time slots or stamina to perform action.");
        }
    }
}
