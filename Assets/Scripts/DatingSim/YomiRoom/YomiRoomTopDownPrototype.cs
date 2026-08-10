using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FXOverdose.DatingSim.Core;
using FXOverdose.DatingSim.LLM;
using FXOverdose.DatingSim.LLM.Memory;

namespace FXOverdose.DatingSim.YomiRoom
{
    public enum YomiRoomInteractionType
    {
        TradingPC,
        RestBed
    }

    public sealed class YomiRoomInteractable : MonoBehaviour
    {
        public YomiRoomInteractionType Type { get; private set; }
        public string DisplayName { get; private set; }
        public string Description { get; private set; }
        public string CostText { get; private set; }

        public void Configure(YomiRoomInteractionType type, string displayName, string description, string costText)
        {
            Type = type;
            DisplayName = displayName;
            Description = description;
            CostText = costText;
        }
    }

    /// <summary>아트 교체 전 동선과 상호작용을 검증하는 P2_03 탑다운 방 MVP입니다.</summary>
    public sealed class YomiRoomTopDownController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 4.2f;
        [SerializeField] private float interactionRadius = 1.45f;

        private Rigidbody2D body;
        private Animator animator;
        private Vector2 input;
        private YomiRoomInteractable nearby;
        private YomiRoomInteractable pending;
        private TMP_Text promptText;
        private TMP_Text feedbackText;
        private TMP_Text modalTitle;
        private TMP_Text modalBody;
        private GameObject modal;
        private Button confirmButton;
        private bool inputLocked;

        public Vector2 MoveInput => input;

        public void Configure(Rigidbody2D playerBody, TMP_Text prompt, TMP_Text feedback,
            GameObject modalObject, TMP_Text title, TMP_Text bodyText, Button confirm)
        {
            body = playerBody;
            animator = body != null ? body.GetComponent<Animator>() : null;
            promptText = prompt;
            feedbackText = feedback;
            modal = modalObject;
            modalTitle = title;
            modalBody = bodyText;
            confirmButton = confirm;
        }

        private void Update()
        {
            if (body == null) return;

            Vector2 keyboard = Vector2.zero;
            Keyboard kb = Keyboard.current;
            bool typing = EventSystem.current != null &&
                          EventSystem.current.currentSelectedGameObject != null &&
                          EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() != null;
            if (!inputLocked && !typing && kb != null)
            {
                keyboard.x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
                keyboard.y = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
                if (kb.escapeKey.wasPressedThisFrame && modal != null && modal.activeSelf)
                    CloseModal();
            }

            Mouse mouse = Mouse.current;
            bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!inputLocked && !pointerOverUI && mouse != null && mouse.leftButton.wasPressedThisFrame && nearby != null)
                OpenInteraction(nearby);

