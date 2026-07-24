using System.Collections.Generic;
using UnityEngine;

namespace FXOverdose.AI.Dialogue
{
    [CreateAssetMenu(fileName = "YomiDialogueDatabase", menuName = "FX Overdose/AI/Yomi Dialogue Database")]
    public class YomiDialogueDatabase : ScriptableObject
    {
        public List<YomiDialogueEntry> entries = new List<YomiDialogueEntry>();
        
        // 런타임 최적화를 위한 딕셔너리 (초기화 시 자동 생성)
        private Dictionary<string, List<YomiDialogueEntry>> lookupTable;
        
        public void InitializeLookupTable()
        {
            if (lookupTable != null) return;
            lookupTable = new Dictionary<string, List<YomiDialogueEntry>>();
            
            foreach (var entry in entries)
            {
                // Key format: "Position_MarketTrend"
                string key = $"{entry.position}_{entry.marketTrend}";
                if (!lookupTable.ContainsKey(key))
                {
                    lookupTable[key] = new List<YomiDialogueEntry>();
                }
                lookupTable[key].Add(entry);
            }
        }
        
        public List<YomiDialogueEntry> GetCandidates(string position, string marketTrend)
        {
            InitializeLookupTable();
            string key = $"{position}_{marketTrend}";
            
            if (lookupTable.TryGetValue(key, out var list))
            {
                return list;
            }
            
            return new List<YomiDialogueEntry>();
        }
    }
}
