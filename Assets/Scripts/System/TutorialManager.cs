using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;
using FXOverdose.AI;
using FXOverdose.Trading;
using FXOverdose.Core;

namespace FXOverdose.Core
{
    public enum TutorialState
    {
        Welcome,
        WaitToOpenPosition,
        WaitToClosePosition,
        AITradingDemo,
        LeverageAndMargin,
        MentalExplanation,
        ShopExplanation,
        LevelSystem,
        SuddenEventDemo,
        DailySettlement,
        Graduation
    }

    public class TutorialManager : MonoBehaviour
    {
        private const int TutorialOverlaySortingOrder = 32000;
        private const int InteractiveControlSortingOrder = 32001;

        public static TutorialManager Instance { get; private set; }

        [Header("상태")]
        public TutorialState CurrentState = TutorialState.Welcome;
        
        public bool AllowAITrading { get; set; } = false;

        [Header("UI 컨트롤 차단 설정")]
        [SerializeField] private GraphicRaycaster canvasRaycaster; // 메인 캔버스 광범위 차단 시 사용 (필요에 따라)
        [SerializeField] private CanvasGroup fullScreenBlocker; // 투명한 전체화면 패널 (모든 클릭 차단용)

        // 각 단계별 허용할 특정 UI 요소들 (Inspector 할당 필요)
        [Header("허용 대상 UI (Inspector 연결)")]
        [SerializeField] private Button btnLong;
        [SerializeField] private Button btnShort;
        [SerializeField] private Button btnClose;
        [SerializeField] private Button btnIncreaseLeverage;
        [SerializeField] private Button btnDecreaseLeverage;
        [SerializeField] private Button btnShop;
        [SerializeField] private Button btnEndTutorial;

        [Header("오브젝트 하이라이트")]
        [SerializeField] private GameObject chartHighlight;
        [SerializeField] private GameObject balanceHighlight;
        [SerializeField] private GameObject marginHighlight;
        [SerializeField] private GameObject mentalHighlight;
        [SerializeField] private GameObject levelHighlight;
        private GameObject leverageHighlight;
        private GameObject shopHighlight;
        private GameObject endTutorialPanel;

        private Canvas highlightCanvas;
        private RectTransform highlightRoot;
        private readonly Dictionary<GameObject, RectTransform> highlightTargets = new();

