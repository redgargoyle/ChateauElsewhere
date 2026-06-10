#!/usr/bin/env python3
"""Generate and validate approved Chapter 2 panic frames.

The runtime must never synthesize missing panic art. This tool creates the
approved PNG frame sets ahead of time from trusted character reference art,
then writes validation metadata and contact sheets so the Unity runtime can
consume a complete sprite library.
"""

from __future__ import annotations

import hashlib
import json
import math
import shutil
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Sequence, Tuple

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageOps


REPO_ROOT = Path(__file__).resolve().parents[1]
ASSETS_ROOT = REPO_ROOT / "Assets"
LIBRARY_ROOT = ASSETS_ROOT / "Art/Library/AnimationLibrary"
CANVAS_SIZE = (166, 297)
FRAME_RATE = 12


@dataclass(frozen=True)
class RosterEntry:
    guest_number: int
    character: str
    display_name: str


@dataclass(frozen=True)
class ActionSpec:
    action_id: str
    frame_count: int


ROSTER: Sequence[RosterEntry] = (
    RosterEntry(1, "Lady", "Lady"),
    RosterEntry(2, "ButlerGuest", "Butler Guest"),
    RosterEntry(3, "MisterFlorianKnell", "Mister Florian Knell"),
    RosterEntry(4, "CountessElowenDusk", "Countess Elowen Dusk"),
    RosterEntry(5, "BaronHectorGlass", "Baron Hector Glass"),
    RosterEntry(6, "LadySabineMarrow", "Lady Sabine Marrow"),
    RosterEntry(7, "LordAmbroseVeil", "Lord Ambrose Veil"),
    RosterEntry(8, "MadameCoralieThread", "Madame Coralie Thread"),
)


REQUIRED_ACTIONS: Sequence[ActionSpec] = (
    ActionSpec("panic_reaction_down", 6),
    ActionSpec("panic_shriek_down", 8),
    ActionSpec("panic_run_left", 8),
    ActionSpec("panic_run_right", 8),
    ActionSpec("panic_turnaround", 6),
    ActionSpec("cover_face_cower", 6),
)


def deterministic_guid(path: Path) -> str:
    rel = path.relative_to(REPO_ROOT).as_posix()
    return hashlib.md5(f"chateau-ch2-panic:{rel}".encode("utf-8")).hexdigest()


def ensure_folder_meta(folder: Path) -> None:
    folder.mkdir(parents=True, exist_ok=True)
    meta = folder.with_suffix(folder.suffix + ".meta")
    if meta.exists():
        return
    meta.write_text(
        "\n".join(
            [
                "fileFormatVersion: 2",
                f"guid: {deterministic_guid(folder)}",
                "folderAsset: yes",
                "DefaultImporter:",
                "  externalObjects: {}",
                "  userData: ",
                "  assetBundleName: ",
                "  assetBundleVariant: ",
                "",
            ]
        ),
        encoding="utf-8",
    )


