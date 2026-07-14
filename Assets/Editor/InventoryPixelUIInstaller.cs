#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>생성한 픽셀 스프라이트를 아이템 데이터에 연결하고 동적 인벤토리 UI를 설치합니다.</summary>
public static class InventoryPixelUIInstaller
{
    private const string EnergyPath = "Assets/Img/Items/EnergyDrinkPixel.png";
    private const string DessertPath = "Assets/Img/Items/DessertPixel.png";
    private const string SlotFramePath = "Assets/Img/UI/InventorySlotFramePixel.png";
    private const string EnergyDataPath = "Assets/Data/Items/EnergyDrink.asset";
    private const string DessertDataPath = "Assets/Data/Items/Dessert.asset";
    private const string AppliedKey = "FXOverdose_DynamicInventoryPixelUI_v1";

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
        if (GameObject.Find("ItemButtons") == null) return;
        Apply(false);
        EditorPrefs.SetBool(AppliedKey, true);
    }

    [MenuItem("Tools/FX OVERDOSE/Install Dynamic Pixel Inventory")]
    public static void ApplyFromMenu() => Apply(true);

    private static void Apply(bool showResult)
    {
        ConfigureTexture(EnergyPath, Vector4.zero);
        ConfigureTexture(DessertPath, Vector4.zero);
        ConfigureTexture(SlotFramePath, new Vector4(32f, 32f, 32f, 32f));
        AssetDatabase.Refresh();

        Sprite energySprite = AssetDatabase.LoadAssetAtPath<Sprite>(EnergyPath);
        Sprite dessertSprite = AssetDatabase.LoadAssetAtPath<Sprite>(DessertPath);
        Sprite slotSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SlotFramePath);
        AssignItemIcon(EnergyDataPath, energySprite);
        AssignItemIcon(DessertDataPath, dessertSprite);

        GameObject panel = GameObject.Find("ItemButtons");
        Inventory inventory = Object.FindAnyObjectByType<Inventory>();
        if (panel == null || inventory == null || slotSprite == null) return;

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-24f, 24f);

        Image background = panel.GetComponent<Image>();
        if (background == null) background = Undo.AddComponent<Image>(panel);
        background.color = new Color(0.015f, 0.035f, 0.065f, 0.96f);
        Outline outline = panel.GetComponent<Outline>();
        if (outline == null) outline = Undo.AddComponent<Outline>(panel);
        outline.effectColor = new Color(0.22f, 0.29f, 0.40f, 1f);
        outline.effectDistance = new Vector2(3f, -3f);

        DynamicInventoryUI dynamicUI = panel.GetComponent<DynamicInventoryUI>();
        if (dynamicUI == null) dynamicUI = Undo.AddComponent<DynamicInventoryUI>(panel);
        SerializedObject serialized = new(dynamicUI);
        serialized.FindProperty("inventory").objectReferenceValue = inventory;
        serialized.FindProperty("slotFrameSprite").objectReferenceValue = slotSprite;
        serialized.FindProperty("font").objectReferenceValue = PFStardustGlobalFontApplicator.GetFont();
        serialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(panel);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        if (showResult)
            EditorUtility.DisplayDialog("동적 인벤토리 적용 완료", "Inventory.Slots 개수에 따라 슬롯이 자동 생성됩니다.", "확인");
    }

    private static void ConfigureTexture(string path, Vector4 border)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 256f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.spriteBorder = border;
        importer.SaveAndReimport();
    }

    private static void AssignItemIcon(string path, Sprite sprite)
    {
        ItemData data = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (data == null || sprite == null) return;
        SerializedObject serialized = new(data);
        serialized.FindProperty("icon").objectReferenceValue = sprite;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
    }
}
#endif
