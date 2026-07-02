#!/usr/bin/env python3
"""Wire dining room chairs for y-axis occlusion and recessed movement blocking."""

from __future__ import annotations

import json
import re
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SCENE_PATH = ROOT / "Assets/Scenes/Gameplay.unity"
MANIFEST_PATH = ROOT / "Assets/Art/Objects/DiningTableChairSplits/_dining_chair_splits_manifest.json"

DINING_ROOM_TRANSFORM_ID = 2300000016
WORLD_Y_SORT_GUID = "75f090bb68ab450d9703d9581c5c543a"
OBJECT_BLOCKER_GUID = "b95469e02af64fee8b29689edb9b583a"
RIGHT_FRONT_TRIMMED_GUID = "24a9b0f498b34b16ac395abdbaa2d168"
RIGHT_FRONT_POSITION = (330.74, -367.21, -7166.9155)
RIGHT_FRONT_SCALE = (111.15611, 116.90039, 79.63239)

BLOCKER_HEIGHT_FRACTION = 0.22
BLOCKER_WIDTH_FRACTION = 0.58

CHAIRS = [
    "DiningHeadChairOverlay",
    "DiningChair_Left01Front_Overlay",
    "DiningChair_Left02MidFront_Overlay",
    "DiningChair_Left03MidBack_Overlay",
    "DiningChair_Left04Back_Overlay",
    "DiningChair_Right01Back_Overlay",
    "DiningChair_Right02MidFront_Overlay",
    "DiningChair_Right03MidBack_Overlay",
    "DiningChair_Right04Front_Overlay",
]

SIDE_CHAIRS = CHAIRS[1:]

LEGACY_STRAY_CHAIRS = [
    "DiningChair_Right03Back_Overlay",
]


@dataclass
class UnityBlock:
    unity_type: int
    file_id: int
    text: str


def fmt(value: float) -> str:
    if abs(value) < 0.000005:
        value = 0.0

    text = f"{value:.5f}".rstrip("0").rstrip(".")
    return text if text and text != "-0" else "0"


def split_scene(scene_text: str) -> tuple[str, list[UnityBlock]]:
    marker = re.compile(r"(?m)^--- !u!(\d+) &(-?\d+).*$")
    matches = list(marker.finditer(scene_text))

    if not matches:
        raise RuntimeError("Gameplay scene does not contain Unity YAML object blocks.")

    preamble = scene_text[: matches[0].start()]
    blocks: list[UnityBlock] = []

    for index, match in enumerate(matches):
        start = match.start()
        end = matches[index + 1].start() if index + 1 < len(matches) else len(scene_text)
        blocks.append(UnityBlock(int(match.group(1)), int(match.group(2)), scene_text[start:end]))

    return preamble, blocks


def join_scene(preamble: str, blocks: list[UnityBlock]) -> str:
    return preamble + "".join(block.text for block in blocks)


def get_name(block: str) -> str | None:
    match = re.search(r"^  m_Name: '?([^'\n]*)'?$", block, re.MULTILINE)
    return match.group(1).rstrip() if match else None


def component_ids(game_object_block: str) -> list[int]:
    return [int(match) for match in re.findall(r"- component: \{fileID: (-?\d+)\}", game_object_block)]


def vector_field(block: str, field_name: str) -> tuple[float, float, float]:
    pattern = rf"^  {re.escape(field_name)}: \{{x: ([^,]+), y: ([^,]+), z: ([^}}]+)\}}$"
    match = re.search(pattern, block, re.MULTILINE)

    if not match:
        raise RuntimeError(f"Missing {field_name} in block:\n{block[:240]}")

    return tuple(float(value) for value in match.groups())


