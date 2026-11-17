using System;
using System.Collections.Generic;

namespace TiltDrop.Core
{
    /// <summary>Main levels + daily puzzles from packs described by shared/levels/manifest.json.
    /// Packs are loaded lazily through the supplied loader (Resources, file, download...).</summary>
    public sealed class LevelCatalog
    {
        struct PackInfo { public string File; public int First, Count; }

        readonly List<PackInfo> packs = new List<PackInfo>();
        readonly Dictionary<string, LevelPack> loaded = new Dictionary<string, LevelPack>();
        readonly Func<string, byte[]> loader;
        readonly string dailyFile;

        public int LevelCount { get; }
        public int DailyCount { get; }
        public int DailyEpochDay { get; }
        public int ContentVersion { get; }

        public LevelCatalog(string manifestJson, Func<string, byte[]> loader)
        {
            this.loader = loader;
            var m = (Dictionary<string, object>)MiniJson.Parse(manifestJson);
            ContentVersion = m.Int("contentVersion", 1);
            LevelCount = m.Int("levelCount");
            foreach (var o in m.Arr("packs"))
            {
                var p = (Dictionary<string, object>)o;
                packs.Add(new PackInfo { File = p.Str("file"), First = p.Int("first"), Count = p.Int("count") });
            }
            var d = m.Obj("daily");
            if (d != null)
            {
                dailyFile = d.Str("file");
                DailyCount = d.Int("count");
                DailyEpochDay = Days.Parse(d.Str("epoch", "2027-01-01"));
            }
        }

        LevelPack Pack(string file, string prefix, int first)
        {
            if (!loaded.TryGetValue(file, out var p)) loaded[file] = p = new LevelPack(loader(file), prefix, first);
            return p;
        }

        /// <summary>Level 1..LevelCount. Move limit adjusted by remote config.</summary>
        public Level Get(int number, GameConfig cfg = null)
        {
            foreach (var pi in packs)
            {
                if (number < pi.First || number >= pi.First + pi.Count) continue;
                var lv = Pack(pi.File, "L", pi.First).Get(number - pi.First);
                if (cfg != null) lv.Moves = cfg.MoveLimit(number, lv.Par, lv.Moves);
                return lv;
            }
            throw new ArgumentOutOfRangeException(nameof(number), $"level {number} not in catalog");
        }

        public Level Daily(int index)
        {
            if (dailyFile == null) throw new InvalidOperationException("no daily pack");
            return Pack(dailyFile, "D", 1).Get(index);
        }
    }
}
