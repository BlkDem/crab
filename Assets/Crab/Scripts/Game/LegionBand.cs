// A band of the legion (circle of Betrayal): the first real army. When the crab comes near (or
// the story calls it), soldiers march out of the dark in ranks and come on all together.
using System.Collections.Generic;
using UnityEngine;

namespace Crab.Game
{
    public sealed class LegionBand : MonoBehaviour
    {
        public int ranks = 2, files = 3;
        public float trigger = 16, spacing = 1.6f;
        public bool auto = true;
        public readonly List<Enemy> soldiers = new List<Enemy>();
        public bool Called { get; private set; }

        public bool Beaten => Called && soldiers.TrueForAll(e => !e || !e.Alive);

        void Update()
        {
            if (Called || !auto) return;
            var hero = Hero.Instance;
            if (hero && Vector3.Distance(hero.transform.position, transform.position) < trigger) Call();
        }

        public void Call()
        {
            if (Called) return;
            Called = true;
            var lib = Library.Current;
            if (!lib || !lib.legionary) return;
            Sfx.Play("roar", transform.position + Vector3.up, 0.6f, 1.5f);
            for (int r = 0; r < ranks; r++)
                for (int f = 0; f < files; f++)
                {
                    var p = transform.position + transform.right * (f - (files - 1) / 2f) * spacing - transform.forward * r * spacing;
                    if (Physics.Raycast(p + Vector3.up * 5, Vector3.down, out var hit, 10, ~0, QueryTriggerInteraction.Ignore)) p.y = hit.point.y;
                    var e = Instantiate(lib.legionary, p, transform.rotation).GetComponent<Enemy>();
                    e.aggro = 60;
                    soldiers.Add(e);
                    Fx.Burst(p + Vector3.up, new Color(1f, 0.4f, 0.3f), 12, 2, 0.08f, 0.5f);
                }
            Story.Hint("Легион! Строем — на меня.");
        }
    }
}
