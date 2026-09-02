using System;
using System.Collections.Generic;
using AlphaTown.Core.Diagnostics;
using AlphaTown.Core.Events;
using AlphaTown.Core.Timing;
using AlphaTown.Data.Catalog;
using AlphaTown.Data.Items;
using AlphaTown.Data.Production;
using AlphaTown.Gameplay.Inventory;

namespace AlphaTown.Gameplay.Production
{
    /// <summary>
    /// A single production building instance: a queue of orders, a number of parallel slots, and
    /// a tray of finished goods waiting to be collected.
    ///
    /// Inputs are consumed when an order is queued, not when it starts — the player has committed
    /// the goods, and it stops a full queue from being a free reservation on the barn.
    ///
    /// Outputs stay in <see cref="Ready"/> until collected, so production keeps running while the
    /// player is away and the tray is what they come back to.
    /// </summary>
    public sealed class Producer
    {
        const int SyncSafetyLimit = 256;

        readonly IProducerDefinition _definition;
        readonly IGameDatabase _database;
        readonly IGameClock _clock;
        readonly IEventBus _events;
        readonly List<ProductionOrder> _orders = new List<ProductionOrder>(4);
        readonly List<ItemStack> _ready = new List<ItemStack>(4);

        int _level = 1;

        public Producer(
            string instanceId,
            IProducerDefinition definition,
            IGameDatabase database,
            IGameClock clock,
            IEventBus events)
        {
            InstanceId = Guard.NotNullOrEmpty(instanceId, nameof(instanceId));
            _definition = Guard.NotNull(definition, nameof(definition));
            _database = Guard.NotNull(database, nameof(database));
            _clock = Guard.NotNull(clock, nameof(clock));
            _events = Guard.NotNull(events, nameof(events));
        }

        /// <summary>Unique per placed building. A town can hold several bakeries.</summary>
        public string InstanceId { get; }

        public string DefinitionId => _definition.Id;
        public IProducerDefinition Definition => _definition;
        public int Level => _level;
        public IReadOnlyList<ProductionOrder> Orders => _orders;
        public IReadOnlyList<ItemStack> Ready => _ready;

        public int QueueCapacity => _definition.GetLevel(_level).QueueCapacity;
        public bool HasFreeQueueSlot => _orders.Count < QueueCapacity;

        /// <summary>Queues an order, paying its inputs. False if the queue is full or goods are short.</summary>
        public bool TryEnqueue(string recipeId, IInventory inventory)
        {
            Guard.NotNull(inventory, nameof(inventory));

            if (string.IsNullOrEmpty(recipeId)) return false;
            if (!HasFreeQueueSlot) return false;
            if (!CanProduce(recipeId)) return false;
            if (!_database.TryGetRecipe(recipeId, out var recipe)) return false;
            if (!inventory.TryRemoveAll(recipe.Inputs)) return false;

            _orders.Add(new ProductionOrder
            {
                RecipeId = recipeId,
                EnqueuedAtTicks = _clock.UtcNowTicks
            });

            _events.Publish(new ProductionOrderQueuedEvent(InstanceId, recipeId, _orders.Count - 1));
            Sync();
            return true;
        }

        /// <summary>
        /// Brings the building up to date with the clock: starts what should have started and
        /// completes what should have completed.
        ///
        /// Cost is proportional to the number of orders that finished, not to elapsed time, so
        /// returning after two weeks costs the same as returning after two minutes.
        /// </summary>
        public void Sync()
        {
            var now = _clock.UtcNowTicks;
            StartPending(now);

            for (var iteration = 0; iteration < SyncSafetyLimit; iteration++)
            {
                var index = IndexOfEarliestDue(now);
                if (index < 0) return;

                var completedAt = _orders[index].CompletesAtTicks;
                CompleteOrder(index);

                // The freed slot picks up the next order at the moment the previous one finished,
                // not "now" — otherwise a chain of offline orders would all start on resume.
                StartPending(completedAt);
            }

            Log.Error("Production", "Sync on '" + InstanceId + "' hit its iteration limit.");
        }

        /// <summary>Moves finished goods into the barn. Partial when space runs out; the rest stays.</summary>
        public int CollectReady(IInventory inventory)
        {
            Guard.NotNull(inventory, nameof(inventory));

            var collected = 0;
            for (var i = _ready.Count - 1; i >= 0; i--)
            {
                var stack = _ready[i];
                var added = inventory.Add(stack.ItemId, stack.Count);
                collected += added;

                if (added >= stack.Count) _ready.RemoveAt(i);
                else if (added > 0) _ready[i] = stack.WithCount(stack.Count - added);
            }

            if (collected > 0) _events.Publish(new ProductionCollectedEvent(InstanceId, collected));
            return collected;
        }

        /// <summary>
        /// The player-facing speed-up. Shortens one order rather than moving the clock, so the
        /// effect is saved, survives a restart, and cannot leak into the rest of the town.
        /// </summary>
        public bool TrySpeedUp(int orderIndex, TimeSpan amount)
        {
            if (orderIndex < 0 || orderIndex >= _orders.Count) return false;
            if (amount <= TimeSpan.Zero) return false;

            var order = _orders[orderIndex];
            if (!order.IsStarted) return false;

            var reduced = order.CompletesAtTicks - amount.Ticks;
            order.CompletesAtTicks = reduced < order.StartedAtTicks ? order.StartedAtTicks : reduced;
            _orders[orderIndex] = order;

            Sync();
            return true;
        }

