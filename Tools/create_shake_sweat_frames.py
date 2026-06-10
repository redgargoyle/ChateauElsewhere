#!/usr/bin/env python3
"""Create whole-sprite shaking and sweating animation variants.

These are candidate library sprites built from existing whole-character cutouts.
The script does not construct limbs, hands, or faces from primitive shapes.
"""

from __future__ import annotations

import argparse
import json
import random
import sys
import uuid
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

from PIL import Image, ImageChops, ImageDraw, ImageFilter

sys.dont_write_bytecode = True

from postprocess_guest_sprite_sheet import write_meta


DEFAULT_LIBRARY_ROOT = Path("Assets/GeneratedSpriteLibrary")
FRAME_COUNT = 6

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
class SequenceSource:
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


def visible_bbox(image: Image.Image) -> tuple[int, int, int, int]:
    bbox = image.convert("RGBA").getchannel("A").getbbox()
    if bbox is None:
        raise ValueError("Image has no visible pixels")
    return bbox


def crop_visible(image: Image.Image, padding: int = 16) -> Image.Image:
    rgba = image.convert("RGBA")
    left, top, right, bottom = visible_bbox(rgba)
    left = max(0, left - padding)
    top = max(0, top - padding)
    right = min(rgba.width, right + padding)
    bottom = min(rgba.height, bottom + padding)
    return rgba.crop((left, top, right, bottom))


def ranked_files(folder: Path, patterns: Iterable[str]) -> list[Path]:
    files: list[Path] = []
    for pattern in patterns:
        files.extend(sorted(folder.glob(pattern)))
    seen: set[Path] = set()
    ranked: list[Path] = []
    for path in files:
        if path.is_file() and path not in seen:
            ranked.append(path)
            seen.add(path)
    return ranked


def pick_sources(guest_root: Path) -> SequenceSource | None:
    guest = guest_root.name
    panic = guest_root / "Panic"
    surprised = guest_root / "Surprised"
    idle = guest_root / "Idle"
    sitting = guest_root / "Sitting"
    if not sitting.exists():
        return None

    standing = ranked_files(
        panic,
        (
            f"{guest}_panic_recoil_scream_02.png",
            f"{guest}_panic_scream_hands_up_02_originalstyle_01.png",
            f"{guest}_panic_scream_hands_up_02.png",
            f"{guest}_panic_scream_hands_up_01.png",
            f"{guest}_panic_cover_face_recoil_01.png",
            "*.png",
        ),
    )
    if not standing:
        standing = ranked_files(
            surprised,
            (
                f"{guest}_surprised_hand_to_mouth_gasp_02.png",
                f"{guest}_surprised_front_startled_01.png",
                "*.png",
            ),
        )
    if not standing:
        standing = ranked_files(idle, (f"{guest}_idle_anxious_clutching_02.png", "*.png"))

    seated = ranked_files(
        sitting,
        (
            f"{guest}_sitting_hunched_shaken_02.png",
            f"{guest}_sitting_tense_hands_clasped_02_originalstyle_01.png",
            f"{guest}_sitting_tense_hands_clasped_02.png",
            f"{guest}_sitting_tense_hands_clasped_01.png",
            f"{guest}_sitting_idle_plain_01.png",
            "*.png",
        ),
    )
    if not standing or not seated:
        return None
    return SequenceSource(guest=guest, standing=standing[0], sitting=seated[0])


def paste_bottom_center(canvas: Image.Image, sprite: Image.Image, bottom: int, center_x: int, x_offset: int) -> None:
    left = int(center_x - sprite.width / 2 + x_offset)
    top = int(bottom - sprite.height)
    canvas.alpha_composite(sprite, (left, top))


