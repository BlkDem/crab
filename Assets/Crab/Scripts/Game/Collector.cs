using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    /// <summary>A collector of Greed: a hunched demon dragging a sack of gold. It does not fight —
    /// it runs from the crab with its loot; caught, it spills the gold.</summary>
    public sealed class Collector : Enemy
    {
        public int loot = 5;
        Transform body, head, armL, armR, legL, legR, sack;
        float phase, mutterT;

        protected override bool Flees => true;

        protected override void Awake()
        {
            base.Awake();
            body = model ? CrabRig.BodyOf(model) : transform;
            head = CrabRig.Find(transform, "Head");
            armL = CrabRig.Find(transform, "ArmL");
            armR = CrabRig.Find(transform, "ArmR");
            legL = CrabRig.Find(transform, "LegL");
            legR = CrabRig.Find(transform, "LegR");
            sack = CrabRig.Find(transform, "Sack");
            mutterT = Random.Range(1f, 4f);
        }

        protected override Color HitColor => new Color(1f, 0.85f, 0.35f);

        protected override void Hunt(Hero hero, float dist, float dt)
        {
            mutterT -= dt;
            if (mutterT <= 0 && dist < 15)
            {
                mutterT = Random.Range(2.5f, 5f);
                Sfx.Play("crawler", transform.position, 0.4f, 0.45f);
                // a coin drops out of the sack now and then
                Fx.Burst(transform.position + Vector3.up * 0.6f - transform.forward * 0.3f, new Color(1f, 0.82f, 0.25f), 3, 1.5f, 0.05f, 0.6f, true, 2);
            }
        }

        protected override void Die()
        {
            base.Die();
            var hero = Target;
            var p = transform.position + Vector3.up * 0.6f;
            Fx.Burst(p, new Color(1f, 0.82f, 0.25f), 40, 3, 0.07f, 1, true, 2);
            Sfx.Play("absorb", p, 0.8f, 1.3f);
            if (hero && hero.State.Flag("spark"))
            {
                int got = hero.State.Matter.Add(Matter.Gold, loot);
                if (hero.State.SetFlag("met_" + Matter.Gold))
                    Story.Think("Золото. Тяжёлое, мягкое… и как же оно тянет к себе. Понимаю демонов. Почти.", 5);
                if (got > 0)
                {
                    Fx.Stream(p, hero.Core, new Color(1f, 0.82f, 0.25f), 8);
                    Story.Hint($"+{got} золота");
                }
            }
        }

        protected override void Animate(float dt, float spd, float w)
        {
            phase += dt * (2 + spd * 3.5f);
            float s = Mathf.Sin(phase) * 35 * Mathf.Clamp01(spd / 2);
            if (legL) legL.localRotation = Quaternion.Euler(s, 0, 0);
            if (legR) legR.localRotation = Quaternion.Euler(-s, 0, 0);
            if (armL) armL.localRotation = Quaternion.Euler(-s * 0.6f - 20, 0, 0);
            if (armR) armR.localRotation = Quaternion.Euler(s * 0.6f - 20, 0, 0);
            if (body) body.localRotation = Quaternion.Euler(18 + spd * 5, 0, Mathf.Sin(phase) * 5);
            if (sack) sack.localRotation = Quaternion.Euler(Mathf.Sin(phase * 2) * 6, 0, Mathf.Sin(phase) * 8);
            if (head) head.localRotation = Quaternion.Euler(-15, Mathf.Sin(life * 2.3f) * 25, 0);
        }
    }
}
