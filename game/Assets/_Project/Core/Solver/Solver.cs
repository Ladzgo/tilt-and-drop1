using System;
using System.Collections.Generic;

namespace TiltDrop.Core
{
    /// <summary>Hashable (cells, gravity) state key. Piece ids are not part of the state.</summary>
    public readonly struct StateKey : IEquatable<StateKey>
    {
        readonly byte[] data;
        readonly int hash;

        public StateKey(byte[] cells, Dir g)
        {
            data = new byte[cells.Length + 1];
            Buffer.BlockCopy(cells, 0, data, 0, cells.Length);
            data[cells.Length] = (byte)g;
            unchecked
            {
                uint h = 2166136261;
                for (int i = 0; i < data.Length; i++) h = (h ^ data[i]) * 16777619;
                hash = (int)h;
            }
        }

        public bool Equals(StateKey o)
        {
            if (hash != o.hash || data.Length != o.data.Length) return false;
            for (int i = 0; i < data.Length; i++) if (data[i] != o.data[i]) return false;
            return true;
        }

        public override bool Equals(object obj) => obj is StateKey k && Equals(k);
        public override int GetHashCode() => hash;
    }

    /// <summary>First-visit BFS state graph (mirrors tiltdrop/solver.py).</summary>
    public sealed class SolverGraph
    {
        public readonly List<byte[]> Cells = new List<byte[]>();
        public readonly List<Dir> Grav = new List<Dir>();
        public readonly List<int> Depth = new List<int>();
        public readonly List<bool> Win = new List<bool>();
        public readonly List<int> ChildCw = new List<int>();
        public readonly List<int> ChildCcw = new List<int>();
        public readonly Dictionary<StateKey, int> Index = new Dictionary<StateKey, int>();
        public int ExpandedDepth = -1;
        public bool Truncated;

        public int Count => Cells.Count;

        public int Add(byte[] cells, Dir g, int d)
        {
            var key = new StateKey(cells, g);
            if (Index.TryGetValue(key, out int i)) return i;
            i = Cells.Count;
            Index[key] = i;
            Cells.Add(cells); Grav.Add(g); Depth.Add(d);
            Win.Add(!Settler.HasColored(cells));
            ChildCw.Add(-1); ChildCcw.Add(-1);
            return i;
        }

        public int MinWinDepth()
        {
            int best = -1;
            for (int i = 0; i < Count; i++) if (Win[i] && (best < 0 || Depth[i] < best)) best = Depth[i];
            return best;
        }
    }

    public struct SolveResult
    {
        public bool Solvable;
        public int MinMoves;          // -1 if unsolvable
        public long SolutionsAtMin;
        public int Limit;
        public double DeadEndRatio;
        public double RandomWin;
        public Rot FirstMove;
        public int States;
        public bool Truncated;
    }

    public static class Solver
    {
        public static SolverGraph Explore(Level lv, int maxDepth, int extraAfterWin, int nodeCap, byte[] startCells = null, Dir startG = Dir.Down)
        {
            var gr = new SolverGraph();
            var topo = Topology.Get(lv.W, lv.H);
            gr.Add((byte[])(startCells ?? lv.Cells).Clone(), startG, 0);
            var frontier = new List<int> { 0 };
            int winDepth = gr.Win[0] ? 0 : -1;
            int d = 0;
            while (frontier.Count > 0 && d < maxDepth)
            {
                if (winDepth >= 0 && d >= winDepth + extraAfterWin) break;
                if (gr.Count > nodeCap) { gr.Truncated = true; break; }
                int nextStart = gr.Count;
                foreach (int i in frontier)
                {
                    if (gr.Win[i]) continue;
                    for (int r = 0; r < 2; r++)
                    {
                        Dir ng = Dirs.Rotate(gr.Grav[i], (Rot)r);
                        var buf = (byte[])gr.Cells[i].Clone();
                        Settler.Settle(buf, topo, ng, lv.Gates[Dirs.GravityEdge(ng)]);
                        int c = gr.Add(buf, ng, gr.Depth[i] + 1);
                        if (r == 0) gr.ChildCw[i] = c; else gr.ChildCcw[i] = c;
                    }
                }
                gr.ExpandedDepth = d;
                frontier = new List<int>();
                for (int i = nextStart; i < gr.Count; i++) frontier.Add(i);
                d++;
                if (winDepth < 0) foreach (int i in frontier) if (gr.Win[i]) { winDepth = d; break; }
            }
            return gr;
        }

