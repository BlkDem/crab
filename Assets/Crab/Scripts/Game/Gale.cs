// The infernal storm of the second circle (Dante's "bufera infernal, che mai non resta"): a wind
// that never rests and breathes in cycles — calm, a warning (the howl rises, streaks race through
// the air), a gust. It blows mostly one way across the circle (`prevailing`), swaying slowly, each
// gust a little off. Behind rocks there is the wind's shadow: no wind. The crab (CrabController)
// and the demons read Force(); the crab rides gusts and fights them with its anchor.
using System;
using UnityEngine;

namespace Crab.Game
{
    public sealed class Gale : MonoBehaviour
    {
        public static Gale Instance { get; private set; }

        public enum Phase { Calm, Warning, Gust }

        [Tooltip("Where the wind blows, mostly (yaw, degrees)")]
        public float prevailing = 0;
        public float sway = 25, gustSpread = 15;
        [Tooltip("Push in the calm and in a gust (m/s)")]
        public float calmStrength = 1.0f, gustStrength = 9f;
        public Vector2 calmTime = new Vector2(3.5f, 6f);
        public float warnTime = 2f, gustTime = 2.8f;
        [Tooltip("How far upwind a rock still shelters (m)")]
        public float shadow = 4.5f;

        public Phase Now { get; private set; } = Phase.Calm;
        public event Action<Phase> Changed;
        float t, len = 4, gustOff, held;
        ParticleSystem streaks;

        /// <summary>0..1 through the warning (0 otherwise).</summary>
        public float Warning01 => Now == Phase.Warning ? Mathf.Clamp01(t / len) : 0;
        public bool Gusting => Now == Phase.Gust;

        /// <summary>Where the wind blows now (on the ground plane).</summary>
        public Vector3 Dir => Quaternion.Euler(0, prevailing + Mathf.Sin(Time.time * 0.07f) * sway + (Now == Phase.Gust ? gustOff : 0), 0) * Vector3.forward;

        /// <summary>The wind's strength now (m/s), before shelter.</summary>
        public float Strength
        {
            get
            {
                if (Now == Phase.Gust)
                {
                    float a = Mathf.Clamp01(t / 0.35f), b = Mathf.Clamp01((len - t) / 0.6f);   // rises fast, dies slower
                    return Mathf.Lerp(calmStrength, gustStrength, Mathf.Min(a, b));
                }
                return calmStrength * (1 + Warning01 * 1.5f);
            }
        }

        /// <summary>The push of the wind at a place (nothing in the lee of a rock).</summary>
        public Vector3 Force(Vector3 p) => Sheltered(p) ? Vector3.zero : Dir * Strength;

        /// <summary>A rock (or wall) stands upwind close by: the wind's shadow.</summary>
        public bool Sheltered(Vector3 p)
        {
            var up = -Dir;
            foreach (float h in new[] { 0.35f, 0.9f })
            {
                if (!Physics.Raycast(p + Vector3.up * h, up, out var hit, shadow, ~0, QueryTriggerInteraction.Ignore)) return false;
                if (hit.collider is CharacterController || Hits.Of(hit.collider) != null) return false;
            }
            return true;
        }

        /// <summary>Into a phase now, for `seconds` (tests, scripted moments; 0: its usual length).</summary>
        public void Set(Phase p, float seconds = 0)
        {
            Now = p;
            t = 0;
            len = seconds > 0 ? seconds : p == Phase.Calm ? UnityEngine.Random.Range(calmTime.x, calmTime.y) : p == Phase.Warning ? warnTime : gustTime;
            if (p == Phase.Gust) gustOff = UnityEngine.Random.Range(-gustSpread, gustSpread);
            if (p == Phase.Warning) Sfx.Play("wind", null, 0.9f, 0.7f);
            if (p == Phase.Gust && UnityEngine.Random.value < 0.5f) Lightning();
            Changed?.Invoke(p);
        }

        /// <summary>Stop the cycle where it is (tests); Resume() lets it breathe again.</summary>
        public void Hold(Phase p) { Set(p, 1e6f); held = 1; }
        public void Resume() { held = 0; Set(Phase.Calm); }

        void Awake()
        {
            Instance = this;
            Set(Phase.Calm);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            Sfx.Loop("wind", 0.5f);
            streaks = Streaks();
        }

        void Update()
        {
            t += Time.deltaTime;
            if (held == 0 && t >= len) Set(Now == Phase.Calm ? Phase.Warning : Now == Phase.Warning ? Phase.Gust : Phase.Calm);
            Sfx.Loop("wind", 0.35f + 0.65f * Mathf.InverseLerp(calmStrength, gustStrength, Strength));
            if (streaks)
            {
                // the air's streaks: round the camera, along the wind, as many as it is strong
                var cam = Camera.main;
                if (cam) streaks.transform.position = cam.transform.position - Dir * 12;
                streaks.transform.rotation = Quaternion.LookRotation(Dir);
                var main = streaks.main;
                main.startSpeed = 10 + Strength * 3;
                var em = streaks.emission;
                em.rateOverTime = 20 + Strength * 30 + Warning01 * 60;
            }
            if (Now == Phase.Gust && UnityEngine.Random.value < Time.deltaTime * 0.2f) Lightning();
        }

        void Lightning()
        {
            var cam = Camera.main;
            var c = cam ? cam.transform.position : transform.position;
            var at = c + Quaternion.Euler(0, UnityEngine.Random.Range(0, 360f), 0) * Vector3.forward * UnityEngine.Random.Range(40, 90);
            Fx.Lightning(at + Vector3.up * 60, at + Vector3.down * 20, new Color(0.8f, 0.45f, 1f));
            Sfx.Play("thunder", null, 0.5f, UnityEngine.Random.Range(0.8f, 1.1f));
        }

        ParticleSystem Streaks()
        {
            var lib = Library.Current;
            var go = new GameObject("WindStreaks");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 1.4f;
            main.startSize = 0.06f;
            main.startColor = new Color(0.85f, 0.8f, 1f, 0.35f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1500;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(36, 14, 2);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 4;
            r.velocityScale = 0.12f;
            if (lib) r.sharedMaterial = lib.particleAdditive;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }
    }
}
