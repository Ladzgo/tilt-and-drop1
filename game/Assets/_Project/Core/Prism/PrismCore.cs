using System;
using System.Collections.Generic;

namespace TiltDrop.Core.Prism
{
    /// <summary>Prism Paths rules (docs/games/02-prism.md §1). Mirrors tools/levelgen/prism/sim.py + solver.py;
    /// parity enforced by PrismGoldenTests over /shared/prism/golden.json.</summary>
    public enum PKind : byte { Empty, Wall, Source, Target, Mirror, Fixed, Filter, Splitter }

    public struct PCell
    {
        public PKind Kind;
        public int A; // source: color*4+dir; target: mask; mirror/fixed/splitter: state; filter: color
        public PCell(PKind k, int a) { Kind = k; A = a; }
        public bool Rotatable => Kind == PKind.Mirror || Kind == PKind.Splitter;
    }

    public struct BeamSegment
    {
        public int Color;
        public float X0, Y0, X1, Y1;
    }

    public sealed class PrismLevel
    {
        public static readonly int[] DX = { 1, 0, -1, 0 }, DY = { 0, 1, 0, -1 };
        const string DirCh = ">v<^";

        public readonly int W, H;
        public readonly PCell[] Cells;
        public int Moves, Par;
        public string Id = "";
        public Tier Tier = Tier.N;
        public readonly List<int> Rotatables = new List<int>(), Targets = new List<int>(), Sources = new List<int>();

