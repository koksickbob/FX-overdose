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

        // 플레이어가 슬롯을 구분하기 위해 직접 입력한 표시 이름입니다.
        public string SaveName;

        // 구버전 JSON에는 이 필드가 없으므로 enum 기본값인 Story(0)로 안전하게 복원됩니다.
        public GameMode GameMode = GameMode.Story;
        // 기존 세이브에는 값이 없어 enum 기본값 Hard(현행 난이도)로 호환됩니다.
        public StoryDifficulty StoryDifficulty = StoryDifficulty.Hard;

        // --- GameManager 상태 ---
        public float Balance = 1000f;

        // 달력 날짜 (2026-08 개편). JsonUtility가 DateTime을 직렬화하지 못하므로 ISO 문자열입니다.
        // ⚠️ 초기화자를 빈 문자열이 아닌 실제 날짜로 바꾸지 마십시오. 빈 문자열은 "달력 도입 이전
        //    세이브"를 가려내는 판정 기준입니다. 기본 날짜를 박으면 구버전 JSON에 이 키가 없어
        //    초기화자 값이 그대로 남고, 20일차 세이브가 1일차 날짜로 로드됩니다.
        //    새 게임의 초기값은 SaveLoadManager.PrepareNewGame()이 명시적으로 심습니다.
        public string CurrentDate = "";
        // 세이브가 만들어진 시점의 에폭. 밸런싱으로 기본 에폭을 바꿔도 진행 중인 세이브의
        // 일차 계산이 어긋나지 않도록 세이브가 스스로 들고 있습니다.
        public string StartDate = "";

        // 일차 서수. CurrentDate에서 파생되는 값이지만 계속 기록합니다 —
        // 구버전 호환, 날짜 손상 시의 역산 근거, DatingDay 계약 검증에 쓰입니다.
        public int CurrentDay = 1;
        public int CurrentHour = 9;
        public int CurrentMinute = 0;
        // ⚠️ 기록 전용입니다 — 불러오기 때 게임에 되돌리지 않습니다.
        // 기준 배속은 씬에 직렬화된 값이 유일한 출처이며(설계 상수), 이 필드는 해당 세션이
        // 어떤 배속으로 돌았는지 남기는 진단 기록입니다. 복원하면 기준값을 조정해도
        // 기존 세이브가 옛 값에 고착됩니다. 자세한 경위는 SaveLoadManager의 복원 블록 주석 참고.
        // -1 = GameManager가 없는 씬(요미의 방·월드맵)에서 저장돼 기록되지 않음.
        public float SecondsPerGameMinute = -1f;
        public float StartOfDayEquity = -1f;

        // 당일 P&L 스파크라인 궤적(인게임 15분 간격 총자산 표본).
        // 비어 있으면 복원 측이 StartOfDayEquity로 1점 시딩합니다 —
        // 구버전 세이브는 이 키가 없어 초기화자의 빈 리스트가 유지되므로 마이그레이터가 필요 없습니다.
        public List<float> DailyEquityHistory = new List<float>();

        // --- TraderStatus 상태 ---
        public float PeakBalance = 0f;
        public float CurrentMental = 100f;
        public float CurrentHealth = 100f; // 24.12.01: 체력 저장 복구
        public float MaxMental = 100f;
        public TraderStatus.MentalState CurrentMentalState = TraderStatus.MentalState.Stable;

        // --- 인벤토리 상태 ---
        public List<string> InventoryItemIds = new List<string>();
        public List<int> InventoryItemQuantities = new List<int>();

        // --- 튜토리얼 완료 플래그 ---
        public bool IsTutorialCompleted = false;

        // --- 재접속 복귀 지점 ---
        // 저장 당시 플레이어가 있던 씬. 빈 문자열이면 GameScene(구버전 세이브의 기존 동작).
        // 기록 대상은 SaveLoadManager.ResumableScenes로 제한됩니다.
        public string LastSceneName = "";

        // 시작 아이템(에너지드링크·파르페 5개, 약품 2개)을 아직 지급받지 못했음을 뜻합니다.
        // 아이템 구성의 출처가 GameScene에 배치된 ItemData 슬롯이라 데이터만으로는 심을 수 없어,
        // 복원 경로가 이 플래그를 보고 Inventory.ResetForNewGame()을 대신 호출합니다.
        // 기본값 false = "이미 지급됨"이라 구버전 세이브는 마이그레이션 없이 안전합니다.
        public bool NeedsStartingItems = false;

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

        // --- 편의점 알바 (Phase 2) ---
        // 누적 근무 횟수. 손님 스폰 tier 선택에만 쓰입니다. 근무 중간 상태는 저장하지 않습니다.
        // 기본값 0이라 구버전 세이브는 마이그레이션 없이 tier 0으로 안전하게 시작합니다.
        public int StoreTotalShifts = 0;

        // --- TraderStatus 중독 / 연패 상태 (SV-A1~A3) ---
        public bool IsLeverageAddicted = false;
        public int ConsecutiveHighLevWins = 0;
        public int ConsecutiveLowLevTrades = 0;
        public int CurrentLosingStreak = 0;

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

        // --- 자유 채팅 일일 한도 (2026-08-14 개편) ---
        // 자유 채팅은 하루 1회. 카운트는 대화가 끝나는 시점(완주·중도 종료 모두)에 소모됩니다.
        public int TalkLastSessionEndDay = -1;
        // 당일 한정 필드(TalkAffectionGainToday/TalkTopicsUsedToday)가 어느 일차의 것인지.
        // 일차 전환 리셋이 계획(4.5절)만 있고 구현이 없었어서, 읽는 쪽(YomiRoomManager)이
        // 일차가 바뀐 것을 보면 스스로 비웁니다. 값이 어긋나 있어도 다음 대화 시작 때 자가 치유됩니다.
        public int TalkDailyStateDay = -1;

        // --- 이벤트 진행 시스템 (EventSystem) ---
        // 이 세 필드는 매니저가 아니라 SaveData 자신이 주인입니다. EventRunner가 CurrentData에 직접 쓰고
        // EventLauncher가 직접 읽습니다. SaveGame이 CurrentData를 베이스로 삼으므로(SV-A6) 별도의
        // gather/scatter 코드가 필요 없고, 구버전 JSON에 키가 없어도 초기화자가 유지되므로 마이그레이션도 없습니다.
        //
        // 종료 플래그. 끝까지 진행된 이벤트만 들어옵니다 — 중도 이탈은 아무것도 남기지 않습니다.
        public List<string> EventCompletedIds = new List<string>();
        // 선택 이력. "<이벤트ID>:<노드>:<선택인덱스>" 한 줄 = 선택 1회. TalkChoiceHistory와 동일 포맷·동일 상한.
        public List<string> EventChoiceHistory = new List<string>();
        // 분기 플래그. "MAIN_ROUTE_A" 등. 이후 이벤트의 조건 판정에 쓰입니다.
        public List<string> StoryFlags = new List<string>();

        // --- 호감도 기반 토픽 해금 ---
        // 해금은 '현재 호감도'가 아니라 '역대 최고'로 판정합니다.
        // 호감도가 깎였다고 이미 열린 화제가 다시 잠기면 진행하던 대화가 증발합니다. (TS8)
        public int TalkPeakAffection = 0;
        // TalkAffectionTierSeen 제거 (2026-08-14, F-8): "승급 연출용"으로 미리 넣었으나 읽지도 쓰지도
        // 않는 사문 필드였습니다. 연출을 실제로 만들 때 그 코드와 함께 추가하십시오.
        // 구버전 JSON에 남은 키는 JsonUtility가 조용히 무시하므로 마이그레이션이 필요 없습니다.
    }
}
