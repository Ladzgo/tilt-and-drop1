using System;
using System.Collections.Generic;
using UnityEngine;

namespace TiltDrop.Game
{
    /// <summary>Procedural sprites (signed-distance rasterized, anti-aliased) so the project needs no art to run.</summary>
    public static class Sprites
    {
        const int Size = 128;
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite RoundedSquare => Get("rsq", (x, y) => SdRoundBox(x, y, 0.86f, 0.22f));
        public static Sprite Square => Get("sq", (x, y) => SdRoundBox(x, y, 1f, 0.0f));
        public static Sprite Circle => Get("circle", (x, y) => Mathf.Sqrt(x * x + y * y) - 0.9f);
        public static Sprite Ring => Get("ring", (x, y) => Mathf.Abs(Mathf.Sqrt(x * x + y * y) - 0.62f) - 0.12f);
        public static Sprite Soft => Get("soft", (x, y) => Mathf.Sqrt(x * x + y * y) - 0.2f, 0.8f);
        public static Sprite Star => Symbol(5);

        /// <summary>Color symbols 1..6: circle, triangle, square, diamond, star, cross.</summary>
        public static Sprite Symbol(int color)
        {
            switch (color)
            {
                case 1: return Get("s1", (x, y) => Mathf.Sqrt(x * x + y * y) - 0.42f);
                case 2: return Get("s2", (x, y) => SdTriangle(x, y + 0.08f, 0.5f));
                case 3: return Get("s3", (x, y) => SdRoundBox(x, y, 0.72f, 0.08f));
                case 4: return Get("s4", (x, y) => (Mathf.Abs(x) + Mathf.Abs(y)) * 0.7071f - 0.38f);
                case 5: return Get("s5", (x, y) => SdStar(x, y + 0.04f, 0.52f, 0.42f));
                default: return Get("s6", (x, y) => Mathf.Min(SdRoundBox(x, y, 0.28f, 0.04f, 0.9f), SdRoundBox(x, y, 0.9f, 0.04f, 0.28f)));
            }
        }

        static Sprite Get(string key, Func<float, float, float> sdf, float soft = 0f)
        {
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = key };
            var px = new Color32[Size * Size];
            float aa = 2.5f / Size;
            for (int j = 0; j < Size; j++)
            for (int i = 0; i < Size; i++)
            {
                float x = (i + 0.5f) / Size * 2f - 1f, y = (j + 0.5f) / Size * 2f - 1f;
                float d = sdf(x, y);
                float a = soft > 0f ? Mathf.Clamp01(1f - (d + 0.2f) / soft) : Mathf.Clamp01(0.5f - d / aa);
                if (soft > 0f) a *= a;
                px[j * Size + i] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
            s.name = key;
            Cache[key] = s;
            return s;
        }

        static float SdRoundBox(float x, float y, float size, float r, float sizeY = -1f)
        {
            float hx = size - r, hy = (sizeY < 0 ? size : sizeY) - r;
            float qx = Mathf.Abs(x) - hx, qy = Mathf.Abs(y) - hy;
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        static float SdTriangle(float x, float y, float r)
        {
            const float k = 1.7320508f;
            x = Mathf.Abs(x) - r;
            y = -y + r / k;
            if (x + k * y > 0f) { float nx = (x - k * y) / 2f, ny = (-k * x - y) / 2f; x = nx; y = ny; }
            x -= Mathf.Clamp(x, -2f * r, 0f);
            return -Mathf.Sqrt(x * x + y * y) * Mathf.Sign(y);
        }

        static float SdStar(float x, float y, float r, float rf)
        {
            // 5-point star (Inigo Quilez), y up
            float k1x = 0.809016994f, k1y = -0.587785252f, k2x = -k1x, k2y = k1y;
            x = Mathf.Abs(x);
            y = -y;
            float d1 = 2f * Mathf.Max(k1x * x + k1y * y, 0f);
            x -= d1 * k1x; y -= d1 * k1y;
            float d2 = 2f * Mathf.Max(k2x * x + k2y * y, 0f);
            x -= d2 * k2x; y -= d2 * k2y;
            x = Mathf.Abs(x);
            y -= r;
            float bax = rf * -k1y - 0f, bay = rf * k1x - 1f;
            float h = Mathf.Clamp((x * bax + y * bay) / (bax * bax + bay * bay), 0f, r);
            float dx = x - bax * h, dy = y - bay * h;
            return Mathf.Sqrt(dx * dx + dy * dy) * Mathf.Sign(dy * bax - dx * bay);
        }
    }
}
