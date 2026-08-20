# Indieable Global Event Bus

The event bus is an optional integration pattern shipped in both the Unity UPM
package and the generic C# package.

Its goal is to let game-domain systems publish facts without depending on the
Indieable client:

```text
Door system ───────────────┐
Workorder system ──────────┤
Procedural node system ────┤
Player lifecycle ──────────┼── GlobalEventBus
Run/match system ──────────┘          │
                                      │ one subscriber
                                      ▼
                         IndieableEventBusBridge
                                      │
                         routing + purpose gate
                                      │
                                      ▼
                              Indieable API
```

Direct SDK calls remain available. The bus is not required.

## Local guarantees

`GameEventBus.Publish(...)` is synchronous and local. It does not:

- know the Indieable Public Game Key;
- initialize Indieable;
- establish a session;
- create an Installation or Game Player;
- serialize the payload;
- persist an event;
- perform network traffic;
- infer permission.

This means a game can use the bus as ordinary application architecture even when
the Indieable bridge is absent or disabled.

Subscribers are invoked from a snapshot outside the bus lock. One subscriber
throwing does not prevent other subscribers from running. Failures are exposed
through `SubscriberException`. Subscriptions hold strong references until disposed.

## Publishing

```csharp
using IndieableSdk.Events;

GlobalEventBus.Publish(
    "game.workorder.done",
    new WorkorderDoneEvent
    {
        workorder_id = "repair-pipe",
        node_id = "fulfillment-dispatch",
        duration_ms = 14000
    },
    new IndieableEventContext
    {
        IdempotencyKey = "repair-pipe-run-42",
        Test = true
    });
```

Event bus names are game-domain names. They may be mapped to different registered
Indieable event keys by the routing layer.

## Isolated buses

`GlobalEventBus` wraps a default `GameEventBus`. Worlds, tests, server shards, or
advanced hosts may instantiate their own:

```csharp
var worldBus = new GameEventBus();
using var subscription =
    worldBus.Subscribe<WorkorderDoneEvent>(
        "game.workorder.done",
        HandleWorkorder);

worldBus.Publish("game.workorder.done", payload);
```

Unity bridges may switch buses at runtime:

```csharp
bridge.Bind(worldBus);
```

## Unity routing asset

`IndieableEventRoutingSettings` is a ScriptableObject with:

```text
SelectionMode
TestByDefault
MaxPendingEvents
MaxEventsPerFrame
Routes[]
```

Each route contains:

```text
SourceEventName
IndieableEventKey
Forward
Purpose
optional Test override
```

Modes:

### Disabled

No local event is forwarded.

### AllowList

Only matching routes with `Forward=true` are forwarded. This is the recommended
production default.

### DenyList

Unmatched events are forwarded. A matching route with `Forward=false` blocks that
event. Matching enabled routes may also rename or classify events.

### All

Every local event is forwarded. Matching routes may rename/classify an event; the
`Forward` field is intentionally ignored because the mode means all.

`All` should be used only for deliberate integration testing. It can generate a
large number of rejected requests, but it still cannot bypass server-side schema,
purpose, permission, identity, trust, idempotency, or rate-limit rules.

## Purpose gates

The Unity and generic forwarders distinguish:

```text
GameplayTelemetry
Diagnostics
CommunityChallengeOperation
```

Gameplay telemetry and diagnostics require the corresponding current preference
before the event enters the network queue.

Community Challenge operation is a separate requested-feature purpose. The client
does not treat Challenge participation as broad telemetry permission. The server
remains authoritative for membership, persistent identity, schema, processing
authority, and event trust.

## No pre-permission replay

Game systems may publish local events before permission because the bus itself is
not tracking or transmitting data.

The bridge does not keep those optional events for later. If permission is not
currently available, it drops the event. A later grant applies only to future
events. This prevents a consent popup from becoming retroactive permission to
upload a hidden local history.

## Threading

`GameEventBus` is thread-safe and calls subscribers on the publishing thread.

The Unity bridge evaluates the routing/purpose gate immediately. Main-thread events
are forwarded directly. Allowed worker-thread events enter a bounded in-memory
handoff queue and are serialized/sent from `Update()`. The queue is only a
thread-affinity bridge; events that fail permission/routing are not placed into it.

The generic .NET forwarder uses a bounded channel and one asynchronous reader.

Neither forwarder is a durable delivery queue. Indieable API retries and backend
idempotency remain responsible for transient transport behavior.

## Payloads and schemas

The Unity bridge uses `JsonUtility` by default:

```csharp
[Serializable]
public sealed class DoorOpenedEvent
{
    public string door_id;
    public string method;
    public int open_count;
}
```

A JSON object string may be published directly. Custom serializers may be supplied
through `IndieableEventPayloadJson.CustomSerializer`.

Forwarding does not create a schema automatically. Production event definitions
remain registered and default-deny. Avoid freeform strings, nested arbitrary JSON,
email, real names, platform IDs, auth credentials, hardware identifiers, precise
location, or other sensitive data in ordinary gameplay events.

## Event trust

The global event bus does not make a client event trustworthy. Unity/game-client
events remain `CLIENT_REPORTED`. A modified client can fabricate them.

Challenges with meaningful prizes, money, exclusive access, or reputational stakes
should require the appropriate `SERVER_VERIFIED` event trust and a trusted server
path. Identity trust and event trust are separate.
