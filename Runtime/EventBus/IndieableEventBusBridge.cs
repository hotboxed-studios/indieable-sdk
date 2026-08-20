using System;
using System.Collections.Generic;
using System.Threading;
using IndieableSdk.Events;
using UnityEngine;

namespace IndieableSdk.EventBus
{
    /// <summary>
    /// Optional adapter between a local game event bus and Indieable.
    ///
    /// Game systems publish ordinary local events and never need to reference the
    /// Indieable HTTP API. This component is the policy boundary that selects,
    /// permission-gates, serializes, and forwards configured events.
    ///
    /// Events rejected before a valid purpose is available are dropped. They are
    /// never buffered for later consent or replayed after a Player changes a choice.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IndieableEventBusBridge : MonoBehaviour
    {
        [SerializeField]
        private IndieableEventRoutingSettings routingSettings;

        [SerializeField]
        private bool refreshPrivacyPreferencesOnEnable = true;

        private readonly object _pendingGate = new object();
        private readonly Queue<PendingEvent> _pending = new Queue<PendingEvent>();

        private IGameEventBus _eventBus;
        private IDisposable _subscription;
        private int _mainThreadId;
        private bool _telemetryGranted;
        private bool _diagnosticsGranted;
        private bool _enabled;

        public IndieableEventRoutingSettings RoutingSettings
        {
            get { return routingSettings; }
            set { routingSettings = value; }
        }

        public bool GameplayTelemetryGranted { get { return _telemetryGranted; } }
        public bool DiagnosticsGranted { get { return _diagnosticsGranted; } }

        public event Action<GameEventEnvelope, string> EventDropped;
        public event Action<GameEventEnvelope, string> EventForwarded;
        public event Action<GameEventEnvelope, IndieableError> EventFailed;

        private void Awake()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            _eventBus = GlobalEventBus.Default;
        }

        private void OnEnable()
        {
            _enabled = true;
            Subscribe();
            if (refreshPrivacyPreferencesOnEnable && Indieable.IsConnected)
                RefreshPrivacyPreferences();
        }

        private void OnDisable()
        {
            _enabled = false;
            if (_subscription != null)
            {
                _subscription.Dispose();
                _subscription = null;
            }
            ClearPending("bridge disabled");
        }

        private void Update()
        {
            var max = routingSettings != null
                ? Math.Max(1, routingSettings.MaxEventsPerFrame)
                : 1;

            for (var i = 0; i < max; i++)
            {
                PendingEvent pending;
                lock (_pendingGate)
                {
                    if (_pending.Count == 0) break;
                    pending = _pending.Dequeue();
                }

                if (!string.IsNullOrEmpty(pending.DropReason))
                    Drop(pending.Envelope, pending.DropReason);
                else
                    ForwardOnMainThread(pending.Envelope);
            }
        }

        /// <summary>
        /// Bind another bus instance. The default is GlobalEventBus.Default.
        /// This lets tests or advanced hosts keep isolated buses while using the
        /// same bridge and routing policy.
        /// </summary>
        public void Bind(IGameEventBus eventBus)
        {
            if (eventBus == null) throw new ArgumentNullException("eventBus");
            if (ReferenceEquals(_eventBus, eventBus)) return;

            if (_subscription != null)
            {
                _subscription.Dispose();
                _subscription = null;
            }

            _eventBus = eventBus;
            if (_enabled) Subscribe();
        }

        public void UseGlobalEventBus()
        {
            Bind(GlobalEventBus.Default);
        }

        public void ApplyPrivacyPreferences(IndieablePrivacyPreferences preferences)
        {
            _telemetryGranted = preferences != null &&
                preferences.IsGranted(Indieable.GameplayTelemetryPurpose);
            _diagnosticsGranted = preferences != null &&
                preferences.IsGranted(Indieable.DiagnosticsPurpose);
        }

        public void SetPurposePermission(
            IndieableEventPurpose purpose,
            bool granted)
        {
            if (purpose == IndieableEventPurpose.GameplayTelemetry)
                _telemetryGranted = granted;
            else if (purpose == IndieableEventPurpose.Diagnostics)
                _diagnosticsGranted = granted;
        }

        public void RefreshPrivacyPreferences()
        {
            if (!Indieable.IsConnected)
            {
                _telemetryGranted = false;
                _diagnosticsGranted = false;
                return;
            }

            Indieable.GetPrivacyPreferences(
                delegate(IndieablePrivacyPreferences preferences)
                {
                    ApplyPrivacyPreferences(preferences);
                },
                delegate(IndieableError error)
                {
                    _telemetryGranted = false;
                    _diagnosticsGranted = false;
                    if (routingSettings != null && routingSettings.VerboseLogging)
                        Debug.LogWarning(
                            "[Indieable Event Bus] Privacy preferences were not loaded: " +
                            error.Message);
                });
        }

        private void Subscribe()
        {
            if (_subscription != null) _subscription.Dispose();
            if (_eventBus == null) _eventBus = GlobalEventBus.Default;
            _subscription = _eventBus.SubscribeAll(Receive);
        }

