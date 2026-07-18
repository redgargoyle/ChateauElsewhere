#!/usr/bin/env python3
"""Regenerate only the Butler's pre-scare address with Fish Audio S2-Pro.

The rejected take was cloned from other synthesized lines and then time-stretched.
This pass uses the original clean human Butler recording, generates a longer natural
continuation, locates the intended interruption with offline word timestamps, and
cuts after "Countess" without any tempo processing. No guest asset is referenced or
written, and the Unity metadata beside the target WAV is preserved.
"""

from __future__ import annotations

import argparse
import hashlib
import math
import os
import re
import shutil
import sys
import time
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path

os.environ.setdefault("HF_HUB_OFFLINE", "1")
os.environ.setdefault("TRANSFORMERS_OFFLINE", "1")
os.environ.setdefault("NUMBA_CACHE_DIR", "/tmp/chateau_butler_prescare_numba")

import librosa
import numpy as np
import soundfile as sf
import torch


PROJECT_ROOT = Path(__file__).resolve().parents[2]
FISH_ROOT = Path.home() / "ai-tts" / "fish-speech-s2" / "fish-speech-src"
CHECKPOINT = FISH_ROOT / "checkpoints" / "s2-pro"
ORIGINAL_REFERENCE = (
    Path.home()
    / "Desktop"
    / "FishAudio_S2_Butler_Hello_YoutubeRef_20260620_090942"
    / "ref"
    / "butler_youtube_ref.wav"
)
ORIGINAL_REFERENCE_TEXT = (
    "Hello, and thank you for calling. Unfortunately, the person that you've called "
    "cannot come to the phone at the moment. This is the butler speaking."
)
TARGET = (
    PROJECT_ROOT
    / "Assets"
    / "Audio"
    / "Voice"
    / "Butler"
    / "SUB_CH02_BUTLER_ADDRESS_GUESTS_001.wav"
)
TARGET_META = Path(f"{TARGET}.meta")
VOICE_ROOT = PROJECT_ROOT / "Assets" / "Audio" / "Voice"
REPORT = (
    PROJECT_ROOT
    / "Tools"
    / "Voice"
    / "reports"
    / "butler_prescare_natural_delivery_fish_s2_report.md"
)
WHISPER_SNAPSHOT = (
    Path.home()
    / ".cache"
    / "huggingface"
    / "hub"
    / "models--openai--whisper-tiny.en"
    / "snapshots"
    / "87c7102498dcde7456f24cfd30239ca606ed9063"
)

CANONICAL_TEXT = (
    "Welcome, friends and honored guests, to Chateau Chantilly. "
    "On behalf of the Count and Countess—"
)
EXPECTED_WORDS = (
    "welcome",
    "friends",
    "and",
    "honored",
    "guests",
    "to",
    "chateau",
    "chantilly",
    "on",
    "behalf",
    "of",
    "the",
    "count",
    "and",
    "countess",
)
GENERATION_WORDS = (
    "welcome",
    "friends",
    "and",
    "honored",
    "guests",
    "to",
    "chateau",
    "chantilly",
    "and",
    "on",
    "behalf",
    "of",
    "the",
    "count",
    "and",
    "countess",
)

# Continuing beyond the in-game interruption prevents Fish from applying a
# synthetic sentence-ending cadence to "Chantilly" or "Countess". The extra
# words are generated for prosody only and are removed before installation.
SYNTHESIS_VARIANTS = (
    (
        "allow_me",
        "Welcome, friends and honored guests, to Chateau Chantilly, and on behalf of "
        "the Count and Countess, allow me to begin.",
    ),
    (
        "bid_welcome",
        "Welcome, friends and honored guests, to Chateau Chantilly, and on behalf of "
        "the Count and Countess, I bid you welcome.",
    ),
    (
        "privilege",
        "Welcome, friends and honored guests, to Chateau Chantilly, and on behalf of "
        "the Count and Countess, it is my privilege to receive you.",
    ),
    (
        "pleased",
        "Welcome, friends and honored guests, to Chateau Chantilly, and on behalf of "
        "the Count and Countess, may I say how pleased we are to receive you.",
    ),
    (
        "formal_welcome",
        "Welcome, friends and honored guests, to Chateau Chantilly, and on behalf of "
        "the Count and Countess, let me extend our warmest welcome.",
    ),
)

