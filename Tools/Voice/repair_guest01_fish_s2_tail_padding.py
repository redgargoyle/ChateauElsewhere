#!/usr/bin/env python3
"""Repair Guest 1 Fish S2 WAV endings by adding a natural post-roll.

This does not regenerate speech. It remasters the latest Fish S2 Guest 1 raw
outputs to 44.1 kHz mono PCM_16, peak-normalizes them, adds a short quiet tail
so Unity playback does not feel clipped, backs up Guest01, and replaces Guest01
only.
"""

from __future__ import annotations

import shutil
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path

import numpy as np
import soundfile as sf
import soxr


PROJECT_ROOT = Path("/home/hamzak/Desktop/ChataeuChantilly")
VOICE_ROOT = PROJECT_ROOT / "Tools" / "Voice"
GENERATED_ROOT = VOICE_ROOT / "generated_fish_s2_guest_dialogue"
BACKUP_ROOT = VOICE_ROOT / "backups"
REPORT_ROOT = VOICE_ROOT / "reports"
ASSET_DIR = PROJECT_ROOT / "Assets" / "Audio" / "Voice" / "Guests" / "Guest01"
FINAL_SR = 44100
TARGET_PEAK = 10 ** (-3.0 / 20.0)
BASE_TAIL_SECONDS = 0.60
LONG_TAIL_SECONDS = 0.85
TAIL_ANALYSIS_SECONDS = 0.12
FADE_SECONDS = 0.020


@dataclass(frozen=True)
class DialogueLine:
    line_id: str
    text: str

    @property
    def filename(self) -> str:
        return f"{self.line_id}.wav"


LINES = [
    DialogueLine("CH1_G01_ENTRY", "Good evening. I trust the house remembers its manners better than the weather does."),
    DialogueLine("CH1_G01_DELAYED", "We were beginning to wonder if anyone was home."),
    DialogueLine("CH1_G01_COAT_HANDOFF", "Careful with the collar, if you please. It has survived worse evenings than this one."),
    DialogueLine("CH1_G01_TO_DRAWING_ROOM", "A proper house is judged by its wardrobe first. So far, Chateau Chantilly remains under review."),
    DialogueLine("CH1_G01_AMBIENT_01", "This house is colder than I expected."),
    DialogueLine("CH1_G01_AMBIENT_02", "The fire looks arranged rather than lit."),
    DialogueLine("CH1_G01_EMPTY_BELL_REACTION", "Then who, precisely, rang?"),
    DialogueLine("CH2_G01_PRESPEECH_BARK", "Do begin, Butler. Formality is all that stands between dinner and nonsense."),
    DialogueLine("CH2_G01_PANIC", "Do not run! Do not—oh Lord, run!"),
    DialogueLine("CH2_G01_FOUND_START", "Announce yourself before I die of manners."),
    DialogueLine("CH2_G01_FOUND_REPLY", "You may record whatever prevents further surprises."),
    DialogueLine("CH2_G01_MEAL_PLINK", "The fresh monte genellion de plink. If one must face horrors, one should do it properly fed."),
    DialogueLine("CH2_G01_MEAL_THYME", "Thyme with Lillums. It sounds disciplined, and discipline is wanted tonight."),
    DialogueLine("CH2_G01_SMOKE_CIGAR", "A cigar. Something with authority."),
    DialogueLine("CH2_G01_SMOKE_PIPE", "A pipe. Slower nerves make better decisions."),
    DialogueLine("CH2_G01_SMOKE_NONE", "No smoke. I should like my lungs available for any further screaming."),
    DialogueLine("CH2_G01_SPIRITS_REPLY", "See that it is not shy."),
    DialogueLine("CH2_G01_EXIT_TO_DINING", "Very good. I shall present myself in the Dining Room and recover what dignity remains to us."),
    DialogueLine("CH2_G01_CLOCK_REACTION", "Seven o’clock. At least the clock is still obedient."),
    DialogueLine("CH2_G01_DINING_REVEAL", "Civilization survives another minute."),
]


def latest_raw_dir() -> Path:
    candidates = sorted(
        path / "raw_native"
        for path in GENERATED_ROOT.glob("guest01_fish_s2_full_dialogue_*")
        if (path / "raw_native").is_dir()
    )
    if not candidates:
        raise FileNotFoundError(f"No Fish S2 Guest 1 raw_native folders under {GENERATED_ROOT}")
    return candidates[-1]


def load_mono_delivery_rate(path: Path) -> np.ndarray:
    data, sample_rate = sf.read(path, always_2d=True, dtype="float32")
    mono = np.nan_to_num(data.mean(axis=1), nan=0.0, posinf=0.0, neginf=0.0)
    if sample_rate != FINAL_SR:
        mono = soxr.resample(mono, sample_rate, FINAL_SR, quality="VHQ")
    return mono.astype(np.float32, copy=False)


def tail_peak(audio: np.ndarray) -> float:
    samples = min(audio.size, int(FINAL_SR * TAIL_ANALYSIS_SECONDS))
    if samples <= 0:
        return 0.0
    return float(np.max(np.abs(audio[-samples:])))


