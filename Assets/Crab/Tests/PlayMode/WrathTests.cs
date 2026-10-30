// The second circle played in the real scene: the machine gun against imps, the vents teaching
// the armour and the drone, the drone fighting on its own, and the Fire Colossus.
// Pictures go to Docs/shots/play-wrath-*.png.
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
        static GameState WrathState()
        {
            var st = GameSession.Ensure().State;
            foreach (var f in new[] { "spark", "limbo_intro", "circle1_done", "wrath_intro" }) st.SetFlag(f);
            st.Learn(ItemId.Pistol);
            st.Learn(ItemId.Ammo);
            return st;
        }

        static void Arm(Hero hero, GameState st)
        {
            st.Matter.Set(10, 10, 30);
            st.Learn(ItemId.MachineGun);
            Assert.That(hero.Power.Create(ItemId.MachineGun), Is.EqualTo(GameState.CreateResult.Done));
            st.Items.Add(ItemId.Ammo, 100);
            st.Items.Equip(ItemId.MachineGun);
        }

        [UnityTest]
        public IEnumerator WrathMachineGunMowsImps()
        {
            var st = WrathState();
            yield return Load("Wrath");
            UiToTexture();
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            Arm(hero, st);
            var dir = Object.FindAnyObjectByType<WrathDirector>();
            var imps = dir.firstImps.Where(e => e).ToList();
            Assert.That(imps.Count, Is.EqualTo(3));
            hero.Controller.Teleport(imps[0].transform.position + Vector3.back * 7, 0);
            yield return Wait(0.3f);
            int shots = 0;
            foreach (var imp in imps)
            {
                // as a player would: within sight of each (demons don't see, nor are hit, through walls)
                if (imp && imp.Alive && Vector3.Distance(hero.transform.position, imp.transform.position) > 9)
                {
                    hero.Controller.Teleport(Nav.FreeSpot(imp.transform.position + Vector3.back * 7, 0.4f, 0.7f) + Vector3.up * 0.1f, 0);
                    yield return Wait(0.3f);
                }
                float end = Time.realtimeSinceStartup + 8;
                while (imp && imp.Alive && Time.realtimeSinceStartup < end)
                {
                    AimAt(hero, imp.transform.position + Vector3.up * 0.6f);
                    yield return null;
                    if (hero.Combat.Fire()) shots++;
                    if (shots == 6) yield return Shot("wrath-mg");
                }
            }
            Assert.That(imps.All(e => !e || !e.Alive), $"the machine gun mows the imps down ({shots} shots)");
            Assert.That(st.Items.Count(ItemId.Ammo), Is.LessThan(100));
        }

        [UnityTest]
        public IEnumerator VentsTeachArmourAndTheDrone()
        {
            var st = WrathState();
            yield return Load("Wrath");
            UiToTexture();
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            Arm(hero, st);
            var dir = Object.FindAnyObjectByType<WrathDirector>();
            foreach (var e in dir.firstImps) if (e) Object.Destroy(e.gameObject);
            yield return Until(() => Story.Objective.StartsWith("Заглушить"), 10, "the vents task");

            var vent = dir.vents[0];
            hero.Controller.Teleport(vent.transform.position + Vector3.back * 2.5f, 0);
            yield return Wait(0.3f);
            for (int i = 0; i < 100 && vent.open; i++) { vent.Channel(hero, 0.1f); yield return null; }
            Assert.That(vent.open, Is.False);
            yield return null;
            Assert.That(st.Knows(ItemId.ScrapArmor), "the first vent shows how to make armour");
            Assert.That(hero.Power.Create(ItemId.ScrapArmor), Is.EqualTo(GameState.CreateResult.Done));
            yield return null;
            Assert.That(st.Level, Is.EqualTo(2), "armour makes a warrior");

            vent = dir.vents[1];
            hero.Controller.Teleport(vent.transform.position + Vector3.back * 2.5f, 0);
            yield return Wait(0.3f);
            for (int i = 0; i < 100 && vent.open; i++) { vent.Channel(hero, 0.1f); yield return null; }
            yield return null;
            Assert.That(st.Knows(ItemId.ScoutDrone), "the second vent shows how to make the drone");
            st.Matter.Set(10, 10, 30);
            Assert.That(hero.Power.Create(ItemId.ScoutDrone), Is.EqualTo(GameState.CreateResult.Done));
            yield return Until(() => hero.Drone, 3, "the drone flies");

            // the drone fights on its own: an imp comes at the crab, the crab does nothing
            foreach (var e in Enemy.All.ToList()) if (e) Object.Destroy(e.gameObject);
            yield return null;
            var lib = Library.Current;
            var p = hero.transform.position + hero.transform.forward * 9;
            var imp = Object.Instantiate(lib.imp, p, Quaternion.identity).GetComponent<Enemy>();
            yield return Wait(1.5f);
            AimAt(hero, imp.transform.position + Vector3.up * 0.5f);
            yield return Wait(0.3f);
            yield return Shot("wrath-drone");
            yield return Until(() => !imp || !imp.Alive, 20, "the drone kills the imp");
        }

        [UnityTest]
        public IEnumerator ColossusFightFirstPerson()
        {
            var st = WrathState();
            yield return Load("Wrath");
            UiToTexture();
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            Arm(hero, st);
            var dir = Object.FindAnyObjectByType<WrathDirector>();
            foreach (var e in dir.firstImps) if (e) Object.Destroy(e.gameObject);
            yield return Until(() => st.Knows(ItemId.MachineGun) && Story.Objective.StartsWith("Заглушить"), 10, "the vents task");
            foreach (var v in dir.vents) v.Seal();
            yield return Until(() => dir.colossus.awake, 10, "the Colossus wakes");
            var c = dir.colossus;
            hero.Controller.Teleport(c.arena.position + Vector3.back * 6, 0);
            yield return Until(() => c.Fighting && hero.Cam.Mode == CamMode.First && !hero.Controller.frozen, 20, "the first-person fight");
            int shots = 0;
            float before = c.Health01;
            float end = Time.realtimeSinceStartup + 150;
            while (c.Alive && Time.realtimeSinceStartup < end)
            {
                st.Items.Add(ItemId.Ammo, 20);
                var weak = c.Weak;
                if (weak && c.CoresOpen)
                {
                    AimAt(hero, c.CorePoint(weak));
                    yield return null;
                    if (hero.Combat.Fire()) shots++;
                    if (shots == 10) yield return Shot("wrath-colossus");
                }
                else yield return null;
            }
            Assert.That(c.Health01, Is.LessThan(before));
            Assert.That(c.Alive, Is.False, $"three cores broken ({shots} shots)");
            yield return Until(() => hero.Cam.Mode == CamMode.Third, 10, "back to third person");
            yield return Until(() => st.Flag("colossus"), 10, "the victory is kept");
            yield return Until(() => dir.exit.gameObject.activeSelf, 25, "the way on opens");
            AimAt(hero, dir.exit.Point);
            yield return Wait(1);
            yield return Shot("wrath-after");
        }
    }
}
