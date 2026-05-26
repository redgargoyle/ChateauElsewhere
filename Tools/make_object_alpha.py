#!/usr/bin/env python3
"""Convert object renders on white/checker backgrounds into true-alpha PNGs.

This is intentionally simple for the room-object workflow:
drop source JPG/PNG/WebP files into an input folder, run this script, then use
the generated PNGs as SpriteRenderer sprites with bottom-center pivots in Unity.
"""

from __future__ import annotations

import argparse
from collections import deque
from pathlib import Path
from uuid import uuid4

from PIL import Image, ImageFilter


IMAGE_EXTENSIONS = {".jpg", ".jpeg", ".png", ".webp"}


def is_background_like(r: int, g: int, b: int, *, threshold: int) -> bool:
    high = min(r, g, b)
    low = max(r, g, b)
    saturation = low - high
    brightness = (r + g + b) / 3

    # White JPG backgrounds and baked transparency-checker backgrounds both end
    # up as bright, low-saturation pixels. Only border-connected areas are cut.
    return high >= threshold and saturation <= 32 and brightness >= threshold + 3


def background_mask(
    pixels,
    width: int,
    height: int,
    threshold: int,
    hole_min_area: int,
) -> bytearray:
    candidates = bytearray(width * height)
    for y in range(height):
        for x in range(width):
            r, g, b = pixels[x, y][:3]
            if is_background_like(r, g, b, threshold=threshold):
                candidates[y * width + x] = 1

    visited = bytearray(width * height)
    background = bytearray(width * height)

    for start_index, is_candidate in enumerate(candidates):
        if not is_candidate or visited[start_index]:
            continue

        component: list[int] = []
        touches_border = False
        queue: deque[int] = deque([start_index])
        visited[start_index] = 1

        while queue:
            index = queue.popleft()
            component.append(index)
            x = index % width
            y = index // width
            if x == 0 or y == 0 or x == width - 1 or y == height - 1:
                touches_border = True

            neighbors = []
            if x > 0:
                neighbors.append(index - 1)
            if x < width - 1:
                neighbors.append(index + 1)
            if y > 0:
                neighbors.append(index - width)
            if y < height - 1:
                neighbors.append(index + width)

            for neighbor in neighbors:
                if candidates[neighbor] and not visited[neighbor]:
                    visited[neighbor] = 1
                    queue.append(neighbor)

        if touches_border or len(component) >= hole_min_area:
            for index in component:
                background[index] = 1

    return background


def defringe_alpha_edges(
    image: Image.Image,
    *,
    source_alpha: int = 248,
    transparent_cutoff: int = 8,
) -> Image.Image:
    """Bleed object colors into transparent pixels to avoid white matte halos."""
    image = image.convert("RGBA")
    width, height = image.size
    pixel_data = getattr(image, "get_flattened_data", image.getdata)
    pixels = list(pixel_data())
    total = width * height

    visited = bytearray(total)
    fill_r = bytearray(total)
    fill_g = bytearray(total)
    fill_b = bytearray(total)
    queue: deque[int] = deque()

    for index, (r, g, b, a) in enumerate(pixels):
        if a >= source_alpha:
            visited[index] = 1
            fill_r[index] = r
            fill_g[index] = g
            fill_b[index] = b
            queue.append(index)

    if not queue:
        return image

    while queue:
        index = queue.popleft()
        x = index % width
        y = index // width

        neighbors = []
        if x > 0:
            neighbors.append(index - 1)
        if x < width - 1:
            neighbors.append(index + 1)
        if y > 0:
            neighbors.append(index - width)
        if y < height - 1:
            neighbors.append(index + width)

        for neighbor in neighbors:
            if visited[neighbor]:
                continue
            visited[neighbor] = 1
            fill_r[neighbor] = fill_r[index]
            fill_g[neighbor] = fill_g[index]
            fill_b[neighbor] = fill_b[index]
            queue.append(neighbor)

    cleaned_pixels = []
    for index, (r, g, b, a) in enumerate(pixels):
        if a <= transparent_cutoff:
            a = 0
        elif a >= source_alpha:
            a = 255

        if a < 255:
            r = fill_r[index]
            g = fill_g[index]
            b = fill_b[index]

        cleaned_pixels.append((r, g, b, a))

    cleaned = Image.new("RGBA", image.size)
    cleaned.putdata(cleaned_pixels)
    return cleaned


