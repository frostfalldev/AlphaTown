using System.Text.RegularExpressions;
using AlphaTown.Core.Events;
using AlphaTown.Data.Economy;
using AlphaTown.Gameplay.Economy;
using AlphaTown.Services.Timing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlphaTown.Tests.EditMode
{
    public sealed class WalletTests
    {
        const string Coins = "coins";
        const string Gems = "gems";

        EventBus _events;
        CurrencyLedger _ledger;
        Wallet _wallet;

        [SetUp]
        public void SetUp()
        {
            var database = new FakeDatabase()
                .WithCurrency(new FakeCurrency(Coins, CurrencyKind.Soft, startingAmount: 50))
                .WithCurrency(new FakeCurrency(Gems, CurrencyKind.Hard, startingAmount: 5, maxAmount: 100));

            _events = new EventBus();
            _ledger = new CurrencyLedger();
            _wallet = new Wallet(database, new GameClock(new ManualTimeSource()), _events, _ledger);
        }

        [Test]
        public void Grant_RaisesTheBalanceAndAttributesTheSource()
        {
            _wallet.Grant(Coins, 250, CurrencySource.OrderReward, "order_1");

            Assert.That(_wallet.BalanceOf(Coins), Is.EqualTo(250));
            Assert.That(_ledger.TotalFrom(Coins, CurrencySource.OrderReward), Is.EqualTo(250));
            Assert.That(_ledger.TotalEarned(Coins), Is.EqualTo(250));
        }

        [Test]
        public void TrySpend_LeavesTheBalanceUntouchedWhenFundsAreShort()
        {
            _wallet.Grant(Coins, 100, CurrencySource.OrderReward);

            Assert.That(_wallet.TrySpend(Coins, 150, CurrencySink.BuildingPurchase), Is.False);
            Assert.That(_wallet.BalanceOf(Coins), Is.EqualTo(100));
            Assert.That(_ledger.TotalSpent(Coins), Is.EqualTo(0));
        }

        [Test]
        public void TrySpendAll_IsAtomicAcrossCurrencies()
        {
            _wallet.Grant(Coins, 100, CurrencySource.OrderReward);
            _wallet.Grant(Gems, 1, CurrencySource.IapPurchase);

            var cost = new[] { new CurrencyAmount(Coins, 50), new CurrencyAmount(Gems, 5) };

            Assert.That(_wallet.TrySpendAll(cost, CurrencySink.BuildingUpgrade), Is.False);
            Assert.That(_wallet.BalanceOf(Coins), Is.EqualTo(100), "the affordable half must not be taken");
            Assert.That(_wallet.BalanceOf(Gems), Is.EqualTo(1));
        }

        /// <summary>
        /// Two costs in the same currency have to be checked against their sum. Checking them
        /// one at a time would let 100 coins pay for 60 + 60.
        /// </summary>
        [Test]
        public void TrySpendAll_AggregatesRepeatedCurrencies()
        {
            _wallet.Grant(Coins, 100, CurrencySource.OrderReward);

            var cost = new[] { new CurrencyAmount(Coins, 60), new CurrencyAmount(Coins, 60) };

            Assert.That(_wallet.CanAffordAll(cost), Is.False);
            Assert.That(_wallet.TrySpendAll(cost, CurrencySink.MarketPurchase), Is.False);
            Assert.That(_wallet.BalanceOf(Coins), Is.EqualTo(100));
        }

        [Test]
        public void Grant_IsClippedByTheCurrencyCap()
        {
            var capped = 0;
            using (_events.Subscribe<CurrencyCappedEvent>(e => capped = e.DiscardedAmount))
            {
                var granted = _wallet.Grant(Gems, 150, CurrencySource.IapPurchase);

                Assert.That(granted, Is.EqualTo(100), "the cap is 100");
                Assert.That(_wallet.BalanceOf(Gems), Is.EqualTo(100));
                Assert.That(capped, Is.EqualTo(50));
            }
        }

        [Test]
        public void Ledger_KeepsSourcesAndSinksSeparate()
        {
            _wallet.Grant(Coins, 500, CurrencySource.OrderReward);
            _wallet.Grant(Coins, 100, CurrencySource.LevelUpReward);
            _wallet.TrySpend(Coins, 200, CurrencySink.BuildingPurchase);

            Assert.That(_ledger.TotalFrom(Coins, CurrencySource.OrderReward), Is.EqualTo(500));
            Assert.That(_ledger.TotalFrom(Coins, CurrencySource.LevelUpReward), Is.EqualTo(100));
            Assert.That(_ledger.TotalTo(Coins, CurrencySink.BuildingPurchase), Is.EqualTo(200));
            Assert.That(_ledger.TotalEarned(Coins), Is.EqualTo(600));
            Assert.That(_ledger.TotalSpent(Coins), Is.EqualTo(200));
            Assert.That(_ledger.TotalEarned(Coins) - _ledger.TotalSpent(Coins),
                Is.EqualTo(_wallet.BalanceOf(Coins)), "faucet minus sink must reconcile with the balance");
        }

        /// <summary>
        /// An untagged movement is a bug, but it must still be visible. Recording it under Unknown
        /// means an unexplained faucet shows up in the economy numbers instead of vanishing.
        /// </summary>
        [Test]
        public void UntaggedMovements_AreStillAttributedUnderUnknown()
        {
            _wallet.Grant(Coins, 70, CurrencySource.Unknown);
            _wallet.TrySpend(Coins, 20, CurrencySink.Unknown);

            Assert.That(_ledger.TotalFrom(Coins, CurrencySource.Unknown), Is.EqualTo(70));
            Assert.That(_ledger.TotalTo(Coins, CurrencySink.Unknown), Is.EqualTo(20));
        }

        /// <summary>The strongest purchase-intent signal in the game, so it must be observable.</summary>
        [Test]
        public void RejectedSpend_PublishesAnEvent()
        {
            var rejections = 0;
            using (_events.Subscribe<CurrencySpendRejectedEvent>(_ => rejections++))
            {
                _wallet.TrySpend(Gems, 500, CurrencySink.ProductionSpeedUp);
            }

            Assert.That(rejections, Is.EqualTo(1));
        }

        [Test]
        public void UnknownCurrency_IsRefusedRatherThanInvented()
        {
            LogAssert.Expect(LogType.Error, new Regex("Unknown currency"));

            Assert.That(_wallet.Grant("rubies", 100, CurrencySource.DebugGrant), Is.EqualTo(0));
            Assert.That(_wallet.BalanceOf("rubies"), Is.EqualTo(0));
        }

        [Test]
        public void InitialiseNewPlayer_SeedsFromTheCurrencyDefinitions()
        {
            _wallet.InitialiseNewPlayer();

            Assert.That(_wallet.BalanceOf(Coins), Is.EqualTo(50));
            Assert.That(_wallet.BalanceOf(Gems), Is.EqualTo(5));
            Assert.That(_ledger.TotalFrom(Coins, CurrencySource.StartingBalance), Is.EqualTo(50));
        }
    }
}
