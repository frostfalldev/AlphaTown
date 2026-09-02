using System.Collections.Generic;
using AlphaTown.Core.Diagnostics;
using AlphaTown.Core.Events;
using AlphaTown.Core.Timing;
using AlphaTown.Data.Economy;
using AlphaTown.Data.Orders;
using AlphaTown.Data.Progression;
using AlphaTown.Gameplay.Economy;
using AlphaTown.Gameplay.Inventory;
using AlphaTown.Gameplay.Progression;

namespace AlphaTown.Gameplay.Orders
{
    /// <summary>
    /// One delivery board — the helicopter pad, and later the train and the ship.
    ///
    /// This is where the loop closes: goods leave the barn, coins and XP come back, XP raises the
    /// town level, and the higher level widens the pool the next order is drawn from.
    ///
    /// The board is a fixed set of **slots**, each with its own cooldown. A slot that is completed,
    /// expired or discarded goes quiet for a while before offering anything new. That cooldown is
    /// the throttle on the game's main coin faucet: with fields producing for free, a board that
    /// refilled the instant it emptied would be unbounded income.
    ///
    /// Cooldowns are absolute timestamps like everything else, so a board left alone for a week
    /// resolves in a single <see cref="Sync"/> — nothing needs to have been running.
    /// </summary>
    public sealed class OrderBoard
    {
        sealed class Slot
        {
            public Order Order;

            /// <summary>Zero means the slot may produce an order now.</summary>
            public long NextAvailableAtTicks;
        }

        readonly IOrderBoardDefinition _definition;
        readonly IGameClock _clock;
        readonly IEventBus _events;
        readonly ITownProgression _progression;
        readonly IInventory _barn;
        readonly IWallet _wallet;
        readonly OrderGenerator _generator;
        readonly Slot[] _slots;
        readonly List<Order> _active;

        int _nextOrderNumber = 1;

        public OrderBoard(
            IOrderBoardDefinition definition,
            IGameClock clock,
            IEventBus events,
            ITownProgression progression,
            IInventory barn,
            IWallet wallet,
            OrderGenerator generator)
        {
            _definition = Guard.NotNull(definition, nameof(definition));
            _clock = Guard.NotNull(clock, nameof(clock));
            _events = Guard.NotNull(events, nameof(events));
            _progression = Guard.NotNull(progression, nameof(progression));
            _barn = Guard.NotNull(barn, nameof(barn));
            _wallet = Guard.NotNull(wallet, nameof(wallet));
            _generator = Guard.NotNull(generator, nameof(generator));

            var slotCount = definition.SlotCount > 0 ? definition.SlotCount : 1;
            _slots = new Slot[slotCount];
            for (var i = 0; i < slotCount; i++) _slots[i] = new Slot();

            _active = new List<Order>(slotCount);
        }

        public OrderKind Kind => _definition.Kind;

        public int SlotCount => _slots.Length;

        /// <summary>Orders currently on offer. Fewer than <see cref="SlotCount"/> while slots cool.</summary>
        public IReadOnlyList<Order> Orders => _active;

        public int NextOrderNumber => _nextOrderNumber;

        /// <summary>Zero when the slot is occupied or ready to refill.</summary>
        public long SlotAvailableAtTicks(int slotIndex) =>
            slotIndex < 0 || slotIndex >= _slots.Length ? 0 : _slots[slotIndex].NextAvailableAtTicks;

        public bool IsSlotOnCooldown(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Length) return false;

            var slot = _slots[slotIndex];
            return slot.Order == null && slot.NextAvailableAtTicks > _clock.UtcNowTicks;
        }

        /// <summary>Expires what has run out, then refills any slot whose cooldown has passed.</summary>
        public void Sync()
        {
            var now = _clock.UtcNowTicks;

            for (var i = 0; i < _slots.Length; i++)
            {
                var order = _slots[i].Order;
                if (order == null || !order.IsExpired(now)) continue;

                VacateSlot(i, now);
                _events.Publish(new OrderExpiredEvent(order.OrderId, order.TemplateId));
            }

            Refill(now);
        }

        public bool TryGetOrder(string orderId, out Order order)
        {
            var index = IndexOfSlot(orderId);
            order = index >= 0 ? _slots[index].Order : null;
            return index >= 0;
        }

        /// <summary>True when the barn holds everything the order asks for and it has not expired.</summary>
        public bool CanComplete(string orderId)
        {
            var index = IndexOfSlot(orderId);
            if (index < 0) return false;

            var order = _slots[index].Order;
            return !order.IsExpired(_clock.UtcNowTicks) && _barn.ContainsAll(order.Requests);
        }

