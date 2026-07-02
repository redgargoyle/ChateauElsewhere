#!/usr/bin/env python3
"""Wire the dining table overlay for anchored y-axis occlusion."""

from __future__ import annotations

import re
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SCENE_PATH = ROOT / "Assets/Scenes/Gameplay.unity"

TABLE_NAME = "DiningTableCutoutOverlay"
TABLE_SORT_ANCHOR_NAME = "DiningTableSortAnchor"
WORLD_Y_SORT_GUID = "75f090bb68ab450d9703d9581c5c543a"
TABLE_SORT_ANCHOR_ROOM_Y = -356.0


@dataclass
class UnityBlock:
    unity_type: int
    file_id: int
    text: str


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


def build_indexes(blocks: list[UnityBlock]) -> tuple[dict[int, int], dict[str, int]]:
    blocks_by_id = {block.file_id: index for index, block in enumerate(blocks)}
    names_to_ids: dict[str, int] = {}

    for block in blocks:
        if block.unity_type != 1:
            continue

        name = get_name(block.text)

        if name:
            names_to_ids[name] = block.file_id

    return blocks_by_id, names_to_ids


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


def add_child_to_transform(parent_block: UnityBlock, child_transform_file_id: int) -> None:
    if f"- {{fileID: {child_transform_file_id}}}" in parent_block.text:
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


def replace_line(block_text: str, field_name: str, replacement: str) -> str:
    pattern = rf"^  {re.escape(field_name)}: .*$"
    new_text, count = re.subn(pattern, f"  {field_name}: {replacement}", block_text, flags=re.MULTILINE)

    if count == 0:
        raise RuntimeError(f"Could not replace {field_name} in block:\n{block_text[:240]}")

    return new_text


def vector_field(block_text: str, field_name: str) -> tuple[float, float, float]:
    pattern = rf"^  {re.escape(field_name)}: \{{x: ([^,]+), y: ([^,]+), z: ([^}}]+)\}}$"
    match = re.search(pattern, block_text, re.MULTILINE)

    if not match:
        raise RuntimeError(f"Missing {field_name} in block:\n{block_text[:240]}")

    return tuple(float(value) for value in match.groups())


def format_float(value: float) -> str:
    return f"{value:.6f}".rstrip("0").rstrip(".")


def table_child_local_y_for_room_y(table_transform: UnityBlock) -> float:
    _, table_room_y, _ = vector_field(table_transform.text, "m_LocalPosition")
    _, table_scale_y, _ = vector_field(table_transform.text, "m_LocalScale")

    if abs(table_scale_y) < 0.0001:
        raise RuntimeError(f"{TABLE_NAME} has near-zero y scale; cannot place sort anchor.")

    return (TABLE_SORT_ANCHOR_ROOM_Y - table_room_y) / table_scale_y


def make_sort_anchor_game_object_block(game_object_file_id: int, transform_file_id: int) -> str:
    return f"""--- !u!1 &{game_object_file_id}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {transform_file_id}}}
  m_Layer: 0
  m_Name: {TABLE_SORT_ANCHOR_NAME}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
"""


def make_sort_anchor_transform_block(
    transform_file_id: int,
    game_object_file_id: int,
    parent_transform_file_id: int,
    local_y: float,
) -> str:
    local_y_text = format_float(local_y)

    return f"""--- !u!4 &{transform_file_id}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {game_object_file_id}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: {local_y_text}, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {parent_transform_file_id}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
"""


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


def ensure_table_sort_anchor(
    blocks: list[UnityBlock],
    blocks_by_id: dict[int, int],
    names_to_ids: dict[str, int],
    used_file_ids: set[int],
    table_transform: UnityBlock,
) -> UnityBlock:
    sort_anchor_file_id = names_to_ids.get(TABLE_SORT_ANCHOR_NAME)
    sort_anchor_local_y = table_child_local_y_for_room_y(table_transform)

    if sort_anchor_file_id is None:
        sort_anchor_file_id = next_free_file_id(used_file_ids, 3800000004)
        sort_anchor_transform_file_id = next_free_file_id(used_file_ids, 3800000005)
        new_blocks = [
            UnityBlock(
                1,
                sort_anchor_file_id,
                make_sort_anchor_game_object_block(sort_anchor_file_id, sort_anchor_transform_file_id),
            ),
            UnityBlock(
                4,
                sort_anchor_transform_file_id,
                make_sort_anchor_transform_block(
                    sort_anchor_transform_file_id,
                    sort_anchor_file_id,
                    table_transform.file_id,
                    sort_anchor_local_y,
                ),
            ),
        ]
        table_index = blocks_by_id[table_transform.file_id]
        blocks[table_index + 1 : table_index + 1] = new_blocks
        blocks_by_id.clear()
        blocks_by_id.update({block.file_id: index for index, block in enumerate(blocks)})
        add_child_to_transform(table_transform, sort_anchor_transform_file_id)
        return new_blocks[1]

    sort_anchor_block = require_block(blocks_by_id, blocks, sort_anchor_file_id)
    sort_anchor_components = component_ids(sort_anchor_block.text)
    sort_anchor_transform = require_component(
        blocks_by_id,
        blocks,
        sort_anchor_components,
        4,
        f"{TABLE_SORT_ANCHOR_NAME} Transform",
    )
    sort_anchor_transform.text = make_sort_anchor_transform_block(
        sort_anchor_transform.file_id,
        sort_anchor_file_id,
        table_transform.file_id,
        sort_anchor_local_y,
    )
    add_child_to_transform(table_transform, sort_anchor_transform.file_id)
    return sort_anchor_transform


def ensure_table_y_sort(blocks: list[UnityBlock]) -> None:
    blocks_by_id, names_to_ids = build_indexes(blocks)
    table_file_id = names_to_ids.get(TABLE_NAME)

    if table_file_id is None:
        raise RuntimeError(f"Missing GameObject {TABLE_NAME}")

    used_file_ids = {block.file_id for block in blocks}
    table_block = require_block(blocks_by_id, blocks, table_file_id)
    components = component_ids(table_block.text)
    table_transform = require_component(blocks_by_id, blocks, components, 4, f"{TABLE_NAME} Transform")
    table_sprite = require_component(blocks_by_id, blocks, components, 212, f"{TABLE_NAME} SpriteRenderer")
    table_y_sort = find_mono_behaviour(blocks_by_id, blocks, components, WORLD_Y_SORT_GUID)
    sort_anchor_transform = ensure_table_sort_anchor(
        blocks,
        blocks_by_id,
        names_to_ids,
        used_file_ids,
        table_transform,
    )

    table_sprite.text = replace_line(table_sprite.text, "m_SpriteSortPoint", "1")

    if table_y_sort is None:
        y_sort_file_id = next_free_file_id(used_file_ids, table_file_id + 3)
        add_component_reference(table_block, y_sort_file_id)
        sprite_index = blocks_by_id[table_sprite.file_id]
        blocks.insert(
            sprite_index + 1,
            UnityBlock(
                114,
                y_sort_file_id,
                make_world_y_sort_block(y_sort_file_id, table_file_id, sort_anchor_transform.file_id),
            ),
        )
        return

    table_y_sort.text = make_world_y_sort_block(table_y_sort.file_id, table_file_id, sort_anchor_transform.file_id)


def main() -> None:
    preamble, blocks = split_scene(SCENE_PATH.read_text())
    ensure_table_y_sort(blocks)
    SCENE_PATH.write_text(join_scene(preamble, blocks))
    print("Applied dining table y-axis occlusion wiring.")


if __name__ == "__main__":
    main()
