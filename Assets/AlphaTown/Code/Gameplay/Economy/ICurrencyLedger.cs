using System.Collections.Generic;
using AlphaTown.Data.Economy;

namespace AlphaTown.Gameplay.Economy
{
    /// <summary>
    /// Lifetime source/sink totals per currency — the numbers an economy is actually tuned on.
    ///
    /// Individual transactions go out as events for analytics to forward; only these aggregates
    /// are persisted, so the save stays a fixed size no matter how long someone plays.
    /// </summary>
    public interface ICurrencyLedger
    {
        void Record(CurrencyTransaction transaction);

        long TotalFrom(string currencyId, CurrencySource source);
        long TotalTo(string currencyId, CurrencySink sink);

        /// <summary>Everything ever granted. Faucet size.</summary>
        long TotalEarned(string currencyId);

        /// <summary>Everything ever spent. Sink size. Faucet minus sink should track the balance.</summary>
        long TotalSpent(string currencyId);

        IReadOnlyList<LedgerEntry> Snapshot();

        void RestoreFrom(IReadOnlyList<LedgerEntry> entries);
    }
}
