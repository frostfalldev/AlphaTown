using System;
using System.Collections.Generic;
using AlphaTown.Core.Diagnostics;

namespace AlphaTown.Core.Services
{
    /// <summary>
    /// Holds the long-lived services built at startup.
    ///
    /// Use it in the composition root only. Systems take what they need through their
    /// constructors — a class that resolves its own dependencies from here cannot be tested
    /// without standing up the whole game.
    /// </summary>
    public sealed class ServiceRegistry
    {
        readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public void Register<TService>(TService service) where TService : class
        {
            Guard.NotNull(service, nameof(service));
            _services[typeof(TService)] = service;
        }

        public TService Resolve<TService>() where TService : class
        {
            if (_services.TryGetValue(typeof(TService), out var service))
                return (TService)service;

            throw new InvalidOperationException(
                "No service registered for " + typeof(TService).Name +
                ". Register it in the composition root before anything resolves it.");
        }

        public bool TryResolve<TService>(out TService service) where TService : class
        {
            if (_services.TryGetValue(typeof(TService), out var found))
            {
                service = (TService)found;
                return true;
            }

            service = null;
            return false;
        }

        public void Clear() => _services.Clear();
    }
}
