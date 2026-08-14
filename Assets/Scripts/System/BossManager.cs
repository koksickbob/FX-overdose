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
                        GameManager gameManager = GameManager.Instance;
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

        /// <summary>
        /// 보스 시스템 전체 스위치. <b>스토리 개편 기간 동안 꺼 둡니다 (2026-08-15).</b>
        ///
        /// 이 한 값이 보스의 유일한 관문입니다 — 조우 판정·아침 등장 연출·AI 스폰·HUD 설치·
        /// 정산 승패 판정·세이브 기록이 전부 <see cref="HasBossToday"/>/<see cref="GetBossData"/>를
        /// 거치므로, 여기서 끊으면 하위 경로가 전부 함께 죽습니다. 되살릴 때는 true 한 글자입니다.
        ///
        /// ⚠️ <b>const가 아니라 static readonly입니다.</b> const로 두면 하위 코드가 도달 불가로
        ///    판정되어 경고가 쏟아지고, 되살릴 때까지 그 경고가 진짜 문제를 가립니다.
        ///
        /// ⚠️ <b>AITradingBrain은 보스 시스템이 아닙니다.</b> IsBossAI 분기를 갖고 있을 뿐,
        ///    평상시 자동 매매의 실행 주체입니다. 함께 끄지 마십시오.
        /// </summary>
        public static readonly bool BossesEnabled = false;

        /// <summary>
        /// 지금 보스가 동작해야 하는지. <b>비활성화는 스토리 모드에만 적용됩니다.</b>
        ///
        /// 엔드리스·챌린지는 보스전이 곧 모드의 존재 이유이므로 스위치와 무관하게 항상 켜 둡니다.
        /// (P2P는 애초에 이 매니저를 쓰지 않습니다.)
        /// </summary>
        private static bool BossesActive
        {
            get
            {
                if (BossesEnabled) return true;
                var save = SaveLoadManager.Instance;
                // 세이브 매니저가 없는 테스트 씬 등은 스토리로 간주해 종전대로 꺼 둡니다.
                return save != null && save.CurrentGameMode != GameMode.Story;
            }
        }

        public bool HasBossToday(int day)
        {
            if (!BossesActive) return false;
            return bossDatabase.Exists(b => b.Day == day);
        }

        public BossData GetBossData(int day)
        {
            if (!BossesActive) return null;
            return bossDatabase.Find(b => b.Day == day);
        }

        /// <summary>
        /// 그 일차에 보스가 <b>편성되어 있는지</b>를 <see cref="BossesEnabled"/>와 무관하게 답합니다.
        ///
        /// 보스를 껐다고 해서 "그 날은 아무것도 없는 날"이 되는 것은 아닙니다. 날짜 점프 클램프처럼
        /// <b>일정을 보호하는 쪽</b>은 기능이 꺼져 있어도 계속 그 날을 피해야 합니다 —
        /// 그러지 않으면 보스를 되살렸을 때 이미 건너뛰어진 세이브가 남습니다.
        ///
        /// 조우·스폰·판정처럼 <b>기능을 실행하는 쪽</b>은 <see cref="HasBossToday"/>를 쓰십시오.
        /// </summary>
        public bool IsBossScheduledDay(int day)
        {
            return bossDatabase.Exists(b => b.Day == day);
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

            // GetBossData가 null을 돌려주므로 아래 블록은 어차피 통과하지 않지만,
            // 스폰은 UI 설치·AI 오브젝트 생성 같은 부작용이 있어 관문을 하나 더 둡니다.
            if (!BossesActive) return;

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
