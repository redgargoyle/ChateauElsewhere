#!/usr/bin/env python3
"""Create non-destructive watercolor/pixel style-matched sprite copies."""

from __future__ import annotations

import argparse
import colorsys
import hashlib
import random
import sys
import uuid
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageChops, ImageEnhance, ImageFilter, ImageOps, ImageStat

sys.dont_write_bytecode = True

from postprocess_guest_sprite_sheet import write_meta


SKIP_DIRS = {"_ContactSheets"}
SPRITE_CATEGORIES = {
    "DiningRoomChair",
    "DrawingRoomCouch",
    "Idle",
    "Panic",
    "Sitting",
    "Surprised",
    "Walking",
}


FOLDER_META_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


DEFAULT_REFERENCES = (
    Path("Assets/AnimationLibrary/CountessElowenDusk/reference/full_body"),
    Path("Assets/AnimationLibrary/LadySabineMarrow/reference/full_body"),
    Path("Assets/AnimationLibrary/LordAmbroseVeil/reference/full_body"),
)


@dataclass(frozen=True)
class StyleProfile:
    saturation: float
    brightness: float
    contrast: float


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-root", default=Path("Assets/GeneratedSpriteLibrary"), type=Path)
    parser.add_argument("--out-root", default=Path("Assets/GeneratedSpriteLibraryStyleMatched"), type=Path)
    parser.add_argument("--reference", action="append", type=Path, default=[])
    parser.add_argument("--strength", type=float, default=0.48)
    parser.add_argument("--limit", type=int, default=0)
    parser.add_argument("--force", action="store_true")
    return parser.parse_args()


def unity_guid() -> str:
    return uuid.uuid4().hex


def write_folder_meta(folder: Path) -> None:
    meta_path = Path(str(folder) + ".meta")
    if meta_path.exists():
        return
    meta_path.write_text(FOLDER_META_TEMPLATE.format(guid=unity_guid()), encoding="utf-8")


def ensure_folder(folder: Path, meta_root: Path | None = None) -> None:
    folder.mkdir(parents=True, exist_ok=True)
    boundary = meta_root.resolve() if meta_root else folder.resolve()
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


def iter_main_sprites(root: Path) -> list[Path]:
    files: list[Path] = []
    for path in root.glob("*/*/*.png"):
        if any(part in SKIP_DIRS for part in path.parts):
            continue
        if path.parent.name not in SPRITE_CATEGORIES:
            continue
        files.append(path)
    return sorted(files)


def iter_reference_images(paths: list[Path]) -> list[Path]:
    refs: list[Path] = []
    roots = paths or list(DEFAULT_REFERENCES)
    for root in roots:
        if root.is_file() and root.suffix.lower() == ".png":
            refs.append(root)
        elif root.exists():
            refs.extend(sorted(root.rglob("*.png")))
    return refs


