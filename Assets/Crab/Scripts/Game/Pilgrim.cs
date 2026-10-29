// A soul of Limbo walking its path, slowly, back and forth for ever. It harms no one and doesn't
// see the crab — until the crab comes close: then it stops and turns to look at it, and goes on
// only when the crab has gone.
using UnityEngine;

namespace Crab.Game
{
    public sealed class Pilgrim : MonoBehaviour
    {
        public Vector3[] path;
        /// <summary>Where along the path it begins (m), and how fast it walks.</summary>
        public float start, speed = 0.5f;
        const float Notice = 7f;

        float s, total, seed;
        float[] at;
        Transform body;

        void Start()
        {
            at = new float[path.Length];
            for (int i = 1; i < path.Length; i++) at[i] = at[i - 1] + Vector3.Distance(path[i - 1], path[i]);
            total = Mathf.Max(0.01f, at[path.Length - 1]);
            s = start % (2 * total);
            seed = Random.value * 10;
            body = CrabRig.BodyOf(transform);
            transform.position = Point(s, out _);
        }

        /// <summary>The point at distance d along the path walked there and back; dir: the way it faces.</summary>
        Vector3 Point(float d, out Vector3 dir)
        {
            d = Mathf.Repeat(d, 2 * total);
            bool back = d > total;
            float u = back ? 2 * total - d : d;
            int i = 1;
            while (i < path.Length - 1 && at[i] < u) i++;
            float t = Mathf.InverseLerp(at[i - 1], at[i], u);
            dir = (path[i] - path[i - 1]).normalized * (back ? -1 : 1);
            return Vector3.Lerp(path[i - 1], path[i], t);
        }

        void Update()
        {
            if (path == null || path.Length < 2) return;
            float dt = Time.deltaTime;
            var hero = Hero.Instance;
            bool seen = hero && Vector3.Distance(hero.transform.position, transform.position) < Notice;
            if (!seen) s += speed * dt;
            var p = Point(s, out var dir);
            var face = seen ? hero.transform.position - transform.position : dir;
            face.y = 0;
            if (face.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), dt * (seen ? 0.8f : 2f));
            float bob = Mathf.Sin(Time.time * 1.4f + seed) * 0.05f;
            transform.position = Vector3.Lerp(transform.position, p + Vector3.up * (0.1f + bob), dt * 4);
            if (body) body.localRotation = Quaternion.Euler(seen ? -4 : 6 + Mathf.Sin(Time.time * 0.9f + seed) * 2, 0, 0);
        }
    }
}
