using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>우측 상단 설정 버튼, 일시정지 팝업, 게임 종료를 관리합니다.</summary>
public class SettingsMenuController : MonoBehaviour
{
    private const float FloatingModeButtonWidth = 92f;
    private const float FloatingModeButtonHeight = UIStrokeStyle.CompactHudHeight;

    [SerializeField] private Button settingsButton;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private TMP_FontAsset font;

    private GameObject overlay;
    private bool pausedBySettings;
    private float previousTimeScale = 1f;

    private TMP_Text floatingModeText;
    private Image floatingModeImage;
    private Button floatingModeButton;
    private Button saveMenuButton;
    private Button fpsMenuButton;
    private TMP_Text fpsButtonText;
    private GameObject overwriteConfirmPanel;

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
        var saveManager = FXOverdose.Core.SaveLoadManager.Instance;
        bool isChallenge = saveManager != null && saveManager.CurrentGameMode == FXOverdose.Core.GameMode.Challenge;
        bool canSave = saveManager == null || saveManager.AllowsSaving;

        bool isAuto = !isChallenge && controller != null &&
                      controller.ActiveTradingMode == FXOverdose.Trading.TradingController.TradingMode.AI_Auto;
        string floatLabel = isChallenge ? "USER" : isAuto ? "AUTO" : "USER";

        if (floatingModeText != null)
        {
            floatingModeText.text = floatLabel;
            floatingModeText.color = isChallenge
                ? new Color32(255, 114, 142, 255)
                : isAuto ? new Color32(207, 250, 254, 255) : new Color32(234, 179, 8, 255);
            GlobalPFStardustFont.RefreshCompactHudText(floatingModeText);
        }
        if (floatingModeImage != null) floatingModeImage.color = new Color32(20, 29, 51, 255); // #141D33

        if (floatingModeButton != null)
        {
            floatingModeButton.interactable = !isChallenge;
            bool hasPosition = controller != null && controller.CurrentPosition != FXOverdose.Trading.TradingController.PositionType.None;
            bool isAutoTrading = isAuto && hasPosition;
            floatingModeButton.gameObject.SetActive(!isAutoTrading);
        }

        if (saveMenuButton != null)
        {
            saveMenuButton.interactable = canSave;
            TMP_Text saveLabel = saveMenuButton.GetComponentInChildren<TMP_Text>();
            if (saveLabel != null)
            {
                saveLabel.text = canSave
                    ? $"SAVE STORY {Mathf.Clamp((saveManager?.ActiveStorySlotIndex ?? 0) + 1, 1, 3):00}"
                    : "STORY MODE ONLY";
            }
        }
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
        rect.sizeDelta = new Vector2(FloatingModeButtonWidth, FloatingModeButtonHeight);
        rect.anchoredPosition = new Vector2(-15f, -65f);

        floatingModeImage = go.GetComponent<Image>();
        floatingModeImage.sprite = null;
        floatingModeImage.color = new Color32(20, 29, 51, 255); // #141D33

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color32(6, 182, 212, 255); // #06B6D4
        outline.effectDistance = UIStrokeStyle.EffectDistance;

        floatingModeButton = go.GetComponent<Button>();
        floatingModeButton.targetGraphic = floatingModeImage;

        floatingModeText = CreateText(go.transform, "Label", "AUTO", 13f, TextAlignmentOptions.Center);
        floatingModeText.outlineColor = new Color32(11, 15, 25, 255);
        GlobalPFStardustFont.ConfigureCompactHudText(floatingModeText, font, 13f, 0.05f);
        Stretch(floatingModeText.rectTransform);

        floatingModeButton.onClick.AddListener(() => {
            FXOverdose.Trading.TradingController.Instance?.ToggleTradingMode();
        });

        // 주도권 변경 이벤트 구독
        if (FXOverdose.Trading.TradingController.Instance != null)
        {
            FXOverdose.Trading.TradingController.Instance.OnTradingModeChanged -= _ => UpdateModeButtonVisuals();
            FXOverdose.Trading.TradingController.Instance.OnTradingModeChanged += _ => UpdateModeButtonVisuals();
            FXOverdose.Trading.TradingController.Instance.OnPositionChanged -= UpdateModeButtonVisuals;
            FXOverdose.Trading.TradingController.Instance.OnPositionChanged += UpdateModeButtonVisuals;
        }

