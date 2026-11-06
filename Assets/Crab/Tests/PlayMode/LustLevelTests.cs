// The II circle's level (Levels/lust.blend): the islands are land the crab stands on; the first gust
// carries it off the ruina's torn edge onto the islet; the updraft by the valley island lifts it onto
// Semiramis' tower; the three pages of Minos' book open the way out.
// Pictures: Docs/shots/play-lust-level-*.jpg.
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
        static IEnumerator LustLevel()
        {
            var st = GameSession.Ensure().State;
            DebugMode.Prepare(st, 2);
            st.SetFlag("lust_intro");
            yield return Load("Lust");
            yield return Wait(0.5f);
        }

        [UnityTest]
        public IEnumerator LustIslandsHoldTheCrab()
        {
            yield return LustLevel();
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            Gale.Instance.Hold(Gale.Phase.Calm);
            var isles = Object.FindObjectsByType<Land>(FindObjectsSortMode.None);
            Assert.That(isles.Length, Is.GreaterThanOrEqualTo(13), "the islands are land");
            foreach (var isle in isles)
            {
                // a free place on its top, wide enough for the crab (it would climb onto a ruin's step beside it)
                var p = isle.transform.position;
                Vector3? onTop = null;
                for (float r = 0; r < 9 && onTop == null; r += 1.5f)
                    for (int a = 0; a < 8 && onTop == null; a++)
                    {
                        var q = p + Quaternion.Euler(0, a * 45, 0) * Vector3.forward * r;
                        if (Physics.Raycast(q + Vector3.up * 6, Vector3.down, out var hit, 9, ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<Land>() == isle && Mathf.Abs(hit.point.y - p.y) < 0.5f
                            && Physics.OverlapSphere(hit.point + Vector3.up * 1.2f, 1.6f, ~0, QueryTriggerInteraction.Ignore).All(c => c.GetComponentInParent<Land>() == isle || c.transform.IsChildOf(hero.transform)))
                            onTop = hit.point;
                    }
                Assert.That(onTop.HasValue, $"{isle.name}: a free place on its top");
                hero.Controller.Teleport(onTop.Value + Vector3.up * 0.5f, 0);
                yield return Wait(0.8f);
                Assert.That(hero.Controller.Grounded && Mathf.Abs(hero.transform.position.y - onTop.Value.y) < 0.4f, $"{isle.name}: the crab stands on it (at {hero.transform.position}, its top {onTop.Value.y})");
            }
            // the pages hang over land, not over the abyss
            foreach (var page in Object.FindAnyObjectByType<LustDirector>().pages)
                Assert.That(Physics.Raycast(page.transform.position, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore), $"{page.name}: land under it");
        }

        /// <summary>The souls' ledge, from the start to the ruina: nothing hangs over it, nothing bars
        /// it, the sun reaches it (the crags at the wall's foot must not spill over it).</summary>
        [UnityTest]
        public IEnumerator TheLedgeLiesOpenToTheSky()
        {
            yield return LustLevel();
            var hero = Hero.Instance;
            var sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).First(l => l.type == LightType.Directional);
            bool Solid(RaycastHit h) => !h.collider.transform.IsChildOf(hero.transform);
            var wrong = new System.Collections.Generic.List<string>();
            Vector3? last = null;
            for (float z = -112; z <= -52; z += 2)
            {
                var top = new Vector3(0.6f * Mathf.Sin(z * 0.07f), 60, z);
                var down = Physics.RaycastAll(top, Vector3.down, 120, ~0, QueryTriggerInteraction.Ignore).Where(Solid).OrderBy(h => h.distance).ToArray();
                if (down.Length == 0) { wrong.Add($"z {z}: no land"); continue; }
                var ground = down[0];
                if (ground.point.y > 1) wrong.Add($"z {z}: {ground.collider.name} over the ledge, at {ground.point.y:0.0} m");
                var foot = new Vector3(top.x, 0.3f, z);
                if (Physics.Raycast(foot, -sun.transform.forward, out var shade, 300, ~0, QueryTriggerInteraction.Ignore) && Solid(shade))
                    wrong.Add($"z {z}: {shade.collider.name} shades it from the sun");
                if (last.HasValue && Physics.Linecast(last.Value, foot, out var bar, ~0, QueryTriggerInteraction.Ignore) && Solid(bar))
                    wrong.Add($"z {z}: {bar.collider.name} bars the way");
                last = foot;
            }
            Assert.That(wrong, Is.Empty, string.Join("\n", wrong));
        }

        /// <summary>At the very edge the wind eases: time to react. But it does blow the crab off in the end.</summary>
        [UnityTest]
        public IEnumerator TheEdgeGivesTimeToReact()
        {
            yield return LustLevel();
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            var gale = Gale.Instance;
            gale.sway = 0;
            gale.gustSpread = 0;
            gale.Hold(Gale.Phase.Calm);
            hero.Controller.Teleport(new Vector3(0, 0.3f, -5.8f), 0);   // the ruina's north edge is at about -4.5
            yield return Wait(0.4f);
            gale.Hold(Gale.Phase.Gust);
            yield return Wait(1f);
            Assert.That(hero.Controller.Grounded && hero.transform.position.z < -4.2f, $"a second at the edge in a gust: still there (at {hero.transform.position})");
            yield return Until(() => !hero.Controller.Grounded || hero.transform.position.z > -3.5f, 8, "staying in the gust, the crab goes over");
        }

        /// <summary>The task's mark goes along the way, step by step, not straight at a page over the abyss;
        /// the ring under the crab in the air shows the land it will come down on, no ring: the abyss.</summary>
        [UnityTest]
        public IEnumerator TheMarkLeadsAlongTheWay()
        {
            yield return LustLevel();
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            Gale.Instance.Hold(Gale.Phase.Calm);
            var dir = Object.FindAnyObjectByType<LustDirector>();
            Assert.That(dir.route.Count, Is.EqualTo(19), "the way's points");
            Assert.That(dir.routeKinds.Count, Is.EqualTo(dir.route.Count));
            hero.Controller.Teleport(new Vector3(0, 0.3f, -6.5f), 0);
            yield return Wait(0.5f);
            Assert.That(Story.Goal, Is.EqualTo(dir.route[1]), "from the ruina's edge: to the islet");
            hero.Controller.Teleport(dir.route[2].position, 0);
            yield return Wait(0.8f);
            Assert.That(Story.Goal, Is.EqualTo(dir.route[3]), "from the valley: to the updraft");
            hero.Controller.Teleport(new Vector3(0, 0.3f, -6.5f), 0);
            yield return Wait(0.5f);
            Assert.That(Story.Goal, Is.EqualTo(dir.route[1]), "returned by the abyss: the way picks up from there");

            hero.Controller.Teleport(new Vector3(0, 8, -20), 0);
            yield return null;
            yield return null;
            Assert.That(LandingMark.Instance.Shown, "over the ruina: the ring");
            Assert.That(LandingMark.Instance.transform.position.y, Is.LessThan(2), "on the land");
            hero.Controller.Teleport(new Vector3(0, 2, -1), 0);   // the gap past the torn edge
            yield return null;
            yield return null;
            Assert.That(LandingMark.Instance.Shown, Is.False, "over the abyss: no ring");
        }

        /// <summary>The storm reads on the screen: its phase and time, the anchor. Pictures: play-lust-hud-*.jpg.</summary>
        [UnityTest]
        public IEnumerator TheStormReadsOnTheScreen()
        {
            yield return LustLevel();
            UiToTexture();
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            var gale = Gale.Instance;
            var ui = Object.FindAnyObjectByType<GameUI>().GetComponent<UnityEngine.UIElements.UIDocument>().rootVisualElement;
            string Text(string cls) => UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Label>(ui, className: cls).text;
            hero.Controller.Teleport(new Vector3(0, 0.3f, -14), 0);
            gale.Hold(Gale.Phase.Calm);
            yield return Wait(0.6f);
            Assert.That(Text("wind-phase"), Is.EqualTo("затишье"));
            gale.Set(Gale.Phase.Warning, 2);
            yield return Wait(0.6f);
            Assert.That(Text("wind-phase"), Does.StartWith("ВОЙ"));
            yield return Shot("lust-hud-warning");
            hero.Controller.autopilotAnchor = true;
            gale.Hold(Gale.Phase.Gust);
            yield return Wait(0.5f);
            Assert.That(Text("wind-phase"), Is.EqualTo("ПОРЫВ"));
            Assert.That(Text("wind-state"), Is.EqualTo("якорь держит"));
            yield return Shot("lust-hud-gust");
            hero.Controller.autopilotAnchor = null;
        }

        [UnityTest]
        public IEnumerator TheFirstGustCarriesOffTheRuina()
        {
            yield return LustLevel();
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            var gale = Gale.Instance;
            gale.sway = 0;
            gale.gustSpread = 0;
            gale.Hold(Gale.Phase.Calm);
            hero.Controller.Teleport(new Vector3(0, 0.3f, -12), 0);
            hero.Cam.Face(0);
            yield return Wait(0.4f);
            gale.Resume();
            gale.Set(Gale.Phase.Warning, 0.4f);   // the howl rises: run for the edge, jump as the gust comes
            hero.Controller.autopilot = new Vector2(0, 1);
            hero.Controller.autopilotRun = true;
            yield return Until(() => hero.transform.position.z > -5.4f, 6, "running to the edge");
            hero.Controller.autopilotJump = true;
            hero.Controller.autopilotHold = true;
            var track = new System.Text.StringBuilder();
            float tEnd = Time.time + 2.4f, tNext = 0;
            while (Time.time < tEnd)
            {
                if (Time.time > tNext) { tNext = Time.time + 0.1f; var q = hero.transform.position; track.Append($" ({q.z:0.0},{q.y:0.0}{(hero.Controller.Grounded ? "g" : "")})"); }
                if (hero.Controller.Grounded && hero.transform.position.z > 1) { hero.Controller.autopilot = null; hero.Controller.autopilotHold = null; }
                yield return null;
            }
            Debug.Log("LUST level track:" + track);
            hero.Controller.autopilot = null;
            hero.Controller.autopilotHold = null;
            gale.Hold(Gale.Phase.Calm);
            yield return Wait(0.6f);
            yield return Shot("lust-level-gust");
            Assert.That(hero.transform.position.z, Is.GreaterThan(1), "the gust carried the crab off the ruina");
            Assert.That(hero.Controller.Grounded && hero.transform.position.y > -2.6f, $"onto the islet or the valley island (at {hero.transform.position})");
        }

        [UnityTest]
        public IEnumerator TheUpdraftLiftsTheCrabOntoSemiramisTower()
        {
            yield return LustLevel();
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            var gale = Gale.Instance;
            gale.Hold(Gale.Phase.Calm);
            gale.calmStrength = 0;
            var up = GameObject.Find("UpdraftTower").GetComponent<Updraft>();
            var tower = GameObject.Find("TowerIsland").transform;
            var from = new Vector3(up.transform.position.x - 5, 0, up.transform.position.z);
            hero.Controller.Teleport(new Vector3(from.x, -1.7f, from.z), 90);
            hero.Cam.Face(90);
            yield return Wait(0.4f);
            hero.Controller.autopilot = new Vector2(0, 1);
            yield return Until(() => hero.transform.position.x > up.transform.position.x - 0.3f, 6, "walking into the updraft");
            hero.Controller.autopilot = null;
            float peak = -99;
            float end = Time.realtimeSinceStartup + 4;
            while (Time.realtimeSinceStartup < end) { peak = Mathf.Max(peak, hero.transform.position.y); yield return null; }
            yield return Shot("lust-level-updraft");
            hero.Controller.autopilot = new Vector2(0, 1);
            hero.Controller.autopilotHold = true;
            yield return Until(() => hero.Controller.Grounded, 6, "coming down");
            hero.Controller.autopilot = null;
            hero.Controller.autopilotHold = null;
            yield return Wait(0.6f);
            Assert.That(peak, Is.GreaterThan(tower.position.y + 1), "the updraft lifts the crab above the tower island");
            Assert.That(hero.Controller.Grounded && hero.transform.position.y > tower.position.y - 0.6f, $"it lands on the tower island (at {hero.transform.position})");
        }

        [UnityTest]
        public IEnumerator ThePagesOpenTheWayOut()
        {
            yield return LustLevel();
            var dir = Object.FindAnyObjectByType<LustDirector>();
            Assert.That(dir.pages.Count, Is.EqualTo(3), "three pages of Minos' book");
            Assert.That(dir.exit.gameObject.activeSelf, Is.False, "the way out is shut");
            foreach (var p in dir.pages.ToList()) p.Take();
            yield return null;
            Assert.That(dir.exit.gameObject.activeSelf, Is.True, "all three: the way out opens");
            Assert.That(GameSession.Instance.State.Flag("page_francesca"), "the pages are kept");
        }
    }
}
