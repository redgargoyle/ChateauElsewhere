#!/usr/bin/env python3
"""Targeted Fish Audio S2 natural-delivery repair for the Butler, Guest 5, and Guest 7.

This pass intentionally avoids the criticized Chapter 2 order clips as voice/cadence
references. It generates several takes, rejects slow or pause-heavy deliveries, never
time-stretches a selected take, and replaces only the ten live WAVs listed below.
Unity .meta files are preserved.
"""

from __future__ import annotations

import argparse
import math
import os
import re
import shutil
import subprocess
import sys
import time
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path

os.environ.setdefault("NUMBA_CACHE_DIR", "/tmp/chateau_fish_s2_numba_cache")

import librosa
import numpy as np
import soundfile as sf
import torch


PROJECT_ROOT = Path(__file__).resolve().parents[2]
FISH_ROOT = Path.home() / "ai-tts" / "fish-speech-s2" / "fish-speech-src"
CHECKPOINT = FISH_ROOT / "checkpoints" / "s2-pro"
VOICE_ROOT = PROJECT_ROOT / "Assets" / "Audio" / "Voice"
BUTLER_ROOT = VOICE_ROOT / "Butler"
GUEST_ROOT = VOICE_ROOT / "Guests"
REPORT_PATH = PROJECT_ROOT / "Tools" / "Voice" / "reports" / "natural_delivery_guest57_butler_fish_s2_report.md"
STAGING_PARENT = Path("/tmp")

SAMPLE_RATE = 44100
TARGET_PEAK = 10 ** (-3.0 / 20.0)
FADE_SECONDS = 0.020
GUEST_TAIL_SECONDS = 0.25
BUTLER_TAIL_SECONDS = 0.15
ACTIVE_PADDING_SECONDS = 0.025

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
class Speaker:
    key: str
    display_name: str
    references: tuple[ReferenceLine, ...]
    target_words_per_second: float
    minimum_words_per_second: float
    maximum_words_per_second: float
    minimum_pitch_range_semitones: float


@dataclass(frozen=True)
class DialogueLine:
    speaker_key: str
    line_id: str
    canonical_text: str
    synthesis_text: str
    seed: int


@dataclass(frozen=True)
class Candidate:
    index: int
    seed: int
    temperature: float
    top_p: float
    top_k: int
    raw_path: Path
    spoken_seconds: float
    words_per_second: float
    internal_silence_seconds: float
    longest_internal_pause_seconds: float
    pitch_range_semitones: float
    centroid_hz: float
    score: float
    passes: bool


@dataclass(frozen=True)
class InstalledLine:
    line: DialogueLine
    speaker: Speaker
    selected: Candidate
    candidates: tuple[Candidate, ...]
    asset_path: Path
    final_path: Path
    final_duration: float
    tail_seconds: float
    tempo: float
    peak: float
    elapsed_seconds: float


def guest_path(number: int, line_id: str) -> Path:
    return GUEST_ROOT / f"Guest{number:02d}" / f"{line_id}.wav"


PRIOR_BUTLER_ADDRESS = (
    PROJECT_ROOT
    / "Tools"
    / "Voice"
    / "generated_fish_s2_butler_dialogue"
    / "butler_fish_s2_full_dialogue_20260620_091809"
    / "final_44k1_tail_padded"
    / "SUB_CH02_BUTLER_ADDRESS_GUESTS_001.wav"
)


