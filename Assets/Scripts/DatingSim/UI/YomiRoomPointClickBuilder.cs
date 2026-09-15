using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FXOverdose.DatingSim.YomiRoom;
using P = FXOverdose.DatingSim.UI.YomiRoomTopDownPrototype;

namespace FXOverdose.DatingSim.UI
{
    /// <summary>
    /// 포인트 앤 클릭 요미의 방 런타임 조립기입니다. (계획서 YomiRoom_PointAndClick_Renewal_Plan.md)
    ///
    /// 에셋은 Resources/DatingSim/YomiRoom/PointClick/ 아래에 넣기만 하면 교체됩니다. 없으면 플레이스홀더로 그립니다.
    /// 규격(1920×1080 동일 캔버스, 트리밍 금지)은 docs/P2_05_UI_and_Art/P2_05_YomiRoom_PointClick_Asset_Order_Spec.md.
    /// 모달·상태 카드·대화 패널은 탑다운 조립기의 것을 그대로 씁니다.
    /// </summary>
    public static class YomiRoomPointClickBuilder
    {
        /// <summary>Phase 1~2 검증용 씬. 빌드 설정에 넣지 않습니다.</summary>
        public const string TestSceneName = "YomiRoom_PointClick_Test";

        private const string RootName = "Canvas_YomiRoomPointClick";
        private const string YomiSpritePath = "DatingSim/Emotions/Sprites/T1/Calm";

        private static readonly Color32 Cyan = new(34, 211, 238, 255);
        private static readonly Color32 Pink = new(244, 114, 182, 255);
        private static readonly Color32 Text = new(226, 245, 250, 255);

        public static void Build(Scene scene)
        {
            // 런타임 스프라이트를 씬 파일에 직렬화하지 않습니다.
            if (!Application.isPlaying) return;
            if (P.Find(scene, RootName) != null) return;

            GameObject oldCanvas = P.Find(scene, "Canvas_YomiRoom");
            if (oldCanvas != null)
            {
                oldCanvas.SetActive(false);
                Object.Destroy(oldCanvas);
            }

            Canvas canvas = P.CreateCanvas(scene, RootName);
            Transform ui = canvas.transform;
            YomiRoomHotspotController controller = canvas.gameObject.AddComponent<YomiRoomHotspotController>();

            // ── 방 영역 ─────────────────────────────────────────────
            // 16:9를 유지하며 화면을 덮습니다(넘치는 가장자리는 잘림). 레이어가 전부 같은 1920×1080 캔버스라 이 영역에 겹쳐 늘립니다.
            GameObject roomObject = new("RoomArea", typeof(RectTransform), typeof(AspectRatioFitter));
            roomObject.transform.SetParent(ui, false);
            AspectRatioFitter fitter = roomObject.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 16f / 9f;
            RectTransform room = (RectTransform)roomObject.transform;

            Image background = P.CreateUIImage(room, "RoomBackground", null, Color.black, Vector2.zero, Vector2.one);
            TextMeshProUGUI backgroundLabel = P.CreateText(room, "BackgroundLabel", string.Empty, 18f,
                new Vector2(0.4f, 0.94f), new Vector2(0.6f, 0.99f), Text);

            // 뒤 → 앞 순서. 겹치는 곳은 앞 레이어가 클릭을 가져갑니다. 가이드 좌표는 발주 명세서 §4.3 (왼쪽 위 원점 px).
            CreateHotspot(room, controller, YomiRoomHotspotType.Door, "문", "Door", 1400, 150, 1580, 700);
            CreateHotspot(room, controller, YomiRoomHotspotType.Phone, "핸드폰", "Phone", 1230, 480, 1390, 680);
            CreateHotspot(room, controller, YomiRoomHotspotType.Computer, "컴퓨터", "Computer", 60, 380, 600, 920);
            CreateHotspot(room, controller, YomiRoomHotspotType.Bed, "침대", "Bed", 1250, 620, 1920, 1080);
            // 요미: 발 y 940, 키 760px, 가로 중앙 (명세서 §4.4). 원본 887×1774 비율이라 폭은 380px.
            CreateHotspot(room, controller, YomiRoomHotspotType.Yomi, "요미", null, 770, 180, 1150, 940);

            // ── 오버레이 UI ─────────────────────────────────────────
            P.BuildVerticalStatusCards(ui);

            YomiRoomDialogueUI dialogue = P.BuildDialoguePanel(ui, new Vector2(0.625f, 0.03f), new Vector2(0.99f, 0.9f), false);
            CanvasGroup dialogueGroup = dialogue.gameObject.AddComponent<CanvasGroup>();
            Button dialogueClose = P.CreateButton(dialogue.transform, "CloseButton", "닫기",
                new Vector2(0.78f, 0.905f), new Vector2(0.945f, 0.965f), Pink);
            dialogueClose.onClick.AddListener(controller.CloseDialogue);

            // 대화 패널이 기본 숨김이라 설정 버튼은 패널 밖으로 뺍니다. 안에 두면 방에서 설정을 열 수 없습니다.
            P.BuildRoomSettingsButton(ui, new Vector2(0.958f, 0.915f), new Vector2(0.992f, 0.975f));

            TextMeshProUGUI feedback = P.CreateText(ui, "Feedback", string.Empty, 22f,
                new Vector2(0.34f, 0.02f), new Vector2(0.66f, 0.09f), Pink);

            GameObject modal = P.CreateModal(ui, out TextMeshProUGUI modalTitle, out TextMeshProUGUI modalBody,
                out Button confirm, out Button secondary, out Button cancel);
            confirm.onClick.AddListener(controller.ConfirmInteraction);
            secondary.onClick.AddListener(controller.SecondaryInteraction);
            cancel.onClick.AddListener(controller.CloseModal);
            modal.SetActive(false);
            modal.transform.SetAsLastSibling();

            controller.Configure(background, backgroundLabel, feedback, modal, modalTitle, modalBody,
                confirm, secondary, dialogueGroup, dialogueClose);
        }

