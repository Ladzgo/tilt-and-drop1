using System;

namespace TiltDrop.Core
{
    public sealed class Wallet
    {
        public int Coins { get; private set; }
        public event Action<int, int> Changed; // (newBalance, delta)

        public Wallet(int coins = 0) { Coins = Math.Max(0, coins); }

        public void Add(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Coins += amount;
            Changed?.Invoke(Coins, amount);
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (Coins < amount) return false;
            Coins -= amount;
            Changed?.Invoke(Coins, -amount);
            return true;
        }
    }

    public static class Rewards
    {
        public static int LevelWin(GameConfig c, int stars, int leftoverMoves) =>
            c.LevelRewardBase + c.LevelRewardPerStar * stars + Math.Min(c.LevelRewardPerLeftoverMove * Math.Max(0, leftoverMoves), c.LevelRewardLeftoverCap);
    }

    public static class Stars
    {
        /// <summary>GDD §2.7: 3 = par, 2 = par + ceil(slack/2), 1 = any win (always 1 when a continue was used).</summary>
        public static int Compute(int movesUsed, int par, int moveLimit, int continuesUsed)
        {
            if (continuesUsed > 0) return 1;
            int slack = Math.Max(0, moveLimit - par);
            if (movesUsed <= par) return 3;
            if (movesUsed <= par + (slack + 1) / 2) return 2;
            return 1;
        }
    }

    public sealed class PiggyBank
    {
        public int Coins;

        public void OnLevelReward(GameConfig c, int reward) => Coins = Math.Min(c.PiggyCap, Coins + (int)Math.Round(reward * c.PiggyRate, MidpointRounding.AwayFromZero));
        public bool CanBreak(GameConfig c) => Coins >= c.PiggyUnlock;
        public bool IsFull(GameConfig c) => Coins >= c.PiggyCap;

        /// <summary>Call after the €3.99 purchase succeeded. Returns the coins to credit.</summary>
        public int Break()
        {
            int c = Coins;
            Coins = 0;
            return c;
        }
    }

    public sealed class WinStreak
    {
        public int Count;
        public int Tier => Count >= 6 ? 3 : Count >= 4 ? 2 : Count >= 2 ? 1 : 0;

        public void OnWin(bool usedContinue) { if (usedContinue) Count = 0; else Count++; }
        public void OnFail() => Count = 0;

        /// <summary>Free level-only booster charges granted at level start for the current tier.</summary>
        public (int undo, int hammer) StartBoosters()
        {
            switch (Tier)
            {
                case 1: return (1, 0);
                case 2: return (1, 1);
                case 3: return (2, 1);
                default: return (0, 0);
            }
        }
    }

    public enum Booster { Undo, Hammer }

    public sealed class BoosterInventory
    {
        public int Undo, Hammer;

        public int Get(Booster b) => b == Booster.Undo ? Undo : Hammer;
        public void Add(Booster b, int n) { if (b == Booster.Undo) Undo += n; else Hammer += n; }

        public bool TryConsume(Booster b)
        {
            if (Get(b) <= 0) return false;
            Add(b, -1);
            return true;
        }

        public static int Price(GameConfig c, Booster b) => b == Booster.Undo ? c.BoosterPriceUndo : c.BoosterPriceHammer;
    }
}
