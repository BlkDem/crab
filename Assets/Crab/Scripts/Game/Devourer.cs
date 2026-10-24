// The Devourer of Light: the boss of the fifth circle, a mass of darkness with a maw and long arms
// that fills the end of the hall. The fight goes first person. In the dark it cannot be hurt — its
// heart hides deep in the shadow; every lamp the crab lights around the hall opens it more (one
// lamp: a third of the blow goes in, two: two thirds, three: all). It answers by putting the
// lamps out with its arms, reaching for the crab out of the dark (marked on the floor), sending
// waves of despair (jump them, or lose will) and storms of whispers.
using System;
using System.Collections;
using System.Collections.Generic;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    public sealed class Devourer : MonoBehaviour, IHittable, IBoss
    {
        public float maxHealth = 1000;
        public float arenaRadius = 17, triggerRadius = 12;
        public Transform model, arena;
        public List<MemoryLamp> lamps = new List<MemoryLamp>();
        public Light heartLight;
        [Tooltip("The fight may begin (once the crab wears the cyber-frame)")]
        public bool awake;
        public event Action Started, Defeated;

        static readonly Color Violet = new Color(0.6f, 0.4f, 1f), Dark = new Color(0.12f, 0.08f, 0.2f);
        Health health;
        Transform body, maw, heart;
        readonly List<Transform> arms = new List<Transform>();
        bool fighting, dead;
        float rise, open01, breath;
        Renderer[] rends;
        MaterialPropertyBlock mpb;

        public bool Alive => !dead;
        public string Title => "ПОЖИРАТЕЛЬ СВЕТА";
        public bool Fighting => fighting;
        public float Health01 => health == null ? 1 : health.Value / health.Max;
        public int LitLamps => lamps.FindAll(l => l && l.lit).Count;
        /// <summary>How much of a blow reaches the heart (0..1).</summary>
        public float Exposed => lamps.Count == 0 ? 1 : LitLamps / (float)lamps.Count;
        public Vector3 HeartPoint => heart ? heart.position : transform.position + Vector3.up * 5;
        Vector3 Arena => arena ? arena.position : transform.position;

        void Awake()
        {
            health = new Health(maxHealth);
            if (!model && transform.childCount > 0) model = transform.GetChild(0);
            body = model ? CrabRig.BodyOf(model) : transform;
            maw = CrabRig.Find(transform, "Maw");
            heart = CrabRig.Find(transform, "Heart");
            for (int i = 0; i < 6; i++)
            {
                var a = CrabRig.Find(transform, $"Arm{i}");
                if (a) arms.Add(a);
            }
            if (heart)
            {
                var sc = heart.gameObject.AddComponent<SphereCollider>();
                var mf = heart.GetComponent<MeshFilter>();
                if (mf && mf.sharedMesh) sc.center = mf.sharedMesh.bounds.center;
                sc.radius = 0.9f / Mathf.Max(0.01f, heart.lossyScale.x);
                heart.gameObject.AddComponent<DevourerHeart>().devourer = this;
            }
            rends = model ? model.GetComponentsInChildren<Renderer>() : new Renderer[0];
            mpb = new MaterialPropertyBlock();
        }

        // ------------------------------------------------------------------ damage
        public void Hit(float damage, Vector3 point, Vector3 dir, GameObject by) => HitHeart(damage * 0.1f, point);

        public void HitHeart(float damage, Vector3 point)
        {
            if (!fighting || dead) return;
            float k = Exposed;
            if (k <= 0)
            {
                Fx.Burst(point, Dark, 10, 1.5f, 0.2f, 0.6f, false, -0.2f);
                var hero = Hero.Instance;
                if (hero && hero.State.SetFlag("devourer_hint"))
                    Story.AI("Удары тонут в тьме. Зажги огни вокруг зала — свет раскрывает его сердце.", 5);
                return;
            }
            Fx.Burst(point, Violet, (int)(8 + 20 * k), 3, 0.1f, 0.5f);
            Sfx.Play("hit", point, 0.6f + 0.4f * k, 0.8f);
            if (heartLight) heartLight.intensity = 10;
            if (health.Hurt(damage * k)) StartCoroutine(Fall());
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
                var off = Flat(hero.transform.position - Arena);
                if (off.magnitude > arenaRadius)
                {
                    var back = Arena + off.normalized * (arenaRadius - 0.5f);
                    hero.Controller.Teleport(new Vector3(back.x, hero.transform.position.y, back.z), hero.transform.eulerAngles.y);
                }
                var to = Flat(hero.transform.position - transform.position);
                if (to.sqrMagnitude > 0.1f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 1 - Mathf.Exp(-1.5f * dt));
            }
            open01 = Mathf.MoveTowards(open01, dead ? 0 : Exposed, dt * 1.5f);
            breath += dt;
            if (body) body.localPosition = new Vector3(0, Mathf.Lerp(-9, 0, rise) + Mathf.Sin(breath * 0.6f) * 0.25f, 0);
            if (body) body.localScale = Vector3.one * (1 + Mathf.Sin(breath * 0.6f) * 0.03f);
            if (maw) maw.localRotation = Quaternion.Euler(Mathf.Lerp(0, 25, open01) + Mathf.Sin(breath * 1.3f) * 4, 0, 0);
            if (heart) heart.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.1f, open01) * (1 + 0.08f * Mathf.Sin(breath * 5));
            if (heartLight) heartLight.intensity = Mathf.MoveTowards(heartLight.intensity, dead ? 0 : 1 + 6 * open01, dt * 15);
            for (int i = 0; i < arms.Count; i++)
                if (arms[i] && !reaching.Contains(arms[i]))
                    arms[i].localRotation = Quaternion.Euler(Mathf.Sin(breath * 0.8f + i) * 15, Mathf.Sin(breath * 0.5f + i * 2) * 20, 0);
            // the darker its hall, the more it swallows the light around it
            foreach (var r in rends)
            {
                if (!r) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_EmissionColor", Color.Lerp(Color.black, Dark * 2, open01));
                r.SetPropertyBlock(mpb);
            }
        }

        readonly HashSet<Transform> reaching = new HashSet<Transform>();

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

        IEnumerator Fight()
        {
            fighting = true;
            var hero = Hero.Instance;
            hero.Controller.frozen = true;
            hero.Combat.locked = true;
            hero.Power.locked = true;
            Started?.Invoke();
            Sfx.Loop("music_gloom", 0, true);
            foreach (var l in lamps) if (l) l.Light(false);
            hero.Cam.Shot(Arena + Vector3.up * 2, transform.position + Vector3.up * 6);
            Story.Title("ПОЖИРАТЕЛЬ СВЕТА", "Здесь кончается всякий свет");
            for (float t = 0; t < 3.5f; t += Time.deltaTime)
            {
                rise = Mathf.SmoothStep(0, 1, t / 3.5f);
                if (t > 2 && t - Time.deltaTime <= 2) { Sfx.Play("roar", transform.position + Vector3.up * 6, 1, 0.45f); hero.Cam.Shake(1.5f); }
                yield return null;
            }
            rise = 1;
            Story.AI("Масса тьмы. Сердце скрыто. Свет его раскрывает — зажигай огни по кругу.", 5);
            Sfx.Loop("music_boss", 0.7f, true);
            hero.Cam.Face(Mathf.Atan2(transform.position.x - hero.transform.position.x, transform.position.z - hero.transform.position.z) * Mathf.Rad2Deg, -12);
            hero.Cam.SetMode(CamMode.First);
            hero.Combat.firstPerson = true;
            hero.Controller.frozen = false;
            hero.Combat.locked = false;
            hero.Power.locked = false;
            if (hero.Drone) hero.Drone.Engage(heart ? heart.GetComponent<DevourerHeart>() : null, heart);
            if (hero.Hunter) hero.Hunter.Engage(heart ? heart.GetComponent<DevourerHeart>() : null, heart);
            Story.Task("Победить Пожирателя: зажечь огни (держать E) и бить в сердце");
            yield return new WaitForSeconds(2);
            int n = 0;
            while (!dead && hero)
            {
                if (!hero.Alive)
                {
                    yield return Reset();
                    yield break;
                }
                n++;
                if (LitLamps >= 2 && n % 2 == 0) yield return EatLamp();
                else
                {
                    int pick = UnityEngine.Random.Range(0, 3);
                    if (pick == 0) yield return Grasp();
                    else if (pick == 1) yield return DespairWave();
                    else yield return WhisperStorm();
                }
                if (dead) yield break;
                yield return new WaitForSeconds(Health01 < 0.5f ? 1.2f : 2f);
            }
        }

        IEnumerator Reset()
        {
            fighting = false;
            foreach (var l in lamps) if (l) l.Light(false);
            for (float t = 0; t < 2; t += Time.deltaTime) { rise = 1 - t / 2; yield return null; }
            health.Reset(maxHealth);
            Sfx.Loop("music_boss", 0, true);
            Sfx.Loop("music_gloom", 0.6f, true);
        }

        Vector3 GroundAt(Vector3 p)
        {
            if (Physics.Raycast(new Vector3(p.x, Arena.y + 10, p.z), Vector3.down, out var hit, 30, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(transform))
                return hit.point;
            return new Vector3(p.x, Arena.y, p.z);
        }

        Transform FreeArm()
        {
            foreach (var a in arms) if (!reaching.Contains(a)) return a;
            return null;
        }

        /// <summary>It reaches out and snuffs a lamp.</summary>
        IEnumerator EatLamp()
        {
            var lit = lamps.FindAll(l => l && l.lit);
            if (lit.Count == 0) yield break;
            var lamp = lit[UnityEngine.Random.Range(0, lit.Count)];
            var arm = FreeArm();
            if (arm) reaching.Add(arm);
            Story.Hint("Он тянется к огню!");
            Sfx.Play("roar", transform.position + Vector3.up * 6, 0.6f, 0.6f);
            // a shadow crawls over the floor to the lamp; it can be stopped by lighting nothing — only outrun
            var from = transform.position + Vector3.up * 2;
            for (float t = 0; t < 2.2f; t += Time.deltaTime)
            {
                var p = Vector3.Lerp(from, lamp.Point, t / 2.2f);
                if (UnityEngine.Random.value < 0.7f) Fx.Burst(p, Dark, 3, 0.6f, 0.4f, 0.8f, false, -0.1f);
                if (arm) arm.rotation = Quaternion.Slerp(arm.rotation, Quaternion.LookRotation(lamp.Point - arm.position), Time.deltaTime * 3);
                yield return null;
            }
            lamp.Light(false);
            if (arm) reaching.Remove(arm);
            yield return new WaitForSeconds(0.6f);
        }

        IEnumerator Grasp()
        {
            var hero = Hero.Instance;
            Sfx.Play("shade", transform.position + Vector3.up * 4, 0.8f, 0.5f);
            int n = Health01 < 0.5f ? 5 : 3;
            for (int i = 0; i < n && hero && hero.Alive && !dead; i++)
            {
                var at = GroundAt(hero.transform.position + (i == 0 ? Vector3.zero : new Vector3(UnityEngine.Random.Range(-3f, 3f), 0, UnityEngine.Random.Range(-3f, 3f))));
                StartCoroutine(Hand(at));
                yield return new WaitForSeconds(0.45f);
            }
            yield return new WaitForSeconds(1.2f);
        }

        IEnumerator Hand(Vector3 at)
        {
            var mark = BossFx.Marker(at, 1.5f, new Color(0.5f, 0.3f, 0.9f, 0.6f));
            for (float t = 0; t < 1.1f; t += Time.deltaTime)
            {
                BossFx.Pulse(mark, t / 1.1f);
                if (UnityEngine.Random.value < 0.3f) Fx.Burst(at, Dark, 2, 0.5f, 0.3f, 0.6f, false, -1);
                yield return null;
            }
            Destroy(mark);
            // black fingers burst from the floor
            for (int k = 0; k < 5; k++) Fx.Burst(at + Vector3.up * (0.3f + k * 0.4f), Dark, 10, 1.5f, 0.25f, 0.7f, false, -0.5f);
            Sfx.Play("slam", at, 0.6f, 0.6f);
            var hero = Hero.Instance;
            if (hero && hero.Alive && Flat(hero.transform.position - at).magnitude < 1.5f && hero.transform.position.y - at.y < 1.2f)
            {
                hero.Hurt(16, hero.transform.position, Vector3.up);
                Despair.Instance?.Lose(10);
            }
        }

        IEnumerator DespairWave()
        {
            var hero = Hero.Instance;
            Sfx.Play("roar", transform.position + Vector3.up * 6, 0.9f, 0.4f);
            if (maw) maw.localRotation = Quaternion.Euler(35, 0, 0);
            yield return new WaitForSeconds(0.9f);
            var at = GroundAt(transform.position + transform.forward * 4);
            hero.Cam.Shake(1);
            StartCoroutine(Wave(at));
            if (Health01 < 0.5f)
            {
                yield return new WaitForSeconds(0.9f);
                StartCoroutine(Wave(at));
            }
            yield return new WaitForSeconds(1.5f);
        }

        IEnumerator Wave(Vector3 at)
        {
            // like BossFx.Ring, but what it takes is will
            var hero = Hero.Instance;
            float before = hero ? hero.State.Health.Value : 0;
            var co = BossFx.Ring(at, 9, arenaRadius * 1.6f, new Color(0.45f, 0.3f, 0.9f, 0.9f), 10);
            bool lost = false;
            while (co.MoveNext())
            {
                if (!lost && hero && hero.State.Health.Value < before)
                {
                    lost = true;
                    Despair.Instance?.Lose(22);
                }
                yield return co.Current;
            }
        }

        IEnumerator WhisperStorm()
        {
            var d = Despair.Instance;
            Sfx.Play("glitch", transform.position + Vector3.up * 5, 0.8f, 0.5f);
            Story.AI("Психическая атака. Держись ближе к огню.", 3.5f);
            if (d) d.storm = 4;
            for (int i = 0; i < 4; i++)
            {
                d?.Whisper();
                yield return new WaitForSeconds(0.9f);
            }
        }

        IEnumerator Fall()
        {
            dead = true;
            StopAllCoroutines();
            StartCoroutine(FallRoutine());
            yield break;
        }

        IEnumerator FallRoutine()
        {
            var hero = Hero.Instance;
            Sfx.Loop("music_boss", 0, true);
            Sfx.Play("roar", transform.position + Vector3.up * 6, 1, 0.35f);
            hero.Combat.firstPerson = false;
            hero.Cam.SetMode(CamMode.Third);
            hero.Controller.faceView = false;
            if (hero.Drone) hero.Drone.Engage(null, null);
            if (hero.Hunter) hero.Hunter.Engage(null, null);
            // it comes apart into light: the heart bursts, the lamps blaze
            foreach (var l in lamps) if (l) l.Light(true);
            for (float t = 0; t < 4; t += Time.deltaTime)
            {
                rise = 1 - Mathf.SmoothStep(0, 1, t / 4);
                if (UnityEngine.Random.value < Time.deltaTime * 16)
                    Fx.Burst(transform.position + Vector3.up * UnityEngine.Random.Range(1f, 9f) + UnityEngine.Random.insideUnitSphere * 3, new Color(1f, 0.9f, 0.7f), 14, 3, 0.1f, 1.2f, true, -0.5f);
                yield return null;
            }
            Fx.Flash(transform.position + Vector3.up * 4, new Color(1f, 0.95f, 0.8f), 20, 40, 1.5f);
            Story.Flash(new Color(1f, 0.95f, 0.85f, 0.9f), 1.5f);
            if (model) model.gameObject.SetActive(false);
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            Despair.Instance?.Restore();
            fighting = false;
            Defeated?.Invoke();
        }
    }
}
