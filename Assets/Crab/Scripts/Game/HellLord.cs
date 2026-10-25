// The Lord of Hell: the last boss. A giant on a throne of black iron, a crown of fire, four arms.
// The fight goes first person. At first a shield of the circles guards him — four pillars round the
// hall feed it, and the crab breaks them one by one while he hurls fire. Then he rises and fights
// with the echoes of everything the crab has met: the beam of the Guardian, the fire of the
// Colossus, the coins of the Usurer, the hands of the dark. Near the end he offers the crab his
// throne.
using System;
using System.Collections;
using System.Collections.Generic;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    public sealed class HellLord : MonoBehaviour, IHittable, IBoss
    {
        public float maxHealth = 1600;
        public float arenaRadius = 22, triggerRadius = 15;
        public Transform model, arena, shield;
        public List<CirclePillar> pillars = new List<CirclePillar>();
        public Light crownLight;
        public bool awake;
        public event Action Started, Defeated;

        static readonly Color Fire = new Color(1f, 0.45f, 0.15f), Gold = new Color(1f, 0.82f, 0.3f);
        Health health;
        Transform body, head, armL0, armR0;
        bool fighting, dead, standing, offered;
        float rise;
        LineRenderer beam;

        public bool Alive => !dead;
        public string Title => "ВЛАДЫКА АДА";
        public bool Fighting => fighting;
        public float Health01 => health == null ? 1 : health.Value / health.Max;
        public bool Shielded => pillars.Exists(p => p && p.Alive);
        public Vector3 AimPoint => transform.position + Vector3.up * (standing ? 8 : 6);
        Vector3 Arena => arena ? arena.position : transform.position;

        void Awake()
        {
            health = new Health(maxHealth);
            if (!model && transform.childCount > 0) model = transform.GetChild(0);
            body = model ? CrabRig.BodyOf(model) : transform;
            head = CrabRig.Find(transform, "Head");
            armL0 = CrabRig.Find(transform, "ArmL");
            armR0 = CrabRig.Find(transform, "ArmR");
            foreach (var p in pillars) if (p) p.Broken += OnPillar;
        }

        void OnPillar(CirclePillar p)
        {
            int left = pillars.FindAll(x => x && x.Alive).Count;
            Story.Hint(left > 0 ? $"Столп рухнул! Осталось: {left}" : "Щит кругов пал!");
            if (left == 0) StartCoroutine(Rise());
        }

        public void Hit(float damage, Vector3 point, Vector3 dir, GameObject by)
        {
            if (!fighting || dead) return;
            if (Shielded)
            {
                Fx.Burst(point, Gold, 10, 2, 0.06f, 0.3f);
                Sfx.Play("hit", point, 0.4f, 2.2f);
                var hero = Hero.Instance;
                if (hero && hero.State.SetFlag("lord_shield_hint"))
                    Story.AI("Его щит питают четыре столпа кругов. Ломай столпы.", 4.5f);
                return;
            }
            Fx.Burst(point, Fire, 18, 3, 0.1f, 0.5f);
            Sfx.Play("hit", point, 1, 0.5f);
            if (crownLight) crownLight.intensity = 14;
            if (health.Hurt(damage)) { StartCoroutine(Fall()); return; }
            if (!offered && Health01 < 0.3f) StartCoroutine(Offer());
        }

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
                    var b = Arena + off.normalized * (arenaRadius - 0.5f);
                    hero.Controller.Teleport(new Vector3(b.x, hero.transform.position.y, b.z), hero.transform.eulerAngles.y);
                }
                var to = Flat(hero.transform.position - transform.position);
                if (to.sqrMagnitude > 0.1f && standing) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 1 - Mathf.Exp(-1.5f * dt));
            }
            if (shield)
            {
                bool on = fighting && Shielded;
                shield.gameObject.SetActive(on);
                shield.localScale = Vector3.one * (16 + Mathf.Sin(Time.time * 3) * 0.3f);
            }
            if (body) body.localPosition = new Vector3(0, Mathf.Lerp(0, 2.5f, rise) + Mathf.Sin(Time.time * 0.6f) * 0.1f, 0);
            if (head) head.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 0.5f) * 3, Mathf.Sin(Time.time * 0.3f) * 6, 0);
            if (crownLight) crownLight.intensity = Mathf.MoveTowards(crownLight.intensity, dead ? 0 : 5 + Mathf.Sin(Time.time * 4), dt * 15);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

        Vector3 GroundAt(Vector3 p)
        {
            if (Physics.Raycast(new Vector3(p.x, Arena.y + 10, p.z), Vector3.down, out var hit, 30, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(transform))
                return hit.point;
            return new Vector3(p.x, Arena.y, p.z);
        }

        IEnumerator Fight()
        {
            fighting = true;
            var hero = Hero.Instance;
            hero.Controller.frozen = true;
            hero.Combat.locked = true;
            hero.Power.locked = true;
            Started?.Invoke();
            Sfx.Loop("music_power", 0, true);
            hero.Cam.Shot(transform.position + transform.forward * 16 + Vector3.up * 4, transform.position + Vector3.up * 7);
            Story.Title("ВЛАДЫКА АДА", "Тот, кто сидит на троне");
            yield return new WaitForSeconds(2.5f);
            Story.Think("Владыка: «Маленький краб. Ты прошёл все мои круги. Тебя прислали открыть мой трон — знаешь кто?»", 5);
            yield return new WaitForSeconds(5.5f);
            Story.Think("Не знаю. И мне всё равно. Я пришёл, потому что так решил я.", 4);
            yield return new WaitForSeconds(4);
            Sfx.Loop("music_boss", 0.8f, true);
            hero.Cam.Face(Mathf.Atan2(transform.position.x - hero.transform.position.x, transform.position.z - hero.transform.position.z) * Mathf.Rad2Deg, -14);
            hero.Cam.SetMode(CamMode.First);
            hero.Combat.firstPerson = true;
            hero.Controller.frozen = false;
            hero.Combat.locked = false;
            hero.Power.locked = false;
            Story.Task("Сломать четыре столпа кругов");
            yield return new WaitForSeconds(1.5f);
            while (!dead && hero)
            {
                if (!hero.Alive)
                {
                    yield return Reset();
                    yield break;
                }
                if (Shielded)
                {
                    if (UnityEngine.Random.value < 0.6f) yield return Meteors(5);
                    else yield return Wave();
                    yield return new WaitForSeconds(2);
                    continue;
                }
                if (!standing) { yield return null; continue; }
                int pick = UnityEngine.Random.Range(0, 4);
                if (pick == 0) yield return BeamSweep();
                else if (pick == 1) yield return Meteors(Health01 < 0.5f ? 9 : 7);
                else if (pick == 2) yield return CoinRain();
                else yield return Hands();
                if (dead) yield break;
                yield return new WaitForSeconds(Health01 < 0.5f ? 1.1f : 1.7f);
            }
        }

        IEnumerator Reset()
        {
            fighting = false;
            if (beam) beam.enabled = false;
            yield return new WaitForSeconds(2);
            health.Reset(maxHealth);
            Sfx.Loop("music_boss", 0, true);
            Sfx.Loop("music_power", 0.6f, true);
        }

        IEnumerator Rise()
        {
            Sfx.Play("roar", transform.position + Vector3.up * 8, 1, 0.45f);
            Hero.Instance?.Cam.Shake(2);
            Story.Task("Победить Владыку Ада");
            Story.Think("Владыка: «Довольно. Встаю».", 3);
            for (float t = 0; t < 2.5f; t += Time.deltaTime) { rise = Mathf.SmoothStep(0, 1, t / 2.5f); yield return null; }
            rise = 1;
            standing = true;
        }

        IEnumerator Meteors(int n)
        {
            var hero = Hero.Instance;
            if (armR0) armR0.localRotation = Quaternion.Euler(-130, 0, 0);
            Sfx.Play("roar", transform.position + Vector3.up * 8, 0.6f, 1.1f);
            yield return new WaitForSeconds(0.6f);
            for (int i = 0; i < n && hero && hero.Alive && !dead; i++)
            {
                var at = GroundAt(hero.transform.position + (i == 0 ? Vector3.zero : new Vector3(UnityEngine.Random.Range(-5f, 5f), 0, UnityEngine.Random.Range(-5f, 5f))));
                StartCoroutine(Meteor(at));
                yield return new WaitForSeconds(0.3f);
            }
            if (armR0) armR0.localRotation = Quaternion.identity;
            yield return new WaitForSeconds(1.2f);
        }

        IEnumerator Meteor(Vector3 at)
        {
            const float r = 2.0f, fall = 1.5f;
            var mark = BossFx.Marker(at, r, new Color(1f, 0.4f, 0.15f, 0.6f));
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(ball.GetComponent<Collider>());
            ball.transform.localScale = Vector3.one * 1.1f;
            var lib = Library.Current;
            if (lib && lib.fire) ball.GetComponent<Renderer>().sharedMaterial = lib.fire;
            var from = at + Vector3.up * 30 + UnityEngine.Random.insideUnitSphere * 4;
            for (float k = 0; k < fall; k += Time.deltaTime)
            {
                float f = k / fall;
                ball.transform.position = Vector3.Lerp(from, at, f * f);
                if (UnityEngine.Random.value < 0.6f) Fx.Burst(ball.transform.position, Fire, 2, 0.5f, 0.25f, 0.4f, true, -0.5f);
                BossFx.Pulse(mark, f);
                yield return null;
            }
            Destroy(mark);
            Destroy(ball);
            Sfx.Play("slam", at, 0.8f, 1.1f);
            Fx.Burst(at + Vector3.up * 0.3f, Fire, 40, 5, 0.15f, 0.8f, true, 1);
            var hero = Hero.Instance;
            if (hero && hero.Alive && Vector3.Distance(hero.transform.position, at) < r) hero.Hurt(27, hero.transform.position, hero.transform.position - at);
        }

        IEnumerator Wave()
        {
            var hero = Hero.Instance;
            if (armL0) armL0.localRotation = Quaternion.Euler(-120, 0, 0);
            yield return new WaitForSeconds(0.8f);
            if (armL0) armL0.localRotation = Quaternion.Euler(20, 0, 0);
            var at = GroundAt(transform.position + transform.forward * 5);
            Sfx.Play("slam", at, 1, 0.6f);
            hero.Cam.Shake(1.5f);
            StartCoroutine(BossFx.Ring(at, 10, arenaRadius * 1.5f, new Color(1f, 0.45f, 0.15f, 0.9f), 30));
            yield return new WaitForSeconds(0.8f);
            if (armL0) armL0.localRotation = Quaternion.identity;
        }

        /// <summary>The echo of the Guardian: a beam sweeping low across the hall — jump it.</summary>
        IEnumerator BeamSweep()
        {
            var hero = Hero.Instance;
            if (!beam)
            {
                beam = new GameObject("lord beam").AddComponent<LineRenderer>();
                beam.sharedMaterial = Library.Current ? Library.Current.tracer : null;
                beam.positionCount = 2;
                beam.startWidth = beam.endWidth = 0.5f;
                beam.startColor = beam.endColor = new Color(1f, 0.6f, 0.25f, 0.9f);
            }
            Story.Hint("Луч! Прыгай!");
            Sfx.Loop("beam", 0.7f);
            float from = -75, to = 75;
            if (UnityEngine.Random.value < 0.5f) { from = 75; to = -75; }
            bool hit = false;
            var o = new Vector3(transform.position.x, Arena.y + 0.45f, transform.position.z);
            for (float t = 0; t < 2.2f; t += Time.deltaTime)
            {
                float a = Mathf.Lerp(from, to, t / 2.2f);
                var dir = Quaternion.Euler(0, transform.eulerAngles.y + a, 0) * Vector3.forward;
                beam.enabled = true;
                beam.SetPosition(0, o + dir * 3);
                beam.SetPosition(1, o + dir * arenaRadius * 1.8f);
                if (!hit && hero && hero.Alive)
                {
                    var rel = hero.transform.position + Vector3.up * 0.25f - o;
                    float along = Vector3.Dot(rel, dir);
                    if (along > 0 && (rel - dir * along).magnitude < 0.6f && hero.transform.position.y < Arena.y + 0.5f)
                    {
                        hit = true;
                        hero.Hurt(30, hero.transform.position, dir);
                    }
                }
                yield return null;
            }
            beam.enabled = false;
            Sfx.Loop("beam", 0);
            yield return new WaitForSeconds(0.4f);
        }

        /// <summary>The echo of the Usurer: gold falling on marked spots.</summary>
        IEnumerator CoinRain()
        {
            var hero = Hero.Instance;
            for (int i = 0; i < 7 && hero && hero.Alive; i++)
            {
                var at = GroundAt(hero.transform.position + new Vector3(UnityEngine.Random.Range(-4f, 4f), 0, UnityEngine.Random.Range(-4f, 4f)));
                StartCoroutine(Coin(at));
                yield return new WaitForSeconds(0.2f);
            }
            yield return new WaitForSeconds(1.2f);
        }

        IEnumerator Coin(Vector3 at)
        {
            var mark = BossFx.Marker(at, 1.4f, new Color(1f, 0.8f, 0.2f, 0.6f));
            for (float k = 0; k < 1.2f; k += Time.deltaTime) { BossFx.Pulse(mark, k / 1.2f); yield return null; }
            Destroy(mark);
            Fx.Burst(at + Vector3.up * 0.2f, Gold, 20, 4, 0.08f, 0.6f, true, 2);
            Sfx.Play("hit", at, 0.8f, 0.6f);
            var hero = Hero.Instance;
            if (hero && hero.Alive && Flat(hero.transform.position - at).magnitude < 1.4f) hero.Hurt(19, hero.transform.position, Vector3.up);
        }

        /// <summary>The echo of the dark: hands out of the floor under the crab.</summary>
        IEnumerator Hands()
        {
            var hero = Hero.Instance;
            for (int k = 0; k < 3 && hero && hero.Alive; k++)
            {
                var at = GroundAt(hero.transform.position);
                var mark = BossFx.Marker(at, 1.5f, new Color(0.5f, 0.3f, 0.9f, 0.6f));
                for (float t = 0; t < 0.9f; t += Time.deltaTime) { BossFx.Pulse(mark, t / 0.9f); yield return null; }
                Destroy(mark);
                for (int j = 0; j < 4; j++) Fx.Burst(at + Vector3.up * j * 0.5f, new Color(0.15f, 0.1f, 0.2f), 10, 1.5f, 0.25f, 0.6f, false, -0.5f);
                Sfx.Play("slam", at, 0.6f, 0.7f);
                if (hero.Alive && Flat(hero.transform.position - at).magnitude < 1.5f) hero.Hurt(22, hero.transform.position, Vector3.up);
                yield return new WaitForSeconds(0.2f);
            }
            yield return new WaitForSeconds(0.6f);
        }

        /// <summary>Near the end: the Lord offers the crab his throne.</summary>
        IEnumerator Offer()
        {
            offered = true;
            Story.Think("Владыка: «Стой. Сядь на трон, краб. Ты заслужил. Стань мной — и весь ад твой».", 5);
            yield return new WaitForSeconds(5);
            Story.Think("Мне не нужен трон. Я краб. Я пришёл не править — закончить.", 4.5f);
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
            if (beam) beam.enabled = false;
            Sfx.Loop("beam", 0);
            Sfx.Loop("music_boss", 0, true);
            Sfx.Play("roar", transform.position + Vector3.up * 8, 1, 0.35f);
            hero.Combat.firstPerson = false;
            hero.Cam.SetMode(CamMode.Third);
            hero.Controller.faceView = false;
            for (float t = 0; t < 5; t += Time.deltaTime)
            {
                rise = Mathf.Lerp(1, 0, t / 5);
                if (body) body.localRotation = Quaternion.Euler(t * 10, 0, t * 4);
                if (UnityEngine.Random.value < Time.deltaTime * 18)
                    Fx.Burst(transform.position + Vector3.up * UnityEngine.Random.Range(1f, 12f) + UnityEngine.Random.insideUnitSphere * 3, Fire, 20, 5, 0.15f, 1.2f, true, 1);
                hero.Cam.Shake(0.6f);
                yield return null;
            }
            Fx.Flash(transform.position + Vector3.up * 6, Gold, 20, 40, 1.2f);
            Story.Flash(new Color(1f, 0.9f, 0.7f, 0.9f), 1.5f);
            Sfx.Play("slam", transform.position, 1, 0.4f);
            if (model) model.gameObject.SetActive(false);
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            fighting = false;
            Defeated?.Invoke();
        }
    }
}
