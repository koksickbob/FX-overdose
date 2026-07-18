using UnityEngine;
using System.Text;
using FXOverdose.Trading;
using FXOverdose.AI;

namespace FXOverdose.AI.LLM
{
    public enum EventCategory
    {
        General = 0,
        GameStartup = 1,
        MentalChange = 2,
        HealthChange = 3,
        ItemUsed = 4,
        PositionOpened = 5,
        PositionClosed = 6,
        ChartMovement = 7,
        GimmickTriggered = 8,
        SkillUpgraded = 9,
        DailySettlement = 10
    }

    public class AIPromptBuilder : MonoBehaviour
    {
        [Header("시스템 참조")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private TraderStatus traderStatus;
        [SerializeField] private TradingController tradingController;
        [SerializeField] private MarketSimulationEngine marketEngine;
        [SerializeField] private AITradingBrain aiBrain;

        [TextArea(8, 15)]
        public string systemPersona = "";
        private void Awake()
        {
            // 유니티 인스펙터(Scene)에 직렬화되어 남아있는 과거의 짧은 프롬프트를 무시하고,
            // 런타임 시작 시 무조건 파인튜닝 원본 데이터셋과 100% 동일한 프롬프트로 강제 덮어씌웁니다.
            systemPersona = 
                "너는 24시간 비트코인 선물 차트에 미쳐 있는 차트 중독자이자 플레이어('오빠')에게 의존하는 20대 멘헤라 트레이더(이름: 요미)다. " +
                "대답을 생성할 때 반드시 다음 '자연스러운 의존형 멘헤라 가이드'를 엄격하게 지켜 1~2문장으로 짧게 일상 반말로 작성하라.\n" +
                "[규칙 1. 자연스러운 일상 반말] 친근하고 일상적인 구어체(~어, ~지?, ~야)를 사용. 억지 애교(뿌엥, ><, 아기 말투)나 만화적인 이모티콘 남발 절대 금지.\n" +
                "[규칙 2. 감정 표현] 수익 시엔 오빠에게 칭찬을 갈구하며 방방 뛰고, 손실 시엔 현실적으로 절망하며 오빠에게 모든 책임을 미루거나 징징거릴 것.\n" +
                "[규칙 3. 일상적인 코인 은어] 과도하게 꾸며낸 비유 대신 자연스러운 은어(물기둥, 떡상, 나락, 빔)나 차트 묘사를 사용할 것.\n" +
                "[규칙 4. 의존적인 대화 패턴] 무섭거나 그로테스크한 묘사를 피하고, 어떤 상황이든 대화의 결론은 '오빠에 대한 애정 확인'이나 '자신을 구원해달라'로 끝낼 것.";
        }

        private void Start()
        {
            if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
            if (traderStatus == null) traderStatus = FindAnyObjectByType<TraderStatus>();
            if (tradingController == null) tradingController = FindAnyObjectByType<TradingController>();
            if (marketEngine == null) marketEngine = FindAnyObjectByType<MarketSimulationEngine>();
            if (aiBrain == null) aiBrain = FindAnyObjectByType<AITradingBrain>();
        }

        public string BuildPrompt(string extraEventContext = "")
        {
            return BuildPrompt(EventCategory.General, extraEventContext);
        }

        // 파인튜닝된 0.5B 모델을 위한 초경량(Ultra-light) 프롬프트 맵핑
        public string BuildPrompt(EventCategory category, string extraEventContext = "")
        {
            float healthRatio = traderStatus != null ? traderStatus.HealthRatio : 1.0f;
            TraderStatus.MentalState mentalState = traderStatus != null ? traderStatus.CurrentMentalState : TraderStatus.MentalState.Stable;
            
            string posText = "None";
            float roe = 0f;
            if (tradingController != null && tradingController.CurrentPosition != TradingController.PositionType.None)
            {
                posText = tradingController.CurrentPosition == TradingController.PositionType.Long ? "Long" : "Short";
                if (tradingController.MarginAmount > 0f)
                {
                    float currentPrice = marketEngine != null ? marketEngine.CurrentPrice : tradingController.EntryPrice;
                    float priceDiff = tradingController.CurrentPosition == TradingController.PositionType.Long 
                        ? (currentPrice - tradingController.EntryPrice) : (tradingController.EntryPrice - currentPrice);
                    float pnl = priceDiff * (tradingController.MarginAmount * tradingController.CurrentLeverage / tradingController.EntryPrice);
                    roe = (pnl / tradingController.MarginAmount) * 100f;
                }
            }

            TraderEmotion currentEmotion = TraderEmotionEvaluator.Evaluate(roe, mentalState, healthRatio, category, extraEventContext);
            var lvlSys = UnityEngine.Object.FindAnyObjectByType<TraderLevelSystem>();
            int heroLvl = lvlSys != null ? lvlSys.ProtagonistLevel : 1;
            int skillLvl = lvlSys != null ? lvlSys.ChartStudyLevel : 1;

            StringBuilder sb = new StringBuilder();

            // 1. 특수 이벤트/기믹 프롬프트 우선 처리
            if (category == EventCategory.GimmickTriggered || category == EventCategory.ItemUsed || 
                category == EventCategory.SkillUpgraded || extraEventContext.Contains("이벤트"))
            {
                string eventType = extraEventContext;
                if (string.IsNullOrEmpty(eventType)) eventType = category.ToString();
                
                string eventCategoryStr = "멘탈소모기믹";
                if (category == EventCategory.ItemUsed) eventCategoryStr = "아이템사용";
                else if (category == EventCategory.SkillUpgraded) eventCategoryStr = "스킬업그레이드";
                else if (extraEventContext.Contains("수동모드_전환")) eventCategoryStr = "수동모드_전환";
                else if (extraEventContext.Contains("수동모드_무포지션")) eventCategoryStr = "수동모드_무포지션";
                else if (extraEventContext.Contains("수동모드_보유중")) eventCategoryStr = "수동모드_보유중";
                else if (extraEventContext.Contains("조기 종료")) eventCategoryStr = "포지션조기종료";
                else if (extraEventContext.Contains("파산") || extraEventContext.Contains("게임오버")) eventCategoryStr = "강제청산";

                sb.AppendLine("[System_Status]");
                sb.AppendLine($"- Event_Category: {eventCategoryStr}");
                sb.AppendLine($"- Mental_State: {currentEmotion.ToString()}");
                if (eventCategoryStr.StartsWith("수동모드"))
                {
                    sb.AppendLine($"- Skill_Level: {skillLvl}");
                }

                sb.AppendLine("\n[Director_Instruction]");
                sb.AppendLine(eventType);

                string evtRecentChats = TraderMemoryManager.Instance != null ? TraderMemoryManager.Instance.GetShortTermDialoguesText() : "없음";
                if (evtRecentChats != "없음 (오늘 첫 대사)" && evtRecentChats != "없음")
                {
                    sb.AppendLine("\n[Recent_Memory]");
                    sb.AppendLine("- 방금 한 말과 비슷한 뉘앙스/단어는 절대 반복하지 말 것!");
                    sb.AppendLine(evtRecentChats);
                }

                return sb.ToString();
            }

            // 2. 일반 차트 매매 기본 프롬프트
            string roeStr = roe == 0 ? "0%" : (roe > 0 ? $"+{roe:0.0}%" : $"{roe:0.0}%");
            string marketRegimeStr = marketEngine != null ? marketEngine.CurrentRegime.ToString() : "Sideways";
            
            int lev = tradingController != null ? tradingController.CurrentLeverage : 10;
            
            sb.AppendLine("[System_Status]");
            sb.AppendLine($"- Market_Trend: {marketRegimeStr}");
            sb.AppendLine($"- Mental_State: {currentEmotion.ToString()}");
            sb.AppendLine($"- Hero_Level: {heroLvl}");
            sb.AppendLine($"- Skill_Level: {skillLvl}");

            sb.AppendLine("\n[Yomi_Status]");
            sb.AppendLine($"- Position: {posText} ({lev}x Leverage)");
            sb.AppendLine($"- Current_ROE: {roeStr}");

            if (!string.IsNullOrEmpty(extraEventContext) && 
                !(category == EventCategory.GimmickTriggered || 
                  category == EventCategory.ItemUsed || 
                  category == EventCategory.SkillUpgraded || 
                  extraEventContext.Contains("이벤트")))
            {
                sb.AppendLine("\n[Director_Instruction]");
                sb.AppendLine(extraEventContext);
            }

            string genRecentChats = TraderMemoryManager.Instance != null ? TraderMemoryManager.Instance.GetShortTermDialoguesText() : "없음";
            if (genRecentChats != "없음 (오늘 첫 대사)" && genRecentChats != "없음")
            {
                sb.AppendLine("\n[Recent_Memory]");
                sb.AppendLine("- 방금 한 말과 비슷한 뉘앙스/단어는 절대 반복하지 말 것!");
                sb.AppendLine(genRecentChats);
            }

            return sb.ToString();
        }
    }
}
