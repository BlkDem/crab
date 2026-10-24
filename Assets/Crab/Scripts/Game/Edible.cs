using System.Collections.Generic;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    /// <summary>Something to eat: heals, and in the prologue it is what an ordinary crab's day is about.</summary>
    public sealed class Edible : Interactable
    {
        public float heal = 20;
        public string title = "Еда";
        public event System.Action Eaten;

        public override string Prompt => $"[E] Съесть — {title.ToLower()}";

        public override void Use(Hero hero)
        {
            GameSession.Instance.State.Health.Heal(heal);
            hero.Rig.Play(ClawPose.Eat, 1.0f);
            Sfx.Play("eat", transform.position);
            Fx.Burst(transform.position + Vector3.up * 0.1f, new Color(1, 0.9f, 0.7f), 12, 1.2f, 0.05f, 0.5f, false, 1);
            Eaten?.Invoke();
            gameObject.SetActive(false);
        }
    }
}
