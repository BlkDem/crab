// What the crab can make, and how it fights with it. The crab learns the blueprints one by
// one as the story goes (the stick right after the Spark, the pistol by the end of Limbo).
using System;
using System.Collections.Generic;
using System.Linq;

namespace Crab.Core
{
    public enum ItemId { None, Stick, Stone, Crowbar, Knife, Pistol, Ammo }

    public enum WeaponKind { None, Melee, Throw, Gun }

    public sealed class Blueprint
    {
        public ItemId id;
        public string title, hint;
        public Cost cost;
        /// <summary>How many one creation gives (ammo comes in clips).</summary>
        public int yield = 1;
        /// <summary>Kept in a stack (stones, ammo) rather than as one tool.</summary>
        public bool stack;
        public WeaponKind kind;
        public float damage, range, cooldown;
    }

    public static class Blueprints
    {
        public static readonly IReadOnlyList<Blueprint> All = new[]
        {
            new Blueprint { id = ItemId.Stick, title = "Палка", hint = "Первое, что я создал. Бьёт слабо, но далеко.", cost = new Cost(2, 0, 0),
                kind = WeaponKind.Melee, damage = 12, range = 1.3f, cooldown = 0.55f },
            new Blueprint { id = ItemId.Stone, title = "Камень", hint = "Метнуть во врага.", cost = new Cost(0, 1, 0), yield = 1, stack = true,
                kind = WeaponKind.Throw, damage = 18, range = 14, cooldown = 0.6f },
            new Blueprint { id = ItemId.Crowbar, title = "Лом", hint = "Тяжёлый. Медленный удар, большой урон.", cost = new Cost(0, 0, 3),
                kind = WeaponKind.Melee, damage = 30, range = 1.4f, cooldown = 0.9f },
            new Blueprint { id = ItemId.Knife, title = "Нож", hint = "Быстрые удары.", cost = new Cost(0, 1, 2),
                kind = WeaponKind.Melee, damage = 16, range = 1.0f, cooldown = 0.3f },
            new Blueprint { id = ItemId.Pistol, title = "Самодельный пистолет", hint = "Стреляет тем, что я из металла сделаю.", cost = new Cost(1, 2, 5),
                kind = WeaponKind.Gun, damage = 22, range = 40, cooldown = 0.32f },
            new Blueprint { id = ItemId.Ammo, title = "Патроны", hint = "Шесть штук из одного куска металла.", cost = new Cost(0, 0, 1), yield = 6, stack = true },
        };

        public static Blueprint Get(ItemId id) => All.FirstOrDefault(b => b.id == id);
    }
}
