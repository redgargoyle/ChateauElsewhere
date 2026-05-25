#!/usr/bin/env python3
"""Build ButlerClassic idle variant sprites and Unity animation assets.

This is intentionally plain and local: it starts from the existing four
directional ButlerClassic idle frames, draws planted action poses on top, and
rewrites the variant clips/controllers so Unity can test them immediately.
"""

from __future__ import annotations

import hashlib
import math
import re
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[2]
SOURCE_ROOT = ROOT / "Assets/Characters/ButlerClassic/idle/aligned"
VARIANT_ROOT = ROOT / "Assets/Characters/ButlerClassic/idle_variants"
ANIMATION_ROOT = ROOT / "Assets/Animation/ButlerClassic/IdleVariants"
BASE_CONTROLLER_PATH = ROOT / "Assets/Animation/ButlerClassic/ButlerClassic.controller"

FRAME_COUNT = 8
FRAME_RATE = 8
CANVAS_SIZE = (168, 299)
DIRECTIONS = ("down", "left", "right", "up")

VARIANTS = (
	("still_breathe", "StillBreathe"),
	("still_weight_shift", "StillWeightShift"),
	("action_pocket_watch", "PocketWatch"),
	("action_smoke", "Smoke"),
	("action_beard_scratch", "BeardScratch"),
)

BASE_IDLE_CLIPS = {
	"Down": "Assets/Animation/ButlerClassic/ButlerClassic_Idle_Down.anim",
	"Left": "Assets/Animation/ButlerClassic/ButlerClassic_Idle_Left.anim",
	"Right": "Assets/Animation/ButlerClassic/ButlerClassic_Idle_Right.anim",
	"Up": "Assets/Animation/ButlerClassic/ButlerClassic_Idle_Up.anim",
}


def main() -> None:
	ensure_folder_meta(VARIANT_ROOT, ROOT / "Assets/Characters/ButlerClassic/idle_variants.meta")
	ensure_folder_meta(ANIMATION_ROOT, ROOT / "Assets/Animation/ButlerClassic/IdleVariants.meta")

	for variant_id, _ in VARIANTS:
		ensure_folder_meta(VARIANT_ROOT / variant_id, VARIANT_ROOT / f"{variant_id}.meta")
		ensure_folder_meta(VARIANT_ROOT / variant_id / "aligned", VARIANT_ROOT / variant_id / "aligned.meta")

		for direction in DIRECTIONS:
			for frame in range(1, FRAME_COUNT + 1):
				source_frame = ((frame - 1) % 4) + 1
				source = load_source(direction, source_frame)
				output = render_variant(variant_id, direction, frame, source)
				out_path = variant_frame_path(variant_id, direction, frame)
				output.save(out_path)
				ensure_texture_meta(out_path)

	write_animation_assets()
	print(f"Built {len(VARIANTS)} ButlerClassic idle variants with {FRAME_COUNT} frames per direction.")


def load_source(direction: str, frame: int) -> Image.Image:
	path = SOURCE_ROOT / f"butler_classic_idle_{direction}_{frame:02}.png"
	if not path.exists():
		raise FileNotFoundError(path)

	return Image.open(path).convert("RGBA")


def render_variant(variant_id: str, direction: str, frame: int, source: Image.Image) -> Image.Image:
	if variant_id == "still_breathe":
		return anchored_scale(source, direction, frame, breathe=True, sway=False)

	if variant_id == "still_weight_shift":
		return anchored_scale(source, direction, frame, breathe=True, sway=True)

	if variant_id == "action_pocket_watch":
		base = action_body_base(direction, frame)
		return draw_pocket_watch(base, direction, frame)

	if variant_id == "action_smoke":
		base = anchored_scale(source, direction, frame, breathe=True, sway=False)
		return draw_smoking_pipe(base, direction, frame)

	if variant_id == "action_beard_scratch":
		base = action_body_base(direction, frame)
		return draw_lapel_adjust(base, direction, frame)

	raise ValueError(f"Unknown variant {variant_id}")