SAMPLING_PRESETS = (
    (0.64, 0.78, 26),
    (0.68, 0.80, 30),
    (0.70, 0.82, 32),
    (0.72, 0.84, 34),
    (0.74, 0.85, 36),
    (0.76, 0.86, 38),
    (0.78, 0.88, 40),
    (0.70, 0.86, 36),
)

SAMPLE_RATE = 44100
TARGET_PEAK = 10 ** (-3.0 / 20.0)
TAIL_SECONDS = 0.15
FADE_SECONDS = 0.018
EDIT_FADE_SECONDS = 0.008
SENTENCE_PAUSE_SECONDS = 0.20
SEED_BASE = 1_820_071

sys.path.insert(0, str(FISH_ROOT))

from fish_speech.models.text2semantic.inference import (  # noqa: E402
    decode_to_audio,
    encode_audio,
    generate_long,
    init_model,
    load_codec_model,
)


@dataclass(frozen=True)
class WordStamp:
    word: str
    start: float
    end: float


@dataclass(frozen=True)
class Candidate:
    index: int
    seed: int
    variant_name: str
    synthesis_text: str
    temperature: float
    top_p: float
    top_k: int
    raw_path: Path
    transcript: str
    aligned_words: tuple[WordStamp, ...]
    cut_seconds: float
    overall_rate: float
    final_clause_rate: float
    chateau_seconds: float
    chantilly_seconds: float
    pause_after_chantilly: float
    count_seconds: float
    countess_seconds: float
    pitch_range: float
    final_clause_pitch_range: float
    final_clause_pitch_deviation_cents: float
    longest_pause: float
    score: float
    passes: bool
    rejection: str


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def protected_audio_snapshot() -> dict[Path, str]:
    """Hash every live voice WAV/meta except the single allowed target WAV."""
    protected: dict[Path, str] = {}
    for path in sorted(VOICE_ROOT.rglob("*")):
        if not path.is_file() or path == TARGET:
            continue
        if path.suffix.lower() == ".wav" or path.name.lower().endswith(".wav.meta"):
            protected[path] = sha256(path)
    return protected


def normalize_word(value: str) -> str:
    return re.sub(r"[^a-z]", "", value.lower())


def word_matches(actual: str, expected: str) -> bool:
    if actual == expected:
        return True
    aliases = {
        "chateau": {"chateau", "shateau", "shato"},
        "countess": {"countess", "countest"},
        "honored": {"honored", "honoured"},
    }
    return actual in aliases.get(expected, {expected})


def read_mono(path: Path) -> np.ndarray:
    audio, source_rate = sf.read(path, always_2d=True, dtype="float32")
    mono = np.nan_to_num(audio.mean(axis=1), nan=0.0, posinf=0.0, neginf=0.0)
    if source_rate != SAMPLE_RATE:
        mono = librosa.resample(
            mono,
            orig_sr=source_rate,
            target_sr=SAMPLE_RATE,
            res_type="soxr_vhq",
        )
    return mono.astype(np.float32, copy=False)


def active_bounds(audio: np.ndarray) -> tuple[int, int]:
    peak = float(np.max(np.abs(audio))) if audio.size else 0.0
    if peak <= 0.0:
        return 0, 0
    active = np.flatnonzero(np.abs(audio) >= max(0.001, peak * 0.015))
    if active.size == 0:
        return 0, 0
    padding = int(0.025 * SAMPLE_RATE)
    return max(0, int(active[0]) - padding), min(audio.size, int(active[-1]) + 1 + padding)


