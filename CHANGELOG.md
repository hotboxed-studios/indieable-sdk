# Changelog

All notable changes to the Indieable Unity SDK are documented here.

The package follows Semantic Versioning. Nightly packages use the prerelease form
`X.Y.Z-nightly.YYYYMMDD.RUN` and are not stable API promises.

## [Unreleased]

### Changed

- Moved the compile-only Unity API stubs under `ci~/` so direct Git/local UPM installs
  never import the fake `UnityEngine` surface.
- Pinned GitHub Actions to reviewed commit SHAs and disabled persisted checkout
  credentials.
- Stable release publication is immutable and manual publishing is restricted to
  `main`.
- Added an MIT license to the repository and packaged SDK artifacts.

### Security

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
