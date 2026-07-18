#!/usr/bin/env python3
"""Generate only the Chateau Chantilly REV6 replacement dialogue with Fish Audio S2-Pro.

The script conditions each voice on existing, unaffected in-game Fish S2 clips,
generates several candidates, selects the delivery closest to that speaker's
existing cadence, applies a small pitch-preserving tempo fit when needed, and
replaces only the twelve REV6 WAVs. Unity .meta files are never touched.

Run from the project root with GPU access:
    /home/hamzak/micromamba/envs/fish-speech-s2/bin/python -u \
        Tools/Voice/generate_rev6_dialogue_fish_s2.py
"""

from __future__ import annotations

import argparse
import math
import re
import shutil
import subprocess
import sys
import time
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path

import numpy as np
import soundfile as sf
import soxr
import torch


PROJECT_ROOT = Path(__file__).resolve().parents[2]
FISH_ROOT = Path.home() / "ai-tts" / "fish-speech-s2" / "fish-speech-src"
CHECKPOINT = FISH_ROOT / "checkpoints" / "s2-pro"
VOICE_ROOT = PROJECT_ROOT / "Assets" / "Audio" / "Voice"
GUEST_ROOT = VOICE_ROOT / "Guests"
BUTLER_ROOT = VOICE_ROOT / "Butler"
REPORT_PATH = PROJECT_ROOT / "Tools" / "Voice" / "reports" / "rev6_fish_s2_generation_report.md"
STAGING_PARENT = Path("/tmp")

FINAL_SAMPLE_RATE = 44100
TARGET_PEAK = 10 ** (-3.0 / 20.0)
FADE_SECONDS = 0.020
SHORT_TAIL_SECONDS = 0.60
LONG_TAIL_SECONDS = 0.85
ACTIVE_PADDING_SECONDS = 0.040
MIN_TEMPO = 0.86
MAX_TEMPO = 1.16

sys.path.insert(0, str(FISH_ROOT))

from fish_speech.models.text2semantic.inference import (  # noqa: E402
    decode_to_audio,
    encode_audio,
    generate_long,
    init_model,
    load_codec_model,
)


@dataclass(frozen=True)
class ReferenceLine:
    line_id: str
    text: str
    path: Path


@dataclass(frozen=True)
class SpeakerConfig:
    key: str
    display_name: str
    references: tuple[ReferenceLine, ...]


@dataclass(frozen=True)
class DialogueLine:
    speaker_key: str
    line_id: str
    text: str
    seed: int


@dataclass
class CandidateResult:
    index: int
    seed: int
    raw_path: Path
    active_duration: float
    target_duration: float
    centroid_hz: float
    score: float


@dataclass
class FinalResult:
    line: DialogueLine
    speaker: SpeakerConfig
    selected_candidate: CandidateResult
    final_path: Path
    asset_path: Path
    duration: float
    active_duration: float
    target_duration: float
    tempo: float
    peak: float
    tail_seconds: float
    elapsed_seconds: float
    candidates: list[CandidateResult]


def guest_path(number: int, line_id: str) -> Path:
    return GUEST_ROOT / f"Guest{number:02d}" / f"{line_id}.wav"


def butler_reference(line_id: str, text: str) -> ReferenceLine:
    return ReferenceLine(line_id, text, BUTLER_ROOT / f"{line_id}.wav")


def guest_reference(number: int, line_id: str, text: str) -> ReferenceLine:
    return ReferenceLine(line_id, text, guest_path(number, line_id))


