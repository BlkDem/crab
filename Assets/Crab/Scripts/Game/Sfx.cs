// Sound without audio files: every clip is synthesized once, at first use (noise through
// filters, sines and saws with envelopes), then played from a small pool of sources —
// the surf and wind, rain and thunder, the gull's cry, blows and shots, matter flowing,
// things being created, the moans of the shades and the Guardian's roar, and the music beds.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Crab.Game
{
    public static class Sfx
    {
        const int Rate = 44100;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static readonly List<AudioSource> pool = new List<AudioSource>();
        static readonly Dictionary<string, AudioSource> loops = new Dictionary<string, AudioSource>();
        static Transform host;
        public static float volume = 0.8f, music = 0.5f;

        static Transform Host
        {
            get
            {
                if (!host)
                {
                    host = new GameObject("Sfx").transform;
                    UnityEngine.Object.DontDestroyOnLoad(host.gameObject);
                }
                return host;
            }
        }

        // ------------------------------------------------------------------ playing
        /// <summary>Play a clip once; in the world at pos (3D) or flat if pos is null.</summary>
        public static void Play(string name, Vector3? pos = null, float vol = 1, float pitch = 1)
        {
            var clip = Get(name);
            if (!clip) return;
            var src = pool.Find(s => s && !s.isPlaying);
            if (!src)
            {
                if (pool.Count >= 24) { src = pool[0]; pool.RemoveAt(0); pool.Add(src); }
                else
                {
                    var go = new GameObject("sfx");
                    go.transform.SetParent(Host, false);
                    src = go.AddComponent<AudioSource>();
                    src.playOnAwake = false;
                    pool.Add(src);
                }
            }
            src.clip = clip;
            src.volume = vol * volume;
            src.pitch = pitch * UnityEngine.Random.Range(0.95f, 1.05f);
            src.spatialBlend = pos.HasValue ? 1 : 0;
            src.minDistance = 3;
            src.maxDistance = 60;
            src.rolloffMode = AudioRolloffMode.Linear;
            if (pos.HasValue) src.transform.position = pos.Value;
            src.Play();
        }

        /// <summary>A looping bed (surf, wind, rain, music); vol 0 fades it out.</summary>
        public static void Loop(string name, float vol, bool isMusic = false)
        {
            if (!loops.TryGetValue(name, out var src) || !src)
            {
                if (vol <= 0) return;
                var go = new GameObject("loop " + name);
                go.transform.SetParent(Host, false);
                src = go.AddComponent<AudioSource>();
                src.clip = Get(name);
                src.loop = true;
                src.volume = 0;
                src.spatialBlend = 0;
                src.Play();
                go.AddComponent<LoopFader>();
                loops[name] = src;
            }
            var f = src.GetComponent<LoopFader>();
            f.target = vol * (isMusic ? music : volume);
            f.isMusic = isMusic;
            f.baseVol = vol;
        }

        public static void StopAll()
        {
            foreach (var kv in loops) if (kv.Value) kv.Value.GetComponent<LoopFader>().target = 0;
        }

        sealed class LoopFader : MonoBehaviour
        {
            public float target, baseVol;
            public bool isMusic;
            AudioSource s;

            void Awake() => s = GetComponent<AudioSource>();

            void Update()
            {
                if (target > 0) target = baseVol * (isMusic ? music : volume);
                s.volume = Mathf.MoveTowards(s.volume, target, Time.unscaledDeltaTime * 0.6f);
            }
        }

        // ------------------------------------------------------------------ synthesis
        static AudioClip Get(string name)
        {
            if (clips.TryGetValue(name, out var c)) return c;
            float[] d = Make(name);
            if (d == null) return null;
            c = AudioClip.Create(name, d.Length, 1, Rate, false);
            c.SetData(d, 0);
            clips[name] = c;
            return c;
        }

        static System.Random rng = new System.Random(7);
        static float N() => (float)rng.NextDouble() * 2 - 1;
        static float[] Buf(float sec) => new float[(int)(sec * Rate)];

        /// <summary>A one-pole low-pass filter state.</summary>
        sealed class LP
        {
            float y;
            public float Run(float x, float cutoff) { y += (x - y) * Mathf.Clamp01(2 * Mathf.PI * cutoff / Rate); return y; }
        }

        /// <summary>RBJ band-pass.</summary>
        sealed class BP
        {
            float x1, x2, y1, y2;
            public float Run(float x, float f, float q)
            {
                float w = 2 * Mathf.PI * Mathf.Min(f, Rate * 0.45f) / Rate, al = Mathf.Sin(w) / (2 * q), a0 = 1 + al;
                float y = (al * x - al * x2 - (-2 * Mathf.Cos(w)) * y1 - (1 - al) * y2) / a0;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                return y;
            }
        }

        static float Env(float t, float attack, float decay) => t < attack ? t / attack : Mathf.Exp(-(t - attack) / decay);

        static void Normalize(float[] d, float peak = 0.9f)
        {
            float m = 0;
            foreach (var v in d) m = Mathf.Max(m, Mathf.Abs(v));
            if (m <= 0) return;
            for (int i = 0; i < d.Length; i++) d[i] *= peak / m;
        }

        /// <summary>Make a loop seamless: cross-fade the end into the start.</summary>
        static float[] Seamless(float[] d, float fadeSec = 0.5f)
        {
            int f = (int)(fadeSec * Rate);
            var o = new float[d.Length - f];
            for (int i = 0; i < o.Length; i++) o[i] = d[i];
            for (int i = 0; i < f; i++)
            {
                float k = i / (float)f;
                o[i] = d[i] * k + d[o.Length + i] * (1 - k);
            }
            return o;
        }

        static float[] Make(string name)
        {
            rng = new System.Random(name.GetHashCode());
            float[] d;
            switch (name)
            {
                case "surf":
                {
                    d = Buf(9);
                    var lp = new LP();
                    var lp2 = new LP();
                    float brown = 0;
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        brown = (brown + 0.02f * N()) / 1.02f;
                        float swell = 0.35f + 0.65f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2 / 4.5f), 2);
                        d[i] = lp.Run(brown * 3, 500 + 900 * swell) * swell + lp2.Run(N(), 2500) * 0.08f * swell * swell;
                    }
                    Normalize(d, 0.7f);
                    return Seamless(d);
                }
                case "wind":
                {
                    d = Buf(7);
                    var bp = new BP();
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        float f = 300 + 250 * Mathf.Sin(t * 0.9f) + 120 * Mathf.Sin(t * 2.3f);
                        d[i] = bp.Run(N(), f, 2.5f) * (0.6f + 0.4f * Mathf.Sin(t * 0.7f));
                    }
                    Normalize(d, 0.6f);
                    return Seamless(d);
                }
                case "rain":
                {
                    d = Buf(4);
                    var lp = new LP();
                    for (int i = 0; i < d.Length; i++)
                    {
                        float x = N() - lp.Run(N(), 1200);
                        if (rng.NextDouble() < 0.0008) x += N() * 3;   // drops
                        d[i] = x;
                    }
                    Normalize(d, 0.5f);
                    return Seamless(d, 0.3f);
                }
                case "thunder":
                {
                    d = Buf(4);
                    var lp = new LP();
                    float brown = 0;
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        brown = (brown + 0.03f * N()) / 1.01f;
                        float crack = t < 0.15f ? N() * (1 - t / 0.15f) : 0;
                        d[i] = lp.Run(brown * 4, 300) * Env(t, 0.05f, 1.2f) * (1 + 0.5f * Mathf.Sin(t * 9)) + crack * 0.6f;
                    }
                    Normalize(d);
                    return d;
                }
                case "gull":
                {
                    d = Buf(0.9f);
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        float seg = t % 0.3f;
                        float f = 1400 + 900 * Mathf.Sin(seg * 12) - seg * 1500;
                        float mod = Mathf.Sin(2 * Mathf.PI * f * 0.5f * t) * 2.5f;
                        d[i] = Mathf.Sin(2 * Mathf.PI * f * t + mod) * Env(seg, 0.02f, 0.09f) * (t < 0.6f ? 1 : 0);
                    }
                    Normalize(d, 0.6f);
                    return d;
                }
                case "swing":
                {
                    d = Buf(0.22f);
                    var bp = new BP();
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        d[i] = bp.Run(N(), 2200 - t * 7000, 1.5f) * Mathf.Sin(Mathf.PI * t / 0.22f);
                    }
                    Normalize(d, 0.6f);
                    return d;
                }
                case "hit":
                {
                    d = Buf(0.25f);
                    var lp = new LP();
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        d[i] = Mathf.Sin(2 * Mathf.PI * (110 - 200 * t) * t) * Env(t, 0.003f, 0.06f) + lp.Run(N(), 1800) * Env(t, 0.001f, 0.025f);
                    }
                    Normalize(d);
                    return d;
                }
                case "shot":
                {
                    d = Buf(0.6f);
                    var lp = new LP();
                    var lp2 = new LP();
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        d[i] = N() * Env(t, 0.001f, 0.03f) * 0.9f + lp.Run(N(), 900) * Env(t, 0.002f, 0.18f) * 2 + Mathf.Sin(2 * Mathf.PI * (75 - 40 * t) * t) * Env(t, 0.002f, 0.09f)
                               + lp2.Run(N(), 400) * Env(t, 0.05f, 0.25f) * 0.8f;
                    }
                    Normalize(d);
                    return d;
                }
                case "absorb":
                {
                    d = Buf(0.7f);
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        float f = 300 + 900 * t;
                        d[i] = (Mathf.Sin(2 * Mathf.PI * f * t) + 0.5f * Mathf.Sin(2 * Mathf.PI * f * 1.5f * t)) * Mathf.Sin(Mathf.PI * t / 0.7f) * (0.7f + 0.3f * Mathf.Sin(t * 60));
                    }
                    Normalize(d, 0.5f);
                    return d;
                }
                case "create":
                {
                    d = Buf(1.4f);
                    float[] fs = { 523, 784, 1046, 1568, 2093 };
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        float v = 0;
                        for (int k = 0; k < fs.Length; k++)
                        {
                            float tk = t - k * 0.06f;
                            if (tk > 0) v += Mathf.Sin(2 * Mathf.PI * fs[k] * tk) * Env(tk, 0.005f, 0.5f) / (1 + k * 0.3f);
                        }
                        d[i] = v + N() * 0.05f * Env(t, 0.3f, 0.2f);
                    }
                    Normalize(d, 0.6f);
                    return d;
                }
                case "eat":
                {
                    d = Buf(0.5f);
                    var bp = new BP();
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        float c = t % 0.16f;
                        d[i] = bp.Run(N(), 1500, 3) * Env(c, 0.002f, 0.03f);
                    }
                    Normalize(d, 0.6f);
                    return d;
                }
                case "hurt":
                {
                    d = Buf(0.35f);
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        d[i] = Mathf.Sin(2 * Mathf.PI * (900 - 1600 * t) * t) * Env(t, 0.005f, 0.08f) * 0.6f + Mathf.Sin(2 * Mathf.PI * 70 * t) * Env(t, 0.002f, 0.08f);
                    }
                    Normalize(d, 0.8f);
                    return d;
                }
                case "spark":
                {
                    d = Buf(3);
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        float v = 0;
                        foreach (float f in new[] { 440f, 554f, 659f, 880f, 1318f })
                            v += Mathf.Sin(2 * Mathf.PI * f * t * (1 + 0.003f * Mathf.Sin(t * 5)));
                        d[i] = v * Env(t, 0.4f, 1.2f) * 0.2f + N() * Env(t, 0.01f, 0.15f) * 0.6f;
                    }
                    Normalize(d, 0.8f);
                    return d;
                }
                case "shade":
                {
                    d = Buf(1.4f);
                    var b1 = new BP();
                    var b2 = new BP();
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        float src = N() * 0.4f + Mathf.Sin(2 * Mathf.PI * (140 - 30 * t) * t);
                        d[i] = (b1.Run(src, 500 - 100 * t, 6) + b2.Run(src, 900 - 200 * t, 6)) * Mathf.Sin(Mathf.PI * t / 1.4f);
                    }
                    Normalize(d, 0.5f);
                    return d;
                }
                case "crawler":
                {
                    d = Buf(0.45f);
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        float f = 600 + 500 * t;
                        float saw = 2 * ((f * t) % 1) - 1;
                        d[i] = (saw * 0.5f + N() * 0.3f) * Env(t, 0.01f, 0.15f);
                    }
                    Normalize(d, 0.5f);
                    return d;
                }
                case "die":
                {
                    d = Buf(0.9f);
                    var lp = new LP();
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        d[i] = (N() - lp.Run(N(), 3000 - 2500 * t)) * Env(t, 0.02f, 0.3f);
                    }
                    Normalize(d, 0.5f);
                    return d;
                }
                case "roar":
                {
                    d = Buf(2.6f);
                    var lp = new LP();
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        float f = 55 + 25 * Mathf.Sin(t * 2) + 10 * Mathf.Sin(t * 13);
                        float saw = 2 * ((f * t) % 1) - 1;
                        float v = Mathf.Clamp(saw * 1.6f + N() * 0.4f, -1, 1);
                        d[i] = lp.Run(v, 700) * Mathf.Sin(Mathf.PI * Mathf.Min(1, t / 2.6f)) * 1.5f;
                    }
                    Normalize(d);
                    return d;
                }
                case "slam":
                {
                    d = Buf(1.6f);
                    var lp = new LP();
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        d[i] = Mathf.Sin(2 * Mathf.PI * (50 - 20 * t) * t) * Env(t, 0.005f, 0.35f) + lp.Run(N(), 600) * Env(t, 0.002f, 0.4f) * 2;
                    }
                    Normalize(d);
                    return d;
                }
                case "beam":
                {
                    d = Buf(3);
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        d[i] = (Mathf.Sin(2 * Mathf.PI * 220 * t) + 0.5f * Mathf.Sin(2 * Mathf.PI * 330 * t) + 0.25f * Mathf.Sin(2 * Mathf.PI * 440 * t)) * (0.7f + 0.3f * Mathf.Sin(t * 30));
                    }
                    Normalize(d, 0.5f);
                    return Seamless(d, 0.3f);
                }
                case "beacon":
                {
                    d = Buf(2);
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        d[i] = (Mathf.Sin(2 * Mathf.PI * 330 * t) + Mathf.Sin(2 * Mathf.PI * 495 * t) * 0.6f) * Env(t, 0.3f, 0.6f) + N() * 0.15f * Env(t, 0.05f, 0.3f);
                    }
                    Normalize(d, 0.6f);
                    return d;
                }
                case "rift":
                {
                    d = Buf(5);
                    var lp = new LP();
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        d[i] = (Mathf.Sin(2 * Mathf.PI * 62 * t) * 0.8f + lp.Run(N(), 300) * 2) * (0.6f + 0.4f * Mathf.Sin(t * Mathf.PI * 2 / 1.25f));
                    }
                    Normalize(d, 0.6f);
                    return Seamless(d);
                }
                case "click":
                {
                    d = Buf(0.05f);
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        d[i] = Mathf.Sin(2 * Mathf.PI * 1800 * t) * Env(t, 0.001f, 0.01f);
                    }
                    return d;
                }
                case "music_beach":
                    return Pad(new[] { new[] { 0, 4, 7, 11 }, new[] { -3, 0, 4, 7 }, new[] { 5, 9, 12, 16 }, new[] { 2, 5, 9, 12 } }, 57, 4, false);
                case "music_limbo":
                    return Pad(new[] { new[] { 0, 3, 7 }, new[] { -2, 3, 5 }, new[] { 0, 3, 8 }, new[] { -4, 0, 3 } }, 45, 6, true);
                case "music_wrath":
                {
                    // the dark pad of Limbo a step lower, in the Phrygian, under slow war drums
                    d = Pad(new[] { new[] { 0, 3, 7 }, new[] { 1, 5, 8 }, new[] { 0, 3, 7 }, new[] { -2, 1, 5 } }, 40, 4, true);
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        float beat = t % 2f, b2 = (t + 0.75f) % 2f;
                        d[i] += Mathf.Sin(2 * Mathf.PI * (48 - 40 * beat) * beat) * Env(beat, 0.003f, 0.3f) * 0.45f
                              + Mathf.Sin(2 * Mathf.PI * (52 - 40 * b2) * b2) * Env(b2, 0.003f, 0.2f) * 0.25f;
                    }
                    Normalize(d, 0.55f);
                    return d;
                }
                case "music_greed":
                {
                    // a slow, stately minor waltz of plucked notes over a low organ: counting coins
                    d = Pad(new[] { new[] { 0, 3, 7 }, new[] { -4, 0, 3 }, new[] { -2, 2, 5 }, new[] { -5, -1, 2 } }, 43, 3, true);
                    int[] tune = { 12, 15, 19, 15, 8, 12, 15, 12, 10, 14, 17, 14, 7, 11, 14, 19 };
                    float beat = 0.75f;
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        int n = (int)(t / beat);
                        float tb = t - n * beat;
                        float f = 440 * Mathf.Pow(2, (55 + tune[n % tune.Length] - 69) / 12f);
                        float pluck = (Mathf.Sin(2 * Mathf.PI * f * t) + 0.4f * Mathf.Sin(2 * Mathf.PI * f * 2 * t) + 0.2f * Mathf.Sin(2 * Mathf.PI * f * 3 * t)) * Env(tb, 0.004f, 0.25f);
                        d[i] += pluck * 0.22f;
                    }
                    Normalize(d, 0.5f);
                    return d;
                }
                case "lava":
                {
                    // a low rumble with bubbles bursting in it
                    d = Buf(6);
                    var lp = new LP();
                    var bp = new BP();
                    float pop = -1, pf = 200;
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        if (pop < 0 && rng.NextDouble() < 3.0 / Rate) { pop = 0; pf = 120 + (float)rng.NextDouble() * 260; }
                        float b = 0;
                        if (pop >= 0)
                        {
                            b = Mathf.Sin(2 * Mathf.PI * pf * (1 + pop * 6) * pop) * Env(pop, 0.002f, 0.06f);
                            pop += 1f / Rate;
                            if (pop > 0.25f) pop = -1;
                        }
                        d[i] = lp.Run(N(), 120) * 3 + bp.Run(N(), 400, 2) * 0.2f + b * 0.5f;
                    }
                    Normalize(d, 0.6f);
                    return Seamless(d);
                }
                case "music_boss":
                {
                    d = Buf(8);
                    var lp = new LP();
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = i / (float)Rate;
                        float beat = t % 0.5f;
                        float drum = Mathf.Sin(2 * Mathf.PI * (60 - 80 * beat) * beat) * Env(beat, 0.002f, 0.12f);
                        float off = (t + 0.25f) % 1f;
                        float snare = lp.Run(N(), 3000) * Env(off, 0.002f, 0.08f) * 0.5f;
                        float f = 55 * (t % 4 < 2 ? 1 : 1.189f);
                        float drone = (2 * ((f * t) % 1) - 1) * 0.25f;
                        d[i] = drum + snare + drone;
                    }
                    Normalize(d, 0.8f);
                    return d;
                }
            }
            Debug.LogWarning($"no sound '{name}'");
            return null;
        }

        /// <summary>Slow pad chords (soft saws through a low-pass), one chord per `bar` seconds.</summary>
        static float[] Pad(int[][] chords, int root, float bar, bool dark)
        {
            var d = Buf(bar * chords.Length + 1);
            var lp = new LP();
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                int ci = Mathf.Min(chords.Length - 1, (int)(t / bar));
                float tc = t - ci * bar;
                float v = 0;
                foreach (int n in chords[ci])
                {
                    float f = 440 * Mathf.Pow(2, (root + n - 69) / 12f);
                    v += Mathf.Sin(2 * Mathf.PI * f * t) + 0.3f * Mathf.Sin(2 * Mathf.PI * f * 2.003f * t);
                }
                float env = Mathf.Clamp01(tc / 1.2f) * Mathf.Clamp01((bar - tc) / 1.0f + 0.3f);
                d[i] = lp.Run(v, dark ? 500 : 1200) * env + (dark ? Mathf.Sin(2 * Mathf.PI * 440 * Mathf.Pow(2, (root - 12 - 69) / 12f) * t) * 0.6f : 0);
            }
            Normalize(d, 0.5f);
            return Seamless(d, 1f);
        }
    }
}
