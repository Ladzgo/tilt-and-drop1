using System;
using System.Collections.Generic;
using System.Text;

namespace TiltDrop.Core
{
    public enum Tier : byte { E = 0, N = 1, H = 2, SH = 3 }

    /// <summary>Immutable level definition. Gates[edge] is indexed by x (T,B) or y (L,R).</summary>
    public sealed class Level
    {
        public const int MinSize = 3, MaxSize = 8;

        public readonly int W, H;
        public readonly byte[] Cells;
        public readonly byte[][] Gates; // [T,R,B,L]
        public int Moves;
        public int Par;
        public string Id = "";
        public Tier Tier = Tier.N;
        public int MechanicsMask;

        public Level(int w, int h, byte[] cells, byte[][] gates)
        {
            if (w < MinSize || w > MaxSize || h < MinSize || h > MaxSize) throw new ArgumentException($"board size {w}x{h} out of range");
            if (cells == null || cells.Length != w * h) throw new ArgumentException("cells length mismatch");
            if (gates == null || gates.Length != 4) throw new ArgumentException("need 4 gate edges");
            for (int e = 0; e < 4; e++)
            {
                int n = (e == Edges.T || e == Edges.B) ? w : h;
                if (gates[e] == null || gates[e].Length != n) throw new ArgumentException($"gate edge {Edges.Names[e]} must have length {n}");
            }
            W = w; H = h; Cells = cells; Gates = gates;
        }

        public int CountColored()
        {
            int n = 0;
            foreach (var c in Cells) if (TiltDrop.Core.Cells.IsColored(c)) n++;
            return n;
        }

        // ---- text format (shared with Python / golden vectors) ---------------------
        public static byte[] ParseRow(string row)
        {
            var list = new List<byte>();
            for (int i = 0; i < row.Length; i++)
            {
                char ch = row[i];
                if (ch == '[')
                {
                    int end = row.IndexOf(']', i);
                    if (end != i + 3) throw new FormatException($"bad token in row {row}");
                    char p = row[i + 1];
                    int col = row[i + 2] - '0';
                    if (col < 1 || col > TiltDrop.Core.Cells.MaxColors) throw new FormatException("bad color");
                    byte kind = p == 'a' ? TiltDrop.Core.Cells.Anchor : p == 'i' ? TiltDrop.Core.Cells.Ice2 : p == 'j' ? TiltDrop.Core.Cells.Ice1 : (byte)0xFF;
                    if (kind == 0xFF) throw new FormatException($"bad token prefix {p}");
                    list.Add((byte)(kind | col));
                    i = end;
                    continue;
                }
                if (ch == '.') list.Add(TiltDrop.Core.Cells.Empty);
                else if (ch == '#') list.Add(TiltDrop.Core.Cells.Wall);
                else if (ch == '@') list.Add(TiltDrop.Core.Cells.Stone);
                else if (ch >= '1' && ch <= '6') list.Add((byte)(TiltDrop.Core.Cells.Block | (ch - '0')));
                else throw new FormatException($"bad cell '{ch}' in row {row}");
            }
            return list.ToArray();
        }

        public static string Token(byte c)
        {
            int k = c & 0xF0, col = c & 0x0F;
            if (c == TiltDrop.Core.Cells.Empty) return ".";
            if (c == TiltDrop.Core.Cells.Wall) return "#";
            if (c == TiltDrop.Core.Cells.Stone) return "@";
            if (k == TiltDrop.Core.Cells.Block) return col.ToString();
            if (k == TiltDrop.Core.Cells.Anchor) return "[a" + col + "]";
            if (k == TiltDrop.Core.Cells.Ice2) return "[i" + col + "]";
            if (k == TiltDrop.Core.Cells.Ice1) return "[j" + col + "]";
            throw new ArgumentException($"invalid cell 0x{c:x2}");
        }

        public static string[] Rows(byte[] cells, int w, int h)
        {
            var rows = new string[h];
            var sb = new StringBuilder();
            for (int y = 0; y < h; y++)
            {
                sb.Clear();
                for (int x = 0; x < w; x++) sb.Append(Token(cells[y * w + x]));
                rows[y] = sb.ToString();
            }
            return rows;
        }

        public static byte[] ParseGates(string s, int n)
        {
            if (s == null) return new byte[n];
            if (s.Length != n) throw new FormatException($"gate string {s} must have length {n}");
            var g = new byte[n];
            for (int i = 0; i < n; i++)
            {
                char ch = s[i];
                if (ch == '.') g[i] = 0;
                else if (ch >= '1' && ch <= '6') g[i] = (byte)(ch - '0');
                else throw new FormatException($"bad gate '{ch}'");
            }
            return g;
        }

        public static Level FromRows(string[] rows, string gT = null, string gR = null, string gB = null, string gL = null, int moves = 0)
        {
            int h = rows.Length;
            int w = ParseRow(rows[0]).Length;
            var cells = new byte[w * h];
            for (int y = 0; y < h; y++)
            {
                var r = ParseRow(rows[y]);
                if (r.Length != w) throw new FormatException($"row {rows[y]} has {r.Length} cells, expected {w}");
                Array.Copy(r, 0, cells, y * w, w);
            }
            var gates = new[] { ParseGates(gT, w), ParseGates(gR, h), ParseGates(gB, w), ParseGates(gL, h) };
            return new Level(w, h, cells, gates) { Moves = moves };
        }

        /// <summary>Parse the level JSON object (docs/TECH.md §3.2) produced by MiniJson.</summary>
        public static Level FromJson(Dictionary<string, object> d)
        {
            var grid = (List<object>)d["grid"];
            var rows = new string[grid.Count];
            for (int i = 0; i < rows.Length; i++) rows[i] = (string)grid[i];
            var gates = d.TryGetValue("gates", out var go) ? (Dictionary<string, object>)go : new Dictionary<string, object>();
            string G(string k) => gates.TryGetValue(k, out var v) ? (string)v : null;
            var lv = FromRows(rows, G("T"), G("R"), G("B"), G("L"), d.TryGetValue("moves", out var m) ? Convert.ToInt32(m) : 0);
            if (d.TryGetValue("par", out var p)) lv.Par = Convert.ToInt32(p);
            if (d.TryGetValue("id", out var id)) lv.Id = (string)id;
            if (d.TryGetValue("tier", out var t)) lv.Tier = (Tier)Enum.Parse(typeof(Tier), (string)t);
            return lv;
        }
    }
}
