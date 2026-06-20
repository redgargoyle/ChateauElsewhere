#!/usr/bin/env python3
"""Repair selected Chateau Chantilly guest voices with local F5-TTS.

Targets the current review notes:
- Guest 2 and Guest 3 sounded too similar.
- Guest 4 was too fast and unintelligible.
- Guest 6 varied accent across lines.

Only Guests 2, 3, 4, and 6 are regenerated. Other guests are untouched.
"""

from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
import time
from collections import Counter
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path

import numpy as np
import soundfile as sf
import torch
from f5_tts.api import F5TTS

import generate_guest_voice_audio_f5 as base


TARGET_GUESTS = (2, 3, 4, 6)
DEVICE = "cuda"
MODEL_NAME = "F5TTS_v1_Base"
FINAL_SAMPLE_RATE = 48000
TARGET_PEAK = 10 ** (-3.0 / 20.0)
PAUSE_SECONDS = 0.12
RESAMPLE_FILTER = f"aresample=osr={FINAL_SAMPLE_RATE}:filter_size=64:phase_shift=10:linear_interp=0"

ALT_GUEST3_REF = (
    base.HOME
    / "Desktop"
    / "Chateau_Voice_Auditions"
    / "F5_TTS"
    / "British_Victorian_G01_G03_Refined_Candidates_F5_48k_20260619_170005"
    / "Guest03_Mister_Florian_Knell_CH1_G03_ENTRY_B_BaronRef_Classy.wav"
)


@dataclass(frozen=True)
class RepairConfig:
    ref_source: Path
    ref_text: str
    seed: int
    normal_speed: float
    found_speed: float
    ambient_speed: float
    panic_speed: float
    dining_speed: float
    cfg_strength: float
    note: str


REPAIRS = {
    2: RepairConfig(
        base.GUESTS[2].ref_source,
        base.GUESTS[2].ref_text,
        32202,
        0.86,
        0.84,
        0.84,
        0.90,
        0.86,
        2.15,
        "keeps accepted Butler reference, slowed slightly for clearer classy RP",
    ),
    3: RepairConfig(
        ALT_GUEST3_REF,
        base.GUESTS[3].ref_text,
        33303,
        0.87,
        0.85,
        0.85,
        0.91,
        0.87,
        2.05,
        "switches from Butler-style Guest 3 reference to Baron-style Guest 3 reference for separation from Guest 2",
    ),
    4: RepairConfig(
        base.GUESTS[4].ref_source,
        base.GUESTS[4].ref_text,
        34404,
        0.78,
        0.76,
        0.76,
        0.84,
        0.80,
        2.25,
        "slower delivery and firmer conditioning for intelligibility",
    ),
    6: RepairConfig(
        base.GUESTS[6].ref_source,
        base.GUESTS[6].ref_text,
        36606,
        0.84,
        0.82,
        0.82,
        0.88,
        0.84,
        2.25,
        "constant seed/reference strategy to reduce accent wandering",
    ),
}


@dataclass
class RepairOutput:
    line: base.DialogueLine
    seed: int
    speed: float
    ref_path: Path
    final_path: Path
    asset_path: Path
    peak: float
    duration: float
    mode: str


def run_text(command: list[str]) -> str:
    try:
        return subprocess.run(command, check=True, text=True, capture_output=True).stdout.strip()
    except Exception as exc:
        return f"unavailable: {exc}"


def require_preflight() -> None:
    base.require_project_root()
    base.require_cuda()
    for guest, config in REPAIRS.items():
        if not config.ref_source.exists():
            raise FileNotFoundError(f"Missing repair reference for Guest {guest:02d}: {config.ref_source}")


def line_speed(guest: int, line_id: str) -> float:
    config = REPAIRS[guest]
    if "PANIC" in line_id:
        return config.panic_speed
    if "FOUND" in line_id:
        return config.found_speed
    if "AMBIENT" in line_id or "EMPTY_BELL" in line_id:
        return config.ambient_speed
    if "DINING_REVEAL" in line_id:
        return config.dining_speed
    return config.normal_speed


def add_pauses(wav: np.ndarray, sr: int) -> np.ndarray:
    data = np.asarray(wav, dtype=np.float32)
    if data.ndim == 2:
        data = data.mean(axis=1)
    pause = np.zeros(max(1, int(sr * PAUSE_SECONDS)), dtype=np.float32)
    return np.concatenate([pause, np.nan_to_num(data), pause])


