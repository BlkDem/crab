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
    }
}
