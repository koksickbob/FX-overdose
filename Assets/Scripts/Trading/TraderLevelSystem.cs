using System;
using UnityEngine;

namespace FXOverdose.Trading
{
    public enum SkillType
    {
        ChartStudy,    // 차트 공부: 거래 성공률 증가 및 진입 타점 최적화
        CubePatience,  // 큐브 풀기: 인내심 증가 (수익 구간에서 목표 수익 다 가져감)
        BookJudgment   // 책읽기: 판단력 증가 (빠른 손절 타이밍 잡음)
    }

    public interface ITraderLevelProvider
    {
        int ChartStudyLevel { get; }
        int GetMaxAllowedLeverage();
        float GetMaxAllowedMarginRatio();
        float GetSignalAccuracy();
        float GetTakeProfitMultiplier();
        float GetEntryDelayPenaltyRatio();
        float GetStopLossTightness();
    }

    /// <summary>
    /// 주인공 레벨(ProtagonistLevel) 및 스킬 레벨(SkillLevel)을 관리하는 통합 성장 시스템입니다.
    /// 주인공 레벨은 거래 성공(익절) 경험치를 통해 성장하며 레버리지/증거금 한도 및 멘탈 회복을 제어합니다.
    /// 스킬 레벨은 시간, 비용, 체력을 소모하는 학습 기믹을 통해 성장하며 AI 매매 타점 및 성공률을 제어합니다.
    /// </summary>
    public class TraderLevelSystem : MonoBehaviour, ITraderLevelProvider
    {
        private static TraderLevelSystem _instance;
        public static TraderLevelSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<TraderLevelSystem>();
                    if (_instance == null)
                    {
                        var gm = FindAnyObjectByType<GameManager>();
                        if (gm != null)
                        {
                            _instance = gm.gameObject.AddComponent<TraderLevelSystem>();
                        }
                        else
                        {
                            var go = new GameObject("TraderLevelSystem");
                            _instance = go.AddComponent<TraderLevelSystem>();
                        }
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("시스템 참조")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private TraderStatus traderStatus;

        [Header("주인공 레벨 데이터 (Protagonist Level)")]
        [SerializeField] private int protagonistLevel = 1;
        [SerializeField] private float protagonistEXP = 0f;

        [Header("스킬 레벨 데이터 (Skill Level - 최대 10)")]
        [SerializeField, Range(1, 10)] private int chartStudyLevel = 1;
        [SerializeField, Range(1, 10)] private int cubePatienceLevel = 1;
        [SerializeField, Range(1, 10)] private int bookJudgmentLevel = 1;

        // 이벤트 통지
        public event Action<int, float, float> OnProtagonistLevelChanged; // (현재레벨, 현재EXP, 최대EXP)
        public event Action<int> OnProtagonistLeveledUp; // (달성레벨)
        public event Action<SkillType, int> OnSkillLevelChanged; // (스킬종류, 달성레벨)

        // 읽기 전용 속성
        public int ProtagonistLevel => protagonistLevel;
        public float ProtagonistEXP => protagonistEXP;
        public int ChartStudyLevel => chartStudyLevel;
        public int CubePatienceLevel => cubePatienceLevel;
        public int BookJudgmentLevel => bookJudgmentLevel;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
            if (traderStatus == null) traderStatus = TraderStatus.CanonicalInstance;
        }

        // --- 1. 주인공 레벨 및 경험치 연산 (Protagonist Level) ---

        public float GetMaxProtagonistEXP(int level)
        {
            // LV.1 -> 400 EXP, 매 레벨 40%씩 증가 (고레벨 달성 난이도 상승)
            return 400f * Mathf.Pow(1.4f, Mathf.Max(0, level - 1));
        }

