#!/usr/bin/env python3
from __future__ import annotations

import hashlib
import importlib.util
from pathlib import Path
import subprocess
import sys
import tempfile
import textwrap
import unittest
import xml.etree.ElementTree as ET


SCRIPT = Path(__file__).resolve().parents[1] / "verify_nunit_xml.py"
SPEC = importlib.util.spec_from_file_location("verify_nunit_xml", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
VERIFY = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(VERIFY)


SYNTHETIC_XML = textwrap.dedent(
    """\
    <?xml version="1.0" encoding="utf-8"?>
    <test-run total="2" passed="1" failed="1" skipped="0" result="Failed">
      <test-suite name="project" fullname="/machine/path/Assembly-CSharp-Editor.dll" result="Failed">
        <test-suite name="Fixture" fullname="Fixture" result="Failed">
          <test-case name="Passes" fullname="Fixture.Passes" result="Passed" />
          <test-case name="Fails" fullname="Fixture.Fails" result="Failed" />
        </test-suite>
      </test-suite>
    </test-run>
    """
)


class VerifyNUnitXmlTests(unittest.TestCase):
    def test_failed_name_digest_uses_only_newline_terminated_test_cases(self) -> None:
        root = ET.fromstring(SYNTHETIC_XML)

        names = VERIFY.find_failed_names(root)

        self.assertEqual(["Fixture.Fails"], names)
        self.assertEqual(
            hashlib.sha256(b"Fixture.Fails\n").hexdigest(),
            VERIFY.failed_name_digest(names),
        )

    def test_cli_accepts_exact_counts_and_historical_digest(self) -> None:
        digest = hashlib.sha256(b"Fixture.Fails\n").hexdigest()

        result = self._run_verifier(
            "--minimum-total",
            "2",
            "--maximum-failed",
            "1",
            "--expected-total",
            "2",
            "--expected-passed",
            "1",
            "--expected-failed",
            "1",
            "--expected-skipped",
            "0",
            "--expected-failure-sha256",
            digest,
        )

        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn("Test result verified.", result.stdout)

    def test_cli_rejects_a_minimum_satisfying_but_inexact_total(self) -> None:
        result = self._run_verifier(
            "--minimum-total",
            "1",
            "--maximum-failed",
            "1",
            "--expected-total",
            "3",
        )

        self.assertEqual(1, result.returncode, result.stdout + result.stderr)
        self.assertIn("expected exactly 3 tests, got 2", result.stdout)

    def _run_verifier(self, *arguments: str) -> subprocess.CompletedProcess[str]:
        with tempfile.TemporaryDirectory() as directory:
            xml_path = Path(directory) / "result.xml"
            xml_path.write_text(SYNTHETIC_XML, encoding="utf-8")
            return subprocess.run(
                [sys.executable, str(SCRIPT), str(xml_path), *arguments],
                check=False,
                capture_output=True,
                text=True,
            )


if __name__ == "__main__":
    unittest.main()
