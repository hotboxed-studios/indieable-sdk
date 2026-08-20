using IndieableSdk.Events;
using UnityEngine;

namespace IndieableSdk.Samples.EventBus
{
    /// <summary>
    /// Example gameplay system. Notice that this file has no reference to
    /// IndieableTelemetry or Indieable.SendEvent.
    /// </summary>
    public sealed class SampleDoor : MonoBehaviour
    {
        [SerializeField] private string doorId = "dispatch-door";
        [SerializeField] private SampleRunTracker runTracker;

        private int _openCount;

        public void Open()
        {
            _openCount++;
            var context = runTracker != null
                ? runTracker.NewContext("door-opened")
                : null;

            GlobalEventBus.Publish(
                SampleEventNames.DoorOpened,
                new DoorOpenedEvent
                {
                    door_id = doorId,
                    method = "sample-ui",
                    open_count = _openCount
                },
                context);
        }
    }
}
