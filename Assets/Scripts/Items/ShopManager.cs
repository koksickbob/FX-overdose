using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FXOverdose.Core;

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

    [Header("판매 상품 (추가/삭제 시 상점 UI 자동 반영)")]
    [SerializeField] private List<ItemData> catalogItems = new();

    private bool pausedByShop;

    public bool IsOpen => shopPanel != null && shopPanel.activeSelf;
    public IReadOnlyList<ItemData> CatalogItems => catalogItems;
    public Inventory Inventory => inventory;
    public GameManager GameManager => gameManager;
    public CostumeManager Costumes => CostumeManager.Instance;

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
    /// 현재 일차에 비례하여 인플레이션(물가 상승)이 적용된 가격을 반환합니다. (2일마다 20% 상승)
    /// </summary>
    public int GetInflatedPrice(ItemData item)
    {
        if (item == null) return 0;
        
        float basePrice = item.Price;
        if (gameManager != null)
        {
            float inflationMultiplier = Mathf.Pow(1.2f, (gameManager.CurrentDay - 1) / 2f);
            return Mathf.RoundToInt(basePrice * inflationMultiplier);
        }
        return Mathf.RoundToInt(basePrice);
    }

    /// <summary>
    /// 자산이 충분하면 가격을 차감하고 아이템을 지급 또는 업그레이드합니다.
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

        int priceToSpend = GetInflatedPrice(item);
        if (item.IsActiveItem && ActiveItemEffectManager.Instance != null)
        {
            if (ActiveItemEffectManager.Instance.IsMaxLevel(item))
            {
                Debug.Log($"[ShopManager] {item.ItemName}은(는) 이미 최대 구매 제한에 도달했습니다.");
                return false;
            }
            priceToSpend = ActiveItemEffectManager.Instance.GetNextUpgradePrice(item);
        }

        if (priceToSpend <= 0)
        {
            Debug.LogWarning($"[ShopManager] {item.ItemName}의 가격이 올바르지 않습니다.", item);
            return false;
        }

        if (!gameManager.TrySpendBalance(priceToSpend))
        {
            Debug.Log($"[ShopManager] 자산 부족: {item.ItemName} 구매 실패 (필요 자산: {priceToSpend:N0})");
            return false;
        }

        if (item.IsActiveItem && ActiveItemEffectManager.Instance != null)
        {
            ActiveItemEffectManager.Instance.UpgradeOrActivateItem(item);
            Debug.Log($"[ShopManager] 액티브 아이템 {item.ItemName} 활성/업그레이드 완료");
        }
        else
        {
            inventory.AddItem(item);
            Debug.Log($"[ShopManager] 소모형 아이템 {item.ItemName} 구매 완료");
        }

        AudioManager.Play(AudioCue.ShopPurchase);
        return true;
    }

    public bool BuyCostume(string costumeId)
    {
        if (!IsOpen || gameManager == null || CostumeManager.Instance == null) return false;
        bool purchased = CostumeManager.Instance.Purchase(costumeId, gameManager);
        if (purchased) AudioManager.Play(AudioCue.CostumePurchase);
        return purchased;
    }

    public bool EquipCostume(string costumeId)
    {
        if (CostumeManager.Instance == null) return false;
        bool equipped = CostumeManager.Instance.Equip(costumeId);
        if (equipped) AudioManager.Play(AudioCue.CostumeEquip);
        return equipped;
    }
}
