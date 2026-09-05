using System;
using System.Text.RegularExpressions;
using AlphaTown.Core.Events;
using AlphaTown.Data.Economy;
using AlphaTown.Data.Items;
using AlphaTown.Data.Recipes;
using AlphaTown.Gameplay.Production;
using AlphaTown.Gameplay.Saving;
using AlphaTown.Gameplay.World;
using AlphaTown.Services.Save;
using AlphaTown.Services.Timing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AlphaTown.Tests.EditMode
{
    public sealed class SaveRoundTripTests
    {
        ManualTimeSource _time;
        GameClock _clock;
        EventBus _events;
        FakeDatabase _database;
        ISaveService _saveService;

        [SetUp]
        public void SetUp()
        {
            _time = new ManualTimeSource();
            _clock = new GameClock(_time);
            _events = new EventBus();

            var recipe = new FakeRecipe(
                "bread",
                TimeSpan.FromSeconds(60),
                new[] { new ItemStack("flour", 1) },
                new[] { new ItemStack("bread", 1) });

            _database = new FakeDatabase()
                .WithItem(new FakeItem("flour"))
                .WithItem(new FakeItem("bread"))
                .WithRecipe(recipe)
                .WithStorage(new FakeStorage(100))
                .WithCurrency(new FakeCurrency("coins", CurrencyKind.Soft))
                .WithProgressionCurve(new FakeProgressionCurve(100));

            _database.WithProducer(new FakeProducerDefinition(
                "bakery",
                new IRecipeDefinition[] { recipe },
                new FakeProducerLevel(queueCapacity: 3, parallelSlots: 1)));

            _saveService = new SaveService(
                new InMemorySaveStore(),
                new JsonSaveSerializer(),
                _clock,
                GameWorld.SaveSchemaVersion,
                null,
                "tests");
        }

        GameWorld CreateWorld() => new GameWorld(_database, _clock, _events);

        [Test]
        public void SaveAndLoad_RestoresBarnAndProducers()
        {
            var world = CreateWorld();
            world.AddProducer("bakery_1", "bakery");
            world.Barn.Add("flour", 5);

            Assert.That(_saveService.TrySave(GameWorld.DefaultSaveSlot, world.CaptureSave()), Is.True);
            Assert.That(_saveService.TryLoad<GameSaveData>(GameWorld.DefaultSaveSlot, out var data), Is.True);

            var restored = CreateWorld();
            restored.RestoreSave(data);

            Assert.That(restored.Barn.CountOf("flour"), Is.EqualTo(5));
            Assert.That(restored.Producers.Count, Is.EqualTo(1));
            Assert.That(restored.Producers[0].InstanceId, Is.EqualTo("bakery_1"));
        }

        /// <summary>
        /// The behaviour the whole timestamp design exists for: an order queued before the app
        /// closed is finished when the player comes back, with nothing simulated in between.
        /// </summary>
        [Test]
        public void LoadingAfterAnAbsence_AppliesOfflineProgress()
        {
            var world = CreateWorld();
            var bakery = world.AddProducer("bakery_1", "bakery");
            world.Barn.Add("flour", 2);
            Assert.That(bakery.TryEnqueue("bread", world.Barn), Is.True);

            _saveService.TrySave(GameWorld.DefaultSaveSlot, world.CaptureSave());

            // The player closes the game and comes back the next day.
            _time.Advance(TimeSpan.FromHours(20));

            _saveService.TryLoad<GameSaveData>(GameWorld.DefaultSaveSlot, out var data);
            var restored = CreateWorld();
            restored.RestoreSave(data);

            Assert.That(restored.TryGetProducer("bakery_1", out var restoredBakery), Is.True);
            Assert.That(restoredBakery.Orders.Count, Is.EqualTo(0), "the order should have finished offline");
            Assert.That(restoredBakery.Ready.Count, Is.EqualTo(1));
            Assert.That(restored.Barn.CountOf("flour"), Is.EqualTo(1), "inputs were paid before saving");
        }

        [Test]
        public void LoadingASaveFromANewerBuild_IsRefused()
        {
            var store = new InMemorySaveStore();
            var serializer = new JsonSaveSerializer();

            var future = new SaveService(store, serializer, _clock, GameWorld.SaveSchemaVersion + 1, null, "tests");
            var current = new SaveService(store, serializer, _clock, GameWorld.SaveSchemaVersion, null, "tests");

            var world = CreateWorld();
            future.TrySave(GameWorld.DefaultSaveSlot, world.CaptureSave());

            // Refusing is the point, and refusing loudly is part of it — so the error is expected
            // rather than incidental. Without this the test fails on its own success.
            LogAssert.Expect(LogType.Error, new Regex("Refusing to load"));

            Assert.That(current.TryLoad<GameSaveData>(GameWorld.DefaultSaveSlot, out var data), Is.False);
            Assert.That(data, Is.Null);
        }
    }
}
