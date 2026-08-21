# Indieable SDK manual acceptance

This is the remaining hands-on acceptance plan for the Player Data Platform and the
standalone Indieable SDK. Automated checks validate source, migrations, package
boundaries, Unity metadata, compilation, event-bus behavior, generic .NET builds,
secret scanning, and generated artifacts. The checks below require a real Unity
editor, a deployed Indieable Preview stack, a browser, and selected external
providers.

Use disposable Preview data. Do not point these tests at Production.

## Release candidate under test

The application candidate is the cumulative top of the Player Data stack:

```text
#53  Player identity and privacy core
└── #54  persistent anonymous Installation + SDK privacy
    └── #55  account/provider linking, history claims, privacy portals
        └── #56  longitudinal gameplay telemetry
            └── #58  identity-aware Community Challenges
```

The Unity client is the standalone `com.indieable.sdk` package from this repository.
The package includes:

```text
Quick Start
Event Bus Integration
```

`Event Bus Integration` is the primary acceptance sample. It contains a real scene,
a routing asset, authored GameObjects, gameplay-system scripts that publish only to
`GlobalEventBus`, and an `IndieableEventBusBridge` that decides what reaches
Indieable.

## Required test matrix

Complete the full critical path in both supported Unity generations:

| Target | Required coverage |
| --- | --- |
| Unity 2022.3 LTS | Clean project, package import, sample import, compile, Play Mode |
| Current Unity 6 editor used by UwUzon | Existing-project import, assembly compatibility, Play Mode, input/cursor behavior |
| .NET 8 console app | Generic NuGet package restore, connect, manifest, event-bus forwarder smoke |
| Indieable Preview | Database migrations, browser flows, dashboards, Challenge evaluation, rights flows |

Record the exact Unity version, SDK artifact version, application commit, database
migration head, operating system, and tester for every run.

## Prerequisites

Before opening Unity:

- deploy the intended cumulative Indieable application branch to Preview;
- apply every migration through the identity-aware Challenge migration;
- confirm the Preview deployment and database use the same stack head;
- create or reset a disposable general-audience test game;
- enable Indieable Connect for that game;
- publish a Player Data privacy manifest containing:
  - controller/studio name;
  - privacy contact;
  - game privacy-policy URL;
  - general-audience classification;
  - gameplay telemetry enabled with explicit retention;
  - diagnostics enabled only when intentionally testing it;
  - Community Challenge operation enabled;
- obtain the game's Preview **Public Game Key**;
- never place a Server Secret in Unity, the sample, screenshots, logs, or test notes.

Register exact Preview event schemas for the sample:

```text
door_opened
  door_id       bounded string
  method        bounded string or enum
  open_count    integer

workorder_done
  workorder_id  bounded string
  node_id       bounded string
  duration_ms   integer

node_closed
  node_id       bounded string
  outcome       bounded string or enum
  elapsed_ms    integer

player_died
  cause         bounded string or enum
  room_id       bounded string
  run_number    integer

run_completed
  floor         integer
  time_ms       integer
  deaths        integer
  players       integer
```

Create disposable Challenge fixtures covering:

- one public Challenge;
- one anyone-with-link/unlisted Challenge;
- one private Challenge;
- one invite-only Challenge;
- one highest-wins Metric;
- one lowest-wins Metric where practical;
- one Challenge accepting `CLIENT_REPORTED` scores;
- one Challenge requiring `SERVER_VERIFIED` scores.

Prepare disposable accounts and destinations:

- an Indieable account for linking;
- a second account for isolation/conflict testing;
- a Steam account and valid test configuration when Steam acceptance is in scope;
- a Discord webhook or Discord identity fixture;
- disposable email recipients;
- one anonymous browser profile and one linked browser profile.

# Phase A — artifact and package integrity

## A1. Download the candidate

1. Download the rolling Nightly Unity `.tgz` from the SDK release.
2. Download the matching `.sha256` and metadata JSON.
3. Download the generic `.nupkg` artifact when testing the .NET client.
4. Verify the SHA-256 locally.
5. Confirm the metadata:
   - channel is `nightly`;
   - package version matches the file name;
   - commit matches the release notes;
   - no server or provider secret is present.

Nightly is an explicit testing channel. Do not treat it as a Stable compatibility
promise.

## A2. Unity Package Manager import

Perform this first in an empty project, then in the current UwUzon project.

