using System.Collections.Generic;
using AlphaTown.Data.Catalog;
using AlphaTown.Data.Items;
using AlphaTown.Data.Orders;
using AlphaTown.Data.Recipes;

namespace AlphaTown.Data.Validation
{
    /// <summary>
    /// Reads the whole content graph and reports what does not join up.
    ///
    /// Content bugs are the quietest kind this project has. Nothing throws when a recipe wants an
    /// item nothing produces, or a pen is built for a producer no building references, or a good
    /// is made and then wanted by nothing: the game simply runs, and a player finds a building
    /// that does nothing. Every one of those is a question somebody would otherwise have to ask by
    /// eye, once, and then never again as the content doubles.
    ///
    /// It works on the interfaces, so it runs against generated assets in the Editor and against
    /// a hand-built database in a test, with no Unity in sight.
    /// </summary>
    public static class ContentValidator
    {
        public static List<ContentIssue> Validate(IGameDatabase database)
        {
            var issues = new List<ContentIssue>();
            if (database == null)
            {
                issues.Add(Error("database", "There is no database to validate."));
                return issues;
            }

            CheckWellKnownEntries(database, issues);
            CheckRecipes(database, issues);
            CheckProducers(database, issues);
            CheckBuildings(database, issues);
            CheckItemFlow(database, issues);
            CheckMargins(database, issues);
            CheckUnlockOrder(database, issues);
            CheckOrders(database, issues);
            CheckExpansions(database, issues);
            CheckNewGame(database, issues);

            return issues;
        }

        // --- The entries nothing runs without ----------------------------------------------------

        static void CheckWellKnownEntries(IGameDatabase database, List<ContentIssue> issues)
        {
            if (database.SoftCurrency == null)
                issues.Add(Error("database", "No soft currency. Orders cannot pay and nothing can be bought."));

            if (database.DefaultStorage == null)
                issues.Add(Error("database", "No default storage. The barn has no capacity."));

            if (database.ProgressionCurve == null)
                issues.Add(Error("database", "No progression curve. Nothing can ever unlock."));

            if (database.TownDefinition == null)
                issues.Add(Warning("database", "No town definition. The grid falls back to a default size."));

            if (database.NewGame == null)
                issues.Add(Warning("database", "No new-game definition. A new player starts with an empty town."));
        }

        // --- Recipes -----------------------------------------------------------------------------

        static void CheckRecipes(IGameDatabase database, List<ContentIssue> issues)
        {
            var recipes = database.Recipes;
            if (recipes == null) return;

            var runnable = ProducerRecipeIds(database);

            for (var i = 0; i < recipes.Count; i++)
            {
                var recipe = recipes[i];
                if (recipe == null) continue;

                if (recipe.Outputs.Count == 0)
                    issues.Add(Error(recipe.Id, "Produces nothing. Running it would consume its inputs for free."));

                // The failure mode this catches is undramatic and easy to ship: a recipe asset is
                // added, the producer that should run it is left alone by the generator because it
                // already existed, and the recipe sits in the database being made by nothing.
                if (!runnable.Contains(recipe.Id))
                    issues.Add(Warning(recipe.Id, "No producer runs this recipe, so it can never be made."));

                CheckStacksExist(database, recipe.Inputs, recipe.Id, "input", issues);
                CheckStacksExist(database, recipe.Outputs, recipe.Id, "output", issues);

                for (var o = 0; o < recipe.Outputs.Count; o++)
                {
                    if (!database.TryGetItem(recipe.Outputs[o].ItemId, out var item)) continue;

                    if (!item.IsStorable)
                        issues.Add(Warning(recipe.Id,
                            "Produces '" + item.Id + "', which takes no barn space and so can never be delivered."));
                }
            }
        }

        static void CheckStacksExist(IGameDatabase database, IReadOnlyList<ItemStack> stacks,
                                     string subject, string role, List<ContentIssue> issues)
        {
            for (var i = 0; i < stacks.Count; i++)
            {
                if (!database.TryGetItem(stacks[i].ItemId, out _))
                    issues.Add(Error(subject, "Unknown " + role + " item '" + stacks[i].ItemId + "'."));
            }
        }

        // --- Producers ---------------------------------------------------------------------------

