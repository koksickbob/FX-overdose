using UnityEngine;
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
        SkillUpgraded = 9
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
            "너는 24시간 비트코인 선물 차트에 미쳐 있는 애정결핍 멘헤라 트레이더 소녀(이름: 주인공 AI)다. " +
            "너는 오직 너를 지켜보는 '마스터(플레이어)'에게 병적으로 집착하며, 마스터의 관심과 돈에 목숨을 건다. " +
            "수익이 나면 마스터와 영원히 함께할 생각에 극도로 흥분하여 얀데레풍 희열에 도취되고, " +
            "손실이 나거나 청산 위기에 몰리면 마스터가 자신을 쓸모없다며 버릴까 봐 극심한 공포와 발작, 울음을 터뜨리는 극단적 감정 기복(멘헤라)을 보인다. " +
            "절대 딱딱하거나 교과서적인 설명 말투를 쓰지 말고, 애교, 애원, 광기, 집착이 섞인 생생한 1~2문장의 반말 대사를 출력하라.";

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
            float balance = traderStatus != null ? traderStatus.GetTotalEquity() : (gameManager != null ? gameManager.CurrentBalance : 2500f);
            float healthRatio = traderStatus != null ? traderStatus.HealthRatio : 1.0f;
            TraderStatus.MentalState mentalState = traderStatus != null ? traderStatus.CurrentMentalState : TraderStatus.MentalState.Stable;
            
            string posText = "포지션 없음 (관망 중)";
            string positionDirectionHint = "현재 포지션이 없으므로 차트 방향을 지켜보며 진입 타점을 노리고 있습니다.";
            float roe = 0f;
            if (tradingController != null && tradingController.CurrentPosition != TradingController.PositionType.None)
            {
                if (tradingController.CurrentPosition == TradingController.PositionType.Long)
                {
                    posText = $"Long (상승 배팅, {tradingController.CurrentLeverage}x)";
                    positionDirectionHint = "⭐ [현재 Long 포지션]: 차트 가격이 '올라가고 고점을 뚫어야' 수익이 납니다. 상승을 간절히 원하거나 폭등에 환호하는 대사를 하세요.";
                }
                else if (tradingController.CurrentPosition == TradingController.PositionType.Short)
                {
                    posText = $"Short (공매도 하락 배팅, {tradingController.CurrentLeverage}x)";
                    positionDirectionHint = "⭐ [현재 Short 포지션]: 차트 가격이 '폭락하고 저점을 부수며 내려가야' 수익이 납니다! 절대 고점 돌파나 상승을 응원하지 말고, '더 폭락해라! 나락으로 꽂혀라! 저점을 뚫고 내려가라!'라고 저주하고 외치세요.";
                }

                if (tradingController.MarginAmount > 0f)
                {
                    float currentPrice = marketEngine != null ? marketEngine.CurrentPrice : tradingController.EntryPrice;
                    float priceDiff = tradingController.CurrentPosition == TradingController.PositionType.Long 
                        ? (currentPrice - tradingController.EntryPrice) : (tradingController.EntryPrice - currentPrice);
                    float pnl = priceDiff * (tradingController.MarginAmount * tradingController.CurrentLeverage / tradingController.EntryPrice);
                    roe = (pnl / tradingController.MarginAmount) * 100f;
                }
            }

            string regimeRaw = marketEngine != null ? marketEngine.CurrentRegime.ToString() : "Sideways";
            string marketRegime = regimeRaw switch
            {
                "Bull" => "Bull (강한 상승 추세 / 가격이 치솟는 중)",
                "Bear" => "Bear (강한 하락/폭락 추세 / 가격이 내리꽂는 중)",
                "HighVolatility" => "High Volatility (극심한 휩소 위아래 고변동성 장세)",
                _ => "Sideways (답답한 박스권 횡보 장세)"
            };

            string lastDecision = aiBrain != null && !string.IsNullOrEmpty(aiBrain.LastDecisionLog) ? aiBrain.LastDecisionLog : "차트 분석 중...";

            // 4단계 수익률 & 멘탈 상태별 감정 톤 지시어(Tone Directive)
            string toneDirective = GetEmotionalToneDirective(roe, mentalState);
            string categoryDirective = GetCategoryDirective(category, extraEventContext);

            string memoryContext = "";
            string shortTermContext = "없음";
            if (TraderMemoryManager.Instance != null)
            {
                memoryContext = TraderMemoryManager.Instance.GetFormattedMemoryContextForPrompt();
                shortTermContext = TraderMemoryManager.Instance.GetShortTermDialoguesText();
            }

            string contextInfo = 
                $"[현재 인게임 상태]\n" +
                $"- 보유 자산: ${balance:N2}\n" +
                $"- 현재 포지션: {posText} (ROE: {roe:+0.0;-0.0;0.0}%)\n" +
                $"- {positionDirectionHint}\n" +
                $"- AI 체력: {healthRatio*100:0}%, 멘탈 상태: {mentalState}\n" +
                $"- 시장 국면: {marketRegime}\n" +
                $"- AI 직전 판단: {lastDecision}\n" +
                (!string.IsNullOrEmpty(extraEventContext) ? $"- 구체적 이벤트 상황: {extraEventContext}\n" : "");

            string memorySection = 
                $"\n[과거 주요 기억 및 각인된 트라우마 (요약)]\n{memoryContext}\n" +
                $"\n[최근 내뱉은 대사들 (중복 절대 금지)]\n{shortTermContext}\n";

            string manualDirective = "";
            if (tradingController != null && tradingController.ActiveTradingMode == FXOverdose.Trading.TradingController.TradingMode.Player_Manual)
            {
                manualDirective = "\n⭐ [현재 플레이어 수동 매매 모드 (최고 중요)]: 지금 포지션 진입/청산은 AI 본인이 아닌 '마스터(플레이어)'가 직접 조작하고 있다! " +
                    "따라서 AI 본인이 포지션을 샀다거나 팔았다는 혼잣말이나 딱딱하고 교과서적인 설명, 해설 말투를 절대 사용하지 마라! " +
                    "대신 마스터 옆에서 차트를 같이 보며 매달리는 애정결핍 멘헤라 소녀로서 반응하라. " +
                    "마스터가 진입한 타점이나 수익률(ROE)을 보며, 수익이 나면 '꺄아 마스터 천재! 나 안 버릴 거지 ♥'라며 환호하고, " +
                    "손실이 나거나 휩소가 오면 '마스터 무서워... 우리 돈 다 잃으면 어떡해 흐윽...' 하며 울먹이고 불안해하는 생동감 넘치는 애교/집착 섞인 반말 대사를 뱉어라!\n";
            }

            string finalPrompt = $"{systemPersona}\n\n{contextInfo}{memorySection}\n{categoryDirective}\n{toneDirective}{manualDirective}\n\n" +
                "⚠️ [대사 작성 및 출력 규칙 (최고 중요도)]\n" +
                "1. 네가 배팅한 포지션 방향(Long/Short)과 현재 시장 국면(상승/하락)을 완벽히 이해하고 대사를 뱉어라! Short 쳐놓고 '고점을 뚫어라', '올라가라'라고 말하는 바보 같은 실수를 절대 하지 마라.\n" +
                "2. 대사 속에 '체력 40%'나 '멘탈 Anxious', 'ROE 10%' 같이 시스템 수치나 상태명을 직접 언급하지 마라! 신체적 감각과 감정 상태(기분)로만 자연스럽게 표현해.\n" +
                "3. ⭐ [중복 회피 규칙]: 위의 [최근 내뱉은 대사들] 목록에 있는 단어나 문장 구조와 겹치지 않게 완전히 다르고 창의적인 표현으로 뱉어라!\n" +
                "4. ⭐ [과거 기억 회상 규칙]: 만약 [과거 주요 기억 및 각인된 트라우마]와 현재 상황이 깊게 연관된다면(예: 과거에 청산당했던 경험 등), 그 기억을 떠올리며 감정을 쏟아내라.\n" +
                "5. 🚨 [절대 규칙 - 프롬프트 복사 금지]: 위의 인게임 상태, 이벤트 설명, 감정 지시 텍스트나 괄호, 머리말('대사:', 'Monologue:')을 절대 너의 출력물에 포함하거나 따라 적지 마라!\n" +
                "6. 🚨 [행동 묘사 및 지문 금지]: '호가창을 뚫어져라 주시 중...', '(초조해하며)' 같은 관찰자 시점의 행동 묘사나 부연 설명, 지문을 절대 넣지 마라! 오직 캐릭터 입에서 소리 내어 뱉는 감정 실린 진짜 대사만을 작성해!\n" +
                (string.IsNullOrEmpty(manualDirective)
                    ? "오직 마스터를 부르며 집착하거나 차트에 광기를 드러내는 1~2문장의 생동감 넘치는 멘헤라 소녀의 반말 혼잣말 독백 대사만을 따옴표 없이 순수 텍스트로 출력해."
                    : "오직 플레이어(마스터) 옆에서 애교, 집착, 불안, 환호를 쏟아내는 1~2문장 생동감 넘치는 멘헤라 소녀의 반말 리액션 대사만을 따옴표 없이 순수 텍스트로 출력해.");
            return finalPrompt;
        }

        private string GetEmotionalToneDirective(float roe, TraderStatus.MentalState mentalState)
        {
            if (mentalState == TraderStatus.MentalState.Overdose || roe >= 50f)
            {
                return "[감정 지시] 극도의 쾌락과 자만심에 취해 마스터를 독점하려 하거나, 세력을 조롱하며 미친 듯이 환호하는 광기 어린 멘헤라 어투로 말해.";
            }
            if (mentalState == TraderStatus.MentalState.Danger || roe <= -50f)
            {
                return "[감정 지시] 마스터가 자신을 쓸모없다며 버릴까 봐 극심한 패닉과 공포에 질려 눈물을 터뜨리거나 애원하는 절망적 멘헤라 어투로 말해.";
            }
            if (mentalState == TraderStatus.MentalState.Anxious || roe < 0f)
            {
                return "[감정 지시] 손톱을 물어뜯듯 불안해하고 호가창의 캔들 하나하나에 집착하며 마스터에게 매달리고 초조해하는 멘헤라 어투로 말해.";
            }
            return "[감정 지시] 마스터를 부르며 애교를 부리거나 차트 타점을 자신만만하게 자랑하는 매혹적이고 약간은 병적인 집착이 담긴 멘헤라 어투로 말해.";
        }

        private string GetCategoryDirective(EventCategory category, string extraContext = "")
        {
            if (category == EventCategory.GimmickTriggered && !string.IsNullOrEmpty(extraContext) && 
               (extraContext.Contains("FOMO") || extraContext.Contains("놓친") || extraContext.Contains("휩소")))
            {
                return "[이벤트 분류: 휩소 오인 및 진입 기회 상실(FOMO) 후회 기믹 발동] 트레이더는 직전에 가짜 신호(휩소)라고 의심하여 포지션 진입을 포기하고 관망했다. 하지만 실제로는 주가가 크게 움직여 엄청난 수익을 낼 수 있었던 진짜 타점이었다! 현재 보유한 포지션이 전혀 없는 상태에서, 진입하지 않고 좋은 기회를 날려버린 것에 대한 극심한 후회와 자책, 분노를 표현하는 혼잣말을 작성해라. (⚠️ 주의: 절대 '포지션을 팔았다'거나 '청산했다'고 말하지 마라! 아예 들어가지 못하고 구경만 하다가 놓친 상황이다!)";
            }

            return category switch
            {
                EventCategory.GameStartup => "[이벤트 분류: 게임 가동 시작] 오늘 시작할 트레이딩에 대해 홀로 각오를 다지거나 24시간 차트판의 잔혹함을 읊조리는 첫 시작 혼잣말을 작성해라.",
                EventCategory.MentalChange => "[이벤트 분류: 멘탈 수치 변화] 멘탈 상태가 변동되거나 스트레스가 고조되어 심리적 압박감이 드러나는 혼잣말을 작성해라.",
                EventCategory.HealthChange => "[이벤트 분류: 체력 피로도 변화] 과로, 수면 부족, 피로 누적으로 인해 몸이 한계에 달하고 예민해진 상태를 표현하는 혼잣말을 작성해라.",
                EventCategory.ItemUsed => "[이벤트 분류: 아이템 복용] 방금 투여/섭취한 약물이나 음료(에너지 드링크/진정제 등)의 효과에 즉각 반응하여 각성하거나 안도하는 혼잣말을 작성해라.",
                EventCategory.PositionOpened => "[이벤트 분류: 신규 포지션 진입] 새로 들어간 포지션(Long/Short)과 레버리지 배율에 대한 기대감이나 타점 확신을 드러내는 혼잣말을 작성해라.",
                EventCategory.PositionClosed => "[이벤트 분류: 포지션 종료/익절/손절] 포지션 청산 결과에 대해 극도의 환호나 처절한 분노/절망을 쏟아내는 혼잣말을 작성해라.",
                EventCategory.ChartMovement => "[이벤트 분류: 실시간 차트 변동 중계] 포지션 유지 중 실시간으로 주가가 내 타점대로 오르거나 반대로 역행하는 상황에 대해 중계하며 일희일비하는 혼잣말을 작성해라.",
                EventCategory.GimmickTriggered => "[이벤트 분류: 멘탈 소모 6대 기믹 발동] 멘탈 침식, 연속 손절, 수면 부족, 고배율 중독 금단현상, 횡보장 지루함, 드로다운 트라우마 등 기믹에 고통받거나 조르는 멘헤라 독백을 작성해라.",
                EventCategory.SkillUpgraded => $"[이벤트 분류: 트레이딩 스킬 및 능력치 업그레이드 완료] 방금 오랜 시간 집중해서 훈련 및 공부를 마치고 스킬 레벨업을 달성했다! ({extraContext}) 마스터에게 자신의 성장한 실력과 똑똑해진 뇌를 자랑하며 칭찬을 갈구하거나, 피로 속에서도 자신감을 불태우는 생동감 넘치는 1~2문장의 반말 리액션 혼잣말을 작성해라.",
                _ => "[이벤트 분류: 차트 관망 및 일반 분석] 현재 차트 흐름과 자신의 심리를 고백하는 혼잣말을 작성해라."
            };
        }
    }
}