1. Open **Window → Package Manager**.
2. Choose **+ → Add package from tarball…**.
3. Select the candidate `.tgz`.
4. Confirm:
   - `Indieable Connect` appears once;
   - no Console compilation errors appear;
   - no unexpected runtime dependencies are added;
   - `Quick Start` is listed;
   - `Event Bus Integration` is listed;
   - package removal cleanly removes package-owned code without deleting host assets.

## A3. Import the Event Bus sample

1. Import **Event Bus Integration** from the Samples section.
2. Open:

   ```text
   Scenes/IndieableEventBusSample.unity
   ```

3. Verify the authored scene contains:

   ```text
   Indieable SDK
   Sample Gameplay Systems
   ├── Run System
   ├── Door
   ├── Workorder Terminal
   ├── Tunnel Node
   └── Player Lifecycle
   ```

4. Inspect `SampleEventRouting.asset`.
5. Confirm the default is an allow-list and routes are test events by default.
6. Confirm gameplay scripts reference `GlobalEventBus`, not `Indieable.SendEvent` or
   `IndieableTelemetry.Send`.
7. Enter Play Mode and confirm the sample UI opens without runtime-created scene
   replacement or missing-reference warnings.

# Phase B — local-only and pre-permission contract

These checks are stop-ship requirements.

## B1. Initial scene load

On first launch with Indieable local storage removed:

- loading the scene creates no persistent Installation;
- loading the scene creates no Game Player;
- loading the scene sends no gameplay event;
- optional telemetry and diagnostics are both off;
- ordinary local gameplay systems remain usable.

## B2. Initialize

Call Initialize from the sample and confirm:

- initialization is local and side-effect-free;
- no gameplay event is transmitted;
- no telemetry-specific persistent identifier is created;
- invalid remote HTTP URLs are rejected;
- Preview HTTPS and loopback-development HTTP rules behave as documented.

## B3. Public privacy manifest

Load the public manifest before Connect and confirm:

- the notice loads without a Connect session;
- no Installation or Game Player is created;
- controller, privacy contact, policy URL, notice version, purposes, and retention
  match Preview;
- an unpublished or invalid manifest produces a bounded non-fatal error.

## B4. Ephemeral Connect

Connect without granting a persistent purpose and confirm:

- identity state is `EPHEMERAL_SESSION`;
- `persistent_identity` is false;
- no `gp_*` reference is presented as a persistent Player;
- telemetry and diagnostics remain off;
- closing and reopening Play Mode does not resume an anonymous persistent identity.

## B5. Local events before permission

Fire every sample gameplay action before optional permission:

- Door Opened;
- Workorder Done;
- Node Closed;
- Player Died;
- Run Completed.

Confirm:

- the local event-bus activity log receives each event;
- other local subscribers continue to receive each event;
- the bridge reports a permission/session drop where applicable;
- no optional event reaches Indieable;
- no dropped event is buffered;
- granting permission later does **not** replay any earlier event.

# Phase C — GlobalEventBus and bridge routing

## C1. Local bus independence

With Indieable uninitialized or disconnected:

- publish named events;
- publish typed payloads;
- subscribe to one name;
- subscribe to all events;
- dispose one subscription;
- throw deliberately from one disposable test subscriber.

Confirm:

- publication remains synchronous and local;
- the disposed subscriber stops receiving events;
- one failing subscriber does not stop gameplay or other subscribers;
- no network activity occurs merely because the bus is used.

## C2. Routing modes

Exercise all routing modes:

| Mode | Expected result |
| --- | --- |
| Disabled | No local event is forwarded |
| AllowList | Only configured routes marked Forward are eligible |
| DenyList | Matching blocked routes are dropped; unmatched routes are eligible |
| All | Every local event is considered, but server schemas still decide acceptance |

For each mode, verify the local source name, mapped Indieable event key, purpose, test
flag, and drop/forward log.

## C3. Mapping and serializer behavior

- rename one local event to a different registered Indieable key;
- publish a `[Serializable]` public-field payload;
- publish an already serialized JSON object string;
- configure a temporary custom serializer;
- return an array or primitive from the custom serializer.

Confirm valid objects forward and invalid non-object JSON is rejected locally without
breaking gameplay.

## C4. Direct API remains optional

Send one reserved Connect test through the direct SDK API and confirm direct
`Indieable.SendEvent(...)` / `IndieableTelemetry.Send(...)` usage still works without
the event bus. The bus is a recommended decoupling pattern, not a mandatory host-game
architecture.