def clean_reference(input_path: Path, output_path: Path) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    subprocess.run(
        [
            "ffmpeg",
            "-hide_banner",
            "-loglevel",
            "error",
            "-y",
            "-i",
            str(input_path),
            "-af",
            ",".join(
                [
                    "highpass=f=80",
                    "lowpass=f=11500",
                    "afftdn=nf=-32",
                    "equalizer=f=3200:t=q:w=0.9:g=0.8",
                    RESAMPLE_FILTER,
                ]
            ),
            "-ac",
            "1",
            "-c:a",
            "pcm_s16le",
            str(output_path),
        ],
        check=True,
    )


def master_audio(input_path: Path, temp_path: Path, output_path: Path) -> tuple[float, float]:
    temp_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    subprocess.run(
        [
            "ffmpeg",
            "-hide_banner",
            "-loglevel",
            "error",
            "-y",
            "-i",
            str(input_path),
            "-af",
            ",".join(
                [
                    "highpass=f=80",
                    "lowpass=f=11500",
                    "afftdn=nf=-34",
                    "equalizer=f=260:t=q:w=1.0:g=-0.6",
                    "equalizer=f=3200:t=q:w=0.9:g=1.0",
                    "equalizer=f=7600:t=q:w=1.1:g=0.4",
                    RESAMPLE_FILTER,
                ]
            ),
            "-ac",
            "1",
            "-c:a",
            "pcm_f32le",
            str(temp_path),
        ],
        check=True,
    )
    data, sample_rate = sf.read(temp_path, always_2d=True, dtype="float32")
    if sample_rate != FINAL_SAMPLE_RATE:
        raise ValueError(f"Expected {FINAL_SAMPLE_RATE} Hz, got {sample_rate} for {temp_path}")
    mono = np.nan_to_num(data.mean(axis=1), nan=0.0, posinf=0.0, neginf=0.0)
    peak = float(np.max(np.abs(mono))) if mono.size else 0.0
    if peak > 0:
        mono = mono * min(TARGET_PEAK / peak, 32.0)
    mono = np.clip(mono, -0.999, 0.999)
    sf.write(output_path, mono, FINAL_SAMPLE_RATE, subtype="PCM_16")
    temp_path.unlink(missing_ok=True)
    final_peak = float(np.max(np.abs(mono))) if mono.size else 0.0
    duration = float(len(mono) / FINAL_SAMPLE_RATE) if mono.size else 0.0
    return final_peak, duration


def validate_file(path: Path, text: str) -> tuple[float, float]:
    info = sf.info(path)
    if info.samplerate != FINAL_SAMPLE_RATE:
        raise ValueError(f"{path} is {info.samplerate} Hz, expected {FINAL_SAMPLE_RATE}")
    if info.channels != 1:
        raise ValueError(f"{path} has {info.channels} channels, expected mono")
    if info.subtype != "PCM_16":
        raise ValueError(f"{path} subtype is {info.subtype}, expected PCM_16")
    data, sr = sf.read(path, always_2d=True, dtype="float32")
    peak = float(np.max(np.abs(data))) if data.size else 0.0
    duration = float(len(data) / sr) if sr else 0.0
    min_duration = base.expected_min_duration(text)
    if duration < min_duration:
        raise ValueError(f"{path} is too short: {duration:.3f}s < {min_duration:.3f}s")
    if peak <= 0.001:
        raise ValueError(f"{path} appears silent")
    if peak >= 1.0:
        raise ValueError(f"{path} clips: peak={peak:.6f}")
    return peak, duration


def backup_target_assets(timestamp: str) -> Path:
    backup_dir = base.BACKUP_ROOT / f"guest_voice_repair_before_f5_v2_{timestamp}"
    for guest in TARGET_GUESTS:
        source = base.ASSET_GUEST_ROOT / base.GUESTS[guest].folder
        target = backup_dir / base.GUESTS[guest].folder
        if source.exists():
            shutil.copytree(source, target)
    return backup_dir


def copy_target_assets(final_dir: Path) -> None:
    for guest in TARGET_GUESTS:
        folder = base.GUESTS[guest].folder
        source_dir = final_dir / folder
        target_dir = base.ASSET_GUEST_ROOT / folder
        target_dir.mkdir(parents=True, exist_ok=True)
        for wav_path in sorted(source_dir.glob("*.wav")):
            shutil.copy2(wav_path, target_dir / wav_path.name)


