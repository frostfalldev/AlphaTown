using System.Collections.Generic;
using AlphaTown.Core.Diagnostics;
using AlphaTown.Core.Events;
using AlphaTown.Core.Timing;
using AlphaTown.Data.Catalog;
using AlphaTown.Data.Economy;

namespace AlphaTown.Gameplay.Economy
{
    /// <summary>
    /// Currency balances with full source/sink attribution.
    ///
    /// Every movement goes through one of four entry points, each of which demands a reason code,
    /// records to the ledger and publishes a transaction event. There is no path that changes a
    /// balance without attribution — that is the property the whole economy model rests on.
    /// </summary>
    public sealed class Wallet : IWallet
    {
        readonly IGameDatabase _database;
        readonly IGameClock _clock;
        readonly IEventBus _events;
        readonly ICurrencyLedger _ledger;
        readonly Dictionary<string, int> _balances = new Dictionary<string, int>(4);

        /// <summary>Reused by CanAffordAll so a cost check does not allocate.</summary>
        readonly Dictionary<string, int> _costScratch = new Dictionary<string, int>(4);

        public Wallet(IGameDatabase database, IGameClock clock, IEventBus events, ICurrencyLedger ledger)
        {
            _database = Guard.NotNull(database, nameof(database));
            _clock = Guard.NotNull(clock, nameof(clock));
            _events = Guard.NotNull(events, nameof(events));
            _ledger = Guard.NotNull(ledger, nameof(ledger));
        }

        public IReadOnlyDictionary<string, int> Balances => _balances;

        public int BalanceOf(string currencyId)
        {
            if (string.IsNullOrEmpty(currencyId)) return 0;
            return _balances.TryGetValue(currencyId, out var balance) ? balance : 0;
        }

        public bool CanAfford(string currencyId, int amount) => amount <= 0 || BalanceOf(currencyId) >= amount;

        public bool CanAffordAll(IReadOnlyList<CurrencyAmount> costs)
        {
            if (costs == null || costs.Count == 0) return true;

            // Two separate coin costs in one list must be checked against their sum, not
            // individually, or a player with 100 could "afford" 60 + 60.
            _costScratch.Clear();
            for (var i = 0; i < costs.Count; i++)
            {
                var cost = costs[i];
                if (cost.IsEmpty) continue;

                _costScratch.TryGetValue(cost.CurrencyId, out var running);
                _costScratch[cost.CurrencyId] = running + cost.Amount;
            }

            foreach (var pair in _costScratch)
            {
                if (BalanceOf(pair.Key) < pair.Value) return false;
            }

            return true;
        }

        public int Grant(string currencyId, int amount, CurrencySource source, string context = null)
        {
            if (amount <= 0) return 0;
            if (!TryResolve(currencyId, out var definition)) return 0;

            if (source == CurrencySource.Unknown)
            {
                Log.Warn("Wallet",
                    "Untagged grant of " + amount + " " + currencyId +
                    ". Every faucet needs a reason code or the economy cannot be modelled.");
            }

            var granted = amount;
            if (definition.MaxAmount > 0)
            {
                var room = definition.MaxAmount - BalanceOf(currencyId);
                if (room <= 0) granted = 0;
                else if (granted > room) granted = room;
            }

            if (granted > 0) ApplyGrant(currencyId, granted, source, context);

            var discarded = amount - granted;
            if (discarded > 0) _events.Publish(new CurrencyCappedEvent(currencyId, discarded));

            return granted;
        }

        public void GrantAll(IReadOnlyList<CurrencyAmount> rewards, CurrencySource source, string context = null)
        {
            if (rewards == null) return;

            for (var i = 0; i < rewards.Count; i++)
            {
                if (rewards[i].IsEmpty) continue;
                Grant(rewards[i].CurrencyId, rewards[i].Amount, source, context);
            }
        }

        public bool TrySpend(string currencyId, int amount, CurrencySink sink, string context = null)
        {
            if (amount < 0) return false;
            if (amount == 0) return true;
            if (!TryResolve(currencyId, out _)) return false;

            if (sink == CurrencySink.Unknown)
            {
                Log.Warn("Wallet",
                    "Untagged spend of " + amount + " " + currencyId +
                    ". Every sink needs a reason code or the economy cannot be modelled.");
            }

            var balance = BalanceOf(currencyId);
            if (balance < amount)
            {
                _events.Publish(new CurrencySpendRejectedEvent(currencyId, amount, balance));
                return false;
            }

            ApplySpend(currencyId, amount, sink, context);
            return true;
        }