def load_manifest_sizes_by_guid() -> dict[str, tuple[float, float]]:
    manifest = json.loads(MANIFEST_PATH.read_text())
    sizes: dict[str, tuple[float, float]] = {}

    for chair in manifest["chairs"]:
        meta_path = ROOT / f"{chair['file']}.meta"
        meta_text = meta_path.read_text()
        guid_match = re.search(r"^guid: ([0-9a-f]+)$", meta_text, re.MULTILINE)

        if not guid_match:
            raise RuntimeError(f"Missing guid in {meta_path}")

        width_pixels, height_pixels = chair["size"]
        sizes[guid_match.group(1)] = (width_pixels / 100.0, height_pixels / 100.0)

    return sizes


def build_indexes(blocks: list[UnityBlock]) -> tuple[dict[int, int], dict[str, int]]:
    block_indexes = {block.file_id: index for index, block in enumerate(blocks)}
    names_to_ids: dict[str, int] = {}

    for block in blocks:
        if block.unity_type != 1:
            continue

        name = get_name(block.text)

        if name:
            names_to_ids[name] = block.file_id

    return block_indexes, names_to_ids


def require_block(blocks_by_id: dict[int, int], blocks: list[UnityBlock], file_id: int) -> UnityBlock:
    try:
        return blocks[blocks_by_id[file_id]]
    except KeyError as exc:
        raise RuntimeError(f"Missing Unity object block {file_id}") from exc


def require_component(
    blocks_by_id: dict[int, int],
    blocks: list[UnityBlock],
    components: list[int],
    unity_type: int,
    description: str,
) -> UnityBlock:
    for component_file_id in components:
        block = require_block(blocks_by_id, blocks, component_file_id)

        if block.unity_type == unity_type:
            return block

    raise RuntimeError(f"Missing {description}")


def find_mono_behaviour(
    blocks_by_id: dict[int, int],
    blocks: list[UnityBlock],
    components: list[int],
    script_guid: str,
) -> UnityBlock | None:
    for component_file_id in components:
        block = require_block(blocks_by_id, blocks, component_file_id)

        if block.unity_type == 114 and f"guid: {script_guid}" in block.text:
            return block

    return None


def next_free_file_id(used_file_ids: set[int], preferred_file_id: int) -> int:
    candidate = preferred_file_id

    while candidate in used_file_ids:
        candidate += 1

    used_file_ids.add(candidate)
    return candidate


def add_component_reference(game_object_block: UnityBlock, component_file_id: int) -> None:
    if f"fileID: {component_file_id}" in game_object_block.text:
        return

    lines = game_object_block.text.splitlines(keepends=True)
    insert_at = None

    for index, line in enumerate(lines):
        if re.match(r"^  - component: \{fileID: -?\d+\}\n$", line):
            insert_at = index + 1

    if insert_at is None:
        raise RuntimeError(f"Could not locate m_Component list on GameObject {game_object_block.file_id}")

    lines.insert(insert_at, f"  - component: {{fileID: {component_file_id}}}\n")
    game_object_block.text = "".join(lines)


def make_world_y_sort_block(component_file_id: int, game_object_file_id: int, y_reference_file_id: int) -> str:
    return f"""--- !u!114 &{component_file_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {game_object_file_id}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {WORLD_Y_SORT_GUID}, type: 3}}
  m_Name:
  m_EditorClassIdentifier: Assembly-CSharp::WorldYSortSpriteRenderer
  sortingLayerName: People
  sortingOrderBase: 1000
  sortingOrderPerYUnit: 100
  sortingOrderOffset: 0
  includeChildren: 1
  forcePivotSortPoint: 1
  sortSolidObstacleFromPhysicalBottom: 0
  forceBehindPlayerInsidePhysicalBounds: 0
  behindPlayerSortingOffset: -1
  yReference: {{fileID: {y_reference_file_id}}}
"""


