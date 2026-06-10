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

from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter, ImageOps, ImageStat

sys.dont_write_bytecode = True

from postprocess_guest_sprite_sheet import write_meta


SKIP_DIRS = {"_ContactSheets"}
SPRITE_CATEGORIES = {
    "DiningRoomChair",
    "DrawingRoomCouch",
    "Idle",
    "Panic",
    "Shaking",
    "Sitting",
    "Surprised",
    "Sweating",
    "Transitions",
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
    Path("Assets/Art/Final Images (DO NOT EDIT)/drawing room 2.png"),
    Path("Assets/Art/Library/AnimationLibrary/CountessElowenDusk/reference/full_body"),
    Path("Assets/Art/Library/AnimationLibrary/LadySabineMarrow/reference/full_body"),
    Path("Assets/Art/Library/AnimationLibrary/LordAmbroseVeil/reference/full_body"),
)

DEFAULT_ROOM_REFERENCE = Path("Assets/Art/Final Images (DO NOT EDIT)/drawing room 2.png")


@dataclass(frozen=True)
class StyleProfile:
    saturation: float
    brightness: float
    contrast: float
    edge_mean: float


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-root", default=Path("Assets/Art/Library/GeneratedSprites/Raw"), type=Path)
    parser.add_argument("--out-root", default=Path("Assets/Art/Library/GeneratedSprites/StyleMatched"), type=Path)
    parser.add_argument("--reference", action="append", type=Path, default=[])
    parser.add_argument("--room-reference", default=DEFAULT_ROOM_REFERENCE, type=Path)
    parser.add_argument("--strength", type=float, default=0.9)
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
    edge_means: list[float] = []
    for path in reference_paths:
        try:
            image = Image.open(path)
            pixels.extend(visible_pixel_sample(image, max_samples=2500))
            edge_means.append(edge_mean(image))
        except OSError:
            continue
    if not pixels:
        return StyleProfile(saturation=0.50, brightness=0.42, contrast=45.0, edge_mean=36.0)

    hsv = [colorsys.rgb_to_hsv(r / 255.0, g / 255.0, b / 255.0) for r, g, b in pixels]
    saturation = sum(item[1] for item in hsv) / len(hsv)
    brightness = sum(item[2] for item in hsv) / len(hsv)
    lum_image = Image.new("L", (len(pixels), 1))
    lum_image.putdata([int(0.2126 * r + 0.7152 * g + 0.0722 * b) for r, g, b in pixels])
    contrast = ImageStat.Stat(lum_image).stddev[0]
    edge = sum(edge_means) / len(edge_means) if edge_means else 36.0
    return StyleProfile(saturation=saturation, brightness=brightness, contrast=contrast, edge_mean=edge)


def image_profile(image: Image.Image) -> StyleProfile:
    pixels = visible_pixel_sample(image)
    if not pixels:
        return StyleProfile(saturation=0.50, brightness=0.42, contrast=45.0, edge_mean=36.0)
    hsv = [colorsys.rgb_to_hsv(r / 255.0, g / 255.0, b / 255.0) for r, g, b in pixels]
    saturation = sum(item[1] for item in hsv) / len(hsv)
    brightness = sum(item[2] for item in hsv) / len(hsv)
    lum_image = Image.new("L", (len(pixels), 1))
    lum_image.putdata([int(0.2126 * r + 0.7152 * g + 0.0722 * b) for r, g, b in pixels])
    contrast = ImageStat.Stat(lum_image).stddev[0]
    return StyleProfile(saturation=saturation, brightness=brightness, contrast=contrast, edge_mean=edge_mean(image))


def edge_mean(image: Image.Image) -> float:
    rgba = image.convert("RGBA")
    alpha = rgba.getchannel("A")
    rgb = rgba.convert("RGB")
    gray = ImageOps.grayscale(rgb)
    edges = gray.filter(ImageFilter.FIND_EDGES)
    visible = alpha.point(lambda value: 255 if value > 48 else 0)
    stat = ImageStat.Stat(edges, visible)
    return stat.mean[0]


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


def reference_crop(reference: Image.Image, size: tuple[int, int], seed: int) -> Image.Image:
    ref = reference.convert("RGB")
    width, height = ref.size
    target_w, target_h = size
    target_ratio = target_w / target_h
    ref_ratio = width / height
    if ref_ratio > target_ratio:
        crop_h = height
        crop_w = max(1, int(crop_h * target_ratio))
    else:
        crop_w = width
        crop_h = max(1, int(crop_w / target_ratio))
    rng = random.Random(seed ^ 0x5A17)
    left = rng.randint(0, max(0, width - crop_w))
    top = rng.randint(0, max(0, height - crop_h))
    return ref.crop((left, top, left + crop_w, top + crop_h)).resize(size, Image.Resampling.BICUBIC)


