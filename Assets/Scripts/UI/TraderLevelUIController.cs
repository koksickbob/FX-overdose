using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FXOverdose.Trading;

/// <summary>캐릭터 머리 위에 주인공 레벨과 경험치만 간결하게 표시합니다.</summary>
public sealed class TraderLevelUIController : MonoBehaviour
{
    public RectTransform TutorialLevelHighlightTarget => levelHudRect;

    private const float BaseHudWidth = 280f;
    private const float BaseHudHeight = UIStrokeStyle.CompactHudHeight;
    private const float HudWidthScale = 1.3f;
    private const float VerticalExpansionPerSide = 0f;

    private TraderLevelSystem levelSystem;
    private TMP_Text levelText;
    private TMP_Text expText;
    private Slider expSlider;
    private RectTransform levelHudRect;

    private void Awake()
    {
        EnsureLevelSystem();
        BuildCompactHud();
    }

    private void OnEnable()
    {
        StartCoroutine(SubscribeWhenReady());
    }

    private IEnumerator SubscribeWhenReady()
    {
        yield return null;
        levelSystem = TraderLevelSystem.Instance;
        if (levelSystem == null) yield break;

        levelSystem.OnProtagonistLevelChanged -= OnLevelChanged;
        levelSystem.OnProtagonistLevelChanged += OnLevelChanged;
        levelSystem.OnProtagonistLeveledUp -= OnLeveledUp;
        levelSystem.OnProtagonistLeveledUp += OnLeveledUp;
        Refresh();
    }

    private void OnDisable()
    {
        if (levelSystem == null) return;
        levelSystem.OnProtagonistLevelChanged -= OnLevelChanged;
        levelSystem.OnProtagonistLeveledUp -= OnLeveledUp;
    }

    private void EnsureLevelSystem()
    {
        levelSystem = TraderLevelSystem.Instance;
        if (levelSystem != null) return;

        GameObject host = GameObject.Find("TraderLevelSystem") ?? new GameObject("TraderLevelSystem");
        levelSystem = host.GetComponent<TraderLevelSystem>() ?? host.AddComponent<TraderLevelSystem>();
    }

