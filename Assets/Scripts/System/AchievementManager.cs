using System;
using System.Collections.Generic;
using UnityEngine;

namespace FXOverdose.Core
{
    public class AchievementManager : MonoBehaviour
    {
        // TEMP: 코스튬/음식 연출 전수 검수용. 테스트 종료 후 false로 되돌립니다.
        private static readonly bool DisableAchievementRequirementsForTesting = false;
        // TEMP REVIEW: 메이드 스킨 검수 종료 후 false로 되돌립니다.
        private static readonly bool UnlockMaidAchievementForReview = false;
        private const string Pref_MaidReviewRollbackApplied = "ReviewRollback_MaidGrant_v1";

        public enum AchievementType
        {
            Ending,
            ItemUsage,
            ItemPurchase,
            PeakBalance,
            RiskyEventSuccess,
            LevelUp
        }

        [Serializable]
        public class AchievementDefinition
        {
            public string Id;
            public string Title;
            public string Description;
            public AchievementType Type;
            public float TargetValue;
            public string StringParameter; // e.g. Specific EndingType name, ItemId
            public string RewardCostumeId; // Which costume gets unlocked
        }

        public static AchievementManager Instance { get; private set; }

        private List<AchievementDefinition> achievements = new List<AchievementDefinition>();
        private HashSet<string> unlockedAchievements = new HashSet<string>();

        // We use PlayerPrefs to store the global variables.
        private const string Pref_UnlockedPrefix = "Achieve_Unlock_";
        private const string Pref_EnergyDrinkUsed = "Stat_EnergyDrinkUsed";
        private const string Pref_ParfaitUsed = "Stat_ParfaitUsed";
        private const string Pref_MaratangUsed = "Stat_MaratangUsed";
        private const string Pref_SushiUsed = "Stat_SushiUsed";
        private const string Pref_TteokbokkiUsed = "Stat_TteokbokkiUsed";
        private const string Pref_ConsumablesPurchased = "Stat_ConsumablesPurchased";
        private const string Pref_RiskyEventSuccess = "Stat_RiskyEventSuccess";
        private const string Pref_PeakBalance = "Stat_GlobalPeakBalance";
        
        // For endings
        private const string Pref_EndingFirstGameOver = "Stat_EndingFirstGameOver";
        private const string Pref_EndingTrueClear = "Stat_EndingTrueClear";
        private const string Pref_EndingBankruptcy = "Stat_EndingBankruptcy";
        private const string Pref_EndingOverdose = "Stat_EndingOverdose";

        private const string Pref_Level9Reached = "Stat_Level9Reached";
        private const string Pref_AllSkillsMaxed = "Stat_AllSkillsMaxed";
        private const string Pref_HighestLevel = "Stat_HighestLevel";

        public event Action OnAchievementsChanged;
        public event Action<AchievementDefinition> OnAchievementUnlocked;

        public bool IsAchievementUnlocked(string achievementId)
        {
            return !string.IsNullOrEmpty(achievementId) && unlockedAchievements.Contains(achievementId);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            
            InitializeAchievements();
            LoadUnlockedAchievements();
        }

