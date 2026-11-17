using System.Collections.Generic;
using TiltDrop.Core;
using UnityEngine;

namespace TiltDrop.Game
{
    /// <summary>Runs one level attempt: input -> GameSession -> BoardView playback -> HUD, win/fail, boosters.</summary>
    public sealed class LevelController
    {
        static readonly Dictionary<int, (string title, string body)> Intros = new Dictionary<int, (string, string)>
        {
            [31] = ("Stone", "Stones fall like blocks but never leave the board. Steer around them!"),
            [61] = ("More gates", "A color can have several gates. Pick the one that works best."),
            [91] = ("Anchor", "Anchored blocks stay put until a block of the same color exits the board."),
            [121] = ("Ice", "Frozen blocks thaw after two hits from falling pieces."),
            [151] = ("Four colors", "More colors, more gates. Plan your rotations!"),
            [211] = ("Big boards", "Larger boards, longer cascades."),
            [241] = ("Five colors", "Five colors at once. Good luck!"),
        };

        static readonly string[] Tutorial =
        {
            null,
            "Swipe right (or tap the right arrow) to tilt the board clockwise",
            "Gravity follows every rotation. Rotate twice!",
            "Each rotation changes which way is down",
        };

        readonly GameRoot g;
        readonly int number;
        readonly bool isDaily, practice;
        readonly Level level;
        public GameSession Session { get; private set; }
        Hud hud;
        bool busy, ended, hammerMode;
        Rot? queued;
        float startTime;
        int attempt, freeUndo, freeHammer;
        bool dailyRecorded;
        readonly IReplayRecorder replay = new NoopReplayRecorder();

        public LevelController(GameRoot g, Level level, int number, bool isDaily, bool practice)
        {
            this.g = g;
            this.level = level;
            this.number = number;
            this.isDaily = isDaily;
            this.practice = practice;
        }

        BoardView Board => g.Board;

        public void Start()
        {
            Session = new GameSession(level, number, g.Config, isDaily);
            Board.gameObject.SetActive(true);
            Board.Build(Session.State);
            g.Input.Rotate += OnRotate;
            g.Input.Tap += OnTap;
            g.Input.Active = true;
            hud = new Hud(g.ScreenLayer, number, isDaily, level.Tier, Pause, OnRotate, OnUndo, OnHammer, OnHint);
            hud.SetMoves(Session.State.MovesLeft);
            if (!isDaily)
            {
                g.Save.Attempts.TryGetValue(number, out attempt);
                g.Save.Attempts[number] = ++attempt;
                (freeUndo, freeHammer) = g.Save.WinStreak.StartBoosters();
                if (freeUndo + freeHammer > 0) Popups.Toast($"Win streak bonus: {(freeUndo > 0 ? freeUndo + " free Undo " : "")}{(freeHammer > 0 ? freeHammer + " free Hammer" : "")}");
            }
            RefreshBoosters();
            startTime = Time.unscaledTime;
            g.Analytics.Log(AnalyticsEvents.LevelStart, AnalyticsEvents.Start(level, number, attempt));
            replay.Begin(level.Id, isDaily);
            if (!isDaily && number < Tutorial.Length && Tutorial[number] != null)
            {
                hud.Banner.text = Tutorial[number];
                hud.Pulse(hud.RotCw);
            }
            if (!isDaily && Intros.TryGetValue(number, out var intro) && PlayerPrefs.GetInt("intro_" + number, 0) == 0)
            {
                PlayerPrefs.SetInt("intro_" + number, 1);
                g.Input.Active = false;
                Popups.NewMechanic(intro.title, intro.body, () => g.Input.Active = true);
            }
            g.Dirty();
        }

        public void Dispose()
        {
            Tweener.Kill(this);
            g.Input.Rotate -= OnRotate;
            g.Input.Tap -= OnTap;
            hud?.Destroy();
            hud = null;
        }

        void RefreshBoosters() => hud?.SetBoosters(freeUndo + g.Save.Boosters.Undo, freeHammer + g.Save.Boosters.Hammer);

        // ---- moves --------------------------------------------------------------------------
        void OnRotate(Rot r)
        {
            if (ended || hud == null) return;
            if (hammerMode) { hammerMode = false; hud.Banner.text = ""; }
            if (busy) { queued = r; return; } // buffer one input during animation
            Do(r);
        }

