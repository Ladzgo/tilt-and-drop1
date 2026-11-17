using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TiltDrop.Core.Roll;

namespace TiltDrop.Core.Tests
{
    public class RollGoldenTests
    {
        static Dictionary<string, object> Load(string file)
        {
            var dir = TestPaths.Find(Path.Combine("shared", "roll"));
            Assert.IsNotNull(dir, "shared/roll not found");
            return (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(Path.Combine(dir, file)));
        }

        [Test]
        public void StepsMatchPython()
        {
            int steps = 0;
            foreach (var co in Load("golden.json").Arr("cases"))
            {
                var c = (Dictionary<string, object>)co;
                var lv = RollLevel.FromJson(c.Obj("level"));
                var st = RollRules.Start(lv);
                int movesLeft = lv.Moves;
                string status = RollRules.Solved(lv, st) ? "won" : movesLeft <= 0 ? "lost" : "playing";
                Assert.AreEqual(c.Str("initialStatus"), status);
                foreach (var so in c.Arr("steps"))
                {
                    var s = (Dictionary<string, object>)so;
                    var trail = new List<int>();
                    bool ok = RollRules.Step(lv, ref st, s.Int("dir"), trail);
                    if (ok) movesLeft--;
                    status = RollRules.Solved(lv, st) ? "won" : movesLeft <= 0 ? "lost" : "playing";
                    Assert.AreEqual(s.Bool("ok"), ok);
                    var pos = s.Arr("pos");
                    Assert.AreEqual(Convert.ToInt32(pos[0]), st.X);
                    Assert.AreEqual(Convert.ToInt32(pos[1]), st.Y);
                    Assert.AreEqual(s.Str("faces"), string.Concat(Array.ConvertAll(st.Faces, f => f.ToString())));
                    var pr = s.Arr("prints");
                    for (int k = 0; k < pr.Count; k++) Assert.AreEqual(Convert.ToInt32(pr[k]), st.Prints[k]);
                    var tr = s.Arr("trail");
                    Assert.AreEqual(tr.Count, trail.Count);
                    for (int k = 0; k < tr.Count; k++) Assert.AreEqual(Convert.ToInt32(tr[k]), trail[k]);
                    Assert.AreEqual(s.Int("movesLeft"), movesLeft);
                    Assert.AreEqual(s.Str("status"), status);
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
                var lv = RollLevel.FromJson(c.Obj("level"));
                var r = RollRules.Solve(lv, RollRules.Start(lv), 14);
                int par = c["par"] == null ? -1 : Convert.ToInt32(c["par"]);
                Assert.AreEqual(par, r.par);
                Assert.AreEqual(c.Long("solutions"), r.solutions);
            }
        }

        [Test]
        public void ShippedLevelsSolvableAtPar()
        {
            int n = 0;
            foreach (var o in Load("levels.json").Arr("levels"))
            {
                var lv = RollLevel.FromJson((Dictionary<string, object>)o);
                if (n++ % 5 != 0) continue;
                Assert.AreEqual(lv.Par, RollRules.Solve(lv, RollRules.Start(lv)).par, lv.Id);
                Assert.GreaterOrEqual(lv.Moves, lv.Par);
            }
            Assert.AreEqual(200, n);
        }
    }
}
