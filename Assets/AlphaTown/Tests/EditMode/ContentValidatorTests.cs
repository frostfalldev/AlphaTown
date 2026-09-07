using System;
using System.Collections.Generic;
using AlphaTown.Core.Spatial;
using AlphaTown.Data.Catalog;
using AlphaTown.Data.Economy;
using AlphaTown.Data.Items;
using AlphaTown.Data.Orders;
using AlphaTown.Data.Recipes;
using AlphaTown.Data.Validation;
using NUnit.Framework;

namespace AlphaTown.Tests.EditMode
{
    /// <summary>
    /// The validator's job is to notice what a person would only find by reading every asset, so
    /// these tests are mostly one shape each: take a graph that joins up, break one link, and
    /// check that the report names the thing that broke and says why in a sentence somebody could
    /// act on.
    ///
    /// The first test matters most. A validator that cries wolf on healthy content gets muted
    /// within a week, and then it may as well not exist.
    /// </summary>
    [TestFixture]
    internal sealed class ContentValidatorTests
    {
        [Test]
        public void ContentThatJoinsUp_ReportsNothing()
        {
            var issues = ContentValidator.Validate(Chain());
            Assert.That(Describe(issues), Is.EqualTo(string.Empty));
        }

        [Test]
        public void ARecipeWantingAnItemThatDoesNotExist_IsAnError()
        {
            var database = Chain()
                .WithRecipe(new FakeRecipe("bake_pie", TimeSpan.FromMinutes(1),
                    new[] { new ItemStack("butter", 1) },
                    new[] { new ItemStack(Bread, 1) }));

            AssertHas(database, ContentIssueSeverity.Error, "bake_pie", "butter");
        }

        [Test]
        public void ARecipeThatProducesNothing_IsAnError()
        {
            var database = Chain()
                .WithRecipe(new FakeRecipe("burn_flour", TimeSpan.FromMinutes(1),
                    new[] { new ItemStack(Flour, 1) },
                    Array.Empty<ItemStack>()));

            AssertHas(database, ContentIssueSeverity.Error, "burn_flour", "Produces nothing");
        }

        /// <summary>
        /// The one the generator's skip-existing rule creates: a recipe asset is written, the
        /// producer that should run it already existed and was left alone, and the new good is in
        /// the database being made by nothing.
        /// </summary>
        [Test]
        public void ARecipeNoProducerRuns_IsAWarning()
        {
            var database = Chain()
                .WithRecipe(new FakeRecipe("make_goat_cheese", TimeSpan.FromMinutes(9),
                    new[] { new ItemStack(Flour, 3) }, new[] { new ItemStack(Bread, 1) },
                    unlockLevel: 2));

            AssertHas(database, ContentIssueSeverity.Warning, "make_goat_cheese", "No producer runs this recipe");
        }

        /// <summary>The pen exists, the animal exists, and no building will ever run it.</summary>
        [Test]
        public void AProducerNoBuildingRuns_IsAWarning()
        {
            var loom = new FakeRecipe("weave", TimeSpan.FromMinutes(1),
                Array.Empty<ItemStack>(), new[] { new ItemStack(Flour, 1) });

            var database = Chain()
                .WithRecipe(loom)
                .WithProducer(new FakeProducerDefinition("loom", new IRecipeDefinition[] { loom }));

            AssertHas(database, ContentIssueSeverity.Warning, "loom", "No building runs this producer");
        }

        [Test]
        public void ABuildingNamingAProducerThatDoesNotExist_IsAnError()
        {
            var shed = new FakeBuildingDefinition("shed", new GridSize(1, 1))
            {
                ProducerDefinitionId = "sawmill"
            };

            AssertHas(Chain().WithBuilding(shed),
                ContentIssueSeverity.Error, "shed", "sawmill");
        }

        [Test]
        public void ABuildingUpgradingIntoNothing_IsAnError()
        {
            var bed = new FakeBuildingDefinition("flower_bed", new GridSize(1, 1))
            {
                UpgradesIntoId = "fountain"
            };

            AssertHas(Chain().WithBuilding(bed),
                ContentIssueSeverity.Error, "flower_bed", "fountain");
        }

        [Test]
        public void ABuildingWithNoFootprint_IsAnError()
        {
            var statue = new FakeBuildingDefinition("statue", new GridSize(0, 0));

            AssertHas(Chain().WithBuilding(statue),
                ContentIssueSeverity.Error, "statue", "footprint");
        }

        /// <summary>
        /// The check that found the real bug: three animal pens whose output nothing wanted, which
        /// made the most expensive buildings in the game produce barn clutter.
        /// </summary>
        [Test]
        public void AnIntermediateGoodNoRecipeWants_IsAWarning()
        {
            var database = ChainWithDeadEnd(ItemCategory.AnimalProduce);
            AssertHas(database, ContentIssueSeverity.Warning, "goat_milk", "no recipe wants it");
        }

