// The Reflection of the Crab: the boss of the fourth circle, the first that copies the player. A
// giant crab of dark mirror-glass that moves as the crab's mirror image across the arena (the
// crab steps left — it steps right) and answers with the crab's own means: its gun becomes a
// volley, its blade a lunge, its shield a dome, its portal a step behind the crab, its drone or
// TANK a dark copy. Now and then it shines white — then it is a true mirror and every shot comes
// back to the one who fired. Shards of mirror rain between; at half health envy calls helpers.
using System;
using System.Collections;
using System.Collections.Generic;
using Crab.Core;
using System.Linq;
using UnityEngine;

namespace Crab.Game
{
    public sealed class MirrorCrab : MonoBehaviour, IHittable, IBoss
    {
        public float maxHealth = 1000;
        public float arenaRadius = 16, triggerRadius = 11, speed = 6;
        public Transform arena;
        public CrabRig rig;
        public float scale = 3.5f;
        [Tooltip("The fight may begin (after the Envious ones are dealt with)")]
        public bool awake;
        public Light glow;
        [Tooltip("A point inside the body to aim at (the drone, tests)")]
        public Transform core;
        public event Action Started, Defeated;

        static readonly Color Violet = new Color(0.75f, 0.55f, 1f), Glass = new Color(0.85f, 0.9f, 1f);
        Health health;
        bool fighting, dead, rising, domeUp, summoned2;
        float reflectT, glare, moveLock;
        Vector3 vel;
        Renderer[] rends;
        MaterialPropertyBlock mpb;
        readonly List<GameObject> copies = new List<GameObject>();
        Transform dome;

        public bool Alive => !dead;
        public string Title => "ОТРАЖЕНИЕ КРАБА";
        public bool Fighting => fighting;
        public float Health01 => health == null ? 1 : health.Value / health.Max;
        /// <summary>Shining: every blow comes back to the one who struck.</summary>
        public bool Reflecting => reflectT > 0 || domeUp;
        public Vector3 AimPoint => core ? core.position : transform.position + Vector3.up * 0.35f * scale;
        Vector3 Center => arena ? arena.position : transform.position;

        void Awake()
        {
            health = new Health(maxHealth);
            rends = GetComponentsInChildren<Renderer>();
            mpb = new MaterialPropertyBlock();
        }

        // ------------------------------------------------------------------ damage
        public void Hit(float damage, Vector3 point, Vector3 dir, GameObject by)
        {
            if (!fighting || dead) return;
            var hero = Hero.Instance;
            if (Reflecting)
            {
                // the mirror sends it back
                Fx.Burst(point, Glass, 14, 3, 0.06f, 0.4f);
                Sfx.Play("hit", point, 0.6f, 2.2f);
                if (hero && by == hero.gameObject && hero.Alive)
                {
                    Fx.Tracer(point, hero.transform.position + Vector3.up * 0.3f, Glass, 0.04f, 0.12f);
                    hero.Hurt(Mathf.Max(4, damage * 0.4f), hero.transform.position, -dir);
                }
                if (hero && hero.State.SetFlag("mirror_hint"))
                    Story.Hint("Когда Отражение сияет — оно возвращает удары. Жди!");
                return;
            }
            Fx.Burst(point, Violet, 16, 3, 0.08f, 0.45f);
            Sfx.Play("hit", point, 0.9f, 1.4f);
            glare = Mathf.Max(glare, 0.4f);
            if (health.Hurt(damage)) StartCoroutine(Shatter());
            else if (!summoned2 && Health01 < 0.5f) { summoned2 = true; StartCoroutine(CallEnvy()); }
        }

