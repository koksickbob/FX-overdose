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
        // 저장 시 SaveLoadManager에서 Application.version을 자동으로 주입합니다.
        public string Version;

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
        public MarketSimulationEngine.MarketRegime CurrentDailyRegime;
        // CurrentSignalPhase 제거(SV-D2): activeSignal 객체가 직렬화되지 않아 복원이 불가능했고,
        // 로드 시 항상 None으로 덮어쓰고 있었습니다.

        // --- 차트 엔진 시간축 / 국면 유지 (SV-B3~B5) ---
        public int MarketLastUpdatedDay = -1;
        public int MinutesUntilNextRegimeChange = 60;
        public long MarketTotalMinutes = 0;

        // --- 그날의 거시 방향성 (SV-B8) ---
        public int OutlookDay = -1;
        public MarketSimulationEngine.MarketRegime OutlookRegime;
        public bool OutlookRevealed = false;

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

        // CurrentEmotion 제거(SV-D1): 수집부에도 주입부에도 쓰이지 않는 데드 필드였습니다.
        // 감정은 매 순간 상황에서 재계산되므로 복원할 이유가 없습니다.

        // --- 현재 활성 포지션 및 거래 모드 데이터 ---
        public TradingController.TradingMode ActiveTradingMode;
        public TradingController.AITradingStyle AITradingStyle = TradingController.AITradingStyle.Balanced;
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

        public int LastSteakPurchaseDay = -999;
        public float PastaBuffRemainingSeconds = 0f;

        // --- 보스 데이터 ---
        public float SavedBossStartingAsset = -1f;
        public float SavedBossCurrentAsset = -1f;

        // --- DatingSim 상태 (Phase 2) ---
        public int DatingStamina = 100;
        public int DatingMaxStamina = 100;
        public int DatingAffection = 0;
        public int DatingObsession = 0;
        public int StoryProgressStage = 0;
        public int DatingTimeSlot = 5;
        public int DatingDay = 1;

        // --- TraderStatus 중독 / 연패 상태 (SV-A1~A3) ---
        public bool IsLeverageAddicted = false;
        public int ConsecutiveHighLevWins = 0;
        public int ConsecutiveLowLevTrades = 0;
        public int CurrentLosingStreak = 0;
        public bool CanRegenMental = true;

        // --- 돌발 선택 이벤트 일일 스케줄 (SV-A4) ---
        public int EventLastTriggerDay = -1;
        public int EventsTriggeredToday = 0;
        public int LowMentalEventsTriggeredToday = 0;
        public int EventNextRandomTriggerMinuteOfDay = -1;
        public long EventLastTriggerGameMinutes = -999999L;

        // --- 이벤트 강제 포지션 계약 (SV-A5) ---
        public bool IsEventTradeActive = false;
        public TradingController.EventPositionHandlingMode EventHandlingMode = TradingController.EventPositionHandlingMode.StandardAuto;
        public float EventTargetROELimit = 0f;
        public float EventStopLossROELimit = 0f;
        public bool IsEventPlayerChoice = false;
        public bool IsEventTrueSignal = true;

        // --- 일일 정산 문맥 (SV-B6, SV-B9) ---
        public float TodayRegularDeduction = 0f;
        public string TodayRegularDeductionReason = "";
        public bool IsSettlementProcessing = false;

        // --- 요미 선택형 대화 (P4) ---
        public int TalkAffectionGainToday = 0;
        public int TalkHintIssuedDay = -1;
        public List<string> TalkTopicsUsedToday = new List<string>();

        // 진행 중인 시나리오 (T1/T2). 중단된 대화는 재개하지 않지만, 어떤 토픽에서 끊겼는지는 남깁니다.
        public string TalkActiveTopicId = "";
        public int TalkActiveNodeIndex = -1;

        // 선택 이력 (T3). "<토픽ID>:<노드>:<선택인덱스>" 한 줄 = 선택 1회.
        // JsonUtility가 중첩 구조를 직렬화하지 못하므로 문자열로 인코딩합니다.
        public List<string> TalkChoiceHistory = new List<string>();

        // 발급된 힌트 내역 (T4). 로드 후 재발급을 막습니다.
        public int TalkHintTier = 0;      // 0=미발급 / 1=모호 / 2=명시
        public string TalkHintLineId = "";

        // 누적 이력 (T5/T6/T7)
        public List<string> TalkTopicsSeenTotal = new List<string>();
        public List<string> TalkCompletedFlags = new List<string>();
        public int TalkLastGreetingDay = -1;

        // --- 호감도 기반 토픽 해금 ---
        // 해금은 '현재 호감도'가 아니라 '역대 최고'로 판정합니다.
        // 호감도가 깎였다고 이미 열린 화제가 다시 잠기면 진행하던 대화가 증발합니다. (TS8)
        public int TalkPeakAffection = 0;
        public int TalkAffectionTierSeen = 0; // 플레이어가 인지한 해금 단계. 승급 연출용
    }
}
