#!/usr/bin/env python3
"""Fail when likely credentials exist in the working tree or Git history."""

from __future__ import annotations

import argparse
import re
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path

MAX_BLOB_BYTES = 2_000_000
BLOCKED_BASENAMES = {
    ".env",
    "id_rsa",
    "id_ed25519",
    "credentials.json",
    "service-account.json",
}
BLOCKED_SUFFIXES = {
    ".pem",
    ".key",
    ".p12",
    ".pfx",
    ".jks",
    ".keystore",
}


@dataclass(frozen=True)
class SecretPattern:
    name: str
    regex: re.Pattern[str]


PATTERNS = [
    SecretPattern("private key", re.compile(r"BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY")),
    SecretPattern("GitHub token", re.compile(r"gh[pousr]_[A-Za-z0-9]{20,}")),
    SecretPattern("AWS access key", re.compile(r"AKIA[0-9A-Z]{16}")),
    SecretPattern("Stripe secret", re.compile(r"sk_(?:live|test)_[A-Za-z0-9]{16,}")),
    SecretPattern("OpenAI-style secret", re.compile(r"sk-[A-Za-z0-9]{24,}")),
    SecretPattern("Slack token", re.compile(r"xox[baprs]-[A-Za-z0-9-]{20,}")),
    SecretPattern(
        "Discord webhook",
        re.compile(r"https://(?:canary\.)?discord(?:app)?\.com/api/webhooks/\d+/[A-Za-z0-9._-]+"),
    ),
    SecretPattern("JWT", re.compile(r"eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}")),
    SecretPattern("Indieable server credential", re.compile(r"ind_(?:sec|srv)_[A-Za-z0-9_-]{8,}")),
    SecretPattern(
        "hard-coded secret assignment",
        re.compile(
            r"(?i)(?:api[_-]?key|client[_-]?secret|server[_-]?secret|password|private[_-]?key|access[_-]?token|refresh[_-]?token)"
            r"\s*[:=]\s*[\"'][^\"'\r\n]{12,}[\"']"
        ),
    ),
]

PLACEHOLDER_MARKERS = (
    "replace_me",
    "placeholder",
    "example",
    "your_",
    "<secret>",
    "<token>",
    "<key>",
    "...",
    "${{",
)


def run_git(*args: str) -> bytes:
    return subprocess.check_output(["git", *args], stderr=subprocess.DEVNULL)


def blocked_path(path: str) -> str | None:
    normalized = path.replace("\\", "/")
    name = Path(normalized).name.lower()
    if name == ".env.example":
        return None
    if name in BLOCKED_BASENAMES or name.startswith(".env."):
        return "credential/configuration file"
    if Path(name).suffix.lower() in BLOCKED_SUFFIXES:
        return "private-key/certificate container"
    return None


def is_placeholder(line: str) -> bool:
    lowered = line.lower()
    return any(marker in lowered for marker in PLACEHOLDER_MARKERS)


def is_detection_rule(origin: str, line: str) -> bool:
    is_security_tool = (
        "scripts/validate_package.py" in origin
        or "scripts/scan_secrets.py" in origin
    )
    if not is_security_tool or "-----BEGIN" in line:
        return False
    lowered = line.lower()
    return "begin" in lowered and "private key" in lowered


def scan_text(origin: str, data: bytes, findings: list[str]) -> None:
    if len(data) > MAX_BLOB_BYTES:
        findings.append(f"{origin}: tracked blob exceeds {MAX_BLOB_BYTES} bytes; inspect manually")
        return
    if b"\x00" in data:
        return
    text = data.decode("utf-8", errors="replace")
    for line_number, line in enumerate(text.splitlines(), start=1):
        for pattern in PATTERNS:
            if (
                pattern.regex.search(line)
                and not is_placeholder(line)
                and not is_detection_rule(origin, line)
            ):
                findings.append(f"{origin}:{line_number}: possible {pattern.name}")


def scan_working_tree(root: Path, findings: list[str]) -> None:
    paths = run_git("ls-files", "-z").decode("utf-8").split("\x00")
    for relative in filter(None, paths):
        reason = blocked_path(relative)
        if reason:
            findings.append(f"working tree:{relative}: blocked {reason}")
            continue
        path = root / relative
        if path.is_file():
            scan_text(f"working tree:{relative}", path.read_bytes(), findings)


def scan_history(findings: list[str]) -> None:
    try:
        objects = run_git("rev-list", "--objects", "--all").decode("utf-8").splitlines()
    except subprocess.CalledProcessError:
        findings.append("could not enumerate Git history")
        return

    seen: set[str] = set()
    for row in objects:
        sha, _, path = row.partition(" ")
        if not path or sha in seen:
            continue
        seen.add(sha)
        try:
            object_type = run_git("cat-file", "-t", sha).decode("ascii").strip()
        except subprocess.CalledProcessError:
            findings.append(f"history:{sha}: could not inspect Git object")
            continue
        if object_type != "blob":
            continue
        reason = blocked_path(path)
        if reason:
            findings.append(f"history:{sha[:12]}:{path}: blocked {reason}")
            continue
        try:
            size = int(run_git("cat-file", "-s", sha).decode("ascii").strip())
            if size > MAX_BLOB_BYTES:
                findings.append(
                    f"history:{sha[:12]}:{path}: blob exceeds {MAX_BLOB_BYTES} bytes; inspect manually"
                )
                continue
            data = run_git("cat-file", "blob", sha)
        except (subprocess.CalledProcessError, ValueError):
            findings.append(f"history:{sha[:12]}:{path}: could not read Git blob")
            continue
        scan_text(f"history:{sha[:12]}:{path}", data, findings)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--history", action="store_true", help="scan every reachable Git blob")
    args = parser.parse_args()

    root = Path(run_git("rev-parse", "--show-toplevel").decode("utf-8").strip())
    findings: list[str] = []
    scan_working_tree(root, findings)
    if args.history:
        scan_history(findings)

    if findings:
        print("Secret scan failed:", file=sys.stderr)
        for finding in sorted(set(findings)):
            print(f"  - {finding}", file=sys.stderr)
        print(
            "Remove the credential from the tree and rewrite the affected Git history before publishing.",
            file=sys.stderr,
        )
        return 1

    scope = "working tree and Git history" if args.history else "working tree"
    print(f"Secret scan passed: {scope}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
