#!/usr/bin/env python
from __future__ import annotations

import random
import subprocess
import time
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path

import numpy as np
import torch
import torchaudio


PROJECT_ROOT = Path(__file__).resolve().parents[1]
AUDITION_ROOT = PROJECT_ROOT / "Voice_Auditions_Guest_Arrivals"
BASE_PROMPT_ROOT = AUDITION_ROOT / "arrival_audition_20260619_135604_pitch_checked"
TARGET_PEAK = 10 ** (-3.0 / 20.0)
LEADING_PAUSE_SECONDS = 0.10
TRAILING_PAUSE_SECONDS = 0.16


@dataclass(frozen=True)
class GuestSpec:
    guest: int
    name: str
    gender: str
    line_id: str
    arrival_text: str
    anchor_text: str
    seed: int
    base_prompt: Path
    profile: str
    pitch_factor: float
    exaggeration: float
    cfg_weight: float
    temperature: float


GUESTS = [
    GuestSpec(
        1,
        "Lady",
        "woman",
        "CH1_G01_ENTRY",
        "Good evening. I trust the house remembers its manners better than the weather does.",
        "Good evening. One must observe the proper forms, even when the weather has taken leave of its senses.",
        4101,
        BASE_PROMPT_ROOT / "Guest01_Lady_CH1_G01_ENTRY.wav",
        "Upper-class English lady; dry, precise, severe, elegant restraint.",
        1.055,
        0.46,
        0.43,
        0.70,
    ),
    GuestSpec(
        2,
        "Butler Guest",
        "man",
        "CH1_G02_ENTRY",
        "Thank you. The drive was longer in the dark than I care to admit.",
        "Thank you. I should be obliged if we might enter before the night grows any more insistent.",
        4202,
        BASE_PROMPT_ROOT / "Guest02_Butler_Guest_CH1_G02_ENTRY.wav",
        "Refined nervous English gentleman; gentle, polite, slightly breathless.",
        1.000,
        0.50,
        0.42,
        0.72,
    ),
    GuestSpec(
        3,
        "Mister Florian Knell",
        "man",
        "CH1_G03_ENTRY",
        "Lovely to see you, dear Butler. Tell me, are we late, early, or merely dramatic?",
        "Lovely to see you, my dear fellow. Are we early, late, or merely arriving with theatrical precision?",
        4303,
        BASE_PROMPT_ROOT / "Guest03_Mister_Florian_Knell_CH1_G03_ENTRY.wav",
        "Theatrical English gentleman; witty, bright, crisp comic timing.",
        0.965,
        0.56,
        0.39,
        0.76,
    ),
    GuestSpec(
        4,
        "Countess Elowen Dusk",
        "woman",
        "CH1_G04_ENTRY",
        "Good evening, Butler. The road up here has the cheerful shape of a warning.",
        "Good evening, Butler. Pray take care; the road has been making omens of itself all evening.",
        4404,
        BASE_PROMPT_ROOT / "Guest04_Countess_Elowen_Dusk_CH1_G04_ENTRY.wav",
        "Countess; older aristocratic English woman, smoky, severe, unhurried.",
        0.980,
        0.48,
        0.42,
        0.70,
    ),
    GuestSpec(
        5,
        "Baron Hector Glass",
        "man",
        "CH1_G05_ENTRY",
        "Good evening. I hope the evening has not started without us.",
        "Good evening. Let us conduct ourselves sensibly; panic is seldom improved by bad posture.",
        4505,
        BASE_PROMPT_ROOT / "Guest05_Baron_Hector_Glass_CH1_G05_ENTRY.wav",
        "Baron; composed English man, practical, resonant, protective.",
        0.860,
        0.44,
        0.44,
        0.68,
    ),
    GuestSpec(
        6,
        "Lady Sabine Marrow",
        "woman",
        "CH1_G06_ENTRY",
        "Thank you. I nearly mistook the bell pull for a funeral cord.",
        "Thank you. I confess the bell pull looked rather too much like an invitation to a funeral.",
        4606,
        BASE_PROMPT_ROOT / "Guest06_Lady_Sabine_Marrow_CH1_G06_ENTRY.wav",
        "Lady Sabine; soft, anxious, refined English woman, breath held under politeness.",
        1.000,
        0.54,
        0.39,
        0.74,
    ),
    GuestSpec(
        7,
        "Lord Ambrose Veil",
        "man",
        "CH1_G07_ENTRY",
        "Lovely to see you. The chateau looks almost awake tonight.",
        "Lovely to see you. The stones have a wakeful look tonight, as though the house were listening.",
        4707,
        BASE_PROMPT_ROOT / "Guest07_Lord_Ambrose_Veil_CH1_G07_ENTRY.wav",
        "Lord Ambrose; haunted English mystic, quiet, intense, inward.",
        0.875,
        0.50,
        0.36,
        0.72,
    ),
    GuestSpec(
        8,
        "Madame Coralie Thread",
        "woman",
        "CH1_G08_ENTRY",
        "Good evening, Butler. I see the house has chosen its most severe face.",
        "Good evening, Butler. The house has dressed itself in severity; one must answer in kind.",
        4808,
        BASE_PROMPT_ROOT / "Guest08_Madame_Coralie_Thread_CH1_G08_ENTRY.wav",
        "Madame Coralie; clipped aristocratic English woman, commanding and cool.",
        1.040,
        0.46,
        0.42,
        0.70,
    ),
]


