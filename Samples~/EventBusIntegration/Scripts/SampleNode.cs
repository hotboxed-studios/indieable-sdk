using System;
using IndieableSdk.Events;
using UnityEngine;

namespace IndieableSdk.Samples.EventBus
{
    /// <summary>
    /// Example procedural-node lifecycle publishing through the shared bus.
    /// </summary>
    public sealed class SampleNode : MonoBehaviour
    {
        [SerializeField] private string nodeId = "fulfillment-node-001";
        [SerializeField] private SampleRunTracker runTracker;

        private DateTime _openedAtUtc;

        private void Awake()
        {
            _openedAtUtc = DateTime.UtcNow;
        }

        public void CloseNode()
        {
            var elapsed = DateTime.UtcNow - _openedAtUtc;
            GlobalEventBus.Publish(
                SampleEventNames.NodeClosed,
                new NodeClosedEvent
                {
                    node_id = nodeId,
                    outcome = "completed",
                    elapsed_ms = Math.Max(0, (int)elapsed.TotalMilliseconds)
                },
                runTracker != null
                    ? runTracker.NewContext("node-closed")
                    : null);

            _openedAtUtc = DateTime.UtcNow;
        }
    }
}
