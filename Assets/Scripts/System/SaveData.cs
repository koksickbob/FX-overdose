using System;
using System.Collections.Generic;
using FXOverdose.AI;
using FXOverdose.Trading;

namespace FXOverdose.Core
{
    [Serializable]
    public struct SavedCandle
    {
        public long timestampMinutes;
        public float open;
        public float high;
        public float low;
        public float close;
        public float volume;
    }

    [Serializable]
    public struct TimeframeHistory
    {
        public Timeframe timeframe;
        public List<SavedCandle> candles;
        public SavedCandle liveCandle;
    }

    [Serializable]
    public class SaveData
    {
        public string Version = "1.5.0";

        // 구버전 JSON에는 이 필드가 없으므로 enum 기본값인 Story(0)로 안전하게 복원됩니다.
        public GameMode GameMode = GameMode.Story;

        // --- GameManager 상태 ---
        public float Balance = 1000f;
        public int CurrentDay = 1;
        public int CurrentHour = 9;
        public int CurrentMinute = 0;
        public float SecondsPerGameMinute = 3f;
        public float StartOfDayEquity = -1f;

        // --- TraderStatus 상태 ---
        public float PeakBalance = 0f;
        public float CurrentMental = 100f;
        public float CurrentHealth = 100f; // 24.12.01: 체력 저장 복구
        public float MaxMental = 100f;
        public float MaxMentalLimit = 100f;
        public TraderStatus.MentalState CurrentMentalState = TraderStatus.MentalState.Stable;

        // --- 인벤토리 상태 ---
        public List<string> InventoryItemIds = new List<string>();
        public List<int> InventoryItemQuantities = new List<int>();

        // --- 튜토리얼 완료 플래그 ---
        public bool IsTutorialCompleted = false;

        // --- 차트 및 주가 저장 ---
        public List<TimeframeHistory> ChartHistories = new List<TimeframeHistory>();
        public float CurrentChartPrice;
        public float Current24hHigh;
        public float Current24hLow;
        public float Current24hVolume;
        public MarketSimulationEngine.MarketRegime CurrentRegime;
        public SignalPhase CurrentSignalPhase;

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

        // --- 현재 활성 포지션 및 거래 모드 데이터 ---
        public TradingController.TradingMode ActiveTradingMode;
        public bool HasActivePosition;
        public TradingController.PositionType PositionType;
        public TradingController.OwnerType CurrentOwner;
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

        // --- 배달 음식 데이터 ---
        public int LastSteakPurchaseDay = -999;
        public float PastaBuffRemainingSeconds = 0f;
    }
}
