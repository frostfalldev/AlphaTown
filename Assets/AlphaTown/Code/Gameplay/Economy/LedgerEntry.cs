using System;

namespace AlphaTown.Gameplay.Economy
{
    /// <summary>
    /// One lifetime total: how much of a currency has moved through a single source or sink.
    ///
    /// <see cref="Reason"/> is the numeric value of a <see cref="Data.Economy.CurrencySource"/> or
    /// <see cref="Data.Economy.CurrencySink"/>, picked by <see cref="IsSource"/>. Stored as an int
    /// so an unrecognised value from an older save survives the round trip instead of collapsing
    /// onto Unknown and corrupting the totals.
    /// </summary>
    public readonly struct LedgerEntry : IEquatable<LedgerEntry>
    {
        public readonly string CurrencyId;
        public readonly bool IsSource;
        public readonly int Reason;
        public readonly long Total;

        public LedgerEntry(string currencyId, bool isSource, int reason, long total)
        {
            CurrencyId = currencyId;
            IsSource = isSource;
            Reason = reason;
            Total = total;
        }

        public bool Equals(LedgerEntry other) =>
            IsSource == other.IsSource && Reason == other.Reason && Total == other.Total &&
            string.Equals(CurrencyId, other.CurrencyId, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is LedgerEntry other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = CurrencyId != null ? CurrencyId.GetHashCode() : 0;
                hash = (hash * 397) ^ Reason;
                hash = (hash * 397) ^ (IsSource ? 1 : 0);
                return (hash * 397) ^ Total.GetHashCode();
            }
        }
    }
}
