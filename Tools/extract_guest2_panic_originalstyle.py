#!/usr/bin/env python3
"""Extract and hard-style guest2 panic candidates from a chroma sheet.

This is intentionally harsher than the general sprite post-process pass:
guest2's originals are small, dark, pixelated watercolor sprites, and raw
image-generation output tends to look too smooth and theatrical at full size.
"""

from __future__ import annotations

import argparse
import json
import random
import shutil
import uuid
from collections import deque
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter, ImageOps


DEFAULT_OUT_ROOT = Path("Assets/GeneratedSpriteLibrary/ButlerGuest/Panic/OriginalStyleGuest2_20260610")
DEFAULT_STYLE_ROOT = Path(
    "Assets/GeneratedSpriteLibraryStyleMatched/ButlerGuest/Panic/OriginalStyleGuest2_20260610"
)
DEFAULT_TEMP_ROOT = Path("Temp/guest2_originalstyle_panic")

FRAME_SLUGS = (
    "tense_cane_grip",
    "recoil_half_lift",
    "controlled_palms_up",
    "cane_braced_back",
    "hand_to_mouth",
    "clutch_chest",
    "shield_face",
    "trembling_cane_grip",
)

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

PNG_META_TEMPLATE = """fileFormatVersion: 2
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
    filterMode: 0
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
  spriteMeshType: 0
  alignment: 0
  spritePivot: {{x: 0.5, y: 0}}
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
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
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
    spriteID: {sprite_id}
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
"""


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--out-root", default=DEFAULT_OUT_ROOT, type=Path)
    parser.add_argument("--style-root", default=DEFAULT_STYLE_ROOT, type=Path)
    parser.add_argument("--temp-root", default=DEFAULT_TEMP_ROOT, type=Path)
    parser.add_argument("--target-height", default=218, type=int)
    parser.add_argument("--force", action="store_true")
    return parser.parse_args()


def guid() -> str:
    return uuid.uuid4().hex


def write_folder_meta(folder: Path) -> None:
    meta = Path(str(folder) + ".meta")
    if not meta.exists():
        meta.write_text(FOLDER_META_TEMPLATE.format(guid=guid()), encoding="utf-8")


def write_text_meta(path: Path) -> None:
    meta = Path(str(path) + ".meta")
    if not meta.exists():
        meta.write_text(TEXT_META_TEMPLATE.format(guid=guid()), encoding="utf-8")


def write_png_meta(path: Path) -> None:
    meta = Path(str(path) + ".meta")
    if not meta.exists():
        meta.write_text(PNG_META_TEMPLATE.format(guid=guid(), sprite_id=guid()), encoding="utf-8")


def ensure_folder(folder: Path, boundary: Path) -> None:
    folder.mkdir(parents=True, exist_ok=True)
    boundary = boundary.resolve()
    current = folder.resolve()
    chain = [current]
    while current != boundary:
        parent = current.parent
        if parent == current or not str(current).startswith(str(boundary)):
            break
        current = parent
        chain.append(current)
    for item in reversed(chain):
        if item.exists():
            write_folder_meta(item)


def green_matte_to_alpha(cell: Image.Image) -> Image.Image:
    rgba = cell.convert("RGBA")
    width, height = rgba.size
    pixels = rgba.load()
    alpha = Image.new("L", rgba.size, 0)
    alpha_pixels = alpha.load()

    for y in range(height):
        for x in range(width):
            red, green, blue, source_alpha = pixels[x, y]
            green_excess = green - max(red, blue)
            is_key = green > 118 and green_excess > 40 and red < 170 and blue < 170
            if is_key:
                alpha_pixels[x, y] = max(0, min(source_alpha, int(255 - green_excess * 3.2)))
            else:
                alpha_pixels[x, y] = source_alpha

    alpha = alpha.filter(ImageFilter.MedianFilter(3))
    alpha = keep_visible_components(alpha)
    alpha = alpha.point(lambda value: 0 if value < 28 else value)

    bbox = alpha.point(lambda value: 255 if value > 16 else 0).getbbox()
    if bbox is None:
        return Image.new("RGBA", (1, 1), (0, 0, 0, 0))

    pad = 16
    left = max(0, bbox[0] - pad)
    top = max(0, bbox[1] - pad)
    right = min(width, bbox[2] + pad)
    bottom = min(height, bbox[3] + pad)
    rgba = rgba.crop((left, top, right, bottom))
    alpha = alpha.crop((left, top, right, bottom))

    rgba = despill_and_remove_halo(rgba, alpha)
    bbox = rgba.getchannel("A").point(lambda value: 255 if value > 8 else 0).getbbox()
    if bbox:
        left = max(0, bbox[0] - 8)
        top = max(0, bbox[1] - 8)
        right = min(rgba.width, bbox[2] + 8)
        bottom = min(rgba.height, bbox[3] + 8)
        rgba = rgba.crop((left, top, right, bottom))
    return rgba