        // 로딩 직후 Time.timeScale이 0이어도 Challenge 잠금 상태가 잘 안보일 수 있어, Invoke로 지연 반영
        UpdateModeButtonVisuals();
        Invoke(nameof(UpdateModeButtonVisuals), 0.2f);
        StartCoroutine(SyncFloatingButtonLayoutCoroutine());
    }

    private global::System.Collections.IEnumerator SyncFloatingButtonLayoutCoroutine()
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
        const float buttonWidth = FloatingModeButtonWidth;
        const float buttonHeight = FloatingModeButtonHeight;

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
        myRect.sizeDelta = new Vector2(FloatingModeButtonWidth, FloatingModeButtonHeight);
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
        UpdateModeButtonVisuals();
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
    }

    public void OpenAchievements()
    {
        if (FXOverdose.UI.AchievementUIController.Instance != null)
        {
            FXOverdose.UI.AchievementUIController.Instance.Open();
        }
        else
        {
            var ui = FindAnyObjectByType<FXOverdose.UI.AchievementUIController>(FindObjectsInactive.Include);
            if (ui != null) ui.Open();
        }
    }

    public void CloseMenu()
    {
        if (overlay != null) overlay.SetActive(false);
        RestoreGameState();
    }

    private void OnSaveButtonClicked()
    {
        if (FXOverdose.Core.SaveLoadManager.Instance != null)
        {
            int slotIndex = FXOverdose.Core.SaveLoadManager.Instance.ActiveStorySlotIndex;
            if (FXOverdose.Core.SaveLoadManager.Instance.HasSave(slotIndex))
            {
                if (overwriteConfirmPanel != null) overwriteConfirmPanel.SetActive(true);
            }
            else
            {
                SaveGame();
            }
        }
    }

    public void SaveGame()
    {
        if (overwriteConfirmPanel != null) overwriteConfirmPanel.SetActive(false);

        if (FXOverdose.Core.SaveLoadManager.Instance != null)
        {
            bool saved = FXOverdose.Core.SaveLoadManager.Instance.SaveCurrentGame();
            Debug.Log(saved
                ? $"[SettingsMenuController] 스토리 저장 완료 (Slot {FXOverdose.Core.SaveLoadManager.Instance.ActiveStorySlotIndex + 1})"
                : "[SettingsMenuController] 열린 포지션 또는 시스템 상태로 인해 저장하지 못했습니다.");
            
            Transform saveBtnObj = overlay.transform.Find("SettingsPanel/InnerFrame/SaveButton");
            if (saveBtnObj != null)
            {
                var tmpText = saveBtnObj.GetComponentInChildren<TMP_Text>();
                if (tmpText != null)
                {
                    var trading = FindAnyObjectByType<FXOverdose.Trading.TradingController>(FindObjectsInactive.Include);
                    bool isOverdose = trading != null && trading.IsOverdoseTradeActive;

                    if (saved) tmpText.text = "SAVED!";
                    else if (isOverdose) tmpText.text = "OVERDOSE!";
                    else if (FXOverdose.Core.SaveLoadManager.Instance.AllowsSaving) tmpText.text = "CLOSE POSITION";
                    else tmpText.text = "STORY MODE ONLY";

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
            if (tmpText != null)
            {
                var manager = FXOverdose.Core.SaveLoadManager.Instance;
                tmpText.text = manager == null || manager.AllowsSaving
                    ? $"SAVE STORY {Mathf.Clamp((manager?.ActiveStorySlotIndex ?? 0) + 1, 1, 3):00}"
                    : "STORY MODE ONLY";
            }
        }
    }

    public void QuitGame()
    {
        Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;

        UnityEngine.SceneManagement.SceneManager.LoadScene("TitleScene");
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
        dim.color = new Color(0.002f, 0.008f, 0.022f, 0.90f);
        dim.raycastTarget = true;

        GameObject panel = CreateUIObject("SettingsPanel", overlay.transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(640f, 900f);
        panelRect.anchoredPosition = Vector2.zero;
        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color32(7, 16, 31, 255);
        Outline panelOutline = panel.AddComponent<Outline>();
        panelOutline.effectColor = new Color32(6, 182, 212, 255);
        panelOutline.effectDistance = new Vector2(3f, -3f);

        GameObject inner = CreateUIObject("InnerFrame", panel.transform);
        RectTransform innerRect = inner.GetComponent<RectTransform>();
        innerRect.anchorMin = new Vector2(0.025f, 0.03f);
        innerRect.anchorMax = new Vector2(0.975f, 0.97f);
        innerRect.offsetMin = innerRect.offsetMax = Vector2.zero;
        inner.GetComponent<Image>().color = new Color32(13, 26, 46, 255);
        Outline innerOutline = inner.AddComponent<Outline>();
        innerOutline.effectColor = new Color32(51, 76, 105, 255);
        innerOutline.effectDistance = new Vector2(2f, -2f);

        GameObject headerSurface = CreateSectionPanel(inner.transform, "HeaderSurface",
            new Vector2(0.04f, 0.815f), new Vector2(0.96f, 0.96f), new Color32(19, 38, 63, 255));
        GameObject audioSurface = CreateSectionPanel(inner.transform, "AudioSurface",
            new Vector2(0.06f, 0.535f), new Vector2(0.94f, 0.79f), new Color32(9, 20, 37, 255));
        GameObject displaySurface = CreateSectionPanel(inner.transform, "DisplaySurface",
            new Vector2(0.06f, 0.38f), new Vector2(0.94f, 0.515f), new Color32(9, 20, 37, 255));
        GameObject actionSurface = CreateSectionPanel(inner.transform, "ActionSurface",
            new Vector2(0.06f, 0.025f), new Vector2(0.94f, 0.36f), new Color32(9, 20, 37, 255));
        headerSurface.transform.SetAsFirstSibling();
        audioSurface.transform.SetSiblingIndex(1);
        displaySurface.transform.SetSiblingIndex(2);
        actionSurface.transform.SetSiblingIndex(3);

        TMP_Text title = CreateText(inner.transform, "SettingsTitle", "SETTINGS", 44f, TextAlignmentOptions.MidlineLeft);
        SetRect(title.rectTransform, new Vector2(0.09f, 0.885f), new Vector2(0.72f, 0.95f));
        title.color = new Color32(207, 250, 254, 255);

        TMP_Text paused = CreateText(inner.transform, "PausedLabel", "●  GAME PAUSED", 18f, TextAlignmentOptions.MidlineLeft);
        SetRect(paused.rectTransform, new Vector2(0.09f, 0.825f), new Vector2(0.72f, 0.875f));
        paused.color = new Color32(34, 197, 94, 255);

        TMP_Text audioHeader = CreateText(inner.transform, "AudioHeader", "AUDIO", 18f, TextAlignmentOptions.MidlineLeft);
        SetRect(audioHeader.rectTransform, new Vector2(0.09f, 0.745f), new Vector2(0.35f, 0.785f));
        audioHeader.color = new Color32(6, 182, 212, 255);

        TMP_Text bgmLabel = CreateText(inner.transform, "Label_BGM", "BGM VOLUME", 19f, TextAlignmentOptions.BottomLeft);
        SetRect(bgmLabel.rectTransform, new Vector2(0.11f, 0.685f), new Vector2(0.89f, 0.725f));
        bgmVolumeSlider = CreateSlider(inner.transform, "Slider_BGM", new Vector2(0.11f, 0.635f), new Vector2(0.89f, 0.68f), new Color32(6, 182, 212, 255));

        TMP_Text sfxLabel = CreateText(inner.transform, "Label_SFX", "SFX VOLUME", 19f, TextAlignmentOptions.BottomLeft);
        SetRect(sfxLabel.rectTransform, new Vector2(0.11f, 0.585f), new Vector2(0.89f, 0.625f));
        sfxVolumeSlider = CreateSlider(inner.transform, "Slider_SFX", new Vector2(0.11f, 0.545f), new Vector2(0.89f, 0.585f), new Color32(34, 211, 238, 255));

        TMP_Text displayHeader = CreateText(inner.transform, "DisplayHeader", "DISPLAY", 18f, TextAlignmentOptions.MidlineLeft);
        SetRect(displayHeader.rectTransform, new Vector2(0.09f, 0.475f), new Vector2(0.35f, 0.51f));
        displayHeader.color = new Color32(6, 182, 212, 255);

        TMP_Text fpsLabel = CreateText(inner.transform, "Label_FPS", "FRAME LIMIT", 19f, TextAlignmentOptions.MidlineLeft);
        SetRect(fpsLabel.rectTransform, new Vector2(0.11f, 0.405f), new Vector2(0.45f, 0.465f));
        
        int currentFps = FXOverdose.Core.SystemSettingsManager.GetCurrentFPS();
        fpsMenuButton = CreateButton(inner.transform, "FpsButton", $"{currentFps} FPS", new Color(0.10f, 0.35f, 0.48f, 1f));
        SetRect(fpsMenuButton.GetComponent<RectTransform>(), new Vector2(0.52f, 0.405f), new Vector2(0.89f, 0.465f));
        fpsMenuButton.onClick.AddListener(CycleFPS);
        fpsButtonText = fpsMenuButton.GetComponentInChildren<TMP_Text>();

        Button achievementsMenuButton = CreateButton(inner.transform, "AchievementsButton", "ACHIEVEMENTS", new Color(0.35f, 0.15f, 0.48f, 1f));
        SetRect(achievementsMenuButton.GetComponent<RectTransform>(), new Vector2(0.11f, 0.29f), new Vector2(0.89f, 0.355f));
        achievementsMenuButton.onClick.AddListener(OpenAchievements);

        saveMenuButton = CreateButton(inner.transform, "SaveButton", "SAVE STORY 01", new Color(0.18f, 0.55f, 0.34f, 1f));
        SetRect(saveMenuButton.GetComponent<RectTransform>(), new Vector2(0.11f, 0.205f), new Vector2(0.89f, 0.27f));
        saveMenuButton.onClick.AddListener(OnSaveButtonClicked);

        Button resumeButton = CreateButton(inner.transform, "ResumeButton", "CONTINUE", new Color(0.05f, 0.46f, 0.58f, 1f));
        SetRect(resumeButton.GetComponent<RectTransform>(), new Vector2(0.11f, 0.12f), new Vector2(0.89f, 0.185f));
        resumeButton.onClick.AddListener(CloseMenu);

        Button quitButton = CreateButton(inner.transform, "QuitButton", "RETURN TO TITLE", new Color(0.60f, 0.15f, 0.22f, 1f));
        SetRect(quitButton.GetComponent<RectTransform>(), new Vector2(0.11f, 0.035f), new Vector2(0.89f, 0.10f));
        quitButton.onClick.AddListener(QuitGame);

        UpdateModeButtonVisuals();
        CreateOverwriteConfirmDialog();
        overlay.SetActive(false);
    }

    private void CreateOverwriteConfirmDialog()
    {
        if (overlay == null) return;

        overwriteConfirmPanel = CreateUIObject("OverwriteConfirmPanel", overlay.transform);
        Stretch(overwriteConfirmPanel.GetComponent<RectTransform>());
        Image dim = overwriteConfirmPanel.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.85f);
        dim.raycastTarget = true;

        GameObject box = CreateUIObject("Box", overwriteConfirmPanel.transform);
        RectTransform boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = new Vector2(460f, 240f);
        boxRect.anchoredPosition = Vector2.zero;
        
        Image boxBg = box.GetComponent<Image>();
        boxBg.color = new Color32(13, 26, 46, 255);
        Outline outline = box.AddComponent<Outline>();
        outline.effectColor = new Color32(255, 114, 142, 255);
        outline.effectDistance = new Vector2(2f, -2f);

        TMP_Text msg = CreateText(box.transform, "Message", "기존 저장 데이터가 존재합니다.\n정말 덮어씌우시겠습니까?", 24f, TextAlignmentOptions.Center);
        SetRect(msg.rectTransform, new Vector2(0.05f, 0.45f), new Vector2(0.95f, 0.95f));

        Button yesBtn = CreateButton(box.transform, "YesButton", "OVERWRITE", new Color(0.60f, 0.15f, 0.22f, 1f));
        SetRect(yesBtn.GetComponent<RectTransform>(), new Vector2(0.1f, 0.15f), new Vector2(0.45f, 0.35f));
        yesBtn.onClick.AddListener(SaveGame);
        yesBtn.GetComponentInChildren<TMP_Text>().fontSize = 20f;

        Button noBtn = CreateButton(box.transform, "NoButton", "CANCEL", new Color(0.10f, 0.35f, 0.48f, 1f));
        SetRect(noBtn.GetComponent<RectTransform>(), new Vector2(0.55f, 0.15f), new Vector2(0.9f, 0.35f));
        noBtn.onClick.AddListener(() => overwriteConfirmPanel.SetActive(false));
        noBtn.GetComponentInChildren<TMP_Text>().fontSize = 20f;

        overwriteConfirmPanel.SetActive(false);
    }

    private void CycleFPS()
    {
        int current = FXOverdose.Core.SystemSettingsManager.GetCurrentFPS();
        var options = FXOverdose.Core.SystemSettingsManager.FpsOptions;
        int idx = System.Array.IndexOf(options, current);
        if (idx < 0) idx = 1; // Default to index 1 (60) if not found
        idx = (idx + 1) % options.Length;
        int nextFps = options[idx];
        
        FXOverdose.Core.SystemSettingsManager.SetFPS(nextFps);
        if (fpsButtonText != null)
        {
            fpsButtonText.text = $"{nextFps} FPS";
        }
    }

    private GameObject CreateSectionPanel(Transform parent, string objectName, Vector2 min, Vector2 max, Color color)
    {
        GameObject section = CreateUIObject(objectName, parent);
        SetRect(section.GetComponent<RectTransform>(), min, max);
        section.GetComponent<Image>().color = color;
        Outline outline = section.AddComponent<Outline>();
        outline.effectColor = new Color32(45, 69, 95, 255);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;
        return section;
    }

    private Button CreateButton(Transform parent, string objectName, string label, Color color)
    {
        GameObject go = CreateUIObject(objectName, parent, typeof(Button));
        Image image = go.GetComponent<Image>();
        image.color = color;

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = Color.Lerp(color, Color.white, 0.42f);
        outline.effectDistance = UIStrokeStyle.EffectDistance;
        outline.useGraphicAlpha = true;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.88f);
        colors.pressedColor = new Color(0.68f, 0.76f, 0.90f, 1f);
        button.colors = colors;

        TMP_Text text = CreateText(go.transform, "Label", label, 29f, TextAlignmentOptions.Center);
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
        text.fontSizeMin = Mathf.Max(8f, size - 8f);
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

    private Slider CreateSlider(Transform parent, string objectName, Vector2 min, Vector2 max, Color fillColor)
    {
        GameObject root = new(objectName, typeof(RectTransform), typeof(Slider));
        root.transform.SetParent(parent, false);
        SetRect(root.GetComponent<RectTransform>(), min, max);

        GameObject bg = CreateUIObject("Background", root.transform, typeof(Image));
        SetRect(bg.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
        Image background = bg.GetComponent<Image>();
        background.color = new Color32(2, 8, 18, 255);
        Outline trackOutline = bg.AddComponent<Outline>();
        trackOutline.effectColor = new Color32(59, 82, 110, 255);
        trackOutline.effectDistance = new Vector2(2f, -2f);

        GameObject fillArea = new("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(root.transform, false);
        SetRect(fillArea.GetComponent<RectTransform>(), new Vector2(0.02f, 0.18f), new Vector2(0.98f, 0.82f));
        
        GameObject fillObj = CreateUIObject("Fill", fillArea.transform, typeof(Image));
        SetRect(fillObj.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
        Image fill = fillObj.GetComponent<Image>();
        fill.color = fillColor;

        GameObject handleArea = new("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(root.transform, false);
        SetRect(handleArea.GetComponent<RectTransform>(), new Vector2(0.02f, 0f), new Vector2(0.98f, 1f));
        GameObject handleObject = CreateUIObject("Handle", handleArea.transform, typeof(Image));
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.anchorMin = handleRect.anchorMax = new Vector2(0f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.sizeDelta = new Vector2(20f, 30f);
        handleRect.anchoredPosition = Vector2.zero;
        Image handle = handleObject.GetComponent<Image>();
        handle.color = new Color32(226, 248, 255, 255);
        Outline handleOutline = handleObject.AddComponent<Outline>();
        handleOutline.effectColor = fillColor;
        handleOutline.effectDistance = new Vector2(2f, -2f);

        Slider slider = root.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle;
        return slider;
    }
}
