using UnityEngine;

namespace Crab.Game
{
    /// <summary>A brute of wrath: three metres of muscle and fire. Slow; raises both fists and pounds the ground around it.</summary>
    public sealed class Brute : Enemy
    {
        Transform body, armL, armR, legL, legR;
        float phase;

        protected override void Awake()
        {
            base.Awake();
            body = model ? CrabRig.BodyOf(model) : transform;
            armL = CrabRig.Find(transform, "ArmL");
            armR = CrabRig.Find(transform, "ArmR");
            legL = CrabRig.Find(transform, "LegL");
            legR = CrabRig.Find(transform, "LegR");
        }

        protected override Color HitColor => new Color(1f, 0.55f, 0.2f);

        protected override void BeginAttack()
        {
            base.BeginAttack();
            Sfx.Play("roar", transform.position + Vector3.up * 2, 0.6f, 1.6f);
        }

        protected override void Strike(Hero hero, float dist)
        {
            var at = transform.position + transform.forward * 1.6f;
            Sfx.Play("slam", at, 0.9f, 1.2f);
            Fx.Burst(at + Vector3.up * 0.3f, new Color(0.7f, 0.5f, 0.35f), 40, 4, 0.25f, 0.9f, false, 0.4f);
            if (hero) hero.Cam.Shake(Mathf.Clamp01(1.5f - dist * 0.15f));
            if (hero && Vector3.Distance(Flat(hero.transform.position), Flat(at)) < 3.2f && hero.Controller.Grounded)
                hero.Hurt(damage, hero.transform.position + Vector3.up * 0.3f, hero.transform.position - at);
        }

        protected override void Animate(float dt, float spd, float w)
        {
            phase += dt * (1.5f + spd * 2.2f);
            float s = Mathf.Sin(phase) * 25 * Mathf.Clamp01(spd);
            if (legL) legL.localRotation = Quaternion.Euler(s, 0, 0);
            if (legR) legR.localRotation = Quaternion.Euler(-s, 0, 0);
            float raise = w >= 0 ? (w < 0.8f ? 150 * w / 0.8f : 150 - 190 * (w - 0.8f) / 0.2f) : s * 0.5f;
            if (armL) armL.localRotation = Quaternion.Euler(-raise, 0, 0);
            if (armR) armR.localRotation = Quaternion.Euler(-raise, 0, 0);
            if (body) body.localRotation = Quaternion.Euler(w >= 0 ? (w < 0.8f ? -8 : 14) : 6, 0, Mathf.Sin(phase) * 3);
        }
    }
}
