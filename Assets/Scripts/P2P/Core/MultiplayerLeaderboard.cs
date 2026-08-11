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
                .OrderBy(player => player.IsEliminated)
                .ThenByDescending(player => player.IsEliminated ? player.EliminationTick : long.MaxValue)
                .ThenByDescending(player => player.TotalEquity)
                .ThenBy(player => player.PlayerId)
                .ToArray();
        }
    }
}
