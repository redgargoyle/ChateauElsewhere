#!/usr/bin/env python3
"""Verify that a Unity/NUnit result XML represents a real, sufficiently large test run."""
from __future__ import annotations

import argparse
import hashlib
from pathlib import Path
import xml.etree.ElementTree as ET


def as_int(value: str | None, default: int = 0) -> int:
    try:
        return int(float(value or default))
    except (TypeError, ValueError):
        return default


def find_failed_names(root: ET.Element) -> list[str]:
    names: list[str] = []
    for node in root.iter():
        result = (node.attrib.get("result") or node.attrib.get("status") or "").lower()
        if result in {"failed", "failure", "error"}:
            name = node.attrib.get("fullname") or node.attrib.get("name")
            if name:
                names.append(name)
    return sorted(set(names))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("xml")
    parser.add_argument("--minimum-total", type=int, default=1)
    parser.add_argument("--maximum-failed", type=int, default=0)
    parser.add_argument("--expected-failure-sha256", default="")
    args = parser.parse_args()

    path = Path(args.xml)
    if not path.is_file() or path.stat().st_size == 0:
        print(f"TEST RESULT INVALID: missing or empty XML: {path}")
        return 2

    try:
        root = ET.parse(path).getroot()
    except ET.ParseError as exc:
        print(f"TEST RESULT INVALID: cannot parse {path}: {exc}")
        return 2

    total = as_int(root.attrib.get("total") or root.attrib.get("testcasecount"))
    failed = as_int(root.attrib.get("failed") or root.attrib.get("failures") or root.attrib.get("errors"))
    passed = as_int(root.attrib.get("passed"))
    skipped = as_int(root.attrib.get("skipped") or root.attrib.get("inconclusive"))

    # Some runners wrap the test-run node.
    if total == 0:
        for node in root.iter():
            if node.tag.endswith("test-run"):
                total = as_int(node.attrib.get("total"))
                failed = as_int(node.attrib.get("failed"))
                passed = as_int(node.attrib.get("passed"))
                skipped = as_int(node.attrib.get("skipped") or node.attrib.get("inconclusive"))
                break

    failed_names = find_failed_names(root)
    failure_digest = hashlib.sha256("\n".join(failed_names).encode("utf-8")).hexdigest()

    print(f"Test XML: {path}")
    print(f"Total={total} Passed={passed} Failed={failed} Skipped={skipped}")
    print(f"Failed-name SHA-256={failure_digest}")

    problems: list[str] = []
    if total < args.minimum_total:
        problems.append(f"expected at least {args.minimum_total} tests, got {total}")
    if failed > args.maximum_failed:
        problems.append(f"expected at most {args.maximum_failed} failures, got {failed}")
    if args.expected_failure_sha256 and failure_digest != args.expected_failure_sha256:
        problems.append(
            "failure-name digest changed: "
            f"expected {args.expected_failure_sha256}, got {failure_digest}"
        )

    if problems:
        print("TEST RESULT FAILED")
        for problem in problems:
            print(f"- {problem}")
        if failed_names:
            print("Failed tests:")
            for name in failed_names:
                print(f"- {name}")
        return 1

    print("Test result verified.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
