// A battle robot of the crab's army (circle of Violence): made by the factory, it marches with the
// crab, goes where the crab orders and fires bolts at the demons. Demons it hits turn on it.
using UnityEngine;

namespace Crab.Game
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class BattleBot : MonoBehaviour, IHittable
    {
        public static readonly System.Collections.Generic.List<BattleBot> All = new System.Collections.Generic.List<BattleBot>();
        public Hero hero;
        public float speed = 3.6f, range = 15, damage = 8, cooldown = 0.9f, maxHealth = 80;
        public Vector3 slot;   // its place in the formation round the crab
        CharacterController cc;
        Transform body, legL, legR, gun;
        float health, shootT, phase, vy;
        Enemy target;

        public bool Alive => health > 0;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            health = maxHealth;
            var model = transform.childCount > 0 ? transform.GetChild(0) : transform;
            body = CrabRig.BodyOf(model);
            legL = CrabRig.Find(transform, "LegL");
            legR = CrabRig.Find(transform, "LegR");
            gun = CrabRig.Find(transform, "Gun");
        }

        public void Hit(float dmg, Vector3 point, Vector3 dir, GameObject by)
        {
            if (hero && by == hero.gameObject) return;
            health -= dmg * Squad.Armor;
            Fx.Burst(point, new Color(1f, 0.8f, 0.4f), 8, 2, 0.05f, 0.3f);
            if (health > 0) return;
            Fx.Burst(transform.position + Vector3.up * 0.6f, new Color(1f, 0.6f, 0.3f), 30, 3, 0.08f, 0.8f);
            Sfx.Play("slam", transform.position, 0.4f, 1.8f);
            Destroy(gameObject);
        }

        void Update()
        {
            if (!hero) { Destroy(gameObject); return; }
            float dt = Time.deltaTime;
            var ordered = Squad.OrderTarget(14);
            if (ordered) target = ordered;
            if (!target || !target.Alive || !target.Visible || Vector3.Distance(target.transform.position, transform.position) > range * 1.4f) target = Nearest();
            Vector3 goal;
            if (Squad.Order == Squad.Orders.Attack && !ordered) goal = Squad.Point + slot * 0.5f;
            else if (target && Vector3.Distance(target.transform.position, transform.position) > range * 0.7f) goal = target.transform.position;
            else goal = Squad.Station(hero, slot);
            var to = goal - transform.position;
            to.y = 0;
            var wish = to.magnitude > 1 ? to.normalized * speed * (to.magnitude > 10 ? 1.5f : 1) : Vector3.zero;
            vy = cc.isGrounded ? -2 : vy - 18 * dt;
            cc.Move((wish + Vector3.up * vy) * dt);
            var face = target ? target.transform.position - transform.position : wish;
            face.y = 0;
            if (face.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), 1 - Mathf.Exp(-6 * dt));
            shootT -= dt;
            if (target && shootT <= 0 && Vector3.Distance(target.transform.position, transform.position) < range && !Squad.Hacked)
            {
                shootT = cooldown * Random.Range(0.8f, 1.2f);
                var from = gun ? gun.position : transform.position + Vector3.up * 0.8f;
                var at = target.transform.position + Vector3.up * 0.8f;
                Fx.Tracer(from, at, new Color(0.5f, 0.85f, 1f), 0.025f, 0.07f);
                Sfx.Play("shot", from, 0.25f, 1.5f);
                target.Hit(damage * Squad.Damage, at, (at - from).normalized, hero.gameObject);
                target.Provoke(this);
            }
            phase += dt * (2 + wish.magnitude * 2.5f);
            float s = Mathf.Sin(phase) * 25 * Mathf.Clamp01(wish.magnitude);
            if (legL) legL.localRotation = Quaternion.Euler(s, 0, 0);
            if (legR) legR.localRotation = Quaternion.Euler(-s, 0, 0);
            if (body) body.localRotation = Quaternion.Euler(3, 0, Mathf.Sin(phase) * 2);
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
    }
}
