// A pillar of the circles (the throne hall of Power): four of them, each burning with the colour of
// the circles it draws on, hold the Lord of Hell's shield. Each can be broken.
using System;
using UnityEngine;

namespace Crab.Game
{
    public sealed class CirclePillar : MonoBehaviour, IHittable
    {
        public float maxHealth = 260;
        public Color color = Color.red;
        public Light glow;
        public event Action<CirclePillar> Broken;
        float health;

        public bool Alive => health > 0;
        public float Health01 => health / maxHealth;

        void Awake() => health = maxHealth;

        public void Hit(float damage, Vector3 point, Vector3 dir, GameObject by)
        {
            if (health <= 0) return;
            health -= damage;
            Fx.Burst(point, color, 12, 2.5f, 0.07f, 0.4f);
            if (glow) glow.intensity = 6 + 6 * Health01;
            if (health > 0) return;
            Sfx.Play("slam", transform.position + Vector3.up * 3, 1, 0.8f);
            Sfx.Play("glitch", transform.position + Vector3.up * 3, 0.6f, 0.7f);
            Fx.Burst(transform.position + Vector3.up * 3, color, 80, 6, 0.15f, 1.2f);
            Fx.Flash(transform.position + Vector3.up * 3, color, 10, 14, 0.6f);
            Hero.Instance?.Cam.Shake(1);
            if (glow) glow.enabled = false;
            foreach (var r in GetComponentsInChildren<Renderer>()) if (r.name.StartsWith("Glow")) r.enabled = false;
            transform.localScale = new Vector3(1, 0.45f, 1);
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            Broken?.Invoke(this);
        }
    }
}
