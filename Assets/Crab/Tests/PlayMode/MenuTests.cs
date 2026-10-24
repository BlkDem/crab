// The title screen with a save: "continue" names the circle and the time played, the settings
// open and close, and a new game over a save asks first. Picture: Docs/shots/play-menu-*.jpg.
using System.Collections;
using System.Linq;
using Crab.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Crab.Tests
{
    public sealed partial class PlayTests
    {
        static void Press(Button b)
        {
            using var e = NavigationSubmitEvent.GetPooled();
            e.target = b;
            b.SendEvent(e);
        }

        static Button ButtonText(VisualElement root, string text) => root.Query<Button>().ToList().FirstOrDefault(b => b.text == text);

        [UnityTest]
        public IEnumerator MenuContinueAndSettings()
        {
            var st = GameSession.Ensure().State;
            st.Scene = "Power";
            st.PlaySeconds = 3 * 3600 + 25 * 60;
            GameSession.Instance.Save();
            yield return Load("Menu");
            UiToTexture();
            yield return Wait(0.5f);
            var root = Object.FindAnyObjectByType<MainMenu>().GetComponent<UIDocument>().rootVisualElement;
            var cont = root.Q<Button>("continue");
            Assert.That(cont, Is.Not.Null, "a save: continue");
            Assert.That(cont.Q<Label>(className: "menu-btn-sub").text, Is.EqualTo("IX. Власть · 3 ч 25 мин"));
            yield return Shot("menu-continue");
            var fresh = ButtonText(root, "Новая игра");
            Press(fresh);
            yield return null;
            Assert.That(fresh.ClassListContains("confirm"), "a new game over a save asks first");
            Assert.That(GameSession.Instance.Changing, Is.False);
            Press(ButtonText(root, "Настройки"));
            yield return null;
            var panel = root.Q(className: "settings-box");
            Assert.That(panel, Is.Not.Null, "the settings open");
            Assert.That(panel.Query<Button>(className: "on").ToList().Count, Is.EqualTo(1), "one quality chosen");
            yield return Wait(0.3f);
            yield return Shot("menu-settings");
            Press(ButtonText(panel, "Назад"));
            yield return null;
            Assert.That(root.Q(className: "settings-box"), Is.Null, "and close");
        }
    }
}
