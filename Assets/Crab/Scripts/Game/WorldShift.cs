// The circle of Lies does not hold still: fragments of the crab's past circles float in the void
// and now and then trade places in a flicker.
using System.Collections.Generic;
using UnityEngine;

namespace Crab.Game
{
    public sealed class WorldShift : MonoBehaviour
    {
        public List<Transform> pieces = new List<Transform>();
        public float every = 7;
        float t = 4;

        void Update()
        {
            for (int i = 0; i < pieces.Count; i++)
                if (pieces[i]) pieces[i].position += Vector3.up * Mathf.Sin(Time.time * 0.5f + i * 1.7f) * 0.004f;
            t -= Time.deltaTime;
            if (t > 0 || pieces.Count < 2) return;
            t = every * Random.Range(0.7f, 1.3f);
            int a = Random.Range(0, pieces.Count), b = (a + 1 + Random.Range(0, pieces.Count - 1)) % pieces.Count;
            var pa = pieces[a];
            var pb = pieces[b];
            if (!pa || !pb) return;
            Fx.Burst(pa.position + Vector3.up * 2, new Color(0.7f, 0.5f, 1f), 30, 3, 0.15f, 0.7f);
            Fx.Burst(pb.position + Vector3.up * 2, new Color(0.7f, 0.5f, 1f), 30, 3, 0.15f, 0.7f);
            (pa.position, pb.position) = (pb.position, pa.position);
            (pa.rotation, pb.rotation) = (pb.rotation, pa.rotation);
            var hero = Hero.Instance;
            if (hero && (Vector3.Distance(hero.transform.position, pa.position) < 40 || Vector3.Distance(hero.transform.position, pb.position) < 40))
                Sfx.Play("glitch", null, 0.4f, 1.2f);
        }
    }
}
