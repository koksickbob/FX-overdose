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

        public void Configure(
            Button freeChat, Button rest, Button worldMap, Button trading,
            TextMeshProUGUI stamina, TextMeshProUGUI timeSlot,
            TextMeshProUGUI affection, TextMeshProUGUI obsession,
            TextMeshProUGUI state)
        {
            freeChatButton = freeChat;
            restButton = rest;
            worldMapButton = worldMap;
            tradingButton = trading;
            staminaText = stamina;
            timeSlotText = timeSlot;
            affectionText = affection;
            obsessionText = obsession;
            roomStateText = state;
        }

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
                freeChatButton.onClick.AddListener(() => YomiRoomManager.Instance?.TryStartChat());
            
            if (restButton != null)
                restButton.onClick.AddListener(() => YomiRoomManager.Instance?.TryRest());
            
            if (worldMapButton != null)
                worldMapButton.onClick.AddListener(() => YomiRoomManager.Instance?.MoveToWorldMap());
            
            if (tradingButton != null)
                tradingButton.onClick.AddListener(() => YomiRoomManager.Instance?.StartTrading());

            if (chatSendButton != null)
                chatSendButton.onClick.AddListener(OnChatSendClicked);
            
            if (closeChatButton != null)
                closeChatButton.onClick.AddListener(() => YomiRoomManager.Instance?.CloseChat());
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
                roomStateText.text = state switch
                {
                    YomiRoomState.Responding => "요미가 대답을 생각하는 중...",
                    YomiRoomState.Idle => "요미가 당신을 기다리고 있어요.",
                    YomiRoomState.Chatting => "요미와 대화하는 중...",
                    YomiRoomState.Resting => "잠시 쉬는 중...",
                    _ => "이동을 준비하고 있어요..."
                };
            }
            
            bool isIdle = (state == YomiRoomState.Idle);
            
            if (freeChatButton != null) freeChatButton.interactable = isIdle;
            if (restButton != null) restButton.interactable = isIdle;
            if (worldMapButton != null) worldMapButton.interactable = isIdle;
            if (tradingButton != null) tradingButton.interactable = isIdle;

            // 채팅창 활성화 제어
            bool isChatting = (state == YomiRoomState.Chatting || state == YomiRoomState.Responding);
            if (chatPanel != null) chatPanel.SetActive(isChatting);

            // 채팅 입력창 제어 (응답 대기 중엔 비활성화)
            bool canType = (state == YomiRoomState.Chatting);
            if (chatInputField != null) chatInputField.interactable = canType;
            if (chatSendButton != null) chatSendButton.interactable = canType;

            // 로딩 표시
            if (state == YomiRoomState.Responding && chatLogText != null)
            {
                chatLogText.text += "\n<color=yellow>[System] 요미가 타이핑 중...</color>";
            }
        }

        private void UpdateStaminaUI(int current, int max)
        {
            if (staminaText != null)
                staminaText.text = $"STAMINA  {current} / {max}";
        }

        private void UpdateTimeSlotUI(int currentSlots)
        {
            if (timeSlotText != null)
                timeSlotText.text = $"TIME SLOT  {currentSlots}";
        }

        private void UpdateAffectionUI(int affection)
        {
            if (affectionText != null)
                affectionText.text = $"AFFECTION  {affection}";
        }

        private void UpdateObsessionUI(int obsession)
        {
            if (obsessionText != null)
                obsessionText.text = $"OBSESSION  {obsession}";
        }

        private void HandleActionFailed()
        {
            if (roomStateText != null)
                roomStateText.text = "체력 또는 남은 시간 슬롯이 부족해요.";
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
                log += $"\n\n<b><color=#55AAFF>오빠:</color></b> {userMessage}\n<b><color=#FFAA55>요미:</color></b> {yomiResponse}";
                chatLogText.text = log;
            }
        }
    }
}
