// The Lord of Matter (circle of Power, level 9): everything the crab has made, in the crab itself —
// the plates of a battle robot, wings of light, the AI's rings round it — and the same small face.
// It glides on its wings (hold jump in the air) and R raises a storm of matter that strikes every
// demon around.
using UnityEngine;

namespace Crab.Game
{
    public sealed class MatterLord : MonoBehaviour
    {
        public Hero hero;
        public float radius = 16, damage = 120, recharge = 18;
        float cooldown;
        Transform wings, halo;

        public float Cooldown => cooldown;

        void Start()
        {
            wings = CrabRig.Find(transform, "GlowWings");
            halo = CrabRig.Find(transform, "Halo");
        }

        void Update()
        {
            float dt = Time.deltaTime;
            cooldown = Mathf.Max(0, cooldown - dt);
            if (!hero || !hero.Alive) return;
            // the wings beat slowly; the AI's rings turn
            if (wings) wings.localScale = new Vector3(1 + 0.08f * Mathf.Sin(Time.time * 2.5f), 1, 1);
            if (halo) halo.localRotation = Quaternion.Euler(0, Time.time * 60, 0);
            if (!hero.Combat.locked && Time.timeScale > 0 && Controls.Storm.WasPressedThisFrame()) Storm();
        }

        /// <summary>The storm of matter (R; tests call it).</summary>
        public bool Storm()
        {
            if (cooldown > 0) return false;
            cooldown = recharge;
            var at = hero.transform.position + Vector3.up * 0.5f;
            Sfx.Play("spark", at, 1, 0.5f);
            Sfx.Play("slam", at, 1, 0.6f);
            Fx.Flash(at, new Color(1f, 0.9f, 0.6f), 14, radius * 1.5f, 0.6f);
            for (int i = 0; i < 6; i++) Fx.Burst(at + Random.insideUnitSphere * 2, i % 2 == 0 ? new Color(1f, 0.85f, 0.45f) : new Color(0.6f, 0.85f, 1f), 40, 8, 0.12f, 1.2f);
            hero.Cam.Shake(1.2f);
            foreach (var e in Enemy.All.ToArray())
            {
                if (!e || !e.Alive || Vector3.Distance(e.transform.position, at) > radius) continue;
                Fx.Tracer(at, e.transform.position + Vector3.up, new Color(1f, 0.9f, 0.6f), 0.06f, 0.25f);
                Hits.Energy = true;
                e.Hit(damage, e.transform.position + Vector3.up, (e.transform.position - at).normalized, hero.gameObject);
                Hits.Energy = false;
                e.Push((e.transform.position - at).normalized * 10 + Vector3.up * 6);
            }
            return true;
        }
    }
}
