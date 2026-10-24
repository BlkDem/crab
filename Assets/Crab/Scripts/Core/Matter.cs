// Matter: what the crab draws out of things and turns into other things. Three kinds in the
// first circles (crystals and energy come later), each with a limit the crab can carry.
using System;

namespace Crab.Core
{
    public enum Matter { Organic, Mineral, Metal }

    /// <summary>How much of each kind of matter something costs.</summary>
    [Serializable]
    public struct Cost
    {
        public int organic, mineral, metal;

        public Cost(int organic, int mineral, int metal)
        {
            this.organic = organic;
            this.mineral = mineral;
            this.metal = metal;
        }

        public int this[Matter m] => m switch { Matter.Organic => organic, Matter.Mineral => mineral, _ => metal };

        public override string ToString()
        {
            var parts = new System.Collections.Generic.List<string>();
            if (organic > 0) parts.Add($"органика {organic}");
            if (mineral > 0) parts.Add($"минерал {mineral}");
            if (metal > 0) parts.Add($"металл {metal}");
            return string.Join(", ", parts);
        }
    }

    public sealed class MatterStore
    {
        public static readonly Matter[] Kinds = { Matter.Organic, Matter.Mineral, Matter.Metal };
        public static string Title(Matter m) => m switch { Matter.Organic => "Органика", Matter.Mineral => "Минерал", _ => "Металл" };

        readonly int[] amounts = new int[3];
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

        public bool CanPay(Cost c) => amounts[0] >= c.organic && amounts[1] >= c.mineral && amounts[2] >= c.metal;

        public bool Pay(Cost c)
        {
            if (!CanPay(c)) return false;
            amounts[0] -= c.organic;
            amounts[1] -= c.mineral;
            amounts[2] -= c.metal;
            Changed?.Invoke();
            return true;
        }

        public void Set(int organic, int mineral, int metal)
        {
            amounts[0] = Math.Min(Capacity, organic);
            amounts[1] = Math.Min(Capacity, mineral);
            amounts[2] = Math.Min(Capacity, metal);
            Changed?.Invoke();
        }
    }
}
