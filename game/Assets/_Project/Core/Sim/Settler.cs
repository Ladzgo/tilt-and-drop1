using System.Collections.Generic;

namespace TiltDrop.Core
{
    /// <summary>Normative settle algorithm (docs/GDD.md §2.4). Mirrors tools/levelgen/tiltdrop/sim.py.</summary>
    public static class Settler
    {
        /// <summary>Settles cells in place under gravity g. ids/events may be null. Returns number of exits.</summary>
        public static int Settle(byte[] cells, Topology topo, Dir g, byte[] gateLine, int[] ids = null, List<SettleEvent> events = null)
        {
            int gi = (int)g;
            int[] order = topo.Order[gi], next = topo.Next[gi], lane = topo.Lane[gi];
            int edge = Dirs.GravityEdge(g);
            int n = cells.Length;
            int exits = 0, pass = 0;
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int oi = 0; oi < order.Length; oi++)
                {
                    int p = order[oi];
                    byte c = cells[p];
                    int k = c & 0xF0;
                    if (k != Cells.Block && k != Cells.Stone) continue;
                    int pos = p;
                    while (true)
                    {
                        int nx = next[pos];
                        if (nx < 0 || cells[nx] != Cells.Empty) break;
                        pos = nx;
                    }
                    bool moved = pos != p;
                    if (moved)
                    {
                        cells[pos] = c;
                        cells[p] = Cells.Empty;
                        changed = true;
                        if (ids != null) { ids[pos] = ids[p]; ids[p] = 0; }
                        events?.Add(new SettleEvent { Type = EventType.Move, Pass = pass, PieceId = ids != null ? ids[pos] : 0, From = p, To = pos });
                    }
                    int nn = next[pos];
                    if (nn < 0)
                    {
                        int col = c & 0x0F;
                        if (k == Cells.Block && gateLine[lane[pos]] == col)
                        {
                            cells[pos] = Cells.Empty;
                            changed = true;
                            exits++;
                            int pid = 0;
                            if (ids != null) { pid = ids[pos]; ids[pos] = 0; }
                            events?.Add(new SettleEvent { Type = EventType.Exit, Pass = pass, PieceId = pid, Edge = edge, Lane = lane[pos] });
                            byte anchor = (byte)(Cells.Anchor | col);
                            for (int i = 0; i < n; i++)
                            {
                                if (cells[i] != anchor) continue;
                                cells[i] = (byte)(Cells.Block | col);
                                events?.Add(new SettleEvent { Type = EventType.Release, Pass = pass, PieceId = ids != null ? ids[i] : 0 });
                            }
                        }
                    }
                    else if (moved)
                    {
                        byte t = cells[nn];
                        int tk = t & 0xF0;
                        if (tk == Cells.Ice2 || tk == Cells.Ice1)
                        {
                            int hp;
                            if (tk == Cells.Ice2) { cells[nn] = (byte)(Cells.Ice1 | (t & 0x0F)); hp = 1; }
                            else { cells[nn] = (byte)(Cells.Block | (t & 0x0F)); hp = 0; }
                            events?.Add(new SettleEvent { Type = EventType.Hit, Pass = pass, PieceId = ids != null ? ids[nn] : 0, Hp = hp });
                        }
                    }
                }
                pass++;
            }
            return exits;
        }

        public static bool HasColored(byte[] cells)
        {
            for (int i = 0; i < cells.Length; i++) if (Cells.IsColored(cells[i])) return true;
            return false;
        }

        public static bool IsStable(Level lv, Dir g = Dir.Down)
        {
            var buf = (byte[])lv.Cells.Clone();
            Settle(buf, Topology.Get(lv.W, lv.H), g, lv.Gates[Dirs.GravityEdge(g)]);
            for (int i = 0; i < buf.Length; i++) if (buf[i] != lv.Cells[i]) return false;
            return true;
        }

        /// <summary>Piece ids: 1.. in row-major order over every non-empty, non-wall cell.</summary>
        public static int[] InitialIds(byte[] cells)
        {
            var ids = new int[cells.Length];
            int next = 1;
            for (int i = 0; i < cells.Length; i++) if (Cells.IsPiece(cells[i])) ids[i] = next++;
            return ids;
        }
    }
}
