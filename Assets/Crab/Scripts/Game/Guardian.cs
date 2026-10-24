// The Guardian of Limbo: a stone knight-wraith with a lantern and a great stone sword, the boss
// of the first circle. When the crab steps onto its square it rises, and the fight goes first
// person: the lantern's beam sweeps low across the square (jump over it), the sword slams the
// ground and a shockwave rolls out (jump over it), from the second phase it calls shades out
// of the ground, and at the end the beam follows the crab. Only the glowing heart truly hurts
// it; the stone around takes a fraction.
using System;
using System.Collections;
using System.Collections.Generic;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    public sealed class Guardian : MonoBehaviour, IHittable
    {
        public float maxHealth = 600;
        public float arenaRadius = 15, triggerRadius = 11;
        public Transform model;
        [Tooltip("The fight may begin (the director allows it when the rifts are sealed)")]
        public bool awake;
        public Light heartLight;
        public event Action Started, Defeated;
        public event Action<int> PhaseChanged;

        Health health;
        Transform body, head, armL0, armL1, armR0, armR1, lantern, heart, lanternCore;
        Vector3 center;
        bool fighting, dead;
        float yaw;
        int phase = 1;
        Quaternion armLGoal = Quaternion.identity, armRGoal = Quaternion.identity, bodyGoal = Quaternion.identity;
        float rise;   // 0 kneeling .. 1 standing
        BeamFx beam;
        readonly List<Enemy> summoned = new List<Enemy>();

        public bool Alive => health != null && !health.Dead;
        public bool Fighting => fighting;
        public float Health01 => health == null ? 1 : health.Value / health.Max;
        public int Phase => phase;

        void Awake()
        {
            health = new Health(maxHealth);
            center = transform.position;
            yaw = transform.eulerAngles.y;
            if (!model && transform.childCount > 0) model = transform.GetChild(0);
            body = model ? CrabRig.BodyOf(model) : CrabRig.Find(transform, "Body");
            head = CrabRig.Find(transform, "Head");
            armL0 = CrabRig.Find(transform, "ArmL_0");
            armL1 = CrabRig.Find(transform, "ArmL_1");
            armR0 = CrabRig.Find(transform, "ArmR_0");
            armR1 = CrabRig.Find(transform, "ArmR_1");
            lantern = CrabRig.Find(transform, "Lantern");
            lanternCore = CrabRig.Find(transform, "GlowLantern");
            heart = CrabRig.Find(transform, "Heart");
            if (heart)
            {
                // the heart's geometry sits away from its object's origin (Blender placed it in the
                // vertices), so the collider goes to the middle of the mesh
                var sc = heart.gameObject.AddComponent<SphereCollider>();
                var mf = heart.GetComponent<MeshFilter>();
                if (mf && mf.sharedMesh) sc.center = mf.sharedMesh.bounds.center;
                sc.radius = 0.4f / Mathf.Max(0.01f, heart.lossyScale.x);
                heart.gameObject.AddComponent<GuardianHeart>().guardian = this;
            }
            rise = 0;
        }

        // ------------------------------------------------------------------ damage
        public void Hit(float damage, Vector3 point, Vector3 dir, GameObject by) => Damage(damage * 0.15f, point, false);

        public void HitHeart(float damage, Vector3 point) => Damage(damage, point, true);

        void Damage(float dmg, Vector3 point, bool heartHit)
        {
            if (!fighting || dead) return;
            Fx.Burst(point, heartHit ? new Color(0.6f, 1f, 0.75f) : new Color(0.7f, 0.72f, 0.7f), heartHit ? 24 : 8, 3, heartHit ? 0.12f : 0.06f, 0.5f, heartHit);
            if (heartHit)
            {
                Sfx.Play("hit", point, 1, 0.6f);
                if (heartLight) heartLight.intensity = 12;
            }
            if (health.Hurt(dmg)) StartCoroutine(Fall());
            else
            {
                int p = Health01 < 0.25f ? 3 : Health01 < 0.6f ? 2 : 1;
                if (p != phase)
                {
                    phase = p;
                    PhaseChanged?.Invoke(p);
                    Sfx.Play("roar", transform.position + Vector3.up * 5, 1, 1.1f);
                    Hero.Instance?.Cam.Shake(1.2f);
                }
            }
        }

        // ------------------------------------------------------------------ the fight
        void Update()
        {
            float dt = Time.deltaTime;
            var hero = Hero.Instance;
            if (!fighting && !dead && awake && hero && hero.Alive && Vector3.Distance(Flat(hero.transform.position), Flat(center)) < triggerRadius)
                StartCoroutine(Fight());
            if (fighting && hero && !dead)
            {
                var to = Flat(hero.transform.position - transform.position);
                if (to.sqrMagnitude > 0.1f)
                    yaw = Mathf.MoveTowardsAngle(yaw, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, dt * (phase == 3 ? 50 : 35));
                transform.rotation = Quaternion.Euler(0, yaw, 0);
                // the crab cannot leave the square while the Guardian stands
                var off = Flat(hero.transform.position - center);
                if (off.magnitude > arenaRadius)
                {
                    var back = center + off.normalized * (arenaRadius - 0.5f);
                    hero.Controller.Teleport(new Vector3(back.x, hero.transform.position.y, back.z), hero.transform.eulerAngles.y);
                }
            }
            if (heartLight) heartLight.intensity = Mathf.MoveTowards(heartLight.intensity, dead ? 0 : (fighting ? 3 + Mathf.Sin(Time.time * 4) : 1), dt * 20);
            Pose(dt);
        }

        void Pose(float dt)
        {
            if (body)
            {
                // kneeling (sunk and bowed) .. standing, with a slow breath
                body.localPosition = new Vector3(0, Mathf.Lerp(-2.2f, 0, rise) + Mathf.Sin(Time.time * 0.8f) * 0.05f, 0);
                body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.Euler(Mathf.Lerp(25, 0, rise), 0, 0) * bodyGoal, dt * 4);
            }
            if (head) head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(Mathf.Lerp(30, 0, rise), 0, 0), dt * 3);
            if (armL0) armL0.localRotation = Quaternion.Slerp(armL0.localRotation, armLGoal, dt * 5);
            if (armR0) armR0.localRotation = Quaternion.Slerp(armR0.localRotation, armRGoal, dt * 5);
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
            // it rises
            Sfx.Loop("music_limbo", 0, true);
            hero.Cam.Shot(center + transform.forward * 9 + Vector3.up * 2.2f, center + Vector3.up * 4.5f);
            Story.Title("СТРАЖ ЛИМБА", "Тот, кто не пускает дальше");
            for (float t = 0; t < 3; t += Time.deltaTime)
            {
                rise = Mathf.SmoothStep(0, 1, t / 3);
                if (t > 1.2f && t - Time.deltaTime <= 1.2f) { Sfx.Play("roar", transform.position + Vector3.up * 5); hero.Cam.Shake(1.5f); }
                yield return null;
            }
            rise = 1;
            Sfx.Loop("music_boss", 0.8f, true);
            // into the crab's eyes
            hero.Cam.Face(Mathf.Atan2(center.x - hero.transform.position.x, center.z - hero.transform.position.z) * Mathf.Rad2Deg, -5);
            hero.Cam.SetMode(CamMode.First);
            hero.Combat.firstPerson = true;
            hero.Controller.frozen = false;
            hero.Combat.locked = false;
            hero.Power.locked = false;
            Story.Task("Победить Стража: бей в светящееся сердце");
            Story.Hint("Прыгай через луч фонаря и ударную волну (Пробел)");
            yield return new WaitForSeconds(1.5f);
            while (!dead && hero)
            {
                if (!hero.Alive)
                {
                    yield return Reset();
                    yield break;
                }
                int pick = UnityEngine.Random.Range(0, phase == 1 ? 2 : 3);
                if (phase == 3 && UnityEngine.Random.value < 0.45f) yield return TrackingBeam();
                else if (pick == 0) yield return Sweep();
                else if (pick == 1) yield return Slam();
                else yield return Summon();
                yield return new WaitForSeconds(phase == 3 ? 0.6f : 1.2f);
            }
        }

        /// <summary>The crab fell: the Guardian kneels again and waits.</summary>
        IEnumerator Reset()
        {
            fighting = false;
            beam?.Hide();
            foreach (var e in summoned) if (e) Destroy(e.gameObject);
            summoned.Clear();
            for (float t = 0; t < 2; t += Time.deltaTime) { rise = 1 - t / 2; yield return null; }
            health.Reset(maxHealth);
            phase = 1;
            armLGoal = armRGoal = Quaternion.identity;
            Sfx.Loop("music_boss", 0, true);
            Sfx.Loop("music_limbo", 0.6f, true);
        }

        BeamFx Beam => beam ??= new BeamFx(lanternCore ? lanternCore : transform);

        IEnumerator Sweep()
        {
            // the lantern comes down, glows brighter, then the beam sweeps low across the square
            armLGoal = Raise(55) * Quaternion.Euler(0, 0, -20);
            for (float t = 0; t < 1.0f; t += Time.deltaTime) { Beam.Charge(t); yield return null; }
            float from = -70, to = 70;
            if (UnityEngine.Random.value < 0.5f) { from = 70; to = -70; }
            float dur = phase == 1 ? 2.4f : 1.8f;
            Sfx.Loop("beam", 0.7f);
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                float a = Mathf.Lerp(from, to, t / dur);
                var dir = Quaternion.Euler(0, yaw + a, 0) * Vector3.forward;
                var o = new Vector3(transform.position.x, center.y + 0.4f, transform.position.z) + dir * 1.5f;
                Beam.Show(o, dir, arenaRadius * 1.6f);
                BeamDamage(o, dir, 20, true);
                yield return null;
            }
            Sfx.Loop("beam", 0);
            Beam.Hide();
            armLGoal = Quaternion.identity;
        }

        IEnumerator TrackingBeam()
        {
            armLGoal = Raise(70);
            for (float t = 0; t < 0.8f; t += Time.deltaTime) { Beam.Charge(t / 0.8f); yield return null; }
            var hero = Hero.Instance;
            var aim = hero.transform.position;
            Sfx.Loop("beam", 0.8f);
            for (float t = 0; t < 4; t += Time.deltaTime)
            {
                if (!hero || !hero.Alive) break;
                aim = Vector3.MoveTowards(aim, hero.transform.position, Time.deltaTime * 3.2f);
                var o = lanternCore ? lanternCore.position : transform.position + Vector3.up * 3;
                var dir = (aim + Vector3.up * 0.3f - o).normalized;
                Beam.Show(o, dir, 40);
                BeamDamage(o, dir, 14 * Time.deltaTime * 4, false);
                yield return null;
            }
            Sfx.Loop("beam", 0);
            Beam.Hide();
            armLGoal = Quaternion.identity;
        }

        void BeamDamage(Vector3 o, Vector3 dir, float dmg, bool jumpable)
        {
            var hero = Hero.Instance;
            if (!hero || !hero.Alive) return;
            var p = hero.transform.position + Vector3.up * 0.25f;
            var rel = p - o;
            float along = Vector3.Dot(rel, dir);
            if (along < 0) return;
            float off = (rel - dir * along).magnitude;
            bool above = jumpable && hero.transform.position.y > center.y + 0.45f;
            if (off < 0.55f && !above) hero.Hurt(dmg, p, dir);
        }

        IEnumerator Slam()
        {
            var hero = Hero.Instance;
            armRGoal = Raise(150);
            bodyGoal = Quaternion.Euler(-8, 0, 0);
            Sfx.Play("roar", transform.position + Vector3.up * 5, 0.6f, 1.3f);
            yield return new WaitForSeconds(phase == 1 ? 1.2f : 0.9f);
            armRGoal = Raise(40);
            bodyGoal = Quaternion.Euler(18, 0, 0);
            yield return new WaitForSeconds(0.18f);
            var at = transform.position + transform.forward * 3.4f;
            at.y = center.y;
            Sfx.Play("slam", at, 1);
            hero.Cam.Shake(1.6f);
            Fx.Burst(at + Vector3.up * 0.3f, new Color(0.55f, 0.55f, 0.5f), 60, 5, 0.3f, 1.2f, false, 0.3f);
            yield return Shockwave(at);
            armRGoal = Quaternion.identity;
            bodyGoal = Quaternion.identity;
        }

        IEnumerator Shockwave(Vector3 at)
        {
            var go = new GameObject("shockwave");
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = Library.Current ? Library.Current.tracer : null;
            lr.loop = true;
            lr.positionCount = 72;
            lr.startWidth = lr.endWidth = 0.35f;
            lr.startColor = lr.endColor = new Color(0.7f, 1f, 0.8f, 0.8f);
            var hero = Hero.Instance;
            bool hit = false;
            float speed = phase == 1 ? 9 : 12;
            for (float r = 0.5f; r < arenaRadius * 1.3f; r += Time.deltaTime * speed)
            {
                for (int i = 0; i < 72; i++)
                {
                    float a = i / 72f * Mathf.PI * 2;
                    lr.SetPosition(i, at + new Vector3(Mathf.Cos(a) * r, 0.15f, Mathf.Sin(a) * r));
                }
                if (!hit && hero && hero.Alive)
                {
                    float d = Vector3.Distance(Flat(hero.transform.position), Flat(at));
                    if (Mathf.Abs(d - r) < 0.6f && hero.Controller.Grounded)
                    {
                        hit = true;
                        hero.Hurt(25, hero.transform.position, hero.transform.position - at);
                    }
                }
                yield return null;
            }
            Destroy(go);
        }

        IEnumerator Summon()
        {
            armLGoal = Raise(120);
            armRGoal = Raise(120);
            Sfx.Play("roar", transform.position + Vector3.up * 5, 0.8f, 0.8f);
            yield return new WaitForSeconds(1f);
            var lib = Library.Current;
            summoned.RemoveAll(e => !e || !e.Alive);
            int n = Mathf.Min(phase + 1, 4 - summoned.Count);
            for (int i = 0; i < n; i++)
            {
                if (!lib || !lib.shade) break;
                float a = UnityEngine.Random.Range(0, Mathf.PI * 2);
                var pos = center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * UnityEngine.Random.Range(6f, arenaRadius - 2);
                var go = Instantiate(lib.shade, pos, Quaternion.identity);
                var e = go.GetComponent<Enemy>();
                e.aggro = 99;
                summoned.Add(e);
                Fx.Burst(pos + Vector3.up, new Color(0.5f, 1f, 0.7f), 30, 2, 0.12f, 0.9f);
            }
            armLGoal = armRGoal = Quaternion.identity;
        }

        IEnumerator Fall()
        {
            dead = true;
            beam?.Hide();
            Sfx.Loop("beam", 0);
            StopAllCoroutines();
            StartCoroutine(FallRoutine());
            yield break;
        }

        IEnumerator FallRoutine()
        {
            var hero = Hero.Instance;
            foreach (var e in summoned) if (e && e.Alive) e.Hit(999, e.transform.position, Vector3.up, gameObject);
            Sfx.Loop("music_boss", 0, true);
            Sfx.Play("roar", transform.position + Vector3.up * 5, 1, 0.7f);
            hero.Cam.Shake(2);
            hero.Combat.firstPerson = false;
            hero.Cam.SetMode(CamMode.Third);
            hero.Controller.faceView = false;
            for (float t = 0; t < 3.5f; t += Time.deltaTime)
            {
                rise = 1 - t / 3.5f;
                bodyGoal = Quaternion.Euler(t * 12, 0, t * 4);
                if (UnityEngine.Random.value < Time.deltaTime * 6)
                    Fx.Burst(transform.position + Vector3.up * UnityEngine.Random.Range(1f, 5f) + UnityEngine.Random.insideUnitSphere, new Color(0.6f, 1f, 0.75f), 20, 3, 0.15f, 1);
                yield return null;
            }
            foreach (var r in GetComponentsInChildren<Light>()) r.enabled = false;
            if (lanternCore) lanternCore.gameObject.SetActive(false);
            if (heart) heart.gameObject.SetActive(false);
            Sfx.Play("slam", transform.position, 1, 0.6f);
            Fx.Burst(transform.position + Vector3.up * 2, new Color(0.55f, 0.55f, 0.5f), 120, 6, 0.4f, 2, false, 0.2f);
            fighting = false;
            Defeated?.Invoke();
        }

        /// <summary>The lantern's beam: a glowing shaft (a stretched cylinder) and a light along it.</summary>
        sealed class BeamFx
        {
            readonly Transform shaft;
            readonly Light light;
            readonly Transform source;

            public BeamFx(Transform source)
            {
                this.source = source;
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "beam";
                UnityEngine.Object.Destroy(go.GetComponent<Collider>());
                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = Library.Current ? Library.Current.beam : null;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                shaft = go.transform;
                var lg = new GameObject("beam light");
                light = lg.AddComponent<Light>();
                light.type = LightType.Spot;
                light.color = new Color(0.75f, 1f, 0.85f);
                light.range = 30;
                light.spotAngle = 20;
                light.intensity = 0;
                Hide();
            }

            public void Charge(float k)
            {
                light.transform.position = source.position;
                light.intensity = Mathf.Lerp(0, 8, k);
                light.type = LightType.Point;
                light.range = 6;
            }

            public void Show(Vector3 from, Vector3 dir, float length)
            {
                shaft.gameObject.SetActive(true);
                shaft.position = from + dir * length / 2;
                shaft.rotation = Quaternion.FromToRotation(Vector3.up, dir);
                shaft.localScale = new Vector3(0.45f + Mathf.Sin(Time.time * 40) * 0.04f, length / 2, 0.45f);
                light.type = LightType.Spot;
                light.range = length;
                light.transform.SetPositionAndRotation(from, Quaternion.LookRotation(dir));
                light.intensity = 30;
            }

            public void Hide()
            {
                if (shaft) shaft.gameObject.SetActive(false);
                if (light) light.intensity = 0;
            }
        }
    }

    /// <summary>The Guardian's weak point: blows here go to its heart in full.</summary>
    public sealed class GuardianHeart : MonoBehaviour, IHittable
    {
        public Guardian guardian;
        public bool Alive => guardian && guardian.Alive;
        public void Hit(float damage, Vector3 point, Vector3 dir, GameObject by) => guardian.HitHeart(damage, point);
    }
}