        public PrismLevel(int w, int h, PCell[] cells)
        {
            if (w < 3 || w > 8 || h < 3 || h > 8 || cells.Length != w * h) throw new ArgumentException("bad prism level size");
            W = w; H = h; Cells = cells;
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].Rotatable) Rotatables.Add(i);
                if (cells[i].Kind == PKind.Target) Targets.Add(i);
                if (cells[i].Kind == PKind.Source) Sources.Add(i);
            }
        }

        static int ColorOf(char c)
        {
            switch (c) { case 'R': return 1; case 'G': return 2; case 'B': return 4; case 'Y': return 3; case 'M': return 5; case 'C': return 6; case 'W': return 7; }
            throw new FormatException($"bad colour {c}");
        }

        public static List<PCell> ParseRow(string row)
        {
            var o = new List<PCell>();
            for (int i = 0; i < row.Length; i++)
            {
                char ch = row[i];
                if (ch == '[')
                {
                    int j = row.IndexOf(']', i);
                    string t = row.Substring(i + 1, j - i - 1);
                    if (t == "/" || t == "\\") o.Add(new PCell(PKind.Splitter, t == "/" ? 0 : 1));
                    else o.Add(new PCell(PKind.Source, ColorOf(t[0]) * 4 + DirCh.IndexOf(t[1])));
                    i = j;
                }
                else if (ch == '(') { int j = row.IndexOf(')', i); o.Add(new PCell(PKind.Target, ColorOf(row[i + 1]))); i = j; }
                else if (ch == '{') { int j = row.IndexOf('}', i); o.Add(new PCell(PKind.Fixed, row[i + 1] == '/' ? 0 : 1)); i = j; }
                else if (ch == '<') { o.Add(new PCell(PKind.Filter, ColorOf(row[i + 1]))); i += 2; }
                else if (ch == '.') o.Add(new PCell(PKind.Empty, 0));
                else if (ch == '#') o.Add(new PCell(PKind.Wall, 0));
                else if (ch == '/') o.Add(new PCell(PKind.Mirror, 0));
                else if (ch == '\\') o.Add(new PCell(PKind.Mirror, 1));
                else throw new FormatException($"bad cell '{ch}' in {row}");
            }
            return o;
        }

        public static PrismLevel FromJson(Dictionary<string, object> d)
        {
            var cells = new List<PCell>();
            foreach (var r in d.Arr("grid")) cells.AddRange(ParseRow((string)r));
            var lv = new PrismLevel(d.Int("w"), d.Int("h"), cells.ToArray()) { Moves = d.Int("moves"), Par = d.Int("par"), Id = d.Str("id", "") };
            var t = d.Str("tier");
            if (t != null) lv.Tier = (Tier)Enum.Parse(typeof(Tier), t);
            return lv;
        }
    }

    public static class PrismRules
    {
        static readonly int[][] Reflect = { new[] { 3, 2, 1, 0 }, new[] { 1, 0, 3, 2 } };

        /// <summary>Traces all beams. Returns received mask per cell, or null if onRotatable(i) asked to abort.</summary>
        public static int[] Trace(PrismLevel lv, IDictionary<int, int> states, List<BeamSegment> segments = null, Func<int, bool> onRotatable = null)
        {
            int w = lv.W, h = lv.H;
            var received = new int[w * h];
            var stack = new Stack<(int x, int y, int d)>();
            var seen = new HashSet<int>();
            foreach (int s in lv.Sources)
            {
                int a = lv.Cells[s].A, color = a >> 2;
                stack.Clear();
                seen.Clear();
                stack.Push((s % w, s / w, a & 3));
                while (stack.Count > 0)
                {
                    var (x, y, d) = stack.Pop();
                    int sx = x, sy = y;
                    while (true)
                    {
                        int nx = x + PrismLevel.DX[d], ny = y + PrismLevel.DY[d];
                        if (nx < 0 || nx >= w || ny < 0 || ny >= h)
                        {
                            segments?.Add(new BeamSegment { Color = color, X0 = sx, Y0 = sy, X1 = nx - PrismLevel.DX[d] * 0.5f, Y1 = ny - PrismLevel.DY[d] * 0.5f });
                            break;
                        }
                        x = nx; y = ny;
                        int i = y * w + x;
                        if (!seen.Add(i * 4 + d)) { segments?.Add(Seg(color, sx, sy, x, y)); break; }
                        var c = lv.Cells[i];
                        if (c.Kind == PKind.Empty) continue;
                        if (c.Kind == PKind.Filter)
                        {
                            if (c.A == color) continue;
                            segments?.Add(Seg(color, sx, sy, x, y));
                            break;
                        }
                        if (c.Kind == PKind.Wall || c.Kind == PKind.Source || c.Kind == PKind.Target)
                        {
                            if (c.Kind == PKind.Target) received[i] |= color;
                            segments?.Add(Seg(color, sx, sy, x, y));
                            break;
                        }
                        int st;
                        if (c.Rotatable)
                        {
                            if (states != null && states.TryGetValue(i, out int v)) st = v;
                            else if (onRotatable != null && onRotatable(i)) return null;
                            else st = c.A;
                        }
                        else st = c.A;
                        segments?.Add(Seg(color, sx, sy, x, y));
                        if (c.Kind == PKind.Splitter) stack.Push((x, y, d));
                        d = Reflect[st][d];
                        sx = x; sy = y;
                    }
                }
            }
            return received;
        }

        static BeamSegment Seg(int c, int x0, int y0, int x1, int y1) => new BeamSegment { Color = c, X0 = x0, Y0 = y0, X1 = x1, Y1 = y1 };

        public static bool IsWin(PrismLevel lv, int[] received)
        {
            if (lv.Targets.Count == 0) return false;
            foreach (int t in lv.Targets) if (received[t] != lv.Cells[t].A) return false;
            return true;
        }

        public struct Solution { public int Par; public int Solutions; public List<int> Taps; }

        /// <summary>Exact solver (taps commute). Par = -1 if unsolvable.</summary>
        public static Solution Solve(PrismLevel lv, IDictionary<int, int> start = null, int nodeCap = 200000)
        {
            if (start == null)
            {
                start = new Dictionary<int, int>();
                foreach (int i in lv.Rotatables) start[i] = lv.Cells[i].A;
            }
            var best = new Solution { Par = -1, Taps = new List<int>() };
            var assign = new Dictionary<int, int>();
            int nodes = 0;
            void Rec()
            {
                if (++nodes > nodeCap) return;
                int pending = -1;
                var r = Trace(lv, assign, null, i => { pending = i; return true; });
                if (r == null)
                {
                    int s0 = start[pending];
                    foreach (int st in new[] { s0, s0 ^ 1 })
                    {
                        assign[pending] = st;
                        Rec();
                        assign.Remove(pending);
                    }
                    return;
                }
                if (!IsWin(lv, r)) return;
                best.Solutions++;
                var taps = new List<int>();
                foreach (var kv in assign) if (kv.Value != start[kv.Key]) taps.Add(kv.Key);
                taps.Sort();
                if (best.Par < 0 || taps.Count < best.Par) { best.Par = taps.Count; best.Taps = taps; }
            }
            Rec();
            return best;
        }
    }

    /// <summary>Stateful Prism session: tap toggles a rotatable piece and costs one move.</summary>
    public sealed class PrismGame
    {
        public readonly PrismLevel Level;
        public readonly Dictionary<int, int> States = new Dictionary<int, int>();
        public int MovesLeft, MovesUsed;
        public int[] Received;
        public Status Status;

        public PrismGame(PrismLevel lv)
        {
            Level = lv;
            foreach (int i in lv.Rotatables) States[i] = lv.Cells[i].A;
            MovesLeft = lv.Moves;
            Received = PrismRules.Trace(lv, States);
            Update();
        }

        void Update() => Status = PrismRules.IsWin(Level, Received) ? Status.Won : MovesLeft <= 0 ? Status.Lost : Status.Playing;

        public bool Tap(int index)
        {
            if (Status != Status.Playing) throw new InvalidOperationException($"cannot tap: {Status}");
            if (!States.ContainsKey(index)) return false;
            States[index] ^= 1;
            MovesLeft--;
            MovesUsed++;
            Received = PrismRules.Trace(Level, States);
            Update();
            return true;
        }

        public void AddMoves(int n)
        {
            if (Status == Status.Won) throw new InvalidOperationException("won");
            MovesLeft += n;
            Update();
        }

        public List<BeamSegment> Segments()
        {
            var s = new List<BeamSegment>();
            PrismRules.Trace(Level, States, s);
            return s;
        }
    }
}
