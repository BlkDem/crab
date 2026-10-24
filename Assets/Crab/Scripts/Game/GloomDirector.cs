// The fifth circle: Despondency. A vast dark world under the earth, almost without light: only
// the Spark, glowing mushrooms and the old lamps of memory the crab lights. The dark drains the
// will and whispers. Three shards of memory show the crab the past: the Spark burned in others
// before it. Then an ancient machine wakes — the AI of the last bearer — and the crab becomes
// the cyber-crab (the frame, the scanner, the AI, HUNTER). In the hall of darkness waits the
// Devourer of Light.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    public sealed class GloomDirector : MonoBehaviour
    {
        public List<MemoryShard> shards = new List<MemoryShard>();
        public List<MemoryLamp> lamps = new List<MemoryLamp>();
        public List<Beacon> beacons = new List<Beacon>();
        public AncientMachine machine;
        public Devourer devourer;
        public Despair despair;
        public Rift exit;

        Hero hero;
        GameState st;

        IEnumerator Start()
        {
            yield return null;
            hero = Hero.Instance;
            st = GameSession.Instance.State;
            despair.hero = hero;
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
            Sfx.Loop("drip", 0.5f);
            Sfx.Loop("music_gloom", 0.6f, true);
            hero.ShowSpark(true);
            // down here the Spark is the crab's lantern
            var lantern = new GameObject("Lantern").AddComponent<Light>();
            lantern.transform.SetParent(hero.Core, false);
            lantern.transform.localPosition = Vector3.up * 0.6f;
            lantern.type = LightType.Point;
            lantern.color = new Color(1f, 0.85f, 0.6f);
            lantern.range = 9;
            lantern.intensity = 1.6f;
            foreach (var b in beacons) b.Light(st.Flag("beacon_" + b.id) || st.Checkpoint == b.id);
            foreach (var l in lamps)
            {
                if (st.Flag("lamp_" + l.name)) l.Light(true);
                var ll = l;
                l.Lit += _ => st.SetFlag("lamp_" + ll.name);
            }
            foreach (var s in shards)
            {
                if (st.Flag("memory_" + s.id)) s.MarkSeen();
                s.Seen += OnSeen;
            }
            if (st.Flag("machine")) machine.Wake(true);
            machine.Awakened += OnMachine;
            exit.gameObject.SetActive(st.Flag("devourer"));
            devourer.Defeated += OnDevourer;
            exit.Entered += _ =>
            {
                st.SetFlag("circle5_done");
                Story.Task("");
                Story.Title("КОНЕЦ V КРУГА", "Впереди — круг предательства");
            };
            if (st.Flag("devourer")) devourer.gameObject.SetActive(false);
            hero.Died += () => StartCoroutine(Respawn());
            if (!string.IsNullOrEmpty(st.Checkpoint))
            {
                var b = beacons.FirstOrDefault(x => x.id == st.Checkpoint);
                if (b) hero.Controller.Teleport(b.transform.position + b.transform.forward * 1.6f, b.transform.eulerAngles.y);
            }

            if (!st.Flag("gloom_intro"))
            {
                st.SetFlag("gloom_intro");
                Story.Title("V. КРУГ УНЫНИЯ", "Где гаснет свет");
                yield return Wait(3.5f);
                Story.Think("Темно. Так темно, что Искра — единственный свет. И она… тускнеет?", 4.5f);
                yield return Wait(5);
                Story.Hint("Во тьме тает воля. Зажигай огни памяти (E) — у огня она возвращается.");
                yield return Wait(4);
            }
            while (Seen < shards.Count)
            {
                if (Story.Objective != ShardTask) Story.Task(ShardTask);
                yield return Wait(0.5f);
            }
            if (!st.Flag("machine"))
            {
                Story.Task("Найти машину из видения");
                while (!st.Flag("machine")) yield return null;
            }
            while (!st.Items.Has(ItemId.CyberFrame))
            {
                if (Story.Objective != FrameTask) Story.Task(FrameTask);
                yield return Wait(0.5f);
            }
            if (st.SetFlag("cyber"))
            {
                Story.Think("Лапы за спиной слушаются, как свои. В глазу — цифры. А в голове — голос, и он не шепчет гадостей.", 5);
                yield return Wait(5);
                Story.AI("Каркас подключён. Сканер видит Тоску сквозь тьму. Я — часть тебя теперь.", 4.5f);
                yield return Wait(5);
            }
            if (!st.Flag("devourer"))
            {
                devourer.awake = true;
                Story.Task("Спуститься в Чертог тьмы");
                Story.AI("Впереди — источник этой тьмы. Уровень угрозы: максимальный.", 4);
            }
        }

        int Seen => shards.Count(s => s.seen);
        string ShardTask => $"Найти осколки памяти ({Seen}/{shards.Count})";
        const string FrameTask = "Собрать кибер-каркас у древней машины (Tab)";

        void OnSeen(MemoryShard s)
        {
            st.SetFlag("memory_" + s.id);
            GameSession.Instance.Save();
            if (Seen == shards.Count) StartCoroutine(Truth());
        }

        IEnumerator Truth()
        {
            yield return Wait(1);
            Story.Title("ИСКРА БЫЛА ДО МЕНЯ", "Я не первый носитель");
            yield return Wait(4);
            Story.Think("Рыцарь. Птица. Машина. Все несли её — и все погасли. А Искра искала дальше. И нашла меня.", 5.5f);
            yield return Wait(6);
            Story.Think("Последней была машина. Она должна быть где-то здесь, в глубине.", 4);
        }

        void OnMachine()
        {
            if (!st.SetFlag("machine")) return;
            st.Learn(ItemId.CyberFrame);
            st.Learn(ItemId.Hunter);
            Story.Hint("Новые чертежи у древней машины: кибер-каркас, дрон HUNTER");
            GameSession.Instance.Save();
        }

        void OnDevourer()
        {
            st.SetFlag("devourer");
            GameSession.Instance.Save();
            StartCoroutine(Victory());
        }

        IEnumerator Victory()
        {
            yield return Wait(3);
            Sfx.Loop("music_gloom", 0.5f, true);
            Story.AI("Источник тьмы уничтожен. Носитель жив. Это… впервые за всю мою память.", 4.5f);
            yield return Wait(5);
            Story.Think("Уныние — это когда кажется, что свет больше не зажечь. Но я зажёг. И зажгу снова.", 5);
            yield return Wait(5.5f);
            exit.gameObject.SetActive(true);
            Fx.Flash(exit.Point, new Color(1f, 0.9f, 0.7f), 10, 20, 0.8f);
            Sfx.Loop("rift", 0.6f);
            Story.Task("Войти в разлом: путь в VI круг");
        }

        IEnumerator Respawn()
        {
            Story.Hint("Краб пал…");
            yield return Wait(2.5f);
            GameSession.Instance.fade = 1;
            hero.Respawn();
            despair.Restore();
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
