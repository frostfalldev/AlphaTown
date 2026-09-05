#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using AlphaTown.Core.Diagnostics;
using AlphaTown.Services.Timing;
using AlphaTown.Data.Catalog;
using AlphaTown.Data.Economy;
using AlphaTown.Data.Items;
using AlphaTown.Data.Progression;
using AlphaTown.Gameplay.World;

namespace AlphaTown.Gameplay.Commands
{
    /// <summary>
    /// Cheats, for testing the parts of the game that are hours away.
    ///
    /// The ship board opens at town level 5 and refills every few hours; crops that take a minute
    /// still add up to an evening before any of that is reachable. Content nobody can reach is
    /// content nobody has checked, so the way to look at it has to exist — and it has to be
    /// impossible to ship.
    ///
    /// The whole file is compiled out of release builds. Not merely hidden: a cheat API present in
    /// a shipped binary is a cheat API, whatever the UI does or does not call.
    ///
    /// Every grant uses the DebugGrant reason codes, which were reserved for exactly this. Debug
    /// coins therefore never contaminate the economy numbers a real session produces — a ledger
    /// that cannot tell a test from a player is a ledger you cannot trust.
    /// </summary>
    public sealed class DebugCommands
    {
        readonly GameWorld _world;
        readonly IGameDatabase _database;
        readonly GameClock _clock;

        public DebugCommands(GameWorld world, IGameDatabase database, GameClock clock)
        {
            _world = Guard.NotNull(world, nameof(world));
            _database = Guard.NotNull(database, nameof(database));
            _clock = clock;
        }

        /// <summary>Enough XP for exactly one level, or a nudge if already at the cap.</summary>
        public CommandResult LevelUp()
        {
            var progression = _world.Progression;
            if (progression.IsMaxLevel) return CommandResult.Fail("Already at the level cap.");

            var needed = progression.XpToNextLevel;
            progression.GrantXp(needed > 0 ? needed : 1, XpSource.DebugGrant, "debug");
            _world.Sync();

            return CommandResult.Ok("Now level " + progression.TownLevel + ".");
        }

        public CommandResult GrantCoins(int amount) => Grant(_database.SoftCurrency, amount);

        public CommandResult GrantGems(int amount) => Grant(_database.HardCurrency, amount);

        CommandResult Grant(ICurrencyDefinition currency, int amount)
        {
            if (currency == null) return CommandResult.Fail("No such currency is configured.");

            _world.Wallet.Grant(currency.Id, amount, CurrencySource.DebugGrant, "debug");
            return CommandResult.Ok("+" + amount + " " + currency.Id + ".");
        }

        /// <summary>
        /// Tops up every storable good, as far as the barn allows.
        ///
        /// Bulk orders are the thing hardest to reach honestly — a ship wants three or four goods
        /// at once, several of them deep in a production chain — so this is what makes the late
        /// boards testable at all.
        /// </summary>
        public CommandResult FillBarn(int perItem)
        {
            var items = _database.Items;
            if (items == null || items.Count == 0) return CommandResult.Fail("No items are defined.");

            var added = 0;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null || !item.IsStorable) continue;

                added += _world.Barn.Add(item.Id, perItem);
            }

            return added > 0
                ? CommandResult.Ok("Added " + added + " goods.")
                : CommandResult.Fail("The barn is full.");
        }

        /// <summary>
        /// Grants the Special-category items — land deeds, in practice.
        ///
        /// By category rather than by id so it keeps working when the deed is renamed, and so a
        /// project with a second special token gets it too.
        /// </summary>
        public CommandResult GrantSpecialItems(int count)
        {
            var items = _database.Items;
            if (items == null) return CommandResult.Fail("No items are defined.");

            var granted = 0;
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i] == null || items[i].Category != ItemCategory.Special) continue;

                granted += _world.Barn.Add(items[i].Id, count);
            }

            return granted > 0
                ? CommandResult.Ok("+" + granted + " special items.")
                : CommandResult.Fail("Nothing special is defined.");
        }

        /// <summary>
        /// Jumps the clock forward and lets the world catch up.
        ///
        /// This is the single most useful thing here, because it exercises the one property the
        /// whole architecture is built around: every timer is an absolute timestamp, so skipping
        /// eight hours resolves production, construction, slot cooldowns and order expiry in one
        /// pass — the same code path a player returning in the morning takes.
        ///
        /// The offset is not saved, so a restart undoes the jump itself; what it produced stays,
        /// exactly as an ordinary absence would.
        /// </summary>
        public CommandResult SkipTime(TimeSpan amount)
        {
            if (_clock == null) return CommandResult.Fail("No clock to advance.");
            if (amount <= TimeSpan.Zero) return CommandResult.Fail("Nothing to skip.");

            _clock.Advance(amount);
            _world.Sync();

            return CommandResult.Ok("Skipped " + amount.TotalHours.ToString("0.#") + "h.");
        }
    }
}
#endif
