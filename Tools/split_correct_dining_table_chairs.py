#!/usr/bin/env python3
"""Split correct_dining_table_chairs.png into nine chair sprites.

The split uses marker-controlled region growing over the source alpha mask.
This is effectively a small watershed segmentation: each chair starts from an
interior seed, then claims connected source pixels by shortest alpha-path.
"""

from __future__ import annotations

import json
import re
from heapq import heappop, heappush
from pathlib import Path
from uuid import uuid4

import numpy as np
from PIL import Image, ImageDraw


SOURCE = Path("Assets/Art/Objects/correct_dining_table_chairs.png")
OUT_DIR = Path("Assets/Art/Objects/DiningTableChairSplits")
SCENE_DIR = OUT_DIR / "SceneOverlays"
PREVIEW = OUT_DIR / "_dining_chair_splits_preview.png"
LABEL_PREVIEW = OUT_DIR / "_dining_chair_splits_labels.png"
SCENE_ALIGNMENT_PREVIEW = OUT_DIR / "_dining_chair_scene_alignment_preview.png"
MANIFEST = OUT_DIR / "_dining_chair_splits_manifest.json"


SEEDS: tuple[tuple[str, tuple[int, int]], ...] = (
    ("dining_chair_left_01_front", (38, 165)),
    ("dining_chair_left_02_mid_front", (102, 145)),
    ("dining_chair_left_03_mid_back", (150, 122)),
    ("dining_chair_left_04_back", (190, 105)),
    ("dining_chair_head", (355, 340)),
    ("dining_chair_right_01_back", (590, 112)),
    ("dining_chair_right_02_mid_back", (628, 132)),
    ("dining_chair_right_03_mid_front", (676, 170)),
    ("dining_chair_right_04_front", (735, 250)),
)


OUTPUT_ORIGINS: dict[str, tuple[int, int]] = {}


def meta_guid(meta_path: Path) -> str:
    if meta_path.exists():
        match = re.search(r"^guid:\s*([0-9a-f]{32})$", meta_path.read_text(encoding="utf-8"), re.MULTILINE)
        if match:
            return match.group(1)
    return uuid4().hex


def write_folder_meta(folder: Path) -> None:
    meta_path = folder.with_suffix(folder.suffix + ".meta")
    if meta_path.exists():
        return
    meta_path.write_text(
        f"""fileFormatVersion: 2
guid: {uuid4().hex}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
""",
        encoding="utf-8",
    )


def write_unity_sprite_meta(image_path: Path, *, pivot: str = "bottom_center") -> None:
    if pivot == "bottom_center":
        alignment = 7
        sprite_pivot = "{x: 0.5, y: 0}"
    elif pivot == "center":
        alignment = 0
        sprite_pivot = "{x: 0.5, y: 0.5}"
    else:
        raise ValueError(f"Unsupported sprite pivot {pivot!r}")

    meta_path = image_path.with_suffix(image_path.suffix + ".meta")
    guid = meta_guid(meta_path)
    meta_path.write_text(
        f"""fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: {alignment}
  spritePivot: {sprite_pivot}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 4
    buildTarget: Standalone
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID: 5e97eb03825dee720800000000000000
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
""",
        encoding="utf-8",
    )


def clean_outputs() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    write_folder_meta(OUT_DIR)
    SCENE_DIR.mkdir(parents=True, exist_ok=True)
    write_folder_meta(SCENE_DIR)
    for path in OUT_DIR.glob("dining_chair_*.png"):
        path.unlink()
    for path in SCENE_DIR.glob("dining_chair_*_scene.png"):
        path.unlink()
    for path in (SCENE_ALIGNMENT_PREVIEW,):
        if path.exists():
            path.unlink()
        meta = path.with_suffix(path.suffix + ".meta")
        if meta.exists():
            meta.unlink()


