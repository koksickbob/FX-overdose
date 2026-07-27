using System;
using System.Threading.Tasks;
using UnityEngine;
using LLMUnity;
using FXOverdose.Events;
using FXOverdose.Trading;

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

        public async Task<GeneratedChoiceEventData> GenerateChoiceEventAsync(EventLogicTemplateSO template, string marketContext)
        {
            string themeTag = template != null ? template.ThemeTag : "Unknown";
            Debug.Log($"[LLMSafeGenerator] 돌발 이벤트 생성 시작... Theme: {themeTag}");
            
            string optionAHint = template != null ? GetOptionHint(template.LogicOptions[0]) : "안전한 선택지";
            string optionBHint = template != null ? GetOptionHint(template.LogicOptions[1]) : "공격적인 선택지";
            string optionCHint = template != null ? GetOptionHint(template.LogicOptions[2]) : "특수 선택지";

            string prompt = $@"당신은 가상화폐 트레이딩 게임의 돌발 이벤트 생성기입니다.
현재 시장 상황({marketContext})과 테마('{themeTag}')에 맞춰 흥미로운 돌발 이벤트를 창작해주세요.
아래의 3가지 선택지는 각각 정해진 게임 시스템적 효과를 가지고 있습니다. 이 효과가 유저에게 직관적으로 느껴지도록 선택지 제목과 설명을 작성하세요.

[선택지 시스템 효과]
- 선택지 A: {optionAHint}
- 선택지 B: {optionBHint}
- 선택지 C: {optionCHint}

반드시 아래 JSON 양식의 값(value) 부분에 제시된 [안내 텍스트]를 지우고, 당신이 직접 창작한 내용으로 채워서 응답해야 합니다. 
[IMPORTANT] 무조건 한국어(Korean)로만 응답하세요! 영어나 한자, 중국어를 사용하면 절대 안 됩니다. 오직 한글만 사용하세요.

{{
  ""ScenarioTitle"": ""[한국어로 이벤트 제목 작성]"",
  ""ScenarioDescription"": ""[한국어로 이벤트 상황 묘사 작성]"",
  ""OptionATitle"": ""[한국어로 선택지 A 제목 작성]"",
  ""OptionADesc"": ""[한국어로 선택지 A 결과 설명 작성]"",
  ""OptionBTitle"": ""[한국어로 선택지 B 제목 작성]"",
  ""OptionBDesc"": ""[한국어로 선택지 B 결과 설명 작성]"",
  ""OptionCTitle"": ""[한국어로 선택지 C 제목 작성]"",
  ""OptionCDesc"": ""[한국어로 선택지 C 결과 설명 작성]""
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
                        if (IsValidKoreanText(data))
                        {
                            return data;
                        }
                        else
                        {
                            Debug.LogWarning("[LLMSafeGenerator] LLM이 한국어가 아닌 언어(중국어/영어 등)를 생성했습니다. 폰트 깨짐 방지를 위해 더미 데이터로 대체합니다.");
                        }
                    }
                }
            } 
            catch (Exception e) 
            {
                Debug.LogError("[LLMSafeGenerator] JSON 파싱 실패: " + e.Message);
            }
            
            Debug.LogWarning("[LLMSafeGenerator] 파싱 실패 또는 한글 미검출로 더미 데이터 반환");
            return GetDummyData(themeTag, marketContext);
        }

        private bool IsValidKoreanText(GeneratedChoiceEventData data)
        {
            if (data == null) return false;
            
            string combinedText = data.ScenarioTitle + data.ScenarioDescription + data.OptionATitle + data.OptionBTitle;
            if (string.IsNullOrEmpty(combinedText)) return false;

            int koreanCount = 0;
            foreach (char c in combinedText)
            {
                // 한글 음절 범위 검사
                if (c >= 0xAC00 && c <= 0xD7A3)
                {
                    koreanCount++;
                }
            }
            
            // 한글 음절이 최소 5자 이상 포함되어야 정상적인 한국어 생성으로 간주
            return koreanCount >= 5;
        }

        public async Task<string> GenerateDailySettlementAsync(float todayProfit, int liquidations)
        {
            Debug.Log($"[LLMSafeGenerator] 일일 정산 일기 생성 시작... Profit: {todayProfit}%, Liquidations: {liquidations}");
            
            string prompt = $"당신은 가상화폐 트레이더입니다. 오늘은 {todayProfit}%의 수익을 냈고, 청산은 {liquidations}번 당했습니다. 이 상황에 대한 짧은 트레이딩 일기를 무조건 한국어(Korean)로 1문장 써주세요. 영어나 중국어는 절대 사용하지 마세요.";
            
            if (llmAgent == null) {
                return $"[더미 일기] 오늘은 {todayProfit}%의 수익을 냈고, 청산은 {liquidations}번 당했다. 내일은 더 잘해야지!";
            }

            string result = await llmAgent.Chat(prompt, null, null, false);
            Debug.Log("[LLMSafeGenerator] 일기 생성 완료!");
            
            int koreanCount = 0;
            foreach (char c in result) { if (c >= 0xAC00 && c <= 0xD7A3) koreanCount++; }
            if (koreanCount < 3) 
            {
                Debug.LogWarning("[LLMSafeGenerator] 일기 생성 결과가 한국어가 아니어서 더미로 대체합니다.");
                return $"오늘은 {todayProfit}%의 수익을 내고, {liquidations}번 청산당했다. 알 수 없는 하루였다.";
            }

            return result.Trim();
        }
        
        private GeneratedChoiceEventData GetDummyData(string themeTag, string marketContext)
        {
            return new GeneratedChoiceEventData
            {
                ScenarioTitle = $"[긴급] 시장 변동성 경고: {themeTag}",
                ScenarioDescription = $"현재 시장 상황({marketContext})에 급격한 변동이 감지되었습니다. 신중한 선택이 필요합니다.",
                OptionATitle = "안전(A) 선택지",
                OptionADesc = "포지션을 정리하고 잠시 시장을 관망하며 위험을 회피합니다.",
                OptionBTitle = "공격(B) 선택지",
                OptionBDesc = "변동성을 기회로 삼아 과감하게 시장의 방향에 베팅합니다.",
                OptionCTitle = "특수(C) 선택지",
                OptionCDesc = "보유하고 있는 특수 아이템이나 수단을 사용하여 상황을 반전시킵니다."
            };
        }

        private string GetOptionHint(EventLogicOptionData option)
        {
            if (option == null) return "";
            string hint = "";
            switch (option.OptionType)
            {
                case ChoiceOptionType.Safe: hint += "안전한 선택(위험 회피 또는 손절/관망). "; break;
                case ChoiceOptionType.Aggressive: hint += "공격적인 선택(고위험 고수익). "; break;
                case ChoiceOptionType.SpecialItem: hint += $"특수 아이템(요구:{option.RequiredItemId}) 사용. "; break;
                case ChoiceOptionType.DirectionalLong: hint += "상승(Long)에 베팅. "; break;
                case ChoiceOptionType.DirectionalShort: hint += "하락(Short)에 베팅. "; break;
            }

            if (option.ForcePosition != TradingController.PositionType.None)
                hint += $"결과적으로 {option.ForcePosition} 포지션 진입. ";
            else if (option.OptionType == ChoiceOptionType.Safe)
                hint += "모든 포지션 청산 및 관망. ";

            if (option.ForceLeverage > 0)
            {
                var choiceCtrl = UnityEngine.Object.FindAnyObjectByType<FXOverdose.Events.ChoiceEventController>();
                int dynamicLeverage = choiceCtrl != null ? choiceCtrl.GetDynamicEventLeverage(option.ForceLeverage) : option.ForceLeverage;
                hint += $"레버리지 {dynamicLeverage}배 강제 적용. ";
            }

            if (Mathf.Abs(option.OverrideBeamPercent) > 0.01f)
            {
                string dir = option.OverrideBeamPercent > 0 ? "상승" : "하락";
                hint += $"시장 가격 약 {Mathf.Abs(option.OverrideBeamPercent)}% {dir} 효과 발생. ";
            }

            if (option.MentalChangeAmount != 0)
            {
                string change = option.MentalChangeAmount > 0 ? "회복" : "감소";
                hint += $"멘탈 {Mathf.Abs(option.MentalChangeAmount)}만큼 {change}. ";
            }
            return hint.Trim();
        }
    }
}
