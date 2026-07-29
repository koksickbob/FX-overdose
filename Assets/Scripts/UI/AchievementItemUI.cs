using UnityEngine;
using TMPro;
using FXOverdose.Core;

namespace FXOverdose.UI
{
    public class AchievementItemUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descText;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private GameObject lockedOverlay;

        public void Setup(AchievementManager.AchievementDefinition ach, bool isUnlocked)
        {
            if (isUnlocked)
            {
                if (titleText != null) titleText.text = ach.Title;
                if (descText != null) descText.text = ach.Description;
                
                if (rewardText != null)
                {
                    if (!string.IsNullOrEmpty(ach.RewardCostumeId))
                    {
                        var costumeDef = CostumeManager.Instance?.GetDefinition(ach.RewardCostumeId);
                        rewardText.text = costumeDef != null ? $"보상: {costumeDef.DisplayName} 스킨" : "보상: 스킨";
                    }
                    else
                    {
                        rewardText.text = "";
                    }
                }
                
                if (lockedOverlay != null) lockedOverlay.SetActive(false);
            }
            else
            {
                if (titleText != null) titleText.text = "???";
                if (descText != null) descText.text = ach.Description; // 달성 조건만 표시
                
                if (rewardText != null)
                {
                    rewardText.text = string.IsNullOrEmpty(ach.RewardCostumeId) ? "" : "보상: ???";
                }
                
                if (lockedOverlay != null) lockedOverlay.SetActive(true);
            }
        }
    }
}
