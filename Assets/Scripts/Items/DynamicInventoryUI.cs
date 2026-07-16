using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Inventory.Slots 개수에 맞춰 픽셀 아이템 슬롯을 자동 생성하고 재배치합니다.</summary>
[RequireComponent(typeof(RectTransform))]
public class DynamicInventoryUI : MonoBehaviour
{
    [Header("데이터 및 이미지")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private Sprite slotFrameSprite;
    [SerializeField] private TMP_FontAsset font;

    [Header("반응형 슬롯 배치")]
    [SerializeField, Min(1)] private int maxColumns = 5;
    [SerializeField] private float maxPanelWidth = 700f;
    [SerializeField] private float preferredSlotSize = 122f;
    [SerializeField] private float minSlotSize = 78f;
    [SerializeField] private float spacing = 10f;
    [SerializeField] private float horizontalPadding = 18f;
    [SerializeField] private float topPadding = 48f;
    [SerializeField] private float bottomPadding = 14f;

    private readonly List<GameObject> generatedSlots = new();
    private RectTransform panelRect;
    private TMP_Text titleText;
    private TMP_Text activeBuffsText;
    private int lastSlotCount = -1;

    private void Awake()
    {
        panelRect = GetComponent<RectTransform>();
        if (inventory == null) inventory = FindAnyObjectByType<Inventory>();
        CreateTitleIfNeeded();
        HideLegacyButtons();
        Rebuild();
    }

    private void OnEnable()
    {
        if (inventory != null) inventory.QuantityChanged += OnInventoryChanged;
        if (ActiveItemEffectManager.Instance != null)
        {
            ActiveItemEffectManager.Instance.OnActiveItemsChanged -= RefreshActiveBuffsText;
            ActiveItemEffectManager.Instance.OnActiveItemsChanged += RefreshActiveBuffsText;
        }
        RefreshActiveBuffsText();
    }

    private void OnDisable()
    {
        if (inventory != null) inventory.QuantityChanged -= OnInventoryChanged;
        if (ActiveItemEffectManager.Instance != null)
        {
            ActiveItemEffectManager.Instance.OnActiveItemsChanged -= RefreshActiveBuffsText;
        }
    }

    private void RefreshActiveBuffsText()
    {
        if (activeBuffsText == null) CreateTitleIfNeeded();
        if (activeBuffsText != null && ActiveItemEffectManager.Instance != null)
        {
            activeBuffsText.text = ActiveItemEffectManager.Instance.GetSummaryText();
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled && generatedSlots.Count > 0) LayoutSlots();
    }

    private void OnInventoryChanged(ItemData item, int quantity)
    {
        if (inventory != null && (inventory.Slots.Count != lastSlotCount || quantity <= 0))
        {
            Rebuild();
            return;
        }

        // 기존 슬롯의 수량만 바뀐 경우에도 같은 프레임 안의 숫자를 즉시 갱신합니다.
        foreach (GameObject slot in generatedSlots)
        {
            if (slot == null) continue;
            InventoryItemButton button = slot.GetComponent<InventoryItemButton>();
            if (button != null) button.RefreshDisplay();
            TMP_Text[] texts = slot.GetComponentsInChildren<TMP_Text>(true);
            if (texts != null)
            {
                foreach (var t in texts) if (t != null) t.ForceMeshUpdate();
            }
        }
        Canvas.ForceUpdateCanvases();
    }

    [ContextMenu("Rebuild Inventory UI")]
    public void Rebuild()
    {
        if (panelRect == null) panelRect = GetComponent<RectTransform>();
        ClearGeneratedSlots();
        if (inventory == null) return;

        foreach (InventorySlot slot in inventory.Slots)
        {
            if (slot?.Item == null) continue;
            generatedSlots.Add(CreateSlot(slot.Item));
        }

        lastSlotCount = inventory.Slots.Count;
        LayoutSlots();
    }

    private GameObject CreateSlot(ItemData item)
    {
        GameObject slot = new($"DynamicSlot_{item.ItemId}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline), typeof(InventoryItemButton));
        slot.transform.SetParent(transform, false);

        Image frame = slot.GetComponent<Image>();
        frame.sprite = slotFrameSprite;
        frame.type = Image.Type.Sliced;
        frame.color = Color.white;

        Outline outline = slot.GetComponent<Outline>();
        outline.effectColor = UIStrokeStyle.DefaultColor;
        outline.effectDistance = UIStrokeStyle.EffectDistance;
        outline.useGraphicAlpha = true;

        Button button = slot.GetComponent<Button>();
        button.targetGraphic = frame;

        Image icon = CreateImage(slot.transform, "ItemIcon");
        // 슬롯 프레임 정중앙을 기준으로 상하좌우 여백을 동일하게 둡니다.
        // 기존 Y 0.19~0.91 영역은 중심이 위로 치우쳐 에너지 드링크 아래 여백이 더 크게 보였습니다.
        SetAnchors(icon.rectTransform, new Vector2(0.14f, 0.14f), new Vector2(0.86f, 0.86f), Vector2.zero, Vector2.zero);
        icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        icon.rectTransform.anchoredPosition = Vector2.zero;
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TMP_Text quantity = CreateText(slot.transform, "Quantity", 22f, TextAlignmentOptions.BottomRight);
        SetAnchors(quantity.rectTransform, new Vector2(0.43f, 0.02f), new Vector2(0.94f, 0.34f), Vector2.zero, Vector2.zero);
        quantity.fontStyle = FontStyles.Bold;

        InventoryItemButton itemButton = slot.GetComponent<InventoryItemButton>();
        itemButton.Configure(inventory, item, button, icon, null, quantity);
        return slot;
    }

    private void LayoutSlots()
    {
        int count = generatedSlots.Count;
        int columns = Mathf.Max(1, Mathf.Min(maxColumns, count));
        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)columns));
        float usableWidth = maxPanelWidth - horizontalPadding * 2f - spacing * (columns - 1);
        float slotSize = Mathf.Clamp(usableWidth / columns, minSlotSize, preferredSlotSize);
        float panelWidth = horizontalPadding * 2f + columns * slotSize + (columns - 1) * spacing;
        float panelHeight = topPadding + bottomPadding + rows * slotSize + (rows - 1) * spacing;

        panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);
        for (int i = 0; i < count; i++)
        {
            int column = i % columns;
            int row = i / columns;
            RectTransform rect = generatedSlots[i].GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(slotSize, slotSize);
            rect.anchoredPosition = new Vector2(
                horizontalPadding + column * (slotSize + spacing),
                -topPadding - row * (slotSize + spacing));
        }
    }

    private void CreateTitleIfNeeded()
    {
        Transform existing = transform.Find("CareItemsTitle");
        titleText = existing != null ? existing.GetComponent<TMP_Text>() : CreateText(transform, "CareItemsTitle", 25f, TextAlignmentOptions.MidlineLeft);
        titleText.text = "CARE ITEMS";
        titleText.fontStyle = FontStyles.Bold;
        SetAnchors(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(22f, -44f), new Vector2(-12f, -4f));

        Transform existingBuffs = transform.Find("ActiveBuffsSummary");
        activeBuffsText = existingBuffs != null ? existingBuffs.GetComponent<TMP_Text>() : CreateText(transform, "ActiveBuffsSummary", 18f, TextAlignmentOptions.MidlineRight);
        activeBuffsText.fontStyle = FontStyles.Bold;
        activeBuffsText.color = new Color(0.4f, 0.95f, 0.6f, 1f);
        SetAnchors(activeBuffsText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(200f, -44f), new Vector2(-22f, -4f));
        if (ActiveItemEffectManager.Instance != null)
        {
            activeBuffsText.text = ActiveItemEffectManager.Instance.GetSummaryText();
        }
    }

    private void HideLegacyButtons()
    {
        foreach (InventoryItemButton button in GetComponentsInChildren<InventoryItemButton>(true))
        {
            if (!button.name.StartsWith("DynamicSlot_")) button.gameObject.SetActive(false);
        }
    }

    private void ClearGeneratedSlots()
    {
        foreach (GameObject slot in generatedSlots)
            if (slot != null) Destroy(slot);
        generatedSlots.Clear();
    }

    private Image CreateImage(Transform parent, string objectName)
    {
        GameObject go = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        return go.GetComponent<Image>();
    }

    private TMP_Text CreateText(Transform parent, string objectName, float size, TextAlignmentOptions alignment)
    {
        GameObject go = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.font = font != null ? font : TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.enableAutoSizing = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.color = Color.white;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
