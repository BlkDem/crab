// The eighth circle: Lies. The world will not hold still: islands of rock hang in a void, bits of
// the crab's own past circles drift between them and trade places; bridges lie, heaps of scrap
// bite, and a Voice keeps talking. The AI gives the crab a lens for the truth; where no way is,
// the crab builds one. For a moment it is not even sure it exists. In the archive it learns that
// its mission was written by someone else — and the Father of Lies waits beyond.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    public sealed class LiesDirector : MonoBehaviour
    {
        public List<Beacon> beacons = new List<Beacon>();
        [Tooltip("Where the crab learns to build bridges (the edge of an island with no way on)")]
        public Transform gap;
        [Tooltip("Where the crab is not sure it exists any more")]
        public Transform doubt;
        public MemoryShard archive;
        public FatherOfLies father;
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
            Sfx.Loop("wind", 0.3f);
            Sfx.Loop("music_lies", 0.6f, true);
            foreach (var b in beacons) b.Light(st.Flag("beacon_" + b.id) || st.Checkpoint == b.id);
            if (st.Flag("archive")) archive.MarkSeen();
            archive.Seen += _ => OnArchive();
            exit.gameObject.SetActive(st.Flag("liar"));
            father.Defeated += OnFather;
            exit.Entered += _ =>
            {
                st.SetFlag("circle8_done");
                Story.Task("");
                Story.Title("КОНЕЦ VIII КРУГА", "Впереди — последний круг");
            };
            if (st.Flag("liar")) father.gameObject.SetActive(false);
            hero.Died += () => StartCoroutine(Respawn());
            if (!string.IsNullOrEmpty(st.Checkpoint))
            {
                var b = beacons.FirstOrDefault(x => x.id == st.Checkpoint);
                if (b) hero.Controller.Teleport(b.transform.position + b.transform.forward * 1.6f, b.transform.eulerAngles.y);
            }

            if (!st.Flag("lies_intro"))
            {
                st.SetFlag("lies_intro");
                Story.Title("VIII. КРУГ ЛЖИ", "Где ничему нельзя верить");
                yield return Wait(3.5f);
                Story.Think("Я узнаю эти места… пальма с пляжа, дом из Лимба, золотой купол. Обломки моего пути. И всё перепутано.", 5);
                yield return Wait(5.5f);
                Story.Whisper("Голос: «Добро пожаловать домой, краб. Здесь всё — правда. Честно».");
                yield return Wait(3);
                Story.AI("Носитель, мой сканер видит расхождения: что-то здесь не то, чем кажется. Я соберу тебе линзу.", 5);
                st.Learn(ItemId.TruthLens);
                yield return Wait(5);
            }
            while (!st.Items.Has(ItemId.TruthLens))
            {
                if (Story.Objective != LensTask) Story.Task(LensTask);
                yield return Wait(0.5f);
            }
            if (!st.Flag("bridges"))
            {
                Story.Task("Идти по островам к Архиву (T — взгляд истины: какие мосты лгут?)");
                while (Vector3.Distance(hero.transform.position, gap.position) > 6) yield return Wait(0.2f);
                st.SetFlag("bridges");
                st.Learn(ItemId.Bridgework);
                Story.Think("Здесь нет моста — ни ложного, ни настоящего. Но материя слушается меня. Я построю путь сам.", 5);
                Story.Hint("Новый чертёж: мосты из материи (B — положить плиту)");
                GameSession.Instance.Save();
            }
            while (!st.Items.Has(ItemId.Bridgework))
            {
                if (Story.Objective != BridgeTask) Story.Task(BridgeTask);
                yield return Wait(0.5f);
            }
            if (st.SetFlag("architect_of_ways"))
            {
                Story.Think("Я больше не ищу дорогу. Я её делаю.", 4);
                yield return Wait(3);
            }
            if (!st.Flag("doubt"))
            {
                Story.Task("Идти к Архиву");
                while (Vector3.Distance(hero.transform.position, doubt.position) > 7) yield return Wait(0.2f);
                yield return Doubt();
            }
            if (!st.Flag("archive"))
            {
                Story.Task("Прочесть Архив носителей");
                while (!st.Flag("archive")) yield return null;
                yield return Wait(1);
            }
            if (!st.Flag("liar"))
            {
                father.awake = true;
                Story.Task("Найти того, кто написал миссию");
            }
        }

        const string LensTask = "Создать Взгляд истины (Tab)";
        const string BridgeTask = "Создать мосты из материи (Tab) и перебраться дальше";

        /// <summary>A moment when the crab is not sure it is there at all.</summary>
        IEnumerator Doubt()
        {
            st.SetFlag("doubt");
            var rends = hero.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToList();
            Sfx.Play("glitch", null, 1, 0.5f);
            Story.Flash(new Color(0.6f, 0.4f, 1f, 0.6f), 0.8f);
            foreach (var r in rends) r.enabled = false;
            Story.AI("Носитель… не обнаружен. Сигнал Искры… не обнаружен.", 3.5f);
            yield return Wait(3.5f);
            Story.Whisper("Голос: «Тебя никогда не было. Был только сон краба на пляже».");
            yield return Wait(3);
            Story.Think("Меня… нет? Но я чувствую лапы. Песок. Искру. Я есть.", 4);
            yield return Wait(3);
            foreach (var r in rends) if (r) r.enabled = true;
            Sfx.Play("spark", hero.transform.position, 0.8f, 1);
            Fx.Burst(hero.transform.position + Vector3.up * 0.4f, new Color(1f, 0.85f, 0.5f), 40, 2, 0.08f, 0.8f);
            Story.AI("Сигнал восстановлен. Прости. Кто-то вмешивается в мои сенсоры.", 4);
            yield return Wait(4);
            GameSession.Instance.Save();
        }

        void OnArchive()
        {
            if (!st.SetFlag("archive")) return;
            GameSession.Instance.Save();
            StartCoroutine(AfterArchive());
        }

        IEnumerator AfterArchive()
        {
            Story.Title("МИССИЮ НАПИСАЛ НЕ БОГ", "Подпись стёрта");
            yield return Wait(4);
            Story.AI("Мой протокол сопровождения… тоже написан не мной. Я не знал. Носитель, я не знал.", 5);
            yield return Wait(5.5f);
            Story.Think("Значит, кто-то ведёт меня к трону Ада. Для себя. Посмотрим, кто.", 4.5f);
        }

        void OnFather()
        {
            st.SetFlag("liar");
            GameSession.Instance.Save();
            StartCoroutine(Victory());
        }

        IEnumerator Victory()
        {
            yield return Wait(3);
            Sfx.Loop("music_lies", 0.4f, true);
            Story.Think("Ложь кончилась. Осталась правда: дальше я иду не по чужой миссии — по своей.", 5);
            yield return Wait(5.5f);
            Story.AI("Носитель, я переписал свой протокол. Теперь там одна строка: «Быть рядом». Её написал я.", 5);
            yield return Wait(5.5f);
            exit.gameObject.SetActive(true);
            Fx.Flash(exit.Point, new Color(1f, 0.9f, 0.6f), 10, 20, 0.8f);
            Sfx.Loop("rift", 0.6f);
            Story.Task("Войти в разлом: путь в IX круг");
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
