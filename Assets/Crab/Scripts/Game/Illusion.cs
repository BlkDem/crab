// A lie in the world (circle of Lies). A false bridge looks solid and gives way underfoot; a true
// one is hidden and holds. The truth lens shows each for what it is.
using UnityEngine;

namespace Crab.Game
{
    public sealed class Illusion : MonoBehaviour
    {
        public enum Kind { False, Hidden }
        public Kind kind;
        Renderer[] rends;
        Collider[] cols;
        MaterialPropertyBlock mpb;
        bool shown = true;

        void Awake()
        {
            rends = GetComponentsInChildren<Renderer>();
            cols = GetComponentsInChildren<Collider>();
            mpb = new MaterialPropertyBlock();
            // a false bridge cannot be stood on; a hidden one can
            foreach (var c in cols) c.enabled = kind == Kind.Hidden;
            Show(kind == Kind.False, Color.black);
        }

        void Update()
        {
            bool lens = TruthLens.Active;
            if (kind == Kind.False) Show(true, lens ? new Color(1.5f, 0.1f, 0.1f) : Color.black);
            else Show(lens, lens ? new Color(0.2f, 0.8f, 1.6f) : Color.black);
        }

        void Show(bool on, Color glow)
        {
            if (on != shown)
            {
                shown = on;
                foreach (var r in rends) if (r) r.enabled = on;
            }
            foreach (var r in rends)
            {
                if (!r) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_EmissionColor", glow);
                r.SetPropertyBlock(mpb);
            }
        }
    }
}
