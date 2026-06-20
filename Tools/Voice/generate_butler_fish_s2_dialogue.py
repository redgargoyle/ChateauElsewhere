#!/usr/bin/env python3
"""Generate Butler dialogue with Fish Audio S2-Pro.

Uses a local Fish S2 checkpoint on CUDA, writes mastered WAVs into the Unity
Assets tree, and keeps raw/generated run artifacts under Tools/Voice.
"""

from __future__ import annotations

import shutil
import subprocess
import sys
import time
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path

import numpy as np
import soundfile as sf
import torch


HOME = Path.home()
PROJECT_ROOT = HOME / "Desktop" / "ChataeuChantilly"
FISH_ROOT = HOME / "ai-tts" / "fish-speech-s2" / "fish-speech-src"
CHECKPOINT = FISH_ROOT / "checkpoints" / "s2-pro"
BUTLER_REF_ROOT = HOME / "Desktop" / "FishAudio_S2_Butler_Hello_YoutubeRef_20260620_090942"
ORIGINAL_REFERENCE_FILE = BUTLER_REF_ROOT / "ref" / "butler_youtube_ref.wav"
HELLO_REFERENCE_FILE = BUTLER_REF_ROOT / "final" / "Butler_Hello_FishS2.wav"
ORIGINAL_REFERENCE_TEXT = (
    "Hello and thank you for calling. Unfortunately, the person that you've "
    "called cannot come to the phone at the moment. This is the butler."
)
HELLO_REFERENCE_TEXT = "Hello."
ASSET_DIR = PROJECT_ROOT / "Assets" / "Audio" / "Voices" / "Butler"
VOICE_ROOT = PROJECT_ROOT / "Tools" / "Voice"
GENERATED_ROOT = VOICE_ROOT / "generated_fish_s2_butler_dialogue"
BACKUP_ROOT = VOICE_ROOT / "backups"
REPORT_ROOT = VOICE_ROOT / "reports"
FINAL_SR = 44100
TARGET_PEAK = 10 ** (-3.0 / 20.0)
BASE_TAIL_SECONDS = 0.70
LONG_TAIL_SECONDS = 0.95
TAIL_ANALYSIS_SECONDS = 0.12
FADE_SECONDS = 0.025

sys.path.insert(0, str(FISH_ROOT))

from fish_speech.models.text2semantic.inference import (  # noqa: E402
    decode_to_audio,
    encode_audio,
    generate_long,
    init_model,
    load_codec_model,
)


@dataclass(frozen=True)
class DialogueLine:
    line_id: str
    text: str
    seed: int

    @property
    def output_name(self) -> str:
        return f"{self.line_id}.wav"


@dataclass(frozen=True)
class ReferenceChoice:
    path: Path
    text: str
    reason: str


@dataclass(frozen=True)
class GeneratedLine:
    line: DialogueLine
    raw_path: Path
    final_path: Path
    asset_path: Path
    duration: float
    peak: float
    pre_repair_tail_peak: float
    added_tail_seconds: float
    elapsed_seconds: float


GUEST_NAMES = {
    1: "Lady",
    2: "Butler Guest",
    3: "Mister Florian Knell",
    4: "Countess Elowen Dusk",
    5: "Baron Hector Glass",
    6: "Lady Sabine Marrow",
    7: "Lord Ambrose Veil",
    8: "Madame Coralie Thread",
}

