using UnityEngine;

namespace Crab.Game
{
    /// <summary>A guard of the treasury: a knight in gilded armour with a tower shield and a halberd.
    /// From the front the shield turns bullets and blades (a quarter gets through); energy shots,
    /// and blows from the side or behind, go in full.</summary>
    public sealed class GoldGuard : Enemy
    {
        Transform body, head, armL, armR, legL, legR;
        float phase, clangT;

        protected override void Awake()
        {
            base.Awake();
            body = model ? CrabRig.BodyOf(model) : transform;
            head = CrabRig.Find(transform, "Head");
            armL = CrabRig.Find(transform, "ArmL");
            armR = CrabRig.Find(transform, "ArmR");
            legL = CrabRig.Find(transform, "LegL");
            legR = CrabRig.Find(transform, "LegR");
        }

        protected override Color HitColor => new Color(1f, 0.85f, 0.4f);

        /// <summary>Whether a blow coming along `dir` meets the shield.</summary>
        public bool Blocks(Vector3 dir)
        {
            dir.y = 0;
            return !Hits.Energy && dir.sqrMagnitude > 1e-4f && Vector3.Dot(dir.normalized, transform.forward) < -0.35f;
        }

        public override void Hit(float dmg, Vector3 point, Vector3 dir, GameObject by)
        {
            if (Alive && Blocks(dir))
            {
                Fx.Burst(point, new Color(1f, 0.9f, 0.5f), 8, 3, 0.04f, 0.25f);
                if (clangT <= 0)
                {
                    clangT = 0.15f;
                    Sfx.Play("hit", point, 0.6f, 2.2f);
                }
                if (Target && Target.State.SetFlag("guard_hint"))
                    Story.Hint("Щит держит пули спереди. Зайди сбоку — или бей энергией.");
                dmg *= 0.25f;
            }
            base.Hit(dmg, point, dir, by);
        }

        protected override void Strike(Hero hero, float dist)
        {
            base.Strike(hero, dist);
            Sfx.Play("swing", transform.position + Vector3.up, 0.8f, 0.7f);
        }

        protected override void Animate(float dt, float spd, float w)
        {
            clangT -= dt;
            phase += dt * (1.6f + spd * 2.5f);
            float s = Mathf.Sin(phase) * 28 * Mathf.Clamp01(spd / 1.5f);
            if (legL) legL.localRotation = Quaternion.Euler(s, 0, 0);
            if (legR) legR.localRotation = Quaternion.Euler(-s, 0, 0);
            // the shield stays up before it; the halberd rises and chops down
            if (armL) armL.localRotation = Quaternion.Euler(-35, 0, 0);
            float chop = w >= 0 ? (w < 0.75f ? -140 * w / 0.75f : -140 + 190 * (w - 0.75f) / 0.25f) : -10 + s * 0.3f;
            if (armR) armR.localRotation = Quaternion.Euler(chop, 0, 0);
            if (body) body.localRotation = Quaternion.Euler(w >= 0.75f ? 10 : 2, 0, Mathf.Sin(phase) * 2);
            if (head) head.localRotation = Quaternion.Euler(0, Mathf.Sin(life * 0.9f) * 12, 0);
        }
    }
}
