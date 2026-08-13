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
        private readonly Dictionary<ulong,uint> lastAcceptedRequestIds = new Dictionary<ulong,uint>();

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

            bool duplicate = lastAcceptedRequestIds.TryGetValue(playerId,out uint previousRequestId) &&
                !IsSequenceNewer(request.RequestId,previousRequestId);
            P2PTradeRejectReason rejection = P2PTradeValidator.Validate(
                request, player, Rules, Phase, IsChoiceEventActive, duplicate, MarketPrice);

            if (rejection != P2PTradeRejectReason.None)
                return new P2PTradeResult(request.RequestId, rejection, MarketPrice);

            // 승인된 요청만 기록합니다. 일시적인 경기 상태 오류로 거절된 요청은 재시도할 수 있습니다.
            lastAcceptedRequestIds[playerId]=request.RequestId;

            if (request.Action == P2PTradeAction.ClosePosition)
                ClosePositionAndApplyVitals(player);
            else
            {
                P2PTradeCalculator.OpenPosition(player, request, Rules, MarketPrice);
                // 원본 MentalDrainGimmickController의 포지션 진입 고정 멘탈 -10.
                player.RecordPositionOpened();
            }

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
                    ClosePositionAndApplyVitals(player);

                P2PEliminationEvaluator.EvaluateAndApply(player, serverTick);
            }
        }

        public IReadOnlyList<P2PPlayerRuntimeState> GetLeaderboard() => MultiplayerLeaderboard.Rank(players.Values);

        public bool ForfeitDisconnectedPlayer(ulong playerId, long serverTick)
        {
            if (!players.TryGetValue(playerId, out P2PPlayerRuntimeState player) || player.IsEliminated) return false;
            if (player.Position.IsOpen) ClosePositionAndApplyVitals(player);
            player.SetConnected(false);
            return player.TryEliminate(P2PEliminationReason.DisconnectForfeit, serverTick);
        }

        public bool SetPlayerConnected(ulong playerId,bool connected)
        {
            if(!players.TryGetValue(playerId,out P2PPlayerRuntimeState player)||player.IsEliminated)return false;
            player.SetConnected(connected);
            return true;
        }

        // uint 최대값에서 0으로 넘어가는 경우도 앞으로 진행한 요청으로 판정합니다.
        private static bool IsSequenceNewer(uint candidate,uint previous)=>unchecked((int)(candidate-previous))>0;

        private void ClosePositionAndApplyVitals(P2PPlayerRuntimeState player)
        {
            if (player == null || !player.Position.IsOpen) return;
            player.Position.MarkToMarket(MarketPrice);
            double exitFee=P2PTradeCalculator.CalculateFee(player.Position.MarginAmount,player.Position.Leverage,Rules.TradingFeeRate);
            double realizedPnl=player.Position.UnrealizedPnL-exitFee;
            P2PTradeCalculator.ClosePosition(player,Rules,MarketPrice);
            // 싱글플레이 정산 규칙과 동일하게 손실 5%를 멘탈 피해로, 수익 2%와 체력 5를 회복으로 반영합니다.
            if(realizedPnl<0)player.ChangeMental(realizedPnl*.05d);
            else if(realizedPnl>0){player.ChangeMental(realizedPnl*.02d);player.ChangeHealth(5d);}
            // 원본 연속 손절 5/12/25 및 수동매매 1.5배 페널티를 별도로 누적합니다.
            player.RecordTradeResult(realizedPnl);
        }

        public void Finish()
        {
            if (Phase != P2PMatchPhase.Playing) return;
            foreach (P2PPlayerRuntimeState player in players.Values)
                if (player.Position.IsOpen) ClosePositionAndApplyVitals(player);
            Phase = P2PMatchPhase.Finished;
        }

    }
}
