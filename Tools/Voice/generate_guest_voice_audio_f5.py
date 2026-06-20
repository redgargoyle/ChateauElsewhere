#!/usr/bin/env python3
"""Generate Chateau Chantilly guest dialogue with local F5-TTS.

The script uses game-dialogue reference clips as fixed F5 references for every
line. Accepted arrival clips and generated game-dialogue anchors are mastered
directly for their own entry lines.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import io
import shutil
import subprocess
import sys
import time
from collections import Counter, defaultdict
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path

import numpy as np
import soundfile as sf
import torch
from f5_tts.api import F5TTS


PROJECT_ROOT = Path(__file__).resolve().parents[2]
HOME = Path.home()

PROMPT_PATHS = [
    PROJECT_ROOT / "Tools" / "Voice" / "F5_TTS_FULL_GUEST_AUDIO_GENERATION_PROMPT.md",
    HOME / "Desktop" / "Chateau_Voice_Auditions" / "F5_TTS" / "F5_TTS_FULL_GUEST_AUDIO_GENERATION_PROMPT.md",
    HOME / ".codex" / "attachments" / "22a65f94-5773-464c-9e1c-2ba2f7c5f415" / "pasted-text.txt",
]

NAMED_ACCENT_REF_DIR = HOME / "Desktop" / "British_Victorian_YouTube_Accent_F5_Arrivals_G06_G07_Repair_20260619_192020"
ASSET_GUEST_ROOT = PROJECT_ROOT / "Assets" / "Audio" / "Voice" / "Guests"
VOICE_ROOT = PROJECT_ROOT / "Tools" / "Voice"
REFERENCE_ROOT = VOICE_ROOT / "reference_clips"
GAME_ANCHOR_DIR = REFERENCE_ROOT / "game_dialogue_anchors"
REPORT_ROOT = VOICE_ROOT / "reports"
GENERATED_ROOT = VOICE_ROOT / "generated_f5_guest_dialogue"
BACKUP_ROOT = VOICE_ROOT / "backups"

MODEL_NAME = "F5TTS_v1_Base"
DEVICE = "cuda"
FINAL_SAMPLE_RATE = 48000
TARGET_PEAK = 10 ** (-3.0 / 20.0)
PAUSE_SECONDS = 0.10
RESAMPLE_FILTER = f"aresample=osr={FINAL_SAMPLE_RATE}:filter_size=64:phase_shift=10:linear_interp=0"
CONSTANT_SEED_GUESTS = {2, 3, 4, 6, 7}


@dataclass(frozen=True)
class GuestConfig:
    guest: int
    folder: str
    display_name: str
    ref_source: Path
    ref_text: str
    entry_line_id: str
    seed_base: int
    normal_speed: float


@dataclass(frozen=True)
class DialogueLine:
    line_id: str
    speaker: str
    text: str
    guest: int
    index_for_guest: int

    @property
    def output_name(self) -> str:
        return f"{self.line_id}.wav"


@dataclass
class GeneratedFile:
    line: DialogueLine
    raw_path: Path
    final_path: Path
    asset_path: Path
    seed: int
    speed: float
    peak: float
    duration: float
    mode: str
    attempts: int


GUESTS = {
    1: GuestConfig(
        1,
        "Guest01",
        "Lady",
        NAMED_ACCENT_REF_DIR / "Guest01_Lady_CH1_G01_ENTRY.wav",
        "Good evening. I trust the house remembers its manners better than the weather does.",
        "CH1_G01_ENTRY",
        21101,
        0.86,
    ),
    2: GuestConfig(
        2,
        "Guest02",
        "Butler Guest",
        NAMED_ACCENT_REF_DIR / "Guest02_Butler_Guest_CH1_G02_ENTRY.wav",
        "Thank you. The drive was longer in the dark than I care to admit.",
        "CH1_G02_ENTRY",
        32202,
        0.86,
    ),
    3: GuestConfig(
        3,
        "Guest03",
        "Mister Florian Knell",
        NAMED_ACCENT_REF_DIR / "Guest03_Mister_Florian_Knell_CH1_G03_ENTRY.wav",
        "Lovely to see you, dear Butler. Tell me, are we late, early, or merely dramatic?",
        "CH1_G03_ENTRY",
        33303,
        0.87,
    ),
    4: GuestConfig(
        4,
        "Guest04",
        "Countess Elowen Dusk",
        NAMED_ACCENT_REF_DIR / "Guest04_Countess_Elowen_Dusk_CH1_G04_ENTRY.wav",
        "Good evening, Butler. The road up here has the cheerful shape of a warning.",
        "CH1_G04_ENTRY",
        34404,
        0.78,
    ),
    5: GuestConfig(
        5,
        "Guest05",
        "Baron Hector Glass",
        NAMED_ACCENT_REF_DIR / "Guest05_Baron_Hector_Glass_CH1_G05_ENTRY.wav",
        "Good evening. I hope the evening has not started without us.",
        "CH1_G05_ENTRY",
        21505,
        0.90,
    ),
    6: GuestConfig(
        6,
        "Guest06",
        "Lady Sabine Marrow",
        GAME_ANCHOR_DIR / "guest06_game_anchor.wav",
        "Thank you. I nearly mistook the bell pull for a funeral cord.",
        "CH1_G06_ENTRY",
        36606,
        0.84,
    ),
    7: GuestConfig(
        7,
        "Guest07",
        "Lord Ambrose Veil",
        GAME_ANCHOR_DIR / "guest07_game_anchor.wav",
        "Lovely to see you. The chateau looks almost awake tonight.",
        "CH1_G07_ENTRY",
        21707,
        0.88,
    ),
    8: GuestConfig(
        8,
        "Guest08",
        "Madame Coralie Thread",
        NAMED_ACCENT_REF_DIR / "Guest08_Madame_Coralie_Thread_CH1_G08_ENTRY.wav",
        "Good evening, Butler. I see the house has chosen its most severe face.",
        "CH1_G08_ENTRY",
        21808,
        0.86,
    ),
}


def run_text(command: list[str]) -> str:
    try:
        return subprocess.run(command, check=True, text=True, capture_output=True).stdout.strip()
    except Exception as exc:
        return f"unavailable: {exc}"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def require_project_root() -> None:
    required = [PROJECT_ROOT / "Assets", PROJECT_ROOT / "ProjectSettings", PROJECT_ROOT / "Packages"]
    missing = [str(path) for path in required if not path.exists()]
    if missing:
        raise RuntimeError(f"Not at a Unity project root; missing: {missing}")


def require_cuda() -> None:
    if not any(Path("/dev").glob("nvidia*")):
        raise RuntimeError("No /dev/nvidia* devices are visible")
    if not torch.cuda.is_available():
        raise RuntimeError("CUDA is not available in the f5-tts environment")
    device_name = torch.cuda.get_device_name(0)
    if "RTX 5090" not in device_name:
        raise RuntimeError(f"Expected RTX 5090, got CUDA device: {device_name}")


def find_prompt_path() -> Path:
    for path in PROMPT_PATHS:
        if path.exists():
            return path
    raise FileNotFoundError(f"Could not find F5 dialogue prompt in: {[str(path) for path in PROMPT_PATHS]}")


def parse_dialogue(prompt_path: Path) -> list[DialogueLine]:
    text = prompt_path.read_text(encoding="utf-8")
    start_marker = "line_id,speaker,text"
    end_marker = "\nFINAL SUCCESS CONDITION:"
    if start_marker not in text or end_marker not in text:
        raise ValueError(f"Prompt does not contain the expected dialogue CSV markers: {prompt_path}")
    start = text.index(start_marker)
    end = text.index(end_marker, start)
    csv_text = text[start:end].strip()
    reader = csv.DictReader(io.StringIO(csv_text))

    per_guest_seen: dict[int, int] = defaultdict(int)
    lines: list[DialogueLine] = []
    for row in reader:
        speaker = row["speaker"].strip()
        if not speaker.startswith("Guest "):
            raise ValueError(f"Unexpected speaker field: {speaker}")
        guest = int(speaker.split(" ", 1)[1])
        index = per_guest_seen[guest]
        per_guest_seen[guest] += 1
        lines.append(
            DialogueLine(
                line_id=row["line_id"].strip(),
                speaker=speaker,
                text=row["text"],
                guest=guest,
                index_for_guest=index,
            )
        )

    counts = Counter(line.guest for line in lines)
    if len(lines) != 160:
        raise ValueError(f"Expected 160 dialogue lines, found {len(lines)}")
    bad_counts = {guest: counts[guest] for guest in range(1, 9) if counts[guest] != 20}
    if bad_counts:
        raise ValueError(f"Expected 20 lines per guest; bad counts: {bad_counts}")
    return lines


def ensure_reference_clips(timestamp: str) -> None:
    REFERENCE_ROOT.mkdir(parents=True, exist_ok=True)
    backup_dir = REFERENCE_ROOT / f"backup_before_f5_{timestamp}"
    for guest, config in GUESTS.items():
        if not config.ref_source.exists():
            raise FileNotFoundError(f"Missing accepted reference for Guest {guest}: {config.ref_source}")
        target = REFERENCE_ROOT / f"guest{guest:02d}_ref.wav"
        if target.exists() and sha256(target) != sha256(config.ref_source):
            backup_dir.mkdir(parents=True, exist_ok=True)
            shutil.copy2(target, backup_dir / target.name)
        shutil.copy2(config.ref_source, target)


def add_pauses(wav: np.ndarray, sr: int) -> np.ndarray:
    data = np.asarray(wav, dtype=np.float32)
    if data.ndim == 2:
        data = data.mean(axis=1)
    pause = np.zeros(max(1, int(sr * PAUSE_SECONDS)), dtype=np.float32)
    return np.concatenate([pause, np.nan_to_num(data), pause])


def master_audio_48k(input_path: Path, temp_path: Path, output_path: Path) -> tuple[float, float]:
    temp_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.parent.mkdir(parents=True, exist_ok=True)
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
                    "lowpass=f=11500",
                    "afftdn=nf=-34",
                    "equalizer=f=260:t=q:w=1.0:g=-0.8",
                    "equalizer=f=3200:t=q:w=0.9:g=1.0",
                    "equalizer=f=8200:t=q:w=1.2:g=0.5",
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
    final_peak = float(np.max(np.abs(mono))) if mono.size else 0.0
    duration = float(len(mono) / FINAL_SAMPLE_RATE) if mono.size else 0.0
    return final_peak, duration


def line_speed(line: DialogueLine) -> float:
    config = GUESTS[line.guest]
    line_id = line.line_id
    speed = config.normal_speed
    if "PANIC" in line_id:
        speed = min(0.98, speed + 0.06)
    elif "FOUND" in line_id:
        speed = max(0.84, speed - 0.02)
    elif "AMBIENT" in line_id or "EMPTY_BELL" in line_id:
        speed = max(0.84, speed - 0.01)
    elif "DINING_REVEAL" in line_id:
        speed = min(0.92, speed + 0.01)
    return speed


def expected_min_duration(text: str) -> float:
    words = max(1, len(text.split()))
    return max(0.55, min(2.8, words * 0.105))


def validate_audio_file(path: Path, min_duration: float = 0.25) -> tuple[float, float]:
    info = sf.info(path)
    if info.samplerate != FINAL_SAMPLE_RATE:
        raise ValueError(f"{path} is {info.samplerate} Hz, expected {FINAL_SAMPLE_RATE}")
    if info.channels != 1:
        raise ValueError(f"{path} has {info.channels} channels, expected mono")
    if info.subtype != "PCM_16":
        raise ValueError(f"{path} subtype is {info.subtype}, expected PCM_16")
    data, sample_rate = sf.read(path, always_2d=True, dtype="float32")
    duration = len(data) / sample_rate
    if duration < min_duration:
        raise ValueError(f"{path} is too short: {duration:.3f}s")
    peak = float(np.max(np.abs(data))) if data.size else 0.0
    if peak <= 0.001:
        raise ValueError(f"{path} appears silent: peak={peak:.6f}")
    if peak >= 1.0:
        raise ValueError(f"{path} clips: peak={peak:.6f}")
    return peak, duration


def generate_line(
    model: F5TTS,
    line: DialogueLine,
    raw_path: Path,
    final_path: Path,
    temp_path: Path,
    seed: int,
    speed: float,
) -> tuple[float, float]:
    config = GUESTS[line.guest]
    wav, sr, _spec = model.infer(
        ref_file=str(config.ref_source),
        ref_text=config.ref_text,
        gen_text=line.text,
        show_info=lambda *args, **_kwargs: print(*args),
        target_rms=0.1,
        cross_fade_duration=0.15,
        sway_sampling_coef=-1.0,
        cfg_strength=2.0,
        nfe_step=64,
        speed=speed,
        remove_silence=False,
        seed=seed,
    )
    raw_path.parent.mkdir(parents=True, exist_ok=True)
    sf.write(raw_path, add_pauses(wav, int(sr)), int(sr), subtype="PCM_16")
    peak, duration = master_audio_48k(raw_path, temp_path, final_path)
    validate_audio_file(final_path, expected_min_duration(line.text))
    return peak, duration


def stage_reference_entry(
    line: DialogueLine,
    raw_path: Path,
    final_path: Path,
    temp_path: Path,
) -> tuple[float, float]:
    config = GUESTS[line.guest]
    raw_path.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(config.ref_source, raw_path)
    peak, duration = master_audio_48k(raw_path, temp_path, final_path)
    validate_audio_file(final_path, expected_min_duration(line.text))
    return peak, duration


def backup_existing_assets(timestamp: str) -> Path | None:
    if not ASSET_GUEST_ROOT.exists():
        return None
    BACKUP_ROOT.mkdir(parents=True, exist_ok=True)
    backup_dir = BACKUP_ROOT / f"guest_voice_assets_before_f5_{timestamp}"
    shutil.copytree(ASSET_GUEST_ROOT, backup_dir)
    return backup_dir


def copy_to_assets(final_dir: Path) -> None:
    for guest in range(1, 9):
        config = GUESTS[guest]
        source_dir = final_dir / config.folder
        target_dir = ASSET_GUEST_ROOT / config.folder
        target_dir.mkdir(parents=True, exist_ok=True)
        for wav_path in sorted(source_dir.glob("*.wav")):
            shutil.copy2(wav_path, target_dir / wav_path.name)


def validate_final_set(root: Path, expected_lines: list[DialogueLine]) -> tuple[list[str], list[str], dict[int, int]]:
    expected = {f"{GUESTS[line.guest].folder}/{line.output_name}" for line in expected_lines}
    actual = {
        f"{path.parent.name}/{path.name}"
        for path in root.glob("Guest*/*.wav")
        if path.parent.name.startswith("Guest")
    }
    counts: dict[int, int] = {}
    for guest in range(1, 9):
        counts[guest] = len(list((root / GUESTS[guest].folder).glob("*.wav")))
    return sorted(expected - actual), sorted(actual - expected), counts


def write_report(
    report_path: Path,
    *,
    prompt_path: Path,
    run_dir: Path,
    final_dir: Path,
    asset_backup_dir: Path | None,
    generated: list[GeneratedFile],
    failures: list[str],
    started_at: str,
    elapsed: float,
    copied_to_assets: bool,
) -> None:
    counts = Counter(item.line.guest for item in generated)
    copied_reference_count = sum(1 for item in generated if item.mode == "accepted_reference_entry")
    synthesized_count = sum(1 for item in generated if item.mode == "f5_synthesis")
    missing, unexpected, final_counts = validate_final_set(final_dir, [item.line for item in generated])
    lines = [
        "# F5-TTS Guest Voice Generation Report",
        "",
        "Audio-generation only. Unity gameplay, subtitles, and playback hooks were not modified.",
        "",
        f"- Started: {started_at}",
        f"- Elapsed seconds: {elapsed:.1f}",
        f"- Project root: `{PROJECT_ROOT}`",
        f"- Dialogue prompt: `{prompt_path}`",
        f"- Model: {MODEL_NAME}",
        f"- Device requested: {DEVICE}",
        f"- Torch: {torch.__version__}",
        f"- CUDA build: {torch.version.cuda}",
        f"- CUDA available: {torch.cuda.is_available()}",
        f"- CUDA device: {torch.cuda.get_device_name(0) if torch.cuda.is_available() else 'none'}",
        f"- `nvidia-smi`: {run_text(['nvidia-smi', '--query-gpu=name,driver_version,memory.total', '--format=csv,noheader'])}",
        f"- F5-TTS source commit: {run_text(['git', '-C', str(HOME / 'ai-tts' / 'f5-tts' / 'F5-TTS-src'), 'rev-parse', '--short', 'HEAD'])}",
        "- Final format: 48000 Hz, mono, PCM_16 WAV, peak-normalized near -3 dBFS.",
        "- Voice drift precaution: every synthesized line uses the same per-guest `ref_file` and exact matching `ref_text`; no generated anchors are chained.",
        "- Entry handling: accepted arrival clips and game-dialogue anchors are used directly for entry lines.",
        f"- Staging folder: `{run_dir}`",
        f"- Final staging folder: `{final_dir}`",
        f"- Copied to Unity Assets: {copied_to_assets}",
        f"- Existing Assets backup: `{asset_backup_dir}`" if asset_backup_dir else "- Existing Assets backup: none needed",
        f"- Command used: `{sys.executable} {Path(__file__).resolve()}`",
        "",
        "## Counts",
        "",
        f"- Total final WAV count: {len(generated)}",
        f"- F5 synthesized count: {synthesized_count}",
        f"- Accepted reference entry count: {copied_reference_count}",
        "- Skipped count: 0",
        f"- Failed count: {len(failures)}",
        f"- Missing expected files: {len(missing)}",
        f"- Unexpected WAV files: {len(unexpected)}",
        "",
        "## Per-Guest Counts",
        "",
    ]
    for guest in range(1, 9):
        lines.append(f"- Guest {guest:02d} {GUESTS[guest].display_name}: generated={counts[guest]}, final_folder={final_counts[guest]}")

    lines.extend(["", "## Reference Clips", ""])
    for guest in range(1, 9):
        config = GUESTS[guest]
        lines.extend(
            [
                f"- Guest {guest:02d} {config.display_name}",
                f"  - ref_file: `{config.ref_source}`",
                f"  - ref_text: `{config.ref_text}`",
                f"  - seed_base: {config.seed_base}",
                f"  - normal_speed: {config.normal_speed:.2f}",
            ]
        )

    lines.extend(["", "## Generated Files", ""])
    for item in generated:
        lines.append(
            f"- Guest {item.line.guest:02d} `{item.line.line_id}`: mode={item.mode}, seed={item.seed}, "
            f"speed={item.speed:.2f}, attempts={item.attempts}, duration={item.duration:.3f}s, "
            f"peak={item.peak:.4f}, final=`{item.final_path}`, asset=`{item.asset_path}`"
        )

    lines.extend(["", "## Validation Issues", ""])
    if not missing and not unexpected:
        lines.append("- None")
    else:
        for path in missing:
            lines.append(f"- Missing: {path}")
        for path in unexpected:
            lines.append(f"- Unexpected: {path}")

    lines.extend(["", "## Failures", ""])
    lines.extend(f"- {failure}" for failure in failures) if failures else lines.append("- None")

    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--stage-only", action="store_true", help="Generate and validate staging files without copying into Assets.")
    return parser


def main() -> int:
    args = build_parser().parse_args()
    require_project_root()
    require_cuda()
    prompt_path = find_prompt_path()
    dialogue = parse_dialogue(prompt_path)

    started = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    start = time.time()

    for path in (REFERENCE_ROOT, REPORT_ROOT, GENERATED_ROOT):
        path.mkdir(parents=True, exist_ok=True)
    ensure_reference_clips(timestamp)

    run_dir = GENERATED_ROOT / f"f5_full_guest_dialogue_{timestamp}"
    raw_dir = run_dir / "raw_native"
    temp_dir = run_dir / "temp_48k_float"
    final_dir = run_dir / "final_48k"
    for guest in range(1, 9):
        (raw_dir / GUESTS[guest].folder).mkdir(parents=True, exist_ok=True)
        (temp_dir / GUESTS[guest].folder).mkdir(parents=True, exist_ok=True)
        (final_dir / GUESTS[guest].folder).mkdir(parents=True, exist_ok=True)

    print(f"Loading {MODEL_NAME} on {DEVICE}...")
    model = F5TTS(model=MODEL_NAME, device=DEVICE)

    generated: list[GeneratedFile] = []
    failures: list[str] = []
    for line in dialogue:
        config = GUESTS[line.guest]
        seed = config.seed_base if line.guest in CONSTANT_SEED_GUESTS else config.seed_base + line.index_for_guest
        speed = line_speed(line)
        raw_path = raw_dir / config.folder / line.output_name
        temp_path = temp_dir / config.folder / line.output_name
        final_path = final_dir / config.folder / line.output_name
        asset_path = ASSET_GUEST_ROOT / config.folder / line.output_name
        print(f"[Guest {line.guest:02d} {line.index_for_guest + 1:02d}/20] {line.line_id}")

        attempts = 0
        mode = "accepted_reference_entry" if line.line_id == config.entry_line_id else "f5_synthesis"
        last_error: Exception | None = None
        max_attempts = 1 if mode == "accepted_reference_entry" else 2
        while attempts < max_attempts:
            attempts += 1
            try:
                attempt_seed = seed if attempts == 1 else seed + 10000
                if mode == "accepted_reference_entry":
                    peak, duration = stage_reference_entry(line, raw_path, final_path, temp_path)
                else:
                    peak, duration = generate_line(model, line, raw_path, final_path, temp_path, attempt_seed, speed)
                generated.append(
                    GeneratedFile(
                        line=line,
                        raw_path=raw_path,
                        final_path=final_path,
                        asset_path=asset_path,
                        seed=attempt_seed,
                        speed=speed,
                        peak=peak,
                        duration=duration,
                        mode=mode,
                        attempts=attempts,
                    )
                )
                break
            except Exception as exc:
                last_error = exc
                print(f"FAILED attempt {attempts} for {line.line_id}: {exc}", file=sys.stderr)

        if last_error and (not generated or generated[-1].line.line_id != line.line_id):
            failures.append(f"{line.line_id}: {type(last_error).__name__}: {last_error}")

    expected_missing, unexpected, final_counts = validate_final_set(final_dir, dialogue)
    validation_errors = []
    if expected_missing:
        validation_errors.append(f"Missing expected files: {expected_missing}")
    if unexpected:
        validation_errors.append(f"Unexpected WAV files: {unexpected}")
    for guest, count in final_counts.items():
        if count != 20:
            validation_errors.append(f"Guest {guest:02d} final count is {count}, expected 20")
    if len(generated) != 160:
        validation_errors.append(f"Generated {len(generated)} files, expected 160")

    asset_backup_dir = None
    copied_to_assets = False
    if failures or validation_errors:
        failures.extend(validation_errors)
    elif not args.stage_only:
        asset_backup_dir = backup_existing_assets(timestamp)
        copy_to_assets(final_dir)
        copied_to_assets = True

    report_path = REPORT_ROOT / "guest_voice_generation_report_f5.md"
    write_report(
        report_path,
        prompt_path=prompt_path,
        run_dir=run_dir,
        final_dir=final_dir,
        asset_backup_dir=asset_backup_dir,
        generated=generated,
        failures=failures,
        started_at=started,
        elapsed=time.time() - start,
        copied_to_assets=copied_to_assets,
    )
    shutil.copy2(report_path, run_dir / "guest_voice_generation_report_f5.md")

    if failures:
        print(f"Completed with failures. Report: {report_path}")
        return 1

    print(f"Generated {len(generated)} final guest dialogue WAVs.")
    print(f"Staging: {run_dir}")
    print(f"Assets: {ASSET_GUEST_ROOT}")
    print(f"Report: {report_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
