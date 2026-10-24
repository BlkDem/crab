// Anything that can be struck: creatures, the boss's heart, breakable things.
using UnityEngine;

namespace Crab.Game
{
    public interface IHittable
    {
        /// <summary>A blow of `damage` at `point` from direction `dir`; `by` is the attacker (the hero for its weapons).</summary>
        void Hit(float damage, Vector3 point, Vector3 dir, GameObject by);
        bool Alive { get; }
    }

    public static class Hits
    {
        public static IHittable Of(Collider c) => c ? c.GetComponentInParent<IHittable>() : null;
    }
}
