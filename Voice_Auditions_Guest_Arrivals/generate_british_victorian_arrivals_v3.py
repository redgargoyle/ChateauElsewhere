#!/usr/bin/env python
from __future__ import annotations

import shutil
import subprocess
import time
from dataclasses import dataclass, replace
from datetime import datetime
from pathlib import Path

import torch

from generate_british_victorian_arrivals import (
    AUDITION_ROOT,
    GUESTS,
    generate_clip,
    safe_name,
)


@dataclass(frozen=True)
class Mastering:
    pitch: float = 1.0
    tempo: float = 1.0


REVIEW_NOTES = {
    1: "Still not British; make young upper-class RP, not elderly.",
    2: "Southern accent; replace with refined English RP.",
    3: "V1 was better; restore theatrical character while increasing British accent.",
    4: "Good; preserve and lightly polish.",
    5: "Pretty good; preserve classy direction.",
    6: "Bad and not British; rebuild from female RP anchor.",
    7: "Too low/slow; lift pitch, increase tempo, avoid Darth Vader read.",
    8: "American accent; rebuild from female RP anchor with lower aristocratic tone.",
}


def latest_run(prefix: str) -> Path:
    matches = sorted(
        [path for path in AUDITION_ROOT.glob(f"{prefix}_*/final_mastered") if path.is_dir()],
        key=lambda path: path.stat().st_mtime,
    )
    if not matches:
        raise FileNotFoundError(f"No prior run found for {prefix}_*/final_mastered")
    return matches[-1]


def master_audio(input_path: Path, output_path: Path, mastering: Mastering) -> None:
    if abs(mastering.pitch - 1.0) < 0.001 and abs(mastering.tempo - 1.0) < 0.001:
        subprocess.run(
            [
                "ffmpeg",
                "-hide_banner",
                "-loglevel",
                "error",
                "-y",
                "-i",
                str(input_path),
                "-c:a",
                "pcm_s16le",
                str(output_path),
            ],
            check=True,
        )
        return

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
            (
                f"rubberband=pitch={mastering.pitch}:tempo={mastering.tempo}:"
                "formant=preserved:pitchq=quality"
            ),
            "-c:a",
            "pcm_s16le",
            str(output_path),
        ],
        check=True,
    )


def v3_specs():
    original = {guest.guest: guest for guest in GUESTS}
    v1_dir = AUDITION_ROOT / "arrival_audition_20260619_135604_pitch_checked"
    v2_dir = latest_run("british_victorian_audition_v2")

    female_rp_anchor = v2_dir / "Guest04_Countess_Elowen_Dusk_CH1_G04_ENTRY.wav"
    male_rp_anchor = v2_dir / "Guest05_Baron_Hector_Glass_CH1_G05_ENTRY.wav"
    g3_v1_reference = v1_dir / "Guest03_Mister_Florian_Knell_CH1_G03_ENTRY.wav"

    specs = [
        (
            replace(
                original[1],
                seed=6101,
                base_prompt=female_rp_anchor,
                anchor_text=(
                    "Good evening, Butler. I should be frightfully obliged for a proper fire, "
                    "and I shan't be bullied by a house with airs above its station."
                ),
                profile="Young upper-class English lady; bright, crisp, unmistakable RP.",
                exaggeration=0.68,
                cfg_weight=0.22,
                temperature=0.93,
            ),
            Mastering(pitch=1.115, tempo=1.06),
        ),
        (
            replace(
                original[2],
                seed=6202,
                base_prompt=male_rp_anchor,
                anchor_text=(
                    "Thank you, Butler. The road was ghastly, quite ghastly, and I should like "
                    "a civil room before this damp murders my vowels."
                ),
                profile="Refined English gentleman; no southern cadence, careful RP.",
                exaggeration=0.66,
                cfg_weight=0.22,
                temperature=0.92,
            ),
            Mastering(pitch=1.035, tempo=1.05),
        ),
        (
            replace(
                original[3],
                seed=6303,
                base_prompt=g3_v1_reference,
                anchor_text=(
                    "My dear Butler, how deliciously ominous. I adore a dreadful house, "
                    "provided it has the decency to sound properly English."
                ),
                profile="Theatrical English gentleman; closer to v1, sharper RP.",
                exaggeration=0.72,
                cfg_weight=0.20,
                temperature=0.94,
            ),
            Mastering(pitch=0.985, tempo=1.06),
        ),
        (
            replace(
                original[4],
                seed=6404,
                base_prompt=female_rp_anchor,
                anchor_text=(
                    "Good evening, Butler. Kindly take my cloak. The night has behaved abominably, "
                    "and I refuse to let it lower the tone."
                ),
                profile="Countess; preserve v2, polished aristocratic RP.",
                exaggeration=0.56,
                cfg_weight=0.28,
                temperature=0.84,
            ),
            Mastering(pitch=0.985, tempo=1.02),
        ),
        (
            replace(
                original[5],
                seed=6505,
                base_prompt=male_rp_anchor,
                anchor_text=(
                    "Good evening. Let us keep our heads, shall we. A gentleman need not shout "
                    "in order to be obeyed."
                ),
                profile="Classy English baron; preserve v2, smoother and less robotic.",
                exaggeration=0.58,
                cfg_weight=0.26,
                temperature=0.88,
            ),
            Mastering(pitch=0.955, tempo=1.07),
        ),
        (
            replace(
                original[6],
                seed=6606,
                base_prompt=female_rp_anchor,
                anchor_text=(
                    "Thank you. I am perfectly composed, of course, though the bell pull looked "
                    "rather too much like a funeral invitation."
                ),
                profile="Lady Sabine; soft anxious refined English woman with strong RP.",
                exaggeration=0.70,
                cfg_weight=0.20,
                temperature=0.94,
            ),
            Mastering(pitch=1.045, tempo=1.05),
        ),
        (
            replace(
                original[7],
                seed=6707,
                base_prompt=male_rp_anchor,
                anchor_text=(
                    "Lovely to see you. The stones are listening tonight, but we shall speak "
                    "with breeding and give them nothing coarse to remember."
                ),
                profile="Lord Ambrose; haunted but articulate, higher and faster than v2.",
                exaggeration=0.62,
                cfg_weight=0.22,
                temperature=0.92,
            ),
            Mastering(pitch=1.075, tempo=1.18),
        ),
        (
            replace(
                original[8],
                seed=6808,
                base_prompt=female_rp_anchor,
                anchor_text=(
                    "Good evening, Butler. Compose yourself. The house may posture and preen, "
                    "but breeding is the sharper weapon."
                ),
                profile="Madame Coralie; lower, cool aristocratic RP, not American.",
                exaggeration=0.64,
                cfg_weight=0.22,
                temperature=0.90,
            ),
            Mastering(pitch=0.895, tempo=1.04),
        ),
    ]
    return specs, female_rp_anchor, male_rp_anchor


