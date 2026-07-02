#!/usr/bin/env python3
"""Wire the dining seated guest occlusion exception scene controller."""

from __future__ import annotations

import re
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SCENE_PATH = ROOT / "Assets/Scenes/Gameplay.unity"

CONTROLLER_GUID = "a7ef8aa1178e4cd38ac4fd89669b9e29"
DINING_ROOM_TRANSFORM_ID = 2300000016
CONTROLLER_GAME_OBJECT_ID = 3920000000
CONTROLLER_TRANSFORM_ID = 3920000001
CONTROLLER_COMPONENT_ID = 3920000002

TABLE_NAME = "DiningTableCutoutOverlay"
TABLE_SORT_ANCHOR_NAME = "DiningTableSortAnchor"
CONTROLLER_NAME = "DiningRoomSeatedGuestOcclusionController"

SEAT_TO_CHAIR = {
    "Ch2_DiningSeat_01": "DiningChair_Left04Back_Overlay",
    "Ch2_DiningSeat_02": "DiningChair_Left03MidBack_Overlay",
    "Ch2_DiningSeat_03": "DiningChair_Right01Back_Overlay",
    "Ch2_DiningSeat_04": "DiningChair_Left02MidFront_Overlay",
    "Ch2_DiningSeat_05": "DiningChair_Left01Front_Overlay",
    "Ch2_DiningSeat_06": "DiningChair_Right02MidBack_Overlay",
    "Ch2_DiningSeat_07": "DiningChair_Right04Front_Overlay",
    "Ch2_DiningSeat_08": "DiningChair_Right03MidFront_Overlay",
}


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


def require_block(blocks_by_id: dict[int, UnityBlock], file_id: int, description: str) -> UnityBlock:
    block = blocks_by_id.get(file_id)

    if block is None:
        raise RuntimeError(f"Missing {description} fileID {file_id}.")

    return block


def require_named_game_object(names_to_ids: dict[str, int], name: str) -> int:
    file_id = names_to_ids.get(name)

    if file_id is None:
        raise RuntimeError(f"Missing required scene GameObject {name}.")

    return file_id


def require_component(
    blocks_by_id: dict[int, UnityBlock],
    component_file_ids: list[int],
    unity_type: int,
    description: str,
) -> int:
    for component_file_id in component_file_ids:
        block = blocks_by_id.get(component_file_id)

        if block is not None and block.unity_type == unity_type:
            return component_file_id

    raise RuntimeError(f"Missing {description}.")


def controller_game_object_block() -> str:
    return f"""--- !u!1 &{CONTROLLER_GAME_OBJECT_ID}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {CONTROLLER_TRANSFORM_ID}}}
  - component: {{fileID: {CONTROLLER_COMPONENT_ID}}}
  m_Layer: 0
  m_Name: {CONTROLLER_NAME}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
"""


def controller_transform_block() -> str:
    return f"""--- !u!4 &{CONTROLLER_TRANSFORM_ID}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {CONTROLLER_GAME_OBJECT_ID}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {DINING_ROOM_TRANSFORM_ID}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
"""


def controller_component_block(
    table_go_id: int,
    table_renderer_id: int,
    table_sort_anchor_transform_id: int,
    bindings: list[tuple[int, int, int]],
) -> str:
    binding_lines = []

    for seat_anchor_id, chair_go_id, chair_renderer_id in bindings:
        binding_lines.append(
            f"  - seatAnchor: {{fileID: {seat_anchor_id}}}\n"
            f"    assignedChair: {{fileID: {chair_go_id}}}\n"
            f"    assignedChairRenderer: {{fileID: {chair_renderer_id}}}"
        )

    return f"""--- !u!114 &{CONTROLLER_COMPONENT_ID}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {CONTROLLER_GAME_OBJECT_ID}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {CONTROLLER_GUID}, type: 3}}
  m_Name:
  m_EditorClassIdentifier: Assembly-CSharp::DiningRoomSeatedGuestOcclusionController
  diningRoomName: Dining Room
  butlerExclusionObjectName: Butler
  diningTable: {{fileID: {table_go_id}}}
  diningTableRenderer: {{fileID: {table_renderer_id}}}
  diningTableSortAnchor: {{fileID: {table_sort_anchor_transform_id}}}
  seatBindings:
{chr(10).join(binding_lines)}
"""


