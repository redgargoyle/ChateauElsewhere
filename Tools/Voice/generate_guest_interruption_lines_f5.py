#!/usr/bin/env python3
"""Generate the eight guest interruption barks with local F5-TTS."""

from __future__ import annotations

import shutil
import sys
import time
from collections import Counter
from datetime import datetime
from pathlib import Path

from generate_guest_voice_audio_f5 import (
    ASSET_GUEST_ROOT,
    DEVICE,
    GUESTS,
    MODEL_NAME,
    REPORT_ROOT,
    VOICE_ROOT,
    DialogueLine,
    F5TTS,
    generate_line,
    require_cuda,
    require_project_root,
    run_text,
)


GENERATED_ROOT = VOICE_ROOT / "generated_f5_guest_interruptions"
LINE_TEXT = "You interrupted me."


def write_report(
    report_path: Path,
    *,
    started_at: str,
    elapsed: float,
    run_dir: Path,
    generated: list[tuple[DialogueLine, Path, Path, int, float, float, float]],
) -> None:
    counts = Counter(line.guest for line, *_rest in generated)
    lines = [
        "# F5-TTS Guest Interruption Bark Report",
        "",
        "Generated the coat-pickup interruption barks only.",
        "",
        f"- Started: {started_at}",
        f"- Elapsed seconds: {elapsed:.1f}",
        f"- Model: {MODEL_NAME}",
        f"- Device requested: {DEVICE}",
        f"- CUDA device: {run_text(['nvidia-smi', '--query-gpu=name', '--format=csv,noheader'])}",
        f"- Staging folder: `{run_dir}`",
        f"- Unity asset root: `{ASSET_GUEST_ROOT}`",
        f"- Transcript: {LINE_TEXT}",
        "",
        "## Counts",
        "",
        f"- Total generated: {len(generated)}",
    ]

    for guest in range(1, 9):
        lines.append(f"- Guest {guest:02d}: {counts[guest]}")

    lines.extend(["", "## Files", ""])
    for line, final_path, asset_path, seed, speed, peak, duration in generated:
        lines.append(
            f"- `{line.line_id}`: seed={seed}, speed={speed:.2f}, duration={duration:.3f}s, "
            f"peak={peak:.4f}, final=`{final_path}`, asset=`{asset_path}`"
        )

    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    require_project_root()
    require_cuda()

    started = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    start = time.time()

    run_dir = GENERATED_ROOT / f"f5_guest_interruptions_{timestamp}"
    raw_dir = run_dir / "raw_native"
    temp_dir = run_dir / "temp_48k_float"
    final_dir = run_dir / "final_48k"

    for guest in range(1, 9):
        config = GUESTS[guest]
        for root in (raw_dir, temp_dir, final_dir):
            (root / config.folder).mkdir(parents=True, exist_ok=True)

    print(f"Loading {MODEL_NAME} on {DEVICE}...")
    model = F5TTS(model=MODEL_NAME, device=DEVICE)

    generated: list[tuple[DialogueLine, Path, Path, int, float, float, float]] = []
    for guest in range(1, 9):
        config = GUESTS[guest]
        line = DialogueLine(
            line_id=f"CH1_G{guest:02d}_INTERRUPTED",
            speaker=f"Guest {guest}",
            text=LINE_TEXT,
            guest=guest,
            index_for_guest=0,
        )
        seed = config.seed_base + 777
        speed = max(0.78, min(0.94, config.normal_speed + 0.01))
        raw_path = raw_dir / config.folder / line.output_name
        temp_path = temp_dir / config.folder / line.output_name
        final_path = final_dir / config.folder / line.output_name
        asset_path = ASSET_GUEST_ROOT / config.folder / line.output_name

        print(f"[Guest {guest:02d}] {line.line_id}")
        peak, duration = generate_line(model, line, raw_path, final_path, temp_path, seed, speed)

        asset_path.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(final_path, asset_path)
        generated.append((line, final_path, asset_path, seed, speed, peak, duration))

    if len(generated) != 8:
        raise RuntimeError(f"Generated {len(generated)} files, expected 8")

    report_path = REPORT_ROOT / "guest_interruption_generation_report_f5.md"
    write_report(
        report_path,
        started_at=started,
        elapsed=time.time() - start,
        run_dir=run_dir,
        generated=generated,
    )
    shutil.copy2(report_path, run_dir / "guest_interruption_generation_report_f5.md")

    print(f"Generated {len(generated)} interruption WAVs.")
    print(f"Staging: {run_dir}")
    print(f"Report: {report_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