        // ------------------------------------------------------------------ the fight
        void Update()
        {
            float dt = Time.deltaTime;
            var hero = Hero.Instance;
            if (!fighting && !dead && awake && hero && hero.Alive && Vector3.Distance(Flat(hero.transform.position), Flat(Center)) < triggerRadius)
                StartCoroutine(Fight());
            reflectT -= dt;
            glare = Mathf.MoveTowards(glare, Reflecting ? 1 : 0, dt * 3);
            foreach (var r in rends)
            {
                if (!r) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_EmissionColor", Color.Lerp(new Color(0.12f, 0.08f, 0.2f), new Color(2.5f, 2.6f, 3f), glare));
                r.SetPropertyBlock(mpb);
            }
            if (glow) glow.intensity = dead ? 0 : 1.5f + glare * 8;
            if (fighting && hero && !dead)
            {
                // the mirror image: across the arena's centre from the crab
                if (moveLock <= 0)
                {
                    var off = Flat(hero.transform.position - Center);
                    var goal = Center - off;
                    var g = Flat(goal - Center);
                    if (g.magnitude > arenaRadius - 3) goal = Center + g.normalized * (arenaRadius - 3);
                    var to = Flat(goal - transform.position);
                    var want = to.magnitude > 0.3f ? to.normalized * Mathf.Min(speed, to.magnitude * 3) : Vector3.zero;
                    vel = Vector3.MoveTowards(vel, want, dt * 20);
                    transform.position += vel * dt;
                    transform.position = new Vector3(transform.position.x, GroundY(transform.position), transform.position.z);
                }
                else moveLock -= dt;
                var face = Flat(hero.transform.position - transform.position);
                if (face.sqrMagnitude > 0.1f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), 1 - Mathf.Exp(-5 * dt));
                // its shield answers the crab's
                if (!domeUp && hero.Shield && hero.Shield.Active) StartCoroutine(Dome());
                // the crab stays in the arena
                var offc = Flat(hero.transform.position - Center);
                if (offc.magnitude > arenaRadius)
                {
                    var back = Center + offc.normalized * (arenaRadius - 0.5f);
                    hero.Controller.Teleport(new Vector3(back.x, hero.transform.position.y, back.z), hero.transform.eulerAngles.y);
                }
            }
            if (rig)
            {
                rig.velocity = vel / scale;
                rig.grounded = true;
            }
        }

        float GroundY(Vector3 p)
        {
            if (Physics.Raycast(new Vector3(p.x, Center.y + 10, p.z), Vector3.down, out var hit, 30, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(transform))
                return hit.point.y;
            return Center.y;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

        IEnumerator Fight()
        {
            fighting = true;
            var hero = Hero.Instance;
            hero.Controller.frozen = true;
            hero.Combat.locked = true;
            hero.Power.locked = true;
            Started?.Invoke();
            Sfx.Loop("music_envy", 0, true);
            // it rises out of the mirror lake
            var end = transform.position;
            var start = end + Vector3.down * 4 * scale * 0.3f;
            transform.position = start;
            hero.Cam.Shot(end + transform.forward * 9 + Vector3.up * 2.5f, end + Vector3.up * 1.5f);
            Story.Title("ОТРАЖЕНИЕ КРАБА", "Всё, что у тебя есть, — есть и у меня");
            for (float t = 0; t < 3; t += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0, 1, t / 3));
                if (UnityEngine.Random.value < Time.deltaTime * 14) Fx.Burst(end + UnityEngine.Random.insideUnitSphere * 3, Glass, 6, 2, 0.06f, 0.6f);
                yield return null;
            }
            Sfx.Play("roar", transform.position + Vector3.up * 2, 1, 1.6f);
            Story.Think("Это… я? Огромный. Пустой внутри. Он хочет быть мной.", 4);
            yield return new WaitForSeconds(1.5f);
            Sfx.Loop("music_boss", 0.8f, true);
            hero.Cam.Face(Mathf.Atan2(transform.position.x - hero.transform.position.x, transform.position.z - hero.transform.position.z) * Mathf.Rad2Deg, -4);
            hero.Cam.SetMode(CamMode.First);
            hero.Combat.firstPerson = true;
            hero.Controller.frozen = false;
            hero.Combat.locked = false;
            hero.Power.locked = false;
            if (hero.Drone) hero.Drone.Engage(this, core ? core : transform);
            Story.Task("Победить Отражение: бей, пока оно не сияет");
            Story.Hint("Оно повторяет твои движения зеркально и копирует твоё оружие. Когда сияет — не бей.");
            yield return new WaitForSeconds(1.2f);
            int n = 0;
            while (!dead && hero)
            {
                if (!hero.Alive)
                {
                    yield return Reset();
                    yield break;
                }
                n++;
                var bp = Blueprints.Get(hero.State.Items.Equipped);
                // every third move it shines; between them it copies, or rains shards
                if (n % 3 == 0) yield return Shine();
                else if (hero.Portal && UnityEngine.Random.value < 0.3f) yield return StepBehind();
                else if ((hero.Drone || hero.Tank) && copies.Count == 0 && UnityEngine.Random.value < 0.4f) yield return CopyCompanion(hero);
                else if (UnityEngine.Random.value < 0.35f) yield return Shards();
                else if (bp != null && bp.kind == WeaponKind.Gun) yield return Volley(bp);
                else yield return Lunge();
                if (dead) yield break;
                yield return new WaitForSeconds(Health01 < 0.5f ? 0.8f : 1.3f);
            }
        }

