// The prologue on the beach: an ordinary crab's day (find food, hide from the gull, run from the
// big wave), then a storm, the Spark falling from the sky into the crab — and the first
// thoughts of a creature that has never thought before. It learns to draw matter in and makes
// its first thing, a stick; a rift opens, and the way leads down into Limbo.
using System.Collections;
using System.Collections.Generic;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    public sealed class PrologueDirector : MonoBehaviour
    {
        public List<Edible> morsels = new List<Edible>();
        public Collider shelter;
        public Gull gull;
        public Wave wave;
        public Transform safeLine;
        public Storm storm;
        public MatterSource driftwood;
        public Rift rift;
        public Light sparkLight;

        Hero hero;
        int eaten;

        IEnumerator Start()
        {
            yield return null;
            hero = Hero.Instance;
            var st = GameSession.Instance.State;
            Sfx.Loop("surf", 0.7f);
            Sfx.Loop("wind", 0.25f);
            Sfx.Loop("music_beach", 0.5f, true);
            rift.gameObject.SetActive(false);
            if (sparkLight) sparkLight.enabled = false;
            gull.heroSheltered = () => shelter && shelter.bounds.Contains(hero.transform.position + Vector3.up * 0.2f);
            foreach (var m in morsels) m.Eaten += () => eaten++;
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
            hero.Died += () => StartCoroutine(Revive());

            if (!st.Flag("spark"))
            {
                Story.Title("КРАБ ПРОТИВ АДА", "Пролог. Берег");
                yield return Wait(3);
                // an ordinary day
                Story.Task("Найти еду (0/3)");
                Story.Hint("WASD — идти, мышь — смотреть, Shift — бежать, Пробел — прыжок, E — съесть");
                while (eaten < 3)
                {
                    Story.Task($"Найти еду ({eaten}/3)");
                    yield return null;
                }
                // the gull
                Story.Task("Спрятаться от чайки под камнем!");
                Story.Hint("Плоский камень на ножках — там, где светится круг");
                Sfx.Play("gull", gull.transform.position, 1);
                // a ring of light under the shelter while the gull hunts
                var c = shelter.bounds.center;
                var mark = BossFx.Marker(new Vector3(c.x, hero.transform.position.y, c.z), 1.8f, new Color(0.5f, 1f, 0.7f, 0.8f));
                var pulse = StartCoroutine(PulseMark(mark));
                yield return Wait(2);
                bool safe = false;
                bool first = true;
                while (!safe)
                {
                    yield return Wait(first ? 5 : 3);   // the first time, a moment more to find the stone
                    first = false;
                    bool done = false, hit = false;
                    System.Action<bool> h = x => { done = true; hit = x; };
                    gull.DiveDone += h;
                    gull.Dive();
                    while (!done) yield return null;
                    gull.DiveDone -= h;
                    safe = !hit && gull.heroSheltered();
                    if (!safe) Story.Hint(hit ? "Больно! Под плоским камнем чайка не достанет." : "Ещё заход! Под камень!");
                }
                StopCoroutine(pulse);
                Destroy(mark);
                Story.Hint("Чайка улетела ни с чем.");
                gull.circleRadius = 30;
                gull.height = 20;
                yield return Wait(2);
                // the big wave
                Story.Task("Бежать от волны вверх по берегу!");
                bool escaped = false;
                while (!escaped)
                {
                    yield return Wait(1.5f);
                    bool done = false, caught = false;
                    System.Action<bool> h = x => { done = true; caught = x; };
                    wave.Done += h;
                    wave.Roll();
                    while (!done) yield return null;
                    wave.Done -= h;
                    escaped = !caught;
                    if (caught) Story.Hint("Волна утащила назад! Беги к пальмам, выше по берегу.");
                }
                // the storm and the Spark
                Story.Task("");
                storm.Brew(1);
                Sfx.Loop("music_beach", 0, true);
                Sfx.Loop("wind", 0.6f);
                yield return Wait(6);
                yield return SparkFalls(st);
            }
            else
            {
                if (sparkLight) sparkLight.enabled = true;
                storm.amount = 0.7f;
                storm.Brew(0.7f);
            }

            // the power: drawing matter in and making a thing of it
            if (!st.Knows(ItemId.Stick)) st.Learn(ItemId.Stick);
            if (!st.Items.Has(ItemId.Stick))
            {
                Story.Task("Почувствовать материю: подойти к коряге и держать E");
                while (st.Matter[Matter.Organic] < 2) yield return null;
                Story.Think("Дерево. Оно… отзывается. Я могу сделать из него что угодно.", 4);
                yield return Wait(1);
                Story.Task("Создать палку: Tab — круг создания");
                while (!st.Items.Has(ItemId.Stick)) yield return null;
                yield return Wait(4);
            }
            Story.Think("Я понял. Ад существует.", 3.5f);
            yield return Wait(3.5f);
            Story.Think("И кто-то должен его уничтожить.", 3.5f);
            yield return Wait(3.5f);
            Story.Think("Почему я? Не знаю. Но Искра попала в меня не случайно.", 4.5f);
            rift.gameObject.SetActive(true);
            Sfx.Loop("rift", 0.6f);
            Fx.Flash(rift.Point, new Color(0.6f, 1f, 0.75f), 10, 20, 0.6f);
            Story.Task("Войти в разлом", rift.transform);
            st.SetFlag("prologue_done");
            GameSession.Instance.Save();
        }

        IEnumerator SparkFalls(GameState st)
        {
            var target = hero.transform.position;
            var from = target + new Vector3(-20, 60, -40);
            hero.Controller.frozen = true;
            hero.Combat.locked = true;
            hero.Cam.Shot(target + new Vector3(4, 2.5f, 6), target + Vector3.up * 3);
            // the spark: a bright mote with a trail, falling fast
            var spark = new GameObject("Божья Искра");
            var l = spark.AddComponent<Light>();
            l.color = new Color(1f, 0.92f, 0.6f);
            l.intensity = 8;
            l.range = 12;
            var trail = spark.AddComponent<TrailRenderer>();
            trail.sharedMaterial = Library.Current ? Library.Current.tracer : null;
            trail.time = 0.5f;
            trail.startWidth = 0.6f;
            trail.endWidth = 0;
            trail.startColor = new Color(1, 0.95f, 0.7f);
            trail.endColor = new Color(1, 0.6f, 0.2f, 0);
            Sfx.Play("spark", null, 1);
            for (float t = 0; t < 1; t += Time.deltaTime / 1.6f)
            {
                spark.transform.position = Vector3.Lerp(from, target + Vector3.up * 0.4f, t * t);
                yield return null;
            }
            Destroy(spark, 0.6f);
            storm.Bolt(target);
            Story.Flash(Color.white, 1.5f);
            Fx.Burst(target + Vector3.up * 0.4f, new Color(1f, 0.9f, 0.5f), 120, 8, 0.15f, 1.5f);
            hero.Cam.Shake(2);
            hero.Rig.Hold(ClawPose.Hurt);
            st.SetFlag("spark");
            hero.ShowSpark(true);
            yield return Wait(3);
            // the first thoughts
            hero.Rig.Hold(ClawPose.Think);
            hero.Cam.Shot(target + hero.transform.forward * 1.6f + Vector3.up * 0.7f, target + Vector3.up * 0.35f);
            foreach (var (line, sec) in new[] { ("…", 2f), ("Что… это?", 2.5f), ("Я… думаю?", 3f), ("Я вижу, из чего сделан мир.", 3.5f), ("Песок. Камень. Дерево. Всё это — материя. И она слушается меня.", 4.5f) })
            {
                Story.Think(line, sec);
                yield return Wait(sec + 0.3f);
            }
            hero.Rig.Release(ClawPose.Think);
            hero.Cam.SetMode(CamMode.Third);
            hero.Controller.frozen = false;
            hero.Combat.locked = false;
            storm.Brew(0.7f);
            GameSession.Instance.Save();
        }

        IEnumerator Revive()
        {
            Story.Hint("Краб пал… но краба так просто не взять.");
            yield return Wait(2.5f);
            yield return BossFx.Calm();   // the boss starts over first
            hero.Respawn();
        }

        static IEnumerator PulseMark(GameObject mark)
        {
            for (float t = 0; mark; t += Time.deltaTime) { BossFx.Pulse(mark, 0.5f + 0.5f * Mathf.Sin(t * 4)); yield return null; }
        }

        static IEnumerator Wait(float s)
        {
            for (float t = 0; t < s; t += Time.deltaTime) yield return null;
        }
    }
}
