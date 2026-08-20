# Unity Example Testing Guide

This guide separates automated SDK validation from the manual Unity/editor and hosted Preview checks that still require a human.

## Automated gates

Every SDK pull request runs full-history secret scanning, strict package validation, Unity GUID validation, C# compilation, example synchronization and permission-default assertions, deterministic package generation, archive allowlist verification, and SHA-256 metadata generation.

The gates prove that `Initialize()` contains no network/session work, the public manifest path creates no identity, optional toggles start false, no startup path grants permission, the permission actions have equal treatment, examples contain only a Public Game Key placeholder, no secret enters source/history/artifacts, no hardware identifier is used, and the package/full-project examples remain identical.

## Manual Unity package acceptance

1. Download the current Nightly `.tgz`.
2. Create a clean supported Unity project.
3. Install through **Window → Package Manager → + → Add package from tarball…**.
4. Confirm there are no Console compile errors.
5. Import **UI Toolkit Integration Lab**.
6. Enter Play Mode and verify the dashboard at desktop and phone-like aspect ratios.
7. Confirm keyboard navigation reaches every input, toggle, and action.
8. Confirm the permission dialog can be closed and reopened and its two actions are equally prominent.

## Hosted Preview acceptance

### No collection before permission

Initialize locally, load the notice, and confirm no session or persistent identity exists. Connect and confirm `EPHEMERAL_SESSION`. Continue without optional data, attempt `run_completed`, and confirm no gameplay event is accepted.

### Telemetry grant and continuity

Enable only Gameplay Telemetry, send `run_completed` with test mode on, restart Play Mode, and confirm the same `gp_*` reference resumes while Diagnostics remains off.

### Withdrawal

Disable Gameplay Telemetry, save, and confirm the next optional event is blocked immediately.

### Shared installation/local profiles

Exercise `slot-a` and `slot-b`, confirm different game-scoped references, return to `slot-a`, and confirm its original reference resumes. Confirm raw local profile labels never appear in developer views.

### Account linking

Complete browser linking and confirm future activity uses the linked Game Player. Confirm old shared-device history is not silently claimed and linking does not turn on telemetry, diagnostics, or marketing.

### Forms

Submit feedback and a bug report. Confirm `in_game` provenance, game/build/platform context, correct Game Player relationship, and no marketing inference.

### Community Challenges

Keep broad telemetry off, explicitly join a test Challenge, submit the approved metric fact, improve the score, and confirm one Personal Record. Verify private/invite authorization and no duplicate PR on equal score.

### Reset and privacy controls

Reset local identity, restart, and confirm the next session is ephemeral. Exercise anonymous and linked privacy portals using disposable Preview data only.

## Website acceptance

Verify dashboard navigation/deep links, privacy manifest editor, Events & Metrics, telemetry timelines, Audience deduplication/contact labels, all Challenge visibility modes, email/Discord retries, public SDK links, mobile layout, keyboard navigation, and legal-page drift.

## Stable release decision

Stable requires all automated gates, real Unity import, Play Mode smoke, hosted Preview permission/withdrawal, test-event, account-link, forms, and Challenge round trips, plus owner sign-off on legal/data-practice release blockers.