SPEAKERS = {
    "butler": Speaker(
        "butler",
        "Butler",
        (
            ReferenceLine(
                "PREVIOUS_BUTLER_ADDRESS",
                "Welcome friends and gentlemen, guests of the evening, Count and Countess of Chantilly—",
                PRIOR_BUTLER_ADDRESS,
            ),
            ReferenceLine(
                "SUB_CH01_BUTLER_THIS_WAY_001",
                "This way, please. The Drawing Room is prepared.",
                BUTLER_ROOT / "SUB_CH01_BUTLER_THIS_WAY_001.wav",
            ),
            ReferenceLine(
                "SUB_CH01_BUTLER_ONE_COAT_001",
                "One coat at a time, if you please.",
                BUTLER_ROOT / "SUB_CH01_BUTLER_ONE_COAT_001.wav",
            ),
        ),
        target_words_per_second=2.90,
        minimum_words_per_second=2.65,
        maximum_words_per_second=3.30,
        minimum_pitch_range_semitones=6.80,
    ),
    "g05": Speaker(
        "g05",
        "Baron Hector Glass",
        (
            ReferenceLine(
                "CH1_G05_ENTRY",
                "Good evening. I hope the evening has not started without us.",
                guest_path(5, "CH1_G05_ENTRY"),
            ),
            ReferenceLine(
                "CH1_G05_DELAYED",
                "We were beginning to wonder if anyone was home.",
                guest_path(5, "CH1_G05_DELAYED"),
            ),
            ReferenceLine(
                "CH1_G05_TO_DRAWING_ROOM",
                "Then let us not keep the Drawing Room from its purpose.",
                guest_path(5, "CH1_G05_TO_DRAWING_ROOM"),
            ),
            ReferenceLine(
                "CH1_G05_AMBIENT_01",
                "This house is colder than I expected.",
                guest_path(5, "CH1_G05_AMBIENT_01"),
            ),
        ),
        target_words_per_second=2.35,
        minimum_words_per_second=2.10,
        maximum_words_per_second=3.50,
        minimum_pitch_range_semitones=5.50,
    ),
    "g07": Speaker(
        "g07",
        "Lord Ambrose Veil",
        (
            ReferenceLine(
                "CH1_G07_ENTRY",
                "Lovely to see you. The chateau looks almost awake tonight.",
                guest_path(7, "CH1_G07_ENTRY"),
            ),
            ReferenceLine(
                "CH1_G07_DELAYED",
                "We have been waiting at the door for some time. The house was listening with us.",
                guest_path(7, "CH1_G07_DELAYED"),
            ),
            ReferenceLine(
                "CH1_G07_AMBIENT_02",
                "The ceiling has footsteps in it, and not all of them are human.",
                guest_path(7, "CH1_G07_AMBIENT_02"),
            ),
            ReferenceLine(
                "CH1_G07_EMPTY_BELL_REACTION",
                "It wanted us all in here. That is what I think.",
                guest_path(7, "CH1_G07_EMPTY_BELL_REACTION"),
            ),
        ),
        target_words_per_second=2.70,
        minimum_words_per_second=2.55,
        maximum_words_per_second=3.85,
        minimum_pitch_range_semitones=5.50,
    ),
}


LINES = (
    DialogueLine(
        "butler",
        "SUB_CH02_BUTLER_ADDRESS_GUESTS_001",
        "Welcome, friends and honored guests, to Chateau Chantilly. On behalf of the Count and Countess—",
        "Welcome, friends and honored guests to Chateau Chantilly—on behalf of the Count and Countess—",
        982007,
    ),
    DialogueLine(
        "g05",
        "CH2_G05_FOUND_REPLY",
        "Proceed. The more ordinary the ritual, the less power we give the extraordinary.",
        "Proceed—the more ordinary the ritual, the less power we give the extraordinary.",
        975511,
    ),
    DialogueLine(
        "g05",
        "CH2_G05_MEAL_PLINK",
        "Fresh monte genellion de plink. Something substantial. I dislike fleeing on an empty stomach.",
        "Fresh monte genellion de plink; something substantial—I dislike fleeing on an empty stomach.",
        975512,
    ),
    DialogueLine(
        "g05",
        "CH2_G05_SMOKE_CIGAR",
        "A cigar. For victory, or for pretending.",
        "A cigar—for victory, or for pretending.",
        975514,
    ),
    DialogueLine(
        "g05",
        "CH2_G05_EXIT_TO_DINING",
        "Understood. I shall take my place in the Dining Room and keep watch on the doors.",
        "Understood—I shall take my place in the Dining Room and keep watch on the doors.",
        975518,
    ),
    DialogueLine(
        "g07",
        "CH2_G07_FOUND_REPLY",
        "Record quickly. The walls have begun pretending not to listen.",
        "Record quickly—the walls have begun pretending not to listen.",
        977711,
    ),
    DialogueLine(
        "g07",
        "CH2_G07_MEAL_PLINK",
        "Fresh monte genellion de plink. It sounds like a spell, and we may need one.",
        "Fresh monte genellion de plink—it sounds like a spell, and we may need one.",
        977712,
    ),
    DialogueLine(
        "g07",
        "CH2_G07_SMOKE_PIPE",
        "A pipe. Smoke curls like warnings when the air is honest.",
        "A pipe—smoke curls like warnings when the air is honest.",
        977715,
    ),
    DialogueLine(
        "g07",
        "CH2_G07_SPIRITS_REPLY",
        "Then pour generously. The chateau has had enough of my nerves.",
        "Then pour generously—the chateau has had enough of my nerves.",
        977717,
    ),
    DialogueLine(
        "g07",
        "CH2_G07_EXIT_TO_DINING",
        "I shall meet the others in the Dining Room. Better that none of us make the journey alone.",
        "I shall meet the others in the Dining Room; better that none of us make the journey alone.",
        977718,
    ),
)


