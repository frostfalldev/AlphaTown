using System;
using AlphaTown.Core.Events;
using AlphaTown.Data.Economy;
using AlphaTown.Gameplay.Commands;
using AlphaTown.Gameplay.Economy;
using AlphaTown.Gameplay.World;
using AlphaTown.Services.Timing;
using NUnit.Framework;

namespace AlphaTown.Tests.EditMode
{
    /// <summary>
    /// The market is a pressure valve, not a way to play. These pin down the two properties that
    /// keep it that way: it always pays clearly less than delivering, and it will not touch the
    /// things that are not surplus.
    /// </summary>
    public sealed class MarketTests
    {
        ManualTimeSource _time;
        GameClock _clock;
        EventBus _events;
        FakeDatabase _database;
        GameWorld _world;
        TownCommands _commands;

        [SetUp]
        public void SetUp()
        {
            _time = new ManualTimeSource();
            _clock = new GameClock(_time);
            _events = new EventBus();

            _database = TestContent.Build(includeExpansion: true);
            _world = new GameWorld(_database, _clock, _events, new Random(3));
            _world.InitialiseNewPlayer();
            _commands = new TownCommands(_world, _database, _clock);
        }

        [Test]
        public void SellingPaysCoinsAndTakesTheGoods()
        {
            _world.Barn.Add(TestContent.Bread, 3);
            var before = _world.Wallet.BalanceOf(TestContent.Coins);

            var paid = _world.Market.Sell(TestContent.Bread, 2);

            Assert.That(paid, Is.GreaterThan(0));
            Assert.That(_world.Wallet.BalanceOf(TestContent.Coins), Is.EqualTo(before + paid));
            Assert.That(_world.Barn.CountOf(TestContent.Bread), Is.EqualTo(1));
        }

        /// <summary>
        /// The whole design in one assertion. If selling ever pays as well as delivering, the
        /// order board stops being the reason to play and the barn stops being pressure.
        /// </summary>
        [Test]
        public void SellingPaysFarLessThanTheItemIsWorth()
        {
            var unit = _world.Market.UnitPrice(TestContent.Bread);

            Assert.That(unit, Is.LessThan(TestContent.BreadCoinValue),
                "a market that matched an item's face value would replace the order board");
        }

        /// <summary>
        /// Land deeds cost no barn space, which is what marks them as not-surplus. A market that
        /// bought them for a coin each would quietly delete the expansion gate.
        /// </summary>
        [Test]
        public void ItemsThatTakeNoBarnSpaceAreNotForSale()
        {
            _world.Barn.Add(TestContent.Deed, 5);

            Assert.That(_world.Market.UnitPrice(TestContent.Deed), Is.EqualTo(0));
            Assert.That(_world.Market.Sell(TestContent.Deed, 1), Is.EqualTo(0));
            Assert.That(_world.Barn.CountOf(TestContent.Deed), Is.EqualTo(5), "the deeds are still there");
        }

        [Test]
        public void SellingMoreThanYouHoldDoesNothing()
        {
            _world.Barn.Add(TestContent.Bread, 1);
            var before = _world.Wallet.BalanceOf(TestContent.Coins);

            Assert.That(_world.Market.Sell(TestContent.Bread, 2), Is.EqualTo(0));
            Assert.That(_world.Barn.CountOf(TestContent.Bread), Is.EqualTo(1));
            Assert.That(_world.Wallet.BalanceOf(TestContent.Coins), Is.EqualTo(before));
        }

        [Test]
        public void SellingAnUnknownItemDoesNothing()
        {
            Assert.That(_world.Market.Sell("not_a_thing", 1), Is.EqualTo(0));
        }

        [Test]
        public void SellAllEmptiesTheStack()
        {
            _world.Barn.Add(TestContent.Bread, 4);

            var paid = _world.Market.SellAll(TestContent.Bread);

            Assert.That(paid, Is.EqualTo(_world.Market.UnitPrice(TestContent.Bread) * 4));
            Assert.That(_world.Barn.CountOf(TestContent.Bread), Is.EqualTo(0));
        }

        /// <summary>A sale is a faucet, and every faucet is attributed. This one funds nothing else.</summary>
        [Test]
        public void SalesAreAttributedToTheirOwnSource()
        {
            _world.Barn.Add(TestContent.Bread, 2);
            var paid = _world.Market.Sell(TestContent.Bread, 2);

            Assert.That(_world.Ledger.TotalFrom(TestContent.Coins, CurrencySource.ItemSale),
                Is.EqualTo((long)paid));
        }

