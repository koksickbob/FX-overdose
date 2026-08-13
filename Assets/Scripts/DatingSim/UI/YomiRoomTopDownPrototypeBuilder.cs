using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FXOverdose.DatingSim.YomiRoom;

namespace FXOverdose.DatingSim.UI
{
    /// <summary>외부 아트 없이 P2_03의 이동과 상호작용 동선을 확인하기 위한 런타임 조립기입니다.</summary>
    public static class YomiRoomTopDownPrototype
    {
        /// <summary>노드 하나에 붙을 수 있는 선택지 최대 개수. 계획서 요청사항 4번의 상한과 같습니다.</summary>
        private const int MaxTalkChoices = 4;

        private static Sprite pixelSprite;
        private static readonly Color32 Navy = new(7, 16, 31, 255);
        private static readonly Color32 Floor = new(29, 43, 59, 255);
        private static readonly Color32 Wall = new(13, 28, 48, 255);
        private static readonly Color32 Cyan = new(34, 211, 238, 255);
        private static readonly Color32 Pink = new(244, 114, 182, 255);
        private static readonly Color32 Text = new(226, 245, 250, 255);

        public static void Build(Scene scene)
        {
            // 임시 런타임 스프라이트를 씬 파일에 직렬화하지 않습니다.
            if (!Application.isPlaying) return;
            if (Find(scene, "YomiRoomTopDownRoot") != null) return;

            GameObject oldCanvas = Find(scene, "Canvas_YomiRoom");
            if (oldCanvas != null)
            {
                oldCanvas.SetActive(false);
                Object.Destroy(oldCanvas);
            }

            Camera camera = FindComponent<Camera>(scene);
            ConfigureCamera(camera);

            GameObject root = CreateRoot(scene, "YomiRoomTopDownRoot");
            // 좌측 상태 카드의 여백은 확보하면서 방을 우측 채팅 패널에 더 가깝게 배치합니다.
            root.transform.position = new Vector3(0.55f, 0f, 0f);
            BuildRoom(root.transform);
            Rigidbody2D player = BuildPlayer(root.transform);
            BuildInteractables(root.transform);
            BuildUI(scene, root.transform, player);
        }