        [Header("시스템 참조")]
        private TradingController tradingController;
        private AIVisualController aiVisualController;
        private MarketSimulationEngine marketEngine;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private IEnumerator Start()
        {
            // 의존성 찾기
            tradingController = FindAnyObjectByType<TradingController>();
            aiVisualController = FindAnyObjectByType<AIVisualController>();
            marketEngine = FindAnyObjectByType<MarketSimulationEngine>();

            // 동적 UI 바인딩
            var panelUI = FindAnyObjectByType<FXOverdose.UI.Chart.TradingPanelUIController>();
            if (panelUI != null)
            {
                btnLong = panelUI.LongButton;
                btnShort = panelUI.ShortButton;
                btnClose = panelUI.ClosePositionButton;
                btnIncreaseLeverage = panelUI.BtnLeveragePlus;
                btnDecreaseLeverage = panelUI.BtnLeverageMinus;
            }

            var shopFollower = FindAnyObjectByType<ShopButtonInventoryFollower>();
            if (shopFollower != null)
            {
                btnShop = shopFollower.GetComponent<Button>();
            }

            // 돌발 이벤트 랜덤 발생 차단
            var choiceCtrl = FindAnyObjectByType<FXOverdose.Events.ChoiceEventController>();
            if (choiceCtrl != null)
            {
                choiceCtrl.IsTutorialMode = true;
            }

            // 기존 요미의 일반 대사(트레이딩, 기믹 등) 차단
            if (aiVisualController != null)
            {
                aiVisualController.SuppressNormalDialogues = true;
            }

            // 동적 튜토리얼 블로커 캔버스 생성
            GameObject blockerGo = new GameObject(
                "TutorialBlockerCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(GraphicRaycaster));
            highlightCanvas = blockerGo.GetComponent<Canvas>();
            highlightCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            highlightCanvas.sortingOrder = TutorialOverlaySortingOrder; // 다른 동적 HUD보다 항상 위
            highlightRoot = blockerGo.GetComponent<RectTransform>();
            
            GameObject bgGo = new GameObject("BlockerPanel");
            bgGo.transform.SetParent(blockerGo.transform, false);
            Image bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0, 0, 0, 0f); // 투명 차단
            RectTransform bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            
            fullScreenBlocker = bgGo.AddComponent<CanvasGroup>();
            fullScreenBlocker.blocksRaycasts = true;
            fullScreenBlocker.alpha = 0f;

            // 유니티 UI LayoutGroup 사이즈 강제 계산
            yield return new WaitForEndOfFrame();
            Canvas.ForceUpdateCanvases();

            // 동적 HUD의 Awake/Start 생성 순서가 튜토리얼보다 늦어도 모든 대상을 찾을 수 있도록
            // 한 프레임짜리 단발 바인딩 대신 여러 프레임 동안 재시도합니다.
            yield return StartCoroutine(BindHighlightsWhenReady());

            SetButtonsInteractable(false);
            DisableAllHighlights();

            // 튜토리얼 진입 시 장 개장 (GameManager 상태 변경 및 차트 개시)
            var gm = FindAnyObjectByType<GameManager>();
            if (gm != null)
            {
                gm.FinishLoadingAndStartPlaying();
            }
            var market = FindAnyObjectByType<FXOverdose.Trading.MarketSimulationEngine>();
            if (market != null)
            {
                market.OpenMarketAfterLoading();
            }

            // 1단계: 환영 및 UI 소개 (게임 시작 대기)
            yield return new WaitForSeconds(1.0f); // 씬 진입 후 살짝 대기
            yield return StartCoroutine(Step1_Welcome());

            // 2단계: 수동 매매 진입
            yield return StartCoroutine(Step2_ManualTrading());

            // 3단계: 포지션 청산
            yield return StartCoroutine(Step3_ClosePosition());

            // 4단계: 요미 자동매매 시연
            yield return StartCoroutine(Step4_AITradingDemo());
            yield return StartCoroutine(Step4_5_AIBoast());

            // 5단계: 레버리지와 증거금
            yield return StartCoroutine(Step5_LeverageMargin());

            // 6단계: 멘탈 시스템
            yield return StartCoroutine(Step6_Mental());

            // 7단계: 상점 시스템
            yield return StartCoroutine(Step7_Shop());

            // 8단계: 레벨 시스템
            yield return StartCoroutine(Step8_LevelSystem());

            // 9단계: 돌발 이벤트
            yield return StartCoroutine(Step9_SuddenEvent());

            // 10단계: 정산 및 졸업
            yield return StartCoroutine(Step10_Graduation());
        }

        private void SetButtonsInteractable(bool interactableState, Button specificBtn = null)
        {
            // 초기화
            if (btnLong != null) ResetToNormal(btnLong);
            if (btnShort != null) ResetToNormal(btnShort);
            if (btnClose != null) ResetToNormal(btnClose);
            if (btnIncreaseLeverage != null) ResetToNormal(btnIncreaseLeverage);
            if (btnDecreaseLeverage != null) ResetToNormal(btnDecreaseLeverage);
            if (btnShop != null) ResetToNormal(btnShop);
            if (btnEndTutorial != null) ResetToNormal(btnEndTutorial);

            // 차단 또는 허용
            if (fullScreenBlocker != null) 
            {
                fullScreenBlocker.blocksRaycasts = !interactableState;
            }

            if (specificBtn != null)
            {
                BringToFront(specificBtn);
            }
        }

        private void BringToFront(Button btn)
        {
            if (btn == null) return;
            var canvas = btn.gameObject.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = btn.gameObject.AddComponent<Canvas>();
                btn.gameObject.AddComponent<GraphicRaycaster>();
            }
            canvas.overrideSorting = true;
            canvas.sortingOrder = InteractiveControlSortingOrder;
        }

        private void ResetToNormal(Button btn)
        {
            if (btn == null) return;
            var canvas = btn.gameObject.GetComponent<Canvas>();
            if (canvas != null &&
                canvas.overrideSorting &&
                canvas.sortingOrder == InteractiveControlSortingOrder)
            {
                Destroy(btn.gameObject.GetComponent<GraphicRaycaster>());
                Destroy(canvas);
            }
        }

