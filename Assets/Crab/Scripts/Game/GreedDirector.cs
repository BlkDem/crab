// The third circle: Greed. A fortress city of gold on a dark plain, where demons hoard
// everything that can be counted. The crab meets real wealth — gold, crystals, pure energy —
// builds its first workshop, becomes the armoured crab (heavy armour, the energy rifle, the
// shield), pays its way into the vault and brings down the Usurer King. After him it
// understands complex mechanisms: the TANK.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    public sealed class GreedDirector : MonoBehaviour
    {
        public List<Enemy> firstCollectors = new List<Enemy>();
        public List<Beacon> beacons = new List<Beacon>();
        public Workshop workshop;
        public VaultDoor vault;
        public Usurer usurer;
        public Rift exit;
        [Tooltip("Inside the city gate: entering it starts the city part")]
        public Transform gate;

        Hero hero;
        GameState st;

        IEnumerator Start()
        {
            yield return null;
            hero = Hero.Instance;
            st = GameSession.Instance.State;
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
            Sfx.Loop("wind", 0.35f);
            Sfx.Loop("music_greed", 0.6f, true);
            foreach (var b in beacons) b.Light(st.Flag("beacon_" + b.id) || st.Checkpoint == b.id);
            if (st.Flag("workshop")) workshop.Build(false);
            workshop.Built += OnWorkshop;
            if (st.Flag("vault")) vault.Open(false);
            vault.Opened += () =>
            {
                st.SetFlag("vault");
                GameSession.Instance.Save();
                if (!st.Flag("usurer"))
                {
                    usurer.awake = true;
                    Story.Task("Войти в тронный зал Ростовщика");
                    Story.Think("Дверь взяла плату и отворилась. За ней — тот, кто берёт плату со всего круга.", 4.5f);
                }
            };
            exit.gameObject.SetActive(st.Flag("usurer"));
            usurer.Defeated += OnUsurer;
            exit.Entered += _ =>
            {
                st.SetFlag("circle3_done");
                Story.Task("");
                Story.Title("КОНЕЦ III КРУГА", "IV. Круг зависти — скоро");
            };
            if (st.Flag("usurer")) usurer.gameObject.SetActive(false);
            else if (st.Flag("vault")) usurer.awake = true;
            hero.Died += () => StartCoroutine(Respawn());

            if (!string.IsNullOrEmpty(st.Checkpoint))
            {
                var b = beacons.FirstOrDefault(x => x.id == st.Checkpoint);
                if (b) hero.Controller.Teleport(b.transform.position + b.transform.forward * 1.6f, b.transform.eulerAngles.y);
            }

            if (!st.Flag("greed_intro"))
            {
                st.SetFlag("greed_intro");
                Story.Title("III. КРУГ ЖАДНОСТИ", "Где всё имеет цену");
                yield return Wait(3.5f);
                Story.Think("Золотые стены до неба. Здесь не жгут и не рвут — здесь считают.", 4.5f);
                yield return Wait(5);
                Story.Task("Войти в золотой город");
                // the first collectors run off with their sacks
                while (firstCollectors.Any(e => e && e.Alive) && firstCollectors.All(e => !e || Vector3.Distance(e.transform.position, hero.transform.position) > 14)) yield return null;
                if (firstCollectors.Any(e => e && e.Alive))
                    Story.Think("Сборщики. Тащат мешки и удирают… Догоню — мешок будет мой.", 4);
            }
            if (!st.Flag("city"))
            {
                while (Vector3.Distance(hero.transform.position, gate.position) > 10) yield return null;
                st.SetFlag("city");
                Story.Think("Здесь всё — материя. Золото, кристаллы… даже сама энергия течёт по столбам.", 4.5f);
                yield return Wait(5);
            }
            if (!st.Flag("workshop"))
            {
                Story.Task("Найти кузню в городе и построить мастерскую");
                if (st.SetFlag("workshop_hint"))
                    Story.Think("Мне нужно место, где можно собрать сложное. Кузня на площади — разрушена, но я её подниму.", 4.5f);
                while (!st.Flag("workshop")) yield return null;
            }
            while (!st.Items.Has(ItemId.HeavyArmor))
            {
                if (Story.Objective != ArmourTask) Story.Task(ArmourTask);
                yield return Wait(0.5f);
            }
            if (st.SetFlag("armoured"))
            {
                Story.Think("Тяжёлая броня. Я — Бронекраб. Пусть теперь попробуют.", 4.5f);
                yield return Wait(4);
            }
            if (!st.Flag("vault"))
            {
                Story.Task($"Открыть золотую дверь сокровищницы ({vault.price} золота)");
                if (st.SetFlag("vault_hint"))
                    Story.Think("Сокровищница на севере. Дверь без замка — она просто ждёт оплаты.", 4.5f);
            }
        }

        const string ArmourTask = "В мастерской: тяжёлая броня, энергоружьё, энергощит";

        void OnWorkshop()
        {
            if (!st.SetFlag("workshop")) return;
            foreach (var id in new[] { ItemId.HeavyArmor, ItemId.EnergyRifle, ItemId.EnergyCell, ItemId.EnergyShield }) st.Learn(id);
            Story.Think("Мастерская! Здесь я соберу то, что на ходу не сложить: броню, энергоружьё, щит.", 5);
            Story.Hint("У мастерской — Tab или E: новые чертежи");
            GameSession.Instance.Save();
        }

        void OnUsurer()
        {
            st.SetFlag("usurer");
            GameSession.Instance.Save();
            StartCoroutine(Victory());
        }

        IEnumerator Victory()
        {
            yield return Wait(3);
            Sfx.Loop("music_greed", 0.5f, true);
            Story.Think("Он копил, пока золото не стало им самим. И рассыпался монетами.", 4.5f);
            yield return Wait(5);
            st.Learn(ItemId.Tank);
            Story.Think("Я вижу, как складывать сложное. Механизм, у которого есть своя воля. Тяжёлый робот — TANK.", 5);
            Story.Hint("Новый чертёж: робот TANK (в мастерской)");
            yield return Wait(5.5f);
            exit.gameObject.SetActive(true);
            Fx.Flash(exit.Point, new Color(0.6f, 1f, 0.8f), 10, 20, 0.8f);
            Sfx.Loop("rift", 0.6f);
            Story.Task("Создать TANK в мастерской и войти в разлом: путь в IV круг");
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
