#!/usr/bin/env python3
"""Map local MonoScript GUIDs to text-serialized Unity assets."""
from __future__ import annotations

import argparse
import csv
from pathlib import Path

from common import project_root, relative, script_guid

SERIALIZED_EXTENSIONS = {
    ".unity", ".prefab", ".asset", ".anim", ".controller",
    ".overrideController", ".playable", ".mat",
}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--output", default="Docs/Architecture/Generated/serialized_script_refs.csv")
    args = parser.parse_args()

    root = project_root(args.project_root)
    assets = root / "Assets"
    serialized = {
        relative(root, path): path.read_text(errors="ignore")
        for path in assets.rglob("*")
        if path.is_file() and path.suffix in SERIALIZED_EXTENSIONS
    }

    rows = []
    for script in sorted(assets.rglob("*.cs")):
        guid = script_guid(script.with_suffix(script.suffix + ".meta"))
        hits = []
        if guid:
            for asset, text in serialized.items():
                count = text.count(guid)
                if count:
                    hits.append((asset, count))
        rows.append({
            "script": relative(root, script),
            "guid": guid,
            "reference_count": sum(count for _, count in hits),
            "referencing_assets": " | ".join(f"{asset}:{count}" for asset, count in hits),
        })

    output = root / args.output
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=rows[0].keys())
        writer.writeheader()
        writer.writerows(rows)

    print(f"Wrote {len(rows)} script records to {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
