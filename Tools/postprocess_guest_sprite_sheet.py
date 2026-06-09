#!/usr/bin/env python3
"""Convert a generated chroma-key contact sheet into library sprite PNGs."""

from __future__ import annotations

import argparse
import shutil
import subprocess
import uuid
from pathlib import Path

from PIL import Image


POSE_PRESETS = {
    "starter": (
        3,
        2,
        [
            ("Idle", "idle_front_neutral"),
            ("Surprised", "surprised_front_startled"),
            ("Panic", "panic_cover_face_recoil"),
            ("Sitting", "sitting_idle_plain"),
            ("DiningRoomChair", "dining_room_chair_eating"),
            ("DrawingRoomCouch", "drawing_room_couch_shocked"),
        ],
    ),
    "walking": (
        2,
        2,
        [
            ("Walking", "walk_front_step_left"),
            ("Walking", "walk_front_step_right"),
            ("Walking", "walk_side_step_left"),
            ("Walking", "walk_side_step_right"),
        ],
    ),
    "expanded": (
        3,
        2,
        [
            ("Idle", "idle_tense_three_quarter"),
            ("Surprised", "surprised_hand_to_chest"),
            ("Panic", "panic_scream_hands_up"),
            ("Sitting", "sitting_tense_hands_clasped"),
            ("DiningRoomChair", "dining_room_chair_startled"),
            ("DrawingRoomCouch", "drawing_room_couch_cover_face"),
        ],
    ),
    "hurried": (
        2,
        2,
        [
            ("Walking", "walk_front_hurried_left"),
            ("Walking", "walk_front_hurried_right"),
            ("Walking", "walk_side_hurried_left"),
            ("Walking", "walk_side_hurried_right"),
        ],
    ),
}


META_TEMPLATE = """fileFormatVersion: 2
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
  - serializedVersion: 4
    buildTarget: Standalone
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
    parser.add_argument("--guest", required=True)
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--library-root", default=Path("Assets/GeneratedSpriteLibrary"), type=Path)
    parser.add_argument("--index", default="01")
    parser.add_argument("--preset", choices=sorted(POSE_PRESETS), default="starter")
    parser.add_argument("--padding", type=int, default=28)
    parser.add_argument("--transparent-threshold", type=int, default=10)
    parser.add_argument("--opaque-threshold", type=int, default=90)
    parser.add_argument("--edge-contract", type=float, default=0.0)
    return parser.parse_args()


def unity_guid() -> str:
    return uuid.uuid4().hex


def write_meta(path: Path) -> None:
    meta_path = Path(str(path) + ".meta")
    meta_path.write_text(
        META_TEMPLATE.format(guid=unity_guid(), sprite_id=unity_guid()),
        encoding="utf-8",
    )


def remove_chroma(source: Path, out: Path, args: argparse.Namespace) -> None:
    helper = Path.home() / ".codex" / "skills" / ".system" / "imagegen" / "scripts" / "remove_chroma_key.py"
    cmd = [
        "python",
        str(helper),
        "--input",
        str(source),
        "--out",
        str(out),
        "--auto-key",
        "border",
        "--soft-matte",
        "--transparent-threshold",
        str(args.transparent_threshold),
        "--opaque-threshold",
        str(args.opaque_threshold),
        "--despill",
        "--force",
    ]
    if args.edge_contract:
        cmd.extend(["--edge-contract", str(args.edge_contract)])
    subprocess.run(cmd, check=True)


def trim_alpha(cell: Image.Image, padding: int) -> Image.Image:
    rgba = cell.convert("RGBA")
    alpha = rgba.getchannel("A")
    mask = alpha.point(lambda value: 255 if value > 8 else 0)
    bbox = mask.getbbox()
    if bbox is None:
        return rgba
    cropped = rgba.crop(bbox)
    canvas = Image.new("RGBA", (cropped.width + padding * 2, cropped.height + padding * 2), (0, 0, 0, 0))
    canvas.alpha_composite(cropped, (padding, padding))
    return canvas


def validate_alpha(path: Path) -> dict[str, int | bool]:
    image = Image.open(path).convert("RGBA")
    alpha = image.getchannel("A")
    corners = [
        alpha.getpixel((0, 0)),
        alpha.getpixel((image.width - 1, 0)),
        alpha.getpixel((0, image.height - 1)),
        alpha.getpixel((image.width - 1, image.height - 1)),
    ]
    alpha_data = alpha.get_flattened_data() if hasattr(alpha, "get_flattened_data") else alpha.getdata()
    visible_pixels = sum(1 for value in alpha_data if value > 8)
    return {
        "width": image.width,
        "height": image.height,
        "transparent_corners": all(value == 0 for value in corners),
        "visible_pixels": visible_pixels,
    }


def main() -> None:
    args = parse_args()
    source = args.input.resolve()
    guest_root = args.library_root / args.guest
    contact_root = guest_root / "_ContactSheets"
    contact_root.mkdir(parents=True, exist_ok=True)

    sheet_slug = "sprite_library_sheet" if args.preset == "starter" else f"{args.preset}_sheet"
    chroma_sheet = contact_root / f"{args.guest}_{sheet_slug}_{args.index}_chroma.png"
    transparent_sheet = contact_root / f"{args.guest}_{sheet_slug}_{args.index}_alpha.png"
    shutil.copy2(source, chroma_sheet)
    remove_chroma(chroma_sheet, transparent_sheet, args)
    write_meta(transparent_sheet)

    sheet = Image.open(transparent_sheet).convert("RGBA")
    cols, rows, poses = POSE_PRESETS[args.preset]
    cell_width = sheet.width // cols
    cell_height = sheet.height // rows
    manifest_lines = [
        f"guest={args.guest}",
        f"source={source}",
        f"chroma_sheet={chroma_sheet}",
        f"transparent_sheet={transparent_sheet}",
    ]

    for i, (category, pose_slug) in enumerate(poses):
        col = i % cols
        row = i // cols
        box = (
            col * cell_width,
            row * cell_height,
            sheet.width if col == cols - 1 else (col + 1) * cell_width,
            sheet.height if row == rows - 1 else (row + 1) * cell_height,
        )
        cell = sheet.crop(box)
        sprite = trim_alpha(cell, args.padding)
        out_dir = guest_root / category
        out_dir.mkdir(parents=True, exist_ok=True)
        out_path = out_dir / f"{args.guest}_{pose_slug}_{args.index}.png"
        sprite.save(out_path)
        write_meta(out_path)
        stats = validate_alpha(out_path)
        manifest_lines.append(
            f"{category}/{out_path.name}: {stats['width']}x{stats['height']}, "
            f"transparent_corners={stats['transparent_corners']}, visible_pixels={stats['visible_pixels']}"
        )

    (contact_root / f"{args.guest}_{sheet_slug}_{args.index}_manifest.txt").write_text(
        "\n".join(manifest_lines) + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
