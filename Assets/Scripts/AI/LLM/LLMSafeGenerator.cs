using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using LLMUnity;
using FXOverdose.Events;
using FXOverdose.Trading;
using System.Text.RegularExpressions;

namespace FXOverdose.AI.LLM
{
    [Serializable]
    public class GeneratedChoiceEventData
    {
        public string ScenarioTitle;
        public string ScenarioDescription;
        public string AIMonologue;
    }

    [RequireComponent(typeof(LLMUnity.LLM))]
    [RequireComponent(typeof(LLMUnity.LLMAgent))]
    public class LLMSafeGenerator : MonoBehaviour
    {
        public static LLMSafeGenerator Instance { get; private set; }

        /// <summary>
        /// LLMAgent 동시 사용 방지. <c>llmAgent.grammar</c>가 에이전트 전역 설정이라
        /// 두 생성이 겹치면 서로의 문법 제약을 지웁니다. 에이전트는 TitleScene의 LLM_Manager에
        /// 하나만 존재하므로(DontDestroyOnLoad) 인스턴스가 아니라 정적으로 둡니다.
        /// </summary>
        private static readonly SemaphoreSlim AgentGate = new SemaphoreSlim(1, 1);

        [Header("모델 파라미터 (코드에서 강제 적용 — 씬 직렬화 값보다 우선)")]
        [Tooltip("끄면 TitleScene의 LLMAgent 인스펙터 값을 그대로 사용합니다. 진단용.")]
        [SerializeField] private bool overrideModelParameters = true;

        [Tooltip("생성 길이 상한. -1(무제한)이면 모델이 프롬프트를 통째로 복창해도 멈추지 않습니다.")]
        [SerializeField] private int maxPredictTokens = 320;

        [Tooltip("0.2처럼 낮으면 프롬프트 예시 복사가 가장 확률 높은 선택이 되어 복창을 조장합니다.")]
        [SerializeField, Range(0f, 2f)] private float generationTemperature = 0.65f;

        [Header("GBNF 문법 강제")]
        [Tooltip("3필드 JSON 외의 출력을 모델 레벨에서 물리적으로 차단합니다. " +
                 "생성이 아예 실패하거나 비면 이 옵션을 꺼서 원인을 분리하십시오.")]
        [SerializeField] private bool useGrammarConstraint = true;

        private LLMUnity.LLMAgent llmAgent;

        /// <summary>
        /// 복창 대조용 정적 지시문. 프롬프트 조립 시 채워지며, 테마/시장상황/선택지 같은
        /// 동적 컨텍스트는 포함하지 않습니다. (기사 본문이 정당하게 재사용할 수 있으므로)
        /// </summary>
        private string lastInstructionCorpus;

        /// <summary>모델이 반드시 지켜야 하는 3필드 JSON 구조를 강제하는 GBNF 문법.</summary>
        private const string ChoiceEventGrammar =
            "root ::= \"{\" ws \"\\\"ScenarioTitle\\\"\" ws \":\" ws str ws \",\" ws " +
            "\"\\\"ScenarioDescription\\\"\" ws \":\" ws str ws \",\" ws " +
            "\"\\\"AIMonologue\\\"\" ws \":\" ws str ws \"}\"\n" +
            "str ::= \"\\\"\" char+ \"\\\"\"\n" +
            "char ::= [가-힣] | [ㄱ-ㅎ] | [ㅏ-ㅣ] | [0-9] | [A-Za-z] | \" \" | \".\" | \",\" | \"!\" | \"?\" | \"%\" | \"-\" | \"(\" | \")\" | \"~\" | \":\" | \"'\" | \"/\" | \"+\" | \"…\" | \"·\"\n" +
            "ws ::= [ \\t\\n]*\n";

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                llmAgent = GetComponent<LLMUnity.LLMAgent>();
                ApplyModelParameters();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 씬에 직렬화된 LLMUnity 기본값(영어 시스템 프롬프트 / numPredict -1 / temperature 0.2)은
        /// 한국어 JSON 생성에 불리하므로 코드에서 명시적으로 덮어씁니다.
        /// </summary>
        private void ApplyModelParameters()
        {
            if (!overrideModelParameters || llmAgent == null) return;

            llmAgent.systemPrompt =
                "당신은 한국어 텍스트만 출력하는 게임 시나리오 생성기입니다. " +
                "요청받은 JSON 객체 하나만 출력하며, 설명·머리말·코드블록 표시를 절대 덧붙이지 않습니다. " +
                "한자, 일본어, 중국어, 영어 문장을 사용하지 않습니다.";

            llmAgent.numPredict = maxPredictTokens;
            llmAgent.temperature = generationTemperature;

            Debug.Log($"[LLMSafeGenerator] 모델 파라미터 적용: numPredict={maxPredictTokens}, temperature={generationTemperature}, grammar={(useGrammarConstraint ? "ON" : "OFF")}");
        }

