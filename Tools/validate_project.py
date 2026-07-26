#!/usr/bin/env python3
"""Fast repository checks that do not require a Unity installation."""

from __future__ import annotations

import json
import pathlib
import re
import sys
from dataclasses import dataclass

ROOT = pathlib.Path(__file__).resolve().parents[1]
TEXT_EXTENSIONS = {".cs", ".json", ".md", ".shader", ".txt", ".asmdef"}
PROHIBITED_BRAND_PATTERNS = [
    re.compile(r"\bmario\b", re.IGNORECASE),
    re.compile(r"\bnintendo\b", re.IGNORECASE),
    re.compile(r"\bkoop(?:a|aling)\b", re.IGNORECASE),
    re.compile(r"\bprincess peach\b", re.IGNORECASE),
    re.compile(r"\btoad\b", re.IGNORECASE),
]
ALLOWLIST_FILES = {
    ROOT / "README.md",
    ROOT / "docs" / "IP_AND_ASSET_POLICY.md",
    ROOT / "Tools" / "validate_project.py",
}


@dataclass
class Failure:
    path: pathlib.Path
    message: str


def iter_text_files() -> list[pathlib.Path]:
    ignored = {"Library", "Temp", "Obj", "Build", "Builds", ".git"}
    result: list[pathlib.Path] = []
    for path in ROOT.rglob("*"):
        if not path.is_file() or any(part in ignored for part in path.parts):
            continue
        if path.suffix.lower() in TEXT_EXTENSIONS or path.name in {"LICENSE", ".gitattributes"}:
            result.append(path)
    return result


def validate_json(path: pathlib.Path, failures: list[Failure]) -> None:
    try:
        json.loads(path.read_text(encoding="utf-8"))
    except Exception as exc:  # noqa: BLE001 - validation tool should report all parse failures.
        failures.append(Failure(path, f"invalid JSON: {exc}"))


def strip_csharp_comments_and_strings(text: str) -> str:
    pattern = re.compile(
        r'@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|//.*?$|/\*.*?\*/',
        re.MULTILINE | re.DOTALL,
    )
    return pattern.sub("", text)


def validate_csharp_balance(path: pathlib.Path, text: str, failures: list[Failure]) -> None:
    clean = strip_csharp_comments_and_strings(text)
    pairs = {"{": "}", "(": ")", "[": "]"}
    closing = set(pairs.values())
    stack: list[tuple[str, int]] = []
    for index, char in enumerate(clean):
        if char in pairs:
            stack.append((char, index))
        elif char in closing:
            if not stack or pairs[stack[-1][0]] != char:
                failures.append(Failure(path, f"unbalanced delimiter {char!r} at character {index}"))
                return
            stack.pop()
    if stack:
        failures.append(Failure(path, f"unclosed delimiter {stack[-1][0]!r}"))


def main() -> int:
    failures: list[Failure] = []

    version_file = ROOT / "ProjectSettings" / "ProjectVersion.txt"
    expected_version = "m_EditorVersion: 6000.3.20f1"
    if not version_file.exists() or expected_version not in version_file.read_text(encoding="utf-8"):
        failures.append(Failure(version_file, f"expected {expected_version!r}"))

    for path in iter_text_files():
        text = path.read_text(encoding="utf-8")
        if path.suffix.lower() in {".json", ".asmdef"}:
            validate_json(path, failures)
        if path.suffix.lower() == ".cs":
            validate_csharp_balance(path, text, failures)
        if path not in ALLOWLIST_FILES:
            for pattern in PROHIBITED_BRAND_PATTERNS:
                if pattern.search(text) or pattern.search(path.as_posix()):
                    failures.append(Failure(path, f"contains prohibited borrowed-brand term matching {pattern.pattern!r}"))

    if failures:
        print("Validation failed:")
        for failure in failures:
            print(f"- {failure.path.relative_to(ROOT)}: {failure.message}")
        return 1

    print("Repository validation passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
