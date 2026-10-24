// The truth lens (circle of Lies): the AI's scanner turned on the world itself. T shows for a few
// seconds what is real and what is a lie — fake bridges glow red, hidden real ones blue, mimics
// show their teeth, the true one among copies is marked.
using UnityEngine;

namespace Crab.Game
{
    public sealed class TruthLens : MonoBehaviour
    {
        public static bool Active { get; private set; }
        public Hero hero;
        public float duration = 6, recharge = 6;
        float activeT, cooldown;

        public float Cooldown => cooldown;
        public bool On => activeT > 0;

        void Update()
        {
            float dt = Time.deltaTime;
            cooldown = Mathf.Max(0, cooldown - dt);
            if (activeT > 0)
            {
                activeT -= dt;
                if (activeT <= 0) { Active = false; Sfx.Play("click", null, 0.6f, 0.6f); }
            }
            if (hero && hero.Alive && !hero.Combat.locked && Time.timeScale > 0 && Controls.Truth.WasPressedThisFrame()) Look();
        }

        /// <summary>Open the lens (the T key; tests call it).</summary>
        public bool Look()
        {
            if (activeT > 0 || cooldown > 0) return false;
            activeT = duration;
            cooldown = duration + recharge;
            Active = true;
            Sfx.Play("spark", null, 0.5f, 1.8f);
            Story.Flash(new Color(0.4f, 0.9f, 1f, 0.35f), 0.4f);
            return true;
        }

        void OnDestroy() => Active = false;
    }
}