def room_texture_maps(reference: Image.Image | None, size: tuple[int, int], seed: int) -> tuple[Image.Image, Image.Image]:
    if reference is None:
        detail = low_frequency_noise(size, seed ^ 0x4211, scale=9, amplitude=34)
        line = low_frequency_noise(size, seed ^ 0x7129, scale=4, amplitude=42)
        return detail, line
    crop = reference_crop(reference, size, seed)
    gray = ImageOps.grayscale(crop)
    high = ImageChops.subtract(gray, gray.filter(ImageFilter.GaussianBlur(5.5)), offset=128)
    high = ImageOps.autocontrast(high.filter(ImageFilter.GaussianBlur(0.25)))
    line = gray.filter(ImageFilter.FIND_EDGES).filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.GaussianBlur(0.2))
    line = ImageOps.autocontrast(line)
    return high, line


def hatch_mask(size: tuple[int, int], seed: int) -> Image.Image:
    width, height = size
    rng = random.Random(seed ^ 0xBEAD)
    mask = Image.new("L", size, 0)
    draw = ImageDraw.Draw(mask)
    spacing = rng.choice((7, 8, 9))
    jitter = rng.randint(0, spacing)
    for offset in range(-height, width + height, spacing):
        value = rng.randint(18, 44)
        draw.line((offset + jitter, height, offset + height + jitter, 0), fill=value, width=1)
    for y in range(rng.randint(0, 5), height, spacing * 2):
        draw.line((0, y, width, y + rng.randint(-2, 2)), fill=rng.randint(8, 22), width=1)
    return mask.filter(ImageFilter.GaussianBlur(0.35))


def retarget_rgb_profile(
    rgb: Image.Image,
    alpha: Image.Image,
    reference_profile: StyleProfile,
    strength: float,
) -> Image.Image:
    profiled = Image.merge("RGBA", (*rgb.split(), alpha))
    current = image_profile(profiled)
    color_factor = clamp(1.0 + (reference_profile.saturation - current.saturation) * 0.90 * strength, 0.86, 1.38)
    brightness_factor = clamp(1.0 + (reference_profile.brightness - current.brightness) * 0.78 * strength, 0.90, 1.28)
    contrast_factor = clamp(1.0 + (reference_profile.contrast - current.contrast) / 180.0 * strength, 0.86, 1.20)
    rgb = ImageEnhance.Color(rgb).enhance(color_factor)
    rgb = ImageEnhance.Brightness(rgb).enhance(brightness_factor)
    rgb = ImageEnhance.Contrast(rgb).enhance(contrast_factor)
    return rgb


def apply_style(
    image: Image.Image,
    path: Path,
    reference_profile: StyleProfile,
    strength: float,
    room_reference: Image.Image | None = None,
) -> Image.Image:
    source = image.convert("RGBA")
    alpha = source.getchannel("A")
    visible_mask = alpha.point(lambda value: 255 if value > 8 else 0)
    rgb = source.convert("RGB")
    current = image_profile(source)
    seed = stable_seed(path)

    saturation_factor = clamp(1.0 + (reference_profile.saturation - current.saturation) * 0.85 * strength, 0.82, 1.28)
    brightness_factor = clamp(1.0 + (reference_profile.brightness - current.brightness) * 0.42 * strength, 0.95, 1.14)
    contrast_factor = clamp(0.98 + (reference_profile.contrast - current.contrast) / 280.0 * strength, 0.88, 1.08)

    rgb = ImageEnhance.Color(rgb).enhance(saturation_factor)
    rgb = ImageEnhance.Brightness(rgb).enhance(brightness_factor)
    rgb = ImageEnhance.Contrast(rgb).enhance(contrast_factor)

    gray_for_tone = ImageOps.grayscale(rgb)
    warm_glaze = Image.new("RGB", rgb.size, (202, 155, 58))
    olive_glaze = Image.new("RGB", rgb.size, (92, 116, 56))
    teal_shadow = Image.new("RGB", rgb.size, (25, 61, 62))
    mid_mask = gray_for_tone.point(lambda value: int(clamp(255 - abs(value - 142) * 2.1, 0, 255)))
    shadow_mask = gray_for_tone.point(lambda value: int(clamp(210 - value * 1.18, 0, 255)))
    rgb = Image.composite(Image.blend(rgb, warm_glaze, 0.090 * strength), rgb, ImageChops.multiply(mid_mask, visible_mask))
    rgb = Image.composite(Image.blend(rgb, olive_glaze, 0.040 * strength), rgb, ImageChops.multiply(mid_mask, visible_mask))
    rgb = Image.composite(Image.blend(rgb, teal_shadow, 0.045 * strength), rgb, ImageChops.multiply(shadow_mask, visible_mask))

    # Slight sprite-scale roughness, blended so forms and facial details survive.
    pixelated = rgb.resize((max(1, int(rgb.width * 0.66)), max(1, int(rgb.height * 0.66))), Image.Resampling.BILINEAR)
    pixelated = pixelated.resize(rgb.size, Image.Resampling.NEAREST)
    rgb = Image.blend(rgb, pixelated, 0.10 * strength)

    # Watercolor flattening: soften broad paint regions, then reintroduce line texture.
    median = rgb.filter(ImageFilter.MedianFilter(3))
    soft = rgb.filter(ImageFilter.GaussianBlur(0.45))
    rgb = Image.blend(rgb, median, 0.24 * strength)
    rgb = Image.blend(rgb, soft, 0.12 * strength)

    poster = ImageOps.posterize(rgb, 6)
    rgb = Image.blend(rgb, poster, 0.13 * strength)

    room_detail, room_lines = room_texture_maps(room_reference, rgb.size, seed)
    paper_noise = low_frequency_noise(rgb.size, seed, scale=8, amplitude=32)
    paper_mix = Image.blend(room_detail, paper_noise, 0.35)
    paper = ImageOps.colorize(paper_mix, black=(190, 151, 82), white=(248, 224, 151))
    rgb = Image.composite(Image.blend(rgb, paper, 0.078 * strength), rgb, visible_mask)

    fine_noise = low_frequency_noise(rgb.size, seed ^ 0xA531, scale=3, amplitude=26)
    hatch = hatch_mask(rgb.size, seed)
    fine_mix = ImageChops.lighter(fine_noise, hatch)
    fine = ImageOps.colorize(fine_mix, black=(45, 36, 24), white=(159, 130, 76))
    rgb = Image.composite(Image.blend(rgb, fine, 0.022 * strength), rgb, visible_mask)

    gray = ImageOps.grayscale(rgb)
    edges = gray.filter(ImageFilter.FIND_EDGES).filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.GaussianBlur(0.25))
    room_line_mask = room_lines.point(lambda value: int(clamp((value - 62) * 1.25, 0, 190)))
    edges = ImageChops.lighter(edges, room_line_mask)
    target_edge_boost = clamp((reference_profile.edge_mean + 10.0) / max(current.edge_mean, 1.0), 1.0, 2.2)
    edges = edges.point(lambda value: int(clamp(value * (2.6 + target_edge_boost * strength), 0, 255)))
    darkened = Image.blend(rgb, Image.new("RGB", rgb.size, (28, 29, 24)), 0.24 * strength)
    rgb = Image.composite(darkened, rgb, ImageChops.multiply(edges, visible_mask))

    rgb = retarget_rgb_profile(rgb, alpha, reference_profile, strength)

    # Bring back just enough crispness to keep the assets readable as sprites.
    rgb = rgb.filter(ImageFilter.UnsharpMask(radius=0.75, percent=int(85 * strength), threshold=4))

    return Image.merge("RGBA", (*rgb.split(), alpha))


