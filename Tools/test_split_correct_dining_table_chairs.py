#!/usr/bin/env python3
"""Regression checks for splitting correct_dining_table_chairs.png."""

from __future__ import annotations

import importlib.util
import sys
from pathlib import Path

import numpy as np
from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "Tools" / "split_correct_dining_table_chairs.py"
SOURCE = ROOT / "Assets" / "Art" / "Objects" / "correct_dining_table_chairs.png"
OUT_DIR = ROOT / "Assets" / "Art" / "Objects" / "DiningTableChairSplits"
SCENE_DIR = OUT_DIR / "SceneOverlays"


EXPECTED_NAMES = (
    "dining_chair_left_01_front.png",
    "dining_chair_left_02_mid_front.png",
    "dining_chair_left_03_mid_back.png",
    "dining_chair_left_04_back.png",
    "dining_chair_head.png",
    "dining_chair_right_01_back.png",
    "dining_chair_right_02_mid_back.png",
    "dining_chair_right_03_mid_front.png",
    "dining_chair_right_04_front.png",
)

EXPECTED_SCENE_NAMES = tuple(name.replace(".png", "_scene.png") for name in EXPECTED_NAMES)


def load_splitter():
    spec = importlib.util.spec_from_file_location("split_correct_dining_table_chairs", SCRIPT)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Could not load {SCRIPT}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def main() -> int:
    splitter = load_splitter()
    splitter.main()

    source = Image.open(SOURCE).convert("RGBA")
    source_alpha = np.array(source.getchannel("A")) > 0
    restored = np.zeros(source_alpha.shape, dtype=np.uint16)
    failures: list[str] = []

    files = sorted(path.name for path in OUT_DIR.glob("dining_chair_*.png"))
    if tuple(files) != tuple(sorted(EXPECTED_NAMES)):
        failures.append(f"expected 9 named chair PNGs, got {files}")

    scene_files = sorted(path.name for path in SCENE_DIR.glob("dining_chair_*_scene.png"))
    if tuple(scene_files) != tuple(sorted(EXPECTED_SCENE_NAMES)):
        failures.append(f"expected 9 full-canvas scene overlay PNGs, got {scene_files}")

    for name in EXPECTED_NAMES:
        path = OUT_DIR / name
        if not path.exists():
            failures.append(f"missing {name}")
            continue
        image = Image.open(path).convert("RGBA")
        alpha = np.array(image.getchannel("A")) > 0
        origin = splitter.OUTPUT_ORIGINS[name]
        y0, x0 = origin[1], origin[0]
        restored[y0 : y0 + alpha.shape[0], x0 : x0 + alpha.shape[1]] += alpha.astype(np.uint16)

    scene_restored = np.zeros(source_alpha.shape, dtype=np.uint16)
    for name in EXPECTED_SCENE_NAMES:
        path = SCENE_DIR / name
        if not path.exists():
            failures.append(f"missing {name}")
            continue
        image = Image.open(path).convert("RGBA")
        if image.size != source.size:
            failures.append(f"{name} should keep source canvas {source.size}, got {image.size}")
            continue
        alpha = np.array(image.getchannel("A")) > 0
        scene_restored += alpha.astype(np.uint16)

    lost = int(np.logical_and(source_alpha, restored == 0).sum())
    overlaps = int((restored > 1).sum())
    extra = int(np.logical_and(~source_alpha, restored > 0).sum())
    scene_lost = int(np.logical_and(source_alpha, scene_restored == 0).sum())
    scene_overlaps = int((scene_restored > 1).sum())
    scene_extra = int(np.logical_and(~source_alpha, scene_restored > 0).sum())

    if lost:
        failures.append(f"{lost} visible source pixels were not assigned to a chair")
    if overlaps:
        failures.append(f"{overlaps} pixels were assigned to more than one chair")
    if extra:
        failures.append(f"{extra} transparent source pixels became visible")
    if scene_lost:
        failures.append(f"{scene_lost} visible source pixels were not assigned to a scene overlay")
    if scene_overlaps:
        failures.append(f"{scene_overlaps} scene overlay pixels were assigned to more than one chair")
    if scene_extra:
        failures.append(f"{scene_extra} transparent source pixels became visible in scene overlays")

    if failures:
        print("FAIL correct dining table chair split")
        for failure in failures:
            print(f"- {failure}")
        return 1

    print("PASS correct dining table chair split")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
