# Changelog

All notable changes to Indieable SDKs are documented here.

The packages follow Semantic Versioning. Nightly artifacts use
`X.Y.Z-nightly.YYYYMMDD.RUN` and are not stable API promises.

## [Unreleased]

## [0.6.1] - 2026-08-24

### Changed

- Presented Feedback and Bug Report as larger centered UI Toolkit modals with
  a dimmed backdrop while retaining the Player Data card's visual language.
- Kept the editable Event Bus Integration sample feedback styles aligned with
  the packaged defaults.

### Fixed

- Let a manually opened Player Data card close when its backend notice is
  unavailable, without making the required startup consent prompt dismissible.

## [0.6.0] - 2026-08-24

### Added

- Added an optional `IndieableUiToolkitAssets` override containing explicit
  runtime theme, privacy, feedback, and bug-report UXML/USS references.
- Added editable copies of all support UI assets to the Event Bus Integration
  sample and wired its scene to install them before the first-scene consent
  callback.

### Changed

- Replaced the legacy IMGUI feedback and bug-report windows with themed UI
  Toolkit surfaces.
- Changed Player Data, Feedback, and Bug Report to bounded bottom-right cards
  on transparent pick-through layers.
- Startup consent now has no dismiss-only UI and remains until the Player saves
  or declines; failures remain visible with a retry action.

### Fixed

- Assigned an explicit runtime `ThemeStyleSheet` to SDK-created panels, fixing
  controls that rendered as unstyled white bars in Unity 6.
- Corrected UI Toolkit font-style declarations to Unity's supported prefixed
  USS property so imported styles compile without warnings.

## [0.5.1] - 2026-08-24

### Fixed

- Hid repository-only .NET, documentation, and release-script folders from
  Unity package imports with the standard trailing-tilde convention.
- Added Unity metadata for package-root files so immutable Git installs import
  without missing-meta errors.

## [0.5.0] - 2026-08-24

### Added

- Added SDK-owned Unity startup through `SubsystemRegistration`,
  `BeforeSceneLoad`, and `AfterSceneLoad`, including domain-reload-disabled
  static reset and idempotent initialization.
- Added a built-in UI Toolkit Player Data consent form, versioned one-time
  startup prompting, current UI visibility state/events, and automatic Event
  Bus preference refresh after connection.
- Updated both Unity samples to use project settings and automatic
  initialization instead of owning an SDK bootstrap.

### Changed

- An explicit decline is stored locally for the current notice without
  requiring persistent identity. Dismissed, failed, batch, and headless prompts
  are not recorded as consent decisions.
- The SteamTemplate-facing integration can now observe SDK UI state and retain
  only cursor/input presentation ownership.

### Removed

- Removed the Vercel-specific request-header constants, factory, Project
  Settings preset, tests, and current setup documentation. Generic optional
  request headers remain available without hosting-provider defaults.

## [0.4.2] - 2026-08-23

### Added

- Added validated optional request headers to the Unity and generic .NET
  clients.
- Added a Unity Project Settings button for a Vercel deployment-protection
  bypass header whose value resolves from
  `VERCEL_AUTOMATION_BYPASS_SECRET` instead of a serialized secret.

### Security

- Optional headers reject duplicate or malformed names, newline-bearing
  values, and SDK-owned headers such as Authorization and Content-Type.
- Missing environment variables skip their header without logging its value.

## [0.4.1] - 2026-08-23

### Added

- Added first-class Unity event options for schema version, occurrence time,
  trace type/ID, and shared run ID, matching the generic .NET client.
- Added explicit `Project Settings > Indieable` and
  `Tools > Indieable > Open Settings` authoring for project-owned endpoint,
  Public Game Key, environment, identity, retry, logging, and routing values.
- Added focused Unity Editor tests for correlated event JSON, cloned bus
  context, and the missing-key Preview default.

- Added an engine-agnostic, thread-safe `GameEventBus` and optional process-wide
  `GlobalEventBus`.
- Added `IndieableEventBusBridge` and ScriptableObject routing with Disabled,
  AllowList, DenyList, and All selection modes.
- Added purpose-aware client gating so gameplay telemetry and diagnostics are
  dropped before their current permission exists.
- Added an importable **Event Bus Integration** Unity sample containing a scene,
  routing asset, UI Toolkit permission popup, named GameObjects, typed payloads,
  and independent Door, Workorder, Node, Player, and Run systems.
- Added the engine-agnostic .NET 8 `Indieable.Sdk` client and NuGet-form build
  artifact.
- Added a generic C# event-bus forwarder and core smoke test.

### Changed

- Replaced the repository-owned standalone `UnityExample` project with the normal
  UPM sample import workflow.
- Release workflows now produce the Unity `.tgz` and generic `.nupkg`; they no
  longer package a second Unity project ZIP.
- Expanded package validation to verify the event bus, route defaults, sample
  scene/GameObjects, safe permission defaults, and generic C# package.
- Expanded the zero-secret compile harness to compile the Event Bus Integration
  sample.

### Security

- The local event bus performs no serialization, storage, identity creation, or
  network traffic.
- Optional bus events published before permission are dropped and never replayed
  after a later grant.
- The sample uses an AllowList and test events by default.
- `All` routing still cannot bypass server-side schema, purpose, identity,
  permission, trust, or rate-limit enforcement.

## [0.4.0] - 2026-08-20

### Added

- Standalone Unity Package Manager repository for `com.indieable.sdk`.
- Privacy-manifest and purpose-specific preference APIs.
- Persistent game-scoped Installation continuity without hardware fingerprinting.
- Optional local-profile separation.
- Indieable account and Steam linking surfaces.
- Gameplay-event and explicit telemetry APIs.
- In-game playtest feedback and bug-report APIs with optional runtime UI.
- Community Challenge listing, joining, and leaderboard APIs.
- Stable and Nightly package workflows.
- Full-history secret scanning, strict package allowlisting, package validation,
  and a zero-secret C# compile check.

[Unreleased]: https://github.com/hotboxed-studios/indieable-sdk/compare/v0.6.1...HEAD
[0.6.1]: https://github.com/hotboxed-studios/indieable-sdk/compare/v0.6.0...v0.6.1
[0.6.0]: https://github.com/hotboxed-studios/indieable-sdk/compare/v0.5.1...v0.6.0
[0.5.1]: https://github.com/hotboxed-studios/indieable-sdk/compare/v0.5.0...v0.5.1
[0.5.0]: https://github.com/hotboxed-studios/indieable-sdk/compare/v0.4.2...v0.5.0
[0.4.2]: https://github.com/hotboxed-studios/indieable-sdk/compare/v0.4.1...v0.4.2
[0.4.1]: https://github.com/hotboxed-studios/indieable-sdk/compare/v0.4.0...v0.4.1
[0.4.0]: https://github.com/hotboxed-studios/indieable-sdk/releases/tag/v0.4.0
