// The second circle: Lust (Dante, canto V) — the souls of the lustful whirled for ever by the
// infernal hurricane over the abyss. The crab passes Minos the judge, crosses the ruina where the
// storm begins, and gathers the three pages torn out of Minos' book (Semiramis, Dido, Francesca)
// from the islands adrift in the wind — riding the gusts, rising on the updrafts; with all three
// the way out before Minos' throne opens. (The blockout: no demons of the wind yet, no boss.)
// Progress is kept in story flags, so a loaded game does not replay what is done.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    public sealed class LustDirector : MonoBehaviour
    {
        public List<Beacon> beacons = new List<Beacon>();
        public List<PagePickup> pages = new List<PagePickup>();
        public Rift exit;
        public Transform judge;
        [Tooltip("Where the ruina begins (z): the storm's lesson")]
        public float ruinaZ = -50;

        Hero hero;
        GameState st;

        IEnumerator Start()
        {
            yield return null;
            hero = Hero.Instance;
            st = GameSession.Instance.State;
            hero.Controller.stormCraft = true;
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
            Sfx.Loop("music_limbo", 0.35f, true);
            foreach (var b in beacons) b.Light(st.Flag("beacon_" + b.id) || st.Checkpoint == b.id);
            foreach (var p in pages)
            {
                if (st.Flag("page_" + p.id)) p.gameObject.SetActive(false);
                p.Taken += OnPage;
            }
            exit.gameObject.SetActive(Taken == pages.Count);
            exit.Entered += _ =>
            {
                st.SetFlag("lust_done");
                Story.Task("");
                Story.Title("КОНЕЦ II КРУГА", "Блокаут: дальше — гнев");
            };
            hero.Died += () => StartCoroutine(Respawn());

            // where the crab stands at the start: at its last beacon
            if (!string.IsNullOrEmpty(st.Checkpoint))
            {
                var b = beacons.FirstOrDefault(x => x.id == st.Checkpoint);
                if (b) hero.Controller.Teleport(b.transform.position + b.transform.forward * 1.6f, b.transform.eulerAngles.y);
            }

            if (!st.Flag("lust_intro"))
            {
                st.SetFlag("lust_intro");
                Story.Title("II. ПОХОТЬ", "Адский ураган, не знающий покоя");
                yield return Wait(3.5f);
                Story.Think("Ветер. Он не стихает ни на миг — и несёт в себе голоса. Стоны. Имена.", 4.5f);
                yield return Wait(5);
                Story.Task("Пройти мимо судьи", judge);
                while (judge && hero.transform.position.z < judge.position.z - 14) yield return null;
                Story.Think("Минос. Он судит каждого и обвивает себя хвостом столько раз, на сколько кругов вниз отправить. Дважды. Второй круг.", 6);
                while (hero.transform.position.z < ruinaZ) yield return null;
                Story.Hint("При вое ветра — за скалу или держись якорем (Ctrl). Прыгни в порыв — и он унесёт далеко; держи прыжок — планируй.");
                yield return Wait(6);
                Story.Think("Здесь, на краю, ветер вырвал из его книги страницы. Три. Без них суд не полон — и путь дальше закрыт.", 5);
                yield return Wait(5);
            }
            if (Taken < pages.Count) Story.Task(PageTask, null, NearestPage);
            else OpenExit(false);
        }

        int Taken => pages.Count(p => st.Flag("page_" + p.id));
        string PageTask => $"Собрать страницы книги Миноса ({Taken}/{pages.Count})";

        static string Thought(string id) => id switch
        {
            "semiramis" => "Семирамида. Царица многих народов, сделавшая вольность законом, чтобы скрыть свой позор. Первая страница.",
            "dido" => "Дидона. Та, что нарушила верность праху Сихея и сгорела от любви. Ещё одна страница.",
            "francesca" => "«Любовь, любить велящая любимым…» Франческа и Паоло. Вместе даже здесь, даже в буре.",
            _ => "Страница книги Миноса.",
        };

        void OnPage(PagePickup p)
        {
            st.SetFlag("page_" + p.id);
            Story.Think(Thought(p.id), 5);
            GameSession.Instance.Save();
            if (Taken < pages.Count) Story.Task(PageTask, null, NearestPage);
            else OpenExit(true);
        }

        void OpenExit(bool now)
        {
            exit.gameObject.SetActive(true);
            if (now)
            {
                Fx.Flash(exit.Point, new Color(1f, 0.5f, 0.2f), 10, 20, 0.8f);
                Sfx.Loop("rift", 0.6f);
            }
            Story.Task("Все страницы собраны. К трону Миноса: войти в разлом", exit.transform);
        }

        Transform NearestPage()
        {
            Transform best = null;
            float d = float.MaxValue;
            foreach (var p in pages)
                if (p && p.gameObject.activeInHierarchy && Vector3.Distance(p.transform.position, hero.transform.position) is var x && x < d) { d = x; best = p.transform; }
            return best;
        }

        IEnumerator Respawn()
        {
            Story.Hint("Краб пал…");
            yield return Wait(2.5f);
            GameSession.Instance.fade = 1;
            hero.Respawn();
            Story.Hint("Маяк вернул меня.");
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime) { GameSession.Instance.fade = 1 - t; yield return null; }
            GameSession.Instance.fade = 0;
        }

        static IEnumerator Wait(float s)
        {
            for (float t = 0; t < s; t += Time.deltaTime) yield return null;
        }
    }
}
