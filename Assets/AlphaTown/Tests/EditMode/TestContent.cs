using System;
using AlphaTown.Data.Economy;
using AlphaTown.Data.Items;
using AlphaTown.Data.Orders;
using AlphaTown.Data.Recipes;

namespace AlphaTown.Tests.EditMode
{
    /// <summary>
    /// A small, complete content set shared by the economy tests.
    ///
    /// Tuned so that at town level 1 exactly one item is producible — bread — which makes
    /// generated orders deterministic without depending on a particular RNG seed. Cake unlocks at
    /// level 3, so tests can watch the order pool widen as the player progresses.
    /// </summary>
    internal static class TestContent
    {
        public const string Coins = "coins";
        public const string Gems = "gems";

        public const string Flour = "flour";
        public const string Bread = "bread";
        public const string Cake = "cake";

        public const string BreadRecipe = "recipe.bread";
        public const string CakeRecipe = "recipe.cake";

        public const string Bakery = "bakery";
        public const string HelicopterTemplate = "order.helicopter";

        public const int BreadCoinValue = 20;
        public const int BreadXpValue = 4;

        /// <summary>Level 1 needs 100 XP to advance, level 2 needs 200. Level 3 is the cap.</summary>
        public static readonly int[] DefaultXpCurve = { 100, 200, 0 };

        public static FakeDatabase Build(int[] xpCurve = null, int barnCapacity = 100,
                                         int startingCoins = 0, int startingGems = 0,
                                         IOrderTemplateDefinition orderTemplate = null)
        {
            var breadRecipe = new FakeRecipe(
                BreadRecipe,
                TimeSpan.FromSeconds(60),
                new[] { new ItemStack(Flour, 1) },
                new[] { new ItemStack(Bread, 1) });

            var cakeRecipe = new FakeRecipe(
                CakeRecipe,
                TimeSpan.FromSeconds(120),
                new[] { new ItemStack(Bread, 2) },
                new[] { new ItemStack(Cake, 1) },
                unlockLevel: 3);

            var database = new FakeDatabase()
                .WithItem(new FakeItem(Flour, coinValue: 5, xpValue: 1))
                .WithItem(new FakeItem(Bread, coinValue: BreadCoinValue, xpValue: BreadXpValue))
                .WithItem(new FakeItem(Cake, coinValue: 50, xpValue: 10))
                .WithRecipe(breadRecipe)
                .WithRecipe(cakeRecipe)
                .WithStorage(new FakeStorage(barnCapacity))
                .WithCurrency(new FakeCurrency(Coins, CurrencyKind.Soft, startingCoins))
                .WithCurrency(new FakeCurrency(Gems, CurrencyKind.Hard, startingGems))
                .WithProgressionCurve(new FakeProgressionCurve(xpCurve ?? DefaultXpCurve))
                .WithOrderTemplate(orderTemplate ?? SingleBreadTemplate());

            database.WithProducer(new FakeProducerDefinition(
                Bakery,
                new IRecipeDefinition[] { breadRecipe, cakeRecipe },
                new FakeProducerLevel(queueCapacity: 3, parallelSlots: 1)));

            return database;
        }

        /// <summary>Asks for exactly one item type, exactly one unit. Deterministic at level 1.</summary>
        public static FakeOrderTemplate SingleBreadTemplate() =>
            new FakeOrderTemplate(HelicopterTemplate)
            {
                MinItemTypes = 1,
                MaxItemTypes = 1,
                MinQuantityPerItem = 1,
                MaxQuantityPerItem = 1
            };

        /// <summary>
        /// Always asks for two item types. At level 1 the pool holds one item so it clamps to
        /// bread; at level 3 the pool holds two, so the order must contain both. That makes
        /// "generation widens as content unlocks" observable without depending on a seed.
        /// </summary>
        public static FakeOrderTemplate TwoItemTemplate() =>
            new FakeOrderTemplate(HelicopterTemplate)
            {
                MinItemTypes = 2,
                MaxItemTypes = 2,
                MinQuantityPerItem = 1,
                MaxQuantityPerItem = 1
            };

        public static FakeOrderTemplate TimedTemplate(TimeSpan limit)
        {
            var template = SingleBreadTemplate();
            template.TimeLimit = limit;
            return template;
        }
    }
}
