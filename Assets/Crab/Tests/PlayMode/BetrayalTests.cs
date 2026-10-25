// The sixth circle played in the real scene: following the Guide through the labyrinth (the gates
// and their levers), the legion's workshop and the architect (ENGINEER, MAG, the squad's
// upgrades), the betrayal at the heart (the squad turned against the crab), and the Two-Faced.
// Pictures: Docs/shots/play-betrayal-*.png.
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
        static GameState BetrayalState()
        {
            var st = GameSession.Ensure().State;
            foreach (var f in new[] { "spark", "limbo_intro", "circle1_done", "circle2_done", "circle3_done", "circle4_done", "circle5_done", "betrayal_intro" }) st.SetFlag(f);
            foreach (var id in new[] { ItemId.Pistol, ItemId.Ammo, ItemId.MachineGun, ItemId.EnergyRifle, ItemId.EnergyCell, ItemId.SparkCore, ItemId.EnergyBlade, ItemId.GravityGun, ItemId.Portal, ItemId.CyberFrame }) st.Learn(id);
            st.Items.Add(ItemId.HeavyArmor);   // made in the circle of Greed: no one goes on without it
            return st;
        }

        [UnityTest]
        public IEnumerator FollowTheGuideAndBuildTheSquad()
        {
            var st = BetrayalState();
            yield return Load("Betrayal");
            UiToTexture();
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            var dir = Object.FindAnyObjectByType<BetrayalDirector>();
            var g = dir.guide;
            yield return Until(() => Story.Objective.StartsWith("Идти за Проводником"), 15, "the guide task");
            // walk with the guide: the crab keeps close; levers open the gates; the legion is cut down
            st.Matter.Set(10, 20, 30, 10, 20, 30);
            float end = Time.realtimeSinceStartup + 120;
            bool shot = false;
            while (g.At < dir.workshopStop && Time.realtimeSinceStartup < end)
            {
                var p = g.transform.position - g.transform.forward * 2;
                hero.Controller.Teleport(new Vector3(p.x, g.transform.position.y + 0.2f, p.z), g.transform.eulerAngles.y);
                foreach (var lever in Object.FindObjectsByType<Lever>().Where(l => !l.pulled))
                    if (g.gates.Any(gt => !gt.open && Vector3.Distance(gt.transform.position, g.transform.position) < 9)) lever.Use(hero);
                foreach (var e in Enemy.All.ToList()) if (e && e.Alive && Vector3.Distance(e.transform.position, hero.transform.position) < 20) e.Hit(999, e.transform.position, Vector3.up, hero.gameObject);
                if (!shot && g.At >= 3) { shot = true; hero.Cam.Face(g.transform.eulerAngles.y, 12); yield return Wait(0.3f); yield return Shot("betrayal-maze"); }
                yield return Wait(0.2f);
            }
            Assert.That(g.At, Is.GreaterThanOrEqualTo(dir.workshopStop), "the guide led the crab to the workshop");
            Assert.That(g.gates.Count(gt => gt.open), Is.GreaterThanOrEqualTo(1), "a lever opened a gate");
            yield return Until(() => st.Knows(ItemId.Constructor), 15, "the squad's blueprints");
            var ws = dir.workshop;
            hero.Controller.Teleport(ws.transform.position + ws.transform.forward * 3, 0);
            yield return Wait(0.3f);
            Assert.That(st.AtWorkshop);
            foreach (var id in new[] { ItemId.Constructor, ItemId.Engineer, ItemId.MageOrb, ItemId.Tank, ItemId.SquadFire })
            {
                st.Matter.Set(10, 20, 30, 10, 30, 30);
                Assert.That(hero.Power.Create(id), Is.EqualTo(GameState.CreateResult.Done), id.ToString());
            }
            Assert.That(st.Level, Is.EqualTo(6));
            Assert.That(GameState.LevelTitle(6), Is.EqualTo("Краб-архитектор"));
            yield return Until(() => hero.Engineer && hero.Mage && hero.Tank, 3, "the squad stands");
            Assert.That(Squad.Damage, Is.EqualTo(1.5f));
            // the squad at work: a band of the legion
            hero.Invulnerable = false;
            st.Health.Hurt(40);
            var band = new GameObject("test band").AddComponent<LegionBand>();
            band.transform.SetPositionAndRotation(hero.transform.position + hero.transform.forward * 8, Quaternion.LookRotation(-hero.transform.forward));
            band.auto = false;
            band.ranks = 1;
            band.files = 2;
            band.Call();
            yield return Wait(2);
            hero.Cam.Face(hero.transform.eulerAngles.y, 12);
            yield return Shot("betrayal-squad");
            float hp = st.Health.Value;
            yield return Until(() => band.Beaten, 40, "the squad beats the band");
            hero.Invulnerable = true;
            Assert.That(st.Health.Value, Is.GreaterThan(hp - 1), "the ENGINEER mends the crab");
        }

        [UnityTest]
        public IEnumerator BetrayalAndTheTwoFaced()
        {
            var st = BetrayalState();
            foreach (var f in new[] { "guide_workshop", "architect_learned", "architect" }) st.SetFlag(f);
            yield return Load("Betrayal");
            UiToTexture();
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            var dir = Object.FindAnyObjectByType<BetrayalDirector>();
            st.AtWorkshop = true;
            foreach (var id in new[] { ItemId.Constructor, ItemId.Tank, ItemId.EnergyRifle })
            {
                st.Learn(id);
                st.Matter.Set(10, 20, 30, 10, 30, 30);
                hero.Power.Create(id);
            }
            st.AtWorkshop = false;
            st.Items.Add(ItemId.EnergyCell, 60);
            st.Items.Equip(ItemId.EnergyRifle);
            var g = dir.guide;
            yield return Wait(0.5f);   // after the director has placed the guide for a loaded game
            g.Jump(g.path.Count - 2);
            for (float t = 0; t < 15 && !g.Arrived; t += 0.2f)
            {
                hero.Controller.Teleport(g.transform.position + Vector3.back * 2, 0);   // keep up with it
                yield return Wait(0.2f);
            }
            yield return Until(() => !dir.heartGate.open, 20, "the gate shuts behind");
            yield return Until(() => Squad.Hacked, 10, "the squad is taken over");
            yield return Wait(1);
            yield return Shot("betrayal-betrayed");
            // the trap: cut the legion down
            float end = Time.realtimeSinceStartup + 40;
            while (!dir.janus.awake && Time.realtimeSinceStartup < end)
            {
                foreach (var e in Enemy.All.ToList()) if (e && e.Alive) e.Hit(999, e.transform.position, Vector3.up, hero.gameObject);
                yield return Wait(0.5f);
            }
            Assert.That(dir.janus.awake, "the Guide shows its true face");
            var j = dir.janus;
            hero.Controller.Teleport(j.arena.position + Vector3.back * 10, 0);
            yield return Until(() => j.Fighting && hero.Cam.Mode == CamMode.First && !hero.Controller.frozen, 20, "the first-person fight");
            // the smile feeds it
            for (int i = 0; i < 3; i++)
            {
                st.Items.Add(ItemId.EnergyCell, 10);
                AimAt(hero, j.FrontPoint);
                yield return null;
                hero.Combat.Fire();
                yield return Wait(0.3f);
            }
            Assert.That(j.Health01, Is.EqualTo(1).Within(0.001f), "blows to the smiling face do not hurt it");
            // behind it: the true face
            int shots = 0;
            end = Time.realtimeSinceStartup + 150;
            while (j.Alive && Time.realtimeSinceStartup < end)
            {
                var back = j.Back;
                var away = back.position - j.transform.position;
                away.y = 0;
                var behind = j.transform.position + away.normalized * 9;
                behind.y = j.arena.position.y;
                if (Vector3.Distance(hero.transform.position, behind) > 2) hero.Controller.Teleport(behind, 0);
                st.Items.Add(ItemId.EnergyCell, 10);
                AimAt(hero, j.BackPoint);
                yield return null;
                if (hero.Combat.Fire()) shots++;
                if (shots == 6) yield return Shot("betrayal-janus");
                foreach (var e in Enemy.All.ToList()) if (e && e.Alive && Vector3.Distance(e.transform.position, hero.transform.position) < 4) e.Hit(999, e.transform.position, Vector3.up, hero.gameObject);
            }
            Assert.That(j.Alive, Is.False, $"the true face breaks it ({shots} shots)");
            yield return Until(() => st.Flag("janus"), 10, "the victory is kept");
            yield return Until(() => dir.exit.gameObject.activeSelf, 30, "the way on opens");
            AimAt(hero, dir.exit.Point);
            yield return Wait(1);
            yield return Shot("betrayal-after");
        }
    }
}
