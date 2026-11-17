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
        public static Sprite ArcArrow => Get("arc", SdArcArrow);

        /// <summary>9-sliced rounded rectangle for UI panels and buttons.</summary>
        public static Sprite Panel
        {
            get
            {
                if (Cache.TryGetValue("panel", out var s) && s != null) return s;
                const int n = 64, r = 24;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "panel" };
                var px = new Color32[n * n];
                for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float cx = Mathf.Clamp(i + 0.5f, r, n - r), cy = Mathf.Clamp(j + 0.5f, r, n - r);
                    float d = Mathf.Sqrt((i + 0.5f - cx) * (i + 0.5f - cx) + (j + 0.5f - cy) * (j + 0.5f - cy)) - (r - 1);
                    px[j * n + i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255f));
                }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r + 2, r + 2, r + 2, r + 2));
                s.name = "panel";
                Cache["panel"] = s;
                return s;
            }
        }

        /// <summary>Color symbols 1..6: circle, triangle, square, diamond, star, cross.</summary>
        public static Sprite Symbol(int color)
        {
            switch (color)
            {
                case 1: return Get("s1", (x, y) => Mathf.Sqrt(x * x + y * y) - 0.42f);
                case 2: return Get("s2", (x, y) => SdTriangle(x, -y - 0.06f, 0.5f));
                case 3: return Get("s3", (x, y) => SdRoundBox(x, y, 0.4f, 0.06f));
                case 4: return Get("s4", (x, y) => (Mathf.Abs(x) + Mathf.Abs(y)) * 0.7071f - 0.38f);
                case 5: return Get("s5", (x, y) => SdStar(x, -y + 0.03f, 0.56f, 0.45f));
                default: return Get("s6", (x, y) => Mathf.Min(SdRoundBox(x, y, 0.15f, 0.03f, 0.5f), SdRoundBox(x, y, 0.5f, 0.03f, 0.15f)));
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

        static float SdArcArrow(float x, float y)
        {
            // clockwise circular arrow: ring with a gap between 62° and 108°, head at 108° pointing clockwise
            const float r = 0.58f, th = 0.13f;
            float a = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
            float d = Mathf.Abs(Mathf.Sqrt(x * x + y * y) - r) - th;
            if (a > 62f && a < 108f) d = Mathf.Max(d, 0.05f);
            float ha = 108f * Mathf.Deg2Rad;
            float hx = Mathf.Cos(ha) * r, hy = Mathf.Sin(ha) * r;
            float tx = Mathf.Sin(ha), ty = -Mathf.Cos(ha);
            float lx = x - hx, ly = y - hy;
            float u = lx * tx + ly * ty, v = -lx * ty + ly * tx;
            float head = Mathf.Max(Mathf.Abs(v) * 0.9f + u * 1.1f - 0.34f, -u - 0.04f);
            return Mathf.Min(d, head);
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
