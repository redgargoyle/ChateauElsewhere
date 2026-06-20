#!/usr/bin/env python
from __future__ import annotations

import time
from dataclasses import replace
from datetime import datetime
from pathlib import Path

import torch

from generate_british_victorian_arrivals import (
    AUDITION_ROOT,
    GUESTS,
    generate_clip,
    pitch_master,
    safe_name,
)


REVIEW_NOTES = {
    1: "Clear woman, but not British and too old. Make younger, brighter, upper-class RP.",
    2: "Not British enough. Make unmistakably refined English.",
    3: "Good tone, but not British enough. Push theatrical RP harder.",
    4: "Pretty good. Keep close, slightly more aristocratic RP.",
    5: "Too slow/robotic, barely British. Make classy, smoother, less heavy.",
    6: "Good voice, but not British. Add refined English accent.",
    7: "Too low and slow, not British enough. Lift pitch and pace, keep haunted.",
    8: "Not British, too high. Lower and cool the voice, more aristocratic.",
}


def v2_guests():
    original = {guest.guest: guest for guest in GUESTS}
    return [
        replace(
            original[1],
            seed=5101,
            anchor_text=(
                "Quite so, Butler. I should rather like a proper fire, a glass of claret, "
                "and no more nonsense from this dreadful house."
            ),
            profile="Young upper-class English lady; clear, poised RP, sharper and less elderly.",
            pitch_factor=1.085,
            exaggeration=0.55,
            cfg_weight=0.32,
            temperature=0.86,
        ),
        replace(
            original[2],
            seed=5202,
            anchor_text=(
                "Thank you, my good man. The road was quite ghastly, and I should be grateful "
                "for a civil room and a properly made cup of tea."
            ),
            profile="Refined English gentleman; precise RP, polite, lightly nervous, clearly British.",
            pitch_factor=1.01,
            exaggeration=0.58,
            cfg_weight=0.30,
            temperature=0.86,
        ),
        replace(
            original[3],
            seed=5303,
            anchor_text=(
                "My dear Butler, how marvellously grim. One does adore a house with manners, "
                "provided it does not murder the vowels."
            ),
            profile="Theatrical English gentleman; witty, crisp RP, classier and more British.",
            pitch_factor=0.985,
            exaggeration=0.62,
            cfg_weight=0.30,
            temperature=0.86,
        ),
        replace(
            original[4],
            seed=5404,
            anchor_text=(
                "Good evening, Butler. Kindly take my cloak. The night has been vulgar, "
                "and I have no intention of matching it."
            ),
            profile="Countess; mature aristocratic English woman, severe, clipped RP.",
            pitch_factor=0.98,
            exaggeration=0.52,
            cfg_weight=0.34,
            temperature=0.80,
        ),
        replace(
            original[5],
            seed=5505,
            anchor_text=(
                "Good evening. Let us be sensible, shall we. A gentleman keeps his head, "
                "even when the architecture appears to have lost its own."
            ),
            profile="Classy English baron; smooth practical RP, less slow, less robotic, composed.",
            pitch_factor=0.955,
            exaggeration=0.56,
            cfg_weight=0.28,
            temperature=0.88,
        ),
        replace(
            original[6],
            seed=5606,
            anchor_text=(
                "Thank you. I confess I am rather shaken, though one ought not say so "
                "in a house with such attentive walls."
            ),
            profile="Lady Sabine; refined English woman, soft anxious RP, elegant and breath-held.",
            pitch_factor=1.015,
            exaggeration=0.60,
            cfg_weight=0.28,
            temperature=0.88,
        ),
        replace(
            original[7],
            seed=5707,
            anchor_text=(
                "Lovely to see you. The stones are listening tonight. Do speak softly, "
                "but do not let the house think us afraid."
            ),
            profile="Lord Ambrose; haunted English mystic, lifted from previous low/slow read.",
            pitch_factor=1.01,
            exaggeration=0.56,
            cfg_weight=0.27,
            temperature=0.86,
        ),
        replace(
            original[8],
            seed=5808,
            anchor_text=(
                "Good evening, Butler. Compose yourself. This house may posture all it likes; "
                "we shall answer with breeding."
            ),
            profile="Madame Coralie; lower commanding aristocratic English woman, clipped RP.",
            pitch_factor=0.925,
            exaggeration=0.54,
            cfg_weight=0.30,
            temperature=0.84,
        ),
    ]


def main() -> int:
    from chatterbox.tts import ChatterboxTTS

    guests = v2_guests()
    missing = [guest.base_prompt for guest in guests if not guest.base_prompt.exists()]
    if missing:
        for path in missing:
            print(f"MISSING BASE PROMPT: {path}")
        return 2

    if not torch.cuda.is_available():
        raise SystemExit("CUDA is required for this v2 audition pass.")

    stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    run_dir = AUDITION_ROOT / f"british_victorian_audition_v2_{stamp}"
    anchor_dir = run_dir / "generated_british_anchors"
    raw_dir = run_dir / "raw_chatterbox"
    final_dir = run_dir / "final_mastered"
    for path in (anchor_dir, raw_dir, final_dir):
        path.mkdir(parents=True, exist_ok=False)

    device = "cuda"
    started = time.time()
    print(f"Loading Chatterbox on {device}")
    model = ChatterboxTTS.from_pretrained(device=device)

    results = []
    for guest in guests:
        anchor_path = anchor_dir / f"Guest{guest.guest:02d}_{guest.name.replace(' ', '_')}_british_anchor_v2.wav"
        raw_path = raw_dir / safe_name(guest)
        final_path = final_dir / safe_name(guest)
        print(f"Generating v2 British anchor for Guest {guest.guest:02d} {guest.name}")
        anchor_duration = generate_clip(model, guest, guest.anchor_text, guest.base_prompt, anchor_path, 0)
        print(f"Generating v2 arrival line for Guest {guest.guest:02d} {guest.name}")
        raw_duration = generate_clip(model, guest, guest.arrival_text, anchor_path, raw_path, 100)
        pitch_master(raw_path, final_path, guest.pitch_factor)
        results.append((guest, anchor_path, anchor_duration, raw_path, raw_duration, final_path))

    report = run_dir / "AUDITION_REPORT.md"
    lines = [
        "# British Victorian Guest Arrival Audition V2",
        "",
        f"- Generated: {datetime.now().isoformat(timespec='seconds')}",
        f"- Chatterbox device: {device}",
        f"- CUDA GPU: {torch.cuda.get_device_name(0)}",
        f"- Output count: {len(results)} final WAV files",
        f"- Runtime seconds: {time.time() - started:.1f}",
        "- Scope: arrival dialogue only; Unity Assets/Audio was not modified.",
        "- Goal: heavier British/RP accents, classier voices, and fixes from the user review.",
        "",
        "## Review Fixes",
    ]
    for guest in guests:
        lines.append(f"- Guest {guest.guest:02d}: {REVIEW_NOTES[guest.guest]}")
    lines.extend(["", "## Final Files"])
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
                f"  - settings: seed={guest.seed}, exaggeration={guest.exaggeration}, "
                f"cfg_weight={guest.cfg_weight}, temperature={guest.temperature}, "
                f"pitch_factor={guest.pitch_factor}",
            ]
        )
    report.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"RUN_DIR={run_dir}")
    print(f"FINAL_DIR={final_dir}")
    print(f"REPORT={report}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
