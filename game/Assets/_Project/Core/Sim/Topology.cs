using System;
using System.Collections.Generic;

namespace TiltDrop.Core
{
    /// <summary>Precomputed per-gravity tables: nearest-first order, neighbour index, gate lane.</summary>
    public sealed class Topology
    {
        public readonly int W, H;
        public readonly int[][] Order = new int[4][];
        public readonly int[][] Next = new int[4][];
        public readonly int[][] Lane = new int[4][];

        static readonly Dictionary<int, Topology> Cache = new Dictionary<int, Topology>();

        public static Topology Get(int w, int h)
        {
            int key = w * 16 + h;
            lock (Cache)
            {
                if (!Cache.TryGetValue(key, out var t)) Cache[key] = t = new Topology(w, h);
                return t;
            }
        }

        Topology(int w, int h)
        {
            W = w; H = h;
            int n = w * h;
            for (int g = 0; g < 4; g++)
            {
                var order = new int[n];
                var keys = new long[n];
                for (int i = 0; i < n; i++)
                {
                    int x = i % w, y = i / w;
                    int dist, perp;
                    switch (g)
                    {
                        case 0: dist = h - 1 - y; perp = x; break; // Down
                        case 1: dist = w - 1 - x; perp = y; break; // Right
                        case 2: dist = y; perp = x; break;         // Up
                        default: dist = x; perp = y; break;        // Left
                    }
                    order[i] = i;
                    keys[i] = (long)dist * 64 + perp;
                }
                Array.Sort(keys, order); // keys are unique -> deterministic
                Order[g] = order;
                var nx = new int[n];
                var ln = new int[n];
                for (int i = 0; i < n; i++)
                {
                    int x = i % w, y = i / w;
                    int tx = x + Dirs.DX[g], ty = y + Dirs.DY[g];
                    nx[i] = (tx >= 0 && tx < w && ty >= 0 && ty < h) ? ty * w + tx : -1;
                    ln[i] = (g == 0 || g == 2) ? x : y;
                }
                Next[g] = nx;
                Lane[g] = ln;
            }
        }
    }
}
