using System.Collections.Generic;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    /// <summary>Debris the crab draws matter from: driftwood and planks (organic), stones (mineral), scrap (metal).</summary>
    public sealed class MatterSource : Interactable
    {
        public Matter kind;
        public int amount = 6;
        [Tooltip("Comes back after this many seconds (0: never)")]
        public float regrow;
        [Tooltip("Blueprints learned the first time the crab draws matter from such a thing")]
        public List<ItemId> teaches = new List<ItemId>();
        public string firstThought;

        int left;
        float tick, regrowT;
        Vector3 scale0;
        Collider[] cols;
        Renderer[] rends;

        void Awake()
        {
            left = amount;
            scale0 = transform.localScale;
            cols = GetComponentsInChildren<Collider>();
            rends = GetComponentsInChildren<Renderer>();
        }

        public static Color ColorOf(Matter m) => m switch
        {
            Matter.Organic => new Color(0.55f, 1f, 0.45f),
            Matter.Mineral => new Color(0.75f, 0.85f, 1f),
            _ => new Color(1f, 0.7f, 0.35f),
        };

        public override string Prompt => $"[E] Втянуть материю — {MatterStore.Title(kind).ToLower()} ({left})";
        public override bool Usable => left > 0;
        public override bool Hold => true;

        public override bool Channel(Hero hero, float dt)
        {
            tick += dt;
            if (tick < 0.16f) return left <= 0;
            tick = 0;
            var st = GameSession.Instance.State;
            if (st.Matter.Add(kind, 1) == 0)
            {
                Story.Hint($"{MatterStore.Title(kind)}: больше не унести. Создай что-нибудь (Tab)");
                return true;
            }
            left--;
            Fx.Stream(Point, hero.Core, ColorOf(kind), 5);
            Sfx.Play("absorb", Point, 0.4f, 0.9f + 0.3f * (1 - left / (float)amount));
            transform.localScale = scale0 * Mathf.Lerp(0.35f, 1, left / (float)amount);
            if (teaches.Count > 0)
            {
                bool any = false;
                foreach (var id in teaches)
                    if (!st.Knows(id)) { st.Learn(id); any = true; }
                if (any && !string.IsNullOrEmpty(firstThought)) Story.Think(firstThought, 5);
            }
            if (left > 0) return false;
            Fx.Burst(Point, ColorOf(kind), 30, 2.5f, 0.1f);
            foreach (var c in cols) c.enabled = false;
            foreach (var r in rends) r.enabled = false;
            regrowT = regrow;
            return true;
        }

        void Update()
        {
            if (left > 0 || regrow <= 0) return;
            regrowT -= Time.deltaTime;
            if (regrowT > 0) return;
            left = amount;
            transform.localScale = scale0;
            foreach (var c in cols) c.enabled = true;
            foreach (var r in rends) r.enabled = true;
            Fx.Burst(Point, ColorOf(kind), 20, 1.5f, 0.08f);
        }
    }
}
