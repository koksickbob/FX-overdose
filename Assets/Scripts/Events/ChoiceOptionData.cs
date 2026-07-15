using System;
using UnityEngine;
using FXOverdose.Trading;

namespace FXOverdose.Events
{
    public enum ChoiceOptionType
    {
        Safe,
        Aggressive,
        SpecialItem,
        DirectionalLong,
        DirectionalShort
    }

    public enum EventTriggerCondition
    {
        Any,
        TimeOfDay,
        LowMental,
        HighLeverage
    }

    [Serializable]
    public class ChoiceOptionData
    {
        [Header("선택지 기본 명세")]
        public ChoiceOptionType OptionType;
        public string OptionTitle;
        [TextArea(2, 4)]
        public string Description;

        [Header("아이템 개입 요구 (SpecialItem 선택 시)")]
        public int RequiredItemIndex = -1; // -1이면 불필요, 0~3은 4대 아이템군
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
        public float CustomTargetROELimit = 0f;     // 0이면 기본값(InstantTakeProfit: +100%, GreedyHold: +3000%, StandardAuto: +300%)
        public float CustomStopLossROELimit = 0f;   // 0이면 기본값(InstantStopLoss: -30%, HoldToMitigateLoss: -85%, StandardAuto: -60%)
    }
}

