// The bottomless dark under the islands: the crab that falls into it isn't lost — the storm throws
// it back onto the last firm ground it stood on, hurt. (Not death: the pressure is steady, but fair.)
using UnityEngine;

namespace Crab.Game
{
    public sealed class Abyss : MonoBehaviour
    {
        [Tooltip("Below this height the crab has fallen")]
        public float depth = -14;
        public float harm = 15;
        Vector3 safe;
        float safeFacing, groundedFor;
        bool known;

        void Update()
        {
            var hero = Hero.Instance;
            if (!hero || !hero.Alive) return;
            var c = hero.Controller;
            // firm ground: stood on for a moment, nothing missing under the crab's sides
            if (c.Grounded)
            {
                groundedFor += Time.deltaTime;
                var p = hero.transform.position;
                if (groundedFor > 0.4f && Footing(p))
                {
                    safe = p;
                    safeFacing = hero.transform.eulerAngles.y;
                    known = true;
                }
            }
            else groundedFor = 0;
            if (hero.transform.position.y < depth)
            {
                var back = known ? safe : hero.transform.position + Vector3.up * 30;
                c.Teleport(back + Vector3.up * 0.3f, safeFacing);
                hero.Cam.Face(safeFacing);
                Fx.Burst(back + Vector3.up * 0.5f, new Color(0.7f, 0.5f, 1f), 30, 3, 0.1f, 0.6f);
                Sfx.Play("hurt", back, 0.7f, 0.8f);
                hero.Hurt(harm, back, Vector3.zero);
                Story.Hint("Буря сбросила в бездну — и вышвырнула обратно. Держись (Ctrl) или прячься за скалами.");
            }
        }

        static bool Footing(Vector3 p)
        {
            foreach (var d in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                if (!Physics.Raycast(p + d * 1.2f + Vector3.up, Vector3.down, 2.5f, ~0, QueryTriggerInteraction.Ignore)) return false;
            return true;
        }
    }
}
