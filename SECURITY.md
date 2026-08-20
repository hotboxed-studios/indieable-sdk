# Security Policy

## Reporting a vulnerability

Use this repository's private GitHub security-advisory flow for suspected
vulnerabilities. Do not include credentials, tokens, private keys, Player data, or
production request payloads in a normal issue or pull request.

A useful report contains:

- affected SDK version or commit
- affected Unity version and platform
- minimal reproduction steps using placeholder credentials
- expected and observed behavior
- impact and any suggested mitigation

## Secret handling

This repository must not contain or distribute secrets. The only credential type
intended to be configured in a Unity client is an Indieable **Public Game Key**.

Never commit or package:

- Indieable Server Secrets
- database URLs containing credentials or Supabase service-role keys
- Steam publisher/Web API keys
- Discord webhooks or bot tokens
- OAuth client secrets or refresh tokens
- signing keys, certificates, keystores, or `.env` files
- captured runtime session or Installation credentials

GitHub Actions requires no configured repository secrets. Release publication uses the
short-lived, repository-scoped token supplied automatically to that workflow job; it is
not written into source, artifacts, logs, or Git history.

CI scans the current tree and every reachable historical blob for common secret
formats. Release archives are built from a strict package allowlist.

## Supported versions

Until the first Stable release is published, only the latest Nightly is evaluated for
security fixes. After Stable publication, the latest Stable and latest Nightly are
supported.
