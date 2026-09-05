using System;
using AlphaTown.Data.Economy;
using AlphaTown.Data.Items;
using AlphaTown.Data.Orders;
using AlphaTown.Gameplay.Orders;
using NUnit.Framework;

namespace AlphaTown.Tests.EditMode
{
    /// <summary>
    /// A flat quantity range treats wheat and cake as the same ask. That is harmless while orders
    /// are small and absurd once they are not — eighteen cakes is hours of production, eighteen
    /// wheat is one field — and an unfillable slot the player must pay to reroll is the worst
    /// possible use of a reroll. Sizing by value is what keeps a big board playable.
    /// </summary>
    public sealed class OrderSizingTests
    {
        const string Cheap = "cheap";
        const string Dear = "dear";
        const string Free = "free";

        FakeDatabase _database;
        OrderGenerator _generator;

        [SetUp]
        public void SetUp()
        {
            _database = new FakeDatabase()
                .WithItem(new FakeItem(Cheap, coinValue: 5))
                .WithItem(new FakeItem(Dear, coinValue: 100))
                .WithItem(new FakeItem(Free, coinValue: 0))
                .WithCurrency(new FakeCurrency(TestContent.Coins, CurrencyKind.Soft, 0));

            _generator = new OrderGenerator(_database, new Random(11));
        }

        void Produces(string itemId) =>
            _database.WithRecipe(new FakeRecipe("recipe." + itemId, TimeSpan.FromSeconds(1), null,
                new[] { new ItemStack(itemId, 1) }));

        static FakeOrderTemplate Template(int min, int max, int valuePerType) =>
            new FakeOrderTemplate("template")
            {
                MinItemTypes = 1,
                MaxItemTypes = 1,
                MinQuantityPerItem = min,
                MaxQuantityPerItem = max,
                ValuePerItemType = valuePerType
            };

        int QuantityAsked(FakeOrderTemplate template)
        {
            var order = _generator.TryGenerate(template, townLevel: 99, nowTicks: 0, orderId: "o1");
            Assert.That(order, Is.Not.Null);
            return order.Requests[0].Count;
        }

        [Test]
        public void WithNoValueBudgetTheFlatRangeIsUsed()
        {
            Produces(Dear);

            Assert.That(QuantityAsked(Template(7, 7, valuePerType: 0)), Is.EqualTo(7));
        }

        /// <summary>The whole point: the same budget buys many cheap goods or few dear ones.</summary>
        [Test]
        public void ACheapGoodIsAskedForInBulkAndADearOneInHandfuls()
        {
            Produces(Cheap);
            var cheap = QuantityAsked(Template(1, 40, valuePerType: 200));

            SetUp();
            Produces(Dear);
            var dear = QuantityAsked(Template(1, 40, valuePerType: 200));

            Assert.That(cheap, Is.GreaterThan(dear * 4),
                "a good worth twenty times less should be asked for far more of");
        }

        [Test]
        public void TheCeilingStillApplies()
        {
            Produces(Cheap);

            // The budget alone would want 40 of these; the template says no more than 6.
            Assert.That(QuantityAsked(Template(1, 6, valuePerType: 200)), Is.LessThanOrEqualTo(6));
        }

        [Test]
        public void TheFloorStillApplies()
        {
            Produces(Dear);

            // The budget alone would want one; the template insists on at least three.
            Assert.That(QuantityAsked(Template(3, 20, valuePerType: 50)), Is.GreaterThanOrEqualTo(3));
        }

        /// <summary>A worthless good cannot be sized by value, so it falls back rather than dividing by zero.</summary>
        [Test]
        public void AGoodWithNoCoinValueFallsBackToTheFlatRange()
        {
            Produces(Free);

            Assert.That(QuantityAsked(Template(4, 4, valuePerType: 200)), Is.EqualTo(4));
        }

        /// <summary>
        /// Orders should not all look the same. A quarter's variance is enough to hide the
        /// machinery without letting any of them drift back to impossible.
        /// </summary>
        [Test]
        public void SizedOrdersStillVary()
        {
            Produces(Cheap);
            var template = Template(1, 40, valuePerType: 200);

            var seen = new System.Collections.Generic.HashSet<int>();
            for (var i = 0; i < 40; i++)
            {
                var order = _generator.TryGenerate(template, 99, 0, "o" + i);
                seen.Add(order.Requests[0].Count);
            }

            Assert.That(seen.Count, Is.GreaterThan(1), "every order asked for exactly the same amount");
        }

        /// <summary>
        /// The failure this exists to prevent, stated as a number: a ship-sized budget must never
        /// ask for more of an expensive good than a player could plausibly make.
        /// </summary>
        [Test]
        public void ALargeBudgetNeverAsksForAnAbsurdNumberOfExpensiveGoods()
        {
            Produces(Dear);

            for (var i = 0; i < 40; i++)
            {
                var order = _generator.TryGenerate(Template(2, 20, valuePerType: 220), 99, 0, "o" + i);
                Assert.That(order.Requests[0].Count, Is.LessThanOrEqualTo(4));
            }
        }
    }
}
