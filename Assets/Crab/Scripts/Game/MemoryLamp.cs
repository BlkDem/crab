// A lamp of memory: an old stone brazier in the dark. The crab lights it with the Spark; then it
// burns, holds back the despair around it, and the crab's will comes back near it. In the
// Devourer's hall the lamps take longer to light, and the Devourer puts them out.
using System;
using UnityEngine;

namespace Crab.Game
{
    public sealed class MemoryLamp : Interactable
    {
        public Light glow;
        public Transform flame;
        public LightZone zone;
        public bool lit;
        [Tooltip("Seconds of holding E to light it (0: at once)")]
        public float lightTime;
        public event Action<MemoryLamp> Lit;
        float t;

        public override string Prompt => lightTime > 0 ? $"[E] Зажечь огонь — держать{(t > 0 ? $" {Mathf.RoundToInt(t / lightTime * 100)}%" : "")}" : "[E] Зажечь огонь памяти";
        public override bool Usable => !lit;
        public override bool Hold => lightTime > 0;
        public override Vector3 Point => transform.position + Vector3.up * 1.4f;

        void Start() => Show();

        public override void Use(Hero hero)
        {
            if (lightTime <= 0) Light(true);
        }

        public override bool Channel(Hero hero, float dt)
        {
            t += dt;
            if (UnityEngine.Random.value < dt * 10) Fx.Stream(hero.Core.position, transform, new Color(1f, 0.85f, 0.5f), 2);
            if (t < lightTime) return false;
            Light(true);
            return true;
        }

        public override void StopChannel(Hero hero) { if (!lit) t = 0; }

        public void Light(bool on)
        {
            if (lit == on) return;
            lit = on;
            t = 0;
            Show();
            if (on)
            {
                Sfx.Play("beacon", Point, 0.8f, 0.8f);
                Fx.Burst(Point, new Color(1f, 0.85f, 0.5f), 40, 2.5f, 0.1f, 0.8f);
                Despair.Instance?.Restore();
                Lit?.Invoke(this);
            }
            else
            {
                Sfx.Play("glitch", Point, 0.5f, 0.6f);
                Fx.Burst(Point, new Color(0.2f, 0.15f, 0.3f), 40, 2, 0.2f, 1, false, -0.2f);
            }
        }

        void Show()
        {
            if (flame) flame.gameObject.SetActive(lit);
            if (zone) zone.on = lit;
            if (glow) glow.enabled = lit;
        }

        void Update()
        {
            if (lit && glow) glow.intensity = 4 + Mathf.PerlinNoise(Time.time * 3, transform.position.x) * 2;
            if (lit && flame) flame.localScale = Vector3.one * (1 + 0.12f * Mathf.Sin(Time.time * 9 + transform.position.z));
        }
    }
}
