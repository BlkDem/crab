using UnityEngine;

namespace Crab.Game
{
    /// <summary>A turret the ENGINEER sets down: it turns to the nearest demon and fires until it
    /// runs dry, then folds away.</summary>
    public sealed class Turret : MonoBehaviour
    {
        public Hero hero;
        public float life = 18, range = 16, damage = 7, cooldown = 0.35f;
        Transform head, muzzle;
        float t, shootT = 0.6f;

        void Awake()
        {
            head = CrabRig.Find(transform, "Head");
            muzzle = CrabRig.Find(transform, "Muzzle");
        }

        void Update()
        {
            t += Time.deltaTime;
            if (t > life || !hero) { Fx.Burst(transform.position + Vector3.up * 0.5f, new Color(0.5f, 1f, 0.8f), 20, 2, 0.05f, 0.4f); Destroy(gameObject); return; }
            Enemy best = null;
            float bd = range;
            foreach (var e in Enemy.All)
            {
                if (!e || !e.Alive || !e.Visible) continue;
                float d = Vector3.Distance(e.transform.position, transform.position);
                if (d < bd) { bd = d; best = e; }
            }
            if (!best) return;
            var to = best.transform.position + Vector3.up * 0.7f - (head ? head.position : transform.position);
            if (head) head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(new Vector3(to.x, 0, to.z)), 1 - Mathf.Exp(-8 * Time.deltaTime));
            shootT -= Time.deltaTime;
            if (shootT > 0) return;
            shootT = cooldown;
            var from = muzzle ? muzzle.position : transform.position + Vector3.up * 0.6f;
            var at = best.transform.position + Vector3.up * 0.7f;
            Fx.Tracer(from, at, new Color(0.6f, 1f, 0.85f), 0.02f, 0.06f);
            Sfx.Play("shot", from, 0.25f, 1.7f);
            best.Hit(damage * Squad.Damage, at, (at - from).normalized, hero.gameObject);
        }
    }
}
