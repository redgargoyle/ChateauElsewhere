#!/usr/bin/env python3
"""Check Unity C# metadata, duplicate GUIDs, and unresolved *project* MonoScript refs.

External/package/built-in MonoScript GUIDs are valid and are not treated as missing.
Project GUID identity is learned from the committed baseline SerializedReferences.csv
plus every current C# .meta file.
"""
from __future__ import annotations

import argparse
import csv
import re
from pathlib import Path

SERIALIZED_EXTENSIONS = {
    ".unity", ".prefab", ".asset", ".anim", ".controller", ".overrideController",
    ".playable", ".mat",
}
GUID_RE = re.compile(r"guid:\s*([0-9a-fA-F]{32})")
SCRIPT_RE = re.compile(r"m_Script:\s*\{[^}]*guid:\s*([0-9a-fA-F]{32})")


def meta_guid(meta: Path) -> str:
    if not meta.is_file():
        return ""
    match = GUID_RE.search(meta.read_text(errors="ignore"))
    return match.group(1).lower() if match else ""


def load_baseline_project_guids(path: Path) -> dict[str, str]:
    result: dict[str, str] = {}
    if not path.is_file():
        return result
    with path.open(encoding="utf-8", newline="") as handle:
        for row in csv.DictReader(handle):
            guid = (row.get("guid") or "").lower()
            file = row.get("file") or row.get("script") or ""
            if guid and file:
                result[guid] = file
    return result


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--output", default="Docs/Architecture/Generated/script_integrity.csv")
    parser.add_argument(
        "--baseline-project-scripts",
        default="Docs/Architecture/Baseline/SerializedReferences.csv",
    )
    args = parser.parse_args()

    root = Path(args.project_root).resolve()
    assets = root / "Assets"
    failures: list[str] = []
    current_guid_to_script: dict[str, str] = {}

    for script in sorted(assets.rglob("*.cs")):
        rel = script.relative_to(root).as_posix()
        meta = script.with_suffix(script.suffix + ".meta")
        guid = meta_guid(meta)
        if not meta.is_file():
            failures.append(f"missing .meta: {rel}")
        elif not guid:
            failures.append(f"missing script GUID: {meta.relative_to(root).as_posix()}")
        elif guid in current_guid_to_script:
            failures.append(f"duplicate script GUID {guid}: {current_guid_to_script[guid]} and {rel}")
        else:
            current_guid_to_script[guid] = rel

    baseline_guid_to_script = load_baseline_project_guids(root / args.baseline_project_scripts)
    known_project_guids = set(current_guid_to_script) | set(baseline_guid_to_script)

    rows: list[dict[str, str | int]] = []
    unresolved: dict[str, list[str]] = {}
    external_count = 0
    for asset in sorted(assets.rglob("*")):
        if not asset.is_file() or asset.suffix not in SERIALIZED_EXTENSIONS:
            continue
        text = asset.read_text(errors="ignore")
        for guid in SCRIPT_RE.findall(text):
            guid = guid.lower()
            rel = asset.relative_to(root).as_posix()
            resolved = current_guid_to_script.get(guid, "")
            kind = "project" if guid in known_project_guids else "external"
            if kind == "external":
                external_count += 1
            rows.append({
                "asset": rel,
                "script_guid": guid,
                "kind": kind,
                "resolved_script": resolved,
                "baseline_script": baseline_guid_to_script.get(guid, ""),
            })
            if kind == "project" and not resolved:
                unresolved.setdefault(guid, []).append(rel)

    output = root / args.output
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(
            handle,
            fieldnames=["asset", "script_guid", "kind", "resolved_script", "baseline_script"],
        )
        writer.writeheader()
        writer.writerows(rows)

    for guid, assets_using in sorted(unresolved.items()):
        previous = baseline_guid_to_script.get(guid, "unknown project script")
        failures.append(
            f"unresolved project MonoScript GUID {guid} ({previous}): "
            f"{' | '.join(sorted(set(assets_using)))}"
        )

    if failures:
        print("UNITY SCRIPT INTEGRITY FAILED")
        for failure in failures:
            print(f"- {failure}")
        print(f"Evidence: {output}")
        return 1

    print(
        f"Unity script integrity passed: {len(current_guid_to_script)} current scripts, "
        f"{len(rows)} serialized script references, {external_count} external/package refs. "
        f"Evidence: {output}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