# Phase D — permission, persistence, withdrawal, profiles, and reset

## D1. Balanced permission UI

Open the permission UI and confirm:

- neither optional purpose is preselected;
- decline and affirmative actions have equal visual weight;
- each purpose has separate copy and current state;
- account linking, Challenges, forms, and marketing are not presented as telemetry
  permission;
- keyboard/controller focus can reach every action.

## D2. Grant gameplay telemetry only

Enable gameplay telemetry while leaving diagnostics off. Confirm:

- a random game/environment-scoped Installation is created;
- a game-scoped `gp_*` reference is created;
- current session binds Installation → Game Player;
- diagnostics remains off;
- the permission event and processing authority are purpose-specific;
- the client cannot choose trusted identity IDs or permission receipt IDs.

## D3. Persistence

Stop and restart Play Mode. Confirm:

- the same Installation resumes while local storage survives;
- the same Game Player resumes for the same local profile;
- a different Preview/Production environment cannot reuse the credential;
- a different game cannot reuse the credential.

## D4. Local profiles

Set an opaque local profile reference for Profile A, then Profile B.

Confirm:

- A and B resolve to different Game Players on the same Installation;
- returning to A resumes A;
- raw profile labels are not exposed or stored as human identity;
- blank/default profile behavior is deterministic;
- profile changes do not grant permissions that were not granted for the effective
  Player/purpose.

## D5. Withdrawal

Withdraw gameplay telemetry and immediately fire every sample event. Confirm:

- the next optional telemetry event is rejected/dropped;
- the host game continues;
- diagnostics remains independently unchanged;
- withdrawal does not delete Challenge membership by implication;
- no hidden pre-withdrawal queue is flushed afterward.

## D6. Reset local identity

Use Reset Local Identity and confirm:

- the Installation is revoked server-side;
- active authorities and sessions are revoked as designed;
- local credential storage is cleared;
- the next Connect is ephemeral;
- the old credential cannot resume after being restored manually.

# Phase E — telemetry schema, context, idempotency, and retention

## E1. Test events

Send each sample event as test data. Confirm:

- exact valid schemas are accepted;
- test events are visible in integration activity;
- test events do not change production telemetry projections;
- test events do not change Challenge rankings or Personal Records.

## E2. Production-valid events

After verifying Preview isolation, send one non-test event for every registered key.
Confirm each appears once under the correct pairwise Game Player and includes the
expected build/platform/session context.

## E3. Default-deny schema failures

Attempt all of the following:

- unknown event key;
- disabled event key;
- unknown field;
- missing required field;
- wrong type;
- invalid enum;
- value below minimum;
- value above maximum;
- oversized string;
- nested object;
- array;
- null where disallowed;
- payload over the request limit;
- obvious direct-identifier field key.

Confirm every failure is human-readable, non-fatal, and stores no accepted gameplay
fact.

## E4. Idempotency

Send the exact same event twice with the same idempotency key. Confirm:

- one event row exists;
- one projection update occurs;
- one Challenge evaluation occurs;
- retries report duplicate/safe acceptance rather than creating another fact.

Send the same payload with a different idempotency key and confirm it is treated as a
separate event.

## E5. Time and correlation context

Where exposed by the selected SDK surface, verify occurred time, trace/run context,
schema version, test mode, and idempotency context. Confirm the server derives Game
Player, Installation, permission, identity trust, and event trust rather than trusting
client-supplied internal IDs.

## E6. Retention and erasure

Using disposable fixtures:

- expire an event under its configured retention policy;
- execute a Game Player erasure path;
- rebuild affected projections;
- verify Challenge/analytics behavior after deletion;
- confirm ordinary append-only operation does not mean legally undeletable data;
- confirm the erasure/backup-expiry audit contains no unnecessary personal payload.

# Phase F — identity linking, history claims, and rights

## F1. Indieable account linking

Start from a persistent anonymous Game Player, then link an Indieable account.
Confirm:

- Unity sees only the short-lived browser/device flow data;
- passwords, magic-link secrets, provider tokens, and canonical IDs never enter Unity;
- future sessions use the linked identity state;
- optional telemetry and diagnostics choices do not broaden automatically;
- prior anonymous history is not silently claimed.

## F2. Explicit history claim

Exercise approve and decline on the generated history candidate. Confirm:

- approval creates the bounded alias/audit relationship;
- decline preserves separate history;
- raw historical event rows are not rewritten;
- a shared-device scenario does not claim ambiguous history automatically.