        /// <summary>
        /// 익절(수익) 성공 시 거래 금액 및 레버리지에 비례한 경험치를 지급합니다.
        /// </summary>
        public void AddProtagonistEXP(float pnl, int leverage)
        {
            // 거래 성공(수익 발생) 시에만 경험치 지급: 수익이 0 이하인 경우 절대 지급하지 않음
            if (pnl <= 0f)
            {
                Debug.Log($"[TraderLevelSystem] 수익이 발생하지 않은 거래(PnL: ${pnl:N1})이므로 경험치를 지급하지 않습니다.");
                return;
            }

            // 기본 EXP 10 + PNL의 제곱근에 0.6배 + 레버리지 배율 (초중반 억제 밸런스)
            float gainedExp = 10f + (Mathf.Sqrt(pnl) * 0.6f) + leverage;
            protagonistEXP += gainedExp;

            Debug.Log($"[TraderLevelSystem 🌟] 거래 성공! 경험치 획득: +{gainedExp:N1} (현재 EXP: {protagonistEXP:N1} / {GetMaxProtagonistEXP(protagonistLevel):N1})");

            while (protagonistEXP >= GetMaxProtagonistEXP(protagonistLevel))
            {
                protagonistEXP -= GetMaxProtagonistEXP(protagonistLevel);
                protagonistLevel++;
                Debug.Log($"[TraderLevelSystem 🚀] [레벨업] 주인공 레벨 LV.{protagonistLevel} 달성! (레버리지/증거금 한도 및 멘탈 회복 증가)");
                OnProtagonistLeveledUp?.Invoke(protagonistLevel);
                FXOverdose.Core.AchievementManager.Instance?.RecordLevelUp(protagonistLevel);
            }

            OnProtagonistLevelChanged?.Invoke(protagonistLevel, protagonistEXP, GetMaxProtagonistEXP(protagonistLevel));
        }

        public int GetMaxAllowedLeverage()
        {
            return protagonistLevel switch
            {
                1 => 5,
                2 => 7,
                3 => 10,
                4 => 15,
                5 => 20,
                6 => 25,
                7 => 30,
                8 => 35,
                9 => 40,
                10 => 50,
                11 => 60,
                12 => 70,
                13 => 80,
                14 => 90,
                15 => 100,
                16 => 105,
                17 => 110,
                18 => 115,
                19 => 120,
                _ => 125 // LV 20 이상
            };
        }

        public float GetMaxAllowedMarginRatio()
        {
            return protagonistLevel switch
            {
                1 => 0.15f,
                2 => 0.20f,
                3 => 0.25f,
                4 => 0.30f,
                5 => 0.35f,
                6 => 0.40f,
                7 => 0.45f,
                8 => 0.50f,
                9 => 0.55f,
                10 => 0.60f,
                11 => 0.65f,
                12 => 0.70f,
                13 => 0.75f,
                14 => 0.80f,
                15 => 0.85f,
                16 => 0.90f,
                17 => 0.93f,
                18 => 0.96f,
                19 => 0.98f,
                _ => 1.0f // LV 20 이상
            };
        }

        /// <summary>
        /// 주인공 레벨에 따른 거래 성공(익절) 시 멘탈 회복 배율을 반환합니다.
        /// </summary>
        public float GetMentalRecoveryMultiplierOnWin()
        {
            return protagonistLevel switch
            {
                1 => 0.5f,
                2 => 0.55f,
                3 => 0.6f,
                4 => 0.7f,
                5 => 0.8f,
                6 => 0.9f,
                7 => 1.0f,
                8 => 1.1f,
                9 => 1.2f,
                10 => 1.3f,
                11 => 1.4f,
                12 => 1.5f,
                13 => 1.6f,
                14 => 1.7f,
                15 => 1.8f,
                16 => 1.9f,
                _ => 2.0f   // LV 17+ 
            };
        }

        // --- 2. 스킬 레벨 효과 API (Skill Level Modifiers) ---

        /// <summary>
        /// [차트 공부 귀속] 신호 정확도 (0.0 ~ 1.0).
        /// 미달 시 AI가 정상 신호에서도 반대 방향(역진입)으로 주문을 넣을 확률이 생깁니다.
        /// </summary>
        public float GetSignalAccuracy()
        {
            float accuracy = chartStudyLevel switch
            {
                1 => 0.60f, // 40% 오진입
                2 => 0.65f,
                3 => 0.70f,
                4 => 0.75f,
                5 => 0.80f, // 20% 오진입
                6 => 0.84f,
                7 => 0.87f,
                _ => 0.90f  // LV 8 이상 오진입 최소 10% 남음
            };

            if (CostumeManager.Instance != null && CostumeManager.Instance.EquippedCostumeId == CostumeManager.OfficeLookId)
            {
                accuracy += 0.05f;
            }

            return accuracy;
        }

        /// <summary>
        /// [차트 공부 귀속] 진입 지연(이미 주가가 움직인 뒤 늦게 따라 들어가는 슬리피지 패널티 비율).
        /// </summary>
        public float GetEntryDelayPenaltyRatio()
        {
            return chartStudyLevel switch
            {
                1 => 0.07f, // 7% 불리한 타점에 진입
                2 => 0.06f,
                3 => 0.05f,
                4 => 0.04f,
                5 => 0.03f,
                6 => 0.02f,
                7 => 0.01f,
                _ => 0.0f   // LV 8 이상 최적 타점 진입
            };
        }

