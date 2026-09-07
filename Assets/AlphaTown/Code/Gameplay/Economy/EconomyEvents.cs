using AlphaTown.Data.Economy;

namespace AlphaTown.Gameplay.Economy
{
    /// <summary>Balance moved. The event UI binds to.</summary>
    public readonly struct CurrencyBalanceChangedEvent
    {
        public readonly string CurrencyId;
        public readonly int NewBalance;
        public readonly int Delta;

        public CurrencyBalanceChangedEvent(string currencyId, int newBalance, int delta)
        {
            CurrencyId = currencyId;
            NewBalance = newBalance;
            Delta = delta;
        }
    }

    /// <summary>
    /// Full attribution for one movement. An analytics adapter subscribes here and forwards to
    /// the Services-side sink — which is why the payload is all Data types, nothing from Gameplay.
    /// </summary>
    public readonly struct CurrencyTransactionEvent
    {
        public readonly CurrencyTransaction Transaction;

        public CurrencyTransactionEvent(CurrencyTransaction transaction)
        {
            Transaction = transaction;
        }
    }

    /// <summary>A grant was clipped by the currency's cap. The discarded amount is lost.</summary>
    public readonly struct CurrencyCappedEvent
    {
        public readonly string CurrencyId;
        public readonly int DiscardedAmount;

        public CurrencyCappedEvent(string currencyId, int discardedAmount)
        {
            CurrencyId = currencyId;
            DiscardedAmount = discardedAmount;
        }
    }

    /// <summary>
    /// The player tried to spend more than they had. Not an error — it is the strongest purchase-
    /// intent signal in the game, and the hook an offer surface listens on.
    /// </summary>
    public readonly struct CurrencySpendRejectedEvent
    {
        public readonly string CurrencyId;
        public readonly int RequestedAmount;
        public readonly int Balance;

        public CurrencySpendRejectedEvent(string currencyId, int requestedAmount, int balance)
        {
            CurrencyId = currencyId;
            RequestedAmount = requestedAmount;
            Balance = balance;
        }
    }
}