def ensure_world_y_sort(
    blocks: list[UnityBlock],
    blocks_by_id: dict[int, int],
    used_file_ids: set[int],
    game_object_file_id: int,
) -> None:
    game_object_block = require_block(blocks_by_id, blocks, game_object_file_id)
    components = component_ids(game_object_block.text)
    transform_block = require_component(blocks_by_id, blocks, components, 4, f"{game_object_file_id} Transform")
    sprite_block = require_component(blocks_by_id, blocks, components, 212, f"{game_object_file_id} SpriteRenderer")
    y_sort_block = find_mono_behaviour(blocks_by_id, blocks, components, WORLD_Y_SORT_GUID)

    if y_sort_block is None:
        y_sort_file_id = next_free_file_id(used_file_ids, game_object_file_id + 3)
        add_component_reference(game_object_block, y_sort_file_id)
        sprite_index = blocks_by_id[sprite_block.file_id]
        new_block = UnityBlock(
            114,
            y_sort_file_id,
            make_world_y_sort_block(y_sort_file_id, game_object_file_id, transform_block.file_id),
        )
        blocks.insert(sprite_index + 1, new_block)
        blocks_by_id.clear()
        blocks_by_id.update({block.file_id: index for index, block in enumerate(blocks)})
        return

    y_sort_block.text = make_world_y_sort_block(y_sort_block.file_id, game_object_file_id, transform_block.file_id)


def replace_line(block_text: str, field_name: str, replacement: str) -> str:
    pattern = rf"^  {re.escape(field_name)}: .*$"
    new_text, count = re.subn(pattern, f"  {field_name}: {replacement}", block_text, flags=re.MULTILINE)

    if count == 0:
        raise RuntimeError(f"Could not replace {field_name} in block:\n{block_text[:240]}")

    return new_text


def sprite_guid(sprite_block: UnityBlock) -> str | None:
    match = re.search(r"m_Sprite: \{fileID: [^,]+, guid: ([0-9a-f]+), type: 3\}", sprite_block.text)
    return match.group(1) if match else None


def update_sprite_renderer(sprite_block: UnityBlock, guid: str, size: tuple[float, float] | None = None) -> None:
    sprite_block.text = replace_line(
        sprite_block.text,
        "m_Sprite",
        f"{{fileID: 21300000, guid: {guid}, type: 3}}",
    )

    if size is not None:
        sprite_block.text = replace_line(
            sprite_block.text,
            "m_Size",
            f"{{x: {fmt(size[0])}, y: {fmt(size[1])}}}",
        )

    sprite_block.text = replace_line(sprite_block.text, "m_WasSpriteAssigned", "1")
    sprite_block.text = replace_line(sprite_block.text, "m_SpriteSortPoint", "1")


def make_chair_game_object_block(game_object_file_id: int, transform_file_id: int, sprite_file_id: int, chair_name: str) -> str:
    return f"""--- !u!1 &{game_object_file_id}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {transform_file_id}}}
  - component: {{fileID: {sprite_file_id}}}
  m_Layer: 0
  m_Name: {chair_name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
"""


def make_chair_transform_block(transform_file_id: int, game_object_file_id: int, parent_transform_file_id: int) -> str:
    return f"""--- !u!4 &{transform_file_id}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {game_object_file_id}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: {fmt(RIGHT_FRONT_POSITION[0])}, y: {fmt(RIGHT_FRONT_POSITION[1])}, z: {fmt(RIGHT_FRONT_POSITION[2])}}}
  m_LocalScale: {{x: {fmt(RIGHT_FRONT_SCALE[0])}, y: {fmt(RIGHT_FRONT_SCALE[1])}, z: {fmt(RIGHT_FRONT_SCALE[2])}}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {parent_transform_file_id}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
"""


def clone_sprite_renderer_block(
    source_sprite_block: UnityBlock,
    sprite_file_id: int,
    game_object_file_id: int,
    sprite_guid_value: str,
    sprite_size: tuple[float, float],
) -> str:
    text = re.sub(
        r"^--- !u!212 &-?\d+",
        f"--- !u!212 &{sprite_file_id}",
        source_sprite_block.text,
        count=1,
        flags=re.MULTILINE,
    )
    text = replace_line(text, "m_GameObject", f"{{fileID: {game_object_file_id}}}")
    text = replace_line(text, "m_SortingOrder", "1000")
    text = replace_line(text, "m_Size", f"{{x: {fmt(sprite_size[0])}, y: {fmt(sprite_size[1])}}}")
    text = replace_line(text, "m_Sprite", f"{{fileID: 21300000, guid: {sprite_guid_value}, type: 3}}")
    text = replace_line(text, "m_WasSpriteAssigned", "1")
    text = replace_line(text, "m_SpriteSortPoint", "1")
    return text


