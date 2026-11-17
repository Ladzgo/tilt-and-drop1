using System;
using System.Collections.Concurrent;

namespace TiltDrop.Core
{
    /// <summary>Marshals SDK callbacks (which may arrive on background threads) to the Unity main thread.
    /// GameRoot calls Drain() every frame.</summary>
    public static class MainThread
    {
        static readonly ConcurrentQueue<Action> Queue = new ConcurrentQueue<Action>();
        public static void Post(Action a) { if (a != null) Queue.Enqueue(a); }

        public static int Drain()
        {
            int n = 0;
            while (Queue.TryDequeue(out var a)) { a(); n++; }
            return n;
        }
    }
}
