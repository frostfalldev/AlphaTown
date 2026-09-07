using System.Collections.Generic;
using AlphaTown.Core.Diagnostics;
using AlphaTown.Core.Events;
using AlphaTown.Core.Spatial;
using AlphaTown.Data.Catalog;
using AlphaTown.Data.Economy;
using AlphaTown.Data.Expansion;
using AlphaTown.Data.Town;
using AlphaTown.Gameplay.Economy;
using AlphaTown.Gameplay.Grid;
using AlphaTown.Gameplay.Inventory;
using AlphaTown.Gameplay.Progression;

namespace AlphaTown.Gameplay.Expansion
{
    /// <summary>
    /// Which land the player owns.
    ///
    /// The gate is land deeds — an item, earned from orders — rather than coins. Coins already buy
    /// buildings, and a coin-gated town would grow at whatever rate a player can grind orders,
    /// which is no pacing at all. Deeds drop at a rate the designer sets, so land is the one thing
    /// money cannot rush.
    ///
    /// State is a set of owned expansion ids, and the grid's mask is always rebuilt from that set
    /// rather than accumulated. There is one source of truth and no way for the two to drift.
    /// </summary>
    public sealed class TownExpansion
    {
        readonly IGameDatabase _database;
        readonly IEventBus _events;
        readonly IWallet _wallet;
        readonly IInventory _barn;
        readonly IUnlockGate _unlocks;
        readonly TownGrid _grid;

        readonly HashSet<string> _owned = new HashSet<string>(System.StringComparer.Ordinal);
        readonly List<GridRect> _regionScratch = new List<GridRect>(16);

        readonly GridRect _startingArea;

        public TownExpansion(
            TownGrid grid,
            IGameDatabase database,
            IEventBus events,
            IWallet wallet,
            IInventory barn,
            IUnlockGate unlocks,
            ITownDefinition town)
        {
            _grid = Guard.NotNull(grid, nameof(grid));
            _database = Guard.NotNull(database, nameof(database));
            _events = Guard.NotNull(events, nameof(events));
            _wallet = Guard.NotNull(wallet, nameof(wallet));
            _barn = Guard.NotNull(barn, nameof(barn));
            _unlocks = Guard.NotNull(unlocks, nameof(unlocks));

            // A town with no authored starting area owns everything, which is exactly what a
            // project without expansion content wants.
            _startingArea = town != null && town.StartingArea.IsValid
                ? town.StartingArea
                : new GridRect(GridPosition.Zero, grid.Size);

            ApplyToGrid();
        }

        /// <summary>The patch owned before anything is bought.</summary>
        public GridRect StartingArea => _startingArea;

        public int OwnedCount => _owned.Count;

        public bool IsUnlocked(string expansionId) =>
            !string.IsNullOrEmpty(expansionId) && _owned.Contains(expansionId);

        /// <summary>Same checks as <see cref="TryUnlock"/>, charging nothing.</summary>
        public ExpansionResult CanUnlock(string expansionId)
        {
            if (!_database.TryGetExpansion(expansionId, out var expansion))
                return ExpansionResult.UnknownExpansion;

            return Evaluate(expansion);
        }

        /// <summary>
        /// Buys a region. Deeds and coins are both checked before either is taken — charging one
        /// and failing the other would take payment for land the player never got.
        /// </summary>
        public ExpansionResult TryUnlock(string expansionId)
        {
            if (!_database.TryGetExpansion(expansionId, out var expansion))
                return ExpansionResult.UnknownExpansion;

            var verdict = Evaluate(expansion);
            if (verdict != ExpansionResult.Success) return verdict;

            _wallet.TrySpendAll(expansion.CurrencyCost, CurrencySink.ExpansionPurchase, expansion.Id);
            _barn.TryRemoveAll(expansion.ItemCost);

            _owned.Add(expansion.Id);
            ApplyToGrid();

            _events.Publish(new TownExpandedEvent(expansion.Id, expansion.Region));
            return ExpansionResult.Success;
        }

        /// <summary>
        /// Fills <paramref name="results"/> with everything buyable right now — prerequisite met,
        /// level met, not already owned. Affordability is deliberately not filtered: a land menu
        /// wants to show the plot the player is saving deeds for.
        /// </summary>
        public void CollectAvailable(List<IExpansionDefinition> results)
        {
            if (results == null) return;
            results.Clear();

            var all = _database.Expansions;
            if (all == null) return;

            for (var i = 0; i < all.Count; i++)
            {
                var expansion = all[i];
                if (expansion == null || _owned.Contains(expansion.Id)) continue;
                if (!_unlocks.IsUnlocked(expansion.UnlockLevel)) continue;
                if (!PrerequisiteMet(expansion)) continue;

                results.Add(expansion);
            }

            results.Sort(CompareBySortOrder);
        }

        /// <summary>Restores from save. Ids that no longer exist are dropped with a warning.</summary>
        public void RestoreState(IReadOnlyList<string> ownedIds)
        {
            _owned.Clear();

            if (ownedIds != null)
            {
                for (var i = 0; i < ownedIds.Count; i++)
                {
                    var id = ownedIds[i];
                    if (string.IsNullOrEmpty(id)) continue;

                    if (!_database.TryGetExpansion(id, out _))
                    {
                        // Land the player paid for, in a build that no longer defines it. Loud,
                        // because it silently shrinks a town.
                        Log.Error("Expansion",
                            "Save owns unknown expansion '" + id + "'. That land is no longer defined.");
                        continue;
                    }

                    _owned.Add(id);
                }
            }

            ApplyToGrid();
        }

        public List<string> Snapshot()
        {
            var snapshot = new List<string>(_owned.Count);
            foreach (var id in _owned) snapshot.Add(id);
            return snapshot;
        }

        ExpansionResult Evaluate(IExpansionDefinition expansion)
        {
            if (_owned.Contains(expansion.Id)) return ExpansionResult.AlreadyUnlocked;

            var region = expansion.Region;
            if (!region.IsValid || !_grid.IsInBounds(region)) return ExpansionResult.InvalidRegion;

            if (!_unlocks.IsUnlocked(expansion.UnlockLevel)) return ExpansionResult.Locked;
            if (!PrerequisiteMet(expansion)) return ExpansionResult.PrerequisiteNotMet;

            // Checked together, applied together — see TryUnlock.
            if (!_barn.ContainsAll(expansion.ItemCost)) return ExpansionResult.InsufficientItems;
            if (!_wallet.CanAffordAll(expansion.CurrencyCost)) return ExpansionResult.InsufficientFunds;

            return ExpansionResult.Success;
        }

        bool PrerequisiteMet(IExpansionDefinition expansion)
        {
            var required = expansion.RequiresExpansionId;
            return string.IsNullOrEmpty(required) || _owned.Contains(required);
        }

        /// <summary>Rebuilds the grid mask from the owned set. The only writer of the mask.</summary>
        void ApplyToGrid()
        {
            _regionScratch.Clear();
            _regionScratch.Add(_startingArea);

            var all = _database.Expansions;
            if (all != null)
            {
                for (var i = 0; i < all.Count; i++)
                {
                    var expansion = all[i];
                    if (expansion != null && _owned.Contains(expansion.Id))
                        _regionScratch.Add(expansion.Region);
                }
            }

            _grid.SetUnlockedRegions(_regionScratch);
        }

        static int CompareBySortOrder(IExpansionDefinition a, IExpansionDefinition b)
        {
            var order = a.SortOrder.CompareTo(b.SortOrder);
            return order != 0 ? order : string.CompareOrdinal(a.Id, b.Id);
        }
    }
}
