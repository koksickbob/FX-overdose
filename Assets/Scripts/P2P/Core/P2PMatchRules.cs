using System;

namespace FXOverdose.P2P.Core
{
    /// <summary>호스트가 확정하고 모든 참가자에게 동일하게 적용하는 경기 규칙입니다.</summary>
    public sealed class P2PMatchRules
    {
        public const int MinimumPlayers = 1;
        public const int MaximumPlayersLimit = 4;
        public const int MaximumLeverageLimit = 125;

        public P2PMatchRules(
            int maximumPlayers = 4,
            int maximumLeverage = 50,
            double maximumMarginRatio = 0.5d,
            double minimumOrderAmount = 1d,
            double startingCash = 7000d,
            double choiceTimeoutSeconds = 15d,
            double maintenanceMarginRate = 0.005d,
            double tradingFeeRate = 0.0006d)
        {
            if (maximumPlayers < MinimumPlayers || maximumPlayers > MaximumPlayersLimit)
                throw new ArgumentOutOfRangeException(nameof(maximumPlayers));
            if (maximumLeverage < 1 || maximumLeverage > MaximumLeverageLimit)
                throw new ArgumentOutOfRangeException(nameof(maximumLeverage));
            if (!IsFinitePositive(maximumMarginRatio) || maximumMarginRatio > 1d)
                throw new ArgumentOutOfRangeException(nameof(maximumMarginRatio));
            if (!IsFinitePositive(minimumOrderAmount))
                throw new ArgumentOutOfRangeException(nameof(minimumOrderAmount));
            if (!IsFinitePositive(startingCash))
                throw new ArgumentOutOfRangeException(nameof(startingCash));
            if (!IsFinitePositive(choiceTimeoutSeconds))
                throw new ArgumentOutOfRangeException(nameof(choiceTimeoutSeconds));
            if (!IsFiniteNonNegative(maintenanceMarginRate) || maintenanceMarginRate >= 1d)
                throw new ArgumentOutOfRangeException(nameof(maintenanceMarginRate));
            if (!IsFiniteNonNegative(tradingFeeRate) || tradingFeeRate >= 1d)
                throw new ArgumentOutOfRangeException(nameof(tradingFeeRate));

            MaximumPlayers = maximumPlayers;
            MaximumLeverage = maximumLeverage;
            MaximumMarginRatio = maximumMarginRatio;
            MinimumOrderAmount = minimumOrderAmount;
            StartingCash = startingCash;
            ChoiceTimeoutSeconds = choiceTimeoutSeconds;
            MaintenanceMarginRate = maintenanceMarginRate;
            TradingFeeRate = tradingFeeRate;
        }

        public int MaximumPlayers { get; }
        public int MaximumLeverage { get; }
        public double MaximumMarginRatio { get; }
        public double MinimumOrderAmount { get; }
        public double StartingCash { get; }
        public double ChoiceTimeoutSeconds { get; }
        public double MaintenanceMarginRate { get; }
        public double TradingFeeRate { get; }

        internal static bool IsFinitePositive(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d;

        internal static bool IsFiniteNonNegative(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
    }
}
