// The first circle: Limbo, the dead world. A grey sea, fog, the ruins of a town. The crab learns
// to fight with what it makes, seals the three rifts the demons come through, and faces the
// Guardian of Limbo on the town's square. Progress is kept in story flags, so a loaded game
// does not replay what is done.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    public sealed class LimboDirector : MonoBehaviour
    {
        public List<Rift> rifts = new List<Rift>();
        public List<Enemy> firstShades = new List<Enemy>();
        public List<Beacon> beacons = new List<Beacon>();
        public Guardian guardian;
        public Rift exit;
        public Transform square;

        Hero hero;
        GameState st;

        IEnumerator Start()
        {
            yield return null;
            hero = Hero.Instance;
            st = GameSession.Instance.State;
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
            Sfx.Loop("surf", 0.35f);
            Sfx.Loop("wind", 0.45f);
            Sfx.Loop("music_limbo", 0.6f, true);
            foreach (var b in beacons) b.Light(st.Flag("beacon_" + b.id) || st.Checkpoint == b.id);
            foreach (var r in rifts)
            {
                if (st.Flag("rift_" + r.id)) { r.open = false; r.budget = 0; }
                var rr = r;
                r.Sealed += _ => OnSealed(rr);
            }
            exit.gameObject.SetActive(st.Flag("guardian"));
            guardian.Defeated += OnGuardian;
            exit.Entered += _ =>
            {
                st.SetFlag("circle1_done");
                st.Scene = "Limbo";
                Story.Task("");
                Story.Title("КОНЕЦ I КРУГА", "II. Круг гнева — скоро");
            };
            if (st.Flag("guardian")) { guardian.gameObject.SetActive(false); }
            hero.Died += () => StartCoroutine(Respawn());

            // where the crab stands at the start: at its last beacon
            if (!string.IsNullOrEmpty(st.Checkpoint))
            {
                var b = beacons.FirstOrDefault(x => x.id == st.Checkpoint);
                if (b) hero.Controller.Teleport(b.transform.position + b.transform.forward * 1.6f, b.transform.eulerAngles.y);
            }

            if (!st.Flag("limbo_intro"))
            {
                st.SetFlag("limbo_intro");
                Story.Title("I. ЛИМБ", "Мёртвый мир");
                yield return Wait(3.5f);
                Story.Think("Серое море. Туман. Здесь когда-то жили… или только думали, что живут.", 4.5f);
                yield return Wait(5);
                Story.Task("Осмотреться в мёртвом городе");
                Story.Hint("ЛКМ — удар, ПКМ — прицел, Tab — создать, E — втянуть материю, 1-5 — оружие");
                // the first shades come out of the fog
                while (firstShades.Any(e => e && e.Alive) && firstShades.All(e => !e || Vector3.Distance(e.transform.position, hero.transform.position) > 10)) yield return null;
                if (firstShades.Any(e => e && e.Alive))
                {
                    Story.Think("Тени. Они тянутся ко мне… Нет. Не дамся.", 3.5f);
                    Story.Task("Отбиться от теней");
                    while (firstShades.Any(e => e && e.Alive)) yield return null;
                }
                Story.Think("Они рассыпались в туман. Значит, их можно победить.", 4);
                yield return Wait(2);
            }
            if (Sealed < rifts.Count && !st.Flag("guardian"))
            {
                Story.Task(RiftTask);
                if (!st.Flag("rift_hint"))
                {
                    st.SetFlag("rift_hint");
                    Story.Think("Там, в глубине города, что-то режет мир. Разломы. Оттуда они и лезут.", 4.5f);
                }
            }
            while (Sealed < rifts.Count) yield return null;
            if (!st.Flag("guardian"))
            {
                guardian.awake = true;
                Story.Task("Найти Стража на главной площади");
                if (!st.Flag("guardian_hint"))
                {
                    st.SetFlag("guardian_hint");
                    Story.Think("Разломы закрыты. Но кто-то стережёт этот круг. Я чувствую его на площади.", 4.5f);
                }
            }
        }

        int Sealed => rifts.Count(r => !r.open);
        string RiftTask => $"Запечатать разломы ({Sealed}/{rifts.Count})";

        void OnSealed(Rift r)
        {
            st.SetFlag("rift_" + r.id);
            if (Sealed == 1 && !st.Knows(ItemId.Pistol))
            {
                st.Learn(ItemId.Pistol);
                st.Learn(ItemId.Ammo);
                Story.Think("Когда разлом схлопнулся, я понял, как устроено… оружие. Пистолет. Из металла и камня.", 5);
            }
            if (Sealed < rifts.Count) Story.Task(RiftTask);
            GameSession.Instance.Save();
        }

        void OnGuardian()
        {
            st.SetFlag("guardian");
            GameSession.Instance.Save();
            StartCoroutine(Victory());
        }

        IEnumerator Victory()
        {
            yield return Wait(4);
            Sfx.Loop("music_limbo", 0.5f, true);
            Story.Think("Страж пал. Он… не был злым. Он просто не пускал дальше.", 4.5f);
            yield return Wait(5);
            Story.Think("Я способен на большее, чем думал. Намного большее.", 4);
            yield return Wait(4);
            exit.gameObject.SetActive(true);
            Fx.Flash(exit.Point, new Color(1f, 0.5f, 0.2f), 10, 20, 0.8f);
            Sfx.Loop("rift", 0.6f);
            Story.Task("Войти в разлом: путь во II круг");
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