def anchored_scale(image: Image.Image, direction: str, frame: int, breathe: bool, sway: bool) -> Image.Image:
	phase = math.sin((frame - 1) / FRAME_COUNT * math.tau)
	side_phase = math.sin((frame - 1) / FRAME_COUNT * math.tau + math.pi * 0.5)
	sy = 1.0 + (0.010 * phase if breathe else 0.0)
	sx = 1.0 - (0.004 * phase if breathe else 0.0)
	dx = side_phase * (1.3 if sway else 0.35)
	if direction == "left":
		dx *= -0.6
	elif direction == "right":
		dx *= 0.6
	elif direction == "up":
		dx *= 0.35

	bbox = image.getbbox()
	if bbox is None:
		return image.copy()

	crop = image.crop(bbox)
	new_size = (
		max(1, int(round(crop.width * sx))),
		max(1, int(round(crop.height * sy))),
	)
	resized = crop.resize(new_size, Image.Resampling.LANCZOS)
	out = Image.new("RGBA", image.size, (0, 0, 0, 0))
	left = int(round(bbox[0] + (crop.width - new_size[0]) * 0.5 + dx))
	top = int(round(bbox[3] - new_size[1]))
	out.alpha_composite(resized, (left, top))
	return out


def draw_pocket_watch(image: Image.Image, direction: str, frame: int) -> Image.Image:
	progress = ping_pong(frame)
	overlay = Image.new("RGBA", image.size, (0, 0, 0, 0))
	draw = ImageDraw.Draw(overlay, "RGBA")
	hand = pocket_watch_hand_position(direction, progress)
	watch = (hand[0] + side_for(direction) * 1.5, hand[1] + 14)
	draw_chain(draw, pocket_watch_chain_start(direction), watch)
	draw_watch(draw, watch, radius=7)
	add_soft_shadow(overlay)
	return Image.alpha_composite(image, overlay)


def draw_smoking_pipe(image: Image.Image, direction: str, frame: int) -> Image.Image:
	overlay = Image.new("RGBA", image.size, (0, 0, 0, 0))
	draw = ImageDraw.Draw(overlay, "RGBA")
	mouth = smoke_mouth_position(direction)
	draw_cigarette(draw, mouth, direction)
	draw_smoke(draw, mouth, direction, frame)
	add_soft_shadow(overlay)
	return Image.alpha_composite(image, overlay)


def draw_lapel_adjust(image: Image.Image, direction: str, frame: int) -> Image.Image:
	overlay = Image.new("RGBA", image.size, (0, 0, 0, 0))
	draw = ImageDraw.Draw(overlay, "RGBA")
	draw_lapel_adjust_detail(draw, direction, frame)
	add_soft_shadow(overlay)
	return Image.alpha_composite(image, overlay)


def action_body_base(direction: str, frame: int) -> Image.Image:
	source = load_source(direction, ((frame - 1) % 4) + 1)
	base = anchored_scale(source, direction, frame, breathe=True, sway=False)
	walk = load_walk_action_source(direction).copy()
	mask = Image.new("L", CANVAS_SIZE, 0)
	draw = ImageDraw.Draw(mask)

	if direction == "down":
		draw.rounded_rectangle((39, 78, 132, 190), radius=10, fill=255)
	elif direction == "right":
		draw.rounded_rectangle((39, 77, 134, 192), radius=10, fill=255)
	elif direction == "left":
		draw.rounded_rectangle((34, 77, 129, 192), radius=10, fill=255)
	else:
		draw.rounded_rectangle((43, 79, 126, 194), radius=10, fill=255)

	mask = mask.filter(ImageFilter.GaussianBlur(0.6))
	base.alpha_composite(Image.composite(walk, Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0)), mask))
	return base


def load_walk_action_source(direction: str) -> Image.Image:
	file_name = {
		"down": "butler_classic_walk_02_r01_c02.png",
		"left": "butler_classic_walk_06_r02_c02.png",
		"right": "butler_classic_walk_10_r03_c02.png",
		"up": "butler_classic_walk_14_r04_c02.png",
	}[direction]
	path = ROOT / "Assets/Characters/ButlerClassic/ButlerClassic/walk/aligned" / file_name
	return Image.open(path).convert("RGBA")


