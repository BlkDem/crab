// The map of a scene: one picture from above, taken when the scene starts (roofs, the sky and the
// fog are put aside for it, the light is evened out), and the marks drawn over it by the interface:
// the crab, where the task leads, the ways out, the rifts still open, the beacons.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Crab.Game
{
    public static class WorldMap
    {
        public enum Mark { Goal, Exit, Rift, Beacon, Boss }

        /// <summary>The picture, and the part of the world it covers (x, z of the corner, size).</summary>
        public static RenderTexture Picture { get; private set; }
        public static Rect Area { get; private set; }
        public static bool Ready => Picture;

        /// <summary>World (x, z) to 0..1 across the picture (y down, as in the interface).</summary>
        public static Vector2 ToMap(Vector3 p) => new Vector2((p.x - Area.x) / Area.width, 1 - (p.z - Area.y) / Area.height);

        public static void Clear()
        {
            if (Picture) { Picture.Release(); Object.Destroy(Picture); }
            Picture = null;
        }

        /// <summary>Takes the picture of the scene now loaded. Without a ground there is no map.</summary>
        public static void Take(Vector3 hero)
        {
            Clear();
            var grounds = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                .Where(r => r.name.EndsWith("Ground") || r.name.EndsWith("Floor")).ToList();
            if (grounds.Count == 0) return;
            var b = grounds[0].bounds;
            foreach (var g in grounds) b.Encapsulate(g.bounds);
            float size = Mathf.Max(b.size.x, b.size.z) + 10;
            Area = new Rect(b.center.x - size / 2, b.center.z - size / 2, size, size);
            float floor = Mathf.Max(b.max.y, hero.y);

            // what would hide the ground from above: roofs, ceilings, fortresses in the sky
            var hidden = new List<Renderer>();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r.enabled && (r.bounds.min.y > floor + 9 || r is ParticleSystemRenderer)) { r.enabled = false; hidden.Add(r); }
            bool fog = RenderSettings.fog;
            var ambMode = RenderSettings.ambientMode;
            var amb = RenderSettings.ambientLight;
            var sky = RenderSettings.skybox;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.75f, 0.75f, 0.75f);
            RenderSettings.skybox = null;

            var go = new GameObject("MapCamera");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = size / 2;
            cam.transform.SetPositionAndRotation(new Vector3(b.center.x, floor + 8, b.center.z), Quaternion.Euler(90, 0, 0));
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.04f, 0.05f);
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            Picture = new RenderTexture(1024, 1024, 24) { name = "Map" };
            cam.targetTexture = Picture;
            cam.Render();
            cam.targetTexture = null;
            Object.Destroy(go);

            RenderSettings.fog = fog;
            RenderSettings.ambientMode = ambMode;
            RenderSettings.ambientLight = amb;
            RenderSettings.skybox = sky;
            foreach (var r in hidden) if (r) r.enabled = true;
        }

        /// <summary>The marks to show now: where the task leads, the ways out, the open rifts, the beacons.</summary>
        public static IEnumerable<(Mark kind, Vector3 at)> Marks()
        {
            var goal = Story.Goal;
            if (goal && goal.gameObject.activeInHierarchy) yield return (Mark.Goal, goal.position);
            foreach (var r in Object.FindObjectsByType<Rift>(FindObjectsSortMode.None))
            {
                if (goal && r.transform == goal) continue;
                if (!string.IsNullOrEmpty(r.leadsTo)) yield return (Mark.Exit, r.transform.position);
                else if (r.open) yield return (Mark.Rift, r.transform.position);
            }
            foreach (var b in Object.FindObjectsByType<Beacon>(FindObjectsSortMode.None)) yield return (Mark.Beacon, b.transform.position);
        }
    }
}
