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
    private const string AppliedKey = "FXOverdose_BottomTradingReferenceStyle_PositionStatus_v10";

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
        if (EditorApplication.isPlaying)
        {
            if (EditorSceneManager.GetActiveScene().name == "GameScene" && Find("BottomTradingPanel") != null)
                Apply(false);
            return;
        }
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
        panelImage.color = new Color(0.018f, 0.035f, 0.060f, 0f);

        StyleTradeCard(Find("LongButtonCard"), new Vector2(0f, 0.06f), new Vector2(0.33f, 0.95f), true);
        StyleTradeCard(Find("ShortButtonCard"), new Vector2(0.33f, 0.06f), new Vector2(0.66f, 0.95f), false);
        StyleLeverageCard(Find("ControlBoxCard"));
        StylePositionStatusPanel();
        StyleSellButton(panel);
        StyleAllButtons(panel);

        GameObject chart = Find("ChartMainPanel");
        if (chart != null)
            SetRect(chart.GetComponent<RectTransform>(), new Vector2(0f, 0.30f), new Vector2(1f, 0.90f), new Vector2(12f, 5f), new Vector2(-6f, -8f));

        if (!EditorApplication.isPlaying)
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        if (showResult)
            EditorUtility.DisplayDialog("주문 UI 적용 완료", "LONG / SHORT / LEVERAGE 카드 비율을 적용했습니다.", "확인");
    }

    private static void StylePositionStatusPanel()
    {
        GameObject status = Find("PositionStatusPanel");
        if (status == null) return;

        // 레버리지 카드 내부에 동일한 상하 여백을 두고 상태 텍스트 전체를 중앙 정렬합니다.
        SetRect(status.GetComponent<RectTransform>(), new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f), Vector2.zero, Vector2.zero);

        Image background = GetOrAdd<Image>(status);
        background.color = Color.clear;
        background.raycastTarget = false;

        VerticalLayoutGroup layout = GetOrAdd<VerticalLayoutGroup>(status);
        layout.padding = new RectOffset(14, 14, 14, 14);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        foreach (TMP_Text text in status.GetComponentsInChildren<TMP_Text>(true))
        {
            text.font = PFStardustGlobalFontApplicator.GetFont() ?? TMP_Settings.defaultFontAsset;
            text.enableAutoSizing = true;
            text.fontSizeMin = 9f;
            text.fontSizeMax = Mathf.Max(14f, text.fontSize);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.alignment = TextAlignmentOptions.Center;
            text.margin = Vector4.zero;
            text.raycastTarget = false;
        }
    }

    private static void StyleAllButtons(GameObject panel)
    {
        foreach (Button button in panel.GetComponentsInChildren<Button>(true))
        {
            Outline outline = button.GetComponent<Outline>();
            if (outline == null)
            {
                outline = GetOrAdd<Outline>(button.gameObject);
                outline.effectColor = UIStrokeStyle.DefaultColor;
                outline.useGraphicAlpha = true;
            }
            outline.effectDistance = UIStrokeStyle.EffectDistance;
        }
    }

    private static void StyleSellButton(GameObject panel)
    {
        GameObject sellObject = Find("ClosePositionButton");
        if (panel == null || sellObject == null) return;

        sellObject.transform.SetParent(panel.transform, false);
        sellObject.transform.SetAsLastSibling();
        RemoveLayouts(sellObject);
        SetRect(
            sellObject.GetComponent<RectTransform>(),
            new Vector2(0f, 0.06f),
            new Vector2(0.66f, 0.95f),
            new Vector2(12f, 5f),
            new Vector2(-4f, -5f));

        Image image = GetOrAdd<Image>(sellObject);
        image.color = new Color(0.64f, 0.08f, 0.14f, 1f);
        Outline outline = GetOrAdd<Outline>(sellObject);
        outline.effectColor = new Color(1f, 0.34f, 0.40f, 1f);
        outline.effectDistance = UIStrokeStyle.EffectDistance;

        Button button = GetOrAdd<Button>(sellObject);
        button.targetGraphic = image;

        TMP_Text label = sellObject.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = "▼ 포지션 매도\n<size=55%>현재 포지션 정리</size>";
            SetRect(label.rectTransform, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.92f), Vector2.zero, Vector2.zero);
            StyleText(label, 42f, Color.white);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
        }

        if (!EditorApplication.isPlaying) sellObject.SetActive(false);
    }

    private static void StyleTradeCard(GameObject card, Vector2 min, Vector2 max, bool isLong)
    {
        if (card == null) return;
        RemoveLayouts(card);
        Vector2 offsetMin = new(isLong ? 12f : 4f, 5f);
        SetRect(card.GetComponent<RectTransform>(), min, max, offsetMin, new Vector2(-4f, -5f));

        Color baseColor = isLong ? new Color(0.10f, 0.54f, 0.23f, 1f) : new Color(0.58f, 0.13f, 0.20f, 1f);
        Color borderColor = isLong ? new Color(0.45f, 1f, 0.39f, 1f) : new Color(1f, 0.35f, 0.42f, 1f);
        Image image = GetOrAdd<Image>(card);
        image.color = baseColor;
        Outline outline = GetOrAdd<Outline>(card);
        outline.effectColor = borderColor;
        outline.effectDistance = UIStrokeStyle.EffectDistance;

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
            pillOutline.effectDistance = UIStrokeStyle.EffectDistance;
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
        // 차트의 우측 offsetMax.x(-6px)와 동일하게 맞춰 수직 우측선이 이어지도록 합니다.
        SetRect(card.GetComponent<RectTransform>(), new Vector2(0.66f, 0.06f), new Vector2(1f, 0.95f), new Vector2(4f, 5f), new Vector2(-6f, -5f));
        Image image = GetOrAdd<Image>(card);
        image.color = new Color(0.035f, 0.060f, 0.105f, 1f);
        Outline outline = GetOrAdd<Outline>(card);
        outline.effectColor = new Color(0.25f, 0.31f, 0.40f, 1f);
        outline.effectDistance = UIStrokeStyle.EffectDistance;

        GameObject tabs = Find("TabsBar");
        if (tabs != null)
        {
            RemoveLayouts(tabs);
            SetRect(tabs.GetComponent<RectTransform>(), new Vector2(0.05f, 0.76f), new Vector2(0.95f, 0.94f), Vector2.zero, Vector2.zero);
        }
        GameObject leverageTab = Find("BtnTabLeverageMode");
        if (leverageTab != null)
        {
            leverageTab.SetActive(true);
            SetRect(leverageTab.GetComponent<RectTransform>(), Vector2.zero, new Vector2(0.49f, 1f), Vector2.zero, Vector2.zero);
            TMP_Text text = leverageTab.GetComponentInChildren<TMP_Text>(true);
            if (text != null) { text.text = "LEVERAGE"; StyleText(text, 18f, Color.white); }
        }
        GameObject marginTab = Find("BtnTabMarginRatioMode");
        if (marginTab != null)
        {
            marginTab.SetActive(true);
            SetRect(marginTab.GetComponent<RectTransform>(), new Vector2(0.51f, 0f), Vector2.one, Vector2.zero, Vector2.zero);
            TMP_Text text = marginTab.GetComponentInChildren<TMP_Text>(true);
            if (text != null) { text.text = "MARGIN"; StyleText(text, 18f, Color.white); }
        }

        GameObject container = Find("Container_LeverageMode");
        if (container != null)
        {
            RemoveLayouts(container);
            SetRect(container.GetComponent<RectTransform>(), new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.715f), Vector2.zero, Vector2.zero);
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

        StyleMarginControls();
    }

    private static void StyleMarginControls()
    {
        GameObject container = Find("Container_MarginRatioMode");
        if (container != null)
        {
            RemoveLayouts(container);
            SetRect(container.GetComponent<RectTransform>(), new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.715f), Vector2.zero, Vector2.zero);
        }

        GameObject box = Find("MarginRatioBox");
        if (box != null)
        {
            RemoveLayouts(box);
            SetRect(box.GetComponent<RectTransform>(), new Vector2(0f, 0.46f), Vector2.one, Vector2.zero, Vector2.zero);
        }
        PlaceButton("BtnMarMinus", new Vector2(0f, 0.08f), new Vector2(0.25f, 0.92f), 18f);
        PlaceButton("BtnMarPlus", new Vector2(0.75f, 0.08f), new Vector2(1f, 0.92f), 18f);

        TMP_Text display = Find("MarDisplay")?.GetComponent<TMP_Text>();
        if (display != null)
        {
            SetRect(display.rectTransform, new Vector2(0.27f, 0.05f), new Vector2(0.73f, 0.95f), Vector2.zero, Vector2.zero);
            StyleText(display, 22f, Color.white);
        }

        GameObject presets = Find("MarPresets");
        if (presets != null)
        {
            SetRect(presets.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1f, 0.39f), Vector2.zero, Vector2.zero);
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
                if (label != null) StyleText(label, 15f, Color.white);
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
        text.font = PFStardustGlobalFontApplicator.GetFont() ?? TMP_Settings.defaultFontAsset;
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
        if (component != null) return component;
        return EditorApplication.isPlaying ? go.AddComponent<T>() : Undo.AddComponent<T>(go);
    }

    private static void RemoveLayouts(GameObject go)
    {
        foreach (LayoutGroup layout in go.GetComponents<LayoutGroup>())
        {
            if (EditorApplication.isPlaying) Object.DestroyImmediate(layout);
            else Undo.DestroyObjectImmediate(layout);
        }
    }
}
#endif
