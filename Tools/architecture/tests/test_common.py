#!/usr/bin/env python3
from __future__ import annotations

import importlib.util
from pathlib import Path
import sys
import tempfile
import unittest


SCRIPT = Path(__file__).resolve().parents[1] / "common.py"
SPEC = importlib.util.spec_from_file_location("architecture_common", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
COMMON = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = COMMON
SPEC.loader.exec_module(COMMON)


class RuntimeCSharpFilesTests(unittest.TestCase):
    def test_excludes_editor_and_test_only_assemblies(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            runtime = root / "Assets" / "Scripts" / "RuntimeOwner.cs"
            editor = root / "Assets" / "Editor" / "EditorTool.cs"
            playmode = root / "Assets" / "Tests" / "PlayMode" / "RuntimeCharacterization.cs"

            for path in (runtime, editor, playmode):
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text("public sealed class Placeholder {}\n", encoding="utf-8")

            self.assertEqual([runtime], COMMON.runtime_csharp_files(root))


if __name__ == "__main__":
    unittest.main()
