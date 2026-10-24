// Strips of frames of things in motion, from a camera beside them (Docs/shots/strip-*.png):
// the crab walking sideways and running, a shade and a scavenger coming at it, the demons of Wrath. For checking the
// procedural animation by eye — the joints bend the right way, the feet lift, nothing flies off.
using System.Collections;
using System.IO;
using Crab.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Crab.Tests
{
    public sealed class MotionStrips
    {
        const int W = 420, H = 300, Frames = 6;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            GameSession.SavePathOverride = Path.Combine(Application.temporaryCachePath, "test-save.json");
            var s = GameSession.Ensure();
            s.ResetState();
            s.State.SetFlag("spark");
            s.State.SetFlag("limbo_intro");
            yield return SceneManager.LoadSceneAsync("Limbo");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameSession.SavePathOverride = null;
            yield return null;
        }

        static IEnumerator Strip(string name, Transform subject, Vector3 side, float height, float dist, float step = 0.12f)
        {
            var go = new GameObject("strip camera");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 40;
            cam.nearClipPlane = 0.05f;
            var strip = new Texture2D(W * Frames, H, TextureFormat.RGB24, false);
            var rt = new RenderTexture(W, H, 24) { antiAliasing = 4 };
            for (int f = 0; f < Frames; f++)
            {
                var p = subject.position + Vector3.up * height;
                go.transform.position = p + side.normalized * dist;
                go.transform.LookAt(p);
                cam.targetTexture = rt;
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                strip.ReadPixels(new Rect(0, 0, W, H), f * W, 0);
                RenderTexture.active = prev;
                float end = Time.time + step;
                while (Time.time < end) yield return null;
            }
            strip.Apply();
            Directory.CreateDirectory("Docs/shots");
            File.WriteAllBytes($"Docs/shots/strip-{name}.png", strip.EncodeToPNG());
            Object.Destroy(strip);
            Object.Destroy(go);
            rt.Release();
        }

        [UnityTest]
        public IEnumerator WalkRunAndEnemies()
        {
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            var dir = Object.FindAnyObjectByType<LimboDirector>();
            foreach (var e in dir.firstShades) if (e) Object.Destroy(e.gameObject);
            var open = new Vector3(0, 0, -16);
            open.y = Physics.Raycast(open + Vector3.up * 20, Vector3.down, out var hit, 40) ? hit.point.y : 0;
            hero.Controller.Teleport(open, 90);
            hero.Cam.Face(0);
            yield return new WaitForSeconds(0.5f);

            // sideways (the camera looks north, the crab moves east, its body turned across)
            hero.Controller.autopilot = new Vector2(1, 0);
            yield return new WaitForSeconds(0.8f);
            yield return Strip("crab-walk", hero.transform, Vector3.back, 0.25f, 2.0f);
            yield return Strip("crab-walk-top", hero.transform, Vector3.up * 2 + Vector3.back, 0.2f, 1.8f);
            hero.Controller.autopilotRun = true;
            yield return new WaitForSeconds(0.6f);
            yield return Strip("crab-run", hero.transform, Vector3.back, 0.25f, 2.2f, 0.08f);
            hero.Controller.autopilot = null;
            yield return new WaitForSeconds(0.6f);
            yield return Strip("crab-idle", hero.transform, Vector3.back + Vector3.right * 0.5f, 0.25f, 1.6f, 0.4f);

            // a shade and a scavenger come at the crab
            var lib = Library.Current;
            var pos = hero.transform.position;
            var shade = Object.Instantiate(lib.shade, pos + new Vector3(-7, 0, 1), Quaternion.identity).GetComponent<Enemy>();
            var crawler = Object.Instantiate(lib.crawler, pos + new Vector3(7, 0, -1), Quaternion.identity).GetComponent<Enemy>();
            yield return new WaitForSeconds(0.6f);
            yield return Strip("shade", shade.transform, Vector3.back, 1.0f, 4.5f, 0.2f);
            yield return Strip("crawler", crawler.transform, Vector3.back, 0.35f, 2.6f, 0.1f);
            Assert.That(shade.Alive && crawler.Alive);
        }

        [UnityTest]
        public IEnumerator WrathDemons()
        {
            var st = GameSession.Instance.State;
            st.SetFlag("wrath_intro");
            yield return SceneManager.LoadSceneAsync("Wrath");
            yield return null;
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            foreach (var e in Object.FindAnyObjectByType<WrathDirector>().firstImps) if (e) Object.Destroy(e.gameObject);
            var open = new Vector3(0, 0, -36);
            open.y = Physics.Raycast(open + Vector3.up * 20, Vector3.down, out var hit, 40) ? hit.point.y : 0;
            hero.Controller.Teleport(open, 0);
            yield return new WaitForSeconds(0.5f);
            var lib = Library.Current;
            var pos = hero.transform.position;
            var imp = Object.Instantiate(lib.imp, pos + new Vector3(-8, 0, 2), Quaternion.identity).GetComponent<Enemy>();
            var brute = Object.Instantiate(lib.brute, pos + new Vector3(9, 0, 3), Quaternion.identity).GetComponent<Enemy>();
            var burrower = Object.Instantiate(lib.burrower, pos + new Vector3(0, 0, 9), Quaternion.identity).GetComponent<Enemy>();
            yield return new WaitForSeconds(0.6f);
            yield return Strip("imp", imp.transform, Vector3.back, 0.7f, 3.5f, 0.12f);
            yield return Strip("brute", brute.transform, Vector3.back + Vector3.right * 0.3f, 1.5f, 7f, 0.2f);
            yield return Strip("burrower", burrower.transform, Vector3.back + Vector3.left * 0.4f, 0.4f, 3.5f, 0.15f);
            Assert.That(imp.Alive && brute.Alive && burrower.Alive);
        }
    }
}
