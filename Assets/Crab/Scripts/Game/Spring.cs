// The Spring (circle of Envy): a fountain of pure energy on the hill. Touching it (hold E) the crab
// understands it can make matter out of energy itself.
using System;
using UnityEngine;

namespace Crab.Game
{
    public sealed class Spring : Interactable
    {
        public Light glow;
        public bool touched;
        public event Action Touched;
        float t;
        const float Time_ = 3;

        public override string Prompt => $"[E] Прикоснуться к Источнику — держать{(t > 0 ? $" {Mathf.RoundToInt(t / Time_ * 100)}%" : "")}";
        public override bool Usable => !touched;
        public override bool Hold => true;
        public override Vector3 Point => transform.position + Vector3.up * 1.2f;

        public override bool Channel(Hero hero, float dt)
        {
            t += dt;
            if (UnityEngine.Random.value < dt * 16) Fx.Stream(Point, hero.Core, new Color(0.75f, 0.5f, 1f), 3);
            if (t < Time_) return false;
            Touch();
            return true;
        }

        public override void StopChannel(Hero hero)
        {
            if (!touched) t = 0;
        }

        public void Touch()
        {
            if (touched) return;
            touched = true;
            Sfx.Play("spark", Point, 1, 0.8f);
            Fx.Flash(Point, new Color(0.75f, 0.5f, 1f), 14, 16, 1);
            Fx.Burst(Point, new Color(0.8f, 0.6f, 1f), 120, 5, 0.12f, 1.5f);
            Story.Flash(new Color(0.85f, 0.7f, 1f, 0.8f), 1.2f);
            Touched?.Invoke();
        }

        void Update()
        {
            if (glow) glow.intensity = (touched ? 2 : 4) + Mathf.Sin(Time.time * 2) * 1;
        }
    }
}
