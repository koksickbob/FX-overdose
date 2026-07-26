using System;
using System.Threading.Tasks;
using UnityEngine;
using LLMUnity;

namespace FXOverdose.AI.LLM
{
    [Serializable]
    public class GeneratedChoiceEventData
    {
        public string ScenarioTitle;
        public string ScenarioDescription;
        public string OptionATitle;
        public string OptionADesc;
        public string OptionBTitle;
        public string OptionBDesc;
        public string OptionCTitle;
        public string OptionCDesc;
    }

    [RequireComponent(typeof(LLMUnity.LLM))]
    [RequireComponent(typeof(LLMUnity.LLMAgent))]
    public class LLMSafeGenerator : MonoBehaviour
    {
        public static LLMSafeGenerator Instance { get; private set; }
        
        private LLMUnity.LLMAgent llmAgent;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                llmAgent = GetComponent<LLMUnity.LLMAgent>();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public async Task<GeneratedChoiceEventData> GenerateChoiceEventAsync(string themeTag, string marketContext)
        {
            Debug.Log($"[LLMSafeGenerator] 돌발 이벤트 생성 시작... Theme: {themeTag}");
            
            string prompt = $@"당신은 가상화폐 트레이딩 게임의 돌발 이벤트 생성기입니다.
현재 시장 상황({marketContext})과 테마('{themeTag}')에 맞춰 흥미로운 돌발 이벤트를 창작해주세요.

반드시 아래 JSON 양식의 값(value) 부분에 제시된 [안내 텍스트]를 지우고, 당신이 직접 창작한 내용으로 채워서 응답해야 합니다. 제시된 예시 텍스트를 그대로 출력하지 마세요!

{{
  ""ScenarioTitle"": ""[이곳에 창작한 이벤트 제목 작성]"",
  ""ScenarioDescription"": ""[이곳에 창작한 이벤트 상황 묘사 작성]"",
  ""OptionATitle"": ""[이곳에 선택지 A 제목 작성]"",
  ""OptionADesc"": ""[이곳에 선택지 A를 골랐을 때의 결과 설명 작성]"",
  ""OptionBTitle"": ""[이곳에 선택지 B 제목 작성]"",
  ""OptionBDesc"": ""[이곳에 선택지 B를 골랐을 때의 결과 설명 작성]"",
  ""OptionCTitle"": ""[이곳에 선택지 C 제목 작성]"",
  ""OptionCDesc"": ""[이곳에 선택지 C를 골랐을 때의 결과 설명 작성]""
}}
오직 JSON 코드만 출력하세요. 다른 설명은 절대 추가하지 마세요.";
            
            if (llmAgent == null) {
                Debug.LogError("[LLMSafeGenerator] LLMAgent 컴포넌트가 없습니다!");
                return GetDummyData(themeTag, marketContext);
            }

            string jsonText = await llmAgent.Chat(prompt, null, null, false);
            Debug.Log($"[LLMSafeGenerator] 원본 LLM 응답:\n{jsonText}");
            
            try 
            {
                int start = jsonText.IndexOf('{');
                int end = jsonText.LastIndexOf('}');
                if (start >= 0 && end > start) {
                    string cleanJson = jsonText.Substring(start, end - start + 1);
                    var data = JsonUtility.FromJson<GeneratedChoiceEventData>(cleanJson);
                    if (data != null && !string.IsNullOrEmpty(data.ScenarioTitle))
                    {
                        return data;
                    }
                }
            } 
            catch (Exception e) 
            {
                Debug.LogError("[LLMSafeGenerator] JSON 파싱 실패: " + e.Message);
            }
            
            Debug.LogWarning("[LLMSafeGenerator] 파싱 실패로 더미 데이터 반환");
            return GetDummyData(themeTag, marketContext);
        }

        public async Task<string> GenerateDailySettlementAsync(float todayProfit, int liquidations)
        {
            Debug.Log($"[LLMSafeGenerator] 일일 정산 일기 생성 시작... Profit: {todayProfit}%, Liquidations: {liquidations}");
            
            string prompt = $"당신은 가상화폐 트레이더입니다. 오늘은 {todayProfit}%의 수익을 냈고, 청산은 {liquidations}번 당했습니다. 이 상황에 대한 짧은 트레이딩 일기를 1문장으로 써주세요.";
            
            if (llmAgent == null) {
                return $"[더미 일기] 오늘은 {todayProfit}%의 수익을 냈고, 청산은 {liquidations}번 당했다. 내일은 더 잘해야지!";
            }

            string result = await llmAgent.Chat(prompt, null, null, false);
            Debug.Log("[LLMSafeGenerator] 일기 생성 완료!");
            return result.Trim();
        }
        
        private GeneratedChoiceEventData GetDummyData(string themeTag, string marketContext)
        {
            return new GeneratedChoiceEventData
            {
                ScenarioTitle = $"[더미] 템플릿 '{themeTag}' 발동!",
                ScenarioDescription = $"현재 시장 상황({marketContext})에 맞추어 생성된 더미 뉴스입니다.",
                OptionATitle = "안전(A) 선택지",
                OptionADesc = "관망하거나 손절하는 텍스트가 생성됩니다.",
                OptionBTitle = "공격(B) 선택지",
                OptionBDesc = "풀매수하거나 버티는 텍스트가 생성됩니다.",
                OptionCTitle = "특수(C) 선택지",
                OptionCDesc = "아이템 사용이나 특수 행동 텍스트가 생성됩니다."
            };
        }
    }
}