        IEnumerator Reset()
        {
            fighting = false;
            foreach (var c in copies) if (c) Destroy(c);
            copies.Clear();
            yield return new WaitForSeconds(2);
            health.Reset(maxHealth);
            summoned2 = false;
            Sfx.Loop("music_boss", 0, true);
            Sfx.Loop("music_envy", 0.6f, true);
        }

        IEnumerator Shine()
        {
            reflectT = 2.6f;
            Sfx.Play("beam", transform.position + Vector3.up * 2, 0.6f, 2f);
            Fx.Flash(AimPoint, Glass, 10, 12, 0.5f);
            yield return new WaitForSeconds(2.6f);
        }

        IEnumerator Dome()
        {
            domeUp = true;
            if (!dome)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "mirror dome";
                Destroy(go.GetComponent<Collider>());
                var lib = Library.Current;
                if (lib && lib.shield) go.GetComponent<Renderer>().sharedMaterial = lib.shield;
                dome = go.transform;
            }
            dome.gameObject.SetActive(true);
            Story.Hint("Оно подняло щит — как ты.");
            for (float t = 0; t < 4; t += Time.deltaTime)
            {
                dome.position = AimPoint;
                dome.localScale = Vector3.one * scale * 1.6f * Mathf.Clamp01(t * 5) * Mathf.Clamp01((4 - t) * 3);
                yield return null;
            }
            dome.gameObject.SetActive(false);
            domeUp = false;
        }

        /// <summary>The crab's gun, reflected: a volley of the same shots back at it.</summary>
        IEnumerator Volley(Blueprint bp)
        {
            var hero = Hero.Instance;
            var tint = bp.energy ? new Color(0.45f, 0.9f, 1f) : new Color(1f, 0.85f, 0.5f);
            // a tell: its claw glows the colour of the shots
            Fx.Flash(transform.position + transform.forward * scale * 0.4f + Vector3.up * scale * 0.3f, tint, 6, 6, 0.6f);
            rig?.Play(ClawPose.Aim, 2.2f);
            yield return new WaitForSeconds(0.6f);
            int shots = bp.auto ? 10 : 5;
            float gap = bp.auto ? 0.12f : 0.32f;
            for (int i = 0; i < shots && hero && hero.Alive && !dead; i++)
            {
                var from = transform.position + transform.forward * scale * 0.45f + Vector3.up * scale * 0.3f + transform.right * scale * 0.2f;
                // not quite where the crab is — it can be dodged by moving
                var at = hero.transform.position + Vector3.up * 0.3f + UnityEngine.Random.insideUnitSphere * 0.7f;
                bool hit = !Physics.Linecast(from, at, out var block, ~0, QueryTriggerInteraction.Ignore) || block.transform.IsChildOf(hero.transform);
                if (!hit) at = block.point;
                Fx.Tracer(from, at, tint, bp.energy ? 0.06f : 0.04f, 0.1f);
                Sfx.Play("shot", from, 0.7f, (bp.energy ? 1.6f : 1f) * 0.7f);
                if (hit && Vector3.Distance(at, hero.transform.position + Vector3.up * 0.3f) < 0.5f) hero.Hurt(bp.auto ? 4 : 9, at, at - from);
                yield return new WaitForSeconds(gap);
            }
            yield return new WaitForSeconds(0.4f);
        }

        /// <summary>The crab's claws or blade, reflected: a lunge across the arena and a slam.</summary>
        IEnumerator Lunge()
        {
            var hero = Hero.Instance;
            moveLock = 2.2f;
            rig?.Play(ClawPose.Strike, 1.2f);
            Sfx.Play("roar", transform.position + Vector3.up * 2, 0.6f, 1.8f);
            var target = hero.transform.position;
            var mark = BossFx.Marker(target, 2.6f, new Color(0.8f, 0.6f, 1f, 0.6f));
            for (float t = 0; t < 0.7f; t += Time.deltaTime) { BossFx.Pulse(mark, t / 0.7f); yield return null; }
            var from = transform.position;
            var to = target - Flat(target - from).normalized * scale * 0.4f;
            for (float t = 0; t < 0.45f; t += Time.deltaTime)
            {
                var p = Vector3.Lerp(from, to, t / 0.45f);
                transform.position = new Vector3(p.x, GroundY(p) + Mathf.Sin(t / 0.45f * Mathf.PI) * 1.5f, p.z);
                yield return null;
            }
            Destroy(mark);
            Sfx.Play("slam", target, 1, 1.1f);
            Fx.Burst(target + Vector3.up * 0.3f, Violet, 50, 5, 0.12f, 0.8f);
            hero.Cam.Shake(1.2f);
            if (hero.Alive && Flat(hero.transform.position - target).magnitude < 2.6f && hero.transform.position.y - target.y < 1.2f)
                hero.Hurt(22, hero.transform.position, hero.transform.position - transform.position);
            vel = Vector3.zero;
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>The crab's portal, reflected: it is suddenly behind the crab.</summary>
        IEnumerator StepBehind()
        {
            var hero = Hero.Instance;
            moveLock = 1.6f;
            Fx.Burst(AimPoint, Violet, 40, 4, 0.1f, 0.6f);
            Sfx.Play("spark", AimPoint, 0.7f, 1.2f);
            var behind = hero.transform.position - Flat(Quaternion.Euler(0, hero.Cam.Yaw, 0) * Vector3.forward) * 5;
            var g = Flat(behind - Center);
            if (g.magnitude > arenaRadius - 2) behind = Center + g.normalized * (arenaRadius - 2);
            transform.position = new Vector3(behind.x, GroundY(behind), behind.z);
            transform.rotation = Quaternion.LookRotation(Flat(hero.transform.position - transform.position));
            vel = Vector3.zero;
            Fx.Burst(AimPoint, Violet, 40, 4, 0.1f, 0.6f);
            Story.Hint("Оно за спиной!");
            yield return new WaitForSeconds(0.9f);
            rig?.Play(ClawPose.Strike, 0.8f);
            Sfx.Play("swing", transform.position, 1, 0.6f);
            yield return new WaitForSeconds(0.3f);
            if (hero.Alive && Vector3.Distance(hero.transform.position, transform.position) < scale * 1.1f)
                hero.Hurt(20, hero.transform.position, hero.transform.position - transform.position);
            yield return new WaitForSeconds(0.4f);
        }

        /// <summary>The crab's companion, reflected: a dark copy that hunts the crab for a while.</summary>
        IEnumerator CopyCompanion(Hero hero)
        {
            var lib = Library.Current;
            if (!lib || !lib.drone) yield break;
            Fx.Flash(AimPoint, Violet, 6, 8, 0.4f);
            var go = Instantiate(lib.drone, AimPoint + Vector3.up * 2, Quaternion.identity);
            go.name = "mirror drone";
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            if (lib.mirror) foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = lib.mirror;
            var md = go.AddComponent<MirrorDrone>();
            md.boss = this;
            copies.Add(go);
            Story.Hint("Оно скопировало твоего спутника!");
            yield return new WaitForSeconds(1);
        }

        public void CopyGone(GameObject go) => copies.Remove(go);

        IEnumerator Shards()
        {
            var hero = Hero.Instance;
            rig?.Play(ClawPose.Think, 1.5f);
            Sfx.Play("glitch", AimPoint, 0.6f, 1.3f);
            for (int i = 0; i < 6 && hero && hero.Alive && !dead; i++)
            {
                var at = hero.transform.position + (i == 0 ? Vector3.zero : new Vector3(UnityEngine.Random.Range(-3.5f, 3.5f), 0, UnityEngine.Random.Range(-3.5f, 3.5f)));
                at.y = GroundY(at);
                StartCoroutine(Shard(at));
                yield return new WaitForSeconds(0.2f);
            }
            yield return new WaitForSeconds(1.2f);
        }

        IEnumerator Shard(Vector3 at)
        {
            const float r = 1.3f, fall = 1.2f;
            var mark = BossFx.Marker(at, r, new Color(0.85f, 0.9f, 1f, 0.6f));
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(go.GetComponent<Collider>());
            go.transform.localScale = new Vector3(0.6f, 1.6f, 0.08f);
            var lib = Library.Current;
            if (lib && lib.mirror) go.GetComponent<Renderer>().sharedMaterial = lib.mirror;
            var from = at + Vector3.up * 14;
            for (float t = 0; t < fall; t += Time.deltaTime)
            {
                float k = t / fall;
                go.transform.position = Vector3.Lerp(from, at + Vector3.up * 0.6f, k * k);
                go.transform.rotation = Quaternion.Euler(t * 300, t * 200, 0);
                BossFx.Pulse(mark, k);
                yield return null;
            }
            Destroy(mark);
            Destroy(go);
            Fx.Burst(at + Vector3.up * 0.3f, Glass, 24, 4, 0.05f, 0.6f, false, 2);
            Sfx.Play("hit", at, 0.8f, 2.4f);
            var hero = Hero.Instance;
            if (hero && hero.Alive && Flat(hero.transform.position - at).magnitude < r && hero.transform.position.y - at.y < 1.5f)
                hero.Hurt(14, hero.transform.position, hero.transform.position - at);
        }

        IEnumerator CallEnvy()
        {
            var lib = Library.Current;
            // never more than two of them about: the fight is with the mirror, not a crowd
            int about = Enemy.All.Count(e => e && e.Alive && e is Envious);
            if (about >= 2) yield break;
            Story.Hint("«Всё, что есть у тебя, должно быть нашим!» — зависть зовёт подмогу");
            for (int i = about; i < 2 && lib && lib.envious; i++)
            {
                float a = UnityEngine.Random.Range(0, Mathf.PI * 2);
                var p = Center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (arenaRadius - 3);
                p.y = GroundY(p);
                var e = Instantiate(lib.envious, p, Quaternion.identity).GetComponent<Enemy>();
                e.aggro = 99;
                Fx.Burst(p + Vector3.up, new Color(0.55f, 1f, 0.35f), 30, 3, 0.12f, 0.8f);
                yield return new WaitForSeconds(0.4f);
            }
        }

        IEnumerator Shatter()
        {
            dead = true;
            StopAllCoroutines();
            StartCoroutine(ShatterRoutine());
            yield break;
        }

        IEnumerator ShatterRoutine()
        {
            var hero = Hero.Instance;
            foreach (var c in copies) if (c) Destroy(c);
            if (dome) dome.gameObject.SetActive(false);
            Sfx.Loop("music_boss", 0, true);
            Sfx.Play("glitch", AimPoint, 1, 0.7f);
            hero.Combat.firstPerson = false;
            hero.Cam.SetMode(CamMode.Third);
            hero.Controller.faceView = false;
            if (hero.Drone) hero.Drone.Engage(null, null);
            // cracks spread, then it bursts into a cloud of glass
            for (float t = 0; t < 2.5f; t += Time.deltaTime)
            {
                glare = Mathf.PingPong(t * 6, 1);
                if (UnityEngine.Random.value < Time.deltaTime * 14) Fx.Burst(AimPoint + UnityEngine.Random.insideUnitSphere * scale * 0.5f, Glass, 8, 3, 0.05f, 0.6f);
                yield return null;
            }
            Sfx.Play("slam", AimPoint, 1, 1.6f);
            Fx.Burst(AimPoint, Glass, 220, 8, 0.07f, 2, false, 1.5f);
            Fx.Burst(AimPoint, Violet, 80, 5, 0.12f, 1.2f);
            Fx.Flash(AimPoint, Glass, 14, 16, 0.6f);
            foreach (var r in rends) if (r) r.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            fighting = false;
            Defeated?.Invoke();
        }
    }
}
