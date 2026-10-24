// The third circle played in the real scene: building the workshop and making the heavy armour
// there (level 3), the energy rifle against a gold guard's shield, the shield dome, the gold
// door, the Usurer King, and the TANK after him. Pictures go to Docs/shots/play-greed-*.png.
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
        static GameState GreedState()
        {
            var st = GameSession.Ensure().State;
            foreach (var f in new[] { "spark", "limbo_intro", "circle1_done", "wrath_intro", "circle2_done", "greed_intro", "city" }) st.SetFlag(f);
            foreach (var id in new[] { ItemId.Pistol, ItemId.Ammo, ItemId.MachineGun, ItemId.ScrapArmor, ItemId.ScoutDrone }) st.Learn(id);
            return st;
        }

        static IEnumerator BuildWorkshop(Hero hero, GreedDirector dir, GameState st)
        {
            var ws = dir.workshop;
            hero.Controller.Teleport(ws.transform.position + ws.transform.forward * 3.5f, ws.transform.eulerAngles.y + 180);
            yield return Wait(0.3f);
            st.Matter.Set(10, 20, 30, 20, 20, 20);
            for (int i = 0; i < 100 && !ws.built; i++) { ws.Channel(hero, 0.1f); yield return null; }
            Assert.That(ws.built, "the workshop stands");
            yield return null;
        }

        [UnityTest]
        public IEnumerator WorkshopMakesTheArmouredCrab()
        {
            var st = GreedState();
            yield return Load("Greed");
            UiToTexture();
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            var dir = Object.FindAnyObjectByType<GreedDirector>();
            yield return Until(() => Story.Objective.Contains("мастерскую"), 15, "the workshop task");
            // away from the workshop the complex things cannot be made
            st.Learn(ItemId.HeavyArmor);
            st.Matter.Set(10, 20, 30, 20, 20, 20);
            Assert.That(hero.Power.Create(ItemId.HeavyArmor), Is.EqualTo(GameState.CreateResult.NeedWorkshop));
            yield return BuildWorkshop(hero, dir, st);
            Assert.That(st.Flag("workshop"));
            Assert.That(st.Knows(ItemId.EnergyRifle) && st.Knows(ItemId.EnergyShield));
            yield return Wait(0.2f);
            Assert.That(st.AtWorkshop, "standing by it counts");
            st.Matter.Set(10, 20, 30, 20, 20, 20);
            Assert.That(hero.Power.Create(ItemId.HeavyArmor), Is.EqualTo(GameState.CreateResult.Done));
            Assert.That(st.Level, Is.EqualTo(3), "heavy armour makes the armoured crab");
            Assert.That(hero.Armor, Is.LessThan(0.5f));
            Assert.That(hero.Power.Create(ItemId.EnergyShield), Is.EqualTo(GameState.CreateResult.Done));
            yield return null;
            Assert.That(hero.Shield, Is.Not.Null);
            hero.Invulnerable = false;
            Assert.That(hero.Shield.Raise());
            float hp = st.Health.Value;
            hero.Hurt(30, hero.transform.position, Vector3.forward);
            Assert.That(st.Health.Value, Is.EqualTo(hp), "the dome stops the blow");
            yield return Wait(0.5f);
            hero.Cam.Face(hero.transform.eulerAngles.y + 180, 20);
            yield return Wait(0.4f);
            yield return Shot("greed-workshop");
            hero.Invulnerable = true;
            yield return Until(() => Story.Objective.Contains("золотую дверь"), 10, "the vault task");
        }

        [UnityTest]
        public IEnumerator EnergyGoesThroughTheGoldenShield()
        {
            var st = GreedState();
            yield return Load("Greed");
            UiToTexture();
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            var dir = Object.FindAnyObjectByType<GreedDirector>();
            yield return BuildWorkshop(hero, dir, st);
            st.Matter.Set(10, 20, 30, 20, 20, 20);
            st.Learn(ItemId.MachineGun);
            hero.Power.Create(ItemId.MachineGun);
            hero.Power.Create(ItemId.EnergyRifle);
            hero.Power.Create(ItemId.EnergyCell);
            st.Items.Add(ItemId.Ammo, 120);
            foreach (var e in Enemy.All.ToList()) if (e) Object.Destroy(e.gameObject);
            yield return null;
            // a guard facing the crab, frozen in place for a fair test
            var p = hero.transform.position + hero.transform.forward * 8;
            var guard = Object.Instantiate(Library.Current.goldGuard, p, Quaternion.LookRotation(-hero.transform.forward)).GetComponent<GoldGuard>();
            guard.aggro = 0;
            guard.speed = 0;
            yield return Wait(0.3f);
            guard.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(hero.transform.position - guard.transform.position, Vector3.up));
            Assert.That(guard.Blocks(guard.transform.position - hero.transform.position), "bullets from the front meet the shield");
            Hits.Energy = true;
            Assert.That(guard.Blocks(guard.transform.position - hero.transform.position), Is.False, "energy goes through");
            Hits.Energy = false;
            st.Items.Equip(ItemId.EnergyRifle);
            int shots = 0;
            float end = Time.realtimeSinceStartup + 10;
            while (guard && guard.Alive && Time.realtimeSinceStartup < end)
            {
                st.Items.Add(ItemId.EnergyCell, 10);
                AimAt(hero, guard.transform.position + Vector3.up * 1.4f);
                yield return null;
                if (hero.Combat.Fire()) shots++;
                if (shots == 2) yield return Shot("greed-rifle");
            }
            Assert.That(!guard || !guard.Alive, $"the energy rifle fells a gold guard ({shots} shots)");
            Assert.That(shots, Is.LessThan(8));
        }

        [UnityTest]
        public IEnumerator UsurerFightFirstPerson()
        {
            var st = GreedState();
            yield return Load("Greed");
            UiToTexture();
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            var dir = Object.FindAnyObjectByType<GreedDirector>();
            yield return BuildWorkshop(hero, dir, st);
            st.Matter.Set(10, 20, 30, 20, 20, 20);
            hero.Power.Create(ItemId.HeavyArmor);
            st.Matter.Set(10, 20, 30, 20, 20, 20);
            hero.Power.Create(ItemId.EnergyRifle);
            foreach (var e in Enemy.All.ToList()) if (e) Object.Destroy(e.gameObject);
            // the gold door takes its price
            var vault = dir.vault;
            hero.Controller.Teleport(vault.transform.position + Vector3.back * 3, 0);
            yield return Wait(0.3f);
            st.Matter.Set(0, 0, 0, 20);
            vault.Use(hero);
            Assert.That(vault.opened, "fifteen gold opens the door");
            Assert.That(st.Matter[Matter.Gold], Is.EqualTo(5));
            yield return Until(() => dir.usurer.awake, 10, "the Usurer wakes");
            var u = dir.usurer;
            hero.Controller.Teleport(u.arena.position + Vector3.back * 4, 0);
            yield return Until(() => u.Fighting && hero.Cam.Mode == CamMode.First && !hero.Controller.frozen, 20, "the first-person fight");
            Assert.That(u.Shielded, "the dome stands while the hoards hold gold");
            // shots at the gem bounce off the dome
            float before = u.Health01;
            for (int i = 0; i < 5; i++)
            {
                st.Items.Add(ItemId.EnergyCell, 10);
                AimAt(hero, u.GemPoint);
                yield return null;
                hero.Combat.Fire();
                yield return Wait(0.3f);
            }
            Assert.That(u.Health01, Is.EqualTo(before), "the dome turns the shots");
            yield return Shot("greed-dome");
            // drain the hoards
            foreach (var h in u.hoards)
            {
                hero.Controller.Teleport(h.transform.position + Vector3.back * 2, 0);
                yield return null;
                for (int i = 0; i < 200 && h.Left > 0; i++)
                {
                    st.Matter.Remove(Matter.Gold, 30);
                    h.Channel(hero, 0.2f);
                    yield return null;
                }
            }
            Assert.That(u.Shielded, Is.False, "empty hoards — no dome");
            hero.Controller.Teleport(u.arena.position, 0);
            int shots = 0;
            float end = Time.realtimeSinceStartup + 120;
            while (u.Alive && Time.realtimeSinceStartup < end)
            {
                // halfway he refills them: drain again
                foreach (var h in u.hoards)
                    while (h.Left > 0) { st.Matter.Remove(Matter.Gold, 30); h.Channel(hero, 0.2f); }
                st.Items.Add(ItemId.EnergyCell, 10);
                AimAt(hero, u.GemPoint);
                yield return null;
                if (hero.Combat.Fire()) shots++;
                if (shots == 8) yield return Shot("greed-usurer");
            }
            Assert.That(u.Alive, Is.False, $"the gem breaks him ({shots} shots)");
            yield return Until(() => hero.Cam.Mode == CamMode.Third, 10, "back to third person");
            yield return Until(() => st.Flag("usurer"), 10, "the victory is kept");
            yield return Until(() => st.Knows(ItemId.Tank), 20, "complex mechanisms: the TANK");
            yield return Until(() => dir.exit.gameObject.activeSelf, 20, "the way on opens");
            // the TANK, made at the workshop
            hero.Controller.Teleport(dir.workshop.transform.position + dir.workshop.transform.forward * 3.5f, 0);
            yield return Wait(0.3f);
            st.Matter.Set(10, 20, 30, 20, 20, 20);
            Assert.That(hero.Power.Create(ItemId.Tank), Is.EqualTo(GameState.CreateResult.Done));
            yield return Until(() => hero.Tank, 3, "the TANK walks");
            var imp = Object.Instantiate(Library.Current.imp, hero.transform.position + hero.transform.forward * 6, Quaternion.identity).GetComponent<Enemy>();
            yield return Wait(1);
            AimAt(hero, hero.Tank.transform.position + Vector3.up);
            yield return Shot("greed-tank");
            yield return Until(() => !imp || !imp.Alive, 25, "the TANK pounds the imp");
        }
    }
}
