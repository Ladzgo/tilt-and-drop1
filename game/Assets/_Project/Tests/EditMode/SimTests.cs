using NUnit.Framework;

namespace TiltDrop.Core.Tests
{
    public class SimTests
    {
        [Test]
        public void RotationMapping()
        {
            Assert.AreEqual(Dir.Right, Dirs.Rotate(Dir.Down, Rot.CW));
            Assert.AreEqual(Dir.Up, Dirs.Rotate(Dir.Right, Rot.CW));
            Assert.AreEqual(Dir.Left, Dirs.Rotate(Dir.Up, Rot.CW));
            Assert.AreEqual(Dir.Down, Dirs.Rotate(Dir.Left, Rot.CW));
            Assert.AreEqual(Dir.Left, Dirs.Rotate(Dir.Down, Rot.CCW));
        }

        [Test]
        public void CascadeThroughSameGate()
        {
            var lv = Level.FromRows(new[] { "....", "....", "11.2" }, gL: "..1", moves: 5);
            Assert.IsTrue(Settler.IsStable(lv));
            var g = new GameState(lv);
            var ev = g.Rotate(Rot.CCW);
            CollectionAssert.AreEqual(new[] { "....", "....", "2..." }, g.Rows());
            Assert.AreEqual("X0:1:L2", ev[0].ToGolden(4));
            Assert.AreEqual(4, ev.Count);
            Assert.AreEqual(Status.Playing, g.Status);
        }

        [Test]
        public void CloneIsIndependent()
        {
            var g = new GameState(Level.FromRows(new[] { "....", "....", "11.." }, gL: "..1", moves: 3));
            var c = g.Clone();
            g.Rotate(Rot.CCW);
            Assert.AreEqual(Status.Won, g.Status);
            Assert.AreEqual(Status.Playing, c.Status);
            Assert.AreEqual(3, c.MovesLeft);
        }

        [Test]
        public void InvalidOperationsThrow()
        {
            var g = new GameState(Level.FromRows(new[] { "#...", "....", "1..." }, moves: 1));
            Assert.Throws<System.ArgumentException>(() => g.Hammer(0, 0));
            Assert.Throws<System.ArgumentException>(() => g.Hammer(9, 9));
            g.Rotate(Rot.CW);
            Assert.AreEqual(Status.Lost, g.Status);
            Assert.Throws<System.InvalidOperationException>(() => g.Rotate(Rot.CW));
            Assert.Throws<System.FormatException>(() => Level.ParseRow("x"));
            Assert.Throws<System.ArgumentException>(() => new Level(2, 2, new byte[4], new[] { new byte[2], new byte[2], new byte[2], new byte[2] }));
        }

        [Test]
        public void HintFindsWinningMove()
        {
            var lv = Level.FromRows(new[] { "....", "....", "11.2" }, gL: "..1", gR: "..2", moves: 5);
            var g = new GameState(lv);
            Assert.IsTrue(Solver.TryHint(g, out int min, out Rot first));
            Assert.AreEqual(3, min);
            g.Rotate(first);
            Assert.IsTrue(Solver.TryHint(g, out int min2, out _));
            Assert.AreEqual(2, min2);
        }
    }
}
