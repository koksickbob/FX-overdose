using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FXOverdose.DatingSim.Store;
using FXOverdose.DatingSim.YomiRoom;

namespace FXOverdose.DatingSim.UI
{
    /// <summary>
    /// 편의점 알바 타이쿤 씬을 런타임에 통째로 생성합니다.
    /// 요미의 방과 같은 방식(씬 파일은 비어 있고 sceneLoaded 훅이 조립)이라 씬을 손으로 편집할 필요가 없습니다.
    ///
    /// 아트가 아직 없으므로 스프라이트 로드에 실패하면 색 블록으로 떨어집니다 — 지금 바로 플레이 가능합니다.
    /// </summary>
    public static class ConvenienceStorePrototype
    {
        private static readonly Color32 Navy = new(7, 16, 31, 255);
        private static readonly Color32 Floor = new(29, 43, 59, 255);
        private static readonly Color32 Wall = new(13, 28, 48, 255);
        private static readonly Color32 ShelfWood = new(92, 104, 120, 255);
        private static readonly Color32 Cyan = new(34, 211, 238, 255);
        private static readonly Color32 Pink = new(244, 114, 182, 255);
        private static readonly Color32 Text = new(226, 245, 250, 255);
        private static readonly Color32 Fluoro = new(238, 246, 232, 255);

        // 매대 배치. StoreConfig.shelves 순서와 1:1로 대응합니다.
        private static readonly Vector2[] ShelfPositions =
        {
            new(-5.2f, 0.25f), new(0f, 0.25f), new(5.2f, 0.25f),
            new(-5.2f, -2.85f), new(0f, -2.85f)
        };

        private static readonly Vector2 CounterPosition = new(5.2f, -2.85f);
        // 붙박이 설비는 StoreRoom 배경에 포함되어 있으며, 아래 좌표에는 투명 상호작용 영역만 둡니다.
        private static readonly Vector2 StoragePosition = new(-7.55f, 3.75f);
        private static readonly Vector2 ToolPosition = new(-5.45f, 3.75f);
        private static readonly Vector2 Entrance = new(0f, -4.6f);

        // 슬롯 수는 tier의 maxConcurrent 최대치(5)와 같아야 합니다.
        // 더 적으면 마지막 슬롯 인덱스가 clamp되어 손님 둘이 같은 자리에 겹칩니다.
        private static readonly Vector2[] QueueSlots =
        {
            new(5.2f, -4.1f), new(4.1f, -4.1f), new(3.0f, -4.1f),
            new(1.9f, -4.1f), new(0.8f, -4.1f)
        };

        public static void Build(Scene scene)
        {
            // 임시 런타임 스프라이트를 씬 파일에 직렬화하지 않습니다. (요미의 방과 동일한 가드)
            if (!Application.isPlaying) return;
            if (YomiRoomTopDownPrototype.Find(scene, "ConvenienceStoreRoot") != null) return;

            StoreConfig config = StoreConfig.Load();

            Camera camera = ConfigureCamera(scene);
            GameObject root = YomiRoomTopDownPrototype.CreateRoot(scene, "ConvenienceStoreRoot");

            BuildRoom(root.transform);
            List<StoreStation> shelves = BuildShelves(root.transform, config);
            StoreStation counter = BuildCounter(root.transform, config);
            BuildStorage(root.transform, config);

            Rigidbody2D playerBody = BuildPlayer(root.transform);
            GameObject customers = new("Customers");
            customers.transform.SetParent(root.transform, false);

            StoreShiftManager manager = new GameObject("StoreShiftManager").AddComponent<StoreShiftManager>();
            SceneManager.MoveGameObjectToScene(manager.gameObject, scene);

            StorePlayerController controller = playerBody.gameObject.AddComponent<StorePlayerController>();
            controller.Configure(playerBody, manager, config);

            Sprite[][] customerFrames =
            {
                YomiRoomTopDownPrototype.LoadWalkFrames("DatingSim/Store/Character/CustomerWalkSheet_A"),
                YomiRoomTopDownPrototype.LoadWalkFrames("DatingSim/Store/Character/CustomerWalkSheet_B"),
                YomiRoomTopDownPrototype.LoadWalkFrames("DatingSim/Store/Character/CustomerWalkSheet_C")
            };
            Sprite[] dirtSprites =
            {
                LoadWorldSprite("DatingSim/Store/Objects/Dirt_A"),
                LoadWorldSprite("DatingSim/Store/Objects/Dirt_B"),
                LoadWorldSprite("DatingSim/Store/Objects/Dirt_C")
            };
            manager.Configure(config, controller, counter, shelves, Entrance, QueueSlots, customers.transform,
                customerFrames, dirtSprites, LoadWorldSprite("DatingSim/Store/Objects/Leftover"));

            BuildUI(scene, camera, manager, controller, shelves);
        }

