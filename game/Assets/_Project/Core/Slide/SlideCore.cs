using System;
using System.Collections.Generic;
using System.Text;

namespace TiltDrop.Core.Slide
{
    /// <summary>Slide Out rules (docs/games/04-slide.md §1). Mirrors tools/levelgen/slide/sim.py + solver.py;
    /// parity enforced by SlideGoldenTests over /shared/slide/golden.json.</summary>
    public sealed class SlideLevel
    {
        public static readonly int[] DX = { 1, 0, -1, 0 }, DY = { 0, 1, 0, -1 };
        const string Arrows = ">v<^";
        public readonly int W, H, Capacity;
        public readonly bool[] Wall;
        public readonly int[] Color, Dir; // Color 0 = no block
        public readonly List<int> Blocks = new List<int>();
        public readonly Dictionary<int, int> Index = new Dictionary<int, int>();
        public string Id = "";

        public SlideLevel(int w, int h, bool[] wall, int[] color, int[] dir, int capacity)
        {
            if (w < 3 || w > 8 || h < 3 || h > 8 || wall.Length != w * h) throw new ArgumentException("bad slide level size");
            W = w; H = h; Wall = wall; Color = color; Dir = dir; Capacity = capacity;
            for (int i = 0; i < w * h; i++) if (color[i] != 0) { Index[i] = Blocks.Count; Blocks.Add(i); }
            if (Blocks.Count > 30) throw new ArgumentException("at most 30 blocks");
        }

        public static SlideLevel FromJson(Dictionary<string, object> d)
        {
            int w = d.Int("w"), h = d.Int("h");
            var wall = new bool[w * h];
            var color = new int[w * h];
            var dir = new int[w * h];
            int y = 0;
            foreach (var ro in d.Arr("grid"))
            {
                var r = (string)ro;
                for (int x = 0; x < w; x++)
                {
                    char a = r[2 * x], b = r[2 * x + 1];
                    int i = y * w + x;
                    if (a == '#') wall[i] = true;
                    else if (a != '.') { color[i] = a - '0'; dir[i] = Arrows.IndexOf(b); }
                }
                y++;
            }
            return new SlideLevel(w, h, wall, color, dir, d.Int("capacity", 5)) { Id = d.Str("id", "") };
        }
    }

    public static class SlideRules
    {
        public static int Blocker(SlideLevel lv, uint removed, int k)
        {
            int i = lv.Blocks[k], d = lv.Dir[i], x = i % lv.W, y = i / lv.W;
            while (true)
            {
                x += SlideLevel.DX[d]; y += SlideLevel.DY[d];
                if (x < 0 || x >= lv.W || y < 0 || y >= lv.H) return -1;
                int j = y * lv.W + x;
                if (lv.Wall[j]) return j;
                if (lv.Color[j] == 0) continue;
                if (((removed >> lv.Index[j]) & 1) != 0) continue;
                return j;
            }
        }

        /// <summary>Returns false for a bump / removed block; otherwise updates removed + tray.</summary>
        public static bool Tap(SlideLevel lv, ref uint removed, List<int> tray, int k, out bool matched)
        {
            matched = false;
            if (((removed >> k) & 1) != 0 || Blocker(lv, removed, k) >= 0) return false;
            int colour = lv.Color[lv.Blocks[k]];
            tray.Add(colour);
            int count = 0;
            foreach (int c in tray) if (c == colour) count++;
            if (count >= 3)
            {
                int n = 0;
                for (int i = 0; i < tray.Count;)
                {
                    if (tray[i] == colour && n < 3) { tray.RemoveAt(i); n++; } else i++;
                }
                matched = true;
            }
            removed |= 1u << k;
            return true;
        }

        public static Status StatusOf(SlideLevel lv, uint removed, List<int> tray, int capacity)
        {
            uint all = lv.Blocks.Count == 32 ? uint.MaxValue : (1u << lv.Blocks.Count) - 1;
            if (removed == all && tray.Count == 0) return Status.Won;
            if (tray.Count >= capacity) return Status.Lost;
            return Status.Playing;
        }

        static string Key(uint rm, List<int> tray)
        {
            var t = new List<int>(tray);
            t.Sort();
            var sb = new StringBuilder();
            sb.Append(rm).Append('|');
            foreach (int c in t) sb.Append(c);
            return sb.ToString();
        }

        /// <summary>Memoised DFS with the same visiting order as the Python solver (state counts match).</summary>
        public static (bool solvable, int states, List<int> order) Solve(SlideLevel lv, uint removed = 0, List<int> tray = null, int capacity = -1, int nodeCap = 400000)
        {
            tray = tray ?? new List<int>();
            int cap = capacity > 0 ? capacity : lv.Capacity, n = lv.Blocks.Count;
            var memo = new Dictionary<string, bool>();
            bool Win(uint rm, List<int> tr)
            {
                string k = Key(rm, tr);
                if (memo.TryGetValue(k, out bool v)) return v;
                var st = StatusOf(lv, rm, tr, cap);
                if (st != Status.Playing) { memo[k] = st == Status.Won; return memo[k]; }
                if (memo.Count > nodeCap) return false;
                memo[k] = false;
                bool ok = false;
                for (int b = 0; b < n; b++)
                {
                    uint r = rm;
                    var t = new List<int>(tr);
                    if (Tap(lv, ref r, t, b, out _) && Win(r, t)) ok = true;
                }
                memo[k] = ok;
                return ok;
            }
            bool solvable = Win(removed, tray);
            var order = new List<int>();
            if (solvable)
            {
                uint rm = removed;
                var tr = new List<int>(tray);
                while (StatusOf(lv, rm, tr, cap) == Status.Playing)
                {
                    bool moved = false;
                    for (int b = 0; b < n; b++)
                    {
                        uint r = rm;
                        var t = new List<int>(tr);
                        if (!Tap(lv, ref r, t, b, out _)) continue;
                        if ((memo.TryGetValue(Key(r, t), out bool w) && w) || StatusOf(lv, r, t, cap) == Status.Won)
                        {
                            order.Add(b); rm = r; tr = t; moved = true;
                            break;
                        }
                    }
                    if (!moved) break;
                }
            }
            return (solvable, memo.Count, order);
        }
    }
}
