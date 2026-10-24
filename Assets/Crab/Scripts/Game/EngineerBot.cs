// ENGINEER (circle of Betrayal): a small walking robot with a welding arm. It keeps near the crab,
// mends it and the TANK with a beam, and when demons come close it sets down a turret.
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class EngineerBot : MonoBehaviour
    {
        public Hero hero;
        public float speed = 3.4f, healEvery = 1.5f, heal = 6, turretEvery = 20;
        public Transform model;
        CharacterController cc;
        Transform body, arm, legL, legR;
        float healT, turretT = 4, phase, vy;
        static readonly Color Weld = new Color(0.5f, 1f, 0.8f);

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (!model && transform.childCount > 0) model = transform.GetChild(0);
            body = model ? CrabRig.BodyOf(model) : transform;
            arm = CrabRig.Find(transform, "Arm");
            legL = CrabRig.Find(transform, "LegL");
            legR = CrabRig.Find(transform, "LegR");
        }

        void Update()
        {
            if (!hero) { Destroy(gameObject); return; }
            float dt = Time.deltaTime;
            var goal = hero.transform.position - hero.transform.forward * 1.5f - hero.transform.right * 1.6f;
            var to = goal - transform.position;
            to.y = 0;
            var wish = to.magnitude > 0.8f ? to.normalized * speed * (to.magnitude > 8 ? 2 : 1) : Vector3.zero;
            vy = cc.isGrounded ? -2 : vy - 18 * dt;
            cc.Move((wish + Vector3.up * vy) * dt);
            if (Vector3.Distance(transform.position, hero.transform.position) > 30) transform.position = hero.transform.position - hero.transform.forward * 2 + Vector3.up;
            var face = Squad.Hacked ? hero.transform.position - transform.position : wish.sqrMagnitude > 0.01f ? wish : hero.transform.position - transform.position;
            face.y = 0;
            if (face.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), 1 - Mathf.Exp(-6 * dt));

            healT -= dt;
            turretT -= dt;
            if (healT <= 0 && hero.Alive)
            {
                healT = healEvery;
                if (Squad.Hacked)
                {
                    // taken over: the welding arm burns instead of mending
                    if (Vector3.Distance(hero.transform.position, transform.position) < 6)
                    {
                        Beam(hero.transform.position + Vector3.up * 0.3f, Squad.HackColor);
                        hero.Hurt(5, hero.transform.position, hero.transform.position - transform.position);
                    }
                }
                else if (hero.State.Health.Value < hero.State.Health.Max)
                {
                    Beam(hero.transform.position + Vector3.up * 0.3f, Weld);
                    hero.State.Health.Heal(heal);
                }
                else if (hero.Tank && hero.Tank.Alive && hero.Tank.Health01 < 1)
                {
                    Beam(hero.Tank.transform.position + Vector3.up * 0.8f, Weld);
                    hero.Tank.Repair(heal * 3);
                }
            }
            if (turretT <= 0 && !Squad.Hacked && NearestEnemy(14))
            {
                turretT = turretEvery;
                PlaceTurret();
            }
            Animate(dt, wish.magnitude);
        }

        static Enemy NearestEnemy(float range)
        {
            var h = Hero.Instance;
            foreach (var e in Enemy.All)
                if (e && e.Alive && e.Visible && Vector3.Distance(e.transform.position, h.transform.position) < range) return e;
            return null;
        }

        void Beam(Vector3 to, Color c)
        {
            var from = arm ? arm.position + arm.forward * 0.3f : transform.position + Vector3.up * 0.6f;
            Fx.Tracer(from, to, c, 0.03f, 0.25f);
            Fx.Burst(to, c, 6, 1.2f, 0.04f, 0.3f);
            Sfx.Play("beam", from, 0.2f, 2.4f);
        }

        void PlaceTurret()
        {
            var lib = Library.Current;
            var at = transform.position + transform.forward * 1.2f;
            GameObject go = lib && lib.turret ? Instantiate(lib.turret, at, transform.rotation) : null;
            if (!go) return;
            go.name = "turret";
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            var t = go.AddComponent<Turret>();
            t.hero = hero;
            Fx.Assemble(go.transform, Weld, 0.8f);
            Sfx.Play("create", at, 0.6f, 1.3f);
        }

        void Animate(float dt, float spd)
        {
            phase += dt * (3 + spd * 3);
            float s = Mathf.Sin(phase) * 28 * Mathf.Clamp01(spd);
            if (legL) legL.localRotation = Quaternion.Euler(s, 0, 0);
            if (legR) legR.localRotation = Quaternion.Euler(-s, 0, 0);
            if (arm) arm.localRotation = Quaternion.Euler(healT > healEvery - 0.4f ? -60 : -10 + Mathf.Sin(phase * 0.5f) * 8, 0, 0);
            if (body) body.localRotation = Quaternion.Euler(4, 0, Mathf.Sin(phase) * 3);
        }
    }
}
