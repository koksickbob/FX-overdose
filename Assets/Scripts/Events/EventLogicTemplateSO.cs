using System;
using UnityEngine;
using FXOverdose.Trading; // for PositionType, etc.

namespace FXOverdose.Events
{
    [Serializable]
    public class EventLogicOptionData
    {
        public ChoiceOptionType OptionType;
        public string OptionTitle;
        public string OptionDescription;

        public string RequiredItemId;
        public int RequiredItemCount;
        
        [Range(0f, 1f)] public float OverrideSignalProbTrue = 0f;
        public float OverrideBeamPercent;
        public int OverrideDurationSeconds = 150;
        
        // 성공 시 보상 (Safe/SpecialItem은 무조건 적용). 상세 규칙은 ChoiceOptionData 주석 참고.
        public int MentalChangeAmount;
        public int HealthChangeAmount;

        // 실패 시 페널티 (Aggressive/Directional 전용, 음수로 기입)
        public int MentalPenaltyOnFail;
        public int HealthPenaltyOnFail;

        public int ForceLeverage;
        public TradingController.PositionType ForcePosition;
        public TradingController.EventPositionHandlingMode PositionHandlingMode;
        
        public float CustomTargetROELimit;
        public float CustomStopLossROELimit;
    }

    [CreateAssetMenu(fileName = "NewEventLogicTemplate", menuName = "FX Overdose/AI/Event Logic Template")]
    public class EventLogicTemplateSO : ScriptableObject
    {
        public string TemplateID;

        /// <summary>
        /// 이 템플릿이 묘사하는 상황. 실데이터는 태그가 아니라 "[ID] 한 문장 상황 서술" 형식입니다.
        /// (R7) 프롬프트에는 대괄호 ID 접두사를 제거한 서술문만 주입해야 합니다. <see cref="GetThemeDescription"/> 사용.
        /// </summary>
        [TextArea(2, 4)] public string ThemeTag;

        [Header("사전 작성 텍스트 (LLM 실패 시 사용 / Phase 4 하이브리드)")]
        public string FallbackTitle;
        [TextArea(2, 5)] public string FallbackDescription;
        [Tooltip("요미 대사 후보. 여러 개면 표시할 때 랜덤으로 하나 고릅니다.")]
        [TextArea(1, 3)] public string[] FallbackMonologues = new string[0];

        public EventLogicOptionData[] LogicOptions = new EventLogicOptionData[3];

        /// <summary>
        /// ThemeTag에서 "[TemplateID] " 접두사를 제거한 순수 상황 서술을 반환합니다. (R7)
        /// </summary>
        public string GetThemeDescription()
        {
            string raw = ThemeTag;
            if (string.IsNullOrWhiteSpace(raw)) return "알 수 없는 시장 상황";

            raw = raw.Trim();
            if (raw.StartsWith("["))
            {
                int close = raw.IndexOf(']');
                if (close >= 0 && close < raw.Length - 1)
                {
                    raw = raw.Substring(close + 1).Trim();
                }
            }
            // 자산에 저장된 여러 줄 서술을 한 줄로 정규화
            raw = raw.Replace("\r", " ").Replace("\n", " ");
            while (raw.Contains("  ")) raw = raw.Replace("  ", " ");
            return raw.Trim();
        }

        /// <summary>
        /// 사전 작성 텍스트가 3요소 모두 채워져 있는지. Phase 4 폴백 가능 여부 판정용.
        /// </summary>
        public bool HasFallbackText =>
            !string.IsNullOrWhiteSpace(FallbackTitle) &&
            !string.IsNullOrWhiteSpace(FallbackDescription) &&
            FallbackMonologues != null && FallbackMonologues.Length > 0;

        /// <summary>
        /// LogicOptions가 3개 모두 유효한지 검사합니다. (R3)
        /// </summary>
        public bool HasValidOptions
        {
            get
            {
                if (LogicOptions == null || LogicOptions.Length < 3) return false;
                for (int i = 0; i < 3; i++)
                {
                    if (LogicOptions[i] == null) return false;
                }
                return true;
            }
        }
    }
}