def ensure_missing_right_front_chair(
    blocks: list[UnityBlock],
    blocks_by_id: dict[int, int],
    names_to_ids: dict[str, int],
    used_file_ids: set[int],
    sizes_by_guid: dict[str, tuple[float, float]],
) -> None:
    chair_name = "DiningChair_Right04Front_Overlay"

    if chair_name in names_to_ids:
        return

    reference_name = "DiningChair_Right03MidBack_Overlay"

    if reference_name not in names_to_ids:
        raise RuntimeError(f"Cannot restore {chair_name}; missing reference chair {reference_name}.")

    reference_components = component_ids(require_block(blocks_by_id, blocks, names_to_ids[reference_name]).text)
    reference_sprite = require_component(
        blocks_by_id,
        blocks,
        reference_components,
        212,
        f"{reference_name} SpriteRenderer",
    )

    if RIGHT_FRONT_TRIMMED_GUID not in sizes_by_guid:
        raise RuntimeError("Right-front dining chair sprite guid is missing from manifest sizes.")

    game_object_file_id = next_free_file_id(used_file_ids, 3910000070)
    transform_file_id = next_free_file_id(used_file_ids, game_object_file_id + 1)
    sprite_file_id = next_free_file_id(used_file_ids, game_object_file_id + 2)
    new_blocks = [
        UnityBlock(1, game_object_file_id, make_chair_game_object_block(
            game_object_file_id,
            transform_file_id,
            sprite_file_id,
            chair_name,
        )),
        UnityBlock(4, transform_file_id, make_chair_transform_block(
            transform_file_id,
            game_object_file_id,
            DINING_ROOM_TRANSFORM_ID,
        )),
        UnityBlock(212, sprite_file_id, clone_sprite_renderer_block(
            reference_sprite,
            sprite_file_id,
            game_object_file_id,
            RIGHT_FRONT_TRIMMED_GUID,
            sizes_by_guid[RIGHT_FRONT_TRIMMED_GUID],
        )),
    ]
    insert_after_index = blocks_by_id[reference_sprite.file_id]
    blocks[insert_after_index + 1 : insert_after_index + 1] = new_blocks
    blocks_by_id.clear()
    blocks_by_id.update({block.file_id: index for index, block in enumerate(blocks)})

    dining_room_transform = require_block(blocks_by_id, blocks, DINING_ROOM_TRANSFORM_ID)
    add_child_to_transform(dining_room_transform, transform_file_id)


def disable_legacy_stray_chairs(
    blocks: list[UnityBlock],
    blocks_by_id: dict[int, int],
    names_to_ids: dict[str, int],
) -> None:
    for legacy_name in LEGACY_STRAY_CHAIRS:
        game_object_file_id = names_to_ids.get(legacy_name)

        if game_object_file_id is None:
            continue

        game_object_block = require_block(blocks_by_id, blocks, game_object_file_id)
        game_object_block.text = replace_line(game_object_block.text, "m_Name", f"Disabled_{legacy_name}_Legacy")
        game_object_block.text = replace_line(game_object_block.text, "m_IsActive", "0")


def update_chair_sprite_sizes(
    blocks: list[UnityBlock],
    blocks_by_id: dict[int, int],
    names_to_ids: dict[str, int],
    sizes_by_guid: dict[str, tuple[float, float]],
) -> None:
    for chair_name in SIDE_CHAIRS:
        game_object_file_id = names_to_ids[chair_name]
        components = component_ids(require_block(blocks_by_id, blocks, game_object_file_id).text)
        sprite_block = require_component(blocks_by_id, blocks, components, 212, f"{chair_name} SpriteRenderer")
        current_guid = sprite_guid(sprite_block)

        if chair_name == "DiningChair_Right04Front_Overlay":
            current_guid = RIGHT_FRONT_TRIMMED_GUID

        if current_guid in sizes_by_guid:
            update_sprite_renderer(sprite_block, current_guid, sizes_by_guid[current_guid])


