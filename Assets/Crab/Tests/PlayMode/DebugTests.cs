// The debug mode: any chapter at once, with the crab as it would come there, and its level
// changed on the spot. Picture: Docs/shots/play-debug-menu.jpg.
using System.Collections;
using Crab.Core;
using Crab.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TestTools;

namespace Crab.Tests
{
    public sealed partial class PlayTests
    {
        [Test]
        public void DebugChaptersGiveTheCrabItsLevel()
        {
            int[] level = { 1, 1, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            for (int i = 0; i < DebugMode.Chapters.Length; i++)
            {
                var st = new GameState();
                DebugMode.Prepare(st, i);
                Assert.That(st.Level, Is.EqualTo(level[i]), DebugMode.Chapters[i]);
                Assert.That(st.Flag("spark"), Is.EqualTo(i > 0), DebugMode.Chapters[i]);
            }
            var s = new GameState();
            DebugMode.Prepare(s, 6);
            for (int l = 1; l <= 9; l++)
            {
                DebugMode.SetLevel(s, l);
                Assert.That(s.Level, Is.EqualTo(l), $"level {l}");
            }
            DebugMode.SetLevel(s, 1);
            Assert.That(s.Items.Has(ItemId.ScrapArmor) || s.Items.Has(ItemId.HeavyArmor), Is.False, "no armour on an ordinary crab");
        }

        [UnityTest]
        public IEnumerator DebugMenuStartsAChapter()
        {
            yield return Load("Menu");
            UiToTexture();
            yield return Wait(0.5f);
            var root = Object.FindAnyObjectByType<MainMenu>().GetComponent<UIDocument>().rootVisualElement;
            Press(ButtonText(root, "Отладка: любой круг"));
            yield return Wait(0.3f);
            yield return Shot("debug-menu");
            Press(ButtonText(root, MainMenu.Chapter("Gloom")));
            yield return Until(() => Hero.Instance && GameSession.Instance.State.Scene == "Gloom" && !GameSession.Instance.Changing, 30, "Gloom loads");
            try
            {
                Assert.That(DebugMode.On);
                var st = GameSession.Instance.State;
                Assert.That(st.Level, Is.EqualTo(4), "the mage comes to Gloom");
                Assert.That(st.Flag("circle4_done") && !st.Flag("circle5_done"));
                yield return Wait(1);
                yield return Shot("debug-gloom");
            }
            finally { DebugMode.Leave(); }
        }
    }
}
