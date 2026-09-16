using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using FXOverdose.DatingSim.Core;
using FXOverdose.DatingSim.Dialogue;

namespace FXOverdose.DatingSim.YomiRoom
{
    public enum YomiRoomHotspotType
    {
        Bed,
        Computer,
        Phone,
        Door,
        Yomi
    }

    /// <summary>
    /// 포인트 앤 클릭 방의 입력층입니다. 핫스팟 클릭을 <see cref="YomiRoomManager"/> API로 옮기기만 하고 상태는 갖지 않습니다.
    /// 규칙은 docs/P2_04_System/YomiRoom_PointAndClick_Renewal_Plan.md 3.3~3.5절.
    ///
    /// 모달·포지션 확인·피드백 로직은 <see cref="YomiRoomTopDownController"/>에서 옮겨 왔고, 근접 판정·이동은 버렸습니다.
    /// </summary>
    public sealed class YomiRoomHotspotController : MonoBehaviour
    {
        public const string SpriteRoot = "DatingSim/YomiRoom/PointClick";

        private Image background;
        private TMP_Text backgroundLabel;
        private TMP_Text feedbackText;
        private GameObject modal;
        private TMP_Text modalTitle;
        private TMP_Text modalBody;
        private Button confirmButton;
        private Button secondaryButton;
        private TMP_Text confirmLabel;
        private TMP_Text secondaryLabel;
        private CanvasGroup dialoguePanel;
        private Button dialogueCloseButton;
        private YomiRoomHotspotType? pending;

        private bool DialogueOpen => dialoguePanel != null && dialoguePanel.blocksRaycasts;

        public void Configure(Image roomBackground, TMP_Text roomBackgroundLabel, TMP_Text feedback,
            GameObject modalObject, TMP_Text title, TMP_Text body, Button confirm, Button secondary,
            CanvasGroup dialogue, Button dialogueClose)
        {
            background = roomBackground;
            backgroundLabel = roomBackgroundLabel;
            feedbackText = feedback;
            modal = modalObject;
            modalTitle = title;
            modalBody = body;
            confirmButton = confirm;
            secondaryButton = secondary;
            confirmLabel = confirm != null ? confirm.GetComponentInChildren<TMP_Text>() : null;
            secondaryLabel = secondary != null ? secondary.GetComponentInChildren<TMP_Text>() : null;
            dialoguePanel = dialogue;
            dialogueCloseButton = dialogueClose;

            // 조립 직후 숨겨 한 프레임 깜빡임을 막습니다.
            // ⚠️ SetActive(false)로 숨기면 안 됩니다 — YomiRoomDialogueUI.Start()가 이벤트 구독과 입장 인사를 하므로
            //    핸드폰을 누르기 전까지 인사·실패 사유가 전부 유실됩니다. (계획서 3.5)
            SetDialogueOpen(false);
        }

        private void Start()
        {
            DatingTimeManager time = DatingTimeManager.Instance;
            if (time != null) time.OnTimeSlotChanged += RefreshBackground;
            RefreshBackground(time != null ? time.CurrentTimeSlot : DatingTimeManager.DefaultTimeSlots);

            YomiRoomManager manager = YomiRoomManager.Instance;
            if (manager != null)
            {
                manager.OnStateChanged += HandleStateChanged;
                HandleStateChanged(manager.CurrentState);
            }
        }

        private void OnDestroy()
        {
            DatingTimeManager time = DatingTimeManager.Instance;
            if (time != null) time.OnTimeSlotChanged -= RefreshBackground;

            YomiRoomManager manager = YomiRoomManager.Instance;
            if (manager != null) manager.OnStateChanged -= HandleStateChanged;
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;

            // 모달 → 대화 패널 순서로 닫습니다.
            if (modal != null && modal.activeSelf) CloseModal();
            else if (DialogueOpen && dialogueCloseButton != null && dialogueCloseButton.interactable) CloseDialogue();
        }

        /// <summary>
        /// Resources의 스프라이트를 읽습니다. 없으면 null — 호출부가 플레이스홀더로 대체합니다.
        /// LoadAll인 이유: 감정 스프라이트가 Multiple 모드라 Load&lt;Sprite&gt;가 null을 돌려줍니다 (EventView.LoadEmotionSprite 참고).
        /// </summary>
        public static Sprite LoadSprite(string resourcePath)
        {
            Sprite[] loaded = Resources.LoadAll<Sprite>(resourcePath);
            return loaded != null && loaded.Length > 0 ? loaded[0] : null;
        }

        /// <summary>
        /// 대화·휴식·씬 전환 중에는 상호작용을 열지 않습니다. (F-1)
        /// 매니저의 행동은 Idle이 아니면 조용히 no-op 하므로, 이 가드가 없으면 눌러도 아무 일이 없는 버튼이 됩니다.
        /// </summary>
        private static bool RoomBusy
        {
            get
            {
                YomiRoomManager manager = YomiRoomManager.Instance;
                return manager != null && manager.CurrentState != YomiRoomState.Idle;
            }
        }

        public void HandleHotspot(YomiRoomHotspotType type)
        {
            if (RoomBusy || DialogueOpen || (modal != null && modal.activeSelf)) return;

            switch (type)
            {
                case YomiRoomHotspotType.Bed:
                    DatingTimeManager time = DatingTimeManager.Instance;
                    OpenModal(type, "침대", "잠깐 눈만 붙일까, 오늘은 여기까지 할까?\n\n눈 붙이기: 체력 +10 / 슬롯 -1      취침: 하루 마감",
                        "잠깐 눈 붙이기", time != null && time.CurrentTimeSlot >= 1, "오늘은 여기까지");
                    break;
                case YomiRoomHotspotType.Computer:
                    OpenModal(type, "컴퓨터", "PC를 켜고 트레이딩을 시작할까요?\n\n시간 슬롯 소모 없음", "확인", true, null);
                    break;
                case YomiRoomHotspotType.Door:
                    OpenModal(type, "문", "밖으로 나가 월드맵으로 이동할까요?", "나가기", true, null);
                    break;
                case YomiRoomHotspotType.Phone:
                    SetDialogueOpen(true);
                    break;
                case YomiRoomHotspotType.Yomi:
                    // TODO(P2): 요미 클릭 상호작용 — 추후 설계. 클릭 수신만 연결해 둡니다.
                    break;
            }
        }

