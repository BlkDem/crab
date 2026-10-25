// The fourth circle played in the real scene: talking to the dwellers, the Spring and the
// crab-mage (matter out of energy, the blade, the gravity gun, the portal), envy turning the
// dwellers, the illusion slipping, and the Reflection of the Crab. Pictures: Docs/shots/play-envy-*.png.
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
        static GameState EnvyState()
        {
            var st = GameSession.Ensure().State;
            foreach (var f in new[] { "spark", "limbo_intro", "circle1_done", "circle2_done", "circle3_done", "envy_intro" }) st.SetFlag(f);
            foreach (var id in new[] { ItemId.Pistol, ItemId.Ammo, ItemId.MachineGun, ItemId.EnergyRifle, ItemId.EnergyCell }) st.Learn(id);
            st.Items.Add(ItemId.HeavyArmor);   // made in the circle of Greed: no one goes on without it
            return st;
        }

        [UnityTest]
        public IEnumerator DwellersSpringAndTheMage()
        {
            var st = EnvyState();
            yield return Load("Envy");
            UiToTexture();
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            var dir = Object.FindAnyObjectByType<EnvyDirector>();
            yield return Until(() => Story.Objective.StartsWith("Поговорить"), 15, "the dwellers task");
            var d0 = dir.dwellers[0];
            hero.Controller.Teleport(d0.transform.position + Vector3.back * 2.2f, 0);
            yield return Wait(0.6f);
            yield return Shot("envy-village");
            foreach (var d in dir.dwellers.Take(3)) d.Use(hero);
            yield return Until(() => Story.Objective.StartsWith("Найти Источник"), 10, "the Spring task");
            Assert.That(dir.glitch.Count, Is.GreaterThan(0), "paradise slipped once");
            var s = dir.spring;
            hero.Controller.Teleport(s.transform.position + Vector3.back * 3.5f, 0);
            yield return Wait(0.3f);
            for (int i = 0; i < 60 && !s.touched; i++) { s.Channel(hero, 0.1f); yield return null; }
            Assert.That(st.Knows(ItemId.SparkCore) && st.Knows(ItemId.EnergyBlade) && st.Knows(ItemId.GravityGun) && st.Knows(ItemId.Portal));
            // matter out of energy: no metal at all, energy pays for it
            st.Matter.Set(0, 0, 0, 0, 20, 30);
            Assert.That(hero.Power.Create(ItemId.SparkCore), Is.EqualTo(GameState.CreateResult.Done));
            Assert.That(st.Level, Is.EqualTo(4));
            Assert.That(GameState.LevelTitle(4), Is.EqualTo("Краб-маг"));
            int energy = st.Matter[Matter.Energy];
            Assert.That(hero.Power.Create(ItemId.GravityGun), Is.EqualTo(GameState.CreateResult.Done), "the gun's metal and stone come out of energy");
            Assert.That(st.Matter[Matter.Energy], Is.EqualTo(energy - 4 - 8));
            st.Matter.Set(0, 0, 0, 0, 20, 30);
            Assert.That(hero.Power.Create(ItemId.EnergyBlade), Is.EqualTo(GameState.CreateResult.Done));
            Assert.That(hero.Power.Create(ItemId.Portal), Is.EqualTo(GameState.CreateResult.Done));
            yield return null;
            Assert.That(hero.Portal, Is.Not.Null);
            var before = hero.transform.position;
            hero.Cam.Face(180, 10);   // away from the basin
            yield return null;
            Assert.That(hero.Portal.Step(), "a step through space");
            Assert.That(Vector3.Distance(before, hero.transform.position), Is.GreaterThan(1.5f));
            // envy takes the dwellers
            yield return Until(() => Enemy.All.OfType<Envious>().Count() >= 4, 15, "the dwellers turn");
            yield return Until(() => st.Flag("turned"), 5, "envy is kept");
            st.Items.Equip(ItemId.EnergyBlade);
            var envy = Enemy.All.OfType<Envious>().First();
            hero.Controller.Teleport(envy.transform.position + Vector3.back * 1.2f, 0);
            yield return Wait(0.3f);
            yield return Shot("envy-turned");
            float end = Time.realtimeSinceStartup + 10;
            while (envy && envy.Alive && Time.realtimeSinceStartup < end)
            {
                var to = envy.transform.position - hero.transform.position;
                hero.Controller.Teleport(envy.transform.position - new Vector3(to.x, 0, to.z).normalized * 1.0f, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
                hero.Cam.Face(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 10);   // a strike turns the crab to the view
                hero.Combat.Fire();
                yield return Wait(0.2f);
            }
            Assert.That(!envy || !envy.Alive, "the energy blade cuts envy down");
        }

        [UnityTest]
        public IEnumerator ReflectionFightFirstPerson()
        {
            var st = EnvyState();
            foreach (var f in new[] { "spring", "turned", "envy_cleared" }) st.SetFlag(f);
            foreach (var id in new[] { ItemId.SparkCore, ItemId.EnergyBlade, ItemId.GravityGun, ItemId.Portal }) st.Learn(id);
            yield return Load("Envy");
            UiToTexture();
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            var dir = Object.FindAnyObjectByType<EnvyDirector>();
            st.Matter.Set(10, 10, 20, 10, 20, 30);
            Assert.That(hero.Power.Create(ItemId.SparkCore), Is.EqualTo(GameState.CreateResult.Done));
            st.AtWorkshop = true;   // the rifle was made back in the circle of Greed
            Assert.That(hero.Power.Create(ItemId.EnergyRifle), Is.EqualTo(GameState.CreateResult.Done));
            st.AtWorkshop = false;
            st.Items.Add(ItemId.EnergyCell, 60);
            yield return Until(() => dir.reflection.awake, 15, "the Reflection waits");
            var m = dir.reflection;
            hero.Controller.Teleport(m.arena.position + Vector3.back * 8, 0);
            yield return Until(() => m.Fighting && hero.Cam.Mode == CamMode.First && !hero.Controller.frozen, 20, "the first-person fight");
            // a mirror image: the crab steps right, the Reflection steps left
            var c = m.arena.position;
            hero.Controller.Teleport(c + new Vector3(5, 0, -6), 0);
            yield return Wait(1.5f);
            var mp = m.transform.position - c;
            Assert.That(mp.x, Is.LessThan(-1), "the Reflection mirrors the crab");
            // the shine sends shots back
            int shots = 0, reflected = 0;
            float end = Time.realtimeSinceStartup + 120;
            hero.Invulnerable = false;
            while (m.Alive && Time.realtimeSinceStartup < end)
            {
                st.Items.Add(ItemId.EnergyCell, 10);
                st.Health.Heal(100);
                // as a player would: the Envious ones it calls first
                var add = Enemy.All.FirstOrDefault(e => e && e.Alive && e is Envious);
                if (add)
                {
                    AimAt(hero, add.transform.position + Vector3.up);
                    yield return null;
                    hero.Combat.Fire();
                    continue;
                }
                AimAt(hero, m.AimPoint);
                yield return null;
                if (m.Reflecting)
                {
                    if (reflected == 0)
                    {
                        float hp = st.Health.Value;
                        hero.Combat.Fire();
                        yield return null;
                        if (st.Health.Value < hp) reflected++;
                    }
                    continue;
                }
                if (hero.Combat.Fire()) shots++;
                if (shots == 6) yield return Shot("envy-reflection");
            }
            hero.Invulnerable = true;
            Assert.That(reflected, Is.EqualTo(1), "a shot into the shine came back");
            Assert.That(m.Alive, Is.False, $"the Reflection shatters ({shots} shots)");
            yield return Until(() => st.Flag("reflection"), 10, "the victory is kept");
            yield return Until(() => dir.exit.gameObject.activeSelf, 25, "the way on opens");
            AimAt(hero, dir.exit.Point);
            yield return Wait(1);
            yield return Shot("envy-after");
        }
    }
}
