using System;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    /// <summary>A Shade: drifts slowly, reaches out with both arms and drains life. It trails smoke,
    /// a pale light burns in the hollows of its eyes, and while it hunts it now and then flickers out
    /// and is suddenly nearer.</summary>
    public sealed class Shade : Enemy
    {
        Transform eyes, body;
        BoneTurn armL, armR;
        float moanT, stepT;
        Renderer[] skin;
        float gone;

        protected override void Awake()
        {
            base.Awake();
            body = model ? CrabRig.BodyOf(model) : CrabRig.Find(transform, "Body");
            armL = BoneTurn.Of(CrabRig.Find(transform, "ArmL"), body);
            armR = BoneTurn.Of(CrabRig.Find(transform, "ArmR"), body);
            eyes = CrabRig.Find(transform, "GlowEyes");
            moanT = UnityEngine.Random.Range(2f, 8f);
            stepT = UnityEngine.Random.Range(2f, 5f);
            skin = GetComponentsInChildren<Renderer>();
            Smoke();
            if (eyes)
            {
                var l = new GameObject("EyeLight").AddComponent<Light>();
                l.transform.SetParent(eyes, false);
                l.type = LightType.Point;
                l.color = new Color(0.7f, 1f, 0.85f);
                l.intensity = 0.8f;
                l.range = 3.5f;
                l.shadows = LightShadows.None;
            }
        }

        /// <summary>Dark smoke streaming off it as it moves.</summary>
        void Smoke()
        {
            var lib = Library.Current;
            var go = new GameObject("Smoke");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0, 0.7f, 0);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = 1.6f;
            main.startSpeed = 0.2f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startColor = new Color(0.08f, 0.09f, 0.09f, 0.45f);
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            var em = ps.emission;
            em.rateOverTime = 8;
            em.rateOverDistance = 6;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.35f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            if (lib) r.sharedMaterial = lib.particle;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
        }

        protected override void Update()
        {
            base.Update();
            if (!Alive) return;
            // the flicker: out for a breath, and back nearer the crab
            if (gone > 0)
            {
                gone -= Time.deltaTime;
                if (gone <= 0) foreach (var r in skin) if (r) r.enabled = true;
                return;
            }
            var hero = Target;
            if (!hunting || !hero || windT >= 0) return;
            float d = Vector3.Distance(Flat(hero.transform.position), Flat(transform.position));
            stepT -= Time.deltaTime;
            if (stepT > 0 || d < 4 || d > 16) return;
            stepT = UnityEngine.Random.Range(3f, 6f);
            var to = Flat(hero.transform.position - transform.position).normalized;
            var side = Vector3.Cross(Vector3.up, to) * UnityEngine.Random.Range(-1f, 1f);
            var step = (to + side * 0.6f).normalized * Mathf.Min(2.4f, d - 2.5f);
            cc.Move(step);
            foreach (var r in skin) if (r && !(r is ParticleSystemRenderer)) r.enabled = false;
            gone = 0.14f;
            Sfx.Play("shade", transform.position + Vector3.up, 0.35f, UnityEngine.Random.Range(1.3f, 1.6f));
        }

        protected override void Animate(float dt, float spd, float w)
        {
            float t = life;
            if (body) body.localPosition = new Vector3(0, 0.12f + Mathf.Sin(t * 1.7f) * 0.06f, 0);
            if (body) body.localRotation = Quaternion.Euler(spd * 2.5f + Mathf.Sin(t * 1.3f) * 2, 0, Mathf.Sin(t * 0.9f) * 3);
            float reach = w >= 0 ? Mathf.SmoothStep(0, 1, w) : hunting ? 0.25f : 0;
            var arm = Quaternion.Euler(-70 * reach + Mathf.Sin(t * 2.1f) * 6, 0, 0);
            armL?.Toward(arm * Quaternion.Euler(0, 0, Mathf.Sin(t * 2.7f) * 5), dt * 8);
            armR?.Toward(arm * Quaternion.Euler(0, 0, -Mathf.Sin(t * 2.3f) * 5), dt * 8);
            if (eyes) eyes.localScale = Vector3.one * (hurtT > 0 ? 0.3f : 1 + 0.15f * Mathf.Sin(t * 5));
            moanT -= dt;
            if (moanT <= 0 && hunting)
            {
                moanT = UnityEngine.Random.Range(4f, 9f);
                Sfx.Play("shade", transform.position + Vector3.up, 0.6f, UnityEngine.Random.Range(0.8f, 1.1f));
            }
        }

        protected override void Move(Vector3 horiz, float dt)
        {
            // drifting: no hurry to turn, no bounce
            base.Move(horiz, dt);
        }
    }
}