def add_post_roll(audio: np.ndarray) -> tuple[np.ndarray, float, float]:
    before_tail_peak = tail_peak(audio)
    tail_seconds = LONG_TAIL_SECONDS if before_tail_peak > 0.035 else BASE_TAIL_SECONDS

    fade_samples = min(audio.size, max(1, int(FINAL_SR * FADE_SECONDS)))
    repaired = audio.copy()
    repaired[-fade_samples:] *= np.linspace(1.0, 0.0, fade_samples, dtype=np.float32)

    silence = np.zeros(int(FINAL_SR * tail_seconds), dtype=np.float32)
    repaired = np.concatenate([repaired, silence])

    peak = float(np.max(np.abs(repaired))) if repaired.size else 0.0
    if peak > 0:
        repaired *= min(TARGET_PEAK / peak, 32.0)
    repaired = np.clip(repaired, -0.999, 0.999)
    return repaired.astype(np.float32, copy=False), before_tail_peak, tail_seconds


def write_wav(path: Path, audio: np.ndarray) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    sf.write(path, audio, FINAL_SR, subtype="PCM_16")


def validate_dir(path: Path) -> None:
    files = sorted(item.name for item in path.glob("*.wav"))
    expected = sorted(line.filename for line in LINES)
    if files != expected:
        raise RuntimeError("Guest01 WAV set does not match expected file list")
    for item in path.glob("*.wav"):
        info = sf.info(item)
        if info.samplerate != FINAL_SR or info.channels != 1 or info.subtype != "PCM_16":
            raise RuntimeError(f"{item} is not {FINAL_SR} Hz mono PCM_16")


def backup_assets(timestamp: str) -> Path:
    BACKUP_ROOT.mkdir(parents=True, exist_ok=True)
    backup_dir = BACKUP_ROOT / f"guest01_before_fish_s2_tail_repair_{timestamp}"
    shutil.copytree(ASSET_DIR, backup_dir)
    return backup_dir


def copy_to_assets(final_dir: Path) -> None:
    ASSET_DIR.mkdir(parents=True, exist_ok=True)
    for line in LINES:
        shutil.copy2(final_dir / line.filename, ASSET_DIR / line.filename)


def main() -> None:
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    raw_dir = latest_raw_dir()
    run_dir = GENERATED_ROOT / f"guest01_fish_s2_tail_repair_{timestamp}"
    final_dir = run_dir / "final_44k1_tail_repaired"
    rows: list[tuple[str, float, float, float, float]] = []

    for line in LINES:
        source = raw_dir / line.filename
        if not source.exists():
            raise FileNotFoundError(source)
        audio = load_mono_delivery_rate(source)
        repaired, before_tail_peak, tail_seconds = add_post_roll(audio)
        write_wav(final_dir / line.filename, repaired)
        rows.append(
            (
                line.filename,
                len(audio) / FINAL_SR,
                len(repaired) / FINAL_SR,
                before_tail_peak,
                tail_seconds,
            )
        )

    validate_dir(final_dir)
    backup_dir = backup_assets(timestamp)
    copy_to_assets(final_dir)
    validate_dir(ASSET_DIR)

    report_path = REPORT_ROOT / "guest01_fish_s2_tail_repair_report.md"
    report = [
        "# Guest 1 Fish S2 Tail Repair Report",
        "",
        "Remastered Guest 1 only from the Fish S2 raw outputs. No speech was regenerated.",
        "Unity gameplay, subtitles, and playback hooks were not modified.",
        "",
        f"- Started: {timestamp}",
        f"- Raw source folder: `{raw_dir}`",
        f"- Tail-repaired output folder: `{final_dir}`",
        f"- Copied to Assets folder: `{ASSET_DIR}`",
        f"- Backup of prior Guest01 Assets: `{backup_dir}`",
        f"- Output format: {FINAL_SR} Hz, mono, PCM_16 WAV",
        f"- Peak target: -3 dBFS",
        f"- Base ending post-roll: {BASE_TAIL_SECONDS:.2f}s",
        f"- Long ending post-roll threshold: tail peak > 0.035",
        "",
        "## Files",
        "",
    ]
    for filename, old_duration, new_duration, before_tail_peak, tail_seconds in rows:
        report.append(
            f"- `{filename}`: old_duration={old_duration:.3f}s, "
            f"new_duration={new_duration:.3f}s, pre_repair_tail_peak={before_tail_peak:.5f}, "
            f"added_tail={tail_seconds:.2f}s"
        )
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text("\n".join(report) + "\n", encoding="utf-8")
    shutil.copy2(report_path, run_dir / report_path.name)

    print(f"Tail repaired {len(LINES)} Guest 1 WAVs")
    print(f"Output: {final_dir}")
    print(f"Assets: {ASSET_DIR}")
    print(f"Backup: {backup_dir}")
    print(f"Report: {report_path}")


if __name__ == "__main__":
    main()
