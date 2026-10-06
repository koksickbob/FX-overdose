using System;

namespace FXOverdose.Trading
{
    // 신호 방향 및 종류
    public enum MarketSignalType
    {
        BullishBreakout, // 상승 돌파 (롱 수익 타점 / 숏 청산 유도)
        BearishBreakout, // 하락 돌파 (숏 수익 타점 / 롱 청산 유도)
        BullTrap,        // 롱 꼬시기 휩소 (불트랩 / 속임수 신호)
        BearTrap         // 숏 꼬시기 휩소 (베어트랩 / 속임수 신호)
    }

    // 신호 강도 등급
    public enum SignalStrength
    {
        Strong, // 강한 확정 신호 (+30%~+60% ROE 또는 대형 청산 빔 유도, 15~30분 지속)
        Weak    // 약한 단타/미끼 신호 (+5%~+15% ROE 또는 가벼운 -8% 손절 휩소, 5~10분 지속)
    }

    // 신호 진행 단계
    public enum SignalPhase
    {
        None,               // 일반 평균 회귀 국면
        GraceWindow,        // 판단 여유 시간 (AI 및 플레이어 제안 대기 골든타임, 노이즈 억제)
        GuaranteedOverride, // 확정적 주가 오버라이드 진행 구간 (일방향 드리프트 강제 주입)
        Cooldown            // 신호 종료 후 쿨다운 (중복 발생 방지)
    }

    [Serializable]
    public struct MarketSignal
    {
        public MarketSignalType Type;
        public SignalStrength Strength;
        public bool IsTrueSignal;          // true: 방향 일치(수익 보장) / false: 반대 방향 빔(청산/손절 유도)
        public float TargetPercentageDelta;// 목표 주가 변동률 (%) (예: +4.5% 또는 -1.2%)
        public int DurationMinutes;        // 확정 구간 지속 시간 (분)
        public int GraceMinutes;           // 판단 여유 시간 (분)
        public float SignalStartPrice;     // 신호 발생 시점 주가

        /// <summary>
        /// 이 신호가 진입을 <b>유도하는</b> 방향입니다. 진위와 무관합니다 — 가짜 신호면 실제 가격은 반대로 갑니다.
        /// AI(AITradingBrain)는 신호 종류(Type)가 아니라 이 값으로 방향을 정하므로, 신호 종류를 새로 추가해도
        /// AI 코드를 고칠 필요가 없습니다. (SIG-0)
        /// </summary>
        public TradingController.PositionType Direction;

        /// <summary>기존 4종 신호의 유도 방향. 신호를 만드는 쪽이 Direction을 채울 때 씁니다.</summary>
        public static TradingController.PositionType AdvertisedDirectionOf(MarketSignalType type) => type switch
        {
            MarketSignalType.BullishBreakout => TradingController.PositionType.Long,
            MarketSignalType.BullTrap => TradingController.PositionType.Long,
            MarketSignalType.BearishBreakout => TradingController.PositionType.Short,
            MarketSignalType.BearTrap => TradingController.PositionType.Short,
            _ => TradingController.PositionType.None
        };

        /// <summary>Direction이 비어 있으면(구 경로) 신호 종류에서 유도합니다.</summary>
        public TradingController.PositionType LureDirection =>
            Direction != TradingController.PositionType.None ? Direction : AdvertisedDirectionOf(Type);

        public string GetSignalDescription()
        {
            string strengthText = Strength == SignalStrength.Strong ? "[강한 확정 신호]" : "[약한 단타 신호]";
            string typeText = Type switch
            {
                MarketSignalType.BullishBreakout => "상승 돌파 🚀",
                MarketSignalType.BearishBreakout => "하락 돌파 📉",
                MarketSignalType.BullTrap => "롱 꼬시기 휩소 (Bull Trap) ⚠️",
                MarketSignalType.BearTrap => "숏 꼬시기 휩소 (Bear Trap) ⚠️",
                _ => "알 수 없음"
            };
            return $"{strengthText} {typeText} (여유: {GraceMinutes}분, 보장: {DurationMinutes}분, 예상 변동: {TargetPercentageDelta:+0.00;-0.00}%)";
        }
    }
}