def keep_visible_components(alpha: Image.Image) -> Image.Image:
    mask = alpha.point(lambda value: 255 if value > 28 else 0)
    width, height = mask.size
    pixels = mask.load()
    visited: set[tuple[int, int]] = set()
    components: list[list[tuple[int, int]]] = []

    for y in range(height):
        for x in range(width):
            if pixels[x, y] == 0 or (x, y) in visited:
                continue
            queue: deque[tuple[int, int]] = deque([(x, y)])
            visited.add((x, y))
            component: list[tuple[int, int]] = []
            while queue:
                cx, cy = queue.popleft()
                component.append((cx, cy))
                for nx in (cx - 1, cx, cx + 1):
                    for ny in (cy - 1, cy, cy + 1):
                        if nx < 0 or ny < 0 or nx >= width or ny >= height or (nx, ny) in visited:
                            continue
                        if pixels[nx, ny] == 0:
                            continue
                        visited.add((nx, ny))
                        queue.append((nx, ny))
            components.append(component)

    if not components:
        return alpha

    largest = max(len(component) for component in components)
    keep = Image.new("L", alpha.size, 0)
    keep_pixels = keep.load()
    alpha_pixels = alpha.load()
    for component in components:
        # Keep the body and cane, drop tiny sweat dots / chroma specks.
        if len(component) < max(45, largest * 0.012):
            continue
        for x, y in component:
            keep_pixels[x, y] = alpha_pixels[x, y]
    return keep


def despill_and_remove_halo(rgba: Image.Image, alpha: Image.Image) -> Image.Image:
    out = rgba.convert("RGBA")
    pixels = out.load()
    alpha_pixels = alpha.load()
    width, height = out.size

    for y in range(height):
        for x in range(width):
            red, green, blue, _old_alpha = pixels[x, y]
            visible = alpha_pixels[x, y]
            if visible <= 10:
                pixels[x, y] = (red, green, blue, 0)
                continue
            if green > red and green > blue:
                excess = green - max(red, blue)
                green = max(max(red, blue), int(green - excess * 0.9))
            if visible < 205 and red > 212 and green > 212 and blue > 204:
                visible = 0
            pixels[x, y] = (red, green, blue, visible)

    out.putalpha(alpha)
    silhouette = alpha.point(lambda value: 255 if value > 18 else 0)
    inner = silhouette.filter(ImageFilter.MinFilter(3))
    edge = ImageChops.subtract(silhouette, inner).filter(ImageFilter.GaussianBlur(0.25))
    rgb = out.convert("RGB")
    dark = Image.new("RGB", out.size, (12, 10, 8))
    rgb = Image.composite(dark, rgb, edge)
    out = Image.merge("RGBA", (*rgb.split(), alpha))
    return out


