# Indieable Connect

Indieable Connect is the public SDK repository for Indieable sessions,
privacy-aware game-scoped identity, longitudinal gameplay telemetry, account
linking, in-game playtest forms, and Community Challenges.

The repository ships two client surfaces:

- **Unity Package Manager** — `com.indieable.sdk`, including runtime UI and an
  importable Event Bus Integration sample.
- **Generic C#** — `DotNet~/Indieable.Sdk`, an engine-agnostic .NET 8 client.

It does not contain the Indieable application, database, Server Secrets, provider
credentials, or deployment configuration.

## Release channels

| Channel | Intended use | Version form |
|---|---|---|
| **Stable** | Normal development and production integration | `0.6.1` |
| **Nightly** | Indieable development, early integration, and Unity testing | `0.6.1-nightly.YYYYMMDD.RUN` |

- [Stable and historical releases](../../releases)
- [Current Nightly release](../../releases/tag/nightly)

Nightly is explicit opt-in and may change without migration support. Stable is
published from an immutable matching `vX.Y.Z` tag after real Unity acceptance.

## Install the Unity package

Download a release `.tgz`, then use:

```text
Window → Package Manager → + → Add package from tarball…
```

A public tagged version can also be installed through:

```text
https://github.com/hotboxed-studios/indieable-sdk.git#v0.6.1
```

For current integration testing:

```text
https://github.com/hotboxed-studios/indieable-sdk.git#main
```

Pin production games to a Stable tag rather than `main`.

## Import the Unity sample

The SDK ships a normal UPM sample, not a second Unity project.

```text
Window → Package Manager
→ Indieable Connect
→ Samples
→ Event Bus Integration
```

Open the imported scene:

```text
Scenes/IndieableEventBusSample.unity
```

The scene includes:

```text
Indieable SDK
  IndieableEventBusBridge
  UI Toolkit configuration and permission popup

Sample Gameplay Systems
  Run System
  Door
  Workorder Terminal
  Tunnel Node
  Player Lifecycle
```

The imported `Config/IndieableSampleUiToolkitAssets.asset` references editable
copies of the consent, feedback, bug-report, and runtime-theme assets under the
sample's `UI/` folder. The scene applies that asset set from `Awake`, before the
SDK's first-scene consent callback. Edit those imported UXML/USS/TSS files to
change the sample presentation. Projects that do not configure an override use
the packaged themed defaults.

The game-domain components publish events such as `DoorOpened`,
`WorkorderDone`, `NodeClosed`, `PlayerDied`, and `RunCompleted` to
`GlobalEventBus`. They do not call Indieable directly. A routing ScriptableObject
selects some, all, or all-except-denied events and maps local names to registered
Indieable event keys.

See [the sample README](Samples~/EventBusIntegration/README.md) and
[Unity sample testing guide](Documentation~/UNITY-SAMPLE-TESTING.md).

## Configure a Unity project

Open either of these equivalent entry points:

```text
Edit → Project Settings → Indieable
Tools → Indieable → Open Settings
```

Choose **Create Indieable Project Settings** once. This explicitly creates the
build-included project asset at
`Assets/Resources/Indieable/IndieableProjectSettings.asset`; the SDK never
creates or rewrites it during import, reload, or Play Mode. Configure the HTTPS
base URL, Public Game Key, environment, optional local-profile reference,
request policy, logging, identity recovery, and Event Bus routing asset there.
The Public Game Key is a client credential; never enter a Server Secret.

New settings use `https://preview.indieable.com` and `development`, but remain
disabled until a Public Game Key is supplied. With **Initialize Automatically**
enabled, the SDK loads these settings before the first scene's `Awake`. With
**Show Startup Consent** enabled, its built-in bottom-right UI Toolkit card opens
after the first scene loads and only returns for a new notice version until the
Player explicitly saves or declines. It has no dismiss-only action. Failed,
batch-mode, and headless attempts never mark the notice as answered. A manually
opened Player Data card can close if the notice is unavailable. Feedback and
Bug Report use the same visual language in larger centered, dimmed modals and
remain visible until the Player sends, cancels, or closes a completed/error
state.

## Global event bus

The event bus is optional and local-only:

```csharp
using IndieableSdk.Events;

GlobalEventBus.Publish(
    "game.door.opened",
    new DoorOpenedEvent
    {
        door_id = "dispatch-door",
        method = "interaction",
        open_count = 1
    });
```

Correlate multiplayer observations without including platform or lobby
identity:

```csharp
GlobalEventBus.Publish(
    "game.run.ended",
    payload,
    new IndieableEventContext
    {
        SchemaVersion = 1,
        RunId = sharedOpaqueRunId,
        TraceType = "multiplayer_run",
        TraceId = sharedOpaqueRunId,
        IdempotencyKey = sharedOpaqueRunId + "-ended"
    });
```

Publishing an event:

- performs no network request;
- creates no Indieable session or identity;
- serializes nothing;
- writes nothing to disk;
- does not require permission.

`IndieableEventBusBridge` is the integration boundary. It subscribes once, applies
the routing asset, checks the current purpose permission, serializes the payload,
and calls Indieable. Optional events published before permission are dropped and
are not replayed after a later grant.

The supplied modes are:

```text
Disabled
AllowList       recommended production default
DenyList
All
```

The backend remains default-deny: every forwarded production event still needs an
enabled exact schema, permitted purpose, valid session/identity, allowed trust
level, and rate-limit capacity.

Direct calls remain fully supported:

```csharp
Indieable.SendEvent(...);
IndieableTelemetry.Send(...);
```

