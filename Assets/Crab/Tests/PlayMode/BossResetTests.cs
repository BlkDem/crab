// A fall in a boss fight: the boss notices only between its attacks, so the crab is brought back
// only after the boss has started over (whole again, kneeling), never into the running fight.
using System.Collections;
using Crab.Core;
using Crab.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Crab.Tests
{
    public sealed partial class PlayTests
    {
        [UnityTest]
        public IEnumerator FallingToTheGuardianStartsTheFightOver()
        {
            var st = GameSession.Ensure().State;
            st.SetFlag("spark");
            st.SetFlag("limbo_intro");
            yield return Load("Limbo");
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            var dir = Object.FindAnyObjectByType<LimboDirector>();
            foreach (var r in dir.rifts) r.Seal();
            yield return Until(() => dir.guardian.awake, 10, "the Guardian wakes");
            var g = dir.guardian;
            hero.Controller.Teleport(g.transform.position + Vector3.back * 9, 0);
            yield return Until(() => g.Fighting && hero.Cam.Mode == CamMode.First && !hero.Controller.frozen, 15, "the first-person fight");
            g.GetComponentInChildren<GuardianHeart>().Hit(150, g.transform.position, Vector3.forward, hero.gameObject);
            Assert.That(g.Health01, Is.LessThan(1));
            yield return Wait(1.2f);   // in the middle of an attack
            hero.Invulnerable = false;
            hero.Hurt(9999, hero.transform.position, Vector3.forward);
            Assert.That(hero.Alive, Is.False);
            hero.Invulnerable = true;
            yield return Until(() => hero.Alive, 20, "the crab comes back");
            Assert.That(g.Fighting, Is.False, "the fight is over before the crab is back");
            yield return Wait(2.5f);
            Assert.That(g.Health01, Is.EqualTo(1).Within(0.001f), "the Guardian is whole again");
            Assert.That(hero.Cam.Mode, Is.EqualTo(CamMode.Third));
        }
    }
}