    private void BuildCompactHud()
    {
        GameObject old = GameObject.Find("CharacterLevelExpHUD");
        if (old != null) Destroy(old);

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        Transform hudParent = parentCanvas != null ? parentCanvas.transform : transform;

        GameObject hud = new("CharacterLevelExpHUD", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        hud.transform.SetParent(hudParent, false);

        levelHudRect = hud.GetComponent<RectTransform>();
        levelHudRect.anchorMin = levelHudRect.anchorMax = new Vector2(1f, 1f);
        levelHudRect.pivot = new Vector2(0f, 1f);
        levelHudRect.sizeDelta = new Vector2(
            BaseHudWidth * HudWidthScale,
            BaseHudHeight + VerticalExpansionPerSide * 2f);
        levelHudRect.anchoredPosition = new Vector2(-394f, -65f + VerticalExpansionPerSide);

        Image frame = hud.GetComponent<Image>();
        // AI/USER 버튼과 동일한 배경색과 Unity Outline을 사용해 선 두께를 정확히 통일합니다.
        frame.sprite = null;
        frame.color = new Color32(20, 29, 51, 255); // #141D33
        frame.raycastTarget = false;

        Outline frameOutline = hud.AddComponent<Outline>();
        frameOutline.effectColor = new Color32(6, 182, 212, 255); // #06B6D4
        frameOutline.effectDistance = UIStrokeStyle.EffectDistance;
        frameOutline.useGraphicAlpha = true;

        Image divider = CreateImage(hud.transform, "LevelDivider");
        RectTransform dividerRect = divider.rectTransform;
        dividerRect.anchorMin = new Vector2(0.28f, 0.12f);
        dividerRect.anchorMax = new Vector2(0.28f, 0.88f);
        dividerRect.pivot = new Vector2(0.5f, 0.5f);
        dividerRect.anchoredPosition = Vector2.zero;
        dividerRect.sizeDelta = new Vector2(UIStrokeStyle.Width, 0f);
        divider.color = new Color32(6, 182, 212, 255);

        levelText = CreateText(hud.transform, "LevelText", "LV.1", 19f, TextAlignmentOptions.Center);
        SetRect(levelText.rectTransform, new Vector2(0.035f, 0.16f), new Vector2(0.27f, 0.84f));
        levelText.color = new Color32(207, 250, 254, 255); // #CFFAFE
        GlobalPFStardustFont.ConfigureCompactHudText(levelText, null, 19f, 0.04f);

        expText = CreateText(hud.transform, "ExpText", "EXP  0 / 100", 13f, TextAlignmentOptions.Center);
        SetRect(expText.rectTransform, new Vector2(0.31f, 0.52f), new Vector2(0.94f, 0.82f));
        expText.color = new Color32(207, 250, 254, 255); // #CFFAFE
        GlobalPFStardustFont.ConfigureCompactHudText(expText, null, 13f);

        expSlider = CreateExperienceBar(hud.transform);
        SetRect(expSlider.GetComponent<RectTransform>(), new Vector2(0.31f, 0.25f), new Vector2(0.94f, 0.47f));

        if (parentCanvas != null)
            StartCoroutine(AlignBesideModeButton(parentCanvas));
    }

    private IEnumerator AlignBesideModeButton(Canvas parentCanvas)
    {
        // SettingsMenuController의 동적 버튼 생성/차트 기준 재배치가 끝날 때까지 기다립니다.
        RectTransform modeRect = null;
        for (int attempt = 0; attempt < 10 && modeRect == null; attempt++)
        {
            yield return null;
            modeRect = GameObject.Find("Temp_TradingModeToggleBtn")?.GetComponent<RectTransform>();
        }

        if (modeRect == null || levelHudRect == null) yield break;

        yield return null;
        Canvas.ForceUpdateCanvases();

        Vector3[] modeCorners = new Vector3[4];
        modeRect.GetWorldCorners(modeCorners);
        Vector3 modeTopRight = parentCanvas.transform.InverseTransformPoint(modeCorners[2]);

        const float gap = 10f;
        const float edgeMargin = 10f;
        const float hudWidth = BaseHudWidth * HudWidthScale;
        float hudHeight = UIStrokeStyle.CompactHudHeight + VerticalExpansionPerSide * 2f;
        levelHudRect.anchorMin = levelHudRect.anchorMax = new Vector2(0.5f, 0.5f);
        levelHudRect.pivot = new Vector2(0f, 1f);
        levelHudRect.sizeDelta = new Vector2(hudWidth, hudHeight);

        RectTransform canvasRect = parentCanvas.GetComponent<RectTransform>();
        float x = modeTopRight.x + gap;
        if (canvasRect != null)
            x = Mathf.Min(x, canvasRect.rect.xMax - hudWidth - edgeMargin);

        levelHudRect.localPosition = new Vector3(x, modeTopRight.y + VerticalExpansionPerSide, 0f);
    }

    private void Refresh()
    {
        if (levelSystem == null) levelSystem = TraderLevelSystem.Instance;
        if (levelSystem == null || levelText == null || expText == null || expSlider == null) return;

        float maxExp = levelSystem.GetMaxProtagonistEXP(levelSystem.ProtagonistLevel);
        levelText.text = $"LV.{levelSystem.ProtagonistLevel}";
        expText.text = $"EXP  {levelSystem.ProtagonistEXP:N0} / {maxExp:N0}";
        GlobalPFStardustFont.RefreshCompactHudText(levelText);
        GlobalPFStardustFont.RefreshCompactHudText(expText);
        expSlider.minValue = 0f;
        expSlider.maxValue = Mathf.Max(1f, maxExp);
        expSlider.value = levelSystem.ProtagonistEXP;
    }

    private void OnLevelChanged(int level, float currentExp, float maxExp) => Refresh();
    private void OnLeveledUp(int level) => Refresh();

    private static Slider CreateExperienceBar(Transform parent)
    {
        GameObject root = new("ExperienceBar", typeof(RectTransform), typeof(Slider));
        root.transform.SetParent(parent, false);

        Image background = CreateImage(root.transform, "Background");
        Stretch(background.rectTransform);
        background.color = new Color32(20, 29, 51, 235); // #141D33

        GameObject fillArea = new("FillArea", typeof(RectTransform));
        fillArea.transform.SetParent(root.transform, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        Stretch(fillAreaRect);
        fillAreaRect.offsetMin = new Vector2(3f, 3f);
        fillAreaRect.offsetMax = new Vector2(-3f, -3f);

        Image fill = CreateImage(fillArea.transform, "Fill");
        Stretch(fill.rectTransform);
        fill.color = new Color32(6, 182, 212, 255); // #06B6D4

        Slider slider = root.GetComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.targetGraphic = background;
        slider.direction = Slider.Direction.LeftToRight;
        slider.interactable = false;
        return slider;
    }

    private static TMP_Text CreateText(Transform parent, string name, string value, float size, TextAlignmentOptions alignment)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.enableAutoSizing = true;
        text.fontSizeMin = 11f;
        text.fontSizeMax = size;
        return text;
    }

    private static Image CreateImage(Transform parent, string name)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}

/// <summary>게임 및 튜토리얼 씬의 캐릭터 이미지에 소형 레벨 HUD를 자동 부착합니다.</summary>
public static class TraderLevelUIBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneCallback()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "GameScene" &&
            !string.Equals(scene.name, "tutorial", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Install(scene);
    }

    private static void Install(Scene gameScene)
    {
        GameObject character = null;
        foreach (GameObject root in gameScene.GetRootGameObjects())
        {
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child.name != "ProtagonistCharacterImage") continue;
                character = child.gameObject;
                break;
            }

            if (character != null) break;
        }

        if (character == null || character.GetComponent<TraderLevelUIController>() != null) return;
        character.AddComponent<TraderLevelUIController>();
        Debug.Log("[TraderLevelUI] GameScene 캐릭터에 레벨/EXP UI 복구 완료");
    }
}