def downscale_to_sprite(sprite: Image.Image, target_height: int) -> Image.Image:
    rgba = sprite.convert("RGBA")
    bbox = rgba.getchannel("A").point(lambda value: 255 if value > 8 else 0).getbbox()
    if bbox:
        rgba = rgba.crop(bbox)
    scale = target_height / max(1, rgba.height)
    target_width = max(1, round(rgba.width * scale))
    # LANCZOS gives the painted source a real reduction, then NEAREST restores sprite-scale pixel edges.
    reduced = rgba.resize((target_width, target_height), Image.Resampling.LANCZOS)
    chunky = reduced.resize((max(1, target_width // 2), max(1, target_height // 2)), Image.Resampling.BILINEAR)
    chunky = chunky.resize(reduced.size, Image.Resampling.NEAREST)
    blended = Image.blend(reduced, chunky, 0.34)
    canvas = Image.new("RGBA", (blended.width + 16, blended.height + 16), (0, 0, 0, 0))
    canvas.alpha_composite(blended, (8, 8))
    return canvas


def watercolor_pixel_style(sprite: Image.Image, seed: int) -> Image.Image:
    rng = random.Random(seed)
    rgba = sprite.convert("RGBA")
    alpha = rgba.getchannel("A")
    rgb = rgba.convert("RGB")

    rgb = ImageOps.autocontrast(rgb, cutoff=0.7)
    rgb = ImageEnhance.Color(rgb).enhance(0.72)
    rgb = ImageEnhance.Contrast(rgb).enhance(1.16)
    rgb = ImageEnhance.Brightness(rgb).enhance(0.86)

    # Quantize through an adaptive palette to remove CGI smoothness while preserving costume colors.
    quant = rgb.quantize(colors=56, method=Image.Quantize.MEDIANCUT).convert("RGB")
    rgb = Image.blend(rgb, quant, 0.62)

    wash = Image.new("RGB", rgb.size, (39, 29, 22))
    rgb = Image.blend(rgb, wash, 0.08)

    width, height = rgb.size
    noise_small = Image.effect_noise((max(1, width // 5), max(1, height // 5)), rng.uniform(22, 34))
    noise = noise_small.resize(rgb.size, Image.Resampling.BICUBIC).convert("L")
    noise = ImageOps.autocontrast(noise).point(lambda value: int((value - 128) * 0.30 + 128))
    rgb = ImageChops.multiply(rgb, Image.merge("RGB", (noise, noise, noise)))

    gray = ImageOps.grayscale(rgb)
    edges = gray.filter(ImageFilter.FIND_EDGES)
    edges = ImageOps.autocontrast(edges).point(lambda value: 0 if value < 38 else min(110, int(value * 0.48)))

    hatch = Image.new("L", rgb.size, 0)
    draw = ImageDraw.Draw(hatch)
    spacing = rng.choice((6, 7, 8))
    for offset in range(-height, width + height, spacing):
        draw.line((offset, height, offset + height, 0), fill=rng.randint(10, 26), width=1)
    for y in range(rng.randint(0, spacing), height, spacing * 3):
        draw.line((0, y, width, y + rng.randint(-1, 1)), fill=rng.randint(5, 14), width=1)
    hatch = hatch.filter(ImageFilter.GaussianBlur(0.25))

    visible = alpha.point(lambda value: 255 if value > 20 else 0)
    detail = ImageChops.lighter(edges, hatch)
    detail = ImageChops.multiply(detail, visible)
    dark = Image.new("RGB", rgb.size, (11, 9, 7))
    rgb = Image.composite(dark, rgb, detail)

    # Dark sprite outline. This also masks any remaining pale/chroma fringe.
    silhouette = alpha.point(lambda value: 255 if value > 15 else 0)
    inner = silhouette.filter(ImageFilter.MinFilter(3))
    rim = ImageChops.subtract(silhouette, inner).filter(ImageFilter.GaussianBlur(0.2))
    rgb = Image.composite(Image.new("RGB", rgb.size, (10, 8, 6)), rgb, rim)

    final = Image.merge("RGBA", (*rgb.split(), alpha))
    return final


def edge_white_ratio(image: Image.Image) -> float:
    rgba = image.convert("RGBA")
    alpha = rgba.getchannel("A")
    edge = ImageChops.subtract(
        alpha.point(lambda value: 255 if value > 12 else 0),
        alpha.point(lambda value: 255 if value > 218 else 0).filter(ImageFilter.MinFilter(3)),
    )
    pixels = rgba.load()
    edge_pixels = edge.load()
    total = 0
    white = 0
    for y in range(rgba.height):
        for x in range(rgba.width):
            if edge_pixels[x, y] <= 0:
                continue
            total += 1
            red, green, blue, _alpha = pixels[x, y]
            if red > 220 and green > 220 and blue > 212:
                white += 1
    return white / total if total else 0.0


def contact_sheet(paths: list[Path], out_path: Path) -> None:
    thumbs: list[Image.Image] = []
    for path in paths:
        sprite = Image.open(path).convert("RGBA")
        canvas = Image.new("RGBA", (150, 250), (30, 24, 22, 255))
        thumb = sprite.copy()
        thumb.thumbnail((130, 222), Image.Resampling.NEAREST)
        canvas.alpha_composite(thumb, ((canvas.width - thumb.width) // 2, 10))
        ImageDraw.Draw(canvas).text((8, 230), path.stem[-22:], fill=(232, 222, 202, 255))
        thumbs.append(canvas)
    sheet = Image.new("RGBA", (4 * 150, 2 * 250), (22, 18, 16, 255))
    for index, thumb in enumerate(thumbs):
        sheet.alpha_composite(thumb, ((index % 4) * 150, (index // 4) * 250))
    out_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out_path)


def main() -> None:
    args = parse_args()
    if not args.input.exists():
        raise SystemExit(f"Input sheet does not exist: {args.input}")

    for folder, boundary in (
        (args.out_root, Path("Assets/GeneratedSpriteLibrary")),
        (args.style_root, Path("Assets/GeneratedSpriteLibraryStyleMatched")),
    ):
        ensure_folder(folder, boundary)

    args.temp_root.mkdir(parents=True, exist_ok=True)
    source_copy = args.temp_root / args.input.name
    shutil.copy2(args.input, source_copy)

    sheet = Image.open(args.input).convert("RGBA")
    columns, rows = 4, 2
    cell_width = sheet.width // columns
    cell_height = sheet.height // rows
    written: list[Path] = []
    style_written: list[Path] = []

    for index, slug in enumerate(FRAME_SLUGS, start=1):
        col = (index - 1) % columns
        row = (index - 1) // columns
        cell = sheet.crop((col * cell_width, row * cell_height, (col + 1) * cell_width, (row + 1) * cell_height))
        clean = green_matte_to_alpha(cell)
        small = downscale_to_sprite(clean, args.target_height)
        final = watercolor_pixel_style(small, seed=20260610 + index)

        name = f"ButlerGuest_panic_originalstyle_{slug}_{index:02d}.png"
        out_path = args.out_root / name
        style_path = args.style_root / name
        if out_path.exists() and not args.force:
            raise SystemExit(f"Refusing to overwrite existing file without --force: {out_path}")
        final.save(out_path)
        final.save(style_path)
        write_png_meta(out_path)
        write_png_meta(style_path)
        written.append(out_path)
        style_written.append(style_path)

    manifest = {
        "guest": "ButlerGuest",
        "category": "Panic",
        "set": "OriginalStyleGuest2_20260610",
        "source_sheet_copy": str(source_copy).replace("\\", "/"),
        "source_sheet_original": str(args.input).replace("\\", "/"),
        "notes": (
            "Eight guest2 panic candidates extracted from a pose sheet, then reduced to original guest2 "
            "sprite scale with green matte removal, connected-component cleanup, muted pixel palette, "
            "watercolor noise, hatch lines, and dark rim cleanup. Raw smooth/cartoon sheet is not used directly."
        ),
        "frames": [str(path).replace("\\", "/") for path in written],
        "style_matched_frames": [str(path).replace("\\", "/") for path in style_written],
        "edge_white_ratio": {path.name: edge_white_ratio(Image.open(path)) for path in style_written},
    }
    for root in (args.out_root, args.style_root):
        manifest_path = root / "ButlerGuest_panic_originalstyle_manifest.json"
        manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
        write_text_meta(manifest_path)

    contact = args.temp_root / "guest2_panic_originalstyle_contact.png"
    contact_sheet(style_written, contact)
    print(f"Written: {len(written)} source frames")
    print(f"Written: {len(style_written)} style frames")
    print(f"Review contact sheet: {contact}")
    for path in style_written:
        image = Image.open(path).convert("RGBA")
        alpha = image.getchannel("A")
        corners = [
            image.getpixel((0, 0))[3],
            image.getpixel((image.width - 1, 0))[3],
            image.getpixel((0, image.height - 1))[3],
            image.getpixel((image.width - 1, image.height - 1))[3],
        ]
        print(
            f"{path.name}: size={image.size} alpha={alpha.getextrema()} "
            f"corners={corners} edge_white_ratio={edge_white_ratio(image):.4f}"
        )


if __name__ == "__main__":
    main()
