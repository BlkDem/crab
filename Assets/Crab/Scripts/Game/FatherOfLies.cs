// The Father of Lies: the boss of the eighth circle. A tall robed figure in a smiling golden mask
// with too many hands. The fight goes first person — and nothing in it can be trusted: he stands
// among copies of himself and every few moments they all trade places; a blow to a copy shatters
// it and feeds him; only the true one takes harm (the truth lens marks him). His markers on the
// floor are half lies (the lens shows which are real), he sends false companions, and he talks.
using System;
using System.Collections;
using System.Collections.Generic;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    public sealed class FatherOfLies : MonoBehaviour, IHittable, IBoss
    {
        public float maxHealth = 1100;
        public float arenaRadius = 18, triggerRadius = 13;
        public Transform model, arena;
        public Light truthLight;
        public bool awake;
        public event Action Started, Defeated;

        static readonly Color Violet = new Color(0.75f, 0.5f, 1f), Gold = new Color(1f, 0.8f, 0.35f);
        static readonly string[] Lies =
        {
            "Голос: «Я — это ты. Ты — это я. Бей — и попадёшь в себя».",
            "Голос: «Твой ИИ лжёт тебе. Он всегда лгал».",
            "Голос: «Искра — подарок. Я подарил её. Будь благодарен».",
            "Голос: «Ты уже победил. Можешь остановиться».",
            "Голос: «Все, кто были до тебя, тоже думали, что идут сами».",
        };
        Health health;
        bool fighting, dead;
        readonly List<GameObject> copies = new List<GameObject>();
        float rise, sway;
        Transform head, armL, armR;

        public bool Alive => !dead;
        public string Title => "ОТЕЦ ЛЖИ";
        public bool Fighting => fighting;
        public float Health01 => health == null ? 1 : health.Value / health.Max;
        public Vector3 AimPoint => transform.position + Vector3.up * 2.6f;
        public IReadOnlyList<GameObject> Copies => copies;
        Vector3 Arena => arena ? arena.position : transform.position;

        void Awake()
        {
            health = new Health(maxHealth);
            if (!model && transform.childCount > 0) model = transform.GetChild(0);
            head = CrabRig.Find(transform, "Head");
            armL = CrabRig.Find(transform, "ArmL");
            armR = CrabRig.Find(transform, "ArmR");
        }

        public void Hit(float damage, Vector3 point, Vector3 dir, GameObject by)
        {
            if (!fighting || dead) return;
            Fx.Burst(point, Gold, 18, 3, 0.08f, 0.4f);
            Sfx.Play("hit", point, 0.9f, 1.1f);
            if (truthLight) truthLight.intensity = 8;
            if (health.Hurt(damage)) StartCoroutine(Fall());
        }

        public void HitCopy(LiarCopy c, Vector3 point)
        {
            if (!fighting || dead || !c.gameObject.activeSelf) return;
            Fx.Burst(point, Violet, 30, 3, 0.08f, 0.6f);
            Sfx.Play("glitch", point, 0.6f, 1.6f);
            c.gameObject.SetActive(false);
            health.Heal(maxHealth * 0.03f);
            var hero = Hero.Instance;
            if (hero && hero.State.SetFlag("liar_copy_hint"))
                Story.AI("Это была копия — и он стал сильнее. Взгляд истины (T) покажет настоящего.", 5);
            else Story.Whisper("Голос: «Мимо. Опять мимо».");
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
                Face(transform, hero);
                foreach (var c in copies) if (c && c.activeSelf) Face(c.transform, hero);
            }
            sway += dt;
            if (model) model.localPosition = new Vector3(0, Mathf.Lerp(-5, 0, rise) + Mathf.Sin(sway * 1.2f) * 0.15f, 0);
            if (head) head.localRotation = Quaternion.Euler(Mathf.Sin(sway * 0.8f) * 8, 0, Mathf.Sin(sway * 0.6f) * 10);
            if (armL) armL.localRotation = Quaternion.Euler(-30 + Mathf.Sin(sway * 1.3f) * 20, 0, 10);
            if (armR) armR.localRotation = Quaternion.Euler(-30 + Mathf.Sin(sway * 1.3f + 1) * 20, 0, -10);
            // only the lens shows which one is true
            if (truthLight) truthLight.intensity = Mathf.MoveTowards(truthLight.intensity, fighting && TruthLens.Active ? 6 : 0, dt * 20);
        }

        static void Face(Transform t, Hero hero)
        {
            var to = Flat(hero.transform.position - t.position);
            if (to.sqrMagnitude > 0.1f) t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(to), 1 - Mathf.Exp(-3 * Time.deltaTime));
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
            Sfx.Loop("music_lies", 0, true);
            hero.Cam.Shot(transform.position + transform.forward * 8 + Vector3.up * 2, transform.position + Vector3.up * 2.5f);
            Story.Title("ОТЕЦ ЛЖИ", "Всё, что ты знаешь, — я сказал тебе");
            for (float t = 0; t < 2.5f; t += Time.deltaTime) { rise = Mathf.SmoothStep(0, 1, t / 2.5f); yield return null; }
            rise = 1;
            Story.Think("Голос: «Здравствуй, краб. Это я написал твою миссию. Не веришь? Правильно делаешь. Или нет?»", 5);
            yield return new WaitForSeconds(4);
            MakeCopies(Health01 < 0.5f ? 5 : 3);
            Shuffle(false);
            Sfx.Loop("music_boss", 0.7f, true);
            hero.Cam.Face(Mathf.Atan2(transform.position.x - hero.transform.position.x, transform.position.z - hero.transform.position.z) * Mathf.Rad2Deg, -6);
            hero.Cam.SetMode(CamMode.First);
            hero.Combat.firstPerson = true;
            hero.Controller.frozen = false;
            hero.Combat.locked = false;
            hero.Power.locked = false;
            if (hero.Drone) hero.Drone.Engage(this, transform);
            if (hero.Hunter) hero.Hunter.Engage(this, transform);
            Story.Task("Победить Отца Лжи: найти настоящего (T — взгляд истины)");
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
                if (n % 2 == 0) Shuffle(true);
                if (UnityEngine.Random.value < 0.3f) Story.Whisper(Lies[UnityEngine.Random.Range(0, Lies.Length)]);
                int pick = UnityEngine.Random.Range(0, 3);
                if (pick == 0) yield return LyingMarkers();
                else if (pick == 1) yield return FalseCompanions();
                else yield return HandsOfLies();
                if (dead) yield break;
                yield return new WaitForSeconds(Health01 < 0.5f ? 1.2f : 2f);
            }
        }

        IEnumerator Reset()
        {
            fighting = false;
            foreach (var c in copies) if (c) Destroy(c);
            copies.Clear();
            yield return new WaitForSeconds(2);
            health.Reset(maxHealth);
            Sfx.Loop("music_boss", 0, true);
            Sfx.Loop("music_lies", 0.6f, true);
        }

        void MakeCopies(int n)
        {
            foreach (var c in copies) if (c) Destroy(c);
            copies.Clear();
            for (int i = 0; i < n; i++)
            {
                var go = Instantiate(model.gameObject);
                go.name = "copy";
                foreach (var col in go.GetComponentsInChildren<Collider>()) Destroy(col);
                var root = new GameObject("liar copy");
                go.transform.SetParent(root.transform, false);
                var cap = root.AddComponent<CapsuleCollider>();
                cap.center = new Vector3(0, 2.2f, 0);
                cap.height = 4.4f;
                cap.radius = 0.8f;
                root.AddComponent<LiarCopy>().father = this;
                copies.Add(root);
            }
        }

        /// <summary>All of them trade places in a flicker.</summary>
        void Shuffle(bool show)
        {
            copies.RemoveAll(c => !c);
            int n = copies.Count + 1;
            var spots = new List<Vector3>();
            float a0 = UnityEngine.Random.Range(0, Mathf.PI * 2);
            for (int i = 0; i < n; i++)
            {
                float a = a0 + i / (float)n * Mathf.PI * 2;
                spots.Add(GroundAt(Arena + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 9));
            }
            for (int i = spots.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); (spots[i], spots[j]) = (spots[j], spots[i]); }
            transform.position = spots[0];
            for (int i = 0; i < copies.Count; i++)
            {
                copies[i].transform.position = spots[i + 1];
                copies[i].SetActive(true);
            }
            if (show)
            {
                foreach (var s in spots) Fx.Burst(s + Vector3.up * 2, Violet, 24, 3, 0.12f, 0.6f);
                Sfx.Play("glitch", Arena, 0.7f, 1.1f);
            }
        }

        Vector3 GroundAt(Vector3 p)
        {
            if (Physics.Raycast(new Vector3(p.x, Arena.y + 10, p.z), Vector3.down, out var hit, 30, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(transform))
                return hit.point;
            return new Vector3(p.x, Arena.y, p.z);
        }

        /// <summary>Marks on the floor — half of them lies. The lens shows the real ones.</summary>
        IEnumerator LyingMarkers()
        {
            var hero = Hero.Instance;
            Sfx.Play("beam", AimPoint, 0.5f, 1.5f);
            var spots = new List<(Vector3, bool)>();
            for (int i = 0; i < 8; i++)
            {
                var at = GroundAt(hero.transform.position + (i == 0 ? Vector3.zero : new Vector3(UnityEngine.Random.Range(-5f, 5f), 0, UnityEngine.Random.Range(-5f, 5f))));
                spots.Add((at, i % 2 == 0));
            }
            var marks = spots.ConvertAll(s => BossFx.Marker(s.Item1, 1.6f, new Color(1f, 0.8f, 0.3f, 0.6f)));
            for (float t = 0; t < 1.5f; t += Time.deltaTime)
            {
                for (int i = 0; i < marks.Count; i++)
                {
                    BossFx.Pulse(marks[i], t / 1.5f);
                    // through the lens the lies go grey
                    var lr = marks[i].GetComponent<LineRenderer>();
                    lr.startColor = lr.endColor = TruthLens.Active && !spots[i].Item2 ? new Color(0.5f, 0.5f, 0.5f, 0.25f) : new Color(1f, 0.8f, 0.3f, 0.7f);
                }
                yield return null;
            }
            foreach (var m in marks) Destroy(m);
            foreach (var (at, real) in spots)
            {
                if (!real) { Fx.Burst(at + Vector3.up * 0.3f, new Color(0.6f, 0.6f, 0.6f), 6, 1, 0.1f, 0.4f, false, -0.5f); continue; }
                Fx.Burst(at + Vector3.up * 0.3f, Gold, 30, 4, 0.12f, 0.6f, true, 1);
                Sfx.Play("slam", at, 0.5f, 1.5f);
                if (hero.Alive && Flat(hero.transform.position - at).magnitude < 1.6f) hero.Hurt(18, hero.transform.position, Vector3.up);
            }
            yield return new WaitForSeconds(0.6f);
        }

        /// <summary>False companions: dark copies of a drone that shoot the crab for a while.</summary>
        IEnumerator FalseCompanions()
        {
            var lib = Library.Current;
            Story.Whisper("Голос: «Смотри, твои друзья пришли. Ты ведь им веришь?»");
            for (int i = 0; i < 2 && lib && lib.drone; i++)
            {
                var go = Instantiate(lib.drone, AimPoint + Vector3.up * 2 + UnityEngine.Random.insideUnitSphere * 2, Quaternion.identity);
                go.name = "false companion";
                foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
                if (lib.mirror) foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = lib.mirror;
                var md = go.AddComponent<MirrorDrone>();
                md.life = 10;
                yield return new WaitForSeconds(0.3f);
            }
            yield return new WaitForSeconds(1);
        }

        /// <summary>His hands strike out of the floor, three at a time, around the crab.</summary>
        IEnumerator HandsOfLies()
        {
            var hero = Hero.Instance;
            for (int k = 0; k < 3 && hero && hero.Alive; k++)
            {
                var at = GroundAt(hero.transform.position);
                var mark = BossFx.Marker(at, 1.4f, new Color(0.75f, 0.5f, 1f, 0.6f));
                for (float t = 0; t < 0.9f; t += Time.deltaTime) { BossFx.Pulse(mark, t / 0.9f); yield return null; }
                Destroy(mark);
                for (int j = 0; j < 4; j++) Fx.Burst(at + Vector3.up * j * 0.5f, Violet, 8, 1.5f, 0.12f, 0.5f, true, -1);
                Sfx.Play("swing", at, 0.7f, 0.7f);
                if (hero.Alive && Flat(hero.transform.position - at).magnitude < 1.4f) hero.Hurt(15, hero.transform.position, Vector3.up);
                yield return new WaitForSeconds(0.25f);
            }
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
            foreach (var c in copies) if (c) { Fx.Burst(c.transform.position + Vector3.up * 2, Violet, 40, 3, 0.1f, 0.8f); Destroy(c); }
            copies.Clear();
            Sfx.Loop("music_boss", 0, true);
            Sfx.Play("glitch", AimPoint, 1, 0.5f);
            hero.Combat.firstPerson = false;
            hero.Cam.SetMode(CamMode.Third);
            hero.Controller.faceView = false;
            if (hero.Drone) hero.Drone.Engage(null, null);
            if (hero.Hunter) hero.Hunter.Engage(null, null);
            Story.Think("Голос: «Ты… сам? Без меня? Это… неправда…»", 3.5f);
            for (float t = 0; t < 3; t += Time.deltaTime)
            {
                rise = 1 - t / 3;
                if (UnityEngine.Random.value < Time.deltaTime * 14) Fx.Burst(AimPoint + UnityEngine.Random.insideUnitSphere * 1.5f, Gold, 12, 3, 0.08f, 0.8f);
                yield return null;
            }
            Fx.Flash(AimPoint, Gold, 14, 16, 0.8f);
            if (model) model.gameObject.SetActive(false);
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            fighting = false;
            Defeated?.Invoke();
        }
    }
}
