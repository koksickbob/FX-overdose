using UnityEngine;

namespace FXOverdose.Events
{
    [CreateAssetMenu(fileName = "ChoiceEvent_", menuName = "FX OVERDOSE/Events/Choice Event")]
    public class ChoiceEventSO : ScriptableObject
    {
        [Header("이벤트 식별자 및 뉴스 헤드라인")]
        public string EventID;
        public string ScenarioTitle;
        [TextArea(3, 6)]
        public string ScenarioDescription;

        [Header("AI 트레이더 멘헤라 독백")]
        [TextArea(2, 5)]
        public string AIMonologue;

        [Header("등장 조건")]
        public EventTriggerCondition TriggerCondition = EventTriggerCondition.Any;

        [Header("3분기 선택지 명세 (A: 안전, B: 공격, C: 특수/직접선택)")]
        public ChoiceOptionData[] Options = new ChoiceOptionData[3];
    }
}
