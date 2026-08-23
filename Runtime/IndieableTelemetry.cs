using System;

namespace IndieableSdk
{
    /// <summary>
    /// Explicit gameplay-telemetry surface. Event purpose, identity, permission,
    /// schema version, and trust are still resolved and enforced by Indieable.
    /// Failures are non-fatal to the host game.
    /// </summary>
    public static class IndieableTelemetry
    {
        public static void Send(
            string eventKey,
            string payloadJson = "{}",
            bool test = false,
            Action onSuccess = null,
            Action<IndieableError> onError = null,
            string idempotencyKey = null)
        {
            Indieable.SendEvent(
                eventKey,
                payloadJson,
                test,
                onSuccess,
                onError,
                idempotencyKey);
        }

        public static void Send(
            string eventKey,
            string payloadJson,
            IndieableEventOptions options,
            Action onSuccess = null,
            Action<IndieableError> onError = null)
        {
            Indieable.SendEvent(
                eventKey,
                payloadJson,
                options,
                onSuccess,
                onError);
        }
    }
}
