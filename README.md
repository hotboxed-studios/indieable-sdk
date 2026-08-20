# Indieable Connect for Unity

Indieable Connect is the standalone Unity Package Manager SDK for Indieable sessions,
privacy-aware game-scoped identity, longitudinal gameplay telemetry, account linking,
in-game playtest feedback, bug reports, and Community Challenges.

The repository contains only the client package, samples, package validation, and
release automation. It does not contain the Indieable application, database code,
server credentials, provider credentials, or deployment configuration.

## Release channels

Indieable publishes two explicit SDK channels:

| Channel | Intended use | Version form |
|---|---|---|
| **Stable** | Normal game development and production integration | `0.4.0` |
| **Nightly** | Indieable development, early integration, and Unity testing | `0.4.0-nightly.YYYYMMDD.RUN` |

Stable is produced from a matching `vX.Y.Z` tag. Nightly is a rolling prerelease built
from `main` after every push, on a daily schedule, and on manual dispatch.

- [Stable and historical releases](../../releases)
- [Current Nightly release](../../releases/tag/nightly)

Nightly is intentionally opt-in and may change without migration support.

## Install in Unity

### Download a release tarball

1. Open the desired GitHub release.
2. Download `indieable-connect-<version>.tgz`.
3. In Unity, open **Window → Package Manager**.
4. Click **+ → Add package from tarball…**.
5. Select the downloaded archive.

Each release also includes a SHA-256 checksum and JSON build metadata.

### Install a tagged Stable version with Git

In Package Manager choose **+ → Add package from git URL…** and enter:

```text
https://github.com/hotboxed-studios/indieable-sdk.git#v0.4.0
```

For a private repository, Git must already be authenticated on the development
machine. A downloaded tarball avoids Unity/Git credential handling.

### Test the working tree locally

Clone this repository, then choose **+ → Add package from disk…** and select the root
`package.json`. This is the fastest path while developing the SDK itself.

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
`Indieable.GetPrivacyManifest(...)` can also be called before `Connect()` without
creating a session or persistent identifier.

## Client credential boundary

A game's **Public Game Key** is designed to ship in a game client. Everything else
that authenticates a trusted backend must remain outside Unity.

Never place any of the following in this repository, a Unity project, a build, a
sample, a Git commit, or a release archive:

- Indieable Server Secret
- Supabase service-role key or database credentials
- Steam publisher/Web API key
- Discord webhook or bot token
- OAuth client secret or refresh token
- signing key, certificate, private key, or `.env` file

The SDK receives short-lived session and Installation credentials from Indieable at
runtime. Those values are stored only on the Player's machine through
`IIndieableIdentityStorage`; they are not source-controlled or compiled into the
package.

The CI pipeline scans both the current tree and all reachable Git history for common
secret formats. The package builder additionally uses a strict allowlist, so workflow
files, test tooling, repository metadata, and local configuration cannot enter a
release archive.

## Identity and privacy lifecycle

```text
Indieable.Initialize()
  local only; no network request

Indieable.GetPrivacyManifest()
  public notice read; no session or persistent identifier

Indieable.Connect()
  short-lived ephemeral session unless a previously permitted Installation exists

Player enables telemetry or explicitly requests a persistent feature
  server issues one random, game/environment-scoped Installation credential
  SDK stores it under Application.persistentDataPath
```

The package never uses `SystemInfo.deviceUniqueIdentifier`, hardware serials, MAC
addresses, advertising identifiers, or device fingerprinting. An Installation means
one installation of one game, not one confirmed human. Games with local users or save
slots can provide an opaque local profile reference:

```csharp
Indieable.SetLocalProfile("save-slot-2");
```

Do not pass a name, email, Steam ID, Discord ID, or other real-world identifier as a
local profile reference.

Optional gameplay telemetry and diagnostics are independent choices. Account linking,
Steam, Challenges, feedback, and marketing do not grant either choice.

## Privacy preferences

Open the dependency-free default UI:

```csharp
Indieable.OpenPrivacyPreferences();
```

Or use a custom UI:

```csharp
Indieable.SetPrivacyPreference(
    Indieable.GameplayTelemetryPurpose,
    enabled: true,
    onSuccess: preferences => Debug.Log(preferences.PublicPlayerRef),
    onError: error => Debug.LogWarning(error),
    customUi: true);
```

Withdrawal is enforced by the server. Resetting the local identity revokes the
Installation and its active sessions before clearing local storage:

```csharp
Indieable.ResetLocalIdentity();
```

## Test the connection

```csharp
Indieable.SendEvent(
    "indieable.connect_test",
    "{\"message\":\"Unity integration reached Indieable.\"}",
    test: true);
```

The reserved event is intended for integration testing and does not affect production
Challenge rankings.

## Gameplay telemetry

```csharp
IndieableTelemetry.Send(
    "run_completed",
    "{\"floor\":8,\"time_ms\":123000,\"deaths\":2,\"players\":3}",
    idempotencyKey: "run-8b0f3e31");
```

Production events are accepted only when the developer has registered the exact event
schema and the server permits the event's processing purpose. The client cannot choose
a trusted Game Player, Installation, permission receipt, identity trust, or event
trust.

Ordinary gameplay telemetry should describe what happened in the game. Use Indieable's
separate feedback and bug-report APIs for freeform user submissions.

## Account linking and Steam

```csharp
Indieable.LinkAccount(link =>
{
    Debug.Log(link.UserCode);
    Debug.Log(link.VerificationUrlComplete);
});
```

Verifying an account changes future activity to the verified game-scoped Player. It
does not silently claim ambiguous history from a shared installation.

For Steam, implement `IIndieableSteamTicketProvider` in the host game using its
existing Steam integration. The SDK has no Steamworks dependency and never needs a
publisher key.

## In-game playtesting

```csharp
Indieable.OpenFeedback();
Indieable.OpenBugReport();
```

The optional runtime UI creates its own object, requires no host Canvas, and never
changes `Time.timeScale`. Games with custom UI can call `GetFeedbackConfig`,
`SubmitFeedback`, and `SubmitBugReport` directly.

## Community Challenges

```csharp
Indieable.GetChallenges(collection =>
{
    foreach (var challenge in collection.Joined)
        Debug.Log(challenge.Name);
});

Indieable.JoinChallenge("challenge-slug");
Indieable.GetLeaderboard("challenge-slug", leaderboard =>
{
    foreach (var row in leaderboard.Items)
        Debug.Log($"#{row.Rank} {row.DisplayName} {row.BestScore}");
});
```

## Samples

In Unity Package Manager, open the package's **Samples** tab and import **Quick Start**.
Add `IndieableQuickStart` to a scene object and enter the game's Public Game Key in the
Inspector. The sample uses a placeholder and contains no live credentials.

## Failure behavior

Requests retry only bounded transient failures. Errors are returned through callbacks
and logged as warnings by default. Indieable does not pause, quit, or fail the host
game when the service is unavailable.

## Repository validation and releases

Local checks require Python 3.12 and the .NET 8 SDK:

```bash
python scripts/scan_secrets.py --history
python scripts/validate_package.py
dotnet build ci/CompileCheck/CompileCheck.csproj --configuration Release
python scripts/package.py --channel stable --output dist
```

The compile check uses minimal Unity API stubs only to catch C# syntax and public API
breakage in a zero-secret GitHub runner. A real Unity import and Play Mode test remains
the release acceptance step.

To create a Stable release, update `package.json` and `CHANGELOG.md`, then push the
matching tag:

```bash
git tag v0.4.0
git push origin v0.4.0
```

The release workflow refuses a tag whose version differs from `package.json`.
