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

        /// <summary>
        /// Dispatches to every subscriber. One handler throwing must not stop the others, and
        /// must not propagate back into the raiser.
        ///
        /// Both halves of that matter here. Raise sites are almost always gameplay code — often
        /// inside a coroutine — so an exception escaping this method KILLS THE CALLER'S
        /// COROUTINE at the raise point. `ArrangeController.VerifyRoutine` sets `_busy = true`
        /// at the top and clears it at the end; a throwing `ArrangeVerified` subscriber would
        /// leave VERIFY, UNDO, every slot and every piece dead with no exit, on the one screen
        /// a story cannot be completed without. And the listener most likely to throw is
        /// `SessionLogService`, whose entire job is recording study data it must never be able
        /// to stop a learner to collect.
        ///
        /// Swallowing is deliberately noisy: the failure is logged with the event type so it is
        /// findable, rather than a silently missing log row.
        /// </summary>
        public static void Raise<T>(T evt)
        {
            if (!_subscribers.TryGetValue(typeof(T), out var list)) return;
            // Copy so handlers can safely unsubscribe during dispatch.
            foreach (var handler in list.ToArray())
            {
                try
                {
                    ((Action<T>)handler)?.Invoke(evt);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError(
                        "EventBus: a subscriber to " + typeof(T).Name + " threw. The remaining " +
                        "subscribers still ran and the raiser was not interrupted, but something " +
                        "did not happen — most likely a study-data row was not recorded. " + e);
                }
            }
        }

        /// <summary>Editor/test helper — clears all subscriptions.</summary>
        public static void Clear() => _subscribers.Clear();
    }
}
