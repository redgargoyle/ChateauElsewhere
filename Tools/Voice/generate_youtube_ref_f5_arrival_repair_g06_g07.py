#!/usr/bin/env python3
"""Repair Guest 6 and Guest 7 arrival auditions with fresh YouTube RP refs.

Audition-only. Copies the accepted arrival files for the other guests and
generates new F5-TTS replacements for Guests 6 and 7. It does not write to
Unity Assets.
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
from f5_tts.api import F5TTS


HOME = Path.home()
WORK_ROOT = HOME / "Desktop" / "Chateau_Voice_Auditions" / "YouTube_British_Accent_Refs"
SOURCE_AUDIO_DIR = WORK_ROOT / "source_audio"
REF_DIR = WORK_ROOT / "cut_refs"
DESKTOP_ROOT = HOME / "Desktop"

BASE_AUDITION_DIR = HOME / "Desktop" / "British_Victorian_YouTube_Accent_F5_Arrivals_20260619_190625"

MODEL_NAME = "F5TTS_v1_Base"
DEVICE = "cuda"
FINAL_SAMPLE_RATE = 48000
TARGET_PEAK = 10 ** (-3.0 / 20.0)
RESAMPLE_FILTER = f"aresample=osr={FINAL_SAMPLE_RATE}:filter_size=64:phase_shift=10:linear_interp=0"


@dataclass(frozen=True)
class YouTubeRef:
    guest: int
    guest_name: str
    line_id: str
    gen_text: str
    video_id: str
    video_title: str
    source_url: str
    start: float
    duration: float
    ref_text: str
    seed: int
    speed: float
    notes: str

    @property
    def source_wav(self) -> Path:
        return SOURCE_AUDIO_DIR / f"{self.video_id}.wav"

    @property
    def ref_wav(self) -> Path:
        safe_name = self.guest_name.replace(" ", "_")
        return REF_DIR / f"Guest{self.guest:02d}_{safe_name}_repair_youtube_ref.wav"

    @property
    def output_name(self) -> str:
        safe_name = self.guest_name.replace(" ", "_")
        return f"Guest{self.guest:02d}_{safe_name}_{self.line_id}.wav"


ACCEPTED_FILES = [
    "Guest01_Lady_CH1_G01_ENTRY.wav",
    "Guest02_Butler_Guest_CH1_G02_ENTRY.wav",
    "Guest03_Mister_Florian_Knell_CH1_G03_ENTRY.wav",
    "Guest04_Countess_Elowen_Dusk_CH1_G04_ENTRY.wav",
    "Guest05_Baron_Hector_Glass_CH1_G05_ENTRY.wav",
    "Guest08_Madame_Coralie_Thread_CH1_G08_ENTRY.wav",
]


REPAIRS = [
    YouTubeRef(
        6,
        "Lady Sabine Marrow",
        "CH1_G06_ENTRY",
        "Thank you. I nearly mistook the bell pull for a funeral cord.",
        "gdpvo4w0mZc",
        "How to Learn a British Accent *Fast* (Modern RP)",
        "https://www.youtube.com/watch?v=gdpvo4w0mZc",
        18.56,
        5.36,
        "If you're new here, my name's Izzy. I'm a final year medical student at Cambridge University.",
        51606,
        0.82,
        "Newer female modern RP reference, chosen to improve clarity and avoid the prior low/quiet Guest 6 result.",
    ),
    YouTubeRef(
        7,
        "Lord Ambrose Veil",
        "CH1_G07_ENTRY",
        "Lovely to see you. The chateau looks almost awake tonight.",
        "_QfimKdlCGI",
        "3 Posh RP Accent Mini Tutorials - Conservative, Classic & Modern RP",
        "https://www.youtube.com/watch?v=_QfimKdlCGI",
        36.56,
        5.40,
        "Oh, it's marvelous. Wonderful. It's utterly delightful. This perfect pianist that I've just seen in this concert hall.",
        51707,
        0.84,
        "Fresh male posh/RP reference, chosen to separate Guest 7 from the existing men while keeping a clear British accent.",
    ),
]


def run(command: list[str]) -> None:
    subprocess.run(command, check=True)


def run_text(command: list[str]) -> str:
    try:
        return subprocess.run(command, check=True, text=True, capture_output=True).stdout.strip()
    except Exception as exc:
        return f"unavailable: {exc}"


def require_cuda() -> None:
    if not torch.cuda.is_available():
        raise RuntimeError("CUDA is not available in the f5-tts environment")
    name = torch.cuda.get_device_name(0)
    if "RTX 5090" not in name:
        raise RuntimeError(f"Expected RTX 5090, got {name}")


def download_sources() -> None:
    SOURCE_AUDIO_DIR.mkdir(parents=True, exist_ok=True)
    for ref in REPAIRS:
        if ref.source_wav.exists():
            continue
        run(
            [
                "yt-dlp",
                "--no-warnings",
                "-f",
                "bestaudio/best",
                "--extract-audio",
                "--audio-format",
                "wav",
                "-o",
                str(SOURCE_AUDIO_DIR / f"{ref.video_id}.%(ext)s"),
                ref.source_url,
            ]
        )


def cut_refs() -> None:
    REF_DIR.mkdir(parents=True, exist_ok=True)
    for ref in REPAIRS:
        run(
            [
                "ffmpeg",
                "-hide_banner",
                "-loglevel",
                "error",
                "-y",
                "-ss",
                f"{ref.start:.3f}",
                "-t",
                f"{ref.duration:.3f}",
                "-i",
                str(ref.source_wav),
                "-af",
                ",".join(
                    [
                        "highpass=f=85",
                        "lowpass=f=12000",
                        "afftdn=nf=-32",
                        "loudnorm=I=-20:LRA=8:TP=-3",
                        "afade=t=in:st=0:d=0.02",
                        "apad=pad_dur=0.30",
                        "atrim=0:5.8",
                    ]
                ),
                "-ac",
                "1",
                "-ar",
                "24000",
                "-c:a",
                "pcm_s16le",
                str(ref.ref_wav),
            ]
        )


def add_pauses(wav: np.ndarray, sr: int) -> np.ndarray:
    data = np.asarray(wav, dtype=np.float32)
    if data.ndim == 2:
        data = data.mean(axis=1)
    pause = np.zeros(max(1, int(sr * 0.10)), dtype=np.float32)
    return np.concatenate([pause, np.nan_to_num(data), pause])


def master_audio(input_path: Path, temp_path: Path, output_path: Path) -> tuple[float, float]:
    run(
        [
            "ffmpeg",
            "-hide_banner",
            "-loglevel",
            "error",
            "-y",
            "-i",
            str(input_path),
            "-af",
            ",".join(
                [
                    "highpass=f=85",
                    "lowpass=f=12000",
                    "afftdn=nf=-34",
                    "acompressor=threshold=-21dB:ratio=1.8:attack=5:release=80:makeup=1.4",
                    "equalizer=f=260:t=q:w=1.0:g=-0.6",
                    "equalizer=f=3400:t=q:w=0.9:g=1.2",
                    "loudnorm=I=-18:LRA=8:TP=-3",
                    RESAMPLE_FILTER,
                ]
            ),
            "-ac",
            "1",
            "-c:a",
            "pcm_f32le",
            str(temp_path),
        ]
    )
    data, sr = sf.read(temp_path, always_2d=True, dtype="float32")
    if sr != FINAL_SAMPLE_RATE:
        raise ValueError(f"Expected {FINAL_SAMPLE_RATE}, got {sr}: {temp_path}")
    mono = np.nan_to_num(data.mean(axis=1), nan=0.0, posinf=0.0, neginf=0.0)
    peak = float(np.max(np.abs(mono))) if mono.size else 0.0
    if peak > 0:
        mono = mono * min(TARGET_PEAK / peak, 32.0)
    mono = np.clip(mono, -0.999, 0.999)
    sf.write(output_path, mono, FINAL_SAMPLE_RATE, subtype="PCM_16")
    temp_path.unlink(missing_ok=True)
    return float(np.max(np.abs(mono))) if mono.size else 0.0, len(mono) / FINAL_SAMPLE_RATE


def validate(path: Path) -> tuple[float, float]:
    info = sf.info(path)
    if info.samplerate != FINAL_SAMPLE_RATE or info.channels != 1 or info.subtype != "PCM_16":
        raise ValueError(f"Bad format {path}: {info}")
    data, sr = sf.read(path, always_2d=True, dtype="float32")
    peak = float(np.max(np.abs(data))) if data.size else 0.0
    dur = len(data) / sr
    if peak <= 0.001 or peak >= 1.0 or dur < 0.5:
        raise ValueError(f"Bad audio {path}: peak={peak:.6f}, dur={dur:.3f}")
    return peak, dur


def write_report(
    path: Path,
    output_dir: Path,
    copied: list[tuple[Path, float, float]],
    generated: list[tuple[YouTubeRef, Path, float, float]],
    elapsed: float,
) -> None:
    lines = [
        "# F5-TTS Guest 6 and 7 YouTube RP Reference Repair Audition",
        "",
        "Audition-only. Unity Assets were not modified.",
        "",
        f"- Elapsed seconds: {elapsed:.1f}",
        f"- Model: {MODEL_NAME}",
        f"- CUDA available: {torch.cuda.is_available()}",
        f"- CUDA device: {torch.cuda.get_device_name(0) if torch.cuda.is_available() else 'none'}",
        f"- `nvidia-smi`: {run_text(['nvidia-smi', '--query-gpu=name,driver_version,memory.total', '--format=csv,noheader'])}",
        "- Final format: 48000 Hz mono PCM_16 WAV, peak-normalized near -3 dBFS.",
        "- YouTube reference clips are short local audition references only; review source licensing before shipping any voice derived from them.",
        f"- Base accepted audition folder: `{BASE_AUDITION_DIR}`",
        f"- Output folder: `{output_dir}`",
        f"- Command used: `{sys.executable} {Path(__file__).resolve()}`",
        "",
        "## Copied Accepted Files",
        "",
    ]
    for copied_path, peak, duration in copied:
        lines.append(f"- `{copied_path.name}` - peak={peak:.4f}, duration={duration:.3f}s")
    lines.extend(["", "## Generated Repair Files", ""])
    for ref, output_path, peak, duration in generated:
        lines.extend(
            [
                f"- `{output_path.name}`",
                f"  - guest: Guest {ref.guest:02d} {ref.guest_name}",
                f"  - source: {ref.video_title}",
                f"  - url: {ref.source_url}",
                f"  - cut: start={ref.start:.2f}s duration={ref.duration:.2f}s",
                f"  - ref_text: `{ref.ref_text}`",
                f"  - gen_text: `{ref.gen_text}`",
                f"  - seed={ref.seed}, speed={ref.speed:.2f}, peak={peak:.4f}, duration={duration:.3f}s",
                f"  - notes: {ref.notes}",
            ]
        )
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def copy_accepted(output_dir: Path) -> list[tuple[Path, float, float]]:
    copied: list[tuple[Path, float, float]] = []
    for name in ACCEPTED_FILES:
        src = BASE_AUDITION_DIR / name
        dst = output_dir / name
        if not src.exists():
            raise FileNotFoundError(src)
        shutil.copy2(src, dst)
        peak, duration = validate(dst)
        copied.append((dst, peak, duration))
    return copied


def main() -> int:
    require_cuda()
    if not BASE_AUDITION_DIR.exists():
        raise FileNotFoundError(BASE_AUDITION_DIR)

    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    start = time.time()
    output_dir = DESKTOP_ROOT / f"British_Victorian_YouTube_Accent_F5_Arrivals_G06_G07_Repair_{timestamp}"
    raw_dir = WORK_ROOT / f"raw_f5_youtube_refs_g06_g07_repair_{timestamp}"
    temp_dir = WORK_ROOT / f"temp_48k_g06_g07_repair_{timestamp}"
    output_dir.mkdir(parents=True, exist_ok=False)
    raw_dir.mkdir(parents=True, exist_ok=False)
    temp_dir.mkdir(parents=True, exist_ok=False)

    copied = copy_accepted(output_dir)
    download_sources()
    cut_refs()

    print(f"Loading {MODEL_NAME} on {DEVICE}...")
    model = F5TTS(model=MODEL_NAME, device=DEVICE)

    generated: list[tuple[YouTubeRef, Path, float, float]] = []
    for ref in REPAIRS:
        print(f"[Guest {ref.guest:02d}] {ref.guest_name} using {ref.video_id}")
        wav, sr, _spec = model.infer(
            ref_file=str(ref.ref_wav),
            ref_text=ref.ref_text,
            gen_text=ref.gen_text,
            show_info=lambda *args, **_kwargs: print(*args),
            target_rms=0.12,
            cross_fade_duration=0.16,
            sway_sampling_coef=-1.0,
            cfg_strength=2.35,
            nfe_step=64,
            speed=ref.speed,
            remove_silence=False,
            seed=ref.seed,
        )
        raw_path = raw_dir / ref.output_name
        sf.write(raw_path, add_pauses(wav, int(sr)), int(sr), subtype="PCM_16")
        output_path = output_dir / ref.output_name
        peak, duration = master_audio(raw_path, temp_dir / ref.output_name, output_path)
        validate(output_path)
        generated.append((ref, output_path, peak, duration))

    all_wavs = sorted(output_dir.glob("*.wav"))
    if len(all_wavs) != 8:
        raise RuntimeError(f"Expected 8 WAV files, found {len(all_wavs)} in {output_dir}")

    report_path = output_dir / "AUDITION_REPORT.md"
    write_report(report_path, output_dir, copied, generated, time.time() - start)
    print(f"Created 8-file audition folder with {len(generated)} repaired WAVs.")
    print(f"Output: {output_dir}")
    print(f"Report: {report_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
