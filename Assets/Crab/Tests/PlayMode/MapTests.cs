// The map and the sand demons, in the circle of Wrath: a picture of the scene from above with the
// crab, the vents still open and the beacons on it, and a blow at a burrower's trail that knocks
// it out of the sand. Picture: Docs/shots/play-map-wrath.jpg.
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
        [UnityTest]
        public IEnumerator MapOfWrathAndBurrowers()
        {
            var st = WrathState();
            yield return Load("Wrath");
            UiToTexture();
            var hero = Hero.Instance;
            hero.Invulnerable = true;
            yield return Until(() => WorldMap.Ready, 5, "the picture from above");
            Assert.That(WorldMap.Area.width, Is.GreaterThan(150), "the whole circle");
            var marks = WorldMap.Marks().ToList();
            Assert.That(marks.Count(m => m.kind == WorldMap.Mark.Rift), Is.EqualTo(3), "three vents still open");
            Assert.That(marks.Any(m => m.kind == WorldMap.Mark.Beacon), "the beacons");
            var me = WorldMap.ToMap(hero.transform.position);
            Assert.That(me.x, Is.InRange(0f, 1f));
            Assert.That(me.y, Is.InRange(0f, 1f));
            Object.FindAnyObjectByType<GameUI>().ShowBigMap(true);
            yield return Wait(0.5f);
            yield return Shot("map-wrath");
            Object.FindAnyObjectByType<GameUI>().ShowBigMap(false);
            // a burrower under the sand: a blow at it brings it up
            foreach (var e in Enemy.All.ToList()) if (e) Object.Destroy(e.gameObject);
            yield return null;
            var b = Object.Instantiate(Library.Current.burrower, hero.transform.position + hero.transform.forward * 8, Quaternion.identity).GetComponent<Enemy>();
            yield return Wait(0.3f);
            Assert.That(b.Visible, Is.False, "under the sand");
            float hp = b.Health01;
            b.Hit(20, b.transform.position, Vector3.forward, hero.gameObject);
            Assert.That(b.Visible, "knocked out of the sand");
            Assert.That(b.Health01, Is.LessThan(hp), "and hurt");
        }
    }
}
