// A crusher in a corridor of the labyrinth: a piston head that rises slowly and slams down. Its
// shadow on the floor warns; under it at the slam — a heavy blow.
using UnityEngine;

namespace Crab.Game
{
    public sealed class Crusher : MonoBehaviour
    {
        public Transform head;
        public float period = 3.2f, height = 4.5f, damage = 30, radius = 1.6f, offset;
        Vector3 down;
        bool slammed;

        void Awake()
        {
            if (head) down = head.localPosition;
        }

        void Update()
        {
            if (!head) return;
            float t = Mathf.Repeat(Time.time + offset, period) / period;
            // up slowly (70%), hold, then down fast
            float y = t < 0.7f ? Mathf.SmoothStep(0, 1, t / 0.7f) : t < 0.85f ? 1 : 1 - Mathf.Pow((t - 0.85f) / 0.15f, 2);
            head.localPosition = down + Vector3.up * height * y;
            if (t >= 0.985f && !slammed)
            {
                slammed = true;
                var p = head.position;
                Sfx.Play("slam", p, 0.7f, 1.2f);
                Fx.Burst(transform.position + Vector3.up * 0.2f, new Color(0.6f, 0.55f, 0.5f), 20, 3, 0.1f, 0.5f, false, 1);
                var hero = Hero.Instance;
                if (hero && hero.Alive)
                {
                    var d = hero.transform.position - transform.position;
                    d.y = 0;
                    if (d.magnitude < radius) hero.Hurt(damage, hero.transform.position, d.sqrMagnitude > 0.01f ? d : Vector3.forward);
                    else if (d.magnitude < 8) hero.Cam.Shake(0.3f);
                }
                foreach (var e in Enemy.All)
                {
                    if (!e || !e.Alive) continue;
                    var d = e.transform.position - transform.position;
                    d.y = 0;
                    if (d.magnitude < radius) e.Hit(damage, e.transform.position + Vector3.up, Vector3.down, gameObject);
                }
            }
            if (t < 0.5f) slammed = false;
        }
    }
}
