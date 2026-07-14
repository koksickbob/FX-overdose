#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>프리뷰 기반 동적 CARE SHOP UI를 GameScene에 설치합니다.</summary>
public static class DynamicShopUIInstaller
{
    private const string EnergyPath = "Assets/Data/Items/EnergyDrink.asset";
    private const string DessertPath = "Assets/Data/Items/Dessert.asset";
    private const string CardFramePath = "Assets/Img/UI/InventorySlotFramePixel.png";
    private const string AppliedKey = "FXOverdose_DynamicCareShop_v1";

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
        if (FindSceneObject("ShopPanel") == null) return;
        Apply(false);
        EditorPrefs.SetBool(AppliedKey, true);
    }

    [MenuItem("Tools/FX OVERDOSE/Install Dynamic Care Shop")]
    public static void ApplyFromMenu() => Apply(true);

    private static void Apply(bool showResult)
    {
        GameObject panel = FindSceneObject("ShopPanel");
        ShopManager manager = Object.FindAnyObjectByType<ShopManager>(FindObjectsInactive.Include);
        ItemData energy = AssetDatabase.LoadAssetAtPath<ItemData>(EnergyPath);
        ItemData dessert = AssetDatabase.LoadAssetAtPath<ItemData>(DessertPath);
        Sprite cardFrame = AssetDatabase.LoadAssetAtPath<Sprite>(CardFramePath);
        if (panel == null || manager == null || cardFrame == null) return;

        SerializedObject managerSerialized = new(manager);
        SerializedProperty catalog = managerSerialized.FindProperty("catalogItems");
        if (catalog.arraySize == 0)
        {
            AddCatalogItem(catalog, energy);
            AddCatalogItem(catalog, dessert);
        }
        managerSerialized.ApplyModifiedProperties();

        DynamicShopUI dynamicUI = panel.GetComponent<DynamicShopUI>();
        if (dynamicUI == null) dynamicUI = Undo.AddComponent<DynamicShopUI>(panel);
        SerializedObject uiSerialized = new(dynamicUI);
        uiSerialized.FindProperty("shopManager").objectReferenceValue = manager;
        uiSerialized.FindProperty("cardFrameSprite").objectReferenceValue = cardFrame;
        uiSerialized.FindProperty("font").objectReferenceValue = PFStardustGlobalFontApplicator.GetFont() ?? TMP_Settings.defaultFontAsset;
        uiSerialized.ApplyModifiedProperties();

        panel.transform.SetAsLastSibling();
        EditorUtility.SetDirty(panel);
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        if (showResult)
            EditorUtility.DisplayDialog("CARE SHOP 적용 완료", "Catalog Items에 따라 상품 카드가 자동 생성·삭제됩니다.", "확인");
    }

    private static void AddCatalogItem(SerializedProperty catalog, ItemData item)
    {
        if (item == null) return;
        int index = catalog.arraySize;
        catalog.InsertArrayElementAtIndex(index);
        catalog.GetArrayElementAtIndex(index).objectReferenceValue = item;
    }

    private static GameObject FindSceneObject(string name) => Resources.FindObjectsOfTypeAll<GameObject>()
        .FirstOrDefault(go => go.scene.IsValid() && go.name == name);
}
#endif
