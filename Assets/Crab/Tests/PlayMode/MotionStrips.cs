// Strips of frames of things in motion, from a camera beside them (Docs/shots/strip-*.jpg):
// the crab walking sideways and running, a shade and a scavenger coming at it, the demons of Wrath. For checking the
// procedural animation by eye — the joints bend the right way, the feet lift, nothing flies off.
using System.Collections;
using System.IO;
using System.Linq;
using Crab.Core;
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
            SceneManager.LoadScene("Limbo");
            yield return null;
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
            File.WriteAllBytes($"Docs/shots/strip-{name}.jpg", strip.EncodeToJPG(88));
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
        public IEnumerator CrabGestures()
        {
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            var model = hero.Rig.body;
            for (int s = 0; s < 2; s++)
            {
                string side = s == 0 ? "L" : "R";
                for (int i = 0; i < 4; i++)
                    foreach (var j in new[] { "_0", "_1", "_2" }) Assert.That(CrabRig.Find(model, $"Leg{side}{i}{j}"), Is.Not.Null, $"Leg{side}{i}{j}");
                for (int j = 0; j < 4; j++) Assert.That(CrabRig.Find(model, $"Claw{side}_{j}"), Is.Not.Null, $"Claw{side}_{j}");
                Assert.That(CrabRig.Find(model, "Eye" + side), Is.Not.Null, "Eye" + side);
            }
            Assert.That(hero.Rig.gripRight && hero.Rig.gripLeft, "the grips");
            var dir = Object.FindAnyObjectByType<LimboDirector>();
            foreach (var e in dir.firstShades) if (e) Object.Destroy(e.gameObject);
            var open = new Vector3(0, 0, -16);
            open.y = Physics.Raycast(open + Vector3.up * 20, Vector3.down, out var hit, 40) ? hit.point.y : 0;
            hero.Controller.Teleport(open, 0);
            hero.Cam.Face(0);
            yield return new WaitForSeconds(0.5f);
            var front = Vector3.forward + Vector3.right * 0.6f + Vector3.up * 0.3f;
            foreach (var pose in new[] { ClawPose.Strike, ClawPose.Throw, ClawPose.Eat, ClawPose.Create, ClawPose.Think, ClawPose.Hurt })
            {
                if (pose == ClawPose.Create || pose == ClawPose.Think) hero.Rig.Hold(pose);
                else hero.Rig.Play(pose, 0.7f);
                yield return Strip("crab-" + pose.ToString().ToLowerInvariant(), hero.transform, front, 0.3f, 2.2f, 0.12f);
                hero.Rig.Release(pose);
                yield return new WaitForSeconds(0.3f);
            }
            // things in the big claw: a stick swung, a pistol aimed
            var st = GameSession.Instance.State;
            st.Items.Add(ItemId.Stick);
            st.Items.Equip(ItemId.Stick);
            yield return new WaitForSeconds(0.3f);
            hero.Rig.Play(ClawPose.Strike, 0.7f);
            yield return Strip("crab-stick", hero.transform, front, 0.3f, 2.2f, 0.12f);
            st.Items.Add(ItemId.Pistol);
            st.Items.Add(ItemId.Ammo, 30);
            st.Items.Equip(ItemId.Pistol);
            yield return new WaitForSeconds(0.3f);
            var g = hero.Rig.gripRight;
            hero.Rig.Hold(ClawPose.Aim);
            yield return Strip("crab-pistol", hero.transform, front, 0.3f, 2.2f, 0.2f);
            hero.Rig.Release(ClawPose.Aim);
            // the rest of the arsenal, each in the claw (the guns aimed)
            foreach (var id in new[] { ItemId.Knife, ItemId.MachineGun, ItemId.EnergyRifle, ItemId.EnergyBlade, ItemId.GravityGun })
            {
                st.Items.Add(id);
                st.Items.Equip(id);
                yield return new WaitForSeconds(0.3f);
                Assert.That(hero.Rig.gripRight.childCount, Is.EqualTo(1), id.ToString());
                bool gun = Blueprints.Get(id).kind == WeaponKind.Gun;
                if (gun) hero.Rig.Hold(ClawPose.Aim);
                yield return Strip("crab-" + id.ToString().ToLowerInvariant(), hero.transform, front, 0.3f, 2.4f, 0.15f);
                if (gun) hero.Rig.Release(ClawPose.Aim);
            }
            // the shots in flight, from the side: each gun its own (a shot at the start of each strip)
            st.Items.Add(ItemId.Ammo, 60);
            st.Items.Add(ItemId.EnergyCell, 30);
            foreach (var id in new[] { ItemId.Pistol, ItemId.MachineGun, ItemId.EnergyRifle, ItemId.GravityGun })
            {
                st.Items.Equip(id);
                yield return new WaitForSeconds(0.4f);
                Assert.That(hero.Combat.Fire(), id.ToString());
                yield return Strip("shot-" + id.ToString().ToLowerInvariant(), hero.transform, Vector3.right + Vector3.up * 0.3f, 0.4f, 6f, 0.03f);
            }
        }

        [UnityTest]
        public IEnumerator CrabForms()
        {
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            var st = GameSession.Instance.State;
            foreach (var e in Object.FindAnyObjectByType<LimboDirector>().firstShades) if (e) Object.Destroy(e.gameObject);
            var open = new Vector3(0, 0, -16);
            open.y = Physics.Raycast(open + Vector3.up * 20, Vector3.down, out var hit, 40) ? hit.point.y : 0;
            st.Items.Add(ItemId.Stick);
            for (int level = 2; level <= 6; level++)
            {
                DebugMode.SetLevel(st, level);
                yield return null;
                Assert.That(hero.Form, Is.EqualTo(level), $"the form of level {level}");
                var body = hero.Rig.body;
                foreach (var side in new[] { "L", "R" })
                {
                    Assert.That(CrabRig.Find(body, $"Claw{side}_0"), Is.Not.Null, $"form {level}: Claw{side}");
                    for (int i = 0; i < 4; i++) Assert.That(CrabRig.Find(body, $"Leg{side}{i}_1"), Is.Not.Null, $"form {level}: Leg{side}{i}");
                }
                Assert.That(hero.Rig.gripRight && hero.Rig.gripRight.childCount > 0, $"form {level}: the stick in the claw");
                hero.Controller.Teleport(open, 90);
                hero.Cam.Face(0);
                yield return new WaitForSeconds(0.4f);
                hero.Controller.autopilot = new Vector2(1, 0);
                yield return new WaitForSeconds(0.6f);
                yield return Strip($"form{level}-walk", hero.transform, Vector3.back + Vector3.up * 0.4f, 0.4f, 3.2f);
                hero.Controller.autopilot = null;
                yield return new WaitForSeconds(0.5f);
                hero.Rig.Play(ClawPose.Strike, 0.7f);
                yield return Strip($"form{level}-strike", hero.transform, hero.transform.forward + hero.transform.right * 0.6f + Vector3.up * 0.4f, 0.4f, 3.4f);
            }
            DebugMode.SetLevel(st, 1);
            yield return null;
            Assert.That(hero.Form, Is.EqualTo(1), "back to the crab after the Spark");
        }

        [UnityTest]
        public IEnumerator WrathDemons()
        {
            var st = GameSession.Instance.State;
            st.SetFlag("wrath_intro");
            SceneManager.LoadScene("Wrath");
            yield return null;
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

        [UnityTest]
        public IEnumerator GreedDemonsAndTheTank()
        {
            var st = GameSession.Instance.State;
            st.SetFlag("greed_intro");
            st.SetFlag("city");
            SceneManager.LoadScene("Greed");
            yield return null;
            yield return null;
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            foreach (var e in Enemy.All.ToArray()) if (e) Object.Destroy(e.gameObject);
            var open = new Vector3(0, 0, -8);
            hero.Controller.Teleport(open, 0);
            yield return new WaitForSeconds(0.5f);
            var lib = Library.Current;
            var pos = hero.transform.position;
            var guard = Object.Instantiate(lib.goldGuard, pos + new Vector3(-7, 0, 6), Quaternion.identity).GetComponent<Enemy>();
            var collector = Object.Instantiate(lib.collector, pos + new Vector3(6, 0, 5), Quaternion.identity).GetComponent<Enemy>();
            st.Items.Add(ItemId.Tank);
            yield return new WaitForSeconds(0.8f);
            yield return Strip("goldguard", guard.transform, Vector3.back + Vector3.left * 0.3f, 1.2f, 5f, 0.15f);
            yield return Strip("collector", collector.transform, Vector3.back, 0.6f, 3.5f, 0.12f);
            Assert.That(hero.Tank, Is.Not.Null);
            yield return Strip("tank", hero.Tank.transform, Vector3.back + Vector3.right * 0.5f, 0.7f, 3.5f, 0.15f);
        }
    }
}
