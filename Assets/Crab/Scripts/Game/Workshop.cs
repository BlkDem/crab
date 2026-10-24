// The crab's first workshop (circle of Greed): a ruined forge it rebuilds from matter. Once it
// stands, the crab can make the complex things there — heavy armour, the energy rifle, the
// shield, later the TANK. Standing near it counts as being at the workshop.
using System;
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    public sealed class Workshop : Interactable
    {
        public Cost cost = new Cost(0, 8, 15, 5);
        [Tooltip("The part that appears when it is built (the forge, the anvil, the crane)")]
        public Transform build;
        public Light forgeLight;
        public float area = 7;
        public bool built;
        public event Action Built;
        float buildT, grow = -1;
        const float BuildTime = 3;

        public override string Prompt => built
            ? "[E] Мастерская — создать сложное"
            : $"[E] Построить мастерскую ({cost}) — держать{(buildT > 0 ? $" {Mathf.RoundToInt(buildT / BuildTime * 100)}%" : "")}";
        public override bool Hold => !built;
        public override Vector3 Point => transform.position + Vector3.up * 1.2f;

        void Start()
        {
            if (build) build.gameObject.SetActive(built);
            if (forgeLight) forgeLight.enabled = built;
        }

        public override void Use(Hero hero)
        {
            if (built) hero.Power.SetMenu(true);
        }

        public override bool Channel(Hero hero, float dt)
        {
            var st = hero.State;
            if (buildT <= 0 && !st.Matter.CanPay(cost))
            {
                Story.Hint($"Для мастерской нужно: {cost}");
                return true;
            }
            buildT += dt;
            if (UnityEngine.Random.value < dt * 14) Fx.Stream(hero.Core.position, transform, new Color(1f, 0.85f, 0.45f), 3);
            if (buildT < BuildTime) return false;
            if (!st.Matter.Pay(cost)) { buildT = 0; return true; }
            Build(true);
            return true;
        }

        public override void StopChannel(Hero hero)
        {
            if (!built) buildT = 0;
        }

        /// <summary>Make it stand (the crab built it, or a loaded game already has it).</summary>
        public void Build(bool show)
        {
            if (built) return;
            built = true;
            if (build) build.gameObject.SetActive(true);
            if (forgeLight) forgeLight.enabled = true;
            if (show)
            {
                grow = 0;
                Sfx.Play("create", Point, 1, 0.6f);
                Sfx.Play("slam", Point, 0.6f, 1.4f);
                Fx.Flash(Point, new Color(1f, 0.85f, 0.5f), 10, 12, 0.8f);
                if (build) Fx.Assemble(build, new Color(1f, 0.85f, 0.45f), 1.5f);
            }
            Built?.Invoke();
        }

        void Update()
        {
            if (grow >= 0 && build)
            {
                grow += Time.deltaTime / 1.5f;
                build.localScale = Vector3.one * Mathf.SmoothStep(0, 1, Mathf.Min(1, grow));
                if (grow >= 1) grow = -1;
            }
            if (forgeLight && built) forgeLight.intensity = 2.5f + Mathf.PerlinNoise(Time.time * 3, 0) * 1.5f;
            var hero = Hero.Instance;
            if (hero && GameSession.Instance)
                hero.State.AtWorkshop = built && Vector3.Distance(hero.transform.position, transform.position) < area;
        }
    }
}
