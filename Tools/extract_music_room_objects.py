#!/usr/bin/env python3
"""Extract first-pass Music Room object cutouts with true alpha.

This script is intentionally boring and repeatable: it crops named regions from
the final room image, applies hand-authored masks, trims transparent padding,
and writes Unity sprite metadata with a bottom-center pivot.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from uuid import uuid4

from PIL import Image, ImageDraw, ImageFilter


SOURCE = Path("Assets/Art/Final Images (DO NOT EDIT)/music room.png")
OUT_DIR = Path("Assets/Art/Objects/Music_Room")
PREVIEW = OUT_DIR / "_music_room_cutouts_preview.png"


Point = tuple[int, int]


@dataclass(frozen=True)
class CutoutSpec:
    name: str
    polygons: tuple[tuple[Point, ...], ...]
    ellipses: tuple[tuple[int, int, int, int], ...] = ()
    padding: int = 10


SPECS: tuple[CutoutSpec, ...] = (
    CutoutSpec(
        "music_grand_piano",
        (
            (
                (712, 554),
                (744, 516),
                (823, 487),
                (988, 334),
                (1097, 333),
                (1062, 407),
                (1127, 464),
                (1160, 544),
                (1124, 635),
                (1100, 785),
                (1015, 800),
                (1002, 678),
                (916, 674),
                (903, 767),
                (774, 770),
                (766, 652),
                (705, 624),
            ),
            ((751, 595), (1010, 548), (1116, 585), (1060, 674), (795, 660)),
            ((839, 481), (932, 480), (954, 542), (837, 550)),
            ((1005, 424), (1026, 416), (1081, 641), (1057, 650)),
        ),
        padding=14,
    ),
    CutoutSpec(
        "music_piano_bench",
        (
            (
                (684, 706),
                (748, 682),
                (895, 690),
                (911, 722),
                (882, 756),
                (899, 826),
                (875, 842),
                (849, 770),
                (717, 767),
                (704, 835),
                (680, 830),
                (697, 756),
            ),
        ),
        padding=10,
    ),
    CutoutSpec(
        "music_teddy_bear",
        (
            (
                (1053, 731),
                (1073, 712),
                (1097, 724),
                (1116, 708),
                (1140, 727),
                (1147, 782),
                (1122, 803),
                (1072, 800),
                (1047, 779),
            ),
        ),
        ellipses=((1062, 706, 1098, 746), (1111, 705, 1144, 742), (1074, 728, 1134, 795)),
        padding=8,
    ),
    CutoutSpec(
        "music_potted_plant",
        (
            (
                (250, 451),
                (287, 388),
                (342, 419),
                (392, 399),
                (401, 468),
                (378, 526),
                (398, 584),
                (372, 653),
                (295, 665),
                (262, 612),
                (277, 552),
                (238, 500),
            ),
            (
                (280, 531),
                (360, 526),
                (382, 608),
                (347, 671),
                (290, 660),
                (268, 603),
            ),
        ),
        ellipses=((270, 525, 382, 674),),
        padding=12,
    ),
    CutoutSpec(
        "music_center_armchair",
        (
            (
                (501, 456),
                (545, 424),
                (590, 447),
                (623, 535),
                (596, 622),
                (522, 615),
                (493, 551),
            ),
        ),
        ellipses=((510, 438, 607, 605),),
        padding=10,
    ),
    CutoutSpec(
        "music_round_table_flowers",
        (
            (
                (607, 428),
                (645, 398),
                (696, 416),
                (727, 477),
                (734, 553),
                (703, 604),
                (625, 604),
                (592, 551),
            ),
            ((628, 504), (712, 504), (725, 540), (704, 569), (630, 568), (605, 540)),
        ),
        ellipses=((622, 394, 710, 516), (600, 503, 728, 572)),
        padding=10,
    ),
    CutoutSpec(
        "music_fireplace_screen",
        (
            (
                (389, 493),
                (509, 489),
                (523, 563),
                (492, 650),
                (398, 645),
                (368, 566),
            ),
        ),
        padding=10,
    ),
    CutoutSpec(
        "music_left_parlor_armchair",
        (
            (
                (147, 471),
                (215, 444),
                (257, 504),
                (245, 591),
                (172, 606),
                (132, 556),
            ),
        ),
        ellipses=((140, 456, 252, 606),),
        padding=10,
    ),
    CutoutSpec(
        "music_left_parlor_table",
        (
            (
                (45, 504),
                (153, 498),
                (178, 549),
                (143, 604),
                (74, 601),
                (42, 551),
            ),
        ),
        padding=10,
    ),
    CutoutSpec(
        "music_right_study_desk",
        (
            (
                (1434, 468),
                (1579, 456),
                (1610, 545),
                (1580, 648),
                (1440, 637),
                (1412, 544),
            ),
        ),
        ellipses=((1455, 445, 1578, 560),),
        padding=12,
    ),
    CutoutSpec(
        "music_right_room_globe",
        (
            (
                (1530, 415),
                (1572, 395),
                (1614, 423),
                (1614, 498),
                (1581, 530),
                (1537, 520),
                (1515, 473),
            ),
        ),
        ellipses=((1525, 404, 1616, 514),),
        padding=10,
    ),
    CutoutSpec(
        "music_chandelier",
        (
            (
                (739, 0),
                (944, 0),
                (965, 185),
                (916, 238),
                (822, 238),
                (731, 197),
            ),
        ),
        ellipses=((750, 63, 956, 230),),
        padding=8,
    ),
    CutoutSpec(
        "music_mantel_candelabra_left",
        (
            (
                (311, 304),
                (364, 304),
                (379, 421),
                (350, 438),
                (315, 420),
            ),
        ),
        padding=8,
    ),
    CutoutSpec(
        "music_mantel_candelabra_right",
        (
            (
                (475, 316),
                (534, 315),
                (550, 423),
                (519, 441),
                (481, 421),
            ),
        ),
        padding=8,
    ),
    CutoutSpec(
        "music_right_wall_sconce",
        (
            (
                (1340, 321),
                (1419, 316),
                (1433, 426),
                (1396, 449),
                (1350, 429),
            ),
        ),
        padding=8,
    ),
)


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


def build_mask(size: tuple[int, int], spec: CutoutSpec) -> Image.Image:
    mask = Image.new("L", size, 0)
    draw = ImageDraw.Draw(mask)
    for polygon in spec.polygons:
        draw.polygon(polygon, fill=255)
    for ellipse in spec.ellipses:
        draw.ellipse(ellipse, fill=255)
    return mask.filter(ImageFilter.GaussianBlur(radius=0.55))


def crop_to_alpha(image: Image.Image, alpha: Image.Image, padding: int) -> Image.Image:
    bbox = alpha.getbbox()
    if bbox is None:
        raise ValueError("Empty cutout mask")
    left = max(0, bbox[0] - padding)
    top = max(0, bbox[1] - padding)
    right = min(image.width, bbox[2] + padding)
    bottom = min(image.height, bbox[3] + padding)
    rgba = image.copy()
    rgba.putalpha(alpha)
    return rgba.crop((left, top, right, bottom))


def checker(size: tuple[int, int], square: int = 16) -> Image.Image:
    image = Image.new("RGBA", size, (220, 220, 220, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], square):
        for x in range(0, size[0], square):
            if (x // square + y // square) % 2:
                draw.rectangle((x, y, x + square - 1, y + square - 1), fill=(170, 170, 170, 255))
    return image


def write_preview(paths: list[Path]) -> None:
    thumbs: list[tuple[str, Image.Image]] = []
    for path in paths:
        image = Image.open(path).convert("RGBA")
        image.thumbnail((220, 180), Image.Resampling.LANCZOS)
        tile = checker((240, 220), 12)
        tile.alpha_composite(image, ((240 - image.width) // 2, 8))
        draw = ImageDraw.Draw(tile)
        draw.rectangle((0, 192, 239, 219), fill=(20, 20, 20, 230))
        draw.text((8, 198), path.stem, fill=(255, 255, 255, 255))
        thumbs.append((path.stem, tile))

    columns = 4
    rows = (len(thumbs) + columns - 1) // columns
    sheet = Image.new("RGBA", (columns * 240, rows * 220), (0, 0, 0, 0))
    for index, (_, tile) in enumerate(thumbs):
        x = (index % columns) * 240
        y = (index // columns) * 220
        sheet.alpha_composite(tile, (x, y))
    sheet.save(PREVIEW)


def main() -> int:
    source = Image.open(SOURCE).convert("RGBA")
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    written: list[Path] = []

    for spec in SPECS:
        alpha = build_mask(source.size, spec)
        cutout = crop_to_alpha(source, alpha, spec.padding)
        out_path = OUT_DIR / f"{spec.name}.png"
        cutout.save(out_path)
        write_unity_sprite_meta(out_path)
        written.append(out_path)
        print(f"wrote {out_path} ({cutout.width}x{cutout.height})")

    write_preview(written)
    print(f"wrote {PREVIEW}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
