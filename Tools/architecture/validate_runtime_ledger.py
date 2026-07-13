#!/usr/bin/env python3
"""Ensure every runtime source file is represented exactly once in the migration ledger."""
from __future__ import annotations

import argparse
import csv
from pathlib import Path

FINAL_FORBIDDEN_ACTIONS = {
    "UNKNOWN", "UNCLASSIFIED", "MIGRATE/REVIEW", "REVIEW", "TBD", "TEMPORARY",
}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", default=".")
    parser.add_argument(
        "--ledger",
        default="Docs/Architecture/Overhaul/FINAL_RUNTIME_MIGRATION_LEDGER.csv",
    )
    parser.add_argument("--final", action="store_true")
    args = parser.parse_args()

    root = Path(args.project_root).resolve()
    ledger_path = root / args.ledger
    if not ledger_path.is_file():
        print(f"LEDGER INVALID: missing {ledger_path}")
        return 2

    runtime = sorted(
        path.relative_to(root).as_posix()
        for path in (root / "Assets").rglob("*.cs")
        if "/Editor/" not in path.as_posix() and "/Tests/" not in path.as_posix()
    )

    with ledger_path.open(encoding="utf-8", newline="") as handle:
        rows = list(csv.DictReader(handle))

    by_file: dict[str, list[dict[str, str]]] = {}
    for row in rows:
        by_file.setdefault(row.get("current_file", ""), []).append(row)

    failures: list[str] = []
    for file in runtime:
        count = len(by_file.get(file, []))
        if count != 1:
            failures.append(f"{file}: expected one ledger row, got {count}")

    for file in sorted(set(by_file) - set(runtime)):
        # A deleted file may remain as historical proof only if its action begins with PRUNED/DELETED.
        actions = {r.get("final_action", "").upper() for r in by_file[file]}
        if not all(a.startswith("PRUNED") or a.startswith("DELETED") for a in actions):
            failures.append(f"ledger row has no current runtime file and is not marked pruned: {file}")

    for file, file_rows in by_file.items():
        for row in file_rows:
            required = [
                "final_action", "target_owner_or_path", "migration_phase",
                "deletion_or_completion_gate", "required_test_evidence",
            ]
            for field in required:
                if not (row.get(field) or "").strip():
                    failures.append(f"{file}: empty required field {field}")
            if args.final:
                action = (row.get("final_action") or "").strip().upper()
                if action in FINAL_FORBIDDEN_ACTIONS or "REVIEW" in action or "TEMPORARY" in action:
                    failures.append(f"{file}: unresolved final action {action}")

    if failures:
        print("RUNTIME LEDGER FAILED")
        for failure in failures:
            print(f"- {failure}")
        return 1

    print(f"Runtime ledger passed: {len(runtime)} runtime files, {len(rows)} rows.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
