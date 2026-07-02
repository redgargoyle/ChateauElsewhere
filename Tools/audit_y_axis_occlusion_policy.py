#!/usr/bin/env python3
"""Audit serialized Unity YAML for the y-axis occlusion sorting policy.

This pass treats dining table/chair policy violations as hard errors because
this branch owns those objects. Older project-wide sorting systems are reported
as design-required findings so they can be converted deliberately instead of
being silently rewritten by this dining-room pass.
"""

from __future__ import annotations

import argparse
import re
import sys
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "Assets"

WORLD_Y_SORT_GUID = "75f090bb68ab450d9703d9581c5c543a"
OBJECT_BLOCKER_GUID = "b95469e02af64fee8b29689edb9b583a"
ROOM_PROJECTED_ENTITY_GUID = "361e3658088b41ab98d330ae6457640b"

APPROVED_EXCEPTION_NAMES = {
    "DrawingRoomSeatedGuestOcclusionException",
    "DiningRoomSeatedGuestOcclusionException",
}

DINING_TABLE_NAME = "DiningTableCutoutOverlay"
DINING_CHAIR_NAMES = {
    "DiningHeadChairOverlay",
    "DiningChair_Left01Front_Overlay",
    "DiningChair_Left02MidFront_Overlay",
    "DiningChair_Left03MidBack_Overlay",
    "DiningChair_Left04Back_Overlay",
    "DiningChair_Right01Back_Overlay",
    "DiningChair_Right02MidBack_Overlay",
    "DiningChair_Right03MidFront_Overlay",
    "DiningChair_Right04Front_Overlay",
    "DiningChair_Rightback04_Overlay",
}
DINING_SORTED_PROP_NAMES = {DINING_TABLE_NAME, *DINING_CHAIR_NAMES}


@dataclass(frozen=True)
class UnityBlock:
    unity_type: int
    file_id: int
    text: str
    line: int


@dataclass(frozen=True)
class Finding:
    severity: str
    path: Path
    line: int
    object_name: str
    message: str

    def format(self) -> str:
        rel_path = self.path.relative_to(ROOT)
        return f"{self.severity}: {rel_path}:{self.line}: {self.object_name}: {self.message}"


@dataclass
class ParsedYaml:
    path: Path
    blocks: dict[int, UnityBlock]
    names_by_go: dict[int, str]
    active_by_go: dict[int, bool]
    components_by_go: dict[int, list[int]]
    owner_by_component: dict[int, int]


def split_blocks(path: Path) -> dict[int, UnityBlock]:
    text = path.read_text()
    marker = re.compile(r"(?m)^--- !u!(\d+) &(-?\d+).*$")
    matches = list(marker.finditer(text))
    blocks: dict[int, UnityBlock] = {}

    for index, match in enumerate(matches):
        start = match.start()
        end = matches[index + 1].start() if index + 1 < len(matches) else len(text)
        file_id = int(match.group(2))
        blocks[file_id] = UnityBlock(
            unity_type=int(match.group(1)),
            file_id=file_id,
            text=text[start:end],
            line=text.count("\n", 0, start) + 1,
        )

    return blocks


def get_name(block: str) -> str | None:
    match = re.search(r"^  m_Name: '?([^'\n]*)'?$", block, re.MULTILINE)
    return match.group(1).rstrip() if match else None


def get_game_object_ref(block: str) -> int | None:
    match = re.search(r"^  m_GameObject: \{fileID: (-?\d+)\}$", block, re.MULTILINE)
    return int(match.group(1)) if match else None


def component_ids(game_object_block: str) -> list[int]:
    return [int(match) for match in re.findall(r"- component: \{fileID: (-?\d+)\}", game_object_block)]


def field_value(block: str, field_name: str) -> str | None:
    match = re.search(rf"^  {re.escape(field_name)}: ?(.*)$", block, re.MULTILINE)
    return match.group(1).strip() if match else None


def field_int(block: str, field_name: str) -> int | None:
    value = field_value(block, field_name)

    if value is None:
        return None

    try:
        return int(value)
    except ValueError:
        return None


def is_active_go(block: str) -> bool:
    value = field_int(block, "m_IsActive")
    return value != 0