SPEAKERS = {
    "butler": SpeakerConfig(
        "butler",
        "Butler",
        (
            butler_reference(
                "SUB_CH02_BUTLER_FOUND_G01",
                "I have found you, Miss Isolde Wren. Dinner shall be served in the Dining Room at seven o'clock precisely. Might I record your wishes for the table?",
            ),
            butler_reference(
                "SUB_CH02_BUTLER_FOUND_G02",
                "I have found you, Professor Lucien Vale. Dinner shall be served in the Dining Room at seven o'clock precisely. Might I record your wishes for the table?",
            ),
            butler_reference(
                "SUB_CH02_BUTLER_SPIRITS_ASK_001",
                "And shall I see that your bottle of spirits is waiting at the table?",
            ),
        ),
    ),
    "g01": SpeakerConfig(
        "g01",
        "Miss Isolde Wren",
        (
            guest_reference(1, "CH2_G01_MEAL_PLINK", "The fresh monte genellion de plink. If one must face horrors, one should do it properly fed."),
            guest_reference(1, "CH2_G01_SPIRITS_REPLY", "See that it is not shy."),
            guest_reference(1, "CH2_G01_SMOKE_PIPE", "A pipe. Slower nerves make better decisions."),
        ),
    ),
    "g02": SpeakerConfig(
        "g02",
        "Professor Lucien Vale",
        (
            guest_reference(2, "CH2_G02_MEAL_THYME", "Thyme with Lillums, please. Something gentle. Something with leaves."),
            guest_reference(2, "CH2_G02_SMOKE_CIGAR", "A cigar, though I may only hold it for courage."),
            guest_reference(2, "CH2_G02_FOUND_REPLY", "At seven? After that thing? Yes. Yes, ordinary questions may save us."),
        ),
    ),
    "g03": SpeakerConfig(
        "g03",
        "Mister Florian Knell",
        (
            guest_reference(3, "CH2_G03_MEAL_PLINK", "Fresh monte genellion de plink. It sounds impossible, and I am in an impossible mood."),
            guest_reference(3, "CH2_G03_SMOKE_NONE", "No smoke. The monster already supplied quite enough atmosphere."),
            guest_reference(3, "CH2_G03_SPIRITS_REPLY", "Make it visible. I may need to toast survival several times."),
        ),
    ),
    "g04": SpeakerConfig(
        "g04",
        "Countess Elowen Dusk",
        (
            guest_reference(4, "CH2_G04_MEAL_THYME", "Thyme with Lillums. Quiet food. Sensible food. Food unlikely to chase me."),
            guest_reference(4, "CH2_G04_SPIRITS_REPLY", "Good. I distrust a dinner table without witnesses."),
            guest_reference(4, "CH2_G04_SMOKE_PIPE", "A pipe. It gives the hands something to do besides tremble."),
        ),
    ),
    "g05": SpeakerConfig(
        "g05",
        "Baron Hector Glass",
        (
            guest_reference(5, "CH2_G05_MEAL_PLINK", "Fresh monte genellion de plink. Something substantial. I dislike fleeing on an empty stomach."),
            guest_reference(5, "CH2_G05_SMOKE_CIGAR", "A cigar. For victory, or for pretending."),
            guest_reference(5, "CH2_G05_SPIRITS_REPLY", "Place it where I can reach it without turning my back."),
        ),
    ),
    "g06": SpeakerConfig(
        "g06",
        "Lady Sabine Marrow",
        (
            guest_reference(6, "CH2_G06_MEAL_THYME", "Thyme with Lillums. That sounds almost medicinal. I accept."),
            guest_reference(6, "CH2_G06_SMOKE_NONE", "No smoke. The room has already burned itself into my memory."),
            guest_reference(6, "CH2_G06_FOUND_REPLY", "Yes. Please. Ask me anything that has only two answers."),
        ),
    ),
    "g07": SpeakerConfig(
        "g07",
        "Lord Ambrose Veil",
        (
            guest_reference(7, "CH2_G07_MEAL_PLINK", "Fresh monte genellion de plink. It sounds like a spell, and we may need one."),
            guest_reference(7, "CH2_G07_SMOKE_PIPE", "A pipe. Smoke curls like warnings when the air is honest."),
            guest_reference(7, "CH2_G07_SPIRITS_REPLY", "Then pour generously. The chateau has had enough of my nerves."),
        ),
    ),
    "g08": SpeakerConfig(
        "g08",
        "Madame Coralie Thread",
        (
            guest_reference(8, "CH2_G08_MEAL_THYME", "Thyme with Lillums. Quiet, green, and unlikely to announce itself on nine legs."),
            guest_reference(8, "CH2_G08_SMOKE_CIGAR", "A cigar. I intend to leave evidence that I remained composed."),
            guest_reference(8, "CH2_G08_FOUND_REPLY", "You may. I admire a household that continues taking orders after an omen."),
        ),
    ),
}


