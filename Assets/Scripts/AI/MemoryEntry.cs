using System;
using FXOverdose.AI.LLM;

namespace FXOverdose.AI
{
    [Serializable]
    public struct MemoryEntry
    {
        public int Day;                  // 기록된 날짜 (일차)
        public string TimeString;        // 시간 문자열 (예: "09:30")
        public EventCategory Category;   // 이벤트 분류
        public int ImportanceScore;      // 감정 충격량 / 중요도 점수 (1~10점)
        public string Description;       // 이벤트 상세 설명

        public MemoryEntry(int day, string timeString, EventCategory category, int importanceScore, string description)
        {
            Day = day;
            TimeString = timeString;
            Category = category;
            ImportanceScore = importanceScore;
            Description = description;
        }

        public override string ToString()
        {
            return $"[Day {Day} {TimeString}] ({Category}, 중요도:{ImportanceScore}/10) {Description}";
        }
    }
}