        public async Task<GeneratedChoiceEventData> GenerateChoiceEventAsync(
            EventLogicTemplateSO template,
            string marketContext,
            CancellationToken cancellationToken = default)
        {
            string themeDescription = template != null ? template.GetThemeDescription() : "알 수 없는 시장 상황";
            Debug.Log($"[LLMSafeGenerator] 돌발 이벤트 생성 시작... Theme: {themeDescription}");

            string instruction = BuildInstructionBlock();
            string context = BuildContextBlock(template, themeDescription, marketContext);
            lastInstructionCorpus = instruction;

            string prompt = instruction + "\n" + context;

            if (llmAgent == null)
            {
                // R8: LLM이 없는 것은 예외 상황이 아니라 정상 경로입니다(에디터에서 GameScene 직접 재생 등).
                Debug.Log("[LLMSafeGenerator] LLMAgent가 없어 사전 작성 텍스트 경로로 넘깁니다.");
                return null;
            }

            string jsonText;
            bool grammarApplied = false;

            // 에이전트 접근을 직렬화합니다. grammar는 LLMAgent 전역 설정이라 생성이 겹치면
            // 먼저 끝난 쪽의 finally가 아직 생성 중인 쪽의 JSON 제약을 풀어 버려, 뒤쪽은
            // 자유 텍스트를 뱉고 파싱에서 기각됩니다(→ 폴백). LLMUnity의 Chat에는 취소 인자가 없어
            // 진행 중 호출을 끊을 수 없으므로 애초에 겹치지 않게 막습니다.
            await AgentGate.WaitAsync();
            try
            {
                // 대기하는 동안 취소됐다면(일자 전환·다음 트리거 예약) 아예 시작하지 않습니다.
                if (cancellationToken.IsCancellationRequested)
                {
                    Debug.Log("[LLMSafeGenerator] 대기 중 취소되어 생성을 시작하지 않습니다.");
                    return null;
                }

                try
                {
                    if (useGrammarConstraint)
                    {
                        llmAgent.grammar = ChoiceEventGrammar;
                        grammarApplied = true;
                    }

                    jsonText = await llmAgent.Chat(prompt, null, null, false);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LLMSafeGenerator] 생성 호출 실패: {e.Message}");
                    LLMGenerationStats.RecordRejected($"생성 호출 예외: {e.Message}");
                    return null;
                }
                finally
                {
                    // 문법은 에이전트 전역 설정입니다. 일기 생성 등 다른 용도가 JSON에 묶이지 않도록 즉시 해제합니다.
                    if (grammarApplied)
                    {
                        try { llmAgent.grammar = ""; }
                        catch (Exception e) { Debug.LogWarning($"[LLMSafeGenerator] 문법 해제 실패: {e.Message}"); }
                    }
                }
            }
            finally
            {
                AgentGate.Release();
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Debug.Log("[LLMSafeGenerator] 생성이 취소되었습니다. 결과를 폐기합니다.");
                return null;
            }

            Debug.Log($"[LLMSafeGenerator] 원본 LLM 응답:\n{jsonText}");

            if (string.IsNullOrEmpty(jsonText))
            {
                LLMGenerationStats.RecordRejected("응답이 비어 있음");
                return null;
            }

            if (!TryExtractFirstJsonObject(jsonText, out string cleanJson))
            {
                LLMGenerationStats.RecordRejected("균형 잡힌 JSON 객체를 찾지 못함");
                Debug.LogWarning("[LLMSafeGenerator] 응답에서 완결된 JSON 객체를 찾지 못했습니다.");
                return null;
            }

