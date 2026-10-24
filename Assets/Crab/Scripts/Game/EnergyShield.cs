// The energy shield (circle of Greed): Q raises a dome around the crab for a few seconds; nothing
// gets through while it stands. Then it needs time to gather again.
using UnityEngine;

namespace Crab.Game
{
    public sealed class EnergyShield : MonoBehaviour
    {
        public Hero hero;
        public float duration = 4, recharge = 12;
        float activeT, cooldown;
        Transform dome;
        Light glow;

        public bool Active => activeT > 0;
        public float Cooldown => cooldown;

        void Update()
        {
            float dt = Time.deltaTime;
            cooldown = Mathf.Max(0, cooldown - dt);
            if (activeT > 0)
            {
                activeT -= dt;
                if (activeT <= 0) Drop();
            }
            if (hero && hero.Alive && !hero.Combat.locked && Time.timeScale > 0 && Controls.Shield.WasPressedThisFrame()) Raise();
            if (dome)
            {
                bool on = activeT > 0;
                float k = on ? Mathf.Clamp01((duration - activeT) * 6) * Mathf.Clamp01(activeT * 3) : 0;
                dome.localScale = Vector3.one * (2.3f * k + 0.05f * Mathf.Sin(Time.time * 20) * k);
                dome.gameObject.SetActive(k > 0.01f);
                dome.position = hero.transform.position + Vector3.up * 0.3f;
                if (glow) glow.intensity = 2 * k;
            }
        }

        /// <summary>Raise the dome (the Q key; tests call it). False while it gathers again.</summary>
        public bool Raise()
        {
            if (activeT > 0 || cooldown > 0) return false;
            if (!dome) Build();
            activeT = duration;
            cooldown = duration + recharge;
            Sfx.Play("beam", hero.transform.position, 0.5f, 1.8f);
            Fx.Flash(hero.transform.position + Vector3.up * 0.4f, new Color(0.45f, 0.9f, 1f), 4, 5, 0.3f);
            return true;
        }

        void Drop()
        {
            Sfx.Play("click", hero.transform.position, 0.8f, 0.5f);
        }

        /// <summary>A blow meets the dome: true if it stopped it.</summary>
        public bool Absorb(Vector3 point)
        {
            if (activeT <= 0) return false;
            Fx.Burst(point, new Color(0.45f, 0.9f, 1f), 12, 2, 0.06f, 0.4f);
            Sfx.Play("hit", point, 0.5f, 1.8f);
            return true;
        }

        void Build()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Shield dome";
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            var lib = Library.Current;
            if (lib && lib.shield) r.sharedMaterial = lib.shield;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dome = go.transform;
            glow = go.AddComponent<Light>();
            glow.color = new Color(0.45f, 0.9f, 1f);
            glow.range = 5;
            glow.intensity = 0;
            go.SetActive(false);
        }

        void OnDestroy()
        {
            if (dome) Destroy(dome.gameObject);
        }
    }
}