def remove_background(
    source: Path,
    out_path: Path,
    threshold: int,
    padding: int,
    hole_min_area: int,
) -> None:
    image = Image.open(source).convert("RGBA")
    pixels = image.load()
    width, height = image.size
    background = background_mask(pixels, width, height, threshold, hole_min_area)

    alpha = Image.new("L", image.size, 255)
    alpha_pixels = alpha.load()

    for y in range(height):
        for x in range(width):
            if background[y * width + x]:
                alpha_pixels[x, y] = 0

    # Slight feathering avoids crunchy JPG halos without inventing extra logic.
    alpha = alpha.filter(ImageFilter.GaussianBlur(radius=0.35))
    image.putalpha(alpha)
    image = defringe_alpha_edges(image)
    alpha = image.getchannel("A")

    bbox = alpha.getbbox()
    if bbox:
        left = max(0, bbox[0] - padding)
        top = max(0, bbox[1] - padding)
        right = min(width, bbox[2] + padding)
        bottom = min(height, bbox[3] + padding)
        image = image.crop((left, top, right, bottom))

    out_path.parent.mkdir(parents=True, exist_ok=True)
    image.save(out_path)


def iter_sources(paths: list[Path]) -> list[Path]:
    sources: list[Path] = []
    for path in paths:
        if path.is_dir():
            sources.extend(
                sorted(
                    child
                    for child in path.iterdir()
                    if child.is_file() and child.suffix.lower() in IMAGE_EXTENSIONS
                )
            )
        elif path.is_file() and path.suffix.lower() in IMAGE_EXTENSIONS:
            sources.append(path)
    return sources


def output_name(source: Path) -> str:
    clean = source.stem.strip().strip("()").replace(" ", "_")
    if clean.isdigit():
        return f"object_{int(clean):02d}.png"
    return f"{clean}_alpha.png"


def meta_guid(meta_path: Path) -> str:
    if meta_path.exists():
        for line in meta_path.read_text(encoding="utf-8").splitlines():
            if line.startswith("guid: "):
                return line.split("guid: ", 1)[1].strip()
    return uuid4().hex


def write_unity_sprite_meta(image_path: Path) -> None:
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
  alignment: 7
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
  - serializedVersion: 3
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
  - serializedVersion: 3
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
    physicsShape: []
    bones: []
    spriteID: 5e97eb03825dee720800000000000000
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""",
        encoding="utf-8",
    )


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("inputs", nargs="+", type=Path)
    parser.add_argument("--out-dir", type=Path, default=Path("Assets/Art/Objects"))
    parser.add_argument("--output-names", nargs="*", default=[])
    parser.add_argument("--threshold", type=int, default=216)
    parser.add_argument("--hole-min-area", type=int, default=96)
    parser.add_argument("--padding", type=int, default=8)
    parser.add_argument("--unity-meta", action="store_true")
    args = parser.parse_args()

    sources = iter_sources(args.inputs)
    if not sources:
        raise SystemExit("No source images found.")
    if args.output_names and len(args.output_names) != len(sources):
        raise SystemExit("--output-names must match the number of source images.")

    for index, source in enumerate(sources):
        name = args.output_names[index] if args.output_names else output_name(source)
        if not name.lower().endswith(".png"):
            name += ".png"
        out_path = args.out_dir / name
        remove_background(source, out_path, args.threshold, args.padding, args.hole_min_area)
        if args.unity_meta:
            write_unity_sprite_meta(out_path)
        print(f"{source} -> {out_path}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
