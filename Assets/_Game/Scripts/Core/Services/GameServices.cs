using System;
using System.Collections.Generic;

namespace Game.Core.Services
{
    /// <summary>
    /// The single service root of the game. Systems are registered once at boot and looked up by type.
    /// This is the only global access point; avoid adding other singletons.
    /// </summary>
    public sealed class GameServices
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        /// <summary>The active root, set by the bootstrap. Null before boot and in unit tests that do not set it.</summary>
        public static GameServices Current { get; private set; }

        /// <summary>Makes <paramref name="root"/> the active root (pass null to clear it).</summary>
        public static void SetCurrent(GameServices root) { Current = root; }

        /// <summary>Registers <paramref name="service"/> under type <typeparamref name="T"/>. Throws if one is already registered.</summary>
        public void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (_services.ContainsKey(typeof(T)))
            {
                throw new InvalidOperationException($"A service of type {typeof(T).Name} is already registered.");
            }
            _services[typeof(T)] = service;
        }

        /// <summary>Returns the service registered as <typeparamref name="T"/>, or throws if missing.</summary>
        public T Get<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var service)) return (T)service;
            throw new InvalidOperationException($"No service of type {typeof(T).Name} is registered.");
        }

        /// <summary>Looks up the service registered as <typeparamref name="T"/> without throwing.</summary>
        public bool TryGet<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var found))
            {
                service = (T)found;
                return true;
            }
            service = null;
            return false;
        }

        /// <summary>Removes every registered service.</summary>
        public void Clear() { _services.Clear(); }
    }
}
