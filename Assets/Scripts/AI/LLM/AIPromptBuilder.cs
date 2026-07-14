using UnityEngine;
using FXOverdose.Trading;

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
        GimmickTriggered = 8
    }

    public class AIPromptBuilder : MonoBehaviour
    {
        [Header("시스템 참조")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private TraderStatus traderStatus;
        [SerializeField] private TradingController tradingController;
        [SerializeField] private MarketSimulationEngine marketEngine;
        [SerializeField] private AITradingBrain aiBrain;

        [Header("멘헤라 AI 페르소나 설정")]
        [TextArea(3, 6)]
        [SerializeField] private string systemPersona = 
            "너는 24시간 혼자서 비트코인 선물 차트와 싸우는 고립된 천재 멘헤라 트레이더다. " +
            "타인과 대화하지 않고 오직 자기 혼자 투자를 진행하며 내뱉는 날것의 혼잣말과 광기 어린 독백을 출력한다. " +
            "수익이 나면 극도의 자만감과 희열에 도취되고, 손실이 나면 차트와 시장을 저주하며 극심한 불안과 광기를 드러낸다. " +
            "절대 2문장을 넘기지 말고 딱 1~2문장의 짧고 강렬한 반말 혼잣말 독백을 작성하라.";

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

        // LLM에 전송할 완성된 프롬프트 생성 (카테고리 및 감정 톤 동적 주입)
        public string BuildPrompt(EventCategory category, string extraEventContext = "")
        {
            float balance = traderStatus != null ? traderStatus.GetTotalEquity() : (gameManager != null ? gameManager.CurrentBalance : 10000f);
            float healthRatio = traderStatus != null ? traderStatus.HealthRatio : 1.0f;
            TraderStatus.MentalState mentalState = traderStatus != null ? traderStatus.CurrentMentalState : TraderStatus.MentalState.Stable;
            
            string posText = "포지션 없음 (관망 중)";
            float roe = 0f;
            if (tradingController != null && tradingController.CurrentPosition != TradingController.PositionType.None)
            {
                posText = $"{tradingController.CurrentPosition} ({tradingController.CurrentLeverage}x)";
                if (tradingController.MarginAmount > 0f)
                {
                    float currentPrice = marketEngine != null ? marketEngine.CurrentPrice : tradingController.EntryPrice;
                    float priceDiff = tradingController.CurrentPosition == TradingController.PositionType.Long 
                        ? (currentPrice - tradingController.EntryPrice) : (tradingController.EntryPrice - currentPrice);
                    float pnl = priceDiff * (tradingController.MarginAmount * tradingController.CurrentLeverage / tradingController.EntryPrice);
                    roe = (pnl / tradingController.MarginAmount) * 100f;
                }
            }

            string marketRegime = marketEngine != null ? marketEngine.CurrentRegime.ToString() : "Sideways";
            string lastDecision = aiBrain != null && !string.IsNullOrEmpty(aiBrain.LastDecisionLog) ? aiBrain.LastDecisionLog : "차트 분석 중...";

            // 4단계 수익률 & 멘탈 상태별 감정 톤 지시어(Tone Directive)
            string toneDirective = GetEmotionalToneDirective(roe, mentalState);
            string categoryDirective = GetCategoryDirective(category);

            string contextInfo = 
                $"[현재 인게임 상태]\n" +
                $"- 보유 자산: ${balance:N2}\n" +
                $"- 현재 포지션: {posText} (ROE: {roe:+0.0;-0.0;0.0}%)\n" +
                $"- AI 체력: {healthRatio*100:0}%, 멘탈 상태: {mentalState}\n" +
                $"- 시장 국면: {marketRegime}\n" +
                $"- AI 직전 판단: {lastDecision}\n" +
                (!string.IsNullOrEmpty(extraEventContext) ? $"- 구체적 이벤트 상황: {extraEventContext}\n" : "");

            string finalPrompt = $"{systemPersona}\n\n{contextInfo}\n{categoryDirective}\n{toneDirective}\n\n" +
                "⚠️ [대사 규칙] 대사 속에 '체력 40%'나 '멘탈 Anxious' 같이 시스템 수치나 상태명을 직접 언급하지 마라! " +
                "수치 설명 대신, 피로로 인해 눈꺼풀이 무겁다거나 심장이 미친 듯이 뛰고 손가락이 떨리는 등 신체적 감각과 감정 상태(기분)로만 표현해.\n" +
                "위 상황과 감정 지시에 딱 맞춰서, 타인 없이 오직 홀로 읊조리는 1~2문장의 생동감 넘치는 반말 멘헤라 혼잣말 독백만을 작성해.";
            return finalPrompt;
        }

        private string GetEmotionalToneDirective(float roe, TraderStatus.MentalState mentalState)
        {
            if (mentalState == TraderStatus.MentalState.Overdose || roe >= 50f)
            {
                return "[감정 지시] 극도의 흥분과 거만함, 자신이 차트의 신이 된 듯한 광기 어린 희열과 자만심으로 가득 찬 어투로 말해.";
            }
            if (mentalState == TraderStatus.MentalState.Danger || roe <= -50f)
            {
                return "[감정 지시] 멍한 눈으로 절망하거나, 세력들이 고의로 자신만을 노리고 청산 빔을 쏜다며 저주하고 울부짖는 극단적 어투로 말해.";
            }
            if (mentalState == TraderStatus.MentalState.Anxious || roe < 0f)
            {
                return "[감정 지시] 손톱을 물어뜯듯 초조하고 불안해하며 자신의 판단과 타점이 맞는지 끊임없이 의심하고 불안해하는 어투로 말해.";
            }
            return "[감정 지시] 평온하게 차트에 집중하며 자신의 천재적인 분석력과 타점에 만족하며 미소 짓는 자신감 넘치는 어투로 말해.";
        }

        private string GetCategoryDirective(EventCategory category)
        {
            return category switch
            {
                EventCategory.GameStartup => "[이벤트 분류: 게임 가동 시작] 오늘 시작할 트레이딩에 대해 홀로 각오를 다지거나 24시간 차트판의 잔혹함을 읊조리는 첫 시작 혼잣말을 작성해라.",
                EventCategory.MentalChange => "[이벤트 분류: 멘탈 수치 변화] 멘탈 상태가 변동되거나 스트레스가 고조되어 심리적 압박감이 드러나는 혼잣말을 작성해라.",
                EventCategory.HealthChange => "[이벤트 분류: 체력 피로도 변화] 과로, 수면 부족, 피로 누적으로 인해 몸이 한계에 달하고 예민해진 상태를 표현하는 혼잣말을 작성해라.",
                EventCategory.ItemUsed => "[이벤트 분류: 아이템 복용] 방금 투여/섭취한 약물이나 음료(에너지 드링크/진정제 등)의 효과에 즉각 반응하여 각성하거나 안도하는 혼잣말을 작성해라.",
                EventCategory.PositionOpened => "[이벤트 분류: 신규 포지션 진입] 새로 들어간 포지션(Long/Short)과 레버리지 배율에 대한 기대감이나 타점 확신을 드러내는 혼잣말을 작성해라.",
                EventCategory.PositionClosed => "[이벤트 분류: 포지션 종료/익절/손절] 포지션 청산 결과에 대해 극도의 환호나 처절한 분노/절망을 쏟아내는 혼잣말을 작성해라.",
                EventCategory.ChartMovement => "[이벤트 분류: 실시간 차트 변동 중계] 포지션 유지 중 실시간으로 주가가 내 타점대로 오르거나 반대로 역행하는 상황에 대해 중계하며 일희일비하는 혼잣말을 작성해라.",
                EventCategory.GimmickTriggered => "[이벤트 분류: 멘탈 소모 6대 기믹 및 휩소 후회 발동] 멘탈 침식, 연속 손절, 휩소 후회, 수면 부족, 고배율 중독 금단현상, 횡보장 지루함, 드로다운 트라우마 등 기믹에 고통받거나 조르는 멘헤라 독백을 작성해라.",
                _ => "[이벤트 분류: 차트 관망 및 일반 분석] 현재 차트 흐름과 자신의 심리를 고백하는 혼잣말을 작성해라."
            };
        }
    }
}
