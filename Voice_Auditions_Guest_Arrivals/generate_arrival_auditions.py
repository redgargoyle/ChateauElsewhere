#!/usr/bin/env python
from __future__ import annotations

import random
import time
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path

import numpy as np
import torch
import torchaudio


PROJECT_ROOT = Path(__file__).resolve().parents[1]
AUDITION_ROOT = PROJECT_ROOT / "Voice_Auditions_Guest_Arrivals"
TARGET_PEAK = 10 ** (-3.0 / 20.0)
LEADING_PAUSE_SECONDS = 0.08
TRAILING_PAUSE_SECONDS = 0.14


@dataclass(frozen=True)
class GuestAudition:
    guest: int
    name: str
    gender: str
    line_id: str
    text: str
    seed: int
    prompt_path: Path
    profile: str
    exaggeration: float = 0.50
    cfg_weight: float = 0.38
    temperature: float = 0.78


AUDITIONS = [
    GuestAudition(
        1,
        "Lady",
        "woman",
        "CH1_G01_ENTRY",
        "Good evening. I trust the house remembers its manners better than the weather does.",
        3101,
        Path("/home/hamzak/Desktop/Chantilly_Audio_Library/01_guest_persona_panic/guest_03_nina/guest_03_nina_calm_professional_woman_restrained_fear_turning_brittle_variant_1_seed9155.wav"),
        "Severe upper-class English lady; controlled, dry, exacting.",
        exaggeration=0.46,
        cfg_weight=0.35,
        temperature=0.76,
    ),
    GuestAudition(
        2,
        "Butler Guest",
        "man",
        "CH1_G02_ENTRY",
        "Thank you. The drive was longer in the dark than I care to admit.",
        3202,
        Path("/home/hamzak/Desktop/Chantilly_Audio_Library/01_guest_persona_panic/guest_04_eli/guest_04_eli_young_nervous_man_quick_high_anxious_reactions_variant_1_seed10155.wav"),
        "Refined nervous English gentleman; polite, soft-edged, slightly breathless.",
        exaggeration=0.54,
        cfg_weight=0.36,
        temperature=0.80,
    ),
    GuestAudition(
        3,
        "Mister Florian Knell",
        "man",
        "CH1_G03_ENTRY",
        "Lovely to see you, dear Butler. Tell me, are we late, early, or merely dramatic?",
        3303,
        Path("/home/hamzak/Desktop/Chantilly_Audio_Library/01_guest_persona_panic/guest_06_victor/guest_06_victor_angry_middle_aged_man_panic_masked_as_aggression_variant_1_seed12155.wav"),
        "Theatrical English gentleman; witty, bright, dramatic over real unease.",
        exaggeration=0.58,
        cfg_weight=0.34,
        temperature=0.84,
    ),
    GuestAudition(
        4,
        "Countess Elowen Dusk",
        "woman",
        "CH1_G04_ENTRY",
        "Good evening, Butler. The road up here has the cheerful shape of a warning.",
        3404,
        Path("/home/hamzak/Desktop/atmospheric/guest_05_rosa_older_woman_smoky_hoarse_voice_with_grief_under_panic_variant_1_seed11155.wav"),
        "Countess; older aristocratic woman, low, smoky, severe.",
        exaggeration=0.50,
        cfg_weight=0.34,
        temperature=0.77,
    ),
    GuestAudition(
        5,
        "Baron Hector Glass",
        "man",
        "CH1_G05_ENTRY",
        "Good evening. I hope the evening has not started without us.",
        3505,
        Path("/home/hamzak/Desktop/Chantilly_Audio_Library/01_guest_persona_panic/guest_02_marcus/guest_02_marcus_large_older_man_deep_controlled_voice_losing_composure_variant_1_seed8155.wav"),
        "Baron; deep, practical, composed English man.",
        exaggeration=0.46,
        cfg_weight=0.37,
        temperature=0.75,
    ),
    GuestAudition(
        6,
        "Lady Sabine Marrow",
        "woman",
        "CH1_G06_ENTRY",
        "Thank you. I nearly mistook the bell pull for a funeral cord.",
        3606,
        Path("/home/hamzak/Desktop/Chantilly_Audio_Library/01_guest_persona_panic/guest_07_maya/guest_07_maya_soft_spoken_woman_small_breathy_fear_escalating_variant_1_seed13155.wav"),
        "Lady Sabine; soft, anxious, breathy, refined.",
        exaggeration=0.56,
        cfg_weight=0.33,
        temperature=0.82,
    ),
    GuestAudition(
        7,
        "Lord Ambrose Veil",
        "man",
        "CH1_G07_ENTRY",
        "Lovely to see you. The chateau looks almost awake tonight.",
        3707,
        Path("/home/hamzak/Desktop/Chantilly_Audio_Library/01_guest_persona_panic/guest_08_owen/guest_08_owen_older_wiry_man_nasal_tense_voice_and_ragged_breath_variant_1_seed14155.wav"),
        "Lord Ambrose; haunted, quiet, wiry, mystic restraint.",
        exaggeration=0.52,
        cfg_weight=0.32,
        temperature=0.80,
    ),
    GuestAudition(
        8,
        "Madame Coralie Thread",
        "woman",
        "CH1_G08_ENTRY",
        "Good evening, Butler. I see the house has chosen its most severe face.",
        3808,
        Path("/home/hamzak/Desktop/atmosphere/guest_01_ava_high_strung_young_woman_sharp_bright_voice_variant_3_seed7229.wav"),
        "Madame Coralie; clipped, sharp, commanding society woman.",
        exaggeration=0.48,
        cfg_weight=0.34,
        temperature=0.78,
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
        wav = wav * min(TARGET_PEAK / peak, 32.0)
    leading = torch.zeros((wav.shape[0], int(sample_rate * LEADING_PAUSE_SECONDS)))
    trailing = torch.zeros((wav.shape[0], int(sample_rate * TRAILING_PAUSE_SECONDS)))
    return torch.cat([leading, wav.clamp(-0.999, 0.999), trailing], dim=1)


def output_name(audition: GuestAudition) -> str:
    safe_name = audition.name.replace(" ", "_")
    return f"Guest{audition.guest:02d}_{safe_name}_{audition.line_id}.wav"


def main() -> int:
    from chatterbox.tts import ChatterboxTTS

    stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    run_dir = AUDITION_ROOT / f"arrival_audition_{stamp}"
    run_dir.mkdir(parents=True, exist_ok=False)

    missing = [a for a in AUDITIONS if not a.prompt_path.exists()]
    if missing:
        for audition in missing:
            print(f"MISSING PROMPT: Guest {audition.guest}: {audition.prompt_path}")
        return 2

    device = "cuda" if torch.cuda.is_available() else "cpu"
    started = time.time()
    print(f"Loading Chatterbox on {device}")
    model = ChatterboxTTS.from_pretrained(device=device)

    generated = []
    for audition in AUDITIONS:
        set_seed(audition.seed)
        out_path = run_dir / output_name(audition)
        print(f"Generating Guest {audition.guest:02d} {audition.name}: {out_path.name}")
        wav = model.generate(
            audition.text,
            audio_prompt_path=str(audition.prompt_path),
            exaggeration=audition.exaggeration,
            cfg_weight=audition.cfg_weight,
            temperature=audition.temperature,
        )
        wav = normalize_with_pauses(wav, model.sr)
        torchaudio.save(str(out_path), wav, model.sr)
        duration = wav.shape[-1] / model.sr
        generated.append((audition, out_path, duration))

    report = run_dir / "AUDITION_REPORT.md"
    lines = [
        "# Guest Arrival Voice Audition",
        "",
        f"- Generated: {datetime.now().isoformat(timespec='seconds')}",
        f"- Chatterbox device: {device}",
        f"- Output count: {len(generated)} WAV files",
        f"- Runtime seconds: {time.time() - started:.1f}",
        "- Scope: arrival dialogue only; Unity Assets/Audio was not modified.",
        "",
        "## Files",
    ]
    for audition, out_path, duration in generated:
        lines.extend(
            [
                f"- Guest {audition.guest:02d} {audition.name} ({audition.gender})",
                f"  - line_id: `{audition.line_id}`",
                f"  - file: `{out_path.name}`",
                f"  - duration: {duration:.2f}s",
                f"  - prompt: `{audition.prompt_path}`",
                f"  - profile: {audition.profile}",
                f"  - settings: seed={audition.seed}, exaggeration={audition.exaggeration}, cfg_weight={audition.cfg_weight}, temperature={audition.temperature}",
            ]
        )
    report.write_text("\n".join(lines) + "\n", encoding="utf-8")

    print(f"RUN_DIR={run_dir}")
    print(f"REPORT={report}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
