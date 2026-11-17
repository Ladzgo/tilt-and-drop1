using UnityEngine;

namespace TiltDrop.Game
{
    /// <summary>Colorblind-aware palette; every color also has a symbol (GDD §3).</summary>
    public static class Palette
    {
        public static readonly Color[] Block =
        {
            new Color(0, 0, 0, 0),
            Hex(0xE5484D), // 1 red      ●
            Hex(0x3E8EF7), // 2 blue     ▲
            Hex(0xF5C400), // 3 yellow   ■
            Hex(0x30A46C), // 4 green    ◆
            Hex(0x8E4EC6), // 5 purple   ★
            Hex(0xF76B15), // 6 orange   ✚
        };

        public static readonly Color Background = Hex(0x15151C);
        public static readonly Color BoardBg = Hex(0x23232E);
        public static readonly Color BoardCell = Hex(0x2B2B38);
        public static readonly Color Wall = Hex(0x4A4A5C);
        public static readonly Color Stone = Hex(0x8C8379);
        public static readonly Color Ice = new Color(0.78f, 0.92f, 1f, 0.55f);
        public static readonly Color Text = Hex(0xF2F2F7);
        public static readonly Color TextDim = Hex(0x9A9AAA);
        public static readonly Color Panel = Hex(0x1F1F29);
        public static readonly Color Button = Hex(0x3E8EF7);
        public static readonly Color ButtonAlt = Hex(0x34343F);
        public static readonly Color Gold = Hex(0xFFC53D);
        public static readonly Color Good = Hex(0x30A46C);

        public static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

        public static Color Darker(Color c, float k = 0.75f) => new Color(c.r * k, c.g * k, c.b * k, c.a);
    }
}