def parse_yaml_file(path: Path) -> ParsedYaml:
    blocks = split_blocks(path)
    names_by_go: dict[int, str] = {}
    active_by_go: dict[int, bool] = {}
    components_by_go: dict[int, list[int]] = {}
    owner_by_component: dict[int, int] = {}

    for file_id, block in blocks.items():
        if block.unity_type != 1:
            continue

        name = get_name(block.text) or f"<GameObject {file_id}>"
        components = component_ids(block.text)
        names_by_go[file_id] = name
        active_by_go[file_id] = is_active_go(block.text)
        components_by_go[file_id] = components

        for component_file_id in components:
            owner_by_component[component_file_id] = file_id

    for file_id, block in blocks.items():
        if file_id in owner_by_component:
            continue

        owner = get_game_object_ref(block.text)

        if owner is not None:
            owner_by_component[file_id] = owner

    return ParsedYaml(path, blocks, names_by_go, active_by_go, components_by_go, owner_by_component)


def yaml_paths() -> list[Path]:
    paths = list((ASSETS / "Scenes").rglob("*.unity")) + list((ASSETS / "Prefabs").rglob("*.prefab"))
    return sorted(path for path in paths if path.is_file())


def is_allowed_exception(object_name: str) -> bool:
    return object_name in APPROVED_EXCEPTION_NAMES


def is_dining_sorted_prop(object_name: str) -> bool:
    return object_name in DINING_SORTED_PROP_NAMES


def is_dining_table_or_chair_overlay(object_name: str) -> bool:
    if object_name in DINING_SORTED_PROP_NAMES:
        return True

    if object_name.startswith("Disabled_"):
        return False

    return (
        object_name.startswith("Dining")
        and "Overlay" in object_name
        and ("Chair" in object_name or "Table" in object_name)
    )


def is_dining_blocker(object_name: str, block: str) -> bool:
    source_name = field_value(block, "sourceObjectName") or ""
    return object_name.startswith("PlayerBlocker_Dining") or source_name in DINING_SORTED_PROP_NAMES


def has_component(parsed: ParsedYaml, go_file_id: int, unity_type: int, script_guid: str | None = None) -> bool:
    for component_file_id in parsed.components_by_go.get(go_file_id, []):
        component = parsed.blocks.get(component_file_id)

        if component is None or component.unity_type != unity_type:
            continue

        if script_guid is None or f"guid: {script_guid}" in component.text:
            return True

    return False


def owner_name(parsed: ParsedYaml, component: UnityBlock) -> tuple[int | None, str, bool]:
    owner = parsed.owner_by_component.get(component.file_id) or get_game_object_ref(component.text)

    if owner is None:
        return None, f"<component {component.file_id}>", True

    return owner, parsed.names_by_go.get(owner, f"<GameObject {owner}>"), parsed.active_by_go.get(owner, True)


def add_finding(
    findings: list[Finding],
    severity: str,
    path: Path,
    line: int,
    object_name: str,
    message: str,
) -> None:
    findings.append(Finding(severity, path, line, object_name, message))


def audit_world_y_sort(parsed: ParsedYaml, findings: list[Finding]) -> None:
    for block in parsed.blocks.values():
        if block.unity_type != 114 or f"guid: {WORLD_Y_SORT_GUID}" not in block.text:
            continue

        _, name, active = owner_name(parsed, block)

        if not active:
            continue

        if is_allowed_exception(name):
            continue

        checks = {
            "sortingOrderOffset": "0",
            "forceBehindPlayerInsidePhysicalBounds": "0",
        }

        if is_dining_sorted_prop(name):
            checks.update(
                {
                    "sortingLayerName": "People",
                    "sortingOrderBase": "1000",
                    "sortingOrderPerYUnit": "100",
                    "includeChildren": "1",
                    "forcePivotSortPoint": "1",
                    "sortSolidObstacleFromPhysicalBottom": "0",
                }
            )

        for field_name, expected in checks.items():
            actual = field_value(block.text, field_name)

            if actual != expected:
                add_finding(
                    findings,
                    "ERROR",
                    parsed.path,
                    block.line,
                    name,
                    f"WorldYSortSpriteRenderer {field_name} is {actual!r}, expected {expected!r}",
                )


