using System;
using UnityEngine;

namespace TiltDrop.Game
{
    public enum Sound { Tap, Whoosh, Land, Crack, Chain, Coin, Win, Fail, Star, Hammer }

    /// <summary>Procedurally synthesized placeholder SFX (swap for designed audio later; the hooks stay).</summary>
    public sealed class Sfx : MonoBehaviour
    {
        const int Rate = 44100;
        AudioSource src;
        AudioClip[] clips;
        AudioClip[] notes; // pentatonic, for exit combos
        public bool Enabled = true;

        void Awake()
        {
            src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            clips = new AudioClip[Enum.GetValues(typeof(Sound)).Length];
            clips[(int)Sound.Tap] = Make("tap", 0.05f, t => Sine(1200, t) * Env(t, 0.002f, 0.04f) * 0.25f);
            clips[(int)Sound.Whoosh] = MakeNoise("whoosh", 0.28f, 0.35f);
            clips[(int)Sound.Land] = Make("land", 0.09f, t => (Sine(170 - 400 * t, t) * 0.8f + Noise() * 0.15f) * Env(t, 0.002f, 0.07f) * 0.5f);
            clips[(int)Sound.Crack] = Make("crack", 0.12f, t => Noise() * Env(t, 0.001f, 0.06f) * 0.35f + Sine(2400, t) * Env(t, 0.001f, 0.03f) * 0.1f);
            clips[(int)Sound.Chain] = Make("chain", 0.3f, t => (Sine(1530, t) + Sine(2210, t) * 0.6f + Sine(3170, t) * 0.3f) * Env(t, 0.001f, 0.25f) * 0.15f);
            clips[(int)Sound.Coin] = Make("coin", 0.12f, t => (Sine(1567, t) + Sine(2093, t) * Step(t, 0.04f)) * Env(t, 0.002f, 0.1f) * 0.18f);
            clips[(int)Sound.Star] = Make("star", 0.35f, t => (Sine(1046, t) + Sine(1568, t) * 0.5f) * Env(t, 0.002f, 0.3f) * 0.22f);
            clips[(int)Sound.Hammer] = Make("hammer", 0.2f, t => (Sine(90, t) + Noise() * 0.5f) * Env(t, 0.001f, 0.15f) * 0.5f);
            clips[(int)Sound.Fail] = Make("fail", 0.6f, t => Pluck(t < 0.25f ? 392f : 311f, t < 0.25f ? t : t - 0.25f) * 0.35f);
            clips[(int)Sound.Win] = Make("win", 0.9f, t => Arp(t, new[] { 523f, 659f, 784f, 1046f }, 0.12f) * 0.35f);
            float[] scale = { 523.25f, 587.33f, 659.25f, 783.99f, 880f, 1046.5f, 1174.66f, 1318.51f, 1567.98f, 1760f };
            notes = new AudioClip[scale.Length];
            for (int i = 0; i < scale.Length; i++)
            {
                float f = scale[i];
                notes[i] = Make("note" + i, 0.45f, t => Pluck(f, t) * 0.32f);
            }
        }

        public void Play(Sound s, float volume = 1f, float pitch = 1f)
        {
            if (!Enabled || clips == null) return;
            src.pitch = pitch;
            src.PlayOneShot(clips[(int)s], volume);
        }

        /// <summary>Exit sound; consecutive exits in one settle climb the scale.</summary>
        public void Exit(int comboIndex)
        {
            if (!Enabled || notes == null) return;
            src.pitch = 1f;
            src.PlayOneShot(notes[Mathf.Clamp(comboIndex, 0, notes.Length - 1)], 1f);
        }

        // ---- synthesis helpers -----------------------------------------------------------
        static System.Random rng = new System.Random(7);
        static float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);
        static float Sine(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t);
        static float Step(float t, float at) => t >= at ? 1f : 0f;
        static float Env(float t, float attack, float decay) => t < attack ? t / attack : Mathf.Exp(-(t - attack) / (decay * 0.35f));
        static float Pluck(float f, float t) => (Sine(f, t) + 0.35f * Sine(2 * f, t) + 0.12f * Sine(3 * f, t)) * Env(t, 0.003f, 0.35f);

        static float Arp(float t, float[] fs, float step)
        {
            float v = 0f;
            for (int i = 0; i < fs.Length; i++) if (t >= i * step) v += Pluck(fs[i], t - i * step) * 0.6f;
            return v;
        }

        static AudioClip Make(string name, float seconds, Func<float, float> f)
        {
            int n = (int)(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip MakeNoise(string name, float seconds, float volume)
        {
            int n = (int)(seconds * Rate);
            var data = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, p = t / seconds;
                float cutoff = Mathf.Lerp(0.02f, 0.25f, Mathf.Sin(p * Mathf.PI)); // filter sweep
                lp += cutoff * (Noise() - lp);
                data[i] = lp * Mathf.Sin(p * Mathf.PI) * volume * 3f;
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