        /// <summary>
        /// [큐브 풀기 귀속] 인내심 증폭 계수 (익절 목표가 배율).
        /// </summary>
        public float GetTakeProfitMultiplier()
        {
            return cubePatienceLevel switch
            {
                1 => 0.25f, 
                2 => 0.35f,
                3 => 0.45f,
                4 => 0.55f,
                5 => 0.65f,
                6 => 0.72f,
                7 => 0.78f,
                8 => 0.84f, 
                9 => 0.88f, 
                _ => 0.90f  // LV 10: 인내심 만렙 시 0.90
            };
        }

        /// <summary>
        /// [책읽기 귀속] 판단력 증가에 따른 칼손절 타임(진입가 대비 손절 비율).
        /// 값이 작을수록(-0.015 = -1.5%) 빠른 칼손절로 손해를 줄이고, 클수록(-0.09 = -9%) 미련하게 큰 손해를 봅니다.
        /// </summary>
        public float GetStopLossTightness()
        {
            return bookJudgmentLevel switch
            {
                1 => 0.090f, 
                2 => 0.085f,
                3 => 0.080f,
                4 => 0.075f,
                5 => 0.070f, 
                6 => 0.065f,
                7 => 0.060f,
                8 => 0.055f,
                9 => 0.052f,
                _ => 0.050f  // -5.0%
            };
        }

        // --- 3. 스킬 성장 기믹 비용 소모 시스템 (`TryUpgradeSkillWithCost`) ---

        public int GetSkillLevel(SkillType type)
        {
            return type switch
            {
                SkillType.ChartStudy => chartStudyLevel,
                SkillType.CubePatience => cubePatienceLevel,
                SkillType.BookJudgment => bookJudgmentLevel,
                _ => 1
            };
        }

        public float GetSkillCost(SkillType type)
        {
            int lv = GetSkillLevel(type);
            float baseCost = type switch
            {
                SkillType.ChartStudy => 3500f * Mathf.Pow(1.65f, lv - 1),
                SkillType.CubePatience => 2000f * Mathf.Pow(1.6f, lv - 1),
                SkillType.BookJudgment => 2500f * Mathf.Pow(1.62f, lv - 1),
                _ => 100f
            };
            
            if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
            if (gameManager != null)
            {
                baseCost *= Mathf.Pow(1.15f, gameManager.CurrentDay - 1);
            }
            
            return baseCost;
        }

        public float GetSkillHealthCost(SkillType type)
        {
            return type switch
            {
                SkillType.ChartStudy => 20f,   // 눈 피로 및 두뇌 과로
                SkillType.CubePatience => 12f, // 신경 집중 소모
                SkillType.BookJudgment => 25f, // 수면 부족 및 뇌 당분 소모
                _ => 15f
            };
        }

        public int GetSkillTimeCostHours(SkillType type)
        {
            return type switch
            {
                SkillType.ChartStudy => 3,   // 3시간 소모
                SkillType.CubePatience => 1, // 1시간 (또는 1.5시간 -> 90분)
                SkillType.BookJudgment => 4, // 4시간 소모
                _ => 2
            };
        }

        public bool CanUpgradeSkill(SkillType type, out string reason)
        {
            int currentLv = GetSkillLevel(type);
            if (currentLv >= 10)
            {
                reason = "이미 최대 레벨(LV.10)입니다.";
                return false;
            }

            if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
            if (gameManager == null)
            {
                reason = "자산 정보를 찾을 수 없습니다.";
                return false;
            }

            // 🚀 [이벤트 중 스킬 레벨업 차단] 돌발 이벤트 팝업 등이 떠서 게임이 Paused 상태일 때는 진행 금지
            if (gameManager.CurrentState != GameManager.GameState.Playing)
            {
                reason = "현재 진행 중인 중요한 상황(이벤트 등)을 먼저 해결해야 합니다.";
                return false;
            }

            float cost = GetSkillCost(type);
            if (gameManager.CurrentBalance < cost)
            {
                reason = $"자산이 부족합니다. (필요: ${cost:N0})";
                return false;
            }

            if (traderStatus == null) traderStatus = TraderStatus.CanonicalInstance;
            if (traderStatus == null)
            {
                reason = "플레이어 상태 정보를 찾을 수 없습니다.";
                return false;
            }

            float healthCost = GetSkillHealthCost(type);
            if (traderStatus.CurrentHealth <= healthCost + 5f)
            {
                reason = $"과로로 인한 탈진/사경(Overdose) 위험! 체력이 부족합니다. (필요 HP: {healthCost:N0})";
                return false;
            }

            reason = "";
            return true;
        }