        private void Receive(GameEventEnvelope envelope)
        {
            IndieableResolvedEventRoute route;
            string reason;
            var canForward = CanForward(envelope, out route, out reason);
            if (Thread.CurrentThread.ManagedThreadId == _mainThreadId)
            {
                if (!canForward)
                    Drop(envelope, reason);
                else
                    ForwardOnMainThread(envelope);
                return;
            }

            lock (_pendingGate)
            {
                var capacity = routingSettings != null
                    ? Math.Max(1, routingSettings.MaxPendingEvents)
                    : 1;
                if (_pending.Count >= capacity)
                {
                    // Do not invoke Unity APIs or host callbacks from a worker
                    // thread. A full handoff queue is a local best-effort drop.
                    return;
                }
                _pending.Enqueue(new PendingEvent(
                    envelope,
                    canForward ? "" : reason));
            }
        }

        private bool CanForward(
            GameEventEnvelope envelope,
            out IndieableResolvedEventRoute route,
            out string reason)
        {
            route = null;
            reason = "";

            if (!_enabled)
            {
                reason = "bridge is disabled";
                return false;
            }

            if (routingSettings == null)
            {
                reason = "routing settings are missing";
                return false;
            }

            if (!routingSettings.TryResolve(envelope, out route, out reason))
                return false;

            if (!Indieable.IsInitialized)
            {
                reason = "Indieable is not initialized";
                return false;
            }

            if (!Indieable.IsConnected)
            {
                reason = "Indieable has no active Connect session";
                return false;
            }

            if (route.Purpose == IndieableEventPurpose.GameplayTelemetry &&
                !_telemetryGranted)
            {
                reason = "gameplay telemetry permission is not granted";
                return false;
            }

            if (route.Purpose == IndieableEventPurpose.Diagnostics &&
                !_diagnosticsGranted)
            {
                reason = "diagnostics permission is not granted";
                return false;
            }

            // CommunityChallengeOperation is a separate requested-feature purpose.
            // The server still verifies identity, membership, schema, and authority.
            return true;
        }

        private void ForwardOnMainThread(GameEventEnvelope envelope)
        {
            IndieableResolvedEventRoute route;
            string reason;
            if (!CanForward(envelope, out route, out reason))
            {
                Drop(envelope, reason);
                return;
            }

            string payloadJson;
            if (!IndieableEventPayloadJson.TrySerialize(
                envelope.Payload,
                out payloadJson,
                out reason))
            {
                Drop(envelope, reason);
                return;
            }

            IndieableTelemetry.Send(
                route.IndieableEventKey,
                payloadJson,
                route.Test,
                delegate
                {
                    NotifyForwarded(
                        envelope,
                        string.Format(
                            "{0} -> {1} ({2})",
                            envelope.Name,
                            route.IndieableEventKey,
                            route.Test ? "test" : "production"));
                },
                delegate(IndieableError error)
                {
                    NotifyFailed(envelope, error);
                },
                route.IdempotencyKey);
        }

        private void Drop(GameEventEnvelope envelope, string reason)
        {
            if (routingSettings != null && routingSettings.VerboseLogging)
            {
                Debug.Log(
                    "[Indieable Event Bus] Dropped " +
                    SafeEventName(envelope) + ": " + reason + ".");
            }

            var callback = EventDropped;
            if (callback == null) return;
            try { callback(envelope, reason); }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[Indieable Event Bus] EventDropped callback failed: " +
                    exception.Message);
            }
        }

        private void NotifyForwarded(GameEventEnvelope envelope, string message)
        {
            if (routingSettings != null && routingSettings.VerboseLogging)
                Debug.Log("[Indieable Event Bus] Forwarded " + message + ".");

            var callback = EventForwarded;
            if (callback == null) return;
            try { callback(envelope, message); }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[Indieable Event Bus] EventForwarded callback failed: " +
                    exception.Message);
            }
        }

        private void NotifyFailed(GameEventEnvelope envelope, IndieableError error)
        {
            if (routingSettings != null && routingSettings.VerboseLogging)
            {
                Debug.LogWarning(
                    "[Indieable Event Bus] " + SafeEventName(envelope) +
                    " was rejected: " + error.Message);
            }

            var callback = EventFailed;
            if (callback == null) return;
            try { callback(envelope, error); }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[Indieable Event Bus] EventFailed callback failed: " +
                    exception.Message);
            }
        }

        private void ClearPending(string reason)
        {
            PendingEvent[] values;
            lock (_pendingGate)
            {
                values = _pending.ToArray();
                _pending.Clear();
            }
            for (var i = 0; i < values.Length; i++)
                Drop(values[i].Envelope, reason);
        }

        private static string SafeEventName(GameEventEnvelope envelope)
        {
            return envelope == null ? "event" : envelope.Name;
        }

        private sealed class PendingEvent
        {
            internal GameEventEnvelope Envelope { get; private set; }
            internal string DropReason { get; private set; }

            internal PendingEvent(
                GameEventEnvelope envelope,
                string dropReason = "")
            {
                Envelope = envelope;
                DropReason = dropReason ?? "";
            }
        }
    }
}
