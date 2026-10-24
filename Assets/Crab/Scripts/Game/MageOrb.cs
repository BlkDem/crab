// MAG (circle of Betrayal): a sphere of crystal and energy in turning rings. It floats over the
// crab, strikes up to three demons at once with a chain of lightning, and feeds the crab energy.
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    public sealed class MageOrb : MonoBehaviour
    {
        public Hero hero;
        public float range = 16, damage = 14, cooldown = 2.6f;
        Transform ringA, ringB;
        float t, castT = 1.5f, feedT;
        static readonly Color Bolt = new Color(0.75f, 0.6f, 1f);

        void Awake()
        {
            ringA = CrabRig.Find(transform, "RingA");
            ringB = CrabRig.Find(transform, "RingB");
        }

        void Update()
        {
            if (!hero) { Destroy(gameObject); return; }
            float dt = Time.deltaTime;
            t += dt;
            var home = hero.transform.position + Vector3.up * (2.0f + Mathf.Sin(t * 1.7f) * 0.2f) - hero.transform.right * 1.2f + hero.transform.forward * 0.3f;
            transform.position = Vector3.Lerp(transform.position, home, 1 - Mathf.Exp(-3 * dt));
            if (ringA) ringA.localRotation = Quaternion.Euler(t * 120, 0, 30);
            if (ringB) ringB.localRotation = Quaternion.Euler(0, t * -90, 60);
            feedT += dt;
            if (feedT > 4 && !Squad.Hacked) { feedT = 0; hero.State.Matter.Add(Matter.Energy, 1); }
            castT -= dt;
            if (castT > 0 || !hero.Alive) return;
            castT = cooldown;
            if (Squad.Hacked)
            {
                Arc(transform.position, hero.transform.position + Vector3.up * 0.3f, Squad.HackColor);
                hero.Hurt(8, hero.transform.position, Vector3.up);
                return;
            }
            // a chain: from the orb to the nearest demon, on to the next nearest, three links
            var from = transform.position;
            var hitList = new System.Collections.Generic.List<Enemy>();
            for (int i = 0; i < 3; i++)
            {
                Enemy best = null;
                float bd = i == 0 ? range : 7;
                foreach (var e in Enemy.All)
                {
                    if (!e || !e.Alive || !e.Visible || hitList.Contains(e)) continue;
                    float d = Vector3.Distance(e.transform.position, from);
                    if (d < bd) { bd = d; best = e; }
                }
                if (!best) break;
                var at = best.transform.position + Vector3.up * 0.8f;
                Arc(from, at, Bolt);
                Hits.Energy = true;
                best.Hit(damage * Squad.Damage, at, (at - from).normalized, hero.gameObject);
                Hits.Energy = false;
                hitList.Add(best);
                from = at;
            }
            if (hitList.Count > 0) Sfx.Play("thunder", transform.position, 0.25f, 2.2f);
        }

        static void Arc(Vector3 a, Vector3 b, Color c)
        {
            // a jagged line of short tracers
            var p = a;
            for (int i = 1; i <= 5; i++)
            {
                var q = Vector3.Lerp(a, b, i / 5f) + (i < 5 ? Random.insideUnitSphere * 0.35f : Vector3.zero);
                Fx.Tracer(p, q, c, 0.04f, 0.15f);
                p = q;
            }
            Fx.Burst(b, c, 10, 2, 0.05f, 0.3f);
        }
    }
}
