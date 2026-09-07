using System;

namespace AlphaTown.Core.Events
{
    /// <summary>
    /// Decouples the layers: Gameplay publishes, UI and Services listen, and neither needs a
    /// reference to the other. Events are structs so publishing does not allocate.
    /// </summary>
    public interface IEventBus
    {
        /// <summary>Dispose the returned handle to unsubscribe. Always unsubscribe on teardown.</summary>
        IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;

        void Publish<TEvent>(TEvent message) where TEvent : struct;
    }
}