        /// <summary>A finished good is supposed to be a dead end — it exists to be sold or shipped.</summary>
        [Test]
        public void AFinishedGoodNoRecipeWants_IsNotAWarning()
        {
            var issues = ContentValidator.Validate(ChainWithDeadEnd(ItemCategory.FinishedGood));
            Assert.That(Describe(issues), Is.EqualTo(string.Empty));
        }

        /// <summary>
        /// Bread shipped like this: two flour and a corn for a loaf worth exactly the same. The
        /// building ran, the timer ticked, the loaf appeared, and the player was no better off.
        /// </summary>
        [Test]
        public void ARecipeWorthNoMoreThanItsInputs_IsAWarning()
        {
            var database = Chain()
                .WithItem(new FakeItem("dough", coinValue: 10))
                .WithRecipe(new FakeRecipe("knead", TimeSpan.FromMinutes(5),
                    new[] { new ItemStack(Flour, 2) }, new[] { new ItemStack("dough", 2) },
                    unlockLevel: 2));

            // Flour is worth 14 in this fixture, so two in is 28 against two dough at 20 out —
            // five minutes of work to end up behind.
            AssertHas(database, ContentIssueSeverity.Warning, "knead", "loses the player value");
        }

        [Test]
        public void AnItemNothingProduces_IsAWarning()
        {
            var database = Chain()
                .WithItem(new FakeItem("truffle"));

            AssertHas(database, ContentIssueSeverity.Warning, "truffle", "can never hold one");
        }

        /// <summary>Starting stock and order bonuses are the two ways to hold what you cannot make.</summary>
        [Test]
        public void AnItemOnlyGrantedByAnOrderBonus_IsNotAWarning()
        {
            var template = new FakeOrderTemplate("helicopter")
            {
                BonusItems = new[] { new ItemStack(Deed, 1) }
            };

            var issues = ContentValidator.Validate(Chain()
                .WithItem(new FakeItem(Deed, category: ItemCategory.Special))
                .WithOrderTemplate(template));

            Assert.That(Describe(issues), Is.EqualTo(string.Empty));
        }

        [Test]
        public void ARecipeUnlockingBeforeItsInputsCanExist_IsAnError()
        {
            var database = Chain()
                .WithRecipe(new FakeRecipe("bake_early", TimeSpan.FromMinutes(1),
                    new[] { new ItemStack(Bread, 1) },
                    new[] { new ItemStack(Flour, 1) },
                    unlockLevel: 1));

            AssertHas(database, ContentIssueSeverity.Error, "bake_early", "cannot be made until level 2");
        }

        [Test]
        public void ABuildingThatMakesNothingAtItsUnlockLevel_IsAWarning()
        {
            var database = Chain()
                .WithBuilding(new FakeBuildingDefinition("early_bakery", new GridSize(2, 2))
                {
                    ProducerDefinitionId = BakeryProducer,
                    UnlockLevel = 1
                });

            AssertHas(database, ContentIssueSeverity.Warning, "early_bakery", "makes nothing until level 2");
        }

        [Test]
        public void ABoardThatOpensWithNoTemplateOfItsKind_IsAWarning()
        {
            var database = Chain()
                .WithOrderBoard(new FakeOrderBoardDefinition(OrderKind.Ship, 600) { UnlockLevel = 2 });

            AssertHas(database, ContentIssueSeverity.Warning, "orderboard.Ship", "opens empty");
        }

        [Test]
        public void AnExpansionRequiringItself_IsAnError()
        {
            var region = new GridRect(GridPosition.Zero, new GridSize(4, 4));
            var east = new FakeExpansionDefinition("land_east", region)
            {
                RequiresExpansionId = "land_east"
            };

            AssertHas(Chain().WithExpansion(east),
                ContentIssueSeverity.Error, "land_east", "Requires itself");
        }

        [Test]
        public void ANewGameStartingWithAnItemThatDoesNotExist_IsAnError()
        {
            var database = Chain()
                .WithNewGame(new FakeNewGameDefinition(items: new[] { new ItemStack("acorn", 4) }));

            AssertHas(database, ContentIssueSeverity.Error, "new_game", "acorn");
        }

        [Test]
        public void ANewGameAskingForABarnLevelStorageDoesNotReach_IsAWarning()
        {
            var database = Chain()
                .WithNewGame(new FakeNewGameDefinition(startingBarnLevel: 9));

            AssertHas(database, ContentIssueSeverity.Warning, "new_game", "barn level 9");
        }

        [Test]
        public void AMissingSoftCurrency_IsAnError()
        {
            var database = Chain();
            database.SoftCurrency = null;

            AssertHas(database, ContentIssueSeverity.Error, "database", "No soft currency");
        }

        [Test]
        public void ANullDatabase_IsReportedRatherThanThrown()
        {
            var issues = ContentValidator.Validate(null);
            Assert.That(issues.Count, Is.EqualTo(1));
            Assert.That(issues[0].Severity, Is.EqualTo(ContentIssueSeverity.Error));
        }

