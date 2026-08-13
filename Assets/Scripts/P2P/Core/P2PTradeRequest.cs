namespace FXOverdose.P2P.Core
{
    public enum P2PMatchPhase : byte
    {
        Lobby,
        Playing,
        Finished
    }

    public enum P2PTradeAction : byte
    {
        OpenLong,
        OpenShort,
        ClosePosition
    }

    public enum P2PTradeRejectReason : byte
    {
        None,
        MatchNotPlaying,
        PlayerDisconnected,
        PlayerEliminated,
        ChoiceEventActive,
        DuplicateRequest,
        PositionConflict,
        InvalidLeverage,
        InvalidMargin,
        InsufficientBalance,
        InvalidMarketPrice
    }

    /// <summary>클라이언트가 호스트에 보내는 변경 불가능한 주문 요청 값입니다.</summary>
    public readonly struct P2PTradeRequest
    {
        public P2PTradeRequest(uint requestId, P2PTradeAction action, int leverage = 1, double marginRatio = 0d)
        {
            RequestId = requestId;
            Action = action;
            Leverage = leverage;
            MarginRatio = marginRatio;
        }

        public uint RequestId { get; }
        public P2PTradeAction Action { get; }
        public int Leverage { get; }
        public double MarginRatio { get; }
    }

    /// <summary>호스트가 주문 처리 후 호출자에게 돌려주는 결과입니다.</summary>
    public readonly struct P2PTradeResult
    {
        public P2PTradeResult(uint requestId, P2PTradeRejectReason rejectReason, double fillPrice)
        {
            RequestId = requestId;
            RejectReason = rejectReason;
            FillPrice = fillPrice;
        }

        public uint RequestId { get; }
        public P2PTradeRejectReason RejectReason { get; }
        public double FillPrice { get; }
        public bool IsAccepted => RejectReason == P2PTradeRejectReason.None;
    }
}
