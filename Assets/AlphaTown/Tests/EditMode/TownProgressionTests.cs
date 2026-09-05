using System;
using AlphaTown.Core.Events;
using AlphaTown.Data.Economy;
using AlphaTown.Data.Items;
using AlphaTown.Data.Progression;
using AlphaTown.Gameplay.Economy;
using AlphaTown.Gameplay.Progression;
using AlphaTown.Services.Timing;
using NUnit.Framework;

namespace AlphaTown.Tests.EditMode
{
    public sealed class TownProgressionTests
    {
        const string Coins = "coins";

        EventBus _events;
        CurrencyLedger _ledger;
        Wallet _wallet;
        TownProgression _progression;

        [SetUp]
        public void SetUp()
        {
            // Level 1 costs 100 XP, level 2 costs 200. Level 3 is the cap.
            var curve = new FakeProgressionCurve(100, 200, 0)
                .WithRewardForReaching(2, new CurrencyAmount(Coins, 500));

            var database = new FakeDatabase()
                .WithCurrency(new FakeCurrency(Coins, CurrencyKind.Soft))
                .WithProgressionCurve(curve);

            _events = new EventBus();
            _ledger = new CurrencyLedger();
            _wallet = new Wallet(database, new GameClock(new ManualTimeSource()), _events, _ledger);
            _progression = new TownProgression(curve, _wallet, _events);
        }

        [Test]
        public void NewTown_StartsAtLevelOne()
        {
            Assert.That(_progression.TownLevel, Is.EqualTo(1));
            Assert.That(_progression.TotalXp, Is.EqualTo(0));
            Assert.That(_progression.XpToNextLevel, Is.EqualTo(100));
        }

        [Test]
        public void GrantXp_AdvancesOneLevelAndKeepsTheRemainder()
        {
            var gained = _progression.GrantXp(130, XpSource.OrderReward);

            Assert.That(gained, Is.EqualTo(1));
            Assert.That(_progression.TownLevel, Is.EqualTo(2));
            Assert.That(_progression.XpIntoLevel, Is.EqualTo(30));
            Assert.That(_progression.XpToNextLevel, Is.EqualTo(170));
        }

        /// <summary>
        /// One grant can cover several levels — normal after a long absence or a large order.
        /// </summary>
        [Test]
        public void GrantXp_CascadesThroughMultipleLevels()
        {
            var gained = _progression.GrantXp(350, XpSource.OrderReward);

            Assert.That(gained, Is.EqualTo(2));
            Assert.That(_progression.TownLevel, Is.EqualTo(3));
            Assert.That(_progression.XpIntoLevel, Is.EqualTo(50));
            Assert.That(_progression.TotalXp, Is.EqualTo(350));
        }

        [Test]
        public void LevelUpEvent_FiresOncePerLevelGained()
        {
            var levels = 0;
            using (_events.Subscribe<TownLevelUpEvent>(_ => levels++))
            {
                _progression.GrantXp(350, XpSource.OrderReward);
            }

            Assert.That(levels, Is.EqualTo(2));
        }

        [Test]
        public void LevelUp_PaysTheCurveRewardIntoTheWallet()
        {
            _progression.GrantXp(100, XpSource.OrderReward);

            Assert.That(_progression.TownLevel, Is.EqualTo(2));
            Assert.That(_wallet.BalanceOf(Coins), Is.EqualTo(500));
            Assert.That(_ledger.TotalFrom(Coins, CurrencySource.LevelUpReward), Is.EqualTo(500));
        }

        /// <summary>
        /// Level caps get raised in live-ops updates, so XP earned at the cap is banked rather
        /// than thrown away — raising the cap should credit it immediately.
        /// </summary>
        [Test]
        public void XpEarnedAtTheCap_IsRetainedNotDiscarded()
        {
            _progression.GrantXp(300, XpSource.OrderReward);
            Assert.That(_progression.IsMaxLevel, Is.True);

            var gained = _progression.GrantXp(1000, XpSource.OrderReward);

            Assert.That(gained, Is.EqualTo(0));
            Assert.That(_progression.TownLevel, Is.EqualTo(3));
            Assert.That(_progression.XpIntoLevel, Is.EqualTo(1000));
            Assert.That(_progression.TotalXp, Is.EqualTo(1300));
            Assert.That(_progression.XpToNextLevel, Is.EqualTo(0));
        }

        [Test]
        public void UnlockGate_FollowsTheTownLevel()
        {
            var lockedAtThree = new FakeRecipe("cake", TimeSpan.FromMinutes(1),
                new[] { new ItemStack("bread", 2) }, new[] { new ItemStack("cake", 1) }, unlockLevel: 3);

            Assert.That(_progression.IsRecipeUnlocked(lockedAtThree), Is.False);

            _progression.GrantXp(300, XpSource.OrderReward);

            Assert.That(_progression.TownLevel, Is.EqualTo(3));
            Assert.That(_progression.IsRecipeUnlocked(lockedAtThree), Is.True);
        }

        [Test]
        public void XpGrants_AreAttributedBySource()
        {
            _progression.GrantXp(40, XpSource.OrderReward);
            _progression.GrantXp(10, XpSource.QuestReward);
            _progression.GrantXp(5, XpSource.Unknown);

            Assert.That(_progression.TotalXpFrom(XpSource.OrderReward), Is.EqualTo(40));
            Assert.That(_progression.TotalXpFrom(XpSource.QuestReward), Is.EqualTo(10));
            Assert.That(_progression.TotalXpFrom(XpSource.Unknown), Is.EqualTo(5),
                "untagged XP must stay visible rather than vanish");
            Assert.That(_progression.TotalXp, Is.EqualTo(55));
        }

        [Test]
        public void RestoreState_ClampsALevelBeyondTheCurve()
        {
            _progression.RestoreState(99, 10, 5000, null);

            Assert.That(_progression.TownLevel, Is.EqualTo(3));
            Assert.That(_progression.IsMaxLevel, Is.True);
        }
    }
}