        private void InitializeAchievements()
        {
            achievements.Clear();
            // Populating the achievements
            achievements.Add(new AchievementDefinition { Id = "ending_first_gameover", Title = "첫 쓴맛", Description = "최초 게임 오버 달성", Type = AchievementType.Ending, StringParameter = "FirstGameOver" });
            achievements.Add(new AchievementDefinition { Id = "ending_true_clear", Title = "자본주의의 기적", Description = "게임 최초 클리어 (진엔딩 달성)", Type = AchievementType.Ending, StringParameter = "TrueClear", RewardCostumeId = CostumeManager.DongtanLookId });
            achievements.Add(new AchievementDefinition { Id = "ending_bankruptcy", Title = "빈털터리", Description = "배드 엔딩 - 파산 엔딩 달성", Type = AchievementType.Ending, StringParameter = "Bankruptcy" });
            achievements.Add(new AchievementDefinition { Id = "ending_overdose", Title = "과부하", Description = "배드 엔딩 - 오버도즈 엔딩 달성", Type = AchievementType.Ending, StringParameter = "Overdose", RewardCostumeId = CostumeManager.PajamaId });
            
            achievements.Add(new AchievementDefinition { Id = "level_master", Title = "트레이딩 마스터", Description = "모든 스킬 레벨 10 달성", Type = AchievementType.LevelUp, RewardCostumeId = CostumeManager.OfficeLookId });
            achievements.Add(new AchievementDefinition { Id = "use_energy_drink_50", Title = "카페인 중독 I", Description = "에너지 드링크 총 50개 사용", Type = AchievementType.ItemUsage, StringParameter = "EnergyDrink", TargetValue = 50, RewardCostumeId = "street_cap" });
            achievements.Add(new AchievementDefinition { Id = "use_energy_drink_100", Title = "카페인 중독 II", Description = "에너지 드링크 총 100개 사용", Type = AchievementType.ItemUsage, StringParameter = "EnergyDrink", TargetValue = 100, RewardCostumeId = CostumeManager.BartenderId });
            achievements.Add(new AchievementDefinition { Id = "use_parfait_100", Title = "당분 중독", Description = "파르페 총 100개 사용", Type = AchievementType.ItemUsage, StringParameter = "Parfait", TargetValue = 100, RewardCostumeId = CostumeManager.MaidId });
            achievements.Add(new AchievementDefinition { Id = "use_maratang_50", Title = "마라탕 중독자", Description = "마라탕 총 50개 사용", Type = AchievementType.ItemUsage, StringParameter = "Maratang", TargetValue = 50, RewardCostumeId = CostumeManager.QipaoId });
            achievements.Add(new AchievementDefinition { Id = "use_sushi_50", Title = "초밥 중독자", Description = "초밥 총 50개 사용", Type = AchievementType.ItemUsage, StringParameter = "Sushi", TargetValue = 50, RewardCostumeId = CostumeManager.YukataId });
            achievements.Add(new AchievementDefinition { Id = "use_tteokbokki_50", Title = "떡볶이 중독자", Description = "떡볶이 총 50개 사용", Type = AchievementType.ItemUsage, StringParameter = "Tteokbokki", TargetValue = 50, RewardCostumeId = CostumeManager.HanbokId });
            achievements.Add(new AchievementDefinition { Id = "purchase_delivery_200", Title = "큰손 고객", Description = "배달음식 구매 수량 총 200개 돌파", Type = AchievementType.ItemPurchase, TargetValue = 200 });
            
            achievements.Add(new AchievementDefinition { Id = "risky_event_success_20", Title = "하이 리스크 하이 리턴", Description = "돌발 이벤트에서 위험 선택지를 선택하여 총 20번 성공", Type = AchievementType.RiskyEventSuccess, TargetValue = 20, RewardCostumeId = "jirai_kei" });
            
            achievements.Add(new AchievementDefinition { Id = "balance_1m", Title = "백만장자", Description = "누적 최고 자산 100만 달러 달성", Type = AchievementType.PeakBalance, TargetValue = 1000000 });
            achievements.Add(new AchievementDefinition { Id = "balance_25m", Title = "억만장자의 길 I", Description = "누적 최고 자산 2,500만 달러 달성", Type = AchievementType.PeakBalance, TargetValue = 25000000, RewardCostumeId = CostumeManager.NurseId });
            achievements.Add(new AchievementDefinition { Id = "balance_50m", Title = "억만장자의 길 II", Description = "누적 최고 자산 5,000만 달러 달성", Type = AchievementType.PeakBalance, TargetValue = 50000000, RewardCostumeId = "bunny_girl,bikini" });
        }

        private void LoadUnlockedAchievements()
        {
            if (!UnlockMaidAchievementForReview && PlayerPrefs.GetInt(Pref_MaidReviewRollbackApplied, 0) == 0)
            {
                // 검수 플래그가 강제로 기록했던 해금만 제거합니다. 정상 달성 기록과 사용 횟수는 보존됩니다.
                if (PlayerPrefs.GetInt(Pref_ParfaitUsed, 0) < 100)
                    PlayerPrefs.DeleteKey(Pref_UnlockedPrefix + "use_parfait_100");
                PlayerPrefs.SetInt(Pref_MaidReviewRollbackApplied, 1);
                PlayerPrefs.Save();
            }

            unlockedAchievements.Clear();
            foreach (var ach in achievements)
            {
                if (PlayerPrefs.GetInt(Pref_UnlockedPrefix + ach.Id, 0) == 1)
                {
                    unlockedAchievements.Add(ach.Id);
                }
            }

            if (UnlockMaidAchievementForReview)
            {
                const string maidAchievementId = "use_parfait_100";
                unlockedAchievements.Add(maidAchievementId);
                PlayerPrefs.SetInt(Pref_UnlockedPrefix + maidAchievementId, 1);
                PlayerPrefs.Save();
            }
        }

