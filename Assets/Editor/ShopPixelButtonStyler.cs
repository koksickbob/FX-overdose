#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>SHOP 버튼에 생성한 픽셀 프레임을 적용합니다.</summary>
public static class ShopPixelButtonStyler
{
    private const string SpritePath = "Assets/Img/UI/ShopButtonPixel.png";
    private const string AppliedKey = "FXOverdose_ShopPixelButton_Proportional_v4";

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
        if (GameObject.Find("ShopOpenButton") == null) return;
        Apply(false);
        EditorPrefs.SetBool(AppliedKey, true);
    }

    [MenuItem("Tools/FX OVERDOSE/Apply Pixel Shop Button")]
    public static void ApplyFromMenu() => Apply(true);

    private static void Apply(bool showResult)
    {
        TextureImporter importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 256f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spriteBorder = new Vector4(46f, 44f, 46f, 44f);
            importer.SaveAndReimport();
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        GameObject buttonObject = GameObject.Find("ShopOpenButton");
        GameObject inventoryObject = GameObject.Find("ItemButtons");
        if (sprite == null || buttonObject == null || inventoryObject == null) return;

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        // 3:1 비율을 유지하면서 직전 420×140 크기에서 가로·세로를 절반으로 축소합니다.
        rect.sizeDelta = new Vector2(210f, 70f);
        Image image = buttonObject.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = Color.white;

        Button button = buttonObject.GetComponent<Button>();
        if (button != null)
        {
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.86f, 0.95f, 1f, 1f);
            colors.pressedColor = new Color(0.68f, 0.78f, 0.92f, 1f);
            button.colors = colors;
        }

        TMP_Text label = buttonObject.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = "SHOP";
            label.font = PFStardustGlobalFontApplicator.GetFont() ?? TMP_Settings.defaultFontAsset;
            label.fontSize = 27f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.rectTransform.anchorMin = new Vector2(0.30f, 0f);
            label.rectTransform.anchorMax = new Vector2(0.94f, 1f);
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
        }

        ShopButtonInventoryFollower follower = buttonObject.GetComponent<ShopButtonInventoryFollower>();
        if (follower == null) follower = Undo.AddComponent<ShopButtonInventoryFollower>(buttonObject);
        SerializedObject serialized = new(follower);
        serialized.FindProperty("inventoryPanel").objectReferenceValue = inventoryObject.GetComponent<RectTransform>();
        serialized.ApplyModifiedProperties();
        follower.Configure(inventoryObject.GetComponent<RectTransform>());

        EditorUtility.SetDirty(buttonObject);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        if (showResult)
            EditorUtility.DisplayDialog("SHOP 버튼 적용 완료", "픽셀 버튼 이미지와 동적 인벤토리 추적 위치를 적용했습니다.", "확인");
    }
}
#endif
