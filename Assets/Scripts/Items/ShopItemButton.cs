using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상점에서 아이템 정보와 가격을 표시하고 구매를 요청합니다.
/// </summary>
[RequireComponent(typeof(Button))]
public class ShopItemButton : MonoBehaviour
{
    [SerializeField] private ShopManager shopManager;
    [SerializeField] private ItemData item;
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text ownedText;
    [SerializeField] private Inventory inventory;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        button.onClick.AddListener(BuyItem);
        Refresh();
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(BuyItem);
        }
    }

    public void Configure(
        ShopManager targetShop,
        Inventory targetInventory,
        ItemData targetItem,
        Button targetButton,
        TMP_Text targetName,
        TMP_Text targetPrice,
        TMP_Text targetOwned)
    {
        shopManager = targetShop;
        inventory = targetInventory;
        item = targetItem;
        button = targetButton;
        nameText = targetName;
        priceText = targetPrice;
        ownedText = targetOwned;
        Refresh();
    }

    private void Refresh()
    {
        if (item == null)
        {
            return;
        }

        if (nameText != null)
        {
            nameText.text = item.ItemName;
        }

        if (priceText != null)
        {
            priceText.text = $"${item.Price:N0}";
        }

        if (ownedText != null)
        {
            int owned = inventory != null ? inventory.GetQuantity(item) : 0;
            ownedText.text = $"OWNED x{owned}";
        }
    }

    private void BuyItem()
    {
        if (shopManager == null || item == null)
        {
            Debug.LogWarning("[ShopItemButton] ShopManager 또는 ItemData가 연결되지 않았습니다.", this);
            return;
        }

        if (shopManager.BuyItem(item)) Refresh();
    }
}