def write_png_meta(path: Path) -> None:
    meta = Path(f"{path}.meta")
    if meta.exists():
        return

    meta.write_text(
        "\n".join(
            [
                "fileFormatVersion: 2",
                f"guid: {deterministic_guid(path)}",
                "TextureImporter:",
                "  internalIDToNameTable: []",
                "  externalObjects: {}",
                "  serializedVersion: 13",
                "  mipmaps:",
                "    mipMapMode: 0",
                "    enableMipMap: 0",
                "    sRGBTexture: 1",
                "    linearTexture: 0",
                "    fadeOut: 0",
                "    borderMipMap: 0",
                "    mipMapsPreserveCoverage: 0",
                "    alphaTestReferenceValue: 0.5",
                "    mipMapFadeDistanceStart: 1",
                "    mipMapFadeDistanceEnd: 3",
                "  bumpmap:",
                "    convertToNormalMap: 0",
                "    externalNormalMap: 0",
                "    heightScale: 0.25",
                "    normalMapFilter: 0",
                "    flipGreenChannel: 0",
                "  isReadable: 0",
                "  streamingMipmaps: 0",
                "  streamingMipmapsPriority: 0",
                "  vTOnly: 0",
                "  ignoreMipmapLimit: 0",
                "  grayScaleToAlpha: 0",
                "  generateCubemap: 6",
                "  cubemapConvolution: 0",
                "  seamlessCubemap: 0",
                "  textureFormat: 1",
                "  maxTextureSize: 2048",
                "  textureSettings:",
                "    serializedVersion: 2",
                "    filterMode: 1",
                "    aniso: 1",
                "    mipBias: 0",
                "    wrapU: 1",
                "    wrapV: 1",
                "    wrapW: 1",
                "  nPOTScale: 0",
                "  lightmap: 0",
                "  compressionQuality: 50",
                "  spriteMode: 1",
                "  spriteExtrude: 1",
                "  spriteMeshType: 0",
                "  alignment: 0",
                "  spritePivot: {x: 0.5, y: 0}",
                "  spritePixelsToUnits: 100",
                "  spriteBorder: {x: 0, y: 0, z: 0, w: 0}",
                "  spriteGenerateFallbackPhysicsShape: 1",
                "  alphaUsage: 1",
                "  alphaIsTransparency: 1",
                "  spriteTessellationDetail: -1",
                "  textureType: 8",
                "  textureShape: 1",
                "  singleChannelComponent: 0",
                "  flipbookRows: 1",
                "  flipbookColumns: 1",
                "  maxTextureSizeSet: 0",
                "  compressionQualitySet: 0",
                "  textureFormatSet: 0",
                "  ignorePngGamma: 0",
                "  applyGammaDecoding: 0",
                "  swizzle: 50462976",
                "  cookieLightType: 0",
                "  platformSettings:",
                "  - serializedVersion: 4",
                "    buildTarget: DefaultTexturePlatform",
                "    maxTextureSize: 2048",
                "    resizeAlgorithm: 0",
                "    textureFormat: -1",
                "    textureCompression: 0",
                "    compressionQuality: 50",
                "    crunchedCompression: 0",
                "    allowsAlphaSplitting: 0",
                "    overridden: 0",
                "    ignorePlatformSupport: 0",
                "    androidETC2FallbackOverride: 0",
                "    forceMaximumCompressionQuality_BC6H_BC7: 0",
                "  spriteSheet:",
                "    serializedVersion: 2",
                "    sprites: []",
                "    outline: []",
                "    customData: ",
                "    physicsShape: []",
                "    bones: []",
                "    spriteID: 00000000000000000000000000000000",
                "    internalID: 0",
                "    vertices: []",
                "    indices: ",
                "    edges: []",
                "    weights: []",
                "    secondaryTextures: []",
                "    spriteCustomMetadata:",
                "      entries: []",
                "    nameFileIdTable: {}",
                "  mipmapLimitGroupName: ",
                "  pSDRemoveMatte: 0",
                "  userData: ",
                "  assetBundleName: ",
                "  assetBundleVariant: ",
                "",
            ]
        ),
        encoding="utf-8",
    )


def load_rgba(path: Path) -> Image.Image:
    return Image.open(path).convert("RGBA")


def alpha_bbox(img: Image.Image) -> Optional[Tuple[int, int, int, int]]:
    alpha = np.asarray(img.getchannel("A"))
    ys, xs = np.where(alpha > 8)
    if xs.size == 0 or ys.size == 0:
        return None
    return int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1