def trim_active(audio: np.ndarray) -> np.ndarray:
    start, end = active_bounds(audio)
    if end <= start:
        raise ValueError("Audio contains no detectable speech.")
    return audio[start:end].copy()


def pitch_range_semitones(audio: np.ndarray) -> float:
    if audio.size < 2048:
        return 0.0
    hop = 256
    f0 = librosa.yin(
        audio,
        fmin=65.0,
        fmax=350.0,
        sr=SAMPLE_RATE,
        frame_length=2048,
        hop_length=hop,
    )
    rms = librosa.feature.rms(y=audio, frame_length=2048, hop_length=hop)[0]
    count = min(f0.size, rms.size)
    threshold = max(0.002, float(np.max(rms[:count])) * 0.08) if count else 0.0
    valid = f0[:count][np.isfinite(f0[:count]) & (rms[:count] >= threshold)]
    if valid.size < 8:
        return 0.0
    low, high = np.percentile(valid, [10.0, 90.0])
    return float(12.0 * math.log2(high / low)) if high > low > 0 else 0.0


def pitch_deviation_cents(audio: np.ndarray) -> float:
    if audio.size < 2048:
        return 0.0
    hop = 256
    f0 = librosa.yin(
        audio,
        fmin=65.0,
        fmax=350.0,
        sr=SAMPLE_RATE,
        frame_length=2048,
        hop_length=hop,
    )
    rms = librosa.feature.rms(y=audio, frame_length=2048, hop_length=hop)[0]
    count = min(f0.size, rms.size)
    threshold = max(0.002, float(np.max(rms[:count])) * 0.08) if count else 0.0
    valid = f0[:count][np.isfinite(f0[:count]) & (rms[:count] >= threshold)]
    if valid.size < 8:
        return 0.0
    cents = 1200.0 * np.log2(valid / np.median(valid))
    return float(np.std(cents))


def longest_internal_pause(audio: np.ndarray) -> float:
    frame = int(0.020 * SAMPLE_RATE)
    hop = int(0.010 * SAMPLE_RATE)
    if audio.size < frame:
        return 0.0
    frames = librosa.util.frame(audio, frame_length=frame, hop_length=hop)
    rms = np.sqrt(np.mean(frames * frames, axis=0))
    threshold = max(0.0015, float(np.max(rms)) * 0.045)
    speech = rms >= threshold
    active = np.flatnonzero(speech)
    if active.size == 0:
        return 0.0
    speech = speech[int(active[0]) : int(active[-1]) + 1]
    longest = 0.0
    gap_start: int | None = None
    for index, voiced in enumerate(speech):
        if not voiced and gap_start is None:
            gap_start = index
        elif voiced and gap_start is not None:
            longest = max(longest, (index - gap_start) * hop / SAMPLE_RATE)
            gap_start = None
    return float(longest)


def require_preflight() -> None:
    for path in (CHECKPOINT, ORIGINAL_REFERENCE, TARGET, TARGET_META, WHISPER_SNAPSHOT):
        if not path.exists():
            raise FileNotFoundError(f"Required path is missing: {path}")
    if not torch.cuda.is_available():
        raise RuntimeError("CUDA is unavailable to Fish Audio S2.")


