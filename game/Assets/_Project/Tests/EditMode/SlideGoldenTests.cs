using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TiltDrop.Core.Slide;

namespace TiltDrop.Core.Tests
{
    public class SlideGoldenTests
    {
        static Dictionary<string, object> Load(string file)
        {
            var dir = TestPaths.Find(Path.Combine("shared", "slide"));
            Assert.IsNotNull(dir, "shared/slide not found");
            return (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(Path.Combine(dir, file)));
        }

        static string Name(Status s) => s == Status.Playing ? "playing" : s == Status.Won ? "won" : "lost";

        [Test]
        public void TapsMatchPython()
        {
            int steps = 0;
            foreach (var co in Load("golden.json").Arr("cases"))
            {
                var c = (Dictionary<string, object>)co;
                var lv = SlideLevel.FromJson(c.Obj("level"));
                uint removed = 0;
                var tray = new List<int>();
                foreach (var so in c.Arr("steps"))
                {
                    var s = (Dictionary<string, object>)so;
                    int cell = s.Int("cell");
                    string res;
                    if (!lv.Index.TryGetValue(cell, out int k) || ((removed >> k) & 1) != 0) res = null;
                    else res = SlideRules.Tap(lv, ref removed, tray, k, out bool m) ? (m ? "match" : "exit") : "bump";
                    Assert.AreEqual(s.Str("result"), res);
                    Assert.AreEqual(s.Long("removed"), (long)removed);
                    var exp = s.Arr("tray");
                    Assert.AreEqual(exp.Count, tray.Count);
                    for (int i = 0; i < exp.Count; i++) Assert.AreEqual(Convert.ToInt32(exp[i]), tray[i]);
                    Assert.AreEqual(s.Str("status"), Name(SlideRules.StatusOf(lv, removed, tray, lv.Capacity)));
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
                var r = SlideRules.Solve(SlideLevel.FromJson(c.Obj("level")));
                Assert.AreEqual(c.Bool("solvable"), r.solvable);
                Assert.AreEqual(c.Int("states"), r.states);
                var order = c.Arr("order");
                Assert.AreEqual(order.Count, r.order.Count);
                for (int i = 0; i < order.Count; i++) Assert.AreEqual(Convert.ToInt32(order[i]), r.order[i]);
            }
        }

        [Test]
        public void ShippedLevelsSolvable()
        {
            int n = 0;
            foreach (var o in Load("levels.json").Arr("levels"))
            {
                if (n++ % 5 != 0) continue;
                Assert.IsTrue(SlideRules.Solve(SlideLevel.FromJson((Dictionary<string, object>)o)).solvable);
            }
            Assert.AreEqual(200, n);
        }
    }
}
