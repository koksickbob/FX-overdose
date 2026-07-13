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
    }

    private void BuyItem()
    {
        if (shopManager == null || item == null)
        {
            Debug.LogWarning("[ShopItemButton] ShopManager 또는 ItemData가 연결되지 않았습니다.", this);
            return;
        }

        shopManager.BuyItem(item);
    }
}