        void Do(Rot r)
        {
            if (Session.IsOver) return;
            if (number <= 3 && !isDaily) hud.Banner.text = "";
            var ev = Session.Rotate(r);
            replay.Record(r == Rot.CW ? "CW" : "CCW");
            hud.SetMoves(Session.State.MovesLeft);
            float rot = Board.AnimateRotation(r);
            float lead = rot * 0.55f; // gravity kicks in before the rotation fully settles: feels snappier
            float total = lead + Board.PlayEvents(ev, Session.State.Gravity, lead);
            busy = true;
            Tweener.Delay(Mathf.Max(total, rot), AfterAnim, this);
        }

        void AfterAnim()
        {
            busy = false;
            var st = Session.State.Status;
            if (st == Status.Won) { queued = null; Win(); return; }
            if (st == Status.Lost) { queued = null; Fail(); return; }
            if (queued.HasValue)
            {
                var q = queued.Value;
                queued = null;
                Do(q);
            }
        }

        // ---- boosters -----------------------------------------------------------------------
        bool TakeCharge(Booster b, System.Action then)
        {
            ref int free = ref (b == Booster.Undo ? ref freeUndo : ref freeHammer);
            if (free > 0) { free--; then(); return true; }
            if (g.Save.Boosters.TryConsume(b)) { then(); return true; }
            int price = BoosterInventory.Price(g.Config, b);
            Popups.Confirm(b.ToString(), $"Use {b} for {price} coins ({StoreCatalog.ApproxEur(price)})?", $"Buy for {price}", () =>
            {
                if (g.Wallet.TrySpend(price)) then();
                else if (g.Privacy.Purchases) Popups.Store(g);
                else Popups.Toast("Not enough coins - win levels to earn more!");
            }, "No thanks");
            return false;
        }

        void OnUndo()
        {
            if (busy || ended || !Session.CanUndo) { Board.Nudge(); return; }
            TakeCharge(Booster.Undo, () =>
            {
                Session.Undo();
                Board.Build(Session.State);
                hud.SetMoves(Session.State.MovesLeft);
                g.Sfx.Play(Sound.Whoosh, 0.6f, 0.8f);
                g.Analytics.Log(AnalyticsEvents.BoosterUse, new Dictionary<string, object> { ["type"] = "undo", ["level"] = number });
                RefreshBoosters();
                g.Dirty();
            });
        }

        void OnHammer()
        {
            if (busy || ended || !Session.CanHammer) { Board.Nudge(); return; }
            hammerMode = !hammerMode;
            hud.Banner.text = hammerMode ? "Tap a block or stone to smash it" : "";
        }