        public bool TrySpendAll(IReadOnlyList<CurrencyAmount> costs, CurrencySink sink, string context = null)
        {
            if (costs == null || costs.Count == 0) return true;

            for (var i = 0; i < costs.Count; i++)
            {
                if (costs[i].Amount < 0) return false;
                if (!costs[i].IsEmpty && !_database.TryGetCurrency(costs[i].CurrencyId, out _))
                {
                    Log.Error("Wallet", "Unknown currency id '" + costs[i].CurrencyId + "' in a cost list.");
                    return false;
                }
            }

            if (!CanAffordAll(costs))
            {
                for (var i = 0; i < costs.Count; i++)
                {
                    if (costs[i].IsEmpty || CanAfford(costs[i].CurrencyId, costs[i].Amount)) continue;

                    _events.Publish(new CurrencySpendRejectedEvent(
                        costs[i].CurrencyId, costs[i].Amount, BalanceOf(costs[i].CurrencyId)));
                    break;
                }

                return false;
            }

            // Affordability was checked across the whole list, so this half cannot fail partway.
            for (var i = 0; i < costs.Count; i++)
            {
                if (costs[i].IsEmpty) continue;
                ApplySpend(costs[i].CurrencyId, costs[i].Amount, sink, context);
            }

            return true;
        }

        /// <summary>Seeds a brand-new player from the starting amounts on the currency definitions.</summary>
        public void InitialiseNewPlayer()
        {
            var currencies = _database.Currencies;
            if (currencies == null) return;

            for (var i = 0; i < currencies.Count; i++)
            {
                var currency = currencies[i];
                if (currency == null || currency.StartingAmount <= 0) continue;

                Grant(currency.Id, currency.StartingAmount, CurrencySource.StartingBalance, "new_player");
            }
        }

        /// <summary>Restores balances from save without replaying every historical transaction.</summary>
        public void ResetTo(IReadOnlyList<CurrencyAmount> balances)
        {
            _balances.Clear();
            if (balances == null) return;

            for (var i = 0; i < balances.Count; i++)
            {
                if (balances[i].IsEmpty) continue;
                _balances[balances[i].CurrencyId] = balances[i].Amount;
            }
        }

        /// <summary>Current balances as a list, for save capture.</summary>
        public List<CurrencyAmount> Snapshot()
        {
            var snapshot = new List<CurrencyAmount>(_balances.Count);
            foreach (var pair in _balances)
            {
                snapshot.Add(new CurrencyAmount(pair.Key, pair.Value));
            }

            return snapshot;
        }

        void ApplyGrant(string currencyId, int amount, CurrencySource source, string context)
        {
            var newBalance = BalanceOf(currencyId) + amount;
            _balances[currencyId] = newBalance;

            var transaction = CurrencyTransaction.Granted(
                currencyId, amount, newBalance, source, context, _clock.UtcNowTicks);

            _ledger.Record(transaction);
            _events.Publish(new CurrencyBalanceChangedEvent(currencyId, newBalance, amount));
            _events.Publish(new CurrencyTransactionEvent(transaction));
        }

        void ApplySpend(string currencyId, int amount, CurrencySink sink, string context)
        {
            var newBalance = BalanceOf(currencyId) - amount;
            if (newBalance <= 0) _balances.Remove(currencyId);
            else _balances[currencyId] = newBalance;

            if (newBalance < 0) newBalance = 0;

            var transaction = CurrencyTransaction.Spent(
                currencyId, amount, newBalance, sink, context, _clock.UtcNowTicks);

            _ledger.Record(transaction);
            _events.Publish(new CurrencyBalanceChangedEvent(currencyId, newBalance, -amount));
            _events.Publish(new CurrencyTransactionEvent(transaction));
        }

        bool TryResolve(string currencyId, out ICurrencyDefinition definition)
        {
            if (_database.TryGetCurrency(currencyId, out definition)) return true;

            // Never invent a currency. A typo that silently created a balance would be invisible
            // until it showed up as an unexplained faucet in the economy numbers.
            Log.Error("Wallet",
                "Unknown currency id '" + currencyId + "'. Refusing to change any balance.");
            return false;
        }
    }
}
