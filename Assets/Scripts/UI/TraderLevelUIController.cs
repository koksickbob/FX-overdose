using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FXOverdose.Trading;

/// <summary>캐릭터 머리 위에 주인공 레벨과 경험치만 간결하게 표시합니다.</summary>
public sealed class TraderLevelUIController : MonoBehaviour
{
    private TraderLevelSystem levelSystem;
    private TMP_Text levelText;
    private TMP_Text expText;
    private Slider expSlider;

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
        Transform old = transform.Find("CharacterLevelExpHUD");
        if (old != null) Destroy(old.gameObject);

        GameObject hud = new("CharacterLevelExpHUD", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        hud.transform.SetParent(transform, false);

        RectTransform hudRect = hud.GetComponent<RectTransform>();
        hudRect.anchorMin = hudRect.anchorMax = new Vector2(0.5f, 1f);
        hudRect.pivot = new Vector2(0.5f, 0f);
        hudRect.sizeDelta = new Vector2(390f, 60f);
        hudRect.anchoredPosition = new Vector2(0f, 14f);

        Image frame = hud.GetComponent<Image>();
        frame.sprite = Resources.Load<Sprite>("UI/CharacterLevelExpFrame");
        // 캐릭터 머리 위 공간을 덜 차지하도록 원본보다 세로를 살짝 낮춰 표시합니다.
        frame.preserveAspect = false;
        frame.raycastTarget = false;

        levelText = CreateText(hud.transform, "LevelText", "LV.1", 19f, TextAlignmentOptions.Center);
        SetRect(levelText.rectTransform, new Vector2(0.035f, 0.16f), new Vector2(0.27f, 0.84f));
        levelText.color = new Color32(207, 250, 254, 255); // #CFFAFE
        levelText.fontStyle = FontStyles.Bold;

        expText = CreateText(hud.transform, "ExpText", "EXP  0 / 100", 13f, TextAlignmentOptions.Center);
        SetRect(expText.rectTransform, new Vector2(0.31f, 0.52f), new Vector2(0.94f, 0.82f));
        expText.color = new Color32(207, 250, 254, 255); // #CFFAFE

        expSlider = CreateExperienceBar(hud.transform);
        SetRect(expSlider.GetComponent<RectTransform>(), new Vector2(0.31f, 0.25f), new Vector2(0.94f, 0.47f));
    }

    private void Refresh()
    {
        if (levelSystem == null) levelSystem = TraderLevelSystem.Instance;
        if (levelSystem == null || levelText == null || expText == null || expSlider == null) return;

        float maxExp = levelSystem.GetMaxProtagonistEXP(levelSystem.ProtagonistLevel);
        levelText.text = $"LV.{levelSystem.ProtagonistLevel}";
        expText.text = $"EXP  {levelSystem.ProtagonistEXP:N0} / {maxExp:N0}";
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

/// <summary>GameScene의 캐릭터 이미지에 소형 레벨 HUD를 자동 부착합니다.</summary>
public static class TraderLevelUIBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (SceneManager.GetActiveScene().name != "GameScene") return;

        GameObject character = GameObject.Find("ProtagonistCharacterImage");
        if (character == null || character.GetComponent<TraderLevelUIController>() != null) return;
        character.AddComponent<TraderLevelUIController>();
    }
}