        /// <summary>
        /// The content the rest of the suite runs on. It is deliberately partial — flour has no
        /// producer, cake is a dead end — so this asserts only that nothing in it is broken, which
        /// is what stops a test fixture from quietly drifting into content that cannot run.
        /// </summary>
        [Test]
        public void TheSharedTestContent_HasNoErrors()
        {
            var issues = ContentValidator.Validate(
                TestContent.Build(includeFarming: true, includeExpansion: true));

            Assert.That(Describe(Errors(issues)), Is.EqualTo(string.Empty));
        }

        // --- A graph that joins up ---------------------------------------------------------------

        const string Wheat = "wheat";
        const string Flour = "flour";
        const string Bread = "bread";
        const string Deed = "land_deed";
        const string BakeryProducer = "bakery";

        /// <summary>
        /// Wheat to flour to bread, each with a producer and a building, every unlock in an order
        /// a player could actually walk. Small enough that a test can break exactly one link.
        /// </summary>
        static FakeDatabase Chain()
        {
            var growWheat = new FakeRecipe("grow_wheat", TimeSpan.FromMinutes(1),
                Array.Empty<ItemStack>(), new[] { new ItemStack(Wheat, 2) });

            var millFlour = new FakeRecipe("mill_flour", TimeSpan.FromMinutes(2),
                new[] { new ItemStack(Wheat, 3) }, new[] { new ItemStack(Flour, 1) });

            var bakeBread = new FakeRecipe("bake_bread", TimeSpan.FromMinutes(5),
                new[] { new ItemStack(Flour, 2) }, new[] { new ItemStack(Bread, 1) },
                unlockLevel: 2);

            return new FakeDatabase()
                // Values rise along the chain, because a fixture that claims to be content which
                // joins up has to pay for the work it asks for.
                .WithItem(new FakeItem(Wheat, coinValue: 3, category: ItemCategory.Crop))
                .WithItem(new FakeItem(Flour, coinValue: 14, category: ItemCategory.Ingredient))
                .WithItem(new FakeItem(Bread, coinValue: 34, category: ItemCategory.FinishedGood))
                .WithRecipe(growWheat)
                .WithRecipe(millFlour)
                .WithRecipe(bakeBread)
                .WithProducer(new FakeProducerDefinition("field", new IRecipeDefinition[] { growWheat }))
                .WithProducer(new FakeProducerDefinition("mill", new IRecipeDefinition[] { millFlour }))
                .WithProducer(new FakeProducerDefinition(BakeryProducer, new IRecipeDefinition[] { bakeBread }))
                .WithBuilding(new FakeBuildingDefinition("field_plot", new GridSize(1, 1))
                    { ProducerDefinitionId = "field" })
                .WithBuilding(new FakeBuildingDefinition("mill_house", new GridSize(2, 2))
                    { ProducerDefinitionId = "mill" })
                .WithBuilding(new FakeBuildingDefinition("bakery", new GridSize(2, 2))
                    { ProducerDefinitionId = BakeryProducer, UnlockLevel = 2 })
                .WithStorage(new FakeStorage(50, 100))
                .WithCurrency(new FakeCurrency("coins"))
                .WithProgressionCurve(new FakeProgressionCurve(100, 0))
                .WithTown(new FakeTownDefinition(8, 8))
                .WithNewGame(new FakeNewGameDefinition(items: new[] { new ItemStack(Wheat, 4) }))
                .WithOrderTemplate(new FakeOrderTemplate("helicopter"))
                .WithOrderBoard(new FakeOrderBoardDefinition(OrderKind.Helicopter, 60));
        }

        /// <summary>The chain plus a goat pen whose milk nothing drinks.</summary>
        static FakeDatabase ChainWithDeadEnd(ItemCategory category)
        {
            var collect = new FakeRecipe("collect_goat_milk", TimeSpan.FromMinutes(10),
                new[] { new ItemStack(Flour, 1) }, new[] { new ItemStack("goat_milk", 2) },
                unlockLevel: 2);

            return Chain()
                .WithItem(new FakeItem("goat_milk", category: category))
                .WithRecipe(collect)
                .WithProducer(new FakeProducerDefinition("goats", new IRecipeDefinition[] { collect }))
                .WithBuilding(new FakeBuildingDefinition("goat_pen", new GridSize(2, 2))
                    { ProducerDefinitionId = "goats", UnlockLevel = 2 });
        }

        // --- Assertions --------------------------------------------------------------------------

        static void AssertHas(IGameDatabase database, ContentIssueSeverity severity,
                              string subject, string fragment)
        {
            var issues = ContentValidator.Validate(database);

            for (var i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity != severity) continue;
                if (issues[i].Subject != subject) continue;
                if (issues[i].Message.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) < 0) continue;

                return;
            }

            Assert.Fail("Expected a " + severity + " on '" + subject + "' mentioning '" + fragment +
                        "'. Got:\n" + Describe(issues));
        }

        static List<ContentIssue> Errors(List<ContentIssue> issues)
        {
            var errors = new List<ContentIssue>();
            for (var i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == ContentIssueSeverity.Error) errors.Add(issues[i]);
            }

            return errors;
        }

        static string Describe(List<ContentIssue> issues)
        {
            var text = string.Empty;
            for (var i = 0; i < issues.Count; i++) text += issues[i] + "\n";
            return text;
        }
    }
}
