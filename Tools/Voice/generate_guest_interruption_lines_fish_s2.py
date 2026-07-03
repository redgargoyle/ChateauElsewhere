#!/usr/bin/env python3
"""Generate guest interruption barks with Fish Audio S2-Pro.

Run from the project root with:
micromamba run -n fish-speech-s2 python Tools/Voice/generate_guest_interruption_lines_fish_s2.py
"""

from __future__ import annotations

import argparse
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
from scipy.signal import welch


HOME = Path.home()
PROJECT_ROOT = HOME / "Desktop" / "ChataeuChantilly"
FISH_ROOT = HOME / "ai-tts" / "fish-speech-s2" / "fish-speech-src"
CHECKPOINT = FISH_ROOT / "checkpoints" / "s2-pro"
REFERENCE_ROOT = HOME / "Desktop" / "FishAudio_S2_YoutubeRefs_Chateau_Arrivals_Curated_20260619_210017" / "final_48k"
ASSET_ROOT = PROJECT_ROOT / "Assets" / "Audio" / "Voice" / "Guests"
VOICE_ROOT = PROJECT_ROOT / "Tools" / "Voice"
GENERATED_ROOT = VOICE_ROOT / "generated_fish_s2_guest_interruptions"
REPORT_ROOT = VOICE_ROOT / "reports"
FINAL_SR = 44100
TARGET_PEAK = 10 ** (-3.0 / 20.0)
BASE_TAIL_SECONDS = 0.60
LONG_TAIL_SECONDS = 0.85
TAIL_ANALYSIS_SECONDS = 0.12
FADE_SECONDS = 0.020
LINE_TEXT = "You inturrupted me."

sys.path.insert(0, str(FISH_ROOT))

from fish_speech.models.text2semantic.inference import (  # noqa: E402
    decode_to_audio,
    encode_audio,
    generate_long,
    init_model,
    load_codec_model,
)


@dataclass(frozen=True)
class GuestConfig:
    number: int
    folder: str
    display_name: str
    reference_file: Path
    reference_text: str
    seed: int
    max_new_tokens: int = 220


@dataclass(frozen=True)
class GeneratedLine:
    config: GuestConfig
    line_id: str
    seed: int
    raw_path: Path
    final_path: Path
    asset_path: Path
    duration: float
    peak: float
    pre_repair_tail_peak: float
    added_tail_seconds: float
    generated_4_8k_pct: float
    generated_8_12k_pct: float
    generated_centroid_hz: float
    elapsed_seconds: float


GUESTS = [
    GuestConfig(
        1,
        "Guest01",
        "Miss Isolde Wren",
        REFERENCE_ROOT / "Guest01_Lady_CH1_G01_ENTRY.wav",
        "Good evening. I trust the house remembers its manners better than the weather does.",
        71177,
        200,
    ),
    GuestConfig(
        2,
        "Guest02",
        "Professor Lucien Vale",
        REFERENCE_ROOT / "Guest02_Butler_Guest_CH1_G02_ENTRY.wav",
        "Thank you. The drive was longer in the dark than I care to admit.",
        72277,
    ),
    GuestConfig(
        3,
        "Guest03",
        "Mister Florian Knell",
        REFERENCE_ROOT / "Guest03_Mister_Florian_Knell_CH1_G03_ENTRY.wav",
        "Lovely to see you, dear Butler. Tell me, are we late, early, or merely dramatic?",
        73377,
    ),
    GuestConfig(
        4,
        "Guest04",
        "Countess Elowen Dusk",
        REFERENCE_ROOT / "Guest04_Countess_Elowen_Dusk_CH1_G04_ENTRY.wav",
        "Good evening, Butler. The road up here has the cheerful shape of a warning.",
        74477,
    ),
    GuestConfig(
        5,
        "Guest05",
        "Baron Hector Glass",
        REFERENCE_ROOT / "Guest05_Baron_Hector_Glass_CH1_G05_ENTRY.wav",
        "Good evening. I hope the evening has not started without us.",
        75577,
    ),
    GuestConfig(
        6,
        "Guest06",
        "Lady Sabine Marrow",
        REFERENCE_ROOT / "Guest06_Lady_Sabine_Marrow_CH1_G06_ENTRY.wav",
        "Thank you. I nearly mistook the bell pull for a funeral cord.",
        76677,
    ),
    GuestConfig(
        7,
        "Guest07",
        "Lord Ambrose Veil",
        REFERENCE_ROOT / "Guest07_Lord_Ambrose_Veil_CH1_G07_ENTRY.wav",
        "Lovely to see you. The chateau looks almost awake tonight.",
        77777,
    ),
    GuestConfig(
        8,
        "Guest08",
        "Madame Coralie Thread",
        REFERENCE_ROOT / "Guest08_Madame_Coralie_Thread_CH1_G08_ENTRY.wav",
        "Good evening, Butler. I see the house has chosen its most severe face.",
        78877,
    ),
]

