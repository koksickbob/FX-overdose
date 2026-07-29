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
        [SerializeField] private UnityEngine.UI.Image background;
        [SerializeField] private UnityEngine.UI.Image progressFill;
        [SerializeField] private TMP_Text progressText;

        public void Configure(
            TMP_Text title,
            TMP_Text description,
            TMP_Text reward,
            GameObject locked,
            UnityEngine.UI.Image cardBackground,
            UnityEngine.UI.Image fill,
            TMP_Text percentage)
        {
            titleText = title;
            descText = description;
            rewardText = reward;
            lockedOverlay = locked;
            background = cardBackground;
            progressFill = fill;
            progressText = percentage;
        }

        public void Setup(AchievementManager.AchievementDefinition ach, bool isUnlocked)
        {
            float progress = AchievementManager.Instance != null
                ? AchievementManager.Instance.GetProgress01(ach)
                : (isUnlocked ? 1f : 0f);
            if (progressFill != null)
            {
                RectTransform fillRect = progressFill.rectTransform;
                fillRect.anchorMax = new Vector2(progress, 1f);
                progressFill.color = isUnlocked
                    ? new Color32(34, 197, 94, 255)
                    : new Color32(6, 182, 212, 255);
            }
            if (progressText != null) progressText.text = $"{Mathf.RoundToInt(progress * 100f)}%";

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
                if (background != null) background.color = new Color32(13, 45, 54, 255);
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
                if (background != null) background.color = new Color32(16, 29, 48, 255);
            }
        }
    }
}