def watch_pose(direction: str, progress: float) -> dict[str, tuple[float, float]]:
	points = {
		"down": {
			"shoulder": (108, 99),
			"elbow_low": (106, 130),
			"elbow_high": (101, 122),
			"hand_low": (93, 153),
			"hand_high": (91, 136),
			"chain": (84, 126),
		},
		"right": {
			"shoulder": (99, 101),
			"elbow_low": (107, 132),
			"elbow_high": (107, 122),
			"hand_low": (100, 154),
			"hand_high": (105, 137),
			"chain": (91, 126),
		},
		"left": {
			"shoulder": (69, 101),
			"elbow_low": (61, 132),
			"elbow_high": (61, 122),
			"hand_low": (68, 154),
			"hand_high": (63, 137),
			"chain": (77, 126),
		},
		"up": {
			"shoulder": (97, 111),
			"elbow_low": (100, 138),
			"elbow_high": (101, 128),
			"hand_low": (91, 155),
			"hand_high": (95, 139),
			"chain": (87, 132),
		},
	}[direction]
	hand = lerp_point(points["hand_low"], points["hand_high"], progress)
	elbow = lerp_point(points["elbow_low"], points["elbow_high"], progress)
	watch = (hand[0] + side_for(direction) * 1.5, hand[1] + 10)
	return {
		"shoulder": points["shoulder"],
		"elbow": elbow,
		"wrist": lerp_point(elbow, hand, 0.62),
		"hand": hand,
		"watch": watch,
		"chain": points["chain"],
	}


def pocket_watch_hand_position(direction: str, progress: float) -> tuple[float, float]:
	positions = {
		"down": ((109, 160), (103, 149)),
		"right": ((108, 160), (104, 151)),
		"left": ((60, 160), (64, 151)),
		"up": ((105, 162), (100, 153)),
	}[direction]
	return lerp_point(positions[0], positions[1], ease_in_out(progress))


def pocket_watch_chain_start(direction: str) -> tuple[float, float]:
	return {
		"down": (86, 121),
		"right": (90, 125),
		"left": (78, 125),
		"up": (86, 132),
	}[direction]


def smoke_mouth_position(direction: str) -> tuple[float, float]:
	return {
		"down": (95, 62),
		"right": (103, 61),
		"left": (65, 61),
		"up": (91, 57),
	}[direction]


def draw_cigarette(draw: ImageDraw.ImageDraw, mouth: tuple[float, float], direction: str) -> None:
	side = side_for(direction)
	end = (mouth[0] + side * 11, mouth[1] - 1)
	draw.line([mouth, end], fill=(235, 226, 206, 245), width=2)
	draw.line([(end[0] - side * 2, end[1]), end], fill=(210, 78, 31, 230), width=2)


def draw_lapel_adjust_detail(draw: ImageDraw.ImageDraw, direction: str, frame: int) -> None:
	if frame in (1, 8):
		return

	progress = ease_in_out(ping_pong(frame))
	hand = {
		"down": lerp_point((104, 145), (100, 131), progress),
		"right": lerp_point((105, 148), (103, 134), progress),
		"left": lerp_point((63, 148), (65, 134), progress),
		"up": lerp_point((99, 149), (97, 136), progress),
	}[direction]
	draw_hand(draw, hand, radius=(3, 4))
	draw.line((hand[0] - side_for(direction) * 3, hand[1] + 2, hand[0] + side_for(direction) * 7, hand[1] - 5), fill=(225, 216, 188, 115), width=1)


def smoke_pose(direction: str, progress: float) -> dict[str, tuple[float, float]]:
	points = {
		"down": {
			"shoulder": (109, 96),
			"elbow_low": (112, 115),
			"elbow_high": (106, 103),
			"hand_low": (100, 85),
			"hand_high": (98, 73),
			"mouth": (94, 62),
		},
		"right": {
			"shoulder": (99, 101),
			"elbow_low": (113, 116),
			"elbow_high": (111, 104),
			"hand_low": (106, 85),
			"hand_high": (105, 73),
			"mouth": (103, 61),
		},
		"left": {
			"shoulder": (69, 101),
			"elbow_low": (55, 116),
			"elbow_high": (57, 104),
			"hand_low": (62, 85),
			"hand_high": (63, 73),
			"mouth": (65, 61),
		},
		"up": {
			"shoulder": (99, 109),
			"elbow_low": (105, 122),
			"elbow_high": (104, 111),
			"hand_low": (98, 92),
			"hand_high": (97, 81),
			"mouth": (93, 58),
		},
	}[direction]
	return {
		"shoulder": points["shoulder"],
		"elbow": lerp_point(points["elbow_low"], points["elbow_high"], progress),
		"wrist": lerp_point(lerp_point(points["elbow_low"], points["elbow_high"], progress), lerp_point(points["hand_low"], points["hand_high"], progress), 0.72),
		"hand": lerp_point(points["hand_low"], points["hand_high"], progress),
		"mouth": points["mouth"],
	}