        private static Camera ConfigureCamera(Scene scene)
        {
            Camera camera = YomiRoomTopDownPrototype.FindComponent<Camera>(scene);
            if (camera == null) return null;

            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 5.4f;
            // ⚠️ 요미의 방은 우측 채팅 패널 때문에 rect가 화면 절반입니다. 편의점은 채팅이 없으므로
            //    반드시 전체 화면으로 되돌립니다. 빠뜨리면 화면 절반이 검게 남습니다.
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Navy;
            camera.cullingMask = ~0;
            return camera;
        }

        private static void BuildRoom(Transform parent)
        {
            GameObject background = YomiRoomTopDownPrototype.CreateBlock(parent, "StoreFloor", Vector2.zero,
                new Vector2(19.2f, 10.8f), Floor, -10, false);
            YomiRoomTopDownPrototype.ApplyResourceSprite(background, "DatingSim/Store/Room/StoreRoom", new Vector2(19.2f, 10.8f));
            // CreateBlock의 프로토타입 색이 원화에 곱해지지 않도록 실제 스프라이트는 원색으로 표시합니다.
            background.GetComponent<SpriteRenderer>().color = Color.white;

            // 벽은 배경 원화에 이미 그려져 있습니다. 렌더러 없는 콜라이더만 두어 밝은 외곽 띠가 생기지 않게 합니다.
            YomiRoomTopDownPrototype.CreateCollisionRegion(parent, "TopWall", new Vector2(0f, 5.2f), new Vector2(19.2f, 0.4f));
            // 화면 안쪽을 잠식하지 않도록 하단 벽은 카메라 끝선에 얇게 둡니다.
            // 아랫줄 진열대 충돌 하단(-4.325)과 약 1.075의 통로가 확보됩니다.
            YomiRoomTopDownPrototype.CreateCollisionRegion(parent, "BottomWall", new Vector2(0f, -5.45f), new Vector2(19.2f, 0.1f));
            YomiRoomTopDownPrototype.CreateCollisionRegion(parent, "LeftWall", new Vector2(-9.4f, 0f), new Vector2(0.4f, 10.8f));
            YomiRoomTopDownPrototype.CreateCollisionRegion(parent, "RightWall", new Vector2(9.4f, 0f), new Vector2(0.4f, 10.8f));
        }

        private static List<StoreStation> BuildShelves(Transform parent, StoreConfig config)
        {
            List<StoreStation> result = new();
            string[] stockNames = { "Drink", "Snack", "Lunch", "Daily", "Ice" };

            for (int i = 0; i < ShelfPositions.Length; i++)
            {
                ShelfEntry entry = i < config.shelves.Count ? config.shelves[i] : default;
                string label = string.IsNullOrEmpty(entry.displayName) ? $"매대 {i + 1}" : entry.displayName;

                GameObject body = YomiRoomTopDownPrototype.CreateBlock(parent, $"Shelf_{i}", ShelfPositions[i],
                    new Vector2(3f, 2.1f), ShelfWood, 5, true);
                YomiRoomTopDownPrototype.ApplyResourceSprite(body, "DatingSim/Store/Objects/Shelf_Body", new Vector2(3f, 2.1f));
                body.GetComponent<SpriteRenderer>().color = Color.white;
                // 탑다운 가구는 보이는 높이 전체가 아니라 바닥에 닿는 하단 받침만 충돌시킵니다.
                // 두 행 사이에 캐릭터 한 명이 지날 통로를 남기면서 좌우 폭은 충분히 막습니다.
                SetWorldColliderSize(body, new Vector2(2.8f, 1.2f), new Vector2(0f, -0.45f));
                body.GetComponent<SpriteRenderer>().sortingOrder = 13;

                // 재고 오버레이. CreateBlock의 스케일 방식과 섞이지 않도록 별도 오브젝트로 얹습니다.
                GameObject overlay = new("StockOverlay", typeof(SpriteRenderer));
                overlay.transform.SetParent(parent, false);
                overlay.transform.localPosition = ShelfPositions[i] + new Vector2(0f, 0.08f);
                // 견본처럼 상품이 매대 프레임 대부분을 채우도록 원본 비율을 거의 그대로 사용합니다.
                overlay.transform.localScale = new Vector3(0.96f, 0.96f, 1f);
                SpriteRenderer overlayRenderer = overlay.GetComponent<SpriteRenderer>();
                overlayRenderer.sprite = StoreVisuals.Pixel;
                overlayRenderer.color = Cyan;
                // 진열대 앞면은 캐릭터보다 앞에 그려져 내부로 올라간 것처럼 보이지 않게 합니다.
                overlayRenderer.sortingOrder = 14;

                StoreStation station = body.AddComponent<StoreStation>();
                int max = entry.maxStock > 0 ? entry.maxStock : 8;
                int initial = entry.maxStock > 0 ? entry.initialStock : 5;
                float hold = entry.refillHoldSeconds > 0f ? entry.refillHoldSeconds : 3f;
                station.Configure(StoreStationKind.Shelf, label, hold, i);
                string stockName = i < stockNames.Length ? stockNames[i] : stockNames[0];
                station.ConfigureShelfStock(initial, max, overlayRenderer,
                    LoadWorldSprite($"DatingSim/Store/Objects/Stock_{stockName}_Full"),
                    LoadWorldSprite($"DatingSim/Store/Objects/Stock_{stockName}_Half"));
                result.Add(station);
            }

            return result;
        }

