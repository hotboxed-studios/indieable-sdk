# Indieable SDK development

**SDK in development.** Use Indieable’s HTTP API for current integrations.

Your Indieable deployment's `/docs/api` HTTP API guide explains trusted backend account linking, limited player sessions, optional
activity, configured playtest feedback, consent and retry rules. The application’s
Integrations workspace supplies the matching first-party guide. Website signup,
private playtests, access distribution and ordinary feedback need no SDK.

This repository retains experimental implementation and compatibility source.
It is not the current product’s installation/download path. No Unity, IL2CPP/AOT,
WebGL, Godot or other engine integration is established by C# compilation.
Repository visibility and historical packages have not been changed.

## Maintainer references

- [Experimental .NET 8 implementation](DotNet~/README.md)
- [Development workflow client notes](Documentation~/DEVELOPER-WORKFLOW.md)
- [Legacy setup and compatibility history](Documentation~/LEGACY-SETUP.md)
- [Local acceptance requirements](MANUAL-ACCEPTANCE.md)

The original Unity and v1 clients target `/api/connect/v1`, which the current
application does not implement. The experimental workflow client targets v2 and
.NET 8; it is not included in the legacy UPM archive. No package is published by
this branch, and existing artifacts are retained for history.

Backend credentials belong only on a trusted game backend. Never put one in
browser JavaScript, a client binary, Unity project settings, URLs or logs. Player
management proofs stay in the first-party player flow; clients receive only
their own limited session capabilities. Email is not a universal identity key.

Historical **Stable** and **Nightly** artifacts are retained; those channel names
do not establish support for the current application. A legacy **Public Game Key**
is not a v2 backend credential. An **Indieable Server Secret** never belongs in
a client. **Generic C#**, **GlobalEventBus** and **Event Bus Integration** remain
development/legacy source references, not supported engine setup promises.

Do not publish, change release workflows, or treat local/stub checks as engine
acceptance. The internal workflow core has versioned feedback, consent-context
metadata, bounded in-memory delivery and structured retry timing.
Those local contracts do not establish engine support or a released SDK; use
the HTTP contract for supported setup.