def write_report(
    report_path: Path,
    *,
    run_dir: Path,
    final_dir: Path,
    clean_ref_dir: Path,
    outputs: list[RepairOutput],
    failures: list[str],
    backup_dir: Path | None,
    copied_to_assets: bool,
    elapsed: float,
) -> None:
    counts = Counter(item.line.guest for item in outputs)
    lines = [
        "# F5-TTS Targeted Guest Voice Repair V2",
        "",
        "Audio-generation only. Unity gameplay, subtitles, and playback hooks were not modified.",
        "",
        f"- Elapsed seconds: {elapsed:.1f}",
        f"- Model: {MODEL_NAME}",
        f"- Device requested: {DEVICE}",
        f"- Torch: {torch.__version__}",
        f"- CUDA build: {torch.version.cuda}",
        f"- CUDA available: {torch.cuda.is_available()}",
        f"- CUDA device: {torch.cuda.get_device_name(0) if torch.cuda.is_available() else 'none'}",
        f"- `nvidia-smi`: {run_text(['nvidia-smi', '--query-gpu=name,driver_version,memory.total', '--format=csv,noheader'])}",
        f"- Run folder: `{run_dir}`",
        f"- Final staging folder: `{final_dir}`",
        f"- Clean reference folder: `{clean_ref_dir}`",
        f"- Copied to Unity Assets: {copied_to_assets}",
        f"- Backup folder: `{backup_dir}`" if backup_dir else "- Backup folder: none",
        f"- Command used: `{sys.executable} {Path(__file__).resolve()}`",
        "",
        "## Fix Strategy",
        "",
        "- Guest 2: kept accepted Butler voice but slowed/classed it for clearer RP.",
        "- Guest 3: changed to the Baron-style Guest 3 reference to separate him from Guest 2.",
        "- Guest 4: slowed substantially and strengthened conditioning for intelligibility.",
        "- Guest 6: regenerated with one constant seed and one cleaned reference to reduce accent drift.",
        "- All repaired lines use fixed cleaned references and light denoise/mastering.",
        "",
        "## Counts",
        "",
        f"- Generated WAV count: {len(outputs)}",
        f"- Failed count: {len(failures)}",
    ]
    for guest in TARGET_GUESTS:
        lines.append(f"- Guest {guest:02d}: {counts[guest]} WAVs")

    lines.extend(["", "## Repair References", ""])
    for guest in TARGET_GUESTS:
        config = REPAIRS[guest]
        lines.extend(
            [
                f"- Guest {guest:02d} {base.GUESTS[guest].display_name}",
                f"  - source_ref: `{config.ref_source}`",
                f"  - ref_text: `{config.ref_text}`",
                f"  - seed: {config.seed}",
                f"  - speeds: normal={config.normal_speed:.2f}, found={config.found_speed:.2f}, ambient={config.ambient_speed:.2f}, panic={config.panic_speed:.2f}, dining={config.dining_speed:.2f}",
                f"  - cfg_strength: {config.cfg_strength:.2f}",
                f"  - note: {config.note}",
            ]
        )

    lines.extend(["", "## Generated Files", ""])
    for item in outputs:
        lines.append(
            f"- Guest {item.line.guest:02d} `{item.line.line_id}`: mode={item.mode}, seed={item.seed}, "
            f"speed={item.speed:.2f}, duration={item.duration:.3f}s, peak={item.peak:.4f}, "
            f"ref=`{item.ref_path}`, final=`{item.final_path}`, asset=`{item.asset_path}`"
        )

    lines.extend(["", "## Failures", ""])
    lines.extend(f"- {failure}" for failure in failures) if failures else lines.append("- None")

    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--stage-only", action="store_true", help="Generate staging files without copying into Assets.")
    return parser


