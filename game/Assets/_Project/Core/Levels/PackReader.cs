using System;
using System.IO;
using System.IO.Compression;

namespace TiltDrop.Core
{
    /// <summary>Reads `.tdp` level packs (docs/TECH.md §3.3), optionally gzip-compressed.</summary>
    public sealed class LevelPack
    {
        readonly byte[] data;
        readonly int count, indexOffset;
        readonly string idPrefix;
        readonly int firstNumber;

        public int Count => count;

        public LevelPack(byte[] bytes, string idPrefix = "L", int firstNumber = 1)
        {
            if (bytes.Length > 2 && bytes[0] == 0x1f && bytes[1] == 0x8b) bytes = Gunzip(bytes);
            if (bytes.Length < 20 || bytes[0] != 'T' || bytes[1] != 'D' || bytes[2] != 'P' || bytes[3] != '1') throw new InvalidDataException("not a TDP pack");
            uint crc = BitConverter.ToUInt32(bytes, bytes.Length - 4);
            if (Crc32.Compute(bytes, 0, bytes.Length - 4) != crc) throw new InvalidDataException("pack CRC mismatch");
            int version = BitConverter.ToUInt16(bytes, 4);
            if (version != 1) throw new InvalidDataException($"unsupported pack version {version}");
            count = (int)BitConverter.ToUInt32(bytes, 8);
            indexOffset = (int)BitConverter.ToUInt32(bytes, 12);
            data = bytes;
            this.idPrefix = idPrefix;
            this.firstNumber = firstNumber;
        }

        public Level Get(int i)
        {
            if (i < 0 || i >= count) throw new ArgumentOutOfRangeException(nameof(i));
            int off = (int)BitConverter.ToUInt32(data, indexOffset + 4 * i);
            int w = data[off] >> 4, h = data[off] & 0xF;
            int moves = data[off + 1], par = data[off + 2], tier = data[off + 3], mech = data[off + 4];
            off += 5;
            var cells = new byte[w * h];
            Buffer.BlockCopy(data, off, cells, 0, w * h);
            off += w * h;
            var nib = new byte[2 * (w + h)];
            for (int k = 0; k < w + h; k++) { nib[2 * k] = (byte)(data[off + k] >> 4); nib[2 * k + 1] = (byte)(data[off + k] & 0xF); }
            var gates = new byte[4][];
            gates[Edges.T] = Slice(nib, 0, w);
            gates[Edges.R] = Slice(nib, w, h);
            gates[Edges.B] = Slice(nib, w + h, w);
            gates[Edges.L] = Slice(nib, 2 * w + h, h);
            return new Level(w, h, cells, gates) { Moves = moves, Par = par, Tier = (Tier)tier, MechanicsMask = mech, Id = idPrefix + (firstNumber + i) };
        }

        static byte[] Slice(byte[] a, int off, int n) { var r = new byte[n]; Array.Copy(a, off, r, 0, n); return r; }

        static byte[] Gunzip(byte[] gz)
        {
            using (var src = new MemoryStream(gz))
            using (var z = new GZipStream(src, CompressionMode.Decompress))
            using (var dst = new MemoryStream())
            {
                z.CopyTo(dst);
                return dst.ToArray();
            }
        }
    }

    public static class Crc32
    {
        static readonly uint[] Table = Build();

        static uint[] Build()
        {
            var t = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                t[i] = c;
            }
            return t;
        }

        public static uint Compute(byte[] buf, int off, int len)
        {
            uint c = 0xFFFFFFFFu;
            for (int i = off; i < off + len; i++) c = Table[(c ^ buf[i]) & 0xFF] ^ (c >> 8);
            return c ^ 0xFFFFFFFFu;
        }
    }
}
