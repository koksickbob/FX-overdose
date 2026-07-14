using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using FXOverdose.EditorTools;

public static class ItemButtonsUIBuilder
{
    private const string EnergyDrinkPath = "Assets/Data/Items/EnergyDrink.asset";
    private const string DessertPath = "Assets/Data/Items/Dessert.asset";

    [MenuItem("FX Overdose/Setup Item Buttons")]
    public static void SetupItemButtons()
    {
        GameObject hud = GameObject.Find("HUD");
        GameObject traderObject = GameObject.Find("TraderStatus");

        if (hud == null || traderObject == null)
        {
            EditorUtility.DisplayDialog(
                "아이템 버튼 생성 실패",
                "현재 씬에서 HUD 또는 TraderStatus 오브젝트를 찾지 못했습니다.",
                "확인");
            return;
        }

        ItemData energyDrink = AssetDatabase.LoadAssetAtPath<ItemData>(EnergyDrinkPath);
        ItemData dessert = AssetDatabase.LoadAssetAtPath<ItemData>(DessertPath);

        if (energyDrink == null || dessert == null)
        {
            EditorUtility.DisplayDialog(
                "아이템 버튼 생성 실패",
                "EnergyDrink 또는 Dessert 아이템 에셋을 찾지 못했습니다.",
                "확인");
            return;
        }

        ItemUser itemUser = GetOrAddComponent<ItemUser>(traderObject);
        Inventory inventory = GetOrAddComponent<Inventory>(traderObject);
        TraderStatus traderStatus = traderObject.GetComponent<TraderStatus>();

        ConnectItemUser(itemUser, traderStatus);
        ConfigureInventory(inventory, itemUser, energyDrink, dessert);

        RectTransform panel = GetOrCreatePanel(hud.transform);
        CreateOrUpdateButton(panel, "EnergyDrinkButton", energyDrink, inventory, -110f);
        CreateOrUpdateButton(panel, "DessertButton", dessert, inventory, 110f);
        EnsureEventSystem();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = panel.gameObject;

        EditorUtility.DisplayDialog(
            "아이템 버튼 생성 완료",
            "HUD 아래에 EnergyDrinkButton과 DessertButton을 생성하고 연결했습니다.\n씬을 저장한 뒤 Play 모드에서 확인하세요.",
            "확인");
    }

    private static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(target);
    }

    private static void ConnectItemUser(ItemUser itemUser, TraderStatus traderStatus)
    {
        SerializedObject serializedItemUser = new(itemUser);
        serializedItemUser.FindProperty("traderStatus").objectReferenceValue = traderStatus;
        serializedItemUser.ApplyModifiedProperties();
    }

    private static void ConfigureInventory(
        Inventory inventory,
        ItemUser itemUser,
        ItemData energyDrink,
        ItemData dessert)
    {
        SerializedObject serializedInventory = new(inventory);
        serializedInventory.FindProperty("itemUser").objectReferenceValue = itemUser;

        SerializedProperty slots = serializedInventory.FindProperty("slots");
        EnsureInventorySlot(slots, energyDrink, 3);
        EnsureInventorySlot(slots, dessert, 3);

        serializedInventory.ApplyModifiedProperties();
    }

    private static void EnsureInventorySlot(
        SerializedProperty slots,
        ItemData item,
        int startingQuantity)
    {
        for (int i = 0; i < slots.arraySize; i++)
        {
            SerializedProperty slot = slots.GetArrayElementAtIndex(i);
            if (slot.FindPropertyRelative("item").objectReferenceValue == item)
            {
                return;
            }
        }

        int newIndex = slots.arraySize;
        slots.InsertArrayElementAtIndex(newIndex);

        SerializedProperty newSlot = slots.GetArrayElementAtIndex(newIndex);
        newSlot.FindPropertyRelative("item").objectReferenceValue = item;
        newSlot.FindPropertyRelative("quantity").intValue = startingQuantity;
    }

    private static RectTransform GetOrCreatePanel(Transform hud)
    {
        Transform existing = hud.Find("ItemButtons");
        RectTransform panel;

        if (existing != null)
        {
            panel = existing.GetComponent<RectTransform>();
        }
        else
        {
            GameObject panelObject = new("ItemButtons", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(panelObject, "Create Item Buttons");
            panelObject.transform.SetParent(hud, false);
            panel = panelObject.GetComponent<RectTransform>();
        }

        panel.anchorMin = new Vector2(1f, 0f);
        panel.anchorMax = new Vector2(1f, 0f);
        panel.pivot = new Vector2(1f, 0f);
        panel.anchoredPosition = new Vector2(-30f, 30f);
        panel.sizeDelta = new Vector2(450f, 130f);
        return panel;
    }

    private static void CreateOrUpdateButton(
        RectTransform panel,
        string objectName,
        ItemData item,
        Inventory inventory,
        float positionX)
    {
        Transform existing = panel.Find(objectName);
        GameObject buttonObject;

        if (existing != null)
        {
            buttonObject = existing.gameObject;
        }
        else
        {
            buttonObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(InventoryItemButton));

            Undo.RegisterCreatedObjectUndo(buttonObject, $"Create {objectName}");
            buttonObject.transform.SetParent(panel, false);
        }

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(positionX, 0f);
        rect.sizeDelta = new Vector2(200f, 100f);

        Image background = buttonObject.GetComponent<Image>();
        background.color = item.Type == ItemData.EffectType.Health
            ? new Color(0.12f, 0.34f, 0.48f, 0.95f)
            : new Color(0.36f, 0.20f, 0.48f, 0.95f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = background;

        TMP_Text nameText = GetOrCreateText(
            buttonObject.transform,
            "NameText",
            new Vector2(0f, 12f),
            new Vector2(180f, 40f),
            24f);

        TMP_Text quantityText = GetOrCreateText(
            buttonObject.transform,
            "QuantityText",
            new Vector2(0f, -25f),
            new Vector2(180f, 30f),
            20f);

        SerializedObject serializedButton = new(buttonObject.GetComponent<InventoryItemButton>());
        serializedButton.FindProperty("inventory").objectReferenceValue = inventory;
        serializedButton.FindProperty("item").objectReferenceValue = item;
        serializedButton.FindProperty("button").objectReferenceValue = button;
        serializedButton.FindProperty("iconImage").objectReferenceValue = null;
        serializedButton.FindProperty("nameText").objectReferenceValue = nameText;
        serializedButton.FindProperty("quantityText").objectReferenceValue = quantityText;
        serializedButton.ApplyModifiedProperties();

        nameText.text = item.ItemName;
        quantityText.text = $"×{inventory.GetQuantity(item)}";
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

    private static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystem = new(
            "EventSystem",
            typeof(EventSystem),
            typeof(StandaloneInputModule));

        Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
    }
}
