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

    private void Awake()
    {
        if (settingsButton == null) settingsButton = GetComponent<Button>();
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();

        BuildMenu();
        settingsButton?.onClick.AddListener(ToggleMenu);
        CreateFloatingModeToggleButton();
    }

    private void UpdateModeButtonVisuals()
    {
        var controller = FXOverdose.Trading.TradingController.Instance;
        if (controller == null) return;

        bool isAuto = controller.ActiveTradingMode == FXOverdose.Trading.TradingController.TradingMode.AI_Auto;
        string popupLabel = isAuto ? "모드: ⚡ AI 자동" : "모드: 🎮 수동 매매";
        string floatLabel = isAuto ? "⚡\nAI" : "🎮\n수동";
        Color btnColor = isAuto ? new Color(0.12f, 0.48f, 0.72f, 1f) : new Color(0.75f, 0.35f, 0.08f, 1f);

        if (popupModeText != null) popupModeText.text = popupLabel;
        if (floatingModeText != null) floatingModeText.text = floatLabel;
        if (popupModeImage != null) popupModeImage.color = btnColor;
        if (floatingModeImage != null) floatingModeImage.color = btnColor;
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
        rect.sizeDelta = new Vector2(44f, 44f);
        rect.anchoredPosition = new Vector2(-15f, -65f);

        floatingModeImage = go.GetComponent<Image>();
        floatingModeImage.color = new Color(0.12f, 0.48f, 0.72f, 0.95f);

        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = floatingModeImage;

        floatingModeText = CreateText(go.transform, "Label", "⚡\nAI", 11f, TextAlignmentOptions.Center);
        floatingModeText.textWrappingMode = TextWrappingModes.Normal;
        floatingModeText.fontSizeMin = 8f;
        floatingModeText.fontSizeMax = 11f;
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
        if (settingsButton == null || floatingModeImage == null) return;
        RectTransform settingsRect = settingsButton.GetComponent<RectTransform>();
        RectTransform myRect = floatingModeImage.GetComponent<RectTransform>();
        if (settingsRect == null || myRect == null) return;

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null) return;

        // 설정 버튼(SettingsIcon)의 실제 월드 모서리를 가져와 캔버스 내부 정확한 픽셀 크기 및 위치 계산
        Vector3[] corners = new Vector3[4];
        settingsRect.GetWorldCorners(corners);

        Vector3 localBottomLeft = parentCanvas.transform.InverseTransformPoint(corners[0]);
        Vector3 localTopLeft = parentCanvas.transform.InverseTransformPoint(corners[1]);
        Vector3 localTopRight = parentCanvas.transform.InverseTransformPoint(corners[2]);
        Vector3 localBottomRight = parentCanvas.transform.InverseTransformPoint(corners[3]);

        float width = Mathf.Abs(localTopRight.x - localTopLeft.x);
        float height = Mathf.Abs(localTopLeft.y - localBottomLeft.y);

        if (width <= 5f) width = 44f;
        if (height <= 5f) height = 44f;

        myRect.anchorMin = new Vector2(1f, 1f);
        myRect.anchorMax = new Vector2(1f, 1f);
        myRect.pivot = new Vector2(1f, 1f);
        myRect.sizeDelta = new Vector2(width, height);

        // myRect(피벗이 우측상단 1,1)를 설정 버튼의 우측하단(localBottomRight) 바로 아래에 6px 간격으로 배치
        myRect.localPosition = localBottomRight + new Vector3(0f, -6f, 0f);
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
        SetRect(modeSwitchButton.GetComponent<RectTransform>(), new Vector2(0.26f, 0.46f), new Vector2(0.74f, 0.55f));
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

        Button resumeButton = CreateButton(inner.transform, "ResumeButton", "CONTINUE", new Color(0.05f, 0.46f, 0.58f, 1f));
        SetRect(resumeButton.GetComponent<RectTransform>(), new Vector2(0.13f, 0.25f), new Vector2(0.87f, 0.40f));
        resumeButton.onClick.AddListener(CloseMenu);

        Button quitButton = CreateButton(inner.transform, "QuitButton", "QUIT GAME", new Color(0.60f, 0.15f, 0.22f, 1f));
        SetRect(quitButton.GetComponent<RectTransform>(), new Vector2(0.13f, 0.06f), new Vector2(0.87f, 0.21f));
        quitButton.onClick.AddListener(QuitGame);

        overlay.SetActive(false);
    }

    private Button CreateButton(Transform parent, string objectName, string label, Color color)
    {
        GameObject go = CreateUIObject(objectName, parent, typeof(Button));
        Image image = go.GetComponent<Image>();
        image.color = color;

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
