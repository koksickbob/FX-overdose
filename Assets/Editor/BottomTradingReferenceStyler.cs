#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>하단 주문부를 LONG / SHORT / LEVERAGE 3카드 레이아웃으로 정리합니다.</summary>
public static class BottomTradingReferenceStyler
{
    private const string AppliedKey = "FXOverdose_BottomTradingReferenceStyle_v2";

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.delayCall += TryApply;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += TryApply;
    }

    private static void TryApply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorPrefs.GetBool(AppliedKey, false)) return;
        if (EditorSceneManager.GetActiveScene().name != "GameScene") return;
        if (Find("BottomTradingPanel") == null) return;
        Apply(false);
        EditorSceneManager.SaveOpenScenes();
        EditorPrefs.SetBool(AppliedKey, true);
    }

    [MenuItem("Tools/FX OVERDOSE/Style Long Short Leverage Panel")]
    public static void ApplyFromMenu() => Apply(true);

    private static void Apply(bool showResult)
    {
        GameObject panel = Find("BottomTradingPanel");
        if (panel == null) return;

        RemoveLayouts(panel);
        SetRect(panel.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1f, 0.30f), Vector2.zero, Vector2.zero);
        Image panelImage = GetOrAdd<Image>(panel);
        panelImage.color = new Color(0.018f, 0.035f, 0.060f, 1f);

        StyleTradeCard(Find("LongButtonCard"), new Vector2(0.015f, 0.06f), new Vector2(0.325f, 0.95f), true);
        StyleTradeCard(Find("ShortButtonCard"), new Vector2(0.345f, 0.06f), new Vector2(0.655f, 0.95f), false);
        StyleLeverageCard(Find("ControlBoxCard"));

        GameObject chart = Find("ChartMainPanel");
        if (chart != null)
            SetRect(chart.GetComponent<RectTransform>(), new Vector2(0f, 0.30f), new Vector2(1f, 0.92f), new Vector2(5f, 5f), new Vector2(-5f, -5f));

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        if (showResult)
            EditorUtility.DisplayDialog("주문 UI 적용 완료", "LONG / SHORT / LEVERAGE 카드 비율을 적용했습니다.", "확인");
    }

    private static void StyleTradeCard(GameObject card, Vector2 min, Vector2 max, bool isLong)
    {
        if (card == null) return;
        RemoveLayouts(card);
        SetRect(card.GetComponent<RectTransform>(), min, max, new Vector2(5f, 5f), new Vector2(-5f, -5f));

        Color baseColor = isLong ? new Color(0.10f, 0.54f, 0.23f, 1f) : new Color(0.58f, 0.13f, 0.20f, 1f);
        Color borderColor = isLong ? new Color(0.45f, 1f, 0.39f, 1f) : new Color(1f, 0.35f, 0.42f, 1f);
        Image image = GetOrAdd<Image>(card);
        image.color = baseColor;
        Outline outline = GetOrAdd<Outline>(card);
        outline.effectColor = borderColor;
        outline.effectDistance = new Vector2(4f, -4f);

        TMP_Text title = Find(isLong ? "LongTitle" : "ShortTitle")?.GetComponent<TMP_Text>();
        if (title != null)
        {
            title.text = isLong ? "▲ LONG" : "▼ SHORT";
            SetRect(title.rectTransform, new Vector2(0.06f, 0.42f), new Vector2(0.94f, 0.90f), Vector2.zero, Vector2.zero);
            StyleText(title, 43f, Color.white);
        }

        GameObject pill = Find(isLong ? "LongPill" : "ShortPill");
        TMP_Text subtitle = pill != null ? pill.GetComponentInChildren<TMP_Text>(true) : null;
        if (pill != null)
        {
            SetRect(pill.GetComponent<RectTransform>(), new Vector2(0.10f, 0.12f), new Vector2(0.90f, 0.39f), Vector2.zero, Vector2.zero);
            Image pillImage = GetOrAdd<Image>(pill);
            pillImage.color = isLong ? new Color(0.07f, 0.38f, 0.17f, 0.72f) : new Color(0.42f, 0.08f, 0.14f, 0.72f);
            Outline pillOutline = GetOrAdd<Outline>(pill);
            pillOutline.effectColor = isLong ? new Color(0.35f, 0.80f, 0.31f, 0.75f) : new Color(0.84f, 0.25f, 0.31f, 0.75f);
            pillOutline.effectDistance = new Vector2(2f, -2f);
        }
        if (subtitle != null)
        {
            subtitle.text = isLong ? "Tap to Open Long" : "Tap to Open Short";
            SetRect(subtitle.rectTransform, Vector2.zero, Vector2.one, new Vector2(5f, 3f), new Vector2(-5f, -3f));
            StyleText(subtitle, 20f, new Color(1f, 0.92f, 0.84f, 1f));
        }
    }

    private static void StyleLeverageCard(GameObject card)
    {
        if (card == null) return;
        RemoveLayouts(card);
        SetRect(card.GetComponent<RectTransform>(), new Vector2(0.675f, 0.06f), new Vector2(0.985f, 0.95f), new Vector2(5f, 5f), new Vector2(-5f, -5f));
        Image image = GetOrAdd<Image>(card);
        image.color = new Color(0.035f, 0.060f, 0.105f, 1f);
        Outline outline = GetOrAdd<Outline>(card);
        outline.effectColor = new Color(0.25f, 0.31f, 0.40f, 1f);
        outline.effectDistance = new Vector2(3f, -3f);

        GameObject tabs = Find("TabsBar");
        if (tabs != null)
        {
            RemoveLayouts(tabs);
            SetRect(tabs.GetComponent<RectTransform>(), new Vector2(0.08f, 0.77f), new Vector2(0.92f, 0.98f), Vector2.zero, Vector2.zero);
        }
        GameObject leverageTab = Find("BtnTabLeverageMode");
        if (leverageTab != null)
        {
            Image tabImage = leverageTab.GetComponent<Image>();
            if (tabImage != null) tabImage.color = Color.clear;
            SetRect(leverageTab.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            TMP_Text text = leverageTab.GetComponentInChildren<TMP_Text>(true);
            if (text != null) { text.text = "LEVERAGE"; StyleText(text, 23f, Color.white); }
        }
        GameObject marginTab = Find("BtnTabMarginRatioMode");
        if (marginTab != null) marginTab.SetActive(false);

        GameObject container = Find("Container_LeverageMode");
        if (container != null)
        {
            RemoveLayouts(container);
            SetRect(container.GetComponent<RectTransform>(), new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.78f), Vector2.zero, Vector2.zero);
        }

        GameObject box = Find("LeverageBox");
        if (box != null)
        {
            RemoveLayouts(box);
            SetRect(box.GetComponent<RectTransform>(), new Vector2(0f, 0.46f), Vector2.one, Vector2.zero, Vector2.zero);
        }
        PlaceButton("BtnLevMinus", new Vector2(0f, 0.08f), new Vector2(0.23f, 0.92f), 29f);
        PlaceButton("BtnLevPlus", new Vector2(0.77f, 0.08f), new Vector2(1f, 0.92f), 29f);

        TMP_Text display = Find("LevDisplay")?.GetComponent<TMP_Text>();
        if (display != null)
        {
            SetRect(display.rectTransform, new Vector2(0.27f, 0.05f), new Vector2(0.73f, 0.95f), Vector2.zero, Vector2.zero);
            StyleText(display, 38f, Color.white);
        }

        GameObject presets = Find("LevPresets");
        if (presets != null)
        {
            SetRect(presets.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0.39f), Vector2.zero, Vector2.zero);
            HorizontalLayoutGroup layout = GetOrAdd<HorizontalLayoutGroup>(presets);
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.spacing = 3f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            foreach (Button button in presets.GetComponentsInChildren<Button>(true))
            {
                LayoutElement element = GetOrAdd<LayoutElement>(button.gameObject);
                element.minWidth = 0f;
                element.preferredWidth = 52f;
                element.flexibleWidth = 1f;
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null) StyleText(label, 16f, Color.white);
            }
        }
    }

    private static void PlaceButton(string name, Vector2 min, Vector2 max, float fontSize)
    {
        GameObject go = Find(name);
        if (go == null) return;
        SetRect(go.GetComponent<RectTransform>(), min, max, Vector2.zero, Vector2.zero);
        TMP_Text text = go.GetComponentInChildren<TMP_Text>(true);
        if (text != null) StyleText(text, fontSize, Color.white);
    }

    private static void StyleText(TMP_Text text, float size, Color color)
    {
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.fontSizeMin = Mathf.Max(11f, size - 8f);
        text.fontSizeMax = size;
        text.enableAutoSizing = true;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.raycastTarget = false;
    }

    private static void SetRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        if (rect == null) return;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
    }

    private static GameObject Find(string name) => Resources.FindObjectsOfTypeAll<GameObject>()
        .FirstOrDefault(go => go.scene.IsValid() && go.name == name);

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(go);
    }

    private static void RemoveLayouts(GameObject go)
    {
        foreach (LayoutGroup layout in go.GetComponents<LayoutGroup>()) Undo.DestroyObjectImmediate(layout);
    }
}
#endif
