// A demon on foot (the Shades and Scavengers of Limbo and every walker after them). It wanders
// about where it appeared, by reachable places; it hunts the crab only once it sees it (not
// through walls) or is struck, and walks to it by the navmesh's paths, not in a straight line:
// from afar it comes round at its own angle, so a pack closes in from several sides; in the last
// few metres it may circle and wait for its moment before it goes in; packed demons keep apart.
// When the crab is lost from sight it goes where it was last seen, searches, then gives up and
// goes home. A demon that stays stuck repaths, then steps aside, at last is put back on the
// navmesh; it is set down at a free place when it appears. Dissolves into mist when killed.
using System;
using Crab.Core;
using UnityEngine;
using UnityEngine.AI;

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
        public float Health01 => health == null ? 1 : health.Value / health.Max;
        /// <summary>Can be seen and shot (a burrower under the sand cannot).</summary>
        public virtual bool Visible => true;
        float dieT = -1;
        // getting unstuck: when it wants to move but stays put, it goes around for a while
        Vector3 lastPos, detour;
        float stuckT, detourT, stuckFor;
        // finding the way: the path to the current goal, its corners
        NavMeshPath path;
        Vector3[] corners = new Vector3[0];
        int corner;
        Vector3 pathGoal;
        float repathT;
        // the hunt: what it saw last, its own angle of approach, circling before it strikes
        Vector3 lastSeen;
        float unseenT, sightT, flank, stalkT, stalkCd, idleT;
        bool sees;
        int stalkDir = 1;
        /// <summary>How often it circles and waits before going in (0: never).</summary>
        protected virtual float StalkChance => 0.4f;

        protected virtual void Awake()
        {
            cc = GetComponent<CharacterController>();
            health = new Health(maxHealth);
            home = transform.position;
            wanderTo = home;
            if (!model && transform.childCount > 0) model = transform.GetChild(0);
            path = new NavMeshPath();
            flank = UnityEngine.Random.Range(-70f, 70f);
            stalkDir = UnityEngine.Random.value < 0.5f ? 1 : -1;
        }

        /// <summary>Set down where it fits: on the navmesh, clear of walls and other creatures.</summary>
        protected virtual void Start()
        {
            cc.enabled = false;
            var p = transform.position;
            var q = Nav.FreeSpot(p, cc.radius, cc.height);
            if ((q - p).sqrMagnitude > 0.04f) transform.position = q;
            cc.enabled = true;
            home = transform.position;
            wanderTo = home;
        }

        /// <summary>A shove from outside (the gravity gun): adds to the knockback.</summary>
        public virtual void Push(Vector3 v) => knock += v;

        public virtual void Hit(float dmg, Vector3 point, Vector3 dir, GameObject by)
        {
            if (!Alive) return;
            hunting = true;
            unseenT = 0;
            lastSeen = by ? by.transform.position : transform.position - dir;
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
            // fell out of the world (into a pit, through the ground): it is gone, nothing left to chase
            if (transform.position.y < -60) { health.Hurt(99999); Die(); return; }
            hurtT -= dt;
            attackT -= dt;
            var hero = Target;
            provokedT -= dt;
            var foe = Provoked ? provoker.transform : hero ? hero.transform : null;
            float dist = foe ? Vector3.Distance(Flat(foe.position), Flat(transform.position)) : 999;
            // sight: the crab must be near and in view (looked for a few times a second)
            sightT -= dt;
            if (sightT <= 0)
            {
                sightT = 0.25f;
                sees = foe && dist < aggro && (Provoked || Sees(foe));
            }
            if (hero && hero.Alive && sees)
            {
                hunting = true;
                unseenT = 0;
                lastSeen = foe.position;
            }
            else if (hunting && !Provoked)
            {
                unseenT += dt;
                if (unseenT > 8) { hunting = false; wanderTo = home; }   // lost: back home
            }
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
                var away = Flat(transform.position - hero.transform.position).normalized;
                wish = dist < aggro * 1.5f ? Way(transform.position + away * 8) : Vector3.zero;
                if (wish.sqrMagnitude > 0.01f) FaceTowards(wish, dt);
                Hunt(hero, dist, dt);
            }
            else if (hunting && foe && !sees && !Provoked && dist > attackRange)
            {
                // where it was seen last; there, a look round
                var to = Flat(lastSeen - transform.position);
                if (to.magnitude > 1.2f) { wish = Way(lastSeen) * 0.8f; FaceTowards(wish, dt); }
                else transform.Rotate(0, 90 * dt * stalkDir, 0);
            }
            else if (hunting && foe)
            {
                var to = Flat(foe.position - transform.position);
                var dirTo = to.normalized;
                stalkCd -= dt;
                if (stalkT > 0) stalkT -= dt;
                else if (stalkCd <= 0)
                {
                    stalkCd = 1;
                    if (dist < 7 && dist > attackRange * 2 && UnityEngine.Random.value < StalkChance)
                    {
                        stalkT = UnityEngine.Random.Range(1.2f, 2.8f);
                        stalkCd = UnityEngine.Random.Range(4f, 7f);
                        stalkDir = -stalkDir;
                    }
                }
                if (stalkT > 0 && dist > attackRange * 1.5f)
                {
                    // circling, edging a little nearer; it watches the crab all the while
                    var round = Vector3.Cross(Vector3.up, dirTo) * stalkDir;
                    wish = Steer((round + dirTo * (dist > 4.5f ? 0.35f : -0.1f)).normalized) * 0.55f;
                }
                else if (dist > 7)
                {
                    // from afar: round at its own angle, so a pack comes from several sides
                    var off = Quaternion.Euler(0, flank, 0) * Flat(transform.position - foe.position).normalized;
                    wish = Way(foe.position + off * Mathf.Min(5, dist - 4));
                }
                else if (dist > attackRange * 0.85f) wish = Way(foe.position);
                FaceTowards(to, dt);
                if (dist <= attackRange && attackT <= 0 && hurtT <= 0) { stalkT = 0; BeginAttack(); }
                Hunt(hero, dist, dt);
            }
            else
            {
                // wandering by reachable places near home, with pauses
                if (idleT > 0) idleT -= dt;
                else
                {
                    wanderT -= dt;
                    if (wanderT <= 0 || Vector3.Distance(Flat(transform.position), Flat(wanderTo)) < 0.7f)
                    {
                        wanderT = UnityEngine.Random.Range(4f, 8f);
                        idleT = UnityEngine.Random.Range(0.5f, 2.5f);
                        wanderTo = Nav.Somewhere(home, 6);
                    }
                    var to = Flat(wanderTo - transform.position);
                    if (to.magnitude > 0.5f) { wish = Way(wanderTo) * 0.4f; FaceTowards(wish, dt); }
                }
            }
            // packed demons keep apart
            if (wish.sqrMagnitude > 0.01f || hunting)
            {
                var apart = Vector3.zero;
                foreach (var e in All)
                {
                    if (e == this || !e || !e.Alive) continue;
                    var d = Flat(transform.position - e.transform.position);
                    float m = d.magnitude, r = cc.radius + e.cc.radius + 0.6f;
                    if (m < r && m > 1e-3f) apart += d / m * (1 - m / r);
                }
                wish += apart * 0.8f;
            }
            if (wish.sqrMagnitude > 0.01f)
            {
                if (detourT > 0) { detourT -= dt; wish = detour; }
                stuckT += dt;
                if (stuckT > 1)
                {
                    if (Vector3.Distance(Flat(transform.position), Flat(lastPos)) < 0.25f * speed)
                    {
                        stuckFor += stuckT;
                        repathT = 0;   // a new way first, then a step aside
                        detourT = UnityEngine.Random.Range(0.6f, 1.2f);
                        detour = Quaternion.Euler(0, UnityEngine.Random.value < 0.5f ? 80 : -80, 0) * wish.normalized;
                        if (stuckFor > 4) Unstick();
                    }
                    else stuckFor = 0;
                    stuckT = 0;
                    lastPos = transform.position;
                }
            }
            Move(wish * speed * (hurtT > 0 ? 0.3f : 1), dt);
            Animate(dt, new Vector3(velocity.x, 0, velocity.z).magnitude, windT >= 0 ? windT / windup : -1);
        }

        /// <summary>Back on the navmesh at a free place near here (stuck in a corner for long).</summary>
        void Unstick()
        {
            stuckFor = 0;
            cc.enabled = false;
            transform.position = Nav.FreeSpot(transform.position, cc.radius, cc.height, 4);
            cc.enabled = true;
            repathT = 0;
        }

        /// <summary>Nothing solid between its eyes and the foe.</summary>
        protected bool Sees(Transform foe)
        {
            var eye = transform.position + Vector3.up * Mathf.Min(1.2f, cc.height * 0.8f);
            var to = foe.position + Vector3.up * 0.4f - eye;
            foreach (var h in Physics.RaycastAll(eye, to.normalized, to.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                var t = h.collider.transform;
                if (t.IsChildOf(transform) || t.IsChildOf(foe) || h.collider is CharacterController || Hits.Of(h.collider) != null) continue;
                return false;
            }
            return true;
        }

        /// <summary>The way toward a goal: along the navmesh's path (round what stands between; other
        /// demons are kept apart by Update); felt for past obstacles where there is no navmesh.</summary>
        protected Vector3 Way(Vector3 goal)
        {
            var here = transform.position;
            repathT -= Time.deltaTime;
            if (repathT <= 0 || (goal - pathGoal).sqrMagnitude > 2.25f)
            {
                repathT = UnityEngine.Random.Range(0.35f, 0.6f);
                pathGoal = goal;
                corners = new Vector3[0];
                if (Nav.Snap(here, 2, out var from) && Nav.Snap(goal, 4, out var to) && NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) && path.status != NavMeshPathStatus.PathInvalid)
                {
                    corners = path.corners;
                    corner = corners.Length > 1 ? 1 : 0;
                }
            }
            while (corner < corners.Length - 1 && Flat(corners[corner] - here).magnitude < 0.9f) corner++;
            var aim = corners.Length > 0 ? corners[corner] : goal;
            var d = Flat(aim - here);
            if (d.sqrMagnitude < 1e-4f) return Vector3.zero;
            // a path already goes round what stands; feeling the way is for where there is none
            return corners.Length > 0 ? d.normalized : Steer(d.normalized);
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
