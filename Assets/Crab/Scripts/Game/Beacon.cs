using System.Collections.Generic;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    /// <summary>A beacon: lights up when the crab touches it — the place to come back to, and a save.</summary>
    public sealed class Beacon : Interactable
    {
        public string id;
        public Light glow;
        bool lit;

        public bool Lit => lit;
        public override string Prompt => lit ? "[E] Отдохнуть у маяка" : "[E] Зажечь маяк";

        public override void Use(Hero hero)
        {
            var s = GameSession.Instance;
            bool first = !lit;
            Light(true);
            s.State.Checkpoint = id;
            s.State.SetFlag("beacon_" + id);
            s.State.Health.Heal(1000);
            s.Save();
            Sfx.Play("beacon", transform.position);
            Fx.Burst(transform.position + Vector3.up * 1.4f, new Color(0.5f, 1f, 0.7f), 40, 2f, 0.12f);
            Story.Hint(first ? "Маяк зажжён. Здесь я вернусь, если паду." : "Силы восстановлены. Игра сохранена.");
        }

        public void Light(bool on)
        {
            lit = on;
            if (glow) glow.intensity = on ? 3 : 0.4f;
        }

        public override Vector3 Point => transform.position + Vector3.up * 1.4f;
    }
}
