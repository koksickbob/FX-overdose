using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using FXOverdose.EditorTools;

public static class ShopUIBuilder
{
    private const string EnergyDrinkPath = "Assets/Data/Items/EnergyDrink.asset";
    private const string DessertPath = "Assets/Data/Items/Dessert.asset";

    [MenuItem("FX Overdose/Setup Shop UI")]
    public static void SetupShopUI()
    {
        GameObject hud = GameObject.Find("HUD");
        GameObject gameManagerObject = GameObject.Find("GameManager");
        GameObject traderObject = GameObject.Find("TraderStatus");

        if (hud == null || gameManagerObject == null || traderObject == null)
        {
            EditorUtility.DisplayDialog(
                "상점 생성 실패",
                "HUD, GameManager 또는 TraderStatus 오브젝트를 찾지 못했습니다.",
                "확인");
            return;
        }

        GameManager gameManager = gameManagerObject.GetComponent<GameManager>();
        Inventory inventory = traderObject.GetComponent<Inventory>();
        ItemData energyDrink = AssetDatabase.LoadAssetAtPath<ItemData>(EnergyDrinkPath);
        ItemData dessert = AssetDatabase.LoadAssetAtPath<ItemData>(DessertPath);

        if (gameManager == null || inventory == null || energyDrink == null || dessert == null)
        {
            EditorUtility.DisplayDialog(
                "상점 생성 실패",
                "GameManager, Inventory 또는 아이템 데이터가 준비되지 않았습니다. 먼저 Setup Item Buttons를 실행하세요.",
                "확인");
            return;
        }

        ShopManager shopManager = GetOrAddComponent<ShopManager>(hud);
        Button openButton = CreateOpenButton(hud.transform);
        RectTransform panel = CreateShopPanel(hud.transform);
        Button closeButton = CreateCloseButton(panel);

        CreateShopItemButton(panel, "BuyEnergyDrink", energyDrink, shopManager, -170f);
        CreateShopItemButton(panel, "BuyDessert", dessert, shopManager, 170f);
        ConnectShopManager(shopManager, gameManager, inventory, panel.gameObject, openButton, closeButton);

        panel.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = panel.gameObject;

        EditorUtility.DisplayDialog(
            "상점 생성 완료",
            "상점 열기 버튼과 구매 팝업을 생성했습니다. 씬을 저장한 뒤 Play 모드에서 확인하세요.",
            "확인");
    }

