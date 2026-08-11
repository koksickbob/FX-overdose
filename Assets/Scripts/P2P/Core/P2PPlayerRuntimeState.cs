using System;
using System.Collections.Generic;

namespace FXOverdose.P2P.Core
{
    public enum P2PEliminationReason : byte
    {
        None,
        Bankruptcy,
        MentalDepleted,
        DisconnectForfeit
    }

    /// <summary>Unity 오브젝트와 무관하게 계산 가능한 플레이어 한 명의 권위 상태입니다.</summary>
    public sealed class P2PPlayerRuntimeState
    {
        private readonly Dictionary<string, int> inventory = new Dictionary<string, int>(StringComparer.Ordinal);

        public P2PPlayerRuntimeState(ulong playerId, string displayName, double startingCash, double health = 100d, double mental = 100d)
        {
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("표시 이름이 필요합니다.", nameof(displayName));
            if (!P2PMatchRules.IsFiniteNonNegative(startingCash)) throw new ArgumentOutOfRangeException(nameof(startingCash));
            if (!P2PMatchRules.IsFiniteNonNegative(health)) throw new ArgumentOutOfRangeException(nameof(health));
            if (!P2PMatchRules.IsFiniteNonNegative(mental)) throw new ArgumentOutOfRangeException(nameof(mental));

            PlayerId = playerId;
            DisplayName = displayName;
            CashBalance = startingCash;
            StartingEquity = startingCash;
            Health = health;
            Mental = mental;
            IsConnected = true;
            Position = new P2PPositionState();
        }

        public ulong PlayerId { get; }
        public string DisplayName { get; }
        public double StartingEquity { get; }
        public double CashBalance { get; private set; }
        public double Health { get; private set; }
        public double Mental { get; private set; }
        public bool IsConnected { get; private set; }
        public bool IsEliminated { get; private set; }
        public P2PEliminationReason EliminationReason { get; private set; }
        public long EliminationTick { get; private set; } = -1;
        public P2PPositionState Position { get; }
        public IReadOnlyDictionary<string, int> Inventory => inventory;

        public double TotalEquity => CashBalance + (Position.IsOpen ? Position.MarginAmount + Position.UnrealizedPnL : 0d);
        public double ReturnRate => StartingEquity > 0d ? (TotalEquity - StartingEquity) / StartingEquity : 0d;

        public void ChangeCash(double amount)
        {
            if (double.IsNaN(amount) || double.IsInfinity(amount)) throw new ArgumentOutOfRangeException(nameof(amount));
            CashBalance += amount;
        }

        public void ChangeHealth(double amount) => Health = ClampNonNegative(Health + amount);
        public void ChangeMental(double amount) => Mental = ClampNonNegative(Mental + amount);
        public void SetConnected(bool connected) => IsConnected = connected;

        public void SetInventoryAmount(string itemId, int amount)
        {
            if (string.IsNullOrWhiteSpace(itemId)) throw new ArgumentException("아이템 ID가 필요합니다.", nameof(itemId));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0) inventory.Remove(itemId);
            else inventory[itemId] = amount;
        }

        public bool TryEliminate(P2PEliminationReason reason, long serverTick)
        {
            if (IsEliminated || reason == P2PEliminationReason.None) return false;
            IsEliminated = true;
            EliminationReason = reason;
            EliminationTick = serverTick;
            return true;
        }

        private static double ClampNonNegative(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
            return Math.Max(0d, value);
        }
    }
}