            input = Vector2.ClampMagnitude(keyboard, 1f);
            UpdateNearby();
            UpdateVisualDirection();
        }

        private void FixedUpdate()
        {
            if (body != null)
                body.MovePosition(body.position + input * (moveSpeed * Time.fixedDeltaTime));
        }

        private void UpdateNearby()
        {
            YomiRoomInteractable best = null;
            float bestDistance = interactionRadius;
            foreach (YomiRoomInteractable candidate in FindObjectsByType<YomiRoomInteractable>(FindObjectsSortMode.None))
            {
                float distance = Vector2.Distance(body.position, candidate.transform.position);
                if (distance >= bestDistance) continue;
                best = candidate;
                bestDistance = distance;
            }

            nearby = best;
            if (promptText != null)
            {
                promptText.gameObject.SetActive(nearby != null && !inputLocked);
                if (nearby != null) promptText.text = $"마우스 좌클릭  ·  {nearby.DisplayName} 사용";
            }
        }

        private void UpdateVisualDirection()
        {
            if (body == null || input.sqrMagnitude < 0.01f) return;
            if (animator != null)
            {
                animator.SetFloat("MoveX", input.x);
                animator.SetFloat("MoveY", input.y);
                animator.SetFloat("Speed", input.sqrMagnitude);
            }
        }

        private void OpenInteraction(YomiRoomInteractable target)
        {
            pending = target;
            inputLocked = true;
            input = Vector2.zero;
            modalTitle.text = target.DisplayName;
            modalBody.text = $"{target.Description}\n\n{target.CostText}";
            confirmButton.interactable = CanExecute(target.Type);
            modal.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(confirmButton.gameObject);
        }

        private static bool CanExecute(YomiRoomInteractionType type)
        {
            DatingTimeManager time = DatingTimeManager.Instance;
            if (type == YomiRoomInteractionType.RestBed)
                return time != null && time.CurrentTimeSlot >= 1;
            return true;
        }

        public void ConfirmInteraction()
        {
            if (pending == null || YomiRoomManager.Instance == null) return;
            YomiRoomInteractionType type = pending.Type;
            CloseModal(false);

            switch (type)
            {
                case YomiRoomInteractionType.TradingPC:
                    YomiRoomManager.Instance.StartTrading();
                    break;
                case YomiRoomInteractionType.RestBed:
                    YomiRoomManager.Instance.TryRest();
                    ShowFeedback("잠시 쉬었습니다. 체력이 10 회복됐어요.");
                    inputLocked = false;
                    break;
            }
        }

        public void CloseModal() => CloseModal(true);

        private void CloseModal(bool unlock)
        {
            if (modal != null) modal.SetActive(false);
            pending = null;
            if (unlock) inputLocked = false;
        }

        private void ShowFeedback(string message)
        {
            if (feedbackText == null) return;
            feedbackText.text = message;
            CancelInvoke(nameof(ClearFeedback));
            Invoke(nameof(ClearFeedback), 2.5f);
        }

        private void ClearFeedback()
        {
            if (feedbackText != null) feedbackText.text = string.Empty;
        }
    }

    public sealed class YomiRoomTopDownHUD : MonoBehaviour
    {
        private TMP_Text stamina;
        private TMP_Text slots;
        private TMP_Text affection;
        private TMP_Text obsession;

        public void Configure(TMP_Text staminaText, TMP_Text slotText, TMP_Text affectionText, TMP_Text obsessionText)
        {
            stamina = staminaText;
            slots = slotText;
            affection = affectionText;
            obsession = obsessionText;
        }

        private void Start()
        {
            DatingTimeManager time = DatingTimeManager.Instance;
            if (time == null) return;
            time.OnStaminaChanged += UpdateStamina;
            time.OnTimeSlotChanged += UpdateSlots;
            time.OnAffectionChanged += UpdateAffection;
            time.OnObsessionChanged += UpdateObsession;
            UpdateStamina(time.CurrentStamina, time.MaxStamina);
            UpdateSlots(time.CurrentTimeSlot);
            UpdateAffection(time.CurrentAffection);
            UpdateObsession(time.CurrentObsession);
        }

        private void OnDestroy()
        {
            DatingTimeManager time = DatingTimeManager.Instance;
            if (time == null) return;
            time.OnStaminaChanged -= UpdateStamina;
            time.OnTimeSlotChanged -= UpdateSlots;
            time.OnAffectionChanged -= UpdateAffection;
            time.OnObsessionChanged -= UpdateObsession;
        }

        private void UpdateStamina(int value, int max)
        {
            if (stamina != null) stamina.text = $"STAMINA  {value} / {max}";
        }

        private void UpdateSlots(int value)
        {
            if (slots != null) slots.text = $"TIME SLOT  {value}";
        }

        private void UpdateAffection(int value)
        {
            if (affection != null) affection.text = $"AFFECTION  {value}";
        }

        private void UpdateObsession(int value)
        {
            if (obsession != null) obsession.text = $"OBSESSION  {value}";
        }
    }

    /// <summary>맵 좌측 여백에 표시되는 세로형 미연시 상태 카드 묶음입니다.</summary>
    public sealed class YomiRoomVerticalStatusUI : MonoBehaviour
    {
        private TMP_Text staminaValue;
        private TMP_Text timeValue;
        private TMP_Text affectionValue;
        private TMP_Text obsessionValue;
        private Image staminaFill;
        private Image affectionFill;
        private Image obsessionFill;
        private Image[] timePips;

        public void Configure(TMP_Text stamina, TMP_Text time, TMP_Text affection, TMP_Text obsession,
            Image staminaBar, Image affectionBar, Image obsessionBar, Image[] pips)
        {
            staminaValue = stamina;
            timeValue = time;
            affectionValue = affection;
            obsessionValue = obsession;
            staminaFill = staminaBar;
            affectionFill = affectionBar;
            obsessionFill = obsessionBar;
            timePips = pips;
        }

        private void Start()
        {
            DatingTimeManager time = DatingTimeManager.Instance;
            if (time == null) return;
            time.OnStaminaChanged += UpdateStamina;
            time.OnTimeSlotChanged += UpdateTime;
            time.OnAffectionChanged += UpdateAffection;
            time.OnObsessionChanged += UpdateObsession;
            UpdateStamina(time.CurrentStamina, time.MaxStamina);
            UpdateTime(time.CurrentTimeSlot);
            UpdateAffection(time.CurrentAffection);
            UpdateObsession(time.CurrentObsession);
        }

        private void OnDestroy()
        {
            DatingTimeManager time = DatingTimeManager.Instance;
            if (time == null) return;
            time.OnStaminaChanged -= UpdateStamina;
            time.OnTimeSlotChanged -= UpdateTime;
            time.OnAffectionChanged -= UpdateAffection;
            time.OnObsessionChanged -= UpdateObsession;
        }

        private void UpdateStamina(int current, int max)
        {
            if (staminaValue != null) staminaValue.text = $"{current}/{max}";
            if (staminaFill != null) staminaFill.fillAmount = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
        }

        private void UpdateTime(int current)
        {
            int clamped = Mathf.Clamp(current, 0, 5);
            if (timeValue != null) timeValue.text = $"{clamped}/5";
            if (timePips == null) return;
            for (int i = 0; i < timePips.Length; i++)
            {
                if (timePips[i] == null) continue;
                timePips[i].color = i < clamped
                    ? new Color32(250, 184, 45, 255)
                    : new Color32(53, 62, 70, 255);
            }
        }

        private void UpdateAffection(int value)
        {
            int clamped = Mathf.Clamp(value, 0, 100);
            if (affectionValue != null) affectionValue.text = $"{clamped}%";
            if (affectionFill != null) affectionFill.fillAmount = clamped / 100f;
        }

        private void UpdateObsession(int value)
        {
            int clamped = Mathf.Clamp(value, 0, 100);
            if (obsessionValue != null) obsessionValue.text = $"{clamped}%";
            if (obsessionFill != null) obsessionFill.fillAmount = clamped / 100f;
        }
    }

    /// <summary>생성된 4방향 3프레임 시트를 Rigidbody2D 이동량에 맞춰 재생합니다.</summary>
    public sealed class YomiTopDownWalkAnimator : MonoBehaviour
    {
        private Rigidbody2D body;
        private SpriteRenderer renderer;
        private Sprite[] frames;
        private YomiRoomTopDownController controller;
        private int direction;
        private float animationTime;

        public void Configure(Rigidbody2D targetBody, SpriteRenderer targetRenderer, Sprite[] walkFrames)
        {
            body = targetBody;
            renderer = targetRenderer;
            frames = walkFrames;
            controller = body != null ? body.transform.parent.GetComponent<YomiRoomTopDownController>() : null;
            direction = 0;
            if (renderer != null && frames != null && frames.Length >= 2)
                renderer.sprite = frames[1];
        }

        private void LateUpdate()
        {
            if (body == null || renderer == null || frames == null || frames.Length < 12) return;

            if (controller == null && body.transform.parent != null)
                controller = body.transform.parent.GetComponent<YomiRoomTopDownController>();

            Vector2 delta = controller != null ? controller.MoveInput : Vector2.zero;
            bool moving = delta.sqrMagnitude > 0.01f;
            if (moving)
            {
                if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y)) direction = delta.x < 0f ? 1 : 2;
                else direction = delta.y < 0f ? 0 : 3;

                animationTime += Time.deltaTime * 7.5f;
                int[] walkOrder = { 0, 1, 2, 1 };
                int frame = walkOrder[Mathf.FloorToInt(animationTime) % walkOrder.Length];
                renderer.sprite = frames[(direction * 3) + frame];
            }
            else
            {
                animationTime = 0f;
                renderer.sprite = frames[(direction * 3) + 1];
            }
        }
    }

    /// <summary>방 우측에서 상시 사용하는 자유 대화 패널입니다.</summary>
    public sealed class YomiRoomDialogueUI : MonoBehaviour
    {
        private RectTransform messageContent;
        private TMP_InputField inputField;
        private Button sendButton;
        private ScrollRect historyScroll;
        private Sprite yomiBubble;
        private Sprite masterBubble;
        private Sprite avatarFrame;
        private Sprite portrait;
        private GameObject thinkingRow;
        private float contentHeight;
        private bool sending;
        private bool sessionStarted;

        public void Configure(RectTransform content, TMP_InputField input, Button send, ScrollRect scroll,
            Sprite yomiFrame, Sprite masterFrame, Sprite portraitFrame, Sprite yomiPortrait)
        {
            messageContent = content;
            inputField = input;
            sendButton = send;
            historyScroll = scroll;
            yomiBubble = yomiFrame;
            masterBubble = masterFrame;
            avatarFrame = portraitFrame;
            portrait = yomiPortrait;
        }

        private void Start()
        {
            if (sendButton != null) sendButton.onClick.AddListener(SendCurrentMessage);
            if (inputField != null) inputField.onSubmit.AddListener(_ => SendCurrentMessage());
            AppendLine("요미", "마스터, 오늘도 어서 오세요.\n기다리고 있었어요.", "#F472B6");
        }

        private void OnDestroy()
        {
            if (sendButton != null) sendButton.onClick.RemoveListener(SendCurrentMessage);
            if (inputField != null) inputField.onSubmit.RemoveAllListeners();
        }

        private async void SendCurrentMessage()
        {
            if (sending || inputField == null) return;
            string message = inputField.text.Trim();
            if (string.IsNullOrEmpty(message)) return;

            if (!sessionStarted)
            {
                if (DatingTimeManager.Instance == null || DatingTimeManager.Instance.CurrentTimeSlot < 1)
                {
                    AppendLine("SYSTEM", "남은 시간 슬롯이 부족합니다.", "#F87171");
                    return;
                }

                YomiRoomManager.Instance?.TryStartFreeChat();
                sessionStarted = true;
            }

            sending = true;
            sendButton.interactable = false;
            inputField.interactable = false;
            inputField.text = string.Empty;
            AppendLine("마스터", message, "#22D3EE");
            AppendLine("요미", "...", "#F472B6");

            string response;
            try
            {
                response = DatingSimLLMController.Instance != null
                    ? await DatingSimLLMController.Instance.GenerateChatAsync(message, MemoryTopic.Daily)
                    : "마스터, 지금은 대화 준비가 아직 안 됐어...";
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                response = "미안해, 잠깐 생각이 끊겼어. 다시 말해 줄래?";
            }

            RemoveThinkingLine();
            AppendLine("요미", response, "#F472B6");
            YomiRoomManager.Instance?.CompleteRoomInteraction();
            sending = false;
            sendButton.interactable = true;
            inputField.interactable = true;
            inputField.ActivateInputField();
        }

        private void AppendLine(string speaker, string message, string color)
        {
            if (messageContent == null) return;
            bool isYomi = speaker == "요미";
            bool isSystem = speaker == "SYSTEM";
            // 한글 장문도 말풍선 내부에서 줄바꿈될 공간을 충분히 확보합니다.
            float rowHeight = Mathf.Clamp(104f + (message.Length / 20) * 28f, 112f, 220f);
            GameObject row = new($"Message_{speaker}", typeof(RectTransform));
            row.transform.SetParent(messageContent, false);
            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, -contentHeight);
            rowRect.sizeDelta = new Vector2(0f, rowHeight);

            float bubbleMin = isYomi ? 0.14f : 0.26f;
            float bubbleMax = isYomi ? 0.82f : 0.97f;
            GameObject bubble = new("Bubble", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bubble.transform.SetParent(row.transform, false);
            RectTransform bubbleRect = bubble.GetComponent<RectTransform>();
            StretchRect(bubbleRect, new Vector2(bubbleMin, 0.05f), new Vector2(bubbleMax, 0.8f));
            Image bubbleImage = bubble.GetComponent<Image>();
            bubbleImage.sprite = isYomi ? yomiBubble : masterBubble;
            bubbleImage.type = Image.Type.Sliced;
            bubbleImage.color = isSystem ? new Color32(91, 32, 47, 255) : Color.white;
            bubbleImage.raycastTarget = false;

            if (isYomi)
            {
                AddImage(row.transform, "AvatarFrame", avatarFrame, new Vector2(0.015f, 0.2f), new Vector2(0.125f, 0.94f), Color.white);
                AddImage(row.transform, "Portrait", portrait, new Vector2(0.035f, 0.25f), new Vector2(0.105f, 0.88f), Color.white).preserveAspect = true;
            }

            TextMeshProUGUI label = AddText(row.transform, "Speaker", speaker, 16f,
                new Vector2(isYomi ? 0.15f : 0.66f, 0.78f), new Vector2(isYomi ? 0.5f : 0.97f, 1f), ColorUtility.TryParseHtmlString(color, out Color parsed) ? parsed : Color.white);
            label.alignment = isYomi ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
            label.fontStyle = FontStyles.Bold;
            TextMeshProUGUI body = AddText(bubble.transform, "Body", message, 17f, new Vector2(0.075f, 0.14f), new Vector2(0.925f, 0.86f), new Color32(226, 235, 243, 255));
            body.alignment = TextAlignmentOptions.MidlineLeft;
            body.overflowMode = TextOverflowModes.Ellipsis;

            contentHeight += rowHeight + 10f;
            messageContent.sizeDelta = new Vector2(messageContent.sizeDelta.x, contentHeight);
            if (message == "...") thinkingRow = row;
            Canvas.ForceUpdateCanvases();
            if (historyScroll != null) historyScroll.verticalNormalizedPosition = 0f;
        }

        private void RemoveThinkingLine()
        {
            if (thinkingRow == null) return;
            RectTransform rect = thinkingRow.GetComponent<RectTransform>();
            contentHeight = Mathf.Max(0f, contentHeight - rect.sizeDelta.y - 10f);
            messageContent.sizeDelta = new Vector2(messageContent.sizeDelta.x, contentHeight);
            Destroy(thinkingRow);
            thinkingRow = null;
        }

        private static Image AddImage(Transform parent, string name, Sprite sprite, Vector2 min, Vector2 max, Color color)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            StretchRect(go.GetComponent<RectTransform>(), min, max);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI AddText(Transform parent, string name, string value, float size, Vector2 min, Vector2 max, Color color)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            StretchRect(go.GetComponent<RectTransform>(), min, max);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = value;
            text.fontSize = size;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Max(12f, size * 0.76f);
            text.fontSizeMax = size;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        private static void StretchRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    /// <summary>방 배경의 하단 통로에 닿으면 별도 문 클릭 없이 월드맵으로 이동합니다.</summary>
    public sealed class YomiRoomExitTrigger : MonoBehaviour
    {
        private bool transitioning;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (transitioning || other.GetComponent<Rigidbody2D>() == null) return;
            transitioning = true;
            YomiRoomManager.Instance?.MoveToWorldMap();
        }
    }

}
