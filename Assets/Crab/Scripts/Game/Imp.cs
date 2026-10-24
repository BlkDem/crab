using UnityEngine;

namespace Crab.Game
{
    /// <summary>An imp of wrath: quick, horned, it runs at the crab, leaps the last metres and slashes.</summary>
    public sealed class Imp : Enemy
    {
        Transform body, head, armL, armR, legL, legR;
        float phase, leapCd, yellT;

        protected override void Awake()
        {
            base.Awake();
            body = model ? CrabRig.BodyOf(model) : transform;
            head = CrabRig.Find(transform, "Head");
            armL = CrabRig.Find(transform, "ArmL");
            armR = CrabRig.Find(transform, "ArmR");
            legL = CrabRig.Find(transform, "LegL");
            legR = CrabRig.Find(transform, "LegR");
            yellT = Random.Range(1f, 5f);
        }

        protected override Color HitColor => new Color(1f, 0.6f, 0.25f);

        protected override void Hunt(Hero hero, float dist, float dt)
        {
            leapCd -= dt;
            if (leapCd <= 0 && dist > 2 && dist < 5 && cc.isGrounded)
            {
                leapCd = Random.Range(2f, 3.5f);
                knock += Flat(hero.transform.position - transform.position).normalized * 7 + Vector3.up * 3;
                Sfx.Play("crawler", transform.position, 0.6f, 0.7f);
            }
            yellT -= dt;
            if (yellT <= 0)
            {
                yellT = Random.Range(3f, 7f);
                Sfx.Play("crawler", transform.position, 0.5f, 0.55f);
            }
        }

        protected override void Animate(float dt, float spd, float w)
        {
            phase += dt * (2 + spd * 3.2f);
            float s = Mathf.Sin(phase) * 40 * Mathf.Clamp01(spd / 2);
            if (legL) legL.localRotation = Quaternion.Euler(s, 0, 0);
            if (legR) legR.localRotation = Quaternion.Euler(-s, 0, 0);
            float swipe = w >= 0 ? (w < 0.7f ? -110 * w / 0.7f : -110 + 200 * (w - 0.7f) / 0.3f) : -s * 0.6f;
            if (armR) armR.localRotation = Quaternion.Euler(swipe, 0, 0);
            if (armL) armL.localRotation = Quaternion.Euler(w >= 0 ? -40 : s * 0.6f, 0, 0);
            if (body) body.localRotation = Quaternion.Euler(10 + spd * 4, 0, Mathf.Sin(phase) * 4);
            if (head) head.localRotation = Quaternion.Euler(Mathf.Sin(life * 3) * 6, Mathf.Sin(life * 1.7f) * 10, 0);
        }
    }
}
