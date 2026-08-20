# Changelog

All notable changes to the Indieable Unity SDK are documented here.

The package follows Semantic Versioning. Nightly packages use the prerelease form
`X.Y.Z-nightly.YYYYMMDD.RUN` and are not stable API promises.

## [Unreleased]

### Added

- Added the importable **UI Toolkit Integration Lab** sample.
- Added a minimal `UnityExample` project that references the package root and opens
  the integration dashboard in an empty scene.
- Added a balanced optional-permission popup with gameplay telemetry and diagnostics
  disabled by default.
- Added Preview controls for `run_completed`, account linking, playtest feedback,
  bug reports, Community Challenges, leaderboards, and local identity reset.
- Added a complete automated/manual Unity acceptance guide.
- Nightly and Stable releases now include a self-contained UnityExample ZIP.

### Changed

- Expanded package CI to verify UI Toolkit example synchronization, optional-purpose
  defaults, equal permission-action treatment, and the full example project boundary.
- Expanded the zero-secret compile harness to compile the UI Toolkit sample.
- Moved the compile-only Unity API stubs under `ci~/` so direct Git/local UPM installs
  never import the fake `UnityEngine` surface.
- Pinned GitHub Actions to reviewed commit SHAs and disabled persisted checkout
  credentials.
- Stable release publication is immutable and manual publishing is restricted to
  `main`.
- Added an MIT license to the repository and packaged SDK artifacts.

### Security

- Optional gameplay telemetry and diagnostics are never preselected in the example.
- Reject non-HTTPS remote `BaseUrl` values; plain HTTP remains available only for
  loopback development endpoints.
- Expanded secret scanning and ignored credential-file coverage.

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
- Full-history secret scanning, strict package allowlisting, package validation, and a
  zero-secret C# compile check.

[Unreleased]: https://github.com/hotboxed-studios/indieable-sdk/compare/v0.4.0...HEAD
[0.4.0]: https://github.com/hotboxed-studios/indieable-sdk/releases/tag/v0.4.0
