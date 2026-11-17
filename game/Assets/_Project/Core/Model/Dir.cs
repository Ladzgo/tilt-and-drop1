namespace TiltDrop.Core
{
    /// <summary>Gravity direction in board coordinates. CW rotation = +1 (mod 4).</summary>
    public enum Dir : byte { Down = 0, Right = 1, Up = 2, Left = 3 }

    public enum Rot : byte { CW = 0, CCW = 1 }

    public static class Dirs
    {
        public static readonly int[] DX = { 0, 1, 0, -1 };
        public static readonly int[] DY = { 1, 0, -1, 0 };
        public const string Names = "DRUL";

        /// <summary>Edge index (T=0,R=1,B=2,L=3) that a gravity direction points into.</summary>
        public static int GravityEdge(Dir g)
        {
            switch (g)
            {
                case Dir.Down: return Edges.B;
                case Dir.Right: return Edges.R;
                case Dir.Up: return Edges.T;
                default: return Edges.L;
            }
        }

        public static Dir Rotate(Dir g, Rot r) => r == Rot.CW ? (Dir)(((int)g + 1) & 3) : (Dir)(((int)g + 3) & 3);
    }

    public static class Edges
    {
        public const int T = 0, R = 1, B = 2, L = 3;
        public const string Names = "TRBL";
    }
}