def make_blocker_blocks(
    game_object_file_id: int,
    transform_file_id: int,
    marker_file_id: int,
    collider_file_id: int,
    blocker_name: str,
    chair_name: str,
    chair_file_id: int,
    parent_transform_file_id: int,
    width: float,
    height: float,
) -> list[UnityBlock]:
    offset_y = height * 0.5

    game_object_text = f"""--- !u!1 &{game_object_file_id}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {transform_file_id}}}
  - component: {{fileID: {collider_file_id}}}
  - component: {{fileID: {marker_file_id}}}
  m_Layer: 0
  m_Name: {blocker_name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
"""
    transform_text = f"""--- !u!4 &{transform_file_id}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {game_object_file_id}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {parent_transform_file_id}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
"""
    marker_text = f"""--- !u!114 &{marker_file_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {game_object_file_id}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {OBJECT_BLOCKER_GUID}, type: 3}}
  m_Name:
  m_EditorClassIdentifier: Assembly-CSharp::ObjectMovementBlocker2D
  sourceObject: {{fileID: {chair_file_id}}}
  sourceObjectName: {chair_name}
  sourceRoomName: Dining Room
  category: Chair
  footprintHeightFraction: {fmt(BLOCKER_HEIGHT_FRACTION)}
  generatedByCollisionBoxTool: 0
  sortSourceRenderers: 0
  sourceSortingLayerName: People
  sourceSortingOrderBase: 1000
  sourceSortingOrderPerYUnit: 100
  sourceSortingOrderOffset: 0
  forceSourcePivotSortPoint: 1
  authoringNote: recessed lower chair footprint; top/back remains pass-through for y-axis occlusion checks
"""
    collider_text = f"""--- !u!61 &{collider_file_id}
BoxCollider2D:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {game_object_file_id}}}
  m_Enabled: 1
  serializedVersion: 3
  m_Density: 1
  m_Material: {{fileID: 0}}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_LayerOverridePriority: 0
  m_ForceSendLayers:
    serializedVersion: 2
    m_Bits: 4294967295
  m_ForceReceiveLayers:
    serializedVersion: 2
    m_Bits: 4294967295
  m_ContactCaptureLayers:
    serializedVersion: 2
    m_Bits: 4294967295
  m_CallbackLayers:
    serializedVersion: 2
    m_Bits: 4294967295
  m_IsTrigger: 1
  m_UsedByEffector: 0
  m_CompositeOperation: 0
  m_CompositeOrder: 0
  m_Offset: {{x: 0, y: {fmt(offset_y)}}}
  m_SpriteTilingProperty:
    border: {{x: 0, y: 0, z: 0, w: 0}}
    pivot: {{x: 0.5, y: 0}}
    oldSize: {{x: {fmt(width)}, y: {fmt(height)}}}
    newSize: {{x: {fmt(width)}, y: {fmt(height)}}}
    adaptiveTilingThreshold: 0.5
    drawMode: 0
    adaptiveTiling: 0
  m_AutoTiling: 0
  m_Size: {{x: {fmt(width)}, y: {fmt(height)}}}
  m_EdgeRadius: 0
"""

    return [
        UnityBlock(1, game_object_file_id, game_object_text),
        UnityBlock(4, transform_file_id, transform_text),
        UnityBlock(61, collider_file_id, collider_text),
        UnityBlock(114, marker_file_id, marker_text),
    ]