            GeneratedChoiceEventData data;
            try
            {
                // JsonUtility는 키 대소문자를 엄격하게 구분하므로 정규화합니다.
                cleanJson = Regex.Replace(cleanJson, "\"scenarioTitle\"", "\"ScenarioTitle\"", RegexOptions.IgnoreCase);
                cleanJson = Regex.Replace(cleanJson, "\"scenarioDescription\"", "\"ScenarioDescription\"", RegexOptions.IgnoreCase);
                cleanJson = Regex.Replace(cleanJson, "\"aiMonologue\"", "\"AIMonologue\"", RegexOptions.IgnoreCase);

                data = JsonUtility.FromJson<GeneratedChoiceEventData>(cleanJson);
            }
            catch (Exception e)
            {
                LLMGenerationStats.RecordRejected($"JSON 파싱 예외: {e.Message}");
                Debug.LogWarning($"[LLMSafeGenerator] JSON 파싱 실패: {e.Message}\n{cleanJson}");
                return null;
            }

            // ⭐ 출력 위생 검사 — 프롬프트 복창/안내문 잔존/길이/한국어 비율을 한 번에 판정합니다.
            var verdict = LLMOutputSanitizer.Inspect(data, lastInstructionCorpus);
            if (!verdict.Accepted)
            {
                LLMGenerationStats.RecordRejected(verdict.Reason);
                Debug.LogWarning($"[LLMSafeGenerator] ❌ 위생 검사 기각: {verdict.Reason}\n원본: {cleanJson}");
                return null;
            }

            data.ScenarioTitle = data.ScenarioTitle.Trim();
            data.ScenarioDescription = data.ScenarioDescription.Trim();
            data.AIMonologue = data.AIMonologue.Trim();

            LLMGenerationStats.RecordAccepted();
            Debug.Log($"[LLMSafeGenerator] ✅ 위생 검사 통과: {data.ScenarioTitle}");
            return data;
        }

        /// <summary>
        /// 정적 지시문 블록. 이 문자열이 그대로 복창 대조 코퍼스로 쓰이므로,
        /// 동적으로 바뀌는 테마/시장상황/선택지는 절대 여기에 넣지 마십시오.
        /// </summary>
        private static string BuildInstructionBlock()
        {
            // ⚠️ 빈 JSON 스켈레톤(값이 "[...작성]"인 형태)을 주면 모델이 그것을 복창합니다. (C2)
            //    반드시 완전히 채워진 예시 하나만 제시합니다.
            return
@"당신은 가상화폐 트레이딩 게임의 속보 기사 작성기입니다.
주어진 상황을 다룬 속보 기사와, 플레이어의 파트너 '요미'가 그 기사를 보고 내뱉는 한마디를 만듭니다.
요미는 살아 있는 인간 소녀입니다. AI도 프로그램도 아닙니다.
요미는 플레이어를 '오빠'라고 부르며, 항상 반말로 감정적이고 호들갑스럽게 다급히 말합니다.
자기 자신은 '요미'(3인칭) 또는 '나'로 부릅니다.

지켜야 할 규칙:
- 오직 한글로만 씁니다. 한자, 일본어, 중국어, 영어 문장을 쓰지 마십시오. (BTC 같은 종목 약어는 허용)
- 제목은 40자 이내의 기사 헤드라인입니다.
- 본문은 200자 이내로 무슨 일이 벌어졌는지 설명합니다.
- 요미의 한마디는 60자 이내의 구어체 대사입니다.
- 요미의 한마디에 존댓말을 쓰지 마십시오. '마스터', '주인님' 호칭도 쓰지 마십시오.
- 요미가 자신을 기계로 지칭하는 표현(재부팅, 학습 데이터, 전원)을 쓰지 마십시오.
- 아래 예시의 문장을 베끼지 말고 상황에 맞게 새로 창작하십시오.
- 객체 하나만 출력하고 그 앞뒤에 어떤 말도 덧붙이지 마십시오.

다음은 형식을 보여주는 완성된 예시입니다.
{""ScenarioTitle"":""거래소 지갑서 4만 BTC 이상 징후 포착"",""ScenarioDescription"":""새벽 대형 거래소의 콜드월렛에서 정체불명의 주소로 대규모 물량이 옮겨졌다. 시장은 매도 압력을 우려하며 호가창이 급격히 얇아지는 중이다. 관계자는 단순 지갑 정비라고 해명했지만 투자 심리는 이미 얼어붙었다."",""AIMonologue"":""오빠...! 이 물량 지금 던지면 우리 다 죽어! 어떻게 할 거야?!""}";
        }

