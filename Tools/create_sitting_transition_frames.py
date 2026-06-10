#!/usr/bin/env python3
"""Create whole-sprite stand-to-sit transition frames.

This tool intentionally does not draw replacement limbs, hands, or face shapes.
It creates non-destructive in-between cutouts from existing whole-character
generated sprites, then leaves the stronger watercolor room-style pass to
Tools/style_match_generated_sprites.py.
"""

from __future__ import annotations

import argparse
import json
import uuid
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

from PIL import Image

from postprocess_guest_sprite_sheet import write_meta


DEFAULT_LIBRARY_ROOT = Path("Assets/GeneratedSpriteLibrary")
FRAME_T_VALUES = (0.18, 0.38, 0.62, 0.82)

FOLDER_META_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

TEXT_META_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


@dataclass(frozen=True)
class SourcePair:
    guest: str
    standing: Path
    sitting: Path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--library-root", default=DEFAULT_LIBRARY_ROOT, type=Path)
    parser.add_argument("--guest", action="append", default=[])
    parser.add_argument("--force", action="store_true")
    return parser.parse_args()


def write_folder_meta(folder: Path) -> None:
    meta = Path(str(folder) + ".meta")
    if meta.exists():
        return
    meta.write_text(FOLDER_META_TEMPLATE.format(guid=uuid.uuid4().hex), encoding="utf-8")


def write_text_meta(path: Path) -> None:
    meta = Path(str(path) + ".meta")
    if meta.exists():
        return
    meta.write_text(TEXT_META_TEMPLATE.format(guid=uuid.uuid4().hex), encoding="utf-8")


def ensure_folder(folder: Path, boundary: Path) -> None:
    folder.mkdir(parents=True, exist_ok=True)
    boundary = boundary.resolve()
    current = folder.resolve()
    chain: list[Path] = [current]
    while current != boundary:
        parent = current.parent
        if parent == current or not str(current).startswith(str(boundary)):
            break
        current = parent
        chain.append(current)
    for item in reversed(chain):
        if item.exists():
            write_folder_meta(item)


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int]:
    bbox = image.convert("RGBA").getchannel("A").getbbox()
    if bbox is None:
        raise ValueError("Image has no visible pixels")
    return bbox


def crop_visible(image: Image.Image, padding: int = 10) -> Image.Image:
    rgba = image.convert("RGBA")
    left, top, right, bottom = alpha_bbox(rgba)
    left = max(0, left - padding)
    top = max(0, top - padding)
    right = min(rgba.width, right + padding)
    bottom = min(rgba.height, bottom + padding)
    return rgba.crop((left, top, right, bottom))


def resize_to_height(image: Image.Image, height: int, width_factor: float) -> Image.Image:
    image = crop_visible(image)
    height = max(1, height)
    width = max(1, int(image.width * (height / image.height) * width_factor))
    return image.resize((width, height), Image.Resampling.LANCZOS)


def paste_bottom_center(
    canvas: Image.Image,
    sprite: Image.Image,
    bottom: int,
    center_x: int,
    x_offset: int = 0,
) -> None:
    left = int(center_x - sprite.width / 2 + x_offset)
    top = int(bottom - sprite.height)
    canvas.alpha_composite(sprite, (left, top))


def trim_transparent(image: Image.Image, padding: int = 12) -> Image.Image:
    bbox = image.getchannel("A").getbbox()
    if bbox is None:
        return image
    left, top, right, bottom = bbox
    left = max(0, left - padding)
    top = max(0, top - padding)
    right = min(image.width, right + padding)
    bottom = min(image.height, bottom + padding)
    return image.crop((left, top, right, bottom))


def smoothstep(value: float) -> float:
    return value * value * (3.0 - 2.0 * value)


