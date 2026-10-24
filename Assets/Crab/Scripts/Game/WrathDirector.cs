// The second circle: Wrath. Orange dunes, rivers of lava, volcanoes on the horizon and the
// burnt ruins of a town. Here the demons are big and angry: imps leap, burrowers come up out
// of the sand, brutes smash the ground. The crab finds a real weapon (the machine gun), makes
// itself armour, and its first companion — the drone SCOUT. Three fire vents feed the demons;
// when they are choked, the Fire Colossus rises from the lava lake.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    public sealed class WrathDirector : MonoBehaviour
    {
        public List<Rift> vents = new List<Rift>();
        public List<Enemy> firstImps = new List<Enemy>();
        public List<Beacon> beacons = new List<Beacon>();
        public Colossus colossus;
        public Rift exit;

        Hero hero;
        GameState st;

        IEnumerator Start()
        {
            yield return null;
            hero = Hero.Instance;
            st = GameSession.Instance.State;
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
            Sfx.Loop("lava", 0.4f);
            Sfx.Loop("wind", 0.3f);
            Sfx.Loop("music_wrath", 0.6f, true);
            foreach (var b in beacons) b.Light(st.Flag("beacon_" + b.id) || st.Checkpoint == b.id);
            foreach (var v in vents)
            {
                if (st.Flag("vent_" + v.id)) { v.open = false; v.budget = 0; if (v.fire) v.fire.Stop(); }
                var vv = v;
                v.Sealed += _ => OnSealed(vv);
            }
            exit.gameObject.SetActive(st.Flag("colossus"));
            colossus.Defeated += OnColossus;
            exit.Entered += _ =>
            {
                st.SetFlag("circle2_done");
                Story.Task("");
                Story.Title("КОНЕЦ II КРУГА", "III. Круг жадности — скоро");
            };
            if (st.Flag("colossus")) colossus.gameObject.SetActive(false);
            hero.Died += () => StartCoroutine(Respawn());

            if (!string.IsNullOrEmpty(st.Checkpoint))
            {
                var b = beacons.FirstOrDefault(x => x.id == st.Checkpoint);
                if (b) hero.Controller.Teleport(b.transform.position + b.transform.forward * 1.6f, b.transform.eulerAngles.y);
            }

            if (!st.Flag("wrath_intro"))
            {
                st.SetFlag("wrath_intro");
                Story.Title("II. КРУГ ГНЕВА", "Где ярость не гаснет");
                yield return Wait(3.5f);
                Story.Think("Жарко. Песок под лапами горячий, а воздух дрожит. Здесь всё злится.", 4.5f);
                yield return Wait(5);
                Story.Task("Пройти через пустыню гнева");
                Story.Hint("Лава обжигает — обходи её. Металл ищи в сгоревших домах.");
                while (firstImps.Any(e => e && e.Alive) && firstImps.All(e => !e || Vector3.Distance(e.transform.position, hero.transform.position) > 12)) yield return null;
                if (firstImps.Any(e => e && e.Alive))
                {
                    Story.Think("Бесы! Быстрые… и они прыгают. Пистолета мало.", 3.5f);
                    Story.Task("Отбиться от бесов");
                    while (firstImps.Any(e => e && e.Alive)) yield return null;
                }
                Story.Think("Мне нужно что-то посерьёзнее. Много металла — и оружие, которое не умолкает.", 4.5f);
                yield return Wait(2);
            }
            if (!st.Knows(ItemId.MachineGun)) Story.Task("Найти металлолом в сгоревшем городе");
            while (!st.Knows(ItemId.MachineGun)) yield return null;
            if (Sealed < vents.Count && !st.Flag("colossus"))
            {
                Story.Task(VentTask);
                if (!st.Flag("vent_hint"))
                {
                    st.SetFlag("vent_hint");
                    yield return Wait(3);
                    Story.Think("Огненные жерла. Из них лезут демоны, как из разломов в Лимбе. Их надо заглушить.", 4.5f);
                }
            }
            while (Sealed < vents.Count) yield return null;
            if (!st.Flag("colossus"))
            {
                colossus.awake = true;
                Story.Task("Спуститься к лавовому озеру");
                if (!st.Flag("colossus_hint"))
                {
                    st.SetFlag("colossus_hint");
                    Story.Think("Жерла затихли. А озеро лавы… оно дышит. Там кто-то огромный.", 4.5f);
                }
            }
        }

        int Sealed => vents.Count(r => !r.open);
        string VentTask => $"Заглушить огненные жерла ({Sealed}/{vents.Count})";

        void OnSealed(Rift v)
        {
            st.SetFlag("vent_" + v.id);
            if (Sealed == 1 && !st.Knows(ItemId.ScrapArmor))
            {
                st.Learn(ItemId.ScrapArmor);
                Story.Think("Жар жерла впился в панцирь… Нужна защита. Пластины из металлолома — я вижу, как их приладить.", 5);
            }
            else if (Sealed == 2 && !st.Knows(ItemId.ScoutDrone))
            {
                st.Learn(ItemId.ScoutDrone);
                Story.Think("Мне не справиться одному. А если… сделать того, кто будет рядом? Маленький летучий помощник.", 5);
                Story.Hint("Новый чертёж: Создать дрона-разведчика (Tab)");
            }
            if (Sealed < vents.Count) Story.Task(VentTask);
            GameSession.Instance.Save();
        }

        void OnColossus()
        {
            st.SetFlag("colossus");
            GameSession.Instance.Save();
            StartCoroutine(Victory());
        }

        IEnumerator Victory()
        {
            yield return Wait(3);
            Sfx.Loop("music_wrath", 0.5f, true);
            Story.Think("Колосс рассыпался. Он был сама ярость — и ярость сожгла его изнутри.", 4.5f);
            yield return Wait(5);
            if (hero.Drone) Story.Think("А SCOUT стрелял вместе со мной. Оружие можно создавать не только для себя.", 5);
            else Story.Think("Оружие можно создавать не только для себя. Можно создать того, кто встанет рядом.", 5);
            yield return Wait(5.5f);
            exit.gameObject.SetActive(true);
            Fx.Flash(exit.Point, new Color(1f, 0.8f, 0.3f), 10, 20, 0.8f);
            Sfx.Loop("rift", 0.6f);
            Story.Task("Войти в разлом: путь в III круг");
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
