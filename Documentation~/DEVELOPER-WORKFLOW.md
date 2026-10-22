# Developer-workflow integration (v2)

This branch adds a small .NET 8 integration for the matching Indieable web
developer-workflow branch. It is suitable for any studio's trusted game backend.
It does not replace the game's login and contains no game-specific logic.
Organizations own games and their subscriptions; individual accounts join those
organizations as Owner, Admin, or Member. Paid access in one organization never
unlocks another organization's features.
Website audience signup, private playtests, invitations, build access, ordinary
feedback, saved groups, and messages remain useful without an SDK.

The Unity package and original `IndieableClient` remain v1 compatibility code for
the earlier Connect backend. They cannot call v2 automatically. The new workflow
code lives under `DotNet~/Indieable.Sdk/DeveloperWorkflow` and is excluded from the
UPM archive. No new SDK package is published by this branch.

## Boundaries

| Identifier or credential | Meaning | Where it belongs |
|---|---|---|
| Studio operator account | Individual account with an organization role | Indieable's developer login |
| Developer organization | Durable owner of games and their subscription/quotas | Organization workspace; separate from an individual account |
| Email | Contact method and consent provenance | Indieable audience relationship; never an account join key |
| `gp_*` player reference | One relationship within one game | That game's workspace/backend; not a global player ID |
| Credential-bound issuer + opaque subject | Game account the trusted backend authenticated | Game backend; issuer cannot be overridden in a request |
| `dw_server_*` credential | Backend authority bound to a game, its current organization, issuer, and issuing operator | Backend secret store only |
| `dw_manage_*` credential | Player contact and purpose-preference authority | Player's first-party Indieable management experience only |
| `dw_link_*` proof | Short-lived, one-use permission to link the relationship | Explicitly passed by the player to their authenticated game backend |
| `dw_session_*` credential | Short-lived session scoped to an already linked game relationship | Authenticated game client or backend; never another player |
| Installation / character reference | Optional opaque observation context | Event properties; does not identify or authenticate a person |

The backend authenticates its own account before supplying `subject`. Do not
trust an arbitrary subject from browser or client JSON. Use a stable internal
account identifier, not email, a Steam ID in an unrelated namespace, a character
name, or an installation-local save slot. One backend credential is bound to
one game and issuer; using an email shared by two people never silently merges
them. A conflicting identity or reused linking proof returns HTTP 409.

Owners and Admins manage game integrations; Members have a masked read-only
workspace and cannot issue credentials or submit developer mutations. Billing
and organization/game ownership controls belong to Owners. Membership is checked
again on supported operations and delayed work. Removing a credential's issuing
operator, demoting that operator to Member, disabling the integration, rotating
its credential generation, or moving the game to another organization revokes
the affected authority. Existing credentials cannot follow a game into another
organization. Shared history remains with the game if its legacy creator leaves
or deletes their individual account after any required ownership handover.

Linking proves a relationship. It never grants marketing or optional gameplay
permission. A backend must never receive an audience management token or an
original personal feedback token: either grants broader player authority than
the backend needs. The player mints a separate scoped linking code instead.

## 1. Collect and confirm the audience

Use Indieable's supported website audience form/API, independent of an open
playtest or a published public listing. A game can bring all of its own players
and run private tests. The signup records source and purpose-specific choices;
the confirmation email proves control of the contact method.

Website integration and confirmation belong to the web API. Do not write to
Indieable's database from a game or website. A generic account link requires
explicit player possession proof; matching email is insufficient.

## 2. The player explicitly creates a link code

In the first-party Indieable audience-management or personal-playtest experience,
the player calls:

```http
POST /api/audience/v1/link-proof
Content-Type: application/json

{"management_token":"<player-only-management-token>"}
```

A personal playtest link can instead provide `playtest_token`. The response is
`{ "link_token": "dw_link_…", "expires_at": "…" }`. The code expires after
10 minutes and authorizes one link operation. The player explicitly copies or
passes only `link_token` to the game they are already signed into. Keep tokens
out of URLs, logs, analytics, and chat. This player endpoint is deliberately not
wrapped by the trusted-backend SDK.

## 3. Link the game's authenticated account

An Owner or Admin creates a backend credential through the organization's game
Integrations workflow, then loads it from the game backend's secret store. Never put it into
Unity project settings, a mobile/desktop build, browser JavaScript, a sample, or
source control. HTTPS is required except on loopback during local tests.

```csharp
using IndieableSdk.DeveloperWorkflow;

using var backend = new WorkflowBackendClient(
    new WorkflowOptions
    {
        BaseUrl = "https://indieable.com",
        RequestTimeout = TimeSpan.FromSeconds(5),
        MaxRetries = 2
    },
    backendCredentialFromSecretStore);

// These values come from your authenticated game session and explicit player action.
var requestId = Guid.NewGuid();
var receipt = await backend.LinkAsync(
    authenticatedGameAccountId,
    new WorkflowLinkProof(playerSuppliedLinkToken),
    requestId,
    cancellationToken);
```

