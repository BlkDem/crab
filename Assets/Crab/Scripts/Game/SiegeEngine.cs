// A siege engine of the demon army (circle of Violence). A catapult lobs burning stones at the
// crab and its army (the fall marked on the ground); a siege tower rolls on slowly and lets out
// soldiers. Both are wood and iron: shots and blows break them.
using System;
using System.Collections;
using UnityEngine;

namespace Crab.Game
{
    public sealed class SiegeEngine : MonoBehaviour, IHittable
    {
        public enum Kind { Catapult, Tower }
        public Kind kind;
        public float maxHealth = 400, range = 45, every = 5.5f, speed = 0.8f;
        public Transform arm;
        public Vector3 rollTo;
        public event Action<SiegeEngine> Broken;
        float health, t = 3;
        bool dead;

        public bool Alive => !dead;
        public float Health01 => health / maxHealth;

        void Awake() => health = maxHealth;

        public void Hit(float damage, Vector3 point, Vector3 dir, GameObject by)
        {
            if (dead) return;
            health -= damage;
            Fx.Burst(point, new Color(0.8f, 0.6f, 0.4f), 8, 2, 0.08f, 0.5f, false, 1);
            if (health <= 0) StartCoroutine(Break());
        }

        void Update()
        {
            if (dead) return;
            var hero = Hero.Instance;
            if (!hero || !hero.Alive) return;
            float d = Vector3.Distance(hero.transform.position, transform.position);
            if (kind == Kind.Tower && d < range * 1.5f)
            {
                var to = rollTo - transform.position;
                to.y = 0;
                if (to.magnitude > 1) transform.position += to.normalized * speed * Time.deltaTime;
            }
            if (d > range) return;
            t -= Time.deltaTime;
            if (t > 0) return;
            t = every * UnityEngine.Random.Range(0.8f, 1.2f);
            if (kind == Kind.Catapult) StartCoroutine(Throw(hero));
            else Unload();
        }

        IEnumerator Throw(Hero hero)
        {
            // the target: the crab, or one of its robots
            var target = hero.transform.position;
            if (BattleBot.All.Count > 0 && UnityEngine.Random.value < 0.4f)
            {
                var b = BattleBot.All[UnityEngine.Random.Range(0, BattleBot.All.Count)];
                if (b) target = b.transform.position;
            }
            if (arm) arm.localRotation = Quaternion.Euler(-80, 0, 0);
            Sfx.Play("swing", transform.position, 0.8f, 0.4f);
            var from = arm ? arm.position + arm.forward * 2 : transform.position + Vector3.up * 4;
            const float r = 2.2f, flight = 2.2f;
            var mark = BossFx.Marker(target, r, new Color(1f, 0.4f, 0.15f, 0.6f));
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(ball.GetComponent<Collider>());
            ball.transform.localScale = Vector3.one * 0.8f;
            var lib = Library.Current;
            if (lib && lib.fire) ball.GetComponent<Renderer>().sharedMaterial = lib.fire;
            for (float k = 0; k < flight; k += Time.deltaTime)
            {
                float f = k / flight;
                ball.transform.position = Vector3.Lerp(from, target, f) + Vector3.up * Mathf.Sin(f * Mathf.PI) * 14;
                if (UnityEngine.Random.value < 0.5f) Fx.Burst(ball.transform.position, new Color(1f, 0.5f, 0.15f), 2, 0.5f, 0.2f, 0.4f, true, -0.5f);
                BossFx.Pulse(mark, f);
                if (arm && k > 0.3f) arm.localRotation = Quaternion.Slerp(arm.localRotation, Quaternion.identity, Time.deltaTime * 3);
                yield return null;
            }
            Destroy(mark);
            Destroy(ball);
            Sfx.Play("slam", target, 0.8f, 1.2f);
            Fx.Burst(target + Vector3.up * 0.3f, new Color(1f, 0.55f, 0.15f), 40, 5, 0.15f, 0.8f, true, 1);
            if (hero && hero.Alive && Vector3.Distance(hero.transform.position, target) < r) hero.Hurt(18, hero.transform.position, hero.transform.position - target);
            foreach (var b in BattleBot.All.ToArray()) if (b && Vector3.Distance(b.transform.position, target) < r) b.Hit(30, b.transform.position, Vector3.up, gameObject);
        }

        void Unload()
        {
            var lib = Library.Current;
            if (!lib || !lib.legionary) return;
            for (int i = 0; i < 2; i++)
            {
                var p = transform.position + transform.forward * 3 + transform.right * (i - 0.5f) * 1.8f;
                if (Physics.Raycast(p + Vector3.up * 5, Vector3.down, out var hit, 10, ~0, QueryTriggerInteraction.Ignore)) p.y = hit.point.y;
                var e = Instantiate(lib.legionary, p, transform.rotation).GetComponent<Enemy>();
                e.aggro = 40;
            }
            Sfx.Play("slam", transform.position, 0.5f, 0.9f);
        }

        IEnumerator Break()
        {
            dead = true;
            Sfx.Play("slam", transform.position, 1, 0.6f);
            Fx.Burst(transform.position + Vector3.up * 3, new Color(0.6f, 0.45f, 0.3f), 80, 5, 0.25f, 1.5f, false, 1);
            Fx.Burst(transform.position + Vector3.up * 2, new Color(1f, 0.55f, 0.2f), 50, 4, 0.15f, 1.2f, true, -0.3f);
            Broken?.Invoke(this);
            for (float k = 0; k < 2; k += Time.deltaTime)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, transform.rotation * Quaternion.Euler(0, 0, 25), Time.deltaTime);
                transform.position += Vector3.down * Time.deltaTime * 0.8f;
                yield return null;
            }
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            gameObject.SetActive(false);
        }
    }
}