GUEST_BY_NUMBER = {config.number: config for config in GUESTS}


def parse_guest_selection(value: str) -> list[GuestConfig]:
    clean = (value or "all").strip().lower()
    if clean == "all":
        return list(GUESTS)

    selected: list[GuestConfig] = []
    seen: set[int] = set()
    for item in clean.split(","):
        item = item.strip()
        if not item:
            continue

        try:
            guest_number = int(item)
        except ValueError as exc:
            raise argparse.ArgumentTypeError(f"Invalid guest number '{item}'. Use 1-8 or all.") from exc

        if guest_number not in GUEST_BY_NUMBER:
            raise argparse.ArgumentTypeError(f"Guest {guest_number} is outside the supported 1-8 range.")
        if guest_number in seen:
            continue

        selected.append(GUEST_BY_NUMBER[guest_number])
        seen.add(guest_number)

    if not selected:
        raise argparse.ArgumentTypeError("No guests selected. Use 1-8 or all.")

    return selected


def run_text(command: list[str]) -> str:
    try:
        return subprocess.run(command, check=True, text=True, capture_output=True).stdout.strip()
    except Exception as exc:
        return f"unavailable: {exc}"


def require_preflight(selected_guests: list[GuestConfig]) -> None:
    required = [PROJECT_ROOT / "Assets", PROJECT_ROOT / "ProjectSettings", PROJECT_ROOT / "Packages"]
    missing = [str(path) for path in required if not path.exists()]
    if missing:
        raise RuntimeError(f"Not at expected Unity project root; missing: {missing}")
    if not CHECKPOINT.exists():
        raise FileNotFoundError(f"Missing Fish S2 checkpoint: {CHECKPOINT}")
    missing_refs = [str(config.reference_file) for config in selected_guests if not config.reference_file.exists()]
    if missing_refs:
        raise FileNotFoundError(f"Missing Fish S2 guest references: {missing_refs}")
    if not torch.cuda.is_available():
        raise RuntimeError("CUDA is not available in fish-speech-s2")
    device_name = torch.cuda.get_device_name(0)
    if "RTX 5090" not in device_name:
        raise RuntimeError(f"Expected RTX 5090, got {device_name}")


def spectrum(path: Path) -> dict[str, float]:
    data, sample_rate = sf.read(path, always_2d=True, dtype="float32")
    mono = data.mean(axis=1)
    if mono.size == 0:
        return {
            "duration": 0.0,
            "peak": 0.0,
            "centroid_hz": 0.0,
            "band_4_8k_pct": 0.0,
            "band_8_12k_pct": 0.0,
        }
    freqs, psd = welch(mono, sample_rate, nperseg=min(4096, mono.size))
    total = float(np.sum(psd)) or 1.0

    def band(lo: int, hi: int) -> float:
        mask = (freqs >= lo) & (freqs < hi)
        return float(np.sum(psd[mask]) / total * 100.0)

    return {
        "duration": float(mono.size / sample_rate),
        "peak": float(np.max(np.abs(mono))),
        "centroid_hz": float(np.sum(freqs * psd) / np.sum(psd)) if np.sum(psd) else 0.0,
        "band_4_8k_pct": band(4000, 8000),
        "band_8_12k_pct": band(8000, 12000),
    }


def load_mono_delivery_rate(path: Path) -> np.ndarray:
    data, sample_rate = sf.read(path, always_2d=True, dtype="float32")
    mono = np.nan_to_num(data.mean(axis=1), nan=0.0, posinf=0.0, neginf=0.0)
    if sample_rate != FINAL_SR:
        mono = soxr.resample(mono, sample_rate, FINAL_SR, quality="VHQ")
    return mono.astype(np.float32, copy=False)


def add_post_roll(audio: np.ndarray) -> tuple[np.ndarray, float, float]:
    tail_samples = min(audio.size, int(FINAL_SR * TAIL_ANALYSIS_SECONDS))
    before_tail_peak = float(np.max(np.abs(audio[-tail_samples:]))) if tail_samples else 0.0
    tail_seconds = LONG_TAIL_SECONDS if before_tail_peak > 0.035 else BASE_TAIL_SECONDS

    repaired = audio.copy()
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