        /// <summary>
        /// 동적 컨텍스트 블록. 템플릿의 실제 선택지 정보를 여기서 주입합니다. (R2 해소)
        /// </summary>
        private static string BuildContextBlock(EventLogicTemplateSO template, string themeDescription, string marketContext)
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("[상황]");
            sb.AppendLine(themeDescription);
            sb.AppendLine();
            sb.AppendLine("[시장 지표]");
            sb.AppendLine(string.IsNullOrWhiteSpace(marketContext) ? "특이사항 없음" : marketContext);

            // ⭐ R2: 계산만 하고 버려지던 선택지 힌트를 실제로 프롬프트에 주입합니다.
            //    이것이 없으면 LLM은 선택지가 무엇인지 모른 채 기사를 써서, 기사와 선택지가 따로 놉니다.
            if (template != null && template.HasValidOptions)
            {
                sb.AppendLine();
                sb.AppendLine("[플레이어가 고를 수 있는 대응]");
                for (int i = 0; i < 3; i++)
                {
                    sb.AppendLine($"{i + 1}. {GetOptionHint(template.LogicOptions[i])}");
                }
                sb.AppendLine("기사 본문은 위 세 가지 대응이 자연스러운 선택지로 보이도록 상황을 서술해야 합니다.");
            }

            sb.AppendLine();
            sb.AppendLine("이제 위 상황에 맞는 객체 하나를 출력하십시오.");
            return sb.ToString();
        }

        /// <summary>
        /// 균형 잡힌 첫 번째 JSON 객체를 추출합니다.
        /// 기존의 "첫 '{' ~ 마지막 '}'" 통짜 슬라이스는 모델이 객체를 두 개 뱉으면
        /// 둘을 걸쳐 자르거나 앞쪽 쓰레기 객체만 잡는 문제가 있었습니다.
        /// </summary>
        public static bool TryExtractFirstJsonObject(string text, out string json)
        {
            json = null;
            if (string.IsNullOrEmpty(text)) return false;

            for (int start = 0; start < text.Length; start++)
            {
                if (text[start] != '{') continue;

                int depth = 0;
                bool inString = false;
                bool escaped = false;

                for (int i = start; i < text.Length; i++)
                {
                    char c = text[i];

                    if (escaped) { escaped = false; continue; }
                    if (c == '\\') { escaped = true; continue; }

                    if (c == '"') { inString = !inString; continue; }
                    if (inString) continue;

                    if (c == '{') depth++;
                    else if (c == '}')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            string candidate = text.Substring(start, i - start + 1);
                            // 3필드를 모두 갖춘 객체만 채택. 앞쪽에 껍데기 객체가 끼어 있어도 건너뜁니다.
                            if (candidate.IndexOf("ScenarioTitle", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                candidate.IndexOf("ScenarioDescription", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                candidate.IndexOf("AIMonologue", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                json = candidate;
                                return true;
                            }
                            start = i; // 이 객체는 버리고 다음 '{'부터 다시 탐색
                            break;
                        }
                    }
                }
            }

            return false;
        }

        private static string GetOptionHint(EventLogicOptionData option)
        {
            if (option == null) return "(없음)";

            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(option.OptionTitle))
            {
                sb.Append(option.OptionTitle.Trim());
                sb.Append(" — ");
            }

            switch (option.OptionType)
            {
                case ChoiceOptionType.Safe: sb.Append("안전한 선택(위험 회피 또는 손절/관망). "); break;
                case ChoiceOptionType.Aggressive: sb.Append("공격적인 선택(고위험 고수익). "); break;
                case ChoiceOptionType.SpecialItem: sb.Append("특수 아이템을 써서 위기를 넘김. "); break;
                case ChoiceOptionType.DirectionalLong: sb.Append("상승(롱)에 베팅. "); break;
                case ChoiceOptionType.DirectionalShort: sb.Append("하락(숏)에 베팅. "); break;
            }

            if (option.ForcePosition != TradingController.PositionType.None)
                sb.Append($"결과적으로 {(option.ForcePosition == TradingController.PositionType.Long ? "상승" : "하락")} 방향에 베팅한다. ");
            else if (option.OptionType == ChoiceOptionType.Safe)
                sb.Append("포지션을 종료하고 관망한다. ");

            return sb.ToString().Trim();
        }
    }
}
