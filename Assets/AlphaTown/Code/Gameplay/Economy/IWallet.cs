using System.Collections.Generic;
using AlphaTown.Data.Economy;

namespace AlphaTown.Gameplay.Economy
{
    /// <summary>
    /// The player's currency balances.
    ///
    /// Currency deliberately never enters the barn: it is not bound by storage space, it is not
    /// an item, and — unlike items — every movement has to carry an attribution reason so the
    /// economy can be modelled and hard-currency spend can be audited against revenue.
    ///
    /// Reason codes are required parameters, not optional ones. There is no overload that lets a
    /// caller move currency anonymously.
    /// </summary>
    public interface IWallet
    {
        IReadOnlyDictionary<string, int> Balances { get; }

        int BalanceOf(string currencyId);

        bool CanAfford(string currencyId, int amount);

        /// <summary>Aggregates duplicate currencies, so two coin costs are checked as their sum.</summary>
        bool CanAffordAll(IReadOnlyList<CurrencyAmount> costs);

        /// <summary>Returns the amount actually granted, which is less than asked if a cap clipped it.</summary>
        int Grant(string currencyId, int amount, CurrencySource source, string context = null);

        void GrantAll(IReadOnlyList<CurrencyAmount> rewards, CurrencySource source, string context = null);

        bool TrySpend(string currencyId, int amount, CurrencySink sink, string context = null);

        /// <summary>All-or-nothing. A rejected spend leaves every balance untouched.</summary>
        bool TrySpendAll(IReadOnlyList<CurrencyAmount> costs, CurrencySink sink, string context = null);
    }
}
