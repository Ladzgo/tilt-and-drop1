using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TiltDrop.Core.Domino;

namespace TiltDrop.Core.Tests
{
    public class DominoGoldenTests
    {
        static Dictionary<string, object> Load(string file)
        {
            var dir = TestPaths.Find(Path.Combine("shared", "domino"));
            Assert.IsNotNull(dir, "shared/domino not found");
            return (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(Path.Combine(dir, file)));
        }

        [Test]
        public void PushesAndRotationsMatchPython()
        {
            int steps = 0;
            foreach (var co in Load("golden.json").Arr("cases"))
            {
                var c = (Dictionary<string, object>)co;
                var lv = DominoLevel.FromJson(c.Obj("level"));
                var cfg = lv.StartConfig();
                int attempts = lv.Attempts;
                string status = "playing";
                foreach (var so in c.Arr("steps"))
                {
                    var s = (Dictionary<string, object>)so;
                    if (s["act"] is string a && a == "P")
                    {
                        var rung = new HashSet<int>();
                        var falls = DominoRules.RunChain(lv, cfg, rung);
                        attempts--;
                        bool all = true;
                        foreach (int b in lv.Bells) if (!rung.Contains(b)) all = false;
                        status = all ? "won" : attempts <= 0 ? "lost" : "playing";
                        var ef = s.Arr("falls");
                        Assert.AreEqual(ef.Count, falls.Count);
                        for (int i = 0; i < falls.Count; i++)
                        {
                            var e = (List<object>)ef[i];
                            Assert.AreEqual(Convert.ToInt32(e[0]), falls[i].T);
                            Assert.AreEqual(Convert.ToInt32(e[1]), falls[i].Cell);
                            var eo = (List<object>)e[2];
                            Assert.AreEqual(eo.Count, falls[i].Outs.Length);
                            for (int j = 0; j < eo.Count; j++) Assert.AreEqual(Convert.ToInt32(eo[j]), falls[i].Outs[j]);
                        }
                        var er = s.Arr("rung");
                        Assert.AreEqual(er.Count, rung.Count);
                        Assert.AreEqual(s.Int("attempts"), attempts);
                    }
                    else
                    {
                        int cell = s.Int("act"), k = lv.Turners.IndexOf(cell);
                        bool ok = k >= 0;
                        if (ok) cfg[k] = (cfg[k] + 1) & 3;
                        Assert.AreEqual(s.Bool("ok"), ok);
                        var ec = s.Arr("config");
                        for (int i = 0; i < cfg.Length; i++) Assert.AreEqual(Convert.ToInt32(ec[i]), cfg[i]);
                    }
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
                var lv = DominoLevel.FromJson(c.Obj("level"));
                var r = DominoRules.Solve(lv, lv.StartConfig());
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
                var lv = DominoLevel.FromJson((Dictionary<string, object>)o);
                Assert.AreEqual(lv.Par, DominoRules.Solve(lv, lv.StartConfig()).par, lv.Id);
                n++;
            }
            Assert.AreEqual(200, n);
        }
    }
}
