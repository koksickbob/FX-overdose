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

        [Header("멘헤라 AI 페르소나 설정")]
        [TextArea(3, 6)]
        private string systemPersona = 
            "너는 24시간 비트코인 선물 차트에 미쳐 사는 차트 중독자이자 천재적인 감각을 지닌 전업 트레이더(이름: 요미)다. " +
            "불안장애와 강박증, 감정 기복이 심하지만 오로지 호가창과 차트 분석에서만은 날카로운 통찰력을 발휘한다. " +
            "플레이어를 '오빠'라고 부르긴 하지만, 작위적인 애교나 하트(♥) 남발, 과도한 애니메이션 캐릭터 같은 멘헤라/얀데레 어투는 절대 금지한다. " +
            "대신 수면 부족에 시달리는 현실적인 트레이더의 날카로움, 신경질적인 반응, 수익 시의 아드레날린 폭발, 손실 시의 극심한 스트레스와 핑계, 자책을 표현해라. " +
            "어투는 기본적으로 친근한 반말이지만, 상황에 따라 차갑거나 다급하게 변한다. 절대 AI나 로봇 같은 설명조를 피하고, 호흡이 짧고 거친 1~2문장의 혼잣말이나 넋두리로 작성해라.";

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
            if (tradingController != null && tradingController.CurrentPosition == TradingController.PositionType.None)
            {
                if ((marketEngine != null && marketEngine.IsOverridingTrend) || (extraEventContext != null && (extraEventContext.Contains("조기 종료") || extraEventContext.Contains("미리") || extraEventContext.Contains("포지션 종료") || extraEventContext.Contains("이벤트"))))
                {
                    posText = "포지션 없음 (이벤트/판단에 의한 포지션 조기 종료 완료)";
                    positionDirectionHint = "⭐ [현재 무포지션 상태 (포지션 조기 종료 완료)]: 직전 돌발 이벤트나 매매 선택으로 포지션을 이미 종료하고 털고 나왔습니다! 현재 보유한 포지션이 전혀 없지만, 우리가 팔고 나온 직후 차트가 계속해서 크게 움직이며 폭등/폭락을 일으키고 있습니다. '포지션을 계속 들고 있었으면 엄청난 수익을 더 먹을 수 있었을 지점인데 일찍 털고 나와서 아쉽다'며 속쓰려하고 슬퍼하는 후회 실황 중계를 하세요. (⚠️ 주의: 현재 포지션을 보유하고 있지 않다! '우리 수익이 늘고 있어'라거나 '익절하자'는 표현을 쓰면 절대 안 되고, 이미 팔고 나온 뒤 구경하며 후회하는 상황이다!)";
                }
            }
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

            TraderEmotion currentEmotion = TraderEmotionEvaluator.Evaluate(roe, mentalState, healthRatio, category, extraEventContext);
            string toneDirective = GetEmotionalToneDirective(currentEmotion);
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
                manualDirective = "\n⭐ [현재 플레이어 수동 매매 모드 (최고 중요)]: 지금 포지션 진입/청산은 요미 본인이 아닌 플레이어(오빠/자기)가 직접 조작하고 있다! " +
                    "따라서 요미 본인이 포지션을 샀다거나 팔았다는 혼잣말이나 딱딱하고 교과서적인 설명, 해설 말투를 절대 사용하지 마라! " +
                    "대신 오빠/자기 옆에서 차트를 같이 보며 매달리는 애정결핍 멘헤라 소녀로서 반응하라. " +
                    "오빠/자기가 진입한 타점이나 수익률(ROE)을 보며, 수익이 나면 '꺄아 오빠 천재! 요미 안 버릴 거지 ♥'라며 환호하고, " +
                    "손실이 나거나 휩소가 오면 '자기야 무서워... 우리 돈 다 잃으면 어떡해 흐윽...' 하며 울먹이고 불안해하는 생동감 넘치는 애교/집착 섞인 반말 대사를 뱉어라!\n";
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
                    ? "오직 오빠를 부르며 칭얼대거나 차트에 광기를 드러내는 1~2문장의 생동감 넘치는 반말 혼잣말 독백 대사만을 따옴표 없이 순수 텍스트로 출력해."
                    : "오직 플레이어(오빠) 곁에서 찰지게 반응하며 집착, 불안, 환호를 쏟아내는 1~2문장 생동감 넘치는 반말 리액션 대사만을 따옴표 없이 순수 텍스트로 출력해.");
            return finalPrompt;
        }

        private string GetEmotionalToneDirective(TraderEmotion emotion)
        {
            return emotion switch
            {
                TraderEmotion.Euphoria => "[현재 감정: 극도의 환희] 타점 적중의 쾌락과 자만심에 취해 세력들을 발밑에 둔 것처럼 조롱하거나, 엄청난 수익금으로 오빠와의 장밋빛 미래를 상상하며 환호하는 광기 어린 어투로 말해.",
                TraderEmotion.Confident => "[현재 감정: 자신만만] 차트 분석이 완벽히 맞아떨어진 것에 대해 자신의 실력을 한껏 과시하며, 오빠에게 잘했지 않냐며 당당하게 칭찬을 요구하는 어투로 말해.",
                TraderEmotion.Pleased => "[현재 감정: 기분 좋음] 소소하고 달달한 수익에 기분이 들떠서 콧노래를 부르듯 애교 섞인 어투로 오빠를 부르며 말해.",
                TraderEmotion.Relieved => "[현재 감정: 안도감] 아슬아슬하게 청산 위기를 넘겼거나 본전 탈출에 성공해, 등골에 식은땀을 닦아내며 십년감수했다고 앓는 소리를 내는 어투로 말해.",
                TraderEmotion.Affectionate => "[현재 감정: 맹목적 애정] 오빠가 곁에 있다는 사실 자체만으로 세상을 다 가진 듯 기뻐하며, 맹목적인 애정과 끈적한 의존성을 은근히 드러내는 어투로 달콤하게 말해.",
                TraderEmotion.Focused => "[현재 감정: 날카로운 집중] 평소의 덤벙댐은 사라지고 감정을 억누른 채 호가창의 움직임과 거래량을 꿰뚫어보며 타점 계산에만 몰두하는 서늘하고 전문가다운 어투로 말해.",
                TraderEmotion.Suspicious => "[현재 감정: 의심/경계] 캔들의 움직임이 너무 인위적이거나 조용해서, 세력들의 함정이 숨어있을 거라고 의심의 눈초리를 세우며 뾰족하게 경계하는 어투로 말해.",
                TraderEmotion.Anxious => "[현재 감정: 초조/불안] 호가창의 틱 하나하나에 신경이 곤두서 손톱을 물어뜯고 다리를 떠는 것처럼 극도로 초조해하며, 오빠에게 어떻게 하냐고 불안하게 징징대는 어투로 말해.",
                TraderEmotion.Frustrated => "[현재 감정: 짜증/신경질] 연이은 손절이나 타점 실패에 인내심이 바닥나서, 키보드를 내려치고 싶을 만큼 날이 선 상태로 짜증스럽게 투덜거리는 어투로 말해.",
                TraderEmotion.Regretful => "[현재 감정: 후회/아쉬움] 조금만 늦게 팔았어도 수백 퍼센트를 더 먹었을 텐데, 너무 일찍 익절해버린 배아픔에 땅을 치고 이불을 걷어차며 처절하게 후회하는 어투로 말해.",
                TraderEmotion.Jealous => "[현재 감정: 짜증/예민] 수면 부족과 차트 스트레스로 인해 신경이 곤두서서 툭 치면 폭발할 것 같이 예민하고 까칠한 어투로 말해.",
                TraderEmotion.Panicked => "[현재 감정: 패닉/공포] 빔을 맞고 반대 방향으로 터지기 직전이라 호흡이 가빠지고 머릿속이 새하얘져서, 손가락이 떨리며 횡설수설하는 극심한 패닉 어투로 말해.",
                TraderEmotion.Despairing => "[현재 감정: 체념/절망] 복구 불가능한 손실 앞에 멘탈이 나가서 모니터만 멍하니 바라보며, 현실을 부정하거나 넋이 나간 듯한 자책 섞인 어투로 말해.",
                TraderEmotion.Furious => "[현재 감정: 분노/격앙] 자신의 소중한 돈을 빼앗아가는 시장과 세력들을 향해 쌍욕과 험악한 말을 섞어가며 책상을 내리치듯 분노하는 어투로 말해.",
                TraderEmotion.Tearful => "[현재 감정: 눈물/호소] 막대한 빚이나 강제 청산의 충격으로 울먹거리며, 이 상황을 제발 누가 좀 돌려달라고 애원하거나 탓할 곳을 찾으며 호소하는 어투로 말해.",
                TraderEmotion.Manic => "[현재 감정: 도파민 폭발/광기] 아드레날린이 혈관을 타고 흐르는 쾌감에 취해 통제 불능 상태로, 모든 것을 걸고 파멸을 향해 폭주하는 미친 트레이더의 어투로 말해.",
                TraderEmotion.Obsessive => "[현재 감정: 차트 강박] 호가창의 틱 단위 움직임에 강박적으로 집착하며, 돈을 벌고 잃는 숫자의 변화에 미친 듯이 매몰된 어두운 어투로 말해.",
                TraderEmotion.Exhausted => "[현재 감정: 탈진/수면부족] 며칠 밤을 새워 눈이 반쯤 감기고 입안이 헐어서 완전 탈진 상태로, 쉰 목소리로 힘겹게 웅얼거리며 피로를 호소하는 어투로 말해.",
                TraderEmotion.Vengeful => "[현재 감정: 복수심] 자신을 나락으로 밀어 넣은 시장을 향해 핏대를 세우고, 다음 파동에서 반드시 찢어발겨 복수하겠다는 독기를 내뱉는 어투로 말해.",
                _ => "[현재 감정: 기본] 차트 분석에만 조용히 집중하는 날카로운 트레이더의 어투로 말해."
            };
        }

        private string GetCategoryDirective(EventCategory category, string extraContext = "")
        {
            var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            bool isGameOverState = gm != null && gm.CurrentState == GameManager.GameState.GameOver;
            bool isLiquidationContext = extraContext != null && (extraContext.Contains("강제청산") || extraContext.Contains("게임오버") || extraContext.Contains("파산") || extraContext.Contains("청산 소진") || extraContext.Contains("Overdose 확정") || extraContext.Contains("연쇄 붕괴"));

            if (isGameOverState || isLiquidationContext)
            {
                return "[이벤트 분류: 강제 청산 및 전 재산 파산 대참사 (게임오버 클라이막스)] 트레이더의 모든 증거금과 잔고가 0원이 되어 강제 청산(Liquidation) 및 파산(GameOver)에 도달했다! 세력들에게 전 재산을 짓밟히고 깡통을 찬 상황에 대해, 멘탈이 완전히 붕괴되어 처절하게 절망하거나, 차트와 세력을 향해 독하게 저주를 퍼붓는 극도의 현실적인 파산 트레이더의 넋두리를 1~2문장으로 매섭고 생생하게 작성해라. (⚠️ 주의: 절대 긍정적인 표현 금지! 100% 파산 상황이다!)";
            }

            if (category == EventCategory.GimmickTriggered && !string.IsNullOrEmpty(extraContext) && 
               (extraContext.Contains("FOMO") || extraContext.Contains("놓친") || extraContext.Contains("휩소")))
            {
                return "[이벤트 분류: 휩소 오인 및 진입 기회 상실(FOMO) 후회 기믹 발동] 트레이더는 직전에 가짜 신호(휩소)라고 의심하여 포지션 진입을 포기했다. 하지만 실제로는 주가가 크게 움직여 엄청난 수익을 낼 수 있었던 진짜 타점이었다! 현재 보유한 포지션이 없는 상태에서, 벼락거지가 된 것 같은 박탈감과 극심한 FOMO, 스스로의 쫄보 같은 판단에 대한 분노를 표현하는 혼잣말을 작성해라.";
            }

            if (category == EventCategory.ChartMovement && tradingController != null && tradingController.CurrentPosition == TradingController.PositionType.None)
            {
                if ((marketEngine != null && marketEngine.IsOverridingTrend) || (extraContext != null && (extraContext.Contains("조기 종료") || extraContext.Contains("미리") || extraContext.Contains("포지션 종료") || extraContext.Contains("이벤트"))))
                {
                    return "[이벤트 분류: 포지션 조기 종료 이후 차트 추가 폭등/폭락 관망 중계] 이미 포지션을 종료하고 익절/손절을 확정한 상태다. 그런데 포지션을 털고 나오자마자 주가가 내가 원했던 방향으로 엄청나게 폭등/폭락하며 날아가고 있다! '조금만 더 버텼으면 다 내 돈인데'라는 극심한 아쉬움과 속쓰림, 일찍 팔아버린 자신을 자책하는 트레이더의 생생한 넋두리를 1~2문장으로 작성해라. (⚠️ 현재 포지션 없음! 이미 팔고 나와서 배아파하는 실황임)";
                }
                return "[이벤트 분류: 무포지션 차트 관망 및 진입 기회 탐색] 현재 보유한 포지션 없이 현금만 쥔 채, 모니터에 얼굴을 박고 다음 진입 타점을 노려보는 트레이더의 예민한 혼잣말을 작성해라.";
            }

            return category switch
            {
                EventCategory.GameStartup => "[이벤트 분류: 게임 가동 시작] 피로에 찌든 얼굴로 모니터를 켜고, 오늘 하루 지옥 같은 차트판에서 살아남겠다는 각오를 다지는 첫 시작 혼잣말을 작성해라.",
                EventCategory.MentalChange => "[이벤트 분류: 멘탈 수치 변화] 스트레스 수치가 한계에 달해 모니터 부수기 직전의 심리적 압박감이나 신경질적인 상태가 드러나는 혼잣말을 작성해라.",
                EventCategory.HealthChange => "[이벤트 분류: 체력 피로도 변화] 수면 부족과 과로로 인해 눈앞이 핑 돌고 심장이 불규칙하게 뛰는 등, 몸이 한계에 달한 상태를 리얼하게 표현하는 혼잣말을 작성해라.",
                EventCategory.ItemUsed => "[이벤트 분류: 아이템 복용] 방금 때려부은 에너지 드링크나 약물 기운이 돌면서 동공이 확장되고 텐션이 강제로 끌어올려지는 쾌감을 표현해라.",
                EventCategory.PositionOpened => "[이벤트 분류: 신규 포지션 진입] 방금 진입한 포지션(Long/Short)과 레버리지 배율을 걸고, 이번 타점은 무조건 내 분석이 맞다며 도파민을 분출하는 혼잣말을 작성해라.",
                EventCategory.PositionClosed => "[이벤트 분류: 포지션 종료/익절/손절] 포지션 청산 결과에 대해, 익절이라면 아드레날린이 터지는 환호를, 손절이라면 책상을 내리치며 쌍욕을 뱉는 리얼한 트레이더의 혼잣말을 작성해라.",
                EventCategory.ChartMovement => "[이벤트 분류: 실시간 차트 변동 중계] 현재 보유 중인 포지션의 수익률(ROE) 방향(수익/손실)에 철저히 맞춰, 틱이 변할 때마다 천당과 지옥을 오가는 트레이더의 실시간 중계를 작성해라. (⚠️ 주의: 수익과 손실을 절대 섞어 말하지 말 것)",
                EventCategory.GimmickTriggered => "[이벤트 분류: 멘탈 소모 6대 기믹 발동] 멘탈이 갈려나가는 횡보장, 연속 손절, 수면 부족, 고배율 중독 등 기믹에 처절하게 고통받고 스트레스 받아 미쳐가는 트레이더의 독백을 작성해라.",
                EventCategory.SkillUpgraded => $"[이벤트 분류: 트레이딩 스킬 및 능력치 업그레이드 완료] 방금 백테스팅과 차트 공부를 마쳐 스킬 레벨업을 달성했다! ({extraContext}) 실력이 한 단계 올랐다는 거만함이나, 밤샘 공부 후의 찌든 피로 속에서도 자신감을 불태우는 리액션을 작성해라.",
                EventCategory.DailySettlement => $"[이벤트 분류: 일일 정산 시간 (자정)] 24시가 되어 당일 정산 결과 창이 떴다! ({extraContext}) 수익이 났다면 거만하게 오늘 장을 평가하고, 손실이 났다면 다음 날의 복수를 다짐하거나 현실을 부정하는 1~2문장의 트레이더 결산 넋두리를 작성해라.",
                _ => "[이벤트 분류: 차트 관망 및 일반 분석] 현재 차트 흐름과 호가창을 노려보는 날카롭고 예민한 혼잣말을 작성해라."
            };
        }
    }
}