def set_seed(seed: int) -> None:
    random.seed(seed)
    np.random.seed(seed)
    torch.manual_seed(seed)
    if torch.cuda.is_available():
        torch.cuda.manual_seed_all(seed)


def normalize_with_pauses(wav: torch.Tensor, sample_rate: int) -> torch.Tensor:
    if wav.dim() == 1:
        wav = wav.unsqueeze(0)
    wav = wav.detach().cpu().float()
    peak = wav.abs().max().item()
    if peak > 0:
        wav = wav * min(TARGET_PEAK / peak, 24.0)
    leading = torch.zeros((wav.shape[0], int(sample_rate * LEADING_PAUSE_SECONDS)))
    trailing = torch.zeros((wav.shape[0], int(sample_rate * TRAILING_PAUSE_SECONDS)))
    return torch.cat([leading, wav.clamp(-0.999, 0.999), trailing], dim=1)


def safe_name(guest: GuestSpec) -> str:
    return f"Guest{guest.guest:02d}_{guest.name.replace(' ', '_')}_{guest.line_id}.wav"


def generate_clip(model, guest: GuestSpec, text: str, prompt: Path, output: Path, seed_offset: int) -> float:
    set_seed(guest.seed + seed_offset)
    wav = model.generate(
        text,
        audio_prompt_path=str(prompt),
        exaggeration=guest.exaggeration,
        cfg_weight=guest.cfg_weight,
        temperature=guest.temperature,
        repetition_penalty=1.18,
        min_p=0.045,
        top_p=0.92,
    )
    wav = normalize_with_pauses(wav, model.sr)
    torchaudio.save(str(output), wav, model.sr)
    return wav.shape[-1] / model.sr


def pitch_master(input_path: Path, output_path: Path, pitch_factor: float) -> None:
    if abs(pitch_factor - 1.0) < 0.001:
        subprocess.run(
            ["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", str(input_path), "-c:a", "pcm_s16le", str(output_path)],
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
            f"rubberband=pitch={pitch_factor}",
            "-c:a",
            "pcm_s16le",
            str(output_path),
        ],
        check=True,
    )


def main() -> int:
    from chatterbox.tts import ChatterboxTTS

    missing = [guest.base_prompt for guest in GUESTS if not guest.base_prompt.exists()]
    if missing:
        for path in missing:
            print(f"MISSING BASE PROMPT: {path}")
        return 2

    stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    run_dir = AUDITION_ROOT / f"british_victorian_audition_{stamp}"
    anchor_dir = run_dir / "generated_british_anchors"
    raw_dir = run_dir / "raw_chatterbox"
    final_dir = run_dir / "final_mastered"
    for path in (anchor_dir, raw_dir, final_dir):
        path.mkdir(parents=True, exist_ok=False)

    if not torch.cuda.is_available():
        raise SystemExit(
            "CUDA is required for this audition pass, but torch.cuda.is_available() is false. "
            "Restart Codex with GPU device access before running again."
        )
    device = "cuda"
    started = time.time()
    print(f"Loading Chatterbox on {device}")
    model = ChatterboxTTS.from_pretrained(device=device)

    results = []
    for guest in GUESTS:
        anchor_path = anchor_dir / f"Guest{guest.guest:02d}_{guest.name.replace(' ', '_')}_british_anchor.wav"
        raw_path = raw_dir / safe_name(guest)
        final_path = final_dir / safe_name(guest)
        print(f"Generating British anchor for Guest {guest.guest:02d} {guest.name}")
        anchor_duration = generate_clip(model, guest, guest.anchor_text, guest.base_prompt, anchor_path, 0)
        print(f"Generating arrival line for Guest {guest.guest:02d} {guest.name}")
        raw_duration = generate_clip(model, guest, guest.arrival_text, anchor_path, raw_path, 100)
        pitch_master(raw_path, final_path, guest.pitch_factor)
        results.append((guest, anchor_path, anchor_duration, raw_path, raw_duration, final_path))

    report = run_dir / "AUDITION_REPORT.md"
    lines = [
        "# British Victorian Guest Arrival Audition",
        "",
        f"- Generated: {datetime.now().isoformat(timespec='seconds')}",
        f"- Chatterbox device: {device}",
        f"- Output count: {len(results)} final WAV files",
        f"- Runtime seconds: {time.time() - started:.1f}",
        "- Scope: arrival dialogue only; Unity Assets/Audio was not modified.",
        "- Method: previous approved diverse audition voice -> generated British/Victorian anchor -> final arrival line.",
        "",
        "## Final Files",
    ]
    for guest, anchor_path, anchor_duration, raw_path, raw_duration, final_path in results:
        lines.extend(
            [
                f"- Guest {guest.guest:02d} {guest.name} ({guest.gender})",
                f"  - line_id: `{guest.line_id}`",
                f"  - final: `{final_path.relative_to(run_dir)}`",
                f"  - raw: `{raw_path.relative_to(run_dir)}`",
                f"  - anchor: `{anchor_path.relative_to(run_dir)}`",
                f"  - base prompt: `{guest.base_prompt}`",
                f"  - profile: {guest.profile}",
                f"  - durations: anchor={anchor_duration:.2f}s, raw={raw_duration:.2f}s",
                f"  - settings: seed={guest.seed}, exaggeration={guest.exaggeration}, cfg_weight={guest.cfg_weight}, temperature={guest.temperature}, pitch_factor={guest.pitch_factor}",
            ]
        )
    report.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"RUN_DIR={run_dir}")
    print(f"FINAL_DIR={final_dir}")
    print(f"REPORT={report}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
