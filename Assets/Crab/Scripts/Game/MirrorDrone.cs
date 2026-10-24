using UnityEngine;

namespace Crab.Game
{
    /// <summary>A dark copy of the crab's companion, made by its Reflection: it circles the crab and
    /// shoots it for a while, then cracks. A couple of shots break it sooner.</summary>
    public sealed class MirrorDrone : MonoBehaviour, IHittable
    {
        public MirrorCrab boss;
        public float life = 12, health = 25;
        float t, shootT = 1.5f;

        public bool Alive => health > 0;

        public void Hit(float damage, Vector3 point, Vector3 dir, GameObject by)
        {
            health -= damage;
            Fx.Burst(point, new Color(0.85f, 0.9f, 1f), 10, 2, 0.05f, 0.3f);
            if (health <= 0) Break();
        }

        void Start()
        {
            // something to shoot at: a small collider around it
            var sc = gameObject.AddComponent<SphereCollider>();
            sc.radius = 0.35f;
        }

        void Update()
        {
            var hero = Hero.Instance;
            t += Time.deltaTime;
            if (!hero || t > life || (boss && !boss.Alive)) { Break(); return; }
            var home = hero.transform.position + Quaternion.Euler(0, t * 50, 0) * new Vector3(0, 0, 4) + Vector3.up * 1.6f;
            transform.position = Vector3.Lerp(transform.position, home, 1 - Mathf.Exp(-2.5f * Time.deltaTime));
            transform.rotation = Quaternion.LookRotation(hero.transform.position - transform.position);
            shootT -= Time.deltaTime;
            if (shootT > 0 || !hero.Alive) return;
            shootT = 1.1f;
            var at = hero.transform.position + Vector3.up * 0.3f + Random.insideUnitSphere * 0.4f;
            Fx.Tracer(transform.position, at, new Color(0.8f, 0.6f, 1f), 0.025f, 0.08f);
            Sfx.Play("shot", transform.position, 0.3f, 1.4f);
            if (Vector3.Distance(at, hero.transform.position + Vector3.up * 0.3f) < 0.35f) hero.Hurt(5, at, at - transform.position);
        }

        void Break()
        {
            Fx.Burst(transform.position, new Color(0.85f, 0.9f, 1f), 30, 3, 0.05f, 0.6f, false, 1.5f);
            Sfx.Play("hit", transform.position, 0.5f, 2.4f);
            if (boss) boss.CopyGone(gameObject);
            Destroy(gameObject);
        }
    }
}
