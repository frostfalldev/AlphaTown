using System;
using System.Collections.Generic;
using AlphaTown.Data.Catalog;
using AlphaTown.Data.Items;
using AlphaTown.Data.Production;
using AlphaTown.Data.Recipes;
using AlphaTown.Data.Storage;
using AlphaTown.Services.Save;

namespace AlphaTown.Tests.EditMode
{
    /// <summary>
    /// Plain-object stand-ins for the ScriptableObject definitions.
    ///
    /// Nothing here touches Unity assets, which is the whole reason the gameplay systems depend
    /// on interfaces: a test builds a content set in three lines instead of importing a project.
    /// </summary>
    internal sealed class FakeItem : IItemDefinition
    {
        public FakeItem(string id, int storageCost = 1, bool isStorable = true)
        {
            Id = id;
            StorageCost = storageCost;
            IsStorable = isStorable;
        }

        public string Id { get; }
        public string DisplayNameKey => "item." + Id;
        public ItemCategory Category => ItemCategory.Ingredient;
        public int StorageCost { get; }
        public bool IsStorable { get; }
    }

    internal sealed class FakeRecipe : IRecipeDefinition
    {
        public FakeRecipe(string id, TimeSpan duration, ItemStack[] inputs, ItemStack[] outputs)
        {
            Id = id;
            Duration = duration;
            Inputs = inputs ?? Array.Empty<ItemStack>();
            Outputs = outputs ?? Array.Empty<ItemStack>();
        }

        public string Id { get; }
        public IReadOnlyList<ItemStack> Inputs { get; }
        public IReadOnlyList<ItemStack> Outputs { get; }
        public TimeSpan Duration { get; }
        public int UnlockLevel => 1;
    }

    internal sealed class FakeProducerLevel : IProducerLevel
    {
        public FakeProducerLevel(int queueCapacity, int parallelSlots, float speedMultiplier = 1f)
        {
            QueueCapacity = queueCapacity;
            ParallelSlots = parallelSlots;
            SpeedMultiplier = speedMultiplier;
        }

        public int QueueCapacity { get; }
        public int ParallelSlots { get; }
        public float SpeedMultiplier { get; }
    }

    internal sealed class FakeProducerDefinition : IProducerDefinition
    {
        readonly IProducerLevel[] _levels;

        public FakeProducerDefinition(string id, IReadOnlyList<IRecipeDefinition> recipes, params IProducerLevel[] levels)
        {
            Id = id;
            Recipes = recipes ?? Array.Empty<IRecipeDefinition>();
            _levels = levels != null && levels.Length > 0
                ? levels
                : new IProducerLevel[] { new FakeProducerLevel(3, 1) };
        }

        public string Id { get; }
        public string DisplayNameKey => "producer." + Id;
        public IReadOnlyList<IRecipeDefinition> Recipes { get; }
        public int MaxLevel => _levels.Length;

        public IProducerLevel GetLevel(int level)
        {
            var index = level < 1 ? 0 : (level > _levels.Length ? _levels.Length - 1 : level - 1);
            return _levels[index];
        }
    }

    internal sealed class FakeStorage : IStorageDefinition
    {
        readonly int[] _capacities;

        public FakeStorage(params int[] capacities)
        {
            _capacities = capacities != null && capacities.Length > 0 ? capacities : new[] { 100 };
        }

        public string Id => "storage.test";
        public int MaxLevel => _capacities.Length;

        public int GetCapacity(int level)
        {
            var index = level < 1 ? 0 : (level > _capacities.Length ? _capacities.Length - 1 : level - 1);
            return _capacities[index];
        }
    }

    internal sealed class FakeDatabase : IGameDatabase
    {
        readonly Dictionary<string, IItemDefinition> _items = new Dictionary<string, IItemDefinition>();
        readonly Dictionary<string, IRecipeDefinition> _recipes = new Dictionary<string, IRecipeDefinition>();
        readonly Dictionary<string, IProducerDefinition> _producers = new Dictionary<string, IProducerDefinition>();
        readonly Dictionary<string, IStorageDefinition> _storages = new Dictionary<string, IStorageDefinition>();

        public IStorageDefinition DefaultStorage { get; set; }

        public FakeDatabase WithItem(IItemDefinition item)
        {
            _items[item.Id] = item;
            return this;
        }

        public FakeDatabase WithRecipe(IRecipeDefinition recipe)
        {
            _recipes[recipe.Id] = recipe;
            return this;
        }

        public FakeDatabase WithProducer(IProducerDefinition producer)
        {
            _producers[producer.Id] = producer;
            return this;
        }

        public FakeDatabase WithStorage(IStorageDefinition storage, bool asDefault = true)
        {
            _storages[storage.Id] = storage;
            if (asDefault) DefaultStorage = storage;
            return this;
        }

        public bool TryGetItem(string id, out IItemDefinition item) =>
            _items.TryGetValue(id ?? string.Empty, out item);

        public bool TryGetRecipe(string id, out IRecipeDefinition recipe) =>
            _recipes.TryGetValue(id ?? string.Empty, out recipe);

        public bool TryGetProducer(string id, out IProducerDefinition producer) =>
            _producers.TryGetValue(id ?? string.Empty, out producer);

        public bool TryGetStorage(string id, out IStorageDefinition storage) =>
            _storages.TryGetValue(id ?? string.Empty, out storage);
    }

    /// <summary>Save store backed by a dictionary, so save tests never touch the filesystem.</summary>
    internal sealed class InMemorySaveStore : ISaveStore
    {
        readonly Dictionary<string, string> _files = new Dictionary<string, string>();

        public bool Exists(string key) => _files.ContainsKey(key);

        public bool TryRead(string key, out string contents) => _files.TryGetValue(key, out contents);

        public bool TryWrite(string key, string contents)
        {
            _files[key] = contents;
            return true;
        }

        public bool Delete(string key) => _files.Remove(key);
    }
}
