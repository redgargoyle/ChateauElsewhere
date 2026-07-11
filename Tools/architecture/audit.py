#!/usr/bin/env python3
"""Generate deterministic static architecture metrics for the Chateau Unity project."""
from __future__ import annotations

import argparse
import csv
import json
from pathlib import Path
from collections import Counter

from common import PROHIBITED_PATTERNS, audit_runtime, project_root


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--output", default="Docs/Architecture/Generated")
    args = parser.parse_args()

    root = project_root(args.project_root)
    output = root / args.output
    output.mkdir(parents=True, exist_ok=True)
    audits = audit_runtime(root)

    totals = Counter()
    direct = []
    for audit in audits:
        totals.update(audit.smells)
        for class_name in audit.direct_monobehaviours:
            direct.append({"file": audit.file, "class": class_name})

    with (output / "runtime_files.csv").open("w", newline="", encoding="utf-8") as handle:
        fieldnames = ["file", "loc", "sha256", *PROHIBITED_PATTERNS.keys(), "direct_monobehaviours"]
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        for audit in audits:
            writer.writerow({
                "file": audit.file,
                "loc": audit.loc,
                "sha256": audit.sha256,
                **audit.smells,
                "direct_monobehaviours": " | ".join(audit.direct_monobehaviours),
            })

    summary = {
        "runtime_files": len(audits),
        "runtime_loc": sum(item.loc for item in audits),
        "smell_totals": dict(totals),
        "direct_monobehaviour_count": len(direct),
        "direct_monobehaviours": direct,
    }
    (output / "summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")

    markdown = [
        "# Generated architecture audit",
        "",
        f"- Runtime C# files: **{summary['runtime_files']}**",
        f"- Runtime C# lines: **{summary['runtime_loc']}**",
        f"- Direct `MonoBehaviour` declarations: **{summary['direct_monobehaviour_count']}**",
        "",
        "## Runtime dependency-repair and global-access occurrences",
        "",
    ]
    for name in PROHIBITED_PATTERNS:
        markdown.append(f"- `{name}`: **{totals[name]}**")
    markdown += [
        "",
        "> Counts identify migration debt. They are not automatic deletion instructions.",
    ]
    (output / "README.md").write_text("\n".join(markdown) + "\n", encoding="utf-8")

    print(json.dumps(summary, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
