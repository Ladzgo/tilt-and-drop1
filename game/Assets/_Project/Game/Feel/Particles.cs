using System.Collections.Generic;
using UnityEngine;

namespace TiltDrop.Game
{
    /// <summary>Pooled sprite particle bursts driven by the tweener (no ParticleSystem setup needed).</summary>
    public sealed class Particles : MonoBehaviour
    {
        readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
        public bool Reduced;

        SpriteRenderer Take()
        {
            var sr = pool.Count > 0 ? pool.Pop() : new GameObject("p").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.gameObject.SetActive(true);
            sr.sortingOrder = 50;
            return sr;
        }

        void Give(SpriteRenderer sr)
        {
            sr.gameObject.SetActive(false);
            pool.Push(sr);
        }

        /// <summary>Radial burst at a world position.</summary>
        public void Burst(Vector3 pos, Color color, int count = 14, float speed = 2.6f, float size = 0.22f, float life = 0.55f, Vector2 bias = default)
        {
            if (Reduced) count = Mathf.Max(3, count / 3);
            for (int i = 0; i < count; i++)
            {
                var sr = Take();
                sr.sprite = i % 3 == 0 ? Sprites.Star : Sprites.Circle;
                sr.color = Color.Lerp(color, Color.white, Random.value * 0.4f);
                float a = Random.value * Mathf.PI * 2f;
                var v = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * speed * Random.Range(0.4f, 1f) + (Vector3)bias * speed;
                float s0 = size * Random.Range(0.6f, 1.3f);
                float l = life * Random.Range(0.7f, 1.2f);
                float spin = Random.Range(-540f, 540f);
                var t = sr.transform;
                t.position = pos;
                t.localScale = Vector3.one * s0;
                Tweener.Run(l, p =>
                {
                    t.position = pos + v * p * l + Vector3.down * 2.2f * p * p * l;
                    t.localScale = Vector3.one * s0 * (1f - p);
                    t.localRotation = Quaternion.Euler(0, 0, spin * p);
                    var c = sr.color; c.a = 1f - p * p; sr.color = c;
                }, Ease.Linear, 0f, () => Give(sr), this);
            }
        }

        /// <summary>Soft expanding puff (landing dust).</summary>
        public void Puff(Vector3 pos, Color color, float size = 0.6f)
        {
            if (Reduced) return;
            var sr = Take();
            sr.sprite = Sprites.Soft;
            var t = sr.transform;
            t.position = pos;
            Tweener.Run(0.35f, p =>
            {
                t.localScale = Vector3.one * Mathf.Lerp(size * 0.4f, size, p);
                sr.color = new Color(color.r, color.g, color.b, 0.35f * (1f - p));
            }, Ease.OutCubic, 0f, () => Give(sr), this);
        }
    }
}
