# UI Toolkit Integration Lab

This sample is a runtime UI Toolkit dashboard for exercising the public Indieable
Unity SDK against a Preview game.

It demonstrates:

- local, side-effect-free SDK initialization;
- public privacy-manifest loading before a session exists;
- an ephemeral Connect session;
- a balanced optional-permission popup;
- gameplay telemetry and diagnostics **off by default**;
- persistent game-scoped Installation continuity only after an affirmative choice;
- `run_completed` test-event submission;
- account linking, playtest feedback, bug reporting, and Community Challenges;
- local identity reset.

## Import and run

1. In Unity Package Manager, select **Indieable Connect**.
2. Import **UI Toolkit Integration Lab** from the Samples tab.
3. Enter Play Mode in any scene.

The sample uses `RuntimeInitializeOnLoadMethod` and creates its own `UIDocument`, so
it does not require a prefab, Canvas, or scene object.

Enter:

```text
Base URL:
https://preview.indieable.com

Public Game Key:
the client-safe ind_pub_... key from the game's Connect dashboard
```

Then use:

```text
Initialize locally
→ Load privacy notice
→ Connect
→ Review optional Player Data permissions
```

The two optional choices are deliberately unselected. A previously saved choice is
shown when the same Installation resumes, but the sample never broadens it.

## Event setup

The sample's `run_completed` form sends:

```text
floor       integer
time_ms     integer
deaths      integer
players     integer
```

The Preview game must have a matching enabled event definition. **Send as test event**
starts on so manual checks cannot alter production rankings or analytics projections.

## Credential boundary

Only the Public Game Key belongs in the sample. Never paste an Indieable Server
Secret, Supabase credential, Steam publisher key, Discord credential, OAuth secret,
private key, or captured session/Installation credential into Unity or source control.

## Accessibility and host-game behavior

The permission dialog uses equal-size actions:

```text
Continue without optional data
Allow selected
```

Optional toggles are not preselected. The UI does not pause the game, change
`Time.timeScale`, or take over input maps. A production game should decide how to
coordinate cursor mode, controls, and pausing when opening any Indieable surface.
