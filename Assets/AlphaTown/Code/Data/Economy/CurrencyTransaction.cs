using System;

namespace AlphaTown.Data.Economy
{
    /// <summary>
    /// One movement of currency, with everything an economy dashboard needs to attribute it.
    ///
    /// Individual transactions are published as events for analytics to forward; only lifetime
    /// aggregates are persisted, because a full ledger would outgrow the save file within days.
    /// </summary>
    public readonly struct CurrencyTransaction
    {
        public readonly string CurrencyId;

        /// <summary>Signed: positive is a grant, negative is a spend.</summary>
        public readonly int Delta;

        public readonly int BalanceAfter;

        /// <summary>Meaningful on grants; <see cref="CurrencySource.Unknown"/> on spends.</summary>
        public readonly CurrencySource Source;

        /// <summary>Meaningful on spends; <see cref="CurrencySink.Unknown"/> on grants.</summary>
        public readonly CurrencySink Sink;

        /// <summary>Which specific thing: an order id, a producer instance id. Optional.</summary>
        public readonly string Context;

        public readonly long TimestampTicks;

        CurrencyTransaction(string currencyId, int delta, int balanceAfter, CurrencySource source,
                            CurrencySink sink, string context, long timestampTicks)
        {
            CurrencyId = currencyId;
            Delta = delta;
            BalanceAfter = balanceAfter;
            Source = source;
            Sink = sink;
            Context = context;
            TimestampTicks = timestampTicks;
        }

        public bool IsGrant => Delta > 0;

        public static CurrencyTransaction Granted(string currencyId, int amount, int balanceAfter,
                                                  CurrencySource source, string context, long timestampTicks) =>
            new CurrencyTransaction(currencyId, amount, balanceAfter, source, CurrencySink.Unknown,
                                    context, timestampTicks);

        public static CurrencyTransaction Spent(string currencyId, int amount, int balanceAfter,
                                                CurrencySink sink, string context, long timestampTicks) =>
            new CurrencyTransaction(currencyId, -Math.Abs(amount), balanceAfter, CurrencySource.Unknown,
                                    sink, context, timestampTicks);

        public override string ToString() =>
            (Delta > 0 ? "+" : string.Empty) + Delta + " " + CurrencyId +
            " (" + (IsGrant ? Source.ToString() : Sink.ToString()) + ")";
    }
}
