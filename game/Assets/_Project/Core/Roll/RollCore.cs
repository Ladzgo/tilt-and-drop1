using System;
using System.Collections.Generic;

namespace TiltDrop.Core.Roll
{
    /// <summary>Roll &amp; Print rules (docs/games/03-roll.md §1). Mirrors tools/levelgen/roll/sim.py + solver.py;
    /// parity enforced by RollGoldenTests over /shared/roll/golden.json.</summary>
    public enum RTile : byte { Floor, Wall, Target, Pool, Eraser, Ice }

    public sealed class RollLevel
    {
        public const int T = 0, B = 1, N = 2, S = 3, E = 4, W = 5;
        public static readonly int[] DX = { 1, 0, -1, 0 }, DY = { 0, 1, 0, -1 };
        public readonly int Wd, Ht;
        public readonly RTile[] Kind;
        public readonly int[] Color;
        public readonly int StartX, StartY;
        public readonly int[] Faces;
        public int Moves, Par;
        public string Id = "";
        public readonly List<int> Targets = new List<int>();

        public RollLevel(int w, int h, RTile[] kind, int[] color, int sx, int sy, int[] faces)
        {
            if (w < 3 || w > 8 || h < 3 || h > 8 || kind.Length != w * h) throw new ArgumentException("bad roll level size");
            if (faces.Length != 6) throw new ArgumentException("need 6 faces");
            if (kind[sy * w + sx] == RTile.Wall) throw new ArgumentException("cube must start on an open tile");
            Wd = w; Ht = h; Kind = kind; Color = color; StartX = sx; StartY = sy; Faces = faces;
            for (int i = 0; i < kind.Length; i++) if (kind[i] == RTile.Target) Targets.Add(i);
        }

        public static RollLevel FromJson(Dictionary<string, object> d)
        {
            int w = d.Int("w"), h = d.Int("h");
            var kind = new RTile[w * h];
            var color = new int[w * h];
            int i = 0;
            foreach (var ro in d.Arr("grid"))
            {
                foreach (char ch in (string)ro)
                {
                    if (ch == '.') kind[i] = RTile.Floor;
                    else if (ch == '#') kind[i] = RTile.Wall;
                    else if (ch == '*') kind[i] = RTile.Eraser;
                    else if (ch == '~') kind[i] = RTile.Ice;
                    else if (ch >= '1' && ch <= '6') { kind[i] = RTile.Target; color[i] = ch - '0'; }
                    else if (ch >= 'a' && ch <= 'f') { kind[i] = RTile.Pool; color[i] = ch - 'a' + 1; }
                    else throw new FormatException($"bad tile '{ch}'");
                    i++;
                }
            }
            var c = d.Obj("cube");
            var fs = c.Str("faces");
            var faces = new int[6];
            for (int k = 0; k < 6; k++) faces[k] = fs[k] - '0';
            return new RollLevel(w, h, kind, color, c.Int("x"), c.Int("y"), faces) { Moves = d.Int("moves"), Par = d.Int("par"), Id = d.Str("id", "") };
        }
    }

    public struct RollState
    {
        public int X, Y;
        public int[] Faces;
        public int[] Prints; // per target (same order as level.Targets)

        public RollState Clone() => new RollState { X = X, Y = Y, Faces = (int[])Faces.Clone(), Prints = (int[])Prints.Clone() };

