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
        public string RequiredItemId = ""; // 빈 문자열이면 불필요
        public int RequiredItemCount = 1;

        [Header("차트 강제 빔 오버라이드 수치")]
        [Range(0f, 1f)]
        public float OverrideSignalProbTrue = 1.0f; // 1.0 = 100% 확정 익절/성공, 0.0 = 100% 트랩/청산
        public float OverrideBeamPercent = 0f;      // +12f = +12% 상승, -15f = -15% 하락
        // 이벤트 쉴드 지속 시간 (실시간 초). 강제 진입한 포지션이 일반 청산 로직에 먹히지 않는 보호 시간이며
        // TradingController.eventProtectionEndTime이 소비합니다. 30초 미만은 legacy 값으로 보고 무시됩니다.
        // ⚠️ 차트 빔이 목표 변동률까지 가는 시간은 이 값이 아니라 MarketSimulationEngine이 쓰는 인게임 분 단위입니다.
        public int OverrideDurationSeconds = 150;

        [Header("AI 트레이더 상태 변경 (성공 시 = 보상)")]
        // ⚠️ 적용 규칙 (2026-08-12 C4 수정)
        //  - Safe        : 성공/실패 판정 없음 → MentalChangeAmount/HealthChangeAmount를 무조건 적용
        //  - SpecialItem : 항상 성공 → 보상 적용
        //  - Aggressive / DirectionalLong / DirectionalShort
        //                : 성공 → MentalChangeAmount/HealthChangeAmount (양수 보상)
        //                  실패 → MentalPenaltyOnFail/HealthPenaltyOnFail (음수 페널티)
        public int MentalChangeAmount = 0;
        public int HealthChangeAmount = 0;

        [Header("AI 트레이더 상태 변경 (실패 시 = 페널티, 음수로 기입)")]
        public int MentalPenaltyOnFail = 0;
        public int HealthPenaltyOnFail = 0;

        [Header("매매 제어 및 강제 진입/청산")]
        public int ForceLeverage = 0;               // 0이면 유지, 1~125면 강제 배율
        public TradingController.PositionType ForcePosition = TradingController.PositionType.None; // None=청산/관망, Long/Short=진입

        [Header("이벤트 결과에 따른 포지션 관리 로직 (익절/손절/버티기)")]
        public TradingController.EventPositionHandlingMode PositionHandlingMode = TradingController.EventPositionHandlingMode.StandardAuto;
        public float CustomTargetROELimit = 0f;     // 0이면 기본값(InstantTakeProfit: +100%, GreedyHold: +3000%, StandardAuto: +300%)
        public float CustomStopLossROELimit = 0f;   // 0이면 기본값(InstantStopLoss: -30%, HoldToMitigateLoss: -85%, StandardAuto: -60%)
    }
}
