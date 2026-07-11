#!/usr/bin/env python3
"""Fail when new architecture debt exceeds the committed legacy baseline."""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from common import audit_runtime, project_root


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--baseline", default="Docs/Architecture/Baseline/architecture_guard_baseline.json")
    args = parser.parse_args()

    root = project_root(args.project_root)
    baseline_path = root / args.baseline
    baseline = json.loads(baseline_path.read_text(encoding="utf-8"))
    allowed_counts = baseline["allowed_smell_counts_by_file"]
    allowed_direct = {
        (item["file"], item["class"])
        for item in baseline["allowed_direct_monobehaviours"]
    }
    approved_roots = {
        (item["file"], item["class"])
        for item in baseline.get("approved_architecture_roots", [])
    }

    failures = []
    audits = audit_runtime(root)
    for audit in audits:
        limits = allowed_counts.get(audit.file, {})
        for smell, count in audit.smells.items():
            allowed = int(limits.get(smell, 0))
            if count > allowed:
                failures.append(f"{audit.file}: {smell} increased from allowed {allowed} to {count}")
        for class_name in audit.direct_monobehaviours:
            key = (audit.file, class_name)
            if key not in allowed_direct and key not in approved_roots:
                failures.append(
                    f"{audit.file}: new direct MonoBehaviour '{class_name}'. "
                    "Use an approved Chateau base or record an ADR and update the baseline deliberately."
                )

    for removed in baseline.get("must_remain_pruned", []):
        if (root / removed).exists():
            failures.append(f"Pruned file returned: {removed}")

    # Every C# script must carry a Unity meta file, and GUIDs must be unique.
    guids = {}
    for script in sorted((root / "Assets").rglob("*.cs")):
        meta = script.with_suffix(script.suffix + ".meta")
        if not meta.is_file():
            failures.append(f"Missing Unity meta file: {script.relative_to(root).as_posix()}")
            continue
        guid = ""
        for line in meta.read_text(errors="ignore").splitlines():
            if line.startswith("guid:"):
                guid = line.split(":", 1)[1].strip()
                break
        if not guid:
            failures.append(f"Missing GUID in {meta.relative_to(root).as_posix()}")
        elif guid in guids:
            failures.append(
                f"Duplicate script GUID {guid}: {guids[guid]} and {script.relative_to(root).as_posix()}"
            )
        else:
            guids[guid] = script.relative_to(root).as_posix()

    if failures:
        print("ARCHITECTURE GUARD FAILED")
        for failure in failures:
            print(f"- {failure}")
        return 1

    print("Architecture guard passed: no new debt above the committed legacy baseline.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