        /// <summary>
        /// 핫스팟 하나. 에셋이 있으면 방 영역 전체로 늘린 레이어(요미만 가이드 영역 + 비율 유지),
        /// 없으면 가이드 영역에 반투명 사각형 + 라벨 플레이스홀더를 그립니다.
        /// </summary>
        private static void CreateHotspot(RectTransform room, YomiRoomHotspotController controller, YomiRoomHotspotType type,
            string label, string layerName, float x0, float y0, float x1, float y1)
        {
            Vector2 guideMin = new(x0 / 1920f, 1f - (y1 / 1080f));
            Vector2 guideMax = new(x1 / 1920f, 1f - (y0 / 1080f));

            Sprite sprite = YomiRoomHotspotController.LoadSprite(
                layerName != null ? $"{YomiRoomHotspotController.SpriteRoot}/{layerName}" : YomiSpritePath);

            Image image = P.CreateUIImage(room, $"Hotspot_{type}", sprite, Color.white, guideMin, guideMax);
            image.raycastTarget = true;

            if (sprite == null)
            {
                image.color = new Color32(34, 211, 238, 70);
                P.CreateText(image.transform, "Label", label, 24f, Vector2.zero, Vector2.one, Text);
            }
            else
            {
                if (layerName != null) P.Stretch(image.rectTransform, Vector2.zero, Vector2.one);
                else image.preserveAspect = true;

                // 투명 영역 클릭 무시. Read/Write가 꺼진 텍스처에 걸면 레이캐스트마다 에러가 나므로 확인 후 겁니다.
                if (sprite.texture.isReadable) image.alphaHitTestMinimumThreshold = 0.1f;
                else Debug.LogWarning($"[YomiRoomPointClick] {sprite.texture.name}의 Read/Write가 꺼져 있어 사각형 전체가 클릭 영역이 됩니다.");
            }

            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.78f, 0.95f, 1f, 1f);
            colors.pressedColor = new Color(0.6f, 0.8f, 0.9f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() => controller.HandleHotspot(type));
        }
    }
}
