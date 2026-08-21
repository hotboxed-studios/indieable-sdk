using System;
using System.Collections.Generic;
using System.Threading;

namespace IndieableSdk.Events
{
    /// <summary>
    /// Engine-agnostic synchronous event bus. Publishing is local-only: it does not
    /// create an Indieable session, serialize a payload, write to disk, or perform
    /// network traffic.
    /// </summary>
    public interface IGameEventBus
    {
        IDisposable Subscribe(string eventName, Action<GameEventEnvelope> subscriber);
        IDisposable SubscribeAll(Action<GameEventEnvelope> subscriber);
        IDisposable Subscribe<TPayload>(string eventName, Action<TPayload> subscriber);
        IDisposable Subscribe<TPayload>(Action<TPayload> subscriber);
        GameEventEnvelope Publish(
            string eventName,
            object payload = null,
            IndieableEventContext context = null);
        void Clear();
    }

    public sealed class GameEventBus : IGameEventBus
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, List<Subscription>> _named =
            new Dictionary<string, List<Subscription>>(StringComparer.Ordinal);
        private readonly List<Subscription> _all = new List<Subscription>();
        private long _sequence;

        /// <summary>
        /// Subscriber failures are isolated so one analytics/integration listener
        /// cannot break gameplay or prevent other listeners from receiving the event.
        /// </summary>
        public event Action<GameEventSubscriberException> SubscriberException;

        public IDisposable Subscribe(string eventName, Action<GameEventEnvelope> subscriber)
        {
            if (subscriber == null) throw new ArgumentNullException("subscriber");
            var normalized = NormalizeName(eventName);
            var subscription = new Subscription(this, normalized, subscriber);
            lock (_gate)
            {
                List<Subscription> values;
                if (!_named.TryGetValue(normalized, out values))
                {
                    values = new List<Subscription>();
                    _named.Add(normalized, values);
                }
                values.Add(subscription);
            }
            return subscription;
        }

        public IDisposable SubscribeAll(Action<GameEventEnvelope> subscriber)
        {
            if (subscriber == null) throw new ArgumentNullException("subscriber");
            var subscription = new Subscription(this, null, subscriber);
            lock (_gate) _all.Add(subscription);
            return subscription;
        }

        public IDisposable Subscribe<TPayload>(string eventName, Action<TPayload> subscriber)
        {
            if (subscriber == null) throw new ArgumentNullException("subscriber");
            return Subscribe(eventName, delegate(GameEventEnvelope envelope)
            {
                if (envelope.Payload is TPayload)
                    subscriber((TPayload)envelope.Payload);
            });
        }

        public IDisposable Subscribe<TPayload>(Action<TPayload> subscriber)
        {
            if (subscriber == null) throw new ArgumentNullException("subscriber");
            return SubscribeAll(delegate(GameEventEnvelope envelope)
            {
                if (envelope.Payload is TPayload)
                    subscriber((TPayload)envelope.Payload);
            });
        }

        public GameEventEnvelope Publish(
            string eventName,
            object payload = null,
            IndieableEventContext context = null)
        {
            var normalized = NormalizeName(eventName);
            var publishedAt = DateTime.UtcNow;
            var effectiveContext = context != null
                ? context.Clone()
                : new IndieableEventContext { OccurredAtUtc = publishedAt };
            if (effectiveContext.OccurredAtUtc.Kind == DateTimeKind.Local)
                effectiveContext.OccurredAtUtc = effectiveContext.OccurredAtUtc.ToUniversalTime();
            else if (effectiveContext.OccurredAtUtc.Kind == DateTimeKind.Unspecified)
                effectiveContext.OccurredAtUtc = DateTime.SpecifyKind(
                    effectiveContext.OccurredAtUtc, DateTimeKind.Utc);

            var envelope = new GameEventEnvelope(
                Interlocked.Increment(ref _sequence),
                normalized,
                payload,
                publishedAt,
                effectiveContext);

            Subscription[] subscribers;
            lock (_gate)
            {
                List<Subscription> named;
                var count = _all.Count +
                    (_named.TryGetValue(normalized, out named) ? named.Count : 0);
                subscribers = new Subscription[count];
                var index = 0;
                for (var i = 0; i < _all.Count; i++) subscribers[index++] = _all[i];
                if (named != null)
                    for (var i = 0; i < named.Count; i++) subscribers[index++] = named[i];
            }

            for (var i = 0; i < subscribers.Length; i++)
            {
                var subscription = subscribers[i];
                if (subscription.IsDisposed) continue;
                try
                {
                    subscription.Handler(envelope);
                }
                catch (Exception exception)
                {
                    NotifySubscriberException(envelope, subscription.Handler, exception);
                }
            }

            return envelope;
        }

