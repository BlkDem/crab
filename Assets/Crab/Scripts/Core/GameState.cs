// Everything about one playthrough: matter, items, health, what the crab knows and where the
// story is. Saved as a plain DTO (the game writes it with JsonUtility).
using System;
using System.Collections.Generic;
using System.Linq;

namespace Crab.Core
{
    [Serializable]
    public sealed class SaveData
    {
        public int version = 1;
        public string scene = "Prologue";
        public string checkpoint = "";
        public int organic, mineral, metal, gold, crystal, energy;
        public List<string> items = new List<string>();     // "Pistol:1"
        public string equipped = "None";
        public List<string> blueprints = new List<string>();
        public List<string> flags = new List<string>();
        public float health = 100;
        public double playSeconds;
    }

    public sealed class GameState
    {
        public readonly MatterStore Matter = new MatterStore();
        public readonly Inventory Items = new Inventory();
        public readonly Health Health = new Health(100);
        readonly HashSet<ItemId> known = new HashSet<ItemId>();
        readonly HashSet<string> flags = new HashSet<string>();
        public string Scene = "Prologue", Checkpoint = "";
        public double PlaySeconds;
        public event Action<Blueprint> Learned;
        public event Action<string> FlagSet;

        public bool Knows(ItemId id) => known.Contains(id);

        /// <summary>The crab's level (its evolution): 1 an ordinary crab, 2 a crab-warrior in scrap armour,
        /// 3 the armoured crab (heavy armour).</summary>
        public int Level => Items.Has(ItemId.Bridgework) ? 8 : Items.Has(ItemId.Factory) ? 7 : Items.Has(ItemId.Constructor) ? 6 : Items.Has(ItemId.CyberFrame) ? 5 : Items.Has(ItemId.SparkCore) ? 4 : Items.Has(ItemId.HeavyArmor) ? 3 : Items.Has(ItemId.ScrapArmor) ? 2 : 1;
        /// <summary>With the Spark's core the crab makes matter out of energy: what it lacks of the
        /// plain kinds (organic, mineral, metal) energy covers, one for one.</summary>
        public bool Mage => Items.Has(ItemId.SparkCore);
        /// <summary>The crab stands at its workshop (set by the game): complex things can be made.</summary>
        public bool AtWorkshop;
        public static string LevelTitle(int level) => level switch { 1 => "Обычный краб", 2 => "Краб-воин", 3 => "Бронекраб", 4 => "Краб-маг", 5 => "Киберкраб", 9 => "Повелитель материи", _ => "Краб-архитектор" };
        public IEnumerable<Blueprint> Known => Blueprints.All.Where(b => known.Contains(b.id));

        public void Learn(ItemId id)
        {
            if (!known.Add(id)) return;
            Learned?.Invoke(Blueprints.Get(id));
        }

        public bool Flag(string f) => flags.Contains(f);

        /// <summary>Set a story flag; true the first time.</summary>
        public bool SetFlag(string f)
        {
            if (!flags.Add(f)) return false;
            FlagSet?.Invoke(f);
            return true;
        }

        public enum CreateResult { Done, Unknown, NoMatter, Full, NeedWorkshop }

        /// <summary>Make one item from matter (the superpower, not a shop).</summary>
        public CreateResult Create(ItemId id)
        {
            var b = Blueprints.Get(id);
            if (b == null || !Knows(id)) return CreateResult.Unknown;
            if (Items.Full(id)) return CreateResult.Full;
            if (b.workshop && !AtWorkshop) return CreateResult.NeedWorkshop;
            if (!Matter.Pay(Mage ? FromEnergy(b.cost) : b.cost)) return CreateResult.NoMatter;
            Items.Add(id, b.yield);
            return CreateResult.Done;
        }

        /// <summary>The cost with the missing plain matter paid in energy (the mage's way).</summary>
        public Cost FromEnergy(Cost c)
        {
            int Short(Matter m) => Math.Max(0, c[m] - Matter[m]);
            int need = Short(Core.Matter.Organic) + Short(Core.Matter.Mineral) + Short(Core.Matter.Metal);
            if (need == 0) return c;
            return new Cost(c.organic - Short(Core.Matter.Organic), c.mineral - Short(Core.Matter.Mineral), c.metal - Short(Core.Matter.Metal), c.gold, c.crystal, c.energy + need);
        }

        public SaveData ToSave() => new SaveData
        {
            scene = Scene,
            checkpoint = Checkpoint,
            organic = Matter[Core.Matter.Organic],
            mineral = Matter[Core.Matter.Mineral],
            metal = Matter[Core.Matter.Metal],
            gold = Matter[Core.Matter.Gold],
            crystal = Matter[Core.Matter.Crystal],
            energy = Matter[Core.Matter.Energy],
            items = Items.Snapshot().Where(kv => kv.Value > 0).Select(kv => $"{kv.Key}:{kv.Value}").ToList(),
            equipped = Items.Equipped.ToString(),
            blueprints = known.Select(k => k.ToString()).ToList(),
            flags = flags.ToList(),
            health = Health.Value,
            playSeconds = PlaySeconds,
        };

        public void Load(SaveData s)
        {
            Scene = string.IsNullOrEmpty(s.scene) ? "Prologue" : s.scene;
            Checkpoint = s.checkpoint ?? "";
            Matter.Set(s.organic, s.mineral, s.metal, s.gold, s.crystal, s.energy);
            var items = new List<KeyValuePair<ItemId, int>>();
            foreach (var e in s.items ?? new List<string>())
            {
                var p = e.Split(':');
                if (p.Length == 2 && Enum.TryParse(p[0], out ItemId id) && int.TryParse(p[1], out int n)) items.Add(new KeyValuePair<ItemId, int>(id, n));
            }
            Enum.TryParse(s.equipped, out ItemId eq);
            Items.Restore(items, eq);
            known.Clear();
            foreach (var b in s.blueprints ?? new List<string>())
                if (Enum.TryParse(b, out ItemId id)) known.Add(id);
            flags.Clear();
            foreach (var f in s.flags ?? new List<string>()) flags.Add(f);
            Health.Reset(100);
            if (s.health > 0 && s.health < 100) Health.Hurt(100 - s.health);
            PlaySeconds = s.playSeconds;
        }
    }
}