LINES = [
    DialogueLine("SUB_CH01_BUTLER_WELCOME_001", "Good evening. Welcome to Chateau Chantilly.", 82001),
    DialogueLine("SUB_CH01_BUTLER_TAKE_COAT_001", "May I take your coat?", 82002),
    DialogueLine("SUB_CH01_BUTLER_THIS_WAY_001", "This way, please. The Drawing Room is prepared.", 82003),
    DialogueLine("SUB_CH01_BUTLER_ONE_COAT_001", "One coat at a time, if you please.", 82004),
    DialogueLine("SUB_CH01_BUTLER_NO_COAT_001", "I have no coat to hang.", 82005),
    DialogueLine("SUB_CH01_BUTLER_EMPTY_DOOR_001", "No one is there.", 82006),
    DialogueLine(
        "SUB_CH02_BUTLER_ADDRESS_GUESTS_001",
        "Welcome friends and gentlemen, guests of the evening, Count and Countess of Chantilly—",
        82007,
    ),
    *[
        DialogueLine(
            f"SUB_CH02_BUTLER_FOUND_G{guest_number:02d}",
            f"I have found you, {guest_name}. Dinner shall be served in the Dining Room at seven o'clock precisely. Might I record your wishes for the table?",
            82100 + guest_number,
        )
        for guest_number, guest_name in GUEST_NAMES.items()
    ],
    DialogueLine(
        "SUB_CH02_BUTLER_MEAL_ASK_001",
        "For supper, shall I put you down for the fresh monte genellion de plink, or thyme with Lillums?",
        82016,
    ),
    DialogueLine(
        "SUB_CH02_BUTLER_SMOKE_ASK_001",
        "After dinner, shall I prepare a cigar, a pipe, or no smoke at all?",
        82017,
    ),
]


def run_text(command: list[str]) -> str:
    try:
        return subprocess.run(command, check=True, text=True, capture_output=True).stdout.strip()
    except Exception as exc:
        return f"unavailable: {exc}"


def require_preflight() -> None:
    required = [PROJECT_ROOT / "Assets", PROJECT_ROOT / "ProjectSettings", PROJECT_ROOT / "Packages"]
    missing = [str(path) for path in required if not path.exists()]
    if missing:
        raise RuntimeError(f"Not at expected Unity project root; missing: {missing}")
    if not CHECKPOINT.exists():
        raise FileNotFoundError(f"Missing Fish S2 checkpoint: {CHECKPOINT}")
    if not ORIGINAL_REFERENCE_FILE.exists():
        raise FileNotFoundError(f"Missing original Butler reference: {ORIGINAL_REFERENCE_FILE}")
    if not HELLO_REFERENCE_FILE.exists():
        raise FileNotFoundError(f"Missing generated Butler hello reference: {HELLO_REFERENCE_FILE}")
    if not torch.cuda.is_available():
        raise RuntimeError("CUDA is not available in fish-speech-s2")
    device_name = torch.cuda.get_device_name(0)
    if "RTX 5090" not in device_name:
        raise RuntimeError(f"Expected RTX 5090, got {device_name}")


def wav_stats(path: Path) -> dict[str, float | int | str]:
    info = sf.info(path)
    data, _ = sf.read(path, always_2d=True, dtype="float32")
    mono = data.mean(axis=1) if data.size else np.array([], dtype=np.float32)
    peak = float(np.max(np.abs(mono))) if mono.size else 0.0
    rms = float(np.sqrt(np.mean(mono * mono))) if mono.size else 0.0
    return {
        "sample_rate": info.samplerate,
        "channels": info.channels,
        "subtype": info.subtype,
        "duration": info.duration,
        "peak": peak,
        "rms": rms,
    }


def choose_reference() -> ReferenceChoice:
    original = wav_stats(ORIGINAL_REFERENCE_FILE)
    hello = wav_stats(HELLO_REFERENCE_FILE)

    if float(hello["duration"]) >= 4.0 and float(hello["rms"]) >= 0.01:
        return ReferenceChoice(
            HELLO_REFERENCE_FILE,
            HELLO_REFERENCE_TEXT,
            "Generated hello is long enough to serve as a stable Fish prompt.",
        )

    return ReferenceChoice(
        ORIGINAL_REFERENCE_FILE,
        ORIGINAL_REFERENCE_TEXT,
        (
            "Original YouTube reference selected because it is a longer clean Butler sample "
            f"({float(original['duration']):.2f}s) than the generated hello "
            f"({float(hello['duration']):.2f}s)."
        ),
    )


