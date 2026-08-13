namespace FXOverdose.P2P.Core
{
    public enum P2PPositionSide : byte
    {
        None,
        Long,
        Short
    }

    /// <summary>플레이어 한 명이 보유할 수 있는 단일 포지션 상태입니다.</summary>
    public sealed class P2PPositionState
    {
        public P2PPositionSide Side { get; private set; }
        public double EntryPrice { get; private set; }
        public double MarginAmount { get; private set; }
        public int Leverage { get; private set; }
        public double LiquidationPrice { get; private set; }
        public double UnrealizedPnL { get; private set; }
        public bool IsOpen => Side != P2PPositionSide.None;

        public void Open(P2PPositionSide side, double entryPrice, double marginAmount, int leverage, double liquidationPrice)
        {
            Side = side;
            EntryPrice = entryPrice;
            MarginAmount = marginAmount;
            Leverage = leverage;
            LiquidationPrice = liquidationPrice;
            UnrealizedPnL = 0d;
        }

        public void MarkToMarket(double marketPrice)
        {
            UnrealizedPnL = IsOpen
                ? P2PTradeCalculator.CalculateUnrealizedPnL(this, marketPrice)
                : 0d;
        }

        public void Clear()
        {
            Side = P2PPositionSide.None;
            EntryPrice = 0d;
            MarginAmount = 0d;
            Leverage = 0;
            LiquidationPrice = 0d;
            UnrealizedPnL = 0d;
        }
    }
}
