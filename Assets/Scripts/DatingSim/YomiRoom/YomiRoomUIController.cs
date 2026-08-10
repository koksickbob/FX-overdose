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

        [Header("Chat UI")]
        [SerializeField] private GameObject chatPanel;
        [SerializeField] private TextMeshProUGUI chatLogText;
        [SerializeField] private TMP_InputField chatInputField;
        [SerializeField] private Button chatSendButton;
        [SerializeField] private Button closeChatButton;

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

            if (chatSendButton != null)
                chatSendButton.onClick.AddListener(OnChatSendClicked);
            
            if (closeChatButton != null)
                closeChatButton.onClick.AddListener(() => YomiRoomManager.Instance?.CloseFreeChat());
        }

        private void OnChatSendClicked()
        {
            if (chatInputField != null && !string.IsNullOrWhiteSpace(chatInputField.text))
            {
                YomiRoomManager.Instance?.ProcessUserChatInput(chatInputField.text);
                chatInputField.text = "";
            }
        }

        private void SubscribeEvents()
        {
            if (YomiRoomManager.Instance != null)
            {
                YomiRoomManager.Instance.OnStateChanged += UpdateStateUI;
                YomiRoomManager.Instance.OnChatUpdated += HandleChatUpdated;
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
                YomiRoomManager.Instance.OnChatUpdated -= HandleChatUpdated;
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
            {
                if (state == YomiRoomState.LLMLoading)
                    roomStateText.text = "State: LLM Loading...";
                else
                    roomStateText.text = $"State: {state}";
            }
            
            bool isIdle = (state == YomiRoomState.Idle);
            
            if (freeChatButton != null) freeChatButton.interactable = isIdle;
            if (restButton != null) restButton.interactable = isIdle;
            if (worldMapButton != null) worldMapButton.interactable = isIdle;
            if (tradingButton != null) tradingButton.interactable = isIdle;

            // 채팅창 활성화 제어
            bool isChatting = (state == YomiRoomState.FreeChatting || state == YomiRoomState.LLMProcessing);
            if (chatPanel != null) chatPanel.SetActive(isChatting);

            // 채팅 입력창 제어 (응답 대기 중엔 비활성화)
            bool canType = (state == YomiRoomState.FreeChatting);
            if (chatInputField != null) chatInputField.interactable = canType;
            if (chatSendButton != null) chatSendButton.interactable = canType;

            // 로딩 표시
            if (state == YomiRoomState.LLMProcessing && chatLogText != null)
            {
                chatLogText.text += "\n<color=yellow>[System] 요미가 타이핑 중...</color>";
            }
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

        private void HandleChatUpdated(string userMessage, string yomiResponse)
        {
            if (chatLogText != null)
            {
                // '타이핑 중...' 메시지 제거
                string log = chatLogText.text;
                log = log.Replace("\n<color=yellow>[System] 요미가 타이핑 중...</color>", "");
                
                // 새 메시지 추가
                log += $"\n\n<b><color=#55AAFF>마스터:</color></b> {userMessage}\n<b><color=#FFAA55>요미:</color></b> {yomiResponse}";
                chatLogText.text = log;
            }
        }
    }
}