def load_mono_delivery_rate(path: Path) -> np.ndarray:
    data, sample_rate = sf.read(path, always_2d=True, dtype="float32")
    mono = np.nan_to_num(data.mean(axis=1), nan=0.0, posinf=0.0, neginf=0.0)
    if sample_rate != FINAL_SR:
        import soxr

        mono = soxr.resample(mono, sample_rate, FINAL_SR, quality="VHQ")
    return mono.astype(np.float32, copy=False)


def add_post_roll(audio: np.ndarray) -> tuple[np.ndarray, float, float]:
    tail_samples = min(audio.size, int(FINAL_SR * TAIL_ANALYSIS_SECONDS))
    before_tail_peak = float(np.max(np.abs(audio[-tail_samples:]))) if tail_samples else 0.0
    tail_seconds = LONG_TAIL_SECONDS if before_tail_peak > 0.035 else BASE_TAIL_SECONDS

    repaired = audio.copy()
    if repaired.size:
        fade_samples = min(repaired.size, max(1, int(FINAL_SR * FADE_SECONDS)))
        repaired[-fade_samples:] *= np.linspace(1.0, 0.0, fade_samples, dtype=np.float32)
    repaired = np.concatenate([repaired, np.zeros(int(FINAL_SR * tail_seconds), dtype=np.float32)])

    peak = float(np.max(np.abs(repaired))) if repaired.size else 0.0
    if peak > 0:
        repaired *= min(TARGET_PEAK / peak, 32.0)
    repaired = np.clip(repaired, -0.999, 0.999)
    return repaired.astype(np.float32, copy=False), before_tail_peak, tail_seconds


def write_final(input_path: Path, output_path: Path) -> tuple[float, float, float, float]:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    audio = load_mono_delivery_rate(input_path)
    repaired, before_tail_peak, tail_seconds = add_post_roll(audio)
    sf.write(output_path, repaired, FINAL_SR, subtype="PCM_16")
    peak = float(np.max(np.abs(repaired))) if repaired.size else 0.0
    duration = float(repaired.size / FINAL_SR) if repaired.size else 0.0
    return peak, duration, before_tail_peak, tail_seconds


def expected_min_duration(text: str) -> float:
    words = max(1, len(text.split()))
    return max(0.75, min(4.2, words * 0.115))


def validate_wav(path: Path, text: str) -> tuple[float, float]:
    info = sf.info(path)
    if info.samplerate != FINAL_SR:
        raise ValueError(f"{path} sample rate is {info.samplerate}, expected {FINAL_SR}")
    if info.channels != 1:
        raise ValueError(f"{path} has {info.channels} channels, expected mono")
    if info.subtype != "PCM_16":
        raise ValueError(f"{path} subtype is {info.subtype}, expected PCM_16")

    data, _ = sf.read(path, always_2d=True, dtype="float32")
    peak = float(np.max(np.abs(data))) if data.size else 0.0
    duration = float(data.shape[0] / FINAL_SR) if data.size else 0.0
    if peak <= 0.001:
        raise ValueError(f"{path} appears silent")
    if peak >= 1.0:
        raise ValueError(f"{path} clips")
    if duration < expected_min_duration(text):
        raise ValueError(f"{path} is too short: {duration:.3f}s")
    tail = data[-int(FINAL_SR * 0.25) :]
    if tail.size and float(np.max(np.abs(tail))) > 0.001:
        raise ValueError(f"{path} does not have a quiet ending tail")
    return peak, duration


def backup_existing_assets(timestamp: str) -> Path | None:
    existing_wavs = sorted(ASSET_DIR.glob("*.wav")) if ASSET_DIR.exists() else []
    if not existing_wavs:
        return None

    backup_dir = BACKUP_ROOT / f"butler_before_fish_s2_{timestamp}"
    backup_dir.mkdir(parents=True, exist_ok=True)
    for wav_path in existing_wavs:
        shutil.copy2(wav_path, backup_dir / wav_path.name)
        meta_path = wav_path.with_suffix(wav_path.suffix + ".meta")
        if meta_path.exists():
            shutil.copy2(meta_path, backup_dir / meta_path.name)
    return backup_dir


def max_tokens_for_line(text: str) -> int:
    return max(180, min(520, 80 + len(text.split()) * 13))


