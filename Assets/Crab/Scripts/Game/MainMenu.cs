// The title screen: the crab on the beach behind a big "КРАБ ПРОТИВ АДА"; new game, continue,
// quit. The camera drifts slowly around the scene.
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

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            root.AddToClassList("screen");
            var col = new VisualElement();
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
            Btn(btns, "Новая игра", () => GameSession.Ensure().NewGame());
            if (GameSession.HasSave) Btn(btns, "Продолжить", () => GameSession.Ensure().Continue());
            Btn(btns, "Выход", () =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
            var foot = new Label("Маленький краб. Божья Искра. Девять кругов ада.  ·  Глава I — Лимб");
            foot.AddToClassList("main-foot");
            root.Add(foot);
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        static void Btn(VisualElement parent, string text, System.Action click)
        {
            var b = new Button(() => { Sfx.Play("click"); click(); }) { text = text };
            b.AddToClassList("menu-btn");
            parent.Add(b);
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
