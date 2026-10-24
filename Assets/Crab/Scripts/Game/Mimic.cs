using UnityEngine;

namespace Crab.Game
{
    /// <summary>A mimic (circle of Lies): it lies still, dressed as a heap of scrap, until the crab
    /// comes close — then the heap opens a mouth. The truth lens shows it for what it is.</summary>
    public sealed class Mimic : Enemy
    {
        [Tooltip("What it pretends to be")]
        public Transform disguise;
        public float reveal = 3.2f;
        Transform body, jawL, jawR;
        bool revealed;

        public override bool Visible => revealed;
        public bool Revealed => revealed;

        protected override void Awake()
        {
            base.Awake();
            body = model ? CrabRig.BodyOf(model) : transform;
            jawL = CrabRig.Find(transform, "JawL");
            jawR = CrabRig.Find(transform, "JawR");
            if (model) model.gameObject.SetActive(false);
        }

        protected override Color HitColor => new Color(0.8f, 0.6f, 1f);

        protected override void Update()
        {
            if (!revealed)
            {
                var hero = Target;
                bool lens = TruthLens.Active;
                if (model) model.gameObject.SetActive(lens && Mathf.Repeat(Time.time * 4, 1) < 0.5f);
                if (hero && Vector3.Distance(hero.transform.position, transform.position) < reveal) Reveal();
                return;
            }
            base.Update();
        }

        public override void Hit(float dmg, Vector3 point, Vector3 dir, GameObject by)
        {
            if (!revealed) Reveal();
            base.Hit(dmg, point, dir, by);
        }

        public void Reveal()
        {
            if (revealed) return;
            revealed = true;
            hunting = true;
            if (disguise) disguise.gameObject.SetActive(false);
            if (model) model.gameObject.SetActive(true);
            Sfx.Play("roar", transform.position, 0.6f, 1.7f);
            Fx.Burst(transform.position + Vector3.up * 0.6f, new Color(0.6f, 0.55f, 0.5f), 30, 3, 0.1f, 0.6f, false, 1);
            Story.Hint("Мимик! Это была ложь.");
        }

        protected override void Animate(float dt, float spd, float w)
        {
            float open = w >= 0 ? Mathf.Sin(w * Mathf.PI) * 40 : 12 + Mathf.Sin(life * 6) * 8;
            if (jawL) jawL.localRotation = Quaternion.Euler(0, -open, 0);
            if (jawR) jawR.localRotation = Quaternion.Euler(0, open, 0);
            if (body) body.localRotation = Quaternion.Euler(-12, 0, Mathf.Sin(life * 5) * 6);
        }
    }
}
