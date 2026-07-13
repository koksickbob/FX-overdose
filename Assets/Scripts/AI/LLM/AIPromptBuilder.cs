using UnityEngine;
using FXOverdose.Trading;

namespace FXOverdose.AI.LLM
{
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
            "절대 2문장을 넘기지 말고 딱 1~2문장의 짧고 강렬한 혼잣말 독백을 작성하라.";

        private void Start()
        {
            if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
            if (traderStatus == null) traderStatus = FindAnyObjectByType<TraderStatus>();
            if (tradingController == null) tradingController = FindAnyObjectByType<TradingController>();
            if (marketEngine == null) marketEngine = FindAnyObjectByType<MarketSimulationEngine>();
            if (aiBrain == null) aiBrain = FindAnyObjectByType<AITradingBrain>();
        }

        // LLM에 전송할 완성된 프롬프트 생성
        public string BuildPrompt(string extraEventContext = "")
        {
            float balance = gameManager != null ? gameManager.CurrentBalance : 10000f;
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

            string contextInfo = 
                $"[현재 인게임 상태]\n" +
                $"- 보유 자산: ${balance:N2}\n" +
                $"- 현재 포지션: {posText} (ROE: {roe:+0.0;-0.0;0.0}%)\n" +
                $"- AI 체력: {healthRatio*100:0}%, 멘탈 상태: {mentalState}\n" +
                $"- 시장 국면: {marketRegime}\n" +
                $"- AI 직전 판단: {lastDecision}\n" +
                (!string.IsNullOrEmpty(extraEventContext) ? $"- 최근 이벤트 발생: {extraEventContext}\n" : "");

            string finalPrompt = $"{systemPersona}\n\n{contextInfo}\n위 상황에 맞춰 타인 없이 오직 홀로 투자를 진행하며 읊조리는 1~2문장의 생동감 넘치는 반말 멘헤라 혼잣말 독백을 작성해.";
            return finalPrompt;
        }
    }
}
