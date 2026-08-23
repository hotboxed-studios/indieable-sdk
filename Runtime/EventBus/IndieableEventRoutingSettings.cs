using System;
using System.Text.RegularExpressions;
using IndieableSdk.Events;
using UnityEngine;

namespace IndieableSdk.EventBus
{
    public enum IndieableEventSelectionMode
    {
        Disabled = 0,
        AllowList = 1,
        DenyList = 2,
        All = 3
    }

    public enum IndieableEventPurpose
    {
        GameplayTelemetry = 0,
        Diagnostics = 1,
        CommunityChallengeOperation = 2
    }

    [Serializable]
    public sealed class IndieableEventRoute
    {
        [Tooltip("Exact event name published on the local game event bus.")]
        public string SourceEventName = "";

        [Tooltip("Registered Indieable event key. Leave empty to reuse Source Event Name.")]
        public string IndieableEventKey = "";

        [Tooltip("AllowList/DenyList route decision. All mode intentionally ignores this value.")]
        public bool Forward = true;

        [Tooltip("Client-side permission gate. The Indieable backend remains authoritative.")]
        public IndieableEventPurpose Purpose = IndieableEventPurpose.GameplayTelemetry;

        [Tooltip("Use this route's Test value instead of the routing asset default.")]
        public bool OverrideTest = false;

        public bool Test = true;

    }

    public sealed class IndieableResolvedEventRoute
    {
        public string SourceEventName { get; internal set; }
        public string IndieableEventKey { get; internal set; }
        public IndieableEventPurpose Purpose { get; internal set; }
        public bool Test { get; internal set; }
        public string IdempotencyKey { get; internal set; }
        public int? SchemaVersion { get; internal set; }
        public DateTime? OccurredAtUtc { get; internal set; }
        public string TraceType { get; internal set; }
        public string TraceId { get; internal set; }
        public string RunId { get; internal set; }
    }

    [CreateAssetMenu(
        fileName = "IndieableEventRouting",
        menuName = "Indieable/Event Bus Routing",
        order = 100)]
    public sealed class IndieableEventRoutingSettings : ScriptableObject
    {
        private static readonly Regex IndieableEventKeyPattern =
            new Regex("^[a-z][a-z0-9_.-]{0,79}$", RegexOptions.Compiled);

        [Tooltip("AllowList is the recommended production default. All can generate requests for every local bus event.")]
        public IndieableEventSelectionMode SelectionMode = IndieableEventSelectionMode.AllowList;

        [Tooltip("Sample and development integrations should normally leave this enabled.")]
        public bool TestByDefault = true;

        [Tooltip("Write local bridge decisions to the Unity Console. No payload values are logged.")]
        public bool VerboseLogging = true;

        [Tooltip("Bounded main-thread handoff capacity for events published from worker threads.")]
        [Range(1, 4096)]
        public int MaxPendingEvents = 256;

        [Tooltip("Maximum number of queued events forwarded during one Unity frame.")]
        [Range(1, 128)]
        public int MaxEventsPerFrame = 16;

        public IndieableEventRoute[] Routes = new IndieableEventRoute[0];

        public bool TryResolve(
            GameEventEnvelope envelope,
            out IndieableResolvedEventRoute resolved,
            out string reason)
        {
            resolved = null;
            reason = "";

            if (envelope == null)
            {
                reason = "missing event envelope";
                return false;
            }

            if (SelectionMode == IndieableEventSelectionMode.Disabled)
            {
                reason = "routing is disabled";
                return false;
            }

            var configured = FindRoute(envelope.Name);
            if (SelectionMode == IndieableEventSelectionMode.AllowList &&
                (configured == null || !configured.Forward))
            {
                reason = "event is not enabled by the allowlist";
                return false;
            }

            if (SelectionMode == IndieableEventSelectionMode.DenyList &&
                configured != null && !configured.Forward)
            {
                reason = "event is blocked by the denylist";
                return false;
            }

            // All means exactly all local events. A matching row may rename or
            // classify the event, but its Forward flag is deliberately ignored.
            var eventKey = configured != null &&
                           !string.IsNullOrWhiteSpace(configured.IndieableEventKey)
                ? configured.IndieableEventKey.Trim()
                : envelope.Name.Trim();

            if (!IndieableEventKeyPattern.IsMatch(eventKey))
            {
                reason = "resolved Indieable event key is invalid";
                return false;
            }

            var context = envelope.Context;
            var routeTest = configured != null && configured.OverrideTest
                ? configured.Test
                : TestByDefault;
            var test = context != null && context.Test.HasValue
                ? context.Test.Value
                : routeTest;

            var idempotencyKey = context != null
                ? NormalizeIdempotencyKey(context.IdempotencyKey)
                : "";
            if (string.IsNullOrEmpty(idempotencyKey))
            {
                idempotencyKey = string.Format(
                    "bus-{0}-{1}-{2}",
                    SanitizeKeyPart(eventKey),
                    envelope.Sequence,
                    Guid.NewGuid().ToString("N"));
            }

            resolved = new IndieableResolvedEventRoute
            {
                SourceEventName = envelope.Name,
                IndieableEventKey = eventKey,
                Purpose = configured != null
                    ? configured.Purpose
                    : IndieableEventPurpose.GameplayTelemetry,
                Test = test,
                IdempotencyKey = idempotencyKey,
                SchemaVersion = context != null
                    ? context.SchemaVersion
                    : null,
                OccurredAtUtc = context != null
                    ? (DateTime?)context.OccurredAtUtc
                    : envelope.PublishedAtUtc,
                TraceType = context != null
                    ? context.TraceType ?? ""
                    : "",
                TraceId = context != null
                    ? context.TraceId ?? ""
                    : "",
                RunId = context != null
                    ? context.RunId ?? ""
                    : ""
            };
            return true;
        }

        private IndieableEventRoute FindRoute(string eventName)
        {
            var values = Routes ?? new IndieableEventRoute[0];
            for (var i = 0; i < values.Length; i++)
            {
                var route = values[i];
                if (route == null) continue;
                if (string.Equals(
                    (route.SourceEventName ?? "").Trim(),
                    eventName,
                    StringComparison.Ordinal))
                    return route;
            }
            return null;
        }

        private static string NormalizeIdempotencyKey(string value)
        {
            var normalized = (value ?? "").Trim();
            if (normalized.Length < 8 || normalized.Length > 128) return "";
            for (var i = 0; i < normalized.Length; i++)
            {
                var c = normalized[i];
                if (!(char.IsLetterOrDigit(c) || c == '.' || c == '_' ||
                      c == ':' || c == '-'))
                    return "";
            }
            return normalized;
        }

        private static string SanitizeKeyPart(string value)
        {
            var source = value ?? "event";
            var chars = new char[Math.Min(source.Length, 48)];
            var count = 0;
            for (var i = 0; i < source.Length && count < chars.Length; i++)
            {
                var c = source[i];
                chars[count++] = char.IsLetterOrDigit(c) || c == '.' ||
                                 c == '_' || c == '-' ? c : '-';
            }
            return count == 0 ? "event" : new string(chars, 0, count);
        }
    }
}
