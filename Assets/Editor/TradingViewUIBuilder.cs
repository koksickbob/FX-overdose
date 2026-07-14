#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using FXOverdose.Trading;
using FXOverdose.UI.Chart;
using FXOverdose.UI.TopBar;

namespace FXOverdose.EditorTools
{
    public static class TradingViewUIBuilder
    {
        [InitializeOnLoadMethod]
        private static void AutoBuildOnRecompileOnce()
        {
            if (!UnityEditor.EditorPrefs.GetBool("FXOverdose_AutoBuildDone_v7", false))
            {
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    try
                    {
                        BuildTradingChartUI();
                        UnityEditor.EditorPrefs.SetBool("FXOverdose_AutoBuildDone_v7", true);
                        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
                        Debug.Log("[FX OVERDOSE] 🚀 주인공 AI 캐릭터 및 말풍선 UI 원클릭 자동 조립 & 씬 저장 완료!");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[FX OVERDOSE] 자동 UI 조립 중 오류 발생: {ex}");
                    }
                };
            }
        }

        [MenuItem("Tools/FX OVERDOSE/Build Trading Chart UI")]
        public static void BuildTradingChartUI()
        {
            Debug.Log("[FX OVERDOSE] 신규 트레이딩뷰 전용 캔버스 원클릭 조립을 시작합니다...");

            // 1. 기존 TradingViewCanvas 검색 및 삭제 (중복 방지)
            GameObject existingCanvas = GameObject.Find("TradingViewCanvas");
            if (existingCanvas != null)
            {
                Undo.DestroyObjectImmediate(existingCanvas);
                Debug.Log("[FX OVERDOSE] 기존 TradingViewCanvas를 제거했습니다.");
            }

            // 2. EventSystem 확인 및 Input System Package 호환 Module 자동 연결
            UnityEngine.EventSystems.EventSystem[] existingEventSystems = Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include);
            if (existingEventSystems == null || existingEventSystems.Length == 0)
            {
                GameObject esGO = new GameObject("EventSystem");
                esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                Undo.RegisterCreatedObjectUndo(esGO, "Create EventSystem");
            }