        private void AutoBindHighlights()
        {
            // 1. Chart
            if (chartHighlight == null)
            {
                var chart = FindAnyObjectByType<FXOverdose.UI.Chart.ChartUIController>();
                if (chart != null) chartHighlight = CreateHighlightOverlay(chart.transform);
            }

            // 2. Balance
            if (balanceHighlight == null)
            {
                var topBar = FindAnyObjectByType<FXOverdose.UI.TopBar.TopStatusBarUIController>();
                if (topBar != null && topBar.TutorialBalanceHighlightTarget != null)
                {
                    balanceHighlight = CreateHighlightOverlay(topBar.TutorialBalanceHighlightTarget);
                }
            }

            // 3-1. Leverage
            if (leverageHighlight == null)
            {
                var panelUI = FindAnyObjectByType<FXOverdose.UI.Chart.TradingPanelUIController>();
                if (panelUI != null && panelUI.TutorialLeverageHighlightTarget != null)
                    leverageHighlight = CreateHighlightOverlay(panelUI.TutorialLeverageHighlightTarget);
            }

            // 3. Margin
            if (marginHighlight == null)
            {
                var panelUI = FindAnyObjectByType<FXOverdose.UI.Chart.TradingPanelUIController>();
                if (panelUI != null && panelUI.TutorialMarginHighlightTarget != null)
                    marginHighlight = CreateHighlightOverlay(panelUI.TutorialMarginHighlightTarget);
            }

            // 4. Mental
            if (mentalHighlight == null)
            {
                var vitals = FindAnyObjectByType<VitalsValueUI>();
                if (vitals != null && vitals.TutorialMentalHighlightTarget != null)
                {
                    mentalHighlight = CreateHighlightOverlay(vitals.TutorialMentalHighlightTarget);
                }
            }

            // 5. Shop
            if (shopHighlight == null && btnShop != null)
            {
                shopHighlight = CreateHighlightOverlay(btnShop.transform);
            }

            // 6. Level / EXP
            if (levelHighlight == null)
            {
                var levelUI = FindAnyObjectByType<TraderLevelUIController>();
                RectTransform levelHud = levelUI != null
                    ? levelUI.TutorialLevelHighlightTarget
                    : GameObject.Find("CharacterLevelExpHUD")?.GetComponent<RectTransform>();
                if (levelHud != null) levelHighlight = CreateHighlightOverlay(levelHud);
            }
        }



        private GameObject CreateHighlightOverlay(Transform target)
        {
            if (target == null || highlightRoot == null) return null;
            RectTransform targetRect = target as RectTransform ?? target.GetComponent<RectTransform>();
            if (targetRect == null) return null;

            GameObject overlay = new GameObject(
                $"TutorialHighlight_{target.name}",
                typeof(RectTransform),
                typeof(CanvasGroup));
            overlay.transform.SetParent(highlightRoot, false);
            overlay.transform.SetAsLastSibling();
            
            RectTransform rt = overlay.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            
            var canvasGroup = overlay.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            
            Color borderColor = new Color(1f, 0.85f, 0.1f, 1f); // 선명한 노란색
            float thickness = 4f;

            CreateBorderLine(overlay.transform, "Top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -thickness), Vector2.zero, borderColor);
            CreateBorderLine(overlay.transform, "Bottom", new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, thickness), borderColor);
            CreateBorderLine(overlay.transform, "Left", new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(thickness, 0), borderColor);
            CreateBorderLine(overlay.transform, "Right", new Vector2(1, 0), new Vector2(1, 1), new Vector2(-thickness, 0), Vector2.zero, borderColor);
            
            highlightTargets[overlay] = targetRect;
            UpdateHighlightBounds(overlay, targetRect);
            overlay.SetActive(false);
            return overlay;
        }

        private void LateUpdate()
        {
            // 머지나 동적 UI 생성 순서 변화로 초기 바인딩을 놓쳐도 실행 중 자동 복구합니다.
            if (Time.frameCount % 30 == 0 && !AreAllHighlightsBound())
            {
                Canvas.ForceUpdateCanvases();
                AutoBindHighlights();
            }

            foreach (KeyValuePair<GameObject, RectTransform> pair in highlightTargets)
            {
                if (pair.Key == null || !pair.Key.activeSelf || pair.Value == null) continue;
                UpdateHighlightBounds(pair.Key, pair.Value);
            }
        }

        private void UpdateHighlightBounds(GameObject overlay, RectTransform target)
        {
            if (overlay == null || target == null || highlightRoot == null) return;

            Canvas targetCanvas = target.GetComponentInParent<Canvas>();
            Camera targetCamera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? targetCanvas.worldCamera
                : null;

            Vector3[] worldCorners = new Vector3[4];
            target.GetWorldCorners(worldCorners);
            Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new(float.NegativeInfinity, float.NegativeInfinity);

            foreach (Vector3 worldCorner in worldCorners)
            {
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(targetCamera, worldCorner);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        highlightRoot,
                        screenPoint,
                        null,
                        out Vector2 localPoint))
                {
                    continue;
                }