        public string Key()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(X).Append(',').Append(Y).Append('|');
            foreach (int f in Faces) sb.Append(f);
            sb.Append('|');
            foreach (int p in Prints) sb.Append(p);
            return sb.ToString();
        }
    }

    public static class RollRules
    {
        static readonly int[][] Perm =
        {
            new[] { RollLevel.W, RollLevel.E, RollLevel.N, RollLevel.S, RollLevel.T, RollLevel.B },
            new[] { RollLevel.N, RollLevel.S, RollLevel.B, RollLevel.T, RollLevel.E, RollLevel.W },
            new[] { RollLevel.E, RollLevel.W, RollLevel.N, RollLevel.S, RollLevel.B, RollLevel.T },
            new[] { RollLevel.S, RollLevel.N, RollLevel.T, RollLevel.B, RollLevel.E, RollLevel.W },
        };

        public static int[] RollFaces(int[] f, int d)
        {
            var p = Perm[d];
            return new[] { f[p[0]], f[p[1]], f[p[2]], f[p[3]], f[p[4]], f[p[5]] };
        }

        public static RollState Start(RollLevel lv) => new RollState { X = lv.StartX, Y = lv.StartY, Faces = (int[])lv.Faces.Clone(), Prints = new int[lv.Targets.Count] };

        /// <summary>One roll incl. ice slide. Returns false if blocked (state unchanged). trail gets landing indices.</summary>
        public static bool Step(RollLevel lv, ref RollState st, int d, List<int> trail = null)
        {
            int x = st.X, y = st.Y, nx = x + RollLevel.DX[d], ny = y + RollLevel.DY[d];
            if (nx < 0 || nx >= lv.Wd || ny < 0 || ny >= lv.Ht || lv.Kind[ny * lv.Wd + nx] == RTile.Wall) return false;
            var faces = st.Faces;
            var prints = (int[])st.Prints.Clone();
            while (true)
            {
                x = nx; y = ny;
                faces = RollFaces(faces, d);
                int i = y * lv.Wd + x;
                var k = lv.Kind[i];
                if (k == RTile.Pool) faces[RollLevel.B] = lv.Color[i];
                else if (k == RTile.Target && faces[RollLevel.B] != 0) prints[lv.Targets.IndexOf(i)] = faces[RollLevel.B];
                else if (k == RTile.Eraser) faces[RollLevel.B] = 0;
                trail?.Add(i);
                if (k != RTile.Ice) break;
                nx = x + RollLevel.DX[d]; ny = y + RollLevel.DY[d];
                if (nx < 0 || nx >= lv.Wd || ny < 0 || ny >= lv.Ht || lv.Kind[ny * lv.Wd + nx] == RTile.Wall) break;
            }
            st = new RollState { X = x, Y = y, Faces = faces, Prints = prints };
            return true;
        }

        public static bool Solved(RollLevel lv, RollState st)
        {
            if (lv.Targets.Count == 0) return false;
            for (int k = 0; k < lv.Targets.Count; k++) if (st.Prints[k] != lv.Color[lv.Targets[k]]) return false;
            return true;
        }

        /// <summary>BFS: par (-1 if none within maxDepth), number of shortest solutions, first move.</summary>
        public static (int par, long solutions, int first) Solve(RollLevel lv, RollState start, int maxDepth = 30, int cap = 300000)
        {
            if (Solved(lv, start)) return (0, 1, -1);
            var depth = new Dictionary<string, int> { [start.Key()] = 0 };
            var paths = new Dictionary<string, long> { [start.Key()] = 1 };
            var firstOf = new Dictionary<string, int> { [start.Key()] = -1 };
            var frontier = new List<(string, RollState)> { (start.Key(), start) };
            for (int dpt = 0; dpt < maxDepth && frontier.Count > 0; dpt++)
            {
                var next = new List<(string, RollState)>();
                long sols = 0;
                int first = -1;
                foreach (var (k, s) in frontier)
                {
                    for (int d = 0; d < 4; d++)
                    {
                        var ns = s;
                        if (!Step(lv, ref ns, d)) continue;
                        string nk = ns.Key();
                        if (!depth.TryGetValue(nk, out int nd))
                        {
                            depth[nk] = dpt + 1; paths[nk] = paths[k]; firstOf[nk] = dpt == 0 ? d : firstOf[k];
                            next.Add((nk, ns));
                            if (Solved(lv, ns)) { sols += paths[k]; if (first < 0) first = firstOf[nk]; }
                        }
                        else if (nd == dpt + 1)
                        {
                            paths[nk] += paths[k];
                            if (Solved(lv, ns)) sols += paths[k];
                        }
                        if (depth.Count > cap) return (-1, 0, -1);
                    }
                }
                if (sols > 0) return (dpt + 1, sols, first);
                frontier = next;
            }
            return (-1, 0, -1);
        }
    }
}
