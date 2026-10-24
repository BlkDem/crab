// The crab's squad, as a whole: what the companion upgrades give to all of them, and the moment a
// traitor's signal takes them over (circle of Betrayal) — then they turn on the crab until the
// AI wins them back.
using Crab.Core;
using UnityEngine;

namespace Crab.Game
{
    public static class Squad
    {
        static GameState St => GameSession.Instance ? GameSession.Instance.State : null;

        /// <summary>Damage multiplier for every companion.</summary>
        public static float Damage => St != null && St.Items.Has(ItemId.SquadFire) ? 1.5f : 1f;

        /// <summary>Share of a blow a companion takes.</summary>
        public static float Armor => St != null && St.Items.Has(ItemId.SquadPlating) ? 0.55f : 1f;

        static float hackedUntil = -1;

        /// <summary>The companions are under someone else's control right now.</summary>
        public static bool Hacked => Time.time < hackedUntil;

        public static void Hack(float seconds) => hackedUntil = Time.time + seconds;

        public static void Release() => hackedUntil = -1;

        public static readonly Color HackColor = new Color(1f, 0.2f, 0.25f);

        // ------------------------------------------------------------------ orders (the crab-general)
        public enum Orders { Follow, Attack, Hold }
        public static Orders Order { get; private set; } = Orders.Follow;
        /// <summary>Where the order points: the place to attack or to hold.</summary>
        public static Vector3 Point { get; private set; }
        public static event System.Action<Orders> Ordered;

        public static void Give(Orders o, Vector3 at)
        {
            Order = o;
            Point = at;
            Ordered?.Invoke(o);
        }

        /// <summary>The demon nearest to the order's point (for Attack), or null.</summary>
        public static Enemy OrderTarget(float radius = 10)
        {
            if (Order != Orders.Attack) return null;
            Enemy best = null;
            float bd = radius;
            foreach (var e in Enemy.All)
            {
                if (!e || !e.Alive || !e.Visible) continue;
                float d = Vector3.Distance(e.transform.position, Point);
                if (d < bd) { bd = d; best = e; }
            }
            return best;
        }

        /// <summary>Where a companion should keep: by the crab, or at the held point.</summary>
        public static Vector3 Station(Hero hero, Vector3 offset) => Order == Orders.Hold ? Point + offset * 0.6f : hero.transform.position + offset;
    }
}
