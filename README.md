# Indieable Connect for Unity

Indieable Connect is the standalone Unity Package Manager SDK for Indieable sessions,
privacy-aware game-scoped identity, longitudinal gameplay telemetry, account linking,
in-game playtest feedback, bug reports, and Community Challenges.

This public repository contains only the client package, samples, a minimal
`UnityExample` project, validation, and release automation. It does not contain the
Indieable application, database code, Server Secrets, provider credentials, or
deployment configuration.

## Release channels

| Channel | Intended use | Version form |
|---|---|---|
| **Stable** | Normal development and production integration | `0.4.0` |
| **Nightly** | Indieable development, early integration, and Unity testing | `0.4.0-nightly.YYYYMMDD.RUN` |

- [Stable and historical releases](../../releases)
- [Current Nightly release](../../releases/tag/nightly)

Nightly is explicit opt-in and may change without migration support. Stable is
published from an immutable matching `vX.Y.Z` tag after Unity acceptance.

## Install

Download a release `.tgz`, then use:

```text
Window → Package Manager → + → Add package from tarball…
```

A public tagged Stable version can also be installed through:

```text
https://github.com/hotboxed-studios/indieable-sdk.git#v0.4.0
```

For current pre-release integration testing:

```text
https://github.com/hotboxed-studios/indieable-sdk.git#main
```

Pin production games to a Stable tag rather than `main`.

## UnityExample and UI Toolkit sample

The repository includes two ways to exercise the SDK:

- **UI Toolkit Integration Lab** — importable from the package Samples tab.
- [`UnityExample/`](UnityExample) — a complete minimal Unity 2022.3 project that
  references the repository root through `file:../..`.

The runtime dashboard covers:

- local-only initialization;
- side-effect-free privacy-manifest loading;
- ephemeral and persistent sessions;
- a balanced optional-permission popup;
- gameplay telemetry and diagnostics **off by default**;
- Preview `run_completed` test events;
- account linking;
- playtest feedback and bug reports;
- Challenge listing, joining, and leaderboards;
- local identity reset.

Open `UnityExample` in Unity Hub, load
`Assets/Scenes/IndieableExample.unity`, and enter Play Mode. The empty scene starts
the dashboard automatically. See
[`docs/UNITY-EXAMPLE-TESTING.md`](docs/UNITY-EXAMPLE-TESTING.md).

## Minimal setup

```csharp
using IndieableSdk;
using UnityEngine;

public sealed class IndieableBootstrap : MonoBehaviour
{
    [SerializeField] private string publicGameKey = "ind_pub_replace_me";
    [SerializeField] private string baseUrl = "https://indieable.com";

    private void Awake()
    {
        Indieable.Initialize(new IndieableOptions
        {
            BaseUrl = baseUrl,
            PublicGameKey = publicGameKey,
            BuildVersion = Application.version,
            Environment = Debug.isDebugBuild ? "development" : "production"
        });
    }

    private void Start()
    {
        Indieable.Connect(
            session => Debug.Log($"Indieable: {session.IdentityState}"),
            error => Debug.LogWarning(error));
    }
}
```

`Indieable.Initialize(...)` is local and side-effect-free. It loads configuration and
a previously permitted local Installation credential, but makes no network request.
`Indieable.GetPrivacyManifest(...)` can be called before `Connect()` without creating
a session or persistent identifier.

## Client credential boundary

A game's **Public Game Key** is designed to ship in a client. Never place any of the
following in this repository, a Unity project, sample, build, commit, or release:

- Indieable Server Secret
- Supabase service-role key or database credentials
- Steam publisher/Web API key
- Discord webhook or bot token
- OAuth client secret or refresh token
- signing key, certificate, private key, captured session credential, or `.env` file

Runtime session and Installation credentials are issued by Indieable and stored only
on the Player's machine through `IIndieableIdentityStorage`. Non-loopback endpoints
must use HTTPS. CI scans the current tree and all reachable Git history, and release
archives use a strict allowlist.

## Identity and privacy lifecycle

```text
Indieable.Initialize()
  local only; no network request

Indieable.GetPrivacyManifest()
  public notice read; no session or persistent identifier

Indieable.Connect()
  short-lived ephemeral session unless a previously permitted Installation exists

Player enables telemetry or explicitly requests a persistent feature
  random game/environment-scoped Installation credential is issued
  SDK stores it under Application.persistentDataPath
```

The SDK never uses `SystemInfo.deviceUniqueIdentifier`, hardware serials, MAC
addresses, advertising identifiers, or device fingerprinting. An Installation is not
a confirmed human. A game may supply an opaque local save/profile reference:

```csharp
Indieable.SetLocalProfile("save-slot-2");
```

Do not use a name, email, Steam ID, Discord ID, or another real-world identifier as a
local profile reference.

## Optional permissions

Gameplay telemetry and diagnostics are independent and **off by default**. Account
linking, Steam, Challenges, forms, and marketing do not grant either choice.

```csharp
Indieable.SetPrivacyPreference(
    Indieable.GameplayTelemetryPurpose,
    enabled: true,
    onSuccess: preferences => Debug.Log(preferences.PublicPlayerRef),
    onError: error => Debug.LogWarning(error),
    customUi: true);
```

The UI Toolkit example gives equal treatment to:

```text
Continue without optional data
Allow selected
```

A prior affirmative choice may be reflected when the same Installation resumes, but
the example never broadens it. Withdrawal is server-enforced. Resetting local identity
revokes the Installation and active sessions before clearing storage:

```csharp
Indieable.ResetLocalIdentity();
```

## Test and telemetry events

```csharp
Indieable.SendEvent(
    "indieable.connect_test",
    "{\"message\":\"Unity integration reached Indieable.\"}",
    test: true);

IndieableTelemetry.Send(
    "run_completed",
    "{\"floor\":8,\"time_ms\":123000,\"deaths\":2,\"players\":3}",
    idempotencyKey: "run-8b0f3e31");
```

Production events require a registered exact schema and an allowed processing
purpose. The backend derives Game Player, Installation, permission receipt, identity
trust, and event trust. Ordinary telemetry should describe what happened in the game;
use forms for freeform user submissions.

## Account linking, playtesting, and Challenges

```csharp
Indieable.LinkAccount(link => Application.OpenURL(link.VerificationUrlComplete));
Indieable.OpenFeedback();
Indieable.OpenBugReport();

Indieable.GetChallenges(collection => Debug.Log(collection.Joined.Length));
Indieable.JoinChallenge("challenge-slug");
Indieable.GetLeaderboard("challenge-slug", board => Debug.Log(board.Total));
```

Steam support uses the host game's implementation of
`IIndieableSteamTicketProvider`; the package has no Steamworks dependency or publisher
key. Challenge operation is a separate requested purpose, so broad gameplay telemetry
may remain off.

## Validation and releases

Local checks require Python 3.12 and .NET 8:

```bash
python scripts/scan_secrets.py --history
python scripts/validate_package.py
python scripts/validate_examples.py
dotnet build ci~/CompileCheck/CompileCheck.csproj --configuration Release
python scripts/package.py --channel stable --output dist
python scripts/package_example.py --output dist
```

CI compiles the public SDK and UI Toolkit sample against a zero-secret Unity API stub.
Nightly and Stable releases include the UPM tarball, checksums/metadata, and a
self-contained UnityExample ZIP. A real Unity import and Play Mode test remains a
Stable release gate.

## License

Indieable Connect is available under the [MIT License](LICENSE.md).
