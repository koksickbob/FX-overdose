using UnityEngine;
using System;
using System.Collections.Generic;
using FXOverdose.Trading;

namespace FXOverdose.Core
{
    public class BossLevelProvider : ITraderLevelProvider
    {
        private int bossSkillLevel;

        public BossLevelProvider(int skillLevel)
        {
            this.bossSkillLevel = Mathf.Clamp(skillLevel, 1, 10);
        }

        public int ChartStudyLevel => bossSkillLevel;

        public int GetMaxAllowedLeverage() => 50 + (bossSkillLevel * 5); // 50 to 100
        public float GetMaxAllowedMarginRatio() => 0.2f + (bossSkillLevel * 0.05f); // 0.25 to 0.7
        public float GetSignalAccuracy() => 0.6f + (bossSkillLevel * 0.03f); // 0.63 to 0.9
        public float GetTakeProfitMultiplier() => 1.0f + (bossSkillLevel * 0.1f); 
        public float GetEntryDelayPenaltyRatio() => Mathf.Max(0f, 0.5f - (bossSkillLevel * 0.05f)); 
        public float GetStopLossTightness() => Mathf.Max(0.01f, 0.1f - (bossSkillLevel * 0.005f));
    }

    [Serializable]
    public class BossData
    {
        public int Day;
        public string Name;
        public string Description;
        public float AssetScalePercentage; // 0.3 for 30%
        public int SkillLevel;
        public bool IsFinalBoss;
    }

    public class BossManager : MonoBehaviour
    {
        private static BossManager _instance;
        public static BossManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<BossManager>(FindObjectsInactive.Include);
                    if (_instance == null)
                    {
                        // Additive 로딩 중 활성 씬이 LoadingScene일 수 있으므로,
                        // 가능하면 GameManager에 붙여 반드시 게임 씬 소속으로 생성합니다.
                        GameManager gameManager = FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
                        if (gameManager != null)
                        {
                            _instance = gameManager.GetComponent<BossManager>();
                            if (_instance == null)
                                _instance = gameManager.gameObject.AddComponent<BossManager>();
                        }
                        else
                        {
                            GameObject go = new GameObject("BossManager");
                            _instance = go.AddComponent<BossManager>();
                            DontDestroyOnLoad(go);
                        }
                    }
                }
                return _instance;
            }
        }

        [SerializeField] private List<BossData> bossDatabase = new List<BossData>
        {
            new BossData { Day = 3, Name = "편의점 사장", Description = "과거 요미의 알바비를 떼먹은 악덕 편의점 점주", AssetScalePercentage = 0.3f, SkillLevel = 3, IsFinalBoss = false },
            new BossData { Day = 6, Name = "카페 사장", Description = "갑질을 일삼던 카페 사장", AssetScalePercentage = 0.4f, SkillLevel = 4, IsFinalBoss = false },
            new BossData { Day = 9, Name = "PC방 사장", Description = "야간 수당을 안주던 PC방 사장", AssetScalePercentage = 0.5f, SkillLevel = 5, IsFinalBoss = false },
            new BossData { Day = 12, Name = "고깃집 사장", Description = "불판 닦기를 강요하던 고깃집 사장", AssetScalePercentage = 0.6f, SkillLevel = 5, IsFinalBoss = false },
            new BossData { Day = 15, Name = "전화상담 사장", Description = "감정노동을 강요하던 콜센터 사장", AssetScalePercentage = 0.7f, SkillLevel = 6, IsFinalBoss = false },
            new BossData { Day = 18, Name = "화장품 매장 사장", Description = "외모 지적을 일삼던 화장품 매장 사장", AssetScalePercentage = 0.8f, SkillLevel = 7, IsFinalBoss = false },
            new BossData { Day = 20, Name = "사채업자", Description = "모든 빚의 원흉이자 최종 흑막", AssetScalePercentage = 0.9f, SkillLevel = 8, IsFinalBoss = true }
        };

        public BossData CurrentBoss { get; private set; }
        public float BossStartingAsset { get; private set; }
        public float BossCurrentAsset { get; private set; }
        public bool IsBossBankrupt { get; private set; }

        public float LoadedBossStartingAsset { get; set; } = -1f;
        public float LoadedBossCurrentAsset { get; set; } = -1f;
        
        private GameObject activeBossObject;
        public FXOverdose.AI.BossAIController CurrentBossAI { get; private set; }

        public event Action<float, float> OnBossAssetChanged; // current, starting
        public event Action OnBossBankrupted;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        public bool HasBossToday(int day)
        {
            return bossDatabase.Exists(b => b.Day == day);
        }

        public BossData GetBossData(int day)
        {
            return bossDatabase.Find(b => b.Day == day);
        }

        public void ClearBoss()
        {
            if (activeBossObject != null)
            {
                Destroy(activeBossObject);
                activeBossObject = null;
            }
            CurrentBossAI = null;
            CurrentBoss = null;
            BossStartingAsset = 0;
            BossCurrentAsset = 0;
        }

        public void SpawnBossForDay(int day, float playerCurrentAssets)
        {
            ClearBoss();

            CurrentBoss = GetBossData(day);
            if (CurrentBoss != null)
            {
                FXOverdose.UI.BossBattleUIBootstrap.EnsureInstalled(gameObject.scene);
                
                if (LoadedBossStartingAsset > 0f)
                {
                    BossStartingAsset = LoadedBossStartingAsset;
                    BossCurrentAsset = LoadedBossCurrentAsset >= 0f ? LoadedBossCurrentAsset : BossStartingAsset;
                    LoadedBossStartingAsset = -1f;
                    LoadedBossCurrentAsset = -1f;
                    Debug.Log($"[BossManager] 세이브 데이터에서 보스 에셋 복원: {BossStartingAsset:N0} (현재: {BossCurrentAsset:N0})");
                }
                else
                {
                    BossStartingAsset = playerCurrentAssets * CurrentBoss.AssetScalePercentage;
                    BossCurrentAsset = BossStartingAsset;
                }
                
                IsBossBankrupt = BossCurrentAsset <= 0f;
                Debug.Log(
                    $"[BossManager] {day}일차 보스 '{CurrentBoss.Name}' 등장! " +
                    $"초기 자산: {BossStartingAsset:N0} / 소속 씬: {gameObject.scene.name}");
                
                activeBossObject = new GameObject($"BossAI_{CurrentBoss.Name}");
                activeBossObject.transform.SetParent(this.transform);
                
                CurrentBossAI = activeBossObject.AddComponent<FXOverdose.AI.BossAIController>();
                CurrentBossAI.InitializeForBoss(CurrentBoss, BossStartingAsset);

                OnBossAssetChanged?.Invoke(BossCurrentAsset, BossStartingAsset);
            }
        }

        public void UpdateBossAsset(float newAsset)
        {
            if (IsBossBankrupt || CurrentBoss == null) return; // 파산 시 자산 변동 무시

            BossCurrentAsset = newAsset;
            if (BossCurrentAsset <= 0f)
            {
                BossCurrentAsset = 0f;
                IsBossBankrupt = true;
                Debug.Log($"[BossManager] 보스 '{CurrentBoss.Name}' 파산!");
                OnBossBankrupted?.Invoke();
            }
            
            OnBossAssetChanged?.Invoke(BossCurrentAsset, BossStartingAsset);
        }

        public float GetBossDailyReturnRate()
        {
            if (BossStartingAsset <= 0f) return 0f;
            return ((BossCurrentAsset - BossStartingAsset) / BossStartingAsset) * 100f;
        }
    }
}
