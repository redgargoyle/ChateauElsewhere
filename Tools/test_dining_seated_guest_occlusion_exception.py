#!/usr/bin/env python3
"""Regression checks for the dining seated guest occlusion exception."""

from __future__ import annotations

import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SCENE_PATH = ROOT / "Assets/Scenes/Gameplay.unity"
EXCEPTION_SCRIPT_PATH = ROOT / "Assets/Scripts/Characters/DiningRoomSeatedGuestOcclusionException.cs"
CONTROLLER_SCRIPT_PATH = ROOT / "Assets/Scripts/Characters/DiningRoomSeatedGuestOcclusionController.cs"

WORLD_Y_SORT_GUID = "75f090bb68ab450d9703d9581c5c543a"
OBJECT_BLOCKER_GUID = "b95469e02af64fee8b29689edb9b583a"
ROOM_PROJECTED_ENTITY_GUID = "361e3658088b41ab98d330ae6457640b"
CONTROLLER_GUID = "a7ef8aa1178e4cd38ac4fd89669b9e29"

DINING_ROOM = "Dining Room"
CONTROLLER_NAME = "DiningRoomSeatedGuestOcclusionController"
EXCEPTION_NAME = "DiningRoomSeatedGuestOcclusionException"
TABLE_NAME = "DiningTableCutoutOverlay"
TABLE_SORT_ANCHOR_NAME = "DiningTableSortAnchor"

DINING_SEATS = [f"Ch2_DiningSeat_{index:02d}" for index in range(1, 9)]
CANONICAL_CHAIRS = [
    "DiningChair_Left01Front_Overlay",
    "DiningChair_Left02MidFront_Overlay",
    "DiningChair_Left03MidBack_Overlay",
    "DiningChair_Left04Back_Overlay",
    "DiningHeadChairOverlay",
    "DiningChair_Right01Back_Overlay",
    "DiningChair_Right02MidBack_Overlay",
    "DiningChair_Right03MidFront_Overlay",
    "DiningChair_Right04Front_Overlay",
]


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


def assert_contains(text: str, needle: str, description: str) -> None:
    if needle not in text:
        fail(f"{description} does not contain {needle!r}")


def field_ref(block: str, field_name: str) -> int | None:
    match = re.search(rf"^  {re.escape(field_name)}: \{{fileID: (-?\d+)\}}$", block, re.MULTILINE)
    return int(match.group(1)) if match else None