def scratch_pose(direction: str, progress: float) -> dict[str, tuple[float, float]]:
	points = {
		"down": {
			"shoulder": (109, 98),
			"elbow_low": (111, 123),
			"elbow_high": (106, 107),
			"hand_low": (99, 120),
			"hand_high": (96, 83),
		},
		"right": {
			"shoulder": (99, 101),
			"elbow_low": (113, 124),
			"elbow_high": (109, 108),
			"hand_low": (105, 121),
			"hand_high": (103, 83),
		},
		"left": {
			"shoulder": (69, 101),
			"elbow_low": (55, 124),
			"elbow_high": (59, 108),
			"hand_low": (63, 121),
			"hand_high": (65, 83),
		},
		"up": {
			"shoulder": (99, 109),
			"elbow_low": (104, 129),
			"elbow_high": (103, 116),
			"hand_low": (97, 124),
			"hand_high": (96, 91),
		},
	}[direction]
	return {
		"shoulder": points["shoulder"],
		"elbow": lerp_point(points["elbow_low"], points["elbow_high"], progress),
		"wrist": lerp_point(points["elbow_low"], points["hand_high"], 0.72),
		"hand": lerp_point(points["hand_low"], points["hand_high"], progress),
	}


def cover_resting_arm(draw: ImageDraw.ImageDraw, direction: str) -> None:
	if direction == "down":
		draw.polygon([(96, 94), (115, 110), (109, 170), (95, 173), (89, 128)], fill=(18, 18, 19, 235))
		draw.line([(100, 96), (94, 172)], fill=(67, 64, 58, 95), width=2)
	elif direction == "right":
		draw.polygon([(88, 96), (111, 111), (107, 174), (90, 177), (84, 129)], fill=(18, 18, 19, 235))
		draw.line([(92, 99), (88, 174)], fill=(68, 65, 59, 95), width=2)
	elif direction == "left":
		draw.polygon([(80, 96), (57, 111), (61, 174), (78, 177), (84, 129)], fill=(18, 18, 19, 235))
		draw.line([(76, 99), (80, 174)], fill=(68, 65, 59, 95), width=2)
	elif direction == "up":
		draw.polygon([(91, 108), (107, 122), (105, 174), (92, 178), (86, 132)], fill=(19, 19, 20, 220))


