#!/usr/bin/env python3
"""Validate the standalone Unity Package Manager source tree."""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

PACKAGE_NAME = "com.indieable.sdk"
EXPECTED_PACKAGE_FILES = [
    "Editor",
    "Runtime",
    "Samples~",
    "Tests",
    "README.md",
    "CHANGELOG.md",
    "LICENSE.md",
]
REQUIRED_PATHS = [
    "package.json",
    "package.json.meta",
    "README.md",
    "README.md.meta",
    "CHANGELOG.md",
    "CHANGELOG.md.meta",
    "LICENSE.md",
    "LICENSE.md.meta",
    "Editor/Indieable.Editor.asmdef",
    "Editor/IndieableProjectSettingsProvider.cs",
    "Runtime/Indieable.Runtime.asmdef",
    "Runtime/Indieable.cs",
    "Runtime/IndieableAutoBootstrap.cs",
    "Runtime/IndieableClient.cs",
    "Runtime/IndieableFeedbackUI.cs",
    "Runtime/IndieableIdentityStorage.cs",
    "Runtime/IndieableModels.cs",
    "Runtime/IndieablePrivacyUI.cs",
    "Runtime/IndieableProjectSettings.cs",
    "Runtime/IndieableRuntime.cs",
    "Runtime/IndieableTelemetry.cs",
    "Runtime/Resources/IndieablePrivacyPreferences.uxml",
    "Runtime/Resources/IndieablePrivacyPreferences.uss",
    "Runtime/Steam/IIndieableSteamTicketProvider.cs",
    "Runtime/Events/GlobalEventBus.cs",
    "Runtime/Events/IndieableEventContext.cs",
    "Runtime/EventBus/IndieableEventRoutingSettings.cs",
    "Runtime/EventBus/IndieableEventPayloadJson.cs",
    "Runtime/EventBus/IndieableEventBusBridge.cs",
    "Samples~/QuickStart/IndieableQuickStart.cs",
    "Samples~/QuickStart/README.md",
    "Samples~/EventBusIntegration/README.md",
    "Samples~/EventBusIntegration/Scenes/IndieableEventBusSample.unity",
    "Samples~/EventBusIntegration/Config/SampleEventRouting.asset",
    "Samples~/EventBusIntegration/Resources/IndieableEventBusSample.uxml",
    "Samples~/EventBusIntegration/Resources/IndieableEventBusSample.uss",
    "Samples~/EventBusIntegration/Scripts/Indieable.EventBusSample.asmdef",
    "Samples~/EventBusIntegration/Scripts/SampleEventNames.cs",
    "Samples~/EventBusIntegration/Scripts/SampleEvents.cs",
    "Samples~/EventBusIntegration/Scripts/SampleDoor.cs",
    "Samples~/EventBusIntegration/Scripts/SampleWorkorderTerminal.cs",
    "Samples~/EventBusIntegration/Scripts/SampleNode.cs",
    "Samples~/EventBusIntegration/Scripts/SamplePlayerLifecycle.cs",
    "Samples~/EventBusIntegration/Scripts/SampleRunTracker.cs",
    "Samples~/EventBusIntegration/Scripts/IndieableEventBusSampleController.cs",
    "Tests/Editor/Indieable.Tests.Editor.asmdef",
    "Tests/Editor/IndieableEventContextTests.cs",
]
VERSION_RE = re.compile(
    r"^\d+\.\d+\.\d+(?:-nightly\.\d{8}\.\d+)?$"
)
GUID_RE = re.compile(
    r"^guid:\s*([0-9a-f]{32})\s*$",
    re.MULTILINE,
)
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


def require_meta(
    asset: Path,
    root: Path,
    errors: list[str],
    guids: dict[str, Path],
) -> None:
    meta = Path(str(asset) + ".meta")
    if not meta.is_file():
        errors.append(
            f"missing Unity .meta file: {meta.relative_to(root)}"
        )
        return
    text = meta.read_text(
        encoding="utf-8",
        errors="replace",
    )
    match = GUID_RE.search(text)
    if not match:
        errors.append(
            f"missing or invalid Unity GUID: {meta.relative_to(root)}"
        )
        return
    guid = match.group(1)
    previous = guids.get(guid)
    if previous is not None:
        errors.append(
            f"duplicate Unity GUID {guid}: "
            f"{previous.relative_to(root)} and "
            f"{meta.relative_to(root)}"
        )
    else:
        guids[guid] = meta