SAMPLING_PRESETS = (
    (0.70, 0.82, 30),
    (0.74, 0.86, 35),
    (0.78, 0.88, 40),
    (0.82, 0.90, 45),
    (0.72, 0.88, 35),
    (0.76, 0.90, 40),
    (0.80, 0.86, 45),
    (0.84, 0.92, 50),
    (0.75, 0.84, 30),
)

# Offline speech recognition found that this otherwise fluent take softened
# "Countess" into "counters". Keep it for audit history but never install it.
CONTENT_REJECTED_CANDIDATES = {
    ("SUB_CH02_BUTLER_ADDRESS_GUESTS_001", 5),
}


def asset_path(line: DialogueLine) -> Path:
    if line.speaker_key == "butler":
        return BUTLER_ROOT / f"{line.line_id}.wav"
    return guest_path(int(line.speaker_key[1:]), line.line_id)


def word_count(text: str) -> int:
    return len(re.findall(r"[A-Za-z]+(?:'[A-Za-z]+)?", text))


def read_mono(path: Path) -> np.ndarray:
    audio, source_rate = sf.read(path, always_2d=True, dtype="float32")
    mono = np.nan_to_num(audio.mean(axis=1), nan=0.0, posinf=0.0, neginf=0.0)
    if source_rate != SAMPLE_RATE:
        mono = librosa.resample(mono, orig_sr=source_rate, target_sr=SAMPLE_RATE, res_type="soxr_vhq")
    return mono.astype(np.float32, copy=False)


def active_bounds(audio: np.ndarray) -> tuple[int, int]:
    if audio.size == 0:
        return 0, 0
    peak = float(np.max(np.abs(audio)))
    threshold = max(0.001, peak * 0.015)
    active = np.flatnonzero(np.abs(audio) >= threshold)
    if active.size == 0:
        return 0, 0
    padding = int(ACTIVE_PADDING_SECONDS * SAMPLE_RATE)
    return max(0, int(active[0]) - padding), min(audio.size, int(active[-1]) + 1 + padding)


def trim_active(audio: np.ndarray) -> np.ndarray:
    start, end = active_bounds(audio)
    if end <= start:
        raise ValueError("Audio contains no detectable speech.")
    return audio[start:end].copy()


def spectral_centroid(audio: np.ndarray) -> float:
    values = librosa.feature.spectral_centroid(y=audio, sr=SAMPLE_RATE)[0]
    return float(np.median(values)) if values.size else 0.0


def pitch_range_semitones(audio: np.ndarray) -> float:
    if audio.size < 2048:
        return 0.0
    hop = 256
    f0 = librosa.yin(audio, fmin=65.0, fmax=350.0, sr=SAMPLE_RATE, frame_length=2048, hop_length=hop)
    rms = librosa.feature.rms(y=audio, frame_length=2048, hop_length=hop)[0]
    count = min(f0.size, rms.size)
    if count == 0:
        return 0.0
    threshold = max(0.002, float(np.max(rms[:count])) * 0.08)
    valid = f0[:count][np.isfinite(f0[:count]) & (rms[:count] >= threshold)]
    if valid.size < 8:
        return 0.0
    low, high = np.percentile(valid, [10.0, 90.0])
    if low <= 0 or high <= low:
        return 0.0
    return float(12.0 * math.log2(high / low))


def internal_silence(audio: np.ndarray) -> tuple[float, float]:
    frame = int(0.020 * SAMPLE_RATE)
    hop = int(0.010 * SAMPLE_RATE)
    if audio.size < frame:
        return 0.0, 0.0
    frames = librosa.util.frame(audio, frame_length=frame, hop_length=hop)
    rms = np.sqrt(np.mean(frames * frames, axis=0))
    threshold = max(0.0015, float(np.max(rms)) * 0.045)
    speech = rms >= threshold
    active_frames = np.flatnonzero(speech)
    if active_frames.size == 0:
        return 0.0, 0.0
    speech = speech[int(active_frames[0]) : int(active_frames[-1]) + 1]
    gaps: list[float] = []
    start = None
    for index, is_speech in enumerate(speech):
        if not is_speech and start is None:
            start = index
        elif is_speech and start is not None:
            duration = (index - start) * hop / SAMPLE_RATE
            if duration >= 0.16:
                gaps.append(duration)
            start = None
    return float(sum(gaps)), float(max(gaps, default=0.0))


