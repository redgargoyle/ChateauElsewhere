#!/usr/bin/env python
from __future__ import annotations

import shutil
import subprocess
import time
from dataclasses import dataclass, replace
from datetime import datetime
from pathlib import Path

import numpy as np
import soundfile as sf
import torch

from generate_british_victorian_arrivals import (
    AUDITION_ROOT,
    GUESTS,
    TARGET_PEAK,
    generate_clip,
    safe_name,
)


FINAL_SAMPLE_RATE = 48000
SOXR_FILTER = f"aresample=resampler=soxr:precision=33:osr={FINAL_SAMPLE_RATE}"
DESKTOP_BASE = Path.home() / "Desktop" / "Chateau_Voice_Auditions" / "Chatterbox"


@dataclass(frozen=True)
class Mastering:
    pitch: float = 1.0
    tempo: float = 1.0


REVIEW_NOTES = {
    1: "Make more unmistakably British, classier, and crisp.",
    2: "Good; preserve and class up lightly.",
    3: "Decent; preserve while adding polish.",
    4: "Good; preserve.",
    5: "Needs stronger British class marker.",
    6: "Sounds southern; rebuild from Countess-style female RP anchor.",
    7: "Sounds Indian; rebuild from refined male RP anchor, avoid Indian cadence.",
    8: "Keep female and aristocratic; avoid masculine tone.",
}


def latest_run(prefix: str, final_name: str) -> Path:
    matches = sorted(
        [path / final_name for path in AUDITION_ROOT.glob(f"{prefix}_*") if (path / final_name).is_dir()],
        key=lambda path: path.stat().st_mtime,
    )
    if not matches:
        raise FileNotFoundError(f"No prior run found for {prefix}_*/{final_name}")
    return matches[-1]


def run_text(command: list[str]) -> str:
    try:
        return subprocess.run(command, check=True, text=True, capture_output=True).stdout.strip()
    except Exception as exc:
        return f"unavailable: {exc}"


def normalize_file_48k(input_path: Path, output_path: Path) -> float:
    data, sample_rate = sf.read(input_path, always_2d=True, dtype="float32")
    if sample_rate != FINAL_SAMPLE_RATE:
        raise ValueError(f"Expected {FINAL_SAMPLE_RATE} Hz after mastering, got {sample_rate} for {input_path}")
    mono = data.mean(axis=1)
    mono = np.nan_to_num(mono, nan=0.0, posinf=0.0, neginf=0.0)
    peak = float(np.max(np.abs(mono))) if mono.size else 0.0
    if peak > 0:
        mono = mono * min(TARGET_PEAK / peak, 24.0)
    mono = np.clip(mono, -0.999, 0.999)
    sf.write(output_path, mono, FINAL_SAMPLE_RATE, subtype="PCM_16")
    return float(np.max(np.abs(mono))) if mono.size else 0.0


def master_audio_48k(input_path: Path, temp_path: Path, output_path: Path, mastering: Mastering) -> float:
    filters: list[str] = []
    if abs(mastering.pitch - 1.0) >= 0.001 or abs(mastering.tempo - 1.0) >= 0.001:
        filters.append(
            f"rubberband=pitch={mastering.pitch}:tempo={mastering.tempo}:"
            "formant=preserved:pitchq=quality"
        )

    # Light dialogue-presence mastering: remove rumble/mud and add a small consonant lift.
    filters.extend(
        [
            "highpass=f=75",
            "equalizer=f=260:t=q:w=1.0:g=-1.1",
            "equalizer=f=3200:t=q:w=0.9:g=1.4",
            "equalizer=f=8200:t=q:w=1.2:g=0.8",
            SOXR_FILTER,
        ]
    )

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
            ",".join(filters),
            "-ac",
            "1",
            "-c:a",
            "pcm_f32le",
            str(temp_path),
        ],
        check=True,
    )
    peak = normalize_file_48k(temp_path, output_path)
    temp_path.unlink(missing_ok=True)
    return peak