def main() -> int:
    args = build_parser().parse_args()
    require_preflight()
    prompt_path = base.find_prompt_path()
    dialogue = [line for line in base.parse_dialogue(prompt_path) if line.guest in TARGET_GUESTS]

    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    start = time.time()
    run_dir = base.GENERATED_ROOT / f"f5_targeted_repair_v2_{timestamp}"
    raw_dir = run_dir / "raw_native"
    temp_dir = run_dir / "temp_48k_float"
    final_dir = run_dir / "final_48k"
    clean_ref_dir = run_dir / "clean_references"
    for guest in TARGET_GUESTS:
        folder = base.GUESTS[guest].folder
        (raw_dir / folder).mkdir(parents=True, exist_ok=True)
        (temp_dir / folder).mkdir(parents=True, exist_ok=True)
        (final_dir / folder).mkdir(parents=True, exist_ok=True)

    clean_refs: dict[int, Path] = {}
    for guest, config in REPAIRS.items():
        clean_ref = clean_ref_dir / f"guest{guest:02d}_repair_ref.wav"
        clean_reference(config.ref_source, clean_ref)
        validate_file(clean_ref, config.ref_text)
        clean_refs[guest] = clean_ref

    print(f"Loading {MODEL_NAME} on {DEVICE}...")
    model = F5TTS(model=MODEL_NAME, device=DEVICE)

    outputs: list[RepairOutput] = []
    failures: list[str] = []
    for line in dialogue:
        config = REPAIRS[line.guest]
        folder = base.GUESTS[line.guest].folder
        raw_path = raw_dir / folder / line.output_name
        temp_path = temp_dir / folder / line.output_name
        final_path = final_dir / folder / line.output_name
        asset_path = base.ASSET_GUEST_ROOT / folder / line.output_name
        speed = line_speed(line.guest, line.line_id)
        seed = config.seed
        mode = "cleaned_reference_entry" if line.line_id == base.GUESTS[line.guest].entry_line_id else "f5_repair_synthesis"
        print(f"[Repair Guest {line.guest:02d} {line.index_for_guest + 1:02d}/20] {line.line_id}")
        try:
            if mode == "cleaned_reference_entry":
                raw_path.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(clean_refs[line.guest], raw_path)
            else:
                wav, sr, _spec = model.infer(
                    ref_file=str(clean_refs[line.guest]),
                    ref_text=config.ref_text,
                    gen_text=line.text,
                    show_info=lambda *args, **_kwargs: print(*args),
                    target_rms=0.1,
                    cross_fade_duration=0.18,
                    sway_sampling_coef=-1.0,
                    cfg_strength=config.cfg_strength,
                    nfe_step=64,
                    speed=speed,
                    remove_silence=False,
                    seed=seed,
                )
                raw_path.parent.mkdir(parents=True, exist_ok=True)
                sf.write(raw_path, add_pauses(wav, int(sr)), int(sr), subtype="PCM_16")
            peak, duration = master_audio(raw_path, temp_path, final_path)
            validate_file(final_path, line.text)
            outputs.append(
                RepairOutput(
                    line=line,
                    seed=seed,
                    speed=speed,
                    ref_path=clean_refs[line.guest],
                    final_path=final_path,
                    asset_path=asset_path,
                    peak=peak,
                    duration=duration,
                    mode=mode,
                )
            )
        except Exception as exc:
            failures.append(f"{line.line_id}: {type(exc).__name__}: {exc}")
            print(f"FAILED {line.line_id}: {exc}", file=sys.stderr)

    for guest in TARGET_GUESTS:
        count = len(list((final_dir / base.GUESTS[guest].folder).glob("*.wav")))
        if count != 20:
            failures.append(f"Guest {guest:02d} final count is {count}, expected 20")
    if len(outputs) != 80:
        failures.append(f"Generated {len(outputs)} repaired files, expected 80")

    backup_dir = None
    copied = False
    if not failures and not args.stage_only:
        backup_dir = backup_target_assets(timestamp)
        copy_target_assets(final_dir)
        copied = True

    report_path = base.REPORT_ROOT / "guest_voice_repair_report_f5_v2.md"
    write_report(
        report_path,
        run_dir=run_dir,
        final_dir=final_dir,
        clean_ref_dir=clean_ref_dir,
        outputs=outputs,
        failures=failures,
        backup_dir=backup_dir,
        copied_to_assets=copied,
        elapsed=time.time() - start,
    )
    shutil.copy2(report_path, run_dir / "guest_voice_repair_report_f5_v2.md")

    if failures:
        print(f"Completed with failures. Report: {report_path}")
        return 1

    print(f"Generated {len(outputs)} repaired F5-TTS WAVs.")
    print(f"Staging: {run_dir}")
    print(f"Assets repaired: {[base.GUESTS[g].folder for g in TARGET_GUESTS]}")
    print(f"Report: {report_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
