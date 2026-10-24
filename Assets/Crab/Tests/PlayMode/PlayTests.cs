// The first stage played through in the real scenes (the story's moments are triggered the way
// the player would: eating, hiding, running, drawing matter in, creating, shooting, sealing).
// Pictures with the interface on top go to Docs/shots/play-*.png.
using System.Collections;
using System.IO;
using System.Linq;
using Crab.Core;
using Crab.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Crab.Tests
{
    public sealed partial class PlayTests
    {
        const int W = 1280, Hh = 720;
        RenderTexture uiRt;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            GameSession.SavePathOverride = Path.Combine(Application.temporaryCachePath, "test-save.json");
            if (File.Exists(GameSession.SavePathOverride)) File.Delete(GameSession.SavePathOverride);
            Time.timeScale = 1;
            GameSession.Ensure().ResetState();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            var doc = Object.FindAnyObjectByType<UIDocument>();
            if (doc && doc.panelSettings) doc.panelSettings.targetTexture = null;
            if (uiRt) Object.Destroy(uiRt);
            GameSession.SavePathOverride = null;
            yield return null;
        }

        static IEnumerator Load(string scene)
        {
            GameSession.Ensure().State.Scene = scene;
            yield return SceneManager.LoadSceneAsync(scene);
            yield return null;
            yield return null;
        }

        static IEnumerator Wait(float s)
        {
            float end = Time.realtimeSinceStartup + s;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        static IEnumerator Until(System.Func<bool> cond, float timeout, string what)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!cond())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail($"timed out waiting for: {what} (task: '{Story.Objective}')");
                yield return null;
            }
        }

        void UiToTexture()
        {
            var doc = Object.FindAnyObjectByType<UIDocument>();
            uiRt = new RenderTexture(W, Hh, 24, RenderTextureFormat.ARGB32);
            doc.panelSettings.targetTexture = uiRt;
            doc.panelSettings.clearColor = true;
            doc.panelSettings.colorClearValue = Color.clear;
        }

        IEnumerator Shot(string name)
        {
            yield return null;
            var cam = Camera.main;
            var rt = new RenderTexture(W, Hh, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            var scene = Read(rt);
            if (uiRt)
            {
                var ui = Read(uiRt);
                var a = scene.GetPixels32();
                var b = ui.GetPixels32();
                for (int i = 0; i < a.Length; i++)
                {
                    float k = b[i].a / 255f;
                    a[i] = new Color32((byte)Mathf.Min(255, b[i].r + a[i].r * (1 - k)), (byte)Mathf.Min(255, b[i].g + a[i].g * (1 - k)), (byte)Mathf.Min(255, b[i].b + a[i].b * (1 - k)), 255);
                }
                scene.SetPixels32(a);
                Object.Destroy(ui);
            }
            Directory.CreateDirectory("Docs/shots");
            File.WriteAllBytes($"Docs/shots/play-{name}.png", scene.EncodeToPNG());
            Object.Destroy(scene);
            rt.Release();
        }

        static Texture2D Read(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            t.Apply();
            RenderTexture.active = prev;
            return t;
        }

        /// <summary>Point the crab and its camera at a point (third person, or first in boss scenes).</summary>
        static void AimAt(Hero hero, Vector3 target)
        {
            var from = hero.Cam.Mode == CamMode.First ? hero.Cam.eye.position : hero.transform.position + Vector3.up * 0.85f;
            var d = target - from;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            hero.Cam.Face(yaw, pitch);
        }

        // ------------------------------------------------------------------ the prologue
        [UnityTest]
        public IEnumerator PrologueToTheRift()
        {
            yield return Load("Prologue");
            UiToTexture();
            var hero = Hero.Instance;
            var dir = Object.FindAnyObjectByType<PrologueDirector>();
            var st = GameSession.Instance.State;
            Time.timeScale = 3;
            yield return Until(() => Story.Objective.StartsWith("Найти еду"), 15, "the food task");
            yield return Shot("prologue-start");
            foreach (var m in dir.morsels) { m.Use(hero); yield return null; }
            yield return Until(() => Story.Objective.StartsWith("Спрятаться"), 10, "the gull task");
            // hide under the slab before the gull dives
            var c = dir.shelter.bounds.center;
            hero.Controller.Teleport(new Vector3(c.x, c.y - dir.shelter.bounds.extents.y + 0.05f, c.z), 0);
            yield return Until(() => Story.Objective.StartsWith("Бежать"), 30, "the wave task");
            Assert.That(st.Health.Value, Is.GreaterThan(80), "the gull missed the hidden crab");
            // run up the beach beyond the wave's reach
            hero.Controller.Teleport(new Vector3(0, 1.2f, dir.wave.reachZ + 3), 0);
            yield return Until(() => st.Flag("spark"), 40, "the Spark");
            yield return Wait(2);
            yield return Shot("prologue-spark");
            yield return Until(() => Story.Objective.StartsWith("Почувствовать"), 40, "the matter task");
            // draw matter out of the driftwood, make the stick
            var src = dir.driftwood;
            hero.Controller.Teleport(src.transform.position + Vector3.back * 1.2f, 0);
            for (int i = 0; i < 40 && st.Matter[Matter.Organic] < 4; i++) { src.Channel(hero, 0.2f); yield return null; }
            Assert.That(st.Matter[Matter.Organic], Is.GreaterThanOrEqualTo(2));
            yield return Until(() => Story.Objective.StartsWith("Создать палку"), 15, "the stick task");
            Assert.That(hero.Power.Create(ItemId.Stick), Is.EqualTo(GameState.CreateResult.Done));
            yield return Until(() => dir.rift.gameObject.activeSelf, 30, "the rift");
            Assert.That(st.Items.Equipped, Is.EqualTo(ItemId.Stick));
            Assert.That(Story.Objective, Does.StartWith("Войти в разлом"));
            Time.timeScale = 1;
            AimAt(hero, dir.rift.Point);
            yield return Wait(1);
            yield return Shot("prologue-rift");
        }

        // ------------------------------------------------------------------ Limbo: fighting
        [UnityTest]
        public IEnumerator LimboPistolKillsAShade()
        {
            var st = GameSession.Ensure().State;
            st.SetFlag("spark");
            st.SetFlag("limbo_intro");
            yield return Load("Limbo");
            UiToTexture();
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            st.Matter.Set(10, 10, 30);
            st.Learn(ItemId.Pistol);
            st.Learn(ItemId.Ammo);
            Assert.That(hero.Power.Create(ItemId.Pistol), Is.EqualTo(GameState.CreateResult.Done));
            Assert.That(hero.Power.Create(ItemId.Ammo), Is.EqualTo(GameState.CreateResult.Done));
            Assert.That(st.Items.Equipped, Is.EqualTo(ItemId.Pistol));
            var dir = Object.FindAnyObjectByType<LimboDirector>();
            var shade = dir.firstShades.First(e => e);
            var sp = shade.transform.position;
            hero.Controller.Teleport(sp + Vector3.back * 6, 0);
            yield return Wait(0.5f);
            for (int i = 0; i < 12 && shade && shade.Alive; i++)
            {
                AimAt(hero, shade.transform.position + Vector3.up * 0.9f);
                yield return null;
                yield return null;
                hero.Combat.Fire();
                if (i == 0) yield return Shot("limbo-shoot");
                yield return Wait(0.4f);
            }
            Assert.That(!shade || !shade.Alive, "two or three shots fell a shade");
            Assert.That(st.Items.Count(ItemId.Ammo), Is.LessThan(8));
        }

        [UnityTest]
        public IEnumerator SealingARiftTeachesThePistol()
        {
            var st = GameSession.Ensure().State;
            st.SetFlag("spark");
            st.SetFlag("limbo_intro");
            yield return Load("Limbo");
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            var dir = Object.FindAnyObjectByType<LimboDirector>();
            var rift = dir.rifts[0];
            hero.Controller.Teleport(rift.transform.position + rift.transform.forward * 2, rift.transform.eulerAngles.y + 180);
            yield return Wait(0.5f);
            for (int i = 0; i < 100 && rift.open; i++) { rift.Channel(hero, 0.1f); yield return null; }
            Assert.That(rift.open, Is.False);
            yield return null;
            Assert.That(st.Flag("rift_" + rift.id), Is.True);
            Assert.That(st.Knows(ItemId.Pistol), Is.True, "the first sealed rift shows how to make a pistol");
            Assert.That(Story.Objective, Does.Contain("(1/3)"));
        }

        // ------------------------------------------------------------------ Limbo: the Guardian
        [UnityTest]
        public IEnumerator GuardianFightFirstPerson()
        {
            var st = GameSession.Ensure().State;
            st.SetFlag("spark");
            st.SetFlag("limbo_intro");
            yield return Load("Limbo");
            UiToTexture();
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            var dir = Object.FindAnyObjectByType<LimboDirector>();
            foreach (var r in dir.rifts) r.Seal();
            yield return Until(() => dir.guardian.awake, 10, "the Guardian wakes");
            st.Matter.Set(10, 10, 30);
            st.Learn(ItemId.Pistol);
            st.Learn(ItemId.Ammo);
            hero.Power.Create(ItemId.Pistol);
            for (int i = 0; i < 10; i++) hero.Power.Create(ItemId.Ammo);
            var g = dir.guardian;
            hero.Controller.Teleport(g.transform.position + Vector3.back * 9, 0);
            yield return Until(() => g.Fighting && hero.Cam.Mode == CamMode.First && !hero.Controller.frozen, 15, "the first-person fight");
            yield return Wait(0.5f);
            var heart = g.GetComponentInChildren<GuardianHeart>().transform;
            int shots = 0;
            float before = g.Health01;
            while (g.Alive && shots < 80)
            {
                st.Items.Add(ItemId.Ammo, 6);
                AimAt(hero, heart.GetComponent<SphereCollider>().bounds.center);   // where the glowing heart is seen
                yield return null;
                yield return null;
                if (hero.Combat.Fire()) shots++;
                if (shots == 3) yield return Shot("guardian-fight");
                yield return Wait(0.35f);
            }
            Assert.That(g.Health01, Is.LessThan(before));
            Assert.That(g.Alive, Is.False, $"the heart takes the full blow ({shots} shots)");
            yield return Until(() => hero.Cam.Mode == CamMode.Third, 10, "back to third person");
            yield return Until(() => st.Flag("guardian"), 10, "the victory is kept");
            yield return Until(() => dir.exit.gameObject.activeSelf, 20, "the way to the second circle opens");
            AimAt(hero, dir.exit.Point);
            yield return Wait(1);
            yield return Shot("guardian-after");
        }
    }
}