def average_profile(paths: list[Path]) -> StyleProfile:
    profiles = [image_profile(Image.open(path)) for path in paths]
    if not profiles:
        return StyleProfile(saturation=0.0, brightness=0.0, contrast=0.0, edge_mean=0.0)
    return StyleProfile(
        saturation=sum(item.saturation for item in profiles) / len(profiles),
        brightness=sum(item.brightness for item in profiles) / len(profiles),
        contrast=sum(item.contrast for item in profiles) / len(profiles),
        edge_mean=sum(item.edge_mean for item in profiles) / len(profiles),
    )


def write_index(
    out_root: Path,
    count: int,
    profile: StyleProfile,
    references: list[Path],
    source_root: Path,
    room_reference: Path | None,
    before_profile: StyleProfile,
    after_profile: StyleProfile,
) -> None:
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
        f"Reference edge mean: {profile.edge_mean:.2f}",
        "",
        f"Room reference: `{room_reference.as_posix() if room_reference else 'none'}`",
        "",
        "Comparison metrics:",
        f"- Source average saturation/brightness/contrast/edge: "
        f"{before_profile.saturation:.3f} / {before_profile.brightness:.3f} / "
        f"{before_profile.contrast:.2f} / {before_profile.edge_mean:.2f}",
        f"- Styled average saturation/brightness/contrast/edge: "
        f"{after_profile.saturation:.3f} / {after_profile.brightness:.3f} / "
        f"{after_profile.contrast:.2f} / {after_profile.edge_mean:.2f}",
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
            "- Match the drawing room's stronger ochre/olive watercolor glaze.",
            "- Add room-derived paper, crackle, and sketch-line texture.",
            "- Add stronger ink density while preserving silhouettes and alpha.",
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
    room_reference_path = args.room_reference if args.room_reference and args.room_reference.exists() else None
    room_reference_image = Image.open(room_reference_path).convert("RGB") if room_reference_path else None
    profile = compute_profile([room_reference_path] if room_reference_path else references)
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
        styled = apply_style(
            Image.open(sprite),
            sprite,
            profile,
            clamp(args.strength, 0.0, 1.0),
            room_reference_image,
        )
        styled.save(out_path)
        meta_path = Path(str(out_path) + ".meta")
        if not meta_path.exists():
            write_meta(out_path)
        written += 1

    output_sprites = [out_root / sprite.relative_to(source_root) for sprite in sprites]
    before_profile = average_profile(sprites)
    after_profile = average_profile(output_sprites)
    write_index(out_root, len(sprites), profile, references, source_root, room_reference_path, before_profile, after_profile)
    print(f"Reference PNGs: {len(references)}")
    print(f"Style-matched sprite paths considered: {len(sprites)}")
    print(f"Style-matched sprites written: {written}")
    print(f"Output root: {out_root}")


if __name__ == "__main__":
    main()
