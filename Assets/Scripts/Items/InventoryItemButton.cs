using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인벤토리 아이템 하나를 표시하고 버튼 클릭으로 사용합니다.
/// 같은 컴포넌트를 여러 버튼에 붙여 서로 다른 ItemData를 연결할 수 있습니다.
/// </summary>
[RequireComponent(typeof(Button))]
public class InventoryItemButton : MonoBehaviour
{
    [Header("아이템 연결")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private ItemData item;

    [Header("버튼 UI")]
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text quantityText;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        button.onClick.AddListener(UseItem);
        ApplyItemInformation();
    }

    /// <summary>동적 인벤토리 UI가 생성한 슬롯에 데이터와 표시 요소를 연결합니다.</summary>
    public void Configure(
        Inventory targetInventory,
        ItemData targetItem,
        Button targetButton,
        Image targetIcon,
        TMP_Text targetName,
        TMP_Text targetQuantity)
    {
        inventory = targetInventory;
        item = targetItem;
        button = targetButton;
        iconImage = targetIcon;
        nameText = targetName;
        quantityText = targetQuantity;
        ApplyItemInformation();
        RefreshQuantity();
    }

    private void OnEnable()
    {
        if (inventory != null)
        {
            inventory.QuantityChanged += OnQuantityChanged;
        }

        RefreshQuantity();
    }

    private void OnDisable()
    {
        if (inventory != null)
        {
            inventory.QuantityChanged -= OnQuantityChanged;
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(UseItem);
        }
    }

    // ItemData의 이름과 아이콘을 UI에 반영합니다.
    private void ApplyItemInformation()
    {
        if (item == null)
        {
            return;
        }

        if (nameText != null)
        {
            nameText.text = item.ItemName;
        }

        if (iconImage != null)
        {
            iconImage.sprite = item.Icon;
            iconImage.enabled = item.Icon != null;
        }
    }

    // 버튼을 누르면 Inventory에 아이템 사용을 요청합니다.
    private void UseItem()
    {
        if (inventory == null || item == null)
        {
            Debug.LogWarning("[InventoryItemButton] Inventory 또는 ItemData가 연결되지 않았습니다.", this);
            return;
        }

        inventory.UseItem(item);
        RefreshQuantity();
    }

    // 이 버튼이 표시하는 아이템 수량이 바뀌었을 때만 UI를 갱신합니다.
    private void OnQuantityChanged(ItemData changedItem, int quantity)
    {
        if (changedItem != item)
        {
            return;
        }

        SetQuantity(quantity);
    }

    private void RefreshQuantity()
    {
        int quantity = inventory != null && item != null
            ? inventory.GetQuantity(item)
            : 0;

        SetQuantity(quantity);
    }

    private void SetQuantity(int quantity)
    {
        if (quantityText != null)
        {
            quantityText.text = $"×{quantity}";
        }

        if (button != null)
        {
            button.interactable = quantity > 0;
        }
    }
}