def transform_whole_sprite(sprite: Image.Image, frame_index: int, seated: bool) -> Image.Image:
    jitter = (-3, 2, -2, 3, -1, 2)
    angles = (-1.35, 1.05, -0.85, 1.25, -0.55, 0.75)
    squash = (0.995, 1.010, 0.990, 1.012, 0.997, 1.006)
    stretch = (1.010, 0.992, 1.014, 0.990, 1.006, 0.996)
    idx = frame_index % FRAME_COUNT
    scale = 0.72 if seated else 1.0
    width = max(1, int(sprite.width * squash[idx]))
    height = max(1, int(sprite.height * stretch[idx]))
    resized = sprite.resize((width, height), Image.Resampling.LANCZOS)
    rotated = resized.rotate(angles[idx] * scale, resample=Image.Resampling.BICUBIC, expand=True)
    return rotated


def alpha_mask(image: Image.Image) -> Image.Image:
    alpha = image.getchannel("A")
    return alpha.point(lambda value: 255 if value > 20 else 0)


def add_sweat_highlights(sprite: Image.Image, frame_index: int, seed: int, intensity: float) -> Image.Image:
    rgba = sprite.convert("RGBA")
    mask = alpha_mask(rgba)
    overlay = Image.new("RGBA", rgba.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay, "RGBA")
    rng = random.Random(seed + frame_index * 131)

    left, top, right, bottom = visible_bbox(rgba)
    width = right - left
    height = bottom - top
    regions = [
        (0.27, 0.08, 0.73, 0.30, 15),
        (0.25, 0.28, 0.75, 0.52, 9),
    ]
    frame_drift = (frame_index % FRAME_COUNT) * 1.2
    for x0, y0, x1, y1, count in regions:
        for _ in range(count):
            x = int(left + width * rng.uniform(x0, x1))
            y = int(top + height * rng.uniform(y0, y1) + frame_drift * rng.uniform(0.0, 1.0))
            if x < 0 or y < 0 or x >= rgba.width or y >= rgba.height or mask.getpixel((x, y)) == 0:
                continue
            length = rng.randint(4, 12)
            alpha = min(155, int(rng.randint(62, 125) * intensity))
            shade = min(72, int(rng.randint(26, 52) * intensity))
            draw.line((x + 1, y + 1, x + 1, y + length), fill=(23, 54, 50, shade), width=1)
            draw.line((x, y, x - rng.choice((0, 1)), y + length), fill=(226, 238, 218, alpha), width=1)
            if rng.random() < 0.50:
                radius = rng.choice((1, 2, 2))
                draw.ellipse(
                    (x - radius, y + length - radius, x + radius, y + length + radius),
                    fill=(229, 238, 218, max(36, alpha - 20)),
                )

    head_top = int(top + height * 0.09)
    head_bottom = int(top + height * 0.31)
    edge_rows = [int(head_top + (head_bottom - head_top) * value) for value in (0.16, 0.34, 0.52, 0.70)]
    for y in edge_rows:
        if y < 0 or y >= rgba.height:
            continue
        visible_xs = [x for x in range(max(0, left), min(rgba.width, right)) if mask.getpixel((x, y)) > 0]
        if not visible_xs:
            continue
        side = rng.choice((-1, 1))
        edge_x = min(visible_xs) if side < 0 else max(visible_xs)
        x = edge_x + side * rng.randint(3, 9)
        if x <= 1 or x >= rgba.width - 2:
            continue
        length = rng.randint(5, 10)
        alpha = min(145, int(rng.randint(84, 128) * intensity))
        draw.line((x, y, x + side, y + length), fill=(224, 237, 219, alpha), width=1)
        draw.ellipse((x - 1, y + length - 1, x + 2, y + length + 2), fill=(226, 238, 220, max(52, alpha - 26)))

    soft_alpha = ImageChops.multiply(overlay.getchannel("A"), mask)
    edge_alpha = overlay.getchannel("A").point(lambda value: value if value > 96 else 0)
    soft_alpha = ImageChops.lighter(soft_alpha, edge_alpha)
    overlay = Image.merge("RGBA", (*overlay.convert("RGB").split(), soft_alpha))
    overlay = overlay.filter(ImageFilter.GaussianBlur(0.12))
    return Image.alpha_composite(rgba, overlay)


