using System;
using AlphaTown.Core.Spatial;
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

        // Buildings. Costs are round numbers so a test can assert a balance by hand.
        public const string Shed = "building.shed";
        public const string Workshop = "building.workshop";
        public const string BakeryBuilding = "building.bakery";
        public const string Hut = "building.hut";
        public const string Villa = "building.villa";

        public const int ShedCoinCost = 20;
        public const int WorkshopCoinCost = 10;
        public const int WorkshopFlourCost = 2;
        public const int BakeryLevel1CoinCost = 100;
        public const int BakeryLevel2CoinCost = 200;
        public const int BakeryLevel1BuildSeconds = 60;
        public const int BakeryLevel2BuildSeconds = 120;

        /// <summary>Small enough that out-of-bounds cases are easy to write.</summary>
        public const int TownWidth = 8;
        public const int TownHeight = 8;
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
                .WithOrderTemplate(orderTemplate ?? SingleBreadTemplate())
                .WithTown(new FakeTownDefinition(TownWidth, TownHeight));

            AddBuildings(database);

            database.WithProducer(new FakeProducerDefinition(
                Bakery,
                new IRecipeDefinition[] { breadRecipe, cakeRecipe },
                new FakeProducerLevel(queueCapacity: 3, parallelSlots: 1),
                new FakeProducerLevel(queueCapacity: 5, parallelSlots: 2)));

            return database;
        }

        /// <summary>
        /// A 1x1 instant shed for placement cases, a workshop that also costs materials, a 2x2
        /// bakery with a timed two-level upgrade path and a producer, and a hut that transforms
        /// into a larger villa.
        /// </summary>
        static void AddBuildings(FakeDatabase database)
        {
            database.WithBuilding(new FakeBuildingDefinition(
                Shed,
                new GridSize(1, 1),
                new FakeBuildingLevel(0, new[] { new CurrencyAmount(Coins, ShedCoinCost) })));

            database.WithBuilding(new FakeBuildingDefinition(
                Workshop,
                new GridSize(1, 1),
                new FakeBuildingLevel(
                    0,
                    new[] { new CurrencyAmount(Coins, WorkshopCoinCost) },
                    new[] { new ItemStack(Flour, WorkshopFlourCost) })));

            database.WithBuilding(new FakeBuildingDefinition(
                BakeryBuilding,
                new GridSize(2, 2),
                new FakeBuildingLevel(
                    BakeryLevel1BuildSeconds,
                    new[] { new CurrencyAmount(Coins, BakeryLevel1CoinCost) }),
                new FakeBuildingLevel(
                    BakeryLevel2BuildSeconds,
                    new[] { new CurrencyAmount(Coins, BakeryLevel2CoinCost) }))
            {
                ProducerDefinitionId = Bakery
            });

            database.WithBuilding(new FakeBuildingDefinition(
                Villa,
                new GridSize(2, 2),
                new FakeBuildingLevel(0, new[] { new CurrencyAmount(Coins, 150) })));

            database.WithBuilding(new FakeBuildingDefinition(
                Hut,
                new GridSize(1, 1),
                new FakeBuildingLevel(0, new[] { new CurrencyAmount(Coins, 50) }))
            {
                UpgradesIntoId = Villa
            });
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
