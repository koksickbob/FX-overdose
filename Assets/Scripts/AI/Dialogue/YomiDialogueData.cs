using System;
using UnityEngine;
using System.Collections.Generic;

namespace FXOverdose.AI.Dialogue
{
    public enum DirectionTag
    {
        None = 0,
        Up = 1,     // 상승세, 양봉
        Down = 2    // 하락세, 음봉
    }

    [Serializable]
    public class YomiDialogueEntry
    {
        [TextArea(2, 5)]
        public string text;
        
        // 메타데이터 필터링용
        public string marketTrend;     // Bull, Bear, Sideways, Volatile
        public string mentalState;     // Euphoria, Manic, Furious, Panicked, etc.
        public string position;        // Long, Short, None
        
        // 방향성 요구치 (자동 태깅 됨)
        public DirectionTag requiredDirection;
        
        // 수익 여부 요구치 (수치 자체보다 부호를 중요시)
        public bool isProfit;          // Current_ROE가 0보다 컸는가?
        
        public YomiDialogueEntry(string t, string trend, string mental, string pos, DirectionTag dir, bool profit)
        {
            text = t;
            marketTrend = trend;
            mentalState = mental;
            position = pos;
            requiredDirection = dir;
            isProfit = profit;
        }
    }
}
