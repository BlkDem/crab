using System;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    /// <summary>A Scavenger: a skeletal beast that lopes on four bony legs, its skull snapping, and leaps
    /// at the crab to bite. Legs Leg{L,R}{0 front, 1 back}_0 (hip) > _1 (knee); Jaw; Tail.</summary>
    public sealed class Crawler : Enemy
    {
        Transform body;
        BoneTurn jaw, tail;
        readonly BoneTurn[,] legs = new BoneTurn[4, 2];
        float phase, leapT = -1, leapCd;
        bool bit;

        protected override void Awake()
        {
            base.Awake();
            body = model ? CrabRig.BodyOf(model) : CrabRig.Find(transform, "Body");
            jaw = BoneTurn.Of(CrabRig.Find(transform, "Jaw"), body);
            tail = BoneTurn.Of(CrabRig.Find(transform, "Tail"), body);
            int k = 0;
            foreach (var s in new[] { "L", "R" })
                for (int i = 0; i < 2; i++, k++)
                {
                    var hip = CrabRig.Find(transform, $"Leg{s}{i}_0");
                    legs[k, 0] = BoneTurn.Of(hip, body);
                    legs[k, 1] = hip ? BoneTurn.Of(CrabRig.Find(hip, $"Leg{s}{i}_1"), body) : null;
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
            // a lope: the diagonal pairs (front left with back right) together, the knees folding
            // as each leg swings forward; in a leap the front legs reach and the back ones kick
            phase += dt * (3 + spd * 3.2f);
            float amp = Mathf.Clamp01(spd / 2.5f);
            for (int k = 0; k < 4; k++)
            {
                if (legs[k, 0] == null) continue;
                bool front = k % 2 == 0;
                float ph = phase + (k == 0 || k == 3 ? 0 : Mathf.PI);
                float swing = -Mathf.Sin(ph) * 30 * amp;
                float fold = Mathf.Max(0, Mathf.Cos(ph)) * 40 * amp;
                if (leapT >= 0) { swing = front ? -40 : 35; fold = front ? 30 : 10; }
                legs[k, 0].Set(Quaternion.Euler(swing, 0, 0));
                legs[k, 1]?.Set(Quaternion.Euler(front ? -fold : fold, 0, 0));
            }
            jaw?.Toward(Quaternion.Euler(w >= 0 || leapT >= 0 ? 28 : 3 + Mathf.Sin(life * 9) * 3, 0, 0), dt * 12);
            tail?.Set(Quaternion.Euler(0, Mathf.Sin(phase * 0.5f) * 18, 0));
            if (body) body.localRotation = Quaternion.Euler(leapT >= 0 ? -12 : Mathf.Sin(phase * 2) * 2 * amp, 0, Mathf.Sin(phase) * 2.5f * amp);
        }
    }
}
