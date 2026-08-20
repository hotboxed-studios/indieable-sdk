# Releasing Indieable Connect

## Nightly

The `Nightly` workflow runs after every push to `main`, once each day, and on manual
dispatch. It validates the complete Git history, compiles the public C# surface against
CI stubs, creates a version such as `0.4.0-nightly.20260820.42`, and replaces the
rolling `nightly` prerelease.

Nightly does not require repository secrets. Publication uses only the automatic,
short-lived GitHub Actions token with `contents: write` for that job.

## Stable

1. Update `package.json` to the intended Semantic Version.
2. Update `CHANGELOG.md`.
3. Run the local validation commands from the root README.
4. Import the resulting `.tgz` into a supported Unity editor and complete a Play Mode
   smoke test.
5. Push the matching tag, for example:

   ```bash
   git tag v0.4.0
   git push origin v0.4.0
   ```

The `Release` workflow rejects a tag that does not exactly match `package.json`. A
successful run creates or refreshes the GitHub release assets and checksums.

## Package boundary

Release archives are built only from:

```text
Runtime/
Samples~/
README.md
CHANGELOG.md
package.json
```

CI, scripts, repository configuration, local files, and credentials are not eligible
for packaging. Do not bypass this allowlist.
