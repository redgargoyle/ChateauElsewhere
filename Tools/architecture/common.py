#!/usr/bin/env python3
"""Shared static-analysis helpers for Chateau architecture tooling."""
from __future__ import annotations

from dataclasses import dataclass, asdict
from pathlib import Path
import hashlib
import re
from typing import Iterable

PROHIBITED_PATTERNS: dict[str, re.Pattern[str]] = {
    "FindObject": re.compile(r"\b(?:Object\.)?Find(?:AnyObjectByType|FirstObjectByType|ObjectsByType|ObjectOfType|ObjectsOfType)\b|\bGameObject\.Find\s*\("),
    "ResourcesLoad": re.compile(r"\bResources\.Load(?:All)?\s*<?"),
    "NewGameObject": re.compile(r"\bnew\s+GameObject\s*\("),
    "AddComponent": re.compile(r"\.AddComponent\s*<"),
    "RuntimeInitialize": re.compile(r"RuntimeInitializeOnLoadMethod"),
    "PlayerPrefs": re.compile(r"\bPlayerPrefs\."),
}

DIRECT_MONO_RE = re.compile(
    r"(?m)^\s*(?:(?:public|internal|private|protected|sealed|abstract|partial|static)\s+)*"
    r"class\s+(?P<name>\w+)\s*:\s*(?P<base>MonoBehaviour)\b"
)

TOP_LEVEL_CLASS_RE = re.compile(
    r"(?m)^\s*(?:(?:public|internal|private|protected|sealed|abstract|partial|static)\s+)*"
    r"class\s+(?P<name>\w+)(?:\s*:\s*(?P<bases>[^\{\n]+))?"
)


def project_root(value: str | Path) -> Path:
    root = Path(value).resolve()
    if not (root / "Assets").is_dir() or not (root / "ProjectSettings").is_dir():
        raise ValueError(f"Not a Unity project root: {root}")
    return root


def runtime_csharp_files(root: Path) -> list[Path]:
    return sorted(
        path
        for path in (root / "Assets").rglob("*.cs")
        if "Editor" not in path.parts
    )


def relative(root: Path, path: Path) -> str:
    return path.relative_to(root).as_posix()


def count_patterns(text: str) -> dict[str, int]:
    return {name: len(pattern.findall(text)) for name, pattern in PROHIBITED_PATTERNS.items()}


def direct_monobehaviours(text: str) -> list[str]:
    return [match.group("name") for match in DIRECT_MONO_RE.finditer(text)]


def script_guid(meta_path: Path) -> str:
    if not meta_path.is_file():
        return ""
    match = re.search(r"^guid:\s*([0-9a-fA-F]+)", meta_path.read_text(errors="ignore"), re.MULTILINE)
    return match.group(1) if match else ""


@dataclass(frozen=True)
class FileAudit:
    file: str
    loc: int
    sha256: str
    direct_monobehaviours: tuple[str, ...]
    smells: dict[str, int]

    def as_dict(self) -> dict:
        result = asdict(self)
        result["direct_monobehaviours"] = list(self.direct_monobehaviours)
        return result


def audit_runtime_file(root: Path, path: Path) -> FileAudit:
    text = path.read_text(errors="ignore")
    return FileAudit(
        file=relative(root, path),
        loc=len(text.splitlines()),
        sha256=hashlib.sha256(text.encode(errors="ignore")).hexdigest(),
        direct_monobehaviours=tuple(direct_monobehaviours(text)),
        smells=count_patterns(text),
    )


def audit_runtime(root: Path) -> list[FileAudit]:
    return [audit_runtime_file(root, path) for path in runtime_csharp_files(root)]