        [Test]
        public void SellingRaisesAnEvent()
        {
            _world.Barn.Add(TestContent.Bread, 2);

            var seen = 0;
            var soldCount = 0;
            using (_events.Subscribe<ItemSoldEvent>(sold => { seen++; soldCount = sold.Count; }))
            {
                _world.Market.Sell(TestContent.Bread, 2);
            }

            Assert.That(seen, Is.EqualTo(1));
            Assert.That(soldCount, Is.EqualTo(2));
        }

        [Test]
        public void SellingFreesBarnSpace()
        {
            _world.Barn.Add(TestContent.Bread, 5);
            var used = _world.Barn.UsedSpace;

            _world.Market.SellAll(TestContent.Bread);

            Assert.That(_world.Barn.UsedSpace, Is.LessThan(used));
        }

        // --- Through the command layer the UI actually uses ---------------------------------

        [Test]
        public void TheCommandExplainsWhySomethingWillNotSell()
        {
            _world.Barn.Add(TestContent.Deed, 1);

            var result = _commands.Sell(TestContent.Deed, 1);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.Not.Empty);
        }

        [Test]
        public void TheCommandRefusesSellingWhatYouDoNotHave()
        {
            Assert.That(_commands.Sell(TestContent.Bread, 1).Success, Is.False);
        }

        [Test]
        public void TheCommandSellsAndSaysWhatItPaid()
        {
            _world.Barn.Add(TestContent.Bread, 2);

            var result = _commands.SellAll(TestContent.Bread);

            Assert.That(result.Success, Is.True);
            Assert.That(_world.Barn.CountOf(TestContent.Bread), Is.EqualTo(0));
        }

        // --- Buying ---------------------------------------------------------------------------

        [Test]
        public void BuyingTakesCoinsAndFillsTheBarn()
        {
            _world.Wallet.Grant(TestContent.Coins, 1000, CurrencySource.DebugGrant);
            var before = _world.Wallet.BalanceOf(TestContent.Coins);

            var spent = _world.Market.Buy(TestContent.Bread, 2);

            Assert.That(spent, Is.GreaterThan(0));
            Assert.That(_world.Barn.CountOf(TestContent.Bread), Is.EqualTo(2));
            Assert.That(_world.Wallet.BalanceOf(TestContent.Coins), Is.EqualTo(before - spent));
        }

        /// <summary>
        /// The property the whole feature rests on. If buying an item costs less than an order
        /// pays for it, production is optional and the game is a spreadsheet.
        /// </summary>
        [Test]
        public void BuyingCostsMoreThanAnOrderPaysForTheSameGoods()
        {
            var buy = _world.Market.BuyPrice(TestContent.Bread);

            // Orders pay a multiple of the item's coin value; buying has to sit above that.
            Assert.That(buy, Is.GreaterThan(TestContent.BreadCoinValue * 2),
                "buying to fill an order must lose coins, or it becomes the way to play");
        }

        /// <summary>
        /// No round trip may profit. An item priced generously to sell must still cost more to
        /// buy back, whatever the content says, or the market is a money printer.
        /// </summary>
        [Test]
        public void BuyingBackAlwaysCostsMoreThanSellingPaid()
        {
            var database = TestContent.Build();

            // Deliberately perverse content: a sell price far above the item's face value.
            database.WithItem(new FakeItem("gilded", coinValue: 4, sellValue: 500));

            var world = new GameWorld(database, new GameClock(new ManualTimeSource()), new EventBus());
            world.InitialiseNewPlayer();

            Assert.That(world.Market.BuyPrice("gilded"),
                Is.GreaterThan(world.Market.UnitPrice("gilded")));
        }

        /// <summary>
        /// The most important guard here. Land is gated by deeds rather than coins by design; a
        /// market that sold deeds would turn expansion straight back into a coin purchase.
        /// </summary>
        [Test]
        public void LandDeedsCannotBeBought()
        {
            _world.Wallet.Grant(TestContent.Coins, 100000, CurrencySource.DebugGrant);

            Assert.That(_world.Market.BuyPrice(TestContent.Deed), Is.EqualTo(0));
            Assert.That(_world.Market.Buy(TestContent.Deed, 1), Is.EqualTo(0));
            Assert.That(_world.Barn.CountOf(TestContent.Deed), Is.EqualTo(0));
        }