def validate_wav(path: Path) -> tuple[float, float]:
    info = sf.info(path)
    if info.samplerate != FINAL_SR:
        raise ValueError(f"{path} sample rate is {info.samplerate}, expected {FINAL_SR}")
    if info.channels != 1:
        raise ValueError(f"{path} has {info.channels} channels, expected mono")
    if info.subtype != "PCM_16":
        raise ValueError(f"{path} subtype is {info.subtype}, expected PCM_16")
    spec = spectrum(path)
    if spec["peak"] <= 0.001:
        raise ValueError(f"{path} appears silent")
    if spec["peak"] >= 1.0:
        raise ValueError(f"{path} clips")
    if spec["duration"] < 0.7:
        raise ValueError(f"{path} is too short: {spec['duration']:.3f}s")
    data, _ = sf.read(path, always_2d=True, dtype="float32")
    tail = data[-int(FINAL_SR * 0.25) :]
    if tail.size and float(np.max(np.abs(tail))) > 0.001:
        raise ValueError(f"{path} does not have a quiet ending tail")
    return spec["peak"], spec["duration"]


def generate_line(
    config: GuestConfig,
    model,
    decode_one_token,
    codec,
    raw_dir: Path,
    final_dir: Path,
    prompt_tokens: list[torch.Tensor],
    args: argparse.Namespace,
) -> GeneratedLine:
    line_id = f"CH1_G{config.number:02d}_INTERRUPTED"
    output_name = f"{line_id}.wav"
    raw_path = raw_dir / config.folder / output_name
    final_path = final_dir / config.folder / output_name
    asset_path = ASSET_ROOT / config.folder / output_name
    start = time.perf_counter()

    seed = config.seed + args.seed_offset
    torch.manual_seed(seed)
    torch.cuda.manual_seed(seed)
    torch.cuda.manual_seed_all(seed)
    codes = []
    for response in generate_long(
        model=model,
        device="cuda",
        decode_one_token=decode_one_token,
        text=f"<|speaker:0|>{LINE_TEXT}",
        num_samples=1,
        max_new_tokens=config.max_new_tokens,
        top_p=args.top_p,
        top_k=args.top_k,
        temperature=args.temperature,
        compile=False,
        iterative_prompt=True,
        chunk_length=300,
        prompt_text=[config.reference_text],
        prompt_tokens=prompt_tokens,
    ):
        if response.action == "sample":
            codes.append(response.codes)
    if not codes:
        raise RuntimeError(f"No Fish S2 codes generated for {line_id}")

    merged_codes = torch.cat(codes, dim=1)
    audio = decode_to_audio(merged_codes.to("cuda"), codec)
    raw_path.parent.mkdir(parents=True, exist_ok=True)
    sf.write(raw_path, audio.cpu().float().numpy(), int(codec.sample_rate), subtype="PCM_16")

    peak, duration, before_tail_peak, tail_seconds = write_final(raw_path, final_path)
    peak, duration = validate_wav(final_path)
    spec = spectrum(final_path)

    asset_path.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(final_path, asset_path)

    return GeneratedLine(
        config=config,
        line_id=line_id,
        seed=seed,
        raw_path=raw_path,
        final_path=final_path,
        asset_path=asset_path,
        duration=duration,
        peak=peak,
        pre_repair_tail_peak=before_tail_peak,
        added_tail_seconds=tail_seconds,
        generated_4_8k_pct=spec["band_4_8k_pct"],
        generated_8_12k_pct=spec["band_8_12k_pct"],
        generated_centroid_hz=spec["centroid_hz"],
        elapsed_seconds=time.perf_counter() - start,
    )


