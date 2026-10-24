// The seventh circle: Violence. A battlefield to the horizon: craters, barricades, burning wrecks,
// demon armies, siege engines, fortresses in the sky. Here the crab becomes a general: it gives
// orders to its squad, captures the legion's forge, raises a factory that builds it an army, breaks
// the siege engines — and meets the Warlord of Slaughter at the head of his guard.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    public sealed class ViolenceDirector : MonoBehaviour
    {
        public List<SiegeEngine> engines = new List<SiegeEngine>();
        public List<LegionBand> bands = new List<LegionBand>();
        public List<Beacon> beacons = new List<Beacon>();
        public Workshop forge;
        public RobotFactory factory;
        public Warlord warlord;
        public FlyingFortress fortress;
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
            Sfx.Loop("battle", 0.4f);
            Sfx.Loop("music_violence", 0.6f, true);
            if (!hero.GetComponent<Commander>()) hero.gameObject.AddComponent<Commander>().hero = hero;
            foreach (var b in beacons) b.Light(st.Flag("beacon_" + b.id) || st.Checkpoint == b.id);
            if (st.Flag("forge")) forge.Build(false);
            forge.Built += OnForge;
            foreach (var e in engines)
            {
                if (st.Flag("engine_" + e.name)) e.gameObject.SetActive(false);
                var ee = e;
                e.Broken += _ => { st.SetFlag("engine_" + ee.name); GameSession.Instance.Save(); };
            }
            factory.gameObject.SetActive(st.Items.Has(ItemId.Factory));
            factory.running = st.Items.Has(ItemId.Factory);
            exit.gameObject.SetActive(st.Flag("warlord"));
            warlord.Defeated += OnWarlord;
            exit.Entered += _ =>
            {
                st.SetFlag("circle7_done");
                Story.Task("");
                Story.Title("КОНЕЦ VII КРУГА", "VIII. Круг лжи — скоро");
            };
            if (st.Flag("warlord")) warlord.gameObject.SetActive(false);
            hero.Died += () => StartCoroutine(Respawn());
            if (!string.IsNullOrEmpty(st.Checkpoint))
            {
                var b = beacons.FirstOrDefault(x => x.id == st.Checkpoint);
                if (b) hero.Controller.Teleport(b.transform.position + b.transform.forward * 1.6f, b.transform.eulerAngles.y);
            }

            if (!st.Flag("violence_intro"))
            {
                st.SetFlag("violence_intro");
                Story.Title("VII. КРУГ НАСИЛИЯ", "Где война не кончается");
                yield return Wait(3.5f);
                Story.Think("Поле боя до самого горизонта. Армии демонов. А в небе — крепости.", 4.5f);
                yield return Wait(5);
                Story.AI("Носитель, теперь ты — командир. G — отряд атакует цель, H — ко мне, J — держать позицию.", 6);
                yield return Wait(6.5f);
            }
            if (!st.Flag("forge"))
            {
                Story.Task("Захватить кузницу легиона: построить на ней свою");
                while (!st.Flag("forge")) yield return null;
            }
            while (!st.Items.Has(ItemId.Factory))
            {
                if (Story.Objective != FactoryTask) Story.Task(FactoryTask);
                yield return Wait(0.5f);
            }
            if (!factory.running) StartFactory();
            if (st.SetFlag("general"))
            {
                Story.Think("Фабрика гудит. Первые роботы выходят строем — мои. Я больше не один и даже не отряд. Я — армия.", 5);
                yield return Wait(5);
            }
            while (engines.Any(e => e && e.Alive))
            {
                string task = $"Разрушить осадные машины ({engines.Count(e => !e || !e.Alive)}/{engines.Count})";
                if (Story.Objective != task) Story.Task(task);
                yield return Wait(0.5f);
            }
            if (!st.Flag("warlord"))
            {
                warlord.awake = true;
                Story.Task("Выйти против Полководца Резни");
                if (st.SetFlag("warlord_hint"))
                    Story.AI("Осадные машины уничтожены. Их командир идёт сам. С ним — гвардия.", 4.5f);
            }
        }

        const string FactoryTask = "Построить фабрику роботов у кузницы (Tab)";

        void OnForge()
        {
            if (!st.SetFlag("forge")) return;
            st.Learn(ItemId.Factory);
            Story.Think("Кузница легиона — теперь моя. Здесь можно поставить не верстак, а целую фабрику.", 5);
            Story.Hint("Новый чертёж: фабрика роботов");
            GameSession.Instance.Save();
        }

        void StartFactory()
        {
            factory.gameObject.SetActive(true);
            factory.running = true;
            Fx.Assemble(factory.transform, new Color(0.5f, 0.85f, 1f), 1.5f);
            Sfx.Play("create", factory.transform.position, 1, 0.5f);
        }

        void OnWarlord()
        {
            st.SetFlag("warlord");
            GameSession.Instance.Save();
            StartCoroutine(Victory());
        }

        IEnumerator Victory()
        {
            yield return Wait(3);
            Sfx.Loop("music_violence", 0.4f, true);
            Story.Think("Он знал только войну. А я узнал, зачем она: не чтобы ломать — чтобы защищать.", 5);
            yield return Wait(5.5f);
            Story.AI("Армия демонов отступает. Крепости уходят из неба. Носитель… ты — генерал.", 4.5f);
            yield return Wait(5);
            exit.gameObject.SetActive(true);
            Fx.Flash(exit.Point, new Color(0.8f, 0.8f, 1f), 10, 20, 0.8f);
            Sfx.Loop("rift", 0.6f);
            Story.Task("Войти в разлом: путь в VIII круг");
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
