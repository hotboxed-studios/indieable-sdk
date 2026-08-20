using System;

namespace IndieableSdk.Samples.EventBus
{
    [Serializable]
    public sealed class DoorOpenedEvent
    {
        public string door_id = "";
        public string method = "";
        public int open_count;
    }

    [Serializable]
    public sealed class WorkorderDoneEvent
    {
        public string workorder_id = "";
        public string node_id = "";
        public int duration_ms;
    }

    [Serializable]
    public sealed class NodeClosedEvent
    {
        public string node_id = "";
        public string outcome = "";
        public int elapsed_ms;
    }

    [Serializable]
    public sealed class PlayerDiedEvent
    {
        public string cause = "";
        public string room_id = "";
        public int run_number;
    }

    [Serializable]
    public sealed class RunCompletedEvent
    {
        public int floor;
        public int time_ms;
        public int deaths;
        public int players;
    }
}
