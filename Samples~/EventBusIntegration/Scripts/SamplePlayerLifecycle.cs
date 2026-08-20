using IndieableSdk.Events;
using UnityEngine;

namespace IndieableSdk.Samples.EventBus
{
    /// <summary>
    /// Example player lifecycle. It updates its own run state and publishes one
    /// local event. It does not know whether Indieable is installed or permitted.
    /// </summary>
    public sealed class SamplePlayerLifecycle : MonoBehaviour
    {
        [SerializeField] private string roomId = "truck-lane";
        [SerializeField] private SampleRunTracker runTracker;

        public void Die()
        {
            if (runTracker != null) runTracker.RecordDeath();

            GlobalEventBus.Publish(
                SampleEventNames.PlayerDied,
                new PlayerDiedEvent
                {
                    cause = "sample-semi-truck",
                    room_id = roomId,
                    run_number = runTracker != null
                        ? runTracker.RunNumber
                        : 0
                },
                runTracker != null
                    ? runTracker.NewContext("player-died")
                    : null);
        }
    }
}