LINES = (
    DialogueLine(
        "butler",
        "SUB_CH02_BUTLER_ADDRESS_GUESTS_001",
        "Welcome, friends and honored guests, to Chateau Chantilly. On behalf of the Count and Countess—",
        82007,
    ),
    DialogueLine("g01", "CH2_G01_EXIT_TO_DINING", "Then I shall proceed to the Dining Room. Perhaps punctuality can restore what panic has misplaced.", 71118),
    DialogueLine("g02", "CH2_G02_SPIRITS_REPLY", "No, thank you. I may need every faculty I possess.", 72217),
    DialogueLine("g02", "CH2_G02_EXIT_TO_DINING", "Right. The Dining Room at seven. A chair and an ordinary meal sound remarkably reassuring.", 72218),
    DialogueLine("g03", "CH2_G03_EXIT_TO_DINING", "To the Dining Room, then. I shall arrive composed, even if I must rehearse it on the way.", 73318),
    DialogueLine("g04", "CH2_G04_EXIT_TO_DINING", "I will be in the Dining Room at seven—assuming the house still permits a civilized schedule.", 74418),
    DialogueLine("g05", "CH2_G05_EXIT_TO_DINING", "Understood. I shall take my place in the Dining Room and keep watch on the doors.", 75518),
    DialogueLine("g06", "CH2_G06_SPIRITS_REPLY", "Please leave my bottle put away. I need to know whether that violin starts again.", 76617),
    DialogueLine("g06", "CH2_G06_EXIT_TO_DINING", "Thank you. I will make my way to the Dining Room. Please warn me if anything starts playing again.", 76618),
    DialogueLine("g07", "CH2_G07_EXIT_TO_DINING", "I shall meet the others in the Dining Room. Better that none of us make the journey alone.", 77718),
    DialogueLine("g08", "CH2_G08_SPIRITS_REPLY", "No spirits tonight. I intend to remain the most trustworthy guest at the table.", 78817),
    DialogueLine("g08", "CH2_G08_EXIT_TO_DINING", "Then the Dining Room it is. I intend to arrive before the house invents another interruption.", 78818),
)


def asset_path(line: DialogueLine) -> Path:
    if line.speaker_key == "butler":
        return BUTLER_ROOT / f"{line.line_id}.wav"
    return guest_path(int(line.speaker_key[1:]), line.line_id)


def word_count(text: str) -> int:
    return len(re.findall(r"[A-Za-z]+(?:'[A-Za-z]+)?", text))


def read_mono(path: Path, sample_rate: int = FINAL_SAMPLE_RATE) -> np.ndarray:
    data, source_rate = sf.read(path, always_2d=True, dtype="float32")
    mono = np.nan_to_num(data.mean(axis=1), nan=0.0, posinf=0.0, neginf=0.0)
    if source_rate != sample_rate:
        mono = soxr.resample(mono, source_rate, sample_rate, quality="VHQ")
    return mono.astype(np.float32, copy=False)


def active_bounds(audio: np.ndarray) -> tuple[int, int]:
    if audio.size == 0:
        return 0, 0
    peak = float(np.max(np.abs(audio)))
    if peak <= 0:
        return 0, 0
    threshold = max(0.001, peak * 0.015)
    active = np.flatnonzero(np.abs(audio) >= threshold)
    if active.size == 0:
        return 0, 0
    padding = int(FINAL_SAMPLE_RATE * ACTIVE_PADDING_SECONDS)
    return max(0, int(active[0]) - padding), min(audio.size, int(active[-1]) + 1 + padding)


