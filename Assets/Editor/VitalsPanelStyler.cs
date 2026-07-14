#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>기존 HP/Mental 슬라이더를 픽셀 상태 패널 안으로 옮기고 연결을 유지합니다.</summary>
public static class VitalsPanelStyler
{
    private const string FramePath = "Assets/Img/VitalsPanelFrame.png";
    private const string HeartPath = "Assets/Img/HealthHeartIcon.png";
    private const string BrainPath = "Assets/Img/MentalBrainIcon.png";
    private const string FontPath = "Assets/Fonts/PFStardustBold Dynamic SDF.asset";
    private const string AppliedKey = "FXOverdose_VitalsPanelStyle_v7";

    [InitializeOnLoadMethod]
    private static void ApplyOnceAfterCompile()
    {
        EditorApplication.delayCall += TryApplyOnce;
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
    }

    private static void HandlePlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += TryApplyOnce;
    }

    private static void TryApplyOnce()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorPrefs.GetBool(AppliedKey, false)) return;
        if (EditorSceneManager.GetActiveScene().name != "GameScene") return;
        if (Find("TopStatusBarPanel") == null) return;

        Apply(false);
        EditorSceneManager.SaveOpenScenes();
        EditorPrefs.SetBool(AppliedKey, true);
    }

    [MenuItem("Tools/FX OVERDOSE/Style HP and MENTAL Panel")]
    public static void ApplyFromMenu() => Apply(true);

    public static void ApplySilently() => Apply(false);

    private static void Apply(bool showResult)
    {
        GameObject topBar = Find("TopStatusBarPanel");
        TMP_FontAsset font = TMP_Settings.defaultFontAsset ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        Sprite frame = PrepareSprite(FramePath);
        Sprite heart = PrepareSprite(HeartPath);
        Sprite brain = PrepareSprite(BrainPath);

        if (topBar == null || font == null || frame == null || heart == null || brain == null)
        {
            if (showResult)
                EditorUtility.DisplayDialog("HP/MENTAL 적용 실패", "상태바, 슬라이더, 폰트 또는 생성 이미지를 찾지 못했습니다.", "확인");
            return;
        }

        GameObject panel = GetOrCreate(topBar.transform, "VitalsPanel");
        Slider hp = Find("HP")?.GetComponent<Slider>() ?? CreateStatusSlider("HP", panel.transform);
        Slider mental = Find("Mental")?.GetComponent<Slider>() ?? CreateStatusSlider("Mental", panel.transform);
        HorizontalLayoutGroup topLayout = topBar.GetComponent<HorizontalLayoutGroup>();
        if (topLayout != null)
        {
            Undo.RecordObject(topLayout, "Fix Top Status Bar Child Sizing");
            // LayoutElement의 840px 폭을 실제 RectTransform에 반영하도록 설정합니다.
            topLayout.childControlWidth = true;
            topLayout.childControlHeight = true;
            topLayout.childForceExpandWidth = false;
            topLayout.childForceExpandHeight = false;
            topLayout.childAlignment = TextAnchor.MiddleLeft;
        }

        LayoutElement panelSize = GetOrAdd<LayoutElement>(panel);
        // 1920 화면에서 HP와 MENTAL 게이지가 답답하지 않도록 상단바의 약 44%를 사용합니다.
        panelSize.minWidth = 840f;
        panelSize.preferredWidth = 840f;
        panelSize.preferredHeight = 96f;
        panelSize.flexibleWidth = 0f;

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        Undo.RecordObject(panelRect, "Set Vitals Panel Fallback Size");
        panelRect.sizeDelta = new Vector2(840f, 96f);

        Image panelImage = GetOrAdd<Image>(panel);
        panelImage.sprite = frame;
        panelImage.type = Image.Type.Simple;
        panelImage.preserveAspect = false;
        panelImage.color = Color.white;

        RemoveLayoutGroups(panel);
        BuildLabels(panel.transform, font);
        BuildIcon(panel.transform, "HealthHeartIcon", heart, new Vector2(0.035f, 0.12f), new Vector2(0.095f, 0.58f));
        BuildIcon(panel.transform, "MentalBrainIcon", brain, new Vector2(0.455f, 0.12f), new Vector2(0.515f, 0.58f));

        BuildBarBackground(panel.transform, "HealthBarBackground", new Vector2(0.10f, 0.14f), new Vector2(0.38f, 0.53f));
        BuildBarBackground(panel.transform, "MentalBarBackground", new Vector2(0.52f, 0.14f), new Vector2(0.80f, 0.53f));

        MoveAndStyleSlider(hp, panel.transform, new Vector2(0.105f, 0.18f), new Vector2(0.375f, 0.49f), new Color(1f, 0.27f, 0.43f, 1f));
        MoveAndStyleSlider(mental, panel.transform, new Vector2(0.525f, 0.18f), new Vector2(0.795f, 0.49f), new Color(0.62f, 0.30f, 0.88f, 1f));

        HideIfExists(panel.transform, "HealthValue");
        HideIfExists(panel.transform, "MentalValue");

        TMP_Text gear = BuildText(panel.transform, "SettingsIcon", font, "⚙", 39f, new Color(0.80f, 0.83f, 0.88f, 1f),
            new Vector2(0.885f, 0.14f), new Vector2(0.975f, 0.86f));
        gear.alignment = TextAlignmentOptions.Center;

        VitalsValueUI values = panel.GetComponent<VitalsValueUI>();
        if (values != null) Undo.DestroyObjectImmediate(values);

        BindHUDController(hp, mental);

        panel.transform.SetAsLastSibling();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        if (showResult)
        {
            Selection.activeGameObject = panel;
            EditorUtility.DisplayDialog("HP/MENTAL 적용 완료", "생성한 픽셀 프레임과 아이콘을 적용하고 기존 슬라이더를 연결했습니다.", "확인");
        }
    }

    private static void BuildLabels(Transform panel, TMP_FontAsset font)
    {
        BuildText(panel, "HPTitle", font, "HP", 25f, Color.white,
            new Vector2(0.035f, 0.56f), new Vector2(0.45f, 0.91f)).alignment = TextAlignmentOptions.MidlineLeft;
        BuildText(panel, "MentalTitle", font, "MENTAL", 25f, Color.white,
            new Vector2(0.455f, 0.56f), new Vector2(0.80f, 0.91f)).alignment = TextAlignmentOptions.MidlineLeft;
    }

    private static void MoveAndStyleSlider(Slider slider, Transform parent, Vector2 min, Vector2 max, Color fillColor)
    {
        Undo.SetTransformParent(slider.transform, parent, "Move " + slider.name + " Slider");
        SetRect(slider.GetComponent<RectTransform>(), min, max);
        slider.interactable = false;

        if (slider.fillRect != null)
        {
            Image fill = slider.fillRect.GetComponent<Image>();
            if (fill != null)
            {
                fill.color = fillColor;
                fill.type = Image.Type.Simple;
                fill.sprite = null;
            }
        }

        foreach (TMP_Text oldText in slider.GetComponentsInChildren<TMP_Text>(true))
            oldText.gameObject.SetActive(false);
    }

    private static Slider CreateStatusSlider(string name, Transform parent)
    {
        GameObject sliderObject = new GameObject(name, typeof(RectTransform), typeof(Slider));
        Undo.RegisterCreatedObjectUndo(sliderObject, "Create " + name + " Slider");
        sliderObject.transform.SetParent(parent, false);

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(fillArea, "Create Fill Area");
        fillArea.transform.SetParent(sliderObject.transform, false);
        SetRect(fillArea.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(fillObject, "Create Fill");
        fillObject.transform.SetParent(fillArea.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        SetRect(fillRect, Vector2.zero, Vector2.one);

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.fillRect = fillRect;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        slider.interactable = false;
        return slider;
    }

    private static void BindHUDController(Slider hp, Slider mental)
    {
        HUDController hud = Resources.FindObjectsOfTypeAll<HUDController>()
            .FirstOrDefault(controller => controller.gameObject.scene.IsValid());
        if (hud == null) return;

        SerializedObject serialized = new SerializedObject(hud);
        SerializedProperty hpProperty = serialized.FindProperty("healthSlider");
        SerializedProperty mentalProperty = serialized.FindProperty("mentalSlider");
        if (hpProperty != null) hpProperty.objectReferenceValue = hp;
        if (mentalProperty != null) mentalProperty.objectReferenceValue = mental;
        serialized.ApplyModifiedProperties();
    }

    private static void BuildBarBackground(Transform parent, string name, Vector2 min, Vector2 max)
    {
        GameObject bar = GetOrCreate(parent, name);
        Image image = GetOrAdd<Image>(bar);
        image.color = new Color(0.005f, 0.012f, 0.025f, 0.97f);
        image.raycastTarget = false;
        SetRect(bar.GetComponent<RectTransform>(), min, max);
        bar.transform.SetAsFirstSibling();
    }

    private static void BuildIcon(Transform parent, string name, Sprite sprite, Vector2 min, Vector2 max)
    {
        GameObject icon = GetOrCreate(parent, name);
        Image image = GetOrAdd<Image>(icon);
        image.sprite = sprite;
        image.preserveAspect = true;
        image.color = Color.white;
        image.raycastTarget = false;
        SetRect(icon.GetComponent<RectTransform>(), min, max);
    }

    private static TMP_Text BuildText(Transform parent, string name, TMP_FontAsset font, string value, float size, Color color, Vector2 min, Vector2 max)
    {
        GameObject go = GetOrCreate(parent, name);
        TMP_Text text = go.GetComponent<TMP_Text>();
        if (text == null) text = Undo.AddComponent<TextMeshProUGUI>(go);
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = FontStyles.Normal;
        text.color = color;
        text.enableAutoSizing = true;
        text.fontSizeMin = 14f;
        text.fontSizeMax = size;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        SetRect(text.rectTransform, min, max);
        return text;
    }

    private static Sprite PrepareSprite(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
    {
        Undo.RecordObject(rect, "Layout " + rect.name);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void RemoveLayoutGroups(GameObject target)
    {
        foreach (LayoutGroup group in target.GetComponents<LayoutGroup>()) Undo.DestroyObjectImmediate(group);
    }

    private static void HideIfExists(Transform parent, string name)
    {
        Transform found = parent.Find(name);
        if (found == null) return;
        Undo.RecordObject(found.gameObject, "Hide " + name);
        found.gameObject.SetActive(false);
    }

    private static GameObject GetOrCreate(Transform parent, string name)
    {
        Transform found = parent.Find(name);
        if (found != null) return found.gameObject;
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(target);
    }

    private static GameObject Find(string objectName)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(go => go.scene.IsValid() && go.name == objectName);
    }
}
#endif
