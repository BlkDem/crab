// The ninth circle: Power. A demonic megastructure; the sky is shut by the fortress of hell. The
// crab comes here not alone: its squad, its drones, its machines march behind it. At the forge of
// the Spark it becomes the Lord of Matter. Across the great bridge the army breaks the last
// legions; at the gate it stays behind — the last fight the crab goes to alone. The Lord of Hell
// falls, hell cracks, and behind the throne a portal opens.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    public sealed class PowerDirector : MonoBehaviour
    {
        public List<Beacon> beacons = new List<Beacon>();
        public List<LegionBand> bands = new List<LegionBand>();
        public Transform armyStart, gate;
        public RobotFactory armyMaker;
        public HellLord lord;
        public Transform ceiling;
        public Rift portal;
        public int army = 8;

        Hero hero;
        GameState st;

        IEnumerator Start()
        {
            yield return null;
            hero = Hero.Instance;
            st = GameSession.Instance.State;
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
            Sfx.Loop("battle", 0.35f);
            Sfx.Loop("music_power", 0.6f, true);
            if (!hero.GetComponent<Commander>()) hero.gameObject.AddComponent<Commander>().hero = hero;
            foreach (var b in beacons) b.Light(st.Flag("beacon_" + b.id) || st.Checkpoint == b.id);
            portal.gameObject.SetActive(st.Flag("lord"));
            lord.Defeated += OnLord;
            portal.Entered += _ => st.SetFlag("circle9_done");
            if (st.Flag("lord")) lord.gameObject.SetActive(false);
            hero.Died += () => StartCoroutine(Respawn());
            if (!string.IsNullOrEmpty(st.Checkpoint))
            {
                var b = beacons.FirstOrDefault(x => x.id == st.Checkpoint);
                if (b) hero.Controller.Teleport(b.transform.position + b.transform.forward * 1.6f, b.transform.eulerAngles.y);
            }
            if (st.Flag("alone")) { yield return Alone(false); yield break; }

            // the army: everyone the crab ever made walks with it
            foreach (var id in new[] { ItemId.ScoutDrone, ItemId.Tank, ItemId.Engineer, ItemId.MageOrb, ItemId.Hunter })
                if (!st.Items.Has(id)) st.Items.Add(id);
            var lib = Library.Current;
            if (lib && lib.battleBot)
                for (int i = 0; i < army; i++) armyMaker.Make(hero, lib);
            if (!st.Flag("power_intro"))
            {
                st.SetFlag("power_intro");
                Story.Title("IX. КРУГ ВЛАСТИ", "Небо закрыто. Дальше — трон");
                yield return Wait(3.5f);
                Story.Think("Над головой — не небо, а днище крепости. Ад здесь — это одна огромная машина.", 4.5f);
                yield return Wait(5);
                Story.AI("Носитель, оглянись. Все, кого ты сделал, — здесь. SCOUT, TANK, ИНЖЕНЕР, МАГ, HUNTER. И армия.", 5);
                yield return Wait(5.5f);
                Story.Think("Я пришёл не один. Всё началось с маленького краба на пляже — а теперь за мной идёт целый мир.", 5);
                yield return Wait(5.5f);
            }
            if (!st.Items.Has(ItemId.MatterLord))
            {
                st.Learn(ItemId.MatterLord);
                Story.Hint("Новый чертёж: Повелитель материи — всё, что я умею, во мне одном");
                while (!st.Items.Has(ItemId.MatterLord))
                {
                    if (Story.Objective != LordTask) Story.Task(LordTask);
                    yield return Wait(0.5f);
                }
            }
            if (st.SetFlag("matter_lord"))
            {
                Story.Think("Крылья света. Броня машины. Голос ИИ — внутри. А лицо… то же. Моё.", 5);
                yield return Wait(5.5f);
            }
            Story.Task("Вести армию по мосту к цитадели (G — в атаку)");
            while (Vector3.Distance(hero.transform.position, gate.position) > 10) yield return Wait(0.3f);
            yield return Alone(true);
        }

        const string LordTask = "Стать Повелителем материи (Tab)";

        /// <summary>At the gate: the army stays, the crab goes on alone.</summary>
        IEnumerator Alone(bool show)
        {
            st.SetFlag("alone");
            GameSession.Instance.Save();
            Squad.Give(Squad.Orders.Hold, gate.position - gate.forward * 6);
            if (show)
            {
                Story.AI("Барьер у ворот режет все сигналы. Мы не пройдём. Иди, носитель. Мы будем ждать здесь.", 5);
                yield return Wait(5.5f);
                Story.Think("Последний бой — мой. Так и должно быть.", 4);
                yield return Wait(3);
            }
            // the companions stay behind the barrier
            foreach (var c in new MonoBehaviour[] { hero.Drone, hero.Tank, hero.Engineer, hero.Mage, hero.Hunter })
                if (c) c.gameObject.SetActive(false);
            foreach (var b in BattleBot.All.ToArray()) if (b) b.gameObject.SetActive(false);
            if (!st.Flag("lord"))
            {
                lord.awake = true;
                Story.Task("Войти в тронный зал");
            }
        }

        void OnLord()
        {
            st.SetFlag("lord");
            GameSession.Instance.Save();
            StartCoroutine(Collapse());
        }

        /// <summary>Hell cracks: the roof of the fortress breaks, the throne splits, a portal opens.</summary>
        IEnumerator Collapse()
        {
            yield return Wait(2);
            Sfx.Loop("music_power", 0, true);
            Story.Title("АД ПАЛ", "");
            for (float t = 0; t < 5; t += Time.deltaTime)
            {
                hero.Cam.Shake(0.8f);
                if (ceiling) ceiling.position += Vector3.up * Time.deltaTime * 8;
                if (UnityEngine.Random.value < Time.deltaTime * 6)
                    Fx.Burst(hero.transform.position + new Vector3(UnityEngine.Random.Range(-30f, 30f), UnityEngine.Random.Range(5f, 25f), UnityEngine.Random.Range(-10f, 40f)), new Color(1f, 0.5f, 0.2f), 40, 6, 0.4f, 2, true, 1);
                yield return null;
            }
            portal.gameObject.SetActive(true);
            portal.transform.localScale = Vector3.one * 3;
            Fx.Flash(portal.Point, new Color(0.7f, 0.85f, 1f), 20, 40, 1.5f);
            Sfx.Loop("rift", 0.8f);
            Story.AI("Трон раскололся. За ним — проход. Не в ад. Куда-то… дальше.", 4.5f);
            yield return Wait(5);
            Story.Task("Войти в портал за троном");
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