def active_duration(audio: np.ndarray) -> float:
    start, end = active_bounds(audio)
    return max(0.0, (end - start) / FINAL_SAMPLE_RATE)


def trim_to_active(audio: np.ndarray) -> np.ndarray:
    start, end = active_bounds(audio)
    if end <= start:
        raise ValueError("Generated audio contains no detectable speech.")
    return audio[start:end].copy()


def spectral_centroid(audio: np.ndarray) -> float:
    if audio.size < 2:
        return 0.0
    windowed = audio * np.hanning(audio.size).astype(np.float32)
    magnitude = np.abs(np.fft.rfft(windowed))
    total = float(np.sum(magnitude))
    if total <= 0:
        return 0.0
    frequencies = np.fft.rfftfreq(audio.size, 1.0 / FINAL_SAMPLE_RATE)
    return float(np.sum(frequencies * magnitude) / total)


def speaker_delivery(speaker: SpeakerConfig) -> tuple[float, float]:
    total_words = 0
    total_seconds = 0.0
    centroids: list[float] = []
    for reference in speaker.references:
        audio = read_mono(reference.path)
        trimmed = trim_to_active(audio)
        total_words += word_count(reference.text)
        total_seconds += trimmed.size / FINAL_SAMPLE_RATE
        centroids.append(spectral_centroid(trimmed))
    if total_words <= 0 or total_seconds <= 0:
        raise ValueError(f"Invalid references for {speaker.display_name}.")
    return total_words / total_seconds, float(np.median(centroids))


def require_preflight() -> None:
    if not CHECKPOINT.exists():
        raise FileNotFoundError(f"Missing Fish S2 checkpoint: {CHECKPOINT}")
    if shutil.which("ffmpeg") is None:
        raise FileNotFoundError("ffmpeg is required for pitch-preserving tempo fitting.")
    for speaker in SPEAKERS.values():
        for reference in speaker.references:
            if not reference.path.exists():
                raise FileNotFoundError(f"Missing reference: {reference.path}")
    for line in LINES:
        target = asset_path(line)
        if not target.exists():
            raise FileNotFoundError(f"Missing target WAV: {target}")
        if not target.with_suffix(target.suffix + ".meta").exists():
            raise FileNotFoundError(f"Missing Unity metadata: {target}.meta")
    if not torch.cuda.is_available():
        raise RuntimeError("CUDA is unavailable. Run this script with GPU access.")


def generate_candidate(
    line: DialogueLine,
    speaker: SpeakerConfig,
    model,
    decode_one_token,
    codec,
    prompt_tokens: list[torch.Tensor],
    output_path: Path,
    seed: int,
    target_duration: float,
    reference_centroid: float,
    args: argparse.Namespace,
    index: int,
) -> CandidateResult:
    torch.manual_seed(seed)
    torch.cuda.manual_seed(seed)
    torch.cuda.manual_seed_all(seed)
    codes = []
    prompt_text = [f"<|speaker:0|>{reference.text}" for reference in speaker.references]
    for response in generate_long(
        model=model,
        device="cuda",
        decode_one_token=decode_one_token,
        text=f"<|speaker:0|>{line.text}",
        num_samples=1,
        max_new_tokens=args.max_new_tokens,
        top_p=args.top_p,
        top_k=args.top_k,
        temperature=args.temperature,
        compile=False,
        iterative_prompt=True,
        chunk_length=300,
        prompt_text=prompt_text,
        prompt_tokens=prompt_tokens,
    ):
        if response.action == "sample":
            codes.append(response.codes)
    if not codes:
        raise RuntimeError(f"Fish S2 generated no codes for {line.line_id}, candidate {index}.")

    audio = decode_to_audio(torch.cat(codes, dim=1).to("cuda"), codec)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    sf.write(output_path, audio.cpu().float().numpy(), int(codec.sample_rate), subtype="PCM_16")
    processed = trim_to_active(read_mono(output_path))
    duration = processed.size / FINAL_SAMPLE_RATE
    centroid = spectral_centroid(processed)
    duration_score = abs(math.log(max(duration, 0.001) / max(target_duration, 0.001)))
    centroid_score = abs(math.log(max(centroid, 1.0) / max(reference_centroid, 1.0)))
    return CandidateResult(index, seed, output_path, duration, target_duration, centroid, duration_score + 0.10 * centroid_score)


