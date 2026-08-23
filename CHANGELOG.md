# Changelog

All notable changes to Indieable SDKs are documented here.

The packages follow Semantic Versioning. Nightly artifacts use
`X.Y.Z-nightly.YYYYMMDD.RUN` and are not stable API promises.

## [Unreleased]

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

[Unreleased]: https://github.com/hotboxed-studios/indieable-sdk/compare/v0.4.1...HEAD
[0.4.1]: https://github.com/hotboxed-studios/indieable-sdk/compare/v0.4.0...v0.4.1
[0.4.0]: https://github.com/hotboxed-studios/indieable-sdk/releases/tag/v0.4.0