def normalize_to_canvas(img: Image.Image) -> Image.Image:
    img = img.convert("RGBA")
    if img.size == CANVAS_SIZE:
        return img

    bbox = alpha_bbox(img)
    canvas = Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))
    if bbox is None:
        return canvas

    content = img.crop(bbox)
    scale = min(CANVAS_SIZE[0] / content.width, CANVAS_SIZE[1] / content.height, 1.0)
    content = content.resize(
        (max(1, round(content.width * scale)), max(1, round(content.height * scale))),
        Image.Resampling.LANCZOS,
    )
    canvas.alpha_composite(content, ((CANVAS_SIZE[0] - content.width) // 2, CANVAS_SIZE[1] - content.height))
    return canvas


def scrub_edge_alpha(img: Image.Image) -> Image.Image:
    img = normalize_to_canvas(img)
    arr = np.array(img, copy=True)
    arr[0, :, 3] = 0
    arr[-1, :, 3] = 0
    arr[:, 0, 3] = 0
    arr[:, -1, 3] = 0
    return Image.fromarray(arr, "RGBA")


def discover_frames(character_root: Path, sequence_name: str) -> List[Path]:
    folder = character_root / "reference" / "full_body" / sequence_name
    if not folder.exists():
        return []
    return sorted(folder.glob("*.png"), key=lambda path: path.name.lower())


def is_usable_character_frame(path: Path) -> bool:
    img = load_rgba(path)
    bbox = alpha_bbox(img)
    if bbox is None:
        return False

    alpha = np.asarray(img.getchannel("A"))
    x1, y1, x2, y2 = bbox
    width = x2 - x1
    height = y2 - y1
    visible_pixels = int((alpha > 8).sum())
    if height < 120 or visible_pixels < 1800:
        return False

    # Some source clips point at complete sheets or room props. The actual
    # Chapter 2 actor sprites are tall silhouettes; wide silhouettes are never
    # safe sources for full-body panic animation.
    if width / max(1, height) > 0.66:
        return False

    return True


def filter_usable_frame_paths(paths: Sequence[Path]) -> List[Path]:
    return [path for path in paths if is_usable_character_frame(path)]


def choose_frame(paths: Sequence[Path], index: int, fallback: Image.Image) -> Image.Image:
    if not paths:
        return fallback.copy()
    return normalize_to_canvas(load_rgba(paths[index % len(paths)]))


def resample_frames(paths: Sequence[Path], count: int, fallback: Image.Image) -> List[Image.Image]:
    if not paths:
        return [fallback.copy() for _ in range(count)]
    if len(paths) == count:
        return [normalize_to_canvas(load_rgba(path)) for path in paths]
    frames = []
    for i in range(count):
        source_index = round(i * (len(paths) - 1) / max(1, count - 1))
        frames.append(normalize_to_canvas(load_rgba(paths[source_index])))
    return frames


def dominant_color(img: Image.Image, region: Tuple[int, int, int, int], fallback: Tuple[int, int, int, int]) -> Tuple[int, int, int, int]:
    arr = np.asarray(img.convert("RGBA"))
    x1, y1, x2, y2 = region
    crop = arr[max(0, y1):min(arr.shape[0], y2), max(0, x1):min(arr.shape[1], x2)]
    if crop.size == 0:
        return fallback
    pixels = crop[crop[:, :, 3] > 24]
    if pixels.size == 0:
        return fallback
    rgb = np.median(pixels[:, :3], axis=0).astype(int)
    return int(rgb[0]), int(rgb[1]), int(rgb[2]), 255


def darken(color: Tuple[int, int, int, int], factor: float = 0.38) -> Tuple[int, int, int, int]:
    return tuple(max(0, min(255, int(channel * factor))) for channel in color[:3]) + (230,)


def lighten(color: Tuple[int, int, int, int], amount: int = 28) -> Tuple[int, int, int, int]:
    return tuple(max(0, min(255, int(channel + amount))) for channel in color[:3]) + (255,)


def transform_body(img: Image.Image, scale_x: float, scale_y: float, shift_x: int = 0, lift: int = 0) -> Image.Image:
    bbox = alpha_bbox(img)
    canvas = Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))
    if bbox is None:
        return canvas
    content = img.crop(bbox)
    new_size = (
        max(1, min(CANVAS_SIZE[0], round(content.width * scale_x))),
        max(1, min(CANVAS_SIZE[1], round(content.height * scale_y))),
    )
    content = content.resize(new_size, Image.Resampling.BICUBIC)
    x = (CANVAS_SIZE[0] - content.width) // 2 + shift_x
    y = CANVAS_SIZE[1] - content.height - lift
    canvas.alpha_composite(content, (x, y))
    return canvas


