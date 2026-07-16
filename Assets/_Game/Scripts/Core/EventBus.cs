using System;
using System.Collections.Generic;

namespace SummaRace.Core
{
    /// <summary>
    /// Tiny static pub/sub. Features never call each other directly —
    /// they raise events here and read shared state from GameManager (TDD §8).
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, List<Delegate>> _subscribers = new();

        public static void Subscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (!_subscribers.TryGetValue(type, out var list))
            {
                list = new List<Delegate>();
                _subscribers[type] = list;
            }
            list.Add(handler);
        }

        public static void Unsubscribe<T>(Action<T> handler)
        {
            if (_subscribers.TryGetValue(typeof(T), out var list))
                list.Remove(handler);
        }

        public static void Raise<T>(T evt)
        {
            if (!_subscribers.TryGetValue(typeof(T), out var list)) return;
            // Copy so handlers can safely unsubscribe during dispatch.
            foreach (var handler in list.ToArray())
                ((Action<T>)handler)?.Invoke(evt);
        }

        /// <summary>Editor/test helper — clears all subscriptions.</summary>
        public static void Clear() => _subscribers.Clear();
    }
}