def replace_or_insert_blocks(
    blocks: list[UnityBlock],
    blocks_by_id: dict[int, int],
    new_blocks: list[UnityBlock],
    insert_after_file_id: int,
) -> None:
    insert_after_index = blocks_by_id[insert_after_file_id]
    blocks_to_insert: list[UnityBlock] = []

    for block in new_blocks:
        existing_index = blocks_by_id.get(block.file_id)

        if existing_index is None:
            blocks_to_insert.append(block)
            continue

        blocks[existing_index] = block

    if blocks_to_insert:
        blocks[insert_after_index + 1 : insert_after_index + 1] = blocks_to_insert

    blocks_by_id.clear()
    blocks_by_id.update({block.file_id: index for index, block in enumerate(blocks)})


def add_child_to_transform(parent_block: UnityBlock, child_transform_file_id: int) -> None:
    existing_children = {
        int(match)
        for match in re.findall(r"^  - \{fileID: (-?\d+)\}$", parent_block.text, re.MULTILINE)
    }

    if child_transform_file_id in existing_children:
        return

    if "  m_Children: []\n" in parent_block.text:
        parent_block.text = parent_block.text.replace(
            "  m_Children: []\n",
            f"  m_Children:\n  - {{fileID: {child_transform_file_id}}}\n",
            1,
        )
        return

    lines = parent_block.text.splitlines(keepends=True)
    insert_at = None

    for index, line in enumerate(lines):
        if re.match(r"^  - \{fileID: -?\d+\}\n$", line):
            insert_at = index + 1

    if insert_at is None:
        raise RuntimeError(f"Could not locate child list insertion point on transform {parent_block.file_id}.")

    lines.insert(insert_at, f"  - {{fileID: {child_transform_file_id}}}\n")
    parent_block.text = "".join(lines)


def remove_children_from_transform(parent_block: UnityBlock, child_transform_file_ids: list[int]) -> None:
    for child_transform_file_id in child_transform_file_ids:
        parent_block.text = re.sub(
            rf"^  - \{{fileID: {child_transform_file_id}\}}\n",
            "",
            parent_block.text,
            flags=re.MULTILINE,
        )


def update_existing_head_marker(
    blocks: list[UnityBlock],
    blocks_by_id: dict[int, int],
    names_to_ids: dict[str, int],
) -> None:
    blocker_id = names_to_ids["PlayerBlocker_DiningHeadChairOverlay"]
    blocker_components = component_ids(require_block(blocks_by_id, blocks, blocker_id).text)
    marker_block = find_mono_behaviour(blocks_by_id, blocks, blocker_components, OBJECT_BLOCKER_GUID)

    if marker_block is None:
        raise RuntimeError("Head chair blocker is missing ObjectMovementBlocker2D.")

    marker_block.text = replace_line(
        marker_block.text,
        "footprintHeightFraction",
        fmt(BLOCKER_HEIGHT_FRACTION),
    )
    marker_block.text = replace_line(
        marker_block.text,
        "authoringNote",
        "recessed lower chair footprint; top/back remains pass-through for y-axis occlusion checks",
    )