The request is `POST /api/connect/v2/links` with backend bearer authentication and
`{request_id, subject, link_token}`. The game and issuer come from the credential.
The response contains only `{player_ref, linked}`, with no global account ID or
player-management token. Keep the same request ID and body when retrying a link;
reusing its one-use proof for a different operation is rejected.

## 4. Create a session without replacing game login

```csharp
var session = await backend.CreateSessionAsync(new WorkflowSessionRequest
{
    RequestId = Guid.NewGuid(),
    Subject = authenticatedGameAccountId,
    TelemetryConsent = playerHasChosenOptionalGameplayData,
    RoundId = activePlaytestRoundId,
    Build = "test-build-1"
}, cancellationToken);
```

`POST /api/connect/v2/sessions` returns `session_token`, `player_ref`, `expires_at`,
and `gameplay_enabled`. Indieable independently checks the player's stored
gameplay permission. Setting `TelemetryConsent = true` does not create that
permission. Setting it to false narrows the session to non-telemetry features,
including feedback. A personal playtest link alone grants no optional telemetry.
Players change optional gameplay choices through their own management proof;
withdrawal is checked server-side for later event ingestion.

Return only the short-lived session credential to the authenticated game client
if client-reported observations or in-game feedback are needed. Reauthenticate
your game's account before issuing a replacement; never mint sessions for a
caller-supplied account ID. A session has no marketing authority.

## 5. Submit bounded observations with explicit trust

Only session boundaries and registered milestones are supported:

| Key | Properties | Interpretation |
|---|---|---|
| `session.started` | Optional installation and character references | A session observation, not inferred from a download or email |
| `session.ended` | Optional `duration_seconds` from 0 through 86400 | Observed session duration |
| `milestone.<registered_key>` | Optional installation and character references | A developer-defined milestone registered for this game |

Keys are case-sensitive. Registered milestone names use a lowercase letter then
up to 79 lowercase letters, digits, underscores, or periods. Installation and
character references are at most 100 characters, with no whitespace or `@`;
they are context, not an account-linking mechanism. Do not include names, emails,
authentication tokens, hardware identifiers, or freeform personal information.

```csharp
var eventId = Guid.NewGuid();
var observation = new WorkflowEvent
{
    EventId = eventId,
    Key = "session.ended",
    OccurredAt = DateTimeOffset.UtcNow,
    Properties = new WorkflowEventProperties
    {
        DurationSeconds = 180,
        InstallationRef = "opaque-installation-7",
        CharacterRef = "opaque-character-2"
    }
};

var delivery = await backend.TrySendVerifiedEventsAsync(
    session.SessionToken,
    new[] { observation },
    Guid.NewGuid(),
    cancellationToken);

if (!delivery.Succeeded)
{
    // Optional integration diagnostics: record only the bounded error code.
    // Continue gameplay; do not retry indefinitely or include the request body.
    RecordIntegrationFailure(delivery.Error!.Code);
}
```

`server_verified` means a valid game backend submitted an observation it verified.
It is not an independent anticheat or proof-of-human system; the studio must
submit only observations its backend actually made or validated.
Do not launder arbitrary client JSON through this method. With only a short-lived
session credential, the client API produces `client_reported` observations:

```csharp
using var client = new WorkflowSessionClient(
    new WorkflowOptions { BaseUrl = "https://indieable.com" },
    sessionTokenFromAuthenticatedGameBackend);

var reported = await client.TrySendReportedEventsAsync(
    new[] { new WorkflowEvent { Key = "session.started" } },
    Guid.NewGuid(),
    cancellationToken);
```

Both use `POST /api/connect/v2/events`. Backend-authenticated events additionally
carry `player_session_token`; no API accepts caller-selected player IDs. A batch
contains 1–50 events, serialized request size is at most 32 KiB, timestamps must
fall within the previous seven days and five minutes ahead of the server clock,
and events require current gameplay permission. An integration cannot turn
downloads, queue acceptance, or self-reported check-ins into verified play.

## Nonblocking delivery and retry behavior

The explicit async methods have a total deadline (default five seconds), at most
three retries (default two), and bounded backoff. Only transient network errors,
429s, and eligible 5xx responses retry. Permission errors, malformed requests,
unknown milestones, conflicts, and invalid credentials do not. The transport
honors a short `Retry-After`; a backoff longer than two seconds returns a
retryable result to the host without retrying early. Retries preserve
the exact serialized body, request ID, and event IDs. Keep original event IDs if
your host retries later; do not regenerate IDs for an existing observation.

`TrySend…` returns `WorkflowDelivery<T>` with a receipt or structured
`WorkflowException` (`Code`, `StatusCode`, `Retryable`). Caller cancellation still
propagates. Invalid SDK arguments report programming errors. Errors never include
the credential or request body. The SDK creates no local identity, cookie,
device fingerprint, file, or persistent event backlog.