def visible_pixel_sample(image: Image.Image, max_samples: int = 20000) -> list[tuple[int, int, int]]:
    rgba = image.convert("RGBA")
    pixels = rgba.get_flattened_data() if hasattr(rgba, "get_flattened_data") else rgba.getdata()
    total = len(pixels)
    step = max(1, total // max_samples)
    sample: list[tuple[int, int, int]] = []
    for i in range(0, total, step):
        r, g, b, a = pixels[i]
        if a <= 48:
            continue
        # Avoid white/checker export backgrounds in opaque contact sheets.
        if r > 225 and g > 225 and b > 225:
            continue
        if abs(r - g) < 5 and abs(g - b) < 5 and 180 <= r <= 235:
            continue
        sample.append((r, g, b))
    return sample


def compute_profile(reference_paths: list[Path]) -> StyleProfile:
    pixels: list[tuple[int, int, int]] = []
    for path in reference_paths:
        try:
            pixels.extend(visible_pixel_sample(Image.open(path), max_samples=2500))
        except OSError:
            continue
    if not pixels:
        return StyleProfile(saturation=0.43, brightness=0.43, contrast=58.0)

    hsv = [colorsys.rgb_to_hsv(r / 255.0, g / 255.0, b / 255.0) for r, g, b in pixels]
    saturation = sum(item[1] for item in hsv) / len(hsv)
    brightness = sum(item[2] for item in hsv) / len(hsv)
    lum_image = Image.new("L", (len(pixels), 1))
    lum_image.putdata([int(0.2126 * r + 0.7152 * g + 0.0722 * b) for r, g, b in pixels])
    contrast = ImageStat.Stat(lum_image).stddev[0]
    return StyleProfile(saturation=saturation, brightness=brightness, contrast=contrast)


def image_profile(image: Image.Image) -> StyleProfile:
    pixels = visible_pixel_sample(image)
    if not pixels:
        return StyleProfile(saturation=0.43, brightness=0.43, contrast=58.0)
    hsv = [colorsys.rgb_to_hsv(r / 255.0, g / 255.0, b / 255.0) for r, g, b in pixels]
    saturation = sum(item[1] for item in hsv) / len(hsv)
    brightness = sum(item[2] for item in hsv) / len(hsv)
    lum_image = Image.new("L", (len(pixels), 1))
    lum_image.putdata([int(0.2126 * r + 0.7152 * g + 0.0722 * b) for r, g, b in pixels])
    contrast = ImageStat.Stat(lum_image).stddev[0]
    return StyleProfile(saturation=saturation, brightness=brightness, contrast=contrast)


def clamp(value: float, low: float, high: float) -> float:
    return min(high, max(low, value))


def stable_seed(path: Path) -> int:
    digest = hashlib.sha256(str(path).replace("\\", "/").encode("utf-8")).digest()
    return int.from_bytes(digest[:8], "big")


def low_frequency_noise(size: tuple[int, int], seed: int, scale: int, amplitude: int) -> Image.Image:
    width, height = size
    low_size = (max(1, (width + scale - 1) // scale), max(1, (height + scale - 1) // scale))
    rng = random.Random(seed)
    data = bytes(clamp(128 + rng.randint(-amplitude, amplitude), 0, 255).__int__() for _ in range(low_size[0] * low_size[1]))
    return Image.frombytes("L", low_size, data).resize(size, Image.Resampling.BICUBIC)


def apply_style(image: Image.Image, path: Path, reference_profile: StyleProfile, strength: float) -> Image.Image:
    source = image.convert("RGBA")
    alpha = source.getchannel("A")
    visible_mask = alpha.point(lambda value: 255 if value > 8 else 0)
    rgb = source.convert("RGB")
    current = image_profile(source)
    seed = stable_seed(path)

    saturation_factor = clamp(1.0 + (reference_profile.saturation - current.saturation) * 0.45 * strength, 0.86, 1.04)
    brightness_factor = clamp(1.0 + (reference_profile.brightness - current.brightness) * 0.10 * strength, 0.97, 1.03)
    contrast_factor = clamp(0.98 + (reference_profile.contrast - current.contrast) / 320.0 * strength, 0.93, 1.02)

    rgb = ImageEnhance.Color(rgb).enhance(saturation_factor)
    rgb = ImageEnhance.Brightness(rgb).enhance(brightness_factor)
    rgb = ImageEnhance.Contrast(rgb).enhance(contrast_factor)

    # Slight sprite-scale roughness, blended so forms and facial details survive.
    pixelated = rgb.resize((max(1, int(rgb.width * 0.74)), max(1, int(rgb.height * 0.74))), Image.Resampling.BILINEAR)
    pixelated = pixelated.resize(rgb.size, Image.Resampling.NEAREST)
    rgb = Image.blend(rgb, pixelated, 0.055 * strength)

    # Watercolor flattening: soften broad paint regions, then reintroduce line texture.
    median = rgb.filter(ImageFilter.MedianFilter(3))
    soft = rgb.filter(ImageFilter.GaussianBlur(0.32))
    rgb = Image.blend(rgb, median, 0.13 * strength)
    rgb = Image.blend(rgb, soft, 0.065 * strength)

    poster = ImageOps.posterize(rgb, 6)
    rgb = Image.blend(rgb, poster, 0.075 * strength)

    paper_noise = low_frequency_noise(rgb.size, seed, scale=10, amplitude=22)
    paper = ImageOps.colorize(paper_noise, black=(222, 214, 190), white=(255, 250, 230))
    rgb = Image.composite(Image.blend(rgb, paper, 0.045 * strength), rgb, visible_mask)

    fine_noise = low_frequency_noise(rgb.size, seed ^ 0xA531, scale=3, amplitude=16)
    fine = ImageOps.colorize(fine_noise, black=(32, 28, 22), white=(255, 252, 238))
    rgb = Image.composite(Image.blend(rgb, fine, 0.014 * strength), rgb, visible_mask)

    gray = ImageOps.grayscale(rgb)
    edges = gray.filter(ImageFilter.FIND_EDGES).filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.GaussianBlur(0.25))
    edges = edges.point(lambda value: 255 if value > 34 else int(value * 3.2))
    darkened = Image.blend(rgb, Image.new("RGB", rgb.size, (26, 21, 16)), 0.20 * strength)
    rgb = Image.composite(darkened, rgb, ImageChops.multiply(edges, visible_mask))

    # Bring back just enough crispness to keep the assets readable as sprites.
    rgb = rgb.filter(ImageFilter.UnsharpMask(radius=0.7, percent=int(65 * strength), threshold=5))

    return Image.merge("RGBA", (*rgb.split(), alpha))


def write_index(out_root: Path, count: int, profile: StyleProfile, references: list[Path], source_root: Path) -> None:
    lines = [
        "# Style-Matched Generated Sprite Library",
        "",
        f"Source root: `{source_root.as_posix()}`",
        "",
        "This folder mirrors the generated sprite library with non-destructive filtered copies.",
        "The original generated sprites and original animation/reference sprites are not edited.",
        "",
        f"Style-matched main sprite PNGs: {count}",
        f"Reference saturation: {profile.saturation:.3f}",
        f"Reference brightness: {profile.brightness:.3f}",
        f"Reference luminance contrast: {profile.contrast:.2f}",
        "",
        "Reference inputs:",
    ]
    lines.extend(f"- `{path.as_posix()}`" for path in references[:40])
    if len(references) > 40:
        lines.append(f"- ...and {len(references) - 40} more reference PNGs")
    lines.extend(
        [
            "",
            "Filter intent:",
            "- Slightly mute overly clean generated colors toward the existing sprite references.",
            "- Add subtle watercolor paper/grain variation.",
            "- Add light sprite-scale pixel roughness while preserving silhouettes and alpha.",
            "- Keep ink edges readable without drawing new limbs, shapes, or animation overlays.",
            "",
        ]
    )
    ensure_folder(out_root, out_root)
    index_path = out_root / "STYLE_MATCH_INDEX.md"
    index_path.write_text("\n".join(lines), encoding="utf-8")


def main() -> None:
    args = parse_args()
    source_root = args.source_root
    out_root = args.out_root
    references = iter_reference_images(args.reference)
    profile = compute_profile(references)
    sprites = iter_main_sprites(source_root)
    if args.limit:
        sprites = sprites[: args.limit]

    ensure_folder(out_root, out_root)
    written = 0
    for sprite in sprites:
        relative = sprite.relative_to(source_root)
        out_path = out_root / relative
        if out_path.exists() and not args.force:
            continue
        ensure_folder(out_path.parent, out_root)
        styled = apply_style(Image.open(sprite), sprite, profile, clamp(args.strength, 0.0, 1.0))
        styled.save(out_path)
        write_meta(out_path)
        written += 1

    write_index(out_root, len(sprites), profile, references, source_root)
    print(f"Reference PNGs: {len(references)}")
    print(f"Style-matched sprite paths considered: {len(sprites)}")
    print(f"Style-matched sprites written: {written}")
    print(f"Output root: {out_root}")


if __name__ == "__main__":
    main()
