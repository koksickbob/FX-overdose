using System.Collections.Generic;
using UnityEngine;

namespace FXOverdose.DatingSim.Scenario
{
    [CreateAssetMenu(fileName = "ScenarioDatabase", menuName = "FX Overdose/Dating Sim/Scenario Database")]
    public class ScenarioDatabase : ScriptableObject
    {
        public List<ScenarioEntry> entries = new List<ScenarioEntry>();
        
        // 런타임 최적화를 위한 딕셔너리 (초기화 시 자동 생성)
        // Key: Category_RequiredTime (예: Daily_Night)
        private Dictionary<string, List<ScenarioEntry>> lookupTable;

        public void InitializeLookupTable()
        {
            if (lookupTable != null) return;
            lookupTable = new Dictionary<string, List<ScenarioEntry>>();
            
            foreach (var entry in entries)
            {
                // 시간대 제약이 없으면 Any로 통일
                string timeKey = string.IsNullOrEmpty(entry.requiredTime) ? "Any" : entry.requiredTime;
                string catKey = string.IsNullOrEmpty(entry.category) ? "None" : entry.category;
                
                string key = $"{catKey}_{timeKey}";
                
                if (!lookupTable.ContainsKey(key))
                {
                    lookupTable[key] = new List<ScenarioEntry>();
                }
                lookupTable[key].Add(entry);
            }
        }

        /// <summary>
        /// 특정 카테고리와 시간에 매칭되는 시나리오 그룹을 O(1) 속도로 즉시 가져옵니다.
        /// </summary>
        public List<ScenarioEntry> GetCandidates(string category, string timeOfDay)
        {
            InitializeLookupTable();
            string key = $"{category}_{timeOfDay}";
            
            if (lookupTable.TryGetValue(key, out var list))
            {
                return list;
            }
            return new List<ScenarioEntry>();
        }
    }
}
