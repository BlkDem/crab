// Matter: what the crab draws out of things and turns into other things. Three kinds in the
// first circles (organic, mineral, metal); in the circle of Greed come gold, crystals and
// energy. Each kind has a limit the crab can carry.
using System;

namespace Crab.Core
{
    public enum Matter { Organic, Mineral, Metal, Gold, Crystal, Energy }

    /// <summary>How much of each kind of matter something costs.</summary>
    [Serializable]
    public struct Cost
    {
        public int organic, mineral, metal, gold, crystal, energy;

        public Cost(int organic, int mineral, int metal, int gold = 0, int crystal = 0, int energy = 0)
        {
            this.organic = organic;
            this.mineral = mineral;
            this.metal = metal;
            this.gold = gold;
            this.crystal = crystal;
            this.energy = energy;
        }

        public int this[Matter m] => m switch
        {
            Matter.Organic => organic,
            Matter.Mineral => mineral,
            Matter.Metal => metal,
            Matter.Gold => gold,
            Matter.Crystal => crystal,
            _ => energy,
        };

        public override string ToString()
        {
            var parts = new System.Collections.Generic.List<string>();
            foreach (var m in MatterStore.Kinds)
                if (this[m] > 0) parts.Add($"{MatterStore.Title(m).ToLower()} {this[m]}");
            return string.Join(", ", parts);
        }
    }

    public sealed class MatterStore
    {
        public static readonly Matter[] Kinds = { Matter.Organic, Matter.Mineral, Matter.Metal, Matter.Gold, Matter.Crystal, Matter.Energy };
        public static string Title(Matter m) => m switch
        {
            Matter.Organic => "Органика",
            Matter.Mineral => "Минерал",
            Matter.Metal => "Металл",
            Matter.Gold => "Золото",
            Matter.Crystal => "Кристаллы",
            _ => "Энергия",
        };
        /// <summary>The first three are always shown; the rest once the crab has met them.</summary>
        public static bool Basic(Matter m) => (int)m < 3;

        readonly int[] amounts = new int[Kinds.Length];
        public int Capacity = 30;
        public event Action Changed;

        public int this[Matter m] => amounts[(int)m];

        /// <summary>Take in up to n units; returns how many fitted.</summary>
        public int Add(Matter m, int n)
        {
            int room = Math.Max(0, Capacity - amounts[(int)m]);
            int took = Math.Min(room, Math.Max(0, n));
            if (took > 0)
            {
                amounts[(int)m] += took;
                Changed?.Invoke();
            }
            return took;
        }

        /// <summary>Give up to n units away (stolen, paid for a door); returns how many went.</summary>
        public int Remove(Matter m, int n)
        {
            int took = Math.Min(amounts[(int)m], Math.Max(0, n));
            if (took > 0)
            {
                amounts[(int)m] -= took;
                Changed?.Invoke();
            }
            return took;
        }

        public bool CanPay(Cost c)
        {
            foreach (var m in Kinds)
                if (amounts[(int)m] < c[m]) return false;
            return true;
        }

        public bool Pay(Cost c)
        {
            if (!CanPay(c)) return false;
            foreach (var m in Kinds) amounts[(int)m] -= c[m];
            Changed?.Invoke();
            return true;
        }

        public void Set(int organic, int mineral, int metal, int gold = 0, int crystal = 0, int energy = 0)
        {
            var c = new Cost(organic, mineral, metal, gold, crystal, energy);
            foreach (var m in Kinds) amounts[(int)m] = Math.Min(Capacity, c[m]);
            Changed?.Invoke();
        }
    }
}