def add_child_to_dining_room(block: UnityBlock) -> None:
    if f"{{fileID: {CONTROLLER_TRANSFORM_ID}}}" in block.text:
        return

    marker = "  m_Father: {fileID:"

    if marker not in block.text:
        raise RuntimeError("Dining room transform does not have an m_Father line.")

    block.text = block.text.replace(
        marker,
        f"  - {{fileID: {CONTROLLER_TRANSFORM_ID}}}\n{marker}",
        1,
    )


def remove_existing_controller_blocks(blocks: list[UnityBlock]) -> list[UnityBlock]:
    return [
        block
        for block in blocks
        if block.file_id
        not in {
            CONTROLLER_GAME_OBJECT_ID,
            CONTROLLER_TRANSFORM_ID,
            CONTROLLER_COMPONENT_ID,
        }
    ]


def main() -> None:
    scene_text = SCENE_PATH.read_text()
    preamble, blocks = split_scene(scene_text)
    blocks = remove_existing_controller_blocks(blocks)
    blocks_by_id = {block.file_id: block for block in blocks}
    names_to_ids = {
        name: block.file_id
        for block in blocks
        if block.unity_type == 1
        if (name := get_name(block.text))
    }

    table_go_id = require_named_game_object(names_to_ids, TABLE_NAME)
    table_components = component_ids(require_block(blocks_by_id, table_go_id, TABLE_NAME).text)
    table_renderer_id = require_component(blocks_by_id, table_components, 212, f"{TABLE_NAME} SpriteRenderer")

    table_sort_anchor_go_id = require_named_game_object(names_to_ids, TABLE_SORT_ANCHOR_NAME)
    table_sort_anchor_components = component_ids(require_block(blocks_by_id, table_sort_anchor_go_id, TABLE_SORT_ANCHOR_NAME).text)
    table_sort_anchor_transform_id = require_component(
        blocks_by_id,
        table_sort_anchor_components,
        4,
        f"{TABLE_SORT_ANCHOR_NAME} Transform",
    )

    bindings: list[tuple[int, int, int]] = []

    for seat_name, chair_name in SEAT_TO_CHAIR.items():
        seat_go_id = require_named_game_object(names_to_ids, seat_name)
        seat_components = component_ids(require_block(blocks_by_id, seat_go_id, seat_name).text)
        seat_anchor_id = require_component(blocks_by_id, seat_components, 114, f"{seat_name} RoomAnchor")

        chair_go_id = require_named_game_object(names_to_ids, chair_name)
        chair_components = component_ids(require_block(blocks_by_id, chair_go_id, chair_name).text)
        chair_renderer_id = require_component(blocks_by_id, chair_components, 212, f"{chair_name} SpriteRenderer")
        bindings.append((seat_anchor_id, chair_go_id, chair_renderer_id))

    dining_room_transform = require_block(blocks_by_id, DINING_ROOM_TRANSFORM_ID, "Room_Dining_Room Transform")
    add_child_to_dining_room(dining_room_transform)

    new_blocks = [
        UnityBlock(1, CONTROLLER_GAME_OBJECT_ID, controller_game_object_block()),
        UnityBlock(4, CONTROLLER_TRANSFORM_ID, controller_transform_block()),
        UnityBlock(
            114,
            CONTROLLER_COMPONENT_ID,
            controller_component_block(
                table_go_id,
                table_renderer_id,
                table_sort_anchor_transform_id,
                bindings,
            ),
        ),
    ]
    scene_roots_index = next(
        (index for index, block in enumerate(blocks) if block.unity_type == 1660057539),
        len(blocks),
    )
    blocks[scene_roots_index:scene_roots_index] = new_blocks

    SCENE_PATH.write_text(join_scene(preamble, blocks))
    print(f"Wired {CONTROLLER_NAME} with {len(bindings)} dining seat bindings.")


if __name__ == "__main__":
    main()
