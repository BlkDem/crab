// The ninth circle and the end played in the real scenes: the army behind the crab, the Lord of
// Matter and its storm, the gate where the crab goes on alone, the four pillars, the Lord of Hell
// falling, the portal behind the throne — and the epilogue before the Earth.
// Pictures: Docs/shots/play-power-*.jpg, play-epilogue-*.jpg.
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
        static GameState PowerState()
        {
            var st = GameSession.Ensure().State;
            foreach (var f in new[] { "spark", "limbo_intro", "circle1_done", "circle2_done", "circle3_done", "circle4_done", "circle5_done", "circle6_done", "circle7_done", "circle8_done" }) st.SetFlag(f);
            foreach (var id in new[] { ItemId.Pistol, ItemId.Ammo, ItemId.EnergyRifle, ItemId.EnergyCell, ItemId.SparkCore, ItemId.CyberFrame, ItemId.Constructor, ItemId.Factory, ItemId.TruthLens, ItemId.Bridgework }) st.Learn(id);
            return st;
        }

        [UnityTest]
        public IEnumerator ArmyLordOfMatterAndTheGate()
        {
            var st = PowerState();
            yield return Load("Power");
            UiToTexture();
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            var dir = Object.FindAnyObjectByType<PowerDirector>();
            yield return Until(() => st.Knows(ItemId.MatterLord), 40, "the last blueprint");
            Assert.That(hero.Tank && hero.Engineer && hero.Mage && hero.Hunter && hero.Drone, "the whole squad walks with the crab");
            Assert.That(BattleBot.All.Count, Is.GreaterThanOrEqualTo(dir.army), "and an army");
            st.Matter.Set(10, 20, 30, 20, 20, 30);
            Assert.That(hero.Power.Create(ItemId.MatterLord), Is.EqualTo(GameState.CreateResult.Done));
            Assert.That(st.Level, Is.EqualTo(9));
            yield return Until(() => hero.Lord, 3, "the Lord of Matter");
            Assert.That(hero.Controller.canGlide, "wings of light");
            hero.Cam.Face(160, 10);
            yield return Wait(1);
            yield return Shot("power-army");
            Assert.That(hero.Lord.Storm(), "the storm of matter");
            Assert.That(hero.Lord.Storm(), Is.False, "and it needs to recharge");
            // the bridge to the gate: the army stays, the crab goes alone
            hero.Controller.Teleport(dir.gate.position - dir.gate.forward * 4 + Vector3.up * 0.2f, 0);
            yield return Until(() => st.Flag("alone"), 10, "at the gate");
            yield return Until(() => dir.lord.awake, 15, "the throne waits");
            Assert.That(!hero.Tank.gameObject.activeSelf && !hero.Mage.gameObject.activeSelf, "the squad stays behind");
            Assert.That(BattleBot.All.All(b => !b || !b.gameObject.activeSelf), "and the army");
        }

        [UnityTest]
        public IEnumerator LordOfHellFight()
        {
            var st = PowerState();
            foreach (var f in new[] { "power_intro", "matter_lord", "alone" }) st.SetFlag(f);
            st.Learn(ItemId.MatterLord);
            yield return Load("Power");
            UiToTexture();
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            var dir = Object.FindAnyObjectByType<PowerDirector>();
            st.AtWorkshop = true;
            foreach (var id in new[] { ItemId.MatterLord, ItemId.EnergyRifle })
            {
                st.Matter.Set(10, 20, 30, 20, 20, 30);
                hero.Power.Create(id);
            }
            st.AtWorkshop = false;
            st.Items.Add(ItemId.EnergyCell, 60);
            var lord = dir.lord;
            yield return Until(() => lord.awake, 10, "the throne waits");
            foreach (var e in Enemy.All.ToList()) if (e) Object.Destroy(e.gameObject);
            hero.Controller.Teleport(lord.arena.position + Vector3.back * 12, 0);
            yield return Until(() => lord.Fighting && hero.Cam.Mode == CamMode.First && !hero.Controller.frozen, 30, "the first-person fight");
            Assert.That(lord.Shielded, "the pillars shield him");
            float before = lord.Health01;
            AimAt(hero, lord.AimPoint);
            yield return null;
            hero.Combat.Fire();
            yield return Wait(0.3f);
            Assert.That(lord.Health01, Is.EqualTo(before), "the shield holds");
            yield return Shot("power-pillars");
            // break the pillars with the rifle
            int shots = 0;
            float end = Time.realtimeSinceStartup + 120;
            foreach (var p in lord.pillars)
                while (p.Alive && Time.realtimeSinceStartup < end)
                {
                    st.Items.Add(ItemId.EnergyCell, 10);
                    AimAt(hero, p.transform.position + Vector3.up * 4);
                    yield return null;
                    if (hero.Combat.Fire()) shots++;
                }
            Assert.That(lord.Shielded, Is.False, $"the pillars fall ({shots} shots)");
            end = Time.realtimeSinceStartup + 150;
            bool pictured = false;
            while (lord.Alive && Time.realtimeSinceStartup < end)
            {
                st.Items.Add(ItemId.EnergyCell, 10);
                if (hero.Lord && hero.Lord.Cooldown <= 0) hero.Lord.Storm();
                AimAt(hero, lord.AimPoint);
                yield return null;
                if (hero.Combat.Fire()) shots++;
                if (!pictured && lord.Health01 < 0.7f) { pictured = true; yield return Shot("power-lord"); }
            }
            Assert.That(lord.Alive, Is.False, $"the Lord of Hell falls ({shots} shots)");
            yield return Until(() => st.Flag("lord"), 15, "the victory is kept");
            yield return Until(() => dir.portal.gameObject.activeSelf, 30, "the portal behind the throne");
            yield return Shot("power-portal");
            Assert.That(dir.portal.leadsTo, Is.EqualTo("Epilogue"));
        }

        [UnityTest]
        public IEnumerator Epilogue()
        {
            var st = PowerState();
            foreach (var f in new[] { "lord", "circle9_done" }) st.SetFlag(f);
            yield return Load("Epilogue");
            UiToTexture();
            var dir = Object.FindAnyObjectByType<EpilogueDirector>();
            yield return Wait(4);
            yield return Shot("epilogue-earth");
            bool no = false;
            System.Action<string, float> said = (t, _) => no |= t == "Нет.";
            Story.OnThought += said;
            Time.timeScale = 4;
            try
            {
                float end = Time.realtimeSinceStartup + 40;
                bool pictured = false;
                while (!dir.done && Time.realtimeSinceStartup < end)
                {
                    if (!pictured && no) { pictured = true; Time.timeScale = 1; yield return Shot("epilogue-no"); Time.timeScale = 4; }
                    yield return null;
                }
            }
            finally { Time.timeScale = 1; Story.OnThought -= said; }
            Assert.That(no, "the crab says no");
            Assert.That(dir.done, "the end plays out");
            Assert.That(st.Flag("game_done"));
        }
    }
}
