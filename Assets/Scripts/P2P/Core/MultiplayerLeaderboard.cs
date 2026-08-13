using System.Collections.Generic;
using System.Linq;

namespace FXOverdose.P2P.Core
{
    /// <summary>생존자와 탈락자를 동일한 결정 규칙으로 정렬합니다.</summary>
    public static class MultiplayerLeaderboard
    {
        public static IReadOnlyList<P2PPlayerRuntimeState> Rank(IEnumerable<P2PPlayerRuntimeState> players)
        {
            return players
                // 전 플레이어를 실시간 PnL(동일 시작금 기준 TotalEquity) 순으로 표시합니다.
                .OrderByDescending(player => player.TotalEquity)
                .ThenBy(player => player.IsEliminated)
                .ThenByDescending(player => player.EliminationTick)
                .ThenBy(player => player.PlayerId)
                .ToArray();
        }
    }
}
