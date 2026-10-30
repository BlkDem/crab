// The II circle's storm, on its proving ground: a gust moves the crab, but not anchored, nor in a
// rock's lee; a jump into a gust carries it over a gap it couldn't jump; the updraft lifts it onto
// the high island; the abyss throws it back onto firm ground, hurt, not dead.
// Pictures: Docs/shots/play-lust-*.jpg.
using System.Collections;
using System.Linq;
using Crab.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Crab.Tests
{
    public sealed partial class PlayTests
    {
        static IEnumerator LustTest()
        {
            var st = GameSession.Ensure().State;
            DebugMode.Prepare(st, 2);
            yield return Load("LustTest");
            yield return Wait(0.5f);
        }

        static float Drift(Hero hero, Vector3 from) => Vector3.Distance(new Vector3(from.x, 0, from.z), new Vector3(hero.transform.position.x, 0, hero.transform.position.z));

        [UnityTest]
        public IEnumerator AGustMovesTheCrabButNotAnchoredNorSheltered()
        {
            yield return LustTest();
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            var gale = Gale.Instance;
            gale.prevailing = 0;
            gale.sway = 0;
            gale.gustSpread = 0;
            // in the open, letting go
            var open = new Vector3(0, 0.3f, -4);
            hero.Controller.Teleport(open, 0);
            gale.Hold(Gale.Phase.Gust);
            yield return Wait(1.5f);
            float free = Drift(hero, open);
            yield return Shot("lust-gust");
            // anchored
            gale.Hold(Gale.Phase.Calm);
            hero.Controller.Teleport(open, 0);
            yield return Wait(0.3f);
            hero.Controller.autopilotAnchor = true;
            gale.Hold(Gale.Phase.Gust);
            yield return Wait(1.5f);
            float held = Drift(hero, open);
            hero.Controller.autopilotAnchor = null;
            // in the lee of a rock: the rock south of the crab, the wind blowing north
            gale.Hold(Gale.Phase.Calm);
            var rock = GameObject.Find("LustTest").GetComponentsInChildren<Transform>().First(t => t.name.StartsWith("Crag") && t.position.z < 5 && t.GetComponentInChildren<Renderer>());
            var rb = Extent(rock.gameObject);
            var lee = new Vector3(rock.position.x, rock.position.y + 0.5f, rb.max.z + 0.9f);
            hero.Controller.Teleport(lee, 0);
            yield return Wait(0.4f);
            var at = hero.transform.position;
            Assert.That(gale.Sheltered(at), "the rock's lee is sheltered");
            gale.Hold(Gale.Phase.Gust);
            yield return Wait(1.5f);
            float lee1 = Drift(hero, at);
            gale.Resume();
            Debug.Log($"LUST drift: free {free:0.00}, anchored {held:0.00}, lee {lee1:0.00}");
            Assert.That(free, Is.GreaterThan(3), "a gust carries the crab off");
            Assert.That(held, Is.LessThan(0.3f), "the anchor holds");
            Assert.That(lee1, Is.LessThan(0.6f), "the lee shelters");
        }

        [UnityTest]
        public IEnumerator AJumpIntoAGustCrossesTheGap()
        {
            yield return LustTest();
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            var gale = Gale.Instance;
            gale.sway = 0;
            gale.gustSpread = 0;
            gale.Hold(Gale.Phase.Calm);
            // without the gust the gap is too far: the abyss throws the crab back
            hero.Controller.Teleport(new Vector3(0, 0.3f, 7), 0);
            hero.Cam.Face(0);
            yield return Wait(0.3f);
            hero.Controller.autopilot = new Vector2(0, 1);
            hero.Controller.autopilotRun = true;
            yield return new WaitUntil(() => hero.transform.position.z > 11.2f);
            hero.Controller.autopilotJump = true;
            yield return Wait(2.5f);
            hero.Controller.autopilot = null;
            bool across = hero.transform.position.z > 18 && hero.transform.position.y > -1.5f;
            Assert.That(across, Is.False, $"no jump crosses it in the calm (at {hero.transform.position})");
            // with the gust it does
            hero.Controller.Teleport(new Vector3(0, 0.3f, 7), 0);
            yield return Wait(0.3f);
            gale.Resume();
            gale.Set(Gale.Phase.Warning, 0.6f);   // the howl rises: run for the edge, jump as the gust comes
            hero.Controller.autopilot = new Vector2(0, 1);
            yield return new WaitUntil(() => hero.transform.position.z > 11.2f);
            hero.Controller.autopilotJump = true;
            hero.Controller.autopilotHold = true;
            var track = new System.Text.StringBuilder();
            float tEnd = Time.time + 2.2f, tNext = 0;
            bool shot = false;
            while (Time.time < tEnd)
            {
                if (Time.time > tNext) { tNext = Time.time + 0.1f; var q = hero.transform.position; track.Append($" ({q.z:0.0},{q.y:0.0}{(hero.Controller.Grounded ? "g" : "")})"); }
                if (!shot && Time.time > tEnd - 1.6f) { shot = true; yield return Shot("lust-gust-jump"); }
                // landed across: the player lets go (else the gust at its back runs it off the far side)
                if (hero.Controller.Grounded && hero.transform.position.z > 19) { hero.Controller.autopilot = null; hero.Controller.autopilotHold = null; }
                yield return null;
            }
            Debug.Log("LUST track:" + track);
            hero.Controller.autopilot = null;
            hero.Controller.autopilotHold = null;
            gale.Hold(Gale.Phase.Calm);
            yield return Wait(0.6f);
            Assert.That(hero.transform.position.z, Is.GreaterThan(18), "the gust carried the crab to the next island");
            Assert.That(hero.transform.position.y, Is.GreaterThan(-1.5f), "and it stands on it");
        }

        [UnityTest]
        public IEnumerator TheUpdraftLiftsTheCrabOntoTheHighIsland()
        {
            yield return LustTest();
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            var gale = Gale.Instance;
            gale.Hold(Gale.Phase.Calm);
            gale.calmStrength = 0;
            var up = Object.FindAnyObjectByType<Updraft>();
            hero.Controller.Teleport(new Vector3(7, 0, 28), 90);
            hero.Cam.Face(90);
            yield return Wait(0.3f);
            hero.Controller.autopilot = new Vector2(0, 1);
            yield return new WaitUntil(() => hero.transform.position.x > up.transform.position.x - 0.3f || Time.time > 99999);
            hero.Controller.autopilot = null;
            float peak = 0;
            float end = Time.realtimeSinceStartup + 4;
            while (Time.realtimeSinceStartup < end) { peak = Mathf.Max(peak, hero.transform.position.y); yield return null; }
            yield return Shot("lust-updraft");
            hero.Controller.autopilot = new Vector2(0, 1);
            hero.Controller.autopilotHold = true;
            yield return Wait(2.5f);
            hero.Controller.autopilot = null;
            hero.Controller.autopilotHold = null;
            yield return Wait(1f);
            Assert.That(peak, Is.GreaterThan(10.5f), "the updraft lifts the crab above the high island");
            Assert.That(hero.Controller.Grounded && hero.transform.position.y > 8, $"it lands on the high island (at {hero.transform.position})");
        }

        [UnityTest]
        public IEnumerator TheAbyssThrowsTheCrabBack()
        {
            yield return LustTest();
            var hero = Hero.Instance;
            var st = GameSession.Instance.State;
            var gale = Gale.Instance;
            gale.Hold(Gale.Phase.Calm);
            hero.Controller.Teleport(new Vector3(0, 0.3f, -2), 0);
            yield return Wait(1.2f);   // stood on firm ground
            float hp = st.Health.Value;
            hero.Controller.Teleport(new Vector3(0, 2, 15), 0);   // over the gap
            yield return Until(() => hero.transform.position.y < -3, 6, "falling into the abyss");
            yield return Until(() => hero.transform.position.y > -1 && hero.Controller.Grounded, 8, "back on firm ground");
            Assert.That(hero.Alive, "not dead");
            Assert.That(st.Health.Value, Is.LessThan(hp), "but hurt");
            Assert.That(hero.transform.position.z, Is.LessThan(12), "on the island it fell from");
        }
    }
}
