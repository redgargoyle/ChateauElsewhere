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
        if node.tag.rsplit("}", 1)[-1] != "test-case":
            continue
        result = (node.attrib.get("result") or node.attrib.get("status") or "").lower()
        if result in {"failed", "failure", "error"}:
            name = node.attrib.get("fullname") or node.attrib.get("name")
            if name:
                names.append(name)
    return sorted(set(names))


def failed_name_digest(names: list[str]) -> str:
    # Historical Chateau baselines are the sorted failed test-case full names,
    # one per line, including the final newline. Parent-suite names and assembly
    # paths are deliberately excluded so the digest is stable across machines.
    payload = "".join(f"{name}\n" for name in sorted(set(names)))
    return hashlib.sha256(payload.encode("utf-8")).hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("xml")
    parser.add_argument("--minimum-total", type=int, default=1)
    parser.add_argument("--maximum-failed", type=int, default=0)
    parser.add_argument("--expected-total", type=int)
    parser.add_argument("--expected-passed", type=int)
    parser.add_argument("--expected-failed", type=int)
    parser.add_argument("--expected-skipped", type=int)
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
    failure_digest = failed_name_digest(failed_names)

    print(f"Test XML: {path}")
    print(f"Total={total} Passed={passed} Failed={failed} Skipped={skipped}")
    print(f"Failed-name SHA-256={failure_digest}")

    problems: list[str] = []
    if total < args.minimum_total:
        problems.append(f"expected at least {args.minimum_total} tests, got {total}")
    if failed > args.maximum_failed:
        problems.append(f"expected at most {args.maximum_failed} failures, got {failed}")
    if args.expected_total is not None and total != args.expected_total:
        problems.append(f"expected exactly {args.expected_total} tests, got {total}")
    if args.expected_passed is not None and passed != args.expected_passed:
        problems.append(f"expected exactly {args.expected_passed} passing tests, got {passed}")
    if args.expected_failed is not None and failed != args.expected_failed:
        problems.append(f"expected exactly {args.expected_failed} failures, got {failed}")
    if args.expected_skipped is not None and skipped != args.expected_skipped:
        problems.append(f"expected exactly {args.expected_skipped} skipped tests, got {skipped}")
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