        static void CheckProducers(IGameDatabase database, List<ContentIssue> issues)
        {
            var producers = database.Producers;
            if (producers == null) return;

            var housed = BuildingProducerIds(database);

            for (var i = 0; i < producers.Count; i++)
            {
                var producer = producers[i];
                if (producer == null) continue;

                if (producer.Recipes.Count == 0)
                    issues.Add(Error(producer.Id, "Has no recipes, so any building holding it does nothing."));

                for (var r = 0; r < producer.Recipes.Count; r++)
                {
                    var recipe = producer.Recipes[r];
                    if (recipe == null)
                    {
                        issues.Add(Error(producer.Id, "Has an empty recipe slot."));
                        continue;
                    }

                    if (!database.TryGetRecipe(recipe.Id, out _))
                        issues.Add(Error(producer.Id,
                            "Runs recipe '" + recipe.Id + "', which is not in the database's recipe list."));
                }

                if (!housed.Contains(producer.Id))
                    issues.Add(Warning(producer.Id,
                        "No building runs this producer, so nothing it makes can ever be made."));
            }
        }

        static HashSet<string> ProducerRecipeIds(IGameDatabase database)
        {
            var ids = new HashSet<string>();
            var producers = database.Producers;
            if (producers == null) return ids;

            for (var i = 0; i < producers.Count; i++)
            {
                if (producers[i] == null) continue;

                for (var r = 0; r < producers[i].Recipes.Count; r++)
                {
                    if (producers[i].Recipes[r] != null) ids.Add(producers[i].Recipes[r].Id);
                }
            }

            return ids;
        }

        static HashSet<string> BuildingProducerIds(IGameDatabase database)
        {
            var ids = new HashSet<string>();
            var buildings = database.Buildings;
            if (buildings == null) return ids;

            for (var i = 0; i < buildings.Count; i++)
            {
                if (buildings[i] == null) continue;
                if (!string.IsNullOrEmpty(buildings[i].ProducerDefinitionId))
                    ids.Add(buildings[i].ProducerDefinitionId);
            }

            return ids;
        }

        // --- Buildings ---------------------------------------------------------------------------

        static void CheckBuildings(IGameDatabase database, List<ContentIssue> issues)
        {
            var buildings = database.Buildings;
            if (buildings == null) return;

            for (var i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building == null) continue;

                if (!string.IsNullOrEmpty(building.ProducerDefinitionId) &&
                    !database.TryGetProducer(building.ProducerDefinitionId, out _))
                {
                    issues.Add(Error(building.Id,
                        "Names producer '" + building.ProducerDefinitionId + "', which does not exist."));
                }

                if (!string.IsNullOrEmpty(building.UpgradesIntoId) &&
                    !database.TryGetBuilding(building.UpgradesIntoId, out _))
                {
                    issues.Add(Error(building.Id,
                        "Upgrades into '" + building.UpgradesIntoId + "', which does not exist."));
                }

                if (!building.Footprint.IsValid)
                    issues.Add(Error(building.Id, "Has no footprint, so it can never be placed."));

                for (var level = 1; level <= building.MaxLevel; level++)
                {
                    var tier = building.GetLevel(level);
                    CheckStacksExist(database, tier.ItemCost, building.Id, "build cost", issues);

                    for (var c = 0; c < tier.CurrencyCost.Count; c++)
                    {
                        if (!database.TryGetCurrency(tier.CurrencyCost[c].CurrencyId, out _))
                            issues.Add(Error(building.Id,
                                "Level " + level + " costs unknown currency '" +
                                tier.CurrencyCost[c].CurrencyId + "'."));
                    }
                }
            }
        }

        // --- What is made, and what wants it -----------------------------------------------------

        static void CheckItemFlow(IGameDatabase database, List<ContentIssue> issues)
        {
            var items = database.Items;
            if (items == null) return;

            var produced = new HashSet<string>();
            var consumed = new HashSet<string>();
            var granted = GrantedItemIds(database);

            var recipes = database.Recipes;
            if (recipes != null)
            {
                for (var i = 0; i < recipes.Count; i++)
                {
                    if (recipes[i] == null) continue;

                    for (var o = 0; o < recipes[i].Outputs.Count; o++) produced.Add(recipes[i].Outputs[o].ItemId);
                    for (var n = 0; n < recipes[i].Inputs.Count; n++) consumed.Add(recipes[i].Inputs[n].ItemId);
                }
            }

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null) continue;

                if (!produced.Contains(item.Id) && !granted.Contains(item.Id))
                {
                    issues.Add(Warning(item.Id,
                        "Nothing produces or grants this, so a player can never hold one."));
                }

                // Only intermediate goods are expected to be wanted by something. A finished good
                // exists to be delivered or sold, so a dead end is the whole point of it.
                var intermediate = item.Category == ItemCategory.Ingredient ||
                                   item.Category == ItemCategory.AnimalProduce;

