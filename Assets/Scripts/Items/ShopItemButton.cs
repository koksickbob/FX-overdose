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

    private void OnEnable()
    {
        if (ActiveItemEffectManager.Instance != null)
        {
            ActiveItemEffectManager.Instance.OnActiveItemsChanged -= Refresh;
            ActiveItemEffectManager.Instance.OnActiveItemsChanged += Refresh;
        }
        if (inventory != null)
        {
            inventory.QuantityChanged -= OnInventoryChanged;
            inventory.QuantityChanged += OnInventoryChanged;
        }
        Refresh();
    }

    private void OnDisable()
    {
        if (ActiveItemEffectManager.Instance != null)
        {
            ActiveItemEffectManager.Instance.OnActiveItemsChanged -= Refresh;
        }
        if (inventory != null)
        {
            inventory.QuantityChanged -= OnInventoryChanged;
        }
    }

    private void OnInventoryChanged(ItemData changedItem, int qty)
    {
        if (changedItem == item) Refresh();
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

    public void Refresh()
    {
        if (item == null)
        {
            return;
        }

        if (nameText != null)
        {
            nameText.text = item.ItemName;
        }

        bool isActive = item.IsActiveItem;
        bool isMax = isActive && ActiveItemEffectManager.Instance != null && ActiveItemEffectManager.Instance.IsMaxLevel(item);
        bool isSteakBlocked = false;
        string steakReason = string.Empty;
        if (item.ItemId == "steak" && shopManager != null && shopManager.GameManager != null)
        {
            isSteakBlocked = !DeliveryFoodManager.EnsureInstance()
                .CanPurchaseSteak(shopManager.GameManager.CurrentDay, out steakReason);
        }

        if (button != null)
        {
            button.interactable = !isMax && !isSteakBlocked;
            TMP_Text buttonLabel = button.GetComponentInChildren<TMP_Text>();
            if (buttonLabel != null)
            {
                ConfigureContainedLabel(buttonLabel, 23f, 12f, 8f);
                if (isSteakBlocked)
                {
                    buttonLabel.text = "잠김";
                }
                else if (isMax)
                {
                    buttonLabel.text = item.MaxLevel <= 1 ? "활성화 ✓" : "최대 레벨 ✓";
                }
                else
                {
                    buttonLabel.text = "구매";
                }
            }
        }

        if (priceText != null)
        {
            priceText.enableAutoSizing = false;
            priceText.fontSize = 23f;
            priceText.fontSizeMin = 23f;
            priceText.fontSizeMax = 23f;
            priceText.textWrappingMode = TextWrappingModes.NoWrap;
            priceText.overflowMode = TextOverflowModes.Ellipsis;
            if (isSteakBlocked)
            {
                priceText.text = "잠김";
            }
            else if (isMax)
            {
                priceText.text = "최대 레벨";
            }
            else if (isActive && ActiveItemEffectManager.Instance != null)
            {
                int nextPrice = ActiveItemEffectManager.Instance.GetNextUpgradePrice(item);
                priceText.text = $"${nextPrice:N0}";
            }
            else
            {
                int displayPrice = shopManager != null ? shopManager.GetInflatedPrice(item) : item.Price;
                priceText.text = $"${displayPrice:N0}";
            }
        }

        if (ownedText != null)
        {
            ConfigureContainedLabel(ownedText, 17f, 10f, 4f);
            if (isSteakBlocked)
            {
                ownedText.text = steakReason;
            }
            else if (isActive && ActiveItemEffectManager.Instance != null)
            {
                ownedText.text = ActiveItemEffectManager.Instance.GetItemStatusLabel(item);
            }
            else
            {
                int owned = inventory != null ? inventory.GetQuantity(item) : 0;
                ownedText.text = $"보유 x{owned}";
            }
        }
    }

    private static void ConfigureContainedLabel(TMP_Text text, float maxSize, float minSize, float horizontalMargin)
    {
        text.enableAutoSizing = true;
        text.fontSizeMax = maxSize;
        text.fontSizeMin = minSize;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.margin = new Vector4(horizontalMargin, 1f, horizontalMargin, 1f);
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
