// What the crab can make, and how it fights with it. The crab learns the blueprints one by
// one as the story goes (the stick right after the Spark, the pistol by the end of Limbo).
using System;
using System.Collections.Generic;
using System.Linq;

namespace Crab.Core
{
    public enum ItemId { None, Stick, Stone, Crowbar, Knife, Pistol, Ammo, MachineGun, ScrapArmor, ScoutDrone, HeavyArmor, EnergyRifle, EnergyCell, EnergyShield, Tank, SparkCore, EnergyBlade, GravityGun, Portal, CyberFrame, Hunter, Constructor, Engineer, MageOrb, SquadPlating, SquadFire, Factory, TruthLens, Bridgework }

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
        /// <summary>Fires while the button is held.</summary>
        public bool auto;
        /// <summary>Worn on the body (armour) rather than held.</summary>
        public bool armor;
        /// <summary>Becomes a companion beside the crab (a drone).</summary>
        public bool companion;
        /// <summary>Too complex to make on the go: only at the workshop.</summary>
        public bool workshop;
        /// <summary>What a gun fires (bullets for the scrap guns, cells for the energy rifle).</summary>
        public ItemId ammo = ItemId.Ammo;
        /// <summary>Energy shots go through golden armour and shields.</summary>
        public bool energy;
        /// <summary>A gear ability rather than a thing in the claw (the shield).</summary>
        public bool gear;
        /// <summary>Throws what it hits away (the gravity gun).</summary>
        public bool gravity;
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
            new Blueprint { id = ItemId.Ammo, title = "Патроны", hint = "Восемь штук из одного куска металла.", cost = new Cost(0, 0, 1), yield = 8, stack = true },
            new Blueprint { id = ItemId.MachineGun, title = "Пулемёт", hint = "Настоящее оружие. Держи — и он стреляет, пока есть патроны.", cost = new Cost(1, 4, 10),
                kind = WeaponKind.Gun, damage = 9, range = 45, cooldown = 0.09f, auto = true },
            new Blueprint { id = ItemId.ScrapArmor, title = "Броня из металлолома", hint = "Пластины на панцирь. Удары держит втрое легче… ну, почти.", cost = new Cost(0, 2, 12), armor = true },
            new Blueprint { id = ItemId.ScoutDrone, title = "Дрон-разведчик SCOUT", hint = "Маленький, летучий, мой. Стреляет по тем, кто рядом.", cost = new Cost(1, 2, 8), companion = true },
            new Blueprint { id = ItemId.HeavyArmor, title = "Тяжёлая броня", hint = "Сталь, золото и кристальные вставки. Бронекраб.", cost = new Cost(0, 4, 14, 6, 3), armor = true, workshop = true },
            new Blueprint { id = ItemId.EnergyRifle, title = "Энергоружьё", hint = "Кристалл фокусирует энергию в луч. Пробивает золотую броню.", cost = new Cost(0, 2, 8, 2, 4, 2),
                kind = WeaponKind.Gun, damage = 30, range = 60, cooldown = 0.25f, ammo = ItemId.EnergyCell, energy = true, workshop = true },
            new Blueprint { id = ItemId.EnergyCell, title = "Энергоячейки", hint = "Десять зарядов из одной единицы энергии.", cost = new Cost(0, 0, 0, 0, 0, 1), yield = 10, stack = true },
            new Blueprint { id = ItemId.EnergyShield, title = "Энергощит", hint = "Купол на несколько секунд (Q). Ничто не пройдёт.", cost = new Cost(0, 2, 6, 2, 4, 3), gear = true, workshop = true },
            new Blueprint { id = ItemId.Tank, title = "Робот TANK", hint = "Тяжёлый, упрямый, мой. Идёт впереди и бьёт.", cost = new Cost(0, 4, 18, 6, 4, 6), companion = true, workshop = true },
            new Blueprint { id = ItemId.SparkCore, title = "Ядро Искры", hint = "Кристалл на панцире, через который Искра течёт наружу. Теперь материю можно лепить из энергии.", cost = new Cost(0, 0, 0, 0, 6, 8), gear = true },
            new Blueprint { id = ItemId.EnergyBlade, title = "Энергоклинок", hint = "Лезвие из чистой энергии. Режет и золото, и зеркала.", cost = new Cost(0, 0, 2, 0, 2, 4),
                kind = WeaponKind.Melee, damage = 42, range = 1.7f, cooldown = 0.38f, energy = true },
            new Blueprint { id = ItemId.GravityGun, title = "Гравипушка", hint = "Хватает пространство и швыряет. Враги летят кувырком.", cost = new Cost(0, 2, 6, 0, 4, 4),
                kind = WeaponKind.Gun, damage = 18, range = 35, cooldown = 0.7f, ammo = ItemId.EnergyCell, energy = true, gravity = true },
            new Blueprint { id = ItemId.CyberFrame, title = "Кибер-каркас", hint = "Механические лапы на спине, глаз-сканер, встроенный вычислитель. Я больше не один в своей голове.", cost = new Cost(0, 4, 14, 4, 6, 6), gear = true, workshop = true },
            new Blueprint { id = ItemId.Hunter, title = "Дрон HUNTER", hint = "Длинный ствол, холодный глаз. Бьёт издалека, пока враг меня ещё не видит.", cost = new Cost(0, 2, 10, 2, 4, 4), companion = true, workshop = true },
            new Blueprint { id = ItemId.Constructor, title = "Конструктор отряда", hint = "Я больше не делаю вещи по одной. Я проектирую тех, кто будет рядом.", cost = new Cost(0, 6, 16, 6, 8, 8), gear = true, workshop = true },
            new Blueprint { id = ItemId.Engineer, title = "ИНЖЕНЕР", hint = "Чинит меня и TANK, ставит турели, когда жарко.", cost = new Cost(0, 4, 12, 2, 4, 4), companion = true, workshop = true },
            new Blueprint { id = ItemId.MageOrb, title = "МАГ", hint = "Сфера из кристалла и энергии: бьёт молнией по нескольким сразу и подпитывает меня энергией.", cost = new Cost(0, 2, 4, 4, 8, 8), companion = true, workshop = true },
            new Blueprint { id = ItemId.SquadPlating, title = "Броня отряда", hint = "Все мои спутники держат удар почти вдвое лучше.", cost = new Cost(0, 4, 16, 4, 4, 0), gear = true, workshop = true },
            new Blueprint { id = ItemId.SquadFire, title = "Огонь отряда", hint = "Все мои спутники бьют в полтора раза сильнее.", cost = new Cost(0, 2, 8, 4, 6, 6), gear = true, workshop = true },
            new Blueprint { id = ItemId.Factory, title = "Фабрика роботов", hint = "Не одна вещь и не один спутник — поток. Фабрика сама собирает мне армию.", cost = new Cost(0, 10, 24, 8, 8, 10), gear = true, workshop = true },
            new Blueprint { id = ItemId.TruthLens, title = "Взгляд истины", hint = "Линза ИИ: на миг показывает, что настоящее, а что — ложь (T).", cost = new Cost(0, 2, 4, 2, 6, 6), gear = true },
            new Blueprint { id = ItemId.Bridgework, title = "Мосты из материи", hint = "Я строю путь там, где его нет (B): плита под лапы, из камня и металла.", cost = new Cost(0, 8, 12, 0, 4, 6), gear = true },
            new Blueprint { id = ItemId.Portal, title = "Портал", hint = "Шаг сквозь пространство (F) — туда, куда смотрю.", cost = new Cost(0, 0, 0, 0, 3, 5), gear = true },
        };

        public static Blueprint Get(ItemId id) => All.FirstOrDefault(b => b.id == id);
    }
}