        public static long CountSolutions(SolverGraph gr, int minMoves)
        {
            var paths = new long[gr.Count];
            paths[0] = 1;
            // states were added in BFS order -> index order is depth order
            long total = 0;
            for (int i = 0; i < gr.Count; i++)
            {
                if (gr.Win[i] && gr.Depth[i] == minMoves) total += paths[i];
                if (gr.Depth[i] >= minMoves || paths[i] == 0 || gr.ChildCw[i] < 0) continue;
                int a = gr.ChildCw[i], b = gr.ChildCcw[i];
                if (gr.Depth[a] == gr.Depth[i] + 1) paths[a] += paths[i];
                if (gr.Depth[b] == gr.Depth[i] + 1) paths[b] += paths[i];
            }
            return total;
        }

        public static int[] WinDistance(SolverGraph gr)
        {
            const int Inf = 1 << 30;
            int n = gr.Count;
            var dist = new int[n];
            var parents = new List<int>[n];
            for (int i = 0; i < n; i++) { dist[i] = gr.Win[i] ? 0 : Inf; parents[i] = new List<int>(2); }
            for (int i = 0; i < n; i++)
            {
                if (gr.ChildCw[i] < 0) continue;
                parents[gr.ChildCw[i]].Add(i);
                parents[gr.ChildCcw[i]].Add(i);
            }
            var queue = new List<int>();
            for (int i = 0; i < n; i++) if (gr.Win[i]) queue.Add(i);
            for (int head = 0; head < queue.Count; head++)
            {
                int c = queue[head];
                foreach (int p in parents[c])
                {
                    if (dist[p] != Inf) continue;
                    dist[p] = dist[c] + 1;
                    queue.Add(p);
                }
            }
            return dist;
        }

        public static double DeadEndRatio(SolverGraph gr, int limit, int[] dist)
        {
            int total = 0, dead = 0;
            for (int i = 0; i < gr.Count; i++)
            {
                int d = gr.Depth[i];
                if (d < 1 || d > limit - 1 || gr.Win[i]) continue;
                total++;
                if (dist[i] > limit - d) dead++;
            }
            return total == 0 ? 0.0 : (double)dead / total;
        }

        public static double RandomWin(SolverGraph gr, int limit)
        {
            var memo = new Dictionary<long, double>();
            double P(int i, int r)
            {
                if (gr.Win[i]) return 1.0;
                if (r == 0) return 0.0;
                if (gr.ChildCw[i] < 0) return 0.0;
                long key = (long)i * 64 + r;
                if (memo.TryGetValue(key, out double v)) return v;
                v = 0.5 * (P(gr.ChildCw[i], r - 1) + P(gr.ChildCcw[i], r - 1));
                memo[key] = v;
                return v;
            }
            return P(0, limit);
        }

        /// <summary>Par and metrics at limit = par + slack (same semantics as Python solve(level, slack)).</summary>
        public static SolveResult Solve(Level lv, int slack, int maxDepth = 30, int nodeCap = 300000)
        {
            var gr = Explore(lv, maxDepth, slack, nodeCap);
            int mw = gr.MinWinDepth();
            var res = new SolveResult { MinMoves = mw, States = gr.Count, Truncated = gr.Truncated };
            if (mw < 0) return res;
            res.Solvable = true;
            res.Limit = mw + slack;
            var dist = WinDistance(gr);
            res.SolutionsAtMin = CountSolutions(gr, mw);
            res.DeadEndRatio = DeadEndRatio(gr, res.Limit, dist);
            res.RandomWin = RandomWin(gr, res.Limit);
            if (mw > 0) res.FirstMove = dist[gr.ChildCw[0]] == dist[0] - 1 ? Rot.CW : Rot.CCW;
            return res;
        }

        /// <summary>Hint for the current position: minimum moves to win and the first rotation.
        /// Returns false if no win within maxDepth / nodeCap.</summary>
        public static bool TryHint(GameState s, out int minMoves, out Rot first, int maxDepth = 20, int nodeCap = 60000)
        {
            first = Rot.CW;
            var gr = Explore(s.Level, Math.Min(maxDepth, Math.Max(1, s.MovesLeft)), 0, nodeCap, s.Cells, s.Gravity);
            minMoves = gr.MinWinDepth();
            if (minMoves <= 0) return minMoves == 0;
            var dist = WinDistance(gr);
            first = dist[gr.ChildCw[0]] == dist[0] - 1 ? Rot.CW : Rot.CCW;
            return true;
        }
    }
}
