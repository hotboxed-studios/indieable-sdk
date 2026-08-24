# Releasing Indieable SDKs

## Nightly

The `Nightly` workflow runs after every push to `main`, once each day, and on
manual dispatch. Publication is restricted to `main`.

It:

1. scans the working tree and complete reachable Git history;
2. validates the UPM package, sample scene, and generic C# package;
3. compiles the Unity runtime and Event Bus Integration sample against zero-secret
   CI stubs;
4. builds and smoke-tests the .NET client/event-bus forwarder;
5. creates a version such as `0.4.0-nightly.20260820.42`;
6. replaces the rolling `nightly` prerelease.

Nightly assets include:

```text
indieable-connect-<version>.tgz
indieable-connect-<version>.tgz.sha256
indieable-connect-<version>.json
Indieable.Sdk.<version>.nupkg
```

Nightly does not require repository secrets. Publication uses only the automatic,
short-lived GitHub Actions token with `contents: write`. Checkout credentials are
not persisted.

## Stable

1. Update `package.json` and
   `DotNet~/Indieable.Sdk/Indieable.Sdk.csproj` to the same Semantic Version.
2. Update `CHANGELOG.md`.
3. Run the local validation commands from the root README.
4. Import the generated Unity `.tgz` into Unity 2022.3 and Unity 6.
5. Import **Event Bus Integration**, open its scene, and complete
   `Documentation~/UNITY-SAMPLE-TESTING.md`.
6. Exercise the generic `.nupkg` from a disposable .NET 8 application.
7. Push the matching tag, for example:

   ```bash
   git tag v0.6.0
   git push origin v0.6.0
   ```

The `Release` workflow rejects a tag that does not exactly match both package
versions. Manual Stable publication is restricted to `main`. Existing Stable tags
and releases are immutable and are never replaced.

## Unity package boundary

The UPM tarball is built only from:

```text
Runtime/
Samples~/
README.md
CHANGELOG.md
LICENSE.md
package.json
```

`DotNet~/`, CI, scripts, workflows, repository configuration, local files, and
credentials are not eligible for the Unity archive.

CI-only C# stubs live under `ci~/`, keeping them invisible when Unity installs the
repository root directly as a Git package.

## Generic C# package boundary

`dotnet pack` builds only `DotNet~/Indieable.Sdk`, the linked pure C# sources under
`Runtime/Events`, the generic README, and the MIT license.

The generic package must not contain:

```text
UnityEngine code
Unity samples or scenes
CI stubs
workflows
application/database source
credentials
```

## Manual release gates

Automated compilation is not a real Unity editor. Stable still requires:

- real Unity import and Play Mode;
- imported scene and routing asset validation;
- permission grant/decline/withdraw/reset;
- local event-bus publishing and route selection;
- Preview accepted/rejected/test event checks;
- generic C# smoke in a consuming application;
- account-link, forms, and Challenge checks.