def audit_object_blockers(parsed: ParsedYaml, findings: list[Finding]) -> None:
    for block in parsed.blocks.values():
        if block.unity_type != 114 or f"guid: {OBJECT_BLOCKER_GUID}" not in block.text:
            continue

        _, name, active = owner_name(parsed, block)

        if not active:
            continue

        sort_source_renderers = field_int(block.text, "sortSourceRenderers")

        if sort_source_renderers == 1:
            severity = "ERROR" if is_dining_blocker(name, block.text) else "DESIGN_REQUIRED"
            add_finding(
                findings,
                severity,
                parsed.path,
                block.line,
                name,
                "ObjectMovementBlocker2D sortSourceRenderers is 1; movement blockers must not drive visual sorting",
            )

        source_offset = field_int(block.text, "sourceSortingOrderOffset")

        if source_offset not in (None, 0):
            severity = "ERROR" if is_dining_blocker(name, block.text) else "DESIGN_REQUIRED"
            add_finding(
                findings,
                severity,
                parsed.path,
                block.line,
                name,
                f"ObjectMovementBlocker2D sourceSortingOrderOffset is {source_offset}, expected 0",
            )


def audit_dining_sprite_ownership(parsed: ParsedYaml, findings: list[Finding]) -> None:
    for go_file_id, name in parsed.names_by_go.items():
        if not parsed.active_by_go.get(go_file_id, True):
            continue

        if not is_dining_table_or_chair_overlay(name):
            continue

        if not has_component(parsed, go_file_id, 212):
            continue

        has_y_sort = has_component(parsed, go_file_id, 114, WORLD_Y_SORT_GUID)

        if not has_y_sort:
            block = parsed.blocks[go_file_id]
            add_finding(
                findings,
                "ERROR",
                parsed.path,
                block.line,
                name,
                "active dining table/chair SpriteRenderer has no WorldYSortSpriteRenderer and would rely on fixed sorting",
            )


def audit_room_projected_entities(parsed: ParsedYaml, findings: list[Finding]) -> None:
    for block in parsed.blocks.values():
        if block.unity_type != 114 or f"guid: {ROOM_PROJECTED_ENTITY_GUID}" not in block.text:
            continue

        _, name, active = owner_name(parsed, block)

        if not active:
            continue

        sorting_offset = field_int(block.text, "sortingOffset")

        if sorting_offset in (None, 0):
            continue

        if is_allowed_exception(name):
            continue

        add_finding(
            findings,
            "DESIGN_REQUIRED",
            parsed.path,
            block.line,
            name,
            f"RoomProjectedEntity sortingOffset is {sorting_offset}; approve by explicit exception name or migrate to pure y sorting",
        )


def audit_generic_guest_prop_offsets(parsed: ParsedYaml, findings: list[Finding]) -> None:
    offset_field_pattern = re.compile(r"^  ([A-Za-z0-9_]*[Ss]orting[A-Za-z0-9_]*Offset): (-?\d+)$", re.MULTILINE)

    for block in parsed.blocks.values():
        if block.unity_type != 114:
            continue

        _, name, active = owner_name(parsed, block)

        if not active or is_allowed_exception(name):
            continue

        for match in offset_field_pattern.finditer(block.text):
            field_name = match.group(1)

            if field_name in {"sortingOrderOffset", "behindPlayerSortingOffset", "sourceSortingOrderOffset"}:
                continue

            if field_name == "sortingOffset" and f"guid: {ROOM_PROJECTED_ENTITY_GUID}" in block.text:
                continue

            value = int(match.group(2))

            if value == 0:
                continue

            if not any(token in name.lower() for token in ("guest", "chair", "table", "prop", "overlay")):
                continue

            add_finding(
                findings,
                "DESIGN_REQUIRED",
                parsed.path,
                block.line + block.text[: match.start()].count("\n"),
                name,
                f"{field_name} is {value}; guest/prop sorting offsets need explicit exception naming or y-only migration",
            )


def audit_file(path: Path) -> list[Finding]:
    parsed = parse_yaml_file(path)
    findings: list[Finding] = []
    audit_world_y_sort(parsed, findings)
    audit_object_blockers(parsed, findings)
    audit_dining_sprite_ownership(parsed, findings)
    audit_room_projected_entities(parsed, findings)
    audit_generic_guest_prop_offsets(parsed, findings)
    return findings


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--strict",
        action="store_true",
        help="Treat design-required project-wide findings as failures too.",
    )
    args = parser.parse_args()

    findings: list[Finding] = []

    for path in yaml_paths():
        findings.extend(audit_file(path))

    errors = [finding for finding in findings if finding.severity == "ERROR"]
    design_required = [finding for finding in findings if finding.severity == "DESIGN_REQUIRED"]

    for finding in findings:
        print(finding.format())

    print(
        "Y-axis occlusion policy audit complete: "
        f"{len(errors)} hard error(s), {len(design_required)} design-required finding(s)."
    )

    if errors or (args.strict and design_required):
        return 1

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
