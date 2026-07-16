using System;
using System.Collections.Generic;
using FXOverdose.AI;
using FXOverdose.Trading;

namespace FXOverdose.Core
{
    [Serializable]
    public class SaveData
    {
        public string Version = "1.0.0"; // 데이터 호환성 버저닝

        // --- GameManager 데이터 ---
        public float Balance;
        public int CurrentDay;
        public int CurrentHour;
        public int CurrentMinute;
        public float SecondsPerGameMinute;

        // --- TraderStatus 데이터 ---
        public float PeakBalance;
        public float CurrentMental;
        public TraderStatus.MentalState CurrentMentalState;
        public float CurrentHealth;
        
        // 인벤토리 상태 등 추가 가능
        // public List<int> ItemInventory = new List<int>();

        // --- TraderLevelSystem 데이터 ---
        public int ProtagonistLevel;
        public float ProtagonistEXP;
        public int ChartStudyLevel;
        public int CubePatienceLevel;
        public int BookJudgmentLevel;

        // --- AI 기억 데이터 (TraderMemoryManager) ---
        public List<string> ShortTermDialogues = new List<string>();
        public List<MemoryEntry> LongTermMemories = new List<MemoryEntry>();
        // Dictionary는 Serializable되지 않으므로 키와 값을 분리하거나 구조체 배열로 변환
        public List<int> DailySummaryKeys = new List<int>();
        public List<string> DailySummaryValues = new List<string>();

        // --- 추가 징후 ---
        public TraderEmotion CurrentEmotion;
    }
}