def metrics(audio: np.ndarray, text: str) -> tuple[float, float, float, float, float]:
    trimmed = trim_active(audio)
    spoken_seconds = trimmed.size / SAMPLE_RATE
    words_per_second = word_count(text) / max(spoken_seconds, 0.001)
    silence_seconds, longest_pause = internal_silence(trimmed)
    return spoken_seconds, words_per_second, silence_seconds, longest_pause, pitch_range_semitones(trimmed)


def require_preflight() -> None:
    if not CHECKPOINT.exists():
        raise FileNotFoundError(f"Missing Fish S2 checkpoint: {CHECKPOINT}")
    if shutil.which("ffmpeg") is None:
        raise FileNotFoundError("ffmpeg with the rubberband filter is required.")
    for speaker in SPEAKERS.values():
        for reference in speaker.references:
            if not reference.path.exists():
                raise FileNotFoundError(f"Missing reference: {reference.path}")
    for line in LINES:
        target = asset_path(line)
        if not target.exists() or not Path(str(target) + ".meta").exists():
            raise FileNotFoundError(f"Missing target WAV or Unity metadata: {target}")
    if not torch.cuda.is_available():
        raise RuntimeError("CUDA is unavailable. Run with GPU access.")


def prepare_references(run_dir: Path, codec) -> dict[str, tuple[list[str], list[torch.Tensor], float]]:
    prepared: dict[str, tuple[list[str], list[torch.Tensor], float]] = {}
    for speaker_key, speaker in SPEAKERS.items():
        prompt_text: list[str] = []
        prompt_tokens: list[torch.Tensor] = []
        centroids: list[float] = []
        for index, reference in enumerate(speaker.references, start=1):
            trimmed = trim_active(read_mono(reference.path))
            trimmed_path = run_dir / "references" / speaker_key / f"{index:02d}_{reference.line_id}.wav"
            trimmed_path.parent.mkdir(parents=True, exist_ok=True)
            sf.write(trimmed_path, trimmed, SAMPLE_RATE, subtype="PCM_16")
            prompt_text.append(f"<|speaker:0|>{reference.text}")
            prompt_tokens.append(encode_audio(trimmed_path, codec, "cuda").cpu())
            centroids.append(spectral_centroid(trimmed))
        prepared[speaker_key] = (prompt_text, prompt_tokens, float(np.median(centroids)))
    return prepared


def generate_candidate(
    line: DialogueLine,
    speaker: Speaker,
    model,
    decode_one_token,
    codec,
    prompt_text: list[str],
    prompt_tokens: list[torch.Tensor],
    reference_centroid: float,
    output_path: Path,
    index: int,
    seed: int,
    preset: tuple[float, float, int],
    max_new_tokens: int,
) -> Candidate:
    temperature, top_p, top_k = preset
    torch.manual_seed(seed)
    torch.cuda.manual_seed_all(seed)
    codes = []
    for response in generate_long(
        model=model,
        device="cuda",
        decode_one_token=decode_one_token,
        text=f"<|speaker:0|>{line.synthesis_text}",
        num_samples=1,
        max_new_tokens=max_new_tokens,
        top_p=top_p,
        top_k=top_k,
        temperature=temperature,
        compile=False,
        iterative_prompt=True,
        chunk_length=300,
        prompt_text=prompt_text,
        prompt_tokens=prompt_tokens,
    ):
        if response.action == "sample":
            codes.append(response.codes)
    if not codes:
        raise RuntimeError(f"Fish S2 returned no codes for {line.line_id} candidate {index}.")

    audio = decode_to_audio(torch.cat(codes, dim=1).to("cuda"), codec)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    sf.write(output_path, audio.cpu().float().numpy(), int(codec.sample_rate), subtype="PCM_16")
    return evaluate_candidate(line, speaker, reference_centroid, output_path, index, seed, preset)


