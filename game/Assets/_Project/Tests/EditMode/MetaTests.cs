using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace TiltDrop.Core.Tests
{
    public class MetaTests
    {
        static readonly DateTime T0 = new DateTime(2027, 3, 1, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void AdPacingFollowsGddRules()
        {
            var cfg = new GameConfig();
            var st = new AdPacingState();
            var p = new AdPacingPolicy(cfg, st);
            p.OnSessionStart(); // first session
            for (int i = 0; i < 5; i++) p.OnLevelWon();
            Assert.IsFalse(p.ShouldShowInterstitial(AdMoment.AfterWin, 30, false, T0), "never in first session");
            p.OnSessionStart();
            Assert.IsFalse(p.ShouldShowInterstitial(AdMoment.AfterWin, 14, false, T0), "never before level 15");
            Assert.IsTrue(p.ShouldShowInterstitial(AdMoment.AfterWin, 15, false, T0));
            Assert.IsFalse(p.ShouldShowInterstitial(AdMoment.AfterFail, 30, false, T0), "never after fail");
            Assert.IsFalse(p.ShouldShowInterstitial(AdMoment.AfterDaily, 30, false, T0));
            Assert.IsFalse(p.ShouldShowInterstitial(AdMoment.AfterWin, 30, true, T0), "remove ads");
            p.OnInterstitialShown(T0);
            for (int i = 0; i < 3; i++) p.OnLevelWon();
            Assert.IsFalse(p.ShouldShowInterstitial(AdMoment.AfterWin, 30, false, T0.AddSeconds(89)), "90 s cooldown");
            Assert.IsTrue(p.ShouldShowInterstitial(AdMoment.AfterWin, 30, false, T0.AddSeconds(90)));
            p.OnInterstitialShown(T0.AddSeconds(90));
            p.OnLevelWon(); p.OnLevelWon();
            Assert.IsFalse(p.ShouldShowInterstitial(AdMoment.AfterWin, 30, false, T0.AddSeconds(600)), "every 3 wins");
            cfg.Apply(new Dictionary<string, string> { ["ad_inter_level_interval"] = "2" });
            Assert.IsTrue(p.ShouldShowInterstitial(AdMoment.AfterWin, 30, false, T0.AddSeconds(600)), "remote config interval");
        }

        [Test]
        public void StarsAndRewards()
        {
            Assert.AreEqual(3, Stars.Compute(4, 4, 6, 0));
            Assert.AreEqual(2, Stars.Compute(5, 4, 6, 0));
            Assert.AreEqual(1, Stars.Compute(6, 4, 6, 0));
            Assert.AreEqual(2, Stars.Compute(6, 4, 9, 0)); // slack 5 -> par + 3
            Assert.AreEqual(1, Stars.Compute(4, 4, 6, 1)); // continue used
            var cfg = new GameConfig();
            Assert.AreEqual(10 + 15 + 4, Rewards.LevelWin(cfg, 3, 2));
            Assert.AreEqual(10 + 5 + 20, Rewards.LevelWin(cfg, 1, 50));
        }

        [Test]
        public void DailyStreakWithFreeWeeklyFreeze()
        {
            var s = new DailyStreak();
            int mon = Days.Of(new DateTime(2027, 3, 1)); // a Monday
            Assert.AreEqual(0, mon % 7, "day numbering is Monday-based");
            s.OnSolved(mon); s.OnSolved(mon + 1);
            Assert.AreEqual(2, s.Streak);
            Assert.AreEqual(1, s.Freezes, "free weekly freeze");
            s.OnSolved(mon + 3); // missed Wednesday -> freeze bridges it
            Assert.AreEqual(3, s.Streak);
            Assert.AreEqual(0, s.Freezes);
            s.OnSolved(mon + 5); // missed Friday, no freeze left
            Assert.AreEqual(1, s.Streak);
            s.OnSolved(mon + 7); // next Monday: new free freeze bridges Sunday
            Assert.AreEqual(2, s.Streak);
            s.OnSolved(mon + 7);
            Assert.AreEqual(2, s.Streak, "same day twice is a no-op");
            s.AddEarnedFreeze(5);
            Assert.AreEqual(DailyStreak.MaxFreezes, s.Freezes);
            Assert.AreEqual(3, s.Best);
        }

        [Test]
        public void LoginCalendarNeverResets()
        {
            var c = new LoginCalendar();
            Assert.AreEqual(50, c.Claim(100).Coins);
            Assert.IsFalse(c.CanClaim(100));
            Assert.Throws<InvalidOperationException>(() => c.Claim(100));
            Assert.AreEqual(75, c.Claim(105).Coins, "gap pauses, does not reset");
            Assert.IsFalse(c.CanClaim(104), "clock rollback");
            for (int d = 106; d < 111; d++) c.Claim(d);
            Assert.AreEqual(0, c.DayIndex, "7-day cycle wraps");
        }

        [Test]
        public void DailyNumberingAndShareGrid()
        {
            int epoch = Days.Parse("2027-01-01");
            Assert.AreEqual(1, DailyPuzzle.Number(epoch, epoch));
            Assert.AreEqual(3, DailyPuzzle.LevelIndex(404, 400));
            string share = ShareGrid.Build(123, 3, 5, 7, new[] { 0, 1, 2, 0, 5 });
            StringAssert.StartsWith("Tilt & Drop #123  ★★★\n5/7 moves\n", share);
            StringAssert.Contains("⬛\U0001F7E8\U0001F7E7⬛\U0001F7E5", share);
            StringAssert.DoesNotContain("CW", share);
            StringAssert.Contains("X/7", ShareGrid.Build(1, 0, 7, 7, new int[0]));
            Assert.IsTrue(DailyPuzzle.IsWeekendEvent(new DateTime(2027, 3, 5)));
            Assert.IsFalse(DailyPuzzle.IsWeekendEvent(new DateTime(2027, 3, 4)));
        }

        [Test]
        public void PrivacyDecisions()
        {
            var today = new DateTime(2027, 6, 15);
            Assert.AreEqual(AgeBand.Under13, PrivacyRules.FromBirth(2015, 1, today));
            Assert.AreEqual(AgeBand.Under16, PrivacyRules.FromBirth(2011, 7, today)); // turns 16 next month
            Assert.AreEqual(AgeBand.Adult, PrivacyRules.FromBirth(2011, 6, today));
            Assert.AreEqual(AgeBand.Unknown, PrivacyRules.FromBirth(2030, 1, today));
            Assert.AreEqual(AgeBand.Under16, PrivacyRules.Update(AgeBand.Under16, AgeBand.Adult), "only younger");
            Assert.AreEqual(AgeBand.Under13, PrivacyRules.Update(AgeBand.Adult, AgeBand.Under13));

            var kid = PrivacyRules.Decide(AgeBand.Under16, true, true, true, false);
            Assert.IsFalse(kid.InitMax); Assert.IsTrue(kid.InitAdMobDirect); Assert.IsFalse(kid.PersonalizedAds);
            Assert.IsFalse(kid.Purchases); Assert.IsFalse(kid.Analytics); Assert.IsFalse(kid.RequestAtt); Assert.IsFalse(kid.ChildDirected);
            Assert.IsTrue(PrivacyRules.Decide(AgeBand.Under13, false, false, false, false).ChildDirected);

            var adult = PrivacyRules.Decide(AgeBand.Adult, true, false, true, false);
            Assert.IsTrue(adult.InitMax); Assert.IsFalse(adult.PersonalizedAds); Assert.IsFalse(adult.RequestAtt);
            Assert.IsTrue(adult.Analytics); Assert.IsTrue(adult.Purchases); Assert.IsTrue(adult.Crashlytics);
            Assert.IsFalse(PrivacyRules.Decide(AgeBand.Unknown, true, true, true, false).InitAdMobDirect);
        }

        [Test]
        public void SessionUndoContinueAndStars()
        {
            var cfg = new GameConfig();
            var lv = Level.FromRows(new[] { "....", "....", "11.2" }, gL: "..1", gR: "..2", moves: 3);
            lv.Par = 3;
            var s = new GameSession(lv, 1, cfg);
            s.Rotate(Rot.CW);
            Assert.AreEqual(2, s.State.MovesLeft);
            s.Undo();
            Assert.AreEqual(3, s.State.MovesLeft, "undo refunds the move");
            Assert.IsFalse(s.CanUndo);
            s.Rotate(Rot.CW); s.Rotate(Rot.CW); s.Rotate(Rot.CW);
            Assert.AreEqual(Status.Lost, s.State.Status);
            Assert.IsFalse(s.CanUndo, "no undo after loss");
            Assert.IsTrue(s.CanContinue);
            s.Continue();
            Assert.AreEqual(3, s.State.MovesLeft);
            s.Rotate(Rot.CW); // up -> right... play the known solution from here
            Assert.AreEqual(1, s.Continues);
            var daily = new GameSession(lv, 1, cfg, isDaily: true);
            daily.Rotate(Rot.CW);
            Assert.IsFalse(daily.CanUndo, "boosters disabled in daily");
            Assert.IsFalse(daily.CanHammer);

            var win = new GameSession(lv, 1, cfg);
            win.Rotate(Rot.CW); win.Rotate(Rot.CCW); win.Rotate(Rot.CCW);
            Assert.AreEqual(Status.Won, win.State.Status);
            Assert.AreEqual(3, win.EarnedStars);
            CollectionAssert.AreEqual(new[] { 1, 0, 2 }, win.ExitsPerMove);
            Assert.AreEqual(25, win.CoinReward);
        }

        [Test]
        public void EconomyObjects()
        {
            var w = new Wallet(50);
            int seen = 0;
            w.Changed += (bal, d) => seen += d;
            Assert.IsFalse(w.TrySpend(100));
            w.Add(100);
            Assert.IsTrue(w.TrySpend(100));
            Assert.AreEqual(50, w.Coins);
            Assert.AreEqual(0, seen);
            Assert.Throws<ArgumentOutOfRangeException>(() => w.Add(-1));

            var cfg = new GameConfig();
            var pig = new PiggyBank();
            for (int i = 0; i < 1100; i++) pig.OnLevelReward(cfg, 30); // +3 per win, capped
            Assert.AreEqual(3000, pig.Coins);
            Assert.IsTrue(pig.IsFull(cfg) && pig.CanBreak(cfg));
            Assert.AreEqual(3000, pig.Break());

            var ws = new WinStreak();
            ws.OnWin(false); ws.OnWin(false);
            Assert.AreEqual((1, 0), ws.StartBoosters());
            ws.OnWin(false); ws.OnWin(false); ws.OnWin(false); ws.OnWin(false);
            Assert.AreEqual(3, ws.Tier);
            ws.OnWin(true);
            Assert.AreEqual(0, ws.Tier);

            var inv = new BoosterInventory();
            Assert.IsFalse(inv.TryConsume(Booster.Hammer));
            inv.Add(Booster.Hammer, 1);
            Assert.IsTrue(inv.TryConsume(Booster.Hammer));
            Assert.AreEqual(200, BoosterInventory.Price(cfg, Booster.Hammer));
            Assert.AreEqual("≈ €1.99", StoreCatalog.ApproxEur(1000));
            Assert.AreEqual(9, StoreCatalog.Products.Count);
            Assert.IsTrue(StoreCatalog.Find(StoreCatalog.RemoveAds).RemovesAds);
        }

        [Test]
        public void ProgressionChestsAndStickers()
        {
            var p = new Progression();
            Assert.IsTrue(p.IsUnlocked(1)); Assert.IsFalse(p.IsUnlocked(2));
            for (int l = 1; l <= 50; l++) p.RecordWin(l, 3);
            Assert.IsFalse(p.RecordWin(10, 1), "replay is not a first completion");
            Assert.AreEqual(3, p.StarsOf(10), "best stars kept");
            Assert.AreEqual(150, p.WorldStars(1));
            Assert.AreEqual(500, p.ClaimChest(1, 2).Coins);
            Assert.IsFalse(p.CanClaimChest(1, 2));
            Assert.AreEqual(2, Progression.WorldOf(51));
            Assert.AreEqual("w1-0", Stickers.ForLevelComplete(10));
            Assert.AreEqual("w2-4", Stickers.ForLevelComplete(100));
            Assert.IsNull(Stickers.ForLevelComplete(11));
            Assert.AreEqual("w3-9", Stickers.ForDailySolved(3, 7));
            Assert.AreEqual("w1-6", Stickers.ForChest(1, 1));
        }

        [Test]
        public void SaveRoundTripAndTamperDetection()
        {
            var s = new SaveData { Coins = 1234, AgeBand = AgeBand.Adult, RemoveAds = true };
            s.Progress.RecordWin(1, 3); s.Progress.RecordWin(2, 2);
            s.DailyStreak.OnSolved(1000);
            s.Calendar.Claim(1000);
            s.Stickers.Add("w1-0");
            s.Attempts[5] = 3;
            s.Settings.Speed = 3;
            string json = s.ToJson();
            var b = SaveData.FromJson(json);
            Assert.IsNotNull(b);
            Assert.AreEqual(1234, b.Coins);
            Assert.AreEqual(AgeBand.Adult, b.AgeBand);
            Assert.AreEqual(2, b.Progress.StarsOf(2));
            Assert.AreEqual(2, b.Progress.HighestCompleted);
            Assert.AreEqual(1, b.DailyStreak.Streak);
            Assert.AreEqual(1, b.Calendar.DayIndex);
            Assert.IsTrue(b.Stickers.Contains("w1-0"));
            Assert.AreEqual(3, b.Attempts[5]);
            Assert.AreEqual(3, b.Settings.Speed);
            Assert.IsTrue(b.RemoveAds);
            Assert.AreEqual(json, b.ToJson(), "stable serialization");
            Assert.IsNull(SaveData.FromJson(json.Replace("1234", "9999")), "tampering detected");
            Assert.IsNull(SaveData.FromJson("not json"));
        }

        [Test]
        public void RemoteConfigApplyAndMoveOverrides()
        {
            var cfg = new GameConfig();
            cfg.Apply(new Dictionary<string, string> { ["continue_cost"] = "120", ["piggy_rate"] = "0.2", ["bogus"] = "1", ["max_continues"] = "x", ["difficulty_overrides"] = "{\"7\": 9}" });
            Assert.AreEqual(120, cfg.ContinueCost);
            Assert.AreEqual(0.2, cfg.PiggyRate, 1e-9);
            Assert.AreEqual(2, cfg.MaxContinues, "invalid value ignored");
            Assert.AreEqual(9, cfg.MoveLimit(7, 5, 6));
            Assert.AreEqual(6, cfg.MoveLimit(8, 5, 6));
            cfg.Apply(new Dictionary<string, string> { ["move_slack_delta"] = "-5" });
            Assert.AreEqual(5, cfg.MoveLimit(8, 5, 6), "never below par");
            Assert.AreEqual(GameConfig.Keys.Length, cfg.Defaults().Count);
        }

        [Test]
        public void MiniJsonRoundTrip()
        {
            var o = (Dictionary<string, object>)MiniJson.Parse("{\"a\": [1, 2.5, \"x\\n\\u00e9\", true, null], \"b\": {}}");
            Assert.AreEqual(1L, o.Arr("a")[0]);
            Assert.AreEqual(2.5, o.Arr("a")[1]);
            Assert.AreEqual("x\né", o.Arr("a")[2]);
            Assert.AreEqual("{\"a\":[1,2.5,\"x\\né\",true,null],\"b\":{}}", MiniJson.Serialize(o));
            Assert.Throws<FormatException>(() => MiniJson.Parse("{\"a\":1} x"));
        }
    }
}
