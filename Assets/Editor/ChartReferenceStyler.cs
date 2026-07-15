#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>실시간 차트 기능은 유지하면서 레퍼런스 이미지와 같은 픽셀 트레이딩 패널로 꾸밉니다.</summary>
public static class ChartReferenceStyler
{
    // v7: 예전 외부 가격축 여백을 제거해 캔들을 내부 가격축 바로 옆까지 확장합니다.
    private const string AppliedKey = "FXOverdose_ChartReferenceStyle_v7";

    [InitializeOnLoadMethod]
    private static void ApplyOnceAfterCompile()
    {
        EditorApplication.delayCall += TryApply;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += TryApply;
    }

    private static void TryApply()
    {
        // Play 중에는 화면 확인용으로 현재 인스턴스에 적용하고, 종료 후 다시 씬에 저장합니다.
        if (EditorApplication.isPlaying)
        {
            if (EditorSceneManager.GetActiveScene().name == "GameScene" && Find("ChartMainPanel") != null)
                Apply(false);
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorPrefs.GetBool(AppliedKey, false)) return;
        if (EditorSceneManager.GetActiveScene().name != "GameScene") return;
        if (Find("ChartMainPanel") == null) return;

        Apply(false);
        EditorSceneManager.SaveOpenScenes();
        EditorPrefs.SetBool(AppliedKey, true);
    }

    [MenuItem("Tools/FX OVERDOSE/Style Reference Trading Chart")]
    public static void ApplyFromMenu() => Apply(true);

    private static void Apply(bool showResult)
    {
        GameObject panel = Find("ChartMainPanel");
        if (panel == null) return;

        Image panelImage = GetOrAdd<Image>(panel);
        panelImage.color = new Color(0.015f, 0.045f, 0.075f, 1f);
        Outline outline = GetOrAdd<Outline>(panel);
        outline.effectColor = new Color(0.28f, 0.34f, 0.43f, 1f);
        outline.effectDistance = new Vector2(3f, -3f);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.offsetMin = new Vector2(6f, 6f);
        panelRect.offsetMax = new Vector2(-6f, -4f);

        StyleHeader(panel.transform);
        StyleChartArea(panel.transform);
        StyleAxes(panel.transform);
        if (!EditorApplication.isPlaying)
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        if (showResult)
        {
            Selection.activeGameObject = panel;
            EditorUtility.DisplayDialog("차트 스타일 적용 완료", "캔들 기능을 유지하면서 레퍼런스형 차트 UI를 적용했습니다.", "확인");
        }
    }

