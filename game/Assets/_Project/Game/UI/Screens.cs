using System;
using System.Collections.Generic;
using TiltDrop.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TiltDrop.Game
{
    /// <summary>Neutral age screen (COMPLIANCE §3): no default, no hint about the threshold.</summary>
    public sealed class AgeGateScreen
    {
        readonly RectTransform root;
        int year = -1, month = -1;
        readonly Action<AgeBand> done;
        readonly DateTime today;
        Button cont;
        RectTransform monthPanel;
        readonly List<Image> yearButtons = new List<Image>();
        readonly List<Image> monthButtons = new List<Image>();

        public AgeGateScreen(Transform parent, DateTime today, Action<AgeBand> done)
        {
            this.done = done;
            this.today = today;
            root = UIKit.Rect("agegate", parent).Stretch();
            root.gameObject.AddComponent<Image>().color = Palette.Background;
            var col = UIKit.Rect("col", root).Stretch(60, 140, 60, 80);
            UIKit.VStack(col, 24);
            UIKit.Label(col, "Welcome to Tilt & Drop", 66, Palette.Text, TextAnchor.MiddleCenter, true).Size(110);
            UIKit.Label(col, "When were you born?", 50, Palette.TextDim).Size(80);

            // year grid in a scroll view (newest first), nothing preselected
            var scroll = MakeScroll(col, 760);
            var grid = scroll.content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(220, 110);
            grid.spacing = new Vector2(16, 16);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.UpperCenter;
            for (int y = today.Year; y >= today.Year - 100; y--)
            {
                int yy = y;
                var b = UIKit.Button(scroll.content, y.ToString(), Palette.ButtonAlt, () => PickYear(yy), 40, 110);
                yearButtons.Add((Image)b.targetGraphic);
            }

            monthPanel = UIKit.Rect("months", col);
            var mg = monthPanel.gameObject.AddComponent<GridLayoutGroup>();
            mg.cellSize = new Vector2(220, 96);
            mg.spacing = new Vector2(16, 12);
            mg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            mg.constraintCount = 4;
            mg.childAlignment = TextAnchor.UpperCenter;
            string[] names = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            for (int m = 1; m <= 12; m++)
            {
                int mm = m;
                var b = UIKit.Button(monthPanel, names[m - 1], Palette.ButtonAlt, () => PickMonth(mm), 38, 96);
                monthButtons.Add((Image)b.targetGraphic);
            }
            monthPanel.Size(330);
            monthPanel.gameObject.SetActive(false);

            cont = UIKit.Button(col, "Continue", Palette.Button, Submit);
            cont.interactable = false;
        }

        static ScrollRect MakeScroll(Transform parent, float height)
        {
            var view = UIKit.Rect("scroll", parent);
            view.Size(height);
            view.gameObject.AddComponent<RectMask2D>();
            var sr = view.gameObject.AddComponent<ScrollRect>();
            var content = UIKit.Rect("content", view);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = content;
            sr.viewport = view;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            return sr;
        }

        bool NeedsMonth(int y) => y == today.Year - PrivacyRules.ConsentAge || y == today.Year - 13;

        void PickYear(int y)
        {
            year = y;
            month = -1;
            for (int i = 0; i < yearButtons.Count; i++) yearButtons[i].color = today.Year - i == y ? Palette.Button : Palette.ButtonAlt;
            foreach (var b in monthButtons) b.color = Palette.ButtonAlt;
            monthPanel.gameObject.SetActive(NeedsMonth(y));
            cont.interactable = !NeedsMonth(y);
        }

        void PickMonth(int m)
        {
            month = m;
            for (int i = 0; i < monthButtons.Count; i++) monthButtons[i].color = i + 1 == m ? Palette.Button : Palette.ButtonAlt;
            cont.interactable = true;
        }

        void Submit()
        {
            // when the month does not matter, use December (the conservative choice: youngest possible age)
            var band = PrivacyRules.FromBirth(year, month > 0 ? month : 12, today);
            UnityEngine.Object.Destroy(root.gameObject);
            done(band);
        }
    }

    /// <summary>World map: worlds of 50 levels, stars, chests, daily/calendar/album/store entry points.</summary>
    public sealed class MapScreen
    {
        public readonly RectTransform Root;
        readonly GameRoot g;
        Text coins;

        public MapScreen(GameRoot g, Transform parent)
        {
            this.g = g;
            Root = UIKit.Rect("map", parent).Stretch();
            BuildTop();
            BuildFeatures();
            BuildList();
            int next = Mathf.Min(g.Save.Progress.NextLevel, g.Catalog.LevelCount);
            var play = UIKit.Button(Root, $"Play  Level {next}", Palette.Good, () => g.StartLevel(next), 60, 170);
            ((RectTransform)play.transform).Band(0, 170, 40, 60);
            g.Wallet.Changed += OnCoins;
        }

        public void Destroy()
        {
            g.Wallet.Changed -= OnCoins;
            UnityEngine.Object.Destroy(Root.gameObject);
        }

        void OnCoins(int balance, int delta) { if (coins != null) coins.text = balance.ToString("N0"); }

        void BuildTop()
        {
            var bar = UIKit.Rect("top", Root).Band(1, 140, -20, 40);
            UIKit.HStack(bar, 20);
            var pill = UIKit.Panel(bar, Palette.ButtonAlt, "coins");
            var prt = pill.rectTransform;
            var h = UIKit.HStack(prt, 12, 14, TextAnchor.MiddleLeft);
            h.childForceExpandWidth = false;
            UIKit.Icon(prt, Sprites.Circle, Palette.Gold, 70);
            coins = UIKit.Label(prt, g.Wallet.Coins.ToString("N0"), 50, Palette.Text, TextAnchor.MiddleLeft, true);
            coins.Size(-1, 260);
            pill.Size(-1, 400, 1);
            if (g.Privacy.Purchases) UIKit.Button(bar, "Shop", Palette.Gold, () => Popups.Store(g), 44, 140).Size(-1, 210, 0);
            UIKit.Button(bar, "Settings", Palette.ButtonAlt, () => Popups.Settings(g), 40, 140).Size(-1, 250, 0);
        }

        void BuildFeatures()
        {
            var row = UIKit.Rect("features", Root).Band(1, 150, -180, 40);
            UIKit.HStack(row, 20);
            int today = Days.Of(g.Clock.LocalNow);
            int n = g.Catalog.DailyCount > 0 ? DailyPuzzle.Number(g.Catalog.DailyEpochDay, today) : 0;
            bool played = g.Save.DailyResults.ContainsKey(today);
            UIKit.Button(row, played ? $"Daily #{n} - done" : $"Daily #{n}", played ? Palette.ButtonAlt : Palette.Button, () => Popups.Daily(g), 42, 150);
            bool claim = g.Save.Calendar.CanClaim(today);
            UIKit.Button(row, claim ? "Gift !" : "Calendar", claim ? Palette.Gold : Palette.ButtonAlt, () => Popups.Calendar(g), 42, 150);
            UIKit.Button(row, "Album", Palette.ButtonAlt, () => Popups.Album(g, Progression.WorldOf(g.Save.Progress.NextLevel)), 42, 150);
        }

        void BuildList()
        {
            var view = UIKit.Rect("list", Root).Stretch(30, 350, 30, 240);
            view.gameObject.AddComponent<RectMask2D>();
            var sr = view.gameObject.AddComponent<ScrollRect>();
            var content = UIKit.Rect("content", view);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            UIKit.VStack(content, 30, 10);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = content;
            sr.viewport = view;
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Elastic;

            var prog = g.Save.Progress;
            int next = prog.NextLevel;
            int cw = Progression.WorldOf(next);
            int maxWorld = (g.Catalog.LevelCount - 1) / Progression.WorldSize + 1;
            int firstWorld = Mathf.Max(1, cw - 2), lastWorld = Mathf.Min(maxWorld, cw + 1);
            int rowsBefore = 0, totalRows = 0;
            for (int w = firstWorld; w <= lastWorld; w++)
            {
                BuildWorld(content, w, next);
                totalRows += 11;
                if (w < cw) rowsBefore += 11;
                else if (w == cw) rowsBefore += 1 + ((next - 1) % Progression.WorldSize) / 5;
            }
            Canvas.ForceUpdateCanvases();
            sr.verticalNormalizedPosition = Mathf.Clamp01(1f - (rowsBefore - 2f) / Mathf.Max(1, totalRows - 6));
        }

        void BuildWorld(RectTransform content, int w, int next)
        {
            var prog = g.Save.Progress;
            var head = UIKit.Panel(content, Palette.Panel, "world" + w);
            head.Size(150);
            var hrt = head.rectTransform;
            var hh = UIKit.HStack(hrt, 16, 20);
            hh.childForceExpandWidth = false;
            UIKit.Label(hrt, $"World {w}", 50, Palette.Text, TextAnchor.MiddleLeft, true).Size(-1, 300);
            UIKit.Label(hrt, $"{prog.WorldStars(w)}/150", 40, Palette.Gold, TextAnchor.MiddleLeft).Size(-1, 170);
            UIKit.Icon(hrt, Sprites.Star, Palette.Gold, 44);
            for (int c = 0; c < 3; c++)
            {
                int ci = c;
                bool can = prog.CanClaimChest(w, c);
                bool claimed = ((prog.ChestsClaimed.TryGetValue(w, out int m) ? m : 0) & (1 << c)) != 0;
                var b = UIKit.Button(hrt, claimed ? "OK" : Progression.ChestThresholds[c].ToString(), can ? Palette.Gold : Palette.ButtonAlt, () => g.ClaimChest(w, ci), 34, 100);
                b.Size(100, 120, 0);
                b.interactable = can;
            }

            var gridRt = UIKit.Rect("grid", content);
            var grid = gridRt.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(176, 176);
            grid.spacing = new Vector2(22, 22);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.childAlignment = TextAnchor.UpperCenter;
            gridRt.Size(10 * 176 + 9 * 22);
            for (int i = 1; i <= Progression.WorldSize; i++)
            {
                int n = (w - 1) * Progression.WorldSize + i;
                if (n > g.Catalog.LevelCount) break;
                bool unlocked = prog.IsUnlocked(n);
                var color = n == next ? Palette.Good : unlocked ? Palette.Button : Palette.ButtonAlt;
                var b = UIKit.Button(gridRt, unlocked ? n.ToString() : "", color, () => g.StartLevel(n), 44, 176);
                b.interactable = unlocked;
                if (unlocked && n < next)
                {
                    var st = UIKit.StarRow(b.transform, prog.StarsOf(n), 34, 2);
                    st.At(0.5f, 0f, 120, 36, 0, 12);
                    var label = b.GetComponentInChildren<Text>();
                    label.rectTransform.Stretch(4, 4, 4, 30);
                }
            }
        }
    }

    /// <summary>In-level HUD: moves, level title, rotate buttons, boosters.</summary>
    public sealed class Hud
    {
        public readonly RectTransform Root;
        public Text Moves, Title, Banner;
        public Button Undo, Hammer, Hint, RotCw, RotCcw;

        public Hud(Transform parent, int levelNumber, bool isDaily, Tier tier, Action pause, Action<Rot> rotate, Action undo, Action hammer, Action hint)
        {
            Root = UIKit.Rect("hud", parent).Stretch();
            var top = UIKit.Rect("top", Root).Band(1, 190, -10, 30);
            var pauseBtn = UIKit.Button(top, "II", Palette.ButtonAlt, pause, 50, 130);
            ((RectTransform)pauseBtn.transform).At(0, 0.5f, 130, 130);
            string tierText = tier == Tier.SH ? "  <color=#e5484d>Super hard</color>" : tier == Tier.H ? "  <color=#ffc53d>Hard</color>" : "";
            Title = UIKit.Label(top, (isDaily ? "Daily puzzle" : $"Level {levelNumber}") + tierText, 52, Palette.Text, TextAnchor.MiddleCenter, true);
            Title.rectTransform.At(0.5f, 0.5f, 640, 120);
            var mp = UIKit.Panel(top, Palette.ButtonAlt, "moves");
            mp.rectTransform.At(1, 0.5f, 210, 170);
            Moves = UIKit.Label(mp.transform, "0", 84, Palette.Text, TextAnchor.MiddleCenter, true);
            Moves.rectTransform.Stretch(0, 0, 0, 40);
            UIKit.Label(mp.transform, "moves", 30, Palette.TextDim).rectTransform.Stretch(0, 112, 0, 6);

            Banner = UIKit.Label(Root, "", 44, Palette.Text);
            Banner.rectTransform.Band(1, 120, -220, 40);

            var bottom = UIKit.Rect("bottom", Root).Band(0, 330, 30, 30);
            RotCcw = UIKit.Button(bottom, "", Palette.Button, () => rotate(Rot.CCW), 40, 220);
            ((RectTransform)RotCcw.transform).At(0, 0, 220, 220);
            var iccw = UIKit.Icon(RotCcw.transform, Sprites.ArcArrow, Color.white, 150);
            iccw.rectTransform.localScale = new Vector3(-1, 1, 1);
            RotCw = UIKit.Button(bottom, "", Palette.Button, () => rotate(Rot.CW), 40, 220);
            ((RectTransform)RotCw.transform).At(1, 0, 220, 220);
            UIKit.Icon(RotCw.transform, Sprites.ArcArrow, Color.white, 150);

            var boosters = UIKit.Rect("boosters", bottom);
            boosters.anchorMin = new Vector2(0, 0);
            boosters.anchorMax = new Vector2(1, 0);
            boosters.pivot = new Vector2(0.5f, 0);
            boosters.offsetMin = new Vector2(250, 0);
            boosters.offsetMax = new Vector2(-250, 150);
            UIKit.HStack(boosters, 14);
            if (!isDaily)
            {
                Undo = UIKit.Button(boosters, "Undo", Palette.ButtonAlt, undo, 34, 150);
                Hammer = UIKit.Button(boosters, "Hammer", Palette.ButtonAlt, hammer, 34, 150);
                Hint = UIKit.Button(boosters, "Hint (ad)", Palette.ButtonAlt, hint, 34, 150);
            }
        }

        public void SetMoves(int moves)
        {
            Moves.text = moves.ToString();
            Moves.color = moves <= 2 ? Palette.Block[1] : Palette.Text;
            var t = Moves.transform;
            Tweener.Run(0.2f, p => t.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(p * Mathf.PI)), Ease.Linear);
        }

        public void SetBoosters(int undo, int hammer)
        {
            if (Undo != null) Undo.SetText(undo > 0 ? $"Undo ×{undo}" : "Undo +");
            if (Hammer != null) Hammer.SetText(hammer > 0 ? $"Hammer ×{hammer}" : "Hammer +");
        }

        public void Pulse(Button b)
        {
            if (b == null) return;
            var t = b.transform;
            Tweener.Run(1.2f, p => t.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(p * Mathf.PI * 4f) * (1f - p)), Ease.Linear);
        }

        public void Destroy() => UnityEngine.Object.Destroy(Root.gameObject);
    }
}
