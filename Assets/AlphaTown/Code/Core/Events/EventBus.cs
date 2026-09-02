using System;
using System.Collections.Generic;
using AlphaTown.Core.Diagnostics;

namespace AlphaTown.Core.Events
{
    /// <summary>
    /// Synchronous typed event bus.
    ///
    /// Three properties it guarantees, all of which matter once a few hundred producers are
    /// publishing every frame:
    ///  - Publishing to a type with no subscribers costs one dictionary lookup and no allocation.
    ///  - Subscribing or unsubscribing from inside a handler is safe; dispatch runs over a snapshot.
    ///  - A throwing handler is logged and does not stop the remaining handlers.
    /// </summary>
    public sealed class EventBus : IEventBus
    {
        interface IChannel
        {
            void Clear();
        }

        sealed class Channel<TEvent> : IChannel where TEvent : struct
        {
            readonly List<Action<TEvent>> _handlers = new List<Action<TEvent>>(4);
            Action<TEvent>[] _dispatchBuffer = Array.Empty<Action<TEvent>>();
            int _depth;

            public void Add(Action<TEvent> handler) => _handlers.Add(handler);

            public void Remove(Action<TEvent> handler) => _handlers.Remove(handler);

            public void Clear() => _handlers.Clear();

            public void Publish(TEvent message, Action<Exception> onHandlerFailed)
            {
                var count = _handlers.Count;
                if (count == 0) return;

                // The common case reuses one buffer. A re-entrant publish of the same event type
                // takes a fresh one rather than trampling the outer dispatch.
                Action<TEvent>[] buffer;
                if (_depth == 0)
                {
                    if (_dispatchBuffer.Length < count) _dispatchBuffer = new Action<TEvent>[count];
                    buffer = _dispatchBuffer;
                }
                else
                {
                    buffer = new Action<TEvent>[count];
                }

                _handlers.CopyTo(0, buffer, 0, count);
                _depth++;
                try
                {
                    for (var i = 0; i < count; i++)
                    {
                        try
                        {
                            buffer[i].Invoke(message);
                        }
                        catch (Exception exception)
                        {
                            onHandlerFailed?.Invoke(exception);
                        }
                    }
                }
                finally
                {
                    _depth--;
                    // Do not hold handler references alive in the reusable buffer.
                    Array.Clear(buffer, 0, count);
                }
            }
        }

        sealed class Subscription<TEvent> : IDisposable where TEvent : struct
        {
            Channel<TEvent> _channel;
            Action<TEvent> _handler;

            public Subscription(Channel<TEvent> channel, Action<TEvent> handler)
            {
                _channel = channel;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_channel == null) return;
                _channel.Remove(_handler);
                _channel = null;
                _handler = null;
            }
        }

        static readonly Action<Exception> HandlerFailed = OnHandlerFailed;

        readonly Dictionary<Type, IChannel> _channels = new Dictionary<Type, IChannel>();

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            Guard.NotNull(handler, nameof(handler));

            var channel = GetOrCreateChannel<TEvent>();
            channel.Add(handler);
            return new Subscription<TEvent>(channel, handler);
        }

        public void Publish<TEvent>(TEvent message) where TEvent : struct
        {
            if (!_channels.TryGetValue(typeof(TEvent), out var channel)) return;
            ((Channel<TEvent>)channel).Publish(message, HandlerFailed);
        }

        /// <summary>Drops every subscription. For teardown between play sessions and tests.</summary>
        public void Clear()
        {
            foreach (var channel in _channels.Values) channel.Clear();
            _channels.Clear();
        }

        Channel<TEvent> GetOrCreateChannel<TEvent>() where TEvent : struct
        {
            if (_channels.TryGetValue(typeof(TEvent), out var existing))
                return (Channel<TEvent>)existing;

            var created = new Channel<TEvent>();
            _channels[typeof(TEvent)] = created;
            return created;
        }

        static void OnHandlerFailed(Exception exception) => Log.Exception(exception);
    }
}
