using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TiltDrop.Core.Fold;

namespace TiltDrop.Core.Tests
{
    public class FoldGoldenTests
    {
        static Dictionary<string, object> Load(string file)
        {
            var dir = TestPaths.Find(Path.Combine("shared", "fold"));
            Assert.IsNotNull(dir, "shared/fold not found");
            return (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(Path.Combine(dir, file)));
        }

        [Test]
        public void FoldsMatchPython()
        {
            int steps = 0;
            foreach (var co in Load("golden.json").Arr("cases"))
            {
                var c = (Dictionary<string, object>)co;
                var lv = FoldLevel.FromJson(c.Obj("level"));
                var sh = FoldRules.Initial(lv);
                int movesLeft = lv.Moves;
                foreach (var so in c.Arr("steps"))
                {
                    var s = (Dictionary<string, object>)so;
                    var mv = s.Arr("move");
                    var r = FoldRules.Fold(sh, ((string)mv[0])[0], Convert.ToInt32(mv[1]), Convert.ToInt32(mv[2]));
                    bool ok = r != null;
                    if (ok) { sh = r; movesLeft--; }
                    Assert.AreEqual(s.Bool("ok"), ok);
                    var vis = FoldRules.Visible(lv, sh);
                    var ev = s.Arr("visible");
                    Assert.AreEqual(ev.Count, vis.Length);
                    for (int i = 0; i < vis.Length; i++) Assert.AreEqual((string)ev[i], vis[i]);
                    var es = s.Arr("stacks");
                    for (int y = 0; y < es.Count; y++)
                    {
                        var row = (List<object>)es[y];
                        for (int x = 0; x < row.Count; x++)
                        {
                            var st = (List<object>)row[x];
                            Assert.AreEqual(st.Count, sh[y, x].Count);
                            for (int k = 0; k < st.Count; k++)
                            {
                                var e = (List<object>)st[k];
                                Assert.AreEqual(Convert.ToInt32(e[0]), sh[y, x][k].s);
                                Assert.AreEqual(Convert.ToInt32(e[1]), sh[y, x][k].f);
                            }
                        }
                    }
                    string status = FoldRules.Solved(lv, sh) ? "won" : movesLeft <= 0 ? "lost" : "playing";
                    Assert.AreEqual(s.Str("status"), status);
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
                var r = FoldRules.Solve(FoldLevel.FromJson(c.Obj("level")));
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
                var lv = FoldLevel.FromJson((Dictionary<string, object>)o);
                if (n++ % 4 == 0) Assert.AreEqual(lv.Par, FoldRules.Solve(lv).par, lv.Id);
            }
            Assert.AreEqual(200, n);
        }
    }
}
