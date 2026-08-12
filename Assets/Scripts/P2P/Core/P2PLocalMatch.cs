using System;
using System.Collections.Generic;

namespace FXOverdose.P2P.Core
{
    /// <summary>
    /// 네트워크 없이 호스트 권위 흐름을 재현하는 로컬 경기입니다.
    /// 이후 NGO 계층은 이 객체에 요청을 전달하고 결과만 복제합니다.
    /// </summary>
    public sealed class P2PLocalMatch
    {
        private readonly Dictionary<ulong, P2PPlayerRuntimeState> players = new Dictionary<ulong, P2PPlayerRuntimeState>();
        private readonly HashSet<PlayerRequestKey> processedRequests = new HashSet<PlayerRequestKey>();

        public P2PLocalMatch(P2PMatchRules rules)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        public P2PMatchRules Rules { get; }
        public P2PMatchPhase Phase { get; private set; } = P2PMatchPhase.Lobby;
        public bool IsChoiceEventActive { get; set; }
        public double MarketPrice { get; private set; }
        public IReadOnlyCollection<P2PPlayerRuntimeState> Players => players.Values;

        public bool TryGetPlayer(ulong playerId, out P2PPlayerRuntimeState player) => players.TryGetValue(playerId, out player);

        public P2PPlayerRuntimeState AddPlayer(ulong playerId, string displayName)
        {
            if (Phase != P2PMatchPhase.Lobby) throw new InvalidOperationException("로비 단계에서만 참가할 수 있습니다.");
            if (players.Count >= Rules.MaximumPlayers) throw new InvalidOperationException("로비 정원이 가득 찼습니다.");
            if (players.ContainsKey(playerId)) throw new InvalidOperationException("이미 참가한 플레이어입니다.");

            var player = new P2PPlayerRuntimeState(playerId, displayName, Rules.StartingCash);
            players.Add(playerId, player);
            return player;
        }

        public void Start(double initialMarketPrice)
        {
            if (Phase != P2PMatchPhase.Lobby) throw new InvalidOperationException("이미 시작했거나 종료된 경기입니다.");
            if (players.Count < P2PMatchRules.MinimumPlayers) throw new InvalidOperationException("참가자가 없습니다.");
            if (!P2PMatchRules.IsFinitePositive(initialMarketPrice)) throw new ArgumentOutOfRangeException(nameof(initialMarketPrice));

            MarketPrice = initialMarketPrice;
            Phase = P2PMatchPhase.Playing;
        }

        public P2PTradeResult SubmitTrade(ulong playerId, P2PTradeRequest request)
        {
            if (!players.TryGetValue(playerId, out P2PPlayerRuntimeState player))
                return new P2PTradeResult(request.RequestId, P2PTradeRejectReason.PlayerDisconnected, MarketPrice);

            var key = new PlayerRequestKey(playerId, request.RequestId);
            bool duplicate = processedRequests.Contains(key);
            P2PTradeRejectReason rejection = P2PTradeValidator.Validate(
                request, player, Rules, Phase, IsChoiceEventActive, duplicate, MarketPrice);

            if (rejection != P2PTradeRejectReason.None)
                return new P2PTradeResult(request.RequestId, rejection, MarketPrice);

            // 승인된 요청만 기록합니다. 일시적인 경기 상태 오류로 거절된 요청은 재시도할 수 있습니다.
            processedRequests.Add(key);

            if (request.Action == P2PTradeAction.ClosePosition)
                P2PTradeCalculator.ClosePosition(player, Rules, MarketPrice);
            else
                P2PTradeCalculator.OpenPosition(player, request, Rules, MarketPrice);

            P2PEliminationEvaluator.EvaluateAndApply(player, 0);
            return new P2PTradeResult(request.RequestId, P2PTradeRejectReason.None, MarketPrice);
        }

        public void UpdateMarketPrice(double marketPrice, long serverTick)
        {
            if (Phase != P2PMatchPhase.Playing) throw new InvalidOperationException("진행 중인 경기만 가격을 갱신할 수 있습니다.");
            if (!P2PMatchRules.IsFinitePositive(marketPrice)) throw new ArgumentOutOfRangeException(nameof(marketPrice));

            MarketPrice = marketPrice;
            foreach (P2PPlayerRuntimeState player in players.Values)
            {
                if (player.IsEliminated || !player.Position.IsOpen) continue;

                player.Position.MarkToMarket(marketPrice);
                if (P2PTradeCalculator.HasReachedLiquidationPrice(player.Position, marketPrice))
                    P2PTradeCalculator.ClosePosition(player, Rules, marketPrice);

                P2PEliminationEvaluator.EvaluateAndApply(player, serverTick);
            }
        }

        public IReadOnlyList<P2PPlayerRuntimeState> GetLeaderboard() => MultiplayerLeaderboard.Rank(players.Values);

        public void Finish()
        {
            if (Phase != P2PMatchPhase.Playing) return;
            foreach (P2PPlayerRuntimeState player in players.Values)
                if (player.Position.IsOpen) P2PTradeCalculator.ClosePosition(player, Rules, MarketPrice);
            Phase = P2PMatchPhase.Finished;
        }

        private readonly struct PlayerRequestKey : IEquatable<PlayerRequestKey>
        {
            public PlayerRequestKey(ulong playerId, uint requestId)
            {
                PlayerId = playerId;
                RequestId = requestId;
            }

            private ulong PlayerId { get; }
            private uint RequestId { get; }
            public bool Equals(PlayerRequestKey other) => PlayerId == other.PlayerId && RequestId == other.RequestId;
            public override bool Equals(object obj) => obj is PlayerRequestKey other && Equals(other);
            public override int GetHashCode() => unchecked((PlayerId.GetHashCode() * 397) ^ (int)RequestId);
        }
    }
}