def draw_sleeve(draw: ImageDraw.ImageDraw, shoulder: tuple[float, float], elbow: tuple[float, float], wrist: tuple[float, float], width: int) -> None:
	draw.line([shoulder, elbow, wrist], fill=(3, 3, 4, 245), width=width + 5, joint="curve")
	draw.line([shoulder, elbow, wrist], fill=(23, 23, 25, 252), width=width, joint="curve")
	draw.line([shoulder, elbow, wrist], fill=(88, 85, 76, 145), width=max(2, width // 5), joint="curve")
	draw.ellipse((shoulder[0] - 8, shoulder[1] - 7, shoulder[0] + 8, shoulder[1] + 8), fill=(21, 21, 23, 245), outline=(5, 5, 5, 220), width=1)


def draw_cuff(draw: ImageDraw.ImageDraw, wrist: tuple[float, float], hand: tuple[float, float], width: int) -> None:
	cuff = lerp_point(wrist, hand, 0.82)
	normal = segment_normal(wrist, hand)
	start = (cuff[0] - normal[0] * width, cuff[1] - normal[1] * width)
	end = (cuff[0] + normal[0] * width, cuff[1] + normal[1] * width)
	draw.line([start, end], fill=(40, 37, 32, 210), width=7)
	draw.line([start, end], fill=(226, 222, 208, 245), width=4)


def draw_hand(draw: ImageDraw.ImageDraw, hand: tuple[float, float], radius: tuple[int, int]) -> None:
	rx, ry = radius
	draw.ellipse((hand[0] - rx, hand[1] - ry, hand[0] + rx, hand[1] + ry), fill=(169, 111, 76, 248), outline=(66, 40, 30, 220), width=1)
	draw.line((hand[0] - 2, hand[1] - 2, hand[0] + 3, hand[1] + 1), fill=(229, 190, 154, 150), width=1)


def segment_normal(a: tuple[float, float], b: tuple[float, float]) -> tuple[float, float]:
	dx = b[0] - a[0]
	dy = b[1] - a[1]
	length = math.sqrt(dx * dx + dy * dy)
	if length <= 0.001:
		return (0.0, 1.0)

	return (-dy / length, dx / length)


def draw_watch(draw: ImageDraw.ImageDraw, center: tuple[float, float], radius: int) -> None:
	x, y = center
	draw.ellipse((x - radius - 1, y - radius - 1, x + radius + 1, y + radius + 1), fill=(68, 43, 12, 230))
	draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=(216, 170, 48, 245), outline=(75, 48, 12, 245), width=2)
	draw.ellipse((x - radius + 3, y - radius + 3, x + radius - 3, y + radius - 3), outline=(255, 235, 150, 210), width=1)
	draw.line((x, y, x + 2, y - 4), fill=(80, 55, 20, 220), width=1)
	draw.line((x, y, x + 4, y + 1), fill=(80, 55, 20, 220), width=1)


def draw_chain(draw: ImageDraw.ImageDraw, start: tuple[float, float], watch: tuple[float, float]) -> None:
	mid = ((start[0] + watch[0]) * 0.5, min(start[1], watch[1]) - 9)
	draw.line([start, mid, watch], fill=(238, 202, 95, 210), width=2)
	draw.line([(start[0] + 1, start[1] + 1), (mid[0] + 1, mid[1] + 1), (watch[0] + 1, watch[1] + 1)], fill=(92, 64, 22, 170), width=1)


def draw_pipe(draw: ImageDraw.ImageDraw, hand: tuple[float, float], mouth: tuple[float, float], direction: str) -> None:
	side = side_for(direction)
	bowl = (hand[0] + side * 7, hand[1] + 4)
	draw.line([hand, mouth], fill=(70, 39, 22, 245), width=3)
	draw.line([hand, mouth], fill=(139, 78, 38, 225), width=1)
	draw.ellipse((bowl[0] - 4, bowl[1] - 3, bowl[0] + 5, bowl[1] + 6), fill=(84, 45, 22, 245), outline=(36, 22, 15, 235), width=1)


def draw_smoke(draw: ImageDraw.ImageDraw, mouth: tuple[float, float], direction: str, frame: int) -> None:
	side = side_for(direction)
	x, y = mouth
	drift = (frame - 1) * 1.4
	first = ordered_rect(x + side * (6 + drift), y - 24, x + side * (26 + drift), y - 6)
	second = ordered_rect(x + side * (12 + drift), y - 37, x + side * (35 + drift), y - 14)
	draw.arc(first, 110 if side > 0 else 250, 285 if side > 0 else 70, fill=(217, 217, 205, 145), width=2)
	draw.arc(second, 105 if side > 0 else 245, 290 if side > 0 else 75, fill=(217, 217, 205, 100), width=1)


def ordered_rect(x0: float, y0: float, x1: float, y1: float) -> tuple[float, float, float, float]:
	return (min(x0, x1), min(y0, y1), max(x0, x1), max(y0, y1))


def draw_scratch_marks(draw: ImageDraw.ImageDraw, hand: tuple[float, float], direction: str, frame: int) -> None:
	if frame in (1, 8):
		return

	side = side_for(direction)
	x, y = hand
	for i in range(3):
		y_offset = -5 + i * 4
		draw.line((x + side * 4, y + y_offset, x + side * 11, y + y_offset - 2), fill=(226, 198, 166, 150), width=1)


def add_soft_shadow(overlay: Image.Image) -> None:
	alpha = overlay.getchannel("A").filter(ImageFilter.GaussianBlur(0.35))
	shadow = Image.new("RGBA", overlay.size, (0, 0, 0, 42))
	shadow.putalpha(alpha)
	combined = Image.alpha_composite(shadow, overlay)
	overlay.paste(combined)


def ping_pong(frame: int) -> float:
	t = (frame - 1) / (FRAME_COUNT - 1)
	return 1.0 - abs(t * 2.0 - 1.0)


def ease_in_out(value: float) -> float:
	return value * value * (3.0 - 2.0 * value)


def side_for(direction: str) -> int:
	return -1 if direction == "left" else 1


def lerp_point(a: tuple[float, float], b: tuple[float, float], t: float) -> tuple[float, float]:
	return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)


