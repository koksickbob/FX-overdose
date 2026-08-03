using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace FXOverdose.UI
{
    /// <summary>
    /// 스토리 모드의 컷툰(만화) 연출을 담당하는 UI 컨트롤러입니다.
    /// 게임 중 팝업 형태로 화면 최상단에 나타나며, 클릭 시 다음 컷으로 넘어갑니다.
    /// </summary>
    public class ComicCutsceneController : MonoBehaviour
    {
        private static ComicCutsceneController _instance;
        public static ComicCutsceneController Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<ComicCutsceneController>();
                }
                return _instance;
            }
        }

        /// <summary>
        /// 스토리 컷씬 Canvas가 배치되지 않은 tutorial 씬에서도 동일한 재생 UI를 제공합니다.
        /// </summary>
        public static ComicCutsceneController GetOrCreateRuntime(Scene ownerScene)
        {
            ComicCutsceneController existing = Instance;
            if (existing != null)
                return existing;

            GameObject root = new(
                "ComicCutsceneCanvas_Runtime",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup),
                typeof(Image));
            if (ownerScene.IsValid() && ownerScene.isLoaded)
                SceneManager.MoveGameObjectToScene(root, ownerScene);

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1200;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Image backdrop = root.GetComponent<Image>();
            backdrop.color = new Color32(4, 9, 18, 245);
            backdrop.raycastTarget = false;

            GameObject imageObject = new("ComicImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(root.transform, false);
            Image comicImage = imageObject.GetComponent<Image>();
            comicImage.preserveAspect = true;
            comicImage.raycastTarget = false;

            GameObject nextObject = new("NextPanelButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            nextObject.transform.SetParent(root.transform, false);
            RectTransform nextRect = nextObject.GetComponent<RectTransform>();
            nextRect.anchorMin = Vector2.zero;
            nextRect.anchorMax = Vector2.one;
            nextRect.offsetMin = nextRect.offsetMax = Vector2.zero;
            Image nextImage = nextObject.GetComponent<Image>();
            nextImage.color = Color.clear;
            Button nextButton = nextObject.GetComponent<Button>();
            nextButton.transition = Selectable.Transition.None;

            GameObject skipObject = new("SkipButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            skipObject.transform.SetParent(root.transform, false);
            Button runtimeSkipButton = skipObject.GetComponent<Button>();

            GameObject labelObject = new("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(skipObject.transform, false);
            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;

            ComicCutsceneController controller = root.AddComponent<ComicCutsceneController>();
            controller.canvasGroup = root.GetComponent<CanvasGroup>();
            controller.comicImageDisplay = comicImage;
            controller.skipButton = runtimeSkipButton;
            controller.nextPanelButton = nextButton;
            controller.EnsureRuntimePresentation();
            controller.ApplySkipButtonStyle();
            runtimeSkipButton.onClick.AddListener(controller.SkipCutscene);
            nextButton.onClick.AddListener(controller.ShowNextPanel);

            controller.canvasGroup.alpha = 0f;
            controller.canvasGroup.interactable = false;
            controller.canvasGroup.blocksRaycasts = false;
            return controller;
        }

        [Header("UI References")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image comicImageDisplay;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button nextPanelButton; // 전체 화면을 덮는 투명 버튼 (클릭 시 다음 컷)
        [SerializeField] private TextMeshProUGUI instructionText; // "화면을 터치하세요" 등

        [Header("Settings")]
        [SerializeField] private float fadeDuration = 0.5f;

        private List<Sprite> _currentPanels;
        private int _currentIndex = 0;
        private Action _onCompleteCallback;
        private bool _isPlaying = false;
        private bool _isFading = false;
        private float _fadeTimer = 0f;
        private float _targetAlpha = 0f;
        private List<string> _currentCaptions;
        private TextMeshProUGUI _captionText;
        private TextMeshProUGUI _progressText;

        private static readonly string[] TerminologyPanelPaths =
        {
            "Tutorial/Terminology/Panel01_LongShort",
            "Tutorial/Terminology/Panel02_Margin",
            "Tutorial/Terminology/Panel03_Leverage",
            "Tutorial/Terminology/Panel04_Liquidation"
        };

        private static readonly string[] TerminologyCaptions =
        {
            "보통 주식은 오를 때만 돈을 벌지?\n<color=#FF5B65><b>LONG(롱)</b></color>: 주가가 <b>오를 것</b>에 배팅!    <color=#58AFFF><b>SHORT(숏)</b></color>: 주가가 <b>내릴 것</b>에 배팅!",
            "<color=#F9D66D><b>증거금(Margin)</b></color>: 이번 거래에 내가 실제로 걸 '판돈'이야.\n가진 돈 전부가 아니라 일부만 떼어서 투자할 수 있어!",
            "<color=#59E3F2><b>레버리지(Leverage)</b></color>: 적은 돈으로 큰 돈을 굴리는 마법의 지렛대!\n배율이 커지면 수익도 크게 늘어나지만 위험도 똑같이 커져.",
            "<color=#FF6B72><b>청산(Liquidation)</b></color>: 손실이 증거금을 넘어서는 순간 거래가 강제로 종료돼!\n판돈을 전부 잃을 수 있으니 하이 리스크, 하이 리턴을 꼭 명심해!"
        };

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            EnsureRuntimePresentation();
            ApplySkipButtonStyle();
            
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (skipButton != null)
                skipButton.onClick.AddListener(SkipCutscene);

            if (nextPanelButton != null)
                nextPanelButton.onClick.AddListener(ShowNextPanel);
        }

        private void EnsureRuntimePresentation()
        {
            RectTransform rootRect = GetComponent<RectTransform>();
            if (rootRect != null) rootRect.localScale = Vector3.one;

            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null) canvas.sortingOrder = 1200;
            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            if (comicImageDisplay != null)
            {
                RectTransform imageRect = comicImageDisplay.rectTransform;
                // 용어 만화는 대사/설명이 말풍선으로 이미지에 포함되어 있으므로
                // 하단 자막 공간을 비워두지 않고 패널 전체를 사용합니다.
                imageRect.anchorMin = new Vector2(0.035f, 0.035f);
                imageRect.anchorMax = new Vector2(0.965f, 0.965f);
                imageRect.offsetMin = imageRect.offsetMax = Vector2.zero;
                comicImageDisplay.preserveAspect = true;
            }

            Transform existing = transform.Find("TutorialCaptionPanel");
            GameObject captionPanel = existing != null ? existing.gameObject :
                new GameObject("TutorialCaptionPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            captionPanel.transform.SetParent(transform, false);
            RectTransform panelRect = captionPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.08f, 0.025f);
            panelRect.anchorMax = new Vector2(0.92f, 0.20f);
            panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
            Image panelImage = captionPanel.GetComponent<Image>();
            panelImage.color = new Color32(8, 18, 39, 242);
            panelImage.raycastTarget = false;
            Outline panelOutline = captionPanel.GetComponent<Outline>();
            panelOutline.effectColor = new Color32(6, 182, 212, 220);
            panelOutline.effectDistance = UIStrokeStyle.EffectDistance;

            _captionText = CreateRuntimeText(captionPanel.transform, "Caption", 30f, TextAlignmentOptions.Center);
            SetRuntimeRect(_captionText.rectTransform, new Vector2(0.04f, 0.12f), new Vector2(0.92f, 0.90f));
            _captionText.textWrappingMode = TextWrappingModes.Normal;

            _progressText = CreateRuntimeText(captionPanel.transform, "Progress", 20f, TextAlignmentOptions.Center);
            SetRuntimeRect(_progressText.rectTransform, new Vector2(0.92f, 0.10f), new Vector2(0.985f, 0.90f));
            _progressText.color = new Color32(139, 234, 242, 255);

            captionPanel.transform.SetAsLastSibling();
            captionPanel.SetActive(false);
            if (skipButton != null) skipButton.transform.SetAsLastSibling();
        }

        public void PlayTerminologyTutorial(Action onCompleteCallback)
        {
            List<Sprite> panels = new();
            foreach (string path in TerminologyPanelPaths)
            {
                Sprite panel = Resources.Load<Sprite>(path);
                if (panel != null) panels.Add(panel);
                else Debug.LogWarning($"[ComicCutsceneController] 용어 만화 컷 누락: Resources/{path}");
            }

            // 용어 설명은 각 패널의 말풍선에 직접 포함되어 있습니다.
            _currentCaptions = null;
            PlayCutscene(panels, onCompleteCallback);
        }

        private void ApplySkipButtonStyle()
        {
            if (skipButton == null) return;

            RectTransform buttonRect = skipButton.GetComponent<RectTransform>();
            buttonRect.anchorMin = Vector2.one;
            buttonRect.anchorMax = Vector2.one;
            buttonRect.pivot = Vector2.one;
            buttonRect.anchoredPosition = new Vector2(-28f, -28f);
            buttonRect.sizeDelta = new Vector2(154f, 52f);
            buttonRect.SetAsLastSibling();

            Image background = skipButton.GetComponent<Image>();
            if (background == null)
            {
                background = skipButton.gameObject.AddComponent<Image>();
            }
            background.color = new Color32(64, 68, 76, 165);
            skipButton.targetGraphic = background;
            skipButton.transition = Selectable.Transition.ColorTint;

            ColorBlock colors = skipButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color32(220, 220, 220, 255);
            colors.pressedColor = new Color32(165, 165, 165, 255);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color32(110, 110, 110, 130);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            skipButton.colors = colors;

            Outline outline = skipButton.GetComponent<Outline>();
            if (outline == null)
            {
                outline = skipButton.gameObject.AddComponent<Outline>();
            }
            outline.effectColor = new Color32(255, 255, 255, 90);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;

            TextMeshProUGUI label = skipButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.text = "SKIP  >";
                label.fontSize = 22f;
                label.enableAutoSizing = false;
                label.fontStyle = FontStyles.Normal;
                label.alignment = TextAlignmentOptions.Center;
                label.color = Color.white;
                label.characterSpacing = 1f;
                label.raycastTarget = false;

                RectTransform labelRect = label.rectTransform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(10f, 4f);
                labelRect.offsetMax = new Vector2(-10f, -4f);
            }
        }

        private void Update()
        {
            if (_isFading && canvasGroup != null)
            {
                _fadeTimer += Time.unscaledDeltaTime; // TimeScale=0 상태에서도 작동하도록
                float t = Mathf.Clamp01(_fadeTimer / fadeDuration);
                
                // 알파값 보간
                float startAlpha = canvasGroup.alpha;
                canvasGroup.alpha = Mathf.MoveTowards(startAlpha, _targetAlpha, Time.unscaledDeltaTime / fadeDuration);

                if (Mathf.Approximately(canvasGroup.alpha, _targetAlpha))
                {
                    _isFading = false;
                    if (_targetAlpha == 0f)
                    {
                        canvasGroup.interactable = false;
                        canvasGroup.blocksRaycasts = false;
                        _isPlaying = false;
                        _onCompleteCallback?.Invoke();
                        _onCompleteCallback = null;
                        _currentCaptions = null;
                    }
                }
            }
        }

        /// <summary>
        /// 만화(컷툰) 패널 리스트를 순차적으로 띄웁니다. 재생이 끝나면 callback이 호출됩니다.
        /// </summary>
        public void PlayCutscene(List<Sprite> panels, Action onCompleteCallback)
        {
            if (panels == null || panels.Count == 0)
            {
                Debug.LogWarning("[ComicCutsceneController] 전달된 컷툰 패널이 없습니다. 즉시 콜백을 실행합니다.");
                onCompleteCallback?.Invoke();
                return;
            }

            _currentPanels = panels;
            _currentIndex = 0;
            _onCompleteCallback = onCompleteCallback;
            _isPlaying = true;

            if (comicImageDisplay != null)
                comicImageDisplay.sprite = _currentPanels[_currentIndex];
            RefreshPanelCaption();

            // UI 켜기
            if (canvasGroup != null)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                _targetAlpha = 1f;
                _fadeTimer = 0f;
                _isFading = true;
            }
        }

        private void ShowNextPanel()
        {
            if (!_isPlaying || _isFading) return;

            _currentIndex++;
            if (_currentIndex < _currentPanels.Count)
            {
                // 다음 컷 띄우기
                if (comicImageDisplay != null)
                {
                    comicImageDisplay.sprite = _currentPanels[_currentIndex];
                }
                RefreshPanelCaption();
            }
            else
            {
                // 만화 끝남
                EndCutscene();
            }
        }

        private void SkipCutscene()
        {
            if (!_isPlaying || _isFading) return;
            EndCutscene();
        }

        private void EndCutscene()
        {
            // UI 끄기 (Fade Out)
            if (canvasGroup != null)
            {
                _targetAlpha = 0f;
                _fadeTimer = 0f;
                _isFading = true;
                canvasGroup.interactable = false;
            }
            else
            {
                _isPlaying = false;
                _onCompleteCallback?.Invoke();
                _onCompleteCallback = null;
            }
        }

        private void RefreshPanelCaption()
        {
            if (_captionText != null)
                _captionText.text = _currentCaptions != null && _currentIndex < _currentCaptions.Count
                    ? _currentCaptions[_currentIndex]
                    : string.Empty;
            if (_progressText != null)
                _progressText.text = _currentPanels != null ? $"{_currentIndex + 1}/{_currentPanels.Count}" : string.Empty;
        }

        private static TextMeshProUGUI CreateRuntimeText(Transform parent, string name, float size, TextAlignmentOptions alignment)
        {
            GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = size;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Max(14f, size * 0.65f);
            text.fontSizeMax = size;
            text.color = Color.white;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private static void SetRuntimeRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
