# Indieable SDK manual acceptance

Automated CI validates the source tree, full Git history, package boundary, Unity
metadata, C# surface, and generated tarball. The checks below are the remaining
high-level acceptance work that requires a real Unity editor, a configured Indieable
Preview game, a browser, and optional external providers.

Use disposable Preview data. Do not point this test at Production.

## Prerequisites

- `preview.indieable.com` is deployed from the intended Preview application branch.
- Preview Supabase migrations for the Player Data Platform have completed.
- The test game has Indieable Connect enabled.
- The test game has a published Player Data privacy manifest with:
  - controller/studio name
  - privacy contact
  - privacy-policy URL
  - general-audience classification for the first test
  - gameplay telemetry enabled
  - diagnostics enabled only when intentionally testing it
  - explicit retention values
- The game has the following test event definition:

  ```text
  Event key: run_completed

  floor       integer
  time_ms     integer
  deaths      integer
  players     integer
  difficulty  string/enum
  ```

- You have the game's Preview Public Game Key. Do not use or paste a Server Secret.

## 1. Download and verify Nightly

1. Open the rolling `nightly` release in this repository.
2. Download:
   - `indieable-connect-<nightly-version>.tgz`
   - the matching `.sha256`
   - the matching `.json` metadata file
3. Verify the SHA-256 locally.
4. Confirm the metadata channel is `nightly` and the commit matches the release notes.

Nightly is for integration testing. Do not treat it as a Stable compatibility promise.

## 2. Unity package import

Run this once in Unity 2022.3 LTS and once in the current Unity 6 project used by the
game.

1. Create or open a disposable project.
2. Open **Window → Package Manager**.
3. Choose **+ → Add package from tarball…**.
4. Select the downloaded `.tgz`.
5. Confirm:
   - `Indieable Connect` appears as an installed package
   - there are no Console compile errors
   - no unexpected package dependencies are added
   - the package exposes **Quick Start** and **UI Toolkit Integration Example** in
     the Samples tab

## 3. Import the UI Toolkit example

1. Import **UI Toolkit Integration Example**.
2. Create an empty GameObject in a test scene.
3. Add `IndieableUIToolkitExample`.
4. Configure:

   ```text
   Base URL: https://preview.indieable.com
   Environment: development
   Public Game Key: <Preview Public Game Key>
   Prompt For Optional Data On Start: enabled
   Send Demo Events As Test: enabled
   ```

5. Enter Play Mode.
6. Open **Indieable Example** from the lower-right corner.

## 4. Pre-permission behavior

Before enabling optional data, confirm:

- the public privacy notice loads
- a short-lived session connects
- identity is `EPHEMERAL_SESSION`
- no `gp_*` reference is displayed
- gameplay telemetry is off
- diagnostics is off
- neither optional toggle is preselected
- **Continue without optional data** and **Save selected choices** have equal visual
  weight and are both keyboard reachable
- declining does not block ordinary gameplay or the example UI

In the Preview database, confirm the pre-permission path did not create a persistent
Installation or Game Player.

## 5. Permission, persistence, withdrawal, and reset

1. Reopen **Review privacy choices**.
2. Enable gameplay telemetry only.
3. Confirm:
   - a game-scoped `gp_*` reference appears
   - diagnostics remains off
   - controller, privacy-policy, and retention copy match the Preview manifest
4. Stop and restart Play Mode.
5. Confirm the same `gp_*` reference resumes while local storage remains present.
6. Disable gameplay telemetry.
7. Attempt to send a production telemetry event and confirm it is rejected without
   interrupting the game.
8. Use **Reset local Indieable identity**.
9. Confirm the next session returns to `EPHEMERAL_SESSION` and the old local
   credential no longer resumes.

## 6. Gameplay event tests

Keep **Send as test event** enabled first.

1. Send a valid `run_completed` event.
2. Confirm the Connect console records acceptance.
3. Confirm the test event does not affect production analytics or Challenge rankings.
4. Change the event key to an unknown value and send it.
5. Enter a value outside the configured schema bounds and send it.
6. Confirm both failures are human-readable and non-fatal to the host game.
7. Restore the valid schema and, only after verifying Preview isolation, test one
   non-test event.
8. Confirm it appears once in the Player telemetry view.

## 7. Account linking

1. Click **Link Indieable account**.
2. Confirm Unity displays only a short code and browser URL.
3. Open the link and authenticate through the normal Indieable browser flow.
4. Approve the game link.
5. Confirm:
   - Unity never receives a password or magic-link secret
   - the session becomes `INDIEABLE_LINKED`
   - optional telemetry/diagnostics choices do not change automatically
   - existing anonymous history is not silently claimed without the explicit history
     decision flow

## 8. Forms

1. Open feedback from the example.
2. Submit a disposable response.
3. Open bug report and submit a disposable report.
4. Confirm in the Preview dashboard:
   - source is `in_game`
   - game/build/platform context is correct
   - trusted identity context comes from the session, not editable form fields
   - the form privacy snapshot is attached
   - no marketing permission or raw-email sharing is inferred

## 9. Community Challenges

1. Keep broad gameplay telemetry off.
2. Request/join a disposable public or unlisted Challenge.
3. Send the exact Metric-required event.
4. Confirm:
   - the requested Challenge feature works independently of broad telemetry consent
   - the leaderboard uses only a game-scoped display identity
   - a better score creates one Personal Record
   - an equal/repeated score does not create another Personal Record
   - private/invite-only authorization remains enforced
   - client-reported and server-verified trust requirements are displayed honestly

## 10. Accessibility and layout

Check the UI Toolkit example with mouse, keyboard, and the game's supported controller
navigation strategy:

- visible focus
- logical tab order
- readable at 200% UI scaling
- no clipped content at approximately 1280×720 and narrow-window layouts
- modal choices remain reachable without a mouse
- color is not the only signal for state or error
- the game is not paused or forced to change `Time.timeScale`

The sample is a reference implementation. A production game should integrate the same
permission semantics into its own tested accessibility/navigation system.

## 11. External delivery and recovery

Using disposable Preview email and Discord destinations:

1. Trigger a qualifying Challenge Personal Record or #1 event.
2. Trigger a Preview announcement to an eligible Audience segment.
3. Confirm gameplay/form requests complete before external delivery.
4. Break the disposable provider destination and confirm bounded retry rather than
   request failure.
5. Restore it and confirm recovery.
6. Confirm routine non-PR gameplay does not create Discord spam.
7. Confirm browser-facing delivery activity exposes no payloads, recipients, tokens,
   webhook URLs, or provider subjects.

## Stable release gate

Do not publish Stable until all of the following are complete:

- this guide passes in the supported Unity versions
- Preview website acceptance passes
- Steam verification is tested with a real disposable ticket/configuration
- the SDK repository has an explicit owner-approved license
- published privacy/cookie notices and developer contractual materials have received
  owner/legal approval
- the production retention, deletion, backup-expiry, minors, incident, and subprocessor
  procedures are approved

After approval, update `package.json` and `CHANGELOG.md`, then push the matching
`vX.Y.Z` tag. The Stable workflow rejects version mismatches automatically.
