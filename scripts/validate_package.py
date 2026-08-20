#!/usr/bin/env python3
"""Validate the standalone Unity Package Manager source tree."""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

PACKAGE_NAME = "com.indieable.sdk"
EXPECTED_PACKAGE_FILES = ["Runtime", "Samples~", "README.md", "CHANGELOG.md"]
REQUIRED_PATHS = [
    "package.json",
    "README.md",
    "CHANGELOG.md",
    "Runtime/Indieable.Runtime.asmdef",
    "Runtime/Indieable.cs",
    "Runtime/IndieableClient.cs",
    "Runtime/IndieableFeedbackUI.cs",
    "Runtime/IndieableIdentityStorage.cs",
    "Runtime/IndieableModels.cs",
    "Runtime/IndieablePrivacyUI.cs",
    "Runtime/IndieableRuntime.cs",
    "Runtime/IndieableTelemetry.cs",
    "Runtime/Steam/IIndieableSteamTicketProvider.cs",
    "Samples~/QuickStart/IndieableQuickStart.cs",
    "Samples~/QuickStart/README.md",
]
VERSION_RE = re.compile(
    r"^\d+\.\d+\.\d+(?:-nightly\.\d{8}\.\d+)?$"
)
GUID_RE = re.compile(r"^guid:\s*([0-9a-f]{32})\s*$", re.MULTILINE)
SDK_VERSION_RE = re.compile(
    r'public\s+string\s+SdkVersion\s*=\s*"([^"]+)"\s*;'
)
FORBIDDEN_RUNTIME_TEXT = {
    "SystemInfo.deviceUniqueIdentifier": "Unity device fingerprinting",
    "SUPABASE_SERVICE_ROLE_KEY": "Supabase service-role credential",
    "service_role": "Supabase service-role boundary",
    "SERVER_SECRET": "server secret",
    "CRON_SECRET": "cron secret",
    "STEAM_WEB_API_KEY": "Steam publisher key",
    "DISCORD_WEBHOOK_URL": "Discord webhook",
    "BEGIN PRIVATE KEY": "private key",
    "BEGIN RSA PRIVATE KEY": "private key",
}


def load_json(path: Path, errors: list[str]) -> dict:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError:
        errors.append(f"missing JSON file: {path}")
        return {}
    except json.JSONDecodeError as exc:
        errors.append(f"invalid JSON in {path}: {exc}")
        return {}
    if not isinstance(value, dict):
        errors.append(f"JSON root must be an object: {path}")
        return {}
    return value


def require_meta(asset: Path, root: Path, errors: list[str], guids: dict[str, Path]) -> None:
    meta = Path(str(asset) + ".meta")
    if not meta.is_file():
        errors.append(f"missing Unity .meta file: {meta.relative_to(root)}")
        return
    text = meta.read_text(encoding="utf-8", errors="replace")
    match = GUID_RE.search(text)
    if not match:
        errors.append(f"missing or invalid Unity GUID: {meta.relative_to(root)}")
        return
    guid = match.group(1)
    previous = guids.get(guid)
    if previous is not None:
        errors.append(
            f"duplicate Unity GUID {guid}: {previous.relative_to(root)} and {meta.relative_to(root)}"
        )
    else:
        guids[guid] = meta


def validate(root: Path) -> list[str]:
    root = root.resolve()
    errors: list[str] = []

    manifest = load_json(root / "package.json", errors)
    if manifest.get("name") != PACKAGE_NAME:
        errors.append(f"package name must be {PACKAGE_NAME!r}")

    version = str(manifest.get("version", ""))
    if not VERSION_RE.fullmatch(version):
        errors.append(f"invalid package version: {version!r}")

    if manifest.get("unity") != "2022.3":
        errors.append("package.json unity baseline must remain 2022.3")

    if manifest.get("files") != EXPECTED_PACKAGE_FILES:
        errors.append(
            "package.json files must be the exact public-package allowlist: "
            + repr(EXPECTED_PACKAGE_FILES)
        )

    dependencies = manifest.get("dependencies", {})
    if dependencies not in ({}, None):
        errors.append("the SDK package must remain dependency-free unless explicitly reviewed")

    for relative in REQUIRED_PATHS:
        if not (root / relative).is_file():
            errors.append(f"missing required package file: {relative}")

    for path in root.rglob("*"):
        if path.is_symlink():
            errors.append(f"symlinks are not allowed: {path.relative_to(root)}")

    asmdef = load_json(root / "Runtime/Indieable.Runtime.asmdef", errors)
    if asmdef.get("name") != "Indieable.Runtime":
        errors.append("runtime asmdef name must be Indieable.Runtime")
    if asmdef.get("allowUnsafeCode") is not False:
        errors.append("runtime asmdef must not enable unsafe code")
    if asmdef.get("references") not in ([], None):
        errors.append("runtime asmdef must remain free of package references")

    runtime_files = sorted((root / "Runtime").rglob("*.cs"))
    runtime_text = "\n".join(
        file.read_text(encoding="utf-8", errors="replace") for file in runtime_files
    )
    for token, meaning in FORBIDDEN_RUNTIME_TEXT.items():
        if token in runtime_text:
            errors.append(f"runtime contains forbidden {meaning}: {token}")

    if "SystemInfo" in runtime_text and "deviceUniqueIdentifier" in runtime_text:
        errors.append("runtime must not use Unity hardware/device identifiers")

    models_path = root / "Runtime/IndieableModels.cs"
    if models_path.is_file() and version:
        match = SDK_VERSION_RE.search(models_path.read_text(encoding="utf-8"))
        expected_sdk_version = f"unity-{version}"
        if not match:
            errors.append("could not locate IndieableOptions.SdkVersion")
        elif match.group(1) != expected_sdk_version:
            errors.append(
                f"IndieableOptions.SdkVersion must be {expected_sdk_version!r}, "
                f"found {match.group(1)!r}"
            )

    sample_path = root / "Samples~/QuickStart/IndieableQuickStart.cs"
    if sample_path.is_file():
        sample_text = sample_path.read_text(encoding="utf-8")
        if 'publicGameKey = "ind_pub_replace_me"' not in sample_text:
            errors.append("Quick Start must contain only the documented placeholder Public Game Key")
        if re.search(r'ind_(?:sec|srv)_[A-Za-z0-9_-]+', sample_text):
            errors.append("Quick Start contains a server-side Indieable credential")

    guids: dict[str, Path] = {}
    for package_dir_name in ("Runtime", "Samples~"):
        package_dir = root / package_dir_name
        if not package_dir.exists():
            continue
        require_meta(package_dir, root, errors, guids)
        for asset in sorted(package_dir.rglob("*")):
            if asset.name.endswith(".meta"):
                continue
            require_meta(asset, root, errors, guids)

    readme = root / "README.md"
    if readme.is_file():
        text = readme.read_text(encoding="utf-8")
        for required_phrase in (
            "Public Game Key",
            "Indieable Server Secret",
            "Stable",
            "Nightly",
        ):
            if required_phrase not in text:
                errors.append(f"README is missing required boundary/channel text: {required_phrase}")

    return errors


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    args = parser.parse_args()

    errors = validate(args.root)
    if errors:
        print("Package validation failed:", file=sys.stderr)
        for error in errors:
            print(f"  - {error}", file=sys.stderr)
        return 1

    print(f"Package validation passed: {args.root.resolve()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
