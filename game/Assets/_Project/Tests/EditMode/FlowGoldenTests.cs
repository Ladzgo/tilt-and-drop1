using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TiltDrop.Core.Flow;

namespace TiltDrop.Core.Tests
{
    public class FlowGoldenTests
    {
        static Dictionary<string, object> Load(string file)
        {
            var dir = TestPaths.Find(Path.Combine("shared", "flow"));
            Assert.IsNotNull(dir, "shared/flow not found");
            return (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(Path.Combine(dir, file)));
        }

        static string Lower(FlowStatus s) => s == FlowStatus.Won ? "won" : s == FlowStatus.Lost ? "lost" : "playing";

        [Test]
        public void TapsMatchPython()
        {
            int steps = 0;
            foreach (var co in Load("golden.json").Arr("cases"))
            {
                var c = (Dictionary<string, object>)co;
                var g = new FlowGame(FlowLevel.FromJson(c.Obj("level")));
                Assert.AreEqual(c.Str("initialStatus"), Lower(g.Status));
                foreach (var so in c.Arr("steps"))
                {
                    var s = (Dictionary<string, object>)so;
                    Assert.AreEqual(s.Bool("ok"), g.Tap(s.Int("cell")));
                    var ec = s.Arr("colour"); var ed = s.Arr("dist");
                    for (int i = 0; i < ec.Count; i++) { Assert.AreEqual(Convert.ToInt32(ec[i]), g.State.Colour[i]); Assert.AreEqual(Convert.ToInt32(ed[i]), g.State.Dist[i]); }
                    var el = s.Arr("leaks");
                    Assert.AreEqual(el.Count, g.State.Leaks.Count);
                    for (int k = 0; k < el.Count; k++) { var l = (List<object>)el[k]; Assert.AreEqual(Convert.ToInt32(l[0]), g.State.Leaks[k].cell); Assert.AreEqual(Convert.ToInt32(l[1]), g.State.Leaks[k].dir); }
                    var cups = s.Arr("cups");
                    for (int k = 0; k < cups.Count; k++) Assert.AreEqual(Convert.ToInt32(cups[k]) == 1, g.State.Cups[k]);
                    Assert.AreEqual(s.Int("movesLeft"), g.MovesLeft);
                    Assert.AreEqual(s.Str("status"), Lower(g.Status));
                    steps++;
                }
            }
            Assert.Greater(steps, 500);
        }

        [Test]
        public void SolverMatchesPython()
        {
            foreach (var co in Load("golden.json").Arr("solver"))
            {
                var c = (Dictionary<string, object>)co;
                var r = FlowRules.Solve(FlowLevel.FromJson(c.Obj("level")));
                Assert.AreEqual(c["par"] == null ? -1 : Convert.ToInt32(c["par"]), r.par);
                Assert.AreEqual(c.Long("solutions"), r.solutions);
            }
        }

        [Test]
        public void ShippedLevelsSolvableAtPar()
        {
            int n = 0;
            foreach (var o in Load("levels.json").Arr("levels"))
            {
                var d = (Dictionary<string, object>)o;
                var lv = FlowLevel.FromJson(d);
                if (n++ % 5 != 0) continue;
                Assert.AreEqual(lv.Par, FlowRules.Solve(lv).par, lv.Id);
                var g = new FlowGame(lv);
                foreach (var t in d.Obj("meta").Arr("solution")) g.Tap(Convert.ToInt32(t));
                Assert.AreEqual(FlowStatus.Won, g.Status, lv.Id);
            }
            Assert.AreEqual(200, n);
        }
    }
}
