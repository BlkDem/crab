// The title screen: the crab on the beach behind a big "КРАБ ПРОТИВ АДА"; continue (with the
// circle the save stands in), new game, settings, quit. The camera drifts slowly around the scene.
using UnityEngine;
using UnityEngine.UIElements;

namespace Crab.Game
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainMenu : MonoBehaviour
    {
        public Transform orbitCenter;
        public Camera cam;
        float t;
        VisualElement col, settings;

        public static string Chapter(string scene) => scene switch
        {
            "Prologue" => "Пролог",
            "Limbo" => "I. Лимб",
            "Wrath" => "II. Гнев",
            "Greed" => "III. Жадность",
            "Envy" => "IV. Зависть",
            "Gloom" => "V. Уныние",
            "Betrayal" => "VI. Предательство",
            "Violence" => "VII. Насилие",
            "Lies" => "VIII. Ложь",
            "Power" => "IX. Власть",
            "Epilogue" => "Финал",
            _ => scene,
        };

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            root.AddToClassList("screen");
            col = new VisualElement();
            col.AddToClassList("main-col");
            root.Add(col);
            var title = new Label("КРАБ");
            title.AddToClassList("main-title");
            col.Add(title);
            var sub = new Label("ПРОТИВ АДА");
            sub.AddToClassList("main-sub");
            col.Add(sub);
            var btns = new VisualElement();
            btns.AddToClassList("main-btns");
            col.Add(btns);
            var session = GameSession.Ensure();
            bool saved = GameSession.HasSave && session.Load();
            if (saved)
            {
                var c = Btn(btns, "", () => session.Continue());
                c.name = "continue";
                c.AddToClassList("menu-btn-two");
                c.Add(new Label("Продолжить"));
                var where = new Label(session.State.Flag("game_done") ? "игра пройдена · смотреть финал" : $"{Chapter(session.State.Scene)} · {Clock((float)session.State.PlaySeconds)}");
                where.AddToClassList("menu-btn-sub");
                c.Add(where);
            }
            Button fresh = null;
            fresh = Btn(btns, "Новая игра", () =>
            {
                if (saved && !fresh.ClassListContains("confirm"))
                {
                    fresh.AddToClassList("confirm");
                    fresh.text = "Начать заново? Сохранение пропадёт";
                    return;
                }
                session.NewGame();
            });
            Btn(btns, "Настройки", () =>
            {
                col.style.display = DisplayStyle.None;
                settings = Settings.Panel(() => { settings.RemoveFromHierarchy(); settings = null; col.style.display = DisplayStyle.Flex; });
                settings.AddToClassList("main-settings");
                root.Add(settings);
            });
            Btn(btns, "Выход", () =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
            var foot = new Label($"Маленький краб. Божья Искра. Девять кругов ада.  ·  версия {Application.version}");
            foot.AddToClassList("main-foot");
            root.Add(foot);
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        static Button Btn(VisualElement parent, string text, System.Action click)
        {
            var b = new Button(() => { Sfx.Play("click"); click(); }) { text = text };
            b.AddToClassList("menu-btn");
            parent.Add(b);
            return b;
        }

        static string Clock(float seconds)
        {
            int m = Mathf.FloorToInt(seconds / 60);
            return m >= 60 ? $"{m / 60} ч {m % 60:00} мин" : $"{m} мин";
        }

        void Start()
        {
            GameSession.Ensure();
            Sfx.Loop("surf", 0.6f);
            Sfx.Loop("music_beach", 0.6f, true);
        }

        void Update()
        {
            if (!cam || !orbitCenter) return;
            t += Time.deltaTime * 0.04f;
            var c = orbitCenter.position;
            cam.transform.position = c + new Vector3(Mathf.Sin(t) * 4.5f, 1.4f, -Mathf.Cos(t) * 4.5f);
            cam.transform.LookAt(c + Vector3.up * 0.4f + cam.transform.right * -1.2f);
        }
    }
}