        private void UnlockAchievement(AchievementDefinition ach)
        {
            if (!unlockedAchievements.Contains(ach.Id))
            {
                unlockedAchievements.Add(ach.Id);
                PlayerPrefs.SetInt(Pref_UnlockedPrefix + ach.Id, 1);
                PlayerPrefs.Save();
                
                Debug.Log($"[AchievementManager] 업적 달성: {ach.Title} - {ach.Description}");
                OnAchievementsChanged?.Invoke();
                OnAchievementUnlocked?.Invoke(ach);
            }
        }

        private void CheckAchievements()
        {
            foreach (var ach in achievements)
            {
                if (unlockedAchievements.Contains(ach.Id)) continue;

                bool isMet = false;
                switch (ach.Type)
                {
                    case AchievementType.Ending:
                        if (ach.StringParameter == "FirstGameOver" && PlayerPrefs.GetInt(Pref_EndingFirstGameOver, 0) >= 1) isMet = true;
                        if (ach.StringParameter == "TrueClear" && PlayerPrefs.GetInt(Pref_EndingTrueClear, 0) >= 1) isMet = true;
                        if (ach.StringParameter == "Bankruptcy" && PlayerPrefs.GetInt(Pref_EndingBankruptcy, 0) >= 1) isMet = true;
                        if (ach.StringParameter == "Overdose" && PlayerPrefs.GetInt(Pref_EndingOverdose, 0) >= 1) isMet = true;
                        break;
                    case AchievementType.ItemUsage:
                        if (ach.StringParameter == "EnergyDrink" && PlayerPrefs.GetInt(Pref_EnergyDrinkUsed, 0) >= ach.TargetValue) isMet = true;
                        if (ach.StringParameter == "Parfait" && PlayerPrefs.GetInt(Pref_ParfaitUsed, 0) >= ach.TargetValue) isMet = true;
                        if (ach.StringParameter == "Maratang" && PlayerPrefs.GetInt(Pref_MaratangUsed, 0) >= ach.TargetValue) isMet = true;
                        if (ach.StringParameter == "Sushi" && PlayerPrefs.GetInt(Pref_SushiUsed, 0) >= ach.TargetValue) isMet = true;
                        if (ach.StringParameter == "Tteokbokki" && PlayerPrefs.GetInt(Pref_TteokbokkiUsed, 0) >= ach.TargetValue) isMet = true;
                        break;
                    case AchievementType.ItemPurchase:
                        if (PlayerPrefs.GetInt(Pref_ConsumablesPurchased, 0) >= ach.TargetValue) isMet = true;
                        break;
                    case AchievementType.PeakBalance:
                        if (PlayerPrefs.GetFloat(Pref_PeakBalance, 0f) >= ach.TargetValue) isMet = true;
                        break;
                    case AchievementType.RiskyEventSuccess:
                        if (PlayerPrefs.GetInt(Pref_RiskyEventSuccess, 0) >= ach.TargetValue) isMet = true;
                        break;
                    case AchievementType.LevelUp:
                        if (PlayerPrefs.GetInt(Pref_AllSkillsMaxed, 0) == 1) isMet = true;
                        break;
                }

                if (isMet)
                {
                    UnlockAchievement(ach);
                }
            }
        }

        public void RecordEnding(string endingType)
        {
            bool isFirstGameOver = PlayerPrefs.GetInt(Pref_EndingFirstGameOver, 0) == 0;
            if (endingType != "Success")
            {
                if (isFirstGameOver)
                {
                    PlayerPrefs.SetInt(Pref_EndingFirstGameOver, 1);
                }
            }
            
            if (endingType == "Success") PlayerPrefs.SetInt(Pref_EndingTrueClear, 1);
            else if (endingType == "Bankruptcy") PlayerPrefs.SetInt(Pref_EndingBankruptcy, PlayerPrefs.GetInt(Pref_EndingBankruptcy, 0) + 1);
            else if (endingType == "Overdose") PlayerPrefs.SetInt(Pref_EndingOverdose, PlayerPrefs.GetInt(Pref_EndingOverdose, 0) + 1);
            
            PlayerPrefs.Save();
            CheckAchievements();
        }