def generate_line(
    line: DialogueLine,
    model,
    decode_one_token,
    codec,
    prompt_tokens: list[torch.Tensor],
    reference_text: str,
    raw_dir: Path,
    final_dir: Path,
    asset_dir: Path,
) -> GeneratedLine:
    raw_path = raw_dir / line.output_name
    final_path = final_dir / line.output_name
    asset_path = asset_dir / line.output_name
    start = time.perf_counter()
    last_error: Exception | None = None

    for attempt in range(3):
        try:
            torch.manual_seed(line.seed + attempt * 1000)
            torch.cuda.manual_seed(line.seed + attempt * 1000)
            codes = []
            for response in generate_long(
                model=model,
                device="cuda",
                decode_one_token=decode_one_token,
                text=f"<|speaker:0|>{line.text}",
                num_samples=1,
                max_new_tokens=max_tokens_for_line(line.text),
                top_p=0.78 if attempt == 0 else 0.82,
                top_k=28,
                temperature=0.66 if attempt == 0 else 0.70,
                compile=False,
                iterative_prompt=True,
                chunk_length=300,
                prompt_text=[reference_text],
                prompt_tokens=prompt_tokens,
            ):
                if response.action == "sample":
                    codes.append(response.codes)

            if not codes:
                raise RuntimeError(f"No Fish S2 codes generated for {line.line_id}")

            merged_codes = torch.cat(codes, dim=1)
            audio = decode_to_audio(merged_codes.to("cuda"), codec)
            raw_path.parent.mkdir(parents=True, exist_ok=True)
            sf.write(raw_path, audio.cpu().float().numpy(), int(codec.sample_rate), subtype="PCM_16")
            peak, duration, before_tail_peak, tail_seconds = write_final(raw_path, final_path)
            validate_wav(final_path, line.text)
            asset_dir.mkdir(parents=True, exist_ok=True)
            shutil.copy2(final_path, asset_path)
            return GeneratedLine(
                line,
                raw_path,
                final_path,
                asset_path,
                duration,
                peak,
                before_tail_peak,
                tail_seconds,
                time.perf_counter() - start,
            )
        except Exception as exc:
            last_error = exc
            print(f"[retry] {line.line_id} attempt {attempt + 1} failed: {exc}", flush=True)
            torch.cuda.empty_cache()

    raise RuntimeError(f"{line.line_id} failed after retries: {last_error}")