def ensure_side_blockers(
    blocks: list[UnityBlock],
    blocks_by_id: dict[int, int],
    names_to_ids: dict[str, int],
    used_file_ids: set[int],
    sizes_by_guid: dict[str, tuple[float, float]],
) -> list[tuple[int, int]]:
    child_links: list[tuple[int, int]] = []
    insert_after_file_id = 3960000003

    for index, chair_name in enumerate(SIDE_CHAIRS):
        chair_file_id = names_to_ids[chair_name]
        chair_components = component_ids(require_block(blocks_by_id, blocks, chair_file_id).text)
        chair_transform = require_component(blocks_by_id, blocks, chair_components, 4, f"{chair_name} Transform")
        chair_sprite = require_component(blocks_by_id, blocks, chair_components, 212, f"{chair_name} SpriteRenderer")
        current_guid = sprite_guid(chair_sprite)

        if current_guid not in sizes_by_guid:
            raise RuntimeError(f"{chair_name} is not using a split chair sprite.")

        sprite_width, sprite_height = sizes_by_guid[current_guid]
        blocker_width = sprite_width * BLOCKER_WIDTH_FRACTION
        blocker_height = sprite_height * BLOCKER_HEIGHT_FRACTION
        blocker_name = f"PlayerBlocker_{chair_name}"
        existing_blocker_id = names_to_ids.get(blocker_name)

        if existing_blocker_id is None:
            base_file_id = next_free_file_id(used_file_ids, 3970000000 + index * 10)
            transform_file_id = next_free_file_id(used_file_ids, base_file_id + 1)
            marker_file_id = next_free_file_id(used_file_ids, base_file_id + 2)
            collider_file_id = next_free_file_id(used_file_ids, base_file_id + 3)
        else:
            blocker_components = component_ids(require_block(blocks_by_id, blocks, existing_blocker_id).text)
            transform_file_id = require_component(
                blocks_by_id,
                blocks,
                blocker_components,
                4,
                f"{blocker_name} Transform",
            ).file_id
            collider_file_id = require_component(
                blocks_by_id,
                blocks,
                blocker_components,
                61,
                f"{blocker_name} BoxCollider2D",
            ).file_id
            marker = find_mono_behaviour(blocks_by_id, blocks, blocker_components, OBJECT_BLOCKER_GUID)

            if marker is None:
                marker_file_id = next_free_file_id(used_file_ids, existing_blocker_id + 2)
            else:
                marker_file_id = marker.file_id

            base_file_id = existing_blocker_id

        new_blocks = make_blocker_blocks(
            base_file_id,
            transform_file_id,
            marker_file_id,
            collider_file_id,
            blocker_name,
            chair_name,
            chair_file_id,
            chair_transform.file_id,
            blocker_width,
            blocker_height,
        )
        replace_or_insert_blocks(blocks, blocks_by_id, new_blocks, insert_after_file_id)
        insert_after_file_id = new_blocks[-1].file_id
        names_to_ids[blocker_name] = base_file_id
        child_links.append((chair_transform.file_id, transform_file_id))

    return child_links


def main() -> None:
    preamble, blocks = split_scene(SCENE_PATH.read_text())
    blocks_by_id, names_to_ids = build_indexes(blocks)
    used_file_ids = {block.file_id for block in blocks}
    sizes_by_guid = load_manifest_sizes_by_guid()

    ensure_missing_right_front_chair(blocks, blocks_by_id, names_to_ids, used_file_ids, sizes_by_guid)
    blocks_by_id, names_to_ids = build_indexes(blocks)
    disable_legacy_stray_chairs(blocks, blocks_by_id, names_to_ids)
    blocks_by_id, names_to_ids = build_indexes(blocks)

    for chair_name in CHAIRS:
        if chair_name not in names_to_ids:
            raise RuntimeError(f"Missing chair GameObject {chair_name}")

        ensure_world_y_sort(blocks, blocks_by_id, used_file_ids, names_to_ids[chair_name])
        blocks_by_id, names_to_ids = build_indexes(blocks)

    update_chair_sprite_sizes(blocks, blocks_by_id, names_to_ids, sizes_by_guid)
    blocks_by_id, names_to_ids = build_indexes(blocks)

    update_existing_head_marker(blocks, blocks_by_id, names_to_ids)
    child_links = ensure_side_blockers(blocks, blocks_by_id, names_to_ids, used_file_ids, sizes_by_guid)

    blocks_by_id, names_to_ids = build_indexes(blocks)
    child_transform_ids = [child_file_id for _, child_file_id in child_links]
    dining_room_transform = require_block(blocks_by_id, blocks, DINING_ROOM_TRANSFORM_ID)
    remove_children_from_transform(dining_room_transform, child_transform_ids)

    for chair_transform_file_id, blocker_transform_file_id in child_links:
        chair_transform = require_block(blocks_by_id, blocks, chair_transform_file_id)
        add_child_to_transform(chair_transform, blocker_transform_file_id)

    SCENE_PATH.write_text(join_scene(preamble, blocks))
    print(
        "Applied dining chair y-axis occlusion wiring: "
        f"{len(CHAIRS)} y-sort components, {len(SIDE_CHAIRS)} recessed blockers."
    )


if __name__ == "__main__":
    main()
