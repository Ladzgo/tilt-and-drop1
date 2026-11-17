using System;
using System.Collections.Generic;
using UnityEngine;

namespace TiltDrop.Game
{
    public static class Ease
    {
        public static float Linear(float t) => t;
        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);
        public static float InQuad(float t) => t * t;
        public static float OutCubic(float t) { float u = 1f - t; return 1f - u * u * u; }
        public static float InOutCubic(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

        /// <summary>Overshoot then settle (rotation "snap").</summary>
        public static float OutBack(float t)
        {
            const float c1 = 1.70158f * 0.9f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        public static float OutElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            const float c4 = 2f * Mathf.PI / 3f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
        }
    }

    /// <summary>Tiny allocation-light tween runner (no external dependency).</summary>
    public sealed class Tweener : MonoBehaviour
    {
        sealed class T
        {
            public float Start, Duration;
            public Func<float, float> Ease;
            public Action<float> Apply;
            public Action Done;
            public object Owner;
            public bool Started;
        }

        static Tweener instance;
        readonly List<T> active = new List<T>();
        readonly List<T> adding = new List<T>();
        public static float TimeScale = 1f;
        float clock;

        public static Tweener Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("Tweener");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<Tweener>();
                }
                return instance;
            }
        }

        public static float Now => Instance.clock;

        /// <summary>Runs apply(eased t in 0..1) over duration seconds after delay seconds.</summary>
        public static void Run(float duration, Action<float> apply, Func<float, float> ease = null, float delay = 0f, Action done = null, object owner = null)
        {
            var inst = Instance;
            inst.adding.Add(new T { Start = inst.clock + Mathf.Max(0f, delay), Duration = Mathf.Max(0.0001f, duration), Ease = ease ?? Game.Ease.OutQuad, Apply = apply, Done = done, Owner = owner });
        }

        public static void Delay(float seconds, Action done, object owner = null) => Run(seconds, _ => { }, Game.Ease.Linear, 0f, done, owner);

        /// <summary>Stops (without completing) all tweens of an owner.</summary>
        public static void Kill(object owner)
        {
            if (instance == null || owner == null) return;
            instance.active.RemoveAll(t => t.Owner == owner);
            instance.adding.RemoveAll(t => t.Owner == owner);
        }

        public static void CompleteAll()
        {
            if (instance == null) return;
            instance.clock += 1000f;
            instance.Update();
        }

        void Update()
        {
            clock += Time.unscaledDeltaTime * TimeScale;
            if (adding.Count > 0) { active.AddRange(adding); adding.Clear(); }
            // insertion order: tweens scheduled for the same instant run in the order they were created
            int write = 0;
            for (int i = 0; i < active.Count; i++)
            {
                var t = active[i];
                bool finished = false;
                if (clock >= t.Start)
                {
                    float p = Mathf.Clamp01((clock - t.Start) / t.Duration);
                    try { t.Apply(t.Ease(p)); }
                    catch (Exception e) { Debug.LogException(e); finished = true; p = -1f; }
                    if (p >= 1f)
                    {
                        finished = true;
                        try { t.Done?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
                    }
                }
                if (!finished) active[write++] = t;
            }
            active.RemoveRange(write, active.Count - write);
        }
    }
}