                min = Vector2.Min(min, localPoint);
                max = Vector2.Max(max, localPoint);
            }

            if (float.IsInfinity(min.x) || float.IsInfinity(min.y)) return;

            const float padding = 4f;
            RectTransform overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchoredPosition = (min + max) * 0.5f;
            overlayRect.sizeDelta = max - min + Vector2.one * padding * 2f;
        }

        private void CreateBorderLine(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            GameObject line = new GameObject(name);
            line.transform.SetParent(parent, false);
            RectTransform rt = line.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            Image img = line.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        private void DisableAllHighlights()
        {
            if (chartHighlight != null) chartHighlight.SetActive(false);
            if (balanceHighlight != null) balanceHighlight.SetActive(false);
            if (marginHighlight != null) marginHighlight.SetActive(false);
            if (leverageHighlight != null) leverageHighlight.SetActive(false);
            if (mentalHighlight != null) mentalHighlight.SetActive(false);
            if (shopHighlight != null) shopHighlight.SetActive(false);
            if (levelHighlight != null) levelHighlight.SetActive(false);
        }

        private Coroutine pulseCoroutine;

        private void SetHighlight(GameObject highlightObj, bool state)
        {
            if (highlightObj == null) return;
            highlightObj.SetActive(state);
            
            if (state)
            {
                if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
                pulseCoroutine = StartCoroutine(PulseHighlightEffect(highlightObj));
            }
            else
            {
                if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
                // Reset alpha
                var canvasGroup = highlightObj.GetComponent<CanvasGroup>();
                if (canvasGroup != null) canvasGroup.alpha = 1f;
                else 
                {
                    var img = highlightObj.GetComponent<Image>();
                    if (img != null) {
                        var c = img.color;
                        c.a = 1f;
                        img.color = c;
                    }
                }
            }
        }

        private IEnumerator PulseHighlightEffect(GameObject target)
        {
            var canvasGroup = target.GetComponent<CanvasGroup>();
            Image img = null;
            if (canvasGroup == null)
            {
                img = target.GetComponent<Image>();
                if (img == null) yield break;
            }

            float speed = 5f;
            while (true)
            {
                float alpha = (Mathf.Sin(Time.time * speed) + 1f) / 2f * 0.7f + 0.3f; // 0.3 ~ 1.0
                
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = alpha;
                }
                else if (img != null)
                {
                    var c = img.color;
                    c.a = alpha;
                    img.color = c;
                }
                yield return null;
            }
        }

        private IEnumerator PlayDialogueAndWait(string text, DialoguePriority priority = DialoguePriority.Critical)
        {
            if (aiVisualController != null)
            {
                aiVisualController.DisplayDialogueBalloon(text, priority, FXOverdose.AI.EventCategory.Tutorial);
                
                // 최소 대기 시간 (실수 연타 방지)
                yield return new WaitForSeconds(0.5f);
                
                // 플레이어가 클릭(터치)할 때까지 무한 대기
                while (true)
                {
                    bool clicked = false;
#if ENABLE_INPUT_SYSTEM
                    if (UnityEngine.InputSystem.Pointer.current != null && UnityEngine.InputSystem.Pointer.current.press.wasPressedThisFrame)
                    {
                        clicked = true;
                    }
#else
                    if (Input.GetMouseButtonDown(0)) clicked = true;
#endif
                    if (clicked) break;
                    
                    yield return null;
                }
                
                aiVisualController.SetTutorialAdvanceIndicator(false);

                // 클릭 직후 약간의 유예 시간
                yield return new WaitForSeconds(0.1f);
            }
            else
            {
                yield return new WaitForSeconds(3.0f);
            }
        }

        private IEnumerator Step1_Welcome()
        {
            CurrentState = TutorialState.Welcome;
            
            yield return StartCoroutine(PlayDialogueAndWait("안녕? 난 오빠의 트레이딩 파트너 요미야."));
            
            if (chartHighlight != null) SetHighlight(chartHighlight, true);
            yield return StartCoroutine(PlayDialogueAndWait("여긴 오빠가 돈을 벌(혹은 날릴) 차트고,"));
            if (chartHighlight != null) SetHighlight(chartHighlight, false);

            if (balanceHighlight != null) SetHighlight(balanceHighlight, true);
            yield return StartCoroutine(PlayDialogueAndWait("위쪽이 오빠의 잔고야."));
            if (balanceHighlight != null) SetHighlight(balanceHighlight, false);

            yield return StartCoroutine(PlayDialogueAndWait("자, 이제 트레이딩의 기본부터 알려줄게."));
        }

        private FXOverdose.Trading.TradingController.PositionType playerTutorialPosition;

        private IEnumerator Step2_ManualTrading()
        {
            CurrentState = TutorialState.WaitToOpenPosition;
            
            // 수동 매매로 전환
            if (tradingController != null)
            {
                tradingController.UnlockManualMode(); // 락 해제 후
                tradingController.SetTradingMode(TradingController.TradingMode.Player_Manual);
                tradingController.LockManualMode();   // 다시 강제 락 (요미 외 전환 불가)
            }

            yield return StartCoroutine(PlayDialogueAndWait("일단 오빠의 실력 좀 볼까? 수동 매매 모드로 바꿨으니까, 차트를 보고 상승(Long)이든 하락(Short)이든 버튼을 눌러서 포지션을 잡아봐!"));

            // 롱/숏 버튼만 앞으로 가져오고 활성화
            SetButtonsInteractable(false);
            BringToFront(btnLong);
            BringToFront(btnShort);

            // 포지션 잡을 때까지 무한 대기
            while (tradingController != null && tradingController.CurrentPosition == TradingController.PositionType.None)
            {
                yield return null;
            }

            if (tradingController != null)
            {
                playerTutorialPosition = tradingController.CurrentPosition;
            }

            // 진입 성공 시 다시 전역 차단
            SetButtonsInteractable(false);
        }

        private IEnumerator Step3_ClosePosition()
        {
            CurrentState = TutorialState.WaitToClosePosition;
            
            yield return new WaitForSeconds(2.0f); // 가격 변동 대기
            yield return StartCoroutine(PlayDialogueAndWait("좋아, 포지션이 잡혔어! 손익(ROE)이 움직이는 거 보이지? 적당할 때 '포지션 매도' 버튼을 눌러서 수익을 확정(또는 손절)해봐."));

            SetButtonsInteractable(false);
            BringToFront(btnClose);

            // 포지션 청산할 때까지 무한 대기
            while (tradingController != null && tradingController.CurrentPosition != TradingController.PositionType.None)
            {
                yield return null;
            }

            SetButtonsInteractable(false);
        }

        private IEnumerator Step4_AITradingDemo()
        {
            CurrentState = TutorialState.AITradingDemo;
            AllowAITrading = true; // 이 단계에서만 요미 매매 허용
            
            yield return StartCoroutine(PlayDialogueAndWait("음, 나쁘지 않네. 하지만 진정한 수익은 내 완벽한 알고리즘에서 나오지! 이제는 요미가 직접 매매 해볼게. 요미가 어떻게 타점을 잡는지 잘 봐."));

            if (tradingController != null)
            {
                tradingController.UnlockManualMode();
                tradingController.SetTradingMode(TradingController.TradingMode.AI_Auto);
                tradingController.LockManualMode();
            }

            // 확정적 주가 제어 트리거 (MarketEngine 등에 구현 필요)
            // 요미가 무조건 포지션을 잡고 수익을 내도록 MarketEngine의 빔 이벤트를 고정
            if (marketEngine != null)
            {
                // 플레이어의 선택과 무관하게 차트에 상승/하락 랜덤 신호를 발생시킵니다.
                var randomSignal = UnityEngine.Random.value > 0.5f 
                    ? FXOverdose.Trading.MarketSignalType.BullishBreakout 
                    : FXOverdose.Trading.MarketSignalType.BearishBreakout;
                
                marketEngine.TriggerGuaranteedProfitEvent(randomSignal);
            }

            // AI가 포지션을 잡을 때까지 대기
            while (tradingController != null && tradingController.CurrentPosition == TradingController.PositionType.None)
            {
                yield return null;
            }

            // 수익이 나서 스스로 청산할 때까지 대기
            while (tradingController != null && tradingController.CurrentPosition != TradingController.PositionType.None)
            {
                yield return null;
            }
            
            AllowAITrading = false; // 매매 시연 종료 후 다시 차단
        }

        private IEnumerator Step4_5_AIBoast()
        {
            yield return StartCoroutine(PlayDialogueAndWait("봐봐! 요미의 완벽한 예측으로 이렇게 쉽게 돈을 벌 수 있다니까? 오빠는 요미만 믿고 맡기기만 해도 된다구!"));
            yield return new WaitForSeconds(2.0f);
        }

        private IEnumerator Step5_LeverageMargin()
        {
            CurrentState = TutorialState.LeverageAndMargin;

            var panelUI = FindAnyObjectByType<FXOverdose.UI.Chart.TradingPanelUIController>();
            panelUI?.ShowMarginControlsForTutorial();
            yield return null;
            if (marginHighlight != null) SetHighlight(marginHighlight, true);

            yield return StartCoroutine(PlayDialogueAndWait("트레이딩의 꽃은 역시 레버리지지! 적은 돈으로 큰 돈을 굴릴 수 있게 해줘."));
            yield return StartCoroutine(PlayDialogueAndWait("여기 이 증거금(Margin)이 오빠가 한 번의 거래에 실제로 투입하는 판돈이야. 시드의 몇 퍼센트를 투입할지 신중하게 결정해."));
            
            if (marginHighlight != null) SetHighlight(marginHighlight, false);

            panelUI?.ShowLeverageControlsForTutorial();
            yield return null;
            if (leverageHighlight != null) SetHighlight(leverageHighlight, true);
            yield return StartCoroutine(PlayDialogueAndWait("레버리지를 높이면 증거금 대비 수익도 배가 되지만, 조금만 빗나가도 증거금을 순식간에 다 날려버리니까(청산) 조심해야 해!"));
            
            // 레버리지 조작 버튼 하이라이트
            SetButtonsInteractable(false);
            BringToFront(btnIncreaseLeverage);
            BringToFront(btnDecreaseLeverage);
            
            yield return new WaitForSeconds(2.0f);
            if (leverageHighlight != null) SetHighlight(leverageHighlight, false);
            SetButtonsInteractable(false);
        }

        private IEnumerator Step6_Mental()
        {
            CurrentState = TutorialState.MentalExplanation;
            
            yield return StartCoroutine(PlayDialogueAndWait("아, 중요한 걸 잊을 뻔했네."));
            
            if (mentalHighlight != null) SetHighlight(mentalHighlight, true);
            yield return StartCoroutine(PlayDialogueAndWait("오른쪽 위에 멘탈 게이지 보여?"));
            yield return StartCoroutine(PlayDialogueAndWait("포지션 때문에 스트레스를 너무 받으면 오빠가 요미를 통제할 수 없을지도 몰라!"));
            yield return StartCoroutine(PlayDialogueAndWait("낮아진 체력과 멘탈은 아이템을 사용하여 회복할 수 있어."));
            if (mentalHighlight != null) SetHighlight(mentalHighlight, false);
        }

        private IEnumerator Step7_Shop()
        {
            CurrentState = TutorialState.ShopExplanation;

            if (shopHighlight == null && btnShop != null)
                shopHighlight = CreateHighlightOverlay(btnShop.transform);
            if (shopHighlight != null) SetHighlight(shopHighlight, true);

            yield return StartCoroutine(PlayDialogueAndWait("여기는 요미가 평소에 밥을 먹거나 여러 가지 서포트 아이템을 사는 상점이야."));
            yield return StartCoroutine(PlayDialogueAndWait("매매에 도움을 주는 유용한 아이템들이나, 요미의 밥과 음료수들을 살 수 있어."));
            yield return StartCoroutine(PlayDialogueAndWait("오빠가 번 돈은 요미를 위해 아낌없이 쓰라구!"));
            
            SetButtonsInteractable(false);
            BringToFront(btnShop);
            yield return new WaitForSeconds(2.0f);
            if (shopHighlight != null) SetHighlight(shopHighlight, false);
            SetButtonsInteractable(false);
        }

        private IEnumerator Step8_LevelSystem()
        {
            CurrentState = TutorialState.LevelSystem;

            if (levelHighlight == null)
            {
                var levelUI = FindAnyObjectByType<TraderLevelUIController>();
                RectTransform levelHud = levelUI != null
                    ? levelUI.TutorialLevelHighlightTarget
                    : GameObject.Find("CharacterLevelExpHUD")?.GetComponent<RectTransform>();
                if (levelHud != null) levelHighlight = CreateHighlightOverlay(levelHud);
            }
            if (levelHighlight != null) SetHighlight(levelHighlight, true);
            yield return StartCoroutine(PlayDialogueAndWait("오빠가 성공적으로 매매를 이어갈수록 레벨이 오를 거야!"));
            yield return StartCoroutine(PlayDialogueAndWait("레벨이 오르면 요미의 차트 분석력이나 멘탈, 인내력 같은 스킬들을 직접 업그레이드할 수 있어."));
            yield return StartCoroutine(PlayDialogueAndWait("투자를 통해 요미를 최고의 파트너로 키워줘!"));
            if (levelHighlight != null) SetHighlight(levelHighlight, false);
        }

        private IEnumerator Step9_SuddenEvent()
        {
            CurrentState = TutorialState.SuddenEventDemo;
            
            var playWait = StartCoroutine(PlayDialogueAndWait("앗! 방금 중대한 뉴스가 떴어!\n시장에는 예상치 못한 돌발 이벤트가 발생하기도 해.\n어떻게 대처할지 오빠의 선택에 따라 시장이 요동칠 테니까 신중하게 결정해!"));
            
            // 대사 출력 및 클릭(넘김) 완료 대기
            yield return playWait;

            // 돌발 이벤트 팝업 띄우기 (미리 예열된 LLM 데이터 사용)
            var choiceController = FindAnyObjectByType<FXOverdose.Events.ChoiceEventController>();
            if (choiceController != null)
            {
                // 이벤트 팝업을 클릭할 수 있도록 전체 화면 클릭 방지 임시 해제
                if (fullScreenBlocker != null) fullScreenBlocker.blocksRaycasts = false;
                
                // 튜토리얼에서는 LLM 생성 상태와 무관하게 내용이 완성된 고정 이벤트를 사용합니다.
                choiceController.TriggerSpecificEvent("EVENT_01_FSC_ETF");
                
                // 이벤트가 활성화되어 있는 동안 대기 (팝업 떠있는 상태)
                while (choiceController.IsEventActive)
                {
                    yield return null;
                }
                
                // 이벤트 팝업 종료 후 다시 클릭 방지
                if (fullScreenBlocker != null) fullScreenBlocker.blocksRaycasts = true;

                // [NEW] 선택 완료 후 요미의 자연스러운 확인 및 시간 가속 처리
                yield return StartCoroutine(PlayDialogueAndWait("어때? 돌발 이벤트에 어떻게 대처해야 할지 감이 좀 와?"));
                yield return StartCoroutine(PlayDialogueAndWait("오빠의 선택이 시장에 어떤 결과를 가져오는지 빠르게 시간을 돌려볼게!"));
                
                var gameManager = FindAnyObjectByType<GameManager>();
                if (gameManager != null)
                {
                    // 이벤트 지속시간 150분 고속 경과
                    gameManager.AdvanceGameMinutes(150);
                    
                    // 빨리 감기가 끝날 때까지 대기
                    while (gameManager.IsFastForwardingTime)
                    {
                        yield return null;
                    }
                }
                
                yield return StartCoroutine(PlayDialogueAndWait("결과 확인 완료! 돌발 이벤트 대응도 완벽하네!"));
            }
            else
            {
                yield return new WaitForSeconds(3.0f); // Fallback
            }
        }

        private IEnumerator Step10_Graduation()
        {
            CurrentState = TutorialState.Graduation;

            yield return StartCoroutine(PlayDialogueAndWait("게임 시간은 계속 흘러서 24:00이 되면 하루가 끝나고 그 날의 모든 포지션이 강제 정산돼. 그 전에 깔끔하게 포지션을 정리하는 게 좋아. 자, 이제 진짜 실전으로 가볼까?"));

            // 튜토리얼 종료 버튼 활성화
            SetButtonsInteractable(false);
            
            if (btnEndTutorial == null)
            {
                EnsureEndTutorialButton();
            }

            if (endTutorialPanel != null) endTutorialPanel.SetActive(true);
            BringToFront(btnEndTutorial);
            if (btnEndTutorial != null) 
            {
                btnEndTutorial.interactable = true;
                btnEndTutorial.gameObject.SetActive(true);
                btnEndTutorial.onClick.RemoveAllListeners();
                btnEndTutorial.onClick.AddListener(EndTutorial);
            }
        }

        private void EnsureEndTutorialButton()
        {
            if (fullScreenBlocker == null) return;
            
            Transform root = fullScreenBlocker.transform.parent;
            endTutorialPanel = new GameObject(
                "TutorialCompletePanel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline));
            endTutorialPanel.transform.SetParent(root, false);

            RectTransform panelRect = endTutorialPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.35f, 0.34f);
            panelRect.anchorMax = new Vector2(0.65f, 0.64f);
            panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;

            Image panelImage = endTutorialPanel.GetComponent<Image>();
            panelImage.color = new Color32(9, 16, 31, 248);
            panelImage.raycastTarget = true;
            Outline panelOutline = endTutorialPanel.GetComponent<Outline>();
            panelOutline.effectColor = new Color32(6, 182, 212, 255);
            panelOutline.effectDistance = new Vector2(3f, -3f);

            CreateTutorialPanelImage(
                endTutorialPanel.transform,
                "TopAccent",
                new Vector2(0f, 0.965f),
                Vector2.one,
                new Color32(34, 211, 238, 255));

            TMP_Text badge = CreateTutorialPanelText(
                endTutorialPanel.transform,
                "CompleteBadge",
                "TUTORIAL  COMPLETE",
                14f,
                new Vector2(0.08f, 0.75f),
                new Vector2(0.92f, 0.91f),
                new Color32(103, 232, 249, 255));
            badge.fontStyle = FontStyles.Bold;
            badge.characterSpacing = 3f;

            TMP_Text title = CreateTutorialPanelText(
                endTutorialPanel.transform,
                "CompleteTitle",
                "READY FOR THE MARKET?",
                25f,
                new Vector2(0.07f, 0.52f),
                new Vector2(0.93f, 0.75f),
                Color.white);
            title.fontStyle = FontStyles.Bold;

            CreateTutorialPanelText(
                endTutorialPanel.transform,
                "CompleteSubtitle",
                "튜토리얼을 마쳤어. 이제 실전 트레이딩을 시작해!",
                15f,
                new Vector2(0.08f, 0.39f),
                new Vector2(0.92f, 0.54f),
                new Color32(148, 163, 184, 255));

            GameObject btnGo = new(
                "Btn_EndTutorial",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(Outline));
            btnGo.transform.SetParent(endTutorialPanel.transform, false);

            RectTransform buttonRect = btnGo.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.09f, 0.10f);
            buttonRect.anchorMax = new Vector2(0.91f, 0.34f);
            buttonRect.offsetMin = buttonRect.offsetMax = Vector2.zero;

            Image buttonImage = btnGo.GetComponent<Image>();
            buttonImage.color = new Color32(8, 126, 151, 255);
            Outline buttonOutline = btnGo.GetComponent<Outline>();
            buttonOutline.effectColor = new Color32(103, 232, 249, 255);
            buttonOutline.effectDistance = UIStrokeStyle.EffectDistance;

            btnEndTutorial = btnGo.GetComponent<Button>();
            btnEndTutorial.targetGraphic = buttonImage;
            ColorBlock colors = btnEndTutorial.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color32(207, 250, 254, 255);
            colors.pressedColor = new Color32(103, 232, 249, 255);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color32(71, 85, 105, 180);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            btnEndTutorial.colors = colors;

            TMP_Text buttonLabel = CreateTutorialPanelText(
                btnGo.transform,
                "Label",
                "START TRADING   >",
                19f,
                Vector2.zero,
                Vector2.one,
                Color.white);
            buttonLabel.fontStyle = FontStyles.Bold;
            buttonLabel.characterSpacing = 1.5f;

            endTutorialPanel.SetActive(false);
        }

        private static Image CreateTutorialPanelImage(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateTutorialPanelText(
            Transform parent,
            string name,
            string value,
            float fontSize,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            TMP_Text text = go.GetComponent<TMP_Text>();
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Max(10f, fontSize - 6f);
            text.fontSizeMax = fontSize;
            text.raycastTarget = false;
            GlobalPFStardustFont.ConfigureCompactHudText(text, null, fontSize);
            return text;
        }

        public void EndTutorial()
        {
            Debug.Log("[TutorialManager] 튜토리얼 종료 버튼 클릭 -> LoadingScene -> GameScene 이동");
            if (btnEndTutorial != null) btnEndTutorial.interactable = false;
            
            FXOverdose.UI.LoadingScreenController.TargetSceneToLoad = "GameScene";
            
            if (Application.CanStreamedLevelBeLoaded("LoadingScene"))
            {
                SceneManager.LoadScene("LoadingScene");
            }
            else
            {
                SceneManager.LoadScene("GameScene");
            }
        }
    }
}
