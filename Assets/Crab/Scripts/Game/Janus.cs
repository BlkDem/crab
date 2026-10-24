// The Two-Faced: the boss of the sixth circle, a mechanical idol the Guide climbs into. Its head
// has two faces: the front one smiles and lies — blows to it feed the idol (trust feeds betrayal);
// the back one is its true, demonic face, and only there do blows go in. The head turns slowly
// to whoever it watches: the crab, or for a while the TANK that strikes it — so the crab gets
// behind it by running round, by the portal, or with the squad's help. It answers with rings of
// saws (jump), pistons slamming from the floor, the legion, and its signal that turns the crab's
// own companions against it.
using System;
using System.Collections;
using System.Collections.Generic;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    public sealed class Janus : MonoBehaviour, IHittable, IBoss
    {
        public float maxHealth = 1200;
        public float arenaRadius = 18, triggerRadius = 13, turnSpeed = 45;
        public Transform model, arena;
        public Light faceLight, backLight;
        public bool awake;
        public event Action Started, Defeated;

        static readonly Color Blue = new Color(0.5f, 0.8f, 1f), Red = new Color(1f, 0.25f, 0.2f);
        Health health;
        Transform body, head, front, back, armL, armR, sawL, sawR;
        bool fighting, dead, hacked;
        float headYaw, rise, distractT;
        Transform watched;
        readonly List<Enemy> legion = new List<Enemy>();

        public bool Alive => !dead;
        public string Title => "ДВУЛИКИЙ";
        public bool Fighting => fighting;
        public float Health01 => health == null ? 1 : health.Value / health.Max;
        public Transform Back => back;
        public Transform Front => front;
        public Vector3 BackPoint => FacePoint(back);
        public Vector3 FrontPoint => FacePoint(front);
        Vector3 Arena => arena ? arena.position : transform.position;

        static Vector3 FacePoint(Transform t)
        {
            var c = t ? t.GetComponent<Collider>() : null;
            return c ? c.bounds.center : t ? t.position : Vector3.zero;
        }

        void Awake()
        {
            health = new Health(maxHealth);
            if (!model && transform.childCount > 0) model = transform.GetChild(0);
            body = model ? CrabRig.BodyOf(model) : transform;
            head = CrabRig.Find(transform, "Head");
            front = CrabRig.Find(transform, "FaceFront");
            back = CrabRig.Find(transform, "FaceBack");
            armL = CrabRig.Find(transform, "ArmL");
            armR = CrabRig.Find(transform, "ArmR");
            sawL = CrabRig.Find(transform, "SawL");
            sawR = CrabRig.Find(transform, "SawR");
            foreach (var (f, isBack) in new[] { (front, false), (back, true) })
            {
                if (!f) continue;
                var sc = f.gameObject.AddComponent<SphereCollider>();
                var mf = f.GetComponent<MeshFilter>();
                if (mf && mf.sharedMesh) sc.center = mf.sharedMesh.bounds.center;
                sc.radius = 1.1f / Mathf.Max(0.01f, f.lossyScale.x);
                var jf = f.gameObject.AddComponent<JanusFace>();
                jf.janus = this;
                jf.back = isBack;
            }
            headYaw = transform.eulerAngles.y;
        }

        // ------------------------------------------------------------------ damage
        public void Hit(float damage, Vector3 point, Vector3 dir, GameObject by)
        {
            if (!fighting || dead) return;
            Fx.Burst(point, new Color(0.7f, 0.65f, 0.6f), 6, 2, 0.05f, 0.3f, false);
            Distract(by, point - dir * 6);
            if (health.Hurt(damage * 0.08f)) StartCoroutine(Fall());
        }

        public void HitFace(bool isBack, float damage, Vector3 point, Vector3 dir, GameObject by)
        {
            if (!fighting || dead) return;
            if (!isBack)
            {
                // the smiling face drinks the blow
                Fx.Burst(point, Blue, 14, 2.5f, 0.07f, 0.4f);
                health.Heal(damage * 0.3f);
                var hero = Hero.Instance;
                if (hero && hero.State.SetFlag("janus_hint"))
                    Story.AI("Удары в улыбку его лечат. Настоящее лицо — на затылке. Зайди сзади или пусть TANK отвлечёт.", 5.5f);
                return;
            }
            Fx.Burst(point, Red, 24, 3, 0.1f, 0.5f);
            Sfx.Play("hit", point, 1, 0.6f);
            if (backLight) backLight.intensity = 12;
            if (health.Hurt(damage)) StartCoroutine(Fall());
        }

        /// <summary>A companion struck it: for a while it watches that one.</summary>
        void Distract(GameObject by, Vector3 from)
        {
            var hero = Hero.Instance;
            if (!hero || !hero.Tank || !hero.Tank.Alive) return;
            if (Vector3.Distance(hero.Tank.transform.position, from) < 5)
            {
                watched = hero.Tank.transform;
                distractT = 3.5f;
            }
        }

        public void Provoked(TankBot tank)
        {
            watched = tank.transform;
            distractT = 3.5f;
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
                    var b = Arena + off.normalized * (arenaRadius - 0.5f);
                    hero.Controller.Teleport(new Vector3(b.x, hero.transform.position.y, b.z), hero.transform.eulerAngles.y);
                }
                distractT -= dt;
                var look = distractT > 0 && watched ? watched : hero.transform;
                var to = Flat(look.position - transform.position);
                if (to.sqrMagnitude > 0.1f)
                    headYaw = Mathf.MoveTowardsAngle(headYaw, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, turnSpeed * (Health01 < 0.5f ? 1.3f : 1) * dt);
            }
            if (head) head.rotation = Quaternion.Euler(0, headYaw, 0) * Quaternion.Euler(head.localEulerAngles.x, 0, 0);
            if (body) body.localPosition = new Vector3(0, Mathf.Lerp(-6, 0, rise), 0);
            float spin = fighting ? 600 : 60;
            if (sawL) sawL.Rotate(0, 0, spin * dt, Space.Self);
            if (sawR) sawR.Rotate(0, 0, -spin * dt, Space.Self);
            if (faceLight) faceLight.intensity = dead ? 0 : 3 + Mathf.Sin(Time.time * 2) * 0.5f;
            if (backLight) backLight.intensity = Mathf.MoveTowards(backLight.intensity, dead ? 0 : 3, dt * 20);
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
            Sfx.Loop("music_betrayal", 0, true);
            hero.Cam.Shot(Arena + (hero.transform.position - Arena).normalized * 4 + Vector3.up * 2, transform.position + Vector3.up * 6);
            Story.Title("ДВУЛИКИЙ", "У предательства всегда два лица");
            for (float t = 0; t < 3; t += Time.deltaTime)
            {
                rise = Mathf.SmoothStep(0, 1, t / 3);
                if (UnityEngine.Random.value < Time.deltaTime * 12) Fx.Burst(transform.position + UnityEngine.Random.insideUnitSphere * 3 + Vector3.up, new Color(0.6f, 0.55f, 0.5f), 8, 3, 0.15f, 0.8f, false, 1);
                yield return null;
            }
            Sfx.Play("roar", transform.position + Vector3.up * 6, 1, 0.9f);
            hero.Cam.Shake(1.2f);
            yield return new WaitForSeconds(1);
            Sfx.Loop("music_boss", 0.8f, true);
            hero.Cam.Face(Mathf.Atan2(transform.position.x - hero.transform.position.x, transform.position.z - hero.transform.position.z) * Mathf.Rad2Deg, -14);
            hero.Cam.SetMode(CamMode.First);
            hero.Combat.firstPerson = true;
            hero.Controller.frozen = false;
            hero.Combat.locked = false;
            hero.Power.locked = false;
            var backFace = back ? back.GetComponent<JanusFace>() : null;
            if (hero.Drone) hero.Drone.Engage(backFace, back);
            if (hero.Hunter) hero.Hunter.Engage(backFace, back);
            if (hero.Tank) hero.Tank.Engage(this, transform);
            Story.Task("Победить Двуликого: бить в лицо на затылке");
            Story.Hint("Улыбка лжёт — удары в неё его лечат. Обходи его (F — портал) или пусть TANK отвлечёт.");
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
                if (!hacked && Health01 < 0.6f) yield return HackSquad();
                else if (n % 4 == 0 && legion.FindAll(e => e && e.Alive).Count < 3) yield return CallLegion();
                else if (UnityEngine.Random.value < 0.5f) yield return SawRing();
                else yield return Pistons();
                if (dead) yield break;
                yield return new WaitForSeconds(Health01 < 0.5f ? 1f : 1.6f);
            }
        }

        IEnumerator Reset()
        {
            fighting = false;
            Squad.Release();
            foreach (var e in legion) if (e) Destroy(e.gameObject);
            legion.Clear();
            yield return new WaitForSeconds(2);
            health.Reset(maxHealth);
            hacked = false;
            Sfx.Loop("music_boss", 0, true);
            Sfx.Loop("music_betrayal", 0.6f, true);
        }

        Vector3 GroundAt(Vector3 p)
        {
            if (Physics.Raycast(new Vector3(p.x, Arena.y + 10, p.z), Vector3.down, out var hit, 30, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(transform))
                return hit.point;
            return new Vector3(p.x, Arena.y, p.z);
        }

        IEnumerator SawRing()
        {
            var hero = Hero.Instance;
            Sfx.Play("swing", transform.position + Vector3.up * 4, 1, 0.5f);
            if (armL) armL.localRotation = Quaternion.Euler(-60, 0, 0);
            if (armR) armR.localRotation = Quaternion.Euler(-60, 0, 0);
            yield return new WaitForSeconds(0.9f);
            if (armL) armL.localRotation = Quaternion.Euler(20, 0, 0);
            if (armR) armR.localRotation = Quaternion.Euler(20, 0, 0);
            var at = GroundAt(transform.position);
            Sfx.Play("slam", at, 1, 0.9f);
            hero.Cam.Shake(1.2f);
            StartCoroutine(BossFx.Ring(at, 10, arenaRadius * 1.5f, new Color(1f, 0.6f, 0.3f, 0.9f), 20));
            if (Health01 < 0.5f)
            {
                yield return new WaitForSeconds(0.7f);
                StartCoroutine(BossFx.Ring(at, 12, arenaRadius * 1.5f, new Color(1f, 0.6f, 0.3f, 0.9f), 20));
            }
            yield return new WaitForSeconds(0.8f);
            if (armL) armL.localRotation = Quaternion.identity;
            if (armR) armR.localRotation = Quaternion.identity;
        }

        IEnumerator Pistons()
        {
            var hero = Hero.Instance;
            Sfx.Play("roar", transform.position + Vector3.up * 5, 0.5f, 1.4f);
            var spots = new List<Vector3> { GroundAt(hero.transform.position) };
            for (int i = 0; i < 4; i++)
            {
                float a = UnityEngine.Random.Range(0, Mathf.PI * 2);
                spots.Add(GroundAt(hero.transform.position + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * UnityEngine.Random.Range(2.5f, 6f)));
            }
            var marks = spots.ConvertAll(s => BossFx.Marker(s, 1.4f, new Color(1f, 0.5f, 0.2f, 0.6f)));
            for (float t = 0; t < 1.2f; t += Time.deltaTime) { foreach (var m in marks) BossFx.Pulse(m, t / 1.2f); yield return null; }
            foreach (var m in marks) Destroy(m);
            foreach (var s in spots)
            {
                Fx.Burst(s + Vector3.up * 0.5f, new Color(0.6f, 0.55f, 0.5f), 18, 4, 0.1f, 0.5f, false, 1);
                for (int k = 0; k < 4; k++) Fx.Burst(s + Vector3.up * k * 0.7f, new Color(0.8f, 0.6f, 0.4f), 4, 0.5f, 0.3f, 0.4f, false, 0);
                Sfx.Play("slam", s, 0.5f, 1.5f);
                if (hero.Alive && Flat(hero.transform.position - s).magnitude < 1.4f) hero.Hurt(22, hero.transform.position, Vector3.up);
            }
            hero.Cam.Shake(0.8f);
            yield return new WaitForSeconds(0.8f);
        }

        IEnumerator CallLegion()
        {
            var lib = Library.Current;
            Story.Hint("«Легион, ко мне!»");
            Sfx.Play("roar", transform.position + Vector3.up * 5, 0.8f, 1.2f);
            legion.RemoveAll(e => !e || !e.Alive);
            for (int i = 0; i < 4 && lib && lib.legionary; i++)
            {
                float a = UnityEngine.Random.Range(0, Mathf.PI * 2);
                var p = GroundAt(Arena + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (arenaRadius - 2));
                var e = Instantiate(lib.legionary, p, Quaternion.LookRotation(Flat(Arena - p))).GetComponent<Enemy>();
                e.aggro = 99;
                legion.Add(e);
                Fx.Burst(p + Vector3.up, Red, 16, 2, 0.08f, 0.5f);
                yield return new WaitForSeconds(0.25f);
            }
            yield return new WaitForSeconds(0.6f);
        }

        /// <summary>Its signal takes over the crab's own companions for a while.</summary>
        IEnumerator HackSquad()
        {
            hacked = true;
            var hero = Hero.Instance;
            Sfx.Play("glitch", transform.position + Vector3.up * 5, 1, 0.8f);
            Story.Flash(new Color(1f, 0.2f, 0.25f, 0.5f), 0.8f);
            Story.Think("Проводник: «Твои игрушки слушают и меня, краб. Все слушают того, кто сильнее».", 4);
            Squad.Hack(9);
            yield return new WaitForSeconds(1.5f);
            Story.AI("Взлом! Сигнал Двуликого в каналах отряда. Перехватываю управление…", 4);
            yield return new WaitForSeconds(7.5f);
            Squad.Release();
            Story.AI("Управление восстановлено. Они снова твои. Я — тоже.", 4);
            yield return new WaitForSeconds(0.5f);
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
            Squad.Release();
            foreach (var e in legion) if (e && e.Alive) e.Hit(999, e.transform.position, Vector3.up, gameObject);
            Sfx.Loop("music_boss", 0, true);
            Sfx.Play("roar", transform.position + Vector3.up * 6, 1, 0.6f);
            hero.Combat.firstPerson = false;
            hero.Cam.SetMode(CamMode.Third);
            hero.Controller.faceView = false;
            if (hero.Drone) hero.Drone.Engage(null, null);
            if (hero.Hunter) hero.Hunter.Engage(null, null);
            if (hero.Tank) hero.Tank.Engage(null, null);
            // the head splits: the two faces fall apart
            for (float t = 0; t < 3.5f; t += Time.deltaTime)
            {
                if (front) front.position += (front.position - (head ? head.position : transform.position)).normalized * Time.deltaTime * 0.8f + Vector3.down * Time.deltaTime * t;
                if (back) back.position += (back.position - (head ? head.position : transform.position)).normalized * Time.deltaTime * 0.8f + Vector3.down * Time.deltaTime * t;
                if (UnityEngine.Random.value < Time.deltaTime * 14)
                    Fx.Burst(transform.position + Vector3.up * UnityEngine.Random.Range(1f, 8f) + UnityEngine.Random.insideUnitSphere * 2, new Color(1f, 0.6f, 0.3f), 12, 3, 0.08f, 0.8f, true, 1);
                yield return null;
            }
            Sfx.Play("slam", transform.position, 1, 0.6f);
            Fx.Burst(transform.position + Vector3.up * 3, new Color(0.6f, 0.55f, 0.5f), 120, 6, 0.3f, 2, false, 0.5f);
            if (model) model.gameObject.SetActive(false);
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            fighting = false;
            Defeated?.Invoke();
        }
    }
}
