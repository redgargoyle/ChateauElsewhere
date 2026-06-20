#!/usr/bin/env python3
"""Create game-dialogue F5 reference anchors for Guests 6 and 7.

This one-time bridge uses the approved RP voice samples only to synthesize the
actual entry dialogue. The resulting anchors can then be used by the full
dialogue generator with game-dialogue-only ref_text.
"""

from __future__ import annotations

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


PROJECT_ROOT = Path(__file__).resolve().parents[2]
HOME = Path.home()
VOICE_ROOT = PROJECT_ROOT / "Tools" / "Voice"
APPROVED_SAMPLE_DIR = HOME / "Desktop" / "British_RP_Reference_Samples_Guest06_Guest07_20260619_192959"
ANCHOR_ROOT = VOICE_ROOT / "reference_clips" / "game_dialogue_anchors"
REPORT_ROOT = VOICE_ROOT / "reports"

MODEL_NAME = "F5TTS_v1_Base"
DEVICE = "cuda"
FINAL_SAMPLE_RATE = 48000
TARGET_PEAK = 10 ** (-3.0 / 20.0)
RESAMPLE_FILTER = f"aresample=osr={FINAL_SAMPLE_RATE}:filter_size=64:phase_shift=10:linear_interp=0"


@dataclass(frozen=True)
class AnchorConfig:
    guest: int
    name: str
    source_ref: Path
    source_ref_text: str
    game_text: str
    seed: int
    speed: float

    @property
    def output_path(self) -> Path:
        return ANCHOR_ROOT / f"guest{self.guest:02d}_game_anchor.wav"


ANCHORS = [
    AnchorConfig(
        6,
        "Lady Sabine Marrow",
        APPROVED_SAMPLE_DIR / "G06_A_Female_Emma_ModernRP.wav",
        "These are five things that you can easily change about your English pronunciation and accent.",
        "Thank you. I nearly mistook the bell pull for a funeral cord.",
        46606,
        0.84,
    ),
    AnchorConfig(
        7,
        "Lord Ambrose Veil",
        APPROVED_SAMPLE_DIR / "G07_A_Male_Geoff_ClassicRP.wav",
        "King Charles's accent is basically classic Received Pronunciation, RP.",
        "Lovely to see you. The chateau looks almost awake tonight.",
        46707,
        0.88,
    ),
]


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
        raise RuntimeError(f"Expected RTX 5090, got CUDA device: {name}")


def add_pauses(wav: np.ndarray, sr: int) -> np.ndarray:
    data = np.asarray(wav, dtype=np.float32)
    if data.ndim == 2:
        data = data.mean(axis=1)
    pause = np.zeros(max(1, int(sr * 0.10)), dtype=np.float32)
    return np.concatenate([pause, np.nan_to_num(data), pause])


def master_audio_48k(input_path: Path, temp_path: Path, output_path: Path) -> tuple[float, float]:
    subprocess.run(
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
                    "highpass=f=75",
                    "lowpass=f=11000",
                    "afftdn=nf=-32",
                    "equalizer=f=260:t=q:w=1.0:g=-0.7",
                    "equalizer=f=3200:t=q:w=0.9:g=0.9",
                    RESAMPLE_FILTER,
                ]
            ),
            "-ac",
            "1",
            "-c:a",
            "pcm_f32le",
            str(temp_path),
        ],
        check=True,
    )
    data, sample_rate = sf.read(temp_path, always_2d=True, dtype="float32")
    if sample_rate != FINAL_SAMPLE_RATE:
        raise ValueError(f"Expected {FINAL_SAMPLE_RATE} Hz, got {sample_rate} for {temp_path}")
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
        raise ValueError(f"Bad format: {path}: {info}")
    data, sample_rate = sf.read(path, always_2d=True, dtype="float32")
    peak = float(np.max(np.abs(data))) if data.size else 0.0
    duration = len(data) / sample_rate
    if peak <= 0.001 or peak >= 1.0 or duration < 0.5:
        raise ValueError(f"Bad audio: {path}: peak={peak:.6f}, duration={duration:.3f}")
    return peak, duration


def main() -> int:
    require_cuda()
    for anchor in ANCHORS:
        if not anchor.source_ref.exists():
            raise FileNotFoundError(anchor.source_ref)

    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    start = time.time()
    ANCHOR_ROOT.mkdir(parents=True, exist_ok=True)
    raw_dir = ANCHOR_ROOT / f"raw_anchor_{timestamp}"
    temp_dir = ANCHOR_ROOT / f"temp_anchor_{timestamp}"
    raw_dir.mkdir(parents=True, exist_ok=False)
    temp_dir.mkdir(parents=True, exist_ok=False)

    print(f"Loading {MODEL_NAME} on {DEVICE}...")
    model = F5TTS(model=MODEL_NAME, device=DEVICE)

    generated: list[tuple[AnchorConfig, float, float]] = []
    for anchor in ANCHORS:
        print(f"[Guest {anchor.guest:02d}] Creating game-dialogue anchor for {anchor.name}")
        wav, sr, _spec = model.infer(
            ref_file=str(anchor.source_ref),
            ref_text=anchor.source_ref_text,
            gen_text=anchor.game_text,
            show_info=lambda *args, **_kwargs: print(*args),
            target_rms=0.1,
            cross_fade_duration=0.15,
            sway_sampling_coef=-1.0,
            cfg_strength=2.0,
            nfe_step=64,
            speed=anchor.speed,
            remove_silence=False,
            seed=anchor.seed,
        )
        raw_path = raw_dir / anchor.output_path.name
        temp_path = temp_dir / anchor.output_path.name
        sf.write(raw_path, add_pauses(wav, int(sr)), int(sr), subtype="PCM_16")
        peak, duration = master_audio_48k(raw_path, temp_path, anchor.output_path)
        validate(anchor.output_path)
        generated.append((anchor, peak, duration))

    report_path = REPORT_ROOT / "guest06_07_game_dialogue_anchor_report_f5.md"
    lines = [
        "# F5-TTS Guest 6 and 7 Game-Dialogue Anchor Report",
        "",
        "These anchors are for local generation only. Unity gameplay, subtitles, and playback hooks were not modified.",
        "",
        f"- Started: {timestamp}",
        f"- Elapsed seconds: {time.time() - start:.1f}",
        f"- CUDA device: {torch.cuda.get_device_name(0)}",
        f"- `nvidia-smi`: {run_text(['nvidia-smi', '--query-gpu=name,driver_version,memory.total', '--format=csv,noheader'])}",
        "- Final format: 48000 Hz, mono, PCM_16 WAV, peak-normalized near -3 dBFS.",
        "- Purpose: convert approved RP samples into game-dialogue reference anchors so the full 160-line pass uses game-dialogue-only ref_text.",
        "",
        "## Anchors",
        "",
    ]
    for anchor, peak, duration in generated:
        lines.extend(
            [
                f"- Guest {anchor.guest:02d} {anchor.name}",
                f"  - output: `{anchor.output_path}`",
                f"  - final_game_ref_text: `{anchor.game_text}`",
                f"  - seed: {anchor.seed}",
                f"  - speed: {anchor.speed:.2f}",
                f"  - peak: {peak:.4f}",
                f"  - duration: {duration:.3f}s",
            ]
        )
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"Generated {len(generated)} game-dialogue anchors.")
    print(f"Anchor folder: {ANCHOR_ROOT}")
    print(f"Report: {report_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