        private static void ConfigureCamera(Camera camera)
        {
            if (camera == null) return;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 5.25f;
            camera.rect = new Rect(0f, 0f, 0.5f, 1f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Navy;
            camera.cullingMask = ~0;
        }

        private static void BuildRoom(Transform parent)
        {
            GameObject background = CreateBlock(parent, "MorningRoomBackground", Vector2.zero, new Vector2(6.8f, 9f), Color.white, -10, false);
            ApplyResourceSprite(background, "DatingSim/YomiRoom/Morning/RoomLeft", new Vector2(6.8f, 9f));
            // 배경의 상단은 단순 테두리가 아니라 창문/도배면까지 이어지는 벽 영역입니다.
            // 얇은 외곽선만 막으면 캐릭터 발이 벽면 위까지 올라가므로 실제 바닥 경계까지 충돌을 내립니다.
            CreateBlock(parent, "TopWall", new Vector2(0f, 4.15f), new Vector2(6.45f, 0.3f), Wall, 0, true);
            CreateCollisionRegion(parent, "TOP WALL BARRIER", new Vector2(0f, 4.15f), new Vector2(6.45f, 1f));
            // 세로형 방 중앙 하단의 통로를 출구로 비워 둡니다.
            CreateBlock(parent, "BottomWallLeft", new Vector2(-2f, -4.15f), new Vector2(2.45f, 0.3f), Wall, 0, true);
            CreateBlock(parent, "BottomWallRight", new Vector2(2f, -4.15f), new Vector2(2.45f, 0.3f), Wall, 0, true);
            CreateBlock(parent, "LeftWall", new Vector2(-3.2f, 0f), new Vector2(0.3f, 8.6f), Wall, 0, true);
            CreateBlock(parent, "RightWall", new Vector2(3.2f, 0f), new Vector2(0.3f, 8.6f), Wall, 0, true);

            // 가구는 완성 맵에 직접 그려져 있으므로 여기에는 보이지 않는 충돌 영역만 둡니다.
            CreateCollisionRegion(parent, "BED", new Vector2(2f, 1.1f), new Vector2(1.65f, 4.1f));
            // L자 PC 전체를 큰 사각형 하나로 막지 않고 실제 실루엣에 맞춰 세분화합니다.
            CreateCollisionRegion(parent, "PC DESK TOP", new Vector2(-1.2f, 2.3f), new Vector2(2.45f, 1.25f));
            CreateCollisionRegion(parent, "UPPER WALL FACE", new Vector2(0.82f, 3.55f), new Vector2(2.55f, 1.3f));
            CreateCollisionRegion(parent, "PC DESK LEFT", new Vector2(-2.55f, 0.65f), new Vector2(0.82f, 3.25f));
            CreateCollisionRegion(parent, "PC TOWER", new Vector2(-2.25f, -0.9f), new Vector2(1.05f, 0.75f));
            CreateCollisionRegion(parent, "PC CHAIR", new Vector2(-0.75f, 0.65f), new Vector2(1.15f, 1.35f));
            CreateExitTrigger(parent, new Vector2(0f, -3.95f), new Vector2(1.5f, 0.45f));
        }

        private static Rigidbody2D BuildPlayer(Transform parent)
        {
            GameObject player = new("YomiPlayer", typeof(Rigidbody2D), typeof(CapsuleCollider2D));
            player.transform.SetParent(parent, false);
            // 맵 루트가 좌측으로 이동해도 요미가 방 내부의 로컬 시작점을 유지해야 합니다.
            player.transform.localPosition = new Vector3(0f, -1.1f, 0f);
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            CapsuleCollider2D collider = player.GetComponent<CapsuleCollider2D>();
            collider.size = new Vector2(0.72f, 0.88f);
            collider.offset = new Vector2(0f, -0.2f);

            GameObject visual = new("Visual", typeof(SpriteRenderer), typeof(YomiTopDownWalkAnimator));
            visual.transform.SetParent(player.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            visual.transform.localScale = new Vector3(1.35f, 1.35f, 1f);
            SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
            renderer.sortingOrder = 10;
            Sprite[] frames = LoadWalkFrames("DatingSim/YomiRoom/Morning/Character/YomiWalkSheet");
            visual.GetComponent<YomiTopDownWalkAnimator>().Configure(body, renderer, frames);
            return body;
        }

        private static void BuildInteractables(Transform parent)
        {
            CreateInteractable(parent, YomiRoomInteractionType.RestBed, "침대", "잠깐 눈만 붙일까, 오늘은 여기까지 할까?",
                "눈 붙이기: 체력 +10 / 슬롯 -1      취침: 하루 마감", new Vector2(1f, 0.8f));
            CreateInteractable(parent, YomiRoomInteractionType.TradingPC, "PC", "PC를 켜고 트레이딩을 시작할까요?", "시간 슬롯 소모 없음", new Vector2(0.05f, 0.55f));
        }

        private static void CreateInteractable(Transform parent, YomiRoomInteractionType type, string title,
            string description, string cost, Vector2 position)
        {
            GameObject go = new($"Interactable_{type}", typeof(YomiRoomInteractable));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.GetComponent<YomiRoomInteractable>().Configure(type, title, description, cost);
        }

        private static void CreateExitTrigger(Transform parent, Vector2 position, Vector2 size)
        {
            GameObject exit = new("RoomExitTrigger", typeof(BoxCollider2D), typeof(YomiRoomExitTrigger));
            exit.transform.SetParent(parent, false);
            exit.transform.localPosition = position;
            BoxCollider2D trigger = exit.GetComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = size;
        }

        private static void BuildUI(Scene scene, Transform root, Rigidbody2D player)
        {
            Canvas canvas = CreateCanvas(scene);
            TextMeshProUGUI prompt = CreateText(canvas.transform, "InteractionPrompt", string.Empty, 22f,
                new Vector2(0.06f, 0.09f), new Vector2(0.42f, 0.16f), Text);
            prompt.gameObject.SetActive(false);
            TextMeshProUGUI feedback = CreateText(canvas.transform, "Feedback", string.Empty, 20f,
                new Vector2(0.05f, 0.17f), new Vector2(0.42f, 0.23f), Pink);

            GameObject modal = CreateModal(canvas.transform, out TextMeshProUGUI modalTitle, out TextMeshProUGUI modalBody,
                out Button confirm, out Button secondary, out Button cancel);

            YomiRoomTopDownController controller = root.gameObject.AddComponent<YomiRoomTopDownController>();
            controller.Configure(player, prompt, feedback, modal, modalTitle, modalBody, confirm, secondary);
            confirm.onClick.AddListener(controller.ConfirmInteraction);
            secondary.onClick.AddListener(controller.SecondaryInteraction);
            cancel.onClick.AddListener(controller.CloseModal);
            modal.SetActive(false);

            BuildVerticalStatusCards(canvas.transform);
            BuildDialoguePanel(canvas.transform);

            // 채팅 패널이 화면 우측에 나중에 생성되므로 모달을 마지막 형제로 올려 항상 화면 전체 위에 표시합니다.
            modal.transform.SetAsLastSibling();
        }

        private static void BuildVerticalStatusCards(Transform parent)
        {
            const float minX = 0.008f;
            const float maxX = 0.092f;
            float[] centers = { 0.77f, 0.59f, 0.41f, 0.23f };
            string[] names = { "Stamina", "TimeSlot", "Affection", "Obsession" };
            string[] labels = { "체력", "남은 시간", "호감도", "집착도" };
            string[] iconPaths =
            {
                "DatingSim/YomiRoom/UI/StatusIcons/Stamina",
                "DatingSim/YomiRoom/UI/StatusIcons/TimeSlot",
                "DatingSim/YomiRoom/UI/StatusIcons/Affection",
                "DatingSim/YomiRoom/UI/StatusIcons/Obsession"
            };
            Color[] accents = { Cyan, new Color32(250, 184, 45, 255), Pink, new Color32(168, 85, 247, 255) };

            TextMeshProUGUI[] values = new TextMeshProUGUI[4];
            Image[] fills = new Image[4];
            Image[] timePips = new Image[5];
            for (int i = 0; i < 4; i++)
            {
                RectTransform card = CreatePanel(parent, names[i] + "Card",
                    new Vector2(minX, centers[i] - 0.09f), new Vector2(maxX, centers[i] + 0.09f), new Color32(5, 14, 28, 244));
                card.GetComponent<Outline>().effectColor = new Color32(47, 81, 105, 255);

                Image icon = CreateUIImage(card, "Icon", LoadUISprite(iconPaths[i]), Color.white,
                    new Vector2(0.08f, 0.68f), new Vector2(0.31f, 0.91f));
                icon.preserveAspect = true;
                TextMeshProUGUI label = CreateText(card, "Label", labels[i], 14f,
                    new Vector2(0.33f, 0.67f), new Vector2(0.94f, 0.92f), accents[i]);
                label.alignment = TextAlignmentOptions.Left;
                label.fontStyle = FontStyles.Bold;
                values[i] = CreateText(card, "Value", i == 0 ? "100/100" : i == 1 ? "5/5" : "0%", 21f,
                    new Vector2(0.08f, 0.3f), new Vector2(0.92f, 0.67f), Text);

                if (i == 1)
                {
                    for (int pip = 0; pip < 5; pip++)
                    {
                        float x0 = 0.08f + (pip * 0.178f);
                        timePips[pip] = CreateUIImage(card, $"Pip_{pip}", null, accents[i],
                            new Vector2(x0, 0.12f), new Vector2(x0 + 0.13f, 0.24f));
                    }
                }
                else
                {
                    Image track = CreateUIImage(card, "Track", null, new Color32(27, 45, 61, 255),
                        new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.23f));
                    fills[i] = CreateUIImage(track.transform, "Fill", null, accents[i], Vector2.zero, Vector2.one);
                    fills[i].type = Image.Type.Filled;
                    fills[i].fillMethod = Image.FillMethod.Horizontal;
                    fills[i].fillOrigin = 0;
                }
            }

            YomiRoomVerticalStatusUI status = parent.gameObject.AddComponent<YomiRoomVerticalStatusUI>();
            status.Configure(values[0], values[1], values[2], values[3], fills[0], fills[2], fills[3], timePips);
        }

        private static void BuildDialoguePanel(Transform parent)
        {
            // 채팅 패널은 화면 오른쪽 절반을 정확히 사용합니다.
            Sprite chatFrame = LoadUISprite("DatingSim/YomiRoom/UI/Chat/ChatFrame", new Vector4(34f, 34f, 34f, 34f));
            Sprite yomiBubble = LoadUISprite("DatingSim/YomiRoom/UI/Chat/YomiBubble", new Vector4(34f, 34f, 34f, 34f));
            Sprite masterBubble = LoadUISprite("DatingSim/YomiRoom/UI/Chat/MasterBubble", new Vector4(34f, 34f, 34f, 34f));
            Sprite inputFrame = LoadUISprite("DatingSim/YomiRoom/UI/Chat/InputFrame", new Vector4(28f, 28f, 28f, 28f));
            Sprite sendFrame = LoadUISprite("DatingSim/YomiRoom/UI/Chat/SendButton", new Vector4(30f, 30f, 30f, 30f));
            Sprite avatarFrame = LoadUISprite("DatingSim/YomiRoom/UI/Chat/AvatarFrame", new Vector4(25f, 25f, 25f, 25f));
            Sprite portrait = LoadUISprite("DatingSim/YomiRoom/UI/Chat/YomiPortrait");

            RectTransform panel = CreatePanel(parent, "DialoguePanel", new Vector2(0.5f, 0.02f), new Vector2(0.992f, 0.98f), Color.white);
            Image panelImage = panel.GetComponent<Image>();
            panelImage.sprite = chatFrame;
            panelImage.type = Image.Type.Sliced;
            panel.GetComponent<Outline>().enabled = false;
            TextMeshProUGUI title = CreateText(panel, "Title", "YOMI CHAT", 34f, new Vector2(0.06f, 0.905f), new Vector2(0.88f, 0.975f), new Color32(255, 116, 171, 255));
            title.fontStyle = FontStyles.Bold;
            TextMeshProUGUI subtitle = CreateText(panel, "Subtitle", "요미와 자유롭게 대화하세요", 16f, new Vector2(0.06f, 0.855f), new Vector2(0.94f, 0.905f), Cyan);

            BuildRoomSettingsButton(panel);

            RectTransform historySurface = CreatePanel(panel, "HistorySurface", new Vector2(0.055f, 0.2f), new Vector2(0.945f, 0.84f), new Color32(2, 9, 20, 210));
            historySurface.GetComponent<Outline>().effectColor = new Color32(28, 70, 101, 255);
            historySurface.gameObject.AddComponent<RectMask2D>();
            ScrollRect scroll = historySurface.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
            GameObject contentObject = new("Messages", typeof(RectTransform));
            contentObject.transform.SetParent(historySurface, false);
            RectTransform history = contentObject.GetComponent<RectTransform>();
            history.anchorMin = new Vector2(0f, 1f);
            history.anchorMax = new Vector2(1f, 1f);
            history.pivot = new Vector2(0.5f, 1f);
            history.anchoredPosition = new Vector2(0f, -14f);
            history.sizeDelta = new Vector2(-24f, 0f);
            scroll.viewport = historySurface;
            scroll.content = history;

            RectTransform inputSurface = CreatePanel(panel, "InputSurface", new Vector2(0.055f, 0.065f), new Vector2(0.73f, 0.165f), Color.white);
            inputSurface.GetComponent<Image>().sprite = inputFrame;
            inputSurface.GetComponent<Image>().type = Image.Type.Sliced;
            inputSurface.GetComponent<Outline>().enabled = false;
            TMP_InputField input = inputSurface.gameObject.AddComponent<TMP_InputField>();
            TextMeshProUGUI inputText = CreateText(inputSurface, "Text", string.Empty, 18f, new Vector2(0.04f, 0.1f), new Vector2(0.96f, 0.9f), Text);
            inputText.alignment = TextAlignmentOptions.Left;
            TextMeshProUGUI placeholder = CreateText(inputSurface, "Placeholder", "메시지를 입력하세요...", 18f, new Vector2(0.04f, 0.1f), new Vector2(0.96f, 0.9f), new Color32(91, 116, 133, 255));
            placeholder.alignment = TextAlignmentOptions.Left;
            input.textComponent = inputText;
            input.placeholder = placeholder;
            input.textViewport = inputSurface;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 180;

            Button send = CreateButton(panel, "SendButton", "전송", new Vector2(0.75f, 0.065f), new Vector2(0.945f, 0.165f), Pink);
            Image sendImage = (Image)send.targetGraphic;
            sendImage.sprite = sendFrame;
            sendImage.type = Image.Type.Sliced;
            sendImage.color = Color.white;
            send.GetComponent<Outline>().enabled = false;

            // 입력행을 대체하는 선택지 버튼 4개. 노드마다 필요한 개수만 켜집니다. (2~4개)
            // 스프라이트가 없으면 단색 임시 UI로 그대로 동작합니다.
            Sprite choiceFrame = LoadUISprite("DatingSim/YomiRoom/UI/Chat/ChoiceButton", new Vector4(24f, 24f, 24f, 24f));
            Button[] choices = new Button[MaxTalkChoices];
            for (int i = 0; i < MaxTalkChoices; i++)
            {
                float top = 0.185f - i * 0.042f;
                choices[i] = CreateButton(panel, $"Choice{i}", string.Empty,
                    new Vector2(0.055f, top - 0.038f), new Vector2(0.945f, top), Cyan);

                Image choiceImage = (Image)choices[i].targetGraphic;
                if (choiceFrame != null)
                {
                    choiceImage.sprite = choiceFrame;
                    choiceImage.type = Image.Type.Sliced;
                    choiceImage.color = Color.white;
                    choices[i].GetComponent<Outline>().enabled = false;
                }

                TextMeshProUGUI label = choices[i].GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.fontSize = 17f;
                    label.alignment = TextAlignmentOptions.Left;
                    label.margin = new Vector4(18f, 0f, 8f, 0f);
                }
            }

            YomiRoomDialogueUI dialogue = panel.gameObject.AddComponent<YomiRoomDialogueUI>();
            dialogue.Configure(history, input, send, scroll, yomiBubble, masterBubble, avatarFrame, portrait, choices);

            // 대화 개시 버튼. 슬롯을 소모하고 오늘 안 쓴 토픽을 하나 엽니다.
            Button startTalk = CreateButton(panel, "StartTalkButton", "자유대화",
                new Vector2(0.055f, 0.02f), new Vector2(0.945f, 0.058f), Pink);
            startTalk.onClick.AddListener(() => YomiRoomManager.Instance?.TryStartTalk());
        }

