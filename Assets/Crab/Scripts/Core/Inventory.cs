// What the crab carries: one of each tool, stacks of stones and ammo, the item in its claw.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Crab.Core
{
    public sealed class Inventory
    {
        readonly Dictionary<ItemId, int> counts = new Dictionary<ItemId, int>();
        public ItemId Equipped { get; private set; }
        public event Action Changed;

        public const int MaxStones = 8, MaxAmmo = 60;

        public int Count(ItemId id) => counts.TryGetValue(id, out int n) ? n : 0;
        public bool Has(ItemId id) => Count(id) > 0;

        /// <summary>Weapons in the order the claw cycles through them.</summary>
        public IEnumerable<ItemId> Weapons => Blueprints.All.Where(b => b.kind != WeaponKind.None && Has(b.id)).Select(b => b.id);

        public int Limit(ItemId id) => id == ItemId.Stone ? MaxStones : id == ItemId.Ammo ? MaxAmmo : 1;

        /// <summary>Whether making this now would be a waste (a tool already held, a full stack).</summary>
        public bool Full(ItemId id) => Count(id) >= Limit(id);

        public void Add(ItemId id, int n = 1)
        {
            counts[id] = Math.Min(Limit(id), Count(id) + n);
            if (Equipped == ItemId.None && Blueprints.Get(id)?.kind != WeaponKind.None) Equipped = id;
            Changed?.Invoke();
        }

        public bool Take(ItemId id, int n = 1)
        {
            if (Count(id) < n) return false;
            counts[id] = Count(id) - n;
            if (Equipped == id && Count(id) == 0) Equipped = Weapons.FirstOrDefault();
            Changed?.Invoke();
            return true;
        }

        public void Equip(ItemId id)
        {
            if (id != ItemId.None && !Has(id)) return;
            Equipped = id;
            Changed?.Invoke();
        }

        /// <summary>The next (or previous) weapon in the claw.</summary>
        public void Cycle(int dir)
        {
            var list = Weapons.ToList();
            if (list.Count == 0) return;
            int i = list.IndexOf(Equipped);
            Equip(list[((i < 0 ? 0 : i + dir) % list.Count + list.Count) % list.Count]);
        }

        public Dictionary<ItemId, int> Snapshot() => new Dictionary<ItemId, int>(counts);

        public void Restore(IEnumerable<KeyValuePair<ItemId, int>> items, ItemId equipped)
        {
            counts.Clear();
            foreach (var kv in items) counts[kv.Key] = kv.Value;
            Equipped = Has(equipped) ? equipped : Weapons.FirstOrDefault();
            Changed?.Invoke();
        }
    }
}