def v5_specs():
    original = {guest.guest: guest for guest in GUESTS}
    v4_dir = latest_run("british_victorian_audition_v4_48k", "final_mastered_48k")
    v3_dir = latest_run("british_victorian_audition_v3", "final_mastered")
    v2_dir = latest_run("british_victorian_audition_v2", "final_mastered")
    v1_dir = AUDITION_ROOT / "arrival_audition_20260619_135604_pitch_checked"

    female_countess = v4_dir / "Guest04_Countess_Elowen_Dusk_CH1_G04_ENTRY.wav"
    female_sabine = v4_dir / "Guest06_Lady_Sabine_Marrow_CH1_G06_ENTRY.wav"
    male_butler = v4_dir / "Guest02_Butler_Guest_CH1_G02_ENTRY.wav"
    male_v2_baron = v2_dir / "Guest05_Baron_Hector_Glass_CH1_G05_ENTRY.wav"
    g3_v1 = v1_dir / "Guest03_Mister_Florian_Knell_CH1_G03_ENTRY.wav"

    specs = [
        (
            replace(
                original[1],
                seed=8101,
                base_prompt=female_sabine,
                anchor_text=(
                    "Good evening, Butler. I should be frightfully obliged for a proper fire; "
                    "one must not let dreadful weather bully one's vowels."
                ),
                profile="Young upper-class English lady; crisp cut-glass RP, clear and classy.",
                exaggeration=0.74,
                cfg_weight=0.18,
                temperature=0.94,
            ),
            Mastering(pitch=1.13, tempo=1.06),
        ),
        (
            replace(
                original[2],
                seed=8202,
                base_prompt=male_butler,
                anchor_text=(
                    "Thank you. A civil fire and a properly spoken room would be most welcome "
                    "after such a damp and barbarous road."
                ),
                profile="Refined RP English gentleman; polished and restrained.",
                exaggeration=0.56,
                cfg_weight=0.27,
                temperature=0.84,
            ),
            Mastering(pitch=1.01, tempo=1.02),
        ),
        (
            replace(
                original[3],
                seed=8303,
                base_prompt=g3_v1,
                anchor_text=(
                    "My dear Butler, how deliciously ominous. I adore a dreadful house, "
                    "provided it speaks with proper English theatricality."
                ),
                profile="Theatrical English gentleman; bright, witty, polished RP.",
                exaggeration=0.72,
                cfg_weight=0.19,
                temperature=0.93,
            ),
            Mastering(pitch=0.995, tempo=1.06),
        ),
        (
            replace(
                original[4],
                seed=8404,
                base_prompt=female_countess,
                anchor_text=(
                    "Good evening, Butler. Pray take my cloak. The night has behaved abominably, "
                    "and I shall not let it lower the tone."
                ),
                profile="Countess; preserve classy aristocratic RP woman.",
                exaggeration=0.52,
                cfg_weight=0.31,
                temperature=0.80,
            ),
            Mastering(pitch=0.995, tempo=1.00),
        ),
        (
            replace(
                original[5],
                seed=8505,
                base_prompt=male_v2_baron,
                anchor_text=(
                    "Good evening. Let us conduct ourselves like gentlemen. Panic is vulgar, "
                    "and vulgarity has no place at dinner."
                ),
                profile="Classy English baron; stronger RP, smooth authority, not robotic.",
                exaggeration=0.66,
                cfg_weight=0.20,
                temperature=0.90,
            ),
            Mastering(pitch=0.975, tempo=1.06),
        ),
        (
            replace(
                original[6],
                seed=8606,
                base_prompt=female_countess,
                anchor_text=(
                    "Thank you. I am quite composed, naturally, though the bell pull looked "
                    "rather too much like a funeral invitation."
                ),
                profile="Lady Sabine; refined RP English woman, anxious but never southern.",
                exaggeration=0.68,
                cfg_weight=0.19,
                temperature=0.92,
            ),
            Mastering(pitch=1.075, tempo=1.04),
        ),
        (
            replace(
                original[7],
                seed=8707,
                base_prompt=male_butler,
                anchor_text=(
                    "Lovely to see you. The stones are listening tonight, but we shall speak "
                    "with careful English breeding and give them nothing coarse to remember."
                ),
                profile="Lord Ambrose; haunted upper-class English man, precise RP, no Indian cadence.",
                exaggeration=0.64,
                cfg_weight=0.20,
                temperature=0.90,
            ),
            Mastering(pitch=1.03, tempo=1.07),
        ),
        (
            replace(
                original[8],
                seed=8808,
                base_prompt=female_countess,
                anchor_text=(
                    "Good evening, Butler. Compose yourself. The house may posture and preen, "
                    "but a well-bred woman need not raise her voice to command it."
                ),
                profile="Madame Coralie; cool clipped aristocratic RP woman, female and commanding.",
                exaggeration=0.62,
                cfg_weight=0.21,
                temperature=0.88,
            ),
            Mastering(pitch=1.045, tempo=1.02),
        ),
    ]
    references = {
        "female_countess": female_countess,
        "female_sabine": female_sabine,
        "male_butler": male_butler,
        "male_v2_baron": male_v2_baron,
        "g3_v1": g3_v1,
        "v3_dir": v3_dir,
    }
    return specs, references


