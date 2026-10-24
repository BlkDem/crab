// The seventh circle played in the real scene: orders to the squad, the captured forge, the factory
// and its army, a siege engine broken, and the Warlord whose guard must fall first.
// Pictures: Docs/shots/play-violence-*.png.
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
        static GameState ViolenceState()
        {
            var st = GameSession.Ensure().State;
            foreach (var f in new[] { "spark", "limbo_intro", "circle1_done", "circle2_done", "circle3_done", "circle4_done", "circle5_done", "circle6_done", "violence_intro" }) st.SetFlag(f);
            foreach (var id in new[] { ItemId.Pistol, ItemId.Ammo, ItemId.MachineGun, ItemId.EnergyRifle, ItemId.EnergyCell, ItemId.SparkCore, ItemId.GravityGun, ItemId.CyberFrame, ItemId.Constructor, ItemId.Tank, ItemId.Engineer, ItemId.MageOrb, ItemId.Hunter }) st.Learn(id);
            return st;
        }

        [UnityTest]
        public IEnumerator ForgeFactoryAndTheArmy()
        {
            var st = ViolenceState();
            yield return Load("Violence");
            UiToTexture();
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            var dir = Object.FindAnyObjectByType<ViolenceDirector>();
            Assert.That(hero.GetComponent<Commander>(), Is.Not.Null, "the crab commands");
            yield return Until(() => Story.Objective.StartsWith("Захватить кузницу"), 15, "the forge task");
            foreach (var e in Enemy.All.ToList()) if (e) Object.Destroy(e.gameObject);
            var forge = dir.forge;
            hero.Controller.Teleport(forge.transform.position + forge.transform.forward * 4, forge.transform.eulerAngles.y + 180);
            yield return Wait(0.3f);
            st.Matter.Set(10, 20, 30, 10, 20, 30);
            for (int i = 0; i < 100 && !forge.built; i++) { forge.Channel(hero, 0.1f); yield return null; }
            Assert.That(forge.built);
            yield return Until(() => st.Knows(ItemId.Factory), 5, "the factory blueprint");
            yield return Wait(0.3f);
            st.Matter.Set(10, 20, 30, 10, 20, 30);
            Assert.That(hero.Power.Create(ItemId.Factory), Is.EqualTo(GameState.CreateResult.Done));
            Assert.That(st.Level, Is.EqualTo(7));
            st.Matter.Set(10, 20, 30, 10, 20, 30);
            hero.Power.Create(ItemId.Tank);
            yield return Until(() => dir.factory.running, 10, "the factory runs");
            dir.factory.every = 0.5f;
            yield return Until(() => BattleBot.All.Count >= 4, 15, "the army comes out");
            yield return Wait(2);
            hero.Cam.Face(hero.transform.eulerAngles.y + 160, 15);
            yield return Wait(0.4f);
            yield return Shot("violence-army");
            // an order: attack the catapult's guard, then the catapult
            var cat = dir.engines.First(e => e.kind == SiegeEngine.Kind.Catapult);
            hero.Controller.Teleport(cat.transform.position - cat.transform.forward * 18, cat.transform.eulerAngles.y);
            yield return Wait(1);
            AimAt(hero, cat.transform.position + Vector3.up);
            yield return null;
            hero.GetComponent<Commander>().Give(Squad.Orders.Attack);
            Assert.That(Squad.Order, Is.EqualTo(Squad.Orders.Attack));
            st.Items.Add(ItemId.EnergyCell, 60);
            st.AtWorkshop = true;
            st.Matter.Set(10, 20, 30, 10, 20, 30);
            hero.Power.Create(ItemId.EnergyRifle);
            st.AtWorkshop = false;
            float end = Time.realtimeSinceStartup + 40;
            while (cat.Alive && Time.realtimeSinceStartup < end)
            {
                st.Items.Add(ItemId.EnergyCell, 10);
                AimAt(hero, cat.transform.position + Vector3.up * 2);
                yield return null;
                hero.Combat.Fire();
                yield return Wait(0.25f);
            }
            Assert.That(cat.Alive, Is.False, "the catapult breaks");
            hero.GetComponent<Commander>().Give(Squad.Orders.Follow);
            Assert.That(Squad.Order, Is.EqualTo(Squad.Orders.Follow));
        }

        [UnityTest]
        public IEnumerator WarlordAndHisGuard()
        {
            var st = ViolenceState();
            foreach (var f in new[] { "forge", "general" }) st.SetFlag(f);
            st.Learn(ItemId.Factory);
            yield return Load("Violence");
            UiToTexture();
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            var dir = Object.FindAnyObjectByType<ViolenceDirector>();
            st.AtWorkshop = true;
            foreach (var id in new[] { ItemId.Factory, ItemId.Tank, ItemId.EnergyRifle })
            {
                st.Matter.Set(10, 20, 30, 10, 20, 30);
                hero.Power.Create(id);
            }
            st.AtWorkshop = false;
            st.Items.Add(ItemId.EnergyCell, 60);
            foreach (var e in dir.engines) e.Hit(9999, e.transform.position, Vector3.down, hero.gameObject);
            yield return Until(() => dir.warlord.awake, 20, "the Warlord comes");
            foreach (var e in Enemy.All.ToList()) if (e) Object.Destroy(e.gameObject);
            var w = dir.warlord;
            hero.Controller.Teleport(w.arena.position + Vector3.back * 12, 0);
            yield return Until(() => w.Fighting && hero.Cam.Mode == CamMode.First && !hero.Controller.frozen, 25, "the first-person fight");
            Assert.That(w.Guarded, "the guard stands round him");
            float before = w.Health01;
            for (int i = 0; i < 4; i++)
            {
                AimAt(hero, w.AimPoint);
                yield return null;
                hero.Combat.Fire();
                yield return Wait(0.3f);
            }
            Assert.That(w.Health01, Is.EqualTo(before), "the aura turns the shots");
            yield return Shot("violence-guard");
            int shots = 0;
            float end = Time.realtimeSinceStartup + 150;
            while (w.Alive && Time.realtimeSinceStartup < end)
            {
                // the squad and the crab cut down the guard; then the crab strikes the Warlord
                var g = w.Guard.FirstOrDefault(e => e && e.Alive);
                st.Items.Add(ItemId.EnergyCell, 10);
                if (g)
                {
                    AimAt(hero, g.transform.position + Vector3.up);
                    yield return null;
                    hero.GetComponent<Commander>().Give(Squad.Orders.Attack);
                    g.Hit(60, g.transform.position + Vector3.up, Vector3.forward, hero.gameObject);
                    yield return Wait(0.2f);
                    continue;
                }
                AimAt(hero, w.AimPoint);
                yield return null;
                if (hero.Combat.Fire()) shots++;
                if (shots == 6) yield return Shot("violence-warlord");
            }
            Assert.That(w.Alive, Is.False, $"the Warlord falls ({shots} shots)");
            yield return Until(() => st.Flag("warlord"), 10, "the victory is kept");
            yield return Until(() => dir.exit.gameObject.activeSelf, 30, "the way on opens");
        }
    }
}
