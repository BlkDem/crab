// A dweller of the circle of Envy: a gentle glowing figure in a white robe with a golden halo. It
// floats, turns to the crab, talks kindly — until envy takes it, and it becomes an Envious one.
using System;
using UnityEngine;

namespace Crab.Game
{
    public sealed class Dweller : Interactable
    {
        public string title = "Житель";
        [TextArea] public string[] lines = { "Добро пожаловать." };
        public Transform model;
        public bool talked;
        public event Action<Dweller> Talked;
        Transform body, armL, armR, halo;
        int next;
        float t, yaw;
        Vector3 home;

        public override string Prompt => $"[E] Поговорить — {title}";
        public override Vector3 Point => transform.position + Vector3.up * 1.3f;

        void Awake()
        {
            if (!model && transform.childCount > 0) model = transform.GetChild(0);
            body = model ? CrabRig.BodyOf(model) : transform;
            armL = CrabRig.Find(transform, "ArmL");
            armR = CrabRig.Find(transform, "ArmR");
            halo = CrabRig.Find(transform, "GlowHalo");
            home = transform.position;
            yaw = transform.eulerAngles.y;
            t = UnityEngine.Random.Range(0, 10f);
        }

        public override void Use(Hero hero)
        {
            var line = lines[Mathf.Min(next, lines.Length - 1)];
            next++;
            Story.Think($"{title}: «{line}»", 4.5f);
            Sfx.Play("beacon", Point, 0.4f, 1.6f + UnityEngine.Random.Range(-0.1f, 0.1f));
            if (!talked)
            {
                talked = true;
                Talked?.Invoke(this);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            var hero = Hero.Instance;
            // floating, a slow drift around home; it turns to the crab when it comes close
            transform.position = home + new Vector3(Mathf.Sin(t * 0.3f) * 0.6f, 0.15f + Mathf.Sin(t * 1.4f) * 0.08f, Mathf.Cos(t * 0.23f) * 0.6f);
            float want = yaw;
            if (hero && Vector3.Distance(hero.transform.position, transform.position) < 7)
            {
                var d = hero.transform.position - transform.position;
                want = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            }
            transform.rotation = Quaternion.Euler(0, Mathf.LerpAngle(transform.eulerAngles.y, want, 1 - Mathf.Exp(-3 * dt)), 0);
            if (armL) armL.localRotation = Quaternion.Euler(-10 + Mathf.Sin(t * 1.1f) * 8, 0, 8);
            if (armR) armR.localRotation = Quaternion.Euler(-10 + Mathf.Sin(t * 1.1f + 1) * 8, 0, -8);
            if (body) body.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.8f) * 3, 0, Mathf.Sin(t * 0.6f) * 3);
            if (halo) halo.localRotation = Quaternion.Euler(0, t * 40, 0);
        }

        /// <summary>Envy takes it: the gentle figure tears apart and an Envious one stands there.</summary>
        public Enemy Turn()
        {
            var lib = Library.Current;
            var p = new Vector3(transform.position.x, home.y, transform.position.z);
            Fx.Burst(p + Vector3.up, new Color(0.5f, 1f, 0.4f), 50, 4, 0.12f, 0.9f);
            Fx.Flash(p + Vector3.up, new Color(0.5f, 1f, 0.4f), 6, 8, 0.4f);
            Sfx.Play("roar", p + Vector3.up, 0.6f, 1.8f);
            Enemy e = null;
            if (lib && lib.envious)
            {
                e = Instantiate(lib.envious, p, transform.rotation).GetComponent<Enemy>();
                e.aggro = 30;
            }
            Destroy(gameObject);
            return e;
        }
    }
}
