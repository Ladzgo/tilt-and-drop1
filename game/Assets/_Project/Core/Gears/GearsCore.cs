using System;
using System.Collections.Generic;

namespace TiltDrop.Core.Gears
{
    /// <summary>Gear Works rules (docs/games/05-gears.md §1): hex grid (odd rows shifted right), spin propagation,
    /// jams, inventory taps, exhaustive solver. Mirrors tools/levelgen/gears; parity via GearsGoldenTests.</summary>
    public sealed class GearLevel
    {
        public readonly int W, H, Inventory;
        public readonly char[] Cells;
        public int Moves, Par;
        public string Id = "";
        public readonly List<int> Pegs = new List<int>(), Outputs = new List<int>();
        public readonly Dictionary<int, int> PegIndex = new Dictionary<int, int>();

        public GearLevel(int w, int h, char[] cells, int inventory)
        {
            if (w < 3 || w > 8 || h < 3 || h > 8 || cells.Length != w * h) throw new ArgumentException("bad gear level size");
            W = w; H = h; Cells = cells; Inventory = inventory;
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] == 'o' || cells[i] == 'g') { PegIndex[i] = Pegs.Count; Pegs.Add(i); }
                if (cells[i] == 'R' || cells[i] == 'L') Outputs.Add(i);
            }
        }

        public int StartLayout()
        {
            int m = 0;
            for (int k = 0; k < Pegs.Count; k++) if (Cells[Pegs[k]] == 'g') m |= 1 << k;
            return m;
        }

        public static GearLevel FromJson(Dictionary<string, object> d)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var r in d.Arr("grid")) sb.Append((string)r);
            return new GearLevel(d.Int("w"), d.Int("h"), sb.ToString().ToCharArray(), d.Int("inventory")) { Moves = d.Int("moves"), Par = d.Int("par"), Id = d.Str("id", "") };
        }
    }

    public static class GearRules
    {
        public static List<int> Neighbours(int w, int h, int i)
        {
            int x = i % w, y = i / w;
            int[,] c = y % 2 == 0
                ? new[,] { { x + 1, y }, { x - 1, y }, { x - 1, y - 1 }, { x, y - 1 }, { x - 1, y + 1 }, { x, y + 1 } }
                : new[,] { { x + 1, y }, { x - 1, y }, { x, y - 1 }, { x + 1, y - 1 }, { x, y + 1 }, { x + 1, y + 1 } };
            var o = new List<int>(6);
            for (int k = 0; k < 6; k++)
            {
                int a = c[k, 0], b = c[k, 1];
                if (a >= 0 && a < w && b >= 0 && b < h) o.Add(b * w + a);
            }
            return o;
        }

        static bool IsGear(GearLevel lv, int layout, int i)
        {
            char c = lv.Cells[i];
            if (c == 'G' || c == 'M' || c == 'R' || c == 'L') return true;
            if (c == 'o' || c == 'g') return ((layout >> lv.PegIndex[i]) & 1) != 0;
            return false;
        }

        public static void Spins(GearLevel lv, int layout, out int[] spin, out bool[] jam)
        {
            int w = lv.W, h = lv.H, n = w * h;
            var gear = new bool[n];
            for (int i = 0; i < n; i++) gear[i] = IsGear(lv, layout, i);
            spin = new int[n]; jam = new bool[n];
            var seen = new bool[n];
            for (int s = 0; s < n; s++)
            {
                if (!gear[s] || seen[s]) continue;
                var comp = new List<int>();
                var stack = new Stack<int>();
                stack.Push(s); seen[s] = true;
                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    comp.Add(i);
                    foreach (int j in Neighbours(w, h, i)) if (gear[j] && !seen[j]) { seen[j] = true; stack.Push(j); }
                }
                var motors = comp.FindAll(i => lv.Cells[i] == 'M');
                if (motors.Count == 0) continue;
                motors.Sort();
                var val = new Dictionary<int, int>();
                var queue = new List<int>();
                foreach (int m in motors) { val[m] = 1; queue.Add(m); }
                bool ok = true;
                for (int head = 0; head < queue.Count && ok; head++)
                {
                    int i = queue[head];
                    foreach (int j in Neighbours(w, h, i))
                    {
                        if (!gear[j]) continue;
                        if (val.TryGetValue(j, out int vj)) { if (vj == val[i]) { ok = false; break; } }
                        else { val[j] = -val[i]; queue.Add(j); }
                    }
                }
                foreach (int i in comp) { if (ok) spin[i] = val[i]; else jam[i] = true; }
            }
        }

        public static bool IsWin(GearLevel lv, int layout)
        {
            if (lv.Outputs.Count == 0) return false;
            Spins(lv, layout, out var spin, out _);
            foreach (int o in lv.Outputs) if (spin[o] != (lv.Cells[o] == 'R' ? 1 : -1)) return false;
            return true;
        }

        static int Pop(int m) { int c = 0; while (m != 0) { m &= m - 1; c++; } return c; }

        /// <summary>Exhaustive over layouts within the inventory (same order as Python's combinations).</summary>
        public static (int par, int solutions, int best) Solve(GearLevel lv, int start)
        {
            int m = lv.Pegs.Count, count = 0, best = -1;
            void Rec(int from, int r, int lay)
            {
                if (r == 0)
                {
                    if (!IsWin(lv, lay)) return;
                    count++;
                    if (best < 0) { best = lay; return; }
                    int a = Pop(lay ^ start), b = Pop(best ^ start);
                    if (a < b || (a == b && lay < best)) best = lay;
                    return;
                }
                for (int k = from; k < m; k++) Rec(k + 1, r - 1, lay | (1 << k));
            }
            for (int r = 0; r <= lv.Inventory; r++) Rec(0, r, 0);
            return best < 0 ? (-1, 0, -1) : (Pop(best ^ start), count, best);
        }
    }
}