        public void RecordPeakBalance(float balance)
        {
            float currentPeak = PlayerPrefs.GetFloat(Pref_PeakBalance, 0f);
            if (balance > currentPeak)
            {
                PlayerPrefs.SetFloat(Pref_PeakBalance, balance);
                PlayerPrefs.Save();
                CheckAchievements();
            }
        }

        public void RecordItemUsage(string itemId)
        {
            if (!string.IsNullOrEmpty(itemId))
            {
                string lowerId = itemId.ToLowerInvariant();
                if (lowerId.Contains("energy"))
                {
                    int count = PlayerPrefs.GetInt(Pref_EnergyDrinkUsed, 0) + 1;
                    PlayerPrefs.SetInt(Pref_EnergyDrinkUsed, count);
                }
                else if (lowerId.Contains("parfait"))
                {
                    int count = PlayerPrefs.GetInt(Pref_ParfaitUsed, 0) + 1;
                    PlayerPrefs.SetInt(Pref_ParfaitUsed, count);
                }
                else if (lowerId.Contains("malatang"))
                {
                    int count = PlayerPrefs.GetInt(Pref_MaratangUsed, 0) + 1;
                    PlayerPrefs.SetInt(Pref_MaratangUsed, count);
                }
                else if (lowerId.Contains("sushi"))
                {
                    int count = PlayerPrefs.GetInt(Pref_SushiUsed, 0) + 1;
                    PlayerPrefs.SetInt(Pref_SushiUsed, count);
                }
                else if (lowerId.Contains("tteokbokki"))
                {
                    int count = PlayerPrefs.GetInt(Pref_TteokbokkiUsed, 0) + 1;
                    PlayerPrefs.SetInt(Pref_TteokbokkiUsed, count);
                }
                PlayerPrefs.Save();
                CheckAchievements();
            }
        }

        public void RecordItemPurchase()
        {
            int count = PlayerPrefs.GetInt(Pref_ConsumablesPurchased, 0) + 1;
            PlayerPrefs.SetInt(Pref_ConsumablesPurchased, count);
            PlayerPrefs.Save();
            CheckAchievements();
        }

        public void RecordRiskyEventSuccess()
        {
            int count = PlayerPrefs.GetInt(Pref_RiskyEventSuccess, 0) + 1;
            PlayerPrefs.SetInt(Pref_RiskyEventSuccess, count);
            PlayerPrefs.Save();
            CheckAchievements();
        }

        public void RecordLevelUp(int level)
        {
            int highestLevel = Mathf.Max(PlayerPrefs.GetInt(Pref_HighestLevel, 0), level);
            PlayerPrefs.SetInt(Pref_HighestLevel, highestLevel);
            if (level >= 9)
            {
                PlayerPrefs.SetInt(Pref_Level9Reached, 1);
            }
            PlayerPrefs.Save();
            CheckAchievements();
        }

        public void RecordSkillLevelUp()
        {
            if (FXOverdose.Trading.TraderLevelSystem.Instance != null)
            {
                if (FXOverdose.Trading.TraderLevelSystem.Instance.ChartStudyLevel >= 10 &&
                    FXOverdose.Trading.TraderLevelSystem.Instance.CubePatienceLevel >= 10 &&
                    FXOverdose.Trading.TraderLevelSystem.Instance.BookJudgmentLevel >= 10)
                {
                    PlayerPrefs.SetInt(Pref_AllSkillsMaxed, 1);
                }
            }
            PlayerPrefs.Save();
            CheckAchievements();
        }

        public bool IsCostumeUnlocked(string costumeId, out string requirementText)
        {
            requirementText = string.Empty;
            if (DisableAchievementRequirementsForTesting) return true;

            foreach (var ach in achievements)
            {
                if (!string.IsNullOrEmpty(ach.RewardCostumeId) && Array.IndexOf(ach.RewardCostumeId.Split(','), costumeId) >= 0)
                {
                    if (unlockedAchievements.Contains(ach.Id))
                    {
                        return true;
                    }
                    else
                    {
                        requirementText = ach.Title;
                        return false;
                    }
                }
            }
            
            return true;
        }

