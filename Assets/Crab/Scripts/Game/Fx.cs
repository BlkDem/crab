// Visual effects built in code from particle systems and line renderers (no hand-made prefabs):
// bursts of sparks and dust, the stream of matter flowing into the crab, things assembling out
// of glowing motes, shot tracers, muzzle flashes, lightning.
using System.Collections;
using UnityEngine;

namespace Crab.Game
{
    public static class Fx
    {
        static Transform root;

        static Transform Root
        {
            get
            {
                if (!root) root = new GameObject("Fx").transform;
                return root;
            }
        }

        static Material Mat(bool additive)
        {
            var lib = Library.Current;
            return lib ? (additive ? lib.particleAdditive : lib.particle) : null;
        }

        static ParticleSystem Make(string name, Vector3 pos, bool additive, float life)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = life;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 500;
            var em = ps.emission;
            em.rateOverTime = 0;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mat(additive);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = g;
            return ps;
        }

        /// <summary>A one-off burst of particles.</summary>
        public static void Burst(Vector3 pos, Color color, int count = 20, float speed = 3, float size = 0.08f, float life = 0.6f, bool additive = true, float gravity = 0.5f)
        {
            var ps = Make("burst", pos, additive, life);
            var main = ps.main;
            main.startColor = color;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.3f, speed);
            main.gravityModifier = gravity;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.5f, life);
            ps.Emit(count);
            Object.Destroy(ps.gameObject, life + 0.2f);
        }

        /// <summary>Glowing motes flowing from a point into a target (matter drawn into the crab).</summary>
        public static void Stream(Vector3 from, Transform to, Color color, int count = 6)
        {
            var ps = Make("stream", from, true, 0.7f);
            var main = ps.main;
            main.startColor = color;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.startSpeed = 0;
            var shape = ps.shape;
            shape.radius = 0.35f;
            ps.Emit(count);
            ps.gameObject.AddComponent<Homing>().target = to;
            Object.Destroy(ps.gameObject, 1.2f);
        }

        /// <summary>Motes gathering from all around into a point (a thing being created).</summary>
        public static void Assemble(Transform at, Color color, float duration)
        {
            var ps = Make("assemble", at.position, true, duration);
            var main = ps.main;
            main.startColor = color;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startSpeed = 0;
            main.startLifetime = duration;
            var shape = ps.shape;
            shape.radius = 0.9f;
            shape.radiusThickness = 0.2f;
            ps.Emit(60);
            ps.gameObject.AddComponent<Homing>().target = at;
            Object.Destroy(ps.gameObject, duration + 0.3f);
        }

        /// <summary>A bright line from a to b for a moment (a shot).</summary>
        public static void Tracer(Vector3 a, Vector3 b, Color color, float width = 0.03f, float life = 0.07f)
        {
            var go = new GameObject("tracer");
            go.transform.SetParent(Root, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = Library.Current ? Library.Current.tracer : null;
            lr.positionCount = 2;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            lr.startWidth = width;
            lr.endWidth = width * 0.4f;
            lr.startColor = color;
            lr.endColor = new Color(color.r, color.g, color.b, 0);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Object.Destroy(go, life);
        }

        /// <summary>A short flash of light (muzzle, impact, lightning).</summary>
        public static void Flash(Vector3 pos, Color color, float intensity = 3, float range = 4, float life = 0.08f)
        {
            var go = new GameObject("flash");
            go.transform.SetParent(Root, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            go.AddComponent<FadeLight>().life = life;
        }

        /// <summary>A jagged bolt from the sky to a point.</summary>
        public static void Lightning(Vector3 top, Vector3 bottom, Color color)
        {
            var go = new GameObject("lightning");
            go.transform.SetParent(Root, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = Library.Current ? Library.Current.tracer : null;
            int n = 14;
            lr.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                float t = i / (n - 1f);
                var p = Vector3.Lerp(top, bottom, t);
                if (i > 0 && i < n - 1) p += new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)) * 1.6f * (1 - t * 0.6f);
                lr.SetPosition(i, p);
            }
            lr.startWidth = 0.35f;
            lr.endWidth = 0.12f;
            lr.startColor = lr.endColor = color;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Object.Destroy(go, 0.18f);
            Flash(bottom + Vector3.up * 3, color, 12, 60, 0.25f);
        }

        sealed class FadeLight : MonoBehaviour
        {
            public float life = 0.1f;
            Light l;
            float t, start;

            void Start()
            {
                l = GetComponent<Light>();
                start = l.intensity;
            }

            void Update()
            {
                t += Time.deltaTime;
                l.intensity = start * Mathf.Clamp01(1 - t / life);
                if (t > life) Destroy(gameObject);
            }
        }

        /// <summary>Pulls a system's particles toward a target (matter streams).</summary>
        sealed class Homing : MonoBehaviour
        {
            public Transform target;
            ParticleSystem ps;
            ParticleSystem.Particle[] buf = new ParticleSystem.Particle[500];

            void Awake() => ps = GetComponent<ParticleSystem>();

            void LateUpdate()
            {
                if (!target) return;
                int n = ps.GetParticles(buf);
                var to = target.position;
                for (int i = 0; i < n; i++)
                {
                    float age = 1 - buf[i].remainingLifetime / buf[i].startLifetime;
                    buf[i].position = Vector3.Lerp(buf[i].position, to, Mathf.Clamp01(Time.deltaTime * (2 + age * 10)));
                }
                ps.SetParticles(buf, n);
            }
        }
    }
}