        private static void BuildRoomSettingsButton(Transform parent)
        {
            RectTransform rect = CreatePanel(parent, "SettingsButton", new Vector2(0.9f, 0.9f), new Vector2(0.965f, 0.968f), new Color32(12, 27, 47, 255));
            rect.GetComponent<Outline>().effectColor = Cyan;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color32(176, 244, 255, 255);
            colors.pressedColor = new Color32(94, 182, 200, 255);
            button.colors = colors;

            Image gear = CreateUIImage(rect, "SettingsButtonVisual", LoadUISprite("DatingSim/YomiRoom/UI/SettingsGear"), Color.white,
                new Vector2(0.16f, 0.16f), new Vector2(0.84f, 0.84f));
            gear.preserveAspect = true;

            // 비활성 상태에서 설정해 Awake가 트레이딩 전용 AUTO/USER 버튼을 만들지 않게 합니다.
            rect.gameObject.SetActive(false);
            SettingsMenuController settings = rect.gameObject.AddComponent<SettingsMenuController>();
            settings.ConfigureRoomButton(button);
            rect.gameObject.SetActive(true);
        }

        /// <summary>
        /// 상호작용 확인 모달. 버튼 3개(주 행동 / 보조 행동 / 취소) 구조입니다.
        /// 보조 버튼은 침대처럼 두 갈래 행동이 있는 상호작용에서만 켜집니다.
        ///
        /// 스프라이트는 Resources/DatingSim/YomiRoom/UI/Modal/ 아래를 참조합니다.
        /// 이미지가 없으면 단색 임시 UI로 그대로 동작하며, PNG를 넣는 즉시 교체됩니다.
        /// 자세한 규격은 docs/P2_05_UI_and_Art/P2_05_YomiRoom_Modal_UI_Spec.md 참고.
        /// </summary>
        private static GameObject CreateModal(Transform parent, out TextMeshProUGUI title, out TextMeshProUGUI body,
            out Button confirm, out Button secondary, out Button cancel)
        {
            Sprite dimSprite = LoadUISprite("DatingSim/YomiRoom/UI/Modal/Dim");
            Sprite panelSprite = LoadUISprite("DatingSim/YomiRoom/UI/Modal/PanelFrame", new Vector4(32f, 32f, 32f, 32f));
            Sprite primarySprite = LoadUISprite("DatingSim/YomiRoom/UI/Modal/ButtonPrimary", new Vector4(24f, 24f, 24f, 24f));
            Sprite secondarySprite = LoadUISprite("DatingSim/YomiRoom/UI/Modal/ButtonSecondary", new Vector4(24f, 24f, 24f, 24f));
            Sprite cancelSprite = LoadUISprite("DatingSim/YomiRoom/UI/Modal/ButtonCancel", new Vector4(24f, 24f, 24f, 24f));

            GameObject blocker = new("InteractionModal", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            blocker.transform.SetParent(parent, false);
            Stretch(blocker.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            Image dim = blocker.GetComponent<Image>();
            dim.color = dimSprite != null ? Color.white : new Color32(0, 0, 0, 150);
            dim.sprite = dimSprite;

            RectTransform panel = CreatePanel(blocker.transform, "Panel", new Vector2(0.29f, 0.29f), new Vector2(0.71f, 0.71f), new Color32(8, 19, 34, 252));
            ApplyFrameSprite(panel, panelSprite);

            title = CreateText(panel, "Title", "상호작용", 30f, new Vector2(0.08f, 0.7f), new Vector2(0.92f, 0.9f), Cyan);
            body = CreateText(panel, "Body", string.Empty, 21f, new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.7f), Text);

            // 버튼 3개를 가로로 균등 배치합니다. 보조 버튼이 꺼져도 나머지 위치는 그대로입니다.
            confirm = CreateButton(panel, "Confirm", "확인", new Vector2(0.07f, 0.09f), new Vector2(0.35f, 0.29f), Cyan);
            secondary = CreateButton(panel, "Secondary", "보조", new Vector2(0.37f, 0.09f), new Vector2(0.65f, 0.29f), new Color32(255, 196, 94, 255));
            cancel = CreateButton(panel, "Cancel", "취소", new Vector2(0.67f, 0.09f), new Vector2(0.95f, 0.29f), Pink);

            ApplyFrameSprite((RectTransform)confirm.transform, primarySprite);
            ApplyFrameSprite((RectTransform)secondary.transform, secondarySprite);
            ApplyFrameSprite((RectTransform)cancel.transform, cancelSprite);

            secondary.gameObject.SetActive(false);
            return blocker;
        }

        /// <summary>
        /// 9-슬라이스 프레임 스프라이트를 씌웁니다. 스프라이트가 없으면 기존 단색 임시 UI를 유지합니다.
        /// UI 제작자는 이미지를 지정된 Resources 경로에 넣기만 하면 됩니다.
        /// </summary>
        private static void ApplyFrameSprite(RectTransform target, Sprite sprite)
        {
            if (target == null || sprite == null) return;

            Image image = target.GetComponent<Image>();
            if (image == null) return;

            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;

            Outline outline = target.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
        }

        private static GameObject CreateCollisionRegion(Transform parent, string label, Vector2 position, Vector2 size)
        {
            GameObject region = new(label, typeof(BoxCollider2D));
            region.transform.SetParent(parent, false);
            region.transform.localPosition = position;
            region.GetComponent<BoxCollider2D>().size = size;
            return region;
        }

        private static void ApplyResourceSprite(GameObject target, string resourcePath, Vector2 desiredSize)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                Debug.LogWarning($"[YomiRoomTopDown] 스프라이트 텍스처를 찾지 못했습니다: {resourcePath}");
                return;
            }

            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            Vector2 bounds = sprite.bounds.size;
            target.transform.localScale = new Vector3(desiredSize.x / bounds.x, desiredSize.y / bounds.y, 1f);
        }

