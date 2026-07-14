using UnityEngine;

/// <summary>동적으로 커지는 인벤토리 패널 위에 SHOP 버튼을 유지합니다.</summary>
[RequireComponent(typeof(RectTransform))]
public class ShopButtonInventoryFollower : MonoBehaviour
{
    [SerializeField] private RectTransform inventoryPanel;
    [SerializeField] private float gap = 12f;
    [SerializeField] private float rightMargin = 24f;

    private RectTransform buttonRect;
    private Vector2 lastInventorySize;

    private void Awake()
    {
        buttonRect = GetComponent<RectTransform>();
        UpdatePosition();
    }

    private void LateUpdate()
    {
        if (inventoryPanel != null && inventoryPanel.sizeDelta != lastInventorySize) UpdatePosition();
    }

    public void Configure(RectTransform targetInventoryPanel)
    {
        inventoryPanel = targetInventoryPanel;
        if (buttonRect == null) buttonRect = GetComponent<RectTransform>();
        UpdatePosition();
    }

    private void UpdatePosition()
    {
        if (inventoryPanel == null || buttonRect == null) return;
        buttonRect.anchorMin = new Vector2(1f, 0f);
        buttonRect.anchorMax = new Vector2(1f, 0f);
        buttonRect.pivot = new Vector2(1f, 0f);
        buttonRect.anchoredPosition = new Vector2(-rightMargin, inventoryPanel.anchoredPosition.y + inventoryPanel.rect.height + gap);
        lastInventorySize = inventoryPanel.sizeDelta;
    }
}
