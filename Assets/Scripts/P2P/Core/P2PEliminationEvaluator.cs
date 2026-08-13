namespace FXOverdose.P2P.Core
{
    /// <summary>P2P 전용 즉시 탈락 조건을 호스트 기준으로 한 번만 적용합니다.</summary>
    public static class P2PEliminationEvaluator
    {
        public static P2PEliminationReason Evaluate(P2PPlayerRuntimeState player)
        {
            if (player.Mental <= 0d) return P2PEliminationReason.MentalDepleted;
            if (player.TotalEquity <= 0d) return P2PEliminationReason.Bankruptcy;
            return P2PEliminationReason.None;
        }

        public static bool EvaluateAndApply(P2PPlayerRuntimeState player, long serverTick)
        {
            return player.TryEliminate(Evaluate(player), serverTick);
        }
    }
}
