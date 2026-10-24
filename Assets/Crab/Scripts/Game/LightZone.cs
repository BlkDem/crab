// A place of light in the dark (the circle of Despondency): a lit lamp of memory, a patch of
// glowing mushrooms. In it the crab's will comes back; out of all of them it drains.
using System.Collections.Generic;
using UnityEngine;

namespace Crab.Game
{
    public sealed class LightZone : MonoBehaviour
    {
        public static readonly List<LightZone> All = new List<LightZone>();
        public float radius = 8;
        public bool on = true;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static bool Lit(Vector3 p)
        {
            foreach (var z in All)
                if (z && z.on && (z.transform.position - p).sqrMagnitude < z.radius * z.radius) return true;
            return false;
        }
    }
}