        /// <summary>
        /// 시간, 자금, 체력을 소모하여 스킬 레벨을 올리고 기믹 대사를 트리거합니다.
        /// </summary>
        public bool TryUpgradeSkillWithCost(SkillType type)
        {
            if (!CanUpgradeSkill(type, out string reason))
            {
                Debug.LogWarning($"[TraderLevelSystem] 스킬 업그레이드 실패: {reason}");
                return false;
            }

            if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
            if (traderStatus == null) traderStatus = TraderStatus.CanonicalInstance;

            float cost = GetSkillCost(type);
            float healthCost = GetSkillHealthCost(type);
            int timeHours = GetSkillTimeCostHours(type);

            // 비용 지출
            if (!gameManager.TrySpendBalance(cost))
            {
                Debug.LogWarning("[TraderLevelSystem] 스킬 업그레이드 도중 자산 차감에 실패했습니다.");
                return false;
            }

            // 체력 소모
            traderStatus.ChangeHealth(-healthCost);

            // 시간 경과 처리 (예: 3시간이면 180분 경과)
            // GameManager에 AdvanceMinutes나 그에 준하는 시간이동이 있다면 수행
            gameManager.AdvanceGameMinutes(timeHours * 60);

            // 레벨 증가
            switch (type)
            {
                case SkillType.ChartStudy:
                    chartStudyLevel++;
                    Debug.Log($"[TraderLevelSystem 📊] 차트 공부 완료! ChartStudyLevel LV.{chartStudyLevel} 달성! (성공률 증가 & 오진입 감소)");
                    break;
                case SkillType.CubePatience:
                    cubePatienceLevel++;
                    Debug.Log($"[TraderLevelSystem 🧩] 큐브 풀기 완료! CubePatienceLevel LV.{cubePatienceLevel} 달성! (인내심 증폭 & 목표가 확장)");
                    break;
                case SkillType.BookJudgment:
                    bookJudgmentLevel++;
                    Debug.Log($"[TraderLevelSystem 📖] 책읽기 완료! BookJudgmentLevel LV.{bookJudgmentLevel} 달성! (판단력 증가 & 손절 타점 단축)");
                    break;
            }

            OnSkillLevelChanged?.Invoke(type, GetSkillLevel(type));

            // 스킬 업그레이드 동적 반응 대사 트리거
            var matcher = FXOverdose.AI.Dialogue.YomiDialogueMatcher.Instance;
            if (matcher == null) matcher = UnityEngine.Object.FindAnyObjectByType<FXOverdose.AI.Dialogue.YomiDialogueMatcher>(UnityEngine.FindObjectsInactive.Include);
            
            string dialogue = null;
            if (matcher != null)
            {
                dialogue = matcher.GetEventDialogue("SkillUpgraded");
            }
            
            // 데이터베이스에 이벤트 대사가 없거나 매처가 없을 때를 대비한 하드코딩 Fallback
            if (string.IsNullOrEmpty(dialogue))
            {
                int currentLevel = GetSkillLevel(type);
                dialogue = type switch
                {
                    SkillType.ChartStudy => $"차트 공부 완료! (LV.{currentLevel}) 눈알이 빠질 것 같지만 타점 분석 능력이 올랐어!",
                    SkillType.CubePatience => $"큐브 풀기 완료! (LV.{currentLevel}) 인내심이 크게 늘었어. 이제 목표가까지 묵묵히 버텨주지.",
                    SkillType.BookJudgment => $"독서 완료! (LV.{currentLevel}) 파산 회고록을 읽으니 손절의 중요성을 뼈저리게 느꼈어.",
                    _ => $"트레이딩 실력이 성장하여 더 똑똑해졌다! (LV.{currentLevel})"
                };
            }

            var visual = UnityEngine.Object.FindAnyObjectByType<FXOverdose.AI.AIVisualController>(UnityEngine.FindObjectsInactive.Include);
            if (visual != null)
            {
                visual.DisplayDialogueBalloon(dialogue, FXOverdose.AI.DialoguePriority.High, FXOverdose.AI.EventCategory.SkillUpgraded);
            }

            FXOverdose.Core.AchievementManager.Instance?.RecordSkillLevelUp();
            return true;
        }

        public void ResetLevels()
        {
            protagonistLevel = 1;
            protagonistEXP = 0f;
            chartStudyLevel = 1;
            cubePatienceLevel = 1;
            bookJudgmentLevel = 1;

            Debug.Log("[TraderLevelSystem] 🔄 주인공 레벨(LV.1) 및 스킬 레벨이 모두 초기화되었습니다.");
            OnProtagonistLevelChanged?.Invoke(protagonistLevel, protagonistEXP, GetMaxProtagonistEXP(protagonistLevel));
        }
    }
}