def evaluate_candidate(
    line: DialogueLine,
    speaker: Speaker,
    reference_centroid: float,
    output_path: Path,
    index: int,
    seed: int,
    preset: tuple[float, float, int],
) -> Candidate:
    temperature, top_p, top_k = preset
    processed = trim_active(read_mono(output_path))
    spoken, rate, silence_total, longest_pause, pitch_range = metrics(processed, line.canonical_text)
    centroid = spectral_centroid(processed)
    content_rejected = (line.line_id, index) in CONTENT_REJECTED_CANDIDATES
    passes = (
        speaker.minimum_words_per_second <= rate <= speaker.maximum_words_per_second
        and longest_pause <= 0.45
        and silence_total <= 1.00
        and pitch_range >= speaker.minimum_pitch_range_semitones
        and not content_rejected
    )
    rate_score = abs(math.log(max(rate, 0.01) / speaker.target_words_per_second))
    pause_score = max(0.0, longest_pause - 0.22) * 1.6 + max(0.0, silence_total - 0.35) * 0.45
    pitch_score = max(0.0, speaker.minimum_pitch_range_semitones - pitch_range) * 0.12
    centroid_score = abs(math.log(max(centroid, 1.0) / max(reference_centroid, 1.0))) * 0.05
    score = rate_score * 2.0 + pause_score + pitch_score + centroid_score + (10.0 if content_rejected else 0.0)
    return Candidate(
        index,
        seed,
        temperature,
        top_p,
        top_k,
        output_path,
        spoken,
        rate,
        silence_total,
        longest_pause,
        pitch_range,
        centroid,
        score,
        passes,
    )


def master(
    candidate: Candidate,
    line: DialogueLine,
    speaker: Speaker,
    output_path: Path,
    work_dir: Path,
    allow_tempo_correction: bool = True,
) -> tuple[float, float, float, float]:
    active = trim_active(read_mono(candidate.raw_path))
    tempo = (
        float(np.clip(speaker.target_words_per_second / candidate.words_per_second, 1.0, 1.08))
        if allow_tempo_correction
        else 1.0
    )
    if tempo >= 1.015:
        work_dir.mkdir(parents=True, exist_ok=True)
        source_path = work_dir / "tempo_source.wav"
        fitted_path = work_dir / "tempo_fitted.wav"
        sf.write(source_path, active, SAMPLE_RATE, subtype="PCM_16")
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
                str(SAMPLE_RATE),
                "-c:a",
                "pcm_s16le",
                str(fitted_path),
            ],
            check=True,
        )
        active = trim_active(read_mono(fitted_path))
    else:
        tempo = 1.0
    fade_samples = min(active.size, max(1, int(FADE_SECONDS * SAMPLE_RATE)))
    active[-fade_samples:] *= np.linspace(1.0, 0.0, fade_samples, dtype=np.float32)
    peak = float(np.max(np.abs(active))) if active.size else 0.0
    if peak <= 0:
        raise ValueError(f"Selected candidate is silent: {candidate.raw_path}")
    active *= min(TARGET_PEAK / peak, 32.0)
    active = np.clip(active, -0.999, 0.999)
    tail_seconds = BUTLER_TAIL_SECONDS if line.speaker_key == "butler" else GUEST_TAIL_SECONDS
    final = np.concatenate([active, np.zeros(int(tail_seconds * SAMPLE_RATE), dtype=np.float32)])
    output_path.parent.mkdir(parents=True, exist_ok=True)
    sf.write(output_path, final, SAMPLE_RATE, subtype="PCM_16")
    return final.size / SAMPLE_RATE, tail_seconds, tempo, float(np.max(np.abs(final)))


def validate_final(path: Path, line: DialogueLine, speaker: Speaker) -> None:
    info = sf.info(path)
    if info.samplerate != SAMPLE_RATE or info.channels != 1 or info.subtype != "PCM_16":
        raise ValueError(f"Invalid final WAV format: {path}: {info}")
    audio = read_mono(path)
    peak = float(np.max(np.abs(audio))) if audio.size else 0.0
    if not 0.68 <= peak <= 0.72:
        raise ValueError(f"Unexpected final peak for {path}: {peak:.4f}")
    _, rate, silence_total, longest_pause, pitch_range = metrics(audio, line.canonical_text)
    final_minimum_rate = max(speaker.minimum_words_per_second, speaker.target_words_per_second * 0.90)
    if not final_minimum_rate <= rate <= speaker.maximum_words_per_second:
        raise ValueError(f"Final cadence is outside target for {path}: {rate:.3f} words/sec")
    if longest_pause > 0.45 or silence_total > 1.00:
        raise ValueError(f"Final pauses are too long for {path}: max={longest_pause:.3f}s total={silence_total:.3f}s")
    final_minimum_pitch_range = speaker.minimum_pitch_range_semitones * 0.90
    if pitch_range < final_minimum_pitch_range:
        raise ValueError(f"Final delivery is too flat for {path}: {pitch_range:.2f} semitones")
    tail = audio[-int(0.10 * SAMPLE_RATE) :]
    if tail.size and float(np.max(np.abs(tail))) > 0.001:
        raise ValueError(f"Final WAV lacks a quiet post-roll: {path}")


