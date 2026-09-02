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
    /// Expiry is timestamp-driven like everything else, so a board left alone for a week resolves
    /// in a single <see cref="Sync"/> rather than needing anything to have been running.
    /// </summary>
    public sealed class OrderBoard
    {
        readonly IGameClock _clock;
        readonly IEventBus _events;
        readonly ITownProgression _progression;
        readonly IInventory _barn;
        readonly IWallet _wallet;
        readonly OrderGenerator _generator;
        readonly List<Order> _orders = new List<Order>(8);

        int _nextOrderNumber = 1;

        public OrderBoard(
            OrderKind kind,
            int capacity,
            IGameClock clock,
            IEventBus events,
            ITownProgression progression,
            IInventory barn,
            IWallet wallet,
            OrderGenerator generator)
        {
            Kind = kind;
            Capacity = Guard.Positive(capacity, nameof(capacity));
            _clock = Guard.NotNull(clock, nameof(clock));
            _events = Guard.NotNull(events, nameof(events));
            _progression = Guard.NotNull(progression, nameof(progression));
            _barn = Guard.NotNull(barn, nameof(barn));
            _wallet = Guard.NotNull(wallet, nameof(wallet));
            _generator = Guard.NotNull(generator, nameof(generator));
        }

        public OrderKind Kind { get; }

        public int Capacity { get; }

        public IReadOnlyList<Order> Orders => _orders;

        /// <summary>Expires what has run out, then refills empty slots. Safe to call at any time.</summary>
        public void Sync()
        {
            var now = _clock.UtcNowTicks;

            for (var i = _orders.Count - 1; i >= 0; i--)
            {
                if (!_orders[i].IsExpired(now)) continue;

                var expired = _orders[i];
                _orders.RemoveAt(i);
                _events.Publish(new OrderExpiredEvent(expired.OrderId, expired.TemplateId));
            }

            Refill(now);
        }

        public bool TryGetOrder(string orderId, out Order order)
        {
            var index = IndexOf(orderId);
            order = index >= 0 ? _orders[index] : null;
            return index >= 0;
        }

        /// <summary>True when the barn holds everything the order asks for and it has not expired.</summary>
        public bool CanComplete(string orderId)
        {
            var index = IndexOf(orderId);
            if (index < 0) return false;

            var order = _orders[index];
            return !order.IsExpired(_clock.UtcNowTicks) && _barn.ContainsAll(order.Requests);
        }

        /// <summary>
        /// Delivers the order: consumes the goods, pays out, and refills the slot.
        ///
        /// Atomic — the goods only leave the barn if all of them are there, so a partial delivery
        /// can never take payment-in-kind and give nothing back.
        /// </summary>
        public bool TryComplete(string orderId)
        {
            var index = IndexOf(orderId);
            if (index < 0) return false;

            var order = _orders[index];
            if (order.IsExpired(_clock.UtcNowTicks)) return false;
            if (!_barn.TryRemoveAll(order.Requests)) return false;

            _wallet.GrantAll(order.CurrencyRewards, CurrencySource.OrderReward, order.OrderId);
            var levelsGained = _progression.GrantXp(order.XpReward, XpSource.OrderReward, order.OrderId);

            _orders.RemoveAt(index);
            _events.Publish(new OrderCompletedEvent(
                order.OrderId, order.TemplateId, order.XpReward, levelsGained));

            // Refill after paying out: a level gained on this order widens the pool immediately.
            Sync();
            return true;
        }

        /// <summary>
        /// Drops an order and replaces it. TODO: charge hard currency through
        /// <see cref="CurrencySink.OrderReroll"/> once the reroll price is designed.
        /// </summary>
        public bool TryDiscard(string orderId)
        {
            var index = IndexOf(orderId);
            if (index < 0) return false;

            _orders.RemoveAt(index);
            Sync();
            return true;
        }

        /// <summary>Restores from save. Does not re-pay anything.</summary>
        public void RestoreState(IReadOnlyList<Order> orders, int nextOrderNumber)
        {
            _orders.Clear();
            if (orders != null)
            {
                for (var i = 0; i < orders.Count; i++)
                {
                    if (orders[i] == null) continue;
                    _orders.Add(orders[i]);
                }
            }

            _nextOrderNumber = nextOrderNumber > 0 ? nextOrderNumber : 1;
        }

        public int NextOrderNumber => _nextOrderNumber;

        void Refill(long nowTicks)
        {
            // Bounded independently of Capacity: a template that cannot generate stops the loop,
            // so this only guards against a future generator that returns non-null but adds nothing.
            var attempts = 0;
            while (_orders.Count < Capacity && attempts++ < Capacity + 4)
            {
                var template = _generator.TryPickTemplate(Kind, _progression.TownLevel);
                if (template == null) return;

                var order = _generator.TryGenerate(
                    template, _progression.TownLevel, nowTicks, "order_" + _nextOrderNumber);

                // Nothing the player can produce yet. Try again after the next unlock.
                if (order == null) return;

                _nextOrderNumber++;
                _orders.Add(order);
                _events.Publish(new OrderGeneratedEvent(order.OrderId, order.TemplateId, order.ExpiresAtTicks));
            }
        }

        int IndexOf(string orderId)
        {
            if (string.IsNullOrEmpty(orderId)) return -1;

            for (var i = 0; i < _orders.Count; i++)
            {
                if (string.Equals(_orders[i].OrderId, orderId, System.StringComparison.Ordinal)) return i;
            }

            return -1;
        }
    }
}
