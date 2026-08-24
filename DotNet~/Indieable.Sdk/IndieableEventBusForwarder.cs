using System.Text.RegularExpressions;
using System.Threading.Channels;
using IndieableSdk.Events;

namespace IndieableSdk
{
    public enum IndieableEventForwardingMode
    {
        Disabled = 0,
        AllowList = 1,
        DenyList = 2,
        All = 3
    }

    public enum IndieableEventForwardingPurpose
    {
        GameplayTelemetry = 0,
        Diagnostics = 1,
        CommunityChallengeOperation = 2
    }

    public sealed class IndieableEventForwardingRoute
    {
        public string SourceEventName { get; set; } = "";
        public string IndieableEventKey { get; set; } = "";
        public bool Forward { get; set; } = true;
        public IndieableEventForwardingPurpose Purpose { get; set; } =
            IndieableEventForwardingPurpose.GameplayTelemetry;
        public bool? Test { get; set; }
        public int? SchemaVersion { get; set; }
    }

    public sealed class IndieableEventBusForwarderOptions
    {
        public IndieableEventForwardingMode Mode { get; set; } =
            IndieableEventForwardingMode.AllowList;
        public bool TestByDefault { get; set; } = true;
        public int Capacity { get; set; } = 256;
        public IndieableEventForwardingRoute[] Routes { get; set; } =
            Array.Empty<IndieableEventForwardingRoute>();
    }

    /// <summary>
    /// Optional engine-agnostic adapter between IGameEventBus and IndieableClient.
    /// Optional events are dropped before enqueueing unless the current purpose is
    /// granted. Nothing published before permission is replayed after a later grant.
    /// </summary>
    public sealed class IndieableEventBusForwarder : IAsyncDisposable
    {
        private static readonly Regex EventKeyPattern =
            new("^[a-z][a-z0-9_.-]{0,79}$", RegexOptions.Compiled);

        private readonly IndieableClient _client;
        private readonly IGameEventBus _eventBus;
        private readonly IndieableEventBusForwarderOptions _options;
        private readonly Channel<GameEventEnvelope> _channel;
        private readonly CancellationTokenSource _stop = new();
        private readonly IDisposable _subscription;
        private readonly Task _worker;

        private volatile bool _telemetryGranted;
        private volatile bool _diagnosticsGranted;

        public event Action<GameEventEnvelope, string>? EventDropped;
        public event Action<GameEventEnvelope, IndieableEventReceipt>? EventForwarded;
        public event Action<GameEventEnvelope, Exception>? EventFailed;

        public IndieableEventBusForwarder(
            IndieableClient client,
            IndieableEventBusForwarderOptions options,
            IGameEventBus? eventBus = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _options = options ??
                throw new ArgumentNullException(nameof(options));
            _eventBus = eventBus ?? GlobalEventBus.Default;

            _channel = Channel.CreateBounded<GameEventEnvelope>(
                new BoundedChannelOptions(
                    Math.Max(1, options.Capacity))
                {
                    SingleReader = true,
                    SingleWriter = false,
                    FullMode = BoundedChannelFullMode.Wait,
                    AllowSynchronousContinuations = false
                });

            _subscription = _eventBus.SubscribeAll(OnEvent);
            _worker = Task.Run(RunAsync);
        }

        public void ApplyPrivacyPreferences(
            IndieablePrivacyPreferences? preferences)
        {
            _telemetryGranted = preferences?.IsGranted(
                IndieableClient.GameplayTelemetryPurpose) == true;
            _diagnosticsGranted = preferences?.IsGranted(
                IndieableClient.DiagnosticsPurpose) == true;
        }

        private void OnEvent(GameEventEnvelope envelope)
        {
            if (!TryResolve(
                    envelope,
                    out _,
                    out var reason))
            {
                NotifyDropped(envelope, reason);
                return;
            }

            if (!_channel.Writer.TryWrite(envelope))
                NotifyDropped(envelope, "forwarding queue is full");
        }

