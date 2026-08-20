#!/usr/bin/env python3
"""Validate the importable UI Toolkit sample and full UnityExample project."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SAMPLE = ROOT / "Samples~/UIToolkitExample"
PROJECT = ROOT / "UnityExample/Assets/IndieableExample"
FILES = (
    "IndieableUiToolkitExample.cs",
    "Resources/IndieableUiToolkitExample.uxml",
    "Resources/IndieableUiToolkitExample.uss",
)


def main() -> int:
    errors: list[str] = []

    for relative in FILES:
        sample = SAMPLE / relative
        project = PROJECT / relative
        if not sample.is_file():
            errors.append(f"missing package sample file: {sample.relative_to(ROOT)}")
            continue
        if not project.is_file():
            errors.append(f"missing UnityExample file: {project.relative_to(ROOT)}")
            continue
        if sample.read_bytes() != project.read_bytes():
            errors.append(f"UnityExample diverged from package sample: {relative}")

    script_path = SAMPLE / "IndieableUiToolkitExample.cs"
    uxml_path = SAMPLE / "Resources/IndieableUiToolkitExample.uxml"
    uss_path = SAMPLE / "Resources/IndieableUiToolkitExample.uss"
    if script_path.is_file():
        script = script_path.read_text(encoding="utf-8")
        for marker in (
            "UnityEngine.UIElements",
            "RuntimeInitializeOnLoadMethod",
            'PlaceholderPublicKey = "ind_pub_replace_me"',
            "_telemetryToggle.value = false",
            "_diagnosticsToggle.value = false",
        ):
            if marker not in script:
                errors.append(f"example script missing privacy/integration marker: {marker}")
        if "SystemInfo.deviceUniqueIdentifier" in script or "Time.timeScale" in script:
            errors.append("example uses prohibited device fingerprinting or forced pause behavior")
        if re.search(r"ind_(?:sec|srv)_[A-Za-z0-9_-]+", script):
            errors.append("example contains an Indieable server-side credential")
        awake = re.search(r"private void Awake\(\)\s*\{(?P<body>.*?)\n\s*\}", script, re.S)
        if awake and re.search(
            r"Indieable\.(?:Connect|SendEvent|SetPrivacyPreference|LinkAccount)",
            awake.group("body"),
        ):
            errors.append("example Awake performs identity, event, or permission network work")

    if uxml_path.is_file():
        uxml = uxml_path.read_text(encoding="utf-8")
        for marker in (
            'name="telemetry-toggle" label="Allow gameplay telemetry" value="false"',
            'name="diagnostics-toggle" label="Allow optional diagnostics" value="false"',
            'name="permission-decline" text="Continue without optional data"',
            'name="permission-save" text="Allow selected"',
        ):
            if marker not in uxml:
                errors.append(f"permission UI missing balanced opt-in marker: {marker}")
        if uxml.count("permission-action") < 2:
            errors.append("both permission actions must share the permission-action class")

    if uss_path.is_file():
        uss = uss_path.read_text(encoding="utf-8")
        if ".permission-action" not in uss or "flex-grow: 1" not in uss:
            errors.append("permission actions do not have equal width treatment")

    package = json.loads((ROOT / "package.json").read_text(encoding="utf-8"))
    sample_paths = {row.get("path") for row in package.get("samples", [])}
    if "Samples~/UIToolkitExample" not in sample_paths:
        errors.append("package.json does not publish the UI Toolkit Integration Lab sample")

    project_manifest_path = ROOT / "UnityExample/Packages/manifest.json"
    if not project_manifest_path.is_file():
        errors.append("UnityExample package manifest is missing")
    else:
        manifest = json.loads(project_manifest_path.read_text(encoding="utf-8"))
        if manifest.get("dependencies", {}).get("com.indieable.sdk") != "file:../..":
            errors.append("UnityExample must use the repository-root local package")

    for relative in (
        "UnityExample/ProjectSettings/ProjectVersion.txt",
        "UnityExample/ProjectSettings/EditorBuildSettings.asset",
        "UnityExample/Assets/Scenes/IndieableExample.unity",
        "docs/UNITY-EXAMPLE-TESTING.md",
    ):
        if not (ROOT / relative).is_file():
            errors.append(f"missing UnityExample support file: {relative}")

    if errors:
        print("UnityExample validation failed:", file=sys.stderr)
        for error in errors:
            print(f"  - {error}", file=sys.stderr)
        return 1

    print("UnityExample validation passed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
