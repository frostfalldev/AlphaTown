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
    }
}
