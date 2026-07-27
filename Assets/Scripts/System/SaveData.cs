using System;
using System.Collections.Generic;
using FXOverdose.AI;
using FXOverdose.Trading;

namespace FXOverdose.Core
{
    [Serializable]
    public class SaveData
    {
        public string Version = "1.4.0"; // 1.4.0: 액티브 아이템 보유 레벨 저장 추가

        // 구버전 JSON에는 이 필드가 없으므로 enum 기본값인 Story(0)로 안전하게 복원됩니다.
        public GameMode GameMode = GameMode.Story;

        // --- GameManager 데이터 ---
        public float Balance;
        public int CurrentDay;
        public int CurrentHour;
        public int CurrentMinute;
        public float SecondsPerGameMinute;
        public float StartOfDayEquity;

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

        // --- 활성 포지션 유지 데이터 ---
        public bool HasActivePosition;
        public TradingController.PositionType PositionType;
        public float EntryPrice;
        public float MarginAmount;
        public int CurrentLeverage;
        public float TargetPrice;
        public float StopLossPrice;

        // --- 코스튬 데이터 ---
        public List<string> OwnedCostumeIds = new List<string> { CostumeManager.StandardId };
        public string EquippedCostumeId = CostumeManager.StandardId;

        // --- 액티브 아이템 데이터 ---
        // JsonUtility가 Dictionary를 직렬화하지 못하므로 아이템 ID와 레벨을 같은 인덱스의 병렬 리스트로 저장합니다.
        public List<string> ActiveItemIds = new List<string>();
        public List<int> ActiveItemLevels = new List<int>();
    }
}
