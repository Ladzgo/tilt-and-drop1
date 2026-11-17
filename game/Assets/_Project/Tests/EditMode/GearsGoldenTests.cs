using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TiltDrop.Core.Gears;

namespace TiltDrop.Core.Tests
{
    public class GearsGoldenTests
    {
        static Dictionary<string, object> Load(string file)
        {
            var dir = TestPaths.Find(Path.Combine("shared", "gears"));
            Assert.IsNotNull(dir, "shared/gears not found");
            return (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(Path.Combine(dir, file)));
        }

        [Test]
        public void TapsAndSpinsMatchPython()
        {
            int steps = 0;
            foreach (var co in Load("golden.json").Arr("cases"))
            {
                var c = (Dictionary<string, object>)co;
                var lv = GearLevel.FromJson(c.Obj("level"));
                int layout = lv.StartLayout(), movesLeft = lv.Moves;
                string St() => GearRules.IsWin(lv, layout) ? "won" : movesLeft <= 0 ? "lost" : "playing";
                Assert.AreEqual(c.Str("initialStatus"), St());
                foreach (var so in c.Arr("steps"))
                {
                    var s = (Dictionary<string, object>)so;
                    int cell = s.Int("cell");
                    bool ok = false;
                    if (lv.PegIndex.TryGetValue(cell, out int k))
                    {
                        int inHand = lv.Inventory;
                        for (int b = 0; b < lv.Pegs.Count; b++) if (((layout >> b) & 1) != 0) inHand--;
                        if (((layout >> k) & 1) != 0 || inHand > 0) { layout ^= 1 << k; movesLeft--; ok = true; }
                    }
                    Assert.AreEqual(s.Bool("ok"), ok);
                    Assert.AreEqual(s.Int("layout"), layout);
                    GearRules.Spins(lv, layout, out var spin, out var jam);
                    var es = s.Arr("spin"); var ej = s.Arr("jam");
                    for (int i = 0; i < spin.Length; i++) { Assert.AreEqual(Convert.ToInt32(es[i]), spin[i]); Assert.AreEqual(Convert.ToInt32(ej[i]) == 1, jam[i]); }
                    Assert.AreEqual(s.Int("movesLeft"), movesLeft);
                    Assert.AreEqual(s.Str("status"), St());
                    steps++;
                }
            }
            Assert.Greater(steps, 1000);
        }

        [Test]
        public void SolverMatchesPython()
        {
            foreach (var co in Load("golden.json").Arr("solver"))
            {
                var c = (Dictionary<string, object>)co;
                var lv = GearLevel.FromJson(c.Obj("level"));
                var r = GearRules.Solve(lv, lv.StartLayout());
                Assert.AreEqual(c["par"] == null ? -1 : Convert.ToInt32(c["par"]), r.par);
                Assert.AreEqual(c.Int("solutions"), r.solutions);
            }
        }

        [Test]
        public void ShippedLevelsSolvableAtPar()
        {
            int n = 0;
            foreach (var o in Load("levels.json").Arr("levels"))
            {
                var lv = GearLevel.FromJson((Dictionary<string, object>)o);
                Assert.AreEqual(lv.Par, GearRules.Solve(lv, lv.StartLayout()).par, lv.Id);
                n++;
            }
            Assert.AreEqual(200, n);
        }
    }
}
