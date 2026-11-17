using System;
using TiltDrop.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TiltDrop.Game
{
    /// <summary>All modal popups. No countdown timers anywhere (COMPLIANCE §5). "No thanks" is always equally visible.</summary>
    public static class Popups
    {
        static Transform Layer => GameRoot.I.PopupLayer;

        public static Popup Info(string title, string body, string ok = "OK", Action onOk = null)
        {
            var p = new Popup(Layer, title);
            p.Text(body);
            p.Button(ok, Palette.Button, onOk);
            return p;
        }

        public static Popup Confirm(string title, string body, string yes, Action onYes, string no = "Cancel", Action onNo = null)
        {
            var p = new Popup(Layer, title);
            p.Text(body);
            p.Button(yes, Palette.Button, onYes);
            p.Button(no, Palette.ButtonAlt, onNo);
            return p;
        }

        public static void Win(GameRoot g, GameSession s, int coins, string sticker, Action next, Action map, Action doubled)
        {
            var p = new Popup(Layer, s.IsDaily ? "Daily solved!" : "Level complete!");
            var stars = UIKit.StarRow(p.Body, 0, 130, 20);
            var imgs = stars.GetComponentsInChildren<Image>();
            for (int i = 0; i < s.EarnedStars && i < imgs.Length; i++)
            {
                var img = imgs[i];
                int k = i;
                Tweener.Delay(0.25f + 0.22f * i, () =>
                {
                    if (img == null) return;
                    img.color = Palette.Gold;
                    g.Sfx.Play(Sound.Star, 1f, 1f + 0.12f * k);
                    Haptics.Play(Haptic.Light);
                    var t = img.transform;
                    Tweener.Run(0.35f, q => { if (t != null) t.localScale = Vector3.one * Mathf.LerpUnclamped(1.6f, 1f, Ease.OutBack(q)); }, Ease.Linear);
                });
            }
            var coinText = p.Text($"+{coins} coins", 56, Palette.Gold);
            if (!string.IsNullOrEmpty(sticker)) p.Text("New sticker for your album!", 40, Palette.Text);
            if (doubled != null && g.Ads.IsRewardedReady)
            {
                Button dbl = null;
                dbl = p.Button("Double coins (watch ad)", Palette.Gold, () =>
                {
                    g.ShowRewarded("double_coins", ok =>
                    {
                        if (!ok) return;
                        doubled();
                        coinText.text = $"+{coins * 2} coins";
                        if (dbl != null) dbl.interactable = false;
                    });
                }, closes: false);
            }
            if (next != null) p.Button("Next", Palette.Good, next);
            p.Button("Map", Palette.ButtonAlt, map);
        }

        public static void Fail(GameRoot g, GameSession s, Action continueAd, Action continueCoins, Action retry, Action map)
        {
            var p = new Popup(Layer, "Out of moves");
            p.Text($"{s.State.CountColored()} blocks left");
            if (s.CanContinue)
            {
                if (continueAd != null && g.Ads.IsRewardedReady) p.Button($"+{g.Config.ContinueMoves} moves (watch ad)", Palette.Gold, continueAd);
                var cost = g.Config.ContinueCost;
                var coinBtn = p.Button($"+{g.Config.ContinueMoves} moves ({cost} coins, {StoreCatalog.ApproxEur(cost)})", Palette.Button, continueCoins);
                coinBtn.interactable = g.Wallet.Coins >= cost;
            }
            p.Button("Retry", Palette.ButtonAlt, retry);
            p.Button("Map", Palette.ButtonAlt, map);
        }

        public static void Pause(GameRoot g, Action resume, Action restart, Action map)
        {
            var p = new Popup(Layer, "Paused");
            p.OnClosed = null;
            p.Button("Resume", Palette.Good, resume);
            p.Button("Restart", Palette.Button, restart);
            p.Button("Map", Palette.ButtonAlt, map);
            p.Button("Settings", Palette.ButtonAlt, () => Settings(g));
        }

        public static void Settings(GameRoot g)
        {
            var st = g.Save.Settings;
            var p = new Popup(Layer, "Settings");
            Button Toggle(string name, Func<bool> get, Action<bool> set)
            {
                Button b = null;
                b = p.Button($"{name}: {(get() ? "On" : "Off")}", Palette.ButtonAlt, () =>
                {
                    set(!get());
                    b.SetText($"{name}: {(get() ? "On" : "Off")}");
                    g.ApplySettings();
                    g.Dirty();
                }, closes: false);
                return b;
            }
            Toggle("Sound", () => st.Sound, v => st.Sound = v);
            Toggle("Haptics", () => st.Haptics, v => st.Haptics = v);
            Toggle("Reduced motion", () => st.ReducedMotion, v => st.ReducedMotion = v);
            Button speed = null;
            speed = p.Button(SpeedText(st.Speed), Palette.ButtonAlt, () => { st.Speed = st.Speed % 3 + 1; speed.SetText(SpeedText(st.Speed)); g.ApplySettings(); g.Dirty(); }, closes: false);
            if (g.Save.AgeBand == AgeBand.Adult)
                Toggle("Crash reports", () => !g.Save.CrashlyticsOptOut, v => { g.Save.CrashlyticsOptOut = !v; g.RefreshPrivacy(); });
            if (g.Ads.PrivacyOptionsRequired) p.Button("Privacy choices", Palette.ButtonAlt, () => g.Ads.ShowPrivacyOptions(g.RefreshPrivacy));
            p.Button("How this game earns money", Palette.ButtonAlt, () => Info("Ads & purchases",
                "Tilt & Drop is free. We show ads after some wins (never right after a loss) and offer optional purchases. " +
                "Every level can be solved without paying. Ads are only personalized if you allowed it. Players under 16 see only non-personalized ads and no purchases."), closes: false);
            p.Button("Privacy policy", Palette.ButtonAlt, () => Application.OpenURL(GameRoot.PrivacyPolicyUrl), closes: false);
            if (g.Privacy.Purchases) p.Button("Restore purchases", Palette.ButtonAlt, g.RestorePurchases, closes: false);
            p.Button("Delete my data", Palette.ButtonAlt, () => Confirm("Delete all data?",
                "Progress, coins and settings on this device will be erased and analytics identifiers reset. This cannot be undone.",
                "Delete", g.DeleteAllData), closes: false);
            p.Text("Version " + Application.version, 32);
            p.Button("Close", Palette.Button, null);
        }

        static string SpeedText(int s) => "Animation speed: " + (s == 1 ? "1x" : s == 2 ? "1.5x" : "2x");

        public static void Store(GameRoot g)
        {
            if (!g.Privacy.Purchases) return; // under 16: no paid purchases at all
            var p = new Popup(Layer, "Shop", 960);
            foreach (var def in StoreCatalog.Products)
            {
                if (def.Id == StoreCatalog.Piggy) continue;
                if (def.OncePerUser && g.Save.StarterBought) continue;
                if (def.RemovesAds && g.Save.RemoveAds && def.Coins == 0) continue;
                string price = g.Iap.LocalizedPrice(def.Id) ?? def.FallbackPriceEur;
                string contents = def.Title;
                if (def.Coins > 0 && !def.Title.Contains("Coins")) contents += $" - {def.Coins:N0} coins";
                if (def.Undo > 0 || def.Hammer > 0) contents += $", {def.Undo} Undo, {def.Hammer} Hammer";
                if (def.RemovesAds && def.Coins > 0) contents += ", no ads";
                var d = def;
                p.Button($"{contents}   {price}", def.RemovesAds ? Palette.Good : Palette.Button, () => g.Buy(d.Id), closes: false).Size(150);
            }
            var piggy = g.Save.Piggy;
            string pprice = g.Iap.LocalizedPrice(StoreCatalog.Piggy) ?? StoreCatalog.Find(StoreCatalog.Piggy).FallbackPriceEur;
            var pb = p.Button($"Piggy bank: {piggy.Coins:N0} coins   {pprice}", Palette.Gold, () => g.Buy(StoreCatalog.Piggy), closes: false);
            pb.interactable = piggy.CanBreak(g.Config);
            if (!piggy.CanBreak(g.Config)) p.Text($"The piggy bank opens at {g.Config.PiggyUnlock:N0} coins. It fills as you win.", 32);
            p.Text("Prices include VAT. Coins never expire.", 30);
            p.Button("Close", Palette.ButtonAlt, null);
        }

        public static void Daily(GameRoot g)
        {
            int today = Days.Of(g.Clock.LocalNow);
            if (g.Catalog.DailyCount == 0) { Info("Daily puzzle", "Not available in this build."); return; }
            int n = DailyPuzzle.Number(g.Catalog.DailyEpochDay, today);
            var ds = g.Save.DailyStreak;
            ds.Refresh(today);
            var p = new Popup(Layer, $"Daily puzzle #{n}");
            p.Text($"Streak: {ds.Streak} day{(ds.Streak == 1 ? "" : "s")}   (best {ds.Best})", 44, Palette.Text);
            p.Text($"Streak freezes: {ds.Freezes}  -  one free every week", 34);
            bool played = g.Save.DailyResults.TryGetValue(today, out int result);
            if (played)
            {
                p.Text(result > 0 ? $"Solved today with {result} star{(result == 1 ? "" : "s")}!" : "Today's attempt is done. Practice is still open.", 40, Palette.Text);
                if (g.LastDailyShare != null) p.Button("Share result", Palette.Gold, () => g.Share(g.LastDailyShare), closes: false);
                p.Button("Practice", Palette.Button, () => g.StartDaily(practice: true));
            }
            else
            {
                p.Text("One scored attempt per day. Boosters are off.", 34);
                p.Button("Play", Palette.Good, () => g.StartDaily(practice: false));
            }
            p.Button("Close", Palette.ButtonAlt, null);
        }

        public static void Calendar(GameRoot g)
        {
            int today = Days.Of(g.Clock.LocalNow);
            var cal = g.Save.Calendar;
            var p = new Popup(Layer, "Daily gift");
            p.Text("A fixed reward for every day you visit. Missing a day never resets your progress.", 34);
            var grid = UIKit.Rect("days", p.Body);
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(250, 180);
            gl.spacing = new Vector2(16, 16);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 3;
            gl.childAlignment = TextAnchor.UpperCenter;
            grid.Size(3 * 180 + 2 * 16);
            for (int i = 0; i < LoginCalendar.Rewards.Length; i++)
            {
                var r = LoginCalendar.Rewards[i];
                bool done = i < cal.DayIndex, isNext = i == cal.DayIndex;
                var tile = UIKit.Panel(grid, isNext ? Palette.Gold : done ? Palette.Good : Palette.ButtonAlt, "day");
                string extra = (r.Undo > 0 ? "\n+Undo" : "") + (r.Hammer > 0 ? " +Hammer" : "") + (r.Freeze > 0 ? "\n+Freeze" : "");
                var t = UIKit.Label(tile.transform, $"Day {i + 1}\n{r.Coins} coins{extra}", 32, Color.white, TextAnchor.MiddleCenter, true);
                t.rectTransform.Stretch(6, 6, 6, 6);
            }
            if (cal.CanClaim(today))
                p.Button($"Claim day {cal.DayIndex + 1}", Palette.Good, () => { g.Grant(cal.Claim(today)); g.Sfx.Play(Sound.Coin); g.Dirty(); g.RefreshMap(); });
            else
                p.Text("Come back tomorrow for the next gift.", 36, Palette.Text);
            p.Button("Close", Palette.ButtonAlt, null);
        }

        public static void Album(GameRoot g, int world)
        {
            var p = new Popup(Layer, $"Album - World {world}");
            p.Text("Stickers come only from playing: every 10th level, star chests and daily puzzles.", 32);
            var grid = UIKit.Rect("stickers", p.Body);
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(180, 180);
            gl.spacing = new Vector2(16, 16);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 4;
            gl.childAlignment = TextAnchor.UpperCenter;
            grid.Size(3 * 180 + 2 * 16);
            int have = 0;
            for (int k = 0; k < Stickers.PerPage; k++)
            {
                bool got = g.Save.Stickers.Contains(Stickers.Id(world, k));
                if (got) have++;
                var tile = UIKit.Panel(grid, got ? Palette.Block[k % 6 + 1] : Palette.ButtonAlt, "sticker");
                var icon = UIKit.Icon(tile.transform, Sprites.Symbol(k % 6 + 1), got ? Color.white : new Color(1, 1, 1, 0.12f), 110);
                icon.rectTransform.At(0.5f, 0.5f, 110, 110);
            }
            p.Text($"{have}/{Stickers.PerPage} collected", 40, Palette.Text);
            var nav = UIKit.Rect("nav", p.Body);
            UIKit.HStack(nav, 20);
            nav.Size(130);
            int maxWorld = Mathf.Max(1, Progression.WorldOf(g.Save.Progress.NextLevel));
            var prev = UIKit.Button(nav, "<", Palette.ButtonAlt, () => { p.Close(); Album(g, world - 1); });
            prev.interactable = world > 1;
            var next = UIKit.Button(nav, ">", Palette.ButtonAlt, () => { p.Close(); Album(g, world + 1); });
            next.interactable = world < maxWorld;
            p.Button("Close", Palette.Button, null);
        }

        public static void NewMechanic(string title, string body, Action ok) => Info("New: " + title, body, "Got it", ok);

        public static void Toast(string text)
        {
            var label = UIKit.Label(Layer, text, 44, Color.white);
            var bg = label.gameObject.AddComponent<Outline>();
            bg.effectColor = new Color(0, 0, 0, 0.8f);
            bg.effectDistance = new Vector2(3, -3);
            var rt = label.rectTransform.Band(0, 120, 420, 60);
            Tweener.Run(2.2f, q =>
            {
                if (label == null) return;
                label.color = new Color(1, 1, 1, q < 0.8f ? 1f : (1f - q) / 0.2f);
                rt.anchoredPosition = new Vector2(0, 420 + 40 * q);
            }, Ease.Linear, 0f, () => { if (label != null) UnityEngine.Object.Destroy(label.gameObject); });
        }
    }
}