def render_transition_frame(standing: Image.Image, sitting: Image.Image, t: float) -> Image.Image:
    standing_bbox = crop_visible(standing)
    sitting_bbox = crop_visible(sitting)

    canvas_w = int(max(standing_bbox.width * 1.55, sitting_bbox.width * 1.45, 420))
    canvas_h = int(max(standing_bbox.height * 1.22, sitting_bbox.height * 1.32, 560))
    canvas = Image.new("RGBA", (canvas_w, canvas_h), (0, 0, 0, 0))
    bottom = canvas_h - 24
    center_x = canvas_w // 2

    if t < 0.5:
        stage = smoothstep(t / 0.5)
        height = int(standing_bbox.height * (1.0 - 0.28 * stage))
        width_factor = 1.0 + 0.18 * stage
        sprite = resize_to_height(standing, height, width_factor)
        # The source sprite sinks and widens slightly as the seated sprite resolves.
        # This is whole-sprite resampling, not drawn-on anatomical edits.
        paste_bottom_center(
            canvas,
            sprite,
            bottom - int(standing_bbox.height * 0.018 * stage),
            center_x,
            x_offset=-int(standing_bbox.width * 0.018 * stage),
        )
    else:
        stage = smoothstep((t - 0.5) / 0.5)
        height = int(sitting_bbox.height * (0.88 + 0.12 * stage))
        width_factor = 0.93 + 0.07 * stage
        sprite = resize_to_height(sitting, height, width_factor)
        paste_bottom_center(
            canvas,
            sprite,
            bottom,
            center_x,
            x_offset=int(sitting_bbox.width * 0.010 * stage),
        )
    return trim_transparent(canvas)


def ranked_files(folder: Path, patterns: Iterable[str]) -> list[Path]:
    files: list[Path] = []
    for pattern in patterns:
        files.extend(sorted(folder.glob(pattern)))
    return [path for path in files if path.is_file()]


def pick_source_pair(guest_root: Path) -> SourcePair | None:
    guest = guest_root.name
    idle_dir = guest_root / "Idle"
    sitting_dir = guest_root / "Sitting"
    if not idle_dir.exists() or not sitting_dir.exists():
        return None

    standing = ranked_files(
        idle_dir,
        (
            f"{guest}_idle_tense_three_quarter_02_originalstyle_01.png",
            f"{guest}_idle_tense_three_quarter_02.png",
            f"{guest}_idle_tense_three_quarter_01.png",
            f"{guest}_idle_front_neutral_01.png",
            "*.png",
        ),
    )
    sitting = ranked_files(
        sitting_dir,
        (
            f"{guest}_sitting_tense_hands_clasped_02_originalstyle_01.png",
            f"{guest}_sitting_hunched_shaken_02.png",
            f"{guest}_sitting_tense_hands_clasped_02.png",
            f"{guest}_sitting_tense_hands_clasped_01.png",
            f"{guest}_sitting_idle_plain_01.png",
            "*.png",
        ),
    )
    if not standing or not sitting:
        return None
    return SourcePair(guest=guest, standing=standing[0], sitting=sitting[0])


def write_manifest(folder: Path, pair: SourcePair, frame_paths: list[Path]) -> None:
    manifest = {
        "action": "stand_to_sit",
        "method": "whole-sprite alpha transition from existing generated cutouts",
        "no_drawn_limb_overlays": True,
        "standing_source": pair.standing.as_posix(),
        "sitting_source": pair.sitting.as_posix(),
        "frames": [path.as_posix() for path in frame_paths],
    }
    path = folder / f"{pair.guest}_transition_stand_to_sit_manifest.json"
    path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    write_text_meta(path)


def main() -> None:
    args = parse_args()
    root = args.library_root
    requested = set(args.guest)
    guest_roots = sorted(path for path in root.iterdir() if path.is_dir() and not path.name.startswith("_"))
    if requested:
        guest_roots = [path for path in guest_roots if path.name in requested]

    ensure_folder(root, root)
    written = 0
    skipped = 0
    missing: list[str] = []
    for guest_root in guest_roots:
        pair = pick_source_pair(guest_root)
        if pair is None:
            missing.append(guest_root.name)
            continue

        out_dir = guest_root / "Transitions"
        ensure_folder(out_dir, root)
        standing = Image.open(pair.standing).convert("RGBA")
        sitting = Image.open(pair.sitting).convert("RGBA")
        frame_paths: list[Path] = []
        for index, t_value in enumerate(FRAME_T_VALUES, start=1):
            out_path = out_dir / f"{pair.guest}_transition_stand_to_sit_{index:02d}.png"
            frame_paths.append(out_path)
            if out_path.exists() and not args.force:
                skipped += 1
                continue
            frame = render_transition_frame(standing, sitting, t_value)
            frame.save(out_path)
            meta = Path(str(out_path) + ".meta")
            if not meta.exists():
                write_meta(out_path)
            written += 1
        write_manifest(out_dir, pair, frame_paths)

    print(f"Guests considered: {len(guest_roots)}")
    print(f"Transition frames written: {written}")
    print(f"Transition frames skipped: {skipped}")
    if missing:
        print("Missing source pairs:", ", ".join(missing))


if __name__ == "__main__":
    main()
