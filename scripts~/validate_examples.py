#!/usr/bin/env python3
"""Validate the importable Unity Event Bus sample and generic C# SDK."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SAMPLE = ROOT / "Samples~/EventBusIntegration"
SCRIPTS = SAMPLE / "Scripts"
RESOURCES = SAMPLE / "Resources"
UI = SAMPLE / "UI"

REQUIRED_SAMPLE_FILES = (
    "README.md",
    "Scenes/IndieableEventBusSample.unity",
    "Config/IndieableSampleUiToolkitAssets.asset",
    "Config/SampleEventRouting.asset",
    "Resources/IndieableEventBusSample.uxml",
    "Resources/IndieableEventBusSample.uss",
    "UI/IndieableSampleDefaultRuntimeTheme.tss",
    "UI/IndieableSampleFeedback.uxml",
    "UI/IndieableSampleFeedback.uss",
    "UI/IndieableSamplePrivacy.uxml",
    "UI/IndieableSamplePrivacy.uss",
    "Scripts/Indieable.EventBusSample.asmdef",
    "Scripts/SampleEventNames.cs",
    "Scripts/SampleEvents.cs",
    "Scripts/SampleDoor.cs",
    "Scripts/SampleWorkorderTerminal.cs",
    "Scripts/SampleNode.cs",
    "Scripts/SamplePlayerLifecycle.cs",
    "Scripts/SampleRunTracker.cs",
    "Scripts/IndieableEventBusSampleController.cs",
)

REQUIRED_DOTNET_FILES = (
    "DotNet~/README.md",
    "DotNet~/Indieable.Sdk/README.md",
    "DotNet~/Indieable.Sdk/Indieable.Sdk.csproj",
    "DotNet~/Indieable.Sdk/IndieableClientOptions.cs",
    "DotNet~/Indieable.Sdk/IndieableModels.cs",
    "DotNet~/Indieable.Sdk/IndieableIdentityStorage.cs",
    "DotNet~/Indieable.Sdk/IndieableClient.cs",
    "DotNet~/Indieable.Sdk/IndieableEventBusForwarder.cs",
    "ci~/CoreSmoke/CoreSmoke.csproj",
    "ci~/CoreSmoke/Program.cs",
    "ci~/CompileCheck/EventBusUnityStubs.cs",
)


def require_markers(
    text: str,
    markers: tuple[str, ...],
    label: str,
    errors: list[str],
) -> None:
    for marker in markers:
        if marker not in text:
            errors.append(
                f"{label} missing required marker: {marker}"
            )


def main() -> int:
    errors: list[str] = []

    if (ROOT / "UnityExample").exists():
        errors.append(
            "UnityExample must not exist; the integration is "
            "a normal package sample"
        )
    if (ROOT / "scripts~/package_example.py").exists():
        errors.append(
            "package_example.py must not exist"
        )

    for relative in REQUIRED_SAMPLE_FILES:
        if not (SAMPLE / relative).is_file():
            errors.append(
                f"missing package sample file: "
                f"Samples~/EventBusIntegration/{relative}"
            )

    for relative in REQUIRED_DOTNET_FILES:
        if not (ROOT / relative).is_file():
            errors.append(
                f"missing generic C# support file: {relative}"
            )

    package_path = ROOT / "package.json"
    if package_path.is_file():
        package = json.loads(
            package_path.read_text(encoding="utf-8")
        )
        sample_paths = {
            row.get("path")
            for row in package.get("samples", [])
            if isinstance(row, dict)
        }
        if sample_paths != {
            "Samples~/QuickStart",
            "Samples~/EventBusIntegration",
        }:
            errors.append(
                "package.json must publish exactly Quick Start "
                "and Event Bus Integration"
            )

    controller_path = (
        SCRIPTS / "IndieableEventBusSampleController.cs"
    )
    if controller_path.is_file():
        controller = controller_path.read_text(
            encoding="utf-8"
        )
        require_markers(
            controller,
            (
                "UnityEngine.UIElements",
                "GlobalEventBus.SubscribeAll",
                "eventBusBridge.ApplyPrivacyPreferences",
                "Indieable.ConfigureUiToolkit(uiToolkitAssets)",
                "Indieable.OpenPrivacyPreferences",
                "Indieable.SendEvent",
                "themeStyleSheet",
            ),
            "sample controller",
            errors,
        )
        if "RuntimeInitializeOnLoadMethod" in controller:
            errors.append(
                "sample controller auto-creates itself instead "
                "of using the imported scene"
            )
        if "Indieable.Initialize" in controller:
            errors.append(
                "sample controller must use the project-settings "
                "auto bootstrap instead of initializing the SDK"
            )
        if (
            "SystemInfo.deviceUniqueIdentifier" in controller
            or "Time.timeScale" in controller
        ):
            errors.append(
                "sample uses prohibited device fingerprinting "
                "or forced pause behavior"
            )
        if re.search(
            r"ind_(?:sec|srv)_[A-Za-z0-9_-]+",
            controller,
        ):
            errors.append(
                "sample contains a server-side Indieable credential"
            )

    scene_path = SAMPLE / "Scenes/IndieableEventBusSample.unity"
    if scene_path.is_file():
        scene = scene_path.read_text(encoding="utf-8")
        if "uiToolkitAssets:" not in scene:
            errors.append(
                "sample scene does not reference its editable UI asset set"
            )

    ui_asset_path = (
        SAMPLE / "Config/IndieableSampleUiToolkitAssets.asset"
    )
    if ui_asset_path.is_file():
        ui_asset = ui_asset_path.read_text(encoding="utf-8")
        require_markers(
            ui_asset,
            (
                "themeStyleSheet:",
                "privacyLayout:",
                "privacyStyles:",
                "feedbackLayout:",
                "feedbackStyles:",
            ),
            "sample UI asset set",
            errors,
        )

    privacy_layout = UI / "IndieableSamplePrivacy.uxml"
    if privacy_layout.is_file():
        privacy_uxml = privacy_layout.read_text(encoding="utf-8")
        require_markers(
            privacy_uxml,
            (
                'name="privacy-card"',
                'name="save"',
                'name="decline"',
                'name="retry"',
            ),
            "sample privacy UXML",
            errors,
        )
        if 'name="close"' in privacy_uxml:
            errors.append(
                "sample startup privacy UXML must not have Close"
            )

    feedback_layout = UI / "IndieableSampleFeedback.uxml"
    if feedback_layout.is_file():
        require_markers(
            feedback_layout.read_text(encoding="utf-8"),
            (
                'name="feedback-card"',
                'name="feedback-form"',
                'name="bug-form"',
                'name="feedback-send"',
                'name="feedback-cancel"',
            ),
            "sample feedback UXML",
            errors,
        )

    for stylesheet_name in (
        "IndieableSamplePrivacy.uss",
        "IndieableSampleFeedback.uss",
    ):
        stylesheet = UI / stylesheet_name
        if stylesheet.is_file():
            require_markers(
                stylesheet.read_text(encoding="utf-8"),
                (
                    "align-items: flex-end",
                    "justify-content: flex-end",
                    "background-color:",
                ),
                f"sample {stylesheet_name}",
                errors,
            )

    uxml_path = (
        RESOURCES / "IndieableEventBusSample.uxml"
    )
    if uxml_path.is_file():
        uxml = uxml_path.read_text(
            encoding="utf-8"
        )
        require_markers(
            uxml,
            (
                'name="open-permissions" '
                'text="Open built-in Player Data"',
                'name="door-open"',
                'name="workorder-done"',
                'name="node-close"',
                'name="player-death"',
                'name="run-complete"',
            ),
            "sample UXML",
            errors,
        )
        if 'name="permission-overlay"' in uxml:
            errors.append(
                "sample must use the SDK-owned privacy UI instead "
                "of shipping a competing permission overlay"
            )

    scene_path = (
        SAMPLE / "Scenes/IndieableEventBusSample.unity"
    )
    if scene_path.is_file():
        scene = scene_path.read_text(
            encoding="utf-8"
        )
        require_markers(
            scene,
            (
                "m_Name: Indieable SDK",
                "m_Name: Sample Gameplay Systems",
                "m_Name: Run System",
                "m_Name: Door",
                "m_Name: Workorder Terminal",
                "m_Name: Tunnel Node",
                "m_Name: Player Lifecycle",
                "routingSettings:",
                "door:",
                "workorderTerminal:",
                "node:",
                "playerLifecycle:",
                "runTracker:",
            ),
            "sample scene",
            errors,
        )

    routing_path = (
        SAMPLE / "Config/SampleEventRouting.asset"
    )
    if routing_path.is_file():
        routing = routing_path.read_text(
            encoding="utf-8"
        )
        require_markers(
            routing,
            (
                "SelectionMode: 1",
                "TestByDefault: 1",
                "IndieableEventKey: door_opened",
                "IndieableEventKey: workorder_done",
                "IndieableEventKey: node_closed",
                "IndieableEventKey: player_died",
                "IndieableEventKey: run_completed",
            ),
            "sample routing asset",
            errors,
        )

    producer_files = (
        "SampleDoor.cs",
        "SampleWorkorderTerminal.cs",
        "SampleNode.cs",
        "SamplePlayerLifecycle.cs",
        "SampleRunTracker.cs",
    )
    for file_name in producer_files:
        path = SCRIPTS / file_name
        if not path.is_file():
            continue
        text = path.read_text(encoding="utf-8")
        if "GlobalEventBus.Publish" not in text:
            errors.append(
                f"{file_name} does not publish through "
                "GlobalEventBus"
            )
        if re.search(
            r"\b(?:Indieable\.SendEvent|IndieableTelemetry\.Send)\s*\(",
            text,
        ):
            errors.append(
                f"{file_name} calls Indieable directly"
            )

    dotnet_project = (
        ROOT / "DotNet~/Indieable.Sdk/Indieable.Sdk.csproj"
    )
    if dotnet_project.is_file():
        project = dotnet_project.read_text(
            encoding="utf-8"
        )
        require_markers(
            project,
            (
                "<TargetFramework>net8.0</TargetFramework>",
                "<PackageId>Indieable.Sdk</PackageId>",
                '../../Runtime/Events/*.cs',
                "<PackageLicenseFile>LICENSE.md</PackageLicenseFile>",
            ),
            "generic C# project",
            errors,
        )

    dotnet_source = "\n".join(
        path.read_text(
            encoding="utf-8",
            errors="replace",
        )
        for path in (
            ROOT / "DotNet~/Indieable.Sdk"
        ).glob("*.cs")
    )
    if "UnityEngine" in dotnet_source:
        errors.append(
            "generic C# SDK cannot reference UnityEngine"
        )
    require_markers(
        dotnet_source,
        (
            "public sealed class IndieableClient",
            "GetPrivacyManifestAsync",
            "ConnectAsync",
            "SetPrivacyPreferenceAsync",
            "SendEventAsync",
            "IndieableEventBusForwarder",
            "IIndieableIdentityStorage",
        ),
        "generic C# SDK",
        errors,
    )

    for workflow_name in (
        "ci.yml",
        "nightly.yml",
        "release.yml",
    ):
        path = ROOT / ".github/workflows" / workflow_name
        if not path.is_file():
            continue
        workflow = path.read_text(encoding="utf-8")
        if "UnityExample" in workflow:
            errors.append(
                f"{workflow_name} still packages UnityExample"
            )
        require_markers(
            workflow,
            (
                "DotNet~/Indieable.Sdk/Indieable.Sdk.csproj",
                "ci~/CoreSmoke/CoreSmoke.csproj",
            ),
            workflow_name,
            errors,
        )

    for relative in (
        "Documentation~/EVENT-BUS.md",
        "Documentation~/UNITY-SAMPLE-TESTING.md",
    ):
        if not (ROOT / relative).is_file():
            errors.append(
                f"missing integration documentation: {relative}"
            )

    if errors:
        print(
            "Sample/generic SDK validation failed:",
            file=sys.stderr,
        )
        for error in errors:
            print(f"  - {error}", file=sys.stderr)
        return 1

    print(
        "Event Bus Unity sample and generic C# SDK "
        "validation passed"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
