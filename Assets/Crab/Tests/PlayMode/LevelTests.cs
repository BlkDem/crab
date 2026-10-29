// The levels made in Blender: what the crab must reach stands on the land, not up on a roof or a
// wall that happened to be over it when the scene was built. Picture: Docs/shots/play-limbo-exit.jpg.
using System.Collections;
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
