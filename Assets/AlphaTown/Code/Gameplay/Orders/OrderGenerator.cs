using System;
using System.Collections.Generic;
using AlphaTown.Core.Diagnostics;
using AlphaTown.Data.Catalog;
using AlphaTown.Data.Economy;
using AlphaTown.Data.Items;
using AlphaTown.Data.Orders;

namespace AlphaTown.Gameplay.Orders
{
    /// <summary>
    /// Turns a template into a concrete order.
    ///
    /// The candidate pool is derived from unlocked recipes rather than authored per template, so
    /// the player can only ever be asked for something they can actually make. That property
    /// holds automatically as content grows — no template needs revisiting when a recipe ships.
    ///
    /// Randomness is injected so tests are deterministic. Generated orders are persisted, so
    /// nothing depends on being able to reproduce a sequence.
    /// </summary>
    public sealed class OrderGenerator
    {
        readonly IGameDatabase _database;
        readonly Random _random;

        readonly List<string> _candidatePool = new List<string>(32);
        readonly HashSet<string> _candidateSet = new HashSet<string>(StringComparer.Ordinal);
        readonly List<string> _drawPile = new List<string>(32);
        readonly List<IOrderTemplateDefinition> _eligibleTemplates = new List<IOrderTemplateDefinition>(8);

        public OrderGenerator(IGameDatabase database, Random random)
        {
            _database = Guard.NotNull(database, nameof(database));
            _random = Guard.NotNull(random, nameof(random));
        }

        /// <summary>Picks a template of this kind that the player has unlocked. Null if none.</summary>
        public IOrderTemplateDefinition TryPickTemplate(OrderKind kind, int townLevel)
        {
            var templates = _database.OrderTemplates;
            if (templates == null || templates.Count == 0) return null;

            _eligibleTemplates.Clear();
            for (var i = 0; i < templates.Count; i++)
            {
                var template = templates[i];
                if (template == null || template.Kind != kind) continue;
                if (template.UnlockLevel > townLevel) continue;

                _eligibleTemplates.Add(template);
            }

            if (_eligibleTemplates.Count == 0) return null;
            return _eligibleTemplates[_random.Next(_eligibleTemplates.Count)];
        }

        /// <summary>Null when the player cannot yet produce anything worth asking for.</summary>
        public Order TryGenerate(IOrderTemplateDefinition template, int townLevel, long nowTicks, string orderId)
        {
            if (template == null) return null;

            BuildCandidatePool(townLevel);
            if (_candidatePool.Count == 0) return null;

            var typeCount = RandomInclusive(template.MinItemTypes, template.MaxItemTypes);
            if (typeCount > _candidatePool.Count) typeCount = _candidatePool.Count;

            _drawPile.Clear();
            _drawPile.AddRange(_candidatePool);

            var requests = new List<ItemStack>(typeCount);
            var rawCoins = 0;
            var rawXp = 0;

            for (var i = 0; i < typeCount; i++)
            {
                // Draw without replacement so one order never asks for the same item twice.
                var pick = _random.Next(_drawPile.Count);
                var itemId = _drawPile[pick];
                _drawPile.RemoveAt(pick);

                var quantity = RandomInclusive(template.MinQuantityPerItem, template.MaxQuantityPerItem);
                requests.Add(new ItemStack(itemId, quantity));

                if (!_database.TryGetItem(itemId, out var item)) continue;
                rawCoins += item.CoinValue * quantity;
                rawXp += item.XpValue * quantity;
            }

            if (requests.Count == 0) return null;

            var rewards = BuildRewards(template, rawCoins);
            var itemRewards = RollBonusItems(template);
            var xpReward = Scale(rawXp, template.XpMultiplier);

            var expiresAt = template.TimeLimit.Ticks > 0 ? nowTicks + template.TimeLimit.Ticks : 0L;

            return new Order(orderId, template.Id, template.Kind, requests, rewards, itemRewards,
                xpReward, nowTicks, expiresAt);
        }

        /// <summary>Every storable item that some unlocked recipe can produce.</summary>
        void BuildCandidatePool(int townLevel)
        {
            _candidatePool.Clear();
            _candidateSet.Clear();

            var recipes = _database.Recipes;
            if (recipes == null) return;

            for (var r = 0; r < recipes.Count; r++)
            {
                var recipe = recipes[r];
                if (recipe == null || recipe.UnlockLevel > townLevel) continue;

                var outputs = recipe.Outputs;
                for (var o = 0; o < outputs.Count; o++)
                {
                    var itemId = outputs[o].ItemId;
                    if (string.IsNullOrEmpty(itemId) || !_candidateSet.Add(itemId)) continue;

                    // Non-storable goods can never sit in the barn, so they can never be delivered.
                    if (!_database.TryGetItem(itemId, out var item) || !item.IsStorable)
                    {
                        _candidateSet.Remove(itemId);
                        continue;
                    }

                    _candidatePool.Add(itemId);
                }
            }
        }

        List<CurrencyAmount> BuildRewards(IOrderTemplateDefinition template, int rawCoins)
        {
            var rewards = new List<CurrencyAmount>(2);

            var soft = _database.SoftCurrency;
            var coins = Scale(rawCoins, template.CoinMultiplier);
            if (soft != null && coins > 0) rewards.Add(new CurrencyAmount(soft.Id, coins));

            var hard = _database.HardCurrency;
            if (hard != null && template.BonusHardCurrency > 0)
                rewards.Add(new CurrencyAmount(hard.Id, template.BonusHardCurrency));

            return rewards;
        }

        /// <summary>
        /// Rolls the bonus drop once, here, rather than on completion. The player has to be able
        /// to see the land deed on the order before deciding to fill it — a reward that only
        /// materialised at hand-in would be indistinguishable from a slot machine.
        /// </summary>
        IReadOnlyList<ItemStack> RollBonusItems(IOrderTemplateDefinition template)
        {
            var bonus = template.BonusItems;
            if (bonus == null || bonus.Count == 0) return Array.Empty<ItemStack>();

            var chance = template.BonusItemChance;
            if (chance <= 0f) return Array.Empty<ItemStack>();
            if (chance < 1f && _random.NextDouble() >= chance) return Array.Empty<ItemStack>();

            var granted = new List<ItemStack>(bonus.Count);
            for (var i = 0; i < bonus.Count; i++)
            {
                if (bonus[i].IsEmpty) continue;
                granted.Add(bonus[i]);
            }

            return granted;
        }

        int RandomInclusive(int min, int max)
        {
            if (max < min) max = min;
            return _random.Next(min, max + 1);
        }

        /// <summary>
        /// Scales a payout, never rounding a real reward down to nothing — an order that asks for
        /// goods and pays zero coins reads as a bug to a player, whatever the multiplier says.
        /// </summary>
        static int Scale(int raw, float multiplier)
        {
            if (raw <= 0) return 0;

            var scaled = (int)Math.Round(raw * (double)multiplier, MidpointRounding.AwayFromZero);
            return scaled < 1 ? 1 : scaled;
        }
    }
}