## F3. Provider conflict

Use a second account to create a Steam/provider ownership conflict. Confirm the user
must explicitly choose, the provider subject stays private, unrelated identities are
not merged, and cancellation changes nothing.

## F4. Anonymous privacy portal

Request an anonymous management link and confirm:

- token is high entropy, one-time/short-lived, and game-scoped;
- only the intended Installation/Game Player is visible;
- provider subjects and Canonical Player IDs are absent;
- expired/reused tokens fail safely;
- the user can withdraw optional telemetry without creating an Indieable account.

## F5. Controller-aware rights routing

Submit requests spanning:

- Indieable-controlled identity/community data;
- developer-controlled telemetry/forms;
- mixed domains.

Confirm each routes to the correct Indieable-processing, controller-instruction, or
review state rather than implying Indieable owns every data domain.

# Phase G — Community Challenges

Run these checks with broad gameplay telemetry off unless a test explicitly requires
it. Challenge operation must remain a separate requested feature purpose.

## G1. Visibility and membership

Verify:

- public discovery and join;
- anyone-with-link/unlisted direct join;
- private request and moderator approval/rejection;
- invite-only denial without a valid invite;
- invite generation, redemption, expiry, and revocation;
- non-members cannot enumerate private entries, Metrics, requests, or membership.

## G2. Pairwise identity boundary

Confirm leaderboard and membership responses expose only game-scoped safe display
identity. They must not expose Canonical Player UUIDs, Steam IDs, Discord IDs, emails,
auth UUIDs, provider subjects, session tokens, or activity from another game.

## G3. Scoring semantics

For highest-wins and lowest-wins fixtures, verify:

- first valid score creates one entry and one Personal Record;
- a real improvement updates the entry and adds one Personal Record;
- an equal score creates no Personal Record;
- a worse score changes nothing;
- one event can update multiple eligible Challenges;
- filters and date windows are enforced;
- idempotent retries do not duplicate evaluation;
- ties resolve deterministically by configured ordering rules.

## G4. Trust policy

- send one `CLIENT_REPORTED` event to a Challenge that accepts it;
- confirm it scores;
- send one `CLIENT_REPORTED` event to a `SERVER_VERIFIED`-minimum Challenge;
- confirm it does not score;
- send the equivalent trusted-server event through the server-only path;
- confirm it scores without exposing a Server Secret to Unity.

## G5. Telemetry independence

Confirm joining or scoring a Challenge:

- does not enable broad gameplay telemetry;
- does not enable diagnostics;
- does not grant marketing permission;
- stores only the Metric event required for Challenge operation under the correct
  purpose.

# Phase H — forms and playtesting

## H1. Feedback

Submit in-game feedback and confirm:

- source is `in_game`;
- campaign/round/build/platform/session provenance is server-resolved;
- configured survey questions render correctly;
- no fake signup, email, or username is created;
- no marketing permission is inferred.

## H2. Bug report

Submit a disposable bug report and confirm it reaches the existing developer workflow
with the correct game/build context and no gameplay log, screenshot, or unrelated
telemetry attached unless that is a separately disclosed feature.

## H3. Private playtest authorization

Where configured, verify anonymous public submission and approved-linked private
submission behavior, including denial for an unapproved Player.

# Phase I — external delivery and recovery

Use disposable destinations.

## I1. Discord

Trigger:

- New Member;
- Personal Record;
- New #1;
- Challenge Started;
- Challenge Completed.

Confirm routine non-PR gameplay produces no Discord work, one improvement emits either
New #1 or Personal Record rather than both, mention behavior is controlled, and no
provider subject, Player UUID, raw payload, token, or webhook URL is exposed.

## I2. Failure isolation

Break the disposable Discord destination and confirm:

- gameplay ingestion succeeds;
- Challenge evaluation succeeds;
- membership succeeds;
- a durable job records the delivery intent;
- bounded retry occurs;
- the job eventually stops or dead-letters according to policy.

Restore the destination and verify recovery without duplicate delivery.

## I3. Email and Audience

Where the cumulative stack includes Audience delivery, confirm Challenge/follow/
playtest relationships do not imply email consent. Recipient calculation must still
require explicit marketing consent, deduplicate addresses, and honor unsubscribe and
suppression state.

# Phase J — generic .NET client

Create a clean .NET 8 console application and install the candidate `.nupkg`.

Confirm:

- package restore succeeds without Unity assemblies;
- public manifest retrieval works before Connect;
- ephemeral Connect works;
- in-memory identity storage works;
- file identity storage persists and resumes;
- purpose grant/withdrawal works;
- the generic event-bus forwarder sends only configured events;
- cancellation and HTTP failures remain bounded;
- account linking, feedback, bug reports, Challenges, and leaderboard calls deserialize
  successfully against Preview.

Do not use the .NET client as a trusted game server merely because it is not Unity.
Server verification still requires the server-only credential and authority boundary.

# Phase K — host-game behavior, accessibility, and performance

## K1. Host-game behavior

In UwUzon, confirm the SDK/sample patterns do not:

- replace authored scenes;
- change `Time.timeScale`;
- capture gameplay input while SDK UI is closed;
- permanently change cursor lock/visibility;
- block scene unload or domain reload;
- create duplicate runtime objects after scene changes;
- turn Indieable failures into gameplay failures.

## K2. Accessibility and layout

Test with mouse, keyboard, and the game's supported controller-navigation strategy:

- visible focus;
- logical tab order;
- no keyboard trap;
- readable at 200% UI scaling;
- no clipped content near 1280×720;
- useful behavior in a narrow Game view;
- modal actions reachable without a mouse;
- color is not the only state/error signal;
- decline is not visually or operationally obstructed.

## K3. Event volume and queue behavior

In a disposable build, publish a bounded burst of local events and confirm:

- local gameplay remains responsive;
- worker-thread handoff stays within configured bounds;
- queue overflow drops safely rather than growing without limit;
- bridge logging does not flood Production by default;
- rate limiting produces bounded failures without retries multiplying the event count.

# Security and privacy regression sweep

Before sign-off, explicitly verify:

- no hardware or advertising identifier is used;
- one Installation is never described as one confirmed human;
- the same Canonical Player receives different `gp_*` references in different games;
- developers cannot query Canonical Player or provider identity tables directly;
- no Discord/Steam/email/auth identifier is copied into gameplay events;
- ordinary telemetry has default-deny schemas and no arbitrary nested JSON;
- optional telemetry sends nothing before current permission allows it;
- identity linking, Challenges, forms, and marketing remain separate purposes;
- no child-directed/mixed-audience persistent features can be enabled in v1;
- transport/application logs follow their documented retention policy;
- published UI and policy wording match observed SDK behavior.

# Evidence package

For each editor/environment combination, retain:

- completed checklist with tester/date;
- Unity Editor and SDK versions;
- application and SDK commit SHAs;
- migration head;
- package checksum;
- screenshots of the imported sample hierarchy and routing Inspector;
- screenshots of permission grant and decline states;
- redacted Preview activity, telemetry, rights, and leaderboard evidence;
- expected-failure evidence for schema, permission, visibility, and trust tests;
- external-delivery/retry evidence;
- a list of defects with severity, reproduction steps, and owning repository.

Never attach credentials, complete browser/device codes, provider subjects, private
Player IDs, raw webhook URLs, or user-generated personal content to the evidence
package.

# Stop-ship conditions

Do not merge/release when any of these occurs:

- migration rebuild or behavior suite fails for a product defect;
- Unity import or sample compilation fails in either supported editor generation;
- Initialize creates network identity or optional telemetry side effects;
- an optional event reaches Indieable before permission;
- withdrawn telemetry continues to be accepted;
- earlier dropped events replay after permission;
- a Canonical Player or provider subject is exposed to a developer/client;
- Challenge participation enables broad telemetry or marketing;
- private/invite-only Challenge data is enumerable by a non-member;
- client-reported data satisfies a server-verified trust requirement;
- identity verification silently claims ambiguous history;
- erasure leaves active projections claiming deleted subject data;
- external delivery failure causes gameplay or Challenge evaluation failure;
- the package contains a server-side secret or private application source.

# Sign-off and release order

1. Complete the automated application and SDK checks.
2. Complete this guide against Preview.
3. Record and resolve every stop-ship defect.
4. Obtain owner approval for the acceptance evidence.
5. Obtain the required policy/privacy/contract review before Production.
6. Merge the application stack bottom-to-top.
7. Merge this SDK documentation/release-readiness PR.
8. Update SDK version and changelog for the intended Stable release.
9. Rebuild and re-import the exact Stable `.tgz`.
10. Publish the matching immutable `vX.Y.Z` release only after the final smoke pass.
