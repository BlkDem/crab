// The Fire Colossus: a giant of magma rock that rises from the lava lake, the boss of the second
// circle. The fight goes first person. Its three cores (the chest and the two shoulders) burn
// under the stone; while it attacks they are shut, and after each attack it overheats and they
// open for a few seconds — that is when shots go in full. Each core breaks on its own; with every
// broken core the Colossus grows angrier: volleys of fireballs (marked on the ground before they
// fall), a ring of lava rolling out from its fist (jump over it), eruptions under the crab, and
// imps called out of the lava.
using System;
using System.Collections;
using System.Collections.Generic;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    public sealed class Colossus : MonoBehaviour, IHittable, IBoss
    {
        public float coreHealth = 260;
        public float arenaRadius = 17, triggerRadius = 13;
        public Transform model;
        [Tooltip("The middle of the ground the crab fights on (the Colossus itself stands in the lava)")]
        public Transform arena;
        [Tooltip("The fight may begin (the director allows it when the vents are sealed)")]
        public bool awake;
        public Light glow;
        public event Action Started, Defeated;

        sealed class Core
        {
            public Transform t;
            public Health health;
            public Light light;
            public Renderer renderer;
            public bool Broken => health.Dead;
        }

        readonly List<Core> cores = new List<Core>();
        readonly List<Enemy> summoned = new List<Enemy>();
        Transform body, head, armL0, armL1, armR0, armR1, legL, legR;
        Vector3 center;
        bool fighting, dead, open;
        float yaw, rise, openK;
        Quaternion armLGoal = Quaternion.identity, armRGoal = Quaternion.identity, bodyGoal = Quaternion.identity;
        bool hintedShut;
        Vector3 Arena => arena ? arena.position : center;

        public bool Alive => !dead;
        public string Title => "ОГНЕННЫЙ КОЛОСС";
        public bool Fighting => fighting;
        public float Health01
        {
            get
            {
                if (cores.Count == 0) return 1;
                float s = 0;
                foreach (var c in cores) s += c.health.Value;
                return s / (coreHealth * cores.Count);
            }
        }
        public int Broken { get { int n = 0; foreach (var c in cores) if (c.Broken) n++; return n; } }
        public int Phase => 1 + Mathf.Min(2, Broken);
        public bool CoresOpen => open;

        /// <summary>The point the drone (and a test) aims at: the open core that is still whole.</summary>
        public Transform Weak
        {
            get
            {
                foreach (var c in cores) if (!c.Broken) return c.t;
                return null;
            }
        }

        public Vector3 CorePoint(Transform t)
        {
            var col = t ? t.GetComponent<Collider>() : null;
            return col ? col.bounds.center : t ? t.position : transform.position;
        }

        void Awake()
        {
            center = transform.position;
            yaw = transform.eulerAngles.y;
            if (!model && transform.childCount > 0) model = transform.GetChild(0);
            body = model ? CrabRig.BodyOf(model) : CrabRig.Find(transform, "Body");
            head = CrabRig.Find(transform, "Head");
            armL0 = CrabRig.Find(transform, "ArmL_0");
            armL1 = CrabRig.Find(transform, "ArmL_1");
            armR0 = CrabRig.Find(transform, "ArmR_0");
            armR1 = CrabRig.Find(transform, "ArmR_1");
            legL = CrabRig.Find(transform, "LegL");
            legR = CrabRig.Find(transform, "LegR");
            foreach (var name in new[] { "CoreChest", "CoreL", "CoreR" })
            {
                var t = CrabRig.Find(transform, name);
                if (!t) continue;
                // like the Guardian's heart, the geometry sits away from the object's origin
                var sc = t.gameObject.AddComponent<SphereCollider>();
                var mf = t.GetComponent<MeshFilter>();
                if (mf && mf.sharedMesh) sc.center = mf.sharedMesh.bounds.center;
                sc.radius = 0.75f / Mathf.Max(0.01f, t.lossyScale.x);
                var c = new Core { t = t, health = new Health(coreHealth), renderer = t.GetComponent<Renderer>() };
                var lg = new GameObject("CoreLight");
                lg.transform.SetParent(t, false);
                lg.transform.position = sc.bounds.center + transform.forward * 0.6f;
                c.light = lg.AddComponent<Light>();
                c.light.color = new Color(1f, 0.6f, 0.2f);
                c.light.range = 7;
                c.light.intensity = 0;
                cores.Add(c);
                t.gameObject.AddComponent<ColossusCore>().colossus = this;
            }
            rise = 0;
        }

        // ------------------------------------------------------------------ damage
        public void Hit(float damage, Vector3 point, Vector3 dir, GameObject by)
        {
            if (!fighting || dead) return;
            Fx.Burst(point, new Color(0.45f, 0.3f, 0.25f), 6, 2, 0.06f, 0.4f, false);
        }

        public void HitCore(Transform t, float damage, Vector3 point)
        {
            if (!fighting || dead) return;
            var c = cores.Find(x => x.t == t);
            if (c == null || c.Broken) return;
            if (!open)
            {
                // shut: the stone over it takes the blow
                Fx.Burst(point, new Color(0.9f, 0.5f, 0.2f), 6, 2, 0.05f, 0.3f);
                c.health.Hurt(damage * 0.08f);
                if (!hintedShut)
                {
                    hintedShut = true;
                    Story.Hint("Ядра закрыты, пока он атакует. Стреляй, когда они раскалятся!");
                }
                return;
            }
            Fx.Burst(point, new Color(1f, 0.75f, 0.3f), 24, 3, 0.12f, 0.5f);
            Sfx.Play("hit", point, 1, 0.5f);
            if (c.light) c.light.intensity = 25;
            if (c.health.Hurt(damage)) BreakCore(c);
        }

        void BreakCore(Core c)
        {
            Sfx.Play("slam", c.t.position, 1, 0.8f);
            Sfx.Play("roar", transform.position + Vector3.up * 10, 1, 0.7f);
            var p = CorePoint(c.t);
            Fx.Burst(p, new Color(1f, 0.6f, 0.2f), 80, 7, 0.25f, 1.4f, true, 1);
            Fx.Burst(p, new Color(0.25f, 0.2f, 0.18f), 50, 4, 0.4f, 2, false, 0.4f);
            Fx.Flash(p, new Color(1f, 0.6f, 0.2f), 20, 30, 0.6f);
            if (c.light) c.light.intensity = 0;
            if (c.renderer) c.renderer.enabled = false;
            var col = c.t.GetComponent<Collider>();
            if (col) col.enabled = false;
            Hero.Instance?.Cam.Shake(1.5f);
            if (Broken >= cores.Count) StartCoroutine(Fall());
            else Story.Hint($"Ядро разбито! Осталось: {cores.Count - Broken}");
        }

        // ------------------------------------------------------------------ the fight
        void Update()
        {
            float dt = Time.deltaTime;
            var hero = Hero.Instance;
            if (!fighting && !dead && awake && hero && hero.Alive && Vector3.Distance(Flat(hero.transform.position), Flat(Arena)) < triggerRadius)
                StartCoroutine(Fight());
            if (fighting && hero && !dead)
            {
                var to = Flat(hero.transform.position - transform.position);
                if (to.sqrMagnitude > 0.1f)
                    yaw = Mathf.MoveTowardsAngle(yaw, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, dt * 25);
                transform.rotation = Quaternion.Euler(0, yaw, 0);
                var off = Flat(hero.transform.position - Arena);
                if (off.magnitude > arenaRadius)
                {
                    var back = Arena + off.normalized * (arenaRadius - 0.5f);
                    hero.Controller.Teleport(new Vector3(back.x, hero.transform.position.y, back.z), hero.transform.eulerAngles.y);
                }
            }
            openK = Mathf.MoveTowards(openK, open && fighting ? 1 : 0, dt * 3);
            foreach (var c in cores)
            {
                if (c.Broken || !c.light) continue;
                float want = !fighting ? 0.5f : 1.5f + openK * (6 + 3 * Mathf.Sin(Time.time * 9));
                c.light.intensity = Mathf.MoveTowards(c.light.intensity, want, dt * 40);
                // shut cores sink a little into the stone, open ones swell
                c.t.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.15f + 0.05f * Mathf.Sin(Time.time * 9), openK);
            }
            if (glow) glow.intensity = dead ? Mathf.MoveTowards(glow.intensity, 0, dt * 4) : 3 + openK * 4;
            Pose(dt);
        }

        void Pose(float dt)
        {
            if (body)
            {
                // under the lava .. standing, breathing heavily
                body.localPosition = new Vector3(0, Mathf.Lerp(-11f, 0, rise) + Mathf.Sin(Time.time * 0.7f) * 0.12f, 0);
                body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.Euler(Mathf.Lerp(30, 0, rise), 0, 0) * bodyGoal, dt * 3);
            }
            if (head) head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(Mathf.Sin(Time.time * 0.5f) * 4, Mathf.Sin(Time.time * 0.3f) * 8, 0), dt * 2);
            if (armL0) armL0.localRotation = Quaternion.Slerp(armL0.localRotation, armLGoal, dt * 4);
            if (armR0) armR0.localRotation = Quaternion.Slerp(armR0.localRotation, armRGoal, dt * 4);
            if (armL1) armL1.localRotation = Quaternion.Slerp(armL1.localRotation, Quaternion.Euler(-15 * rise, 0, 0), dt * 3);
            if (armR1) armR1.localRotation = Quaternion.Slerp(armR1.localRotation, Quaternion.Euler(-15 * rise, 0, 0), dt * 3);
        }

        static Quaternion Raise(float deg) => Quaternion.AngleAxis(-deg, Vector3.right);
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

        IEnumerator Fight()
        {
            fighting = true;
            var hero = Hero.Instance;
            hero.Controller.frozen = true;
            hero.Combat.locked = true;
            hero.Power.locked = true;
            Started?.Invoke();
            Sfx.Loop("music_limbo", 0, true);
            // it rises out of the lava
            hero.Cam.Shot(center + transform.forward * 22 + Vector3.up * 3, center + Vector3.up * 7);
            Story.Title("ОГНЕННЫЙ КОЛОСС", "Ярость, ставшая камнем");
            for (float t = 0; t < 4; t += Time.deltaTime)
            {
                rise = Mathf.SmoothStep(0, 1, t / 4);
                if (UnityEngine.Random.value < Time.deltaTime * 10)
                    Fx.Burst(center + new Vector3(UnityEngine.Random.Range(-4f, 4f), 0.3f, UnityEngine.Random.Range(-4f, 4f)), new Color(1f, 0.5f, 0.15f), 20, 4, 0.15f, 1, true, 1);
                if (t > 2.5f && t - Time.deltaTime <= 2.5f) { Sfx.Play("roar", transform.position + Vector3.up * 10, 1, 0.6f); hero.Cam.Shake(2); }
                yield return null;
            }
            rise = 1;
            Sfx.Loop("music_boss", 0.8f, true);
            hero.Cam.Face(Mathf.Atan2(center.x - hero.transform.position.x, center.z - hero.transform.position.z) * Mathf.Rad2Deg, -20);
            hero.Cam.SetMode(CamMode.First);
            hero.Combat.firstPerson = true;
            hero.Controller.frozen = false;
            hero.Combat.locked = false;
            hero.Power.locked = false;
            if (hero.Drone) hero.Drone.Engage(Weak ? Weak.GetComponent<ColossusCore>() : null, Weak);
            Story.Task("Победить Колосса: разбить три огненных ядра");
            Story.Hint("Ядра раскаляются после его атак — тогда и стреляй. Прыгай через лавовую волну.");
            yield return new WaitForSeconds(1.5f);
            while (!dead && hero)
            {
                if (!hero.Alive)
                {
                    yield return Reset();
                    yield break;
                }
                int ph = Phase;
                int pick = UnityEngine.Random.Range(0, ph == 1 ? 2 : 4);
                if (pick == 0) yield return Volley();
                else if (pick == 1) yield return LavaWave();
                else if (pick == 2) yield return Eruption();
                else if (summoned.FindAll(e => e && e.Alive).Count < 3) yield return Summon();
                else yield return Volley();
                if (dead) yield break;
                yield return Overheat(ph == 3 ? 3f : 4f);
            }
        }

        /// <summary>After an attack: the cores glow open, the Colossus stands panting.</summary>
        IEnumerator Overheat(float seconds)
        {
            open = true;
            bodyGoal = Quaternion.Euler(-6, 0, 0);
            armLGoal = Raise(-10) * Quaternion.Euler(0, 0, 25);
            armRGoal = Raise(-10) * Quaternion.Euler(0, 0, -25);
            Sfx.Play("beam", transform.position + Vector3.up * 8, 0.5f, 0.5f);
            var hero = Hero.Instance;
            if (hero && hero.Drone) hero.Drone.Engage(Weak ? Weak.GetComponent<ColossusCore>() : null, Weak);
            for (float t = 0; t < seconds && !dead; t += Time.deltaTime)
            {
                if (UnityEngine.Random.value < Time.deltaTime * 8 && Weak)
                    Fx.Burst(CorePoint(Weak), new Color(1f, 0.7f, 0.3f), 4, 1.5f, 0.06f, 0.5f, true, -1);
                yield return null;
            }
            open = false;
            bodyGoal = Quaternion.identity;
            armLGoal = armRGoal = Quaternion.identity;
        }

        /// <summary>The crab fell: the Colossus sinks back into the lava and waits.</summary>
        IEnumerator Reset()
        {
            fighting = false;
            open = false;
            foreach (var e in summoned) if (e) Destroy(e.gameObject);
            summoned.Clear();
            for (float t = 0; t < 3; t += Time.deltaTime) { rise = 1 - t / 3; yield return null; }
            foreach (var c in cores)
            {
                // broken cores stay broken: the crab does not start from nothing
                if (!c.Broken) c.health.Reset(coreHealth);
            }
            armLGoal = armRGoal = Quaternion.identity;
            Sfx.Loop("music_boss", 0, true);
            Sfx.Loop("music_limbo", 0.6f, true);
        }

        Vector3 GroundAt(Vector3 p)
        {
            if (Physics.Raycast(new Vector3(p.x, center.y + 30, p.z), Vector3.down, out var hit, 60, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(transform))
                return hit.point;
            return new Vector3(p.x, center.y, p.z);
        }

        IEnumerator Volley()
        {
            var hero = Hero.Instance;
            armRGoal = Raise(160);
            Sfx.Play("roar", transform.position + Vector3.up * 10, 0.6f, 1.2f);
            yield return new WaitForSeconds(0.9f);
            int n = 3 + Phase;
            for (int i = 0; i < n && hero && hero.Alive && !dead; i++)
            {
                var guess = hero.transform.position + Flat(hero.Controller.Velocity) * 1.2f;
                var at = i == 0 ? hero.transform.position : guess + new Vector3(UnityEngine.Random.Range(-3f, 3f), 0, UnityEngine.Random.Range(-3f, 3f));
                var from = armR1 ? armR1.position + Vector3.up * 2 : transform.position + Vector3.up * 12;
                StartCoroutine(Fireball(from, GroundAt(at), 1.7f, 2.2f, 22));
                yield return new WaitForSeconds(0.35f);
            }
            armRGoal = Quaternion.identity;
            yield return new WaitForSeconds(1.6f);
        }

        IEnumerator Fireball(Vector3 from, Vector3 to, float flight, float radius, float dmg)
        {
            var mark = BossFx.Marker(to, radius, new Color(1f, 0.35f, 0.1f, 0.6f));
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "fireball";
            Destroy(ball.GetComponent<Collider>());
            ball.transform.localScale = Vector3.one * 0.9f;
            var lib = Library.Current;
            if (lib && lib.fire) ball.GetComponent<Renderer>().sharedMaterial = lib.fire;
            var light = ball.AddComponent<Light>();
            light.color = new Color(1f, 0.55f, 0.2f);
            light.range = 8;
            light.intensity = 6;
            Sfx.Play("swing", from, 0.8f, 0.5f);
            for (float t = 0; t < flight; t += Time.deltaTime)
            {
                float k = t / flight;
                ball.transform.position = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 9;
                if (UnityEngine.Random.value < 0.6f) Fx.Burst(ball.transform.position, new Color(1f, 0.5f, 0.15f), 2, 0.5f, 0.25f, 0.5f, true, -0.5f);
                BossFx.Pulse(mark, k);
                yield return null;
            }
            Destroy(ball);
            Destroy(mark);
            Explode(to, radius, dmg);
        }

        void Explode(Vector3 at, float radius, float dmg)
        {
            Sfx.Play("slam", at, 0.8f, 1.3f);
            Fx.Burst(at + Vector3.up * 0.3f, new Color(1f, 0.55f, 0.15f), 40, 5, 0.18f, 0.8f, true, 1);
            Fx.Burst(at + Vector3.up * 0.3f, new Color(0.25f, 0.2f, 0.18f), 20, 2, 0.35f, 1.4f, false, -0.3f);
            Fx.Flash(at + Vector3.up, new Color(1f, 0.55f, 0.2f), 8, 10, 0.25f);
            var hero = Hero.Instance;
            if (!hero || !hero.Alive) return;
            var d = hero.transform.position - at;
            if (Flat(d).magnitude < radius && d.y < 2)
            {
                hero.Hurt(dmg, hero.transform.position, d);
                hero.Controller.Knock(Flat(d).normalized * 5 + Vector3.up * 3);
            }
            else if (Flat(d).magnitude < radius * 3) hero.Cam.Shake(0.4f);
        }

        IEnumerator LavaWave()
        {
            var hero = Hero.Instance;
            armLGoal = Raise(150);
            bodyGoal = Quaternion.Euler(-8, 0, 0);
            Sfx.Play("roar", transform.position + Vector3.up * 10, 0.7f, 0.9f);
            yield return new WaitForSeconds(Phase == 1 ? 1.4f : 1.0f);
            armLGoal = Raise(30);
            bodyGoal = Quaternion.Euler(20, 0, 0);
            yield return new WaitForSeconds(0.25f);
            var at = GroundAt(transform.position + transform.forward * 4.5f);
            Sfx.Play("slam", at, 1, 0.6f);
            hero.Cam.Shake(2);
            Fx.Burst(at + Vector3.up * 0.4f, new Color(1f, 0.5f, 0.15f), 80, 7, 0.25f, 1.2f, true, 1);
            int rings = Phase == 3 ? 2 : 1;
            for (int i = 0; i < rings; i++)
            {
                StartCoroutine(BossFx.Ring(at, Phase == 1 ? 8 : 10.5f, arenaRadius * 1.4f, new Color(1f, 0.45f, 0.1f, 0.9f), 24));
                yield return new WaitForSeconds(0.9f);
            }
            armLGoal = Quaternion.identity;
            bodyGoal = Quaternion.identity;
            yield return new WaitForSeconds(1.2f);
        }

        IEnumerator Eruption()
        {
            var hero = Hero.Instance;
            bodyGoal = Quaternion.Euler(12, 0, 0);
            armLGoal = armRGoal = Raise(60);
            Sfx.Play("roar", transform.position + Vector3.up * 10, 0.7f, 0.7f);
            var spots = new List<Vector3> { GroundAt(hero.transform.position) };
            for (int i = 0; i < 3 + Phase; i++)
            {
                float a = UnityEngine.Random.Range(0, Mathf.PI * 2);
                spots.Add(GroundAt(hero.transform.position + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * UnityEngine.Random.Range(2.5f, 7f)));
            }
            var marks = spots.ConvertAll(s => BossFx.Marker(s, 1.6f, new Color(1f, 0.35f, 0.1f, 0.6f)));
            for (float t = 0; t < 1.3f; t += Time.deltaTime)
            {
                foreach (var m in marks) BossFx.Pulse(m, t / 1.3f);
                foreach (var s in spots) if (UnityEngine.Random.value < 0.15f) Fx.Burst(s, new Color(1f, 0.5f, 0.15f), 2, 1, 0.08f, 0.4f, true, -1);
                yield return null;
            }
            foreach (var m in marks) Destroy(m);
            hero.Cam.Shake(1);
            foreach (var s in spots)
            {
                for (int k = 0; k < 4; k++) Fx.Burst(s + Vector3.up * k * 0.8f, new Color(1f, 0.5f + k * 0.08f, 0.15f), 14, 3, 0.2f, 0.9f, true, -2);
                Fx.Flash(s + Vector3.up, new Color(1f, 0.55f, 0.2f), 6, 8, 0.3f);
                Sfx.Play("slam", s, 0.5f, 1.5f);
                if (hero && hero.Alive && Flat(hero.transform.position - s).magnitude < 1.6f)
                {
                    hero.Hurt(26, hero.transform.position, Vector3.up);
                    hero.Controller.Knock(Vector3.up * 6);
                }
            }
            bodyGoal = Quaternion.identity;
            armLGoal = armRGoal = Quaternion.identity;
            yield return new WaitForSeconds(1.2f);
        }

        IEnumerator Summon()
        {
            armLGoal = armRGoal = Raise(130);
            Sfx.Play("roar", transform.position + Vector3.up * 10, 0.9f, 0.8f);
            yield return new WaitForSeconds(1f);
            var lib = Library.Current;
            summoned.RemoveAll(e => !e || !e.Alive);
            for (int i = 0; i < 2 + Phase / 3; i++)
            {
                if (!lib || !lib.imp) break;
                float a = UnityEngine.Random.Range(0, Mathf.PI * 2);
                var pos = GroundAt(Arena + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * UnityEngine.Random.Range(5f, arenaRadius - 2));
                var e = Instantiate(lib.imp, pos, Quaternion.identity).GetComponent<Enemy>();
                e.aggro = 99;
                summoned.Add(e);
                Fx.Burst(pos + Vector3.up * 0.5f, new Color(1f, 0.5f, 0.15f), 30, 3, 0.15f, 0.9f, true, 1);
            }
            armLGoal = armRGoal = Quaternion.identity;
            yield return new WaitForSeconds(0.8f);
        }

        IEnumerator Fall()
        {
            dead = true;
            open = false;
            StopAllCoroutines();
            StartCoroutine(FallRoutine());
            yield break;
        }

        IEnumerator FallRoutine()
        {
            var hero = Hero.Instance;
            foreach (var e in summoned) if (e && e.Alive) e.Hit(999, e.transform.position, Vector3.up, gameObject);
            Sfx.Loop("music_boss", 0, true);
            Sfx.Play("roar", transform.position + Vector3.up * 10, 1, 0.5f);
            hero.Cam.Shake(2.5f);
            hero.Combat.firstPerson = false;
            hero.Cam.SetMode(CamMode.Third);
            hero.Controller.faceView = false;
            if (hero.Drone) hero.Drone.Engage(null, null);
            for (float t = 0; t < 5; t += Time.deltaTime)
            {
                rise = 1 - Mathf.SmoothStep(0, 1, t / 5);
                bodyGoal = Quaternion.Euler(t * 6, 0, t * 3);
                if (UnityEngine.Random.value < Time.deltaTime * 10)
                    Fx.Burst(transform.position + Vector3.up * UnityEngine.Random.Range(1f, 10f) + UnityEngine.Random.insideUnitSphere * 2, new Color(1f, 0.55f, 0.2f), 20, 4, 0.2f, 1.2f, true, 1);
                yield return null;
            }
            foreach (var l in GetComponentsInChildren<Light>()) l.enabled = false;
            Sfx.Play("slam", transform.position, 1, 0.5f);
            Fx.Burst(transform.position + Vector3.up, new Color(1f, 0.5f, 0.15f), 150, 8, 0.4f, 2, true, 1);
            fighting = false;
            if (model) model.gameObject.SetActive(false);
            foreach (var c in GetComponents<Collider>()) c.enabled = false;
            Defeated?.Invoke();
        }
    }
}
