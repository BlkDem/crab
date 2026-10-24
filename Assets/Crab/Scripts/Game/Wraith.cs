using UnityEngine;

namespace Crab.Game
{
    /// <summary>A Wraith of despondency (Тоска): a hunched shape of smoke with long arms and pale
    /// eyes. In the dark it cannot be seen — only in light, close to the crab, or through the
    /// cyber-frame's scanner. Its touch wounds little but drains the will and whispers.</summary>
    public sealed class Wraith : Enemy
    {
        Transform body, head, armL, armR;
        Renderer[] rends;
        float phase, shown = 1;

        protected override void Awake()
        {
            base.Awake();
            body = model ? CrabRig.BodyOf(model) : transform;
            head = CrabRig.Find(transform, "Head");
            armL = CrabRig.Find(transform, "ArmL");
            armR = CrabRig.Find(transform, "ArmR");
            rends = GetComponentsInChildren<Renderer>();
        }

        protected override Color HitColor => new Color(0.6f, 0.7f, 1f);

        /// <summary>Seen: in light, near the crab, or scanned.</summary>
        public override bool Visible
        {
            get
            {
                var hero = Target;
                if (!hero) return true;
                float d = Vector3.Distance(hero.transform.position, transform.position);
                return d < 5 || LightZone.Lit(transform.position) || (hero.State.Items.Has(Crab.Core.ItemId.CyberFrame) && d < 18);
            }
        }

        protected override void Strike(Hero hero, float dist)
        {
            if (Provoked || !hero || dist > attackRange * 1.25f) { base.Strike(hero, dist); return; }
            hero.Hurt(damage, hero.transform.position + Vector3.up * 0.3f, hero.transform.position - transform.position);
            Despair.Instance?.Lose(14);
            Sfx.Play("shade", transform.position, 0.7f, 0.6f);
        }

        protected override void Die()
        {
            base.Die();
            Despair.Instance?.Gain(8);   // one less voice in the dark
        }

        protected override void Update()
        {
            base.Update();
            shown = Mathf.MoveTowards(shown, Visible && Alive ? 1 : 0, Time.deltaTime * 2);
            bool on = shown > 0.05f;
            foreach (var r in rends) if (r && r.enabled != on) r.enabled = on;
            if (model && Alive) model.localScale = Vector3.one * Mathf.Lerp(0.85f, 1, shown);
        }

        protected override void Animate(float dt, float spd, float w)
        {
            phase += dt * (1.5f + spd);
            float reach = w >= 0 ? Mathf.Sin(w * Mathf.PI) * 80 : 20 + Mathf.Sin(phase) * 10;
            if (armL) armL.localRotation = Quaternion.Euler(-reach, 0, 10);
            if (armR) armR.localRotation = Quaternion.Euler(-reach, 0, -10);
            if (body) body.localRotation = Quaternion.Euler(20 + Mathf.Sin(phase * 0.7f) * 5, 0, Mathf.Sin(phase) * 6);
            if (head) head.localRotation = Quaternion.Euler(Mathf.Sin(life * 2) * 8, Mathf.Sin(life * 0.9f) * 20, 0);
        }
    }
}