def tempo_fit(audio: np.ndarray, target_duration: float, work_dir: Path) -> tuple[np.ndarray, float]:
    current_duration = audio.size / FINAL_SAMPLE_RATE
    requested_tempo = current_duration / max(target_duration, 0.001)
    tempo = float(np.clip(requested_tempo, MIN_TEMPO, MAX_TEMPO))
    if abs(tempo - 1.0) < 0.025:
        return audio, 1.0

    source_path = work_dir / "tempo_source.wav"
    output_path = work_dir / "tempo_fitted.wav"
    sf.write(source_path, audio, FINAL_SAMPLE_RATE, subtype="PCM_16")
    subprocess.run(
        [
            "ffmpeg",
            "-hide_banner",
            "-loglevel",
            "error",
            "-y",
            "-i",
            str(source_path),
            "-af",
            f"rubberband=tempo={tempo:.6f}",
            "-ac",
            "1",
            "-ar",
            str(FINAL_SAMPLE_RATE),
            "-c:a",
            "pcm_s16le",
            str(output_path),
        ],
        check=True,
    )
    return trim_to_active(read_mono(output_path)), tempo


def master_candidate(candidate: CandidateResult, output_path: Path, work_dir: Path) -> tuple[float, float, float, float, float]:
    active = trim_to_active(read_mono(candidate.raw_path))
    active, tempo = tempo_fit(active, candidate.target_duration, work_dir)
    fade_samples = min(active.size, max(1, int(FINAL_SAMPLE_RATE * FADE_SECONDS)))
    active[-fade_samples:] *= np.linspace(1.0, 0.0, fade_samples, dtype=np.float32)
    peak = float(np.max(np.abs(active))) if active.size else 0.0
    if peak <= 0:
        raise ValueError(f"Selected candidate is silent: {candidate.raw_path}")
    active *= min(TARGET_PEAK / peak, 32.0)
    active = np.clip(active, -0.999, 0.999)
    active_seconds = active.size / FINAL_SAMPLE_RATE
    tail_seconds = LONG_TAIL_SECONDS if active_seconds >= 3.0 else SHORT_TAIL_SECONDS
    final = np.concatenate([active, np.zeros(int(FINAL_SAMPLE_RATE * tail_seconds), dtype=np.float32)])
    output_path.parent.mkdir(parents=True, exist_ok=True)
    sf.write(output_path, final, FINAL_SAMPLE_RATE, subtype="PCM_16")
    return final.size / FINAL_SAMPLE_RATE, active_seconds, tempo, float(np.max(np.abs(final))), tail_seconds


def validate_final(path: Path, target_duration: float) -> None:
    info = sf.info(path)
    if info.samplerate != FINAL_SAMPLE_RATE or info.channels != 1 or info.subtype != "PCM_16":
        raise ValueError(f"Invalid WAV format for {path}: {info}")
    audio = read_mono(path)
    peak = float(np.max(np.abs(audio))) if audio.size else 0.0
    if not 0.68 <= peak <= 0.72:
        raise ValueError(f"Unexpected peak for {path}: {peak:.4f}")
    active_seconds = active_duration(audio)
    if active_seconds < 0.8:
        raise ValueError(f"Speech is too short for {path}: {active_seconds:.3f}s")
    cadence_error = abs(active_seconds - target_duration) / max(target_duration, 0.001)
    if cadence_error > 0.20:
        raise ValueError(f"Cadence differs by {cadence_error:.1%} for {path}")
    tail = audio[-int(FINAL_SAMPLE_RATE * 0.25) :]
    if tail.size and float(np.max(np.abs(tail))) > 0.001:
        raise ValueError(f"Missing quiet tail for {path}")


