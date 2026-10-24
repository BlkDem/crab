using System;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    /// <summary>A Scavenger: skitters fast on bony legs and leaps at the crab to bite.</summary>
    public sealed class Crawler : Enemy
    {
        Transform jaw, body;
        readonly Transform[,] legs = new Transform[6, 2];
        float phase, leapT = -1, leapCd;
        bool bit;

        protected override void Awake()
        {
            base.Awake();
            body = model ? CrabRig.BodyOf(model) : CrabRig.Find(transform, "Body");
            jaw = CrabRig.Find(transform, "Jaw");
            int k = 0;
            foreach (var s in new[] { "L", "R" })
                for (int i = 0; i < 3; i++, k++)
                {
                    legs[k, 0] = CrabRig.Find(transform, $"Leg{s}{i}_0");
                    legs[k, 1] = legs[k, 0] ? CrabRig.Find(legs[k, 0], $"Leg{s}{i}_1") : null;
                }
        }

        protected override Color HitColor => new Color(0.9f, 0.85f, 0.7f);

        protected override void BeginAttack()
        {
            // bites when close; leaps from further away (in Hunt)
            windT = 0;
        }

        protected override void Hunt(Hero hero, float dist, float dt)
        {
            leapCd -= dt;
            if (leapT < 0 && leapCd <= 0 && dist > 2.2f && dist < 5.5f && cc.isGrounded)
            {
                leapT = 0;
                leapCd = UnityEngine.Random.Range(2.5f, 4f);
                bit = false;
                var to = Flat(hero.transform.position - transform.position).normalized;
                velocity = to * 8 + Vector3.up * 4.5f;
                Sfx.Play("crawler", transform.position, 0.7f);
            }
            if (leapT >= 0)
            {
                leapT += dt;
                if (!bit && dist < 1.2f)
                {
                    bit = true;
                    hero.Hurt(damage, hero.transform.position + Vector3.up * 0.3f, hero.transform.position - transform.position);
                }
                if (leapT > 0.25f && cc.isGrounded) leapT = -1;
            }
        }

        protected override void Move(Vector3 horiz, float dt)
        {
            if (leapT >= 0)   // in the air: keep the leap's momentum
            {
                velocity.y -= 18 * dt;
                knock = Vector3.MoveTowards(knock, Vector3.zero, 10 * dt);
                if (cc.enabled) cc.Move((velocity + knock) * dt);
                return;
            }
            base.Move(horiz, dt);
        }

        protected override void Animate(float dt, float spd, float w)
        {
            phase += dt * (3 + spd * 5);
            for (int k = 0; k < 6; k++)
            {
                if (!legs[k, 0]) continue;
                float side = k < 3 ? -1 : 1;
                float ph = phase + (k % 2 == 0 ? 0 : Mathf.PI) + (k >= 3 ? Mathf.PI : 0);
                float lift = Mathf.Max(0, Mathf.Sin(ph)) * 30 * Mathf.Clamp01(spd / 2);
                float swing = Mathf.Cos(ph) * 25 * Mathf.Clamp01(spd / 2);
                if (leapT >= 0) { lift = 25; swing = -20; }
                legs[k, 0].localRotation = Quaternion.Euler(0, swing, side * lift);
                if (legs[k, 1]) legs[k, 1].localRotation = Quaternion.Euler(0, 0, -side * lift * 0.6f);
            }
            if (jaw) jaw.localRotation = Quaternion.Euler(w >= 0 || leapT >= 0 ? 35 : 5 + Mathf.Sin(life * 9) * 4, 0, 0);
            if (body) body.localRotation = Quaternion.Euler(leapT >= 0 ? -15 : Mathf.Sin(phase * 2) * 2, 0, Mathf.Sin(phase) * 3);
        }
    }
}
