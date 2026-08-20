using System;
using IndieableSdk.Events;
using UnityEngine;

namespace IndieableSdk.Samples.EventBus
{
    /// <summary>
    /// Example workorder system publishing a normal game-domain event.
    /// </summary>
    public sealed class SampleWorkorderTerminal : MonoBehaviour
    {
        [SerializeField] private string workorderId = "repair-pipe";
        [SerializeField] private string nodeId = "fulfillment-dispatch";
        [SerializeField] private SampleRunTracker runTracker;

        private DateTime _startedAtUtc;

        private void Awake()
        {
            _startedAtUtc = DateTime.UtcNow;
        }

        public void CompleteWorkorder()
        {
            var elapsed = DateTime.UtcNow - _startedAtUtc;
            GlobalEventBus.Publish(
                SampleEventNames.WorkorderDone,
                new WorkorderDoneEvent
                {
                    workorder_id = workorderId,
                    node_id = nodeId,
                    duration_ms = Math.Max(0, (int)elapsed.TotalMilliseconds)
                },
                runTracker != null
                    ? runTracker.NewContext("workorder-done")
                    : null);

            _startedAtUtc = DateTime.UtcNow;
        }
    }
}
