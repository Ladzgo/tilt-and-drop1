using System;
using System.Collections.Generic;

namespace TiltDrop.Core.Domino
{
    /// <summary>Domino Run rules (docs/games/06-domino.md §1). Mirrors tools/levelgen/domino; parity via DominoGoldenTests.</summary>
    public sealed class DominoLevel
    {
        public static readonly int[] DX = { 1, 0, -1, 0 }, DY = { 0, 1, 0, -1 };
        const string Arrows = ">v<^";
        public readonly int W, H, Attempts;
        public readonly char[] Kind;
        public readonly int[] Dir;
        public readonly List<int> Turners = new List<int>(), Bells = new List<int>();
        public readonly int Start;
        public int Par;
        public string Id = "";

        public DominoLevel(int w, int h, char[] kind, int[] dir, int attempts)
        {
            if (w < 3 || w > 8 || h < 3 || h > 8 || kind.Length != w * h) throw new ArgumentException("bad domino level size");
            W = w; H = h; Kind = kind; Dir = dir; Attempts = attempts; Start = -1;
            for (int i = 0; i < kind.Length; i++)
            {
                if (kind[i] == 't') Turners.Add(i);
                if (kind[i] == 'B') Bells.Add(i);
                if (kind[i] == 'S') Start = i;
            }
            if (Start < 0 || Bells.Count == 0) throw new ArgumentException("need a start domino and a bell");
        }

        public int[] StartConfig() { var c = new int[Turners.Count]; for (int k = 0; k < c.Length; k++) c[k] = Dir[Turners[k]]; return c; }

        public static DominoLevel FromJson(Dictionary<string, object> d)
        {
            int w = d.Int("w"), h = d.Int("h"), y = 0;
            var kind = new char[w * h];
            var dir = new int[w * h];
            foreach (var ro in d.Arr("grid"))
            {
                var r = (string)ro;
                for (int x = 0; x < w; x++)
                {
                    char k = r[2 * x], a = r[2 * x + 1];
                    kind[y * w + x] = k;
                    if (k == 't' || k == 'T' || k == 'S') dir[y * w + x] = Arrows.IndexOf(a);
                }
                y++;
            }
            return new DominoLevel(w, h, kind, dir, d.Int("attempts", 3)) { Par = d.Int("par"), Id = d.Str("id", "") };
        }
    }

    public struct Fall { public int T, Cell; public int[] Outs; }

    public static class DominoRules
    {
        static bool Standing(char k) => k == 'd' || k == 't' || k == 'T' || k == 'Y' || k == 'B' || k == 'S';

        /// <summary>One push. Hits are processed in (time, cell, incoming dir) order, as in the Python heap.</summary>
        public static List<Fall> RunChain(DominoLevel lv, int[] config, HashSet<int> rung)
        {
            var tdir = new Dictionary<int, int>();
            for (int k = 0; k < lv.Turners.Count; k++) tdir[lv.Turners[k]] = config[k];
            var fallen = new HashSet<int>();
            var falls = new List<Fall>();
            var heap = new SortedSet<(int t, int c, int d)> { (0, lv.Start, lv.Dir[lv.Start]) };
            while (heap.Count > 0)
            {
                var top = heap.Min;
                heap.Remove(top);
                var (t, c, d) = top;
                if (!fallen.Add(c)) continue;
                char k = lv.Kind[c];
                if (k == 'B') { rung.Add(c); falls.Add(new Fall { T = t, Cell = c, Outs = new int[0] }); continue; }
                int[] outs = k == 'd' ? new[] { d } : k == 't' ? new[] { tdir[c] } : (k == 'T' || k == 'S') ? new[] { lv.Dir[c] } : new[] { (d + 1) & 3, (d + 3) & 3 };
                falls.Add(new Fall { T = t, Cell = c, Outs = outs });
                int x = c % lv.W, y = c / lv.W;
                foreach (int o in outs)
                {
                    int nx = x + DominoLevel.DX[o], ny = y + DominoLevel.DY[o];
                    if (nx < 0 || nx >= lv.W || ny < 0 || ny >= lv.H) continue;
                    int n = ny * lv.W + nx;
                    if (Standing(lv.Kind[n]) && !fallen.Contains(n)) heap.Add((t + 1, n, o));
                }
            }
            return falls;
        }

        public static bool Wins(DominoLevel lv, int[] config)
        {
            var rung = new HashSet<int>();
            RunChain(lv, config, rung);
            foreach (int b in lv.Bells) if (!rung.Contains(b)) return false;
            return true;
        }

        public static int TapsBetween(int[] a, int[] b) { int s = 0; for (int i = 0; i < a.Length; i++) s += (b[i] - a[i]) & 3; return s; }

        /// <summary>Enumerates 4^turners configurations. Par = -1 if none rings every bell.</summary>
        public static (int par, int solutions, int[] best) Solve(DominoLevel lv, int[] start)
        {
            int t = lv.Turners.Count, total = 1 << (2 * t), count = 0;
            int[] best = null;
            for (int m = 0; m < total; m++)
            {
                var cfg = new int[t];
                int v = m;
                for (int k = t - 1; k >= 0; k--) { cfg[k] = v & 3; v >>= 2; }
                if (!Wins(lv, cfg)) continue;
                count++;
                if (best == null || TapsBetween(start, cfg) < TapsBetween(start, best)) best = cfg; // m ascending = tuple order
            }
            return best == null ? (-1, 0, null) : (TapsBetween(start, best), count, best);
        }
    }
}
