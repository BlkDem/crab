// Pictures of the built scenes from the game camera and a few overviews (Docs/shots), to check
// the look without playing: -executeMethod Crab.EditorTools.Snapshot.All
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Crab.EditorTools
{
    public static class Snapshot
    {
        const string Out = "Docs/shots";

        public static void All()
        {
            EditorSettings.asyncShaderCompilation = false;
            Directory.CreateDirectory(Out);
            Scene("Prologue", new[] { ("start", (Vector3?)null, Vector3.zero), ("beach", new Vector3(-12, 5, -8), new Vector3(2, 1, 8)), ("shelter", new Vector3(5, 1.6f, 0.5f), new Vector3(8, 0.6f, 4.5f)) });
            Scene("Limbo", new[] { ("start", (Vector3?)null, Vector3.zero), ("street", new Vector3(0, 3, -16), new Vector3(0, 2, 10)), ("town", new Vector3(-30, 25, -30), new Vector3(0, 0, 25)),
                ("rift", new Vector3(-24, 2.5f, 20), new Vector3(-30, 1.8f, 16)), ("square", new Vector3(0, 3, 56), new Vector3(0, 4, 74)) });
            Scene("Wrath", new[] { ("start", (Vector3?)null, Vector3.zero), ("river", new Vector3(-6, 4, -26), new Vector3(4, 0, -8)), ("town", new Vector3(-30, 22, -14), new Vector3(0, 0, 22)),
                ("vent", new Vector3(-34, 3, 14), new Vector3(-40, 1, 22)), ("lake", new Vector3(0, 5, 58), new Vector3(0, 7, 106)), ("sky", new Vector3(40, 50, -60), new Vector3(0, 0, 40)) });
            Scene("Greed", new[] { ("start", (Vector3?)null, Vector3.zero), ("gate", new Vector3(-10, 4, -52), new Vector3(0, 6, -30)), ("city", new Vector3(-45, 30, -50), new Vector3(0, 0, 15)),
                ("street", new Vector3(2, 2.5f, -22), new Vector3(0, 3, 10)), ("workshop", new Vector3(-12, 3, 0), new Vector3(-22, 1, 4)), ("hall", new Vector3(0, 4, 56), new Vector3(0, 5, 92)) });
            Scene("Envy", new[] { ("start", (Vector3?)null, Vector3.zero), ("village", new Vector3(-6, 3, -22), new Vector3(0, 2, 6)), ("island", new Vector3(-60, 40, -70), new Vector3(0, 0, 30)),
                ("spring", new Vector3(-20, 9, 26), new Vector3(-30, 7, 36)), ("plaza", new Vector3(0, 4, 52), new Vector3(0, 4, 82)) });
            Scene("Gloom", new[] { ("start", (Vector3?)null, Vector3.zero), ("cave", new Vector3(-20, 14, -50), new Vector3(0, 0, 10)), ("shard", new Vector3(-34, 2.5f, -16), new Vector3(-38, 2, -26)),
                ("machine", new Vector3(8, 4, 42), new Vector3(14, 3, 54)), ("hall", new Vector3(0, 4, 56), new Vector3(0, 6, 90)) });
            Scene("Betrayal", new[] { ("start", (Vector3?)null, Vector3.zero), ("maze", new Vector3(-40, 35, -30), new Vector3(0, 0, 35)), ("corridor", new Vector3(0, 2.5f, -6), new Vector3(0, 2, 10)),
                ("heart", new Vector3(0, 6, 76), new Vector3(0, 6, 98)) });
            Scene("Violence", new[] { ("start", (Vector3?)null, Vector3.zero), ("field", new Vector3(-50, 40, -90), new Vector3(0, 0, 10)), ("forge", new Vector3(-20, 4, -30), new Vector3(-32, 2, -38)),
                ("front", new Vector3(0, 5, 60), new Vector3(0, 5, 94)) });
            Scene("Lies", new[] { ("start", (Vector3?)null, Vector3.zero), ("islands", new Vector3(-40, 30, -60), new Vector3(0, 0, 30)), ("bridges", new Vector3(-4, 3, -18), new Vector3(16, 0, 8)),
                ("arena", new Vector3(0, 5, 86), new Vector3(0, 3, 112)) });
            Scene("Power", new[] { ("start", (Vector3?)null, Vector3.zero), ("bridge", new Vector3(-20, 18, -110), new Vector3(0, 0, 0)), ("gate", new Vector3(0, 4, 30), new Vector3(0, 8, 56)),
                ("hall", new Vector3(0, 5, 66), new Vector3(0, 8, 100)) });
            Scene("Epilogue", new[] { ("start", (Vector3?)null, Vector3.zero), ("earth", new Vector3(2, 1.5f, -3), new Vector3(0, 8, 120)), ("worlds", new Vector3(0, 700, -2200), new Vector3(0, 0, 120)) });
            Scene("Menu", new[] { ("title", (Vector3?)null, Vector3.zero) });
            Debug.Log("Snapshots rendered");
        }

        static void Scene(string name, (string tag, Vector3? pos, Vector3 look)[] views)
        {
            EditorSceneManager.OpenScene($"{SceneBuilder.SceneDir}/{name}.unity", OpenSceneMode.Single);
            var cam = Camera.main;
            var p0 = cam.transform.position;
            var r0 = cam.transform.rotation;
            foreach (var (tag, pos, look) in views)
            {
                if (pos.HasValue)
                {
                    cam.transform.position = pos.Value;
                    cam.transform.LookAt(look);
                }
                else cam.transform.SetPositionAndRotation(p0, r0);
                Save(cam, $"{Out}/{name}-{tag}.jpg", 1280, 720);
            }
        }

        static void Save(Camera cam, string path, int w, int h)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.aspect = (float)w / h;
            cam.Render();
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            File.WriteAllBytes(path, tex.EncodeToJPG(88));
            Object.DestroyImmediate(tex);
            rt.Release();
        }
    }
}
