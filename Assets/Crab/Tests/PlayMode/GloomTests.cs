// The fifth circle played in the real scene: the will draining in the dark and coming back at a
// lamp, a wraith's touch, the shards of memory, the ancient machine and its AI, the cyber-crab
// and HUNTER, and the Devourer of Light. Pictures: Docs/shots/play-gloom-*.png.
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
        static GameState GloomState()
        {
            var st = GameSession.Ensure().State;
            foreach (var f in new[] { "spark", "limbo_intro", "circle1_done", "circle2_done", "circle3_done", "circle4_done", "gloom_intro" }) st.SetFlag(f);
            foreach (var id in new[] { ItemId.Pistol, ItemId.Ammo, ItemId.MachineGun, ItemId.EnergyRifle, ItemId.EnergyCell, ItemId.SparkCore, ItemId.EnergyBlade, ItemId.GravityGun }) st.Learn(id);
            st.Items.Add(ItemId.HeavyArmor);   // made in the circle of Greed: no one goes on without it
            return st;
        }

        [UnityTest]
        public IEnumerator DarkMemoriesAndTheCyberCrab()
        {
            var st = GloomState();
            yield return Load("Gloom");
            UiToTexture();
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            var dir = Object.FindAnyObjectByType<GloomDirector>();
            var d = Despair.Instance;
            Assert.That(d, Is.Not.Null);
            foreach (var e in Enemy.All.ToList()) if (e) Object.Destroy(e.gameObject);
            // the dark drains the will
            hero.Controller.Teleport(new Vector3(-2, 0, -14) + Vector3.up * 2, 0);
            float w0 = d.Will;
            yield return Wait(2.5f);
            Assert.That(d.Will, Is.LessThan(w0), "the dark drains the will");
            yield return Shot("gloom-dark");
            // a lamp brings it back
            var lamp = dir.lamps[0];
            hero.Controller.Teleport(lamp.transform.position + Vector3.back * 2, 0);
            yield return Wait(0.3f);
            lamp.Use(hero);
            Assert.That(lamp.lit);
            yield return Wait(0.5f);
            Assert.That(d.Will, Is.GreaterThan(95), "at the lamp the will comes back");
            // a wraith's touch: little harm, much despair
            var wr = Object.Instantiate(Library.Current.wraith, hero.transform.position + hero.transform.right * 2, Quaternion.identity).GetComponent<Enemy>();   // aside: ahead stands the lamp
            hero.Invulnerable = false;
            float will = d.Will;
            yield return Until(() => d.Will < will - 10 || !wr, 15, "the wraith's touch");
            hero.Invulnerable = true;
            Object.Destroy(wr.gameObject);
            // the shards of memory
            foreach (var s in dir.shards)
            {
                hero.Controller.Teleport(s.transform.position + s.transform.forward * 2, s.transform.eulerAngles.y + 180);
                yield return Wait(0.2f);
                s.Use(hero);
                if (s == dir.shards[0]) { yield return Wait(2); yield return Shot("gloom-vision"); }
                yield return Until(() => !hero.Controller.frozen, 30, "the vision ends");
            }
            yield return Until(() => Story.Objective.StartsWith("Найти машину"), 25, "the machine task");
            var m = dir.machine;
            hero.Controller.Teleport(m.transform.position + m.transform.forward * 4, m.transform.eulerAngles.y + 180);
            yield return Wait(0.3f);
            m.Use(hero);
            yield return Until(() => m.awakened && !hero.Controller.frozen, 40, "the AI wakes");
            Assert.That(st.Knows(ItemId.CyberFrame) && st.Knows(ItemId.Hunter));
            yield return Wait(0.2f);
            Assert.That(st.AtWorkshop, "the machine is the forge down here");
            st.Matter.Set(10, 20, 30, 10, 20, 20);
            Assert.That(hero.Power.Create(ItemId.CyberFrame), Is.EqualTo(GameState.CreateResult.Done));
            Assert.That(st.Level, Is.EqualTo(5));
            st.Matter.Set(10, 20, 30, 10, 20, 20);
            Assert.That(hero.Power.Create(ItemId.Hunter), Is.EqualTo(GameState.CreateResult.Done));
            yield return Until(() => hero.Hunter, 3, "HUNTER flies");
            var target = Object.Instantiate(Library.Current.wraith, hero.transform.position + hero.transform.forward * 12, Quaternion.identity).GetComponent<Enemy>();
            target.aggro = 0;
            yield return Wait(1);
            hero.Cam.Face(hero.transform.eulerAngles.y + 160, 15);
            yield return Wait(0.4f);
            yield return Shot("gloom-cyber");
            yield return Until(() => !target || !target.Alive, 20, "HUNTER picks off a wraith the scanner shows");
            yield return Until(() => dir.devourer.awake, 15, "the Devourer waits");
        }

        [UnityTest]
        public IEnumerator DevourerFightFirstPerson()
        {
            var st = GloomState();
            foreach (var f in new[] { "memory_knight", "memory_bird", "memory_machine", "machine", "cyber" }) st.SetFlag(f);
            st.Learn(ItemId.CyberFrame);
            yield return Load("Gloom");
            UiToTexture();
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            var dir = Object.FindAnyObjectByType<GloomDirector>();
            foreach (var e in Enemy.All.ToList()) if (e) Object.Destroy(e.gameObject);
            st.AtWorkshop = true;
            st.Matter.Set(10, 20, 30, 10, 20, 30);
            hero.Power.Create(ItemId.SparkCore);
            hero.Power.Create(ItemId.CyberFrame);
            hero.Power.Create(ItemId.EnergyRifle);
            st.AtWorkshop = false;
            st.Items.Add(ItemId.EnergyCell, 60);
            yield return Until(() => dir.devourer.awake, 15, "the Devourer waits");
            var dv = dir.devourer;
            hero.Controller.Teleport(dv.arena.position + Vector3.back * 6, 0);
            yield return Until(() => dv.Fighting && hero.Cam.Mode == CamMode.First && !hero.Controller.frozen, 20, "the first-person fight");
            // in the dark the heart cannot be hurt
            float before = dv.Health01;
            for (int i = 0; i < 4; i++)
            {
                AimAt(hero, dv.HeartPoint);
                yield return null;
                hero.Combat.Fire();
                yield return Wait(0.3f);
            }
            Assert.That(dv.Health01, Is.EqualTo(before), "the dark swallows the blows");
            int shots = 0;
            float end = Time.realtimeSinceStartup + 150;
            while (dv.Alive && Time.realtimeSinceStartup < end)
            {
                // light what is out, then shoot
                foreach (var l in dv.lamps.Where(l => !l.lit).ToList())
                    for (int k = 0; k < 40 && !l.lit; k++) l.Channel(hero, 0.1f);
                st.Items.Add(ItemId.EnergyCell, 10);
                AimAt(hero, dv.HeartPoint);
                yield return null;
                if (hero.Combat.Fire()) shots++;
                if (shots == 8) yield return Shot("gloom-devourer");
            }
            Assert.That(dv.Alive, Is.False, $"light and shots break it ({shots} shots)");
            yield return Until(() => st.Flag("devourer"), 10, "the victory is kept");
            yield return Until(() => dir.exit.gameObject.activeSelf, 25, "the way on opens");
            AimAt(hero, dir.exit.Point);
            yield return Wait(1);
            yield return Shot("gloom-after");
        }
    }
}
