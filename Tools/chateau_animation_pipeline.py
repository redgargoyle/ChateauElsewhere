#!/usr/bin/env python3
"""Build a production animation library scaffold for Chateau Elsewhere.

This tool does not invent final animation by deforming existing sprites. It
collects trusted reference frames, writes AI generation briefs, creates intake
folders for generated pose and part sheets, and produces contact sheets plus
QC metadata so every new PNG can be reviewed before it reaches gameplay.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Sequence, Tuple

import numpy as np
from PIL import Image, ImageDraw, ImageFont


REPO_ROOT = Path(__file__).resolve().parents[1]
ASSETS_ROOT = REPO_ROOT / "Assets"
REGISTRY_PATH = ASSETS_ROOT / "Resources/Chapter1/GuestCharacterRegistry.json"
SOURCE_SHEET_ROOT = REPO_ROOT / "SourceSheets"
DEFAULT_OUTPUT_ROOT = ASSETS_ROOT / "AnimationLibrary"
CANVAS_SIZE = (166, 297)
FRAME_RATE = 12
GUID_RE = re.compile(r"guid:\s*([0-9a-f]{32})")


CHARACTER_DESCRIPTIONS: Dict[str, str] = {
    "Lady": (
        "Victorian lady guest in a blue dress, dark hair, pale sleeves, full skirt, "
        "delicate formal posture"
    ),
    "ButlerGuest": (
        "formal butler-like male guest, black suit, white shirt, waistcoat, dark trousers, "
        "reserved posture and compact silhouette"
    ),
    "LordAmbroseVeil": (
        "tall slim Victorian gentleman, tousled dark hair, pale face, brown frock coat, "
        "cream cravat and waistcoat, dark trousers, gloves"
    ),
    "LadySabineMarrow": (
        "petite Victorian lady, dark curled hair with teal accents, pale blue gown, teal bodice, "
        "cream sleeves, delicate skirt volume"
    ),
    "CountessElowenDusk": (
        "older aristocratic countess, ornate gold dress, deep teal embroidered shawl, brown fur collar, "
        "feathered headdress, fan"
    ),
    "MisterFlorianKnell": (
        "stern tall Victorian gentleman, dark hair and facial hair, navy blue military-style coat, "
        "gold trim, dark trousers, broad formal silhouette"
    ),
    "ProfessorLucienVale": (
        "slim nervous professor, round blue-tinted glasses, dark wavy hair, brown long coat, "
        "teal waistcoat, cream shirt, satchel"
    ),
    "MadameCoralieThread": (
        "older Victorian woman, dark jacket, bronze and gold skirt, dark updo, formal brooch, "
        "reserved upright posture"
    ),
    "BaronHectorGlass": (
        "young baron, pale blue suit, cream waistcoat, dark hair, gloved hand, polished formal shoes, "
        "upright fashionable silhouette"
    ),
    "MissIsoldeWren": (
        "young petite woman, dark updo, pale blue and cream gown, teal corset details, delicate skirt, "
        "soft anxious expression"
    ),
    "ButlerClassic": (
        "classic formal butler, black tailcoat, white shirt, waistcoat, bow tie, dark trousers, "
        "controlled professional posture"
    ),
}


EXCLUDED_REFERENCE_PATTERNS: Dict[str, Sequence[str]] = {
    "LordAmbroseVeil": (
        "guestpair02man",
        "guest7sit",
        "guest7_sitting",
    ),
}


FULL_BODY_REQUESTS = [
    {
        "id": "panic_reaction_down",
        "label": "Panic reaction facing camera",
        "views": ["down"],
        "frames": 6,
        "description": (
            "A readable startle beat: recoil, shoulders lift, eyes wide, mouth open, hands rising. "
            "The whole body must be newly painted in pose, not a tilted copy of an idle frame."
        ),
    },
    {
        "id": "panic_shriek_down",
        "label": "Shriek with hands raised",
        "views": ["down"],
        "frames": 8,
        "description": (
            "High-emotion shriek loop with both arms fully raised or thrown outward, fingers visible, "
            "costume sleeves and coat or skirt mass preserved."
        ),
    },
    {
        "id": "panic_run_left",
        "label": "Panicked run left",
        "views": ["left"],
        "frames": 8,
        "description": (
            "A frantic left-facing run cycle with longer stride and arms pumping or flailing. "
            "Feet must change contact positions and the torso must have real pose changes."
        ),
    },
    {
        "id": "panic_run_right",
        "label": "Panicked run right",
        "views": ["right"],
        "frames": 8,
        "description": (
            "A frantic right-facing run cycle with longer stride and arms pumping or flailing. "
            "Do not mirror textural details incorrectly; preserve costume asymmetry where visible."
        ),
    },
    {
        "id": "panic_turnaround",
        "label": "Back and forth turn",
        "views": ["left", "right", "down"],
        "frames": 6,
        "description": (
            "Short transition frames for reversing direction: skid, twist, half-turn, and recover. "
            "Useful for running back and forth without teleporty flips."
        ),
    },
    {
        "id": "cover_face_cower",
        "label": "Cower and cover face",
        "views": ["down", "left", "right"],
        "frames": 6,
        "description": (
            "Fear pose with bent knees, shoulders hunched, hands covering face or clutching chest. "
            "This should read as acting, not a small face decal."
        ),
    },
]


PART_REQUESTS = [
    {
        "id": "front_upper_body_parts",
        "label": "Front rig parts",
        "description": (
            "Separate transparent parts for front view: head neutral/scared/shrieking, torso, "
            "left and right upper arms, forearms, open hands, clenched hands, raised hands, "
            "coat or shawl overlays, skirt or trouser mass, legs, shoes, and character prop."
        ),
    },
    {
        "id": "side_left_upper_body_parts",
        "label": "Left-facing rig parts",
        "description": (
            "Separate transparent parts for left view with full sleeve shapes, profile head emotions, "
            "near and far arms, hands, legs, shoes, coat tails, skirt or dress side volume, and prop."
        ),
    },
    {
        "id": "side_right_upper_body_parts",
        "label": "Right-facing rig parts",
        "description": (
            "Separate transparent parts for right view with full sleeve shapes, profile head emotions, "
            "near and far arms, hands, legs, shoes, coat tails, skirt or dress side volume, and prop."
        ),
    },
    {
        "id": "hands_expression_set",
        "label": "Hands and expressions",
        "description": (
            "A small library of hands and heads: open palms, tense fingers, clenched fists, "
            "hands on cheeks, clutching chest, pointing, neutral face, gasp, scream, grimace, sob."
        ),
    },
]


@dataclass(frozen=True)
class CharacterSpec:
    character: str
    display_name: str
    animation_folder: Path
    source_sheet: Optional[Path]
    role: str


CHAPTER2_AUTHORED_GUEST_SPECS: Sequence[Tuple[str, str, str]] = (
    ("Lady", "Lady", "Assets/Animation/Lady"),
    ("ButlerGuest", "Butler Guest", "Assets/Animation/ButlerGuest"),
)


def snake_case(value: str) -> str:
    text = re.sub(r"(?<!^)([A-Z])", r"_\1", value)
    text = re.sub(r"[^a-zA-Z0-9]+", "_", text)
    return text.strip("_").lower()


def title_from_camel(value: str) -> str:
    return re.sub(r"(?<!^)([A-Z])", r" \1", value).strip()


def load_character_specs(include_butler: bool) -> List[CharacterSpec]:
    if not REGISTRY_PATH.exists():
        raise FileNotFoundError(f"Missing registry: {REGISTRY_PATH}")

    registry = json.loads(REGISTRY_PATH.read_text(encoding="utf-8"))
    specs: List[CharacterSpec] = []
    for character, display_name, animation_folder in CHAPTER2_AUTHORED_GUEST_SPECS:
        specs.append(
            CharacterSpec(
                character=character,
                display_name=display_name,
                animation_folder=REPO_ROOT / animation_folder,
                source_sheet=None,
                role="guest",
            )
        )

    for guest in registry.get("guests", []):
        character = guest["character"]
        source_sheet = SOURCE_SHEET_ROOT / f"{character}_source_sheet.png"
        specs.append(
            CharacterSpec(
                character=character,
                display_name=guest.get("display_name") or title_from_camel(character),
                animation_folder=REPO_ROOT / guest["animation_folder"],
                source_sheet=source_sheet if source_sheet.exists() else None,
                role="guest",
            )
        )

    if include_butler:
        specs.append(
            CharacterSpec(
                character="ButlerClassic",
                display_name="Butler",
                animation_folder=ASSETS_ROOT / "Animation/ButlerClassic",
                source_sheet=None,
                role="staff",
            )
        )

    return specs


def build_guid_map() -> Dict[str, Path]:
    guid_to_png: Dict[str, Path] = {}
    for meta_path in ASSETS_ROOT.rglob("*.png.meta"):
        try:
            text = meta_path.read_text(encoding="utf-8", errors="ignore")
        except OSError:
            continue

        match = GUID_RE.search(text)
        if not match:
            continue

        png_path = meta_path.with_suffix("")
        if png_path.exists():
            guid_to_png[match.group(1)] = png_path

    return guid_to_png


def parse_clip_sprite_paths(anim_path: Path, guid_to_png: Dict[str, Path]) -> List[Path]:
    if not anim_path.exists():
        return []

    text = anim_path.read_text(encoding="utf-8", errors="ignore")
    paths: List[Path] = []
    for guid in GUID_RE.findall(text):
        path = guid_to_png.get(guid)
        if path is not None and path.exists() and path not in paths:
            paths.append(path)
    return paths


def filter_reference_paths(
    spec: CharacterSpec,
    sequence_name: str,
    paths: Sequence[Path],
) -> Tuple[List[Path], List[Dict[str, str]]]:
    patterns = tuple(pattern.lower() for pattern in EXCLUDED_REFERENCE_PATTERNS.get(spec.character, ()))
    if not patterns:
        return list(paths), []

    kept: List[Path] = []
    excluded: List[Dict[str, str]] = []
    for path in paths:
        haystack = path.as_posix().lower()
        if any(pattern in haystack for pattern in patterns):
            excluded.append(
                {
                    "sequence": sequence_name,
                    "source": path.relative_to(REPO_ROOT).as_posix(),
                    "reason": "excluded legacy/mismatched source pattern",
                }
            )
        else:
            kept.append(path)
    return kept, excluded


def discover_reference_sequences(
    spec: CharacterSpec,
    guid_to_png: Dict[str, Path],
) -> Tuple[Dict[str, List[Path]], List[Dict[str, str]]]:
    clip_names = {
        "walk_down": f"{spec.character}_Walk_Down.anim",
        "walk_left": f"{spec.character}_Walk_Left.anim",
        "walk_right": f"{spec.character}_Walk_Right.anim",
        "walk_up": f"{spec.character}_Walk_Up.anim",
        "idle_down": f"{spec.character}_Idle_Down.anim",
        "idle_left": f"{spec.character}_Idle_Left.anim",
        "idle_right": f"{spec.character}_Idle_Right.anim",
        "idle_up": f"{spec.character}_Idle_Up.anim",
        "sitting": f"{spec.character}_Sitting.anim",
    }

    sequences: Dict[str, List[Path]] = {}
    excluded_sources: List[Dict[str, str]] = []
    for key, clip_name in clip_names.items():
        paths = parse_clip_sprite_paths(spec.animation_folder / clip_name, guid_to_png)
        paths, excluded = filter_reference_paths(spec, key, paths)
        excluded_sources.extend(excluded)
        if paths:
            sequences[key] = paths

    return sequences, excluded_sources


def file_sha1(path: Path) -> str:
    digest = hashlib.sha1()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_rgba(path: Path) -> Image.Image:
    return Image.open(path).convert("RGBA")


def alpha_bbox(img: Image.Image) -> Optional[Tuple[int, int, int, int]]:
    alpha = np.asarray(img.getchannel("A"))
    ys, xs = np.where(alpha > 6)
    if xs.size == 0 or ys.size == 0:
        return None
    return int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1


def frame_metrics(path: Path) -> Dict[str, object]:
    img = load_rgba(path)
    bbox = alpha_bbox(img)
    alpha = np.asarray(img.getchannel("A"))
    return {
        "file": path.relative_to(REPO_ROOT).as_posix(),
        "size": list(img.size),
        "bbox": list(bbox) if bbox else None,
        "visible_pixels": int((alpha > 6).sum()),
        "semi_alpha_pixels": int(((alpha > 6) & (alpha < 250)).sum()),
        "bottom_margin": int(img.height - bbox[3]) if bbox else None,
        "sha1": file_sha1(path),
    }


def copy_file(source: Path, destination: Path) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    if destination.exists() and file_sha1(source) == file_sha1(destination):
        return
    shutil.copy2(source, destination)


def normalize_to_canvas(source: Path) -> Image.Image:
    img = load_rgba(source)
    if img.size == CANVAS_SIZE:
        return img

    bbox = alpha_bbox(img)
    if bbox is None:
        return Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))

    content = img.crop(bbox)
    scale = min(CANVAS_SIZE[0] / content.width, CANVAS_SIZE[1] / content.height, 1.0)
    if scale < 0.999:
        content = content.resize(
            (
                max(1, int(round(content.width * scale))),
                max(1, int(round(content.height * scale))),
            ),
            Image.Resampling.LANCZOS,
        )

    canvas = Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))
    paste_x = (CANVAS_SIZE[0] - content.width) // 2
    paste_y = CANVAS_SIZE[1] - content.height
    canvas.alpha_composite(content, (paste_x, paste_y))
    return canvas


def write_reference_frame(source: Path, destination: Path) -> bool:
    destination.parent.mkdir(parents=True, exist_ok=True)
    source_img = load_rgba(source)
    output_img = normalize_to_canvas(source)
    normalized = source_img.size != CANVAS_SIZE

    if destination.exists():
        try:
            existing = load_rgba(destination)
            if existing.size == output_img.size and np.array_equal(np.asarray(existing), np.asarray(output_img)):
                return normalized
        except OSError:
            pass

    output_img.save(destination)
    return normalized


def ensure_keep(path: Path) -> None:
    path.mkdir(parents=True, exist_ok=True)
    keep = path / ".gitkeep"
    if not keep.exists():
        keep.write_text("", encoding="utf-8")


def copy_reference_assets(
    spec: CharacterSpec,
    sequences: Dict[str, List[Path]],
    character_root: Path,
) -> Dict[str, object]:
    reference_root = character_root / "reference"
    reference_full_body = reference_root / "full_body"
    if reference_full_body.exists():
        shutil.rmtree(reference_full_body)

    copied_sequences: Dict[str, List[Dict[str, object]]] = {}

    if spec.source_sheet is not None:
        copy_file(spec.source_sheet, reference_root / "source_sheet.png")

    for sequence_name, paths in sorted(sequences.items()):
        sequence_records: List[Dict[str, object]] = []
        destination_dir = reference_root / "full_body" / sequence_name
        destination_dir.mkdir(parents=True, exist_ok=True)
        for index, source_path in enumerate(paths, start=1):
            destination = destination_dir / f"{index:02d}_{source_path.name}"
            normalized = write_reference_frame(source_path, destination)
            sequence_records.append(
                {
                    "source": source_path.relative_to(REPO_ROOT).as_posix(),
                    "source_size": list(load_rgba(source_path).size),
                    "library_file": destination.relative_to(REPO_ROOT).as_posix(),
                    "normalized_to_canvas": normalized,
                    "metrics": frame_metrics(destination),
                }
            )
        copied_sequences[sequence_name] = sequence_records

    return copied_sequences


def checker_background(size: Tuple[int, int], tile: int = 8) -> Image.Image:
    img = Image.new("RGBA", size, (32, 32, 34, 255))
    draw = ImageDraw.Draw(img)
    for y in range(0, size[1], tile):
        for x in range(0, size[0], tile):
            color = (48, 48, 52, 255) if ((x // tile) + (y // tile)) % 2 else (26, 26, 29, 255)
            draw.rectangle((x, y, x + tile - 1, y + tile - 1), fill=color)
    return img


def composite_preview(path: Path, cell_size: Tuple[int, int], scale: int) -> Image.Image:
    img = load_rgba(path)
    canvas = Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))
    if img.size == CANVAS_SIZE:
        canvas = img
    else:
        x = (CANVAS_SIZE[0] - img.width) // 2
        y = CANVAS_SIZE[1] - img.height
        canvas.alpha_composite(img, (x, y))

    background = checker_background(CANVAS_SIZE)
    background.alpha_composite(canvas)
    preview = background.convert("RGB").resize(cell_size, Image.Resampling.NEAREST)
    return preview


def make_contact_sheet(spec: CharacterSpec, character_root: Path, sequence_records: Dict[str, object]) -> Optional[Path]:
    rows: List[Tuple[str, List[Path]]] = []
    for sequence_name, records in sorted(sequence_records.items()):
        paths = [REPO_ROOT / record["library_file"] for record in records[:8]]
        if paths:
            rows.append((sequence_name, paths))

    if not rows:
        return None

    scale = 2
    cell_size = (CANVAS_SIZE[0] * scale, CANVAS_SIZE[1] * scale)
    label_w = 180
    label_h = 28
    padding = 16
    cols = max(len(paths) for _, paths in rows)
    sheet_w = padding * 2 + label_w + cols * cell_size[0]
    sheet_h = padding * 2 + len(rows) * (cell_size[1] + label_h)
    sheet = Image.new("RGB", (sheet_w, sheet_h), (18, 18, 20))
    draw = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.truetype("DejaVuSans.ttf", 14)
        small_font = ImageFont.truetype("DejaVuSans.ttf", 11)
    except OSError:
        font = ImageFont.load_default()
        small_font = font

    draw.text((padding, 4), f"{spec.display_name} reference frames", fill=(238, 238, 238), font=font)
    for row_index, (sequence_name, paths) in enumerate(rows):
        y = padding + row_index * (cell_size[1] + label_h)
        draw.text((padding, y + label_h), sequence_name, fill=(230, 230, 230), font=font)
        for col, path in enumerate(paths):
            x = padding + label_w + col * cell_size[0]
            preview = composite_preview(path, cell_size, scale)
            sheet.paste(preview, (x, y + label_h))
            draw.text((x + 4, y + 4), f"{col + 1:02d}", fill=(180, 180, 180), font=small_font)

    output = character_root / "qa" / "reference_contact_sheet.png"
    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output)
    return output


def make_character_style_brief(spec: CharacterSpec) -> str:
    description = CHARACTER_DESCRIPTIONS.get(spec.character, spec.display_name)
    source_text = "reference/source_sheet.png" if spec.source_sheet is not None else "reference/full_body"
    return (
        f"{spec.display_name}: {description}. Use the current project reference art in "
        f"{source_text} as the strict visual target."
    )


def build_prompt(spec: CharacterSpec, request: Dict[str, object], request_type: str) -> str:
    style_brief = make_character_style_brief(spec)
    request_id = request["id"]
    description = request["description"]
    frames = request.get("frames")
    views = ", ".join(request.get("views", [])) if request.get("views") else "front, left, right as specified"
    canvas = f"{CANVAS_SIZE[0]} x {CANVAS_SIZE[1]} per final cropped sprite"

    if request_type == "full_body":
        output_detail = (
            f"Create a clean sprite production sheet for {frames} full-body frames, views: {views}. "
            f"Each frame must crop cleanly to {canvas}, bottom-foot pivot, transparent final PNG."
        )
    else:
        output_detail = (
            "Create a clean separated-parts production sheet. Every part must be a complete painted "
            "sprite element with overlap allowance at joints, transparent final PNG, and no cast shadow."
        )

    return "\n".join(
        [
            f"Use case: stylized-concept",
            f"Asset type: Chateau Elsewhere {request_type} animation asset",
            f"Character: {style_brief}",
            f"Primary request: {request['label']}",
            f"Action/part detail: {description}",
            output_detail,
            "Style lock: match the existing painterly Victorian sprite style, inked edges, muted texture, costume color, and body proportions.",
            "Scale lock: match the source sheet head/body ratio and in-game sprite scale; do not enlarge the character or render it as a high-detail portrait.",
            "Quality bar: arms and legs must be real painted limbs with sleeve/trouser/skirt volume and visible hands or shoes where appropriate.",
            "Avoid: stick limbs, single-line arms, pasted idle body, tilted sticker motion, chibi proportions, realistic portrait rendering, modern clothing, extra props, labels, text, shadows.",
            "Background: transparent if supported. If not supported, use a perfectly flat #00ff00 chroma-key background with no shadows or gradients.",
            "Layout: generous padding between cells, no overlap between cells, consistent scale across the sheet.",
        ]
    )


def write_generation_requests(spec: CharacterSpec, character_root: Path) -> Path:
    requests_dir = character_root / "requests"
    requests_dir.mkdir(parents=True, exist_ok=True)
    jsonl_path = requests_dir / "generation_prompts.jsonl"
    md_path = requests_dir / "README.md"
    records: List[Dict[str, object]] = []

    for request in FULL_BODY_REQUESTS:
        intake_dir = character_root / "intake" / "full_body" / request["id"]
        approved_dir = character_root / "approved" / "full_body" / request["id"] / "frames"
        ensure_keep(intake_dir)
        ensure_keep(approved_dir)
        records.append(
            {
                "id": f"{spec.character}.full_body.{request['id']}",
                "character": spec.character,
                "display_name": spec.display_name,
                "request_type": "full_body",
                "frames": request["frames"],
                "views": request["views"],
                "intake_directory": intake_dir.relative_to(REPO_ROOT).as_posix(),
                "approved_directory": approved_dir.relative_to(REPO_ROOT).as_posix(),
                "prompt": build_prompt(spec, request, "full_body"),
            }
        )

    for request in PART_REQUESTS:
        intake_dir = character_root / "intake" / "parts" / request["id"]
        approved_dir = character_root / "approved" / "parts" / request["id"]
        ensure_keep(intake_dir)
        ensure_keep(approved_dir)
        records.append(
            {
                "id": f"{spec.character}.parts.{request['id']}",
                "character": spec.character,
                "display_name": spec.display_name,
                "request_type": "parts",
                "intake_directory": intake_dir.relative_to(REPO_ROOT).as_posix(),
                "approved_directory": approved_dir.relative_to(REPO_ROOT).as_posix(),
                "prompt": build_prompt(spec, request, "parts"),
            }
        )

    with jsonl_path.open("w", encoding="utf-8") as handle:
        for record in records:
            handle.write(json.dumps(record, ensure_ascii=True) + "\n")

    md_lines = [
        f"# {spec.display_name} Animation Requests",
        "",
        "Use these prompts to generate new pose sheets or separated-part sheets.",
        "Generated images go into `intake/`; only reviewed, cropped, alpha-clean PNGs go into `approved/`.",
        "",
    ]
    for record in records:
        md_lines.extend(
            [
                f"## {record['id']}",
                "",
                f"Intake: `{record['intake_directory']}`",
                f"Approved: `{record['approved_directory']}`",
                "",
                "```text",
                record["prompt"],
                "```",
                "",
            ]
        )
    md_path.write_text("\n".join(md_lines), encoding="utf-8")
    return jsonl_path


def write_character_manifest(
    spec: CharacterSpec,
    character_root: Path,
    copied_sequences: Dict[str, object],
    excluded_sources: Sequence[Dict[str, str]],
    contact_sheet: Optional[Path],
    generation_requests: Path,
) -> Dict[str, object]:
    warnings: List[str] = []
    for sequence_name, records in copied_sequences.items():
        for record in records:
            metrics = record["metrics"]
            if metrics["size"] != list(CANVAS_SIZE):
                warnings.append(f"{sequence_name}: {metrics['file']} is {metrics['size']}, expected {list(CANVAS_SIZE)}")
            if metrics["bbox"] is None:
                warnings.append(f"{sequence_name}: {metrics['file']} has no visible alpha")

    manifest = {
        "character": spec.character,
        "display_name": spec.display_name,
        "role": spec.role,
        "canvas_size": list(CANVAS_SIZE),
        "pivot": [0.5, 0.0],
        "frame_rate": FRAME_RATE,
        "source_sheet": spec.source_sheet.relative_to(REPO_ROOT).as_posix() if spec.source_sheet else "",
        "style_brief": make_character_style_brief(spec),
        "reference_sequences": copied_sequences,
        "excluded_sources": list(excluded_sources),
        "contact_sheet": contact_sheet.relative_to(REPO_ROOT).as_posix() if contact_sheet else "",
        "generation_requests": generation_requests.relative_to(REPO_ROOT).as_posix(),
        "warnings": warnings,
    }
    (character_root / "manifest.json").write_text(json.dumps(manifest, indent=2, ensure_ascii=True) + "\n", encoding="utf-8")
    return manifest


def write_root_catalog(output_root: Path, manifests: Sequence[Dict[str, object]]) -> None:
    catalog = {
        "generated_by": "Tools/chateau_animation_pipeline.py",
        "purpose": "Reference, AI intake, and approved animation asset database for Chateau Elsewhere.",
        "canvas_size": list(CANVAS_SIZE),
        "pivot": [0.5, 0.0],
        "frame_rate": FRAME_RATE,
        "policy": {
            "reference": "Trusted existing game/source art.",
            "intake": "AI or manual candidate art awaiting crop, alpha, style, and motion review.",
            "approved": "Reviewed PNGs allowed to feed Unity clips and rigs.",
        },
        "characters": [
            {
                "character": item["character"],
                "display_name": item["display_name"],
                "role": item["role"],
                "manifest": f"Assets/AnimationLibrary/{item['character']}/manifest.json",
                "contact_sheet": item.get("contact_sheet", ""),
                "warnings": item.get("warnings", []),
            }
            for item in manifests
        ],
    }
    output_root.mkdir(parents=True, exist_ok=True)
    (output_root / "_catalog.json").write_text(json.dumps(catalog, indent=2, ensure_ascii=True) + "\n", encoding="utf-8")


def build_library(output_root: Path, include_butler: bool) -> None:
    output_root.mkdir(parents=True, exist_ok=True)
    guid_to_png = build_guid_map()
    manifests: List[Dict[str, object]] = []

    for spec in load_character_specs(include_butler=include_butler):
        character_root = output_root / spec.character
        character_root.mkdir(parents=True, exist_ok=True)
        sequences, excluded_sources = discover_reference_sequences(spec, guid_to_png)
        copied = copy_reference_assets(spec, sequences, character_root)
        contact_sheet = make_contact_sheet(spec, character_root, copied)
        generation_requests = write_generation_requests(spec, character_root)
        manifest = write_character_manifest(spec, character_root, copied, excluded_sources, contact_sheet, generation_requests)
        manifests.append(manifest)

    write_root_catalog(output_root, manifests)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--output-root",
        type=Path,
        default=DEFAULT_OUTPUT_ROOT,
        help="Animation library root. Defaults to Assets/AnimationLibrary.",
    )
    parser.add_argument(
        "--no-butler",
        action="store_true",
        help="Only build guest libraries; omit the classic butler.",
    )
    args = parser.parse_args()

    output_root = args.output_root
    if not output_root.is_absolute():
        output_root = REPO_ROOT / output_root

    build_library(output_root=output_root, include_butler=not args.no_butler)
    print(f"Built animation library at {output_root.relative_to(REPO_ROOT)}")


if __name__ == "__main__":
    main()
