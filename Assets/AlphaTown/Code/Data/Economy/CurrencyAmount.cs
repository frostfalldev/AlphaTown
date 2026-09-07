using System;

namespace AlphaTown.Data.Economy
{
    /// <summary>
    /// A quantity of one currency, identified by id. The currency counterpart to
    /// <see cref="Items.ItemStack"/>, and used the same way: one type across runtime, save data
    /// and rewards, with no conversion at the boundaries.
    /// </summary>
    [Serializable]
    public readonly struct CurrencyAmount : IEquatable<CurrencyAmount>
    {
        public readonly string CurrencyId;
        public readonly int Amount;

        public CurrencyAmount(string currencyId, int amount)
        {
            CurrencyId = currencyId;
            Amount = amount;
        }

        public bool IsEmpty => string.IsNullOrEmpty(CurrencyId) || Amount <= 0;

        public CurrencyAmount WithAmount(int amount) => new CurrencyAmount(CurrencyId, amount);

        public bool Equals(CurrencyAmount other) =>
            Amount == other.Amount && string.Equals(CurrencyId, other.CurrencyId, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is CurrencyAmount other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((CurrencyId != null ? CurrencyId.GetHashCode() : 0) * 397) ^ Amount;
            }
        }

        public override string ToString() => Amount + " " + (CurrencyId ?? "<none>");
    }
}
