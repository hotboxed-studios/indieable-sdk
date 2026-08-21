using System;
using UnityEngine;

namespace IndieableSdk.EventBus
{
    /// <summary>
    /// Payload serialization used by the optional Unity event-bus bridge.
    /// Serializable objects use JsonUtility. A string payload must already be
    /// a JSON object. Hosts may supply a serializer for dictionaries or custom
    /// serialization systems.
    /// </summary>
    public static class IndieableEventPayloadJson
    {
        public static Func<object, string> CustomSerializer;

        public static bool TrySerialize(
            object payload,
            out string payloadJson,
            out string reason)
        {
            payloadJson = "{}";
            reason = "";

            try
            {
                if (payload == null) return true;

                var raw = payload as string;
                if (raw != null)
                {
                    payloadJson = raw.Trim();
                }
                else if (CustomSerializer != null)
                {
                    payloadJson = (CustomSerializer(payload) ?? "").Trim();
                }
                else
                {
                    payloadJson = JsonUtility.ToJson(payload);
                }

                if (string.IsNullOrWhiteSpace(payloadJson))
                    payloadJson = "{}";

                if (!payloadJson.StartsWith("{", StringComparison.Ordinal) ||
                    !payloadJson.EndsWith("}", StringComparison.Ordinal))
                {
                    reason = "event payload serializer must return a JSON object";
                    payloadJson = "{}";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                reason = "event payload serialization failed: " + exception.Message;
                payloadJson = "{}";
                return false;
            }
        }
    }
}
