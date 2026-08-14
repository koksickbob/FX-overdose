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
            new(-4.0f, 0.5f), new(0.0f, 0.5f), new(4.0f, 0.5f), new(-4.0f, -2.0f), new(0.0f, -2.0f)
        };

        private static readonly Vector2 CounterPosition = new(5.5f, -3.2f);
        private static readonly Vector2 StoragePosition = new(-7.3f, 3.0f);
        private static readonly Vector2 ToolPosition = new(-5.6f, 3.0f);
        private static readonly Vector2 Entrance = new(0f, -5.0f);

        // 슬롯 수는 tier의 maxConcurrent 최대치(5)와 같아야 합니다.
        // 더 적으면 마지막 슬롯 인덱스가 clamp되어 손님 둘이 같은 자리에 겹칩니다.
        private static readonly Vector2[] QueueSlots =
        {
            new(5.5f, -4.5f), new(4.3f, -4.5f), new(3.1f, -4.5f), new(1.9f, -4.5f), new(0.7f, -4.5f)
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

            Sprite[] customerFrames = YomiRoomTopDownPrototype.LoadWalkFrames("DatingSim/Store/Character/CustomerWalkSheet_A");
            manager.Configure(config, controller, counter, shelves, Entrance, QueueSlots,
                customers.transform, customerFrames, null);

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
                new Vector2(17f, 11f), Floor, -10, false);
            YomiRoomTopDownPrototype.ApplyResourceSprite(background, "DatingSim/Store/Room/StoreRoom", new Vector2(17f, 11f));

            // 형광등 반사. 아트가 들어오면 배경 1장에 포함되므로 이 두 줄은 지워집니다.
            YomiRoomTopDownPrototype.CreateBlock(parent, "LightStripA", new Vector2(0f, 3.6f), new Vector2(14f, 0.12f), Fluoro, -9, false);
            YomiRoomTopDownPrototype.CreateBlock(parent, "LightStripB", new Vector2(0f, -0.6f), new Vector2(14f, 0.12f), Fluoro, -9, false);

            YomiRoomTopDownPrototype.CreateBlock(parent, "TopWall", new Vector2(0f, 5.0f), new Vector2(17f, 0.4f), Wall, 0, true);
            YomiRoomTopDownPrototype.CreateBlock(parent, "BottomWallLeft", new Vector2(-5.2f, -5.0f), new Vector2(6.6f, 0.4f), Wall, 0, true);
            YomiRoomTopDownPrototype.CreateBlock(parent, "BottomWallRight", new Vector2(5.2f, -5.0f), new Vector2(6.6f, 0.4f), Wall, 0, true);
            YomiRoomTopDownPrototype.CreateBlock(parent, "LeftWall", new Vector2(-8.3f, 0f), new Vector2(0.4f, 10.4f), Wall, 0, true);
            YomiRoomTopDownPrototype.CreateBlock(parent, "RightWall", new Vector2(8.3f, 0f), new Vector2(0.4f, 10.4f), Wall, 0, true);
        }

        private static List<StoreStation> BuildShelves(Transform parent, StoreConfig config)
        {
            List<StoreStation> result = new();

            for (int i = 0; i < ShelfPositions.Length; i++)
            {
                ShelfEntry entry = i < config.shelves.Count ? config.shelves[i] : default;
                string label = string.IsNullOrEmpty(entry.displayName) ? $"매대 {i + 1}" : entry.displayName;

                GameObject body = YomiRoomTopDownPrototype.CreateBlock(parent, $"Shelf_{i}", ShelfPositions[i],
                    new Vector2(2.6f, 1.5f), ShelfWood, 5, true);
                YomiRoomTopDownPrototype.ApplyResourceSprite(body, "DatingSim/Store/Objects/Shelf_Body", new Vector2(2.6f, 1.5f));

                // 재고 오버레이. CreateBlock의 스케일 방식과 섞이지 않도록 별도 오브젝트로 얹습니다.
                GameObject overlay = new("StockOverlay", typeof(SpriteRenderer));
                overlay.transform.SetParent(parent, false);
                overlay.transform.localPosition = ShelfPositions[i] + new Vector2(0f, 0.35f);
                overlay.transform.localScale = new Vector3(2.1f, 0.7f, 1f);
                SpriteRenderer overlayRenderer = overlay.GetComponent<SpriteRenderer>();
                overlayRenderer.sprite = StoreVisuals.Pixel;
                overlayRenderer.color = Cyan;
                overlayRenderer.sortingOrder = 6;

                StoreStation station = body.AddComponent<StoreStation>();
                int max = entry.maxStock > 0 ? entry.maxStock : 8;
                int initial = entry.maxStock > 0 ? entry.initialStock : 5;
                float hold = entry.refillHoldSeconds > 0f ? entry.refillHoldSeconds : 3f;
                station.Configure(StoreStationKind.Shelf, label, hold, i);
                station.ConfigureShelfStock(initial, max, overlayRenderer);
                result.Add(station);
            }

            return result;
        }

        private static StoreStation BuildCounter(Transform parent, StoreConfig config)
        {
            GameObject go = YomiRoomTopDownPrototype.CreateBlock(parent, "Counter", CounterPosition,
                new Vector2(3.4f, 1.6f), ShelfWood, 5, true);
            YomiRoomTopDownPrototype.ApplyResourceSprite(go, "DatingSim/Store/Objects/Counter", new Vector2(3.4f, 1.6f));

            StoreStation station = go.AddComponent<StoreStation>();
            station.Configure(StoreStationKind.Checkout, "계산대", config.checkoutBaseSeconds);
            return station;
        }

        private static void BuildStorage(Transform parent, StoreConfig config)
        {
            GameObject rack = YomiRoomTopDownPrototype.CreateBlock(parent, "StorageRack", StoragePosition,
                new Vector2(2.0f, 1.6f), ShelfWood, 5, true);
            YomiRoomTopDownPrototype.ApplyResourceSprite(rack, "DatingSim/Store/Objects/StorageRack", new Vector2(2.0f, 1.6f));
            rack.AddComponent<StoreStation>().Configure(StoreStationKind.PickupStock, "재고 상자", 0.01f);

            GameObject cabinet = YomiRoomTopDownPrototype.CreateBlock(parent, "ToolCabinet", ToolPosition,
                new Vector2(1.4f, 1.6f), ShelfWood, 5, true);
            YomiRoomTopDownPrototype.ApplyResourceSprite(cabinet, "DatingSim/Store/Objects/ToolCabinet", new Vector2(1.4f, 1.6f));
            cabinet.AddComponent<StoreStation>().Configure(StoreStationKind.PickupTool, "청소도구", 0.01f);
        }

        private static Rigidbody2D BuildPlayer(Transform parent)
        {
            GameObject player = new("YomiPlayer", typeof(Rigidbody2D), typeof(CapsuleCollider2D));
            player.transform.SetParent(parent, false);
            player.transform.localPosition = new Vector3(0f, -3.6f, 0f);

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

            // ── 상단 HUD ───────────────────────────────────────────────
            RectTransform hud = YomiRoomTopDownPrototype.CreatePanel(root, "HUD",
                new Vector2(0.22f, 0.9f), new Vector2(0.78f, 0.985f), new Color32(7, 16, 31, 215));

            TMP_Text customerText = YomiRoomTopDownPrototype.CreateText(hud, "Customers", "처리 0  ·  이탈 0", 24f,
                new Vector2(0.02f, 0.1f), new Vector2(0.3f, 0.9f), Text);
            TMP_Text timeText = YomiRoomTopDownPrototype.CreateText(hud, "Time", "03:00", 30f,
                new Vector2(0.36f, 0.42f), new Vector2(0.64f, 0.95f), Text);

            Image timeFill = YomiRoomTopDownPrototype.CreateUIImage(hud, "TimeFill", StoreVisuals.Pixel, Pink,
                new Vector2(0.36f, 0.16f), new Vector2(0.64f, 0.32f));
            timeFill.type = Image.Type.Filled;
            timeFill.fillMethod = Image.FillMethod.Horizontal;

            TMP_Text payText = YomiRoomTopDownPrototype.CreateText(hud, "Pay", "0 원", 30f,
                new Vector2(0.7f, 0.1f), new Vector2(0.98f, 0.9f), Text);

            // ── 월드 추적 요소 ─────────────────────────────────────────
            RectTransform holdBar = MakeGauge(root, "HoldBar", new Vector2(230f, 40f), Cyan, out Image holdFill);
            TMP_Text carryText = MakeFloatingText(root, "Carry", 20f, Cyan, new Vector2(150f, 40f));
            TMP_Text promptText = MakeFloatingText(root, "Prompt", 22f, Cyan, new Vector2(330f, 44f));

            List<RectTransform> patienceBars = new();
            List<Image> patienceFills = new();
            for (int i = 0; i < QueueSlots.Length + 1; i++)
            {
                RectTransform bar = MakeGauge(root, $"Patience_{i}", new Vector2(130f, 22f), Color.green, out Image fill);
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
            RectTransform result = YomiRoomTopDownPrototype.CreatePanel(root, "ResultPanel",
                new Vector2(0.28f, 0.12f), new Vector2(0.72f, 0.9f), new Color32(10, 22, 39, 248));
            YomiRoomTopDownPrototype.CreateText(result, "Title", "근무 종료", 34f,
                new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f), Text).fontStyle = FontStyles.Bold;
            TMP_Text gradeText = YomiRoomTopDownPrototype.CreateText(result, "Grade", "A", 90f,
                new Vector2(0.35f, 0.68f), new Vector2(0.65f, 0.88f), Cyan);
            gradeText.fontStyle = FontStyles.Bold;

            TMP_Text bodyText = YomiRoomTopDownPrototype.CreateText(result, "Body", string.Empty, 22f,
                new Vector2(0.08f, 0.16f), new Vector2(0.92f, 0.66f), Text);
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.enableAutoSizing = false;

            Button confirm = YomiRoomTopDownPrototype.CreateButton(result, "Confirm", "확인",
                new Vector2(0.32f, 0.04f), new Vector2(0.68f, 0.14f), Cyan);

            StoreShiftUI ui = canvas.gameObject.AddComponent<StoreShiftUI>();
            ui.Configure(manager, controller, camera, canvasRect, timeText, timeFill, payText, customerText,
                feedbackText, introText, holdBar, holdFill, carryText, promptText,
                result.gameObject, gradeText, bodyText, confirm, shelves, shelfLabels, patienceBars, patienceFills);
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
            frame.color = new Color32(7, 16, 31, 220);
            frame.raycastTarget = false;

            GameObject fillGo = new("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.transform.SetParent(go.transform, false);
            RectTransform fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);
            fill = fillGo.GetComponent<Image>();
            fill.sprite = StoreVisuals.Pixel;
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
    }
}
