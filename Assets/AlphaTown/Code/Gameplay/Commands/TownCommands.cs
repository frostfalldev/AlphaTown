using System.Collections.Generic;
using AlphaTown.Core.Diagnostics;
using AlphaTown.Core.Spatial;
using AlphaTown.Core.Timing;
using AlphaTown.Data.Buildings;
using AlphaTown.Data.Catalog;
using AlphaTown.Data.Recipes;
using AlphaTown.Gameplay.Buildings;
using AlphaTown.Gameplay.Expansion;
using AlphaTown.Gameplay.World;

namespace AlphaTown.Gameplay.Commands
{
    /// <summary>
    /// Everything the player can ask the town to do, in one place, phrased the way a screen would
    /// ask it: plant this field, harvest what is ready, build here, deliver that order.
    ///
    /// The point of this layer is that the UI never reaches into the simulation. A view holds a
    /// <see cref="TownCommands"/> and a read-only look at <see cref="GameWorld"/>; it does not know
    /// that planting is really "enqueue a recipe on the producer attached to a Farming-category
    /// building", and it cannot invent a state the simulation would refuse. Every rule — cost,
    /// unlock, capacity, cooldown — is still enforced underneath; this only translates.
    ///
    /// It is also where "why not?" is answered. The systems below return enums built for code;
    /// turning those into a sentence is a presentation job, and doing it here keeps it testable and
    /// out of the MonoBehaviours.
    /// </summary>
    public sealed class TownCommands
    {
        readonly GameWorld _world;
        readonly IGameDatabase _database;
        readonly IGameClock _clock;
        readonly List<BuildingInstance> _buffer = new List<BuildingInstance>(32);

        public TownCommands(GameWorld world, IGameDatabase database, IGameClock clock)
        {
            _world = Guard.NotNull(world, nameof(world));
            _database = Guard.NotNull(database, nameof(database));
            _clock = Guard.NotNull(clock, nameof(clock));
        }

        public GameWorld World => _world;

        // --- Fields -----------------------------------------------------------------------------

        /// <summary>
        /// Sows a field. With no recipe named it plants whatever the field can currently grow —
        /// the last thing sown here if that is still possible, otherwise the first unlocked crop,
        /// which is what makes a one-tap replant work.
        /// </summary>
        public CommandResult Plant(string buildingInstanceId, string recipeId = null)
        {
            if (!_world.Buildings.TryGetBuilding(buildingInstanceId, out var building))
                return CommandResult.Fail("There is nothing there.");

            if (building.IsBusy) return CommandResult.Fail("Still being built.");

            if (!_world.TryGetProducer(buildingInstanceId, out var producer))
                return CommandResult.Fail("Nothing grows here.");

            if (producer.HasReadyGoods) return CommandResult.Fail("Harvest it first.");
            if (!producer.HasFreeQueueSlot) return CommandResult.Fail("Already growing.");

            var recipe = recipeId ?? DefaultRecipeFor(producer.DefinitionId, producer.LastRecipeId);
            if (string.IsNullOrEmpty(recipe)) return CommandResult.Fail("No crop is unlocked yet.");

            if (!_database.TryGetRecipe(recipe, out var definition))
                return CommandResult.Fail("Unknown crop.");

            if (!_world.Progression.IsRecipeUnlocked(definition))
                return CommandResult.Fail("Unlocks at town level " + definition.UnlockLevel + ".");

            if (!_world.Barn.ContainsAll(definition.Inputs))
                return CommandResult.Fail("Not enough materials.");

            return producer.TryEnqueue(recipe, _world.Barn)
                ? CommandResult.Ok("Planted.")
                : CommandResult.Fail("Could not plant that here.");
        }

        /// <summary>Takes the finished goods, pays the XP, and re-sows if the field is upgraded to.</summary>
        public CommandResult Harvest(string buildingInstanceId)
        {
            if (!_world.TryGetProducer(buildingInstanceId, out var producer))
                return CommandResult.Fail("There is nothing to collect.");

            if (!producer.HasReadyGoods) return CommandResult.Fail("Not ready yet.");

            var collected = _world.Collect(buildingInstanceId);
            if (collected <= 0) return CommandResult.Fail("The barn is full.");

            return producer.HasReadyGoods
                ? CommandResult.Ok("Collected " + collected + ". The barn is full.")
                : CommandResult.Ok("Collected " + collected + ".");
        }

        /// <summary>
        /// Harvests whatever stands on a cell. The sickle swipe's entry point: it works in world
        /// space and knows only which tile the blade passed over.
        ///
        /// Silent on an empty or unripe cell — a swipe crosses a lot of ground, and a failure
        /// message for every tile that was not ready would bury the one that was.
        /// </summary>
        public bool HarvestAt(GridPosition cell)
        {
            if (!_world.Buildings.Grid.TryGetOccupant(cell, out var instanceId)) return false;
            if (!_world.TryGetProducer(instanceId, out var producer) || !producer.HasReadyGoods) return false;

            return _world.Collect(instanceId) > 0;
        }

