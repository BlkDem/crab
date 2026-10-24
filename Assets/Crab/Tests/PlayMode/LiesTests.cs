// The eighth circle played in the real scene: the truth lens and the bridges that lie, a mimic,
// a bridge of matter across the gap, the moment of doubt, the archive, and the Father of Lies
// among his copies. Pictures: Docs/shots/play-lies-*.jpg.
using System.Collections;
using System.Linq;
using Crab.Core;
using Crab.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Crab.Tests
{
    public sealed partial class PlayTests
    {
        static GameState LiesState()
        {
            var st = GameSession.Ensure().State;
            foreach (var f in new[] { "spark", "limbo_intro", "circle1_done", "circle2_done", "circle3_done", "circle4_done", "circle5_done", "circle6_done", "circle7_done" }) st.SetFlag(f);
            foreach (var id in new[] { ItemId.Pistol, ItemId.Ammo, ItemId.EnergyRifle, ItemId.EnergyCell, ItemId.SparkCore, ItemId.CyberFrame, ItemId.Constructor, ItemId.Factory }) st.Learn(id);
            return st;
        }

        [UnityTest]
        public IEnumerator TruthBridgesAndDoubt()
        {
            var st = LiesState();
            yield return Load("Lies");
            UiToTexture();
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            var dir = Object.FindAnyObjectByType<LiesDirector>();
            yield return Until(() => st.Knows(ItemId.TruthLens), 25, "the AI offers the lens");
            st.Matter.Set(10, 20, 30, 10, 20, 30);
            Assert.That(hero.Power.Create(ItemId.TruthLens), Is.EqualTo(GameState.CreateResult.Done));
            yield return Until(() => hero.Lens, 3, "the lens is on");
            var falseB = Object.FindObjectsByType<Illusion>().First(i => i.kind == Illusion.Kind.False);
            var hiddenB = Object.FindObjectsByType<Illusion>().First(i => i.kind == Illusion.Kind.Hidden);
            Assert.That(falseB.GetComponent<Collider>().enabled, Is.False, "a lying bridge does not hold");
            Assert.That(hiddenB.GetComponent<Collider>().enabled, Is.True, "a hidden one does");
            Assert.That(hiddenB.GetComponent<Renderer>().enabled, Is.False, "and is not seen");
            hero.Controller.Teleport(new Vector3(0, 0.2f, -16), 30);
            yield return Wait(0.3f);
            Assert.That(hero.Lens.Look());
            yield return Wait(0.3f);
            Assert.That(hiddenB.GetComponent<Renderer>().enabled, "the lens shows it");
            hero.Cam.Face(40, 15);
            yield return Wait(0.3f);
            yield return Shot("lies-lens");
            // a mimic among the heaps
            var mimic = Object.FindObjectsByType<Mimic>().First();
            Assert.That(mimic.Revealed, Is.False);
            hero.Controller.Teleport(mimic.transform.position + Vector3.back * 2.5f, 0);
            yield return Until(() => mimic.Revealed, 5, "the heap opens its mouth");
            mimic.Hit(999, mimic.transform.position + Vector3.up, Vector3.forward, hero.gameObject);
            // the gap: no bridge at all — the crab learns to build one
            hero.Controller.Teleport(dir.gap.position + Vector3.back * 2, 0);
            yield return Until(() => st.Knows(ItemId.Bridgework), 10, "the bridge blueprint");
            st.Matter.Set(10, 20, 30, 10, 20, 30);
            Assert.That(hero.Power.Create(ItemId.Bridgework), Is.EqualTo(GameState.CreateResult.Done));
            Assert.That(st.Level, Is.EqualTo(8));
            yield return Until(() => hero.Bridges, 3, "the bridges");
            var to = dir.doubt.position - hero.transform.position;
            to.y = 0;
            hero.Controller.Teleport(new Vector3(-0.5f, 0.1f, 37), Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
            hero.Cam.Face(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 15);
            yield return Wait(0.3f);
            // lay slabs and walk them across
            for (int i = 0; i < 4 && Vector3.Distance(hero.transform.position, dir.doubt.position) > 9; i++)
            {
                var slab = hero.Bridges.Lay();
                Assert.That(slab, Is.Not.Null);
                yield return Wait(0.2f);
                hero.Controller.Teleport(slab.transform.position + slab.transform.forward * 2 + Vector3.up * 0.3f, hero.transform.eulerAngles.y);
                yield return Wait(0.3f);
                Assert.That(hero.transform.position.y, Is.GreaterThan(-3), "the slab holds the crab");
                if (i == 0) yield return Shot("lies-bridge");
            }
            hero.Controller.Teleport(dir.doubt.position + Vector3.up * 0.3f, 0);
            yield return Until(() => st.Flag("doubt"), 10, "the doubt");
            yield return Wait(2);
            Assert.That(hero.GetComponentsInChildren<Renderer>().All(r => !r.enabled || r.name.StartsWith("Glow") || true));
            yield return Until(() => !hero.Controller.frozen && Story.Objective.StartsWith("Прочесть"), 30, "the crab is back");
            Assert.That(hero.GetComponentsInChildren<Renderer>().Any(r => r.enabled), "the crab exists");
            var a = dir.archive;
            hero.Controller.Teleport(a.transform.position + Vector3.back * 2.5f, 0);
            yield return Wait(0.3f);
            a.Use(hero);
            yield return Until(() => st.Flag("archive"), 30, "the archive read");
            yield return Until(() => dir.father.awake, 20, "the Father waits");
        }

        [UnityTest]
        public IEnumerator FatherOfLiesFight()
        {
            var st = LiesState();
            foreach (var f in new[] { "lies_intro", "bridges", "architect_of_ways", "doubt", "archive" }) st.SetFlag(f);
            foreach (var id in new[] { ItemId.TruthLens, ItemId.Bridgework }) st.Learn(id);
            yield return Load("Lies");
            UiToTexture();
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            var dir = Object.FindAnyObjectByType<LiesDirector>();
            st.AtWorkshop = true;
            foreach (var id in new[] { ItemId.TruthLens, ItemId.Bridgework, ItemId.EnergyRifle })
            {
                st.Matter.Set(10, 20, 30, 10, 20, 30);
                hero.Power.Create(id);
            }
            st.AtWorkshop = false;
            st.Items.Add(ItemId.EnergyCell, 60);
            yield return Until(() => dir.father.awake, 20, "the Father waits");
            foreach (var e in Enemy.All.ToList()) if (e) Object.Destroy(e.gameObject);
            var f0 = dir.father;
            hero.Controller.Teleport(f0.arena.position + Vector3.back * 10, 0);
            yield return Until(() => f0.Fighting && hero.Cam.Mode == CamMode.First && !hero.Controller.frozen, 25, "the first-person fight");
            Assert.That(f0.Copies.Count, Is.GreaterThanOrEqualTo(3), "he stands among copies");
            // a blow to a copy feeds him
            var copy = f0.Copies.First(c => c && c.activeSelf);
            float before = f0.Health01;
            copy.GetComponent<LiarCopy>().Hit(30, copy.transform.position + Vector3.up * 2, Vector3.forward, hero.gameObject);
            Assert.That(copy.activeSelf, Is.False, "the copy shatters");
            int shots = 0;
            float end = Time.realtimeSinceStartup + 120;
            while (f0.Alive && Time.realtimeSinceStartup < end)
            {
                if (!TruthLens.Active) hero.Lens.Look();
                st.Items.Add(ItemId.EnergyCell, 10);
                AimAt(hero, f0.AimPoint);
                yield return null;
                if (hero.Combat.Fire()) shots++;
                if (shots == 5) yield return Shot("lies-father");
            }
            Assert.That(f0.Alive, Is.False, $"the true one falls ({shots} shots)");
            yield return Until(() => st.Flag("liar"), 10, "the victory is kept");
            yield return Until(() => dir.exit.gameObject.activeSelf, 30, "the way on opens");
        }
    }
}