        void OnTap(Vector2 screen)
        {
            if (!hammerMode || busy || ended) return;
            var world = g.Cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -g.Cam.transform.position.z));
            if (!Board.TryCellAt(world, out int x, out int y)) return;
            if (!Cells.IsPiece(Session.State.Cells[y * level.W + x])) { Board.Nudge(); return; }
            hammerMode = false;
            hud.Banner.text = "";
            TakeCharge(Booster.Hammer, () =>
            {
                var ev = Session.Hammer(x, y);
                replay.Record($"H:{x},{y}");
                busy = true;
                float dur = Board.PlayEvents(ev, Session.State.Gravity, 0f);
                Tweener.Delay(dur + 0.05f, AfterAnim, this);
                g.Analytics.Log(AnalyticsEvents.BoosterUse, new Dictionary<string, object> { ["type"] = "hammer", ["level"] = number });
                RefreshBoosters();
                g.Dirty();
            });
        }

        void OnHint()
        {
            if (busy || ended) return;
            g.ShowRewarded("hint", ok =>
            {
                if (!ok || hud == null) return;
                if (Solver.TryHint(Session.State, out int min, out Rot first))
                {
                    hud.Pulse(first == Rot.CW ? hud.RotCw : hud.RotCcw);
                    hud.Banner.text = (first == Rot.CW ? "Rotate right" : "Rotate left") + (min <= Session.State.MovesLeft ? $" - {min} moves to win" : "");
                }
                else hud.Banner.text = "No win within your moves - try Undo or Retry";
            });
        }

        void Pause()
        {
            if (ended) return;
            g.Input.Active = false;
            Popups.Pause(g, () => g.Input.Active = true, () => { EndDailyAttempt(0); g.Restart(); }, () => { EndDailyAttempt(0); g.ShowMap(); });
        }

        // ---- end of level -------------------------------------------------------------------
        int DurationS => Mathf.RoundToInt(Time.unscaledTime - startTime);

        void Win()
        {
            ended = true;
            g.Input.Active = false;
            int stars = Session.EarnedStars;
            int coins = isDaily ? g.Config.DailyPuzzleReward : Session.CoinReward;
            string sticker = null;
            g.Analytics.Log(AnalyticsEvents.LevelWin, AnalyticsEvents.Win(number, attempt, Session.State.MovesUsed, stars, Session.Continues, DurationS));
            replay.End("won");
            int today = Days.Of(g.Clock.LocalNow);
            if (isDaily)
            {
                if (!practice && !dailyRecorded)
                {
                    dailyRecorded = true;
                    g.Save.DailyResults[today] = stars;
                    g.Save.DailyStreak.OnSolved(today);
                    int world = Progression.WorldOf(g.Save.Progress.NextLevel);
                    if (g.Save.DailySolvedWorld != world) { g.Save.DailySolvedWorld = world; g.Save.DailySolvedInWorld = 0; }
                    sticker = Stickers.ForDailySolved(world, ++g.Save.DailySolvedInWorld);
                    g.LastDailyShare = ShareGrid.Build(DailyPuzzle.Number(g.Catalog.DailyEpochDay, today), stars, Session.State.MovesUsed, level.Moves, Session.ExitsPerMove);
                    g.Analytics.Log(AnalyticsEvents.DailyPuzzle, new Dictionary<string, object> { ["n"] = DailyPuzzle.Number(g.Catalog.DailyEpochDay, today), ["result"] = "win", ["moves"] = Session.State.MovesUsed });
                }
                else coins = 0;
            }
            else
            {
                bool first = g.Save.Progress.RecordWin(number, stars);
                g.Save.WinStreak.OnWin(Session.Continues > 0);
                g.Save.Piggy.OnLevelReward(g.Config, coins);
                g.Pacing.OnLevelWon();
                if (first) sticker = Stickers.ForLevelComplete(number);
            }
            if (sticker != null) g.Save.Stickers.Add(sticker);
            if (coins > 0) g.Wallet.Add(coins);
            g.Dirty();
            g.SaveNow();
            Board.Celebrate();
            g.Sfx.Play(Sound.Win);
            Haptics.Play(Haptic.Success);
            bool hasNext = !isDaily && number < g.Catalog.LevelCount;
            Tweener.Delay(0.55f, () => Popups.Win(g, Session, coins, sticker,
                hasNext ? () => g.AfterWin(number, () => g.StartLevel(number + 1)) : (System.Action)null,
                () => { if (isDaily) g.ShowMap(); else g.AfterWin(number, g.ShowMap); },
                isDaily || coins == 0 ? (System.Action)null : () => { g.Wallet.Add(coins); g.Dirty(); }), this);
        }

        void Fail()
        {
            ended = true;
            g.Input.Active = false;
            g.Analytics.Log(AnalyticsEvents.LevelFail, AnalyticsEvents.Fail(number, attempt, Session.State.MovesUsed, Session.State.CountColored()));
            if (!isDaily) g.Save.WinStreak.OnFail();
            g.Sfx.Play(Sound.Fail);
            Haptics.Play(Haptic.Failure);
            g.Dirty();
            Tweener.Delay(0.4f, ShowFail, this);
        }

        void ShowFail()
        {
            Popups.Fail(g, Session,
                () => g.ShowRewarded("continue", ok => { if (ok) Continue("ad"); else ShowFail(); }),
                () => { if (g.Wallet.TrySpend(g.Config.ContinueCost)) Continue("coins"); else ShowFail(); },
                () => { replay.End("lost"); EndDailyAttempt(0); g.Restart(); },
                () => { replay.End("lost"); EndDailyAttempt(0); g.ShowMap(); });
        }

        void Continue(string method)
        {
            Session.Continue();
            ended = false;
            g.Input.Active = true;
            hud.SetMoves(Session.State.MovesLeft);
            g.Analytics.Log(AnalyticsEvents.LevelContinue, AnalyticsEvents.Continue(number, method, Session.Continues));
        }

        /// <summary>The daily puzzle has one scored attempt: leaving it unsolved records a loss.</summary>
        void EndDailyAttempt(int stars)
        {
            if (!isDaily || practice || dailyRecorded) return;
            dailyRecorded = true;
            int today = Days.Of(g.Clock.LocalNow);
            g.Save.DailyResults[today] = stars;
            g.LastDailyShare = ShareGrid.Build(DailyPuzzle.Number(g.Catalog.DailyEpochDay, today), 0, Session.State.MovesUsed, level.Moves, Session.ExitsPerMove);
            g.Analytics.Log(AnalyticsEvents.DailyPuzzle, new Dictionary<string, object> { ["n"] = DailyPuzzle.Number(g.Catalog.DailyEpochDay, today), ["result"] = "loss" });
            g.Dirty();
        }
    }
}