def write_report(
    run_dir: Path,
    installed: list[InstalledLine],
    started: str,
    elapsed: float,
    report_path: Path = REPORT_PATH,
) -> None:
    rows = [
        "# Natural Delivery Fish Audio S2 Repair Report",
        "",
        "Targeted quality repair for the Butler address and the live Guest 5/7 order-taking lines.",
        "",
        f"- Started: `{started}`",
        f"- Elapsed seconds: `{elapsed:.1f}`",
        f"- Model: `{CHECKPOINT}`",
        f"- Device: `{torch.cuda.get_device_name(0)}`",
        "- Candidate policy: varied sampling; reject long-pause or flat takes; pitch-preserving speed correction capped at 1.08x.",
        f"- Guest post-roll: `{GUEST_TAIL_SECONDS:.2f}s`; Butler pre-stinger post-roll: `{BUTLER_TAIL_SECONDS:.2f}s`.",
        f"- Staging and backups: `{run_dir}`",
        "- Unity `.meta` files changed: `False`",
        "",
        "## Installed lines",
        "",
    ]
    for result in installed:
        candidate_rows = ", ".join(
            (
                f"#{candidate.index} seed={candidate.seed} rate={candidate.words_per_second:.2f}w/s "
                f"pause={candidate.longest_internal_pause_seconds:.2f}s pitch={candidate.pitch_range_semitones:.1f}st "
                f"score={candidate.score:.3f} pass={candidate.passes}"
            )
            for candidate in result.candidates
        )
        rows.extend(
            [
                f"- `{result.line.line_id}` — {result.speaker.display_name}",
                f"  - Text: {result.line.canonical_text}",
                f"  - Candidates: {candidate_rows}",
                (
                    f"  - Selected: #{result.selected.index}; spoken={result.selected.spoken_seconds:.3f}s; "
                    f"rate={result.selected.words_per_second:.3f}w/s; internal silence={result.selected.internal_silence_seconds:.3f}s; "
                    f"longest pause={result.selected.longest_internal_pause_seconds:.3f}s; "
                    f"pitch range={result.selected.pitch_range_semitones:.2f}st; tempo={result.tempo:.3f}x"
                ),
                f"  - Final duration={result.final_duration:.3f}s; tail={result.tail_seconds:.2f}s; peak={result.peak:.4f}",
                f"  - Asset: `{result.asset_path}`",
            ]
        )
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text("\n".join(rows) + "\n", encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser(description="Regenerate only the criticized Butler/G5/G7 deliveries.")
    parser.add_argument("--minimum-candidates", type=int, default=5)
    parser.add_argument("--maximum-candidates", type=int, default=9)
    parser.add_argument("--max-new-tokens", type=int, default=420)
    parser.add_argument("--seed-offset", type=int, default=0)
    parser.add_argument("--resume-run-dir", type=Path)
    parser.add_argument("--only-line")
    parser.add_argument("--native-speed", action="store_true")
    parser.add_argument("--report-path", type=Path)
    args = parser.parse_args()
    if args.minimum_candidates < 1 or args.maximum_candidates < args.minimum_candidates:
        raise ValueError("Candidate limits are invalid.")
    if args.maximum_candidates > len(SAMPLING_PRESETS):
        raise ValueError(f"Maximum supported candidates is {len(SAMPLING_PRESETS)}.")

    target_lines = LINES
    if args.only_line:
        target_lines = tuple(line for line in LINES if line.line_id == args.only_line)
        if not target_lines:
            raise ValueError(f"Unknown line id: {args.only_line}")

    require_preflight()
    started = datetime.now().strftime("%Y%m%d_%H%M%S")
    run_dir = args.resume_run_dir or (STAGING_PARENT / f"chateau_natural_delivery_fish_s2_{started}")
    raw_dir = run_dir / "raw_candidates"
    final_dir = run_dir / "final"
    backup_dir = run_dir / "asset_backup"
    run_dir.mkdir(parents=True, exist_ok=args.resume_run_dir is not None)

    print("Loading Fish Audio S2-Pro model on CUDA...", flush=True)
    started_clock = time.perf_counter()
    precision = torch.bfloat16
    model, decode_one_token = init_model(CHECKPOINT, "cuda", precision, compile=False)
    with torch.device("cuda"):
        model.setup_caches(
            max_batch_size=1,
            max_seq_len=model.config.max_seq_len,
            dtype=next(model.parameters()).dtype,
        )
    codec = load_codec_model(CHECKPOINT / "codec.pth", "cuda", precision)
    prepared = prepare_references(run_dir, codec)

    installed: list[InstalledLine] = []
    for line_number, line in enumerate(target_lines, start=1):
        line_clock = time.perf_counter()
        speaker = SPEAKERS[line.speaker_key]
        prompt_text, prompt_tokens, reference_centroid = prepared[line.speaker_key]
        print(f"[{line_number:02d}/{len(target_lines):02d}] {line.line_id} — {speaker.display_name}", flush=True)
        candidates: list[Candidate] = []
        for candidate_index in range(1, args.maximum_candidates + 1):
            seed = line.seed + args.seed_offset + (candidate_index - 1) * 1009
            preset = SAMPLING_PRESETS[candidate_index - 1]
            raw_path = raw_dir / line.line_id / f"candidate_{candidate_index:02d}_seed_{seed}.wav"
            if raw_path.exists():
                candidate = evaluate_candidate(
                    line,
                    speaker,
                    reference_centroid,
                    raw_path,
                    candidate_index,
                    seed,
                    preset,
                )
            else:
                candidate = generate_candidate(
                    line,
                    speaker,
                    model,
                    decode_one_token,
                    codec,
                    prompt_text,
                    prompt_tokens,
                    reference_centroid,
                    raw_path,
                    candidate_index,
                    seed,
                    preset,
                    args.max_new_tokens,
                )
            candidates.append(candidate)
            print(
                f"  candidate {candidate.index}: rate={candidate.words_per_second:.2f}w/s "
                f"pause={candidate.longest_internal_pause_seconds:.2f}s "
                f"pitch={candidate.pitch_range_semitones:.1f}st score={candidate.score:.3f} pass={candidate.passes}",
                flush=True,
            )
            torch.cuda.empty_cache()
            passing = [item for item in candidates if item.passes]
            required_minimum = 8 if line.speaker_key == "butler" else args.minimum_candidates
            if candidate_index >= required_minimum and len(passing) >= 2:
                break

        passing = [item for item in candidates if item.passes]
        if not passing:
            raise RuntimeError(f"No natural-delivery candidate passed validation for {line.line_id}; assets were not changed.")
        selected = min(passing, key=lambda item: item.score)
        final_path = final_dir / f"{line.line_id}.wav"
        final_duration, tail_seconds, tempo, peak = master(
            selected,
            line,
            speaker,
            final_path,
            run_dir / "tempo" / line.line_id,
            allow_tempo_correction=not args.native_speed,
        )
        validate_final(final_path, line, speaker)
        installed.append(
            InstalledLine(
                line,
                speaker,
                selected,
                tuple(candidates),
                asset_path(line),
                final_path,
                final_duration,
                tail_seconds,
                tempo,
                peak,
                time.perf_counter() - line_clock,
            )
        )

    if len(installed) != len(target_lines):
        raise RuntimeError("Not every target line passed; assets were not changed.")

    for result in installed:
        backup_path = backup_dir / result.asset_path.relative_to(VOICE_ROOT)
        backup_path.parent.mkdir(parents=True, exist_ok=True)
        if not backup_path.exists():
            shutil.copy2(result.asset_path, backup_path)
    for result in installed:
        shutil.copy2(result.final_path, result.asset_path)

    elapsed = time.perf_counter() - started_clock
    report_path = args.report_path or REPORT_PATH
    write_report(run_dir, installed, started, elapsed, report_path)
    print(f"Installed {len(installed)} natural-delivery repairs.", flush=True)
    print(f"Staging and backups: {run_dir}", flush=True)
    print(f"Report: {report_path}", flush=True)


if __name__ == "__main__":
    main()
