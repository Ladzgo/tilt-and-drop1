using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace TiltDrop.Core
{
    public sealed class Settings
    {
        public bool Sound = true, Music = true, Haptics = true, ReducedMotion = false;
        public int Speed = 1; // 1, 2 (=1.5x), 3 (=2x)
    }

    /// <summary>Everything persisted locally. JSON + HMAC (deters casual edits only; store receipts are authoritative).</summary>
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;
        const string HmacKey = "tilt&drop/local-save/v1"; // obfuscation only, not a secret

        public int Version = CurrentVersion;
        public long CreatedUtcTicks;
        public AgeBand AgeBand = AgeBand.Unknown;
        public bool ConsentPersonalized, ConsentAnalytics, CrashlyticsOptOut;
        public int Coins;
        public BoosterInventory Boosters = new BoosterInventory();
        public Progression Progress = new Progression();
        public WinStreak WinStreak = new WinStreak();
        public DailyStreak DailyStreak = new DailyStreak();
        public LoginCalendar Calendar = new LoginCalendar();
        public PiggyBank Piggy = new PiggyBank();
        public AdPacingState Ads = new AdPacingState();
        public Settings Settings = new Settings();
        public bool RemoveAds, StarterBought;
        public HashSet<string> Stickers = new HashSet<string>();
        public Dictionary<int, int> DailyResults = new Dictionary<int, int>(); // day -> stars (0 = failed)
        public int DailySolvedInWorld;
        public int DailySolvedWorld;
        public Dictionary<int, int> Attempts = new Dictionary<int, int>();   // level -> attempts
        public int LastFreeBoosterDay = -1;

        public string ToJson()
        {
            var d = new Dictionary<string, object>
            {
                ["version"] = Version,
                ["created"] = CreatedUtcTicks,
                ["profile"] = new Dictionary<string, object> { ["age"] = (int)AgeBand, ["cPers"] = ConsentPersonalized, ["cAna"] = ConsentAnalytics, ["crashOut"] = CrashlyticsOptOut },
                ["coins"] = Coins,
                ["boosters"] = new Dictionary<string, object> { ["undo"] = Boosters.Undo, ["hammer"] = Boosters.Hammer },
                ["progress"] = new Dictionary<string, object>
                {
                    ["highest"] = Progress.HighestCompleted,
                    ["stars"] = Convert.ToBase64String(Progress.StarsByLevel),
                    ["chests"] = IntMap(Progress.ChestsClaimed),
                },
                ["winStreak"] = WinStreak.Count,
                ["daily"] = new Dictionary<string, object>
                {
                    ["streak"] = DailyStreak.Streak, ["best"] = DailyStreak.Best, ["freezes"] = DailyStreak.Freezes,
                    ["last"] = DailyStreak.LastSolvedDay, ["freeWeek"] = DailyStreak.LastFreeFreezeWeek,
                    ["results"] = IntMap(DailyResults), ["inWorld"] = DailySolvedInWorld, ["world"] = DailySolvedWorld,
                },
                ["calendar"] = new Dictionary<string, object> { ["day"] = Calendar.DayIndex, ["last"] = Calendar.LastClaimDay },
                ["piggy"] = Piggy.Coins,
                ["ads"] = new Dictionary<string, object> { ["session"] = Ads.SessionIndex, ["wins"] = Ads.WinsSinceInterstitial, ["last"] = Ads.LastInterstitialUtcTicks },
                ["settings"] = new Dictionary<string, object>
                {
                    ["sound"] = Settings.Sound, ["music"] = Settings.Music, ["haptics"] = Settings.Haptics,
                    ["reducedMotion"] = Settings.ReducedMotion, ["speed"] = Settings.Speed,
                },
                ["purchases"] = new Dictionary<string, object> { ["removeAds"] = RemoveAds, ["starter"] = StarterBought },
                ["stickers"] = new List<object>(Stickers),
                ["attempts"] = IntMap(Attempts),
                ["freeBoosterDay"] = LastFreeBoosterDay,
            };
            string body = MiniJson.Serialize(d);
            return MiniJson.Serialize(new Dictionary<string, object> { ["data"] = body, ["sig"] = Sign(body) });
        }

        /// <summary>Returns null if the file is corrupt or the signature does not match.</summary>
        public static SaveData FromJson(string json)
        {
            Dictionary<string, object> wrap;
            try { wrap = MiniJson.Parse(json) as Dictionary<string, object>; }
            catch (FormatException) { return null; }
            string body = wrap.Str("data"), sig = wrap.Str("sig");
            if (body == null || sig != Sign(body)) return null;
            var d = (Dictionary<string, object>)MiniJson.Parse(body);
            var s = new SaveData
            {
                Version = d.Int("version", CurrentVersion),
                CreatedUtcTicks = d.Long("created"),
                Coins = d.Int("coins"),
                LastFreeBoosterDay = d.Int("freeBoosterDay", -1),
            };
            var pr = d.Obj("profile");
            s.AgeBand = (AgeBand)pr.Int("age");
            s.ConsentPersonalized = pr.Bool("cPers");
            s.ConsentAnalytics = pr.Bool("cAna");
            s.CrashlyticsOptOut = pr.Bool("crashOut");
            var b = d.Obj("boosters");
            s.Boosters.Undo = b.Int("undo");
            s.Boosters.Hammer = b.Int("hammer");
            var p = d.Obj("progress");
            s.Progress.HighestCompleted = p.Int("highest");
            s.Progress.StarsByLevel = Convert.FromBase64String(p.Str("stars", ""));
            s.Progress.ChestsClaimed = ReadIntMap(p.Obj("chests"));
            s.WinStreak.Count = d.Int("winStreak");
            var dl = d.Obj("daily");
            s.DailyStreak.Streak = dl.Int("streak");
            s.DailyStreak.Best = dl.Int("best");
            s.DailyStreak.Freezes = dl.Int("freezes");
            s.DailyStreak.LastSolvedDay = dl.Int("last", -1);
            s.DailyStreak.LastFreeFreezeWeek = dl.Int("freeWeek", -1);
            s.DailyResults = ReadIntMap(dl.Obj("results"));
            s.DailySolvedInWorld = dl.Int("inWorld");
            s.DailySolvedWorld = dl.Int("world");
            var c = d.Obj("calendar");
            s.Calendar.DayIndex = c.Int("day");
            s.Calendar.LastClaimDay = c.Int("last", -1);
            s.Piggy.Coins = d.Int("piggy");
            var a = d.Obj("ads");
            s.Ads.SessionIndex = a.Int("session");
            s.Ads.WinsSinceInterstitial = a.Int("wins");
            s.Ads.LastInterstitialUtcTicks = a.Long("last");
            var st = d.Obj("settings");
            s.Settings.Sound = st.Bool("sound", true);
            s.Settings.Music = st.Bool("music", true);
            s.Settings.Haptics = st.Bool("haptics", true);
            s.Settings.ReducedMotion = st.Bool("reducedMotion");
            s.Settings.Speed = st.Int("speed", 1);
            var pu = d.Obj("purchases");
            s.RemoveAds = pu.Bool("removeAds");
            s.StarterBought = pu.Bool("starter");
            foreach (var o in d.Arr("stickers") ?? new List<object>()) s.Stickers.Add((string)o);
            s.Attempts = ReadIntMap(d.Obj("attempts"));
            return s;
        }

        static Dictionary<string, object> IntMap(Dictionary<int, int> m)
        {
            var r = new Dictionary<string, object>();
            foreach (var kv in m) r[kv.Key.ToString(System.Globalization.CultureInfo.InvariantCulture)] = kv.Value;
            return r;
        }

        static Dictionary<int, int> ReadIntMap(Dictionary<string, object> d)
        {
            var r = new Dictionary<int, int>();
            if (d == null) return r;
            foreach (var kv in d) r[int.Parse(kv.Key, System.Globalization.CultureInfo.InvariantCulture)] = Convert.ToInt32(kv.Value, System.Globalization.CultureInfo.InvariantCulture);
            return r;
        }

        static string Sign(string body)
        {
            using (var h = new HMACSHA256(Encoding.UTF8.GetBytes(HmacKey)))
                return Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(body)));
        }
    }
}
