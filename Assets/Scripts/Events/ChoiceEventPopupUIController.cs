using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FXOverdose.Events
{
    public class ChoiceEventPopupUIController : MonoBehaviour
    {
        private const int EventPopupSortingOrder = 150;

        [Header("UI 연결 슬롯 (미연결 시 런타임 자동 생성)")]
        [SerializeField] private GameObject popupPanel;
        [SerializeField] private TMP_Text scenarioTitleText;
        [SerializeField] private TMP_Text scenarioDescText;
        [SerializeField] private TMP_Text aiMonologueText;
        [SerializeField] private Button[] optionButtons = new Button[3];
        [SerializeField] private TMP_Text[] optionTexts = new TMP_Text[3];
        [SerializeField] private TMP_Text toastText;

        [Header("디자인 테마 색상")]
        private readonly Color colorSafe = new Color(0.133f, 0.773f, 0.369f, 1f);       // #22C55E (A: 안전)
        private readonly Color colorAggressive = new Color(0.937f, 0.267f, 0.267f, 1f); // #EF4444 (B: 공격)
        private readonly Color colorSpecial = new Color(0.918f, 0.702f, 0.031f, 1f);    // #EAB308 (C: 특수/직접)
        private readonly Color colorBG = new Color(0.059f, 0.090f, 0.165f, 0.95f);      // #0F172A

        private Action<int> currentCallback;
        private ChoiceEventSO currentEvent;
        private ScrollRect scrollRect;

        private void Awake()
        {
            EnsureUIBuilt();
            EnsureOverlayPriority();
            if (popupPanel != null)
            {
                popupPanel.SetActive(false);
            }
        }

        public void Show(ChoiceEventSO eventData, Action<int> onOptionSelected)
        {
            if (eventData == null) return;

            EnsureUIBuilt();
            EnsureOverlayPriority();

            currentEvent = eventData;
            currentCallback = onOptionSelected;

            if (scenarioTitleText != null) scenarioTitleText.text = $"[BREAKING NEWS] {eventData.ScenarioTitle}";
            if (scenarioDescText != null) scenarioDescText.text = eventData.ScenarioDescription;
            if (aiMonologueText != null) aiMonologueText.text = $"💬 <color=#06B6D4>[AI 트레이더 독백]</color>\n\"{eventData.AIMonologue}\"";
            if (toastText != null) toastText.gameObject.SetActive(false);

            for (int i = 0; i < 3; i++)
            {
                int optionIndex = i;
                if (optionButtons != null && i < optionButtons.Length && optionButtons[i] != null)
                {
                    optionButtons[i].onClick.RemoveAllListeners();
                    optionButtons[i].onClick.AddListener(() => OnOptionButtonClicked(optionIndex));
                    optionButtons[i].gameObject.SetActive(i < eventData.Options.Length && eventData.Options[i] != null);
                }

                if (optionTexts != null && i < optionTexts.Length && optionTexts[i] != null && i < eventData.Options.Length && eventData.Options[i] != null)
                {
                    string prefix = i == 0 ? "A. [안전] " : (i == 1 ? "B. [공격] " : "C. [특수/직접] ");
                    optionTexts[i].text = $"{prefix}{eventData.Options[i].OptionTitle}\n<size=88%><color=#CBD5E1>{eventData.Options[i].Description}</color></size>";
                }
            }

            if (popupPanel != null)
            {
                popupPanel.SetActive(true);
                popupPanel.transform.SetAsLastSibling();
                EnsureOverlayPriority();

                if (scrollRect == null) scrollRect = popupPanel.GetComponentInChildren<ScrollRect>();
                if (scrollRect != null)
                {
                    Canvas.ForceUpdateCanvases();
                    scrollRect.verticalNormalizedPosition = 1f;
                }
            }
        }

        public void Hide()
        {
            if (popupPanel != null)
            {
                popupPanel.SetActive(false);
            }
        }

        public void ShowToastWarning(string message)
        {
            Debug.LogWarning($"[ChoiceEventUI] ⚠️ {message}");
            if (toastText != null)
            {
                toastText.text = message;
                toastText.gameObject.SetActive(true);
                CancelInvoke(nameof(HideToast));
                Invoke(nameof(HideToast), 2.5f);
            }
        }

        private void HideToast()
        {
            if (toastText != null) toastText.gameObject.SetActive(false);
        }

        private void OnOptionButtonClicked(int index)
        {
            currentCallback?.Invoke(index);
        }

        private void EnsureUIBuilt()
        {
            if (popupPanel != null)
            {
                if (scrollRect != null || popupPanel.GetComponentInChildren<ScrollRect>() != null)
                {
                    return;
                }
                Destroy(popupPanel);
                popupPanel = null;
            }

            // Canvas 찾기 또는 생성
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = FindAnyObjectByType<Canvas>();
            }
            if (canvas == null) return;

            // 팝업 루트 패널 (전체 화면 딤 처리)
            GameObject panelGo = new GameObject("ChoiceEventPopupPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelGo.transform.SetParent(canvas.transform, false);
            RectTransform rootRect = panelGo.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            panelGo.GetComponent<Image>().color = new Color(0, 0, 0, 0.75f);
            popupPanel = panelGo;

            // 중앙 모달 창 (크기 740 x 640으로 넉넉하게 확장)
            GameObject modalGo = new GameObject("ModalBox", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            modalGo.transform.SetParent(panelGo.transform, false);
            RectTransform modalRect = modalGo.GetComponent<RectTransform>();
            modalRect.anchorMin = new Vector2(0.5f, 0.5f);
            modalRect.anchorMax = new Vector2(0.5f, 0.5f);
            modalRect.sizeDelta = new Vector2(740, 640);
            Image modalImg = modalGo.GetComponent<Image>();
            modalImg.color = colorBG;

            // 외곽선 및 헤더 상단 고정 텍스트 (기존 34 -> 39 (+5포인트))
            scenarioTitleText = CreateLabel(modalGo.transform, "Title", 39, new Vector2(24, -16), new Vector2(-24, -76), TextAlignmentOptions.TopLeft, Color.white);

            // 토스트 경고
            toastText = CreateLabel(modalGo.transform, "Toast", 25, new Vector2(24, -580), new Vector2(-24, -616), TextAlignmentOptions.Center, new Color(1f, 0.3f, 0.3f));
            toastText.gameObject.SetActive(false);

            // =========================================================================
            // 하단 스크롤 영역 (ScrollRect) 구축 - 타이틀 하단부터 모달창 하단까지
            // =========================================================================
            GameObject scrollAreaGo = new GameObject("ScrollArea", typeof(RectTransform));
            scrollAreaGo.transform.SetParent(modalGo.transform, false);
            RectTransform scrollAreaRect = scrollAreaGo.GetComponent<RectTransform>();
            scrollAreaRect.anchorMin = new Vector2(0f, 0f);
            scrollAreaRect.anchorMax = new Vector2(1f, 1f);
            scrollAreaRect.offsetMin = new Vector2(20f, 20f);
            scrollAreaRect.offsetMax = new Vector2(-20f, -86f);

            scrollRect = scrollAreaGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 35f;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;

            // Viewport
            GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(scrollAreaGo.transform, false);
            RectTransform viewportRect = viewportGo.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = new Vector2(-16f, 0f); // 우측 스크롤바 여백
            Image viewportImg = viewportGo.GetComponent<Image>();
            viewportImg.color = Color.white;
            viewportImg.raycastTarget = true;
            Mask viewportMask = viewportGo.GetComponent<Mask>();
            viewportMask.showMaskGraphic = false;

            // Content
            GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            RectTransform contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 0f);
            contentRect.anchoredPosition = Vector2.zero;

            VerticalLayoutGroup vLayout = contentGo.GetComponent<VerticalLayoutGroup>();
            vLayout.childAlignment = TextAnchor.UpperLeft;
            vLayout.spacing = 18f;
            vLayout.padding = new RectOffset(8, 12, 10, 24);
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = true;

            ContentSizeFitter contentFitter = contentGo.GetComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Scrollbar (우측 배치)
            GameObject scrollbarGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar));
            scrollbarGo.transform.SetParent(scrollAreaGo.transform, false);
            RectTransform scrollbarRect = scrollbarGo.GetComponent<RectTransform>();
            scrollbarRect.anchorMin = new Vector2(1f, 0f);
            scrollbarRect.anchorMax = new Vector2(1f, 1f);
            scrollbarRect.pivot = new Vector2(1f, 0.5f);
            scrollbarRect.sizeDelta = new Vector2(12f, 0f);
            scrollbarRect.anchoredPosition = Vector2.zero;
            Image scrollbarBg = scrollbarGo.GetComponent<Image>();
            scrollbarBg.color = new Color(0.12f, 0.16f, 0.25f, 0.8f);

            GameObject slidingAreaGo = new GameObject("SlidingArea", typeof(RectTransform));
            slidingAreaGo.transform.SetParent(scrollbarGo.transform, false);
            RectTransform slidingRect = slidingAreaGo.GetComponent<RectTransform>();
            slidingRect.anchorMin = Vector2.zero;
            slidingRect.anchorMax = Vector2.one;
            slidingRect.offsetMin = slidingRect.offsetMax = Vector2.zero;

            GameObject handleGo = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handleGo.transform.SetParent(slidingAreaGo.transform, false);
            RectTransform handleRect = handleGo.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(10f, 0f);
            Image handleImg = handleGo.GetComponent<Image>();
            handleImg.color = new Color(0.28f, 0.38f, 0.55f, 1f);

            Scrollbar scrollbar = scrollbarGo.GetComponent<Scrollbar>();
            scrollbar.handleRect = handleRect;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scrollRect.verticalScrollbarSpacing = 4f;

            // =========================================================================
            // Content 내부에 스크롤되는 대사 및 이벤트 내용, 선택지 버튼 배치
            // =========================================================================
            // 시나리오 설명 (기존 20 -> 25 (+5포인트))
            scenarioDescText = CreateScrollableLabel(contentGo.transform, "Desc", 25, TextAlignmentOptions.TopLeft, new Color(0.89f, 0.92f, 0.96f));

            // AI 트레이더 독백 (기존 18 -> 23 (+5포인트))
            aiMonologueText = CreateScrollableLabel(contentGo.transform, "AIMonologue", 23, TextAlignmentOptions.TopLeft, new Color(0.80f, 0.95f, 1f));

            // 3개 선택지 버튼 (Content 내부에 순차 배치)
            for (int i = 0; i < 3; i++)
            {
                GameObject btnGo = new GameObject($"OptionButton_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
                btnGo.transform.SetParent(contentGo.transform, false);

                Image btnImg = btnGo.GetComponent<Image>();
                btnImg.color = i == 0 ? new Color(colorSafe.r, colorSafe.g, colorSafe.b, 0.25f) :
                               (i == 1 ? new Color(colorAggressive.r, colorAggressive.g, colorAggressive.b, 0.25f) :
                                         new Color(colorSpecial.r, colorSpecial.g, colorSpecial.b, 0.25f));

                VerticalLayoutGroup btnLayout = btnGo.GetComponent<VerticalLayoutGroup>();
                btnLayout.childAlignment = TextAnchor.MiddleLeft;
                btnLayout.padding = new RectOffset(16, 16, 14, 14);
                btnLayout.spacing = 4f;
                btnLayout.childForceExpandWidth = true;
                btnLayout.childForceExpandHeight = false;
                btnLayout.childControlWidth = true;
                btnLayout.childControlHeight = true;

                ContentSizeFitter btnFitter = btnGo.GetComponent<ContentSizeFitter>();
                btnFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                btnFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                LayoutElement btnElem = btnGo.GetComponent<LayoutElement>();
                btnElem.minHeight = 78f;

                optionButtons[i] = btnGo.GetComponent<Button>();
                // 선택지 텍스트 (기존 16 -> 21 (+5포인트))
                optionTexts[i] = CreateScrollableLabel(btnGo.transform, "Text", 21, TextAlignmentOptions.Left, Color.white);
            }

            EnsureOverlayPriority();
        }

        // 차트(5), HUD(20), 상점(100)보다 위에서 렌더링하고 설정창(200)은 최상단으로 유지합니다.
        private void EnsureOverlayPriority()
        {
            if (popupPanel == null) return;

            Canvas popupCanvas = popupPanel.GetComponent<Canvas>();
            if (popupCanvas == null) popupCanvas = popupPanel.AddComponent<Canvas>();
            popupCanvas.overrideSorting = true;
            popupCanvas.sortingOrder = EventPopupSortingOrder;

            if (popupPanel.GetComponent<GraphicRaycaster>() == null)
            {
                popupPanel.AddComponent<GraphicRaycaster>();
            }
        }

        private TMP_Text CreateLabel(Transform parent, string name, int fontSize, Vector2 offsetMin, Vector2 offsetMax, TextAlignmentOptions align, Color textColor)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(offsetMin.x, offsetMax.y);
            rect.offsetMax = new Vector2(offsetMax.x, offsetMin.y);

            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = align;
            text.color = textColor;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private TMP_Text CreateScrollableLabel(Transform parent, string name, int fontSize, TextAlignmentOptions align, Color textColor)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(ContentSizeFitter));
            go.transform.SetParent(parent, false);

            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = align;
            text.color = textColor;
            text.textWrappingMode = TextWrappingModes.Normal;

            ContentSizeFitter fitter = go.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return text;
        }
    }
}
