# Unity Event Bus Sample Acceptance

The SDK now ships one normal importable UPM sample instead of a repository-owned
second Unity project.

## Import

1. Open a disposable Unity 2022.3 or Unity 6 project.
2. Add the Indieable Nightly `.tgz`.
3. In Package Manager, select **Indieable Connect**.
4. Import **Event Bus Integration**.
5. Open:

```text
Assets/Samples/Indieable Connect/<version>/
Event Bus Integration/Scenes/IndieableEventBusSample.unity
```

The exact imported parent folder uses Unity's package display name/version.

## Scene hierarchy

Confirm the scene contains:

```text
Main Camera
Indieable SDK
Sample Gameplay Systems
├── Run System
├── Door
├── Workorder Terminal
├── Tunnel Node
└── Player Lifecycle
```

`Indieable SDK` must contain:

```text
IndieableEventBusSampleController
IndieableEventBusBridge
```

The bridge must reference `Config/SampleEventRouting.asset`.

## Local-only behavior

Enter Play Mode without changing the placeholder Public Game Key.

Expected:

- the UI Toolkit dashboard renders;
- no request occurs automatically;
- no persistent identity is created;
- clicking game-event buttons adds a local `BUS` entry;
- the bridge adds a local `DROP` entry because Indieable is not initialized;
- the scene/game systems continue normally.

The local log must not show payload values, tokens, Installation credentials,
provider subjects, or email addresses.

## Preview setup

In a disposable Preview game:

1. Enable Connect.
2. Publish a general-audience Player Data notice.
3. Enable Gameplay Telemetry with bounded retention.
4. Register exact test schemas for the five sample event keys.
5. Copy only the Preview Public Game Key.

Keep:

```text
Base URL: https://preview.indieable.com
Environment: development
Routing: AllowList
Test by default: true
```

## Privacy acceptance

1. Click **Initialize locally**.
   - no request from Initialize itself;
   - SDK state becomes initialized.

2. Click **Load privacy notice** before connecting.
   - notice loads;
   - session remains none;
   - no persistent identity appears.

3. Click **Connect session**.
   - an ephemeral session appears when no prior permitted Installation exists;
   - the permission popup opens;
   - gameplay telemetry and diagnostics are unselected.

4. Choose **Continue without optional data**.
   - both preferences remain off;
   - fire every sample gameplay event;
   - local `BUS` entries appear;
   - bridge `DROP` entries cite missing telemetry permission;
   - no old events are uploaded later.

5. Open permissions and allow Gameplay Telemetry only.
   - diagnostics remains off;
   - a persistent game-scoped Installation may be issued;
   - only future allowlisted telemetry events are forwarded.

6. Disable Gameplay Telemetry again.
   - the next event is dropped locally or rejected server-side;
   - previously dropped events do not replay.

7. Reset local identity.
   - Installation and active sessions are revoked;
   - local storage clears;
   - the next Connect starts ephemerally.

## Event routing acceptance

Fire each button and verify:

| Local bus name | Indieable event key |
|---|---|
| `game.door.opened` | `door_opened` |
| `game.workorder.done` | `workorder_done` |
| `game.node.closed` | `node_closed` |
| `game.player.died` | `player_died` |
| `game.run.completed` | `run_completed` |

Confirm:

- payloads match exact registered fields;
- events are marked test in development;
- idempotency keys are present;
- event failures do not throw into gameplay;
- unknown/disabled schemas are rejected without breaking the scene.

Change the routing asset in a copy of the sample and test:

```text
Disabled
AllowList
DenyList
All
```

Return to AllowList afterward.

## Direct SDK path

Click **Direct Connect test**.

Expected:

- the reserved direct test is accepted;
- no game-domain producer code changes;
- this demonstrates that `Indieable.SendEvent(...)` remains supported.

## Requested features

Exercise:

- browser account linking;
- feedback popup;
- bug-report popup;
- Challenge listing;
- Challenge join.

These actions must remain separate from gameplay telemetry and diagnostics choices.

## Unity quality pass

Test Unity 2022.3 and Unity 6:

- no compile errors;
- sample scene opens without missing scripts;
- routing asset resolves;
- UI Toolkit popup fits 1280×720, 1920×1080, and a narrow window;
- keyboard focus can reach all controls;
- equal permission actions remain equal width;
- the sample never changes `Time.timeScale`;
- closing/reopening Play Mode does not create hidden duplicate bus subscriptions.

## Generic C# acceptance

From the repository root:

```bash
dotnet build DotNet/Indieable.Sdk/Indieable.Sdk.csproj -c Release
dotnet run --project ci~/CoreSmoke/CoreSmoke.csproj -c Release
dotnet pack DotNet/Indieable.Sdk/Indieable.Sdk.csproj -c Release -o dist
```

Inspect the `.nupkg` and verify it contains the generic client, shared event bus,
README, and license—without UnityEngine code, samples, workflows, credentials, or
application/database source.