        public void Clear()
        {
            Subscription[] subscriptions;
            lock (_gate)
            {
                var count = _all.Count;
                foreach (var pair in _named) count += pair.Value.Count;
                subscriptions = new Subscription[count];
                var index = 0;
                for (var i = 0; i < _all.Count; i++) subscriptions[index++] = _all[i];
                foreach (var pair in _named)
                    for (var i = 0; i < pair.Value.Count; i++)
                        subscriptions[index++] = pair.Value[i];
                _all.Clear();
                _named.Clear();
            }
            for (var i = 0; i < subscriptions.Length; i++)
                subscriptions[i].MarkDisposed();
        }

        private static string NormalizeName(string eventName)
        {
            var normalized = (eventName ?? "").Trim();
            if (normalized.Length == 0)
                throw new ArgumentException("A game event name is required.", "eventName");
            if (normalized.Length > 160)
                throw new ArgumentException("Game event names cannot exceed 160 characters.", "eventName");
            return normalized;
        }

        private void NotifySubscriberException(
            GameEventEnvelope envelope,
            Delegate subscriber,
            Exception exception)
        {
            var callback = SubscriberException;
            if (callback == null) return;
            try
            {
                callback(new GameEventSubscriberException(envelope, subscriber, exception));
            }
            catch
            {
                // Error reporting must not change event publication behavior.
            }
        }

        private void Unsubscribe(Subscription subscription)
        {
            lock (_gate)
            {
                if (subscription.EventName == null)
                {
                    _all.Remove(subscription);
                    return;
                }

                List<Subscription> values;
                if (!_named.TryGetValue(subscription.EventName, out values)) return;
                values.Remove(subscription);
                if (values.Count == 0) _named.Remove(subscription.EventName);
            }
        }

        private sealed class Subscription : IDisposable
        {
            private GameEventBus _owner;
            private int _disposed;

            internal string EventName { get; private set; }
            internal Action<GameEventEnvelope> Handler { get; private set; }
            internal bool IsDisposed { get { return _disposed != 0; } }

            internal Subscription(
                GameEventBus owner,
                string eventName,
                Action<GameEventEnvelope> handler)
            {
                _owner = owner;
                EventName = eventName;
                Handler = handler;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
                var owner = Interlocked.Exchange(ref _owner, null);
                if (owner != null) owner.Unsubscribe(this);
            }

            internal void MarkDisposed()
            {
                Interlocked.Exchange(ref _disposed, 1);
                Interlocked.Exchange(ref _owner, null);
            }
        }
    }

    /// <summary>
    /// Default process-wide bus for games that want a global publish/subscribe
    /// pattern. Tests and advanced hosts may instead instantiate GameEventBus.
    /// </summary>
    public static class GlobalEventBus
    {
        private static readonly GameEventBus Instance = new GameEventBus();

        public static IGameEventBus Default { get { return Instance; } }

        public static event Action<GameEventSubscriberException> SubscriberException
        {
            add { Instance.SubscriberException += value; }
            remove { Instance.SubscriberException -= value; }
        }

        public static IDisposable Subscribe(
            string eventName,
            Action<GameEventEnvelope> subscriber)
        {
            return Instance.Subscribe(eventName, subscriber);
        }

        public static IDisposable SubscribeAll(Action<GameEventEnvelope> subscriber)
        {
            return Instance.SubscribeAll(subscriber);
        }

        public static IDisposable Subscribe<TPayload>(
            string eventName,
            Action<TPayload> subscriber)
        {
            return Instance.Subscribe(eventName, subscriber);
        }

        public static IDisposable Subscribe<TPayload>(Action<TPayload> subscriber)
        {
            return Instance.Subscribe(subscriber);
        }

        public static GameEventEnvelope Publish(
            string eventName,
            object payload = null,
            IndieableEventContext context = null)
        {
            return Instance.Publish(eventName, payload, context);
        }

        public static void Clear()
        {
            Instance.Clear();
        }
    }
}