                if (intermediate && produced.Contains(item.Id) && !consumed.Contains(item.Id))
                {
                    issues.Add(Warning(item.Id,
                        "Made, but no recipe wants it. Either give it a use or mark it a finished good."));
                }
            }
        }

        /// <summary>Items a player can receive without making them: starting stock and order bonuses.</summary>
        static HashSet<string> GrantedItemIds(IGameDatabase database)
        {
            var granted = new HashSet<string>();

            var newGame = database.NewGame;
            if (newGame != null)
            {
                for (var i = 0; i < newGame.StartingItems.Count; i++)
                    granted.Add(newGame.StartingItems[i].ItemId);
            }

            var templates = database.OrderTemplates;
            if (templates == null) return granted;

            for (var i = 0; i < templates.Count; i++)
            {
                if (templates[i] == null) continue;

                for (var b = 0; b < templates[i].BonusItems.Count; b++)
                    granted.Add(templates[i].BonusItems[b].ItemId);
            }

            return granted;
        }

        // --- Is it worth running? ----------------------------------------------------------------

        /// <summary>
        /// A recipe whose outputs are worth no more than its inputs.
        ///
        /// This is the quietest content bug there is. Nothing breaks: the building runs, the timer
        /// ticks, the good appears. It just makes the player poorer for waiting, and the only way
        /// to notice is to price both sides by hand. Bread shipped like this — two flour and a corn
        /// for a loaf worth exactly the same — until this check was written.
        ///
        /// Bonus output is ignored on purpose. A recipe that only pays when it rolls well is still
        /// a recipe that does not pay.
        /// </summary>
        static void CheckMargins(IGameDatabase database, List<ContentIssue> issues)
        {
            var recipes = database.Recipes;
            if (recipes == null) return;

            for (var i = 0; i < recipes.Count; i++)
            {
                var recipe = recipes[i];
                if (recipe == null || recipe.Inputs.Count == 0) continue;

                var cost = TotalValue(database, recipe.Inputs);
                if (cost <= 0) continue;

                var made = TotalValue(database, recipe.Outputs);
                if (made > cost) continue;

                issues.Add(Warning(recipe.Id,
                    "Inputs are worth " + cost + " and outputs " + made +
                    ", so running it loses the player value."));
            }
        }

        static int TotalValue(IGameDatabase database, IReadOnlyList<ItemStack> stacks)
        {
            var total = 0;
            for (var i = 0; i < stacks.Count; i++)
            {
                if (database.TryGetItem(stacks[i].ItemId, out var item))
                    total += item.CoinValue * stacks[i].Count;
            }

            return total;
        }

        // --- Can it be reached in the order it unlocks? ------------------------------------------

        static void CheckUnlockOrder(IGameDatabase database, List<ContentIssue> issues)
        {
            var recipes = database.Recipes;
            if (recipes == null) return;

            var curve = database.ProgressionCurve;
            var maxLevel = curve != null ? curve.MaxLevel : int.MaxValue;

            // Earliest level each item can exist at, taken across every recipe that makes it.
            var earliest = new Dictionary<string, int>();
            for (var i = 0; i < recipes.Count; i++)
            {
                var recipe = recipes[i];
                if (recipe == null) continue;

                for (var o = 0; o < recipe.Outputs.Count; o++)
                {
                    var id = recipe.Outputs[o].ItemId;
                    if (!earliest.TryGetValue(id, out var level) || recipe.UnlockLevel < level)
                        earliest[id] = recipe.UnlockLevel;
                }
            }

            for (var i = 0; i < recipes.Count; i++)
            {
                var recipe = recipes[i];
                if (recipe == null) continue;

                if (recipe.UnlockLevel > maxLevel)
                    issues.Add(Warning(recipe.Id,
                        "Unlocks at level " + recipe.UnlockLevel + ", above the curve's cap of " + maxLevel + "."));

                for (var n = 0; n < recipe.Inputs.Count; n++)
                {
                    var input = recipe.Inputs[n].ItemId;
                    if (!earliest.TryGetValue(input, out var available)) continue;

                    if (available > recipe.UnlockLevel)
                    {
                        issues.Add(Error(recipe.Id,
                            "Unlocks at level " + recipe.UnlockLevel + " but needs '" + input +
                            "', which cannot be made until level " + available + "."));
                    }
                }
            }

            CheckBuildingUnlockOrder(database, earliest, maxLevel, issues);
        }

        static void CheckBuildingUnlockOrder(IGameDatabase database, Dictionary<string, int> earliest,
                                             int maxLevel, List<ContentIssue> issues)
        {
            var buildings = database.Buildings;
            if (buildings == null) return;

            for (var i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building == null) continue;

                if (building.UnlockLevel > maxLevel)
                {
                    issues.Add(Warning(building.Id,
                        "Unlocks at level " + building.UnlockLevel + ", above the curve's cap of " +
                        maxLevel + ", so it can never be built."));
                }

                if (string.IsNullOrEmpty(building.ProducerDefinitionId)) continue;
                if (!database.TryGetProducer(building.ProducerDefinitionId, out var producer)) continue;

                var soonest = int.MaxValue;
                for (var r = 0; r < producer.Recipes.Count; r++)
                {
                    if (producer.Recipes[r] != null && producer.Recipes[r].UnlockLevel < soonest)
                        soonest = producer.Recipes[r].UnlockLevel;
                }

                if (soonest != int.MaxValue && soonest > building.UnlockLevel)
                {
                    issues.Add(Warning(building.Id,
                        "Can be built at level " + building.UnlockLevel + " but makes nothing until level " +
                        soonest + "."));
                }
            }
        }

        // --- Orders ------------------------------------------------------------------------------

        static void CheckOrders(IGameDatabase database, List<ContentIssue> issues)
        {
            var templates = database.OrderTemplates;
            var boards = database.OrderBoards;

            if (templates != null)
            {
                for (var i = 0; i < templates.Count; i++)
                {
                    var template = templates[i];
                    if (template == null) continue;

                    CheckStacksExist(database, template.BonusItems, template.Id, "bonus", issues);

                    if (template.MinItemTypes > template.MaxItemTypes)
                        issues.Add(Error(template.Id, "Asks for more item types than it allows."));
                }
            }

            if (boards == null) return;

            for (var i = 0; i < boards.Count; i++)
            {
                var board = boards[i];
                if (board == null) continue;

                if (board.SlotCount <= 0)
                    issues.Add(Error(board.Id, "Has no slots, so it can never hold an order."));

                if (!HasTemplateFor(templates, board.Kind, board.UnlockLevel))
                {
                    issues.Add(Warning(board.Id,
                        "Opens at level " + board.UnlockLevel +
                        " with no " + board.Kind + " template unlocked, so it opens empty."));
                }
            }
        }

        static bool HasTemplateFor(IReadOnlyList<IOrderTemplateDefinition> templates, OrderKind kind, int level)
        {
            if (templates == null) return false;

            for (var i = 0; i < templates.Count; i++)
            {
                if (templates[i] == null || templates[i].Kind != kind) continue;
                if (templates[i].UnlockLevel <= level) return true;
            }

            return false;
        }

        // --- Land --------------------------------------------------------------------------------

        static void CheckExpansions(IGameDatabase database, List<ContentIssue> issues)
        {
            var expansions = database.Expansions;
            if (expansions == null) return;

            for (var i = 0; i < expansions.Count; i++)
            {
                var expansion = expansions[i];
                if (expansion == null) continue;

                CheckStacksExist(database, expansion.ItemCost, expansion.Id, "land cost", issues);

                if (!expansion.Region.IsValid)
                    issues.Add(Error(expansion.Id, "Covers no cells."));

                if (string.IsNullOrEmpty(expansion.RequiresExpansionId)) continue;

                if (!database.TryGetExpansion(expansion.RequiresExpansionId, out _))
                {
                    issues.Add(Error(expansion.Id,
                        "Requires '" + expansion.RequiresExpansionId + "', which does not exist."));

                    continue;
                }

                if (expansion.RequiresExpansionId == expansion.Id)
                    issues.Add(Error(expansion.Id, "Requires itself, so it can never be unlocked."));
            }
        }

        // --- The first five minutes --------------------------------------------------------------

        static void CheckNewGame(IGameDatabase database, List<ContentIssue> issues)
        {
            var newGame = database.NewGame;
            if (newGame == null) return;

            for (var i = 0; i < newGame.StartingItems.Count; i++)
            {
                if (!database.TryGetItem(newGame.StartingItems[i].ItemId, out _))
                    issues.Add(Error(newGame.Id,
                        "Starts the player with unknown item '" + newGame.StartingItems[i].ItemId + "'."));
            }

            for (var i = 0; i < newGame.StartingBuildings.Count; i++)
            {
                if (!database.TryGetBuilding(newGame.StartingBuildings[i].DefinitionId, out _))
                    issues.Add(Error(newGame.Id,
                        "Starts the player with unknown building '" +
                        newGame.StartingBuildings[i].DefinitionId + "'."));
            }

            var storage = database.DefaultStorage;
            if (storage != null && newGame.StartingBarnLevel > storage.MaxLevel)
            {
                issues.Add(Warning(newGame.Id,
                    "Asks for barn level " + newGame.StartingBarnLevel + "; storage only reaches " +
                    storage.MaxLevel + "."));
            }
        }

        static ContentIssue Error(string subject, string message) =>
            new ContentIssue(ContentIssueSeverity.Error, subject, message);

        static ContentIssue Warning(string subject, string message) =>
            new ContentIssue(ContentIssueSeverity.Warning, subject, message);
    }
}