        private static Sprite[] LoadWalkFrames(string resourcePath)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                Debug.LogWarning($"[YomiRoomTopDown] 요미 걷기 시트를 찾지 못했습니다: {resourcePath}");
                return System.Array.Empty<Sprite>();
            }

            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            Sprite[] frames = new Sprite[12];
            int cellWidth = texture.width / 3;
            for (int direction = 0; direction < 4; direction++)
            {
                int sourceY = Mathf.RoundToInt(texture.height * ((3 - direction) / 4f));
                int sourceTop = Mathf.RoundToInt(texture.height * ((4 - direction) / 4f));
                int cellHeight = sourceTop - sourceY;
                for (int frame = 0; frame < 3; frame++)
                {
                    Rect rect = new(frame * cellWidth, sourceY, cellWidth, cellHeight);
                    frames[(direction * 3) + frame] = Sprite.Create(texture, rect, new Vector2(0.5f, 0.42f), 220f);
                }
            }
            return frames;
        }

        private static GameObject CreateBlock(Transform parent, string name, Vector2 position, Vector2 size,
            Color color, int sortingOrder, bool collider)
        {
            GameObject go = new(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = GetPixelSprite();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            if (collider) go.AddComponent<BoxCollider2D>();
            return go;
        }

        private static Sprite GetPixelSprite()
        {
            if (pixelSprite != null) return pixelSprite;
            Texture2D texture = new(1, 1, TextureFormat.RGBA32, false);
            texture.name = "YomiRoomPrototypePixel";
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            pixelSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            pixelSprite.name = "YomiRoomPrototypePixelSprite";
            return pixelSprite;
        }

        private static Canvas CreateCanvas(Scene scene)
        {
            GameObject go = new("Canvas_YomiRoomTopDown", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(go, scene);
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static RectTransform CreatePanel(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>(), min, max);
            go.GetComponent<Image>().color = color;
            Outline outline = go.GetComponent<Outline>();
            outline.effectColor = new Color32(35, 73, 99, 255);
            outline.effectDistance = new Vector2(2f, -2f);
            return go.GetComponent<RectTransform>();
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string value, float size,
            Vector2 min, Vector2 max, Color color)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>(), min, max);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = value;
            text.fontSize = size;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Max(12f, size * 0.65f);
            text.fontSizeMax = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 min, Vector2 max, Color accent)
        {
            RectTransform panel = CreatePanel(parent, name, min, max, new Color32(17, 31, 51, 245));
            Button button = panel.gameObject.AddComponent<Button>();
            button.targetGraphic = panel.GetComponent<Image>();
            panel.GetComponent<Outline>().effectColor = accent;
            CreateText(panel, "Label", label, 20f, Vector2.zero, Vector2.one, Text).fontStyle = FontStyles.Bold;
            return button;
        }

        private static Image CreateUIImage(Transform parent, string name, Sprite sprite, Color color, Vector2 min, Vector2 max)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>(), min, max);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Sprite LoadUISprite(string resourcePath, Vector4 border = default)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null) return null;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static GameObject CreateRoot(Scene scene, string name)
        {
            GameObject go = new(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        private static GameObject Find(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child.gameObject;
            return null;
        }

        private static T FindComponent<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (T component in root.GetComponentsInChildren<T>(true))
                return component;
            return null;
        }
    }
}
