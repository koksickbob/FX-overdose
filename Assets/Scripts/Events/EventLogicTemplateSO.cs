using System;
using UnityEngine;
using FXOverdose.Trading;

namespace FXOverdose.Events
{
    [Serializable]
    public class EventLogicOptionData
    {
        [Header("선택지 기계적 로직 뼈대")]
        public ChoiceOptionType OptionType;
        
        [Header("아이템 개입 요구 (SpecialItem 선택 시)")]
        public string RequiredItemId = ""; // 빈 문자열이면 불필요
        public int RequiredItemCount = 1;

        [Header("차트 강제 빔 오버라이드 수치")]
        [Range(0f, 1f)]
        public float OverrideSignalProbTrue = 1.0f; // 1.0 = 100% 확정 익절/성공, 0.0 = 100% 트랩/청산
        public float OverrideBeamPercent = 0f;      // +12f = +12% 상승, -15f = -15% 하락
        public int OverrideDurationSeconds = 10;    // 강제 빔 유지 시간 (인게임 초)

        [Header("AI 트레이더 상태 변경")]
        public int MentalChangeAmount = 0;
        public int HealthChangeAmount = 0;

        [Header("매매 제어 및 강제 진입/청산")]
        public int ForceLeverage = 0;               // 0이면 유지, 1~125면 강제 배율
        public TradingController.PositionType ForcePosition = TradingController.PositionType.None; // None=청산/관망, Long/Short=진입

        [Header("이벤트 결과에 따른 포지션 관리 로직 (익절/손절/버티기)")]
        public TradingController.EventPositionHandlingMode PositionHandlingMode = TradingController.EventPositionHandlingMode.StandardAuto;
        public float CustomTargetROELimit = 0f;     
        public float CustomStopLossROELimit = 0f;   
    }

    [CreateAssetMenu(fileName = "EventLogicTemplate_", menuName = "FX OVERDOSE/Events/Event Logic Template")]
    public class EventLogicTemplateSO : ScriptableObject
    {
        [Header("템플릿 기본 정보")]
        public string TemplateID;
        public EventTriggerCondition TriggerCondition = EventTriggerCondition.Any;
        
        [Header("분위기/테마 (LLM에게 전달될 상황 태그)")]
        [Tooltip("예: BullMarket_Pump, BearMarket_Dump, SEC_FUD 등")]
        public string ThemeTag;

        [Header("3분기 선택지 기계적 로직 (A: 안전, B: 공격, C: 특수)")]
        public EventLogicOptionData[] LogicOptions = new EventLogicOptionData[3];
    }
}
