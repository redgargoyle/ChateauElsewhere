#!/usr/bin/env python3
"""Generate 8 F5-TTS arrival auditions from short YouTube British accent refs.

This is an audition-only script. It does not write to Unity Assets.
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
PROJECT_ROOT = Path(__file__).resolve().parents[2]
WORK_ROOT = HOME / "Desktop" / "Chateau_Voice_Auditions" / "YouTube_British_Accent_Refs"
SOURCE_AUDIO_DIR = WORK_ROOT / "source_audio"
REF_DIR = WORK_ROOT / "cut_refs"
DESKTOP_ROOT = HOME / "Desktop"

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

    @property
    def source_wav(self) -> Path:
        return SOURCE_AUDIO_DIR / f"{self.video_id}.wav"

    @property
    def ref_wav(self) -> Path:
        safe_name = self.guest_name.replace(" ", "_")
        return REF_DIR / f"Guest{self.guest:02d}_{safe_name}_youtube_ref.wav"

    @property
    def output_name(self) -> str:
        safe_name = self.guest_name.replace(" ", "_")
        return f"Guest{self.guest:02d}_{safe_name}_{self.line_id}.wav"


REFS = [
    YouTubeRef(
        1,
        "Lady",
        "CH1_G01_ENTRY",
        "Good evening. I trust the house remembers its manners better than the weather does.",
        "X627czLUsGY",
        "Say These 100 DAILY WORDS in a British Accent! (MODERN RP)",
        "https://www.youtube.com/watch?v=X627czLUsGY",
        34.40,
        5.10,
        "I promise you that by the end of this pronunciation training session.",
        50101,
        0.84,
    ),
    YouTubeRef(
        2,
        "Butler Guest",
        "CH1_G02_ENTRY",
        "Thank you. The drive was longer in the dark than I care to admit.",
        "PcIX-U5w5Ws",
        "The RP English Accent - What is it, how does it sound, and who uses it?",
        "https://www.youtube.com/watch?v=PcIX-U5w5Ws",
        153.20,
        5.40,
        "What is it, Benjamin? It's an accent. Okay? It's used with Standard English.",
        50202,
        0.86,
    ),
    YouTubeRef(
        3,
        "Mister Florian Knell",
        "CH1_G03_ENTRY",
        "Lovely to see you, dear Butler. Tell me, are we late, early, or merely dramatic?",
        "5Rde32zipGk",
        "British Accent Shadowing | Pride and Prejudice | Learn RP Pronunciation - Part 1",
        "https://www.youtube.com/watch?v=5Rde32zipGk",
        44.64,
        5.35,
        "It is a truth universally acknowledged that a single man in possession of a good fortune.",
        50303,
        0.87,
    ),
    YouTubeRef(
        4,
        "Countess Elowen Dusk",
        "CH1_G04_ENTRY",
        "Good evening, Butler. The road up here has the cheerful shape of a warning.",
        "FyyT2jmVPAk",
        "One Woman, 17 British Accents - Anglophenia Ep 5",
        "https://www.youtube.com/watch?v=FyyT2jmVPAk",
        36.50,
        4.70,
        "Good evening. It's nine o'clock and this is the news. I'm very important.",
        50404,
        0.80,
    ),
    YouTubeRef(
        5,
        "Baron Hector Glass",
        "CH1_G05_ENTRY",
        "Good evening. I hope the evening has not started without us.",
        "M7RWz0xOJAg",
        "If you can repeat after me, you will get a BRITISH Accent! (Modern RP)",
        "https://www.youtube.com/watch?v=M7RWz0xOJAg",
        13.28,
        5.00,
        "Firstly, you need to be able to say the individual words correctly.",
        50505,
        0.86,
    ),
    YouTubeRef(
        6,
        "Lady Sabine Marrow",
        "CH1_G06_ENTRY",
        "Thank you. I nearly mistook the bell pull for a funeral cord.",
        "RFT5_RT-gKk",
        "Your British Accent Is Missing These Critical Sounds",
        "https://www.youtube.com/watch?v=RFT5_RT-gKk",
        23.10,
        5.00,
        "Rather than region, though it is widely accepted as the Southern English accent.",
        50606,
        0.84,
    ),
    YouTubeRef(
        7,
        "Lord Ambrose Veil",
        "CH1_G07_ENTRY",
        "Lovely to see you. The chateau looks almost awake tonight.",
        "Nj0Rh__1kDw",
        "How to sound posh - Part one",
        "https://www.youtube.com/watch?v=Nj0Rh__1kDw",
        29.75,
        5.10,
        "People who wish to show a level of intelligence or class in their speech will generally speak as I do.",
        50707,
        0.86,
    ),
    YouTubeRef(
        8,
        "Madame Coralie Thread",
        "CH1_G08_ENTRY",
        "Good evening, Butler. I see the house has chosen its most severe face.",
        "djS6wFzsviI",
        "How to Sound Like a British Person (British RP Accent Lesson)",
        "https://www.youtube.com/watch?v=djS6wFzsviI",
        32.40,
        5.00,
        "When I say British accent, that is what I'm talking about.",
        50808,
        0.82,
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
    seen: set[str] = set()
    for ref in REFS:
        if ref.video_id in seen:
            continue
        seen.add(ref.video_id)
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
    for ref in REFS:
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
                        "highpass=f=80",
                        "lowpass=f=11500",
                        "afftdn=nf=-30",
                        "afade=t=in:st=0:d=0.02",
                        f"apad=pad_dur=0.35",
                        "atrim=0:5.6",
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
                    "highpass=f=80",
                    "lowpass=f=11500",
                    "afftdn=nf=-32",
                    "equalizer=f=260:t=q:w=1.0:g=-0.5",
                    "equalizer=f=3200:t=q:w=0.9:g=0.9",
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


def write_report(path: Path, output_dir: Path, generated: list[tuple[YouTubeRef, Path, float, float]], elapsed: float) -> None:
    lines = [
        "# F5-TTS YouTube British Accent Reference Arrival Audition",
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
        f"- Output folder: `{output_dir}`",
        f"- Command used: `{sys.executable} {Path(__file__).resolve()}`",
        "",
        "## Generated Files",
        "",
    ]
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
            ]
        )
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    require_cuda()
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    start = time.time()
    output_dir = DESKTOP_ROOT / f"British_Victorian_YouTube_Accent_F5_Arrivals_{timestamp}"
    raw_dir = WORK_ROOT / f"raw_f5_youtube_refs_{timestamp}"
    temp_dir = WORK_ROOT / f"temp_48k_{timestamp}"
    output_dir.mkdir(parents=True, exist_ok=False)
    raw_dir.mkdir(parents=True, exist_ok=False)
    temp_dir.mkdir(parents=True, exist_ok=False)

    download_sources()
    cut_refs()

    print(f"Loading {MODEL_NAME} on {DEVICE}...")
    model = F5TTS(model=MODEL_NAME, device=DEVICE)

    generated: list[tuple[YouTubeRef, Path, float, float]] = []
    for ref in REFS:
        print(f"[Guest {ref.guest:02d}] {ref.guest_name} using {ref.video_id}")
        wav, sr, _spec = model.infer(
            ref_file=str(ref.ref_wav),
            ref_text=ref.ref_text,
            gen_text=ref.gen_text,
            show_info=lambda *args, **_kwargs: print(*args),
            target_rms=0.1,
            cross_fade_duration=0.18,
            sway_sampling_coef=-1.0,
            cfg_strength=2.25,
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

    report_path = output_dir / "AUDITION_REPORT.md"
    write_report(report_path, output_dir, generated, time.time() - start)
    print(f"Generated {len(generated)} YouTube-reference F5 arrival WAVs.")
    print(f"Output: {output_dir}")
    print(f"Report: {report_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
