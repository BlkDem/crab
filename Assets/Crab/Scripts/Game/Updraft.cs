// A column of rising air between the islands of the second circle: whatever walks into it is lifted
// up to its top; holding jump there, the crab then glides on the storm to where the wind carries it.
using UnityEngine;

namespace Crab.Game
{
    public sealed class Updraft : MonoBehaviour
    {
        [Tooltip("Radius and height of the column (from its foot)")]
        public float radius = 2.2f, height = 14;
        public float lift = 7;

        void Start()
        {
            var lib = Library.Current;
            var ps = gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = height / lift;
            main.startSpeed = lift;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.25f);
            main.startColor = new Color(0.75f, 0.6f, 1f, 0.5f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 60;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = radius;
            sh.rotation = new Vector3(-90, 0, 0);
            var r = GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.lengthScale = 3;
            if (lib) r.sharedMaterial = lib.particleAdditive;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            var l = new GameObject("Glow").AddComponent<Light>();
            l.transform.SetParent(transform, false);
            l.transform.localPosition = Vector3.up * 2;
            l.color = new Color(0.7f, 0.5f, 1f);
            l.range = 8;
            l.intensity = 1.5f;
        }

        /// <summary>The lift at a place (0 outside the column; less toward its top).</summary>
        public float Lift(Vector3 p)
        {
            var d = p - transform.position;
            if (d.y < -1 || d.y > height || new Vector2(d.x, d.z).magnitude > radius) return 0;
            return lift * Mathf.Clamp01((height - d.y) / 3f);
        }

        public static float At(Vector3 p)
        {
            float best = 0;
            foreach (var u in FindObjectsByType<Updraft>(FindObjectsSortMode.None)) best = Mathf.Max(best, u.Lift(p));
            return best;
        }
    }
}