        [Test]
        public void BuyingWhatYouCannotAffordDoesNothing()
        {
            _world.Wallet.ResetTo(Array.Empty<CurrencyAmount>());

            Assert.That(_world.Market.Buy(TestContent.Bread, 1), Is.EqualTo(0));
            Assert.That(_world.Barn.CountOf(TestContent.Bread), Is.EqualTo(0));
        }

        /// <summary>Charging for goods that then did not fit would be the worst bug here.</summary>
        [Test]
        public void BuyingMoreThanTheBarnHoldsDoesNothing()
        {
            _world.Wallet.Grant(TestContent.Coins, 100000, CurrencySource.DebugGrant);
            var before = _world.Wallet.BalanceOf(TestContent.Coins);

            Assert.That(_world.Market.Buy(TestContent.Bread, _world.Barn.Capacity + 1), Is.EqualTo(0));
            Assert.That(_world.Wallet.BalanceOf(TestContent.Coins), Is.EqualTo(before));
            Assert.That(_world.Barn.CountOf(TestContent.Bread), Is.EqualTo(0));
        }

        [Test]
        public void PurchasesAreAttributedToTheirOwnSink()
        {
            _world.Wallet.Grant(TestContent.Coins, 1000, CurrencySource.DebugGrant);
            var spent = _world.Market.Buy(TestContent.Bread, 1);

            Assert.That(_world.Ledger.TotalTo(TestContent.Coins, CurrencySink.MarketPurchase),
                Is.EqualTo((long)spent));
        }

        [Test]
        public void BuyingRaisesAnEvent()
        {
            _world.Wallet.Grant(TestContent.Coins, 1000, CurrencySource.DebugGrant);

            var seen = 0;
            using (_events.Subscribe<ItemBoughtEvent>(_ => seen++))
            {
                _world.Market.Buy(TestContent.Bread, 1);
            }

            Assert.That(seen, Is.EqualTo(1));
        }

        /// <summary>
        /// A world whose orders always ask for four of one thing, so "buys exactly the shortfall"
        /// is observable at all — the default template asks for one, where every shortfall is the
        /// whole request.
        /// </summary>
        (GameWorld World, TownCommands Commands) BulkOrderWorld()
        {
            var template = TestContent.SingleBreadTemplate();
            template.MinQuantityPerItem = 4;
            template.MaxQuantityPerItem = 4;

            var database = TestContent.Build(orderTemplate: template);
            var world = new GameWorld(database, _clock, _events, new Random(9));
            world.InitialiseNewPlayer();
            world.Wallet.Grant(TestContent.Coins, 100000, CurrencySource.DebugGrant);

            return (world, new TownCommands(world, database, _clock));
        }

        /// <summary>The one button that exists: close the gap on an order, exactly.</summary>
        [Test]
        public void BuyingAnOrderShortfallBuysExactlyWhatIsMissing()
        {
            var (world, commands) = BulkOrderWorld();

            var order = world.HelicopterOrders.Orders[0];
            var request = order.Requests[0];
            world.Barn.Add(request.ItemId, 1);

            var result = commands.BuyShortfall(order.OrderId, request.ItemId);

            Assert.That(result.Success, Is.True);
            Assert.That(world.Barn.CountOf(request.ItemId), Is.EqualTo(request.Count),
                "it should top the barn up to the request, not double it");
        }

        /// <summary>Having enough already is not a purchase, however much money is on the table.</summary>
        [Test]
        public void BuyingAShortfallYouDoNotHaveIsRefused()
        {
            var (world, commands) = BulkOrderWorld();

            var order = world.HelicopterOrders.Orders[0];
            var request = order.Requests[0];
            world.Barn.Add(request.ItemId, request.Count);

            var spentBefore = world.Wallet.BalanceOf(TestContent.Coins);

            Assert.That(commands.BuyShortfall(order.OrderId, request.ItemId).Success, Is.False);
            Assert.That(world.Wallet.BalanceOf(TestContent.Coins), Is.EqualTo(spentBefore));
        }

        [Test]
        public void BuyingAShortfallOnAnOrderThatIsGoneIsRefused()
        {
            Assert.That(_commands.BuyShortfall("order_nope", TestContent.Bread).Success, Is.False);
        }
    }
}
