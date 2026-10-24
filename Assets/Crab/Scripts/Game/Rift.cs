// A rift: a wound in the world through which Limbo's demons crawl out. While open and the
// crab is near, it sends out shades and scavengers (a few alive at a time); the crab seals it
// by holding E, pouring the Spark into it, while the demons try to stop it. A rift can also be
// a passage: in the prologue one opens on the beach and leads into Limbo.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Crab.Game
{
    public sealed class Rift : Interactable
    {
        public string id;
        [Tooltip("The glowing membrane inside the frame")]
        public Renderer membrane;
        public Light glow;
        public float spawnEvery = 6, wakeRange = 22;
        public int maxAlive = 3, budget = 8;
        [Range(0, 1)] public float crawlerShare = 0.4f;
        [Tooltip("What comes out (instead of shades and scavengers)")]
        public GameObject[] pool;
        [Tooltip("Words for sealing (a rift in Limbo, a fire vent in the circle of Wrath)")]
        public string sealWord = "Запечатать разлом";
        public Color tint = new Color(0.5f, 1f, 0.7f);
        [Tooltip("Fire that goes out when it is sealed (a vent)")]
        public ParticleSystem fire;
        public float spawnAhead = 1.6f, pointHeight = 1.75f;
        [Tooltip("A passage to another scene instead of a source of demons")]
        public string leadsTo;
        public bool open = true;
        [Tooltip("Seconds between stepping in and the scene change (for a title card)")]
        public float delay;
        public event Action<Rift> Sealed, Entered;

        readonly List<Enemy> alive = new List<Enemy>();
        float spawnT = 2, sealT, flare;
        public float SealProgress => sealT / SealTime;
        const float SealTime = 3.5f;

        public override string Prompt => !string.IsNullOrEmpty(leadsTo)
            ? "[E] Войти в разлом"
            : $"[E] {sealWord} — держать{(sealT > 0 ? $" {Mathf.RoundToInt(SealProgress * 100)}%" : "")}";
        public override bool Usable => open;
        public override bool Hold => string.IsNullOrEmpty(leadsTo);
        public override Vector3 Point => transform.position + Vector3.up * pointHeight;

        public override void Use(Hero hero)
        {
            if (string.IsNullOrEmpty(leadsTo)) return;
            if (entered) return;
            entered = true;
            Entered?.Invoke(this);
            Sfx.Play("spark", null, 0.6f, 0.7f);
            hero.Controller.frozen = true;
            GameSession.Instance.Save();
            StartCoroutine(Leave());
        }

        bool entered;

        System.Collections.IEnumerator Leave()
        {
            for (float t = 0; t < delay; t += Time.deltaTime) yield return null;
            GameSession.Instance.Go(leadsTo, 1.2f);
        }

        public override bool Channel(Hero hero, float dt)
        {
            sealT += dt;
            flare = 1;
            if (UnityEngine.Random.value < dt * 12) Fx.Stream(hero.Core.position, transform, new Color(1f, 0.9f, 0.5f), 3);
            if (sealT < SealTime) return false;
            Seal();
            return true;
        }

        public override void StopChannel(Hero hero)
        {
            // the rift heals back quickly if the crab lets go
            if (open) sealT = Mathf.Max(0, sealT - 0.5f);
        }

        public void Seal()
        {
            if (!open) return;
            open = false;
            Sfx.Play("slam", Point, 0.8f, 1.4f);
            Sfx.Play("create", Point, 0.8f, 0.7f);
            Fx.Burst(Point, Color.Lerp(tint, Color.white, 0.2f), 80, 5, 0.15f, 1.2f);
            Fx.Flash(Point, Color.Lerp(tint, Color.white, 0.3f), 10, 14, 0.6f);
            if (fire) fire.Stop();
            Sealed?.Invoke(this);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (!open)
            {
                if (membrane && membrane.enabled)
                {
                    var t = membrane.transform;
                    t.localScale = Vector3.Lerp(t.localScale, Vector3.zero, dt * 3);
                    if (t.localScale.x < 0.02f) membrane.enabled = false;
                }
                if (glow) glow.intensity = Mathf.MoveTowards(glow.intensity, 0, dt * 4);
                return;
            }
            if (sealT > 0 && (!Hero.Instance || !Hero.Instance.Power || Hero.Instance.Power.Current != this)) sealT = Mathf.Max(0, sealT - dt * 0.4f);
            flare = Mathf.MoveTowards(flare, 0, dt * 2);
            if (glow) glow.intensity = 2.5f + Mathf.Sin(Time.time * 2.3f) * 0.6f + flare * 3;
            if (membrane) membrane.transform.localScale = Vector3.one * (1 - SealProgress * 0.6f);
            if (!string.IsNullOrEmpty(leadsTo)) return;

            alive.RemoveAll(e => !e || !e.Alive);
            var hero = Hero.Instance;
            if (!hero || !hero.Alive || budget <= 0 || alive.Count >= maxAlive) return;
            if (Vector3.Distance(hero.transform.position, transform.position) > wakeRange) return;
            spawnT -= dt;
            if (spawnT > 0) return;
            spawnT = spawnEvery * UnityEngine.Random.Range(0.8f, 1.2f);
            Spawn(UnityEngine.Random.value < crawlerShare);
        }

        public Enemy Spawn(bool crawler)
        {
            var lib = Library.Current;
            var prefab = pool != null && pool.Length > 0 ? pool[UnityEngine.Random.Range(0, pool.Length)] : lib ? (crawler ? lib.crawler : lib.shade) : null;
            if (!prefab) return null;
            var pos = transform.position + transform.forward * spawnAhead + transform.right * UnityEngine.Random.Range(-0.8f, 0.8f);
            var go = Instantiate(prefab, pos, Quaternion.LookRotation(transform.forward));
            var e = go.GetComponent<Enemy>();
            budget--;
            alive.Add(e);
            Fx.Burst(pos + Vector3.up, tint, 25, 2, 0.12f, 0.8f);
            Sfx.Play("rift", pos, 0.5f, 1.4f);
            return e;
        }
    }
}
