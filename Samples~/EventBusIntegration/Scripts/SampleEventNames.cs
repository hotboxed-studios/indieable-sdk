namespace IndieableSdk.Samples.EventBus
{
    /// <summary>
    /// Local game event names. These do not have to match Indieable event keys;
    /// the routing asset maps them at the integration boundary.
    /// </summary>
    public static class SampleEventNames
    {
        public const string DoorOpened = "game.door.opened";
        public const string WorkorderDone = "game.workorder.done";
        public const string NodeClosed = "game.node.closed";
        public const string PlayerDied = "game.player.died";
        public const string RunCompleted = "game.run.completed";
    }
}