        private void OpenModal(YomiRoomHotspotType type, string title, string body,
            string primaryLabel, bool primaryEnabled, string secondaryText)
        {
            pending = type;
            modalTitle.text = title;
            modalBody.text = body;
            if (confirmLabel != null) confirmLabel.text = primaryLabel;
            confirmButton.interactable = primaryEnabled;

            bool hasSecondary = secondaryText != null;
            secondaryButton.gameObject.SetActive(hasSecondary);
            if (hasSecondary && secondaryLabel != null) secondaryLabel.text = secondaryText;

            modal.SetActive(true);
        }

        /// <summary>
        /// 주 행동. 침대는 휴식, 컴퓨터는 거래 개시, 문은 월드맵.
        /// ⚠️ 피드백을 행동보다 먼저 출력하지 마십시오 — 행동은 실패할 수 있습니다. (I-2)
        /// </summary>
        public void ConfirmInteraction()
        {
            YomiRoomManager manager = YomiRoomManager.Instance;
            if (pending == null || manager == null) return;
            YomiRoomHotspotType type = pending.Value;
            CloseModal();

            switch (type)
            {
                case YomiRoomHotspotType.Computer:
                    if (!EnsureNoOpenPosition()) return;
                    if (!manager.StartTrading()) ShowFeedback("지금은 자리를 뜰 수 없어요.");
                    break;
                case YomiRoomHotspotType.Door:
                    if (!manager.MoveToWorldMap()) ShowFeedback("지금은 나갈 수 없어요.");
                    break;
                case YomiRoomHotspotType.Bed:
                    ShowFeedback(manager.TryRest()
                        ? "잠시 쉬었습니다. 체력이 10 회복됐어요."
                        : "지금은 쉴 수 없어요.");
                    break;
            }
        }

        /// <summary>보조 행동. 침대의 취침 — 하루를 마감합니다. (Q1)</summary>
        public void SecondaryInteraction()
        {
            YomiRoomManager manager = YomiRoomManager.Instance;
            if (pending != YomiRoomHotspotType.Bed || manager == null)
            {
                CloseModal();
                return;
            }

            CloseModal();
            ShowFeedback(manager.TrySleep()
                ? "오늘은 여기까지. 잠들었습니다..."
                : "지금은 잠들 수 없어요.");
        }

        public void CloseModal()
        {
            if (modal != null) modal.SetActive(false);
            pending = null;
        }

        public void CloseDialogue() => SetDialogueOpen(false);

        private void SetDialogueOpen(bool open)
        {
            if (dialoguePanel == null) return;
            dialoguePanel.alpha = open ? 1f : 0f;
            dialoguePanel.interactable = open;
            dialoguePanel.blocksRaycasts = open;
        }

        /// <summary>
        /// 대화 진행 중에는 닫기를 막습니다. 선택지 도중 패널만 닫히면 상태가 Chatting에 묶여 핫스팟이 전부 잠깁니다.
        /// 대화를 끝내는 수단은 패널 안의 "대화 종료" 토글입니다. (계획서 3.5)
        /// </summary>
        private void HandleStateChanged(YomiRoomState state)
        {
            if (dialogueCloseButton == null) return;
            dialogueCloseButton.interactable = state != YomiRoomState.Chatting && state != YomiRoomState.Responding;
        }

        /// <summary>
        /// 시간대별 배경 교체. 판정은 대화 토픽과 같은 <see cref="YomiTalkTopics.TimeOfSlot"/>을 씁니다 —
        /// 따로 판정하면 "밤 대사인데 아침 배경"이 됩니다. 해당 시간대가 없으면 Morning, 그것도 없으면 플레이스홀더.
        /// </summary>
        private void RefreshBackground(int remainingSlots)
        {
            if (background == null) return;

            string time = YomiTalkTopics.TimeOfSlot(remainingSlots).ToString();
            Sprite sprite = LoadSprite($"{SpriteRoot}/{time}/Room") ?? LoadSprite($"{SpriteRoot}/Morning/Room");

            background.sprite = sprite;
            background.color = sprite != null ? Color.white : PlaceholderColorFor(time);
            if (backgroundLabel != null) backgroundLabel.text = sprite != null ? string.Empty : $"{time} 배경 (임시)";
        }

        private static Color PlaceholderColorFor(string time)
        {
            switch (time)
            {
                case "Morning": return new Color32(74, 58, 48, 255);
                case "Noon": return new Color32(107, 90, 68, 255);
                case "Evening": return new Color32(90, 46, 42, 255);
                default: return new Color32(11, 17, 27, 255);
            }
        }

        /// <summary>
        /// 포지션을 들고 데이팅 씬으로 도망쳐 청산을 회피하는 것을 막습니다. (SV-B13 / S18)
        /// </summary>
        private bool EnsureNoOpenPosition()
        {
            var trading = FindAnyObjectByType<FXOverdose.Trading.TradingController>(FindObjectsInactive.Include);
            if (trading == null || !trading.IsActive) return true;

            ShowFeedback("포지션이 열려 있어요. 정리하고 나서 움직여요!");
            return false;
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
}
