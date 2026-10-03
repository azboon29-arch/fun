using System;
using System.Collections.Generic;

namespace Game.Core.Events
{
    /// <summary>
    /// A simple synchronous, type-keyed publish/subscribe bus used for communication between systems.
    /// Handlers run in subscription order on the publishing thread.
    /// </summary>
    public sealed class EventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();

        /// <summary>
        /// Subscribes <paramref name="handler"/> to events of type <typeparamref name="T"/>.
        /// Dispose the returned token to unsubscribe.
        /// </summary>
        public IDisposable Subscribe<T>(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            if (!_handlers.TryGetValue(typeof(T), out var list))
            {
                list = new List<Delegate>();
                _handlers[typeof(T)] = list;
            }
            list.Add(handler);
            return new Subscription(() => Unsubscribe(handler));
        }

        /// <summary>Removes a handler previously added with <see cref="Subscribe{T}"/>. Unknown handlers are ignored.</summary>
        public void Unsubscribe<T>(Action<T> handler)
        {
            if (handler != null && _handlers.TryGetValue(typeof(T), out var list))
            {
                list.Remove(handler);
            }
        }

        /// <summary>
        /// Delivers <paramref name="evt"/> to every handler subscribed to <typeparamref name="T"/>.
        /// Handlers added or removed while publishing take effect from the next publish.
        /// </summary>
        public void Publish<T>(T evt)
        {
            if (!_handlers.TryGetValue(typeof(T), out var list) || list.Count == 0) return;

            var snapshot = list.ToArray();
            foreach (var handler in snapshot)
            {
                ((Action<T>)handler)(evt);
            }
        }

        /// <summary>Returns how many handlers are subscribed to <typeparamref name="T"/>.</summary>
        public int SubscriberCount<T>()
        {
            return _handlers.TryGetValue(typeof(T), out var list) ? list.Count : 0;
        }

        private sealed class Subscription : IDisposable
        {
            private Action _dispose;

            public Subscription(Action dispose) { _dispose = dispose; }

            public void Dispose()
            {
                _dispose?.Invoke();
                _dispose = null;
            }
        }
    }
}