The bus is a decoupling pattern, not a requirement.

## Automatic Unity setup

```text
SubsystemRegistration  reset SDK statics for a fresh runtime
BeforeSceneLoad         load project settings and initialize locally
AfterSceneLoad          request the one-time consent form
```

No scene component, prefab, execution-order setting, or game bootstrap is
required. Manual `Indieable.Initialize(...)` remains available for advanced
hosts and is idempotent; it never replaces an already active client.
Initialization is local and side-effect-free: it loads configuration and a
previously permitted local Installation credential but makes no request.
`Indieable.GetPrivacyManifest(...)` can be called before `Connect()` without
creating a session or persistent identifier.

### Optional request headers

`Project Settings > Indieable` can author optional headers applied to every SDK
request. Literal values are build-included project data and must never contain
private credentials. A header may instead resolve from an environment variable;
missing variables cause that optional header to be skipped. The SDK rejects
duplicate, malformed, newline-bearing, and SDK-owned headers such as
`Authorization` and `Content-Type`. The package includes no hosting-provider
bypass preset or credential default.

## Client credential boundary

A game's **Public Game Key** is intended to ship in a client. Never place any of
the following in this repository, a game, sample, build, commit, or release:

- Indieable Server Secret
- Supabase service-role or database credential
- Steam publisher/Web API key
- Discord webhook, bot token, or OAuth client secret
- signing key, certificate, private key, captured session credential, or `.env`

Runtime session and Installation credentials are issued by Indieable and stored
only through `IIndieableIdentityStorage`. Non-loopback endpoints must use HTTPS.
CI scans the current tree and reachable Git history, and release archives use a
strict allowlist.

## Identity and privacy lifecycle

```text
RuntimeInitializeLoadType.BeforeSceneLoad
  project settings load and local-only initialization

RuntimeInitializeLoadType.AfterSceneLoad
  public notice read; no session or persistent identifier

Indieable.Connect()
  short-lived ephemeral session unless a previously permitted Installation exists

Player enables telemetry or explicitly requests a persistent feature
  random game/environment-scoped Installation credential is issued
```

The SDK never uses `SystemInfo.deviceUniqueIdentifier`, hardware serials, MAC
addresses, advertising IDs, or device fingerprinting. An Installation is not a
confirmed human. Games may supply an opaque local save/profile reference:

```csharp
Indieable.SetLocalProfile("save-slot-2");
```

Do not use a name, email, Steam ID, Discord ID, or another real-world identifier as
a local profile reference.

## Optional permissions

Gameplay telemetry and diagnostics are independent and **off by default**. Account
linking, Steam, Challenges, forms, and marketing do not grant either choice.

```csharp
Indieable.SetPrivacyPreference(
    Indieable.GameplayTelemetryPurpose,
    enabled: true,
    onSuccess: preferences =>
        Debug.Log(preferences.PublicPlayerRef),
    onError: error =>
        Debug.LogWarning(error),
    customUi: true);
```

The sample gives equal treatment to:

```text
Continue without optional data
Allow selected
```

A prior affirmative choice may be reflected when the same Installation resumes,
but the sample never broadens it. Withdrawal is server-enforced.

## Generic C# SDK

The engine-agnostic package lives in:

```text
DotNet~/Indieable.Sdk
```

It provides async APIs for:

- public privacy-manifest reads;
- Connect sessions and persistent Installation continuity;
- purpose-specific preferences;
- local profiles and identity reset;
- account and Steam linking;
- rich event context (`occurred_at`, schema version, trace/run IDs);
- feedback and bug reports;
- Challenges and leaderboards;
- the same pure C# event bus and optional forwarder.

Build or pack it with:

```bash
dotnet build DotNet~/Indieable.Sdk/Indieable.Sdk.csproj -c Release
dotnet pack DotNet~/Indieable.Sdk/Indieable.Sdk.csproj -c Release -o dist
```

See [the .NET package README](DotNet~/Indieable.Sdk/README.md).

## Account linking, forms, and Challenges

```csharp
Indieable.LinkAccount(
    link => Application.OpenURL(link.VerificationUrlComplete));

Indieable.OpenFeedback();
Indieable.OpenBugReport();

Indieable.GetChallenges(
    collection => Debug.Log(collection.Joined.Length));
Indieable.JoinChallenge("challenge-slug");
Indieable.GetLeaderboard(
    "challenge-slug",
    board => Debug.Log(board.Total));
```

Steam support uses the host game's implementation of
`IIndieableSteamTicketProvider`; the Unity package has no Steamworks dependency or
publisher key. Challenge operation is a separate requested-feature purpose, so
broad gameplay telemetry may remain off.

## Validation and releases

Local checks require Python 3.12 and .NET 8:

```bash
python scripts~/scan_secrets.py --history
python scripts~/validate_package.py
python scripts~/validate_examples.py
dotnet build ci~/CompileCheck/CompileCheck.csproj -c Release
dotnet run --project ci~/CoreSmoke/CoreSmoke.csproj -c Release
dotnet pack DotNet~/Indieable.Sdk/Indieable.Sdk.csproj -c Release -o dist
python scripts~/package.py --channel stable --output dist
```

CI compiles the Unity package/sample against zero-secret Unity API stubs, builds
and smoke-tests the generic C# client, and produces both UPM and NuGet-form
artifacts. A real Unity import, sample-scene Play Mode pass, and Preview end-to-end
test remain Stable release gates.

## License

Indieable Connect is available under the [MIT License](LICENSE.md).
