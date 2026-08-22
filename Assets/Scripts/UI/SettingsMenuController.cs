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
    [SerializeField] private bool createFloatingModeToggle = true;

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
    private GameObject saveSlotPanel;
    private TMP_InputField saveNameInput;
    private TMP_Text saveSlotFeedback;
    private int pendingSaveSlotIndex = -1;
    private RectTransform settingsPanelRect;
    private GameObject settingsHeaderSurface;
    private GameObject settingsActionSurface;
    private GameObject[] restrictedHiddenObjects;

    /// <summary>
    /// 저장·업적을 숨긴 <b>제한 레이아웃</b>을 강제합니다. P2P 경기 외에 이벤트 진행 중에도 필요합니다.
    ///
    /// ⚠️ 이벤트 진행 중에는 저장이 금지되므로(이벤트 시스템 계획 7.1절) 저장 버튼이 노출되면
    ///    요구를 정면으로 위반합니다. 세우는 쪽이 반드시 되돌려야 하며, 이벤트 시스템은
    ///    종료 경로와 OnDestroy 양쪽에서 내립니다.
    ///
    /// 정적인 이유: 설정 메뉴는 씬마다 따로 있고 이벤트보다 늦게 만들어질 수도 있는데,
    /// 이 값은 메뉴를 <b>열 때마다</b> 다시 읽히므로 누가 먼저 태어났는지와 무관해집니다.
    /// </summary>
    public static bool RestrictedLayoutRequested { get; set; }
    private Button resumeMenuButton;
    private Button quitMenuButton;

    /// <summary>튜토리얼에서 AUTO/USER 전환 버튼 전체를 강조할 때 사용하는 고정 타겟입니다.</summary>
    public RectTransform TutorialTradingModeHighlightTarget =>
        floatingModeButton != null ? floatingModeButton.GetComponent<RectTransform>() : null;

    [Header("오디오 설정 UI (UI 담당자 할당)")]
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    private void Awake()
    {
        if (settingsButton == null) settingsButton = GetComponent<Button>();
        if (gameManager == null) gameManager = GameManager.Instance;

        BuildMenu();
        settingsButton?.onClick.AddListener(ToggleMenu);
        if (createFloatingModeToggle) CreateFloatingModeToggleButton();

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

    /// <summary>요미 방처럼 설정 버튼만 재사용하는 화면에서는 트레이딩 모드 버튼 생성을 생략합니다.</summary>
    public void ConfigureRoomButton(Button button)
    {
        settingsButton = button;
        createFloatingModeToggle = false;
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
        // P2P는 USER 수동 매매로 고정되므로 AUTO/USER 전환 버튼 자체를 표시하지 않습니다.
        // 포지션/PnL 갱신 이벤트가 버튼을 다시 켜서 깜빡이는 것도 여기서 차단합니다.
        if (FXOverdose.P2P.Infrastructure.P2PNetworkSessionManager.Instance?.IsRunning == true)
        {
            if (floatingModeButton != null) floatingModeButton.gameObject.SetActive(false);
            return;
        }

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
            floatingModeButton.gameObject.SetActive(true);
        }

        if (saveMenuButton != null)
        {
            saveMenuButton.interactable = canSave;
            TMP_Text saveLabel = saveMenuButton.GetComponentInChildren<TMP_Text>();
            if (saveLabel != null)
            {
                saveLabel.text = canSave
                    ? "SAVE STORY"
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
            // 람다로 -= 하면 매번 새 델리게이트라 아무것도 제거되지 않아, BuildMenu가 다시 돌 때마다
            // 구독이 누적됐습니다. 메서드 그룹이어야 -=가 실제로 동작합니다.
            FXOverdose.Trading.TradingController.Instance.OnTradingModeChanged -= HandleTradingModeChanged;
            FXOverdose.Trading.TradingController.Instance.OnTradingModeChanged += HandleTradingModeChanged;
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

    private void HandleTradingModeChanged(FXOverdose.Trading.TradingController.TradingMode mode)
        => UpdateModeButtonVisuals();

    private void OnDestroy()
    {
        settingsButton?.onClick.RemoveListener(ToggleMenu);

        var trading = FXOverdose.Trading.TradingController.Instance;
        if (trading != null)
        {
            trading.OnTradingModeChanged -= HandleTradingModeChanged;
            trading.OnPositionChanged -= UpdateModeButtonVisuals;
        }

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
        ApplyRestrictedMenuLayout();
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
        if (FXOverdose.Core.SaveLoadManager.Instance == null || saveSlotPanel == null) return;
        RefreshSaveSlotPanel();
        saveSlotPanel.SetActive(true);
        saveSlotPanel.transform.SetAsLastSibling();
    }

    public void SaveGame()
    {
        if (overwriteConfirmPanel != null) overwriteConfirmPanel.SetActive(false);

        int slotIndex = pendingSaveSlotIndex >= 0
            ? pendingSaveSlotIndex
            : FXOverdose.Core.SaveLoadManager.Instance?.ActiveStorySlotIndex ?? 0;
        string saveName = saveNameInput != null ? saveNameInput.text.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(saveName))
        {
            if (saveSlotFeedback != null) saveSlotFeedback.text = "저장 데이터 이름을 입력해 주세요.";
            return;
        }

        if (FXOverdose.Core.SaveLoadManager.Instance != null)
        {
            bool saved = FXOverdose.Core.SaveLoadManager.Instance.SaveGame(slotIndex, saveName);
            Debug.Log(saved
                ? $"[SettingsMenuController] 스토리 저장 완료 (Slot {slotIndex + 1}: {saveName})"
                : "[SettingsMenuController] 열린 포지션 또는 시스템 상태로 인해 저장하지 못했습니다.");

            if (saved && saveSlotPanel != null) saveSlotPanel.SetActive(false);
            
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
                    ? "SAVE STORY"
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
        // 로딩 직후 freeze 구간(LoadingScreenController)에서 메뉴를 열면 previousTimeScale이 0으로 잡힙니다.
        // 그대로 되돌리면 게임이 영구 정지하므로 QuitGame과 동일한 가드를 겁니다.
        Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
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
        settingsPanelRect = panelRect;
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
        settingsHeaderSurface = headerSurface;
        settingsActionSurface = actionSurface;
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

        resumeMenuButton = CreateButton(inner.transform, "ResumeButton", "CONTINUE", new Color(0.05f, 0.46f, 0.58f, 1f));
        SetRect(resumeMenuButton.GetComponent<RectTransform>(), new Vector2(0.11f, 0.12f), new Vector2(0.89f, 0.185f));
        resumeMenuButton.onClick.AddListener(CloseMenu);

        quitMenuButton = CreateButton(inner.transform, "QuitButton", "RETURN TO TITLE", new Color(0.60f, 0.15f, 0.22f, 1f));
        SetRect(quitMenuButton.GetComponent<RectTransform>(), new Vector2(0.11f, 0.035f), new Vector2(0.89f, 0.10f));
        quitMenuButton.onClick.AddListener(QuitGame);

        restrictedHiddenObjects = new[]
        {
            achievementsMenuButton.gameObject, saveMenuButton.gameObject
        };

        ApplyRestrictedMenuLayout();
        UpdateModeButtonVisuals();
        CreateOverwriteConfirmDialog();
        CreateSaveSlotDialog();
        overlay.SetActive(false);
    }

    /// <summary>
    /// 제한 레이아웃 — 업적·세이브만 숨기고 오디오·FPS 설정과 종료 동작은 그대로 제공합니다.
    /// P2P 경기와 이벤트 진행 중이 이 레이아웃을 씁니다. (<see cref="RestrictedLayoutRequested"/>)
    /// </summary>
    private void ApplyRestrictedMenuLayout()
    {
        bool restricted = RestrictedLayoutRequested ||
                          FXOverdose.P2P.Infrastructure.P2PNetworkSessionManager.Instance?.IsRunning == true;

        if (restrictedHiddenObjects != null)
        {
            foreach (GameObject target in restrictedHiddenObjects)
            {
                if (target != null) target.SetActive(!restricted);
            }
        }

        if (settingsPanelRect != null)
            settingsPanelRect.sizeDelta = new Vector2(640f, 900f);

        if (settingsHeaderSurface != null)
            SetRect(settingsHeaderSurface.GetComponent<RectTransform>(),
                new Vector2(0.04f, 0.815f), new Vector2(0.96f, 0.96f));

        if (settingsActionSurface != null)
            SetRect(settingsActionSurface.GetComponent<RectTransform>(),
                new Vector2(0.06f, 0.025f), new Vector2(0.94f, 0.36f));

        if (resumeMenuButton != null)
            SetRect(resumeMenuButton.GetComponent<RectTransform>(),
                restricted ? new Vector2(0.11f, 0.29f) : new Vector2(0.11f, 0.12f),
                restricted ? new Vector2(0.89f, 0.355f) : new Vector2(0.89f, 0.185f));

        if (quitMenuButton != null)
            SetRect(quitMenuButton.GetComponent<RectTransform>(),
                restricted ? new Vector2(0.11f, 0.205f) : new Vector2(0.11f, 0.035f),
                restricted ? new Vector2(0.89f, 0.27f) : new Vector2(0.89f, 0.10f));
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

    private void CreateSaveSlotDialog()
    {
        saveSlotPanel = CreateUIObject("SaveSlotPanel", overlay.transform);
        Stretch(saveSlotPanel.GetComponent<RectTransform>());
        Image dim = saveSlotPanel.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.88f);
        dim.raycastTarget = true;

        GameObject box = CreateUIObject("Box", saveSlotPanel.transform);
        RectTransform boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = new Vector2(820f, 760f);
        boxRect.anchoredPosition = Vector2.zero;
        box.GetComponent<Image>().color = new Color32(13, 26, 46, 255);
        Outline outline = box.AddComponent<Outline>();
        outline.effectColor = new Color32(6, 182, 212, 255);
        outline.effectDistance = new Vector2(3f, -3f);

        TMP_Text title = CreateText(box.transform, "Title", "SELECT SAVE SLOT", 34f, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(0.05f, 0.91f), new Vector2(0.95f, 0.98f));

        for (int i = 0; i < FXOverdose.Core.SaveLoadManager.MaxStorySlots; i++)
        {
            int slotIndex = i;
            int column = i / 10;
            int row = i % 10;
            float xMin = column == 0 ? 0.06f : 0.52f;
            float xMax = column == 0 ? 0.48f : 0.94f;
            float yMax = 0.89f - row * 0.061f;
            Button button = CreateButton(box.transform, $"SaveSlot_{i + 1}", string.Empty, new Color(0.08f, 0.22f, 0.34f, 1f));
            SetRect(button.GetComponent<RectTransform>(), new Vector2(xMin, yMax - 0.047f), new Vector2(xMax, yMax));
            button.GetComponentInChildren<TMP_Text>().fontSize = 18f;
            button.onClick.AddListener(() => SelectSaveSlot(slotIndex));
        }

        GameObject inputBackground = CreateUIObject("SaveNameInput", box.transform, typeof(TMP_InputField));
        SetRect(inputBackground.GetComponent<RectTransform>(), new Vector2(0.06f, 0.17f), new Vector2(0.94f, 0.245f));
        inputBackground.GetComponent<Image>().color = new Color32(4, 14, 28, 255);
        TMP_Text inputText = CreateText(inputBackground.transform, "Text", string.Empty, 22f, TextAlignmentOptions.MidlineLeft);
        SetRect(inputText.rectTransform, new Vector2(0.035f, 0.08f), new Vector2(0.965f, 0.92f));
        TMP_Text placeholder = CreateText(inputBackground.transform, "Placeholder", "저장 데이터 이름을 입력하세요", 20f, TextAlignmentOptions.MidlineLeft);
        SetRect(placeholder.rectTransform, new Vector2(0.035f, 0.08f), new Vector2(0.965f, 0.92f));
        placeholder.color = new Color32(100, 126, 151, 255);
        saveNameInput = inputBackground.GetComponent<TMP_InputField>();
        saveNameInput.textComponent = inputText;
        saveNameInput.placeholder = placeholder;
        saveNameInput.lineType = TMP_InputField.LineType.SingleLine;
        saveNameInput.characterLimit = 30;

        saveSlotFeedback = CreateText(box.transform, "Feedback", "슬롯을 선택하고 저장 이름을 입력해 주세요.", 16f, TextAlignmentOptions.Center);
        SetRect(saveSlotFeedback.rectTransform, new Vector2(0.06f, 0.12f), new Vector2(0.94f, 0.165f));
        saveSlotFeedback.color = new Color32(126, 160, 184, 255);

        Button save = CreateButton(box.transform, "ConfirmSave", "SAVE", new Color(0.18f, 0.55f, 0.34f, 1f));
        SetRect(save.GetComponent<RectTransform>(), new Vector2(0.12f, 0.035f), new Vector2(0.46f, 0.105f));
        save.onClick.AddListener(RequestSaveSelectedSlot);
        Button cancel = CreateButton(box.transform, "Cancel", "CANCEL", new Color(0.10f, 0.35f, 0.48f, 1f));
        SetRect(cancel.GetComponent<RectTransform>(), new Vector2(0.54f, 0.035f), new Vector2(0.88f, 0.105f));
        cancel.onClick.AddListener(() => saveSlotPanel.SetActive(false));

        saveSlotPanel.SetActive(false);
    }

    private void RefreshSaveSlotPanel()
    {
        var manager = FXOverdose.Core.SaveLoadManager.Instance;
        if (manager == null || saveSlotPanel == null) return;
        pendingSaveSlotIndex = -1;
        if (saveNameInput != null) saveNameInput.SetTextWithoutNotify(string.Empty);
        if (saveSlotFeedback != null) saveSlotFeedback.text = "슬롯을 선택하고 저장 이름을 입력해 주세요.";
        Transform box = saveSlotPanel.transform.Find("Box");
        for (int i = 0; i < FXOverdose.Core.SaveLoadManager.MaxStorySlots; i++)
        {
            TMP_Text label = box?.Find($"SaveSlot_{i + 1}")?.GetComponentInChildren<TMP_Text>();
            if (label == null) continue;
            string savedName = manager.GetSaveName(i);
            label.text = manager.HasSave(i)
                ? $"{i + 1:00}  {(!string.IsNullOrWhiteSpace(savedName) ? savedName : "저장 데이터")}"
                : $"{i + 1:00}  EMPTY";
        }
    }

    private void SelectSaveSlot(int slotIndex)
    {
        pendingSaveSlotIndex = slotIndex;
        var manager = FXOverdose.Core.SaveLoadManager.Instance;
        string existingName = manager?.GetSaveName(slotIndex) ?? string.Empty;
        if (saveNameInput != null) saveNameInput.SetTextWithoutNotify(existingName);
        if (saveSlotFeedback != null)
            saveSlotFeedback.text = $"SLOT {slotIndex + 1:00} 선택됨" + (manager?.HasSave(slotIndex) == true ? " / 기존 데이터 있음" : string.Empty);
        saveNameInput?.ActivateInputField();
    }

    private void RequestSaveSelectedSlot()
    {
        if (pendingSaveSlotIndex < 0)
        {
            if (saveSlotFeedback != null) saveSlotFeedback.text = "먼저 저장할 슬롯을 선택해 주세요.";
            return;
        }
        if (saveNameInput == null || string.IsNullOrWhiteSpace(saveNameInput.text))
        {
            if (saveSlotFeedback != null) saveSlotFeedback.text = "저장 데이터 이름을 입력해 주세요.";
            return;
        }

        if (FXOverdose.Core.SaveLoadManager.Instance?.HasSave(pendingSaveSlotIndex) == true)
        {
            overwriteConfirmPanel.SetActive(true);
            overwriteConfirmPanel.transform.SetAsLastSibling();
        }
        else SaveGame();
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
