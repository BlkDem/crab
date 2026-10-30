// The levels made in Blender: what the crab must reach stands on the land, not up on a roof or a
// wall that happened to be over it when the scene was built. Picture: Docs/shots/play-limbo-exit.jpg.
using System.Collections;
using System.Linq;
using Crab.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Crab.Tests
{
    public sealed partial class PlayTests
    {
        static float LandUnder(Vector3 p)
        {
            var land = GameObject.Find("Ground");
            float y = float.NaN, best = float.MaxValue;
            foreach (var c in land.GetComponentsInChildren<MeshCollider>())
                if (c.Raycast(new Ray(new Vector3(p.x, 500, p.z), Vector3.down), out var hit, 1000) && hit.distance < best) { best = hit.distance; y = hit.point.y; }
            return y;
        }

        /// <summary>A model's lowest points (as the scene builder sets models down by).</summary>
        static System.Collections.Generic.List<Vector3> Feet(GameObject go)
        {
            var pts = new System.Collections.Generic.List<Vector3>();
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                var m = mf.sharedMesh;
                if (!m || mf.name.StartsWith("Glow")) continue;
                var vs = m.vertices;
                int step = Mathf.Max(1, vs.Length / 4000);
                for (int i = 0; i < vs.Length; i += step) pts.Add(mf.transform.TransformPoint(vs[i]));
            }
            if (pts.Count == 0) return pts;
            // the base in the model's own upright frame (it only turns about the vertical), so a slope
            // under it doesn't hide its downhill side; of the base, the lowest point of each half-metre cell
            float y0 = go.transform.position.y;
            float low = pts.Min(v => v.y - y0);
            float band = Mathf.Max(0.25f, (pts.Max(v => v.y) - y0 - low) * 0.06f);
            var cells = new System.Collections.Generic.Dictionary<(int, int), Vector3>();
            foreach (var v in pts)
            {
                if (v.y - y0 > low + band) continue;
                var k = (Mathf.FloorToInt(v.x * 2), Mathf.FloorToInt(v.z * 2));
                if (!cells.TryGetValue(k, out var c) || v.y < c.y) cells[k] = v;
            }
            return cells.Values.ToList();
        }

        [UnityTest]
        public IEnumerator BlenderLevelsStandOnTheLand([Values("Prologue", "Limbo")] string scene)
        {
            GameSession.Ensure().State.SetFlag("spark");
            yield return Load(scene);
            yield return Wait(0.3f);
            foreach (var t in new[] { typeof(Rift), typeof(Beacon), typeof(MatterSource) })
                foreach (MonoBehaviour m in Object.FindObjectsByType(t, FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var p = m.transform.position;
                    float land = LandUnder(p);
                    Assert.That(float.IsNaN(land), Is.False, $"{m.name}: no land under it");
                    Assert.That(Mathf.Abs(p.y - land), Is.LessThan(1.0f), $"{scene}: {m.name} at {p} stands {p.y - land:0.0} m off the land");
                }
            // nothing of the scenery hangs in the air: every model's lowest points are on the land or in it
            var floating = new System.Collections.Generic.List<string>();
            foreach (var t in GameObject.Find("Scenery").transform.Cast<Transform>())
            {
                if (t.name.StartsWith("bridge") || t.name.StartsWith("castle_far")) continue;   // set by its deck / far off
                var feet = Feet(t.gameObject);
                if (feet.Count == 0) continue;
                float hang = feet.Max(v => v.y - LandUnder(v));
                if (hang > 0.12f) floating.Add($"{t.name} {hang:0.00} m");
            }
            Assert.That(floating, Is.Empty, $"{scene}: hanging in the air: " + string.Join("; ", floating));
            if (scene == "Limbo")
            {
                var exit = Object.FindAnyObjectByType<LimboDirector>().exit;
                exit.gameObject.SetActive(true);
                var hero = Hero.Instance;
                // from the Guardian's ground, south of the gate, looking north at the way out
                hero.Controller.Teleport(exit.transform.position + Vector3.back * 9, 0);
                hero.Cam.Face(0, 8);
                yield return Wait(0.8f);
                yield return Shot("limbo-exit");
            }
        }
    }
}