def write_report(
    report_path: Path,
    run_dir: Path,
    raw_dir: Path,
    final_dir: Path,
    generated: list[GeneratedLine],
    started: str,
    elapsed: float,
    args: argparse.Namespace,
) -> None:
    lines = [
        "# Fish Audio S2-Pro Guest Interruption Bark Report",
        "",
        "Generated selected guest coat-pickup interruption barks only.",
        "",
        f"- Started: {started}",
        f"- Elapsed seconds: {elapsed:.1f}",
        f"- Project root: `{PROJECT_ROOT}`",
        f"- Fish source: `{FISH_ROOT}`",
        f"- Fish source commit: `{run_text(['git', '-C', str(FISH_ROOT), 'rev-parse', '--short', 'HEAD'])}`",
        f"- Checkpoint: `{CHECKPOINT}`",
        f"- Device: `{torch.cuda.get_device_name(0) if torch.cuda.is_available() else 'unavailable'}`",
        f"- Torch: `{torch.__version__}`",
        f"- CUDA available: `{torch.cuda.is_available()}`",
        f"- nvidia-smi: `{run_text(['nvidia-smi', '--query-gpu=name,driver_version,memory.total', '--format=csv,noheader'])}`",
        f"- Sampling: temperature={args.temperature}, top_p={args.top_p}, top_k={args.top_k}",
        f"- Seed offset: {args.seed_offset}",
        f"- Selected guests: `{','.join(str(item.config.number) for item in generated)}`",
        f"- Transcript: `{LINE_TEXT}`",
        f"- Output format: {FINAL_SR} Hz, mono, PCM_16 WAV, peak-normalized near -3 dBFS.",
        f"- Ending protection: {BASE_TAIL_SECONDS:.2f}s or {LONG_TAIL_SECONDS:.2f}s quiet post-roll after a short fade.",
        f"- Run folder: `{run_dir}`",
        f"- Raw native folder: `{raw_dir}`",
        f"- Final folder: `{final_dir}`",
        f"- Copied to Unity Assets: `True`",
        "",
        "## Counts",
        "",
        f"- Total generated WAV count: {len(generated)}",
        "",
        "## Reference Clips",
        "",
    ]

    for item in generated:
        config = item.config
        ref_spec = spectrum(config.reference_file)
        lines.extend(
            [
                f"- Guest {config.number:02d} {config.display_name}",
                f"  - Reference file: `{config.reference_file}`",
                f"  - Reference text: `{config.reference_text}`",
                f"  - Reference 4-8k percent: {ref_spec['band_4_8k_pct']:.3f}",
                f"  - Reference 8-12k percent: {ref_spec['band_8_12k_pct']:.3f}",
                f"  - Seed: {item.seed}",
            ]
        )

    lines.extend(["", "## Generated Files", ""])
    for item in generated:
        lines.append(
            f"- Guest {item.config.number:02d} `{item.line_id}.wav`: duration={item.duration:.3f}s, "
            f"peak={item.peak:.4f}, pre_tail_peak={item.pre_repair_tail_peak:.5f}, "
            f"added_tail={item.added_tail_seconds:.2f}s, 4-8k={item.generated_4_8k_pct:.3f}%, "
            f"8-12k={item.generated_8_12k_pct:.3f}%, centroid={item.generated_centroid_hz:.0f}Hz, "
            f"elapsed={item.elapsed_seconds:.1f}s, final=`{item.final_path}`, asset=`{item.asset_path}`"
        )

    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    shutil.copy2(report_path, run_dir / report_path.name)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--guests", type=parse_guest_selection, default=list(GUESTS), help="Comma-separated guest numbers, e.g. 1 or 1,3,8. Use all for every guest.")
    parser.add_argument("--seed-offset", type=int, default=0, help="Deterministic offset added to each guest's base seed.")
    parser.add_argument("--temperature", type=float, default=0.72)
    parser.add_argument("--top-p", type=float, default=0.82)
    parser.add_argument("--top-k", type=int, default=30)
    args = parser.parse_args()
    selected_guests = args.guests

    require_preflight(selected_guests)
    started = datetime.now().strftime("%Y%m%d_%H%M%S")
    run_dir = GENERATED_ROOT / f"guest_interruptions_fish_s2_{started}"
    raw_dir = run_dir / "raw_native"
    final_dir = run_dir / "final_44k1_tail_padded"
    raw_dir.mkdir(parents=True, exist_ok=True)
    final_dir.mkdir(parents=True, exist_ok=True)

    print("Loading Fish S2-Pro model...", flush=True)
    t0 = time.perf_counter()
    precision = torch.bfloat16
    model, decode_one_token = init_model(CHECKPOINT, "cuda", precision, compile=False)
    with torch.device("cuda"):
        model.setup_caches(
            max_batch_size=1,
            max_seq_len=model.config.max_seq_len,
            dtype=next(model.parameters()).dtype,
        )

    print("Loading Fish S2-Pro codec...", flush=True)
    codec = load_codec_model(CHECKPOINT / "codec.pth", "cuda", precision)

    generated: list[GeneratedLine] = []
    for index, config in enumerate(selected_guests, start=1):
        print(f"[Guest{config.number:02d} {index:02d}/{len(selected_guests):02d}] CH1_G{config.number:02d}_INTERRUPTED", flush=True)
        prompt_tokens = [encode_audio(config.reference_file, codec, "cuda").cpu()]
        generated.append(generate_line(config, model, decode_one_token, codec, raw_dir, final_dir, prompt_tokens, args))
        torch.cuda.empty_cache()

    if len(generated) != len(selected_guests):
        raise RuntimeError(f"Generated {len(generated)} files, expected {len(selected_guests)}")

    report_path = REPORT_ROOT / "guest_interruption_generation_report_fish_s2.md"
    write_report(report_path, run_dir, raw_dir, final_dir, generated, started, time.perf_counter() - t0, args)

    print(f"Generated {len(generated)} Fish S2 interruption WAVs", flush=True)
    print(f"Final staging: {final_dir}", flush=True)
    print(f"Assets: {ASSET_ROOT}", flush=True)
    print(f"Report: {report_path}", flush=True)


if __name__ == "__main__":
    main()