        private static StoreStation BuildCounter(Transform parent, StoreConfig config)
        {
            GameObject go = YomiRoomTopDownPrototype.CreateBlock(parent, "Counter", CounterPosition,
                new Vector2(3.8f, 1.7f), ShelfWood, 5, true);
            YomiRoomTopDownPrototype.ApplyResourceSprite(go, "DatingSim/Store/Objects/Counter", new Vector2(3.8f, 1.7f));
            go.GetComponent<SpriteRenderer>().color = Color.white;
            // 계산대도 바닥에 닿는 하단부만 충돌시켜 위쪽 진열대와의 통로를 확보합니다.
            SetWorldColliderSize(go, new Vector2(3.55f, 1.15f), new Vector2(0f, -0.26f));

            StoreStation station = go.AddComponent<StoreStation>();
            station.Configure(StoreStationKind.Checkout, "계산대", config.checkoutBaseSeconds);
            return station;
        }

        private static void BuildStorage(Transform parent, StoreConfig config)
        {
            GameObject rack = CreateInvisibleStation(parent, "StorageArea", StoragePosition, new Vector2(1.7f, 1.25f));
            rack.AddComponent<StoreStation>().Configure(StoreStationKind.PickupStock, "재고 상자", 0.01f);

            GameObject cabinet = CreateInvisibleStation(parent, "CleaningArea", ToolPosition, new Vector2(1.25f, 1.25f));
            cabinet.AddComponent<StoreStation>().Configure(StoreStationKind.PickupTool, "청소도구", 0.01f);
        }

        private static GameObject CreateInvisibleStation(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject go = new(name, typeof(BoxCollider2D));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.GetComponent<BoxCollider2D>().size = size;
            return go;
        }

