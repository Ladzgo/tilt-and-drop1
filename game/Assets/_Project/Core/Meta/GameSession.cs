using System;
using System.Collections.Generic;

namespace TiltDrop.Core
{
    /// <summary>One attempt at a level: rules + undo history, continues, boosters, share data.</summary>
    public sealed class GameSession
    {
        public const int MaxUndoDepth = 30;

        public readonly int LevelNumber;
        public readonly bool IsDaily;
        public readonly GameConfig Config;
        public GameState State { get; private set; }
        public Level Level => State.Level;
        public readonly List<int> ExitsPerMove = new List<int>();
        public int Continues { get; private set; }
        public int UndoUsed { get; private set; }
        public int HammerUsed { get; private set; }

        readonly List<GameState> history = new List<GameState>();

        public GameSession(Level level, int levelNumber, GameConfig config, bool isDaily = false)
        {
            LevelNumber = levelNumber;
            IsDaily = isDaily;
            Config = config;
            State = new GameState(level);
        }

        public bool IsOver => State.Status != Status.Playing;

        public List<SettleEvent> Rotate(Rot r)
        {
            history.Add(State.Clone());
            if (history.Count > MaxUndoDepth) history.RemoveAt(0);
            var ev = State.Rotate(r);
            int exits = 0;
            foreach (var e in ev) if (e.Type == SettleEventType.Exit) exits++;
            ExitsPerMove.Add(exits);
            return ev;
        }

        public bool CanUndo => !IsDaily && history.Count > 0 && State.Status == Status.Playing;

        /// <summary>Restores the exact previous state, including the move count (the move is refunded).</summary>
        public void Undo()
        {
            if (!CanUndo) throw new InvalidOperationException("nothing to undo");
            State = history[history.Count - 1];
            history.RemoveAt(history.Count - 1);
            if (ExitsPerMove.Count > 0) ExitsPerMove.RemoveAt(ExitsPerMove.Count - 1);
            UndoUsed++;
        }

        public bool CanHammer => !IsDaily && State.Status == Status.Playing;

        public List<SettleEvent> Hammer(int x, int y)
        {
            if (!CanHammer) throw new InvalidOperationException("hammer not available");
            var ev = State.Hammer(x, y);
            history.Clear(); // undo never crosses a hammer
            HammerUsed++;
            return ev;
        }

        public bool CanContinue => State.Status == Status.Lost && Continues < Config.MaxContinues;

        public void Continue()
        {
            if (!CanContinue) throw new InvalidOperationException("continue not available");
            State.AddMoves(Config.ContinueMoves);
            Continues++;
        }

        public int EarnedStars => State.Status == Status.Won ? Stars.Compute(State.MovesUsed, Level.Par, Level.Moves, Continues) : 0;
        public int CoinReward => State.Status == Status.Won ? Rewards.LevelWin(Config, EarnedStars, State.MovesLeft) : 0;
    }
}
