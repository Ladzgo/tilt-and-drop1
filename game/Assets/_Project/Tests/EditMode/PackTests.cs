using System.IO;
using NUnit.Framework;

namespace TiltDrop.Core.Tests
{
    public class PackTests
    {
        [Test]
        public void ShippedPacksLoadAndLevelsAreValid()
        {
            var dir = TestPaths.Find(Path.Combine("shared", "levels"));
            if (dir == null || !File.Exists(Path.Combine(dir, "manifest.json"))) Assert.Ignore("level packs not generated yet");
            var cat = new LevelCatalog(File.ReadAllText(Path.Combine(dir, "manifest.json")), f => File.ReadAllBytes(Path.Combine(dir, f)));
            Assert.GreaterOrEqual(cat.LevelCount, 300);
            for (int n = 1; n <= cat.LevelCount; n += n < 300 ? 1 : 97)
            {
                var lv = cat.Get(n);
                Assert.AreEqual("L" + n, lv.Id);
                Assert.IsTrue(Settler.IsStable(lv), $"level {n} unstable");
                Assert.GreaterOrEqual(lv.Moves, lv.Par);
                if (n <= 300 || n % 5 == 0)
                {
                    var r = Solver.Solve(lv, lv.Moves - lv.Par);
                    Assert.AreEqual(lv.Par, r.MinMoves, $"level {n} par");
                }
            }
            if (cat.DailyCount > 0) Assert.IsTrue(Settler.IsStable(cat.Daily(0)));
        }

        [Test]
        public void CorruptPackRejected()
        {
            var bytes = new byte[32];
            bytes[0] = (byte)'T'; bytes[1] = (byte)'D'; bytes[2] = (byte)'P'; bytes[3] = (byte)'1';
            Assert.Throws<InvalidDataException>(() => new LevelPack(bytes));
            Assert.Throws<InvalidDataException>(() => new LevelPack(new byte[32]));
        }
    }
}
