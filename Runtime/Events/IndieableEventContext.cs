using System;

namespace IndieableSdk.Events
{
    /// <summary>
    /// Optional correlation and delivery context carried beside a game event.
    /// It is local metadata until an Indieable bridge explicitly forwards the event.
    /// </summary>
    public sealed class IndieableEventContext
    {
        public DateTime OccurredAtUtc = DateTime.UtcNow;
        public string IdempotencyKey = "";

        /// <summary>
        /// Null uses the bridge/routing default. True or false explicitly overrides it.
        /// </summary>
        public bool? Test = null;

        public IndieableEventContext Clone()
        {
            return new IndieableEventContext
            {
                OccurredAtUtc = OccurredAtUtc,
                IdempotencyKey = IdempotencyKey ?? "",
                Test = Test
            };
        }
    }

    /// <summary>
    /// One event published through the global bus. The payload remains an ordinary
    /// game object; the bus does not serialize, persist, or transmit it.
    /// </summary>
    public sealed class GameEventEnvelope
    {
        public long Sequence { get; private set; }
        public string Name { get; private set; }
        public object Payload { get; private set; }
        public Type PayloadType { get; private set; }
        public DateTime PublishedAtUtc { get; private set; }
        public IndieableEventContext Context { get; private set; }

        internal GameEventEnvelope(
            long sequence,
            string name,
            object payload,
            DateTime publishedAtUtc,
            IndieableEventContext context)
        {
            Sequence = sequence;
            Name = name;
            Payload = payload;
            PayloadType = payload != null ? payload.GetType() : typeof(object);
            PublishedAtUtc = publishedAtUtc;
            Context = context ?? new IndieableEventContext { OccurredAtUtc = publishedAtUtc };
        }

        public override string ToString()
        {
            return string.Format("#{0} {1} ({2})", Sequence, Name, PayloadType.Name);
        }
    }

    public sealed class GameEventSubscriberException
    {
        public GameEventEnvelope Envelope { get; private set; }
        public Delegate Subscriber { get; private set; }
        public Exception Exception { get; private set; }

        internal GameEventSubscriberException(
            GameEventEnvelope envelope,
            Delegate subscriber,
            Exception exception)
        {
            Envelope = envelope;
            Subscriber = subscriber;
            Exception = exception;
        }
    }
}
