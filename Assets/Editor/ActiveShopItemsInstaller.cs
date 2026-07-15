#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>액티브 장비 4종을 생성하고 GameScene 상점 카탈로그에 자동 연결합니다.</summary>
public static class ActiveShopItemsInstaller
{
    private const string AppliedKey = "FXOverdose_ActiveShopItems_MarketplaceSkinSimple_v4";
    private const string ItemFolder = "Assets/Data/Items/Active";
    private const string IconFolder = "Assets/Img/Items/Active";
    private static readonly string[] ShopSkinPaths =
    {
        "Assets/Resources/UI/Shop/CareBadgeFrame.png",
        "Assets/Resources/UI/Shop/ActiveBadgeFrame.png",
        "Assets/Resources/UI/Shop/CareBuyButton.png",
        "Assets/Resources/UI/Shop/ActiveBuyButton.png"
    };

    private readonly struct Definition
    {
        public readonly string AssetName;
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly string IconName;
        public readonly ItemData.EffectType Type;
        public readonly float Amount;
        public readonly int Price;
        public readonly int MaxLevel;
        public readonly float PriceMultiplier;

        public Definition(string assetName, string id, string displayName, string description, string iconName,
            ItemData.EffectType type, float amount, int price, int maxLevel, float priceMultiplier)
        {
            AssetName = assetName;
            Id = id;
            DisplayName = displayName;
            Description = description;
            IconName = iconName;
            Type = type;
            Amount = amount;
            Price = price;
            MaxLevel = maxLevel;
            PriceMultiplier = priceMultiplier;
        }
    }

    private static readonly Definition[] Definitions =
    {
        new("DualMonitor", "dual_monitor", "Dual Monitor", "차트 시야를 확장해 수익 실현액을 높입니다.", "DualMonitorPixel",
            ItemData.EffectType.ProfitBoost, 8f, 2000, 2, 1.5f),
        new("LossInsurance", "loss_insurance", "Loss Insurance", "손실 포지션의 확정 손해를 일부 보전합니다.", "LossInsurancePixel",
            ItemData.EffectType.LossReduction, 12f, 2200, 2, 1.5f),
        new("TherapyPass", "therapy_pass", "Therapy Pass", "손실과 연속 매매로 인한 멘탈 소모를 완화합니다.", "TherapyPassPixel",
            ItemData.EffectType.MentalDrainGuard, 25f, 1600, 1, 1f),
        new("ErgonomicChair", "ergonomic_chair", "Ergonomic Chair", "장시간 트레이딩 중 체력 소모를 줄입니다.", "ErgonomicChairPixel",
            ItemData.EffectType.HealthDrainGuard, 20f, 1800, 1, 1f)
    };

    [InitializeOnLoadMethod]
    private static void Initialize() => EditorApplication.delayCall += TryApply;

    private static void TryApply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorSceneManager.GetActiveScene().name != "GameScene") return;
        if (EditorPrefs.GetBool(AppliedKey, false) && IsInstalled()) return;
        if (Apply(false)) EditorPrefs.SetBool(AppliedKey, true);
    }

    [MenuItem("Tools/FX OVERDOSE/Install Active Marketplace Items")]
    public static void ApplyFromMenu() => Apply(true);

    private static bool Apply(bool showResult)
    {
        EnsureFolder("Assets/Data/Items", "Active");
        foreach (string skinPath in ShopSkinPaths) PrepareShopSkin(skinPath);

        ShopManager shop = Object.FindAnyObjectByType<ShopManager>(FindObjectsInactive.Include);
        DynamicShopUI shopUI = Object.FindAnyObjectByType<DynamicShopUI>(FindObjectsInactive.Include);
        if (shop == null || shopUI == null) return false;

        SerializedObject shopSerialized = new(shop);
        SerializedProperty catalog = shopSerialized.FindProperty("catalogItems");

        foreach (Definition definition in Definitions)
        {
            string iconPath = $"{IconFolder}/{definition.IconName}.png";
            PrepareIcon(iconPath);
            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            if (icon == null) return false;

            ItemData item = CreateOrUpdateItem(definition, icon);
            EnsureObjectInList(catalog, item);
        }

        shopSerialized.ApplyModifiedProperties();
        SerializedObject uiSerialized = new(shopUI);
        uiSerialized.FindProperty("maxColumns").intValue = 3;
        uiSerialized.FindProperty("careBadgeSprite").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>(ShopSkinPaths[0]);
        uiSerialized.FindProperty("activeBadgeSprite").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>(ShopSkinPaths[1]);
        uiSerialized.FindProperty("careBuyButtonSprite").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>(ShopSkinPaths[2]);
        uiSerialized.FindProperty("activeBuyButtonSprite").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>(ShopSkinPaths[3]);
        uiSerialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(shop);
        EditorUtility.SetDirty(shopUI);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();

        if (showResult)
            EditorUtility.DisplayDialog("FX MARKET 설치 완료", "액티브 장비 4종과 3열 마켓 상점을 연결했습니다.", "확인");
        return true;
    }

    private static ItemData CreateOrUpdateItem(Definition definition, Sprite icon)
    {
        string path = $"{ItemFolder}/{definition.AssetName}.asset";
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            item.name = definition.AssetName;
            AssetDatabase.CreateAsset(item, path);
        }

        SerializedObject serialized = new(item);
        serialized.FindProperty("itemId").stringValue = definition.Id;
        serialized.FindProperty("itemName").stringValue = definition.DisplayName;
        serialized.FindProperty("description").stringValue = definition.Description;
        serialized.FindProperty("icon").objectReferenceValue = icon;
        serialized.FindProperty("effectType").enumValueIndex = (int)definition.Type;
        serialized.FindProperty("effectAmount").floatValue = definition.Amount;
        serialized.FindProperty("price").intValue = definition.Price;
        serialized.FindProperty("isActiveItem").boolValue = true;
        serialized.FindProperty("maxLevel").intValue = definition.MaxLevel;
        serialized.FindProperty("priceMultiplierPerLevel").floatValue = definition.PriceMultiplier;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(item);
        return item;
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

    private static void PrepareShopSkin(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.spriteBorder = new Vector4(120f, 90f, 120f, 90f);
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }

    private static void EnsureObjectInList(SerializedProperty list, Object target)
    {
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == target) return;
        int index = list.arraySize;
        list.InsertArrayElementAtIndex(index);
        list.GetArrayElementAtIndex(index).objectReferenceValue = target;
    }

    private static bool IsInstalled()
    {
        ShopManager shop = Object.FindAnyObjectByType<ShopManager>(FindObjectsInactive.Include);
        DynamicShopUI shopUI = Object.FindAnyObjectByType<DynamicShopUI>(FindObjectsInactive.Include);
        if (shop == null || shopUI == null) return false;
        SerializedObject uiSerialized = new(shopUI);
        bool skinInstalled = uiSerialized.FindProperty("careBadgeSprite").objectReferenceValue != null
            && uiSerialized.FindProperty("activeBadgeSprite").objectReferenceValue != null
            && uiSerialized.FindProperty("careBuyButtonSprite").objectReferenceValue != null
            && uiSerialized.FindProperty("activeBuyButtonSprite").objectReferenceValue != null;
        return skinInstalled && Definitions.All(definition =>
        {
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>($"{ItemFolder}/{definition.AssetName}.asset");
            return item != null && shop.CatalogItems.Contains(item);
        });
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