def require_markers(
    path: Path,
    markers: tuple[str, ...],
    errors: list[str],
) -> None:
    if not path.is_file():
        return
    text = path.read_text(
        encoding="utf-8",
        errors="replace",
    )
    for marker in markers:
        if marker not in text:
            errors.append(
                f"{path.name} is missing required marker: {marker}"
            )


def validate(root: Path) -> list[str]:
    root = root.resolve()
    errors: list[str] = []

    for visible_development_directory in (
        "DotNet",
        "docs",
        "scripts",
    ):
        if (root / visible_development_directory).exists():
            errors.append(
                "repository-only directory must use Unity's trailing-tilde "
                "ignore convention: " + visible_development_directory
            )

    manifest = load_json(root / "package.json", errors)
    if manifest.get("name") != PACKAGE_NAME:
        errors.append(
            f"package name must be {PACKAGE_NAME!r}"
        )

    version = str(manifest.get("version", ""))
    if not VERSION_RE.fullmatch(version):
        errors.append(
            f"invalid package version: {version!r}"
        )

    if manifest.get("unity") != "2022.3":
        errors.append(
            "package.json unity baseline must remain 2022.3"
        )

    if manifest.get("license") != "MIT":
        errors.append(
            "package.json license must be MIT"
        )

    if manifest.get("files") != EXPECTED_PACKAGE_FILES:
        errors.append(
            "package.json files must be the exact public-package "
            "allowlist: " + repr(EXPECTED_PACKAGE_FILES)
        )

    dependencies = manifest.get("dependencies", {})
    if dependencies not in ({}, None):
        errors.append(
            "the Unity SDK package must remain dependency-free "
            "unless explicitly reviewed"
        )

    samples = manifest.get("samples")
    expected_samples = {
        "Samples~/QuickStart",
        "Samples~/EventBusIntegration",
    }
    if not isinstance(samples, list):
        errors.append(
            "package.json samples must be an array"
        )
    else:
        paths = {
            row.get("path")
            for row in samples
            if isinstance(row, dict)
        }
        if paths != expected_samples:
            errors.append(
                "package.json must publish exactly Quick Start and "
                "Event Bus Integration samples"
            )

    for relative in REQUIRED_PATHS:
        if not (root / relative).is_file():
            errors.append(
                f"missing required package file: {relative}"
            )

    if (root / "UnityExample").exists():
        errors.append(
            "standalone UnityExample project is forbidden; ship "
            "normal UPM samples under Samples~/"
        )

    if (root / "ci").exists():
        errors.append(
            "Unity-visible ci/ directory is forbidden; "
            "keep CI-only C# under ci~/"
        )

    for path in root.rglob("*"):
        if path.is_symlink():
            errors.append(
                f"symlinks are not allowed: "
                f"{path.relative_to(root)}"
            )

    asmdef = load_json(
        root / "Runtime/Indieable.Runtime.asmdef",
        errors,
    )
    if asmdef.get("name") != "Indieable.Runtime":
        errors.append(
            "runtime asmdef name must be Indieable.Runtime"
        )
    if asmdef.get("allowUnsafeCode") is not False:
        errors.append(
            "runtime asmdef must not enable unsafe code"
        )
    if asmdef.get("references") not in ([], None):
        errors.append(
            "runtime asmdef must remain free of package references"
        )

    sample_asmdef = load_json(
        root
        / "Samples~/EventBusIntegration/Scripts/"
          "Indieable.EventBusSample.asmdef",
        errors,
    )
    if sample_asmdef.get("references") != [
        "Indieable.Runtime"
    ]:
        errors.append(
            "Event Bus sample asmdef must reference only "
            "Indieable.Runtime"
        )

    runtime_files = sorted(
        (root / "Runtime").rglob("*.cs")
    )
    runtime_text = "\n".join(
        file.read_text(
            encoding="utf-8",
            errors="replace",
        )
        for file in runtime_files
    )
    for token, meaning in FORBIDDEN_RUNTIME_TEXT.items():
        if token in runtime_text:
            errors.append(
                f"runtime contains forbidden {meaning}: {token}"
            )

    pure_bus_files = sorted(
        (root / "Runtime/Events").glob("*.cs")
    )
    pure_bus_text = "\n".join(
        path.read_text(encoding="utf-8")
        for path in pure_bus_files
    )
    if "UnityEngine" in pure_bus_text:
        errors.append(
            "Runtime/Events must remain engine-agnostic "
            "and cannot reference UnityEngine"
        )

    require_markers(
        root / "Runtime/Events/GlobalEventBus.cs",
        (
            "public interface IGameEventBus",
            "public sealed class GameEventBus",
            "public static class GlobalEventBus",
            "SubscribeAll",
            "SubscriberException",
        ),
        errors,
    )
    require_markers(
        root
        / "Runtime/EventBus/IndieableEventBusBridge.cs",
        (
            "IndieableEventRoutingSettings",
            "GlobalEventBus.Default",
            "GameplayTelemetryGranted",
            "ApplyPrivacyPreferences",
            "Events rejected before a valid purpose",
            "IndieableTelemetry.Send",
        ),
        errors,
    )
    require_markers(
        root
        / "Runtime/EventBus/"
          "IndieableEventRoutingSettings.cs",
        (
            "Disabled",
            "AllowList",
            "DenyList",
            "All",
            "SelectionMode = "
            "IndieableEventSelectionMode.AllowList",
            "TestByDefault = true",
        ),
        errors,
    )
    require_markers(
        root / "Runtime/IndieableModels.cs",
        (
            "public sealed class IndieableRequestHeader",
            "TryResolve",
        ),
        errors,
    )
    require_markers(
        root / "Runtime/IndieableClient.cs",
        (
            "ApplyRequestHeaders",
            "request.SetRequestHeader(name, value)",
        ),
        errors,
    )
    require_markers(
        root / "Editor/IndieableProjectSettingsProvider.cs",
        (
            "Optional Request Headers",
            "Initialize Automatically",
            "Show Startup Consent",
        ),
        errors,
    )
    require_markers(
        root / "Runtime/IndieableAutoBootstrap.cs",
        (
            "RuntimeInitializeLoadType.SubsystemRegistration",
            "RuntimeInitializeLoadType.BeforeSceneLoad",
            "RuntimeInitializeLoadType.AfterSceneLoad",
            "RequestAutomatic",
            "ShouldSuppressAutomaticUi",
        ),
        errors,
    )
    require_markers(
        root / "Runtime/IndieablePrivacyUI.cs",
        (
            "UnityEngine.UIElements",
            "IndieablePrivacyPreferences",
            "SaveChoices",
            "RecordDecision",
        ),
        errors,
    )

    provider_specific_text = "\n".join(
        path.read_text(encoding="utf-8", errors="replace")
        for package_dir in ("Runtime", "Editor", "Samples~", "Tests")
        for path in (root / package_dir).rglob("*")
        if path.is_file() and not path.name.endswith(".meta")
    )
    for removed in (
        "x-vercel-protection-bypass",
        "VERCEL_AUTOMATION_BYPASS_SECRET",
        "Add Vercel Protection Bypass",
    ):
        if removed in provider_specific_text:
            errors.append(
                "package still contains removed hosting-provider preset: "
                + removed
            )

    models_path = root / "Runtime/IndieableModels.cs"
    if models_path.is_file() and version:
        match = SDK_VERSION_RE.search(
            models_path.read_text(
                encoding="utf-8"
            )
        )
        expected_sdk_version = f"unity-{version}"
        if not match:
            errors.append(
                "could not locate IndieableOptions.SdkVersion"
            )
        elif match.group(1) != expected_sdk_version:
            errors.append(
                "IndieableOptions.SdkVersion must be "
                f"{expected_sdk_version!r}, found "
                f"{match.group(1)!r}"
            )

    quick_start = (
        root
        / "Samples~/QuickStart/IndieableQuickStart.cs"
    )
    if quick_start.is_file():
        sample_text = quick_start.read_text(
            encoding="utf-8"
        )
        if "Indieable.Initialize" in sample_text:
            errors.append(
                "Quick Start must use SDK automatic initialization"
            )
        if "PrivacyVisibilityChanged" not in sample_text:
            errors.append(
                "Quick Start must demonstrate SDK UI visibility handling"
            )
        if re.search(
            r"ind_(?:sec|srv)_[A-Za-z0-9_-]+",
            sample_text,
        ):
            errors.append(
                "Quick Start contains a server-side "
                "Indieable credential"
            )

    sample_scripts = (
        root / "Samples~/EventBusIntegration/Scripts"
    )
    for producer_name in (
        "SampleDoor.cs",
        "SampleWorkorderTerminal.cs",
        "SampleNode.cs",
        "SamplePlayerLifecycle.cs",
        "SampleRunTracker.cs",
    ):
        producer = sample_scripts / producer_name
        if not producer.is_file():
            continue
        text = producer.read_text(encoding="utf-8")
        if "GlobalEventBus.Publish" not in text:
            errors.append(
                f"{producer_name} does not publish to "
                "GlobalEventBus"
            )
        if re.search(
            r"\b(?:Indieable\.SendEvent|IndieableTelemetry\.Send)\s*\(",
            text,
        ):
            errors.append(
                f"{producer_name} directly depends on "
                "the Indieable network API"
            )

    routing_asset = (
        root
        / "Samples~/EventBusIntegration/Config/"
          "SampleEventRouting.asset"
    )
    if routing_asset.is_file():
        routing = routing_asset.read_text(
            encoding="utf-8"
        )
        for marker in (
            "SelectionMode: 1",
            "TestByDefault: 1",
            "SourceEventName: game.door.opened",
            "SourceEventName: game.workorder.done",
            "SourceEventName: game.node.closed",
            "SourceEventName: game.player.died",
            "SourceEventName: game.run.completed",
        ):
            if marker not in routing:
                errors.append(
                    "sample routing asset missing safe/default "
                    f"marker: {marker}"
                )

    scene_path = (
        root
        / "Samples~/EventBusIntegration/Scenes/"
          "IndieableEventBusSample.unity"
    )
    if scene_path.is_file():
        scene = scene_path.read_text(
            encoding="utf-8"
        )
        for name in (
            "m_Name: Indieable SDK",
            "m_Name: Sample Gameplay Systems",
            "m_Name: Run System",
            "m_Name: Door",
            "m_Name: Workorder Terminal",
            "m_Name: Tunnel Node",
            "m_Name: Player Lifecycle",
        ):
            if name not in scene:
                errors.append(
                    "sample scene is missing GameObject: "
                    f"{name.removeprefix('m_Name: ')}"
                )

    controller_path = (
        sample_scripts
        / "IndieableEventBusSampleController.cs"
    )
    if controller_path.is_file():
        controller = controller_path.read_text(
            encoding="utf-8"
        )
        if "Indieable.Initialize" in controller:
            errors.append(
                "Event Bus sample must use SDK automatic initialization"
            )
        if (
            "SystemInfo.deviceUniqueIdentifier" in controller
            or "Time.timeScale" in controller
        ):
            errors.append(
                "sample controller uses prohibited device "
                "fingerprinting or forced pause behavior"
            )
        if re.search(
            r"ind_(?:sec|srv)_[A-Za-z0-9_-]+",
            controller,
        ):
            errors.append(
                "sample contains an Indieable server-side "
                "credential"
            )

    guids: dict[str, Path] = {}
    for package_dir_name in ("Editor", "Runtime", "Samples~", "Tests"):
        package_dir = root / package_dir_name
        if not package_dir.exists():
            continue
        require_meta(
            package_dir,
            root,
            errors,
            guids,
        )
        for asset in sorted(package_dir.rglob("*")):
            if asset.name.endswith(".meta"):
                continue
            require_meta(
                asset,
                root,
                errors,
                guids,
            )

    readme = root / "README.md"
    if readme.is_file():
        text = readme.read_text(
            encoding="utf-8"
        )
        for required_phrase in (
            "Public Game Key",
            "Indieable Server Secret",
            "Stable",
            "Nightly",
            "GlobalEventBus",
            "Event Bus Integration",
            "Generic C#",
        ):
            if required_phrase not in text:
                errors.append(
                    "README is missing required boundary/"
                    f"channel text: {required_phrase}"
                )

    return errors


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--root",
        type=Path,
        default=Path(__file__).resolve().parents[1],
    )
    args = parser.parse_args()

    errors = validate(args.root)
    if errors:
        print(
            "Package validation failed:",
            file=sys.stderr,
        )
        for error in errors:
            print(
                f"  - {error}",
                file=sys.stderr,
            )
        return 1

    print(
        f"Package validation passed: "
        f"{args.root.resolve()}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
