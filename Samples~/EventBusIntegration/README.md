# Indieable Event Bus Integration

This is the normal Unity Package Manager sample for Indieable. Import it from:

```text
Window → Package Manager → Indieable Connect → Samples
→ Event Bus Integration
```

Then open:

```text
Scenes/IndieableEventBusSample.unity
```

The imported scene contains these GameObjects:

```text
Indieable SDK
Sample Gameplay Systems
├── Run System
├── Door
├── Workorder Terminal
├── Tunnel Node
└── Player Lifecycle
```

## The integration pattern

The gameplay components do **not** call Indieable:

```csharp
GlobalEventBus.Publish(
    SampleEventNames.DoorOpened,
    new DoorOpenedEvent
    {
        door_id = "dispatch-door",
        method = "interaction",
        open_count = 1
    });
```

`IndieableEventBusBridge` subscribes once to the bus. Its
`SampleEventRouting.asset` decides which local events are forwarded, what registered
Indieable event key they map to, which purpose gates the event, and whether the event
is sent as test data.

Direct calls such as `Indieable.SendEvent(...)` and `IndieableTelemetry.Send(...)`
remain supported. The bus is an optional decoupling pattern, not a mandatory
abstraction.

## Safe defaults

The sample routing asset uses:

```text
Selection mode: AllowList
Test by default: true
Gameplay telemetry: off until the Player enables it
Diagnostics: off until the Player enables it
Pre-permission replay: none
```

Game systems may continue publishing local events before permission. The local bus
does not serialize, persist, identify, or transmit anything. The bridge drops
optional events until the relevant permission is currently granted; it does not keep
a hidden backlog and send old activity later.

## Configure Preview

1. Create the shared project asset from
   `Tools → Indieable → Open Settings` and paste the game's Preview
   **Public Game Key** there. This is the sample's only configuration source.
2. Keep `https://preview.indieable.com` and `development` for Preview testing.
3. In Indieable, register the exact event schemas you want to test:

```text
door_opened
  door_id      string
  method       string
  open_count   integer

workorder_done
  workorder_id string
  node_id      string
  duration_ms  integer

node_closed
  node_id      string
  outcome      string
  elapsed_ms   integer

player_died
  cause        string
  room_id      string
  run_number   integer

run_completed
  floor        integer
  time_ms      integer
  deaths       integer
  players      integer
```

4. Publish the game's Player Data notice.
5. Enter Play Mode. The SDK initializes before scene `Awake` and opens its
   built-in UI Toolkit consent form after the first scene loads. Declining or
   saving selected purposes records the choice for the current notice version;
   dismissing or failing to load it does not.
6. Fire sample gameplay events and inspect the local activity log and Indieable
   Connect console.

Unknown, disabled, or schema-invalid event keys are still rejected server-side.

Every sample gameplay event carries schema version 1 and the current sample
Run's opaque `run_id`/trace context. Completing the Run creates a new Run ID;
no player, platform, lobby, or device identifier is placed in the payload.

## Selection modes

`IndieableEventRoutingSettings` supports:

- **Disabled** — forward nothing.
- **AllowList** — forward only configured routes marked `Forward`.
- **DenyList** — forward unmatched events, but block matching routes marked off.
- **All** — forward every local event; matching rows may rename/classify events.

`AllowList` is the recommended production default. `All` does not bypass Indieable's
registered-schema, purpose, permission, identity, rate-limit, or trust enforcement.

## Payload serialization

The default Unity bridge uses `JsonUtility`, so payload classes should be marked
`[Serializable]` and use public fields. An already-serialized JSON object string is
also accepted. Games using dictionaries, source generators, Newtonsoft.Json, or
another serializer may assign:

```csharp
IndieableEventPayloadJson.CustomSerializer = payload => MyJson.Serialize(payload);
```

The serializer must return a JSON object.

## Using another bus instance

The package supplies `GlobalEventBus` for a ready-to-use process-wide pattern, while
`GameEventBus` may be instantiated for tests, worlds, or isolated game contexts:

```csharp
var worldBus = new GameEventBus();
bridge.Bind(worldBus);
worldBus.Publish("game.door.opened", payload);
```

Dispose subscriptions when the subscriber's lifetime ends. A failing subscriber is
isolated and reported through `SubscriberException`; it does not stop gameplay or
other listeners.
