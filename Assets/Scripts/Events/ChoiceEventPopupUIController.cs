using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FXOverdose.Events
{
    public class ChoiceEventPopupUIController : MonoBehaviour
    {
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

        private void Awake()
        {
            EnsureUIBuilt();
            if (popupPanel != null)
            {
                popupPanel.SetActive(false);
            }
        }

        public void Show(ChoiceEventSO eventData, Action<int> onOptionSelected)
        {
            if (eventData == null) return;

            EnsureUIBuilt();

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
                    optionTexts[i].text = $"{prefix}{eventData.Options[i].OptionTitle}\n<size=80%><color=#CBD5E1>{eventData.Options[i].Description}</color></size>";
                }
            }

            if (popupPanel != null)
            {
                popupPanel.SetActive(true);
                popupPanel.transform.SetAsLastSibling();
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
            if (popupPanel != null) return;

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

            // 중앙 모달 창
            GameObject modalGo = new GameObject("ModalBox", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            modalGo.transform.SetParent(panelGo.transform, false);
            RectTransform modalRect = modalGo.GetComponent<RectTransform>();
            modalRect.anchorMin = new Vector2(0.5f, 0.5f);
            modalRect.anchorMax = new Vector2(0.5f, 0.5f);
            modalRect.sizeDelta = new Vector2(720, 600);
            Image modalImg = modalGo.GetComponent<Image>();
            modalImg.color = colorBG;

            // 외곽선 및 헤더 텍스트 생성
            scenarioTitleText = CreateLabel(modalGo.transform, "Title", 34, new Vector2(20, -20), new Vector2(-20, -80), TextAlignmentOptions.TopLeft, Color.white);
            scenarioDescText = CreateLabel(modalGo.transform, "Desc", 20, new Vector2(20, -90), new Vector2(-20, -220), TextAlignmentOptions.TopLeft, new Color(0.89f, 0.92f, 0.96f));
            aiMonologueText = CreateLabel(modalGo.transform, "AIMonologue", 18, new Vector2(25, -230), new Vector2(-25, -340), TextAlignmentOptions.TopLeft, new Color(0.80f, 0.95f, 1f));

            // 토스트 경고
            toastText = CreateLabel(modalGo.transform, "Toast", 20, new Vector2(20, -560), new Vector2(-20, -590), TextAlignmentOptions.Center, new Color(1f, 0.3f, 0.3f));
            toastText.gameObject.SetActive(false);

            // 3개 선택지 버튼 컨테이너
            float btnY = -360f;
            for (int i = 0; i < 3; i++)
            {
                GameObject btnGo = new GameObject($"OptionButton_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                btnGo.transform.SetParent(modalGo.transform, false);
                RectTransform btnRect = btnGo.GetComponent<RectTransform>();
                btnRect.anchorMin = new Vector2(0f, 1f);
                btnRect.anchorMax = new Vector2(1f, 1f);
                btnRect.anchoredPosition = new Vector2(0, btnY);
                btnRect.sizeDelta = new Vector2(-40, 56);

                Image btnImg = btnGo.GetComponent<Image>();
                btnImg.color = i == 0 ? new Color(colorSafe.r, colorSafe.g, colorSafe.b, 0.25f) :
                               (i == 1 ? new Color(colorAggressive.r, colorAggressive.g, colorAggressive.b, 0.25f) :
                                         new Color(colorSpecial.r, colorSpecial.g, colorSpecial.b, 0.25f));

                optionButtons[i] = btnGo.GetComponent<Button>();
                optionTexts[i] = CreateLabel(btnGo.transform, "Text", 16, new Vector2(12, -4), new Vector2(-12, -52), TextAlignmentOptions.Center, Color.white);

                btnY -= 64f;
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
    }
}