        public IReadOnlyList<AchievementDefinition> GetAllAchievements()
        {
            return achievements.AsReadOnly();
        }

        public bool IsUnlocked(string id)
        {
            return unlockedAchievements.Contains(id);
        }

        public float GetProgress01(AchievementDefinition achievement)
        {
            if (achievement == null) return 0f;
            if (unlockedAchievements.Contains(achievement.Id)) return 1f;

            float current = 0f;
            float target = achievement.TargetValue > 0f ? achievement.TargetValue : 1f;
            switch (achievement.Type)
            {
                case AchievementType.Ending:
                    if (achievement.StringParameter == "FirstGameOver") current = PlayerPrefs.GetInt(Pref_EndingFirstGameOver, 0);
                    else if (achievement.StringParameter == "TrueClear") current = PlayerPrefs.GetInt(Pref_EndingTrueClear, 0);
                    else if (achievement.StringParameter == "Bankruptcy") current = PlayerPrefs.GetInt(Pref_EndingBankruptcy, 0);
                    else if (achievement.StringParameter == "Overdose") current = PlayerPrefs.GetInt(Pref_EndingOverdose, 0);
                    break;
                case AchievementType.ItemUsage:
                    if (achievement.StringParameter == "EnergyDrink") current = PlayerPrefs.GetInt(Pref_EnergyDrinkUsed, 0);
                    else if (achievement.StringParameter == "Parfait") current = PlayerPrefs.GetInt(Pref_ParfaitUsed, 0);
                    else if (achievement.StringParameter == "Maratang") current = PlayerPrefs.GetInt(Pref_MaratangUsed, 0);
                    else if (achievement.StringParameter == "Sushi") current = PlayerPrefs.GetInt(Pref_SushiUsed, 0);
                    else if (achievement.StringParameter == "Tteokbokki") current = PlayerPrefs.GetInt(Pref_TteokbokkiUsed, 0);
                    break;
                case AchievementType.ItemPurchase:
                    current = PlayerPrefs.GetInt(Pref_ConsumablesPurchased, 0);
                    break;
                case AchievementType.PeakBalance:
                    current = PlayerPrefs.GetFloat(Pref_PeakBalance, 0f);
                    break;
                case AchievementType.RiskyEventSuccess:
                    current = PlayerPrefs.GetInt(Pref_RiskyEventSuccess, 0);
                    break;
                case AchievementType.LevelUp:
                    current = PlayerPrefs.GetInt(Pref_AllSkillsMaxed, 0);
                    break;
            }
            return Mathf.Clamp01(current / target);
        }

        [ContextMenu("Reset Achievements")]
        public void ResetAchievements()
        {
            PlayerPrefs.DeleteKey(Pref_EnergyDrinkUsed);
            PlayerPrefs.DeleteKey(Pref_ParfaitUsed);
            PlayerPrefs.DeleteKey(Pref_MaratangUsed);
            PlayerPrefs.DeleteKey(Pref_SushiUsed);
            PlayerPrefs.DeleteKey(Pref_TteokbokkiUsed);
            PlayerPrefs.DeleteKey(Pref_ConsumablesPurchased);
            PlayerPrefs.DeleteKey(Pref_RiskyEventSuccess);
            PlayerPrefs.DeleteKey(Pref_PeakBalance);
            PlayerPrefs.DeleteKey(Pref_EndingFirstGameOver);
            PlayerPrefs.DeleteKey(Pref_EndingTrueClear);
            PlayerPrefs.DeleteKey(Pref_EndingBankruptcy);
            PlayerPrefs.DeleteKey(Pref_EndingOverdose);
            PlayerPrefs.DeleteKey(Pref_Level9Reached);
            PlayerPrefs.DeleteKey(Pref_AllSkillsMaxed);
            PlayerPrefs.DeleteKey(Pref_HighestLevel);
            
            foreach (var ach in achievements)
            {
                PlayerPrefs.DeleteKey(Pref_UnlockedPrefix + ach.Id);
            }
            PlayerPrefs.Save();
            LoadUnlockedAchievements();
            OnAchievementsChanged?.Invoke();
            Debug.Log("[AchievementManager] All achievements reset.");
        }
    }
}
