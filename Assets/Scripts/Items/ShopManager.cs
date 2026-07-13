using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상점 열기와 닫기, 자산 차감, 구매 아이템 지급을 처리합니다.
/// </summary>
public class ShopManager : MonoBehaviour
{
    [Header("게임 시스템")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private Inventory inventory;

    [Header("상점 UI")]
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;

    private bool pausedByShop;

    public bool IsOpen => shopPanel != null && shopPanel.activeSelf;

    private void Awake()
    {
        if (openButton != null)
        {
            openButton.onClick.AddListener(OpenShop);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseShop);
        }

        if (shopPanel != null)
        {
            shopPanel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (openButton != null)
        {
            openButton.onClick.RemoveListener(OpenShop);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseShop);
        }
    }

    public void OpenShop()
    {
        if (shopPanel == null || gameManager == null)
        {
            Debug.LogWarning("[ShopManager] ShopPanel 또는 GameManager가 연결되지 않았습니다.", this);
            return;
        }

        if (gameManager.CurrentState == GameManager.GameState.GameOver)
        {
            return;
        }

        pausedByShop = gameManager.CurrentState == GameManager.GameState.Playing;

        if (pausedByShop)
        {
            gameManager.PauseGame();
        }

        shopPanel.SetActive(true);
    }

    public void CloseShop()
    {
        if (shopPanel != null)
        {
            shopPanel.SetActive(false);
        }

        if (pausedByShop && gameManager != null)
        {
            gameManager.ResumeGame();
        }

        pausedByShop = false;
    }

    /// <summary>
    /// 자산이 충분하면 가격을 차감하고 아이템을 한 개 지급합니다.
    /// </summary>
    public bool BuyItem(ItemData item)
    {
        if (!IsOpen)
        {
            return false;
        }

        if (gameManager == null || inventory == null || item == null)
        {
            Debug.LogWarning("[ShopManager] 구매에 필요한 참조가 연결되지 않았습니다.", this);
            return false;
        }

        if (item.Price <= 0)
        {
            Debug.LogWarning($"[ShopManager] {item.ItemName}의 가격이 올바르지 않습니다.", item);
            return false;
        }

        if (!gameManager.TrySpendBalance(item.Price))
        {
            Debug.Log($"[ShopManager] 자산 부족: {item.ItemName} 구매 실패");
            return false;
        }

        inventory.AddItem(item);
        Debug.Log($"[ShopManager] {item.ItemName} 구매 완료");
        return true;
    }
}