def render_frame(source: Image.Image, frame_index: int, seated: bool, sweaty: bool, seed: int) -> Image.Image:
    cropped = crop_visible(source)
    if sweaty:
        cropped = add_sweat_highlights(cropped, frame_index, seed, intensity=1.0 if seated else 0.9)
    sprite = transform_whole_sprite(cropped, frame_index, seated)
    canvas_w = max(260, cropped.width + 96)
    canvas_h = max(360, cropped.height + 96)
    canvas = Image.new("RGBA", (canvas_w, canvas_h), (0, 0, 0, 0))
    jitter = (-3, 2, -2, 3, -1, 2)
    lift = (0, 1, -1, 0, 1, -1)
    paste_bottom_center(
        canvas,
        sprite,
        canvas_h - 28 + lift[frame_index % FRAME_COUNT],
        canvas_w // 2,
        jitter[frame_index % FRAME_COUNT],
    )
    return canvas


def write_manifest(folder: Path, source: SequenceSource, category: str, frames: dict[str, list[Path]]) -> None:
    manifest = {
        "category": category,
        "method": "whole-sprite tremble variants from existing generated cutouts",
        "no_drawn_limb_or_face_overlays": True,
        "standing_source": source.standing.as_posix(),
        "sitting_source": source.sitting.as_posix(),
        "frames": {name: [path.as_posix() for path in paths] for name, paths in frames.items()},
    }
    path = folder / f"{source.guest}_{category.lower()}_manifest.json"
    path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    write_text_meta(path)


def write_sequence(
    out_dir: Path,
    source: SequenceSource,
    base_image: Image.Image,
    category: str,
    sequence_name: str,
    seated: bool,
    sweaty: bool,
    force: bool,
) -> tuple[int, int, list[Path]]:
    written = 0
    skipped = 0
    paths: list[Path] = []
    seed = sum(ord(char) for char in f"{source.guest}:{category}:{sequence_name}")
    for frame in range(FRAME_COUNT):
        out_path = out_dir / f"{source.guest}_{sequence_name}_{frame + 1:02d}.png"
        paths.append(out_path)
        if out_path.exists() and not force:
            skipped += 1
            continue
        image = render_frame(base_image, frame, seated=seated, sweaty=sweaty, seed=seed)
        image.save(out_path)
        meta = Path(str(out_path) + ".meta")
        if not meta.exists():
            write_meta(out_path)
        written += 1
    return written, skipped, paths


def main() -> None:
    args = parse_args()
    root = args.library_root
    requested = set(args.guest)
    guest_roots = sorted(path for path in root.iterdir() if path.is_dir() and not path.name.startswith("_"))
    if requested:
        guest_roots = [path for path in guest_roots if path.name in requested]

    ensure_folder(root, root)
    total_written = 0
    total_skipped = 0
    missing: list[str] = []
    for guest_root in guest_roots:
        source = pick_sources(guest_root)
        if source is None:
            missing.append(guest_root.name)
            continue

        standing = Image.open(source.standing).convert("RGBA")
        sitting = Image.open(source.sitting).convert("RGBA")
        for category, sweaty in (("Shaking", False), ("Sweating", True)):
            out_dir = guest_root / category
            ensure_folder(out_dir, root)
            frames: dict[str, list[Path]] = {}
            for sequence_name, base_image, seated in (
                (f"{category.lower()}_standing_panic", standing, False),
                (f"{category.lower()}_seated_panic", sitting, True),
            ):
                written, skipped, paths = write_sequence(
                    out_dir,
                    source,
                    base_image,
                    category,
                    sequence_name,
                    seated=seated,
                    sweaty=sweaty,
                    force=args.force,
                )
                total_written += written
                total_skipped += skipped
                frames[sequence_name] = paths
            write_manifest(out_dir, source, category, frames)

    print(f"Guests considered: {len(guest_roots)}")
    print(f"Shake/sweat frames written: {total_written}")
    print(f"Shake/sweat frames skipped: {total_skipped}")
    if missing:
        print("Missing source pairs:", ", ".join(missing))


if __name__ == "__main__":
    main()
