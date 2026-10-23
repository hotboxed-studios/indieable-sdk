# Indieable.Sdk for .NET

> **SDK in development.** This is experimental or legacy maintainer material,
> not the current product’s supported install path. Use the
> [HTTP API guide](https://github.com/ConnorIllingworth/indieable/blob/codex/unified-player-workspaces/docs/DEVELOPER-WORKFLOW-API.md)
> for current integrations. Unity/v1 setup does not connect to the current v2
> application. Actual engine/export support has not been verified.


Engine-agnostic C# client for Indieable. It targets .NET 8 and is built from the
same public repository as the Unity package.

## Developer-workflow v2

Use `IndieableSdk.DeveloperWorkflow` for the new developer-workflow web branch.
Its APIs are `/api/connect/v2/{links,sessions,events,feedback}` and the
session-authenticated `GET /api/connect/v2/feedback/config`. Render that returned
configuration's survey questions in your engine's UI and submit the player's
answers keyed by those exact questions; no duplicated form definition is needed.

```csharp
using IndieableSdk.DeveloperWorkflow;

// Trusted backend only; load the credential from the host's secret store.
using var backend = new WorkflowBackendClient(
    new WorkflowOptions { BaseUrl = "https://indieable.com" },
    Environment.GetEnvironmentVariable("INDIEABLE_WORKFLOW_CREDENTIAL")
        ?? throw new InvalidOperationException("Missing backend credential"));

// Your game first authenticates this opaque account ID. The player explicitly
// supplies a short-lived, one-use link code, never their contact-management token.
var linked = await backend.LinkAsync(
    authenticatedAccountId,
    new WorkflowLinkProof(playerSuppliedLinkCode),
    stableLinkRequestId);

var session = await backend.CreateSessionAsync(new WorkflowSessionRequest
{
    Subject = authenticatedAccountId,
    TelemetryConsent = playerHasChosenGameplayData,
    RoundId = playtestRoundId,
    Build = "test-build-1"
});

// Use only for observations your game backend actually verified.
var outcome = await backend.TrySendVerifiedEventsAsync(
    session.SessionToken,
    new[] { new WorkflowEvent { Key = "session.started" } },
    Guid.NewGuid());
// A temporary outage produces outcome.Error; it need not interrupt gameplay.
```

The [full integration guide](https://github.com/hotboxed-studios/indieable-sdk/blob/codex/developer-player-workflow/Documentation~/DEVELOPER-WORKFLOW.md)
covers scoped linking proof, consent, feedback without telemetry, nonblocking
delivery, retries, bounds, trust, and the live local contract harness. This branch
has not been published; build the package locally against the matching web branch.

## Existing v1 client

The original `IndieableClient` below requires the older Connect backend. These
v1 endpoints are not present in the new developer-workflow web branch. The v2
backend credential must never be used as a v1 Public Game Key.

```csharp
using IndieableSdk;

await using var indieable = new IndieableClient(
    new IndieableClientOptions
    {
        BaseUrl = "https://preview.indieable.com",
        PublicGameKey = "ind_pub_replace_me",
        Environment = "development",
        BuildVersion = "0.1.0"
    });

var notice = await indieable.GetPrivacyManifestAsync();
var session = await indieable.ConnectAsync();
```

Constructing `IndieableClient` is local. `GetPrivacyManifestAsync` reads the public
notice without creating a session. `ConnectAsync` creates or resumes the explicitly
requested Connect session.

Optional headers can be applied to every request before constructing the
client. Do not place private credentials in source control:

```csharp
var options = new IndieableClientOptions
{
    BaseUrl = "https://preview.indieable.com",
    PublicGameKey = "ind_pub_replace_me"
};
var optionalHeader = Environment.GetEnvironmentVariable(
    "INDIEABLE_OPTIONAL_HEADER");
if (!string.IsNullOrWhiteSpace(optionalHeader))
{
    options.RequestHeaders.Add(
        "x-client-variant",
        optionalHeader);
}
await using var indieable = new IndieableClient(options);
```

## Optional telemetry

```csharp
var preferences = await indieable.SetPrivacyPreferenceAsync(
    IndieableClient.GameplayTelemetryPurpose,
    enabled: true);

var receipt = await indieable.SendEventAsync(
    "run_completed",
    new
    {
        floor = 8,
        time_ms = 123000,
        deaths = 2,
        players = 3
    },
    new IndieableEventOptions
    {
        Test = true,
        RunId = "run-42",
        IdempotencyKey = "run-42-completed"
    });
```

The server derives game, Game Player, Installation, permission receipt, identity
trust, and event trust. Do not put email addresses, provider identifiers, auth
credentials, hardware identifiers, or arbitrary sensitive/freeform data into
ordinary gameplay payloads.

## Global event bus

The pure C# `IndieableSdk.Events` event bus is shared with the Unity package:

```csharp
using IndieableSdk.Events;

GlobalEventBus.Publish(
    "game.workorder.done",
    new
    {
        workorder_id = "repair-pipe",
        node_id = "fulfillment-dispatch",
        duration_ms = 14000
    });
```

An optional forwarder keeps game code independent from the HTTP SDK:

```csharp
await using var forwarder = new IndieableEventBusForwarder(
    indieable,
    new IndieableEventBusForwarderOptions
    {
        Mode = IndieableEventForwardingMode.AllowList,
        TestByDefault = true,
        Routes = new[]
        {
            new IndieableEventForwardingRoute
            {
                SourceEventName = "game.workorder.done",
                IndieableEventKey = "workorder_done",
                Purpose =
                    IndieableEventForwardingPurpose.GameplayTelemetry
            }
        }
    });

forwarder.ApplyPrivacyPreferences(preferences);
```

Optional-purpose events published before the current permission exists are dropped
before enqueueing. They are not stored locally for later replay.

## Identity storage

The default desktop/server storage writes a random game/environment-scoped
Installation credential under the current user's local application-data directory.
It never uses a hardware fingerprint. Production hosts may inject
`IIndieableIdentityStorage` backed by OS credential protection or another secure
store.

Only a Public Game Key belongs in a client or ordinary application. Never ship an
Indieable Server Secret, Supabase credential, Steam publisher key, OAuth client
secret, webhook token, signing key, or captured runtime credential.