def variant_frame_path(variant_id: str, direction: str, frame: int) -> Path:
	return VARIANT_ROOT / variant_id / "aligned" / f"butler_classic_{variant_id}_{direction}_{frame:02}.png"


def write_animation_assets() -> None:
	ANIMATION_ROOT.mkdir(parents=True, exist_ok=True)
	base_controller = BASE_CONTROLLER_PATH.read_text(encoding="utf-8")
	base_idle_guids = {direction: read_guid(asset_path + ".meta") for direction, asset_path in BASE_IDLE_CLIPS.items()}

	for variant_id, variant_name in VARIANTS:
		clip_guids: dict[str, str] = {}
		for direction in DIRECTIONS:
			direction_name = direction.title()
			clip_name = f"ButlerClassic_{variant_name}_Idle_{direction_name}"
			clip_asset = f"Assets/Animation/ButlerClassic/IdleVariants/{clip_name}.anim"
			frame_guids = [
				read_guid(f"Assets/Characters/ButlerClassic/idle_variants/{variant_id}/aligned/butler_classic_{variant_id}_{direction}_{frame:02}.png.meta")
				for frame in range(1, FRAME_COUNT + 1)
			]
			(ROOT / clip_asset).write_text(animation_clip_yaml(clip_name, frame_guids), encoding="utf-8")
			clip_guids[direction_name] = ensure_native_meta(ROOT / clip_asset, 7400000)

		controller_name = f"ButlerClassic_{variant_name}"
		controller_asset = f"Assets/Animation/ButlerClassic/IdleVariants/{controller_name}.controller"
		controller = base_controller.replace("m_Name: ButlerClassic\n", f"m_Name: {controller_name}\n", 1)
		for direction_name, old_guid in base_idle_guids.items():
			new_clip_name = f"ButlerClassic_{variant_name}_Idle_{direction_name}"
			controller = controller.replace(f"m_Name: ButlerClassic_Idle_{direction_name}", f"m_Name: {new_clip_name}")
			controller = controller.replace(f"guid: {old_guid}, type: 2", f"guid: {clip_guids[direction_name]}, type: 2")

		(ROOT / controller_asset).write_text(controller, encoding="utf-8")
		ensure_native_meta(ROOT / controller_asset, 9100000)


def animation_clip_yaml(name: str, sprite_guids: list[str]) -> str:
	times = [format_time(index / FRAME_RATE) for index in range(len(sprite_guids))]

	def sprite_ref(guid: str) -> str:
		return f"{{fileID: 21300000, guid: {guid}, type: 3}}"

	def curve_lines(script: str, class_id: int) -> str:
		lines = ["  - serializedVersion: 2", "    curve:"]
		for time, guid in zip(times, sprite_guids):
			lines.append(f"    - time: {time}")
			lines.append(f"      value: {sprite_ref(guid)}")
		lines.extend(
			[
				"    attribute: m_Sprite",
				"    path: ",
				f"    classID: {class_id}",
				f"    script: {script}",
				"    flags: 2",
			]
		)
		return "\n".join(lines)

	mapping = "\n".join(f"    - {sprite_ref(guid)}" for _ in range(2) for guid in sprite_guids)
	stop_time = format_time(len(sprite_guids) / FRAME_RATE)
	return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!74 &7400000
