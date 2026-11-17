using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace TiltDrop.Core.Tests
{
    /// <summary>Replays /shared/golden vectors produced by the Python simulator; traces must be byte-identical.</summary>
    public class GoldenTests
    {
        static Dictionary<string, object> Load(string name)
        {
            var dir = TestPaths.Find(Path.Combine("shared", "golden"));
            Assert.IsNotNull(dir, "shared/golden not found");
            return (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(Path.Combine(dir, name)));
        }

        static void Apply(GameState g, string input, List<SettleEvent> events)
        {
            if (input == "CW") events.AddRange(g.Rotate(Rot.CW));
            else if (input == "CCW") events.AddRange(g.Rotate(Rot.CCW));
            else if (input.StartsWith("H:", StringComparison.Ordinal))
            {
                var xy = input.Substring(2).Split(',');
                events.AddRange(g.Hammer(int.Parse(xy[0]), int.Parse(xy[1])));
            }
            else if (input.StartsWith("+", StringComparison.Ordinal)) g.AddMoves(int.Parse(input.Substring(1)));
            else throw new ArgumentException(input);
        }

        static string StatusName(Status s) => s == Status.Playing ? "playing" : s == Status.Won ? "won" : "lost";

        static int RunCases(string file)
        {
            var root = Load(file);
            int n = 0;
            foreach (var co in root.Arr("cases"))
            {
                var c = (Dictionary<string, object>)co;
                string name = c.Str("name");
                var lv = Level.FromJson(c.Obj("level"));
                var g = new GameState(lv);
                var init = c.Obj("initial");
                Assert.AreEqual(init.Str("status"), StatusName(g.Status), $"{name}: initial status");
                foreach (var so in c.Arr("steps"))
                {
                    var s = (Dictionary<string, object>)so;
                    var ev = new List<SettleEvent>();
                    Apply(g, s.Str("input"), ev);
                    var expected = s.Arr("events");
                    Assert.AreEqual(expected.Count, ev.Count, $"{name}: event count after {s.Str("input")}");
                    for (int i = 0; i < ev.Count; i++) Assert.AreEqual((string)expected[i], ev[i].ToGolden(lv.W), $"{name}: event {i}");
                    Assert.AreEqual(s.Str("gravity"), Dirs.Names[(int)g.Gravity].ToString(), $"{name}: gravity");
                    Assert.AreEqual(s.Int("movesLeft"), g.MovesLeft, $"{name}: movesLeft");
                    Assert.AreEqual(s.Str("status"), StatusName(g.Status), $"{name}: status");
                    var grid = s.Arr("grid");
                    if (grid != null) CollectionAssert.AreEqual(grid.ConvertAll(o => (string)o), g.Rows(), $"{name}: grid");
                }
                n++;
            }
            return n;
        }

        [Test] public void RuleCasesMatchPython() => Assert.GreaterOrEqual(RunCases("rules.json"), 12);

        [Test] public void RandomSequencesMatchPython() => Assert.GreaterOrEqual(RunCases("random.json"), 1000);

        [Test]
        public void SolverMatchesPython()
        {
            var root = Load("solver.json");
            int n = 0;
            foreach (var co in root.Arr("solver"))
            {
                var c = (Dictionary<string, object>)co;
                var lv = Level.FromJson(c.Obj("level"));
                int min = c.Int("minMoves");
                var r = Solver.Solve(lv, slack: c.Int("limit") - min);
                string name = c.Str("name");
                Assert.AreEqual(min, r.MinMoves, $"{name}: min moves");
                Assert.AreEqual(c.Long("solutionsAtMin"), r.SolutionsAtMin, $"{name}: solutions");
                Assert.AreEqual(Convert.ToDouble(c["deadEndRatio"]), Math.Round(r.DeadEndRatio, 6), 1e-9, $"{name}: dead end");
                Assert.AreEqual(Convert.ToDouble(c["randomWin"]), Math.Round(r.RandomWin, 6), 1e-9, $"{name}: random win");
                n++;
            }
            Assert.Greater(n, 20);
        }
    }
}