    private static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(target);
    }

    private static Button CreateOpenButton(Transform hud)
    {
        Button button = GetOrCreateButton(hud, "ShopOpenButton");
        RectTransform rect = button.GetComponent<RectTransform>();
        // 인벤토리 패널(우측 하단 450x130)의 바로 위쪽에 배치합니다.
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-30f, 175f);
        rect.sizeDelta = new Vector2(180f, 70f);
        button.GetComponent<Image>().color = new Color(0.12f, 0.25f, 0.35f, 0.95f);

        TMP_Text label = GetOrCreateText(button.transform, "Label", Vector2.zero, new Vector2(160f, 50f), 26f);
        label.text = "SHOP";
        return button;
    }

    private static RectTransform CreateShopPanel(Transform hud)
    {
        Transform existing = hud.Find("ShopPanel");
        GameObject panelObject;

        if (existing != null)
        {
            panelObject = existing.gameObject;
        }
        else
        {
            panelObject = new GameObject(
                "ShopPanel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            Undo.RegisterCreatedObjectUndo(panelObject, "Create Shop Panel");
            panelObject.transform.SetParent(hud, false);
        }

        panelObject.SetActive(true);
        RectTransform panel = panelObject.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(760f, 460f);
        panelObject.GetComponent<Image>().color = new Color(0.025f, 0.055f, 0.09f, 0.98f);

        TMP_Text title = GetOrCreateText(panel, "Title", new Vector2(0f, 170f), new Vector2(500f, 60f), 38f);
        title.text = "ITEM SHOP";
        return panel;
    }

    private static Button CreateCloseButton(RectTransform panel)
    {
        Button button = GetOrCreateButton(panel, "CloseButton");
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-20f, -20f);
        rect.sizeDelta = new Vector2(70f, 55f);
        button.GetComponent<Image>().color = new Color(0.45f, 0.12f, 0.18f, 0.95f);

        TMP_Text label = GetOrCreateText(button.transform, "Label", Vector2.zero, new Vector2(60f, 45f), 28f);
        label.text = "X";
        return button;
    }

    private static void CreateShopItemButton(
        RectTransform panel,
        string objectName,
        ItemData item,
        ShopManager shopManager,
        float positionX)
    {
        Button button = GetOrCreateButton(panel, objectName);
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(positionX, -20f);
        rect.sizeDelta = new Vector2(270f, 210f);
        button.GetComponent<Image>().color = item.Type == ItemData.EffectType.Health
            ? new Color(0.10f, 0.32f, 0.45f, 0.95f)
            : new Color(0.34f, 0.18f, 0.45f, 0.95f);

        TMP_Text nameText = GetOrCreateText(button.transform, "NameText", new Vector2(0f, 35f), new Vector2(240f, 55f), 27f);
        TMP_Text priceText = GetOrCreateText(button.transform, "PriceText", new Vector2(0f, -35f), new Vector2(240f, 45f), 25f);

        ShopItemButton shopButton = GetOrAddComponent<ShopItemButton>(button.gameObject);
        SerializedObject serializedButton = new(shopButton);
        serializedButton.FindProperty("shopManager").objectReferenceValue = shopManager;
        serializedButton.FindProperty("item").objectReferenceValue = item;
        serializedButton.FindProperty("button").objectReferenceValue = button;
        serializedButton.FindProperty("nameText").objectReferenceValue = nameText;
        serializedButton.FindProperty("priceText").objectReferenceValue = priceText;
        serializedButton.ApplyModifiedProperties();

        nameText.text = item.ItemName;
        priceText.text = $"${item.Price:N0}";
    }

    private static void ConnectShopManager(
        ShopManager shopManager,
        GameManager gameManager,
        Inventory inventory,
        GameObject panel,
        Button openButton,
        Button closeButton)
    {
        SerializedObject serializedManager = new(shopManager);
        serializedManager.FindProperty("gameManager").objectReferenceValue = gameManager;
        serializedManager.FindProperty("inventory").objectReferenceValue = inventory;
        serializedManager.FindProperty("shopPanel").objectReferenceValue = panel;
        serializedManager.FindProperty("openButton").objectReferenceValue = openButton;
        serializedManager.FindProperty("closeButton").objectReferenceValue = closeButton;
        serializedManager.ApplyModifiedProperties();
    }

    private static Button GetOrCreateButton(Transform parent, string objectName)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            return existing.GetComponent<Button>();
        }

        GameObject buttonObject = new(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        Undo.RegisterCreatedObjectUndo(buttonObject, $"Create {objectName}");
        buttonObject.transform.SetParent(parent, false);
        return buttonObject.GetComponent<Button>();
    }

    private static TMP_Text GetOrCreateText(
        Transform parent,
        string objectName,
        Vector2 position,
        Vector2 size,
        float fontSize)
    {
        Transform existing = parent.Find(objectName);
        TextMeshProUGUI text;

        if (existing != null)
        {
            text = existing.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            GameObject textObject = new(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(textObject, $"Create {objectName}");
            textObject.transform.SetParent(parent, false);
            text = textObject.GetComponent<TextMeshProUGUI>();
        }

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;

        TMP_FontAsset kFont = FXOverdose.EditorTools.TradingViewUIBuilder.GetOrCreateKoreanFontAsset();
        if (kFont != null)
        {
            text.font = kFont;
            if (kFont.material != null) text.fontSharedMaterial = kFont.material;
        }

        return text;
    }
}
