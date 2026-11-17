using System;
using System.Collections.Generic;
using System.Globalization;

namespace TiltDrop.Core
{
    /// <summary>Remote-config driven tunables (docs/TECH.md §7.2). Defaults ship in code.</summary>
    public sealed class GameConfig
    {
        public int AdInterMinLevel = 15;
        public int AdInterLevelInterval = 3;
        public int AdInterCooldownSeconds = 90;
        public bool AdInterFirstSession = false;
        public int ContinueMoves = 3;
        public int ContinueCost = 100;
        public int MaxContinues = 2;
        public int MoveSlackDelta = 0;
        public int LevelRewardBase = 10;
        public int LevelRewardPerStar = 5;
        public int LevelRewardPerLeftoverMove = 2;
        public int LevelRewardLeftoverCap = 20;
        public int DailyPuzzleReward = 30;
        public double PiggyRate = 0.1;
        public int PiggyCap = 3000;
        public int PiggyUnlock = 1000;
        public int BoosterPriceUndo = 150;
        public int BoosterPriceHammer = 200;
        public string DifficultyOverrides = "{}"; // JSON level -> moves
        public string DailyOverride = "{}";       // JSON yyyy-MM-dd -> daily index
        public string ContentPackUrl = "";

        public static readonly string[] Keys =
        {
            "ad_inter_min_level", "ad_inter_level_interval", "ad_inter_cooldown_s", "ad_inter_first_session",
            "continue_moves", "continue_cost", "max_continues", "move_slack_delta", "level_reward_base",
            "daily_puzzle_reward", "piggy_rate", "booster_price_undo", "booster_price_hammer",
            "difficulty_overrides", "daily_override", "content_pack_url",
        };

        /// <summary>Applies string values (as delivered by Remote Config). Unknown/invalid values are ignored.</summary>
        public void Apply(IDictionary<string, string> v)
        {
            int I(string k, int def) => v.TryGetValue(k, out var s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int r) ? r : def;
            bool B(string k, bool def) => v.TryGetValue(k, out var s) && bool.TryParse(s, out bool r) ? r : def;
            double D(string k, double def) => v.TryGetValue(k, out var s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double r) ? r : def;
            string S(string k, string def) => v.TryGetValue(k, out var s) && s != null ? s : def;
            AdInterMinLevel = I("ad_inter_min_level", AdInterMinLevel);
            AdInterLevelInterval = Math.Max(1, I("ad_inter_level_interval", AdInterLevelInterval));
            AdInterCooldownSeconds = Math.Max(0, I("ad_inter_cooldown_s", AdInterCooldownSeconds));
            AdInterFirstSession = B("ad_inter_first_session", AdInterFirstSession);
            ContinueMoves = I("continue_moves", ContinueMoves);
            ContinueCost = I("continue_cost", ContinueCost);
            MaxContinues = I("max_continues", MaxContinues);
            MoveSlackDelta = I("move_slack_delta", MoveSlackDelta);
            LevelRewardBase = I("level_reward_base", LevelRewardBase);
            DailyPuzzleReward = I("daily_puzzle_reward", DailyPuzzleReward);
            PiggyRate = D("piggy_rate", PiggyRate);
            BoosterPriceUndo = I("booster_price_undo", BoosterPriceUndo);
            BoosterPriceHammer = I("booster_price_hammer", BoosterPriceHammer);
            DifficultyOverrides = S("difficulty_overrides", DifficultyOverrides);
            DailyOverride = S("daily_override", DailyOverride);
            ContentPackUrl = S("content_pack_url", ContentPackUrl);
        }

        public Dictionary<string, string> Defaults()
        {
            var c = CultureInfo.InvariantCulture;
            return new Dictionary<string, string>
            {
                ["ad_inter_min_level"] = AdInterMinLevel.ToString(c),
                ["ad_inter_level_interval"] = AdInterLevelInterval.ToString(c),
                ["ad_inter_cooldown_s"] = AdInterCooldownSeconds.ToString(c),
                ["ad_inter_first_session"] = AdInterFirstSession ? "true" : "false",
                ["continue_moves"] = ContinueMoves.ToString(c),
                ["continue_cost"] = ContinueCost.ToString(c),
                ["max_continues"] = MaxContinues.ToString(c),
                ["move_slack_delta"] = MoveSlackDelta.ToString(c),
                ["level_reward_base"] = LevelRewardBase.ToString(c),
                ["daily_puzzle_reward"] = DailyPuzzleReward.ToString(c),
                ["piggy_rate"] = PiggyRate.ToString(c),
                ["booster_price_undo"] = BoosterPriceUndo.ToString(c),
                ["booster_price_hammer"] = BoosterPriceHammer.ToString(c),
                ["difficulty_overrides"] = DifficultyOverrides,
                ["daily_override"] = DailyOverride,
                ["content_pack_url"] = ContentPackUrl,
            };
        }

        /// <summary>Move limit for a level after remote overrides (never below par).</summary>
        public int MoveLimit(int levelNumber, int par, int shippedMoves)
        {
            int moves = shippedMoves + MoveSlackDelta;
            try
            {
                if (MiniJson.Parse(DifficultyOverrides) is Dictionary<string, object> d && d.TryGetValue(levelNumber.ToString(CultureInfo.InvariantCulture), out var o))
                    moves = Convert.ToInt32(o, CultureInfo.InvariantCulture);
            }
            catch (FormatException) { }
            return Math.Max(par, moves);
        }
    }
}
