#!/usr/bin/env python3
"""Build a self-contained UnityExample ZIP with the local SDK package."""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import tempfile
import zipfile
from pathlib import Path

from package import patch_version
from validate_package import VERSION_RE, validate

ROOT = Path(__file__).resolve().parents[1]
PUBLIC_ENTRIES = (
    "Runtime",
    "Runtime.meta",
    "Samples~",
    "Samples~.meta",
    "README.md",
    "CHANGELOG.md",
    "LICENSE.md",
    "package.json",
    "UnityExample",
)


def copy_entry(source: Path, destination: Path) -> None:
    if source.is_dir():
        shutil.copytree(source, destination)
    elif source.is_file():
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, destination)
    else:
        raise FileNotFoundError(source)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def write_zip(staged_root: Path, output: Path) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for path in sorted(staged_root.rglob("*"), key=lambda item: item.as_posix()):
            if path.is_symlink():
                raise RuntimeError(f"symlink is not allowed: {path}")
            if not path.is_file():
                continue
            relative = path.relative_to(staged_root).as_posix()
            info = zipfile.ZipInfo(f"indieable-sdk-example/{relative}")
            info.date_time = (1980, 1, 1, 0, 0, 0)
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o100644 << 16
            archive.writestr(info, path.read_bytes())


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, default=ROOT / "dist")
    parser.add_argument("--version")
    args = parser.parse_args()

    manifest = json.loads((ROOT / "package.json").read_text(encoding="utf-8"))
    version = args.version or str(manifest["version"])
    if not VERSION_RE.fullmatch(version):
        raise SystemExit(f"invalid package version: {version}")

    with tempfile.TemporaryDirectory(prefix="indieable-unity-example-") as temporary:
        staged_root = Path(temporary) / "indieable-sdk-example"
        staged_root.mkdir()
        for entry in PUBLIC_ENTRIES:
            copy_entry(ROOT / entry, staged_root / entry)
        patch_version(staged_root, version)

        errors = validate(staged_root)
        if errors:
            raise SystemExit("UnityExample staging validation failed:\n- " + "\n- ".join(errors))

        output = args.output.resolve() / f"indieable-unity-example-{version}.zip"
        write_zip(staged_root, output)

    digest = sha256(output)
    checksum = Path(str(output) + ".sha256")
    checksum.write_text(f"{digest}  {output.name}\n", encoding="utf-8")
    print(output)
    print(checksum)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