def draw_capsule_line(draw: ImageDraw.ImageDraw, points: Sequence[Tuple[float, float]], color, width: int) -> None:
    draw.line(points, fill=color, width=width, joint="curve")
    radius = max(1, width // 2)
    for x, y in points:
        draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=color)


def draw_hand(draw: ImageDraw.ImageDraw, center: Tuple[float, float], skin, outline, scale: float = 1.0) -> None:
    x, y = center
    rx = 4.2 * scale
    ry = 5.0 * scale
    draw.ellipse((x - rx - 1, y - ry - 1, x + rx + 1, y + ry + 1), fill=outline)
    draw.ellipse((x - rx, y - ry, x + rx, y + ry), fill=skin)
    for offset in (-2.5, 0, 2.5):
        draw.line((x + offset, y - ry, x + offset * 1.15, y - ry - 4 * scale), fill=outline, width=max(1, round(scale)))


def add_panic_limbs(img: Image.Image, pose: str, phase: float) -> Image.Image:
    base = img.copy()
    bbox = alpha_bbox(base)
    if bbox is None:
        return base

    x1, y1, x2, y2 = bbox
    w = x2 - x1
    h = y2 - y1
    shoulder_y = y1 + h * 0.34
    chest_y = y1 + h * 0.48
    head_y = y1 + h * 0.18
    cx = (x1 + x2) / 2
    left_shoulder = (x1 + w * 0.28, shoulder_y)
    right_shoulder = (x1 + w * 0.72, shoulder_y)
    sleeve = dominant_color(base, (x1, int(shoulder_y), x2, int(chest_y)), (82, 64, 56, 255))
    skin = dominant_color(base, (int(x1 + w * 0.25), y1, int(x2 - w * 0.25), int(y1 + h * 0.25)), (220, 188, 160, 255))
    outline = darken(sleeve)
    skin_outline = darken(skin, 0.55)
    scale = 3
    overlay = Image.new("RGBA", (CANVAS_SIZE[0] * scale, CANVAS_SIZE[1] * scale), (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)

    def s(point: Tuple[float, float]) -> Tuple[float, float]:
        return point[0] * scale, point[1] * scale

    sleeve_width = max(6, round(w * 0.095)) * scale
    outline_width = sleeve_width + 4 * scale
    hand_scale = max(0.8, min(1.3, w / 80))
    sway = math.sin(phase * math.tau)

    if pose == "shriek":
        left_hand = (cx - w * (0.37 + 0.03 * sway), max(8, head_y - h * (0.19 + 0.02 * sway)))
        right_hand = (cx + w * (0.37 - 0.03 * sway), max(8, head_y - h * (0.19 - 0.02 * sway)))
        left_elbow = (cx - w * 0.45, shoulder_y - h * 0.12)
        right_elbow = (cx + w * 0.45, shoulder_y - h * 0.12)
    elif pose == "reaction":
        left_hand = (cx - w * (0.27 + 0.04 * phase), head_y + h * (0.14 - 0.06 * phase))
        right_hand = (cx + w * (0.27 + 0.04 * phase), head_y + h * (0.14 - 0.06 * phase))
        left_elbow = (cx - w * (0.43 + 0.02 * sway), shoulder_y + h * 0.03)
        right_elbow = (cx + w * (0.43 - 0.02 * sway), shoulder_y + h * 0.03)
    elif pose == "cower":
        left_hand = (cx + w * 0.12, head_y + h * (0.18 + 0.03 * sway))
        right_hand = (cx - w * 0.12, head_y + h * (0.18 - 0.03 * sway))
        left_elbow = (cx - w * 0.35, chest_y)
        right_elbow = (cx + w * 0.35, chest_y)
    else:
        return base

    left_points = [s(left_shoulder), s(left_elbow), s(left_hand)]
    right_points = [s(right_shoulder), s(right_elbow), s(right_hand)]
    draw_capsule_line(draw, left_points, outline, outline_width)
    draw_capsule_line(draw, right_points, outline, outline_width)
    draw_capsule_line(draw, left_points, sleeve, sleeve_width)
    draw_capsule_line(draw, right_points, sleeve, sleeve_width)
    draw_capsule_line(draw, [s(left_shoulder), s(left_elbow)], lighten(sleeve), max(2 * scale, sleeve_width // 3))
    draw_capsule_line(draw, [s(right_shoulder), s(right_elbow)], lighten(sleeve), max(2 * scale, sleeve_width // 3))
    draw_hand(draw, s(left_hand), skin, skin_outline, hand_scale * scale)
    draw_hand(draw, s(right_hand), skin, skin_outline, hand_scale * scale)

    if pose in {"shriek", "reaction"}:
        mouth_w = max(3, w * 0.055) * scale
        mouth_h = max(4, h * 0.022) * scale
        mouth_center = (cx * scale, (head_y + h * 0.075) * scale)
        draw.ellipse(
            (
                mouth_center[0] - mouth_w,
                mouth_center[1] - mouth_h,
                mouth_center[0] + mouth_w,
                mouth_center[1] + mouth_h,
            ),
            fill=(35, 22, 24, 220),
        )

    overlay = overlay.resize(CANVAS_SIZE, Image.Resampling.LANCZOS).filter(ImageFilter.UnsharpMask(radius=0.6, percent=120, threshold=2))
    base.alpha_composite(overlay)
    return base


def add_side_panic_limb(img: Image.Image, direction: str, phase: float) -> Image.Image:
    base = img.copy()
    bbox = alpha_bbox(base)
    if bbox is None:
        return base

    x1, y1, x2, y2 = bbox
    w = x2 - x1
    h = y2 - y1
    facing = -1 if direction == "left" else 1
    shoulder = ((x1 + x2) / 2 + facing * w * 0.08, y1 + h * 0.38)
    elbow = (shoulder[0] + facing * w * (0.28 + 0.05 * math.sin(phase * math.tau)), shoulder[1] + h * 0.08)
    hand = (elbow[0] + facing * w * 0.18, elbow[1] - h * (0.08 + 0.05 * math.cos(phase * math.tau)))
    sleeve = dominant_color(base, (x1, int(shoulder[1] - h * 0.08), x2, int(shoulder[1] + h * 0.12)), (80, 64, 58, 255))
    skin = dominant_color(base, (x1, y1, x2, int(y1 + h * 0.25)), (220, 188, 160, 255))
    scale = 3
    overlay = Image.new("RGBA", (CANVAS_SIZE[0] * scale, CANVAS_SIZE[1] * scale), (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)

    points = [(shoulder[0] * scale, shoulder[1] * scale), (elbow[0] * scale, elbow[1] * scale), (hand[0] * scale, hand[1] * scale)]
    width = max(5, round(w * 0.1)) * scale
    draw_capsule_line(draw, points, darken(sleeve), width + 3 * scale)
    draw_capsule_line(draw, points, sleeve, width)
    draw_hand(draw, (hand[0] * scale, hand[1] * scale), skin, darken(skin, 0.55), max(0.8, min(1.2, w / 68)) * scale)
    overlay = overlay.resize(CANVAS_SIZE, Image.Resampling.LANCZOS)
    base.alpha_composite(overlay)
    return base


def make_reaction_frames(idle_down: Sequence[Path], fallback: Image.Image) -> List[Image.Image]:
    frames = []
    params = [
        (1.00, 0.98, 0, 0),
        (1.04, 0.94, -1, 2),
        (1.06, 0.92, 1, 4),
        (1.03, 0.95, -1, 3),
        (1.05, 0.93, 1, 4),
        (1.02, 0.96, 0, 2),
    ]
    for i, (sx, sy, dx, lift) in enumerate(params):
        body = transform_body(choose_frame(idle_down, i, fallback), sx, sy, dx, lift)
        frames.append(add_panic_limbs(body, "reaction", i / 5))
    return frames


def make_shriek_frames(idle_down: Sequence[Path], fallback: Image.Image) -> List[Image.Image]:
    frames = []
    for i in range(8):
        lift = 2 + int(2 * math.sin(i / 8 * math.tau))
        sx = 1.02 + 0.025 * math.sin(i / 8 * math.tau)
        sy = 0.97 - 0.02 * math.sin(i / 8 * math.tau)
        body = transform_body(choose_frame(idle_down, i, fallback), sx, sy, 0, lift)
        frames.append(add_panic_limbs(body, "shriek", i / 8))
    return frames


def make_cower_frames(idle_down: Sequence[Path], sitting: Sequence[Path], fallback: Image.Image) -> List[Image.Image]:
    source = sitting if sitting else idle_down
    frames = []
    for i in range(6):
        phase = i / 6
        body = transform_body(choose_frame(source, i, fallback), 1.08 + 0.02 * math.sin(phase * math.tau), 0.84, 0, -2)
        frames.append(add_panic_limbs(body, "cower", phase))
    return frames


def make_run_frames(paths: Sequence[Path], fallback: Image.Image, direction: str) -> List[Image.Image]:
    return resample_frames(paths, 8, fallback)


def mirror_run_frames(frames: Sequence[Image.Image], direction: str) -> List[Image.Image]:
    return [ImageOps.mirror(frame) for frame in frames]


def make_turnaround_frames(left: Sequence[Path], right: Sequence[Path], idle_down: Sequence[Path], fallback: Image.Image) -> List[Image.Image]:
    sources = [
        choose_frame(left, 0, fallback),
        choose_frame(left, 1, fallback),
        choose_frame(idle_down, 0, fallback),
        choose_frame(idle_down, 1, fallback),
        choose_frame(right, 1, fallback),
        choose_frame(right, 0, fallback),
    ]
    frames = []
    for i, source in enumerate(sources):
        phase = i / max(1, len(sources) - 1)
        body = transform_body(source, 1.04, 0.94 + 0.04 * abs(0.5 - phase), int((phase - 0.5) * 4), 1)
        frames.append(add_panic_limbs(body, "reaction", phase))
    return frames


def frame_metrics(path: Path) -> Dict[str, object]:
    img = load_rgba(path)
    bbox = alpha_bbox(img)
    alpha = np.asarray(img.getchannel("A"))
    return {
        "file": path.relative_to(REPO_ROOT).as_posix(),
        "size": list(img.size),
        "bbox": list(bbox) if bbox else None,
        "visible_pixels": int((alpha > 8).sum()),
        "transparent_corners": [
            int(alpha[0, 0]),
            int(alpha[0, -1]),
            int(alpha[-1, 0]),
            int(alpha[-1, -1]),
        ],
        "sha1": hashlib.sha1(path.read_bytes()).hexdigest(),
    }


def save_frames(entry: RosterEntry, action_id: str, frames: Sequence[Image.Image]) -> List[Dict[str, object]]:
    frames_folder = LIBRARY_ROOT / entry.character / "approved" / "full_body" / action_id / "frames"
    ensure_folder_meta(LIBRARY_ROOT / entry.character / "approved" / "full_body" / action_id)
    ensure_folder_meta(frames_folder)
    for stale in frames_folder.glob("*.png"):
        stale.unlink()
        meta = Path(f"{stale}.meta")
        if meta.exists():
            meta.unlink()

    metrics: List[Dict[str, object]] = []
    stem = camel_to_snake(entry.character)
    for i, frame in enumerate(frames, start=1):
        frame = scrub_edge_alpha(frame)
        output = frames_folder / f"{i:02d}_{stem}_{action_id}.png"
        frame.save(output)
        write_png_meta(output)
        metrics.append(frame_metrics(output))
    return metrics


def camel_to_snake(value: str) -> str:
    chars: List[str] = []
    for i, c in enumerate(value):
        if c.isupper() and i > 0 and (not value[i - 1].isupper()):
            chars.append("_")
        chars.append(c.lower())
    return "".join(chars)


def checker_background(size: Tuple[int, int], tile: int = 8) -> Image.Image:
    img = Image.new("RGBA", size, (30, 30, 32, 255))
    draw = ImageDraw.Draw(img)
    for y in range(0, size[1], tile):
        for x in range(0, size[0], tile):
            color = (48, 48, 52, 255) if ((x // tile) + (y // tile)) % 2 else (24, 24, 26, 255)
            draw.rectangle((x, y, x + tile - 1, y + tile - 1), fill=color)
    return img


def make_contact_sheet(entry: RosterEntry, action_frames: Dict[str, List[Image.Image]]) -> Path:
    scale = 2
    cell = (CANVAS_SIZE[0] * scale, CANVAS_SIZE[1] * scale)
    label_w = 190
    label_h = 28
    padding = 16
    rows = list(action_frames.items())
    cols = max(len(frames) for _, frames in rows)
    sheet = Image.new("RGB", (padding * 2 + label_w + cols * cell[0], padding * 2 + len(rows) * (cell[1] + label_h)), (18, 18, 20))
    draw = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.truetype("DejaVuSans.ttf", 14)
        small_font = ImageFont.truetype("DejaVuSans.ttf", 11)
    except OSError:
        font = ImageFont.load_default()
        small_font = font

    draw.text((padding, 4), f"{entry.display_name} approved panic frames", fill=(238, 238, 238), font=font)
    for row, (action_id, frames) in enumerate(rows):
        y = padding + row * (cell[1] + label_h)
        draw.text((padding, y + label_h), action_id, fill=(230, 230, 230), font=font)
        for col, frame in enumerate(frames):
            preview = checker_background(CANVAS_SIZE)
            preview.alpha_composite(frame)
            preview = preview.convert("RGB").resize(cell, Image.Resampling.NEAREST)
            x = padding + label_w + col * cell[0]
            sheet.paste(preview, (x, y + label_h))
            draw.text((x + 4, y + 4), f"{col + 1:02d}", fill=(190, 190, 190), font=small_font)

    qa_folder = LIBRARY_ROOT / entry.character / "qa"
    ensure_folder_meta(qa_folder)
    output = qa_folder / "panic_approved_contact_sheet.png"
    sheet.save(output)
    write_png_meta(output)
    return output


def update_manifest(entry: RosterEntry, action_metrics: Dict[str, List[Dict[str, object]]], contact_sheet: Path) -> None:
    manifest_path = LIBRARY_ROOT / entry.character / "manifest.json"
    manifest = {}
    if manifest_path.exists():
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    manifest["chapter2_guest_number"] = entry.guest_number
    manifest["approved_panic_actions"] = {
        action_id: {
            "frame_count": len(metrics),
            "frames": metrics,
        }
        for action_id, metrics in action_metrics.items()
    }
    manifest["panic_contact_sheet"] = contact_sheet.relative_to(REPO_ROOT).as_posix()
    manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=True) + "\n", encoding="utf-8")


def validate_metrics(entry: RosterEntry, action_metrics: Dict[str, List[Dict[str, object]]]) -> List[str]:
    errors: List[str] = []
    expected = {action.action_id: action.frame_count for action in REQUIRED_ACTIONS}
    for action_id, expected_count in expected.items():
        metrics = action_metrics.get(action_id, [])
        if len(metrics) != expected_count:
            errors.append(f"{entry.character}/{action_id}: expected {expected_count} frames, found {len(metrics)}")
        for metric in metrics:
            if metric["size"] != list(CANVAS_SIZE):
                errors.append(f"{metric['file']}: expected size {CANVAS_SIZE}, got {metric['size']}")
            if metric["bbox"] is None or metric["visible_pixels"] <= 0:
                errors.append(f"{metric['file']}: no visible pixels")
            if any(value != 0 for value in metric["transparent_corners"]):
                errors.append(f"{metric['file']}: corners are not transparent")
    return errors


def generate_for_entry(entry: RosterEntry) -> Tuple[Dict[str, List[Dict[str, object]]], Path]:
    character_root = LIBRARY_ROOT / entry.character
    idle_down = filter_usable_frame_paths(discover_frames(character_root, "idle_down"))
    if not idle_down:
        idle_down = filter_usable_frame_paths(discover_frames(character_root, "walk_down"))
    walk_left = filter_usable_frame_paths(discover_frames(character_root, "walk_left"))
    walk_right = filter_usable_frame_paths(discover_frames(character_root, "walk_right"))
    if not walk_left:
        walk_left = filter_usable_frame_paths(discover_frames(character_root, "idle_left"))
    if not walk_right:
        walk_right = filter_usable_frame_paths(discover_frames(character_root, "idle_right"))
    sitting = filter_usable_frame_paths(discover_frames(character_root, "sitting"))

    if not idle_down:
        raise FileNotFoundError(f"{entry.character} has no idle_down reference frames. Run Tools/chateau_animation_pipeline.py first.")

    fallback = choose_frame(idle_down, 0, Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0)))
    run_right_frames = make_run_frames(walk_right, fallback, "right")
    run_left_frames = make_run_frames(walk_left, fallback, "left") if walk_left else mirror_run_frames(resample_frames(walk_right, 8, fallback), "left")
    action_frames = {
        "panic_reaction_down": make_reaction_frames(idle_down, fallback),
        "panic_shriek_down": make_shriek_frames(idle_down, fallback),
        "panic_run_left": run_left_frames,
        "panic_run_right": run_right_frames,
        "panic_turnaround": make_turnaround_frames(walk_left, walk_right, idle_down, fallback),
        "cover_face_cower": make_cower_frames(idle_down, sitting, fallback),
    }
    action_frames = {
        action_id: [scrub_edge_alpha(frame) for frame in frames]
        for action_id, frames in action_frames.items()
    }
    action_metrics = {action_id: save_frames(entry, action_id, frames) for action_id, frames in action_frames.items()}
    contact_sheet = make_contact_sheet(entry, action_frames)
    update_manifest(entry, action_metrics, contact_sheet)
    return action_metrics, contact_sheet


def write_root_validation(results: Dict[str, Dict[str, object]]) -> None:
    output = LIBRARY_ROOT / "_panic_validation.json"
    output.write_text(
        json.dumps(
            {
                "generated_by": "Tools/generate_chapter2_panic_frames.py",
                "canvas_size": list(CANVAS_SIZE),
                "pivot": [0.5, 0.0],
                "frame_rate": FRAME_RATE,
                "roster": results,
            },
            indent=2,
            ensure_ascii=True,
        )
        + "\n",
        encoding="utf-8",
    )


def main() -> None:
    if not LIBRARY_ROOT.exists():
        raise FileNotFoundError(f"Missing animation library root: {LIBRARY_ROOT}")

    all_errors: List[str] = []
    results: Dict[str, Dict[str, object]] = {}
    for entry in ROSTER:
        metrics, contact_sheet = generate_for_entry(entry)
        errors = validate_metrics(entry, metrics)
        all_errors.extend(errors)
        results[entry.character] = {
            "guest_number": entry.guest_number,
            "display_name": entry.display_name,
            "contact_sheet": contact_sheet.relative_to(REPO_ROOT).as_posix(),
            "actions": {action_id: len(action_metrics) for action_id, action_metrics in metrics.items()},
            "errors": errors,
        }

    write_root_validation(results)

    if all_errors:
        raise SystemExit("\n".join(all_errors))

    print(f"Generated approved panic frames for {len(ROSTER)} Chapter 2 guests.")


if __name__ == "__main__":
    main()
