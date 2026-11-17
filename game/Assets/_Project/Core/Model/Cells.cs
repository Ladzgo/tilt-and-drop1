namespace TiltDrop.Core
{
    /// <summary>Cell byte encoding (docs/TECH.md §3.1): high nibble = kind, low nibble = color 1..6.</summary>
    public static class Cells
    {
        public const byte Empty = 0x00;
        public const byte Wall = 0x10;
        public const byte Block = 0x20;
        public const byte Stone = 0x30;
        public const byte Anchor = 0x40;
        public const byte Ice2 = 0x50;
        public const byte Ice1 = 0x60;
        public const int MaxColors = 6;

        public static int Kind(byte c) => c & 0xF0;
        public static int Color(byte c) => c & 0x0F;

        public static bool IsMovable(byte c)
        {
            int k = c & 0xF0;
            return k == Block || k == Stone;
        }

        /// <summary>Counts toward the win condition (block, anchor, ice).</summary>
        public static bool IsColored(byte c)
        {
            int k = c & 0xF0;
            return k == Block || k == Anchor || k == Ice2 || k == Ice1;
        }

        public static bool IsPiece(byte c) => c != Empty && c != Wall;
    }
}
