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
        public string GetDialogue(string position, string marketTrend, string mentalState, DirectionTag currentDirection, bool isProfit)
        {
            if (Time.time - lastDialogueTime < dialogueCooldown)
            {
                return null; // 쿨다운 중에는 대사 갱신 안 함
            }

            if (database == null) return "데이터베이스가 연결되지 않았어!";

            // 1. 딕셔너리 기반 1차 필터링
            var candidates = database.GetCandidates(position, marketTrend);

            if (candidates == null || candidates.Count == 0)
            {
                // 포지션/트렌드가 완벽히 일치하는 풀이 없으면 대략적인 풀(None 포지션 등) 사용
                candidates = database.GetCandidates("None", marketTrend);
                if (candidates == null || candidates.Count == 0)
                {
                    return "무슨 상황인지 모르겠어..."; // 최후의 보루
                }
            }

            // 2. 조건부 스코어링
            var scoredList = candidates.Select(c => new { Entry = c, Score = CalculateScore(c, mentalState, currentDirection, isProfit, position) })
                                       .OrderByDescending(x => x.Score)
                                       .ToList();

            // 3. 쿨다운 및 앵무새 방지를 위해 상위 5개 중 안 쓴 대사 찾기
            YomiDialogueEntry bestMatch = null;
            int topTake = Mathf.Min(5, scoredList.Count);
            
            for (int i = 0; i < topTake; i++)
            {
                var cand = scoredList[i].Entry;
                string tag = $"{cand.mentalState}_{cand.requiredDirection}";
                
                if (!recentDialogueTags.Contains(tag))
                {
                    bestMatch = cand;
                    recentDialogueTags.Add(tag);
                    if (recentDialogueTags.Count > 5) // 히스토리 제한
                    {
                        recentDialogueTags.Remove(recentDialogueTags.First());
                    }
                    break;
                }
            }

            // 전부 최근에 썼다면 그냥 1등을 씀
            if (bestMatch == null)
            {
                bestMatch = scoredList[0].Entry;
            }

            lastDialogueTime = Time.time;
            return bestMatch.text;
        }

        private int CalculateScore(YomiDialogueEntry entry, string currentMentalState, DirectionTag currentDirection, bool isProfit, string position)
        {
            int score = 0;

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

            return score;
        }
    }
}
