// Despondency: in the dark the crab's will drains away; in light it comes back. With little will
// the world goes grey, the crab drags its legs and the dark whispers; with none, life itself
// fades. The AI of the cyber-frame keeps it company (half the drain).
using Crab.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crab.Game
{
    public sealed class Despair : MonoBehaviour
    {
        public static Despair Instance { get; private set; }
        public Hero hero;
        public Volume grey;
        public float drain = 2.2f, regain = 22;
        public float Will { get; private set; } = 100;
        /// <summary>A storm of whispers: the drain is several times stronger for a while.</summary>
        [System.NonSerialized] public float storm;
        float whisperT = 6, hurtT;

        static readonly string[] Lines =
        {
            "Зачем? Всё равно всё зря.",
            "Ты один. Всегда был один.",
            "Искра ошиблась. Ты просто краб.",
            "Остановись. Здесь тихо. Здесь не больно.",
            "Никто не ждёт тебя наверху.",
            "Свет кончится. Он всегда кончается.",
            "Те, кто нёс её до тебя, тоже верили.",
        };

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Lose(float amount)
        {
            Will = Mathf.Max(0, Will - amount);
            Whisper();
        }

        public void Whisper() => Story.Whisper(Lines[Random.Range(0, Lines.Length)]);

        public void Restore() => Will = 100;
        public void Gain(float amount) => Will = Mathf.Min(100, Will + amount);

        void Update()
        {
            if (!hero) return;
            float dt = Time.deltaTime;
            storm = Mathf.Max(0, storm - dt);
            bool lit = LightZone.Lit(hero.transform.position);
            if (lit) Will = Mathf.Min(100, Will + regain * dt);
            else if (hero.Alive)
            {
                float k = hero.State.Items.Has(ItemId.CyberFrame) ? 0.5f : 1;
                Will = Mathf.Max(0, Will - drain * k * (storm > 0 ? 4 : 1) * dt);
            }
            float low = 1 - Will / 100;
            if (grey) grey.weight = Mathf.Lerp(grey.weight, Mathf.SmoothStep(0, 1, low * 1.3f), 1 - Mathf.Exp(-3 * dt));
            hero.Controller.moodScale = Mathf.Lerp(1, 0.6f, Mathf.Clamp01((40 - Will) / 40));
            whisperT -= dt;
            if (Will < 50 && whisperT <= 0 && !lit)
            {
                whisperT = Random.Range(5f, 9f) * (0.5f + Will / 100);
                Whisper();
            }
            if (Will <= 0 && hero.Alive)
            {
                hurtT -= dt;
                if (hurtT <= 0)
                {
                    hurtT = 1;
                    hero.Hurt(3, hero.transform.position, Vector3.zero);
                }
            }
        }

        void OnDisable()
        {
            if (hero) hero.Controller.moodScale = 1;
        }
    }
}
