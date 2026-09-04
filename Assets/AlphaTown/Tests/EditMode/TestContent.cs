using System;
using AlphaTown.Core.Spatial;
using AlphaTown.Data.Buildings;
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

        // Farming. Added only when Build is asked for it, so the order-generation tests keep
        // their level-1 pool of exactly one item and stay deterministic.
        public const string Wheat = "wheat";
        public const string Corn = "corn";
        public const string WheatCrop = "recipe.wheat";
        public const string CornCrop = "recipe.corn";
        public const string Field = "field";
        public const string FieldBuilding = "building.field";

        public const int WheatGrowSeconds = 120;
        public const int WheatYield = 2;
        public const int CornGrowSeconds = 300;
        public const int FieldCoinCost = 10;
        public const int FieldUpgradeCoinCost = 100;

        /// <summary>Every helicopter slot cools for ten minutes after its order clears.</summary>
        public const int OrderSlotCooldownSeconds = 600;

        // Expansion. Added only when Build is asked for it, so every other test keeps a town
        // that is unlocked end to end.
        public const string Deed = "land_deed";
        public const string ExpansionEast = "expansion.east";
        public const string ExpansionNorth = "expansion.north";
        public const string ExpansionNortheast = "expansion.northeast";

        public const int EastDeedCost = 2;
        public const int NorthDeedCost = 3;
        public const int NortheastDeedCost = 4;
        public const int NortheastCoinCost = 500;

        /// <summary>The town starts owning the bottom-left quarter of the 8x8 grid.</summary>
        public const int StartingAreaSize = 4;

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
                                         IOrderTemplateDefinition orderTemplate = null,
                                         bool includeFarming = false,
                                         IOrderBoardDefinition orderBoard = null,
                                         bool includeExpansion = false)
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
                // Two levels, so a test can exercise a barn upgrade. Level 1 keeps the requested
                // capacity, so every test that only cares about the starting barn is unaffected.
                .WithStorage(new FakeStorage(barnCapacity, barnCapacity * 2))
                .WithCurrency(new FakeCurrency(Coins, CurrencyKind.Soft, startingCoins))
                .WithCurrency(new FakeCurrency(Gems, CurrencyKind.Hard, startingGems))
                .WithProgressionCurve(new FakeProgressionCurve(xpCurve ?? DefaultXpCurve))
                .WithOrderTemplate(orderTemplate ?? SingleBreadTemplate())
                .WithTown(includeExpansion
                    ? new FakeTownDefinition(TownWidth, TownHeight, StartingArea())
                    : new FakeTownDefinition(TownWidth, TownHeight))
                .WithOrderBoard(orderBoard ?? DefaultOrderBoard());

            AddBuildings(database);
            if (includeFarming) AddFarming(database);
            if (includeExpansion) AddExpansion(database);

            database.WithProducer(new FakeProducerDefinition(
                Bakery,
                new IRecipeDefinition[] { breadRecipe, cakeRecipe },
                new FakeProducerLevel(queueCapacity: 3, parallelSlots: 1),
                new FakeProducerLevel(queueCapacity: 5, parallelSlots: 2)));

            return database;
        }

        public static GridRect StartingArea() =>
            new GridRect(GridPosition.Zero, new GridSize(StartingAreaSize, StartingAreaSize));

        /// <summary>
        /// Three plots around the starting quarter. East and North grow straight off it; Northeast
        /// requires East first, which is what keeps land spreading outward instead of letting a
        /// player buy a far corner.
        /// </summary>
        static void AddExpansion(FakeDatabase database)
        {
            // A deed is an item that costs no barn space — the cleanest way to hold a token the
            // player never stockpiles by the hundred.
            database.WithItem(new FakeItem(Deed, isStorable: false, coinValue: 0, xpValue: 0));

            database.WithExpansion(new FakeExpansionDefinition(
                ExpansionEast,
                new GridRect(new GridPosition(4, 0), new GridSize(4, 4)),
                new[] { new ItemStack(Deed, EastDeedCost) }));

            database.WithExpansion(new FakeExpansionDefinition(
                ExpansionNorth,
                new GridRect(new GridPosition(0, 4), new GridSize(4, 4)),
                new[] { new ItemStack(Deed, NorthDeedCost) })
            {
                SortOrder = 1
            });

            database.WithExpansion(new FakeExpansionDefinition(
                ExpansionNortheast,
                new GridRect(new GridPosition(4, 4), new GridSize(4, 4)),
                new[] { new ItemStack(Deed, NortheastDeedCost) },
                new[] { new CurrencyAmount(Coins, NortheastCoinCost) })
            {
                RequiresExpansionId = ExpansionEast,
                SortOrder = 2
            });
        }

        /// <summary>A bread order that always carries a land deed. Deeds come from orders.</summary>
        public static FakeOrderTemplate DeedTemplate(int deedCount = 1)
        {
            var template = SingleBreadTemplate();
            template.BonusItems = new[] { new ItemStack(Deed, deedCount) };
            template.BonusItemChance = 1f;
            return template;
        }

        /// <summary>Four helicopter slots, each cooling for ten minutes after its order clears.</summary>
        public static FakeOrderBoardDefinition DefaultOrderBoard() =>
            new FakeOrderBoardDefinition(
                OrderKind.Helicopter,
                OrderSlotCooldownSeconds, OrderSlotCooldownSeconds,
                OrderSlotCooldownSeconds, OrderSlotCooldownSeconds);

        /// <summary>
        /// Crops and a field to grow them in. A field is nothing more than a Farming building
        /// whose producer runs recipes with no inputs — the free end of the same machinery that
        /// runs the bakery. Its level 2 turns auto-replant on, which is the upgrade in data form.
        /// </summary>
        static void AddFarming(FakeDatabase database)
        {
            var wheat = new FakeRecipe(
                WheatCrop,
                TimeSpan.FromSeconds(WheatGrowSeconds),
                null,
                new[] { new ItemStack(Wheat, WheatYield) });

            var corn = new FakeRecipe(
                CornCrop,
                TimeSpan.FromSeconds(CornGrowSeconds),
                null,
                new[] { new ItemStack(Corn, 2) },
                unlockLevel: 2);

            database
                .WithItem(new FakeItem(Wheat, coinValue: 8, xpValue: 2))
                .WithItem(new FakeItem(Corn, coinValue: 12, xpValue: 3))
                .WithRecipe(wheat)
                .WithRecipe(corn);

            database.WithProducer(new FakeProducerDefinition(
                Field,
                new IRecipeDefinition[] { wheat, corn },
                new FakeProducerLevel(queueCapacity: 1, parallelSlots: 1),
                new FakeProducerLevel(queueCapacity: 1, parallelSlots: 1, autoRepeat: true)));

            database.WithBuilding(new FakeBuildingDefinition(
                FieldBuilding,
                new GridSize(1, 1),
                new FakeBuildingLevel(0, new[] { new CurrencyAmount(Coins, FieldCoinCost) }),
                new FakeBuildingLevel(0, new[] { new CurrencyAmount(Coins, FieldUpgradeCoinCost) }))
            {
                Category = BuildingCategory.Farming,
                ProducerDefinitionId = Field
            });
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