def main() -> None:
    if not EXCEPTION_SCRIPT_PATH.exists():
        fail(f"Missing explicit dining seated exception script {EXCEPTION_SCRIPT_PATH}")

    if not CONTROLLER_SCRIPT_PATH.exists():
        fail(f"Missing dining seated occlusion controller script {CONTROLLER_SCRIPT_PATH}")

    exception_text = EXCEPTION_SCRIPT_PATH.read_text()
    controller_text = CONTROLLER_SCRIPT_PATH.read_text()
    assert_contains(exception_text, "class DiningRoomSeatedGuestOcclusionException", EXCEPTION_NAME)
    assert_contains(exception_text, "SortingGroup", EXCEPTION_NAME)
    assert_contains(exception_text, "ActorRoomState", EXCEPTION_NAME)
    assert_contains(exception_text, "IsSeated", EXCEPTION_NAME)
    assert_contains(exception_text, "CurrentRoomId", EXCEPTION_NAME)
    assert_contains(exception_text, "SetProjectedSortingSuppressed", EXCEPTION_NAME)
    assert_contains(exception_text, "RoomProjectedEntity", EXCEPTION_NAME)
    assert_contains(exception_text, "guestOrder = tableOrder - 1", EXCEPTION_NAME)
    assert_contains(exception_text, "guestOrder <= chairOrder", EXCEPTION_NAME)
    assert_contains(exception_text, "Dining seat occlusion order invalid", EXCEPTION_NAME)
    assert_contains(exception_text, "RestoreNormalSorting", EXCEPTION_NAME)
    assert_contains(controller_text, "Ch2_DiningSeat_", CONTROLLER_NAME)
    assert_contains(controller_text, "DiningRoomSeatedGuestOcclusionException", CONTROLLER_NAME)
    assert_contains(controller_text, "ActivateForGuest", CONTROLLER_NAME)
    assert_contains(controller_text, "DeactivateForGuest", CONTROLLER_NAME)

    scene_text = SCENE_PATH.read_text()
    blocks = split_blocks(scene_text)
    names_to_file_ids = {
        name: file_id
        for file_id, (unity_type, block) in blocks.items()
        if unity_type == 1
        if (name := get_name(block))
    }

    for seat_name in DINING_SEATS:
        if seat_name not in names_to_file_ids:
            fail(f"Missing dining seat anchor {seat_name}")

    for chair_name in CANONICAL_CHAIRS:
        if chair_name not in names_to_file_ids:
            fail(f"Missing canonical dining chair overlay {chair_name}")

    table_file_id = names_to_file_ids.get(TABLE_NAME)
    table_anchor_file_id = names_to_file_ids.get(TABLE_SORT_ANCHOR_NAME)

    if table_file_id is None:
        fail(f"Missing dining table {TABLE_NAME}")

    if table_anchor_file_id is None:
        fail(f"Missing dining table sort anchor {TABLE_SORT_ANCHOR_NAME}")

    table_components = component_ids(blocks[table_file_id][1])
    _, table_y_sort = require_mono_behaviour(
        blocks,
        table_components,
        WORLD_Y_SORT_GUID,
        f"{TABLE_NAME} WorldYSortSpriteRenderer",
    )
    assert_contains(table_y_sort, "sortingOrderOffset: 0", f"{TABLE_NAME} WorldYSortSpriteRenderer")
    assert_contains(table_y_sort, "sortSolidObstacleFromPhysicalBottom: 0", f"{TABLE_NAME} WorldYSortSpriteRenderer")
    assert_contains(table_y_sort, "forceBehindPlayerInsidePhysicalBounds: 0", f"{TABLE_NAME} WorldYSortSpriteRenderer")

    controller_file_id = names_to_file_ids.get(CONTROLLER_NAME)

    if controller_file_id is None:
        fail(f"Missing scene GameObject {CONTROLLER_NAME}")

    controller_components = component_ids(blocks[controller_file_id][1])
    _, controller_block = require_mono_behaviour(
        blocks,
        controller_components,
        CONTROLLER_GUID,
        f"{CONTROLLER_NAME} MonoBehaviour",
    )

    assert_contains(controller_block, f"diningRoomName: {DINING_ROOM}", CONTROLLER_NAME)
    assert_contains(controller_block, "butlerExclusionObjectName: Butler", CONTROLLER_NAME)
    assert_contains(controller_block, f"diningTable: {{fileID: {table_file_id}}}", CONTROLLER_NAME)
    assert_contains(controller_block, "diningTableSortAnchor:", CONTROLLER_NAME)

    binding_blocks = re.findall(
        r"  - seatAnchor: \{fileID: (-?\d+)\}\n"
        r"    assignedChair: \{fileID: (-?\d+)\}\n"
        r"    assignedChairRenderer: \{fileID: (-?\d+)\}",
        controller_block,
    )

    if len(binding_blocks) != len(DINING_SEATS):
        fail(f"{CONTROLLER_NAME} should serialize {len(DINING_SEATS)} dining seat bindings, found {len(binding_blocks)}")

    seen_seats: set[str] = set()

    for seat_anchor_file_id, chair_file_id, chair_renderer_file_id in binding_blocks:
        seat_anchor_block = blocks.get(int(seat_anchor_file_id))

        if seat_anchor_block is None:
            fail(f"Binding references missing seat anchor component {seat_anchor_file_id}")

        seat_go_ref = field_ref(seat_anchor_block[1], "m_GameObject")

        if seat_go_ref is None:
            fail(f"Binding seat anchor {seat_anchor_file_id} has no GameObject reference")

        seat_name = get_name(blocks[seat_go_ref][1])
        seen_seats.add(seat_name or "")

        chair_block = blocks.get(int(chair_file_id))

        if chair_block is None or chair_block[0] != 1:
            fail(f"{seat_name} references missing assigned chair GameObject {chair_file_id}")

        chair_name = get_name(chair_block[1])

        if chair_name not in CANONICAL_CHAIRS:
            fail(f"{seat_name} assigned chair {chair_name!r} is not a canonical dining chair overlay")

        chair_renderer_block = blocks.get(int(chair_renderer_file_id))

        if chair_renderer_block is None or chair_renderer_block[0] != 212:
            fail(f"{seat_name} assigned chair renderer {chair_renderer_file_id} is not a SpriteRenderer")

        if "m_SortingOrder" not in chair_renderer_block[1]:
            fail(f"{seat_name} assigned chair renderer has no serialized SpriteRenderer sorting order")

    missing_seats = sorted(set(DINING_SEATS) - seen_seats)

    if missing_seats:
        fail(f"{CONTROLLER_NAME} is missing bindings for {', '.join(missing_seats)}")

    if "ApplyDiningRoomGuestSorting" in (ROOT / "Assets/_Chateau/Scripts/Chapter/Chapter02/Chapter2GuestSearchController.cs").read_text():
        fail("Dining seating should use the explicit exception component, not ApplyDiningRoomGuestSorting")

    if re.search(r"DiningRoom.*sortingOffset: -?\d+", scene_text):
        fail("Dining seated guest occlusion must not use RoomProjectedEntity sortingOffset as the solution")

    dining_blockers = [
        block
        for _, (unity_type, block) in blocks.items()
        if unity_type == 114 and f"guid: {OBJECT_BLOCKER_GUID}" in block and "sourceRoomName: Dining Room" in block
    ]

    for blocker in dining_blockers:
        assert_contains(blocker, "sortSourceRenderers: 0", "Dining ObjectMovementBlocker2D")

    print("Dining seated guest occlusion exception wiring verified.")


if __name__ == "__main__":
    main()
