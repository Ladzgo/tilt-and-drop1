using System;
using System.Collections.Generic;

namespace TiltDrop.Core
{
    /// <summary>Level unlocks, stars per level, world star chests (fixed, shown in advance).</summary>
    public sealed class Progression
    {
        public const int WorldSize = 50;
        public static readonly int[] ChestThresholds = { 50, 100, 150 };
        public static readonly CalendarReward[] ChestRewards = { new CalendarReward(150, undo: 1), new CalendarReward(250, hammer: 1), new CalendarReward(500, undo: 1, hammer: 1) };

        public byte[] StarsByLevel = new byte[0]; // index = level - 1
        public int HighestCompleted;              // 0 = none
        public Dictionary<int, int> ChestsClaimed = new Dictionary<int, int>(); // world -> bitmask

        public int NextLevel => HighestCompleted + 1;
        public static int WorldOf(int level) => (level - 1) / WorldSize + 1;
        public bool IsUnlocked(int level) => level >= 1 && level <= HighestCompleted + 1;
        public int StarsOf(int level) => level >= 1 && level <= StarsByLevel.Length ? StarsByLevel[level - 1] : 0;

        /// <summary>Records a win; keeps the best star count. Returns true if this was the first completion.</summary>
        public bool RecordWin(int level, int stars)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
            if (StarsByLevel.Length < level) Array.Resize(ref StarsByLevel, level);
            StarsByLevel[level - 1] = (byte)Math.Max(StarsByLevel[level - 1], Math.Min(3, stars));
            bool first = level > HighestCompleted;
            if (first) HighestCompleted = level;
            return first;
        }

        public int WorldStars(int world)
        {
            int sum = 0;
            for (int l = (world - 1) * WorldSize + 1; l <= world * WorldSize; l++) sum += StarsOf(l);
            return sum;
        }

        public bool CanClaimChest(int world, int idx) =>
            WorldStars(world) >= ChestThresholds[idx] && ((ChestsClaimed.TryGetValue(world, out int m) ? m : 0) & (1 << idx)) == 0;

        public CalendarReward ClaimChest(int world, int idx)
        {
            if (!CanClaimChest(world, idx)) throw new InvalidOperationException("chest not available");
            ChestsClaimed[world] = (ChestsClaimed.TryGetValue(world, out int m) ? m : 0) | (1 << idx);
            return ChestRewards[idx];
        }
    }

    /// <summary>Sticker album: 12 per world page, awarded only through play on a fixed schedule
    /// (never for coins or money, never random). k 0..4 = level 10/20/30/40/50 of the world,
    /// k 5..7 = star chests, k 8..11 = daily puzzles solved while in that world (3/7/15/30).</summary>
    public static class Stickers
    {
        public const int PerPage = 12;
        public static readonly int[] DailyMilestones = { 3, 7, 15, 30 };

        public static string Id(int world, int k) => $"w{world}-{k}";

        public static string ForLevelComplete(int level) => level % 10 == 0 ? Id(Progression.WorldOf(level), (level - 1) % Progression.WorldSize / 10) : null;
        public static string ForChest(int world, int idx) => Id(world, 5 + idx);

        public static string ForDailySolved(int currentWorld, int dailySolvedInWorld)
        {
            int i = Array.IndexOf(DailyMilestones, dailySolvedInWorld);
            return i < 0 ? null : Id(currentWorld, 8 + i);
        }
    }
}
