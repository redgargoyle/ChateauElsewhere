#!/usr/bin/env python3
"""Deterministically recover 2x detail for the reviewed Butler sprite set."""

from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage
import torch
from spandrel import ModelLoader


SOURCE_SIZE = (168, 299)
OUTPUT_SIZE = (336, 598)
PLANTED_SOURCE_Y = 235
PLANTED_OUTPUT_Y = PLANTED_SOURCE_Y * 2
CANONICAL_RELATIVE_PATH = Path(
    "Assets/Art/Characters/butler/butler_classic_walk_01_r01_c01.png"
)
IDLE_DIRECTORY = Path("Assets/Art/Characters/butler/butler_idle")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("manifest", type=Path)
    parser.add_argument("cleaned_root", type=Path)
    parser.add_argument("output_root", type=Path)
    parser.add_argument("model", type=Path)
    return parser.parse_args()


def manifest_paths(path: Path) -> list[Path]:
    paths: list[Path] = []
    for raw_line in path.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if line and not line.startswith("#"):
            paths.append(Path(line))
    if not paths:
        raise RuntimeError(f"No sprites listed in {path}")
    return paths


def bleed_rgb_under_transparency(rgba: np.ndarray) -> np.ndarray:
    alpha = rgba[:, :, 3]
    visible = alpha > 8
    if not np.any(visible):
        raise RuntimeError("Sprite contains no visible pixels")

    _, nearest = ndimage.distance_transform_edt(~visible, return_indices=True)
    rgb = rgba[:, :, :3].copy()
    rgb[~visible] = rgb[nearest[0][~visible], nearest[1][~visible]]
    return rgb


def upscale_rgba(
    source_path: Path,
    model: object,
    device: torch.device,
) -> np.ndarray:
    rgba = np.asarray(Image.open(source_path).convert("RGBA"), dtype=np.uint8)
    if (rgba.shape[1], rgba.shape[0]) != SOURCE_SIZE:
        raise RuntimeError(
            f"{source_path} is {rgba.shape[1]}x{rgba.shape[0]}, expected "
            f"{SOURCE_SIZE[0]}x{SOURCE_SIZE[1]}"
        )

    rgb = bleed_rgb_under_transparency(rgba)
    tensor = (
        torch.from_numpy(rgb.copy())
        .permute(2, 0, 1)
        .unsqueeze(0)
        .to(device=device, dtype=torch.float32)
        .div_(255.0)
    )
    with torch.inference_mode():
        result = model(tensor).clamp_(0.0, 1.0)[0]
    rgb_x2 = np.rint(
        result.permute(1, 2, 0).to(dtype=torch.float32).cpu().numpy() * 255.0
    ).astype(np.uint8)

    alpha = Image.fromarray(rgba[:, :, 3], "L")
    alpha_x2 = np.asarray(
        alpha.resize(OUTPUT_SIZE, Image.Resampling.LANCZOS), dtype=np.uint8
    )
    output = np.dstack((rgb_x2, alpha_x2))
    if (output.shape[1], output.shape[0]) != OUTPUT_SIZE:
        raise RuntimeError(f"Model returned unexpected size for {source_path}")
    return output


def write_png(path: Path, rgba: np.ndarray) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(rgba, "RGBA").save(path, optimize=True)


def main() -> int:
    args = parse_args()
    paths = manifest_paths(args.manifest)
    if not args.model.is_file():
        raise FileNotFoundError(args.model)

    torch.manual_seed(0)
    torch.backends.cudnn.benchmark = False
    torch.backends.cudnn.deterministic = True
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    model = ModelLoader().load_from_file(args.model).to(device).float().eval()
    if model.scale != 2 or model.input_channels != 3 or model.output_channels != 3:
        raise RuntimeError("Expected a three-channel 2x image model")

    outputs: dict[Path, np.ndarray] = {}
    for index, relative_path in enumerate(paths, start=1):
        source_path = args.cleaned_root / relative_path
        if not source_path.is_file():
            raise FileNotFoundError(source_path)
        output = upscale_rgba(source_path, model, device)
        outputs[relative_path] = output
        print(f"[{index:02d}/{len(paths):02d}] {relative_path}", flush=True)

    canonical = outputs.get(CANONICAL_RELATIVE_PATH)
    if canonical is None:
        raise RuntimeError("Manifest omitted the canonical planted Butler frame")
    for relative_path, output in outputs.items():
        if relative_path.parent == IDLE_DIRECTORY:
            output[PLANTED_OUTPUT_Y:, :, :] = canonical[PLANTED_OUTPUT_Y:, :, :]

    for relative_path, output in outputs.items():
        write_png(args.output_root / relative_path, output)

    print(
        f"wrote={len(outputs)} size={OUTPUT_SIZE[0]}x{OUTPUT_SIZE[1]} "
        f"device={device.type} plantedRows={PLANTED_OUTPUT_Y}-{OUTPUT_SIZE[1] - 1}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