def run_text(command: list[str]) -> str:
    try:
        return subprocess.run(command, check=True, text=True, capture_output=True).stdout.strip()
    except Exception as exc:
        return f"unavailable: {exc}"


def write_report(
    run_dir: Path,
    results: list[FinalResult],
    delivery: dict[str, tuple[float, float]],
    started: str,
    elapsed: float,
    args: argparse.Namespace,
) -> None:
    lines = [
        "# Chateau Chantilly REV6 Fish Audio S2 Generation Report",
        "",
        "Generated only the dialogue explicitly required by the REV6 final-audio checklist.",
        "",
        f"- Started: `{started}`",
        f"- Elapsed seconds: `{elapsed:.1f}`",
        f"- Fish source: `{FISH_ROOT}`",
        f"- Fish source commit: `{run_text(['git', '-C', str(FISH_ROOT), 'rev-parse', '--short', 'HEAD'])}`",
        f"- Checkpoint: `{CHECKPOINT}`",
        f"- Device: `{torch.cuda.get_device_name(0)}`",
        f"- Sampling: temperature={args.temperature}, top_p={args.top_p}, top_k={args.top_k}",
        f"- Candidates per line: `{args.candidates}`",
        f"- Output: {FINAL_SAMPLE_RATE} Hz mono PCM-16, about -3 dBFS, pitch-preserving cadence fit, quiet post-roll.",
        f"- Staging/backup directory: `{run_dir}`",
        "- Unity `.meta` files changed: `False`",
        "",
        "## Voice references",
        "",
    ]
    for speaker_key, speaker in SPEAKERS.items():
        words_per_second, centroid = delivery[speaker_key]
        lines.append(f"- {speaker.display_name}: {words_per_second:.3f} active words/sec, median centroid {centroid:.0f} Hz")
        for reference in speaker.references:
            lines.append(f"  - `{reference.line_id}`: `{reference.path}`")

    lines.extend(["", "## Generated files", ""])
    for result in results:
        candidate_summary = ", ".join(
            f"#{item.index} seed={item.seed} active={item.active_duration:.3f}s score={item.score:.4f}"
            for item in result.candidates
        )
        lines.extend(
            [
                f"- `{result.line.line_id}` — {result.speaker.display_name}",
                f"  - Text: {result.line.text}",
                f"  - Candidates: {candidate_summary}",
                f"  - Selected: #{result.selected_candidate.index}; target active={result.target_duration:.3f}s; final active={result.active_duration:.3f}s; tempo={result.tempo:.4f}",
                f"  - Final duration={result.duration:.3f}s; tail={result.tail_seconds:.2f}s; peak={result.peak:.4f}; elapsed={result.elapsed_seconds:.1f}s",
                f"  - Asset: `{result.asset_path}`",
            ]
        )

    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    REPORT_PATH.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate the twelve Chateau Chantilly REV6 replacement voice lines.")
    parser.add_argument("--candidates", type=int, default=3)
    parser.add_argument("--temperature", type=float, default=0.72)
    parser.add_argument("--top-p", type=float, default=0.82)
    parser.add_argument("--top-k", type=int, default=30)
    parser.add_argument("--max-new-tokens", type=int, default=420)
    parser.add_argument("--seed-offset", type=int, default=0)
    args = parser.parse_args()
    if args.candidates < 1:
        raise ValueError("--candidates must be at least 1")

    require_preflight()
    started = datetime.now().strftime("%Y%m%d_%H%M%S")
    run_dir = STAGING_PARENT / f"chateau_rev6_fish_s2_{started}"
    raw_dir = run_dir / "raw_candidates"
    final_dir = run_dir / "final"
    backup_dir = run_dir / "asset_backup"
    run_dir.mkdir(parents=True, exist_ok=False)

    delivery = {key: speaker_delivery(speaker) for key, speaker in SPEAKERS.items()}
    precision = torch.bfloat16
    print("Loading Fish Audio S2-Pro model on CUDA...", flush=True)
    started_clock = time.perf_counter()
    model, decode_one_token = init_model(CHECKPOINT, "cuda", precision, compile=False)
    with torch.device("cuda"):
        model.setup_caches(
            max_batch_size=1,
            max_seq_len=model.config.max_seq_len,
            dtype=next(model.parameters()).dtype,
        )
    print("Loading Fish Audio S2-Pro codec...", flush=True)
    codec = load_codec_model(CHECKPOINT / "codec.pth", "cuda", precision)

    prompt_cache: dict[str, list[torch.Tensor]] = {}
    for key, speaker in SPEAKERS.items():
        print(f"Encoding {speaker.display_name} reference clips...", flush=True)
        prompt_cache[key] = [encode_audio(reference.path, codec, "cuda").cpu() for reference in speaker.references]

    results: list[FinalResult] = []
    for line_index, line in enumerate(LINES, start=1):
        line_clock = time.perf_counter()
        speaker = SPEAKERS[line.speaker_key]
        words_per_second, reference_centroid = delivery[line.speaker_key]
        target_duration = word_count(line.text) / words_per_second
        print(f"[{line_index:02d}/{len(LINES):02d}] {line.line_id} — {speaker.display_name}", flush=True)
        candidates: list[CandidateResult] = []
        for candidate_index in range(args.candidates):
            seed = line.seed + args.seed_offset + candidate_index * 1009
            raw_path = raw_dir / line.line_id / f"candidate_{candidate_index + 1:02d}_seed_{seed}.wav"
            candidate = generate_candidate(
                line,
                speaker,
                model,
                decode_one_token,
                codec,
                prompt_cache[line.speaker_key],
                raw_path,
                seed,
                target_duration,
                reference_centroid,
                args,
                candidate_index + 1,
            )
            candidates.append(candidate)
            print(f"  candidate {candidate.index}: active={candidate.active_duration:.3f}s score={candidate.score:.4f}", flush=True)
            torch.cuda.empty_cache()

        selected = min(candidates, key=lambda item: item.score)
        final_path = final_dir / f"{line.line_id}.wav"
        line_work_dir = run_dir / "tempo" / line.line_id
        line_work_dir.mkdir(parents=True, exist_ok=True)
        duration, final_active, tempo, peak, tail_seconds = master_candidate(selected, final_path, line_work_dir)
        validate_final(final_path, target_duration)
        results.append(
            FinalResult(
                line,
                speaker,
                selected,
                final_path,
                asset_path(line),
                duration,
                final_active,
                target_duration,
                tempo,
                peak,
                tail_seconds,
                time.perf_counter() - line_clock,
                candidates,
            )
        )

    if len(results) != len(LINES):
        raise RuntimeError(f"Generated {len(results)} lines; expected {len(LINES)}")

    # All staged files have passed validation. Back up and replace WAV contents only.
    for result in results:
        backup_path = backup_dir / result.asset_path.relative_to(VOICE_ROOT)
        backup_path.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(result.asset_path, backup_path)
    for result in results:
        shutil.copy2(result.final_path, result.asset_path)

    elapsed = time.perf_counter() - started_clock
    write_report(run_dir, results, delivery, started, elapsed, args)
    print(f"Generated and installed {len(results)} REV6 WAVs.", flush=True)
    print(f"Staging and backups: {run_dir}", flush=True)
    print(f"Report: {REPORT_PATH}", flush=True)


if __name__ == "__main__":
    main()
