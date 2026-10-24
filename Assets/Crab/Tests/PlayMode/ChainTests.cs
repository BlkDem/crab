// The whole road: every scene's way out leads into the next circle, in order, to the end; and
// stepping into a rift saves the game as already in the next circle.
using System.Collections;
using System.IO;
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
        static readonly string[] Road = { "Prologue", "Limbo", "Wrath", "Greed", "Envy", "Gloom", "Betrayal", "Violence", "Lies", "Power", "Epilogue" };

        [UnityTest]
        public IEnumerator EveryCircleLeadsToTheNext()
        {
            for (int i = 0; i + 1 < Road.Length; i++)
            {
                GameSession.Ensure().ResetState();
                yield return Load(Road[i]);
                var ways = Object.FindObjectsByType<Rift>(FindObjectsInactive.Include).Select(r => r.leadsTo).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
                Assert.That(ways, Is.EquivalentTo(new[] { Road[i + 1] }), $"the way out of {Road[i]}");
                foreach (var e in Enemy.All.ToList()) if (e) Object.Destroy(e.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator ARiftSavesTheNextCircle()
        {
            var st = GameSession.Ensure().State;
            yield return Load("Violence");
            st.Checkpoint = "violence_camp";
            var exit = Object.FindObjectsByType<Rift>(FindObjectsInactive.Include).First(r => r.leadsTo == "Lies");
            exit.gameObject.SetActive(true);
            exit.Use(Hero.Instance);
            var saved = JsonUtility.FromJson<SaveData>(File.ReadAllText(GameSession.SavePath));
            Assert.That(saved.scene, Is.EqualTo("Lies"), "continue opens the next circle");
            Assert.That(saved.checkpoint, Is.Empty, "not at a beacon of the last one");
            yield return Until(() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Lies", 15, "the passage");
        }
    }
}
