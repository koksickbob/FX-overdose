using UnityEngine;
using UnityEngine.UI;
using FXOverdose.Core;

namespace FXOverdose.UI
{
    public class AchievementUIController : MonoBehaviour
    {
        [Header("UI References (필수 할당)")]
        [SerializeField] private GameObject overlay;
        [SerializeField] private Transform contentPanel;
        [SerializeField] private GameObject itemPrefab;
        [SerializeField] private Button closeButton;

        public static AchievementUIController Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void Start()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Close);
            }
            
            if (overlay != null)
            {
                overlay.SetActive(false);
            }
        }

        public void Open()
        {
            if (overlay != null) overlay.SetActive(true);
            if (overlay != null) overlay.transform.SetAsLastSibling();
            PopulateList();
        }

        public void Close()
        {
            if (overlay != null) overlay.SetActive(false);
        }

        private void PopulateList()
        {
            if (contentPanel == null || itemPrefab == null) return;
            if (AchievementManager.Instance == null) return;

            // 기존 아이템 제거
            foreach (Transform child in contentPanel)
            {
                Destroy(child.gameObject);
            }

            var allAchievements = AchievementManager.Instance.GetAllAchievements();
            foreach (var ach in allAchievements)
            {
                GameObject obj = Instantiate(itemPrefab, contentPanel);
                AchievementItemUI itemUI = obj.GetComponent<AchievementItemUI>();
                if (itemUI != null)
                {
                    bool isUnlocked = AchievementManager.Instance.IsUnlocked(ach.Id);
                    itemUI.Setup(ach, isUnlocked);
                }
            }
        }
    }
}