def write_report(
    report_path: Path,
    started: str,
    elapsed: float,
    reference_choice: ReferenceChoice,
    backup_dir: Path | None,
    rows: list[GeneratedLine],
    failures: list[str],
) -> None:
    original_stats = wav_stats(ORIGINAL_REFERENCE_FILE)
    hello_stats = wav_stats(HELLO_REFERENCE_FILE)
    lines = [
        "# Fish Audio S2 Butler Dialogue Generation Report",
        "",
        "Generated local Fish Audio S2-Pro Butler dialogue for Unity import.",
        "",
        f"- Started: {started}",
        f"- Elapsed seconds: {elapsed:.1f}",
        f"- Project root: `{PROJECT_ROOT}`",
        f"- Asset output folder: `{ASSET_DIR}`",
        f"- Fish source: `{FISH_ROOT}`",
        f"- Fish source commit: `{run_text(['git', '-C', str(FISH_ROOT), 'rev-parse', '--short', 'HEAD'])}`",
        f"- Checkpoint: `{CHECKPOINT}`",
        f"- Device: `{torch.cuda.get_device_name(0) if torch.cuda.is_available() else 'unavailable'}`",
        f"- Torch: `{torch.__version__}`",
        f"- CUDA available: `{torch.cuda.is_available()}`",
        f"- nvidia-smi: `{run_text(['nvidia-smi', '--query-gpu=name,driver_version,memory.total', '--format=csv,noheader'])}`",
        f"- Selected reference: `{reference_choice.path}`",
        f"- Reference decision: {reference_choice.reason}",
        f"- Original reference duration: {float(original_stats['duration']):.3f}s, RMS: {float(original_stats['rms']):.5f}",
        f"- Generated hello reference duration: {float(hello_stats['duration']):.3f}s, RMS: {float(hello_stats['rms']):.5f}",
        f"- Existing asset backup: `{backup_dir}`" if backup_dir else "- Existing asset backup: none needed",
        "- Output format: 44100 Hz, mono, PCM_16 WAV, peak-normalized near -3 dBFS, with quiet ending tail.",
        "- Target text: game dialogue only. No style prompt text was inserted into generated speech.",
        "",
        "## Summary",
        "",
        f"- Expected count: {len(LINES)}",
        f"- Generated count: {len(rows)}",
        f"- Failed count: {len(failures)}",
        "",
        "## Files",
        "",
    ]

    for row in rows:
        lines.append(
            f"- `{row.line.line_id}`: duration={row.duration:.3f}s, peak={row.peak:.4f}, "
            f"pre_tail_peak={row.pre_repair_tail_peak:.5f}, added_tail={row.added_tail_seconds:.2f}s, "
            f"elapsed={row.elapsed_seconds:.1f}s, asset=`{row.asset_path}`"
        )
        lines.append(f"  - text: {row.line.text}")

    if failures:
        lines.extend(["", "## Failures", ""])
        lines.extend(f"- {failure}" for failure in failures)

    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> None:
    require_preflight()
    started = datetime.now().strftime("%Y%m%d_%H%M%S")
    run_dir = GENERATED_ROOT / f"butler_fish_s2_full_dialogue_{started}"
    raw_dir = run_dir / "raw_native"
    final_dir = run_dir / "final_44k1_tail_padded"
    raw_dir.mkdir(parents=True, exist_ok=True)
    final_dir.mkdir(parents=True, exist_ok=True)
    REPORT_ROOT.mkdir(parents=True, exist_ok=True)

    reference_choice = choose_reference()
    backup_dir = backup_existing_assets(started)

    t0 = time.perf_counter()
    precision = torch.bfloat16
    print("Loading Fish S2-Pro model on CUDA...", flush=True)
    model, decode_one_token = init_model(CHECKPOINT, "cuda", precision, compile=False)
    with torch.device("cuda"):
        model.setup_caches(
            max_batch_size=1,
            max_seq_len=model.config.max_seq_len,
            dtype=next(model.parameters()).dtype,
        )
    print("Loading Fish S2-Pro codec...", flush=True)
    codec = load_codec_model(CHECKPOINT / "codec.pth", "cuda", precision)

    print(f"Using reference: {reference_choice.path}", flush=True)
    prompt_tokens = [encode_audio(reference_choice.path, codec, "cuda").cpu()]

    rows: list[GeneratedLine] = []
    failures: list[str] = []
    for index, line in enumerate(LINES, start=1):
        print(f"[Butler {index:02d}/{len(LINES):02d}] {line.line_id}", flush=True)
        try:
            rows.append(
                generate_line(
                    line,
                    model,
                    decode_one_token,
                    codec,
                    prompt_tokens,
                    reference_choice.text,
                    raw_dir,
                    final_dir,
                    ASSET_DIR,
                )
            )
        except Exception as exc:
            failures.append(f"{line.line_id}: {exc}")
        finally:
            torch.cuda.empty_cache()

    elapsed = time.perf_counter() - t0
    report_path = REPORT_ROOT / "butler_fish_s2_generation_report.md"
    run_report_path = run_dir / "BUTLER_GENERATION_REPORT.md"
    write_report(report_path, started, elapsed, reference_choice, backup_dir, rows, failures)
    shutil.copy2(report_path, run_report_path)

    if failures:
        print("Failures:", flush=True)
        for failure in failures:
            print(f"  {failure}", flush=True)
        raise SystemExit(1)

    print(f"Asset output: {ASSET_DIR}", flush=True)
    print(f"Run folder: {run_dir}", flush=True)
    print(f"Report: {report_path}", flush=True)


if __name__ == "__main__":
    main()
