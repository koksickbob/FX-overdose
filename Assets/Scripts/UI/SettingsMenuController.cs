using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>우측 상단 설정 버튼, 일시정지 팝업, 게임 종료를 관리합니다.</summary>
public class SettingsMenuController : MonoBehaviour
{
    [SerializeField] private Button settingsButton;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private TMP_FontAsset font;

    private GameObject overlay;
    private bool pausedBySettings;
    private float previousTimeScale = 1f;

    private TMP_Text popupModeText;
    private TMP_Text floatingModeText;
    private Image popupModeImage;
    private Image floatingModeImage;

    [Header("오디오 설정 UI (UI 담당자 할당)")]
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    private void Awake()
    {
        if (settingsButton == null) settingsButton = GetComponent<Button>();
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();

        BuildMenu();
        settingsButton?.onClick.AddListener(ToggleMenu);
        CreateFloatingModeToggleButton();

        // 오디오 슬라이더 이벤트 연동
        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.onValueChanged.AddListener(OnBgmVolumeChanged);
            if (FXOverdose.Core.AudioManager.Instance != null)
                bgmVolumeSlider.value = FXOverdose.Core.AudioManager.Instance.bgmVolume;
        }
        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
            if (FXOverdose.Core.AudioManager.Instance != null)
                sfxVolumeSlider.value = FXOverdose.Core.AudioManager.Instance.sfxVolume;
        }
    }

    public void OnBgmVolumeChanged(float value)
    {
        if (FXOverdose.Core.AudioManager.Instance != null)
        {
            FXOverdose.Core.AudioManager.Instance.SetBGMVolume(value);
            FXOverdose.Core.AudioManager.Instance.SaveSettings();
        }
    }

    public void OnSfxVolumeChanged(float value)
    {
        if (FXOverdose.Core.AudioManager.Instance != null)
        {
            FXOverdose.Core.AudioManager.Instance.SetSFXVolume(value);
            FXOverdose.Core.AudioManager.Instance.SaveSettings();
        }
    }

    private void UpdateModeButtonVisuals()
    {
        var controller = FXOverdose.Trading.TradingController.Instance;
        if (controller == null) return;

        bool isAuto = controller.ActiveTradingMode == FXOverdose.Trading.TradingController.TradingMode.AI_Auto;
        string popupLabel = isAuto ? "모드: ⚡ AI 자동" : "모드: 🎮 수동 매매";
        string floatLabel = isAuto ? "AI" : "USER";
        Color btnColor = isAuto ? new Color(0.12f, 0.48f, 0.72f, 1f) : new Color(0.75f, 0.35f, 0.08f, 1f);

        if (popupModeText != null) popupModeText.text = popupLabel;
        if (floatingModeText != null) floatingModeText.text = floatLabel;
        if (popupModeImage != null) popupModeImage.color = btnColor;
        if (floatingModeImage != null) floatingModeImage.color = new Color32(20, 29, 51, 255); // #141D33
        if (floatingModeText != null)
            floatingModeText.color = isAuto ? new Color32(207, 250, 254, 255) : new Color32(234, 179, 8, 255);
    }

    private void CreateFloatingModeToggleButton()
    {
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null) return;

        GameObject go = CreateUIObject("Temp_TradingModeToggleBtn", parentCanvas.transform, typeof(Button));
        RectTransform rect = go.GetComponent<RectTransform>();

        // 전체 화면 캔버스의 우측 상단(1, 1)을 앵커/피벗으로 고정하여 앵커 스트레칭(세로 늘어남)을 완벽 차단
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(92f, 46f);
        rect.anchoredPosition = new Vector2(-15f, -65f);

        floatingModeImage = go.GetComponent<Image>();
        floatingModeImage.sprite = null;
        floatingModeImage.color = new Color32(20, 29, 51, 255); // #141D33

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color32(6, 182, 212, 255); // #06B6D4
        outline.effectDistance = UIStrokeStyle.EffectDistance;

        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = floatingModeImage;

        floatingModeText = CreateText(go.transform, "Label", "AI", 13f, TextAlignmentOptions.Center);
        floatingModeText.textWrappingMode = TextWrappingModes.NoWrap;
        floatingModeText.fontSizeMin = 8f;
        floatingModeText.fontSizeMax = 13f;
        floatingModeText.fontStyle = FontStyles.Bold;
        floatingModeText.outlineWidth = 0.18f;
        floatingModeText.outlineColor = new Color32(11, 15, 25, 255);
        Stretch(floatingModeText.rectTransform);

        btn.onClick.AddListener(() => {
            FXOverdose.Trading.TradingController.Instance?.ToggleTradingMode();
            UpdateModeButtonVisuals();
        });

        Invoke(nameof(UpdateModeButtonVisuals), 0.2f);
        StartCoroutine(SyncFloatingButtonLayoutCoroutine());
    }

    private System.Collections.IEnumerator SyncFloatingButtonLayoutCoroutine()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        UpdateFloatingButtonLayout();
    }

    private void UpdateFloatingButtonLayout()
    {
        if (floatingModeImage == null) return;
        RectTransform myRect = floatingModeImage.GetComponent<RectTransform>();
        if (myRect == null) return;

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null) return;

        RectTransform canvasRect = parentCanvas.GetComponent<RectTransform>();
        RectTransform pnlRect = GameObject.Find("PnLCard")?.GetComponent<RectTransform>();
        RectTransform chartRect = GameObject.Find("ChartMainPanel")?.GetComponent<RectTransform>();

        // 레이아웃 기준 오브젝트를 찾지 못한 경우에만 기존 설정 버튼 아래 위치를 사용합니다.
        if (canvasRect == null || pnlRect == null || chartRect == null)
        {
            PlaceBelowSettingsFallback(myRect, parentCanvas);
            return;
        }

        Vector3[] pnlCorners = new Vector3[4];
        Vector3[] chartCorners = new Vector3[4];
        pnlRect.GetWorldCorners(pnlCorners);
        chartRect.GetWorldCorners(chartCorners);

        Vector3 pnlBottomLeft = parentCanvas.transform.InverseTransformPoint(pnlCorners[0]);
        Vector3 chartTopRight = parentCanvas.transform.InverseTransformPoint(chartCorners[2]);

        const float edgeGap = 10f;
        const float chartGap = 10f; // 2px Outline을 제외한 실제 보이는 간격은 상단 카드와 같은 8px
        const float chartTopInset = 0f;
        const float buttonWidth = 92f;
        const float buttonHeight = 46f;

        // 차트 오른쪽 + P&L 아래의 교차 영역에 배치합니다.
        // 세로 위치는 chartTopInset으로 직접 조정하고, 가로는 외곽선을 고려한 시각 간격을 유지합니다.
        float x = chartTopRight.x + chartGap;
        float y = Mathf.Min(pnlBottomLeft.y - edgeGap, chartTopRight.y - chartTopInset);

        Rect bounds = canvasRect.rect;
        x = Mathf.Clamp(x, bounds.xMin + edgeGap, bounds.xMax - buttonWidth - edgeGap);
        y = Mathf.Clamp(y, bounds.yMin + buttonHeight + edgeGap, bounds.yMax - edgeGap);

        myRect.anchorMin = myRect.anchorMax = new Vector2(0.5f, 0.5f);
        myRect.pivot = new Vector2(0f, 1f);
        myRect.sizeDelta = new Vector2(buttonWidth, buttonHeight);
        myRect.localPosition = new Vector3(x, y, 0f);
    }

    private void PlaceBelowSettingsFallback(RectTransform myRect, Canvas parentCanvas)
    {
        if (settingsButton == null) return;
        RectTransform settingsRect = settingsButton.GetComponent<RectTransform>();
        if (settingsRect == null) return;

        Vector3[] corners = new Vector3[4];
        settingsRect.GetWorldCorners(corners);

        Vector3 localBottomRight = parentCanvas.transform.InverseTransformPoint(corners[3]);

        myRect.anchorMin = myRect.anchorMax = new Vector2(0.5f, 0.5f);
        myRect.pivot = new Vector2(1f, 1f);
        myRect.sizeDelta = new Vector2(92f, 46f);
        myRect.localPosition = localBottomRight + new Vector3(0f, -8f, 0f);
    }

    private void OnDestroy()
    {
        settingsButton?.onClick.RemoveListener(ToggleMenu);
        if (overlay != null && overlay.activeSelf) RestoreGameState();
    }

    public void ToggleMenu()
    {
        if (overlay == null) BuildMenu();

        if (overlay.activeSelf) CloseMenu();
        else OpenMenu();
    }

    public void OpenMenu()
    {
        if (overlay == null) BuildMenu();
        if (overlay == null || overlay.activeSelf) return;

        pausedBySettings = gameManager != null && gameManager.CurrentState == GameManager.GameState.Playing;
        if (pausedBySettings) gameManager.PauseGame();

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
    }

    public void CloseMenu()
    {
        if (overlay != null) overlay.SetActive(false);
        RestoreGameState();
    }

    public void SaveGame()
    {
        if (FXOverdose.Core.SaveLoadManager.Instance != null)
        {
            FXOverdose.Core.SaveLoadManager.Instance.SaveGame(0);
            Debug.Log("[SettingsMenuController] 게임 저장 완료 (Slot 0)");
            
            Transform saveBtnObj = overlay.transform.Find("SettingsPanel/InnerFrame/SaveButton");
            if (saveBtnObj != null)
            {
                var tmpText = saveBtnObj.GetComponentInChildren<TMP_Text>();
                if (tmpText != null)
                {
                    tmpText.text = "SAVED!";
                    Invoke(nameof(ResetSaveButtonText), 2f);
                }
            }
        }
    }

    private void ResetSaveButtonText()
    {
        if (overlay == null) return;
        Transform saveBtnObj = overlay.transform.Find("SettingsPanel/InnerFrame/SaveButton");
        if (saveBtnObj != null)
        {
            var tmpText = saveBtnObj.GetComponentInChildren<TMP_Text>();
            if (tmpText != null) tmpText.text = "SAVE GAME";
        }
    }

    public void QuitGame()
    {
        Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void RestoreGameState()
    {
        Time.timeScale = previousTimeScale;
        if (pausedBySettings && gameManager != null) gameManager.ResumeGame();
        pausedBySettings = false;
    }

    private void BuildMenu()
    {
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null) return;

        Transform oldOverlay = parentCanvas.transform.Find("SettingsOverlay");
        if (oldOverlay != null) Destroy(oldOverlay.gameObject);

        overlay = CreateUIObject("SettingsOverlay", parentCanvas.transform, typeof(Canvas), typeof(GraphicRaycaster));
        Stretch(overlay.GetComponent<RectTransform>());

        Canvas overlayCanvas = overlay.GetComponent<Canvas>();
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = 200;

        Image dim = overlay.GetComponent<Image>();
        dim.color = new Color(0.005f, 0.012f, 0.035f, 0.82f);
        dim.raycastTarget = true;

        GameObject panel = CreateUIObject("SettingsPanel", overlay.transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(570f, 540f);
        panelRect.anchoredPosition = Vector2.zero;
        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.025f, 0.065f, 0.13f, 0.99f);

        GameObject inner = CreateUIObject("InnerFrame", panel.transform);
        RectTransform innerRect = inner.GetComponent<RectTransform>();
        innerRect.anchorMin = new Vector2(0.025f, 0.03f);
        innerRect.anchorMax = new Vector2(0.975f, 0.97f);
        innerRect.offsetMin = innerRect.offsetMax = Vector2.zero;
        inner.GetComponent<Image>().color = new Color(0.05f, 0.11f, 0.20f, 1f);

        TMP_Text title = CreateText(inner.transform, "SettingsTitle", "SETTINGS", 46f, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(0.08f, 0.76f), new Vector2(0.92f, 0.95f));
        title.color = new Color(0.55f, 0.90f, 1f, 1f);

        TMP_Text paused = CreateText(inner.transform, "PausedLabel", "GAME PAUSED", 23f, TextAlignmentOptions.Center);
        SetRect(paused.rectTransform, new Vector2(0.08f, 0.62f), new Vector2(0.92f, 0.75f));
        paused.color = new Color(0.75f, 0.80f, 0.90f, 1f);

        Button modeSwitchButton = CreateButton(inner.transform, "ModeSwitchButton", "모드: ⚡ AI 자동", new Color(0.12f, 0.48f, 0.72f, 1f));
        SetRect(modeSwitchButton.GetComponent<RectTransform>(), new Vector2(0.26f, 0.50f), new Vector2(0.74f, 0.58f));
        popupModeImage = modeSwitchButton.GetComponent<Image>();
        popupModeText = modeSwitchButton.GetComponentInChildren<TMP_Text>();
        if (popupModeText != null)
        {
            popupModeText.fontSize = 17f;
            popupModeText.fontSizeMin = 13f;
            popupModeText.fontSizeMax = 17f;
        }
        modeSwitchButton.onClick.AddListener(() => {
            FXOverdose.Trading.TradingController.Instance?.ToggleTradingMode();
            UpdateModeButtonVisuals();
        });

        Button saveButton = CreateButton(inner.transform, "SaveButton", "SAVE GAME", new Color(0.18f, 0.55f, 0.34f, 1f));
        SetRect(saveButton.GetComponent<RectTransform>(), new Vector2(0.13f, 0.34f), new Vector2(0.87f, 0.45f));
        saveButton.onClick.AddListener(SaveGame);

        Button resumeButton = CreateButton(inner.transform, "ResumeButton", "CONTINUE", new Color(0.05f, 0.46f, 0.58f, 1f));
        SetRect(resumeButton.GetComponent<RectTransform>(), new Vector2(0.13f, 0.19f), new Vector2(0.87f, 0.30f));
        resumeButton.onClick.AddListener(CloseMenu);

        Button quitButton = CreateButton(inner.transform, "QuitButton", "QUIT GAME", new Color(0.60f, 0.15f, 0.22f, 1f));
        SetRect(quitButton.GetComponent<RectTransform>(), new Vector2(0.13f, 0.04f), new Vector2(0.87f, 0.15f));
        quitButton.onClick.AddListener(QuitGame);

        overlay.SetActive(false);
    }

    private Button CreateButton(Transform parent, string objectName, string label, Color color)
    {
        GameObject go = CreateUIObject(objectName, parent, typeof(Button));
        Image image = go.GetComponent<Image>();
        image.color = color;

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = UIStrokeStyle.DefaultColor;
        outline.effectDistance = UIStrokeStyle.EffectDistance;
        outline.useGraphicAlpha = true;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.88f);
        colors.pressedColor = new Color(0.68f, 0.76f, 0.90f, 1f);
        button.colors = colors;

        TMP_Text text = CreateText(go.transform, "Label", label, 27f, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        return button;
    }

    private TMP_Text CreateText(Transform parent, string objectName, string value, float size, TextAlignmentOptions alignment)
    {
        GameObject go = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.font = font != null ? font : TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(14f, size - 8f);
        text.fontSizeMax = size;
        text.fontStyle = FontStyles.Bold;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private static GameObject CreateUIObject(string objectName, Transform parent, params System.Type[] components)
    {
        GameObject go = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        foreach (System.Type component in components)
        {
            if (go.GetComponent(component) == null) go.AddComponent(component);
        }
        return go;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
