// The ancient machine (circle of Despondency): the remains of a giant robot — the body the last
// bearer of the Spark wore. Its AI is still in it. Touched, it wakes and speaks; from then on it
// is the crab's forge down here, and its AI goes with the crab.
using System;
using System.Collections;
using UnityEngine;

namespace Crab.Game
{
    public sealed class AncientMachine : Interactable
    {
        public Light eye;
        public float area = 7;
        public bool awakened;
        public event Action Awakened;

        public override string Prompt => awakened ? "[E] Древняя машина — собрать сложное" : "[E] Коснуться древней машины";
        public override Vector3 Point => transform.position + Vector3.up * 2;

        void Start()
        {
            if (eye) eye.intensity = awakened ? 4 : 0;
        }

        public override void Use(Hero hero)
        {
            if (awakened) { hero.Power.SetMenu(true); return; }
            StartCoroutine(Wake(hero));
        }

        IEnumerator Wake(Hero hero)
        {
            hero.Controller.frozen = true;
            hero.Combat.locked = true;
            Sfx.Play("spark", Point, 0.8f, 0.4f);
            Fx.Stream(hero.Core.position, transform, new Color(1f, 0.9f, 0.6f), 20);
            hero.Cam.Shot(transform.position + transform.forward * 7 + Vector3.up * 3, transform.position + Vector3.up * 2.5f);
            for (float t = 0; t < 2; t += Time.deltaTime)
            {
                if (eye) eye.intensity = Mathf.PingPong(t * 6, 1) * 4;
                yield return null;
            }
            if (eye) eye.intensity = 4;
            string[] lines =
            {
                "…Сигнал Искры… обнаружен. Перезагрузка.",
                "Я — ИИ сопровождения. Модель «Страж-9». Мой носитель нёс Искру до тебя.",
                "Мы дошли до девятого круга. Он не выжил. Я ждал здесь… 4117 лет.",
                "Ты — краб. Неожиданно. Но Искра выбрала тебя. Значит, я — с тобой.",
            };
            foreach (var l in lines)
            {
                Story.AI(l, 4.5f);
                for (float t = 0; t < 4.8f; t += Time.deltaTime) yield return null;
            }
            hero.Cam.SetMode(CamMode.Third);
            hero.Controller.frozen = false;
            hero.Combat.locked = false;
            Wake(false);
        }

        /// <summary>Awake now (after the scene, or a loaded game).</summary>
        public void Wake(bool quiet)
        {
            if (awakened) return;
            awakened = true;
            if (eye) eye.intensity = 4;
            Awakened?.Invoke();
        }

        void Update()
        {
            var hero = Hero.Instance;
            if (hero && GameSession.Instance && awakened && Vector3.Distance(hero.transform.position, transform.position) < area) hero.State.AtWorkshop = true;
            else if (hero && GameSession.Instance && awakened) hero.State.AtWorkshop = false;
        }
    }
}