def main() -> int:
    from chatterbox.tts import ChatterboxTTS

    specs, female_rp_anchor, male_rp_anchor = v3_specs()
    missing = [guest.base_prompt for guest, _ in specs if not guest.base_prompt.exists()]
    if missing:
        for path in missing:
            print(f"MISSING BASE PROMPT: {path}")
        return 2

    if not torch.cuda.is_available():
        raise SystemExit("CUDA is required for this v3 audition pass.")

    stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    run_dir = AUDITION_ROOT / f"british_victorian_audition_v3_{stamp}"
    anchor_dir = run_dir / "generated_british_anchors"
    raw_dir = run_dir / "raw_chatterbox"
    final_dir = run_dir / "final_mastered"
    desktop_dir = Path.home() / "Desktop" / f"British_Victorian_Guest_Arrivals_V3_{stamp}"
    for path in (anchor_dir, raw_dir, final_dir, desktop_dir):
        path.mkdir(parents=True, exist_ok=False)

    device = "cuda"
    started = time.time()
    print(f"Loading Chatterbox on {device}")
    model = ChatterboxTTS.from_pretrained(device=device)

    results = []
    for guest, mastering in specs:
        anchor_path = anchor_dir / f"Guest{guest.guest:02d}_{guest.name.replace(' ', '_')}_british_anchor_v3.wav"
        raw_path = raw_dir / safe_name(guest)
        final_path = final_dir / safe_name(guest)
        print(f"Generating v3 British anchor for Guest {guest.guest:02d} {guest.name}")
        anchor_duration = generate_clip(model, guest, guest.anchor_text, guest.base_prompt, anchor_path, 0)
        print(f"Generating v3 arrival line for Guest {guest.guest:02d} {guest.name}")
        raw_duration = generate_clip(model, guest, guest.arrival_text, anchor_path, raw_path, 100)
        master_audio(raw_path, final_path, mastering)
        shutil.copy2(final_path, desktop_dir / final_path.name)
        results.append((guest, mastering, anchor_path, anchor_duration, raw_path, raw_duration, final_path))

    report = run_dir / "AUDITION_REPORT.md"
    lines = [
        "# British Victorian Guest Arrival Audition V3",
        "",
        f"- Generated: {datetime.now().isoformat(timespec='seconds')}",
        f"- Chatterbox device: {device}",
        f"- CUDA GPU: {torch.cuda.get_device_name(0)}",
        f"- Output count: {len(results)} final WAV files",
        f"- Runtime seconds: {time.time() - started:.1f}",
        "- Scope: arrival dialogue only; Unity Assets/Audio was not modified.",
        "- Goal: heavy British/RP accents and classier voices.",
        f"- Female RP source anchor: `{female_rp_anchor}`",
        f"- Male RP source anchor: `{male_rp_anchor}`",
        "",
        "## Review Fixes",
    ]
    for guest, _mastering in specs:
        lines.append(f"- Guest {guest.guest:02d}: {REVIEW_NOTES[guest.guest]}")
    lines.extend(["", "## Final Files"])
    for guest, mastering, anchor_path, anchor_duration, raw_path, raw_duration, final_path in results:
        lines.extend(
            [
                f"- Guest {guest.guest:02d} {guest.name} ({guest.gender})",
                f"  - line_id: `{guest.line_id}`",
                f"  - final: `{final_path.relative_to(run_dir)}`",
                f"  - desktop copy: `{desktop_dir / final_path.name}`",
                f"  - raw: `{raw_path.relative_to(run_dir)}`",
                f"  - anchor: `{anchor_path.relative_to(run_dir)}`",
                f"  - base prompt: `{guest.base_prompt}`",
                f"  - profile: {guest.profile}",
                f"  - durations: anchor={anchor_duration:.2f}s, raw={raw_duration:.2f}s",
                f"  - settings: seed={guest.seed}, exaggeration={guest.exaggeration}, "
                f"cfg_weight={guest.cfg_weight}, temperature={guest.temperature}, "
                f"master_pitch={mastering.pitch}, master_tempo={mastering.tempo}",
            ]
        )
    report.write_text("\n".join(lines) + "\n", encoding="utf-8")
    shutil.copy2(report, desktop_dir / "AUDITION_REPORT.md")

    print(f"RUN_DIR={run_dir}")
    print(f"FINAL_DIR={final_dir}")
    print(f"DESKTOP_DIR={desktop_dir}")
    print(f"REPORT={report}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
