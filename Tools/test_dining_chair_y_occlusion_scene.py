#!/usr/bin/env python3
"""Regression checks for dining room chair y-axis occlusion scene wiring."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SCENE_PATH = ROOT / "Assets/Scenes/Gameplay.unity"
MANIFEST_PATH = ROOT / "Assets/Art/Objects/DiningTableChairSplits/_dining_chair_splits_manifest.json"

WORLD_Y_SORT_GUID = "75f090bb68ab450d9703d9581c5c543a"
OBJECT_BLOCKER_GUID = "b95469e02af64fee8b29689edb9b583a"
RIGHT_FRONT_TRIMMED_GUID = "24a9b0f498b34b16ac395abdbaa2d168"
RIGHT_FRONT_SCENE_OVERLAY_GUID = "960f64daeb0a47a68dc0b6e201a080db"

BLOCKER_HEIGHT_FRACTION = 0.22

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
        unity_type = int(match.group(1))
        file_id = int(match.group(2))
        blocks[file_id] = (unity_type, scene_text[start:end])

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


def two_value_field(block: str, field_name: str) -> tuple[float, float]:
    pattern = rf"^  {re.escape(field_name)}: \{{x: ([^,]+), y: ([^}}]+)\}}$"
    match = re.search(pattern, block, re.MULTILINE)

    if not match:
        fail(f"Missing {field_name} on block:\n{block[:240]}")

    return float(match.group(1)), float(match.group(2))


def require_component(
    blocks: dict[int, tuple[int, str]],
    component_file_ids: list[int],
    unity_type: int,
    description: str,
) -> tuple[int, str]:
    for component_file_id in component_file_ids:
        block_info = blocks.get(component_file_id)

        if block_info is None:
            continue

        if block_info[0] == unity_type:
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


def manifest_sizes_by_guid() -> dict[str, tuple[float, float]]:
    manifest = json.loads(MANIFEST_PATH.read_text())
    sizes: dict[str, tuple[float, float]] = {}

    for chair in manifest["chairs"]:
        meta_path = ROOT / f"{chair['file']}.meta"
        guid_match = re.search(r"^guid: ([0-9a-f]+)$", meta_path.read_text(), re.MULTILINE)

        if not guid_match:
            fail(f"Missing guid in {meta_path}")

        width_pixels, height_pixels = chair["size"]
        sizes[guid_match.group(1)] = (width_pixels / 100.0, height_pixels / 100.0)

    return sizes


def main() -> None:
    scene_text = SCENE_PATH.read_text()
    blocks = split_blocks(scene_text)
    sizes_by_guid = manifest_sizes_by_guid()

    names_to_file_ids = {
        name: file_id
        for file_id, (unity_type, block) in blocks.items()
        if unity_type == 1
        if (name := get_name(block))
    }

    for chair_name in CHAIRS:
        chair_file_id = names_to_file_ids.get(chair_name)

        if chair_file_id is None:
            fail(f"Missing chair GameObject {chair_name}")

        chair_block = blocks[chair_file_id][1]
        components = component_ids(chair_block)
        transform_file_id, transform_block = require_component(blocks, components, 4, f"{chair_name} Transform")
        _, sprite_block = require_component(blocks, components, 212, f"{chair_name} SpriteRenderer")
        _, y_sort_block = require_mono_behaviour(
            blocks,
            components,
            WORLD_Y_SORT_GUID,
            f"{chair_name} WorldYSortSpriteRenderer",
        )

        assert_contains(sprite_block, "m_SpriteSortPoint: 1", f"{chair_name} SpriteRenderer")
        assert_contains(y_sort_block, "sortingLayerName: People", f"{chair_name} WorldYSortSpriteRenderer")
        assert_contains(y_sort_block, "sortingOrderBase: 1000", f"{chair_name} WorldYSortSpriteRenderer")
        assert_contains(y_sort_block, "sortingOrderPerYUnit: 100", f"{chair_name} WorldYSortSpriteRenderer")
        assert_contains(y_sort_block, "includeChildren: 1", f"{chair_name} WorldYSortSpriteRenderer")
        assert_contains(y_sort_block, "forcePivotSortPoint: 1", f"{chair_name} WorldYSortSpriteRenderer")
        assert_contains(
            y_sort_block,
            "sortSolidObstacleFromPhysicalBottom: 0",
            f"{chair_name} WorldYSortSpriteRenderer",
        )
        assert_contains(
            y_sort_block,
            f"yReference: {{fileID: {transform_file_id}}}",
            f"{chair_name} WorldYSortSpriteRenderer",
        )

        _, _, z_position = vector_field(transform_block, "m_LocalPosition")

        if z_position >= 0:
            fail(f"{chair_name} should remain in the dining overlay z-depth, found z {z_position}")

    right_front_id = names_to_file_ids["DiningChair_Right04Front_Overlay"]
    right_front_components = component_ids(blocks[right_front_id][1])
    _, right_front_sprite = require_component(blocks, right_front_components, 212, "right front chair SpriteRenderer")
    assert_contains(right_front_sprite, f"guid: {RIGHT_FRONT_TRIMMED_GUID}", "right front chair SpriteRenderer")

    if RIGHT_FRONT_SCENE_OVERLAY_GUID in right_front_sprite:
        fail("right front chair still uses the full-canvas scene overlay instead of the bottom-pivot cutout")

    for chair_name in SIDE_CHAIRS:
        chair_file_id = names_to_file_ids[chair_name]
        chair_components = component_ids(blocks[chair_file_id][1])
        chair_transform_file_id, chair_transform = require_component(blocks, chair_components, 4, f"{chair_name} Transform")
        _, chair_sprite = require_component(blocks, chair_components, 212, f"{chair_name} SpriteRenderer")
        sprite_guid_match = re.search(r"m_Sprite: \{fileID: [^,]+, guid: ([0-9a-f]+), type: 3\}", chair_sprite)

        if not sprite_guid_match:
            fail(f"{chair_name} SpriteRenderer has no sprite guid")

        sprite_guid = sprite_guid_match.group(1)
        visual_size = sizes_by_guid.get(sprite_guid)

        if visual_size is None:
            fail(f"{chair_name} does not use one of the split bottom-pivot chair sprites")

        blocker_name = f"PlayerBlocker_{chair_name}"
        blocker_file_id = names_to_file_ids.get(blocker_name)

        if blocker_file_id is None:
            fail(f"Missing recessed movement blocker {blocker_name}")

        blocker_block = blocks[blocker_file_id][1]
        blocker_components = component_ids(blocker_block)
        blocker_transform_file_id, blocker_transform = require_component(
            blocks,
            blocker_components,
            4,
            f"{blocker_name} Transform",
        )
        _, blocker_box = require_component(blocks, blocker_components, 61, f"{blocker_name} BoxCollider2D")
        _, blocker_marker = require_mono_behaviour(
            blocks,
            blocker_components,
            OBJECT_BLOCKER_GUID,
            f"{blocker_name} ObjectMovementBlocker2D",
        )

        assert_contains(
            blocker_transform,
            f"m_Father: {{fileID: {chair_transform_file_id}}}",
            f"{blocker_name} Transform",
        )
        assert_contains(chair_transform, f"- {{fileID: {blocker_transform_file_id}}}", f"{chair_name} Transform")
        assert_contains(blocker_box, "m_IsTrigger: 1", f"{blocker_name} BoxCollider2D")
        assert_contains(blocker_marker, f"sourceObject: {{fileID: {chair_file_id}}}", f"{blocker_name} marker")
        assert_contains(blocker_marker, f"sourceObjectName: {chair_name}", f"{blocker_name} marker")
        assert_contains(blocker_marker, "sourceRoomName: Dining Room", f"{blocker_name} marker")
        assert_contains(blocker_marker, "category: Chair", f"{blocker_name} marker")
        assert_contains(blocker_marker, "generatedByCollisionBoxTool: 0", f"{blocker_name} marker")
        assert_contains(blocker_marker, "sortSourceRenderers: 0", f"{blocker_name} marker")
        assert_contains(blocker_marker, "authoringNote: recessed lower chair footprint", f"{blocker_name} marker")

        blocker_width, blocker_height = two_value_field(blocker_box, "m_Size")
        blocker_offset_x, blocker_offset_y = two_value_field(blocker_box, "m_Offset")
        blocker_local_x, blocker_local_y, blocker_local_z = vector_field(blocker_transform, "m_LocalPosition")
        expected_visual_width = visual_size[0]
        expected_visual_height = visual_size[1]

        if abs(blocker_local_x) > 0.001 or abs(blocker_local_y) > 0.001 or abs(blocker_local_z) > 0.001:
            fail(f"{blocker_name} should stay at the chair local origin so it follows chair edits")

        if blocker_height >= expected_visual_height * 0.35:
            fail(
                f"{blocker_name} covers too much chair height: "
                f"{blocker_height:.2f} vs visual {expected_visual_height:.2f}"
            )

        if blocker_width >= expected_visual_width * 0.8:
            fail(
                f"{blocker_name} covers too much chair width: "
                f"{blocker_width:.2f} vs visual {expected_visual_width:.2f}"
            )

        if blocker_offset_y <= 0:
            fail(f"{blocker_name} should sit above the bottom pivot with a positive y offset")

        if abs(blocker_offset_x) > 0.001:
            fail(f"{blocker_name} should stay centered on the chair pivot")

        expected_height = expected_visual_height * BLOCKER_HEIGHT_FRACTION

        if abs(blocker_height - expected_height) > 0.02:
            fail(
                f"{blocker_name} has unexpected recessed blocker height: "
                f"{blocker_height:.2f} vs expected {expected_height:.2f}"
            )

    head_blocker_id = names_to_file_ids.get("PlayerBlocker_DiningHeadChairOverlay")

    if head_blocker_id is None:
        fail("Missing existing head chair blocker")

    head_components = component_ids(blocks[head_blocker_id][1])
    _, head_marker = require_mono_behaviour(
        blocks,
        head_components,
        OBJECT_BLOCKER_GUID,
        "head chair ObjectMovementBlocker2D",
    )
    assert_contains(head_marker, "category: Chair", "head chair marker")
    assert_contains(head_marker, "sortSourceRenderers: 0", "head chair marker")
    assert_contains(head_marker, "recessed", "head chair marker")

    print("Dining chair y-occlusion scene wiring verified.")


if __name__ == "__main__":
    main()
