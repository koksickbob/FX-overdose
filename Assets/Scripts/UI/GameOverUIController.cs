using System.Collections;
using FXOverdose.AI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FXOverdose.UI
{
    /// <summary>
    /// 게임오버 독백 연출의 절반 시점에 최종 세션 리포트를 표시하고 타이틀 복귀를 처리합니다.
    /// 패널을 수동으로 조립하지 않아도 GameScene의 메인 Canvas에 런타임으로 생성됩니다.
    /// </summary>
    public sealed class GameOverUIController : MonoBehaviour
    {
        private const int GameOverSortingOrder = 2000;
        private const float ModalWidth = 1360f;
        private const float ModalHeight = 800f;

        private static readonly Color DeepBackground = new Color32(11, 15, 25, 255);
        private static readonly Color PanelBackground = new Color32(15, 23, 42, 255);
        private static readonly Color HeaderBackground = new Color32(20, 29, 51, 255);
        private static readonly Color BorderColor = new Color32(59, 75, 102, 255);
        private static readonly Color Cyan = new Color32(6, 182, 212, 255);
        private static readonly Color BodyText = new Color32(203, 213, 225, 255);
        private static readonly Color MutedText = new Color32(102, 117, 143, 255);
        private static readonly Color LossRed = new Color32(239, 68, 68, 255);
        private static readonly Color SuccessGreen = new Color32(34, 197, 94, 255);

        [Header("UI 연동 대상")]
        [Tooltip("게임 오버 화면 패널입니다. 비어 있으면 런타임에 자동 생성합니다.")]
        [SerializeField] private GameObject gameOverPanel;

        [Tooltip("게임 오버 사유 TextMeshPro입니다. 비어 있으면 런타임에 자동 생성합니다.")]
        [SerializeField] private TextMeshProUGUI reasonText;

        [Header("타이밍 설정")]
        [Tooltip("게임오버 독백 루프 시작 후 UI를 표시할 실제 시간입니다.")]
#pragma warning disable 0414
        [SerializeField, Min(0f)] private float displayDelay = 13f;
#pragma warning restore 0414
        [SerializeField, Min(0.05f)] private float fadeDuration = 0.4f;
        [SerializeField] private string titleSceneName = "TitleScene";

        private GameManager gameManager;
        private CanvasGroup overlayCanvasGroup;
        private RectTransform modalRect;
        private Outline modalOutline;
        private Image topAccent;
        private Image statusBadgeBackground;
        private Outline statusBadgeOutline;
        private Outline reasonCardOutline;
        private Image characterImage;
        private Button returnTitleButton;
        private Image returnTitleButtonBackground;
        private Outline returnTitleButtonOutline;
        private TMP_Text terminalLabelText;
        private TMP_Text titleText;
        private TMP_Text titleSubtitleText;
        private TMP_Text statusBadgeText;
        private TMP_Text endingCodeText;
        private TMP_Text reasonDetailText;
        private TMP_Text balanceValueText;
        private TMP_Text dayValueText;
        private TMP_Text mentalValueText;
        private TMP_Text emotionLabelText;
        private TMP_Text finalMessageText;
        private TMP_Text returnTitleButtonText;

        private Coroutine presentationCoroutine;
        private bool presentationQueued;
        private bool isReturningToTitle;

        private void Awake()
        {
            ResolveSystems();
            EnsureUIBuilt();
            HideImmediate();
        }

        private void OnEnable()
        {
            GameManager.OnGameOverEvent -= HandleGameOver;
            GameManager.OnGameOverEvent += HandleGameOver;
        }

        private void Start()
        {
            ResolveSystems();
            if (!presentationQueued && gameManager != null &&
                gameManager.CurrentState == GameManager.GameState.GameOver &&
                gameManager.CurrentEnding != GameManager.EndingType.None)
            {
                HandleGameOver(gameManager.CurrentEnding);
            }
        }

        private void OnDisable()
        {
            GameManager.OnGameOverEvent -= HandleGameOver;
        }

        private void OnDestroy()
        {
            GameManager.OnGameOverEvent -= HandleGameOver;
        }

        private void ResolveSystems()
        {
            if (gameManager == null)
            {
                gameManager = GameManager.Instance;
            }
        }

        private void HandleGameOver(GameManager.EndingType endingType)
        {
            ResolveSystems();
            EnsureUIBuilt();

            if (presentationCoroutine != null)
            {
                StopCoroutine(presentationCoroutine);
            }

            presentationQueued = true;
            isReturningToTitle = false;
            HideImmediate();

            // 13초 연출 중 화면은 그대로 보이되, 다른 상점/설정 버튼이 눌리지 않도록 투명 입력막을 즉시 켭니다.
            gameOverPanel.SetActive(true);
            gameOverPanel.transform.SetAsLastSibling();
            overlayCanvasGroup.alpha = 0f;
            overlayCanvasGroup.blocksRaycasts = true;
            overlayCanvasGroup.interactable = true;
            presentationCoroutine = StartCoroutine(ShowGameOverUI(endingType));
        }

        private IEnumerator ShowGameOverUI(GameManager.EndingType endingType)
        {
            if (endingType == GameManager.EndingType.Overdose)
            {
                FXOverdose.AI.AIVisualController aiVisual = FindAnyObjectByType<FXOverdose.AI.AIVisualController>();
                if (aiVisual != null)
                {
                    // 독백 출력이 시작될 여유 프레임을 잠시 대기
                    yield return null;
                    yield return null;

                    while (aiVisual.IsBalloonActive)
                    {
                        if (gameManager != null && gameManager.CurrentState != GameManager.GameState.GameOver)
                        {
                            presentationQueued = false;
                            presentationCoroutine = null;
                            HideImmediate();
                            yield break;
                        }
                        yield return null;
                    }
                }
            }

            ApplyEndingData(endingType);
            gameOverPanel.SetActive(true);
            gameOverPanel.transform.SetAsLastSibling();
            overlayCanvasGroup.alpha = 0f;
            overlayCanvasGroup.blocksRaycasts = true;
            overlayCanvasGroup.interactable = true;
            modalRect.localScale = new Vector3(1.025f, 1.025f, 1f);
            modalRect.anchoredPosition = new Vector2(0f, -28f);

            float transitionElapsed = 0f;
            while (transitionElapsed < fadeDuration)
            {
                transitionElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(transitionElapsed / fadeDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                overlayCanvasGroup.alpha = eased;
                modalRect.localScale = Vector3.LerpUnclamped(new Vector3(1.025f, 1.025f, 1f), Vector3.one, eased);
                modalRect.anchoredPosition = Vector2.LerpUnclamped(new Vector2(0f, -28f), Vector2.zero, eased);
                yield return null;
            }

            overlayCanvasGroup.alpha = 1f;
            modalRect.localScale = Vector3.one;
            modalRect.anchoredPosition = Vector2.zero;
            presentationCoroutine = null;
        }

        private void ApplyEndingData(GameManager.EndingType endingType)
        {
            bool success = endingType == GameManager.EndingType.Success;
            Color theme = success ? SuccessGreen : LossRed;
            string emotionName = GetEmotionName(endingType);
            string endingCode = GetEndingCode(endingType);

            topAccent.color = theme;
            modalOutline.effectColor = Color.Lerp(BorderColor, theme, 0.7f);
            reasonCardOutline.effectColor = Color.Lerp(BorderColor, theme, 0.65f);
            statusBadgeBackground.color = Color.Lerp(HeaderBackground, theme, 0.24f);
            statusBadgeOutline.effectColor = theme;
            statusBadgeText.color = theme;
            endingCodeText.color = theme;
            returnTitleButtonBackground.color = success
                ? new Color32(20, 116, 70, 255)
                : new Color32(132, 38, 48, 255);
            returnTitleButtonOutline.effectColor = theme;
            returnTitleButtonText.color = Color.white;

            terminalLabelText.text = $"FX OVERDOSE  /  TERMINAL NOTICE  /  {endingCode}";
            titleText.text = success ? "SESSION COMPLETE" : "GAME OVER";
            titleSubtitleText.text = success
                ? "TARGET CONDITION VERIFIED  /  TRADING SESSION CLOSED"
                : "ACCOUNT ACCESS REVOKED  /  TRADING SESSION TERMINATED";
            statusBadgeText.text = success ? "SESSION CLEARED" : "ACCOUNT LOCKED";
            endingCodeText.text = $"ENDING CODE  /  {endingCode}";
            reasonText.text = GetEndingReason(endingType);
            reasonDetailText.text = GetEndingDetail(endingType);
            finalMessageText.text = GetFinalMessage(endingType);

            float finalEquity = gameManager != null ? gameManager.CurrentBalance : 0f;
            TraderStatus status = TraderStatus.CanonicalInstance;
            if (status != null)
            {
                finalEquity = status.GetTotalEquity();
            }

            balanceValueText.text = $"${Mathf.Max(0f, finalEquity):N2}";
            dayValueText.text = gameManager != null
                ? $"{FXOverdose.Core.GameCalendar.ToKoreanFull(gameManager.CurrentDate)}  /  {gameManager.CurrentHour:00}:{gameManager.CurrentMinute:00}"
                : "----년 --월 --일  /  --:--";
            mentalValueText.text = status != null
                ? $"{status.CurrentMental:0}/{status.MaxMental:0}  /  {status.CurrentMentalState.ToString().ToUpperInvariant()}"
                : "--/--  /  UNKNOWN";

            Sprite emotionSprite = Resources.Load<Sprite>($"Characters/Emotions/{emotionName}");
            characterImage.sprite = emotionSprite;
            characterImage.enabled = emotionSprite != null;
            characterImage.preserveAspect = true;
            emotionLabelText.text = $"YOMI  /  {emotionName.ToUpperInvariant()}";

            if (System.Enum.TryParse(emotionName, out TraderEmotion emotion))
            {
                AIVisualController visualController = FindAnyObjectByType<AIVisualController>(FindObjectsInactive.Include);
                visualController?.ShowEmotion(emotion, 5f);
            }

            returnTitleButton.interactable = true;
        }

        private static string GetEndingReason(GameManager.EndingType endingType)
        {
            return endingType switch
            {
                GameManager.EndingType.Bankruptcy => "자본금 전액 손실\n강제 청산 및 파산",
                GameManager.EndingType.Overdose => "도파민 과다\n치명적 멘탈 붕괴",
                GameManager.EndingType.Success => "목표 수익 달성\n트레이딩 세션 완료",
                _ => "알 수 없는 오류로\n트레이딩 세션 종료"
            };
        }

        private static string GetEndingDetail(GameManager.EndingType endingType)
        {
            return endingType switch
            {
                GameManager.EndingType.Bankruptcy => "남은 순자산이 거래 유지 기준 아래로 내려가 계정이 강제 종료되었습니다.",
                GameManager.EndingType.Overdose => "요미의 멘탈이 완전히 붕괴해 더 이상 정상적인 매매 판단을 유지할 수 없습니다.",
                GameManager.EndingType.Success => "설정된 목표 조건을 달성했습니다. 최종 세션 기록이 안전하게 확정되었습니다.",
                _ => "세션 종료 원인을 확인할 수 없습니다. 타이틀 화면에서 새 게임을 시작해 주세요."
            };
        }

        private static string GetEndingCode(GameManager.EndingType endingType)
        {
            return endingType switch
            {
                GameManager.EndingType.Bankruptcy => "FXOD-BK-000",
                GameManager.EndingType.Overdose => "FXOD-OD-000",
                GameManager.EndingType.Success => "FXOD-SC-100",
                _ => "FXOD-ER-999"
            };
        }

        private static string GetEmotionName(GameManager.EndingType endingType)
        {
            return endingType switch
            {
                GameManager.EndingType.Bankruptcy => nameof(TraderEmotion.Tearful),
                GameManager.EndingType.Overdose => nameof(TraderEmotion.Obsessive),
                GameManager.EndingType.Success => nameof(TraderEmotion.Euphoria),
                _ => nameof(TraderEmotion.Despairing)
            };
        }

        private static string GetFinalMessage(GameManager.EndingType endingType)
        {
            return endingType switch
            {
                GameManager.EndingType.Bankruptcy => "“다 잃어버렸어... 그래도 오빠, 요미를 혼자 두고 가지 마...”",
                GameManager.EndingType.Overdose => "“머릿속이 멈추질 않아... 오빠만 여기 남아 있으면 돼...”",
                GameManager.EndingType.Success => "“해냈어, 오빠! 우리의 기록은 여기서 끝이 아니라 시작이야.”",
                _ => "“세션이 끝났어... 타이틀 화면에서 다시 만나자.”"
            };
        }

        public void ReturnToTitle()
        {
            if (isReturningToTitle)
            {
                return;
            }

            isReturningToTitle = true;
            returnTitleButton.interactable = false;

            if (presentationCoroutine != null)
            {
                StopCoroutine(presentationCoroutine);
            }
            presentationCoroutine = StartCoroutine(FadeOutAndReturnToTitle());
        }

        private IEnumerator FadeOutAndReturnToTitle()
        {
            float startAlpha = overlayCanvasGroup != null ? overlayCanvasGroup.alpha : 1f;
            float elapsed = 0f;
            const float duration = 0.3f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (overlayCanvasGroup != null)
                {
                    overlayCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                }
                yield return null;
            }

            Time.timeScale = 1f;
            if (Application.CanStreamedLevelBeLoaded(titleSceneName))
            {
                SceneManager.LoadScene(titleSceneName, LoadSceneMode.Single);
            }
            else if (SceneManager.sceneCountInBuildSettings > 0)
            {
                Debug.LogWarning($"[GameOverUI] '{titleSceneName}'을 찾지 못해 빌드 인덱스 0으로 이동합니다.");
                SceneManager.LoadScene(0, LoadSceneMode.Single);
            }
            else
            {
                Debug.LogError("[GameOverUI] 이동할 타이틀 씬이 Build Settings에 없습니다.");
                isReturningToTitle = false;
                returnTitleButton.interactable = true;
            }
        }

        private void HideImmediate()
        {
            if (gameOverPanel == null)
            {
                return;
            }

            if (overlayCanvasGroup != null)
            {
                overlayCanvasGroup.alpha = 0f;
                overlayCanvasGroup.blocksRaycasts = false;
                overlayCanvasGroup.interactable = false;
            }
            gameOverPanel.SetActive(false);
        }

        private void EnsureUIBuilt()
        {
            if (gameOverPanel != null && modalRect != null)
            {
                return;
            }

            Transform canvasTransform = transform;
            Canvas parentCanvas = GetComponent<Canvas>();
            if (parentCanvas == null)
            {
                parentCanvas = GetComponentInParent<Canvas>();
                if (parentCanvas != null)
                {
                    canvasTransform = parentCanvas.transform;
                }
            }

            gameOverPanel = CreatePanel(canvasTransform, "GameOverUI_Panel", new Color(0f, 0f, 0f, 0.91f), true);
            Stretch(gameOverPanel.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

            Canvas overlayCanvas = gameOverPanel.AddComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = GameOverSortingOrder;
            gameOverPanel.AddComponent<GraphicRaycaster>();
            overlayCanvasGroup = gameOverPanel.AddComponent<CanvasGroup>();

            BuildBackdrop(gameOverPanel.transform);

            GameObject modal = CreatePanel(gameOverPanel.transform, "FinalSessionReport", PanelBackground, false);
            modalRect = modal.GetComponent<RectTransform>();
            Fixed(modalRect, new Vector2(0.5f, 0.5f), new Vector2(ModalWidth, ModalHeight), Vector2.zero);

            Shadow modalShadow = modal.AddComponent<Shadow>();
            modalShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            modalShadow.effectDistance = new Vector2(14f, -14f);
            modalShadow.useGraphicAlpha = true;
            modalOutline = AddOutline(modal, LossRed);

            GameObject accent = CreatePanel(modal.transform, "EndingAccent", LossRed, false);
            topAccent = accent.GetComponent<Image>();
            SetTopRect(accent.GetComponent<RectTransform>(), 0f, 0f, 0f, 6f);

            BuildHeader(modal.transform);
            BuildCharacterCard(modal.transform);
            BuildReasonColumn(modal.transform);
        }

        private static void BuildBackdrop(Transform parent)
        {
            GameObject topRule = CreatePanel(parent, "TopWarningRule", new Color32(239, 68, 68, 135), false);
            SetTopRect(topRule.GetComponent<RectTransform>(), 0f, 0f, 70f, 3f);

            GameObject bottomRule = CreatePanel(parent, "BottomWarningRule", new Color32(239, 68, 68, 100), false);
            RectTransform bottomRect = bottomRule.GetComponent<RectTransform>();
            bottomRect.anchorMin = new Vector2(0f, 0f);
            bottomRect.anchorMax = new Vector2(1f, 0f);
            bottomRect.offsetMin = new Vector2(0f, 70f);
            bottomRect.offsetMax = new Vector2(0f, 73f);

            for (int i = 0; i < 5; i++)
            {
                GameObject marker = CreatePanel(parent, $"WarningMarker_{i}", new Color32(239, 68, 68, 34), false);
                RectTransform rect = marker.GetComponent<RectTransform>();
                float x = 0.08f + i * 0.21f;
                rect.anchorMin = new Vector2(x, 0.08f);
                rect.anchorMax = new Vector2(x, 0.92f);
                rect.sizeDelta = new Vector2(2f, 0f);
            }
        }

        private void BuildHeader(Transform parent)
        {
            GameObject header = CreatePanel(parent, "Header", HeaderBackground, false);
            SetTopRect(header.GetComponent<RectTransform>(), 0f, 0f, 6f, 124f);

            terminalLabelText = CreateText(header.transform, "TerminalLabel", 15f, Cyan, TextAlignmentOptions.TopLeft);
            SetTopRect(terminalLabelText.rectTransform, 30f, 430f, 18f, 22f);
            terminalLabelText.text = "FX OVERDOSE  /  TERMINAL NOTICE  /  FXOD-BK-000";
            terminalLabelText.characterSpacing = 1.1f;

            titleText = CreateText(header.transform, "TitleText", 46f, Color.white, TextAlignmentOptions.TopLeft);
            SetTopRect(titleText.rectTransform, 30f, 420f, 40f, 53f);
            titleText.text = "GAME OVER";
            titleText.fontStyle = FontStyles.Bold;

            titleSubtitleText = CreateText(header.transform, "TitleSubtitle", 16f, MutedText, TextAlignmentOptions.TopLeft);
            SetTopRect(titleSubtitleText.rectTransform, 32f, 390f, 94f, 22f);
            titleSubtitleText.text = "ACCOUNT ACCESS REVOKED  /  TRADING SESSION TERMINATED";

            GameObject badge = CreatePanel(header.transform, "StatusBadge", Color.Lerp(HeaderBackground, LossRed, 0.24f), false);
            statusBadgeBackground = badge.GetComponent<Image>();
            SetTopRect(badge.GetComponent<RectTransform>(), 1055f, 30f, 32f, 56f);
            statusBadgeOutline = AddOutline(badge, LossRed, new Vector2(2f, -2f));

            statusBadgeText = CreateText(badge.transform, "Label", 18f, LossRed, TextAlignmentOptions.Center);
            Stretch(statusBadgeText.rectTransform, new Vector2(10f, 0f), new Vector2(-10f, 0f));
            statusBadgeText.text = "ACCOUNT LOCKED";
            statusBadgeText.fontStyle = FontStyles.Bold;
        }

        private void BuildCharacterCard(Transform parent)
        {
            GameObject card = CreatePanel(parent, "CharacterCard", DeepBackground, false);
            SetTopRect(card.GetComponent<RectTransform>(), 30f, 944f, 158f, 604f);
            AddOutline(card, BorderColor);

            TMP_Text cardLabel = CreateText(card.transform, "CardLabel", 14f, MutedText, TextAlignmentOptions.TopLeft);
            SetTopRect(cardLabel.rectTransform, 22f, 22f, 18f, 22f);
            cardLabel.text = "FINAL EMOTION CAPTURE";
            cardLabel.characterSpacing = 1f;

            GameObject portraitFrame = CreatePanel(card.transform, "PortraitFrame", HeaderBackground, false);
            SetTopRect(portraitFrame.GetComponent<RectTransform>(), 22f, 22f, 54f, 445f);
            AddOutline(portraitFrame, new Color32(43, 58, 78, 255), new Vector2(2f, -2f));

            characterImage = CreateImage(portraitFrame.transform, "CharacterImage");
            SetRect(characterImage.rectTransform, new Vector2(0.07f, 0.03f), new Vector2(0.93f, 0.97f), Vector2.zero, Vector2.zero);
            characterImage.preserveAspect = true;
            characterImage.raycastTarget = false;

            emotionLabelText = CreateText(card.transform, "EmotionLabel", 20f, BodyText, TextAlignmentOptions.Center);
            SetTopRect(emotionLabelText.rectTransform, 22f, 22f, 515f, 36f);
            emotionLabelText.text = "YOMI  /  TEARFUL";
            emotionLabelText.fontStyle = FontStyles.Bold;

            TMP_Text archiveText = CreateText(card.transform, "ArchiveLabel", 13f, MutedText, TextAlignmentOptions.Center);
            SetTopRect(archiveText.rectTransform, 22f, 22f, 556f, 24f);
            archiveText.text = "LAST FRAME ARCHIVED";
        }

        private void BuildReasonColumn(Transform parent)
        {
            GameObject reasonCard = CreatePanel(parent, "ReasonCard", DeepBackground, false);
            SetTopRect(reasonCard.GetComponent<RectTransform>(), 446f, 30f, 158f, 248f);
            reasonCardOutline = AddOutline(reasonCard, Color.Lerp(BorderColor, LossRed, 0.65f));

            TMP_Text label = CreateText(reasonCard.transform, "ReasonLabel", 15f, MutedText, TextAlignmentOptions.TopLeft);
            SetTopRect(label.rectTransform, 24f, 280f, 20f, 22f);
            label.text = "TERMINATION REASON";
            label.characterSpacing = 1.1f;

            endingCodeText = CreateText(reasonCard.transform, "EndingCode", 14f, LossRed, TextAlignmentOptions.TopRight);
            SetTopRect(endingCodeText.rectTransform, 360f, 24f, 20f, 22f);
            endingCodeText.text = "ENDING CODE  /  FXOD-BK-000";

            reasonText = CreateText(reasonCard.transform, "ReasonText", 32f, Color.white, TextAlignmentOptions.TopLeft) as TextMeshProUGUI;
            SetTopRect(reasonText.rectTransform, 24f, 24f, 60f, 90f);
            reasonText.text = "자본금 전액 손실\n강제 청산 및 파산";
            reasonText.fontStyle = FontStyles.Bold;
            reasonText.textWrappingMode = TextWrappingModes.Normal;
            reasonText.overflowMode = TextOverflowModes.Ellipsis;

            reasonDetailText = CreateText(reasonCard.transform, "ReasonDetail", 18f, BodyText, TextAlignmentOptions.TopLeft);
            SetTopRect(reasonDetailText.rectTransform, 24f, 24f, 164f, 58f);
            reasonDetailText.text = "남은 순자산이 거래 유지 기준 아래로 내려가 계정이 강제 종료되었습니다.";
            reasonDetailText.textWrappingMode = TextWrappingModes.Normal;

            BuildMetricCards(parent);

            GameObject messageCard = CreatePanel(parent, "FinalMessageCard", HeaderBackground, false);
            SetTopRect(messageCard.GetComponent<RectTransform>(), 446f, 30f, 552f, 98f);
            AddOutline(messageCard, BorderColor, new Vector2(2f, -2f));

            TMP_Text messageLabel = CreateText(messageCard.transform, "MessageLabel", 13f, Cyan, TextAlignmentOptions.TopLeft);
            SetTopRect(messageLabel.rectTransform, 20f, 20f, 12f, 20f);
            messageLabel.text = "FINAL YOMI MESSAGE";

            finalMessageText = CreateText(messageCard.transform, "FinalMessage", 21f, new Color32(207, 250, 254, 255), TextAlignmentOptions.TopLeft);
            SetTopRect(finalMessageText.rectTransform, 20f, 20f, 38f, 46f);
            finalMessageText.text = "“다 잃어버렸어... 그래도 오빠, 요미를 혼자 두고 가지 마...”";
            finalMessageText.textWrappingMode = TextWrappingModes.Normal;

            BuildReturnButton(parent);
        }

        private void BuildMetricCards(Transform parent)
        {
            GameObject metrics = new GameObject("FinalMetrics", typeof(RectTransform));
            metrics.transform.SetParent(parent, false);
            SetTopRect(metrics.GetComponent<RectTransform>(), 446f, 30f, 424f, 110f);

            balanceValueText = CreateMetricCard(metrics.transform, "BalanceMetric", "FINAL EQUITY", "$0.00", 0f, 0.315f);
            dayValueText = CreateMetricCard(metrics.transform, "DayMetric", "SESSION CLOSED", "2026년 6월 26일 / 09:00", 0.3425f, 0.6575f);
            mentalValueText = CreateMetricCard(metrics.transform, "MentalMetric", "MENTAL STATUS", "0/100 / OVERDOSE", 0.685f, 1f);
        }

        private static TMP_Text CreateMetricCard(Transform parent, string name, string label, string value, float minX, float maxX)
        {
            GameObject card = CreatePanel(parent, name, HeaderBackground, false);
            RectTransform rect = card.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(minX, 0f);
            rect.anchorMax = new Vector2(maxX, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            AddOutline(card, BorderColor, new Vector2(2f, -2f));

            TMP_Text labelText = CreateText(card.transform, "Label", 13f, MutedText, TextAlignmentOptions.TopLeft);
            SetTopRect(labelText.rectTransform, 16f, 12f, 14f, 20f);
            labelText.text = label;

            TMP_Text valueText = CreateText(card.transform, "Value", 20f, BodyText, TextAlignmentOptions.TopLeft);
            SetTopRect(valueText.rectTransform, 16f, 12f, 48f, 38f);
            valueText.text = value;
            valueText.enableAutoSizing = true;
            valueText.fontSizeMin = 13f;
            valueText.fontSizeMax = 20f;
            valueText.fontStyle = FontStyles.Bold;
            return valueText;
        }

        private void BuildReturnButton(Transform parent)
        {
            GameObject buttonObject = CreatePanel(parent, "ReturnTitle_Button", new Color32(132, 38, 48, 255), true);
            SetTopRect(buttonObject.GetComponent<RectTransform>(), 446f, 30f, 668f, 94f);
            returnTitleButtonBackground = buttonObject.GetComponent<Image>();
            returnTitleButtonOutline = AddOutline(buttonObject, LossRed);

            returnTitleButton = buttonObject.AddComponent<Button>();
            returnTitleButton.targetGraphic = returnTitleButtonBackground;
            ColorBlock colors = returnTitleButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color32(255, 225, 225, 255);
            colors.pressedColor = new Color32(186, 160, 165, 255);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color32(78, 87, 105, 210);
            colors.fadeDuration = 0.08f;
            returnTitleButton.colors = colors;
            returnTitleButton.onClick.AddListener(ReturnToTitle);

            returnTitleButtonText = CreateText(buttonObject.transform, "Label", 27f, Color.white, TextAlignmentOptions.Center);
            SetTopRect(returnTitleButtonText.rectTransform, 18f, 18f, 10f, 42f);
            returnTitleButtonText.text = "RETURN TO TITLE";
            returnTitleButtonText.fontStyle = FontStyles.Bold;

            TMP_Text hint = CreateText(buttonObject.transform, "Hint", 13f, new Color32(238, 220, 224, 255), TextAlignmentOptions.Center);
            RectTransform hintRect = hint.rectTransform;
            hintRect.anchorMin = new Vector2(0f, 0f);
            hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.offsetMin = new Vector2(18f, 10f);
            hintRect.offsetMax = new Vector2(-18f, 32f);
            hint.text = "END SESSION AND OPEN MAIN MENU";
        }

        private static GameObject CreatePanel(Transform parent, string name, Color color, bool raycastTarget)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(parent, false);
            Image image = panel.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return panel;
        }

        private static Image CreateImage(Transform parent, string name)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            return imageObject.GetComponent<Image>();
        }

        private static TMP_Text CreateText(Transform parent, string name, float size, Color color, TextAlignmentOptions alignment)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.richText = true;
            text.raycastTarget = false;
            return text;
        }

        private static Outline AddOutline(GameObject target, Color color, Vector2? distance = null)
        {
            Outline outline = target.GetComponent<Outline>();
            if (outline == null)
            {
                outline = target.AddComponent<Outline>();
            }
            outline.effectColor = color;
            outline.effectDistance = distance ?? global::UIStrokeStyle.EffectDistance;
            outline.useGraphicAlpha = true;
            return outline;
        }

        private static void Fixed(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 anchoredPosition)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void SetTopRect(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }

    /// <summary>GameScene이 단독 또는 LoadingScene에서 Additive로 열릴 때 게임오버 UI를 자동 설치합니다.</summary>
    public static class GameOverUIBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneCallback()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "GameScene")
            {
                return;
            }

            Install(scene);
        }

        private static void Install(Scene gameScene)
        {
            Canvas target = null;
            Canvas fallback = null;

            foreach (GameObject root in gameScene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<GameOverUIController>(true) != null)
                {
                    return;
                }

                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    if (!canvas.isRootCanvas)
                    {
                        continue;
                    }

                    if (canvas.name == "TradingViewCanvas")
                    {
                        target = canvas;
                        break;
                    }

                    if (fallback == null || canvas.sortingOrder > fallback.sortingOrder)
                    {
                        fallback = canvas;
                    }
                }

                if (target != null)
                {
                    break;
                }
            }

            target ??= fallback;
            if (target == null)
            {
                Debug.LogError("[GameOverUI] GameScene에서 설치할 Canvas를 찾지 못했습니다.");
                return;
            }

            target.gameObject.AddComponent<GameOverUIController>();
            Debug.Log("[GameOverUI] GameScene에 게임오버 UI 설치 완료");
        }
    }
}
