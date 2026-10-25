using UnityEngine;

namespace Crab.Game
{
    /// <summary>
    /// A sand demon: it moves under the sand (only a trail of dust shows), bursts out next to the
    /// crab, bites, stays out a few seconds — that is the time to hit it — and goes under again.
    /// A blow at the trail knocks it out of the sand at once (half the harm goes through), so it
    /// can always be fought: it is never untouchable for long.
    /// </summary>
    public sealed class Burrower : Enemy
    {
        Transform body, jawL, jawR;
        float stateT, dustT;
        bool under = true;
        float sink = 1;   // 0 out .. 1 under

        public override bool Visible => !under;

        protected override void Awake()
        {
            base.Awake();
            body = model ? CrabRig.BodyOf(model) : transform;
            jawL = CrabRig.Find(transform, "JawL");
            jawR = CrabRig.Find(transform, "JawR");
        }

        protected override Color HitColor => new Color(1f, 0.75f, 0.4f);

        protected override void BeginAttack()
        {
            if (under) return;   // it bites only once out
            base.BeginAttack();
        }

        protected override void Hunt(Hero hero, float dist, float dt)
        {
            stateT += dt;
            if (under && (dist < 3.2f || stateT > 7))   // and it cannot stay down for ever
                Surface(0.3f);
            else if (!under && stateT > 4.5f)
            {
                under = true;
                stateT = 0;
                Fx.Burst(transform.position + Vector3.up * 0.2f, new Color(0.85f, 0.6f, 0.35f), 30, 2, 0.18f, 0.7f, false, 1);
            }
        }

        protected override void Move(Vector3 horiz, float dt)
        {
            // fast under the sand, slow out of it
            base.Move(horiz * (under ? 2.2f : 0.35f), dt);
        }

        protected override void Animate(float dt, float spd, float w)
        {
            sink = Mathf.MoveTowards(sink, under ? 1 : 0, dt * 3);
            if (body) body.localPosition = new Vector3(0, -1.1f * sink, 0);
            if (model) model.gameObject.SetActive(sink < 0.98f);
            dustT -= dt;
            if (under && spd > 0.5f && dustT <= 0)
            {
                dustT = 0.12f;
                Fx.Burst(transform.position + Vector3.up * 0.05f, new Color(0.85f, 0.62f, 0.38f, 0.8f), 5, 1.2f, 0.25f, 0.6f, false, -0.2f);
            }
            float open = w >= 0 ? Mathf.Sin(w * Mathf.PI) * 35 : 8 + Mathf.Sin(life * 7) * 6;
            if (jawL) jawL.localRotation = Quaternion.Euler(0, -open, 0);
            if (jawR) jawR.localRotation = Quaternion.Euler(0, open, 0);
            if (body && !under) body.localRotation = Quaternion.Euler(-15 * (1 - sink), 0, Mathf.Sin(life * 4) * 5);
        }

        void Surface(float bite)
        {
            under = false;
            stateT = 0;
            Sfx.Play("slam", transform.position, 0.6f, 1.8f);
            Fx.Burst(transform.position + Vector3.up * 0.3f, new Color(0.85f, 0.6f, 0.35f), 50, 4, 0.2f, 0.9f, false, 1);
            attackT = bite;
        }

        public override void Hit(float dmg, Vector3 point, Vector3 dir, GameObject by)
        {
            if (under)
            {
                // struck through the sand: out it comes, stunned for a moment
                Surface(1.2f);
                base.Hit(dmg * 0.5f, point, dir, by);
                return;
            }
            base.Hit(dmg, point, dir, by);
        }
    }
}