        /// <summary>
        /// Delivers the order: consumes the goods, pays out, and puts the slot on cooldown.
        ///
        /// Atomic — the goods only leave the barn if all of them are there, so a partial delivery
        /// can never take payment-in-kind and give nothing back.
        /// </summary>
        public bool TryComplete(string orderId)
        {
            var index = IndexOfSlot(orderId);
            if (index < 0) return false;

            var now = _clock.UtcNowTicks;
            var order = _slots[index].Order;
            if (order.IsExpired(now)) return false;
            if (!_barn.TryRemoveAll(order.Requests)) return false;

            _wallet.GrantAll(order.CurrencyRewards, CurrencySource.OrderReward, order.OrderId);
            GrantItemRewards(order);
            var levelsGained = _progression.GrantXp(order.XpReward, XpSource.OrderReward, order.OrderId);

            VacateSlot(index, now);
            _events.Publish(new OrderCompletedEvent(
                order.OrderId, order.TemplateId, order.XpReward, levelsGained));

            // Sync after paying out: a level gained on this order widens the pool other slots
            // draw from, even though this slot is now cooling.
            Sync();
            return true;
        }

        /// <summary>
        /// Drops an order. The slot cools like any other, which is what stops rerolling from being
        /// a free way to fish for a better payout.
        ///
        /// TODO: a paid reroll charging <see cref="CurrencySink.OrderReroll"/> should skip the
        /// cooldown — that is what the player would be buying.
        /// </summary>
        public bool TryDiscard(string orderId)
        {
            var index = IndexOfSlot(orderId);
            if (index < 0) return false;

            VacateSlot(index, _clock.UtcNowTicks);
            Sync();
            return true;
        }

        /// <summary>Restores from save, including which slots are still cooling.</summary>
        public void RestoreState(IReadOnlyList<Order> orders, IReadOnlyList<int> slotIndices,
                                 IReadOnlyList<long> slotCooldowns, int nextOrderNumber)
        {
            _active.Clear();
            for (var i = 0; i < _slots.Length; i++)
            {
                _slots[i].Order = null;
                _slots[i].NextAvailableAtTicks =
                    slotCooldowns != null && i < slotCooldowns.Count ? slotCooldowns[i] : 0;
            }

            _nextOrderNumber = nextOrderNumber > 0 ? nextOrderNumber : 1;

            if (orders == null) return;

            for (var i = 0; i < orders.Count; i++)
            {
                var order = orders[i];
                if (order == null) continue;

                var index = slotIndices != null && i < slotIndices.Count ? slotIndices[i] : i;
                if (index < 0 || index >= _slots.Length || _slots[index].Order != null)
                {
                    // A slot count change between builds. Drop the order rather than the board.
                    Log.Warn("Orders", "Order '" + order.OrderId + "' has no slot to return to.");
                    continue;
                }

                _slots[index].Order = order;
                _slots[index].NextAvailableAtTicks = 0;
                _active.Add(order);
            }
        }

        /// <summary>
        /// Pays the order's item rewards into the barn. Land deeds are non-storable so they always
        /// fit; a storable bonus is clipped by barn space like any other delivery, and Add already
        /// publishes the overflow.
        /// </summary>
        void GrantItemRewards(Order order)
        {
            var rewards = order.ItemRewards;
            for (var i = 0; i < rewards.Count; i++)
            {
                if (rewards[i].IsEmpty) continue;
                _barn.Add(rewards[i].ItemId, rewards[i].Count);
            }
        }

        /// <summary>Empties the slot and starts its cooldown.</summary>
        void VacateSlot(int index, long nowTicks)
        {
            var slot = _slots[index];
            if (slot.Order != null) _active.Remove(slot.Order);
            slot.Order = null;

            var cooldown = _definition.CooldownForSlot(index);
            if (cooldown.Ticks <= 0)
            {
                slot.NextAvailableAtTicks = 0;
                return;
            }

            slot.NextAvailableAtTicks = nowTicks + cooldown.Ticks;
            _events.Publish(new OrderSlotCooldownStartedEvent(index, slot.NextAvailableAtTicks));
        }

        void Refill(long nowTicks)
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (slot.Order != null) continue;
                if (nowTicks < slot.NextAvailableAtTicks) continue;

                var template = _generator.TryPickTemplate(Kind, _progression.TownLevel);
                if (template == null) return;

                var order = _generator.TryGenerate(
                    template, _progression.TownLevel, nowTicks, "order_" + _nextOrderNumber);

                // Nothing the player can produce yet. Try again after the next unlock.
                if (order == null) return;

                _nextOrderNumber++;
                slot.Order = order;
                slot.NextAvailableAtTicks = 0;
                _active.Add(order);

                _events.Publish(new OrderGeneratedEvent(order.OrderId, order.TemplateId, order.ExpiresAtTicks));
            }
        }

        int IndexOfSlot(string orderId)
        {
            if (string.IsNullOrEmpty(orderId)) return -1;

            for (var i = 0; i < _slots.Length; i++)
            {
                var order = _slots[i].Order;
                if (order != null && string.Equals(order.OrderId, orderId, System.StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        /// <summary>Slot index an order sits in, or -1. Used when capturing a save.</summary>
        public int SlotIndexOf(string orderId) => IndexOfSlot(orderId);
    }
}
