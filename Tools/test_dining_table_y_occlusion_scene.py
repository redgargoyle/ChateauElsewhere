#!/usr/bin/env python3
"""Regression checks for dining table y-axis occlusion scene wiring."""

from __future__ import annotations

import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SCENE_PATH = ROOT / "Assets/Scenes/Gameplay.unity"
TABLE_META_PATH = ROOT / "Assets/Art/Objects/DiningRoomLayered/dining_table_main_no_chairs.png.meta"

WORLD_Y_SORT_GUID = "75f090bb68ab450d9703d9581c5c543a"
OBJECT_BLOCKER_GUID = "b95469e02af64fee8b29689edb9b583a"

TABLE_NAME = "DiningTableCutoutOverlay"
TABLE_SORT_ANCHOR_NAME = "DiningTableSortAnchor"
TABLE_BLOCKER_NAME = "PlayerBlocker_DiningTableCutoutOverlay"
TABLE_SORT_ANCHOR_ROOM_Y = -356.0
TABLE_SORT_ANCHOR_ROOM_Y_TOLERANCE = 2.0


def fail(message: str) -> None:
    print(f"FAIL: {message}", file=sys.stderr)
    raise SystemExit(1)


def split_blocks(scene_text: str) -> dict[int, tuple[int, str]]:
    marker = re.compile(r"(?m)^--- !u!(\d+) &(-?\d+).*$")
    matches = list(marker.finditer(scene_text))
    blocks: dict[int, tuple[int, str]] = {}

    for index, match in enumerate(matches):
        start = match.start()
        end = matches[index + 1].start() if index + 1 < len(matches) else len(scene_text)
        blocks[int(match.group(2))] = (int(match.group(1)), scene_text[start:end])

    return blocks


def get_name(block: str) -> str | None:
    match = re.search(r"^  m_Name: '?([^'\n]*)'?$", block, re.MULTILINE)
    return match.group(1).rstrip() if match else None


def component_ids(game_object_block: str) -> list[int]:
    return [int(match) for match in re.findall(r"- component: \{fileID: (-?\d+)\}", game_object_block)]


def vector_field(block: str, field_name: str) -> tuple[float, float, float]:
    pattern = rf"^  {re.escape(field_name)}: \{{x: ([^,]+), y: ([^,]+), z: ([^}}]+)\}}$"
    match = re.search(pattern, block, re.MULTILINE)

    if not match:
        fail(f"Missing {field_name} on block:\n{block[:240]}")

    return tuple(float(value) for value in match.groups())


def require_component(
    blocks: dict[int, tuple[int, str]],
    component_file_ids: list[int],
    unity_type: int,
    description: str,
) -> tuple[int, str]:
    for component_file_id in component_file_ids:
        block_info = blocks.get(component_file_id)

        if block_info is not None and block_info[0] == unity_type:
            return component_file_id, block_info[1]

    fail(f"Missing {description}")


def require_mono_behaviour(
    blocks: dict[int, tuple[int, str]],
    component_file_ids: list[int],
    script_guid: str,
    description: str,
) -> tuple[int, str]:
    for component_file_id in component_file_ids:
        block_info = blocks.get(component_file_id)

        if block_info is None or block_info[0] != 114:
            continue

        if f"guid: {script_guid}" in block_info[1]:
            return component_file_id, block_info[1]

    fail(f"Missing {description}")


def assert_contains(block: str, needle: str, description: str) -> None:
    if needle not in block:
        fail(f"{description} does not contain {needle!r}")


