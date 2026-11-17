using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TiltDrop.Core.Prism;

namespace TiltDrop.Core.Tests
{
    public class PrismGoldenTests
    {
        static Dictionary<string, object> Load()
        {
            var dir = TestPaths.Find(Path.Combine("shared", "prism"));
            Assert.IsNotNull(dir, "shared/prism not found");
            return (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(Path.Combine(dir, "golden.json")));
        }

        static string StatusName(Status s) => s == Status.Playing ? "playing" : s == Status.Won ? "won" : "lost";
        static double R2(double v) => Math.Round(v * 100, MidpointRounding.ToEven) / 100.0;

        [Test]
        public void TracesAndStatesMatchPython()
        {
            int steps = 0;
            foreach (var co in Load().Arr("cases"))
            {
                var c = (Dictionary<string, object>)co;
                var lv = PrismLevel.FromJson(c.Obj("level"));
                var g = new PrismGame(lv);
                Assert.AreEqual(c.Str("initialStatus"), StatusName(g.Status));
                foreach (var so in c.Arr("steps"))
                {
                    var s = (Dictionary<string, object>)so;
                    bool spent = g.Tap(s.Int("tap"));
                    Assert.AreEqual(s.Bool("spent"), spent);
                    Assert.AreEqual(s.Int("movesLeft"), g.MovesLeft);
                    Assert.AreEqual(s.Str("status"), StatusName(g.Status));
                    var rec = s.Arr("received");
                    for (int k = 0; k < lv.Targets.Count; k++) Assert.AreEqual(Convert.ToInt32(rec[k]), g.Received[lv.Targets[k]]);
                    var segs = g.Segments();
                    var exp = s.Arr("segments");
                    Assert.AreEqual(exp.Count, segs.Count, "segment count");
                    for (int k = 0; k < segs.Count; k++)
                    {
                        var e = (List<object>)exp[k];
                        Assert.AreEqual(Convert.ToInt32(e[0]), segs[k].Color);
                        Assert.AreEqual(Convert.ToDouble(e[1]), R2(segs[k].X0), 1e-9);
                        Assert.AreEqual(Convert.ToDouble(e[2]), R2(segs[k].Y0), 1e-9);
                        Assert.AreEqual(Convert.ToDouble(e[3]), R2(segs[k].X1), 1e-9);
                        Assert.AreEqual(Convert.ToDouble(e[4]), R2(segs[k].Y1), 1e-9);
                    }
                    steps++;
                }
            }
            Assert.Greater(steps, 500);
        }

        [Test]
        public void SolverMatchesPython()
        {
            foreach (var co in Load().Arr("solver"))
            {
                var c = (Dictionary<string, object>)co;
                var lv = PrismLevel.FromJson(c.Obj("level"));
                var s = PrismRules.Solve(lv);
                int par = c["par"] == null ? -1 : Convert.ToInt32(c["par"]);
                Assert.AreEqual(par, s.Par);
                Assert.AreEqual(c.Int("solutions"), s.Solutions);
            }
        }

        [Test]
        public void ShippedLevelsSolvableAtPar()
        {
            var dir = TestPaths.Find(Path.Combine("shared", "prism"));
            var root = (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(Path.Combine(dir, "levels.json")));
            int n = 0;
            foreach (var o in root.Arr("levels"))
            {
                var lv = PrismLevel.FromJson((Dictionary<string, object>)o);
                Assert.AreEqual(lv.Par, PrismRules.Solve(lv).Par, lv.Id);
                Assert.GreaterOrEqual(lv.Moves, lv.Par);
                n++;
            }
            Assert.AreEqual(200, n);
        }
    }
}