After credential rotation or an organization/access change, do not replay a
session-creation request ID under a new credential generation. Authenticate the
game account again and create a new operation/session. A 401/403 is a permanent
denial for the current credential or session; the SDK does not retry it. Obtain
new authorized credentials through the organization's supported workflow rather
than falling back to an individual's entitlement or the old creator ID.

For network-independent gameplay callbacks, use the optional bounded buffer:

```csharp
await using var deliveryBuffer = backend.CreateEventBuffer(
    session.SessionToken, capacity: 128);

// Returns immediately. A full or disposed queue returns false.
deliveryBuffer.TryEnqueue(new WorkflowEvent { Key = "session.started" });
```

The buffer sends serially in the background with the same bounded transport
policy. It holds at most the configured 1–1024 pending events plus one in flight,
drops failures after the bounded attempt, persists nothing, and discards pending
observations on disposal. Dispose the buffer before its client. It never creates
a session or grants permission. Its optional `DeliveryCompleted` notification
does not log automatically; subscriber exceptions cannot stop delivery.

An injected `HttpClient` remains host-owned. Its handler must disable automatic
redirects so bearer credentials are never redirected. The SDK-owned handler has
redirects disabled; response bodies are capped at 64 KiB.

## 6. Submit feedback independently of optional gameplay

The session must be pinned to the relevant playtest round by the trusted backend.
Retrieve the configured form before rendering the game UI; the SDK does not
duplicate a developer's questions in game code:

```csharp
var form = await client.GetFeedbackConfigAsync(cancellationToken);
// Render form.Fields and form.SurveyQuestions using your engine's own UI.
// The form also supplies RoundId, BuildLabel, Focus, and AnonymousSupported.
```

`GET /api/connect/v2/feedback/config` uses the session bearer and returns the
game's configured round context, fixed field constraints, and existing campaign
survey questions. It exposes no audience or other-player information. Analytics
sessions without a pinned round cannot submit round feedback. Supplying another
round cannot override the session's pinned context.

```csharp
var feedbackRequest = new WorkflowFeedbackRequest
{
    RequestId = Guid.NewGuid(),
    Rating = 4,
    Liked = "The opening goal was clear.",
    Confused = "I could not find the return route.",
    RoundId = form.RoundId,
    Anonymous = false,
    // Values are entered by the player in the questions returned above.
    Answers = answersEnteredByPlayer
};
var feedback = await client.SubmitFeedbackAsync(feedbackRequest, cancellationToken);
```

`POST /api/connect/v2/feedback` uses only the session bearer. Rating is 1–5;
`liked` and `confused` are each at most 2000 characters. `Answers` is a dictionary
keyed by the exact question text from `SurveyQuestions`: at most five known
questions, question keys up to 200 characters, and answer values up to 1000
characters. The server rejects unknown questions. Retrying the same request
returns the original submission with `duplicate: true`. `Anonymous = true` keeps
the feedback anonymous; Indieable does not attach it to the player's history or
manufacture a signup or marketing consent. The session or explicit round supplies
game/round context and is validated against that game. A feedback-only session
works while optional gameplay remains off. Ordinary web feedback needs no SDK.

## Local validation

```bash
python scripts~/scan_secrets.py --history
python scripts~/validate_package.py
python scripts~/validate_examples.py
dotnet build ci~/CompileCheck/CompileCheck.csproj -c Release
dotnet run --project ci~/CoreSmoke/CoreSmoke.csproj -c Release
dotnet build ci~/WorkflowContract/WorkflowContract.csproj -c Release
dotnet pack DotNet~/Indieable.Sdk/Indieable.Sdk.csproj -c Release -o dist
```

The core smoke checks auth boundaries, snake-case serialization, stable retry
bodies, trust mapping, bounds, timeout/cancellation, nonfatal offline results,
and queue overflow. The Unity compile check uses API stubs and does not replace
a real Unity Editor acceptance pass.

The matching web repository's synthetic journey creates isolated local games,
confirms a synthetic contact, grants its optional gameplay preference through
the player interface, and mints a scoped link proof. It writes an ignored local
fixture file with `base_url`, `server_credential`, `subject`, `audience_link_token`,
`round_id`, optionally `milestone_key` and `other_server_credential`. Run it soon
after minting because the link code expires:

```bash
dotnet run --project ci~/WorkflowContract/WorkflowContract.csproj -c Release -- \
  /workspace/scratch/developer-workflow/sdk-contract.json
```

This harness refuses non-loopback servers. It exercises the actual app for
link/session/events/feedback, configured form retrieval and custom answers,
same-request and same-event retries, trust labels,
anonymous feedback, feedback without telemetry, invalid credentials, and two
owner boundaries. It prints receipts without tokens. Fixture provisioning may
use local administrative setup, but every journey operation uses the supported
HTTP API. No production credential, migration, provider delivery, SDK publication,
or game repository change is performed.