AnimationClip:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  serializedVersion: 7
  m_Legacy: 0
  m_Compressed: 0
  m_UseHighQualityCurve: 1
  m_RotationCurves: []
  m_CompressedRotationCurves: []
  m_EulerCurves: []
  m_PositionCurves: []
  m_ScaleCurves: []
  m_FloatCurves: []
  m_PPtrCurves:
{curve_lines("{fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}", 114)}
{curve_lines("{fileID: 0}", 212)}
  m_SampleRate: {FRAME_RATE}
  m_WrapMode: 0
  m_Bounds:
    m_Center: {{x: 0, y: 0, z: 0}}
    m_Extent: {{x: 0, y: 0, z: 0}}
  m_ClipBindingConstant:
    genericBindings:
    - serializedVersion: 2
      path: 0
      attribute: 0
      script: {{fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}}
      typeID: 114
      customType: 23
      isPPtrCurve: 1
      isIntCurve: 0
      isSerializeReferenceCurve: 0
    - serializedVersion: 2
      path: 0
      attribute: 0
      script: {{fileID: 0}}
      typeID: 212
      customType: 23
      isPPtrCurve: 1
      isIntCurve: 0
      isSerializeReferenceCurve: 0
    pptrCurveMapping:
{mapping}
  m_AnimationClipSettings:
    serializedVersion: 2
    m_AdditiveReferencePoseClip: {{fileID: 0}}
    m_AdditiveReferencePoseTime: 0
    m_StartTime: 0
    m_StopTime: {stop_time}
    m_OrientationOffsetY: 0
    m_Level: 0
    m_CycleOffset: 0
    m_HasAdditiveReferencePose: 0
    m_LoopTime: 1
    m_LoopBlend: 0
    m_LoopBlendOrientation: 0
    m_LoopBlendPositionY: 0
    m_LoopBlendPositionXZ: 0
    m_KeepOriginalOrientation: 0
    m_KeepOriginalPositionY: 1
    m_KeepOriginalPositionXZ: 0
    m_HeightFromFeet: 0
    m_Mirror: 0
  m_EditorCurves: []
  m_EulerEditorCurves: []
  m_HasGenericRootTransform: 0
  m_HasMotionFloatCurves: 0
  m_Events: []
"""


def format_time(value: float) -> str:
	if abs(value - round(value)) < 0.00001:
		return str(int(round(value)))
	return f"{value:.9f}".rstrip("0").rstrip(".")


def read_guid(meta_asset_path: str) -> str:
	path = ROOT / meta_asset_path
	match = re.search(r"^guid: ([a-f0-9]{32})$", path.read_text(encoding="utf-8"), re.MULTILINE)
	if not match:
		raise RuntimeError(f"No Unity guid in {path}")
	return match.group(1)


def ensure_folder_meta(folder: Path, meta_path: Path) -> None:
	folder.mkdir(parents=True, exist_ok=True)
	if meta_path.exists():
		return

	meta_path.write_text(
		f"""fileFormatVersion: 2
guid: {stable_guid(asset_path_for(meta_path))}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""",
		encoding="utf-8",
	)


def ensure_texture_meta(texture_path: Path) -> None:
	meta_path = texture_path.with_suffix(texture_path.suffix + ".meta")
	if meta_path.exists():
		return

	meta_path.write_text(
		f"""fileFormatVersion: 2
guid: {stable_guid(asset_path_for(texture_path))}
TextureImporter:
  fileIDToRecycleName: {{}}
  externalObjects: {{}}
  serializedVersion: 6
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
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
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
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.0}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  platformSettings:
  - serializedVersion: 2
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    androidETC2FallbackOverride: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: {stable_guid(asset_path_for(texture_path) + ":sprite")}
    vertices: []
    indices: 
    edges: []
    weights: []
  spritePackingTag: 
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""",
		encoding="utf-8",
	)


def ensure_native_meta(asset_path: Path, main_object_file_id: int) -> str:
	meta_path = asset_path.with_suffix(asset_path.suffix + ".meta")
	guid = existing_guid(meta_path) or stable_guid(asset_path_for(asset_path))
	meta_path.write_text(
		f"""fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: {main_object_file_id}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""",
		encoding="utf-8",
	)
	return guid


def existing_guid(meta_path: Path) -> str | None:
	if not meta_path.exists():
		return None

	match = re.search(r"^guid: ([a-f0-9]{32})$", meta_path.read_text(encoding="utf-8"), re.MULTILINE)
	return match.group(1) if match else None


def asset_path_for(path: Path) -> str:
	return path.relative_to(ROOT).as_posix().removesuffix(".meta")


def stable_guid(text: str) -> str:
	return hashlib.md5(text.encode("utf-8")).hexdigest()


if __name__ == "__main__":
	main()
