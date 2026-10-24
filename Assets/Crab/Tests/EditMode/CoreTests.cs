// The game's core without Unity: matter, creating things, items, saves.
using System.Linq;
using Crab.Core;
using NUnit.Framework;

namespace Crab.Tests
{
    public sealed class CoreTests
    {
        [Test]
        public void MatterIsCappedAndPaid()
        {
            var m = new MatterStore { Capacity = 5 };
            Assert.That(m.Add(Matter.Metal, 8), Is.EqualTo(5));
            Assert.That(m[Matter.Metal], Is.EqualTo(5));
            Assert.That(m.Pay(new Cost(0, 0, 3)), Is.True);
            Assert.That(m[Matter.Metal], Is.EqualTo(2));
            Assert.That(m.Pay(new Cost(1, 0, 0)), Is.False, "no organic matter yet");
        }

        [Test]
        public void CreatingNeedsTheBlueprintAndTheMatter()
        {
            var s = new GameState();
            Assert.That(s.Create(ItemId.Stick), Is.EqualTo(GameState.CreateResult.Unknown));
            s.Learn(ItemId.Stick);
            Assert.That(s.Create(ItemId.Stick), Is.EqualTo(GameState.CreateResult.NoMatter));
            s.Matter.Add(Matter.Organic, 4);
            Assert.That(s.Create(ItemId.Stick), Is.EqualTo(GameState.CreateResult.Done));
            Assert.That(s.Items.Equipped, Is.EqualTo(ItemId.Stick), "the first weapon goes into the claw");
            Assert.That(s.Create(ItemId.Stick), Is.EqualTo(GameState.CreateResult.Full), "one stick is enough");
            Assert.That(s.Matter[Matter.Organic], Is.EqualTo(2));
        }

        [Test]
        public void AmmoComesInClipsAndStonesStack()
        {
            var s = new GameState();
            s.Learn(ItemId.Ammo);
            s.Learn(ItemId.Stone);
            s.Matter.Set(0, 30, 30);
            s.Create(ItemId.Ammo);
            Assert.That(s.Items.Count(ItemId.Ammo), Is.EqualTo(8));
            for (int i = 0; i < 12; i++) s.Create(ItemId.Stone);
            Assert.That(s.Items.Count(ItemId.Stone), Is.EqualTo(Inventory.MaxStones));
            Assert.That(s.Matter[Matter.Mineral], Is.EqualTo(30 - Inventory.MaxStones));
        }

        [Test]
        public void WeaponsCycle()
        {
            var inv = new Inventory();
            inv.Add(ItemId.Stick);
            inv.Add(ItemId.Knife);
            inv.Add(ItemId.Pistol);
            inv.Add(ItemId.Ammo, 6);
            Assert.That(inv.Weapons.ToList(), Is.EqualTo(new[] { ItemId.Stick, ItemId.Knife, ItemId.Pistol }));
            inv.Equip(ItemId.Pistol);
            inv.Cycle(1);
            Assert.That(inv.Equipped, Is.EqualTo(ItemId.Stick));
            inv.Cycle(-1);
            Assert.That(inv.Equipped, Is.EqualTo(ItemId.Pistol));
        }

        [Test]
        public void SaveRoundTrip()
        {
            var s = new GameState { Scene = "Limbo", Checkpoint = "town" };
            s.Learn(ItemId.Pistol);
            s.SetFlag("spark");
            s.Matter.Set(3, 4, 5);
            s.Items.Add(ItemId.Pistol);
            s.Items.Add(ItemId.Ammo, 12);
            s.Health.Hurt(30);
            var copy = new GameState();
            copy.Load(s.ToSave());
            Assert.That(copy.Scene, Is.EqualTo("Limbo"));
            Assert.That(copy.Checkpoint, Is.EqualTo("town"));
            Assert.That(copy.Knows(ItemId.Pistol), Is.True);
            Assert.That(copy.Flag("spark"), Is.True);
            Assert.That(copy.Matter[Matter.Metal], Is.EqualTo(5));
            Assert.That(copy.Items.Count(ItemId.Ammo), Is.EqualTo(12));
            Assert.That(copy.Items.Equipped, Is.EqualTo(ItemId.Pistol));
            Assert.That(copy.Health.Value, Is.EqualTo(70).Within(0.01));
        }

        [Test]
        public void ArmourMakesAWarrior()
        {
            var s = new GameState();
            Assert.That(s.Level, Is.EqualTo(1));
            s.Learn(ItemId.ScrapArmor);
            s.Matter.Set(0, 10, 20);
            Assert.That(s.Create(ItemId.ScrapArmor), Is.EqualTo(GameState.CreateResult.Done));
            Assert.That(s.Level, Is.EqualTo(2));
            Assert.That(GameState.LevelTitle(2), Is.EqualTo("Краб-воин"));
            Assert.That(s.Items.Equipped, Is.EqualTo(ItemId.None), "armour is worn, not held");
            Assert.That(s.Create(ItemId.ScrapArmor), Is.EqualTo(GameState.CreateResult.Full));
        }

        [Test]
        public void HealthDiesOnce()
        {
            var h = new Health(20);
            int died = 0;
            h.Died += () => died++;
            Assert.That(h.Hurt(15), Is.False);
            Assert.That(h.Hurt(15), Is.True);
            Assert.That(h.Hurt(15), Is.False);
            Assert.That(died, Is.EqualTo(1));
            h.Heal(10);
            Assert.That(h.Value, Is.EqualTo(0), "the dead are not healed");
        }
    }
}
