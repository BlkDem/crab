// The weak demons of Limbo. Shades — robed figures that drift slowly and reach out to drain the
// crab's life; Scavengers — bony many-legged things that skitter fast and leap to bite. Both
// wander near where they appeared until the crab comes close, then hunt it, and dissolve into
// mist when killed. Movement is plain steering with a look-ahead around walls (no navmesh).
using System;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    [RequireComponent(typeof(CharacterController))]
    public abstract class Enemy : MonoBehaviour, IHittable
    {
        public float maxHealth = 40, speed = 2, aggro = 14, attackRange = 1.6f, damage = 9, attackCooldown = 1.4f, windup = 0.5f;
        public Transform model;
        public event Action<Enemy> Died;
        /// <summary>Every live demon (the drone picks its targets here).</summary>
        public static readonly System.Collections.Generic.List<Enemy> All = new System.Collections.Generic.List<Enemy>();
        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);

        protected Health health;
        protected CharacterController cc;
        protected Vector3 home, velocity, knock;
        protected float attackT, windT = -1, hurtT, wanderT, life;
        protected Vector3 wanderTo;
        protected bool hunting;
        protected Hero Target => Hero.Instance;
        /// <summary>A companion that struck this demon draws it away from the crab for a while.</summary>
        protected MonoBehaviour provoker;
        IHittable provokerHit;
        float provokedT;
        /// <summary>An ally of the crab (the TANK, a battle robot) struck it: it turns on that one.</summary>
        public void Provoke(MonoBehaviour by)
        {
            if (!(by is IHittable h)) return;
            provoker = by;
            provokerHit = h;
            provokedT = 5;
            hunting = true;
        }
        protected bool Provoked => provoker && provokerHit != null && provokerHit.Alive && provokedT > 0;
        /// <summary>Runs away from the crab instead of fighting (the gold collectors).</summary>
        protected virtual bool Flees => false;
        public bool Alive => health != null && !health.Dead;
        /// <summary>Can be seen and shot (a burrower under the sand cannot).</summary>
        public virtual bool Visible => true;
        float dieT = -1;
        // getting unstuck: when it wants to move but stays put, it goes around for a while
        Vector3 lastPos, detour;
        float stuckT, detourT;

        protected virtual void Awake()
        {
            cc = GetComponent<CharacterController>();
            health = new Health(maxHealth);
            home = transform.position;
            wanderTo = home;
            if (!model && transform.childCount > 0) model = transform.GetChild(0);
        }

        /// <summary>A shove from outside (the gravity gun): adds to the knockback.</summary>
        public virtual void Push(Vector3 v) => knock += v;

        public virtual void Hit(float dmg, Vector3 point, Vector3 dir, GameObject by)
        {
            if (!Alive) return;
            hunting = true;
            hurtT = 0.25f;
            dir.y = 0;
            knock += dir.normalized * 3.5f;
            Fx.Burst(point, HitColor, 14, 2.5f, 0.07f, 0.45f);
            OnHurt();
            if (health.Hurt(dmg)) Die();
        }

        protected virtual Color HitColor => new Color(0.6f, 1f, 0.75f);
        protected virtual void OnHurt() { }

        protected virtual void Die()
        {
            dieT = 0;
            windT = -1;
            cc.enabled = false;
            Sfx.Play("die", transform.position);
            Fx.Burst(transform.position + Vector3.up * 0.8f, new Color(0.5f, 0.6f, 0.58f), 40, 1.2f, 0.25f, 1.4f, false, -0.2f);
            Died?.Invoke(this);
        }

        protected virtual void Update()
        {
            float dt = Time.deltaTime;
            life += dt;
            if (dieT >= 0)
            {
                // dissolve: sink, shrink and fade into the mist
                dieT += dt;
                if (model) model.localScale = Vector3.one * Mathf.Max(0.01f, 1 - dieT / 1.2f);
                transform.position += Vector3.down * dt * 0.4f;
                if (dieT > 1.3f) Destroy(gameObject);
                return;
            }
            hurtT -= dt;
            attackT -= dt;
            var hero = Target;
            provokedT -= dt;
            var foe = Provoked ? provoker.transform : hero ? hero.transform : null;
            float dist = foe ? Vector3.Distance(Flat(foe.position), Flat(transform.position)) : 999;
            if (hero && hero.Alive && dist < aggro) hunting = true;
            if (hero && !hero.Alive) hunting = false;

            Vector3 wish = Vector3.zero;
            if (windT >= 0)
            {
                windT += dt;
                if (windT >= windup)
                {
                    windT = -1;
                    attackT = attackCooldown;
                    Strike(hero, dist);
                }
            }
            else if (hunting && hero && Flees)
            {
                var away = Flat(transform.position - hero.transform.position);
                wish = dist < aggro * 1.5f ? Steer(away.normalized) : Vector3.zero;
                if (wish.sqrMagnitude > 0.01f) FaceTowards(wish, dt);
                Hunt(hero, dist, dt);
            }
            else if (hunting && foe)
            {
                var to = Flat(foe.position - transform.position);
                if (dist > attackRange * 0.85f) wish = Steer(to.normalized);
                FaceTowards(to, dt);
                if (dist <= attackRange && attackT <= 0 && hurtT <= 0) BeginAttack();
                Hunt(hero, dist, dt);
            }
            else
            {
                wanderT -= dt;
                if (wanderT <= 0 || Vector3.Distance(Flat(transform.position), Flat(wanderTo)) < 0.5f)
                {
                    wanderT = UnityEngine.Random.Range(2f, 5f);
                    wanderTo = home + new Vector3(UnityEngine.Random.Range(-4f, 4f), 0, UnityEngine.Random.Range(-4f, 4f));
                }
                var to = Flat(wanderTo - transform.position);
                if (to.magnitude > 0.3f) { wish = Steer(to.normalized) * 0.4f; FaceTowards(to, dt); }
            }
            if (wish.sqrMagnitude > 0.01f)
            {
                if (detourT > 0) { detourT -= dt; wish = detour; }
                stuckT += dt;
                if (stuckT > 1)
                {
                    if (Vector3.Distance(Flat(transform.position), Flat(lastPos)) < 0.25f * speed)
                    {
                        detourT = UnityEngine.Random.Range(0.8f, 1.6f);
                        detour = Quaternion.Euler(0, UnityEngine.Random.value < 0.5f ? 80 : -80, 0) * wish.normalized;
                    }
                    stuckT = 0;
                    lastPos = transform.position;
                }
            }
            Move(wish * speed * (hurtT > 0 ? 0.3f : 1), dt);
            Animate(dt, new Vector3(velocity.x, 0, velocity.z).magnitude, windT >= 0 ? windT / windup : -1);
        }

        protected virtual void BeginAttack() => windT = 0;
        protected virtual void Hunt(Hero hero, float dist, float dt) { }

        /// <summary>The blow at the end of the wind-up: lands if the crab is still in reach.</summary>
        protected virtual void Strike(Hero hero, float dist)
        {
            if (Provoked)
            {
                if (dist <= attackRange * 1.25f) provokerHit.Hit(damage, provoker.transform.position + Vector3.up * 0.6f, provoker.transform.position - transform.position, gameObject);
                return;
            }
            if (!hero || dist > attackRange * 1.25f) return;
            hero.Hurt(damage, hero.transform.position + Vector3.up * 0.3f, hero.transform.position - transform.position);
        }

        protected abstract void Animate(float dt, float speed, float windup01);

        protected virtual void Move(Vector3 horiz, float dt)
        {
            velocity.x = Mathf.MoveTowards(velocity.x, horiz.x, 20 * dt);
            velocity.z = Mathf.MoveTowards(velocity.z, horiz.z, 20 * dt);
            velocity.y = cc.isGrounded ? -2 : velocity.y - 18 * dt;
            knock = Vector3.MoveTowards(knock, Vector3.zero, 10 * dt);
            if (cc.enabled) cc.Move((velocity + knock) * dt);
        }

        /// <summary>Head toward dir, sliding around what blocks the way.</summary>
        protected Vector3 Steer(Vector3 dir)
        {
            var o = transform.position + Vector3.up * 0.5f;
            if (!Physics.SphereCast(o, cc.radius * 0.8f, dir, out var hit, 1.6f, ~0, QueryTriggerInteraction.Ignore) || Hits.Of(hit.collider) != null) return dir;
            foreach (float a in new[] { 45f, -45f, 90f, -90f })
            {
                var d = Quaternion.Euler(0, a, 0) * dir;
                if (!Physics.SphereCast(o, cc.radius * 0.8f, d, out hit, 1.6f, ~0, QueryTriggerInteraction.Ignore)) return d;
            }
            return -dir * 0.3f;
        }

        protected void FaceTowards(Vector3 dir, float dt)
        {
            if (dir.sqrMagnitude < 1e-4f) return;
            var target = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1 - Mathf.Exp(-6 * dt));
        }

        protected static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
    }
}
