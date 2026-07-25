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
        private Dictionary<string, List<YomiDialogueEntry>> eventLookupTable; // 이벤트 대사용 딕셔너리
        
        public void InitializeLookupTable()
        {
            if (lookupTable != null) return;
            lookupTable = new Dictionary<string, List<YomiDialogueEntry>>();
            eventLookupTable = new Dictionary<string, List<YomiDialogueEntry>>();
            
            foreach (var entry in entries)
            {
                // 이벤트용 대사인 경우 별도 테이블에 저장
                if (!string.IsNullOrEmpty(entry.eventCategory))
                {
                    if (!eventLookupTable.ContainsKey(entry.eventCategory))
                        eventLookupTable[entry.eventCategory] = new List<YomiDialogueEntry>();
                    eventLookupTable[entry.eventCategory].Add(entry);
                }
                else
                {
                    // 일반 대사는 기존 포지션_트렌드 키로 저장
                    string key = $"{entry.position}_{entry.marketTrend}";
                    if (!lookupTable.ContainsKey(key))
                    {
                        lookupTable[key] = new List<YomiDialogueEntry>();
                    }
                    lookupTable[key].Add(entry);
                }
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

        public List<YomiDialogueEntry> GetEventCandidates(string eventCategory)
        {
            InitializeLookupTable();
            if (eventLookupTable.TryGetValue(eventCategory, out var list))
            {
                return list;
            }
            return new List<YomiDialogueEntry>();
        }
    }
}
