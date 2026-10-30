// The world's mechanics in Limbo: the crab can't walk through what stands on the land, and a pack
// of demons finds its way round the ruins to the crab — none stuck, none inside a wall.
// Picture: Docs/shots/play-limbo-pack.jpg.
using System.Collections;
using System.Collections.Generic;
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
        static Bounds Extent(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        [UnityTest]
        public IEnumerator PropsStopTheCrab()
        {
            var st = GameSession.Ensure().State;
            st.SetFlag("spark");
            st.SetFlag("limbo_intro");
            yield return Load("Limbo");
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            foreach (var e in Enemy.All.ToList()) if (e) Object.Destroy(e.gameObject);
            // each kind of thing that should stop the crab; arches are met side-on (their opening is a way through)
            var kinds = new (string prefix, bool sideways)[]
            {
                ("Arch0", true), ("Arch1", true), ("column", false), ("Stele0", false), ("Stele1", false), ("Cypress", false),
                ("deadtree", false), ("Crag", false), ("CastleWall", false), ("CastleTower", false), ("gate_statue", false),
                ("Rubble", false), ("Scrap", false), ("Planks", false), ("Beacon", false),
            };
            var all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            var leaks = new List<string>();
            foreach (var (prefix, sideways) in kinds)
            {
                bool tried = false;
                foreach (var t in all.Where(x => x && x.name.StartsWith(prefix) && x.GetComponentInChildren<Renderer>()).Take(6))
                {
                    var b = Extent(t.gameObject);
                    if (b.size.y < 0.8f) { tried = true; break; }   // low enough to step over (a heap of chains)
                    var c = new Vector3(t.position.x, b.center.y, t.position.z);
                    var dir = sideways ? t.right : t.forward;
                    dir.y = 0;
                    dir.Normalize();
                    float reach = Mathf.Max(b.extents.x, b.extents.z) + 3;
                    var from = c - dir * reach;
                    if (!Physics.Raycast(from + Vector3.up * 30, Vector3.down, out var hit, 60)) continue;
                    from = hit.point;
                    if (!Nav.Clear(from, 0.4f, 0.7f)) continue;   // no room to start from here
                    tried = true;
                    float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                    hero.Controller.Teleport(from + Vector3.up * 0.1f, yaw);
                    hero.Cam.Face(yaw);
                    yield return Wait(0.2f);
                    float y0 = hero.transform.position.y;
                    hero.Controller.autopilot = new Vector2(0, 1);
                    yield return Wait(Mathf.Max(3, reach / 2));
                    hero.Controller.autopilot = null;
                    var p = hero.transform.position;
                    bool crossed = Vector3.Dot(p - c, dir) > 0.3f;
                    bool climbed = p.y - y0 > 0.4f;
                    if (crossed && !climbed) leaks.Add($"{t.name} at {t.position}");
                    break;
                }
                if (!tried) Debug.Log($"PROPS: no clear approach to any {prefix}");
            }
            Assert.That(leaks, Is.Empty, "the crab walked through: " + string.Join("; ", leaks));
        }

        [UnityTest]
        public IEnumerator APackFindsTheCrabRoundTheRuins()
        {
            var st = GameSession.Ensure().State;
            st.SetFlag("spark");
            st.SetFlag("limbo_intro");
            yield return Load("Limbo");
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            foreach (var e in Enemy.All.ToList()) if (e) Object.Destroy(e.gameObject);
            // the crab among the graveyard's rows; the pack comes from all round, past steles and trees
            var spot = Nav.FreeSpot(new Vector3(-10, 0, -52), 0.4f, 0.7f);
            hero.Controller.Teleport(spot + Vector3.up * 0.1f, 0);
            yield return Wait(0.3f);
            var lib = Library.Current;
            var pack = new List<Enemy>();
            for (int i = 0; i < 8; i++)
            {
                var at = spot + Quaternion.Euler(0, i * 45 + 10, 0) * Vector3.forward * 13;
                var e = Object.Instantiate(i % 2 == 0 ? lib.shade : lib.crawler, at, Quaternion.identity).GetComponent<Enemy>();
                e.aggro = 30;
                pack.Add(e);
            }
            yield return Wait(0.5f);
            foreach (var e in pack)
            {
                var cc = e.GetComponent<CharacterController>();
                var p = e.transform.position;
                cc.enabled = false;
                Assert.That(Nav.Clear(p, cc.radius, cc.height), $"{e.name} set down inside something at {p}");
                cc.enabled = true;
            }
            float end = Time.realtimeSinceStartup + 30;
            while (Time.realtimeSinceStartup < end && pack.Any(e => Vector3.Distance(e.transform.position, hero.transform.position) > 3.5f)) yield return null;
            hero.Cam.Face(0, 30);
            yield return Shot("limbo-pack");
            var far = pack.Where(e => Vector3.Distance(e.transform.position, hero.transform.position) > 3.5f).Select(e => $"{e.name} at {e.transform.position}").ToList();
            Assert.That(far, Is.Empty, "stuck on the way: " + string.Join("; ", far));
        }
    }
}
