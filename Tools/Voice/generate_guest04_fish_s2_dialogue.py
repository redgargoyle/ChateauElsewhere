#!/usr/bin/env python3
"""Generate Chateau Guest 4 dialogue with Fish Audio S2-Pro.

Uses the approved Guest 4 arrival example as the fixed reference voice,
generates Guest 4 only, exports 44.1 kHz mono PCM_16 WAVs with a short ending
post-roll, backs up Guest04, and replaces only Guest04 WAVs in Assets.
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
REFERENCE_FILE = (
    HOME
    / "Desktop"
    / "FishAudio_S2_YoutubeRefs_Chateau_Arrivals_Curated_20260619_210017"
    / "final_48k"
    / "Guest04_Countess_Elowen_Dusk_CH1_G04_ENTRY.wav"
)
REFERENCE_TEXT = "Good evening, Butler. The road up here has the cheerful shape of a warning."
ASSET_DIR = PROJECT_ROOT / "Assets" / "Audio" / "Voice" / "Guests" / "Guest04"
VOICE_ROOT = PROJECT_ROOT / "Tools" / "Voice"
BACKUP_ROOT = VOICE_ROOT / "backups"
GENERATED_ROOT = VOICE_ROOT / "generated_fish_s2_guest_dialogue"
REPORT_ROOT = VOICE_ROOT / "reports"
FINAL_SR = 44100
TARGET_PEAK = 10 ** (-3.0 / 20.0)
BASE_TAIL_SECONDS = 0.60
LONG_TAIL_SECONDS = 0.85
TAIL_ANALYSIS_SECONDS = 0.12
FADE_SECONDS = 0.020

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
class GeneratedLine:
    line: DialogueLine
    mode: str
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


LINES = [
    DialogueLine("CH1_G04_ENTRY", "Good evening, Butler. The road up here has the cheerful shape of a warning.", 74401),
    DialogueLine("CH1_G04_DELAYED", "At last. I had begun composing my obituary in the frost.", 74402),
    DialogueLine("CH1_G04_COAT_HANDOFF", "Take it before it decides to stay here without me.", 74403),
    DialogueLine("CH1_G04_TO_DRAWING_ROOM", "Prepared rooms and prepared excuses often look alike. Lead on.", 74404),
    DialogueLine("CH1_G04_AMBIENT_01", "The drawing room should be warmer.", 74405),
    DialogueLine("CH1_G04_AMBIENT_02", "Old houses groan. This one seems to choose its words.", 74406),
    DialogueLine("CH1_G04_EMPTY_BELL_REACTION", "Doors do not summon themselves. Not respectable doors, anyway.", 74407),
    DialogueLine("CH2_G04_PRESPEECH_BARK", "It is never a good sign when the servants make speeches.", 74408),
    DialogueLine("CH2_G04_PANIC", "Down! Get down!", 74409),
    DialogueLine("CH2_G04_FOUND_START", "If you are here to say dinner is canceled, lie more elegantly.", 74410),
    DialogueLine("CH2_G04_FOUND_REPLY", "Good. A schedule is a flimsy shield, but it is a shield.", 74411),
    DialogueLine("CH2_G04_MEAL_PLINK", "Fresh monte genellion de plink. If the name is a trap, I expect you to spring it first.", 74412),
    DialogueLine("CH2_G04_MEAL_THYME", "Thyme with Lillums. Quiet food. Sensible food. Food unlikely to chase me.", 74413),
    DialogueLine("CH2_G04_SMOKE_CIGAR", "A cigar. If I am to be hunted by architecture, I shall smell expensive.", 74414),
    DialogueLine("CH2_G04_SMOKE_PIPE", "A pipe. It gives the hands something to do besides tremble.", 74415),
    DialogueLine("CH2_G04_SMOKE_NONE", "No smoke. I prefer to see what is coming.", 74416),
    DialogueLine("CH2_G04_SPIRITS_REPLY", "Good. I distrust a dinner table without witnesses.", 74417),
    DialogueLine("CH2_G04_EXIT_TO_DINING", "I will be in the Dining Room at seven—assuming the house still permits a civilized schedule.", 74418),
    DialogueLine("CH2_G04_CLOCK_REACTION", "The clock sounds pleased with itself. I resent that.", 74419),
    DialogueLine("CH2_G04_DINING_REVEAL", "If the soup screams, I am leaving.", 74420),
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
    if not REFERENCE_FILE.exists():
        raise FileNotFoundError(f"Missing Guest 4 Fish reference: {REFERENCE_FILE}")
    if not CHECKPOINT.exists():
        raise FileNotFoundError(f"Missing Fish S2 checkpoint: {CHECKPOINT}")
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


def expected_min_duration(text: str) -> float:
    words = max(1, len(text.split()))
    return max(0.65, min(3.0, words * 0.12))


def validate_wav(path: Path, text: str) -> tuple[float, float]:
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
    if spec["duration"] < expected_min_duration(text):
        raise ValueError(f"{path} is too short: {spec['duration']:.3f}s")
    data, _ = sf.read(path, always_2d=True, dtype="float32")
    tail = data[-int(FINAL_SR * 0.25) :]
    if tail.size and float(np.max(np.abs(tail))) > 0.001:
        raise ValueError(f"{path} does not have a quiet ending tail")
    return spec["peak"], spec["duration"]


def generate_line(
    line: DialogueLine,
    model,
    decode_one_token,
    codec,
    raw_dir: Path,
    final_dir: Path,
    prompt_tokens: list[torch.Tensor],
    max_new_tokens: int,
    temperature: float,
    top_p: float,
    top_k: int,
) -> GeneratedLine:
    raw_path = raw_dir / line.output_name
    final_path = final_dir / line.output_name
    asset_path = ASSET_DIR / line.output_name
    start = time.perf_counter()

    if line.line_id == "CH1_G04_ENTRY":
        raw_path.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(REFERENCE_FILE, raw_path)
        mode = "approved_reference_copy"
    else:
        torch.manual_seed(line.seed)
        torch.cuda.manual_seed(line.seed)
        codes = []
        for response in generate_long(
            model=model,
            device="cuda",
            decode_one_token=decode_one_token,
            text=f"<|speaker:0|>{line.text}",
            num_samples=1,
            max_new_tokens=max_new_tokens,
            top_p=top_p,
            top_k=top_k,
            temperature=temperature,
            compile=False,
            iterative_prompt=True,
            chunk_length=300,
            prompt_text=[REFERENCE_TEXT],
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
        mode = "fish_s2_synthesis"

    peak, duration, before_tail_peak, tail_seconds = write_final(raw_path, final_path)
    peak, duration = validate_wav(final_path, line.text)
    spec = spectrum(final_path)
    return GeneratedLine(
        line=line,
        mode=mode,
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


def validate_final_set(final_dir: Path) -> None:
    wavs = sorted(final_dir.glob("*.wav"))
    if len(wavs) != len(LINES):
        raise RuntimeError(f"Expected {len(LINES)} WAVs, found {len(wavs)}")
    expected = {line.output_name for line in LINES}
    actual = {path.name for path in wavs}
    if expected != actual:
        raise RuntimeError(f"Missing={sorted(expected - actual)}, unexpected={sorted(actual - expected)}")
    for line in LINES:
        validate_wav(final_dir / line.output_name, line.text)


def backup_guest04(timestamp: str) -> Path | None:
    if not ASSET_DIR.exists():
        return None
    BACKUP_ROOT.mkdir(parents=True, exist_ok=True)
    backup_dir = BACKUP_ROOT / f"guest04_before_fish_s2_{timestamp}"
    shutil.copytree(ASSET_DIR, backup_dir)
    return backup_dir


def copy_to_assets(final_dir: Path) -> None:
    ASSET_DIR.mkdir(parents=True, exist_ok=True)
    for line in LINES:
        shutil.copy2(final_dir / line.output_name, ASSET_DIR / line.output_name)


def write_report(
    report_path: Path,
    run_dir: Path,
    raw_dir: Path,
    final_dir: Path,
    generated: list[GeneratedLine],
    backup_dir: Path | None,
    started: str,
    elapsed: float,
    args: argparse.Namespace,
) -> None:
    ref_spec = spectrum(REFERENCE_FILE)
    lines = [
        "# Fish Audio S2-Pro Guest 4 Full Dialogue Report",
        "",
        "Generated Guest 4 only. Unity gameplay, subtitles, and playback hooks were not modified.",
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
        f"- Reference file: `{REFERENCE_FILE}`",
        f"- Reference text: `{REFERENCE_TEXT}`",
        f"- Reference 4-8k percent: {ref_spec['band_4_8k_pct']:.3f}",
        f"- Reference 8-12k percent: {ref_spec['band_8_12k_pct']:.3f}",
        f"- Sampling: temperature={args.temperature}, top_p={args.top_p}, top_k={args.top_k}, max_new_tokens={args.max_new_tokens}",
        "- Target text: game dialogue only. No accent/style instruction tags were inserted into generated text.",
        f"- Output format: {FINAL_SR} Hz, mono, PCM_16 WAV, peak-normalized near -3 dBFS.",
        f"- Ending protection: {BASE_TAIL_SECONDS:.2f}s or {LONG_TAIL_SECONDS:.2f}s quiet post-roll after a short fade.",
        f"- Run folder: `{run_dir}`",
        f"- Raw native folder: `{raw_dir}`",
        f"- Final folder: `{final_dir}`",
        f"- Copied to Unity Assets: `{not args.no_copy_assets}`",
        f"- Guest04 backup: `{backup_dir}`",
        "",
        "## Counts",
        "",
        f"- Total Guest 4 WAV count: {len(generated)}",
        f"- Synthesized count: {sum(1 for item in generated if item.mode == 'fish_s2_synthesis')}",
        f"- Reference-copy count: {sum(1 for item in generated if item.mode == 'approved_reference_copy')}",
        "- Failed count: 0",
        "",
        "## Generated Files",
        "",
    ]
    for item in generated:
        lines.append(
            f"- `{item.line.output_name}`: mode={item.mode}, duration={item.duration:.3f}s, "
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
    parser.add_argument("--temperature", type=float, default=0.72)
    parser.add_argument("--top-p", type=float, default=0.82)
    parser.add_argument("--top-k", type=int, default=30)
    parser.add_argument("--max-new-tokens", type=int, default=300)
    parser.add_argument("--no-copy-assets", action="store_true")
    args = parser.parse_args()

    require_preflight()
    started = datetime.now().strftime("%Y%m%d_%H%M%S")
    run_dir = GENERATED_ROOT / f"guest04_fish_s2_full_dialogue_{started}"
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
    prompt_tokens = [encode_audio(REFERENCE_FILE, codec, "cuda").cpu()]

    generated: list[GeneratedLine] = []
    for index, line in enumerate(LINES, start=1):
        print(f"[Guest04 {index:02d}/{len(LINES):02d}] {line.line_id}", flush=True)
        generated.append(
            generate_line(
                line,
                model,
                decode_one_token,
                codec,
                raw_dir,
                final_dir,
                prompt_tokens,
                args.max_new_tokens,
                args.temperature,
                args.top_p,
                args.top_k,
            )
        )
        torch.cuda.empty_cache()

    validate_final_set(final_dir)
    backup_dir = None
    if not args.no_copy_assets:
        backup_dir = backup_guest04(started)
        copy_to_assets(final_dir)
        validate_final_set(ASSET_DIR)

    report_path = REPORT_ROOT / "guest04_fish_s2_generation_report.md"
    write_report(report_path, run_dir, raw_dir, final_dir, generated, backup_dir, started, time.perf_counter() - t0, args)
    print(f"Generated {len(generated)} Guest 4 WAVs", flush=True)
    print(f"Final staging: {final_dir}", flush=True)
    print(f"Assets: {ASSET_DIR}", flush=True)
    print(f"Report: {report_path}", flush=True)


if __name__ == "__main__":
    main()
