// The Warlord of Slaughter: the boss of the seventh circle, a giant armoured demon general with a
// cleaver and a war banner. The fight goes first person — and it is a battle, not a duel: while
// his honour guard stands round him, a red aura makes him untouchable; the crab must send its
// squad and its army at the guards (G), and only then strike him. Twice more he calls a new guard.
// He cleaves the ground (a wave to jump), charges the crab (marked), and calls the flying fortress
// to bombard.
using System;
using System.Collections;
using System.Collections.Generic;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    public sealed class Warlord : MonoBehaviour, IHittable, IBoss
    {
        public float maxHealth = 1300;
        public float arenaRadius = 22, triggerRadius = 16, speed = 1.6f;
        public Transform model, arena, aura;
        public FlyingFortress fortress;
        public Light auraLight;
        public bool awake;
        public event Action Started, Defeated;

        static readonly Color Red = new Color(1f, 0.25f, 0.15f);
        Health health;
        Transform body, head, armL, armR, legL, legR;
        bool fighting, dead;
        int wave;
        float phase, rise, moveLock;
        readonly List<Enemy> guard = new List<Enemy>();

        public bool Alive => !dead;
        public string Title => "ПОЛКОВОДЕЦ РЕЗНИ";
        public bool Fighting => fighting;
        public float Health01 => health == null ? 1 : health.Value / health.Max;
        public bool Guarded => guard.Exists(e => e && e.Alive);
        public IReadOnlyList<Enemy> Guard => guard;
        public Vector3 AimPoint => transform.position + Vector3.up * 4;
        Vector3 Arena => arena ? arena.position : transform.position;

        void Awake()
        {
            health = new Health(maxHealth);
            if (!model && transform.childCount > 0) model = transform.GetChild(0);
            body = model ? CrabRig.BodyOf(model) : transform;
            head = CrabRig.Find(transform, "Head");
            armL = CrabRig.Find(transform, "ArmL");
            armR = CrabRig.Find(transform, "ArmR");
            legL = CrabRig.Find(transform, "LegL");
            legR = CrabRig.Find(transform, "LegR");
        }

        public void Hit(float damage, Vector3 point, Vector3 dir, GameObject by)
        {
            if (!fighting || dead) return;
            if (Guarded)
            {
                Fx.Burst(point, Red, 10, 2, 0.06f, 0.3f);
                Sfx.Play("hit", point, 0.4f, 2.2f);
                var hero = Hero.Instance;
                if (hero && hero.State.SetFlag("guard_aura_hint"))
                    Story.AI("Аура гвардии. Пока гвардейцы стоят — он неуязвим. Отправь отряд на них: G — атаковать цель.", 5);
                return;
            }
            Fx.Burst(point, new Color(1f, 0.6f, 0.3f), 16, 3, 0.08f, 0.4f);
            Sfx.Play("hit", point, 0.9f, 0.7f);
            if (health.Hurt(damage)) { StartCoroutine(Fall()); return; }
            if ((wave == 1 && Health01 < 0.66f) || (wave == 2 && Health01 < 0.33f)) StartCoroutine(CallGuard());
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var hero = Hero.Instance;
            if (!fighting && !dead && awake && hero && hero.Alive && Vector3.Distance(Flat(hero.transform.position), Flat(Arena)) < triggerRadius)
                StartCoroutine(Fight());
            float spd = 0;
            if (fighting && hero && !dead)
            {
                var off = Flat(hero.transform.position - Arena);
                if (off.magnitude > arenaRadius)
                {
                    var b = Arena + off.normalized * (arenaRadius - 0.5f);
                    hero.Controller.Teleport(new Vector3(b.x, hero.transform.position.y, b.z), hero.transform.eulerAngles.y);
                }
                var to = Flat(hero.transform.position - transform.position);
                if (to.sqrMagnitude > 0.1f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 1 - Mathf.Exp(-2 * dt));
                moveLock -= dt;
                if (moveLock <= 0 && to.magnitude > 7)
                {
                    transform.position += to.normalized * speed * dt;
                    transform.position = new Vector3(transform.position.x, GroundY(transform.position), transform.position.z);
                    spd = speed;
                }
            }
            bool guarded = fighting && Guarded;
            if (aura)
            {
                aura.gameObject.SetActive(guarded);
                aura.localScale = Vector3.one * (10 + Mathf.Sin(Time.time * 6) * 0.3f);
            }
            if (auraLight) auraLight.intensity = guarded ? 4 + Mathf.Sin(Time.time * 6) : 0;
            phase += dt * (1 + spd * 1.5f);
            float s = Mathf.Sin(phase * 2) * 20 * Mathf.Clamp01(spd);
            if (legL) legL.localRotation = Quaternion.Euler(s, 0, 0);
            if (legR) legR.localRotation = Quaternion.Euler(-s, 0, 0);
            if (body) body.localPosition = new Vector3(0, Mathf.Lerp(-7, 0, rise), 0);
            if (head) head.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 0.7f) * 4, Mathf.Sin(Time.time * 0.4f) * 8, 0);
        }

        float GroundY(Vector3 p)
        {
            if (Physics.Raycast(new Vector3(p.x, Arena.y + 10, p.z), Vector3.down, out var hit, 30, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(transform))
                return hit.point.y;
            return Arena.y;
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
            Sfx.Loop("music_violence", 0, true);
            hero.Cam.Shot(transform.position + transform.forward * 12 + Vector3.up * 3, transform.position + Vector3.up * 5);
            Story.Title("ПОЛКОВОДЕЦ РЕЗНИ", "Война — его единственная песня");
            for (float t = 0; t < 3; t += Time.deltaTime)
            {
                rise = Mathf.SmoothStep(0, 1, t / 3);
                if (UnityEngine.Random.value < Time.deltaTime * 10) Fx.Burst(transform.position + UnityEngine.Random.insideUnitSphere * 3, new Color(0.5f, 0.4f, 0.3f), 10, 3, 0.2f, 1, false, 1);
                yield return null;
            }
            rise = 1;
            Sfx.Play("roar", transform.position + Vector3.up * 5, 1, 0.75f);
            hero.Cam.Shake(1.5f);
            yield return CallGuard();
            Sfx.Loop("music_boss", 0.8f, true);
            hero.Cam.Face(Mathf.Atan2(transform.position.x - hero.transform.position.x, transform.position.z - hero.transform.position.z) * Mathf.Rad2Deg, -10);
            hero.Cam.SetMode(CamMode.First);
            hero.Combat.firstPerson = true;
            hero.Controller.frozen = false;
            hero.Combat.locked = false;
            hero.Power.locked = false;
            if (hero.Drone) hero.Drone.Engage(this, transform);
            if (hero.Hunter) hero.Hunter.Engage(this, transform);
            if (hero.Tank) hero.Tank.Engage(this, transform);
            Story.Task("Победить Полководца: сначала его гвардию (G — приказ отряду)");
            // the fortress bombs at his word only (its own runs over the field would double the rain)
            if (fortress) fortress.bombing = false;
            yield return new WaitForSeconds(1.5f);
            int n = 0;
            while (!dead && hero)
            {
                if (!hero.Alive)
                {
                    yield return Reset();
                    yield break;
                }
                n++;
                if (n % 3 == 0) yield return Charge();
                else if (n % 3 == 1) yield return Cleave();
                else if (fortress) yield return fortress.Bombard(Health01 < 0.5f ? 5 : 4);
                if (dead) yield break;
                yield return new WaitForSeconds(Health01 < 0.5f ? 1.4f : 2.2f);
            }
        }

        IEnumerator Reset()
        {
            fighting = false;
            if (fortress) fortress.bombing = false;
            foreach (var e in guard) if (e) Destroy(e.gameObject);
            guard.Clear();
            yield return new WaitForSeconds(2);
            health.Reset(maxHealth);
            wave = 0;
            Sfx.Loop("music_boss", 0, true);
            Sfx.Loop("music_violence", 0.6f, true);
        }

        IEnumerator CallGuard()
        {
            wave++;
            var lib = Library.Current;
            if (!lib || !lib.legionary) yield break;
            Story.Hint("«Гвардия, ко мне!» — вокруг него встаёт аура");
            Sfx.Play("roar", transform.position + Vector3.up * 5, 0.8f, 1.1f);
            if (armL) armL.localRotation = Quaternion.Euler(-120, 0, 0);
            int n = 3 + wave;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2;
                var p = transform.position + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 5;
                p.y = GroundY(p);
                var e = Instantiate(lib.legionary, p, Quaternion.LookRotation(Flat(p - transform.position))).GetComponent<Enemy>();
                e.aggro = 99;
                guard.Add(e);
                Fx.Burst(p + Vector3.up, Red, 20, 2.5f, 0.1f, 0.6f);
                yield return new WaitForSeconds(0.2f);
            }
            if (armL) armL.localRotation = Quaternion.identity;
        }

        IEnumerator Cleave()
        {
            var hero = Hero.Instance;
            moveLock = 2.5f;
            if (armR) armR.localRotation = Quaternion.Euler(-150, 0, 0);
            Sfx.Play("roar", transform.position + Vector3.up * 5, 0.6f, 1.2f);
            yield return new WaitForSeconds(1.0f);
            if (armR) armR.localRotation = Quaternion.Euler(30, 0, 0);
            yield return new WaitForSeconds(0.15f);
            var at = transform.position + transform.forward * 3.5f;
            at.y = GroundY(at);
            Sfx.Play("slam", at, 1, 0.8f);
            hero.Cam.Shake(1.4f);
            Fx.Burst(at + Vector3.up * 0.3f, new Color(0.6f, 0.45f, 0.3f), 60, 5, 0.25f, 1, false, 0.5f);
            StartCoroutine(BossFx.Ring(at, 11, arenaRadius * 1.5f, new Color(1f, 0.35f, 0.2f, 0.9f), 16));
            foreach (var b in BattleBot.All.ToArray()) if (b && Vector3.Distance(b.transform.position, at) < 6) b.Hit(40, b.transform.position, Vector3.up, gameObject);
            yield return new WaitForSeconds(0.8f);
            if (armR) armR.localRotation = Quaternion.identity;
        }

        IEnumerator Charge()
        {
            var hero = Hero.Instance;
            moveLock = 3;
            var target = hero.transform.position;
            target.y = GroundY(target);
            var mark = BossFx.Marker(target, 3, new Color(1f, 0.3f, 0.2f, 0.6f));
            Sfx.Play("roar", transform.position + Vector3.up * 5, 0.9f, 0.9f);
            for (float t = 0; t < 1; t += Time.deltaTime) { BossFx.Pulse(mark, t); yield return null; }
            var from = transform.position;
            var to = target - Flat(target - from).normalized * 2;
            for (float t = 0; t < 0.7f; t += Time.deltaTime)
            {
                var p = Vector3.Lerp(from, to, t / 0.7f);
                transform.position = new Vector3(p.x, GroundY(p), p.z);
                phase += Time.deltaTime * 10;
                yield return null;
            }
            Destroy(mark);
            Sfx.Play("slam", target, 1, 1);
            hero.Cam.Shake(1.6f);
            Fx.Burst(target + Vector3.up * 0.3f, new Color(0.6f, 0.45f, 0.3f), 50, 5, 0.2f, 0.8f, false, 0.5f);
            if (hero.Alive && Flat(hero.transform.position - target).magnitude < 3) hero.Hurt(22, hero.transform.position, hero.transform.position - from);
            yield return new WaitForSeconds(0.6f);
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
            if (fortress) fortress.bombing = false;
            foreach (var e in guard) if (e && e.Alive) e.Hit(999, e.transform.position, Vector3.up, gameObject);
            Sfx.Loop("music_boss", 0, true);
            Sfx.Play("roar", transform.position + Vector3.up * 5, 1, 0.55f);
            hero.Combat.firstPerson = false;
            hero.Cam.SetMode(CamMode.Third);
            hero.Controller.faceView = false;
            if (hero.Drone) hero.Drone.Engage(null, null);
            if (hero.Hunter) hero.Hunter.Engage(null, null);
            if (hero.Tank) hero.Tank.Engage(null, null);
            for (float t = 0; t < 3.5f; t += Time.deltaTime)
            {
                if (body) body.localRotation = Quaternion.Euler(Mathf.Lerp(0, 75, t / 3.5f), 0, 0);
                if (body) body.localPosition = new Vector3(0, -t * 0.4f, 0);
                yield return null;
            }
            Sfx.Play("slam", transform.position, 1, 0.5f);
            Fx.Burst(transform.position + Vector3.up * 2, new Color(0.55f, 0.45f, 0.35f), 140, 6, 0.35f, 2, false, 0.4f);
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            fighting = false;
            Defeated?.Invoke();
        }
    }
}
