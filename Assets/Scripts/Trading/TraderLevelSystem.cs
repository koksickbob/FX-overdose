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

    /// <summary>
    /// 주인공 레벨(ProtagonistLevel) 및 스킬 레벨(SkillLevel)을 관리하는 통합 성장 시스템입니다.
    /// 주인공 레벨은 거래 성공(익절) 경험치를 통해 성장하며 레버리지/증거금 한도 및 멘탈 회복을 제어합니다.
    /// 스킬 레벨은 시간, 비용, 체력을 소모하는 학습 기믹을 통해 성장하며 AI 매매 타점 및 성공률을 제어합니다.
    /// </summary>
    public class TraderLevelSystem : MonoBehaviour
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
            // LV.1 -> 100 EXP, LV.2 -> 135 EXP, 매 레벨 35%씩 증가
            return 100f * Mathf.Pow(1.35f, Mathf.Max(0, level - 1));
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

            // 기본 EXP 20 + 손익금의 5% + 레버리지 배율 * 1.5 (전체 획득량을 1/3로 축소)
            float gainedExp = (20f + (pnl * 0.05f) + (leverage * 1.5f)) / 3.0f;
            protagonistEXP += gainedExp;

            Debug.Log($"[TraderLevelSystem 🌟] 거래 성공! 경험치 획득: +{gainedExp:N1} (현재 EXP: {protagonistEXP:N1} / {GetMaxProtagonistEXP(protagonistLevel):N1})");

            while (protagonistEXP >= GetMaxProtagonistEXP(protagonistLevel))
            {
                protagonistEXP -= GetMaxProtagonistEXP(protagonistLevel);
                protagonistLevel++;
                Debug.Log($"[TraderLevelSystem 🚀] [레벨업] 주인공 레벨 LV.{protagonistLevel} 달성! (레버리지/증거금 한도 및 멘탈 회복 증가)");
                OnProtagonistLeveledUp?.Invoke(protagonistLevel);
            }

            OnProtagonistLevelChanged?.Invoke(protagonistLevel, protagonistEXP, GetMaxProtagonistEXP(protagonistLevel));
        }

        public int GetMaxAllowedLeverage()
        {
            return protagonistLevel switch
            {
                1 => 10,
                2 => 15,
                3 => 25,
                4 => 35,
                5 => 50,
                6 => 65,
                7 => 80,
                8 => 100,
                _ => 125 // LV 9 이상
            };
        }

        public float GetMaxAllowedMarginRatio()
        {
            return protagonistLevel switch
            {
                1 => 0.25f,
                2 => 0.30f,
                3 => 0.40f,
                4 => 0.50f,
                5 => 0.60f,
                6 => 0.70f,
                7 => 0.80f,
                8 => 0.90f,
                _ => 1.0f // LV 9 이상
            };
        }

        /// <summary>
        /// 주인공 레벨에 따른 거래 성공(익절) 시 멘탈 회복 배율을 반환합니다.
        /// </summary>
        public float GetMentalRecoveryMultiplierOnWin()
        {
            return protagonistLevel switch
            {
                1 => 0.5f,  // 초보 시절 불안감으로 50% 축소
                2 => 0.6f,
                3 => 0.8f,
                4 => 0.9f,
                5 => 1.0f,  // 100% 정상 회복
                6 => 1.15f,
                7 => 1.3f,
                8 => 1.5f,
                9 => 1.75f,
                _ => 2.0f   // LV 10+ 승리의 희열 극대화 (200%)
            };
        }

        // --- 2. 스킬 레벨 효과 API (Skill Level Modifiers) ---

        /// <summary>
        /// [차트 공부 귀속] 신호 정확도 (0.0 ~ 1.0).
        /// 미달 시 AI가 정상 신호에서도 반대 방향(역진입)으로 주문을 넣을 확률이 생깁니다.
        /// </summary>
        public float GetSignalAccuracy()
        {
            return chartStudyLevel switch
            {
                1 => 0.70f, // 30% 오진입/역진입 에러 확률
                2 => 0.75f,
                3 => 0.80f,
                4 => 0.85f,
                5 => 0.90f, // 10% 에러 확률
                6 => 0.94f,
                7 => 0.97f,
                _ => 1.0f   // LV 8 이상 오진입 0% 완벽 정확도
            };
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
                1 => 0.35f, // 확정 수익 구간에서도 35% 지점에서 참지 못하고 조기 익절
                2 => 0.45f,
                3 => 0.55f,
                4 => 0.65f,
                5 => 0.75f,
                6 => 0.85f,
                7 => 0.95f,
                8 => 1.0f,  // 100% 목표가 도달까지 홀딩
                9 => 1.1f,
                _ => 1.2f   // LV 10 풀스크린 초과 수익 극대화
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
                1 => 0.090f, // -9.0%의 막대한 손해를 볼 때까지 못 끊음
                2 => 0.080f,
                3 => 0.070f,
                4 => 0.060f,
                5 => 0.045f, // -4.5% 손절
                6 => 0.035f,
                7 => 0.028f,
                8 => 0.022f,
                9 => 0.018f,
                _ => 0.015f  // -1.5% 기계적 칼손절로 손해 극소화
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
            return type switch
            {
                SkillType.ChartStudy => 200f * Mathf.Pow(1.6f, lv - 1),
                SkillType.CubePatience => 100f * Mathf.Pow(1.5f, lv - 1),
                SkillType.BookJudgment => 150f * Mathf.Pow(1.55f, lv - 1),
                _ => 100f
            };
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

            // LLM 기반 스킬 업그레이드 동적 반응 대사 트리거 (다중 업그레이드 시 마지막 업그레이드 기준 반영)
            int currentLevel = GetSkillLevel(type);
            string contextString = type switch
            {
                SkillType.ChartStudy => $"[스킬 업그레이드 완료: 차트 공부 LV.{currentLevel}] 눈알이 빠질 것 같이 피곤하지만 기술적 분석과 패턴 파악 능력이 크게 늘었다! 호가창 속 세력들의 가짜 반등 빔이나 휩소에 더 이상 안 속고 정확한 매매 타점을 잡을 수 있다는 자신감과 피로/희열을 나타내는 주인공 반응 대사.",
                SkillType.CubePatience => $"[스킬 업그레이드 완료: 큐브 풀기 LV.{currentLevel}] 극심한 인내심 훈련(큐브 풀기)을 통해 뇌의 참을성이 증폭되었다! 수익이 조금 났다고 촐랑거리며 일찍 털어버리지 않고, 목표가까지 묵묵히 버텨서 큰 파동을 다 먹겠다는 인내와 각오를 보여주는 주인공 반응 대사.",
                SkillType.BookJudgment => $"[스킬 업그레이드 완료: 책읽기(파산 회고록) LV.{currentLevel}] 머리가 터질 것 같이 두꺼운 전설적인 파산 회고록을 완독했다! 손절을 머뭇거리는 게 얼마나 멍청한지 뼛속까지 깨달았어. 미련하게 물타기 하다가 패가망신한 트레이더들의 사례를 뼈저리게 깨닫고, 칼손절과 냉철한 리스크 판단력으로 살아남겠다는 독기를 품은 주인공 반응 대사.",
                _ => $"[스킬 업그레이드 완료 LV.{currentLevel}] 트레이딩 실력이 성장하여 더 똑똑해졌다!"
            };

            var llm = FXOverdose.AI.LLM.LocalLLMService.Instance;
            if (llm != null)
            {
                llm.RequestDialogue(FXOverdose.AI.LLM.EventCategory.SkillUpgraded, contextString);
            }

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
