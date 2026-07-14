#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>DAY/TIME 카드를 레퍼런스 이미지 형태로 꾸미고 PF스타더스트 폰트를 적용합니다.</summary>
public static class DayTimeCardStyler
{
    private const string SourceFontPath = "Assets/Fonts/PF스타더스트 3.0 Bold.ttf";
    private const string FontAssetPath = "Assets/Fonts/PFStardustBold SDF.asset";
    private const string FrameSpritePath = "Assets/Img/DayTimeCardFrame.png";
    private const string AppliedKey = "FXOverdose_DayTimeCard_PFStardust_v4";

    [InitializeOnLoadMethod]
    private static void ApplyOnceAfterCompile()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorPrefs.GetBool(AppliedKey, false)) return;
            if (FindSceneObject("DayTimeCard") == null) return;

            ApplyStyle(false);
            EditorSceneManager.SaveOpenScenes();
            EditorPrefs.SetBool(AppliedKey, true);
        };
    }

    [MenuItem("Tools/FX OVERDOSE/Style DAY TIME Card")]
    public static void ApplyFromMenu()
    {
        ApplyStyle(true);
    }

    private static void ApplyStyle(bool showResult)
    {
        GameObject card = FindSceneObject("DayTimeCard");
        TMP_Text day = FindSceneObject("DayLabel")?.GetComponent<TMP_Text>();
        TMP_Text time = FindSceneObject("TimeLabel")?.GetComponent<TMP_Text>();
        TMP_FontAsset fontAsset = GetOrCreateFontAsset();

        if (card == null || day == null || time == null || fontAsset == null)
        {
            if (showResult)
                EditorUtility.DisplayDialog("DAY/TIME 적용 실패", "카드 또는 PF스타더스트 폰트를 찾지 못했습니다.", "확인");
            return;
        }

        Undo.RecordObject(card, "Style DAY TIME Card");

        VerticalLayoutGroup vertical = card.GetComponent<VerticalLayoutGroup>();
        if (vertical != null) Undo.DestroyObjectImmediate(vertical);

        HorizontalLayoutGroup horizontal = card.GetComponent<HorizontalLayoutGroup>();
        if (horizontal == null) horizontal = Undo.AddComponent<HorizontalLayoutGroup>(card);
        horizontal.padding = new RectOffset(18, 18, 12, 12);
        horizontal.spacing = 8f;
        horizontal.childAlignment = TextAnchor.MiddleCenter;
        horizontal.childControlWidth = true;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = false;
        horizontal.childForceExpandHeight = true;

        LayoutElement cardSize = card.GetComponent<LayoutElement>();
        if (cardSize == null) cardSize = Undo.AddComponent<LayoutElement>(card);
        cardSize.preferredWidth = 310f;
        cardSize.minWidth = 310f;
        cardSize.preferredHeight = 96f;

        Image background = card.GetComponent<Image>();
        if (background == null) background = Undo.AddComponent<Image>(card);
        background.sprite = PrepareFrameSprite();
        background.type = Image.Type.Simple;
        background.preserveAspect = false;
        background.color = Color.white;

        Outline outline = card.GetComponent<Outline>();
        if (outline == null) outline = Undo.AddComponent<Outline>(card);
        outline.effectColor = new Color(0.25f, 0.31f, 0.40f, 1f);
        outline.effectDistance = new Vector2(3f, -3f);
        outline.useGraphicAlpha = true;

        ConfigureText(day, fontAsset, 29f, 122f, Color.white);
        day.text = string.IsNullOrWhiteSpace(day.text) ? "DAY 03" : day.text;
        day.transform.SetAsFirstSibling();

        TMP_Text clock = GetOrCreateClock(card.transform);
        ConfigureText(clock, fontAsset, 31f, 38f, new Color(0.08f, 0.72f, 0.90f, 1f));
        clock.text = "◷";
        clock.transform.SetSiblingIndex(1);

        ConfigureText(time, fontAsset, 29f, 98f, Color.white);
        time.transform.SetSiblingIndex(2);

        // 상단 상태바의 첫 카드가 화면 좌측 상단 모서리에서 시작하도록 여백을 제거합니다.
        if (card.transform.parent is RectTransform topBarRect)
        {
            Undo.RecordObject(topBarRect, "Pin Top Bar To Upper Left");
            topBarRect.anchorMin = new Vector2(0f, 1f);
            topBarRect.anchorMax = new Vector2(1f, 1f);
            topBarRect.pivot = new Vector2(0.5f, 1f);
            topBarRect.anchoredPosition = Vector2.zero;
            topBarRect.sizeDelta = new Vector2(0f, 105f);

            HorizontalLayoutGroup topLayout = topBarRect.GetComponent<HorizontalLayoutGroup>();
            if (topLayout != null)
            {
                topLayout.padding = new RectOffset(0, 0, 0, 4);
                topLayout.childAlignment = TextAnchor.UpperLeft;
            }
        }

        EditorUtility.SetDirty(card);
        EditorUtility.SetDirty(day);
        EditorUtility.SetDirty(time);
        EditorUtility.SetDirty(clock);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        if (showResult)
        {
            Selection.activeGameObject = card;
            EditorUtility.DisplayDialog("DAY/TIME 적용 완료", "PF스타더스트 폰트와 가로형 날짜·시간 UI를 적용했습니다.", "확인");
        }
    }

    private static void ConfigureText(TMP_Text text, TMP_FontAsset font, float size, float width, Color color)
    {
        Undo.RecordObject(text, "Apply PF Stardust Font");
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyles.Normal;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 18f;
        text.fontSizeMax = size;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;

        LayoutElement layout = text.GetComponent<LayoutElement>();
        if (layout == null) layout = Undo.AddComponent<LayoutElement>(text.gameObject);
        layout.preferredWidth = width;
        layout.flexibleWidth = 0f;
    }

    private static TMP_Text GetOrCreateClock(Transform card)
    {
        Transform existing = card.Find("ClockIcon");
        if (existing != null && existing.TryGetComponent(out TMP_Text existingText)) return existingText;

        GameObject icon = new GameObject("ClockIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(icon, "Create Clock Icon");
        icon.transform.SetParent(card, false);
        return icon.GetComponent<TextMeshProUGUI>();
    }

    private static TMP_FontAsset GetOrCreateFontAsset()
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (existing != null) return existing;

        Font source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (source == null)
        {
            string guid = AssetDatabase.FindAssets("PF t:Font").FirstOrDefault();
            if (!string.IsNullOrEmpty(guid))
                source = AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(guid));
        }
        if (source == null) return null;

        TMP_FontAsset created = TMP_FontAsset.CreateFontAsset(source);
        created.name = "PFStardustBold SDF";
        created.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        AssetDatabase.CreateAsset(created, FontAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return created;
    }

    private static Sprite PrepareFrameSprite()
    {
        TextureImporter importer = AssetImporter.GetAtPath(FrameSpritePath) as TextureImporter;
        if (importer != null)
        {
            bool changed = importer.textureType != TextureImporterType.Sprite ||
                           importer.spriteImportMode != SpriteImportMode.Single ||
                           importer.filterMode != FilterMode.Point ||
                           importer.textureCompression != TextureImporterCompression.Uncompressed;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            if (changed) importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(FrameSpritePath);
    }

    private static GameObject FindSceneObject(string objectName)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(go => go.scene.IsValid() && go.name == objectName);
    }
}
#endif
