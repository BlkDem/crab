// TANK, the heavy robot (made at the workshop after the Usurer falls): it walks by the crab,
// steps in front of the nearest demon and pounds it with its fists. Demons that come close turn
// on it — it is built to take the blows.
using UnityEngine;

namespace Crab.Game
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class TankBot : MonoBehaviour, IHittable
    {
        public Hero hero;
        public float speed = 2.8f, range = 14, damage = 22, cooldown = 1.1f, maxHealth = 400;
        public Transform model;
        CharacterController cc;
        Transform body, armL, armR, legL, legR, eye;
        float health, punchT, phase, vy, hurtT;
        Enemy target;
        bool left;

        public bool Alive => health > 0;
        public float Health01 => health / maxHealth;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            health = maxHealth;
            if (!model && transform.childCount > 0) model = transform.GetChild(0);
            body = model ? CrabRig.BodyOf(model) : transform;
            armL = CrabRig.Find(transform, "ArmL");
            armR = CrabRig.Find(transform, "ArmR");
            legL = CrabRig.Find(transform, "LegL");
            legR = CrabRig.Find(transform, "LegR");
            eye = CrabRig.Find(transform, "GlowEye");
        }

        public void Hit(float damage, Vector3 point, Vector3 dir, GameObject by)
        {
            if (hero && by == hero.gameObject) return;
            health = Mathf.Max(0, health - damage * 0.5f);
            hurtT = 0.2f;
            Fx.Burst(point, new Color(1f, 0.8f, 0.4f), 10, 2.5f, 0.05f, 0.3f);
            Sfx.Play("hit", point, 0.6f, 0.7f);
            if (health <= 0) StartCoroutine(Rebuild());
        }

        /// <summary>Broken, it falls apart and gathers itself again a little later.</summary>
        System.Collections.IEnumerator Rebuild()
        {
            Fx.Burst(transform.position + Vector3.up, new Color(1f, 0.6f, 0.3f), 50, 4, 0.12f, 1);
            if (model) model.gameObject.SetActive(false);
            Story.Hint("TANK разбит — соберётся снова через 20 секунд");
            yield return new WaitForSeconds(20);
            health = maxHealth;
            if (hero) transform.position = hero.transform.position - hero.transform.forward * 2;
            if (model) model.gameObject.SetActive(true);
            Fx.Assemble(transform, new Color(1f, 0.85f, 0.45f), 0.8f);
        }

        void Update()
        {
            if (!hero) { Destroy(gameObject); return; }
            float dt = Time.deltaTime;
            hurtT -= dt;
            punchT -= dt;
            if (!Alive) return;
            if (!target || !target.Alive || !target.Visible || Vector3.Distance(target.transform.position, hero.transform.position) > range) target = Nearest();
            Vector3 goal;
            if (target) goal = target.transform.position;
            else goal = hero.transform.position - hero.transform.forward * 1.8f + hero.transform.right * 1.4f;
            var to = goal - transform.position;
            to.y = 0;
            float stop = target ? 1.6f : 1.2f;
            var wish = to.magnitude > stop ? to.normalized * speed * (to.magnitude > 8 && !target ? 1.8f : 1) : Vector3.zero;
            vy = cc.isGrounded ? -2 : vy - 18 * dt;
            cc.Move((wish + Vector3.up * vy) * dt);
            if (to.sqrMagnitude > 0.05f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 1 - Mathf.Exp(-6 * dt));
            // left far behind (a jump, a respawn): it catches up at once
            if (Vector3.Distance(transform.position, hero.transform.position) > 30) transform.position = hero.transform.position - hero.transform.forward * 2 + Vector3.up;
            if (target && to.magnitude < 2.2f && punchT <= 0) Punch();
            Animate(dt, wish.magnitude);
        }

        Enemy Nearest()
        {
            Enemy best = null;
            float bd = range;
            foreach (var e in Enemy.All)
            {
                if (!e || !e.Alive || !e.Visible) continue;
                float d = Vector3.Distance(e.transform.position, hero.transform.position);
                if (d < bd) { bd = d; best = e; }
            }
            return best;
        }

        void Punch()
        {
            punchT = cooldown;
            left = !left;
            var p = target.transform.position + Vector3.up * 0.6f;
            target.Hit(damage, p, transform.forward, hero.gameObject);
            Sfx.Play("slam", p, 0.5f, 1.6f);
            Fx.Burst(p, new Color(1f, 0.8f, 0.4f), 12, 3, 0.07f, 0.3f);
            // the demon turns on the robot
            target.Provoke(this);
        }

        void Animate(float dt, float spd)
        {
            phase += dt * (2 + spd * 2.5f);
            float s = Mathf.Sin(phase) * 25 * Mathf.Clamp01(spd);
            if (legL) legL.localRotation = Quaternion.Euler(s, 0, 0);
            if (legR) legR.localRotation = Quaternion.Euler(-s, 0, 0);
            float pk = Mathf.Clamp01(punchT / cooldown);
            float jab = pk > 0.7f ? -90 * (1 - (pk - 0.7f) / 0.3f) : -90 * pk / 0.7f;
            if (armL) armL.localRotation = Quaternion.Euler(left ? jab : -s * 0.5f, 0, 0);
            if (armR) armR.localRotation = Quaternion.Euler(!left ? jab : s * 0.5f, 0, 0);
            if (body) body.localRotation = Quaternion.Euler(hurtT > 0 ? -8 : 3, 0, Mathf.Sin(phase) * 2);
            if (eye) eye.localScale = Vector3.one * (1 + 0.1f * Mathf.Sin(Time.time * 5));
        }
    }
}
