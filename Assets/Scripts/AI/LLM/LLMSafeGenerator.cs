using System;
using System.Threading.Tasks;
using UnityEngine;

namespace FXOverdose.AI.LLM
{
    /// <summary>
    /// LLM 생성 결과를 담는 래퍼 클래스 (돌발 이벤트용)
    /// </summary>
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

    /// <summary>
    /// LLM 구동 플러그인(LlamaSharp 등)을 래핑하여 비동기 호출 및 Grammar 제약을 담당하는 싱글톤
    /// </summary>
    public class LLMSafeGenerator : MonoBehaviour
    {
        public static LLMSafeGenerator Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 돌발 이벤트(Choice Event)의 텍스트를 LLM을 통해 생성합니다. (비동기)
        /// </summary>
        /// <param name="themeTag">템플릿의 분위기 테마 (예: BullMarket_Pump)</param>
        /// <param name="marketContext">현재 차트 및 시장 상황 요약</param>
        public async Task<GeneratedChoiceEventData> GenerateChoiceEventAsync(string themeTag, string marketContext)
        {
            // TODO: 실제 LLM 플러그인 호출 코드로 교체
            // LLama.Common.Grammar 등을 활용하여 JSON 포맷(GeneratedChoiceEventData) 강제
            
            Debug.Log($"[LLMSafeGenerator] 돌발 이벤트 생성 시작... Theme: {themeTag}");
            
            // 더미 딜레이 (온디바이스 1.5B 모델 추론 시간 시뮬레이션: 3~5초)
            await Task.Delay(3000);
            
            Debug.Log("[LLMSafeGenerator] 돌발 이벤트 생성 완료!");
            
            return new GeneratedChoiceEventData
            {
                ScenarioTitle = $"[더미] 템플릿 '{themeTag}' 발동!",
                ScenarioDescription = $"현재 시장 상황({marketContext})에 맞추어 생성된 더미 뉴스입니다. LLM 플러그인 연결 시 실제 텍스트로 대체됩니다.",
                OptionATitle = "안전(A) 선택지",
                OptionADesc = "관망하거나 손절하는 텍스트가 생성됩니다.",
                OptionBTitle = "공격(B) 선택지",
                OptionBDesc = "풀매수하거나 버티는 텍스트가 생성됩니다.",
                OptionCTitle = "특수(C) 선택지",
                OptionCDesc = "아이템 사용이나 특수 행동 텍스트가 생성됩니다."
            };
        }

        /// <summary>
        /// 일일 정산(Daily Settlement) 일기 텍스트를 LLM을 통해 생성합니다. (비동기)
        /// </summary>
        public async Task<string> GenerateDailySettlementAsync(float todayProfit, int liquidations)
        {
            // TODO: 실제 LLM 플러그인 호출 코드로 교체
            
            Debug.Log($"[LLMSafeGenerator] 일일 정산 일기 생성 시작... Profit: {todayProfit}%, Liquidations: {liquidations}");
            
            await Task.Delay(4000);
            
            Debug.Log("[LLMSafeGenerator] 일일 정산 일기 생성 완료!");
            
            return $"[더미 일기] 오늘은 {todayProfit}%의 수익을 냈고, 청산은 {liquidations}번 당했다. 내일은 더 잘해야지! (LLM 연동 시 실제 텍스트로 대체됨)";
        }
    }
}
