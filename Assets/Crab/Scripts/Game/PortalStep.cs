// The portal (circle of Envy, the crab-mage): F opens a ring before the crab and another one up
// to seven metres ahead along the view, and the crab steps through. One energy a step.
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    public sealed class PortalStep : MonoBehaviour
    {
        public Hero hero;
        public float distance = 7, recharge = 2.5f;
        float cooldown;
        static readonly Color Violet = new Color(0.75f, 0.5f, 1f);

        public float Cooldown => cooldown;

        void Update()
        {
            cooldown = Mathf.Max(0, cooldown - Time.deltaTime);
            if (hero && hero.Alive && !hero.Combat.locked && !hero.Controller.frozen && Time.timeScale > 0 && Controls.Portal.WasPressedThisFrame()) Step();
        }

        /// <summary>Step through (the F key; tests call it). False if it cannot now.</summary>
        public bool Step()
        {
            if (cooldown > 0) return false;
            var st = hero.State;
            if (st.Matter[Matter.Energy] < 1)
            {
                Story.Hint("Для портала нужна энергия");
                return false;
            }
            var fwd = hero.Cam ? Quaternion.Euler(0, hero.Cam.Yaw, 0) * Vector3.forward : hero.transform.forward;
            var from = hero.transform.position;
            var cc = hero.GetComponent<CharacterController>();
            float r = cc ? cc.radius : 0.3f;
            var p1 = from + Vector3.up * (r + 0.1f);
            var p2 = from + Vector3.up * Mathf.Max(r + 0.1f, (cc ? cc.height : 0.6f) - r);
            float d = distance;
            if (Physics.CapsuleCast(p1, p2, r * 0.9f, fwd, out var hit, distance, ~0, QueryTriggerInteraction.Ignore)) d = Mathf.Max(0, hit.distance - 0.2f);
            if (d < 1) { Story.Hint("Некуда шагнуть"); return false; }
            var to = from + fwd * d;
            if (Physics.Raycast(to + Vector3.up * 3, Vector3.down, out var g, 8, ~0, QueryTriggerInteraction.Ignore)) to.y = g.point.y;
            st.Matter.Remove(Matter.Energy, 1);
            cooldown = recharge;
            Ring(from + Vector3.up * 0.4f, fwd);
            Ring(to + Vector3.up * 0.4f, fwd);
            Fx.Burst(from + Vector3.up * 0.3f, Violet, 30, 3, 0.08f, 0.5f);
            hero.Controller.Teleport(to, hero.transform.eulerAngles.y);
            Fx.Burst(to + Vector3.up * 0.3f, Violet, 30, 3, 0.08f, 0.5f);
            Fx.Flash(to + Vector3.up * 0.5f, Violet, 5, 6, 0.3f);
            Sfx.Play("spark", to, 0.6f, 1.6f);
            return true;
        }

        static void Ring(Vector3 at, Vector3 facing)
        {
            var go = new GameObject("portal");
            go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(facing));
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = Library.Current ? Library.Current.tracer : null;
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 36;
            for (int i = 0; i < 36; i++)
            {
                float a = i / 36f * Mathf.PI * 2;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * 0.55f, Mathf.Sin(a) * 0.55f, 0));
            }
            lr.startWidth = lr.endWidth = 0.07f;
            lr.startColor = lr.endColor = Violet;
            go.AddComponent<PortalFade>();
        }

        sealed class PortalFade : MonoBehaviour
        {
            float t;
            void Update()
            {
                t += Time.deltaTime;
                transform.localScale = Vector3.one * (t < 0.15f ? t / 0.15f : 1 - (t - 0.15f) / 0.5f);
                if (t > 0.65f) Destroy(gameObject);
            }
        }
    }
}