def main() -> None:
    scene_text = SCENE_PATH.read_text()
    blocks = split_blocks(scene_text)
    names_to_file_ids = {
        name: file_id
        for file_id, (unity_type, block) in blocks.items()
        if unity_type == 1
        if (name := get_name(block))
    }

    table_file_id = names_to_file_ids.get(TABLE_NAME)

    if table_file_id is None:
        fail(f"Missing table GameObject {TABLE_NAME}")

    table_components = component_ids(blocks[table_file_id][1])
    table_transform_file_id, table_transform = require_component(
        blocks,
        table_components,
        4,
        f"{TABLE_NAME} Transform",
    )
    _, table_sprite = require_component(blocks, table_components, 212, f"{TABLE_NAME} SpriteRenderer")
    _, table_y_sort = require_mono_behaviour(
        blocks,
        table_components,
        WORLD_Y_SORT_GUID,
        f"{TABLE_NAME} WorldYSortSpriteRenderer",
    )

    sort_anchor_file_id = names_to_file_ids.get(TABLE_SORT_ANCHOR_NAME)

    if sort_anchor_file_id is None:
        fail(f"Missing table sort anchor GameObject {TABLE_SORT_ANCHOR_NAME}")

    sort_anchor_components = component_ids(blocks[sort_anchor_file_id][1])
    sort_anchor_transform_file_id, sort_anchor_transform = require_component(
        blocks,
        sort_anchor_components,
        4,
        f"{TABLE_SORT_ANCHOR_NAME} Transform",
    )

    assert_contains(table_sprite, "m_SpriteSortPoint: 1", f"{TABLE_NAME} SpriteRenderer")
    assert_contains(
        table_transform,
        f"- {{fileID: {sort_anchor_transform_file_id}}}",
        f"{TABLE_NAME} Transform",
    )
    assert_contains(
        sort_anchor_transform,
        f"m_Father: {{fileID: {table_transform_file_id}}}",
        f"{TABLE_SORT_ANCHOR_NAME} Transform",
    )
    assert_contains(table_y_sort, "sortingLayerName: People", f"{TABLE_NAME} WorldYSortSpriteRenderer")
    assert_contains(table_y_sort, "sortingOrderBase: 1000", f"{TABLE_NAME} WorldYSortSpriteRenderer")
    assert_contains(table_y_sort, "sortingOrderPerYUnit: 100", f"{TABLE_NAME} WorldYSortSpriteRenderer")
    assert_contains(table_y_sort, "sortingOrderOffset: 0", f"{TABLE_NAME} WorldYSortSpriteRenderer")
    assert_contains(table_y_sort, "includeChildren: 1", f"{TABLE_NAME} WorldYSortSpriteRenderer")
    assert_contains(table_y_sort, "forcePivotSortPoint: 1", f"{TABLE_NAME} WorldYSortSpriteRenderer")
    assert_contains(
        table_y_sort,
        "sortSolidObstacleFromPhysicalBottom: 0",
        f"{TABLE_NAME} WorldYSortSpriteRenderer",
    )
    assert_contains(
        table_y_sort,
        "forceBehindPlayerInsidePhysicalBounds: 0",
        f"{TABLE_NAME} WorldYSortSpriteRenderer",
    )
    assert_contains(
        table_y_sort,
        f"yReference: {{fileID: {sort_anchor_transform_file_id}}}",
        f"{TABLE_NAME} WorldYSortSpriteRenderer",
    )

    anchor_x, anchor_y, anchor_z = vector_field(sort_anchor_transform, "m_LocalPosition")
    _, table_room_y, _ = vector_field(table_transform, "m_LocalPosition")
    _, table_scale_y, _ = vector_field(table_transform, "m_LocalScale")
    anchor_room_y = table_room_y + anchor_y * table_scale_y

    if abs(anchor_x) > 0.001 or abs(anchor_z) > 0.001:
        fail(f"{TABLE_SORT_ANCHOR_NAME} should stay centered under the table, found x/z {anchor_x}/{anchor_z}")

    if abs(anchor_room_y - TABLE_SORT_ANCHOR_ROOM_Y) > TABLE_SORT_ANCHOR_ROOM_Y_TOLERANCE:
        fail(
            f"{TABLE_SORT_ANCHOR_NAME} should sit on the tablecloth front occlusion line near "
            f"dining-room y {TABLE_SORT_ANCHOR_ROOM_Y}, found {anchor_room_y}"
        )

    table_blocker_file_id = names_to_file_ids.get(TABLE_BLOCKER_NAME)

    if table_blocker_file_id is None:
        fail(f"Missing table movement blocker {TABLE_BLOCKER_NAME}")

    blocker_components = component_ids(blocks[table_blocker_file_id][1])
    _, blocker_marker = require_mono_behaviour(
        blocks,
        blocker_components,
        OBJECT_BLOCKER_GUID,
        f"{TABLE_BLOCKER_NAME} ObjectMovementBlocker2D",
    )
    assert_contains(blocker_marker, f"sourceObject: {{fileID: {table_file_id}}}", f"{TABLE_BLOCKER_NAME} marker")
    assert_contains(blocker_marker, f"sourceObjectName: {TABLE_NAME}", f"{TABLE_BLOCKER_NAME} marker")
    assert_contains(blocker_marker, "sourceRoomName: Dining Room", f"{TABLE_BLOCKER_NAME} marker")
    assert_contains(blocker_marker, "category: Table", f"{TABLE_BLOCKER_NAME} marker")
    assert_contains(blocker_marker, "sortSourceRenderers: 0", f"{TABLE_BLOCKER_NAME} marker")

    if not TABLE_META_PATH.exists():
        fail(f"Missing dining table sprite meta {TABLE_META_PATH}")

    print("Dining table y-occlusion scene wiring verified.")


if __name__ == "__main__":
    main()
