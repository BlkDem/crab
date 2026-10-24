// Lava: one surface at a fixed height over the whole scene; the ground dips below it in the rivers
// and the lake. A crab that sinks below the surface burns and is thrown back out the way it came.
using UnityEngine;

namespace Crab.Game
{
    public sealed class LavaZone : MonoBehaviour
    {
        [Tooltip("Height of the lava surface (world)")]
        public float level = -0.3f;
        public float damage = 12, every = 0.45f;
        float t;
        bool hinted;
        Vector3 lastSafe;

        void Update()
        {
            var hero = Hero.Instance;
            if (!hero || !hero.Alive) return;
            var pos = hero.transform.position;
            bool inside = pos.y < level - 0.05f;
            if (!inside)
            {
                if (hero.Controller.Grounded) lastSafe = pos;
                return;
            }
            t -= Time.deltaTime;
            if (t > 0) return;
            t = every;
            // back toward the last firm ground, and up
            var away = lastSafe - pos;
            away.y = 0;
            away = away.sqrMagnitude > 0.01f ? away.normalized : -hero.transform.forward;
            hero.Hurt(damage, pos, away);
            hero.Controller.Knock(away * 6 + Vector3.up * 7);
            Fx.Burst(pos + Vector3.up * 0.2f, new Color(1f, 0.55f, 0.15f), 24, 3, 0.1f, 0.6f, true, -0.5f);
            Sfx.Play("hurt", pos, 0.5f, 0.6f);
            if (!hinted)
            {
                hinted = true;
                Story.Hint("Лава! Обходи её — или перепрыгивай узкие места (Пробел).");
            }
        }
    }
}
