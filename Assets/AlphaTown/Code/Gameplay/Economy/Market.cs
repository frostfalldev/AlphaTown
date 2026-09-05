using AlphaTown.Core.Diagnostics;
using AlphaTown.Core.Events;
using AlphaTown.Data.Catalog;
using AlphaTown.Data.Economy;
using AlphaTown.Gameplay.Inventory;

namespace AlphaTown.Gameplay.Economy
{
    /// <summary>
    /// Sells surplus goods for coins, badly.
    ///
    /// It exists as a pressure valve, not as a way to play. The barn filling is what sends a
    /// player to the order board, and that only works while delivering is clearly the better
    /// deal — so the market pays about a third of an item's base worth, against the roughly
    /// 1.7x an order pays. Selling nets a fifth of delivering. That gap is the design.
    ///
    /// What it buys instead is that a barn full of the wrong goods is never a dead end: there is
    /// always a move, and it always costs something.
    ///
    /// Buying is the other direction and the loop's only real coin sink. It is priced above what
    /// an order pays, so filling an order with bought goods loses money: it is a tax on
    /// impatience, never a strategy. What the player gets for the loss is the XP, the deed roll
    /// and the freed slot — reasons enough to skip a wait, and not reasons to stop farming.
    /// </summary>
    public sealed class Market
    {
        /// <summary>
        /// Paid per unit as a percentage of an item's coin value, when the item does not price
        /// itself. Low enough that selling is a concession.
        ///
        /// TODO(economy): move to an economy definition asset when one exists, alongside the
        /// order payout multipliers it has to stay in proportion with.
        /// </summary>
        public const int DefaultSellRatePercent = 35;

        /// <summary>
        /// Paid per unit as a percentage of an item's coin value when buying.
        ///
        /// Above the ~1.7x an order pays, on purpose. Buying a missing item to complete an order
        /// is then a net loss in coins, which is exactly what stops "buy the whole delivery" from
        /// being the game. Drop this below the order multiplier and production becomes optional.
        ///
        /// TODO(economy): move to an economy definition asset when one exists, next to the order
        /// payout multipliers it has to stay in proportion with.
        /// </summary>
        public const int DefaultBuyRatePercent = 250;

        readonly IGameDatabase _database;
        readonly IInventory _barn;
        readonly IWallet _wallet;
        readonly IEventBus _events;

        public Market(IGameDatabase database, IInventory barn, IWallet wallet, IEventBus events)
        {
            _database = Guard.NotNull(database, nameof(database));
            _barn = Guard.NotNull(barn, nameof(barn));
            _wallet = Guard.NotNull(wallet, nameof(wallet));
            _events = Guard.NotNull(events, nameof(events));
        }

        /// <summary>
        /// Coins per unit, or zero for anything the market will not take.
        ///
        /// Two things are refused. Items that take no barn space are not surplus — land deeds are
        /// the expansion gate wearing an item's clothes, and a market that bought them for a coin
        /// each would quietly delete that gate. Items worth nothing stay worth nothing: the
        /// minimum of one coin applies to rounding, never to conjuring value that was not there.
        /// </summary>
        public int UnitPrice(string itemId)
        {
            if (!_database.TryGetItem(itemId, out var item)) return 0;
            if (!item.IsStorable) return 0;
            if (item.SellValue > 0) return item.SellValue;
            if (item.CoinValue <= 0) return 0;

            var price = item.CoinValue * DefaultSellRatePercent / 100;
            return price < 1 ? 1 : price;
        }

        public bool CanSell(string itemId) => UnitPrice(itemId) > 0 && _barn.CountOf(itemId) > 0;

        /// <summary>
        /// Coins per unit to buy, or zero for anything the market will not stock.
        ///
        /// The same two exclusions as selling, and the first of them is load-bearing: land is
        /// gated by deeds rather than coins by design, and a market that sold deeds would turn
        /// expansion straight back into a coin purchase. Deeds cost no barn space, which is what
        /// marks them as not merchandise.
        ///
        /// Never at or below the sell price, whatever the content says. Otherwise an item priced
        /// generously to sell could be bought and resold forever, and an economy with a money
        /// printer in it has no other numbers worth tuning.
        /// </summary>
        public int BuyPrice(string itemId)
        {
            if (!_database.TryGetItem(itemId, out var item)) return 0;
            if (!item.IsStorable) return 0;
            if (item.CoinValue <= 0) return 0;

            var price = item.CoinValue * DefaultBuyRatePercent / 100;
            if (price < 1) price = 1;

            var sell = UnitPrice(itemId);
            return price <= sell ? sell + 1 : price;
        }

        /// <summary>What it would cost to put this many in the barn. Zero if it is not stocked.</summary>
        public int PriceToBuy(string itemId, int count) =>
            count <= 0 ? 0 : BuyPrice(itemId) * count;

        /// <summary>
        /// Buys goods into the barn. Returns the coins spent, or zero if nothing was bought.
        ///
        /// Everything that can refuse is checked before anything moves, so neither half of the
        /// exchange can fail once it has started. Charging for goods that then did not fit would
        /// be the worst bug this class could have.
        /// </summary>
        public int Buy(string itemId, int count)
        {
            if (count <= 0) return 0;

            var unit = BuyPrice(itemId);
            if (unit <= 0) return 0;

            if (_barn.RoomFor(itemId) < count) return 0;

            var currency = _database.SoftCurrency;
            if (currency == null)
            {
                Log.Error("Market", "No soft currency is configured, so nothing can be bought.");
                return 0;
            }

            var cost = unit * count;
            if (!_wallet.CanAfford(currency.Id, cost)) return 0;

            if (!_barn.TryAddExact(itemId, count)) return 0;

            _wallet.TrySpend(currency.Id, cost, CurrencySink.MarketPurchase, itemId);
            _events.Publish(new ItemBoughtEvent(itemId, count, cost));

            return cost;
        }

        /// <summary>What the whole stack in the barn would fetch. Zero if it will not sell.</summary>
        public int PriceOfEverything(string itemId) => UnitPrice(itemId) * _barn.CountOf(itemId);

        /// <summary>Sells a number of units. Returns the coins paid, or zero if nothing was sold.</summary>
        public int Sell(string itemId, int count)
        {
            if (count <= 0) return 0;

            var unit = UnitPrice(itemId);
            if (unit <= 0) return 0;

            if (_barn.CountOf(itemId) < count) return 0;

            // Everything that could refuse is checked before anything moves. A sale that took the
            // goods and then found it had nowhere to pay from would destroy them for nothing.
            var currency = _database.SoftCurrency;
            if (currency == null)
            {
                Log.Error("Market", "No soft currency is configured, so nothing can be sold.");
                return 0;
            }

            if (!_barn.TryRemove(itemId, count)) return 0;

            var paid = unit * count;
            _wallet.Grant(currency.Id, paid, CurrencySource.ItemSale, itemId);
            _events.Publish(new ItemSoldEvent(itemId, count, paid));

            return paid;
        }

        /// <summary>Sells everything of one kind. The common case when the barn is full.</summary>
        public int SellAll(string itemId) => Sell(itemId, _barn.CountOf(itemId));
    }
}