        /// <summary>Instant finish, as sold for hard currency. TODO: price it from remaining time.</summary>
        public bool TryFinishNow(int orderIndex)
        {
            if (orderIndex < 0 || orderIndex >= _orders.Count) return false;

            var order = _orders[orderIndex];
            if (!order.IsStarted) return false;

            order.CompletesAtTicks = _clock.UtcNowTicks;
            _orders[orderIndex] = order;

            Sync();
            return true;
        }

        /// <summary>Cancels an order and refunds its inputs, as far as the barn has room.</summary>
        public bool TryCancel(int orderIndex, IInventory refundTo)
        {
            if (orderIndex < 0 || orderIndex >= _orders.Count) return false;

            var order = _orders[orderIndex];
            _orders.RemoveAt(orderIndex);

            if (refundTo != null && _database.TryGetRecipe(order.RecipeId, out var recipe))
            {
                var inputs = recipe.Inputs;
                for (var i = 0; i < inputs.Count; i++)
                {
                    refundTo.Add(inputs[i].ItemId, inputs[i].Count);
                }
            }

            Sync();
            return true;
        }

        /// <summary>
        /// Upgrade. Only affects orders that start afterwards — an order already running keeps the
        /// duration it was quoted, which is the honest reading of a timer the player has watched.
        /// </summary>
        public void SetLevel(int level)
        {
            var max = _definition.MaxLevel;
            _level = level < 1 ? 1 : (level > max ? max : level);
        }

        /// <summary>Restores state from save. Call <see cref="Sync"/> afterwards to catch up.</summary>
        public void RestoreState(int level, IReadOnlyList<ProductionOrder> orders, IReadOnlyList<ItemStack> ready)
        {
            SetLevel(level);

            _orders.Clear();
            if (orders != null)
            {
                for (var i = 0; i < orders.Count; i++)
                {
                    if (string.IsNullOrEmpty(orders[i].RecipeId)) continue;
                    _orders.Add(orders[i]);
                }
            }

            _ready.Clear();
            if (ready == null) return;

            for (var i = 0; i < ready.Count; i++)
            {
                if (!ready[i].IsEmpty) AddReady(ready[i]);
            }
        }

        public bool CanProduce(string recipeId)
        {
            var recipes = _definition.Recipes;
            for (var i = 0; i < recipes.Count; i++)
            {
                if (string.Equals(recipes[i].Id, recipeId, StringComparison.Ordinal)) return true;
            }

            return false;
        }

        void StartPending(long boundaryTicks)
        {
            var level = _definition.GetLevel(_level);

            var running = 0;
            for (var i = 0; i < _orders.Count; i++)
            {
                if (_orders[i].IsStarted) running++;
            }

            for (var i = 0; i < _orders.Count && running < level.ParallelSlots; i++)
            {
                var order = _orders[i];
                if (order.IsStarted) continue;

                var startAt = boundaryTicks > order.EnqueuedAtTicks ? boundaryTicks : order.EnqueuedAtTicks;
                order.StartedAtTicks = startAt;
                order.CompletesAtTicks = startAt + DurationTicks(order.RecipeId, level);
                _orders[i] = order;
                running++;

                _events.Publish(new ProductionOrderStartedEvent(InstanceId, order.RecipeId, order.CompletesAtTicks));
            }
        }

        int IndexOfEarliestDue(long nowTicks)
        {
            var best = -1;
            var bestTicks = long.MaxValue;

            for (var i = 0; i < _orders.Count; i++)
            {
                var order = _orders[i];
                if (!order.IsStarted || order.CompletesAtTicks > nowTicks) continue;
                if (order.CompletesAtTicks >= bestTicks) continue;

                best = i;
                bestTicks = order.CompletesAtTicks;
            }

            return best;
        }

        void CompleteOrder(int index)
        {
            var order = _orders[index];
            _orders.RemoveAt(index);

            if (_database.TryGetRecipe(order.RecipeId, out var recipe))
            {
                var outputs = recipe.Outputs;
                for (var i = 0; i < outputs.Count; i++) AddReady(outputs[i]);
            }
            else
            {
                Log.Error("Production",
                    "Completed an order for unknown recipe '" + order.RecipeId + "'; its output was lost.");
            }

            _events.Publish(new ProductionOrderCompletedEvent(InstanceId, order.RecipeId));
        }

        void AddReady(ItemStack stack)
        {
            for (var i = 0; i < _ready.Count; i++)
            {
                if (!string.Equals(_ready[i].ItemId, stack.ItemId, StringComparison.Ordinal)) continue;

                _ready[i] = _ready[i].WithCount(_ready[i].Count + stack.Count);
                return;
            }

            _ready.Add(stack);
        }

        long DurationTicks(string recipeId, IProducerLevel level)
        {
            if (!_database.TryGetRecipe(recipeId, out var recipe)) return 0L;

            var multiplier = level.SpeedMultiplier <= 0f ? 1f : level.SpeedMultiplier;
            var seconds = recipe.Duration.TotalSeconds / multiplier;
            return (long)(seconds * TimeSpan.TicksPerSecond);
        }
    }
}