def generate_candidates(run_dir: Path, count: int, max_new_tokens: int) -> list[dict[str, object]]:
    print("Loading Fish Audio S2-Pro on CUDA...", flush=True)
    precision = torch.bfloat16
    model, decode_one_token = init_model(CHECKPOINT, "cuda", precision, compile=False)
    with torch.device("cuda"):
        model.setup_caches(
            max_batch_size=1,
            max_seq_len=model.config.max_seq_len,
            dtype=next(model.parameters()).dtype,
        )
    codec = load_codec_model(CHECKPOINT / "codec.pth", "cuda", precision)

    reference = trim_active(read_mono(ORIGINAL_REFERENCE))
    reference_path = run_dir / "reference" / "original_human_butler.wav"
    reference_path.parent.mkdir(parents=True, exist_ok=True)
    sf.write(reference_path, reference, SAMPLE_RATE, subtype="PCM_16")
    prompt_text = [f"<|speaker:0|>{ORIGINAL_REFERENCE_TEXT}"]
    prompt_tokens = [encode_audio(reference_path, codec, "cuda").cpu()]

    generated: list[dict[str, object]] = []
    for index in range(1, count + 1):
        variant_name, synthesis_text = SYNTHESIS_VARIANTS[(index - 1) % len(SYNTHESIS_VARIANTS)]
        temperature, top_p, top_k = SAMPLING_PRESETS[(index - 1) % len(SAMPLING_PRESETS)]
        seed = SEED_BASE + (index - 1) * 1009
        torch.manual_seed(seed)
        torch.cuda.manual_seed_all(seed)
        codes = []
        for response in generate_long(
            model=model,
            device="cuda",
            decode_one_token=decode_one_token,
            text=f"<|speaker:0|>{synthesis_text}",
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
            raise RuntimeError(f"Fish returned no audio codes for candidate {index}.")
        audio = decode_to_audio(torch.cat(codes, dim=1).to("cuda"), codec)
        raw_path = run_dir / "raw_candidates" / f"candidate_{index:02d}_{variant_name}_{seed}.wav"
        raw_path.parent.mkdir(parents=True, exist_ok=True)
        sf.write(raw_path, audio.cpu().float().numpy(), int(codec.sample_rate), subtype="PCM_16")
        generated.append(
            {
                "index": index,
                "seed": seed,
                "variant_name": variant_name,
                "synthesis_text": synthesis_text,
                "temperature": temperature,
                "top_p": top_p,
                "top_k": top_k,
                "raw_path": raw_path,
            }
        )
        print(f"  generated candidate {index:02d}/{count}: {variant_name}", flush=True)
        torch.cuda.empty_cache()

    del model, decode_one_token, codec, prompt_tokens
    torch.cuda.empty_cache()
    return generated


def load_asr_pipeline():
    from transformers import pipeline

    return pipeline(
        "automatic-speech-recognition",
        model=str(WHISPER_SNAPSHOT),
        device=-1,
    )


def align_words(
    chunks: list[dict[str, object]],
    expected_words: tuple[str, ...],
) -> tuple[WordStamp, ...] | None:
    stamps: list[WordStamp] = []
    for chunk in chunks:
        timestamp = chunk.get("timestamp")
        if not isinstance(timestamp, tuple) or len(timestamp) != 2:
            continue
        start, end = timestamp
        word = normalize_word(str(chunk.get("text", "")))
        if word and start is not None and end is not None:
            stamps.append(WordStamp(word, float(start), float(end)))

    for start_index in range(len(stamps)):
        aligned: list[WordStamp] = []
        cursor = start_index
        for expected in expected_words:
            if cursor >= len(stamps) or not word_matches(stamps[cursor].word, expected):
                break
            aligned.append(stamps[cursor])
            cursor += 1
        if len(aligned) == len(expected_words):
            return tuple(aligned)
    return None


def voiced_word_end(audio: np.ndarray, stamp: WordStamp) -> float:
    """Remove silence that Whisper sometimes assigns to the preceding word."""
    start_sample = max(0, int(stamp.start * SAMPLE_RATE))
    end_sample = min(audio.size, int(stamp.end * SAMPLE_RATE))
    segment = audio[start_sample:end_sample]
    frame_samples = max(1, int(0.010 * SAMPLE_RATE))
    frame_count = segment.size // frame_samples
    if frame_count < 12:
        return stamp.end
    frames = segment[: frame_count * frame_samples].reshape(frame_count, frame_samples)
    rms = np.sqrt(np.mean(frames * frames, axis=1))
    threshold = max(0.0015, float(np.max(rms)) * 0.045)
    active = rms >= threshold
    minimum_word_frames = max(1, int(0.25 / 0.010))
    minimum_silence_frames = max(1, int(0.12 / 0.010))
    for index in range(minimum_word_frames, frame_count - minimum_silence_frames + 1):
        if active[index]:
            continue
        if not np.any(active[max(0, index - minimum_word_frames) : index]):
            continue
        if not np.any(active[index : index + minimum_silence_frames]):
            return stamp.start + index * 0.010
    return stamp.end


def construct_canonical_audio(
    audio: np.ndarray,
    aligned: tuple[WordStamp, ...],
    apply_edit_fades: bool,
) -> tuple[np.ndarray, np.ndarray]:
    """Remove the temporary connective and insert the intended sentence pause."""
    first_start = max(0, int((aligned[0].start - 0.025) * SAMPLE_RATE))
    first_end = min(audio.size, int((aligned[7].end + 0.020) * SAMPLE_RATE))
    second_start = max(0, int((aligned[9].start - 0.020) * SAMPLE_RATE))
    countess_end = voiced_word_end(audio, aligned[15])
    second_end = min(audio.size, int((countess_end + 0.025) * SAMPLE_RATE))
    first = audio[first_start:first_end].copy()
    second = audio[second_start:second_end].copy()
    if not first.size or not second.size:
        raise ValueError("Unable to construct canonical audio from aligned words.")
    if apply_edit_fades:
        fade = min(first.size, second.size, max(1, int(EDIT_FADE_SECONDS * SAMPLE_RATE)))
        first[-fade:] *= np.linspace(1.0, 0.0, fade, dtype=np.float32)
        second[:fade] *= np.linspace(0.0, 1.0, fade, dtype=np.float32)
    pause = np.zeros(int(SENTENCE_PAUSE_SECONDS * SAMPLE_RATE), dtype=np.float32)
    return np.concatenate([first, pause, second]), second


def evaluate_candidate(raw: dict[str, object], asr) -> Candidate:
    raw_path = Path(raw["raw_path"])
    result = asr(str(raw_path), return_timestamps="word")
    transcript = str(result.get("text", "")).strip()
    aligned = align_words(list(result.get("chunks", [])), GENERATION_WORDS)
    rejection = ""
    if aligned is None:
        rejection = "offline ASR did not confirm the complete canonical line"
        return Candidate(
            int(raw["index"]),
            int(raw["seed"]),
            str(raw["variant_name"]),
            str(raw["synthesis_text"]),
            float(raw["temperature"]),
            float(raw["top_p"]),
            int(raw["top_k"]),
            raw_path,
            transcript,
            (),
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            100.0,
            False,
            rejection,
        )

    audio = read_mono(raw_path)
    cut_audio, final_clause_audio = construct_canonical_audio(audio, aligned, False)
    cut_seconds = cut_audio.size / SAMPLE_RATE
    first_duration = max(0.001, aligned[7].end - aligned[0].start)
    countess_end = voiced_word_end(audio, aligned[15])
    final_clause_duration = max(0.001, countess_end - aligned[9].start)
    active_duration = first_duration + SENTENCE_PAUSE_SECONDS + final_clause_duration
    overall_rate = len(EXPECTED_WORDS) / active_duration
    final_clause_rate = 7.0 / final_clause_duration
    chateau_seconds = aligned[6].end - aligned[6].start
    chantilly_seconds = aligned[7].end - aligned[7].start
    pause_after_chantilly = SENTENCE_PAUSE_SECONDS
    count_seconds = aligned[13].end - aligned[13].start
    countess_seconds = countess_end - aligned[15].start
    pitch_range = pitch_range_semitones(cut_audio)
    final_clause_pitch = pitch_range_semitones(final_clause_audio)
    final_clause_deviation = pitch_deviation_cents(final_clause_audio)
    longest_pause = longest_internal_pause(cut_audio)

    failures: list[str] = []
    if not 2.35 <= overall_rate <= 3.30:
        failures.append("overall cadence outside 2.35-3.30 words/sec")
    if not 2.55 <= final_clause_rate <= 3.95:
        failures.append("final clause cadence outside 2.55-3.95 words/sec")
    if not 0.25 <= chateau_seconds <= 0.50:
        failures.append("Chateau duration outside 0.25-0.50 sec")
    if not 0.35 <= chantilly_seconds <= 0.70:
        failures.append("Chantilly duration outside 0.35-0.70 sec")
    if not 0.12 <= pause_after_chantilly <= 0.42:
        failures.append("pause after Chantilly outside 0.12-0.42 sec")
    if not 0.18 <= count_seconds <= 0.42:
        failures.append("Count duration outside 0.18-0.42 sec")
    if not 0.30 <= countess_seconds <= 0.70:
        failures.append("Countess duration outside 0.30-0.70 sec")
    if pitch_range < 6.8:
        failures.append("overall pitch range below 6.8 semitones")
    if final_clause_pitch < 3.8:
        failures.append("final clause pitch range below 3.8 semitones")
    if final_clause_deviation < 140.0:
        failures.append("final clause pitch deviation below 140 cents")
    if longest_pause > 0.48:
        failures.append("internal pause exceeds 0.48 sec")

    # Favor an unhurried but conversational address, a compact proper-name
    # pronunciation, and an expressive final clause. No tempo correction is used.
    score = (
        abs(overall_rate - 2.75) * 1.4
        + abs(final_clause_rate - 3.05) * 1.1
        + abs(chateau_seconds - 0.36) * 0.8
        + abs(chantilly_seconds - 0.50) * 2.0
        + abs(pause_after_chantilly - 0.20) * 0.8
        + abs(count_seconds - 0.23) * 0.4
        + abs(countess_seconds - 0.38) * 0.5
        + max(0.0, 7.6 - pitch_range) * 0.10
        + max(0.0, 5.4 - final_clause_pitch) * 0.18
        + max(0.0, 175.0 - final_clause_deviation) * 0.002
        + max(0.0, longest_pause - 0.30) * 0.7
    )
    rejection = "; ".join(failures)
    return Candidate(
        int(raw["index"]),
        int(raw["seed"]),
        str(raw["variant_name"]),
        str(raw["synthesis_text"]),
        float(raw["temperature"]),
        float(raw["top_p"]),
        int(raw["top_k"]),
        raw_path,
        transcript,
        aligned,
        cut_seconds,
        overall_rate,
        final_clause_rate,
        chateau_seconds,
        chantilly_seconds,
        pause_after_chantilly,
        count_seconds,
        countess_seconds,
        pitch_range,
        final_clause_pitch,
        final_clause_deviation,
        longest_pause,
        score,
        not failures,
        rejection,
    )


def master(selected: Candidate, output_path: Path) -> None:
    audio = read_mono(selected.raw_path)
    active, _ = construct_canonical_audio(audio, selected.aligned_words, True)
    fade_samples = min(active.size, max(1, int(FADE_SECONDS * SAMPLE_RATE)))
    active[-fade_samples:] *= np.linspace(1.0, 0.0, fade_samples, dtype=np.float32)
    peak = float(np.max(np.abs(active))) if active.size else 0.0
    if peak <= 0.0:
        raise ValueError("Selected candidate is silent.")
    active *= min(TARGET_PEAK / peak, 32.0)
    final = np.concatenate(
        [active, np.zeros(int(TAIL_SECONDS * SAMPLE_RATE), dtype=np.float32)]
    )
    output_path.parent.mkdir(parents=True, exist_ok=True)
    sf.write(output_path, np.clip(final, -0.999, 0.999), SAMPLE_RATE, subtype="PCM_16")


def validate_final(path: Path, asr) -> dict[str, object]:
    info = sf.info(path)
    if info.samplerate != SAMPLE_RATE or info.channels != 1 or info.subtype != "PCM_16":
        raise ValueError(f"Unexpected final WAV format: {info}")
    audio = read_mono(path)
    peak = float(np.max(np.abs(audio))) if audio.size else 0.0
    if not 0.68 <= peak <= 0.72:
        raise ValueError(f"Unexpected mastered peak: {peak:.4f}")
    tail = audio[-int(0.10 * SAMPLE_RATE) :]
    if tail.size and float(np.max(np.abs(tail))) > 0.001:
        raise ValueError("Final clip does not have a quiet interruption tail.")
    result = asr(str(path), return_timestamps="word")
    if align_words(list(result.get("chunks", [])), EXPECTED_WORDS) is None:
        raise ValueError(f"Final transcript failed canonical validation: {result.get('text')}")
    return {
        "duration": info.duration,
        "peak": peak,
        "transcript": str(result.get("text", "")).strip(),
    }


def write_report(
    run_dir: Path,
    candidates: list[Candidate],
    selected: Candidate,
    final_stats: dict[str, object],
    meta_before: str,
    meta_after: str,
    protected_count: int,
    elapsed: float,
) -> None:
    lines = [
        "# Butler Pre-Scare Natural Delivery — Fish Audio S2",
        "",
        "Targeted regeneration of only `SUB_CH02_BUTLER_ADDRESS_GUESTS_001.wav`.",
        "",
        f"- Canonical text: {CANONICAL_TEXT}",
        f"- Original human reference: `{ORIGINAL_REFERENCE}`",
        "- Synthesized-reference inputs: `None`",
        "- Tempo/time-stretch processing: `None`",
        f"- Candidates generated: `{len(candidates)}`",
        f"- Selected candidate: `#{selected.index}` (`{selected.variant_name}`)",
        f"- Selected transcript: {selected.transcript}",
        f"- Final transcript: {final_stats['transcript']}",
        f"- Final duration: `{float(final_stats['duration']):.3f}s`",
        f"- Final peak: `{float(final_stats['peak']):.4f}`",
        f"- Unity `.meta` SHA-256 preserved: `{meta_before == meta_after}`",
        f"- Other live voice WAV/meta files verified byte-identical: `{protected_count}`",
        f"- Staging and backup: `{run_dir}`",
        f"- Elapsed: `{elapsed:.1f}s`",
        "",
        "## Candidate audit",
        "",
    ]
    for candidate in candidates:
        lines.extend(
            [
                f"- `#{candidate.index}` seed `{candidate.seed}` / `{candidate.variant_name}` — "
                f"pass=`{candidate.passes}`, score=`{candidate.score:.3f}`",
                f"  - Transcript: {candidate.transcript}",
                (
                    f"  - Rate `{candidate.overall_rate:.2f}` w/s; final clause "
                    f"`{candidate.final_clause_rate:.2f}` w/s; Chantilly "
                    f"`{candidate.chantilly_seconds:.2f}s`; Chateau "
                    f"`{candidate.chateau_seconds:.2f}s`; post-Chantilly pause "
                    f"`{candidate.pause_after_chantilly:.2f}s`; pitch "
                    f"`{candidate.pitch_range:.1f}` st; final pitch "
                    f"`{candidate.final_clause_pitch_range:.1f}` st / "
                    f"`{candidate.final_clause_pitch_deviation_cents:.0f}` cents; "
                    f"Count `{candidate.count_seconds:.2f}s`; Countess "
                    f"`{candidate.countess_seconds:.2f}s`; longest pause "
                    f"`{candidate.longest_pause:.2f}s`"
                ),
                f"  - Rejection: {candidate.rejection or 'none'}",
            ]
        )
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--candidates", type=int, default=20)
    parser.add_argument("--max-new-tokens", type=int, default=520)
    parser.add_argument("--resume-run-dir", type=Path)
    args = parser.parse_args()
    if args.candidates < 12:
        raise ValueError("At least 12 candidates are required for this high-scrutiny pass.")

    require_preflight()
    started = datetime.now().strftime("%Y%m%d_%H%M%S")
    run_dir = args.resume_run_dir or (
        Path("/tmp") / f"chateau_butler_prescare_fish_s2_{started}"
    )
    run_dir.mkdir(parents=True, exist_ok=args.resume_run_dir is not None)
    meta_before = sha256(TARGET_META)
    protected_before = protected_audio_snapshot()
    started_clock = time.perf_counter()

    if args.resume_run_dir is None:
        generated = generate_candidates(run_dir, args.candidates, args.max_new_tokens)
    else:
        generated = []
        variants = dict(SYNTHESIS_VARIANTS)
        for raw_path in sorted((run_dir / "raw_candidates").glob("candidate_*.wav")):
            parts = raw_path.stem.split("_")
            index = int(parts[1])
            seed = int(parts[-1])
            variant_name = "_".join(parts[2:-1])
            temperature, top_p, top_k = SAMPLING_PRESETS[
                (index - 1) % len(SAMPLING_PRESETS)
            ]
            generated.append(
                {
                    "index": index,
                    "seed": seed,
                    "variant_name": variant_name,
                    "synthesis_text": variants[variant_name],
                    "temperature": temperature,
                    "top_p": top_p,
                    "top_k": top_k,
                    "raw_path": raw_path,
                }
            )
        if len(generated) < 12:
            raise RuntimeError(
                f"Resume directory contains only {len(generated)} candidates; need 12."
            )
    print("Loading offline Whisper word-timestamp validator on CPU...", flush=True)
    asr = load_asr_pipeline()
    candidates: list[Candidate] = []
    for raw in generated:
        candidate = evaluate_candidate(raw, asr)
        candidates.append(candidate)
        print(
            f"  candidate {candidate.index:02d}: pass={candidate.passes} "
            f"rate={candidate.overall_rate:.2f} Chantilly={candidate.chantilly_seconds:.2f}s "
            f"ending_pitch={candidate.final_clause_pitch_range:.1f}st "
            f"score={candidate.score:.3f}",
            flush=True,
        )

    passing = [candidate for candidate in candidates if candidate.passes]
    if len(passing) < 2:
        raise RuntimeError(
            f"Only {len(passing)} candidate(s) passed; the existing asset was not changed."
        )
    selected = min(passing, key=lambda candidate: candidate.score)
    final_path = run_dir / "final" / TARGET.name
    master(selected, final_path)
    final_stats = validate_final(final_path, asr)

    backup_path = run_dir / "asset_backup" / TARGET.name
    backup_path.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(TARGET, backup_path)
    shutil.copy2(final_path, TARGET)
    meta_after = sha256(TARGET_META)
    if meta_before != meta_after:
        shutil.copy2(backup_path, TARGET)
        raise RuntimeError("Unity metadata changed unexpectedly; restored the previous asset.")
    protected_after = protected_audio_snapshot()
    if protected_before != protected_after:
        shutil.copy2(backup_path, TARGET)
        changed = sorted(
            str(path)
            for path in set(protected_before) | set(protected_after)
            if protected_before.get(path) != protected_after.get(path)
        )
        raise RuntimeError(
            "A non-target voice asset changed during generation; restored the target. "
            f"Unexpected paths: {changed}"
        )

    elapsed = time.perf_counter() - started_clock
    write_report(
        run_dir,
        candidates,
        selected,
        final_stats,
        meta_before,
        meta_after,
        len(protected_before),
        elapsed,
    )
    print(f"Installed candidate #{selected.index} into {TARGET}", flush=True)
    print(f"Staging and backup: {run_dir}", flush=True)
    print(f"Report: {REPORT}", flush=True)


if __name__ == "__main__":
    main()
