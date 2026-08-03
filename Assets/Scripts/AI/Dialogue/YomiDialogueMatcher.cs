using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace FXOverdose.AI.Dialogue
{
    public class YomiDialogueMatcher : MonoBehaviour
    {
        public static YomiDialogueMatcher Instance { get; private set; }

        [SerializeField] private YomiDialogueDatabase database;
        
        // 쿨다운 관리를 위한 최근 출력 멘탈/트렌드 해시
        private HashSet<string> recentDialogueTags = new HashSet<string>();
        private float lastDialogueTime = -999f;
        public float dialogueCooldown = 5f;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                if (database != null)
                {
                    database.InitializeLookupTable();
                }
                else
                {
                    Debug.LogError("[YomiDialogueMatcher] Database is missing!");
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 새로운 상태에 기반해 가장 적절한 대사를 반환합니다.
        /// </summary>
        public string GetDialogue(string position, string marketTrend, string mentalState, DirectionTag currentDirection, bool isProfit, int currentLeverage, float currentMarginRatio, int heroLevel, int skillLevel,
                                  string owner = "Any", string actualChartTrend = "Any", float absolutePnL = 0f, float duration = 0f, float currentHealth = 100f, string costumeId = "Any", string currentAction = "")
        {
            if (Time.time - lastDialogueTime < dialogueCooldown)
            {
                return null; // 쿨다운 중에는 대사 갱신 안 함
            }

            if (database == null) return "데이터베이스가 연결되지 않았어!";

            // 1. 딕셔너리 기반 1차 필터링
            var candidates = new List<YomiDialogueEntry>();
            
            var exactCandidates = database.GetCandidates(position, marketTrend);
            if (exactCandidates != null) candidates.AddRange(exactCandidates);
            
            var anyTrendCandidates = database.GetCandidates(position, "Any");
            if (anyTrendCandidates != null) candidates.AddRange(anyTrendCandidates);

            // 포지션 상태와 무관하게 범용으로 쓰이는(예: PositionClosed) 대사들도 탐색 풀에 추가
            var anyPosCandidates = database.GetCandidates("Any", marketTrend);
            if (anyPosCandidates != null) candidates.AddRange(anyPosCandidates);
            
            var anyPosAnyTrendCandidates = database.GetCandidates("Any", "Any");
            if (anyPosAnyTrendCandidates != null) candidates.AddRange(anyPosAnyTrendCandidates);

            if (candidates.Count == 0)
            {
                // 포지션/트렌드가 완벽히 일치하는 풀이 없으면 대략적인 풀(None 포지션 등) 사용
                var fallback1 = database.GetCandidates("None", marketTrend);
                if (fallback1 != null) candidates.AddRange(fallback1);
                
                var fallback2 = database.GetCandidates("None", "Any");
                if (fallback2 != null) candidates.AddRange(fallback2);

                if (candidates.Count == 0)
                {
                    return null; // 조건에 맞는 대사가 없으면 침묵
                }
            }

            // 2. 조건부 스코어링
            var scoredList = candidates.Select(c => new { Entry = c, Score = CalculateScore(c, mentalState, currentDirection, isProfit, position, currentLeverage, currentMarginRatio, heroLevel, skillLevel, owner, actualChartTrend, absolutePnL, duration, currentHealth, costumeId, currentAction) })
                                       .Where(x => x.Score > -9999) // 불일치(페널티) 제외
                                       .OrderByDescending(x => x.Score)
                                       .ToList();

            if (scoredList.Count == 0) return null;

            // 3. 최고 점수 풀(Pool) 추출 및 랜덤화
            int maxScore = scoredList[0].Score;
            // 최고 점수와 동일하거나(혹은 오차범위 내) 가장 높은 등급의 대사들을 모두 가져옵니다.
            var topCandidates = scoredList.Where(x => x.Score >= maxScore - 5).ToList();
            
            // 리스트 셔플 (Fisher-Yates)
            for (int i = 0; i < topCandidates.Count; i++)
            {
                var temp = topCandidates[i];
                int randomIndex = UnityEngine.Random.Range(i, topCandidates.Count);
                topCandidates[i] = topCandidates[randomIndex];
                topCandidates[randomIndex] = temp;
            }

            // 4. 쿨다운 및 앵무새 방지를 위해 셔플된 풀에서 안 쓴 대사 찾기
            YomiDialogueEntry bestMatch = null;
            
            foreach (var candObj in topCandidates)
            {
                var cand = candObj.Entry;
                // 💡 중복 방지 태그를 대사 원문 그 자체로 변경하여 감정 상태가 같아도 다양한 대사가 출력되도록 수정
                string tag = cand.text.GetHashCode().ToString();
                
                if (!recentDialogueTags.Contains(tag))
                {
                    bestMatch = cand;
                    recentDialogueTags.Add(tag);
                    if (recentDialogueTags.Count > 15) // 히스토리 제한 확장
                    {
                        recentDialogueTags.Remove(recentDialogueTags.First());
                    }
                    break;
                }
            }

            // 전부 최근에 썼다면 그냥 랜덤 풀의 첫 번째를 씀
            if (bestMatch == null)
            {
                bestMatch = topCandidates[0].Entry;
            }

            lastDialogueTime = Time.time;
            
            // 💡 동적 치환 태그 적용
            string finalDialogue = bestMatch.text;
            finalDialogue = finalDialogue.Replace("{leverage}", currentLeverage.ToString());
            finalDialogue = finalDialogue.Replace("{margin}", Mathf.RoundToInt(currentMarginRatio * 100).ToString());
            
            return finalDialogue;
        }

        /// <summary>
        /// 이벤트 카테고리(예: SkillUpgraded)에 해당하는 대사를 반환합니다.
        /// </summary>
        public string GetEventDialogue(string eventCategory, int currentLeverage = 0, float currentMarginRatio = 0f, float roe = 0f, float maxObserved = 0f)
        {
            if (database == null) return null;
            var candidates = database.GetEventCandidates(eventCategory);
            if (candidates == null || candidates.Count == 0) return null;
            
            // 이벤트 대사도 랜덤하게 하나 선택 (간단히)
            int r = Random.Range(0, candidates.Count);
            string finalDialogue = candidates[r].text;
            
            finalDialogue = finalDialogue.Replace("{leverage}", currentLeverage.ToString());
            finalDialogue = finalDialogue.Replace("{margin}", Mathf.RoundToInt(currentMarginRatio * 100).ToString());
            finalDialogue = finalDialogue.Replace("{roe:F1}", roe.ToString("F1"));
            finalDialogue = finalDialogue.Replace("{maxObserved:F1}", maxObserved.ToString("F1"));

            // 이벤트 출력 직후에 일반 대사가 덮어씌우지 않도록 쿨다운 갱신
            lastDialogueTime = Time.time; 
            return finalDialogue;
        }

        private int CalculateScore(YomiDialogueEntry entry, string currentMentalState, DirectionTag currentDirection, bool isProfit, string position, int currentLeverage, float currentMarginRatio, int heroLevel, int skillLevel,
                                   string owner, string actualChartTrend, float absolutePnL, float duration, float currentHealth, string costumeId, string currentAction)
        {
            int score = 0;

            // --- 아키텍처 보강: 시스템 이벤트(Action) 강제 매칭 로직 ---
            if (!string.IsNullOrEmpty(entry.eventCategory))
            {
                if (entry.eventCategory == currentAction)
                {
                    score += 500; // 상황/행동(종료, 관망 등)이 완벽하게 일치하면 초고가점
                }
                else
                {
                    return -9999; // 대사가 요구하는 행동과 현재 행동이 불일치하면 채택 불가 (원천 차단)
                }
            }

            // 필수 조건 체크: 포지션이 있을 경우, 수익 상태 및 방향성이 아주 중요
            if (position != "None")
            {
                // 방향성 일치
                if (entry.requiredDirection != DirectionTag.None && entry.requiredDirection == currentDirection)
                {
                    score += 50;
                }
                // 수익(ROE 부호) 일치
                if (entry.isProfit == isProfit)
                {
                    score += 50;
                }
            }
            else
            {
                // 무포지션 예외 처리: 변동성(방향성 유무) 및 트렌드에 따라 가중치
                // None인데 특정 방향성을 요구하는 대사면 가산점 (관망 중 큰 변동성 포착)
                if (entry.requiredDirection == currentDirection && currentDirection != DirectionTag.None)
                {
                    score += 30;
                }
            }

            // 멘탈 상태 일치
            if (entry.mentalState == currentMentalState)
            {
                score += 40;
            }

            // --- 추가 가중치 1: 레버리지 매칭 ---
            if (entry.requiredLeverage > 0 && currentLeverage > 0)
            {
                // 레버리지 차이가 적을수록 높은 점수 부여 (최대 30점)
                int diff = Mathf.Abs(entry.requiredLeverage - currentLeverage);
                score += Mathf.Max(0, 30 - diff);
            }

            // --- 추가 가중치 2: 마진 리스크(풀매수) 매칭 ---
            // 대사가 하이리스크를 가정하고, 현재 플레이어의 마진 비율이 0.7(70%) 이상이면 가점
            if (entry.isHighMarginRisk && currentMarginRatio >= 0.7f)
            {
                score += 35;
            }
            else if (!entry.isHighMarginRisk && currentMarginRatio < 0.5f)
            {
                score += 15; // 로우리스크 대사인데 마진이 적으면 약간의 가점
            }

            // --- 추가 가중치 3: 짬바(레벨) 매칭 ---
            if (entry.requiredHeroLevel > 0)
            {
                int diff = Mathf.Abs(entry.requiredHeroLevel - heroLevel);
                score += Mathf.Max(0, 20 - (diff * 2)); // 차이가 적을수록 높은 점수
            }
            if (entry.requiredSkillLevel > 0)
            {
                int diff = Mathf.Abs(entry.requiredSkillLevel - skillLevel);
                score += Mathf.Max(0, 20 - (diff * 2));
            }

            // --- 아키텍처 보강 가중치 (Context Deficit 완화) ---
            if (entry.requiredOwner != "Any" && entry.requiredOwner == owner)
            {
                score += 30; // 주체(AI/Player) 정확히 일치 시 높은 가산점
            }
            if (entry.requiredChartTrend != "Any")
            {
                if (entry.requiredChartTrend == actualChartTrend)
                {
                    score += 50; // 실제 빔 방향 일치 시 가산점
                }
                else
                {
                    return -9999; // 실제 차트 방향(Sideways 등)과 대사(Pump 등)가 다르면 출력 차단
                }
            }
            if (absolutePnL >= entry.minAbsolutePnL && absolutePnL <= entry.maxAbsolutePnL && (entry.minAbsolutePnL != -9999999f || entry.maxAbsolutePnL != 9999999f))
            {
                score += 20; // 절대 수익금 구간 일치 (푼돈 vs 거액 구분)
            }
            if (duration >= entry.minTradeDuration && duration <= entry.maxTradeDuration && (entry.minTradeDuration != -1f || entry.maxTradeDuration != 999999f))
            {
                score += 20; // 유지시간(스캘핑 vs 스윙) 일치
            }
            if (currentHealth <= entry.maxHealthLimit && entry.maxHealthLimit != 999f)
            {
                score += 35; // HP 한계 상황 묘사 대사 매칭 우대
            }
            if (entry.requiredCostumeId != "Any" && entry.requiredCostumeId == costumeId)
            {
                score += 40; // 전용 복장 대사 우대
            }

            return score;
        }
    }
}
