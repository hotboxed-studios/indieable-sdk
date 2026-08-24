# Indieable.Sdk for .NET

Engine-agnostic C# client for Indieable. It targets .NET 8 and is built from the
same public repository as the Unity package.

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
var vercelBypass = Environment.GetEnvironmentVariable(
    "VERCEL_AUTOMATION_BYPASS_SECRET");
if (!string.IsNullOrWhiteSpace(vercelBypass))
{
    options.RequestHeaders.Add(
        "x-vercel-protection-bypass",
        vercelBypass);
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