        /// <summary>Fields with something waiting. Drives the "harvest all" prompt and the map badges.</summary>
        public void CollectHarvestable(List<BuildingInstance> results)
        {
            if (results == null) return;

            results.Clear();
            _world.Buildings.CollectByCategory(BuildingCategory.Farming, _buffer);

            for (var i = 0; i < _buffer.Count; i++)
            {
                if (_world.TryGetProducer(_buffer[i].InstanceId, out var producer) && producer.HasReadyGoods)
                    results.Add(_buffer[i]);
            }
        }

        // --- Buildings --------------------------------------------------------------------------

        public CommandResult Build(string definitionId, GridPosition origin)
        {
            var result = _world.Buildings.TryPlace(definitionId, origin, out _);
            return result == BuildingActionResult.Success
                ? CommandResult.Ok("Under construction.")
                : CommandResult.Fail(Describe(result));
        }

        public CommandResult Upgrade(string buildingInstanceId)
        {
            var result = _world.Buildings.TryUpgrade(buildingInstanceId);
            return result == BuildingActionResult.Success
                ? CommandResult.Ok("Upgrading.")
                : CommandResult.Fail(Describe(result));
        }

        public CommandResult Move(string buildingInstanceId, GridPosition origin)
        {
            var result = _world.Buildings.TryMove(buildingInstanceId, origin);
            return result == BuildingActionResult.Success
                ? CommandResult.Ok("Moved.")
                : CommandResult.Fail(Describe(result));
        }

        // --- Orders -----------------------------------------------------------------------------

        public CommandResult Deliver(string orderId)
        {
            // Found by searching the boards rather than assuming one, so a train order delivers
            // through exactly the same path as a helicopter order.
            if (!_world.TryGetBoardFor(orderId, out var board) || !board.TryGetOrder(orderId, out var order))
                return CommandResult.Fail("That order is gone.");

            if (order.IsExpired(_clock.UtcNowTicks)) return CommandResult.Fail("That order expired.");
            if (!board.CanComplete(orderId)) return CommandResult.Fail("You are missing goods.");

            return board.TryComplete(orderId)
                ? CommandResult.Ok("Delivered.")
                : CommandResult.Fail("Could not deliver that.");
        }

        /// <summary>
        /// Pays to replace an order with a fresh one.
        ///
        /// The coins buy the slot's cooldown, which is the only thing here worth selling — a
        /// reroll moves no goods, so however often it is used it cannot bend the production
        /// economy the way a discounted purchase could.
        /// </summary>
        public CommandResult Reroll(string orderId)
        {
            if (!_world.TryGetBoardFor(orderId, out var board))
                return CommandResult.Fail("That order is gone.");

            var cost = board.RerollCost(orderId);
            if (!_world.Wallet.CanAfford(SoftCurrencyId, cost))
                return CommandResult.Fail("Rerolling costs " + cost + ".");

            return board.TryReroll(orderId)
                ? CommandResult.Ok("New order in.")
                : CommandResult.Fail("Could not reroll that.");
        }

        // --- Market -----------------------------------------------------------------------------

        /// <summary>
        /// Sells goods for coins at the market's rate.
        ///
        /// Worth saying plainly when it refuses: the two things the market will not take — goods
        /// that cost no barn space, and goods worth nothing — look identical from a screen, and a
        /// button that does nothing without explanation reads as broken.
        /// </summary>
        public CommandResult Sell(string itemId, int count)
        {
            if (count <= 0) return CommandResult.Fail("Nothing selected.");
            if (_world.Barn.CountOf(itemId) < count) return CommandResult.Fail("You do not have that many.");

            if (_world.Market.UnitPrice(itemId) <= 0)
                return CommandResult.Fail(DisplayNameOf(itemId) + " is not for sale.");

            var paid = _world.Market.Sell(itemId, count);
            return paid > 0
                ? CommandResult.Ok("Sold " + count + " for " + paid + ".")
                : CommandResult.Fail("Could not sell that.");
        }

        public CommandResult SellAll(string itemId) => Sell(itemId, _world.Barn.CountOf(itemId));

        /// <summary>
        /// Buys goods into the barn at the market's markup.
        ///
        /// The refusals are worth spelling out. "Not stocked" and "cannot afford" and "no room"
        /// all look like a dead button otherwise, and the last of them is the one a player will
        /// hit most: the barn being full is exactly when they want to buy the thing that finishes
        /// an order.
        /// </summary>
        public CommandResult Buy(string itemId, int count)
        {
            if (count <= 0) return CommandResult.Fail("Nothing to buy.");

            var unit = _world.Market.BuyPrice(itemId);
            if (unit <= 0) return CommandResult.Fail(DisplayNameOf(itemId) + " is not for sale here.");

            if (_world.Barn.RoomFor(itemId) < count)
                return CommandResult.Fail("The barn has no room for that.");

            var cost = unit * count;
            if (!_world.Wallet.CanAfford(SoftCurrencyId, cost))
                return CommandResult.Fail("Not enough coins — that costs " + cost + ".");

            return _world.Market.Buy(itemId, count) > 0
                ? CommandResult.Ok("Bought " + count + " for " + cost + ".")
                : CommandResult.Fail("Could not buy that.");
        }

