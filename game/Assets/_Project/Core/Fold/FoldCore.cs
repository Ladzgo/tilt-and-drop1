using System;
using System.Collections.Generic;
using System.Text;

namespace TiltDrop.Core.Fold
{
    /// <summary>Fold rules (docs/games/07-fold.md §1). A sheet is rows of stacks; a stack lists (square, faceUp) top first.
    /// Mirrors tools/levelgen/fold; parity via FoldGoldenTests.</summary>
    public sealed class FoldLevel
    {
        public readonly int W, H;
        public readonly int[] Front, Back;
        public readonly string[] Target;
        public int Moves, Par;
        public string Id = "";

        public FoldLevel(int w, int h, int[] front, int[] back, string[] target)
        {
            if (w < 1 || w > 6 || h < 1 || h > 6 || front.Length != w * h || back.Length != w * h) throw new ArgumentException("bad fold level size");
            W = w; H = h; Front = front; Back = back; Target = target;
        }

        public static FoldLevel FromJson(Dictionary<string, object> d)
        {
            int w = d.Int("w"), h = d.Int("h");
            var f = new List<int>(); var b = new List<int>(); var t = new List<string>();
            foreach (var r in d.Arr("front")) foreach (char c in (string)r) f.Add(c - '0');
            foreach (var r in d.Arr("back")) foreach (char c in (string)r) b.Add(c - '0');
            foreach (var r in d.Arr("target")) t.Add((string)r);
            return new FoldLevel(w, h, f.ToArray(), b.ToArray(), t.ToArray()) { Moves = d.Int("moves"), Par = d.Int("par"), Id = d.Str("id", "") };
        }
    }

    public static class FoldRules
    {
        public static List<(int s, int f)>[,] Initial(FoldLevel lv)
        {
            var sh = new List<(int, int)>[lv.H, lv.W];
            for (int y = 0; y < lv.H; y++) for (int x = 0; x < lv.W; x++) sh[y, x] = new List<(int, int)> { (y * lv.W + x, 1) };
            return sh;
        }

        static List<(int s, int f)> Flip(List<(int s, int f)> st)
        {
            var r = new List<(int, int)>(st.Count);
            for (int i = st.Count - 1; i >= 0; i--) r.Add((st[i].s, 1 - st[i].f));
            return r;
        }

        static List<(int s, int f)>[,] Transpose(List<(int s, int f)>[,] a)
        {
            int h = a.GetLength(0), w = a.GetLength(1);
            var t = new List<(int, int)>[w, h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) t[x, y] = a[y, x];
            return t;
        }

        public static List<(char axis, int k, int side)> LegalFolds(List<(int s, int f)>[,] sh)
        {
            int h = sh.GetLength(0), w = sh.GetLength(1);
            var o = new List<(char, int, int)>();
            for (int k = 1; k < w; k++) { if (k <= w - k) o.Add(('V', k, 0)); if (w - k <= k) o.Add(('V', k, 1)); }
            for (int k = 1; k < h; k++) { if (k <= h - k) o.Add(('H', k, 0)); if (h - k <= k) o.Add(('H', k, 1)); }
            return o;
        }

        public static List<(int s, int f)>[,] Fold(List<(int s, int f)>[,] sh, char axis, int k, int side)
        {
            int h = sh.GetLength(0), w = sh.GetLength(1);
            if (axis == 'H') { var r = Fold(Transpose(sh), 'V', k, side); return r == null ? null : Transpose(r); }
            if (axis != 'V' || k < 1 || k >= w) return null;
            if (side == 0)
            {
                if (k > w - k) return null;
                var n = new List<(int, int)>[h, w - k];
                for (int y = 0; y < h; y++)
                    for (int xn = 0; xn < w - k; xn++)
                    {
                        int m = k - 1 - xn;
                        var st = m >= 0 ? Flip(sh[y, m]) : new List<(int, int)>();
                        st.AddRange(sh[y, k + xn]);
                        n[y, xn] = st;
                    }
                return n;
            }
            if (w - k > k) return null;
            var n2 = new List<(int, int)>[h, k];
            for (int y = 0; y < h; y++)
                for (int xn = 0; xn < k; xn++)
                {
                    int m = 2 * k - 1 - xn;
                    var st = m < w ? Flip(sh[y, m]) : new List<(int, int)>();
                    st.AddRange(sh[y, xn]);
                    n2[y, xn] = st;
                }
            return n2;
        }

        public static string[] Visible(FoldLevel lv, List<(int s, int f)>[,] sh)
        {
            int h = sh.GetLength(0), w = sh.GetLength(1);
            var rows = new string[h];
            for (int y = 0; y < h; y++)
            {
                var sb = new StringBuilder();
                for (int x = 0; x < w; x++) { var top = sh[y, x][0]; sb.Append(top.f == 1 ? lv.Front[top.s] : lv.Back[top.s]); }
                rows[y] = sb.ToString();
            }
            return rows;
        }

        public static bool Solved(FoldLevel lv, List<(int s, int f)>[,] sh)
        {
            var v = Visible(lv, sh);
            if (v.Length != lv.Target.Length) return false;
            for (int i = 0; i < v.Length; i++) if (v[i] != lv.Target[i]) return false;
            return true;
        }

        public static string Key(List<(int s, int f)>[,] sh)
        {
            var sb = new StringBuilder();
            int h = sh.GetLength(0), w = sh.GetLength(1);
            sb.Append(h).Append('x').Append(w).Append(':');
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) { foreach (var e in sh[y, x]) sb.Append(e.s).Append(e.f == 1 ? '+' : '-'); sb.Append('|'); }
            return sb.ToString();
        }

        /// <summary>BFS: par (-1 if unsolvable within maxDepth) and number of shortest solutions.</summary>
        public static (int par, long solutions) Solve(FoldLevel lv, int maxDepth = 12)
        {
            var start = Initial(lv);
            if (Solved(lv, start)) return (0, 1);
            var depth = new Dictionary<string, int> { [Key(start)] = 0 };
            var paths = new Dictionary<string, long> { [Key(start)] = 1 };
            var frontier = new List<List<(int, int)>[,]> { start };
            for (int d = 0; d < maxDepth && frontier.Count > 0; d++)
            {
                var next = new List<List<(int, int)>[,]>();
                long sols = 0;
                foreach (var s in frontier)
                {
                    string ks = Key(s);
                    foreach (var mv in LegalFolds(s))
                    {
                        var ns = Fold(s, mv.axis, mv.k, mv.side);
                        string kn = Key(ns);
                        if (!depth.TryGetValue(kn, out int dn))
                        {
                            depth[kn] = d + 1; paths[kn] = paths[ks]; next.Add(ns);
                            if (Solved(lv, ns)) sols += paths[ks];
                        }
                        else if (dn == d + 1) { paths[kn] += paths[ks]; if (Solved(lv, ns)) sols += paths[ks]; }
                    }
                }
                if (sols > 0) return (d + 1, sols);
                frontier = next;
            }
            return (-1, 0);
        }
    }
}