def seed_region(labels: np.ndarray, dist: np.ndarray, alpha: np.ndarray) -> list[tuple[float, int, int, int]]:
    heap: list[tuple[float, int, int, int]] = []
    for index, (_, (seed_x, seed_y)) in enumerate(SEEDS):
        seeded = 0
        for y in range(seed_y - 5, seed_y + 6):
            for x in range(seed_x - 5, seed_x + 6):
                if not (0 <= y < alpha.shape[0] and 0 <= x < alpha.shape[1]):
                    continue
                if not alpha[y, x] or (x - seed_x) ** 2 + (y - seed_y) ** 2 > 25:
                    continue
                labels[y, x] = index
                dist[y, x] = 0.0
                heappush(heap, (0.0, index, x, y))
                seeded += 1
        if seeded == 0:
            raise RuntimeError(f"Seed for {SEEDS[index][0]} did not land on visible source pixels")
    return heap


def segment_labels(alpha: np.ndarray) -> np.ndarray:
    labels = np.full(alpha.shape, -1, dtype=np.int16)
    dist = np.full(alpha.shape, np.inf, dtype=np.float32)
    heap = seed_region(labels, dist, alpha)
    neighbors = (
        (-1, 0, 1.0),
        (1, 0, 1.0),
        (0, -1, 1.0),
        (0, 1, 1.0),
        (-1, -1, 1.4142135),
        (1, -1, 1.4142135),
        (-1, 1, 1.4142135),
        (1, 1, 1.4142135),
    )

    while heap:
        current_dist, index, x, y = heappop(heap)
        if current_dist != dist[y, x] or index != labels[y, x]:
            continue
        for dx, dy, cost in neighbors:
            nx = x + dx
            ny = y + dy
            if not (0 <= ny < alpha.shape[0] and 0 <= nx < alpha.shape[1]):
                continue
            if not alpha[ny, nx]:
                continue
            next_dist = current_dist + cost
            if next_dist < dist[ny, nx]:
                dist[ny, nx] = next_dist
                labels[ny, nx] = index
                heappush(heap, (next_dist, index, nx, ny))

    unassigned = np.logical_and(alpha, labels < 0)
    if np.any(unassigned):
        raise RuntimeError(f"{int(unassigned.sum())} source pixels were not reached by chair seeds")
    return labels


def crop_label(source: Image.Image, labels: np.ndarray, index: int, padding: int = 8) -> tuple[Image.Image, tuple[int, int]]:
    rgba = np.array(source)
    label_mask = labels == index
    rgba[..., 3] = np.where(label_mask, rgba[..., 3], 0).astype(np.uint8)
    image = Image.fromarray(rgba, "RGBA")
    bbox = image.getchannel("A").getbbox()
    if bbox is None:
        raise RuntimeError(f"Empty output for {SEEDS[index][0]}")

    left = max(0, bbox[0] - padding)
    top = max(0, bbox[1] - padding)
    right = min(source.width, bbox[2] + padding)
    bottom = min(source.height, bbox[3] + padding)
    return image.crop((left, top, right, bottom)), (left, top)


def canvas_label(source: Image.Image, labels: np.ndarray, index: int) -> Image.Image:
    rgba = np.array(source)
    label_mask = labels == index
    rgba[..., 3] = np.where(label_mask, rgba[..., 3], 0).astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")


