using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FXOverdose.DatingSim.Core;
using FXOverdose.DatingSim.Dialogue;

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
        private Button secondaryButton;
        private TMP_Text confirmLabel;
        private TMP_Text secondaryLabel;
        private bool inputLocked;

        // 방 안 상호작용 오브젝트는 개수가 고정적이므로 캐싱합니다. 매 프레임 씬 전체를 스캔하지 않기 위함입니다.
        private readonly List<YomiRoomInteractable> interactables = new List<YomiRoomInteractable>();

        public Vector2 MoveInput => input;

        public void Configure(Rigidbody2D playerBody, TMP_Text prompt, TMP_Text feedback,
            GameObject modalObject, TMP_Text title, TMP_Text bodyText, Button confirm, Button secondary = null)
        {
            body = playerBody;
            animator = body != null ? body.GetComponent<Animator>() : null;
            promptText = prompt;
            feedbackText = feedback;
            modal = modalObject;
            modalTitle = title;
            modalBody = bodyText;
            confirmButton = confirm;
            secondaryButton = secondary;
            confirmLabel = confirm != null ? confirm.GetComponentInChildren<TMP_Text>() : null;
            secondaryLabel = secondary != null ? secondary.GetComponentInChildren<TMP_Text>() : null;
            RefreshInteractables();
        }

        /// <summary>방 안 상호작용 오브젝트 목록을 다시 수집합니다. 오브젝트를 런타임에 추가/제거했다면 호출하십시오.</summary>
        public void RefreshInteractables()
        {
            interactables.Clear();
            interactables.AddRange(FindObjectsByType<YomiRoomInteractable>(FindObjectsInactive.Exclude));
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
            if (!inputLocked && !RoomBusy && !pointerOverUI && mouse != null && mouse.leftButton.wasPressedThisFrame && nearby != null)
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
            for (int i = 0; i < interactables.Count; i++)
            {
                YomiRoomInteractable candidate = interactables[i];
                if (candidate == null) continue; // 파괴된 오브젝트 방어

                float distance = Vector2.Distance(body.position, candidate.transform.position);
                if (distance >= bestDistance) continue;
                best = candidate;
                bestDistance = distance;
            }

            nearby = best;
            if (promptText != null)
            {
                promptText.gameObject.SetActive(nearby != null && !inputLocked && !RoomBusy);
                if (nearby != null) promptText.text = $"마우스 좌클릭  ·  {nearby.DisplayName} 사용";
            }
        }

        /// <summary>
        /// 대화·휴식 등으로 방이 다른 일을 하고 있는지. 이때는 상호작용을 열지 않습니다. (F-1)
        ///
        /// ⚠️ 이 가드가 없으면 대화 중에 PC/침대를 눌러 <b>조작이 영구히 잠깁니다.</b>
        ///    매니저의 StartTrading/TrySleep이 Idle이 아니면 조용히 no-op 하는데,
        ///    호출부는 이미 inputLocked를 켠 채 모달을 닫아버려 되돌릴 손잡이가 사라지기 때문입니다. (I-1)
        /// </summary>
        private static bool RoomBusy
        {
            get
            {
                YomiRoomManager manager = YomiRoomManager.Instance;
                return manager != null && manager.CurrentState != YomiRoomState.Idle;
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

            if (confirmLabel != null) confirmLabel.text = PrimaryLabelFor(target.Type);
            confirmButton.interactable = CanExecutePrimary(target.Type);

            if (secondaryButton != null)
            {
                bool hasSecondary = HasSecondaryAction(target.Type);
                secondaryButton.gameObject.SetActive(hasSecondary);
                if (hasSecondary && secondaryLabel != null) secondaryLabel.text = SecondaryLabelFor(target.Type);
            }

            modal.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(confirmButton.gameObject);
        }

        /// <summary>주 행동(왼쪽 버튼)을 실행할 수 있는지.</summary>
        private static bool CanExecutePrimary(YomiRoomInteractionType type)
        {
            DatingTimeManager time = DatingTimeManager.Instance;
            if (type == YomiRoomInteractionType.RestBed)
                return time != null && time.CurrentTimeSlot >= 1; // 휴식은 슬롯 1 소모
            return true;
        }

        /// <summary>보조 행동(가운데 버튼)이 있는 상호작용인지. 없으면 버튼을 숨깁니다.</summary>
        private static bool HasSecondaryAction(YomiRoomInteractionType type)
        {
            return type == YomiRoomInteractionType.RestBed; // 침대: 휴식 / 취침
        }

        private static string PrimaryLabelFor(YomiRoomInteractionType type)
        {
            return type == YomiRoomInteractionType.RestBed ? "잠깐 눈 붙이기" : "확인";
        }

        private static string SecondaryLabelFor(YomiRoomInteractionType type)
        {
            return type == YomiRoomInteractionType.RestBed ? "오늘은 여기까지" : string.Empty;
        }

        /// <summary>
        /// 주 행동. 침대는 휴식, PC는 거래 개시.
        ///
        /// ⚠️ <b>피드백을 행동보다 먼저 출력하지 마십시오.</b> 매니저의 행동은 실패할 수 있고,
        ///    실패했는데 성공 문구를 띄우면 플레이어는 체력이 회복된 줄 압니다. (I-2)
        ///    씬 전환에 실패하면 방에 남으므로 inputLocked도 반드시 되돌려야 합니다. (I-1)
        /// </summary>
        public void ConfirmInteraction()
        {
            if (pending == null || YomiRoomManager.Instance == null) return;
            YomiRoomInteractionType type = pending.Type;
            CloseModal(false);

            switch (type)
            {
                case YomiRoomInteractionType.TradingPC:
                    if (!EnsureNoOpenPosition()) return;
                    // 성공하면 씬이 넘어가므로 잠금을 유지합니다. 실패했을 때만 방에 남습니다.
                    if (!YomiRoomManager.Instance.StartTrading())
                    {
                        ShowFeedback("지금은 자리를 뜰 수 없어요.");
                        inputLocked = false;
                    }
                    break;
                case YomiRoomInteractionType.RestBed:
                    ShowFeedback(YomiRoomManager.Instance.TryRest()
                        ? "잠시 쉬었습니다. 체력이 10 회복됐어요."
                        : "지금은 쉴 수 없어요.");
                    inputLocked = false;
                    break;
            }
        }

        /// <summary>보조 행동. 침대의 취침 — 하루를 마감하고 다음 날로 넘어갑니다. (Q1)</summary>
        public void SecondaryInteraction()
        {
            if (pending == null || YomiRoomManager.Instance == null) return;
            YomiRoomInteractionType type = pending.Type;
            CloseModal(false);

            if (type != YomiRoomInteractionType.RestBed)
            {
                inputLocked = false;
                return;
            }

            // 취침도 실패할 수 있습니다. "잠들었습니다"를 먼저 띄우면 거짓말이 됩니다. (I-2)
            if (YomiRoomManager.Instance.TrySleep())
            {
                ShowFeedback("오늘은 여기까지. 잠들었습니다...");
                return;
            }

            ShowFeedback("지금은 잠들 수 없어요.");
            inputLocked = false;
        }

        /// <summary>
        /// 포지션을 들고 데이팅 씬으로 도망쳐 무기한 청산을 회피하는 것을 막습니다. (SV-B13 / S18)
        /// 요미의 방에서는 시세가 멈추므로, 열린 포지션이 있으면 씬을 넘나들 수 없어야 합니다.
        /// </summary>
        private bool EnsureNoOpenPosition()
        {
            var trading = FindAnyObjectByType<FXOverdose.Trading.TradingController>(FindObjectsInactive.Include);
            if (trading == null || !trading.IsActive) return true;

            ShowFeedback("포지션이 열려 있어요. 정리하고 나서 움직여요!");
            inputLocked = false;
            return false;
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

        // 선택지 버튼. 입력형 채팅을 대체합니다. (2026-08-13)
        private Button[] choiceButtons;
        private TMP_Text[] choiceLabels;

        // 버스트 출력 대기열. 요미의 말풍선을 한 번에 쏟지 않고 한 줄씩 흘립니다. (R-1)
        // 즉답과 다음 노드가 같은 프레임에 들어오므로, 코루틴 하나가 큐를 비우는 형태여야 순서가 지켜집니다.
        private readonly List<string> lineQueue = new List<string>();
        private Coroutine drainRoutine;
        private TalkChoice[] pendingChoices;
        private bool skipRequested;

        // ── 출력 연출 튜닝 노브 (11장 D-7) ──────────────────────────────
        // 실기로 봐야 정해지는 값들입니다. 흩어 두면 못 고치므로 한 블록에 모읍니다.
        private const float CharInterval = 0.045f;      // 글자 하나 (한글 초당 약 22자)
        private const float PauseComma = 0.08f;         // , 뒤
        private const float PausePeriod = 0.15f;        // . ! ? 뒤
        private const float PauseEllipsis = 0.35f;      // ... 뒤 — 요미의 머뭇거림이 여기서 나옵니다
        private const float LineTailBase = 0.25f;       // 줄 사이 여운 = Base + 글자수 * PerChar
        private const float LineTailPerChar = 0.012f;
        private const float LineTailMax = 0.8f;
        private const float NarrationTail = 0.55f;      // 지문은 타자기 없이 즉시 표시 후 이 텀 (D-4)
        private const float ChoiceDelay = 0.3f;         // 마지막 글자와 동시에 버튼이 튀어나오지 않게 (D-5)

        // 하단 버튼 하나가 상태에 따라 "자유대화"와 "대화 종료"를 겸합니다. (F-5)
        // 진행 중인 대화를 끊을 수단이 아예 없어서, 대화 중에는 방을 떠나지도 저장을 정리하지도 못했습니다.
        private Button talkToggleButton;
        private TMP_Text talkToggleLabel;

        public void Configure(RectTransform content, TMP_InputField input, Button send, ScrollRect scroll,
            Sprite yomiFrame, Sprite masterFrame, Sprite portraitFrame, Sprite yomiPortrait,
            Button[] choices = null, Button talkToggle = null)
        {
            messageContent = content;
            inputField = input;
            sendButton = send;
            historyScroll = scroll;
            yomiBubble = yomiFrame;
            masterBubble = masterFrame;
            avatarFrame = portraitFrame;
            portrait = yomiPortrait;

            talkToggleButton = talkToggle;
            if (talkToggleButton != null)
            {
                talkToggleLabel = talkToggleButton.GetComponentInChildren<TMP_Text>();
                talkToggleButton.onClick.AddListener(OnTalkToggleClicked);
            }

            choiceButtons = choices ?? new Button[0];
            choiceLabels = new TMP_Text[choiceButtons.Length];
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i] == null) continue;
                choiceLabels[i] = choiceButtons[i].GetComponentInChildren<TMP_Text>();
                int index = i; // 클로저 캡처 주의
                choiceButtons[i].onClick.AddListener(() => YomiRoomManager.Instance?.SelectChoice(index));
                choiceButtons[i].gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            // 입력형 채팅 폐지. 남아 있던 입력창과 전송 버튼은 숨깁니다.
            if (inputField != null) inputField.gameObject.SetActive(false);
            if (sendButton != null) sendButton.gameObject.SetActive(false);

            var manager = YomiRoomManager.Instance;
            if (manager != null)
            {
                manager.OnTalkNodeAdvanced += HandleTalkNode;
                manager.OnPlayerChoiceSpoken += HandlePlayerChoice;
                manager.OnYomiReplied += HandleYomiReply;
                manager.OnTalkFinished += HandleTalkFinished;
                manager.OnYomiGreeted += HandleGreeting;
                manager.OnActionFailed += HandleActionFailed;
                manager.OnStateChanged += HandleStateChanged;
                HandleStateChanged(manager.CurrentState);
                manager.TryGreetOnEnter();
            }
        }

        private void OnDestroy()
        {
            var manager = YomiRoomManager.Instance;
            if (manager != null)
            {
                manager.OnTalkNodeAdvanced -= HandleTalkNode;
                manager.OnPlayerChoiceSpoken -= HandlePlayerChoice;
                manager.OnYomiReplied -= HandleYomiReply;
                manager.OnTalkFinished -= HandleTalkFinished;
                manager.OnYomiGreeted -= HandleGreeting;
                manager.OnActionFailed -= HandleActionFailed;
                manager.OnStateChanged -= HandleStateChanged;
            }

            // 파괴된 UI에 코루틴이 계속 append하지 않도록 끊습니다. (TS12)
            StopAllCoroutines();
            lineQueue.Clear();
        }

        // 인사도 요미의 말이므로 같은 대기열을 탑니다. 타자기 연출이 공짜로 붙습니다.
        private void HandleGreeting(string line)
        {
            Enqueue(new[] { line });
        }

        // 대화 시작 실패(하루 1회 소진 / 체력 부족 / 토픽 없음)는 소리 없이 버튼만 죽으면
        // 버그로 읽히므로, 사유를 지문 채널로 한 줄 남깁니다.
        private void HandleActionFailed(string reason)
        {
            AppendNarration(reason);
        }

        /// <summary>하단 버튼 하나로 개시와 중단을 겸합니다. 상태에 따라 라벨과 동작이 갈립니다. (F-5)</summary>
        private void OnTalkToggleClicked()
        {
            YomiRoomManager manager = YomiRoomManager.Instance;
            if (manager == null) return;

            if (manager.HasActiveTopic)
            {
                // 출력 중이던 줄은 버립니다. 안 그러면 끊은 뒤에도 요미가 계속 말합니다. (TS12)
                StopAllCoroutines();
                lineQueue.Clear();
                drainRoutine = null;
                HideChoices();
                pendingChoices = null;

                manager.CloseTalk();
                AppendNarration("대화를 여기서 마쳤다.");
                return;
            }

            manager.TryStartTalk();
        }

        private void HandleStateChanged(YomiRoomState state)
        {
            if (talkToggleButton == null) return;

            bool chatting = state == YomiRoomState.Chatting || state == YomiRoomState.Responding;
            if (talkToggleLabel != null) talkToggleLabel.text = chatting ? "대화 종료" : "자유대화";
            // 씬 전환·휴식 중에는 누를 수 없어야 합니다. 눌러도 매니저가 막지만 버튼이 살아 있으면 눌러보게 됩니다.
            talkToggleButton.interactable = chatting || state == YomiRoomState.Idle;
        }

        private void HandleTalkNode(TalkNode node)
        {
            // 선택지는 말풍선을 다 흘린 뒤에 켭니다. 출력 중에 노출되면 이전 노드의 선택을 다시 누릅니다. (TS17)
            HideChoices();
            pendingChoices = node.Choices;
            Enqueue(node.YomiLines);
        }

        private void HandlePlayerChoice(string line)
        {
            AppendLine("오빠", line, "#22D3EE");
            HideChoices();
        }

        /// <summary>고른 선택지에 대한 요미의 즉답. 다음 노드보다 먼저 큐에 들어갑니다. (R-2)</summary>
        private void HandleYomiReply(string line)
        {
            Enqueue(new[] { line });
        }

        private void HandleTalkFinished(string closingLine, string hintLine)
        {
            HideChoices();
            pendingChoices = null;

            // 힌트는 별도 팝업 없이 같은 로그에 한 줄 더 이어 붙입니다.
            // 플레이어에게는 일상 대화 끝에 요미가 무심코 흘리는 예감으로 읽힙니다.
            Enqueue(string.IsNullOrEmpty(hintLine)
                ? new[] { closingLine }
                : new[] { closingLine, hintLine });
        }

        private void Enqueue(string[] lines)
        {
            if (lines == null || lines.Length == 0) return;
            lineQueue.AddRange(lines);
            if (drainRoutine == null) drainRoutine = StartCoroutine(DrainQueue());
        }

        /// <summary>
        /// 대기열을 한 줄씩 흘립니다. 즉답 → 다음 노드 대사가 같은 프레임에 쌓여도 순서가 지켜집니다.
        ///
        /// 줄은 통째로 뜨지 않고 <b>한 글자씩 찍힙니다</b>. 이전에는 0.4초 고정 간격으로 통째 출력이라
        /// 첫 줄을 읽기도 전에 셋째 줄이 도착했습니다. (11.1절)
        ///
        /// 스킵은 2단입니다 — 한 번 누르면 현재 줄이 즉시 완성되고, 한 번 더 누르면 다음 줄로 넘어갑니다. (D-6)
        /// </summary>
        private System.Collections.IEnumerator DrainQueue()
        {
            while (lineQueue.Count > 0)
            {
                string line = lineQueue[0];
                lineQueue.RemoveAt(0);

                if (messageContent == null) break; // 패널이 이미 파괴됨 (TS12)

                float tail;
                if (TalkNode.IsNarration(line))
                {
                    // 지문은 타자기를 쓰지 않습니다. 서술은 대사와 리듬이 달라야 하고,
                    // 타이핑까지 하면 늘어집니다. (D-4)
                    AppendNarration(TalkNode.StripMark(line));
                    tail = NarrationTail;
                }
                else
                {
                    TMP_Text body = AppendLine("요미", line, "#F472B6");
                    if (body != null) yield return TypeLine(body, line);
                    tail = Mathf.Min(LineTailMax, LineTailBase + line.Length * LineTailPerChar);
                }

                if (lineQueue.Count == 0) break;

                // 방금 줄을 완성시킨 클릭이 여운까지 삼키지 않도록 한 프레임 띄웁니다.
                yield return null;

                float waited = 0f;
                while (waited < tail)
                {
                    if (ClickedThisFrame()) break;
                    waited += Time.deltaTime;
                    yield return null;
                }
            }

            if (pendingChoices != null)
            {
                yield return new WaitForSeconds(ChoiceDelay);
                ShowChoices(pendingChoices);
                pendingChoices = null;
            }

            // 선택지가 뜨기 전에는 새 줄이 들어올 수 없으므로(선택 자체가 불가능) 여기서 비웁니다.
            drainRoutine = null;
        }

        /// <summary>한 글자씩 찍습니다. 구두점에서는 손이 멈춥니다. (D-1 / D-2)</summary>
        private System.Collections.IEnumerator TypeLine(TMP_Text body, string text)
        {
            skipRequested = false;
            body.maxVisibleCharacters = 0;

            for (int i = 0; i < text.Length; i++)
            {
                body.maxVisibleCharacters = i + 1;

                float wait = CharInterval + PauseAfter(text, i);
                float waited = 0f;
                while (waited < wait)
                {
                    if (ClickedThisFrame()) { skipRequested = true; break; }
                    waited += Time.deltaTime;
                    yield return null;
                }

                if (skipRequested) break;
            }

            body.maxVisibleCharacters = int.MaxValue; // 남은 글자 즉시 표시
        }

        /// <summary>
        /// 이 글자 뒤에 얼마나 쉴지. 요미 대사는 말줄임이 압도적으로 많아서,
        /// "..." 뒤의 정지가 머뭇거림을 그대로 연출로 만들어 줍니다. 대사는 한 줄도 안 고칩니다.
        /// </summary>
        private static float PauseAfter(string text, int index)
        {
            char c = text[index];

            if (c == '…') return PauseEllipsis;
            if (c == '.')
            {
                // 점이 이어지는 중간에서는 쉬지 않습니다. 점마다 멈추면 1초를 넘깁니다.
                if (index + 1 < text.Length && text[index + 1] == '.') return 0f;
                bool ellipsis = index >= 2 && text[index - 1] == '.' && text[index - 2] == '.';
                return ellipsis ? PauseEllipsis : PausePeriod;
            }
            if (c == '!' || c == '?') return PausePeriod;
            if (c == ',') return PauseComma;
            return 0f;
        }

        private static bool ClickedThisFrame()
        {
            return Pointer.current != null && Pointer.current.press.wasPressedThisFrame;
        }

        private void ShowChoices(TalkChoice[] choices)
        {
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i] == null) continue;

                bool used = choices != null && i < choices.Length;
                choiceButtons[i].gameObject.SetActive(used);
                if (used && choiceLabels[i] != null) choiceLabels[i].text = choices[i].Text;
            }
        }

        private void HideChoices()
        {
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i] != null) choiceButtons[i].gameObject.SetActive(false);
            }
        }

        /// <summary>말풍선 한 줄을 붙이고 <b>본문 TMP를 돌려줍니다</b>. 타자기 연출에 이 참조가 필요합니다. (D-a)</summary>
        private TextMeshProUGUI AppendLine(string speaker, string message, string color)
        {
            if (messageContent == null) return null;
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

            // 행 높이는 전체 문자열로 이미 확정됩니다. 타자기가 글자를 늘려도 레이아웃이 안 흔들립니다. (TS25)
            contentHeight += rowHeight + 10f;
            messageContent.sizeDelta = new Vector2(messageContent.sizeDelta.x, contentHeight);
            if (message == "...") thinkingRow = row;
            Canvas.ForceUpdateCanvases();
            if (historyScroll != null) historyScroll.verticalNormalizedPosition = 0f;
            return body;
        }

        /// <summary>
        /// 지문 줄. 말풍선도 화자도 없는 회색 서술입니다. (10.3절)
        /// 라이트 노벨의 대화가 자연스러운 이유의 절반은 대사 사이의 서술이고, 채팅 로그에는 그 채널이 없었습니다.
        /// </summary>
        private void AppendNarration(string message)
        {
            if (messageContent == null) return;

            float rowHeight = Mathf.Clamp(46f + (message.Length / 24) * 22f, 46f, 96f);
            GameObject row = new("Message_Narration", typeof(RectTransform));
            row.transform.SetParent(messageContent, false);
            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, -contentHeight);
            rowRect.sizeDelta = new Vector2(0f, rowHeight);

            TextMeshProUGUI body = AddText(row.transform, "Body", message, 15f,
                new Vector2(0.12f, 0.05f), new Vector2(0.88f, 0.95f), new Color32(139, 139, 154, 255));
            body.alignment = TextAlignmentOptions.Center;
            body.fontStyle = FontStyles.Italic;

            contentHeight += rowHeight + 10f;
            messageContent.sizeDelta = new Vector2(messageContent.sizeDelta.x, contentHeight);
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
