using System;
using System.Collections.Generic;

namespace TiltDrop.Core.Flow
{
    /// <summary>Flow Fill rules (docs/games/08-flow.md §1). Mirrors tools/levelgen/flow; parity via FlowGoldenTests.</summary>
    public sealed class FlowPipe
    {
        public readonly char Type; public readonly int Rot; public readonly bool Rotatable;
        public FlowPipe(char type, int rot, bool rotatable) { Type = type; Rot = rot; Rotatable = rotatable; }
    }

    public sealed class FlowLevel
    {
        public readonly int W, H;
        public readonly FlowPipe[] Pipes;
        public readonly int[][] Sources, Cups;   // [x, y, colour, dir]
        public int Moves, Par;
        public string Id = "";
        public readonly List<int> Rotatables = new List<int>();

        public FlowLevel(int w, int h, FlowPipe[] pipes, int[][] sources, int[][] cups)
        {
            if (w < 3 || w > 8 || h < 3 || h > 8 || pipes.Length != w * h) throw new ArgumentException("bad flow level size");
            if (sources.Length == 0 || cups.Length == 0) throw new ArgumentException("need sources and cups");
            W = w; H = h; Pipes = pipes; Sources = sources; Cups = cups;
            for (int i = 0; i < pipes.Length; i++) if (pipes[i] != null && pipes[i].Rotatable) Rotatables.Add(i);
        }

        static int[][] Quads(List<object> l)
        {
            var r = new int[l.Count][];
            for (int k = 0; k < l.Count; k++) { var q = (List<object>)l[k]; r[k] = new[] { Convert.ToInt32(q[0]), Convert.ToInt32(q[1]), Convert.ToInt32(q[2]), Convert.ToInt32(q[3]) }; }
            return r;
        }

        public static FlowLevel FromJson(Dictionary<string, object> d)
        {
            int w = d.Int("w"), h = d.Int("h");
            var pipes = new List<FlowPipe>();
            foreach (var ro in d.Arr("grid"))
            {
                var row = (string)ro;
                if (row.Length != 2 * w) throw new ArgumentException("row length != 2*w");
                for (int x = 0; x < w; x++)
                {
                    char t = row[2 * x], k = row[2 * x + 1];
                    if (t == '.') { pipes.Add(null); continue; }
                    char lt = char.ToLowerInvariant(t);
                    if (FlowRules.Base(lt) < 0 || k < '0' || k > '3') throw new ArgumentException("bad pipe " + t + k);
                    pipes.Add(new FlowPipe(lt, k - '0', char.IsLower(t) && t != 'x'));
                }
            }
            return new FlowLevel(w, h, pipes.ToArray(), Quads(d.Arr("sources")), Quads(d.Arr("cups"))) { Moves = d.Int("moves"), Par = d.Int("par"), Id = d.Str("id", "") };
        }
    }

    public sealed class FlowFlood
    {
        public int[] Colour, Dist;
        public List<(int cell, int dir)> Leaks;
    }

    public sealed class FlowState
    {
        public int[] Colour, Dist;
        public List<(int cell, int dir)> Leaks;
        public List<int> Contaminated;
        public bool[] Cups;
        public bool Win;
    }

    public static class FlowRules
    {
        public static readonly int[] DX = { 1, 0, -1, 0 }, DY = { 0, 1, 0, -1 };

        public static int Base(char t) => t == 'i' ? 0b0101 : t == 'l' ? 0b1001 : t == 't' ? 0b1101 : t == 'x' ? 0b1111 : -1;
        public static int RotMask(int m, int r) { r &= 3; return ((m << r) | (m >> (4 - r))) & 15; }

        public static int Mask(FlowLevel lv, int i, IDictionary<int, int> rot)
        {
            var p = lv.Pipes[i];
            if (p != null) return RotMask(Base(p.Type), rot != null && rot.TryGetValue(i, out var r) ? r : p.Rot);
            foreach (var s in lv.Sources) if (s[1] * lv.W + s[0] == i) return 1 << s[3];
            foreach (var c in lv.Cups) if (c[1] * lv.W + c[0] == i) return 1 << c[3];
            return 0;
        }

