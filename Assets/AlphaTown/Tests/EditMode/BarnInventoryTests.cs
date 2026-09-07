using AlphaTown.Core.Events;
using AlphaTown.Data.Items;
using AlphaTown.Gameplay.Inventory;
using NUnit.Framework;

namespace AlphaTown.Tests.EditMode
{
    public sealed class BarnInventoryTests
    {
        static BarnInventory CreateBarn(int capacity)
        {
            var database = new FakeDatabase()
                .WithItem(new FakeItem("wheat"))
                .WithItem(new FakeItem("crate", storageCost: 5))
                .WithStorage(new FakeStorage(capacity));

            return new BarnInventory(database, database.DefaultStorage, new EventBus());
        }

        [Test]
        public void Add_StoresWhatFitsAndReportsIt()
        {
            var barn = CreateBarn(10);

            var stored = barn.Add("wheat", 15);

            Assert.That(stored, Is.EqualTo(10));
            Assert.That(barn.CountOf("wheat"), Is.EqualTo(10));
            Assert.That(barn.FreeSpace, Is.EqualTo(0));
        }

        [Test]
        public void StorageCost_IsChargedPerUnit()
        {
            var barn = CreateBarn(10);

            barn.Add("crate", 2);

            Assert.That(barn.UsedSpace, Is.EqualTo(10));
            Assert.That(barn.RoomFor("crate"), Is.EqualTo(0));
        }

        [Test]
        public void TryAddExact_LeavesTheBarnUntouchedWhenItWillNotFit()
        {
            var barn = CreateBarn(10);
            barn.Add("wheat", 8);

            Assert.That(barn.TryAddExact("wheat", 5), Is.False);
            Assert.That(barn.CountOf("wheat"), Is.EqualTo(8));
        }

        [Test]
        public void TryRemoveAll_IsAtomic()
        {
            var barn = CreateBarn(100);
            barn.Add("wheat", 3);

            var cost = new[] { new ItemStack("wheat", 2), new ItemStack("crate", 1) };

            Assert.That(barn.TryRemoveAll(cost), Is.False);
            Assert.That(barn.CountOf("wheat"), Is.EqualTo(3), "a failed payment must not consume anything");
        }

        [Test]
        public void Upgrade_GrowsCapacityWithoutTouchingContents()
        {
            var database = new FakeDatabase()
                .WithItem(new FakeItem("wheat"))
                .WithStorage(new FakeStorage(10, 25));
            var barn = new BarnInventory(database, database.DefaultStorage, new EventBus());
            barn.Add("wheat", 10);

            barn.SetLevel(2);

            Assert.That(barn.Capacity, Is.EqualTo(25));
            Assert.That(barn.CountOf("wheat"), Is.EqualTo(10));
            Assert.That(barn.FreeSpace, Is.EqualTo(15));
        }
    }
}
