// The crab as a general (from the circle of Violence): G sends the squad and the army at what the
// crab looks at, H calls them back, J tells them to hold where the crab looks. A ring marks the
// order's place for a moment.
using UnityEngine;

namespace Crab.Game
{
    public sealed class Commander : MonoBehaviour
    {
        public Hero hero;

        void Update()
        {
            if (!hero || !hero.Alive || hero.Combat.locked || Time.timeScale == 0) return;
            if (Controls.OrderAttack.WasPressedThisFrame()) Give(Squad.Orders.Attack);
            else if (Controls.OrderFollow.WasPressedThisFrame()) Give(Squad.Orders.Follow);
            else if (Controls.OrderHold.WasPressedThisFrame()) Give(Squad.Orders.Hold);
        }

        /// <summary>Give an order at the point the crab looks at (keys; tests call it).</summary>
        public void Give(Squad.Orders o)
        {
            var at = o == Squad.Orders.Follow ? hero.transform.position : hero.Combat.AimPoint(60, out _);
            Squad.Give(o, at);
            var c = o == Squad.Orders.Attack ? new Color(1f, 0.35f, 0.25f, 0.8f) : o == Squad.Orders.Hold ? new Color(0.4f, 0.8f, 1f, 0.8f) : new Color(0.5f, 1f, 0.6f, 0.8f);
            var mark = BossFx.Marker(at, 2, c);
            Destroy(mark, 1.2f);
            Sfx.Play("click", at, 1, o == Squad.Orders.Attack ? 1.4f : 0.9f);
            Story.Hint(o == Squad.Orders.Attack ? "Отряд: в атаку!" : o == Squad.Orders.Hold ? "Отряд: держать позицию." : "Отряд: ко мне!");
        }
    }
}
