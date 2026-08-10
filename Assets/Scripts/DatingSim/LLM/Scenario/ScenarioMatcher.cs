using System;
using System.Collections.Generic;
using UnityEngine;
using FXOverdose.Core; // Assuming SaveData/TimeManager is here or DatingSim.Core

namespace FXOverdose.DatingSim.LLM.Scenario
{
    public class ScenarioMatcher : MonoBehaviour
    {
        public static ScenarioMatcher Instance { get; private set; }

        [SerializeField] private ScenarioDatabase database;
        
        // 앵무새 방지용 쿨다운 해시셋
        private HashSet<string> recentScenarios = new HashSet<string>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (database != null) database.InitializeLookupTable();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public ScenarioEntry GetBestScenario(string category, string timeOfDay, int currentAffection, int currentObsession, string currentMood, float idleHours, List<string> activeFlags)
        {
            if (database == null) return null;

            // 1. O(1) 딕셔너리 기반 1차 컷오프
            List<ScenarioEntry> candidates = database.GetCandidates(category, timeOfDay);
            if (candidates.Count == 0)
            {
                // Fallback (Any Time)
                candidates = database.GetCandidates(category, "Any");
            }

            if (candidates.Count == 0) return null;

            int maxScore = -9999;
            bool allOnCooldown = true;
            
            // 2-1. 최고 점수 찾기 (Zero-Allocation 유지)
            for (int i = 0; i < candidates.Count; i++)
            {
                ScenarioEntry entry = candidates[i];
                if (!CheckFlags(entry.requiredFlags, entry.blockingFlags, activeFlags)) continue;
                
                if (recentScenarios.Contains(entry.scenarioID)) continue;
                
                allOnCooldown = false;
                int score = CalculateScore(entry, currentMood, currentAffection, currentObsession, idleHours);
                if (score > maxScore) maxScore = score;
            }

            // [안전장치] 데이터가 너무 적어서 모든 씬이 쿨다운에 걸린 경우 (게임 멈춤 방지)
            if (allOnCooldown || maxScore == -9999) 
            {
                // 쿨다운을 무시하고 다시 최고점을 찾습니다.
                maxScore = -9999;
                for (int i = 0; i < candidates.Count; i++)
                {
                    ScenarioEntry entry = candidates[i];
                    if (!CheckFlags(entry.requiredFlags, entry.blockingFlags, activeFlags)) continue;

                    int score = CalculateScore(entry, currentMood, currentAffection, currentObsession, idleHours);
                    if (score > maxScore) maxScore = score;
                }
                
                if (maxScore == -9999) return null; // 플래그 제한 등으로 아예 진입 가능한 씬이 없는 경우
            }

            // 2-2. 최고점 오차 범위(Top-Tier Pool) 내의 씬들을 수집
            List<ScenarioEntry> topPool = new List<ScenarioEntry>();
            int scoreThreshold = maxScore - 5; // 최고점 대비 5점 차이까지는 동급으로 취급 (다양성 확보)
            
            for (int i = 0; i < candidates.Count; i++)
            {
                ScenarioEntry entry = candidates[i];
                if (!CheckFlags(entry.requiredFlags, entry.blockingFlags, activeFlags)) continue;
                
                // allOnCooldown 상태가 아니면 쿨다운 필터링 정상 적용, 맞다면 쿨다운 무시
                if (!allOnCooldown && recentScenarios.Contains(entry.scenarioID)) continue;

                int score = CalculateScore(entry, currentMood, currentAffection, currentObsession, idleHours);
                if (score >= scoreThreshold)
                {
                    topPool.Add(entry);
                }
            }

            if (topPool.Count == 0) return null;

            // 2-3. Top-Tier Pool 내에서 랜덤 추출 (다회차 앵무새 방지)
            int randomIndex = UnityEngine.Random.Range(0, topPool.Count);
            ScenarioEntry bestEntry = topPool[randomIndex];

            // 3. 기록 및 반환
            recentScenarios.Add(bestEntry.scenarioID);
            if (recentScenarios.Count > 10) // 큐 방식 제한 필요 시 구현
            {
                // 단순화를 위해 주기적 클리어 구조 (추후 최적화 가능)
                // 현재는 데모용으로 해시셋이 너무 커지면 초기화
                if (recentScenarios.Count > 50) recentScenarios.Clear();
            }

            return bestEntry;
        }

        // 플래그 파싱 및 검증용 Zero-Alloc 로직 뼈대
        private bool CheckFlags(string reqFlags, string blockFlags, List<string> activeFlags)
        {
            if (activeFlags == null) return true;
            // 콤마 파싱 오버헤드가 있으나, 뼈대 설계이므로 구조만 잡음
            // 실제 데이터 연동 시 string[] 배열로 데이터베이스를 구축하는 것이 좋음
            return true; 
        }

        // 스코어 계산 로직 분리 (가독성 및 재사용성)
        private int CalculateScore(ScenarioEntry entry, string currentMood, int currentAffection, int currentObsession, float idleHours)
        {
            int score = 0;
            if (entry.requiredMood != "Any" && entry.requiredMood == currentMood) score += 50;

            int affDiff = Mathf.Abs(entry.targetAffection - currentAffection);
            int obsDiff = Mathf.Abs(entry.targetObsession - currentObsession);
            score += Mathf.Max(0, 30 - affDiff);
            score += Mathf.Max(0, 30 - obsDiff);

            if (idleHours >= entry.minIdleHours && entry.minIdleHours > 0) score += 40;

            return score;
        }
    }
}
