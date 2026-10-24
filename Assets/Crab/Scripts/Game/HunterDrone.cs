// HUNTER, the long-range drone (circle of Despondency): it hangs high behind the crab and picks
// off demons far ahead with slow, heavy shots — the cyber-frame's scanner shows it the hidden ones.
using UnityEngine;

namespace Crab.Game
{
    public sealed class HunterDrone : MonoBehaviour
    {
        public Hero hero;
        public float range = 35, damage = 22, cooldown = 1.6f;
        float t, shootT = 1;
        Enemy target;
        Transform muzzle;
        IHittable bossTarget;
        Transform bossPoint;

        void Awake() => muzzle = CrabRig.Find(transform, "Muzzle");

        public void Engage(IHittable boss, Transform point)
        {
            bossTarget = boss;
            bossPoint = point;
        }

        void Update()
        {
            if (!hero) { Destroy(gameObject); return; }
            float dt = Time.deltaTime;
            t += dt;
            var home = hero.transform.position + Vector3.up * (2.6f + Mathf.Sin(t * 0.9f) * 0.15f) - hero.transform.forward * 1.5f - hero.transform.right * 1.0f;
            transform.position = Vector3.Lerp(transform.position, home, 1 - Mathf.Exp(-3 * dt));
            if (!target || !target.Alive || !target.Visible || Vector3.Distance(target.transform.position, transform.position) > range) target = Nearest();
            var look = target ? target.transform.position + Vector3.up * 0.8f : bossPoint ? bossPoint.position : transform.position + hero.transform.forward;
            var d = look - transform.position;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 1 - Mathf.Exp(-5 * dt));
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
            else if (bossTarget != null && bossTarget.Alive && bossPoint) Shoot(bossTarget, bossPoint.position);
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
            var from = muzzle ? muzzle.position : transform.position;
            Fx.Tracer(from, at, new Color(1f, 0.35f, 0.3f), 0.04f, 0.15f);
            Fx.Flash(from, new Color(1f, 0.4f, 0.3f), 3, 4);
            Sfx.Play("shot", from, 0.5f, 0.7f);
            Hits.Energy = true;
            h.Hit(damage * Squad.Damage, at, (at - from).normalized, hero.gameObject);
            Hits.Energy = false;
        }
    }
}
