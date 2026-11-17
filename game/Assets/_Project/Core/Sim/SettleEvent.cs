namespace TiltDrop.Core
{
    public enum SettleEventType : byte { Move, Exit, Hit, Release, Hammer }

    /// <summary>One step of a settle trace. The view plays these back in order.</summary>
    public struct SettleEvent
    {
        public SettleEventType Type;
        public int Pass;
        public int PieceId;
        public int From;   // cell index (Move, Hammer)
        public int To;     // cell index (Move)
        public int Edge;   // Exit
        public int Lane;   // Exit
        public int Hp;     // Hit (0 = thawed)

        /// <summary>Compact form shared with the Python golden vectors.</summary>
        public string ToGolden(int w)
        {
            switch (Type)
            {
                case SettleEventType.Move: return $"M{Pass}:{PieceId}:{From % w},{From / w}>{To % w},{To / w}";
                case SettleEventType.Exit: return $"X{Pass}:{PieceId}:{Edges.Names[Edge]}{Lane}";
                case SettleEventType.Hit: return $"H{Pass}:{PieceId}:{Hp}";
                case SettleEventType.Release: return $"R{Pass}:{PieceId}";
                default: return $"K:{PieceId}:{From % w},{From / w}";
            }
        }

        public override string ToString() => ToGolden(8);
    }
}