    private static void StyleHeader(Transform panel)
    {
        GameObject header = FindChild(panel, "ChartHeaderRow");
        if (header == null) return;

        RemoveLayoutGroups(header);
        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = Vector2.zero;
        headerRect.sizeDelta = new Vector2(0f, 110f);

        GameObject info = FindChild(header.transform, "TitlePriceBox");
        if (info != null)
        {
            RemoveLayoutGroups(info);
            SetRect(info.GetComponent<RectTransform>(), Vector2.zero, new Vector2(0.40f, 1f), new Vector2(18f, 5f), new Vector2(-8f, -5f));

            GameObject symbolRow = FindChild(info.transform, "SymbolRow");
            if (symbolRow != null)
            {
                RemoveLayoutGroups(symbolRow);
                SetRect(symbolRow.GetComponent<RectTransform>(), new Vector2(0f, 0.57f), Vector2.one, Vector2.zero, Vector2.zero);
                TMP_Text symbol = FindChild(symbolRow.transform, "Symbol")?.GetComponent<TMP_Text>();
                if (symbol != null)
                {
                    // 기본 TMP 폰트에 없는 별 문자는 네모로 보이므로 지원되는 * 문자를 사용합니다.
                    symbol.text = "<color=#F5A623>●</color>  BTC/USDT  <color=#F5B933>*</color>";
                    SetRect(symbol.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    StyleText(symbol, 25f, Color.white, TextAlignmentOptions.MidlineLeft);
                }
            }

            GameObject priceRow = FindChild(info.transform, "PriceRow");
            if (priceRow != null)
            {
                RemoveLayoutGroups(priceRow);
                SetRect(priceRow.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1f, 0.57f), Vector2.zero, Vector2.zero);
                TMP_Text price = FindChild(priceRow.transform, "PriceHeaderLabel")?.GetComponent<TMP_Text>();
                TMP_Text change = FindChild(priceRow.transform, "PriceChangeLabel")?.GetComponent<TMP_Text>();
                if (price != null)
                {
                    SetRect(price.rectTransform, new Vector2(0f, 0.36f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
                    StyleText(price, 32f, new Color(0.10f, 0.76f, 0.88f, 1f), TextAlignmentOptions.MidlineLeft);
                }
                if (change != null)
                {
                    SetRect(change.rectTransform, Vector2.zero, new Vector2(1f, 0.36f), Vector2.zero, Vector2.zero);
                    StyleText(change, 15f, new Color(0.40f, 0.84f, 0.53f, 1f), TextAlignmentOptions.MidlineLeft);
                }
            }
        }

        GameObject buttons = FindChild(header.transform, "TimeframeButtons");
        if (buttons != null)
        {
            SetRect(buttons.GetComponent<RectTransform>(), new Vector2(0.42f, 0.48f), new Vector2(0.985f, 0.94f), Vector2.zero, Vector2.zero);
            HorizontalLayoutGroup layout = GetOrAdd<HorizontalLayoutGroup>(buttons);
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 9f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            foreach (Button button in buttons.GetComponentsInChildren<Button>(true))
            {
                LayoutElement size = GetOrAdd<LayoutElement>(button.gameObject);
                size.minWidth = 58f;
                size.preferredWidth = 68f;
                size.preferredHeight = 44f;
                Image image = button.GetComponent<Image>();
                if (image != null && button.name != "Btn5m") image.color = new Color(0.07f, 0.10f, 0.16f, 1f);
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null) StyleText(label, 21f, Color.white, TextAlignmentOptions.Center);
            }
        }
    }

    private static void StyleChartArea(Transform panel)
    {
        GameObject chartArea = FindChild(panel, "ChartArea");
        if (chartArea == null) return;
        SetRect(chartArea.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(20f, 48f), new Vector2(-12f, -118f));

        for (int i = 1; i < 6; i++)
            CreateGridLine(chartArea.transform, "GridH_" + i, new Vector2(0f, i / 6f), new Vector2(1f, i / 6f));
        for (int i = 1; i < 6; i++)
            CreateGridLine(chartArea.transform, "GridV_" + i, new Vector2(i / 6f, 0f), new Vector2(i / 6f, 1f));

        GameObject currentLine = FindChild(chartArea.transform, "CurrentPriceLine");
        if (currentLine != null)
        {
            RectTransform lineRect = currentLine.GetComponent<RectTransform>();
            lineRect.anchorMin = new Vector2(0f, 0.5f);
            lineRect.anchorMax = new Vector2(1f, 0.5f);
            lineRect.offsetMin = Vector2.zero;
            lineRect.offsetMax = new Vector2(-86f, 2f);
            Image solid = currentLine.GetComponent<Image>();
            if (solid != null) solid.color = Color.clear;
            for (int i = 0; i < 24; i++)
            {
                GameObject segment = GetOrCreate(currentLine.transform, "Dash_" + i);
                Image image = GetOrAdd<Image>(segment);
                image.color = new Color(0.05f, 0.78f, 0.88f, 0.92f);
                image.raycastTarget = false;
                float start = i / 24f;
                SetRect(segment.GetComponent<RectTransform>(), new Vector2(start, 0.5f), new Vector2(start + 0.022f, 0.5f), Vector2.zero, new Vector2(0f, 2f));
            }

            GameObject tag = FindChild(currentLine.transform, "PriceTag");
            if (tag != null)
            {
                RectTransform tagRect = tag.GetComponent<RectTransform>();
                tagRect.anchorMin = new Vector2(1f, 0.5f);
                tagRect.anchorMax = new Vector2(1f, 0.5f);
                tagRect.pivot = new Vector2(0f, 0.5f);
                tagRect.anchoredPosition = new Vector2(6f, 0f);
                tagRect.sizeDelta = new Vector2(82f, 28f);
                tag.transform.SetAsLastSibling();
                TMP_Text text = tag.GetComponentInChildren<TMP_Text>(true);
                if (text != null) StyleText(text, 16f, new Color(0.02f, 0.10f, 0.14f, 1f), TextAlignmentOptions.Center);
            }
        }
    }

    private static void StyleAxes(Transform panel)
    {
        GameObject chartArea = FindChild(panel, "ChartArea");
        GameObject yAxis = FindChild(panel, "YAxisContainer");
        if (yAxis != null)
        {
            // 패널 우측 바깥 여백이 아니라 차트의 어두운 영역 안에서 축을 표시합니다.
            if (chartArea != null) yAxis.transform.SetParent(chartArea.transform, false);
            RectTransform rect = yAxis.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.offsetMin = new Vector2(-86f, 0f);
            rect.offsetMax = new Vector2(-4f, 0f);
            yAxis.transform.SetAsLastSibling();

            Image axisShade = GetOrAdd<Image>(yAxis);
            axisShade.color = new Color(0.012f, 0.035f, 0.060f, 0.92f);
            axisShade.raycastTarget = false;

            GameObject divider = GetOrCreate(yAxis.transform, "YAxisInnerDivider");
            Image dividerImage = GetOrAdd<Image>(divider);
            dividerImage.color = new Color(0.28f, 0.34f, 0.43f, 0.9f);
            dividerImage.raycastTarget = false;
            SetRect(divider.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(2f, 0f));
            divider.transform.SetAsFirstSibling();

            foreach (TMP_Text text in yAxis.GetComponentsInChildren<TMP_Text>(true))
            {
                RectTransform labelRect = text.transform.parent.GetComponent<RectTransform>();
                if (labelRect != null) labelRect.sizeDelta = new Vector2(0f, 22f);
                text.margin = new Vector4(5f, 0f, 7f, 0f);
                StyleText(text, 13f, new Color(0.72f, 0.75f, 0.80f, 1f), TextAlignmentOptions.MidlineRight);
            }

            // 현재가 태그는 가격축 위에 보여야 합니다.
            GameObject currentLine = chartArea != null ? FindChild(chartArea.transform, "CurrentPriceLine") : null;
            if (currentLine != null) currentLine.transform.SetAsLastSibling();
        }

        GameObject xAxis = FindChild(panel, "XAxisContainer");
        if (xAxis != null)
        {
            RectTransform rect = xAxis.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.offsetMin = new Vector2(46f, 6f);
            rect.offsetMax = new Vector2(-108f, 44f);
            TMP_Text[] labels = xAxis.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                StyleText(labels[i], 13f, new Color(0.68f, 0.70f, 0.74f, 1f), TextAlignmentOptions.Center);
                RectTransform labelRect = labels[i].transform.parent.GetComponent<RectTransform>();
                if (labelRect != null) labelRect.sizeDelta = new Vector2(64f, 0f);
            }
        }
    }

