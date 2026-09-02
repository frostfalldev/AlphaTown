using System;
using System.Collections.Generic;
using AlphaTown.Data.Economy;

namespace AlphaTown.Gameplay.Economy
{
    /// <summary>
    /// In-memory aggregate ledger. Bounded by the number of (currency × reason) pairs the game
    /// actually uses — a few dozen — so it never grows with play time.
    /// </summary>
    public sealed class CurrencyLedger : ICurrencyLedger
    {
        readonly struct Key : IEquatable<Key>
        {
            public readonly string CurrencyId;
            public readonly bool IsSource;
            public readonly int Reason;

            public Key(string currencyId, bool isSource, int reason)
            {
                CurrencyId = currencyId;
                IsSource = isSource;
                Reason = reason;
            }

            public bool Equals(Key other) =>
                IsSource == other.IsSource && Reason == other.Reason &&
                string.Equals(CurrencyId, other.CurrencyId, StringComparison.Ordinal);

            public override bool Equals(object obj) => obj is Key other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = CurrencyId != null ? CurrencyId.GetHashCode() : 0;
                    hash = (hash * 397) ^ Reason;
                    return (hash * 397) ^ (IsSource ? 1 : 0);
                }
            }
        }

        readonly Dictionary<Key, long> _totals = new Dictionary<Key, long>(32);

        public void Record(CurrencyTransaction transaction)
        {
            if (string.IsNullOrEmpty(transaction.CurrencyId) || transaction.Delta == 0) return;

            var isSource = transaction.IsGrant;
            var reason = isSource ? (int)transaction.Source : (int)transaction.Sink;
            var key = new Key(transaction.CurrencyId, isSource, reason);

            // Sinks accumulate as positive magnitudes: "spent 500" reads better than "spent -500".
            var magnitude = transaction.Delta > 0 ? transaction.Delta : -transaction.Delta;

            _totals.TryGetValue(key, out var running);
            _totals[key] = running + magnitude;
        }

        public long TotalFrom(string currencyId, CurrencySource source) =>
            Lookup(new Key(currencyId, true, (int)source));

        public long TotalTo(string currencyId, CurrencySink sink) =>
            Lookup(new Key(currencyId, false, (int)sink));

        public long TotalEarned(string currencyId) => SumWhere(currencyId, isSource: true);

        public long TotalSpent(string currencyId) => SumWhere(currencyId, isSource: false);

        public IReadOnlyList<LedgerEntry> Snapshot()
        {
            var entries = new List<LedgerEntry>(_totals.Count);
            foreach (var pair in _totals)
            {
                entries.Add(new LedgerEntry(pair.Key.CurrencyId, pair.Key.IsSource, pair.Key.Reason, pair.Value));
            }

            return entries;
        }

        public void RestoreFrom(IReadOnlyList<LedgerEntry> entries)
        {
            _totals.Clear();
            if (entries == null) return;

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (string.IsNullOrEmpty(entry.CurrencyId)) continue;

                _totals[new Key(entry.CurrencyId, entry.IsSource, entry.Reason)] = entry.Total;
            }
        }

        long Lookup(Key key) => _totals.TryGetValue(key, out var total) ? total : 0L;

        long SumWhere(string currencyId, bool isSource)
        {
            var sum = 0L;
            foreach (var pair in _totals)
            {
                if (pair.Key.IsSource != isSource) continue;
                if (!string.Equals(pair.Key.CurrencyId, currencyId, StringComparison.Ordinal)) continue;

                sum += pair.Value;
            }

            return sum;
        }
    }
}
