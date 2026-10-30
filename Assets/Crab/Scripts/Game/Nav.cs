// The ground the walkers know: the navigation mesh baked into each scene (SceneBuilder), and free
// places on it to stand — a demon coming out of a rift, or set down anywhere, lands where it
// fits, not inside a wall or another demon. Without a navmesh everything falls back to plain places.
using UnityEngine;
using UnityEngine.AI;

namespace Crab.Game
{
    public static class Nav
    {
        /// <summary>The nearest point of the navmesh within range.</summary>
        public static bool Snap(Vector3 p, float range, out Vector3 q)
        {
            if (NavMesh.SamplePosition(p, out var hit, range, NavMesh.AllAreas)) { q = hit.position; return true; }
            q = p;
            return false;
        }

        /// <summary>Nothing (walls, props, other creatures) in an upright capsule standing at q.</summary>
        public static bool Clear(Vector3 q, float radius, float height) =>
            !Physics.CheckCapsule(q + Vector3.up * (radius + 0.3f), q + Vector3.up * Mathf.Max(radius + 0.3f, height - radius), radius * 0.9f, ~0, QueryTriggerInteraction.Ignore);

        /// <summary>A free place to stand as near p as there is one (rings out to `search` m), on the navmesh.</summary>
        public static Vector3 FreeSpot(Vector3 p, float radius, float height, float search = 5f)
        {
            for (float r = 0; r <= search; r += 0.8f)
            {
                int n = r == 0 ? 1 : Mathf.Max(6, (int)(r * 5));
                float a0 = Random.value * 360;
                for (int i = 0; i < n; i++)
                {
                    var c = p + Quaternion.Euler(0, a0 + i * 360f / n, 0) * Vector3.forward * r;
                    if (Snap(c, 1.5f, out var q) && Clear(q, radius, height)) return q;
                }
            }
            return Snap(p, search * 2, out var any) ? any : p;
        }

        /// <summary>A random reachable point near `around`, within `radius`.</summary>
        public static Vector3 Somewhere(Vector3 around, float radius)
        {
            for (int i = 0; i < 6; i++)
            {
                var c = around + new Vector3(Random.Range(-radius, radius), 0, Random.Range(-radius, radius));
                if (Snap(c, 2f, out var q)) return q;
            }
            return around;
        }
    }
}