        private async Task RunAsync()
        {
            try
            {
                await foreach (var envelope in _channel.Reader
                    .ReadAllAsync(_stop.Token))
                {
                    if (!TryResolve(
                            envelope,
                            out var route,
                            out var reason))
                    {
                        NotifyDropped(envelope, reason);
                        continue;
                    }

                    try
                    {
                        var context = envelope.Context;
                        var receipt =
                            await _client.SendEventAsync(
                                    route.EventKey,
                                    envelope.Payload,
                                    new IndieableEventOptions
                                    {
                                        Test = context?.Test ??
                                            route.Test,
                                        IdempotencyKey =
                                            string.IsNullOrWhiteSpace(
                                                context?.IdempotencyKey)
                                                ? "bus-" +
                                                  Guid.NewGuid()
                                                      .ToString("N")
                                                : context!
                                                    .IdempotencyKey,
                                        SchemaVersion =
                                            route.SchemaVersion,
                                        OccurredAtUtc =
                                            context == null
                                                ? envelope.PublishedAtUtc
                                                : new DateTimeOffset(
                                                    context.OccurredAtUtc)
                                    },
                                    _stop.Token)
                                .ConfigureAwait(false);

                        var callback = EventForwarded;
                        if (callback != null)
                        {
                            try { callback(envelope, receipt); }
                            catch { }
                        }
                    }
                    catch (OperationCanceledException)
                        when (_stop.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception exception)
                    {
                        var callback = EventFailed;
                        if (callback != null)
                        {
                            try { callback(envelope, exception); }
                            catch { }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
                when (_stop.IsCancellationRequested)
            {
            }
        }

        private bool TryResolve(
            GameEventEnvelope envelope,
            out ResolvedRoute route,
            out string reason)
        {
            route = default;
            reason = "";

            if (_options.Mode ==
                IndieableEventForwardingMode.Disabled)
            {
                reason = "routing is disabled";
                return false;
            }

            if (!_client.IsConnected)
            {
                reason = "Indieable has no active session";
                return false;
            }

            var configured = FindRoute(envelope.Name);
            if (_options.Mode ==
                    IndieableEventForwardingMode.AllowList &&
                (configured == null || !configured.Forward))
            {
                reason = "event is not enabled by the allowlist";
                return false;
            }

            if (_options.Mode ==
                    IndieableEventForwardingMode.DenyList &&
                configured != null &&
                !configured.Forward)
            {
                reason = "event is blocked by the denylist";
                return false;
            }

            var eventKey =
                !string.IsNullOrWhiteSpace(
                    configured?.IndieableEventKey)
                    ? configured!.IndieableEventKey.Trim()
                    : envelope.Name.Trim();

            if (!EventKeyPattern.IsMatch(eventKey))
            {
                reason = "resolved Indieable event key is invalid";
                return false;
            }

            var purpose = configured?.Purpose ??
                IndieableEventForwardingPurpose
                    .GameplayTelemetry;
            if (purpose ==
                    IndieableEventForwardingPurpose
                        .GameplayTelemetry &&
                !_telemetryGranted)
            {
                reason =
                    "gameplay telemetry permission is not granted";
                return false;
            }

            if (purpose ==
                    IndieableEventForwardingPurpose.Diagnostics &&
                !_diagnosticsGranted)
            {
                reason =
                    "diagnostics permission is not granted";
                return false;
            }

            route = new ResolvedRoute(
                eventKey,
                purpose,
                envelope.Context?.Test ??
                    configured?.Test ??
                    _options.TestByDefault,
                configured?.SchemaVersion);
            return true;
        }

        private IndieableEventForwardingRoute? FindRoute(
            string eventName)
        {
            return (_options.Routes ??
                    Array.Empty<IndieableEventForwardingRoute>())
                .FirstOrDefault(
                    candidate =>
                        candidate != null &&
                        string.Equals(
                            (candidate.SourceEventName ?? "").Trim(),
                            eventName,
                            StringComparison.Ordinal));
        }

        private void NotifyDropped(
            GameEventEnvelope envelope,
            string reason)
        {
            var callback = EventDropped;
            if (callback == null) return;
            try { callback(envelope, reason); }
            catch { }
        }

        public async ValueTask DisposeAsync()
        {
            _subscription.Dispose();
            _channel.Writer.TryComplete();
            _stop.Cancel();

            try
            {
                await _worker.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }

            _stop.Dispose();
        }

        private readonly struct ResolvedRoute
        {
            public string EventKey { get; }
            public IndieableEventForwardingPurpose Purpose { get; }
            public bool Test { get; }
            public int? SchemaVersion { get; }

            public ResolvedRoute(
                string eventKey,
                IndieableEventForwardingPurpose purpose,
                bool test,
                int? schemaVersion)
            {
                EventKey = eventKey;
                Purpose = purpose;
                Test = test;
                SchemaVersion = schemaVersion;
            }
        }
    }
}