def checker(size: tuple[int, int], square: int = 12) -> Image.Image:
    image = Image.new("RGBA", size, (218, 218, 218, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], square):
        for x in range(0, size[0], square):
            if (x // square + y // square) % 2:
                draw.rectangle((x, y, x + square - 1, y + square - 1), fill=(166, 166, 166, 255))
    return image


def write_preview(paths: list[Path]) -> None:
    columns = 3
    tile_size = (190, 220)
    rows = (len(paths) + columns - 1) // columns
    sheet = Image.new("RGBA", (columns * tile_size[0], rows * tile_size[1]), (0, 0, 0, 0))
    for index, path in enumerate(paths):
        image = Image.open(path).convert("RGBA")
        image.thumbnail((170, 170), Image.Resampling.LANCZOS)
        tile = checker(tile_size, 12)
        tile.alpha_composite(image, ((tile_size[0] - image.width) // 2, 8))
        draw = ImageDraw.Draw(tile)
        draw.rectangle((0, 190, tile_size[0] - 1, tile_size[1] - 1), fill=(20, 20, 20, 235))
        draw.text((7, 198), path.stem.replace("dining_chair_", ""), fill=(255, 255, 255, 255))
        sheet.alpha_composite(tile, ((index % columns) * tile_size[0], (index // columns) * tile_size[1]))
    sheet.save(PREVIEW)
    write_unity_sprite_meta(PREVIEW, pivot="center")


def write_label_preview(source: Image.Image, labels: np.ndarray) -> None:
    colors = (
        (255, 80, 80),
        (255, 180, 60),
        (255, 255, 70),
        (80, 255, 120),
        (80, 220, 255),
        (120, 120, 255),
        (220, 90, 255),
        (255, 90, 180),
        (255, 255, 255),
    )
    preview = Image.new("RGBA", source.size, (29, 29, 31, 255))
    preview.alpha_composite(source)
    overlay = Image.new("RGBA", source.size, (0, 0, 0, 0))
    overlay_array = np.array(overlay)
    for index, color in enumerate(colors):
        mask = labels == index
        overlay_array[..., 0] = np.where(mask, color[0], overlay_array[..., 0])
        overlay_array[..., 1] = np.where(mask, color[1], overlay_array[..., 1])
        overlay_array[..., 2] = np.where(mask, color[2], overlay_array[..., 2])
        overlay_array[..., 3] = np.where(mask, 70, overlay_array[..., 3])
    preview.alpha_composite(Image.fromarray(overlay_array, "RGBA"))

    draw = ImageDraw.Draw(preview)
    for index, (name, _) in enumerate(SEEDS):
        ys, xs = np.nonzero(labels == index)
        if len(xs) == 0:
            continue
        bbox = (int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1)
        color = colors[index] + (255,)
        draw.rectangle((bbox[0], bbox[1], bbox[2] - 1, bbox[3] - 1), outline=color, width=2)
        draw.text((bbox[0] + 2, bbox[1] + 2), str(index + 1), fill=color)
    preview.save(LABEL_PREVIEW)
    write_unity_sprite_meta(LABEL_PREVIEW, pivot="center")


def main() -> int:
    source = Image.open(SOURCE).convert("RGBA")
    alpha = np.array(source.getchannel("A")) > 0
    clean_outputs()
    labels = segment_labels(alpha)

    OUTPUT_ORIGINS.clear()
    outputs: list[Path] = []
    manifest: dict[str, object] = {
        "source": str(SOURCE),
        "method": "marker-controlled alpha watershed region growing from nine chair seeds",
        "scene_overlay_note": "Scene overlay PNGs keep the original 793x585 canvas so each separate chair can share the dining table transform.",
        "chairs": [],
    }

    for index, (name, seed) in enumerate(SEEDS):
        crop, origin = crop_label(source, labels, index)
        path = OUT_DIR / f"{name}.png"
        crop.save(path)
        write_unity_sprite_meta(path)
        scene_path = SCENE_DIR / f"{name}_scene.png"
        canvas_label(source, labels, index).save(scene_path)
        write_unity_sprite_meta(scene_path, pivot="center")
        OUTPUT_ORIGINS[path.name] = origin
        outputs.append(path)
        alpha_pixels = int(np.count_nonzero(labels == index))
        manifest["chairs"].append(
            {
                "name": name,
                "file": str(path),
                "scene_file": str(scene_path),
                "seed": seed,
                "origin": origin,
                "size": crop.size,
                "scene_size": source.size,
                "source_alpha_pixels": alpha_pixels,
            }
        )
        print(f"wrote {path} ({crop.width}x{crop.height})")
        print(f"wrote {scene_path} ({source.width}x{source.height})")

    write_preview(outputs)
    write_label_preview(source, labels)
    MANIFEST.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"wrote {PREVIEW}")
    print(f"wrote {LABEL_PREVIEW}")
    print(f"wrote {MANIFEST}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
