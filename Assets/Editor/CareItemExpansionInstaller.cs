#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>영양제와 진정제 에셋을 생성하고 상점·인벤토리에 연결합니다.</summary>
public static class CareItemExpansionInstaller
{
    private const string SupplementIconPath = "Assets/Img/Items/SupplementPixel.png";
    private const string SedativeIconPath = "Assets/Img/Items/SedativePixel.png";
    private const string SupplementAssetPath = "Assets/Data/Items/Supplement.asset";
    private const string SedativeAssetPath = "Assets/Data/Items/Sedative.asset";
    private const string AppliedKey = "FXOverdose_CareItemExpansion_v1";

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
        if (EditorSceneManager.GetActiveScene().name != "GameScene") return;
        if (EditorPrefs.GetBool(AppliedKey, false) && IsInstalled()) return;
        if (Apply(false)) EditorPrefs.SetBool(AppliedKey, true);
    }

    [MenuItem("Tools/FX OVERDOSE/Install Supplement And Sedative")]
    public static void ApplyFromMenu() => Apply(true);

    private static bool Apply(bool showResult)
    {
        PrepareIcon(SupplementIconPath);
        PrepareIcon(SedativeIconPath);

        Sprite supplementIcon = AssetDatabase.LoadAssetAtPath<Sprite>(SupplementIconPath);
        Sprite sedativeIcon = AssetDatabase.LoadAssetAtPath<Sprite>(SedativeIconPath);
        if (supplementIcon == null || sedativeIcon == null) return false;

        ItemData supplement = CreateOrUpdateItem(
            SupplementAssetPath, "Supplement", "supplement", "Supplement",
            "체력 50 회복", supplementIcon, ItemData.EffectType.Health, 50f, 1100);
        ItemData sedative = CreateOrUpdateItem(
            SedativeAssetPath, "Sedative", "sedative", "Sedative",
            "멘탈 40 회복", sedativeIcon, ItemData.EffectType.Mental, 40f, 1300);
        if (supplement == null || sedative == null) return false;

        ShopManager shop = Object.FindAnyObjectByType<ShopManager>(FindObjectsInactive.Include);
        Inventory inventory = Object.FindAnyObjectByType<Inventory>(FindObjectsInactive.Include);
        if (shop == null || inventory == null) return false;

        SerializedObject shopSerialized = new(shop);
        SerializedProperty catalog = shopSerialized.FindProperty("catalogItems");
        EnsureObjectInList(catalog, supplement);
        EnsureObjectInList(catalog, sedative);
        shopSerialized.ApplyModifiedProperties();

        SerializedObject inventorySerialized = new(inventory);
        SerializedProperty slots = inventorySerialized.FindProperty("slots");
        EnsureInventorySlot(slots, supplement, 1);
        EnsureInventorySlot(slots, sedative, 1);
        inventorySerialized.ApplyModifiedProperties();

        DynamicShopUI shopUI = Object.FindAnyObjectByType<DynamicShopUI>(FindObjectsInactive.Include);
        if (shopUI != null)
        {
            SerializedObject uiSerialized = new(shopUI);
            uiSerialized.FindProperty("maxColumns").intValue = 2;
            uiSerialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(shopUI);
        }

        EditorUtility.SetDirty(shop);
        EditorUtility.SetDirty(inventory);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();

        if (showResult)
        {
            EditorUtility.DisplayDialog(
                "CARE ITEMS 확장 완료",
                "영양제와 진정제를 생성하고 상점 2×2 카드 및 동적 인벤토리에 연결했습니다.",
                "확인");
        }

        return true;
    }

    private static ItemData CreateOrUpdateItem(
        string path, string assetName, string id, string displayName, string description,
        Sprite icon, ItemData.EffectType type, float amount, int price)
    {
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            item.name = assetName;
            AssetDatabase.CreateAsset(item, path);
        }

        SerializedObject serialized = new(item);
        serialized.FindProperty("itemId").stringValue = id;
        serialized.FindProperty("itemName").stringValue = displayName;
        serialized.FindProperty("description").stringValue = description;
        serialized.FindProperty("icon").objectReferenceValue = icon;
        serialized.FindProperty("effectType").enumValueIndex = (int)type;
        serialized.FindProperty("effectAmount").floatValue = amount;
        serialized.FindProperty("price").intValue = price;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(item);
        return item;
    }

    private static void EnsureObjectInList(SerializedProperty list, Object target)
    {
        if (target == null || ContainsObject(list, target)) return;
        int index = list.arraySize;
        list.InsertArrayElementAtIndex(index);
        list.GetArrayElementAtIndex(index).objectReferenceValue = target;
    }

    private static void EnsureInventorySlot(SerializedProperty slots, ItemData item, int startingQuantity)
    {
        for (int i = 0; i < slots.arraySize; i++)
        {
            SerializedProperty slot = slots.GetArrayElementAtIndex(i);
            if (slot.FindPropertyRelative("item").objectReferenceValue == item) return;
        }

        int index = slots.arraySize;
        slots.InsertArrayElementAtIndex(index);
        SerializedProperty newSlot = slots.GetArrayElementAtIndex(index);
        newSlot.FindPropertyRelative("item").objectReferenceValue = item;
        newSlot.FindPropertyRelative("quantity").intValue = startingQuantity;
    }

    private static bool ContainsObject(SerializedProperty list, Object target)
    {
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == target) return true;
        return false;
    }

    private static bool IsInstalled()
    {
        ItemData supplement = AssetDatabase.LoadAssetAtPath<ItemData>(SupplementAssetPath);
        ItemData sedative = AssetDatabase.LoadAssetAtPath<ItemData>(SedativeAssetPath);
        ShopManager shop = Object.FindAnyObjectByType<ShopManager>(FindObjectsInactive.Include);
        Inventory inventory = Object.FindAnyObjectByType<Inventory>(FindObjectsInactive.Include);
        if (supplement == null || sedative == null || shop == null || inventory == null) return false;

        return shop.CatalogItems.Contains(supplement)
            && shop.CatalogItems.Contains(sedative)
            && inventory.Slots.Any(slot => slot?.Item == supplement)
            && inventory.Slots.Any(slot => slot?.Item == sedative);
    }

    private static void PrepareIcon(string path)
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
        importer.SaveAndReimport();
    }
}
#endif