        /// <summary>
        /// Buys exactly what an order is short of. The only place buying really makes sense, and
        /// the moment the player is looking at the shortfall.
        /// </summary>
        public CommandResult BuyShortfall(string orderId, string itemId)
        {
            if (!_world.TryGetBoardFor(orderId, out var board) || !board.TryGetOrder(orderId, out var order))
                return CommandResult.Fail("That order is gone.");

            var needed = 0;
            for (var i = 0; i < order.Requests.Count; i++)
            {
                if (order.Requests[i].ItemId == itemId) needed = order.Requests[i].Count;
            }

            var shortfall = needed - _world.Barn.CountOf(itemId);
            return shortfall <= 0
                ? CommandResult.Fail("You already have enough.")
                : Buy(itemId, shortfall);
        }

        string SoftCurrencyId
        {
            get
            {
                var currency = _database.SoftCurrency;
                return currency != null ? currency.Id : string.Empty;
            }
        }

        // --- Land -------------------------------------------------------------------------------

        public CommandResult UnlockLand(string expansionId)
        {
            var result = _world.Expansion.TryUnlock(expansionId);
            return result == ExpansionResult.Success
                ? CommandResult.Ok("Land unlocked.")
                : CommandResult.Fail(Describe(result));
        }

        // --- Helpers ----------------------------------------------------------------------------

        /// <summary>
        /// What to sow with a single tap: the last crop if it is still legal, otherwise the first
        /// recipe on the producer the player has unlocked. Content order is therefore the
        /// designer's lever over what a new field defaults to.
        /// </summary>
        public string DefaultRecipeFor(string producerDefinitionId, string preferredRecipeId = null)
        {
            if (!_database.TryGetProducer(producerDefinitionId, out var producer)) return null;

            var recipes = producer.Recipes;
            IRecipeDefinition fallback = null;

            for (var i = 0; i < recipes.Count; i++)
            {
                var recipe = recipes[i];
                if (recipe == null || !_world.Progression.IsRecipeUnlocked(recipe)) continue;
                if (!_world.Barn.ContainsAll(recipe.Inputs)) continue;

                if (recipe.Id == preferredRecipeId) return recipe.Id;
                if (fallback == null) fallback = recipe;
            }

            return fallback?.Id;
        }

        /// <summary>
        /// The best name available without a localisation table. Falls back to the id, which is
        /// still more use in a failure message than nothing.
        /// </summary>
        string DisplayNameOf(string itemId) =>
            _database.TryGetItem(itemId, out var item) ? item.DisplayNameKey : itemId;

        public static string Describe(BuildingActionResult result)
        {
            switch (result)
            {
                case BuildingActionResult.Success: return "";
                case BuildingActionResult.UnknownDefinition: return "That building does not exist.";
                case BuildingActionResult.BuildingNotFound: return "That building is gone.";
                case BuildingActionResult.Locked: return "Not unlocked yet.";
                case BuildingActionResult.InsufficientFunds: return "Not enough coins.";
                case BuildingActionResult.InsufficientItems: return "Not enough materials.";
                case BuildingActionResult.InvalidFootprint: return "That building has no size.";
                case BuildingActionResult.OutOfBounds: return "That is outside the town.";
                case BuildingActionResult.Overlaps: return "Something is already there.";
                case BuildingActionResult.AreaLocked: return "You do not own that land yet.";
                case BuildingActionResult.BuildingBusy: return "It is already busy.";
                case BuildingActionResult.AlreadyMaxLevel: return "Fully upgraded.";
                default: return "That did not work.";
            }
        }

        public static string Describe(ExpansionResult result)
        {
            switch (result)
            {
                case ExpansionResult.Success: return "";
                case ExpansionResult.UnknownExpansion: return "That land does not exist.";
                case ExpansionResult.AlreadyUnlocked: return "You already own that.";
                case ExpansionResult.Locked: return "Not unlocked yet.";
                case ExpansionResult.PrerequisiteNotMet: return "Unlock the land next to it first.";
                case ExpansionResult.InsufficientItems: return "You need more land deeds.";
                case ExpansionResult.InsufficientFunds: return "Not enough coins.";
                case ExpansionResult.InvalidRegion: return "That land is outside the map.";
                default: return "That did not work.";
            }
        }
    }
}
