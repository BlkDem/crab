// The fourth circle: Envy. A paradise — meadows, blossoming trees, white pavilions, a clear lake,
// kind glowing dwellers. Too good: now and then it slips and hell shows through. The crab talks to
// the dwellers, finds the Spring and becomes the crab-mage (matter out of energy: the blade, the
// gravity gun, the portal). Then envy takes the dwellers — why him and not us? — and at the mirror
// lake waits the Reflection of the Crab, which copies everything he has.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    public sealed class EnvyDirector : MonoBehaviour
    {
        public List<Dweller> dwellers = new List<Dweller>();
        public List<Beacon> beacons = new List<Beacon>();
        public Spring spring;
        public MirrorCrab reflection;
        public IllusionGlitch glitch;
        public Rift exit;

        Hero hero;
        GameState st;
        readonly List<Enemy> envious = new List<Enemy>();

        IEnumerator Start()
        {
            yield return null;
            hero = Hero.Instance;
            st = GameSession.Instance.State;
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
            Sfx.Loop("wind", 0.2f);
            Sfx.Loop("music_envy", 0.6f, true);
            foreach (var b in beacons) b.Light(st.Flag("beacon_" + b.id) || st.Checkpoint == b.id);
            foreach (var d in dwellers) d.Talked += OnTalked;
            if (st.Flag("spring")) spring.touched = true;
            spring.Touched += OnSpring;
            exit.gameObject.SetActive(st.Flag("reflection"));
            reflection.Defeated += OnReflection;
            exit.Entered += _ =>
            {
                st.SetFlag("circle4_done");
                Story.Task("");
                Story.Title("КОНЕЦ IV КРУГА", "Впереди — круг уныния");
            };
            if (st.Flag("reflection")) reflection.gameObject.SetActive(false);
            hero.Died += () => StartCoroutine(Respawn());
            if (!string.IsNullOrEmpty(st.Checkpoint))
            {
                var b = beacons.FirstOrDefault(x => x.id == st.Checkpoint);
                if (b) hero.Controller.Teleport(b.transform.position + b.transform.forward * 1.6f, b.transform.eulerAngles.y);
            }

            if (!st.Flag("envy_intro"))
            {
                st.SetFlag("envy_intro");
                Story.Title("IV. КРУГ ЗАВИСТИ", "Где всё слишком хорошо");
                yield return Wait(3.5f);
                Story.Think("Солнце. Цветы. Тишина… После золотого города — как будто рай. Слишком похоже на рай.", 5);
                yield return Wait(6);
            }
            // the dwellers
            if (!st.Flag("turned"))
            {
                glitch.interval = 40;
                if (!st.Flag("spring"))
                {
                    while (Talked < 3)
                    {
                        if (Story.Objective != TalkTask) Story.Task(TalkTask);
                        yield return Wait(0.5f);
                    }
                    if (st.SetFlag("spring_hint"))
                    {
                        Story.Think("Они все говорят об Источнике на холме. «Он даёт всё, чего пожелаешь».", 4.5f);
                        yield return Wait(2);
                    }
                    glitch.interval = 22;
                    Story.Task("Найти Источник на холме");
                    while (!st.Flag("spring")) yield return null;
                }
                // the mage
                while (!st.Mage)
                {
                    if (Story.Objective != MageTask) Story.Task(MageTask);
                    yield return Wait(0.5f);
                }
                yield return Wait(2);
                yield return Turn();
            }
            else foreach (var d in dwellers) if (d) Destroy(d.gameObject);
            glitch.interval = 12;
            if (!st.Flag("envy_cleared"))
            {
                while (envious.Any(e => e && e.Alive))
                {
                    Story.Task($"Пережить зависть ({envious.Count(e => !e || !e.Alive)}/{envious.Count})");
                    yield return Wait(0.5f);
                }
                st.SetFlag("envy_cleared");
                GameSession.Instance.Save();
                Story.Think("Они хотели не меня — они хотели быть мной. И ничего не осталось от них самих.", 5);
                yield return Wait(5);
            }
            if (!st.Flag("reflection"))
            {
                reflection.awake = true;
                Story.Task("Подойти к зеркальному озеру");
                if (st.SetFlag("lake_hint"))
                    Story.Think("Озеро гладкое, как зеркало. В нём отражается не небо — я.", 4.5f);
            }
        }

        int Talked => dwellers.Count(d => !d || d.talked);
        string TalkTask => $"Поговорить с жителями ({Mathf.Min(3, Talked)}/3)";
        const string MageTask = "Создать Ядро Искры (Tab): материя из энергии";

        void OnTalked(Dweller d)
        {
            // the more the crab listens, the more the paradise slips
            if (Talked == 2) glitch.Slip(0.5f);
            if (Talked == 2 && st.SetFlag("first_slip")) Story.Think("Что это было?! На миг — кости и красное небо. Показалось?", 4);
        }

        void OnSpring()
        {
            if (!st.SetFlag("spring")) return;
            foreach (var id in new[] { ItemId.SparkCore, ItemId.EnergyBlade, ItemId.GravityGun, ItemId.Portal, ItemId.EnergyCell }) st.Learn(id);
            StartCoroutine(Insight());
            GameSession.Instance.Save();
        }

        IEnumerator Insight()
        {
            Story.Think("Я понял. Мне не нужно искать материю — я могу создать её. Из энергии. Из себя.", 5.5f);
            yield return Wait(6);
            Story.Hint("Новые чертежи: Ядро Искры, энергоклинок, гравипушка, портал (F)");
        }

        IEnumerator Turn()
        {
            Story.Think("Жители смотрят на меня. Улыбки… застыли.", 3.5f);
            yield return Wait(3);
            glitch.Slip(1.2f);
            Story.Hint("«Почему ему — а не нам?!»");
            foreach (var d in dwellers.ToList())
            {
                if (!d) continue;
                var e = d.Turn();
                if (e) envious.Add(e);
                yield return Wait(0.4f);
            }
            dwellers.Clear();
            st.SetFlag("turned");
            Story.Think("Друзья оказались демонами. Это и есть Зависть.", 4);
        }

        void OnReflection()
        {
            st.SetFlag("reflection");
            GameSession.Instance.Save();
            StartCoroutine(Victory());
        }

        IEnumerator Victory()
        {
            glitch.interval = 0;
            yield return Wait(3);
            Sfx.Loop("music_envy", 0.5f, true);
            Story.Think("Отражение разбилось. Оно умело всё, что умею я, — и не было ничем.", 4.5f);
            yield return Wait(5);
            Story.Think("Зависть — это желание быть кем-то другим. А я — краб. И этого достаточно.", 5);
            yield return Wait(5.5f);
            exit.gameObject.SetActive(true);
            Fx.Flash(exit.Point, new Color(0.6f, 0.5f, 1f), 10, 20, 0.8f);
            Sfx.Loop("rift", 0.6f);
            Story.Task("Войти в разлом: путь в V круг");
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
