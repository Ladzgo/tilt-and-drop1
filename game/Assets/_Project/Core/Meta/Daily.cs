using System;
using System.Collections.Generic;
using System.Text;

namespace TiltDrop.Core
{
    /// <summary>Daily-puzzle streak with a free weekly freeze (never sold). Days are Days.Of(local date).</summary>
    public sealed class DailyStreak
    {
        public const int MaxFreezes = 3;
        public int Streak, Best, Freezes;
        public int LastSolvedDay = -1;
        public int LastFreeFreezeWeek = -1;

        /// <summary>Grants this week's free freeze and bridges missed days with freezes (auto-applied).</summary>
        public void Refresh(int today)
        {
            int week = today / 7;
            if (week != LastFreeFreezeWeek)
            {
                LastFreeFreezeWeek = week;
                if (Freezes < 1) Freezes = 1;
            }
            if (LastSolvedDay < 0 || Streak == 0) return;
            int missed = today - LastSolvedDay - 1;
            if (missed <= 0) return;
            if (Freezes >= missed)
            {
                Freezes -= missed;
                LastSolvedDay = today - 1;
            }
            else
            {
                Streak = 0;
            }
        }

        public void OnSolved(int today)
        {
            Refresh(today);
            if (LastSolvedDay == today) return;
            Streak = LastSolvedDay == today - 1 ? Streak + 1 : 1;
            LastSolvedDay = today;
            Best = Math.Max(Best, Streak);
        }

        public void AddEarnedFreeze(int n = 1) => Freezes = Math.Min(MaxFreezes, Freezes + n);
    }

    public struct CalendarReward
    {
        public int Coins, Undo, Hammer, Freeze;
        public CalendarReward(int coins, int undo = 0, int hammer = 0, int freeze = 0) { Coins = coins; Undo = undo; Hammer = hammer; Freeze = freeze; }
    }

    /// <summary>Fixed 7-day login calendar. Missing a day pauses (never resets). No random rewards.</summary>
    public sealed class LoginCalendar
    {
        public static readonly CalendarReward[] Rewards =
        {
            new CalendarReward(50), new CalendarReward(75), new CalendarReward(100, undo: 1), new CalendarReward(125),
            new CalendarReward(150, hammer: 1), new CalendarReward(200), new CalendarReward(300, undo: 1, hammer: 1, freeze: 1),
        };

        public int DayIndex;          // 0..6, next reward to claim
        public int LastClaimDay = -1;

        public bool CanClaim(int today) => today > LastClaimDay; // clock rolled back -> no claim
        public CalendarReward Next => Rewards[DayIndex];

        public CalendarReward Claim(int today)
        {
            if (!CanClaim(today)) throw new InvalidOperationException("already claimed today");
            var r = Rewards[DayIndex];
            DayIndex = (DayIndex + 1) % Rewards.Length;
            LastClaimDay = today;
            return r;
        }
    }

    public static class DailyPuzzle
    {
        /// <summary>Wordle-style number: #1 on the epoch date.</summary>
        public static int Number(int epochDay, int today) => today - epochDay + 1;

        public static int LevelIndex(int number, int dailyCount) => ((number - 1) % dailyCount + dailyCount) % dailyCount;

        /// <summary>Weekend event runs Friday 00:00 – Sunday 23:59 local.</summary>
        public static bool IsWeekendEvent(DateTime local) => local.DayOfWeek == DayOfWeek.Friday || local.DayOfWeek == DayOfWeek.Saturday || local.DayOfWeek == DayOfWeek.Sunday;
    }

    public static class ShareGrid
    {
        public static string Square(int exits) => exits <= 0 ? "⬛" : exits == 1 ? "\U0001F7E8" : exits <= 3 ? "\U0001F7E7" : "\U0001F7E5";

        /// <summary>Spoiler-free result text (GDD §4.4): no rotation directions.</summary>
        public static string Build(int puzzleNumber, int stars, int movesUsed, int moveLimit, IList<int> exitsPerMove, string url = "tiltdrop.app/d/")
        {
            var sb = new StringBuilder();
            sb.Append("Tilt & Drop #").Append(puzzleNumber);
            if (stars > 0) sb.Append("  ").Append(new string('★', stars));
            sb.Append('\n');
            sb.Append(stars > 0 ? $"{movesUsed}/{moveLimit} moves" : $"X/{moveLimit} moves").Append('\n');
            foreach (int e in exitsPerMove) sb.Append(Square(e));
            sb.Append('\n').Append(url).Append(puzzleNumber);
            return sb.ToString();
        }
    }
}
