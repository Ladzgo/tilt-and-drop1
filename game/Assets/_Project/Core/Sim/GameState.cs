using System;
using System.Collections.Generic;

namespace TiltDrop.Core
{
    public enum Status : byte { Playing, Won, Lost }

    /// <summary>Stateful game session rules (mirrors sim.Game in Python). Cloneable for undo.</summary>
    public sealed class GameState
    {
        public readonly Level Level;
        public readonly Topology Topo;
        public byte[] Cells;
        public int[] Ids;
        public Dir Gravity;
        public int MovesLeft;
        public int MovesUsed;
        public Status Status;

        public GameState(Level level)
        {
            Level = level;
            Topo = Topology.Get(level.W, level.H);
            Cells = (byte[])level.Cells.Clone();
            Ids = Settler.InitialIds(level.Cells);
            Gravity = Dir.Down;
            MovesLeft = level.Moves;
            UpdateStatus();
        }

        GameState(GameState o)
        {
            Level = o.Level; Topo = o.Topo;
            Cells = (byte[])o.Cells.Clone(); Ids = (int[])o.Ids.Clone();
            Gravity = o.Gravity; MovesLeft = o.MovesLeft; MovesUsed = o.MovesUsed; Status = o.Status;
        }

        public GameState Clone() => new GameState(this);

        void UpdateStatus()
        {
            if (!Settler.HasColored(Cells)) Status = Status.Won;
            else if (MovesLeft <= 0) Status = Status.Lost;
            else Status = Status.Playing;
        }

        List<SettleEvent> SettleNow()
        {
            var ev = new List<SettleEvent>();
            Settler.Settle(Cells, Topo, Gravity, Level.Gates[Dirs.GravityEdge(Gravity)], Ids, ev);
            return ev;
        }

        public List<SettleEvent> Rotate(Rot r)
        {
            if (Status != Status.Playing) throw new InvalidOperationException($"cannot rotate: game is {Status}");
            Gravity = Dirs.Rotate(Gravity, r);
            MovesLeft--;
            MovesUsed++;
            var ev = SettleNow();
            UpdateStatus();
            return ev;
        }

        public List<SettleEvent> Hammer(int x, int y)
        {
            if (Status != Status.Playing) throw new InvalidOperationException($"cannot hammer: game is {Status}");
            if (x < 0 || x >= Level.W || y < 0 || y >= Level.H) throw new ArgumentException("hammer target outside board");
            int i = y * Level.W + x;
            if (!TiltDrop.Core.Cells.IsPiece(Cells[i])) throw new ArgumentException("hammer target must be a piece");
            int removed = Ids[i];
            Cells[i] = TiltDrop.Core.Cells.Empty;
            Ids[i] = 0;
            var ev = new List<SettleEvent> { new SettleEvent { Type = EventType.Hammer, PieceId = removed, From = i } };
            ev.AddRange(SettleNow());
            UpdateStatus();
            return ev;
        }

        /// <summary>Continue (+n moves) after a loss.</summary>
        public void AddMoves(int n)
        {
            if (Status == Status.Won) throw new InvalidOperationException("cannot continue a won game");
            MovesLeft += n;
            UpdateStatus();
        }

        public int CountColored()
        {
            int n = 0;
            foreach (var c in Cells) if (TiltDrop.Core.Cells.IsColored(c)) n++;
            return n;
        }

        public string[] Rows() => Level.Rows(Cells, Level.W, Level.H);
    }
}