            // 모든 EventSystem에서 구형 StandaloneInputModule 제거 및 InputSystemUIInputModule 연결 (Player Settings Input System Package 호환)
            foreach (var es in Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include))
            {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                var standaloneModule = es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                if (standaloneModule != null)
                {
                    Undo.DestroyObjectImmediate(standaloneModule);
                    Debug.Log("[FX OVERDOSE] EventSystem에서 구형 StandaloneInputModule을 제거했습니다 (Input System Package 충돌 방지).");
                }

                System.Type inputSystemModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (inputSystemModuleType != null && es.GetComponent(inputSystemModuleType) == null)
                {
                    es.gameObject.AddComponent(inputSystemModuleType);
                    Debug.Log("[FX OVERDOSE] EventSystem에 InputSystemUIInputModule을 자동 연결했습니다.");
                }
#else
                if (es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>() == null && es.GetComponent<UnityEngine.EventSystems.BaseInputModule>() == null)
                {
                    es.gameObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                }
#endif
            }

            // 3. 신규 캔버스 생성 (1920x1080 기준 ScreenSpaceCamera 또는 Overlay)
            GameObject canvasGO = new GameObject("TradingViewCanvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();

            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camGO = GameObject.FindWithTag("MainCamera");
                if (camGO != null) mainCam = camGO.GetComponent<Camera>();
            }
            if (mainCam == null)
            {
                mainCam = Object.FindAnyObjectByType<Camera>();
            }

            if (mainCam != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 5;
                Debug.Log($"[FX OVERDOSE] 메인 카메라({mainCam.name}) 확인 완료. 캔버스 렌더 모드가 ScreenSpaceOverlay(Screen Space - Overlay)로 설정되었습니다.");
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 5;
                Debug.LogWarning("[FX OVERDOSE] ScreenSpaceOverlay 모드로 캔버스를 생성합니다.");
            }

            canvasGO.AddComponent<GraphicRaycaster>();

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Undo.RegisterCreatedObjectUndo(canvasGO, "Build Trading Chart UI");

            // 4. Prefab 디렉토리 및 Prefab 리소스 준비
            string prefabDir = "Assets/Prefabs/UI";
            if (!Directory.Exists(prefabDir))
            {
                Directory.CreateDirectory(prefabDir);
            }

            Image lineSegmentPrefab = CreateOrUpdateLineSegmentPrefab(prefabDir);
            CandleItemUI candlePrefab = CreateOrUpdateCandleItemPrefab(prefabDir);

            // 5. 코어 엔진 보장 및 바인딩
            EnsureCoreEngines(out GameManager gm, out FXOverdose.Trading.MarketSimulationEngine mse, out FXOverdose.Trading.TradingController tc, out TraderStatus ts);

            // 6. 메인 카메라 뷰의 좌측 절반 (Left Half: 0.0 ~ 0.5) 전용 루트 컨테이너 생성
            GameObject leftContainerGO = CreateUIObject("LeftHalfTradingContainer", canvasGO.transform);
            RectTransform leftContainerRect = leftContainerGO.GetComponent<RectTransform>();
            leftContainerRect.anchorMin = new Vector2(0f, 0f);
            leftContainerRect.anchorMax = new Vector2(0.5f, 1f);
            leftContainerRect.offsetMin = Vector2.zero;
            leftContainerRect.offsetMax = Vector2.zero;

            // 7. 좌측 절반 컨테이너 내부에 3대 패널 생성 및 조립
            CreateTopStatusBarPanel(leftContainerGO.transform, lineSegmentPrefab, gm, tc);
            CreateChartMainPanel(leftContainerGO.transform, candlePrefab, mse);
            CreateBottomTradingPanel(leftContainerGO.transform, tc, gm);

            // 8. 메인 카메라 뷰의 우측 절반 (Right Half: 0.5 ~ 1.0) 주인공 AI 캐릭터 + 말풍선 + 체력/멘탈 연동 UI 생성
            GameObject rightContainerGO = CreateUIObject("RightHalfAIContainer", canvasGO.transform);
            RectTransform rightContainerRect = rightContainerGO.GetComponent<RectTransform>();
            rightContainerRect.anchorMin = new Vector2(0.5f, 0f);
            rightContainerRect.anchorMax = new Vector2(1f, 1f);
            rightContainerRect.offsetMin = Vector2.zero;
            rightContainerRect.offsetMax = Vector2.zero;
            // 배경 누끼 및 기존 UI(HP, 멘탈, 아이템 등) 가림 방지를 위해 투명하게 유지 (Image 컴포넌트 추가하지 않음)

            CreateRightHalfAIPanel(rightContainerGO.transform, gm, mse, tc, ts);

            // 9. 기존 메인 배경(BackGround)은 TradingViewCanvas(5) 뒤(0)에 그대로 두어 차트와 캐릭터가 가려지지 않게 하고,
            //    아이템 UI(ItemButtons) 및 상점 버튼이 포함된 HUD 오브젝트에 독립 Canvas(sortingOrder: 20)와 GraphicRaycaster를 부여하여 최상단 클릭 보장
            GameObject hudGO = GameObject.Find("HUD");
            if (hudGO != null)
            {
                Canvas hudCanvas = hudGO.GetComponent<Canvas>();
                if (hudCanvas == null) hudCanvas = hudGO.AddComponent<Canvas>();
                hudCanvas.overrideSorting = true;
                hudCanvas.sortingOrder = 20;

                GraphicRaycaster hudRaycaster = hudGO.GetComponent<GraphicRaycaster>();
                if (hudRaycaster == null) hudRaycaster = hudGO.AddComponent<GraphicRaycaster>();

                hudGO.transform.SetAsLastSibling();
                Debug.Log("[FX OVERDOSE] 우측 하단 아이템 UI 및 HUD 클릭 보장과 차트 가림 방지를 위해 HUD에 독립 Canvas(sortingOrder: 20)를 부여했습니다.");
            }

            GameObject mainCanvasGO = GameObject.Find("Canvas");
            if (mainCanvasGO != null)
            {
                Canvas mainCanvas = mainCanvasGO.GetComponent<Canvas>();
                if (mainCanvas != null)
                {
                    mainCanvas.overrideSorting = false;
                    mainCanvas.sortingOrder = 0;
                }
            }

            // 씬 갱신 및 dirty 표시
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            Debug.Log("[FX OVERDOSE] 🚀 신규 차트 전용 캔버스 (TradingViewCanvas) 및 주인공 AI 매매 연동 UI 조립 완료!");
        }

        // =========================================================================================
        // [Prefab 자동 생성기]
        // =========================================================================================

        private static Image CreateOrUpdateLineSegmentPrefab(string dir)
        {
            string path = $"{dir}/LineSegmentUI.prefab";
            GameObject go = new GameObject("LineSegmentUI", typeof(RectTransform), typeof(Image));
            Image img = go.GetComponent<Image>();
            img.color = Color.white;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);

            GameObject prefabGO = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefabGO.GetComponent<Image>();
        }

        private static CandleItemUI CreateOrUpdateCandleItemPrefab(string dir)
        {
            string path = $"{dir}/CandleItemUI.prefab";
            GameObject go = new GameObject("CandleItemUI", typeof(RectTransform), typeof(CandleItemUI));
            RectTransform candleRootRect = go.GetComponent<RectTransform>();
            candleRootRect.anchorMin = Vector2.zero;
            candleRootRect.anchorMax = Vector2.zero;
            candleRootRect.pivot = Vector2.zero;
            CandleItemUI script = go.GetComponent<CandleItemUI>();

            // 윗꼬리 (Upper Wick)
            GameObject upperWickGO = CreateUIObject("UpperWick", go.transform);
            Image upperWickImg = upperWickGO.AddComponent<Image>();
            RectTransform upperWickRect = upperWickGO.GetComponent<RectTransform>();
            upperWickRect.sizeDelta = new Vector2(2f, 20f);

            // 몸통 (Body)
            GameObject bodyGO = CreateUIObject("Body", go.transform);
            Image bodyImg = bodyGO.AddComponent<Image>();
            RectTransform bodyRect = bodyGO.GetComponent<RectTransform>();
            bodyRect.sizeDelta = new Vector2(10f, 40f);

            // 아랫꼬리 (Lower Wick)
            GameObject lowerWickGO = CreateUIObject("LowerWick", go.transform);
            Image lowerWickImg = lowerWickGO.AddComponent<Image>();
            RectTransform lowerWickRect = lowerWickGO.GetComponent<RectTransform>();
            lowerWickRect.sizeDelta = new Vector2(2f, 20f);

            // 거래량 바 (VolumeBar)
            GameObject volGO = CreateUIObject("VolumeBar", go.transform);
            Image volImg = volGO.AddComponent<Image>();
            RectTransform volRect = volGO.GetComponent<RectTransform>();
            volRect.sizeDelta = new Vector2(8f, 30f);

            // C# Reflection으로 private field 설정
            SetField(script, "candleContainerTransform", go.GetComponent<RectTransform>());
            SetField(script, "upperWickRect", upperWickRect);
            SetField(script, "bodyRect", bodyRect);
            SetField(script, "lowerWickRect", lowerWickRect);
            SetField(script, "upperWickImage", upperWickImg);
            SetField(script, "bodyImage", bodyImg);
            SetField(script, "lowerWickImage", lowerWickImg);
            SetField(script, "volumeBarRect", volRect);
            SetField(script, "volumeBarImage", volImg);

            GameObject prefabGO = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefabGO.GetComponent<CandleItemUI>();
        }

        // =========================================================================================
        // [1. 상단 상태 바 생성] TopStatusBarPanel (80px, 3개의 독립 카드 박스 배치)
        // =========================================================================================

        private static void CreateTopStatusBarPanel(Transform parent, Image lineSegmentPrefab, GameManager gm, FXOverdose.Trading.TradingController tc)
        {
            GameObject panelGO = CreateUIObject("TopStatusBarPanel", parent);
            RectTransform panelRect = panelGO.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.offsetMin = new Vector2(12f, 0f);
            panelRect.offsetMax = new Vector2(-12f, 0f);
            panelRect.sizeDelta = new Vector2(0f, 80f);

            Image panelBg = panelGO.AddComponent<Image>();
            panelBg.color = new Color(0.043f, 0.059f, 0.098f, 1f); // #0B0F19 Dark Background

            HorizontalLayoutGroup layout = panelGO.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 12f;
            layout.padding = new RectOffset(14, 14, 8, 8);
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            TopStatusBarUIController controller = panelGO.AddComponent<TopStatusBarUIController>();

            // 카드 1: 날짜 및 시간 Card
            GameObject dayTimeCard = CreateUIObject("DayTimeCard", panelGO.transform);
            Image dtCardBg = dayTimeCard.AddComponent<Image>();
            dtCardBg.color = new Color(0.075f, 0.11f, 0.19f, 1f); // #131C31 Dark Blue Card
            LayoutElement dtElem = dayTimeCard.AddComponent<LayoutElement>();
            dtElem.preferredWidth = 180f;

            VerticalLayoutGroup dtLayout = dayTimeCard.AddComponent<VerticalLayoutGroup>();
            dtLayout.childAlignment = TextAnchor.MiddleCenter;
            dtLayout.padding = new RectOffset(12, 12, 6, 6);
            TMP_Text dayText = CreateTMPText("DayLabel", dayTimeCard.transform, "DAY 03", 15, Color.white);
            dayText.fontStyle = FontStyles.Bold;
            dayText.alignment = TextAlignmentOptions.Center;
            TMP_Text timeText = CreateTMPText("TimeLabel", dayTimeCard.transform, "23:47", 24, new Color(0.024f, 0.714f, 0.831f, 1f));
            timeText.fontStyle = FontStyles.Bold;
            timeText.alignment = TextAlignmentOptions.Center;

            // 카드 2: 총 자산 (BALANCE) Card
            GameObject balanceCard = CreateUIObject("BalanceCard", panelGO.transform);
            Image balCardBg = balanceCard.AddComponent<Image>();
            balCardBg.color = new Color(0.075f, 0.11f, 0.19f, 1f);
            LayoutElement balElem = balanceCard.AddComponent<LayoutElement>();
            balElem.preferredWidth = 250f;

            VerticalLayoutGroup balLayout = balanceCard.AddComponent<VerticalLayoutGroup>();
            balLayout.childAlignment = TextAnchor.MiddleLeft;
            balLayout.padding = new RectOffset(18, 18, 6, 6);
            CreateTMPText("BalanceTitle", balanceCard.transform, "BALANCE", 12, new Color(0.58f, 0.64f, 0.72f, 1f));
            TMP_Text balValueText = CreateTMPText("BalanceValue", balanceCard.transform, "$12,458.36", 24, Color.white);
            balValueText.fontStyle = FontStyles.Bold;

            // 카드 3: P&L 및 스파크라인 Card
            GameObject pnlCard = CreateUIObject("PnLCard", panelGO.transform);
            Image pnlCardBg = pnlCard.AddComponent<Image>();
            pnlCardBg.color = new Color(0.075f, 0.11f, 0.19f, 1f);
            LayoutElement pnlElem = pnlCard.AddComponent<LayoutElement>();
            pnlElem.preferredWidth = 454f;

            HorizontalLayoutGroup pnlCardLayout = pnlCard.AddComponent<HorizontalLayoutGroup>();
            pnlCardLayout.childAlignment = TextAnchor.MiddleLeft;
            pnlCardLayout.padding = new RectOffset(18, 18, 6, 6);
            pnlCardLayout.spacing = 20f;

            GameObject pnlTextContainer = CreateUIObject("PnLTextGroup", pnlCard.transform);
            VerticalLayoutGroup pnlTextLayout = pnlTextContainer.AddComponent<VerticalLayoutGroup>();
            pnlTextLayout.childAlignment = TextAnchor.MiddleLeft;
            CreateTMPText("PnLTitle", pnlTextContainer.transform, "P&L", 12, new Color(0.58f, 0.64f, 0.72f, 1f));
            TMP_Text pnlPctText = CreateTMPText("PnLPct", pnlTextContainer.transform, "+18.47%", 24, new Color(0.133f, 0.773f, 0.369f, 1f));
            pnlPctText.fontStyle = FontStyles.Bold;
            TMP_Text pnlAmtText = CreateTMPText("PnLAmt", pnlTextContainer.transform, "+$1,458.36", 13, new Color(0.133f, 0.773f, 0.369f, 1f));

            GameObject sparkContainer = CreateUIObject("SparklineContainer", pnlCard.transform);
            RectTransform sparkRect = sparkContainer.GetComponent<RectTransform>();
            LayoutElement sparkLayoutElem = sparkContainer.AddComponent<LayoutElement>();
            sparkLayoutElem.preferredWidth = 140f;
            sparkLayoutElem.preferredHeight = 44f;

            SparklineRenderer sparkline = sparkContainer.AddComponent<SparklineRenderer>();
            SetField(sparkline, "containerTransform", sparkRect);
            SetField(sparkline, "lineSegmentPrefab", lineSegmentPrefab);
            SetField(sparkline, "lineWidth", 2.5f);

            // 컨트롤러 연결
            SetField(controller, "dayLabel", dayText);
            SetField(controller, "timeLabel", timeText);
            SetField(controller, "balanceValueLabel", balValueText);
            SetField(controller, "pnlPercentageLabel", pnlPctText);
            SetField(controller, "pnlAmountLabel", pnlAmtText);
            SetField(controller, "sparklineRenderer", sparkline);
            if (gm != null) SetField(controller, "gameManager", gm);
            if (tc != null) SetField(controller, "tradingController", tc);
        }

        // =========================================================================================
        // [2. 중앙 차트 영역 생성] ChartMainPanel (상단 바와 하단 카드 사이 정확한 간격 분리)
        // =========================================================================================

        private static void CreateChartMainPanel(Transform parent, CandleItemUI candlePrefab, FXOverdose.Trading.MarketSimulationEngine mse)
        {
            GameObject panelGO = CreateUIObject("ChartMainPanel", parent);
            RectTransform panelRect = panelGO.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0.31f);
            panelRect.anchorMax = new Vector2(1f, 0.92f);
            panelRect.offsetMin = new Vector2(12f, 8f);
            panelRect.offsetMax = new Vector2(-12f, -6f);

            Image panelBg = panelGO.AddComponent<Image>();
            panelBg.color = new Color(0.055f, 0.082f, 0.14f, 1f); // #0E1524 Clean Chart Background

            ChartUIController controller = panelGO.AddComponent<ChartUIController>();

            // 2-1. 헤더 로우 (상단 56px 고정 - 좌측 가격정보, 우측 타임프레임 버튼 격리)
            GameObject headerRow = CreateUIObject("ChartHeaderRow", panelGO.transform);
            RectTransform headerRect = headerRow.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = Vector2.zero;
            headerRect.sizeDelta = new Vector2(0f, 56f);

            HorizontalLayoutGroup headerLayout = headerRow.AddComponent<HorizontalLayoutGroup>();
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            headerLayout.padding = new RectOffset(16, 16, 6, 6);
            headerLayout.spacing = 16f;

            // 좌측: 심볼 및 가격 정보 박스
            GameObject titlePriceBox = CreateUIObject("TitlePriceBox", headerRow.transform);
            LayoutElement tpElem = titlePriceBox.AddComponent<LayoutElement>();
            tpElem.flexibleWidth = 1f;
            VerticalLayoutGroup tpLayout = titlePriceBox.AddComponent<VerticalLayoutGroup>();
            tpLayout.childAlignment = TextAnchor.MiddleLeft;
            tpLayout.spacing = 2f;

            HorizontalLayoutGroup symbolLayoutGroup = CreateUIObject("SymbolRow", titlePriceBox.transform).AddComponent<HorizontalLayoutGroup>();
            symbolLayoutGroup.childAlignment = TextAnchor.MiddleLeft;
            symbolLayoutGroup.spacing = 8f;
            TMP_Text symText = CreateTMPText("Symbol", symbolLayoutGroup.transform, "BTC/USDT *", 18, Color.white);
            symText.fontStyle = FontStyles.Bold;

            HorizontalLayoutGroup priceRow = CreateUIObject("PriceRow", titlePriceBox.transform).AddComponent<HorizontalLayoutGroup>();
            priceRow.childAlignment = TextAnchor.MiddleLeft;
            priceRow.spacing = 12f;
            TMP_Text priceHead = CreateTMPText("PriceHeaderLabel", priceRow.transform, "67,842.1", 22, new Color(0.024f, 0.714f, 0.831f, 1f));
            priceHead.fontStyle = FontStyles.Bold;
            TMP_Text priceChg = CreateTMPText("PriceChangeLabel", priceRow.transform, "≈ $67,842.10  +1.24%", 13, new Color(0.133f, 0.773f, 0.369f, 1f));

            // 우측: 타임프레임 버튼 그룹
            GameObject tfBar = CreateUIObject("TimeframeButtons", headerRow.transform);
            HorizontalLayoutGroup tfLayout = tfBar.AddComponent<HorizontalLayoutGroup>();
            tfLayout.childAlignment = TextAnchor.MiddleRight;
            tfLayout.spacing = 6f;

            Button btn1m = CreateButton("Btn1m", tfBar.transform, "1m", 46, 32);
            Button btn5m = CreateButton("Btn5m", tfBar.transform, "5m", 46, 32, new Color(0.024f, 0.714f, 0.831f, 1f));
            Button btn15m = CreateButton("Btn15m", tfBar.transform, "15m", 46, 32);
            Button btn1h = CreateButton("Btn1h", tfBar.transform, "1h", 46, 32);
            Button btn4h = CreateButton("Btn4h", tfBar.transform, "4h", 46, 32);
            Button btn1D = CreateButton("Btn1D", tfBar.transform, "1D", 46, 32);

            // 2-2. 차트 렌더링 캔버스 영역 (ChartArea - 헤더 56px, 우측 66px, 하단 26px 여백 분리)
            GameObject chartArea = CreateUIObject("ChartArea", panelGO.transform);
            RectTransform chartAreaRect = chartArea.GetComponent<RectTransform>();
            chartAreaRect.anchorMin = Vector2.zero;
            chartAreaRect.anchorMax = Vector2.one;
            chartAreaRect.offsetMin = new Vector2(10f, 26f);
            chartAreaRect.offsetMax = new Vector2(-66f, -56f);

            // 2-2-1. 거래량 구역(0~22%) 및 차트 구역(26~100%) 구분선 (Y = 0.24)
            GameObject volSeparator = CreateUIObject("VolumeSeparatorLine", chartArea.transform);
            RectTransform volSepRect = volSeparator.GetComponent<RectTransform>();
            volSepRect.anchorMin = new Vector2(0f, 0.24f);
            volSepRect.anchorMax = new Vector2(1f, 0.24f);
            volSepRect.sizeDelta = new Vector2(0f, 1f);
            Image volSepImg = volSeparator.AddComponent<Image>();
            volSepImg.color = new Color(0.18f, 0.23f, 0.33f, 0.8f); // #1E293B 슬레이트 구분선

            // 2-3. 실시간 현재가 점선 라인 (CurrentPriceLine)
            GameObject cpLine = CreateUIObject("CurrentPriceLine", chartArea.transform);
            RectTransform cpRect = cpLine.GetComponent<RectTransform>();
            cpRect.anchorMin = new Vector2(0f, 0.5f);
            cpRect.anchorMax = new Vector2(1f, 0.5f);
            cpRect.sizeDelta = new Vector2(0f, 2f);
            Image cpImg = cpLine.AddComponent<Image>();
            cpImg.color = new Color(0.024f, 0.714f, 0.831f, 0.8f);

            GameObject cpTag = CreateUIObject("PriceTag", cpLine.transform);
            RectTransform cpTagRect = cpTag.GetComponent<RectTransform>();
            cpTagTagSize(cpTagRect);
            cpTagRect.anchorMin = new Vector2(1f, 0.5f);
            cpTagRect.anchorMax = new Vector2(1f, 0.5f);
            cpTagRect.pivot = new Vector2(0f, 0.5f);
            cpTagRect.anchoredPosition = new Vector2(0f, 0f);
            Image cpTagBg = cpTag.AddComponent<Image>();
            cpTagBg.color = new Color(0.024f, 0.714f, 0.831f, 1f);
            TMP_Text cpTagText = CreateTMPText("PriceTagText", cpTag.transform, "67,842.1", 13, Color.white);
            cpTagText.alignment = TextAlignmentOptions.Center;

            // 우측 Y축 눈금 컨테이너 (너비 66px)
            GameObject yAxisContainer = CreateUIObject("YAxisContainer", panelGO.transform);
            RectTransform yAxisRect = yAxisContainer.GetComponent<RectTransform>();
            yAxisRect.anchorMin = new Vector2(1f, 0f);
            yAxisRect.anchorMax = new Vector2(1f, 1f);
            yAxisRect.pivot = new Vector2(1f, 0.5f);
            yAxisRect.offsetMin = new Vector2(-66f, 26f);
            yAxisRect.offsetMax = new Vector2(0f, -56f);

            TMP_Text[] yLabels = new TMP_Text[6];
            for (int i = 0; i < 6; i++)
            {
                GameObject lblGO = CreateUIObject($"YLabel_{i}", yAxisContainer.transform);
                RectTransform lblRect = lblGO.GetComponent<RectTransform>();
                float normalizedY = 0.26f + (i / 5f) * 0.74f; // 상단 가격 차트 전용 구역(26% ~ 100%)으로 눈금 재배치
                lblRect.anchorMin = new Vector2(0f, normalizedY);
                lblRect.anchorMax = new Vector2(1f, normalizedY);
                lblRect.sizeDelta = new Vector2(0f, 18f);
                yLabels[i] = CreateTMPText("Text", lblGO.transform, "68,000.0", 11, new Color(0.58f, 0.64f, 0.72f, 1f));
                yLabels[i].alignment = TextAlignmentOptions.Right;
            }

            // 하단 거래량 구역 식별용 Y축 라벨
            GameObject volLblGO = CreateUIObject("VolumeAxisLabel", yAxisContainer.transform);
            RectTransform volLblRect = volLblGO.GetComponent<RectTransform>();
            volLblRect.anchorMin = new Vector2(0f, 0.11f);
            volLblRect.anchorMax = new Vector2(1f, 0.11f);
            volLblRect.sizeDelta = new Vector2(0f, 18f);
            TMP_Text volLblText = CreateTMPText("Text", volLblGO.transform, "VOL", 11, new Color(0.40f, 0.46f, 0.55f, 1f));
            volLblText.alignment = TextAlignmentOptions.Right;

            // 하단 X축 눈금 컨테이너 (높이 26px)
            GameObject xAxisContainer = CreateUIObject("XAxisContainer", panelGO.transform);
            RectTransform xAxisRect = xAxisContainer.GetComponent<RectTransform>();
            xAxisRect.anchorMin = new Vector2(0f, 0f);
            xAxisRect.anchorMax = new Vector2(1f, 0f);
            xAxisRect.offsetMin = new Vector2(10f, 0f);
            xAxisRect.offsetMax = new Vector2(-66f, 26f);

            TMP_Text[] xLabels = new TMP_Text[6];
            string[] timeSamples = { "18:00", "19:30", "21:00", "22:30", "23:47", "01:00" };
            for (int i = 0; i < 6; i++)
            {
                GameObject lblGO = CreateUIObject($"XLabel_{i}", xAxisContainer.transform);
                RectTransform lblRect = lblGO.GetComponent<RectTransform>();
                float normalizedX = i / 5f;
                lblRect.anchorMin = new Vector2(normalizedX, 0f);
                lblRect.anchorMax = new Vector2(normalizedX, 1f);
                lblRect.sizeDelta = new Vector2(50f, 0f);
                Color timeColor = (i == 4) ? new Color(0.024f, 0.714f, 0.831f, 1f) : new Color(0.58f, 0.64f, 0.72f, 1f);
                xLabels[i] = CreateTMPText("Text", lblGO.transform, timeSamples[i], 11, timeColor);
                xLabels[i].alignment = TextAlignmentOptions.Center;
            }

            // 컨트롤러 연결
            SetField(controller, "priceHeaderLabel", priceHead);
            SetField(controller, "priceChangeLabel", priceChg);
            SetField(controller, "btn1m", btn1m);
            SetField(controller, "btn5m", btn5m);
            SetField(controller, "btn15m", btn15m);
            SetField(controller, "btn1h", btn1h);
            SetField(controller, "btn4h", btn4h);
            SetField(controller, "btn1D", btn1D);
            SetField(controller, "chartAreaTransform", chartAreaRect);
            SetField(controller, "candlePrefab", candlePrefab);
            SetField(controller, "yAxisPriceLabels", yLabels);
            SetField(controller, "xAxisTimeLabels", xLabels);
            SetField(controller, "currentPriceLineTransform", cpRect);
            SetField(controller, "currentPriceLineImage", cpImg);
            SetField(controller, "currentPriceTagRect", cpTagRect);
            SetField(controller, "currentPriceTagText", cpTagText);
            SetField(controller, "currentPriceTagBackground", cpTagBg);
            if (mse != null) SetField(controller, "marketEngine", mse);
        }

        private static void cpTagTagSize(RectTransform r)
        {
            r.sizeDelta = new Vector2(65f, 22f);
        }

        // =========================================================================================
        // [3. 하단 매매/비율 조작부 생성] BottomTradingPanel (Image 1 1:1 수평 3-Card 배치 구조)
        // =========================================================================================

        private static void CreateBottomTradingPanel(Transform parent, FXOverdose.Trading.TradingController tc, GameManager gm)
        {
            GameObject panelGO = CreateUIObject("BottomTradingPanel", parent);
            RectTransform panelRect = panelGO.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0.31f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            Image panelBg = panelGO.AddComponent<Image>();
            panelBg.color = new Color(0.043f, 0.059f, 0.098f, 1f); // #0B0F19 Dark Background

            HorizontalLayoutGroup cardsLayout = panelGO.AddComponent<HorizontalLayoutGroup>();
            cardsLayout.childAlignment = TextAnchor.MiddleCenter;
            cardsLayout.spacing = 12f;
            cardsLayout.padding = new RectOffset(14, 14, 10, 12);
            cardsLayout.childForceExpandWidth = false;
            cardsLayout.childForceExpandHeight = true;

            TradingPanelUIController controller = panelGO.AddComponent<TradingPanelUIController>();

            // -------------------------------------------------------------------------------------
            // [카드 1: 녹색 LONG 매수 버튼 카드]
            // -------------------------------------------------------------------------------------
            GameObject longCard = CreateUIObject("LongButtonCard", panelGO.transform);
            Image longBg = longCard.AddComponent<Image>();
            longBg.color = new Color(0.133f, 0.773f, 0.369f, 0.2f); // Dark Green Background
            Button btnLong = longCard.AddComponent<Button>();
            LayoutElement longElem = longCard.AddComponent<LayoutElement>();
            longElem.preferredWidth = 264f;

            VerticalLayoutGroup longVLayout = longCard.AddComponent<VerticalLayoutGroup>();
            longVLayout.childAlignment = TextAnchor.MiddleCenter;
            longVLayout.spacing = 16f;
            longVLayout.padding = new RectOffset(14, 14, 24, 20);

            TMP_Text longTitle = CreateTMPText("LongTitle", longCard.transform, "^ LONG", 34, new Color(0.133f, 0.773f, 0.369f, 1f));
            longTitle.fontStyle = FontStyles.Bold;
            longTitle.alignment = TextAlignmentOptions.Center;

            GameObject longPill = CreateUIObject("LongPill", longCard.transform);
            Image longPillImg = longPill.AddComponent<Image>();
            longPillImg.color = new Color(0.133f, 0.773f, 0.369f, 0.8f);
            LayoutElement longPillElem = longPill.AddComponent<LayoutElement>();
            longPillElem.preferredWidth = 210f;
            longPillElem.preferredHeight = 44f;
            TMP_Text longSubText = CreateTMPText("Text", longPill.transform, "AI 매수 판단 대기중", 15, Color.white);
            longSubText.alignment = TextAlignmentOptions.Center;
            RectTransform lpTextRect = longSubText.GetComponent<RectTransform>();
            lpTextRect.anchorMin = Vector2.zero; lpTextRect.anchorMax = Vector2.one;
            lpTextRect.offsetMin = lpTextRect.offsetMax = Vector2.zero;

            // -------------------------------------------------------------------------------------
            // [카드 2: 적색 SHORT 매도 버튼 카드]
            // -------------------------------------------------------------------------------------
            GameObject shortCard = CreateUIObject("ShortButtonCard", panelGO.transform);
            Image shortBg = shortCard.AddComponent<Image>();
            shortBg.color = new Color(0.937f, 0.267f, 0.267f, 0.2f); // Dark Red Background
            Button btnShort = shortCard.AddComponent<Button>();
            LayoutElement shortElem = shortCard.AddComponent<LayoutElement>();
            shortElem.preferredWidth = 264f;

            VerticalLayoutGroup shortVLayout = shortCard.AddComponent<VerticalLayoutGroup>();
            shortVLayout.childAlignment = TextAnchor.MiddleCenter;
            shortVLayout.spacing = 16f;
            shortVLayout.padding = new RectOffset(14, 14, 24, 20);

            TMP_Text shortTitle = CreateTMPText("ShortTitle", shortCard.transform, "v SHORT", 34, new Color(0.937f, 0.267f, 0.267f, 1f));
            shortTitle.fontStyle = FontStyles.Bold;
            shortTitle.alignment = TextAlignmentOptions.Center;

            GameObject shortPill = CreateUIObject("ShortPill", shortCard.transform);
            Image shortPillImg = shortPill.AddComponent<Image>();
            shortPillImg.color = new Color(0.937f, 0.267f, 0.267f, 0.8f);
            LayoutElement shortPillElem = shortPill.AddComponent<LayoutElement>();
            shortPillElem.preferredWidth = 210f;
            shortPillElem.preferredHeight = 44f;
            TMP_Text shortSubText = CreateTMPText("Text", shortPill.transform, "AI 매도 판단 대기중", 15, Color.white);
            shortSubText.alignment = TextAlignmentOptions.Center;
            RectTransform spTextRect = shortSubText.GetComponent<RectTransform>();
            spTextRect.anchorMin = Vector2.zero; spTextRect.anchorMax = Vector2.one;
            spTextRect.offsetMin = spTextRect.offsetMax = Vector2.zero;

            // -------------------------------------------------------------------------------------
            // [카드 3: 다크 블루 LEVERAGE & MARGIN 조작부 및 포지션 오버레이 박스]
            // -------------------------------------------------------------------------------------
            GameObject controlCard = CreateUIObject("ControlBoxCard", panelGO.transform);
            Image ctrlBg = controlCard.AddComponent<Image>();
            ctrlBg.color = new Color(0.075f, 0.11f, 0.19f, 1f); // #131C31 Dark Slate
            LayoutElement ctrlElem = controlCard.AddComponent<LayoutElement>();
            ctrlElem.preferredWidth = 356f;

            VerticalLayoutGroup ctrlVLayout = controlCard.AddComponent<VerticalLayoutGroup>();
            ctrlVLayout.childAlignment = TextAnchor.UpperCenter;
            ctrlVLayout.spacing = 10f;
            ctrlVLayout.padding = new RectOffset(12, 12, 12, 12);

            // 상단 헤더 탭 (LEVERAGE MODE vs MARGIN RATIO)
            GameObject tabsBar = CreateUIObject("TabsBar", controlCard.transform);
            HorizontalLayoutGroup tabsLayout = tabsBar.AddComponent<HorizontalLayoutGroup>();
            tabsLayout.childAlignment = TextAnchor.MiddleCenter;
            tabsLayout.spacing = 8f;
            Button btnTabLev = CreateButton("BtnTabLeverageMode", tabsBar.transform, "LEVERAGE", 145, 32);
            Button btnTabMar = CreateButton("BtnTabMarginRatioMode", tabsBar.transform, "MARGIN", 145, 32);

            // 레버리지 조작부 컨테이너
            GameObject levContainer = CreateUIObject("Container_LeverageMode", controlCard.transform);
            VerticalLayoutGroup levVLayout = levContainer.AddComponent<VerticalLayoutGroup>();
            levVLayout.childAlignment = TextAnchor.MiddleCenter;
            levVLayout.spacing = 10f;

            GameObject levBox = CreateUIObject("LeverageBox", levContainer.transform);
            HorizontalLayoutGroup levHLayout = levBox.AddComponent<HorizontalLayoutGroup>();
            levHLayout.childAlignment = TextAnchor.MiddleCenter;
            levHLayout.spacing = 14f;
            Button btnLevMinus = CreateButton("BtnLevMinus", levBox.transform, "-", 48, 42);
            TMP_Text levDisplay = CreateTMPText("LevDisplay", levBox.transform, "10x", 30, Color.white);
            levDisplay.fontStyle = FontStyles.Bold;
            levDisplay.alignment = TextAlignmentOptions.Center;
            LayoutElement levDispElem = levDisplay.gameObject.AddComponent<LayoutElement>();
            levDispElem.preferredWidth = 100f;
            Button btnLevPlus = CreateButton("BtnLevPlus", levBox.transform, "+", 48, 42);

            GameObject levPresets = CreateUIObject("LevPresets", levContainer.transform);
            HorizontalLayoutGroup levPresetLayout = levPresets.AddComponent<HorizontalLayoutGroup>();
            levPresetLayout.childAlignment = TextAnchor.MiddleCenter;
            levPresetLayout.spacing = 6f;
            Button btn1x = CreateButton("Btn1x", levPresets.transform, "1x", 56, 34);
            Button btn5x = CreateButton("Btn5x", levPresets.transform, "5x", 56, 34);
            Button btn10x = CreateButton("Btn10x", levPresets.transform, "10x", 56, 34);
            Button btn25x = CreateButton("Btn25x", levPresets.transform, "25x", 56, 34);
            Button btn50x = CreateButton("Btn50x", levPresets.transform, "50x", 56, 34);
            Button btn100x = CreateButton("Btn100x", levPresets.transform, "100x", 56, 34);
            Button btn125x = CreateButton("Btn125x", levPresets.transform, "125x", 56, 34);
            btn100x.gameObject.SetActive(false); btn125x.gameObject.SetActive(false);

            // 투자비율 (Margin Ratio) 조작부 컨테이너
            GameObject marContainer = CreateUIObject("Container_MarginRatioMode", controlCard.transform);
            VerticalLayoutGroup marVLayout = marContainer.AddComponent<VerticalLayoutGroup>();
            marVLayout.childAlignment = TextAnchor.MiddleCenter;
            marVLayout.spacing = 10f;

            GameObject marBox = CreateUIObject("MarginRatioBox", marContainer.transform);
            HorizontalLayoutGroup marHLayout = marBox.AddComponent<HorizontalLayoutGroup>();
            marHLayout.childAlignment = TextAnchor.MiddleCenter;
            marHLayout.spacing = 12f;
            Button btnMarMinus = CreateButton("BtnMarMinus", marBox.transform, "- 10%", 68, 42);
            TMP_Text marDisplay = CreateTMPText("MarDisplay", marBox.transform, "30% ($3,737)", 22, Color.white);
            marDisplay.fontStyle = FontStyles.Bold;
            marDisplay.alignment = TextAlignmentOptions.Center;
            LayoutElement marDispElem = marDisplay.gameObject.AddComponent<LayoutElement>();
            marDispElem.preferredWidth = 150f;
            Button btnMarPlus = CreateButton("BtnMarPlus", marBox.transform, "+ 10%", 68, 42);

            GameObject marPresets = CreateUIObject("MarPresets", marContainer.transform);
            HorizontalLayoutGroup marPresetLayout = marPresets.AddComponent<HorizontalLayoutGroup>();
            marPresetLayout.childAlignment = TextAnchor.MiddleCenter;
            marPresetLayout.spacing = 6f;
            Button btnMar10 = CreateButton("BtnMar10", marPresets.transform, "10%", 56, 34);
            Button btnMar25 = CreateButton("BtnMar25", marPresets.transform, "25%", 56, 34);
            Button btnMar50 = CreateButton("BtnMar50", marPresets.transform, "50%", 56, 34);
            Button btnMar75 = CreateButton("BtnMar75", marPresets.transform, "75%", 56, 34);
            Button btnMar100 = CreateButton("BtnMar100", marPresets.transform, "100%", 56, 34);

            GameObject sliderGO = CreateUIObject("MarginPercentageSlider", marContainer.transform);
            Slider slider = sliderGO.AddComponent<Slider>();
            sliderGO.SetActive(false);
            marContainer.SetActive(false);

            // 포지션 진입 시 우측 오버레이 패널 및 청산 버튼
            GameObject statusOverlay = CreateUIObject("PositionStatusPanel", controlCard.transform);
            VerticalLayoutGroup statusLayout = statusOverlay.AddComponent<VerticalLayoutGroup>();
            statusLayout.childAlignment = TextAnchor.MiddleCenter;
            statusLayout.spacing = 6f;
            statusLayout.padding = new RectOffset(10, 10, 10, 10);
            TMP_Text posType = CreateTMPText("PosType", statusOverlay.transform, "LONG 10x", 22, Color.green);
            posType.fontStyle = FontStyles.Bold; posType.alignment = TextAlignmentOptions.Center;
            TMP_Text posRoe = CreateTMPText("PosRoe", statusOverlay.transform, "+18.47%", 26, Color.green);
            posRoe.fontStyle = FontStyles.Bold; posRoe.alignment = TextAlignmentOptions.Center;
            TMP_Text posPnl = CreateTMPText("PosPnl", statusOverlay.transform, "+$1,458.36", 16, Color.green);
            posPnl.alignment = TextAlignmentOptions.Center;
            TMP_Text posEntry = CreateTMPText("PosEntry", statusOverlay.transform, "ENTRY: $67,840", 14, Color.white);
            posEntry.alignment = TextAlignmentOptions.Center;
            TMP_Text posTarget = CreateTMPText("PosTarget", statusOverlay.transform, "TARGET: $68,500 (AI 목표가)", 14, new Color(0.024f, 0.714f, 0.831f, 1f));
            posTarget.alignment = TextAlignmentOptions.Center;
            TMP_Text posLiq = CreateTMPText("PosLiq", statusOverlay.transform, "LIQ: $61,200", 14, new Color(0.937f, 0.267f, 0.267f, 1f));
            posLiq.alignment = TextAlignmentOptions.Center;

            Button btnClose = CreateButton("ClosePositionButton", statusOverlay.transform, "🔒 AI 자동 청산 시스템 (Player Locked)", 260, 44, new Color(0.18f, 0.23f, 0.33f, 1f));
            statusOverlay.SetActive(false);

            // TradingPanelUIController 슬롯 연결
            SetField(controller, "longButton", btnLong);
            SetField(controller, "shortButton", btnShort);
            SetField(controller, "closePositionButton", btnClose);
            SetField(controller, "longSubtitleText", longSubText);
            SetField(controller, "shortSubtitleText", shortSubText);
            SetField(controller, "btnTabLeverageMode", btnTabLev);
            SetField(controller, "btnTabMarginRatioMode", btnTabMar);
            SetField(controller, "tabsBarContainer", tabsBar);
            SetField(controller, "leverageControlContainer", levContainer);
            SetField(controller, "marginRatioControlContainer", marContainer);
            SetField(controller, "marginPercentageSlider", slider);
            SetField(controller, "btnMarginRatioMinus", btnMarMinus);
            SetField(controller, "btnMarginRatioPlus", btnMarPlus);
            SetField(controller, "marginRatioDisplayText", marDisplay);
            SetField(controller, "btnPresetRatio10", btnMar10);
            SetField(controller, "btnPresetRatio25", btnMar25);
            SetField(controller, "btnPresetRatio50", btnMar50);
            SetField(controller, "btnPresetRatio75", btnMar75);
            SetField(controller, "btnPresetRatio100", btnMar100);
            SetField(controller, "btnLeverageMinus", btnLevMinus);
            SetField(controller, "btnLeveragePlus", btnLevPlus);
            SetField(controller, "leverageDisplayText", levDisplay);
            SetField(controller, "btnPreset1x", btn1x);
            SetField(controller, "btnPreset5x", btn5x);
            SetField(controller, "btnPreset10x", btn10x);
            SetField(controller, "btnPreset25x", btn25x);
            SetField(controller, "btnPreset50x", btn50x);
            SetField(controller, "btnPreset100x", btn100x);
            SetField(controller, "btnPreset125x", btn125x);
            SetField(controller, "positionStatusPanel", statusOverlay);
            SetField(controller, "positionTypeText", posType);
            SetField(controller, "roeText", posRoe);
            SetField(controller, "pnlText", posPnl);
            SetField(controller, "entryPriceText", posEntry);
            SetField(controller, "targetPriceText", posTarget);
            SetField(controller, "liquidationPriceText", posLiq);
            if (tc != null) SetField(controller, "tradingController", tc);
            if (gm != null) SetField(controller, "gameManager", gm);
        }

        // =========================================================================================
        // [4. 우측 주인공 AI 매매 연동 패널 생성] RightHalfAIContainer (체력/멘탈 바 + 말풍선 + 주인공 이미지 + AI 컨트롤러)
        // =========================================================================================

        private static void CreateRightHalfAIPanel(Transform parent, GameManager gm, FXOverdose.Trading.MarketSimulationEngine mse, FXOverdose.Trading.TradingController tc, TraderStatus ts)
        {
            // 4-1. AITradingBrain 보장 및 바인딩
            FXOverdose.AI.AITradingBrain aiBrain = null;
            if (gm != null)
            {
                aiBrain = gm.gameObject.GetComponent<FXOverdose.AI.AITradingBrain>();
                if (aiBrain == null) aiBrain = gm.gameObject.AddComponent<FXOverdose.AI.AITradingBrain>();
                SetField(aiBrain, "marketEngine", mse);
                SetField(aiBrain, "tradingController", tc);
                SetField(aiBrain, "traderStatus", ts);
                SetField(aiBrain, "gameManager", gm);
                SetField(aiBrain, "defaultLeverage", 10);
                SetField(aiBrain, "tradeMarginRatio", 0.35f);
                EditorUtility.SetDirty(gm.gameObject);
            }

            // [중복 UI 생성 방지] 기존 씬에 HP, Mental, Item 등 HUD 기능 및 UI가 사전 구현되어 있으므로
            // 우측 패널에는 중복된 체력/멘탈 바(AIStatusSummaryCard)와 HUDController를 추가하지 않고 주인공 캐릭터와 말풍선만 연동합니다.

            // 4-3. 말풍선 패널 (DialogueBalloonPanel - 주인공 대사 표시부)
            GameObject balloonGO = CreateUIObject("DialogueBalloonPanel", parent);
            RectTransform balloonRect = balloonGO.GetComponent<RectTransform>();
            balloonRect.anchorMin = new Vector2(0.05f, 0.65f);
            balloonRect.anchorMax = new Vector2(0.95f, 0.85f);
            balloonRect.offsetMin = Vector2.zero;
            balloonRect.offsetMax = Vector2.zero;

            Image balloonImg = balloonGO.AddComponent<Image>();
            Sprite balloonSprite = LoadSpriteAsset("Assets/Img/Generated_image_1-removebg-preview.png");
            if (balloonSprite != null) balloonImg.sprite = balloonSprite;
            else balloonImg.color = new Color(0.08f, 0.12f, 0.22f, 0.95f);
            balloonImg.preserveAspect = false;
            balloonImg.raycastTarget = false; // 말풍선이 하위 UI 클릭을 방해하지 않도록 RaycastTarget 해제
            balloonGO.SetActive(false); // 주인공이 대사를 출력할 때만 표시되도록 기본 숨김

            GameObject textGO = CreateUIObject("DialogueText", balloonGO.transform);
            RectTransform textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(45f, 25f);
            textRect.offsetMax = new Vector2(-45f, -30f);

            TextMeshProUGUI dialogueText = textGO.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset kFont = GetOrCreateKoreanFontAsset();
            if (IsFontAssetValid(kFont)) dialogueText.font = kFont;
            else if (IsFontAssetValid(TMPro.TMP_Settings.defaultFontAsset)) dialogueText.font = TMPro.TMP_Settings.defaultFontAsset;
            dialogueText.enableAutoSizing = true;
            dialogueText.fontSizeMin = 15;
            dialogueText.fontSizeMax = 21;
            dialogueText.overflowMode = TMPro.TextOverflowModes.Overflow;
            dialogueText.color = Color.white;
            dialogueText.fontStyle = FontStyles.Bold;
            dialogueText.alignment = TextAlignmentOptions.Center;
            dialogueText.textWrappingMode = TMPro.TextWrappingModes.Normal;
            dialogueText.raycastTarget = false;
            dialogueText.text = "";

            // 4-4. 주인공 AI 캐릭터 이미지 (ProtagonistCharacterImage)
            GameObject charGO = CreateUIObject("ProtagonistCharacterImage", parent);
            RectTransform charRect = charGO.GetComponent<RectTransform>();
            charRect.anchorMin = new Vector2(0.15f, 0.01f);
            charRect.anchorMax = new Vector2(0.85f, 0.64f);
            charRect.offsetMin = Vector2.zero;
            charRect.offsetMax = Vector2.zero;

            Image charImg = charGO.AddComponent<Image>();
            Sprite charSprite = LoadSpriteAsset("Assets/Img/Generated_image_2-removebg-preview.png");
            if (charSprite != null) charImg.sprite = charSprite;
            charImg.preserveAspect = true;
            charImg.raycastTarget = false; // 주인공 이미지가 우측 하단 아이템 UI 클릭을 막지 않도록 RaycastTarget 해제

            // 4-5. AIVisualController 부착 및 바인딩
            FXOverdose.AI.AIVisualController visualController = parent.gameObject.GetComponent<FXOverdose.AI.AIVisualController>();
            if (visualController == null) visualController = parent.gameObject.AddComponent<FXOverdose.AI.AIVisualController>();
            SetField(visualController, "traderStatus", ts);
            SetField(visualController, "tradingController", tc);
            SetField(visualController, "aiBrain", aiBrain);
            SetField(visualController, "dialogueBalloonPanel", balloonGO);
            SetField(visualController, "dialogueText", dialogueText);
            SetField(visualController, "balloonDisplayDuration", 8.0f);
        }

        private static Slider CreateBiometricSlider(string name, Transform parent, string labelText, Color fillColor)
        {
            GameObject rowGO = CreateUIObject(name, parent);
            HorizontalLayoutGroup hLayout = rowGO.AddComponent<HorizontalLayoutGroup>();
            hLayout.childAlignment = TextAnchor.MiddleLeft;
            hLayout.spacing = 14f;
            LayoutElement rowElem = rowGO.AddComponent<LayoutElement>();
            rowElem.preferredHeight = 22f;

            TMP_Text label = CreateTMPText("Label", rowGO.transform, labelText, 14, Color.white);
            label.fontStyle = FontStyles.Bold;
            LayoutElement lblElem = label.gameObject.AddComponent<LayoutElement>();
            lblElem.preferredWidth = 130f;

            GameObject sliderGO = CreateUIObject("Slider", rowGO.transform);
            LayoutElement sldElem = sliderGO.AddComponent<LayoutElement>();
            sldElem.flexibleWidth = 1f;
            sldElem.preferredHeight = 18f;

            Image bgImg = sliderGO.AddComponent<Image>();
            bgImg.color = new Color(0.18f, 0.23f, 0.33f, 0.6f);

            GameObject fillArea = CreateUIObject("Fill Area", sliderGO.transform);
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero; fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = fillAreaRect.offsetMax = Vector2.zero;

            GameObject fillGO = CreateUIObject("Fill", fillArea.transform);
            RectTransform fillRect = fillGO.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;

            Image fillImg = fillGO.AddComponent<Image>();
            fillImg.color = fillColor;

            Slider slider = sliderGO.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            return slider;
        }

        // =========================================================================================
        // [유틸리티 Helper 메서드]
        // =========================================================================================

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        private static bool IsFontAssetValid(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null) return false;
            try
            {
                if (fontAsset.atlasTextures == null || fontAsset.atlasTextures.Length == 0) return false;
                Texture2D tex = fontAsset.atlasTextures[0];
                if (tex == null) return false;
                string checkName = tex.name;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static TMP_FontAsset cachedKoreanFontAsset = null;
        public static TMP_FontAsset GetOrCreateKoreanFontAsset()
        {
            if (IsFontAssetValid(cachedKoreanFontAsset))
            {
                cachedKoreanFontAsset.atlasPopulationMode = TMPro.AtlasPopulationMode.Dynamic;
                try { cachedKoreanFontAsset.isMultiAtlasTexturesEnabled = true; } catch {}
                return cachedKoreanFontAsset;
            }
            cachedKoreanFontAsset = null;

            string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("Korean") || path.Contains("Malgun") || path.Contains("Dynamic"))
                {
                    TMP_FontAsset loaded = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                    if (IsFontAssetValid(loaded))
                    {
                        cachedKoreanFontAsset = loaded;
                        cachedKoreanFontAsset.atlasPopulationMode = TMPro.AtlasPopulationMode.Dynamic;
                        try { cachedKoreanFontAsset.isMultiAtlasTexturesEnabled = true; } catch {}
                        return cachedKoreanFontAsset;
                    }
                    else if (loaded != null)
                    {
                        // 텍스처 아틀라스가 손상되거나 비어있는 에셋은 재생성을 위해 즉시 제거
                        AssetDatabase.DeleteAsset(path);
                    }
                }
            }

            try
            {
                string fontDir = "Assets/Fonts";
                string ttfPath = $"{fontDir}/malgun.ttf";
                if (!System.IO.File.Exists(ttfPath) && System.IO.File.Exists("C:/Windows/Fonts/malgun.ttf"))
                {
                    if (!AssetDatabase.IsValidFolder(fontDir)) System.IO.Directory.CreateDirectory(fontDir);
                    System.IO.File.Copy("C:/Windows/Fonts/malgun.ttf", ttfPath, true);
                    AssetDatabase.ImportAsset(ttfPath, ImportAssetOptions.ForceUpdate);
                }

                Font ttfFont = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
                if (ttfFont != null)
                {
                    TMP_FontAsset created = TMP_FontAsset.CreateFontAsset(ttfFont);
                    if (IsFontAssetValid(created))
                    {
                        cachedKoreanFontAsset = created;
                        cachedKoreanFontAsset.name = "KoreanDynamicFont_TMP";
                        cachedKoreanFontAsset.atlasPopulationMode = TMPro.AtlasPopulationMode.Dynamic;
                        try { cachedKoreanFontAsset.isMultiAtlasTexturesEnabled = true; } catch {}

                        string dir = "Assets/TextMesh Pro/Resources/Fonts & Materials";
                        if (!AssetDatabase.IsValidFolder("Assets/TextMesh Pro")) System.IO.Directory.CreateDirectory("Assets/TextMesh Pro");
                        if (!AssetDatabase.IsValidFolder("Assets/TextMesh Pro/Resources")) System.IO.Directory.CreateDirectory("Assets/TextMesh Pro/Resources");
                        if (!AssetDatabase.IsValidFolder(dir)) System.IO.Directory.CreateDirectory(dir);

                        string savePath = $"{dir}/KoreanDynamicFont_TMP.asset";
                        AssetDatabase.CreateAsset(cachedKoreanFontAsset, savePath);
                        if (cachedKoreanFontAsset.atlasTextures[0] != null)
                        {
                            cachedKoreanFontAsset.atlasTextures[0].name = "KoreanDynamicFont_TMP Atlas";
                            AssetDatabase.AddObjectToAsset(cachedKoreanFontAsset.atlasTextures[0], cachedKoreanFontAsset);
                        }
                        AssetDatabase.SaveAssets();
                        Debug.Log($"[FX OVERDOSE] 💡 TextMeshPro 한글 폰트 에셋({savePath})을 아틀라스 텍스처와 함께 안전하게 자동 생성 및 저장했습니다.");
                        return cachedKoreanFontAsset;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[FX OVERDOSE] 한글 Dynamic 폰트 에셋 생성 중 예외 발생 (기본 폰트로 대체): {ex.Message}");
            }

            if (!IsFontAssetValid(cachedKoreanFontAsset))
            {
                cachedKoreanFontAsset = TMPro.TMP_Settings.defaultFontAsset;
                if (!IsFontAssetValid(cachedKoreanFontAsset) && guids.Length > 0)
                {
                    foreach (string guid in guids)
                    {
                        TMP_FontAsset anyLoaded = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                        if (IsFontAssetValid(anyLoaded))
                        {
                            cachedKoreanFontAsset = anyLoaded;
                            break;
                        }
                    }
                }
            }

            if (IsFontAssetValid(cachedKoreanFontAsset))
            {
                cachedKoreanFontAsset.atlasPopulationMode = TMPro.AtlasPopulationMode.Dynamic;
                try { cachedKoreanFontAsset.isMultiAtlasTexturesEnabled = true; } catch {}
            }

            return cachedKoreanFontAsset;
        }

        private static Sprite LoadSpriteAsset(string path)
        {
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s != null) return s;

            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (allAssets != null)
            {
                foreach (Object obj in allAssets)
                {
                    if (obj is Sprite subSprite)
                    {
                        return subSprite;
                    }
                }
            }
            return null;
        }

        private static TMP_Text CreateTMPText(string name, Transform parent, string text, int fontSize, Color color)
        {
            GameObject go = CreateUIObject(name, parent);
            TMP_Text tmp = go.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset kFont = GetOrCreateKoreanFontAsset();
            if (IsFontAssetValid(kFont)) tmp.font = kFont;
            else if (IsFontAssetValid(TMPro.TMP_Settings.defaultFontAsset)) tmp.font = TMPro.TMP_Settings.defaultFontAsset;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Left;
            return tmp;
        }

        private static Button CreateButton(string name, Transform parent, string label, float width, float height, Color? bgColor = null)
        {
            GameObject go = CreateUIObject(name, parent);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, height);

            Image img = go.AddComponent<Image>();
            img.color = bgColor ?? new Color(0.122f, 0.161f, 0.235f, 1f);

            Button btn = go.AddComponent<Button>();
            TMP_Text tmp = CreateTMPText("Text", go.transform, label, 16, Color.white);
            tmp.alignment = TextAlignmentOptions.Center;
            RectTransform tmpRect = tmp.GetComponent<RectTransform>();
            tmpRect.anchorMin = Vector2.zero;
            tmpRect.anchorMax = Vector2.one;
            tmpRect.offsetMin = tmpRect.offsetMax = Vector2.zero;

            return btn;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            if (target == null) return;
            var type = target.GetType();
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (field != null)
            {
                field?.SetValue(target, value);
            }
        }

        private static void EnsureCoreEngines(out GameManager gm, out FXOverdose.Trading.MarketSimulationEngine mse, out FXOverdose.Trading.TradingController tc, out TraderStatus ts)
        {
            gm = Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            if (gm == null)
            {
                GameObject coreGO = new GameObject("[FX OVERDOSE Core Engines]");
                gm = coreGO.AddComponent<GameManager>();
                Undo.RegisterCreatedObjectUndo(coreGO, "Create Core Engines");
                Debug.Log("[FX OVERDOSE] 씬 내에 [FX OVERDOSE Core Engines] 오브젝트 및 GameManager를 생성했습니다.");
            }

            GameObject go = gm.gameObject;
            ts = go.GetComponent<TraderStatus>();
            if (ts == null) ts = go.AddComponent<TraderStatus>();

            mse = go.GetComponent<FXOverdose.Trading.MarketSimulationEngine>();
            if (mse == null) mse = go.AddComponent<FXOverdose.Trading.MarketSimulationEngine>();

            tc = go.GetComponent<FXOverdose.Trading.TradingController>();
            if (tc == null) tc = go.AddComponent<FXOverdose.Trading.TradingController>();

            FXOverdose.AI.AITradingBrain aiBrain = go.GetComponent<FXOverdose.AI.AITradingBrain>();
            if (aiBrain == null) aiBrain = go.AddComponent<FXOverdose.AI.AITradingBrain>();

            // 상호 참조 자동 바인딩
            SetField(ts, "gameManager", gm);
            SetField(mse, "gameManager", gm);
            SetField(tc, "gameManager", gm);
            SetField(tc, "marketEngine", mse);
            SetField(tc, "traderStatus", ts);

            SetField(aiBrain, "marketEngine", mse);
            SetField(aiBrain, "tradingController", tc);
            SetField(aiBrain, "traderStatus", ts);
            SetField(aiBrain, "gameManager", gm);

            EditorUtility.SetDirty(go);
        }
    }
}
#endif