        /// <summary>Breadth-first flood from all sources; returns null when onRotatable asks to abort (solver branching).</summary>
        public static FlowFlood Flood(FlowLevel lv, IDictionary<int, int> rot, Func<int, bool> onRotatable = null)
        {
            int w = lv.W, h = lv.H, n = w * h;
            var colour = new int[n]; var dist = new int[n];
            for (int i = 0; i < n; i++) dist[i] = -1;
            var leaks = new SortedSet<int>();
            var queue = new List<(int cell, int c)>();
            foreach (var s in lv.Sources) { int i = s[1] * w + s[0]; colour[i] |= 1 << s[2]; dist[i] = 0; queue.Add((i, s[2])); }
            var cache = new Dictionary<int, int>();
            bool abort = false;
            int M(int i)
            {
                if (cache.TryGetValue(i, out var v)) return v;
                var p = lv.Pipes[i];
                if (p != null && p.Rotatable && !(rot != null && rot.ContainsKey(i)) && onRotatable != null && onRotatable(i)) { abort = true; return 0; }
                v = Mask(lv, i, rot); cache[i] = v; return v;
            }
            for (int head = 0; head < queue.Count; head++)
            {
                var (i, c) = queue[head];
                int mi = M(i); if (abort) return null;
                int x = i % w, y = i / w;
                for (int d = 0; d < 4; d++)
                {
                    if ((mi >> d & 1) == 0) continue;
                    int nx = x + DX[d], ny = y + DY[d];
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) { leaks.Add(i * 4 + d); continue; }
                    int j = ny * w + nx;
                    int mj = M(j); if (abort) return null;
                    if ((mj >> ((d + 2) & 3) & 1) == 0) { leaks.Add(i * 4 + d); continue; }
                    if ((colour[j] >> c & 1) != 0) continue;
                    colour[j] |= 1 << c;
                    if (dist[j] < 0) dist[j] = dist[i] + 1;
                    queue.Add((j, c));
                }
            }
            var lk = new List<(int, int)>();
            foreach (var k in leaks) lk.Add((k >> 2, k & 3));
            return new FlowFlood { Colour = colour, Dist = dist, Leaks = lk };
        }

        public static FlowState Evaluate(FlowLevel lv, IDictionary<int, int> rot)
        {
            var f = Flood(lv, rot);
            var cont = new List<int>();
            for (int i = 0; i < f.Colour.Length; i++) if ((f.Colour[i] & (f.Colour[i] - 1)) != 0) cont.Add(i);
            var cups = new bool[lv.Cups.Length]; bool all = true;
            for (int k = 0; k < cups.Length; k++) { var c = lv.Cups[k]; cups[k] = f.Colour[c[1] * lv.W + c[0]] == 1 << c[2]; all &= cups[k]; }
            return new FlowState { Colour = f.Colour, Dist = f.Dist, Leaks = f.Leaks, Contaminated = cont, Cups = cups, Win = all && f.Leaks.Count == 0 && cont.Count == 0 };
        }

        static List<(int r, int cost)> Options(FlowLevel lv, int i, int startR)
        {
            var seen = new List<int>(); var res = new List<(int, int)>();
            for (int k = 0; k < 4; k++)
            {
                int r = (startR + k) & 3, mk = RotMask(Base(lv.Pipes[i].Type), r);
                if (!seen.Contains(mk)) { seen.Add(mk); res.Add((r, k)); }
            }
            return res;   // already in (cost, rotation) order: cost k increases strictly
        }

        /// <summary>Exact par (fewest clockwise taps) and number of winning configurations of the reached pipes. par = -1 if unsolvable.</summary>
        public static (int par, long solutions, Dictionary<int, int> rot) Solve(FlowLevel lv, IDictionary<int, int> start = null, int nodeCap = 300000)
        {
            if (start == null) { var s0 = new Dictionary<int, int>(); foreach (var i in lv.Rotatables) s0[i] = lv.Pipes[i].Rot; start = s0; }
            int best = -1; long count = 0; int nodes = 0; Dictionary<int, int> bestRot = null;
            var assign = new Dictionary<int, int>();
            void Rec(int cost)
            {
                if (++nodes > nodeCap) return;
                int pending = -1;
                var f = Flood(lv, assign, i => { if (pending < 0) pending = i; return true; });
                if (f == null)
                {
                    int i = pending;
                    foreach (var (rr, c) in Options(lv, i, start[i])) { assign[i] = rr; Rec(cost + c); assign.Remove(i); }
                    return;
                }
                if (f.Leaks.Count > 0) return;
                foreach (var cm in f.Colour) if ((cm & (cm - 1)) != 0) return;
                foreach (var c in lv.Cups) if (f.Colour[c[1] * lv.W + c[0]] != 1 << c[2]) return;
                count++;
                if (best < 0 || cost < best) { best = cost; bestRot = new Dictionary<int, int>(assign); }
            }
            Rec(0);
            return (best, count, bestRot);
        }
    }

    public enum FlowStatus { Playing, Won, Lost }

    public sealed class FlowGame
    {
        public readonly FlowLevel Level;
        public readonly Dictionary<int, int> Rot = new Dictionary<int, int>();
        public int MovesLeft, MovesUsed;
        public FlowState State;
        public FlowStatus Status;

        public FlowGame(FlowLevel lv)
        {
            Level = lv; MovesLeft = lv.Moves;
            foreach (var i in lv.Rotatables) Rot[i] = lv.Pipes[i].Rot;
            Update();
        }

        void Update()
        {
            State = FlowRules.Evaluate(Level, Rot);
            Status = State.Win ? FlowStatus.Won : MovesLeft <= 0 ? FlowStatus.Lost : FlowStatus.Playing;
        }

        public bool Tap(int cell)
        {
            if (Status != FlowStatus.Playing) throw new InvalidOperationException("cannot tap: " + Status);
            if (!Rot.ContainsKey(cell)) return false;
            Rot[cell] = (Rot[cell] + 1) & 3; MovesLeft--; MovesUsed++; Update(); return true;
        }

        public void AddMoves(int n)
        {
            if (Status == FlowStatus.Won) throw new InvalidOperationException("won");
            MovesLeft += n; Update();
        }
    }
}
