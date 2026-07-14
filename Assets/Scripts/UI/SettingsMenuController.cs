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

    private void Awake()
    {
        if (settingsButton == null) settingsButton = GetComponent<Button>();
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();

        BuildMenu();
        settingsButton?.onClick.AddListener(ToggleMenu);
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
        panelRect.sizeDelta = new Vector2(570f, 460f);
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
        SetRect(title.rectTransform, new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.94f));
        title.color = new Color(0.55f, 0.90f, 1f, 1f);

        TMP_Text paused = CreateText(inner.transform, "PausedLabel", "GAME PAUSED", 23f, TextAlignmentOptions.Center);
        SetRect(paused.rectTransform, new Vector2(0.08f, 0.58f), new Vector2(0.92f, 0.72f));
        paused.color = new Color(0.75f, 0.80f, 0.90f, 1f);

        Button resumeButton = CreateButton(inner.transform, "ResumeButton", "CONTINUE", new Color(0.05f, 0.46f, 0.58f, 1f));
        SetRect(resumeButton.GetComponent<RectTransform>(), new Vector2(0.13f, 0.34f), new Vector2(0.87f, 0.52f));
        resumeButton.onClick.AddListener(CloseMenu);

        Button quitButton = CreateButton(inner.transform, "QuitButton", "QUIT GAME", new Color(0.60f, 0.15f, 0.22f, 1f));
        SetRect(quitButton.GetComponent<RectTransform>(), new Vector2(0.13f, 0.11f), new Vector2(0.87f, 0.29f));
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