        /// <summary>
        /// ApplyResourceSprite가 원화 크기에 맞춰 Transform을 다시 스케일하므로 기본 1x1 콜라이더도 함께 작아집니다.
        /// 원하는 월드 크기를 로컬 크기로 역산해 보이는 스프라이트 전체를 막습니다.
        /// </summary>
        private static void SetWorldColliderSize(GameObject target, Vector2 worldSize, Vector2 worldOffset = default)
        {
            BoxCollider2D collider = target.GetComponent<BoxCollider2D>();
            if (collider == null) return;
            Vector3 scale = target.transform.localScale;
            collider.size = new Vector2(
                worldSize.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                worldSize.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)));
            collider.offset = new Vector2(
                worldOffset.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                worldOffset.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)));
        }

        private static Rigidbody2D BuildPlayer(Transform parent)
        {
            GameObject player = new("YomiPlayer", typeof(Rigidbody2D), typeof(CapsuleCollider2D));
            player.transform.SetParent(parent, false);
            player.transform.localPosition = CounterPosition + new Vector2(0f, -1.2f);

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
            // 손님 워크시트와 동일한 PPU/셀 규격이므로 별도 확대 없이 같은 체급으로 표시합니다.
            visual.transform.localScale = Vector3.one;

            SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
            renderer.sortingOrder = 12;

            Sprite[] frames = YomiRoomTopDownPrototype.LoadWalkFrames("DatingSim/YomiRoom/Morning/Character/YomiWalkSheet");
            if (frames == null || frames.Length < 12)
            {
                renderer.sprite = StoreVisuals.Pixel;
                renderer.color = Pink;
                visual.transform.localScale = new Vector3(0.7f, 1.2f, 1f);
            }
            else
            {
                // 요미의 방 컨트롤러가 없는 씬이므로 이동량을 직접 물려 줍니다.
                StorePlayerController controller = null;
                visual.GetComponent<YomiTopDownWalkAnimator>().Configure(body, renderer, frames, () =>
                {
                    if (controller == null) controller = player.GetComponent<StorePlayerController>();
                    return controller != null ? controller.MoveInput : Vector2.zero;
                });
            }

            return body;
        }

        private static void BuildUI(Scene scene, Camera camera, StoreShiftManager manager,
            StorePlayerController controller, List<StoreStation> shelves)
        {
            Canvas canvas = YomiRoomTopDownPrototype.CreateCanvas(scene, "Canvas_ConvenienceStore");
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            Transform root = canvas.transform;

            Sprite hudSprite = UI("HUDBar", new Vector4(28f, 28f, 28f, 28f));
            Sprite boxSprite = UI("Icon_Box");
            Sprite mopSprite = UI("Icon_Mop");

            // ── 우측 상단 가로 HUD: 좌측의 붙박이 창고·청소 구역을 가리지 않습니다. ──
            RectTransform hud = MakeFixedImage(root, "HUD", new Vector2(650f, 84f), hudSprite,
                hudSprite != null ? Color.white : new Color32(7, 16, 31, 230));
            hud.anchorMin = hud.anchorMax = new Vector2(1f, 1f);
            hud.pivot = new Vector2(1f, 1f);
            hud.anchoredPosition = new Vector2(-16f, -16f);

            MakeFixedImage(hud, "CustomerIcon", new Vector2(48f, 48f), UI("Icon_Customer"), Color.white,
                new Vector2(-280f, 0f));
            TMP_Text customerText = MakeFixedText(hud, "Customers", "처리 0  ·  이탈 0", 21f,
                new Vector2(180f, 52f), Text, new Vector2(-188f, 0f));
            TMP_Text timeText = MakeFixedText(hud, "Time", "03:00", 30f,
                new Vector2(120f, 36f), Text, new Vector2(0f, 16f));
            RectTransform timeBar = MakeGauge(hud, "TimeBar", new Vector2(170f, 19f), Pink, out Image timeFill);
            timeBar.anchoredPosition = new Vector2(0f, -20f);
            MakeFixedImage(hud, "MoneyIcon", new Vector2(48f, 48f), UI("Icon_Money"), Color.white,
                new Vector2(185f, 0f));
            TMP_Text payText = MakeFixedText(hud, "Pay", "0 원", 25f,
                new Vector2(125f, 52f), Text, new Vector2(255f, 0f));

            // ── 월드 추적 요소 ─────────────────────────────────────────
            RectTransform holdBar = MakeGauge(root, "HoldBar", new Vector2(240f, 44f), Cyan, out Image holdFill);
            RectTransform carryIcon = MakeFixedImage(root, "CarryIcon", new Vector2(64f, 64f), boxSprite, Color.white);
            TMP_Text carryCount = MakeFixedText(carryIcon, "Count", string.Empty, 18f, new Vector2(52f, 26f), Text,
                new Vector2(34f, -25f));
            RectTransform promptRoot = MakeFixedImage(root, "Prompt", new Vector2(72f, 72f), UI("KeyCap_E"), Color.white);
            TMP_Text promptText = null;

            List<RectTransform> patienceBars = new();
            List<Image> patienceFills = new();
            for (int i = 0; i < QueueSlots.Length + 1; i++)
            {
                RectTransform bar = MakeGauge(root, $"Patience_{i}", new Vector2(140f, 26f), Color.green, out Image fill);
                bar.gameObject.SetActive(false);
                patienceBars.Add(bar);
                patienceFills.Add(fill);
            }

            List<TMP_Text> shelfLabels = new();
            for (int i = 0; i < shelves.Count; i++)
                shelfLabels.Add(MakeFloatingText(root, $"Stock_{i}", 20f, Text, new Vector2(110f, 32f)));

            // ── 피드백 / 인트로 ────────────────────────────────────────
            TMP_Text feedbackText = YomiRoomTopDownPrototype.CreateText(root, "Feedback", string.Empty, 26f,
                new Vector2(0.25f, 0.08f), new Vector2(0.75f, 0.15f), Cyan);
            TMP_Text introText = YomiRoomTopDownPrototype.CreateText(root, "Intro", "근무 시작!", 64f,
                new Vector2(0.3f, 0.45f), new Vector2(0.7f, 0.6f), Text);

            // ── 결과 패널 ──────────────────────────────────────────────
            RectTransform dim = YomiRoomTopDownPrototype.CreatePanel(root, "ResultDim", Vector2.zero, Vector2.one,
                new Color32(2, 7, 14, 210));
            RectTransform result = MakeFixedImage(dim, "ResultPanel", new Vector2(900f, 620f),
                YomiRoomTopDownPrototype.LoadUISprite("DatingSim/YomiRoom/UI/Modal/PanelFrame", new Vector4(32f, 32f, 32f, 32f)),
                new Color32(10, 22, 39, 255));
            YomiRoomTopDownPrototype.CreateText(result, "Title", "근무 종료", 34f,
                new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f), Text).fontStyle = FontStyles.Bold;
            RectTransform gradeRect = MakeFixedImage(result, "Grade", new Vector2(160f, 160f), UI("Grade_A"), Color.white,
                new Vector2(0f, 165f));
            Image gradeBadge = gradeRect.GetComponent<Image>();
            RectTransform giftRect = MakeFixedImage(result, "Gift", new Vector2(64f, 64f), UI("Icon_Gift"), Color.white,
                new Vector2(350f, -155f));

            TMP_Text bodyText = YomiRoomTopDownPrototype.CreateText(result, "Body", string.Empty, 22f,
                new Vector2(0.08f, 0.16f), new Vector2(0.92f, 0.66f), Text);
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.enableAutoSizing = false;

            Button confirm = YomiRoomTopDownPrototype.CreateButton(result, "Confirm", "확인",
                new Vector2(0.32f, 0.04f), new Vector2(0.68f, 0.14f), Cyan);

            StoreShiftUI ui = canvas.gameObject.AddComponent<StoreShiftUI>();
            Sprite[] grades = { UI("Grade_S"), UI("Grade_A"), UI("Grade_B"), UI("Grade_C"), UI("Grade_D") };
            ui.Configure(manager, controller, camera, canvasRect, timeText, timeFill, payText, customerText,
                feedbackText, introText, holdBar, holdFill, carryIcon, carryCount, promptRoot, promptText,
                dim.gameObject, gradeBadge, giftRect.gameObject, bodyText, confirm, boxSprite, mopSprite, grades,
                shelves, shelfLabels, patienceBars, patienceFills);
        }

        /// <summary>월드에 따라다니는 게이지 한 쌍(테두리 + 채움)을 만듭니다.</summary>
        private static RectTransform MakeGauge(Transform parent, string name, Vector2 size, Color color, out Image fill)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            Image frame = go.GetComponent<Image>();
            frame.sprite = UI("ProgressFrame", new Vector4(18f, 18f, 18f, 18f));
            frame.type = frame.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            frame.color = frame.sprite != null ? Color.white : new Color32(7, 16, 31, 220);
            frame.raycastTarget = false;

            GameObject fillGo = new("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.transform.SetParent(go.transform, false);
            RectTransform fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);
            fill = fillGo.GetComponent<Image>();
            fill.sprite = UI("ProgressFill") ?? StoreVisuals.Pixel;
            fill.color = color;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.raycastTarget = false;
            return rect;
        }

        private static TMP_Text MakeFloatingText(Transform parent, string name, float size, Color color, Vector2 box)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = box;

            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static Sprite UI(string name, Vector4 border = default) =>
            YomiRoomTopDownPrototype.LoadUISprite($"DatingSim/Store/UI/{name}", border);

        private static Sprite LoadWorldSprite(string path)
        {
            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture == null) return null;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f);
        }

        private static RectTransform MakeFixedImage(Transform parent, string name, Vector2 size, Sprite sprite,
            Color color, Vector2 anchoredPosition = default)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            if (sprite != null && sprite.border.sqrMagnitude > 0f) image.type = Image.Type.Sliced;
            return rect;
        }

        private static TMP_Text MakeFixedText(Transform parent, string name, string value, float fontSize,
            Vector2 size, Color color, Vector2 anchoredPosition)
        {
            TMP_Text label = MakeFloatingText(parent, name, fontSize, color, size);
            label.text = value;
            label.rectTransform.anchoredPosition = anchoredPosition;
            return label;
        }
    }
}
