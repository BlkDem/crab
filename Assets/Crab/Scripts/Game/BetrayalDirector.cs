// The sixth circle: Betrayal. A labyrinth of iron walls, gears and crushers, where the first real
// armies march. A Guide offers to lead the crab through — "I hate them too". On the way, in the
// workshop of the legion, the crab becomes the architect: it designs its own squad (ENGINEER,
// MAG, upgrades for all). At the heart of the labyrinth the Guide shuts the gate behind, turns
// the crab's own companions against it and climbs into the Two-Faced.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    public sealed class BetrayalDirector : MonoBehaviour
    {
        public Guide guide;
        public List<LegionBand> bands = new List<LegionBand>();
        [Tooltip("The bands that come at the heart, when the Guide betrays")]
        public List<LegionBand> trap = new List<LegionBand>();
        public List<Beacon> beacons = new List<Beacon>();
        public Workshop workshop;
        public int workshopStop = 6;
        public MechGate heartGate;
        public Janus janus;
        public Rift exit;

        Hero hero;
        GameState st;
        bool spokeWorkshop;

        IEnumerator Start()
        {
            yield return null;
            hero = Hero.Instance;
            st = GameSession.Instance.State;
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
            Sfx.Loop("machines", 0.35f);
            Sfx.Loop("music_betrayal", 0.6f, true);
            foreach (var b in beacons) b.Light(st.Flag("beacon_" + b.id) || st.Checkpoint == b.id);
            workshop.Build(false);
            exit.gameObject.SetActive(st.Flag("janus"));
            janus.Defeated += OnJanus;
            exit.Entered += _ =>
            {
                st.SetFlag("circle6_done");
                Story.Task("");
                Story.Title("КОНЕЦ VI КРУГА", "Впереди — круг насилия");
            };
            if (st.Flag("janus")) janus.gameObject.SetActive(false);
            hero.Died += () => StartCoroutine(Respawn());
            guide.Reached += OnReached;
            if (!string.IsNullOrEmpty(st.Checkpoint))
            {
                var b = beacons.FirstOrDefault(x => x.id == st.Checkpoint);
                if (b) hero.Controller.Teleport(b.transform.position + b.transform.forward * 1.6f, b.transform.eulerAngles.y);
            }

            if (st.Flag("betrayed"))
            {
                guide.gameObject.SetActive(false);
                if (!st.Flag("janus")) { janus.awake = true; Story.Task("Победить Двуликого в сердце лабиринта"); }
                yield break;
            }
            if (st.Flag("guide_workshop")) guide.Jump(workshopStop);
            if (!st.Flag("betrayal_intro"))
            {
                st.SetFlag("betrayal_intro");
                Story.Title("VI. КРУГ ПРЕДАТЕЛЬСТВА", "Где верят только в выгоду");
                yield return Wait(3.5f);
                Story.Think("Железные стены. Шестерни, поршни, скрежет. Целый лабиринт — машина.", 4.5f);
                yield return Wait(5);
                guide.Say("Эй, краб! Я — Проводник. Знаю этот лабиринт как свои шестерни. Я тоже против них. Иди за мной.");
                yield return Wait(5);
                Story.AI("Объект неизвестной конструкции. Доверие: не рассчитано.", 3.5f);
                yield return Wait(4);
            }
            Story.Task("Идти за Проводником через лабиринт");
            while (!guide.Arrived) yield return null;
            yield return Betray();
        }

        void OnReached(int i)
        {
            if (i == 2) guide.Say("Тихо… Слышишь шаги? Легион. Первая настоящая армия. Держись за мной.");
            if (i == 4 && guide.gates.Any(g => g && !g.open)) guide.Say("Ворота. Рычаг где-то рядом — дёрни, а я покараулю.");
            if (i == workshopStop && !spokeWorkshop) StartCoroutine(Workshop());
            if (i == guide.path.Count - 3) guide.Say("Почти пришли. Сердце лабиринта — там выход. Я обещал.");
        }

        IEnumerator Workshop()
        {
            spokeWorkshop = true;
            st.SetFlag("guide_workshop");
            GameSession.Instance.Save();
            guide.Say("Здесь легион чинит своих. Бери, что нужно, — тебе понадобится армия.");
            yield return Wait(4);
            if (st.SetFlag("architect_learned"))
            {
                foreach (var id in new[] { ItemId.Constructor, ItemId.Engineer, ItemId.MageOrb, ItemId.SquadPlating, ItemId.SquadFire, ItemId.Tank, ItemId.Hunter, ItemId.ScoutDrone }) st.Learn(id);
                Story.Think("Я вижу, как сложить не вещь — команду. Кто чинит, кто бьёт молнией, кто держит удар.", 5);
                Story.Hint("Мастерская: конструктор отряда, ИНЖЕНЕР, МАГ, улучшения отряда (Tab)");
            }
            var keep = guide.lead;
            guide.lead = 0;   // it waits here while the crab builds
            while (!st.Items.Has(ItemId.Constructor))
            {
                if (Story.Objective != SquadTask) Story.Task(SquadTask);
                yield return Wait(0.5f);
            }
            if (st.SetFlag("architect"))
            {
                Story.Think("Я — архитектор. Не одиночка с палкой. Нас — отряд.", 4.5f);
                yield return Wait(4);
            }
            guide.lead = keep;
            Story.Task("Идти за Проводником через лабиринт");
        }

        const string SquadTask = "Собрать отряд в мастерской: конструктор отряда (и ИНЖЕНЕР, МАГ)";

        IEnumerator Betray()
        {
            guide.Say("Вот мы и пришли.");
            yield return Wait(3);
            heartGate.Set(false);
            Story.Flash(new Color(1f, 0.2f, 0.2f, 0.4f), 0.6f);
            guide.Say("Прости, краб. Им нужна твоя Искра — а мне нужна свобода. Честная сделка.");
            yield return Wait(4.5f);
            Story.AI("Ворота заперты. Сигналы легиона со всех сторон. Это была ловушка.", 4);
            Squad.Hack(6);
            Story.Hint("Сигнал Проводника перехватил отряд!");
            foreach (var b in trap) b.Call();
            yield return Wait(6);
            Squad.Release();
            Story.AI("Управление отрядом восстановлено.", 3);
            st.SetFlag("betrayed");
            GameSession.Instance.Save();
            Story.Task("Отбиться от легиона");
            while (trap.Any(b => !b.Beaten)) yield return Wait(0.5f);
            Story.Think("Он знал каждый поворот. Он вёл меня — сюда.", 4);
            yield return Wait(3);
            guide.Say("Ты всё ещё жив? Тогда посмотри на моё настоящее лицо.");
            yield return Wait(2.5f);
            Fx.Burst(guide.transform.position + Vector3.up, new Color(1f, 0.3f, 0.25f), 40, 3, 0.1f, 0.8f);
            guide.gameObject.SetActive(false);
            janus.awake = true;
            Story.Task("Победить Двуликого в сердце лабиринта");
        }

        void OnJanus()
        {
            st.SetFlag("janus");
            GameSession.Instance.Save();
            StartCoroutine(Victory());
        }

        IEnumerator Victory()
        {
            yield return Wait(3);
            heartGate.Set(true);
            Sfx.Loop("music_betrayal", 0.5f, true);
            Story.Think("Он предавал всех, кому служил. И в конце — самого себя.", 4.5f);
            yield return Wait(5);
            Story.AI("Носитель… я ведь тоже мог бы предать. У меня были 4117 лет подумать. Я не стану.", 5);
            yield return Wait(5.5f);
            Story.Think("Знаю. Иначе ты бы уже это сделал. Пошли.", 3.5f);
            yield return Wait(4);
            exit.gameObject.SetActive(true);
            Fx.Flash(exit.Point, new Color(1f, 0.6f, 0.4f), 10, 20, 0.8f);
            Sfx.Loop("rift", 0.6f);
            Story.Task("Войти в разлом: путь в VII круг");
        }

        IEnumerator Respawn()
        {
            Story.Hint("Краб пал…");
            yield return Wait(2.5f);
            GameSession.Instance.fade = 1;
            Squad.Release();
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