    private static void CreateGridLine(Transform parent, string name, Vector2 min, Vector2 max)
    {
        GameObject line = GetOrCreate(parent, name);
        Image image = GetOrAdd<Image>(line);
        image.color = new Color(0.14f, 0.20f, 0.29f, 0.38f);
        image.raycastTarget = false;
        SetRect(line.GetComponent<RectTransform>(), min, max, Vector2.zero,
            Mathf.Approximately(min.x, max.x) ? new Vector2(1f, 0f) : new Vector2(0f, 1f));
        line.transform.SetAsFirstSibling();
    }

    private static void StyleText(TMP_Text text, float size, Color color, TextAlignmentOptions alignment)
    {
        text.font = PFStardustGlobalFontApplicator.GetFont() ?? TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(10f, size - 6f);
        text.fontSizeMax = size;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
    }

    private static void SetRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
    }

    private static GameObject Find(string name) => Resources.FindObjectsOfTypeAll<GameObject>()
        .FirstOrDefault(go => go.scene.IsValid() && go.name == name);

    private static GameObject FindChild(Transform parent, string name) => parent.GetComponentsInChildren<Transform>(true)
        .FirstOrDefault(child => child.name == name)?.gameObject;

    private static GameObject GetOrCreate(Transform parent, string name)
    {
        Transform found = parent.Find(name);
        if (found != null) return found.gameObject;
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(go);
    }

    private static void RemoveLayoutGroups(GameObject go)
    {
        foreach (LayoutGroup layout in go.GetComponents<LayoutGroup>()) Undo.DestroyObjectImmediate(layout);
    }
}
#endif
