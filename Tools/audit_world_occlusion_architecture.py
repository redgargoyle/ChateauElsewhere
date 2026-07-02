#!/usr/bin/env python3
"""Audit the project-wide world occlusion architecture policy."""

from __future__ import annotations

import sys
from pathlib import Path


TOOLS_DIR = Path(__file__).resolve().parent

if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from audit_y_axis_occlusion_policy import main  # noqa: E402


if __name__ == "__main__":
    raise SystemExit(main())
