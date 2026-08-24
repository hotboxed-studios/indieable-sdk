#!/usr/bin/env python3
"""Build an allowlisted Unity Package Manager .tgz release artifact."""

from __future__ import annotations

import argparse
import gzip
import hashlib
import json
import os
import re
import shutil
import subprocess
import tarfile
import tempfile
from datetime import datetime, timezone
from pathlib import Path

from validate_package import SDK_VERSION_RE, VERSION_RE, validate

ROOT = Path(__file__).resolve().parents[1]
PUBLIC_ENTRIES = (
    "Editor",
    "Editor.meta",
    "Runtime",
    "Runtime.meta",
    "Samples~",
    "Samples~.meta",
    "Tests",
    "Tests.meta",
    "README.md",
    "README.md.meta",
    "CHANGELOG.md",
    "CHANGELOG.md.meta",
    "LICENSE.md",
    "LICENSE.md.meta",
    "package.json",
    "package.json.meta",
)


def git_value(*args: str) -> str:
    try:
        return subprocess.check_output(
            ["git", *args], cwd=ROOT, stderr=subprocess.DEVNULL, text=True
        ).strip()
    except (subprocess.CalledProcessError, FileNotFoundError):
        return ""


def copy_public_package(destination: Path) -> None:
    for entry in PUBLIC_ENTRIES:
        source = ROOT / entry
        target = destination / entry
        if source.is_dir():
            shutil.copytree(source, target)
        elif source.is_file():
            shutil.copy2(source, target)
        else:
            raise FileNotFoundError(f"missing public package entry: {entry}")


def patch_version(package_root: Path, version: str) -> None:
    manifest_path = package_root / "package.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    manifest["version"] = version
    manifest_path.write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )

    models_path = package_root / "Runtime/IndieableModels.cs"
    models = models_path.read_text(encoding="utf-8")
    replacement = f'public string SdkVersion = "unity-{version}";'
    models, count = SDK_VERSION_RE.subn(replacement, models, count=1)
    if count != 1:
        raise RuntimeError("could not patch IndieableOptions.SdkVersion")
    models_path.write_text(models, encoding="utf-8")


def normalized_tar_info(tar: tarfile.TarFile, path: Path, arcname: str) -> tarfile.TarInfo:
    info = tar.gettarinfo(str(path), arcname=arcname)
    info.uid = 0
    info.gid = 0
    info.uname = ""
    info.gname = ""
    info.mtime = int(os.environ.get("SOURCE_DATE_EPOCH", "0"))
    if info.isdir():
        info.mode = 0o755
    elif info.isfile():
        info.mode = 0o644
    return info


def write_archive(package_root: Path, archive_path: Path) -> None:
    archive_path.parent.mkdir(parents=True, exist_ok=True)
    with archive_path.open("wb") as raw:
        with gzip.GzipFile(filename="", mode="wb", fileobj=raw, mtime=0) as compressed:
            with tarfile.open(fileobj=compressed, mode="w", format=tarfile.PAX_FORMAT) as tar:
                paths = [package_root, *sorted(package_root.rglob("*"), key=lambda p: p.as_posix())]
                for path in paths:
                    relative = path.relative_to(package_root)
                    arcname = "package" if relative == Path(".") else f"package/{relative.as_posix()}"
                    info = normalized_tar_info(tar, path, arcname)
                    if info.isfile():
                        with path.open("rb") as source:
                            tar.addfile(info, source)
                    else:
                        tar.addfile(info)


def verify_archive(archive_path: Path, package_root: Path) -> None:
    expected = {"package"}
    for path in package_root.rglob("*"):
        expected.add(f"package/{path.relative_to(package_root).as_posix()}")

    with tarfile.open(archive_path, mode="r:gz") as tar:
        actual = set()
        for member in tar.getmembers():
            name = member.name.rstrip("/")
            if name.startswith("/") or ".." in Path(name).parts:
                raise RuntimeError(f"unsafe archive member: {member.name}")
            if member.issym() or member.islnk():
                raise RuntimeError(f"archive link is not allowed: {member.name}")
            actual.add(name)

    if actual != expected:
        missing = sorted(expected - actual)
        extra = sorted(actual - expected)
        raise RuntimeError(f"archive contents differ from allowlist; missing={missing}, extra={extra}")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, default=ROOT / "dist")
    parser.add_argument("--channel", choices=("stable", "nightly"), required=True)
    parser.add_argument("--version", help="override the package version in the staged artifact")
    args = parser.parse_args()

    source_manifest = json.loads((ROOT / "package.json").read_text(encoding="utf-8"))
    source_version = str(source_manifest["version"])
    version = args.version or source_version
    if not VERSION_RE.fullmatch(version):
        raise SystemExit(f"invalid package version: {version}")
    if args.channel == "stable" and version != source_version:
        raise SystemExit("Stable package version must exactly match package.json")
    if args.channel == "stable" and "-" in version:
        raise SystemExit("Stable package versions cannot be prereleases")
    if args.channel == "nightly" and not re.fullmatch(
        r"\d+\.\d+\.\d+-nightly\.\d{8}\.\d+", version
    ):
        raise SystemExit("Nightly version must use X.Y.Z-nightly.YYYYMMDD.RUN")

    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)

    with tempfile.TemporaryDirectory(prefix="indieable-sdk-") as temporary:
        package_root = Path(temporary) / "package"
        package_root.mkdir()
        copy_public_package(package_root)
        patch_version(package_root, version)

        errors = validate(package_root)
        if errors:
            raise SystemExit("staged package validation failed:\n- " + "\n- ".join(errors))

        archive = output / f"indieable-connect-{version}.tgz"
        write_archive(package_root, archive)
        verify_archive(archive, package_root)

    digest = sha256(archive)
    checksum_path = output / f"{archive.name}.sha256"
    checksum_path.write_text(f"{digest}  {archive.name}\n", encoding="utf-8")

    metadata_path = output / f"indieable-connect-{version}.json"
    metadata = {
        "package": source_manifest["name"],
        "version": version,
        "channel": args.channel,
        "file": archive.name,
        "sha256": digest,
        "commit": os.environ.get("GITHUB_SHA") or git_value("rev-parse", "HEAD"),
        "built_at": datetime.now(timezone.utc).isoformat(),
    }
    metadata_path.write_text(
        json.dumps(metadata, indent=2, sort_keys=True) + "\n", encoding="utf-8"
    )

    print(archive)
    print(checksum_path)
    print(metadata_path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
