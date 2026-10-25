// The Usurer King: a bloated demon on a golden throne, the boss of the third circle. The fight
// goes first person. A golden dome guards him while his hoards hold gold — the crab must draw the
// gold out of the three hoards (hold E) to bring the dome down, then shoot the gem on his chest.
// He fights back the way a usurer would: coins rain down (marked on the floor), his scales
// strike the floor and a golden wave rolls out (jump), guards come to collect, and his chain of
// debt drags the crab's own gold back into his hoards — unless a column stands between them.
using System;
using System.Collections;
using System.Collections.Generic;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    public sealed class Usurer : MonoBehaviour, IHittable, IBoss
    {
        public float maxHealth = 900;
        public float arenaRadius = 19, triggerRadius = 12;
        public Transform model, arena;
        public List<MatterSource> hoards = new List<MatterSource>();
        [Tooltip("The golden dome around him (a sphere with a collider)")]
        public Transform dome;
        public Light gemLight;
        [Tooltip("The fight may begin (the director allows it once the crab is inside the vault)")]
        public bool awake;
        public event Action Started, Defeated;

        static readonly Color Gold = new Color(1f, 0.82f, 0.25f);
        Health health;
        Transform body, head, armL0, armL1, armR0, armR1, gem;
        Vector3 center;
        bool fighting, dead, refilled;
        float domeK = 1, sway;
        Quaternion armLGoal = Quaternion.identity, armRGoal = Quaternion.identity, bodyGoal = Quaternion.identity;
        readonly List<Enemy> summoned = new List<Enemy>();
        LineRenderer chain;

        public bool Alive => !dead;
        public string Title => "КОРОЛЬ-РОСТОВЩИК";
        public bool Fighting => fighting;
        public float Health01 => health == null ? 1 : health.Value / health.Max;
        public bool Shielded => hoards.Exists(h => h && h.Left > 0);
        public Transform Gem => gem;
        Vector3 Arena => arena ? arena.position : center;

        public Vector3 GemPoint
        {
            get
            {
                var col = gem ? gem.GetComponent<Collider>() : null;
                return col ? col.bounds.center : transform.position + Vector3.up * 4;
            }
        }

        void Awake()
        {
            health = new Health(maxHealth);
            center = transform.position;
            if (!model && transform.childCount > 0) model = transform.GetChild(0);
            body = model ? CrabRig.BodyOf(model) : CrabRig.Find(transform, "Body");
            head = CrabRig.Find(transform, "Head");
            armL0 = CrabRig.Find(transform, "ArmL_0");
            armL1 = CrabRig.Find(transform, "ArmL_1");
            armR0 = CrabRig.Find(transform, "ArmR_0");
            armR1 = CrabRig.Find(transform, "ArmR_1");
            gem = CrabRig.Find(transform, "Gem");
            if (gem)
            {
                var sc = gem.gameObject.AddComponent<SphereCollider>();
                var mf = gem.GetComponent<MeshFilter>();
                if (mf && mf.sharedMesh) sc.center = mf.sharedMesh.bounds.center;
                sc.radius = 0.6f / Mathf.Max(0.01f, gem.lossyScale.x);
                gem.gameObject.AddComponent<UsurerGem>().usurer = this;
            }
            if (dome) dome.gameObject.AddComponent<UsurerDome>().usurer = this;
        }

        // ------------------------------------------------------------------ damage
        public void Hit(float damage, Vector3 point, Vector3 dir, GameObject by)
        {
            if (!fighting || dead) return;
            Fx.Burst(point, new Color(0.7f, 0.5f, 0.3f), 6, 2, 0.05f, 0.3f, false);
            if (health.Hurt(damage * 0.08f)) StartCoroutine(Fall());
        }

        public void HitGem(float damage, Vector3 point)
        {
            if (!fighting || dead) return;
            if (Shielded) { HitDome(point); return; }
            Fx.Burst(point, new Color(1f, 0.3f, 0.35f), 24, 3, 0.12f, 0.5f);
            Sfx.Play("hit", point, 1, 0.6f);
            if (gemLight) gemLight.intensity = 14;
            if (health.Hurt(damage)) StartCoroutine(Fall());
            else if (!refilled && Health01 < 0.5f) StartCoroutine(Refill());
        }

        public void HitDome(Vector3 point)
        {
            Fx.Burst(point, Gold, 8, 2.5f, 0.05f, 0.3f);
            Sfx.Play("hit", point, 0.4f, 2.4f);
            var hero = Hero.Instance;
            if (hero && hero.State.SetFlag("dome_hint"))
                Story.Hint("Купол держится на его золоте. Высоси сокровищницы (держать E)!");
        }

        /// <summary>Halfway, he refills his hoards from his own robes: the dome comes back.</summary>
        IEnumerator Refill()
        {
            refilled = true;
            Story.Hint("«Мои долги! Моё золото!» — сокровищницы снова полны");
            Sfx.Play("roar", transform.position + Vector3.up * 5, 1, 1.2f);
            foreach (var h in hoards)
            {
                if (!h) continue;
                Fx.Stream(GemPoint, h.transform, Gold, 14);
                h.Refill(6);
                yield return new WaitForSeconds(0.3f);
            }
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
            }
            domeK = Mathf.MoveTowards(domeK, !dead && Shielded ? 1 : 0, dt * 2);
            if (dome)
            {
                dome.gameObject.SetActive(domeK > 0.01f);
                dome.localScale = Vector3.one * (Mathf.Lerp(0.2f, 1, domeK) + 0.01f * Mathf.Sin(Time.time * 7));
                var col = dome.GetComponentInChildren<Collider>(true);
                if (col) col.enabled = domeK > 0.5f;
            }
            if (gemLight) gemLight.intensity = Mathf.MoveTowards(gemLight.intensity, dead ? 0 : Shielded ? 1.5f : 4 + 2 * Mathf.Sin(Time.time * 6), dt * 20);
            Pose(dt);
        }

        void Pose(float dt)
        {
            sway += dt;
            if (body) body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.Euler(Mathf.Sin(sway * 0.7f) * 2, 0, Mathf.Sin(sway * 0.4f) * 2) * bodyGoal, dt * 3);
            if (head) head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(Mathf.Sin(sway * 0.9f) * 5, Mathf.Sin(sway * 0.5f) * 12, 0), dt * 2);
            if (armL0) armL0.localRotation = Quaternion.Slerp(armL0.localRotation, armLGoal, dt * 4);
            if (armR0) armR0.localRotation = Quaternion.Slerp(armR0.localRotation, armRGoal, dt * 4);
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
            Sfx.Loop("music_greed", 0, true);
            hero.Cam.Shot(transform.position + transform.forward * 11 + Vector3.up * 3, transform.position + Vector3.up * 4.5f);
            Story.Title("КОРОЛЬ-РОСТОВЩИК", "Всё имеет цену. Особенно ты.");
            armRGoal = Raise(60);
            Sfx.Play("roar", transform.position + Vector3.up * 5, 1, 1.3f);
            yield return new WaitForSeconds(1.2f);
            Story.Think("«Маленький краб с чужой искрой. Знаешь, сколько она стоит? Я возьму её в уплату».", 4);
            yield return new WaitForSeconds(2.6f);
            armRGoal = Quaternion.identity;
            Sfx.Loop("music_boss", 0.8f, true);
            hero.Cam.Face(Mathf.Atan2(center.x - hero.transform.position.x, center.z - hero.transform.position.z) * Mathf.Rad2Deg, -12);
            hero.Cam.SetMode(CamMode.First);
            hero.Combat.firstPerson = true;
            hero.Controller.frozen = false;
            hero.Combat.locked = false;
            hero.Power.locked = false;
            if (hero.Drone) hero.Drone.Engage(gem ? gem.GetComponent<UsurerGem>() : null, gem);
            Story.Task("Победить Ростовщика: опустошить сокровищницы, стрелять в самоцвет");
            Story.Hint("Втягивай золото из сокровищниц (держать E) — купол падёт. Прячься от цепи за колоннами.");
            yield return new WaitForSeconds(1.5f);
            while (!dead && hero)
            {
                if (!hero.Alive)
                {
                    yield return Reset();
                    yield break;
                }
                int pick = UnityEngine.Random.Range(0, Health01 < 0.5f ? 4 : 3);
                if (pick == 0) yield return CoinRain();
                else if (pick == 1) yield return DebtChain();
                else if (pick == 2) yield return ScalesWave();
                else if (summoned.FindAll(e => e && e.Alive).Count < 2) yield return Summon();
                else yield return CoinRain();
                if (dead) yield break;
                yield return new WaitForSeconds(Health01 < 0.5f ? 1.4f : 2.2f);
            }
        }

        IEnumerator Reset()
        {
            fighting = false;
            if (chain) chain.enabled = false;
            foreach (var e in summoned) if (e) Destroy(e.gameObject);
            summoned.Clear();
            yield return new WaitForSeconds(2);
            health.Reset(maxHealth);
            refilled = false;
            foreach (var h in hoards) if (h) h.Refill(99);
            armLGoal = armRGoal = Quaternion.identity;
            Sfx.Loop("music_boss", 0, true);
            Sfx.Loop("music_greed", 0.6f, true);
        }

        Vector3 GroundAt(Vector3 p)
        {
            if (Physics.Raycast(new Vector3(p.x, Arena.y + 20, p.z), Vector3.down, out var hit, 40, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(transform))
                return hit.point;
            return new Vector3(p.x, Arena.y, p.z);
        }

        IEnumerator CoinRain()
        {
            var hero = Hero.Instance;
            armLGoal = Raise(110);
            Sfx.Play("roar", transform.position + Vector3.up * 5, 0.5f, 1.5f);
            yield return new WaitForSeconds(0.7f);
            int n = Health01 < 0.5f ? 9 : 6;
            for (int i = 0; i < n && hero && hero.Alive && !dead; i++)
            {
                var at = i == 0 ? hero.transform.position : hero.transform.position + new Vector3(UnityEngine.Random.Range(-4f, 4f), 0, UnityEngine.Random.Range(-4f, 4f));
                StartCoroutine(Coin(GroundAt(at)));
                yield return new WaitForSeconds(0.22f);
            }
            armLGoal = Quaternion.identity;
            yield return new WaitForSeconds(1.4f);
        }

        IEnumerator Coin(Vector3 to)
        {
            const float r = 1.5f, flight = 1.3f;
            var mark = BossFx.Marker(to, r, new Color(1f, 0.8f, 0.2f, 0.6f));
            var coin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coin.name = "coin";
            Destroy(coin.GetComponent<Collider>());
            Destroy(coin, 8);
            coin.transform.localScale = new Vector3(1.2f, 0.08f, 1.2f);
            var lib = Library.Current;
            if (lib && lib.gold) coin.GetComponent<Renderer>().sharedMaterial = lib.gold;
            var from = to + Vector3.up * 16 + UnityEngine.Random.insideUnitSphere * 2;
            var spin = UnityEngine.Random.onUnitSphere * 720;
            for (float t = 0; t < flight; t += Time.deltaTime)
            {
                float k = t / flight;
                coin.transform.position = Vector3.Lerp(from, to + Vector3.up * 0.1f, k * k);
                coin.transform.Rotate(spin * Time.deltaTime);
                BossFx.Pulse(mark, k);
                yield return null;
            }
            Destroy(mark);
            Sfx.Play("hit", to, 0.9f, 0.6f);
            Fx.Burst(to + Vector3.up * 0.2f, Gold, 24, 4, 0.08f, 0.6f, true, 2);
            Hero.Instance?.Cam.Shake(0.3f);
            var hero = Hero.Instance;
            if (hero && hero.Alive && Flat(hero.transform.position - to).magnitude < r && hero.transform.position.y - to.y < 1.5f)
                hero.Hurt(16, hero.transform.position, hero.transform.position - to);
            // the coin stays on the floor a while, then melts away
            coin.transform.rotation = Quaternion.Euler(UnityEngine.Random.Range(-8f, 8f), UnityEngine.Random.Range(0, 360f), 0);
            Destroy(coin, 4);
        }

        /// <summary>The chain of debt: a golden line from his gem to the crab; while it holds (nothing
        /// in between) it hurts and pulls the crab's gold back into his hoards.</summary>
        IEnumerator DebtChain()
        {
            var hero = Hero.Instance;
            armRGoal = Raise(80);
            if (!chain)
            {
                var go = new GameObject("debt chain");
                chain = go.AddComponent<LineRenderer>();
                chain.sharedMaterial = Library.Current ? Library.Current.tracer : null;
                chain.positionCount = 2;
            }
            Story.Hint("Цепь долга! Спрячься за колонну.");
            Sfx.Play("beam", transform.position + Vector3.up * 4, 0.6f, 0.6f);
            // first a thin warning line, then the chain bites
            float tick = 0;
            for (float t = 0; t < 4.2f && hero && hero.Alive && !dead; t += Time.deltaTime)
            {
                var from = GemPoint;
                var to = hero.transform.position + Vector3.up * 0.3f;
                bool clear = !Blocked(from, to);
                bool bite = t > 1.0f;
                chain.enabled = true;
                chain.SetPosition(0, from);
                chain.SetPosition(1, clear ? to : BlockPoint(from, to));
                float w = bite ? 0.12f + 0.04f * Mathf.Sin(Time.time * 30) : 0.03f;
                // thin at the crab's end, or it would fill the view in the first person
                chain.startWidth = w;
                chain.endWidth = w * 0.15f;
                chain.startColor = chain.endColor = bite ? new Color(1f, 0.75f, 0.2f, 0.95f) : new Color(1f, 0.75f, 0.2f, 0.4f);
                tick -= Time.deltaTime;
                if (bite && clear && tick <= 0)
                {
                    tick = 0.3f;
                    hero.Hurt(5, to, from - to);
                    Steal(hero);
                }
                yield return null;
            }
            if (chain) chain.enabled = false;
            armRGoal = Quaternion.identity;
            yield return new WaitForSeconds(0.6f);
        }

        bool Blocked(Vector3 from, Vector3 to)
        {
            foreach (var h in Physics.RaycastAll(from, (to - from).normalized, Vector3.Distance(from, to), ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.transform.IsChildOf(transform) || (dome && h.transform.IsChildOf(dome))) continue;
                if (Hero.Instance && h.transform.IsChildOf(Hero.Instance.transform)) continue;
                if (Hits.Of(h.collider) is Enemy) continue;
                return true;
            }
            return false;
        }

        Vector3 BlockPoint(Vector3 from, Vector3 to)
        {
            float best = float.MaxValue;
            var p = to;
            foreach (var h in Physics.RaycastAll(from, (to - from).normalized, Vector3.Distance(from, to), ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.transform.IsChildOf(transform) || (dome && h.transform.IsChildOf(dome))) continue;
                if (h.distance < best) { best = h.distance; p = h.point; }
            }
            return p;
        }

        void Steal(Hero hero)
        {
            var st = hero.State;
            int got = st.Matter.Remove(Matter.Gold, 1);
            if (got == 0) got = st.Matter.Remove(Matter.Metal, 1);
            if (got == 0) return;
            MatterSource best = null;
            foreach (var h in hoards)
                if (h && (best == null || h.Left < best.Left)) best = h;
            if (!best) return;
            best.Refill(got);
            Fx.Stream(hero.Core.position, best.transform, Gold, 4);
        }

        IEnumerator ScalesWave()
        {
            var hero = Hero.Instance;
            armRGoal = Raise(140);
            bodyGoal = Quaternion.Euler(-6, 0, 0);
            Sfx.Play("roar", transform.position + Vector3.up * 5, 0.7f, 1.0f);
            yield return new WaitForSeconds(1.1f);
            armRGoal = Raise(20);
            bodyGoal = Quaternion.Euler(12, 0, 0);
            yield return new WaitForSeconds(0.25f);
            var at = GroundAt(transform.position + transform.forward * 4f);
            Sfx.Play("slam", at, 1, 0.8f);
            hero.Cam.Shake(1.5f);
            Fx.Burst(at + Vector3.up * 0.3f, Gold, 60, 6, 0.15f, 1, true, 1);
            StartCoroutine(BossFx.Ring(at, 9, arenaRadius * 1.6f, new Color(1f, 0.8f, 0.25f, 0.9f), 20));
            if (Health01 < 0.5f)
            {
                yield return new WaitForSeconds(0.8f);
                StartCoroutine(BossFx.Ring(at, 11, arenaRadius * 1.6f, new Color(1f, 0.8f, 0.25f, 0.9f), 20));
            }
            armRGoal = Quaternion.identity;
            bodyGoal = Quaternion.identity;
            yield return new WaitForSeconds(1.2f);
        }

        IEnumerator Summon()
        {
            armLGoal = armRGoal = Raise(100);
            Sfx.Play("roar", transform.position + Vector3.up * 5, 0.9f, 0.9f);
            Story.Hint("«Стража! Взыскать с него!»");
            yield return new WaitForSeconds(1f);
            var lib = Library.Current;
            summoned.RemoveAll(e => !e || !e.Alive);
            for (int i = 0; i < 2; i++)
            {
                if (!lib || !lib.goldGuard) break;
                float a = UnityEngine.Random.Range(0, Mathf.PI * 2);
                var pos = GroundAt(Arena + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * UnityEngine.Random.Range(8f, arenaRadius - 2));
                var e = Instantiate(lib.goldGuard, pos, Quaternion.identity).GetComponent<Enemy>();
                e.aggro = 99;
                summoned.Add(e);
                Fx.Burst(pos + Vector3.up, Gold, 30, 3, 0.12f, 0.9f);
            }
            armLGoal = armRGoal = Quaternion.identity;
            yield return new WaitForSeconds(0.8f);
        }

        IEnumerator Fall()
        {
            dead = true;
            StopAllCoroutines();
            if (chain) chain.enabled = false;
            StartCoroutine(FallRoutine());
            yield break;
        }

        IEnumerator FallRoutine()
        {
            var hero = Hero.Instance;
            foreach (var e in summoned) if (e && e.Alive) e.Hit(999, e.transform.position, Vector3.up, gameObject);
            Sfx.Loop("music_boss", 0, true);
            Sfx.Play("roar", transform.position + Vector3.up * 5, 1, 0.6f);
            hero.Cam.Shake(2);
            hero.Combat.firstPerson = false;
            hero.Cam.SetMode(CamMode.Third);
            hero.Controller.faceView = false;
            if (hero.Drone) hero.Drone.Engage(null, null);
            // he crumbles into the gold he hoarded
            for (float t = 0; t < 4; t += Time.deltaTime)
            {
                bodyGoal = Quaternion.Euler(t * 8, 0, t * 3);
                if (model) model.localScale = Vector3.one * Mathf.Lerp(1, 0.75f, t / 4);
                if (UnityEngine.Random.value < Time.deltaTime * 12)
                    Fx.Burst(transform.position + Vector3.up * UnityEngine.Random.Range(1f, 6f) + UnityEngine.Random.insideUnitSphere * 1.5f, Gold, 20, 4, 0.1f, 1.2f, true, 2);
                yield return null;
            }
            Sfx.Play("slam", transform.position, 1, 0.5f);
            Fx.Burst(transform.position + Vector3.up * 2, Gold, 200, 7, 0.12f, 2, true, 2);
            if (model) model.gameObject.SetActive(false);
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            foreach (var l in GetComponentsInChildren<Light>()) l.enabled = false;
            fighting = false;
            Defeated?.Invoke();
        }
    }
}
