using System.Collections.Generic;

namespace TiltDrop.Core
{
    /// <summary>Event names/params (docs/TECH.md §7.1). Kept in Core so tests pin the schema.</summary>
    public static class AnalyticsEvents
    {
        public const string LevelStart = "level_start";
        public const string LevelFail = "level_fail";
        public const string LevelWin = "level_win";
        public const string LevelContinue = "level_continue";
        public const string BoosterUse = "booster_use";
        public const string AdRewarded = "ad_rewarded";
        public const string AdInterstitial = "ad_interstitial";
        public const string IapPurchase = "iap_purchase";
        public const string DailyPuzzle = "daily_puzzle";
        public const string Streak = "streak";
        public const string Consent = "consent";

        public static Dictionary<string, object> Start(Level lv, int number, int attempt) => new Dictionary<string, object>
        {
            ["level"] = number, ["world"] = Progression.WorldOf(number), ["tier"] = lv.Tier.ToString(), ["attempt"] = attempt, ["moves_limit"] = lv.Moves,
        };

        public static Dictionary<string, object> Fail(int number, int attempt, int movesUsed, int blocksLeft) => new Dictionary<string, object>
        {
            ["level"] = number, ["attempt"] = attempt, ["moves_used"] = movesUsed, ["blocks_left"] = blocksLeft,
        };

        public static Dictionary<string, object> Win(int number, int attempt, int movesUsed, int stars, int continues, int durationS) => new Dictionary<string, object>
        {
            ["level"] = number, ["attempt"] = attempt, ["moves_used"] = movesUsed, ["stars"] = stars, ["continues"] = continues, ["duration_s"] = durationS,
        };

        public static Dictionary<string, object> Continue(int number, string method, int n) => new Dictionary<string, object>
        {
            ["level"] = number, ["method"] = method, ["n"] = n,
        };
    }
}