def main() -> int:
    from chatterbox.tts import ChatterboxTTS

    specs, references = v5_specs()
    missing = [guest.base_prompt for guest, _ in specs if not guest.base_prompt.exists()]
    if missing:
        for path in missing:
            print(f"MISSING BASE PROMPT: {path}")
        return 2

    if not torch.cuda.is_available():
        raise SystemExit("CUDA is required for this v5 audition pass.")

    stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    run_dir = AUDITION_ROOT / f"british_victorian_audition_v5_classy_crisp_48k_{stamp}"
    anchor_dir = run_dir / "generated_british_anchors"
    raw_dir = run_dir / "raw_chatterbox_native"
    mastered_temp_dir = run_dir / "mastered_temp_48k_float"
    final_dir = run_dir / "final_mastered_48k"
    desktop_dir = DESKTOP_BASE / f"British_Victorian_Guest_Arrivals_V5_Classy_Crisp_48k_{stamp}"
    for path in (anchor_dir, raw_dir, mastered_temp_dir, final_dir, desktop_dir):
        path.mkdir(parents=True, exist_ok=False)

    device = "cuda"
    started = time.time()
    print(f"Loading Chatterbox on {device}")
    model = ChatterboxTTS.from_pretrained(device=device)

    results = []
    failures: list[str] = []
    for guest, mastering in specs:
        anchor_path = anchor_dir / f"Guest{guest.guest:02d}_{guest.name.replace(' ', '_')}_british_anchor_v5.wav"
        raw_path = raw_dir / safe_name(guest)
        temp_path = mastered_temp_dir / safe_name(guest)
        final_path = final_dir / safe_name(guest)
        try:
            print(f"Generating v5 classy British anchor for Guest {guest.guest:02d} {guest.name}")
            anchor_duration = generate_clip(model, guest, guest.anchor_text, guest.base_prompt, anchor_path, 0)
            print(f"Generating v5 arrival line for Guest {guest.guest:02d} {guest.name}")
            raw_duration = generate_clip(model, guest, guest.arrival_text, anchor_path, raw_path, 100)
            final_peak = master_audio_48k(raw_path, temp_path, final_path, mastering)
            shutil.copy2(final_path, desktop_dir / final_path.name)
            results.append((guest, mastering, anchor_path, anchor_duration, raw_path, raw_duration, final_path, final_peak))
        except Exception as exc:
            failures.append(f"Guest {guest.guest:02d} {guest.name}: {type(exc).__name__}: {exc}")
            print(f"FAILED Guest {guest.guest:02d} {guest.name}: {exc}")

    report = run_dir / "AUDITION_REPORT.md"
    lines = [
        "# British Victorian Guest Arrival Audition V5 Classy Crisp 48 kHz",
        "",
        f"- Generated: {datetime.now().isoformat(timespec='seconds')}",
        f"- Chatterbox device: {device}",
        f"- CUDA GPU: {torch.cuda.get_device_name(0)}",
        f"- `nvidia-smi`: {run_text(['nvidia-smi', '--query-gpu=name,driver_version,memory.total', '--format=csv,noheader'])}",
        f"- Native model sample rate: {getattr(model, 'sr', 'unknown')} Hz",
        f"- Final mastered sample rate: {FINAL_SAMPLE_RATE} Hz",
        "- Final format: mono PCM_16 WAV, peak-normalized near -3 dBFS.",
        "- Mastering: SOXR 48 kHz resample plus light dialogue presence EQ.",
        f"- Output count: {len(results)} final WAV files",
        f"- Failed count: {len(failures)}",
        f"- Runtime seconds: {time.time() - started:.1f}",
        "- Scope: arrival dialogue only; Unity Assets/Audio was not modified.",
        "- Goal: classier, heavier British/RP accents and crisper dialogue.",
        f"- Desktop output folder: `{desktop_dir}`",
        "",
        "## Source References",
    ]
    for name, path in references.items():
        lines.append(f"- {name}: `{path}`")

    lines.extend(["", "## Review Fixes"])
    for guest, _mastering in specs:
        lines.append(f"- Guest {guest.guest:02d}: {REVIEW_NOTES[guest.guest]}")

    lines.extend(["", "## Final Files"])
    for guest, mastering, anchor_path, anchor_duration, raw_path, raw_duration, final_path, final_peak in results:
        lines.extend(
            [
                f"- Guest {guest.guest:02d} {guest.name} ({guest.gender})",
                f"  - line_id: `{guest.line_id}`",
                f"  - final: `{final_path.relative_to(run_dir)}`",
                f"  - desktop copy: `{desktop_dir / final_path.name}`",
                f"  - raw native: `{raw_path.relative_to(run_dir)}`",
                f"  - anchor native: `{anchor_path.relative_to(run_dir)}`",
                f"  - base prompt: `{guest.base_prompt}`",
                f"  - profile: {guest.profile}",
                f"  - text: \"{guest.arrival_text}\"",
                f"  - durations: anchor={anchor_duration:.2f}s, raw={raw_duration:.2f}s",
                f"  - final_peak_linear: {final_peak:.4f}",
                f"  - settings: seed={guest.seed}, exaggeration={guest.exaggeration}, "
                f"cfg_weight={guest.cfg_weight}, temperature={guest.temperature}, "
                f"master_pitch={mastering.pitch}, master_tempo={mastering.tempo}",
            ]
        )

    lines.extend(["", "## Failures"])
    if failures:
        lines.extend(f"- {failure}" for failure in failures)
    else:
        lines.append("- None")

    report.write_text("\n".join(lines) + "\n", encoding="utf-8")
    shutil.copy2(report, desktop_dir / "AUDITION_REPORT.md")

    print(f"RUN_DIR={run_dir}")
    print(f"FINAL_DIR={final_dir}")
    print(f"DESKTOP_DIR={desktop_dir}")
    print(f"REPORT={report}")
    return 1 if failures or len(results) != len(specs) else 0


if __name__ == "__main__":
    raise SystemExit(main())
