# Global Event Bus integration

The Unity package includes an optional local-event architecture that keeps gameplay systems independent from Indieable.

```text
Gameplay system
    -> GlobalEventBus.Publish(...)
    -> IndieableEventBusBridge
    -> Indieable only when the route and purpose are allowed
```

The bus is local, synchronous, and pure C#. Publishing an event does not itself perform a network request, create an Indieable identity, or imply permission to collect optional telemetry.

## Recommended ownership boundary

Gameplay assemblies own their event names and payload types. Examples include:

```text
game.workorder.done
game.node.closed
game.player.died
game.door.opened
game.run.completed
```

The Indieable integration assembly owns the routing asset and bridge. It may map a local name to a registered Indieable event key, classify the purpose, mark sample traffic as test traffic, and choose disabled, allow-list, deny-list, or all-event routing.

## Direct calls remain supported

Games that do not use the bus may continue calling `Indieable.SendEvent(...)` or `IndieableTelemetry.Send(...)` directly. The event bus is an integration pattern, not a mandatory application framework.

## Privacy behavior

Optional gameplay telemetry and diagnostics remain permission-gated. Events published before the corresponding optional permission is available are discarded rather than buffered and replayed later. Community Challenge operation remains a separate requested-feature purpose and is still validated server-side for identity, membership, authority, schema, and event trust.

## Import the sample

In Unity Package Manager, open the Indieable package's **Samples** section and import **Global Event Bus + Indieable Bridge**. Open the imported `IndieableEventBusSample.unity` scene to inspect the authored GameObjects, gameplay publishers, routing asset, bridge, and UI Toolkit controls.
