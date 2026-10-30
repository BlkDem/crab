// The storm's proving ground (the II circle's iteration 2): plain islands over the abyss, to find
// out whether fighting the wind is fun before the circle is built. Four tasks, one after another:
// sit out a gust (in a rock's lee, or anchored), ride a gust over to the next island, rise on the
// updraft to the high one (gliding), reach the far island by gusts.
using System.Collections;
using UnityEngine;

namespace Crab.Game
{
    public sealed class LustTestDirector : MonoBehaviour
    {
        public Transform next, high, far;
        public float nextZ = 18, highY = 8, farZ = 48;
        Hero hero;
        public int Step { get; private set; }

        IEnumerator Start()
        {
            yield return null;
            hero = Hero.Instance;
            hero.Controller.stormCraft = true;
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
            Sfx.Loop("music_limbo", 0.35f, true);
            Story.Title("II КРУГ — ПОХОТЬ", "Прототип бури");
            Story.Task("Пережить порыв: при вое — за скалу или держаться якорем (Ctrl)");
            var gale = Gale.Instance;
            bool endured = false;
            gale.Changed += p => { if (p == Gale.Phase.Calm && Step == 0 && hero.Controller.Grounded) endured = true; };
            yield return new WaitUntil(() => endured);
            Step = 1;
            Story.Hint("Порыв — не только угроза: прыгни в него, и ветер унесёт далеко.");
            Story.Task("Перелететь на соседний остров на порыве", next);
            yield return new WaitUntil(() => hero.Controller.Grounded && hero.transform.position.z > nextZ);
            Step = 2;
            Story.Task("Подняться восходящим потоком на высокий остров (держать прыжок — планировать)", high);
            yield return new WaitUntil(() => hero.Controller.Grounded && hero.transform.position.y > highY);
            Step = 3;
            Story.Task("Добраться до дальнего острова", far);
            yield return new WaitUntil(() => hero.Controller.Grounded && hero.transform.position.z > farZ);
            Step = 4;
            Story.Task("");
            Story.Title("ВЕТЕР ПРОЙДЕН", "Прототип бури — конец");
        }
    }
}
