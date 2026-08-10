using UnityEngine;
using FXOverdose.DatingSim.LLM.Scenario;
using FXOverdose.DatingSim.Core;
using System.Collections.Generic;

namespace FXOverdose.DatingSim.LLM
{
    public class ScenarioManager : MonoBehaviour
    {
        public static ScenarioManager Instance { get; private set; }

        public ScenarioEntry CurrentScenario { get; private set; }
        private int currentTurnCount = 0;

        // 임시 더미 플래그 리스트 (실제 게임에서는 SaveData에서 로드)
        public List<string> ActiveFlags = new List<string>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void StartNewScenario(string category, string timeOfDay)
        {
            int affection = DatingTimeManager.Instance != null ? DatingTimeManager.Instance.CurrentAffection : 50;
            int obsession = DatingTimeManager.Instance != null ? DatingTimeManager.Instance.CurrentObsession : 50;
            string mood = "Neutral"; // 임시
            float idle = 0f;

            CurrentScenario = ScenarioMatcher.Instance.GetBestScenario(category, timeOfDay, affection, obsession, mood, idle, ActiveFlags);
            currentTurnCount = 0;
            
            if (CurrentScenario != null)
            {
                Debug.Log($"[ScenarioManager] 씬 시작됨: {CurrentScenario.scenarioID}");
            }
            else
            {
                Debug.LogWarning($"[ScenarioManager] 적합한 씬을 찾을 수 없습니다. 기본 상태 유지.");
            }
        }

        public void IncrementTurn()
        {
            currentTurnCount++;
            if (CurrentScenario != null && currentTurnCount >= CurrentScenario.maxTurns)
            {
                Debug.Log($"[ScenarioManager] 씬 턴 한도 도달. 강제 종료 시퀀스 진입 필요.");
                // TODO: 씬 종료 처리 및 보상/플래그 지급 로직 추가
                CurrentScenario = null;
            }
        }
    }
}
