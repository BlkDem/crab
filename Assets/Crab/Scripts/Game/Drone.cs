// SCOUT, the crab's first companion: a little drone that keeps by its shoulder, turns to the
// nearest demon and shoots blue bolts at it. Made from matter like everything else.
using UnityEngine;

namespace Crab.Game
{
    public sealed class Drone : MonoBehaviour
    {
        public Hero hero;
        public float range = 18, damage = 6, cooldown = 0.7f;
        Transform ring, eye;
        float t, shootT;
        Enemy target;
        IHittable bossTarget;
        Transform bossPoint;

        void Awake()
        {
            ring = CrabRig.Find(transform, "Ring");
            eye = CrabRig.Find(transform, "GlowEye");
        }

        void Update()
        {
            if (!hero) { Destroy(gameObject); return; }
            float dt = Time.deltaTime;
            t += dt;
            // above the crab's right shoulder, drifting in a slow loop
            var home = hero.transform.position + Vector3.up * (1.3f + Mathf.Sin(t * 1.3f) * 0.12f) + hero.transform.right * 0.8f + hero.transform.forward * Mathf.Sin(t * 0.7f) * 0.4f;
            transform.position = Vector3.Lerp(transform.position, home, 1 - Mathf.Exp(-4 * dt));
            if (ring) ring.localRotation = Quaternion.Euler(0, t * 900, 0);
            if (eye) eye.localScale = Vector3.one * (1 + 0.15f * Mathf.Sin(t * 6));

            if (!target || !target.Alive || Vector3.Distance(target.transform.position, transform.position) > range) target = Nearest();
            var ordered = Squad.OrderTarget();
            if (ordered && Vector3.Distance(ordered.transform.position, transform.position) < range * 1.5f) target = ordered;
            var look = target ? target.transform.position + Vector3.up * 0.8f : bossPoint ? bossPoint.position : transform.position + hero.transform.forward;
            var d = look - transform.position;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 1 - Mathf.Exp(-6 * dt));
            shootT -= dt;
            if (Squad.Hacked)
            {
                // taken over: it shoots the crab
                if (shootT <= 0 && hero.Alive)
                {
                    shootT = cooldown * 1.5f;
                    var from = transform.position;
                    var at = hero.transform.position + Vector3.up * 0.3f;
                    Fx.Tracer(from, at, Squad.HackColor, 0.025f, 0.08f);
                    hero.Hurt(damage * 0.5f, at, at - from);
                }
                return;
            }
            if (shootT > 0 || !hero.Alive) return;
            if (target) Shoot(target, target.transform.position + Vector3.up * 0.8f);
            else if (bossTarget != null && bossTarget.Alive && bossPoint && Vector3.Distance(bossPoint.position, transform.position) < range * 1.6f) Shoot(bossTarget, bossPoint.position);
        }

        /// <summary>In a boss fight the drone shoots at the boss's weak point.</summary>
        public void Engage(IHittable boss, Transform point)
        {
            bossTarget = boss;
            bossPoint = point;
        }

        Enemy Nearest()
        {
            Enemy best = null;
            float bd = range;
            foreach (var e in Enemy.All)
            {
                if (!e || !e.Alive || !e.Visible) continue;
                float d = Vector3.Distance(e.transform.position, transform.position);
                if (d < bd) { bd = d; best = e; }
            }
            return best;
        }

        void Shoot(IHittable h, Vector3 at)
        {
            shootT = cooldown;
            var from = eye ? eye.position : transform.position;
            Fx.Tracer(from, at, new Color(0.4f, 0.75f, 1f), 0.025f, 0.08f);
            Fx.Flash(from, new Color(0.4f, 0.75f, 1f), 1.5f, 3);
            Sfx.Play("shot", from, 0.35f, 1.8f);
            h.Hit(damage * Squad.Damage, at, (at - from).normalized, hero.gameObject);
        }
    }
}
